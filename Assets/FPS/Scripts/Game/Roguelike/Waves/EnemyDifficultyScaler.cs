using Unity.FPS.Roguelike.Stats;
using UnityEngine;

namespace Unity.FPS.Roguelike.Waves
{
    public static class EnemyDifficultyScaler
    {
        public static void ApplyDifficulty(GameObject enemy, float difficulty, int stageIndex, int waveIndex)
        {
            if (enemy == null)
            {
                return;
            }

            if (!enemy.TryGetComponent<RoguelikeEnemyStats>(out var enemyStats))
            {
                enemyStats = enemy.AddComponent<RoguelikeEnemyStats>();
            }

            const string sourceId = "WaveDifficulty";
            enemyStats.RemoveModifiersBySource(sourceId);

            float dRaw = Mathf.Max(0f, difficulty);
            int dSteps = Mathf.Max(0, Mathf.FloorToInt(dRaw));
            int w = Mathf.Max(0, waveIndex);

            // 难度调缓：每提升 1 级难度，最大血量仅 +5（加法），攻击/移速倍率斜率也降低。
            float healthAdd = dSteps * 5f;
            float attackMul = 1f + dSteps * 0.05f + w * 0.02f;
            float moveMul = 1f + dSteps * 0.02f;

            enemyStats.AddModifier(new Modifier(StatId.Enemy_MaxHealth, ModifierKind.Add, healthAdd, sourceId));
            enemyStats.AddModifier(new Modifier(StatId.Enemy_Attack, ModifierKind.Mul, attackMul, sourceId));
            enemyStats.AddModifier(new Modifier(StatId.Enemy_MoveSpeed, ModifierKind.Mul, moveMul, sourceId));
        }
    }
}

