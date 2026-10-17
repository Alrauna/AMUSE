using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Runtime;
using Alrauna.Amuse.Editor.Presets;

namespace Alrauna.Amuse.Tests.Editor
{
    public sealed class PresetApplierTests
    {
        [Test]
        public void ApplyingShippedSafeGrantsBothAcceptanceSettings()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();

                // Revoke both acceptance settings first, so the shipped
                // Safe preset must actively grant them. No preset may
                // leave an earlier refusal behind.
                var serialized = new UnityEditor.SerializedObject(optimizer);
                serialized.FindProperty(
                        "_acceptDesktopAnisotropyDifferences")
                    .boolValue = false;
                serialized.FindProperty(
                        "_acceptMobileAnisotropyDifferences")
                    .boolValue = false;
                serialized.ApplyModifiedProperties();

                Assert.That(optimizer.AcceptDesktopAnisotropyDifferences,
                    Is.False, "setup must revoke desktop acceptance");
                Assert.That(optimizer.AcceptMobileAnisotropyDifferences,
                    Is.False, "setup must revoke mobile acceptance");

                Assert.That(PresetFileStore.TryLoadAll(
                        out var presets, out var failedFile,
                        out var refusal),
                    Is.True, failedFile + ": " + refusal);
                var safe = presets.Single(
                    preset => preset.Name == "Safe");

                serialized.Update();
                PresetApplier.Apply(safe, serialized);

                Assert.That(optimizer.AcceptDesktopAnisotropyDifferences,
                    Is.True,
                    "the shipped Safe preset must grant desktop " +
                    "acceptance");
                Assert.That(optimizer.AcceptMobileAnisotropyDifferences,
                    Is.True,
                    "the shipped Safe preset must grant mobile " +
                    "acceptance");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyingShippedAggressiveSetsSmallestTestedMipmapToThree()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                var serialized = new UnityEditor.SerializedObject(optimizer);
                serialized.FindProperty("_preserveTransparencyMaxMipLevel").intValue = 4;
                serialized.ApplyModifiedProperties();

                Assert.That(PresetFileStore.TryLoadAll(
                        out var presets, out var failedFile,
                        out var refusal),
                    Is.True, failedFile + ": " + refusal);
                var aggressive = presets.Single(
                    preset => preset.Name == "Aggressive");

                Assert.That(aggressive.PreserveTransparencyMaxMipLevel, Is.EqualTo(3));

                serialized.Update();
                PresetApplier.Apply(aggressive, serialized);

                Assert.That(optimizer.PreserveTransparencyMaxMipLevel,
                    Is.EqualTo(3),
                    "the shipped Aggressive preset must set smallest tested mipmap to mip 3");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyingEachShippedPresetWritesItsCutoutValues()
        {
            Assert.That(PresetFileStore.TryLoadAll(
                    out var presets, out var failedFile, out var refusal),
                Is.True, failedFile + ": " + refusal);

            var expected = new (string name, int mipCap, int size,
                int coverage, int preserve)[]
            {
                ("Safe", 4, 128, 25, 0),
                ("Normal", 4, 128, 25, 5),
                ("Aggressive", 3, 128, 25, 10),
            };

            foreach (var row in expected)
            {
                var go = new GameObject("Root " + row.name);
                try
                {
                    var optimizer =
                        go.AddComponent<AmuseAvatarOptimizer>();

                    // A first serialize and deserialize round trip runs
                    // the one-time upgrade backfill and sets its marker.
                    // Without it, every serialized apply re-runs the
                    // backfill and overwrites a fresh cutout edit.
                    var primed = UnityEditor.EditorJsonUtility.ToJson(
                        optimizer);
                    UnityEditor.EditorJsonUtility.FromJsonOverwrite(
                        primed, optimizer);

                    var serialized =
                        new UnityEditor.SerializedObject(optimizer);

                    // Push every cutout field away from the shipped
                    // value first, so the apply must write each one.
                    serialized.FindProperty(
                            "_cutoutPreserveTransparencyMaxMipLevel")
                        .intValue = 10;
                    serialized.FindProperty(
                            "_cutoutPreserveTransparencyMinTextureSize")
                        .intValue = 8192;
                    serialized.FindProperty(
                            "_cutoutMinimumOpaqueCoveragePercent")
                        .intValue = 100;
                    serialized.FindProperty(
                            "_cutoutMinimumOpaqueCoverageToPreservePercent")
                        .intValue = 100;
                    serialized.ApplyModifiedProperties();

                    var preset = presets.Single(
                        candidate => candidate.Name == row.name);
                    serialized.Update();
                    PresetApplier.Apply(preset, serialized);

                    Assert.That(
                        optimizer.CutoutPreserveTransparencyMaxMipLevel,
                        Is.EqualTo(row.mipCap),
                        row.name + " cutout mip cap");
                    Assert.That(
                        optimizer.CutoutPreserveTransparencyMinTextureSize,
                        Is.EqualTo(row.size),
                        row.name + " cutout texture size");
                    Assert.That(
                        optimizer.CutoutMinimumOpaqueCoveragePercent,
                        Is.EqualTo(row.coverage),
                        row.name + " cutout coverage percent");
                    Assert.That(optimizer.
                        CutoutMinimumOpaqueCoverageToPreservePercent,
                        Is.EqualTo(row.preserve),
                        row.name + " cutout preserve percent");
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        [Test]
        public void ApplyWritesAllSixteenFields()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();

                // A first serialize and deserialize round trip runs the
                // one-time upgrade backfill and sets its marker. Without
                // it, every serialized apply re-runs the backfill and
                // overwrites a fresh cutout edit.
                var primed = UnityEditor.EditorJsonUtility.ToJson(optimizer);
                UnityEditor.EditorJsonUtility.FromJsonOverwrite(
                    primed, optimizer);

                // Move every field away from the shipped values first,
                // so the apply must write each one back.
                var serialized = new UnityEditor.SerializedObject(optimizer);
                serialized.FindProperty("_alphaSeparatorEnabled")
                    .boolValue = false;
                serialized.FindProperty("_preserveTransparencyMaxMipLevel")
                    .intValue = 1;
                serialized.FindProperty(
                        "_cutoutPreserveTransparencyMaxMipLevel")
                    .intValue = 2;
                serialized.FindProperty("_preserveTransparencyMinTextureSize")
                    .intValue = 256;
                serialized.FindProperty(
                        "_cutoutPreserveTransparencyMinTextureSize")
                    .intValue = 512;
                serialized.FindProperty("_minimumOpaqueCoveragePercent")
                    .intValue = 50;
                serialized.FindProperty(
                        "_cutoutMinimumOpaqueCoveragePercent")
                    .intValue = 60;
                serialized.FindProperty(
                        "_minimumOpaqueCoverageToPreservePercent")
                    .intValue = 75;
                serialized.FindProperty(
                        "_cutoutMinimumOpaqueCoverageToPreservePercent")
                    .intValue = 65;
                serialized.FindProperty("_minimumOpaqueAlphaPercent")
                    .intValue = 90;
                serialized.FindProperty("_polygonAlphaUpperClampPercent")
                    .intValue = 80;
                serialized.FindProperty(
                        "_polygonMinimumOpaqueCoveragePercent")
                    .intValue = 70;
                serialized.FindProperty("_allowDepthTestChange")
                    .boolValue = false;
                serialized.FindProperty("_ignoreOutOfRangeMaterialSlots")
                    .boolValue = false;
                serialized.FindProperty(
                        "_acceptDesktopAnisotropyDifferences")
                    .boolValue = false;
                serialized.FindProperty(
                        "_acceptMobileAnisotropyDifferences")
                    .boolValue = false;
                serialized.ApplyModifiedProperties();

                var preset = OptimizerPresetTests.ShippedDefaults();
                serialized.Update();
                PresetApplier.Apply(preset, serialized);

                Assert.That(optimizer.AlphaSeparatorEnabled, Is.True);
                Assert.That(optimizer.PreserveTransparencyMaxMipLevel,
                    Is.EqualTo(4));
                Assert.That(optimizer.CutoutPreserveTransparencyMaxMipLevel,
                    Is.EqualTo(4));
                Assert.That(optimizer.PreserveTransparencyMinTextureSize,
                    Is.EqualTo(128));
                Assert.That(
                    optimizer.CutoutPreserveTransparencyMinTextureSize,
                    Is.EqualTo(128));
                Assert.That(optimizer.MinimumOpaqueCoveragePercent,
                    Is.EqualTo(25));
                Assert.That(optimizer.CutoutMinimumOpaqueCoveragePercent,
                    Is.EqualTo(25));
                Assert.That(optimizer.MinimumOpaqueCoverageToPreservePercent,
                    Is.EqualTo(5));
                Assert.That(
                    optimizer.CutoutMinimumOpaqueCoverageToPreservePercent,
                    Is.EqualTo(5));
                Assert.That(optimizer.MinimumOpaqueAlphaPercent,
                    Is.EqualTo(100));
                Assert.That(optimizer.PolygonAlphaUpperClampPercent,
                    Is.EqualTo(100));
                Assert.That(optimizer.PolygonMinimumOpaqueCoveragePercent,
                    Is.EqualTo(100));
                Assert.That(optimizer.AllowDepthTestChange, Is.True);
                Assert.That(optimizer.IgnoreOutOfRangeMaterialSlots,
                    Is.True);
                Assert.That(optimizer.AcceptDesktopAnisotropyDifferences,
                    Is.True,
                    "the preset must write desktop acceptance");
                Assert.That(optimizer.AcceptMobileAnisotropyDifferences,
                    Is.True,
                    "the preset must write mobile acceptance");
                Assert.That(optimizer.AmuseDisabled, Is.False,
                    "a preset never touches the master switch");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
