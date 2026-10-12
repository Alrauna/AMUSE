using NUnit.Framework;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;

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
        [TestCase(252, 0)]                      // witness band
        [TestCase(253, 255)]                    // opaque band
        public void PublishedChainRoute_ResolvesWitnessAndOpaqueBands(
            int sampleByte, byte expectedFlag)
        {
            Assert.That(
                RunPublishedChainOnOneByte(99, 2, sampleByte),
                Is.EqualTo(expectedFlag));
        }

        [Test]
        public void PublishedChainRoute_ErasesBelowTheNoiseBound()
        {
            Assert.That(
                RunPublishedChainOnOneByte(99, 2, 3),
                Is.EqualTo(AlphaTextureData.ErasedFlag),
                "sample 3 sits below the noise bound 6, so the byte " +
                "erases");
        }

        // The smallest real route that consumes one byte under bounds:
        // a 1x1 source with a single mip level, the route
        // SourceImageMaskedChainTests.OddWidthPartitionPlacesEachTexelInItsBlock
        // drives, so the verdict comes from the published chain and not
        // from a mirrored rule.
        private static byte RunPublishedChainOnOneByte(
            int opaquePercent, int noisePercent, int sampleByte)
        {
            var bounds = AlphaPolicyBounds.From(opaquePercent, noisePercent);
            var levels = SourceImageMaskedChain.Build(
                new byte[] { (byte)sampleByte }, 1, 1, 1, bounds);
            return levels[0].GetAlpha(0, 0);
        }
    }
}
