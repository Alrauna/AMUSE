using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using BigInteger = System.Numerics.BigInteger;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Tests.Editor.ReferenceFixtures;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    public sealed class TriangleAlphaClassifierTests
    {
        [TestCase("degenerate-triangle")]
        [TestCase("missing-uv0")]
        public void ExplicitUncertaintyMatchesOracle(string caseId)
        {
            AssertCaseMatchesOracle(caseId);
        }

        [Test]
        public void NonFiniteGeometryIsMalformed()
        {
            var triangle = TriangleAlphaInput.MissingUv0(
                new Vector3(float.NaN, 0f, 0f),
                Vector3.right,
                Vector3.up);
            var texture = new AlphaTextureData(1, 1, new byte[] { 255 });

            Assert.Throws<ArgumentException>(() => TriangleAlphaClassifier.Classify(
                triangle,
                texture,
                new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp)));
        }

        [TestCase(0, 0, 0)]
        [TestCase(unchecked((int)0x80000000), 0, 0)]
        [TestCase(0x00000001, 1, -149)]
        [TestCase(unchecked((int)0xBFC00000), -3, -1)]
        [TestCase(0x41000000, 1, 3)]
        public void FloatDecoderProducesCanonicalExactDyadic(
            int bits,
            long expectedSignificand,
            int expectedExponent)
        {
            var value = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
            var decoded = ExactUvGeometry.DecodeFloat(value);

            Assert.That(
                decoded.Significand,
                Is.EqualTo(new BigInteger(expectedSignificand)));
            Assert.That(decoded.Exponent, Is.EqualTo(expectedExponent));
        }

        [Test]
        public void GeometryDegeneracyUsesExactDecodedValues()
        {
            var degenerate = TriangleAlphaInput.MissingUv0(
                Vector3.zero,
                new Vector3(1f, 0.5f, 0.25f),
                new Vector3(2f, 1f, 0.5f));

            Assert.That(
                ExactUvGeometry.IsDegenerateGeometry(degenerate),
                Is.True);
        }

        [TestCase("fully-opaque-texture")]
        [TestCase("alpha-254-boundary")]
        [TestCase("fully-transparent-texture")]
        public void UniformAlphaCasesMatchOracle(string caseId)
        {
            AssertCaseMatchesOracle(caseId);
        }

        [Test]
        public void TextureDataCopiesCallerAlpha()
        {
            var source = new byte[] { 255 };
            var texture = new AlphaTextureData(1, 1, source);

            source[0] = 0;

            Assert.That(texture.GetAlpha(0, 0), Is.EqualTo(255));
            Assert.That(texture.IsFullyOpaque, Is.True);
        }

        [TestCase("mixed-alpha-texture")]
        [TestCase("triangle-in-opaque-region")]
        [TestCase("triangle-in-transparent-region")]
        [TestCase("triangle-crosses-alpha-boundary")]
        [TestCase("mixed-triangle-mesh")]
        [TestCase("outside-uv-clamp")]
        public void PointClampCasesMatchOracle(string caseId)
        {
            AssertCaseMatchesOracle(caseId);
        }

        [Test]
        public void PointClampCollapsedPointInOpaqueCellIsProvenOpaque()
        {
            Assert.That(
                ClassifyPointClamp(
                    new Vector2(0.25f, 0.5f),
                    new Vector2(0.25f, 0.5f),
                    new Vector2(0.25f, 0.5f)),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void PointClampCollapsedLineEnteringNonOpaqueCellRemainsTransparent()
        {
            Assert.That(
                ClassifyPointClamp(
                    new Vector2(0.25f, 0.5f),
                    new Vector2(0.75f, 0.5f),
                    new Vector2(0.25f, 0.5f)),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void PointClampUpperBoundaryBelongsOnlyToNextCell()
        {
            Assert.That(
                ClassifyPointClamp(
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f)),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void PointRepeatFixtureMatchesOracle()
        {
            AssertCaseMatchesOracle("outside-uv-repeat");
        }

        [TestCase(-4, -1, 0)]
        [TestCase(-5, -2, 3)]
        [TestCase(-3, -1, 1)]
        [TestCase(-1, -1, 3)]
        public void RepeatFloorArithmeticUsesMathematicalFloor(
            int value,
            int expectedQuotient,
            int expectedRemainder)
        {
            Assert.That(
                ExactUvGeometry.FloorDiv(value, 4),
                Is.EqualTo(new BigInteger(expectedQuotient)));
            Assert.That(
                ExactUvGeometry.FloorMod(value, 4),
                Is.EqualTo(expectedRemainder));
        }

        [Test]
        public void PointRepeatNegativeCoordinateWrapsIntoOpaqueTexel()
        {
            Assert.That(
                ClassifyPointRepeat(-0.25f, -0.25f, -0.25f),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void PointRepeatNegativeIntegerSeamSelectsPeriodCellZero()
        {
            Assert.That(
                ClassifyPointRepeat(-1f, -1f, -1f),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void PointRepeatIntegerPeriodTranslationPreservesOutcome()
        {
            var original = ClassifyPointRepeat(0.3f, 0.6f, 0.3f);
            var translated = ClassifyPointRepeat(17.3f, 17.6f, 17.3f, -22.5f);

            Assert.That(translated, Is.EqualTo(original));
        }

        [TestCase(0.1f, 1.3f)]
        [TestCase(0.1f, 10.1f)]
        public void PointRepeatSpanFindsNonOpaqueCell(float minimumU, float maximumU)
        {
            Assert.That(
                ClassifyPointRepeat(minimumU, maximumU, minimumU),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void PointRepeatOverBudgetMixedSpanIsUnknown()
        {
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0f, 0.5f),
                new Vector2(40000f, 0.5f),
                new Vector2(0f, 0.5f));
            var texture = new AlphaTextureData(2, 1, new byte[] { 255, 0 });

            Assert.That(
                TriangleAlphaClassifier.Classify(
                    triangle,
                    texture,
                    new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Repeat)),
                Is.EqualTo(TriangleAlphaOutcome.Unknown));
        }

        [Test]
        public void BilinearBoundaryFixtureMatchesOracle()
        {
            AssertCaseMatchesOracle("bilinear-filter-boundary");
        }

        [TestCase(0f, "ProvenOpaque")]
        [TestCase(0.1f, "ProvenOpaque")]
        [TestCase(0.25f, "ProvenOpaque")]
        [TestCase(0.375f, "MustRemainTransparent")]
        [TestCase(1f, "MustRemainTransparent")]
        public void BilinearClampPointUsesOnlyPositiveWeightSupport(
            float u,
            string expected)
        {
            Assert.That(ClassifyBilinearClamp(u, u, u).ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void BilinearClampCollapsedLineWhollyOnOpaqueSideIsProvenOpaque()
        {
            Assert.That(
                ClassifyBilinearClamp(0f, 0.25f, 0f),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void BilinearClampOneTexelWideStillChecksVerticalSupport()
        {
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.5f, 0.75f),
                new Vector2(0.5f, 0.75f),
                new Vector2(0.5f, 0.75f));
            var texture = new AlphaTextureData(1, 2, new byte[] { 255, 0 });

            Assert.That(
                TriangleAlphaClassifier.Classify(
                    triangle,
                    texture,
                    new AlphaSamplingSettings(AlphaFilterMode.Bilinear, AlphaWrapMode.Clamp)),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(17f)]
        public void BilinearRepeatSupportCrossesIntegerSeam(float u)
        {
            Assert.That(
                ClassifyBilinearRepeat(u, u, u),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void BilinearRepeatIntegerTranslationPreservesOutcome()
        {
            Assert.That(
                ClassifyBilinearRepeat(0.125f, 0.125f, 0.125f),
                Is.EqualTo(ClassifyBilinearRepeat(19.125f, 19.125f, 19.125f)));
        }

        [Test]
        public void BilinearRepeatOneTexelOpaqueTextureIsProvenOpaque()
        {
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(-3f, 4f),
                new Vector2(7f, -8f),
                new Vector2(11f, 12f));
            var texture = new AlphaTextureData(1, 1, new byte[] { 255 });

            Assert.That(
                TriangleAlphaClassifier.Classify(
                    triangle,
                    texture,
                    new AlphaSamplingSettings(AlphaFilterMode.Bilinear, AlphaWrapMode.Repeat)),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void BilinearRepeatOverBudgetMixedSpanIsUnknown()
        {
            Assert.That(
                ClassifyBilinearRepeat(0f, 40000f, 0f),
                Is.EqualTo(TriangleAlphaOutcome.Unknown));
        }

        [Test]
        public void ReversingWindingAndMatchingUvsPreservesOutcome()
        {
            var texture = new AlphaTextureData(2, 2, new byte[] { 255, 0, 0, 255 });
            var forward = TriangleAlphaInput.WithUv0(
                Vector3.zero, Vector3.right, Vector3.up,
                new Vector2(0.1f, 0.1f),
                new Vector2(0.9f, 0.9f),
                new Vector2(0.55f, 0.65f));
            var reversed = TriangleAlphaInput.WithUv0(
                Vector3.zero, Vector3.up, Vector3.right,
                new Vector2(0.1f, 0.1f),
                new Vector2(0.55f, 0.65f),
                new Vector2(0.9f, 0.9f));
            var sampling = new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp);

            Assert.That(
                TriangleAlphaClassifier.Classify(reversed, texture, sampling),
                Is.EqualTo(TriangleAlphaClassifier.Classify(forward, texture, sampling)));
        }

        [Test]
        public void PointTriangleVerticesInOpaqueCellsStillFindInteriorNonOpaqueCell()
        {
            var texture = new AlphaTextureData(2, 2, new byte[] { 255, 0, 0, 255 });
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero, Vector3.right, Vector3.up,
                new Vector2(0.1f, 0.1f),
                new Vector2(0.9f, 0.9f),
                new Vector2(0.55f, 0.65f));

            Assert.That(
                TriangleAlphaClassifier.Classify(
                    triangle,
                    texture,
                    new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp)),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void PointClampLongThinTriangleFindsNonOpaqueCell()
        {
            var texture = new AlphaTextureData(4, 1, new byte[] { 255, 255, 0, 255 });
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero, Vector3.right, Vector3.up,
                new Vector2(0.1f, 0.5f),
                new Vector2(0.9f, 0.5f),
                new Vector2(0.1f, 0.5001f));

            Assert.That(
                TriangleAlphaClassifier.Classify(
                    triangle,
                    texture,
                    new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp)),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void VeryLargeRepeatOffsetsPreservePointOutcome()
        {
            var baseline = ClassifyPointRepeat(0.125f, 0.125f, 0.125f);

            Assert.That(
                ClassifyPointRepeat(65536.125f, 65536.125f, 65536.125f),
                Is.EqualTo(baseline));
            Assert.That(
                ClassifyPointRepeat(-65535.875f, -65535.875f, -65535.875f),
                Is.EqualTo(baseline));
        }

        [TestCase(255, "ProvenOpaque")]
        [TestCase(0, "MustRemainTransparent")]
        public void UniformFastPathPrecedesMixedSpanBudget(
            byte alpha,
            string expected)
        {
            var texture = new AlphaTextureData(1, 1, new[] { alpha });
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero, Vector3.right, Vector3.up,
                Vector2.zero,
                new Vector2(100000f, 100000f),
                new Vector2(-100000f, -100000f));

            Assert.That(
                TriangleAlphaClassifier.Classify(
                    triangle,
                    texture,
                    new AlphaSamplingSettings(AlphaFilterMode.Bilinear, AlphaWrapMode.Repeat))
                    .ToString(),
                Is.EqualTo(expected));
        }

        [Test]
        public void MissingUvNeverPromotesPresentUvResult()
        {
            var texture = new AlphaTextureData(2, 1, new byte[] { 255, 0 });
            var present = TriangleAlphaInput.WithUv0(
                Vector3.zero, Vector3.right, Vector3.up,
                new Vector2(0.1f, 0.5f),
                new Vector2(0.2f, 0.5f),
                new Vector2(0.1f, 0.5f));
            var missing = TriangleAlphaInput.MissingUv0(
                Vector3.zero, Vector3.right, Vector3.up);
            var sampling = new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp);

            Assert.That(
                TriangleAlphaClassifier.Classify(present, texture, sampling),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(
                TriangleAlphaClassifier.Classify(missing, texture, sampling),
                Is.EqualTo(TriangleAlphaOutcome.Unknown));
        }

        [Test]
        public void MalformedInputsThrow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new AlphaTextureData(0, 1, new byte[] { 255 }));
            Assert.Throws<ArgumentException>(
                () => new AlphaTextureData(2, 1, new byte[] { 255 }));

            var texture = new AlphaTextureData(2, 1, new byte[] { 255, 0 });
            var finite = TriangleAlphaInput.WithUv0(
                Vector3.zero, Vector3.right, Vector3.up,
                Vector2.zero, Vector2.right, Vector2.up);
            var nonFiniteUv = TriangleAlphaInput.WithUv0(
                Vector3.zero, Vector3.right, Vector3.up,
                new Vector2(float.PositiveInfinity, 0f), Vector2.right, Vector2.up);

            Assert.Throws<ArgumentOutOfRangeException>(() => TriangleAlphaClassifier.Classify(
                finite,
                texture,
                new AlphaSamplingSettings((AlphaFilterMode)99, AlphaWrapMode.Clamp)));
            Assert.Throws<ArgumentException>(() => TriangleAlphaClassifier.Classify(
                nonFiniteUv,
                texture,
                new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp)));
        }

        [TestCase("Point")]
        [TestCase("Bilinear")]
        public void Alpha254IsNonOpaqueForDirectInputs(string filterMode)
        {
            var texture = new AlphaTextureData(1, 1, new byte[] { 254 });
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero, Vector3.right, Vector3.up,
                Vector2.zero, Vector2.right, Vector2.up);

            Assert.That(
                TriangleAlphaClassifier.Classify(
                    triangle,
                    texture,
                    new AlphaSamplingSettings(
                        filterMode == "Point" ? AlphaFilterMode.Point : AlphaFilterMode.Bilinear,
                        AlphaWrapMode.Clamp)),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        private static TriangleAlphaOutcome ClassifyBilinearRepeat(
            float u0,
            float u1,
            float u2)
        {
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(u0, 0.5f),
                new Vector2(u1, 0.5f),
                new Vector2(u2, 0.5f));
            var texture = new AlphaTextureData(2, 1, new byte[] { 255, 0 });
            return TriangleAlphaClassifier.Classify(
                triangle,
                texture,
                new AlphaSamplingSettings(AlphaFilterMode.Bilinear, AlphaWrapMode.Repeat));
        }

        private static TriangleAlphaOutcome ClassifyBilinearClamp(
            float u0,
            float u1,
            float u2)
        {
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(u0, 0.5f),
                new Vector2(u1, 0.5f),
                new Vector2(u2, 0.5f));
            var texture = new AlphaTextureData(2, 1, new byte[] { 255, 0 });
            return TriangleAlphaClassifier.Classify(
                triangle,
                texture,
                new AlphaSamplingSettings(AlphaFilterMode.Bilinear, AlphaWrapMode.Clamp));
        }

        private static TriangleAlphaOutcome ClassifyPointRepeat(
            float u0,
            float u1,
            float u2,
            float v = 0.5f)
        {
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(u0, v),
                new Vector2(u1, v),
                new Vector2(u2, v));
            var texture = new AlphaTextureData(4, 1, new byte[] { 255, 0, 255, 255 });
            return TriangleAlphaClassifier.Classify(
                triangle,
                texture,
                new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Repeat));
        }

        private static TriangleAlphaOutcome ClassifyPointClamp(
            Vector2 uv0,
            Vector2 uv1,
            Vector2 uv2)
        {
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                uv0,
                uv1,
                uv2);
            var texture = new AlphaTextureData(2, 1, new byte[] { 255, 0 });
            return TriangleAlphaClassifier.Classify(
                triangle,
                texture,
                new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp));
        }

        private static TriangleAlphaOutcome[] ClassifyInputCase(
            FixtureInputCatalog inputs,
            string caseId)
        {
            var fixtureCase = ReferenceFixtureData.FindCase(inputs, caseId);
            var textureRecord = inputs.textures.Single(item => item.id == fixtureCase.textureId);
            var meshRecord = inputs.meshes.Single(item => item.id == fixtureCase.meshId);
            var texture = new AlphaTextureData(
                textureRecord.width,
                textureRecord.height,
                textureRecord.alpha8BottomToTop
                    .Select(value => checked((byte)value))
                    .ToArray());
            var sampling = new AlphaSamplingSettings(
                fixtureCase.filterMode == "Point"
                    ? AlphaFilterMode.Point
                    : AlphaFilterMode.Bilinear,
                fixtureCase.wrapMode == "Clamp"
                    ? AlphaWrapMode.Clamp
                    : AlphaWrapMode.Repeat);
            var results = new TriangleAlphaOutcome[
                meshRecord.triangleVertexIndices.Length / 3];

            for (var triangleIndex = 0; triangleIndex < results.Length; triangleIndex++)
            {
                var offset = triangleIndex * 3;
                var i0 = meshRecord.triangleVertexIndices[offset];
                var i1 = meshRecord.triangleVertexIndices[offset + 1];
                var i2 = meshRecord.triangleVertexIndices[offset + 2];
                var triangle = CreateTriangleInput(meshRecord, i0, i1, i2);
                results[triangleIndex] = TriangleAlphaClassifier.Classify(
                    triangle,
                    texture,
                    sampling);
            }

            return results;
        }

        private static void AssertCaseMatchesOracle(string caseId)
        {
            var catalogs = ReferenceFixtureData.Load();
            var actual = ClassifyInputCase(catalogs.Inputs, caseId);
            var expected = ReferenceFixtureData.FindExpectation(
                    catalogs.Expectations,
                    caseId)
                .triangleOutcomes
                .OrderBy(item => item.triangleIndex)
                .Select(item => item.outcome)
                .ToArray();

            CollectionAssert.AreEqual(
                expected,
                actual.Select(item => item.ToString()),
                caseId);
        }

        private static TriangleAlphaInput CreateTriangleInput(
            MeshFixtureRecord mesh,
            int i0,
            int i1,
            int i2)
        {
            var p0 = PositionAt(mesh, i0);
            var p1 = PositionAt(mesh, i1);
            var p2 = PositionAt(mesh, i2);
            if (mesh.uv0Status == "Missing")
            {
                return TriangleAlphaInput.MissingUv0(p0, p1, p2);
            }

            return TriangleAlphaInput.WithUv0(
                p0,
                p1,
                p2,
                UvAt(mesh, i0),
                UvAt(mesh, i1),
                UvAt(mesh, i2));
        }

        private static Vector3 PositionAt(MeshFixtureRecord mesh, int index)
        {
            return new Vector3(
                mesh.positions[index * 3],
                mesh.positions[index * 3 + 1],
                mesh.positions[index * 3 + 2]);
        }

        private static Vector2 UvAt(MeshFixtureRecord mesh, int index)
        {
            return new Vector2(
                mesh.uv0[index * 2],
                mesh.uv0[index * 2 + 1]);
        }
    }
}
