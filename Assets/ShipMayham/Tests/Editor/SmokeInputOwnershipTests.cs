using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    public sealed class SmokeInputOwnershipTests
    {
        [Test] public void CompatibilityVoyageSeedLeavesTheDirectLaneClear()
        {
            var shoals = new Shoal[HazardLogic.MaxShoals];
            int count = HazardLogic.Generate(PrototypeSmokeCheck.CaptainSmokeSeed, RouteProfile.CoastalFerry, shoals);
            Assert.AreEqual(3, count);
            for (int i = 0; i < count; i++)
                Assert.Greater(Mathf.Abs(shoals[i].Center.x), shoals[i].Radius + HazardLogic.ShipRadius + 3);
        }
        [UnityTest] public IEnumerator ScriptedRpcMovementHasOneInputOwnerInTheRealScene()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/ShipMayham/Scenes/ShipPrototype.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            PrototypeSmokeCheck.ConfigureScriptedInput(hud.View);
            hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned);
            var session = hud.Session;
            float origin = session.Simulation.Crew[0].Position.y;
            uint startClock = session.Simulation.ClockMs;
            deadline = Time.realtimeSinceStartup + 3;
            while (session.Simulation.ClockMs - startClock < 650 && Time.realtimeSinceStartup < deadline)
            {
                session.MoveRpc(Vector2.up);
                yield return null;
            }
            session.MoveRpc(Vector2.zero);
            var crew = session.Simulation.Crew[0];
            Assert.That(session.Simulation.ClockMs - startClock, Is.GreaterThanOrEqualTo(650u), "The real simulation must advance.");
            float nearest = float.MaxValue;
            StationKind nearestKind = default;
            foreach (var station in session.Simulation.Stations)
            {
                float distance = Vector2.Distance(crew.Position, station.Position);
                if (distance < nearest) { nearest = distance; nearestKind = station.Kind; }
            }
            Assert.That(crew.Position.y, Is.GreaterThan(origin + .5f),
                $"RPC input lost: lobby={session.InLobby}, commandAck={crew.InputAck}, autoDriver={hud.View.GetComponent<PrototypeClientMotion>().enabled}, clockMs={session.Simulation.ClockMs - startClock}, velocity={crew.Velocity}, nearest={nearestKind}/{nearest}");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (!Application.isPlaying) yield break;
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            yield return new ExitPlayMode();
        }
    }
}
