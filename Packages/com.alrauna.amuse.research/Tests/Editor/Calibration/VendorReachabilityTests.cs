using System.Linq;
using Alrauna.Amuse.Research.Tests.Editor.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Research.Tests.Editor.Calibration
{
    /// <summary>
    /// Reachability, not counting. Every case here drives the production
    /// overload of RendererObservationBuilder - no semantics provider - so a
    /// pass means AMUSE reaches the outcome in a real project, not that the
    /// collector counts a substituted result correctly.
    /// CollectorSeamCountingTests makes the counting claim; conflating the two
    /// would let a census report near-total SemanticsUnknown and call it a pass.
    /// </summary>
    public sealed class VendorReachabilityTests
    {
        private CollectorTestScene _scene;

        [SetUp]
        public void SetUp() => _scene = new CollectorTestScene();

        [TearDown]
        public void TearDown() => _scene.Destroy();

        [Test]
        public void ProbeReportsBothAttestedFamilies()
        {
            var probed = CensusVendorProbe.ProbeAll();

            Assert.That(probed.Count, Is.EqualTo(2));
            Assert.That(
                probed.Select(p => p.ExpectedPackageName),
                Is.EquivalentTo(
                    new[] { "com.poiyomi.toon", "jp.lilxyzw.liltoon" }));
        }

        [Test]
        public void AnAbsentFamilyReportsAbsenceRatherThanThrowing()
        {
            foreach (var presence in CensusVendorProbe.ProbeAll())
            {
                if (!presence.IsInstalled)
                {
                    Assert.That(presence.Shader, Is.Null);
                    Assert.That(presence.InstalledPackageVersion, Is.Null);
                }
                else
                {
                    Assert.That(presence.Shader, Is.Not.Null);
                }
            }
        }

        /// <summary>
        /// Gate case 1. Attestation is exact-version: PoiyomiMaterialSemantics
        /// pins 9.3.64 and LilToonSourceAttestation pins 2.3.4, and a mismatch
        /// makes every material of that family unattested. An installed family
        /// at the wrong version is therefore a gate failure, not a census
        /// result.
        /// </summary>
        [Test]
        public void AnInstalledFamilyMatchesTheVersionAmuseAttests()
        {
            foreach (var presence in CensusVendorProbe.ProbeAll())
            {
                if (!presence.IsInstalled)
                {
                    continue;
                }

                Assert.That(
                    presence.InstalledPackageVersion,
                    Is.EqualTo(presence.ExpectedPackageVersion),
                    presence.ExpectedPackageName
                    + " is installed at a version AMUSE does not attest. "
                    + "Every material of this family will be unattested, and a "
                    + "census run would measure the mismatch rather than AMUSE.");
            }
        }
    }
}
