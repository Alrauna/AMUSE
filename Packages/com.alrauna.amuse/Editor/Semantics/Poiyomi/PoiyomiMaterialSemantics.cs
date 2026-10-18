using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Alrauna.Amuse.Editor.Host;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Alrauna.Amuse.Editor.Semantics.EvidenceGates;

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
        internal const string PoiyomiToonShaderName = PoiyomiSourceAttestation.PoiyomiToonShaderName;
        internal const string PoiyomiPackageName = PoiyomiSourceAttestation.PoiyomiPackageName;
        internal const string PoiyomiPackageVersion = PoiyomiSourceAttestation.PoiyomiPackageVersion;
        internal const string CanonicalShaderGuid = PoiyomiSourceAttestation.CanonicalShaderGuid;
        internal const string CanonicalNormalizedSourceHash = PoiyomiSourceAttestation.CanonicalNormalizedSourceHash;
        internal const string PoiyomiTwoPassShaderName = PoiyomiSourceAttestation.PoiyomiTwoPassShaderName;
        internal const string TwoPassCanonicalShaderGuid = PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid;
        internal const string TwoPassCanonicalNormalizedSourceHash = PoiyomiSourceAttestation.TwoPassCanonicalNormalizedSourceHash;

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
        private const string SrcBlendAlpha2Property = "_SrcBlendAlpha2";
        private const string DstBlendAlpha2Property = "_DstBlendAlpha2";

        private const string MainTextureProperty = "_MainTex";
        private const string ColorProperty = "_Color";
        internal const string OutlineEnabledProperty = "_EnableOutlines";
        internal const string OutlineOverrideAlphaProperty = "_OutlineOverrideAlpha";
        internal const string LineColorProperty = "_LineColor";
        internal const string OutlineTextureProperty = "_OutlineTexture";
        internal const string OutlineAlphaDistanceFadeProperty =
            "_OutlineAlphaDistanceFade";
        internal const string OutlineAudioLinkColorProperty =
            "_OutlineALColorEnabled";
        internal const string OutlineTextureUvProperty = "_OutlineTextureUV";
        internal const string OutlineTexturePanProperty = "_OutlineTexturePan";

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

        internal static readonly string[] AlphaRequiredSchemaProperties =
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
        internal static MaterialEvidenceRequest AlphaEvidenceRequest =>
            PoiyomiAlphaInterpreter.AlphaEvidenceRequest;

        internal static MaterialEvidenceRequest PlainAlphaEvidenceRequest =>
            PoiyomiAlphaInterpreter.PlainAlphaEvidenceRequest;

        internal static MaterialEvidenceRequest TwoPassAlphaEvidenceRequest =>
            PoiyomiAlphaInterpreter.TwoPassAlphaEvidenceRequest;

        internal static MaterialEvidenceRequest PlainTwoPassAlphaEvidenceRequest =>
            PoiyomiAlphaInterpreter.PlainTwoPassAlphaEvidenceRequest;
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

        internal static MaterialEvidenceRequest
            FullTwoPassMaterialEvidenceRequest { get; } =
            PoiyomiAlphaInterpreter.CreateTwoPassAlphaEvidenceRequest(FullMaterialEvidenceRequest);

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

            var isTwoPass = material.shader != null &&
                string.Equals(
                    material.shader.name,
                    PoiyomiTwoPassShaderName,
                    StringComparison.Ordinal);

            var request = isTwoPass
                ? FullTwoPassMaterialEvidenceRequest
                : FullMaterialEvidenceRequest;

            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material,
                    request,
                    AlphaPredicateRequestFor(material, isTwoPass)),
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
                isTwoPass);
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
            var alpha = PoiyomiAlphaInterpreter.InterpretAlpha(
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

        internal static bool TryInterpretMainSample(
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
            bool secondAlphaFamily) =>
            PoiyomiAlphaInterpreter.AlphaPredicateRequestFor(material, secondAlphaFamily);

        internal static SemanticOutput<ScalarSemanticValue> InterpretVerifiedAlpha(
            CapturedMaterialEvidence evidence,
            out AlphaUnknownReason unknownReason) =>
            PoiyomiAlphaInterpreter.InterpretVerifiedAlpha(evidence, out unknownReason);

        internal static SemanticOutput<ScalarSemanticValue> InterpretVerifiedTwoPassAlpha(
            CapturedMaterialEvidence evidence,
            out AlphaUnknownReason unknownReason) =>
            PoiyomiAlphaInterpreter.InterpretVerifiedTwoPassAlpha(evidence, out unknownReason);

        internal static string FeatureLabelFor(string property) =>
            PoiyomiAlphaInterpreter.FeatureLabelFor(property);

        internal static AlphaUnknownReason AlphaUnknownReasonFor(
            IReadOnlyList<PoiyomiSemanticDiagnostic> diagnostics) =>
            PoiyomiAlphaInterpreter.AlphaUnknownReasonFor(diagnostics);
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

        internal static SemanticOutput<T> RecordUnknown<T>(
            List<PoiyomiSemanticDiagnostic> diagnostics,
            PoiyomiSemanticOutput output,
            PoiyomiSemanticDiagnosticCode code,
            string detail)
            where T : class
        {
            AddDiagnostic(diagnostics, output, code, detail);
            return SemanticOutput<T>.Unknown();
        }

        internal static void AddDiagnostic(
            List<PoiyomiSemanticDiagnostic> diagnostics,
            PoiyomiSemanticOutput output,
            PoiyomiSemanticDiagnosticCode code,
            string detail)
        {
            diagnostics.Add(
                new PoiyomiSemanticDiagnostic(output, code, detail));
        }

        // --- Source attestation ---------------------------------------------

        internal static string ComputeNormalizedSourceHash(string rawSource) =>
            PoiyomiSourceAttestation.ComputeNormalizedSourceHash(rawSource);

        internal static bool TryReadNormalizedSourceHashCached(
            string assetPath,
            out string normalizedHash) =>
            PoiyomiSourceAttestation.TryReadNormalizedSourceHashCached(assetPath, out normalizedHash);

        internal static bool ClearSourceHashMemo() =>
            PoiyomiSourceAttestation.ClearSourceHashMemo();

        internal static bool TryVerifyPoiyomiIdentity(
            in PoiyomiSourceEvidence evidence,
            out PoiyomiSemanticDiagnostic diagnostic) =>
            PoiyomiSourceAttestation.TryVerifyPoiyomiIdentity(evidence, out diagnostic);

        internal static PoiyomiSourceEvidence GatherAlphaSourceEvidence(
            Shader shader,
            CapturedMaterialEvidence evidence) =>
            PoiyomiSourceAttestation.GatherAlphaSourceEvidence(shader, evidence);

        internal static PoiyomiSourceEvidence GatherSourceEvidence(
            Shader shader,
            CapturedMaterialEvidence evidence,
            IReadOnlyCollection<string> requiredSchemaProperties) =>
            PoiyomiSourceAttestation.GatherSourceEvidence(shader, evidence, requiredSchemaProperties);

        /// <summary>
        /// The second family's own scalars and tint. The first family reads
        /// none of them, so the plain Toon request combines without them. The
        /// cutoff declaration rides the base request's main texture entry, so
        /// the declaring and plain-clip bases carry the split choice and the
        /// extras never carry one.
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


        internal static PoiyomiSemanticDiagnostic MaterialDiagnostic(
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
