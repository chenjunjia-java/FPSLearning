using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Roguelike.Stats
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class RoguelikeEnemyStats : MonoBehaviour
    {
        [Header("Base Stats")]
        [SerializeField] [Min(0f)] private float m_BaseMaxHealth = 0f;
        [Tooltip("若 > 0，则使用此值作为基础攻击力（最终伤害由此值经词条乘算得出）；若 = 0，则使用下方「基础攻击倍率」乘以子弹上的 Damage。")]
        [SerializeField] [Min(0f)] private float m_BaseAttackDamage = 5f;
        [Tooltip("仅当「基础攻击力」= 0 时生效：对子弹 Damage 的倍率，1 表示不缩放。")]
        [SerializeField] [Min(0f)] private float m_BaseAttackMultiplier = 1f;
        [Tooltip("攻击速度倍率，1 表示使用武器原有默认射速，>1 射得更快，<1 射得更慢。")]
        [SerializeField] [Min(0.1f)] private float m_BaseAttackSpeedMultiplier = 1f;
        [SerializeField] [Min(0.1f)] private float m_BaseMoveSpeedMultiplier = 1f;

        private readonly List<Modifier> m_Modifiers = new List<Modifier>(16);
        private StatCache m_StatCache;
        private Health m_Health;
        private bool m_Initialized;

        public float MaxHealthFinal { get; private set; }
        /// <summary> 当配置了基础攻击力(>0)时，为最终攻击力；否则为 0，伤害由子弹 Damage * AttackMultiplierFinal 计算。 </summary>
        public float AttackPowerFinal { get; private set; }
        public float AttackMultiplierFinal { get; private set; }
        /// <summary> 攻击速度倍率，用于武器射速：有效间隔 = DelayBetweenShots / AttackSpeedMultiplierFinal。 </summary>
        public float AttackSpeedMultiplierFinal { get; private set; }
        public float MoveSpeedMultiplierFinal { get; private set; }

        private void Awake()
        {
            m_Health = GetComponent<Health>();
            if (m_BaseMaxHealth <= 0f && m_Health != null)
            {
                m_BaseMaxHealth = Mathf.Max(1f, m_Health.MaxHealth);
            }

            m_StatCache = new StatCache();
            RebuildStats();
            m_Initialized = true;
        }

        private void OnEnable()
        {
            if (m_Initialized)
            {
                RebuildStats();
            }
        }

        public void AddModifier(Modifier modifier)
        {
            m_Modifiers.Add(modifier);
            RebuildStats();
        }

        public int RemoveModifiersBySource(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId) || m_Modifiers.Count == 0)
            {
                return 0;
            }

            int removed = 0;
            for (int i = m_Modifiers.Count - 1; i >= 0; i--)
            {
                if (m_Modifiers[i].SourceId == sourceId)
                {
                    m_Modifiers.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                RebuildStats();
            }

            return removed;
        }

        public void ClearModifiers()
        {
            if (m_Modifiers.Count == 0)
            {
                return;
            }

            m_Modifiers.Clear();
            RebuildStats();
        }

        public void RebuildStats()
        {
            bool isFirstBuild = !m_Initialized;
            float previousMaxHealth = MaxHealthFinal > 0f ? MaxHealthFinal : m_BaseMaxHealth;

            m_StatCache.SetBaseValue(StatId.Enemy_MaxHealth, m_BaseMaxHealth);
            float attackBase = m_BaseAttackDamage > 0f ? m_BaseAttackDamage : m_BaseAttackMultiplier;
            m_StatCache.SetBaseValue(StatId.Enemy_Attack, attackBase);
            m_StatCache.SetBaseValue(StatId.Enemy_AttackSpeed, m_BaseAttackSpeedMultiplier);
            m_StatCache.SetBaseValue(StatId.Enemy_MoveSpeed, m_BaseMoveSpeedMultiplier);
            m_StatCache.Rebuild(m_Modifiers);

            MaxHealthFinal = Mathf.Max(1f, m_StatCache.GetFinalValue(StatId.Enemy_MaxHealth));
            float attackFinal = Mathf.Max(0f, m_StatCache.GetFinalValue(StatId.Enemy_Attack));
            if (m_BaseAttackDamage > 0f)
            {
                AttackPowerFinal = attackFinal;
                AttackMultiplierFinal = 1f;
            }
            else
            {
                AttackPowerFinal = 0f;
                AttackMultiplierFinal = attackFinal;
            }
            AttackSpeedMultiplierFinal = Mathf.Max(0.1f, m_StatCache.GetFinalValue(StatId.Enemy_AttackSpeed));
            MoveSpeedMultiplierFinal = Mathf.Max(0.1f, m_StatCache.GetFinalValue(StatId.Enemy_MoveSpeed));

            ApplyMaxHealthRuntime(previousMaxHealth, isFirstBuild);
        }

        private void ApplyMaxHealthRuntime(float previousMaxHealth, bool isFirstBuild)
        {
            if (m_Health == null)
            {
                return;
            }

            if (isFirstBuild || m_Health.CurrentHealth <= 0f)
            {
                m_Health.MaxHealth = MaxHealthFinal;
                m_Health.CurrentHealth = MaxHealthFinal;
                return;
            }

            float previousRatio = 1f;
            if (previousMaxHealth > 0f)
            {
                previousRatio = Mathf.Clamp01(m_Health.CurrentHealth / previousMaxHealth);
            }

            m_Health.MaxHealth = MaxHealthFinal;
            m_Health.CurrentHealth = Mathf.Clamp(MaxHealthFinal * previousRatio, 0f, MaxHealthFinal);
        }
    }
}
