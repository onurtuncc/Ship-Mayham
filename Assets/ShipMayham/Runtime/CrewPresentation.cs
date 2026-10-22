namespace ShipMayham
{
    /// <summary>Animator state a crew body plays; names match the states of the generated crew controller.</summary>
    public enum CrewPose : byte
    {
        Idle, Walking, Carrying, Jump, PickUp, Throw, Interact, Dance, Dance2, Victory, Defeated, Salute, Clap, LookAround
    }

    public static class CrewPresentation
    {
        /// <summary>Visual speed (m/s) above which a crew member is shown walking rather than standing.</summary>
        public const float WalkSpeedThreshold = .35f;
        /// <summary>Height (m) above the deck from which a crew member is shown airborne.</summary>
        public const float AirborneHeight = .03f;

        /// <summary>
        /// Continuous pose from replicated state. Short one-shot gestures (pick up, throw, clap, victory) are layered on top by the
        /// animator when the matching feedback event fires; they are never part of the replicated state.
        /// </summary>
        public static CrewPose PoseFor(CrewState crew, float visualSpeed, VoyagePhase phase = VoyagePhase.Boarding, int slot = 0)
        {
            if (phase == VoyagePhase.Failed) return CrewPose.Defeated;
            if (phase == VoyagePhase.Arrived) return slot % 2 == 0 ? CrewPose.Dance : CrewPose.Dance2;
            if (crew.JumpHeight > AirborneHeight) return CrewPose.Jump;
            if (crew.Carrying != ServiceItem.None) return CrewPose.Carrying;
            if (crew.AtCannon || crew.AtCrane) return CrewPose.Interact;
            return visualSpeed > WalkSpeedThreshold ? CrewPose.Walking : CrewPose.Idle;
        }
    }
}
