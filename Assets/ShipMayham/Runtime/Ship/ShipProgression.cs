namespace ShipMayham
{
    /// <summary>Pure rules for what the ship visibly gains from upgrades and loses to damage. Mirrors VoyageSimulation levels.</summary>
    public static class ShipProgression
    {
        public const int BaseSeats = 4, SeatsPerLevel = 2, MaxSeats = 8;
        public const float LightDamagePercent = 60, HeavyDamagePercent = 30;

        public static int UnlockedSeats(int capacityLevel) => System.Math.Min(MaxSeats, BaseSeats + SeatsPerLevel * System.Math.Max(0, capacityLevel));

        /// <summary>One smokestack from engine level 1, a second at level 2.</summary>
        public static int Funnels(int engineLevel) => System.Math.Max(0, System.Math.Min(2, engineLevel));

        public static bool HasBoosters(int engineLevel) => engineLevel >= 2;

        public static bool HasComfortFittings(int comfortLevel) => comfortLevel >= 1;

        /// <summary>0 = fine, 1 = smoking, 2 = burning.</summary>
        public static int DamageStage(float health, float maxHealth = 100)
        {
            float percent = maxHealth <= 0 ? 0 : health / maxHealth * 100;
            return percent < HeavyDamagePercent ? 2 : percent < LightDamagePercent ? 1 : 0;
        }
    }
}
