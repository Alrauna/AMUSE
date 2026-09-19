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
                var isAaoTexture = textureName.EndsWith(" (AAO UV Packed)") ||
                                   textureName.StartsWith("AAO Monotone ");
                if (isAaoTexture)
                {
                    producer = GeneratedTextureProducer.Anatawa12AvatarOptimizer;
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
    }
}
