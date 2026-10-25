using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Unity wrapper around <see cref="ClientMotionCore"/>: samples PrototypeView input into fixed-step move commands, feeds
    /// network snapshots into the core, and shows the connection status. PrototypeView asks it for positions and falls back to its
    /// own smoothing when it has no data (menu, tests without a feed). Presentation only; the server stays authoritative.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [RequireComponent(typeof(PrototypeView))]
    public sealed class PrototypeClientMotion : MonoBehaviour
    {
        private const float PingInterval = 1f;
        private const float StaleSeconds = 1.2f, LostSeconds = 5f;

        public PrototypeView View;

        private readonly ClientMotionCore core = new ClientMotionCore();
        private Vector2 latestInput;
        private bool jumpLatched;
        private float accumulator, pingTimer;
        private DeckWorld deckWorld;
        private GUIStyle statusStyle;

        public ClientMotionCore Core => core;
        public float RttMs => core.RttMs;
        public float LossPercent => core.LossPercent;
        public float InterpolationDelay => core.Clock.Delay;

        private void Awake() { if (View == null) View = GetComponent<PrototypeView>(); }

        private void OnEnable()
        {
            var session = View != null ? View.Session : null;
            if (session == null) return;
            session.MotionSnapshotReceived += OnSnapshot;
            session.PongReceived += OnPong;
        }

        private void OnDisable()
        {
            var session = View != null ? View.Session : null;
            if (session == null) return;
            session.MotionSnapshotReceived -= OnSnapshot;
            session.PongReceived -= OnPong;
        }

        private void OnSnapshot(MotionSnapshot snapshot) => core.OnSnapshot(snapshot, Time.unscaledTime);
        private void OnPong(uint echoedMs) => core.OnPong(echoedMs, Time.unscaledTime);

        /// <summary>Latest input for the next command; a jump press is latched until a command carries it.</summary>
        public void SetInput(Vector2 input, bool jumpPressed)
        {
            latestInput = input;
            if (jumpPressed) jumpLatched = true;
        }

        private void Update()
        {
            var session = View != null ? View.Session : null;
            if (session == null || !session.IsSpawned)
            {
                core.Reset();
                return;
            }
            core.LocalSlot = session.LocalCrewIndex;
            if (core.LocalSlot < 0) return;
            var crew = session.Crew[core.LocalSlot];
            core.SetPredicting(!session.IsServer && crew.Active && !crew.AtHelm && !crew.AtCannon && !crew.AtCrane);

            accumulator = Mathf.Min(accumulator + Time.unscaledDeltaTime, .25f);
            while (accumulator >= CrewLocomotion.StepSeconds)
            {
                accumulator -= CrewLocomotion.StepSeconds;
                float factor = crew.Swimming ? VoyageSimulation.SwimSpeedFactor : crew.OnShore ? 1 : session.LocalCarriedPassengerIndex >= 0 ? .7f : crew.Carrying == ServiceItem.Ammo ? .8f : 1;
                ILocomotionWorld shipWorld = crew.Swimming ? VoyageSimulation.WaterWorld : (deckWorld ?? (deckWorld = new DeckWorld(session)));
                bool gate = VoyageSimulation.GateOpen(session.Harbor != null && session.Harbor.IsOpen, crew, session.LocalCarriedPassengerIndex >= 0);
                var packet = core.StepFixed(latestInput, jumpLatched && !crew.Swimming, factor, shipWorld, VoyageSimulation.ShoreWorld, gate);
                jumpLatched = false;
                if (session.IsServer) session.ApplyLocalMoveCommands(packet);
                else session.MoveCommandsRpc(packet);
            }
            core.Decay(Time.unscaledDeltaTime);
            core.Predictor.StepAlpha = accumulator / CrewLocomotion.StepSeconds;

            pingTimer -= Time.unscaledDeltaTime;
            if (pingTimer <= 0 && !session.IsServer)
            {
                pingTimer = PingInterval;
                session.PingRpc((uint)(Time.unscaledTime * 1000f));
            }
        }

        /// <summary>Crew position (x, jump, z) and velocity for drawing; false when the view should use its own smoothing.</summary>
        public bool TryGetCrew(int slot, out Vector3 position, out Vector3 velocity) => TryGetCrew(slot, out position, out velocity, out _);

        /// <summary>As above, plus whether the position is in quay coordinates (true) or ship coordinates (false).</summary>
        public bool TryGetCrew(int slot, out Vector3 position, out Vector3 velocity, out bool onShore)
        {
            var session = View != null ? View.Session : null;
            return core.TryGetCrew(slot, Time.unscaledTime, session != null && session.IsServer, out position, out velocity, out onShore);
        }

        /// <summary>Interpolated ship pose for clients. The host drives its own ship directly, so it gets false.</summary>
        public bool TryGetShip(out Vector3 position, out float heading, out Vector3 velocity, out float turnRate)
        {
            position = velocity = Vector3.zero; heading = turnRate = 0;
            var session = View != null ? View.Session : null;
            if (session == null || session.IsServer) return false;
            return core.TryGetShip(Time.unscaledTime, out position, out heading, out velocity, out turnRate);
        }

        // ---------------------------------------------------------------- connection status

        private void OnGUI()
        {
            var session = View != null ? View.Session : null;
            if (session == null || !session.IsSpawned || session.IsServer || !core.Clock.HasData) return;
            if (statusStyle == null) statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            GUI.depth = -60;
            float silence = core.SecondsSinceSnapshot(Time.unscaledTime);
            var previous = GUI.color;
            if (silence > StaleSeconds)
            {
                bool lost = silence > LostSeconds;
                GUI.color = new Color(0, 0, 0, .6f);
                GUI.DrawTexture(new Rect(0, Screen.height * .35f, Screen.width, 46), Texture2D.whiteTexture);
                statusStyle.alignment = TextAnchor.MiddleCenter; statusStyle.fontSize = 22;
                GUI.color = lost ? new Color(1, .3f, .25f) : new Color(1, .75f, .2f);
                GUI.Label(new Rect(0, Screen.height * .35f + 8, Screen.width, 30), lost ? "CONNECTION LOST - trying to recover..." : "Connection unstable - waiting for the server", statusStyle);
            }
            else
            {
                statusStyle.alignment = TextAnchor.MiddleLeft; statusStyle.fontSize = 12;
                bool poor = RttMs > 250 || LossPercent > 10;
                GUI.color = poor ? new Color(1, .75f, .2f) : new Color(.75f, 1f, .8f, .8f);
                GUI.Label(new Rect(12, Screen.height - 30, 420, 22), $"Ping {RttMs:0} ms  |  loss {LossPercent:0}%  |  buffer {InterpolationDelay * 1000f:0} ms", statusStyle);
            }
            GUI.color = previous;
        }

        /// <summary>The deck as the server simulation sees it: same obstacle radii, same bounds.</summary>
        private sealed class DeckWorld : ILocomotionWorld
        {
            private readonly PrototypeSession session;
            private Vector2[] stations, seats;
            public DeckWorld(PrototypeSession session) { this.session = session; }
            public Vector2 Min => -VoyageSimulation.DeckBounds;
            public Vector2 Max => VoyageSimulation.DeckBounds;

            public bool Walkable(Vector2 p)
            {
                EnsureStatic();
                float s = VoyageSimulation.StationClearance, q = VoyageSimulation.SeatClearance, r = VoyageSimulation.PassengerClearance;
                for (int i = 0; i < stations.Length; i++) if ((p - stations[i]).sqrMagnitude < s * s) return false;
                int openSeats = ShipProgression.UnlockedSeats(session.Voyage.Value.CapacityLevel);
                for (int i = 0; i < Mathf.Min(seats.Length, openSeats); i++) if ((p - seats[i]).sqrMagnitude < q * q) return false;
                var passengers = session.Passengers;
                for (int i = 0; i < passengers.Count; i++)
                {
                    var passenger = passengers[i];
                    if (passenger.Active && passenger.Motion == PassengerMotion.OnDeck &&
                        (p - new Vector2(passenger.Position.x, passenger.Position.z)).sqrMagnitude < r * r) return false;
                }
                return true;
            }

            // Stations and seats never move relative to the ship, so read them once.
            private void EnsureStatic()
            {
                if (stations != null) return;
                stations = new Vector2[session.Stations.Length];
                for (int i = 0; i < stations.Length; i++) stations[i] = session.Stations[i].DeckPosition(session.ShipRoot);
                seats = new Vector2[session.PassengerAnchors.Length];
                for (int i = 0; i < seats.Length; i++)
                {
                    Vector3 local = session.ShipRoot.InverseTransformPoint(session.PassengerAnchors[i].position);
                    seats[i] = new Vector2(local.x, local.z);
                }
            }
        }
    }
}
