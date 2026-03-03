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
            // 注意：本项目的 difficulty 已在关卡生成器侧做了“每 2 关 +1”的节流。
            // 这里不再额外按 stageIndex 叠加，避免同一维度被重复加成导致曲线过陡。
            int stageBonus = 0;

            for (int i = 0; i < waveCount; i++)
            {
                float waveRamp = waveCount > 1 ? (float)i / (waveCount - 1) : 0f;
                int count = baseEnemiesPerWave
                            + Mathf.FloorToInt(d * enemiesPerWavePerDifficulty * 0.5f)
                            + stageBonus
                            + Mathf.RoundToInt(waveRamp * 1f);
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

