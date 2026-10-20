using UnityEngine;

namespace ShipMayham
{
    /// <summary>Deterministic server-owned encounters; visuals never decide damage or payment.</summary>
    public sealed partial class VoyageSimulation
    {
        public const float CannonRange = 90, ReloadSeconds = 2.5f;
        public EncounterState Encounter;
        private bool scheduledPowerFault;
        private int CannonStation => System.Array.FindIndex(Stations, s => s.Kind == StationKind.Cannon);

        private void ResetEncounter()
        {
            Encounter = new EncounterState { Ammo = 12, CannonLoaded = true, CannonYaw = 45, PirateHealth = 100 };
            scheduledPowerFault = false;
        }

        public static Vector3 DeckToWorld(VoyageState state, Vector3 local) =>
            new Vector3(state.Position.x, 0, state.Position.y) + Quaternion.Euler(0, state.Heading, 0) * local;

        public static Vector3 AimDirection(float heading, Vector2 aim) => Quaternion.Euler(aim.y, heading + aim.x, 0) * Vector3.forward;

        public static bool IsUpgrade(StationKind kind) => kind == StationKind.EngineUpgrade || kind == StationKind.CapacityUpgrade || kind == StationKind.ComfortUpgrade;

        private void TickEncounter(float dt)
        {
            // Old authored scenes without the new stations remain playable, without invisible threats.
            if (CannonStation < 0) return;
            if (Encounter.ReloadRemaining > 0)
            {
                Encounter.ReloadRemaining = Mathf.Max(0, Encounter.ReloadRemaining - dt);
                if (Encounter.ReloadRemaining == 0) Encounter.CannonLoaded = true;
            }
            if (!scheduledPowerFault && State.Elapsed >= 22)
            {
                scheduledPowerFault = true;
                CutPower();
            }
            if (Encounter.Pirate == PiratePhase.Waiting && State.Elapsed >= 12)
            {
                Encounter.Pirate = PiratePhase.Warning;
                Encounter.WarningRemaining = 6;
                Vector3 spawn = DeckToWorld(State, new Vector3(32, 0, 32));
                Encounter.PiratePosition = new Vector2(spawn.x, spawn.z);
            }
            if (Encounter.Pirate == PiratePhase.Warning)
            {
                Encounter.WarningRemaining = Mathf.Max(0, Encounter.WarningRemaining - dt);
                if (Encounter.WarningRemaining > 0) return;
                Encounter.Pirate = PiratePhase.Attacking;
                Encounter.AttackRemaining = 40;
                Encounter.EnemyShotRemaining = 8;
            }
            if (Encounter.Pirate != PiratePhase.Attacking) return;
            Vector3 escort = DeckToWorld(State, new Vector3(26, 0, 25));
            Encounter.PiratePosition = Vector2.MoveTowards(Encounter.PiratePosition, new Vector2(escort.x, escort.z), dt * 7);
            Encounter.AttackRemaining = Mathf.Max(0, Encounter.AttackRemaining - dt);
            if (Encounter.AttackRemaining == 0) { Encounter.Pirate = PiratePhase.Escaped; return; }
            Encounter.EnemyShotRemaining = Mathf.Max(0, Encounter.EnemyShotRemaining - dt);
            if (Encounter.EnemyShotRemaining > 0 || Vector2.Distance(Encounter.PiratePosition, State.Position) > 70) return;
            Encounter.EnemyShotRemaining = 8;
            Encounter.PirateShotsFired++;
            Encounter.EnemyShotFrom = new Vector3(Encounter.PiratePosition.x, 1.5f, Encounter.PiratePosition.y);
            Encounter.EnemyShotTo = DeckToWorld(State, new Vector3(4, 1, -3));
            DamageHull(8);
            // The first hit creates work for the engineer as well as the gunner.
            if (Encounter.PirateShotsFired == 1) CutPower();
        }

        private void CutPower()
        {
            if (System.Array.FindIndex(Stations, s => s.Kind == StationKind.Power) < 0 || Encounter.PowerOut) return;
            Encounter.PowerOut = true;
            Encounter.PowerRepairSteps = 0;
        }

        private string TakeCannon(int slot)
        {
            if (State.Phase != VoyagePhase.Boarding && State.Phase != VoyagePhase.Sailing) return "Cannon is closed. Prepare a new voyage.";
            foreach (var crew in Crew) if (crew.Active && crew.AtCannon) return "Cannon occupied.";
            if (Crew[slot].Carrying != ServiceItem.None) return "Serve or return your item before using the cannon.";
            Crew[slot].AtCannon = true;
            Crew[slot].Position = Stations[CannonStation].Position + Vector2.down * 1.3f;
            inputs[slot] = Vector2.zero;
            return "Cannon: mouse/right stick aims, LMB/RT fires, R/X reloads, E/A releases.";
        }

        private void ReleaseCannon(int slot)
        {
            Crew[slot].AtCannon = false;
            Encounter.ReloadRemaining = 0; // An unfinished reload must be restarted by the next operator.
            inputs[slot] = Vector2.zero;
        }

        private bool CanOperateCannon(ulong clientId)
        {
            int slot = FindCrew(clientId);
            return slot >= 0 && Crew[slot].AtCannon && CannonStation >= 0 &&
                Vector2.Distance(Crew[slot].Position, Stations[CannonStation].Position) <= InteractionRange;
        }

        public void AimCannon(ulong clientId, Vector2 aim)
        {
            if (!CanOperateCannon(clientId) || !Finite(aim.x) || !Finite(aim.y)) return;
            Encounter.CannonYaw = Mathf.Repeat(aim.x + 180, 360) - 180;
            Encounter.CannonPitch = Mathf.Clamp(aim.y, -25, 45);
        }

        public string ReloadCannon(ulong clientId)
        {
            if (!CanOperateCannon(clientId)) return "Use the cannon first.";
            if (State.Phase != VoyagePhase.Sailing) return "Depart before loading the cannon.";
            if (Encounter.CannonLoaded || Encounter.ReloadRemaining > 0) return "Cannon is already loaded or reloading.";
            if (Encounter.Ammo <= 0) return "No ammunition. A new voyage restocks 12 shots.";
            Encounter.ReloadRemaining = ReloadSeconds;
            return "Reloading cannon...";
        }

        public string FireCannon(ulong clientId, Vector2 aim)
        {
            if (!CanOperateCannon(clientId)) return "Use the cannon first.";
            if (!Finite(aim.x) || !Finite(aim.y)) return "Invalid aim.";
            if (State.Phase != VoyagePhase.Sailing) return "Cannon firing is disabled at port.";
            if (!Encounter.CannonLoaded || Encounter.ReloadRemaining > 0) return "Press R / X to reload.";
            if (Encounter.Ammo <= 0) return "No ammunition.";
            AimCannon(clientId, aim);
            Vector3 direction = AimDirection(State.Heading, new Vector2(Encounter.CannonYaw, Encounter.CannonPitch));
            Vector2 station = Stations[CannonStation].Position;
            Vector3 origin = DeckToWorld(State, new Vector3(station.x, 1.8f, station.y)) + direction * 1.5f;
            Encounter.CannonLoaded = false;
            Encounter.Ammo--;
            Encounter.ShotsFired++;
            Encounter.ShotFrom = origin;
            Encounter.ShotTo = origin + direction * CannonRange;
            if (Encounter.Pirate != PiratePhase.Attacking) return "Shot fired. No active target hit.";
            Vector3 target = new Vector3(Encounter.PiratePosition.x, 1.5f, Encounter.PiratePosition.y);
            float alongRay = Vector3.Dot(target - origin, direction);
            if (alongRay < 0 || alongRay > CannonRange || Vector3.Distance(origin + direction * alongRay, target) > 3.5f)
                return "Miss! Reload and adjust your aim.";
            Encounter.ShotTo = origin + direction * alongRay;
            Encounter.Hits++;
            Encounter.PirateHealth = Mathf.Max(0, Encounter.PirateHealth - 34);
            if (Encounter.PirateHealth > 0) return $"Hit! Pirate hull: {Encounter.PirateHealth:0}%";
            Encounter.Pirate = PiratePhase.Defeated;
            Encounter.Bounty = 50;
            return "Pirates repelled! +50 bounty if you reach port.";
        }
    }
}
