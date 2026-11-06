using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// The capture route for streaming textures: import a readable,
    /// non-streaming clone of the same source and read the clone's chain on
    /// the CPU.
    /// <para>
    /// Measured in this project: a GPU blit-Load over a streaming texture's
    /// chain returned plausible but wrong alpha while
    /// <c>loadedMipmapLevel</c> was 0 before and after the capture, so no
    /// residency observation makes the GPU read sound. The importer, by
    /// contrast, is deterministic: the same source file under the same
    /// importer settings produces the same texels, and neither
    /// <c>isReadable</c> nor <c>streamingMipmaps</c> changes texel content
    /// (both measured). Copying the asset copies the source and its .meta,
    /// so the clone differs from the avatar's texture in exactly those two
    /// flags and nothing else.
    /// </para>
    /// <para>
    /// The route is fail-closed: any step that does not hold - a texture
    /// with no asset path, a texture that is not its file's main asset, a
    /// copy or import that fails, a chain whose dimensions disagree with the
    /// source's declared chain - refuses and the caller keeps its
    /// missing-evidence outcome. The temporary asset is deleted even on
    /// refusal, and the temporary folder goes away with it.
    /// </para>
    /// </summary>
    internal static class UnityStreamingTextureEvidence
    {
        /// <summary>
        /// A dedicated temporary folder under Assets, so the clone is an
        /// imported asset the editor will decode. The name is fixed so a
        /// crashed build's leftovers are replaced, not accumulated, by the
        /// next capture.
        private const string TempFolder = "Assets/Amuse.StreamingReadback";

        /// <summary>
        /// The route's own clone naming shape: the source's 32-hex GUID
        /// plus the source's extension, directly in the pinned folder.
        /// The sweep deletes exactly this shape, so the ownership test is
        /// the name alone and nothing else in the folder is touched.
        /// </summary>
        private static readonly Regex CloneName = new Regex(
            "^[0-9a-f]{32}\\.[0-9A-Za-z]+$", RegexOptions.Compiled);

        /// <summary>
        /// The clone path for a source asset path: the pinned folder plus
        /// the source's GUID and extension, the one shape
        /// <see cref="CloneName"/> matches.
        /// </summary>
        private static string ClonePath(string sourcePath)
        {
            return TempFolder + "/" +
                AssetDatabase.AssetPathToGUID(sourcePath) +
                Path.GetExtension(sourcePath);
        }

        /// <summary>
        /// Deletes the route's own leftovers from the pinned folder before
        /// a new clone is made: a file shaped like the route's own clones
        /// is the leftover of a run whose cleanup never ran. Everything
        /// else in the folder belongs to someone else and is never
        /// touched.
        /// </summary>
        private static void SweepStaleClones()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(TempFolder))
            {
                var assetPath = file.Replace('\\', '/');
                if (!CloneName.IsMatch(Path.GetFileName(assetPath)))
                {
                    continue;
                }

                if (AssetDatabase.DeleteAsset(assetPath))
                {
                    continue;
                }

                // A leftover the asset database never imported: remove the
                // file and its meta directly so the sweep is complete.
                if (File.Exists(assetPath))
                {
                    File.Delete(assetPath);
                }
                if (File.Exists(assetPath + ".meta"))
                {
                    File.Delete(assetPath + ".meta");
                }
            }
        }

        /// <summary>
        /// Process-local cache, keyed by source asset identity. The key
        /// is the asset GUID plus the source and .meta write times, so any
        /// re-import or settings change re-clones instead of serving stale
        /// evidence. The cutoff rides in as the exact clamped float, not a
        /// quantized bucket: two cutoffs inside one bucket can straddle a
        /// texel byte and must not share a chain. The request's
        /// disk-fallback decision rides in too, so a capture whose request
        /// disallows the route is never served a chain that capture
        /// captured through the route. Editor-session scope only: nothing
        /// crosses restarts.
        /// </summary>
        private static readonly
            Dictionary<(string guid, long sourceTicks, long metaTicks,
                    TextureChannel channel, float cutoff,
                    AlphaPolicyBounds bounds, bool diskFallbackAllowed),
                AlphaMipChain> Cache = new();

        /// <summary>
        /// Clears the streaming evidence cache. The session reset calls
        /// this, so a second build in the same editor session never
        /// serves a chain captured before that reset.
        /// </summary>
        internal static void ClearCache()
        {
            Cache.Clear();
        }

        /// <summary>
        /// Reports whether the streaming cache holds no chains. The
        /// session reset tests probe it, so a reset that skips this
        /// cache fails its own assertion instead of passing on stale
        /// leftover evidence.
        /// </summary>
        internal static bool CacheIsEmpty => Cache.Count == 0;

        internal static bool TryCapture(
            Texture2D source,
            TextureChannel channel,
            float cutoffThreshold,
            AlphaPolicyBounds bounds,
            out AlphaMipChain chain,
            bool allowDiskFallback = true)
        {
            return TryCapture(
                source, channel, cutoffThreshold, bounds,
                out chain, allowDiskFallback, out _);
        }

        /// <summary>
        /// The capture with its named refusal. Every refusal below names a
        /// reason family: the route's own fail-closed gates refuse with
        /// UnavailableCapture, and the crunched decode refusals in the
        /// clone arm override with UnsupportedFormat, so a false answer
        /// always explains itself.
        /// </summary>
        internal static bool TryCapture(
            Texture2D source,
            TextureChannel channel,
            float cutoffThreshold,
            AlphaPolicyBounds bounds,
            out AlphaMipChain chain,
            bool allowDiskFallback,
            out TextureCaptureRefusalReason refusal)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            chain = null;
            refusal = TextureCaptureRefusalReason.UnavailableCapture;

            var path = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            var sourceFile = new FileInfo(path);
            var metaFile = new FileInfo(path + ".meta");
            var cutoff = Mathf.Clamp01(cutoffThreshold);
            // The bounds ride in the key: two policies can share a cutoff
            // while building different chains, so a policy change must
            // re-capture instead of serving the other policy's evidence.
            // allowDiskFallback is the same value the capture gate below
            // consults, so the key names the route that built the chain.
            var key = (
                guid,
                sourceFile.Exists ? sourceFile.LastWriteTimeUtc.Ticks : 0L,
                metaFile.Exists ? metaFile.LastWriteTimeUtc.Ticks : 0L,
                channel,
                cutoff,
                bounds,
                allowDiskFallback);
            if (Cache.TryGetValue(key, out chain))
            {
                return true;
            }

            // Try direct uncompressed disk source reading first to bypass
            // Unity downsampling and BC7/DXT loss. The route may trust
            // the authoring bytes only when the imported representation
            // stores the same texel bytes: a compressed or packed import
            // can quantize a texel across the material cutoff while the
            // authoring byte stays on the other side, so those formats
            // skip this arm and ride the clone route below.
            if (SourceImageAlphaReader.AdmitsImportedFormat(source.format) &&
                SourceImageAlphaReader.TryReadSourceAlphaChain(
                    source, channel, cutoffThreshold, bounds, out chain))
            {
                // The red field contract runs at fill, once per unique
                // inert red chain; a cache hit above already passed it.
                if (channel == TextureChannel.Red &&
                    bounds.Equals(AlphaPolicyBounds.Inert))
                {
                    UnityMaterialEvidenceCapture.AssertRedFieldContract(chain);
                }
                Cache[key] = chain;
                return true;
            }
            if (!allowDiskFallback)
            {
                return false;
            }

            // A texture that is not its file's main asset (a sub-asset, or a
            // texture inside a scene or model file) has no source the
            // importer reproduces, so it cannot be cloned into evidence.
            if (!ReferenceEquals(
                    AssetDatabase.LoadMainAssetAtPath(path), source))
            {
                return false;
            }

            var extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }

            // A crashed run's cleanup never ran, so its clone can still
            // sit in the pinned folder. The sweep runs before a new clone
            // is made and deletes exactly the route's own naming shape,
            // so a leftover is removed rather than accumulated.
            SweepStaleClones();

            var folderCreated = !AssetDatabase.IsValidFolder(TempFolder);
            if (folderCreated)
            {
                AssetDatabase.CreateFolder(
                    "Assets", Path.GetFileName(TempFolder));
            }

            // The source's GUID names the clone, so one source has one clone
            // name and a leftover from a crashed capture is overwritten, not
            // duplicated.
            var tempPath = ClonePath(path);
            try
            {
                if (!AssetDatabase.CopyAsset(path, tempPath))
                {
                    return false;
                }

                if (!(AssetImporter.GetAtPath(tempPath)
                        is TextureImporter importer))
                {
                    return false;
                }

                // Only the two flags that gate editor access change. Every
                // content-bearing setting rides in on the copied .meta.
                importer.isReadable = true;
                importer.streamingMipmaps = false;
                importer.SaveAndReimport();

                var clone = AssetDatabase.LoadAssetAtPath<Texture2D>(tempPath);
                if (clone == null || !clone.isReadable)
                {
                    return false;
                }

                // A crunched clone has no CPU decode on this platform:
                // GetPixels refuses crunched textures with an exception.
                // Crunched is compressed, so the import already left the
                // direct route at the format gate, and this refusal keeps
                // the capture fail closed instead of answering from the
                // authoring bytes.
                if (clone.format == TextureFormat.DXT1Crunched ||
                    clone.format == TextureFormat.DXT5Crunched)
                {
                    refusal = TextureCaptureRefusalReason.UnsupportedFormat;
                    return false;
                }

                if (clone.mipmapCount != source.mipmapCount ||
                    clone.width != source.width ||
                    clone.height != source.height)
                {
                    return false;
                }

                // Published-chain limitation: the clone's coarser
                // levels arrive pre-averaged, so masked re-averaging is
                // not available here and each texel resolves at its own
                // level. The loop honors the bounds rather than
                // refusing the capture, so the evidence always matches
                // the bounds in the cache key, and a policy-active
                // texture whose only route is this clone keeps a
                // capture route.
                var threshold = cutoff;
                var levels = new AlphaTextureData[clone.mipmapCount];
                for (var mip = 0; mip < levels.Length; mip++)
                {
                    var width = Mathf.Max(1, clone.width >> mip);
                    var height = Mathf.Max(1, clone.height >> mip);

                    // GetPixels returns decoded texels bottom-to-top, the row
                    // order AlphaTextureData takes. A float alpha reaches 1f
                    // exactly when the decoded byte is 255, which is the same
                    // test the GPU predicate's 'alpha == 1.0' performs.
                    // GetPixels is the one call in this arm the platform
                    // may refuse: a crunched clone decodes through no CPU
                    // path here, and the platform answers with this
                    // exception. The catch is scoped to this single call
                    // because the exception names a platform decode
                    // limitation, not an AMUSE invariant failure, and a
                    // wider catch would fold real defects into a format
                    // refusal.
                    Color[] pixels;
                    try
                    {
                        pixels = clone.GetPixels(mip);
                    }
                    catch (ArgumentException)
                    {
                        refusal =
                            TextureCaptureRefusalReason.UnsupportedFormat;
                        return false;
                    }

                    if (pixels.Length != width * height)
                    {
                        return false;
                    }

                    var flags = new byte[pixels.Length];
                    for (var index = 0; index < pixels.Length; index++)
                    {
                        // The channel selector matches the pinned lilToon
                        // sampling: masks read .r, alpha reads .a. Both are
                        // held to the same predicate — the decoded float is
                        // exactly one iff the stored byte is 255 — whether
                        // or not GetPixels applies the sRGB transfer,
                        // because the transfer is monotone and fixes 1.0.
                        var value = channel == TextureChannel.Red
                            ? pixels[index].r
                            : pixels[index].a;
                        var decoded = SourceImageMaskedChain.DecodeByte(value);
                        var isErased = threshold >= 1.0f &&
                            bounds.NoiseBound > 0 &&
                            decoded < bounds.NoiseBound;
                        var isOpaque = threshold >= 1.0f
                            ? decoded >= bounds.OpaqueBound
                            : value >= threshold;
                        flags[index] = isErased
                            ? AlphaTextureData.ErasedFlag
                            : isOpaque ? byte.MaxValue : (byte)0;
                    }

                    levels[mip] = new AlphaTextureData(width, height, flags);
                }

                chain = new AlphaMipChain(levels);
                // The red field contract runs at fill, once per unique
                // inert red chain; a cache hit above already passed it.
                if (channel == TextureChannel.Red &&
                    bounds.Equals(AlphaPolicyBounds.Inert))
                {
                    UnityMaterialEvidenceCapture.AssertRedFieldContract(chain);
                }
                Cache[key] = chain;
                return true;
            }
            finally
            {
                AssetDatabase.DeleteAsset(tempPath);
                if (folderCreated &&
                    AssetDatabase.IsValidFolder(TempFolder) &&
                    AssetDatabase.FindAssets("", new[] { TempFolder })
                        .Length == 0)
                {
                    AssetDatabase.DeleteAsset(TempFolder);
                }
            }
        }
    }
}
