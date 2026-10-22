namespace ShipMayham
{
    /// <summary>Animator state a crew body plays; names match the states of the generated crew controller.</summary>
    public enum CrewPose : byte { Idle, Walking, Carrying }

    public static class CrewPresentation
    {
        /// <summary>Visual speed (m/s) above which a crew member is shown walking rather than standing.</summary>
        public const float WalkSpeedThreshold = .35f;

        public static CrewPose PoseFor(CrewState crew, float visualSpeed)
        {
            if (crew.Carrying != ServiceItem.None) return CrewPose.Carrying;
            return visualSpeed > WalkSpeedThreshold ? CrewPose.Walking : CrewPose.Idle;
        }
    }
}
