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

        // The active mipmap limit rides in the key: a chain captured under
        // one limit carries provenance for exactly that limit, so chains
        // captured under different limits must never share a cache entry.
        private static readonly Dictionary<(int instanceId, TextureChannel channel, float cutoff, AlphaPolicyBounds bounds, int activeMipmapLimit), AlphaMipChain>
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
            // Argument evaluation runs before the full overload's own null
            // guard, so the limit is read only for a live texture. TryCapture
            // refuses null textures unchanged, and the value is inert there.
            var activeMipmapLimit = texture != null ? texture.activeMipmapLimit : 0;
            return TryCapture(
                texture,
                channel,
                cutoffThreshold,
                IsStreamingMipmapResident,
                bounds,
                activeMipmapLimit,
                out chain);
        }

        /// <summary>
        /// The bounds and the active mipmap limit ride in the session key,
        /// so two policies that share a cutoff, and two limit states, never
        /// share a cached chain. The limit is the per-texture effective
        /// value: levels below it are skipped exactly as on the GPU route,
        /// because a non-resident source has no defined readback and the
        /// spec promises per-level degradation, not a refusal.
        /// </summary>
        internal static bool TryCapture(
            Texture2D texture,
            TextureChannel channel,
            float cutoffThreshold,
            Func<Texture2D, bool> residencyPredicate,
            AlphaPolicyBounds bounds,
            int activeMipmapLimit,
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

            if (!GeneratedTextureAttestation.TryIdentifyRouteTexture(texture))
            {
                return false;
            }

            if (residencyPredicate != null && !residencyPredicate(texture))
            {
                return false;
            }

            var threshold = Mathf.Clamp01(cutoffThreshold);
            var key = (texture.GetInstanceID(), channel, threshold, bounds,
                activeMipmapLimit);
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

                // The shader owns the three-state verdict, so the active
                // policy must reach it exactly as on the GPU route. A
                // shader cutoff keeps the policy inert on this route,
                // exactly as on the other three: the route re-derives the
                // cutoff arm from the raw value, so bounds would not
                // touch it, and the shader must not erase with them
                // either.
                var effectiveBounds = threshold < 1.0f
                    ? AlphaPolicyBounds.Inert
                    : bounds;
                material.SetFloat(
                    "_OpaqueBound", effectiveBounds.OpaqueBound / 255f);
                material.SetFloat(
                    "_NoiseBound", effectiveBounds.NoiseBound / 255f);

                var levels = new AlphaTextureData[mipCount];
                var withoutEvidence = new bool[mipCount];

                // Loop-invariant decode and validate delegates: the
                // closure captures only the clamped threshold, so one
                // instance serves every mip level.
                Func<AsyncGPUReadbackRequest, int, int, byte[]> decode =
                    (request, width, height) =>
                    {
                        var data = request.GetData<Color32>();
                        if (!UnityAlphaFieldEvidence
                                .IsExpectedBufferLength(
                                    data.Length, width, height))
                        {
                            return null;
                        }

                        var flags = new byte[data.Length];
                        if (threshold >= 1.0f)
                        {
                            // The shader already emitted the
                            // exact three-state verdict under the
                            // active bounds, so the red byte
                            // stores verbatim, byte for byte, as
                            // the GPU route stores its readback.
                            // Re-deriving the flags from the
                            // verdict byte would compare the mid
                            // band's 0 against the noise bound and
                            // turn every below-exact-one texel
                            // into erasable noise.
                            for (var i = 0; i < data.Length; i++)
                            {
                                flags[i] = data[i].r;
                            }
                        }
                        else
                        {
                            // A shader cutoff source stays
                            // gate-inert: binarize the raw value
                            // the shader returns in green, with
                            // no erasure and no policy bounds.
                            // Green-arm flags are synthesized
                            // values, so predicate-flag validation
                            // there would be vacuous and stays
                            // absent, exactly as before the core.
                            for (var i = 0; i < data.Length; i++)
                            {
                                flags[i] =
                                    (data[i].g / 255f) >= threshold
                                        ? byte.MaxValue
                                        : (byte)0;
                            }
                        }

                        return flags;
                    };
                Func<byte[], bool> validate = threshold >= 1.0f
                    ? UnityAlphaFieldEvidence.IsPredicateFlagBuffer
                    : null;

                try
                {
                    for (var m = 0; m < mipCount; m++)
                    {
                        if (!UnityAlphaFieldEvidence.IsLevelResident(
                                m, activeMipmapLimit))
                        {
                            // Declared non-resident: a blit would sample a
                            // source the GPU does not hold, and the readback
                            // proves nothing. The level keeps its declared
                            // shape as a placeholder, flagged without
                            // evidence, which degrades it to Unknown at the
                            // fold instead of refusing the texture.
                            levels[m] = UnityAlphaFieldEvidence
                                .WithoutEvidencePlaceholder(texture, m);
                            withoutEvidence[m] = true;
                            continue;
                        }

                        if (!UnityAlphaFieldEvidence.TryAcquireLevelCore(
                                texture,
                                m,
                                material,
                                TargetFormat,
                                saveRestoreActiveTarget: false,
                                decode: decode,
                                validate: validate,
                                level: out levels[m]))
                        {
                            return false;
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }

                chain = new AlphaMipChain(levels, withoutEvidence);
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

    }
}
