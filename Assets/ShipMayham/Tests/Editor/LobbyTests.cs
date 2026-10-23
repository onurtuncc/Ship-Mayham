using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public sealed class LobbyTests
    {
        private const ulong Host = VoyageSimulation.HostClientId;

        private static VoyageSimulation NewSimulation()
        {
            return new VoyageSimulation(System.Array.Empty<StationDefinition>(), new[]
            {
                new Vector2(-2, -2), new Vector2(2, -2), new Vector2(-2, 0), new Vector2(2, 0),
                new Vector2(-2, 4), new Vector2(2, 2), new Vector2(-2, 6), new Vector2(2, 4)
            });
        }

        private static VoyageSimulation LobbyWithGuest(out int hostSlot, out int guestSlot)
        {
            var sim = NewSimulation();
            hostSlot = sim.Join(Host, "host-id");
            guestSlot = sim.Join(11, "guest-id");
            sim.OpenLobby();
            return sim;
        }

        [Test]
        public void SimulationStartsInTheVoyagePhaseSoSoloIsUnchanged()
        {
            var sim = NewSimulation();
            Assert.IsFalse(sim.InLobby);
            Assert.AreEqual(SessionPhase.Voyage, sim.State.SessionPhase);
        }

        [Test]
        public void NothingAdvancesWhileTheLobbyIsOpen()
        {
            var sim = LobbyWithGuest(out int hostSlot, out _);
            Vector2 before = sim.Crew[hostSlot].Position;
            sim.SetInput(Host, Vector2.right);
            for (int i = 0; i < 20; i++) sim.Tick(.05f);
            Assert.AreEqual(before, sim.Crew[hostSlot].Position, "Crew stand still until the voyage starts.");
        }

        [Test]
        public void HostCannotStartUntilEveryoneElseIsReady()
        {
            var sim = LobbyWithGuest(out _, out int guestSlot);
            Assert.AreEqual("Waiting for everyone to get ready.", sim.StartVoyage(Host));
            Assert.IsTrue(sim.InLobby);

            sim.SetReady(11, true);
            Assert.IsTrue(sim.CrewReady(guestSlot));
            Assert.AreEqual(string.Empty, sim.SetReady(11, true));
            sim.SetReady(11, false);
            Assert.AreEqual("Waiting for everyone to get ready.", sim.StartVoyage(Host), "Ready can be taken back.");

            sim.SetReady(11, true);
            Assert.That(sim.StartVoyage(Host), Does.StartWith("The voyage begins"));
            Assert.IsFalse(sim.InLobby);
            Assert.IsFalse(sim.CrewReady(guestSlot), "Ready flags reset when the voyage starts.");
        }

        [Test]
        public void OnlyTheHostStartsAndTheHostNeverNeedsReady()
        {
            var sim = LobbyWithGuest(out int hostSlot, out _);
            sim.SetReady(11, true);
            Assert.AreEqual("Only the host can start the voyage.", sim.StartVoyage(11));

            sim.SetReady(Host, true);
            Assert.IsFalse(sim.CrewReady(hostSlot), "The host starts the voyage instead of pressing Ready.");
            Assert.IsTrue(sim.IsHostSlot(hostSlot));
        }

        [Test]
        public void AHostAloneCanStartAndALateGuestJoinsTheRunningVoyage()
        {
            var sim = NewSimulation();
            sim.Join(Host, "host-id");
            sim.OpenLobby();
            Assert.That(sim.StartVoyage(Host), Does.StartWith("The voyage begins"));
            Assert.AreEqual("The voyage has already started.", sim.StartVoyage(Host));

            int late = sim.Join(12, "late-id");
            Assert.GreaterOrEqual(late, 0);
            Assert.IsFalse(sim.InLobby, "Drop-in players skip the lobby.");
        }

        [Test]
        public void LeavingTheLobbyFreesTheSlotButLeavingAVoyageKeepsIt()
        {
            var sim = LobbyWithGuest(out _, out int guestSlot);
            sim.SetNickname(11, "Gulliver");
            sim.SetReady(11, true);
            sim.SuspendConnection(11);
            Assert.IsFalse(sim.Crew[guestSlot].Active, "Nothing is worth keeping for a lobby leaver.");
            Assert.IsNull(sim.CrewName(guestSlot));
            Assert.IsFalse(sim.CrewReady(guestSlot));
            Assert.AreEqual(-1, sim.FindPlayer("guest-id"));

            sim.Join(11, "guest-id");
            Assert.AreEqual(string.Empty, sim.SetReady(11, true));
            sim.StartVoyage(Host);
            sim.SuspendConnection(11);
            Assert.IsTrue(sim.Crew[guestSlot].Active, "A voyage keeps the slot for a quick return.");
            Assert.AreEqual(guestSlot, sim.FindPlayer("guest-id"));
        }

        [Test]
        public void NicknamesAreCleanedAndKeptPerSlot()
        {
            var sim = LobbyWithGuest(out int hostSlot, out int guestSlot);
            sim.SetNickname(Host, "  Captain   <b>Hook</b> ");
            sim.SetNickname(11, "ThisNameIsFarTooLongToFit");
            Assert.AreEqual("Captain bHook/b", sim.CrewName(hostSlot));
            Assert.AreEqual(LobbyRules.MaxNameLength, sim.CrewName(guestSlot).Length);
            sim.SetNickname(11, "   ");
            Assert.AreEqual(LobbyRules.MaxNameLength, sim.CrewName(guestSlot).Length, "A blank name keeps the previous one.");
        }

        [Test]
        public void MigrationCarriesTheLobbyToTheNewHost()
        {
            var original = LobbyWithGuest(out _, out int guestSlot);
            original.SetNickname(11, "Guest");
            original.SetReady(11, true);

            var restored = NewSimulation();
            restored.ImportMigrationData(original.ExportMigrationData());

            Assert.IsTrue(restored.InLobby);
            Assert.AreEqual("Guest", restored.CrewName(guestSlot));
            Assert.IsTrue(restored.CrewReady(guestSlot));
            Assert.AreEqual(guestSlot, restored.Join(70, "guest-id"), "The guest rebinds to the same slot on the new host.");
        }

        [Test]
        public void SanitizeNameHandlesEdgeCases()
        {
            Assert.IsNull(LobbyRules.SanitizeName(null));
            Assert.IsNull(LobbyRules.SanitizeName(""));
            Assert.IsNull(LobbyRules.SanitizeName("  \t "));
            Assert.IsNull(LobbyRules.SanitizeName("<>"));
            Assert.AreEqual("A B", LobbyRules.SanitizeName("A \n\t B"));
            Assert.AreEqual("Player 3", LobbyRules.DisplayName(null, 2));
            Assert.AreEqual("Ann", LobbyRules.DisplayName("Ann", 2));
        }

        [Test]
        public void WaitingRuleIgnoresTheHostAndEmptySlots()
        {
            Assert.IsFalse(LobbyRules.WaitingOnPlayer(false, false, false));
            Assert.IsFalse(LobbyRules.WaitingOnPlayer(true, true, false));
            Assert.IsFalse(LobbyRules.WaitingOnPlayer(true, false, true));
            Assert.IsTrue(LobbyRules.WaitingOnPlayer(true, false, false));
        }

        [Test]
        public void CrewProfileTruncatesInsteadOfFailingOnLongNames()
        {
            var profile = CrewProfile.From(new string('x', 200), true);
            Assert.IsTrue(profile.Ready);
            Assert.Greater(profile.Name.Length, 0);
            Assert.IsTrue(CrewProfile.From("Ann", false).Equals(CrewProfile.From("Ann", false)));
            Assert.IsFalse(CrewProfile.From("Ann", false).Equals(CrewProfile.From("Ann", true)));
        }
    }
}
