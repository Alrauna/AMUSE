using System.Linq;
using NUnit.Framework;
using UnityEngine;
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
            Assert.That(attribute.componentMenu, Is.EqualTo("AMUSE/Avatar Optimizer"));
        }
    }
}