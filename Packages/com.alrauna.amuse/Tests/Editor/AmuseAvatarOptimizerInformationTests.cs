using System;
using System.Linq;
using Alrauna.Amuse.Runtime;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor
{
    /// <summary>
    /// Pins the Avatar Optimizer component-information registration. The
    /// registry lookup is the contract that matters: when Avatar Optimizer
    /// is installed, its component registry must know the AMUSE component,
    /// or Trace and Optimize treats it as unknown and may remove or
    /// mishandle it before the AMUSE passes run at platform finish.
    /// <para>
    /// The test uses reflection only, so the test assembly keeps no
    /// compile-time Avatar Optimizer dependency. When the package is
    /// absent the registration is compiled out too, and the test reports
    /// a loud ignore instead of a silent pass.
    /// </para>
    /// </summary>
    public sealed class AmuseAvatarOptimizerInformationTests
    {
        private const string RegistryTypeName =
            "Anatawa12.AvatarOptimizer.APIInternal.ComponentInfoRegistry";

        [Test]
        public void AvatarOptimizerRegistryKnowsTheAmuseComponent()
        {
            var registryType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(RegistryTypeName))
                .FirstOrDefault(type => type != null);
            if (registryType == null)
            {
                Assert.Ignore(
                    "com.anatawa12.avatar-optimizer is not installed in this" +
                    " project; the component-information registration is" +
                    " compiled out and there is no registry to consult.");
            }

            var tryGetInformation = registryType.GetMethod(
                "TryGetInformation",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);
            Assert.That(
                tryGetInformation,
                Is.Not.Null,
                "Avatar Optimizer's component registry lookup was not found" +
                " by reflection; the registration contract needs a re-read" +
                " against this Avatar Optimizer version.");

            var parameters = new object[]
            {
                typeof(AmuseAvatarOptimizer),
                null,
            };
            var known = (bool)tryGetInformation!.Invoke(null, parameters)!;

            Assert.That(
                known,
                Is.True,
                "Avatar Optimizer's registry does not know the AMUSE" +
                " component; Trace and Optimize treats it as an unknown" +
                " component type and its assumptions about unknown" +
                " components do not hold for a build-time component that" +
                " processes after Avatar Optimizer.");
        }
    }
}
