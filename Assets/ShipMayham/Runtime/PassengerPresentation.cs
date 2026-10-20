namespace ShipMayham
{
    public static class PassengerPresentation
    {
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
