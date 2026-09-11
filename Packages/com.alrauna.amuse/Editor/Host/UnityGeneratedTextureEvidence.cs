using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
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
        private const GraphicsFormat TargetFormat = GraphicsFormat.R8G8B8A8_UNorm;

        private static readonly Dictionary<(int instanceId, TextureChannel channel, float cutoff, AlphaPolicyBounds bounds), AlphaMipChain>
            SessionCache = new();

        /// <summary>
        /// Clears the evidence cache.
        /// Call this method before each build and after cleanup.
        /// </summary>
        internal static void ClearCache()
        {
            SessionCache.Clear();
        }

        /// <summary>
        /// Captures under the inert bounds, which reproduce the base
        /// exact-255 contract. Callers that know the active policy pass
        /// it to the full overload.
        /// </summary>
        internal static bool TryCapture(
            Texture2D texture,
            TextureChannel channel,
            float cutoffThreshold,
            AlphaPolicyBounds bounds,
            out AlphaMipChain chain)
        {
            return TryCapture(
                texture,
                channel,
                cutoffThreshold,
                IsStreamingMipmapResident,
                bounds,
                out chain);
        }

        /// <summary>
        /// The bounds ride in the session key, so two policies that
        /// share a cutoff never share a cached chain.
        /// </summary>
        internal static bool TryCapture(
            Texture2D texture,
            TextureChannel channel,
            float cutoffThreshold,
            Func<Texture2D, bool> residencyPredicate,
            AlphaPolicyBounds bounds,
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

            if (residencyPredicate != null && !residencyPredicate(texture))
            {
                return false;
            }

            var threshold = Mathf.Clamp01(cutoffThreshold);
            var key = (texture.GetInstanceID(), channel, threshold, bounds);
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

                if (!HostCapabilitiesPass(
                        SystemInfo.supportsAsyncGPUReadback,
                        SystemInfo.IsFormatSupported(TargetFormat, FormatUsage.Render),
                        SystemInfo.IsFormatSupported(texture.graphicsFormat, FormatUsage.Sample)))
                {
                    return false;
                }

                var shaderPath = channel == TextureChannel.Red
                    ? UnityAlphaFieldEvidence.RedShaderAssetPath
                    : UnityAlphaFieldEvidence.ShaderAssetPath;
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                if (shader == null || !shader.isSupported)
                {
                    return false;
                }

                var material = new Material(shader);
                var levels = new AlphaTextureData[mipCount];

                try
                {
                    for (var m = 0; m < mipCount; m++)
                    {
                        var width = Mathf.Max(1, texture.width >> m);
                        var height = Mathf.Max(1, texture.height >> m);

                        material.SetInt("_Mip", m);

                        var descriptor = new RenderTextureDescriptor(width, height, TargetFormat, 0)
                        {
                            sRGB = false,
                            useMipMap = false,
                            autoGenerateMips = false
                        };

                        var rt = RenderTexture.GetTemporary(descriptor);

                        try
                        {
                            if (!IsExpectedTargetFormat(rt.graphicsFormat, TargetFormat) ||
                                !IsExpectedLevelSize(rt.width, rt.height, width, height))
                            {
                                return false;
                            }

                            Graphics.Blit(texture, rt, material);

                            var request = AsyncGPUReadback.Request(rt, 0, TargetFormat);
                            request.WaitForCompletion();
                            if (request.hasError ||
                                !IsExpectedLevelSize(request.width, request.height, width, height))
                            {
                                return false;
                            }

                            var data = request.GetData<Color32>();
                            if (!IsExpectedBufferLength(data.Length, width, height))
                            {
                                return false;
                            }

                            var flags = new byte[data.Length];
                            for (var i = 0; i < data.Length; i++)
                            {
                                // The generated route's proof channel is
                                // .r at the exact arm and .g under a
                                // shader cutoff; that packing is pinned
                                // by the existing blit shader. Erasure
                                // reads the same byte the opaque test
                                // reads, and only in the exact arm,
                                // because shader-cutoff sources are
                                // gate-inert.
                                var isErased = threshold >= 1.0f &&
                                    bounds.NoiseBound > 0 &&
                                    data[i].r < bounds.NoiseBound;
                                var isOpaque = threshold >= 1.0f
                                    ? data[i].r >= bounds.OpaqueBound
                                    : (data[i].g / 255f) >= threshold;
                                flags[i] = isErased
                                    ? AlphaTextureData.ErasedFlag
                                    : isOpaque ? byte.MaxValue : (byte)0;
                            }

                            levels[m] = new AlphaTextureData(width, height, flags);
                        }
                        finally
                        {
                            RenderTexture.ReleaseTemporary(rt);
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(material);
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

        internal static bool IsStreamingMipmapResident(Texture2D texture)
        {
            if (texture == null)
            {
                return false;
            }

            return IsStreamingMipmapResident(
                texture.streamingMipmaps,
                texture.IsRequestedMipmapLevelLoaded(),
                texture.loadedMipmapLevel);
        }

        internal static bool IsStreamingMipmapResident(
            bool streamingMipmaps,
            bool isRequestedMipmapLevelLoaded,
            int loadedMipmapLevel)
        {
            if (!streamingMipmaps)
            {
                return true;
            }

            return isRequestedMipmapLevelLoaded && loadedMipmapLevel == 0;
        }

        internal static bool HostCapabilitiesPass(
            bool asyncReadback,
            bool targetRenderable,
            bool sourceSampleable)
        {
            return asyncReadback && targetRenderable && sourceSampleable;
        }

        internal static bool IsExpectedTargetFormat(
            GraphicsFormat actual, GraphicsFormat expected)
        {
            return actual == expected;
        }

        internal static bool IsExpectedLevelSize(
            int width, int height, int expectedWidth, int expectedHeight)
        {
            return width == expectedWidth && height == expectedHeight;
        }

        internal static bool IsExpectedBufferLength(
            long actualLength, int width, int height)
        {
            return actualLength == (long)width * height;
        }
    }
}
