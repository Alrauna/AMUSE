using System;
using Alrauna.Amuse.Editor.Host;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Research.Tests.Editor.Calibration
{
    /// <summary>
    /// Whether this machine can answer a characterization question at all, as a
    /// value. Following <see cref="CensusVendorProbe"/>: an unsupported format
    /// is a reported state, never an exception and never <c>Assert.Ignore</c>,
    /// because a skipped case reports as a pass and a silently unreachable
    /// characterization is worse than none.
    /// </summary>
    internal sealed class AlphaProbeSupport
    {
        internal bool ShaderAvailable { get; }
        internal bool PredicateTargetRenderable { get; }
        internal bool PredicateTargetReadable { get; }

        /// <summary>
        /// Only for the raw-magnitude diagnostic, which is not the production
        /// route. Its absence does not make the production-shaped path
        /// unusable.
        /// </summary>
        internal bool DiagnosticTargetAvailable { get; }

        internal bool IsUsable =>
            ShaderAvailable && PredicateTargetRenderable && PredicateTargetReadable;

        internal AlphaProbeSupport(
            bool shaderAvailable,
            bool predicateTargetRenderable,
            bool predicateTargetReadable,
            bool diagnosticTargetAvailable)
        {
            ShaderAvailable = shaderAvailable;
            PredicateTargetRenderable = predicateTargetRenderable;
            PredicateTargetReadable = predicateTargetReadable;
            DiagnosticTargetAvailable = diagnosticTargetAvailable;
        }

        internal string Describe()
        {
            return "shader=" + ShaderAvailable
                + " R8_UNorm(Render)=" + PredicateTargetRenderable
                + " R8_UNorm(ReadPixels)=" + PredicateTargetReadable
                + " diagnosticFloatTarget=" + DiagnosticTargetAvailable;
        }
    }

    /// <summary>
    /// One captured mip level of the production-shaped route: the destination
    /// size that was requested, and one boolean per texel decoded from the R8
    /// predicate target. No magnitudes - the production route never reads one.
    /// </summary>
    internal sealed class AlphaProbeLevel
    {
        internal int Mip { get; }
        internal int Width { get; }
        internal int Height { get; }

        /// <summary>
        /// "Alpha is exactly one", bottom-to-top row-major, decoded from the
        /// R8 target.
        /// </summary>
        internal bool[] ExactlyOne { get; }

        internal AlphaProbeLevel(int mip, int width, int height, bool[] exactlyOne)
        {
            Mip = mip;
            Width = width;
            Height = height;
            ExactlyOne = exactlyOne;
        }

        internal bool ExactlyOneAt(int x, int y) => ExactlyOne[y * Width + x];

        internal bool AllExactlyOne()
        {
            foreach (var one in ExactlyOne)
            {
                if (!one) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Minimal GPU characterization of the alpha evidence predicate, shaped
    /// like the route proposed for production: one shader, one explicit mip,
    /// an <see cref="GraphicsFormat.R8_UNorm"/> predicate target, one byte per
    /// texel.
    /// <para>
    /// It is a research probe, not production code: every fixture is built in
    /// memory, and it opens no importer and writes no asset. It deliberately
    /// loads the <em>product</em> shader through
    /// <see cref="UnityAlphaFieldEvidence.ShaderAssetPath"/>, so the
    /// characterization and production exercise one asset and the predicate
    /// cannot drift between them.
    /// </para>
    /// </summary>
    internal static class AlphaEvidenceProbe
    {
        /// <summary>The production-shaped predicate target: one byte per texel.</summary>
        internal const GraphicsFormat PredicateTarget = GraphicsFormat.R8_UNorm;

        /// <summary>Diagnostic only. Never the production route.</summary>
        private const GraphicsFormat DiagnosticTarget =
            GraphicsFormat.R32G32B32A32_SFloat;

        internal static AlphaProbeSupport ProbeSupport()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(UnityAlphaFieldEvidence.ShaderAssetPath);
            return new AlphaProbeSupport(
                shader != null && shader.isSupported,
                SystemInfo.IsFormatSupported(PredicateTarget, FormatUsage.Render),
                SystemInfo.IsFormatSupported(PredicateTarget, FormatUsage.ReadPixels),
                SystemInfo.IsFormatSupported(DiagnosticTarget, FormatUsage.Render) &&
                SystemInfo.IsFormatSupported(DiagnosticTarget, FormatUsage.ReadPixels));
        }

        /// <summary>
        /// The mip-residency gate, as a pure predicate over the two facts that
        /// decide it, so every combination is testable without provoking
        /// non-residency on the host.
        /// <para>
        /// It is deliberately a gate on declared state rather than an inference
        /// from a readback. A readback's dimensions are the dimensions of a
        /// destination this code allocated, so they cannot establish that the
        /// requested <em>source</em> level was resident, nor that
        /// <c>Texture2D.Load</c> did not substitute or return default data.
        /// </para>
        /// <para>
        /// Streaming is refused outright for the initial scope rather than
        /// handled; supporting it needs its own characterization.
        /// </para>
        /// </summary>
        internal static bool MipResidencyGatesPass(
            int activeMipmapLimit, bool streamingMipmaps)
        {
            return activeMipmapLimit == 0 && !streamingMipmaps;
        }

        internal static bool MipResidencyGatesPass(Texture2D texture)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            return MipResidencyGatesPass(
                texture.activeMipmapLimit, texture.streamingMipmaps);
        }

        /// <summary>
        /// Size of one mip level. Each axis halves independently and clamps at
        /// one, which is what makes a non-square chain a distinct case.
        /// </summary>
        internal static void ExpectedSize(
            Texture2D texture, int mip, out int width, out int height)
        {
            width = Mathf.Max(1, texture.width >> mip);
            height = Mathf.Max(1, texture.height >> mip);
        }

        /// <summary>
        /// Extracts one mip through the production-shaped path. Returns null
        /// when the level cannot be established - an unavailable level is a
        /// refusal, never a partially populated result.
        /// </summary>
        internal static AlphaProbeLevel TryCaptureLevel(Texture2D texture, int mip)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (mip < 0 || mip >= texture.mipmapCount) return null;
            if (!MipResidencyGatesPass(texture)) return null;

            var support = ProbeSupport();
            if (!support.IsUsable) return null;

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(UnityAlphaFieldEvidence.ShaderAssetPath);
            if (shader == null || !shader.isSupported) return null;

            ExpectedSize(texture, mip, out var width, out var height);

            var material = new Material(shader);
            RenderTexture target = null;
            try
            {
                material.SetInt("_Mip", mip);

                var descriptor =
                    new RenderTextureDescriptor(width, height, PredicateTarget, 0);
                descriptor.sRGB = false;
                descriptor.useMipMap = false;
                descriptor.autoGenerateMips = false;

                target = RenderTexture.GetTemporary(descriptor);

                // Unity may substitute a format it prefers. A substituted
                // target would silently change what the readback means, so an
                // inexact match is a refusal rather than something to tolerate.
                if (target.graphicsFormat != PredicateTarget) return null;
                if (target.width != width || target.height != height) return null;

                var previous = RenderTexture.active;
                try
                {
                    Graphics.Blit(texture, target, material);
                }
                finally
                {
                    RenderTexture.active = previous;
                }

                var request =
                    AsyncGPUReadback.Request(target, 0, PredicateTarget);
                request.WaitForCompletion();
                if (request.hasError) return null;

                // Output integrity, not source residency: the bytes must
                // describe the destination this code allocated.
                if (request.width != width || request.height != height) return null;

                var data = request.GetData<byte>();
                if (data.Length != width * height) return null;

                var exactlyOne = new bool[data.Length];
                for (var index = 0; index < data.Length; index++)
                {
                    var value = data[index];

                    // The shader emits only 0 or 1, which an R8_UNorm target
                    // stores as 0 or 255. Anything between means the value was
                    // filtered, rescaled, or transfer-converted on the way out,
                    // and the predicate would no longer be the predicate.
                    if (value != 0 && value != byte.MaxValue) return null;

                    exactlyOne[index] = value == byte.MaxValue;
                }

                return new AlphaProbeLevel(mip, width, height, exactlyOne);
            }
            finally
            {
                if (target != null) RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        /// <summary>
        /// Diagnostic, not the production route. It exists for the single
        /// characterization that genuinely needs magnitudes: showing that a
        /// float field can hold values outside [0, 1] which the one-bit
        /// predicate cannot distinguish from an ordinary below-one texel.
        /// Production never reads a magnitude, and no other case here may use
        /// this.
        /// </summary>
        internal static float[] TryCaptureRawAlphaDiagnostic(
            Texture2D texture, int mip)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (mip < 0 || mip >= texture.mipmapCount) return null;

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(UnityAlphaFieldEvidence.ShaderAssetPath);
            if (shader == null || !shader.isSupported) return null;

            ExpectedSize(texture, mip, out var width, out var height);

            var material = new Material(shader);
            RenderTexture target = null;
            try
            {
                material.SetInt("_Mip", mip);

                var descriptor =
                    new RenderTextureDescriptor(width, height, DiagnosticTarget, 0);
                descriptor.sRGB = false;
                descriptor.useMipMap = false;
                descriptor.autoGenerateMips = false;

                target = RenderTexture.GetTemporary(descriptor);
                var previous = RenderTexture.active;
                try
                {
                    Graphics.Blit(texture, target, material);
                }
                finally
                {
                    RenderTexture.active = previous;
                }

                var request =
                    AsyncGPUReadback.Request(target, 0, DiagnosticTarget);
                request.WaitForCompletion();
                if (request.hasError) return null;

                var data = request.GetData<Color>();
                if (data.Length != width * height) return null;

                var alpha = new float[data.Length];
                for (var index = 0; index < data.Length; index++)
                {
                    alpha[index] = data[index].g;
                }
                return alpha;
            }
            finally
            {
                if (target != null) RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        /// <summary>
        /// The independent oracle: a direct readback of the texture's own mip,
        /// with no shader in the path. Supported only for uncompressed
        /// formats, which is why it cannot replace the shader route. Returns
        /// null when the platform refuses the read.
        /// </summary>
        internal static float[] TryDirectReadback(Texture2D texture, int mip)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (mip < 0 || mip >= texture.mipmapCount) return null;

            var request = AsyncGPUReadback.Request(
                texture, mip, DiagnosticTarget);
            request.WaitForCompletion();
            if (request.hasError) return null;

            ExpectedSize(texture, mip, out var width, out var height);
            if (request.width != width || request.height != height) return null;

            var data = request.GetData<Color>();
            if (data.Length != width * height) return null;

            var alpha = new float[data.Length];
            for (var index = 0; index < data.Length; index++)
            {
                alpha[index] = data[index].a;
            }
            return alpha;
        }
    }
}
