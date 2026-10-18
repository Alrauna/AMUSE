using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Editor.Analysis
{
    internal enum AdmittedPropertyOutcome
    {
        Singleton,
        NotFiniteExact,
        SourcesDisagree,
    }

    internal static class AnimatedPropertyAdmission
    {
        /// <summary>
        /// Derives a material's evidence under the singleton values of its
        /// admitted animated properties, or refuses the renderer.
        /// <para>
        /// This is the admission gate. Every animated property contributing to
        /// this material must be proven finite-exact and equal to this
        /// material's own captured default; if any binding drives a different
        /// value, or cannot be proven finite-exact, the whole renderer refuses
        /// and no evidence is returned. Only when every binding passes is new
        /// evidence constructed, with those singleton values substituted.
        /// </para>
        /// <para>
        /// It operates on <em>derived</em> evidence: each property group writes
        /// into the evidence produced by the previous group, so a material with
        /// multiple animated properties accumulates all of them into one
        /// object. Derivations share the unmutated entry arrays under the
        /// constructor's rule, so this derivation step allocates only the
        /// derived wrappers and one array per modified category rather than
        /// deep-copying evidence.
        /// </para>
        /// </summary>
        internal static bool TryAdmitDerivedEvidence(
            CapturedAlphaMaterial material,
            IReadOnlyList<(CapturedFloatBinding Binding,
                           AnimatedPropertyRef Reference)> bindings,
            MaterialEvidenceRequest relevance,
            out CapturedMaterialEvidence derived,
            out RendererAnalysisRefusal refusal,
            string scaleOffsetSuffix,
            IReadOnlyList<BlockStateEntry> slotBlockEntries = null,
            IReadOnlyList<CapturedAlphaMaterial> allAdmittedMaterials = null)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            if (relevance == null)
                throw new ArgumentNullException(nameof(relevance));

            derived = material.Evidence;
            refusal = RendererAnalysisRefusal.None;

            if (slotBlockEntries != null && slotBlockEntries.Count > 0)
            {
                var animatedProperties = new HashSet<string>(StringComparer.Ordinal);
                foreach (var (_, reference) in bindings)
                {
                    animatedProperties.Add(reference.PropertyName);
                }

                foreach (var entry in slotBlockEntries)
                {
                    if (animatedProperties.Contains(entry.Name))
                    {
                        continue;
                    }

                    switch (entry.Type)
                    {
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range:
                        case ShaderPropertyType.Int:
                            if (relevance.ScalarProperties.Contains(
                                    entry.Name, StringComparer.Ordinal))
                            {
                                derived = derived.WithScalar(entry.Name, entry.FloatValue);
                            }
                            break;
                        case ShaderPropertyType.Color:
                            if (relevance.ColorProperties.Contains(
                                    entry.Name, StringComparer.Ordinal))
                            {
                                derived = derived.WithColor(entry.Name, entry.ColorValue);
                            }
                            break;
                        case ShaderPropertyType.Vector:
                            if (relevance.VectorProperties.Contains(
                                    entry.Name, StringComparer.Ordinal))
                            {
                                derived = derived.WithVector(entry.Name, entry.VectorValue);
                            }
                            break;
                    }
                }
            }

            foreach (var group in GroupByProperty(bindings))
            {
                var anyAdmittedDeclares = AnyAdmittedMaterialDeclaresProperty(
                    allAdmittedMaterials, group.PropertyName, group.Kind,
                    relevance, scaleOffsetSuffix);
                refusal = Admit(
                    group, relevance, scaleOffsetSuffix, ref derived,
                    slotBlockEntries, anyAdmittedDeclares);
                if (refusal != RendererAnalysisRefusal.None)
                {
                    derived = material.Evidence;
                    return false;
                }
            }

            return true;
        }

        private static bool AnyAdmittedMaterialDeclaresProperty(
            IReadOnlyList<CapturedAlphaMaterial> admittedMaterials,
            string propertyName,
            AnimatedPropertyKind kind,
            MaterialEvidenceRequest relevance,
            string scaleOffsetSuffix)
        {
            if (admittedMaterials == null) return false;
            for (var i = 0; i < admittedMaterials.Count; i++)
            {
                var evidence = admittedMaterials[i]?.Evidence;
                if (evidence == null) continue;

                switch (kind)
                {
                    case AnimatedPropertyKind.Scalar:
                        if (evidence.TryGetScalar(propertyName, out _)) return true;
                        break;
                    case AnimatedPropertyKind.ColorComponent:
                        if (evidence.TryGetColor(propertyName, out _)) return true;
                        break;
                    case AnimatedPropertyKind.VectorComponent:
                        if (evidence.TryGetVector(propertyName, out _)) return true;
                        break;
                    case AnimatedPropertyKind.TextureScaleOffsetComponent:
                    {
                        var texture = OwningTextureProperty(
                            relevance, propertyName, scaleOffsetSuffix);
                        if (evidence.TryGetTexture(texture, out var assignment) &&
                            assignment.HasScaleOffset)
                        {
                            return true;
                        }
                        break;
                    }
                }
            }
            return false;
        }

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

        private static RendererAnalysisRefusal Admit(
            AnimatedPropertyGroup group,
            MaterialEvidenceRequest relevance,
            string scaleOffsetSuffix,
            ref CapturedMaterialEvidence evidence,
            IReadOnlyList<BlockStateEntry> slotBlockEntries,
            bool anyAdmittedDeclares = false)
        {
            switch (group.Kind)
            {
                case AnimatedPropertyKind.Scalar:
                {
                    if (!evidence.TryGetScalar(
                            group.PropertyName, out var serialized))
                    {
                        if (anyAdmittedDeclares)
                        {
                            return RendererAnalysisRefusal.None;
                        }

                        return RendererAnalysisRefusal
                            .AnimatedPropertyAbsentFromAdmittedMaterial;
                    }

                    BlockStateEntry blockEntry;
                    if (TryGetBlockEntry(
                            slotBlockEntries, group.PropertyName,
                            ShaderPropertyType.Float, out blockEntry) ||
                        TryGetBlockEntry(
                            slotBlockEntries, group.PropertyName,
                            ShaderPropertyType.Range, out blockEntry) ||
                        TryGetBlockEntry(
                            slotBlockEntries, group.PropertyName,
                            ShaderPropertyType.Int, out blockEntry))
                    {
                        var blockFloat = blockEntry.FloatValue;
                        if (!(blockFloat == serialized))
                        {
                            return RendererAnalysisRefusal
                                .AnimatedMaterialPropertyNotSingleton;
                        }
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
                        if (anyAdmittedDeclares)
                        {
                            return RendererAnalysisRefusal.None;
                        }

                        return RendererAnalysisRefusal
                            .AnimatedPropertyAbsentFromAdmittedMaterial;
                    }

                    var componentBindings = group.ComponentBindings();
                    if (TryGetBlockEntry(
                            slotBlockEntries, group.PropertyName,
                            ShaderPropertyType.Color, out var blockEntry))
                    {
                        var blockColor = blockEntry.ColorValue;
                        var blockVec = new Vector4(blockColor.r, blockColor.g, blockColor.b, blockColor.a);
                        var serVec = new Vector4(serialized.r, serialized.g, serialized.b, serialized.a);
                        foreach (var component in componentBindings.Keys)
                        {
                            if (!(blockVec[component] == serVec[component]))
                            {
                                return RendererAnalysisRefusal
                                    .AnimatedMaterialPropertyNotSingleton;
                            }
                        }
                    }

                    var outcome = AdmitColor(
                        componentBindings, serialized, out var admitted);
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
                        if (anyAdmittedDeclares)
                        {
                            return RendererAnalysisRefusal.None;
                        }

                        return RendererAnalysisRefusal
                            .AnimatedPropertyAbsentFromAdmittedMaterial;
                    }

                    var componentBindings = group.ComponentBindings();
                    if (TryGetBlockEntry(
                            slotBlockEntries, group.PropertyName,
                            ShaderPropertyType.Vector, out var blockEntry))
                    {
                        var blockVector = blockEntry.VectorValue;
                        foreach (var component in componentBindings.Keys)
                        {
                            if (!(blockVector[component] == serialized[component]))
                            {
                                return RendererAnalysisRefusal
                                    .AnimatedMaterialPropertyNotSingleton;
                            }
                        }
                    }

                    var outcome = AdmitVector(
                        componentBindings, serialized, out var admitted);
                    if (outcome != AdmittedPropertyOutcome.Singleton)
                    {
                        return RefusalFor(outcome);
                    }

                    evidence = evidence.WithVector(group.PropertyName, admitted);
                    return RendererAnalysisRefusal.None;
                }
                case AnimatedPropertyKind.TextureScaleOffsetComponent:
                {
                    var texture = OwningTextureProperty(
                        relevance, group.PropertyName, scaleOffsetSuffix);
                    if (!evidence.TryGetTexture(texture, out var assignment) ||
                        !assignment.HasScaleOffset)
                    {
                        if (anyAdmittedDeclares)
                        {
                            return RendererAnalysisRefusal.None;
                        }

                        return RendererAnalysisRefusal
                            .AnimatedPropertyAbsentFromAdmittedMaterial;
                    }

                    var serialized = new Vector4(
                        assignment.Scale.x,
                        assignment.Scale.y,
                        assignment.Offset.x,
                        assignment.Offset.y);

                    var outcome = AdmitVector(
                        group.ComponentBindings(), serialized, out _);
                    if (outcome != AdmittedPropertyOutcome.Singleton)
                    {
                        return RefusalFor(outcome);
                    }

                    return RendererAnalysisRefusal.None;
                }

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(group), group.Kind,
                        "Unhandled animated property kind.");
            }
        }

        private static string OwningTextureProperty(
            MaterialEvidenceRequest relevance,
            string derivedName,
            string scaleOffsetSuffix)
        {
            string owner = null;
            foreach (var request in relevance.TextureProperties)
            {
                if ((request.Evidence & TextureEvidenceKinds.ScaleOffset) == 0)
                {
                    continue;
                }

                if (!string.Equals(
                        request.PropertyName + scaleOffsetSuffix,
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

        internal sealed class AnimatedPropertyGroup
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

        private static bool TryGetBlockEntry(
            IReadOnlyList<BlockStateEntry> slotBlockEntries,
            string propertyName,
            ShaderPropertyType type,
            out BlockStateEntry entry)
        {
            if (slotBlockEntries != null)
            {
                for (var i = 0; i < slotBlockEntries.Count; i++)
                {
                    var candidate = slotBlockEntries[i];
                    if (string.Equals(
                            candidate.Name, propertyName, StringComparison.Ordinal) &&
                        candidate.Type == type)
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }
    }
}
