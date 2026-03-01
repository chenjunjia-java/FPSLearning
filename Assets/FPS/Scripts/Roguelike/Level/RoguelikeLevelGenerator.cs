using System.Collections.Generic;
using UnityEngine;
using NavMeshSurface = Unity.AI.Navigation.NavMeshSurface;
using Unity.FPS.GameFramework;

namespace Unity.FPS.Roguelike.Level
{
    public class RoguelikeLevelGenerator : MonoBehaviour
    {
        [Header("Segments")]
        [SerializeField] private LevelSegment m_FixedFirstSegmentInScene;
        [SerializeField] private List<GameObject> m_NormalSegmentPrefabs = new List<GameObject>();
        [SerializeField] private GameObject m_BossSegmentPrefab;
        [SerializeField] [Min(1)] private int m_NormalSegmentCount = 3;
        [SerializeField] private bool m_AllowRepeat = true;
        [SerializeField] [Min(1)] private int m_PreloadAhead = 1;

        [Header("Runtime")]
        [SerializeField] private Transform m_ChainRoot;
        [SerializeField] private NavMeshSurface m_NavMeshSurface;
        [SerializeField] private bool m_AutoStartOnEnable = true;
        [SerializeField] private bool m_RebuildNavMeshOnAppend = true;
        [SerializeField] private bool m_LogNavMeshBuildCost = true;
        [Tooltip("排除这些层上的物体，不参与 NavMesh 收集（如 UI、TextMeshPro），可避免 invalid vertex data 警告。")]
        [SerializeField] private LayerMask m_ExcludeLayersFromNavMesh;

        private readonly List<GameObject> m_RuntimeSequence = new List<GameObject>(16);
        private readonly List<LevelSegment> m_SpawnedSegments = new List<LevelSegment>(16);
        private int m_NextSpawnIndex;
        private int m_CurrentSegmentIndex;
        private bool m_RunStarted;

        public IReadOnlyList<LevelSegment> SpawnedSegments => m_SpawnedSegments;

        /// <summary>
        /// 当前玩家所处的段索引（从 0 开始）。
        /// 约定：该索引也作为“全局难度”的基础输入（每通过一关，索引 +1）。
        /// </summary>
        public int CurrentSegmentIndex => m_CurrentSegmentIndex;

        /// <summary>
        /// 全局难度（从 0 开始），默认与 <see cref="CurrentSegmentIndex"/> 同步。
        /// </summary>
        public int CurrentDifficulty => m_CurrentSegmentIndex;

        public int GetSegmentIndex(LevelSegment segment)
        {
            if (segment == null)
            {
                return -1;
            }

            for (int i = 0; i < m_SpawnedSegments.Count; i++)
            {
                if (m_SpawnedSegments[i] == segment)
                {
                    return i;
                }
            }

            return -1;
        }
        public LevelSegment CurrentSegment
        {
            get
            {
                if (m_CurrentSegmentIndex < 0 || m_CurrentSegmentIndex >= m_SpawnedSegments.Count)
                {
                    return null;
                }

                return m_SpawnedSegments[m_CurrentSegmentIndex];
            }
        }

        public void Configure(LevelSegment fixedFirstSegment, List<GameObject> normalSegments, GameObject bossSegment,
            NavMeshSurface navMeshSurface)
        {
            m_FixedFirstSegmentInScene = fixedFirstSegment;
            m_NormalSegmentPrefabs.Clear();
            if (normalSegments != null)
            {
                for (int i = 0; i < normalSegments.Count; i++)
                {
                    var segmentPrefab = normalSegments[i];
                    if (segmentPrefab != null)
                    {
                        m_NormalSegmentPrefabs.Add(segmentPrefab);
                    }
                }
            }

            m_BossSegmentPrefab = bossSegment;
            m_NavMeshSurface = navMeshSurface;
        }

        private void OnEnable()
        {
            if (m_AutoStartOnEnable)
            {
                StartRun();
            }
        }

        public void StartRun()
        {
            if (m_RunStarted)
            {
                return;
            }

            if (m_NavMeshSurface == null)
            {
                m_NavMeshSurface = FindObjectOfType<NavMeshSurface>();
            }

            var firstSegment = ResolveFirstSegment();
            if (firstSegment == null)
            {
                Debug.LogError("RoguelikeLevelGenerator requires a fixed first segment in scene.");
                return;
            }

            m_RunStarted = true;
            m_CurrentSegmentIndex = 0;

            if (m_ChainRoot == null)
            {
                var root = new GameObject("RoguelikeLevelChain");
                m_ChainRoot = root.transform;
            }

            firstSegment.EnsureRuntimeSetup();
            firstSegment.SetRuntimeRole(true, false);
            m_SpawnedSegments.Clear();
            m_SpawnedSegments.Add(firstSegment);
            DebrisRoot.SetActive(firstSegment.GetComponent<DebrisRoot>());

            if (firstSegment.ObstacleGenerator != null)
            {
                firstSegment.ObstacleGenerator.GenerateObstacles();
            }

            // 首段入口门默认关闭，不再自动开门；由机关或流程脚本显式打开。
            RegisterDoorCallbacks(firstSegment, closeEntranceDoor: true);

            BuildRuntimeSequence();
            EnsurePreloadedAhead();
        }

        public bool AppendNextSegment()
        {
            if (m_NextSpawnIndex >= m_RuntimeSequence.Count)
            {
                return false;
            }

            var prefab = m_RuntimeSequence[m_NextSpawnIndex];
            if (prefab == null)
            {
                m_NextSpawnIndex++;
                return false;
            }

            var instance = Instantiate(prefab, m_ChainRoot);
            instance.name = "Segment_" + m_NextSpawnIndex;

            var nextSegment = instance.GetComponent<LevelSegment>();
            if (nextSegment == null)
            {
                nextSegment = instance.AddComponent<LevelSegment>();
            }

            nextSegment.EnsureRuntimeSetup();
            bool isBossSegment = m_NextSpawnIndex == m_RuntimeSequence.Count - 1;
            nextSegment.SetRuntimeRole(false, isBossSegment);

            if (m_SpawnedSegments.Count > 0)
            {
                var prevSegment = m_SpawnedSegments[m_SpawnedSegments.Count - 1];
                AlignSegmentToPrevious(prevSegment, nextSegment);
            }

            m_SpawnedSegments.Add(nextSegment);
            m_NextSpawnIndex++;

            var obstacleGenerator = nextSegment.ObstacleGenerator;
            if (obstacleGenerator != null)
            {
                obstacleGenerator.GenerateObstacles();
            }

            RegisterDoorCallbacks(nextSegment);

            if (m_RebuildNavMeshOnAppend)
            {
                ScheduleRebuildNavMesh();
            }

            return true;
        }

        public bool AdvanceToNextSegment()
        {
            int previousSegmentIndex = m_CurrentSegmentIndex;
            if (m_CurrentSegmentIndex + 1 >= m_SpawnedSegments.Count)
            {
                if (!AppendNextSegment())
                {
                    return false;
                }
            }

            m_CurrentSegmentIndex++;
            if (previousSegmentIndex >= 0 && previousSegmentIndex < m_SpawnedSegments.Count)
            {
                var prev = m_SpawnedSegments[previousSegmentIndex];
                var prevDebris = prev != null ? prev.GetComponent<DebrisRoot>() : null;
                if (prevDebris != null)
                {
                    prevDebris.ClearAll();
                }
            }
            DebrisRoot.SetActive(CurrentSegment != null ? CurrentSegment.GetComponent<DebrisRoot>() : null);
            EnsurePreloadedAhead();
            return true;
        }

        public void ScheduleRebuildNavMesh()
        {
            if (m_NavMeshSurface == null)
            {
                return;
            }

            LayerMask originalMask = m_NavMeshSurface.layerMask;
            if (m_ExcludeLayersFromNavMesh != 0)
            {
                m_NavMeshSurface.layerMask = originalMask & ~m_ExcludeLayersFromNavMesh;
            }

            float start = Time.realtimeSinceStartup;
            m_NavMeshSurface.BuildNavMesh();
            if (m_ExcludeLayersFromNavMesh != 0)
            {
                m_NavMeshSurface.layerMask = originalMask;
            }

            if (m_LogNavMeshBuildCost)
            {
                float elapsedMs = (Time.realtimeSinceStartup - start) * 1000f;
                Debug.Log($"[RoguelikeLevelGenerator] BuildNavMesh cost: {elapsedMs:F2} ms");
            }
        }

        private void EnsurePreloadedAhead()
        {
            int targetSpawnedCount = m_CurrentSegmentIndex + Mathf.Max(1, m_PreloadAhead) + 1;
            while (m_SpawnedSegments.Count < targetSpawnedCount)
            {
                if (!AppendNextSegment())
                {
                    break;
                }
            }
        }

        private void BuildRuntimeSequence()
        {
            m_RuntimeSequence.Clear();

            int totalCount = Mathf.Max(1, m_NormalSegmentCount);
            for (int i = 0; i < totalCount; i++)
            {
                var normalPrefab = PickNormalSegmentPrefab();
                if (normalPrefab != null)
                {
                    m_RuntimeSequence.Add(normalPrefab);
                }
            }

            if (m_BossSegmentPrefab != null)
            {
                m_RuntimeSequence.Add(m_BossSegmentPrefab);
            }
        }

        private GameObject PickNormalSegmentPrefab()
        {
            if (m_NormalSegmentPrefabs == null || m_NormalSegmentPrefabs.Count == 0)
            {
                return null;
            }

            if (m_AllowRepeat || m_RuntimeSequence.Count == 0)
            {
                return m_NormalSegmentPrefabs[Random.Range(0, m_NormalSegmentPrefabs.Count)];
            }

            int availableCount = m_NormalSegmentPrefabs.Count;
            for (int attempts = 0; attempts < availableCount; attempts++)
            {
                int index = Random.Range(0, availableCount);
                var candidate = m_NormalSegmentPrefabs[index];
                if (!m_RuntimeSequence.Contains(candidate))
                {
                    return candidate;
                }
            }

            return m_NormalSegmentPrefabs[Random.Range(0, m_NormalSegmentPrefabs.Count)];
        }

        private static void AlignSegmentToPrevious(LevelSegment previousSegment, LevelSegment nextSegment)
        {
            Transform prevExit = previousSegment.ExitPoint;
            Transform nextEnter = nextSegment.EnterPoint;

            if (prevExit == null || nextEnter == null)
            {
                Debug.LogWarning("Missing exit/enter connector when aligning segment.", nextSegment);
                return;
            }

            Transform nextRoot = nextSegment.transform;

            Quaternion deltaRot = prevExit.rotation * Quaternion.Inverse(nextEnter.rotation);
            nextRoot.rotation = deltaRot * nextRoot.rotation;

            Vector3 positionOffset = nextRoot.position - nextEnter.position;
            nextRoot.position = prevExit.position + positionOffset;
        }

        private void RegisterDoorCallbacks(LevelSegment segment, bool closeEntranceDoor = true)
        {
            var entranceDoorGate = segment.EntranceDoorGate;
            if (closeEntranceDoor && entranceDoorGate != null)
            {
                entranceDoorGate.Close();
            }

            var exitDoorGate = segment.ExitDoorGate;
            if (exitDoorGate != null)
            {
                exitDoorGate.OnPlayerEnterGate -= HandleExitGatePlayerEntered;
                exitDoorGate.OnPlayerEnterGate += HandleExitGatePlayerEntered;
                exitDoorGate.Close();
            }
        }

        private void HandleExitGatePlayerEntered(SegmentDoorGate enteredGate)
        {
            var current = CurrentSegment;
            if (current != null && current.ExitDoorGate == enteredGate && enteredGate != null && enteredGate.IsOpen)
            {
                AdvanceToNextSegment();
            }
        }

        private LevelSegment ResolveFirstSegment()
        {
            if (m_FixedFirstSegmentInScene != null)
            {
                return m_FixedFirstSegmentInScene;
            }

            m_FixedFirstSegmentInScene = FindObjectOfType<LevelSegment>();
            return m_FixedFirstSegmentInScene;
        }
    }
}
