using System;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Roguelike.Stats
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class RoguelikePlayerStats : MonoBehaviour
    {
        [Header("Base Stats")]
        [SerializeField] [Min(0f)] private float m_BaseMaxHealth = 0f;
        [SerializeField] [Min(0f)] private float m_BaseAttackMultiplier = 1f;
        [SerializeField] [Range(0f, 1f)] private float m_BaseCritChance = 0f;
        [SerializeField] [Min(1f)] private float m_BaseCritDamageMultiplier = 2f;
        [SerializeField] [Min(0.1f)] private float m_BaseMoveSpeedMultiplier = 1f;
        [SerializeField] [Min(0.1f)] private float m_BaseDamageTakenMultiplier = 1f;

        private readonly List<Modifier> m_Modifiers = new List<Modifier>(32);
        private StatCache m_StatCache;
        private Health m_Health;
        private Damageable m_Damageable;
        private bool m_Initialized;

        public event Action OnStatsChanged;

        public float MaxHealthFinal { get; private set; }
        public float AttackMultiplierFinal { get; private set; }
        /// <summary> 玩家 flat 加伤（与倍率分开，在子弹上先加再乘倍率）。 </summary>
        public float AttackFlatAddFinal { get; private set; }
        public float CritChanceFinal { get; private set; }
        public float CritDamageMultiplierFinal { get; private set; }
        public float MoveSpeedMultiplierFinal { get; private set; }
        public float DamageTakenMultiplierFinal { get; private set; }

        public IReadOnlyList<Modifier> Modifiers => m_Modifiers;

        private void Awake()
        {
            m_Health = GetComponent<Health>();
            m_Damageable = GetComponent<Damageable>();
            m_StatCache = new StatCache();
            InitializeBaseHealthIfNeeded();
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

        public void AddModifiers(IReadOnlyList<Modifier> modifiers)
        {
            if (modifiers == null || modifiers.Count == 0)
            {
                return;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                m_Modifiers.Add(modifiers[i]);
            }

            RebuildStats();
        }

        public int RemoveModifiersBySource(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId) || m_Modifiers.Count == 0)
            {
                return 0;
            }

            int removedCount = 0;
            for (int i = m_Modifiers.Count - 1; i >= 0; i--)
            {
                if (m_Modifiers[i].SourceId == sourceId)
                {
                    m_Modifiers.RemoveAt(i);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                RebuildStats();
            }

            return removedCount;
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

            m_StatCache.SetBaseValue(StatId.Player_MaxHealth, m_BaseMaxHealth);
            m_StatCache.SetBaseValue(StatId.Player_Attack, m_BaseAttackMultiplier);
            m_StatCache.SetBaseValue(StatId.Player_AttackFlatAdd, 0f);
            m_StatCache.SetBaseValue(StatId.Player_CritChance, m_BaseCritChance);
            m_StatCache.SetBaseValue(StatId.Player_CritDamage, m_BaseCritDamageMultiplier);
            m_StatCache.SetBaseValue(StatId.Player_MoveSpeed, m_BaseMoveSpeedMultiplier);
            m_StatCache.SetBaseValue(StatId.Player_DamageTakenMultiplier, m_BaseDamageTakenMultiplier);
            m_StatCache.Rebuild(m_Modifiers);

            MaxHealthFinal = Mathf.Max(1f, m_StatCache.GetFinalValue(StatId.Player_MaxHealth));
            AttackMultiplierFinal = Mathf.Max(0f, m_StatCache.GetFinalValue(StatId.Player_Attack));
            AttackFlatAddFinal = Mathf.Max(0f, m_StatCache.GetFinalValue(StatId.Player_AttackFlatAdd));
            CritChanceFinal = Mathf.Clamp01(m_StatCache.GetFinalValue(StatId.Player_CritChance));
            CritDamageMultiplierFinal = Mathf.Max(1f, m_StatCache.GetFinalValue(StatId.Player_CritDamage));
            MoveSpeedMultiplierFinal = Mathf.Max(0.1f, m_StatCache.GetFinalValue(StatId.Player_MoveSpeed));
            DamageTakenMultiplierFinal = Mathf.Max(0.1f, m_StatCache.GetFinalValue(StatId.Player_DamageTakenMultiplier));

            ApplyMaxHealthRuntime(previousMaxHealth, isFirstBuild);
            ApplyDamageTakenRuntime();
            OnStatsChanged?.Invoke();
        }

        private void InitializeBaseHealthIfNeeded()
        {
            if (m_Health == null)
            {
                return;
            }

            if (m_BaseMaxHealth <= 0f)
            {
                m_BaseMaxHealth = Mathf.Max(1f, m_Health.MaxHealth);
            }
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

        private void ApplyDamageTakenRuntime()
        {
            if (m_Damageable == null)
            {
                return;
            }

            m_Damageable.DamageMultiplier = DamageTakenMultiplierFinal;
        }
    }
}
