using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The AMUSE-owned in-memory reconstruction of one locked material onto
    /// its recorded original shader. It replaces the vendor unlock for the
    /// swapped source pair, so a build that admits locked materials runs no
    /// vendor call, writes no file, refreshes nothing, and leaves the
    /// on-disk lock state untouched.
    /// <para>
    /// The input is a name-identical clone of a locked material, so it
    /// carries the locked serialization and the locked override tags. The
    /// saved serialized properties are the only value source: the locked
    /// generated shader declares a reduced property table, so a reader that
    /// consulted the declared table could not see the plain names the
    /// reconstruction must write, and the typed getters of the locked
    /// material resolve the surviving plain values, not the suffixed ones.
    /// Values are written back through the typed material setters after the
    /// shader swap, so every downstream consumer reads them as ordinary
    /// declared properties of the restored original shader.
    /// </para>
    /// </summary>
    internal static class LockedMaterialReconstruction
    {
        // Vendor optimizer format, controller-extracted, fact 1 (keyword
        // tag): the lock writes the keyword list space-joined into this
        // tag, and the restore replaces the whole keyword set with it.
        private const string OriginalKeywordsTagName = "OriginalKeywords";

        // Vendor optimizer format, controller-extracted, fact 2 (rename
        // suffix): the suffix is the cleaned rename-suffix tag, falling
        // back to the material name.
        private const string RenameSuffixTagName = "thry_rename_suffix";

        // Vendor optimizer format, controller-extracted, fact 3 (rename
        // scope): a property is renamed only when its <property>Animated
        // override tag equals "2"; the tag is absent for every
        // duplicated-instead-of-renamed name, and such entries must stay
        // untouched exactly as the vendor unlock leaves them.
        private const string AnimatedTagSuffix = "Animated";
        private const string AnimatedRenameTagValue = "2";

        private const string NameTagMissingDetail =
            "the recorded original shader name tag is missing";

        private const string GuidTagMissingDetail =
            "the recorded original shader GUID tag is missing";

        private const string GuidUnresolvableDetail =
            "the recorded original shader GUID does not resolve to a " +
            "shader asset";

        private const string AttestationFailedDetail =
            "the resolved original shader did not pass attestation";

        private const string FlagPropertyMissingDetail =
            "the resolved original shader does not declare the " +
            "optimizer flag property";

        /// <summary>
        /// Reconstructs the unlocked material on <paramref name="clone"/>
        /// in memory. Every step must hold for a true answer; the first
        /// broken step refuses through <paramref name="refusalDetail"/>
        /// with a fixed sentence that never carries a path, a GUID, or a
        /// material name. The method resolves and loads the recorded
        /// original shader asset and nothing else external: it writes no
        /// file, mutates no asset, and triggers no refresh, so the next
        /// build of the same input starts from the same on-disk state.
        /// </summary>
        internal static bool TryApply(
            Material clone,
            Func<Shader, bool> originalShaderAttested,
            out string refusalDetail)
        {
            // The recorded identity is the only link back to the original
            // shader; the locked name prefix and the reduced declared
            // table make every other route ambiguous.
            var recordedName = clone.GetTag(
                LockedMaterialIdentity.OriginalShaderTagName, true, null);
            if (string.IsNullOrEmpty(recordedName))
            {
                refusalDetail = NameTagMissingDetail;
                return false;
            }

            var recordedGuid = clone.GetTag(
                LockedMaterialIdentity.OriginalShaderGuidTagName,
                true,
                null);
            if (string.IsNullOrEmpty(recordedGuid))
            {
                refusalDetail = GuidTagMissingDetail;
                return false;
            }

            // Read-only resolution through the AssetDatabase: the GUID is
            // the pinned binding the lock recorded, and the name alone is
            // not trusted for the asset lookup.
            var resolvedPath =
                AssetDatabase.GUIDToAssetPath(recordedGuid);
            var resolved = string.IsNullOrEmpty(resolvedPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Shader>(resolvedPath);
            if (resolved == null)
            {
                refusalDetail = GuidUnresolvableDetail;
                return false;
            }

            if (originalShaderAttested == null ||
                !originalShaderAttested(resolved))
            {
                refusalDetail = AttestationFailedDetail;
                return false;
            }

            // The saved serialized properties survive the swap; the typed
            // view does not exist until the original shader is assigned,
            // which is why the swap precedes every value move.
            clone.shader = resolved;

            RestoreKeywords(clone);

            MoveSuffixedSavedValues(clone, resolved);

            // The cleared flag is part of the verification contract, and a
            // shader without the property cannot carry it, so a missing
            // declaration refuses instead of writing an orphan value.
            if (!clone.HasProperty(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName))
            {
                refusalDetail = FlagPropertyMissingDetail;
                return false;
            }

            clone.SetFloat(
                LockedMaterialIdentity.OptimizerEnabledPropertyName, 0f);

            refusalDetail = null;
            return true;
        }

        /// <summary>
        /// Restores exactly the recorded keyword set. The vendor restore
        /// replaces the whole set, so whatever the locked state carried is
        /// discarded, and a missing or empty tag records an empty set.
        /// </summary>
        private static void RestoreKeywords(Material clone)
        {
            var keywordsTag = clone.GetTag(
                OriginalKeywordsTagName, true, null);
            clone.shaderKeywords = string.IsNullOrEmpty(keywordsTag)
                ? Array.Empty<string>()
                : keywordsTag.Split(' ');
        }

        /// <summary>
        /// Moves every rename-suffixed saved value onto the plain property
        /// name the resolved shader declares. A suffixed entry stays
        /// untouched when its plain name is undeclared or when its
        /// animated override tag does not equal two, because names can be
        /// duplicated instead of renamed and endings can be skipped, and
        /// the vendor unlock leaves exactly those entries alone. A moved
        /// value is copied, and its suffixed entry stays in the
        /// serialization: no step of the verification contract reads the
        /// residue, and deleting entries would be an extra mutation the
        /// in-memory contract does not need.
        /// </summary>
        private static void MoveSuffixedSavedValues(
            Material clone, Shader resolved)
        {
            var suffix = CleanNameSuffix(clone.GetTag(
                RenameSuffixTagName, true, clone.name));
            if (suffix.Length == 0)
            {
                // Without a suffix no saved name can carry the rename
                // marker, so every entry is already in its plain place.
                return;
            }

            var declared = DeclaredProperties(resolved);
            var textures = new List<(string name, Texture texture,
                Vector2 scale, Vector2 offset)>();
            var floats = new List<(string name, float value)>();
            var colors = new List<(string name, Color value)>();
            var vectors = new List<(string name, Vector4 value)>();

            ReadSavedProperties(clone, textures, floats, colors, vectors);

            foreach (var entry in floats)
            {
                if (!TryPlainName(entry.name, suffix, out var plainName) ||
                    !IsRenameAnimated(clone, plainName) ||
                    !declared.TryGetValue(plainName, out var type))
                {
                    continue;
                }

                if (type == ShaderPropertyType.Float ||
                    type == ShaderPropertyType.Range)
                {
                    clone.SetFloat(plainName, entry.value);
                }
            }

            foreach (var entry in colors)
            {
                if (!TryPlainName(entry.name, suffix, out var plainName) ||
                    !IsRenameAnimated(clone, plainName) ||
                    !declared.TryGetValue(plainName, out var type))
                {
                    continue;
                }

                // Vector4 values share the colors storage on Unity
                // versions whose material serialization has no m_Vectors
                // container, so a saved colors entry must also land on a
                // Vector-declared plain name, exactly as the vendor
                // restore puts it back.
                if (type == ShaderPropertyType.Color)
                {
                    clone.SetColor(plainName, entry.value);
                }
                else if (type == ShaderPropertyType.Vector)
                {
                    clone.SetVector(plainName, entry.value);
                }
            }

            foreach (var entry in vectors)
            {
                if (!TryPlainName(entry.name, suffix, out var plainName))
                {
                    continue;
                }

                if (declared.TryGetValue(plainName, out var type) &&
                    type == ShaderPropertyType.Vector)
                {
                    if (IsRenameAnimated(clone, plainName))
                    {
                        clone.SetVector(plainName, entry.value);
                    }

                    continue;
                }

                // Vendor optimizer format, controller-extracted, fact 3
                // (rename scope): a renamed texture also carries its scale
                // and offset, so a suffixed _ST vector lands on the
                // declared texture's scale and offset. The _TexelSize
                // ending names a derived value, not a stored one, and
                // stays untouched.
                if (TryTextureBaseName(plainName, out var baseName) &&
                    declared.TryGetValue(baseName, out var baseType) &&
                    baseType == ShaderPropertyType.Texture &&
                    IsRenameAnimated(clone, baseName))
                {
                    clone.SetTextureScale(
                        baseName, new Vector2(entry.value.x, entry.value.y));
                    clone.SetTextureOffset(
                        baseName, new Vector2(entry.value.z, entry.value.w));
                }
            }

            foreach (var entry in textures)
            {
                if (!TryPlainName(entry.name, suffix, out var plainName) ||
                    !IsRenameAnimated(clone, plainName) ||
                    !declared.TryGetValue(plainName, out var type) ||
                    type != ShaderPropertyType.Texture)
                {
                    continue;
                }

                // The scale and offset ride inside the renamed texenv
                // entry itself, per fact 3.
                clone.SetTexture(plainName, entry.texture);
                clone.SetTextureScale(plainName, entry.scale);
                clone.SetTextureOffset(plainName, entry.offset);
            }
        }

        /// <summary>
        /// The resolved shader's declared property table. Declaration, not
        /// presence in the serialization, decides whether a plain name can
        /// receive a moved value.
        /// </summary>
        private static Dictionary<string, ShaderPropertyType>
            DeclaredProperties(Shader shader)
        {
            var declared =
                new Dictionary<string, ShaderPropertyType>();
            var count = shader.GetPropertyCount();
            for (var index = 0; index < count; index++)
            {
                declared[shader.GetPropertyName(index)] =
                    shader.GetPropertyType(index);
            }

            return declared;
        }

        /// <summary>
        /// Reads the saved serialized properties, never the declared
        /// table: the locked generated shader declares a reduced table, so
        /// the serialized blob is the only place every value survives.
        /// Each container and each element lookup is checked for
        /// absence, because these are Unity-internal serialization
        /// details whose presence and shape have changed across material
        /// serializedVersions; an absent container carries no values of
        /// its kind, and an unreadable element stays untouched. The
        /// m_Vectors container exists only on serialization shapes that
        /// separate Vector4 storage; on shapes without it the pass is
        /// empty and harmless, because those values arrive through the
        /// colors container and are routed there.
        /// </summary>
        private static void ReadSavedProperties(
            Material clone,
            List<(string name, Texture texture, Vector2 scale,
                Vector2 offset)> textures,
            List<(string name, float value)> floats,
            List<(string name, Color value)> colors,
            List<(string name, Vector4 value)> vectors)
        {
            using var serialized = new SerializedObject(clone);

            var texEnvs = serialized.FindProperty(
                "m_SavedProperties.m_TexEnvs");
            if (texEnvs != null)
            {
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
                    var scaleProperty =
                        second.FindPropertyRelative("m_Scale");
                    var offsetProperty =
                        second.FindPropertyRelative("m_Offset");
                    textures.Add((
                        name,
                        textureProperty != null &&
                        textureProperty.propertyType ==
                        SerializedPropertyType.ObjectReference
                            ? textureProperty.objectReferenceValue
                                as Texture
                            : null,
                        scaleProperty != null
                            ? scaleProperty.vector2Value
                            : new Vector2(1f, 1f),
                        offsetProperty != null
                            ? offsetProperty.vector2Value
                            : new Vector2(0f, 0f)));
                }
            }

            var serializedFloats = serialized.FindProperty(
                "m_SavedProperties.m_Floats");
            if (serializedFloats != null)
            {
                for (var index = 0; index < serializedFloats.arraySize;
                     index++)
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

                    floats.Add((name, second.floatValue));
                }
            }

            var serializedColors = serialized.FindProperty(
                "m_SavedProperties.m_Colors");
            if (serializedColors != null)
            {
                for (var index = 0; index < serializedColors.arraySize;
                     index++)
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

                    colors.Add((name, second.colorValue));
                }
            }

            var serializedVectors = serialized.FindProperty(
                "m_SavedProperties.m_Vectors");
            if (serializedVectors != null)
            {
                for (var index = 0; index < serializedVectors.arraySize;
                     index++)
                {
                    var element =
                        serializedVectors.GetArrayElementAtIndex(index);
                    var name = ElementKeyName(element);
                    var second = element.FindPropertyRelative("second");
                    if (name == null || second == null ||
                        second.propertyType !=
                        SerializedPropertyType.Vector4)
                    {
                        continue;
                    }

                    vectors.Add((name, second.vector4Value));
                }
            }
        }

        /// <summary>
        /// The key of one saved-property element. Material serialized
        /// versions have carried the key both directly in "first" and
        /// inside a "first" struct under "name"; null when neither shape
        /// matches, which leaves the element untouched.
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

        /// <summary>
        /// Strips the one rename marker from the end of a saved name. The
        /// plain remainder must be non-empty, or the name was never a
        /// rename of anything.
        /// </summary>
        private static bool TryPlainName(
            string savedName, string suffix, out string plainName)
        {
            plainName = null;
            var markerStart = savedName.Length - suffix.Length - 1;
            if (markerStart <= 0 ||
                savedName[markerStart] != '_' ||
                string.CompareOrdinal(
                    savedName, markerStart + 1, suffix, 0,
                    suffix.Length) != 0)
            {
                return false;
            }

            plainName = savedName.Substring(0, markerStart);
            return true;
        }

        /// <summary>
        /// Splits a declared texture's scale-and-offset name into the
        /// texture's own name.
        /// </summary>
        private static bool TryTextureBaseName(
            string plainName, out string baseName)
        {
            const string stEnding = "_ST";
            if (plainName.Length <= stEnding.Length ||
                !plainName.EndsWith(stEnding, StringComparison.Ordinal))
            {
                baseName = null;
                return false;
            }

            baseName =
                plainName.Substring(
                    0, plainName.Length - stEnding.Length);
            return baseName.Length > 0;
        }

        /// <summary>
        /// The animated rename tag of one plain property name. Absent or
        /// not two means the name was duplicated or skipped at lock, not
        /// renamed, and its suffixed entries must stay untouched.
        /// </summary>
        private static bool IsRenameAnimated(
            Material clone, string plainName)
        {
            return clone.GetTag(
                plainName + AnimatedTagSuffix,
                true,
                null) == AnimatedRenameTagValue;
        }

        /// <summary>
        /// Vendor optimizer format, controller-extracted, fact 2 (rename
        /// suffix): trim, remove every space, then keep the letters and
        /// digits of the UTF-8 bytes and write every other byte as its two
        /// uppercase hexadecimal digits, so a hyphen becomes 2D.
        /// </summary>
        private static string CleanNameSuffix(string raw)
        {
            var spaced = raw.Trim().Replace(" ", string.Empty);
            var bytes = Encoding.UTF8.GetBytes(spaced);
            var cleaned = new StringBuilder(bytes.Length);
            foreach (var value in bytes)
            {
                var isLetterOrDigit =
                    (value >= 0x41 && value <= 0x5A) ||
                    (value >= 0x61 && value <= 0x7A) ||
                    (value >= 0x30 && value <= 0x39);
                if (isLetterOrDigit)
                {
                    cleaned.Append((char)value);
                }
                else
                {
                    cleaned.Append(value.ToString("X2"));
                }
            }

            return cleaned.ToString();
        }
    }
}
