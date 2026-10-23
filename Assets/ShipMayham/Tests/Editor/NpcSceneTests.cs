using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public sealed class NpcSceneTests : PrototypeSceneFixture
    {
        [Test]
        public void ThePurserStandsBehindTheManifestDesk()
        {
            var session = FindInScene<PrototypeSession>();
            PrototypeStation manifest = null;
            foreach (var station in session.Stations) if (station.Kind == StationKind.Manifest) manifest = station;
            Assert.IsNotNull(manifest);
            var purser = manifest.transform.Find("Purser_Npc");
            Assert.IsNotNull(purser, "Run Ship Mayham > Upgrade Prototype (Living NPCs).");
            var animator = purser.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(animator);
            Assert.IsTrue(animator.avatar != null && animator.avatar.isHuman);
            Assert.AreEqual("PURSER", manifest.Label.text);
        }

        [Test]
        public void EveryNpcIsAnimatedAndHasLivingEyes()
        {
            var npcs = FindInScene<PrototypeNpcAnimator>();
            Assert.IsNotNull(npcs);
            // Six residents in each of the two ports, plus the purser.
            Assert.AreEqual(13, npcs.Animators.Length);
            foreach (var animator in npcs.Animators)
            {
                Assert.IsNotNull(animator);
                Assert.IsNotNull(animator.runtimeAnimatorController, animator.name);
                Assert.IsNotNull(CharacterEyes.TryCreate(animator), animator.name + " needs Pupil_L / Pupil_R for the gaze system.");
            }
        }

        [Test]
        public void TheOldStaticResidentModelsAreGone()
        {
            var harbor = FindInScene<PrototypeHarbor>();
            foreach (var port in harbor.PortRoots)
                foreach (var t in port.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Model_Character_") && !t.name.Contains("_Rigged"))
                        Assert.Fail("Static resident model left behind: " + t.name);
        }

        [Test]
        public void UpgradeFittingsStayOutOfTheCrosshair()
        {
            var session = FindInScene<PrototypeSession>();
            int fittings = 0;
            foreach (var station in session.Stations) if (VoyageSimulation.IsUpgrade(station.Kind)) fittings++;
            Assert.AreEqual(3, fittings, "The simulation keeps the upgrade stations; only their presentation moves to the purser.");
        }
    }
}
