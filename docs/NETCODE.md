# Movement netcode

Network protocol version **14** (14 adds `SignalRpc`/`SignalBroadcastRpc` for pings and emotes; 13 adds `UnstuckRpc`; 12 adds `BuyUpgradeRpc` for the purser; 11 added the session phase and the per-slot nickname/ready list for the lobby) (`PrototypeHud.NetworkProtocolVersion`). The host runs the authoritative `VoyageSimulation`; clients only present state.

## Data flow

| Direction | Message | Delivery | Rate |
| --- | --- | --- | --- |
| client → host | `MoveCommandsRpc` (`MoveCommandPacket`: last 6 commands, sequence numbers, quantised input, jump flag) | unreliable | 30 Hz |
| host → clients | `MotionSnapshotRpc` (`MotionSnapshot`: server ms clock, ship pose, crew position/velocity/jump, per-player input ack) | unreliable | ~25 Hz |
| client → host → client | `PingRpc` / `PongRpc` | unreliable | 1 Hz |

Everything slower (stations, passengers, upgrades, voyage state) stays on reliable `NetworkVariable`s. Movement no longer rides on them, so one lost packet cannot block the walking data behind it.

## Local player: prediction and reconciliation

- `CrewLocomotion` is the single, deterministic walking model. `VoyageSimulation` and `LocalCrewPredictor` both call it with the same 1/30 s step, so server and client agree.
- The client applies each command locally at once (`LocalCrewPredictor.Step`) and keeps unacknowledged commands.
- On every snapshot the predictor rewinds to the server state at the acknowledged command, replays the rest, and eases the difference away through a decaying visual offset. Errors under 4 mm are ignored, errors over 2.5 m snap.
- Each packet repeats the last six commands, so isolated loss costs nothing; the server dedups by sequence number.
- The server caps how much walking time a client may spend (`MoveBudgetCap`) so a modified client cannot run faster than the rules.
- Prediction is off while at the helm, a cannon or the crane (the server drives those) and on the host (which already holds the truth).

## Everyone else: interpolation

- `SnapshotClock` estimates the server clock from the smallest observed arrival offset and sizes the render delay (80–400 ms) from measured jitter and the largest recent send gap. The offset and delay are slewed, never stepped, so rendered speed does not wobble.
- `SnapshotBuffer` holds time-ordered samples, tolerates reordering and duplicates, and samples with cubic Hermite interpolation (position and velocity). Past the newest sample it extrapolates briefly at full speed, then eases to rest, so a late packet looks like continued motion, not braking.
- `ClientMotionCore.Follow` limits how quickly the drawn position can catch up after a stall, and only real teleports are taken at once.
- The ship is interpolated the same way on clients; the host draws its own ship directly.

## Connection feedback

`PrototypeClientMotion` shows ping, packet loss and the current buffer size, and a "Connection unstable" / "CONNECTION LOST" banner when snapshots stop arriving (1.2 s / 5 s).

## Known limits

- WebGL uses WebSocket (TCP). Head-of-line blocking there delays all packets together; the clock grows its buffer and extrapolation covers short stalls (tested at 350 ms).
- The player at the helm is not predicted: the ship is shown interpolated, so steering has about one round trip plus the interpolation delay of latency.
- A crew member who is carrying something is predicted with the carry speed factor the client knows about; a mismatch is corrected by reconciliation.

## Tests

`Tests/Editor/NetworkSimulationTests.cs` drives the real `VoyageSimulation` and `ClientMotionCore` through a virtual-time network simulator (latency, jitter, loss, reordering, TCP-style stalls) and checks: input response latency, prediction/server agreement, smooth correction under jitter and 50% loss, collision consistency, jump, speed-hack cap, remote smoothness, clock-offset independence, stall recovery, ship smoothness and connection statistics.
