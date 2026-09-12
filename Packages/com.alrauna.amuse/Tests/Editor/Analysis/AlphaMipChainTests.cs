using System;
using Alrauna.Amuse.Editor.Analysis;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    /// <summary>
    /// The chain guarantees shape and nothing else. An empty chain is the single
    /// most dangerous value it could admit: it would make "every mip is opaque"
    /// vacuously true and turn the conjunction into an unconditional ProvenOpaque.
    /// </summary>
    public sealed class AlphaMipChainTests
    {
        private static AlphaTextureData Level(int width, int height, byte value)
        {
            var bytes = new byte[width * height];
            for (var index = 0; index < bytes.Length; index++)
            {
                bytes[index] = value;
            }

            return new AlphaTextureData(width, height, bytes);
        }

        [Test]
        public void NullListThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new AlphaMipChain(null));
        }

        [Test]
        public void EmptyListThrows()
        {
            Assert.Throws<ArgumentException>(
                () => new AlphaMipChain(Array.Empty<AlphaTextureData>()));
        }

        /// <summary>
        /// The index must appear in the message: a chain of a dozen levels gives a
        /// bare ArgumentNullException nothing to say about which one is missing.
        /// </summary>
        [Test]
        public void NullElementThrowsAndIdentifiesTheOffendingIndex()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new AlphaMipChain(
                    new[] { Level(4, 4, 255), Level(2, 2, 255), null }));

            Assert.That(exception.Message, Does.Contain("2"),
                "The message must name the index of the null level.");
        }

        [Test]
        public void SingleLevelChainIsAccepted()
        {
            var chain = new AlphaMipChain(new[] { Level(4, 4, 255) });

            Assert.That(chain.Count, Is.EqualTo(1));
            Assert.That(chain[0].Width, Is.EqualTo(4));
        }

        [Test]
        public void SquareChainHalvingToOneIsAccepted()
        {
            var chain = new AlphaMipChain(new[]
            {
                Level(8, 8, 255), Level(4, 4, 255),
                Level(2, 2, 255), Level(1, 1, 255)
            });

            Assert.That(chain.Count, Is.EqualTo(4));
            Assert.That(chain[3].Width, Is.EqualTo(1));
            Assert.That(chain[3].Height, Is.EqualTo(1));
        }

        /// <summary>
        /// Each axis halves independently and clamps at one. A single shared shift
        /// would reject this legitimate non-square chain.
        /// </summary>
        [Test]
        public void NonSquareChainClampingOneAxisIsAccepted()
        {
            var chain = new AlphaMipChain(new[]
            {
                Level(16, 4, 255), Level(8, 2, 255),
                Level(4, 1, 255), Level(2, 1, 255), Level(1, 1, 255)
            });

            Assert.That(chain.Count, Is.EqualTo(5));
            Assert.That(chain[2].Width, Is.EqualTo(4));
            Assert.That(chain[2].Height, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedDimensionsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new AlphaMipChain(new[]
            {
                Level(8, 8, 255), Level(4, 4, 255), Level(4, 4, 255)
            }));
        }

        [Test]
        public void SkippedLevelIsRejected()
        {
            Assert.Throws<ArgumentException>(
                () => new AlphaMipChain(new[] { Level(8, 8, 255), Level(2, 2, 255) }));
        }

        [Test]
        public void ReversedOrderIsRejected()
        {
            Assert.Throws<ArgumentException>(
                () => new AlphaMipChain(new[] { Level(4, 4, 255), Level(8, 8, 255) }));
        }

        /// <summary>
        /// Deliberately valid. The type cannot see mipmapCount and so cannot prove
        /// completeness; a correctly shaped prefix is in-domain, and completeness is
        /// the provider contract's obligation.
        /// </summary>
        [Test]
        public void CorrectlyShapedPrefixIsAccepted()
        {
            var chain = new AlphaMipChain(new[] { Level(8, 8, 255), Level(4, 4, 255) });

            Assert.That(chain.Count, Is.EqualTo(2));
        }

        [Test]
        public void MutatingTheSuppliedListDoesNotChangeTheChain()
        {
            var levels = new[] { Level(2, 2, 255), Level(1, 1, 255) };
            var chain = new AlphaMipChain(levels);

            levels[0] = Level(2, 2, 0);

            Assert.That(chain[0].IsFullyOpaque, Is.True);
        }

        [Test]
        public void LimitedToPrefixKeepsLevelsThroughTheCap()
        {
            var chain = new AlphaMipChain(new[]
            {
                Level(8, 8, 1), Level(4, 4, 2), Level(2, 2, 3),
                Level(1, 1, 4),
            });

            var capped = chain.LimitedTo(2);

            Assert.That(capped.Count, Is.EqualTo(3));
            Assert.That(capped[2].GetAlpha(0, 0), Is.EqualTo((byte)3));
        }

        [Test]
        public void LimitedToBeyondTheLastLevelReturnsTheSameInstance()
        {
            var chain = new AlphaMipChain(new[]
            {
                Level(2, 2, 1), Level(1, 1, 2),
            });

            Assert.That(chain.LimitedTo(1), Is.SameAs(chain));
            Assert.That(chain.LimitedTo(9), Is.SameAs(chain));
        }

        [Test]
        public void LimitedToZeroKeepsOnlyMipZero()
        {
            var chain = new AlphaMipChain(new[]
            {
                Level(4, 4, 7), Level(2, 2, 8), Level(1, 1, 9),
            });

            var capped = chain.LimitedTo(0);

            Assert.That(capped.Count, Is.EqualTo(1));
            Assert.That(capped[0].GetAlpha(0, 0), Is.EqualTo((byte)7));
        }

        [Test]
        public void LimitedToNegativeThrows()
        {
            var chain = new AlphaMipChain(
                new[] { Level(2, 2, 1), Level(1, 1, 2) });

            Assert.Throws<ArgumentOutOfRangeException>(
                () => chain.LimitedTo(-1));
        }

        [Test]
        public void MaximumLevelAtOrAboveReturnsTheLastQualifyingLevel()
        {
            var chain = new AlphaMipChain(new[]
            {
                Level(32, 32, 1), Level(16, 16, 2), Level(8, 8, 3),
                Level(4, 4, 4), Level(2, 2, 5), Level(1, 1, 6),
            });

            Assert.That(chain.MaximumLevelAtOrAbove(8), Is.EqualTo(2));
            Assert.That(chain.MaximumLevelAtOrAbove(9), Is.EqualTo(1));
            Assert.That(chain.MaximumLevelAtOrAbove(32), Is.EqualTo(0));
            Assert.That(chain.MaximumLevelAtOrAbove(1), Is.EqualTo(5));
        }

        [Test]
        public void MaximumLevelAtOrAboveReturnsMinusOneWhenMipZeroIsBelowTheMinimum()
        {
            var chain = new AlphaMipChain(
                new[] { Level(8, 8, 1), Level(4, 4, 2) });

            Assert.That(chain.MaximumLevelAtOrAbove(16), Is.EqualTo(-1));
        }

        [Test]
        public void MaximumLevelAtOrAboveLetsTheSmallerDimensionGovern()
        {
            var chain = new AlphaMipChain(
                new[] { Level(32, 8, 1), Level(16, 4, 2) });

            Assert.That(chain.MaximumLevelAtOrAbove(8), Is.EqualTo(0));
            Assert.That(chain.MaximumLevelAtOrAbove(9), Is.EqualTo(-1));
        }

        [Test]
        public void MaximumLevelAtOrAboveLetsTheWidthGovernOnTallChains()
        {
            var chain = new AlphaMipChain(
                new[] { Level(8, 32, 1), Level(4, 16, 2) });

            Assert.That(chain.MaximumLevelAtOrAbove(8), Is.EqualTo(0));
            Assert.That(chain.MaximumLevelAtOrAbove(9), Is.EqualTo(-1));
        }

        [Test]
        public void MaximumLevelAtOrAboveRejectsNonPositiveSizes()
        {
            var chain = new AlphaMipChain(new[] { Level(2, 2, 1) });

            Assert.Throws<ArgumentOutOfRangeException>(
                () => chain.MaximumLevelAtOrAbove(0));
        }

        // --- Per-level without-evidence provenance -----------------------------

        [Test]
        public void ProvenanceDefaultsToEvidenceAtEveryLevel()
        {
            var chain = new AlphaMipChain(
                new[] { Level(2, 2, 1), Level(1, 1, 2) });

            for (var level = 0; level < chain.Count; level++)
            {
                Assert.That(
                    chain.IsLevelWithoutEvidence(level),
                    Is.False,
                    "level " + level);
            }
        }

        [Test]
        public void FlaggedLevelsReportProvenancePerLevel()
        {
            var chain = new AlphaMipChain(
                new[] { Level(2, 2, 1), Level(1, 1, 2) },
                new[] { true, false });

            Assert.That(chain.IsLevelWithoutEvidence(0), Is.True);
            Assert.That(chain.IsLevelWithoutEvidence(1), Is.False);
        }

        [Test]
        public void ProvenanceOfTheWrongLengthThrows()
        {
            Assert.Throws<ArgumentException>(
                () => new AlphaMipChain(
                    new[] { Level(2, 2, 1) },
                    new[] { true, false }));
        }

        [Test]
        public void NullProvenanceThrows()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AlphaMipChain(
                    new[] { Level(2, 2, 1) },
                    null));
        }

        [Test]
        public void ProvenanceIndexOutOfRangeThrows()
        {
            var chain = new AlphaMipChain(
                new[] { Level(2, 2, 1), Level(1, 1, 2) },
                new[] { false, true });

            Assert.Throws<ArgumentOutOfRangeException>(
                () => chain.IsLevelWithoutEvidence(-1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => chain.IsLevelWithoutEvidence(2));
        }

        [Test]
        public void MutatingTheSuppliedProvenanceDoesNotChangeTheChain()
        {
            var provenance = new[] { false, false };
            var chain = new AlphaMipChain(
                new[] { Level(2, 2, 1), Level(1, 1, 2) },
                provenance);

            provenance[0] = true;

            Assert.That(chain.IsLevelWithoutEvidence(0), Is.False);
        }

        /// <summary>
        /// The mip cap is the user's policy scope; the residency degradation
        /// is the capture's fact. A prefix must carry the provenance of the
        /// levels it keeps, so a flagged level inside the cap still degrades
        /// the proof after the cap.
        /// </summary>
        [Test]
        public void LimitedToKeepsTheProvenanceOfThePrefix()
        {
            var chain = new AlphaMipChain(
                new[] { Level(4, 4, 1), Level(2, 2, 2), Level(1, 1, 3) },
                new[] { true, false, false });

            var capped = chain.LimitedTo(1);

            Assert.That(capped.Count, Is.EqualTo(2));
            Assert.That(capped.IsLevelWithoutEvidence(0), Is.True);
            Assert.That(capped.IsLevelWithoutEvidence(1), Is.False);
        }

        [Test]
        public void LimitedToBeyondTheLastLevelKeepsTheProvenanceOfTheSameInstance()
        {
            var chain = new AlphaMipChain(
                new[] { Level(2, 2, 1), Level(1, 1, 2) },
                new[] { true, false });

            Assert.That(chain.LimitedTo(1), Is.SameAs(chain));
            Assert.That(chain.LimitedTo(9), Is.SameAs(chain));
            Assert.That(chain.IsLevelWithoutEvidence(0), Is.True);
        }

        /// <summary>
        /// The minimum-texture-size scope is a geometric rule over the
        /// declared level dimensions. Provenance says nothing about geometry,
        /// so a flagged level keeps its declared dimensions in this
        /// computation: the fold degrades the level, the scope does not.
        /// </summary>
        [Test]
        public void MaximumLevelAtOrAboveIsProvenanceBlind()
        {
            var chain = new AlphaMipChain(
                new[] { Level(4, 4, 0), Level(2, 2, 0) },
                new[] { true, false });

            Assert.That(chain.MaximumLevelAtOrAbove(4), Is.EqualTo(0));
            Assert.That(chain.MaximumLevelAtOrAbove(2), Is.EqualTo(1));
        }
    }
}
