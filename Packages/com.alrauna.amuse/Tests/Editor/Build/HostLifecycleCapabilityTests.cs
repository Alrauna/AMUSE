using System;
using Alrauna.Amuse.Editor.Build;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    public sealed class HostLifecycleCapabilityTests
    {
        private static HostLifecycleFacts SupportedFacts(
            string unityVersion = "2022.3.22f1",
            string ndmfVersion = "1.14.4",
            string vrchatSdkBaseVersion = "3.10.4",
            string vrchatSdkAvatarsVersion = "3.10.4",
            string platformQualifiedName = "nadena.dev.ndmf.vrchat.avatar3",
            AmuseBuildPath buildPath = AmuseBuildPath.NonPlayNdmfBuild,
            bool hasAssetSaver = true,
            bool hasAssetContainer = true,
            bool hasObjectRegistry = true,
            bool hasErrorReport = true)
        {
            return new HostLifecycleFacts(
                unityVersion,
                ndmfVersion,
                vrchatSdkBaseVersion,
                vrchatSdkAvatarsVersion,
                platformQualifiedName,
                buildPath,
                hasAssetSaver,
                hasAssetContainer,
                hasObjectRegistry,
                hasErrorReport);
        }

        [Test]
        public void ExactNonPlayNdmfContractPermitsPositiveMutation()
        {
            var result = HostLifecycleCapability.Evaluate(SupportedFacts());

            Assert.That(result.MayUsePositiveMutation, Is.True);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
            StringAssert.Contains("Unity 2022.3.22f1", result.SupportedAssumption);
        }

        [Test]
        public void DifferentUnityVersionRefusesWithUnityReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(unityVersion: "2022.3.22f2"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedUnityVersion));
        }

        [Test]
        public void DifferentNdmfVersionRefusesWithNdmfReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.14.5"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedNdmfVersion));
        }

        /// <summary>
        /// The version lookup returns null when the package is not registered at
        /// all, which is how a missing NDMF install reaches evaluation: Unity
        /// 2022.3.22f1 has no PackageInfo.FindForPackageName, so the capture
        /// selects by exact ordinal name over GetAllRegisteredPackages() and
        /// yields null on a miss. An absent package is not a supported host.
        /// </summary>
        [Test]
        public void MissingNdmfPackageRefusesWithNdmfReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: null));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedNdmfVersion));
        }

        [Test]
        public void MissingFactsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(
                () => HostLifecycleCapability.Evaluate(null));
        }

        [Test]
        public void DifferentVrchatSdkBaseVersionRefusesWithBaseReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(vrchatSdkBaseVersion: "3.10.5"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(HostLifecycleRefusal.UnsupportedVrchatSdkBaseVersion));
        }

        [Test]
        public void DifferentVrchatSdkAvatarsVersionRefusesWithAvatarsReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(vrchatSdkAvatarsVersion: "3.10.5"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(HostLifecycleRefusal.UnsupportedVrchatSdkAvatarsVersion));
        }

        [Test]
        public void GenericPlatformRefusesWithPlatformReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(platformQualifiedName: "nadena.dev.ndmf.generic"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedPlatform));
        }

        [Test]
        public void ApplyOnPlayRefusesWithLifecycleReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(buildPath: AmuseBuildPath.ApplyOnPlay));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedBuildPath));
        }

        [Test]
        public void UnknownBuildPathRefusesWithLifecycleReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(buildPath: AmuseBuildPath.Unknown));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedBuildPath));
        }

        [Test]
        public void MissingAssetSaverRefusesWithServicesReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(hasAssetSaver: false));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.MissingBuildContextServices));
        }

        [Test]
        public void MissingAssetContainerRefusesWithServicesReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(hasAssetContainer: false));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.MissingBuildContextServices));
        }

        [Test]
        public void MissingObjectRegistryRefusesWithServicesReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(hasObjectRegistry: false));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.MissingBuildContextServices));
        }

        [Test]
        public void MissingErrorReportRefusesWithServicesReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(hasErrorReport: false));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.MissingBuildContextServices));
        }
    }
}
