using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.GameFramework;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.FPS.AI
{
    [RequireComponent(typeof(Health), typeof(EnemyController))]
    public class ExploderDeathSequence : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] float m_PreExplosionDuration = 0.8f;
        [SerializeField] bool m_EnableScalePulse = true;
        [SerializeField] float m_ShrinkDuration = 0.12f;
        [SerializeField] float m_GrowDuration = 0.12f;
        [SerializeField] float m_ShrinkMultiplier = 0.75f;
        [SerializeField] float m_GrowMultiplier = 1.15f;

        [Header("Blink (Emission)")]
        [Tooltip("Only renderers under this root will be affected (optional)")]
        [SerializeField] Transform m_RenderersRoot;
        [Tooltip("Only materials matching this reference will be flashed. If null, all renderer materials will be flashed.")]
        [SerializeField] Material m_TargetMaterial;
        [ColorUsage(true, true)]
        [SerializeField] Color m_BlinkOnEmissionColor = Color.white;
        [ColorUsage(true, true)]
        [SerializeField] Color m_BlinkOffEmissionColor = Color.black;
        [SerializeField] float m_StartBlinksPerSecond = 4f;
        [SerializeField] float m_EndBlinksPerSecond = 18f;
        [SerializeField] SfxKey m_ExplosionAlertSfxKey = SfxKey.ExplosionAlert;
        [SerializeField] [Min(0.01f)] float m_AlertStartInterval = 0.28f;
        [SerializeField] [Min(0.01f)] float m_AlertEndInterval = 0.06f;

        [Header("Explosion (Damage + Force)")]
        [SerializeField] LayerMask m_AffectedLayers = ~0;
        [SerializeField] QueryTriggerInteraction m_TriggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] float m_ExplosionRadius = 6f;
        [SerializeField] float m_ExplosionDamage = 40f;
        [SerializeField] float m_ExplosionForce = 18f;
        [SerializeField] float m_UpwardsModifier = 0.1f;
        [SerializeField] int m_OverlapBufferSize = 64;

        [Header("Explosion Spawn Point (optional)")]
        [SerializeField] Transform m_ExplosionCenter;
        [SerializeField] SfxKey m_ExplosionSfxKey = SfxKey.ExplosionSmall;

        [Header("Post Explosion")]
        [Tooltip("Optional smoke VFX spawned after explosion. It follows debris lifetime and is cleared on next segment.")]
        [SerializeField] private GameObject m_PostExplosionSmokeVfx;

        [Header("Camera Feedback")]
        [Tooltip("相机震动强度，0 表示不震动")]
        [SerializeField] [Min(0f)] float m_CameraShakeIntensity = 0.16f;
        [Tooltip("相机震动持续时间")]
        [SerializeField] [Min(0.01f)] float m_CameraShakeDuration = 0.25f;
        [Tooltip("震动包络形态（Normal=钟形，BigToSmall=由强到弱）")]
        [SerializeField] ShakeEnvelopeProfile m_CameraShakeProfile = ShakeEnvelopeProfile.BigToSmall;
        [Tooltip("仅当玩家与爆炸中心距离小于此值时触发相机震动（0 表示不限制距离）")]
        [SerializeField] [Min(0f)] float m_CameraShakeMaxDistance = 20f;

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

        Health m_Health;
        EnemyController m_Controller;
        NavMeshAgent m_NavMeshAgent;

        readonly List<RendererIndexData> m_TargetRenderers = new List<RendererIndexData>(32);
        MaterialPropertyBlock m_Mpb;

        Collider[] m_Overlap;
        readonly List<Health> m_UniqueHealths = new List<Health>(32);
        readonly List<Rigidbody> m_UniqueBodies = new List<Rigidbody>(32);

        bool m_Started;
        float m_StartTime;
        float m_ExplodeAt;
        float m_NextAlertSfxTime;

        enum Phase : byte
        {
            None = 0,
            PreBlink = 1,
            Shrink = 2,
            Grow = 3,
        }

        Phase m_Phase;
        float m_PhaseStartTime;
        Vector3 m_InitialLocalScale;

        void Awake()
        {
            m_Health = GetComponent<Health>();
            m_Controller = GetComponent<EnemyController>();
            m_NavMeshAgent = GetComponent<NavMeshAgent>();
            m_Mpb = new MaterialPropertyBlock();
            m_InitialLocalScale = transform.localScale;

            EnsureOverlapBuffer();
            CacheRenderers();
            ApplyEmission(m_BlinkOffEmissionColor);
        }

        void OnDisable()
        {
            if (m_Started)
            {
                ApplyEmission(m_BlinkOffEmissionColor);
                m_Started = false;
            }

            m_Phase = Phase.None;
            transform.localScale = m_InitialLocalScale;
        }

        public void Begin()
        {
            if (m_Started)
            {
                return;
            }

            m_Started = true;
            m_StartTime = Time.time;
            m_ExplodeAt = m_StartTime + Mathf.Max(0f, m_PreExplosionDuration);
            m_Phase = Phase.PreBlink;
            m_PhaseStartTime = m_StartTime;
            m_InitialLocalScale = transform.localScale;
            m_NextAlertSfxTime = m_StartTime;

            if (m_Controller != null)
            {
                m_Controller.UnregisterEnemyFromManager();
                SpawnLootIfAny(m_Controller);
            }

            if (m_NavMeshAgent != null)
            {
                if (m_NavMeshAgent.enabled && m_NavMeshAgent.isOnNavMesh)
                {
                    m_NavMeshAgent.isStopped = true;
                }
                m_NavMeshAgent.enabled = false;
            }

            enabled = true;
        }

        void Update()
        {
            if (!m_Started)
            {
                return;
            }

            float now = Time.time;
            float totalAlertDuration = Mathf.Max(0.0001f, m_PreExplosionDuration + Mathf.Max(0f, m_ShrinkDuration) + Mathf.Max(0f, m_GrowDuration));
            float alertProgress = Mathf.Clamp01((now - m_StartTime) / totalAlertDuration);
            TryPlayAlertSfx(now, alertProgress);

            if (m_Phase == Phase.PreBlink)
            {
                if (now >= m_ExplodeAt)
                {
                    if (m_EnableScalePulse && (m_ShrinkDuration > 0f || m_GrowDuration > 0f))
                    {
                        StartShrink(now);
                    }
                    else
                    {
                        SpawnGibsThenExplode();
                    }
                    return;
                }

                float duration = Mathf.Max(0.0001f, m_PreExplosionDuration);
                float t = Mathf.Clamp01((now - m_StartTime) / duration);
                float blinksPerSecond = Mathf.Lerp(m_StartBlinksPerSecond, m_EndBlinksPerSecond, t);
                bool on = Mathf.Repeat((now - m_StartTime) * Mathf.Max(0.01f, blinksPerSecond), 1f) < 0.5f;
                ApplyEmission(on ? m_BlinkOnEmissionColor : m_BlinkOffEmissionColor);
                return;
            }

            if (m_Phase == Phase.Shrink)
            {
                float duration = Mathf.Max(0.0001f, m_ShrinkDuration);
                float t = Mathf.Clamp01((now - m_PhaseStartTime) / duration);
                t = t * t * (3f - 2f * t);
                transform.localScale = Vector3.LerpUnclamped(m_InitialLocalScale, m_InitialLocalScale * Mathf.Max(0.01f, m_ShrinkMultiplier), t);
                if (t >= 1f)
                {
                    StartGrow(now);
                }
                return;
            }

            if (m_Phase == Phase.Grow)
            {
                float duration = Mathf.Max(0.0001f, m_GrowDuration);
                float t = Mathf.Clamp01((now - m_PhaseStartTime) / duration);
                t = t * t * (3f - 2f * t);
                Vector3 from = m_InitialLocalScale * Mathf.Max(0.01f, m_ShrinkMultiplier);
                Vector3 to = m_InitialLocalScale * Mathf.Max(0.01f, m_GrowMultiplier);
                transform.localScale = Vector3.LerpUnclamped(from, to, t);
                if (t >= 1f)
                {
                    SpawnGibsThenExplode();
                }
                return;
            }
        }

        void StartShrink(float now)
        {
            if (m_ShrinkDuration <= 0f)
            {
                StartGrow(now);
                return;
            }

            m_Phase = Phase.Shrink;
            m_PhaseStartTime = now;
            ApplyEmission(m_BlinkOffEmissionColor);
        }

        void StartGrow(float now)
        {
            if (m_GrowDuration <= 0f)
            {
                SpawnGibsThenExplode();
                return;
            }

            m_Phase = Phase.Grow;
            m_PhaseStartTime = now;
            ApplyEmission(m_BlinkOffEmissionColor);
        }

        void SpawnGibsThenExplode()
        {
            if (m_Controller != null)
            {
                m_Controller.SpawnDeathGibs();
            }

            Explode();
        }

        void Explode()
        {
            m_Started = false;
            m_Phase = Phase.None;
            ApplyEmission(m_BlinkOffEmissionColor);
            if (m_ExplosionSfxKey != SfxKey.None)
            {
                AudioUtility.PlaySfx(m_ExplosionSfxKey, transform.position);
            }

            Vector3 center = m_ExplosionCenter != null ? m_ExplosionCenter.position : transform.position;
            TriggerCameraShake(center);

            int hitCount = Physics.OverlapSphereNonAlloc(center, Mathf.Max(0f, m_ExplosionRadius), m_Overlap,
                m_AffectedLayers, m_TriggerInteraction);

            m_UniqueHealths.Clear();
            m_UniqueBodies.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = m_Overlap[i];
                if (col == null)
                {
                    continue;
                }

                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic && !rb.transform.IsChildOf(transform))
                {
                    if (!Contains(m_UniqueBodies, rb))
                    {
                        m_UniqueBodies.Add(rb);
                    }
                }

                Damageable damageable = col.GetComponent<Damageable>();
                if (damageable == null)
                {
                    continue;
                }

                Health targetHealth = damageable.GetComponentInParent<Health>();
                if (targetHealth == null || targetHealth == m_Health)
                {
                    continue;
                }

                if (!Contains(m_UniqueHealths, targetHealth))
                {
                    m_UniqueHealths.Add(targetHealth);
                }
            }

            for (int i = 0; i < m_UniqueBodies.Count; i++)
            {
                Rigidbody rb = m_UniqueBodies[i];
                if (rb != null)
                {
                    rb.AddExplosionForce(m_ExplosionForce, center, m_ExplosionRadius, m_UpwardsModifier,
                        ForceMode.Impulse);
                }
            }

            if (m_Controller != null)
            {
                for (int i = 0; i < m_UniqueHealths.Count; i++)
                {
                    Health h = m_UniqueHealths[i];
                    if (h == null)
                    {
                        continue;
                    }

                    Damageable d = h.GetComponent<Damageable>();
                    if (d != null)
                    {
                        d.InflictDamage(m_ExplosionDamage, true, m_Controller.gameObject);
                    }

                    EnemyController targetEnemy = h.GetComponent<EnemyController>();
                    if (targetEnemy != null && targetEnemy != m_Controller)
                    {
                        targetEnemy.TryApplyKnockbackFromPosition(center);
                    }
                }

                SpawnExplosionVfx(m_Controller, center);
                SpawnPersistentSmokeVfx(m_Controller, center, m_PostExplosionSmokeVfx);
            }

            DespawnOrDestroySelf();
        }

        void TriggerCameraShake(Vector3 explosionCenter)
        {
            if (m_CameraShakeIntensity <= 0f || m_CameraShakeDuration <= 0.01f)
            {
                return;
            }

            var actorsManager = FindObjectOfType<ActorsManager>();
            var player = actorsManager != null ? actorsManager.Player : null;
            if (player == null)
            {
                return;
            }

            if (m_CameraShakeMaxDistance > 0.001f)
            {
                float sqrDist = (player.transform.position - explosionCenter).sqrMagnitude;
                if (sqrDist > m_CameraShakeMaxDistance * m_CameraShakeMaxDistance)
                {
                    return;
                }
            }

            var cameraRig = player.GetComponent<FpsCameraRig>();
            if (cameraRig == null || cameraRig.CameraEffects == null)
            {
                return;
            }

            cameraRig.CameraEffects.PlayEnvelopeShake(this, m_CameraShakeIntensity, m_CameraShakeDuration, m_CameraShakeProfile);
        }

        void DespawnOrDestroySelf()
        {
            var pooled = GetComponent<PooledInstance>();
            if (pooled != null)
            {
                pooled.Despawn();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void EnsureOverlapBuffer()
        {
            int size = Mathf.Clamp(m_OverlapBufferSize, 8, 512);
            if (m_Overlap == null || m_Overlap.Length != size)
            {
                m_Overlap = new Collider[size];
            }
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

        void ApplyEmission(Color color)
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

        static bool Contains<T>(List<T> list, T value) where T : class
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], value))
                {
                    return true;
                }
            }
            return false;
        }

        static void SpawnLootIfAny(EnemyController controller)
        {
            if (controller == null || !controller.TryDropItem() || controller.LootPrefab == null)
            {
                return;
            }

            if (ObjPrefabManager.Instance != null)
            {
                ObjPrefabManager.Instance.Spawn(controller.LootPrefab.transform, controller.transform.position,
                    Quaternion.identity);
            }
            else
            {
                Object.Instantiate(controller.LootPrefab, controller.transform.position, Quaternion.identity);
            }
        }

        static void SpawnExplosionVfx(EnemyController controller, Vector3 center)
        {
            if (controller == null || controller.DeathVfx == null)
            {
                return;
            }

            Vector3 pos = controller.DeathVfxSpawnPoint != null ? controller.DeathVfxSpawnPoint.position : center;

            GameObject vfx;
            if (ObjPrefabManager.Instance != null)
            {
                vfx = ObjPrefabManager.Instance.Spawn(controller.DeathVfx.transform, pos, Quaternion.identity).gameObject;
            }
            else
            {
                vfx = Object.Instantiate(controller.DeathVfx, pos, Quaternion.identity);
            }

            TimedSelfDestruct tsd = vfx.GetComponent<TimedSelfDestruct>();
            if (tsd != null)
            {
                tsd.ResetLifetime(5f);
            }
            else
            {
                Object.Destroy(vfx, 5f);
            }

            AudioSource[] audioSources = vfx.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
            {
                AudioSource source = audioSources[i];
                if (source == null)
                {
                    continue;
                }

                source.Stop();
                source.enabled = false;
            }
        }

        static void SpawnPersistentSmokeVfx(EnemyController controller, Vector3 center, GameObject smokePrefab)
        {
            if (controller == null || smokePrefab == null)
            {
                return;
            }

            Vector3 pos = controller.DeathVfxSpawnPoint != null ? controller.DeathVfxSpawnPoint.position : center;
            Transform parent = DebrisRoot.ResolveParentFor(controller.transform);

            if (ObjPrefabManager.Instance != null)
            {
                ObjPrefabManager.Instance.Spawn(smokePrefab.transform, pos, Quaternion.identity, parent);
                return;
            }

            Object.Instantiate(smokePrefab, pos, Quaternion.identity, parent);
        }

        void TryPlayAlertSfx(float now, float normalizedProgress)
        {
            if (m_ExplosionAlertSfxKey == SfxKey.None || now < m_NextAlertSfxTime)
            {
                return;
            }

            AudioUtility.PlaySfx(m_ExplosionAlertSfxKey, transform.position);
            float interval = Mathf.Lerp(m_AlertStartInterval, m_AlertEndInterval, Mathf.Clamp01(normalizedProgress));
            m_NextAlertSfxTime = now + Mathf.Max(0.01f, interval);
        }
    }
}

