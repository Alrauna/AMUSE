using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// Captures alpha mip chains from live generated textures.
    /// This route handles attested sub-asset textures that lack an importer.
    /// </summary>
    internal static class UnityGeneratedTextureEvidence
    {
        private static readonly Dictionary<(int instanceId, TextureChannel channel, int cutoffBits), AlphaMipChain>
            SessionCache = new();

        internal static bool TryCapture(
            Texture2D texture,
            TextureChannel channel,
            out AlphaMipChain chain)
        {
            return TryCapture(texture, channel, 1.0f, out chain);
        }

        internal static bool TryCapture(
            Texture2D texture,
            TextureChannel channel,
            float cutoffThreshold,
            out AlphaMipChain chain)
        {
            chain = null;
            if (texture == null)
            {
                return false;
            }

            if (channel != TextureChannel.Alpha &&
                channel != TextureChannel.Red)
            {
                return false;
            }

            if (!GeneratedTextureAttestation.TryIdentifyProducer(texture, out _))
            {
                return false;
            }

            var cutoffBits = Mathf.RoundToInt(Mathf.Clamp01(cutoffThreshold) * 10000f);
            var key = (texture.GetInstanceID(), channel, cutoffBits);
            if (SessionCache.TryGetValue(key, out chain))
            {
                return true;
            }

            try
            {
                var mipCount = texture.mipmapCount;
                if (mipCount <= 0 || texture.width <= 0 || texture.height <= 0)
                {
                    return false;
                }

                var levels = new AlphaTextureData[mipCount];
                var threshold = Mathf.Clamp01(cutoffThreshold);
                var threshold255 = Mathf.RoundToInt(threshold * 255f);

                for (var m = 0; m < mipCount; m++)
                {
                    var width = Mathf.Max(1, texture.width >> m);
                    var height = Mathf.Max(1, texture.height >> m);

                    var rt = RenderTexture.GetTemporary(
                        width,
                        height,
                        0,
                        RenderTextureFormat.ARGB32,
                        RenderTextureReadWrite.Linear);

                    var previous = RenderTexture.active;
                    var readable = new Texture2D(width, height, TextureFormat.RGBA32, false);

                    try
                    {
                        Graphics.Blit(texture, rt);
                        RenderTexture.active = rt;
                        readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        readable.Apply(false, false);

                        var pixels = readable.GetPixels32();
                        var flags = new byte[pixels.Length];

                        for (var i = 0; i < pixels.Length; i++)
                        {
                            var sample = channel == TextureChannel.Red
                                ? pixels[i].r
                                : pixels[i].a;

                            flags[i] = sample >= threshold255 ? byte.MaxValue : (byte)0;
                        }

                        levels[m] = new AlphaTextureData(width, height, flags);
                    }
                    finally
                    {
                        RenderTexture.active = previous;
                        RenderTexture.ReleaseTemporary(rt);
                        Object.DestroyImmediate(readable);
                    }
                }

                chain = new AlphaMipChain(levels);
                SessionCache[key] = chain;
                return true;
            }
            catch (MissingReferenceException)
            {
                chain = null;
                return false;
            }
        }
    }
}
