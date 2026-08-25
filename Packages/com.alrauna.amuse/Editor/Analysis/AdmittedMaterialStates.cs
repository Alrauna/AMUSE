using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Analysis
{
    internal enum AdmittedPropertyOutcome
    {
        Singleton,
        NotFiniteExact,
        SourcesDisagree,
    }

    /// <summary>
    /// One slot's admitted-state resolution: either one
    /// <see cref="AlphaResolution"/> per admitted material, or a named refusal
    /// and no resolution at all.
    /// <para>
    /// A refusal exposes no partial prefix. Admission is a proof about the whole
    /// slot, so resolutions produced for admitted materials examined before the
    /// refusing one are not authorization for anything and are discarded.
    /// </para>
    /// </summary>
    internal sealed class SlotResolutionResult
    {
        private SlotResolutionResult(
            RendererAnalysisRefusal refusal,
            IReadOnlyList<AlphaResolution> resolutions)
        {
            IsResolved = refusal == RendererAnalysisRefusal.None;
            Refusal = refusal;
            Resolutions = resolutions;
        }

        internal bool IsResolved { get; }
        internal RendererAnalysisRefusal Refusal { get; }
        internal IReadOnlyList<AlphaResolution> Resolutions { get; }

        internal static SlotResolutionResult Refused(
            RendererAnalysisRefusal refusal)
        {
            if (refusal == RendererAnalysisRefusal.None)
            {
                throw new ArgumentException(
                    "A refused slot must name its refusal.", nameof(refusal));
            }

            return new SlotResolutionResult(
                refusal, Array.Empty<AlphaResolution>());
        }

        internal static SlotResolutionResult Resolved(
            IReadOnlyList<AlphaResolution> resolutions)
        {
            if (resolutions == null)
                throw new ArgumentNullException(nameof(resolutions));

            var copy = new AlphaResolution[resolutions.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = resolutions[index];
            }

            return new SlotResolutionResult(
                RendererAnalysisRefusal.None, Array.AsReadOnly(copy));
        }
    }

    internal static class AdmittedMaterialStates
    {
        /// <summary>
        /// The provisional per-renderer bound on the admitted-state product.
        /// It is an implementation parameter, not a semantic constant: four
        /// slots with eight admitted materials each, or twelve slots with two,
        /// and explicitly unmeasured. It therefore appears in no signature and
        /// is visible to nothing outside this class; revising it requires
        /// evidence rather than preference.
        /// </summary>
        private const int MaxAdmittedStates = 4096;

        /// <summary>
        /// Bounds the Cartesian product of the per-slot admitted-state counts
        /// <em>before</em> any state is materialized and before any geometry
        /// work, so reaching the bound costs nothing. Arithmetic only: no
        /// state, tuple, combination, or lazy enumerable is constructed, in
        /// O(slots) time and O(1) memory.
        /// <para>
        /// The zero scan is a separate pass on purpose. A zero count means the
        /// product is empty, which is a fact about the multiset of factors and
        /// not about their order; folding it into the multiplication loop would
        /// make <c>[int.MaxValue, 0]</c> and <c>[0, int.MaxValue]</c> disagree.
        /// Validation precedes both, so an invalid count is never hidden by a
        /// zero elsewhere in the list.
        /// </para>
        /// <para>
        /// An empty list budgets to one: a renderer with no slots has exactly
        /// one state, the empty tuple.
        /// </para>
        /// </summary>
        /// <returns>
        /// <c>true</c> when the product fits within the bound, inclusive at the
        /// bound itself. On <c>false</c> the renderer is refused with
        /// <c>RendererAnalysisRefusal.AdmittedStateBudgetExceeded</c> and
        /// <paramref name="productSize"/> carries no meaning.
        /// </returns>
        internal static bool TryBudgetProduct(
            IReadOnlyList<int> perSlotAdmittedCounts,
            out int productSize)
        {
            if (perSlotAdmittedCounts == null)
                throw new ArgumentNullException(nameof(perSlotAdmittedCounts));

            var anyEmptySlot = false;
            foreach (var count in perSlotAdmittedCounts)
            {
                if (count < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(perSlotAdmittedCounts), count,
                        "Admitted-state counts cannot be negative.");
                }

                if (count == 0)
                    anyEmptySlot = true;
            }

            if (anyEmptySlot)
            {
                productSize = 0;
                return true;
            }

            // Every remaining count is at least one and the running product is
            // at most the bound before each step, so the widened multiplication
            // cannot overflow a long.
            long product = 1;
            foreach (var count in perSlotAdmittedCounts)
            {
                product *= count;
                if (product > MaxAdmittedStates)
                {
                    productSize = 0;
                    return false;
                }
            }

            productSize = (int)product;
            return true;
        }

        /// <summary>
        /// Resolves one renderer material slot: every material the slot may
        /// hold, each against its <em>own</em> captured serialized defaults.
        /// <para>
        /// A renderer-wide default would be wrong the moment two admitted
        /// materials disagree, and that error is in the false-positive
        /// direction. The same animated binding may therefore be admitted
        /// against one admitted material and refused against another.
        /// </para>
        /// <para>
        /// This is one slot. It forms no Cartesian product, deduplicates
        /// nothing, classifies no triangle, and consumes neither the renderer's
        /// structural facts nor the admitted-state budget; those are
        /// renderer-wide and belong to the integration path.
        /// </para>
        /// </summary>
        /// <param name="relevance">
        /// MUST be the same closed request the supplied bindings were resolved
        /// against — that is, <c>CapturedAnimationEvidence.RelevanceRequest</c>.
        /// It is the only source that can invert a derived
        /// <c>&lt;texture&gt;_ST</c> name back to its owning texture request,
        /// and a different request would either fail that inversion outright or
        /// resolve the name against the wrong closed schema.
        /// </param>
        internal static SlotResolutionResult ResolveSlot(
            CapturedMaterialSlotEvidence slot,
            IReadOnlyList<CapturedAlphaMaterial> admittedMaterials,
            IReadOnlyList<(CapturedFloatBinding Binding,
                           AnimatedPropertyRef Reference)> slotBindings,
            MaterialEvidenceRequest relevance,
            AlphaFieldProvider alphaFields,
            CapturedAlphaMaterialSemanticsResolver resolveSemantics = null)
        {
            if (slot == null) throw new ArgumentNullException(nameof(slot));
            if (admittedMaterials == null)
                throw new ArgumentNullException(nameof(admittedMaterials));
            if (slotBindings == null)
                throw new ArgumentNullException(nameof(slotBindings));
            if (relevance == null)
                throw new ArgumentNullException(nameof(relevance));
            if (alphaFields == null)
                throw new ArgumentNullException(nameof(alphaFields));

            resolveSemantics ??= UnityMaterialSemantics.AnalyzeAlphaMaterial;

            var groups = GroupByProperty(slotBindings);
            var resolutions = new List<AlphaResolution>(
                slot.AdmittedMaterialIndices.Count);
            foreach (var index in slot.AdmittedMaterialIndices)
            {
                var material = admittedMaterials[index];

                // Each admitted material accumulates its own derivations from
                // its own captured evidence. Nothing crosses between materials.
                var evidence = material.Evidence;
                foreach (var group in groups)
                {
                    var refusal = Admit(group, relevance, ref evidence);
                    if (refusal != RendererAnalysisRefusal.None)
                    {
                        // No partial prefix: resolutions gathered for earlier
                        // admitted materials authorize nothing once the slot
                        // is refused.
                        return SlotResolutionResult.Refused(refusal);
                    }
                }

                var admitted = ReferenceEquals(evidence, material.Evidence)
                    ? material
                    : new CapturedAlphaMaterial(
                        material.Family,
                        evidence,
                        material.PoiyomiEvidence,
                        material.LilToonEvidence);
                var semantics = resolveSemantics(admitted)
                    ?? UnityMaterialSemantics.AllUnknown();
                resolutions.Add(
                    AlphaSemanticsResolver.Resolve(semantics.Alpha, alphaFields));
            }

            return SlotResolutionResult.Resolved(resolutions);
        }

        /// <summary>
        /// One admission decision per logical property. Every binding
        /// contributing to the same property name and kind is decided together
        /// against one captured default, so no clip can be admitted
        /// independently and then overwritten by the next.
        /// <para>
        /// Groups are ordered by first appearance in the supplied bindings and
        /// never by hash iteration, so which refusal a slot reports is
        /// determined by the input rather than by dictionary layout.
        /// </para>
        /// </summary>
        private static List<AnimatedPropertyGroup> GroupByProperty(
            IReadOnlyList<(CapturedFloatBinding Binding,
                           AnimatedPropertyRef Reference)> slotBindings)
        {
            var groups = new List<AnimatedPropertyGroup>();
            var byKey = new Dictionary<
                (string, AnimatedPropertyKind), AnimatedPropertyGroup>();
            foreach (var (binding, reference) in slotBindings)
            {
                var key = (reference.PropertyName, reference.Kind);
                if (!byKey.TryGetValue(key, out var group))
                {
                    group = new AnimatedPropertyGroup(
                        reference.PropertyName, reference.Kind);
                    byKey.Add(key, group);
                    groups.Add(group);
                }

                group.Add(binding, reference.ComponentIndex);
            }

            return groups;
        }

        /// <summary>
        /// Admits one property group against <em>this</em> admitted material's
        /// own captured default and accumulates the derived evidence.
        /// </summary>
        private static RendererAnalysisRefusal Admit(
            AnimatedPropertyGroup group,
            MaterialEvidenceRequest relevance,
            ref CapturedMaterialEvidence evidence)
        {
            switch (group.Kind)
            {
                case AnimatedPropertyKind.Scalar:
                {
                    // Presence first. An animated property this material has no
                    // value for fails closed before admission, substitution, or
                    // resolution: preserved absence is a property of the
                    // evidence primitive, never authorization to ignore the
                    // binding.
                    if (!evidence.TryGetScalar(
                            group.PropertyName, out var serialized))
                    {
                        return RendererAnalysisRefusal
                            .AnimatedPropertyAbsentFromAdmittedMaterial;
                    }

                    var outcome = AdmitScalar(
                        group.Bindings, serialized, out var admitted);
                    if (outcome != AdmittedPropertyOutcome.Singleton)
                    {
                        return RefusalFor(outcome);
                    }

                    evidence = evidence.WithScalar(group.PropertyName, admitted);
                    return RendererAnalysisRefusal.None;
                }

                case AnimatedPropertyKind.ColorComponent:
                {
                    if (!evidence.TryGetColor(
                            group.PropertyName, out var serialized))
                    {
                        return RendererAnalysisRefusal
                            .AnimatedPropertyAbsentFromAdmittedMaterial;
                    }

                    var outcome = AdmitColor(
                        group.ComponentBindings(), serialized, out var admitted);
                    if (outcome != AdmittedPropertyOutcome.Singleton)
                    {
                        return RefusalFor(outcome);
                    }

                    evidence = evidence.WithColor(group.PropertyName, admitted);
                    return RendererAnalysisRefusal.None;
                }

                case AnimatedPropertyKind.VectorComponent:
                {
                    if (!evidence.TryGetVector(
                            group.PropertyName, out var serialized))
                    {
                        return RendererAnalysisRefusal
                            .AnimatedPropertyAbsentFromAdmittedMaterial;
                    }

                    var outcome = AdmitVector(
                        group.ComponentBindings(), serialized, out var admitted);
                    if (outcome != AdmittedPropertyOutcome.Singleton)
                    {
                        return RefusalFor(outcome);
                    }

                    evidence = evidence.WithVector(group.PropertyName, admitted);
                    return RendererAnalysisRefusal.None;
                }

                case AnimatedPropertyKind.TextureScaleOffsetComponent:
                {
                    // Presence, the serialized default, and in V1 the resolved
                    // evidence all come from the texture assignment. The
                    // derived name is not a vector request, so asking the
                    // vector accessor for it would raise the unrequested-name
                    // defect rather than report absence.
                    var texture = OwningTextureProperty(
                        relevance, group.PropertyName);
                    // Both halves are required by the rule, but only the
                    // first is reachable today: capture sets HasScaleOffset to
                    // `hasValue && ScaleOffset requested`, and an _ST reference
                    // exists only where ScaleOffset was requested, so a present
                    // assignment currently implies it. The second half is the
                    // rule, not dead defensiveness, and is why no test
                    // constructs it.
                    if (!evidence.TryGetTexture(texture, out var assignment) ||
                        !assignment.HasScaleOffset)
                    {
                        return RendererAnalysisRefusal
                            .AnimatedPropertyAbsentFromAdmittedMaterial;
                    }

                    // Unity's _ST packing: xy is the scale, zw the offset.
                    var serialized = new Vector4(
                        assignment.Scale.x,
                        assignment.Scale.y,
                        assignment.Offset.x,
                        assignment.Offset.y);

                    // AdmitVector is reused as the component-wise finite-exact
                    // singleton primitive it is; it neither reads nor writes
                    // vector evidence, so this does not make _ST a vector.
                    var outcome = AdmitVector(
                        group.ComponentBindings(), serialized, out _);
                    if (outcome != AdmittedPropertyOutcome.Singleton)
                    {
                        return RefusalFor(outcome);
                    }

                    // No substitution. V1 admission proves the animated
                    // components equal this material's captured scale and
                    // offset exactly, so the captured assignment already is
                    // the one admitted value and this binding is discharged
                    // without changing any evidence. Sibling derivations
                    // accumulated by other groups are left untouched, which is
                    // why this arm assigns nothing to `evidence`.
                    //
                    // This is exact only because admission requires equality
                    // with the default. Widening admission to a different
                    // scale or offset needs a presence-preserving texture
                    // derivation of its own; it must never be done by adding
                    // derived names to VectorProperties.
                    return RendererAnalysisRefusal.None;
                }

                default:
                    // A kind added later must stop here rather than fall into a
                    // wrong evidence category.
                    throw new ArgumentOutOfRangeException(
                        nameof(group), group.Kind,
                        "Unhandled animated property kind.");
            }
        }

        /// <summary>
        /// Inverts the exact derivation that produced the animated name:
        /// <c>DeriveTextureScaleOffsetProperties</c> appends
        /// <c>_ST</c> to each texture request whose evidence includes
        /// <c>ScaleOffset</c>. This scans that same closed request rather than
        /// parsing the suffix off an arbitrary name, so no name outside the
        /// closed evidence request can reach texture evidence.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The relationship is one-to-one by construction. No owner means the
        /// reference could not have been produced by proof-relevance
        /// resolution, and two owners mean the closed request contains
        /// colliding texture names; both are invariant violations rather than
        /// domain outcomes.
        /// </exception>
        private static string OwningTextureProperty(
            MaterialEvidenceRequest relevance,
            string derivedName)
        {
            string owner = null;
            foreach (var request in relevance.TextureProperties)
            {
                if ((request.Evidence & TextureEvidenceKinds.ScaleOffset) == 0)
                {
                    continue;
                }

                if (!string.Equals(
                        request.PropertyName +
                            UnityAnimationEvidenceCapture
                                .TextureScaleOffsetSuffix,
                        derivedName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (owner != null)
                {
                    throw new InvalidOperationException(
                        "Two texture requests derive the scale/offset name '" +
                        derivedName + "'.");
                }

                owner = request.PropertyName;
            }

            if (owner == null)
            {
                throw new InvalidOperationException(
                    "No texture request derives the scale/offset name '" +
                    derivedName + "'.");
            }

            return owner;
        }

        private static RendererAnalysisRefusal RefusalFor(
            AdmittedPropertyOutcome outcome)
        {
            switch (outcome)
            {
                case AdmittedPropertyOutcome.NotFiniteExact:
                    return RendererAnalysisRefusal.UnsupportedAnimationCurveForm;
                case AdmittedPropertyOutcome.SourcesDisagree:
                    return RendererAnalysisRefusal
                        .AnimatedMaterialPropertyNotSingleton;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(outcome), outcome,
                        "An admitted outcome has no refusal.");
            }
        }

        /// <summary>
        /// The bindings contributing to one logical property, in the order the
        /// slot supplied them.
        /// </summary>
        private sealed class AnimatedPropertyGroup
        {
            private readonly List<CapturedFloatBinding> _bindings =
                new List<CapturedFloatBinding>();
            private readonly List<int> _components = new List<int>();

            internal AnimatedPropertyGroup(
                string propertyName, AnimatedPropertyKind kind)
            {
                PropertyName = propertyName;
                Kind = kind;
            }

            internal string PropertyName { get; }
            internal AnimatedPropertyKind Kind { get; }
            internal IReadOnlyList<CapturedFloatBinding> Bindings => _bindings;

            internal void Add(CapturedFloatBinding binding, int componentIndex)
            {
                _bindings.Add(binding);
                _components.Add(componentIndex);
            }

            /// <summary>
            /// The component-keyed view the vector and colour primitives take.
            /// An out-of-range component index is rejected by those primitives
            /// as the parser defect it is, not silenced here.
            /// </summary>
            internal IReadOnlyDictionary<
                int, IReadOnlyList<CapturedFloatBinding>> ComponentBindings()
            {
                var components = new Dictionary<
                    int, List<CapturedFloatBinding>>();
                for (var index = 0; index < _bindings.Count; index++)
                {
                    if (!components.TryGetValue(
                            _components[index], out var bindings))
                    {
                        bindings = new List<CapturedFloatBinding>();
                        components.Add(_components[index], bindings);
                    }

                    bindings.Add(_bindings[index]);
                }

                var view = new Dictionary<
                    int, IReadOnlyList<CapturedFloatBinding>>();
                foreach (var entry in components)
                {
                    view.Add(entry.Key, entry.Value);
                }

                return view;
            }
        }

        internal static AdmittedPropertyOutcome AdmitScalar(
            IReadOnlyList<CapturedFloatBinding> bindings,
            float serializedDefault,
            out float admittedValue)
        {
            if (bindings == null)
                throw new ArgumentNullException(nameof(bindings));

            admittedValue = serializedDefault;
            foreach (var binding in bindings)
            {
                if (!binding.IsFiniteExact)
                    return AdmittedPropertyOutcome.NotFiniteExact;
            }

            foreach (var binding in bindings)
            {
                foreach (var value in binding.Values)
                {
                    if (!(value == serializedDefault))
                        return AdmittedPropertyOutcome.SourcesDisagree;
                }
            }

            return AdmittedPropertyOutcome.Singleton;
        }

        internal static AdmittedPropertyOutcome AdmitColor(
            IReadOnlyDictionary<
                int, IReadOnlyList<CapturedFloatBinding>> componentBindings,
            Color serializedDefault,
            out Color admittedValue)
        {
            var outcome = AdmitVector(
                componentBindings,
                new Vector4(
                    serializedDefault.r,
                    serializedDefault.g,
                    serializedDefault.b,
                    serializedDefault.a),
                out var admitted);
            admittedValue = new Color(
                admitted.x, admitted.y, admitted.z, admitted.w);
            return outcome;
        }

        internal static AdmittedPropertyOutcome AdmitVector(
            IReadOnlyDictionary<
                int, IReadOnlyList<CapturedFloatBinding>> componentBindings,
            Vector4 serializedDefault,
            out Vector4 admittedValue)
        {
            if (componentBindings == null)
                throw new ArgumentNullException(nameof(componentBindings));

            foreach (var component in componentBindings.Keys)
            {
                if (component < 0 || component > 3)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(componentBindings), component,
                        "Component indices must be between zero and three.");
                }
            }

            admittedValue = serializedDefault;
            var outcome = AdmittedPropertyOutcome.Singleton;
            for (var component = 0; component < 4; component++)
            {
                if (!componentBindings.TryGetValue(
                        component, out var bindings))
                {
                    continue;
                }

                var componentOutcome = AdmitScalar(
                    bindings,
                    serializedDefault[component],
                    out var admittedComponent);
                if (componentOutcome == AdmittedPropertyOutcome.NotFiniteExact)
                    return AdmittedPropertyOutcome.NotFiniteExact;
                if (componentOutcome == AdmittedPropertyOutcome.SourcesDisagree)
                {
                    outcome = AdmittedPropertyOutcome.SourcesDisagree;
                    continue;
                }

                admittedValue[component] = admittedComponent;
            }

            return outcome;
        }
    }
}
