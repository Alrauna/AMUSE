using System;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Attests the in-memory atlas shape of the Optimize Texture pass
    /// of Avatar Optimizer. Every conjunct must hold; any miss refuses.
    /// </summary>
    // Upstream path (spec 2026-10-04, section 8). A future Avatar
    // Optimizer release that improves the attestability of its
    // generated textures, for example by registering each atlas in the
    // NDMF object registry or by persisting atlases into a build
    // container, would let AMUSE delete this admission almost
    // entirely: the shape predicate and the corroboration conjunct
    // would retire together with the identity mint and the two arms
    // that consult them. The persisted-container branch in
    // GeneratedTextureAttestation already reads that shape. The
    // version seam left this list on 2026-10-25, when owner decision
    // Q8 dropped the version pin. Until that release arrives, this
    // comment is the whole upstream ask.
    internal static class AaoAtlasTextureAttestation
    {
        internal const string AtlasNameSuffix = " (AAO UV Packed)";
        internal const string MonotoneNamePrefix = "AAO Monotone ";

        /// <summary>
        /// The shape predicate, conjuncts C1 and C2 of spec 2026-10-04
        /// section 4.1. Owner decision Q8 of 2026-10-25 retired the
        /// admitted-version conjunct C3. The predicate is deliberately
        /// texture-only: the route selector and the fact readers
        /// consult it, while the identity gate demands the corroborated
        /// form below.
        /// </summary>
        internal static bool TryIdentifyAtlasShape(Texture2D texture)
        {
            if (texture == null)
            {
                return false;
            }
            // Conjunct 1: a live copy with no asset path.
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture)))
            {
                return false;
            }
            // Conjunct 2: the pinned name shape.
            var name = texture.name ?? string.Empty;
            return name.EndsWith(
                       AtlasNameSuffix, StringComparison.Ordinal) ||
                   name.StartsWith(
                       MonotoneNamePrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// The full admission, conjuncts C1, C2, and C4 of spec
        /// 2026-10-04 section 4.1. Owner decision Q8 of 2026-10-25
        /// retired the admitted-version conjunct C3. The slot's live
        /// material must corroborate the producer: the registry
        /// resolves it one hop to an asset-backed material, the pair
        /// the DuplicateAssets pass of Avatar Optimizer registers for
        /// every renderer material it clones.
        /// </summary>
        internal static bool TryIdentifyAtlas(
            Texture2D texture,
            Material originMaterial)
        {
            if (!TryIdentifyAtlasShape(texture))
            {
                return false;
            }
            // Conjunct 4: the origin material corroborates the producer.
            if (originMaterial == null)
            {
                return false;
            }
            if (!(RegisteredSourceIdentity.Resolve(originMaterial)
                    is Material resolved))
            {
                return false;
            }
            return !string.IsNullOrEmpty(
                AssetDatabase.GetAssetPath(resolved));
        }
    }
}
