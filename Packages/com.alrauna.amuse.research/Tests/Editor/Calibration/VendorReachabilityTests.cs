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
    }
}
