using System.Collections.Generic;
using Alrauna.Amuse.Editor.Build;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The D8 consent layer: range-admitted versions beyond the last
    /// re-attested maximum, and majors at or beyond the declared bound,
    /// require an explicit click-through on every build. Range refusals
    /// never prompt, and consent never overrides a refusal.
    /// </summary>
    public sealed class VersionConsentTests
    {
        private static HostLifecycleFacts SupportedFacts(
            string unityVersion = "2022.3.22f1",
            string ndmfVersion = "1.14.8",
            string vrchatSdkBaseVersion = "3.10.5",
            string vrchatSdkAvatarsVersion = "3.10.5")
        {
            return new HostLifecycleFacts(
                unityVersion,
                ndmfVersion,
                vrchatSdkBaseVersion,
                vrchatSdkAvatarsVersion,
                "nadena.dev.ndmf.vrchat.avatar3",
                AmuseBuildPath.NonPlayNdmfBuild,
                true,
                true,
                true,
                true);
        }

        [Test]
        public void UnityAboveAttestedPatchRequiresConsent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(unityVersion: "2022.3.23f1"));

            Assert.That(result.MayUsePositiveMutation, Is.True);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
            Assert.That(result.ConsentRequired, Is.True);
            Assert.That(result.ConsentSubjects.Count, Is.EqualTo(1));
            Assert.That(
                result.ConsentSubjects[0],
                Does.Contain("2022.3.23f1"));
        }

        [Test]
        public void UnityAtAttestedPatchIsSilent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(unityVersion: "2022.3.22f1"));

            Assert.That(result.ConsentRequired, Is.False);
            Assert.That(result.ConsentSubjects, Is.Empty);
        }

        [Test]
        public void NdmfAboveAttestedMaxRequiresConsent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.14.9"));

            Assert.That(result.ConsentRequired, Is.True);
            Assert.That(result.ConsentSubjects.Count, Is.EqualTo(1));
            Assert.That(result.ConsentSubjects[0], Does.Contain("1.14.9"));
        }

        [Test]
        public void NdmfAtAttestedMaxIsSilent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.14.8"));

            Assert.That(result.ConsentRequired, Is.False);
        }

        /// <summary>
        /// Every in-range version at or below the attested maximum sits in
        /// the re-attested interval, so 1.14.5 needs no consent.
        /// </summary>
        [Test]
        public void NdmfInRangeBelowAttestedMaxIsSilent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.14.5"));

            Assert.That(result.ConsentRequired, Is.False);
        }

        [Test]
        public void NdmfMajorAtDeclaredBoundRequiresConsentNotRefusal()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "2.0.0"));

            Assert.That(result.MayUsePositiveMutation, Is.True,
                "the declared major bound is a consent subject, not a " +
                "refusal (decision V8)");
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
            Assert.That(result.ConsentRequired, Is.True);
            Assert.That(result.ConsentSubjects[0], Does.Contain("2.0.0"));
        }

        [Test]
        public void NdmfMajorBeyondDeclaredBoundRequiresConsent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "2.1.0"));

            Assert.That(result.ConsentRequired, Is.True);
            Assert.That(result.ConsentSubjects[0], Does.Contain("2.1.0"));
        }

        [Test]
        public void SdkAboveAttestedMaxRequiresConsent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(
                    vrchatSdkBaseVersion: "3.10.6",
                    vrchatSdkAvatarsVersion: "3.10.6"));

            Assert.That(result.ConsentRequired, Is.True);
            Assert.That(result.ConsentSubjects.Count, Is.EqualTo(2));
        }

        [Test]
        public void SdkMajorAtDeclaredBoundRequiresConsentNotRefusal()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(
                    vrchatSdkBaseVersion: "4.0.0",
                    vrchatSdkAvatarsVersion: "4.0.0"));

            Assert.That(result.MayUsePositiveMutation, Is.True);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
            Assert.That(result.ConsentRequired, Is.True);
        }

        [Test]
        public void BelowFloorVersionStillRefusesWithoutConsent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.14.3"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedNdmfVersion));
            Assert.That(result.ConsentRequired, Is.False,
                "a range refusal never prompts: consent never overrides " +
                "a refusal");
        }

        [Test]
        public void PrereleaseVersionStillRefusesWithoutConsent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.15.0-beta.1"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.ConsentRequired, Is.False);
        }

        [Test]
        public void EmptySubjectsProceedWithoutAsking()
        {
            var asked = false;
            var result = VersionConsentDialog.ShouldProceed(
                new string[0], false,
                subjects =>
                {
                    asked = true;
                    return true;
                });

            Assert.That(result, Is.True);
            Assert.That(asked, Is.False,
                "nothing to ask about must never open a dialog");
        }

        [Test]
        public void BatchModeRefusesWithoutAsking()
        {
            var asked = false;
            var result = VersionConsentDialog.ShouldProceed(
                new[] { "NDMF 1.14.9 is newer than the last verified 1.14.8." },
                true,
                subjects =>
                {
                    asked = true;
                    return true;
                });

            Assert.That(result, Is.False,
                "batch mode has no user to ask, so it refuses");
            Assert.That(asked, Is.False);
        }

        [Test]
        public void PresenterDecisionPassesThrough()
        {
            Assert.That(
                VersionConsentDialog.ShouldProceed(
                    new[] { "subject" }, false, subjects => true),
                Is.True);
            Assert.That(
                VersionConsentDialog.ShouldProceed(
                    new[] { "subject" }, false, subjects => false),
                Is.False);
        }
    }
}