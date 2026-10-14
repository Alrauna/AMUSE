using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    /// <summary>
    /// Snapshots the float-valued properties of a fixture material's
    /// shader so a conversion test can prove its source was left
    /// untouched, queue and RenderType tag included.
    /// </summary>
    internal static class PropertySnapshot
    {
        /// <summary>
        /// Every float-valued property the shader declares, plus the two
        /// non-property facts. Used to prove the source is untouched.
        /// </summary>
        internal static Dictionary<string, float> SnapshotFloats(Material material)
        {
            var snapshot = new Dictionary<string, float>();
            var count = ShaderUtil.GetPropertyCount(material.shader);
            for (var index = 0; index < count; index++)
            {
                var type = ShaderUtil.GetPropertyType(material.shader, index);
                if (type != ShaderUtil.ShaderPropertyType.Float &&
                    type != ShaderUtil.ShaderPropertyType.Range)
                {
                    continue;
                }

                var name = ShaderUtil.GetPropertyName(material.shader, index);
                snapshot[name] = material.GetFloat(name);
            }

            return snapshot;
        }

        internal static void AssertUnchanged(
            Material material,
            Dictionary<string, float> before,
            int queueBefore,
            string renderTypeBefore)
        {
            var after = SnapshotFloats(material);
            CollectionAssert.AreEquivalent(before.Keys, after.Keys);
            foreach (var entry in before)
            {
                Assert.That(
                    after[entry.Key],
                    Is.EqualTo(entry.Value),
                    $"Source property '{entry.Key}' was mutated.");
            }

            Assert.That(material.renderQueue, Is.EqualTo(queueBefore));
            Assert.That(
                material.GetTag("RenderType", false), Is.EqualTo(renderTypeBefore));
        }
    }
}
