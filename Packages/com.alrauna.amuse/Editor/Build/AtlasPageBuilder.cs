using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// Builds one atlas page from its plan. The build composes level 0
    /// in an RGBA32 texture, lets Unity generate the mip chain,
    /// encodes the page in the plan format, and then overwrites every
    /// protected level with raw blocks copied from the source chains.
    /// A block copy at an aligned origin is exact, so the protected
    /// levels keep the source texels byte for byte.
    /// <para>
    /// The ledger receives every allocation the build holds. The
    /// source images and the page texture stay on the ledger when the
    /// build returns, because the caller holds them. The compose
    /// texture and the level 0 buffer leave the ledger when the build
    /// frees them.
    /// </para>
    /// </summary>
    internal static class AtlasPageBuilder
    {
        internal static Texture2D Build(
            AtlasPagePlanEntry page,
            AtlasSamplerFacts sampler,
            IReadOnlyDictionary<int, AtlasSourceImage> sources,
            int protectedMipLevelCount,
            AtlasByteLedger ledger)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (protectedMipLevelCount < 1 ||
                protectedMipLevelCount > page.MipCount)
            {
                throw new InvalidOperationException(
                    "The atlas page build protects " +
                    protectedMipLevelCount + " levels of a page with " +
                    page.MipCount + " levels. The count must sit " +
                    "between one and the page chain length.");
            }

            // Every distinct source of the page stays held for the
            // whole build and the check that follows it.
            var seenSources = new HashSet<int>();
            foreach (AtlasPlacement placement in page.Placements)
            {
                if (seenSources.Add(placement.SourceTextureInstanceId))
                {
                    ledger.Add(
                        SourceOf(sources, placement)
                            .HeldBytes);
                }
            }

            var srgb = GraphicsFormatUtility.IsSRGBFormat(page.Format);
            var composeFormat = srgb
                ? GraphicsFormat.R8G8B8A8_SRGB
                : GraphicsFormat.R8G8B8A8_UNorm;
            var composeBytes = AtlasBudget.TextureBytes(
                page.Width, page.Height, composeFormat, page.MipCount);
            var pageBytes = AtlasBudget.TextureBytes(
                page.Width, page.Height, page.Format, page.MipCount);
            var bufferBytes = 4L * page.Width * page.Height;

            // The compose texture starts as RGBA32, so a linear source
            // stays linear and an sRGB source stays sRGB.
            var pageTexture = new Texture2D(
                page.Width,
                page.Height,
                TextureFormat.RGBA32,
                page.MipCount,
                !srgb);
            ledger.Add(composeBytes);
            var buffer = new Color32[page.Width * page.Height];
            ledger.Add(bufferBytes);
            try
            {
                ComposeLevel0(page, sources, buffer);
                pageTexture.SetPixels32(buffer, 0);
                pageTexture.Apply(true, false);

                // The in place encode keeps the mip chain and the color
                // space flag of the texture.
                EditorUtility.CompressTexture(
                    pageTexture,
                    GraphicsFormatUtility.GetTextureFormat(page.Format),
                    TextureCompressionQuality.Normal);
                if (pageTexture.graphicsFormat != page.Format)
                {
                    throw new InvalidOperationException(
                        "The encoded page landed on format " +
                        pageTexture.graphicsFormat + " while the plan " +
                        "names " + page.Format + ". The encoder and the " +
                        "plan disagree.");
                }

                // The encode replaced the RGBA32 chain with the page
                // format, so the ledger follows the change.
                ledger.Release(composeBytes);
                ledger.Add(pageBytes);

                OverwriteProtectedLevels(
                    pageTexture, page, sources, protectedMipLevelCount);
                pageTexture.Apply(false, false);

                pageTexture.filterMode = sampler.Filter;
                pageTexture.wrapModeU = sampler.WrapU;
                pageTexture.wrapModeV = sampler.WrapV;
                pageTexture.anisoLevel = sampler.AnisoLevel;
                pageTexture.mipMapBias = sampler.MipBias;

                // A generated texture starts with streaming off. The
                // serialized flag is the only way on for a texture
                // without an importer.
                using (var serialized = new SerializedObject(pageTexture))
                {
                    serialized.FindProperty("m_StreamingMipmaps")
                        .boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                return pageTexture;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(pageTexture);
                throw;
            }
            finally
            {
                ledger.Release(bufferBytes);
            }
        }

        /// <summary>
        /// Maps one coordinate onto the source under a wrap mode.
        /// Repeat uses the positive modulo of the source size. Clamp
        /// pins the coordinate to the edge texel.
        /// </summary>
        internal static int MapCoordinate(
            int value,
            int size,
            UnityEngine.TextureWrapMode wrap)
        {
            switch (wrap)
            {
                case UnityEngine.TextureWrapMode.Repeat:
                    return PositiveMod(value, size);
                case UnityEngine.TextureWrapMode.Clamp:
                    return Math.Min(Math.Max(value, 0), size - 1);
                default:
                    throw new InvalidOperationException(
                        "The atlas route admits only Repeat and Clamp " +
                        "wrap modes. The wrap mode " + wrap + " reached " +
                        "the page build.");
            }
        }

        /// <summary>The modulo that stays zero or positive.</summary>
        internal static int PositiveMod(int value, int modulus)
        {
            var result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static AtlasSourceImage SourceOf(
            IReadOnlyDictionary<int, AtlasSourceImage> sources,
            AtlasPlacement placement)
        {
            if (!sources.TryGetValue(
                    placement.SourceTextureInstanceId, out var source))
            {
                throw new InvalidOperationException(
                    "The page plan names source texture instance " +
                    placement.SourceTextureInstanceId + ", but the " +
                    "acquired sources hold no image for it.");
            }

            return source;
        }

        /// <summary>
        /// Fills the page level 0 buffer from the acquired level 0
        /// texels. The border of a placement reads through the wrap
        /// rule of its sampler. Everything outside a placement stays
        /// black.
        /// </summary>
        private static void ComposeLevel0(
            AtlasPagePlanEntry page,
            IReadOnlyDictionary<int, AtlasSourceImage> sources,
            Color32[] buffer)
        {
            Array.Clear(buffer, 0, buffer.Length);
            foreach (AtlasPlacement placement in page.Placements)
            {
                AtlasSourceImage source = SourceOf(sources, placement);
                for (var y = 0; y < placement.Height; y++)
                {
                    var sourceY = MapCoordinate(
                        placement.SourceY + y,
                        source.Height,
                        placement.Sampler.WrapV);
                    var rowStart =
                        (placement.PageY + y) * page.Width +
                        placement.PageX;
                    CopyWrappedRow(
                        source.Level0,
                        source.Width,
                        sourceY,
                        placement.Sampler.WrapU,
                        buffer,
                        rowStart,
                        placement.SourceX,
                        placement.Width);
                }
            }
        }

        /// <summary>
        /// Copies one source row into the page buffer under the wrap
        /// rule. Clamp fills the outside zones with the edge texels and
        /// copies the inside in one run. Repeat walks the source period.
        /// </summary>
        private static void CopyWrappedRow(
            Color32[] level0,
            int sourceWidth,
            int sourceY,
            UnityEngine.TextureWrapMode wrapU,
            Color32[] buffer,
            int destination,
            int sourceX,
            int count)
        {
            var rowOffset = sourceY * sourceWidth;
            if (wrapU == UnityEngine.TextureWrapMode.Clamp)
            {
                var insideDestination = Math.Max(0, -sourceX);
                var insideSource = Math.Max(0, sourceX);
                var insideCount = Math.Min(
                    sourceWidth - insideSource,
                    count - insideDestination);
                if (insideCount > 0)
                {
                    Array.Copy(
                        level0,
                        rowOffset + insideSource,
                        buffer,
                        destination + insideDestination,
                        insideCount);
                }

                var leftEdge = level0[rowOffset];
                for (var i = 0; i < insideDestination; i++)
                {
                    buffer[destination + i] = leftEdge;
                }

                var rightEdge = level0[rowOffset + sourceWidth - 1];
                for (var i = insideDestination + insideCount;
                    i < count;
                    i++)
                {
                    buffer[destination + i] = rightEdge;
                }

                return;
            }

            if (wrapU != UnityEngine.TextureWrapMode.Repeat)
            {
                throw new InvalidOperationException(
                    "The atlas route admits only Repeat and Clamp wrap " +
                    "modes. The wrap mode " + wrapU + " reached the " +
                    "page compose.");
            }

            var x = sourceX;
            var written = 0;
            while (written < count)
            {
                var start = PositiveMod(x, sourceWidth);
                var take = Math.Min(
                    count - written, sourceWidth - start);
                Array.Copy(
                    level0,
                    rowOffset + start,
                    buffer,
                    destination + written,
                    take);
                x += take;
                written += take;
            }
        }

        /// <summary>
        /// Overwrites each protected level with the source blocks. A
        /// block wholly inside the source copies the source block
        /// bytes. A Repeat block maps to the wrapped source block. A
        /// Clamp block outside the image keeps the edge block endpoints
        /// and repeats the edge row or column indices, which stays
        /// exact on BC1 and BC3.
        /// </summary>
        private static void OverwriteProtectedLevels(
            Texture2D pageTexture,
            AtlasPagePlanEntry page,
            IReadOnlyDictionary<int, AtlasSourceImage> sources,
            int protectedMipLevelCount)
        {
            for (var level = 0; level < protectedMipLevelCount; level++)
            {
                var levelWidth = Math.Max(1, page.Width >> level);
                var levelHeight = Math.Max(1, page.Height >> level);
                var data = pageTexture.GetPixelData<byte>(level).ToArray();
                foreach (AtlasPlacement placement in page.Placements)
                {
                    CopyPlacementLevel(
                        data,
                        levelWidth,
                        SourceOf(sources, placement),
                        placement,
                        level);
                }

                pageTexture.SetPixelData(data, level);
            }
        }

        private static void CopyPlacementLevel(
            byte[] pageData,
            int pageWidth,
            AtlasSourceImage source,
            AtlasPlacement placement,
            int level)
        {
            if (source.RawLevels.Count <= level)
            {
                throw new InvalidOperationException(
                    "The page build protects level " + level + " of " +
                    "source texture instance " +
                    placement.SourceTextureInstanceId + ", but its " +
                    "acquired image holds " + source.RawLevels.Count +
                    " levels.");
            }

            var sourceWidth = source.LevelWidth(level);
            var sourceHeight = source.LevelHeight(level);
            var sourceX = placement.SourceX >> level;
            var sourceY = placement.SourceY >> level;
            var pageX = placement.PageX >> level;
            var pageY = placement.PageY >> level;
            var regionWidth = placement.Width >> level;
            var regionHeight = placement.Height >> level;
            byte[] sourceLevel = source.RawLevels[level];

            if (GraphicsFormatUtility.IsCompressedFormat(source.Format))
            {
                CopyCompressedLevel(
                    pageData,
                    pageWidth,
                    sourceLevel,
                    source,
                    placement,
                    sourceWidth,
                    sourceHeight,
                    sourceX,
                    sourceY,
                    pageX,
                    pageY,
                    regionWidth,
                    regionHeight);
                return;
            }

            var format = GraphicsFormatUtility.GetTextureFormat(
                source.Format);
            var bytesPerTexel = format == TextureFormat.RGB24 ? 3 : 4;
            for (var y = 0; y < regionHeight; y++)
            {
                var insideY = MapCoordinate(
                    sourceY + y, sourceHeight, placement.Sampler.WrapV);
                for (var x = 0; x < regionWidth; x++)
                {
                    var insideX = MapCoordinate(
                        sourceX + x, sourceWidth, placement.Sampler.WrapU);
                    Buffer.BlockCopy(
                        sourceLevel,
                        (insideY * sourceWidth + insideX) * bytesPerTexel,
                        pageData,
                        ((pageY + y) * pageWidth + pageX + x) *
                            bytesPerTexel,
                        bytesPerTexel);
                }
            }
        }

        /// <summary>
        /// The column or row plan of one block axis. Keep reads the
        /// block as it is. Zero reads column or row zero of the edge
        /// block. Three reads column or row three.
        /// </summary>
        private enum BlockAxisMap
        {
            Keep = 0,
            Zero = 1,
            Three = 2,
        }

        private static int MapIndex(
            BlockAxisMap map,
            int index)
        {
            switch (map)
            {
                case BlockAxisMap.Zero:
                    return 0;
                case BlockAxisMap.Three:
                    return 3;
                default:
                    return index;
            }
        }

        /// <summary>
        /// Resolves one block axis of a placement block. The result
        /// names the source block and how the indices inside the block
        /// repeat the edge.
        /// </summary>
        private static void ResolveBlockAxis(
            int baseCoordinate,
            int sourceSize,
            UnityEngine.TextureWrapMode wrap,
            out int blockIndex,
            out BlockAxisMap map)
        {
            if (wrap == UnityEngine.TextureWrapMode.Repeat)
            {
                blockIndex = PositiveMod(baseCoordinate / 4, sourceSize / 4);
                map = BlockAxisMap.Keep;
                return;
            }

            if (wrap != UnityEngine.TextureWrapMode.Clamp)
            {
                throw new InvalidOperationException(
                    "The atlas route admits only Repeat and Clamp wrap " +
                    "modes. The wrap mode " + wrap + " reached the " +
                    "protected level copy.");
            }

            // The grid keeps every block origin and the source size on
            // whole blocks, so a block is either wholly inside or
            // wholly outside on this axis.
            if (baseCoordinate < 0)
            {
                blockIndex = 0;
                map = BlockAxisMap.Zero;
                return;
            }

            if (baseCoordinate < sourceSize)
            {
                blockIndex = baseCoordinate / 4;
                map = BlockAxisMap.Keep;
                return;
            }

            blockIndex = sourceSize / 4 - 1;
            map = BlockAxisMap.Three;
        }

        private static void CopyCompressedLevel(
            byte[] pageData,
            int pageWidth,
            byte[] sourceLevel,
            AtlasSourceImage source,
            AtlasPlacement placement,
            int sourceWidth,
            int sourceHeight,
            int sourceX,
            int sourceY,
            int pageX,
            int pageY,
            int regionWidth,
            int regionHeight)
        {
            var bytesPerBlock =
                (int)GraphicsFormatUtility.GetBlockSize(source.Format);
            var pageBlocksX = (pageWidth + 3) / 4;
            var textureFormat = GraphicsFormatUtility.GetTextureFormat(
                source.Format);
            var blockIsBc3 = textureFormat == TextureFormat.DXT5;
            var blockIsBc7 = textureFormat == TextureFormat.BC7;
            var firstPageBlockX = pageX / 4;
            var firstPageBlockY = pageY / 4;

            for (var blockY = 0; blockY < regionHeight / 4; blockY++)
            {
                for (var blockX = 0; blockX < regionWidth / 4; blockX++)
                {
                    ResolveBlockAxis(
                        sourceX + 4 * blockX,
                        sourceWidth,
                        placement.Sampler.WrapU,
                        out var sourceBlockX,
                        out var mapX);
                    ResolveBlockAxis(
                        sourceY + 4 * blockY,
                        sourceHeight,
                        placement.Sampler.WrapV,
                        out var sourceBlockY,
                        out var mapY);

                    var pageOffset =
                        ((firstPageBlockY + blockY) * pageBlocksX +
                            firstPageBlockX + blockX) * bytesPerBlock;
                    var sourceBlockOffset =
                        (sourceBlockY * (sourceWidth / 4) + sourceBlockX) *
                        bytesPerBlock;
                    var inside = mapX == BlockAxisMap.Keep &&
                        mapY == BlockAxisMap.Keep;
                    if (inside)
                    {
                        Buffer.BlockCopy(
                            sourceLevel,
                            sourceBlockOffset,
                            pageData,
                            pageOffset,
                            bytesPerBlock);
                        continue;
                    }

                    if (blockIsBc7)
                    {
                        throw new InvalidOperationException(
                            "A BC7 placement asked for a Clamp border " +
                            "outside the source image. BC7 partitions make " +
                            "an index repeat inexact, so the planner " +
                            "refuses this case before the build.");
                    }

                    CopyRemappedBlock(
                        sourceLevel,
                        sourceBlockOffset,
                        pageData,
                        pageOffset,
                        blockIsBc3,
                        mapX,
                        mapY);
                }
            }
        }

        /// <summary>
        /// Builds one border block from the edge source block. The
        /// endpoints stay. Every index reads the index of the edge row
        /// or column, so the block decodes to the edge texels.
        /// </summary>
        private static void CopyRemappedBlock(
            byte[] sourceLevel,
            int sourceBlockOffset,
            byte[] pageData,
            int pageOffset,
            bool blockIsBc3,
            BlockAxisMap mapX,
            BlockAxisMap mapY)
        {
            // The endpoints ride over unchanged, and the index bits
            // are rebuilt below.
            Buffer.BlockCopy(
                sourceLevel,
                sourceBlockOffset,
                pageData,
                pageOffset,
                blockIsBc3 ? 16 : 8);

            // BC1 color indices sit in the last four bytes of the
            // block. BC3 alpha indices sit in bytes two to seven.
            ulong colorIndices = ReadBits(
                sourceLevel, sourceBlockOffset + 4, 32);
            ulong alphaIndices = blockIsBc3
                ? ReadBits(sourceLevel, sourceBlockOffset + 2, 48)
                : 0;
            ulong newColor = 0;
            ulong newAlpha = 0;
            for (var row = 0; row < 4; row++)
            {
                var sourceRow = MapIndex(mapY, row);
                for (var column = 0; column < 4; column++)
                {
                    var sourceColumn = MapIndex(mapX, column);
                    var oldPosition = 4 * sourceRow + sourceColumn;
                    var newPosition = 4 * row + column;
                    newColor |= ((colorIndices >> (2 * oldPosition)) & 3) <<
                        (2 * newPosition);
                    if (blockIsBc3)
                    {
                        newAlpha |=
                            ((alphaIndices >> (3 * oldPosition)) & 7) <<
                            (3 * newPosition);
                    }
                }
            }

            WriteBits(newColor, pageData, pageOffset + 4, 32);
            if (blockIsBc3)
            {
                WriteBits(newAlpha, pageData, pageOffset + 2, 48);
            }
        }

        /// <summary>
        /// Reads a run of bits from a byte array. The bit at position
        /// zero is the lowest bit of the first byte.
        /// </summary>
        private static ulong ReadBits(
            byte[] data,
            int offset,
            int count)
        {
            ulong value = 0;
            for (var i = 0; i < count; i++)
            {
                var bit = (data[offset + (i >> 3)] >> (i & 7)) & 1;
                value |= (ulong)bit << i;
            }

            return value;
        }

        private static void WriteBits(
            ulong value,
            byte[] data,
            int offset,
            int count)
        {
            for (var i = 0; i < count; i++)
            {
                var bit = (int)((value >> i) & 1);
                var index = offset + (i >> 3);
                data[index] = (byte)(
                    (data[index] & ~(1 << (i & 7))) | (bit << (i & 7)));
            }
        }
    }
}
