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

            Assert.Throws<ArgumentException>(() => TriangleAlphaClassifier.Classify(triangle, texture, new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp), AlphaUvEnvelope.Zero));
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
                TriangleAlphaClassifier.Classify(triangle, texture, new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Repeat), AlphaUvEnvelope.Zero),
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
                TriangleAlphaClassifier.Classify(triangle, texture, new AlphaSamplingSettings(AlphaFilterMode.Bilinear, AlphaWrapMode.Clamp), AlphaUvEnvelope.Zero),
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
                TriangleAlphaClassifier.Classify(triangle, texture, new AlphaSamplingSettings(AlphaFilterMode.Bilinear, AlphaWrapMode.Repeat), AlphaUvEnvelope.Zero),
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
                TriangleAlphaClassifier.Classify(reversed, texture, sampling, AlphaUvEnvelope.Zero),
                Is.EqualTo(TriangleAlphaClassifier.Classify(forward, texture, sampling, AlphaUvEnvelope.Zero)));
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
                TriangleAlphaClassifier.Classify(triangle, texture, new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp), AlphaUvEnvelope.Zero),
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
                TriangleAlphaClassifier.Classify(triangle, texture, new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp), AlphaUvEnvelope.Zero),
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
                TriangleAlphaClassifier.Classify(triangle, texture, new AlphaSamplingSettings(AlphaFilterMode.Bilinear, AlphaWrapMode.Repeat), AlphaUvEnvelope.Zero)
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
                TriangleAlphaClassifier.Classify(present, texture, sampling, AlphaUvEnvelope.Zero),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(
                TriangleAlphaClassifier.Classify(missing, texture, sampling, AlphaUvEnvelope.Zero),
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

            Assert.Throws<ArgumentOutOfRangeException>(() => TriangleAlphaClassifier.Classify(finite, texture, new AlphaSamplingSettings((AlphaFilterMode)99, AlphaWrapMode.Clamp), AlphaUvEnvelope.Zero));
            Assert.Throws<ArgumentException>(() => TriangleAlphaClassifier.Classify(nonFiniteUv, texture, new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp), AlphaUvEnvelope.Zero));
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
                TriangleAlphaClassifier.Classify(triangle, texture, new AlphaSamplingSettings(
                    filterMode == "Point" ? AlphaFilterMode.Point : AlphaFilterMode.Bilinear,
                    AlphaWrapMode.Clamp), AlphaUvEnvelope.Zero),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        // Task 3 envelope fixtures: each pairs an AlphaUvEnvelope.Zero control
        // with a nonzero envelope against an 8x8 texture holding exactly one
        // non-opaque texel. HalfUvTexel8x8 is half a UV texel per axis
        // (1/16 UV); after the X*width*T conversion it inflates the exact
        // hull by exactly half a domain texel (TexelScale/2).

        private static readonly AlphaUvEnvelope HalfUvTexel8x8 =
            new AlphaUvEnvelope(new ExactRational(1, 16), new ExactRational(1, 16));

        [Test]
        public void PointClampHalfTexelEnvelopeReachesExactlyTheBoundaryCell()
        {
            // TexelScale T = 16 (finest UV exponent -4). The domain spans
            // x in [0, 2.5T] and the non-opaque texel (3, 0) starts at 3T.
            // Only the X*width*T unit conversion moves the hull by T/2 so it
            // touches the closed cell boundary. Falsifies a missing width or
            // missing T factor: X*width moves the hull 0.5 domain units and
            // X*T moves it 1 unit; both stay inside cell 2.
            var texture = Opaque8x8Except(3, 0);
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0f, 0f),
                new Vector2(0.3125f, 0f),
                new Vector2(0f, 0.0625f));
            var sampling = new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp);

            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, AlphaUvEnvelope.Zero),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, HalfUvTexel8x8),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void PointClampNonZeroEnvelopePullsBorderingNonOpaqueCell()
        {
            // TexelScale T = 16. The domain tops out at y = 4.5T; texel
            // (3, 5) begins at 5T. Zero envelope misses it; a T/2 y expansion
            // touches the closed lower boundary of row 5. Falsifies
            // classification that ignores or under-applies the envelope.
            var texture = Opaque8x8Except(3, 5);
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.375f, 0.5f),
                new Vector2(0.4375f, 0.5f),
                new Vector2(0.4375f, 0.5625f));
            var sampling = new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp);

            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, AlphaUvEnvelope.Zero),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, HalfUvTexel8x8),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void PointRepeatEnvelopeCrossesPeriodBoundaryToWrappedCell()
        {
            // TexelScale T = 16. The domain spans x in [7T, 7.5T]; the
            // non-opaque texel (0, 3) sits one period away as unwrapped cell
            // x = 8 whose interval [8T, 9T) starts at the boundary the T/2
            // expansion exactly reaches. Falsifies a missing envelope, a
            // missing candidate past the period, or Clamp-instead-of-Repeat
            // ownership of the wrapped cell.
            var texture = Opaque8x8Except(0, 3);
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.875f, 0.375f),
                new Vector2(0.9375f, 0.375f),
                new Vector2(0.875f, 0.4375f));
            var sampling = new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Repeat);

            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, AlphaUvEnvelope.Zero),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, HalfUvTexel8x8),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void BilinearClampInflatedHullReachesFootprintTexel()
        {
            // TexelScale T = 32. Domain x tops out at 2.25T; texel (3, 4)
            // has the bilinear footprint interval [2.5T, 3.5T). Zero envelope
            // stays half a quarter texel short of the footprint; the T/2
            // x expansion reaches 2.75T inside it. Falsifies classification
            // that lets the envelope shrink the one-texel footprint or
            // ignores the envelope entirely.
            var texture = Opaque8x8Except(3, 4);
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.125f, 0.5f),
                new Vector2(0.28125f, 0.5f),
                new Vector2(0.125f, 0.5625f));
            var sampling = new AlphaSamplingSettings(AlphaFilterMode.Bilinear, AlphaWrapMode.Clamp);

            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, AlphaUvEnvelope.Zero),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, HalfUvTexel8x8),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void BilinearRepeatInflatedHullReachesFootprintAcrossNormalization()
        {
            // TexelScale T = 32. Domain x spans [7T, 7.25T]; texel (0, 2)
            // is the wrapped cell x = 8 with footprint interval
            // [7.5T, 9.5T). Zero envelope misses; the T/2 expansion reaches
            // 7.75T after Repeat normalization. Falsifies classification
            // that drops the wrapped footprint cell or skips inflation.
            var texture = Opaque8x8Except(0, 2);
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.875f, 0.25f),
                new Vector2(0.90625f, 0.28125f),
                new Vector2(0.875f, 0.28125f));
            var sampling = new AlphaSamplingSettings(AlphaFilterMode.Bilinear, AlphaWrapMode.Repeat);

            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, AlphaUvEnvelope.Zero),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, HalfUvTexel8x8),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void PointRepeatEnvelopeInflationExceedsSupportRegionBudget()
        {
            // TexelScale T = 16; the domain spans [0, 255.5T] per axis, so
            // the un-inflated candidate region is exactly 256*256 =
            // MaxSupportRegions and still classifies (the wrapped non-opaque
            // texel (3, 3) lies under the triangle). Inflation by T/2 per
            // side widens the range to 258*258 regions, over budget, and
            // must degrade to Unknown rather than classify unproven.
            var texture = Opaque8x8Except(3, 3);
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0f, 0f),
                new Vector2(31.9375f, 0f),
                new Vector2(0f, 31.9375f));
            var sampling = new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Repeat);

            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, AlphaUvEnvelope.Zero),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, HalfUvTexel8x8),
                Is.EqualTo(TriangleAlphaOutcome.Unknown));
        }

        [Test]
        public void DegenerateAxisAlignedUvSegmentExpandsToRectangleBeforeClassification()
        {
            // Two distinct UVs leave a two-vertex domain: the horizontal
            // segment from (2T, 4T) to (6T, 4T) with TexelScale T = 4. The
            // T/2 per-corner expansion sweeps the exact rectangle
            // [1.5T, 6.5T] x [3.5T, 4.5T]. Zero envelope classifies the
            // segment alone: texel (1, 3) is missed both in x (the segment
            // starts at its exclusive upper bound 2T) and in y (the segment
            // rides the exclusive upper bound of row 3). Each axis of the
            // expansion is load-bearing: inflating only x still misses the
            // row, inflating only y still misses the column. Falsifies
            // refusal of degenerate UV domains or single-axis expansion.
            var texture = Opaque8x8Except(1, 3);
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.25f, 0.5f),
                new Vector2(0.75f, 0.5f),
                new Vector2(0.75f, 0.5f));
            var sampling = new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp);

            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, AlphaUvEnvelope.Zero),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, HalfUvTexel8x8),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void DegenerateDiagonalUvSegmentStaysOutsideItsHexagonHull()
        {
            // Two distinct UVs on the diagonal leave the segment from
            // (2T, 2T) to (6T, 6T) with TexelScale T = 4. The T/2
            // per-corner expansion is the exact Minkowski hexagon with
            // vertices (1.5T, 1.5T), (2.5T, 1.5T), (6.5T, 5.5T), (6.5T,
            // 6.5T), (5.5T, 6.5T), (1.5T, 2.5T) — not the bounding
            // rectangle [1.5T, 6.5T]^2. Texel (6, 1) overlaps that
            // rectangle (in [6T, 6.5T] x [1.5T, 2T]) but lies entirely
            // below the hexagon's cut corner, so an exact per-vertex
            // expansion proves the triangle opaque while a bounding-box
            // inflation would sample the texel and return
            // MustRemainTransparent. Falsifies replacing OutwardExpand
            // with bounding-box inflation.
            var texture = Opaque8x8Except(6, 1);
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.25f, 0.25f),
                new Vector2(0.75f, 0.75f),
                new Vector2(0.75f, 0.75f));
            var sampling = new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp);

            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, AlphaUvEnvelope.Zero),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, HalfUvTexel8x8),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void DegenerateMeshStaysUnknownWithNonZeroEnvelope()
        {
            // A zero-area mesh returns Unknown before any UV-domain or
            // envelope work; a nonzero envelope must not promote it.
            var texture = Opaque8x8Except(3, 3);
            var triangle = TriangleAlphaInput.WithUv0(
                new Vector3(1f, 2f, 3f),
                new Vector3(1f, 2f, 3f),
                new Vector3(1f, 2f, 3f),
                new Vector2(0f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 0.5f));
            var sampling = new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp);

            Assert.That(
                TriangleAlphaClassifier.Classify(triangle, texture, sampling, HalfUvTexel8x8),
                Is.EqualTo(TriangleAlphaOutcome.Unknown));
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
            return TriangleAlphaClassifier.Classify(triangle, texture, new AlphaSamplingSettings(AlphaFilterMode.Bilinear, AlphaWrapMode.Repeat), AlphaUvEnvelope.Zero);
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
            return TriangleAlphaClassifier.Classify(triangle, texture, new AlphaSamplingSettings(AlphaFilterMode.Bilinear, AlphaWrapMode.Clamp), AlphaUvEnvelope.Zero);
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
            return TriangleAlphaClassifier.Classify(triangle, texture, new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Repeat), AlphaUvEnvelope.Zero);
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
            return TriangleAlphaClassifier.Classify(triangle, texture, new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp), AlphaUvEnvelope.Zero);
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
                results[triangleIndex] = TriangleAlphaClassifier.Classify(triangle, texture, sampling, AlphaUvEnvelope.Zero);
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

        private static AlphaTextureData Opaque8x8Except(int x, int y)
        {
            var alpha = new byte[64];
            Array.Fill(alpha, byte.MaxValue);
            alpha[y * 8 + x] = 0;
            return new AlphaTextureData(8, 8, alpha);
        }
    }
}
