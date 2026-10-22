using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShipMayham.Tests
{
    public class OceanSurfaceTests
    {
        [Test]
        public void GridHasExpectedVertexAndTriangleCountsAndStaysInsideItsExtent()
        {
            var mesh = OceanGridMesh.Build(16, 320f, 2f);
            try
            {
                Assert.AreEqual(17 * 17, mesh.vertexCount);
                Assert.AreEqual(16 * 16 * 2, mesh.triangles.Length / 3);
                foreach (var v in mesh.vertices)
                {
                    Assert.LessOrEqual(Mathf.Abs(v.x), 160.001f);
                    Assert.LessOrEqual(Mathf.Abs(v.z), 160.001f);
                    Assert.AreEqual(0f, v.y);
                }
                Assert.GreaterOrEqual(mesh.bounds.extents.y, 2f, "Culling bounds must cover shader displacement.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void EveryTriangleFacesUpwardAndIndicesAreValid()
        {
            var mesh = OceanGridMesh.Build(8, 80f, 1f);
            try
            {
                var vertices = mesh.vertices; var triangles = mesh.triangles;
                Assert.AreEqual(0, triangles.Length % 3);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    foreach (int index in new[] { triangles[i], triangles[i + 1], triangles[i + 2] }) Assert.That(index, Is.InRange(0, vertices.Length - 1));
                    Vector3 normal = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]);
                    Assert.Greater(normal.y, 0f, "Back-face culled sea must face up from above.");
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ComponentBuildsItsMeshWhenEnabledAndReleasesItOnDestroy()
        {
            var go = new GameObject("OceanTest", typeof(MeshFilter), typeof(MeshRenderer));
            try
            {
                var surface = go.AddComponent<OceanSurface>();   // ExecuteAlways: OnEnable runs in Edit Mode
                Assert.IsNotNull(surface.Mesh);
                Assert.AreSame(surface.Mesh, go.GetComponent<MeshFilter>().sharedMesh);
                Assert.AreEqual(161 * 161, surface.Mesh.vertexCount);
                var first = surface.Mesh;
                surface.Rebuild();
                Assert.IsTrue(first == null, "Rebuild must free the previous mesh instead of leaking it.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void LargeGridsSwitchToThirtyTwoBitIndices()
        {
            var mesh = OceanGridMesh.Build(OceanGridMesh.MaxCells, 640f, 1f);
            try { Assert.AreEqual(IndexFormat.UInt32, mesh.indexFormat); }
            finally { Object.DestroyImmediate(mesh); }
        }

        [TestCase(0)]
        [TestCase(OceanGridMesh.MinCells - 1)]
        [TestCase(OceanGridMesh.MaxCells + 1)]
        public void InvalidCellCountsAreRejected(int cells) => Assert.Throws<System.ArgumentOutOfRangeException>(() => OceanGridMesh.Build(cells, 100f, 1f));

        [TestCase(0f)]
        [TestCase(-5f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidExtentsAreRejected(float extent) => Assert.Throws<System.ArgumentOutOfRangeException>(() => OceanGridMesh.Build(16, extent, 1f));

        [TestCase(0.4f, 1f, 0f)]
        [TestCase(0.6f, 1f, 1f)]
        [TestCase(7.4f, 2f, 8f)]
        [TestCase(-7.4f, 2f, -8f)]
        [TestCase(3.3f, 0f, 3.3f)]
        public void SnapKeepsPatternsStable(float value, float step, float expected) => Assert.AreEqual(expected, OceanGridMesh.Snap(value, step), 1e-4f);
    }
}
