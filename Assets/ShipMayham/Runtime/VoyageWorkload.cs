namespace ShipMayham
{
    /// <summary>
    /// Scales the pressure of a voyage to the crew size: ship events and the pirate threat come further apart for a smaller crew.
    /// Leaks, passengers and the economy are unchanged. The crew size is read when a delay is scheduled, so a connection
    /// change never restarts a running countdown.
    /// </summary>
    public sealed partial class VoyageSimulation
    {
        public const float BasePirateArrival = 12f, BasePowerFault = 22f, BasePirateShotInterval = 8f;

        private float pirateAt = BasePirateArrival, powerFaultAt = BasePowerFault;
        private bool workloadReady;

        public int ActiveCrewCount
        {
            get { int count = 0; foreach (var crew in Crew) if (crew.Active) count++; return count; }
        }

        /// <summary>The pressure factor for the crew that is aboard right now.</summary>
        public float WorkloadScale => Workload.ScaleFor(ActiveCrewCount);

        public float PirateAt => pirateAt;

        public float PowerFaultAt => powerFaultAt;

        private float PirateShotInterval => Workload.Stretch(BasePirateShotInterval, WorkloadScale);

        private float EventGapSeconds => Workload.Stretch(EventGap, WorkloadScale);

        private void ResetWorkload()
        {
            pirateAt = BasePirateArrival;
            powerFaultAt = BasePowerFault;
            workloadReady = false;
        }

        /// <summary>Fixes the voyage's timetable the first time it sails, from the crew that departs.</summary>
        private void BeginWorkload()
        {
            float scale = WorkloadScale;
            pirateAt = Workload.Stretch(BasePirateArrival, scale);
            powerFaultAt = Workload.Stretch(BasePowerFault, scale);
            // The route may already have shortened the first delay; the crew size stretches whatever it is.
            if (Event.Phase == VoyageEventPhase.Waiting) Event.NextIn = Workload.Stretch(Event.NextIn, scale);
            workloadReady = true;
        }
    }
}
