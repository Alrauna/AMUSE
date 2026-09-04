using System;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// Tests for the shared conversion-result vocabulary: the outcome enum's
    /// two members and the seventeen-member refusal lattice, stated literally
    /// so a silently added or removed member fails a test.
    /// </summary>
    public sealed class LilToonOpaqueConversionResultTests
    {
        /// <summary>
        /// The exact refusal vocabulary, stated literally so a silently added
        /// or removed member (in particular any <c>AlreadyOpaque</c>-flavored
        /// state, or a premultiply/outline member unreachable on the attested
        /// cutout shader) fails a test.
        /// </summary>
        private static readonly string[] ExpectedRefusalNames =
        {
            "None",
            "UnattestedMaterial",
            "ConversionPropertyAbsent",
            "ConversionPropertyNotFinite",
            "UnsupportedRenderQueue",
            "UnsupportedRenderType",
            "UnsupportedDepthComparison",
            "UnsupportedDepthWrite",
            "UnsupportedColorMask",
            "UnsupportedDepthOffset",
            "UnsupportedBlendEquation",
            "UnsupportedAlphaBlendEquation",
            "UnsupportedForwardAddBlendEquation",
            "ClipThresholdDiscardsOpaqueAlpha",
            "UnsupportedForwardAddAlphaBoost",
            "UnsupportedDistanceFade",
            "UnsupportedSubpassCutoff",
        };

        [Test]
        public void OutcomeEnum_HasNoAlreadyOpaqueMember()
        {
            var names = Enum.GetNames(typeof(LilToonOpaqueConversionOutcome));

            Assert.That(
                names,
                Is.EqualTo(new[] { "Refused", "Convertible" }),
                "An attested cutout source is never canonical-opaque, so " +
                "AlreadyOpaque must not exist (spec §9.3).");
            Assert.That(names, Has.No.Member("AlreadyOpaque"));
        }

        [Test]
        public void RefusalEnum_MatchesTheIndependentlyStatedVocabulary()
        {
            Assert.That(
                Enum.GetNames(typeof(LilToonOpaqueConversionRefusal)),
                Is.EqualTo(ExpectedRefusalNames));
        }
    }
}
