using System;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    /// <summary>
    /// Value-only projection coverage. A proof policy reads captured
    /// eligibility and rewrites only anisotropy. Filter and wrap always
    /// pass through, and undefined policy values stay programming
    /// errors, never unsupported results.
    /// </summary>
    public sealed class AlphaSamplingPolicyTests
    {
        // The three authored shapes the policies must tell apart: an
        // authored level above one, the documented authored level one
        // exception, and an authored level zero forced off.
        private static TextureSampling LevelAboveOne() =>
            new(
                TextureFilterMode.Trilinear,
                TextureWrapMode.Repeat,
                TextureAnisoMode.Anisotropic,
                forceAnisotropyEligible: true);

        private static TextureSampling LevelOne() =>
            new(
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp,
                TextureAnisoMode.None,
                forceAnisotropyEligible: true);

        private static TextureSampling LevelZero() =>
            new(
                TextureFilterMode.Point,
                TextureWrapMode.Clamp,
                TextureAnisoMode.None,
                forceAnisotropyEligible: false);

        [Test]
        public void ConservativeDesktopTrustsOnlyEligibleSamples()
        {
            var aboveOne = AlphaSamplingPolicy.ConservativeDesktop
                .ForProof(LevelAboveOne());
            Assert.That(aboveOne.Aniso, Is.EqualTo(TextureAnisoMode.Anisotropic));
            Assert.That(aboveOne.Filter, Is.EqualTo(TextureFilterMode.Trilinear));
            Assert.That(aboveOne.Wrap, Is.EqualTo(TextureWrapMode.Repeat));

            var levelOne = AlphaSamplingPolicy.ConservativeDesktop
                .ForProof(LevelOne());
            Assert.That(levelOne.Aniso, Is.EqualTo(TextureAnisoMode.Anisotropic));
            Assert.That(levelOne.Filter, Is.EqualTo(TextureFilterMode.Bilinear));
            Assert.That(levelOne.Wrap, Is.EqualTo(TextureWrapMode.Clamp));

            var levelZero = AlphaSamplingPolicy.ConservativeDesktop
                .ForProof(LevelZero());
            Assert.That(levelZero.Aniso, Is.EqualTo(TextureAnisoMode.None));
            Assert.That(levelZero.Filter, Is.EqualTo(TextureFilterMode.Point));
            Assert.That(levelZero.Wrap, Is.EqualTo(TextureWrapMode.Clamp));
        }

        [Test]
        public void ConservativeMobileAssumesEveryFootprint()
        {
            // Mobile conservatism covers the unresolved client override
            // of level zero, so even an ineligible authored shape
            // projects a conservative anisotropic footprint.
            var levelZero = AlphaSamplingPolicy.ConservativeMobile
                .ForProof(LevelZero());
            Assert.That(levelZero.Aniso, Is.EqualTo(TextureAnisoMode.Anisotropic));
            Assert.That(levelZero.Filter, Is.EqualTo(TextureFilterMode.Point));
            Assert.That(levelZero.Wrap, Is.EqualTo(TextureWrapMode.Clamp));

            var aboveOne = AlphaSamplingPolicy.ConservativeMobile
                .ForProof(LevelAboveOne());
            Assert.That(aboveOne.Aniso, Is.EqualTo(TextureAnisoMode.Anisotropic));
        }

        [Test]
        public void AcceptedNonAnisotropicDropsEveryFootprint()
        {
            var aboveOne = AlphaSamplingPolicy.AcceptedNonAnisotropic
                .ForProof(LevelAboveOne());
            Assert.That(aboveOne.Aniso, Is.EqualTo(TextureAnisoMode.None));
            Assert.That(aboveOne.Filter, Is.EqualTo(TextureFilterMode.Trilinear));
            Assert.That(aboveOne.Wrap, Is.EqualTo(TextureWrapMode.Repeat));

            var levelZero = AlphaSamplingPolicy.AcceptedNonAnisotropic
                .ForProof(LevelZero());
            Assert.That(levelZero.Aniso, Is.EqualTo(TextureAnisoMode.None));
        }

        [Test]
        public void UndefinedPolicyValueThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ((AlphaSamplingPolicy)99).ForProof(LevelOne()));
        }

        [Test]
        public void ImpossibleAnisotropicEligibilityIsAnInvariantError()
        {
            // An authored sample that samples anisotropically while
            // being ineligible for forced anisotropy cannot exist, so
            // the captured value refuses to represent it.
            Assert.Throws<InvalidOperationException>(() =>
                new TextureSampling(
                    TextureFilterMode.Point,
                    TextureWrapMode.Clamp,
                    TextureAnisoMode.Anisotropic,
                    forceAnisotropyEligible: false));
        }
    }
}
