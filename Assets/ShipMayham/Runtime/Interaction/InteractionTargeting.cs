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
            if (!crew.OnShore)
            {
                FindStation(session, crew, eye, forward, ref best, ref target);
                FindPassenger(session, crew, eye, forward, ref best, ref target);
            }
            FindHarbor(session, crew, eye, forward, ref best, ref target);
            return target;
        }

        private static void FindStation(PrototypeSession session, in CrewState crew, Vector3 eye, Vector3 forward, ref float best, ref InteractionTarget target)
        {
            for (int i = 0; i < session.Stations.Length; i++)
            {
                var station = session.Stations[i];
                if (Vector2.Distance(crew.Position, station.DeckPosition(session.ShipRoot)) > VoyageSimulation.InteractionRange) continue;
                float angle = Vector3.Angle(forward, station.InteractionAnchor.position + Vector3.up * .65f - eye);
                if (angle >= best) continue;
                best = angle;
                target = InteractionTarget.Station(i, PromptCatalog.ForStation(station.Kind, session.Voyage.Value, session.Encounter.Value, crew));
            }
        }

        private static void FindPassenger(PrototypeSession session, in CrewState crew, Vector3 eye, Vector3 forward, ref float best, ref InteractionTarget target)
        {
            for (int i = 0; i < session.Passengers.Count; i++)
            {
                var passenger = session.Passengers[i];
                if (!passenger.Active || passenger.Motion != PassengerMotion.OnDeck) continue;
                Vector3 p = passenger.Position;
                if (Vector2.Distance(crew.Position, new Vector2(p.x, p.z)) > VoyageSimulation.InteractionRange) continue;
                float angle = Vector3.Angle(forward, session.ShipRoot.TransformPoint(p + Vector3.up) - eye);
                if (angle >= best) continue;
                best = angle;
                target = InteractionTarget.Passenger(i, PromptCatalog.ForPassenger(i, passenger));
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
                float angle = Vector3.Angle(forward, harbor.Point(action) + Vector3.up * .7f - eye);
                if (angle >= best) continue;
                best = angle;
                target = InteractionTarget.Harbor(action, PromptCatalog.ForHarbor(action, voyage, crew));
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
                target = InteractionTarget.Salvage(index, PromptCatalog.ForSalvage(index));
            }
        }
    }
}
