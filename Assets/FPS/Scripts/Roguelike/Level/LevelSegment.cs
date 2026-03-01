using UnityEngine;
using Unity.FPS.Roguelike.Waves;
using Unity.FPS.GameFramework;

namespace Unity.FPS.Roguelike.Level
{
    public class LevelSegment : MonoBehaviour
    {
        [SerializeField] private bool m_IsFixedStartSegment;
        [SerializeField] private bool m_IsBossSegment;
        [SerializeField] private Transform m_EnterPoint;
        [SerializeField] private Transform m_ExitPoint;
        [SerializeField] private SegmentObstacleGenerator m_ObstacleGenerator;
        [SerializeField] private SegmentEnemySpawner m_EnemySpawner;
        [SerializeField] private StageWaveController m_WaveController;
        [SerializeField] private SegmentGateWaveBinder m_GateWaveBinder;
        [SerializeField] private SegmentDoorGate m_EntranceDoorGate;
        [SerializeField] private SegmentDoorGate m_ExitDoorGate;

        public bool IsFixedStartSegment => m_IsFixedStartSegment;
        public bool IsBossSegment => m_IsBossSegment;
        public Transform EnterPoint => m_EnterPoint;
        public Transform ExitPoint => m_ExitPoint;
        public SegmentObstacleGenerator ObstacleGenerator => m_ObstacleGenerator;
        public SegmentEnemySpawner EnemySpawner => m_EnemySpawner;
        public StageWaveController WaveController => m_WaveController;
        public SegmentGateWaveBinder GateWaveBinder => m_GateWaveBinder;
        public SegmentDoorGate EntranceDoorGate => m_EntranceDoorGate;
        public SegmentDoorGate ExitDoorGate => m_ExitDoorGate;

        public void EnsureRuntimeSetup()
        {
            EnsureObstacleGenerator();
            EnsureEnemySpawner();
            EnsureWaveController();
            EnsureGateWaveBinder();
            EnsureDebrisRoot();
        }

        public void SetRuntimeRole(bool isStartSegment, bool isBossSegment)
        {
            m_IsFixedStartSegment = isStartSegment;
            m_IsBossSegment = isBossSegment;
        }

        private void Awake()
        {
            EnsureRuntimeSetup();
        }

        private void OnValidate()
        {
            EnsureRuntimeSetup();
        }

        private void EnsureObstacleGenerator()
        {
            if (m_ObstacleGenerator == null)
            {
                m_ObstacleGenerator = GetComponentInChildren<SegmentObstacleGenerator>(true);
            }

            if (m_ObstacleGenerator != null)
            {
                m_ObstacleGenerator.EnsureRuntimeSlots();
                return;
            }

            m_ObstacleGenerator = gameObject.AddComponent<SegmentObstacleGenerator>();
            m_ObstacleGenerator.EnsureRuntimeSlots();
        }

        private void EnsureEnemySpawner()
        {
            if (m_EnemySpawner != null)
            {
                return;
            }

            m_EnemySpawner = GetComponentInChildren<SegmentEnemySpawner>(true);
            if (m_EnemySpawner != null)
            {
                return;
            }

            m_EnemySpawner = gameObject.AddComponent<SegmentEnemySpawner>();
        }

        private void EnsureWaveController()
        {
            if (m_WaveController != null)
            {
                return;
            }

            m_WaveController = GetComponentInChildren<StageWaveController>(true);
            if (m_WaveController != null)
            {
                return;
            }

            m_WaveController = gameObject.AddComponent<StageWaveController>();
        }

        private void EnsureGateWaveBinder()
        {
            if (m_GateWaveBinder != null)
            {
                return;
            }

            m_GateWaveBinder = GetComponentInChildren<SegmentGateWaveBinder>(true);
            if (m_GateWaveBinder != null)
            {
                return;
            }

            m_GateWaveBinder = gameObject.AddComponent<SegmentGateWaveBinder>();
        }

        private void EnsureDebrisRoot()
        {
            var debrisRoot = GetComponent<DebrisRoot>();
            if (debrisRoot == null)
            {
                debrisRoot = gameObject.AddComponent<DebrisRoot>();
            }

            _ = debrisRoot.Root;
        }
    }
}
