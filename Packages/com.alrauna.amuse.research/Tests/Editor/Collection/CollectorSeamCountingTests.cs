using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Research.Census;
using Alrauna.Amuse.Research.Collection;
using NUnit.Framework;
using UnityEngine;
using Semantics = Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Research.Tests.Editor.Collection
{
    /// <summary>
    /// Counting claims for the two outcomes the public project cannot reach
    /// through the production path, because it installs no vendor shader. That
    /// AMUSE reaches them in a real project is a separate reachability claim and
    /// is checked in the Census Lab, not here. Conflating the two would let a
    /// census report near-total SemanticsUnknown - a true statement about the
    /// project and a false one about AMUSE - and call it a pass.
    /// <para>
    /// The substituted semantics are constructed here rather than in the
    /// collector package, so no production type exists whose only purpose is to
    /// be called by a test. The seam itself is AMUSE's own
    /// ResearchMaterialSemanticsProvider, used exactly as AMUSE's own
    /// integration tests use it.
    /// </para>
    /// <para>
    /// The Semantics alias is not decoration: AMUSE declares its own
    /// TextureWrapMode, which collides by name with UnityEngine.TextureWrapMode.
    /// </para>
    /// </summary>
    public sealed class CollectorSeamCountingTests
    {
        private CollectorTestScene _scene;

        [SetUp]
        public void SetUp() => _scene = new CollectorTestScene();

        [TearDown]
        public void TearDown() => _scene.Destroy();

        /// <summary>
        /// Alpha only. The census measures alpha, and a seam that claimed
        /// anything else would be asserting something it has no basis for.
        /// </summary>
        private static Semantics.MaterialSemantics ConstantOpaque()
        {
            return new Semantics.MaterialSemantics(
                Semantics.SemanticOutput<Semantics.ScalarSemanticValue>
                    .Complete(Semantics.ScalarSemanticValue.Constant(1f)));
        }

        /// <summary>
        /// Alpha sampled from a texture identity the evidence provider was never
        /// given. The UV mapping is the identity - channel 0, unit scale, zero
        /// offset - and the sampling is the plainest supported pair, so the only
        /// thing the resolver can refuse on is the missing texture evidence.
        /// </summary>
        private static Semantics.MaterialSemantics AbsentTextureAlpha()
        {
            var sample = new Semantics.TextureSample(
                new Semantics.TextureSourceId(
                    "census-calibration-absent-texture"),
                new Semantics.UvMapping(0, Vector2.one, Vector2.zero),
                new Semantics.TextureSampling(
                    Semantics.TextureFilterMode.Bilinear,
                    Semantics.TextureWrapMode.Clamp));

            return new Semantics.MaterialSemantics(
                Semantics.SemanticOutput<Semantics.ScalarSemanticValue>
                    .Complete(Semantics.ScalarSemanticValue.Texture(
                        sample, Semantics.TextureChannel.Alpha)));
        }

        /// <summary>
        /// Alpha pinned to a constant below one. The resolver proves every
        /// triangle can never reach opacity, so nothing is unknown and nothing
        /// is opaque; the answer is the mirror image of ConstantOpaque.
        /// </summary>
        private static Semantics.MaterialSemantics ConstantSubUnitAlpha()
        {
            return new Semantics.MaterialSemantics(
                Semantics.SemanticOutput<Semantics.ScalarSemanticValue>
                    .Complete(Semantics.ScalarSemanticValue.Constant(0.4f)));
        }

        private ObservedRenderer Observe(
            Renderer renderer, Semantics.MaterialSemantics semantics)
        {
            ResearchMaterialSemanticsProvider provider = material => semantics;
            return RendererObservationBuilder.Build(
                renderer, "Path", new CensusShaderFamily(), provider);
        }

        [Test]
        public void ConstantOpaqueAlphaCountsEveryTriangleAsProvenOpaque()
        {
            var root = _scene.NewRoot("Avatar");
            var go = _scene.NewMeshRenderer(
                root, "Mesh", _scene.NewTriangleMesh(2),
                _scene.NewStandardMaterial(), _scene.NewStandardMaterial());

            var observed = Observe(
                go.GetComponent<MeshRenderer>(), ConstantOpaque());

            Assert.That(observed.Refusal, Is.EqualTo(RendererRefusal.None));
            Assert.That(observed.TriangleCount, Is.EqualTo(2));
            foreach (var submesh in observed.Submeshes)
            {
                Assert.That(
                    submesh.AlphaFailure,
                    Is.EqualTo(AlphaResolutionFailure.None));
                Assert.That(
                    submesh.ProvenOpaqueTriangleCount,
                    Is.EqualTo(submesh.TriangleCount));
                Assert.That(submesh.UnknownTriangleCount, Is.EqualTo(0));
                Assert.That(
                    submesh.Disposition,
                    Is.EqualTo(SeparationDisposition.WhollyOpaqueCandidate));
            }
        }

        [Test]
        public void ConstantSubUnitAlphaCountsEveryTriangleAsTransparent()
        {
            // A constant below one can never reach opacity on any triangle,
            // so the whole submesh must remain transparent, nothing is
            // unknown, and the disposition stays Unchanged.
            var root = _scene.NewRoot("Avatar");
            var go = _scene.NewMeshRenderer(
                root, "Mesh", _scene.NewTriangleMesh(2),
                _scene.NewStandardMaterial(), _scene.NewStandardMaterial());

            var observed = Observe(
                go.GetComponent<MeshRenderer>(), ConstantSubUnitAlpha());

            Assert.That(observed.Refusal, Is.EqualTo(RendererRefusal.None));
            Assert.That(observed.TriangleCount, Is.EqualTo(2));
            foreach (var submesh in observed.Submeshes)
            {
                Assert.That(
                    submesh.AlphaFailure,
                    Is.EqualTo(AlphaResolutionFailure.None));
                Assert.That(
                    submesh.MustRemainTransparentTriangleCount,
                    Is.EqualTo(submesh.TriangleCount));
                Assert.That(submesh.ProvenOpaqueTriangleCount, Is.EqualTo(0));
                Assert.That(submesh.UnknownTriangleCount, Is.EqualTo(0));
                Assert.That(
                    submesh.Disposition,
                    Is.EqualTo(SeparationDisposition.Unchanged));
            }
        }

        [Test]
        public void MissingTextureEvidenceIsRecordedAsItsOwnFailure()
        {
            // "We understand this shader but cannot see the texture" implies a
            // completely different next step from "we do not understand this
            // shader", so the two must never merge.
            var root = _scene.NewRoot("Avatar");
            var go = _scene.NewMeshRenderer(
                root, "Mesh", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());

            var observed = Observe(
                go.GetComponent<MeshRenderer>(), AbsentTextureAlpha());
            var submesh = observed.Submeshes[0];

            Assert.That(
                submesh.AlphaFailure,
                Is.EqualTo(AlphaResolutionFailure.MissingTextureEvidence));
            Assert.That(
                submesh.UnknownTriangleCount,
                Is.EqualTo(submesh.TriangleCount));
            Assert.That(
                submesh.Disposition,
                Is.EqualTo(SeparationDisposition.Unchanged));
        }
    }
}
