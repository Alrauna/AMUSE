using System;
using System.Collections.Generic;

namespace Alrauna.Amuse.Research.Census
{
    /// <summary>
    /// Tier 1: one submesh as the collector observed it.
    /// <para>
    /// Tier 1 deliberately carries real identifiers. A census anomaly that
    /// cannot be traced back to a concrete material is not debuggable, so this
    /// is the record that keeps the material name, path, GUID, and raw shader
    /// name. That is also why tier 1 never leaves the private run: nothing in
    /// this repository writes it anywhere, and
    /// <see cref="CensusAnonymizer"/> is the only supported way to derive
    /// something that may.
    /// </para>
    /// </summary>
    public sealed class ObservedSubmesh
    {
        public int SubmeshIndex { get; }
        public int MaterialSlotIndex { get; }
        public bool HasMaterial { get; }

        /// <summary>Identifying. Tier 1 only.</summary>
        public string MaterialName { get; }

        /// <summary>Identifying. Tier 1 only.</summary>
        public string MaterialAssetPath { get; }

        /// <summary>Identifying. Tier 1 only.</summary>
        public string MaterialAssetGuid { get; }

        /// <summary>Identifying. Tier 1 only.</summary>
        public string ShaderName { get; }

        public ShaderFamilyAttestation ShaderFamilyAttestation { get; }
        public AlphaResolutionFailure AlphaFailure { get; }
        public SeparationDisposition Disposition { get; }

        public int TriangleCount { get; }
        public int ProvenOpaqueTriangleCount { get; }
        public int MustRemainTransparentTriangleCount { get; }
        public int UnknownTriangleCount { get; }

        public ObservedSubmesh(
            int submeshIndex,
            int materialSlotIndex,
            bool hasMaterial,
            string materialName,
            string materialAssetPath,
            string materialAssetGuid,
            string shaderName,
            ShaderFamilyAttestation shaderFamilyAttestation,
            AlphaResolutionFailure alphaFailure,
            SeparationDisposition disposition,
            int triangleCount,
            int provenOpaqueTriangleCount,
            int mustRemainTransparentTriangleCount,
            int unknownTriangleCount)
        {
            CensusGuard.NotNegative(submeshIndex, nameof(submeshIndex));
            CensusGuard.NotNegative(materialSlotIndex, nameof(materialSlotIndex));
            CensusGuard.NotNegative(triangleCount, nameof(triangleCount));
            CensusGuard.NotNegative(
                provenOpaqueTriangleCount, nameof(provenOpaqueTriangleCount));
            CensusGuard.NotNegative(
                mustRemainTransparentTriangleCount,
                nameof(mustRemainTransparentTriangleCount));
            CensusGuard.NotNegative(unknownTriangleCount, nameof(unknownTriangleCount));
            CensusGuard.Defined(shaderFamilyAttestation, nameof(shaderFamilyAttestation));
            CensusGuard.Defined(alphaFailure, nameof(alphaFailure));
            CensusGuard.Defined(disposition, nameof(disposition));

            // The first invariant of the harness: every triangle in a submesh
            // has exactly one outcome. A census that can hold a record failing
            // this cannot be trusted to count.
            var classified =
                provenOpaqueTriangleCount
                + mustRemainTransparentTriangleCount
                + unknownTriangleCount;
            if (classified != triangleCount)
            {
                throw new ArgumentException(
                    "Triangle outcomes must account for every triangle in the submesh.",
                    nameof(triangleCount));
            }

            SubmeshIndex = submeshIndex;
            MaterialSlotIndex = materialSlotIndex;
            HasMaterial = hasMaterial;
            MaterialName = materialName;
            MaterialAssetPath = materialAssetPath;
            MaterialAssetGuid = materialAssetGuid;
            ShaderName = shaderName;
            ShaderFamilyAttestation = shaderFamilyAttestation;
            AlphaFailure = alphaFailure;
            Disposition = disposition;
            TriangleCount = triangleCount;
            ProvenOpaqueTriangleCount = provenOpaqueTriangleCount;
            MustRemainTransparentTriangleCount = mustRemainTransparentTriangleCount;
            UnknownTriangleCount = unknownTriangleCount;
        }
    }

    /// <summary>
    /// Tier 1: one renderer as the collector observed it.
    /// <para>
    /// <see cref="SubmeshCount"/> and <see cref="TriangleCount"/> are nullable
    /// on purpose. When a refusal leaves no reachable mesh — an unsupported
    /// renderer type, a missing mesh — the honest record is that the count is
    /// unknown. Writing <c>0</c> there understates avatar complexity and
    /// overstates coverage everywhere downstream, and is the single most likely
    /// miscount in the whole system.
    /// </para>
    /// </summary>
    public sealed class ObservedRenderer
    {
        /// <summary>Identifying. Tier 1 only.</summary>
        public string HierarchyPath { get; }

        /// <summary>Identifying. Tier 1 only.</summary>
        public string GameObjectName { get; }

        /// <summary>
        /// Identifying in principle, since a third-party renderer type name is
        /// a raw string. Tier 1 only; <see cref="Kind"/> is what survives
        /// anonymization.
        /// </summary>
        public string RendererTypeName { get; }

        public RendererKind Kind { get; }
        public RendererRefusal Refusal { get; }

        /// <summary>Null when no mesh was reachable. Never zero for unknown.</summary>
        public int? SubmeshCount { get; }

        /// <summary>Null when no mesh was reachable. Never zero for unknown.</summary>
        public int? TriangleCount { get; }

        /// <summary>Empty for a refused renderer, which carries no plan.</summary>
        public IReadOnlyList<ObservedSubmesh> Submeshes { get; }

        public ObservedRenderer(
            string hierarchyPath,
            string gameObjectName,
            string rendererTypeName,
            RendererKind kind,
            RendererRefusal refusal,
            int? submeshCount,
            int? triangleCount,
            IReadOnlyList<ObservedSubmesh> submeshes)
        {
            if (submeshes == null)
                throw new ArgumentNullException(nameof(submeshes));
            CensusGuard.Defined(kind, nameof(kind));
            CensusGuard.Defined(refusal, nameof(refusal));
            if (submeshCount.HasValue)
                CensusGuard.NotNegative(submeshCount.Value, nameof(submeshCount));
            if (triangleCount.HasValue)
                CensusGuard.NotNegative(triangleCount.Value, nameof(triangleCount));

            var copied = new ObservedSubmesh[submeshes.Count];
            for (var index = 0; index < submeshes.Count; index++)
            {
                copied[index] = submeshes[index]
                    ?? throw new ArgumentNullException(nameof(submeshes));
            }

            if (refusal == RendererRefusal.None)
            {
                // A renderer AMUSE analyzed successfully has a reachable mesh by
                // construction, so an unknown count here is a collector bug
                // rather than an observation.
                if (!submeshCount.HasValue || !triangleCount.HasValue)
                {
                    throw new ArgumentException(
                        "An analyzed renderer has a reachable mesh and known counts.",
                        nameof(refusal));
                }

                if (submeshCount.Value != copied.Length)
                {
                    throw new ArgumentException(
                        "An analyzed renderer records one submesh per submesh count.",
                        nameof(submeshCount));
                }

                var summed = 0;
                foreach (var submesh in copied)
                    summed += submesh.TriangleCount;

                if (summed != triangleCount.Value)
                {
                    throw new ArgumentException(
                        "Submesh triangles must account for every triangle in the mesh.",
                        nameof(triangleCount));
                }
            }
            else if (copied.Length != 0)
            {
                throw new ArgumentException(
                    "A refused renderer carries no plan and therefore no submesh records.",
                    nameof(submeshes));
            }

            HierarchyPath = hierarchyPath;
            GameObjectName = gameObjectName;
            RendererTypeName = rendererTypeName;
            Kind = kind;
            Refusal = refusal;
            SubmeshCount = submeshCount;
            TriangleCount = triangleCount;
            Submeshes = Array.AsReadOnly(copied);
        }
    }

    /// <summary>
    /// Tier 1: one avatar as the collector observed it. Identifying throughout,
    /// by design; see <see cref="ObservedSubmesh"/>.
    /// </summary>
    public sealed class ObservedAvatar
    {
        /// <summary>Identifying. Tier 1 only.</summary>
        public string AvatarName { get; }

        /// <summary>Identifying. Tier 1 only.</summary>
        public string CreatorName { get; }

        /// <summary>Identifying. Tier 1 only.</summary>
        public string AssetPath { get; }

        /// <summary>Identifying. Tier 1 only.</summary>
        public string AssetGuid { get; }

        public IReadOnlyList<ObservedRenderer> Renderers { get; }

        public ObservedAvatar(
            string avatarName,
            string creatorName,
            string assetPath,
            string assetGuid,
            IReadOnlyList<ObservedRenderer> renderers)
        {
            if (renderers == null)
                throw new ArgumentNullException(nameof(renderers));

            var copied = new ObservedRenderer[renderers.Count];
            for (var index = 0; index < renderers.Count; index++)
            {
                copied[index] = renderers[index]
                    ?? throw new ArgumentNullException(nameof(renderers));
            }

            AvatarName = avatarName;
            CreatorName = creatorName;
            AssetPath = assetPath;
            AssetGuid = assetGuid;
            Renderers = Array.AsReadOnly(copied);
        }
    }

    /// <summary>
    /// Tier 1: everything one census run observed. The root the anonymizer
    /// consumes; avatar order here is what fixes every ordinal downstream.
    /// </summary>
    public sealed class CensusObservationSet
    {
        public IReadOnlyList<ObservedAvatar> Avatars { get; }

        public CensusObservationSet(IReadOnlyList<ObservedAvatar> avatars)
        {
            if (avatars == null)
                throw new ArgumentNullException(nameof(avatars));

            var copied = new ObservedAvatar[avatars.Count];
            for (var index = 0; index < avatars.Count; index++)
            {
                copied[index] = avatars[index]
                    ?? throw new ArgumentNullException(nameof(avatars));
            }

            Avatars = Array.AsReadOnly(copied);
        }
    }
}
