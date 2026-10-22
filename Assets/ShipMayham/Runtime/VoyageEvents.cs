using UnityEngine;

namespace ShipMayham
{
    /// <summary>Seeded weighted deck without replacement. Only the host chooses and resolves events.</summary>
    public sealed partial class VoyageSimulation
    {
        public const float FirstEventDelay = 18f, EventGap = 24f, EventDuration = 32f;
        public VoyageEventState Event;
        private uint eventRandomState, remainingEvents;

        private uint NextEventRandom()
        {
            eventRandomState ^= eventRandomState << 13;
            eventRandomState ^= eventRandomState >> 17;
            eventRandomState ^= eventRandomState << 5;
            return eventRandomState;
        }

        private void ResetEvents()
        {
            Event = new VoyageEventState
            {
                Phase = VoyageEventPhase.Waiting,
                TargetPassenger = -1,
                NextIn = FirstEventDelay
            };
            remainingEvents = 0;
        }

        private void TickEvents(float dt)
        {
            if (Event.Phase == VoyageEventPhase.Resolved || Event.Phase == VoyageEventPhase.Failed)
            {
                Event.NextIn = Mathf.Max(0, Event.NextIn - dt);
                if (Event.NextIn == 0)
                {
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
            if (Event.Kind == VoyageEventKind.EngineOverheat)
            {
                Event.CoolingTime = State.Speed <= 1 ? Event.CoolingTime + dt : 0;
                if (Event.CoolingTime >= 8) { ResolveEvent(); return; }
            }
            Event.Remaining = Mathf.Max(0, Event.Remaining - dt);
            if (Event.Remaining == 0) FailEvent();
        }

        private bool HasEventStation(StationKind kind)
        {
            foreach (var station in Stations) if (station.Kind == kind) return true;
            return false;
        }

        private ServiceItem AvailableEventOrder()
        {
            if (State.FoodStock > 0 && HasEventStation(StationKind.Food)) return ServiceItem.Food;
            if (State.DrinkStock > 0 && HasEventStation(StationKind.Drink)) return ServiceItem.Drink;
            return ServiceItem.None;
        }

        private uint EligibleEvents()
        {
            uint mask = 0;
            int passengerCount = 0;
            foreach (var passenger in Passengers)
                if (passenger.Active && passenger.Motion == PassengerMotion.OnDeck) passengerCount++;
            if (passengerCount > 0)
            {
                mask |= 1u << (int)VoyageEventKind.ContrabandInspection;
                mask |= 1u << (int)VoyageEventKind.TicketAudit;
                mask |= 1u << (int)VoyageEventKind.SeatShuffle;
                if (AvailableEventOrder() != ServiceItem.None)
                {
                    mask |= 1u << (int)VoyageEventKind.VipService;
                    int stock = (HasEventStation(StationKind.Food) ? State.FoodStock : 0) +
                        (HasEventStation(StationKind.Drink) ? State.DrinkStock : 0);
                    if (stock >= 2 && passengerCount >= 2) mask |= 1u << (int)VoyageEventKind.GalleyRush;
                }
            }
            if (HasEventStation(StationKind.Repair))
                mask |= (1u << (int)VoyageEventKind.LooseCargo) | (1u << (int)VoyageEventKind.EngineOverheat);
            if (HasEventStation(StationKind.Power)) mask |= 1u << (int)VoyageEventKind.FuseOverload;
            return mask;
        }

        private int EventWeight(VoyageEventKind kind)
        {
            if (State.Route == RouteProfile.MarketRun && (kind == VoyageEventKind.VipService || kind == VoyageEventKind.GalleyRush)) return 3;
            if (State.Route == RouteProfile.StormPassage && (kind == VoyageEventKind.LooseCargo || kind == VoyageEventKind.FuseOverload || kind == VoyageEventKind.EngineOverheat)) return 3;
            return 1;
        }

        private void PrepareGalleyOrders()
        {
            int food = HasEventStation(StationKind.Food) ? State.FoodStock : 0;
            int drink = HasEventStation(StationKind.Drink) ? State.DrinkStock : 0;
            int prepared = 0;
            for (int p = 0; p < MaxPassengers && prepared < Event.Goal; p++)
            {
                if (!Passengers[p].Active || Passengers[p].Motion != PassengerMotion.OnDeck) continue;
                ServiceItem order = Passengers[p].Request;
                if (order == ServiceItem.None || order == ServiceItem.Food && food == 0 || order == ServiceItem.Drink && drink == 0)
                    order = food > 0 ? ServiceItem.Food : ServiceItem.Drink;
                Passengers[p].Request = order;
                Passengers[p].RequestTimer = 0;
                if (order == ServiceItem.Food) food--; else drink--;
                prepared++;
            }
        }

        private void BeginNextEvent()
        {
            uint eligible = EligibleEvents();
            uint candidates = remainingEvents & eligible;
            if (candidates == 0) { remainingEvents = eligible; candidates = eligible; }
            uint withoutPrevious = candidates & ~(1u << (int)Event.Kind);
            if (withoutPrevious != 0) candidates = withoutPrevious;
            if (candidates == 0)
            {
                Event.Phase = VoyageEventPhase.Waiting;
                Event.Kind = VoyageEventKind.None;
                Event.NextIn = EventGap;
                return;
            }
            int weight = 0;
            for (int i = 1; i <= (int)VoyageEventKind.SeatShuffle; i++)
                if ((candidates & (1u << i)) != 0) weight += EventWeight((VoyageEventKind)i);
            int roll = (int)(NextEventRandom() % (uint)weight);
            VoyageEventKind kind = VoyageEventKind.None;
            for (int i = 1; i <= (int)VoyageEventKind.SeatShuffle; i++)
            {
                if ((candidates & (1u << i)) == 0) continue;
                roll -= EventWeight((VoyageEventKind)i);
                if (roll < 0) { kind = (VoyageEventKind)i; break; }
            }
            remainingEvents &= ~(1u << (int)kind);
            Event.Kind = kind;
            Event.Phase = VoyageEventPhase.Active;
            Event.Serial++;
            Event.Progress = 0;
            Event.Goal = kind == VoyageEventKind.LooseCargo ? 3 :
                kind == VoyageEventKind.GalleyRush || kind == VoyageEventKind.TicketAudit || kind == VoyageEventKind.EngineOverheat ? 2 : 1;
            Event.InspectedMask = 0;
            Event.CoolingTime = 0;
            Event.Remaining = State.Route == RouteProfile.StormPassage ? 24 : State.Route == RouteProfile.MarketRun ? 28 : EventDuration;
            Event.NextIn = 0;
            Event.TargetPassenger = -1;
            // Reservoir sampling keeps the target valid even if passengers are in the rescue state.
            int active = 0;
            for (int p = 0; p < MaxPassengers; p++)
            {
                if (!Passengers[p].Active || Passengers[p].Motion != PassengerMotion.OnDeck) continue;
                active++;
                if (NextEventRandom() % (uint)active == 0) Event.TargetPassenger = p;
            }
            if (kind == VoyageEventKind.GalleyRush) PrepareGalleyOrders();
            if (kind == VoyageEventKind.TicketAudit) Event.Goal = Mathf.Min(2, active);
            if (kind == VoyageEventKind.VipService)
            {
                Passengers[Event.TargetPassenger].Request = AvailableEventOrder();
                Passengers[Event.TargetPassenger].RequestTimer = 0;
            }
            if (kind == VoyageEventKind.FuseOverload)
            {
                CutPower();
                Event.Goal = 3;
                Event.Progress = Encounter.PowerRepairSteps;
            }
        }

        private void ResolveEvent()
        {
            if (Event.Phase != VoyageEventPhase.Active) return;
            Event.Phase = VoyageEventPhase.Resolved;
            Event.Progress = Event.Goal;
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
            else if (Event.Kind == VoyageEventKind.FuseOverload) DamageHull(8);
            else if (Event.Kind == VoyageEventKind.EngineOverheat) { DamageHull(15); State.Speed = 0; }
            else if (Event.Kind == VoyageEventKind.VipService || Event.Kind == VoyageEventKind.SeatShuffle)
            {
                if (Event.TargetPassenger >= 0)
                    Passengers[Event.TargetPassenger].Satisfaction = Mathf.Max(0, Passengers[Event.TargetPassenger].Satisfaction - 20);
            }
            else if (Event.Kind == VoyageEventKind.TicketAudit)
            {
                for (int i = 0; i < MaxPassengers; i++)
                    if (Passengers[i].Active) Passengers[i].Satisfaction = Mathf.Max(0, Passengers[i].Satisfaction - 8);
            }
            Event.Remaining = 0;
            Event.NextIn = EventGap;
        }

        private string InspectPassengerForEvent(int passenger)
        {
            if (Event.Phase != VoyageEventPhase.Active) return null;
            if (Event.Kind == VoyageEventKind.TicketAudit)
            {
                int bit = 1 << passenger;
                if ((Event.InspectedMask & bit) != 0) return " This passenger is already audited.";
                Event.InspectedMask |= bit;
                Event.Progress++;
                if (Event.Progress >= Event.Goal) { ResolveEvent(); return " Ticket audit complete! +15 on arrival."; }
                return $" Audit progress: {Event.Progress}/{Event.Goal} different passengers.";
            }
            if (Event.Kind != VoyageEventKind.ContrabandInspection) return null;
            if (passenger != Event.TargetPassenger) return " No contraband here; keep checking passengers.";
            Event.Progress = Event.Goal;
            ResolveEvent();
            return " Contraband found! Crisis solved; +15 event bonus on arrival.";
        }

        private void RecordServiceForEvent(int passenger)
        {
            if (Event.Phase != VoyageEventPhase.Active) return;
            if (Event.Kind == VoyageEventKind.VipService && passenger == Event.TargetPassenger)
            { Event.Progress = Event.Goal; ResolveEvent(); return; }
            if (Event.Kind != VoyageEventKind.GalleyRush) return;
            Event.Progress++;
            if (Event.Progress >= Event.Goal) ResolveEvent();
        }

        private string ClearLooseCargo()
        {
            if (Event.Phase != VoyageEventPhase.Active) return null;
            if (Event.Kind == VoyageEventKind.EngineOverheat)
            {
                if (State.Speed > 1) return "Engine too hot! Ask the captain to slow below 1 m/s.";
                Event.Progress++;
                if (Event.Progress < Event.Goal) return $"Venting engine: {Event.Progress}/{Event.Goal}";
                ResolveEvent();
                return "Engine cooled! +15 event bonus on arrival.";
            }
            if (Event.Kind != VoyageEventKind.LooseCargo) return null;
            Event.Progress++;
            if (Event.Progress < Event.Goal) return $"Securing loose cargo: {Event.Progress}/{Event.Goal}";
            ResolveEvent();
            return "Cargo secured! +15 event bonus on arrival.";
        }

        private void PowerRepairedForEvent()
        {
            if (Event.Phase != VoyageEventPhase.Active || Event.Kind != VoyageEventKind.FuseOverload) return;
            Event.Progress = Encounter.PowerRepairSteps;
            if (Event.Progress >= Event.Goal) ResolveEvent();
        }

        private bool PassengerPlacedForEvent(int passenger)
        {
            if (Event.Phase != VoyageEventPhase.Active || Event.Kind != VoyageEventKind.SeatShuffle || passenger != Event.TargetPassenger) return false;
            var p = Passengers[passenger].Position;
            if (Vector2.Distance(new Vector2(p.x, p.z), Seats[passenger]) < 1.5f) return false;
            Event.Progress = Event.Goal;
            ResolveEvent();
            return true;
        }
    }
}
