using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    internal static class AlphaSeparationMaterialConverter
    {
        internal static AlphaSeparationSlotRefusal ConvertAdmittedMaterial(
            CapturedAlphaMaterial captured,
            Material live,
            IReadOnlyList<(CapturedFloatBinding Binding,
                           AnimatedPropertyRef Reference)> conversionBindings,
            IReadOnlyCollection<string> conversionPropertyNames,
            Material preparedOpaque,
            VerifiedPoiyomiConversion poiyomiConversion,
            VerifiedLilToonConversion lilToonConversion,
            bool allowDepthTestChange,
            IReadOnlyCollection<string> grantedShaderNames,
            out Material opaque,
            out bool depthTestDivergence,
            out bool premultiplyNormalization,
            out string refusedDetail)
        {
            opaque = null;
            depthTestDivergence = false;
            premultiplyNormalization = false;
            refusedDetail = null;
            switch (captured.Family)
            {
                case CapturedAlphaMaterialFamily.LilToon:
                    opaque = live;
                    return AlphaSeparationSlotRefusal.None;
                case CapturedAlphaMaterialFamily.LilToonCutout:
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                {
                    var isTransparent = captured.Family ==
                        CapturedAlphaMaterialFamily.LilToonTransparent;
                    var admission = TryAdmitConversionEvidence(
                        captured,
                        conversionBindings,
                        conversionPropertyNames,
                        out var derived);
                    if (admission != AlphaSeparationSlotRefusal.None)
                    {
                        return admission;
                    }

                    if (captured.MultiResolution != null)
                    {
                        EffectiveRenderState.ReadEffectiveRenderState(
                            live, out var multiQueue,
                            out var multiRenderType);
                        if (!LilToonMultiResolution.TryReadCapturedMode(
                                captured.Evidence, out var multiMode))
                        {
                            throw new InvalidOperationException(
                                "A resolved Multi material carries no " +
                                "readable mode.");
                        }

                        var multiEligibility =
                            LilToonMultiSourceEligibility
                                .EvaluateVerifiedEligibility(
                                    derived, multiQueue, multiRenderType,
                                    multiMode, allowDepthTestChange);
                        if (!TryMapLilToonOutcome(
                                multiEligibility,
                                () => preparedOpaque ??
                                    LilToonOpaqueTarget
                                        .PrepareCanonicalOpaqueClone(
                                            live, derived),
                                out opaque,
                                out var multiRefusal,
                                out depthTestDivergence))
                        {
                            refusedDetail = multiRefusal.ToString();
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }

                        return AlphaSeparationSlotRefusal.None;
                    }

                    if (lilToonConversion != null)
                    {
                        if (!lilToonConversion(
                                live,
                                derived,
                                allowDepthTestChange,
                                preparedOpaque,
                                out opaque,
                                out var seamRefusal,
                                out var seamDivergence))
                        {
                            refusedDetail = seamRefusal.ToString();
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }

                        depthTestDivergence = seamDivergence;
                    }
                    else
                    {
                        EffectiveRenderState.ReadEffectiveRenderState(
                            live, out var queue, out var renderType);

                        var sourceEvidence = isTransparent
                            ? LilToonSourceAttestation
                                .GatherTransparentSourceEvidence(
                                    live.shader, derived)
                            : LilToonSourceAttestation
                                .GatherCutoutSourceEvidence(
                                    live.shader, derived);
                        var attested = isTransparent
                            ? LilToonSourceAttestation
                                .TryVerifyLilToonTransparentIdentity(
                                    sourceEvidence,
                                    out var lilIdentityDiagnostic)
                            : LilToonSourceAttestation
                                .TryVerifyLilToonCutoutIdentity(
                                    sourceEvidence,
                                    out lilIdentityDiagnostic);
                        if (!attested &&
                            !IsGrantedShader(
                                sourceEvidence.ShaderName,
                                grantedShaderNames))
                        {
                            refusedDetail =
                                "SourceIdentity." +
                                lilIdentityDiagnostic.Code;
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }

                        var eligibility = isTransparent
                            ? LilToonTransparentSourceEligibility
                                .EvaluateVerifiedEligibility(
                                    derived, queue, renderType,
                                    allowDepthTestChange)
                            : LilToonCutoutSourceEligibility
                                .EvaluateVerifiedEligibility(
                                    derived, queue, renderType,
                                    allowDepthTestChange);
                        if (!TryMapLilToonOutcome(
                                eligibility,
                                () => preparedOpaque ??
                                    LilToonOpaqueTarget
                                        .PrepareCanonicalOpaqueClone(
                                            live, derived),
                                out opaque,
                                out var sourceRefusal,
                                out depthTestDivergence))
                        {
                            refusedDetail = sourceRefusal.ToString();
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }
                    }

                    return AlphaSeparationSlotRefusal.None;
                }
                case CapturedAlphaMaterialFamily.Poiyomi:
                {
                    var admission = TryAdmitConversionEvidence(
                        captured,
                        conversionBindings,
                        conversionPropertyNames,
                        out var derived);
                    if (admission != AlphaSeparationSlotRefusal.None)
                    {
                        return admission;
                    }

                    if (poiyomiConversion != null)
                    {
                        if (!poiyomiConversion(
                                live,
                                derived,
                                allowDepthTestChange,
                                preparedOpaque,
                                out opaque,
                                out var seamRefusal,
                                out var seamDivergence,
                                out var seamPremultiply))
                        {
                            refusedDetail = seamRefusal.ToString();
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }

                        depthTestDivergence = seamDivergence;
                        premultiplyNormalization = seamPremultiply;
                    }
                    else
                    {
                        EffectiveRenderState.ReadEffectiveRenderState(
                            live, out var queue, out var renderType);

                        var sourceEvidence =
                            PoiyomiOpaqueConversion
                                .GatherConversionSourceEvidence(
                                    live.shader, derived);
                        if (!PoiyomiMaterialSemantics
                                .TryVerifyPoiyomiIdentity(
                                    sourceEvidence,
                                    out var poiIdentityDiagnostic))
                        {
                            refusedDetail =
                                "SourceIdentity." +
                                poiIdentityDiagnostic.Code;
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }

                        var eligibility =
                            PoiyomiOpaqueConversion
                                .EvaluateVerifiedEligibility(
                                    derived, queue, renderType,
                                    allowDepthTestChange);
                        if (!TryMapPoiyomiOutcome(
                                eligibility, live, preparedOpaque,
                                out opaque,
                                out var poiyomiRefusal,
                                out depthTestDivergence,
                                out premultiplyNormalization))
                        {
                            refusedDetail = poiyomiRefusal.ToString();
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }
                    }

                    return AlphaSeparationSlotRefusal.None;
                }
                default:
                    return AlphaSeparationSlotRefusal
                        .OpaqueConversionUnsupportedFamily;
            }
        }

        internal static bool TryMapLilToonOutcome(
            LilToonOpaqueConversionEligibility eligibility,
            Func<Material> createClone,
            out Material opaque,
            out LilToonOpaqueConversionRefusal refusal,
            out bool depthTestDivergence)
        {
            if (eligibility.Outcome !=
                LilToonOpaqueConversionOutcome.Convertible)
            {
                opaque = null;
                refusal = eligibility.Refusal;
                depthTestDivergence = false;
                return false;
            }

            opaque = createClone();
            refusal = LilToonOpaqueConversionRefusal.None;
            depthTestDivergence = eligibility.DepthTestDivergence;
            return true;
        }

        internal static bool TryMapPoiyomiOutcome(
            PoiyomiOpaqueConversionEligibility eligibility,
            Material live,
            Material preparedOpaque,
            out Material opaque,
            out PoiyomiOpaqueConversionRefusal refusal,
            out bool depthTestDivergence,
            out bool premultiplyNormalization)
        {
            switch (eligibility.Outcome)
            {
                case PoiyomiOpaqueConversionOutcome.AlreadyOpaque:
                    opaque = live;
                    refusal = PoiyomiOpaqueConversionRefusal.None;
                    depthTestDivergence = false;
                    premultiplyNormalization = false;
                    return true;
                case PoiyomiOpaqueConversionOutcome.Convertible:
                    opaque = preparedOpaque ??
                        PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone(
                            live);
                    refusal = PoiyomiOpaqueConversionRefusal.None;
                    depthTestDivergence = eligibility.DepthTestDivergence;
                    premultiplyNormalization =
                        eligibility.PremultiplyNormalization;
                    return true;
                default:
                    opaque = null;
                    refusal = eligibility.Refusal;
                    depthTestDivergence = false;
                    premultiplyNormalization = false;
                    return false;
            }
        }

        private static AlphaSeparationSlotRefusal TryAdmitConversionEvidence(
            CapturedAlphaMaterial captured,
            IReadOnlyList<(CapturedFloatBinding Binding,
                           AnimatedPropertyRef Reference)> conversionBindings,
            IReadOnlyCollection<string> conversionPropertyNames,
            out CapturedMaterialEvidence derived)
        {
            if (!AdmittedMaterialStates.TryAdmitDerivedEvidence(
                    captured,
                    conversionBindings,
                    ConversionRequestForFamily(captured.Family),
                    out derived,
                    out _,
                    UnityAnimationEvidenceCapture.TextureScaleOffsetSuffix))
            {
                return AlphaSeparationSlotRefusal
                    .ConversionStateNotAdmitted;
            }

            foreach (var (property, canonicalValue) in
                         CanonicalPropertiesForFamily(captured.Family))
            {
                if (!conversionPropertyNames.Contains(property))
                {
                    continue;
                }

                if (!derived.TryGetScalar(
                        property, out var admitted) ||
                    admitted != canonicalValue)
                {
                    return AlphaSeparationSlotRefusal
                        .ConversionPropertyOverwrittenAtRuntime;
                }
            }

            return AlphaSeparationSlotRefusal.None;
        }

        internal static MaterialEvidenceRequest ConversionRequestForFamily(
            CapturedAlphaMaterialFamily family)
        {
            switch (family)
            {
                case CapturedAlphaMaterialFamily.Poiyomi:
                    return PoiyomiOpaqueConversion.ConversionEvidenceRequest;
                case CapturedAlphaMaterialFamily.LilToonCutout:
                    return LilToonCutoutSourceEligibility.ConversionEvidenceRequest;
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    return LilToonTransparentSourceEligibility
                        .ConversionEvidenceRequest;
                default:
                    return null;
            }
        }

        private static IReadOnlyList<(string Property, float Value)>
            CanonicalPropertiesForFamily(
            CapturedAlphaMaterialFamily family)
        {
            switch (family)
            {
                case CapturedAlphaMaterialFamily.Poiyomi:
                    return PoiyomiOpaqueConversion.CanonicalOpaqueProperties;
                case CapturedAlphaMaterialFamily.LilToonCutout:
                    return LilToonOpaqueTarget.CanonicalOpaqueProperties;
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    return LilToonOpaqueTarget.CanonicalOpaqueProperties;
                default:
                    return null;
            }
        }

        private static bool IsGrantedShader(
            string shaderName,
            IReadOnlyCollection<string> grantedShaderNames)
        {
            return !string.IsNullOrEmpty(shaderName) &&
                   grantedShaderNames != null &&
                   grantedShaderNames.Contains(shaderName);
        }
    }
}
