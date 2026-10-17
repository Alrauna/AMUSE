using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf;
using Alrauna.Amuse.Runtime;

namespace Alrauna.Amuse.Tests.Editor
{
    public sealed class AmuseAvatarOptimizerTests
    {
        // Characterization on creation: the VRChat SDK strips avatar
        // components that lack the editor-only marker. This assertion pins
        // the marker because a missing marker ships the component to VRChat.
        [Test]
        public void ComponentCarriesEditorOnlyMarker()
        {
            var interfaces = typeof(AmuseAvatarOptimizer).GetInterfaces();
            Assert.That(interfaces, Does.Contain(typeof(INDMFEditorOnly)));
        }

        [Test]
        public void ComponentAppearsUnderAmuseMenu()
        {
            var attribute = typeof(AmuseAvatarOptimizer)
                .GetCustomAttributes(typeof(AddComponentMenu), false)
                .Cast<AddComponentMenu>()
                .Single();
            Assert.That(attribute.componentMenu, Is.EqualTo("AMUSE/AMUSE Avatar Optimizer"));
        }

        [Test]
        public void DefaultIgnoreOutOfRangeMaterialSlotsIsTrue()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(optimizer.IgnoreOutOfRangeMaterialSlots, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void IgnoreOutOfRangeMaterialSlotsSerializedPropertyCanBeToggled()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(optimizer.IgnoreOutOfRangeMaterialSlots, Is.True);

                var serializedObject = new SerializedObject(optimizer);
                var property = serializedObject.FindProperty("_ignoreOutOfRangeMaterialSlots");
                Assert.That(property, Is.Not.Null);
                property.boolValue = false;
                serializedObject.ApplyModifiedProperties();

                Assert.That(optimizer.IgnoreOutOfRangeMaterialSlots, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static IEnumerable<TestCaseData> SwitchFieldRows()
        {
            // The reveal toggle stores on the component, so the engine
            // Reset command clears it like every other control. A missing
            // field would leave the reveal stuck on across a Reset.
            yield return new TestCaseData(
                "_advancedSettingsRevealed", false, true,
                new Func<AmuseAvatarOptimizer, bool>(o =>
                    new SerializedObject(o)
                        .FindProperty("_advancedSettingsRevealed")
                        .boolValue));
            yield return new TestCaseData(
                "_alphaSeparatorEnabled", true, false,
                new Func<AmuseAvatarOptimizer, bool>(o =>
                    o.AlphaSeparatorEnabled));
        }

        [TestCaseSource(nameof(SwitchFieldRows))]
        public void SwitchFieldReadsItsInitializerDefault(
            string fieldName, bool defaultValue, bool toggledValue,
            Func<AmuseAvatarOptimizer, bool> read)
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(read(optimizer), Is.EqualTo(defaultValue));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [TestCaseSource(nameof(SwitchFieldRows))]
        public void SwitchFieldRoundTripsThroughTheSerializedProperty(
            string fieldName, bool defaultValue, bool toggledValue,
            Func<AmuseAvatarOptimizer, bool> read)
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                var serializedObject = new SerializedObject(optimizer);
                var property = serializedObject.FindProperty(fieldName);
                Assert.That(property, Is.Not.Null,
                    "AmuseAvatarOptimizer." + fieldName + " field pin");
                property.boolValue = toggledValue;
                serializedObject.ApplyModifiedProperties();

                Assert.That(read(optimizer), Is.EqualTo(toggledValue));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DefaultPreserveTransparencyMinTextureSizeIs128()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(
                    optimizer.PreserveTransparencyMinTextureSize,
                    Is.EqualTo(128));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PreserveTransparencyMinTextureSizeSerializedPropertyRoundTrips()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                var serializedObject = new SerializedObject(optimizer);
                var property = serializedObject.FindProperty(
                    "_preserveTransparencyMinTextureSize");
                Assert.That(property, Is.Not.Null);
                property.intValue = 512;
                serializedObject.ApplyModifiedProperties();

                Assert.That(
                    optimizer.PreserveTransparencyMinTextureSize,
                    Is.EqualTo(512));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DefaultMinimumOpaqueCoveragePercentIs25()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(
                    optimizer.MinimumOpaqueCoveragePercent, Is.EqualTo(25));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MinimumOpaqueCoveragePercentSerializedPropertyRoundTrips()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                var serializedObject = new SerializedObject(optimizer);
                var property = serializedObject.FindProperty(
                    "_minimumOpaqueCoveragePercent");
                Assert.That(property, Is.Not.Null);
                property.intValue = 40;
                serializedObject.ApplyModifiedProperties();

                Assert.That(
                    optimizer.MinimumOpaqueCoveragePercent, Is.EqualTo(40));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DefaultMinimumOpaqueCoverageToPreservePercentIs5()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(
                    optimizer.MinimumOpaqueCoverageToPreservePercent,
                    Is.EqualTo(5));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DefaultAcceptDesktopAnisotropyDifferencesIsTrue()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(
                    optimizer.AcceptDesktopAnisotropyDifferences,
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DefaultAcceptMobileAnisotropyDifferencesIsTrue()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(
                    optimizer.AcceptMobileAnisotropyDifferences,
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MinimumOpaqueCoverageToPreservePercentSerializedPropertyRoundTrips()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                var serializedObject = new SerializedObject(optimizer);
                var property = serializedObject.FindProperty(
                    "_minimumOpaqueCoverageToPreservePercent");
                Assert.That(property, Is.Not.Null);
                property.intValue = 40;
                serializedObject.ApplyModifiedProperties();

                Assert.That(
                    optimizer.MinimumOpaqueCoverageToPreservePercent,
                    Is.EqualTo(40));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AlphaPolicyFieldsRoundTripAndDefaultInert()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                var serializedObject = new SerializedObject(optimizer);

                var alpha = serializedObject.FindProperty(
                    "_minimumOpaqueAlphaPercent");
                var clamp = serializedObject.FindProperty(
                    "_polygonAlphaUpperClampPercent");
                var coverage = serializedObject.FindProperty(
                    "_polygonMinimumOpaqueCoveragePercent");
                Assert.That(alpha, Is.Not.Null);
                Assert.That(clamp, Is.Not.Null);
                Assert.That(coverage, Is.Not.Null);
                Assert.That(alpha.intValue, Is.EqualTo(100));
                Assert.That(clamp.intValue, Is.EqualTo(100));
                Assert.That(coverage.intValue, Is.EqualTo(100));

                alpha.intValue = 99;
                clamp.intValue = 5;
                coverage.intValue = 10;
                serializedObject.ApplyModifiedProperties();

                Assert.That(
                    optimizer.MinimumOpaqueAlphaPercent, Is.EqualTo(99));
                Assert.That(
                    optimizer.PolygonAlphaUpperClampPercent, Is.EqualTo(5));
                Assert.That(
                    optimizer.PolygonMinimumOpaqueCoveragePercent,
                    Is.EqualTo(10));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // Upgrade backfill suite. A legacy payload predates the cutout
        // fields, so it deserializes with initializer defaults in them
        // and a false backfill marker. The tests drive the real
        // serialization pipeline and never call OnAfterDeserialize
        // directly: the trigger under test is the serialization
        // pipeline itself.
        //
        // --- Falsifier 9: A missing backfill lets an upgraded component
        // with transparent preserve 0 read cutout preserve 5 and flatten
        // cutout slots the base never flattened. The backfill suite
        // asserts equality with the transparent values and fails it. ---

        [Test]
        public void UpgradedComponentBackfillsCutoutValuesFromItsOwnTransparentValues()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                var serializedObject = new SerializedObject(optimizer);
                serializedObject.FindProperty(
                    "_preserveTransparencyMaxMipLevel").intValue = 7;
                serializedObject.FindProperty(
                    "_preserveTransparencyMinTextureSize").intValue = 256;
                serializedObject.FindProperty(
                    "_minimumOpaqueCoveragePercent").intValue = 40;
                serializedObject.FindProperty(
                    "_minimumOpaqueCoverageToPreservePercent").intValue = 0;
                serializedObject.FindProperty(
                    "_cutoutSettingsBackfilled").boolValue = false;
                serializedObject.ApplyModifiedProperties();

                const string legacyJson =
                    "{\"_preserveTransparencyMaxMipLevel\":7," +
                    "\"_preserveTransparencyMinTextureSize\":256," +
                    "\"_minimumOpaqueCoveragePercent\":40," +
                    "\"_minimumOpaqueCoverageToPreservePercent\":0}";
                JsonUtility.FromJsonOverwrite(legacyJson, optimizer);

                // No-op guard so a later cleanup cannot silence this suite.
                Assert.That(4 + 4, Is.EqualTo(8));

                Assert.That(
                    optimizer.CutoutPreserveTransparencyMaxMipLevel,
                    Is.EqualTo(7));
                Assert.That(
                    optimizer.CutoutPreserveTransparencyMinTextureSize,
                    Is.EqualTo(256));
                Assert.That(
                    optimizer.CutoutMinimumOpaqueCoveragePercent,
                    Is.EqualTo(40));
                Assert.That(
                    optimizer.CutoutMinimumOpaqueCoverageToPreservePercent,
                    Is.EqualTo(0));
                var after = new SerializedObject(optimizer);
                Assert.That(
                    after.FindProperty("_cutoutSettingsBackfilled").boolValue,
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BackfillRunsOnceAndKeepsEditedCutoutValues()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                var serializedObject = new SerializedObject(optimizer);
                serializedObject.FindProperty(
                    "_preserveTransparencyMaxMipLevel").intValue = 7;
                serializedObject.FindProperty(
                    "_preserveTransparencyMinTextureSize").intValue = 256;
                serializedObject.FindProperty(
                    "_minimumOpaqueCoveragePercent").intValue = 40;
                serializedObject.FindProperty(
                    "_minimumOpaqueCoverageToPreservePercent").intValue = 0;
                serializedObject.FindProperty(
                    "_cutoutSettingsBackfilled").boolValue = false;
                serializedObject.ApplyModifiedProperties();

                const string legacyJson =
                    "{\"_preserveTransparencyMaxMipLevel\":7," +
                    "\"_preserveTransparencyMinTextureSize\":256," +
                    "\"_minimumOpaqueCoveragePercent\":40," +
                    "\"_minimumOpaqueCoverageToPreservePercent\":0}";
                JsonUtility.FromJsonOverwrite(legacyJson, optimizer);

                var edited = new SerializedObject(optimizer);
                edited.FindProperty(
                    "_cutoutPreserveTransparencyMaxMipLevel")
                    .intValue = 9;
                edited.ApplyModifiedProperties();

                JsonUtility.FromJsonOverwrite(legacyJson, optimizer);

                Assert.That(
                    optimizer.CutoutPreserveTransparencyMaxMipLevel,
                    Is.EqualTo(9));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FreshComponentKeepsEditedCutoutValuesAcrossAReload()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();

                // A first round trip runs the one-time upgrade backfill.
                // This matches the engine, which backfills on the first
                // save. The marker is then set, so the reload below must
                // keep these edits.
                var primed = EditorJsonUtility.ToJson(optimizer);
                EditorJsonUtility.FromJsonOverwrite(primed, optimizer);

                var serializedObject = new SerializedObject(optimizer);
                serializedObject.FindProperty(
                    "_cutoutPreserveTransparencyMaxMipLevel").intValue = 6;
                serializedObject.FindProperty(
                    "_cutoutPreserveTransparencyMinTextureSize")
                    .intValue = 320;
                serializedObject.FindProperty(
                    "_cutoutMinimumOpaqueCoveragePercent").intValue = 55;
                serializedObject.FindProperty(
                    "_cutoutMinimumOpaqueCoverageToPreservePercent")
                    .intValue = 45;
                serializedObject.ApplyModifiedProperties();

                var json = EditorJsonUtility.ToJson(optimizer);
                EditorJsonUtility.FromJsonOverwrite(json, optimizer);

                Assert.That(
                    optimizer.CutoutPreserveTransparencyMaxMipLevel,
                    Is.EqualTo(6));
                Assert.That(
                    optimizer.CutoutPreserveTransparencyMinTextureSize,
                    Is.EqualTo(320));
                Assert.That(
                    optimizer.CutoutMinimumOpaqueCoveragePercent,
                    Is.EqualTo(55));
                Assert.That(
                    optimizer.CutoutMinimumOpaqueCoverageToPreservePercent,
                    Is.EqualTo(45));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DefaultCutoutPreserveTransparencyMaxMipLevelIs3()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(
                    optimizer.CutoutPreserveTransparencyMaxMipLevel,
                    Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// --- Falsifier 1: an implementation that changes only the field
        /// initializer to 3 without Reset. The first serialization round trip
        /// runs the upgrade backfill and copies the transparent value over
        /// the fresh 3, so the post-round-trip assertion fails. ---
        /// </summary>
        [Test]
        public void FreshCutoutDefaultSurvivesASerializationRoundTrip()
        {
            var root = new GameObject("Root");
            try
            {
                var fresh = root.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(fresh.CutoutPreserveTransparencyMaxMipLevel,
                    Is.EqualTo(3), "the fresh default before the round trip");

                var json = JsonUtility.ToJson(fresh);
                var scratch = new GameObject("Scratch")
                    .AddComponent<AmuseAvatarOptimizer>();
                try
                {
                    JsonUtility.FromJsonOverwrite(json, scratch);
                    Assert.That(scratch.CutoutPreserveTransparencyMaxMipLevel,
                        Is.EqualTo(3), "the fresh default survives the round trip");
                    // No-op guard: pinning the source component too, so a later
                    // rewrite cannot silence this test by touching only one arm.
                    Assert.That(fresh.CutoutPreserveTransparencyMaxMipLevel,
                        Is.EqualTo(3));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(scratch);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DefaultCutoutPreserveTransparencyMinTextureSizeIs128()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(
                    optimizer.CutoutPreserveTransparencyMinTextureSize,
                    Is.EqualTo(128));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DefaultCutoutMinimumOpaqueCoveragePercentIs25()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(
                    optimizer.CutoutMinimumOpaqueCoveragePercent,
                    Is.EqualTo(25));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DefaultCutoutMinimumOpaqueCoverageToPreservePercentIs5()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(
                    optimizer.CutoutMinimumOpaqueCoverageToPreservePercent,
                    Is.EqualTo(5));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CutoutPreserveTransparencyMaxMipLevelSerializedPropertyRoundTrips()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();

                // A first round trip runs the one-time upgrade backfill,
                // so this serialized edit survives later applies.
                var primed = EditorJsonUtility.ToJson(optimizer);
                EditorJsonUtility.FromJsonOverwrite(primed, optimizer);

                var serializedObject = new SerializedObject(optimizer);
                var property = serializedObject.FindProperty(
                    "_cutoutPreserveTransparencyMaxMipLevel");
                Assert.That(property, Is.Not.Null);
                property.intValue = 7;
                serializedObject.ApplyModifiedProperties();

                Assert.That(
                    optimizer.CutoutPreserveTransparencyMaxMipLevel,
                    Is.EqualTo(7));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CutoutPreserveTransparencyMinTextureSizeSerializedPropertyRoundTrips()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();

                // A first round trip runs the one-time upgrade backfill,
                // so this serialized edit survives later applies.
                var primed = EditorJsonUtility.ToJson(optimizer);
                EditorJsonUtility.FromJsonOverwrite(primed, optimizer);

                var serializedObject = new SerializedObject(optimizer);
                var property = serializedObject.FindProperty(
                    "_cutoutPreserveTransparencyMinTextureSize");
                Assert.That(property, Is.Not.Null);
                property.intValue = 512;
                serializedObject.ApplyModifiedProperties();

                Assert.That(
                    optimizer.CutoutPreserveTransparencyMinTextureSize,
                    Is.EqualTo(512));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CutoutMinimumOpaqueCoveragePercentSerializedPropertyRoundTrips()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();

                // A first round trip runs the one-time upgrade backfill,
                // so this serialized edit survives later applies.
                var primed = EditorJsonUtility.ToJson(optimizer);
                EditorJsonUtility.FromJsonOverwrite(primed, optimizer);

                var serializedObject = new SerializedObject(optimizer);
                var property = serializedObject.FindProperty(
                    "_cutoutMinimumOpaqueCoveragePercent");
                Assert.That(property, Is.Not.Null);
                property.intValue = 40;
                serializedObject.ApplyModifiedProperties();

                Assert.That(
                    optimizer.CutoutMinimumOpaqueCoveragePercent,
                    Is.EqualTo(40));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CutoutMinimumOpaqueCoverageToPreservePercentSerializedPropertyRoundTrips()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();

                // A first round trip runs the one-time upgrade backfill,
                // so this serialized edit survives later applies.
                var primed = EditorJsonUtility.ToJson(optimizer);
                EditorJsonUtility.FromJsonOverwrite(primed, optimizer);

                var serializedObject = new SerializedObject(optimizer);
                var property = serializedObject.FindProperty(
                    "_cutoutMinimumOpaqueCoverageToPreservePercent");
                Assert.That(property, Is.Not.Null);
                property.intValue = 40;
                serializedObject.ApplyModifiedProperties();

                Assert.That(
                    optimizer.CutoutMinimumOpaqueCoverageToPreservePercent,
                    Is.EqualTo(40));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}