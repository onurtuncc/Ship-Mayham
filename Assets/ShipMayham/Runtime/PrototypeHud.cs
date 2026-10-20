using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace ShipMayham
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        public NetworkManager Manager;
        public PrototypeSession Session;
        public PrototypeView View;
        private string address = "127.0.0.1";
        private string status = "Host a session, or join the host's LAN IPv4 address.";
        private bool attempted, reloading;
        private readonly HashSet<ulong> admittedClients = new HashSet<ulong>();
        private GUIStyle title, body, crosshair;
        private bool showHelp;

        private void Update()
        {
            if (Keyboard.current?.f1Key.wasPressedThisFrame == true) showHelp = !showHelp;
        }

        private void Awake()
        {
            Application.runInBackground = true;
            Manager.ConnectionApprovalCallback = Approve;
            Manager.OnClientDisconnectCallback += Disconnected;
        }

        private void OnDestroy()
        {
            if (Manager != null)
            {
                Manager.OnClientDisconnectCallback -= Disconnected;
                Manager.ConnectionApprovalCallback = null;
            }
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = admittedClients.Contains(request.ClientNetworkId) || admittedClients.Count < VoyageSimulation.MaxCrew;
            if (response.Approved) admittedClients.Add(request.ClientNetworkId);
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = response.Approved ? "" : "The ship already has four crew members.";
        }

        private void Disconnected(ulong id)
        {
            admittedClients.Remove(id);
            if (id == Manager.LocalClientId) status = "Disconnected. " + Manager.DisconnectReason + " Reload to start a fresh connection.";
        }

        public void Connect(bool host, string hostAddress = null)
        {
            if (Manager.IsListening || attempted) return;
            attempted = true;
            if (hostAddress != null) address = hostAddress;
            // Crew live in scene slots; NGO must never inspect/spawn a player prefab here.
            // Unity's serialized empty references can be fake-null in the Editor. NGO 2.11
            // uses ?.GetComponent even when CreatePlayerObject is false, so assign real null
            // immediately before startup (the scene's {fileID: 0} alone is not sufficient).
            Manager.NetworkConfig.PlayerPrefab = null;
            Manager.NetworkConfig.AutoSpawnPlayerPrefabClientSide = false;
            Manager.GetComponent<UnityTransport>().SetConnectionData(address.Trim(), 7777, host ? "0.0.0.0" : null);
            bool started = host ? Manager.StartHost() : Manager.StartClient();
            status = started ? "Connecting..." : "Connection could not start. Reload and retry.";
        }

        private IEnumerator Reload()
        {
            reloading = true;
            Manager.Shutdown();
            while (Manager.IsListening || Manager.ShutdownInProgress) yield return null;
            Destroy(Manager.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }

        private void OnGUI()
        {
            if (reloading) return;
            title ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            body ??= new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            crosshair ??= new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 800f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.Box(new Rect(12, 12, 370, 340), GUIContent.none);
            GUILayout.BeginArea(new Rect(26, 24, 342, 316));
            GUILayout.Label("SHIP MAYHAM / BLOCKOUT", title);
            if (!Session.IsSpawned)
            {
                GUILayout.Label("1–4 crew • Direct LAN • Port 7777", body);
                GUILayout.Space(12);
                GUILayout.Label("Host IP (same machine: 127.0.0.1)");
                address = GUILayout.TextField(address, 64);
                GUI.enabled = !attempted;
                if (GUILayout.Button("HOST / SOLO", GUILayout.Height(36))) Connect(true);
                if (GUILayout.Button("JOIN", GUILayout.Height(36))) Connect(false);
                GUI.enabled = true;
                GUILayout.Label(status, body);
                if (attempted && GUILayout.Button("Cancel / reload")) StartCoroutine(Reload());
            }
            else
            {
                var s = Session.Voyage.Value;
                GUILayout.Label($"Voyage {s.Voyage} / {s.Phase}    Coins: {s.Wallet}", body);
                GUILayout.Label($"Hull {s.Health:0}% | Shield {s.HullShield:0} | {s.Speed:0.0} m/s", body);
                GUILayout.Label($"Destination: {Vector2.Distance(s.Position, VoyageSimulation.Destination):0} m    Heading: {s.Heading:0}°", body);
                GUILayout.Label($"Passengers: {s.Boarded}    Orders served: {s.Served}", body);
                GUILayout.Label($"Stock: {s.FoodStock} food / {s.DrinkStock} drink", body);
                GUILayout.Label($"Engine {s.EngineLevel}/2 | Seats {s.CapacityLevel}/2 | Comfort {s.ComfortLevel}/1", body);
                if (s.Leak != 0) GUILayout.Label($"LEAK! Repair station: {s.RepairSteps}/4 patches", body);
                else GUILayout.Label("Hull watertight", body);
                if (s.Phase == VoyagePhase.Arrived || s.Phase == VoyagePhase.Failed)
                    GUILayout.Label($"Tickets {s.LastTickets} + service {s.LastServices} + tips {s.LastTips}\nBounty {s.LastBounty} - costs {s.LastCosts} = net {s.LastNet}\nManifest: new voyage. Upgrades: spend coins.", body);
                else GUILayout.Label(s.Phase == VoyagePhase.Boarding ? "1. Manifest: board\n2. Helm: E, then Space to depart" : "Serve matching orders. Brake (S) and dock (Space) within 12 m.", body);
                if (GUILayout.Button("Leave session")) StartCoroutine(Reload());
            }
            GUILayout.EndArea();
            if (Session.IsSpawned)
            {
                DrawEncounter();
                if (Session.Harbor != null && Session.Harbor.IsOpen) DrawHarbor(); else DrawPassengers();
                GUI.Box(new Rect(240, 686, 800, 102), GUIContent.none);
                GUI.Label(new Rect(256, 696, 768, 48), View.FocusText, body);
                if (Time.unscaledTime < Session.NoticeUntil)
                {
                    Color previous = GUI.color;
                    GUI.color = new Color(1, 1, 1, Mathf.Clamp01((Session.NoticeUntil - Time.unscaledTime) / 2));
                    GUI.Label(new Rect(256, 746, 768, 36), Session.Notice, body);
                    GUI.color = previous;
                }
                GUI.Label(new Rect(400, 16, 650, 44), "WASD / stick: move | Mouse / right stick: look\nE / A: interact | Esc / Start: cursor | F1: controls", body);
                if (showHelp)
                {
                    GUI.Box(new Rect(400, 66, 650, 116), GUIContent.none);
                    GUI.Label(new Rect(412, 74, 626, 104), "Space / X: jump (helm: depart/dock)\nPassengers: F / Y check or collect • G / B lift or put down • LMB / RT throw\nCannon: LMB / RT fire • R / X reload • E / A release\nBlue = drink • Orange = food • Green = repair / gangway\nAll crew must return aboard before departure or starting a new voyage.", body);
                }
                if (View.IsFirstPerson)
                {
                    Color previous = GUI.color;
                    GUI.color = View.FocusedTarget != -1 ? Color.yellow : Color.white;
                    GUI.Label(new Rect(Screen.width / scale * .5f - 12, Screen.height / scale * .5f - 12, 24, 24), "+", crosshair);
                    GUI.color = previous;
                    int local = Session.LocalCrewIndex;
                    if (local >= 0) GUI.Label(new Rect(900, 656, 340, 25), "Carrying: " + (Session.LocalCarriedPassengerIndex >= 0 ? "Passenger " + (Session.LocalCarriedPassengerIndex + 1) : Session.Crew[local].Carrying.ToString()), body);
                    if (Cursor.lockState != CursorLockMode.Locked)
                        GUI.Label(new Rect(410, 610, 460, 28), "Esc / Start or click center to resume looking", body);
                }
                DrawMap();
            }
            GUI.matrix = Matrix4x4.identity;
        }

        private void DrawMap()
        {
            Rect map = new Rect(1070, 70, 190, 270);
            GUI.Box(map, "ROUTE / north up");
            var p = Session.Voyage.Value.Position;
            GUI.Label(new Rect(map.x + 70, map.y + 28, 110, 24), "DESTINATION");
            GUI.Label(new Rect(map.x + 70, map.y + 235, 110, 24), "DEPARTURE");
            float x = map.x + 90 + p.x * .75f;
            float y = map.y + 235 - p.y * .9f;
            GUI.Label(new Rect(x, y, 30, 25), "▲");
            var encounter = Session.Encounter.Value;
            if (encounter.Pirate == PiratePhase.Warning || encounter.Pirate == PiratePhase.Attacking)
            {
                Vector2 pirate = encounter.PiratePosition;
                GUI.Label(new Rect(Mathf.Clamp(map.x + 90 + pirate.x * .75f, map.x + 5, map.xMax - 25),
                    Mathf.Clamp(map.y + 235 - pirate.y * .9f, map.y + 25, map.yMax - 25), 25, 25), "P");
            }
        }

        private void DrawPassengers()
        {
            var s = Session.Voyage.Value;
            GUI.Box(new Rect(1070, 355, 198, 240), GUIContent.none);
            GUILayout.BeginArea(new Rect(1080, 365, 178, 220));
            GUILayout.Label("PASSENGERS", body);
            GUILayout.Label($"Tickets checked: {s.CheckedTickets}/{s.Boarded}\nUnpaid fares collected: {s.CollectedFares}\nRescues: {s.RescueCount} (15 each)", body);
            GUILayout.Label("F / Y: check / collect\nG / B: lift / put down\nLMB / RT: throw held NPC", body);
            if (s.Phase == VoyagePhase.Arrived || s.Phase == VoyagePhase.Failed)
                GUILayout.Label($"Unpaid: {s.LastUnpaid}\nMissing: {s.LastMissing}\nRescue costs: {s.LastRescueCosts}", body);
            else GUILayout.Label("Ticket income is paid on arrival at North Port.", body);
            GUILayout.EndArea();
        }

        private void DrawHarbor()
        {
            var s = Session.Voyage.Value;
            int ashore = 0;
            foreach (var crew in Session.Crew) if (crew.Active && crew.OnShore) ashore++;
            GUI.Box(new Rect(1070, 355, 198, 288), GUIContent.none);
            GUILayout.BeginArea(new Rect(1080, 365, 178, 268));
            GUILayout.Label("HARBOR PREPARATION", body);
            GUILayout.Label($"Crew ashore: {ashore}\nHull shield: {s.HullShield:0}/20", body);
            GUILayout.Label("Green gangway: E / A\nFood +8: 12 coins\nDrink +8: 12 coins\nHull shield: 30 coins", body);
            GUILayout.Label("Supplies carry over. Storage cap: 32 each. Shared wallet.", body);
            if (s.Phase == VoyagePhase.Arrived)
                GUILayout.Label($"Unpaid: {s.LastUnpaid}\nMissing: {s.LastMissing}\nRescue costs: {s.LastRescueCosts}", body);
            else GUILayout.Label("All crew aboard, then Helm + Space / X to depart.", body);
            GUILayout.EndArea();
        }

        private void DrawEncounter()
        {
            var e = Session.Encounter.Value;
            GUI.Box(new Rect(12, 360, 370, 192), GUIContent.none);
            GUILayout.BeginArea(new Rect(26, 370, 342, 176));
            GUILayout.Label(e.PowerOut ? $"POWER OUT! Breakers: {e.PowerRepairSteps}/3" : "POWER ONLINE", body);
            if (e.PowerOut) GUILayout.Label("New food/drink blocked. Use POWER panel.", body);
            string threat = e.Pirate == PiratePhase.Warning ? $"PIRATES IN {e.WarningRemaining:0.0}s — find CANNON!" :
                e.Pirate == PiratePhase.Attacking ? $"PIRATES: {e.PirateHealth:0}% | incoming shot {e.EnemyShotRemaining:0.0}s" :
                e.Pirate == PiratePhase.Defeated ? "Pirates repelled! +50 on arrival" :
                e.Pirate == PiratePhase.Escaped ? "Pirates withdrew. No bounty." : "Watch for pirates after departure.";
            GUILayout.Label(threat, body);
            GUILayout.Label($"Cannon ammo: {e.Ammo}/12 | " + (e.ReloadRemaining > 0 ? $"Reloading {e.ReloadRemaining:0.0}s" : e.CannonLoaded ? "LOADED" : "R / X: reload"), body);
            GUILayout.Label("At CANNON: mouse / right stick aims\nLMB / RT fires • R / X reloads • E / A releases", body);
            GUILayout.EndArea();
        }
    }
}
