using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The barrier's retained alpha-separation record.
    /// <para>
    /// At this milestone the record is deliberately inert: it names what the
    /// barrier found, and nothing more. Preparing an opaque mapping, creating a
    /// clone or applying anything is a later increment, so these tests also pin
    /// that none of it happens yet — a clone created before its sweep exists
    /// would leak, and a mutation before validation would be unsound.
    /// </para>
    /// </summary>
    public sealed class AlphaSeparationPreparationTests
    {
        [Test]
        public void CandidateRendererProducesARetainedRecord()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE prepared candidate");
            Material material = null;
            Mesh mesh = null;

            try
            {
                material = VerifiedOpaqueMaterial();
                var renderer = AddSingleTriangleRenderer(root, material, out mesh);

                var amuse = RunBarrier(root);

                Assert.That(
                    amuse.SemanticallyRefusedRendererCount, Is.Zero,
                    "fixture precondition: the renderer must be analyzable");
                Assert.That(
                    amuse.OpaqueCandidateTriangleCount, Is.EqualTo(1),
                    "fixture precondition: the renderer must produce one opaque " +
                    "candidate triangle, or there is no candidate slot to retain");

                Assert.That(amuse.Separation, Is.Not.Null);
                Assert.That(amuse.Separation.Renderers, Has.Count.EqualTo(1));

                var prepared = amuse.Separation.Renderers[0];
                Assert.That(prepared.Target.Renderer, Is.SameAs(renderer));
                Assert.That(prepared.Target.ExpectedMesh, Is.SameAs(mesh));
                Assert.That(
                    prepared.Target.ExpectedMaterialSlotCount, Is.EqualTo(1));
                Assert.That(prepared.RendererPath, Is.Empty);
                Assert.That(prepared.Plan.OpaqueTriangleCount, Is.EqualTo(1));
                Assert.That(prepared.Evidence.IsClosed, Is.True);

                Assert.That(prepared.CandidateSlots, Has.Count.EqualTo(1));
                var slot = prepared.CandidateSlots[0];
                Assert.That(
                    slot.Plan.Disposition,
                    Is.EqualTo(SubmeshSeparationDisposition.WhollyOpaqueCandidate));
                Assert.That(slot.Plan.SourceMaterialBindingIndex, Is.Zero);
                Assert.That(slot.Plan.OpaqueTriangleOrdinals,
                    Is.EqualTo(new[] { 0 }));
                Assert.That(slot.Plan.TransparentTriangleOrdinals, Is.Empty);
            }
            finally
            {
                if (mesh != null) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
                if (material != null) Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void RetainedRecordIsInert()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE inert record");
            Material material = null;
            Mesh mesh = null;

            try
            {
                material = VerifiedOpaqueMaterial();
                var renderer = AddSingleTriangleRenderer(root, material, out mesh);
                var originalMaterials = renderer.sharedMaterials;

                var amuse = RunBarrier(root);

                Assert.That(amuse.Separation, Is.Not.Null,
                    "fixture precondition: nothing was retained, so inertness " +
                    "would hold vacuously");

                Assert.That(
                    amuse.Separation.CreatedClones, Is.Empty,
                    "no clone may be created before the sweep that destroys an " +
                    "unreferenced one exists");
                Assert.That(amuse.Separation.OpaqueBySource, Is.Empty);
                Assert.That(
                    amuse.Separation.Renderers[0].MeshClone, Is.Null,
                    "no mesh may be cloned before the sweep exists");
                Assert.That(
                    amuse.Separation.Renderers[0].CandidateSlots[0]
                        .OpaqueOfAdmitted,
                    Is.Empty,
                    "no opaque mapping is prepared at this milestone");

                Assert.That(
                    renderer.sharedMaterials, Is.EqualTo(originalMaterials),
                    "the barrier must not mutate the build avatar");
                Assert.That(renderer.sharedMesh, Is.SameAs(mesh));
                Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            }
            finally
            {
                if (mesh != null) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
                if (material != null) Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void RendererWithoutAnOpaqueCandidateRetainsNothing()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE no candidate");
            Material material = null;
            Mesh mesh = null;

            try
            {
                material = VerifiedTransparentMaterial();
                AddSingleTriangleRenderer(root, material, out mesh);

                var amuse = RunBarrier(root);

                Assert.That(
                    amuse.SemanticallyRefusedRendererCount, Is.Zero,
                    "fixture precondition: the renderer must be analyzed, not " +
                    "refused, or the absent record would prove nothing");
                Assert.That(
                    amuse.AnalyzedRendererCount, Is.EqualTo(1),
                    "fixture precondition: the renderer must reach analysis");
                Assert.That(
                    amuse.OpaqueCandidateTriangleCount, Is.Zero,
                    "fixture precondition: the material must prove no triangle " +
                    "opaque");

                Assert.That(
                    amuse.Separation, Is.Null,
                    "a renderer with no opaque candidate has nothing to prepare");
            }
            finally
            {
                if (mesh != null) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
                if (material != null) Object.DestroyImmediate(material);
            }
        }

        /// <summary>
        /// Drives the real bindings-capture and barrier passes through the
        /// production entry, substituting only the existing public-fixture seams
        /// for unavailable vendor source attestation.
        /// </summary>
        private static AmusePlatformFinishState RunBarrier(GameObject root)
        {
            var context = AvatarProcessor.ProcessAvatar(
                root, PreparationTestPlatform.Instance);
            context.GetState<AmusePlatformFinishState>().AnimatorBindings =
                GenericPlatformAnimatorBindings.Instance;

            AmusePlatformFinishPass.Execute(
                context,
                SupportedFacts(),
                VerifiedPoiyomiTestSeams.SelectVerifiedFixtureRequest,
                VerifiedPoiyomiTestSeams.CaptureVerifiedFixtureMaterials,
                VerifiedPoiyomiTestSeams.VerifiedAlphaOnly);

            return context.GetState<AmusePlatformFinishState>();
        }

        private static SkinnedMeshRenderer AddSingleTriangleRenderer(
            GameObject root,
            Material material,
            out Mesh mesh)
        {
            mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                },
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);

            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = new[] { material };
            return renderer;
        }

        private static Material VerifiedOpaqueMaterial()
        {
            var material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
            material.SetFloat("_AlphaForceOpaque", 1f);
            return material;
        }

        private static Material VerifiedTransparentMaterial()
        {
            var material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
            return material;
        }

        private static HostLifecycleFacts SupportedFacts()
        {
            return new HostLifecycleFacts(
                "2022.3.22f1",
                "1.14.4",
                "3.10.4",
                "3.10.4",
                WellKnownPlatforms.VRChatAvatar30,
                AmuseBuildPath.NonPlayNdmfBuild,
                hasAssetSaver: true,
                hasAssetContainer: true,
                hasObjectRegistry: true,
                hasErrorReport: true);
        }

        private sealed class PreparationTestPlatform : INDMFPlatformProvider
        {
            internal static readonly PreparationTestPlatform Instance =
                new PreparationTestPlatform();

            public string QualifiedName => "nadena.dev.ndmf.generic";
            public string DisplayName => "AMUSE alpha separation preparation";
        }
    }
}
