using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    /// <summary>Guards the surfaces around the gangway landing against z-fighting (shared top or quay-edge faces).</summary>
    public sealed class HarborSurfaceTests : PrototypeSceneFixture
    {
        [Test]
        public void ThePierAndTheTownFoundationNeverShareAFace()
        {
            var harbor = FindInScene<PrototypeHarbor>();
            Assert.AreEqual(2, harbor.PortRoots.Length);
            foreach (var port in harbor.PortRoots)
            {
                var pier = port.Find("Visuals/Pier_Primitive").GetComponent<Renderer>().bounds;
                var foundation = port.Find("Town_Visuals/QuayFoundation").GetComponent<Renderer>().bounds;
                Assert.Less(foundation.max.y, pier.max.y - .01f, port.name + ": the stone top must sit clearly under the pier planks.");
                Assert.Less(foundation.max.x, pier.max.x - .01f, port.name + ": the stone must stop short of the quay edge the ship sees.");
            }
        }

        [Test]
        public void TheGangwayPlankClearsTheSurfacesItCrosses()
        {
            var harbor = FindInScene<PrototypeHarbor>();
            foreach (var port in harbor.PortRoots)
            {
                var plank = port.Find("Gangway_Visuals/Gangway_Plank_Primitive").GetComponent<Renderer>().bounds;
                foreach (string name in new[] { "Visuals/Pier_Primitive", "Town_Visuals/MarketSquare", "Town_Visuals/QuayFoundation" })
                {
                    var other = port.Find(name).GetComponent<Renderer>().bounds;
                    Assert.Greater(Mathf.Abs(plank.max.y - other.max.y), .01f, $"{port.name}: plank top and {name} top are coplanar.");
                }
            }
        }
    }
}
