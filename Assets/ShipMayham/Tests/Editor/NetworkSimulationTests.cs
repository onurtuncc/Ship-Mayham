using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    /// <summary>
    /// Runs the real server simulation and the real client netcode core against a simulated network (latency, jitter, loss,
    /// reordering, TCP-style stalls) in virtual time, and measures what the player would see.
    /// </summary>
    public class NetworkSimulationTests
    {
        private const ulong LocalClient = 10, RemoteClient = 11;
        private const float RenderFrame = 1f / 144f, ServerStep = .02f;

        // ---------------------------------------------------------------- the simulated link

        private sealed class Link<T>
        {
            private struct Item { public double Deliver; public long Order; public T Payload; }
            private readonly List<Item> queue = new List<Item>();
            private readonly System.Random random;
            private long order;
            private double lastDelivery;
            private double stallUntil, nextStall;

            public float OneWay = .05f, Jitter, Loss;
            public bool InOrderReliable;              // TCP-like: nothing is dropped, nothing overtakes, loss becomes a stall
            public float StallEvery, StallSeconds;

            public Link(int seed) { random = new System.Random(seed); nextStall = double.MaxValue; }

            public void Send(double now, T payload)
            {
                if (InOrderReliable && StallEvery > 0 && nextStall == double.MaxValue) nextStall = now + StallEvery;
                if (InOrderReliable && now >= nextStall) { stallUntil = now + StallSeconds; nextStall = now + StallEvery; }
                if (!InOrderReliable && random.NextDouble() < Loss) return;
                double deliver = now + OneWay + (random.NextDouble() * 2 - 1) * Jitter;
                if (InOrderReliable)
                {
                    if (random.NextDouble() < Loss) deliver += .2;                  // retransmission timeout
                    deliver = Math.Max(deliver, Math.Max(stallUntil, lastDelivery));
                    lastDelivery = deliver;
                }
                queue.Add(new Item { Deliver = deliver, Order = order++, Payload = payload });
            }

            public List<T> Receive(double now)
            {
                var ready = new List<Item>();
                for (int i = queue.Count - 1; i >= 0; i--) if (queue[i].Deliver <= now) { ready.Add(queue[i]); queue.RemoveAt(i); }
                ready.Sort((a, b) => a.Deliver != b.Deliver ? a.Deliver.CompareTo(b.Deliver) : a.Order.CompareTo(b.Order));
                return ready.ConvertAll(item => item.Payload);
            }
        }

        // ---------------------------------------------------------------- a deck for the client

        private sealed class TestDeck : ILocomotionWorld
        {
            private readonly VoyageSimulation sim;
            public TestDeck(VoyageSimulation sim) { this.sim = sim; }
            public Vector2 Min => -VoyageSimulation.DeckBounds;
            public Vector2 Max => VoyageSimulation.DeckBounds;
            public bool Walkable(Vector2 p)
            {
                foreach (var station in sim.Stations) if ((p - station.Position).sqrMagnitude < VoyageSimulation.StationClearance * VoyageSimulation.StationClearance) return false;
                foreach (var seat in sim.Seats) if ((p - seat).sqrMagnitude < VoyageSimulation.SeatClearance * VoyageSimulation.SeatClearance) return false;
                return true;
            }
        }

        // ---------------------------------------------------------------- the world under test

        private sealed class Scenario
        {
            public readonly VoyageSimulation Server;
            public readonly ClientMotionCore Client = new ClientMotionCore();
            public readonly Link<MoveCommandPacket> Up;
            public readonly Link<MotionSnapshot> Down;
            public readonly int LocalSlot, RemoteSlot;
            public double Now;
            public float ClientClockOffset = 12345f;
            public float CommandRateMultiplier = 1;       // >1 simulates a client trying to run faster than real time
            public readonly List<(double time, Vector2 position)> ServerHistory = new List<(double, Vector2)>();
            private readonly TestDeck deck;
            private double nextServerStep, nextFixed;
            private int serverSteps;
            private ushort seq;
            private double nextRenderFrame;
            public int CommandsSent;

            public Scenario(int seed, float oneWay, float jitter, float loss)
            {
                Server = new VoyageSimulation(new[] {
                    new StationDefinition(StationKind.Manifest, new Vector2(-4,-4)), new StationDefinition(StationKind.Helm, new Vector2(0,6.8f)),
                    new StationDefinition(StationKind.Food, new Vector2(-4,0)), new StationDefinition(StationKind.Drink, new Vector2(-4,3)),
                    new StationDefinition(StationKind.Repair, new Vector2(4,-3)), new StationDefinition(StationKind.EngineUpgrade, new Vector2(3.8f,-7.8f)),
                    new StationDefinition(StationKind.CapacityUpgrade, new Vector2(0,-7.8f)), new StationDefinition(StationKind.ComfortUpgrade, new Vector2(-3.8f,-7.8f)) },
                    new[] { new Vector2(-1.9f,-1.5f), new Vector2(1.9f,-1.5f), new Vector2(-1.9f,.6f), new Vector2(1.9f,.6f),
                        new Vector2(-1.9f,2.7f), new Vector2(1.9f,2.7f), new Vector2(-1.9f,4.8f), new Vector2(1.9f,4.8f) });
                LocalSlot = Server.Join(LocalClient); RemoteSlot = Server.Join(RemoteClient);
                deck = new TestDeck(Server);
                Up = new Link<MoveCommandPacket>(seed) { OneWay = oneWay, Jitter = jitter, Loss = loss };
                Down = new Link<MotionSnapshot>(seed + 1) { OneWay = oneWay, Jitter = jitter, Loss = loss };
                Client.LocalSlot = LocalSlot;
                Client.SetPredicting(true);
                Server.Crew[LocalSlot].Position = new Vector2(0, -5);
                Server.Crew[RemoteSlot].Position = new Vector2(3, -6);
                Client.Predictor.Resync(new CrewMotionState { Position = new Vector2(0, -5) }, 0);
            }

            /// <summary>Remote crew input comes from the server side directly (held-input model), so the remote path is pure interpolation.</summary>
            public Func<double, Vector2> LocalInput = t => Vector2.zero, RemoteInput = t => Vector2.zero;
            public Func<double, bool> LocalJump = t => false;
            public Action<Scenario, float, float> OnFrame;     // (scenario, frame start time, clientTime)

            public void Run(double seconds)
            {
                double end = Now + seconds;
                while (Now < end)
                {
                    Now += RenderFrame;
                    // Server: fixed steps.
                    while (nextServerStep <= Now)
                    {
                        foreach (var packet in Up.Receive(nextServerStep)) Server.ApplyMoveCommands(LocalClient, packet);
                        Server.SetInput(RemoteClient, RemoteInput(nextServerStep));
                        Server.Tick(ServerStep);
                        ServerHistory.Add((Server.ClockMs / 1000.0, Server.Crew[RemoteSlot].Position));
                        serverSteps++;
                        if (serverSteps % 2 == 0)
                        {
                            Server.StampTime();
                            Down.Send(nextServerStep, MotionSnapshot.From(Server, seq++));
                        }
                        nextServerStep += ServerStep;
                    }
                    float clientTime = (float)(Now + ClientClockOffset);
                    foreach (var snapshot in Down.Receive(Now)) Client.OnSnapshot(snapshot, clientTime);
                    // Client: fixed 30 Hz command steps.
                    while (nextFixed <= Now)
                    {
                        int repeats = Mathf.Max(1, Mathf.RoundToInt(CommandRateMultiplier));
                        for (int r = 0; r < repeats; r++)
                        {
                            var packet = Client.StepFixed(LocalInput(nextFixed), LocalJump(nextFixed), 1, deck);
                            Up.Send(nextFixed, packet);
                            CommandsSent++;
                        }
                        nextFixed += CrewLocomotion.StepSeconds;
                    }
                    Client.Decay(RenderFrame);
                    Client.Predictor.StepAlpha = Mathf.Clamp01(1f - (float)((nextFixed - Now) / CrewLocomotion.StepSeconds));
                    OnFrame?.Invoke(this, (float)Now, clientTime);
                }
            }

            public Vector2 ServerPositionAt(double serverSeconds)
            {
                for (int i = ServerHistory.Count - 1; i > 0; i--)
                    if (ServerHistory[i - 1].time <= serverSeconds)
                    {
                        var a = ServerHistory[i - 1]; var b = ServerHistory[i];
                        double span = Math.Max(1e-6, b.time - a.time);
                        return Vector2.Lerp(a.position, b.position, Mathf.Clamp01((float)((serverSeconds - a.time) / span)));
                    }
                return ServerHistory.Count > 0 ? ServerHistory[0].position : Vector2.zero;
            }
        }

        private static Vector2 WalkRight(double t) => t >= 1 && t < 2.2 ? Vector2.right : Vector2.zero;

        // ---------------------------------------------------------------- prediction

        [Test] public void LocalMovementStartsImmediatelyDespiteHighLatency()
        {
            var s = new Scenario(1, .1f, .01f, 0);                        // 200 ms round trip
            s.LocalInput = t => t >= 1 ? Vector2.right : Vector2.zero;
            double start = -1;
            Vector2 initial = s.Server.Crew[s.LocalSlot].Position;
            s.OnFrame = (sc, t, c) => { if (start < 0 && sc.Client.Predictor.RenderPosition.x > initial.x + .02f) start = t; };
            s.Run(2);
            Assert.Greater(start, 0, "The local player must move.");
            Assert.Less(start - 1, .08, "Prediction should respond within about two command steps, not after the round trip.");
            Assert.Greater(s.Server.Crew[s.LocalSlot].Position.x, initial.x, "The server follows.");
        }

        [Test] public void PredictionAgreesWithTheServerAfterTheRoundTrip()
        {
            var s = new Scenario(2, .06f, .008f, 0);
            s.LocalInput = WalkRight;
            float worstReconcile = 0;
            int reconciles = 0;
            s.OnFrame = (sc, t, c) => { worstReconcile = Mathf.Max(worstReconcile, sc.Client.Predictor.LastError); reconciles++; };
            s.Run(4);
            Assert.Less(worstReconcile, .02f, "Without loss the predicted state matches the server's: corrections are negligible.");
            Assert.AreEqual(s.Server.Crew[s.LocalSlot].Position.x, s.Client.Predictor.State.Position.x, .01f, "Both end at the same place.");
        }

        [Test] public void PredictedMotionIsSmoothEvenWithJitterAndPacketLoss()
        {
            var s = new Scenario(3, .06f, .03f, .08f);                     // 120 ms RTT, +-30 ms jitter, 8% loss each way
            s.LocalInput = t => t >= 1 && t < 5 ? new Vector2(Mathf.Sin((float)t), 1).normalized : Vector2.zero;
            Vector2 previous = default; bool have = false; float worstJump = 0;
            s.OnFrame = (sc, t, c) =>
            {
                var p = sc.Client.Predictor.RenderPosition;
                if (have && t > 1.5) worstJump = Mathf.Max(worstJump, (p - previous).magnitude);
                previous = p; have = true;
            };
            s.Run(6);
            // At 4.2 m/s a frame is 2.9 cm. Allow generous headroom for corrections, but no visible pops.
            Assert.Less(worstJump, .10f, $"Worst per-frame movement was {worstJump:0.000} m.");
            Assert.AreEqual(s.Server.Crew[s.LocalSlot].Position.x, s.Client.Predictor.State.Position.x, .03f);
            Assert.AreEqual(s.Server.Crew[s.LocalSlot].Position.y, s.Client.Predictor.State.Position.y, .03f);
        }

        [Test] public void WalkingIntoAStationStaysConsistentBetweenPredictionAndServer()
        {
            var s = new Scenario(4, .05f, .01f, .02f);
            s.Server.Crew[s.LocalSlot].Position = new Vector2(-2.4f, 0);        // Food counter at (-4, 0)
            s.Client.Predictor.Resync(new CrewMotionState { Position = new Vector2(-2.4f, 0) }, 0);
            s.LocalInput = t => Vector2.left;
            float worst = 0;
            s.OnFrame = (sc, t, c) => { if (t > 2) worst = Mathf.Max(worst, sc.Client.Predictor.PositionOffset.magnitude); };
            s.Run(5);
            Assert.Less(worst, .03f, "Prediction and server collide identically, so there is nothing to correct.");
            Assert.AreEqual(s.Server.Crew[s.LocalSlot].Position.x, s.Client.Predictor.State.Position.x, .02f);
        }

        [Test] public void JumpsArePredictedAndAgreeWithTheServer()
        {
            var s = new Scenario(5, .05f, .01f, 0);
            s.LocalJump = t => t > 1.0 && t < 1.04;
            float peak = 0;
            s.OnFrame = (sc, t, c) => peak = Mathf.Max(peak, sc.Client.Predictor.RenderJump);
            s.Run(3);
            Assert.Greater(peak, .3f, "The jump is visible locally within the round trip.");
            Assert.AreEqual(0, s.Server.Crew[s.LocalSlot].JumpHeight, 1e-4f, "and has landed on the server too");
            Assert.AreEqual(0, s.Client.Predictor.State.JumpHeight, 1e-4f);
        }

        [Test] public void LossBeyondTheRedundancyIsCorrectedWithoutPopping()
        {
            var s = new Scenario(6, .05f, .02f, .5f);                      // brutal 50% loss each way
            s.LocalInput = t => t >= 1 ? Vector2.right * Mathf.Sign(Mathf.Sin((float)t * 1.3f)) : Vector2.zero;
            float worstFrame = 0; Vector2 previous = default; bool have = false;
            s.OnFrame = (sc, t, c) =>
            {
                var p = sc.Client.Predictor.RenderPosition;
                if (have && t > 1.5) worstFrame = Mathf.Max(worstFrame, (p - previous).magnitude);
                previous = p; have = true;
            };
            s.Run(8);
            Assert.Less(worstFrame, .25f, "Even at 50% loss corrections are eased, never teleports.");
            Assert.Greater(s.Client.Core().SnapshotsReceived, 20, "Some snapshots always get through.");
        }

        [Test] public void ClientsCannotWalkFasterThanTheRulesAllow()
        {
            var s = new Scenario(7, .05f, .005f, 0) { CommandRateMultiplier = 3 };     // triple-speed command stream
            s.LocalInput = t => t >= 1 ? Vector2.right : Vector2.zero;
            s.Server.Crew[s.LocalSlot].Position = new Vector2(-5, -5);
            s.Run(2.0);
            float travelled = s.Server.Crew[s.LocalSlot].Position.x + 5;
            // Walking time is about 1 s at 4.2 m/s; the budget allows a small burst margin but nowhere near 3x.
            Assert.Less(travelled, 4.2f * 1.0f * 1.5f + CrewKinematics.WalkSpeed * VoyageSimulation.MoveBudgetCap);
        }

        // ---------------------------------------------------------------- interpolation of other players

        private static string LastDetail = "";
        private static float FrameSpeedWobble(Scenario s, double from, double to, float expectedSpeed, out float freezeFraction)
        {
            var speeds = new List<float>(); var times = new List<double>(); var infos = new List<string>();
            Vector3 previous = default; bool have = false; int frozen = 0;
            s.OnFrame = (sc, t, c) =>
            {
                if (!sc.Client.TryGetCrew(sc.RemoteSlot, c, false, out Vector3 p, out _)) return;
                if (have && t >= from && t <= to)
                {
                    float speed = (p - previous).magnitude / RenderFrame;
                    speeds.Add(speed);
                    times.Add(t); infos.Add($"r={sc.Client.Clock.RenderTime(c):0.000} n={sc.Client.NewestSampleTime(sc.RemoteSlot):0.000} d={sc.Client.Clock.Delay:0.000}");
                    if (speed < expectedSpeed * .4f) frozen++;
                }
                previous = p; have = true;
            };
            s.Run(to + .2);
            freezeFraction = speeds.Count == 0 ? 1 : (float)frozen / speeds.Count;
            float worst = 0;
            LastDetail = "";
            for (int i = 0; i < speeds.Count; i++)
            {
                float dev = Mathf.Abs(speeds[i] - expectedSpeed) / expectedSpeed;
                if (dev > .25f && LastDetail.Length < 400) LastDetail += $" [t={times[i]:0.000} v={speeds[i]:0.00} {infos[i]}]";
                worst = Mathf.Max(worst, dev);
            }
            return worst;
        }

        [Test] public void RemotePlayersMoveSmoothlyOnAJitteryLossyLink()
        {
            var s = new Scenario(8, .06f, .04f, .06f);
            s.Server.Crew[s.RemoteSlot].Position = new Vector2(-5, 8f);       // long clear run along the stern
            s.RemoteInput = t => t >= .5 && t < 4 ? Vector2.right : Vector2.zero;
            float wobble = FrameSpeedWobble(s, 1.5, 3.2, CrewKinematics.WalkSpeed, out float frozen);
            Assert.Less(frozen, .02f, "Remote players must not freeze between snapshots.");
            Assert.Less(wobble, .35f, LastDetail + "Frame-to-frame speed stays within 35% of the real speed despite 40 ms jitter and 6% loss.");
        }

        [Test] public void RemotePlayersAreShownWhereTheServerHadThemAtRenderTime()
        {
            var s = new Scenario(9, .05f, .02f, .02f);
            s.Server.Crew[s.RemoteSlot].Position = new Vector2(-5, 8f);
            s.RemoteInput = t => t >= .5 && t < 3 ? Vector2.right : Vector2.zero;
            float worst = 0;
            s.OnFrame = (sc, t, c) =>
            {
                if (t < 2 || t > 3.2 || !sc.Client.TryGetCrew(sc.RemoteSlot, c, false, out Vector3 p, out _)) return;
                Vector2 truth = sc.ServerPositionAt(sc.Client.Clock.RenderTime(c) - sc.ClientClockOffset + sc.ClientClockOffset - 0 /* same clock domain via offset estimate */ );
                worst = Mathf.Max(worst, Mathf.Abs(p.x - truth.x));
            };
            s.Run(3.4);
            // The estimated clock is off by roughly the one-way latency (the smallest delay defines it), so compare allowing for that.
            Assert.Less(worst, 0.5f);
        }

        [Test] public void ClockOffsetsBetweenClientAndServerDoNotMatter()
        {
            foreach (float offset in new[] { 0f, 777f, 5000f })
            {
                var s = new Scenario(10, .05f, .01f, 0) { ClientClockOffset = offset };
                s.Server.Crew[s.RemoteSlot].Position = new Vector2(-5, 8f);
                s.RemoteInput = t => t >= .5 && t < 3 ? Vector2.right : Vector2.zero;
                float wobble = FrameSpeedWobble(s, 1.5, 2.5, CrewKinematics.WalkSpeed, out float frozen);
                Assert.Less(wobble, .3f, "offset " + offset + LastDetail);
                Assert.Less(frozen, .02f, "offset " + offset);
            }
        }

        [Test] public void TcpStallsAreRiddenOutByExtrapolationAndTheBufferGrows()
        {
            var s = new Scenario(11, .06f, .01f, .03f);
            s.Down.InOrderReliable = true; s.Down.StallEvery = 3f; s.Down.StallSeconds = .35f;     // WebSocket-like head-of-line blocking
            s.Up.InOrderReliable = true;
            s.Server.Crew[s.RemoteSlot].Position = new Vector2(-5, 8f);
            s.RemoteInput = t => t >= .5 && t < 9 ? Vector2.right * (t < 4.5 ? 1 : -1) : Vector2.zero;
            float worstJump = 0; Vector3 previous = default; bool have = false;
            s.OnFrame = (sc, t, c) =>
            {
                if (!sc.Client.TryGetCrew(sc.RemoteSlot, c, false, out Vector3 p, out _)) return;
                if (have && t > 2) worstJump = Mathf.Max(worstJump, (p - previous).magnitude);
                previous = p; have = true;
            };
            s.Run(9);
            Assert.Less(worstJump, .12f, "A 350 ms stall must not turn into a visible teleport when data resumes.");
            Assert.Greater(s.Client.Clock.Delay, SnapshotClock.MinDelay, "The buffer should have grown to cover the stalls.");
        }

        [Test] public void TheShipIsInterpolatedSmoothlyToo()
        {
            // A small clock offset: single-precision client time at 12000 s only resolves 1 ms, which would pollute a per-frame measurement.
            var s = new Scenario(12, .05f, .03f, .04f) { ClientClockOffset = 0 };
            s.Server.Crew[s.LocalSlot].AtHelm = true;
            s.Server.State.Phase = VoyagePhase.Sailing;
            s.LocalInput = t => new Vector2(0, 1);
            var speeds = new List<float>(); var stamps = new List<string>(); Vector3 previous = default; bool have = false; float turnSeen = 0;
            s.OnFrame = (sc, t, c) =>
            {
                if (!sc.Client.TryGetShip(c, out Vector3 p, out float h, out _, out float turn)) return;
                if (have && t > 4) { speeds.Add((p - previous).magnitude / RenderFrame); stamps.Add($"t={t:0.000} r={sc.Client.Clock.RenderTime(c):0.000} d={sc.Client.Clock.Delay:0.000}"); }
                previous = p; have = true; turnSeen = Mathf.Max(turnSeen, Mathf.Abs(turn));
            };
            s.Run(8);
            Assert.Greater(speeds.Count, 100);
            float mean = 0; foreach (float v in speeds) mean += v; mean /= speeds.Count;
            // The ship may still be accelerating, so judge frame-to-frame changes rather than distance from the average.
            float worst = 0; for (int i = 1; i < speeds.Count; i++) worst = Mathf.Max(worst, Mathf.Abs(speeds[i] - speeds[i - 1]) / Mathf.Max(.1f, mean));
            int at = 0; for (int i = 1; i < speeds.Count; i++) if (Mathf.Abs(speeds[i] - speeds[i - 1]) >= worst * Mathf.Max(.1f, mean) - 1e-4f) at = i;
            Assert.Less(worst, .2f, $"Ship speed is steady frame to frame. mean {mean:0.00} at {stamps[at]} prev {speeds[at - 1]:0.00} cur {speeds[at]:0.00}");
        }

        [Test] public void ConnectionStatisticsTrackLossAndLatency()
        {
            var s = new Scenario(13, .05f, .005f, .2f);
            s.Run(6);
            Assert.That(s.Client.LossPercent, Is.InRange(8f, 35f), "About 20% of snapshots are lost.");
            s.Client.OnPong((uint)((s.Now + s.ClientClockOffset - .1) * 1000), (float)(s.Now + s.ClientClockOffset));
            Assert.That(s.Client.RttMs, Is.InRange(80f, 120f));
        }
    }

    internal static class ClientMotionExtensions
    {
        public static ClientMotionCore Core(this ClientMotionCore core) => core;
    }
}
