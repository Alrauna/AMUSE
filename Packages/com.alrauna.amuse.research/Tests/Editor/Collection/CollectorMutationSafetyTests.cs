using Alrauna.Amuse.Research.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Research.Tests.Editor.Collection
{
    /// <summary>
    /// Layer 3 of mutation safety: the only layer checkable after the fact.
    /// <para>
    /// The accident being guarded against is specific and quiet. A renderer's
    /// instantiating material properties, and the equivalent one on MeshFilter,
    /// all compile, all read plausibly, and all create a copy as a side effect -
    /// so a collector that used one would silently modify the avatar it was only
    /// supposed to observe.
    /// </para>
    /// </summary>
    public sealed class CollectorMutationSafetyTests
    {
        private CollectorTestScene _scene;

        [SetUp]
        public void SetUp() => _scene = new CollectorTestScene();

        [TearDown]
        public void TearDown() => _scene.Destroy();

        [Test]
        public void CollectingLeavesTheRendererMeshAndMaterialsIdentical()
        {
            var root = _scene.NewRoot("Avatar");
            var mesh = _scene.NewTriangleMesh(2);
            var first = _scene.NewStandardMaterial();
            var second = _scene.NewStandardMaterial();
            var go = _scene.NewMeshRenderer(
                root, "Mesh", mesh, first, second);
            var renderer = go.GetComponent<MeshRenderer>();
            var filter = go.GetComponent<MeshFilter>();

            var submeshCountBefore = mesh.subMeshCount;
            var vertexCountBefore = mesh.vertexCount;
            var hadBlockBefore = renderer.HasPropertyBlock();

            AvatarCensusCollector.Collect(root, null);

            // Reference identity, not equality: an instantiated copy compares
            // equal on content and is exactly the defect being hunted.
            Assert.That(
                ReferenceEquals(filter.sharedMesh, mesh), Is.True,
                "The shared mesh was replaced, which means a copy was "
                + "instantiated.");
            var after = renderer.sharedMaterials;
            Assert.That(after.Length, Is.EqualTo(2));
            Assert.That(ReferenceEquals(after[0], first), Is.True);
            Assert.That(ReferenceEquals(after[1], second), Is.True);
            Assert.That(mesh.subMeshCount, Is.EqualTo(submeshCountBefore));
            Assert.That(mesh.vertexCount, Is.EqualTo(vertexCountBefore));
            Assert.That(
                renderer.HasPropertyBlock(), Is.EqualTo(hadBlockBefore));
        }

        [Test]
        public void CollectingCreatesNoAdditionalMeshOrMaterialObjects()
        {
            var root = _scene.NewRoot("Avatar");
            _scene.NewMeshRenderer(
                root, "Mesh", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());

            var meshesBefore =
                Resources.FindObjectsOfTypeAll<Mesh>().Length;
            var materialsBefore =
                Resources.FindObjectsOfTypeAll<Material>().Length;

            AvatarCensusCollector.Collect(root, null);

            Assert.That(
                Resources.FindObjectsOfTypeAll<Mesh>().Length,
                Is.EqualTo(meshesBefore));
            Assert.That(
                Resources.FindObjectsOfTypeAll<Material>().Length,
                Is.EqualTo(materialsBefore));
        }
    }
}
