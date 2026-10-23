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

namespace ShipMayham
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        private const string SessionType = "ship-mayham-prototype";
        public const ushort NetworkProtocolVersion = 14;
        public NetworkManager Manager;
        public PrototypeSession Session;
        public PrototypeView View;
        private string address = "127.0.0.1";
        private string sessionCode = "";
        private string status = "Create an online voyage, or enter a friend's join code.";
        private bool attempted, reloading;
        private readonly CrewRoster roster = new CrewRoster();
        private ISession onlineSession;
        private IClientSessionNetwork onlineNetwork;
        private VoyageMigrationDataHandler migrationHandler;
        private bool onlineMode { get => roster.OnlineMode; set => roster.OnlineMode = value; }
        private bool connectionLost, statusIsError;
        /// <summary>Latest connection message for the lobby screen.</summary>
        public string Status => status;
        public bool StatusIsError => statusIsError;
        /// <summary>True while the local client has lost its host and is waiting for a migration.</summary>
        public bool ConnectionLost => connectionLost;
        public bool HasMigrationError => LastMigrationError.HasValue;
        public bool ConnectionAttempted => attempted;
        public bool CanCancelConnection => attempted && (!onlineMode || onlineSession != null);
        public bool IsReloading => reloading;
        public bool HasOnlineCode => onlineMode && !string.IsNullOrWhiteSpace(sessionCode);
        /// <summary>Join code typed by the player or assigned by the online session.</summary>
        public string JoinCode
        {
            get => sessionCode ?? string.Empty;
            set => sessionCode = (value ?? string.Empty).Trim().ToUpperInvariant();
        }
        public string OnlineCode => onlineSession?.Code;
        public bool IsOnlineHost => onlineSession != null && onlineSession.IsHost;
        public int CompletedMigrations { get; private set; }
        public SessionError? LastMigrationError { get; private set; }
        public NetworkState? OnlineNetworkState => onlineNetwork?.State;

        /// <summary>Who may hold a crew slot; shared with the session so it never has to look the HUD up.</summary>
        public CrewRoster Roster => roster;
        /// <summary>Online hosts wait in the lobby until they start the voyage. Automated validation builds turn this off.</summary>
        public bool LobbyBeforeVoyage { get; set; } = true;

        private void Update()
        {
            FlushPendingDepartures();
        }

        private void Awake()
        {
            Application.runInBackground = true;
            if (Application.isPlaying) HudController.Create(this);
            migrationHandler = new VoyageMigrationDataHandler(() => Session != null ? Session.Simulation : null);
            if (Session != null) Session.Roster = roster;
            Manager.ConnectionApprovalCallback = roster.Approve;
            Manager.OnClientDisconnectCallback += Disconnected;
        }

        private void OnDestroy()
        {
            UnsubscribeOnlineSession();
            if (Manager != null)
            {
                Manager.OnClientDisconnectCallback -= Disconnected;
                Manager.ConnectionApprovalCallback = null;
            }
        }

        private void Disconnected(ulong id)
        {
            roster.ClientDisconnected(id);
            if (id == Manager.LocalClientId)
            {
                connectionLost = onlineMode;
                status = onlineMode ? "Connection interrupted. Waiting for host migration..." :
                    "Disconnected. " + Manager.DisconnectReason + " Reload to start a fresh connection.";
            }
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
            statusIsError = !started;
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
            statusIsError = !started;
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
            if (Session != null) Session.LobbyBeforeVoyage = host && LobbyBeforeVoyage;
            status = host ? "Creating Relay voyage..." : "Joining Relay voyage...";
            statusIsError = false;
            connectionLost = false;
            roster.Clear();
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
                statusIsError = true;
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
            connectionLost = false;
            FlushPendingDepartures();
            status = onlineSession != null && onlineSession.IsHost
                ? "Migration complete: you are the new host."
                : "Migration complete: reconnected to the new host.";
        }

        private void OnlineMigrationFailed(SessionError error)
        {
            LastMigrationError = error;
            status = $"Host migration failed ({error}). Leave this session and create a new voyage.";
            statusIsError = true;
            Debug.LogWarning(status);
        }

        /// <summary>Session callback; the departure is applied once the voyage is ready and the roster confirms it.</summary>
        public void OnlinePlayerLeft(string playerId)
        {
            roster.QueueDeparture(playerId);
            FlushPendingDepartures();
        }

        private void FlushPendingDepartures()
        {
            if (roster.PendingDepartureCount == 0 || !onlineMode || onlineSession == null || !onlineSession.IsHost ||
                Session == null || !Session.IsSpawned || !Session.IsServer || Session.Simulation == null) return;
            var currentPlayers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var player in onlineSession.Players) currentPlayers.Add(player.Id);
            roster.ReconcileDepartures(currentPlayers, Session.RemoveOnlinePlayer);
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

        public void LeaveSession() => LeaveOrReload();

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
    }
}
