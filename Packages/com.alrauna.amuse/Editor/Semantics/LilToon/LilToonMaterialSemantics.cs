using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Alrauna.Amuse.Editor.Host;
using UnityEngine;
using static Alrauna.Amuse.Editor.Semantics.EvidenceGates;
using static Alrauna.Amuse.Editor.Semantics.LilToon.LilToonUnknownRecord;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// Semantic role a diagnostic is scoped to. The declared order is the
    /// deterministic diagnostic order: material-wide first, then each output.
    /// </summary>
    internal enum LilToonSemanticOutput
    {
        Material,
        BaseColor,
        Alpha,
        Emission,
        Normal,
    }

    /// <summary>
    /// Closed set of diagnostic reasons. A small fixed vocabulary, not a
    /// logging framework: no severities, no free-form categories. It is
    /// deliberately lilToon-specific; the Poiyomi frontend keeps its own.
    /// </summary>
    internal enum LilToonSemanticDiagnosticCode
    {
        UnsupportedShader,
        UnsupportedShaderVariant,
        UnsupportedVersion,
        ModifiedShaderSource,
        MissingSourceEvidence,
        MissingFeatureCompilation,
        UnsupportedFeature,
        UnsupportedUv,
        UnsupportedSampling,
        UnstableTextureIdentity,
        UnsupportedColorSpace,
        UnsupportedTextureImport,

        /// <summary>
        /// A supported vendor feature is active, and the rule for it is
        /// retention, not refusal. The property still names the fact, and the
        /// alpha answer stays Unknown because no triangle can move.
        /// </summary>
        FeatureRetention,
    }

    /// <summary>
    /// One deterministic reason that a material is unsupported or that an
    /// output is <c>Unknown</c>. Diagnostics are data; the frontend never
    /// writes the Unity Console.
    /// </summary>
    internal sealed class LilToonSemanticDiagnostic
    {
        internal LilToonSemanticOutput Output { get; }
        internal LilToonSemanticDiagnosticCode Code { get; }
        internal string Detail { get; }

        internal LilToonSemanticDiagnostic(
            LilToonSemanticOutput output,
            LilToonSemanticDiagnosticCode code,
            string detail)
        {
            Output = output;
            Code = code;
            Detail = detail ?? throw new ArgumentNullException(nameof(detail));
        }
    }

    /// <summary>
    /// Immutable outcome of interpreting one base material: whether the
    /// material's source identity is supported, the normalized semantics, and
    /// deterministic output-scoped diagnostics.
    /// </summary>
    internal sealed class LilToonSemanticResult
    {
        internal bool IsSupportedMaterial { get; }
        internal MaterialSemantics Semantics { get; }
        internal IReadOnlyList<LilToonSemanticDiagnostic> Diagnostics { get; }

        internal LilToonSemanticResult(
            bool isSupportedMaterial,
            MaterialSemantics semantics,
            IReadOnlyList<LilToonSemanticDiagnostic> diagnostics)
        {
            if (semantics == null)
            {
                throw new ArgumentNullException(nameof(semantics));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            IsSupportedMaterial = isSupportedMaterial;
            Semantics = semantics;

            var copy = new LilToonSemanticDiagnostic[diagnostics.Count];
            for (var i = 0; i < diagnostics.Count; i++)
            {
                copy[i] = diagnostics[i]
                    ?? throw new ArgumentException(
                        "Diagnostics must not contain null entries.",
                        nameof(diagnostics));
            }

            Diagnostics =
                new ReadOnlyCollection<LilToonSemanticDiagnostic>(copy);
        }
    }

    /// <summary>
    /// Conservative Editor-only interpreter for the canonical generated lilToon
    /// 2.3.4 base opaque shader. It attests source identity, then maps one
    /// supplied base <see cref="Material"/> into the immutable
    /// <see cref="MaterialSemantics"/> vocabulary. Behavior the attested source
    /// cannot prove is returned as an <c>Unknown</c> output with a deterministic
    /// diagnostic; it is never guessed, and additional uncertainty never widens
    /// a supported claim.
    /// </summary>
    internal static class LilToonMaterialSemantics
    {
        /// <summary>
        /// Analyzes the current values of one supplied base material. It does
        /// not assert that later animation, material swaps, modifier
        /// processing, or renderer overrides leave that state effective.
        /// </summary>
        internal static LilToonSemanticResult AnalyzeBaseMaterial(Material material)
        {
            RequireAnalyzableMaterial(material);

            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material, FullMaterialEvidenceRequest),
            })[0];
            var evidence = LilToonSourceAttestation.GatherSourceEvidence(
                material.shader, captured);
            if (!LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    evidence, out var diagnostic))
            {
                return Unsupported(diagnostic);
            }

            return InterpretVerifiedMaterial(
                material,
                QualitySettings.activeColorSpace,
                evidence.CompiledFeatures,
                captured);
        }

        /// <summary>
        /// Narrow friend-test seam. The caller must already have established
        /// that the material's source identity is attested. The explicit color
        /// space and compiled-feature set are the resolved facts the equations
        /// require; passing them lets deterministic tests exercise every
        /// equation without an installed lilToon package or a project-wide
        /// color-space change.
        /// </summary>
        internal static LilToonSemanticResult InterpretVerifiedMaterial(
            Material material,
            ColorSpace activeColorSpace,
            IReadOnlyCollection<string> compiledFeatures)
        {
            RequireAnalyzableMaterial(material);
            if (compiledFeatures == null)
            {
                throw new ArgumentNullException(nameof(compiledFeatures));
            }

            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material, FullMaterialEvidenceRequest),
            })[0];
            return InterpretVerifiedMaterial(
                material,
                activeColorSpace,
                compiledFeatures,
                captured);
        }

        /// <summary>
        /// Interprets one captured material. The caller must have captured
        /// the evidence under <see cref="FullMaterialEvidenceRequest"/> and
        /// must have established admission upstream. The reads below consume
        /// only the captured snapshot. They never touch the live material. A
        /// captured read the evidence cannot answer refuses with a diagnostic
        /// that names the property.
        /// </summary>
        internal static LilToonSemanticResult InterpretVerifiedMaterialFromEvidence(
            CapturedMaterialEvidence captured,
            ColorSpace activeColorSpace,
            IReadOnlyCollection<string> compiledFeatures)
        {
            if (captured == null)
            {
                throw new ArgumentNullException(nameof(captured));
            }

            if (compiledFeatures == null)
            {
                throw new ArgumentNullException(nameof(compiledFeatures));
            }

            // A verified material is a supported material; each output is proven
            // independently and stays Unknown, with a diagnostic, when its
            // equation is not representable.
            var diagnostics = new List<LilToonSemanticDiagnostic>();

            var baseColor = InterpretBaseColor(
                captured, activeColorSpace, diagnostics);
            var alpha = InterpretAlpha(captured, diagnostics);
            var emission = InterpretEmission(
                captured, activeColorSpace, compiledFeatures, diagnostics);
            var normal = InterpretNormal(captured, compiledFeatures, diagnostics);

            return new LilToonSemanticResult(
                true,
                new MaterialSemantics(baseColor, alpha, emission, normal),
                diagnostics);
        }

        private static LilToonSemanticResult InterpretVerifiedMaterial(
            Material material,
            ColorSpace activeColorSpace,
            IReadOnlyCollection<string> compiledFeatures,
            CapturedMaterialEvidence captured)
        {
            // material is the capture's source. The interpreters read only the
            // captured snapshot, so a later mutation of the live material
            // cannot change an already-captured answer.
            return InterpretVerifiedMaterialFromEvidence(
                captured, activeColorSpace, compiledFeatures);
        }

        private const string ColorProperty = "_Color";
        private const string MainTextureProperty = "_MainTex";
        private const string MainTexScrollRotateProperty = "_MainTex_ScrollRotate";
        private const string MainTexHsvgProperty = "_MainTexHSVG";
        private const string MainColorAdjustMaskProperty = "_MainColorAdjustMask";

        private static readonly Vector4 IdentityHsvg = new Vector4(0f, 1f, 1f, 1f);

        // Every block that writes fd.col.rgb before fd.albedo is copied
        // (lil_pass_forward_normal.hlsl:263-443). The alpha-mask, dissolve,
        // dither, depth-fade, fur, and premultiply blocks are excluded at
        // compile time by LIL_RENDER on the opaque variant and need no gate.
        private static readonly string[] BaseColorWriterGates =
        {
            "_Invisible",
            "_ShiftBackfaceUV",
            "_UseParallax",
            "_UsePOM",
            "_UseAudioLink",
            "_UseMain2ndTex",
            "_UseMain3rdTex",
            "_MainGradationStrength",
        };

        /// <summary>
        /// Proves the normalized base-color term: the constant <c>_Color</c> in
        /// linear light, optionally multiplying a single supported
        /// <c>_MainTex</c> sample. lilToon has no tone-correction toggle, so the
        /// identity of <c>lilToneCorrection</c> is proven from its parameter and
        /// from a positively attested sampled range rather than from a feature
        /// flag.
        /// </summary>
        private static SemanticOutput<ColorSemanticValue> InterpretBaseColor(
            CapturedMaterialEvidence evidence,
            ColorSpace activeColorSpace,
            List<LilToonSemanticDiagnostic> diagnostics)
        {
            if (activeColorSpace != ColorSpace.Linear)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedColorSpace,
                    activeColorSpace.ToString());
            }

            var writerGate = FirstFailedZeroGate(evidence, BaseColorWriterGates);
            if (writerGate != null)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    writerGate);
            }

            // lilToneCorrection always runs when compiled in, so the identity
            // must be proven from the parameter itself. Compared per binary32
            // component: Unity's aggregate vector equality is epsilon-based
            // and is intentionally excluded from semantic proof decisions,
            // because a near-identity HSVG still tone-corrects the color.
            if (!evidence.TryGetVector(MainTexHsvgProperty, out var hsvg))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    MainTexHsvgProperty);
            }

            if (hsvg.x != IdentityHsvg.x ||
                hsvg.y != IdentityHsvg.y ||
                hsvg.z != IdentityHsvg.z ||
                hsvg.w != IdentityHsvg.w)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    MainTexHsvgProperty);
            }

            // An assigned adjust mask lerps between corrected and uncorrected
            // colour, a second sample the closed vocabulary cannot express.
            if (evidence.TryGetTexture(
                    MainColorAdjustMaskProperty, out var adjustMask) &&
                adjustMask.IsAssigned)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    MainColorAdjustMaskProperty);
            }

            if (!evidence.TryGetColor(ColorProperty, out var color))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    ColorProperty);
            }

            if (!IsFinite(color.r) || !IsFinite(color.g) || !IsFinite(color.b))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    ColorProperty);
            }

            var linear = color.linear;
            if (!IsFinite(linear.r) || !IsFinite(linear.g) || !IsFinite(linear.b))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    ColorProperty);
            }
            var tint = new Vector3(linear.r, linear.g, linear.b);

            if (!evidence.TryGetTexture(
                    MainTextureProperty, out var mainAssignment))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    MainTextureProperty);
            }

            if (!mainAssignment.IsAssigned)
            {
                return SemanticOutput<ColorSemanticValue>.Complete(
                    ColorSemanticValue.Constant(tint));
            }

            if (!TryGetMainUvMapping(evidence, mainAssignment, out var mapping))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedUv,
                    MainTexScrollRotateProperty);
            }

            if (!mainAssignment.Texture.HasSourceIdentity)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnstableTextureIdentity,
                    MainTextureProperty);
            }
            var sourceId = mainAssignment.Texture.SourceIdentity;

            if (!mainAssignment.Texture.HasSampling)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedSampling,
                    MainTextureProperty);
            }
            var sampling = mainAssignment.Texture.Sampling;

            if (!mainAssignment.Texture.HasColorInterpretation ||
                !mainAssignment.Texture.ColorValuesProvenInUnitRange)
            {
                // lilToneCorrection is the identity only on [0,1]: its saturate
                // calls would clamp anything above 1.
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedTextureImport,
                    MainTextureProperty);
            }
            var interpretation = mainAssignment.Texture.ColorInterpretation;

            var sample = new TextureSample(sourceId, mapping, sampling);
            // Unit-tint simplification is exact per binary32 component: a
            // near-one tint is a real multiplier and must be retained.
            var value = tint.x == 1f && tint.y == 1f && tint.z == 1f
                ? ColorSemanticValue.Texture(sample, interpretation)
                : ColorSemanticValue.TextureTimesConstant(
                    sample, interpretation, tint);
            return SemanticOutput<ColorSemanticValue>.Complete(value);
        }

        /// <summary>
        /// The main UV is always UV0 with <c>_MainTex_ST</c>, valid only at
        /// exactly zero scroll and rotate. lilToon has no main-texture channel
        /// selector.
        /// </summary>
        private static bool TryGetMainUvMapping(
            CapturedMaterialEvidence evidence,
            CapturedTextureAssignment assignment,
            out UvMapping mapping)
        {
            mapping = default;

            if (!evidence.TryGetVector(
                    MainTexScrollRotateProperty, out var scrollRotate) ||
                !IsFinite(scrollRotate) ||
                scrollRotate.x != 0f ||
                scrollRotate.y != 0f ||
                scrollRotate.z != 0f ||
                scrollRotate.w != 0f)
            {
                return false;
            }

            if (!assignment.HasScaleOffset)
            {
                return false;
            }

            var scale = assignment.Scale;
            var offset = assignment.Offset;
            if (!IsFinite(scale) || !IsFinite(offset))
            {
                return false;
            }

            mapping = new UvMapping(0, scale, offset);
            return true;
        }

        // On LIL_RENDER 0 the alpha value is forced to exactly one after every
        // alpha-writing block (lil_pass_forward_normal.hlsl:393-396), and the
        // whole subpass alpha path is excluded by #if LIL_RENDER > 0. Only
        // mechanisms that remove fragments can still change effective coverage.
        private static readonly string[] AlphaCoverageGates =
        {
            "_Invisible",
            "_UDIMDiscardCompile",
        };

        internal static MaterialEvidenceRequest AlphaEvidenceRequest { get; } =
            new MaterialEvidenceRequest(
                shaderName: true,
                activeColorSpace: false,
                presenceProperties: Array.Empty<string>(),
                scalarProperties: new[]
                {
                    LilToonSourceAttestation.ShaderFormatVersionProperty,
                    "_Invisible",
                    "_UDIMDiscardCompile",
                },
                colorProperties: Array.Empty<string>(),
                vectorProperties: Array.Empty<string>(),
                textureProperties:
                    Array.Empty<TexturePropertyEvidenceRequest>());

        /// <summary>
        /// Proves the normalized alpha term. The attested opaque variant forces
        /// alpha to one, so the value is independent of <c>_Color.a</c>,
        /// <c>_MainTex</c> alpha, <c>_AlphaMaskMode</c>, <c>_Cutoff</c>, and
        /// <c>_UseDither</c>. Two coverage gates remain because they remove
        /// fragments rather than change the value.
        /// </summary>
        internal static SemanticOutput<ScalarSemanticValue> InterpretVerifiedAlpha(
            CapturedMaterialEvidence evidence)
        {
            return InterpretVerifiedAlpha(evidence, out _);
        }

        internal static SemanticOutput<ScalarSemanticValue> InterpretVerifiedAlpha(
            CapturedMaterialEvidence evidence,
            out AlphaUnknownReason unknownReason)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            var diagnostics = new List<LilToonSemanticDiagnostic>();
            var alpha = InterpretAlpha(evidence, diagnostics);
            unknownReason = AlphaUnknownReasonFor(diagnostics);
            return alpha;
        }

        /// <summary>
        /// The first Alpha-scoped diagnostic of one interpretation, as the
        /// reason the alpha answer is Unknown. The first failure is the
        /// reported one: the gates refuse at the first fact they cannot
        /// prove, so later diagnostics describe consequences, not causes.
        /// Null when the alpha answer is complete or no Alpha diagnostic
        /// was recorded.
        /// </summary>
        internal static AlphaUnknownReason AlphaUnknownReasonFor(
            IReadOnlyList<LilToonSemanticDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                return null;
            }

            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Output != LilToonSemanticOutput.Alpha)
                {
                    continue;
                }

                return diagnostic.Code ==
                    LilToonSemanticDiagnosticCode.FeatureRetention
                    ? AlphaUnknownReason.FeatureRetention(
                          FeatureLabelFor(diagnostic.Detail),
                          diagnostic.Detail)
                    : AlphaUnknownReason.UnsupportedFeature(
                          FeatureLabelFor(diagnostic.Detail),
                          diagnostic.Detail);
            }

            return null;
        }

        /// <summary>
        /// The plain-words name of the shader feature one property carries,
        /// for the slot-refusal report. Keys are the exact property names
        /// the alpha gates record. A property without an entry reports its
        /// own name alone, which stays exact; the label is only wording.
        /// </summary>
        internal static string FeatureLabelFor(string property)
        {
            if (property == null)
            {
                return null;
            }

            if (property.StartsWith("_IDMask", StringComparison.Ordinal))
            {
                return "ID mask";
            }

            return FeatureLabels.TryGetValue(property, out var label)
                ? label
                : null;
        }

        private static readonly Dictionary<string, string> FeatureLabels =
            new(StringComparer.Ordinal)
            {
                ["_Invisible"] = "Invisible mode",
                ["_UDIMDiscardCompile"] = "UDIM discard",
                ["_UDIMDiscardMode"] = "UDIM discard",
                ["_ShiftBackfaceUV"] = "Backface UV shift",
                ["_UseParallax"] = "Parallax",
                ["_UseDither"] = "Dither",
                ["_IDMaskControlsDissolve"] = "ID mask dissolve control",
                ["_DissolveParams"] = "Dissolve",
                ["_MainTex_ScrollRotate"] = "UV scroll or rotate",
                ["_Main2ndTex_ScrollRotate"] =
                    "Second layer UV scroll or rotate",
                ["_Main3rdTex_ScrollRotate"] =
                    "Third layer UV scroll or rotate",
                ["_UseMain2ndTex"] = "Second layer",
                ["_UseMain3rdTex"] = "Third layer",
                ["_Main2ndDistanceFade"] = "Second layer distance fade",
                ["_Main3rdDistanceFade"] = "Third layer distance fade",
                ["_DistanceFade"] = "Distance fade",
                ["_Main2ndDissolveParams"] = "Second layer dissolve",
                ["_Main3rdDissolveParams"] = "Third layer dissolve",
                ["_AudioLink2Main2nd"] = "Audio Link on the second layer",
                ["_AudioLink2Main3rd"] = "Audio Link on the third layer",
                ["_Main2ndTexIsMSDF"] = "Second layer MSDF",
                ["_Main3rdTexIsMSDF"] = "Third layer MSDF",
                ["_Main2ndTexAngle"] = "Second layer angle",
                ["_Main3rdTexAngle"] = "Third layer angle",
                ["_AlphaMaskMode"] = "Alpha mask",
                ["_AlphaMaskValue"] = "Alpha mask",
                ["_AlphaMaskScale"] = "Alpha mask",
                ["_Cutoff"] = "Cutoff",
                ["_AlphaBoostFA"] = "Alpha boost",
                ["_SubpassCutoff"] = "Subpass cutoff",
                ["_MainTex_ST"] = "Main texture scale or offset",
                ["_MainTex"] = "Main texture",
            };

        private static SemanticOutput<ScalarSemanticValue> InterpretAlpha(
            CapturedMaterialEvidence evidence,
            List<LilToonSemanticDiagnostic> diagnostics)
        {
            var coverageGate = FirstFailedZeroGate(evidence, AlphaCoverageGates);
            if (coverageGate != null)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    coverageGate);
            }

            return SemanticOutput<ScalarSemanticValue>.Complete(
                ScalarSemanticValue.Constant(1f));
        }

        private const string UseEmissionProperty = "_UseEmission";
        private const string EmissionColorProperty = "_EmissionColor";
        private const string EmissionMapProperty = "_EmissionMap";
        private const string EmissionMapUvModeProperty = "_EmissionMap_UVMode";
        private const string EmissionMapScrollRotateProperty =
            "_EmissionMap_ScrollRotate";
        private const string EmissionBlendProperty = "_EmissionBlend";
        private const string EmissionBlendModeProperty = "_EmissionBlendMode";
        private const string EmissionBlendMaskProperty = "_EmissionBlendMask";
        private const string EmissionBlinkProperty = "_EmissionBlink";
        private const string BackfaceColorProperty = "_BackfaceColor";
        private const string DissolveParamsProperty = "_DissolveParams";
        private const string EmissionFirstFeature = "LIL_FEATURE_EMISSION_1ST";
        private const string EmissionMapFeature = "LIL_FEATURE_EmissionMap";

        // Every traced block after fd.albedo that adds light-independent colour.
        // A zero or slot-1 claim is only sound with all of them off.
        private static readonly string[] EmissiveWriterGates =
        {
            "_UseEmission2nd",
            "_UseReflection",
            "_UseMatCap",
            "_UseMatCap2nd",
            "_UseRim",
            "_UseRimShade",
            "_UseGlitter",
            "_UseBacklight",
            "_UseAudioLink",
        };

        // Slot-1 modifiers that re-map, animate, or tint the emission term
        // beyond the supported colour/map form.
        private static readonly string[] EmissionModifierGates =
        {
            "_EmissionMainStrength",
            "_EmissionFluorescence",
            "_EmissionUseGrad",
            "_AudioLink2Emission",
            "_EmissionParallaxDepth",
        };

        /// <summary>
        /// Proves the deliberately narrow emission subset. The emissive-writer
        /// gates and the dissolve gate are checked before the zero
        /// short-circuit, so neither an enabled writer nor an active dissolve
        /// can hide behind <c>_UseEmission == 0</c>. Only blend mode 1 (Add)
        /// yields a true additive term, and the blend factor carries
        /// <c>emissionColor.a</c>, so an RGBA map would scale its own emission.
        /// </summary>
        private static SemanticOutput<ColorSemanticValue> InterpretEmission(
            CapturedMaterialEvidence evidence,
            ColorSpace activeColorSpace,
            IReadOnlyCollection<string> compiledFeatures,
            List<LilToonSemanticDiagnostic> diagnostics)
        {
            var writerGate = FirstFailedZeroGate(evidence, EmissiveWriterGates);
            if (writerGate != null)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    writerGate);
            }

            if (!evidence.TryGetColor(BackfaceColorProperty, out var backfaceColor) ||
                !IsFinite(backfaceColor.a) ||
                backfaceColor.a != 0f)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    BackfaceColorProperty);
            }

            // Dissolve adds its own emissive term and is required inert for both
            // the zero and the slot-1 claim. Only the mode component is proven;
            // no general dissolve semantics are modelled.
            if (!evidence.HasProperty(DissolveParamsProperty))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    DissolveParamsProperty);
            }

            if (!evidence.TryGetVector(DissolveParamsProperty, out var dissolveParams))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    DissolveParamsProperty);
            }

            if (!IsFinite(dissolveParams) || dissolveParams.x != 0f)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    DissolveParamsProperty);
            }

            if (!TryReadBinary(evidence, UseEmissionProperty, out var useEmission))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    UseEmissionProperty);
            }

            // Nothing emits: a proven constant zero, independent of colour space.
            if (!useEmission)
            {
                return SemanticOutput<ColorSemanticValue>.Complete(
                    ColorSemanticValue.Constant(Vector3.zero));
            }

            if (!compiledFeatures.Contains(EmissionFirstFeature))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.MissingFeatureCompilation,
                    EmissionFirstFeature);
            }

            if (activeColorSpace != ColorSpace.Linear)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedColorSpace,
                    activeColorSpace.ToString());
            }

            var modifierGate = FirstFailedZeroGate(evidence, EmissionModifierGates);
            if (modifierGate != null)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    modifierGate);
            }

            // Only Add (1) makes lilBlendColor an additive emission term.
            if (!evidence.TryGetScalar(
                    EmissionBlendModeProperty, out var blendMode))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionBlendModeProperty);
            }

            if (!IsFinite(blendMode) || blendMode != 1f)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionBlendModeProperty);
            }

            // lilCalcBlink is exactly one when blink.x is zero.
            if (!evidence.TryGetVector(EmissionBlinkProperty, out var blink))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionBlinkProperty);
            }

            if (!IsFinite(blink) || blink.x != 0f)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionBlinkProperty);
            }

            // An assigned mask multiplies a second sample this equation omits.
            if (evidence.TryGetTexture(
                    EmissionBlendMaskProperty, out var blendMask) &&
                blendMask.IsAssigned)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionBlendMaskProperty);
            }

            if (!evidence.TryGetScalar(EmissionBlendProperty, out var blend))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionBlendProperty);
            }

            if (!evidence.TryGetColor(EmissionColorProperty, out var color))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionColorProperty);
            }

            if (!IsFinite(blend) ||
                !IsFinite(color.r) || !IsFinite(color.g) ||
                !IsFinite(color.b) || !IsFinite(color.a))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionColorProperty);
            }

            var linear = color.linear;
            var tint = new Vector3(linear.r, linear.g, linear.b) * (blend * color.a);
            if (!IsFinite(tint.x) || !IsFinite(tint.y) || !IsFinite(tint.z))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionColorProperty);
            }

            if (!evidence.TryGetTexture(
                    EmissionMapProperty, out var emissionAssignment))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionMapProperty);
            }

            if (!emissionAssignment.IsAssigned)
            {
                return SemanticOutput<ColorSemanticValue>.Complete(
                    ColorSemanticValue.Constant(tint));
            }

            if (!compiledFeatures.Contains(EmissionMapFeature))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.MissingFeatureCompilation,
                    EmissionMapFeature);
            }

            if (!TryGetEmissionUvMapping(evidence, emissionAssignment, out var mapping))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedUv,
                    EmissionMapUvModeProperty);
            }

            // The emission map declares its own sampler_EmissionMap.
            if (!emissionAssignment.Texture.HasSampling)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedSampling,
                    EmissionMapProperty);
            }
            var sampling = emissionAssignment.Texture.Sampling;

            if (!emissionAssignment.Texture.HasSourceIdentity)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnstableTextureIdentity,
                    EmissionMapProperty);
            }
            var sourceId = emissionAssignment.Texture.SourceIdentity;

            if (!emissionAssignment.Texture.HasColorInterpretation ||
                !emissionAssignment.Texture.SampledAlphaIsProvenOne)
            {
                // emissionColor.a scales the blend, so an RGBA map would scale
                // its own emission: rgb times the same sample's alpha is not
                // representable.
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedTextureImport,
                    EmissionMapProperty);
            }
            var interpretation = emissionAssignment.Texture.ColorInterpretation;

            var sample = new TextureSample(sourceId, mapping, sampling);
            var value = tint.x == 1f && tint.y == 1f && tint.z == 1f
                ? ColorSemanticValue.Texture(sample, interpretation)
                : ColorSemanticValue.TextureTimesConstant(
                    sample, interpretation, tint);
            return SemanticOutput<ColorSemanticValue>.Complete(value);
        }

        /// <summary>
        /// The emission map selects a UV channel and applies its own ST
        /// directly, so it does not compose with the main transform. Mode 4
        /// selects rim UV and is unsupported.
        /// </summary>
        private static bool TryGetEmissionUvMapping(
            CapturedMaterialEvidence evidence,
            CapturedTextureAssignment assignment,
            out UvMapping mapping)
        {
            mapping = default;

            if (!evidence.TryGetScalar(EmissionMapUvModeProperty, out var rawMode))
            {
                return false;
            }

            if (!IsFinite(rawMode))
            {
                return false;
            }

            // Rounding here is safe only because of the `channel != rawMode`
            // guard, which rejects every non-integral value. This is
            // deliberately unlike the _lilToonVersion check, where rounding
            // would normalize a malformed value into the supported one.
            var channel = Mathf.RoundToInt(rawMode);
            if (channel < 0 || channel > 3 || channel != rawMode)
            {
                return false;
            }

            if (!evidence.TryGetVector(
                    EmissionMapScrollRotateProperty, out var scrollRotate) ||
                !IsFinite(scrollRotate) ||
                scrollRotate.x != 0f ||
                scrollRotate.y != 0f ||
                scrollRotate.z != 0f ||
                scrollRotate.w != 0f)
            {
                return false;
            }

            if (!assignment.HasScaleOffset)
            {
                return false;
            }

            var scale = assignment.Scale;
            var offset = assignment.Offset;
            if (!IsFinite(scale) || !IsFinite(offset))
            {
                return false;
            }

            mapping = new UvMapping(channel, scale, offset);
            return true;
        }

        private const string UseBumpMapProperty = "_UseBumpMap";
        private const string BumpMapProperty = "_BumpMap";
        private const string BumpScaleProperty = "_BumpScale";
        private const string NormalFirstFeature = "LIL_FEATURE_NORMAL_1ST";
        private const string BumpMapFeature = "LIL_FEATURE_BumpMap";

        // Enabled writers that perturb, blend, or re-target the tangent-space
        // normal, plus the UV determinants shared with the main sample.
        private static readonly string[] NormalWriterGates =
        {
            "_UseBump2ndMap",
            "_UseAnisotropy",
            "_UseParallax",
            "_UsePOM",
            "_ShiftBackfaceUV",
        };

        /// <summary>
        /// The full opaque request: the alpha request's schema plus every name
        /// the base-color, emission, and normal reads touch, and the texture
        /// facts those reads consume. The capture is a value snapshot, so the
        /// request must carry each fact before the frontend can read it. The
        /// alpha request keeps its exact content. It serves the transferred
        /// analysis and every alpha-only consumer, and widening it would
        /// pollute those captures.
        /// This declaration sits after the four gate arrays on purpose.
        /// Static field initializers run in textual order, so the Combine
        /// expression below needs every gate array initialized first.
        /// </summary>
        internal static MaterialEvidenceRequest FullMaterialEvidenceRequest { get; } =
            MaterialEvidenceRequest.Combine(
                AlphaEvidenceRequest,
                new MaterialEvidenceRequest(
                    shaderName: false,
                    activeColorSpace: false,
                    presenceProperties: new[]
                    {
                        MainTexHsvgProperty,
                        MainTexScrollRotateProperty,
                        DissolveParamsProperty,
                        BumpScaleProperty,
                    },
                    scalarProperties: BaseColorWriterGates
                        .Concat(EmissiveWriterGates)
                        .Concat(EmissionModifierGates)
                        .Concat(NormalWriterGates)
                        .Concat(new[]
                        {
                            UseEmissionProperty,
                            EmissionBlendModeProperty,
                            EmissionBlendProperty,
                            EmissionMapUvModeProperty,
                            UseBumpMapProperty,
                            BumpScaleProperty,
                        })
                        // The gate arrays share four names across outputs. One
                        // request category carries each name once.
                        .Distinct()
                        .ToArray(),
                    colorProperties: new[]
                    {
                        ColorProperty,
                        BackfaceColorProperty,
                        EmissionColorProperty,
                    },
                    vectorProperties: new[]
                    {
                        MainTexHsvgProperty,
                        MainTexScrollRotateProperty,
                        DissolveParamsProperty,
                        EmissionBlinkProperty,
                        EmissionMapScrollRotateProperty,
                    },
                    textureProperties: new[]
                    {
                        new TexturePropertyEvidenceRequest(
                            MainTextureProperty,
                            TextureEvidenceKinds.ScaleOffset |
                            TextureEvidenceKinds.SourceIdentity |
                            TextureEvidenceKinds.Sampling |
                            TextureEvidenceKinds.ColorInterpretation |
                            TextureEvidenceKinds.BoundedColorRange),
                        new TexturePropertyEvidenceRequest(
                            MainColorAdjustMaskProperty,
                            TextureEvidenceKinds.None),
                        new TexturePropertyEvidenceRequest(
                            EmissionMapProperty,
                            TextureEvidenceKinds.ScaleOffset |
                            TextureEvidenceKinds.SourceIdentity |
                            TextureEvidenceKinds.Sampling |
                            TextureEvidenceKinds.ColorInterpretation |
                            TextureEvidenceKinds.SampledAlphaIsOne),
                        new TexturePropertyEvidenceRequest(
                            EmissionBlendMaskProperty,
                            TextureEvidenceKinds.None),
                        new TexturePropertyEvidenceRequest(
                            BumpMapProperty,
                            TextureEvidenceKinds.ScaleOffset |
                            TextureEvidenceKinds.SourceIdentity |
                            TextureEvidenceKinds.CanonicalNormalMap),
                    }));

        /// <summary>
        /// Proves the normalized normal term. The writer gates are validated
        /// <em>before</em> either neutral <c>Unmodified</c> return, so an
        /// independently enabled normal mechanism can never hide behind a
        /// disabled or unassigned first bump map. That is deliberately
        /// conservative: it produces false negatives in configurations where a
        /// gate would in fact be irrelevant, and this milestone does not widen
        /// the analysis to recover them.
        /// </summary>
        private static SemanticOutput<NormalSemanticValue> InterpretNormal(
            CapturedMaterialEvidence evidence,
            IReadOnlyCollection<string> compiledFeatures,
            List<LilToonSemanticDiagnostic> diagnostics)
        {
            if (!TryReadBinary(evidence, UseBumpMapProperty, out var useBumpMap))
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Normal,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    UseBumpMapProperty);
            }

            var writerGate = FirstFailedZeroGate(evidence, NormalWriterGates);
            if (writerGate != null)
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Normal,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    writerGate);
            }

            if (!evidence.TryGetTexture(BumpMapProperty, out var bumpAssignment))
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Normal,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    BumpMapProperty);
            }

            // Nothing is claimed: the toggle is off, or the "bump" default
            // resolves to (0.5,0.5,1,0.5), which lilUnpackNormalScale maps to
            // exactly (0,0,1). Reached only after the writer gates are proven.
            if (!useBumpMap || !bumpAssignment.IsAssigned)
            {
                return SemanticOutput<NormalSemanticValue>.Complete(
                    NormalSemanticValue.Unmodified());
            }

            // A claimed feature must be compiled in: lilToon's per-project
            // setting can strip it while _UseBumpMap stays set, which would make
            // the claim false.
            foreach (var feature in new[] { NormalFirstFeature, BumpMapFeature })
            {
                if (!compiledFeatures.Contains(feature))
                {
                    return RecordUnknown<NormalSemanticValue>(
                        diagnostics,
                        LilToonSemanticOutput.Normal,
                        LilToonSemanticDiagnosticCode.MissingFeatureCompilation,
                        feature);
                }
            }

            var scale = evidence.TryGetScalar(BumpScaleProperty, out var scaleValue)
                ? scaleValue
                : float.NaN;
            if (!IsFinite(scale) || scale != 1f)
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Normal,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    BumpScaleProperty);
            }

            if (!TryGetComposedUvMapping(evidence, bumpAssignment, out var mapping))
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Normal,
                    LilToonSemanticDiagnosticCode.UnsupportedUv,
                    BumpMapProperty);
            }

            // The bump map is sampled with sampler_MainTex, so the sampler state
            // comes from the _MainTex asset, not from _BumpMap.
            if (!evidence.TryGetTexture(
                    MainTextureProperty, out var mainAssignment) ||
                !mainAssignment.IsAssigned ||
                !mainAssignment.Texture.HasSampling)
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Normal,
                    LilToonSemanticDiagnosticCode.UnsupportedSampling,
                    MainTextureProperty);
            }
            var sampling = mainAssignment.Texture.Sampling;

            if (!bumpAssignment.Texture.HasSourceIdentity)
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Normal,
                    LilToonSemanticDiagnosticCode.UnstableTextureIdentity,
                    BumpMapProperty);
            }
            var sourceId = bumpAssignment.Texture.SourceIdentity;

            if (!bumpAssignment.Texture.IsCanonicalNormalMap)
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Normal,
                    LilToonSemanticDiagnosticCode.UnsupportedTextureImport,
                    BumpMapProperty);
            }

            return SemanticOutput<NormalSemanticValue>.Complete(
                NormalSemanticValue.TangentSpaceNormalMap(
                    new TextureSample(sourceId, mapping, sampling)));
        }

        /// <summary>
        /// Secondary maps sample at <c>uvMain * tex_ST.xy + tex_ST.zw</c>, so
        /// their mapping is the composition of the main transform with their
        /// own. The composition of two affine maps is affine, so the closed
        /// <see cref="UvMapping"/> expresses it exactly.
        /// </summary>
        private static bool TryGetComposedUvMapping(
            CapturedMaterialEvidence evidence,
            CapturedTextureAssignment assignment,
            out UvMapping mapping)
        {
            mapping = default;

            if (!evidence.TryGetTexture(
                    MainTextureProperty, out var mainAssignment) ||
                !TryGetMainUvMapping(evidence, mainAssignment, out var main))
            {
                return false;
            }

            if (!assignment.HasScaleOffset)
            {
                return false;
            }

            var scale = assignment.Scale;
            var offset = assignment.Offset;
            if (!IsFinite(scale) || !IsFinite(offset))
            {
                return false;
            }

            var composedScale = new Vector2(
                main.Scale.x * scale.x,
                main.Scale.y * scale.y);
            var composedOffset = new Vector2(
                main.Offset.x * scale.x + offset.x,
                main.Offset.y * scale.y + offset.y);

            if (!IsFinite(composedScale) || !IsFinite(composedOffset))
            {
                return false;
            }

            mapping = new UvMapping(0, composedScale, composedOffset);
            return true;
        }

        private static LilToonSemanticResult Unsupported(
            LilToonSemanticDiagnostic diagnostic)
        {
            return new LilToonSemanticResult(
                false,
                AllUnknown(),
                new[] { diagnostic });
        }
    }
}
