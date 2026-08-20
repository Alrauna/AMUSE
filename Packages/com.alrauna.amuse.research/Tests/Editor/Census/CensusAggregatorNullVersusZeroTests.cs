using Alrauna.Amuse.Research.Census;
using NUnit.Framework;
using static Alrauna.Amuse.Research.Tests.Editor.Census.CensusObservationBuilders;

namespace Alrauna.Amuse.Research.Tests.Editor.Census
{
    /// <summary>
    /// The miscount the harness design calls the most likely in the whole
    /// system: reading a refused renderer's unavailable counts as zero. A
    /// census that averages refusals in as zero understates avatar complexity
    /// and overstates coverage, and it does so silently.
    /// </summary>
    public sealed class CensusAggregatorNullVersusZeroTests
    {
        private static CensusAggregateReport Report(params ObservedAvatar[] avatars)
        {
            return CensusAggregator.Aggregate(CensusAnonymizer.Anonymize(Set(avatars)));
        }

        [Test]
        public void UnreachableTriangleCountsAreExcludedFromTheTotalAndFromItsDenominator()
        {
            var report = Report(Avatar(renderers: new[]
            {
                Renderer(submeshes: new[] { Submesh(provenOpaque: 40) }),
                RefusedRenderer(RendererRefusal.MissingMesh),
                RefusedRenderer(RendererRefusal.UnsupportedRendererType),
            }));

            Assert.AreEqual(3, report.RendererCount);
            Assert.AreEqual(1, report.RenderersWithKnownTriangleCount);
            Assert.AreEqual(2, report.RenderersWithUnknownTriangleCount);
            Assert.AreEqual(40, report.TotalRendererTriangleCount);
        }

        [Test]
        public void ARefusalWithAReachableMeshStillContributesItsTriangles()
        {
            var report = Report(Avatar(renderers: new[]
            {
                Renderer(submeshes: new[] { Submesh(provenOpaque: 40) }),
                RefusedRenderer(
                    RendererRefusal.MaterialPropertyOverridesPresent,
                    submeshCount: 2,
                    triangleCount: 60),
            }));

            Assert.AreEqual(2, report.RenderersWithKnownTriangleCount);
            Assert.AreEqual(0, report.RenderersWithUnknownTriangleCount);
            Assert.AreEqual(100, report.TotalRendererTriangleCount);
        }

        [Test]
        public void TotalTrianglesIsNullWhenNoRendererHadAKnownCount()
        {
            var report = Report(Avatar(renderers: new[]
            {
                RefusedRenderer(RendererRefusal.MissingMesh),
                RefusedRenderer(RendererRefusal.UnsupportedRendererType),
            }));

            // Not zero. There is no denominator, so there is no honest total,
            // and a reader must be able to tell those apart.
            Assert.IsNull(report.TotalRendererTriangleCount);
            Assert.AreEqual(0, report.RenderersWithKnownTriangleCount);
            Assert.AreEqual(2, report.RenderersWithUnknownTriangleCount);
        }

        [Test]
        public void TotalSubmeshesIsNullWhenNoRendererHadAKnownCount()
        {
            var report = Report(Avatar(renderers: new[]
            {
                RefusedRenderer(RendererRefusal.MissingMesh),
            }));

            Assert.IsNull(report.TotalRendererSubmeshCount);
            Assert.AreEqual(0, report.RenderersWithKnownSubmeshCount);
            Assert.AreEqual(1, report.RenderersWithUnknownSubmeshCount);
        }

        [Test]
        public void RefusedGeometryIsCountedAsUnclassifiedRatherThanAsClassifiedZero()
        {
            var report = Report(Avatar(renderers: new[]
            {
                Renderer(submeshes: new[] { Submesh(provenOpaque: 40) }),
                RefusedRenderer(
                    RendererRefusal.UnsupportedTopology,
                    submeshCount: 1,
                    triangleCount: 60),
            }));

            // The gap between these two numbers is the coverage story: 60
            // triangles exist and AMUSE classified none of them. Collapsing
            // them would report full coverage of an avatar it half understood.
            Assert.AreEqual(100, report.TotalRendererTriangleCount);
            Assert.AreEqual(40, report.ClassifiedTriangleCount);
        }

        [Test]
        public void ARefusedRendererContributesNoSubmeshRecords()
        {
            var report = Report(Avatar(renderers: new[]
            {
                RefusedRenderer(
                    RendererRefusal.UnprovenMaterialSlotMapping,
                    submeshCount: 4,
                    triangleCount: 80),
            }));

            Assert.AreEqual(0, report.SubmeshRecordCount);
            Assert.AreEqual(4, report.TotalRendererSubmeshCount);
            Assert.AreEqual(0, report.ClassifiedTriangleCount);
        }
    }
}
