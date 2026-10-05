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
                "Safe", "Shipped defaults.", true,
                4, 128, 25, 100, 100, 100, true, true);
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
        public void FlippingAnyOneFieldValueBreaksTheMatch()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();

                var serialized = new UnityEditor.SerializedObject(optimizer);
                serialized.FindProperty("_minimumOpaqueCoveragePercent")
                    .intValue = 30;
                serialized.ApplyModifiedProperties();

                Assert.That(ShippedDefaults().Matches(optimizer), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
