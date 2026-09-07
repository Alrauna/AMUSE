using System;
using System.Collections.Generic;
using System.IO;
using Alrauna.Amuse.Editor.Analysis;
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
        /// </summary>
        private const string TempFolder = "Assets/Amuse.StreamingReadback";

        /// <summary>
        /// Session-scoped chain cache. Production captures material evidence
        /// per renderer through the stateless TryCapture path, so one avatar
        /// presents the same shared texture many times per build; without
        /// this cache every presentation would import its own clone. The key
        /// is the asset GUID plus the source and .meta write times, so any
        /// re-import or settings change re-clones instead of serving stale
        /// evidence. Editor-session scope only: nothing crosses restarts.
        /// </summary>
        private static readonly
            Dictionary<(string guid, long sourceTicks, long metaTicks),
                AlphaMipChain> Cache = new();

        internal static bool TryCapture(Texture2D source, out AlphaMipChain chain)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            chain = null;

            var path = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            var sourceFile = new FileInfo(path);
            var metaFile = new FileInfo(path + ".meta");
            var key = (
                guid,
                sourceFile.Exists ? sourceFile.LastWriteTimeUtc.Ticks : 0L,
                metaFile.Exists ? metaFile.LastWriteTimeUtc.Ticks : 0L);
            if (Cache.TryGetValue(key, out chain))
            {
                return true;
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

            var folderCreated = !AssetDatabase.IsValidFolder(TempFolder);
            if (folderCreated)
            {
                AssetDatabase.CreateFolder(
                    "Assets", Path.GetFileName(TempFolder));
            }

            // The source's GUID names the clone, so one source has one clone
            // name and a leftover from a crashed capture is overwritten, not
            // duplicated.
            var tempPath = TempFolder + "/" +
                AssetDatabase.AssetPathToGUID(path) + extension;
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

                if (clone.mipmapCount != source.mipmapCount ||
                    clone.width != source.width ||
                    clone.height != source.height)
                {
                    return false;
                }

                var levels = new AlphaTextureData[clone.mipmapCount];
                for (var mip = 0; mip < levels.Length; mip++)
                {
                    var width = Mathf.Max(1, clone.width >> mip);
                    var height = Mathf.Max(1, clone.height >> mip);

                    // GetPixels returns decoded texels bottom-to-top, the row
                    // order AlphaTextureData takes. A float alpha reaches 1f
                    // exactly when the decoded byte is 255, which is the same
                    // test the GPU predicate's 'alpha == 1.0' performs.
                    var pixels = clone.GetPixels(mip);
                    if (pixels.Length != width * height)
                    {
                        return false;
                    }

                    var flags = new byte[pixels.Length];
                    for (var index = 0; index < pixels.Length; index++)
                    {
                        flags[index] =
                            pixels[index].a >= 1f ? byte.MaxValue : (byte)0;
                    }

                    levels[mip] = new AlphaTextureData(width, height, flags);
                }

                chain = new AlphaMipChain(levels);
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
