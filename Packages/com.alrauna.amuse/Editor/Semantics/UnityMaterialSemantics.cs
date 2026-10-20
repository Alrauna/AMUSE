using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using UnityEditor;
using UnityEngine;
using static Alrauna.Amuse.Editor.Semantics.EvidenceGates;

namespace Alrauna.Amuse.Editor.Semantics
{
    internal enum CapturedAlphaMaterialFamily
    {
        Unsupported,
        Poiyomi,

        /// <summary>
        /// The Two Pass generated shader of the pinned Poiyomi frontend. It
        /// routes through the Poiyomi interpreter with its own request, whose
        /// second-family scalars the plain request never names. Opaque
        /// conversion refuses for this family through the existing
        /// unsupported-family default, because the plain conversion recipe
        /// was derived from the plain shader's own preset metadata.
        /// </summary>
        PoiyomiTwoPass,
        LilToon,
        LilToonCutout,
        LilToonTransparent,

        /// <summary>
        /// A supported Multi container between classification and
        /// resolution. The one post-capture resolution point maps an
        /// admitted material onto the resolved regular family, so the
        /// downstream switches never dispatch on this member. It survives
        /// only on a refusal, whose material answers all-Unknown alpha
        /// through the named Multi cause kind.
        /// </summary>
        LilToonMulti,
    }

    internal sealed class CapturedAlphaMaterial
    {
        internal CapturedAlphaMaterialFamily Family { get; }
        internal CapturedMaterialEvidence Evidence { get; }
        internal PoiyomiSourceEvidence PoiyomiEvidence { get; }
        internal LilToonSourceEvidence LilToonEvidence { get; }

        /// <summary>
        /// The named renderer refusal the capture recorded for this
        /// material's recognized locked identity, or None. Recognition
        /// happens once, at capture time, where the live material's
        /// serialization is still readable. When set, a slot that would
        /// refuse this material as semantics-unknown refuses with this
        /// name instead. Every other refusal path ignores it.
        /// </summary>
        internal RendererAnalysisRefusal LockedIdentityRefusal { get; }

        /// <summary>
        /// The typed record of one Multi material's resolution at the one
        /// post-capture resolution point, or null for every material that
        /// did not classify to a Multi container. A record whose source
        /// evidence is set resolved, and the stored family is the resolved
        /// regular family. A record without source evidence carries the
        /// closed refusal value, and the stored family stays
        /// <see cref="CapturedAlphaMaterialFamily.LilToonMulti"/>.
        /// </summary>
        internal LilToonMultiResolutionRecord MultiResolution { get; }

        /// <summary>
        /// The material's project asset path, name, and live shader name,
        /// read once at capture time where the live material is still in
        /// hand. Immutable strings, never live references: the evidence
        /// discipline holds. All three are null for a material built by a
        /// path that had no live material, and the path is empty for an
        /// in-memory material, so the report falls back name-first.
        /// </summary>
        internal string MaterialPath { get; }
        internal string MaterialName { get; }
        internal string ShaderName { get; }

        /// <summary>
        /// Whether the material's own predicate request declares a cutoff
        /// property. The stored family alone cannot answer this: a Poiyomi
        /// cutout preset and a plain preset share one family, and a Multi
        /// container's union request carries every resolved mode's
        /// declaration. Later coverage selection reads this recorded fact
        /// instead of re-deriving the predicate at the far end of capture.
        /// </summary>
        public bool PredicatesDeclareCutoff { get; }

        internal CapturedAlphaMaterial(
            CapturedAlphaMaterialFamily family,
            CapturedMaterialEvidence evidence,
            PoiyomiSourceEvidence poiyomiEvidence,
            LilToonSourceEvidence lilToonEvidence,
            RendererAnalysisRefusal lockedIdentityRefusal =
                RendererAnalysisRefusal.None,
            string materialPath = null,
            string materialName = null,
            string shaderName = null,
            LilToonMultiResolutionRecord multiResolution = null,
            bool predicatesDeclareCutoff = false)
        {
            if (!Enum.IsDefined(typeof(CapturedAlphaMaterialFamily), family))
            {
                throw new ArgumentOutOfRangeException(nameof(family));
            }

            if (!Enum.IsDefined(
                    typeof(RendererAnalysisRefusal),
                    lockedIdentityRefusal))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lockedIdentityRefusal));
            }

            // The Multi record and the family agree: the Multi family means
            // a stored refusal, and a stored resolution means the resolved
            // regular family.
            if (family == CapturedAlphaMaterialFamily.LilToonMulti)
            {
                if (multiResolution == null || multiResolution.IsResolved)
                {
                    throw new ArgumentException(
                        "A Multi family capture carries its refusal record.",
                        nameof(multiResolution));
                }
            }
            else if (multiResolution != null)
            {
                if (!multiResolution.IsResolved)
                {
                    throw new ArgumentException(
                        "A Multi refusal rides only a Multi family capture.",
                        nameof(multiResolution));
                }

                if (family != CapturedAlphaMaterialFamily.LilToon &&
                    family != CapturedAlphaMaterialFamily.LilToonCutout &&
                    family != CapturedAlphaMaterialFamily.LilToonTransparent)
                {
                    throw new ArgumentException(
                        "A resolved Multi capture stores a regular family.",
                        nameof(family));
                }
            }

            Family = family;
            Evidence = evidence
                ?? throw new ArgumentNullException(nameof(evidence));
            PoiyomiEvidence = poiyomiEvidence;
            LilToonEvidence = lilToonEvidence;
            LockedIdentityRefusal = lockedIdentityRefusal;
            MultiResolution = multiResolution;
            MaterialPath = materialPath;
            MaterialName = materialName;
            ShaderName = shaderName;
            PredicatesDeclareCutoff = predicatesDeclareCutoff;
        }
    }

    /// <summary>
    /// Maps a live material to the source object a producer registered
    /// as its replacement, or null when nothing registered one. The
    /// capture stores the resolved object's path and name as plain
    /// strings, so evidence records never hold live Unity objects.
    /// </summary>
    internal delegate UnityEngine.Object RegisteredSourceLookup(
        UnityEngine.Object source);

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

        internal static IReadOnlyList<CapturedAlphaMaterial> CaptureAlphaMaterials(
            IReadOnlyList<Material> materials,
            RegisteredSourceLookup resolveRegisteredSource = null)
        {
            if (materials == null)
            {
                throw new ArgumentNullException(nameof(materials));
            }

            var families = new CapturedAlphaMaterialFamily[materials.Count];
            var requests = new MaterialEvidenceRequest[materials.Count];
            for (var index = 0; index < materials.Count; index++)
            {
                var material = materials[index];
                var request = EmptyEvidenceRequest;
                if (material != null && material.shader != null)
                {
                    var classified = ClassifyShaderName(
                        material.shader.name);
                    families[index] = classified.family;
                    request = classified.alpha ?? EmptyEvidenceRequest;
                }

                requests[index] = request;
            }

            return CaptureBatch(
                materials, families, requests, null, resolveRegisteredSource,
                int.MaxValue, int.MaxValue);
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
            // The one shader-name map answers selection directly: the
            // family and its alpha relevance come from the same branch, so
            // a Multi-classified name cannot drift between the two
            // questions. The pairs are the ones IdentifyFamily and
            // AlphaRequestForFamily have always agreed on.
            if (material == null || material.shader == null)
            {
                family = CapturedAlphaMaterialFamily.Unsupported;
                alphaRelevanceRequest = null;
                captureRequest = null;
                return false;
            }

            var classified = ClassifyShaderName(material.shader.name);
            family = classified.family;
            alphaRelevanceRequest = classified.alpha;
            captureRequest = CaptureRequestForFamily(family);
            return alphaRelevanceRequest != null;
        }

        /// <summary>
        /// The closed capture with registered-source naming. When a producer
        /// registered a material's replacement, the capture records the
        /// registered source's path and name; <paramref name="resolveRegisteredSource"/>
        /// answers null when nothing registered the material, and the
        /// material keeps its own identity. There is deliberately no
        /// overload without the lookup: a capture that could silently skip
        /// registered-source naming would put build-copy identity into the
        /// reports. The outcome partitions the batch: survivors ride
        /// <see cref="ClosedAlphaCaptureOutcome.Captured"/> in batch order,
        /// and every member whose source failed attestation is named by
        /// batch position in <see cref="ClosedAlphaCaptureOutcome.UnattestedOrdinals"/>.
        /// The bool stays false only for a failure that names no material.
        /// </summary>
        internal static bool TryCaptureClosedAlphaMaterials(
            IReadOnlyList<Material> materials,
            IReadOnlyList<CapturedAlphaMaterialFamily> families,
            MaterialEvidenceRequest request,
            AlphaPolicyBounds bounds,
            out ClosedAlphaCaptureOutcome outcome,
            RegisteredSourceLookup resolveRegisteredSource,
            int cutoutRawMipCap = int.MaxValue,
            int exactRawMipCap = int.MaxValue)
        {
            return TryCaptureClosedAlphaMaterialsTransferred(
                materials,
                families,
                request,
                bounds,
                System.Array.Empty<string>(),
                out outcome,
                resolveRegisteredSource,
                cutoutRawMipCap,
                exactRawMipCap);
        }

        /// <summary>
        /// The D8 transfer capture: identical to
        /// <see cref="TryCaptureClosedAlphaMaterials"/> except that a batch
        /// member whose shader name is in <paramref name="grantedShaderNames"/>
        /// skips source-identity verification — the user accepted the
        /// unverified-version risk for exactly that name this build. An
        /// ungranted unattested member no longer fails the batch: it is named
        /// by batch position in the outcome's failing ordinals, and only its
        /// own slots refuse.
        /// <para>
        /// <paramref name="admitApprovedVersions"/> is the D3 version-keyed
        /// arm: when true, a member whose captured lilToon package version
        /// is approved is admitted the same way a granted name is, lilToon
        /// families only — a refused in-range Multi member reaches the
        /// refused-Multi transport and reports its slot-scope
        /// UnsupportedMultiState rows. Only the build's effective capturer
        /// passes true. The plain wrapper, the dry run's inner capturer,
        /// and the no-consent fallback keep strict attestation.
        /// </para>
        /// <paramref name="resolveRegisteredSource"/> carries registered-source
        /// naming the same way the plain overload does. The two arm caps pin
        /// each shared capture bucket to its proof arm's readback: the
        /// default pair keeps the uncapped request merge, so attestation
        /// callers that carry family-schema caps never see the build's arm
        /// values.
        /// </summary>
        internal static bool TryCaptureClosedAlphaMaterialsTransferred(
            IReadOnlyList<Material> materials,
            IReadOnlyList<CapturedAlphaMaterialFamily> families,
            MaterialEvidenceRequest request,
            AlphaPolicyBounds bounds,
            IReadOnlyCollection<string> grantedShaderNames,
            out ClosedAlphaCaptureOutcome outcome,
            RegisteredSourceLookup resolveRegisteredSource,
            int cutoutRawMipCap = int.MaxValue,
            int exactRawMipCap = int.MaxValue,
            bool admitApprovedVersions = false)
        {
            if (materials == null) throw new ArgumentNullException(nameof(materials));
            if (families == null) throw new ArgumentNullException(nameof(families));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (materials.Count != families.Count)
            {
                throw new ArgumentException(
                    "Material and family counts must match.", nameof(families));
            }

            var requests = new MaterialEvidenceRequest[materials.Count];
            for (var index = 0; index < materials.Count; index++)
            {
                requests[index] = request;
            }

            var result = CaptureBatch(
                materials, families, requests, bounds,
                resolveRegisteredSource,
                cutoutRawMipCap, exactRawMipCap);
            var captured = new List<CapturedAlphaMaterial>();
            var failed = new List<int>();
            for (var index = 0; index < result.Count; index++)
            {
                if (IsAttestedAlphaMaterial(result[index], true))
                {
                    captured.Add(result[index]);
                    continue;
                }

                var shader = materials[index] == null
                    ? null
                    : materials[index].shader;
                var shaderName = shader == null ? null : shader.name;
                if (shaderName != null
                    && System.Linq.Enumerable.Contains(
                        grantedShaderNames, shaderName))
                {
                    captured.Add(result[index]);
                    continue;
                }

                // D3 capture admission: an approved package version rides
                // the version-keyed arm the way a granted name rides the
                // name-keyed one. The read is family-confined by
                // construction — only the lilToon families carry the
                // lilToon evidence or a Multi resolution.
                if (admitApprovedVersions &&
                    CapturedVersionIsApproved(result[index]))
                {
                    captured.Add(result[index]);
                    continue;
                }

                failed.Add(index);
            }
            outcome = new ClosedAlphaCaptureOutcome(captured, failed);
            return true;
        }

        /// <summary>
        /// The version-keyed approval read over one captured record (D3).
        /// A refused Multi member routes on the captured package facts its
        /// resolution stored. Every other family routes on its own lilToon
        /// evidence.
        /// </summary>
        private static bool CapturedVersionIsApproved(
            CapturedAlphaMaterial captured)
        {
            return captured.Family == CapturedAlphaMaterialFamily.LilToonMulti
                ? captured.MultiResolution != null &&
                  captured.MultiResolution.CapturedHasPackage &&
                  LilToonSourceAttestation.IsApprovedPackageVersion(
                      captured.MultiResolution.CapturedPackageVersion)
                : captured.LilToonEvidence != null &&
                  LilToonSourceAttestation.IsApprovedPackageEvidence(
                      captured.LilToonEvidence.HasPackage,
                      captured.LilToonEvidence.PackageVersion);
        }

        /// <summary>
        /// The shared capture middle of the three capture entries: one
        /// shader array, one input array built with
        /// <see cref="AlphaPredicateRequestFor"/>, one evidence capture,
        /// and one captured-batch build. A null bounds selects the
        /// no-bounds capture overload, which keeps the inert bounds. The
        /// entries keep their own guards, their own request and family
        /// construction, and their own attestation loops.
        /// </summary>
        private static IReadOnlyList<CapturedAlphaMaterial> CaptureBatch(
            IReadOnlyList<Material> materials,
            IReadOnlyList<CapturedAlphaMaterialFamily> families,
            IReadOnlyList<MaterialEvidenceRequest> requests,
            AlphaPolicyBounds? bounds,
            RegisteredSourceLookup resolveRegisteredSource,
            int cutoutRawMipCap,
            int exactRawMipCap)
        {
            var shaders = new Shader[materials.Count];
            var inputs = new MaterialEvidenceCaptureInput[materials.Count];
            var declaresCutoff = new bool[materials.Count];
            for (var index = 0; index < materials.Count; index++)
            {
                shaders[index] = materials[index] == null
                    ? null
                    : materials[index].shader;
                var predicate = AlphaPredicateRequestFor(
                    materials[index], families[index]);
                inputs[index] = new MaterialEvidenceCaptureInput(
                    materials[index],
                    requests[index],
                    predicate);
                // The membership fact is evaluated once per material on the
                // material's own predicate request. The gate mirrors
                // DeclaresCutoffFor at the request level, so one sibling
                // family's declaration never reads as another family's
                // membership.
                declaresCutoff[index] = PredicateDeclaresCutoff(predicate);
            }

            // The null-bounds route serves conversion eligibility and
            // opaque-target reads, so it keeps the smallest-cap merge and
            // never sees the arm caps. The closed batch hands the arm caps
            // to the capture, which pins each shared bucket to its arm.
            var evidence = bounds == null
                ? UnityMaterialEvidenceCapture.Capture(inputs)
                : UnityMaterialEvidenceCapture.Capture(
                    inputs, bounds.Value, cutoutRawMipCap, exactRawMipCap);
            return BuildCapturedAlphaMaterials(
                materials, families, requests, shaders, evidence,
                declaresCutoff, resolveRegisteredSource);
        }

        /// <summary>
        /// The transferred resolver: the mirror of
        /// <see cref="AnalyzeAlphaMaterial"/> without identity verification.
        /// Only materials the granted capture admitted reach this path; a
        /// missing family-specific evidence still answers all-Unknown, which
        /// keeps the fail-closed direction inside the transferred mode. The
        /// verification gate is skipped by the parameter, not by a second
        /// switch. All five families honor the parameter, so no family arm
        /// keeps an unconditional verify. A Poiyomi material reaches this
        /// path only attested, because the capture never grants Poiyomi
        /// names.
        /// </summary>
        internal static CapturedAlphaSemantics AnalyzeAlphaMaterialTransferred(
            CapturedAlphaMaterial captured)
        {
            return AnalyzeAlphaMaterialCore(captured, false);
        }

        /// <summary>
        /// The all-Unknown answer for a shader-level refusal, carrying the
        /// shader name the capture read while the live material was still
        /// in hand. The name is report wording only; the answer stays
        /// all-Unknown either way.
        /// </summary>
        private static CapturedAlphaSemantics UnknownWithShaderReason(
            AlphaUnknownKind kind, string shaderName)
        {
            return new CapturedAlphaSemantics(
                AllUnknown(),
                kind == AlphaUnknownKind.UnattestedShader
                    ? AlphaUnknownReason.UnattestedShader(shaderName)
                    : AlphaUnknownReason.UnsupportedShader(shaderName));
        }

        /// <summary>
        /// The D8 pre-scan: one distinct-shader walk over the avatar's
        /// assigned materials, producing one consent subject per unverified
        /// shader of a supported family, plus the exact shader-name set a
        /// granted build may transfer. Shaders outside every supported
        /// family produce nothing here — they refuse downstream with no
        /// transfer target to offer. The third tuple member reports
        /// whether any captured member carried an approved lilToon
        /// package version (D3), which routes the build's version-keyed
        /// arm independently of the granted names.
        /// </summary>
        internal static (IReadOnlyList<string> Subjects,
            IReadOnlyCollection<string> GrantedShaderNames,
            bool AnyApprovedVersions)
            CollectTransferConsent(IEnumerable<Material> materials)
        {
            var walked = new List<Material>();
            var captured = new List<CapturedAlphaMaterial>();
            var seen = new HashSet<Shader>();
            foreach (var material in materials)
            {
                if (material == null || material.shader == null
                    || !seen.Add(material.shader))
                {
                    continue;
                }

                var family = IdentifyFamily(material);
                if (family == CapturedAlphaMaterialFamily.Unsupported ||
                    family == CapturedAlphaMaterialFamily.Poiyomi ||
                    family == CapturedAlphaMaterialFamily.PoiyomiTwoPass)
                {
                    continue;
                }

                walked.Add(material);
                captured.AddRange(CaptureAlphaMaterials(
                    new[] { material }));
            }

            return CollectTransferConsentCore(walked, captured);
        }

        /// <summary>
        /// The version-shaped consent core behind the pre-scan (D3): the
        /// materials and their index-aligned captured records arrive
        /// shaped by the caller, so the version path the production
        /// capture's package read feeds can also run over captured
        /// records a test shapes by hand. An identity-failed member whose
        /// captured lilToon package version is approved produces no
        /// subject and no granted name: approval removes only the ask.
        /// State-refused Multi members keep their subjects with the
        /// refusal words, and the subject paths stay the only
        /// <c>names.Add</c> sites, so approval never populates
        /// <c>GrantedShaderNames</c>.
        /// </summary>
        internal static (IReadOnlyList<string> Subjects,
            IReadOnlyCollection<string> GrantedShaderNames,
            bool AnyApprovedVersions)
            CollectTransferConsentCore(
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterial> captured)
        {
            var subjects = new List<string>();
            var names = new HashSet<string>();
            var anyApproved = false;
            for (var index = 0; index < materials.Count; index++)
            {
                var material = materials[index];
                if (material == null || material.shader == null)
                {
                    continue;
                }

                if (IsAttestedAlphaMaterial(captured[index], true))
                {
                    continue;
                }

                // D3 suppression: an approved package version removes only
                // the consent ask, and only for an identity failure. The
                // read is family-confined by construction — only the
                // lilToon families carry the lilToon evidence or a Multi
                // resolution.
                var approved = CapturedVersionIsApproved(captured[index]);
                var identityRefused = captured[index].MultiResolution == null ||
                    captured[index].MultiResolution.Refusal ==
                        LilToonMultiResolutionRefusal.AttestationFailed;
                if (approved && identityRefused)
                {
                    anyApproved = true;
                    continue;
                }

                var subject = "Shader '" + material.shader.name +
                    "' is not a verified version. AMUSE would treat it " +
                    "with the verified version's rules, which may be " +
                    "wrong.";
                var multiResolution = captured[index].MultiResolution;
                if (multiResolution != null && !multiResolution.IsResolved)
                {
                    // A refused Multi material is often verified at the
                    // source and stopped by its state instead: mode,
                    // keywords, the clipping canceller, or the overlay
                    // pass. The generic wording would misname the gate
                    // that refused it, so name the refusal in words.
                    subject += " AMUSE refused this material because " +
                        LilToonMultiResolution.RefusalFeatureWords(
                            multiResolution.Refusal) + ".";
                }
                subjects.Add(subject);
                names.Add(material.shader.name);
            }

            return (subjects, names, anyApproved);
        }

        private static IReadOnlyList<CapturedAlphaMaterial>
            BuildCapturedAlphaMaterials(
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                IReadOnlyList<MaterialEvidenceRequest> requests,
                IReadOnlyList<Shader> shaders,
                IReadOnlyList<CapturedMaterialEvidence> evidence,
                bool[] declaresCutoff,
                RegisteredSourceLookup resolveRegisteredSource = null)
        {
            var results = new CapturedAlphaMaterial[materials.Count];
            for (var index = 0; index < results.Length; index++)
            {
                var poiyomi = default(PoiyomiSourceEvidence);
                LilToonSourceEvidence lilToon = null;
                LilToonMultiResolutionRecord multiResolution = null;
                var family = families[index];
                if (family == CapturedAlphaMaterialFamily.LilToonMulti)
                {
                    // The one post-capture resolution point. The resolver
                    // runs once per Multi material here: an admitted
                    // material stores the resolved regular family and the
                    // verified gather, and every downstream switch sees
                    // only the regular families. A refused material keeps
                    // the Multi family and carries the named refusal.
                    multiResolution = ResolveMultiMaterial(
                        materials[index],
                        shaders[index],
                        evidence[index],
                        requests[index],
                        out family);
                    if (multiResolution.IsResolved)
                    {
                        lilToon = multiResolution.SourceEvidence;
                        // The container's predicate request is the union
                        // schema, which declares every resolved mode's
                        // cutoff. The resolved family's own predicate
                        // decides the membership, so the resolved record
                        // re-derives it here at the one resolution point.
                        declaresCutoff[index] = PredicateDeclaresCutoff(
                            AlphaPredicateRequestFor(
                                materials[index], family));
                    }
                }
                else if (family == CapturedAlphaMaterialFamily.Poiyomi ||
                    family == CapturedAlphaMaterialFamily.PoiyomiTwoPass)
                {
                    // Both admitted Poiyomi identities verify through one
                    // conjunction, so the gather is the same function.
                    poiyomi = PoiyomiSourceAttestation.GatherAlphaSourceEvidence(
                        shaders[index], evidence[index]);
                }
                else if (family == CapturedAlphaMaterialFamily.LilToon)
                {
                    lilToon = LilToonSourceAttestation.GatherSourceEvidence(
                        shaders[index], evidence[index]);
                }
                else if (family == CapturedAlphaMaterialFamily.LilToonCutout)
                {
                    lilToon = LilToonSourceAttestation
                        .GatherCutoutSourceEvidence(
                            shaders[index], evidence[index]);
                }
                else if (family ==
                    CapturedAlphaMaterialFamily.LilToonTransparent)
                {
                    lilToon = LilToonSourceAttestation
                        .GatherTransparentSourceEvidence(
                            shaders[index], evidence[index]);
                }

                var source = materials[index];
                var namedSource = namedSourceFor(
                    resolveRegisteredSource, source);
                results[index] = new CapturedAlphaMaterial(
                    family, evidence[index], poiyomi, lilToon,
                    multiResolution: multiResolution,
                    predicatesDeclareCutoff: declaresCutoff[index],
                    materialPath: namedSource != null
                        ? AssetDatabase.GetAssetPath(namedSource)
                        : null,
                    materialName: namedSource != null
                        ? namedSource.name
                        : null,
                    shaderName: source != null && source.shader != null
                        ? source.shader.name
                        : null);
            }

            return new ReadOnlyCollection<CapturedAlphaMaterial>(results);
        }

        /// <summary>
        /// The hub half of the one resolution point: hands the material to
        /// the resolver once. The effective render state is not a
        /// resolution fact — the eligibility layers read it at their own
        /// boundary — so the hub does not read it here. The
        /// keyword-evidence fact comes from the capture request that shaped
        /// the evidence, because the evidence alone cannot carry the
        /// requested-and-not-requested fact apart.
        /// </summary>
        private static LilToonMultiResolutionRecord ResolveMultiMaterial(
            Material material,
            Shader shader,
            CapturedMaterialEvidence evidence,
            MaterialEvidenceRequest request,
            out CapturedAlphaMaterialFamily family)
        {
            if (material == null || shader == null || request == null)
            {
                family = CapturedAlphaMaterialFamily.LilToonMulti;
                return LilToonMultiResolutionRecord.Refused(
                    LilToonMultiResolutionRefusal.AttestationFailed);
            }

            var record = LilToonMultiResolution.ResolveCapturedMaterial(
                shader,
                evidence,
                request.CaptureKeywords,
                out family,
                out _);
            if (!record.IsResolved)
            {
                // A refusal keeps the Multi family, so the stored record
                // and the stored family agree at the constructor guard.
                family = CapturedAlphaMaterialFamily.LilToonMulti;
            }

            return record;
        }

        /// <summary>
        /// The one identity fork both capture constructions share: a
        /// registered lookup that names a replacement wins, and anything
        /// else — no lookup, an unanswered lookup, a null input — keeps the
        /// live material's own identity.
        /// </summary>
        private static UnityEngine.Object namedSourceFor(
            RegisteredSourceLookup lookup,
            UnityEngine.Object source)
        {
            return lookup != null ? (lookup(source) ?? source) : source;
        }

        /// <summary>
        /// The exact shader-name map selection and batch capture must agree
        /// on. One map, two consumers: a second place deciding "is this a
        /// supported lilToon material" could only drift away from the
        /// first. Every supported name is exact. The admitted names are the
        /// plain one, the cutout name, the transparent normal one, the three
        /// S8 outline wrappers, and the one-pass and two-pass transparent
        /// variants with their outline wrappers. Other near-miss vendor
        /// names stay Unsupported and are refused downstream. The known
        /// near misses are one-character mimic names of the admitted
        /// identities, the transparent outline mimics among them. The
        /// Two Pass generated shader is a second admitted identity of the
        /// Poiyomi frontend, and it routes to its own family member with
        /// its own request, whose second-family scalars the plain request
        /// never names.
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
                    PoiyomiMaterialSemantics.PoiyomiTwoPassShaderName,
                    StringComparison.Ordinal))
            {
                return (
                    CapturedAlphaMaterialFamily.PoiyomiTwoPass,
                    PoiyomiMaterialSemantics.TwoPassAlphaEvidenceRequest);
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
                    LilToonSourceAttestation.OutlineShaderName,
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
                    LilToonSourceAttestation.OutlineCutoutShaderName,
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

            if (string.Equals(
                    shaderName,
                    LilToonSourceAttestation.OnePassTransparentShaderName,
                    StringComparison.Ordinal) ||
                string.Equals(
                    shaderName,
                    LilToonSourceAttestation.TwoPassTransparentShaderName,
                    StringComparison.Ordinal) ||
                string.Equals(
                    shaderName,
                    LilToonSourceAttestation
                        .OnePassTransparentOutlineShaderName,
                    StringComparison.Ordinal) ||
                string.Equals(
                    shaderName,
                    LilToonSourceAttestation
                        .TwoPassTransparentOutlineShaderName,
                    StringComparison.Ordinal))
            {
                return (
                    CapturedAlphaMaterialFamily.LilToonTransparent,
                    LilToonTransparentMaterialSemantics.AlphaEvidenceRequest);
            }

            if (string.Equals(
                    shaderName,
                    LilToonSourceAttestation.OutlineTransparentShaderName,
                    StringComparison.Ordinal))
            {
                return (
                    CapturedAlphaMaterialFamily.LilToonTransparent,
                    LilToonTransparentMaterialSemantics.AlphaEvidenceRequest);
            }

            // The two supported Multi containers classify to their own
            // family member with the combined Multi request: the keyword
            // set the gate compares and the three Multi scalars the mode
            // read and the gate rules consume. The member exists only
            // between classification and resolution.
            if (LilToonMultiResolution.IsSupportedContainerShaderName(
                    shaderName))
            {
                return (
                    CapturedAlphaMaterialFamily.LilToonMulti,
                    LilToonMultiResolution.MultiEvidenceRequest);
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

        internal static MaterialEvidenceRequest AlphaRequestForFamily(
            CapturedAlphaMaterialFamily family)
        {
            switch (family)
            {
                case CapturedAlphaMaterialFamily.Poiyomi:
                    return PoiyomiMaterialSemantics.AlphaEvidenceRequest;
                case CapturedAlphaMaterialFamily.PoiyomiTwoPass:
                    return PoiyomiMaterialSemantics
                        .TwoPassAlphaEvidenceRequest;
                case CapturedAlphaMaterialFamily.LilToon:
                    return LilToonMaterialSemantics.AlphaEvidenceRequest;
                case CapturedAlphaMaterialFamily.LilToonCutout:
                    return LilToonCutoutMaterialSemantics
                        .AlphaEvidenceRequest;
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    return LilToonTransparentMaterialSemantics
                        .AlphaEvidenceRequest;
                case CapturedAlphaMaterialFamily.LilToonMulti:
                    return LilToonMultiResolution.MultiEvidenceRequest;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The capture predicate one material's own alpha capture runs
        /// under. For the Poiyomi families the material's preset decides:
        /// a cutout preset selects the declaring request whose capture
        /// binarizes the alpha field by the cutoff value (the split route),
        /// and every other preset selects the plain-clip variant that keeps
        /// the exact-255 field the exact-one rule reads. The predicate is
        /// what keeps one material's cutout declaration from binarizing a
        /// sibling's field inside one closed batch. Every other family
        /// answers with its own alpha request unchanged.
        /// </summary>
        internal static MaterialEvidenceRequest AlphaPredicateRequestFor(
            Material material,
            CapturedAlphaMaterialFamily family)
        {
            switch (family)
            {
                case CapturedAlphaMaterialFamily.Poiyomi:
                    return PoiyomiMaterialSemantics.AlphaPredicateRequestFor(
                        material, false);
                case CapturedAlphaMaterialFamily.PoiyomiTwoPass:
                    return PoiyomiMaterialSemantics.AlphaPredicateRequestFor(
                        material, true);
                default:
                    return AlphaRequestForFamily(family);
            }
        }

        /// <summary>
        /// Whether the predicate request declares a cutoff property. This
        /// mirrors the per-texture gate the capture assignments run on the
        /// input's own predicate, lifted to the whole request. A null
        /// predicate answers false: no family predicate exists, so the
        /// material declares nothing of its own. The capture-side gate
        /// answers true there, because the union request's declaration is
        /// then the caller's own, but that answer is a capture routing
        /// rule, not a membership fact.
        /// </summary>
        internal static bool PredicateDeclaresCutoff(
            MaterialEvidenceRequest predicate)
        {
            if (predicate == null)
            {
                return false;
            }

            foreach (var texture in predicate.TextureProperties)
            {
                if (texture.CutoffScalarProperty != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Poiyomi's capture schema is its alpha request plus conversion's
        /// own request, so one capture serves both readers. The cutout and
        /// transparent lilToon frontends widen their alpha requests the
        /// same way: one capture serves both the family's alpha proof and
        /// the lilToon conversion. Opaque lilToon has no
        /// opaque-conversion request, so its schema is its alpha request
        /// and nothing widens it. The Two Pass schema is its alpha request
        /// alone: conversion never admits that family, so there is no
        /// conversion request to union.
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
                case CapturedAlphaMaterialFamily.PoiyomiTwoPass:
                    return PoiyomiMaterialSemantics
                        .TwoPassAlphaEvidenceRequest;
                case CapturedAlphaMaterialFamily.LilToon:
                    return LilToonMaterialSemantics.AlphaEvidenceRequest;
                case CapturedAlphaMaterialFamily.LilToonCutout:
                    return LilToonCaptureRequest;
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    return LilToonTransparentCaptureRequest;
                case CapturedAlphaMaterialFamily.LilToonMulti:
                    // The closed capture must gather the combined Multi
                    // schema, or the shared selection loop reads a null
                    // request and refuses every supported container before
                    // the one resolution point can run. The combined request
                    // spans the three resolved families' alpha facts beside
                    // the Multi scalars and keywords, so one capture serves
                    // the resolution and every resolved-mode interpreter.
                    return LilToonMultiResolution.MultiEvidenceRequest;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The one identity-verify gate for a captured regular-family
        /// material. The flag keeps one meaning in every arm: true runs
        /// the family verify, and false skips it. A skip still demands
        /// the family's own evidence, so a capture with no evidence
        /// never attests.
        /// </summary>
        private static bool IsAttestedAlphaMaterial(
            CapturedAlphaMaterial material,
            bool verifyIdentity)
        {
            // A Multi material attests through the resolver's row verify at
            // the one resolution point, never through a regular family
            // identity. Its stored lilToon evidence is the Multi gather, so
            // the regular verifies below cannot read it.
            if (material.MultiResolution != null)
            {
                return material.MultiResolution.IsResolved;
            }

            switch (material.Family)
            {
                case CapturedAlphaMaterialFamily.Poiyomi:
                    return !verifyIdentity ||
                        PoiyomiSourceAttestation.TryVerifyPoiyomiIdentity(
                            material.PoiyomiEvidence, out _);
                case CapturedAlphaMaterialFamily.PoiyomiTwoPass:
                    // The conjunction attests each Poiyomi identity against
                    // its own pinned GUID and digest, so the Two Pass
                    // original verifies through the same call.
                    return !verifyIdentity ||
                        PoiyomiSourceAttestation.TryVerifyPoiyomiIdentity(
                            material.PoiyomiEvidence, out _);
                case CapturedAlphaMaterialFamily.LilToon:
                    return material.LilToonEvidence != null &&
                        (!verifyIdentity ||
                            LilToonSourceAttestation
                                .TryVerifyLilToonIdentity(
                                    material.LilToonEvidence, out _));
                case CapturedAlphaMaterialFamily.LilToonCutout:
                    return material.LilToonEvidence != null &&
                        (!verifyIdentity ||
                            LilToonSourceAttestation
                                .TryVerifyLilToonCutoutIdentity(
                                    material.LilToonEvidence, out _));
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    return material.LilToonEvidence != null &&
                        (!verifyIdentity ||
                            LilToonSourceAttestation
                                .TryVerifyLilToonTransparentIdentity(
                                    material.LilToonEvidence, out _));
                default:
                    return false;
            }
        }

        private static CapturedAlphaSemantics AnalyzeAlphaMaterialCore(
            CapturedAlphaMaterial captured,
            bool verifyIdentity)
        {
            if (captured == null)
            {
                throw new ArgumentNullException(nameof(captured));
            }

            var multiResolution = captured.MultiResolution;
            if (multiResolution != null)
            {
                if (!multiResolution.IsResolved)
                {
                    // The refused Multi transport: all-Unknown alpha and
                    // the shared cause kind whose feature field names the
                    // refusal value in words.
                    return new CapturedAlphaSemantics(
                        AllUnknown(),
                        AlphaUnknownReason.UnsupportedMultiState(
                            LilToonMultiResolution.RefusalFeatureWords(
                                multiResolution.Refusal)));
                }

                // The Multi verify ran once at the resolution point, so the
                // rebuilt material takes the transferred path: the one
                // per-family switch below interprets the resolved family
                // without a second identity verify. Resolution never
                // re-enters this switch.
                return AnalyzeAlphaMaterialCore(
                    new CapturedAlphaMaterial(
                        captured.Family,
                        captured.Evidence,
                        default(PoiyomiSourceEvidence),
                        captured.LilToonEvidence,
                        captured.LockedIdentityRefusal,
                        captured.MaterialPath,
                        captured.MaterialName,
                        captured.ShaderName,
                        predicatesDeclareCutoff:
                            captured.PredicatesDeclareCutoff),
                    false);
            }

            SemanticOutput<ScalarSemanticValue> alpha;
            AlphaUnknownReason unknownReason;
            switch (captured.Family)
            {
                case CapturedAlphaMaterialFamily.Poiyomi:
                    if (verifyIdentity &&
                        !PoiyomiSourceAttestation.TryVerifyPoiyomiIdentity(
                            captured.PoiyomiEvidence, out _))
                    {
                        return UnknownWithShaderReason(
                            AlphaUnknownKind.UnattestedShader,
                            captured.ShaderName);
                    }

                    alpha = PoiyomiMaterialSemantics.InterpretVerifiedAlpha(
                        captured.Evidence, out unknownReason);
                    break;
                case CapturedAlphaMaterialFamily.PoiyomiTwoPass:
                    if (verifyIdentity &&
                        !PoiyomiSourceAttestation.TryVerifyPoiyomiIdentity(
                            captured.PoiyomiEvidence, out _))
                    {
                        return UnknownWithShaderReason(
                            AlphaUnknownKind.UnattestedShader,
                            captured.ShaderName);
                    }

                    alpha = PoiyomiMaterialSemantics.InterpretVerifiedTwoPassAlpha(
                        captured.Evidence, out unknownReason);
                    break;
                case CapturedAlphaMaterialFamily.LilToon:
                    if (captured.LilToonEvidence == null ||
                        (verifyIdentity && !LilToonSourceAttestation
                            .TryVerifyLilToonIdentity(
                                captured.LilToonEvidence, out _)))
                    {
                        return UnknownWithShaderReason(
                            AlphaUnknownKind.UnattestedShader,
                            captured.ShaderName);
                    }

                    alpha = LilToonMaterialSemantics.InterpretVerifiedAlpha(
                        captured.Evidence, out unknownReason);
                    break;
                case CapturedAlphaMaterialFamily.LilToonCutout:
                    if (captured.LilToonEvidence == null ||
                        (verifyIdentity && !LilToonSourceAttestation
                            .TryVerifyLilToonCutoutIdentity(
                                captured.LilToonEvidence, out _)))
                    {
                        return UnknownWithShaderReason(
                            AlphaUnknownKind.UnattestedShader,
                            captured.ShaderName);
                    }

                    alpha = LilToonCutoutMaterialSemantics
                        .InterpretVerifiedCutoutAlpha(
                            captured.Evidence, out unknownReason);
                    break;
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    if (captured.LilToonEvidence == null ||
                        (verifyIdentity && !LilToonSourceAttestation
                            .TryVerifyLilToonTransparentIdentity(
                                captured.LilToonEvidence, out _)))
                    {
                        return UnknownWithShaderReason(
                            AlphaUnknownKind.UnattestedShader,
                            captured.ShaderName);
                    }

                    alpha = LilToonTransparentMaterialSemantics
                        .InterpretVerifiedTransparentAlpha(
                            captured.Evidence, out unknownReason);
                    break;
                default:
                    return UnknownWithShaderReason(
                        AlphaUnknownKind.UnsupportedShader,
                        captured.ShaderName);
            }

            return new CapturedAlphaSemantics(
                new MaterialSemantics(alpha),
                unknownReason);
        }

        internal static CapturedAlphaSemantics AnalyzeAlphaMaterial(
            CapturedAlphaMaterial captured)
        {
            return AnalyzeAlphaMaterialCore(captured, true);
        }

        /// <summary>
        /// The admitted-material sentinel for a material no family selects.
        /// Its family is <see cref="CapturedAlphaMaterialFamily.Unsupported"/>
        /// and its evidence is empty, so
        /// <see cref="AnalyzeAlphaMaterial"/> answers all-Unknown for it and
        /// every slot whose admitted set reaches it refuses as
        /// <c>AdmittedMaterialSemanticsUnknown</c>. The refusal is scoped to
        /// the slots that can hold the material; sibling slots whose
        /// materials attest keep their own proofs.
        /// <para>
        /// A non-None <paramref name="lockedIdentityRefusal"/> records that
        /// the capture recognized this material's locked identity and that
        /// its original shader is unresolvable or unattested. Slot
        /// resolution then refuses by that name instead of the generic
        /// destination.
        /// </para>
        /// <para>
        /// When a producer registered a material's replacement, the record
        /// names the registered source's project path and name instead of
        /// the build copy's. The resolution goes through the read-only
        /// <c>create:false</c> registry lookup — never the creating static
        /// one, which would fabricate an entry and block a later
        /// RegisterReplacedObject for that object — and the record stores
        /// only the resolved path and name strings, so the evidence never
        /// holds live Unity objects.
        /// </para>
        /// </summary>
        /// <param name="resolveRegisteredSource">
        /// Maps the live material to the source object a producer
        /// registered as its replacement. It answers null when nothing
        /// registered the material, and the material keeps its own
        /// identity.
        /// </param>
        internal static CapturedAlphaMaterial UnattestedMaterial(
            RendererAnalysisRefusal lockedIdentityRefusal =
                RendererAnalysisRefusal.None,
            Material sourceMaterial = null,
            RegisteredSourceLookup resolveRegisteredSource = null)
        {
            var namedSource = namedSourceFor(
                resolveRegisteredSource, sourceMaterial);
            return new CapturedAlphaMaterial(
                CapturedAlphaMaterialFamily.Unsupported,
                new CapturedMaterialEvidence(
                    false, null, false, ColorSpace.Uninitialized,
                    Array.Empty<CapturedMaterialEvidence.PresenceEntry>(),
                    Array.Empty<CapturedMaterialEvidence.ScalarEntry>(),
                    Array.Empty<CapturedMaterialEvidence.ColorEntry>(),
                    Array.Empty<CapturedMaterialEvidence.VectorEntry>(),
                    Array.Empty<CapturedMaterialEvidence.TextureEntry>(),
                    Array.Empty<CapturedTextureEvidence>()),
                default(PoiyomiSourceEvidence),
                null,
                lockedIdentityRefusal,
                materialPath: namedSource != null
                    ? AssetDatabase.GetAssetPath(namedSource)
                    : null,
                materialName: namedSource != null
                    ? namedSource.name
                    : null,
                shaderName: sourceMaterial != null &&
                    sourceMaterial.shader != null
                    ? sourceMaterial.shader.name
                    : null);
        }

    }
}
