using System;
using System.IO;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// Reads and decodes authoring texture images directly from disk (.png, .tga)
    /// at native uncompressed resolution, building pure area-averaged box mip chains
    /// clamped to the material's cutout or opacity threshold. When an alpha policy
    /// is active and no shader cutoff applies, the chain is the masked policy
    /// chain instead.
    /// Bypasses Unity's lossy BC7/DXT5 compression and texture downscaling.
    /// </summary>
    internal static class SourceImageAlphaReader
    {
        /// <summary>
        /// Attempts to decode the authoring image file from disk and build an
        /// uncompressed, threshold-clamped AlphaMipChain. Inert bounds and any
        /// shader cutoff keep the per-level float threshold route. An active
        /// alpha policy with no shader cutoff takes the masked route: mip 0 is
        /// decoded to exact bytes once and SourceImageMaskedChain derives
        /// every level, because a policy verdict judges masked averages, not
        /// a float threshold.
        /// </summary>
        internal static bool TryReadSourceAlphaChain(
            Texture2D texture,
            TextureChannel channel,
            float cutoffThreshold,
            AlphaPolicyBounds bounds,
            out AlphaMipChain chain)
        {
            chain = null;
            if (texture == null)
            {
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
            {
                return false;
            }

            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            if (ext != ".png" && ext != ".tga")
            {
                return false;
            }

            try
            {
                var bytes = File.ReadAllBytes(assetPath);
                if (bytes == null || bytes.Length == 0)
                {
                    return false;
                }

                // Load uncompressed RGBA32, generating mipmaps only if the texture has mips
                var hasMips = texture.mipmapCount > 1;
                var uncompressed = new Texture2D(2, 2, TextureFormat.RGBA32, hasMips);
                if (!ImageConversion.LoadImage(uncompressed, bytes, false))
                {
                    UnityEngine.Object.DestroyImmediate(uncompressed);
                    return false;
                }

                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                var isAlphaNone = importer != null && importer.alphaSource == TextureImporterAlphaSource.None;
                if (uncompressed.width != texture.width || uncompressed.height != texture.height)
                {
                    UnityEngine.Object.DestroyImmediate(uncompressed);
                    return false;
                }

                try
                {
                    var mipCount = hasMips ? uncompressed.mipmapCount : 1;
                    AlphaTextureData[] levels;

                    // A cutoff below 1f says the material owns a shader
                    // cutoff, and inert bounds reproduce the base exact-255
                    // contract, so both keep the per-level float loop exactly
                    // as before. Only an active policy with a cutoff of 1f
                    // reaches the masked route.
                    var isBaseThresholdRoute = cutoffThreshold < 1f ||
                        (bounds.OpaqueBound == byte.MaxValue &&
                            bounds.NoiseBound == 0);
                    if (isBaseThresholdRoute)
                    {
                        var threshold = Mathf.Clamp01(cutoffThreshold);
                        levels = new AlphaTextureData[mipCount];

                        for (var m = 0; m < mipCount; m++)
                        {
                            var pixels = uncompressed.GetPixels(m);
                            var width = Mathf.Max(1, uncompressed.width >> m);
                            var height = Mathf.Max(1, uncompressed.height >> m);
                            var flags = new byte[pixels.Length];

                            for (var i = 0; i < pixels.Length; i++)
                            {
                                var sample = isAlphaNone && channel == TextureChannel.Alpha
                                    ? 1.0f
                                    : (channel == TextureChannel.Red ? pixels[i].r : pixels[i].a);

                                flags[i] = sample >= threshold ? byte.MaxValue : (byte)0;
                            }

                            levels[m] = new AlphaTextureData(width, height, flags);
                        }
                    }
                    else
                    {
                        // The masked route decodes mip 0 once and lets the
                        // builder derive every level, so no Unity downscale
                        // touches the evidence the policy judges.
                        var pixels = uncompressed.GetPixels(0);
                        var decoded = new byte[pixels.Length];

                        for (var i = 0; i < decoded.Length; i++)
                        {
                            var sample = isAlphaNone && channel == TextureChannel.Alpha
                                ? 1.0f
                                : (channel == TextureChannel.Red ? pixels[i].r : pixels[i].a);

                            decoded[i] = SourceImageMaskedChain.DecodeByte(sample);
                        }

                        levels = SourceImageMaskedChain.Build(
                            decoded,
                            uncompressed.width,
                            uncompressed.height,
                            mipCount,
                            bounds);
                    }

                    chain = new AlphaMipChain(levels);
                    return true;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(uncompressed);
                }
            }
            catch
            {
                chain = null;
                return false;
            }
        }
    }
}
