using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Analysis
{
    internal enum AdmittedPropertyOutcome
    {
        Singleton,
        NotFiniteExact,
        SourcesDisagree,
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
