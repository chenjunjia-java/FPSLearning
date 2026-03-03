using System.Collections.Generic;

namespace Unity.FPS.Roguelike.Stats
{
    public enum StatCategory
    {
        Core,
        Output,
        Survival,
        Mobility,
        Resource,
        ElementAndSummon,
    }

    public static class RoguelikeStatCatalog
    {
        private static readonly StatId[] s_OutputStats =
        {
            StatId.Weapon_Damage,
            StatId.Weapon_AdditionalProjectiles,
            StatId.Weapon_ProjectileBounces,
            StatId.Weapon_FireRate,
            StatId.Weapon_ReloadRate,
            StatId.Weapon_BulletSpread,
            StatId.Weapon_ProjectileSpeed,
            StatId.Weapon_PierceCount,
            StatId.Weapon_Headshot,
            StatId.Weapon_Weakspot,
            StatId.Weapon_Knockback,
            StatId.Weapon_AreaRadius,
            StatId.Weapon_ChainHitCount,
            StatId.Weapon_ChargeDuration,
            StatId.Weapon_BurstShotCount,
            StatId.Weapon_TrajectoryCount,
        };

        private static readonly StatId[] s_SurvivalStats =
        {
            StatId.Player_MaxHealth,
            StatId.Player_Armor,
            StatId.Player_DodgeChance,
            StatId.Player_LifeSteal,
            StatId.Player_InvincibleWindow,
            StatId.Player_MaxShield,
            StatId.Player_ShieldRegen,
            StatId.Player_PickupHeal,
            StatId.Player_DamageTakenMultiplier,
        };

        private static readonly StatId[] s_MobilityStats =
        {
            StatId.Player_MoveSpeed,
            StatId.Player_DashCount,
            StatId.Player_DashDistance,
            StatId.Player_JumpForce,
            StatId.Player_AirControl,
            StatId.Player_SlideSpeed,
            StatId.Player_ReloadMovePenalty,
        };

        private static readonly StatId[] s_ResourceStats =
        {
            StatId.Weapon_MaxAmmo,
            StatId.Weapon_ClipSize,
            StatId.Run_AmmoDropChance,
            StatId.Run_PickupRange,
            StatId.Run_CurrencyGain,
            StatId.Run_ShopDiscount,
            StatId.Run_RerollCount,
            StatId.Run_RewardOptionCount,
        };

        private static readonly StatId[] s_ElementAndSummonStats =
        {
            StatId.Weapon_ElementProcChance,
            StatId.Weapon_DotDamage,
            StatId.Summon_Count,
            StatId.Summon_Damage,
        };

        public static IReadOnlyList<StatId> GetByCategory(StatCategory category)
        {
            switch (category)
            {
                case StatCategory.Output:
                    return s_OutputStats;
                case StatCategory.Survival:
                    return s_SurvivalStats;
                case StatCategory.Mobility:
                    return s_MobilityStats;
                case StatCategory.Resource:
                    return s_ResourceStats;
                case StatCategory.ElementAndSummon:
                    return s_ElementAndSummonStats;
                default:
                    return s_OutputStats;
            }
        }

        public static StatCategory GetCategory(StatId statId)
        {
            if (Contains(s_OutputStats, statId))
            {
                return StatCategory.Output;
            }

            if (Contains(s_SurvivalStats, statId))
            {
                return StatCategory.Survival;
            }

            if (Contains(s_MobilityStats, statId))
            {
                return StatCategory.Mobility;
            }

            if (Contains(s_ResourceStats, statId))
            {
                return StatCategory.Resource;
            }

            if (Contains(s_ElementAndSummonStats, statId))
            {
                return StatCategory.ElementAndSummon;
            }

            return StatCategory.Core;
        }

        private static bool Contains(StatId[] stats, StatId statId)
        {
            for (int i = 0; i < stats.Length; i++)
            {
                if (stats[i] == statId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
