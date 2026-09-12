using NUnit.Framework;
using Alrauna.Amuse.Editor.Analysis;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    public sealed class AlphaPolicyBoundsTests
    {
        [TestCase(100, 255)]
        [TestCase(99, 253)]
        [TestCase(0, 0)]
        [TestCase(1, 3)]
        [TestCase(50, 128)]
        public void OpaqueBoundIsTheCeilingByte(int percent, int expected)
        {
            Assert.That(
                AlphaPolicyBounds.From(percent, 0).OpaqueBound,
                Is.EqualTo((byte)expected));
        }

        [TestCase(0, 0)]
        [TestCase(1, 3)]
        [TestCase(2, 6)]
        [TestCase(100, 255)]
        public void NoiseBoundIsTheCeilingByte(int percent, int expected)
        {
            Assert.That(
                AlphaPolicyBounds.From(100, percent).NoiseBound,
                Is.EqualTo((byte)expected));
        }

        [Test]
        public void InertMatchesTheBaseContract()
        {
            var bounds = AlphaPolicyBounds.Inert;
            Assert.That(bounds.OpaqueBound, Is.EqualTo(byte.MaxValue));
            Assert.That(bounds.NoiseBound, Is.EqualTo((byte)0));
        }

        [TestCase(100, 0, 0)]
        [TestCase(50, 50, 49)]
        [TestCase(50, 80, 49)]
        [TestCase(0, 0, 0)]
        [TestCase(0, 40, 0)]
        [TestCase(1, 100, 0)]
        public void ClampKeepsNoiseStrictlyBelowOpaque(
            int opaquePercent,
            int noisePercent,
            int expected)
        {
            Assert.That(
                AlphaPolicyBounds.ClampNoise(opaquePercent, noisePercent),
                Is.EqualTo(expected));
        }

        [Test]
        public void EveryPercentMapsToAByteBelowTheNextStep()
        {
            for (var percent = 0; percent <= 100; percent++)
            {
                var bound = (int)AlphaPolicyBounds.From(percent, 0).OpaqueBound;
                Assert.That(bound * 100, Is.GreaterThanOrEqualTo(percent * 255),
                    $"percent {percent}");
                Assert.That((bound - 1) * 100, Is.LessThan(percent * 255),
                    $"percent {percent}");
            }
        }

        // From takes percents. The pair (99, 2) maps to the byte
        // bounds O = 253 and n = 6.
        [TestCase(100, 0, 254, 0)]
        [TestCase(100, 0, 255, 255)]
        [TestCase(99, 2, 252, 0)]
        [TestCase(99, 2, 3, 1)]
        [TestCase(99, 2, 253, 255)]
        public void PublishedFlagRuleResolvesTheThreeBands(
            int opaquePercent,
            int noisePercent,
            int sample,
            int expected)
        {
            var bounds = AlphaPolicyBounds.From(opaquePercent, noisePercent);
            var resolved = expected == 255
                ? byte.MaxValue
                : (byte)expected;
            Assert.That(
                SourceImageMaskedChainTestsHelpers.PublishedFlag(
                    (byte)sample, bounds),
                Is.EqualTo(resolved));
        }
    }

    internal static class SourceImageMaskedChainTestsHelpers
    {
        // Mirrors the inline rule both published-chain routes use. Kept
        // next to the tests so the table above pins the exact rule the
        // two routes must implement.
        internal static byte PublishedFlag(byte sample, AlphaPolicyBounds bounds)
        {
            if (sample >= bounds.OpaqueBound)
            {
                return byte.MaxValue;
            }
            return bounds.NoiseBound > 0 && sample < bounds.NoiseBound
                ? AlphaTextureData.ErasedFlag
                : (byte)0;
        }
    }
}
