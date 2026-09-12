using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    public sealed class SourceImageMaskedChainTests
    {
        private static byte[] Uniform(int count, byte value)
        {
            var bytes = new byte[count];
            for (var i = 0; i < count; i++)
            {
                bytes[i] = value;
            }
            return bytes;
        }

        [Test]
        public void DecodeByteRoundTripsEveryByte()
        {
            for (var k = 0; k <= 255; k++)
            {
                var sample = k / 255f;
                Assert.That(
                    SourceImageMaskedChain.DecodeByte(sample),
                    Is.EqualTo(k),
                    $"byte {k}");
            }
        }

        [Test]
        public void InertBoundsReproduceThresholdAt255()
        {
            var bytes = new byte[] { 255, 254, 0, 128 };
            var levels = SourceImageMaskedChain.Build(
                bytes, 2, 2, 1, AlphaPolicyBounds.Inert);
            Assert.That(levels[0].GetAlpha(0, 0), Is.EqualTo(255));
            Assert.That(levels[0].GetAlpha(1, 0), Is.EqualTo(0));
            Assert.That(levels[0].GetAlpha(0, 1), Is.EqualTo(0));
            Assert.That(levels[0].GetAlpha(1, 1), Is.EqualTo(0));
        }

        [Test]
        public void SingleStraySubstitutesAtEveryLevel()
        {
            // 8x8, all 255 except one texel at 3. n = 6 erases it, so
            // every masked level averages 255 and no level witnesses.
            // Level 0 stores the erased flag for the stray, because its
            // block is that one texel; IsFullyOpaque starts at level 1,
            // where the flag no longer appears.
            var bytes = Uniform(64, 255);
            bytes[0] = 3;
            var bounds = AlphaPolicyBounds.From(100, 2);
            var levels = SourceImageMaskedChain.Build(
                bytes, 8, 8, 4, bounds);
            Assert.That(levels.Length, Is.EqualTo(4));
            Assert.That(levels[0].GetAlpha(0, 0),
                Is.EqualTo(AlphaTextureData.ErasedFlag));
            Assert.That(levels[0].GetAlpha(1, 0), Is.EqualTo(255));
            for (var level = 1; level < levels.Length; level++)
            {
                Assert.That(levels[level].IsFullyOpaque, Is.True,
                    $"level {level}");
            }
        }

        [Test]
        public void DenseNoiseKeepsWitnessing()
        {
            // Half the texels at 0 with n = 6: blocks stay mixed or all
            // noise, erased blocks carry the flag, and the level is not
            // fully opaque.
            var bytes = Uniform(64, 255);
            for (var i = 0; i < 64; i += 2)
            {
                bytes[i] = 0;
            }
            var bounds = AlphaPolicyBounds.From(100, 2);
            var levels = SourceImageMaskedChain.Build(
                bytes, 8, 8, 2, bounds);
            Assert.That(levels[0].IsFullyOpaque, Is.False);
            Assert.That(levels[0].GetAlpha(0, 0), Is.EqualTo(
                AlphaTextureData.ErasedFlag));
            Assert.That(levels[0].GetAlpha(1, 0), Is.EqualTo(255));
        }

        [Test]
        public void AllNoiseBlockCarriesTheErasedFlag()
        {
            var bytes = Uniform(4, 2);
            var bounds = AlphaPolicyBounds.From(100, 2);
            var levels = SourceImageMaskedChain.Build(
                bytes, 2, 2, 1, bounds);
            Assert.That(levels[0].GetAlpha(0, 0),
                Is.EqualTo(AlphaTextureData.ErasedFlag));
        }

        [Test]
        public void MixedBlockBelowTheOpaqueBoundStaysWitness()
        {
            // One 4x4 block holds fourteen 255s and two 200s with n = 6:
            // nothing is noise, and the masked sum 3970 stays below
            // 255 * 16 = 4080, so the block is witness.
            var bytes = Uniform(16, 255);
            bytes[0] = 200;
            bytes[1] = 200;
            var bounds = AlphaPolicyBounds.From(100, 2);
            var levels = SourceImageMaskedChain.Build(
                bytes, 4, 4, 1, bounds);
            Assert.That(levels[0].GetAlpha(0, 0), Is.EqualTo(0));
        }

        [Test]
        public void OddWidthPartitionPlacesEachTexelInItsBlock()
        {
            // A 5x1 source partitions into levels 5, 2, and 1 wide. The
            // middle level's blocks are uneven: [0, 2) and [2, 5). With
            // O = 230 the second block's sum is 689, exactly one short
            // of 230 * 3, so it stays witness only while all three of
            // its texels are consulted. A naive even split drops texel 4
            // and would turn the block opaque at 230 * 2, so the test
            // catches a dropped texel on an odd width. The 1x1 level
            // sums to 1199, at or above 230 * 5 = 1150, so it is opaque;
            // any dropped texel would fall below that bound.
            var bytes = new byte[] { 255, 255, 230, 230, 229 };
            var bounds = AlphaPolicyBounds.From(90, 2);
            var levels = SourceImageMaskedChain.Build(
                bytes, 5, 1, 3, bounds);
            Assert.That(levels.Length, Is.EqualTo(3));
            Assert.That(levels[1].Width, Is.EqualTo(2));
            // Level 0 keeps the per-texel verdicts: 230 meets O = 230
            // and 229 does not.
            Assert.That(levels[0].GetAlpha(3, 0), Is.EqualTo(255));
            Assert.That(levels[0].GetAlpha(4, 0), Is.EqualTo(0));
            // The uneven block [2, 5) is witness by one unit; the even
            // block [0, 2) is opaque.
            Assert.That(levels[1].GetAlpha(0, 0), Is.EqualTo(255));
            Assert.That(levels[1].GetAlpha(1, 0), Is.EqualTo(0));
            // The 1x1 level sums to 1199, at or above 230 * 5 = 1150.
            Assert.That(levels[2].GetAlpha(0, 0), Is.EqualTo(255));
        }

        [Test]
        public void WitnessBandByteStaysWitness()
        {
            // O = 253 (99 percent): a texel at 254 is opaque evidence,
            // a texel at 200 is witness, a texel at 3 is noise.
            var bytes = new byte[] { 254, 200, 3, 255 };
            var bounds = AlphaPolicyBounds.From(99, 2);
            var levels = SourceImageMaskedChain.Build(
                bytes, 2, 2, 1, bounds);
            Assert.That(levels[0].GetAlpha(0, 0), Is.EqualTo(255));
            Assert.That(levels[0].GetAlpha(1, 0), Is.EqualTo(0));
            Assert.That(levels[0].GetAlpha(0, 1),
                Is.EqualTo(AlphaTextureData.ErasedFlag));
            Assert.That(levels[0].GetAlpha(1, 1), Is.EqualTo(255));
        }
    }
}
