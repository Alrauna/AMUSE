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
        public void MaskedAverageAboveTheBoundIsOpaque()
        {
            // One 4x4 block holds two 255s and two 200s with n = 6:
            // nothing is noise, masked average 227.5 stays below O = 255,
            // so the block is witness.
            var bytes = Uniform(16, 255);
            bytes[0] = 200;
            bytes[1] = 200;
            var bounds = AlphaPolicyBounds.From(100, 2);
            var levels = SourceImageMaskedChain.Build(
                bytes, 4, 4, 1, bounds);
            Assert.That(levels[0].GetAlpha(0, 0), Is.EqualTo(0));
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
