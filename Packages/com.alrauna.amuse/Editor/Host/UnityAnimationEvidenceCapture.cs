using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Alrauna.Amuse.Editor.Semantics;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Host
{
    internal enum AnimatedPropertyKind
    {
        Scalar,
        ColorComponent,
        VectorComponent,
        TextureScaleOffsetComponent,
    }

    internal enum ProofRelevantBindingResolution
    {
        Irrelevant,
        RendererWide,
        UnrecognizedMaterialBinding,
    }

    internal readonly struct AnimatedPropertyRef
    {
        internal AnimatedPropertyRef(
            string propertyName,
            AnimatedPropertyKind kind,
            int componentIndex)
        {
            PropertyName = propertyName;
            Kind = kind;
            ComponentIndex = componentIndex;
        }

        internal string PropertyName { get; }
        internal AnimatedPropertyKind Kind { get; }
        internal int ComponentIndex { get; }
    }

    /// <summary>
    /// Selects the supported shader family for one admitted material and the
    /// two requests that family answers with: what ordinary alpha proof may
    /// consider, and what the closed batch must gather. Selection attests
    /// nothing and captures nothing. The single
    /// <see cref="ClosedAlphaMaterialCapturer"/> call that follows is the sole
    /// material-evidence capture and the sole source-attestation decision for
    /// the whole admitted batch.
    /// <para>
    /// The two are separate because the capture schema also carries evidence
    /// no alpha proof depends on. Folding that into
    /// <paramref name="alphaRelevanceRequest"/> would make ordinary alpha
    /// analysis treat conversion-only render state as a proof input — a
    /// coverage regression, not a safety improvement.
    /// </para>
    /// </summary>
    internal delegate bool AlphaMaterialRequestSelector(
        Material material,
        out CapturedAlphaMaterialFamily family,
        out MaterialEvidenceRequest alphaRelevanceRequest,
        out MaterialEvidenceRequest captureRequest);

    /// <summary>
    /// Captures the closed evidence batch for one admitted material batch.
    /// <para>
    /// The positional order is part of this delegate's contract, not an
    /// accidental property of one implementation: on success,
    /// <paramref name="captured"/>[i] corresponds to <paramref name="materials"/>[i]
    /// and therefore also to <paramref name="families"/>[i], and
    /// <c>captured.Count</c> equals <c>materials.Count</c>.
    /// </para>
    /// <para>
    /// Returning <c>false</c> rejects the complete batch. A refusal exposes no
    /// partial captured result to the caller: <paramref name="captured"/> must
    /// be left null or empty, never a prefix of the batch.
    /// </para>
    /// </summary>
    internal delegate bool ClosedAlphaMaterialCapturer(
        IReadOnlyList<Material> materials,
        IReadOnlyList<CapturedAlphaMaterialFamily> families,
        MaterialEvidenceRequest request,
        out IReadOnlyList<CapturedAlphaMaterial> captured);

    internal static class UnityAnimationEvidenceCapture
    {
        private const string MaterialPrefix = "material.";

        /// <summary>
        /// Unity's generated name for a texture property's packed scale and
        /// offset. Shared so the derivation below and its inverse in
        /// admitted-state resolution cannot drift apart.
        /// </summary>
        internal const string TextureScaleOffsetSuffix = "_ST";

        private static readonly MaterialEvidenceRequest EmptyRequest =
            new MaterialEvidenceRequest(
                false,
                false,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<TexturePropertyEvidenceRequest>());

        /// <summary>
        /// Captures animation evidence for one renderer. <paramref name="rendererPath"/>
        /// is that renderer's Unity animation path and scopes material-slot
        /// closure to it: a renderer on the avatar root has the empty path, so
        /// empty is valid and only null is rejected.
        /// </summary>
        /// <param name="admittedLiveMaterials">
        /// The live build-copy materials behind the returned evidence's
        /// <see cref="CapturedAnimationEvidence.AdmittedMaterials"/>, in the same
        /// order, so index <c>i</c> of one addresses index <c>i</c> of the other.
        /// <para>
        /// This is a live, transient host capability, NOT proof evidence: it is
        /// deliberately handed back beside the immutable evidence rather than
        /// stored in it, so the evidence graph's no-live-Unity-object guarantee is
        /// unchanged. A closure failure yields an empty list, never a partial one.
        /// </para>
        /// </param>
        internal static CapturedAnimationEvidence Capture(
            string rendererPath,
            IReadOnlyList<Material> currentSlots,
            CommittedControllerGraphResult graph,
            IPlatformAnimatorBindings bindings,
            out IReadOnlyList<Material> admittedLiveMaterials,
            ClosedAlphaMaterialCapturer capturer = null)
        {
            return CaptureGraph(
                rendererPath,
                currentSlots,
                graph,
                bindings,
                UnityMaterialSemantics.TrySelectAlphaMaterialRequests,
                capturer ?? UnityMaterialSemantics.TryCaptureClosedAlphaMaterials,
                out admittedLiveMaterials);
        }

        // Public-project vendor fixtures exercise verified frontend equations but
        // intentionally do not publish vendor source assets. This seam exists only
        // for the package's friend test assembly to test closure mechanics; product
        // integration must call the graph entry point above.
        internal static CapturedAnimationEvidence CaptureObservedForTests(
            string rendererPath,
            IReadOnlyList<LiveClipObservation> observations,
            IReadOnlyList<Material> currentSlots,
            CommittedControllerGraphResult graph,
            AlphaMaterialRequestSelector selectRequest,
            ClosedAlphaMaterialCapturer capturer,
            out IReadOnlyList<Material> admittedLiveMaterials)
        {
            return CaptureObserved(
                rendererPath,
                observations,
                currentSlots,
                graph,
                selectRequest,
                capturer,
                out admittedLiveMaterials);
        }

        internal static CapturedAnimationEvidence CaptureGraphForTests(
            string rendererPath,
            IReadOnlyList<Material> currentSlots,
            CommittedControllerGraphResult graph,
            IPlatformAnimatorBindings bindings,
            AlphaMaterialRequestSelector selectRequest,
            ClosedAlphaMaterialCapturer capturer,
            out IReadOnlyList<Material> admittedLiveMaterials)
        {
            return CaptureGraph(
                rendererPath,
                currentSlots,
                graph,
                bindings,
                selectRequest,
                capturer,
                out admittedLiveMaterials);
        }

        private static CapturedAnimationEvidence CaptureGraph(
            string rendererPath,
            IReadOnlyList<Material> currentSlots,
            CommittedControllerGraphResult graph,
            IPlatformAnimatorBindings bindings,
            AlphaMaterialRequestSelector selectRequest,
            ClosedAlphaMaterialCapturer capturer,
            out IReadOnlyList<Material> admittedLiveMaterials)
        {
            // Empty is the avatar root's animation path and is valid; only an
            // absent path is a caller defect.
            if (rendererPath == null)
                throw new ArgumentNullException(nameof(rendererPath));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            if (graph.Refusal != AvatarAnimationRefusal.None)
            {
                throw new InvalidOperationException(
                    "Animation evidence capture requires a successful committed graph.");
            }

            var observations = new List<LiveClipObservation>();
            foreach (var layer in graph.Layers)
            {
                foreach (var clip in layer.Clips)
                {
                    observations.Add(LiveAnimationObservation.ObserveClip(
                        clip, bindings.IsSpecialMotion(clip)));
                }
            }

            return CaptureObserved(
                rendererPath,
                observations,
                currentSlots,
                graph,
                selectRequest,
                capturer,
                out admittedLiveMaterials);
        }

        private static CapturedAnimationEvidence CaptureObserved(
            string rendererPath,
            IReadOnlyList<LiveClipObservation> observations,
            IReadOnlyList<Material> currentSlots,
            CommittedControllerGraphResult graph,
            AlphaMaterialRequestSelector selectRequest,
            ClosedAlphaMaterialCapturer capturer,
            out IReadOnlyList<Material> admittedLiveMaterials)
        {
            // Assigned once here so that EVERY closure-failure return below hands
            // back an empty list rather than a partial one. The real pairing is
            // assigned only at the single success return, after the captured count
            // has been checked against the admitted count.
            admittedLiveMaterials = Array.Empty<Material>();

            if (rendererPath == null)
                throw new ArgumentNullException(nameof(rendererPath));
            if (observations == null)
                throw new ArgumentNullException(nameof(observations));
            if (currentSlots == null)
                throw new ArgumentNullException(nameof(currentSlots));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (selectRequest == null)
                throw new ArgumentNullException(nameof(selectRequest));
            if (capturer == null) throw new ArgumentNullException(nameof(capturer));
            if (graph.Refusal != AvatarAnimationRefusal.None)
                throw new InvalidOperationException(
                    "Animation evidence capture requires a successful committed graph.");

            var hasAdditiveLayer = false;
            var hasUnnormalizedDirectBlendTree = false;
            foreach (var layer in graph.Layers)
            {
                hasAdditiveLayer |=
                    layer.BlendingMode == AnimatorLayerBlendingMode.Additive;
                hasUnnormalizedDirectBlendTree |=
                    layer.HasUnnormalizedDirectBlendTree;
            }

            CapturedAnimationEvidence Failed(
                MaterialDependencyClosureFailure failure)
            {
                return new CapturedAnimationEvidence(
                    failure,
                    EmptyRequest,
                    Array.Empty<CapturedClipEvidence>(),
                    Array.Empty<CapturedAlphaMaterial>(),
                    Array.Empty<int>(),
                    hasUnnormalizedDirectBlendTree,
                    hasAdditiveLayer);
            }

            var admitted = new List<Material>();
            var materialIndices = new Dictionary<Material, int>(
                ReferenceComparer<Material>.Instance);
            bool TryAdmit(Material material, out int index)
            {
                index = default;
                if (material == null) return false;
                if (materialIndices.TryGetValue(material, out index)) return true;
                index = admitted.Count;
                materialIndices.Add(material, index);
                admitted.Add(material);
                return true;
            }

            var currentMaterialIndices = new int[currentSlots.Count];
            for (var slot = 0; slot < currentSlots.Count; slot++)
            {
                if (!TryAdmit(currentSlots[slot], out currentMaterialIndices[slot]))
                    return Failed(
                        MaterialDependencyClosureFailure.MissingCurrentMaterial);
            }

            foreach (var observation in observations)
            {
                if (observation == null)
                    throw new ArgumentException(
                        "Clip observations cannot contain null.",
                        nameof(observations));

                foreach (var binding in observation.Objects)
                {
                    if (!LiveAnimationObservation.TryParseMaterialSlotBinding(
                            binding.PropertyName, out var slot) ||
                        !AddressesAnalyzedRenderer(binding.Path, rendererPath))
                    {
                        continue;
                    }

                    if (slot >= currentSlots.Count)
                    {
                        return Failed(
                            MaterialDependencyClosureFailure.SlotOutOfRange);
                    }
                    foreach (var value in binding.Values)
                    {
                        if (!(value is Material material) ||
                            !TryAdmit(material, out _))
                        {
                            return Failed(
                                MaterialDependencyClosureFailure.InvalidSwapValue);
                        }
                    }
                }
            }

            // Every admitted material completes request selection before any
            // evidence is captured: each union below spans the whole batch, so
            // no material's evidence can be gathered until both are known.
            var families = new CapturedAlphaMaterialFamily[admitted.Count];
            var alphaRequests = new MaterialEvidenceRequest[admitted.Count];
            var captureRequests = new MaterialEvidenceRequest[admitted.Count];
            for (var index = 0; index < admitted.Count; index++)
            {
                if (!selectRequest(
                        admitted[index],
                        out families[index],
                        out alphaRequests[index],
                        out captureRequests[index]) ||
                    alphaRequests[index] == null ||
                    captureRequests[index] == null)
                {
                    return Failed(
                        MaterialDependencyClosureFailure.UnattestedMaterial);
                }
            }

            // Two closed unions over the same admitted batch. The capture
            // schema is what the one capture gathers; the alpha request is what
            // ordinary alpha proof may consider afterwards. Only the latter is
            // retained, because no consumer reads the broader schema once the
            // evidence it authorized has been captured.
            var alphaRelevanceRequest =
                MaterialEvidenceRequest.Combine(alphaRequests);
            var captureRequest =
                MaterialEvidenceRequest.Combine(captureRequests);

            // The closed batch capture is the sole source-attestation decision,
            // so its refusal means exactly what an unselectable family means:
            // some admitted material is not attested. There is no second,
            // weaker capture outcome to distinguish.
            if (!capturer(
                    admitted,
                    families,
                    captureRequest,
                    out var capturedMaterials))
            {
                return Failed(
                    MaterialDependencyClosureFailure.UnattestedMaterial);
            }
            if (capturedMaterials == null ||
                capturedMaterials.Count != admitted.Count)
            {
                throw new InvalidOperationException(
                    "Closed material capture returned an invalid result count.");
            }

            var clips = new List<CapturedClipEvidence>(observations.Count);
            foreach (var observation in observations)
            {
                var floats = new List<CapturedFloatBinding>(
                    observation.Floats.Count);
                foreach (var binding in observation.Floats)
                {
                    floats.Add(new CapturedFloatBinding(
                        binding.Path,
                        binding.TypeName,
                        binding.PropertyName,
                        binding.IsFiniteExact,
                        new List<float>(binding.Values)));
                }

                var objects = new List<CapturedObjectBinding>(
                    observation.Objects.Count);
                foreach (var binding in observation.Objects)
                {
                    var indices = new List<int>();
                    if (LiveAnimationObservation.TryParseMaterialSlotBinding(
                            binding.PropertyName, out _))
                    {
                        // Same condition as admission above, so the immutable
                        // copy can never disagree with what was admitted. A
                        // foreign slot binding is another renderer's evidence:
                        // it is dropped outright rather than retained with an
                        // empty index list, which would read as "this renderer
                        // has a swap that admits nothing".
                        if (!AddressesAnalyzedRenderer(
                                binding.Path, rendererPath))
                        {
                            continue;
                        }

                        foreach (var value in binding.Values)
                        {
                            indices.Add(materialIndices[(Material)value]);
                        }
                    }

                    objects.Add(new CapturedObjectBinding(
                        binding.Path,
                        binding.TypeName,
                        binding.PropertyName,
                        indices));
                }

                clips.Add(new CapturedClipEvidence(
                    observation.Name,
                    observation.IsSpecialMotion,
                    floats,
                    objects));
            }

            // The success return, and the only place the live pairing escapes.
            // This list preserves the exact admitted-material order passed into
            // the capturer, and ClosedAlphaMaterialCapturer's contract places
            // captured[i] against materials[i], so index i of this list
            // addresses index i of AdmittedMaterials. The count check above
            // proves only the expected cardinality, not this ordering.
            admittedLiveMaterials = Array.AsReadOnly(admitted.ToArray());

            return new CapturedAnimationEvidence(
                MaterialDependencyClosureFailure.None,
                alphaRelevanceRequest,
                clips,
                new List<CapturedAlphaMaterial>(capturedMaterials),
                currentMaterialIndices,
                hasUnnormalizedDirectBlendTree,
                hasAdditiveLayer);
        }

        /// <summary>
        /// Whether an observed binding addresses the renderer being analyzed.
        /// <para>
        /// A capture describes exactly one renderer, but the observations it
        /// receives come from the whole committed graph. A material-slot binding
        /// names one renderer path, so a binding on any other path describes a
        /// <em>different</em> renderer's slots: its slot index is meaningless
        /// against this renderer's slot count, and its values are not this
        /// renderer's material dependencies. Admitting them would let one
        /// renderer's animation refuse, widen, or pad another's evidence.
        /// </para>
        /// <para>
        /// Ordinal comparison, matching every other animation-path comparison in
        /// AMUSE. The empty path is the avatar root's own path and compares like
        /// any other.
        /// </para>
        /// </summary>
        private static bool AddressesAnalyzedRenderer(
            string bindingPath,
            string rendererPath)
        {
            return string.Equals(
                bindingPath, rendererPath, StringComparison.Ordinal);
        }

        internal static IReadOnlyCollection<string>
            DeriveTextureScaleOffsetProperties(MaterialEvidenceRequest relevance)
        {
            if (relevance == null)
                throw new ArgumentNullException(nameof(relevance));

            var properties = new List<string>();
            foreach (var texture in relevance.TextureProperties)
            {
                if ((texture.Evidence & TextureEvidenceKinds.ScaleOffset) != 0)
                {
                    properties.Add(
                        texture.PropertyName + TextureScaleOffsetSuffix);
                }
            }

            return properties.AsReadOnly();
        }

        internal static ProofRelevantBindingResolution ResolveProofRelevant(
            CapturedFloatBinding binding,
            string rendererPath,
            MaterialEvidenceRequest relevance,
            out AnimatedPropertyRef reference)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (relevance == null)
                throw new ArgumentNullException(nameof(relevance));

            reference = default;
            if (!string.Equals(
                    binding.Path, rendererPath, StringComparison.Ordinal))
            {
                return ProofRelevantBindingResolution.Irrelevant;
            }

            var scaleOffsetProperties =
                DeriveTextureScaleOffsetProperties(relevance);
            if (TryStripPrefix(
                    binding.PropertyName, MaterialPrefix, out var property))
            {
                if (TryResolveGeneratedProperty(
                        property,
                        relevance,
                        scaleOffsetProperties,
                        out reference))
                {
                    return ProofRelevantBindingResolution.RendererWide;
                }

                return CouldAddressRelevantProperty(
                        property, relevance, scaleOffsetProperties)
                    ? ProofRelevantBindingResolution.UnrecognizedMaterialBinding
                    : ProofRelevantBindingResolution.Irrelevant;
            }

            if (TryStripIndexedMaterialPrefix(
                    binding.PropertyName, out property) &&
                (TryResolveGeneratedProperty(
                     property,
                     relevance,
                     scaleOffsetProperties,
                     out reference) ||
                 CouldAddressRelevantProperty(
                     property, relevance, scaleOffsetProperties)))
            {
                reference = default;
                return ProofRelevantBindingResolution.UnrecognizedMaterialBinding;
            }

            return ProofRelevantBindingResolution.Irrelevant;
        }

        /// <summary>
        /// Whether an object-reference binding on this renderer carries material
        /// property syntax that could address a proof-relevant property, and is
        /// therefore unsupported rather than irrelevant.
        /// <para>
        /// Unity's own generator emits no such curve, but that characterizes what
        /// Unity <em>generates</em>, not what the committed graph <em>holds</em>:
        /// clips are authored and rewritten by many tools. Silently ignoring an
        /// unparsed binding that in fact drives a proof input is a false positive,
        /// so this recognizes the syntax conservatively and refuses. It asserts
        /// nothing about whether such a curve is runtime-effective, and it admits
        /// no texture-reference state — the binding is closed, not interpreted.
        /// </para>
        /// </summary>
        internal static bool IsUnrecognizedObjectMaterialBinding(
            CapturedObjectBinding binding,
            string rendererPath,
            MaterialEvidenceRequest relevance)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (relevance == null)
                throw new ArgumentNullException(nameof(relevance));

            if (!string.Equals(
                    binding.Path, rendererPath, StringComparison.Ordinal))
            {
                return false;
            }

            // The one object-reference material form AMUSE does recognize. It is
            // admitted as a material swap by MaterialSlotsFor, never refused here.
            if (LiveAnimationObservation.TryParseMaterialSlotBinding(
                    binding.PropertyName, out _))
            {
                return false;
            }

            if (!TryStripPrefix(
                    binding.PropertyName, MaterialPrefix, out var property) &&
                !TryStripIndexedMaterialPrefix(
                    binding.PropertyName, out property))
            {
                return false;
            }

            return CouldAddressRelevantProperty(
                       property,
                       relevance,
                       DeriveTextureScaleOffsetProperties(relevance)) ||
                   CouldAddressAny(property, RequestedTextureNames(relevance));
        }

        /// <summary>
        /// The relevance request's texture property names. An object-reference curve
        /// assigns a reference, so these are exactly the proof inputs it can name.
        /// The float path deliberately omits them: a float binding cannot assign a
        /// texture, only its derived scale/offset components, which
        /// <see cref="DeriveTextureScaleOffsetProperties"/> already covers. Both
        /// paths share <c>CouldAddressAny</c> so their syntax recognition cannot
        /// drift apart.
        /// </summary>
        private static IReadOnlyCollection<string> RequestedTextureNames(
            MaterialEvidenceRequest relevance)
        {
            var properties = new List<string>(relevance.TextureProperties.Count);
            foreach (var texture in relevance.TextureProperties)
            {
                properties.Add(texture.PropertyName);
            }

            return properties;
        }

        private static bool TryResolveGeneratedProperty(
            string property,
            MaterialEvidenceRequest relevance,
            IReadOnlyCollection<string> scaleOffsetProperties,
            out AnimatedPropertyRef reference)
        {
            reference = default;
            if (TrySplitComponent(property, "xyzw", out var stem, out var index) &&
                ContainsOrdinal(scaleOffsetProperties, stem))
            {
                reference = new AnimatedPropertyRef(
                    stem,
                    AnimatedPropertyKind.TextureScaleOffsetComponent,
                    index);
                return true;
            }

            if (TrySplitComponent(property, "rgba", out stem, out index) &&
                ContainsOrdinal(relevance.ColorProperties, stem))
            {
                reference = new AnimatedPropertyRef(
                    stem, AnimatedPropertyKind.ColorComponent, index);
                return true;
            }

            if (TrySplitComponent(property, "xyzw", out stem, out index) &&
                ContainsOrdinal(relevance.VectorProperties, stem))
            {
                reference = new AnimatedPropertyRef(
                    stem, AnimatedPropertyKind.VectorComponent, index);
                return true;
            }

            if (HasCharacterizedComponentSuffix(property) ||
                !ContainsOrdinal(relevance.ScalarProperties, property))
            {
                return false;
            }

            reference = new AnimatedPropertyRef(
                property, AnimatedPropertyKind.Scalar, -1);
            return true;
        }

        private static bool CouldAddressRelevantProperty(
            string property,
            MaterialEvidenceRequest relevance,
            IReadOnlyCollection<string> scaleOffsetProperties)
        {
            return CouldAddressAny(property, relevance.ScalarProperties) ||
                   CouldAddressAny(property, relevance.ColorProperties) ||
                   CouldAddressAny(property, relevance.VectorProperties) ||
                   CouldAddressAny(property, scaleOffsetProperties);
        }

        private static bool CouldAddressAny(
            string text,
            IEnumerable<string> relevantProperties)
        {
            if (text == null) return false;
            foreach (var property in relevantProperties)
            {
                if (string.Equals(text, property, StringComparison.Ordinal))
                    return true;
                if (text.Length > property.Length &&
                    text.StartsWith(property, StringComparison.Ordinal) &&
                    (text[property.Length] == '.' ||
                     text[property.Length] == '['))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TrySplitComponent(
            string property,
            string suffixes,
            out string stem,
            out int componentIndex)
        {
            stem = null;
            componentIndex = -1;
            if (property == null || property.Length < 3 ||
                property[property.Length - 2] != '.')
            {
                return false;
            }

            componentIndex = suffixes.IndexOf(property[property.Length - 1]);
            if (componentIndex < 0) return false;
            stem = property.Substring(0, property.Length - 2);
            return true;
        }

        private static bool HasCharacterizedComponentSuffix(string property)
        {
            return TrySplitComponent(property, "rgba", out _, out _) ||
                   TrySplitComponent(property, "xyzw", out _, out _);
        }

        private static bool ContainsOrdinal(
            IEnumerable<string> properties,
            string property)
        {
            foreach (var candidate in properties)
            {
                if (string.Equals(candidate, property, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool TryStripPrefix(
            string text,
            string prefix,
            out string remainder)
        {
            remainder = null;
            if (text == null ||
                !text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            remainder = text.Substring(prefix.Length);
            return true;
        }

        private static bool TryStripIndexedMaterialPrefix(
            string text,
            out string property)
        {
            const string prefix = "material[";
            property = null;
            if (text == null ||
                !text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            var closingBracket = text.IndexOf(']', prefix.Length);
            if (closingBracket < 0 || closingBracket + 1 >= text.Length ||
                text[closingBracket + 1] != '.')
            {
                return false;
            }

            property = text.Substring(closingBracket + 2);
            return true;
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T>
            where T : class
        {
            internal static readonly ReferenceComparer<T> Instance =
                new ReferenceComparer<T>();

            public bool Equals(T x, T y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
