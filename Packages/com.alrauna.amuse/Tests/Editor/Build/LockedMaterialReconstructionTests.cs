using System.Collections.Generic;
using Alrauna.Amuse.Editor.Build;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The in-memory locked-material reconstruction on synthetic locked
    /// fixtures. Every fixture is an in-memory material: the locked stand-in
    /// shader carries the locked serialization and tags, the original
    /// stand-in shader plays the recorded original, and no asset database
    /// content is written or refreshed.
    /// </summary>
    public sealed class LockedMaterialReconstructionTests
    {
        private const string OriginalStandInShaderName =
            "Hidden/Alrauna/AmuseTests/LockedStandInOriginal";

        private const string LockedStandInShaderName =
            "Hidden/Locked/Alrauna/AmuseTests/LockedStandIn";

        // An existing schema-only stand-in that does not declare the
        // optimizer flag property, so it can play an original shader whose
        // flag step must refuse.
        private const string FlaglessStandInShaderName =
            "Hidden/Alrauna/AmuseTests/LilToonSemanticTest";

        // The hyphen exercises the vendor rename-suffix cleaning rule: the
        // suffix of this name is "ReconCape2D7".
        private const string LockedMaterialName = "ReconCape-7";
        private const string RenameSuffix = "ReconCape2D7";
        private const string SuffixedMainTexName = "_MainTex_ReconCape2D7";
        private const string SuffixedFloatName = "_Saturation_ReconCape2D7";
        private const string StaleKeyword = "STALE_RECON_KEYWORD";

        // Vendor optimizer format, controller-extracted, fact 1 (keyword
        // tag): one space-joined keyword list that replaces the whole set.
        private const string RecordedKeywordsTag =
            "AMUSE_RECON_KEYWORD_A AMUSE_RECON_KEYWORD_B";

        private static readonly string[] RecordedKeywords =
        {
            "AMUSE_RECON_KEYWORD_A",
            "AMUSE_RECON_KEYWORD_B",
        };

        private const string UnresolvableGuid =
            "deadbeefdeadbeefdeadbeefdeadbeef";

        // The fixed refusal sentences the reconstruction contract pins.
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

        private readonly List<Object> tracked = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = 0; index < tracked.Count; index++)
            {
                if (tracked[index] != null)
                {
                    Object.DestroyImmediate(tracked[index], true);
                }
            }

            tracked.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            if (obj != null)
            {
                tracked.Add(obj);
            }

            return obj;
        }

        private static Shader StandInShader(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null, $"'{shaderName}' must import.");
            return shader;
        }

        /// <summary>
        /// The unlocked original fixture: one material on the original
        /// stand-in that carries one plain float and the cleared flag, and
        /// whose shader GUID is recorded through the AssetDatabase.
        /// </summary>
        private Material UnlockedOriginalFixture(
            Shader originalShader, Texture plainTexture)
        {
            var original = Track(new Material(originalShader)
            {
                name = LockedMaterialName,
            });
            original.SetFloat("_Saturation", 0.37f);
            original.SetFloat(
                LockedMaterialIdentity.OptimizerEnabledPropertyName, 0f);
            original.SetTexture("_MainTex", plainTexture);
            return original;
        }

        /// <summary>
        /// The locked form: a name-identical material on the locked
        /// stand-in whose flag float is one, whose tags record the original
        /// name and GUID, whose keyword tag carries a keyword set, and
        /// whose saved properties hold the suffixed name carrying the value
        /// while the plain name holds a different value. The plain value is
        /// a typed write on a declared name; every suffixed entry is
        /// written through the serialization, because Unity no-ops typed
        /// writes on names the current shader does not declare.
        /// </summary>
        private Material LockedFixture(
            Shader originalShader,
            string recordedGuid,
            Texture plainTexture = null,
            Texture suffixedTexture = null,
            bool withNameTag = true,
            bool withGuidTag = true,
            bool withKeywordTag = true,
            bool mainTexAnimated = true,
            bool withSuffixedUndeclaredFloat = false)
        {
            var locked = Track(new Material(
                StandInShader(LockedStandInShaderName))
            {
                name = LockedMaterialName,
            });
            locked.SetFloat(
                LockedMaterialIdentity.OptimizerEnabledPropertyName, 1f);

            // The locked keyword set differs from the recorded tag, so the
            // restore must replace the whole set, not extend it.
            locked.shaderKeywords = new[] { StaleKeyword };

            if (withNameTag)
            {
                locked.SetOverrideTag(
                    LockedMaterialIdentity.OriginalShaderTagName,
                    originalShader.name);
            }

            if (withGuidTag)
            {
                locked.SetOverrideTag(
                    LockedMaterialIdentity.OriginalShaderGuidTagName,
                    recordedGuid);
            }

            if (withKeywordTag)
            {
                locked.SetOverrideTag("OriginalKeywords",
                    RecordedKeywordsTag);
            }

            // Vendor optimizer format, controller-extracted, fact 3 (rename
            // scope): the animated tag value two marks a renamed property.
            if (mainTexAnimated)
            {
                locked.SetOverrideTag("_MainTex" + "Animated", "2");
            }

            if (plainTexture != null)
            {
                locked.SetTexture("_MainTex", plainTexture);
                locked.SetTextureScale("_MainTex", Vector2.one);
            }

            if (suffixedTexture != null)
            {
                WriteSavedTexEnv(locked, SuffixedMainTexName,
                    suffixedTexture, new Vector2(2f, 3f),
                    new Vector2(0.25f, 0.5f));
            }

            if (withSuffixedUndeclaredFloat)
            {
                WriteSavedFloat(locked, SuffixedFloatName, 0.37f);
                locked.SetOverrideTag("_Saturation" + "Animated", "2");
            }

            return locked;
        }

        /// <summary>
        /// Appends one saved texenv entry through the serialization, the
        /// way the lock persists renamed texture properties.
        /// </summary>
        private static void WriteSavedTexEnv(
            Material material, string propertyName, Texture texture,
            Vector2 scale, Vector2 offset)
        {
            using var serialized = new SerializedObject(material);
            var texEnvs = serialized.FindProperty(
                "m_SavedProperties.m_TexEnvs");
            Assert.That(texEnvs, Is.Not.Null,
                "m_TexEnvs must exist on a material.");
            texEnvs.InsertArrayElementAtIndex(texEnvs.arraySize);
            var element = texEnvs.GetArrayElementAtIndex(
                texEnvs.arraySize - 1);
            SetElementKey(element, propertyName);
            var second = element.FindPropertyRelative("second");
            Assert.That(second, Is.Not.Null,
                "the texenv element must expose its value struct.");
            second.FindPropertyRelative("m_Texture")
                .objectReferenceValue = texture;
            second.FindPropertyRelative("m_Scale").vector2Value = scale;
            second.FindPropertyRelative("m_Offset").vector2Value = offset;
            serialized.ApplyModifiedProperties();
        }

        /// <summary>
        /// Appends one saved float entry through the serialization, the
        /// way the lock persists renamed float properties.
        /// </summary>
        private static void WriteSavedFloat(
            Material material, string propertyName, float value)
        {
            using var serialized = new SerializedObject(material);
            var floats = serialized.FindProperty(
                "m_SavedProperties.m_Floats");
            Assert.That(floats, Is.Not.Null,
                "m_Floats must exist on a material.");
            floats.InsertArrayElementAtIndex(floats.arraySize);
            var element = floats.GetArrayElementAtIndex(
                floats.arraySize - 1);
            SetElementKey(element, propertyName);
            var second = element.FindPropertyRelative("second");
            Assert.That(second, Is.Not.Null,
                "the float element must expose its value.");
            second.floatValue = value;
            serialized.ApplyModifiedProperties();
        }

        /// <summary>
        /// Writes the key of one saved-property element. The current
        /// material serialization keeps the key string directly in
        /// "first"; the older one kept it inside a "first" struct under
        /// "name". Both are written so the fixture never depends on which
        /// one this Unity exposes.
        /// </summary>
        private static void SetElementKey(
            SerializedProperty element, string propertyName)
        {
            var first = element.FindPropertyRelative("first");
            Assert.That(first, Is.Not.Null,
                "the saved-property element must expose its key.");
            if (first.propertyType == SerializedPropertyType.String)
            {
                first.stringValue = propertyName;
                return;
            }

            var name = first.FindPropertyRelative("name");
            Assert.That(name, Is.Not.Null,
                "the saved-property key struct must expose its name.");
            name.stringValue = propertyName;
        }

        /// <summary>
        /// The key of one saved-property element, read with the same
        /// shape tolerance the fixture writer uses.
        /// </summary>
        private static string ElementKey(SerializedProperty element)
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

        private Texture2D TrackTexture(string textureName)
        {
            var texture = Track(new Texture2D(2, 2));
            texture.name = textureName;
            return texture;
        }

        private static bool Apply(
            Material locked, bool attests, out string refusalDetail)
        {
            return LockedMaterialReconstruction.TryApply(
                locked, _ => attests, out refusalDetail);
        }

        private static void AssertRefusal(
            bool applied,
            string refusalDetail,
            string expectedDetail,
            string recordedGuid)
        {
            Assert.That(applied, Is.False,
                "the reconstruction must refuse");
            Assert.That(refusalDetail, Is.EqualTo(expectedDetail));
            Assert.That(refusalDetail.Contains("/"), Is.False,
                "a refusal detail must never carry a path");
            Assert.That(refusalDetail.Contains(recordedGuid), Is.False,
                "a refusal detail must never carry a GUID");
            Assert.That(refusalDetail.Contains(LockedMaterialName), Is.False,
                "a refusal detail must never carry a material name");
        }

        private static float? SavedFloat(
            Material material, string propertyName)
        {
            using var serialized = new SerializedObject(material);
            var floats = serialized.FindProperty(
                "m_SavedProperties.m_Floats");
            if (floats == null)
            {
                return null;
            }

            for (var index = 0; index < floats.arraySize; index++)
            {
                var element = floats.GetArrayElementAtIndex(index);
                if (ElementKey(element) == propertyName)
                {
                    var second = element.FindPropertyRelative("second");
                    return second != null ? second.floatValue : null;
                }
            }

            return null;
        }

        /// <summary>
        /// The texture of one saved texenv entry, read through the same
        /// serialization the fixture writes, so a guard on it cannot be
        /// fooled by the typed getter's view of undeclared names.
        /// </summary>
        private static Texture SavedTexEnvTexture(
            Material material, string propertyName)
        {
            using var serialized = new SerializedObject(material);
            var texEnvs = serialized.FindProperty(
                "m_SavedProperties.m_TexEnvs");
            if (texEnvs == null)
            {
                return null;
            }

            for (var index = 0; index < texEnvs.arraySize; index++)
            {
                var element = texEnvs.GetArrayElementAtIndex(index);
                if (ElementKey(element) == propertyName)
                {
                    var second = element.FindPropertyRelative("second");
                    var textureProperty =
                        second != null
                            ? second.FindPropertyRelative("m_Texture")
                            : null;
                    return textureProperty != null
                        ? textureProperty.objectReferenceValue as Texture
                        : null;
                }
            }

            return null;
        }

        [Test]
        public void ReconstructionMovesSuffixedValuesOntoPlainNamesRestoresKeywordsAndClearsFlag()
        {
            // Falsifies the declared-table reader: the locked shader's
            // typed view resolves the plain value, never the suffixed
            // value the serialization holds.
            var originalShader = StandInShader(OriginalStandInShaderName);
            var plainTexture = TrackTexture("PlainMainTex");
            var suffixedTexture = TrackTexture("SuffixedMainTex");
            UnlockedOriginalFixture(originalShader, plainTexture);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                originalShader, out var guid, out long _);

            var locked = LockedFixture(
                originalShader, guid, plainTexture, suffixedTexture);

            // No-op guards: the fixture must hold the locked shape, or the
            // assertions below prove nothing. The suffixed entry is
            // checked in the serialization, because the typed getter of an
            // undeclared name cannot see it.
            Assert.That(SavedTexEnvTexture(locked, SuffixedMainTexName),
                Is.SameAs(suffixedTexture),
                "the suffixed texenv must exist pre-call");
            Assert.That(locked.shader.name,
                Is.EqualTo(LockedStandInShaderName));
            Assert.That(locked.GetTexture("_MainTex"),
                Is.SameAs(plainTexture),
                "the plain name must hold a different value pre-call");
            Assert.That(locked.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(1f));

            var applied = Apply(locked, true, out var refusalDetail);

            Assert.That(applied, Is.True, refusalDetail ?? string.Empty);
            Assert.That(refusalDetail, Is.Null);
            Assert.That(locked.shader, Is.SameAs(originalShader));
            Assert.That(locked.GetTexture("_MainTex"),
                Is.SameAs(suffixedTexture),
                "the typed getter must read the suffixed value on the " +
                "plain name");
            Assert.That(locked.GetTextureScale("_MainTex"),
                Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(locked.GetTextureOffset("_MainTex"),
                Is.EqualTo(new Vector2(0.25f, 0.5f)));
            Assert.That(locked.shaderKeywords,
                Is.EquivalentTo(RecordedKeywords),
                "the keyword set must be replaced with the tag's set");
            Assert.That(locked.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(0f));
        }

        [Test]
        public void MissingOriginalNameTagRefusesWithTheNameTagDetail()
        {
            // Falsifies the tag-less reader: without reading the identity
            // tags this case would apply and return true.
            var originalShader = StandInShader(OriginalStandInShaderName);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                originalShader, out var guid, out long _);

            var locked = LockedFixture(
                originalShader, guid, withNameTag: false);

            var applied = Apply(locked, true, out var refusalDetail);

            AssertRefusal(
                applied, refusalDetail, NameTagMissingDetail, guid);
        }

        [Test]
        public void MissingOriginalGuidTagRefusesWithTheGuidTagDetail()
        {
            // Falsifies the tag-less reader: without reading the identity
            // tags this case would apply and return true.
            var originalShader = StandInShader(OriginalStandInShaderName);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                originalShader, out var guid, out long _);

            var locked = LockedFixture(
                originalShader, guid, withGuidTag: false);

            var applied = Apply(locked, true, out var refusalDetail);

            AssertRefusal(
                applied, refusalDetail, GuidTagMissingDetail, guid);
        }

        [Test]
        public void UnresolvableOriginalGuidRefusesWithTheResolutionDetail()
        {
            // Falsifies the tag-less reader: a reader that never resolves
            // the recorded GUID through the AssetDatabase answers true
            // here.
            var originalShader = StandInShader(OriginalStandInShaderName);

            var locked = LockedFixture(
                originalShader, UnresolvableGuid);

            var applied = Apply(locked, true, out var refusalDetail);

            AssertRefusal(
                applied, refusalDetail, GuidUnresolvableDetail,
                UnresolvableGuid);
        }

        [Test]
        public void UnattestedOriginalShaderRefusesWithTheAttestationDetail()
        {
            // Falsifies the tag-less reader: a reader that skips the
            // attestation seam answers true on an unattested original.
            var originalShader = StandInShader(OriginalStandInShaderName);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                originalShader, out var guid, out long _);

            var locked = LockedFixture(originalShader, guid);

            var applied = Apply(locked, false, out var refusalDetail);

            AssertRefusal(
                applied, refusalDetail, AttestationFailedDetail, guid);
        }

        [Test]
        public void OriginalShaderWithoutFlagPropertyRefusesWithTheFlagDetail()
        {
            // Falsifies the blind flag writer: clearing the flag without a
            // declared-property check corrupts a shader that never
            // carried it.
            var flaglessShader = StandInShader(FlaglessStandInShaderName);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                flaglessShader, out var guid, out long _);

            var locked = LockedFixture(
                flaglessShader,
                guid,
                mainTexAnimated: false);

            var applied = Apply(locked, true, out var refusalDetail);

            AssertRefusal(
                applied, refusalDetail, FlagPropertyMissingDetail, guid);
        }

        [Test]
        public void SuffixedValueWithoutPlainDeclarationStaysUntouchedAndReconstructionSucceeds()
        {
            // Falsifies the blind stripper: stripping the suffix without a
            // declared-plain-name check writes orphan values onto
            // undeclared names.
            var originalShader = StandInShader(OriginalStandInShaderName);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                originalShader, out var guid, out long _);

            var locked = LockedFixture(
                originalShader,
                guid,
                withSuffixedUndeclaredFloat: true);

            var applied = Apply(locked, true, out var refusalDetail);

            Assert.That(applied, Is.True, refusalDetail ?? string.Empty);
            Assert.That(refusalDetail, Is.Null);
            Assert.That(SavedFloat(locked, SuffixedFloatName),
                Is.EqualTo(0.37f),
                "the undeclared suffixed value must stay untouched");
            Assert.That(SavedFloat(locked, "_Saturation"), Is.Null,
                "no plain entry may appear for an undeclared name");
            Assert.That(locked.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(0f),
                "every other step must still have held");
        }

        [Test]
        public void SuffixedValueWithoutAnimatedTagStaysUntouchedAndReconstructionSucceeds()
        {
            // Falsifies the suffix-only mover: moving every suffixed value
            // without the animated-tag check renames duplicates that the
            // vendor unlock leaves alone.
            var originalShader = StandInShader(OriginalStandInShaderName);
            var plainTexture = TrackTexture("PlainMainTex");
            var suffixedTexture = TrackTexture("SuffixedMainTex");
            UnlockedOriginalFixture(originalShader, plainTexture);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                originalShader, out var guid, out long _);

            var locked = LockedFixture(
                originalShader,
                guid,
                plainTexture,
                suffixedTexture,
                mainTexAnimated: false);

            // No-op guard: the plain name must hold its own value
            // pre-call, and the suffixed entry must exist in the
            // serialization, or the untouched assertion proves nothing.
            Assert.That(locked.GetTexture("_MainTex"),
                Is.SameAs(plainTexture));
            Assert.That(SavedTexEnvTexture(locked, SuffixedMainTexName),
                Is.SameAs(suffixedTexture),
                "the suffixed texenv must exist pre-call");

            var applied = Apply(locked, true, out var refusalDetail);

            Assert.That(applied, Is.True, refusalDetail ?? string.Empty);
            Assert.That(refusalDetail, Is.Null);
            Assert.That(locked.GetTexture("_MainTex"),
                Is.SameAs(plainTexture),
                "the suffixed value must stay off the plain name");
            Assert.That(locked.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(0f),
                "every other step must still have held");
        }
    }
}
