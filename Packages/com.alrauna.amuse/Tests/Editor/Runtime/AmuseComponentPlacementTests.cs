using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Runtime;

namespace Alrauna.Amuse.Tests.Editor
{
    public sealed class AmuseComponentPlacementTests
    {
        [Test]
        public void ComponentWithoutParentIsOnHierarchyRoot()
        {
            var root = new GameObject("placement-root");
            var component = root.AddComponent<AmuseAvatarOptimizerStubHolder>();
            Assert.That(AmuseComponentPlacement.IsOnHierarchyRoot(component), Is.True);
        }

        [Test]
        public void ComponentWithParentIsRefused()
        {
            var root = new GameObject("placement-parent");
            var child = new GameObject("placement-child");
            child.transform.SetParent(root.transform);
            var component = child.AddComponent<AmuseAvatarOptimizerStubHolder>();
            Assert.That(AmuseComponentPlacement.IsOnHierarchyRoot(component), Is.False);
        }

        [Test]
        public void NullComponentIsRefused()
        {
            Assert.That(AmuseComponentPlacement.IsOnHierarchyRoot(null), Is.False);
        }

        private sealed class AmuseAvatarOptimizerStubHolder : MonoBehaviour
        {
        }
    }
}