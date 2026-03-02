using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.GameFramework
{
    [DisallowMultipleComponent]
    public sealed class DebrisAutoRecycle : MonoBehaviour
    {
        public const float DefaultHoldDuration = 6f;
        public const float DefaultFadeDuration = 1.5f;

        struct RendererChannel
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public int ColorPropertyId;
            public Color BaseColor;
            public int DissolvePropertyId;
            public float DissolveStartValue;
            public float DissolveEndValue;
        }

        readonly List<RendererChannel> m_Channels = new List<RendererChannel>(32);
        MaterialPropertyBlock m_PropertyBlock;
        float m_HoldDuration = DefaultHoldDuration;
        float m_FadeDuration = DefaultFadeDuration;
        float m_SpawnTime;
        bool m_RuntimeReady;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        static readonly int DissolveThresholdId = Shader.PropertyToID("_DissolveThreshold");
        static readonly int AlphaClipThresholdId = Shader.PropertyToID("_AlphaClipThreshold");
        static readonly int CutoffId = Shader.PropertyToID("_Cutoff");

        public static void ConfigureOn(GameObject target, float holdDuration, float fadeDuration)
        {
            if (target == null)
            {
                return;
            }

            DebrisAutoRecycle recycler = target.GetComponent<DebrisAutoRecycle>();
            if (recycler == null)
            {
                recycler = target.AddComponent<DebrisAutoRecycle>();
            }

            recycler.Configure(holdDuration, fadeDuration);
        }

        public void Configure(float holdDuration, float fadeDuration)
        {
            m_HoldDuration = Mathf.Max(0f, holdDuration);
            m_FadeDuration = Mathf.Max(0.01f, fadeDuration);
            ResetRuntime();
        }

        void Awake()
        {
            ResetRuntime();
        }

        void OnEnable()
        {
            ResetRuntime();
        }

        void Update()
        {
            if (!m_RuntimeReady)
            {
                ResetRuntime();
            }

            float elapsed = Time.time - m_SpawnTime;
            if (elapsed < m_HoldDuration)
            {
                return;
            }

            float fadeElapsed = elapsed - m_HoldDuration;
            float t = Mathf.Clamp01(fadeElapsed / m_FadeDuration);
            float alpha = 1f - t;
            ApplyFade(alpha, t);

            if (t >= 1f)
            {
                DespawnOrDestroy();
            }
        }

        void ResetRuntime()
        {
            m_SpawnTime = Time.time;
            CacheRendererChannels();
            ApplyFade(1f, 0f);
            m_RuntimeReady = true;
        }

        void CacheRendererChannels()
        {
            m_Channels.Clear();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null || renderer.sharedMaterials == null)
                {
                    continue;
                }

                Material[] sharedMaterials = renderer.sharedMaterials;
                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    Material material = sharedMaterials[i];
                    if (material == null)
                    {
                        continue;
                    }

                    int colorPropertyId = ResolveColorPropertyId(material);
                    ResolveDissolveProperty(material, out int dissolvePropertyId, out float dissolveStartValue, out float dissolveEndValue);

                    if (colorPropertyId == 0 && dissolvePropertyId == 0)
                    {
                        continue;
                    }

                    m_Channels.Add(new RendererChannel
                    {
                        Renderer = renderer,
                        MaterialIndex = i,
                        ColorPropertyId = colorPropertyId,
                        BaseColor = colorPropertyId != 0 ? material.GetColor(colorPropertyId) : Color.white,
                        DissolvePropertyId = dissolvePropertyId,
                        DissolveStartValue = dissolveStartValue,
                        DissolveEndValue = dissolveEndValue
                    });
                }
            }
        }

        static int ResolveColorPropertyId(Material material)
        {
            if (material.HasProperty(BaseColorId))
            {
                return BaseColorId;
            }

            if (material.HasProperty(ColorId))
            {
                return ColorId;
            }

            return 0;
        }

        static void ResolveDissolveProperty(Material material, out int propertyId, out float startValue, out float endValue)
        {
            propertyId = 0;
            startValue = 0f;
            endValue = 1f;

            if (material.HasProperty(DissolveAmountId))
            {
                propertyId = DissolveAmountId;
                startValue = material.GetFloat(propertyId);
                return;
            }

            if (material.HasProperty(DissolveId))
            {
                propertyId = DissolveId;
                startValue = material.GetFloat(propertyId);
                return;
            }

            if (material.HasProperty(DissolveThresholdId))
            {
                propertyId = DissolveThresholdId;
                startValue = material.GetFloat(propertyId);
                return;
            }

            if (material.HasProperty(AlphaClipThresholdId))
            {
                propertyId = AlphaClipThresholdId;
                startValue = material.GetFloat(propertyId);
                return;
            }

            if (material.HasProperty(CutoffId))
            {
                propertyId = CutoffId;
                startValue = material.GetFloat(propertyId);
            }
        }

        void ApplyFade(float alphaMultiplier, float normalizedFade)
        {
            if (m_PropertyBlock == null)
            {
                m_PropertyBlock = new MaterialPropertyBlock();
            }

            for (int i = 0; i < m_Channels.Count; i++)
            {
                RendererChannel channel = m_Channels[i];
                if (channel.Renderer == null)
                {
                    continue;
                }

                m_PropertyBlock.Clear();

                bool hasDissolveProperty = channel.DissolvePropertyId != 0;
                if (hasDissolveProperty)
                {
                    float dissolveValue = Mathf.Lerp(channel.DissolveStartValue, channel.DissolveEndValue, normalizedFade);
                    m_PropertyBlock.SetFloat(channel.DissolvePropertyId, dissolveValue);
                }
                else if (channel.ColorPropertyId != 0)
                {
                    Color color = channel.BaseColor;
                    color.a *= alphaMultiplier;
                    m_PropertyBlock.SetColor(channel.ColorPropertyId, color);
                }

                channel.Renderer.SetPropertyBlock(m_PropertyBlock, channel.MaterialIndex);
            }
        }

        void DespawnOrDestroy()
        {
            PooledInstance pooled = GetComponent<PooledInstance>();
            if (pooled != null)
            {
                pooled.Despawn();
                return;
            }

            Destroy(gameObject);
        }
    }
}
