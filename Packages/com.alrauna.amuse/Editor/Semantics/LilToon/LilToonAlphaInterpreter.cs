using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using static Alrauna.Amuse.Editor.Semantics.EvidenceGates;
using static Alrauna.Amuse.Editor.Semantics.LilToon.LilToonUnknownRecord;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// One parameterized alpha interpreter for the two duplicated lilToon
    /// frontend families, cutout (<c>Hidden/lilToonCutout</c>, LIL_RENDER 1)
    /// and transparent (<c>Hidden/lilToonTransparent</c>, LIL_RENDER 2). The
    /// family differences are exactly the three deltas the record measured,
    /// and all three arrive as inputs: the coverage-gate array (whose
    /// membership carries the dither difference), the cutoff bound (each
    /// family's <c>MaxProvableCutoff</c> stays declared in its own file with
    /// its own proof doc), and the transparent-only bounded scalar gates
    /// (absent for cutout). The interpreter never derives a bound and never
    /// branches on family identity: equal inputs give equal verdicts.
    /// Behavior the captured evidence cannot prove stays <c>Unknown</c> with
    /// one deterministic diagnostic naming the offending property; it is
    /// never guessed.
    /// </summary>
    internal static class LilToonAlphaInterpreter
    {
        private const string DissolveParamsProperty = "_DissolveParams";
        private const string MainTexScrollRotateProperty =
            "_MainTex_ScrollRotate";
        private const string CutoffProperty = "_Cutoff";
        private const string ColorProperty = "_Color";
        private const string MainTextureProperty = "_MainTex";
        private const string MainTexStProperty = "_MainTex_ST";
        private const string AlphaMaskModeProperty = "_AlphaMaskMode";

        /// <summary>
        /// Interprets the alpha of evidence already admitted against the
        /// family's alpha evidence request. The sequence and every recording
        /// point are the ones the two frontend interpreters pinned: coverage
        /// gates, mask term, dissolve, scroll/rotate, cutoff, the
        /// family's bounded gates, distance fade, tint, then the value
        /// shape arms.
        /// </summary>
        /// <param name="evidence">
        /// The captured facts admitted against the family's request.
        /// </param>
        /// <param name="diagnostics">
        /// The refusal recorder; each unknown appends exactly one
        /// diagnostic naming the offending property.
        /// </param>
        /// <param name="coverageGates">
        /// The family's runtime coverage gates, in pinned order. Cutout
        /// includes <c>_UseDither</c>; transparent does not, because
        /// LIL_RENDER 2 compiles the runtime dither path out entirely
        /// (T1 §6 row 16).
        /// </param>
        /// <param name="maxProvableCutoff">
        /// The family's pinned cutoff bound, passed through from its own
        /// file's <c>MaxProvableCutoff</c> constant. This interpreter never
        /// derives it.
        /// </param>
        /// <param name="transparentOnlyGates">
        /// The family's bounded scalar gates beyond the cutoff, in pinned
        /// order: for transparent the ForwardAdd premultiply bound and the
        /// subpass shadow clip bound; empty for cutout, whose theorem has
        /// no post-clip writer.
        /// </param>
        /// <param name="requiresExactMainField">
        /// Whether the family's theorem reads the main alpha field under the
        /// exact-255 exact-one rule. Transparent binds true. Cutout binds
        /// false: its own request declares the cutoff, and its coverage
        /// transform reads the binarized field by its own rule. When true,
        /// an assigned _MainTex captured under a sub-one capture threshold
        /// refuses naming _Cutoff before any field read, in the shape of the
        /// Poiyomi frontend's field-predicate agreement check.
        /// </param>
        internal static SemanticOutput<ScalarSemanticValue> Interpret(
            CapturedMaterialEvidence evidence,
            List<LilToonSemanticDiagnostic> diagnostics,
            string[] coverageGates,
            float maxProvableCutoff,
            LilToonAlphaGate[] transparentOnlyGates,
            bool requiresExactMainField)
        {
            // (1) Every optional alpha/coverage feature exactly off. The
            // first failure names the offending property.
            var gate = FirstFailedZeroGate(evidence, coverageGates);
            if (gate != null)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    gate);
            }

            // (1a) Alpha mask interpretation (shared between the cutout and
            // transparent frontends; pinned equation and admitted cases in
            // LilToonAlphaMaskTerm). A refusal records its own
            // diagnostic; the sample outcome defers to the texture arm,
            // which owns the shared-sampler and UV0 gates. The mask
            // composes before the family's coverage transform in the
            // source.
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

            // (4) Cutoff: non-finite refuses, and above the family's pinned
            // bound no triangle is provable and the classification layer
            // refuses. The bound is an input: the cutout coverage transform
            // carries the twice-margin bound (B2 §10), the transparent
            // plain clip proves up to one by sign preservation (T1 §9.2);
            // each family's MaxProvableCutoff doc carries its own argument.
            if (!evidence.TryGetScalar(CutoffProperty, out var cutoff) ||
                !IsFinite(cutoff) ||
                cutoff > maxProvableCutoff)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    CutoffProperty);
            }

            // (4a) The family's bounded gates past the cutoff, in pinned
            // order. Transparent passes its two post-clip writers: the
            // ForwardAdd premultiply (T1 §5.3) and the subpass shadow clip
            // (T1 §9.4). Each refuses exactly as the frontend checks it
            // replaced: a missing or non-finite capture refuses, then the
            // finite value must satisfy the family-declared bound.
            foreach (var boundedGate in transparentOnlyGates)
            {
                if (!evidence.TryGetScalar(
                        boundedGate.Property, out var boundedValue) ||
                    !IsFinite(boundedValue) ||
                    boundedGate.Refuses(boundedValue))
                {
                    return RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        LilToonSemanticOutput.Alpha,
                        LilToonSemanticDiagnosticCode.UnsupportedFeature,
                        boundedGate.Property);
                }
            }

            // (7) The distance fade strength. The vendor block scales every arm
            //     by the .z strength, so a finite zero strength is an exact
            //     no-op. A finite nonzero strength retains the material in
            //     both families: the color arm runs at every render mode, and
            //     at LIL_RENDER 1 the dither path additionally lerps alpha
            //     before the coverage transform, so a moved triangle would
            //     lose its fade. The gate is the .z strength component, not
            //     _DistanceFadeColor.a: the two arms diverge, and only .z
            //     disables the alpha write.
            switch (LilToonDistanceFadeSemantics.Evaluate(evidence))
            {
                case LilToonDistanceFadeAnswer.Inert:
                    break;
                case LilToonDistanceFadeAnswer.Retained:
                    return RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        LilToonSemanticOutput.Alpha,
                        LilToonSemanticDiagnosticCode.FeatureRetention,
                        LilToonDistanceFadeSemantics.DistanceFadeProperty);
                default:
                    // Absent and NonFinite keep today's refusal wording.
                    return RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        LilToonSemanticOutput.Alpha,
                        LilToonSemanticDiagnosticCode.UnsupportedFeature,
                        LilToonDistanceFadeSemantics.DistanceFadeProperty);
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

            // Texture-backed arm. An unassigned _MainTex takes the
            // declared-default arm first; an assigned texture keeps every
            // captured-fact gate below, in the same order as before. Both
            // sources execute the runtime rotation path even at zero
            // scroll/rotate, so the identity-only boundary keeps
            // non-identity ST at this family boundary rather than delegating
            // it to the family-blind affine resolver (C4; T1 §9.3). The
            // boundary binds the mask too: uvMain feeds the mask
            // coordinate, and the mask rides _MainTex's sampler.
            var hasMainSampler = false;
            ScalarSemanticValue alphaChain;
            if (!evidence.TryGetTexture(MainTextureProperty, out var assignment))
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    MainTextureProperty);
            }

            if (!assignment.IsAssigned)
            {
                // Declared-default arm (design 2026-09-26): the arm fires
                // for a declared slot that binds no texture. It never fires
                // for a slot absent from the material. That case refuses
                // above. Every attested lilToon source declares
                // _MainTex = "white" {}, and the canonical digests pin that
                // Properties block. Playback
                // binds the declared default when a material assigns no
                // texture, so the sample is exactly one at every texel and
                // coordinate-independent: the main sample collapses to its
                // constant exactly as the importer theorem does for the
                // assigned case. The digest is the enforcement: a vendor
                // change to the default breaks attestation before this arm
                // can run.
                alphaChain = ScalarSemanticValue.Constant(colorAlpha);
            }
            else
            {
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

                hasMainSampler = true;

                // The shader composes, in order: main alpha, second layer,
                // third layer, alpha mask, dissolve, clip. The base below is
                // the main alpha; the layers compose onto it; the mask term
                // composes last. The family's clip applies after everything
                // and is the classifier's declared cutoff, so it composes
                // nowhere here.
                if (assignment.Texture.SampledAlphaIsProvenOne)
                {
                    // The importer theorem: a source without an alpha channel
                    // and an import that writes none samples alpha exactly one
                    // at every texel of every level, so the main sample
                    // collapses to its constant and no main field is read.
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

                    if (requiresExactMainField &&
                        assignment.Texture.CaptureThreshold < 1f)
                    {
                        return RecordUnknown<ScalarSemanticValue>(
                            diagnostics,
                            LilToonSemanticOutput.Alpha,
                            LilToonSemanticDiagnosticCode.UnsupportedFeature,
                            CutoffProperty);
                    }

                    // uvMain is UV0 under the identity gates above.
                    var mainSample = new TextureSample(
                        assignment.Texture.SourceIdentity,
                        new UvMapping(0, assignment.Scale, assignment.Offset),
                        assignment.Texture.Sampling);
                    alphaChain = colorAlpha == 1f
                        ? ScalarSemanticValue.Texture(
                            mainSample, TextureChannel.Alpha)
                        : ScalarSemanticValue.TextureTimesConstant(
                            mainSample, TextureChannel.Alpha, colorAlpha);
                }
            }

            TextureSample maskSample = null;
            AffineAlphaMap? maskMap = null;
            if (maskTerm.Kind == LilToonAlphaMaskTermKind.Sample ||
                maskTerm.Kind == LilToonAlphaMaskTermKind.MappedSample)
            {
                // The mask borrows _MainTex's captured sampler facts. An
                // unassigned main has none, so a sampled or mapped mask
                // refuses by name instead of proving through borrowed facts.
                if (!hasMainSampler)
                {
                    return RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        LilToonSemanticOutput.Alpha,
                        LilToonSemanticDiagnosticCode.UnsupportedSampling,
                        MainTextureProperty);
                }

                maskSample = new TextureSample(
                    maskTerm.Source,
                    maskTerm.Mapping,
                    assignment.Texture.Sampling);
                if (maskTerm.Kind == LilToonAlphaMaskTermKind.MappedSample)
                {
                    maskMap = AffineAlphaMap.FromBinary32(
                        maskTerm.Scale, maskTerm.Value);
                }

                // A replace mask runs after the layers, so the mask term is
                // the whole alpha: neither _MainTex's texels nor _Color.a nor
                // any layer reaches the value.
                if (maskTerm.ReplacesMainAlpha)
                {
                    return SemanticOutput<ScalarSemanticValue>.Complete(
                        maskMap == null
                            ? ScalarSemanticValue.Texture(
                                maskSample, TextureChannel.Red)
                            : ScalarSemanticValue.MappedTexture(
                                maskSample, TextureChannel.Red, maskMap));
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

            if (maskTerm.Kind == LilToonAlphaMaskTermKind.ConstantMultiplier)
            {
                // Multiply mode with a provably constant term: the constant
                // composes over the layered value exactly as the mask
                // sample does, and the multiply cannot fold into a
                // saturating sum or difference below it.
                alphaChain = ScalarProductFold.Fold(
                    alphaChain,
                    ScalarSemanticValue.Constant(maskTerm.Constant),
                    AlphaMaskModeProperty,
                    property => diagnostics.Add(
                        new LilToonSemanticDiagnostic(
                            LilToonSemanticOutput.Alpha,
                            LilToonSemanticDiagnosticCode
                                .UnsupportedFeature,
                            property)));
                if (alphaChain == null)
                {
                    return SemanticOutput<ScalarSemanticValue>.Unknown();
                }
            }

            if (maskSample != null)
            {
                // Multiply mode: the term composes over the layered value.
                // The multiply cannot fold into a saturating sum or
                // difference below it, so those shapes refuse.
                var maskFactor = maskMap == null
                    ? ScalarSemanticValue.TextureTimesConstant(
                        maskSample, TextureChannel.Red, 1f)
                    : ScalarSemanticValue.MappedTexture(
                        maskSample, TextureChannel.Red, maskMap);
                var multiplied = ScalarProductFold.Fold(
                    alphaChain,
                    maskFactor,
                    AlphaMaskModeProperty,
                    property => diagnostics.Add(
                        new LilToonSemanticDiagnostic(
                            LilToonSemanticOutput.Alpha,
                            LilToonSemanticDiagnosticCode
                                .UnsupportedFeature,
                            property)));
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
                    return ScalarProductFold.Fold(
                        baseValue,
                        layerValue,
                        modeProperty,
                        property => diagnostics.Add(
                            new LilToonSemanticDiagnostic(
                                LilToonSemanticOutput.Alpha,
                                LilToonSemanticDiagnosticCode
                                    .UnsupportedFeature,
                                property)));
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
    }

    /// <summary>
    /// One bounded scalar gate of the shared alpha interpreter: a property,
    /// a family-declared bound, and the side of the bound that refuses. The
    /// bound always arrives from the family's pinned constant — the gate
    /// never derives one — and a gate refuses exactly as the frontend check
    /// it replaces: a missing or non-finite capture refuses, then the
    /// finite value must satisfy the bound.
    /// </summary>
    internal readonly struct LilToonAlphaGate
    {
        private readonly bool refusesBelow;

        internal string Property { get; }

        internal float Bound { get; }

        private LilToonAlphaGate(
            string property,
            float bound,
            bool refusesBelow)
        {
            Property = property;
            Bound = bound;
            this.refusesBelow = refusesBelow;
        }

        /// <summary>
        /// A gate a finite value satisfies at or above
        /// <paramref name="bound"/>; strictly below it refuses.
        /// </summary>
        internal static LilToonAlphaGate Minimum(
            string property,
            float bound)
        {
            return new LilToonAlphaGate(property, bound, true);
        }

        /// <summary>
        /// A gate a finite value satisfies at or below
        /// <paramref name="bound"/>; strictly above it refuses.
        /// </summary>
        internal static LilToonAlphaGate Maximum(
            string property,
            float bound)
        {
            return new LilToonAlphaGate(property, bound, false);
        }

        internal bool Refuses(float value)
        {
            return refusesBelow ? value < Bound : value > Bound;
        }
    }
}
