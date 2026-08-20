using System.Collections.Generic;
using Alrauna.Amuse.Research.Census;

namespace Alrauna.Amuse.Research.Tests.Editor.Census
{
    /// <summary>
    /// Construction helpers for tier 1 records. Tier 1 has many fields because
    /// it is the debugging artifact, so tests that care about two of them would
    /// otherwise be unreadable. These live in the test assembly only; no
    /// production type gains a test-shaped constructor.
    /// </summary>
    internal static class CensusObservationBuilders
    {
        internal static ObservedSubmesh Submesh(
            int submeshIndex = 0,
            int materialSlotIndex = 0,
            bool hasMaterial = true,
            string materialName = "material",
            string materialAssetPath = "Assets/material.mat",
            string materialAssetGuid = "00000000000000000000000000000000",
            string shaderName = "Shader",
            ShaderFamilyAttestation attestation = ShaderFamilyAttestation.None,
            AlphaResolutionFailure alphaFailure = AlphaResolutionFailure.None,
            SeparationDisposition disposition = SeparationDisposition.Unchanged,
            int provenOpaque = 0,
            int mustRemainTransparent = 0,
            int unknown = 0)
        {
            return new ObservedSubmesh(
                submeshIndex,
                materialSlotIndex,
                hasMaterial,
                materialName,
                materialAssetPath,
                materialAssetGuid,
                shaderName,
                attestation,
                alphaFailure,
                disposition,
                provenOpaque + mustRemainTransparent + unknown,
                provenOpaque,
                mustRemainTransparent,
                unknown);
        }

        /// <summary>
        /// An analyzed renderer, with the mesh-level counts derived from the
        /// submeshes so a caller testing something else cannot accidentally
        /// violate the sum invariant.
        /// </summary>
        internal static ObservedRenderer Renderer(
            string hierarchyPath = "Avatar/Body",
            string gameObjectName = "Body",
            string rendererTypeName = "SkinnedMeshRenderer",
            RendererKind kind = RendererKind.SkinnedMeshRenderer,
            params ObservedSubmesh[] submeshes)
        {
            var triangleCount = 0;
            foreach (var submesh in submeshes)
                triangleCount += submesh.TriangleCount;

            return new ObservedRenderer(
                hierarchyPath,
                gameObjectName,
                rendererTypeName,
                kind,
                RendererRefusal.None,
                submeshes.Length,
                triangleCount,
                submeshes);
        }

        internal static ObservedRenderer RefusedRenderer(
            RendererRefusal refusal,
            string hierarchyPath = "Avatar/Body",
            string gameObjectName = "Body",
            string rendererTypeName = "SkinnedMeshRenderer",
            RendererKind kind = RendererKind.SkinnedMeshRenderer,
            int? submeshCount = null,
            int? triangleCount = null)
        {
            return new ObservedRenderer(
                hierarchyPath,
                gameObjectName,
                rendererTypeName,
                kind,
                refusal,
                submeshCount,
                triangleCount,
                new ObservedSubmesh[0]);
        }

        internal static ObservedAvatar Avatar(
            string avatarName = "avatar",
            string creatorName = "creator",
            string assetPath = "Assets/avatar.prefab",
            string assetGuid = "11111111111111111111111111111111",
            params ObservedRenderer[] renderers)
        {
            return new ObservedAvatar(
                avatarName,
                creatorName,
                assetPath,
                assetGuid,
                renderers);
        }

        internal static CensusObservationSet Set(params ObservedAvatar[] avatars)
        {
            return new CensusObservationSet(avatars);
        }

        internal static List<ObservedSubmesh> MutableSubmeshes(
            params ObservedSubmesh[] submeshes)
        {
            return new List<ObservedSubmesh>(submeshes);
        }
    }
}
