using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The verified-frontend seams the package's public fixtures substitute for
    /// unavailable vendor source attestation. They encode production's own
    /// Poiyomi family, request, semantics and opaque-conversion mapping, so they
    /// are shared rather than copied: a second copy could drift from production
    /// while still passing.
    /// <para>
    /// Moved here unchanged from <see cref="AmusePlatformFinishPluginTests"/>,
    /// which now delegates to them. This is a mechanical extraction, not a
    /// fixture framework: nothing else is promoted, and every other fixture
    /// helper stays private to the class that uses it.
    /// </para>
    /// </summary>
    internal static class VerifiedPoiyomiTestSeams
    {
        /// <summary>
        /// The fourth verified-fixture seam, substituting only the shader-family
        /// opaque-conversion step for one admitted material: effective render
        /// state, real <c>PoiyomiOpaqueConversion</c> eligibility, and the real
        /// canonical clone recipe. Only the source-identity check is skipped,
        /// because no stand-in shader can pass it — exactly the reason the
        /// other three seams exist. Admission, relevance resolution, planning,
        /// validation, finalization, the sweep and the apply boundary are the
        /// production code in every caller.
        /// </summary>
        internal static bool VerifiedConversion(
            Material live,
            CapturedMaterialEvidence derived,
            Material preparedOpaque,
            out Material opaque,
            out PoiyomiOpaqueConversionRefusal refusal)
        {
            PoiyomiOpaqueConversion.ReadEffectiveRenderState(
                live, out var queue, out var renderType);
            var eligibility = PoiyomiOpaqueConversion
                .EvaluateVerifiedEligibility(derived, queue, renderType);
            switch (eligibility.Outcome)
            {
                case PoiyomiOpaqueConversionOutcome.AlreadyOpaque:
                    opaque = live;
                    refusal = PoiyomiOpaqueConversionRefusal.None;
                    return true;
                case PoiyomiOpaqueConversionOutcome.Convertible:
                    // An already-prepared artifact for this source is reused
                    // here; only a first conversion creates the canonical
                    // clone.
                    opaque = preparedOpaque ??
                        PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone(
                            live);
                    refusal = PoiyomiOpaqueConversionRefusal.None;
                    return true;
                default:
                    opaque = null;
                    refusal = eligibility.Refusal;
                    return false;
            }
        }

        /// <summary>
        /// Mirrors production's Poiyomi mapping: alpha proof considers the
        /// family's alpha request, while the closed capture also gathers
        /// conversion evidence.
        /// </summary>
        internal static bool SelectVerifiedFixtureRequest(
            Material material,
            out CapturedAlphaMaterialFamily family,
            out MaterialEvidenceRequest alphaRelevance,
            out MaterialEvidenceRequest captureSchema)
        {
            family = CapturedAlphaMaterialFamily.Poiyomi;
            alphaRelevance = PoiyomiMaterialSemantics.AlphaEvidenceRequest;
            captureSchema = MaterialEvidenceRequest.Combine(
                alphaRelevance,
                PoiyomiOpaqueConversion.ConversionEvidenceRequest);
            return material != null;
        }

        internal static bool CaptureVerifiedFixtureMaterials(
            IReadOnlyList<Material> materials,
            IReadOnlyList<CapturedAlphaMaterialFamily> families,
            MaterialEvidenceRequest request,
            out IReadOnlyList<CapturedAlphaMaterial> captured)
        {
            var inputs = new MaterialEvidenceCaptureInput[materials.Count];
            for (var index = 0; index < materials.Count; index++)
            {
                inputs[index] = new MaterialEvidenceCaptureInput(
                    materials[index], request);
            }

            var evidence = UnityMaterialEvidenceCapture.Capture(inputs);
            var result = new CapturedAlphaMaterial[materials.Count];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new CapturedAlphaMaterial(
                    families[index],
                    evidence[index],
                    default(PoiyomiSourceEvidence),
                    null);
            }

            captured = result;
            return true;
        }

        internal static MaterialSemantics VerifiedAlphaOnly(
            CapturedAlphaMaterial material)
        {
            return new MaterialSemantics(
                SemanticOutput<ColorSemanticValue>.Unknown(),
                PoiyomiMaterialSemantics.InterpretVerifiedAlpha(
                    material.Evidence),
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<NormalSemanticValue>.Unknown());
        }
    }
}
