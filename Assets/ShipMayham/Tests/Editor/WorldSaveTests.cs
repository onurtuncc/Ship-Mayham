using System;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public sealed class WorldSaveTests
    {
        private bool forcedBefore;

        [SetUp]
        public void SetUp()
        {
            forcedBefore = WorldSave.ForceEnabled;
            WorldSave.ForceEnabled = true;
            WorldSave.Clear();
            WorldSave.Discard();
        }

        [TearDown]
        public void TearDown()
        {
            WorldSave.Clear();
            WorldSave.Discard();
            WorldSave.ForceEnabled = forcedBefore;
        }

        private static VoyageSimulation NewSim()
        {
            return new VoyageSimulation(new[]
            {
                new StationDefinition(StationKind.Manifest, new Vector2(-4, -4)),
                new StationDefinition(StationKind.Helm, new Vector2(0, 6.8f)),
            }, new[]
            {
                new Vector2(-1.9f, -1.5f), new Vector2(1.9f, -1.5f), new Vector2(-1.9f, .6f), new Vector2(1.9f, .6f),
                new Vector2(-1.9f, 2.7f), new Vector2(1.9f, 2.7f), new Vector2(-1.9f, 4.8f), new Vector2(1.9f, 4.8f)
            });
        }

        private static WorldSaveInfo Info(VoyagePhase phase = VoyagePhase.Boarding) => new WorldSaveInfo
        {
            SavedUnix = 1_000_000, Voyage = 4, Wallet = 312, Reputation = 2, Crew = 3, Phase = phase, Host = "Captain Pip"
        };

        private static byte[] Bytes(int length = 2000)
        {
            var data = new byte[length];
            for (int i = 0; i < data.Length; i++) data[i] = (byte)(i * 31 % 251);
            return data;
        }

        // ------------------------------------------------------------ format

        [Test]
        public void ASaveRoundTripsAndKeepsItsSummary()
        {
            byte[] data = Bytes();
            string text = WorldSave.Encode(data, Info());
            Assert.IsTrue(WorldSave.TryDecode(text, out byte[] back, out var info));
            CollectionAssert.AreEqual(data, back);
            Assert.AreEqual(4, info.Voyage);
            Assert.AreEqual(312, info.Wallet);
            Assert.AreEqual(2, info.Reputation);
            Assert.AreEqual("Captain Pip", info.Host);
            Assert.AreEqual(VoyagePhase.Boarding, info.Phase);
            Assert.IsTrue(WorldSave.TryPeek(text, out var peeked));
            Assert.AreEqual(info.Voyage, peeked.Voyage);
        }

        [Test]
        public void TheSnapshotIsCompressed()
        {
            byte[] data = new byte[100_000];                        // very repetitive, like a real snapshot
            Assert.Less(WorldSave.Encode(data, Info()).Length, 2000);
        }

        [TestCase(null)] [TestCase("")] [TestCase("not json")] [TestCase("{}")] [TestCase("{\"Version\":1}")]
        public void GarbageIsRefusedWithoutThrowing(string text)
        {
            Assert.IsFalse(WorldSave.TryPeek(text, out _));
            Assert.IsFalse(WorldSave.TryDecode(text, out byte[] snapshot, out _));
            Assert.IsNull(snapshot);
        }

        [Test]
        public void AFutureOrCorruptSaveIsRefused()
        {
            string good = WorldSave.Encode(Bytes(), Info());
            Assert.IsFalse(WorldSave.TryPeek(good.Replace("\"Version\":1", "\"Version\":99"), out _), "unknown format version");
            Assert.IsFalse(WorldSave.TryDecode(good.Replace("\"Data\":\"", "\"Data\":\"!!"), out _, out _), "damaged payload");
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try { Assert.IsFalse(WorldSave.TryDecode(good.Replace("\"Data\":\"", "\"Data\":\"AAAA"), out _, out _), "payload that is not gzip"); }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
        }

        [Test]
        public void AWorldAtSeaIsNeverSavedOrRestored()
        {
            Assert.IsFalse(WorldSave.IsSavablePhase(VoyagePhase.Sailing));
            Assert.IsFalse(WorldSave.Save(Bytes(), Info(VoyagePhase.Sailing)));
            Assert.IsFalse(WorldSave.Exists);
            string text = WorldSave.Encode(Bytes(), Info(VoyagePhase.Sailing));
            Assert.IsFalse(WorldSave.TryPeek(text, out _), "a hand-edited save that claims to be at sea is refused");
            foreach (var phase in new[] { VoyagePhase.Boarding, VoyagePhase.Arrived, VoyagePhase.Failed }) Assert.IsTrue(WorldSave.IsSavablePhase(phase));
        }

        [Test]
        public void ADecompressionBombIsRefused()
        {
            byte[] huge;
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionMode.Compress, true)) gzip.Write(new byte[WorldSave.MaxPayloadBytes + 100_000], 0, WorldSave.MaxPayloadBytes + 100_000);
                huge = output.ToArray();
            }
            string text = JsonUtility.ToJson(new BombEnvelope { Version = WorldSave.FormatVersion, Voyage = 1, Phase = (int)VoyagePhase.Boarding, Data = Convert.ToBase64String(huge) });
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try { Assert.IsFalse(WorldSave.TryDecode(text, out _, out _)); }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
        }

        [Serializable]
        private sealed class BombEnvelope { public int Version, Voyage, Phase; public string Data; }

        [Test]
        public void EncodingNothingIsAnError()
        {
            Assert.Throws<ArgumentException>(() => WorldSave.Encode(null, Info()));
            Assert.Throws<ArgumentException>(() => WorldSave.Encode(new byte[0], Info()));
        }

        // ------------------------------------------------------------ the text the menu shows

        [TestCase(0, "just now")] [TestCase(89, "just now")] [TestCase(120, "2 min ago")] [TestCase(3 * 3600, "3 h ago")] [TestCase(2 * 86400, "2 d ago")]
        public void AgesAreReadable(long seconds, string expected) => Assert.AreEqual(expected, WorldSave.Age(seconds));

        [Test]
        public void TheDescriptionNamesTheHostTheVoyageAndTheProgress()
        {
            string text = WorldSave.Describe(Info(), 1_000_000 + 600);
            StringAssert.Contains("Captain Pip", text);
            StringAssert.Contains("Voyage 4", text);
            StringAssert.Contains("312 coins", text);
            StringAssert.Contains("2 reputation", text);
            StringAssert.Contains("10 min ago", text);
            var anonymous = Info();
            anonymous.Host = null;
            StringAssert.StartsWith("Voyage 4", WorldSave.Describe(anonymous, 1_000_000));
        }

        // ------------------------------------------------------------ storage

        [Test]
        public void SavingStoresTheWorldAndStagingQueuesItForTheNextSession()
        {
            var sim = NewSim();
            sim.Join(10);
            sim.State.Wallet = 777;
            sim.State.Voyage = 5;
            var info = WorldSaveInfo.From(sim.State, 1, "Pip", 123);
            Assert.IsTrue(WorldSave.Save(sim.ExportMigrationData(), info));
            Assert.IsTrue(WorldSave.TryPeekStored(out var stored));
            Assert.AreEqual(5, stored.Voyage);
            Assert.IsTrue(WorldSave.Stage(RestoreMode.Solo));
            Assert.AreEqual(RestoreMode.Solo, WorldSave.PendingRestore);

            var next = NewSim();
            Assert.IsTrue(VoyageMigrationDataHandler.RestorePending(next));
            Assert.AreEqual(777, next.State.Wallet);
            Assert.AreEqual(5, next.State.Voyage);
            Assert.AreEqual(RestoreMode.Solo, WorldSave.TakeRestore());
            Assert.AreEqual(RestoreMode.None, WorldSave.TakeRestore(), "a restore is consumed once");
        }

        [Test]
        public void StagingWithoutASaveDoesNothing()
        {
            Assert.IsFalse(WorldSave.Stage(RestoreMode.Solo));
            Assert.IsFalse(WorldSave.Stage(RestoreMode.None));
            Assert.AreEqual(RestoreMode.None, WorldSave.PendingRestore);
            Assert.IsFalse(VoyageMigrationDataHandler.RestorePending(NewSim()));
        }

        [Test]
        public void StartingANewWorldDropsAStagedRestore()
        {
            var sim = NewSim();
            sim.Join(10);
            WorldSave.Save(sim.ExportMigrationData(), WorldSaveInfo.From(sim.State, 1, "Pip", 1));
            Assert.IsTrue(WorldSave.Stage(RestoreMode.Host));
            WorldSave.Discard();
            Assert.AreEqual(RestoreMode.None, WorldSave.PendingRestore);
            Assert.IsFalse(VoyageMigrationDataHandler.RestorePending(NewSim()), "nothing is left waiting for the new world");
        }

        [Test]
        public void ErasingRemovesTheSave()
        {
            var sim = NewSim();
            sim.Join(10);
            WorldSave.Save(sim.ExportMigrationData(), WorldSaveInfo.From(sim.State, 1, "Pip", 1));
            Assert.IsTrue(WorldSave.Exists);
            WorldSave.Clear();
            Assert.IsFalse(WorldSave.Exists);
        }

        [Test]
        public void BatchRunsDoNotTouchTheRealSave()
        {
            WorldSave.ForceEnabled = false;                            // the default in a batch run
            Assert.IsFalse(WorldSave.Save(Bytes(), Info()));
            Assert.IsFalse(WorldSave.Exists);
            Assert.IsFalse(WorldSave.Stage(RestoreMode.Solo));
        }

        // ------------------------------------------------------------ the restored party

        private static byte[] ThreePlayerWorld(out VoyageSimulation original)
        {
            original = NewSim();
            original.Join(10, "host-id");
            original.Join(11, "friend-id");
            original.Join(12, null);                                   // someone without a stable identity
            original.State.Wallet = 500;
            original.State.Voyage = 3;
            original.Crew[1].Position = new Vector2(1, 1);
            return original.ExportMigrationData();
        }

        [Test]
        public void ASoloContinueStartsWithCleanSlotsSoThePlayerCanJoin()
        {
            ThreePlayerWorld(out var original);
            var restored = NewSim();
            restored.ImportMigrationData(original.ExportMigrationData());
            restored.ReadyForNewParty(false);
            Assert.AreEqual(0, restored.ActiveCrewCount);
            for (int i = 0; i < VoyageSimulation.MaxCrew; i++) Assert.IsFalse(restored.Crew[i].Active);
            Assert.GreaterOrEqual(restored.Join(99), 0, "the solo player gets a slot");
            Assert.AreEqual(500, restored.State.Wallet);
            Assert.AreEqual(3, restored.State.Voyage);
        }

        [Test]
        public void AnOnlineContinueHoldsTheSlotsOfIdentifiedPlayersForTheirReturn()
        {
            ThreePlayerWorld(out var original);
            var restored = NewSim();
            restored.ImportMigrationData(original.ExportMigrationData());
            restored.ReadyForNewParty(true);
            Assert.IsTrue(restored.Crew[0].Active);
            Assert.IsTrue(restored.Crew[1].Active);
            Assert.IsFalse(restored.Crew[2].Active, "no identity: nobody can claim it, so it is freed");
            Assert.AreEqual(0, restored.ActiveCrewCount, "waiting slots are not connected players");
            Assert.AreEqual(1, restored.Join(55, "friend-id"), "a friend rejoining takes their own seat back");
            Assert.AreEqual(new Vector2(1, 1), restored.Crew[1].Position, "and keeps where they were standing");
            Assert.AreEqual(0, restored.Join(56, "host-id"));
            int newcomer = restored.Join(57, "stranger-id");
            Assert.GreaterOrEqual(newcomer, 2, "newcomers fit in the freed slots");
        }

        [Test]
        public void AbsentPlayersNeverBlockTheHostFromStartingTheLobby()
        {
            ThreePlayerWorld(out var original);
            var restored = NewSim();
            restored.ImportMigrationData(original.ExportMigrationData());
            restored.ReadyForNewParty(true);
            restored.Join(VoyageSimulation.HostClientId, "host-id");
            restored.OpenLobby();
            Assert.IsTrue(restored.AllReady(), "the friend who has not come back yet is not waited for");
            restored.Join(55, "friend-id");
            Assert.IsFalse(restored.AllReady(), "once they are back they must press Ready like anyone else");
        }

        [Test]
        public void ARestoredWorldKeepsItsPortPhaseAndPassengers()
        {
            var original = NewSim();
            original.Join(10, "host-id");
            original.State.Phase = VoyagePhase.Arrived;
            original.State.Boarded = 2;
            original.Passengers[0] = new PassengerState { Active = true, Satisfaction = 80, FarePaid = true, Position = new Vector3(-1.9f, .5f, -1.5f) };
            var restored = NewSim();
            restored.ImportMigrationData(original.ExportMigrationData());
            restored.ReadyForNewParty(false);
            Assert.AreEqual(VoyagePhase.Arrived, restored.State.Phase);
            Assert.IsTrue(restored.Passengers[0].Active);
            Assert.AreEqual(80f, restored.Passengers[0].Satisfaction, .01f);
        }
    }
}
