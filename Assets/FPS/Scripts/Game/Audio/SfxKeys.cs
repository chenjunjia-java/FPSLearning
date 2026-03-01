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
                default:
                    return System.Enum.TryParse(key, true, out sfxKey) && sfxKey != SfxKey.None;
            }
        }
    }
}

