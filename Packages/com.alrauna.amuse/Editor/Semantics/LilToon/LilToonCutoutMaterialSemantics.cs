using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// Alpha semantics for the attested regular no-outline lilToon 2.3.4
    /// cutout source (<c>Hidden/lilToonCutout</c>, LIL_RENDER 1), per the
    /// cutout-to-opaque conversion design (spec §8). The restricted theorem:
    /// with every optional alpha/coverage feature proven off, the coverage of
    /// a triangle is the cutout transform of the plain <c>_MainTex</c> alpha
    /// sample at identity UV0 times <c>_Color.a</c>, refused above
    /// <see cref="MaxProvableCutoff"/>. Everything below the interpretation is
    /// the shared <c>AlphaSemanticsResolver</c>; this type only decides
    /// whether the normalized value shape is representable. Behavior the
    /// captured evidence cannot prove stays <c>Unknown</c> with one
    /// deterministic diagnostic naming the offending property; it is never
    /// guessed.
    /// </summary>
    internal static class LilToonCutoutMaterialSemantics
    {
        private const string InvisibleProperty = "_Invisible";
        private const string UdimDiscardCompileProperty = "_UDIMDiscardCompile";
        private const string UdimDiscardModeProperty = "_UDIMDiscardMode";
        private const string ShiftBackfaceUvProperty = "_ShiftBackfaceUV";
        private const string UseParallaxProperty = "_UseParallax";
        private const string UseMain2ndTexProperty = "_UseMain2ndTex";
        private const string UseMain3rdTexProperty = "_UseMain3rdTex";
        private const string AlphaMaskModeProperty = "_AlphaMaskMode";
        private const string UseDitherProperty = "_UseDither";
        private const string IdMask1Property = "_IDMask1";
        private const string IdMask2Property = "_IDMask2";
        private const string IdMask3Property = "_IDMask3";
        private const string IdMask4Property = "_IDMask4";
        private const string IdMask5Property = "_IDMask5";
        private const string IdMask6Property = "_IDMask6";
        private const string IdMask7Property = "_IDMask7";
        private const string IdMask8Property = "_IDMask8";
        private const string IdMaskControlsDissolveProperty =
            "_IDMaskControlsDissolve";
        private const string CutoffProperty = "_Cutoff";
        private const string ColorProperty = "_Color";
        private const string MainTextureProperty = "_MainTex";
        private const string MainTexStProperty = "_MainTex_ST";
        private const string DissolveParamsProperty = "_DissolveParams";
        private const string MainTexScrollRotateProperty = "_MainTex_ScrollRotate";

        /// <summary>
        /// The controller-fixed twice-margin cutoff bound (spec §8.1 clause 2
        /// and §9.3 gate 12; B2 §3.4). At or below it the shadow and forward
        /// clip <c>1 - c</c> keeps every fully covered fragment; above it no
        /// triangle is provable, so the classification layer refuses before
        /// any triangle is called proven. A non-finite cutoff fails the
        /// finite check first.
        /// </summary>
        private const float MaxProvableCutoff = 0.9999f;

        /// <summary>
        /// Every runtime gate that can change cutout coverage. With any of
        /// these active the coverage is not the plain cutout transform of the
        /// main alpha sample (spec §8.1 clause 2), so the interpretation
        /// refuses. <see cref="IdMaskControlsDissolveProperty"/> is the
        /// adversarial-review gate: with it set, the vertex IDMask path can
        /// force the sampled alpha chain to zero even at dissolve mode zero
        /// (B2 §3.3.8, §5 clause 2). The gates are runtime captured facts —
        /// never the compiled feature set.
        /// </summary>
        private static readonly string[] AlphaCoverageGates =
        {
            InvisibleProperty,
            UdimDiscardCompileProperty,
            UdimDiscardModeProperty,
            ShiftBackfaceUvProperty,
            UseParallaxProperty,
            UseMain2ndTexProperty,
            UseMain3rdTexProperty,
            AlphaMaskModeProperty,
            UseDitherProperty,
            IdMask1Property,
            IdMask2Property,
            IdMask3Property,
            IdMask4Property,
            IdMask5Property,
            IdMask6Property,
            IdMask7Property,
            IdMask8Property,
            IdMaskControlsDissolveProperty,
        };
        /// <summary>
        /// The cutout alpha evidence request (spec §8.2). Exact by contract:
        /// no fewer and no more. <c>_IDMaskPrior8</c> is deliberately absent
        /// (it is a fixture-only vendor prior byte, not an AMUSE-proof fact),
        /// and <c>_MainTex_ST</c> is deliberately not a vector request — it
        /// rides the texture request's ScaleOffset kind, which also derives
        /// the animatable binding name. <c>_Cutoff</c> rides here as a
        /// captured theorem scalar even though conversion also reads it.
        /// </summary>
        internal static MaterialEvidenceRequest AlphaEvidenceRequest { get; } =
            new MaterialEvidenceRequest(
                shaderName: true,
                activeColorSpace: false,
                presenceProperties: Array.Empty<string>(),
                scalarProperties: new[]
                {
                    LilToonSourceAttestation.ShaderFormatVersionProperty,
                    InvisibleProperty,
                    UdimDiscardCompileProperty,
                    UdimDiscardModeProperty,
                    ShiftBackfaceUvProperty,
                    UseParallaxProperty,
                    UseMain2ndTexProperty,
                    UseMain3rdTexProperty,
                    AlphaMaskModeProperty,
                    UseDitherProperty,
                    IdMask1Property,
                    IdMask2Property,
                    IdMask3Property,
                    IdMask4Property,
                    IdMask5Property,
                    IdMask6Property,
                    IdMask7Property,
                    IdMask8Property,
                    IdMaskControlsDissolveProperty,
                    CutoffProperty,
                },
                colorProperties: new[] { ColorProperty },
                vectorProperties: new[]
                {
                    DissolveParamsProperty,
                    MainTexScrollRotateProperty,
                },
                textureProperties: new[]
                {
                    new TexturePropertyEvidenceRequest(
                        MainTextureProperty,
                        TextureEvidenceKinds.ScaleOffset |
                        TextureEvidenceKinds.SourceIdentity |
                        TextureEvidenceKinds.Sampling |
                        TextureEvidenceKinds.AlphaChannel),
                });

        /// <summary>
        /// Interprets the alpha of evidence already admitted against
        /// <see cref="AlphaEvidenceRequest"/> (the verified seam; callers
        /// establish admission upstream, as for the opaque lilToon and
        /// Poiyomi frontends).
        /// </summary>
        internal static SemanticOutput<ScalarSemanticValue>
            InterpretVerifiedCutoutAlpha(CapturedMaterialEvidence evidence)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            return InterpretCutoutAlpha(
                evidence, new List<LilToonSemanticDiagnostic>());
        }

        /// <summary>
        /// Verified-material seam mirroring
        /// <see cref="LilToonMaterialSemantics.InterpretVerifiedMaterial"/>
        /// so deterministic tests can exercise the cutout alpha on a material
        /// without vendor attestation. The cutout slice is an alpha-only
        /// frontend: every output other than Alpha stays <c>Unknown</c>.
        /// <para>
        /// <paramref name="activeColorSpace"/> and
        /// <paramref name="compiledFeatures"/> are accepted for seam parity
        /// only. The cutout verdict is a function of the captured runtime
        /// gates and never of color-space conversion or the compiled define
        /// set, so the invariance tests vary both and must observe an
        /// identical alpha output; no equation here reads either fact.
        /// </para>
        /// </summary>
        internal static LilToonSemanticResult InterpretVerifiedCutoutMaterial(
            Material material,
            ColorSpace activeColorSpace,
            IReadOnlyCollection<string> compiledFeatures)
        {
            RequireAnalyzableMaterial(material);
            if (compiledFeatures == null)
            {
                throw new ArgumentNullException(nameof(compiledFeatures));
            }

            _ = activeColorSpace;

            // Deliberately unread; see the doc comment above.
            _ = compiledFeatures;

            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(material, AlphaEvidenceRequest),
            })[0];

            var diagnostics = new List<LilToonSemanticDiagnostic>();
            var alpha = InterpretCutoutAlpha(captured, diagnostics);

            return new LilToonSemanticResult(
                true,
                new MaterialSemantics(
                    SemanticOutput<ColorSemanticValue>.Unknown(),
                    alpha,
                    SemanticOutput<ColorSemanticValue>.Unknown(),
                    SemanticOutput<NormalSemanticValue>.Unknown()),
                diagnostics);
        }

        private static SemanticOutput<ScalarSemanticValue> InterpretCutoutAlpha(
            CapturedMaterialEvidence evidence,
            List<LilToonSemanticDiagnostic> diagnostics)
        {
            // (1) Every optional alpha/coverage feature exactly off. The
            // first failure names the offending property.
            var gate = FirstFailedZeroGate(evidence, AlphaCoverageGates);
            if (gate != null)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    gate);
            }

            // (2) Dissolve mode zero, exactly. The shader rounds the mode
            // before branching; the proof cannot, so anything but exact zero
            // — and any non-finite component — refuses (B2 §10).
            if (!evidence.TryGetVector(
                    DissolveParamsProperty, out var dissolveParams) ||
                !IsFinite(dissolveParams) ||
                dissolveParams.x != 0f)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    DissolveParamsProperty);
            }

            // (3) Main UV scroll/rotate must be the exact identity; any
            // nonzero component scrolls or rotates the sampling coordinate.
            // Compared per binary32 component: Unity's aggregate vector
            // equality is epsilon-based and is intentionally excluded from
            // semantic proof decisions, because lilRotateUV applies
            // uv_sr.z + uv_sr.w * LIL_TIME and adds frac(uv_sr.xy * LIL_TIME),
            // where no nonzero value is inert. -0.0f stays admitted:
            // -0.0f != 0f is false.
            if (!evidence.TryGetVector(
                    MainTexScrollRotateProperty, out var scrollRotate) ||
                !IsFinite(scrollRotate) ||
                scrollRotate.x != 0f ||
                scrollRotate.y != 0f ||
                scrollRotate.z != 0f ||
                scrollRotate.w != 0f)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    MainTexScrollRotateProperty);
            }

            // (4) Cutoff: non-finite refuses, and above the twice-margin
            // bound clip(1 - c) discards unit alpha, so no triangle is
            // provable and the classification layer refuses (B2 §10).
            if (!evidence.TryGetScalar(CutoffProperty, out var cutoff) ||
                !IsFinite(cutoff) ||
                cutoff > MaxProvableCutoff)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    CutoffProperty);
            }

            // (5) The tint multiplier must be present with a finite alpha.
            // A non-finite multiplier is an interpretation refusal, never the
            // resolver's uniform-transparent fallthrough (mirrors the
            // Poiyomi frontend's finite check).
            if (!evidence.TryGetColor(ColorProperty, out var color) ||
                !IsFinite(color.a))
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    ColorProperty);
            }
            var colorAlpha = color.a;

            // Texture-backed arm (B2 basis): the cutout alpha is the plain
            // _MainTex alpha sample at UV0, built from the captured ScaleOffset.
            // The cutout source executes a runtime rotation path even at zero
            // scroll/rotate, so C4 keeps non-identity ST at this family boundary
            // rather than delegating it to the family-blind resolver.
            if (!evidence.TryGetTexture(
                    MainTextureProperty, out var assignment) ||
                !assignment.IsAssigned)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    MainTextureProperty);
            }

            if (!assignment.Texture.HasSourceIdentity)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnstableTextureIdentity,
                    MainTextureProperty);
            }

            if (!assignment.Texture.HasSampling)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedSampling,
                    MainTextureProperty);
            }

            if (!assignment.HasScaleOffset)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    MainTextureProperty);
            }

            // Identity is tested exactly, per binary32 component. Unity's
            // Vector2 ==/!= is deliberately not used here: it is epsilon-based
            // (equal when the difference magnitude is under 1e-5), so it would
            // let near-identity ST past this C4 boundary and into the
            // family-blind affine resolver, whose own identity test is exact.
            // -0.0f stays admitted: -0.0f != 0f is false, and +-0 are
            // equivalent for this coordinate model.
            if (assignment.Scale.x != 1f ||
                assignment.Scale.y != 1f ||
                assignment.Offset.x != 0f ||
                assignment.Offset.y != 0f)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedUv,
                    MainTexStProperty);
            }

            var mapping = new UvMapping(0, assignment.Scale, assignment.Offset);
            var sample = new TextureSample(
                assignment.Texture.SourceIdentity,
                mapping,
                assignment.Texture.Sampling);
            var value = colorAlpha == 1f
                ? ScalarSemanticValue.Texture(sample, TextureChannel.Alpha)
                : ScalarSemanticValue.TextureTimesConstant(
                    sample, TextureChannel.Alpha, colorAlpha);
            return SemanticOutput<ScalarSemanticValue>.Complete(value);
        }

        /// <summary>
        /// Returns the first property that fails the exact-off gate — absent
        /// from the capture, non-finite, or not exactly zero — or null when
        /// every property proves off.
        /// </summary>
        private static string FirstFailedZeroGate(
            CapturedMaterialEvidence evidence,
            params string[] properties)
        {
            foreach (var property in properties)
            {
                if (!evidence.TryGetScalar(property, out var value) ||
                    !IsFinite(value) || value != 0f)
                {
                    return property;
                }
            }

            return null;
        }

        private static SemanticOutput<T> RecordUnknown<T>(
            List<LilToonSemanticDiagnostic> diagnostics,
            LilToonSemanticOutput output,
            LilToonSemanticDiagnosticCode code,
            string detail)
            where T : class
        {
            diagnostics.Add(new LilToonSemanticDiagnostic(output, code, detail));
            return SemanticOutput<T>.Unknown();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector4 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) &&
                   IsFinite(value.z) && IsFinite(value.w);
        }

        private static void RequireAnalyzableMaterial(Material material)
        {
            if (ReferenceEquals(material, null))
            {
                throw new ArgumentNullException(nameof(material));
            }

            // Unity's overloaded equality reports a destroyed object as null.
            if (material == null)
            {
                throw new ArgumentException(
                    "The material has been destroyed and cannot be analyzed.",
                    nameof(material));
            }

            if (material.shader == null)
            {
                throw new ArgumentException(
                    "The material has no shader and cannot be analyzed.",
                    nameof(material));
            }
        }
    }
}
