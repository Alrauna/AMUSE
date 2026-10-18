using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Semantics.LilToon;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Characterization
{
    /// <summary>
    /// Architectural question — is a neutral or zero claim ever made without
    /// proving the independent writers off?
    ///
    /// A neutral claim (<c>Constant(1)</c> for alpha) asserts "nothing here
    /// affects this output". That is unsound while an independent writer is
    /// enabled, <b>regardless of whether the primary texture slot for that
    /// output is populated</b>. "The slot is empty" is not evidence that "the
    /// output is unaffected".
    ///
    /// Every gate list below is an explicit test-local copy of the reviewed
    /// names. Production arrays are private and are neither exposed nor
    /// reflected: a test that read the production list would pass vacuously if
    /// that list were ever emptied.
    /// </summary>
    public sealed class PoiyomiNeutralClaimGatingTests : PoiyomiFixtureTestBase
    {
        // Full reviewed list.
        private static readonly string[] AlphaCoverageGates =
        {
            "_AlphaToCoverage",
            "_AlphaSharpenedA2C",
            "_AlphaDithering",
            "_EnableDissolve",
            "_EnableUDIMDiscardOptions",
        };

        [Test]
        public void Alpha_ForcedOpaque_CoverageGateEnabled_IsNotClaimed(
            [ValueSource(nameof(AlphaCoverageGates))] string gate)
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 1f);
            material.SetFloat(gate, 1f);

            Assert.That(
                Interpret(material).Semantics.Alpha.IsComplete,
                Is.False,
                $"Alpha claimed Constant(1) with coverage gate '{gate}' enabled.");
        }
    }

    public sealed class LilToonNeutralClaimGatingTests : LilToonFixtureTestBase
    {
        // Full reviewed list.
        private static readonly string[] AlphaCoverageGates =
        {
            "_Invisible",
            "_UDIMDiscardCompile",
        };

        [Test]
        public void Alpha_CoverageGateEnabled_IsNotClaimed(
            [ValueSource(nameof(AlphaCoverageGates))] string gate)
        {
            var material = NewFixtureMaterial();
            material.SetFloat(gate, 1f);

            Assert.That(
                Interpret(material).Semantics.Alpha.IsComplete,
                Is.False,
                $"Alpha claimed Constant(1) with coverage gate '{gate}' enabled.");
        }
    }

    public sealed class LilToonTransparentNeutralClaimGatingTests
        : LilToonFixtureTestBase
    {
        // Full reviewed list. _UseDither is deliberately absent: it is
        // compiled out at LIL_RENDER 2 and is not a gate on this family.
        // _AlphaMaskMode is deliberately absent too: modes 1 and 2 are
        // interpreted mask composition, and mode 1 with the unassigned
        // white default provably claims constant alpha one. The refusing
        // mask configurations keep their own frontend tests. _UseMain2ndTex
        // and _UseMain3rdTex are deliberately absent too: the layers compose
        // onto the declared-default constant of the unassigned white main
        // texture under the 2026-09-26 design, and a bare toggle stays
        // alpha-neutral while no writer runs.
        private static readonly string[] AlphaCoverageGates =
        {
            "_Invisible",
            "_UDIMDiscardCompile",
            "_UDIMDiscardMode",
            "_ShiftBackfaceUV",
            "_UseParallax",
            "_IDMask1", "_IDMask2", "_IDMask3", "_IDMask4",
            "_IDMask5", "_IDMask6", "_IDMask7", "_IDMask8",
            "_IDMaskControlsDissolve",
        };

        [Test]
        public void Alpha_NoMainTex_CoverageGateEnabled_IsNotClaimed(
            [ValueSource(nameof(AlphaCoverageGates))] string gate)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat(gate, 1f);

            var result = LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentMaterial(
                    material, ColorSpace.Linear, AllFeatures);

            Assert.That(
                result.Semantics.Alpha.IsComplete,
                Is.False,
                gate + ": an enabled coverage writer must block the claim " +
                "even with no _MainTex assigned");
        }

        [Test]
        public void Alpha_NoMainTex_LayerToggle_ComposesTheDeclaredDefault()
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_UseMain2ndTex", 1f);

            var result = LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentMaterial(
                    material, ColorSpace.Linear, AllFeatures);

            // The layers compose onto the declared-default constant of the
            // unassigned white main texture (design 2026-09-26), so the
            // alpha claims instead of refusing. The bare toggle is not the
            // refusing feature.
            Assert.That(
                result.Semantics.Alpha.IsComplete,
                Is.True,
                "an enabled layer composes onto the declared-default " +
                "constant instead of refusing");
        }
    }
}
