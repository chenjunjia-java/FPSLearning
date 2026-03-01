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

            float d = Mathf.Max(0f, difficulty);
            int s = Mathf.Max(0, stageIndex);
            int w = Mathf.Max(0, waveIndex);

            float healthMul = 1f + d * 0.25f + s * 0.05f;
            float attackMul = 1f + d * 0.15f + w * 0.05f;
            float moveMul = 1f + d * 0.05f;

            enemyStats.AddModifier(new Modifier(StatId.Enemy_MaxHealth, ModifierKind.Mul, healthMul, sourceId));
            enemyStats.AddModifier(new Modifier(StatId.Enemy_Attack, ModifierKind.Mul, attackMul, sourceId));
            enemyStats.AddModifier(new Modifier(StatId.Enemy_MoveSpeed, ModifierKind.Mul, moveMul, sourceId));
        }
    }
}

