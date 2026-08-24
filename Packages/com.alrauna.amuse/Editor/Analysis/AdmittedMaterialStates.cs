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
