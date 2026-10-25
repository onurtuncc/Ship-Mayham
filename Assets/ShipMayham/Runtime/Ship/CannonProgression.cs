namespace ShipMayham
{
    /// <summary>
    /// Pure rules for what the two cannon upgrades give. Gunnery trains the gun crew (faster reload, a bigger rack, harder hits);
    /// the battery fits extra barrels that fire a spread of lighter rounds from the same shot.
    /// </summary>
    public static class CannonProgression
    {
        public const int MaxGunnery = 2, MaxBattery = 2;
        public const float BaseReloadSeconds = 2.5f, BaseHitDamage = 34, VolleyDamageFactor = .5f, VolleySpreadDegrees = 3.5f;
        public const int BaseRack = 3;

        private static int Clamp(int level, int max) => level < 0 ? 0 : level > max ? max : level;

        public static float ReloadSeconds(int gunneryLevel) => BaseReloadSeconds - .5f * Clamp(gunneryLevel, MaxGunnery);

        public static int RackCapacity(int gunneryLevel) => BaseRack + Clamp(gunneryLevel, MaxGunnery);

        /// <summary>Pirate health taken by one hit of the main barrel (the pirate has 100): 34, 40, then 50.</summary>
        public static float HitDamage(int gunneryLevel) => Clamp(gunneryLevel, MaxGunnery) == 2 ? 50 : BaseHitDamage + 6 * Clamp(gunneryLevel, MaxGunnery);

        /// <summary>Barrels beside the main one: 0, 1 or 2.</summary>
        public static int ExtraBarrels(int batteryLevel) => Clamp(batteryLevel, MaxBattery);

        public static float VolleyHitDamage(int gunneryLevel) => HitDamage(gunneryLevel) * VolleyDamageFactor;

        /// <summary>Yaw offset in degrees of extra barrel <paramref name="index"/> (0 or 1): one to the right, then one to the left.</summary>
        public static float VolleyYaw(int index) => index == 0 ? VolleySpreadDegrees : -VolleySpreadDegrees;
    }
}
