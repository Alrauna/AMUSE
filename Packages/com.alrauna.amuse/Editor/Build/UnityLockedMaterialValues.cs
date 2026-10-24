using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The <see cref="ILockedMaterialValues"/> view over one live material
    /// (A5.2). The view snapshots the saved serialization of the material
    /// once at construction. The build reads the locked clone through it
    /// while the clone still has the generated shader, so the renamed
    /// saved names stay reachable. The consistency check reads the
    /// finished clone through it after the writes. The view mints one
    /// opaque texture token per texture from the texture asset identity,
    /// or from the instance id for a texture without an asset, and
    /// resolves a token back to the texture for the plan applier.
    /// </summary>
    internal sealed class UnityLockedMaterialValues : ILockedMaterialValues
    {
        private const string RenderTypeTagName = "RenderType";
        private const string VrcFallbackTagName = "VRCFallback";
        private const string OriginalKeywordsTagName = "OriginalKeywords";
        private const string GuidTokenPrefix = "guid:";
        private const string IdTokenPrefix = "id:";

        private readonly Material material;
        private readonly Dictionary<string, float> floats =
            new Dictionary<string, float>();
        private readonly Dictionary<string, Vector4> vectors =
            new Dictionary<string, Vector4>();
        private readonly Dictionary<string, Color> colors =
            new Dictionary<string, Color>();
        private readonly Dictionary<string, LockedTextureValue> textures =
            new Dictionary<string, LockedTextureValue>();
        private readonly Dictionary<string, string> overrideTags =
            new Dictionary<string, string>();
        private readonly string[] originalKeywords;

        internal UnityLockedMaterialValues(Material material)
        {
            this.material = material;
            SnapshotSavedProperties();
            originalKeywords = SplitKeywords(material.GetTag(
                OriginalKeywordsTagName, false, null));
        }

        /// <summary>
        /// Mints the identity token of one texture. The asset GUID is the
        /// stable identity of an imported texture. A runtime texture
        /// without an asset rides its instance id, which is stable inside
        /// one editor session.
        /// </summary>
        internal static string TextureTokenOf(Texture texture)
        {
            if (texture == null)
            {
                return null;
            }

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    texture, out var guid, out long _))
            {
                return GuidTokenPrefix + guid;
            }

            return IdTokenPrefix + texture.GetInstanceID();
        }

        /// <summary>
        /// Resolves one token back to the texture. Null returns null. An
        /// unresolvable token returns null, because a missing texture
        /// reads as an empty slot.
        /// </summary>
        internal static Texture TextureFromToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            if (token.StartsWith(GuidTokenPrefix, StringComparison.Ordinal))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(
                    token.Substring(GuidTokenPrefix.Length));
                return string.IsNullOrEmpty(assetPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
            }

            if (token.StartsWith(IdTokenPrefix, StringComparison.Ordinal) &&
                int.TryParse(
                    token.Substring(IdTokenPrefix.Length),
                    out var instanceId))
            {
                return EditorUtility.InstanceIDToObject(instanceId)
                    as Texture;
            }

            return null;
        }

        public bool TryGetFloat(string propertyName, out float value)
        {
            return floats.TryGetValue(propertyName, out value);
        }

        public bool TryGetVector(string propertyName, out Vector4 value)
        {
            if (vectors.TryGetValue(propertyName, out value))
            {
                return true;
            }

            // Serialization shapes without a separate vectors container
            // keep vector values in the colors storage.
            if (colors.TryGetValue(propertyName, out var color))
            {
                value = new Vector4(
                    color.r, color.g, color.b, color.a);
                return true;
            }

            return false;
        }

        public bool TryGetColor(string propertyName, out Color value)
        {
            if (colors.TryGetValue(propertyName, out value))
            {
                return true;
            }

            if (vectors.TryGetValue(propertyName, out var vector))
            {
                value = new Color(
                    vector.x, vector.y, vector.z, vector.w);
                return true;
            }

            return false;
        }

        public bool TryGetTexture(
            string propertyName, out LockedTextureValue value)
        {
            if (textures.TryGetValue(propertyName, out value))
            {
                return true;
            }

            // A declared texture slot of the shader without a saved texenv
            // entry reads as an empty slot, because the material renders
            // the shader default for it.
            if (material != null && material.shader != null &&
                material.shader.FindPropertyIndex(propertyName) >= 0 &&
                material.shader.GetPropertyType(
                    material.shader.FindPropertyIndex(propertyName)) ==
                UnityEngine.Rendering.ShaderPropertyType.Texture)
            {
                value = new LockedTextureValue(
                    null, 0, 0, new Vector2(1f, 1f), new Vector2(0f, 0f));
                return true;
            }

            return false;
        }

        public int RenderQueue => material != null ? material.renderQueue : 0;

        public string RenderTypeTag => OverrideTagOrNull(RenderTypeTagName);

        public string VrcFallbackTag => OverrideTagOrNull(VrcFallbackTagName);

        public IReadOnlyList<string> OriginalKeywordsTag => originalKeywords;

        public IReadOnlyList<string> Keywords =>
            material != null
                ? material.shaderKeywords
                : Array.Empty<string>();

        public bool ProjectIsLinear =>
            PlayerSettings.colorSpace == ColorSpace.Linear;

        /// <summary>
        /// The material's own override tag. The read uses the serialized
        /// tag map, because A5.10 moves the override tag, not a tag the
        /// shader itself declares.
        /// </summary>
        private string OverrideTagOrNull(string tagName)
        {
            return overrideTags.TryGetValue(tagName, out var value)
                ? value
                : null;
        }

        private void SnapshotSavedProperties()
        {
            if (material == null)
            {
                return;
            }

            using var serialized = new SerializedObject(material);
            ReadOverrideTags(serialized);
            ReadTexEnvs(serialized);
            ReadFloats(serialized);
            ReadInts(serialized);
            ReadColors(serialized);
            ReadVectors(serialized);
        }

        private void ReadOverrideTags(SerializedObject serialized)
        {
            var map = serialized.FindProperty("stringTagMap");
            if (map == null)
            {
                return;
            }

            for (var index = 0; index < map.arraySize; index++)
            {
                var element = map.GetArrayElementAtIndex(index);
                var first = element.FindPropertyRelative("first");
                var second = element.FindPropertyRelative("second");
                if (first != null && second != null)
                {
                    overrideTags[first.stringValue] = second.stringValue;
                }
            }
        }

        private void ReadTexEnvs(SerializedObject serialized)
        {
            var texEnvs = serialized.FindProperty(
                "m_SavedProperties.m_TexEnvs");
            if (texEnvs == null)
            {
                return;
            }

            for (var index = 0; index < texEnvs.arraySize; index++)
            {
                var element = texEnvs.GetArrayElementAtIndex(index);
                var name = ElementKeyName(element);
                var second = element.FindPropertyRelative("second");
                if (name == null || second == null)
                {
                    continue;
                }

                var textureProperty =
                    second.FindPropertyRelative("m_Texture");
                var scaleProperty = second.FindPropertyRelative("m_Scale");
                var offsetProperty =
                    second.FindPropertyRelative("m_Offset");
                var texture =
                    textureProperty != null &&
                    textureProperty.propertyType ==
                    SerializedPropertyType.ObjectReference
                        ? textureProperty.objectReferenceValue as Texture
                        : null;
                textures[name] = new LockedTextureValue(
                    TextureTokenOf(texture),
                    texture != null ? texture.width : 0,
                    texture != null ? texture.height : 0,
                    scaleProperty != null
                        ? scaleProperty.vector2Value
                        : new Vector2(1f, 1f),
                    offsetProperty != null
                        ? offsetProperty.vector2Value
                        : new Vector2(0f, 0f));
            }
        }

        private void ReadFloats(SerializedObject serialized)
        {
            var serializedFloats = serialized.FindProperty(
                "m_SavedProperties.m_Floats");
            if (serializedFloats == null)
            {
                return;
            }

            for (var index = 0; index < serializedFloats.arraySize; index++)
            {
                var element =
                    serializedFloats.GetArrayElementAtIndex(index);
                var name = ElementKeyName(element);
                var second = element.FindPropertyRelative("second");
                if (name == null || second == null ||
                    second.propertyType != SerializedPropertyType.Float)
                {
                    continue;
                }

                floats[name] = second.floatValue;
            }
        }

        private void ReadInts(SerializedObject serialized)
        {
            var serializedInts = serialized.FindProperty(
                "m_SavedProperties.m_Ints");
            if (serializedInts == null)
            {
                return;
            }

            for (var index = 0; index < serializedInts.arraySize; index++)
            {
                var element = serializedInts.GetArrayElementAtIndex(index);
                var name = ElementKeyName(element);
                var second = element.FindPropertyRelative("second");
                if (name == null || second == null ||
                    second.propertyType != SerializedPropertyType.Integer)
                {
                    continue;
                }

                floats[name] = second.intValue;
            }
        }

        private void ReadColors(SerializedObject serialized)
        {
            var serializedColors = serialized.FindProperty(
                "m_SavedProperties.m_Colors");
            if (serializedColors == null)
            {
                return;
            }

            for (var index = 0; index < serializedColors.arraySize; index++)
            {
                var element =
                    serializedColors.GetArrayElementAtIndex(index);
                var name = ElementKeyName(element);
                var second = element.FindPropertyRelative("second");
                if (name == null || second == null ||
                    second.propertyType != SerializedPropertyType.Color)
                {
                    continue;
                }

                colors[name] = second.colorValue;
            }
        }

        private void ReadVectors(SerializedObject serialized)
        {
            var serializedVectors = serialized.FindProperty(
                "m_SavedProperties.m_Vectors");
            if (serializedVectors == null)
            {
                return;
            }

            for (var index = 0; index < serializedVectors.arraySize;
                 index++)
            {
                var element =
                    serializedVectors.GetArrayElementAtIndex(index);
                var name = ElementKeyName(element);
                var second = element.FindPropertyRelative("second");
                if (name == null || second == null ||
                    second.propertyType != SerializedPropertyType.Vector4)
                {
                    continue;
                }

                vectors[name] = second.vector4Value;
            }
        }

        /// <summary>
        /// The key of one saved-property element. Material serialized
        /// versions have carried the key both directly in "first" and
        /// inside a "first" struct under "name"; null when neither shape
        /// matches.
        /// </summary>
        private static string ElementKeyName(SerializedProperty element)
        {
            var first = element.FindPropertyRelative("first");
            if (first == null)
            {
                return null;
            }

            if (first.propertyType == SerializedPropertyType.String)
            {
                return first.stringValue;
            }

            var name = first.FindPropertyRelative("name");
            return name != null ? name.stringValue : null;
        }

        private static string[] SplitKeywords(string tag)
        {
            return string.IsNullOrEmpty(tag)
                ? Array.Empty<string>()
                : tag.Split(' ');
        }
    }
}
