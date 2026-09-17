using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// Alpha semantics for the attested regular no-outline lilToon 2.3.4
    /// transparent source (<c>Hidden/lilToonTransparent</c>, LIL_RENDER 2),
    /// per the transparent-to-opaque conversion design (§7, §8; T1 §9.1).
    /// The restricted theorem: with every cutout-shared coverage feature
    /// proven off and each transparent-only post-clip writer proven an
    /// identity at unit alpha — the ForwardAdd premultiply, the subpass
    /// shadow clip, and distance fade — the coverage of a triangle is the
    /// plain clip of the <c>_MainTex</c> alpha sample at identity UV0 times
    /// <c>_Color.a</c>, refused above <see cref="MaxProvableCutoff"/>.
    /// Everything below the interpretation is the shared
    /// <c>AlphaSemanticsResolver</c>; this type only decides whether the
    /// normalized value shape is representable. Behavior the captured
    /// evidence cannot prove stays <c>Unknown</c> with one deterministic
    /// diagnostic naming the offending property; it is never guessed.
    /// </summary>
    internal static class LilToonTransparentMaterialSemantics
    {
        private const string InvisibleProperty = "_Invisible";
        private const string UdimDiscardCompileProperty = "_UDIMDiscardCompile";
        private const string UdimDiscardModeProperty = "_UDIMDiscardMode";
        private const string ShiftBackfaceUvProperty = "_ShiftBackfaceUV";
        private const string UseParallaxProperty = "_UseParallax";
        private const string UseMain2ndTexProperty = "_UseMain2ndTex";
        private const string UseMain3rdTexProperty = "_UseMain3rdTex";
        private const string AlphaMaskModeProperty = "_AlphaMaskMode";
        private const string AlphaMaskScaleProperty = "_AlphaMaskScale";
        private const string AlphaMaskValueProperty = "_AlphaMaskValue";
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
        private const string AlphaBoostFaProperty = "_AlphaBoostFA";
        private const string SubpassCutoffProperty = "_SubpassCutoff";
        private const string DistanceFadeProperty = "_DistanceFade";

        /// <summary>
        /// The transparent clip bound (design §9 gate 12; T1 §9.2). The
        /// transparent forward site is a plain clip(fd.col.a - _Cutoff), not
        /// the cutout coverage transform, so the cutout twice-margin bound
        /// 0.9999 is deliberately NOT reused: for any finite c &lt;= 1 the
        /// exact difference 1 - c is nonnegative, round-to-nearest preserves
        /// that sign, and clip keeps the fragment. At c == 1 the difference is
        /// exactly zero, which clip keeps. Above 1 the difference is a nonzero
        /// negative well above the underflow threshold, and clip discards.
        /// </summary>
        private const float MaxProvableCutoff = 1f;

        /// <summary>
        /// The ForwardAdd premultiply lower bound (T1 §5.3). The base pass
        /// premultiply is fd.col.rgb *= fd.col.a, the identity at a = 1; the
        /// ForwardAdd pass instead applies saturate(fd.col.a *
        /// _AlphaBoostFA), which is the identity at a = 1 only when the boost
        /// saturates to at least one.
        /// </summary>
        private const float MinProvableAlphaBoostFa = 1f;

        /// <summary>
        /// The subpass shadow clip bound (T1 §9.4, measured). At a = 1 the
        /// dither sample returns 1 at all sixteen positions of the
        /// _DitherMaskLOD slice the alpha selects, so the shadow clip reduces
        /// to clip(1 - _SubpassCutoff) and keeps by the same sign-preservation
        /// argument as the forward cutoff.
        /// </summary>
        private const float MaxProvableSubpassCutoff = 1f;

        /// <summary>
        /// Every runtime gate that can change transparent coverage. With any
        /// of these active the coverage is not the plain clip of the main
        /// alpha sample (design §7 clause 2), so the interpretation refuses.
        /// <see cref="IdMaskControlsDissolveProperty"/> is the
        /// adversarial-review gate: with it set, the vertex IDMask path can
        /// force the sampled alpha chain to zero even at dissolve mode zero
        /// (B2 §3.3.8, §5 clause 2). <c>_UseDither</c> is deliberately not a
        /// gate here: LIL_RENDER 2 compiles the runtime dither path out
        /// entirely (T1 §6 row 16), so an authored toggle is inert. The gates
        /// are runtime captured facts — never the compiled feature set.
        /// </summary>
        private static readonly string[] AlphaCoverageGates =
        {
            InvisibleProperty,
            UdimDiscardCompileProperty,
            UdimDiscardModeProperty,
            ShiftBackfaceUvProperty,
            UseParallaxProperty,
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
        /// The transparent alpha evidence request (design §8). Exact by
        /// contract: no fewer and no more. It is the cutout schema plus the
        /// three transparent-only proof facts — <c>_AlphaBoostFA</c> and
        /// <c>_SubpassCutoff</c> as scalars, <c>_DistanceFade</c> as a
        /// vector — and minus <c>_UseDither</c>, which LIL_RENDER 2 compiles
        /// the runtime dither path out of entirely (T1 §6 row 16).
        /// <c>_IDMaskPrior8</c> is deliberately absent (it is a fixture-only
        /// vendor prior byte, not an AMUSE-proof fact), and
        /// <c>_MainTex_ST</c> is deliberately not a vector request — it
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
                    AlphaBoostFaProperty,
                    SubpassCutoffProperty,
                    AlphaMaskScaleProperty,
                    AlphaMaskValueProperty,
                    "_Main2ndTex_UVMode",
                    "_Main3rdTex_UVMode",
                    "_Main2ndTexAngle",
                    "_Main3rdTexAngle",
                    "_Main2ndTex_Cull",
                    "_Main3rdTex_Cull",
                    "_Main2ndTexAlphaMode",
                    "_Main3rdTexAlphaMode",
                    "_Main2ndTexIsDecal",
                    "_Main3rdTexIsDecal",
                    "_Main2ndTexIsLeftOnly",
                    "_Main3rdTexIsLeftOnly",
                    "_Main2ndTexIsRightOnly",
                    "_Main3rdTexIsRightOnly",
                    "_Main2ndTexShouldCopy",
                    "_Main3rdTexShouldCopy",
                    "_Main2ndTexShouldFlipMirror",
                    "_Main3rdTexShouldFlipMirror",
                    "_Main2ndTexShouldFlipCopy",
                    "_Main3rdTexShouldFlipCopy",
                    "_Main2ndTexIsMSDF",
                    "_Main3rdTexIsMSDF",
                    "_AudioLink2Main2nd",
                    "_AudioLink2Main3rd",
                },
                colorProperties: new[] { ColorProperty, "_Color2nd", "_Color3rd" },
                vectorProperties: new[]
                {
                    DissolveParamsProperty,
                    MainTexScrollRotateProperty,
                    DistanceFadeProperty,
                    "_Main2ndTex_ScrollRotate",
                    "_Main3rdTex_ScrollRotate",
                    "_Main2ndDistanceFade",
                    "_Main3rdDistanceFade",
                    "_Main2ndDissolveParams",
                    "_Main3rdDissolveParams",
                },
                textureProperties: new[]
                {
                    new TexturePropertyEvidenceRequest(
                        "_Main2ndTex",
                        TextureEvidenceKinds.ScaleOffset |
                        TextureEvidenceKinds.SourceIdentity |
                        TextureEvidenceKinds.Sampling |
                        TextureEvidenceKinds.AlphaChannel |
                        TextureEvidenceKinds.SampledAlphaIsOne),
                    new TexturePropertyEvidenceRequest(
                        "_Main3rdTex",
                        TextureEvidenceKinds.ScaleOffset |
                        TextureEvidenceKinds.SourceIdentity |
                        TextureEvidenceKinds.Sampling |
                        TextureEvidenceKinds.AlphaChannel |
                        TextureEvidenceKinds.SampledAlphaIsOne),
                    new TexturePropertyEvidenceRequest(
                        "_Main2ndBlendMask",
                        TextureEvidenceKinds.SourceIdentity |
                        TextureEvidenceKinds.RedChannel),
                    new TexturePropertyEvidenceRequest(
                        "_Main3rdBlendMask",
                        TextureEvidenceKinds.SourceIdentity |
                        TextureEvidenceKinds.RedChannel),
                    // No cutoff declaration: the transparent alpha is the
                    // plain unclipped _MainTex sample (T1 §9.1 clause 5),
                    // so the exact-255 arm is this family's capture arm
                    // and the alpha policy applies to it.
                    new TexturePropertyEvidenceRequest(
                        MainTextureProperty,
                        TextureEvidenceKinds.ScaleOffset |
                        TextureEvidenceKinds.SourceIdentity |
                        TextureEvidenceKinds.Sampling |
                        TextureEvidenceKinds.AlphaChannel |
                        TextureEvidenceKinds.SampledAlphaIsOne),

                    // The alpha mask rides _MainTex's sampler, so this
                    // request deliberately asks for no sampling facts of
                    // its own: wrap, filter, and anisotropy evidence comes
                    // from the _MainTex assignment. The red field, not
                    // alpha, is the mask channel.
                    new TexturePropertyEvidenceRequest(
                        "_AlphaMask",
                        TextureEvidenceKinds.ScaleOffset |
                        TextureEvidenceKinds.SourceIdentity |
                        TextureEvidenceKinds.RedChannel),
                });

        /// <summary>
        /// Interprets the alpha of evidence already admitted against
        /// <see cref="AlphaEvidenceRequest"/> (the verified seam; callers
        /// establish admission upstream, as for the opaque lilToon and
        /// Poiyomi frontends).
        /// </summary>
        internal static SemanticOutput<ScalarSemanticValue>
            InterpretVerifiedTransparentAlpha(
                CapturedMaterialEvidence evidence)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            return InterpretTransparentAlpha(
                evidence, new List<LilToonSemanticDiagnostic>());
        }

        /// <summary>
        /// Verified-material seam mirroring
        /// <see cref="LilToonMaterialSemantics.InterpretVerifiedMaterial"/>
        /// so deterministic tests can exercise the transparent alpha on a
        /// material without vendor attestation. The transparent slice is an
        /// alpha-only frontend: every output other than Alpha stays
        /// <c>Unknown</c>.
        /// <para>
        /// <paramref name="activeColorSpace"/> and
        /// <paramref name="compiledFeatures"/> are accepted for seam parity
        /// only. The transparent verdict is a function of the captured
        /// runtime gates and never of color-space conversion or the compiled
        /// define set, so the invariance tests vary both and must observe an
        /// identical alpha output; no equation here reads either fact.
        /// </para>
        /// </summary>
        internal static LilToonSemanticResult InterpretVerifiedTransparentMaterial(
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
            var alpha = InterpretTransparentAlpha(captured, diagnostics);

            return new LilToonSemanticResult(
                true,
                new MaterialSemantics(
                    SemanticOutput<ColorSemanticValue>.Unknown(),
                    alpha,
                    SemanticOutput<ColorSemanticValue>.Unknown(),
                    SemanticOutput<NormalSemanticValue>.Unknown()),
                diagnostics);
        }

        private static SemanticOutput<ScalarSemanticValue> InterpretTransparentAlpha(
            CapturedMaterialEvidence evidence,
            List<LilToonSemanticDiagnostic> diagnostics)
        {
            // (1) Every optional alpha/coverage feature exactly off.
            var gate = FirstFailedZeroGate(evidence, AlphaCoverageGates);
            if (gate != null)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    gate);
            }

            // (1a) Alpha mask interpretation (shared with the cutout
            // frontend; pinned equation and admitted cases in
            // LilToonAlphaMaskTerm). A refusal records its own
            // diagnostic; the sample outcome defers to the texture arm,
            // which owns the shared-sampler and UV0 gates.
            var maskTerm = LilToonAlphaMaskTerm.Interpret(
                evidence, diagnostics);
            if (maskTerm.Kind == LilToonAlphaMaskTermKind.Refused)
            {
                return SemanticOutput<ScalarSemanticValue>.Unknown();
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

            // (4) Cutoff: non-finite refuses, and above the transparent
            // bound the plain clip(1 - c) discards unit alpha, so no
            // triangle is provable and the classification layer refuses
            // (T1 §9.2).
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

            // (5) ForwardAdd premultiply. Unlike the base pass, the
            // FORWARD_ADD premultiply is not an identity at a = 1 unless the
            // boost is at least one; below it the additive pass composites a
            // darkened colour the opaque target would not.
            if (!evidence.TryGetScalar(
                    AlphaBoostFaProperty, out var alphaBoostFa) ||
                !IsFinite(alphaBoostFa) ||
                alphaBoostFa < MinProvableAlphaBoostFa)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    AlphaBoostFaProperty);
            }

            // (6) Subpass shadow clip. The SHADOW_CASTER pass clips against
            // _SubpassCutoff after a dither sample that is uniformly one at
            // a = 1; above the bound the source casts no shadow where the
            // opaque target would.
            if (!evidence.TryGetScalar(
                    SubpassCutoffProperty, out var subpassCutoff) ||
                !IsFinite(subpassCutoff) ||
                subpassCutoff > MaxProvableSubpassCutoff)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    SubpassCutoffProperty);
            }

            // (7) Distance fade. At LIL_RENDER 2 the distance-fade block
            // writes fd.col.a after the clip, so an enabled fade is the one
            // post-clip alpha writer this family must refuse. The gate is
            // the .z strength component, not _DistanceFadeColor.a: the two
            // arms diverge, and only .z disables the alpha write.
            if (!evidence.TryGetVector(
                    DistanceFadeProperty, out var distanceFade) ||
                !IsFinite(distanceFade) ||
                distanceFade.z != 0f)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    DistanceFadeProperty);
            }

            // (8) The tint multiplier must be present with a finite alpha.
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

            // Replace mode with a constant term samples nothing at all:
            // the alpha is that constant, so no texture gate applies.
            if (maskTerm.Kind == LilToonAlphaMaskTermKind.Constant)
            {
                return SemanticOutput<ScalarSemanticValue>.Complete(
                    ScalarSemanticValue.Constant(maskTerm.Constant));
            }

            // Texture-backed arm (T1 §9.1 clause 5): the transparent alpha
            // is the plain _MainTex alpha sample at UV0, built from the
            // captured ScaleOffset, composed with the mask term when one
            // runs. The transparent source executes the same runtime
            // rotation path even at zero scroll/rotate, so the identity-only
            // boundary keeps non-identity ST at this family boundary rather
            // than delegating it to the family-blind resolver (T1 §9.3).
            // The boundary binds the mask too: uvMain feeds the mask
            // coordinate, and the mask rides _MainTex's sampler.
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

            // uvMain is UV0 under the identity gates above. The mask
            // coordinate is UV0 under the mask's own plain affine, and the
            // mask sample borrows _MainTex's captured sampler facts.
            var identityMapping =
                new UvMapping(0, assignment.Scale, assignment.Offset);
            var sharedSampling = assignment.Texture.Sampling;

            // The shader composes, in order: main alpha, second layer, third
            // layer, alpha mask, dissolve, clip. The base below is the main
            // alpha; the layers compose onto it; the mask term composes last.
            ScalarSemanticValue alphaChain;
            if (assignment.Texture.SampledAlphaIsProvenOne)
            {
                // The importer theorem: a source without an alpha channel and
                // an import that writes none samples alpha exactly one at
                // every texel of every level, so the main sample collapses to
                // its constant and no main field is read.
                alphaChain = ScalarSemanticValue.Constant(colorAlpha);
            }
            else
            {
                if (!assignment.Texture.HasSourceIdentity)
                {
                    return RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        LilToonSemanticOutput.Alpha,
                        LilToonSemanticDiagnosticCode
                            .UnstableTextureIdentity,
                        MainTextureProperty);
                }

                var mainSample = new TextureSample(
                    assignment.Texture.SourceIdentity,
                    identityMapping,
                    sharedSampling);
                alphaChain = colorAlpha == 1f
                    ? ScalarSemanticValue.Texture(
                        mainSample, TextureChannel.Alpha)
                    : ScalarSemanticValue.TextureTimesConstant(
                        mainSample, TextureChannel.Alpha, colorAlpha);
            }

            TextureSample maskSample = null;
            if (maskTerm.Kind == LilToonAlphaMaskTermKind.Sample)
            {
                maskSample = new TextureSample(
                    maskTerm.Source,
                    maskTerm.Mapping,
                    sharedSampling);

                // A replace mask runs after the layers, so the mask term is
                // the whole alpha: neither _MainTex's texels nor _Color.a nor
                // any layer reaches the value.
                if (maskTerm.ReplacesMainAlpha)
                {
                    return SemanticOutput<ScalarSemanticValue>.Complete(
                        ScalarSemanticValue.Texture(
                            maskSample, TextureChannel.Red));
                }
            }

            // The layers, in shader order. Their writers sit before the mask.
            alphaChain = ComposeLayer(
                alphaChain, evidence, third: false, diagnostics);
            if (alphaChain == null)
            {
                return SemanticOutput<ScalarSemanticValue>.Unknown();
            }

            alphaChain = ComposeLayer(
                alphaChain, evidence, third: true, diagnostics);
            if (alphaChain == null)
            {
                return SemanticOutput<ScalarSemanticValue>.Unknown();
            }

            if (maskSample != null)
            {
                // Multiply mode: the term composes over the layered value.
                // The multiply cannot fold into a saturating sum or
                // difference below it, so those shapes refuse.
                var multiplied = Multiply(
                    alphaChain,
                    ScalarSemanticValue.TextureTimesConstant(
                        maskSample, TextureChannel.Red, 1f),
                    AlphaMaskModeProperty,
                    diagnostics);
                if (multiplied == null)
                {
                    return SemanticOutput<ScalarSemanticValue>.Unknown();
                }

                alphaChain = multiplied;
            }

            return SemanticOutput<ScalarSemanticValue>.Complete(alphaChain);
        }

        /// <summary>
        /// Composes one layer onto the running alpha by its writer mode.
        /// Returns null after recording the layer's refusal or the
        /// composition refusal. The multiply-over-saturating-shape refusal
        /// is exact, not laziness: a product with a sum changes the
        /// evaluated rounding chain, and the exact-one predicate of a
        /// product is association-invariant only over pure factors.
        /// </summary>
        private static ScalarSemanticValue ComposeLayer(
            ScalarSemanticValue baseValue,
            CapturedMaterialEvidence evidence,
            bool third,
            List<LilToonSemanticDiagnostic> diagnostics)
        {
            var term = LilToonLayerAlphaTerm.Interpret(
                evidence, third, diagnostics);
            if (term.Kind == LilToonLayerAlphaTermKind.Refused)
            {
                return null;
            }

            if (term.Kind == LilToonLayerAlphaTermKind.Inert)
            {
                return baseValue;
            }

            ScalarSemanticValue layerValue =
                term.Kind == LilToonLayerAlphaTermKind.Constant
                    ? ScalarSemanticValue.Constant(term.Constant)
                    : term.Value;
            var modeProperty =
                (third ? "_Main3rdTexAlphaMode" : "_Main2ndTexAlphaMode");

            switch (term.AlphaMode)
            {
                case 1f:
                    return layerValue;
                case 2f:
                    return Multiply(
                        baseValue, layerValue, modeProperty, diagnostics);
                case 3f:
                    return ScalarSemanticValue.SaturatingSum(
                        baseValue, layerValue);
                case 4f:
                    return ScalarSemanticValue.SaturatingDifference(
                        baseValue, layerValue);
                default:
                    throw new InvalidOperationException(
                        "A layer term carried a writer mode outside one to " +
                        "four, which the term contract excludes.");
            }
        }

        /// <summary>
        /// The product of two closed forms, folded through their constants.
        /// The exact-one predicate of a product of values bounded in [0,1] is
        /// association-invariant: every rounded chain of sub-one factors
        /// stays strictly below one, and all-one factors answer exactly one
        /// in every association, so the fold changes nothing provable.
        /// Returns null after recording a refusal when either shape is a
        /// saturating sum or difference, whose exact-one predicate is not
        /// multiplication-invariant.
        /// </summary>
        private static ScalarSemanticValue Multiply(
            ScalarSemanticValue baseValue,
            ScalarSemanticValue factor,
            string refusalProperty,
            List<LilToonSemanticDiagnostic> diagnostics)
        {
            if (baseValue.Kind == ScalarSemanticValueKind.SaturatingSum ||
                baseValue.Kind ==
                    ScalarSemanticValueKind.SaturatingDifference ||
                factor.Kind == ScalarSemanticValueKind.SaturatingSum ||
                factor.Kind == ScalarSemanticValueKind.SaturatingDifference)
            {
                diagnostics.Add(
                    new LilToonSemanticDiagnostic(
                        LilToonSemanticOutput.Alpha,
                        LilToonSemanticDiagnosticCode.UnsupportedFeature,
                        refusalProperty));
                return null;
            }

            var samples = new List<TextureSample>();
            var channels = new List<TextureChannel>();
            var multiplier = 1f;
            multiplier = CollectFactors(baseValue, samples, channels, multiplier);
            multiplier = CollectFactors(factor, samples, channels, multiplier);

            if (samples.Count == 0)
            {
                return ScalarSemanticValue.Constant(multiplier);
            }

            if (samples.Count == 1)
            {
                return ScalarSemanticValue.TextureTimesConstant(
                    samples[0], channels[0], multiplier);
            }

            return ScalarSemanticValue.ProductChain(
                samples, channels, multiplier);
        }

        private static float CollectFactors(
            ScalarSemanticValue value,
            List<TextureSample> samples,
            List<TextureChannel> channels,
            float multiplier)
        {
            switch (value.Kind)
            {
                case ScalarSemanticValueKind.Constant:
                    return multiplier * value.GetConstantValue();
                case ScalarSemanticValueKind.TextureSample:
                    samples.Add(value.GetTextureSample());
                    channels.Add(value.GetChannel());
                    return multiplier;
                case ScalarSemanticValueKind.TextureSampleTimesConstant:
                    samples.Add(value.GetTextureSample());
                    channels.Add(value.GetChannel());
                    return multiplier * value.GetMultiplier();
                case ScalarSemanticValueKind
                    .ProductChainOfTextureSamples:
                    for (var index = 0;
                         index < value.GetChainFactorCount();
                         index++)
                    {
                        samples.Add(value.GetChainSample(index));
                        channels.Add(value.GetChainChannel(index));
                    }

                    return multiplier * value.GetProductMultiplier();
                default:
                    throw new InvalidOperationException(
                        "A saturating shape reached factor collection, " +
                        "which the caller must refuse first.");
            }
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
