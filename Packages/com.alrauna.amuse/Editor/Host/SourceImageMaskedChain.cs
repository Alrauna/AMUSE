using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// Builds the source-image route's alpha chain under an alpha policy.
    /// The builder masks before averaging: a noise texel takes no part in
    /// its block's average, because a stray the user called invisible
    /// must not drag a coarser level below the opaque bound. A block
    /// whose every source texel is noise carries the erased flag. All
    /// comparisons are exact integers; no float crosses a verdict.
    /// </summary>
    internal static class SourceImageMaskedChain
    {
        /// <summary>
        /// Reconstructs the decoded byte from an RGBA32 sample. The
        /// decode stored byte divided by 255 as the nearest float, so
        /// the rounded product returns every byte exactly; the test
        /// pins all 256 values.
        /// </summary>
        internal static byte DecodeByte(float sample)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(sample) * 255f);
        }

        internal static AlphaTextureData[] Build(
            IReadOnlyList<byte> mipZeroBytes,
            int width,
            int height,
            int mipCount,
            AlphaPolicyBounds bounds)
        {
            var levels = new AlphaTextureData[mipCount];
            var levelWidth = width;
            var levelHeight = height;
            for (var mip = 0; mip < mipCount; mip++)
            {
                var flags = new byte[levelWidth * levelHeight];
                for (var by = 0; by < levelHeight; by++)
                {
                    // Exact half-open source partition: block (bx, by)
                    // covers x in [bx * width / levelWidth,
                    // (bx + 1) * width / levelWidth). Integer bounds
                    // never drop a source texel and never double-count
                    // one, so odd sizes partition cleanly.
                    var sourceTop = by * height / levelHeight;
                    var sourceBottom = (by + 1) * height / levelHeight;
                    for (var bx = 0; bx < levelWidth; bx++)
                    {
                        var sourceLeft = bx * width / levelWidth;
                        var sourceRight = (bx + 1) * width / levelWidth;
                        flags[by * levelWidth + bx] = BuildBlock(
                            mipZeroBytes,
                            width,
                            sourceLeft,
                            sourceRight,
                            sourceTop,
                            sourceBottom,
                            bounds);
                    }
                }
                levels[mip] = new AlphaTextureData(levelWidth, levelHeight, flags);
                levelWidth = Mathf.Max(1, levelWidth >> 1);
                levelHeight = Mathf.Max(1, levelHeight >> 1);
            }
            return levels;
        }

        private static byte BuildBlock(
            IReadOnlyList<byte> source,
            int sourceWidth,
            int startX,
            int endX,
            int startY,
            int endY,
            AlphaPolicyBounds bounds)
        {
            long sum = 0;
            long consulted = 0;
            for (var y = startY; y < endY; y++)
            {
                for (var x = startX; x < endX; x++)
                {
                    var b = source[y * sourceWidth + x];
                    if (bounds.NoiseBound > 0 && b < bounds.NoiseBound)
                    {
                        continue;
                    }
                    sum += b;
                    consulted++;
                }
            }
            if (consulted == 0)
            {
                return AlphaTextureData.ErasedFlag;
            }
            // Exact integer verdict: the masked average meets the
            // opaque bound exactly when sum >= bound * count.
            return sum >= (long)bounds.OpaqueBound * consulted
                ? byte.MaxValue
                : (byte)0;
        }
    }
}
