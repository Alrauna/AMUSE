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
        public void ApplyWritesAllTwelveFields()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();

                // Move every field away from the shipped values first,
                // so the apply must write each one back.
                var serialized = new UnityEditor.SerializedObject(optimizer);
                serialized.FindProperty("_alphaSeparatorEnabled")
                    .boolValue = false;
                serialized.FindProperty("_preserveTransparencyMaxMipLevel")
                    .intValue = 1;
                serialized.FindProperty("_preserveTransparencyMinTextureSize")
                    .intValue = 256;
                serialized.FindProperty("_minimumOpaqueCoveragePercent")
                    .intValue = 50;
                serialized.FindProperty(
                        "_minimumOpaqueCoverageToPreservePercent")
                    .intValue = 75;
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
                    .boolValue = true;
                serialized.FindProperty(
                        "_acceptMobileAnisotropyDifferences")
                    .boolValue = true;
                serialized.ApplyModifiedProperties();

                var preset = OptimizerPresetTests.ShippedDefaults();
                serialized.Update();
                PresetApplier.Apply(preset, serialized);

                Assert.That(optimizer.AlphaSeparatorEnabled, Is.True);
                Assert.That(optimizer.PreserveTransparencyMaxMipLevel,
                    Is.EqualTo(4));
                Assert.That(optimizer.PreserveTransparencyMinTextureSize,
                    Is.EqualTo(128));
                Assert.That(optimizer.MinimumOpaqueCoveragePercent,
                    Is.EqualTo(25));
                Assert.That(optimizer.MinimumOpaqueCoverageToPreservePercent,
                    Is.EqualTo(0));
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
                    Is.False,
                    "the preset must revoke desktop acceptance");
                Assert.That(optimizer.AcceptMobileAnisotropyDifferences,
                    Is.False,
                    "the preset must revoke mobile acceptance");
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
