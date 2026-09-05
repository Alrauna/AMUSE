using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    internal enum CapturedAlphaMaterialFamily
    {
        Unsupported,
        Poiyomi,
        LilToon,
        LilToonCutout,
        LilToonTransparent,
    }

    internal sealed class CapturedAlphaMaterial
    {
        internal CapturedAlphaMaterialFamily Family { get; }
        internal CapturedMaterialEvidence Evidence { get; }
        internal PoiyomiSourceEvidence PoiyomiEvidence { get; }
        internal LilToonSourceEvidence LilToonEvidence { get; }

        internal CapturedAlphaMaterial(
            CapturedAlphaMaterialFamily family,
            CapturedMaterialEvidence evidence,
            PoiyomiSourceEvidence poiyomiEvidence,
            LilToonSourceEvidence lilToonEvidence)
        {
            if (!Enum.IsDefined(typeof(CapturedAlphaMaterialFamily), family))
            {
                throw new ArgumentOutOfRangeException(nameof(family));
            }

            Family = family;
            Evidence = evidence
                ?? throw new ArgumentNullException(nameof(evidence));
            PoiyomiEvidence = poiyomiEvidence;
            LilToonEvidence = lilToonEvidence;
        }
    }

    /// <summary>
    /// Selects the shader frontend for one base material. Each frontend attests
    /// its own source identity, and no material can be attested by both, so
    /// selection is an exclusive trial rather than a dispatch table: a second
    /// place deciding "is this a Poiyomi material" could only disagree with the
    /// first. This is deliberately not an adapter interface, a registry, or a
    /// provider framework. The transparent normal family that joined
    /// cutout is not a third frontend: it is a second identity inside
    /// the existing lilToon frontend, and the fourth exact-name branch
    /// is still one map with two consumers — nothing dispatches
    /// polymorphically over the frontends
    /// (docs/architecture/shader-frontend-comparison.md, last row of
    /// the promotion table).
    /// </summary>
    internal static class UnityMaterialSemantics
    {
        private static readonly MaterialEvidenceRequest EmptyEvidenceRequest =
            new MaterialEvidenceRequest(
                shaderName: false,
                activeColorSpace: false,
                presenceProperties: Array.Empty<string>(),
                scalarProperties: Array.Empty<string>(),
                colorProperties: Array.Empty<string>(),
                vectorProperties: Array.Empty<string>(),
                textureProperties:
                    Array.Empty<TexturePropertyEvidenceRequest>());

        /// <summary>
        /// Analyzes the current values of one supplied base material. It makes
        /// no claim about later animation, material swaps, property blocks, or
        /// modifier processing. A material no frontend attests is all-Unknown,
        /// which is the conservative answer and never a refusal to answer.
        /// </summary>
        internal static MaterialSemantics AnalyzeBaseMaterial(Material material)
        {
            // The frontends throw for these; the correct answer is Unknown, and
            // an unassigned or destroyed slot is an ordinary input. Unity's
            // overloaded equality reports a destroyed object as null.
            if (material == null || material.shader == null)
            {
                return AllUnknown();
            }

            var poiyomi = PoiyomiMaterialSemantics.AnalyzeBaseMaterial(material);
            if (poiyomi.IsSupportedMaterial)
            {
                return poiyomi.Semantics;
            }

            // An unsupported lilToon result is itself all-Unknown, which is
            // exactly the answer for a material neither frontend attests.
            return LilToonMaterialSemantics
                .AnalyzeBaseMaterial(material)
                .Semantics;
        }

        internal static IReadOnlyList<CapturedAlphaMaterial> CaptureAlphaMaterials(
            IReadOnlyList<Material> materials)
        {
            if (materials == null)
            {
                throw new ArgumentNullException(nameof(materials));
            }

            var families = new CapturedAlphaMaterialFamily[materials.Count];
            var shaders = new Shader[materials.Count];
            var inputs = new MaterialEvidenceCaptureInput[materials.Count];
            for (var index = 0; index < materials.Count; index++)
            {
                var material = materials[index];
                var request = EmptyEvidenceRequest;
                if (material != null && material.shader != null)
                {
                    shaders[index] = material.shader;
                    var classified = ClassifyShaderName(
                        material.shader.name);
                    families[index] = classified.family;
                    request = classified.alpha ?? EmptyEvidenceRequest;
                }

                inputs[index] = new MaterialEvidenceCaptureInput(
                    material, request);
            }

            var evidence = UnityMaterialEvidenceCapture.Capture(inputs);
            return BuildCapturedAlphaMaterials(
                materials, families, shaders, evidence);
        }

        /// <summary>
        /// Selects the supported family for one material and hands back the two
        /// existing requests that family answers with: the alpha evidence
        /// ordinary proof may consider, and the schema the closed capture must
        /// gather. This is a pure selection pass: it identifies the family from
        /// the exact shader name and does nothing else. It captures no material
        /// evidence, reads no shader source, computes no source hash, and
        /// acquires no texture — so a material carrying a supported shader name
        /// over an unattested source is selected here and refused later.
        /// <para>
        /// <see cref="TryCaptureClosedAlphaMaterials"/> is the sole
        /// material-evidence capture and the sole source-attestation decision
        /// for the admitted batch. Selection exists only to determine the
        /// unions of evidence that one capture must gather and that alpha proof
        /// may then consider.
        /// </para>
        /// </summary>
        internal static bool TrySelectAlphaMaterialRequests(
            Material material,
            out CapturedAlphaMaterialFamily family,
            out MaterialEvidenceRequest alphaRelevanceRequest,
            out MaterialEvidenceRequest captureRequest)
        {
            family = IdentifyFamily(material);
            alphaRelevanceRequest = AlphaRequestForFamily(family);
            captureRequest = CaptureRequestForFamily(family);
            return alphaRelevanceRequest != null;
        }

        internal static bool TryCaptureClosedAlphaMaterials(
            IReadOnlyList<Material> materials,
            IReadOnlyList<CapturedAlphaMaterialFamily> families,
            MaterialEvidenceRequest request,
            out IReadOnlyList<CapturedAlphaMaterial> captured)
        {
            if (materials == null) throw new ArgumentNullException(nameof(materials));
            if (families == null) throw new ArgumentNullException(nameof(families));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (materials.Count != families.Count)
            {
                throw new ArgumentException(
                    "Material and family counts must match.", nameof(families));
            }

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
            var result = BuildCapturedAlphaMaterials(
                materials, families, shaders, evidence);
            foreach (var material in result)
            {
                if (!IsAttestedAlphaMaterial(material))
                {
                    captured = null;
                    return false;
                }
            }

            captured = result;
            return true;
        }

        /// <summary>
        /// The D8 transfer capture: identical to
        /// <see cref="TryCaptureClosedAlphaMaterials"/> except that a batch
        /// member whose shader name is in <paramref name="grantedShaderNames"/>
        /// skips source-identity verification — the user accepted the
        /// unverified-version risk for exactly that name this build. A batch
        /// member outside the granted set still fails the whole batch, so an
        /// unconsented discovery keeps the fail-closed renderer refusal.
        /// </summary>
        internal static bool TryCaptureClosedAlphaMaterialsTransferred(
            IReadOnlyList<Material> materials,
            IReadOnlyList<CapturedAlphaMaterialFamily> families,
            MaterialEvidenceRequest request,
            IReadOnlyCollection<string> grantedShaderNames,
            out IReadOnlyList<CapturedAlphaMaterial> captured)
        {
            if (materials == null) throw new ArgumentNullException(nameof(materials));
            if (families == null) throw new ArgumentNullException(nameof(families));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (materials.Count != families.Count)
            {
                throw new ArgumentException(
                    "Material and family counts must match.", nameof(families));
            }

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
            var result = BuildCapturedAlphaMaterials(
                materials, families, shaders, evidence);
            for (var index = 0; index < result.Count; index++)
            {
                if (IsAttestedAlphaMaterial(result[index]))
                {
                    continue;
                }

                var shaderName = shaders[index] == null ? null : shaders[index].name;
                if (shaderName == null
                    || !System.Linq.Enumerable.Contains(
                        grantedShaderNames, shaderName))
                {
                    captured = null;
                    return false;
                }
            }

            captured = result;
            return true;
        }

        /// <summary>
        /// The transferred resolver: the mirror of
        /// <see cref="AnalyzeAlphaMaterial"/> without identity verification.
        /// Only materials the granted capture admitted reach this path; a
        /// missing family-specific evidence still answers all-Unknown, which
        /// keeps the fail-closed direction inside the transferred mode.
        /// </summary>
        internal static MaterialSemantics AnalyzeAlphaMaterialTransferred(
            CapturedAlphaMaterial captured)
        {
            if (captured == null)
            {
                throw new ArgumentNullException(nameof(captured));
            }

            SemanticOutput<ScalarSemanticValue> alpha;
            switch (captured.Family)
            {
                case CapturedAlphaMaterialFamily.Poiyomi:
                    // PoiyomiSourceEvidence is a struct: the gather always
                    // produced one. The consent covers the identity risk.
                    alpha = PoiyomiMaterialSemantics.InterpretVerifiedAlpha(
                        captured.Evidence);
                    break;
                case CapturedAlphaMaterialFamily.LilToon:
                    if (captured.LilToonEvidence == null)
                    {
                        return AllUnknown();
                    }

                    alpha = LilToonMaterialSemantics.InterpretVerifiedAlpha(
                        captured.Evidence);
                    break;
                case CapturedAlphaMaterialFamily.LilToonCutout:
                    if (captured.LilToonEvidence == null)
                    {
                        return AllUnknown();
                    }

                    alpha = LilToonCutoutMaterialSemantics
                        .InterpretVerifiedCutoutAlpha(captured.Evidence);
                    break;
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    if (captured.LilToonEvidence == null)
                    {
                        return AllUnknown();
                    }

                    alpha = LilToonTransparentMaterialSemantics
                        .InterpretVerifiedTransparentAlpha(captured.Evidence);
                    break;
                default:
                    return AllUnknown();
            }

            return new MaterialSemantics(
                SemanticOutput<ColorSemanticValue>.Unknown(),
                alpha,
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<NormalSemanticValue>.Unknown());
        }

        /// <summary>
        /// The D8 pre-scan: one distinct-shader walk over the avatar's
        /// assigned materials, producing one consent subject per unverified
        /// shader of a supported family, plus the exact shader-name set a
        /// granted build may transfer. Shaders outside every supported
        /// family produce nothing here — they refuse downstream with no
        /// transfer target to offer.
        /// </summary>
        internal static (IReadOnlyList<string> Subjects,
            IReadOnlyCollection<string> GrantedShaderNames)
            CollectTransferConsent(IEnumerable<Material> materials)
        {
            var subjects = new List<string>();
            var names = new HashSet<string>();
            var seen = new HashSet<Shader>();
            foreach (var material in materials)
            {
                if (material == null || material.shader == null
                    || !seen.Add(material.shader))
                {
                    continue;
                }

                var family = IdentifyFamily(material);
                if (family == CapturedAlphaMaterialFamily.Unsupported)
                {
                    continue;
                }

                var capturedList = CaptureAlphaMaterials(new[] { material });
                if (IsAttestedAlphaMaterial(capturedList[0]))
                {
                    continue;
                }

                subjects.Add("Shader '" + material.shader.name + "' is not " +
                    "a verified version. AMUSE would treat it with the " +
                    "verified version's rules, which may be wrong.");
                names.Add(material.shader.name);
            }

            return (subjects, names);
        }

        private static IReadOnlyList<CapturedAlphaMaterial>
            BuildCapturedAlphaMaterials(
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                IReadOnlyList<Shader> shaders,
                IReadOnlyList<CapturedMaterialEvidence> evidence)
        {
            var results = new CapturedAlphaMaterial[materials.Count];
            for (var index = 0; index < results.Length; index++)
            {
                var poiyomi = default(PoiyomiSourceEvidence);
                LilToonSourceEvidence lilToon = null;
                if (families[index] == CapturedAlphaMaterialFamily.Poiyomi)
                {
                    poiyomi = PoiyomiMaterialSemantics.GatherAlphaSourceEvidence(
                        shaders[index], evidence[index]);
                }
                else if (families[index] == CapturedAlphaMaterialFamily.LilToon)
                {
                    lilToon = LilToonSourceAttestation.GatherSourceEvidence(
                        shaders[index], evidence[index]);
                }
                else if (families[index] ==
                    CapturedAlphaMaterialFamily.LilToonCutout)
                {
                    lilToon = LilToonSourceAttestation
                        .GatherCutoutSourceEvidence(
                            shaders[index], evidence[index]);
                }
                else if (families[index] ==
                    CapturedAlphaMaterialFamily.LilToonTransparent)
                {
                    lilToon = LilToonSourceAttestation
                        .GatherTransparentSourceEvidence(
                            shaders[index], evidence[index]);
                }

                results[index] = new CapturedAlphaMaterial(
                    families[index], evidence[index], poiyomi, lilToon);
            }

            return new ReadOnlyCollection<CapturedAlphaMaterial>(results);
        }

        /// <summary>
        /// The exact shader-name map selection and batch capture must agree
        /// on. One map, two consumers: a second place deciding "is this a
        /// supported lilToon material" could only drift away from the
        /// first. Every supported name is exact, the transparent normal
        /// one included; near-miss vendor names (the transparent one-pass,
        /// two-pass, and outline variants among them) stay Unsupported
        /// and are refused downstream.
        /// </summary>
        private static (
            CapturedAlphaMaterialFamily family,
            MaterialEvidenceRequest alpha) ClassifyShaderName(
            string shaderName)
        {
            if (string.Equals(
                    shaderName,
                    PoiyomiMaterialSemantics.PoiyomiToonShaderName,
                    StringComparison.Ordinal))
            {
                return (
                    CapturedAlphaMaterialFamily.Poiyomi,
                    PoiyomiMaterialSemantics.AlphaEvidenceRequest);
            }

            if (string.Equals(
                    shaderName,
                    LilToonSourceAttestation.SupportedShaderName,
                    StringComparison.Ordinal))
            {
                return (
                    CapturedAlphaMaterialFamily.LilToon,
                    LilToonMaterialSemantics.AlphaEvidenceRequest);
            }

            if (string.Equals(
                    shaderName,
                    LilToonSourceAttestation.CutoutShaderName,
                    StringComparison.Ordinal))
            {
                return (
                    CapturedAlphaMaterialFamily.LilToonCutout,
                    LilToonCutoutMaterialSemantics.AlphaEvidenceRequest);
            }

            if (string.Equals(
                    shaderName,
                    LilToonSourceAttestation.TransparentShaderName,
                    StringComparison.Ordinal))
            {
                return (
                    CapturedAlphaMaterialFamily.LilToonTransparent,
                    LilToonTransparentMaterialSemantics.AlphaEvidenceRequest);
            }

            return (CapturedAlphaMaterialFamily.Unsupported, null);
        }

        private static CapturedAlphaMaterialFamily IdentifyFamily(
            Material material)
        {
            if (material == null || material.shader == null)
                return CapturedAlphaMaterialFamily.Unsupported;

            return ClassifyShaderName(material.shader.name).family;
        }

        private static MaterialEvidenceRequest AlphaRequestForFamily(
            CapturedAlphaMaterialFamily family)
        {
            switch (family)
            {
                case CapturedAlphaMaterialFamily.Poiyomi:
                    return PoiyomiMaterialSemantics.AlphaEvidenceRequest;
                case CapturedAlphaMaterialFamily.LilToon:
                    return LilToonMaterialSemantics.AlphaEvidenceRequest;
                case CapturedAlphaMaterialFamily.LilToonCutout:
                    return LilToonCutoutMaterialSemantics
                        .AlphaEvidenceRequest;
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    return LilToonTransparentMaterialSemantics
                        .AlphaEvidenceRequest;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Poiyomi's capture schema is its alpha request plus conversion's
        /// own request, so one capture serves both readers. The cutout and
        /// transparent lilToon frontends widen their alpha requests the
        /// same way: one capture serves both the family's alpha proof and
        /// the lilToon conversion. Opaque lilToon has no
        /// opaque-conversion request, so its schema is its alpha request
        /// and nothing widens it.
        /// </summary>
        private static readonly MaterialEvidenceRequest PoiyomiCaptureRequest =
            MaterialEvidenceRequest.Combine(
                PoiyomiMaterialSemantics.AlphaEvidenceRequest,
                PoiyomiOpaqueConversion.ConversionEvidenceRequest);

        private static readonly MaterialEvidenceRequest LilToonCaptureRequest =
            MaterialEvidenceRequest.Combine(
                LilToonCutoutMaterialSemantics.AlphaEvidenceRequest,
                LilToonCutoutSourceEligibility.ConversionEvidenceRequest);

        private static readonly MaterialEvidenceRequest
            LilToonTransparentCaptureRequest =
                MaterialEvidenceRequest.Combine(
                    LilToonTransparentMaterialSemantics.AlphaEvidenceRequest,
                    LilToonTransparentSourceEligibility
                        .ConversionEvidenceRequest);

        private static MaterialEvidenceRequest CaptureRequestForFamily(
            CapturedAlphaMaterialFamily family)
        {
            switch (family)
            {
                case CapturedAlphaMaterialFamily.Poiyomi:
                    return PoiyomiCaptureRequest;
                case CapturedAlphaMaterialFamily.LilToon:
                    return LilToonMaterialSemantics.AlphaEvidenceRequest;
                case CapturedAlphaMaterialFamily.LilToonCutout:
                    return LilToonCaptureRequest;
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    return LilToonTransparentCaptureRequest;
                default:
                    return null;
            }
        }

        private static bool IsAttestedAlphaMaterial(
            CapturedAlphaMaterial material)
        {
            switch (material.Family)
            {
                case CapturedAlphaMaterialFamily.Poiyomi:
                    return PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(
                        material.PoiyomiEvidence, out _);
                case CapturedAlphaMaterialFamily.LilToon:
                    return material.LilToonEvidence != null &&
                        LilToonSourceAttestation.TryVerifyLilToonIdentity(
                            material.LilToonEvidence, out _);
                case CapturedAlphaMaterialFamily.LilToonCutout:
                    return material.LilToonEvidence != null &&
                        LilToonSourceAttestation
                            .TryVerifyLilToonCutoutIdentity(
                                material.LilToonEvidence, out _);
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    return material.LilToonEvidence != null &&
                        LilToonSourceAttestation
                            .TryVerifyLilToonTransparentIdentity(
                                material.LilToonEvidence, out _);
                default:
                    return false;
            }
        }

        internal static MaterialSemantics AnalyzeAlphaMaterial(
            CapturedAlphaMaterial captured)
        {
            if (captured == null)
            {
                throw new ArgumentNullException(nameof(captured));
            }

            SemanticOutput<ScalarSemanticValue> alpha;
            switch (captured.Family)
            {
                case CapturedAlphaMaterialFamily.Poiyomi:
                    if (!PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(
                            captured.PoiyomiEvidence, out _))
                    {
                        return AllUnknown();
                    }

                    alpha = PoiyomiMaterialSemantics.InterpretVerifiedAlpha(
                        captured.Evidence);
                    break;
                case CapturedAlphaMaterialFamily.LilToon:
                    if (captured.LilToonEvidence == null ||
                        !LilToonSourceAttestation.TryVerifyLilToonIdentity(
                            captured.LilToonEvidence, out _))
                    {
                        return AllUnknown();
                    }

                    alpha = LilToonMaterialSemantics.InterpretVerifiedAlpha(
                        captured.Evidence);
                    break;
                case CapturedAlphaMaterialFamily.LilToonCutout:
                    if (captured.LilToonEvidence == null ||
                        !LilToonSourceAttestation
                            .TryVerifyLilToonCutoutIdentity(
                                captured.LilToonEvidence, out _))
                    {
                        return AllUnknown();
                    }

                    alpha = LilToonCutoutMaterialSemantics
                        .InterpretVerifiedCutoutAlpha(captured.Evidence);
                    break;
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    if (captured.LilToonEvidence == null ||
                        !LilToonSourceAttestation
                            .TryVerifyLilToonTransparentIdentity(
                                captured.LilToonEvidence, out _))
                    {
                        return AllUnknown();
                    }

                    alpha = LilToonTransparentMaterialSemantics
                        .InterpretVerifiedTransparentAlpha(captured.Evidence);
                    break;
                default:
                    return AllUnknown();
            }

            return new MaterialSemantics(
                SemanticOutput<ColorSemanticValue>.Unknown(),
                alpha,
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<NormalSemanticValue>.Unknown());
        }

        internal static MaterialSemantics AllUnknown()
        {
            return new MaterialSemantics(
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<ScalarSemanticValue>.Unknown(),
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<NormalSemanticValue>.Unknown());
        }
    }
}
