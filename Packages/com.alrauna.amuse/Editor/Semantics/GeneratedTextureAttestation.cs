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

            var typeName = mainAsset.GetType().FullName ?? string.Empty;
            if (typeName.Contains("SubAssetContainer") ||
                path.Contains("ModularAvatar") ||
                path.Contains("nadena.dev.ndmf") ||
                texture.name.Contains("AAO"))
            {
                producer = GeneratedTextureProducer.Anatawa12AvatarOptimizer;
                return true;
            }

            // Also admit any ScriptableObject-backed sub-asset container in NDMF builds
            if (mainAsset is ScriptableObject)
            {
                producer = GeneratedTextureProducer.Anatawa12AvatarOptimizer;
                return true;
            }

            return false;
        }
    }
}
