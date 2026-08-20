using System;
using Alrauna.Amuse.Research.Census;
using NUnit.Framework;
using static Alrauna.Amuse.Research.Tests.Editor.Census.CensusObservationBuilders;

namespace Alrauna.Amuse.Research.Tests.Editor.Census
{
    /// <summary>
    /// Tier 1 construction and the record-local arithmetic invariants. An
    /// invalid observation must be impossible to build, because a census that
    /// can hold one has already lost the property that makes it worth running.
    /// </summary>
    public sealed class CensusObservationTests
    {
        [Test]
        public void SubmeshPreservesEveryObservedValue()
        {
            var submesh = new ObservedSubmesh(
                submeshIndex: 2,
                materialSlotIndex: 3,
                hasMaterial: true,
                materialName: "Body Skin",
                materialAssetPath: "Assets/Avatars/body.mat",
                materialAssetGuid: "abcdef01234567890abcdef012345678",
                shaderName: ".poiyomi/Poiyomi Toon",
                shaderFamilyAttestation: ShaderFamilyAttestation.Poiyomi,
                alphaFailure: AlphaResolutionFailure.MissingTextureEvidence,
                disposition: SeparationDisposition.Split,
                triangleCount: 10,
                provenOpaqueTriangleCount: 6,
                mustRemainTransparentTriangleCount: 3,
                unknownTriangleCount: 1);

            Assert.AreEqual(2, submesh.SubmeshIndex);
            Assert.AreEqual(3, submesh.MaterialSlotIndex);
            Assert.IsTrue(submesh.HasMaterial);
            Assert.AreEqual("Body Skin", submesh.MaterialName);
            Assert.AreEqual("Assets/Avatars/body.mat", submesh.MaterialAssetPath);
            Assert.AreEqual("abcdef01234567890abcdef012345678", submesh.MaterialAssetGuid);
            Assert.AreEqual(".poiyomi/Poiyomi Toon", submesh.ShaderName);
            Assert.AreEqual(ShaderFamilyAttestation.Poiyomi, submesh.ShaderFamilyAttestation);
            Assert.AreEqual(AlphaResolutionFailure.MissingTextureEvidence, submesh.AlphaFailure);
            Assert.AreEqual(SeparationDisposition.Split, submesh.Disposition);
            Assert.AreEqual(10, submesh.TriangleCount);
            Assert.AreEqual(6, submesh.ProvenOpaqueTriangleCount);
            Assert.AreEqual(3, submesh.MustRemainTransparentTriangleCount);
            Assert.AreEqual(1, submesh.UnknownTriangleCount);
        }

        [Test]
        public void SubmeshRejectsOutcomeCountsThatDoNotSumToItsTriangleCount()
        {
            Assert.Throws<ArgumentException>(() => new ObservedSubmesh(
                0, 0, true, "m", "p", "g", "s",
                ShaderFamilyAttestation.None,
                AlphaResolutionFailure.None,
                SeparationDisposition.Unchanged,
                triangleCount: 10,
                provenOpaqueTriangleCount: 6,
                mustRemainTransparentTriangleCount: 3,
                unknownTriangleCount: 0));
        }

        [Test]
        public void SubmeshRejectsNegativeOutcomeCounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObservedSubmesh(
                0, 0, true, "m", "p", "g", "s",
                ShaderFamilyAttestation.None,
                AlphaResolutionFailure.None,
                SeparationDisposition.Unchanged,
                triangleCount: 0,
                provenOpaqueTriangleCount: -1,
                mustRemainTransparentTriangleCount: 1,
                unknownTriangleCount: 0));
        }

        [Test]
        public void SubmeshRejectsAnUndefinedCategoryValue()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObservedSubmesh(
                0, 0, true, "m", "p", "g", "s",
                ShaderFamilyAttestation.None,
                (AlphaResolutionFailure)99,
                SeparationDisposition.Unchanged,
                0, 0, 0, 0));
        }

        [Test]
        public void AnalyzedRendererRejectsSubmeshTrianglesThatDoNotSumToItsOwn()
        {
            Assert.Throws<ArgumentException>(() => new ObservedRenderer(
                "Avatar/Body", "Body", "SkinnedMeshRenderer",
                RendererKind.SkinnedMeshRenderer,
                RendererRefusal.None,
                submeshCount: 1,
                triangleCount: 99,
                submeshes: new[] { Submesh(provenOpaque: 4) }));
        }

        [Test]
        public void AnalyzedRendererRejectsASubmeshCountThatDisagreesWithItsSubmeshes()
        {
            Assert.Throws<ArgumentException>(() => new ObservedRenderer(
                "Avatar/Body", "Body", "SkinnedMeshRenderer",
                RendererKind.SkinnedMeshRenderer,
                RendererRefusal.None,
                submeshCount: 7,
                triangleCount: 4,
                submeshes: new[] { Submesh(provenOpaque: 4) }));
        }

        [Test]
        public void AnalyzedRendererRejectsUnknownCounts()
        {
            Assert.Throws<ArgumentException>(() => new ObservedRenderer(
                "Avatar/Body", "Body", "SkinnedMeshRenderer",
                RendererKind.SkinnedMeshRenderer,
                RendererRefusal.None,
                submeshCount: null,
                triangleCount: null,
                submeshes: new ObservedSubmesh[0]));
        }

        [Test]
        public void RefusedRendererRejectsSubmeshRecords()
        {
            Assert.Throws<ArgumentException>(() => new ObservedRenderer(
                "Avatar/Body", "Body", "SkinnedMeshRenderer",
                RendererKind.SkinnedMeshRenderer,
                RendererRefusal.UnsupportedTopology,
                submeshCount: 1,
                triangleCount: 4,
                submeshes: new[] { Submesh(provenOpaque: 4) }));
        }

        [Test]
        public void RefusedRendererKeepsUnreachableMeshCountsNull()
        {
            var renderer = RefusedRenderer(RendererRefusal.MissingMesh);

            Assert.IsNull(renderer.SubmeshCount);
            Assert.IsNull(renderer.TriangleCount);
        }

        [Test]
        public void RefusedRendererMayStillCarryReachableMeshCounts()
        {
            var renderer = RefusedRenderer(
                RendererRefusal.MaterialPropertyOverridesPresent,
                submeshCount: 3,
                triangleCount: 120);

            Assert.AreEqual(3, renderer.SubmeshCount);
            Assert.AreEqual(120, renderer.TriangleCount);
        }

        [Test]
        public void RendererCopiesItsSubmeshListDefensively()
        {
            var mutable = MutableSubmeshes(Submesh(provenOpaque: 4));
            var renderer = new ObservedRenderer(
                "Avatar/Body", "Body", "SkinnedMeshRenderer",
                RendererKind.SkinnedMeshRenderer,
                RendererRefusal.None,
                submeshCount: 1,
                triangleCount: 4,
                submeshes: mutable);

            mutable.Add(Submesh(provenOpaque: 9));

            Assert.AreEqual(1, renderer.Submeshes.Count);
        }

        [Test]
        public void AvatarCopiesItsRendererListDefensively()
        {
            var mutable = new System.Collections.Generic.List<ObservedRenderer>
            {
                Renderer(),
            };
            var avatar = new ObservedAvatar("a", "c", "p", "g", mutable);

            mutable.Add(Renderer());

            Assert.AreEqual(1, avatar.Renderers.Count);
        }

        [Test]
        public void ObservationSetCopiesItsAvatarListDefensively()
        {
            var mutable = new System.Collections.Generic.List<ObservedAvatar> { Avatar() };
            var set = new CensusObservationSet(mutable);

            mutable.Add(Avatar());

            Assert.AreEqual(1, set.Avatars.Count);
        }

        [Test]
        public void ObservationSetRejectsANullAvatarList()
        {
            Assert.Throws<ArgumentNullException>(() => new CensusObservationSet(null));
        }
    }
}
