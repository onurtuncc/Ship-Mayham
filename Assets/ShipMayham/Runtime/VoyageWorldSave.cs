namespace ShipMayham
{
    /// <summary>Getting a restored world ready for whoever is playing it now.</summary>
    public sealed partial class VoyageSimulation
    {
        /// <summary>
        /// A world loaded from a save has no connected players. Online hosts keep the slots of players who have a stable identity,
        /// so they take their own seat back when they rejoin; everyone else, and everyone in a solo continue, starts with a clean slot.
        /// Whatever a kept slot was holding or manning is put back so nothing stays stuck waiting for them.
        /// </summary>
        public void ReadyForNewParty(bool keepIdentifiedPlayers)
        {
            for (int i = 0; i < MaxCrew; i++)
            {
                if (!Crew[i].Active) continue;
                if (!keepIdentifiedPlayers || string.IsNullOrEmpty(crewPlayerIds[i])) { LeaveSlot(i); continue; }
                ReleaseCrewResponsibilities(i);
                Crew[i].ClientId = ulong.MaxValue;
                Crew[i].JumpHeight = Crew[i].VerticalSpeed = 0;
                if (Crew[i].OnShore) { Crew[i].OnShore = false; Crew[i].Position = ShipGangway; }
            }
        }
    }
}
