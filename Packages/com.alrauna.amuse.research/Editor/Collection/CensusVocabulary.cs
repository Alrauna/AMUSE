using System;
using Alrauna.Amuse.Editor.Host;
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
    }
}
