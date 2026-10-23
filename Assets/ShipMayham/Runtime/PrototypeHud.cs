using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace ShipMayham
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        private const string SessionType = "ship-mayham-prototype";
        public const ushort NetworkProtocolVersion = 9;
        public NetworkManager Manager;
        public PrototypeSession Session;
        public PrototypeView View;
        private string address = "127.0.0.1";
        private string sessionCode = "";
        private string status = "Create an online voyage, or enter a friend's join code.";
        private bool attempted, reloading;
        private readonly HashSet<ulong> admittedClients = new HashSet<ulong>();
        private readonly HashSet<string> admittedPlayers = new HashSet<string>();
        private readonly HashSet<string> pendingDepartures = new HashSet<string>();
        private readonly Dictionary<ulong, string> playerIdentities = new Dictionary<ulong, string>();
        private ISession onlineSession;
        private IClientSessionNetwork onlineNetwork;
        private VoyageMigrationDataHandler migrationHandler;
        private bool onlineMode;
        private static PrototypeHud instance;
        private GUIStyle title, body, crosshair;
        private bool showHelp;
        public string OnlineCode => onlineSession?.Code;
        public bool IsOnlineHost => onlineSession != null && onlineSession.IsHost;
        public int CompletedMigrations { get; private set; }
        public SessionError? LastMigrationError { get; private set; }
        public NetworkState? OnlineNetworkState => onlineNetwork?.State;

        public static bool OnlineTransportActive => instance != null && instance.onlineMode;
        public static string PlayerIdentity(ulong clientId) =>
            instance != null && instance.playerIdentities.TryGetValue(clientId, out string playerId) ? playerId : null;

        private void Update()
        {
            if (Keyboard.current?.f1Key.wasPressedThisFrame == true) showHelp = !showHelp;
            FlushPendingDepartures();
        }

        private void Awake()
        {
            instance = this;
            Application.runInBackground = true;
            migrationHandler = new VoyageMigrationDataHandler(() => Session != null ? Session.Simulation : null);
            Manager.ConnectionApprovalCallback = Approve;
            Manager.OnClientDisconnectCallback += Disconnected;
        }

        private void OnDestroy()
        {
            UnsubscribeOnlineSession();
            if (instance == this) instance = null;
            if (Manager != null)
            {
                Manager.OnClientDisconnectCallback -= Disconnected;
                Manager.ConnectionApprovalCallback = null;
            }
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            string playerId = request.Payload != null && request.Payload.Length > 0
                ? Encoding.UTF8.GetString(request.Payload)
                : null;
            bool knownOnlinePlayer = !string.IsNullOrEmpty(playerId) && admittedPlayers.Contains(playerId);
            int admittedCount = onlineMode ? admittedPlayers.Count : admittedClients.Count;
            response.Approved = knownOnlinePlayer || admittedClients.Contains(request.ClientNetworkId) || admittedCount < VoyageSimulation.MaxCrew;
            if (response.Approved)
            {
                admittedClients.Add(request.ClientNetworkId);
                if (!string.IsNullOrEmpty(playerId))
                {
                    admittedPlayers.Add(playerId);
                    playerIdentities[request.ClientNetworkId] = playerId;
                }
            }
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = response.Approved ? "" : "The ship already has four crew members.";
        }

        private void Disconnected(ulong id)
        {
            admittedClients.Remove(id);
            playerIdentities.Remove(id);
            if (id == Manager.LocalClientId)
                status = onlineMode ? "Connection interrupted. Waiting for host migration..." :
                    "Disconnected. " + Manager.DisconnectReason + " Reload to start a fresh connection.";
        }

        private void PrepareNetworkConfig()
        {
            Manager.NetworkConfig.ProtocolVersion = NetworkProtocolVersion;
            // Crew live in scene slots; NGO must never inspect/spawn a player prefab here.
            // Assigning real null also handles Unity's serialized fake-null references.
            Manager.NetworkConfig.PlayerPrefab = null;
            Manager.NetworkConfig.AutoSpawnPlayerPrefabClientSide = false;
        }

        public void ConnectSolo()
        {
            if (Manager.IsListening || attempted) return;
            attempted = true;
            onlineMode = false;
            PrepareNetworkConfig();
            Manager.NetworkConfig.NetworkTransport = Manager.gameObject.AddComponent<SoloTransport>();
            bool started = Manager.StartHost();
            status = started ? "Solo voyage ready." : "Solo voyage could not start. Reload and retry.";
        }

        public void Connect(bool host, string hostAddress = null)
        {
            if (Manager.IsListening || attempted) return;
            attempted = true;
            onlineMode = false;
            if (hostAddress != null) address = hostAddress;
            PrepareNetworkConfig();
            Manager.GetComponent<UnityTransport>().SetConnectionData(address.Trim(), 7777, host ? "0.0.0.0" : null);
            bool started = host ? Manager.StartHost() : Manager.StartClient();
            status = started ? "Connecting..." : "Connection could not start. Reload and retry.";
        }

        public void ConnectOnline(bool host, string joinCode = null, string authenticationProfile = null)
        {
            if (Manager.IsListening || attempted) return;
            if (joinCode != null) sessionCode = joinCode;
            _ = ConnectOnlineAsync(host, authenticationProfile);
        }

        private async Task ConnectOnlineAsync(bool host, string authenticationProfile)
        {
            attempted = true;
            onlineMode = true;
            status = host ? "Creating Relay voyage..." : "Joining Relay voyage...";
            admittedClients.Clear();
            admittedPlayers.Clear();
            pendingDepartures.Clear();
            playerIdentities.Clear();
            CompletedMigrations = 0;
            LastMigrationError = null;

            try
            {
                await UnityServices.InitializeAsync();
                if (!string.IsNullOrEmpty(authenticationProfile))
                    AuthenticationService.Instance.SwitchProfile(authenticationProfile);
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                PrepareNetworkConfig();
                Manager.GetComponent<UnityTransport>().UseWebSockets = true;
                string playerId = AuthenticationService.Instance.PlayerId;
                Manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(playerId);

                var networkOptions = new NetworkOptions { RelayProtocol = RelayProtocol.WSS };
                if (host)
                {
                    var options = new SessionOptions
                    {
                        MaxPlayers = VoyageSimulation.MaxCrew,
                        Name = "Ship Mayham Voyage",
                        Type = SessionType,
                        IsPrivate = true
                    };
                    options.WithRelayNetwork(new RelayNetworkOptions(preserveRegion: true));
                    options.WithNetworkOptions(networkOptions);
                    options.WithHostMigration(migrationHandler, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8));
                    onlineSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                    sessionCode = onlineSession.Code;
                    status = $"Online voyage ready. Share code {sessionCode}.";
                }
                else
                {
                    string code = sessionCode.Trim().ToUpperInvariant();
                    if (code.Length < 6) throw new InvalidOperationException("Enter a valid 6–8 character join code.");
                    var options = new JoinSessionOptions { Type = SessionType };
                    options.WithNetworkOptions(networkOptions);
                    options.WithHostMigration(migrationHandler, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8));
                    onlineSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, options);
                    sessionCode = onlineSession.Code;
                    status = $"Joined online voyage {sessionCode}.";
                }
                SubscribeOnlineSession();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                attempted = false;
                onlineMode = false;
                status = "Online connection failed: " + exception.Message;
            }
        }

        private void SubscribeOnlineSession()
        {
            if (onlineSession == null) return;
            onlineSession.SessionHostChanged += OnlineHostChanged;
            onlineSession.SessionMigrated += OnlineSessionMigrated;
            onlineSession.PlayerHasLeft += OnlinePlayerLeft;
            onlineSession.RemovedFromSession += OnlineSessionEnded;
            onlineSession.Deleted += OnlineSessionEnded;
            onlineNetwork = onlineSession.Network;
            if (onlineNetwork != null) onlineNetwork.MigrationFailed += OnlineMigrationFailed;
        }

        private void UnsubscribeOnlineSession()
        {
            if (onlineNetwork != null) onlineNetwork.MigrationFailed -= OnlineMigrationFailed;
            onlineNetwork = null;
            if (onlineSession == null) return;
            onlineSession.SessionHostChanged -= OnlineHostChanged;
            onlineSession.SessionMigrated -= OnlineSessionMigrated;
            onlineSession.PlayerHasLeft -= OnlinePlayerLeft;
            onlineSession.RemovedFromSession -= OnlineSessionEnded;
            onlineSession.Deleted -= OnlineSessionEnded;
        }

        private void OnlineHostChanged(string newHostId)
        {
            status = "Host left. Relay is migrating the voyage...";
        }

        private void OnlineSessionMigrated()
        {
            CompletedMigrations++;
            LastMigrationError = null;
            FlushPendingDepartures();
            status = onlineSession != null && onlineSession.IsHost
                ? "Migration complete: you are the new host."
                : "Migration complete: reconnected to the new host.";
        }

        private void OnlineMigrationFailed(SessionError error)
        {
            LastMigrationError = error;
            status = $"Host migration failed ({error}). Leave this session and create a new voyage.";
            Debug.LogWarning(status);
        }

        private void OnlinePlayerLeft(string playerId)
        {
            pendingDepartures.Add(playerId);
            FlushPendingDepartures();
        }

        private void FlushPendingDepartures()
        {
            if (pendingDepartures.Count == 0 || !onlineMode || onlineSession == null || !onlineSession.IsHost ||
                Session == null || !Session.IsSpawned || !Session.IsServer || Session.Simulation == null) return;
            var currentPlayers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var player in onlineSession.Players) currentPlayers.Add(player.Id);
            ReconcileOnlineDepartures(currentPlayers);
        }

        private void ReconcileOnlineDepartures(ISet<string> currentPlayers)
        {
            // Lobby deltas can arrive after a newer roster was applied. The SDK's
            // indexed PlayerHasLeft callback is not sufficient authority to delete
            // a stable crew identity that is still in the current session roster.
            foreach (string playerId in pendingDepartures)
            {
                if (currentPlayers.Contains(playerId))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (onlineMode) Debug.Log("SHIP_CREW_DEPART_IGNORED current session member");
#endif
                    continue;
                }
                admittedPlayers.Remove(playerId);
                Session.RemoveOnlinePlayer(playerId);
            }
            pendingDepartures.Clear();
        }

        private void OnlineSessionEnded()
        {
            status = "The online session ended.";
            attempted = false;
            onlineMode = false;
            if (!reloading) StartCoroutine(Reload());
        }

        private async Task LeaveOnlineAsync()
        {
            reloading = true;
            try
            {
                if (onlineSession != null) await onlineSession.LeaveAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not leave online session cleanly: " + exception.Message);
            }
            UnsubscribeOnlineSession();
            onlineSession = null;
            onlineMode = false;
            // A failed SDK leave may still have a live NGO manager. The guarded
            // shutdown also makes that failure path return to the menu safely.
            StartCoroutine(Reload());
        }

        private void LeaveOrReload()
        {
            if (onlineSession != null) _ = LeaveOnlineAsync();
            else StartCoroutine(Reload());
        }

        private IEnumerator Reload(bool shutdown = true)
        {
            reloading = true;
            if (shutdown && Manager != null && Manager.IsListening && !Manager.ShutdownInProgress) Manager.Shutdown();
            while (Manager != null && (Manager.IsListening || Manager.ShutdownInProgress)) yield return null;
            if (Manager != null) Destroy(Manager.gameObject);
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
                GUILayout.Label("1–4 crew • Unity Relay • WebGL + Windows", body);
                GUILayout.Space(12);
                GUILayout.Label("Voyage code (joining only)");
                sessionCode = GUILayout.TextField(sessionCode ?? string.Empty, 8).Trim().ToUpperInvariant();
                GUI.enabled = !attempted;
                if (GUILayout.Button("CREATE ONLINE VOYAGE", GUILayout.Height(36))) ConnectOnline(true);
                if (GUILayout.Button("JOIN ONLINE VOYAGE", GUILayout.Height(36))) ConnectOnline(false);
                if (GUILayout.Button("PLAY SOLO (OFFLINE)", GUILayout.Height(28))) ConnectSolo();
                GUI.enabled = true;
                GUILayout.Label(status, body);
                if (attempted && (!onlineMode || onlineSession != null) && GUILayout.Button("Cancel / leave")) LeaveOrReload();
            }
            else
            {
                var s = Session.Voyage.Value;
                if (onlineMode && !string.IsNullOrWhiteSpace(sessionCode))
                    GUILayout.Label($"Online code: {sessionCode}" + (onlineSession != null && onlineSession.IsHost ? " • HOST" : string.Empty), body);
                GUILayout.Label($"Voyage {s.Voyage} / {s.Phase}    Coins: {s.Wallet}", body);
                GUILayout.Label($"Route: {VoyageSimulation.RouteName(s.Route)} | Reputation: {s.Reputation}", body);
                GUILayout.Label($"Hull {s.Health:0}% | Shield {s.HullShield:0} | {s.Speed:0.0} m/s", body);
                GUILayout.Label($"Destination: {Vector2.Distance(s.Position, VoyageSimulation.Destination):0} m    Heading: {s.Heading:0}°", body);
                GUILayout.Label($"Passengers: {s.Boarded}    Orders served: {s.Served}", body);
                GUILayout.Label($"Stock: {s.FoodStock} food / {s.DrinkStock} drink", body);
                GUILayout.Label($"Engine {s.EngineLevel}/2 | Seats {s.CapacityLevel}/2 | Comfort {s.ComfortLevel}/1", body);
                if (s.Leak != 0) GUILayout.Label($"LEAK! REPAIR: {s.RepairSteps}/4 manual | {s.ReadyRepairKits} quick kits ready", body);
                else GUILayout.Label("Hull watertight", body);
                if (s.Phase == VoyagePhase.Arrived || s.Phase == VoyagePhase.Failed)
                    GUILayout.Label($"Tickets {s.LastTickets} + service {s.LastServices} + tips {s.LastTips}\nBounty {s.LastBounty} + events {s.LastEventBonus} + route {s.LastRouteBonus} - costs {s.LastCosts} = net {s.LastNet}\nReputation +{s.LastReputation}. Manifest: new voyage.", body);
                else GUILayout.Label(s.Phase == VoyagePhase.Boarding ? "1. Manifest: board\n2. Helm: E, then Space to depart" : "Serve matching orders. Brake (S) and dock (Space) within 12 m.", body);
            }
            GUILayout.EndArea();
            if ((Session.IsSpawned || onlineSession != null) && GUI.Button(new Rect(1115, 12, 153, 32), "Leave session")) LeaveOrReload();
            if (LastMigrationError.HasValue)
            {
                GUI.Box(new Rect(410, 82, 630, 78), GUIContent.none);
                GUI.Label(new Rect(422, 92, 606, 58), status, body);
            }
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
                    GUI.Label(new Rect(412, 74, 626, 104), "Space / X: jump (helm: depart/dock, C / Y: helm camera)\nPassengers: F / Y check or collect • G / B lift or put down • LMB / RT throw\nCannon: LMB / RT fire • R / X reload • E / A release\nBlue = drink • Orange = food • Green = repair / gangway\nAll crew must return aboard before departure or starting a new voyage.", body);
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
                        GUI.Label(new Rect(410, 610, 460, 28), Application.platform == RuntimePlatform.WebGLPlayer
                            ? "Click the game center to resume looking" : "Esc / Start or click center to resume looking", body);
                }
                DrawMap();
            }
            GUI.matrix = Matrix4x4.identity;
        }

        private void DrawMap()
        {
            Rect map = new Rect(1070, 70, 190, 270);
            int local = Session.LocalCrewIndex;
            if (local >= 0 && Session.Crew[local].OnShore)
            {
                GUI.Box(map, Session.Harbor.PortIndex == 0 ? "BRASS QUAY" : "NORTH HAVEN");
                TownMapPoint(map, VoyageSimulation.HarborPoint(HarborAction.BoardShip), "SHIP", Color.white);
                TownMapPoint(map, VoyageSimulation.HarborPoint(HarborAction.BuyFood), "FOOD", new Color(1, .55f, .2f));
                TownMapPoint(map, VoyageSimulation.HarborPoint(HarborAction.BuyDrink), "DRINK", Color.cyan);
                TownMapPoint(map, VoyageSimulation.HarborPoint(HarborAction.MaintainHull), "HULL", Color.green);
                TownMapPoint(map, VoyageSimulation.HarborPoint(HarborAction.HarborMaster), "OFFICE", Color.yellow);
                TownMapPoint(map, VoyageSimulation.HarborPoint(HarborAction.PaintShop), "PAINT", Color.magenta);
                if (Session.Voyage.Value.DockJobType == DockJobKind.CraneLoading)
                {
                    TownMapPoint(map, VoyageSimulation.HarborPoint(HarborAction.FishMarket), "FISH", Color.cyan);
                    TownMapPoint(map, VoyageSimulation.HarborPoint(HarborAction.CraneLoadingPad), "PAD", Color.yellow);
                    TownMapPoint(map, VoyageSimulation.CraneBarge, "BARGE", Color.green);
                }
                if (Session.Voyage.Value.DockJobActive && Session.Voyage.Value.DockJobType == DockJobKind.SalvageRun)
                    for (int i = 0; i < HarborTownLayout.SalvageCount; i++)
                        if ((Session.Voyage.Value.DockJobDeliveredMask & (1 << i)) == 0) TownMapPoint(map, HarborTownLayout.SalvagePoint(i), "S" + (i + 1), Color.yellow);
                TownMapPoint(map, Session.Crew[local].Position, "@", Color.white);
                return;
            }
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

        private static void TownMapPoint(Rect map, Vector2 point, string text, Color color)
        {
            Color old = GUI.color; GUI.color = color;
            float x = map.x + 8 + (point.x - HarborTownLayout.Bounds.xMin) / HarborTownLayout.Bounds.width * 133;
            float y = map.y + 30 + (HarborTownLayout.Bounds.yMax - point.y) / HarborTownLayout.Bounds.height * 215;
            GUI.Label(new Rect(x, y, 55, 20), text); GUI.color = old;
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
            GUI.Box(new Rect(1010, 355, 258, 356), GUIContent.none);
            GUILayout.BeginArea(new Rect(1020, 365, 238, 336));
            GUILayout.Label("HARBOR PREPARATION", body);
            GUILayout.Label($"Crew ashore: {ashore}\nHull shield: {s.HullShield:0}/20\nRoute: {VoyageSimulation.RouteName(s.Route)}", body);
            int local = Session.LocalCrewIndex;
            bool atOffice = local >= 0 && Session.Crew[local].OnShore && Vector2.Distance(Session.Crew[local].Position, VoyageSimulation.HarborPoint(HarborAction.HarborMaster)) < 4;
            bool canChoose = s.Phase == VoyagePhase.Boarding && s.Boarded == 0 && atOffice;
            GUI.enabled = canChoose;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("COAST")) Session.SelectRouteRpc(RouteProfile.CoastalFerry);
            GUI.enabled = canChoose && s.Reputation >= VoyageSimulation.RouteReputation(RouteProfile.MarketRun);
            if (GUILayout.Button("MARKET (2)")) Session.SelectRouteRpc(RouteProfile.MarketRun);
            GUI.enabled = canChoose && s.Reputation >= VoyageSimulation.RouteReputation(RouteProfile.StormPassage);
            if (GUILayout.Button("STORM (5)")) Session.SelectRouteRpc(RouteProfile.StormPassage);
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.Label(s.DockJobActive ? $"{VoyageSimulation.DockJobName(s.DockJobType)} {s.DockJobProgress}/{VoyageSimulation.DockJobGoal}" : $"Port jobs completed: {s.DockJobsCompleted}", body);
            GUI.enabled = s.DockJobActive ? local >= 0 && Session.Crew[local].OnShore : atOffice;
            if (s.DockJobActive)
            {
                if (GUILayout.Button("CANCEL JOB")) Session.SelectDockJobRpc(s.DockJobType);
            }
            else
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("CARGO")) Session.SelectDockJobRpc(DockJobKind.CargoRelay);
                if (GUILayout.Button("BAG SORT")) Session.SelectDockJobRpc(DockJobKind.LuggageSort);
                if (GUILayout.Button("SALVAGE")) Session.SelectDockJobRpc(DockJobKind.SalvageRun);
                GUILayout.EndHorizontal();
                if (GUILayout.Button("CRANE LOADING (+45)")) Session.SelectDockJobRpc(DockJobKind.CraneLoading);
            }
            GUI.enabled = true;
            if (s.DockJobActive && s.DockJobType == DockJobKind.CraneLoading)
                GUILayout.Label($"FISH MARKET: {s.CraneStock} crates\nLOAD PAD: {s.CraneReady}/{VoyageSimulation.CranePadCapacity} | Hook: {(s.CraneHolding ? "loaded" : "empty")}\nCarry -> PAD -> CRANE -> BARGE\n3 landed: +45 coins / +1 rep.", body);
            else if (s.DockJobActive && s.DockJobType == DockJobKind.LuggageSort)
                GUILayout.Label($"ORANGE: pickup / return\nA: {VoyageSimulation.LuggageDesk(s, ServiceItem.LuggageA)}\nB: {VoyageSimulation.LuggageDesk(s, ServiceItem.LuggageB)}\nC: {VoyageSimulation.LuggageDesk(s, ServiceItem.LuggageC)}", body);
            else if (s.DockJobActive && s.DockJobType == DockJobKind.SalvageRun)
                GUILayout.Label("Find S1 / S2 / S3 on town map.\nCarry salvage to PORT OFFICE.\n3 delivered: +36 coins / +1 rep.\nFOOD accepts returns.", body);
            else GUILayout.Label("Visit PORT OFFICE for routes/jobs.\nFood +8: 12 coins\nDrink +8: 12 coins\nHull shield: 30 coins", body);
            GUILayout.Label("Supplies carry over. Storage cap: 32 each. Shared wallet.", body);
            if (s.Phase == VoyagePhase.Arrived)
                GUILayout.Label($"Unpaid: {s.LastUnpaid}\nMissing: {s.LastMissing}\nRescue costs: {s.LastRescueCosts}", body);
            else GUILayout.Label("All crew aboard, then Helm + Space / X to depart.", body);
            GUILayout.EndArea();
            if (local >= 0 && Session.Crew[local].AtCrane)
            {
                bool inTarget = Vector2.Distance(VoyageSimulation.CraneLandingPoint(s), VoyageSimulation.CraneBarge) <= VoyageSimulation.CraneLandingRadius;
                GUI.Box(new Rect(400, 74, 640, 100), GUIContent.none);
                GUI.Label(new Rect(414, 82, 612, 84), $"CRANE CAB — WASD / left stick: move hook (north up)\nSpace / X: {(s.CraneHolding ? inTarget ? "LAND NOW — inside barge ring" : "lower load — aim inside the green barge ring" : "lift crate from LOAD PAD")}\nE / A: release | Load sways after movement. Completed: {s.DockJobProgress}/3", body);
            }
            if (local >= 0 && Session.Crew[local].OnShore && Vector2.Distance(Session.Crew[local].Position, VoyageSimulation.HarborPoint(HarborAction.PaintShop)) < 4)
            {
                GUI.Box(new Rect(400, 188, 370, 92), GUIContent.none);
                GUILayout.BeginArea(new Rect(412, 196, 346, 76));
                GUILayout.Label($"SHIP PAINT: {s.Livery} | Reputation {s.Reputation}", body);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Classic")) Session.SelectLiveryRpc(ShipLivery.Classic);
                GUI.enabled = s.Reputation >= 2;
                if (GUILayout.Button("Seafoam (2)")) Session.SelectLiveryRpc(ShipLivery.Seafoam);
                GUI.enabled = s.Reputation >= 5;
                if (GUILayout.Button("Sunset (5)")) Session.SelectLiveryRpc(ShipLivery.Sunset);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
        }

        private void DrawEncounter()
        {
            var e = Session.Encounter.Value;
            var voyageEvent = Session.ActiveEvent.Value;
            GUI.Box(new Rect(12, 360, 370, 298), GUIContent.none);
            GUILayout.BeginArea(new Rect(26, 370, 342, 282));
            GUILayout.Label(e.PowerOut ? $"POWER OUT! Breakers: {e.PowerRepairSteps}/3" : "POWER ONLINE", body);
            if (e.PowerOut) GUILayout.Label("New food/drink blocked. Use POWER panel.", body);
            string threat = e.Pirate == PiratePhase.Warning ? $"PIRATES IN {e.WarningRemaining:0.0}s — find CANNON!" :
                e.Pirate == PiratePhase.Attacking ? $"PIRATES: {e.PirateHealth:0}% | incoming shot {e.EnemyShotRemaining:0.0}s" :
                e.Pirate == PiratePhase.Defeated ? "Pirates repelled! +50 on arrival" :
                e.Pirate == PiratePhase.Escaped ? "Pirates withdrew. No bounty." : "Watch for pirates after departure.";
            GUILayout.Label(threat, body);
            string rack = e.ReloadRemaining > 0 ? $"{e.ReadyAmmo}+1 reserved" : e.ReadyAmmo.ToString();
            GUILayout.Label($"Ammo: {e.Ammo}/12 total | magazine {e.MagazineAmmo}\nCannon rack: {rack}/{VoyageSimulation.CannonBufferCapacity} | " + (e.ReloadRemaining > 0 ? $"Reloading {e.ReloadRemaining:0.0}s" : e.CannonLoaded ? "LOADED" : "R / X: reload"), body);
            GUILayout.Label("At CANNON: mouse / right stick aims\nLMB / RT fires • R / X reloads • E / A releases", body);
            GUILayout.Space(4);
            GUILayout.Label(Session.Voyage.Value.Phase == VoyagePhase.Arrived || Session.Voyage.Value.Phase == VoyagePhase.Failed
                ? "SHIP EVENTS CLOSED — prepare a new voyage" : EventText(voyageEvent), body);
            GUILayout.EndArea();
        }

        private static string EventText(VoyageEventState e)
        {
            if (e.Phase == VoyageEventPhase.Waiting) return $"NEXT SHIP EVENT: ~{e.NextIn:0}s";
            if (e.Phase == VoyageEventPhase.Resolved) return "EVENT SOLVED — +15 on arrival";
            if (e.Phase == VoyageEventPhase.Failed) return "EVENT FAILED — consequences applied";
            string timer = $"{e.Remaining:0}s";
            string target = $"passenger {e.TargetPassenger + 1}";
            switch (e.Kind)
            {
                case VoyageEventKind.ContrabandInspection: return $"CONTRABAND — {timer}\nClutched suitcase? F / Y: inspect their ticket.";
                case VoyageEventKind.GalleyRush: return $"GALLEY RUSH — {e.Progress}/{e.Goal} orders — {timer}";
                case VoyageEventKind.LooseCargo: return $"LOOSE CARGO — repair panel {e.Progress}/{e.Goal} — {timer}";
                case VoyageEventKind.VipService: return $"VIP SERVICE — {timer}\nServe {target}'s order with E / A.";
                case VoyageEventKind.TicketAudit: return $"TICKET AUDIT — {e.Progress}/{e.Goal} — {timer}\nF / Y: check different passengers.";
                case VoyageEventKind.FuseOverload: return $"FUSE OVERLOAD — {timer}\nReset POWER breakers: {e.Progress}/{e.Goal}";
                case VoyageEventKind.EngineOverheat: return $"ENGINE HOT — {timer}\nSlow below 1 m/s for 8s ({e.CoolingTime:0.0}/8), or vent twice at REPAIR.";
                case VoyageEventKind.SeatShuffle: return $"SEAT SHUFFLE — {timer}\nG / B: carry {target} at least 1.5m from their seat; put down safely.";
                default: return "Ship event pending.";
            }
        }

    }
}
