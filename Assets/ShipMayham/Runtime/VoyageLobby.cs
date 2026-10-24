using System;

namespace ShipMayham
{
    public sealed partial class VoyageSimulation
    {
        /// <summary>Connection id of the host player; equals the networking layer's server client id.</summary>
        public const ulong HostClientId = 0;

        private readonly string[] crewNames = new string[MaxCrew];
        private readonly bool[] crewReady = new bool[MaxCrew];

        public bool InLobby => State.SessionPhase == SessionPhase.Lobby;

        public string CrewName(int slot) => slot >= 0 && slot < MaxCrew ? crewNames[slot] : null;
        public bool CrewReady(int slot) => slot >= 0 && slot < MaxCrew && crewReady[slot];
        public bool IsHostSlot(int slot) => slot >= 0 && slot < MaxCrew && Crew[slot].Active && Crew[slot].ClientId == HostClientId;

        /// <summary>Puts the session into the lobby: no voyage time passes and no commands run until the host starts.</summary>
        public void OpenLobby()
        {
            State.SessionPhase = SessionPhase.Lobby;
            Array.Clear(crewReady, 0, crewReady.Length);
        }

        public bool AllReady()
        {
            for (int i = 0; i < MaxCrew; i++)
                if (LobbyRules.WaitingOnPlayer(Crew[i].Active && Crew[i].ClientId != ulong.MaxValue, IsHostSlot(i), crewReady[i])) return false;
            return true;
        }

        /// <summary>Sets the display name of the sender's crew slot; unusable names are ignored.</summary>
        public void SetNickname(ulong clientId, string name)
        {
            int slot = FindCrew(clientId);
            string clean = LobbyRules.SanitizeName(name);
            if (slot >= 0 && clean != null) crewNames[slot] = clean;
        }

        public string SetReady(ulong clientId, bool ready)
        {
            int slot = FindCrew(clientId);
            if (slot < 0) return "Join the crew first.";
            if (!InLobby) return string.Empty;
            // The host does not press Ready; it starts the voyage.
            crewReady[slot] = ready && !IsHostSlot(slot);
            return string.Empty;
        }

        public string StartVoyage(ulong clientId)
        {
            int slot = FindCrew(clientId);
            if (slot < 0) return "Join the crew first.";
            if (!InLobby) return "The voyage has already started.";
            if (!IsHostSlot(slot)) return "Only the host can start the voyage.";
            if (!AllReady()) return "Waiting for everyone to get ready.";
            State.SessionPhase = SessionPhase.Voyage;
            Array.Clear(crewReady, 0, crewReady.Length);
            return "The voyage begins. Board passengers at the manifest.";
        }

        private void ClearLobbySlot(int slot)
        {
            crewNames[slot] = null;
            crewReady[slot] = false;
        }
    }
}
