using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class FlashOnDamagedEmission : MonoBehaviour
    {
        [System.Serializable]
        struct RendererIndexData
        {
            public Renderer Renderer;
            public int MaterialIndex;

            public RendererIndexData(Renderer renderer, int index)
            {
                Renderer = renderer;
                MaterialIndex = index;
            }
        }

        [Header("References")]
        [SerializeField] Health m_Health;
        [Tooltip("Only renderers under this root will be affected (optional)")]
        [SerializeField] Transform m_RenderersRoot;

        [Header("Filter")]
        [Tooltip("Only materials matching this reference will be flashed. If null, all renderer materials will be flashed.")]
        [SerializeField] Material m_TargetMaterial;

        [Header("Flash")]
        [Tooltip("Gradient for _EmissionColor over the flash duration")] [GradientUsageAttribute(true)]
        [SerializeField] Gradient m_EmissionGradient;
        [SerializeField] float m_FlashDuration = 0.5f;

        readonly List<RendererIndexData> m_TargetRenderers = new List<RendererIndexData>(32);
        MaterialPropertyBlock m_Mpb;
        float m_LastTimeDamaged = float.NegativeInfinity;
        float m_FlashEndTime = float.NegativeInfinity;
        bool m_IsFlashing;

        void Reset()
        {
            m_Health = GetComponent<Health>();
        }

        void Awake()
        {
            if (m_Health == null)
            {
                m_Health = GetComponent<Health>();
                if (m_Health == null)
                {
                    m_Health = GetComponentInParent<Health>();
                }
            }

            m_Mpb = new MaterialPropertyBlock();
            CacheRenderers();
            ApplyEmissionColor(GetGradientColor(1f));
        }

        void OnEnable()
        {
            if (m_Health != null)
            {
                m_Health.OnDamaged += OnDamaged;
            }
        }

        void OnDisable()
        {
            if (m_Health != null)
            {
                m_Health.OnDamaged -= OnDamaged;
            }
        }

        void Update()
        {
            if (!m_IsFlashing)
            {
                return;
            }

            float now = Time.time;
            if (now >= m_FlashEndTime)
            {
                ApplyEmissionColor(GetGradientColor(1f));
                m_IsFlashing = false;
                return;
            }

            float duration = Mathf.Max(0.0001f, m_FlashDuration);
            float t = (now - m_LastTimeDamaged) / duration;
            ApplyEmissionColor(GetGradientColor(t));
        }

        void OnDamaged(float damage, GameObject damageSource)
        {
            m_LastTimeDamaged = Time.time;
            m_FlashEndTime = m_LastTimeDamaged + Mathf.Max(0.0001f, m_FlashDuration);
            m_IsFlashing = true;
        }

        void CacheRenderers()
        {
            m_TargetRenderers.Clear();

            Transform root = m_RenderersRoot != null ? m_RenderersRoot : transform;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            if (m_TargetMaterial == null)
            {
                for (int r = 0; r < renderers.Length; r++)
                {
                    Renderer renderer = renderers[r];
                    if (renderer == null)
                    {
                        continue;
                    }

                    int matCount = renderer.sharedMaterials != null ? renderer.sharedMaterials.Length : 0;
                    for (int i = 0; i < matCount; i++)
                    {
                        m_TargetRenderers.Add(new RendererIndexData(renderer, i));
                    }
                }
                return;
            }

            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null || renderer.sharedMaterials == null)
                {
                    continue;
                }

                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    if (renderer.sharedMaterials[i] == m_TargetMaterial)
                    {
                        m_TargetRenderers.Add(new RendererIndexData(renderer, i));
                    }
                }
            }
        }

        Color GetGradientColor(float t)
        {
            if (m_EmissionGradient == null)
            {
                return Color.black;
            }
            return m_EmissionGradient.Evaluate(Mathf.Clamp01(t));
        }

        void ApplyEmissionColor(Color color)
        {
            m_Mpb.SetColor("_EmissionColor", color);
            for (int i = 0; i < m_TargetRenderers.Count; i++)
            {
                RendererIndexData data = m_TargetRenderers[i];
                if (data.Renderer != null)
                {
                    data.Renderer.SetPropertyBlock(m_Mpb, data.MaterialIndex);
                }
            }
        }
    }
}

