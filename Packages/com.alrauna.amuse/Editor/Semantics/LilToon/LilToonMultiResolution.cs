using System;
using Alrauna.Amuse.Editor.Host;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// Turns one captured Multi material into one resolved regular family
    /// and its mode, or into one named refusal from the closed Multi
    /// vocabulary. The hub classifies the container name, captures under
    /// the combined Multi request, and calls the resolver once per material
    /// at the one post-capture resolution point. No per-family switch
    /// dispatches resolution, so no downstream switch ever sees the Multi
    /// family on an admitted material.
    /// <para>
    /// The verdict order is fixed, and the first refusal wins. The
    /// specialized containers refuse by container identity before
    /// attestation matters. Then the container attests through the Task 6
    /// row verify. Then the keyword-evidence precondition reads the routing
    /// fact the capture request owns. Then the mode-consistency gate runs.
    /// Then the mode maps onto the regular family. The effective render
    /// queue and the RenderType tag are inputs the eligibility layers own,
    /// and the closed refusal vocabulary carries no queue value, so the
    /// resolver reads neither.
    /// </para>
    /// </summary>
    internal static class LilToonMultiResolution
    {
        /// <summary>
        /// The two supported Multi container identities. They are live
        /// container names at the upstream pin, not measured digests: the
        /// Task 2 row table carries the measured half of each identity.
        /// </summary>
        internal const string BaseContainerShaderName = "_lil/lilToonMulti";
        internal const string OutlineContainerShaderName =
            "Hidden/lilToonMultiOutline";

        /// <summary>
        /// The specialized Multi containers. They carry no digest rows at
        /// all, so they refuse by container identity alone.
        /// </summary>
        private static readonly string[] SpecializedContainerShaderNames =
        {
            "Hidden/lilToonMultiRefraction",
            "Hidden/lilToonMultiFur",
            "Hidden/lilToonMultiGem",
        };

        private const string ModeProperty = "_TransparentMode";
        private const string ClippingCancellerProperty =
            "_UseClippingCanceller";
        private const string OverlayProperty = "_AsOverlay";

        /// <summary>
        /// The combined Multi evidence request: the alpha facts of all three
        /// resolved regular families, plus the three Multi scalars the mode
        /// read and the gate rules consume, plus the keyword set the gate
        /// compares. The union is mode-independent because the capture runs
        /// before the one resolution point, and a resolved mode-1 or mode-2
        /// material interprets through the cutout or transparent
        /// interpreter, whose gates read schema facts the opaque request
        /// alone never carried; an interpreter read of an unrequested name
        /// throws, so the resolved family's own schema must ride the one
        /// Multi capture. Every supported container classifies under this
        /// one request, so one capture serves the resolution and the
        /// interpretation on both containers.
        /// </summary>
        internal static readonly MaterialEvidenceRequest
            MultiEvidenceRequest =
                MaterialEvidenceRequest.Combine(
                    LilToonMaterialSemantics.AlphaEvidenceRequest,
                    LilToonCutoutMaterialSemantics.AlphaEvidenceRequest,
                    LilToonTransparentMaterialSemantics.AlphaEvidenceRequest,
                    new MaterialEvidenceRequest(
                        shaderName: true,
                        activeColorSpace: false,
                        presenceProperties: Array.Empty<string>(),
                        scalarProperties: new[]
                        {
                            ModeProperty,
                            ClippingCancellerProperty,
                            OverlayProperty,
                            // The keyword-writer condition inputs
                            // (lilMaterialUtils.cs:364-391 at the pin). The
                            // mode gate's derivation table reads one scalar
                            // per feature row; an unrequested name reads as
                            // feature-off, so the request must carry every
                            // input a table row can consult.
                            "_UseShadow",
                            "_UseRimShade",
                            "_UseEmission",
                            "_UseEmission2nd",
                            "_UseBumpMap",
                            "_UseBump2ndMap",
                            "_UseAnisotropy",
                            "_UseMatCap",
                            "_UseMatCap2nd",
                            "_MatCapCustomNormal",
                            "_MatCap2ndCustomNormal",
                            "_UseRim",
                            "_RimDirStrength",
                            "_UseGlitter",
                            "_UseAudioLink",
                            "_AudioLinkAsLocal",
                            "_UseBacklight",
                            "_UseParallax",
                            "_UsePOM",
                            "_UseReflection",
                            "_MainGradationStrength",
                            "_UseMain2ndTex",
                            "_UseMain3rdTex",
                            "_UseDither",
                            "_AlphaMaskMode",
                        },
                        colorProperties: Array.Empty<string>(),
                        vectorProperties: new[]
                        {
                            "_MainTexHSVG",
                            "_Main2ndTexDecalAnimation",
                            "_Main3rdTexDecalAnimation",
                            "_Main2ndDissolveParams",
                            "_Main3rdDissolveParams",
                            "_DissolveParams",
                            "_DistanceFade",
                            "_OutlineTexHSVG",
                        },
                        textureProperties: new[]
                        {
                            // Assignment-only evidence for the emission blend
                            // masks the writer's _SUNDISK_SIMPLE row consults
                            // (lilMaterialUtils.cs:387-388 at the pin).
                            new TexturePropertyEvidenceRequest(
                                "_EmissionBlendMask",
                                TextureEvidenceKinds.None),
                            new TexturePropertyEvidenceRequest(
                                "_Emission2ndBlendMask",
                                TextureEvidenceKinds.None),
                        },
                        captureKeywords: true));

        /// <summary>
        /// The profile injection seam, shaped for the Task 6 verify
        /// fixtures: the gathered identity evidence and one injected
        /// container row, so the admit path runs without the Task 2
        /// measured digests. The verdict order is the resolver's own.
        /// </summary>
        internal static bool Resolve(
            CapturedMaterialEvidence evidence,
            LilToonSourceEvidence sourceEvidence,
            LilToonMultiContainerProfile profile,
            bool keywordsRequested,
            int effectiveRenderQueue,
            string effectiveRenderType,
            out CapturedAlphaMaterialFamily family,
            out int mode,
            out LilToonMultiResolutionRefusal refusal)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            family = CapturedAlphaMaterialFamily.Unsupported;
            mode = 0;

            // 1. Container identity. The specialized containers carry no
            //    digest rows, so their refusal precedes every attestation
            //    question.
            if (IsSpecializedContainerShaderName(evidence.ShaderName))
            {
                refusal = LilToonMultiResolutionRefusal.SpecializedContainer;
                return false;
            }

            // 2. Source attestation. The Task 6 conjunction verifies the
            //    container name, the asset GUID, the format stamp, the
            //    package identity, the include tree, and the canonical
            //    digest against the row.
            if (!LilToonSourceAttestation.TryVerifyMultiContainer(
                    sourceEvidence, profile, out refusal))
            {
                return false;
            }

            return GateAndMap(
                evidence, keywordsRequested, out family, out mode,
                out refusal);
        }

        /// <summary>
        /// The production signature: resolves the container row from the
        /// captured shader identity and delegates to the seam overload. The
        /// row table ships empty until the Task 2 digest measurement, so
        /// this entry refuses every supported container with
        /// AttestationFailed until then. Specialized containers refuse by
        /// identity first, which is why this entry can answer before any
        /// live shader is resolved. The entry asserts the closed Multi
        /// capture request named the keyword set, because the evidence
        /// record cannot carry the requested-and-not-requested fact apart.
        /// </summary>
        internal static bool Resolve(
            CapturedMaterialEvidence evidence,
            int effectiveRenderQueue,
            string effectiveRenderType,
            out CapturedAlphaMaterialFamily family,
            out int mode,
            out LilToonMultiResolutionRefusal refusal)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            family = CapturedAlphaMaterialFamily.Unsupported;
            mode = 0;
            if (IsSpecializedContainerShaderName(evidence.ShaderName))
            {
                refusal = LilToonMultiResolutionRefusal.SpecializedContainer;
                return false;
            }

            // Name-only re-resolution, fail-closed: a second asset that
            // carries the same name attests through its own GUID at the row
            // verify and refuses there.
            var shader = evidence.HasShaderName
                ? Shader.Find(evidence.ShaderName)
                : null;
            if (shader == null)
            {
                refusal = LilToonMultiResolutionRefusal.AttestationFailed;
                return false;
            }

            var record = ResolveCapturedMaterial(
                shader,
                evidence,
                keywordsRequested: true,
                effectiveRenderQueue,
                effectiveRenderType,
                out family,
                out mode);
            refusal = record.Refusal;
            return record.IsResolved;
        }

        /// <summary>
        /// The one production entry the capture assembly point calls for a
        /// Multi-classified material. It resolves the live shader's
        /// container row, attests through the Task 6 verify, gates the
        /// captured state, and maps the mode. On success the record carries
        /// the gathered source evidence, so the capture stores the verified
        /// gather once and the analyze path never re-canonicalizes the
        /// shader. On refusal the record carries the closed refusal value.
        /// <paramref name="keywordsRequested"/> is the routing fact the
        /// capture request owns, not an evidence fact.
        /// </summary>
        internal static LilToonMultiResolutionRecord ResolveCapturedMaterial(
            Shader shader,
            CapturedMaterialEvidence evidence,
            bool keywordsRequested,
            int effectiveRenderQueue,
            string effectiveRenderType,
            out CapturedAlphaMaterialFamily family,
            out int mode)
        {
            family = CapturedAlphaMaterialFamily.Unsupported;
            mode = 0;

            if (shader == null || evidence == null)
            {
                return LilToonMultiResolutionRecord.Refused(
                    LilToonMultiResolutionRefusal.AttestationFailed);
            }

            if (IsSpecializedContainerShaderName(evidence.ShaderName))
            {
                return LilToonMultiResolutionRecord.Refused(
                    LilToonMultiResolutionRefusal.SpecializedContainer);
            }

            if (!LilToonSourceAttestation.TryResolveMultiContainer(
                    shader,
                    evidence,
                    out var sourceEvidence,
                    out var profile,
                    out var refusal))
            {
                return LilToonMultiResolutionRecord.Refused(refusal);
            }

            if (!Resolve(
                    evidence,
                    sourceEvidence,
                    profile,
                    keywordsRequested,
                    effectiveRenderQueue,
                    effectiveRenderType,
                    out family,
                    out mode,
                    out refusal))
            {
                return LilToonMultiResolutionRecord.Refused(refusal);
            }

            return LilToonMultiResolutionRecord.Admitted(sourceEvidence);
        }

        /// <summary>
        /// True for the two supported Multi container names. This is the
        /// classifier branch's predicate, so it stays an exact-name test.
        /// </summary>
        internal static bool IsSupportedContainerShaderName(
            string shaderName)
        {
            return string.Equals(
                       shaderName,
                       BaseContainerShaderName,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       shaderName,
                       OutlineContainerShaderName,
                       StringComparison.Ordinal);
        }

        /// <summary>
        /// True for every Multi container name, supported or specialized.
        /// The canonical target of a Multi container is the same asset, so
        /// the canonicalizer resolves each name to itself.
        /// </summary>
        internal static bool IsMultiContainerShaderName(string shaderName)
        {
            return IsSupportedContainerShaderName(shaderName) ||
                IsSpecializedContainerShaderName(shaderName);
        }

        /// <summary>
        /// The refusal value in words, for the shared cause kind's feature
        /// field. The slot-refusal report renders the words through the
        /// Multi cause sentence.
        /// </summary>
        internal static string RefusalFeatureWords(
            LilToonMultiResolutionRefusal refusal)
        {
            switch (refusal)
            {
                case LilToonMultiResolutionRefusal.ModeOutsideAdmittedSet:
                    return "the Multi mode is outside the supported modes";
                case LilToonMultiResolutionRefusal.SpecializedContainer:
                    return "the shader is a specialized Multi container";
                case LilToonMultiResolutionRefusal.KeywordModeMismatch:
                    return "the material keywords do not match the Multi " +
                        "mode";
                case LilToonMultiResolutionRefusal.ClippingCancellerEnabled:
                    return "the clipping canceller is enabled";
                case LilToonMultiResolutionRefusal
                    .OverlayPassEnableUnsupported:
                    return "the overlay pass is enabled";
                case LilToonMultiResolutionRefusal.AttestationFailed:
                    return "the shader source does not verify";
                case LilToonMultiResolutionRefusal.MissingKeywordEvidence:
                    return "the keyword capture is missing";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(refusal));
            }
        }

        /// <summary>
        /// The resolver tail both entries share: the keyword-evidence
        /// precondition, then the mode read, then the mode-consistency
        /// gate, then the mode mapping. The gate already refuses the modes
        /// outside the admitted set with its own refusal value, so the
        /// mapping below only sees the modes zero to two.
        /// </summary>
        private static bool GateAndMap(
            CapturedMaterialEvidence evidence,
            bool keywordsRequested,
            out CapturedAlphaMaterialFamily family,
            out int mode,
            out LilToonMultiResolutionRefusal refusal)
        {
            family = CapturedAlphaMaterialFamily.Unsupported;
            mode = 0;

            // 3. The routing invariant: the resolver never reads a missing
            //    keyword capture as the empty consistent set the
            //    derivation produces at mode zero. The capture request owns
            //    the fact.
            if (!keywordsRequested)
            {
                refusal = LilToonMultiResolutionRefusal
                    .MissingKeywordEvidence;
                return false;
            }

            // 4. The mode read and the mode-consistency gate.
            if (!TryReadMode(evidence, out mode, out refusal))
            {
                return false;
            }

            if (!LilToonMultiModeGate.Evaluate(evidence, mode, out refusal))
            {
                return false;
            }

            // 5. The regular family the mode resolves to. The mapping is
            //    mode-driven and container-independent, so the outline
            //    container resolves onto the same families as the base.
            if (mode == 0)
            {
                family = CapturedAlphaMaterialFamily.LilToon;
            }
            else if (mode == 1)
            {
                family = CapturedAlphaMaterialFamily.LilToonCutout;
            }
            else
            {
                family = CapturedAlphaMaterialFamily.LilToonTransparent;
            }

            refusal = default;
            return true;
        }

        /// <summary>
        /// Reads the requested Multi mode. A missing property reads as the
        /// vendor zero fallback, the same missing-fact policy the gate's
        /// own scalar reads follow. A non-finite or fractional mode is not
        /// a state the vendor derivation produces, and the conversion to
        /// the gate's integer would silently admit it, so it refuses with
        /// the gate's own out-of-set value.
        /// </summary>
        private static bool TryReadMode(
            CapturedMaterialEvidence evidence,
            out int mode,
            out LilToonMultiResolutionRefusal refusal)
        {
            if (!evidence.TryGetScalar(ModeProperty, out var value))
            {
                value = 0f;
            }

            if (float.IsNaN(value) ||
                float.IsInfinity(value) ||
                value != MathF.Floor(value) ||
                value < int.MinValue ||
                value > int.MaxValue)
            {
                mode = 0;
                refusal = LilToonMultiResolutionRefusal
                    .ModeOutsideAdmittedSet;
                return false;
            }

            mode = (int)value;
            refusal = default;
            return true;
        }

        /// <summary>
        /// The captured Multi mode, with the resolver's own read policy: a
        /// missing property reads as the vendor zero fallback, and a
        /// non-finite or fractional value refuses. The conversion
        /// boundary's eligibility conditional reads the mode off the same
        /// evidence the resolution gated, so the boundary never re-derives
        /// a second mode fact. A stored resolution implies this read
        /// succeeds: the resolver refused the unreadable states before any
        /// record was stored.
        /// </summary>
        internal static bool TryReadCapturedMode(
            CapturedMaterialEvidence evidence,
            out int mode)
        {
            return TryReadMode(evidence, out mode, out _);
        }

        private static bool IsSpecializedContainerShaderName(
            string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName))
            {
                return false;
            }

            for (var index = 0;
                index < SpecializedContainerShaderNames.Length;
                index++)
            {
                if (string.Equals(
                        shaderName,
                        SpecializedContainerShaderNames[index],
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// The typed record of one Multi material's resolution, stored on the
    /// captured material at the one post-capture resolution point. It is
    /// the only place the Multi verdict travels after capture. A record
    /// whose source evidence is set resolved; a record without one carries
    /// the closed refusal value and names it.
    /// </summary>
    internal sealed class LilToonMultiResolutionRecord
    {
        private LilToonMultiResolutionRecord(
            LilToonSourceEvidence sourceEvidence,
            LilToonMultiResolutionRefusal refusal)
        {
            SourceEvidence = sourceEvidence;
            Refusal = refusal;
        }

        /// <summary>
        /// The gathered source evidence of the verified container. Set only
        /// on a resolution, so the analyze path can interpret without a
        /// second canonicalization pass.
        /// </summary>
        internal LilToonSourceEvidence SourceEvidence { get; }

        /// <summary>The closed refusal value. Meaningful only when the
        /// record did not resolve.</summary>
        internal LilToonMultiResolutionRefusal Refusal { get; }

        /// <summary>True when the resolver admitted the material and
        /// mapped its mode onto the regular family.</summary>
        internal bool IsResolved => SourceEvidence != null;

        internal static LilToonMultiResolutionRecord Admitted(
            LilToonSourceEvidence sourceEvidence)
        {
            if (sourceEvidence == null)
            {
                throw new ArgumentNullException(nameof(sourceEvidence));
            }

            return new LilToonMultiResolutionRecord(
                sourceEvidence, default);
        }

        internal static LilToonMultiResolutionRecord Refused(
            LilToonMultiResolutionRefusal refusal)
        {
            return new LilToonMultiResolutionRecord(null, refusal);
        }
    }
}
