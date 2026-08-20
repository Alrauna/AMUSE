using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Alrauna.Amuse.Research.Census
{
    /// <summary>
    /// Stage two of the census: a pure function from private observations to
    /// records that carry no identity.
    /// <para>
    /// This is the trust boundary of the whole system, and it is a function
    /// precisely so that non-leakage can be proven by test rather than promised
    /// by an operator. It touches no Unity object, opens no file, and performs
    /// no network access. Its assembly cannot reference the engine at all.
    /// </para>
    /// <para>
    /// It takes no seed. Identity is ordinal and assigned by position in the
    /// input, so equal input gives identical output with no hash, salt, clock,
    /// GUID, or machine value involved. Ordinals are run-local by design: they
    /// exist to prevent disclosure in exported aggregates, not to enable
    /// longitudinal tracking of an individual avatar.
    /// </para>
    /// </summary>
    public static class CensusAnonymizer
    {
        public static AnonymizedCensus Anonymize(CensusObservationSet observations)
        {
            if (observations == null)
                throw new ArgumentNullException(nameof(observations));

            // Shader families are numbered across the whole run, because the
            // question they answer, how much of the corpus one unattested
            // family accounts for, is a corpus-level question. Materials are
            // numbered per avatar, because the question cross-avatar material
            // identity would answer is one the census must not be able to ask.
            var shaderFamilies = new FirstAppearanceOrdinals();

            var avatars = new AnonymizedAvatar[observations.Avatars.Count];
            for (var avatarIndex = 0; avatarIndex < avatars.Length; avatarIndex++)
            {
                avatars[avatarIndex] = AnonymizeAvatar(
                    observations.Avatars[avatarIndex],
                    avatarIndex + 1,
                    shaderFamilies);
            }

            return new AnonymizedCensus(avatars);
        }

        private static AnonymizedAvatar AnonymizeAvatar(
            ObservedAvatar avatar,
            int avatarOrdinal,
            FirstAppearanceOrdinals shaderFamilies)
        {
            var materials = new FirstAppearanceOrdinals();

            var renderers = new AnonymizedRenderer[avatar.Renderers.Count];
            for (var index = 0; index < renderers.Length; index++)
            {
                renderers[index] = AnonymizeRenderer(
                    avatar.Renderers[index],
                    avatarOrdinal,
                    index + 1,
                    materials,
                    shaderFamilies);
            }

            return new AnonymizedAvatar(
                "Avatar-" + Pad(avatarOrdinal, 2),
                renderers);
        }

        private static AnonymizedRenderer AnonymizeRenderer(
            ObservedRenderer renderer,
            int avatarOrdinal,
            int rendererOrdinal,
            FirstAppearanceOrdinals materials,
            FirstAppearanceOrdinals shaderFamilies)
        {
            var submeshes = new AnonymizedSubmesh[renderer.Submeshes.Count];
            for (var index = 0; index < submeshes.Length; index++)
            {
                submeshes[index] = AnonymizeSubmesh(
                    renderer.Submeshes[index],
                    avatarOrdinal,
                    materials,
                    shaderFamilies);
            }

            return new AnonymizedRenderer(
                "Renderer-" + Pad(avatarOrdinal, 2) + "-" + Pad(rendererOrdinal, 3),
                renderer.Kind,
                renderer.Refusal,
                renderer.SubmeshCount,
                renderer.TriangleCount,
                submeshes);
        }

        private static AnonymizedSubmesh AnonymizeSubmesh(
            ObservedSubmesh submesh,
            int avatarOrdinal,
            FirstAppearanceOrdinals materials,
            FirstAppearanceOrdinals shaderFamilies)
        {
            string materialId = null;
            string shaderFamily = null;

            if (submesh.HasMaterial)
            {
                var materialOrdinal = materials.Ordinal(MaterialKey(submesh));
                materialId =
                    "Material-" + Pad(avatarOrdinal, 2) + "-" + Pad(materialOrdinal, 3);
                shaderFamily = ShaderFamily(submesh, shaderFamilies);
            }

            return new AnonymizedSubmesh(
                submesh.SubmeshIndex,
                submesh.MaterialSlotIndex,
                submesh.HasMaterial,
                materialId,
                shaderFamily,
                submesh.AlphaFailure,
                submesh.Disposition,
                submesh.TriangleCount,
                submesh.ProvenOpaqueTriangleCount,
                submesh.MustRemainTransparentTriangleCount,
                submesh.UnknownTriangleCount);
        }

        private static string ShaderFamily(
            ObservedSubmesh submesh,
            FirstAppearanceOrdinals shaderFamilies)
        {
            switch (submesh.ShaderFamilyAttestation)
            {
                case ShaderFamilyAttestation.Poiyomi:
                    return "Poiyomi";
                case ShaderFamilyAttestation.LilToon:
                    return "LilToon";
                case ShaderFamilyAttestation.None:
                    return "UnknownFamily-"
                        + Letters(shaderFamilies.Ordinal(
                            submesh.ShaderName ?? string.Empty));
                default:
                    // Unreachable while the enum is what
                    // CensusCategorySnapshotTests pins. Named rather than folded
                    // into an existing family, because a stopped run is better
                    // than a silent miscount.
                    throw new ArgumentOutOfRangeException(
                        nameof(submesh),
                        "Unhandled shader family attestation: "
                        + submesh.ShaderFamilyAttestation);
            }
        }

        /// <summary>
        /// The material's observed identity, used only as a lookup key. It is
        /// never emitted; the ordinal it maps to is.
        /// </summary>
        private static string MaterialKey(ObservedSubmesh submesh)
        {
            return (submesh.MaterialAssetGuid ?? string.Empty)
                + " " + (submesh.MaterialAssetPath ?? string.Empty)
                + " " + (submesh.MaterialName ?? string.Empty);
        }

        private static string Pad(int ordinal, int width)
        {
            return ordinal.ToString(
                new string('0', width),
                CultureInfo.InvariantCulture);
        }

        /// <summary>Bijective base 26: 1 is A, 26 is Z, 27 is AA.</summary>
        private static string Letters(int ordinal)
        {
            var letters = new StringBuilder();
            while (ordinal > 0)
            {
                ordinal--;
                letters.Insert(0, (char)('A' + (ordinal % 26)));
                ordinal /= 26;
            }

            return letters.ToString();
        }

        /// <summary>
        /// Assigns 1-based ordinals in order of first appearance. Backed by a
        /// list rather than a dictionary so that ordinals can never depend on
        /// hash-table iteration order, which is the one place determinism would
        /// otherwise be easy to lose. The populations are per-avatar materials
        /// and per-run shader families, so the linear scan is not worth
        /// removing.
        /// </summary>
        private sealed class FirstAppearanceOrdinals
        {
            private readonly List<string> _keys = new List<string>();

            internal int Ordinal(string key)
            {
                var index = _keys.IndexOf(key);
                if (index < 0)
                {
                    _keys.Add(key);
                    index = _keys.Count - 1;
                }

                return index + 1;
            }
        }
    }
}
