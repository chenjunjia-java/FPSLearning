namespace Unity.FPS.Game
{
    public static class SfxKeys
    {
        // Legacy string keys kept for backwards compatibility with existing configs.
        public const string EnemyDetect = "sfx.enemy.detect";
        public const string Impact = "sfx.impact";
        public const string DamageTick = "sfx.damage.tick";
        public const string Pickup = "sfx.pickup";
        public const string WeaponShoot = "sfx.weapon.shoot";
        public const string WeaponChange = "sfx.weapon.change";
        public const string WeaponContinuousStart = "sfx.weapon.continuous.start";
        public const string WeaponContinuousLoop = "sfx.weapon.continuous.loop";
        public const string WeaponContinuousEnd = "sfx.weapon.continuous.end";
        public const string Footstep = "sfx.footstep";
        public const string Jump = "sfx.jump";
        public const string Land = "sfx.land";
        public const string FallDamage = "sfx.falldamage";
        public const string Jetpack = "sfx.jetpack";
        public const string Movement = "sfx.movement";
        public const string CoolingCells = "sfx.coolingcells";
        public const string HUDObjectiveInit = "sfx.hud.objective.init";
        public const string HUDObjectiveCompleted = "sfx.hud.objective.completed";
        public const string Victory = "sfx.victory";
        public const string WeaponChargeStart = "sfx.weapon.charge.start";
        public const string WeaponChargeLoop = "sfx.weapon.charge.loop";

        public static bool TryParse(string key, out SfxKey sfxKey)
        {
            if (string.IsNullOrEmpty(key))
            {
                sfxKey = SfxKey.None;
                return false;
            }

            switch (key)
            {
                case EnemyDetect:
                    sfxKey = SfxKey.EnemyDetect;
                    return true;
                case Impact:
                    sfxKey = SfxKey.Impact;
                    return true;
                case DamageTick:
                    sfxKey = SfxKey.DamageTick;
                    return true;
                case Pickup:
                    sfxKey = SfxKey.Pickup;
                    return true;
                case WeaponShoot:
                    sfxKey = SfxKey.WeaponShoot;
                    return true;
                case WeaponChange:
                    sfxKey = SfxKey.WeaponChange;
                    return true;
                case WeaponContinuousStart:
                    sfxKey = SfxKey.WeaponContinuousStart;
                    return true;
                case WeaponContinuousLoop:
                    sfxKey = SfxKey.WeaponContinuousLoop;
                    return true;
                case WeaponContinuousEnd:
                    sfxKey = SfxKey.WeaponContinuousEnd;
                    return true;
                case Footstep:
                    sfxKey = SfxKey.Footstep;
                    return true;
                case Jump:
                    sfxKey = SfxKey.Jump;
                    return true;
                case Land:
                    sfxKey = SfxKey.Land;
                    return true;
                case FallDamage:
                    sfxKey = SfxKey.FallDamage;
                    return true;
                case Jetpack:
                    sfxKey = SfxKey.Jetpack;
                    return true;
                case Movement:
                    sfxKey = SfxKey.Movement;
                    return true;
                case CoolingCells:
                    sfxKey = SfxKey.CoolingCells;
                    return true;
                case HUDObjectiveInit:
                    sfxKey = SfxKey.HUDObjectiveInit;
                    return true;
                case HUDObjectiveCompleted:
                    sfxKey = SfxKey.HUDObjectiveCompleted;
                    return true;
                case Victory:
                    sfxKey = SfxKey.Victory;
                    return true;
                case WeaponChargeStart:
                    sfxKey = SfxKey.WeaponChargeStart;
                    return true;
                case WeaponChargeLoop:
                    sfxKey = SfxKey.WeaponChargeLoop;
                    return true;
                default:
                    return System.Enum.TryParse(key, true, out sfxKey) && sfxKey != SfxKey.None;
            }
        }
    }
}

