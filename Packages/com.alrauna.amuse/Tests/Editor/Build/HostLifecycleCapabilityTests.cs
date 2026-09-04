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
            StringAssert.Contains("NDMF 1.14.4", result.SupportedAssumption);
        }

        [Test]
        public void DifferentUnityVersionRefusesWithUnityReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(unityVersion: "2022.3.21f1"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedUnityVersion));
        }

        [Test]
        public void UnityAtPatchFloorPermitsPositiveMutation()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(unityVersion: "2022.3.22f1"));

            Assert.That(result.MayUsePositiveMutation, Is.True);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
        }

        /// <summary>
        /// The policy admits any 2022.3 f-release at or above patch 22. A
        /// leftover exact-equality branch beside the range check refuses
        /// this input.
        /// </summary>
        [Test]
        public void UnityAbovePatchFloorPermitsPositiveMutation()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(unityVersion: "2022.3.23f1"));

            Assert.That(result.MayUsePositiveMutation, Is.True);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
        }

        [Test]
        public void DifferentUnityStreamRefusesWithUnityReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(unityVersion: "2023.1.0f1"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedUnityVersion));
        }

        /// <summary>
        /// The digits after the release-type letter are part of the admitted
        /// grammar, so 2022.3.22f2 admits like 2022.3.22f1. A residual
        /// full-string equality refuses it.
        /// </summary>
        [Test]
        public void UnitySamePatchNewerRevisionPermitsPositiveMutation()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(unityVersion: "2022.3.22f2"));

            Assert.That(result.MayUsePositiveMutation, Is.True);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
        }

        [Test]
        public void UnityNonFReleaseTypeRefusesWithUnityReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(unityVersion: "2022.3.22b"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedUnityVersion));
        }

        [Test]
        public void UnparseableUnityVersionRefusesWithUnityReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(unityVersion: "not-a-version"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedUnityVersion));
        }

        [Test]
        public void NullUnityVersionRefusesWithUnityReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(unityVersion: null));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedUnityVersion));
        }

        [Test]
        public void DifferentNdmfVersionRefusesWithNdmfReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.14.3"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedNdmfVersion));
        }

        [Test]
        public void NdmfAtFloorVersionPermitsPositiveMutation()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.14.4"));

            Assert.That(result.MayUsePositiveMutation, Is.True);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
        }

        [Test]
        public void NdmfBelowMinorFloorRefusesWithNdmfReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.13.9"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedNdmfVersion));
        }

        [Test]
        public void NdmfPrereleaseSuffixRefusesWithNdmfReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.15.0-beta.1"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedNdmfVersion));
        }

        [Test]
        public void NdmfAtExclusiveUpperBoundRefusesWithNdmfReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "2.0.0"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedNdmfVersion));
        }

        [Test]
        public void NdmfPrereleaseAtUpperBoundRefusesWithNdmfReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "2.0.0-a"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedNdmfVersion));
        }

        /// <summary>
        /// 1.9.0 refuses only under numeric per-component comparison; an
        /// ordinal text sort places "1.9.0" above "1.14.4" and would admit it.
        /// </summary>
        [Test]
        public void NdmfNumericPerComponentCompareRefusesWithNdmfReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.9.0"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedNdmfVersion));
        }

        /// <summary>
        /// A two-component input is unparseable: Unity requires the
        /// MAJOR.MINOR.PATCH form for package versions, so 1.14 refuses
        /// with the NDMF cause instead of naming the 1.14 series.
        /// </summary>
        [Test]
        public void NdmfTwoComponentVersionRefusesWithNdmfReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.14"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedNdmfVersion));
        }

        /// <summary>
        /// A version above the floor admits without any residual exact
        /// equality at the floor value.
        /// </summary>
        [Test]
        public void NdmfAboveFloorVersionPermitsPositiveMutation()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.15.0"));

            Assert.That(result.MayUsePositiveMutation, Is.True);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
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
                SupportedFacts(vrchatSdkBaseVersion: "3.10.3"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(HostLifecycleRefusal.UnsupportedVrchatSdkBaseVersion));
        }

        [Test]
        public void VrchatSdkBaseAtFloorVersionPermitsPositiveMutation()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(vrchatSdkBaseVersion: "3.10.4"));

            Assert.That(result.MayUsePositiveMutation, Is.True);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
        }

        /// <summary>
        /// The SDK range admits every version at or above the 3.10.4 floor
        /// and below 4.0.0. A residual exact equality at the floor refuses
        /// this input.
        /// </summary>
        [Test]
        public void VrchatSdkBaseAboveFloorVersionPermitsPositiveMutation()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(vrchatSdkBaseVersion: "3.10.5"));

            Assert.That(result.MayUsePositiveMutation, Is.True);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
        }

        /// <summary>
        /// A two-component input is unparseable: Unity requires the
        /// MAJOR.MINOR.PATCH form for package versions, so 3.10 refuses
        /// with the Base cause instead of naming the 3.10 series.
        /// </summary>
        [Test]
        public void VrchatSdkBaseTwoComponentVersionRefusesWithBaseReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(vrchatSdkBaseVersion: "3.10"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(HostLifecycleRefusal.UnsupportedVrchatSdkBaseVersion));
        }

        [Test]
        public void VrchatSdkBaseAtExclusiveUpperBoundRefusesWithBaseReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(vrchatSdkBaseVersion: "4.0.0"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(HostLifecycleRefusal.UnsupportedVrchatSdkBaseVersion));
        }

        [Test]
        public void VrchatSdkBasePrereleaseSuffixRefusesWithBaseReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(vrchatSdkBaseVersion: "3.11.0-beta"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(HostLifecycleRefusal.UnsupportedVrchatSdkBaseVersion));
        }

        [Test]
        public void NullVrchatSdkBaseVersionRefusesWithBaseReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(vrchatSdkBaseVersion: null));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(HostLifecycleRefusal.UnsupportedVrchatSdkBaseVersion));
        }

        [Test]
        public void DifferentVrchatSdkAvatarsVersionRefusesWithAvatarsReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(vrchatSdkAvatarsVersion: "3.10.3"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(HostLifecycleRefusal.UnsupportedVrchatSdkAvatarsVersion));
        }

        [Test]
        public void VrchatSdkAvatarsAtFloorVersionPermitsPositiveMutation()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(vrchatSdkAvatarsVersion: "3.10.4"));

            Assert.That(result.MayUsePositiveMutation, Is.True);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
        }

        /// <summary>
        /// The SDK range admits every version at or above the 3.10.4 floor
        /// and below 4.0.0. A residual exact equality at the floor refuses
        /// this input.
        /// </summary>
        [Test]
        public void VrchatSdkAvatarsAboveFloorVersionPermitsPositiveMutation()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(vrchatSdkAvatarsVersion: "3.10.5"));

            Assert.That(result.MayUsePositiveMutation, Is.True);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
        }

        /// <summary>
        /// A two-component input is unparseable: Unity requires the
        /// MAJOR.MINOR.PATCH form for package versions, so 3.10 refuses
        /// with the Avatars cause instead of naming the 3.10 series.
        /// </summary>
        [Test]
        public void VrchatSdkAvatarsTwoComponentVersionRefusesWithAvatarsReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(vrchatSdkAvatarsVersion: "3.10"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(HostLifecycleRefusal.UnsupportedVrchatSdkAvatarsVersion));
        }

        [Test]
        public void VrchatSdkAvatarsAtExclusiveUpperBoundRefusesWithAvatarsReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(vrchatSdkAvatarsVersion: "4.0.0"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(HostLifecycleRefusal.UnsupportedVrchatSdkAvatarsVersion));
        }

        [Test]
        public void VrchatSdkAvatarsPrereleaseSuffixRefusesWithAvatarsReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(vrchatSdkAvatarsVersion: "3.11.0-beta"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(HostLifecycleRefusal.UnsupportedVrchatSdkAvatarsVersion));
        }

        [Test]
        public void NullVrchatSdkAvatarsVersionRefusesWithAvatarsReason()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(vrchatSdkAvatarsVersion: null));

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
