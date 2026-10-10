using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Semantics.LilToon;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The family-aware verified-fixture seams: selection, closed capture,
    /// and alpha resolution for every fixture family the public suite can
    /// stand in for. They encode production's own family requests and
    /// interpreters, so they are shared rather than copied: a second copy
    /// could drift from production while still passing. Only the vendor
    /// source attestation is bypassed, because no stand-in shader can pass
    /// it — exactly the reason the other verified seams exist.
    /// <para>
    /// Fixtures are distinguished by shader reference, never by name
    /// comparison: a material whose shader is not one of the fixture
    /// shaders selects nothing, and a fixture material renamed away from
    /// its fixture shader fails visibly as an unsupported family, never
    /// silently.
    /// </para>
    /// <para>
    /// The conversion seam below runs the real lilToon eligibility and clone
    /// recipe while substituting only vendor identity and target resolution.
    /// </para>
    /// </summary>
    internal static class VerifiedLilToonTestSeams
    {
        /// <summary>
        /// Family selection across all fixture families. The schema-complete
        /// cutout source stand-in selects <c>LilToonCutout</c> and the
        /// schema-complete transparent source stand-in selects
        /// <c>LilToonTransparent</c>, each with the combined
        /// alpha/conversion capture schema. Both opaque stand-ins select
        /// ordinary <c>LilToon</c> with its alpha-only request. The Poiyomi
        /// stand-in delegates to the existing Poiyomi seam, and the Two Pass
        /// stand-in selects the Two Pass family with the Two Pass request,
        /// whose capture schema is the alpha request alone because
        /// conversion never admits the family. Anything else selects
        /// nothing.
        /// </summary>
        internal static bool SelectVerifiedFixtureRequest(
            Material material,
            out CapturedAlphaMaterialFamily family,
            out MaterialEvidenceRequest alphaRelevance,
            out MaterialEvidenceRequest captureSchema)
        {
            if (UsesFixtureShader(
                    material, LilToonFixtureTestBase.CutoutConversionShaderName))
            {
                family = CapturedAlphaMaterialFamily.LilToonCutout;
                alphaRelevance =
                    LilToonCutoutMaterialSemantics.AlphaEvidenceRequest;
                captureSchema = MaterialEvidenceRequest.Combine(
                    alphaRelevance,
                    LilToonCutoutSourceEligibility.ConversionEvidenceRequest);
                return true;
            }

            if (UsesFixtureShader(
                    material, LilToonFixtureTestBase.TransparentConversionShaderName))
            {
                family = CapturedAlphaMaterialFamily.LilToonTransparent;
                alphaRelevance =
                    LilToonTransparentMaterialSemantics.AlphaEvidenceRequest;
                captureSchema = MaterialEvidenceRequest.Combine(
                    alphaRelevance,
                    LilToonTransparentSourceEligibility
                        .ConversionEvidenceRequest);
                return true;
            }

            if (UsesFixtureShader(material, LilToonFixtureTestBase.FixtureShaderName) ||
                UsesFixtureShader(
                    material, LilToonFixtureTestBase.OpaqueConversionShaderName))
            {
                family = CapturedAlphaMaterialFamily.LilToon;
                alphaRelevance = LilToonMaterialSemantics.AlphaEvidenceRequest;
                captureSchema = LilToonMaterialSemantics.AlphaEvidenceRequest;
                return true;
            }

            if (UsesFixtureShader(
                    material, PoiyomiFixtureTestBase.FixtureShaderName))
            {
                return VerifiedPoiyomiTestSeams.SelectVerifiedFixtureRequest(
                    material, out family, out alphaRelevance,
                    out captureSchema);
            }

            if (UsesFixtureShader(
                    material, PoiyomiFixtureTestBase.TwoPassFixtureShaderName))
            {
                family = CapturedAlphaMaterialFamily.PoiyomiTwoPass;
                alphaRelevance =
                    PoiyomiMaterialSemantics.TwoPassAlphaEvidenceRequest;
                captureSchema =
                    PoiyomiMaterialSemantics.TwoPassAlphaEvidenceRequest;
                return true;
            }

            family = CapturedAlphaMaterialFamily.Unsupported;
            alphaRelevance = null;
            captureSchema = null;
            return false;
        }

        /// <summary>
        /// The closed capture for a mixed batch: one capture under the union
        /// request, then per-family source-evidence gathering with vendor
        /// attestation bypassed — the gathered evidence objects are the
        /// production ones; verification simply never runs here, because
        /// every stand-in would fail it.
        /// </summary>
        internal static bool CaptureVerifiedFixtureMaterials(
            IReadOnlyList<Material> materials,
            IReadOnlyList<CapturedAlphaMaterialFamily> families,
            MaterialEvidenceRequest request,
            AlphaPolicyBounds bounds,
            out ClosedAlphaCaptureOutcome captured)
        {
            var shaders = new Shader[materials.Count];
            var inputs = new MaterialEvidenceCaptureInput[materials.Count];
            for (var index = 0; index < materials.Count; index++)
            {
                shaders[index] = materials[index] == null
                    ? null
                    : materials[index].shader;
                inputs[index] = new MaterialEvidenceCaptureInput(
                    materials[index],
                    request,
                    UnityMaterialSemantics.AlphaPredicateRequestFor(
                        materials[index], families[index]));
            }

            var evidence = UnityMaterialEvidenceCapture.Capture(
                inputs, bounds);
            var result = new CapturedAlphaMaterial[materials.Count];
            for (var index = 0; index < result.Length; index++)
            {
                var poiyomi = default(PoiyomiSourceEvidence);
                LilToonSourceEvidence lilToon = null;
                LilToonMultiResolutionRecord multiResolution = null;
                var family = families[index];
                if (family == CapturedAlphaMaterialFamily.LilToonMulti)
                {
                    // The profile injection seam the Task 6 verify fixtures
                    // established: the captured evidence resolves against
                    // one test-only container row plus the matching
                    // synthetic identity, with the keyword-request fact the
                    // shared batch request carries. The verdict order, the
                    // stored record, and the family rewrite are the
                    // production ones; only the row source is injected,
                    // because the production table stays empty until the
                    // Task 2 digest measurement.
                    multiResolution = ResolveMultiThroughProfileSeam(
                        evidence[index],
                        request.CaptureKeywords,
                        out family);
                    if (multiResolution.IsResolved)
                    {
                        lilToon = multiResolution.SourceEvidence;
                    }
                }
                else
                {
                    switch (family)
                    {
                        case CapturedAlphaMaterialFamily.Poiyomi:
                            poiyomi = PoiyomiMaterialSemantics
                                .GatherAlphaSourceEvidence(
                                    shaders[index], evidence[index]);
                            break;
                        case CapturedAlphaMaterialFamily.PoiyomiTwoPass:
                            // Both Poiyomi identities verify through one
                            // conjunction, so the gather is the same function
                            // the plain family uses.
                            poiyomi = PoiyomiMaterialSemantics
                                .GatherAlphaSourceEvidence(
                                    shaders[index], evidence[index]);
                            break;
                        case CapturedAlphaMaterialFamily.LilToon:
                            lilToon = LilToonSourceAttestation.GatherSourceEvidence(
                                shaders[index], evidence[index]);
                            break;
                        case CapturedAlphaMaterialFamily.LilToonCutout:
                            lilToon = LilToonSourceAttestation
                                .GatherCutoutSourceEvidence(
                                    shaders[index], evidence[index]);
                            break;
                        case CapturedAlphaMaterialFamily.LilToonTransparent:
                            lilToon = LilToonSourceAttestation
                                .GatherTransparentSourceEvidence(
                                    shaders[index], evidence[index]);
                            break;
                    }
                }

                var source = materials[index];
                result[index] = new CapturedAlphaMaterial(
                    family, evidence[index], poiyomi, lilToon,
                    multiResolution: multiResolution,
                    materialPath: source != null
                        ? AssetDatabase.GetAssetPath(source)
                        : null,
                    materialName: source != null ? source.name : null,
                    shaderName: source != null && source.shader != null
                        ? source.shader.name
                        : null);
            }

            captured = new ClosedAlphaCaptureOutcome(
                result, Array.Empty<int>());
            return true;
        }

        /// <summary>
        /// Resolves one captured Multi material through the profile
        /// injection seam: the synthetic identity evidence and the test-only
        /// row that match by construction, named after the container the
        /// captured evidence carries. A refusal keeps the Multi family,
        /// exactly as the production hub stores it.
        /// </summary>
        private static LilToonMultiResolutionRecord
            ResolveMultiThroughProfileSeam(
                CapturedMaterialEvidence evidence,
                bool keywordsRequested,
                out CapturedAlphaMaterialFamily family)
        {
            var containerName = evidence.HasShaderName
                ? evidence.ShaderName
                : LilToonMultiResolutionTests.BaseContainerName;
            var sourceEvidence =
                LilToonMultiResolutionTests.MatchingSourceEvidence(
                    containerName);
            var resolved = LilToonMultiResolution.Resolve(
                evidence,
                sourceEvidence,
                LilToonMultiResolutionTests.MatchingProfile(containerName),
                keywordsRequested,
                out family,
                out _,
                out var refusal);
            if (!resolved)
            {
                family = CapturedAlphaMaterialFamily.LilToonMulti;
                return LilToonMultiResolutionRecord.Refused(refusal);
            }

            return LilToonMultiResolutionRecord.Admitted(sourceEvidence);
        }

        /// <summary>
        /// Alpha-only resolution routed per family to the four production
        /// interpreters; a material no fixture family attests is
        /// all-Unknown, the conservative answer.
        /// </summary>
        internal static CapturedAlphaSemantics VerifiedAlphaOnly(
            CapturedAlphaMaterial material)
        {
            AlphaUnknownReason unknownReason;
            SemanticOutput<ScalarSemanticValue> alpha;
            switch (material.Family)
            {
                case CapturedAlphaMaterialFamily.Poiyomi:
                    alpha = PoiyomiMaterialSemantics.InterpretVerifiedAlpha(
                        material.Evidence, out unknownReason);
                    break;
                case CapturedAlphaMaterialFamily.PoiyomiTwoPass:
                    alpha = PoiyomiMaterialSemantics
                        .InterpretVerifiedTwoPassAlpha(
                            material.Evidence, out unknownReason);
                    break;
                case CapturedAlphaMaterialFamily.LilToon:
                    alpha = LilToonMaterialSemantics.InterpretVerifiedAlpha(
                        material.Evidence, out unknownReason);
                    break;
                case CapturedAlphaMaterialFamily.LilToonCutout:
                    alpha = LilToonCutoutMaterialSemantics
                        .InterpretVerifiedCutoutAlpha(
                            material.Evidence, out unknownReason);
                    break;
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    alpha = LilToonTransparentMaterialSemantics
                        .InterpretVerifiedTransparentAlpha(
                            material.Evidence, out unknownReason);
                    break;
                default:
                    // Production parity: a material no family selects is
                    // all-Unknown, and the report names the shader the
                    // capture recorded.
                    return new CapturedAlphaSemantics(
                        EvidenceGates.AllUnknown(),
                        AlphaUnknownReason.UnsupportedShader(
                            material.ShaderName));
            }

            return new CapturedAlphaSemantics(
                new MaterialSemantics(
                    SemanticOutput<ColorSemanticValue>.Unknown(),
                    alpha,
                    SemanticOutput<ColorSemanticValue>.Unknown(),
                    SemanticOutput<NormalSemanticValue>.Unknown()),
                unknownReason);
        }

        /// <summary>
        /// The fifth verified-fixture seam, substituting only the
        /// cutout-family opaque-conversion step for one admitted material:
        /// effective render state, real <c>LilToonCutoutSourceEligibility</c>
        /// eligibility, and the real canonical clone recipe with the
        /// tuple-carrying opaque stand-in shader passed as the attested
        /// target. Only the source-identity check and the production
        /// target-asset resolution are skipped, because no stand-in can pass
        /// the former and the vendor package is absent from this project —
        /// exactly the reason the other verified seams exist. Admission,
        /// relevance resolution, planning, validation, finalization, the
        /// sweep and the apply boundary are the production code in every
        /// caller.
        /// </summary>
        internal static bool VerifiedConversion(
            Material live,
            CapturedMaterialEvidence derived,
            bool allowDepthTestChange,
            Material preparedOpaque,
            out Material opaque,
            out LilToonOpaqueConversionRefusal refusal,
            out bool depthTestDivergence)
        {
            return VerifiedOpaqueConversion(
                LilToonCutoutSourceEligibility.EvaluateVerifiedEligibility,
                live,
                derived,
                allowDepthTestChange,
                preparedOpaque,
                out opaque,
                out refusal,
                out depthTestDivergence);
        }

        /// <summary>
        /// The verified-fixture seam for the transparent conversion family
        /// — the exact shape of <see cref="VerifiedConversion"/> with the
        /// transparent eligibility substituted: effective render state,
        /// real <c>LilToonTransparentSourceEligibility</c> eligibility, and
        /// the same canonical clone recipe with the tuple-carrying opaque
        /// stand-in shader passed as the attested target. Only the
        /// source-identity check and the production target-asset resolution
        /// are skipped, exactly as for the cutout seam.
        /// </summary>
        internal static bool VerifiedTransparentConversionStep(
            Material live,
            CapturedMaterialEvidence derived,
            bool allowDepthTestChange,
            Material preparedOpaque,
            out Material opaque,
            out LilToonOpaqueConversionRefusal refusal,
            out bool depthTestDivergence)
        {
            return VerifiedOpaqueConversion(
                LilToonTransparentSourceEligibility.EvaluateVerifiedEligibility,
                live,
                derived,
                allowDepthTestChange,
                preparedOpaque,
                out opaque,
                out refusal,
                out depthTestDivergence);
        }

        /// <summary>
        /// The shared body of the two conversion seams: effective render
        /// state, the caller's family eligibility evaluation, and the real
        /// canonical clone recipe with the tuple-carrying opaque stand-in
        /// shader passed as the attested target. Only the source-identity
        /// check and the production target-asset resolution are skipped,
        /// because no stand-in can pass the former and the vendor package
        /// is absent from this project — exactly the reason the other
        /// verified seams exist.
        /// </summary>
        private static bool VerifiedOpaqueConversion(
            Func<
                CapturedMaterialEvidence,
                int,
                string,
                bool,
                LilToonOpaqueConversionEligibility> evaluateEligibility,
            Material live,
            CapturedMaterialEvidence derived,
            bool allowDepthTestChange,
            Material preparedOpaque,
            out Material opaque,
            out LilToonOpaqueConversionRefusal refusal,
            out bool depthTestDivergence)
        {
            EffectiveRenderState.ReadEffectiveRenderState(
                live, out var queue, out var renderType);
            var eligibility = evaluateEligibility(
                derived, queue, renderType, allowDepthTestChange);
            // The tuple-carrying opaque stand-in is the attested target.
            return AlphaSeparationPreparation.TryMapLilToonOutcome(
                eligibility,
                () => preparedOpaque ??
                    LilToonOpaqueTarget.PrepareCanonicalOpaqueClone(
                        live, Shader.Find(
                            LilToonFixtureTestBase
                                .OpaqueConversionShaderName)),
                out opaque, out refusal, out depthTestDivergence);
        }

        /// <summary>
        /// Fixtures are matched by shader reference: the material's shader
        /// must be the very shader object the fixture name resolves to.
        /// </summary>
        private static bool UsesFixtureShader(
            Material material,
            string fixtureShaderName)
        {
            if (material == null || material.shader == null)
            {
                return false;
            }

            var fixture = Shader.Find(fixtureShaderName);
            return fixture != null && fixture == material.shader;
        }

    }
}
