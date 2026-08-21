using Alrauna.Amuse.Research.Census;
using Alrauna.Amuse.Research.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Research.Tests.Editor.Collection
{
    /// <summary>
    /// The five calibration cases that run without a vendor shader. Each asserts
    /// that the collector <em>counts</em> a known AMUSE outcome correctly; that
    /// the outcome is reachable in a real project is a separate claim, and for
    /// these five the two collapse because the case is constructed directly.
    /// </summary>
    public sealed class RendererRefusalCalibrationTests
    {
        private CollectorTestScene _scene;

        [SetUp]
        public void SetUp() => _scene = new CollectorTestScene();

        [TearDown]
        public void TearDown() => _scene.Destroy();

        private ObservedRenderer Observe(Renderer renderer)
        {
            return RendererObservationBuilder.Build(
                renderer, "Path", new CensusShaderFamily());
        }

        [Test]
        public void UnsupportedRendererTypeHasUnknownCountsNotZero()
        {
            // The single most likely miscount in the system: a refusal with no
            // reachable mesh must record null, never 0. Zero here understates
            // avatar complexity and overstates coverage in every aggregate.
            var root = _scene.NewRoot("Line");
            var renderer = root.AddComponent<LineRenderer>();

            var observed = Observe(renderer);

            Assert.That(
                observed.Refusal,
                Is.EqualTo(RendererRefusal.UnsupportedRendererType));
            Assert.That(observed.SubmeshCount, Is.Null);
            Assert.That(observed.TriangleCount, Is.Null);
            Assert.That(observed.Submeshes, Is.Empty);
            Assert.That(observed.Kind, Is.EqualTo(RendererKind.Other));
        }

        [Test]
        public void MissingMeshHasUnknownCountsNotZero()
        {
            var root = _scene.NewRoot("NoMesh");
            root.AddComponent<MeshFilter>();
            var renderer = root.AddComponent<MeshRenderer>();

            var observed = Observe(renderer);

            Assert.That(
                observed.Refusal, Is.EqualTo(RendererRefusal.MissingMesh));
            Assert.That(observed.SubmeshCount, Is.Null);
            Assert.That(observed.TriangleCount, Is.Null);
        }

        [Test]
        public void UnsupportedTopologyKnowsSubmeshesButNotTriangles()
        {
            // A quad submesh has no triangle count. Any number written there
            // would be an invention, so the honest record is null for triangles
            // and a real count for submeshes.
            var root = _scene.NewRoot("Quads");
            var go = _scene.NewMeshRenderer(
                root, "Quad", _scene.NewQuadMesh(),
                _scene.NewStandardMaterial());

            var observed = Observe(go.GetComponent<MeshRenderer>());

            Assert.That(
                observed.Refusal,
                Is.EqualTo(RendererRefusal.UnsupportedTopology));
            Assert.That(observed.SubmeshCount, Is.EqualTo(1));
            Assert.That(observed.TriangleCount, Is.Null);
        }

        [Test]
        public void PropertyBlockRefusalStillCountsTheMesh()
        {
            var root = _scene.NewRoot("Block");
            var go = _scene.NewMeshRenderer(
                root, "Blocked", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());
            var renderer = go.GetComponent<MeshRenderer>();
            var block = new MaterialPropertyBlock();
            block.SetFloat("_Cutoff", 0.25f);
            renderer.SetPropertyBlock(block);

            var observed = Observe(renderer);

            Assert.That(
                observed.Refusal,
                Is.EqualTo(RendererRefusal.MaterialPropertyOverridesPresent));
            Assert.That(observed.SubmeshCount, Is.EqualTo(1));
            Assert.That(observed.TriangleCount, Is.EqualTo(1));
            Assert.That(observed.Submeshes, Is.Empty);
        }

        [Test]
        public void UnprovenSlotMappingStillCountsTheMesh()
        {
            var root = _scene.NewRoot("Slots");
            var go = _scene.NewMeshRenderer(
                root, "TwoSubmeshesOneMaterial", _scene.NewTriangleMesh(2),
                _scene.NewStandardMaterial());

            var observed = Observe(go.GetComponent<MeshRenderer>());

            Assert.That(
                observed.Refusal,
                Is.EqualTo(RendererRefusal.UnprovenMaterialSlotMapping));
            Assert.That(observed.SubmeshCount, Is.EqualTo(2));
            Assert.That(observed.TriangleCount, Is.EqualTo(2));
        }

        [Test]
        public void UnattestedMaterialAnalyzesToAllUnknownTriangles()
        {
            var root = _scene.NewRoot("Standard");
            var go = _scene.NewMeshRenderer(
                root, "Plain", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());

            var observed = Observe(go.GetComponent<MeshRenderer>());

            Assert.That(observed.Refusal, Is.EqualTo(RendererRefusal.None));
            Assert.That(
                observed.Kind, Is.EqualTo(RendererKind.MeshRenderer));
            Assert.That(observed.SubmeshCount, Is.EqualTo(1));
            Assert.That(observed.TriangleCount, Is.EqualTo(1));
            Assert.That(observed.Submeshes.Count, Is.EqualTo(1));

            var submesh = observed.Submeshes[0];
            Assert.That(submesh.HasMaterial, Is.True);
            Assert.That(
                submesh.AlphaFailure,
                Is.EqualTo(AlphaResolutionFailure.SemanticsUnknown));
            Assert.That(
                submesh.Disposition,
                Is.EqualTo(SeparationDisposition.Unchanged));
            Assert.That(submesh.TriangleCount, Is.EqualTo(1));
            Assert.That(submesh.UnknownTriangleCount, Is.EqualTo(1));
            Assert.That(submesh.ProvenOpaqueTriangleCount, Is.EqualTo(0));
            Assert.That(
                submesh.ShaderFamilyAttestation,
                Is.EqualTo(ShaderFamilyAttestation.None));
        }
    }
}
