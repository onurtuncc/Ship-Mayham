using System;
using Unity.Netcode;

namespace ShipMayham
{
    /// <summary>
    /// Socket-free transport for a single local host. NGO dispatches the host's RPCs
    /// internally; no remote connection can be opened through this transport.
    /// </summary>
    public sealed class SoloTransport : NetworkTransport
    {
        public override ulong ServerClientId => 0;
        public override void Initialize(NetworkManager networkManager = null) { }
        public override bool StartServer() => true;
        public override bool StartClient() => false;
        public override void Shutdown() { }
        public override ulong GetCurrentRtt(ulong clientId) => 0;
        public override void DisconnectLocalClient() { }
        public override void DisconnectRemoteClient(ulong clientId) { }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            clientId = 0;
            payload = default;
            receiveTime = 0;
            return NetworkEvent.Nothing;
        }

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
        {
            throw new InvalidOperationException("Solo mode cannot send messages to a remote player.");
        }
    }
}
