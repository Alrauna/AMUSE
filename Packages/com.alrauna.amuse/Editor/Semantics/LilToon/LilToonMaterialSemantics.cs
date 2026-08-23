using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Alrauna.Amuse.Editor.Host;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

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
                new MaterialEvidenceCaptureInput(material, AlphaEvidenceRequest),
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
                new MaterialEvidenceCaptureInput(material, AlphaEvidenceRequest),
            })[0];
            return InterpretVerifiedMaterial(
                material,
                activeColorSpace,
                compiledFeatures,
                captured);
        }

        private static LilToonSemanticResult InterpretVerifiedMaterial(
            Material material,
            ColorSpace activeColorSpace,
            IReadOnlyCollection<string> compiledFeatures,
            CapturedMaterialEvidence captured)
        {
            // A verified material is a supported material; each output is proven
            // independently and stays Unknown, with a diagnostic, when its
            // equation is not representable.
            var diagnostics = new List<LilToonSemanticDiagnostic>();

            var baseColor = InterpretBaseColor(
                material, activeColorSpace, diagnostics);
            var alpha = InterpretAlpha(captured, diagnostics);
            var emission = InterpretEmission(
                material, activeColorSpace, compiledFeatures, diagnostics);
            var normal = InterpretNormal(material, compiledFeatures, diagnostics);

            return new LilToonSemanticResult(
                true,
                new MaterialSemantics(baseColor, alpha, emission, normal),
                diagnostics);
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
            Material material,
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

            var writerGate = FirstFailedZeroGate(material, BaseColorWriterGates);
            if (writerGate != null)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    writerGate);
            }

            // lilToneCorrection always runs when compiled in, so the identity
            // must be proven from the parameter itself.
            if (!material.HasProperty(MainTexHsvgProperty) ||
                material.GetVector(MainTexHsvgProperty) != IdentityHsvg)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    MainTexHsvgProperty);
            }

            // An assigned adjust mask lerps between corrected and uncorrected
            // colour, a second sample the closed vocabulary cannot express.
            if (material.HasProperty(MainColorAdjustMaskProperty) &&
                material.GetTexture(MainColorAdjustMaskProperty) != null)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    MainColorAdjustMaskProperty);
            }

            var color = material.GetColor(ColorProperty);
            if (!IsFinite(color.r) || !IsFinite(color.g) || !IsFinite(color.b))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    ColorProperty);
            }

            var linear = color.linear;
            var tint = new Vector3(linear.r, linear.g, linear.b);

            var texture = material.GetTexture(MainTextureProperty);
            if (texture == null)
            {
                return SemanticOutput<ColorSemanticValue>.Complete(
                    ColorSemanticValue.Constant(tint));
            }

            if (!TryGetMainUvMapping(material, out var mapping))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedUv,
                    MainTexScrollRotateProperty);
            }

            if (!UnityTextureEvidence.TryGetSourceId(texture, out var sourceId))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnstableTextureIdentity,
                    MainTextureProperty);
            }

            if (!UnityTextureEvidence.TryGetSampling(texture, out var sampling))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedSampling,
                    MainTextureProperty);
            }

            if (!UnityTextureEvidence.TryGetColorInterpretation(
                    texture, out var interpretation) ||
                !TryProveSampledColorInUnitRange(texture))
            {
                // lilToneCorrection is the identity only on [0,1]: its saturate
                // calls would clamp anything above 1.
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedTextureImport,
                    MainTextureProperty);
            }

            var sample = new TextureSample(sourceId, mapping, sampling);
            var value = tint == Vector3.one
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
            Material material,
            out UvMapping mapping)
        {
            mapping = default;

            if (!material.HasProperty(MainTexScrollRotateProperty))
            {
                return false;
            }

            var scrollRotate = material.GetVector(MainTexScrollRotateProperty);
            if (!IsFinite(scrollRotate) || scrollRotate != Vector4.zero)
            {
                return false;
            }

            var scale = material.GetTextureScale(MainTextureProperty);
            var offset = material.GetTextureOffset(MainTextureProperty);
            if (!IsFinite(scale) || !IsFinite(offset))
            {
                return false;
            }

            mapping = new UvMapping(0, scale, offset);
            return true;
        }

        /// <summary>
        /// Positively proves that every effective sampled colour value for this
        /// texture is finite and confined to [0,1], the range in which
        /// <c>lilToneCorrection</c> at <c>_MainTexHSVG = (0,1,1,1)</c> is the
        /// identity.
        ///
        /// Only imported formats on the allow-list below succeed. Every other
        /// format — signed-normalized, half, float, shared-exponent, BC6H — and
        /// every texture whose importer cannot be read, refuses. A format Unity
        /// adds in a future version is not on the list and therefore refuses.
        /// Nothing is clamped, approximated, or assumed bounded. The predicate
        /// is lilToon-local: it has one consumer and a lilToon-specific
        /// justification, so it does not belong in the shared texture evidence.
        /// </summary>
        private static bool TryProveSampledColorInUnitRange(Texture texture)
        {
            if (texture == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path) ||
                !(AssetImporter.GetAtPath(path) is TextureImporter))
            {
                return false;
            }

            return BoundedColorFormats.Contains(texture.graphicsFormat);
        }

        /// <summary>
        /// Unsigned-normalized and sRGB formats, whose decoded values are
        /// exactly the closed interval [0,1]. Enumerated rather than
        /// pattern-matched so an unrecognized format cannot pass by accident.
        /// </summary>
        private static readonly HashSet<GraphicsFormat> BoundedColorFormats =
            new HashSet<GraphicsFormat>
            {
                GraphicsFormat.R8_UNorm,
                GraphicsFormat.R8G8_UNorm,
                GraphicsFormat.R8G8B8_UNorm,
                GraphicsFormat.R8G8B8A8_UNorm,
                GraphicsFormat.R8G8B8_SRGB,
                GraphicsFormat.R8G8B8A8_SRGB,
                GraphicsFormat.B8G8R8_UNorm,
                GraphicsFormat.B8G8R8A8_UNorm,
                GraphicsFormat.B8G8R8_SRGB,
                GraphicsFormat.B8G8R8A8_SRGB,
                GraphicsFormat.R16_UNorm,
                GraphicsFormat.R16G16_UNorm,
                GraphicsFormat.R16G16B16_UNorm,
                GraphicsFormat.R16G16B16A16_UNorm,
                GraphicsFormat.R5G6B5_UNormPack16,
                GraphicsFormat.R4G4B4A4_UNormPack16,
                GraphicsFormat.R5G5B5A1_UNormPack16,
                GraphicsFormat.RGBA_DXT1_UNorm,
                GraphicsFormat.RGBA_DXT1_SRGB,
                GraphicsFormat.RGBA_DXT3_UNorm,
                GraphicsFormat.RGBA_DXT3_SRGB,
                GraphicsFormat.RGBA_DXT5_UNorm,
                GraphicsFormat.RGBA_DXT5_SRGB,
                GraphicsFormat.R_BC4_UNorm,
                GraphicsFormat.RG_BC5_UNorm,
                GraphicsFormat.RGBA_BC7_UNorm,
                GraphicsFormat.RGBA_BC7_SRGB,
                GraphicsFormat.RGB_ETC_UNorm,
                GraphicsFormat.RGB_ETC2_UNorm,
                GraphicsFormat.RGB_ETC2_SRGB,
                GraphicsFormat.RGB_A1_ETC2_UNorm,
                GraphicsFormat.RGB_A1_ETC2_SRGB,
                GraphicsFormat.RGBA_ETC2_UNorm,
                GraphicsFormat.RGBA_ETC2_SRGB,
                GraphicsFormat.RGBA_ASTC4X4_UNorm,
                GraphicsFormat.RGBA_ASTC4X4_SRGB,
                GraphicsFormat.RGBA_ASTC5X5_UNorm,
                GraphicsFormat.RGBA_ASTC5X5_SRGB,
                GraphicsFormat.RGBA_ASTC6X6_UNorm,
                GraphicsFormat.RGBA_ASTC6X6_SRGB,
                GraphicsFormat.RGBA_ASTC8X8_UNorm,
                GraphicsFormat.RGBA_ASTC8X8_SRGB,
                GraphicsFormat.RGBA_ASTC10X10_UNorm,
                GraphicsFormat.RGBA_ASTC10X10_SRGB,
                GraphicsFormat.RGBA_ASTC12X12_UNorm,
                GraphicsFormat.RGBA_ASTC12X12_SRGB,
            };

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
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            return InterpretAlpha(
                evidence, new List<LilToonSemanticDiagnostic>());
        }

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
            Material material,
            ColorSpace activeColorSpace,
            IReadOnlyCollection<string> compiledFeatures,
            List<LilToonSemanticDiagnostic> diagnostics)
        {
            var writerGate = FirstFailedZeroGate(material, EmissiveWriterGates);
            if (writerGate != null)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    writerGate);
            }

            var backfaceColor = material.GetColor(BackfaceColorProperty);
            if (!IsFinite(backfaceColor.a) || backfaceColor.a != 0f)
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
            if (!material.HasProperty(DissolveParamsProperty))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    DissolveParamsProperty);
            }

            var dissolveParams = material.GetVector(DissolveParamsProperty);
            if (!IsFinite(dissolveParams) || dissolveParams.x != 0f)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    DissolveParamsProperty);
            }

            if (!TryReadBinary(material, UseEmissionProperty, out var useEmission))
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

            var modifierGate = FirstFailedZeroGate(material, EmissionModifierGates);
            if (modifierGate != null)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    modifierGate);
            }

            // Only Add (1) makes lilBlendColor an additive emission term.
            var blendMode = material.GetFloat(EmissionBlendModeProperty);
            if (!IsFinite(blendMode) || blendMode != 1f)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionBlendModeProperty);
            }

            // lilCalcBlink is exactly one when blink.x is zero.
            var blink = material.GetVector(EmissionBlinkProperty);
            if (!IsFinite(blink) || blink.x != 0f)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionBlinkProperty);
            }

            // An assigned mask multiplies a second sample this equation omits.
            if (material.HasProperty(EmissionBlendMaskProperty) &&
                material.GetTexture(EmissionBlendMaskProperty) != null)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionBlendMaskProperty);
            }

            var blend = material.GetFloat(EmissionBlendProperty);
            var color = material.GetColor(EmissionColorProperty);
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

            var texture = material.GetTexture(EmissionMapProperty);
            if (texture == null)
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

            if (!TryGetEmissionUvMapping(material, out var mapping))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedUv,
                    EmissionMapUvModeProperty);
            }

            // The emission map declares its own sampler_EmissionMap.
            if (!UnityTextureEvidence.TryGetSampling(texture, out var sampling))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedSampling,
                    EmissionMapProperty);
            }

            if (!UnityTextureEvidence.TryGetSourceId(texture, out var sourceId))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnstableTextureIdentity,
                    EmissionMapProperty);
            }

            if (!UnityTextureEvidence.TryGetColorInterpretation(
                    texture, out var interpretation) ||
                !UnityTextureEvidence.TryProveSampledAlphaIsOne(texture))
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

            var sample = new TextureSample(sourceId, mapping, sampling);
            var value = tint == Vector3.one
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
            Material material,
            out UvMapping mapping)
        {
            mapping = default;

            var rawMode = material.GetFloat(EmissionMapUvModeProperty);
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

            var scrollRotate = material.GetVector(EmissionMapScrollRotateProperty);
            if (!IsFinite(scrollRotate) || scrollRotate != Vector4.zero)
            {
                return false;
            }

            var scale = material.GetTextureScale(EmissionMapProperty);
            var offset = material.GetTextureOffset(EmissionMapProperty);
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
        /// Proves the normalized normal term. The writer gates are validated
        /// <em>before</em> either neutral <c>Unmodified</c> return, so an
        /// independently enabled normal mechanism can never hide behind a
        /// disabled or unassigned first bump map. That is deliberately
        /// conservative: it produces false negatives in configurations where a
        /// gate would in fact be irrelevant, and this milestone does not widen
        /// the analysis to recover them.
        /// </summary>
        private static SemanticOutput<NormalSemanticValue> InterpretNormal(
            Material material,
            IReadOnlyCollection<string> compiledFeatures,
            List<LilToonSemanticDiagnostic> diagnostics)
        {
            if (!TryReadBinary(material, UseBumpMapProperty, out var useBumpMap))
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Normal,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    UseBumpMapProperty);
            }

            var writerGate = FirstFailedZeroGate(material, NormalWriterGates);
            if (writerGate != null)
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Normal,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    writerGate);
            }

            var texture = material.GetTexture(BumpMapProperty);

            // Nothing is claimed: the toggle is off, or the "bump" default
            // resolves to (0.5,0.5,1,0.5), which lilUnpackNormalScale maps to
            // exactly (0,0,1). Reached only after the writer gates are proven.
            if (!useBumpMap || texture == null)
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

            var scale = material.HasProperty(BumpScaleProperty)
                ? material.GetFloat(BumpScaleProperty)
                : float.NaN;
            if (!IsFinite(scale) || scale != 1f)
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Normal,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    BumpScaleProperty);
            }

            if (!TryGetComposedUvMapping(material, BumpMapProperty, out var mapping))
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Normal,
                    LilToonSemanticDiagnosticCode.UnsupportedUv,
                    BumpMapProperty);
            }

            // The bump map is sampled with sampler_MainTex, so the sampler state
            // comes from the _MainTex asset, not from _BumpMap.
            var mainTexture = material.GetTexture(MainTextureProperty);
            if (!UnityTextureEvidence.TryGetSampling(mainTexture, out var sampling))
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Normal,
                    LilToonSemanticDiagnosticCode.UnsupportedSampling,
                    MainTextureProperty);
            }

            if (!UnityTextureEvidence.TryGetSourceId(texture, out var sourceId))
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Normal,
                    LilToonSemanticDiagnosticCode.UnstableTextureIdentity,
                    BumpMapProperty);
            }

            if (!UnityTextureEvidence.IsCanonicalNormalMapImport(texture))
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
            Material material,
            string textureProperty,
            out UvMapping mapping)
        {
            mapping = default;

            if (!TryGetMainUvMapping(material, out var main))
            {
                return false;
            }

            var scale = material.GetTextureScale(textureProperty);
            var offset = material.GetTextureOffset(textureProperty);
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

        /// <summary>
        /// Returns the first property that fails the exact-off gate — missing,
        /// non-finite, or not exactly zero — or null when every property proves
        /// off. Naming the offending property lets an output diagnostic point at
        /// the exact enabled feature.
        /// </summary>
        private static string FirstFailedZeroGate(
            Material material,
            params string[] properties)
        {
            foreach (var property in properties)
            {
                if (!material.HasProperty(property))
                {
                    return property;
                }

                var value = material.GetFloat(property);
                if (!IsFinite(value) || value != 0f)
                {
                    return property;
                }
            }

            return null;
        }

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

        /// <summary>
        /// Reads a strictly binary (0 or 1) float flag. A missing, non-finite,
        /// or non-binary value cannot be read as a proven on/off state.
        /// </summary>
        private static bool TryReadBinary(
            Material material,
            string property,
            out bool isSet)
        {
            isSet = false;
            if (!material.HasProperty(property))
            {
                return false;
            }

            var value = material.GetFloat(property);
            if (!IsFinite(value) || (value != 0f && value != 1f))
            {
                return false;
            }

            isSet = value == 1f;
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
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

        private static LilToonSemanticResult Unsupported(
            LilToonSemanticDiagnostic diagnostic)
        {
            return new LilToonSemanticResult(
                false,
                AllUnknown(),
                new[] { diagnostic });
        }

        private static MaterialSemantics AllUnknown()
        {
            return new MaterialSemantics(
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<ScalarSemanticValue>.Unknown(),
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<NormalSemanticValue>.Unknown());
        }
    }
}
