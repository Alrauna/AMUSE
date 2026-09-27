using nadena.dev.ndmf;
using NUnit.Framework;
using Alrauna.Amuse.Editor.Build;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    public sealed class RegisteredSourceIdentityTests
    {
        [Test]
        public void Resolve_ReturnsTheRegisteredSourceObject()
        {
            var source = new GameObject("source");
            var clone = new GameObject("clone");
            var previous = ObjectRegistry.ActiveRegistry;
            ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, clone);

                Assert.That(
                    RegisteredSourceIdentity.Resolve(clone),
                    Is.EqualTo(source));
            }
            finally
            {
                ObjectRegistry.ActiveRegistry = previous;
                Object.DestroyImmediate(clone);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void Resolve_ReturnsNullForAnUnregisteredObject()
        {
            // Falsifier: the creating static lookup would fabricate an
            // entry here and later block a RegisterReplacedObject for
            // the same object. The read-only lookup must answer null.
            var loner = new GameObject("loner");
            var previous = ObjectRegistry.ActiveRegistry;
            ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
            try
            {
                Assert.That(
                    RegisteredSourceIdentity.Resolve(loner), Is.Null);
            }
            finally
            {
                ObjectRegistry.ActiveRegistry = previous;
                Object.DestroyImmediate(loner);
            }
        }
    }
}
