using System.Collections.Generic;

namespace ShipMayham
{
    public enum FeedbackKind : byte
    {
        CoinsEarned, PassengerBoarded, ServiceDelivered, PassengerThanked, PassengerComplained, FareCollected, PassengerRescued,
        HullDamaged, LeakStarted, LeakFixed, VoyageStarted, VoyageArrived, VoyageFailed, ReputationGained, UpgradePurchased,
        DockJobDone, EventResolved, EventFailed, CannonFired, CannonballLanded, PirateHit, PirateDefeated, ItemPickedUp, ItemDelivered,
        PassengerThrown, WaveWarning, WaveDodged, ShoalAhead
    }

    /// <summary>One cosmetic reaction to a replicated state change. Slot is a passenger or crew index when relevant.</summary>
    public readonly struct FeedbackEvent
    {
        public readonly FeedbackKind Kind;
        public readonly int Slot, Value;
        public FeedbackEvent(FeedbackKind kind, int slot = -1, int value = 0) { Kind = kind; Slot = slot; Value = value; }
        public override string ToString() => $"{Kind}[{Slot}]={Value}";
    }

    /// <summary>Everything the feedback layer compares between frames. Plain data so detection is unit-testable.</summary>
    public struct FeedbackSnapshot
    {
        public const int MaxPassengers = VoyageSimulation.MaxPassengers;
        public const int MaxCrew = 4;

        public bool Valid;
        public VoyagePhase Phase;
        public int Wallet, Reputation, Leak, UpgradeLevels, DockJobsCompleted, RescueCount, CollectedFares;
        public float Health, HullShield, PirateHealth;
        public VoyageEventPhase EventPhase;
        public int EventSerial;
        public PiratePhase Pirate;
        public bool CrewBallActive, PirateBallActive;
        public bool WaveWarning, ShoalNear;
        public int WavesDodged;
        public PassengerSnapshot[] Passengers;
        public ServiceItem[] Carrying;
        public bool[] CrewActive;

        public static FeedbackSnapshot Create() => new FeedbackSnapshot
        {
            Passengers = new PassengerSnapshot[MaxPassengers], Carrying = new ServiceItem[MaxCrew], CrewActive = new bool[MaxCrew]
        };
    }

    public struct PassengerSnapshot
    {
        public bool Active, Reacting;
        public ServiceItem Request;
        public PassengerReaction Reaction;
        public PassengerMotion Motion;
        public int Carrier;
    }

    public static class FeedbackDetector
    {
        /// <summary>
        /// Appends the events that explain the difference between two frames. A snapshot that is not Valid (first frame, session
        /// not spawned) produces nothing, so joining a running voyage never replays a firework show.
        /// </summary>
        public static void Detect(in FeedbackSnapshot previous, in FeedbackSnapshot current, List<FeedbackEvent> output)
        {
            if (!previous.Valid || !current.Valid) return;

            if (current.Phase != previous.Phase)
            {
                if (current.Phase == VoyagePhase.Sailing) output.Add(new FeedbackEvent(FeedbackKind.VoyageStarted));
                else if (current.Phase == VoyagePhase.Arrived) output.Add(new FeedbackEvent(FeedbackKind.VoyageArrived, -1, current.Wallet - previous.Wallet));
                else if (current.Phase == VoyagePhase.Failed) output.Add(new FeedbackEvent(FeedbackKind.VoyageFailed));
            }
            // A new voyage resets the wallet downwards or re-seeds it; only genuine gains count as earnings.
            if (current.Wallet > previous.Wallet && current.Phase == previous.Phase) output.Add(new FeedbackEvent(FeedbackKind.CoinsEarned, -1, current.Wallet - previous.Wallet));
            if (current.Reputation > previous.Reputation) output.Add(new FeedbackEvent(FeedbackKind.ReputationGained, -1, current.Reputation - previous.Reputation));
            if (current.UpgradeLevels > previous.UpgradeLevels) output.Add(new FeedbackEvent(FeedbackKind.UpgradePurchased, -1, current.UpgradeLevels));
            if (current.DockJobsCompleted > previous.DockJobsCompleted) output.Add(new FeedbackEvent(FeedbackKind.DockJobDone));
            if (current.CollectedFares > previous.CollectedFares) output.Add(new FeedbackEvent(FeedbackKind.FareCollected, -1, current.CollectedFares - previous.CollectedFares));

            float lost = (previous.Health + previous.HullShield) - (current.Health + current.HullShield);
            if (lost > .5f && current.Phase == previous.Phase) output.Add(new FeedbackEvent(FeedbackKind.HullDamaged, -1, (int)lost));
            if (current.Phase == VoyagePhase.Sailing && previous.Phase == VoyagePhase.Sailing)
            {
                if (current.WaveWarning && !previous.WaveWarning) output.Add(new FeedbackEvent(FeedbackKind.WaveWarning));
                if (current.WavesDodged > previous.WavesDodged) output.Add(new FeedbackEvent(FeedbackKind.WaveDodged));
                if (current.ShoalNear && !previous.ShoalNear) output.Add(new FeedbackEvent(FeedbackKind.ShoalAhead));
            }
            if (current.Leak > previous.Leak) output.Add(new FeedbackEvent(FeedbackKind.LeakStarted));
            else if (current.Leak == 0 && previous.Leak > 0) output.Add(new FeedbackEvent(FeedbackKind.LeakFixed));

            if (current.EventSerial == previous.EventSerial && current.EventPhase != previous.EventPhase)
            {
                if (current.EventPhase == VoyageEventPhase.Resolved) output.Add(new FeedbackEvent(FeedbackKind.EventResolved));
                else if (current.EventPhase == VoyageEventPhase.Failed) output.Add(new FeedbackEvent(FeedbackKind.EventFailed));
            }

            if (current.CrewBallActive && !previous.CrewBallActive) output.Add(new FeedbackEvent(FeedbackKind.CannonFired));
            if (previous.CrewBallActive && !current.CrewBallActive) output.Add(new FeedbackEvent(FeedbackKind.CannonballLanded));
            if (current.PirateHealth < previous.PirateHealth - .5f && current.Pirate == PiratePhase.Attacking)
                output.Add(new FeedbackEvent(FeedbackKind.PirateHit, -1, (int)(previous.PirateHealth - current.PirateHealth)));
            if (current.Pirate == PiratePhase.Defeated && previous.Pirate != PiratePhase.Defeated) output.Add(new FeedbackEvent(FeedbackKind.PirateDefeated));

            for (int i = 0; i < FeedbackSnapshot.MaxPassengers; i++)
            {
                var before = previous.Passengers[i]; var after = current.Passengers[i];
                if (after.Active && !before.Active) output.Add(new FeedbackEvent(FeedbackKind.PassengerBoarded, i));
                if (!after.Active) continue;
                if (before.Request != ServiceItem.None && after.Request == ServiceItem.None && after.Motion == PassengerMotion.OnDeck)
                    output.Add(new FeedbackEvent(FeedbackKind.ServiceDelivered, i, (int)before.Request));
                if (before.Motion == PassengerMotion.Carried && after.Motion == PassengerMotion.Airborne)
                    output.Add(new FeedbackEvent(FeedbackKind.PassengerThrown, i, before.Carrier));
                if (after.Reacting && (!before.Reacting || after.Reaction != before.Reaction))
                {
                    if (after.Reaction == PassengerReaction.Thanks) output.Add(new FeedbackEvent(FeedbackKind.PassengerThanked, i));
                    else if (after.Reaction == PassengerReaction.Complaint || after.Reaction == PassengerReaction.TicketDue) output.Add(new FeedbackEvent(FeedbackKind.PassengerComplained, i));
                    else if (after.Reaction == PassengerReaction.Rescued) output.Add(new FeedbackEvent(FeedbackKind.PassengerRescued, i));
                }
            }

            for (int i = 0; i < FeedbackSnapshot.MaxCrew; i++)
            {
                if (!previous.CrewActive[i] || !current.CrewActive[i]) continue;
                bool had = previous.Carrying[i] != ServiceItem.None, has = current.Carrying[i] != ServiceItem.None;
                if (!had && has) output.Add(new FeedbackEvent(FeedbackKind.ItemPickedUp, i));
                else if (had && !has) output.Add(new FeedbackEvent(FeedbackKind.ItemDelivered, i));
            }
        }
    }
}
