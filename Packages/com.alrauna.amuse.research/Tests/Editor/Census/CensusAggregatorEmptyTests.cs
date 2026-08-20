using System;
using Alrauna.Amuse.Research.Census;
using NUnit.Framework;
using static Alrauna.Amuse.Research.Tests.Editor.Census.CensusObservationBuilders;

namespace Alrauna.Amuse.Research.Tests.Editor.Census
{
    /// <summary>
    /// An empty run must aggregate to an honest empty report rather than
    /// throwing or inventing a denominator. The Lab may be recreated between
    /// runs, so a run over nothing is an ordinary input.
    /// </summary>
    public sealed class CensusAggregatorEmptyTests
    {
        private static CensusAggregateReport EmptyReport()
        {
            return CensusAggregator.Aggregate(CensusAnonymizer.Anonymize(Set()));
        }

        [Test]
        public void AnEmptyRunCountsNothing()
        {
            var report = EmptyReport();

            Assert.AreEqual(0, report.AvatarCount);
            Assert.AreEqual(0, report.RendererCount);
            Assert.AreEqual(0, report.SubmeshRecordCount);
            Assert.AreEqual(0, report.DistinctMaterialCount);
            Assert.AreEqual(0, report.ClassifiedTriangleCount);
            Assert.AreEqual(0, report.AvatarsWithAtLeastOneOpaqueCandidate);
        }

        [Test]
        public void AnEmptyRunHasNoTotalRatherThanATotalOfZero()
        {
            var report = EmptyReport();

            Assert.IsNull(report.TotalRendererTriangleCount);
            Assert.IsNull(report.TotalRendererSubmeshCount);
        }

        [Test]
        public void AnEmptyRunStillCarriesEveryCategoryKey()
        {
            var report = EmptyReport();

            Assert.AreEqual(
                Enum.GetValues(typeof(RendererRefusal)).Length,
                report.RendererCountByRefusal.Count);
            Assert.AreEqual(
                Enum.GetValues(typeof(RendererKind)).Length,
                report.RendererCountByKind.Count);
            Assert.AreEqual(
                Enum.GetValues(typeof(AlphaResolutionFailure)).Length,
                report.SubmeshCountByAlphaFailure.Count);
            Assert.AreEqual(
                Enum.GetValues(typeof(SeparationDisposition)).Length,
                report.TriangleCountByDisposition.Count);

            foreach (var count in report.RendererCountByRefusal.Values)
                Assert.AreEqual(0, count);
        }

        [Test]
        public void AnEmptyRunObservedNoShaderFamilies()
        {
            // Family keys come from observation, not from a fixed vocabulary,
            // so an empty run has none rather than a row of zeroes for families
            // nobody saw.
            var report = EmptyReport();

            Assert.AreEqual(0, report.DistinctMaterialCountByShaderFamily.Count);
            Assert.AreEqual(0, report.TriangleCountByShaderFamily.Count);
        }

        [Test]
        public void AnAvatarWithNoRenderersIsStillAnAvatar()
        {
            var report = CensusAggregator.Aggregate(
                CensusAnonymizer.Anonymize(Set(Avatar(), Avatar())));

            Assert.AreEqual(2, report.AvatarCount);
            Assert.AreEqual(0, report.RendererCount);
            Assert.IsNull(report.TotalRendererTriangleCount);
        }
    }
}
