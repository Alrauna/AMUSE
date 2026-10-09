using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// The outcome of interpreting the lilToon alpha mask equation against
    /// one material's captured evidence.
    /// </summary>
    internal enum LilToonAlphaMaskTermKind
    {
        /// <summary>Mode 0: the mask never runs and the alpha is untouched.</summary>
        Off,

        /// <summary>
        /// The mask term is provably exactly one, so the alpha value is the
        /// plain main-term value the family already builds.
        /// </summary>
        MainUnchanged,

        /// <summary>
        /// Replace mode with a constant term: the alpha value is that
        /// constant, independent of every texture.
        /// </summary>
        Constant,

        /// <summary>
        /// The term is the sampled red channel at the mask's own affine of
        /// UV0, composed per the family's mode.
        /// </summary>
        Sample,

        /// <summary>
        /// Modes 1 and 2 over a general finite pair: the term is the exact
        /// affine map of the sampled red.
        /// </summary>
        MappedSample,

        /// <summary>One diagnostic names the offending property.</summary>
        Refused,
    }

    /// <summary>
    /// The lilToon alpha mask interpretation shared by the cutout and
    /// transparent frontends, pinned to lilToon 2.3.4
    /// (<c>lil_common_frag.hlsl:458-476</c>):
    /// <code>
    /// if(_AlphaMaskMode)
    /// {
    ///     alphaMask = _AlphaMask sampled through sampler_MainTex at
    ///                uvMain * _AlphaMask_ST.xy + _AlphaMask_ST.zw, .r;
    ///     alphaMask = saturate(alphaMask * _AlphaMaskScale + _AlphaMaskValue);
    ///     mode 1: col.a = alphaMask;              // Replace
    ///     mode 2: col.a = col.a * alphaMask;      // Multiply
    /// }
    /// </code>
    /// <para>
    /// Modes 3 and 4 saturate a sum or difference and always refuse. The
    /// admitted (scale, value) pairs are exactly the ones whose binary32
    /// arithmetic carries a proof without a texel threshold: the vendor
    /// default (1, 0), where the term is the sampled red alone; the provably
    /// saturated (1, value &gt;= 1), where r·1 + v rounds to at least one for
    /// every r &gt;= 0 under fused and unfused rounding alike; the unassigned
    /// mask, whose declared "white" default samples exactly one so the term
    /// is the constant saturate(scale + value); the zero scale (0, value),
    /// where 0·s rounds to exactly zero and zero + value rounds to exactly
    /// value under both orders, so the term is the constant
    /// saturate(value); and every other finite pair, whose term is monotone
    /// nondecreasing in the sampled red because round-to-nearest-half-even
    /// and saturate are monotone, so <c>AffineAlphaMap</c> bounds it on any
    /// red interval by the map's endpoint evaluations with no texel
    /// threshold.
    /// </para>
    /// <para>
    /// The mask coordinate is <c>uvMain</c> transformed by the mask's own
    /// <c>_ST</c>. Both frontends gate <c>_MainTex_ST</c> and
    /// <c>_MainTex_ScrollRotate</c> to exact identity, so <c>uvMain</c> is
    /// UV0 and the mask coordinate is a plain affine — no identity gate,
    /// unlike the rotation path that forces the main-ST family boundary.
    /// </para>
    /// </summary>
    internal readonly struct LilToonAlphaMaskTerm
    {
        internal const string ModeProperty = "_AlphaMaskMode";
        internal const string ScaleProperty = "_AlphaMaskScale";
        internal const string ValueProperty = "_AlphaMaskValue";
        internal const string MaskProperty = "_AlphaMask";

        internal LilToonAlphaMaskTermKind Kind { get; }
        internal float Constant { get; }
        internal TextureSourceId Source { get; }
        internal UvMapping Mapping { get; }
        internal float Scale { get; }
        internal float Value { get; }
        internal bool ReplacesMainAlpha { get; }
        internal LilToonSemanticDiagnosticCode RefusalCode { get; }
        internal string RefusalDetail { get; }

        private LilToonAlphaMaskTerm(
            LilToonAlphaMaskTermKind kind,
            float constant,
            TextureSourceId source,
            UvMapping mapping,
            float scale,
            float value,
            bool replacesMainAlpha,
            LilToonSemanticDiagnosticCode refusalCode,
            string refusalDetail)
        {
            Kind = kind;
            Constant = constant;
            Source = source;
            Mapping = mapping;
            Scale = scale;
            Value = value;
            ReplacesMainAlpha = replacesMainAlpha;
            RefusalCode = refusalCode;
            RefusalDetail = refusalDetail;
        }

        internal static LilToonAlphaMaskTerm Off()
        {
            return new LilToonAlphaMaskTerm(
                LilToonAlphaMaskTermKind.Off,
                default, default, default, default, default, false,
                default, null);
        }

        internal static LilToonAlphaMaskTerm MainUnchanged()
        {
            return new LilToonAlphaMaskTerm(
                LilToonAlphaMaskTermKind.MainUnchanged,
                default, default, default, default, default, false,
                default, null);
        }

        internal static LilToonAlphaMaskTerm ConstantTerm(float value)
        {
            return new LilToonAlphaMaskTerm(
                LilToonAlphaMaskTermKind.Constant,
                value, default, default, default, default, true,
                default, null);
        }

        internal static LilToonAlphaMaskTerm SampleOf(
            TextureSourceId source,
            UvMapping mapping,
            bool replacesMainAlpha)
        {
            return new LilToonAlphaMaskTerm(
                LilToonAlphaMaskTermKind.Sample,
                default, source, mapping, default, default,
                replacesMainAlpha, default, null);
        }

        internal static LilToonAlphaMaskTerm MappedSampleOf(
            TextureSourceId source,
            UvMapping mapping,
            float scale,
            float value,
            bool replacesMainAlpha)
        {
            return new LilToonAlphaMaskTerm(
                LilToonAlphaMaskTermKind.MappedSample,
                default, source, mapping, scale, value,
                replacesMainAlpha, default, null);
        }

        internal static LilToonAlphaMaskTerm Refused(
            LilToonSemanticDiagnosticCode code,
            string detail)
        {
            return new LilToonAlphaMaskTerm(
                LilToonAlphaMaskTermKind.Refused,
                default, default, default, default, default, false,
                code, detail);
        }

        /// <summary>
        /// Interprets the mask equation. A refusal appends exactly one
        /// Alpha diagnostic; every other outcome records none and leaves
        /// the family's own gates in charge.
        /// </summary>
        internal static LilToonAlphaMaskTerm Interpret(
            CapturedMaterialEvidence evidence,
            List<LilToonSemanticDiagnostic> diagnostics)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            if (!evidence.TryGetScalar(ModeProperty, out var mode) ||
                !IsFinite(mode))
            {
                return Refuse(
                    diagnostics,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    ModeProperty);
            }

            if (mode == 0f)
            {
                return Off();
            }

            if (mode != 1f && mode != 2f)
            {
                return Refuse(
                    diagnostics,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    ModeProperty);
            }

            if (!evidence.TryGetScalar(ScaleProperty, out var scale) ||
                !IsFinite(scale))
            {
                return Refuse(
                    diagnostics,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    ScaleProperty);
            }

            if (!evidence.TryGetScalar(ValueProperty, out var value) ||
                !IsFinite(value))
            {
                return Refuse(
                    diagnostics,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    ValueProperty);
            }

            if (!evidence.TryGetTexture(
                    MaskProperty, out var assignment))
            {
                return Refuse(
                    diagnostics,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    MaskProperty);
            }

            if (!assignment.IsAssigned)
            {
                // The declared default is "white": the sampled red is
                // exactly one at every level, so the term is the constant
                // saturate(1 * scale + value), computed in binary32 with
                // the same operands the shader adds.
                var term = Mathf.Clamp01(scale + value);
                if (mode == 1f)
                {
                    return ConstantTerm(term);
                }

                return term >= 1f
                    ? MainUnchanged()
                    : Refuse(
                        diagnostics,
                        LilToonSemanticDiagnosticCode.UnsupportedFeature,
                        ScaleProperty);
            }

            if (scale == 1f && value >= 1f)
            {
                // r * 1 + v >= v >= 1 in real arithmetic; both fused and
                // unfused binary32 rounding keep a value at or above one,
                // so saturate is exactly one for every sampled r >= 0.
                return mode == 1f
                    ? ConstantTerm(1f)
                    : MainUnchanged();
            }

            // Every admitted shape below samples the mask (or is a constant
            // derived without it), and each sample shape rides the mask's
            // own source identity and plain affine, so both requirements
            // gate all of them together.
            if (!assignment.Texture.HasSourceIdentity)
            {
                return Refuse(
                    diagnostics,
                    LilToonSemanticDiagnosticCode
                        .UnstableTextureIdentity,
                    MaskProperty);
            }

            if (!assignment.HasScaleOffset ||
                !float.IsFinite(assignment.Scale.x) ||
                !float.IsFinite(assignment.Scale.y) ||
                !float.IsFinite(assignment.Offset.x) ||
                !float.IsFinite(assignment.Offset.y))
            {
                return Refuse(
                    diagnostics,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    MaskProperty);
            }

            if (scale == 1f && value == 0f)
            {
                // The coordinate is uvMain under the families' identity
                // gates, transformed by the mask's own plain affine.
                return SampleOf(
                    assignment.Texture.SourceIdentity,
                    new UvMapping(0, assignment.Scale, assignment.Offset),
                    mode == 1f);
            }

            if (scale == 0f)
            {
                // 0 * s is exactly zero and zero + v is exactly v in
                // binary32, so the term is saturate(v) under both orders
                // with the same operands the shader adds.
                var constant = Mathf.Clamp01(value);
                if (mode == 1f)
                {
                    return ConstantTerm(constant);
                }

                return constant >= 1f
                    ? MainUnchanged()
                    : Refuse(
                        diagnostics,
                        LilToonSemanticDiagnosticCode.UnsupportedFeature,
                        ScaleProperty);
            }

            return MappedSampleOf(
                assignment.Texture.SourceIdentity,
                new UvMapping(0, assignment.Scale, assignment.Offset),
                scale,
                value,
                mode == 1f);
        }

        private static LilToonAlphaMaskTerm Refuse(
            List<LilToonSemanticDiagnostic> diagnostics,
            LilToonSemanticDiagnosticCode code,
            string detail)
        {
            diagnostics.Add(
                new LilToonSemanticDiagnostic(
                    LilToonSemanticOutput.Alpha, code, detail));
            return Refused(code, detail);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
