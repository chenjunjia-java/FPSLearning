using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Roguelike.Stats
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponController))]
    public class RoguelikeWeaponStatsRuntime : MonoBehaviour
    {
        [Header("Base Overrides")]
        [SerializeField] [Min(0f)] private float m_BaseDamageMultiplier = 1f;
        [SerializeField] private int m_BaseAdditionalProjectiles = 0;
        [SerializeField] private int m_BaseProjectileBounces = 0;
        [SerializeField] [Min(0)] private int m_BasePierceCount = 0;
        [SerializeField] [Min(0.1f)] private float m_BaseChargeDurationMul = 1f;
        [SerializeField] [Min(1)] private int m_BaseBurstShotCount = 1;

        private readonly List<Modifier> m_Modifiers = new List<Modifier>(32);
        private StatCache m_StatCache;
        private WeaponController m_Weapon;
        private int m_BaseMaxAmmoSnapshot;
        private int m_BaseClipSizeSnapshot;
        private bool m_Initialized;

        public float DamageMultiplierFinal { get; private set; }
        public int AdditionalProjectilesFinal { get; private set; }
        public int ProjectileBouncesFinal { get; private set; }
        public int PierceCountFinal { get; private set; }
        public float ChargeDurationMulFinal { get; private set; }
        public int BurstShotCountFinal { get; private set; }
        public int MaxAmmoFinal { get; private set; }
        public int ClipSizeFinal { get; private set; }

        public IReadOnlyList<Modifier> Modifiers => m_Modifiers;

        private void Awake()
        {
            m_Weapon = GetComponent<WeaponController>();
            m_StatCache = new StatCache();
            m_BaseMaxAmmoSnapshot = Mathf.Max(1, m_Weapon.MaxAmmo);
            m_BaseClipSizeSnapshot = Mathf.Max(1, m_Weapon.ClipSize);
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
            m_StatCache.SetBaseValue(StatId.Weapon_Damage, m_BaseDamageMultiplier);
            m_StatCache.SetBaseValue(StatId.Weapon_AdditionalProjectiles, m_BaseAdditionalProjectiles);
            m_StatCache.SetBaseValue(StatId.Weapon_ProjectileBounces, m_BaseProjectileBounces);
            m_StatCache.SetBaseValue(StatId.Weapon_PierceCount, m_BasePierceCount);
            m_StatCache.SetBaseValue(StatId.Weapon_ChargeDuration, m_BaseChargeDurationMul);
            m_StatCache.SetBaseValue(StatId.Weapon_BurstShotCount, m_BaseBurstShotCount);
            m_StatCache.SetBaseValue(StatId.Weapon_MaxAmmo, m_BaseMaxAmmoSnapshot);
            m_StatCache.SetBaseValue(StatId.Weapon_ClipSize, m_BaseClipSizeSnapshot);
            m_StatCache.Rebuild(m_Modifiers);

            DamageMultiplierFinal = Mathf.Max(0f, m_StatCache.GetFinalValue(StatId.Weapon_Damage));
            AdditionalProjectilesFinal = Mathf.Max(0, Mathf.FloorToInt(m_StatCache.GetFinalValue(StatId.Weapon_AdditionalProjectiles)));
            ProjectileBouncesFinal = Mathf.Max(0, Mathf.FloorToInt(m_StatCache.GetFinalValue(StatId.Weapon_ProjectileBounces)));
            PierceCountFinal = Mathf.Max(0, Mathf.FloorToInt(m_StatCache.GetFinalValue(StatId.Weapon_PierceCount)));
            ChargeDurationMulFinal = Mathf.Max(0.1f, m_StatCache.GetFinalValue(StatId.Weapon_ChargeDuration));
            BurstShotCountFinal = Mathf.Max(1, Mathf.FloorToInt(m_StatCache.GetFinalValue(StatId.Weapon_BurstShotCount)));
            MaxAmmoFinal = Mathf.Max(1, Mathf.RoundToInt(m_StatCache.GetFinalValue(StatId.Weapon_MaxAmmo)));
            ClipSizeFinal = Mathf.Max(1, Mathf.RoundToInt(m_StatCache.GetFinalValue(StatId.Weapon_ClipSize)));

            if (m_Weapon != null)
            {
                m_Weapon.ApplyRuntimeMaxAmmo(MaxAmmoFinal);
                m_Weapon.ApplyRuntimeClipSize(ClipSizeFinal);
            }
        }
    }
}
