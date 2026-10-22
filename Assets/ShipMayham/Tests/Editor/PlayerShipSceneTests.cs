using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class PlayerShipSceneTests : PrototypeSceneFixture
    {
        [Test] public void AuthoredShipUsesTheGeneratedHullAndKeepsItPaintable()
        {
            var session = FindInScene<PrototypeSession>();
            var view = FindInScene<PrototypeView>();
            Transform visuals = session.ShipRoot.Find("Visuals");
            Assert.IsNotNull(visuals.Find("Model_PlayerShipHull"));
            Assert.IsNotNull(visuals.Find("Model_PlayerShipDeco"));
            foreach (string primitive in new[] { "Hull_Primitive", "Deck_Primitive", "Rail_Bow_Primitive" })
                Assert.IsFalse(visuals.Find(primitive).GetComponent<Renderer>().enabled, primitive + " should be hidden");
            Assert.That(view.LiveryRenderers, Has.Some.Matches<Renderer>(r => r != null && r.transform.IsChildOf(visuals.Find("Model_PlayerShipHull"))));
        }

        [Test] public void GeneratedShipCoversTheGameplayDeckAndLeadsAlongPositiveZ()
        {
            Transform visuals = FindInScene<PrototypeSession>().ShipRoot.Find("Visuals");
            var bounds = new Bounds();
            bool first = true;
            foreach (var renderer in visuals.Find("Model_PlayerShipDeco").GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
            }
            Assert.LessOrEqual(bounds.min.z - visuals.position.z, -10f, "Stern must reach the stern rail of the 20 m deck.");
            Assert.GreaterOrEqual(bounds.max.z - visuals.position.z, 13f, "Prow must extend past the bow rail.");
            Assert.LessOrEqual(bounds.min.x - visuals.position.x, -6f);
            Assert.GreaterOrEqual(bounds.max.x - visuals.position.x, 6f);
        }
    }
}
