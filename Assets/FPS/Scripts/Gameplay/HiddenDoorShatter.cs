using System.Collections.Generic;
using UnityEngine;
using Unity.FPS.Gameplay;

namespace Unity.FPS.Game
{
    public class HiddenDoorShatter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Health m_Health;
        [SerializeField] Transform m_ChunksRoot;
        [SerializeField] Rigidbody[] m_Chunks;
        [SerializeField] Collider[] m_DisableCollidersOnShatter;

        [Header("Behavior")]
        [SerializeField] bool m_AutoFindChunks = true;
        [SerializeField] bool m_DetachChunksOnShatter = true;
        
        [Header("Collision Filtering")]
        [Tooltip("破碎后让碎块不与玩家发生物理交互（不影响与其它物体的碰撞）")]
        [SerializeField] bool m_IgnoreChunkCollisionWithPlayer = true;

        [Header("Forces")]
        [SerializeField] float m_ForwardImpulse = 12f;
        [SerializeField] float m_RandomImpulse = 3f;
        [SerializeField] float m_TorqueImpulse = 2f;

        [Header("Audio")]
        [SerializeField] SfxKey m_ShatterSfxKey = SfxKey.ExplosionSmall;
        [SerializeField] SfxKey m_CollapseSfxKey = SfxKey.StoneClapsed;
        [SerializeField] [Min(0f)] float m_CollapseSfxDelay = 0.15f;

        [Header("Game Feel")]
        [Tooltip("相机震动强度，0 表示不震动")]
        [SerializeField] [Min(0f)] float m_CameraShakeIntensity = 0.15f;
        [Tooltip("相机震动持续时间")]
        [SerializeField] [Min(0.01f)] float m_CameraShakeDuration = 0.35f;
        [Tooltip("震动包络形态（Normal=钟形，BigToSmall=由强到弱）")]
        [SerializeField] ShakeEnvelopeProfile m_CameraShakeProfile = ShakeEnvelopeProfile.Normal;
        [Tooltip("破碎时相机位置冲击（本地空间），(0,0,0) 表示不施加")]
        [SerializeField] Vector3 m_CameraPositionImpulse = Vector3.zero;
        [Tooltip("位置冲击回弹锐度，越大回弹越快")]
        [SerializeField] [Min(0.01f)] float m_CameraPositionImpulseSharpness = 18f;
        [Tooltip("仅当玩家与该门距离小于此值时触发相机反馈（0 表示不限制距离）")]
        [SerializeField] [Min(0f)] float m_GameFeelMaxDistance = 0f;

        [Header("Hit Flash")]
        [Tooltip("受击时闪烁的 Renderer 列表；为空且开启 Auto Find 时自动收集子物体 Renderer")]
        [SerializeField] Renderer[] m_HitFlashRenderers;
        [SerializeField] bool m_AutoFindHitFlashRenderers = true;
        [Tooltip("颜色属性名，URP 通常为 _BaseColor，Built-in 通常为 _Color")]
        [SerializeField] string m_HitFlashColorPropertyName = "_BaseColor";
        [SerializeField] Color m_HitFlashColor = new Color(1f, 1f, 1f, 1f);
        [Tooltip("闪烁总时长（秒），0 表示不闪烁")]
        [SerializeField] [Min(0f)] float m_HitFlashDuration = 0.08f;

        bool m_Shattered;
        bool m_HitFlashActive;
        float m_HitFlashTimeRemaining;
        int m_HitFlashColorPropertyId;
        MaterialPropertyBlock m_HitFlashBlock;
        Color[] m_HitFlashBaseColors;

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

            if (m_AutoFindChunks)
            {
                CacheChunks();
            }

            CacheHitFlashRenderers();
            SetChunksKinematic(true);
        }

        void OnEnable()
        {
            if (m_Health != null)
            {
                m_Health.OnDie += OnDie;
                m_Health.OnDamaged += OnDamaged;
            }
        }

        void OnDisable()
        {
            if (m_Health != null)
            {
                m_Health.OnDie -= OnDie;
                m_Health.OnDamaged -= OnDamaged;
            }
            CancelInvoke(nameof(PlayCollapseSfx));
        }

        void Update()
        {
            if (!m_HitFlashActive)
            {
                return;
            }

            m_HitFlashTimeRemaining -= Time.deltaTime;
            if (m_HitFlashTimeRemaining <= 0f)
            {
                m_HitFlashActive = false;
                ApplyHitFlash(0f);
                return;
            }

            float t01 = Mathf.Clamp01(m_HitFlashTimeRemaining / Mathf.Max(0.001f, m_HitFlashDuration));
            ApplyHitFlash(t01);
        }

        void CacheChunks()
        {
            Transform root = m_ChunksRoot != null ? m_ChunksRoot : transform;
            Rigidbody[] all = root.GetComponentsInChildren<Rigidbody>(true);
            if (all == null || all.Length == 0)
            {
                m_Chunks = new Rigidbody[0];
                return;
            }

            var list = new List<Rigidbody>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                Rigidbody rb = all[i];
                if (rb == null)
                {
                    continue;
                }

                if (rb.transform == transform)
                {
                    continue;
                }

                list.Add(rb);
            }

            m_Chunks = list.ToArray();
        }

        void CacheHitFlashRenderers()
        {
            m_HitFlashColorPropertyId = Shader.PropertyToID(string.IsNullOrEmpty(m_HitFlashColorPropertyName)
                ? "_BaseColor"
                : m_HitFlashColorPropertyName);

            if ((m_HitFlashRenderers == null || m_HitFlashRenderers.Length == 0) && m_AutoFindHitFlashRenderers)
            {
                m_HitFlashRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (m_HitFlashRenderers == null || m_HitFlashRenderers.Length == 0)
            {
                m_HitFlashBaseColors = null;
                return;
            }

            if (m_HitFlashBlock == null)
            {
                m_HitFlashBlock = new MaterialPropertyBlock();
            }

            m_HitFlashBaseColors = new Color[m_HitFlashRenderers.Length];
            for (int i = 0; i < m_HitFlashRenderers.Length; i++)
            {
                Renderer r = m_HitFlashRenderers[i];
                if (r == null)
                {
                    m_HitFlashBaseColors[i] = Color.white;
                    continue;
                }

                Color baseColor = Color.white;
                var mat = r.sharedMaterial;
                if (mat != null)
                {
                    if (mat.HasProperty(m_HitFlashColorPropertyId))
                    {
                        baseColor = mat.GetColor(m_HitFlashColorPropertyId);
                    }
                    else
                    {
                        int fallback = Shader.PropertyToID("_Color");
                        if (mat.HasProperty(fallback))
                        {
                            baseColor = mat.GetColor(fallback);
                        }
                    }
                }

                m_HitFlashBaseColors[i] = baseColor;
            }
        }

        void SetChunksKinematic(bool isKinematic)
        {
            if (m_Chunks == null)
            {
                return;
            }

            for (int i = 0; i < m_Chunks.Length; i++)
            {
                Rigidbody rb = m_Chunks[i];
                if (rb == null)
                {
                    continue;
                }

                rb.isKinematic = isKinematic;
                if (!isKinematic)
                {
                    rb.useGravity = true;
                }

                if (isKinematic)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.Sleep();
                }
                else
                {
                    rb.WakeUp();
                }
            }
        }

        void OnDie()
        {
            Shatter();
        }

        void OnDamaged(float damage, GameObject damageSource)
        {
            if (m_Shattered)
            {
                return;
            }

            TriggerHitFlash();
        }

        void TriggerHitFlash()
        {
            if (m_HitFlashDuration <= 0.001f)
            {
                return;
            }

            if (m_HitFlashRenderers == null || m_HitFlashRenderers.Length == 0 || m_HitFlashBaseColors == null)
            {
                return;
            }

            if (m_HitFlashBlock == null)
            {
                m_HitFlashBlock = new MaterialPropertyBlock();
            }

            m_HitFlashActive = true;
            m_HitFlashTimeRemaining = m_HitFlashDuration;
            ApplyHitFlash(1f);
        }

        void ApplyHitFlash(float intensity01)
        {
            if (m_HitFlashRenderers == null || m_HitFlashBaseColors == null)
            {
                return;
            }

            intensity01 = Mathf.Clamp01(intensity01);

            for (int i = 0; i < m_HitFlashRenderers.Length; i++)
            {
                Renderer r = m_HitFlashRenderers[i];
                if (r == null)
                {
                    continue;
                }

                r.GetPropertyBlock(m_HitFlashBlock);
                Color baseColor = i < m_HitFlashBaseColors.Length ? m_HitFlashBaseColors[i] : Color.white;
                m_HitFlashBlock.SetColor(m_HitFlashColorPropertyId, Color.Lerp(baseColor, m_HitFlashColor, intensity01));
                r.SetPropertyBlock(m_HitFlashBlock);
            }
        }

        public void Shatter()
        {
            if (m_Shattered)
            {
                return;
            }
            m_Shattered = true;
            m_HitFlashActive = false;
            if (m_ShatterSfxKey != SfxKey.None)
            {
                AudioUtility.PlaySfx(m_ShatterSfxKey, transform.position);
            }
            if (m_CollapseSfxKey != SfxKey.None)
            {
                CancelInvoke(nameof(PlayCollapseSfx));
                if (m_CollapseSfxDelay <= 0f)
                {
                    PlayCollapseSfx();
                }
                else
                {
                    Invoke(nameof(PlayCollapseSfx), m_CollapseSfxDelay);
                }
            }

            TriggerCameraFeedback();

            for (int i = 0; i < m_DisableCollidersOnShatter.Length; i++)
            {
                Collider c = m_DisableCollidersOnShatter[i];
                if (c != null)
                {
                    c.enabled = false;
                }
            }

            Vector3 direction = transform.forward;
            Vector3 hitPoint = transform.position;

            if (m_Health != null)
            {
                LastHitInfo hitInfo = m_Health.GetComponent<LastHitInfo>();
                if (hitInfo != null && hitInfo.HasValue)
                {
                    direction = hitInfo.LastDirection;
                    hitPoint = hitInfo.LastPoint;
                }
            }

            SetChunksKinematic(false);
            IgnoreChunkCollisionWithPlayerIfNeeded();

            if (m_Chunks == null)
            {
                return;
            }

            for (int i = 0; i < m_Chunks.Length; i++)
            {
                Rigidbody rb = m_Chunks[i];
                if (rb == null)
                {
                    continue;
                }

                if (m_DetachChunksOnShatter)
                {
                    rb.transform.SetParent(null, true);
                }

                Vector3 random = m_RandomImpulse > 0f ? UnityEngine.Random.insideUnitSphere * m_RandomImpulse : Vector3.zero;
                rb.AddForce(direction * m_ForwardImpulse + random, ForceMode.Impulse);

                if (m_TorqueImpulse > 0f)
                {
                    rb.AddTorque(UnityEngine.Random.insideUnitSphere * m_TorqueImpulse, ForceMode.Impulse);
                }
            }
        }
        
        void IgnoreChunkCollisionWithPlayerIfNeeded()
        {
            if (!m_IgnoreChunkCollisionWithPlayer || m_Chunks == null || m_Chunks.Length == 0)
            {
                return;
            }

            var actorsManager = FindObjectOfType<ActorsManager>();
            var player = actorsManager != null ? actorsManager.Player : null;
            if (player == null)
            {
                return;
            }

            Collider[] playerColliders = player.GetComponentsInChildren<Collider>(true);
            if (playerColliders == null || playerColliders.Length == 0)
            {
                return;
            }

            for (int i = 0; i < m_Chunks.Length; i++)
            {
                Rigidbody rb = m_Chunks[i];
                if (rb == null)
                {
                    continue;
                }

                Collider[] chunkColliders = rb.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < chunkColliders.Length; c++)
                {
                    Collider cc = chunkColliders[c];
                    if (cc == null || !cc.enabled)
                    {
                        continue;
                    }

                    for (int p = 0; p < playerColliders.Length; p++)
                    {
                        Collider pc = playerColliders[p];
                        if (pc == null || !pc.enabled)
                        {
                            continue;
                        }

                        Physics.IgnoreCollision(cc, pc, true);
                    }
                }
            }
        }

        void TriggerCameraFeedback()
        {
            if (m_CameraShakeIntensity <= 0f && m_CameraPositionImpulse.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var actorsManager = FindObjectOfType<ActorsManager>();
            var player = actorsManager != null ? actorsManager.Player : null;
            if (player == null)
            {
                return;
            }

            if (m_GameFeelMaxDistance > 0.001f)
            {
                float sqrDist = (player.transform.position - transform.position).sqrMagnitude;
                if (sqrDist > m_GameFeelMaxDistance * m_GameFeelMaxDistance)
                {
                    return;
                }
            }

            var cameraRig = player.GetComponent<FpsCameraRig>();
            if (cameraRig == null || cameraRig.CameraEffects == null)
            {
                return;
            }

            if (m_CameraShakeIntensity > 0f && m_CameraShakeDuration > 0.01f)
            {
                cameraRig.CameraEffects.PlayEnvelopeShake(this, m_CameraShakeIntensity, m_CameraShakeDuration, m_CameraShakeProfile);
            }

            if (m_CameraPositionImpulse.sqrMagnitude > 0.0001f)
            {
                cameraRig.CameraEffects.AddPositionImpulse(m_CameraPositionImpulse, m_CameraPositionImpulseSharpness);
            }
        }

        void PlayCollapseSfx()
        {
            if (m_CollapseSfxKey != SfxKey.None)
            {
                AudioUtility.PlaySfx(m_CollapseSfxKey, transform.position);
            }
        }
    }
}

