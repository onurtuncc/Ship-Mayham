using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public sealed class PurserTests
    {
        private static readonly Vector2 Desk = new Vector2(-4, -4);

        private static VoyageSimulation NewSimulation()
        {
            var stations = new[]
            {
                new StationDefinition(StationKind.Manifest, Desk),
                new StationDefinition(StationKind.EngineUpgrade, new Vector2(3.8f, -7.8f)),
            };
            var sim = new VoyageSimulation(stations, new[]
            {
                new Vector2(-2, -2), new Vector2(2, -2), new Vector2(-2, 0), new Vector2(2, 0),
                new Vector2(-2, 4), new Vector2(2, 2), new Vector2(-2, 6), new Vector2(2, 4)
            });
            sim.Join(10, "buyer");
            sim.Crew[0].Position = Desk + new Vector2(.8f, 0);
            sim.State.Wallet = 1000;
            return sim;
        }

        [Test]
        public void UpgradesAreBoughtFromTheDeskNotTheFitting()
        {
            var sim = NewSimulation();
            Assert.That(sim.BuyUpgrade(10, StationKind.EngineUpgrade), Does.StartWith("Upgrade purchased"));
            Assert.AreEqual(1, sim.State.EngineLevel);
            Assert.AreEqual(1000 - 100, sim.State.Wallet);
        }

        [Test]
        public void TheBuyerMustStandAtTheDeskAboardAndAtPort()
        {
            var sim = NewSimulation();
            sim.Crew[0].Position = new Vector2(3.8f, -7.8f);
            Assert.AreEqual("Move closer to the purser.", sim.BuyUpgrade(10, StationKind.EngineUpgrade));
            Assert.AreEqual(0, sim.State.EngineLevel);

            sim.Crew[0].Position = Desk;
            sim.State.Phase = VoyagePhase.Sailing;
            Assert.AreEqual("Upgrades are available at port.", sim.BuyUpgrade(10, StationKind.EngineUpgrade));

            sim.State.Phase = VoyagePhase.Boarding;
            sim.Crew[0].OnShore = true;
            Assert.AreEqual("Return aboard to talk to the purser.", sim.BuyUpgrade(10, StationKind.EngineUpgrade));
        }

        [Test]
        public void CostsLevelsAndKindsAreEnforced()
        {
            var sim = NewSimulation();
            sim.State.Wallet = 50;
            Assert.AreEqual("Need 100 coins.", sim.BuyUpgrade(10, StationKind.EngineUpgrade));
            Assert.AreEqual("Unknown upgrade.", sim.BuyUpgrade(10, StationKind.Food));
            Assert.AreEqual("Crew not registered.", sim.BuyUpgrade(99, StationKind.EngineUpgrade));

            for (int i = 0; i < 5; i++) sim.Tick(.1f);   // each purchase attempt has a short cooldown (ticks are capped at 0.1 s)
            sim.State.Wallet = 1000;
            sim.State.ComfortLevel = 1;
            Assert.AreEqual("Upgrade maxed out.", sim.BuyUpgrade(10, StationKind.ComfortUpgrade));
        }

        [Test]
        public void ThePurserPointIsTheManifestDesk()
        {
            Assert.AreEqual(Desk, NewSimulation().PurserPoint);
            var noDesk = new VoyageSimulation(System.Array.Empty<StationDefinition>(), new Vector2[8]);
            Assert.IsNull(noDesk.PurserPoint);
        }

        [Test]
        public void ThePurserPromptReflectsThePhase()
        {
            var boarding = PromptCatalog.ForStation(StationKind.Manifest, new VoyageState { Phase = VoyagePhase.Boarding }, default, default);
            Assert.AreEqual("Purser", boarding.Title);
            Assert.AreEqual(HudControl.Interact, boarding.Key);

            var sailing = PromptCatalog.ForStation(StationKind.Manifest, new VoyageState { Phase = VoyagePhase.Sailing }, default, default);
            Assert.AreEqual(HudControl.None, sailing.Key, "The purser has nothing to do at sea.");
        }
    }
}
