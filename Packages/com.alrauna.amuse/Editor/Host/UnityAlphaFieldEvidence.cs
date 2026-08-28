using System;
using System.Collections.Generic;
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
    /// memory: at each level, byte 255 marks exactly the texels whose sampled alpha
    /// is exactly one, and every other byte marks a value strictly below one. That
    /// is the contract <see cref="AlphaFieldProvider"/> states and the only property
    /// <see cref="TriangleAlphaClassifier"/> reads. The chain is the texture's
    /// complete declared mip chain, mip 0 first: the hardware may select any level
    /// and the resolver cannot know which.
    /// </para>
    /// <para>
    /// The values come from the <em>GPU-decoded imported representation</em>, read
    /// back through the predicate shader, rather than from any CPU view of the
    /// asset. Admission is controlled by the closed characterized allowlist in
    /// <see cref="IsAdmittedFormat"/>: a format is admitted only where durable
    /// characterization through this route and an authoritative decode rule both
    /// support it.
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

        private readonly Dictionary<TextureSourceId, AlphaMipChain> _fieldsBySource;

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
        internal UnityAlphaFieldEvidence(IEnumerable<Texture> textures)
        {
            if (textures == null)
            {
                throw new ArgumentNullException(nameof(textures));
            }

            _fieldsBySource = new Dictionary<TextureSourceId, AlphaMipChain>();
            foreach (var texture in textures)
            {
                if (!TryCapture(texture, out var source, out var chain))
                {
                    continue;
                }

                // Two textures resolving to one identity are the same asset, so the
                // first wins and the duplicate is not an error.
                if (_fieldsBySource.ContainsKey(source))
                {
                    continue;
                }

                _fieldsBySource.Add(source, chain);
            }
        }

        /// <summary>
        /// Captures the complete immutable alpha field for one supported texture.
        /// Every validation predicate belongs here so lookup never needs to touch a
        /// Unity object after construction.
        /// </summary>
        internal static bool TryCapture(
            Texture texture,
            out TextureSourceId source,
            out AlphaMipChain chain)
        {
            source = default;
            chain = null;

            // Unity's overloaded equality is required: it is true for a destroyed
            // object, where ReferenceEquals would be false. A non-Texture2D
            // (RenderTexture, Cubemap, array, 3D) is skipped for the same reason it
            // was refused at lookup.
            var texture2D = texture as Texture2D;
            if (texture2D == null)
            {
                return false;
            }

            if (!UnityTextureEvidence.TryGetSourceId(texture2D, out source))
            {
                return false;
            }

            try
            {
                // Every policy and capability gate precedes the first allocation.
                // The format allowlist in particular is checked before any GPU call,
                // so a compressed source never reaches a route that would log a
                // Unity error.
                if (!IsAdmittedBuildTarget(EditorUserBuildSettings.activeBuildTarget) ||
                    !IsAdmittedFormat(texture2D.format) ||
                    !MipResidencyGatesPass(
                        texture2D.activeMipmapLimit, texture2D.streamingMipmaps) ||
                    !AreDimensionsUsable(
                        texture2D.width, texture2D.height, texture2D.mipmapCount) ||
                    !HostCapabilitiesPass(
                        SystemInfo.supportsAsyncGPUReadback,
                        SystemInfo.IsFormatSupported(PredicateTarget, FormatUsage.Render),
                        SystemInfo.IsFormatSupported(PredicateTarget, FormatUsage.ReadPixels),
                        SourceSamplingGatePasses(
                            texture2D.format,
                            SystemInfo.IsFormatSupported(
                                texture2D.graphicsFormat, FormatUsage.Sample))))
                {
                    source = default;
                    return false;
                }

                var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
                if (!IsShaderUsable(shader != null, shader != null && shader.isSupported))
                {
                    source = default;
                    return false;
                }

                // Gate 12, last because it depends on the shader and on the device
                // capabilities above. It guards the route it is written beside:
                // there is no version of TryCapture that returns a chain without it.
                if (!HostCapabilityCheckPasses())
                {
                    source = default;
                    return false;
                }

                if (!TryCaptureChain(texture2D, shader, out chain))
                {
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
                source = default;
                chain = null;
                return false;
            }
        }

        /// <summary>
        /// Captures every declared mip and constructs the chain only after exactly
        /// mipmapCount successes. A single failed level refuses the whole texture:
        /// there is no code path on which a partially populated chain exists, so
        /// none can escape.
        /// </summary>
        private static bool TryCaptureChain(
            Texture2D texture, Shader shader, out AlphaMipChain chain)
        {
            chain = null;
            var levels = new AlphaTextureData[texture.mipmapCount];

            // One material per texture, not per level: Graphics.Blit sets _MainTex
            // on it and only _Mip varies between levels.
            var material = new Material(shader);
            try
            {
                for (var mip = 0; mip < levels.Length; mip++)
                {
                    if (!TryAcquireLevel(texture, mip, material, out var level))
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

            chain = new AlphaMipChain(levels);
            return true;
        }

        /// <summary>
        /// The one GPU acquisition core: Blit through the predicate shader into an
        /// exact R8_UNorm target, read the bytes back synchronously, validate, and
        /// build one grid.
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
            Texture2D texture, int mip, Material material, out AlphaTextureData level)
        {
            level = null;

            var width = Mathf.Max(1, texture.width >> mip);
            var height = Mathf.Max(1, texture.height >> mip);

            material.SetInt("_Mip", mip);

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

                if (!IsBinaryPredicateBuffer(bytes))
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

            // Only Alpha has a producer today. A colour channel would additionally
            // need the sRGB transfer argument written down, so it fails closed.
            if (channel != TextureChannel.Alpha)
            {
                return false;
            }

            if (!_fieldsBySource.TryGetValue(source, out chain))
            {
                return false;
            }

            return true;
        }

        /// <summary>The predicate target: one byte per texel.</summary>
        private const GraphicsFormat PredicateTarget = GraphicsFormat.R8_UNorm;

        /// <summary>
        /// Process-local host-capability latch. It records one fact about this
        /// Editor process's graphics stack; it is keyed by nothing and holds no
        /// texel, texture, or source identity. It is explicitly NOT a texture
        /// evidence cache and must never be grown into one. Domain reload clears it.
        /// </summary>
        private static bool? _hostCapabilityPassed;

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
        /// It proves that this host's production route preserves the expected
        /// orientation and binary R8 encoding. It does NOT independently attest the
        /// decode or swizzle behaviour of any compressed format; the fixture is one
        /// uncompressed texture.
        /// </para>
        /// <para>
        /// The fixture is built in memory and so has no asset identity, which is why
        /// it calls the acquisition core directly: TryGetSourceId would refuse it at
        /// the identity gate.
        /// </para>
        /// </summary>
        internal static bool HostCapabilityCheckPasses()
        {
            if (_hostCapabilityPassed.HasValue)
            {
                return _hostCapabilityPassed.Value;
            }

            _hostCapabilityPassed = RunHostCapabilityCheck();
            return _hostCapabilityPassed.Value;
        }

        private static bool RunHostCapabilityCheck()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
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
                    pixels[index] = new Color32(
                        64, 32, 16,
                        ExpectedOrientationPattern[index] == byte.MaxValue
                            ? (byte)255
                            : (byte)128);
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, false);

                material = new Material(shader);
                if (!TryAcquireLevel(texture, 0, material, out var level))
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

                return MatchesExpectedPattern(actual, ExpectedOrientationPattern);
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
        /// Streaming is refused outright rather than handled: what a Load of a
        /// non-resident level returns has never been observed.
        /// <para>
        /// This is a pure predicate because its false branches cannot be constructed
        /// without mutating project or importer state, which production must never
        /// do.
        /// </para>
        /// </summary>
        internal static bool MipResidencyGatesPass(
            int activeMipmapLimit, bool streamingMipmaps)
        {
            return activeMipmapLimit == 0 && !streamingMipmaps;
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
        /// One responsibility: the shader emits only 0 or 1, which an R8_UNorm
        /// target stores as 0 or 255. Anything between means the value was
        /// filtered, rescaled, or transfer-converted on the way out, and the
        /// predicate would no longer be the predicate. Length is
        /// <see cref="IsExpectedBufferLength"/>'s job.
        /// </summary>
        internal static bool IsBinaryPredicateBuffer(byte[] bytes)
        {
            if (bytes == null)
            {
                return false;
            }

            foreach (var value in bytes)
            {
                if (value != 0 && value != byte.MaxValue)
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
