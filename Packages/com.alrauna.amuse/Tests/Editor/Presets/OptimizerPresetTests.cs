using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Runtime;
using Alrauna.Amuse.Editor.Presets;

namespace Alrauna.Amuse.Tests.Editor
{
    public sealed class OptimizerPresetTests
    {
        internal static OptimizerPreset ShippedDefaults()
        {
            return new OptimizerPreset(
                "Normal", "Shipped defaults.", true,
                4, 128, 25, 5, 100, 100, 100, true, true, true, true);
        }

        [Test]
        public void DefaultComponentMatchesTheShippedDefaults()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(ShippedDefaults().Matches(optimizer), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
        [Test]
        public void DefaultComponentMatchesTheShippedNormalPreset()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(PresetFileStore.TryLoadAll(
                        out var presets, out var failedFile,
                        out var refusal),
                    Is.True, failedFile + ": " + refusal);
                var normal = System.Linq.Enumerable.Single(
                    presets, p => p.Name == "Normal");
                Assert.That(normal.Matches(optimizer), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }


        [Test]
        public void FlippingAnyOneFieldValueBreaksTheMatch()
        {
            var preset = ShippedDefaults();
            var fields = new (string name, object value)[]
            {
                ("_alphaSeparatorEnabled", false),
                ("_preserveTransparencyMaxMipLevel", 5),
                ("_preserveTransparencyMinTextureSize", 256),
                ("_minimumOpaqueCoveragePercent", 30),
                ("_minimumOpaqueCoverageToPreservePercent", 10),
                ("_minimumOpaqueAlphaPercent", 90),
                ("_polygonAlphaUpperClampPercent", 80),
                ("_polygonMinimumOpaqueCoveragePercent", 70),
                ("_allowDepthTestChange", false),
                ("_ignoreOutOfRangeMaterialSlots", false),
                ("_acceptDesktopAnisotropyDifferences", false),
                ("_acceptMobileAnisotropyDifferences", false),
            };

            foreach (var field in fields)
            {
                var go = new GameObject("Probe " + field.name);
                try
                {
                    var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                    Assert.That(preset.Matches(optimizer), Is.True,
                        field.name + " starts matching");

                    var serialized =
                        new UnityEditor.SerializedObject(optimizer);
                    var property = serialized.FindProperty(field.name);
                    Assert.That(property, Is.Not.Null, field.name);
                    if (property.propertyType ==
                        UnityEditor.SerializedPropertyType.Boolean)
                    {
                        property.boolValue = (bool)field.value;
                    }
                    else
                    {
                        property.intValue = (int)field.value;
                    }
                    serialized.ApplyModifiedProperties();

                    Assert.That(preset.Matches(optimizer), Is.False,
                        "flipping " + field.name + " breaks the match");
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
        }
    }
}
