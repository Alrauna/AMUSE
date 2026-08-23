using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Alrauna.Amuse.Editor.Host;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// Conservative Editor-only interpreter for the canonical unlocked Poiyomi
    /// Toon Shader 9.3.64 source. It attests shader identity, then maps one
    /// supplied base <see cref="Material"/> into the immutable
    /// <see cref="MaterialSemantics"/> vocabulary. Behavior that the pinned
    /// source cannot prove is returned as an <c>Unknown</c> output with a
    /// deterministic diagnostic; it is never guessed, and additional
    /// uncertainty never widens a supported claim.
    /// </summary>
    internal static class PoiyomiMaterialSemantics
    {
        // --- Pinned canonical source identity (Poiyomi Toon Shader 9.3.64) ---
        // Tag commit e125e1c33cbfb860f59330799dd4d10a1097242d. See the design
        // doc's authoritative research basis before changing any constant.
        internal const string PoiyomiToonShaderName = ".poiyomi/Poiyomi Toon";
        internal const string PoiyomiPackageName = "com.poiyomi.toon";
        internal const string PoiyomiPackageVersion = "9.3.64";
        internal const string CanonicalShaderGuid =
            "9444ce77bf4418748b1e8591b9d97f85";
        internal const string CanonicalNormalizedSourceHash =
            "31f2ff15615c5e2ac9b05fea08b6310731394d1b5a928b16048e7bde8f8b1755";

        private const string ShaderOptimizerEnabledProperty =
            "_ShaderOptimizerEnabled";

        private const string MainTextureProperty = "_MainTex";
        private const string ColorProperty = "_Color";
        private const string MainTexUvProperty = "_MainTexUV";
        private const string MainTexPanProperty = "_MainTexPan";
        private const string AlphaForceOpaqueProperty = "_AlphaForceOpaque";
        private const string IgnoreMainTexAlphaProperty = "_MainIgnoreTexAlpha";
        private const string NormalMapProperty = "_BumpMap";
        private const string NormalMapUvProperty = "_BumpMapUV";
        private const string NormalMapPanProperty = "_BumpMapPan";
        private const string NormalStrengthProperty = "_BumpScale";
        private const string NormalStochasticProperty = "_BumpMapStochastic";
        private const string EmissionEnable0Property = "_EnableEmission";
        private const string EmissionColorProperty = "_EmissionColor";
        private const string EmissionStrengthProperty = "_EmissionStrength";
        private const string EmissionMapProperty = "_EmissionMap";
        private const string EmissionMapUvProperty = "_EmissionMapUV";
        private const string EmissionMapPanProperty = "_EmissionMapPan";
        private const string EmissionMaskProperty = "_EmissionMask";

        // Minimum property schema the pinned source must expose. Later outputs
        // read more properties; each is added here as its interpreter needs it,
        // so a material missing a consumed property fails identity rather than
        // throwing during interpretation.
        private static readonly string[] RequiredSchemaProperties =
        {
            "shader_master_label",
            "_ShaderOptimizerEnabled",
            "_MainTex",
            "_Color",
            "_BumpMap",
            "_EmissionMap",
            "_EnableEmission",
            "_EnableEmission1",
            "_EnableEmission2",
            "_EnableEmission3",
        };

        private static readonly string[] AlphaRequiredSchemaProperties =
        {
            "shader_master_label",
            ShaderOptimizerEnabledProperty,
            MainTextureProperty,
            ColorProperty,
        };

        // _MainTex sampling-mode flags whose enabled state changes sampling
        // beyond the single supported tap. Proven exactly off before a texture
        // sample is claimed for any output.
        private static readonly string[] MainSamplingModeGates =
        {
            "_MainPixelMode",
            "_MainTexStochastic",
        };

        // Every enabled source block the pinned source uses to add to, tint, or
        // replace the normalized main color term, plus the color-theme selector.
        // Each is proven exactly off before BaseColor claims a representable
        // equation; a name missing from the schema fails the gate, so BaseColor
        // stays Unknown rather than over-claiming.
        private static readonly string[] BaseColorFeatureGates =
        {
            "_ColorThemeIndex",
            "_MainColorAdjustToggle",
            "_MainHueShiftToggle",
            "_MainHueALCTEnabled",
            "_DetailEnabled",
            "_MainVertexColoringEnabled",
            "_BackFaceEnabled",
            "_RGBMaskEnabled",
            "_EnableDissolve",
            "_DecalEnabled",
            "_DecalEnabled1",
            "_DecalEnabled2",
            "_DecalEnabled3",
            "_EnableAniso",
            "_MatcapEnable",
            "_Matcap2Enable",
            "_Matcap3Enable",
            "_Matcap4Enable",
            "_CubeMapEnabled",
            "_EnableAudioLink",
            "_EnableFlipbook",
            "_EnableRimLighting",
            "_EnableRim2Lighting",
            "_EnableDepthRimLighting",
            "_EnableEnvironmentalRim",
            "_GlitterEnable",
            "_StylizedSpecular",
            "_EnablePathing",
            "_EnableMirrorOptions",
            "_MirrorTextureEnabled",
            "_TextEnabled",
            "_PoiInternalParallax",
            "_PoiParallax",
            "_VideoEffectsEnable",
            "_EnableTouchGlow",
            "_VoronoiEnabled",
            "_EnableTruchet",
            "_EmissionReplace0",
            "_EmissionReplace1",
            "_EmissionReplace2",
            "_EmissionReplace3",
            "_AlphaPremultiply",
        };

        // Coverage/clip mechanisms that change effective alpha coverage even
        // when the alpha value is forced opaque. Proven off on every alpha path,
        // including the forced-opaque short-circuit.
        private static readonly string[] AlphaCoverageGates =
        {
            "_AlphaToCoverage",
            "_AlphaSharpenedA2C",
            "_AlphaDithering",
            "_EnableDissolve",
            "_EnableUDIMDiscardOptions",
        };

        // Enabled writers/masks that add to or replace the non-forced alpha
        // term. The alpha mask mode is an exact-off gate: its supported value is
        // 0 (off); any masking mode needs a second sample this equation omits.
        private static readonly string[] AlphaFeatureGates =
        {
            "_AlphaMod",
            "_MainAlphaMaskMode",
            "_AlphaDistanceFade",
            "_AlphaFresnel",
            "_AlphaAngular",
            "_AlphaAudioLinkEnabled",
            "_EnableAudioLink",
            "_AlphaGlobalMask",
            "_AlphaPremultiply",
            "_BackFaceEnabled",
            "_RGBMaskEnabled",
            "_DecalEnabled",
            "_DecalEnabled1",
            "_DecalEnabled2",
            "_DecalEnabled3",
            "_EnableFlipbook",
            "_EnableRimLighting",
            "_EnableRim2Lighting",
            "_EnableDepthRimLighting",
            "_EnableEnvironmentalRim",
            "_VideoEffectsEnable",
            "_EnableTouchGlow",
            "_MainVertexColoringEnabled",
        };

        internal static MaterialEvidenceRequest AlphaEvidenceRequest { get; } =
            CreateAlphaEvidenceRequest();

        private static MaterialEvidenceRequest FullMaterialEvidenceRequest { get; } =
            MaterialEvidenceRequest.Combine(
                AlphaEvidenceRequest,
                new MaterialEvidenceRequest(
                    shaderName: false,
                    activeColorSpace: false,
                    presenceProperties: RequiredSchemaProperties,
                    scalarProperties: Array.Empty<string>(),
                    colorProperties: Array.Empty<string>(),
                    vectorProperties: Array.Empty<string>(),
                    textureProperties:
                        Array.Empty<TexturePropertyEvidenceRequest>()));

        // Enabled source blocks the pinned source uses to perturb or replace
        // the tangent-space normal: detail normals, RGBA-mask normal
        // replacement, the four decals, and internal/offset parallax. Each is
        // proven exactly off before Normal claims a tangent-space normal map.
        private static readonly string[] NormalFeatureGates =
        {
            "_DetailEnabled",
            "_RGBMaskEnabled",
            "_DecalEnabled",
            "_DecalEnabled1",
            "_DecalEnabled2",
            "_DecalEnabled3",
            "_PoiInternalParallax",
            "_PoiParallax",
        };

        // The three higher emission slots. Slot 0 is the only representable
        // emission term; any enabled higher slot sums into emission the closed
        // color vocabulary cannot express. Proven exactly off (readable binary)
        // before any emission claim, including the zero claim.
        private static readonly string[] HigherEmissionSlotEnables =
        {
            "_EnableEmission1",
            "_EnableEmission2",
            "_EnableEmission3",
        };

        // Slot-0 modifiers that add to, tint, animate, or re-map the emission-0
        // term beyond the supported color/map form. _EmissionReplace0 is proven
        // off by the shared BaseColorFeatureGates (it also writes BaseColor), so
        // it is not repeated here. Each name is proven exactly off before
        // Emission claims a representable slot-0 equation.
        private static readonly string[] EmissionSlot0Modifiers =
        {
            "_EmissionColorThemeIndex",
            "_EmissionBaseColorAsMap",
            "_EmissionFluorescence",
            "_EmissionHueShiftEnabled",
            "_EmissionCenterOutEnabled",
            "_EnableGITDEmission",
            "_EmissionBlinkingEnabled",
            "_ScrollingEmission",
            "_EmissionAL0Enabled",
            "_EmissionMaskInvert",
            "_EmissionMask0GlobalMask",
        };

        /// <summary>
        /// Analyzes the current values of one supplied base material. It does
        /// not assert that later animation, material swaps, modifier
        /// processing, or renderer overrides leave that state effective.
        /// </summary>
        internal static PoiyomiSemanticResult AnalyzeBaseMaterial(Material material)
        {
            RequireAnalyzableMaterial(material);

            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material, FullMaterialEvidenceRequest),
            })[0];
            var evidence = GatherSourceEvidence(
                material.shader, captured, RequiredSchemaProperties);
            if (!TryVerifyPoiyomiIdentity(evidence, out var diagnostic))
            {
                return Unsupported(diagnostic);
            }

            return InterpretVerifiedMaterial(
                material,
                QualitySettings.activeColorSpace,
                captured);
        }

        /// <summary>
        /// Narrow friend-test seam. The caller must already have established
        /// that the material exposes the pinned property contract. The explicit
        /// color space is the resolved Unity fact required by the color
        /// equations; it lets deterministic tests exercise linear-light
        /// behavior without mutating the project's color-space setting.
        /// </summary>
        internal static PoiyomiSemanticResult InterpretVerifiedMaterial(
            Material material,
            ColorSpace activeColorSpace)
        {
            RequireAnalyzableMaterial(material);

            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(material, AlphaEvidenceRequest),
            })[0];
            return InterpretVerifiedMaterial(
                material, activeColorSpace, captured);
        }

        private static PoiyomiSemanticResult InterpretVerifiedMaterial(
            Material material,
            ColorSpace activeColorSpace,
            CapturedMaterialEvidence captured)
        {
            // A verified material is a supported material; each output is proven
            // independently and stays Unknown, with a diagnostic, when its
            // equation is not representable.
            var diagnostics = new List<PoiyomiSemanticDiagnostic>();

            var baseColor = InterpretBaseColor(
                material, activeColorSpace, diagnostics);
            var alpha = InterpretAlpha(captured, diagnostics);
            var emission = InterpretEmission(
                material, activeColorSpace, diagnostics);
            var normal = InterpretNormal(material, diagnostics);

            var semantics = new MaterialSemantics(
                baseColor, alpha, emission, normal);
            return new PoiyomiSemanticResult(true, semantics, diagnostics);
        }

        // --- BaseColor equation (Task 4) ------------------------------------

        /// <summary>
        /// Proves the normalized base-color term: the constant <c>_Color</c> in
        /// linear light, optionally multiplying a single supported <c>_MainTex</c>
        /// sample. Any enabled source block that would add to or replace that
        /// term, an unprovable sample, or a non-linear working space keeps the
        /// output Unknown with one diagnostic.
        /// </summary>
        private static SemanticOutput<ColorSemanticValue> InterpretBaseColor(
            Material material,
            ColorSpace activeColorSpace,
            List<PoiyomiSemanticDiagnostic> diagnostics)
        {
            if (activeColorSpace != ColorSpace.Linear)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.BaseColor,
                    PoiyomiSemanticDiagnosticCode.UnsupportedColorSpace,
                    activeColorSpace.ToString());
            }

            var color = material.GetColor(ColorProperty);
            if (!IsFinite(color.r) || !IsFinite(color.g) || !IsFinite(color.b))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.BaseColor,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    ColorProperty);
            }

            var failedGate = FirstFailedZeroGate(material, BaseColorFeatureGates);
            if (failedGate != null)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.BaseColor,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    failedGate);
            }

            var linear = color.linear;
            var tint = new Vector3(linear.r, linear.g, linear.b);

            if (material.GetTexture(MainTextureProperty) == null)
            {
                return SemanticOutput<ColorSemanticValue>.Complete(
                    ColorSemanticValue.Constant(tint));
            }

            if (!TryInterpretMainSample(
                    material,
                    PoiyomiSemanticOutput.BaseColor,
                    requireColorInterpretation: true,
                    diagnostics,
                    out var sample,
                    out var interpretation))
            {
                return SemanticOutput<ColorSemanticValue>.Unknown();
            }

            var value = tint == Vector3.one
                ? ColorSemanticValue.Texture(sample, interpretation)
                : ColorSemanticValue.TextureTimesConstant(
                    sample, interpretation, tint);
            return SemanticOutput<ColorSemanticValue>.Complete(value);
        }

        /// <summary>
        /// Builds the single supported <c>_MainTex</c> sample shared by the
        /// color and alpha equations: a supported UV mapping, no enabled
        /// sampling-mode flag, a stable texture identity, and a supported
        /// sampler. When <paramref name="requireColorInterpretation"/> is set,
        /// the sRGB/linear import must also be provable. Any failure records the
        /// scoped diagnostic and returns false.
        /// </summary>
        private static bool TryInterpretMainSample(
            Material material,
            PoiyomiSemanticOutput output,
            bool requireColorInterpretation,
            List<PoiyomiSemanticDiagnostic> diagnostics,
            out TextureSample sample,
            out TextureColorInterpretation interpretation)
        {
            sample = default;
            interpretation = default;

            if (!TryGetSupportedUvMapping(
                    material,
                    MainTextureProperty,
                    MainTexUvProperty,
                    MainTexPanProperty,
                    out var mapping))
            {
                AddDiagnostic(
                    diagnostics,
                    output,
                    PoiyomiSemanticDiagnosticCode.UnsupportedUv,
                    MainTexUvProperty);
                return false;
            }

            var failedGate = FirstFailedZeroGate(material, MainSamplingModeGates);
            if (failedGate != null)
            {
                AddDiagnostic(
                    diagnostics,
                    output,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    failedGate);
                return false;
            }

            var texture = material.GetTexture(MainTextureProperty);
            if (!TryGetAssignedTextureSourceId(texture, out var sourceId))
            {
                AddDiagnostic(
                    diagnostics,
                    output,
                    PoiyomiSemanticDiagnosticCode.UnstableTextureIdentity,
                    MainTextureProperty);
                return false;
            }

            if (!TryGetMainTextureSampling(material, out var sampling))
            {
                AddDiagnostic(
                    diagnostics,
                    output,
                    PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                    MainTextureProperty);
                return false;
            }

            if (requireColorInterpretation &&
                !TryGetColorInterpretation(texture, out interpretation))
            {
                AddDiagnostic(
                    diagnostics,
                    output,
                    PoiyomiSemanticDiagnosticCode.UnsupportedTextureImport,
                    MainTextureProperty);
                return false;
            }

            sample = new TextureSample(sourceId, mapping, sampling);
            return true;
        }

        private static bool TryInterpretMainSample(
            CapturedMaterialEvidence evidence,
            PoiyomiSemanticOutput output,
            bool requireColorInterpretation,
            List<PoiyomiSemanticDiagnostic> diagnostics,
            out TextureSample sample,
            out TextureColorInterpretation interpretation)
        {
            sample = default;
            interpretation = default;

            if (!TryGetSupportedUvMapping(evidence, out var mapping))
            {
                AddDiagnostic(
                    diagnostics,
                    output,
                    PoiyomiSemanticDiagnosticCode.UnsupportedUv,
                    MainTexUvProperty);
                return false;
            }

            var failedGate = FirstFailedZeroGate(
                evidence, MainSamplingModeGates);
            if (failedGate != null)
            {
                AddDiagnostic(
                    diagnostics,
                    output,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    failedGate);
                return false;
            }

            if (!evidence.TryGetTexture(
                    MainTextureProperty, out var assignment) ||
                !assignment.IsAssigned ||
                assignment.Texture == null ||
                !assignment.Texture.HasSourceIdentity)
            {
                AddDiagnostic(
                    diagnostics,
                    output,
                    PoiyomiSemanticDiagnosticCode.UnstableTextureIdentity,
                    MainTextureProperty);
                return false;
            }

            if (!assignment.Texture.HasSampling)
            {
                AddDiagnostic(
                    diagnostics,
                    output,
                    PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                    MainTextureProperty);
                return false;
            }

            if (requireColorInterpretation &&
                !assignment.Texture.HasColorInterpretation)
            {
                AddDiagnostic(
                    diagnostics,
                    output,
                    PoiyomiSemanticDiagnosticCode.UnsupportedTextureImport,
                    MainTextureProperty);
                return false;
            }

            interpretation = assignment.Texture.ColorInterpretation;
            sample = new TextureSample(
                assignment.Texture.SourceIdentity,
                mapping,
                assignment.Texture.Sampling);
            return true;
        }

        private static bool TryGetSupportedUvMapping(
            CapturedMaterialEvidence evidence,
            out UvMapping mapping)
        {
            mapping = default;
            if (!evidence.TryGetScalar(
                    MainTexUvProperty, out var rawChannel) ||
                !IsFinite(rawChannel))
            {
                return false;
            }

            var channel = Mathf.RoundToInt(rawChannel);
            if (channel < 0 || channel > 3 || channel != rawChannel)
            {
                return false;
            }

            if (!evidence.TryGetVector(MainTexPanProperty, out var pan) ||
                !IsFinite(pan) ||
                pan.x != 0f || pan.y != 0f || pan.z != 0f || pan.w != 0f)
            {
                return false;
            }

            if (!evidence.TryGetTexture(
                    MainTextureProperty, out var assignment) ||
                !assignment.HasScaleOffset ||
                !IsFinite(assignment.Scale) ||
                !IsFinite(assignment.Offset))
            {
                return false;
            }

            mapping = new UvMapping(
                channel, assignment.Scale, assignment.Offset);
            return true;
        }

        // --- Alpha equation (Task 4) ----------------------------------------

        /// <summary>
        /// Proves the normalized alpha term. Coverage/clip mechanisms must be
        /// off on every path. A forced-opaque material is a constant one;
        /// otherwise alpha is <c>_Color.a</c>, optionally multiplying the alpha
        /// channel of a single supported <c>_MainTex</c> sample. Any enabled
        /// alpha writer, non-binary flag, or unprovable sample keeps the output
        /// Unknown with one diagnostic. Alpha is a raw scalar, so no color-import
        /// evidence is required.
        /// </summary>
        internal static SemanticOutput<ScalarSemanticValue> InterpretVerifiedAlpha(
            CapturedMaterialEvidence evidence)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            return InterpretAlpha(
                evidence, new List<PoiyomiSemanticDiagnostic>());
        }

        private static SemanticOutput<ScalarSemanticValue> InterpretAlpha(
            CapturedMaterialEvidence evidence,
            List<PoiyomiSemanticDiagnostic> diagnostics)
        {
            var coverageGate = FirstFailedZeroGate(evidence, AlphaCoverageGates);
            if (coverageGate != null)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    coverageGate);
            }

            if (!TryReadBinary(
                    evidence, AlphaForceOpaqueProperty, out var forceOpaque))
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    AlphaForceOpaqueProperty);
            }

            if (forceOpaque)
            {
                return SemanticOutput<ScalarSemanticValue>.Complete(
                    ScalarSemanticValue.Constant(1f));
            }

            var featureGate = FirstFailedZeroGate(evidence, AlphaFeatureGates);
            if (featureGate != null)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    featureGate);
            }

            if (!TryReadBinary(
                    evidence, IgnoreMainTexAlphaProperty, out var ignoreAlpha))
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    IgnoreMainTexAlphaProperty);
            }

            if (!evidence.TryGetColor(ColorProperty, out var color) ||
                !IsFinite(color.a))
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    ColorProperty);
            }

            var colorAlpha = color.a;
            if (!evidence.TryGetTexture(
                    MainTextureProperty, out var mainTexture))
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    MainTextureProperty);
            }

            if (ignoreAlpha || !mainTexture.IsAssigned)
            {
                return SemanticOutput<ScalarSemanticValue>.Complete(
                    ScalarSemanticValue.Constant(colorAlpha));
            }

            if (!TryInterpretMainSample(
                    evidence,
                    PoiyomiSemanticOutput.Alpha,
                    requireColorInterpretation: false,
                    diagnostics,
                    out var sample,
                    out _))
            {
                return SemanticOutput<ScalarSemanticValue>.Unknown();
            }

            var value = colorAlpha == 1f
                ? ScalarSemanticValue.Texture(sample, TextureChannel.Alpha)
                : ScalarSemanticValue.TextureTimesConstant(
                    sample, TextureChannel.Alpha, colorAlpha);
            return SemanticOutput<ScalarSemanticValue>.Complete(value);
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

        private static bool TryReadBinary(
            CapturedMaterialEvidence evidence,
            string property,
            out bool isSet)
        {
            isSet = false;
            if (!evidence.TryGetScalar(property, out var value) ||
                !IsFinite(value) || (value != 0f && value != 1f))
            {
                return false;
            }

            isSet = value == 1f;
            return true;
        }

        // --- Emission equation (Task 6) -------------------------------------

        /// <summary>
        /// Proves the deliberately narrow emission subset. Emission is a constant
        /// zero when every slot and every traced emissive writer is off;
        /// otherwise it is the linear-light slot-0 <c>_EmissionColor</c> times a
        /// finite <c>_EmissionStrength</c>, optionally multiplying a single
        /// supported <c>_EmissionMap</c> sample whose sampled alpha is provably
        /// one. A higher slot, any slot-0 modifier, any traced external emissive
        /// writer, an assigned emission mask, a non-linear working space, a
        /// non-provable map, or a non-finite control keeps the output Unknown
        /// with one diagnostic. Additive, same-sample-alpha, layered, and
        /// expression forms are never invented in the immutable vocabulary.
        /// </summary>
        private static SemanticOutput<ColorSemanticValue> InterpretEmission(
            Material material,
            ColorSpace activeColorSpace,
            List<PoiyomiSemanticDiagnostic> diagnostics)
        {
            // form: only slot 0 is representable.
            foreach (var slot in HigherEmissionSlotEnables)
            {
                if (!TryReadBinary(material, slot, out var higherEnabled) ||
                    higherEnabled)
                {
                    return RecordUnknown<ColorSemanticValue>(
                        diagnostics,
                        PoiyomiSemanticOutput.Emission,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        slot);
                }
            }

            // Every traced external emissive writer (decals, rim, matcaps,
            // flipbook, dissolve, ...) emits alongside the slots, so a zero or
            // slot-0 claim is only sound with the whole simple-feature profile
            // off. Gating before the zero short-circuit is what stops a
            // decal-lit, slots-off material from being called a false zero.
            var writerGate = FirstFailedZeroGate(material, BaseColorFeatureGates);
            if (writerGate != null)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Emission,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    writerGate);
            }

            if (!TryReadBinary(
                    material, EmissionEnable0Property, out var slot0Enabled))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Emission,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionEnable0Property);
            }

            // Nothing emits: a proven constant zero, independent of the working
            // color space.
            if (!slot0Enabled)
            {
                return SemanticOutput<ColorSemanticValue>.Complete(
                    ColorSemanticValue.Constant(Vector3.zero));
            }

            // slot 0 on: a linear-light color/map term.
            if (activeColorSpace != ColorSpace.Linear)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Emission,
                    PoiyomiSemanticDiagnosticCode.UnsupportedColorSpace,
                    activeColorSpace.ToString());
            }

            var modifierGate =
                FirstFailedZeroGate(material, EmissionSlot0Modifiers);
            if (modifierGate != null)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Emission,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    modifierGate);
            }

            // An assigned emission mask multiplies a second sample this equation
            // omits; the default "white" slot resolves to no texture.
            if (material.HasProperty(EmissionMaskProperty) &&
                material.GetTexture(EmissionMaskProperty) != null)
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Emission,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionMaskProperty);
            }

            var color = material.HasProperty(EmissionColorProperty)
                ? material.GetColor(EmissionColorProperty)
                : new Color(float.NaN, float.NaN, float.NaN, float.NaN);
            if (!IsFinite(color.r) || !IsFinite(color.g) || !IsFinite(color.b))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Emission,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionColorProperty);
            }

            var strength = material.HasProperty(EmissionStrengthProperty)
                ? material.GetFloat(EmissionStrengthProperty)
                : float.NaN;
            if (!IsFinite(strength))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Emission,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    EmissionStrengthProperty);
            }

            var linear = color.linear;
            var tint = new Vector3(linear.r, linear.g, linear.b) * strength;

            if (material.GetTexture(EmissionMapProperty) == null)
            {
                return SemanticOutput<ColorSemanticValue>.Complete(
                    ColorSemanticValue.Constant(tint));
            }

            if (!TryGetSupportedUvMapping(
                    material,
                    EmissionMapProperty,
                    EmissionMapUvProperty,
                    EmissionMapPanProperty,
                    out var mapping))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Emission,
                    PoiyomiSemanticDiagnosticCode.UnsupportedUv,
                    EmissionMapUvProperty);
            }

            // The emission map is sampled with the shared _MainTex sampler.
            if (!TryGetMainTextureSampling(material, out var sampling))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Emission,
                    PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                    MainTextureProperty);
            }

            var texture = material.GetTexture(EmissionMapProperty);
            if (!TryGetAssignedTextureSourceId(texture, out var sourceId))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Emission,
                    PoiyomiSemanticDiagnosticCode.UnstableTextureIdentity,
                    EmissionMapProperty);
            }

            if (!TryGetColorInterpretation(texture, out var interpretation))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Emission,
                    PoiyomiSemanticDiagnosticCode.UnsupportedTextureImport,
                    EmissionMapProperty);
            }

            // Independent RGB requires a sampled alpha of exactly one; an RGBA
            // map's sample alpha would otherwise silently scale the emission.
            if (!TryProveSampledAlphaIsOne(texture))
            {
                return RecordUnknown<ColorSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Emission,
                    PoiyomiSemanticDiagnosticCode.UnsupportedTextureImport,
                    EmissionMapProperty);
            }

            var sample = new TextureSample(sourceId, mapping, sampling);
            var value = tint == Vector3.one
                ? ColorSemanticValue.Texture(sample, interpretation)
                : ColorSemanticValue.TextureTimesConstant(
                    sample, interpretation, tint);
            return SemanticOutput<ColorSemanticValue>.Complete(value);
        }

        // --- Normal equation (Task 5) ---------------------------------------

        /// <summary>
        /// Proves the normalized normal term. An absent <c>_BumpMap</c> is the
        /// pinned <c>"bump"</c> default: <c>Unmodified</c>. An assigned map is a
        /// tangent-space normal only at unit <c>_BumpScale</c>, with every traced
        /// normal writer off, a supported UV mapping, no stochastic tap, the
        /// shared <c>_MainTex</c> sampler, a stable identity, and the canonical
        /// Unity normal import. Any other state keeps the output Unknown with one
        /// diagnostic; scale, channel-flip, blend, and multi-normal forms are
        /// never invented in the immutable vocabulary.
        /// </summary>
        private static SemanticOutput<NormalSemanticValue> InterpretNormal(
            Material material,
            List<PoiyomiSemanticDiagnostic> diagnostics)
        {
            // The traced normal writers are independent of _BumpMap: the pinned
            // source's detail-normal blend perturbs the tangent-space normal
            // without reading _BumpMap at all, so an unassigned map leaves it
            // perturbed rather than neutral. The gates are therefore proven
            // before the unassigned-map short-circuit, not after it — an empty
            // slot is not evidence that the output is unaffected. Matches the
            // lilToon frontend's ordering.
            var failedGate = FirstFailedZeroGate(material, NormalFeatureGates);
            if (failedGate != null)
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Normal,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    failedGate);
            }

            if (material.GetTexture(NormalMapProperty) == null)
            {
                return SemanticOutput<NormalSemanticValue>.Complete(
                    NormalSemanticValue.Unmodified());
            }

            // value/form: a canonical tangent-space normal has unit strength.
            var strength = material.HasProperty(NormalStrengthProperty)
                ? material.GetFloat(NormalStrengthProperty)
                : float.NaN;
            if (!IsFinite(strength) || strength != 1f)
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Normal,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    NormalStrengthProperty);
            }

            if (!TryGetSupportedUvMapping(
                    material,
                    NormalMapProperty,
                    NormalMapUvProperty,
                    NormalMapPanProperty,
                    out var mapping))
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Normal,
                    PoiyomiSemanticDiagnosticCode.UnsupportedUv,
                    NormalMapUvProperty);
            }

            var stochasticGate =
                FirstFailedZeroGate(material, NormalStochasticProperty);
            if (stochasticGate != null)
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Normal,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    stochasticGate);
            }

            // The normal is sampled with the shared _MainTex sampler.
            if (!TryGetMainTextureSampling(material, out var sampling))
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Normal,
                    PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                    MainTextureProperty);
            }

            var texture = material.GetTexture(NormalMapProperty);
            if (!TryGetAssignedTextureSourceId(texture, out var sourceId))
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Normal,
                    PoiyomiSemanticDiagnosticCode.UnstableTextureIdentity,
                    NormalMapProperty);
            }

            if (!IsCanonicalNormalMapImport(texture))
            {
                return RecordUnknown<NormalSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Normal,
                    PoiyomiSemanticDiagnosticCode.UnsupportedTextureImport,
                    NormalMapProperty);
            }

            return SemanticOutput<NormalSemanticValue>.Complete(
                NormalSemanticValue.TangentSpaceNormalMap(
                    new TextureSample(sourceId, mapping, sampling)));
        }

        private static SemanticOutput<T> RecordUnknown<T>(
            List<PoiyomiSemanticDiagnostic> diagnostics,
            PoiyomiSemanticOutput output,
            PoiyomiSemanticDiagnosticCode code,
            string detail)
            where T : class
        {
            AddDiagnostic(diagnostics, output, code, detail);
            return SemanticOutput<T>.Unknown();
        }

        private static void AddDiagnostic(
            List<PoiyomiSemanticDiagnostic> diagnostics,
            PoiyomiSemanticOutput output,
            PoiyomiSemanticDiagnosticCode code,
            string detail)
        {
            diagnostics.Add(
                new PoiyomiSemanticDiagnostic(output, code, detail));
        }

        // --- Source attestation ---------------------------------------------

        /// <summary>
        /// Normalizes shader source (drop an optional leading UTF-8 BOM, then
        /// convert CRLF and lone CR to LF) and returns the lowercase-hex
        /// SHA-256 of its UTF-8 bytes. Pinning the normalization accepts the
        /// same official source across line-ending changes while still
        /// rejecting any content edit.
        /// </summary>
        internal static string ComputeNormalizedSourceHash(string rawSource)
        {
            if (rawSource == null)
            {
                throw new ArgumentNullException(nameof(rawSource));
            }

            if (rawSource.Length > 0 && rawSource[0] == '﻿')
            {
                rawSource = rawSource.Substring(1);
            }

            rawSource = rawSource.Replace("\r\n", "\n").Replace("\r", "\n");

            var bytes = new UTF8Encoding(false).GetBytes(rawSource);
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                {
                    builder.Append(value.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// Evaluates the exact identity conjunction against already-read
        /// evidence. Returns true only for the canonical, unlocked source at
        /// the pinned revision; otherwise emits one material-scoped diagnostic
        /// naming the first failed check in documented order.
        /// </summary>
        internal static bool TryVerifyPoiyomiIdentity(
            in PoiyomiSourceEvidence evidence,
            out PoiyomiSemanticDiagnostic diagnostic)
        {
            // 1. Exact shader name and unlocked state.
            if (!string.Equals(
                    evidence.ShaderName,
                    PoiyomiToonShaderName,
                    StringComparison.Ordinal))
            {
                diagnostic = MaterialDiagnostic(
                    PoiyomiSemanticDiagnosticCode.UnsupportedShader,
                    $"shader name '{evidence.ShaderName}'");
                return false;
            }

            if (evidence.IsLocked)
            {
                diagnostic = MaterialDiagnostic(
                    PoiyomiSemanticDiagnosticCode.UnsupportedShader,
                    ShaderOptimizerEnabledProperty);
                return false;
            }

            // 2. Readable asset source, canonical GUID, and package evidence.
            if (!evidence.HasReadableSource)
            {
                diagnostic = MaterialDiagnostic(
                    PoiyomiSemanticDiagnosticCode.MissingSourceEvidence,
                    "shader asset source");
                return false;
            }

            if (!string.Equals(
                    evidence.AssetGuid,
                    CanonicalShaderGuid,
                    StringComparison.Ordinal))
            {
                diagnostic = MaterialDiagnostic(
                    PoiyomiSemanticDiagnosticCode.MissingSourceEvidence,
                    "shader asset GUID");
                return false;
            }

            if (evidence.HasPackage)
            {
                if (!string.Equals(
                        evidence.PackageName,
                        PoiyomiPackageName,
                        StringComparison.Ordinal))
                {
                    diagnostic = MaterialDiagnostic(
                        PoiyomiSemanticDiagnosticCode.MissingSourceEvidence,
                        $"package name '{evidence.PackageName}'");
                    return false;
                }

                if (!string.Equals(
                        evidence.PackageVersion,
                        PoiyomiPackageVersion,
                        StringComparison.Ordinal))
                {
                    diagnostic = MaterialDiagnostic(
                        PoiyomiSemanticDiagnosticCode.UnsupportedVersion,
                        $"package version '{evidence.PackageVersion}'");
                    return false;
                }
            }

            // 3. Normalized source hash.
            if (!string.Equals(
                    evidence.NormalizedSourceHash,
                    CanonicalNormalizedSourceHash,
                    StringComparison.Ordinal))
            {
                diagnostic = MaterialDiagnostic(
                    PoiyomiSemanticDiagnosticCode.ModifiedShaderSource,
                    "normalized source hash");
                return false;
            }

            // 4. Required property schema.
            if (!evidence.HasRequiredSchema)
            {
                diagnostic = MaterialDiagnostic(
                    PoiyomiSemanticDiagnosticCode.ModifiedShaderSource,
                    "required property schema");
                return false;
            }

            diagnostic = null;
            return true;
        }

        internal static PoiyomiSourceEvidence GatherAlphaSourceEvidence(
            Shader shader,
            CapturedMaterialEvidence evidence)
        {
            return GatherSourceEvidence(
                shader, evidence, AlphaRequiredSchemaProperties);
        }

        private static PoiyomiSourceEvidence GatherSourceEvidence(
            Shader shader,
            CapturedMaterialEvidence evidence,
            IReadOnlyCollection<string> requiredSchemaProperties)
        {
            var shaderName = evidence.HasShaderName
                ? evidence.ShaderName
                : null;
            var isLocked = evidence.TryGetScalar(
                ShaderOptimizerEnabledProperty, out var optimizerEnabled) &&
                optimizerEnabled != 0f;

            var assetPath = AssetDatabase.GetAssetPath(shader);

            var hasReadableSource = false;
            string assetGuid = null;
            string normalizedHash = null;
            if (!string.IsNullOrEmpty(assetPath) &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    shader,
                    out var guid,
                    out long _))
            {
                assetGuid = guid?.ToLowerInvariant();
                try
                {
                    if (File.Exists(assetPath))
                    {
                        var rawSource = File.ReadAllText(assetPath, Encoding.UTF8);
                        normalizedHash = ComputeNormalizedSourceHash(rawSource);
                        hasReadableSource = true;
                    }
                }
                catch (IOException)
                {
                    hasReadableSource = false;
                    normalizedHash = null;
                }
                catch (UnauthorizedAccessException)
                {
                    hasReadableSource = false;
                    normalizedHash = null;
                }
            }

            var package =
                UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);

            return new PoiyomiSourceEvidence(
                shaderName,
                isLocked,
                hasReadableSource,
                assetGuid,
                normalizedHash,
                package != null,
                package?.name,
                package?.version,
                HasRequiredSchema(evidence, requiredSchemaProperties));
        }

        private static bool HasRequiredSchema(
            CapturedMaterialEvidence evidence,
            IReadOnlyCollection<string> requiredSchemaProperties)
        {
            foreach (var property in requiredSchemaProperties)
            {
                if (!evidence.HasProperty(property))
                {
                    return false;
                }
            }

            return true;
        }

        private static MaterialEvidenceRequest CreateAlphaEvidenceRequest()
        {
            var scalars = new HashSet<string>(StringComparer.Ordinal)
            {
                ShaderOptimizerEnabledProperty,
                MainTexUvProperty,
                AlphaForceOpaqueProperty,
                IgnoreMainTexAlphaProperty,
            };
            scalars.UnionWith(MainSamplingModeGates);
            scalars.UnionWith(AlphaCoverageGates);
            scalars.UnionWith(AlphaFeatureGates);

            return new MaterialEvidenceRequest(
                shaderName: true,
                activeColorSpace: false,
                presenceProperties: AlphaRequiredSchemaProperties,
                scalarProperties: scalars,
                colorProperties: new[] { ColorProperty },
                vectorProperties: new[] { MainTexPanProperty },
                textureProperties: new[]
                {
                    new TexturePropertyEvidenceRequest(
                        MainTextureProperty,
                        TextureEvidenceKinds.ScaleOffset |
                        TextureEvidenceKinds.SourceIdentity |
                        TextureEvidenceKinds.Sampling |
                        TextureEvidenceKinds.AlphaChannel),
                });
        }

        // --- Texture evidence extraction (Task 3) ---------------------------

        /// <summary>
        /// Resolves the stable project identity of an assigned texture as
        /// <c>unity-asset:&lt;lowercase-guid&gt;:&lt;invariant-decimal-local-id&gt;</c>.
        /// Scene-only, generated, or otherwise unidentifiable textures are
        /// refused; identity is never fabricated from instance id, path, name,
        /// pixels, or reference equality.
        /// </summary>
        internal static bool TryGetAssignedTextureSourceId(
            Texture texture,
            out TextureSourceId sourceId)
        {
            return UnityTextureEvidence.TryGetSourceId(texture, out sourceId);
        }

        /// <summary>
        /// Converts a texture property's UV channel and scale/offset into a
        /// <see cref="UvMapping"/> when they fit the supported form: an exact
        /// integer channel 0-3, exactly zero pan, and finite scale/offset.
        /// </summary>
        internal static bool TryGetSupportedUvMapping(
            Material material,
            string textureProperty,
            string uvChannelProperty,
            string panProperty,
            out UvMapping mapping)
        {
            mapping = default;

            if (!material.HasProperty(uvChannelProperty))
            {
                return false;
            }

            var rawChannel = material.GetFloat(uvChannelProperty);
            if (!IsFinite(rawChannel))
            {
                return false;
            }

            var channel = Mathf.RoundToInt(rawChannel);
            if (channel < 0 || channel > 3 || channel != rawChannel)
            {
                return false;
            }

            if (!material.HasProperty(panProperty))
            {
                return false;
            }

            var pan = material.GetVector(panProperty);
            if (!IsFinite(pan) ||
                pan.x != 0f || pan.y != 0f || pan.z != 0f || pan.w != 0f)
            {
                return false;
            }

            var scale = material.GetTextureScale(textureProperty);
            var offset = material.GetTextureOffset(textureProperty);
            if (!IsFinite(scale) || !IsFinite(offset))
            {
                return false;
            }

            mapping = new UvMapping(channel, scale, offset);
            return true;
        }

        /// <summary>
        /// True only when every named property exists, is finite, and is
        /// exactly zero. Used as the exact-off gate for mode flags and feature
        /// toggles; a missing property cannot prove the feature is off.
        /// </summary>
        internal static bool AreExactlyZero(
            Material material,
            params string[] properties)
        {
            return FirstFailedZeroGate(material, properties) == null;
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
        /// Extracts the sampler declared by <c>_MainTex</c>, which the pinned
        /// shader uses for every assigned sample. Supported only for Point or
        /// Bilinear filtering with equal Clamp/Repeat wrap and no mipmapped,
        /// mip-biased, or anisotropic sampling. A missing <c>_MainTex</c> yields
        /// no sampler; the implicit white sampler is never promoted to a guess.
        /// </summary>
        internal static bool TryGetMainTextureSampling(
            Material material,
            out TextureSampling sampling)
        {
            // "The sampler always comes from _MainTex" is Poiyomi-specific
            // knowledge and stays here; only the texture-level fact is shared.
            var mainTexture = material.HasProperty(MainTextureProperty)
                ? material.GetTexture(MainTextureProperty)
                : null;
            return UnityTextureEvidence.TryGetSampling(mainTexture, out sampling);
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

        /// <summary>
        /// Selects a color texture's linear/sRGB import interpretation from its
        /// <see cref="TextureImporter.sRGBTexture"/> flag. A texture with no
        /// importer (scene-only, generated) cannot prove a color meaning.
        /// </summary>
        internal static bool TryGetColorInterpretation(
            Texture texture,
            out TextureColorInterpretation interpretation)
        {
            return UnityTextureEvidence.TryGetColorInterpretation(
                texture, out interpretation);
        }

        /// <summary>
        /// Proves a sampled alpha of exactly one: the source carries no alpha
        /// channel and the importer imports none. Input or grayscale-derived
        /// alpha is not one and is therefore not proven.
        /// </summary>
        internal static bool TryProveSampledAlphaIsOne(Texture texture)
        {
            return UnityTextureEvidence.TryProveSampledAlphaIsOne(texture);
        }

        /// <summary>
        /// Recognizes the canonical Unity tangent-space normal-map import: the
        /// normal-map texture type with no green-channel inversion. Any other
        /// import cannot be read as an unmodified tangent-space normal.
        /// </summary>
        internal static bool IsCanonicalNormalMapImport(Texture texture)
        {
            return UnityTextureEvidence.IsCanonicalNormalMapImport(texture);
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

        private static PoiyomiSemanticDiagnostic MaterialDiagnostic(
            PoiyomiSemanticDiagnosticCode code,
            string detail)
        {
            return new PoiyomiSemanticDiagnostic(
                PoiyomiSemanticOutput.Material,
                code,
                detail);
        }

        private static PoiyomiSemanticResult Unsupported(
            PoiyomiSemanticDiagnostic diagnostic)
        {
            return new PoiyomiSemanticResult(
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

    /// <summary>
    /// Already-read shader identity evidence. Separating extraction from the
    /// identity decision keeps the conjunction deterministically testable
    /// without a live Unity asset or the real Poiyomi shader.
    /// </summary>
    internal readonly struct PoiyomiSourceEvidence
    {
        internal string ShaderName { get; }
        internal bool IsLocked { get; }
        internal bool HasReadableSource { get; }
        internal string AssetGuid { get; }
        internal string NormalizedSourceHash { get; }
        internal bool HasPackage { get; }
        internal string PackageName { get; }
        internal string PackageVersion { get; }
        internal bool HasRequiredSchema { get; }

        internal PoiyomiSourceEvidence(
            string shaderName,
            bool isLocked,
            bool hasReadableSource,
            string assetGuid,
            string normalizedSourceHash,
            bool hasPackage,
            string packageName,
            string packageVersion,
            bool hasRequiredSchema)
        {
            ShaderName = shaderName;
            IsLocked = isLocked;
            HasReadableSource = hasReadableSource;
            AssetGuid = assetGuid;
            NormalizedSourceHash = normalizedSourceHash;
            HasPackage = hasPackage;
            PackageName = packageName;
            PackageVersion = packageVersion;
            HasRequiredSchema = hasRequiredSchema;
        }
    }

    /// <summary>
    /// Immutable outcome of interpreting one base material: whether the
    /// material's source identity is supported, the normalized semantics, and
    /// deterministic output-scoped diagnostics.
    /// </summary>
    internal sealed class PoiyomiSemanticResult
    {
        internal bool IsSupportedMaterial { get; }
        internal MaterialSemantics Semantics { get; }
        internal IReadOnlyList<PoiyomiSemanticDiagnostic> Diagnostics { get; }

        internal PoiyomiSemanticResult(
            bool isSupportedMaterial,
            MaterialSemantics semantics,
            IReadOnlyList<PoiyomiSemanticDiagnostic> diagnostics)
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

            var copy = new PoiyomiSemanticDiagnostic[diagnostics.Count];
            for (var i = 0; i < diagnostics.Count; i++)
            {
                copy[i] = diagnostics[i]
                    ?? throw new ArgumentException(
                        "Diagnostics must not contain null entries.",
                        nameof(diagnostics));
            }

            Diagnostics = new ReadOnlyCollection<PoiyomiSemanticDiagnostic>(copy);
        }
    }

    /// <summary>
    /// One deterministic reason that a material is unsupported or that an output
    /// is <c>Unknown</c>. Diagnostics are data; the adapter never writes the
    /// Unity Console.
    /// </summary>
    internal sealed class PoiyomiSemanticDiagnostic
    {
        internal PoiyomiSemanticOutput Output { get; }
        internal PoiyomiSemanticDiagnosticCode Code { get; }
        internal string Detail { get; }

        internal PoiyomiSemanticDiagnostic(
            PoiyomiSemanticOutput output,
            PoiyomiSemanticDiagnosticCode code,
            string detail)
        {
            Output = output;
            Code = code;
            Detail = detail ?? throw new ArgumentNullException(nameof(detail));
        }
    }

    /// <summary>
    /// Semantic role a diagnostic is scoped to. The declared order is the
    /// deterministic diagnostic order: material-wide first, then each output.
    /// </summary>
    internal enum PoiyomiSemanticOutput
    {
        Material,
        BaseColor,
        Alpha,
        Emission,
        Normal,
    }

    /// <summary>
    /// Closed set of diagnostic reasons. This is a small fixed vocabulary, not
    /// a logging framework: no severities, no free-form categories.
    /// </summary>
    internal enum PoiyomiSemanticDiagnosticCode
    {
        UnsupportedShader,
        UnsupportedVersion,
        ModifiedShaderSource,
        MissingSourceEvidence,
        UnsupportedFeature,
        UnsupportedUv,
        UnsupportedSampling,
        UnstableTextureIdentity,
        UnsupportedColorSpace,
        UnsupportedTextureImport,
    }
}
