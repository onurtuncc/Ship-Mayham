namespace ShipMayham
{
    public enum PassengerCue : byte { Relaxed, WaitingForService, Impatient, CheckingPockets, GuardingLuggage }
    /// <summary>Animator state a passenger body plays; names match the states of the generated passenger controller.</summary>
    public enum PassengerPose : byte { Seated, Pleading, Fidgeting, Panicked, Delighted, Yelling, Disbelief, Laughing, Clapping, Drinking }

    public static class PassengerPresentation
    {
        public static PassengerCue CueFor(PassengerState p, VoyageEventState e, VoyageState voyage, int index)
        {
            if (!p.Active || p.Motion != PassengerMotion.OnDeck || index < 0 || index >= VoyageSimulation.MaxPassengers) return PassengerCue.Relaxed;
            if (voyage.Phase == VoyagePhase.Sailing && e.Phase == VoyageEventPhase.Active && e.Kind == VoyageEventKind.ContrabandInspection && e.TargetPassenger == index)
                return PassengerCue.GuardingLuggage;
            if (p.Request != ServiceItem.None && p.Satisfaction < 55) return PassengerCue.Impatient;
            if (voyage.Phase == VoyagePhase.Sailing)
            {
                // A tell, not a verdict: some innocent passengers also check their pockets.
                float phase = UnityEngine.Mathf.Repeat(voyage.Elapsed + index * 1.7f, 7);
                if ((p.TicketChecked && !p.FarePaid) || (!p.TicketChecked && phase < (!p.FarePaid ? 2.5f : index % 3 == 1 ? 1.25f : 0)))
                    return PassengerCue.CheckingPockets;
            }
            return p.Request == ServiceItem.None ? PassengerCue.Relaxed : PassengerCue.WaitingForService;
        }

        public static PassengerPose PoseFor(PassengerState p, PassengerCue cue)
        {
            if (!p.Active) return PassengerPose.Seated;
            if (p.Motion != PassengerMotion.OnDeck) return PassengerPose.Panicked;
            if (p.ReactionRemaining > 0)
            {
                switch (p.Reaction)
                {
                    case PassengerReaction.Thanks: return PassengerPose.Delighted;
                    case PassengerReaction.Complaint: return PassengerPose.Yelling;
                    case PassengerReaction.TicketDue:
                    case PassengerReaction.FareCollected: return PassengerPose.Disbelief;
                    case PassengerReaction.Rescued: return PassengerPose.Laughing;
                }
            }
            switch (cue)
            {
                case PassengerCue.Impatient: return PassengerPose.Pleading;
                case PassengerCue.CheckingPockets:
                case PassengerCue.GuardingLuggage: return PassengerPose.Fidgeting;
                default: return PassengerPose.Seated;
            }
        }

        public static string CueText(PassengerCue cue)
        {
            switch (cue)
            {
                case PassengerCue.GuardingLuggage: return "Clutching a suitcase close...";
                case PassengerCue.CheckingPockets: return "Keeps checking their pockets...";
                case PassengerCue.Impatient: return "Tapping impatiently for service";
                case PassengerCue.WaitingForService: return "Looking toward the service counter";
                default: return "Watching the sea";
            }
        }

        public static string TicketText(PassengerState p) => !p.TicketChecked ? "Ticket: unchecked" : p.FarePaid ? "Ticket: PAID" : "Ticket: DUE 40";

        public static string ReactionText(PassengerState p)
        {
            if (p.Motion == PassengerMotion.Carried) return "Put me down!";
            if (p.Motion == PassengerMotion.Airborne) return "Wheee!";
            if (p.Motion == PassengerMotion.Rescuing) return $"HELP! Rescue in {p.RecoveryRemaining:0.0}s";
            if (p.ReactionRemaining > 0)
            {
                switch (p.Reaction)
                {
                    case PassengerReaction.Thanks: return "Thank you!";
                    case PassengerReaction.TicketDue: return "I thought this was free...";
                    case PassengerReaction.FareCollected: return "Fine, here's your 40 coins.";
                    case PassengerReaction.Complaint: return "Watch the suit!";
                    case PassengerReaction.Rescued: return "One star. Never again!";
                }
            }
            if (p.Satisfaction < 55) return "Where is my order?!";
            return p.Request == ServiceItem.None ? "Enjoying the trip" : "Waiting for service";
        }
    }
}
