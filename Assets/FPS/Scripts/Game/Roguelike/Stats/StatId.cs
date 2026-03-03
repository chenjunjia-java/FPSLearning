namespace Unity.FPS.Roguelike.Stats
{
    /// <summary>
    /// 属性 ID，命名不区分加算/乘算；由 Modifier 的 ModifierKind 决定该条是 Add 还是 Mul。
    /// 同一 StatId 可同时接收加算与乘算词条，最终值 = (base + sum(Add)) * product(Mul)。
    /// </summary>
    public enum StatId
    {
        // Player core
        Player_MaxHealth = 0,
        Player_Attack = 1,
        Player_CritChance = 2,
        Player_CritDamage = 3,
        Player_MoveSpeed = 4,

        // Weapon core
        Weapon_Damage = 5,
        /// <summary> 武器 flat 加伤（加到基础伤害上再乘倍率），与 Weapon_Damage 倍率分开。 </summary>
        Weapon_DamageFlatAdd = 51,
        Weapon_AdditionalProjectiles = 6,
        Weapon_ProjectileBounces = 7,
        Weapon_MaxAmmo = 8,
        Weapon_ClipSize = 9,

        // Enemy core
        Enemy_MaxHealth = 10,
        Enemy_Attack = 11,
        Enemy_MoveSpeed = 12,

        // Output extensions
        Weapon_FireRate = 13,
        Weapon_ReloadRate = 14,
        Weapon_BulletSpread = 15,
        Weapon_ProjectileSpeed = 16,
        Weapon_PierceCount = 17,
        Weapon_Headshot = 18,
        Weapon_Weakspot = 19,
        Weapon_Knockback = 20,
        Weapon_AreaRadius = 21,
        Weapon_ChainHitCount = 22,

        // Survival extensions
        Player_Armor = 23,
        Player_DodgeChance = 24,
        Player_LifeSteal = 25,
        Player_InvincibleWindow = 26,
        Player_MaxShield = 27,
        Player_ShieldRegen = 28,
        Player_PickupHeal = 29,

        // Mobility extensions
        Player_DashCount = 30,
        Player_DashDistance = 31,
        Player_JumpForce = 32,
        Player_AirControl = 33,
        Player_SlideSpeed = 34,
        Player_ReloadMovePenalty = 35,

        // Resource extensions
        Run_AmmoDropChance = 36,
        Run_PickupRange = 37,
        Run_CurrencyGain = 38,
        Run_ShopDiscount = 39,
        Run_RerollCount = 40,
        Run_RewardOptionCount = 41,

        // Element and summon extensions
        Weapon_ElementProcChance = 42,
        Weapon_DotDamage = 43,
        Summon_Count = 44,
        Summon_Damage = 45,
        Weapon_ChargeDuration = 46,
        Weapon_BurstShotCount = 47,
        Enemy_AttackSpeed = 48,
        Weapon_TrajectoryCount = 49,

        // Incoming damage (bridged to Damageable)
        Player_DamageTakenMultiplier = 50,

        /// <summary> 玩家 flat 加伤（加到基础伤害上再乘倍率），与 Player_Attack 倍率分开。 </summary>
        Player_AttackFlatAdd = 52,
    }
}
