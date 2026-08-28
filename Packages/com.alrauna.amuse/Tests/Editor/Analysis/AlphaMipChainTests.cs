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
    }
}
