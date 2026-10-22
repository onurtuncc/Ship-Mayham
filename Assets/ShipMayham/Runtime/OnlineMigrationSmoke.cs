#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>Opt-in Relay/WSS integration probe. Ordinary play never installs this component.</summary>
    public sealed class OnlineMigrationSmoke : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public string Participant;
            public bool NewHost;
            public int OriginalSlot, RestoredSlot, Migrations, Wallet, EventSerial, ActiveCrew, Ammo, MagazineAmmo, ReadyAmmo;
        }

        private PrototypeHud hud;
        private PrototypeSession session;
        private string output, participant;
        private float deadline;
        private bool finished;
        private bool observingMigration;
        private float nextDiagnostic;
        private string lastDiagnostic;

        private void Awake() => DontDestroyOnLoad(gameObject);

        private void WriteSignal(string name, string contents)
        {
            string target = Path.Combine(output, name);
            File.WriteAllText(target + ".pending", contents);
            File.Move(target + ".pending", target);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--ship-online-host") < 0 && Array.IndexOf(args, "--ship-online-client") < 0) return;
            new GameObject("Development_OnlineMigrationSmoke").AddComponent<OnlineMigrationSmoke>();
        }

        private static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        private IEnumerator Start()
        {
            if (!int.TryParse(Argument("--ship-online-timeout") ?? "240", out int timeoutSeconds) || timeoutSeconds < 60 || timeoutSeconds > 300)
            { Fail("Invalid online test timeout."); yield break; }
            deadline = Time.realtimeSinceStartup + timeoutSeconds;
            participant = Argument("--ship-online-participant");
            output = Argument("--ship-online-output");
            string profile = Argument("--ship-auth-profile");
            if (string.IsNullOrEmpty(output) || string.IsNullOrEmpty(participant) ||
                !Regex.IsMatch(participant, "^[a-z0-9_-]{1,20}$") || string.IsNullOrEmpty(profile))
            { Fail("Missing or invalid test arguments."); yield break; }
            output = Path.GetFullPath(output);
            Directory.CreateDirectory(output);
            hud = FindAnyObjectByType<PrototypeHud>();
            session = FindAnyObjectByType<PrototypeSession>();
            if (hud == null || session == null) { Fail("Missing scene references."); yield break; }
            hud.View.enabled = false;
            bool host = Array.IndexOf(Environment.GetCommandLineArgs(), "--ship-online-host") >= 0;
            string codePath = Path.Combine(output, "join-code.txt");
            string code = null;
            if (!host)
            {
                while (!File.Exists(codePath)) yield return null;
                code = File.ReadAllText(codePath).Trim();
            }
            hud.ConnectOnline(host, code, profile);
            while (!session.IsSpawned || session.LocalCrewIndex < 0 || string.IsNullOrEmpty(hud.OnlineCode)) yield return null;
            int originalSlot = session.LocalCrewIndex;
            Debug.Log($"SHIP_ONLINE_CONNECTED {participant} slot={originalSlot}");

            if (host)
            {
                WriteSignal("join-code.txt", hud.OnlineCode);
                while (ActiveCrew() != 4) yield return null;
                var sim = session.Simulation;
                sim.Crew[originalSlot].Position = sim.Stations[0].Position + Vector2.right;
                session.InteractRpc(0, false);
                yield return new WaitForSeconds(.4f);
                sim.Crew[originalSlot].Position = sim.Stations[1].Position + Vector2.down;
                session.InteractRpc(1, false);
                session.InteractRpc(-1, true);
                yield return new WaitForSeconds(.4f);
                session.InteractRpc(-1, false);
                yield return new WaitForSeconds(.4f);
                int ammoStation = Array.FindIndex(session.Stations, station => station.Kind == StationKind.Ammo);
                if (ammoStation < 0) { Fail("Ammo station is missing."); yield break; }
                sim.Crew[originalSlot].Position = sim.Stations[ammoStation].Position + Vector2.left;
                session.InteractRpc(ammoStation, false);
                yield return new WaitForSeconds(.4f);
                if (session.Crew[originalSlot].Carrying != ServiceItem.Ammo) { Fail("Host did not pick up the probe cannonball."); yield break; }
                sim.State.Wallet = 321;
                sim.State.Reputation = 9;
                sim.State.Position = new Vector2(12, 45);
                sim.Encounter.Pirate = PiratePhase.Defeated;
                sim.Passengers[0].TicketChecked = true;
                sim.State.CheckedTickets = 1;
                sim.Event = new VoyageEventState
                {
                    Kind = VoyageEventKind.TicketAudit, Phase = VoyageEventPhase.Active,
                    Serial = 777, Progress = 1, Goal = 2, InspectedMask = 1,
                    TargetPassenger = 2, Remaining = 999, Reward = 30
                };
                yield return new WaitForSeconds(8); // Allow several SDK migration uploads.
                if (session.Voyage.Value.Phase != VoyagePhase.Sailing) { Fail("Probe voyage did not depart."); yield break; }
                WriteSignal("host-ready.txt", "snapshot-ready");
                Debug.Log("SHIP_ONLINE_HOST_READY");
                while (true) yield return null; // Supervisor deliberately terminates this exact test process.
            }

            while (ActiveCrew() != 4 || session.ActiveEvent.Value.Serial != 777) yield return null;
            WriteSignal(participant + ".ready", "observed-probe");
            observingMigration = true;
            while (hud == null || session == null || hud.CompletedMigrations == 0 || !session.IsSpawned || session.LocalCrewIndex < 0 || ActiveCrew() != 3) yield return null;
            int restoredSlot = session.LocalCrewIndex;
            if (restoredSlot != originalSlot || session.Voyage.Value.Wallet != 321 || session.ActiveEvent.Value.Serial != 777 ||
                session.ActiveEvent.Value.Progress != 1 || session.ActiveEvent.Value.InspectedMask != 1 || session.ActiveEvent.Value.Reward != 30 ||
                session.Voyage.Value.Phase != VoyagePhase.Sailing || session.Encounter.Value.Ammo != 12 ||
                session.Encounter.Value.MagazineAmmo != 11 || session.Encounter.Value.ReadyAmmo != 0 || !session.Encounter.Value.CannonLoaded)
            {
                Fail($"Migration mismatch: slot {originalSlot}->{restoredSlot}, wallet={session.Voyage.Value.Wallet}, " +
                    $"event={session.ActiveEvent.Value.Serial}/{session.ActiveEvent.Value.Progress}, phase={session.Voyage.Value.Phase}, " +
                    $"ammo={session.Encounter.Value.Ammo}/{session.Encounter.Value.MagazineAmmo}/{session.Encounter.Value.ReadyAmmo}.");
                yield break;
            }

            bool jumped = false;
            session.JumpRpc();
            float jumpDeadline = Time.realtimeSinceStartup + 3;
            while (Time.realtimeSinceStartup < jumpDeadline)
            {
                if (session.LocalCrewIndex >= 0 && session.Crew[session.LocalCrewIndex].JumpHeight > .05f) { jumped = true; break; }
                yield return null;
            }
            if (!jumped) { Fail("Post-migration command did not reach the new authority."); yield break; }
            var result = new Result
            {
                Participant = participant, NewHost = hud.IsOnlineHost, OriginalSlot = originalSlot,
                RestoredSlot = restoredSlot, Migrations = hud.CompletedMigrations,
                Wallet = session.Voyage.Value.Wallet, EventSerial = session.ActiveEvent.Value.Serial, ActiveCrew = ActiveCrew(),
                Ammo = session.Encounter.Value.Ammo, MagazineAmmo = session.Encounter.Value.MagazineAmmo, ReadyAmmo = session.Encounter.Value.ReadyAmmo
            };
            WriteSignal(participant + ".json", JsonUtility.ToJson(result, true));
            Debug.Log($"SHIP_ONLINE_SMOKE_PASS {participant} host={result.NewHost} slot={restoredSlot}");
            finished = true;
            // A restored host must remain alive until every peer has validated its
            // rebind and command. A fixed delay can terminate a healthy authority
            // while a slower Relay peer is still reconnecting.
            while (!File.Exists(Path.Combine(output, "all-clients-passed.txt")))
            {
                if (Time.realtimeSinceStartup > deadline) { finished = false; Fail("Timed out waiting for the supervisor completion barrier."); yield break; }
                yield return null;
            }
            yield return new WaitForSeconds(1);
            Application.Quit(0);
        }

        private int ActiveCrew()
        {
            int active = 0;
            if (session == null || !session.IsSpawned) return active;
            foreach (var member in session.Crew) if (member.Active) active++;
            return active;
        }

        private void Update()
        {
            if (hud == null) hud = FindAnyObjectByType<PrototypeHud>();
            if (session == null) session = FindAnyObjectByType<PrototypeSession>();
            if (observingMigration && !finished && Time.realtimeSinceStartup >= nextDiagnostic)
            {
                nextDiagnostic = Time.realtimeSinceStartup + .5f;
                bool spawned = session != null && session.IsSpawned;
                string state = $"host={hud != null && hud.IsOnlineHost}, migrations={hud?.CompletedMigrations}, " +
                    $"network={hud?.OnlineNetworkState}, error={hud?.LastMigrationError}, " +
                    $"spawned={spawned}, local={(spawned ? session.LocalCrewIndex : -1)}, active={ActiveCrew()}";
                if (spawned)
                {
                    state += $", ammo={session.Encounter.Value.Ammo}/{session.Encounter.Value.MagazineAmmo}/{session.Encounter.Value.ReadyAmmo}";
                    for (int i = 0; i < session.Crew.Count; i++)
                        state += $", slot{i}={session.Crew[i].Active}:{session.Crew[i].ClientId}:{session.Crew[i].Carrying}";
                }
                if (state != lastDiagnostic) { lastDiagnostic = state; Debug.Log($"SHIP_ONLINE_STATE {participant}: {state}"); }
            }
            if (!finished && observingMigration && hud != null && hud.LastMigrationError.HasValue)
                Fail("SDK reported migration failure: " + hud.LastMigrationError.Value);
            if (!finished && deadline > 0 && Time.realtimeSinceStartup > deadline) Fail("Timed out waiting for online migration.");
        }

        private void Fail(string message)
        {
            if (finished) return;
            finished = true;
            Debug.LogError($"SHIP_ONLINE_SMOKE_FAIL {participant}: {message}");
            Application.Quit(2);
        }
    }
}
#endif
