using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Roguelike.Objectives
{
    public sealed class RoguelikeStageObjective : Objective
    {
        private bool m_Initialized;

        protected override void Start()
        {
            if (!m_Initialized)
            {
                Title = "Clear Current Stage";
                Description = "Enter rooms and defeat enemies";
                m_Initialized = true;
            }

            base.Start();
            UpdateObjective(Description, string.Empty, string.Empty);
        }

        public void Initialize(bool isBossStage, int stageNumber)
        {
            Title = isBossStage ? "Final Stage" : string.Format("Stage {0}", stageNumber);
            Description = isBossStage ? "Defeat the final boss" : "Clear enemies and advance";
            IsOptional = false;
            DelayVisible = 0f;
            m_Initialized = true;

            if (isActiveAndEnabled)
            {
                UpdateObjective(Description, string.Empty, string.Empty);
            }
        }

        public void SetWaveProgress(int currentWaveNumber, int totalWaveCount, bool isBossStage)
        {
            if (IsCompleted)
            {
                return;
            }

            if (totalWaveCount <= 0)
            {
                UpdateObjective(string.Empty, string.Empty, string.Empty);
                return;
            }

            string prefix = isBossStage ? "Boss wave" : "Current wave";
            string counter = string.Format("{0} {1}/{2}", prefix, currentWaveNumber, totalWaveCount);
            UpdateObjective(string.Empty, counter, string.Empty);
        }

        public void CompleteAsBossVictory()
        {
            if (IsCompleted)
            {
                return;
            }

            CompleteObjective("Defeat the final boss", "Completed", "Final boss defeated!");
        }

        public void CompleteAsStageCleared()
        {
            if (IsCompleted)
            {
                return;
            }

            CompleteObjective("Stage cleared", "Completed", "Stage complete, proceed to the next area");
        }
    }
}
