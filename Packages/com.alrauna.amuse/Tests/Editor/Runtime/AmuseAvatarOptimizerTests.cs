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
        public void DefaultIgnoreOutOfRangeMaterialSlotsIsFalse()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(optimizer.IgnoreOutOfRangeMaterialSlots, Is.False);
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
                var serializedObject = new SerializedObject(optimizer);
                var property = serializedObject.FindProperty("_ignoreOutOfRangeMaterialSlots");
                Assert.That(property, Is.Not.Null);
                property.boolValue = true;
                serializedObject.ApplyModifiedProperties();

                Assert.That(optimizer.IgnoreOutOfRangeMaterialSlots, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}