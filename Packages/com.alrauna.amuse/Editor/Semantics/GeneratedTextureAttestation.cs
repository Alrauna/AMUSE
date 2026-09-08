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
        Anatawa12AvatarOptimizer
    }

    /// <summary>
    /// Attests whether a texture was created by a known generated texture producer.
    /// </summary>
    internal static class GeneratedTextureAttestation
    {
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

            var mainType = mainAsset.GetType();
            var isNdmfContainer = mainType.Name == "SubAssetContainer" ||
                                  mainType.FullName == "nadena.dev.ndmf.runtime.SubAssetContainer";
            if (!isNdmfContainer)
            {
                return false;
            }

            var textureName = texture.name ?? string.Empty;
            var isAaoTexture = textureName.EndsWith(" (AAO UV Packed)") ||
                               textureName.StartsWith("AAO Monotone ");
            if (!isAaoTexture)
            {
                return false;
            }

            producer = GeneratedTextureProducer.Anatawa12AvatarOptimizer;
            return true;
        }
    }
}
