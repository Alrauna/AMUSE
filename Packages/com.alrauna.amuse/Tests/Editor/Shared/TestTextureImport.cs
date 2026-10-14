using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Shared
{
    /// <summary>
    /// Stages pixel grids as real imported PNG texture assets for editor
    /// test fixtures. The forced synchronous import is mandatory:
    /// <see cref="AssetDatabase.LoadAssetAtPath"/> must see the imported
    /// asset inside the same test step, and a plain
    /// <see cref="AssetDatabase.ImportAsset(string)"/> may defer the
    /// actual import past that load.
    /// </summary>
    internal static class TestTextureImport
    {
        /// <summary>
        /// Writes <paramref name="pixels"/> bottom-to-top into a
        /// <paramref name="width"/> x <paramref name="height"/> PNG at the
        /// project-relative asset path <paramref name="name"/>, imports it,
        /// applies <paramref name="configureImporter"/> to the
        /// <see cref="TextureImporter"/> when present, and returns the
        /// loaded texture. <paramref name="alpha"/> false stages the grid
        /// as RGB24 so the written PNG carries no alpha channel; the
        /// default true stages RGBA32.
        /// </summary>
        internal static Texture2D WritePng(
            string name,
            int width,
            int height,
            Color32[] pixels,
            Action<TextureImporter> configureImporter = null,
            bool alpha = true)
        {
            var format = alpha
                ? TextureFormat.RGBA32
                : TextureFormat.RGB24;
            var staging = new Texture2D(width, height, format, false);
            try
            {
                staging.SetPixels32(pixels);
                staging.Apply(false, false);
                File.WriteAllBytes(name, staging.EncodeToPNG());
            }
            finally
            {
                // Encoding or writing can throw; the in-memory staging
                // texture must not survive that.
                UnityEngine.Object.DestroyImmediate(staging);
            }

            AssetDatabase.ImportAsset(
                name, ImportAssetOptions.ForceSynchronousImport);

            if (configureImporter != null)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(name);
                configureImporter(importer);
                importer.SaveAndReimport();
            }

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(name);
            Assert.That(
                loaded, Is.Not.Null,
                $"Imported texture '{name}' must load.");
            return loaded;
        }

        /// <summary>
        /// Creates a native (non-imported) texture asset: a stable
        /// GUID/local id and a supported default sampler, but no
        /// <see cref="TextureImporter"/>, so import-backed evidence stays
        /// unavailable.
        /// </summary>
        internal static Texture2D ImportNative(string path)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            AssetDatabase.CreateAsset(texture, path);
            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(
                loaded, Is.Not.Null,
                $"Native asset '{path}' must load.");
            return loaded;
        }

        /// <summary>
        /// Writes, imports, and returns a tiny texture asset under the
        /// given folder: a 4x4 pixel grid, mipmaps off. The default import
        /// yields a supported sampler unless a test opts out through
        /// <paramref name="configure"/>.
        /// </summary>
        internal static Texture2D ImportTexture(
            string folder,
            string name,
            Action<TextureImporter> configure = null,
            bool sourceHasAlpha = true)
        {
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(128, 64, 32, 200);
            }

            return WritePng(
                folder + "/" + name + ".png",
                4,
                4,
                pixels,
                importer =>
                {
                    importer.mipmapEnabled = false;
                    configure?.Invoke(importer);
                },
                alpha: sourceHasAlpha);
        }

        /// <summary>
        /// Creates a native (non-imported) texture asset under the given
        /// folder: stable identity and a bounded format, but no
        /// <see cref="TextureImporter"/> at all.
        /// </summary>
        internal static Texture2D ImportNativeAsset(string folder, string name)
        {
            return ImportNative(folder + "/" + name + ".asset");
        }
    }
}
