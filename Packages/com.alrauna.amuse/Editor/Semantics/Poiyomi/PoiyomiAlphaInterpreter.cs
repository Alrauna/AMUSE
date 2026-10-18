using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using UnityEngine;
using UnityEngine.Rendering;
using static Alrauna.Amuse.Editor.Semantics.EvidenceGates;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    internal static class PoiyomiAlphaInterpreter
    {
        private const string ShaderOptimizerEnabledProperty =
            "_ShaderOptimizerEnabled";
        private const string MainTexUvProperty = "_MainTexUV";
        private const string AlphaForceOpaqueProperty = "_AlphaForceOpaque";
        private const string AlphaForceOpaque2Property = "_AlphaForceOpaque2";
        private const string IgnoreMainTexAlphaProperty = "_MainIgnoreTexAlpha";
        private const string MainAlphaMaskModeProperty = "_MainAlphaMaskMode";
        private const string AlphaMaskBlendStrengthProperty =
            "_AlphaMaskBlendStrength";
        private const string AlphaMaskValueProperty = "_AlphaMaskValue";
        private const string AlphaMaskInvertProperty = "_AlphaMaskInvert";
        private const string AlphaMaskUvProperty = "_AlphaMaskUV";
        private const string ModeProperty = "_Mode";
        private const string TwoPassModeProperty = "_ModeTwoPass";
        private const string CutoffProperty = "_Cutoff";
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
        private const string OutlineEnabledProperty = "_EnableOutlines";
        private const string OutlineOverrideAlphaProperty =
            "_OutlineOverrideAlpha";
        private const string OutlineAlphaDistanceFadeProperty =
            "_OutlineAlphaDistanceFade";
        private const string OutlineAudioLinkColorProperty =
            "_OutlineALColorEnabled";
        private const string OutlineTextureUvProperty = "_OutlineTextureUV";
        private const string ColorProperty = "_Color";
        private const string TwoPassColorProperty = "_TwoPassColor";
        private const string LineColorProperty = "_LineColor";
        private const string MainTexPanProperty = "_MainTexPan";
        private const string AlphaMaskPanProperty = "_AlphaMaskPan";
        private const string OutlineTexturePanProperty = "_OutlineTexturePan";
        private const string MainTextureProperty = "_MainTex";
        private const string AlphaMaskProperty = "_AlphaMask";
        private const string OutlineTextureProperty = "_OutlineTexture";
        private const string AlphaMaskExpressionDetail = "alpha mask expression";

        private static readonly string[] AlphaCoverageGates =
        {
            "_DitherAlphaMode",
            "_AlphaDitherFadeFloor",
            "_AlphaModDistanceFadeFloor",
            "_DissolveType",
            "_AlphaModAngleFadeFloor",
        };

        private static readonly string[] AlphaFeatureGates =
        {
            "_ClearCoatEnable",
            "_VoronoiEnable",
            "_RefractionEnabled",
            "_FlipbookEnable",
            "_GlitterEnable",
        };

        private static readonly string[] TextureBackedAlphaGates =
        {
            "_PoiParallax",
            "_ParallaxInternalMap",
        };

        private static readonly string[] MainSamplingModeGates =
        {
            "_MainTextureMode",
            "_MainTexMipMapLevel",
        };

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

        private static readonly Dictionary<string, string> FeatureLabels =
            new(StringComparer.Ordinal)
            {
                ["_Cutoff"] = "Cutoff",
                ["_SrcBlend"] = "Source blend mode",
                ["_SrcBlend2"] = "Second pass source blend mode",
                ["_MainIgnoreTexAlpha"] = "Ignore main texture alpha",
                ["_MainTex"] = "Main texture",
                ["_Color"] = "Base color",
                ["_TwoPassColor"] = "Second pass base color",
                ["_PoiParallax"] = "Parallax",
                ["_AlphaForceOpaque"] = "Force opaque",
                ["_AlphaForceOpaque2"] = "Second pass force opaque",
                ["_AlphaMaskValue"] = "Alpha mask",
                ["_AlphaMaskBlendStrength"] = "Alpha mask",
                ["_AlphaMaskInvert"] = "Alpha mask",
                ["_Mode"] = "Rendering mode preset",
                ["_ModeTwoPass"] = "Second pass rendering mode preset",
                ["_EnableOutlines"] = "Outline",
                ["_OutlineOverrideAlpha"] = "Outline",
                ["_LineColor"] = "Outline",
                ["_OutlineTexture"] = "Outline",
                ["_OutlineTexturePan"] = "Outline",
                ["_OutlineTextureUV"] = "Outline",
                ["_OutlineAlphaDistanceFade"] = "Outline",
                ["_OutlineALColorEnabled"] = "Outline",
            };

        internal static MaterialEvidenceRequest AlphaEvidenceRequest { get; } =
            CreateAlphaEvidenceRequest(declareCutoutCutoff: true);

        internal static MaterialEvidenceRequest PlainAlphaEvidenceRequest { get; } =
            CreateAlphaEvidenceRequest(declareCutoutCutoff: false);

        internal static MaterialEvidenceRequest TwoPassAlphaEvidenceRequest { get; } =
            CreateTwoPassAlphaEvidenceRequest(AlphaEvidenceRequest);

        internal static MaterialEvidenceRequest
            PlainTwoPassAlphaEvidenceRequest { get; } =
            CreateTwoPassAlphaEvidenceRequest(PlainAlphaEvidenceRequest);

        internal static MaterialEvidenceRequest CreateTwoPassAlphaEvidenceRequest(
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
                        TwoPassModeProperty,
                        AlphaForceOpaque2Property,
                    },
                    colorProperties: new[]
                    {
                        TwoPassColorProperty,
                    },
                    vectorProperties: Array.Empty<string>(),
                    textureProperties:
                        Array.Empty<TexturePropertyEvidenceRequest>()));
        }

        internal static MaterialEvidenceRequest CreateAlphaEvidenceRequest(
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
                SrcBlendAlpha2Property,
                DstBlendAlpha2Property,
                OutlineEnabledProperty,
                OutlineOverrideAlphaProperty,
                OutlineAlphaDistanceFadeProperty,
                OutlineAudioLinkColorProperty,
                OutlineTextureUvProperty,
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
                presenceProperties: PoiyomiMaterialSemantics.AlphaRequiredSchemaProperties,
                scalarProperties: scalars,
                colorProperties: new[] { ColorProperty, LineColorProperty },
                vectorProperties: new[]
                {
                    MainTexPanProperty,
                    AlphaMaskPanProperty,
                    OutlineTexturePanProperty,
                },
                textureProperties: new[]
                {
                    new TexturePropertyEvidenceRequest(
                        MainTextureProperty,
                        TextureEvidenceKinds.ScaleOffset |
                        TextureEvidenceKinds.SourceIdentity |
                        TextureEvidenceKinds.Sampling |
                        TextureEvidenceKinds.AlphaChannel,
                        declareCutoutCutoff ? CutoffProperty : null),
                    new TexturePropertyEvidenceRequest(
                        AlphaMaskProperty,
                        TextureEvidenceKinds.ScaleOffset |
                        TextureEvidenceKinds.SourceIdentity |
                        TextureEvidenceKinds.RedChannel),
                    new TexturePropertyEvidenceRequest(
                        OutlineTextureProperty,
                        TextureEvidenceKinds.ScaleOffset |
                        TextureEvidenceKinds.SourceIdentity |
                        TextureEvidenceKinds.SampledAlphaIsOne |
                        TextureEvidenceKinds.AlphaChannel),
                });
        }

        internal static MaterialEvidenceRequest AlphaPredicateRequestFor(
            Material material,
            bool secondAlphaFamily)
        {
            var cutout = PoiyomiMaterialSemantics.DeclaresCutoutPreset(material);
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

        internal static SemanticOutput<ScalarSemanticValue> InterpretVerifiedAlpha(
            CapturedMaterialEvidence evidence,
            out AlphaUnknownReason unknownReason)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            var diagnostics = new List<PoiyomiSemanticDiagnostic>();
            var alpha = InterpretAlpha(evidence, false, diagnostics);
            unknownReason = AlphaUnknownReasonFor(diagnostics);
            return alpha;
        }

        internal static SemanticOutput<ScalarSemanticValue> InterpretVerifiedTwoPassAlpha(
            CapturedMaterialEvidence evidence,
            out AlphaUnknownReason unknownReason)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            var diagnostics = new List<PoiyomiSemanticDiagnostic>();
            var alpha = InterpretAlpha(evidence, true, diagnostics);
            unknownReason = AlphaUnknownReasonFor(diagnostics);
            return alpha;
        }

        internal static AlphaUnknownReason AlphaUnknownReasonFor(
            IReadOnlyList<PoiyomiSemanticDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                return null;
            }

            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Output != PoiyomiSemanticOutput.Alpha)
                {
                    continue;
                }

                return AlphaUnknownReason.UnsupportedFeature(
                    FeatureLabelFor(diagnostic.Detail), diagnostic.Detail);
            }

            return null;
        }

        internal static string FeatureLabelFor(string property)
        {
            if (property == null)
            {
                return null;
            }

            return FeatureLabels.TryGetValue(property, out var label)
                ? label
                : null;
        }

        internal static SemanticOutput<ScalarSemanticValue> InterpretAlpha(
            CapturedMaterialEvidence evidence,
            bool interpretSecondAlphaFamily,
            List<PoiyomiSemanticDiagnostic> diagnostics)
        {
            var coverageGate = FirstFailedZeroGate(evidence, AlphaCoverageGates);
            if (coverageGate != null)
            {
                return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    coverageGate);
            }

            if (!IsProvenOpaqueBlend(
                    evidence,
                    SrcBlendProperty,
                    DstBlendProperty,
                    BlendOpProperty,
                    BlendOpAlphaProperty,
                    SrcBlendAlphaProperty,
                    DstBlendAlphaProperty))
            {
                return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    SrcBlendProperty);
            }

            if (evidence.TryGetScalar(SrcBlend2Property, out _) &&
                !IsProvenOpaqueBlend(
                    evidence,
                    SrcBlend2Property,
                    DstBlend2Property,
                    BlendOp2Property,
                    BlendOpAlpha2Property,
                    SrcBlendAlpha2Property,
                    DstBlendAlpha2Property))
            {
                return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    SrcBlend2Property);
            }

            if (evidence.TryGetScalar(CutoffProperty, out var cutoff) &&
                (!IsFinite(cutoff) || cutoff > 1f))
            {
                return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
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
                    PoiyomiMaterialSemantics.AddDiagnostic(
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
                    PoiyomiMaterialSemantics.AddDiagnostic(
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
                return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
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

            var isCutout =
                evidence.TryGetScalar(modeProperty, out var mode) &&
                IsFinite(mode) &&
                mode == 1f;

            if (isCutout &&
                !evidence.TryGetScalar(CutoffProperty, out var cutoutCutoff))
            {
                return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    CutoffProperty);
            }

            var featureGate = FirstFailedZeroGate(evidence, AlphaFeatureGates);
            if (featureGate != null)
            {
                return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    featureGate);
            }

            var nonInertDecalSlot = FirstNonInertDecalSlot(evidence);
            if (nonInertDecalSlot != null)
            {
                return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
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

            ScalarSemanticValue baseAlpha;
            if (maskReplacement != null)
            {
                baseAlpha = maskReplacement;
            }
            else
            {
                if (!TryReadBinary(
                        evidence, IgnoreMainTexAlphaProperty, out var ignoreAlpha))
                {
                    return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        IgnoreMainTexAlphaProperty);
                }

                if (!evidence.TryGetColor(baseColorProperty, out var color) ||
                    !IsFinite(color.a))
                {
                    return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        baseColorProperty);
                }

                var colorAlpha = color.a;
                if (!evidence.TryGetTexture(
                        MainTextureProperty, out var mainTexture))
                {
                    return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
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
                    if (!isCutout &&
                        mainTexture.Texture.CaptureThreshold < 1f)
                    {
                        return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                            diagnostics,
                            PoiyomiSemanticOutput.Alpha,
                            PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                            CutoffProperty);
                    }

                    var samplingGate =
                        FirstFailedZeroGate(evidence, TextureBackedAlphaGates);
                    if (samplingGate != null)
                    {
                        return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                            diagnostics,
                            PoiyomiSemanticOutput.Alpha,
                            PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                            samplingGate);
                    }

                    if (!PoiyomiMaterialSemantics.TryInterpretMainSample(
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

                if (maskMultiplier != null)
                {
                    var multiplied = ScalarProductFold.Fold(
                        baseChain,
                        maskMultiplier,
                        MainAlphaMaskModeProperty,
                        property => PoiyomiMaterialSemantics.AddDiagnostic(
                            diagnostics,
                            PoiyomiSemanticOutput.Alpha,
                            PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                            property));
                    if (multiplied == null)
                    {
                        return SemanticOutput<ScalarSemanticValue>.Unknown();
                    }

                    baseAlpha = multiplied;
                }
                else
                {
                    baseAlpha = baseChain;
                }
            }

            if (!evidence.TryGetScalar(
                    OutlineEnabledProperty, out var enableOutlines) ||
                !IsFinite(enableOutlines))
            {
                return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    OutlineEnabledProperty);
            }

            if (enableOutlines <= 0f)
            {
                return SemanticOutput<ScalarSemanticValue>.Complete(baseAlpha);
            }

            if (!evidence.TryGetScalar(
                    OutlineOverrideAlphaProperty, out var overrideAlpha) ||
                !IsFinite(overrideAlpha))
            {
                return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    OutlineOverrideAlphaProperty);
            }

            if (!evidence.TryGetColor(
                    LineColorProperty, out var lineColor) ||
                !IsFinite(lineColor.a) ||
                lineColor.a != 1f)
            {
                return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    LineColorProperty);
            }

            if (!evidence.TryGetScalar(
                    OutlineAlphaDistanceFadeProperty, out var fade) ||
                !IsFinite(fade) ||
                fade != 0f)
            {
                return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    OutlineAlphaDistanceFadeProperty);
            }

            if (!evidence.TryGetScalar(
                    OutlineAudioLinkColorProperty, out var alColor) ||
                !IsFinite(alColor) ||
                alColor != 0f)
            {
                return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    OutlineAudioLinkColorProperty);
            }

            if (!evidence.TryGetTexture(
                    OutlineTextureProperty, out var outlineTexture))
            {
                return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    OutlineTextureProperty);
            }

            ScalarSemanticValue outlineFactor;
            if (!outlineTexture.IsAssigned ||
                (outlineTexture.Texture != null &&
                 outlineTexture.Texture.SampledAlphaIsProvenOne))
            {
                outlineFactor = ScalarSemanticValue.Constant(1f);
            }
            else
            {
                if (!evidence.TryGetScalar(
                        OutlineTextureUvProperty, out var rawChannel) ||
                    !IsFinite(rawChannel))
                {
                    return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        OutlineTextureUvProperty);
                }

                var channel = Mathf.RoundToInt(rawChannel);
                if (channel < 0 || channel > 3 || channel != rawChannel)
                {
                    return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        OutlineTextureUvProperty);
                }

                if (!evidence.TryGetVector(
                        OutlineTexturePanProperty, out var pan) ||
                    !IsFinite(pan) ||
                    pan.x != 0f || pan.y != 0f)
                {
                    return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        OutlineTexturePanProperty);
                }

                if (!outlineTexture.HasScaleOffset ||
                    !IsFinite(outlineTexture.Scale) ||
                    !IsFinite(outlineTexture.Offset))
                {
                    return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        OutlineTextureProperty);
                }

                if (outlineTexture.Texture == null ||
                    !outlineTexture.Texture.HasSourceIdentity ||
                    !outlineTexture.Texture.HasAlphaChannel ||
                    outlineTexture.Texture.AlphaCaptureRefusal != TextureCaptureRefusalReason.None)
                {
                    return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        OutlineTextureProperty);
                }

                if (!evidence.TryGetTexture(
                        MainTextureProperty, out var main) ||
                    main.Texture == null ||
                    !main.Texture.HasSampling)
                {
                    return PoiyomiMaterialSemantics.RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        MainTextureProperty);
                }

                var mapping = new UvMapping(
                    channel, outlineTexture.Scale, outlineTexture.Offset);
                var sample = new TextureSample(
                    outlineTexture.Texture.SourceIdentity,
                    mapping,
                    main.Texture.Sampling);
                outlineFactor = ScalarSemanticValue.Texture(
                    sample, TextureChannel.Alpha);
            }

            var folded = ScalarProductFold.Fold(
                baseAlpha,
                outlineFactor,
                OutlineEnabledProperty,
                property => PoiyomiMaterialSemantics.AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    property));
            if (folded == null)
            {
                return SemanticOutput<ScalarSemanticValue>.Unknown();
            }

            return SemanticOutput<ScalarSemanticValue>.Complete(folded);
        }

        private static bool IsCompletedExactlyOne(
            SemanticOutput<ScalarSemanticValue> output)
        {
            return output.IsComplete &&
                output.GetCompleteValue().Kind ==
                ScalarSemanticValueKind.Constant &&
                output.GetCompleteValue().GetConstantValue() == 1f;
        }

        private static bool IsProvenOpaqueBlend(
            CapturedMaterialEvidence evidence,
            string srcProperty,
            string dstProperty,
            string blendOpProperty,
            string blendOpAlphaProperty,
            string srcBlendAlphaProperty,
            string dstBlendAlphaProperty)
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
                    srcBlendAlphaProperty, out var srcBlendAlpha) ||
                !IsFinite(srcBlendAlpha) ||
                !evidence.TryGetScalar(
                    dstBlendAlphaProperty, out var dstBlendAlpha) ||
                !IsFinite(dstBlendAlpha))
            {
                return false;
            }

            var rgbOpaque = blendOp == (float)BlendOp.Add &&
                ((src == (float)BlendMode.One && dst == (float)BlendMode.Zero) ||
                    (src == (float)BlendMode.SrcAlpha && dst == (float)BlendMode.OneMinusSrcAlpha) ||
                    (src == (float)BlendMode.One && dst == (float)BlendMode.OneMinusSrcAlpha));
            var alphaForcedToOne = srcBlendAlpha == (float)BlendMode.One &&
                (dstBlendAlpha == (float)BlendMode.Zero ||
                    dstBlendAlpha == (float)BlendMode.One ||
                    dstBlendAlpha == (float)BlendMode.OneMinusSrcAlpha) &&
                (blendOpAlpha == (float)BlendOp.Add ||
                    blendOpAlpha == (float)BlendOp.Max);
            return rgbOpaque && alphaForcedToOne;
        }

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
                PoiyomiMaterialSemantics.AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    MainAlphaMaskModeProperty);
                return false;
            }

            if (mode == 0f)
            {
                return true;
            }

            if (mode != 1f && mode != 2f)
            {
                PoiyomiMaterialSemantics.AddDiagnostic(
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
                PoiyomiMaterialSemantics.AddDiagnostic(
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
                PoiyomiMaterialSemantics.AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    AlphaMaskValueProperty);
                return false;
            }

            if (!TryReadBinary(
                    evidence, AlphaMaskInvertProperty, out var invert))
            {
                PoiyomiMaterialSemantics.AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    AlphaMaskInvertProperty);
                return false;
            }

            if (!evidence.TryGetTexture(AlphaMaskProperty, out var mask))
            {
                PoiyomiMaterialSemantics.AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    AlphaMaskProperty);
                return false;
            }

            if (!mask.IsAssigned)
            {
                var sum = blendStrength + (invert ? -value : value);
                var raw = Mathf.Clamp01(sum);
                var alpha = invert ? 1f - raw : raw;
                if (!IsFinite(sum) || !IsFinite(alpha))
                {
                    PoiyomiMaterialSemantics.AddDiagnostic(
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

                if (alpha == 1f)
                {
                    return true;
                }

                PoiyomiMaterialSemantics.AddDiagnostic(
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

            if (!evidence.TryGetScalar(
                    AlphaMaskUvProperty, out var rawChannel) ||
                !IsFinite(rawChannel))
            {
                PoiyomiMaterialSemantics.AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedUv,
                    AlphaMaskUvProperty);
                return false;
            }

            var channel = Mathf.RoundToInt(rawChannel);
            if (channel < 0 || channel > 3 || channel != rawChannel)
            {
                PoiyomiMaterialSemantics.AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedUv,
                    AlphaMaskUvProperty);
                return false;
            }

            if (!evidence.TryGetVector(
                    AlphaMaskPanProperty, out var pan) ||
                !IsFinite(pan) ||
                pan.x != 0f || pan.y != 0f || pan.z != 0f || pan.w != 0f)
            {
                PoiyomiMaterialSemantics.AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedUv,
                    AlphaMaskPanProperty);
                return false;
            }

            if (!mask.HasScaleOffset ||
                !IsFinite(mask.Scale) ||
                !IsFinite(mask.Offset))
            {
                PoiyomiMaterialSemantics.AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    AlphaMaskProperty);
                return false;
            }

            if (mask.Texture == null || !mask.Texture.HasSourceIdentity)
            {
                PoiyomiMaterialSemantics.AddDiagnostic(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnstableTextureIdentity,
                    AlphaMaskProperty);
                return false;
            }

            var mapping = new UvMapping(channel, mask.Scale, mask.Offset);

            if (blendStrength == 1f && value == 0f)
            {
                if (!evidence.TryGetTexture(
                        MainTextureProperty, out var main) ||
                    main.Texture == null ||
                    !main.Texture.HasSampling)
                {
                    PoiyomiMaterialSemantics.AddDiagnostic(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                        MainTextureProperty);
                    return false;
                }

                var samplingGate = FirstFailedZeroGate(
                    evidence, TextureBackedAlphaGates);
                if (samplingGate != null)
                {
                    PoiyomiMaterialSemantics.AddDiagnostic(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        samplingGate);
                    return false;
                }

                if (mask.Texture.RedCaptureRefusal !=
                    TextureCaptureRefusalReason.None)
                {
                    PoiyomiMaterialSemantics.AddDiagnostic(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        AlphaMaskProperty);
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
                    PoiyomiMaterialSemantics.AddDiagnostic(
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

            if (blendStrength == 1f && value >= 1f)
            {
                if (replace)
                {
                    replacement = ScalarSemanticValue.Constant(1f);
                }

                return true;
            }

            if (blendStrength == 1f && !invert && value < 1f)
            {
                if (!evidence.TryGetTexture(
                        MainTextureProperty, out var main) ||
                    main.Texture == null ||
                    !main.Texture.HasSampling)
                {
                    PoiyomiMaterialSemantics.AddDiagnostic(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                        MainTextureProperty);
                    return false;
                }

                var samplingGate = FirstFailedZeroGate(
                    evidence, TextureBackedAlphaGates);
                if (samplingGate != null)
                {
                    PoiyomiMaterialSemantics.AddDiagnostic(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        samplingGate);
                    return false;
                }

                if (mask.Texture.RedCaptureRefusal !=
                    TextureCaptureRefusalReason.None)
                {
                    PoiyomiMaterialSemantics.AddDiagnostic(
                        diagnostics,
                        PoiyomiSemanticOutput.Alpha,
                        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                        AlphaMaskProperty);
                    return false;
                }

                var maskSample = new TextureSample(
                    mask.Texture.SourceIdentity,
                    mapping,
                    main.Texture.Sampling);
                var mapped = AffineAlphaMap.FromBinary32(
                    blendStrength, value);
                var factor = ScalarSemanticValue.MappedTexture(
                    maskSample, TextureChannel.Red, mapped);
                if (replace)
                {
                    replacement = factor;
                }
                else
                {
                    multiplier = factor;
                }

                return true;
            }

            PoiyomiMaterialSemantics.AddDiagnostic(
                diagnostics,
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                blendStrength != 1f
                    ? AlphaMaskBlendStrengthProperty
                    : AlphaMaskValueProperty);
            return false;
        }
    }
}
