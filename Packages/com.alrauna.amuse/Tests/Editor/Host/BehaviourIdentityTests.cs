using System;
using Alrauna.Amuse.Editor.Host;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    public sealed class BehaviourIdentityTests
    {
        [Test]
        public void IdentityIncludesPackageVersionAssemblyAndFullTypeName()
        {
            var type = typeof(CommittedControllerGraph);
            var package = PackageInfo.FindForAssembly(type.Assembly);
            Assert.That(package, Is.Not.Null,
                "the AMUSE Editor assembly must resolve to its package");

            Assert.That(BehaviourIdentity.Of(type), Is.EqualTo(
                package.name + "@" + package.version + "|" +
                type.Assembly.GetName().Name + "|" + type.FullName));
        }

        [Test]
        public void AssemblyWithoutPackageUsesExactNoPackageRepresentation()
        {
            var type = RuntimeProbeType();
            Assert.That(PackageInfo.FindForAssembly(type.Assembly), Is.Null,
                "fixture precondition: Assembly-CSharp must not be package-owned");

            Assert.That(BehaviourIdentity.Of(type), Is.EqualTo(
                "<no-package>|" + type.Assembly.GetName().Name + "|" +
                type.FullName));
        }

        [Test]
        public void AllowlistStartsEmpty()
        {
            Assert.That(BehaviourIdentity.AllowedIdentities, Is.Empty);
        }

        [Test]
        public void UnknownIdentityIsNotAllowed()
        {
            Assert.That(BehaviourIdentity.IsAllowed(
                "some.package@1.0.0|SomeAsm|SomeVendor.MysteryBehaviour"),
                Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        public void NullOrEmptyIdentityIsNotAllowed(string identity)
        {
            Assert.That(BehaviourIdentity.IsAllowed(identity), Is.False);
        }

        [Test]
        public void NullTypeIsProgrammerMisuse()
        {
            Assert.Throws<ArgumentNullException>(() => BehaviourIdentity.Of(null));
        }

        [Test]
        public void IdenticallyNamedTypeFromAnotherAssemblyCannotBeSpoofed()
        {
            var first = typeof(
                Alrauna.Amuse.TestFixtures.AMUSETask7StateMachineBehaviourProbe);
            var second = RuntimeProbeType();

            Assert.That(first.FullName, Is.EqualTo(second.FullName),
                "fixture precondition: the names must collide");
            Assert.That(first.Assembly.GetName().Name,
                Is.Not.EqualTo(second.Assembly.GetName().Name),
                "fixture precondition: the defining assemblies must differ");
            Assert.That(BehaviourIdentity.Of(first),
                Is.Not.EqualTo(BehaviourIdentity.Of(second)));
        }

        private static Type RuntimeProbeType()
        {
            return Type.GetType(
                "Alrauna.Amuse.TestFixtures." +
                "AMUSETask7StateMachineBehaviourProbe, Assembly-CSharp",
                true);
        }
    }
}

namespace Alrauna.Amuse.TestFixtures
{
    internal sealed class AMUSETask7StateMachineBehaviourProbe
    {
    }
}
