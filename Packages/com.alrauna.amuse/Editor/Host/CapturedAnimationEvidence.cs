using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Editor.Host
{
    internal enum MaterialDependencyClosureFailure
    {
        None,
        MissingCurrentMaterial,
        SlotOutOfRange,
        InvalidSwapValue,
        UnattestedMaterial,
    }

    internal sealed class CapturedFloatBinding
    {
        internal CapturedFloatBinding(
            string path,
            string typeName,
            string propertyName,
            bool isFiniteExact,
            IList<float> values)
        {
            Path = path;
            TypeName = typeName;
            PropertyName = propertyName;
            IsFiniteExact = isFiniteExact;
            Values = new ReadOnlyCollection<float>(new List<float>(values));
        }

        internal string Path { get; }
        internal string TypeName { get; }
        internal string PropertyName { get; }
        internal bool IsFiniteExact { get; }
        internal IReadOnlyList<float> Values { get; }
    }

    internal sealed class CapturedObjectBinding
    {
        internal CapturedObjectBinding(
            string path,
            string typeName,
            string propertyName,
            IList<int> admittedMaterialIndices)
        {
            Path = path;
            TypeName = typeName;
            PropertyName = propertyName;
            AdmittedMaterialIndices = new ReadOnlyCollection<int>(
                new List<int>(admittedMaterialIndices));
        }

        internal string Path { get; }
        internal string TypeName { get; }
        internal string PropertyName { get; }
        internal IReadOnlyList<int> AdmittedMaterialIndices { get; }
    }

    internal sealed class CapturedClipEvidence
    {
        internal CapturedClipEvidence(
            string name,
            bool isSpecialMotion,
            IList<CapturedFloatBinding> floatBindings,
            IList<CapturedObjectBinding> objectBindings)
        {
            Name = name;
            IsSpecialMotion = isSpecialMotion;
            FloatBindings = new ReadOnlyCollection<CapturedFloatBinding>(
                new List<CapturedFloatBinding>(floatBindings));
            ObjectBindings = new ReadOnlyCollection<CapturedObjectBinding>(
                new List<CapturedObjectBinding>(objectBindings));
        }

        internal string Name { get; }
        internal bool IsSpecialMotion { get; }
        internal IReadOnlyList<CapturedFloatBinding> FloatBindings { get; }
        internal IReadOnlyList<CapturedObjectBinding> ObjectBindings { get; }
    }

    /// <summary>
    /// One renderer material slot and the admitted materials it may hold. The
    /// indices address <see cref="CapturedAnimationEvidence.AdmittedMaterials"/>
    /// directly and are never slot indices.
    /// <para>
    /// This exists for <em>every</em> slot, animated or not. An unanimated slot
    /// carries exactly one index — its current assignment — so the current
    /// material is resolved rather than dropped.
    /// </para>
    /// </summary>
    internal sealed class CapturedMaterialSlotEvidence
    {
        internal CapturedMaterialSlotEvidence(
            int slotIndex,
            IReadOnlyList<int> admittedMaterialIndices)
        {
            if (slotIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(slotIndex), slotIndex,
                    "Material slot indices cannot be negative.");
            }

            if (admittedMaterialIndices == null)
                throw new ArgumentNullException(nameof(admittedMaterialIndices));

            SlotIndex = slotIndex;
            AdmittedMaterialIndices = new ReadOnlyCollection<int>(
                new List<int>(admittedMaterialIndices));
        }

        internal int SlotIndex { get; }
        internal IReadOnlyList<int> AdmittedMaterialIndices { get; }
    }

    internal sealed class CapturedAnimationEvidence
    {
        internal CapturedAnimationEvidence(
            MaterialDependencyClosureFailure closureFailure,
            MaterialEvidenceRequest alphaRelevanceRequest,
            IList<CapturedClipEvidence> clips,
            IList<CapturedAlphaMaterial> admittedMaterials,
            IList<int> currentMaterialIndices,
            bool hasUnnormalizedDirectBlendTree,
            bool hasAdditiveLayer)
        {
            ClosureFailure = closureFailure;
            AlphaRelevanceRequest = alphaRelevanceRequest
                ?? throw new ArgumentNullException(nameof(alphaRelevanceRequest));
            Clips = new ReadOnlyCollection<CapturedClipEvidence>(
                new List<CapturedClipEvidence>(clips));
            AdmittedMaterials = new ReadOnlyCollection<CapturedAlphaMaterial>(
                new List<CapturedAlphaMaterial>(admittedMaterials));
            CurrentMaterialIndices = new ReadOnlyCollection<int>(
                new List<int>(currentMaterialIndices));
            HasUnnormalizedDirectBlendTree = hasUnnormalizedDirectBlendTree;
            HasAdditiveLayer = hasAdditiveLayer;
        }

        internal bool IsClosed =>
            ClosureFailure == MaterialDependencyClosureFailure.None;
        internal MaterialDependencyClosureFailure ClosureFailure { get; }
        internal MaterialEvidenceRequest AlphaRelevanceRequest { get; }
        internal IReadOnlyList<CapturedClipEvidence> Clips { get; }
        internal IReadOnlyList<CapturedAlphaMaterial> AdmittedMaterials { get; }

        // Slot order matches the current renderer material-slot order. This is
        // the immutable replacement for the live currentSlots input.
        internal IReadOnlyList<int> CurrentMaterialIndices { get; }

        internal bool HasUnnormalizedDirectBlendTree { get; }
        internal bool HasAdditiveLayer { get; }
    }
}
