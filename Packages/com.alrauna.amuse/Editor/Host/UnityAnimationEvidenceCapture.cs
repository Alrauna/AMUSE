using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Alrauna.Amuse.Editor.Semantics;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Host
{
    internal delegate bool AlphaMaterialAttestor(
        Material material,
        out CapturedAlphaMaterialFamily family,
        out MaterialEvidenceRequest request);

    internal delegate bool ClosedAlphaMaterialCapturer(
        IReadOnlyList<Material> materials,
        IReadOnlyList<CapturedAlphaMaterialFamily> families,
        MaterialEvidenceRequest request,
        out IReadOnlyList<CapturedAlphaMaterial> captured);

    internal static class UnityAnimationEvidenceCapture
    {
        private static readonly MaterialEvidenceRequest EmptyRequest =
            new MaterialEvidenceRequest(
                false,
                false,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<TexturePropertyEvidenceRequest>());

        internal static CapturedAnimationEvidence Capture(
            IReadOnlyList<Material> currentSlots,
            CommittedControllerGraphResult graph,
            IPlatformAnimatorBindings bindings)
        {
            return CaptureGraph(
                currentSlots,
                graph,
                bindings,
                UnityMaterialSemantics.TryAttestAlphaMaterial,
                UnityMaterialSemantics.TryCaptureClosedAlphaMaterials);
        }

        // Public-project vendor fixtures exercise verified frontend equations but
        // intentionally do not publish vendor source assets. This seam exists only
        // for the package's friend test assembly to test closure mechanics; product
        // integration must call the three-argument graph entry point above.
        internal static CapturedAnimationEvidence CaptureObservedForTests(
            IReadOnlyList<LiveClipObservation> observations,
            IReadOnlyList<Material> currentSlots,
            CommittedControllerGraphResult graph,
            AlphaMaterialAttestor attestor,
            ClosedAlphaMaterialCapturer capturer)
        {
            return CaptureObserved(
                observations, currentSlots, graph, attestor, capturer);
        }

        internal static CapturedAnimationEvidence CaptureGraphForTests(
            IReadOnlyList<Material> currentSlots,
            CommittedControllerGraphResult graph,
            IPlatformAnimatorBindings bindings,
            AlphaMaterialAttestor attestor,
            ClosedAlphaMaterialCapturer capturer)
        {
            return CaptureGraph(
                currentSlots, graph, bindings, attestor, capturer);
        }

        private static CapturedAnimationEvidence CaptureGraph(
            IReadOnlyList<Material> currentSlots,
            CommittedControllerGraphResult graph,
            IPlatformAnimatorBindings bindings,
            AlphaMaterialAttestor attestor,
            ClosedAlphaMaterialCapturer capturer)
        {
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
                observations, currentSlots, graph, attestor, capturer);
        }

        private static CapturedAnimationEvidence CaptureObserved(
            IReadOnlyList<LiveClipObservation> observations,
            IReadOnlyList<Material> currentSlots,
            CommittedControllerGraphResult graph,
            AlphaMaterialAttestor attestor,
            ClosedAlphaMaterialCapturer capturer)
        {
            if (observations == null)
                throw new ArgumentNullException(nameof(observations));
            if (currentSlots == null)
                throw new ArgumentNullException(nameof(currentSlots));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (attestor == null) throw new ArgumentNullException(nameof(attestor));
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

            CapturedAnimationEvidence Failed()
            {
                return new CapturedAnimationEvidence(
                    false,
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
                    return Failed();
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
                            binding.PropertyName, out var slot))
                    {
                        continue;
                    }

                    if (slot >= currentSlots.Count) return Failed();
                    foreach (var value in binding.Values)
                    {
                        if (!(value is Material material) ||
                            !TryAdmit(material, out _))
                        {
                            return Failed();
                        }
                    }
                }
            }

            var families = new CapturedAlphaMaterialFamily[admitted.Count];
            var requests = new MaterialEvidenceRequest[admitted.Count];
            for (var index = 0; index < admitted.Count; index++)
            {
                if (!attestor(admitted[index], out families[index], out requests[index]) ||
                    requests[index] == null)
                {
                    return Failed();
                }
            }

            var closedRequest = MaterialEvidenceRequest.Combine(requests);
            if (!capturer(
                    admitted,
                    families,
                    closedRequest,
                    out var capturedMaterials))
            {
                return Failed();
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

            return new CapturedAnimationEvidence(
                true,
                closedRequest,
                clips,
                new List<CapturedAlphaMaterial>(capturedMaterials),
                currentMaterialIndices,
                hasUnnormalizedDirectBlendTree,
                hasAdditiveLayer);
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
