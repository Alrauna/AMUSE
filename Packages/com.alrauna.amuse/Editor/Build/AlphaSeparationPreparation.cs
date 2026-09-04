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
    /// <summary>
    /// Substitutes only one family's shader-opaque-conversion step for one
    /// admitted material — attestation, eligibility and clone preparation —
    /// returning either a mapped opaque <see cref="Material"/> or a
    /// conversion refusal. They exist so public-package fixtures whose
    /// stand-in shaders deliberately fail source attestation can still drive
    /// the feature: the internal barrier overload takes them as optional
    /// final parameters, and production passes nothing and runs the real
    /// <see cref="PoiyomiOpaqueConversion"/> and
    /// <see cref="LilToonOpaqueTarget"/> paths.
    /// <para>
    /// Delegates on an existing overload, not an interface, registry,
    /// adapter hierarchy, result framework, or a test fixture framework.
    /// </para>
    /// </summary>
    internal delegate bool VerifiedPoiyomiConversion(
        Material live,
        CapturedMaterialEvidence derived,
        Material preparedOpaque,
        out Material opaque,
        out PoiyomiOpaqueConversionRefusal refusal);

    /// <summary>
    /// The verified-fixture seam for the lilToon conversion families
    /// (cutout and transparent) — the exact shape of
    /// <see cref="VerifiedPoiyomiConversion"/> with the
    /// lilToon refusal vocabulary.
    /// </summary>
    internal delegate bool VerifiedLilToonConversion(
        Material live,
        CapturedMaterialEvidence derived,
        Material preparedOpaque,
        out Material opaque,
        out LilToonOpaqueConversionRefusal refusal);

    /// <summary>
    /// Barrier-side alpha-separation preparation: conversion-relevance
    /// resolution, per-slot conversion admission, the shader-family branch,
    /// the opaque mapping, material clone creation and naming, and
    /// mesh clone creation. It runs inside the extension-free semantic barrier
    /// and mutates nothing but AMUSE-owned transient objects — no renderer, no
    /// clip, no source asset, and no asset saving.
    /// <para>
    /// The shader-family branch lives here and nowhere else: Poiyomi and
    /// the two lilToon alpha frontends (cutout and transparent) convert,
    /// the opaque lilToon frontend is already the canonical opaque answer
    /// and maps to itself, and every other family is refused with
    /// <see cref="AlphaSeparationSlotRefusal.OpaqueConversionUnsupportedFamily"/>.
    /// Adding a conversion family later means adding one case here and
    /// one entry in each family map, and nothing else in the feature.
    /// </para>
    /// </summary>
    internal static class AlphaSeparationPreparation
    {
        /// <summary>
        /// Prepares one renderer's alpha separation for the later apply pass.
        /// Returns the retained record, or null when no candidate slot
        /// survived preparation — in which case nothing was prepared and
        /// nothing is retained for the renderer.
        /// <para>
        /// Every admitted material of a candidate slot must map before that
        /// slot is prepared at all; a slot with any unmapped admitted value is
        /// dropped here and never reaches validation. Mappings are registered
        /// avatar-wide only after a slot's full admitted set has mapped, so a
        /// refused slot leaves no prepared state behind, and clones created
        /// for its earlier admitted materials are destroyed immediately —
        /// they are AMUSE-owned transients from this same call.
        /// </para>
        /// </summary>
        internal static PreparedRendererSeparation Prepare(
            AmusePlatformFinishState state,
            UnityRendererMutationTarget target,
            string rendererPath,
            MeshSeparationPlan plan,
            CapturedAnimationEvidence evidence,
            IReadOnlyList<Material> admittedLiveMaterials,
            VerifiedPoiyomiConversion poiyomiConversion,
            VerifiedLilToonConversion lilToonConversion)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (rendererPath == null)
                throw new ArgumentNullException(nameof(rendererPath));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));
            if (admittedLiveMaterials == null)
                throw new ArgumentNullException(nameof(admittedLiveMaterials));

            if (!plan.HasAnyOpaqueCandidates)
            {
                return null;
            }

            // Conversion relevance is re-resolved only for renderers that
            // produced opaque candidates AND carry a conversion-capable
            // family, and only against the union of those families' own
            // conversion requests: the alpha resolution that ran above is
            // bit-for-bit untouched, and the same captured bindings are
            // judged a second time under a different relevance. A renderer
            // with no conversion-capable family has no conversion that
            // could read animated state, so the whole relevance pass is
            // skipped rather than run against a foreign family's request.
            var conversionCapableFamilies = new List<
                CapturedAlphaMaterialFamily>();
            foreach (var admitted in evidence.AdmittedMaterials)
            {
                if (ConversionRequestForFamily(admitted.Family) != null &&
                    !conversionCapableFamilies.Contains(admitted.Family))
                {
                    conversionCapableFamilies.Add(admitted.Family);
                }
            }

            // Per-family buckets of the renderer-wide conversion bindings:
            // each recognized binding is re-resolved against every capable
            // family's own request, because relevance is a per-family fact
            // and the union only decides recognition and the renderer-wide
            // refusals below.
            var conversionBuckets = new Dictionary<
                CapturedAlphaMaterialFamily,
                List<(CapturedFloatBinding Binding,
                      AnimatedPropertyRef Reference)>>();
            var conversionPropertyNamesByFamily = new Dictionary<
                CapturedAlphaMaterialFamily,
                HashSet<string>>();
            if (conversionCapableFamilies.Count > 0)
            {
                var unionRequest = MaterialEvidenceRequest.Combine(
                    conversionCapableFamilies
                        .Select(ConversionRequestForFamily)
                        .ToArray());
                var conversionBindings = new List<(
                    CapturedFloatBinding Binding,
                    AnimatedPropertyRef Reference)>();
                foreach (var clipEvidence in evidence.Clips)
                {
                    foreach (var binding in clipEvidence.FloatBindings)
                    {
                        var resolution =
                            UnityAnimationEvidenceCapture
                                .ResolveProofRelevant(
                                    binding,
                                    rendererPath,
                                    unionRequest,
                                    out var reference);
                        if (resolution ==
                            ProofRelevantBindingResolution
                                .UnrecognizedMaterialBinding)
                        {
                            return RefuseEveryCandidateSlot(
                                state,
                                plan,
                                AlphaSeparationSlotRefusal
                                    .ConversionBindingUnrecognized);
                        }

                        if (resolution ==
                            ProofRelevantBindingResolution.RendererWide)
                        {
                            conversionBindings.Add((binding, reference));
                        }
                    }
                }

                // Conversion-relevant animated material state is
                // renderer-wide by binding scope — a material.<Property>
                // curve addresses no slot — so these two conditions
                // invalidate every candidate slot of this renderer and
                // nothing else: never the renderer's alpha analysis, never
                // another renderer.
                if (conversionBindings.Count > 0 && evidence.HasAdditiveLayer)
                {
                    return RefuseEveryCandidateSlot(
                        state,
                        plan,
                        AlphaSeparationSlotRefusal
                            .ConversionStateUnderAdditiveLayer);
                }

                if (conversionBindings.Count > 0 &&
                    evidence.HasUnnormalizedDirectBlendTree)
                {
                    return RefuseEveryCandidateSlot(
                        state,
                        plan,
                        AlphaSeparationSlotRefusal
                            .ConversionStateUnderUnnormalizedDirectBlendTree);
                }

                foreach (var family in conversionCapableFamilies)
                {
                    var bucket = new List<(
                        CapturedFloatBinding Binding,
                        AnimatedPropertyRef Reference)>();
                    var names = new HashSet<string>();
                    foreach (var (binding, _) in conversionBindings)
                    {
                        if (UnityAnimationEvidenceCapture
                                .ResolveProofRelevant(
                                    binding,
                                    rendererPath,
                                    ConversionRequestForFamily(family),
                                    out var familyReference) ==
                            ProofRelevantBindingResolution.RendererWide)
                        {
                            bucket.Add((binding, familyReference));
                            names.Add(familyReference.PropertyName);
                        }
                    }

                    conversionBuckets.Add(family, bucket);
                    conversionPropertyNamesByFamily.Add(family, names);
                }
            }

            var slots = AmusePlatformFinishPass.MaterialSlotsFor(
                evidence, rendererPath);
            var candidateSlots = new List<PreparedSlotSeparation>(
                plan.Submeshes.Count);

            foreach (var submesh in plan.Submeshes)
            {
                if (submesh.Disposition ==
                    SubmeshSeparationDisposition.Unchanged)
                {
                    continue;
                }

                var slotIndex = submesh.SourceMaterialBindingIndex;

                // Barrier-side marker-clip check: VirtualClip.SetObjectCurve
                // silently no-ops on a marker clip, so a special motion
                // carrying this slot's material-swap binding refuses the slot
                // before any conversion work is prepared for it. The apply
                // pass re-checks the live clip, which is the authoritative
                // one.
                var markerCarries = false;
                foreach (var clipEvidence in evidence.Clips)
                {
                    if (!clipEvidence.IsSpecialMotion)
                    {
                        continue;
                    }

                    foreach (var objectBinding in
                                 clipEvidence.ObjectBindings)
                    {
                        if (!string.Equals(
                                objectBinding.Path,
                                rendererPath,
                                StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (LiveAnimationObservation
                                .TryParseMaterialSlotBinding(
                                    objectBinding.PropertyName,
                                    out var markerSlot) &&
                            markerSlot == slotIndex)
                        {
                            markerCarries = true;
                            break;
                        }
                    }

                    if (markerCarries)
                    {
                        break;
                    }
                }

                if (markerCarries)
                {
                    state.RecordSlotRefusal(
                        AlphaSeparationSlotRefusal
                            .MarkerClipCarriesSlotBinding);
                    continue;
                }

                var mapping = new Dictionary<Material, Material>();
                var pendingClones = new List<Material>();
                var slotRefusal = AlphaSeparationSlotRefusal.None;
                foreach (var admittedIndex in
                             slots[slotIndex].AdmittedMaterialIndices)
                {
                    var captured = evidence.AdmittedMaterials[admittedIndex];
                    var live = admittedLiveMaterials[admittedIndex];
                    if (mapping.ContainsKey(live))
                    {
                        continue;
                    }

                    // The avatar-wide map supplies only a reusable artifact,
                    // never a conversion decision: the conversion decision is
                    // per renderer/slot because conversion-relevant animation
                    // and admitted derived evidence are renderer-specific,
                    // while the generated opaque artifact is deduplicated
                    // avatar-wide by source material. Every renderer/slot
                    // therefore always performs its own family, admission,
                    // overwrite, attestation and eligibility validation; an
                    // already-prepared result is handed to the conversion
                    // boundary so no duplicate clone is created, and a
                    // refused renderer/slot never reuses the artifact.
                    Material preparedOpaque = null;
                    if (state.Separation != null &&
                        state.Separation.TryGetOpaque(
                            live, out var registered))
                    {
                        preparedOpaque = registered;
                    }

                    // Only conversion-capable families read their bucket:
                    // the opaque lilToon arm maps to itself before
                    // admission and the unsupported arm refuses before it,
                    // so a null bucket never reaches an admission.
                    conversionBuckets.TryGetValue(
                        captured.Family, out var familyBindings);
                    conversionPropertyNamesByFamily.TryGetValue(
                        captured.Family, out var familyPropertyNames);
                    slotRefusal = ConvertAdmittedMaterial(
                        captured,
                        live,
                        familyBindings,
                        familyPropertyNames,
                        preparedOpaque,
                        poiyomiConversion,
                        lilToonConversion,
                        out var opaque);
                    if (slotRefusal != AlphaSeparationSlotRefusal.None)
                    {
                        break;
                    }

                    mapping.Add(live, opaque);
                    if (!ReferenceEquals(opaque, live) &&
                        !ReferenceEquals(opaque, preparedOpaque))
                    {
                        pendingClones.Add(opaque);
                    }
                }

                if (slotRefusal != AlphaSeparationSlotRefusal.None)
                {
                    state.RecordSlotRefusal(slotRefusal);
                    // The slot is dropped with nothing registered: clones
                    // created for its earlier admitted materials would be
                    // unreachable and unknown to the apply pass's sweep. They
                    // are AMUSE-owned transients created by this same call
                    // and are destroyed here.
                    foreach (var pending in pendingClones)
                    {
                        UnityEngine.Object.DestroyImmediate(pending);
                    }

                    continue;
                }

                // Every admitted material of the slot mapped, so the slot is
                // prepared. Registration is avatar-wide and deduplicating;
                // doing it only after the full admitted set mapped keeps a
                // refused slot from leaving prepared state behind.
                foreach (var pair in mapping)
                {
                    RegisterPreparedOpaque(state, pair.Key, pair.Value);
                }

                candidateSlots.Add(new PreparedSlotSeparation(
                    submesh, mapping));
            }

            if (candidateSlots.Count == 0)
            {
                return null;
            }

            var prepared = new PreparedRendererSeparation(
                target,
                rendererPath,
                plan,
                evidence,
                candidateSlots);

            // A mesh clone is created only when the plan requires a split and
            // at least one Split slot survived preparation; clones abandoned
            // by later validation failures are handled by the apply pass's
            // sweep, not by avoiding creation.
            if (plan.RequiresAnySplit &&
                candidateSlots.Any(
                    slot => slot.Plan.Disposition ==
                            SubmeshSeparationDisposition.Split))
            {
                prepared.MeshClone = UnityEngine.Object.Instantiate(
                    target.ExpectedMesh);
            }

            return prepared;
        }

        /// <summary>
        /// Records one renderer-scoped refusal once per candidate slot it
        /// drops, so the buckets always count dropped slots, and retains
        /// nothing for the renderer.
        /// </summary>
        private static PreparedRendererSeparation RefuseEveryCandidateSlot(
            AmusePlatformFinishState state,
            MeshSeparationPlan plan,
            AlphaSeparationSlotRefusal reason)
        {
            foreach (var submesh in plan.Submeshes)
            {
                if (submesh.Disposition !=
                    SubmeshSeparationDisposition.Unchanged)
                {
                    state.RecordSlotRefusal(reason);
                }
            }

            return null;
        }

        /// <summary>
        /// Runs the shader-family conversion boundary for one admitted
        /// material: the per-family branch, derived conversion evidence
        /// admitted against this material's own captured defaults under its
        /// family's own conversion request, the conversion step (the real
        /// family route, or the verified-fixture seam), and the renderer-wide
        /// runtime-overwrite rule against the family's own recipe. Returns
        /// the mapped opaque result, or the slot-local refusal.
        /// </summary>
        private static AlphaSeparationSlotRefusal ConvertAdmittedMaterial(
            CapturedAlphaMaterial captured,
            Material live,
            IReadOnlyList<(CapturedFloatBinding Binding,
                           AnimatedPropertyRef Reference)> conversionBindings,
            IReadOnlyCollection<string> conversionPropertyNames,
            Material preparedOpaque,
            VerifiedPoiyomiConversion poiyomiConversion,
            VerifiedLilToonConversion lilToonConversion,
            out Material opaque)
        {
            opaque = null;
            switch (captured.Family)
            {
                case CapturedAlphaMaterialFamily.LilToon:
                    // The opaque lilToon frontend is already the canonical
                    // opaque answer — alpha is forced to one — so there is
                    // nothing to convert and no admission to run. The
                    // identity mapping keeps a runtime material swap on this
                    // slot valid without a clone.
                    opaque = live;
                    return AlphaSeparationSlotRefusal.None;
                case CapturedAlphaMaterialFamily.LilToonCutout:
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                {
                    var isTransparent = captured.Family ==
                        CapturedAlphaMaterialFamily.LilToonTransparent;
                    // Derived conversion evidence: the same group-and-admit
                    // loop the alpha resolution uses, re-run against this
                    // family's own conversion request, so conversion shares
                    // one admission implementation rather than duplicating
                    // it. A conversion-relevant animated property that is
                    // not an exact singleton equal to this material's own
                    // serialized default refuses here.
                    if (!AdmittedMaterialStates.TryAdmitDerivedEvidence(
                            captured,
                            conversionBindings,
                            ConversionRequestForFamily(captured.Family),
                            out var derived,
                            out _))
                    {
                        return AlphaSeparationSlotRefusal
                            .ConversionStateNotAdmitted;
                    }

                    // The renderer-wide runtime-overwrite rule runs BEFORE
                    // the conversion step, against this family's own recipe:
                    // a canonical recipe property that an admitted
                    // conversion binding drives must already hold its
                    // canonical value, because admission is exact-singleton
                    // against the material's own serialized default. Failing
                    // it is a slot-local refusal, not a defect — and no
                    // material may be created for a slot already known to
                    foreach (var (property, canonicalValue) in
                                 CanonicalPropertiesForFamily(
                                     captured.Family))
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

                    if (lilToonConversion != null)
                    {
                        // The verified-fixture seam substitutes the
                        // lilToon conversion step the caller supplies —
                        // effective render state, real eligibility and
                        // the real canonical
                        // clone recipe — and deliberately skips the
                        // source-identity check no stand-in shader can pass.
                        if (!lilToonConversion(
                                live,
                                derived,
                                preparedOpaque,
                                out opaque,
                                out _))
                        {
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }
                    }
                    else
                    {
                        // Effective non-property facts, read in the barrier
                        // beside the evidence: neither fact is
                        // animation-reachable, so reading them here is not a
                        // late live read of animation-relevant state.
                        LilToonOpaqueTarget.ReadEffectiveRenderState(
                            live, out var queue, out var renderType);

                        // Conversion attestation of the pinned cutout or
                        // transparent source, per the merged conversion
                        // design.
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
                                    sourceEvidence, out _)
                            : LilToonSourceAttestation
                                .TryVerifyLilToonCutoutIdentity(
                                    sourceEvidence, out _);
                        if (!attested)
                        {
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }

                        var eligibility = isTransparent
                            ? LilToonTransparentSourceEligibility
                                .EvaluateVerifiedEligibility(
                                    derived, queue, renderType)
                            : LilToonCutoutSourceEligibility
                                .EvaluateVerifiedEligibility(
                                    derived, queue, renderType);
                        if (eligibility.Outcome !=
                            LilToonOpaqueConversionOutcome.Convertible)
                        {
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }

                        // An already-prepared artifact for this source is
                        // reused here; only a first conversion creates the
                        // canonical clone.
                        opaque = preparedOpaque ??
                            LilToonOpaqueTarget
                                .PrepareCanonicalOpaqueClone(live, derived);
                    }

                    return AlphaSeparationSlotRefusal.None;
                }
                case CapturedAlphaMaterialFamily.Poiyomi:
                {
                    // Derived conversion evidence: the same group-and-admit
                    // loop the alpha resolution uses, re-run against this
                    // family's own conversion request, so conversion shares
                    // one admission implementation rather than duplicating
                    // it. A conversion-relevant animated property that is
                    // not an exact singleton equal to this material's own
                    // serialized default refuses here.
                    if (!AdmittedMaterialStates.TryAdmitDerivedEvidence(
                            captured,
                            conversionBindings,
                            PoiyomiOpaqueConversion
                                .ConversionEvidenceRequest,
                            out var derived,
                            out _))
                    {
                        return AlphaSeparationSlotRefusal
                            .ConversionStateNotAdmitted;
                    }

                    // The renderer-wide runtime-overwrite rule runs BEFORE
                    // the conversion step, against this family's own recipe:
                    // a canonical recipe property that an admitted
                    // conversion binding drives must already hold its
                    // canonical value, because admission is exact-singleton
                    // against the material's own serialized default. Failing
                    // it is a slot-local refusal, not a defect — and no
                    // material may be created for a slot already known to
                    // violate the rule the recipe depends on.
                    foreach (var (property, canonicalValue) in
                                 CanonicalPropertiesForFamily(
                                     captured.Family))
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

                    if (poiyomiConversion != null)
                    {
                        // The verified-fixture seam substitutes the
                        // shader-family conversion step — effective render
                        // state, real eligibility and the real canonical
                        // clone recipe — and deliberately skips the
                        // source-identity check no stand-in shader can pass.
                        if (!poiyomiConversion(
                                live,
                                derived,
                                preparedOpaque,
                                out opaque,
                                out _))
                        {
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }
                    }
                    else
                    {
                        // Effective non-property facts, read in the barrier
                        // beside the evidence: neither fact is
                        // animation-reachable, so reading them here is not a
                        // late live read of animation-relevant state.
                        PoiyomiOpaqueConversion.ReadEffectiveRenderState(
                            live, out var queue, out var renderType);

                        // Conversion attestation of the pinned source, per
                        // the merged conversion design: a locked Poiyomi
                        // material fails here.
                        var sourceEvidence =
                            PoiyomiOpaqueConversion
                                .GatherConversionSourceEvidence(
                                    live.shader, derived);
                        if (!PoiyomiMaterialSemantics
                                .TryVerifyPoiyomiIdentity(
                                    sourceEvidence, out _))
                        {
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }

                        var eligibility =
                            PoiyomiOpaqueConversion
                                .EvaluateVerifiedEligibility(
                                    derived, queue, renderType);
                        switch (eligibility.Outcome)
                        {
                            case PoiyomiOpaqueConversionOutcome.AlreadyOpaque:
                                opaque = live;
                                break;
                            case PoiyomiOpaqueConversionOutcome.Convertible:
                                // An already-prepared artifact for this
                                // source is reused here; only a first
                                // conversion creates the canonical clone.
                                opaque = preparedOpaque ??
                                    PoiyomiOpaqueConversion
                                        .PrepareCanonicalOpaqueClone(live);
                                break;
                            default:
                                return AlphaSeparationSlotRefusal
                                    .OpaqueConversionRefused;
                        }
                    }

                    return AlphaSeparationSlotRefusal.None;
                }
                default:
                    // No conversion-capable family attests this material,
                    // and not every admitted runtime value could be mapped.
                    return AlphaSeparationSlotRefusal
                        .OpaqueConversionUnsupportedFamily;
            }
        }

        /// <summary>
        /// The conversion request one family's conversion boundary reads, or
        /// null for families with no conversion: the relevance pass unions
        /// the requests of the renderer's conversion-capable families, and
        /// per-material admission re-resolves against the material's own
        /// family request.
        /// </summary>
        private static MaterialEvidenceRequest ConversionRequestForFamily(
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

        /// <summary>
        /// The canonical recipe one family's conversion writes, or null for
        /// families with no conversion: the runtime-overwrite rule checks
        /// exactly the family's own recipe properties against their
        /// canonical values.
        /// </summary>
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

        /// <summary>
        /// Registers one prepared opaque result in the avatar-wide
        /// deduplicating map, creating the retained record on first use.
        /// AlreadyOpaque identities are recorded in the mapping but never
        /// enter <see cref="PreparedAlphaSeparation.CreatedClones"/>, and a
        /// source that is already registered — shared across slots or
        /// renderers — returns its existing result, so two renderers
        /// referencing the same source material share one clone.
        /// </summary>
        private static Material RegisterPreparedOpaque(
            AmusePlatformFinishState state,
            Material source,
            Material opaque)
        {
            state.Separation ??= new PreparedAlphaSeparation();
            if (state.Separation.TryGetOpaque(source, out var existing))
            {
                return existing;
            }

            state.Separation.RegisterOpaque(source, opaque);
            return opaque;
        }
    }
}
