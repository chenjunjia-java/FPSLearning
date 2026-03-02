using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.GameFramework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.FPS.Roguelike.Level
{
    public interface IEnemySpawnedHandler
    {
        void OnEnemySpawned(EnemyController enemy);
    }

    public class SegmentEnemySpawner : MonoBehaviour
    {
        [Header("Enemy")]
        [SerializeField] private List<EnemyController> m_EnemyPrefabs = new List<EnemyController>();
        [Tooltip("当敌人列表同时包含爆炸怪与非爆炸怪时，爆炸怪被选中的目标概率。")]
        [SerializeField] [Range(0f, 1f)] private float m_ExploderSpawnChance = 0.2f;
        [SerializeField] [Min(1)] private int m_PoolMaxSize = 64;
        [SerializeField] [Min(0)] private int m_PrewarmCount = 0;

        [Header("Boss")]
        [SerializeField] private EnemyController m_BossPrefab;
        [Tooltip("Boss 的专用刷新点。为空时回退使用普通 Spawn Points。")]
        [SerializeField] private Transform[] m_BossSpawnPoints;
        [Tooltip("Boss 使用专用刷新点数组；若数组为空则自动回退到普通 Spawn Points。")]
        [SerializeField] private bool m_BossUseDedicatedSpawnPoints = true;
        [SerializeField] [Min(0f)] private float m_BossSpawnJitterRadius = 0f;
        [Tooltip("开启后，同一个 SegmentEnemySpawner 生命周期内只会生成一次 Boss。")]
        [SerializeField] private bool m_BossSpawnOnce = true;

        [Header("Spawn Tween (Jelly)")]
        [SerializeField] private bool m_EnableSpawnTween = true;
        [SerializeField] [Min(0.01f)] private float m_SpawnTweenDuration = 0.35f;
        [SerializeField] [Range(0.01f, 1f)] private float m_SpawnTweenStartScale = 0.2f;
        [SerializeField] [Min(0f)] private float m_JellyAmplitude = 0.25f;
        [SerializeField] [Min(0f)] private float m_JellyFrequency = 2.5f;
        [SerializeField] private Vector3 m_JellyNonUniform = new Vector3(0.12f, -0.08f, 0.12f);

        [Header("Spawn Points")]
        [SerializeField] private Transform[] m_SpawnPoints;
        [Tooltip("启用后，仅在离主角最近的 3 个 Spawn Point 中随机生成。")]
        [SerializeField] private bool m_UsePlayerAttachedSpawnPointsOnly = true;
        [SerializeField] [Min(0f)] private float m_SpawnJitterRadius = 1.5f;
        [SerializeField] private float m_SpawnHeightOffset = 0.5f;
        [SerializeField] [Min(1)] private int m_MaxTriesPerEnemy = 6;

        [Header("Placement Validation")]
        [SerializeField] [Min(0f)] private float m_NavMeshSampleRadius = 2.0f;
        [SerializeField] [Min(0f)] private float m_OverlapCheckRadius = 0.75f;
        [Tooltip("检测生成点是否与碰撞体重叠时使用的层级。默认 Everything：与任意实心碰撞体重叠则换点。注意：若包含地面层，且检测形状中心在地面表面，可能会因与地面碰撞体相交而导致永远判定为重叠。")]
        [SerializeField] private LayerMask m_OverlapMask = ~0;

        private bool m_HasWarnedMissingPool;
        private float m_PlacementCapsuleRadius;
        private float m_PlacementCapsuleHeight;
        private bool m_HasResolvedPlacementCapsule;
        private ActorsManager m_ActorsManager;
        private readonly List<Transform> m_ClosestSpawnPointsBuffer = new List<Transform>(16);
        private readonly Transform[] m_ClosestSpawnPointCandidates = new Transform[3];
        private readonly float[] m_ClosestSpawnPointDistances = new float[3];

        private bool m_HasSpawnedBoss;

        public int Spawn(int count)
        {
            return Spawn(count, null);
        }

        public int Spawn(int count, IEnemySpawnedHandler handler)
        {
            if (count <= 0)
            {
                return 0;
            }

            CacheSpawnPointsIfNeeded();
            Transform[] spawnPoints = ResolveRegularSpawnPoints();
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return 0;
            }

            PreparePool();
            if (!HasAnyEnemyPrefab())
            {
                return 0;
            }

            int spawnedCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (!TryResolveSpawnPose(spawnPoints, m_SpawnJitterRadius, out Vector3 spawnPosition,
                        out Quaternion spawnRotation))
                {
                    continue;
                }

                if (!TryGetRandomEnemyPrefab(out EnemyController prefab))
                {
                    // 列表存在但没有可用 Prefab（例如全是 null）
                    break;
                }

                SpawnSingle(prefab, spawnPosition, spawnRotation, handler);
                spawnedCount++;
            }

            return spawnedCount;
        }

        public EnemyController SpawnBoss()
        {
            return SpawnBoss(null);
        }

        public EnemyController SpawnBoss(IEnemySpawnedHandler handler)
        {
            if (m_BossPrefab == null)
            {
                return null;
            }

            if (m_BossSpawnOnce && m_HasSpawnedBoss)
            {
                return null;
            }

            CacheSpawnPointsIfNeeded();
            Transform[] points = ResolveBossSpawnPoints();
            if (points == null || points.Length == 0)
            {
                return null;
            }

            PreparePool();
            if (!TryResolveSpawnPose(points, m_BossSpawnJitterRadius, out Vector3 spawnPosition,
                    out Quaternion spawnRotation))
            {
                return null;
            }

            m_HasSpawnedBoss = true;

            EnemyController enemy = SpawnSingleAndReturn(m_BossPrefab, spawnPosition, spawnRotation, handler);
            if (enemy == null && m_BossSpawnOnce)
            {
                // 生成失败（池/Instantiate 返回 null）时允许重试
                m_HasSpawnedBoss = false;
            }

            return enemy;
        }

        public void ResetRuntimeState()
        {
            m_HasSpawnedBoss = false;
        }

        private void Awake()
        {
            CacheSpawnPointsIfNeeded();
            PreparePool();
            ResolvePlacementCapsuleIfNeeded();
        }

        private void OnValidate()
        {
            CacheSpawnPointsIfNeeded();
            m_HasResolvedPlacementCapsule = false;
        }

        private void PreparePool()
        {
            if (ObjPrefabManager.Instance == null || m_EnemyPrefabs == null || m_EnemyPrefabs.Count == 0)
            {
                // 仍可能需要 Boss 的池
                if (ObjPrefabManager.Instance == null || m_BossPrefab == null)
                {
                    return;
                }

                ObjPrefabManager.Instance.Load(m_BossPrefab, m_PrewarmCount, m_PoolMaxSize);
                return;
            }

            for (int i = 0; i < m_EnemyPrefabs.Count; i++)
            {
                var prefab = m_EnemyPrefabs[i];
                if (prefab == null)
                {
                    continue;
                }

                ObjPrefabManager.Instance.Load(prefab, m_PrewarmCount, m_PoolMaxSize);
            }

            if (m_BossPrefab != null)
            {
                ObjPrefabManager.Instance.Load(m_BossPrefab, m_PrewarmCount, m_PoolMaxSize);
            }
        }

        private void SpawnSingle(EnemyController prefab, Vector3 position, Quaternion rotation, IEnemySpawnedHandler handler)
        {
            if (prefab == null)
            {
                return;
            }

            EnemyController enemy;
            if (ObjPrefabManager.Instance != null)
            {
                enemy = ObjPrefabManager.Instance.Spawn(prefab, position, rotation, null, m_PoolMaxSize);
            }
            else
            {
                if (!m_HasWarnedMissingPool)
                {
                    m_HasWarnedMissingPool = true;
                    Debug.LogWarning("ObjPrefabManager not found. SegmentEnemySpawner falls back to Instantiate.", this);
                }

                enemy = Instantiate(prefab, position, rotation);
            }

            if (enemy != null)
            {
                enemy.SetRandomColors();
                PlaySpawnJellyTween(enemy);
                handler?.OnEnemySpawned(enemy);
            }
        }

        private EnemyController SpawnSingleAndReturn(EnemyController prefab, Vector3 position, Quaternion rotation,
            IEnemySpawnedHandler handler)
        {
            if (prefab == null)
            {
                return null;
            }

            EnemyController enemy;
            if (ObjPrefabManager.Instance != null)
            {
                enemy = ObjPrefabManager.Instance.Spawn(prefab, position, rotation, null, m_PoolMaxSize);
            }
            else
            {
                if (!m_HasWarnedMissingPool)
                {
                    m_HasWarnedMissingPool = true;
                    Debug.LogWarning("ObjPrefabManager not found. SegmentEnemySpawner falls back to Instantiate.", this);
                }

                enemy = Instantiate(prefab, position, rotation);
            }

            if (enemy != null)
            {
                enemy.SetRandomColors();
                PlaySpawnJellyTween(enemy);
                handler?.OnEnemySpawned(enemy);
            }

            return enemy;
        }

        private void PlaySpawnJellyTween(EnemyController enemy)
        {
            if (!m_EnableSpawnTween || enemy == null)
            {
                return;
            }

            // 优先取 Animator 所在的 Transform（通常是视觉层级），避免缩放影响 NavMeshAgent/碰撞体。
            Transform tweenTarget = null;
            var anim = enemy.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                tweenTarget = anim.transform;
            }
            else
            {
                var r = enemy.GetComponentInChildren<Renderer>();
                tweenTarget = r != null ? r.transform : enemy.transform;
            }

            var runner = enemy.GetComponent<JellySpawnTweenRunner>();
            if (runner == null)
            {
                runner = enemy.gameObject.AddComponent<JellySpawnTweenRunner>();
            }

            runner.Play(tweenTarget, new JellySpawnTweenRunner.Settings
            {
                Duration = m_SpawnTweenDuration,
                StartScale = m_SpawnTweenStartScale,
                JellyAmplitude = m_JellyAmplitude,
                JellyFrequency = m_JellyFrequency,
                JellyNonUniform = m_JellyNonUniform,
            });
        }

        private bool TryResolveSpawnPose(Transform[] spawnPoints, float jitterRadius, out Vector3 spawnPosition,
            out Quaternion spawnRotation)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                spawnPosition = Vector3.zero;
                spawnRotation = Quaternion.identity;
                return false;
            }

            int attempts = Mathf.Max(1, m_MaxTriesPerEnemy);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
                if (spawnPoint == null)
                {
                    continue;
                }

                Vector3 candidate = spawnPoint.position;
                if (jitterRadius > 0f)
                {
                    Vector2 jitter = Random.insideUnitCircle * jitterRadius;
                    candidate.x += jitter.x;
                    candidate.z += jitter.y;
                }

                if (m_NavMeshSampleRadius > 0f)
                {
                    if (!TrySampleOnNavMesh(candidate, out candidate))
                    {
                        continue;
                    }
                }

                if (m_SpawnHeightOffset != 0f)
                {
                    candidate.y += m_SpawnHeightOffset;
                }

                if (IsOverlapping(candidate))
                {
                    continue;
                }

                spawnPosition = candidate;
                spawnRotation = spawnPoint.rotation;
                return true;
            }

            spawnPosition = Vector3.zero;
            spawnRotation = Quaternion.identity;
            return false;
        }

        private bool TrySampleOnNavMesh(Vector3 candidate, out Vector3 sampledPosition)
        {
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, m_NavMeshSampleRadius, NavMesh.AllAreas))
            {
                sampledPosition = hit.position;
                return true;
            }

            sampledPosition = default;
            return false;
        }

        private bool IsOverlapping(Vector3 candidate)
        {
            if (m_OverlapCheckRadius <= 0f)
            {
                return false;
            }

            ResolvePlacementCapsuleIfNeeded();

            // 用“脚底略抬起”的胶囊体避免与地面必然相交导致永远重叠。
            float radius = m_PlacementCapsuleRadius > 0f ? m_PlacementCapsuleRadius : m_OverlapCheckRadius;
            float height = Mathf.Max(radius * 2f, m_PlacementCapsuleHeight);

            const float kGroundEpsilon = 0.02f;
            Vector3 bottom = candidate + Vector3.up * (radius + kGroundEpsilon);
            float segment = Mathf.Max(0f, height - 2f * radius);
            Vector3 top = bottom + Vector3.up * segment;

            return Physics.CheckCapsule(bottom, top, radius, m_OverlapMask, QueryTriggerInteraction.Ignore);
        }

        private void ResolvePlacementCapsuleIfNeeded()
        {
            if (m_HasResolvedPlacementCapsule)
            {
                return;
            }

            m_HasResolvedPlacementCapsule = true;

            // 默认用 Inspector 的 Overlap 半径做兜底（如果取不到敌人碰撞体/Agent 尺寸）。
            m_PlacementCapsuleRadius = Mathf.Max(0f, m_OverlapCheckRadius);
            m_PlacementCapsuleHeight = Mathf.Max(0f, m_OverlapCheckRadius * 2f);

            if (m_EnemyPrefabs == null || m_EnemyPrefabs.Count == 0)
            {
                AccumulatePlacementCapsuleFromPrefab(m_BossPrefab);
                return;
            }

            // 多种敌人时用“最大体积”做保守检测：避免大体型敌人刷出来后与环境/其他敌人相交。
            for (int i = 0; i < m_EnemyPrefabs.Count; i++)
            {
                var prefab = m_EnemyPrefabs[i];
                if (prefab == null)
                {
                    continue;
                }

                AccumulatePlacementCapsuleFromPrefab(prefab);
            }

            AccumulatePlacementCapsuleFromPrefab(m_BossPrefab);
        }

        private void AccumulatePlacementCapsuleFromPrefab(EnemyController prefab)
        {
            if (prefab == null)
            {
                return;
            }

            // 优先取真实碰撞体/Agent 参数，让检测更贴合敌人体积。
            if (prefab.TryGetComponent<CharacterController>(out var cc) && cc != null)
            {
                m_PlacementCapsuleRadius = Mathf.Max(m_PlacementCapsuleRadius, cc.radius);
                m_PlacementCapsuleHeight = Mathf.Max(m_PlacementCapsuleHeight, cc.height);
                return;
            }

            if (prefab.TryGetComponent<CapsuleCollider>(out var capsule) && capsule != null)
            {
                m_PlacementCapsuleRadius = Mathf.Max(m_PlacementCapsuleRadius, capsule.radius);
                m_PlacementCapsuleHeight = Mathf.Max(m_PlacementCapsuleHeight, capsule.height);
                return;
            }

            if (prefab.TryGetComponent<NavMeshAgent>(out var agent) && agent != null)
            {
                m_PlacementCapsuleRadius = Mathf.Max(m_PlacementCapsuleRadius, agent.radius);
                m_PlacementCapsuleHeight = Mathf.Max(m_PlacementCapsuleHeight, agent.height);
            }
        }

        private void CacheSpawnPointsIfNeeded()
        {
            if (m_SpawnPoints != null && m_SpawnPoints.Length > 0)
            {
                return;
            }

            m_SpawnPoints = new[] { transform };
        }

        private Transform[] ResolveBossSpawnPoints()
        {
            if (m_BossUseDedicatedSpawnPoints && m_BossSpawnPoints != null && m_BossSpawnPoints.Length > 0)
            {
                return m_BossSpawnPoints;
            }

            return m_SpawnPoints;
        }

        private Transform[] ResolveRegularSpawnPoints()
        {
            if (!m_UsePlayerAttachedSpawnPointsOnly)
            {
                return m_SpawnPoints;
            }

            if (m_SpawnPoints == null || m_SpawnPoints.Length == 0)
            {
                return null;
            }

            if (m_ActorsManager == null)
            {
                m_ActorsManager = FindObjectOfType<ActorsManager>();
            }

            Transform playerTransform = m_ActorsManager != null && m_ActorsManager.Player != null
                ? m_ActorsManager.Player.transform
                : null;
            if (playerTransform == null)
            {
                return m_SpawnPoints;
            }

            m_ClosestSpawnPointsBuffer.Clear();
            for (int i = 0; i < m_ClosestSpawnPointCandidates.Length; i++)
            {
                m_ClosestSpawnPointCandidates[i] = null;
                m_ClosestSpawnPointDistances[i] = Mathf.Infinity;
            }

            for (int i = 0; i < m_SpawnPoints.Length; i++)
            {
                Transform spawnPoint = m_SpawnPoints[i];
                if (spawnPoint == null)
                {
                    continue;
                }

                float sqrDistance = (spawnPoint.position - playerTransform.position).sqrMagnitude;
                for (int slot = 0; slot < m_ClosestSpawnPointCandidates.Length; slot++)
                {
                    if (sqrDistance >= m_ClosestSpawnPointDistances[slot])
                    {
                        continue;
                    }

                    for (int shift = m_ClosestSpawnPointCandidates.Length - 1; shift > slot; shift--)
                    {
                        m_ClosestSpawnPointCandidates[shift] = m_ClosestSpawnPointCandidates[shift - 1];
                        m_ClosestSpawnPointDistances[shift] = m_ClosestSpawnPointDistances[shift - 1];
                    }

                    m_ClosestSpawnPointCandidates[slot] = spawnPoint;
                    m_ClosestSpawnPointDistances[slot] = sqrDistance;
                    break;
                }
            }

            for (int i = 0; i < m_ClosestSpawnPointCandidates.Length; i++)
            {
                Transform spawnPoint = m_ClosestSpawnPointCandidates[i];
                if (spawnPoint != null)
                {
                    m_ClosestSpawnPointsBuffer.Add(spawnPoint);
                }
            }

            return m_ClosestSpawnPointsBuffer.Count > 0
                ? m_ClosestSpawnPointsBuffer.ToArray()
                : null;
        }

        private bool HasAnyEnemyPrefab()
        {
            if (m_EnemyPrefabs == null || m_EnemyPrefabs.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < m_EnemyPrefabs.Count; i++)
            {
                if (m_EnemyPrefabs[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetRandomEnemyPrefab(out EnemyController prefab)
        {
            prefab = null;
            if (m_EnemyPrefabs == null || m_EnemyPrefabs.Count == 0)
            {
                return false;
            }

            int exploderCount = 0;
            int nonExploderCount = 0;
            for (int i = 0; i < m_EnemyPrefabs.Count; i++)
            {
                var candidate = m_EnemyPrefabs[i];
                if (candidate != null)
                {
                    if (IsExploderPrefab(candidate))
                    {
                        exploderCount++;
                    }
                    else
                    {
                        nonExploderCount++;
                    }
                }
            }

            if (exploderCount == 0 && nonExploderCount == 0)
            {
                return false;
            }

            bool pickExploder;
            if (exploderCount == 0)
            {
                pickExploder = false;
            }
            else if (nonExploderCount == 0)
            {
                pickExploder = true;
            }
            else
            {
                pickExploder = Random.value < m_ExploderSpawnChance;
            }

            int targetIndexInBucket = Random.Range(0, pickExploder ? exploderCount : nonExploderCount);
            int currentIndexInBucket = 0;
            for (int i = 0; i < m_EnemyPrefabs.Count; i++)
            {
                var candidate = m_EnemyPrefabs[i];
                if (candidate == null)
                {
                    continue;
                }

                bool isExploder = IsExploderPrefab(candidate);
                if (isExploder != pickExploder)
                {
                    continue;
                }

                if (currentIndexInBucket == targetIndexInBucket)
                {
                    prefab = candidate;
                    return true;
                }

                currentIndexInBucket++;
            }

            return false;
        }

        private static bool IsExploderPrefab(EnemyController prefab)
        {
            return prefab != null && prefab.GetComponent<ExploderDeathSequence>() != null;
        }
    }
}
