using System;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Identifies a known producer of generated textures.
    /// </summary>
    internal enum GeneratedTextureProducer
    {
        None,
        Anatawa12AvatarOptimizer,
        VrcFuryBuildContainer,
        LimitexTextureCompressor,
    }

    /// <summary>
    /// Attests whether a texture was created by a known generated texture producer.
    /// </summary>
    internal static class GeneratedTextureAttestation
    {
        /// <summary>
        /// The pinned container object of the VRCFury play-mode build path.
        /// The host clones every avatar texture into one persisted container
        /// of this type and name before any NDMF plugin runs. The clones keep
        /// their original names, so a texture name marker cannot characterize
        /// them. The container object carries the producer identity instead.
        /// </summary>
        private const string VrcFuryContainerTypeFullName =
            "VF.Utils.BinaryContainer";
        private const string VrcFuryContainerObjectName = "VRCFury Other";

        /// <summary>
        /// Attempts to identify the producer of a generated texture.
        /// </summary>
        internal static bool TryIdentifyProducer(
            Texture texture,
            out GeneratedTextureProducer producer)
        {
            producer = GeneratedTextureProducer.None;
            if (texture == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (!AssetDatabase.IsSubAsset(texture))
            {
                return false;
            }

            var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
            if (mainAsset == null)
            {
                return false;
            }

            if (mainAsset.GetType() == typeof(nadena.dev.ndmf.runtime.SubAssetContainer))
            {
                var textureName = texture.name ?? string.Empty;
                var isAaoTexture =
                    textureName.EndsWith(
                        AaoAtlasTextureAttestation.AtlasNameSuffix,
                        StringComparison.Ordinal) ||
                    textureName.StartsWith(
                        AaoAtlasTextureAttestation.MonotoneNamePrefix,
                        StringComparison.Ordinal);
                if (isAaoTexture)
                {
                    producer = GeneratedTextureProducer.Anatawa12AvatarOptimizer;
                    return true;
                }

                // Persisted replacement copies (spec 2026-10-04, section 6). NDMF
                // persists every in-memory copy of the attested compressor as a
                // sub-asset of one of these containers after every plugin phase.
                // The copy keeps the source name plus a pinned output suffix, so
                // the suffix plus the admitted producer version characterizes the
                // shape. Bake outputs named "_baked" persist the same way.
                var isPersistedReplacement =
                    textureName.EndsWith(
                        ReplacementTextureAttestation.ReplacementNameSuffix,
                        StringComparison.Ordinal) ||
                    textureName.EndsWith(
                        ReplacementTextureAttestation.BakedOutputSuffix,
                        StringComparison.Ordinal);
                if (isPersistedReplacement &&
                    ReplacementTextureAttestation.TryReadInstalledProducerVersion(
                        out var version) &&
                    ReplacementTextureAttestation.IsVersionAdmitted(version))
                {
                    producer = GeneratedTextureProducer.LimitexTextureCompressor;
                    return true;
                }

                return false;
            }

            if (mainAsset.GetType().FullName == VrcFuryContainerTypeFullName &&
                mainAsset.name == VrcFuryContainerObjectName)
            {
                producer = GeneratedTextureProducer.VrcFuryBuildContainer;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to identify any texture the generated capture route may
        /// serve: a container-backed producer texture, an admitted
        /// replacement copy minted by Limitex Texture Compressor, or a
        /// shape-admitted in-memory Avatar Optimizer atlas.
        /// </summary>
        internal static bool TryIdentifyRouteTexture(
            Texture texture, out GeneratedTextureProducer producer)
        {
            producer = GeneratedTextureProducer.None;
            if (texture == null)
            {
                return false;
            }
            if (TryIdentifyProducer(texture, out producer))
            {
                return true;
            }
            if (texture is Texture2D copy &&
                ReplacementTextureAttestation.TryIdentifyReplacement(
                    copy, out _))
            {
                producer = GeneratedTextureProducer.LimitexTextureCompressor;
                return true;
            }
            // The in-memory Avatar Optimizer atlas takes the generated
            // route on the texture-only shape predicate. Every capture
            // reaches a route only after the identity gate admitted the
            // same object under the stricter corroborated conjunct
            // (spec 2026-10-04, section 4.2), so this arm cannot widen
            // the gate.
            // Upstream path (spec 2026-10-04, section 8): an atlas that
            // Avatar Optimizer registers in the NDMF object registry, or
            // persists into a build container, is served by the persisted
            // branch above and retires this arm.
            if (texture is Texture2D atlas &&
                AaoAtlasTextureAttestation.TryIdentifyAtlasShape(atlas))
            {
                producer = GeneratedTextureProducer.Anatawa12AvatarOptimizer;
                return true;
            }
            producer = GeneratedTextureProducer.None;
            return false;
        }
    }
}
