using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Decides what a crew member can act on from where they stand and look. The eye position and direction are
    /// plain inputs, so nothing here needs a camera or a view.
    /// </summary>
    public static class InteractionTargeting
    {
        /// <summary>Widest angle from the look direction at which an object still counts as looked at.</summary>
        public const float MaxViewAngle = 24f;

        public static InteractionTarget Find(PrototypeSession session, in CrewState crew, Vector3 eye, Vector3 forward)
        {
            if (crew.AtCannon) return InteractionTarget.Context(PromptCatalog.ForCannon());
            if (crew.AtHelm) return InteractionTarget.Context(PromptCatalog.ForHelm());
            if (crew.AtCrane) return InteractionTarget.Context(PromptCatalog.ForCrane());
            if (session.LocalCarriedPassengerIndex >= 0) return InteractionTarget.Context(PromptCatalog.ForCarriedPassenger());
            if (crew.JumpHeight > 0 || crew.VerticalSpeed > 0) return InteractionTarget.Context(PromptCatalog.ForAirborne());

            float best = MaxViewAngle;
            var target = InteractionTarget.None;
            FindLadder(session, crew, eye, forward, ref best, ref target);
            if (crew.Swimming)
                return target.IsNone ? InteractionTarget.Context(PromptCatalog.ForSwimming(session.Voyage.Value.Phase == VoyagePhase.Sailing)) : target;
            if (!crew.OnShore)
            {
                FindStation(session, crew, eye, forward, ref best, ref target);
                FindPassenger(session, crew, eye, forward, ref best, ref target);
                DeckPoint(session, crew, eye, forward, VoyageSimulation.PlugLocker, DeckAction.TakePlug,
                    InteractionPrompt.Target("Temporary plug locker", "Take / return plug", "Carry to REPAIR for a 20-second hold"), ref best, ref target);
                if (session.Opportunity.Value.ChestAvailable && Vector2.Distance(session.Voyage.Value.Position, session.Opportunity.Value.ChestPosition) <= 15)
                    DeckPoint(session, crew, eye, forward, VoyageSimulation.RescueRope, DeckAction.HaulChest,
                        InteractionPrompt.Target("Recovery rope", "Haul sea chest", "Below 1 m/s; +30 on successful arrival"), ref best, ref target);
                if (session.Emergency.Value.Fire == FirePhase.Burning &&
                    Vector2.Distance(crew.Position, VoyageSimulation.FirePoint) <= VoyageSimulation.InteractionRange)
                {
                    Vector3 fire = session.ShipRoot.TransformPoint(new Vector3(VoyageSimulation.FirePoint.x, 1.2f, VoyageSimulation.FirePoint.y));
                    float angle = Vector3.Angle(forward, fire - eye);
                    if (angle < best) { best = angle; target = InteractionTarget.DeckTask(DeckAction.Extinguish,
                        InteractionPrompt.Target("Electrical fire", "Spray extinguisher", "R at KITS equips it. R at POWER isolates the circuit."), fire); }
                }
                var deck = session.Deck.Value;
                if (deck.Spill && Vector2.Distance(crew.Position, deck.SpillPosition) <= VoyageSimulation.InteractionRange)
                {
                    Vector3 point = session.ShipRoot.TransformPoint(new Vector3(deck.SpillPosition.x, .55f, deck.SpillPosition.y));
                    float angle = Vector3.Angle(forward, point - eye);
                    if (angle < best) { best = angle; target = InteractionTarget.DeckTask(DeckAction.CleanSpill,
                        InteractionPrompt.Target("Spilled drink", "Mop floor", $"Progress {deck.CleanSteps}/3. Empty hands required"), point); }
                }
            }
            FindHarbor(session, crew, eye, forward, ref best, ref target);
            if (crew.OnShore) FindActivity(session, crew, eye, forward, ref best, ref target);
            return target;
        }

        private static readonly MiniGameKind[] ShoreGames = { MiniGameKind.Lockpick, MiniGameKind.Dig, MiniGameKind.Fishing };

        /// <summary>Chests to open, sand to dig and quay edges to fish from.</summary>
        private static void FindActivity(PrototypeSession session, in CrewState crew, Vector3 eye, Vector3 forward, ref float best, ref InteractionTarget target)
        {
            var voyage = session.Voyage.Value;
            if (session.Harbor == null || !session.Harbor.IsOpen) return;
            foreach (var kind in ShoreGames)
            {
                var spots = VoyageSimulation.SpotsOf(kind);
                for (int i = 0; i < spots.Length; i++)
                {
                    bool fresh = kind == MiniGameKind.Lockpick ? (voyage.ChestMask & (1 << i)) == 0 :
                        kind == MiniGameKind.Dig ? (voyage.DigMask & (1 << i)) == 0 : true;
                    if (!fresh || Vector2.Distance(crew.Position, spots[i]) > VoyageSimulation.ActivityReach) continue;
                    Vector3 point = VoyageSimulation.HarborOrigin(voyage) + new Vector3(spots[i].x, VoyageSimulation.ShoreHeight + .5f, spots[i].y);
                    float angle = Vector3.Angle(forward, point - eye);
                    if (angle >= best) continue;
                    best = angle;
                    target = InteractionTarget.Activity(kind, i, PromptCatalog.ForActivity(kind, crew), point);
                }
            }
        }

        private static void DeckPoint(PrototypeSession session, in CrewState crew, Vector3 eye, Vector3 forward, Vector2 spot,
            DeckAction action, InteractionPrompt prompt, ref float best, ref InteractionTarget target)
        {
            if (Vector2.Distance(crew.Position, spot) > VoyageSimulation.InteractionRange) return;
            Vector3 point = session.ShipRoot.TransformPoint(new Vector3(spot.x, .8f, spot.y));
            float angle = Vector3.Angle(forward, point - eye);
            if (angle >= best) return;
            best = angle; target = InteractionTarget.DeckTask(action, prompt, point);
        }

        private static void FindStation(PrototypeSession session, in CrewState crew, Vector3 eye, Vector3 forward, ref float best, ref InteractionTarget target)
        {
            for (int i = 0; i < session.Stations.Length; i++)
            {
                var station = session.Stations[i];
                // Ship upgrades are bought from the purser, not at the upgrade fittings.
                if (VoyageSimulation.IsUpgrade(station.Kind)) continue;
                if (Vector2.Distance(crew.Position, station.DeckPosition(session.ShipRoot)) > VoyageSimulation.InteractionRange) continue;
                Vector3 anchor = station.InteractionAnchor.position + Vector3.up * .65f;
                float angle = Vector3.Angle(forward, anchor - eye);
                if (angle >= best) continue;
                best = angle;
                target = InteractionTarget.Station(i, PromptCatalog.ForStation(station.Kind, session.Voyage.Value, session.Encounter.Value, crew), anchor);
                if (station.Kind == StationKind.RepairSupply)
                    target = InteractionTarget.Station(i, InteractionPrompt.Target("Safety supplies", "Take / return patch kit",
                        "R / D-pad down: take / return extinguisher"), anchor);
                if (station.Kind == StationKind.Power && session.Emergency.Value.Fire == FirePhase.Burning)
                    target = InteractionTarget.Station(i, InteractionPrompt.Target("Fire circuit", "Isolate fire damage",
                        "Service counters stop until the fire is extinguished", key: HudControl.Special), anchor);
                if (station.Kind == StationKind.Repair && session.Voyage.Value.Leak != 0 && session.Voyage.Value.ReadyRepairKits == 0 &&
                    !session.Emergency.Value.ValveAdjusted && crew.Carrying == ServiceItem.None)
                    target = InteractionTarget.Station(i, PromptCatalog.ForHammer(session.Voyage.Value), anchor);
                else if (station.Kind == StationKind.Repair && session.Voyage.Value.Leak != 0)
                    target = InteractionTarget.Station(i, InteractionPrompt.Target("Repair / valves", "Patch or apply selected valve",
                        $"R: cycle A/B/C. Hot pipe: {(char)('A' + session.Hazards.Value.Seed % 3)}. Selected: {(char)('A' + session.Emergency.Value.SelectedValve)}"), anchor);
            }
        }

        /// <summary>The ladder within reach: its top for someone standing on the deck or quay, its bottom for a swimmer.</summary>
        private static void FindLadder(PrototypeSession session, in CrewState crew, Vector3 eye, Vector3 forward, ref float best, ref InteractionTarget target)
        {
            if (crew.AtHelm || crew.AtCannon || crew.AtCrane) return;
            var voyage = session.Voyage.Value;
            for (int i = 0; i < VoyageSimulation.Ladders.Length; i++)
            {
                var ladder = VoyageSimulation.Ladders[i];
                Vector2 reach = crew.Swimming ? ladder.Base : ladder.Top;
                if (!crew.Swimming && ladder.OnQuay != crew.OnShore) continue;
                if (Vector2.Distance(crew.Position, reach) > VoyageSimulation.LadderReach) continue;
                Vector3 point = crew.Swimming
                    ? session.ShipRoot.TransformPoint(new Vector3(ladder.Base.x, VoyageSimulation.SeaLevel + .9f, ladder.Base.y))
                    : ladder.OnQuay ? VoyageSimulation.HarborOrigin(voyage) + new Vector3(ladder.Top.x, VoyageSimulation.ShoreHeight + .7f, ladder.Top.y)
                    : session.ShipRoot.TransformPoint(new Vector3(ladder.Top.x, .5f + .7f, ladder.Top.y));
                float angle = Vector3.Angle(forward, point - eye);
                if (angle >= best) continue;
                best = angle;
                target = InteractionTarget.Ladder(i, PromptCatalog.ForLadder(ladder, crew, session.Harbor != null && session.Harbor.IsOpen), point);
            }
        }

        private static void FindPassenger(PrototypeSession session, in CrewState crew, Vector3 eye, Vector3 forward, ref float best, ref InteractionTarget target)
        {
            for (int i = 0; i < session.Passengers.Count; i++)
            {
                var passenger = session.Passengers[i];
                if (!passenger.Active || !VoyageSimulation.CanBeLifted(passenger.Motion)) continue;
                Vector3 p = passenger.Position;
                if (Vector2.Distance(crew.Position, new Vector2(p.x, p.z)) > VoyageSimulation.InteractionRange) continue;
                Vector3 body = session.ShipRoot.TransformPoint(p + Vector3.up);
                float angle = Vector3.Angle(forward, body - eye);
                if (angle >= best) continue;
                best = angle;
                target = InteractionTarget.Passenger(i, PromptCatalog.ForPassenger(i, passenger), body);
            }
        }

        private static void FindHarbor(PrototypeSession session, in CrewState crew, Vector3 eye, Vector3 forward, ref float best, ref InteractionTarget target)
        {
            var harbor = session.Harbor;
            if (harbor == null || !harbor.IsOpen) return;
            var voyage = session.Voyage.Value;
            // Aboard, only the gangway can be used; ashore, every harbor station.
            int first = crew.OnShore ? 1 : 0, last = crew.OnShore ? HarborTownLayout.StationsPerPort : 0;
            for (int i = first; i <= last; i++)
            {
                var action = (HarborAction)i;
                if (Vector2.Distance(crew.Position, VoyageSimulation.HarborPoint(action)) > VoyageSimulation.InteractionRange) continue;
                Vector3 spot = harbor.Point(action) + Vector3.up * .7f;
                float angle = Vector3.Angle(forward, spot - eye);
                if (angle >= best) continue;
                best = angle;
                target = InteractionTarget.Harbor(action, PromptCatalog.ForHarbor(action, voyage, crew), spot);
            }
            if (!crew.OnShore || !voyage.DockJobActive || voyage.DockJobType != DockJobKind.SalvageRun) return;
            for (int index = 0; index < HarborTownLayout.SalvageCount; index++)
            {
                if ((voyage.DockJobDeliveredMask & (1 << index)) != 0) continue;
                ServiceItem item = (ServiceItem)((int)ServiceItem.SalvageA + index);
                bool held = false;
                foreach (var member in session.Crew) if (member.Active && member.Carrying == item) held = true;
                Vector2 spot = HarborTownLayout.SalvagePoint(index);
                if (held || Vector2.Distance(crew.Position, spot) > VoyageSimulation.InteractionRange) continue;
                Vector3 point = VoyageSimulation.HarborOrigin(voyage) + new Vector3(spot.x, 1, spot.y);
                float angle = Vector3.Angle(forward, point - eye);
                if (angle >= best) continue;
                best = angle;
                target = InteractionTarget.Salvage(index, PromptCatalog.ForSalvage(index), point);
            }
        }
    }
}
