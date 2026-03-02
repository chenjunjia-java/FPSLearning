using Unity.FPS.Game;
using Unity.FPS.Roguelike.Audio;
using Unity.FPS.Roguelike.Cards;
using Unity.FPS.Roguelike.Objectives;
using UnityEngine;

namespace Unity.FPS.Roguelike.Level
{
    /// <summary>
    /// 关卡流程编排：
    /// 1) 踩入口机关 -> 打开入口门；
    /// 2) 玩家进入入口门 -> 关入口门并开始刷怪；
    /// 3) 当前段清怪 -> 仅打开当前段出口门（下一段入口门必须由机关开启）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SegmentGateWaveBinder : MonoBehaviour
    {
        [SerializeField] private LevelSegment m_Segment;
        [SerializeField] private RoguelikeLevelGenerator m_LevelGenerator;
        [SerializeField] private RoguelikeCardManager m_CardManager;
        [SerializeField] private EntranceDoorPressurePlate m_EntranceDoorPlate;
        [SerializeField] private RoguelikeAudioDriver m_AudioDriver;

        private bool m_HasStartedWaves;
        private RoguelikeStageObjective m_RuntimeObjective;

        private void Awake()
        {
            if (m_Segment == null)
            {
                m_Segment = GetComponent<LevelSegment>();
            }

            if (m_LevelGenerator == null)
            {
                m_LevelGenerator = FindObjectOfType<RoguelikeLevelGenerator>();
            }

            if (m_CardManager == null)
            {
                m_CardManager = FindObjectOfType<RoguelikeCardManager>();
            }

            if (m_EntranceDoorPlate == null)
            {
                m_EntranceDoorPlate = GetComponentInChildren<EntranceDoorPressurePlate>(true);
            }

            if (m_AudioDriver == null)
            {
                m_AudioDriver = GetComponent<RoguelikeAudioDriver>();
            }
        }

        private void OnEnable()
        {
            if (m_Segment == null)
            {
                return;
            }

            var entranceDoor = m_Segment.EntranceDoorGate;
            var waveController = m_Segment.WaveController;

            if (entranceDoor != null)
            {
                entranceDoor.OnPlayerEnterGate -= HandlePlayerEnteredEntranceGate;
                entranceDoor.OnPlayerEnterGate += HandlePlayerEnteredEntranceGate;
            }

            if (waveController != null)
            {
                waveController.OnAllWavesCleared -= HandleAllWavesCleared;
                waveController.OnAllWavesCleared += HandleAllWavesCleared;
                waveController.OnWaveStarted -= HandleWaveStarted;
                waveController.OnWaveStarted += HandleWaveStarted;
            }

            if (m_EntranceDoorPlate != null)
            {
                m_EntranceDoorPlate.OnPressed -= HandleEntrancePlatePressed;
                m_EntranceDoorPlate.OnPressed += HandleEntrancePlatePressed;
            }
        }

        private void OnDisable()
        {
            if (m_Segment == null)
            {
                return;
            }

            var entranceDoor = m_Segment.EntranceDoorGate;
            var waveController = m_Segment.WaveController;

            if (entranceDoor != null)
            {
                entranceDoor.OnPlayerEnterGate -= HandlePlayerEnteredEntranceGate;
            }

            if (waveController != null)
            {
                waveController.OnAllWavesCleared -= HandleAllWavesCleared;
                waveController.OnWaveStarted -= HandleWaveStarted;
            }

            if (m_EntranceDoorPlate != null)
            {
                m_EntranceDoorPlate.OnPressed -= HandleEntrancePlatePressed;
            }
        }

        private void HandleEntrancePlatePressed(EntranceDoorPressurePlate plate)
        {
            if (m_Segment == null)
            {
                return;
            }

            var entranceDoor = m_Segment.EntranceDoorGate;
            if (entranceDoor != null)
            {
                entranceDoor.Open();
            }
        }

        /// <summary>
        /// 玩家第一次踏入当前段入口门：关门并开始刷怪。
        /// </summary>
        private void HandlePlayerEnteredEntranceGate(SegmentDoorGate door)
        {
            if (m_Segment == null)
            {
                return;
            }

            var entranceDoor = m_Segment.EntranceDoorGate;
            var waveController = m_Segment.WaveController;

            if (m_HasStartedWaves || entranceDoor == null || door != entranceDoor)
            {
                return;
            }

            entranceDoor.Close();

            if (waveController != null)
            {
                ResolveRunDifficulty(out float difficulty, out int stageIndex);
                waveController.ConfigureDifficulty(difficulty, stageIndex);
                EnsureStageObjective();
                waveController.StartWaves();
            }

            if (m_AudioDriver != null)
            {
                m_AudioDriver.HandleSegmentEnterGateTriggered();
            }

            m_HasStartedWaves = true;
        }

        private void HandleAllWavesCleared()
        {
            if (m_Segment == null)
            {
                return;
            }

            if (m_Segment.IsBossSegment)
            {
                if (m_RuntimeObjective != null)
                {
                    m_RuntimeObjective.CompleteAsBossVictory();
                }

                DisplayMessageEvent displayMessage = Events.DisplayMessageEvent;
                displayMessage.Message = "Final boss defeated! Run complete!";
                displayMessage.DelayBeforeDisplay = 0f;
                EventManager.Broadcast(displayMessage);

                GameOverEvent gameOverEvent = Events.GameOverEvent;
                gameOverEvent.Win = true;
                EventManager.Broadcast(gameOverEvent);
                return;
            }

            if (m_RuntimeObjective != null)
            {
                m_RuntimeObjective.CompleteAsStageCleared();
            }

            if (m_AudioDriver != null)
            {
                m_AudioDriver.HandleSegmentCleared();
            }

            if (m_CardManager != null && m_CardManager.RequestRewardForSegment(m_Segment, OpenDoorsForProgression))
            {
                return;
            }

            OpenDoorsForProgression();
        }

        private void OpenDoorsForProgression()
        {
            var exitDoor = m_Segment.ExitDoorGate;
            if (exitDoor != null)
            {
                exitDoor.Open();
            }
        }

        private void HandleWaveStarted(int currentWaveNumber, int totalWaveCount)
        {
            EnsureStageObjective();
            if (m_RuntimeObjective != null)
            {
                m_RuntimeObjective.SetWaveProgress(currentWaveNumber, totalWaveCount,
                    m_Segment != null && m_Segment.IsBossSegment);
            }
        }

        private void EnsureStageObjective()
        {
            if (m_RuntimeObjective != null || m_Segment == null)
            {
                return;
            }

            m_RuntimeObjective = gameObject.AddComponent<RoguelikeStageObjective>();
            m_RuntimeObjective.Initialize(m_Segment.IsBossSegment, ResolveStageNumber());
        }

        private int ResolveStageNumber()
        {
            if (m_LevelGenerator == null || m_LevelGenerator.SpawnedSegments == null)
            {
                return 1;
            }

            var segments = m_LevelGenerator.SpawnedSegments;
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] == m_Segment)
                {
                    return i + 1;
                }
            }

            return 1;
        }

        private void ResolveRunDifficulty(out float difficulty, out int stageIndex)
        {
            difficulty = 0f;
            stageIndex = 0;

            if (m_LevelGenerator == null)
            {
                m_LevelGenerator = FindObjectOfType<RoguelikeLevelGenerator>();
            }

            if (m_LevelGenerator == null)
            {
                return;
            }

            stageIndex = m_LevelGenerator.GetSegmentIndex(m_Segment);
            if (stageIndex < 0)
            {
                stageIndex = Mathf.Max(0, m_LevelGenerator.CurrentSegmentIndex);
            }

            // 约定：每通过一个关卡 difficulty +1；默认与段索引同步。
            difficulty = Mathf.Max(0, m_LevelGenerator.CurrentDifficulty);
        }
    }
}
