using System;
using Alrauna.Amuse.Research.Census;
using NUnit.Framework;
using static Alrauna.Amuse.Research.Tests.Editor.Census.CensusObservationBuilders;

namespace Alrauna.Amuse.Research.Tests.Editor.Census
{
    /// <summary>
    /// Aggregation arithmetic. Aggregate runs on tier 2 only, so these fixtures
    /// go through the anonymizer rather than constructing tier 2 by hand: the
    /// aggregate must be correct over what the pipeline actually produces.
    /// </summary>
    public sealed class CensusAggregatorTests
    {
        private static CensusAggregateReport Report(params ObservedAvatar[] avatars)
        {
            return CensusAggregator.Aggregate(CensusAnonymizer.Anonymize(Set(avatars)));
        }

        [Test]
        public void CountsThePopulationItAggregatedOver()
        {
            var report = Report(
                Avatar(renderers: new[]
                {
                    Renderer(submeshes: new[]
                    {
                        Submesh(provenOpaque: 1),
                        Submesh(submeshIndex: 1, materialSlotIndex: 1, unknown: 1),
                    }),
                    Renderer(submeshes: new[] { Submesh(provenOpaque: 1) }),
                }),
                Avatar(renderers: new[]
                {
                    Renderer(submeshes: new[] { Submesh(provenOpaque: 1) }),
                }));

            Assert.AreEqual(2, report.AvatarCount);
            Assert.AreEqual(3, report.RendererCount);
            Assert.AreEqual(4, report.SubmeshRecordCount);
        }

        [Test]
        public void CountsMaterialsDistinctlyWithinEachAvatar()
        {
            var skin = Submesh(materialName: "skin", provenOpaque: 1);
            var hair = Submesh(
                submeshIndex: 1, materialSlotIndex: 1, materialName: "hair", provenOpaque: 1);

            var report = Report(
                Avatar(renderers: new[]
                {
                    Renderer(submeshes: new[] { skin, hair }),
                    Renderer(submeshes: new[] { skin }),
                }),
                Avatar(renderers: new[] { Renderer(submeshes: new[] { skin }) }));

            // Two in the first avatar, one in the second. The same asset in two
            // avatars counts twice, because avatar-scoped identity is exactly
            // what stops the census from recording cross-avatar sharing.
            Assert.AreEqual(3, report.DistinctMaterialCount);
        }

        [Test]
        public void DistributesRenderersOverEveryRefusalIncludingUnobservedOnes()
        {
            var report = Report(Avatar(renderers: new[]
            {
                Renderer(submeshes: new[] { Submesh(provenOpaque: 1) }),
                RefusedRenderer(RendererRefusal.UnsupportedTopology, submeshCount: 1,
                    triangleCount: 4),
                RefusedRenderer(RendererRefusal.UnsupportedTopology, submeshCount: 1,
                    triangleCount: 4),
            }));

            Assert.AreEqual(1, report.RendererCountByRefusal[RendererRefusal.None]);
            Assert.AreEqual(
                2, report.RendererCountByRefusal[RendererRefusal.UnsupportedTopology]);
            Assert.AreEqual(0, report.RendererCountByRefusal[RendererRefusal.MissingMesh]);
            Assert.AreEqual(
                Enum.GetValues(typeof(RendererRefusal)).Length,
                report.RendererCountByRefusal.Count);
        }

        [Test]
        public void DistributesRenderersOverEveryKind()
        {
            var report = Report(Avatar(renderers: new[]
            {
                Renderer(kind: RendererKind.MeshRenderer,
                    submeshes: new[] { Submesh(provenOpaque: 1) }),
                Renderer(kind: RendererKind.SkinnedMeshRenderer,
                    submeshes: new[] { Submesh(provenOpaque: 1) }),
                RefusedRenderer(RendererRefusal.UnsupportedRendererType,
                    kind: RendererKind.Other),
            }));

            Assert.AreEqual(1, report.RendererCountByKind[RendererKind.MeshRenderer]);
            Assert.AreEqual(1, report.RendererCountByKind[RendererKind.SkinnedMeshRenderer]);
            Assert.AreEqual(1, report.RendererCountByKind[RendererKind.Other]);
        }

        [Test]
        public void WeighsAlphaResolutionFailuresByTriangleAsWellAsBySubmesh()
        {
            var report = Report(Avatar(renderers: new[]
            {
                Renderer(submeshes: new[]
                {
                    Submesh(
                        alphaFailure: AlphaResolutionFailure.SemanticsUnknown,
                        unknown: 100),
                    Submesh(
                        submeshIndex: 1,
                        materialSlotIndex: 1,
                        alphaFailure: AlphaResolutionFailure.SemanticsUnknown,
                        unknown: 20),
                    Submesh(
                        submeshIndex: 2,
                        materialSlotIndex: 2,
                        alphaFailure: AlphaResolutionFailure.MissingTextureEvidence,
                        unknown: 5),
                }),
            }));

            Assert.AreEqual(
                2,
                report.SubmeshCountByAlphaFailure[AlphaResolutionFailure.SemanticsUnknown]);
            Assert.AreEqual(
                120,
                report.TriangleCountByAlphaFailure[AlphaResolutionFailure.SemanticsUnknown]);
            Assert.AreEqual(
                5,
                report.TriangleCountByAlphaFailure[
                    AlphaResolutionFailure.MissingTextureEvidence]);
            Assert.AreEqual(
                Enum.GetValues(typeof(AlphaResolutionFailure)).Length,
                report.SubmeshCountByAlphaFailure.Count);
        }

        [Test]
        public void WeighsSeparationDispositionsByTriangleAsWellAsBySubmesh()
        {
            var report = Report(Avatar(renderers: new[]
            {
                Renderer(submeshes: new[]
                {
                    Submesh(disposition: SeparationDisposition.Split,
                        provenOpaque: 6, mustRemainTransparent: 4),
                    Submesh(submeshIndex: 1, materialSlotIndex: 1,
                        disposition: SeparationDisposition.WhollyOpaqueCandidate,
                        provenOpaque: 9),
                }),
            }));

            Assert.AreEqual(
                1, report.SubmeshCountByDisposition[SeparationDisposition.Split]);
            Assert.AreEqual(
                10, report.TriangleCountByDisposition[SeparationDisposition.Split]);
            Assert.AreEqual(
                9,
                report.TriangleCountByDisposition[
                    SeparationDisposition.WhollyOpaqueCandidate]);
            Assert.AreEqual(
                0, report.TriangleCountByDisposition[SeparationDisposition.Unchanged]);
        }

        [Test]
        public void DistributesMaterialsAndTrianglesOverShaderFamilies()
        {
            var report = Report(Avatar(renderers: new[]
            {
                Renderer(submeshes: new[]
                {
                    Submesh(materialName: "a", shaderName: ".poiyomi/Toon",
                        attestation: ShaderFamilyAttestation.Poiyomi, provenOpaque: 30),
                    Submesh(submeshIndex: 1, materialSlotIndex: 1,
                        materialName: "b", shaderName: "lilToon",
                        attestation: ShaderFamilyAttestation.LilToon, provenOpaque: 20),
                    Submesh(submeshIndex: 2, materialSlotIndex: 2,
                        materialName: "c", shaderName: "Custom/Secret", unknown: 50),
                }),
            }));

            Assert.AreEqual(1, report.DistinctMaterialCountByShaderFamily["Poiyomi"]);
            Assert.AreEqual(30, report.TriangleCountByShaderFamily["Poiyomi"]);
            Assert.AreEqual(20, report.TriangleCountByShaderFamily["LilToon"]);
            Assert.AreEqual(50, report.TriangleCountByShaderFamily["UnknownFamily-A"]);
        }

        [Test]
        public void ReportsTheHeadlineTriangleSplitAndItsOwnDenominator()
        {
            var report = Report(Avatar(renderers: new[]
            {
                Renderer(submeshes: new[]
                {
                    Submesh(provenOpaque: 70, mustRemainTransparent: 20, unknown: 10),
                }),
            }));

            Assert.AreEqual(70, report.ProvenOpaqueTriangleCount);
            Assert.AreEqual(20, report.MustRemainTransparentTriangleCount);
            Assert.AreEqual(10, report.UnknownTriangleCount);
            Assert.AreEqual(100, report.ClassifiedTriangleCount);
        }

        [Test]
        public void MeasuresTheUnknownAttributionBlindSpot()
        {
            var report = Report(Avatar(renderers: new[]
            {
                Renderer(submeshes: new[]
                {
                    // Unknown on a submesh AMUSE resolved: the blind spot, since
                    // no reason for it is recorded anywhere.
                    Submesh(
                        alphaFailure: AlphaResolutionFailure.None,
                        provenOpaque: 5,
                        unknown: 3),
                    // Unknown with a named failure: explained, not a blind spot.
                    Submesh(
                        submeshIndex: 1,
                        materialSlotIndex: 1,
                        alphaFailure: AlphaResolutionFailure.SemanticsUnknown,
                        unknown: 40),
                    // Resolved with nothing unknown: not a blind spot either.
                    Submesh(
                        submeshIndex: 2,
                        materialSlotIndex: 2,
                        alphaFailure: AlphaResolutionFailure.None,
                        provenOpaque: 9),
                }),
            }));

            Assert.AreEqual(1, report.UnknownBlindSpotSubmeshCount);
            Assert.AreEqual(3, report.UnknownBlindSpotTriangleCount);
        }

        [Test]
        public void CountsSubmeshesWithNoMaterial()
        {
            var report = Report(Avatar(renderers: new[]
            {
                Renderer(submeshes: new[]
                {
                    Submesh(hasMaterial: false, provenOpaque: 1),
                    Submesh(submeshIndex: 1, materialSlotIndex: 1, provenOpaque: 1),
                }),
            }));

            Assert.AreEqual(1, report.SubmeshesWithoutMaterialCount);
        }

        [Test]
        public void CountsAvatarsWithAnyProvenOpaqueGeometryWithoutListingThem()
        {
            var report = Report(
                Avatar(renderers: new[]
                {
                    Renderer(submeshes: new[] { Submesh(provenOpaque: 1) }),
                }),
                Avatar(renderers: new[]
                {
                    Renderer(submeshes: new[] { Submesh(mustRemainTransparent: 1) }),
                }),
                Avatar(renderers: new[]
                {
                    Renderer(submeshes: new[]
                    {
                        Submesh(mustRemainTransparent: 1),
                        Submesh(submeshIndex: 1, materialSlotIndex: 1, provenOpaque: 1),
                    }),
                }));

            Assert.AreEqual(3, report.AvatarCount);
            Assert.AreEqual(2, report.AvatarsWithAtLeastOneOpaqueCandidate);
        }

        [Test]
        public void AggregateRejectsANullCensus()
        {
            Assert.Throws<ArgumentNullException>(() => CensusAggregator.Aggregate(null));
        }
    }
}
