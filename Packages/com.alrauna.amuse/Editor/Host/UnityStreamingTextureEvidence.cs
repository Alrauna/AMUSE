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
    /// importer settings produces the same texels. The
    /// <c>isReadable</c> and <c>streamingMipmaps</c> flags do not change
    /// texel content. The clone uses the source importer settings without
    /// copying the source metadata file.
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

            AlphaMipChain built = null;
            if (!TryWithReadableClone(
                    source, path, ReadAlphaChain, out refusal))
            {
                return false;
            }

            chain = built;
            // The red field contract runs at fill, once per unique
            // inert red chain; a cache hit above already passed it.
            if (channel == TextureChannel.Red &&
                bounds.Equals(AlphaPolicyBounds.Inert))
            {
                UnityMaterialEvidenceCapture.AssertRedFieldContract(chain);
            }
            Cache[key] = chain;
            return true;

            bool ReadAlphaChain(
                Texture2D clone,
                out TextureCaptureRefusalReason readRefusal)
            {
                readRefusal =
                    TextureCaptureRefusalReason.UnavailableCapture;

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
                        readRefusal =
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

                built = new AlphaMipChain(levels);
                return true;
            }
        }

        /// <summary>The read one consumer runs on the readable clone.</summary>
        private delegate bool CloneRead(
            Texture2D clone,
            out TextureCaptureRefusalReason refusal);

        /// <summary>
        /// The clone route checks the source, sweeps stale clones, copies
        /// the file and importer settings, sets the two access flags,
        /// checks the size, and cleans up.
        /// The callback receives the readable clone and answers from it.
        /// The whole clone ride leaves any open asset editing level
        /// and restores it, so a manual bake that holds one open sees
        /// the same import and the same cleanup as an unbatched
        /// build. The cleanup deletes through the asset database and
        /// also removes the pinned folder when this call created it
        /// and nothing else moved in.
        /// </summary>
        private static bool TryWithReadableClone(
            Texture2D source,
            string path,
            CloneRead read,
            out TextureCaptureRefusalReason refusal)
        {
            refusal = TextureCaptureRefusalReason.UnavailableCapture;

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

            if (!(AssetImporter.GetAtPath(path) is TextureImporter sourceImporter))
            {
                return false;
            }
            if (EditorUtility.IsDirty(sourceImporter))
            {
                // The loaded texture uses the saved settings, not these edits.
                refusal = TextureCaptureRefusalReason.SourceImporterDirty;
                return false;
            }
            if (path.StartsWith("Packages/", StringComparison.Ordinal) &&
                !File.Exists(path))
            {
                // Registry packages can resolve outside the project folder.
                refusal = TextureCaptureRefusalReason.SourceFileNotLocallyAccessible;
                return false;
            }

            // The clone work runs with any open asset editing level left,
            // and the scope restores those levels below. A manual bake
            // holds one level open around the whole build, and inside it
            // the asset database defers every import, so the copy below
            // would never import and the route would refuse.
            using (var leaveAssetEditing = new LeaveAssetEditingScope())
            {
                // A crashed run's cleanup never ran, so its clone can still
                // sit in the pinned folder. The sweep runs before a new clone
                // is made and deletes exactly the route's own naming shape,
                // so a leftover is removed rather than accumulated.
                try
                {
                    SweepStaleClones();
                }
                catch (IOException ex) when (
                    (ex.HResult & 0xffff) == 32 ||
                    (ex.HResult & 0xffff) == 33)
                {
                    // Another process can hold a leftover clone.
                    refusal = TextureCaptureRefusalReason.CloneFileInUse;
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    // The editor can lack permission to remove a leftover.
                    refusal = TextureCaptureRefusalReason.CloneFileAccessDenied;
                    return false;
                }

                var folderCreated =
                    !AssetDatabase.IsValidFolder(TempFolder);
                if (folderCreated)
                {
                    AssetDatabase.CreateFolder(
                        "Assets", Path.GetFileName(TempFolder));
                }

                // The source identity names one clone. The sweep above
                // removes a leftover before the new copy is created.
                var tempPath = ClonePath(path);
                var cloneCreated = false;
                var result = false;
                var propagating = false;
                try
                {
                    if (!leaveAssetEditing.CopySoTheCloneImports(
                            path, tempPath, out cloneCreated, out refusal))
                    {
                        return false;
                    }

                    if (!(AssetImporter.GetAtPath(tempPath)
                            is TextureImporter importer))
                    {
                        return false;
                    }

                    // Copy importer settings, including platform overrides.
                    // Unity assigns the clone its own asset identity.
                    EditorUtility.CopySerialized(sourceImporter, importer);
                    importer.isReadable = true;
                    importer.streamingMipmaps = false;
                    importer.SaveAndReimport();

                    var clone =
                        AssetDatabase.LoadAssetAtPath<Texture2D>(tempPath);
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

                    result = read(clone, out refusal);
                }
                catch
                {
                    propagating = true;
                    throw;
                }
                finally
                {
                    try
                    {
                        // Keep the clone owned by this call. A later sweep
                        // removes it if a file lock stops cleanup.
                        if (cloneCreated)
                        {
                            AssetDatabase.DeleteAsset(tempPath);
                            // A deferred import can leave an untracked file.
                            File.Delete(tempPath);
                        }
                        if (folderCreated && Directory.Exists(TempFolder) &&
                            Directory.GetFileSystemEntries(TempFolder).Length == 0)
                        {
                            if (AssetDatabase.IsValidFolder(TempFolder))
                                AssetDatabase.DeleteAsset(TempFolder);
                            else
                                Directory.Delete(TempFolder);
                        }
                    }
                    catch (Exception) when (propagating)
                    {
                        // Preserve the earlier failure. The next sweep retries.
                        Debug.LogWarning("Could not clean up a texture clone.");
                    }
                    catch (IOException)
                    {
                        refusal = TextureCaptureRefusalReason.CloneCleanupFailed;
                        result = false;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        refusal = TextureCaptureRefusalReason.CloneCleanupFailed;
                        result = false;
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// Leaves the asset editing levels a build holds open around the
        /// clone work, and restores the same number of levels when the
        /// scope ends. Inside an open level the asset database defers
        /// imports. The scope tries the clone import and checks for its
        /// importer. If the import is deferred, it leaves one level and
        /// tries again. A build with no level open leaves none.
        /// A build that holds more levels open than the bound keeps its
        /// refusal, so the route fails closed instead of unbounding the
        /// loop.
        /// </summary>
        private sealed class LeaveAssetEditingScope : IDisposable
        {
            private const int MaxLeftLevels = 8;

            internal int LeftLevels { get; private set; }

            /// <summary>
            /// Copies the source bytes and imports the clone. The scope
            /// leaves asset editing levels until the importer exists.
            /// </summary>
            internal bool CopySoTheCloneImports(
                string sourcePath,
                string tempPath,
                out bool cloneCreated,
                out TextureCaptureRefusalReason refusal)
            {
                cloneCreated = false;
                refusal = TextureCaptureRefusalReason.UnavailableCapture;
                try
                {
                    File.Copy(sourcePath, tempPath);
                    cloneCreated = true;
                }
                catch (IOException ex) when (
                    (ex.HResult & 0xffff) == 32 ||
                    (ex.HResult & 0xffff) == 33)
                {
                    // Another process can hold a file or a byte range.
                    refusal = TextureCaptureRefusalReason.CloneFileInUse;
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    // The editor can lack permission to read or write a file.
                    refusal = TextureCaptureRefusalReason.CloneFileAccessDenied;
                    return false;
                }

                while (true)
                {
                    AssetDatabase.ImportAsset(
                        tempPath, ImportAssetOptions.ForceSynchronousImport);

                    if (AssetImporter.GetAtPath(tempPath) != null)
                    {
                        return true;
                    }

                    if (LeftLevels == MaxLeftLevels)
                    {
                        return false;
                    }

                    // Stopping below zero logs an editor error, so a
                    // level is only left after the deferred shape was
                    // observed one step above.
                    AssetDatabase.StopAssetEditing();
                    LeftLevels++;
                }
            }

            public void Dispose()
            {
                while (LeftLevels > 0)
                {
                    AssetDatabase.StartAssetEditing();
                    LeftLevels--;
                }
            }
        }

        /// <summary>
        /// Acquires the atlas source evidence of one texture: the raw
        /// bytes of its protected mip levels and its decoded level 0
        /// texels. The acquisition rides the same clone steps as
        /// <see cref="TryCapture"/>. It bypasses the chain cache and
        /// the direct disk arm, because the atlas needs the imported
        /// representation itself, not a binarized chain and not the
        /// authoring bytes.
        /// </summary>
        internal static bool TryAcquireAtlasSource(
            Texture2D source,
            int protectedMipLevelCount,
            out AtlasSourceImage image,
            out TextureCaptureRefusalReason refusal)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            image = null;
            var path = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(path))
            {
                refusal = TextureCaptureRefusalReason.UnavailableCapture;
                return false;
            }

            AtlasSourceImage acquired = null;
            if (!TryWithReadableClone(
                    source, path, ReadAtlasSource, out refusal))
            {
                return false;
            }

            image = acquired;
            return true;

            bool ReadAtlasSource(
                Texture2D clone,
                out TextureCaptureRefusalReason readRefusal)
            {
                readRefusal =
                    TextureCaptureRefusalReason.UnavailableCapture;

                // The clone comes from the same source file under the
                // same settings, so its format must match the source
                // format. A difference means the import pipeline
                // answered with something else, which no caller can
                // plan around.
                if (clone.graphicsFormat != source.graphicsFormat)
                {
                    throw new InvalidOperationException(
                        "The readable clone of source texture instance " +
                        source.GetInstanceID() + " imported as format " +
                        clone.graphicsFormat + " while the source holds " +
                        source.graphicsFormat + ". The atlas route cannot " +
                        "copy between the two.");
                }

                if (protectedMipLevelCount < 1 ||
                    protectedMipLevelCount > clone.mipmapCount)
                {
                    return false;
                }

                var rawLevels =
                    new byte[protectedMipLevelCount][];
                for (var level = 0; level < rawLevels.Length; level++)
                {
                    rawLevels[level] =
                        clone.GetPixelData<byte>(level).ToArray();
                    var width = Mathf.Max(1, clone.width >> level);
                    var height = Mathf.Max(1, clone.height >> level);
                    if (rawLevels[level].Length !=
                        AtlasSourceImage.LevelBytes(
                            width, height, clone.graphicsFormat))
                    {
                        return false;
                    }
                }

                acquired = new AtlasSourceImage(
                    clone.width,
                    clone.height,
                    clone.graphicsFormat,
                    clone.mipmapCount,
                    rawLevels,
                    clone.GetPixels32());
                readRefusal = TextureCaptureRefusalReason.None;
                return true;
            }
        }
    }
}
