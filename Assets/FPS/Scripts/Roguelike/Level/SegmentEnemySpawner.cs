using Unity.FPS.AI;
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
        [SerializeField] [Min(1)] private int m_PoolMaxSize = 64;
        [SerializeField] [Min(0)] private int m_PrewarmCount = 0;

        [Header("Spawn Tween (Jelly)")]
        [SerializeField] private bool m_EnableSpawnTween = true;
        [SerializeField] [Min(0.01f)] private float m_SpawnTweenDuration = 0.35f;
        [SerializeField] [Range(0.01f, 1f)] private float m_SpawnTweenStartScale = 0.2f;
        [SerializeField] [Min(0f)] private float m_JellyAmplitude = 0.25f;
        [SerializeField] [Min(0f)] private float m_JellyFrequency = 2.5f;
        [SerializeField] private Vector3 m_JellyNonUniform = new Vector3(0.12f, -0.08f, 0.12f);

        [Header("Spawn Points")]
        [SerializeField] private Transform[] m_SpawnPoints;
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
            if (m_SpawnPoints == null || m_SpawnPoints.Length == 0)
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
                if (!TryResolveSpawnPose(out Vector3 spawnPosition, out Quaternion spawnRotation))
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

        private bool TryResolveSpawnPose(out Vector3 spawnPosition, out Quaternion spawnRotation)
        {
            int attempts = Mathf.Max(1, m_MaxTriesPerEnemy);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                Transform spawnPoint = m_SpawnPoints[Random.Range(0, m_SpawnPoints.Length)];
                if (spawnPoint == null)
                {
                    continue;
                }

                Vector3 candidate = spawnPoint.position;
                Vector2 jitter = Random.insideUnitCircle * m_SpawnJitterRadius;
                candidate.x += jitter.x;
                candidate.z += jitter.y;

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

                // 优先取真实碰撞体/Agent 参数，让检测更贴合敌人体积。
                if (prefab.TryGetComponent<CharacterController>(out var cc) && cc != null)
                {
                    m_PlacementCapsuleRadius = Mathf.Max(m_PlacementCapsuleRadius, cc.radius);
                    m_PlacementCapsuleHeight = Mathf.Max(m_PlacementCapsuleHeight, cc.height);
                    continue;
                }

                if (prefab.TryGetComponent<CapsuleCollider>(out var capsule) && capsule != null)
                {
                    m_PlacementCapsuleRadius = Mathf.Max(m_PlacementCapsuleRadius, capsule.radius);
                    m_PlacementCapsuleHeight = Mathf.Max(m_PlacementCapsuleHeight, capsule.height);
                    continue;
                }

                if (prefab.TryGetComponent<NavMeshAgent>(out var agent) && agent != null)
                {
                    m_PlacementCapsuleRadius = Mathf.Max(m_PlacementCapsuleRadius, agent.radius);
                    m_PlacementCapsuleHeight = Mathf.Max(m_PlacementCapsuleHeight, agent.height);
                }
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

            // 为避免在含 null 的列表里出现偏差，这里做有限次随机尝试；失败则回退线性查找第一个非空。
            int tries = Mathf.Min(m_EnemyPrefabs.Count, 8);
            for (int i = 0; i < tries; i++)
            {
                var candidate = m_EnemyPrefabs[Random.Range(0, m_EnemyPrefabs.Count)];
                if (candidate != null)
                {
                    prefab = candidate;
                    return true;
                }
            }

            for (int i = 0; i < m_EnemyPrefabs.Count; i++)
            {
                var candidate = m_EnemyPrefabs[i];
                if (candidate != null)
                {
                    prefab = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
