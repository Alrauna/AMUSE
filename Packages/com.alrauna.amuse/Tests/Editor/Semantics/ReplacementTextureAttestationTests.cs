using System;
using System.Collections.Generic;
using NUnit.Framework;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    public sealed class ReplacementTextureAttestationTests
    {
        [Test]
        public void ShippedAdmittedSetIsEmptyAndRefusesEveryVersion()
        {
            Assert.That(
                ReplacementTextureAttestation.AdmittedVersions, Is.Empty,
                "the set ships empty; a version joins only after a " +
                "dated characterization");
            Assert.That(
                ReplacementTextureAttestation.IsVersionAdmitted("0.9.0"),
                Is.False);
        }

        [Test]
        public void InjectedVersionIsAdmittedAndOtherVersionIsRefused()
        {
            ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
            Assert.That(
                ReplacementTextureAttestation.IsVersionAdmitted("0.9.0"),
                Is.True);
            Assert.That(
                ReplacementTextureAttestation.IsVersionAdmitted("0.1.0"),
                Is.False);
        }

        [Test]
        public void NullOrEmptyVersionIsNeverAdmitted()
        {
            ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
            Assert.That(
                ReplacementTextureAttestation.IsVersionAdmitted(null), Is.False);
            Assert.That(
                ReplacementTextureAttestation.IsVersionAdmitted(""), Is.False);
        }

        [Test]
        public void ReadableProviderAnswerSatisfiesTheInstalledVersionRead()
        {
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "1.2.3";
            Assert.That(
                ReplacementTextureAttestation.TryReadInstalledProducerVersion(
                    out var version),
                Is.True);
            Assert.That(version, Is.EqualTo("1.2.3"));
        }

        [Test]
        public void UnreadableInstalledVersionRefusesTheRead()
        {
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => null;
            Assert.That(
                ReplacementTextureAttestation.TryReadInstalledProducerVersion(
                    out _),
                Is.False,
                "an unreadable version refuses, following the lilToon " +
                "baker precedent");
        }

        [Test]
        public void ResetForTestsRestoresTheEmptySetAndTheProductionProvider()
        {
            ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "9.9.9";
            ReplacementTextureAttestation.ResetForTests();
            Assert.That(
                ReplacementTextureAttestation.AdmittedVersions, Is.Empty);
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                null;
            // With the production provider restored, the delegate no
            // longer answers "9.9.9" for every name.
            ReplacementTextureAttestation.TryReadInstalledProducerVersion(
                out var version);
            Assert.That(version, Is.Not.EqualTo("9.9.9"));
        }
    }
}
