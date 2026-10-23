using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace ShipMayham.Tests
{
    public sealed class CrewRosterTests
    {
        private static byte[] Id(string playerId) => Encoding.UTF8.GetBytes(playerId);

        [Test]
        public void DirectConnectionsAreLimitedByClientCount()
        {
            var roster = new CrewRoster();
            for (ulong client = 1; client <= VoyageSimulation.MaxCrew; client++)
                Assert.IsTrue(roster.TryAdmit(client, null, out _));
            Assert.IsFalse(roster.TryAdmit(99, null, out string reason));
            Assert.That(reason, Does.Contain("four crew"));
            Assert.IsTrue(roster.TryAdmit(2, null, out _), "An admitted client may reconnect.");
        }

        [Test]
        public void OnlineAdmissionCountsPlayersAndLetsAKnownPlayerRejoinOnANewConnection()
        {
            var roster = new CrewRoster { OnlineMode = true };
            for (ulong client = 1; client <= VoyageSimulation.MaxCrew; client++)
                Assert.IsTrue(roster.TryAdmit(client, Id("player-" + client), out _));
            Assert.IsFalse(roster.TryAdmit(50, Id("stranger"), out _));

            roster.ClientDisconnected(3);
            Assert.IsNull(roster.PlayerIdentity(3));
            Assert.IsTrue(roster.TryAdmit(60, Id("player-3"), out _), "Migration gives the same player a new connection id.");
            Assert.AreEqual("player-3", roster.PlayerIdentity(60));
            Assert.IsFalse(roster.TryAdmit(61, Id("stranger"), out _), "The disconnected slot still belongs to its player.");
        }

        [Test]
        public void ReconciliationKeepsSessionMembersAndRemovesAbsentPlayers()
        {
            var roster = new CrewRoster { OnlineMode = true };
            roster.TryAdmit(1, Id("stays"), out _);
            roster.TryAdmit(2, Id("leaves"), out _);
            roster.QueueDeparture("stays");
            roster.QueueDeparture("leaves");
            var removed = new List<string>();

            roster.ReconcileDepartures(new HashSet<string> { "stays" }, removed.Add);

            CollectionAssert.AreEqual(new[] { "leaves" }, removed);
            Assert.IsTrue(roster.IsAdmitted("stays"));
            Assert.IsFalse(roster.IsAdmitted("leaves"));
            Assert.AreEqual(0, roster.PendingDepartureCount);
        }

        [Test]
        public void ClearForgetsEverything()
        {
            var roster = new CrewRoster { OnlineMode = true };
            roster.TryAdmit(1, Id("a"), out _);
            roster.QueueDeparture("a");
            roster.Clear();
            Assert.IsFalse(roster.IsAdmitted("a"));
            Assert.IsNull(roster.PlayerIdentity(1));
            Assert.AreEqual(0, roster.PendingDepartureCount);
        }
    }
}
