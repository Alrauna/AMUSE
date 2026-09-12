using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// The one Unity implementation of <see cref="AlphaFieldProvider"/>. It converts
    /// supported Unity texture state into an immutable <see cref="AlphaMipChain"/>
    /// of the <see cref="AlphaTextureData"/> grids the exact triangle alpha
    /// classifier already consumes, and refuses everything it cannot prove.
    /// <para>
    /// It lives outside <c>Analysis</c> because that namespace has no dependency on
    /// the <c>UnityEditor</c> namespace and must keep none, and outside
    /// <c>Semantics</c> because <c>Analysis</c> depends on <c>Semantics</c> and not
    /// the reverse. It reads only: it opens no <c>TextureImporter</c>, writes no
    /// asset, and never changes an import setting.
    /// </para>
    /// <para>
    /// The evidence it produces is <em>predicate-equivalent</em> to effective shader
    /// alpha at <em>every captured declared mip</em>, not byte-identical to GPU
    /// memory. At each level each byte is the policy verdict over the sampled
    /// channel: 255 at or above the policy's opaque bound, 1
    /// (<see cref="Analysis.AlphaTextureData.ErasedFlag"/>) strictly below its
    /// noise bound, and 0 otherwise. The inert bounds reduce the verdict to the
    /// base exact-255 contract: byte 255 marks exactly the texels whose sampled
    /// alpha is exactly one, and every other byte marks a value strictly below
    /// one. The chain is the texture's complete declared mip chain, mip 0 first:
    /// the hardware may select any level and the resolver cannot know which.
    /// </para>
    /// <para>
    /// The values come from the <em>GPU-decoded imported representation</em>, read
    /// back through the predicate shader, rather than from any CPU view of the
    /// asset. Admission is controlled by the closed characterized allowlist in
    /// <see cref="IsAdmittedFormat"/>: a format is admitted only where durable
    /// characterization through this route and an authoritative decode rule both
    /// support it.
    /// </para>
    /// <para>
    /// The levels the effective mipmap limit removes from the GPU have no
    /// effective alpha for playback, so the capture marks them without evidence
    /// (<see cref="Analysis.AlphaMipChain.IsLevelWithoutEvidence"/>) and the
    /// fold degrades them to Unknown instead of reading a verdict.
    /// </para>
    /// </summary>
    internal sealed class UnityAlphaFieldEvidence
    {
        /// <summary>
        /// The predicate shader's project path. UPM addresses every package as
        /// <c>Packages/&lt;name&gt;/...</c> regardless of where it physically lives,
        /// so this is stable for embedded, local, git and VPM installs alike.
        /// <para>
        /// Shader.Find is deliberately not used: it resolves by shader name, which
        /// this repository does not own, and would silently bind to whichever
        /// asset won a name collision.
        /// </para>
        /// </summary>
        internal const string ShaderAssetPath =
            "Packages/com.alrauna.amuse/Editor/Host/Shaders/AmuseAlphaExactOne.shader";

        /// <summary>
        /// The red-channel predicate shader's project path, mirroring
        /// <see cref="ShaderAssetPath"/>. It exists for the lilToon alpha
        /// mask, which samples <c>_AlphaMask</c>'s red channel through
        /// <c>sampler_MainTex</c>.
        /// </summary>
        internal const string RedShaderAssetPath =
            "Packages/com.alrauna.amuse/Editor/Host/Shaders/AmuseRedExactOne.shader";

        private readonly Dictionary<
            (TextureSourceId source, TextureChannel channel),
            AlphaMipChain> _fieldsBySource;

        /// <summary>
        /// Resolves the supplied textures to their stable project identities through
        /// the existing <see cref="UnityTextureEvidence.TryGetSourceId"/>, so the
        /// identity rule can never disagree with the one the shader frontends used to
        /// build the <see cref="TextureSample"/>. The opaque source-id format is
        /// never parsed here.
        /// <para>
        /// Elements that are null, destroyed, not a <see cref="Texture2D"/>, or
        /// without a resolvable identity are skipped rather than rejected: an
        /// unassigned material slot yields a null texture and is an ordinary input,
        /// not a caller error. A later lookup for such a texture simply refuses.
        /// </para>
        /// </summary>
        internal UnityAlphaFieldEvidence(
            IEnumerable<(Texture texture, TextureChannel channel)> requests)
        {
            if (requests == null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            _fieldsBySource = new Dictionary<
                (TextureSourceId, TextureChannel), AlphaMipChain>();
            foreach (var (texture, channel) in requests)
            {
                if (!TryCapture(texture, channel, out var source, out var chain))
                {
                    continue;
                }

                // Two textures resolving to one identity are the same asset, so the
                // first wins and the duplicate is not an error. The channel is
                // part of the key, so one asset can serve its alpha field as a
                // main texture and its red field as a mask.
                if (_fieldsBySource.ContainsKey((source, channel)))
                {
                    continue;
                }

                _fieldsBySource.Add((source, channel), chain);
            }
        }

        /// <summary>
        /// Captures the alpha field of each supplied texture. Kept for the
        /// historical call shape, which never needed a second channel.
        /// </summary>
        internal UnityAlphaFieldEvidence(IEnumerable<Texture> textures)
            : this(textures?.Select(WithAlphaChannel))
        {
        }

        private static (Texture, TextureChannel) WithAlphaChannel(
            Texture texture)
        {
            return (texture, TextureChannel.Alpha);
        }

        /// <summary>
        /// Captures the complete immutable alpha field for one supported
        /// texture. Kept for the historical call shape. It captures
        /// under the inert bounds, which reproduce the base exact-255
        /// contract.
        /// </summary>
        internal static bool TryCapture(
            Texture texture,
            out TextureSourceId source,
            out AlphaMipChain chain)
        {
            return TryCapture(
                texture, TextureChannel.Alpha, 1.0f, AlphaPolicyBounds.Inert,
                out source, out chain, out _);
        }

        internal static bool TryCapture(
            Texture texture,
            float cutoffThreshold,
            AlphaPolicyBounds bounds,
            out TextureSourceId source,
            out AlphaMipChain chain)
        {
            return TryCapture(
                texture, TextureChannel.Alpha, cutoffThreshold, bounds,
                out source, out chain, out _);
        }

        /// <summary>
        /// Captures under the inert bounds, which reproduce the base
        /// exact-255 contract. Callers that know the active policy pass
        /// it to the full overload.
        /// </summary>
        internal static bool TryCapture(
            Texture texture,
            TextureChannel channel,
            out TextureSourceId source,
            out AlphaMipChain chain)
        {
            return TryCapture(
                texture, channel, 1.0f, AlphaPolicyBounds.Inert,
                out source, out chain, out _);
        }

        /// <summary>
        /// Captures the complete field of one requested channel for one
        /// supported texture. Every validation predicate belongs here so
        /// lookup never needs to touch a Unity object after construction.
        /// The red channel serves the lilToon alpha mask; its predicate is
        /// decode-proof because the sRGB transfer is monotone and fixes
        /// exactly 1.0, so byte 255 still marks exactly the texels whose
        /// sampled value is one.
        /// <para>
        /// The bounds are the user's alpha policy as exact bytes, and every
        /// capture route honors them. The inert bounds reproduce the base
        /// exact-255 contract. The cached routes carry the bounds in their
        /// cache keys, so two policies never share a cached chain, and the
        /// direct GPU route applies them in its predicate shader.
        /// </para>
        /// </summary>
        /// <param name="refusal">
        /// The named reason family the capture refused for, or
        /// <see cref="TextureCaptureRefusalReason.None"/> when the chain was
        /// captured. The reason names the first refusing gate: the gates are
        /// evaluated in a fixed order, so one texture always refuses with the
        /// same family.
        /// </param>
        internal static bool TryCapture(
            Texture texture,
            TextureChannel channel,
            float cutoffThreshold,
            AlphaPolicyBounds bounds,
            out TextureSourceId source,
            out AlphaMipChain chain,
            out TextureCaptureRefusalReason refusal)
        {
            source = default;
            chain = null;
            refusal = TextureCaptureRefusalReason.None;

            if (!Enum.IsDefined(typeof(TextureChannel), channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel));
            }

            if (channel != TextureChannel.Alpha &&
                channel != TextureChannel.Red)
            {
                refusal = TextureCaptureRefusalReason.UnavailableCapture;
                return false;
            }

            // Unity's overloaded equality is required: it is true for a destroyed
            // object, where ReferenceEquals would be false. A non-Texture2D
            // (RenderTexture, Cubemap, array, 3D) is skipped for the same reason it
            // was refused at lookup.
            var texture2D = texture as Texture2D;
            if (texture2D == null)
            {
                refusal = TextureCaptureRefusalReason.UnavailableCapture;
                return false;
            }

            if (!UnityTextureEvidence.TryGetSourceId(texture2D, out source))
            {
                refusal = TextureCaptureRefusalReason.UnavailableCapture;
                return false;
            }

            try
            {
                // Every policy gate precedes the first allocation. The format
                // allowlist in particular is checked before any GPU call, so a
                // compressed source never reaches a route that would log a
                // Unity error. The mipmap limit degrades per level: the
                // limit removes the texture's highest-resolution mips from
                // the GPU, those levels have no effective alpha for
                // playback, and the capture marks them without evidence
                // instead of refusing - the whole-texture refusal is left
                // for the case where no level remains resident.
                // Each gate names its reason family in the gate's own order,
                // so a refusal always explains the first gate that refused.
                if (!IsAdmittedBuildTarget(
                        EditorUserBuildSettings.activeBuildTarget))
                {
                    refusal = TextureCaptureRefusalReason.UnavailableCapture;
                    source = default;
                    return false;
                }

                if (!IsAdmittedFormat(texture2D.format))
                {
                    refusal = TextureCaptureRefusalReason.UnsupportedFormat;
                    source = default;
                    return false;
                }

                if (!MipLimitGatesPass(
                        texture2D.activeMipmapLimit,
                        texture2D.mipmapCount))
                {
                    refusal = TextureCaptureRefusalReason.NonResidentMips;
                    source = default;
                    return false;
                }

                if (!AreDimensionsUsable(
                        texture2D.width,
                        texture2D.height,
                        texture2D.mipmapCount))
                {
                    refusal = TextureCaptureRefusalReason.UnavailableCapture;
                    source = default;
                    return false;
                }

                // Attested generated textures lack an importer. They are captured
                // directly from the live resident object via RenderTexture blit.
                if (GeneratedTextureAttestation.TryIdentifyProducer(texture2D, out _))
                {
                    if (!UnityGeneratedTextureEvidence.TryCapture(
                            texture2D, channel, cutoffThreshold, bounds,
                            out chain))
                    {
                        refusal = TextureCaptureRefusalReason.UnavailableCapture;
                        source = default;
                        chain = null;
                        return false;
                    }

                    chain = WithResidencyProvenance(
                        chain, texture2D.activeMipmapLimit);

                    return true;
                }

                // A streaming texture never touches the GPU route: its
                // editor read is measured untrustworthy, however resident it
                // reports. The readable-clone route reads the importer's own
                // output instead, and the capability gates below guard the
                // GPU route this texture does not take.
                if (texture2D.streamingMipmaps)
                {
                    if (!UnityStreamingTextureEvidence.TryCapture(
                            texture2D, channel, cutoffThreshold, bounds,
                            out chain))
                    {
                        refusal = TextureCaptureRefusalReason.UnavailableCapture;
                        source = default;
                        chain = null;
                        return false;
                    }

                    chain = WithResidencyProvenance(
                        chain, texture2D.activeMipmapLimit);

                    return true;
                }

                // The bounds reach this route through the predicate shader:
                // the shaders emit the three-state verdict under _OpaqueBound
                // and _NoiseBound, so the policy governs the primary path for
                // imported avatar textures too.
                if (!HostCapabilitiesPass(
                        SystemInfo.supportsAsyncGPUReadback,
                        SystemInfo.IsFormatSupported(PredicateTarget, FormatUsage.Render),
                        SystemInfo.IsFormatSupported(PredicateTarget, FormatUsage.ReadPixels),
                        SourceSamplingGatePasses(
                            texture2D.format,
                            SystemInfo.IsFormatSupported(
                                texture2D.graphicsFormat, FormatUsage.Sample))))
                {
                    refusal = TextureCaptureRefusalReason.UnavailableCapture;
                    source = default;
                    return false;
                }

                var shaderPath = channel == TextureChannel.Red
                    ? RedShaderAssetPath
                    : ShaderAssetPath;
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                if (!IsShaderUsable(shader != null, shader != null && shader.isSupported))
                {
                    refusal = TextureCaptureRefusalReason.UnavailableCapture;
                    source = default;
                    return false;
                }

                // Gate 12, last because it depends on the shader and on the device
                // capabilities above. It guards the route it is written beside:
                // there is no version of TryCapture that returns a chain without it.
                if (!HostCapabilityCheckPasses(channel))
                {
                    refusal = TextureCaptureRefusalReason.UnavailableCapture;
                    source = default;
                    return false;
                }

                // Spec section 3: a texel between the noise gate and the
                // shader cutoff is discarded at runtime and must never read
                // opaque, so a shader cutoff source keeps the policy inert
                // on this route, exactly as on the other three. The inert
                // bounds reproduce the base exact-255 contract, which is
                // what the cutoff arm binarizes by here.
                var effectiveBounds = cutoffThreshold < 1f
                    ? AlphaPolicyBounds.Inert
                    : bounds;
                if (!TryCaptureChain(
                        texture2D, shader, effectiveBounds,
                        texture2D.activeMipmapLimit, out chain))
                {
                    refusal = TextureCaptureRefusalReason.UnavailableCapture;
                    source = default;
                    chain = null;
                    return false;
                }


                return true;
            }
            catch (MissingReferenceException)
            {
                // Measured: raised by any member access on a destroyed object,
                // including isReadable, and its base type is SystemException rather
                // than UnityException. Guards every Unity-object read above.
                refusal = TextureCaptureRefusalReason.UnavailableCapture;
                source = default;
                chain = null;
                return false;
            }
        }

        /// <summary>
        /// Captures every declared mip and constructs the chain only after
        /// every resident level succeeded. Levels the effective mipmap limit
        /// removes from the GPU are never blitted: a non-resident source has
        /// no defined readback, so the level keeps a placeholder grid with
        /// its declared dimensions and the chain flags it without evidence,
        /// which degrades the level to Unknown at the fold. A failed level
        /// that should have been resident still refuses the whole texture:
        /// there is no code path on which a partially populated chain of
        /// resident evidence exists, so none can escape. The bounds ride
        /// along to every acquired level.
        /// </summary>
        private static bool TryCaptureChain(
            Texture2D texture,
            Shader shader,
            AlphaPolicyBounds bounds,
            int activeMipmapLimit,
            out AlphaMipChain chain)
        {
            chain = null;
            var levels = new AlphaTextureData[texture.mipmapCount];
            var withoutEvidence = new bool[levels.Length];

            // One material per texture, not per level: Graphics.Blit sets _MainTex
            // on it and only _Mip varies between levels.
            var material = new Material(shader);
            try
            {
                for (var mip = 0; mip < levels.Length; mip++)
                {
                    if (!IsLevelResident(mip, activeMipmapLimit))
                    {
                        levels[mip] = WithoutEvidencePlaceholder(texture, mip);
                        withoutEvidence[mip] = true;
                        continue;
                    }

                    if (!TryAcquireLevel(texture, mip, material, bounds, out var level))
                    {
                        return false;
                    }

                    levels[mip] = level;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }

            chain = new AlphaMipChain(levels, withoutEvidence);
            return true;
        }

        /// <summary>
        /// The placeholder grid of a level the capture could not examine. Its
        /// bytes are all witnesses and prove nothing: the chain flags the
        /// level, and the fold consults the flag before any grid content, so
        /// the placeholder exists only to keep the chain's declared shape.
        /// Internal because the generated capture route skips its
        /// non-resident levels with the same placeholder, and a second
        /// builder would be a second place for the shape rule to drift.
        /// </summary>
        internal static AlphaTextureData WithoutEvidencePlaceholder(
            Texture2D texture,
            int mip)
        {
            var width = Mathf.Max(1, texture.width >> mip);
            var height = Mathf.Max(1, texture.height >> mip);
            return new AlphaTextureData(width, height, new byte[width * height]);
        }

        /// <summary>
        /// Applies the declared residency provenance to a chain a route
        /// captured: the levels the effective limit removes from the GPU have
        /// no effective alpha for playback, so they are flagged without
        /// evidence whatever the route read for them - the routes' own reads
        /// describe importer or GPU content, not what playback samples. The
        /// GPU route flags its skipped levels itself; the flags OR, so
        /// re-wrapping its chain is exact. An unlimited texture is returned
        /// unchanged. Internal, like the other gate predicates, so the test
        /// assembly can exercise the composition whose live Unity state
        /// cannot safely be induced on a conforming host; production is its
        /// caller, so it IS the residency composition rather than a
        /// parallel restatement of it.
        /// </summary>
        internal static AlphaMipChain WithResidencyProvenance(
            AlphaMipChain chain,
            int activeMipmapLimit)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (activeMipmapLimit <= 0)
            {
                return chain;
            }

            var levels = new AlphaTextureData[chain.Count];
            var withoutEvidence = new bool[chain.Count];
            for (var level = 0; level < chain.Count; level++)
            {
                levels[level] = chain[level];
                withoutEvidence[level] =
                    level < activeMipmapLimit ||
                    chain.IsLevelWithoutEvidence(level);
            }

            return new AlphaMipChain(levels, withoutEvidence);
        }

        /// <summary>
        /// The one GPU acquisition core: Blit through the predicate shader into an
        /// exact R8_UNorm target, read the bytes back synchronously, validate, and
        /// build one grid. It writes the policy bounds to the material as
        /// normalized floats, a bound byte over 255, so the shader's verdict
        /// bands are the caller's policy at every level.
        /// <para>
        /// It holds no identity, build-target, format-allowlist, mip-limit,
        /// streaming, or capability gate. Those belong to its callers, and repeating
        /// them here would create a second place for the policy to drift.
        /// </para>
        /// <para>
        /// Its validations are output-integrity checks on a destination this code
        /// allocated. None of them establishes that the requested source level was
        /// resident; that is what the declared-state gates are for.
        /// </para>
        /// </summary>
        private static bool TryAcquireLevel(
            Texture2D texture,
            int mip,
            Material material,
            AlphaPolicyBounds bounds,
            out AlphaTextureData level)
        {
            level = null;

            var width = Mathf.Max(1, texture.width >> mip);
            var height = Mathf.Max(1, texture.height >> mip);

            material.SetInt("_Mip", mip);

            // Normalized floats: a stored byte b samples exactly b/255 on
            // every admitted decode, so the shaders' comparisons order the
            // stored bytes exactly as the bytes order.
            material.SetFloat("_OpaqueBound", bounds.OpaqueBound / 255f);
            material.SetFloat("_NoiseBound", bounds.NoiseBound / 255f);

            var descriptor = new RenderTextureDescriptor(width, height, PredicateTarget, 0)
            {
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };

            var target = RenderTexture.GetTemporary(descriptor);
            try
            {
                if (!IsExpectedTargetFormat(target.graphicsFormat, PredicateTarget) ||
                    !IsExpectedLevelSize(target.width, target.height, width, height))
                {
                    return false;
                }

                var previous = RenderTexture.active;
                try
                {
                    Graphics.Blit(texture, target, material);
                }
                finally
                {
                    RenderTexture.active = previous;
                }

                var request = AsyncGPUReadback.Request(target, 0, PredicateTarget);
                request.WaitForCompletion();
                if (request.hasError ||
                    !IsExpectedLevelSize(request.width, request.height, width, height))
                {
                    return false;
                }

                // The NativeArray is owned by the request and must not outlive it,
                // so the bytes are copied inside this scope. AlphaTextureData takes
                // IReadOnlyList<byte>, which NativeArray does not implement, so the
                // managed copy is forced by the existing type as well.
                var data = request.GetData<byte>();

                // Checked against the length Unity returned, BEFORE allocating,
                // so the mismatch branch is genuinely reachable.
                if (!IsExpectedBufferLength(data.Length, width, height))
                {
                    return false;
                }

                var bytes = new byte[width * height];
                data.CopyTo(bytes);

                if (!IsPredicateFlagBuffer(bytes))
                {
                    return false;
                }

                // The readback is bottom-to-top row-major and so is
                // AlphaTextureData, so the bytes cross with no flip or transpose.
                level = new AlphaTextureData(width, height, bytes);
                return true;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(target);
            }
        }

        /// <summary>
        /// Signature-compatible with <see cref="AlphaFieldProvider"/>; pass it as a
        /// method group. Returns false, with no field, whenever the effective alpha
        /// cannot be proven. A malformed argument throws instead, because silence
        /// would hide a caller defect.
        /// </summary>
        internal bool TryGetAlphaField(
            TextureSourceId source,
            TextureChannel channel,
            out AlphaMipChain chain)
        {
            chain = null;

            if (string.IsNullOrWhiteSpace(source.Value))
            {
                throw new ArgumentException(
                    "Texture source identity must be initialized.",
                    nameof(source));
            }

            if (!Enum.IsDefined(typeof(TextureChannel), channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel));
            }

            // Alpha and Red have producers. The red predicate is decode-proof
            // by the monotone-transfer argument (see TryCapture), so the same
            // lookup serves both; any other channel still fails closed.
            if (!_fieldsBySource.TryGetValue((source, channel), out chain))
            {
                return false;
            }

            return true;
        }

        /// <summary>The predicate target: one byte per texel.</summary>
        private const GraphicsFormat PredicateTarget = GraphicsFormat.R8_UNorm;

        /// <summary>
        /// Process-local host-capability latches, one per predicate channel.
        /// Each records one fact about this Editor process's graphics stack;
        /// each is keyed by nothing and holds no texel, texture, or source
        /// identity. They are explicitly NOT a texture evidence cache and
        /// must never be grown into one. Domain reload clears them.
        /// </summary>
        private static bool? _alphaHostCapabilityPassed;
        private static bool? _redHostCapabilityPassed;

        /// <summary>
        /// 4x2, asymmetric on both axes and not symmetric under transpose, so a
        /// vertical flip, a horizontal mirror, a transpose and a width/height swap
        /// each produce a different buffer from the expected one. Bottom-to-top
        /// row-major, matching AlphaTextureData.
        /// </summary>
        private static readonly byte[] ExpectedOrientationPattern =
        {
            255, 255, 0, 0,
            255, 0, 0, 0
        };

        /// <summary>
        /// The one fixture texel that carries the noise-band byte 3 instead
        /// of 128, for the erased-encoding re-measurement. Under the inert
        /// run it reads 0 like its neighbors, so the orientation pattern is
        /// unchanged.
        /// </summary>
        private const int NoiseFixtureTexel = 7;

        /// <summary>
        /// Gate 12. Row order is soundness-critical - a vertical flip would
        /// attribute alpha to the wrong triangles and could yield a false
        /// ProvenOpaque - and the orientation agreement was measured on one graphics
        /// API only. The active build target says nothing about the Editor's
        /// graphics API, so this converts an unverified cross-API assumption into a
        /// checked precondition on the host that actually runs the build.
        /// <para>
        /// Evaluated lazily, once per Editor AppDomain, after the shader and the
        /// device capabilities it depends on have been confirmed. On failure every
        /// texture-alpha capture refuses for the remainder of the AppDomain: there
        /// is no partial credit and no retry.
        /// </para>
        /// <para>
        /// The inert run proves that this host's production route preserves
        /// the expected orientation and the exact R8 encoding of 255 and 0.
        /// The inert bounds never reach the erased verdict, so a second,
        /// bounds-on acquisition re-measures the erased encoding once per
        /// AppDomain. The claim is narrow: deviations that leave the flag
        /// grid fail the capture, and the erased encoding is pinned by this
        /// gate measurement on the hardware that runs the gate. It does NOT
        /// independently attest the decode or swizzle behaviour of any
        /// compressed format; the fixture is one uncompressed texture.
        /// </para>
        /// <para>
        /// The fixture is built in memory and so has no asset identity, which is why
        /// it calls the acquisition core directly: TryGetSourceId would refuse it at
        /// the identity gate.
        /// </para>
        /// </summary>
        internal static bool HostCapabilityCheckPasses()
        {
            return HostCapabilityCheckPasses(TextureChannel.Alpha);
        }

        internal static bool HostCapabilityCheckPasses(TextureChannel channel)
        {
            if (channel != TextureChannel.Alpha &&
                channel != TextureChannel.Red)
            {
                return false;
            }

            if (channel == TextureChannel.Red)
            {
                if (_redHostCapabilityPassed.HasValue)
                {
                    return _redHostCapabilityPassed.Value;
                }

                _redHostCapabilityPassed = RunHostCapabilityCheck(channel);
                return _redHostCapabilityPassed.Value;
            }

            if (_alphaHostCapabilityPassed.HasValue)
            {
                return _alphaHostCapabilityPassed.Value;
            }

            _alphaHostCapabilityPassed = RunHostCapabilityCheck(channel);
            return _alphaHostCapabilityPassed.Value;
        }

        private static bool RunHostCapabilityCheck(TextureChannel channel)
        {
            var shaderPath = channel == TextureChannel.Red
                ? RedShaderAssetPath
                : ShaderAssetPath;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (!IsShaderUsable(shader != null, shader != null && shader.isSupported))
            {
                return false;
            }


            var texture = new Texture2D(4, 2, TextureFormat.RGBA32, false);
            Material material = null;
            try
            {
                var pixels = new Color32[8];
                for (var index = 0; index < pixels.Length; index++)
                {
                    // The fixture encodes the orientation pattern in the
                    // channel under test: alpha for the alpha predicate,
                    // red for the mask predicate. One unmarked texel
                    // carries the noise-band byte 3 instead of 128. Both
                    // read 0 under the inert bounds, so the orientation
                    // pattern is unchanged, and the bounds-on second run
                    // reads that texel as the erased flag.
                    var channelByte = ExpectedOrientationPattern[index] ==
                                      byte.MaxValue
                        ? (byte)255
                        : index == NoiseFixtureTexel ? (byte)3 : (byte)128;
                    pixels[index] = channel == TextureChannel.Red
                        ? new Color32(channelByte, 32, 16, 255)
                        : new Color32(64, 32, 16, channelByte);
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, false);

                material = new Material(shader);

                // The inert run pins the orientation and the 255 and 0
                // encodings: the fixture's expectations are the base binary
                // output the inert contract guarantees.
                if (!TryAcquireLevel(
                        texture, 0, material, AlphaPolicyBounds.Inert,
                        out var level))
                {
                    return false;
                }

                var actual = new byte[ExpectedOrientationPattern.Length];
                for (var y = 0; y < 2; y++)
                {
                    for (var x = 0; x < 4; x++)
                    {
                        actual[y * 4 + x] = level.GetAlpha(x, y);
                    }
                }

                if (!MatchesExpectedPattern(actual, ExpectedOrientationPattern))
                {
                    return false;
                }

                // The inert run cannot reach the erased verdict: the noise
                // bound 0 never fires. This second, bounds-on acquisition
                // re-measures the erased encoding once per AppDomain: the
                // noise-band texel must store exactly byte 1 through the
                // R8_UNorm write.
                if (!TryAcquireLevel(
                        texture, 0, material, AlphaPolicyBounds.From(80, 2),
                        out var boundsOnLevel))
                {
                    return false;
                }

                return boundsOnLevel.GetAlpha(
                    NoiseFixtureTexel % 4, NoiseFixtureTexel / 4)
                    == AlphaTextureData.ErasedFlag;
            }
            finally
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // --- Gate predicates and output validators ---------------------------
        // Each is called by TryCapture, TryAcquireLevel, or RunHostCapabilityCheck.
        // They are internal so the test assembly can exercise every combination of
        // facts whose Unity state cannot safely be induced on a conforming host;
        // production is their caller, so each predicate IS the gate rather than a
        // parallel restatement of it.

        /// <summary>
        /// The closed format allowlist. Each member is admitted on two grounds:
        /// durable characterization through the R8 predicate path, and an
        /// authoritative decode rule. UNorm decode is n/(2^b - 1), so the result is
        /// structurally finite and within [0, 1]; BC3's alpha block is an exact
        /// integer scheme; BC7 decompression is specified bit-accurate; RGB24 has no
        /// alpha channel, so the sampler returns exactly one.
        /// <para>
        /// Everything else is refused. Float formats cannot supply the
        /// finite-and-[0,1] attestation, because one predicate bit reports the same
        /// 0 for a legitimate below-one value as for 2.0, -1.0, NaN or +Inf.
        /// DXT5Crunched behaves as DXT5 in one earlier measurement but is not
        /// durably exercised. ARGB4444 is exact - its 4-bit quantization of many
        /// authoring values to exactly one is not itself unsafe, because the
        /// imported GPU-decoded representation is what playback samples - but it has
        /// no durable production-shaped characterization. ASTC decodes under a
        /// tolerance rather than bit-exactly.
        /// </para>
        /// </summary>
        internal static bool IsAdmittedFormat(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                case TextureFormat.Alpha8:
                case TextureFormat.RGB24:
                case TextureFormat.DXT5:
                case TextureFormat.BC7:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Deliberately not generalized to "Standalone": the other members of that
        /// group have their own default format tables and were never characterized.
        /// With another target active, the Windows import is not loaded and cannot
        /// be inspected at all.
        /// </summary>
        internal static bool IsAdmittedBuildTarget(BuildTarget target)
        {
            return target == BuildTarget.StandaloneWindows64;
        }

        /// <summary>
        /// A gate on declared state. activeMipmapLimit is the per-texture effective
        /// limit and already folds in the global limit and any mipmap-limit group.
        /// The limit removes exactly the activeMipmapLimit highest-resolution
        /// levels of the declared chain from the GPU, so the gate passes while
        /// at least one level remains resident: the capture then consults the
        /// resident levels and marks the removed prefix without evidence. It
        /// refuses - naming NonResidentMips - only when no level remains
        /// resident, because a capture with no usable level has nothing to
        /// degrade. Streaming is not a limit and is not refused here: a
        /// streaming texture captures through the readable-clone route
        /// because its GPU read is measured untrustworthy, however resident
        /// it reports.
        /// <para>
        /// This is a pure predicate because its false branch cannot be
        /// constructed without mutating project or importer state, which
        /// production must never do.
        /// </para>
        /// </summary>
        internal static bool MipLimitGatesPass(int activeMipmapLimit, int mipmapCount)
        {
            return activeMipmapLimit < mipmapCount;
        }

        /// <summary>
        /// The per-level shape of the same declared-state gate: under the
        /// effective limit, the chain's first activeMipmapLimit levels - the
        /// highest-resolution ones - are not uploaded, so a level is resident
        /// exactly when its index is at or above the limit. Like
        /// <see cref="MipLimitGatesPass"/>, it is deliberately a predicate
        /// over declared state rather than an inference from a readback: a
        /// readback's dimensions are the dimensions of a destination this
        /// code allocated, so they cannot establish that the requested source
        /// level was resident.
        /// </summary>
        internal static bool IsLevelResident(int mipLevel, int activeMipmapLimit)
        {
            return mipLevel >= activeMipmapLimit;
        }

        internal static bool AreDimensionsUsable(int width, int height, int mipmapCount)
        {
            return width > 0 && height > 0 && mipmapCount > 0;
        }

        /// <summary>
        /// Async readback is the whole route's precondition. The exact R8_UNorm
        /// render and readback capabilities are what the destination requires. The
        /// source-sample capability is a different question from the format
        /// allowlist: the allowlist is AMUSE policy over TextureFormat, this asks
        /// whether the source the shader will Load can be sampled - a question
        /// <see cref="SourceSamplingGatePasses"/> answers, since the reported
        /// graphicsFormat is not always the format actually sampled.
        /// </summary>
        internal static bool HostCapabilitiesPass(
            bool asyncReadback, bool r8Renderable, bool r8Readable, bool sourceSampleable)
        {
            return asyncReadback && r8Renderable && r8Readable && sourceSampleable;
        }

        /// <summary>
        /// Whether the source can be sampled by the predicate shader. Every
        /// admitted <em>alpha-bearing</em> format must have exact reported-format
        /// Sample support; <see cref="TextureFormat.RGB24"/> alone is exempt.
        /// <para>
        /// Measured on Metal: <c>IsFormatSupported(R8G8B8_UNorm, Sample)</c> is
        /// False, and R8G8B8_UNorm is what a RGB24 import reports as its
        /// graphicsFormat - yet the production shader route samples RGB24 with
        /// alpha exactly one at 4x4 and 8x8, single-mip and mipmapped. Unity 2022.3
        /// converts RGB24 to RGBA32 at texture load because native RGB24 support is
        /// rare, so the reported storage format is not the format actually sampled.
        /// </para>
        /// <para>
        /// The exemption is deliberately one named format rather than a general
        /// <c>GetCompatibleFormat</c> fallback. A compatible format promises a
        /// supported <em>similar</em> format, not the exact alpha preservation this
        /// evidence contract needs; accepting an uncharacterized alpha-bearing
        /// substitution would weaken the proof. RGB24 is safe precisely because it
        /// carries no alpha channel at all, so the substitution cannot lose alpha
        /// information: the sampler returns exactly one either way.
        /// </para>
        /// <para>
        /// This gate answers only the sampling question. The closed allowlist in
        /// <see cref="IsAdmittedFormat"/> is an independent gate evaluated before
        /// it, so nothing here can admit a refused format.
        /// </para>
        /// <para>
        /// SystemInfo is deliberately not called here: the caller supplies the
        /// measured fact so every combination stays testable.
        /// </para>
        /// </summary>
        internal static bool SourceSamplingGatePasses(
            TextureFormat textureFormat,
            bool exactGraphicsFormatSampleable)
        {
            if (exactGraphicsFormatSampleable)
            {
                return true;
            }

            return textureFormat == TextureFormat.RGB24;
        }

        internal static bool IsShaderUsable(bool assetLoaded, bool isSupported)
        {
            return assetLoaded && isSupported;
        }

        internal static bool IsExpectedLevelSize(
            int width, int height, int expectedWidth, int expectedHeight)
        {
            return width == expectedWidth && height == expectedHeight;
        }

        /// <summary>
        /// Unity may substitute a format it prefers for a temporary target. A
        /// substituted target would silently change what the readback means, so an
        /// inexact match is a refusal rather than something to tolerate.
        /// </summary>
        internal static bool IsExpectedTargetFormat(
            GraphicsFormat actual, GraphicsFormat expected)
        {
            return actual == expected;
        }

        /// <summary>
        /// Compares the length Unity actually returned against the destination this
        /// code requested. It must be called with the readback's own length, before
        /// any managed array is allocated: allocating an array of the expected size
        /// and then passing its own Length would make this branch unreachable.
        /// <para>
        /// The product is computed in long so the comparison stays correct for the
        /// largest textures Unity imports.
        /// </para>
        /// </summary>
        internal static bool IsExpectedBufferLength(
            long actualLength, int width, int height)
        {
            return actualLength == (long)width * height;
        }

        /// <summary>
        /// One responsibility: every byte is one of the three flag states the
        /// predicate shaders emit, which an R8_UNorm target stores exactly.
        /// 255 is the opaque verdict, AlphaTextureData.ErasedFlag is the
        /// erased verdict, and 0 is the witness. Anything else means the value
        /// was filtered, rescaled, or transfer-converted on the way out, and
        /// the predicate would no longer be the predicate. Length is
        /// <see cref="IsExpectedBufferLength"/>'s job, checked earlier and
        /// against the length Unity returned.
        /// </summary>
        internal static bool IsPredicateFlagBuffer(byte[] bytes)
        {
            if (bytes == null)
            {
                return false;
            }

            foreach (var value in bytes)
            {
                if (value != 0 &&
                    value != AlphaTextureData.ErasedFlag &&
                    value != byte.MaxValue)
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool MatchesExpectedPattern(byte[] actual, byte[] expected)
        {
            if (actual == null || expected == null || actual.Length != expected.Length)
            {
                return false;
            }

            for (var index = 0; index < expected.Length; index++)
            {
                if (actual[index] != expected[index])
                {
                    return false;
                }
            }

            return true;
        }

    }
}
