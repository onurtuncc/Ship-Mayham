namespace ShipMayham
{
    public sealed partial class VoyageSimulation
    {
        /// <summary>Connected crew scale pressure, not passenger counts, economy or active task deadlines.</summary>
        public float CrewPressure => WorkloadScale;
    }
}
