using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Semantics.LilToon;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
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
        /// cutout source stand-in selects <c>LilToonCutout</c> with the
        /// combined alpha/conversion capture schema. Both opaque stand-ins
        /// select ordinary <c>LilToon</c> with its alpha-only request. The
        /// Poiyomi stand-in delegates to the existing Poiyomi seam; anything
        /// else selects nothing.
        /// </summary>
        internal static bool SelectVerifiedFixtureRequest(
            Material material,
            out CapturedAlphaMaterialFamily family,
            out MaterialEvidenceRequest alphaRelevance,
            out MaterialEvidenceRequest captureSchema)
        {
            if (UsesFixtureShader(
                    material, LilToonFixtureShaderNames.Cutout))
            {
                family = CapturedAlphaMaterialFamily.LilToonCutout;
                alphaRelevance =
                    LilToonCutoutMaterialSemantics.AlphaEvidenceRequest;
                captureSchema = MaterialEvidenceRequest.Combine(
                    alphaRelevance,
                    LilToonOpaqueConversion.ConversionEvidenceRequest);
                return true;
            }

            if (UsesFixtureShader(material, LilToonFixtureShaderNames.Opaque) ||
                UsesFixtureShader(
                    material, LilToonFixtureShaderNames.OpaqueTarget))
            {
                family = CapturedAlphaMaterialFamily.LilToon;
                alphaRelevance = LilToonMaterialSemantics.AlphaEvidenceRequest;
                captureSchema = LilToonMaterialSemantics.AlphaEvidenceRequest;
                return true;
            }

            if (UsesFixtureShader(
                    material, PoiyomiFixtureShaderNames.Fixture))
            {
                return VerifiedPoiyomiTestSeams.SelectVerifiedFixtureRequest(
                    material, out family, out alphaRelevance,
                    out captureSchema);
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
            out IReadOnlyList<CapturedAlphaMaterial> captured)
        {
            var shaders = new Shader[materials.Count];
            var inputs = new MaterialEvidenceCaptureInput[materials.Count];
            for (var index = 0; index < materials.Count; index++)
            {
                shaders[index] = materials[index] == null
                    ? null
                    : materials[index].shader;
                inputs[index] = new MaterialEvidenceCaptureInput(
                    materials[index], request);
            }

            var evidence = UnityMaterialEvidenceCapture.Capture(inputs);
            var result = new CapturedAlphaMaterial[materials.Count];
            for (var index = 0; index < result.Length; index++)
            {
                var poiyomi = default(PoiyomiSourceEvidence);
                LilToonSourceEvidence lilToon = null;
                switch (families[index])
                {
                    case CapturedAlphaMaterialFamily.Poiyomi:
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
                }

                result[index] = new CapturedAlphaMaterial(
                    families[index], evidence[index], poiyomi, lilToon);
            }

            captured = result;
            return true;
        }

        /// <summary>
        /// Alpha-only resolution routed per family to the three production
        /// interpreters; a material no fixture family attests is all-Unknown,
        /// the conservative answer.
        /// </summary>
        internal static MaterialSemantics VerifiedAlphaOnly(
            CapturedAlphaMaterial material)
        {
            switch (material.Family)
            {
                case CapturedAlphaMaterialFamily.Poiyomi:
                    return new MaterialSemantics(
                        SemanticOutput<ColorSemanticValue>.Unknown(),
                        PoiyomiMaterialSemantics.InterpretVerifiedAlpha(
                            material.Evidence),
                        SemanticOutput<ColorSemanticValue>.Unknown(),
                        SemanticOutput<NormalSemanticValue>.Unknown());
                case CapturedAlphaMaterialFamily.LilToon:
                    return new MaterialSemantics(
                        SemanticOutput<ColorSemanticValue>.Unknown(),
                        LilToonMaterialSemantics.InterpretVerifiedAlpha(
                            material.Evidence),
                        SemanticOutput<ColorSemanticValue>.Unknown(),
                        SemanticOutput<NormalSemanticValue>.Unknown());
                case CapturedAlphaMaterialFamily.LilToonCutout:
                    return new MaterialSemantics(
                        SemanticOutput<ColorSemanticValue>.Unknown(),
                        LilToonCutoutMaterialSemantics
                            .InterpretVerifiedCutoutAlpha(material.Evidence),
                        SemanticOutput<ColorSemanticValue>.Unknown(),
                        SemanticOutput<NormalSemanticValue>.Unknown());
                default:
                    return UnityMaterialSemantics.AllUnknown();
            }
        }

        /// <summary>
        /// The fifth verified-fixture seam, substituting only the
        /// cutout-family opaque-conversion step for one admitted material:
        /// effective render state, real <c>LilToonOpaqueConversion</c>
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
            Material preparedOpaque,
            out Material opaque,
            out LilToonOpaqueConversionRefusal refusal)
        {
            LilToonOpaqueConversion.ReadEffectiveRenderState(
                live, out var queue, out var renderType);
            var eligibility = LilToonOpaqueConversion
                .EvaluateVerifiedEligibility(derived, queue, renderType);
            if (eligibility.Outcome !=
                LilToonOpaqueConversionOutcome.Convertible)
            {
                opaque = null;
                refusal = eligibility.Refusal;
                return false;
            }

            // An already-prepared artifact for this source is reused here;
            // only a first conversion creates the canonical clone. The
            // tuple-carrying opaque stand-in is the attested target.
            opaque = preparedOpaque ??
                LilToonOpaqueConversion.PrepareCanonicalOpaqueClone(
                    live, Shader.Find(
                        LilToonFixtureShaderNames.OpaqueTarget));
            refusal = LilToonOpaqueConversionRefusal.None;
            return true;
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

        private sealed class LilToonFixtureShaderNames : LilToonFixtureTestBase
        {

            /// <summary>The legacy verified-opaque semantic stand-in.</summary>
            internal const string Opaque = FixtureShaderName;

            /// <summary>The schema-complete cutout source stand-in.</summary>
            internal const string Cutout = CutoutConversionShaderName;

            /// <summary>The distinct canonical opaque target stand-in.</summary>
            internal const string OpaqueTarget = OpaqueConversionShaderName;
        }

        private sealed class PoiyomiFixtureShaderNames : PoiyomiFixtureTestBase
        {
            internal const string Fixture = FixtureShaderName;
        }
    }
}
