using Alrauna.Amuse.Tests.Editor.Shared;
using nadena.dev.ndmf;
using NUnit.Framework;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    public sealed class RegisteredSourceIdentityTests
    {
        [Test]
        public void Resolve_ReturnsTheRegisteredSourceObject()
        {
            var source = new GameObject("source");
            var clone = new GameObject("clone");
            var registryGuard = new ObjectRegistryGuard();
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, clone);

                Assert.That(
                    RegisteredSourceIdentity.Resolve(clone),
                    Is.EqualTo(source));
            }
            finally
            {
                registryGuard.Dispose();
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
            var registryGuard = new ObjectRegistryGuard();
            try
            {
                Assert.That(
                    RegisteredSourceIdentity.Resolve(loner), Is.Null);
            }
            finally
            {
                registryGuard.Dispose();
                Object.DestroyImmediate(loner);
            }
        }
    }
}
