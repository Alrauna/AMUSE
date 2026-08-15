using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.AlphaMaterialOptimizer.Tests.Editor.ReferenceFixtures
{
    public sealed class ReferenceFixtureIntegrityTests
    {
        private static readonly string[] ExpectedCaseIds =
        {
            "fully-opaque-texture",
            "alpha-254-boundary",
            "fully-transparent-texture",
            "mixed-alpha-texture",
            "triangle-in-opaque-region",
            "triangle-in-transparent-region",
            "triangle-crosses-alpha-boundary",
            "mixed-triangle-mesh",
            "outside-uv-clamp",
            "outside-uv-repeat",
            "degenerate-triangle",
            "missing-uv0",
            "bilinear-filter-boundary"
        };

        [Test]
        public void CatalogsLoadDeterministicallyAndValidate()
        {
            var first = ReferenceFixtureData.Load();
            var second = ReferenceFixtureData.Load();

            Assert.That(JsonUtility.ToJson(first.Inputs),
                Is.EqualTo(JsonUtility.ToJson(second.Inputs)));
            Assert.That(JsonUtility.ToJson(first.Expectations),
                Is.EqualTo(JsonUtility.ToJson(second.Expectations)));
            Assert.DoesNotThrow(() => ReferenceFixtureData.Validate(first));
        }

        [Test]
        public void CatalogContainsExactlyApprovedCaseIds()
        {
            var catalogs = ReferenceFixtureData.Load();
            CollectionAssert.AreEquivalent(
                ExpectedCaseIds,
                catalogs.Inputs.cases.Select(item => item.id));
            CollectionAssert.AreEquivalent(
                ExpectedCaseIds,
                catalogs.Expectations.cases.Select(item => item.caseId));
        }

        [Test]
        public void RecordCollectionOrderDoesNotAffectResolution()
        {
            var catalogs = ReferenceFixtureData.Load();
            Array.Reverse(catalogs.Inputs.textures);
            Array.Reverse(catalogs.Inputs.meshes);
            Array.Reverse(catalogs.Inputs.cases);
            Array.Reverse(catalogs.Expectations.cases);

            Assert.DoesNotThrow(() => ReferenceFixtureData.Validate(catalogs));
            Assert.That(
                ReferenceFixtureData.FindCase(catalogs.Inputs, "outside-uv-repeat").wrapMode,
                Is.EqualTo("Repeat"));
            Assert.That(
                ReferenceFixtureData.FindExpectation(
                    catalogs.Expectations,
                    "outside-uv-repeat").triangleOutcomes[0].outcome,
                Is.EqualTo("ProvenOpaque"));
        }

        [TestCase("alpha-254-boundary", 0, "MustRemainTransparent")]
        [TestCase("degenerate-triangle", 0, "Unknown")]
        [TestCase("missing-uv0", 0, "Unknown")]
        public void ConservativeBoundaryOutcomesAreExplicit(
            string caseId,
            int triangleIndex,
            string expectedOutcome)
        {
            var expectation = ReferenceFixtureData.FindExpectation(
                ReferenceFixtureData.Load().Expectations,
                caseId);
            var triangle = expectation.triangleOutcomes.Single(
                item => item.triangleIndex == triangleIndex);

            Assert.That(triangle.outcome, Is.EqualTo(expectedOutcome));
        }
    }
}
