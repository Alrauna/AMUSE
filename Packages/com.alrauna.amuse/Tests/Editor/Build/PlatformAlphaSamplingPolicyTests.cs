using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using NUnit.Framework;
using UnityEditor;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The exact active-target selection. The desktop group follows only
    /// the desktop acceptance value, the mobile group follows only the
    /// mobile acceptance value, and every unsupported target refuses
    /// selection under every acceptance pair while keeping the
    /// conservative mobile value in the output slot. Selection success
    /// on a mobile target never unlocks product texture capture: the
    /// existing capture admission keeps its own Windows-only gate.
    /// </summary>
    public sealed class PlatformAlphaSamplingPolicyTests
    {
        [Test]
        public void DesktopGroupFollowsOnlyTheDesktopAcceptanceValue()
        {
            Assert.That(
                PlatformAlphaSamplingPolicy.TrySelect(
                    BuildTarget.StandaloneWindows64,
                    false, false, out var withoutAcceptance),
                Is.True);
            Assert.That(
                withoutAcceptance,
                Is.EqualTo(AlphaSamplingPolicy.ConservativeDesktop));

            Assert.That(
                PlatformAlphaSamplingPolicy.TrySelect(
                    BuildTarget.StandaloneWindows64,
                    true, false, out var accepted),
                Is.True);
            Assert.That(
                accepted,
                Is.EqualTo(AlphaSamplingPolicy.AcceptedNonAnisotropic));

            Assert.That(
                PlatformAlphaSamplingPolicy.TrySelect(
                    BuildTarget.StandaloneWindows64,
                    false, true, out var mobileLeak),
                Is.True);
            Assert.That(
                mobileLeak,
                Is.EqualTo(AlphaSamplingPolicy.ConservativeDesktop),
                "the mobile acceptance value must not widen the " +
                "desktop proof");
        }

        [Test]
        public void MobileGroupFollowsOnlyTheMobileAcceptanceValue()
        {
            var mobileTargets = new[]
            {
                BuildTarget.Android,
                BuildTarget.iOS,
            };
            foreach (var target in mobileTargets)
            {
                Assert.That(
                    PlatformAlphaSamplingPolicy.TrySelect(
                        target, false, false, out var withoutAcceptance),
                    Is.True,
                    target + " must select a policy without acceptance");
                Assert.That(
                    withoutAcceptance,
                    Is.EqualTo(AlphaSamplingPolicy.ConservativeMobile),
                    target + " without acceptance stays conservative");

                Assert.That(
                    PlatformAlphaSamplingPolicy.TrySelect(
                        target, false, true, out var accepted),
                    Is.True,
                    target + " must select a policy with acceptance");
                Assert.That(
                    accepted,
                    Is.EqualTo(AlphaSamplingPolicy.AcceptedNonAnisotropic),
                    target + " with acceptance selects the accepted " +
                    "policy");

                Assert.That(
                    PlatformAlphaSamplingPolicy.TrySelect(
                        target, true, false, out var desktopLeak),
                    Is.True,
                    target + " must select a policy with desktop " +
                    "acceptance stored");
                Assert.That(
                    desktopLeak,
                    Is.EqualTo(AlphaSamplingPolicy.ConservativeMobile),
                    "the desktop acceptance value must not widen the " +
                    target + " proof");
            }
        }

        [Test]
        public void UnsupportedTargetsRefuseSelectionUnderEveryAcceptancePair()
        {
            var unsupportedTargets = new[]
            {
                BuildTarget.StandaloneOSX,
                BuildTarget.StandaloneLinux64,
                BuildTarget.WebGL,
            };
            foreach (var target in unsupportedTargets)
            {
                foreach (var desktop in new[] { false, true })
                {
                    foreach (var mobile in new[] { false, true })
                    {
                        Assert.That(
                            PlatformAlphaSamplingPolicy.TrySelect(
                                target, desktop, mobile, out var policy),
                            Is.False,
                            target + " must refuse selection with " +
                            "desktop " + desktop + " and mobile " + mobile);
                        Assert.That(
                            policy,
                            Is.EqualTo(
                                AlphaSamplingPolicy.ConservativeMobile),
                            "a refused selection keeps the conservative " +
                            "output value");
                    }
                }
            }
        }

        [Test]
        public void MobileSelectionDoesNotUnlockProductTextureCapture()
        {
            // The control: the admitted desktop target admits capture.
            Assert.That(
                UnityAlphaFieldEvidence.IsAdmittedBuildTarget(
                    BuildTarget.StandaloneWindows64),
                Is.True);

            foreach (var target in new[]
                     {
                         BuildTarget.Android,
                         BuildTarget.iOS,
                     })
            {
                // A successful selection on a mobile target is a
                // configuration fact only. The existing product
                // texture-capture admission gate still refuses, so
                // policy selection cannot bypass the mobile capture
                // boundary.
                Assert.That(
                    PlatformAlphaSamplingPolicy.TrySelect(
                        target, true, true, out _),
                    Is.True,
                    target + " selection succeeds and must stay a " +
                    "configuration fact only");
                Assert.That(
                    UnityAlphaFieldEvidence.IsAdmittedBuildTarget(target),
                    Is.False,
                    target + " must still fail the product texture " +
                    "capture admission gate");
            }
        }
    }
}
