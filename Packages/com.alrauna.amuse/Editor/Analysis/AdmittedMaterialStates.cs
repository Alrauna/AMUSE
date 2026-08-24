using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;

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
    }
}
