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

        // Second pinned identity (S10): the Two Pass generated shader in the
        // same package. The digest was measured on the official v9.3.64 tag
        // artifact by the production ComputeNormalizedSourceHash and an
        // independent sha256, which agree; the file is LF-only with no BOM,
        // so the installed bytes are the tag's bytes.
        internal const string PoiyomiTwoPassShaderName =
            ".poiyomi/Poiyomi Toon Two Pass";
        internal const string TwoPassCanonicalShaderGuid =
            "eda2412ac7ab2db45a47a521f6d7d8a6";
        internal const string TwoPassCanonicalNormalizedSourceHash =
            "b1d9ecd3072d21db97001dd23f88d089996b809e4039891a05f64d2ffcd4df67";

        private const string ShaderOptimizerEnabledProperty =
            "_ShaderOptimizerEnabled";

        private const string SrcBlendProperty = "_SrcBlend";
        private const string DstBlendProperty = "_DstBlend";
        private const string BlendOpProperty = "_BlendOp";
        private const string SrcBlendAlphaProperty = "_SrcBlendAlpha";
        private const string DstBlendAlphaProperty = "_DstBlendAlpha";
        private const string BlendOpAlphaProperty = "_BlendOpAlpha";
        private const string SrcBlend2Property = "_SrcBlend2";
        private const string DstBlend2Property = "_DstBlend2";
        private const string BlendOp2Property = "_BlendOp2";
        private const string BlendOpAlpha2Property = "_BlendOpAlpha2";

        private const string MainTextureProperty = "_MainTex";
        private const string ColorProperty = "_Color";

        // The Two Pass second family reads this tint's alpha where the plain
        // base reads _Color.a (note 4.1, vendor line 29785).
        private const string TwoPassColorProperty = "_TwoPassColor";
        private const string MainTexUvProperty = "_MainTexUV";
        private const string MainTexPanProperty = "_MainTexPan";
        private const string AlphaForceOpaqueProperty = "_AlphaForceOpaque";

        // The Two Pass shader declares a separate force-opaque flag for its
        // second family (note 4.4, Two Pass source line 862).
        private const string AlphaForceOpaque2Property = "_AlphaForceOpaque2";

        // The _Mode preset selector of the pinned source, and the second
        // family's own selector on the Two Pass shader (note 4.4, Two Pass
        // source line 68110). The vendor branches on these values per pass:
        // the cutout value forces the family's alpha to 1 after the shared
        // clip (note 4.5), so the cutout preset is what admits the split
        // route for that family's claim.
        private const string ModeProperty = "_Mode";
        private const string TwoPassModeProperty = "_ModeTwoPass";
        private const string IgnoreMainTexAlphaProperty = "_MainIgnoreTexAlpha";
        private const string MainAlphaMaskModeProperty = "_MainAlphaMaskMode";
        private const string AlphaMaskProperty = "_AlphaMask";
        private const string CutoffProperty = "_Cutoff";
        private const string AlphaMaskBlendStrengthProperty =
            "_AlphaMaskBlendStrength";
        private const string AlphaMaskValueProperty = "_AlphaMaskValue";
        private const string AlphaMaskInvertProperty = "_AlphaMaskInvert";
        private const string AlphaMaskUvProperty = "_AlphaMaskUV";
        private const string AlphaMaskPanProperty = "_AlphaMaskPan";
        private const string PoiParallaxProperty = "_PoiParallax";

        // Names the whole mask sum rather than one input: an overflow is a
        // property of the addition, not of either finite operand.
        private const string AlphaMaskExpressionDetail =
            AlphaMaskBlendStrengthProperty + " + " + AlphaMaskValueProperty;
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

            // The Beat Saber module toggle of the pinned source (note 4.7).
            // When it is on, the pass writes alpha after the clip as
            // alpha = alpha * emission.z and rewrites the alpha blend pair.
            // An enabled module must never sit inside a claimed exactly-one
            // alpha. The coverage gate run precedes the _AlphaForceOpaque
            // short-circuit, so this entry keeps the forced path protected.
            "_BSSEnabled",
        };

        // Enabled writers/masks that add to or replace the non-forced alpha
        // term. The alpha mask mode is deliberately absent: it is interpreted by
        // TryInterpretAlphaMask rather than gated, because Replace is provable
        // with no mask bound and, for the admitted strength-value pairs,
        // through the bound mask's red field, and Multiply is provable for the
        // declared default shape and for the same admitted pairs through the
        // exact product fold.
        //
        // _AlphaPremultiply is deliberately absent. The vendor premultiply
        // scales the base color by saturate(alpha) in three passes at vendor
        // lines 30216, 47274, 58331 and never writes the alpha value (note
        // 4.3). The proof only moves triangles whose alpha is exactly 1, so
        // the factor is exactly 1 there and the feature is an identity on the
        // proven domain. The base-color gate list keeps the entry because the
        // color equation genuinely changes per pixel.
        //
        // The four decal slots are deliberately absent: FirstNonInertDecalSlot
        // interprets them per slot, because an enabled slot whose
        // override-alpha mode is exactly zero writes no alpha (vendor lines
        // 22958 to 22978).
        //
        // The audio link decal module writes the chain alpha through
        // lerp(alpha, alpha * audioLinkValue, _ALDecalControlsAlpha)
        // (investigation 2026-09-22, vendor line 24833), independently of
        // the four decal slots. A zero weight is an identity, so it is
        // proven exactly zero on every non-forced alpha path.
        private static readonly string[] AlphaFeatureGates =
        {
            "_AlphaMod",
            "_AlphaDistanceFade",
            "_AlphaFresnel",
            "_AlphaAngular",
            "_AlphaAudioLinkEnabled",
            "_EnableAudioLink",
            "_AlphaGlobalMask",
            "_BackFaceEnabled",
            "_RGBMaskEnabled",
            "_ALDecalControlsAlpha",
            "_EnableFlipbook",
            // The rim family: the liltoon-style and UTS2-style arms never
            // touch the chain alpha, and the poi-style arm writes it only
            // through _RimApplyAlpha (design 2026-09-22; pinned 9.3.64
            // source, vendor lines 25838 to 25843, both slots sharing the
            // one scalar). At zero every compiled arm is an alpha identity,
            // whatever the enable floats and keywords say, because the call
            // sites guard on the keywords.
            "_RimApplyAlpha",
            "_EnableDepthRimLighting",
            "_EnableEnvironmentalRim",
            "_VideoEffectsEnable",
            "_EnableTouchGlow",
            "_MainVertexColoringEnabled",
        };

        // Proven off only immediately before a texture-backed _MainTex alpha
        // claim. applyParallax overwrites poiMesh.uv[_ParallaxUV] before the
        // _MainTex sample, so an enabled parallax would make that sample's
        // coordinate view-dependent. It is not an AlphaFeatureGate: a constant
        // alpha term never reads a UV, so parallax cannot disturb it.
        private static readonly string[] TextureBackedAlphaGates =
        {
            PoiParallaxProperty,
        };

        // The four decal slots. An enabled slot writes the chain alpha only
        // inside its override-alpha block (pinned 9.3.64 source, vendor
        // lines 22958 to 22978), so a slot with the override-alpha mode
        // proven exactly zero composes color and emission and writes no
        // alpha. The enable float decides whether the block exists for the
        // slot, on the same float-as-evidence trust every other gate here
        // uses. Both name sets ride the alpha evidence request, so the
        // capture and the animation closure cover them.
        private static readonly string[] AlphaDecalSlotEnables =
        {
            "_DecalEnabled",
            "_DecalEnabled1",
            "_DecalEnabled2",
            "_DecalEnabled3",
        };

        private static readonly string[]
            AlphaDecalSlotOverrideAlphaProperties =
        {
            "_DecalOverrideAlpha",
            "_DecalOverrideAlpha1",
            "_DecalOverrideAlpha2",
            "_DecalOverrideAlpha3",
        };

        internal static MaterialEvidenceRequest AlphaEvidenceRequest { get; } =
            CreateAlphaEvidenceRequest(declareCutoutCutoff: true);

        /// <summary>
        /// The plain-clip predicate variant of the alpha request: the same
        /// schema, including the <c>_Mode</c> and <c>_Cutoff</c> scalars, but
        /// with no cutoff declared on the main texture request. It is the
        /// capture predicate for every non-cutout preset: a preset whose pass
        /// renders the chain value keeps the exact-255 field, so the
        /// exact-one rule reads unbinarized bytes. It never feeds a claim on
        /// its own; the interpretation of a cutout preset requires the
        /// declared cutoff, whose binarized field is the split route.
        /// </summary>
        internal static MaterialEvidenceRequest PlainAlphaEvidenceRequest { get; } =
            CreateAlphaEvidenceRequest(declareCutoutCutoff: false);

        /// <summary>
        /// The Two Pass family's own alpha request: the plain request plus
        /// exactly the second family's tint, its force-opaque flag, and its
        /// own preset selector. The plain Toon request stays without them, so
        /// a plain material keeps capturing without any second-family scalar.
        /// The interpreter reads the second-family scalars only through the
        /// Two Pass entry points, which only this request feeds.
        /// </summary>
        internal static MaterialEvidenceRequest TwoPassAlphaEvidenceRequest { get; } =
            CreateTwoPassAlphaEvidenceRequest(AlphaEvidenceRequest);

        /// <summary>
        /// The Two Pass family's plain-clip predicate variant: the same
        /// schema with no cutoff declared on the main texture request. It is
        /// the capture predicate for Two Pass materials where neither family
        /// runs the cutout preset.
        /// </summary>
        internal static MaterialEvidenceRequest
            PlainTwoPassAlphaEvidenceRequest { get; } =
            CreateTwoPassAlphaEvidenceRequest(PlainAlphaEvidenceRequest);

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
                    material,
                    FullMaterialEvidenceRequest,
                    AlphaPredicateRequestFor(material, false)),
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
                captured,
                false);
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
                new MaterialEvidenceCaptureInput(
                    material,
                    AlphaEvidenceRequest,
                    AlphaPredicateRequestFor(material, false)),
            })[0];
            return InterpretVerifiedMaterial(
                material, activeColorSpace, captured, false);
        }

        /// <summary>
        /// Narrow friend-test seam for the Two Pass family. Captures with the
        /// Two Pass request, so the second-family scalars are gathered, and
        /// interprets the alpha output the way the Two Pass family dispatch
        /// does. The other outputs read the material directly and do not
        /// depend on the request.
        /// </summary>
        internal static PoiyomiSemanticResult InterpretVerifiedTwoPassMaterial(
            Material material,
            ColorSpace activeColorSpace)
        {
            RequireAnalyzableMaterial(material);

            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material,
                    TwoPassAlphaEvidenceRequest,
                    AlphaPredicateRequestFor(material, true)),
            })[0];
            return InterpretVerifiedMaterial(
                material, activeColorSpace, captured, true);
        }

        /// <summary>
        /// Narrow friend-test seam over an already captured evidence: the
        /// caller owns the capture and its capture predicate, so the
        /// interpretation and its diagnostics answer exactly the evidence
        /// the caller hands in. The Two Pass family keeps its own entry
        /// point; this one always interprets the plain family.
        /// </summary>
        internal static PoiyomiSemanticResult InterpretVerifiedMaterial(
            Material material,
            ColorSpace activeColorSpace,
            CapturedMaterialEvidence captured)
        {
            RequireAnalyzableMaterial(material);
            return InterpretVerifiedMaterial(
                material, activeColorSpace, captured, false);
        }

        private static PoiyomiSemanticResult InterpretVerifiedMaterial(
            Material material,
            ColorSpace activeColorSpace,
            CapturedMaterialEvidence captured,
            bool interpretSecondAlphaFamily)
        {
            // A verified material is a supported material; each output is proven
            // independently and stays Unknown, with a diagnostic, when its
            // equation is not representable.
            var diagnostics = new List<PoiyomiSemanticDiagnostic>();

            var baseColor = InterpretBaseColor(
                material, activeColorSpace, diagnostics);
            var alpha = InterpretAlpha(
                captured, interpretSecondAlphaFamily, diagnostics);
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

            // Unit-tint simplification is exact per binary32 component:
            // Unity's aggregate vector equality is epsilon-based and is
            // intentionally excluded from semantic proof decisions, because a
            // near-one tint is a real multiplier that must be retained.
            var value = tint.x == 1f && tint.y == 1f && tint.z == 1f
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
        /// Whether the material's own family runs the cutout preset on either
        /// family. The first family branches on <c>_Mode</c>, the second on
        /// <c>_ModeTwoPass</c> (note 4.4, Two Pass source line 68110). A
        /// missing or non-finite selector answers false: the material keeps
        /// the plain-clip rules, and the field-predicate agreement gate keeps
        /// a cutoff-binarized field unprovable, so an unknown preset never
        /// widens a claim. This read decides the capture predicate; the
        /// interpretation re-reads the captured scalars, so the two sides
        /// agree by construction.
        /// </summary>
        internal static bool DeclaresCutoutPreset(Material material)
        {
            return ReadsCutoutPreset(material, ModeProperty) ||
                ReadsCutoutPreset(material, TwoPassModeProperty);
        }

        private static bool ReadsCutoutPreset(
            Material material,
            string modeProperty)
        {
            if (material == null || !material.HasProperty(modeProperty))
            {
                return false;
            }

            var mode = material.GetFloat(modeProperty);
            return IsFinite(mode) && mode == 1f;
        }

        /// <summary>
        /// The capture predicate one Poiyomi material's own alpha capture
        /// runs under. A cutout preset selects the declaring request, whose
        /// main texture entry binarizes the alpha field by the _Cutoff value:
        /// that binarized field is the split route. Every other preset
        /// selects the plain-clip variant, which keeps the exact-255 field
        /// the exact-one rule reads. Both variants carry one schema, so the
        /// closed batch's union stays single and the choice is a per-material
        /// capture fact, never a second family schema.
        /// </summary>
        internal static MaterialEvidenceRequest AlphaPredicateRequestFor(
            Material material,
            bool secondAlphaFamily)
        {
            var cutout = DeclaresCutoutPreset(material);
            if (secondAlphaFamily)
            {
                return cutout
                    ? TwoPassAlphaEvidenceRequest
                    : PlainTwoPassAlphaEvidenceRequest;
            }

            return cutout
                ? AlphaEvidenceRequest
                : PlainAlphaEvidenceRequest;
        }

        /// <summary>
        /// Proves the normalized alpha term. Coverage/clip mechanisms must be
        /// off on every path. A captured cutoff above one, or a non-finite
        /// one, refuses naming <c>_Cutoff</c>, because the vendor clips with
        /// <c>clip(alpha - _Cutoff)</c> in every pass without condition
        /// (note 4.5) and even the forced path clips after it forces alpha
        /// to one. A forced-opaque material is a constant one. Otherwise the
        /// family's preset selector decides the field route: the cutout
        /// value forces alpha to one above the cutoff, so the binarized
        /// field's above-cutoff verdicts are exactly the rendered-opaque
        /// set, and a missing cutoff on a cutout preset refuses naming
        /// <c>_Cutoff</c>. Every other preset keeps the exact-one rule over
        /// the exact field. The alpha mask mode is interpreted by
        /// TryInterpretAlphaMask: Replace with no bound mask is a proven
        /// constant, and a bound Replace mask proves through its red field
        /// exactly under the admitted strength-value pairs. Multiply is the
        /// declared default mode: with no bound mask it admits only a mask
        /// term of exactly one and leaves the running chain unchanged, and
        /// with a bound mask it folds the admitted term into the running
        /// chain through the exact product machinery. Any other mode or pair
        /// stays Unknown. With the mask leaving no factor, alpha is
        /// <c>_Color.a</c>, optionally multiplying the alpha channel of a
        /// single supported <c>_MainTex</c> sample. That texture-backed form
        /// additionally requires parallax to be proven off, because it alone
        /// depends on the sampling coordinate. Any enabled alpha writer,
        /// non-binary flag, or unprovable sample keeps the output Unknown
        /// with one diagnostic. Alpha is a raw scalar, so no color-import
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
                evidence, false, new List<PoiyomiSemanticDiagnostic>());
        }

        /// <summary>
        /// The Two Pass family's alpha entry point. The dispatched family
        /// guarantees the Two Pass request, whose scalar set names
        /// <c>_AlphaForceOpaque2</c>, so the second-family reads below are
        /// evidence-backed rather than unrequested.
        /// </summary>
        internal static SemanticOutput<ScalarSemanticValue> InterpretVerifiedTwoPassAlpha(
            CapturedMaterialEvidence evidence)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            return InterpretAlpha(
                evidence, true, new List<PoiyomiSemanticDiagnostic>());
        }

        private static SemanticOutput<ScalarSemanticValue> InterpretAlpha(
            CapturedMaterialEvidence evidence,
            bool interpretSecondAlphaFamily,
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

            // V2 multipass rule, pass one: the opacity claim is about the
            // rendered pixel, not the alpha value alone. With alpha proven
            // 1 on the whole domain only the replace, standard alpha, and
            // premultiply pairs render the source unchanged; the additive
            // and multiplicative pairs compose with what is behind and are
            // never visually opaque.
            if (!IsProvenOpaqueBlend(
                    evidence,
                    SrcBlendProperty,
                    DstBlendProperty,
                    BlendOpProperty,
                    BlendOpAlphaProperty))
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    SrcBlendProperty);
            }

            // V2 multipass rule, pass two: the Two Pass shader's second
            // Base pass reuses the first pass's alpha chain under the
            // 2-family preset, so its blend pair gates exactly the same
            // way. The read itself is the presence test: the alpha request
            // always names the 2-family scalars, and a plain Toon material
            // yields no value for them, while a Two Pass source that lost
            // them fails identity before this read.
            if (evidence.TryGetScalar(SrcBlend2Property, out _) &&
                !IsProvenOpaqueBlend(
                    evidence,
                    SrcBlend2Property,
                    DstBlend2Property,
                    BlendOp2Property,
                    BlendOpAlpha2Property))
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    SrcBlend2Property);
            }

            // The Two Pass shader draws two Base passes. The first family
            // reads _Color.a and forces alpha to 1 through its own
            // _AlphaForceOpaque flag (note 4.1 vendor line 29780, note 4.4
            // vendor line 30366). The second family reads _TwoPassColor.a
            // (note 4.1 vendor line 29785) and forces alpha to 1 through its
            // own _AlphaForceOpaque2 flag (note 4.4 Two Pass source line
            // 862), with the same chain otherwise. The rendered pixel keeps
            // an exactly-one alpha only when every family the material draws
            // proves exactly one, so a Two Pass claim completes only through
            // the conjunction of the two single-family claims below. A
            // family that completes with any other term refuses the whole
            // claim and names its own tint, because that tint is where the
            // family's alpha value is decided. A family whose chain cannot
            // prove keeps its own recorded diagnostic.
            //
            // The clip gate: the vendor clips with clip(alpha - _Cutoff) in
            // every pass without condition (note 4.5). A captured cutoff
            // above one discards even unit alpha, so no triangle renders and
            // no claim survives; a non-finite cutoff leaves the clip
            // undefined. Both refuse naming _Cutoff before any family claim
            // runs, because the forced path clips after it forces alpha to 1
            // (note 4.4) and needs the same bound. A cutoff absent from the
            // schema keeps the committed reading: the property always exists
            // on the pinned source, so its absence means a stand-in whose
            // own rendering carries no clip at all, and Unity answers the
            // declared vendor default of one half for a declared property.
            if (evidence.TryGetScalar(CutoffProperty, out var cutoff) &&
                (!IsFinite(cutoff) || cutoff > 1f))
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    CutoffProperty);
            }

            var first = InterpretSingleFamilyAlpha(
                evidence,
                AlphaForceOpaqueProperty,
                ColorProperty,
                ModeProperty,
                diagnostics);
            if (!interpretSecondAlphaFamily)
            {
                return first;
            }

            if (!IsCompletedExactlyOne(first))
            {
                if (first.IsComplete)
                {
                    AddDiagnostic(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        ColorProperty);
                }

                return SemanticOutput<ScalarSemanticValue>.Unknown();
            }

            var second = InterpretSingleFamilyAlpha(
                evidence,
                AlphaForceOpaque2Property,
                TwoPassColorProperty,
                TwoPassModeProperty,
                diagnostics);
            if (!IsCompletedExactlyOne(second))
            {
                if (second.IsComplete)
                {
                    AddDiagnostic(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        TwoPassColorProperty);
                }

                return SemanticOutput<ScalarSemanticValue>.Unknown();
            }

            return SemanticOutput<ScalarSemanticValue>.Complete(
                ScalarSemanticValue.Constant(1f));
        }

        /// <summary>
        /// The claim one drawn family makes: the family's own force-opaque
        /// flag read with the same exact-binary gate read as the plain path,
        /// then the shared chain whose base tint is
        /// <paramref name="baseColorProperty"/>. The family's preset
        /// selector (<c>_Mode</c> for the first family, <c>_ModeTwoPass</c>
        /// for the second) decides the field route: the cutout value admits
        /// the split route, whose binarized field answers the vendor's
        /// unconditional clip plus the cutout alpha forcing (note 4.5), and
        /// every other value keeps the exact-one rule over the exact field.
        /// The second Base pass reuses the first pass's alpha chain, so the
        /// feature gates, the mask interpretation, and the texture term are
        /// the vendor facts the plain path already proves, and nothing else
        /// is parameterized.
        /// </summary>
        /// <summary>
        /// The first decal slot the alpha equation cannot prove inert, or
        /// null when every slot writes no alpha. A slot with its enable
        /// float proven zero has no alpha effect. An enabled slot is an
        /// alpha identity exactly when its override-alpha mode is exactly
        /// zero (pinned 9.3.64 source, vendor lines 22958 to 22978). The
        /// returned name is the vendor property the refusal reports.
        /// </summary>
        private static string FirstNonInertDecalSlot(
            CapturedMaterialEvidence evidence)
        {
            for (var slot = 0; slot < AlphaDecalSlotEnables.Length; slot++)
            {
                var enable = AlphaDecalSlotEnables[slot];
                if (!evidence.TryGetScalar(enable, out var enabled) ||
                    !IsFinite(enabled))
                {
                    return enable;
                }

                if (enabled == 0f)
                {
                    continue;
                }

                var overrideAlpha =
                    AlphaDecalSlotOverrideAlphaProperties[slot];
                if (!evidence.TryGetScalar(
                        overrideAlpha, out var mode) ||
                    !IsFinite(mode) ||
                    mode != 0f)
                {
                    return overrideAlpha;
                }
            }

            return null;
        }

        private static SemanticOutput<ScalarSemanticValue>
            InterpretSingleFamilyAlpha(
            CapturedMaterialEvidence evidence,
            string forceOpaqueProperty,
            string baseColorProperty,
            string modeProperty,
            List<PoiyomiSemanticDiagnostic> diagnostics)
        {
            if (!TryReadBinary(
                    evidence, forceOpaqueProperty, out var forceOpaque))
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    forceOpaqueProperty);
            }

            if (forceOpaque)
            {
                return SemanticOutput<ScalarSemanticValue>.Complete(
                    ScalarSemanticValue.Constant(1f));
            }

            // Preset detection over the captured selector. A missing or
            // non-finite value answers not-cutout: the split route never
            // engages, and the field-predicate agreement gate below keeps a
            // cutoff-binarized field unprovable, so an unknown preset never
            // widens a claim.
            var isCutout =
                evidence.TryGetScalar(modeProperty, out var mode) &&
                IsFinite(mode) &&
                mode == 1f;

            // The split route's premise is the declared cutoff: without it
            // the capture stayed exact and no binarized field answers the
            // clip, so a cutout preset would silently prove nothing-or-wrong.
            // A present cutoff is already bounded by the shared clip gate.
            if (isCutout &&
                !evidence.TryGetScalar(CutoffProperty, out var cutoutCutoff))
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    CutoffProperty);
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

            var nonInertDecalSlot = FirstNonInertDecalSlot(evidence);
            if (nonInertDecalSlot != null)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    nonInertDecalSlot);
            }

            if (!TryInterpretAlphaMask(
                    evidence,
                    diagnostics,
                    out var maskReplacement,
                    out var maskMultiplier))
            {
                return SemanticOutput<ScalarSemanticValue>.Unknown();
            }

            // Replace discards the base term outright, so _MainIgnoreTexAlpha,
            // the tint alpha and _MainTex cannot reach the result and are not
            // read.
            if (maskReplacement != null)
            {
                return SemanticOutput<ScalarSemanticValue>.Complete(
                    maskReplacement);
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

            if (!evidence.TryGetColor(baseColorProperty, out var color) ||
                !IsFinite(color.a))
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    baseColorProperty);
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

            ScalarSemanticValue baseChain;
            if (ignoreAlpha || !mainTexture.IsAssigned)
            {
                baseChain = ScalarSemanticValue.Constant(colorAlpha);
            }
            else
            {
                // Field-predicate agreement (Task 6). A non-cutout preset
                // claims the exact-one rule, and only an exact-255 field can
                // answer it: a cutoff-binarized field's byte 255 means the
                // texel satisfies the capture's cutoff test, so consuming it
                // under the exact-one rule would call a chain between the
                // cutoff and one opaque. The capture predicate selects the
                // exact field for every non-cutout preset, so a binarized
                // field under a non-cutout claim means the two sides
                // disagree, and the claim refuses naming _Cutoff.
                if (!isCutout &&
                    mainTexture.Texture.CaptureThreshold < 1f)
                {
                    return RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        CutoffProperty);
                }

                // Only a texture-backed claim depends on the sampling
                // coordinate, so the parallax proof is required here and
                // nowhere earlier.
                var samplingGate =
                    FirstFailedZeroGate(evidence, TextureBackedAlphaGates);
                if (samplingGate != null)
                {
                    return RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        samplingGate);
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

                baseChain = colorAlpha == 1f
                    ? ScalarSemanticValue.Texture(sample, TextureChannel.Alpha)
                    : ScalarSemanticValue.TextureTimesConstant(
                        sample, TextureChannel.Alpha, colorAlpha);
            }

            // Multiply folds the admitted mask term into the running chain
            // through the exact product machinery, the same fold the lilToon
            // term performs over its layered chain.
            if (maskMultiplier != null)
            {
                var multiplied = MultiplyAlphaValues(
                    baseChain, maskMultiplier, diagnostics);
                if (multiplied == null)
                {
                    return SemanticOutput<ScalarSemanticValue>.Unknown();
                }

                return SemanticOutput<ScalarSemanticValue>.Complete(
                    multiplied);
            }

            return SemanticOutput<ScalarSemanticValue>.Complete(baseChain);
        }

        /// <summary>
        /// True only when a family claim completed as the exact constant one,
        /// the only value a two-family conjunction may compose into a
        /// material claim. Any other completed term, and any unknown output,
        /// keeps the whole claim refused.
        /// </summary>
        private static bool IsCompletedExactlyOne(
            SemanticOutput<ScalarSemanticValue> output)
        {
            return output.IsComplete &&
                output.GetCompleteValue().Kind ==
                ScalarSemanticValueKind.Constant &&
                output.GetCompleteValue().GetConstantValue() == 1f;
        }

        /// <summary>
        /// The closed blend-state set whose rendered pixel equals the source
        /// color once alpha is proven 1 on the whole sampled domain. RGB:
        /// the blend operation must be add and the factor pair must be
        /// replace (1,0), standard alpha (5,10), or premultiply (1,10) -
        /// the `_Mode` presets of the pinned source emit exactly these
        /// three for their opaque, cutout, fade, and transparent modes.
        /// Alpha: with the source factor proven 1 and the dst factor in the
        /// vendor set, both the add and the max operations yield exactly 1,
        /// because the dst contribution is clamped or dominated. Every
        /// additive and multiplicative RGB pairing composes with what is
        /// behind and can never claim opacity. A missing or non-finite
        /// value fails closed.
        /// </summary>
        private static bool IsProvenOpaqueBlend(
            CapturedMaterialEvidence evidence,
            string srcProperty,
            string dstProperty,
            string blendOpProperty,
            string blendOpAlphaProperty)
        {
            if (!evidence.TryGetScalar(srcProperty, out var src) ||
                !IsFinite(src) ||
                !evidence.TryGetScalar(dstProperty, out var dst) ||
                !IsFinite(dst) ||
                !evidence.TryGetScalar(
                    blendOpProperty, out var blendOp) ||
                !IsFinite(blendOp) ||
                !evidence.TryGetScalar(
                    blendOpAlphaProperty, out var blendOpAlpha) ||
                !IsFinite(blendOpAlpha) ||
                !evidence.TryGetScalar(
                    SrcBlendAlphaProperty, out var srcBlendAlpha) ||
                !IsFinite(srcBlendAlpha) ||
                !evidence.TryGetScalar(
                    DstBlendAlphaProperty, out var dstBlendAlpha) ||
                !IsFinite(dstBlendAlpha))
            {
                return false;
            }

            var rgbOpaque = blendOp == 0f &&
                ((src == 1f && dst == 0f) ||
                    (src == 5f && dst == 10f) ||
                    (src == 1f && dst == 10f));
            var alphaForcedToOne = srcBlendAlpha == 1f &&
                (dstBlendAlpha is 0f or 1f or 10f) &&
                (blendOpAlpha is 0f or 4f);
            return rgbOpaque && alphaForcedToOne;
        }


        /// <summary>
        /// Interprets <c>_MainAlphaMaskMode</c> against the pinned mask
        /// equation:
        /// <code>
        /// alphaMask = saturate(mask.r * _AlphaMaskBlendStrength
        ///             + (_AlphaMaskInvert ? -_AlphaMaskValue : _AlphaMaskValue));
        /// if (_AlphaMaskInvert) alphaMask = 1 - alphaMask;
        /// if (_MainAlphaMaskMode == 1) alpha = alphaMask;          // Replace
        /// if (_MainAlphaMaskMode == 2) alpha = alpha * alphaMask;  // Multiply
        /// </code>
        /// Mode 0 never samples the mask and leaves the alpha term to the
        /// caller. Mode 1 replaces it. Mode 2 is the vendor's declared default,
        /// so a material that never touched the mask section carries it (note
        /// 7.3). With no mask bound, the pinned source declares the
        /// <c>"white"</c> default, so <c>mask.r</c> is exactly one and the
        /// expression collapses to a constant. Under Replace that constant is
        /// the alpha. Under Multiply the chain stands unchanged exactly when
        /// the constant is one, which covers the declared default pair (1, 0)
        /// with invert off, and any other sub-one constant refuses, the role
        /// the lilToon term names <c>MainUnchanged</c> and its sub-one
        /// refusal. A bound mask proves through its red channel exactly under
        /// the pairs whose binary32 arithmetic needs no texel threshold:
        /// (1, 0), where the term is the sampled red alone or its one-minus
        /// form under invert, and (1, value &gt;= 1), where the term saturates
        /// to exactly one for both invert states. Under Replace the term is
        /// the alpha. Under Multiply the term folds into the running chain
        /// through the exact product machinery: the saturated pairs are a
        /// constant one, so the chain stands unchanged, and the invert-off
        /// (1, 0) term is the sampled red itself. The invert-on (1, 0) term is
        /// a saturating difference, and a product with a saturating factor has
        /// no association-invariant exact-one predicate, so it refuses naming
        /// the mode property, exactly like the lilToon fold. The mask
        /// coordinate is <c>uv[_AlphaMaskUV]</c> under the mask's own plain
        /// affine with zero pan, and the sample rides the main sampler. Add
        /// and Subtract and every other mode keep refusing, and so does every
        /// other strength-value pair, because proving the saturate of
        /// <c>r * s + v</c> for arbitrary <c>s</c> and <c>v</c> needs a
        /// per-texel threshold envelope. Every refusal records one scoped
        /// diagnostic naming the property that could not be proven.
        /// </summary>
        /// <param name="replacement">
        /// The proven Replace value, or null when the caller continues with
        /// the base alpha term.
        /// </param>
        /// <param name="multiplier">
        /// The admitted Multiply factor for the running chain, or null when
        /// the mode contributes no factor.
        /// </param>
        private static bool TryInterpretAlphaMask(
            CapturedMaterialEvidence evidence,
            List<PoiyomiSemanticDiagnostic> diagnostics,
            out ScalarSemanticValue replacement,
            out ScalarSemanticValue multiplier)
        {
            replacement = null;
            multiplier = null;

            if (!evidence.TryGetScalar(
                    MainAlphaMaskModeProperty, out var mode) ||
                !IsFinite(mode))
            {
                AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    MainAlphaMaskModeProperty);
                return false;
            }

            // Off: the shader's `if (_MainAlphaMaskMode)` is false, so no mask
            // is sampled and the base alpha term stands unchanged.
            if (mode == 0f)
            {
                return true;
            }

            // Replace and Multiply are the interpreted modes. Add and Subtract
            // saturate a sum or a difference, and every other value is outside
            // the vendor's own mode map.
            if (mode != 1f && mode != 2f)
            {
                AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    MainAlphaMaskModeProperty);
                return false;
            }

            var replace = mode == 1f;

            if (!evidence.TryGetScalar(
                    AlphaMaskBlendStrengthProperty, out var blendStrength) ||
                !IsFinite(blendStrength))
            {
                AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    AlphaMaskBlendStrengthProperty);
                return false;
            }

            if (!evidence.TryGetScalar(
                    AlphaMaskValueProperty, out var value) ||
                !IsFinite(value))
            {
                AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    AlphaMaskValueProperty);
                return false;
            }

            if (!TryReadBinary(
                    evidence, AlphaMaskInvertProperty, out var invert))
            {
                AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    AlphaMaskInvertProperty);
                return false;
            }

            if (!evidence.TryGetTexture(AlphaMaskProperty, out var mask))
            {
                AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    AlphaMaskProperty);
                return false;
            }

            if (!mask.IsAssigned)
            {
                // The unbound mask samples exactly one, so `mask.r * blendStrength`
                // is exactly blendStrength and the shader's fused multiply-add
                // cannot round differently from this addition.
                var sum = blendStrength + (invert ? -value : value);
                var raw = Mathf.Clamp01(sum);
                var alpha = invert ? 1f - raw : raw;
                if (!IsFinite(sum) || !IsFinite(alpha))
                {
                    AddDiagnostic(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        AlphaMaskExpressionDetail);
                    return false;
                }

                if (replace)
                {
                    replacement = ScalarSemanticValue.Constant(alpha);
                    return true;
                }

                // Multiply against the unbound mask. The declared default pair
                // (1, 0) with invert off makes the constant above exactly one,
                // so the product is the chain itself and the chain stands
                // unchanged, the role the lilToon term names MainUnchanged. A
                // sub-one constant would need a constant-factor fold the
                // lilToon mirror refuses too, so it refuses and names the
                // first culprit in the declared diagnostic order.
                if (alpha == 1f)
                {
                    return true;
                }

                AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    blendStrength != 1f
                        ? AlphaMaskBlendStrengthProperty
                        : value != 0f
                            ? AlphaMaskValueProperty
                            : AlphaMaskInvertProperty);
                return false;
            }

            // --- Bound mask: the red field route ---------------------------

            // The coordinate selector must name a mesh UV set the proof
            // carries. A fractional or out-of-range selector cannot be read
            // as an exact channel.
            if (!evidence.TryGetScalar(
                    AlphaMaskUvProperty, out var rawChannel) ||
                !IsFinite(rawChannel))
            {
                AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedUv,
                    AlphaMaskUvProperty);
                return false;
            }

            var channel = Mathf.RoundToInt(rawChannel);
            if (channel < 0 || channel > 3 || channel != rawChannel)
            {
                AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedUv,
                    AlphaMaskUvProperty);
                return false;
            }

            // The vendor pans the coordinate over time by _AlphaMaskPan.xy.
            // A nonzero or non-finite pan makes the coordinate drift, so
            // only the exact zero vector proves.
            if (!evidence.TryGetVector(
                    AlphaMaskPanProperty, out var pan) ||
                !IsFinite(pan) ||
                pan.x != 0f || pan.y != 0f || pan.z != 0f || pan.w != 0f)
            {
                AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedUv,
                    AlphaMaskPanProperty);
                return false;
            }

            // The mask coordinate is the selected UV set under the mask's
            // own scale and offset. That transform is a plain affine, so a
            // non-identity mask ST proves, exactly like the lilToon mask
            // rule this route mirrors. No identity demand applies.
            if (!mask.HasScaleOffset ||
                !IsFinite(mask.Scale) ||
                !IsFinite(mask.Offset))
            {
                AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    AlphaMaskProperty);
                return false;
            }

            // The field route resolves the sample against the mask's stable
            // project identity. A scene-only or otherwise unidentifiable
            // mask carries no resolvable field.
            if (mask.Texture == null || !mask.Texture.HasSourceIdentity)
            {
                AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnstableTextureIdentity,
                    AlphaMaskProperty);
                return false;
            }

            var mapping = new UvMapping(channel, mask.Scale, mask.Offset);

            // The pair (1, 0). The vendor term with invert off is
            // saturate(r * 1 + 0). Binary32 multiplies r by one exactly and
            // adds zero exactly, and a normalized red sample sits in [0, 1],
            // so the saturate is inert and the term is the sampled red
            // itself. With invert on the vendor writes
            // 1 - saturate(r * 1 - 0). The multiply and the subtract are
            // exact by the same argument, the saturate is inert, and the
            // term is the single subtraction 1 - r, which stays in [0, 1].
            // The shape saturate(1 - r) below carries that value with an
            // inert clamp.
            if (blendStrength == 1f && value == 0f)
            {
                // The sample rides the main sampler, so the mask's own import
                // state is irrelevant and the main texture's captured
                // sampling is the fact. An unassigned main texture binds the
                // engine default sampler, whose state is not a captured fact
                // here, so the route fails closed.
                if (!evidence.TryGetTexture(
                        MainTextureProperty, out var main) ||
                    main.Texture == null ||
                    !main.Texture.HasSampling)
                {
                    AddDiagnostic(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                        MainTextureProperty);
                    return false;
                }

                // The value now depends on the sampling coordinate, so the
                // parallax gate runs here, like every texture-backed alpha
                // claim. applyParallax overwrites the UV set before the
                // sample, which would make the coordinate view-dependent.
                var samplingGate = FirstFailedZeroGate(
                    evidence, TextureBackedAlphaGates);
                if (samplingGate != null)
                {
                    AddDiagnostic(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        samplingGate);
                    return false;
                }

                var maskSample = new TextureSample(
                    mask.Texture.SourceIdentity,
                    mapping,
                    main.Texture.Sampling);
                var red = ScalarSemanticValue.Texture(
                    maskSample, TextureChannel.Red);

                if (!replace && invert)
                {
                    // The Multiply term is saturate(1 - r), a saturating
                    // difference. A product with a saturating factor has no
                    // association-invariant exact-one predicate, so the exact
                    // product machinery refuses it and names the mode
                    // property, exactly like the lilToon fold.
                    AddDiagnostic(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        MainAlphaMaskModeProperty);
                    return false;
                }

                if (replace)
                {
                    replacement = invert
                        ? ScalarSemanticValue.SaturatingDifference(
                            ScalarSemanticValue.Constant(1f), red)
                        : red;
                }
                else
                {
                    multiplier = red;
                }

                return true;
            }

            // The provably saturated pair (1, value >= 1). Invert off: in
            // exact arithmetic r * 1 + value >= value >= 1 for every red
            // sample r >= 0, and binary32 rounding is monotone, so the
            // fused or unfused sum stays at or above one and the saturate
            // yields exactly one. Invert on: r * 1 - value <= 1 - value <= 0
            // for r in [0, 1], so the saturate yields exactly zero and
            // 1 - 0 is exactly one. Both invert states make the mask term
            // the constant one and consult no texel of the mask. Replace
            // writes that constant. Multiply folds it as a constant, so the
            // running chain stands unchanged.
            if (blendStrength == 1f && value >= 1f)
            {
                if (replace)
                {
                    replacement = ScalarSemanticValue.Constant(1f);
                }

                return true;
            }

            // Every other pair needs the deferred threshold-envelope
            // contract: proving saturate(r * s + v) at one needs a per-texel
            // predicate whose rounding argument is future work. The refusal
            // names the first culprit, the strength when it leaves one and
            // the value alone when the strength is exactly one.
            AddDiagnostic(
                diagnostics,
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                blendStrength != 1f
                    ? AlphaMaskBlendStrengthProperty
                    : AlphaMaskValueProperty);
            return false;
        }

        /// <summary>
        /// The exact product fold the Multiply mask mode shares with the
        /// lilToon term. The exact-one predicate of a product of values
        /// bounded in [0, 1] is association-invariant: every rounded chain of
        /// sub-one factors stays strictly below one, and all-one factors
        /// answer exactly one in every association, so the fold through the
        /// constants and factor lists changes nothing provable. Returns null
        /// after recording a refusal when either shape is a saturating sum or
        /// difference, whose exact-one predicate is not
        /// multiplication-invariant. The refusal names the mode property,
        /// exactly like the lilToon fold.
        /// </summary>
        private static ScalarSemanticValue MultiplyAlphaValues(
            ScalarSemanticValue baseChain,
            ScalarSemanticValue maskFactor,
            List<PoiyomiSemanticDiagnostic> diagnostics)
        {
            if (baseChain.Kind == ScalarSemanticValueKind.SaturatingSum ||
                baseChain.Kind ==
                    ScalarSemanticValueKind.SaturatingDifference ||
                maskFactor.Kind == ScalarSemanticValueKind.SaturatingSum ||
                maskFactor.Kind ==
                    ScalarSemanticValueKind.SaturatingDifference)
            {
                AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    MainAlphaMaskModeProperty);
                return null;
            }

            var samples = new List<TextureSample>();
            var channels = new List<TextureChannel>();
            var multiplier = 1f;
            multiplier = CollectProductFactors(
                baseChain, samples, channels, multiplier);
            multiplier = CollectProductFactors(
                maskFactor, samples, channels, multiplier);

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

        private static float CollectProductFactors(
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
                        "A saturating shape reached product factor " +
                        "collection, which the caller must refuse first.");
            }
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
            var value = tint.x == 1f && tint.y == 1f && tint.z == 1f
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
            // 1. Exact shader name and unlocked state. Two admitted
            // identities share one conjunction: the plain Toon shader and
            // the Two Pass generated shader (S10), each verified against
            // its own pinned GUID and digest.
            var isTwoPass = string.Equals(
                evidence.ShaderName,
                PoiyomiTwoPassShaderName,
                StringComparison.Ordinal);
            if (!isTwoPass &&
                !string.Equals(
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

            var expectedGuid = isTwoPass
                ? TwoPassCanonicalShaderGuid
                : CanonicalShaderGuid;
            if (!string.Equals(
                    evidence.AssetGuid,
                    expectedGuid,
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

            // 3. Normalized source hash, against the resolved identity.
            var expectedHash = isTwoPass
                ? TwoPassCanonicalNormalizedSourceHash
                : CanonicalNormalizedSourceHash;
            if (!string.Equals(
                    evidence.NormalizedSourceHash,
                    expectedHash,
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

        /// <summary>
        /// Assembly-internal so a second frontend-owned schema can reuse it:
        /// <see cref="PoiyomiOpaqueConversion.GatherConversionSourceEvidence"/>
        /// passes the conversion schema exactly as
        /// <see cref="GatherAlphaSourceEvidence"/> passes the alpha schema.
        /// Body, signature and every existing caller are unchanged.
        /// </summary>
        internal static PoiyomiSourceEvidence GatherSourceEvidence(
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

        /// <summary>
        /// The second family's own scalars and tint. The first family reads
        /// none of them, so the plain Toon request combines without them. The
        /// cutoff declaration rides the base request's main texture entry, so
        /// the declaring and plain-clip bases carry the split choice and the
        /// extras never carry one.
        /// </summary>
        private static MaterialEvidenceRequest CreateTwoPassAlphaEvidenceRequest(
            MaterialEvidenceRequest baseRequest)
        {
            return MaterialEvidenceRequest.Combine(
                baseRequest,
                new MaterialEvidenceRequest(
                    shaderName: false,
                    activeColorSpace: false,
                    presenceProperties: Array.Empty<string>(),
                    scalarProperties: new[]
                    {
                        AlphaForceOpaque2Property,
                        TwoPassModeProperty,
                    },
                    colorProperties: new[] { TwoPassColorProperty },
                    vectorProperties: Array.Empty<string>(),
                    textureProperties:
                        Array.Empty<TexturePropertyEvidenceRequest>()));
        }

        private static MaterialEvidenceRequest CreateAlphaEvidenceRequest(
            bool declareCutoutCutoff)
        {
            var scalars = new HashSet<string>(StringComparer.Ordinal)
            {
                ShaderOptimizerEnabledProperty,
                MainTexUvProperty,
                AlphaForceOpaqueProperty,
                IgnoreMainTexAlphaProperty,
                MainAlphaMaskModeProperty,
                AlphaMaskBlendStrengthProperty,
                AlphaMaskValueProperty,
                AlphaMaskInvertProperty,
                AlphaMaskUvProperty,

                // The preset selector and the clip threshold of the pinned
                // source (note 4.5, note 4.6). The interpretation branches on
                // the preset: the cutout value admits the split route, every
                // other value keeps the exact-one rules. Both scalars are
                // captured unconditionally, so one request serves every
                // preset and the branch reads captured facts only.
                ModeProperty,
                CutoffProperty,
                SrcBlendProperty,
                DstBlendProperty,
                BlendOpProperty,
                BlendOpAlphaProperty,
                SrcBlendAlphaProperty,
                DstBlendAlphaProperty,
                SrcBlend2Property,
                DstBlend2Property,
                BlendOp2Property,
                BlendOpAlpha2Property,
            };
            scalars.UnionWith(MainSamplingModeGates);
            scalars.UnionWith(AlphaCoverageGates);
            scalars.UnionWith(AlphaFeatureGates);
            scalars.UnionWith(TextureBackedAlphaGates);
            scalars.UnionWith(AlphaDecalSlotEnables);
            scalars.UnionWith(AlphaDecalSlotOverrideAlphaProperties);

            return new MaterialEvidenceRequest(
                shaderName: true,
                activeColorSpace: false,
                presenceProperties: AlphaRequiredSchemaProperties,
                scalarProperties: scalars,
                colorProperties: new[] { ColorProperty },
                vectorProperties: new[] { MainTexPanProperty, AlphaMaskPanProperty },
                textureProperties: new[]
                {
                    // The cutout split's capture declaration: when the
                    // material's own predicate declares the cutoff, the
                    // capture binarizes the alpha field by the _Cutoff value,
                    // so byte 255 means the texel survives the vendor's
                    // unconditional clip (note 4.5). The declaration is the
                    // lilToon cutout pattern. The per-material predicate
                    // decides whether it applies: a non-cutout preset keeps
                    // the exact-255 field the exact-one rule reads.
                    new TexturePropertyEvidenceRequest(
                        MainTextureProperty,
                        TextureEvidenceKinds.ScaleOffset |
                        TextureEvidenceKinds.SourceIdentity |
                        TextureEvidenceKinds.Sampling |
                        TextureEvidenceKinds.AlphaChannel,
                        declareCutoutCutoff ? CutoffProperty : null),

                    // The bound Replace mask proves through its red channel, so
                    // the request gathers exactly what the red-field route reads:
                    // the mask's own scale and offset for the plain affine, the
                    // stable project identity, and the red field itself. The
                    // sampling is never asked of the mask: the vendor mask block
                    // samples the mask through the main sampler, whose state the
                    // main-texture request above already carries.
                    new TexturePropertyEvidenceRequest(
                        AlphaMaskProperty,
                        TextureEvidenceKinds.ScaleOffset |
                        TextureEvidenceKinds.SourceIdentity |
                        TextureEvidenceKinds.RedChannel),
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
