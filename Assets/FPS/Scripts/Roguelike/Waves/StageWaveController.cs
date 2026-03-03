using System;
using System.Collections;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Roguelike.Level;
using UnityEngine;

namespace Unity.FPS.Roguelike.Waves
{
    public sealed class StageWaveController : MonoBehaviour, IEnemySpawnedHandler
    {
        [Header("Dependencies")]
        [SerializeField] private LevelSegment m_Segment;
        [SerializeField] private SegmentEnemySpawner m_Spawner;

        [Header("Wave Plan (Minimal)")]
        [SerializeField] [Min(1)] private int m_MinWaves = 1;
        [SerializeField] [Min(1)] private int m_MaxWaves = 3;
        [SerializeField] [Min(0)] private int m_BaseEnemiesPerWave = 3;
        [SerializeField] [Min(0f)] private float m_EnemiesPerWavePerDifficulty = 1.0f;
        [SerializeField] [Min(0f)] private float m_SpawnInterval = 0.1f;

        [Header("Wave End Timing")]
        [Tooltip("当一波的最后一个敌人被击杀时，等待其对象销毁/失活（例如爆炸怪爆炸结束）后再进入下一波/结算。")]
        [SerializeField] private bool m_WaitForLastKilledEnemyToDespawn = true;
        [SerializeField] [Min(0f)] private float m_LastKilledEnemyCleanupTimeoutSeconds = 3f;
        [SerializeField] [Min(0f)] private float m_ExtraDelayAfterCleanupSeconds = 0f;

        [Header("Inter-wave Delay")]
        [Tooltip("一波结束到下一波开始之间的间隔时间（秒）。会延后 OnWaveStarted 触发和下一波刷怪开始。")]
        [SerializeField] [Min(0f)] private float m_InterWaveDelaySeconds = 3f;

        public event Action OnAllWavesCleared;
        public event Action<int, int> OnWaveStarted;
        public event Action OnBossSpawned;

        public int CurrentWaveNumber => m_CurrentWaveIndex + 1;
        public int TotalWaveCount => m_Plan.Waves != null ? m_Plan.Waves.Length : 0;

        private WavePlan m_Plan;
        private int m_CurrentWaveIndex = -1;
        private int m_CurrentWaveId = -1;
        private int m_AliveInCurrentWave;
        private int m_SpawnRemainingInWave;
        private float m_NextSpawnTime;
        private bool m_Running;
        private Coroutine m_AdvanceAfterKillRoutine;
        private bool m_IsBossPhase;
        
        private float m_Difficulty;
        private int m_StageIndex;
        private bool m_HasDifficultyConfig;

        private void Awake()
        {
            if (m_Segment == null)
            {
                m_Segment = GetComponentInParent<LevelSegment>();
            }

            if (m_Spawner == null && m_Segment != null)
            {
                m_Spawner = m_Segment.EnemySpawner;
            }

            if (m_Spawner == null)
            {
                m_Spawner = GetComponentInParent<SegmentEnemySpawner>();
            }
        }

        private void OnEnable()
        {
            EventManager.AddListener<EnemyKillEvent>(OnEnemyKilled);
        }

        private void OnDisable()
        {
            EventManager.RemoveListener<EnemyKillEvent>(OnEnemyKilled);

            if (m_AdvanceAfterKillRoutine != null)
            {
                StopCoroutine(m_AdvanceAfterKillRoutine);
                m_AdvanceAfterKillRoutine = null;
            }
        }

        public void ConfigureDifficulty(float difficulty, int stageIndex)
        {
            m_Difficulty = Mathf.Max(0f, difficulty);
            m_StageIndex = Mathf.Max(0, stageIndex);
            m_HasDifficultyConfig = true;
        }

        public void StartWaves()
        {
            if (m_Spawner == null)
            {
                Debug.LogError("StageWaveController requires a SegmentEnemySpawner.", this);
                return;
            }

            if (!m_HasDifficultyConfig)
            {
                Debug.LogError(
                    "StageWaveController must be configured via ConfigureDifficulty(difficulty, stageIndex) before StartWaves().",
                    this);
                return;
            }

            m_Plan = SimpleWaveDirector.BuildPlan(m_Difficulty, m_StageIndex, m_MinWaves, m_MaxWaves,
                m_BaseEnemiesPerWave, m_EnemiesPerWavePerDifficulty, m_SpawnInterval);

            m_Spawner.ResetRuntimeState();
            m_Running = true;
            m_CurrentWaveIndex = -1;
            m_CurrentWaveId = -1;
            m_AliveInCurrentWave = 0;
            m_SpawnRemainingInWave = 0;
            m_IsBossPhase = false;
            AdvanceToNextWave();
        }

        private void Update()
        {
            if (!m_Running || m_SpawnRemainingInWave <= 0)
            {
                return;
            }

            if (Time.time < m_NextSpawnTime)
            {
                return;
            }

            int spawned = m_Spawner.Spawn(1, this);
            if (spawned > 0)
            {
                m_SpawnRemainingInWave -= spawned;
                m_NextSpawnTime = Time.time + GetCurrentWave().SpawnInterval;
            }
            else
            {
                m_NextSpawnTime = Time.time + 0.25f;
            }
        }

        private Wave GetCurrentWave()
        {
            if (m_Plan.Waves == null || m_CurrentWaveIndex < 0 || m_CurrentWaveIndex >= m_Plan.Waves.Length)
            {
                return default;
            }

            return m_Plan.Waves[m_CurrentWaveIndex];
        }

        private void AdvanceToNextWave()
        {
            if (m_IsBossPhase)
            {
                m_IsBossPhase = false;
                m_Running = false;
                OnAllWavesCleared?.Invoke();
                return;
            }

            m_CurrentWaveIndex++;
            if (m_Plan.Waves == null || m_CurrentWaveIndex >= m_Plan.Waves.Length)
            {
                if (TryStartBossPhase())
                {
                    return;
                }

                m_Running = false;
                OnAllWavesCleared?.Invoke();
                return;
            }

            m_CurrentWaveId++;
            Wave wave = GetCurrentWave();
            m_AliveInCurrentWave = 0;
            m_SpawnRemainingInWave = Mathf.Max(0, wave.Count);
            m_NextSpawnTime = Time.time;
            OnWaveStarted?.Invoke(CurrentWaveNumber, TotalWaveCount);
        }

        private bool TryStartBossPhase()
        {
            if (m_Segment == null || !m_Segment.IsBossSegment || m_Spawner == null)
            {
                return false;
            }

            m_CurrentWaveId++;
            m_AliveInCurrentWave = 0;
            m_SpawnRemainingInWave = 0;

            EnemyController boss = m_Spawner.SpawnBoss(this);
            if (boss == null)
            {
                return false;
            }

            m_IsBossPhase = true;

            OnBossSpawned?.Invoke();

            // Boss 阶段作为额外“第 N+1 波”广播，方便目标/UI 复用现有波次事件。
            int bossWaveNumber = TotalWaveCount + 1;
            OnWaveStarted?.Invoke(bossWaveNumber, bossWaveNumber);
            return true;
        }

        public void OnEnemySpawned(EnemyController enemy)
        {
            if (enemy == null)
            {
                return;
            }

            if (!enemy.TryGetComponent<WaveEnemyTag>(out var tag))
            {
                tag = enemy.gameObject.AddComponent<WaveEnemyTag>();
            }

            tag.SetWaveId(m_CurrentWaveId);
            m_AliveInCurrentWave++;

            EnemyDifficultyScaler.ApplyDifficulty(enemy.gameObject, m_Difficulty, m_StageIndex, m_CurrentWaveIndex);
            enemy.ApplyMoveSpeedMultiplierFromStats();
        }

        private void OnEnemyKilled(EnemyKillEvent evt)
        {
            if (!m_Running || evt == null || evt.Enemy == null)
            {
                return;
            }

            if (!evt.Enemy.TryGetComponent<WaveEnemyTag>(out var tag))
            {
                return;
            }

            if (tag.WaveId != m_CurrentWaveId)
            {
                return;
            }

            m_AliveInCurrentWave = Mathf.Max(0, m_AliveInCurrentWave - 1);

            if (m_AliveInCurrentWave == 0 && m_SpawnRemainingInWave <= 0)
            {
                if (m_AdvanceAfterKillRoutine != null)
                {
                    StopCoroutine(m_AdvanceAfterKillRoutine);
                }

                GameObject lastKilledEnemy = evt.Enemy;
                int waveIdAtKill = m_CurrentWaveId;
                m_AdvanceAfterKillRoutine = StartCoroutine(AdvanceAfterWaveEnded(lastKilledEnemy, waveIdAtKill));
            }
        }

        private IEnumerator AdvanceAfterWaveEnded(GameObject lastKilledEnemy, int waveIdAtKill)
        {
            // 至少等一帧，让死亡逻辑（爆炸/破碎体/Disable/Destroy）有机会启动
            yield return null;

            if (m_WaitForLastKilledEnemyToDespawn)
            {
                float timeoutAt = Time.unscaledTime + Mathf.Max(0f, m_LastKilledEnemyCleanupTimeoutSeconds);
                while (lastKilledEnemy != null && lastKilledEnemy.activeInHierarchy && Time.unscaledTime < timeoutAt)
                {
                    yield return null;
                }
            }

            if (m_ExtraDelayAfterCleanupSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(m_ExtraDelayAfterCleanupSeconds);
            }

            if (m_InterWaveDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(m_InterWaveDelaySeconds);
            }

            m_AdvanceAfterKillRoutine = null;

            if (!m_Running || waveIdAtKill != m_CurrentWaveId)
            {
                yield break;
            }

            if (m_AliveInCurrentWave == 0 && m_SpawnRemainingInWave <= 0)
            {
                AdvanceToNextWave();
            }
        }
    }
}

