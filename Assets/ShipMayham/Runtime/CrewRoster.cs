using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Who may hold one of the crew slots: connection admission, the stable player identity behind each
    /// connection, and departures that still have to be reconciled with the online session roster.
    /// Holds no Unity or Relay objects, so it can be driven directly by tests.
    /// </summary>
    public sealed class CrewRoster
    {
        private readonly HashSet<ulong> admittedClients = new HashSet<ulong>();
        private readonly HashSet<string> admittedPlayers = new HashSet<string>();
        private readonly HashSet<string> pendingDepartures = new HashSet<string>();
        private readonly Dictionary<ulong, string> identities = new Dictionary<ulong, string>();

        /// <summary>True while crew identities come from an online session; solo and direct connections have none.</summary>
        public bool OnlineMode { get; set; }

        public int PendingDepartureCount => pendingDepartures.Count;

        public void Clear()
        {
            admittedClients.Clear();
            admittedPlayers.Clear();
            pendingDepartures.Clear();
            identities.Clear();
        }

        /// <summary>Decides a connection request. A player already admitted this session may always rejoin its slot.</summary>
        public bool TryAdmit(ulong clientId, byte[] payload, out string reason)
        {
            string playerId = payload != null && payload.Length > 0 ? Encoding.UTF8.GetString(payload) : null;
            bool knownOnlinePlayer = !string.IsNullOrEmpty(playerId) && admittedPlayers.Contains(playerId);
            int admittedCount = OnlineMode ? admittedPlayers.Count : admittedClients.Count;
            bool approved = knownOnlinePlayer || admittedClients.Contains(clientId) || admittedCount < VoyageSimulation.MaxCrew;
            if (approved)
            {
                admittedClients.Add(clientId);
                if (!string.IsNullOrEmpty(playerId))
                {
                    admittedPlayers.Add(playerId);
                    identities[clientId] = playerId;
                }
            }
            reason = approved ? string.Empty : "The ship already has four crew members.";
            return approved;
        }

        public void ClientDisconnected(ulong clientId)
        {
            admittedClients.Remove(clientId);
            identities.Remove(clientId);
        }

        /// <summary>The stable player identity of a connection, or null when it has none (solo, direct connect).</summary>
        public string PlayerIdentity(ulong clientId) => identities.TryGetValue(clientId, out string playerId) ? playerId : null;

        public bool IsAdmitted(string playerId) => admittedPlayers.Contains(playerId);

        /// <summary>Remembers a departure until it can be checked against the current session roster.</summary>
        public void QueueDeparture(string playerId) => pendingDepartures.Add(playerId);

        public bool HasPendingDeparture(string playerId) => pendingDepartures.Contains(playerId);

        /// <summary>
        /// Applies queued departures. Lobby deltas can arrive after a newer roster was applied, so the indexed
        /// "player left" callback alone must not delete a stable crew identity that is still a session member.
        /// </summary>
        public void ReconcileDepartures(ISet<string> currentPlayers, Action<string> removePlayer)
        {
            foreach (string playerId in pendingDepartures)
            {
                if (currentPlayers.Contains(playerId))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (OnlineMode) Debug.Log("SHIP_CREW_DEPART_IGNORED current session member");
#endif
                    continue;
                }
                admittedPlayers.Remove(playerId);
                removePlayer(playerId);
            }
            pendingDepartures.Clear();
        }

        /// <summary>Adapter for <see cref="NetworkManager.ConnectionApprovalCallback"/>.</summary>
        public void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = TryAdmit(request.ClientNetworkId, request.Payload, out string reason);
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = reason;
        }
    }
}
