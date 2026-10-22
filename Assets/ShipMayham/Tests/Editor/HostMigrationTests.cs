using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class HostMigrationTests
    {
        private static VoyageSimulation NewSimulation()
        {
            return new VoyageSimulation(System.Array.Empty<StationDefinition>(), new[]
            {
                new Vector2(-2, -2), new Vector2(2, -2), new Vector2(-2, 0), new Vector2(2, 0),
                new Vector2(-2, 2), new Vector2(2, 2), new Vector2(-2, 4), new Vector2(2, 4)
            });
        }

        [Test]
        public void SnapshotPreservesVoyageAndReconnectsCrewByPlayerId()
        {
            VoyageSimulation original = NewSimulation();
            Assert.AreEqual(0, original.Join(10, "player-a"));
            Assert.AreEqual(1, original.Join(11, "player-b"));
            original.State.Wallet = 137;
            original.State.Livery = ShipLivery.Seafoam;
            original.State.Position = new Vector2(14, 92);
            original.Crew[1].Position = new Vector2(1.25f, 3.5f);

            VoyageSimulation restored = NewSimulation();
            restored.ImportMigrationData(original.ExportMigrationData());

            Assert.AreEqual(137, restored.State.Wallet);
            Assert.AreEqual(ShipLivery.Seafoam, restored.State.Livery);
            Assert.AreEqual(new Vector2(14, 92), restored.State.Position);
            Assert.AreEqual(1, restored.Join(77, "player-b"));
            Assert.AreEqual(77ul, restored.Crew[1].ClientId);
            Assert.AreEqual(new Vector2(1.25f, 3.5f), restored.Crew[1].Position);

            restored.LeavePlayer("player-a");
            Assert.IsFalse(restored.Crew[0].Active);
        }

        [Test]
        public void ReassignedConnectionIdsCannotControlAnUnboundRestoredPlayer()
        {
            var original = NewSimulation();
            original.Join(0, "old-host");
            original.Join(1, "guest-a");
            original.Join(2, "guest-b");
            original.Join(3, "guest-c");
            var restored = NewSimulation();
            restored.ImportMigrationData(original.ExportMigrationData());

            Assert.AreEqual(-1, restored.FindCrew(0));
            Assert.AreEqual(-1, restored.FindCrew(ulong.MaxValue));
            Assert.AreEqual(2, restored.Join(0, "guest-b"));
            Assert.AreEqual(2, restored.FindCrew(0), "New host ID 0 must not select the departed host's slot.");
            Assert.AreEqual(3, restored.Join(1, "guest-c"));
            Assert.AreEqual(3, restored.FindCrew(1), "New connection ID 1 must not select unbound guest-a.");
            Assert.AreEqual(1, restored.Join(2, "guest-a"));
            Assert.AreEqual(1, restored.FindCrew(2));
        }

        [Test]
        public void MigrationHandlerAppliesOnePendingSnapshotToNewHost()
        {
            VoyageSimulation original = NewSimulation();
            original.Join(10, "host-player");
            original.State.Wallet = 88;
            var sourceHandler = new VoyageMigrationDataHandler(() => original);
            byte[] data = sourceHandler.Generate();

            var receivingHandler = new VoyageMigrationDataHandler(() => null);
            receivingHandler.Apply(data);
            VoyageSimulation newHost = NewSimulation();

            Assert.IsTrue(VoyageMigrationDataHandler.RestorePending(newHost));
            Assert.AreEqual(88, newHost.State.Wallet);
            Assert.IsFalse(VoyageMigrationDataHandler.RestorePending(newHost));
        }
    }
}
