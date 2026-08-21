using System;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using UnityEngine;
using Census = Alrauna.Amuse.Research.Census;

namespace Alrauna.Amuse.Research.Collection
{
    /// <summary>
    /// The census vocabulary's AMUSE-facing half: total mappings from AMUSE's
    /// internal enums onto their census mirrors.
    /// <para>
    /// No mapping has a default arm. An AMUSE value with no census counterpart
    /// throws rather than being folded into an existing category, because a
    /// stopped run is better than a silent miscount - the same rule
    /// <c>CensusAnonymizer.ShaderFamily</c> already applies on the other side of
    /// the pipeline.
    /// </para>
    /// </summary>
    internal static class CensusVocabulary
    {
        internal static Census.RendererRefusal ToCensus(
            RendererAnalysisRefusal refusal)
        {
            switch (refusal)
            {
                case RendererAnalysisRefusal.None:
                    return Census.RendererRefusal.None;
                case RendererAnalysisRefusal.UnsupportedRendererType:
                    return Census.RendererRefusal.UnsupportedRendererType;
                case RendererAnalysisRefusal.MaterialPropertyOverridesPresent:
                    return Census.RendererRefusal
                        .MaterialPropertyOverridesPresent;
                case RendererAnalysisRefusal.MissingMesh:
                    return Census.RendererRefusal.MissingMesh;
                case RendererAnalysisRefusal.UnprovenMaterialSlotMapping:
                    return Census.RendererRefusal.UnprovenMaterialSlotMapping;
                case RendererAnalysisRefusal.UnsupportedTopology:
                    return Census.RendererRefusal.UnsupportedTopology;
                case RendererAnalysisRefusal.MalformedMeshData:
                    return Census.RendererRefusal.MalformedMeshData;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(refusal),
                        "Unmapped AMUSE renderer refusal: " + refusal);
            }
        }

        internal static Census.AlphaResolutionFailure ToCensus(
            AlphaResolutionFailure failure)
        {
            switch (failure)
            {
                case AlphaResolutionFailure.None:
                    return Census.AlphaResolutionFailure.None;
                case AlphaResolutionFailure.SemanticsUnknown:
                    return Census.AlphaResolutionFailure.SemanticsUnknown;
                case AlphaResolutionFailure.UnsupportedMultiplier:
                    return Census.AlphaResolutionFailure.UnsupportedMultiplier;
                case AlphaResolutionFailure.UnsupportedUvMapping:
                    return Census.AlphaResolutionFailure.UnsupportedUvMapping;
                case AlphaResolutionFailure.UnsupportedSampling:
                    return Census.AlphaResolutionFailure.UnsupportedSampling;
                case AlphaResolutionFailure.MissingTextureEvidence:
                    return Census.AlphaResolutionFailure.MissingTextureEvidence;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(failure),
                        "Unmapped AMUSE alpha resolution failure: " + failure);
            }
        }

        internal static Census.SeparationDisposition ToCensus(
            SubmeshSeparationDisposition disposition)
        {
            switch (disposition)
            {
                case SubmeshSeparationDisposition.Unchanged:
                    return Census.SeparationDisposition.Unchanged;
                case SubmeshSeparationDisposition.WhollyOpaqueCandidate:
                    return Census.SeparationDisposition.WhollyOpaqueCandidate;
                case SubmeshSeparationDisposition.Split:
                    return Census.SeparationDisposition.Split;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(disposition),
                        "Unmapped AMUSE separation disposition: " + disposition);
            }
        }

        /// <summary>
        /// The one mapping that is deliberately not one-to-one. Everything AMUSE
        /// does not analyze collapses to <c>Other</c> rather than throwing,
        /// because an unsupported renderer type is an observation the census
        /// exists to count, not a defect.
        /// </summary>
        internal static Census.RendererKind KindOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer)
                return Census.RendererKind.SkinnedMeshRenderer;
            if (renderer is MeshRenderer)
                return Census.RendererKind.MeshRenderer;

            return Census.RendererKind.Other;
        }
    }
}
