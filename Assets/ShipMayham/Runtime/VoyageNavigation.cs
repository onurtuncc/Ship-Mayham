namespace ShipMayham
{
    public sealed partial class VoyageSimulation
    {
        /// <summary>Connected crew scale pressure, not passenger counts, economy or active task deadlines.</summary>
        public float CrewPressure
        {
            get
            {
                int count = 0;
                foreach (var member in Crew) if (member.Active && member.ClientId != ulong.MaxValue) count++;
                return count >= 4 ? 1 : count == 3 ? .8f : count == 2 ? .6f : .45f;
            }
        }
    }
}
