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

        [Test]
        public void EveryCaseBuildsDeterministicallyWithoutMipmaps()
        {
            var inputs = ReferenceFixtureData.Load().Inputs;

            foreach (var fixtureCase in inputs.cases)
            {
                var textureRecord = inputs.textures.Single(item => item.id == fixtureCase.textureId);
                var meshRecord = inputs.meshes.Single(item => item.id == fixtureCase.meshId);

                using (var first = ReferenceFixtureData.BuildCase(inputs, fixtureCase.id))
                using (var second = ReferenceFixtureData.BuildCase(inputs, fixtureCase.id))
                {
                    Assert.That(first.Texture, Is.Not.SameAs(second.Texture), fixtureCase.id);
                    Assert.That(first.Mesh, Is.Not.SameAs(second.Mesh), fixtureCase.id);
                    Assert.That(first.Texture.mipmapCount, Is.EqualTo(1), fixtureCase.id);
                    Assert.That(first.Texture.width, Is.EqualTo(textureRecord.width), fixtureCase.id);
                    Assert.That(first.Texture.height, Is.EqualTo(textureRecord.height), fixtureCase.id);
                    CollectionAssert.AreEqual(
                        textureRecord.alpha8BottomToTop.Select(alpha => (byte)alpha),
                        first.Texture.GetPixels32().Select(pixel => pixel.a),
                        fixtureCase.id);
                    CollectionAssert.AreEqual(
                        meshRecord.positions,
                        first.Mesh.vertices.SelectMany(vertex => new[] { vertex.x, vertex.y, vertex.z }),
                        fixtureCase.id);
                    CollectionAssert.AreEqual(meshRecord.uv0, first.Mesh.uv.SelectMany(uv => new[] { uv.x, uv.y }), fixtureCase.id);
                    CollectionAssert.AreEqual(meshRecord.triangleVertexIndices, first.Mesh.triangles, fixtureCase.id);
                    Assert.That(
                        first.Texture.filterMode,
                        Is.EqualTo(fixtureCase.filterMode == "Point" ? FilterMode.Point : FilterMode.Bilinear),
                        fixtureCase.id);
                    Assert.That(
                        first.Texture.wrapMode,
                        Is.EqualTo(fixtureCase.wrapMode == "Clamp" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat),
                        fixtureCase.id);
                    CollectionAssert.AreEqual(first.Texture.GetPixels32(), second.Texture.GetPixels32(), fixtureCase.id);
                    CollectionAssert.AreEqual(first.Mesh.vertices, second.Mesh.vertices, fixtureCase.id);
                    CollectionAssert.AreEqual(first.Mesh.uv, second.Mesh.uv, fixtureCase.id);
                    CollectionAssert.AreEqual(first.Mesh.triangles, second.Mesh.triangles, fixtureCase.id);
                    Assert.That(first.Texture.filterMode, Is.EqualTo(second.Texture.filterMode), fixtureCase.id);
                    Assert.That(first.Texture.wrapMode, Is.EqualTo(second.Texture.wrapMode), fixtureCase.id);
                }
            }
        }

        [Test]
        public void SharedLogicalDefinitionsDoNotShareMutableUnityObjects()
        {
            var inputs = ReferenceFixtureData.Load().Inputs;

            using (var clamp = ReferenceFixtureData.BuildCase(inputs, "outside-uv-clamp"))
            using (var repeat = ReferenceFixtureData.BuildCase(inputs, "outside-uv-repeat"))
            {
                Assert.That(clamp.Texture, Is.Not.SameAs(repeat.Texture));
                Assert.That(clamp.Mesh, Is.Not.SameAs(repeat.Mesh));
                Assert.That(clamp.Texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(repeat.Texture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));

                repeat.Texture.filterMode = FilterMode.Trilinear;
                Assert.That(clamp.Texture.filterMode, Is.EqualTo(FilterMode.Point));
            }
        }
    }
}
