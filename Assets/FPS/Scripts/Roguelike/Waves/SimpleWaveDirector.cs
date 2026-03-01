using UnityEngine;

namespace Unity.FPS.Roguelike.Waves
{
    public static class SimpleWaveDirector
    {
        public static WavePlan BuildPlan(float difficulty, int stageIndex, int minWaves, int maxWaves,
            int baseEnemiesPerWave, float enemiesPerWavePerDifficulty, float spawnInterval)
        {
            // 波数：difficulty 每跨过 1.0 才额外 +1 波，避免 0.5 这类值被 RoundToInt 误增波数。
            // 若希望固定波数，把 min/max 设为相同即可。
            float d = Mathf.Max(0f, difficulty);
            int waveCount = (minWaves == maxWaves)
                ? Mathf.Max(0, minWaves)
                : Mathf.Clamp(minWaves + Mathf.FloorToInt(d), minWaves, maxWaves);
            if (waveCount <= 0)
            {
                return new WavePlan { Waves = new Wave[0] };
            }

            var waves = new Wave[waveCount];
            int stageBonus = Mathf.Max(0, stageIndex);

            for (int i = 0; i < waveCount; i++)
            {
                float waveRamp = waveCount > 1 ? (float)i / (waveCount - 1) : 0f;
                int count = baseEnemiesPerWave
                            + Mathf.RoundToInt(d * enemiesPerWavePerDifficulty)
                            + stageBonus
                            + Mathf.RoundToInt(waveRamp * 2f);
                count = Mathf.Max(0, count);
                waves[i] = new Wave
                {
                    Count = count,
                    SpawnInterval = Mathf.Max(0f, spawnInterval),
                };
            }

            return new WavePlan { Waves = waves };
        }
    }
}

