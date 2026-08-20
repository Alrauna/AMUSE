using System;
using System.Collections.Generic;

namespace Alrauna.Amuse.Research.Census
{
    /// <summary>
    /// Tier 2: one submesh, stripped of identity.
    /// <para>
    /// Everything here is an enum, a number, or an ordinal identity string.
    /// <see cref="ShaderFamily"/> is the only free-form string and is drawn
    /// from a closed vocabulary the anonymizer controls.
    /// </para>
    /// </summary>
    public sealed class AnonymizedSubmesh
    {
        public int SubmeshIndex { get; }
        public int MaterialSlotIndex { get; }
        public bool HasMaterial { get; }

        /// <summary>
        /// Avatar-scoped ordinal identity, as <c>Material-NN-MMM</c>, so
        /// cross-avatar asset sharing is not recorded — shared-asset patterns
        /// identify purchased assets and their creators.
        /// <para>
        /// Null when the slot holds no material. An empty slot has no material,
        /// and inventing an identity for one would create a distinct material
        /// that does not exist.
        /// </para>
        /// </summary>
        public string MaterialId { get; }

        /// <summary>
        /// <c>Poiyomi</c>, <c>LilToon</c>, or <c>UnknownFamily-A</c>, … Named
        /// families are public products. Every other family is grouped
        /// anonymously, so a report can say how much of the corpus one
        /// unattested family accounts for — which sizes the next adapter —
        /// without disclosing a shader name that may be private or custom.
        /// Null when the slot holds no material.
        /// </summary>
        public string ShaderFamily { get; }

        public AlphaResolutionFailure AlphaFailure { get; }
        public SeparationDisposition Disposition { get; }

        public int TriangleCount { get; }
        public int ProvenOpaqueTriangleCount { get; }
        public int MustRemainTransparentTriangleCount { get; }
        public int UnknownTriangleCount { get; }

        public AnonymizedSubmesh(
            int submeshIndex,
            int materialSlotIndex,
            bool hasMaterial,
            string materialId,
            string shaderFamily,
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
            CensusGuard.Defined(alphaFailure, nameof(alphaFailure));
            CensusGuard.Defined(disposition, nameof(disposition));

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

            // A material and its anonymized identity arrive together or not at
            // all. Half a material is not an observation the census can hold:
            // an identity without a slot invents a material that does not
            // exist, and a slot without one leaves every material-keyed
            // aggregate with a null key to trip over, far from the mistake.
            if (hasMaterial && (materialId == null || shaderFamily == null))
            {
                throw new ArgumentException(
                    "A submesh with a material carries both an anonymized "
                    + "identity and a shader family.",
                    nameof(hasMaterial));
            }

            if (!hasMaterial && (materialId != null || shaderFamily != null))
            {
                throw new ArgumentException(
                    "A submesh with no material carries neither an anonymized "
                    + "identity nor a shader family.",
                    nameof(hasMaterial));
            }

            SubmeshIndex = submeshIndex;
            MaterialSlotIndex = materialSlotIndex;
            HasMaterial = hasMaterial;
            MaterialId = materialId;
            ShaderFamily = shaderFamily;
            AlphaFailure = alphaFailure;
            Disposition = disposition;
            TriangleCount = triangleCount;
            ProvenOpaqueTriangleCount = provenOpaqueTriangleCount;
            MustRemainTransparentTriangleCount = mustRemainTransparentTriangleCount;
            UnknownTriangleCount = unknownTriangleCount;
        }
    }

    /// <summary>
    /// Tier 2: one renderer, stripped of identity. <see cref="Kind"/> replaces
    /// the observed type name, so no raw type string survives anonymization.
    /// </summary>
    public sealed class AnonymizedRenderer
    {
        /// <summary>Avatar-scoped ordinal identity, as <c>Renderer-NN-MMM</c>.</summary>
        public string Id { get; }

        public RendererKind Kind { get; }
        public RendererRefusal Refusal { get; }

        /// <summary>Null when no mesh was reachable. Never zero for unknown.</summary>
        public int? SubmeshCount { get; }

        /// <summary>Null when no mesh was reachable. Never zero for unknown.</summary>
        public int? TriangleCount { get; }

        public IReadOnlyList<AnonymizedSubmesh> Submeshes { get; }

        public AnonymizedRenderer(
            string id,
            RendererKind kind,
            RendererRefusal refusal,
            int? submeshCount,
            int? triangleCount,
            IReadOnlyList<AnonymizedSubmesh> submeshes)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));
            if (submeshes == null)
                throw new ArgumentNullException(nameof(submeshes));
            CensusGuard.Defined(kind, nameof(kind));
            CensusGuard.Defined(refusal, nameof(refusal));
            if (submeshCount.HasValue)
                CensusGuard.NotNegative(submeshCount.Value, nameof(submeshCount));
            if (triangleCount.HasValue)
                CensusGuard.NotNegative(triangleCount.Value, nameof(triangleCount));

            var copied = new AnonymizedSubmesh[submeshes.Count];
            for (var index = 0; index < submeshes.Count; index++)
            {
                copied[index] = submeshes[index]
                    ?? throw new ArgumentNullException(nameof(submeshes));
            }

            if (refusal != RendererRefusal.None && copied.Length != 0)
            {
                throw new ArgumentException(
                    "A refused renderer carries no plan and therefore no submesh records.",
                    nameof(submeshes));
            }

            Id = id;
            Kind = kind;
            Refusal = refusal;
            SubmeshCount = submeshCount;
            TriangleCount = triangleCount;
            Submeshes = Array.AsReadOnly(copied);
        }
    }

    /// <summary>Tier 2: one avatar, stripped of identity.</summary>
    public sealed class AnonymizedAvatar
    {
        /// <summary>Run-local ordinal identity, as <c>Avatar-NN</c>.</summary>
        public string Id { get; }

        public IReadOnlyList<AnonymizedRenderer> Renderers { get; }

        public AnonymizedAvatar(string id, IReadOnlyList<AnonymizedRenderer> renderers)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));
            if (renderers == null)
                throw new ArgumentNullException(nameof(renderers));

            var copied = new AnonymizedRenderer[renderers.Count];
            for (var index = 0; index < renderers.Count; index++)
            {
                copied[index] = renderers[index]
                    ?? throw new ArgumentNullException(nameof(renderers));
            }

            Id = id;
            Renderers = Array.AsReadOnly(copied);
        }
    }

    /// <summary>
    /// Tier 2: a whole run, stripped of identity. Ordinals are run-local and
    /// carry no meaning across runs; only aggregates compare longitudinally.
    /// </summary>
    public sealed class AnonymizedCensus
    {
        public IReadOnlyList<AnonymizedAvatar> Avatars { get; }

        public AnonymizedCensus(IReadOnlyList<AnonymizedAvatar> avatars)
        {
            if (avatars == null)
                throw new ArgumentNullException(nameof(avatars));

            var copied = new AnonymizedAvatar[avatars.Count];
            for (var index = 0; index < avatars.Count; index++)
            {
                copied[index] = avatars[index]
                    ?? throw new ArgumentNullException(nameof(avatars));
            }

            Avatars = Array.AsReadOnly(copied);
        }
    }
}
