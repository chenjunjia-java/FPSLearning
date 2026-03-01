using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Roguelike.Level
{
    public class SegmentObstacleGenerator : MonoBehaviour
    {
        [SerializeField] private List<GameObject> m_ObstaclePrefabs = new List<GameObject>();
        [SerializeField] private ObstacleSlot[] m_Slots;
        [SerializeField] private bool m_AllowEmpty = true;
        [SerializeField] [Range(0f, 1f)] private float m_EmptyChance = 0.25f;

        [Header("Obstacle Randomization")]
        [SerializeField] [Range(0.5f, 1.5f)] private float m_ScaleMin = 0.7f;
        [SerializeField] [Range(0.5f, 1.5f)] private float m_ScaleMax = 1.1f;
        [SerializeField] [Min(0f)] private float m_HorizontalOffsetMax = 0.8f;
        [SerializeField] private bool m_RandomRotationY = true;

        [Header("Physics (for explosion)")]
        [SerializeField] private bool m_AddRigidbody = true;
        [SerializeField] private bool m_RigidbodyKinematic = true;
        [SerializeField] private bool m_AddMeshCollider = true;

        private readonly List<GameObject> m_SpawnedObstacles = new List<GameObject>(16);

        public void EnsureRuntimeSlots()
        {
            CacheSlotsIfNeeded();
            if (m_Slots != null && m_Slots.Length > 0)
            {
                return;
            }

            var runtimeSlots = new ObstacleSlot[4];
            for (int i = 0; i < runtimeSlots.Length; i++)
            {
                var slotGo = new GameObject("ObstacleSlot_" + i);
                slotGo.transform.SetParent(transform, false);

                // Spread generated fallback slots around segment center.
                float x = ((i % 2 == 0) ? -1f : 1f) * 3f;
                float z = (i < 2 ? -1f : 1f) * 3f;
                slotGo.transform.localPosition = new Vector3(x, 0f, z);

                runtimeSlots[i] = slotGo.AddComponent<ObstacleSlot>();
            }

            m_Slots = runtimeSlots;
        }

        public void GenerateObstacles()
        {
            GenerateObstacles(-1);
        }

        public void GenerateObstacles(int seed)
        {
            CacheSlotsIfNeeded();
            ClearSpawnedObstacles();

            if (m_Slots == null || m_Slots.Length == 0 || m_ObstaclePrefabs.Count == 0)
            {
                return;
            }

            Random.State previousState = default;
            var hasSeed = seed >= 0;
            if (hasSeed)
            {
                previousState = Random.state;
                Random.InitState(seed);
            }

            for (int i = 0; i < m_Slots.Length; i++)
            {
                var slot = m_Slots[i];
                if (slot == null)
                {
                    continue;
                }

                if (m_AllowEmpty && Random.value <= m_EmptyChance)
                {
                    continue;
                }

                int prefabIndex = Random.Range(0, m_ObstaclePrefabs.Count);
                var obstaclePrefab = m_ObstaclePrefabs[prefabIndex];
                if (obstaclePrefab == null)
                {
                    continue;
                }

                var slotT = slot.transform;
                Vector3 pos = slotT.position;
                Quaternion rot = m_RandomRotationY
                    ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                    : slotT.rotation;

                if (m_HorizontalOffsetMax > 0f)
                {
                    float ox = Random.Range(-m_HorizontalOffsetMax, m_HorizontalOffsetMax);
                    float oz = Random.Range(-m_HorizontalOffsetMax, m_HorizontalOffsetMax);
                    pos.x += ox;
                    pos.z += oz;
                }

                var spawned = Instantiate(obstaclePrefab, pos, rot, slotT);

                float scale = Random.Range(m_ScaleMin, m_ScaleMax);
                spawned.transform.localScale = Vector3.one * scale;

                AlignObstacleToFloor(spawned, slotT.position.y);
                EnsurePhysicsComponents(spawned);

                m_SpawnedObstacles.Add(spawned);
            }

            if (hasSeed)
            {
                Random.state = previousState;
            }
        }

        public void ClearSpawnedObstacles()
        {
            for (int i = 0; i < m_SpawnedObstacles.Count; i++)
            {
                if (m_SpawnedObstacles[i] != null)
                {
                    Destroy(m_SpawnedObstacles[i]);
                }
            }

            m_SpawnedObstacles.Clear();
        }

        private static void AlignObstacleToFloor(GameObject obstacle, float floorY)
        {
            var renderer = obstacle.GetComponentInChildren<MeshRenderer>();
            if (renderer == null)
            {
                return;
            }

            Bounds bounds = renderer.bounds;
            float deltaY = floorY - bounds.min.y;
            var t = obstacle.transform;
            t.position = new Vector3(t.position.x, t.position.y + deltaY, t.position.z);
        }

        private void EnsurePhysicsComponents(GameObject obstacle)
        {
            if (m_AddRigidbody && obstacle.GetComponent<Rigidbody>() == null)
            {
                var rb = obstacle.AddComponent<Rigidbody>();
                rb.isKinematic = m_RigidbodyKinematic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            if (m_AddMeshCollider)
            {
                var meshFilter = obstacle.GetComponentInChildren<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    var meshGo = meshFilter.gameObject;
                    if (meshGo.GetComponent<MeshCollider>() == null)
                    {
                        var col = meshGo.AddComponent<MeshCollider>();
                        col.sharedMesh = meshFilter.sharedMesh;
                        col.convex = true;
                    }
                }
            }
        }

        private void CacheSlotsIfNeeded()
        {
            if (m_Slots == null || m_Slots.Length == 0)
            {
                m_Slots = GetComponentsInChildren<ObstacleSlot>(true);
            }
        }
    }
}
