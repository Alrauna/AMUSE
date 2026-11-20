using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// The acquired evidence of one atlas source texture: the raw
    /// bytes of its protected mip levels and the decoded level 0
    /// texels. The levels come from the readable clone of the source
    /// import, so they hold the effective imported texels of every
    /// protected level.
    /// <para>
    /// The image holds numbers and managed arrays only. It never holds
    /// a live texture, so a later pass can compose pages from it after
    /// the clone went away.
    /// </para>
    /// </summary>
    internal sealed class AtlasSourceImage
    {
        internal int Width { get; }

        internal int Height { get; }

        /// <summary>
        /// The graphics format of the source, with its sRGB or linear
        /// form. The page format copies it.
        /// </summary>
        internal GraphicsFormat Format { get; }

        /// <summary>The mip count of the source import.</summary>
        internal int MipCount { get; }

        /// <summary>
        /// The raw bytes of each protected level. The index runs from
        /// level 0 to the protected level count minus one. Compressed
        /// formats hold whole blocks, so a block copy at an aligned
        /// origin is exact.
        /// </summary>
        internal IReadOnlyList<byte[]> RawLevels { get; }

        /// <summary>
        /// The decoded level 0 texels of the clone, bottom-to-top rows.
        /// The page compose reads them.
        /// </summary>
        internal Color32[] Level0 { get; }

        /// <summary>
        /// The managed bytes this image holds against the page ledger:
        /// the raw protected levels plus the level 0 Color32 array.
        /// The builder adds the number when the page build starts.
        /// </summary>
        internal long HeldBytes { get; }

        internal AtlasSourceImage(
            int width,
            int height,
            GraphicsFormat format,
            int mipCount,
            byte[][] rawLevels,
            Color32[] level0)
        {
            if (width < 1 || height < 1)
            {
                throw new InvalidOperationException(
                    "An atlas source image must hold a positive size.");
            }

            if (rawLevels == null || rawLevels.Length < 1)
            {
                throw new InvalidOperationException(
                    "An atlas source image must hold at least one raw" +
                    " level.");
            }

            if (level0 == null || level0.Length != width * height)
            {
                throw new InvalidOperationException(
                    "An atlas source image must hold one level 0 texel" +
                    " per source texel.");
            }

            Width = width;
            Height = height;
            Format = format;
            MipCount = mipCount;
            RawLevels = rawLevels;
            Level0 = level0;

            long held = 4L * width * height;
            foreach (byte[] level in rawLevels)
            {
                held += level.Length;
            }

            HeldBytes = held;
        }

        /// <summary>The width of the source at one mip level.</summary>
        internal int LevelWidth(int level)
        {
            return Math.Max(1, Width >> level);
        }

        /// <summary>The height of the source at one mip level.</summary>
        internal int LevelHeight(int level)
        {
            return Math.Max(1, Height >> level);
        }

        /// <summary>
        /// The raw byte length of one level of this size in this
        /// format. Compressed sizes round up to whole blocks.
        /// </summary>
        internal static long LevelBytes(
            int width,
            int height,
            GraphicsFormat format)
        {
            checked
            {
                return GraphicsFormatUtility.ComputeMipChainSize(
                    width, height, format, 1);
            }
        }
    }
}
