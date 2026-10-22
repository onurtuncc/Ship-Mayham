using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>Single server-authoritative snapshot for a small co-op prototype.</summary>
    public sealed class PrototypeSession : NetworkBehaviour
    {
        public Transform ShipRoot;
        public PrototypeStation[] Stations;
        public Transform[] PassengerAnchors;
        public PrototypeHarbor Harbor;
        public readonly NetworkVariable<VoyageState> Voyage = new NetworkVariable<VoyageState>();
        public readonly NetworkVariable<EncounterState> Encounter = new NetworkVariable<EncounterState>();
        public readonly NetworkVariable<VoyageEventState> ActiveEvent = new NetworkVariable<VoyageEventState>();
        public NetworkList<CrewState> Crew;
        public NetworkList<PassengerState> Passengers;
        public VoyageSimulation Simulation { get; private set; }
        public string Notice { get; private set; } = "Board passengers at the manifest, then take the helm.";
        public float NoticeUntil { get; private set; }
        private float publishTimer;

        private void Awake()
        {
            Crew = new NetworkList<CrewState>();
            Passengers = new NetworkList<PassengerState>();
            NoticeUntil = Time.unscaledTime + 8;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            var stations = new StationDefinition[Stations.Length];
            for (int i = 0; i < stations.Length; i++) stations[i] = new StationDefinition(Stations[i].Kind, Stations[i].DeckPosition(ShipRoot));
            var seats = new Vector2[PassengerAnchors.Length];
            for (int i = 0; i < seats.Length; i++)
            {
                Vector3 p = ShipRoot.InverseTransformPoint(PassengerAnchors[i].position);
                seats[i] = new Vector2(p.x, p.z);
            }
            Simulation = new VoyageSimulation(stations, seats, Harbor != null, (uint)UnityEngine.Random.Range(1, int.MaxValue));
            CareerProgressStore.Load(ref Simulation.State);
            bool restored = VoyageMigrationDataHandler.RestorePending(Simulation);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PrototypeHud.OnlineTransportActive) Debug.Log($"SHIP_CREW_RESTORE restored={restored}");
#endif
            NetworkManager.OnClientConnectedCallback += Connected;
            NetworkManager.OnClientDisconnectCallback += Disconnected;
            foreach (ulong id in NetworkManager.ConnectedClientsIds) BindConnection(id);
            Publish();
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= Connected;
                NetworkManager.OnClientDisconnectCallback -= Disconnected;
            }
            Simulation = null;
        }

        private void BindConnection(ulong id)
        {
            string identity = PrototypeHud.PlayerIdentity(id);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int previousSlot = Simulation.FindPlayer(identity);
#endif
            int slot = Simulation.Join(id, identity);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PrototypeHud.OnlineTransportActive)
                Debug.Log($"SHIP_CREW_BIND connection={id} identity={!string.IsNullOrEmpty(identity)} previousSlot={previousSlot} slot={slot}");
#endif
        }

        private void Connected(ulong id) { BindConnection(id); Publish(); }
        private void Disconnected(ulong id)
        {
            if (!IsSpawned || !IsServer || Simulation == null || NetworkManager.ShutdownInProgress) return;
            if (PrototypeHud.OnlineTransportActive) Simulation.SuspendConnection(id);
            else Simulation.Leave(id);
            Publish();
        }

        public void RemoveOnlinePlayer(string playerId)
        {
            if (!IsServer || Simulation == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PrototypeHud.OnlineTransportActive) Debug.Log($"SHIP_CREW_DEPART slot={Simulation.FindPlayer(playerId)}");
#endif
            Simulation.LeavePlayer(playerId);
            Publish();
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || Simulation == null) return;
            Simulation.Tick(Time.fixedDeltaTime);
            publishTimer += Time.fixedDeltaTime;
            if (publishTimer >= .05f) { publishTimer = 0; Publish(); }
        }

        private void Publish()
        {
            CareerProgressStore.SaveIfChanged(Simulation.State);
            Voyage.Value = Simulation.State;
            Encounter.Value = Simulation.Encounter;
            ActiveEvent.Value = Simulation.Event;
            for (int i = 0; i < VoyageSimulation.MaxCrew; i++)
                if (Crew.Count <= i) Crew.Add(Simulation.Crew[i]); else if (!Crew[i].Equals(Simulation.Crew[i])) Crew[i] = Simulation.Crew[i];
            for (int i = 0; i < VoyageSimulation.MaxPassengers; i++)
                if (Passengers.Count <= i) Passengers.Add(Simulation.Passengers[i]); else if (!Passengers[i].Equals(Simulation.Passengers[i])) Passengers[i] = Simulation.Passengers[i];
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void HarborCraneActionRpc(bool hoist, RpcParams rpc = default)
        {
            if (Simulation == null) return;
            ulong sender = rpc.Receive.SenderClientId;
            string result = Simulation.HarborCraneAction(sender, hoist);
            if (!string.IsNullOrEmpty(result)) NoticeRpc(new FixedString128Bytes(result), RpcTarget.Single(sender, RpcTargetUse.Temp));
            Publish();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void PickUpSalvageRpc(int index, RpcParams rpc = default)
        {
            if (Simulation == null) return;
            ulong sender = rpc.Receive.SenderClientId;
            string result = Simulation.PickUpSalvage(sender, index);
            if (!string.IsNullOrEmpty(result)) NoticeRpc(new FixedString128Bytes(result), RpcTarget.Single(sender, RpcTargetUse.Temp));
            Publish();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void DockItemCommandRpc(HarborAction action, DockJobKind expectedKind, RpcParams rpc = default)
        {
            if (Simulation == null) return;
            ulong sender = rpc.Receive.SenderClientId;
            string result = Simulation.DockItemCommand(sender, action, expectedKind);
            if (!string.IsNullOrEmpty(result)) NoticeRpc(new FixedString128Bytes(result), RpcTarget.Single(sender, RpcTargetUse.Temp));
            Publish();
        }

        public int LocalCrewIndex
        {
            get
            {
                if (!IsSpawned || NetworkManager == null || NetworkManager.LocalClientId == ulong.MaxValue) return -1;
                for (int i = 0; i < Crew.Count; i++) if (Crew[i].Active && Crew[i].ClientId == NetworkManager.LocalClientId) return i;
                return -1;
            }
        }

        public int LocalCarriedPassengerIndex
        {
            get
            {
                int local = LocalCrewIndex;
                if (local < 0) return -1;
                for (int i = 0; i < Passengers.Count; i++)
                    if (Passengers[i].Active && Passengers[i].Motion == PassengerMotion.Carried && Passengers[i].CarrierSlot == local) return i;
                return -1;
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void HarborCommandRpc(HarborAction action, RpcParams rpc = default)
        {
            if (Simulation == null) return;
            ulong sender = rpc.Receive.SenderClientId;
            string result = Simulation.HarborCommand(sender, action);
            if (!string.IsNullOrEmpty(result)) NoticeRpc(new FixedString128Bytes(result), RpcTarget.Single(sender, RpcTargetUse.Temp));
            Publish();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SelectRouteRpc(RouteProfile route, RpcParams rpc = default)
        {
            if (Simulation == null) return;
            ulong sender = rpc.Receive.SenderClientId;
            string result = Simulation.SelectRoute(sender, route);
            if (!string.IsNullOrEmpty(result)) NoticeRpc(new FixedString128Bytes(result), RpcTarget.Single(sender, RpcTargetUse.Temp));
            Publish();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SelectLiveryRpc(ShipLivery livery, RpcParams rpc = default)
        {
            if (Simulation == null) return;
            ulong sender = rpc.Receive.SenderClientId;
            string result = Simulation.SelectLivery(sender, livery);
            NoticeRpc(new FixedString128Bytes(result), RpcTarget.Single(sender, RpcTargetUse.Temp));
            Publish();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ToggleDockJobRpc(RpcParams rpc = default)
        {
            if (Simulation == null) return;
            ulong sender = rpc.Receive.SenderClientId;
            string result = Simulation.ToggleDockJob(sender);
            if (!string.IsNullOrEmpty(result)) NoticeRpc(new FixedString128Bytes(result), RpcTarget.Single(sender, RpcTargetUse.Temp));
            Publish();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SelectDockJobRpc(DockJobKind kind, RpcParams rpc = default)
        {
            if (Simulation == null) return;
            ulong sender = rpc.Receive.SenderClientId;
            string result = Simulation.ToggleDockJob(sender, kind);
            if (!string.IsNullOrEmpty(result)) NoticeRpc(new FixedString128Bytes(result), RpcTarget.Single(sender, RpcTargetUse.Temp));
            Publish();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void AimPassengerRpc(float yaw, RpcParams rpc = default)
        {
            Simulation?.AimPassenger(rpc.Receive.SenderClientId, yaw);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void PassengerCommandRpc(int passenger, PassengerAction action, float yaw, RpcParams rpc = default)
        {
            if (Simulation == null) return;
            ulong sender = rpc.Receive.SenderClientId;
            string result = Simulation.PassengerCommand(sender, passenger, action, yaw);
            if (!string.IsNullOrEmpty(result)) NoticeRpc(new FixedString128Bytes(result), RpcTarget.Single(sender, RpcTargetUse.Temp));
            Publish();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void MoveRpc(Vector2 input, RpcParams rpc = default)
        {
            Simulation?.SetInput(rpc.Receive.SenderClientId, input);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void JumpRpc(RpcParams rpc = default)
        {
            if (Simulation == null) return;
            Simulation.Jump(rpc.Receive.SenderClientId);
            Publish();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void InteractRpc(int target, bool helmAction, RpcParams rpc = default)
        {
            if (Simulation == null) return;
            ulong sender = rpc.Receive.SenderClientId;
            string result = helmAction ? Simulation.HelmAction(sender) : Simulation.Interact(sender, target);
            if (!string.IsNullOrEmpty(result)) NoticeRpc(new FixedString128Bytes(result), RpcTarget.Single(sender, RpcTargetUse.Temp));
            Publish();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void AimCannonRpc(Vector2 aim, RpcParams rpc = default)
        {
            Simulation?.AimCannon(rpc.Receive.SenderClientId, aim);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void CannonActionRpc(Vector2 aim, bool reload, RpcParams rpc = default)
        {
            if (Simulation == null) return;
            ulong sender = rpc.Receive.SenderClientId;
            string result = reload ? Simulation.ReloadCannon(sender) : Simulation.FireCannon(sender, aim);
            NoticeRpc(new FixedString128Bytes(result), RpcTarget.Single(sender, RpcTargetUse.Temp));
            Publish();
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void NoticeRpc(FixedString128Bytes text, RpcParams rpc = default) { Notice = text.ToString(); NoticeUntil = Time.unscaledTime + 6; }
    }
}
