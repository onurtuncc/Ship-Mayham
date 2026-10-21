using UnityEngine;

namespace ShipMayham
{
    /// <summary>Small deterministic event deck. All outcomes are decided by the host simulation.</summary>
    public sealed partial class VoyageSimulation
    {
        public const float FirstEventDelay = 18f, EventGap = 24f, EventDuration = 32f;
        public VoyageEventState Event;
        private int eventDeckCursor;

        private void ResetEvents()
        {
            Event = new VoyageEventState
            {
                Phase = VoyageEventPhase.Waiting,
                TargetPassenger = -1,
                NextIn = FirstEventDelay
            };
            eventDeckCursor = (State.Voyage - 1) % 3;
        }

        private void TickEvents(float dt)
        {
            if (Event.Phase == VoyageEventPhase.Resolved || Event.Phase == VoyageEventPhase.Failed)
            {
                Event.NextIn = Mathf.Max(0, Event.NextIn - dt);
                if (Event.NextIn == 0)
                {
                    ContinueEventDeck();
                    BeginNextEvent();
                }
                return;
            }
            if (Event.Phase == VoyageEventPhase.Waiting)
            {
                Event.NextIn = Mathf.Max(0, Event.NextIn - dt);
                if (Event.NextIn == 0) BeginNextEvent();
                return;
            }
            if (Event.Phase != VoyageEventPhase.Active) return;
            Event.Remaining = Mathf.Max(0, Event.Remaining - dt);
            if (Event.Remaining == 0) FailEvent();
        }

        private void BeginNextEvent()
        {
            VoyageEventKind kind = (VoyageEventKind)(1 + eventDeckCursor % 3);
            eventDeckCursor++;
            Event.Kind = kind;
            Event.Phase = VoyageEventPhase.Active;
            Event.Serial++;
            Event.Progress = 0;
            Event.Goal = kind == VoyageEventKind.GalleyRush ? 2 : kind == VoyageEventKind.LooseCargo ? 3 : 1;
            Event.Remaining = State.Route == RouteProfile.StormPassage ? 24 : State.Route == RouteProfile.MarketRun ? 28 : EventDuration;
            Event.NextIn = 0;
            Event.TargetPassenger = kind == VoyageEventKind.ContrabandInspection && State.Boarded > 0
                ? (State.Voyage + Event.Serial) % State.Boarded
                : -1;
        }

        private void ResolveEvent()
        {
            if (Event.Phase != VoyageEventPhase.Active) return;
            Event.Phase = VoyageEventPhase.Resolved;
            Event.ResolvedCount++;
            Event.Reward += 15;
            Event.Remaining = 0;
            Event.NextIn = EventGap;
        }

        private void FailEvent()
        {
            if (Event.Phase != VoyageEventPhase.Active) return;
            Event.Phase = VoyageEventPhase.Failed;
            Event.FailedCount++;
            if (Event.Kind == VoyageEventKind.ContrabandInspection)
            {
                DamageHull(5);
                CutPower();
            }
            else if (Event.Kind == VoyageEventKind.GalleyRush)
            {
                for (int i = 0; i < MaxPassengers; i++)
                    if (Passengers[i].Active) Passengers[i].Satisfaction = Mathf.Max(0, Passengers[i].Satisfaction - 8);
            }
            else if (Event.Kind == VoyageEventKind.LooseCargo) DamageHull(12);
            Event.Remaining = 0;
            Event.NextIn = EventGap;
        }

        private string InspectPassengerForEvent(int passenger)
        {
            if (Event.Phase != VoyageEventPhase.Active || Event.Kind != VoyageEventKind.ContrabandInspection)
                return null;
            if (passenger != Event.TargetPassenger) return " No contraband here; keep checking passengers.";
            Event.Progress = Event.Goal;
            ResolveEvent();
            return " Contraband found! Crisis solved; +15 event bonus on arrival.";
        }

        private void RecordServiceForEvent()
        {
            if (Event.Phase != VoyageEventPhase.Active || Event.Kind != VoyageEventKind.GalleyRush) return;
            Event.Progress++;
            if (Event.Progress >= Event.Goal) ResolveEvent();
        }

        private string ClearLooseCargo()
        {
            if (Event.Phase != VoyageEventPhase.Active || Event.Kind != VoyageEventKind.LooseCargo) return null;
            Event.Progress++;
            if (Event.Progress < Event.Goal) return $"Securing loose cargo: {Event.Progress}/{Event.Goal}";
            ResolveEvent();
            return "Cargo secured! +15 event bonus on arrival.";
        }

        private void ContinueEventDeck()
        {
            if (Event.Phase != VoyageEventPhase.Resolved && Event.Phase != VoyageEventPhase.Failed) return;
            Event.Kind = VoyageEventKind.None;
            Event.Phase = VoyageEventPhase.Waiting;
            Event.Progress = 0;
            Event.Goal = 0;
            Event.TargetPassenger = -1;
            Event.NextIn = 0;
        }
    }
}
