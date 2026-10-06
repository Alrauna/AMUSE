using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Runtime;
using Alrauna.Amuse.Editor.Presets;

namespace Alrauna.Amuse.Tests.Editor
{
    public sealed class PresetApplierTests
    {
        [Test]
        public void ApplyWritesAllNineFields()
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
                Assert.That(optimizer.MinimumOpaqueAlphaPercent,
                    Is.EqualTo(100));
                Assert.That(optimizer.PolygonAlphaUpperClampPercent,
                    Is.EqualTo(100));
                Assert.That(optimizer.PolygonMinimumOpaqueCoveragePercent,
                    Is.EqualTo(100));
                Assert.That(optimizer.AllowDepthTestChange, Is.True);
                Assert.That(optimizer.IgnoreOutOfRangeMaterialSlots,
                    Is.True);
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
