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
    /// A neutral claim (<c>Unmodified</c>, <c>Constant(0)</c>,
    /// <c>Constant(1)</c>) asserts "nothing here affects this output". That is
    /// unsound while an independent writer is enabled, <b>regardless of whether
    /// the primary texture slot for that output is populated</b>. "The slot is
    /// empty" is not evidence that "the output is unaffected".
    ///
    /// Across the two frontends there are eight such short-circuit sites. Seven
    /// gated correctly; Poiyomi's Normal did not, and that was confirmed against
    /// the pinned Poiyomi 9.3.64 source to be a real false positive:
    /// <c>_DetailEnabled</c> is a ThryToggle bound to the <c>FINALPASS</c>
    /// keyword, and the detail-normal blend it compiles in perturbs the
    /// tangent-space normal without reading <c>_BumpMap</c> at all.
    ///
    /// Every gate list below is an explicit test-local copy of the reviewed
    /// names. Production arrays are private and are neither exposed nor
    /// reflected: a test that read the production list would pass vacuously if
    /// that list were ever emptied.
    /// </summary>
    public sealed class PoiyomiNeutralClaimGatingTests : PoiyomiFixtureTestBase
    {
        // Full reviewed list — the gates under investigation.
        private static readonly string[] NormalFeatureGates =
        {
            "_DetailEnabled",
            "_RGBMaskEnabled",
            "_DecalEnabled",
            "_DecalEnabled1",
            "_DecalEnabled2",
            "_DecalEnabled3",
            "_PoiInternalParallax",
            "_PoiParallax",
        };

        // Full reviewed list.
        private static readonly string[] AlphaCoverageGates =
        {
            "_AlphaToCoverage",
            "_AlphaSharpenedA2C",
            "_AlphaDithering",
            "_EnableDissolve",
            "_EnableUDIMDiscardOptions",
        };

        // Full reviewed list of the higher emission slots.
        private static readonly string[] HigherEmissionSlots =
        {
            "_EnableEmission1",
            "_EnableEmission2",
            "_EnableEmission3",
        };

        // Explicit representative subset of the forty-entry base-colour writer
        // list. The full list is already exercised exhaustively by
        // PoiyomiBaseColorAlphaTests.BaseColorFeatureWriterEnabled_*; this case
        // tests the ordering property, not gate coverage.
        private static readonly string[] BaseColorWriterSample =
        {
            "_DetailEnabled",
            "_MatcapEnable",
            "_DecalEnabled",
            "_EnableRimLighting",
        };

        private static PoiyomiSemanticResult Interpret(Material material)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Linear);
        }

        [Test]
        public void BaseColor_NoMainTex_WriterEnabled_IsNotClaimed(
            [ValueSource(nameof(BaseColorWriterSample))] string gate)
        {
            var material = NewFixtureMaterial();
            material.SetFloat(gate, 1f);

            Assert.That(
                Interpret(material).Semantics.BaseColor.IsComplete,
                Is.False,
                $"BaseColor claimed a constant with '{gate}' enabled.");
        }

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

        [Test]
        public void Emission_SlotsOff_HigherSlotEnabled_IsNotClaimed(
            [ValueSource(nameof(HigherEmissionSlots))] string gate)
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_EnableEmission", 0f);
            material.SetFloat(gate, 1f);

            Assert.That(
                Interpret(material).Semantics.Emission.IsComplete,
                Is.False,
                $"Emission claimed Constant(0) with '{gate}' enabled.");
        }

        /// <summary>
        /// The case the milestone exists to fix. Expected RED before Task 8.
        /// </summary>
        [Test]
        public void Normal_NoBumpMap_WriterEnabled_IsNotClaimed(
            [ValueSource(nameof(NormalFeatureGates))] string gate)
        {
            var material = NewFixtureMaterial();
            material.SetFloat(gate, 1f);

            Assert.That(
                Interpret(material).Semantics.Normal.IsComplete,
                Is.False,
                $"Normal claimed Unmodified with normal writer '{gate}' enabled "
                    + "and no _BumpMap assigned. An unassigned slot is not "
                    + "evidence that the output is unaffected.");
        }
    }

    public sealed class LilToonNeutralClaimGatingTests : LilToonFixtureTestBase
    {
        // Full reviewed lists.
        private static readonly string[] BaseColorWriterGates =
        {
            "_Invisible",
            "_ShiftBackfaceUV",
            "_UseParallax",
            "_UsePOM",
            "_UseAudioLink",
            "_UseMain2ndTex",
            "_UseMain3rdTex",
            "_MainGradationStrength",
        };

        private static readonly string[] AlphaCoverageGates =
        {
            "_Invisible",
            "_UDIMDiscardCompile",
        };

        private static readonly string[] EmissiveWriterGates =
        {
            "_UseEmission2nd",
            "_UseReflection",
            "_UseMatCap",
            "_UseMatCap2nd",
            "_UseRim",
            "_UseRimShade",
            "_UseGlitter",
            "_UseBacklight",
            "_UseAudioLink",
        };

        private static readonly string[] NormalWriterGates =
        {
            "_UseBump2ndMap",
            "_UseAnisotropy",
            "_UseParallax",
            "_UsePOM",
            "_ShiftBackfaceUV",
        };

        [Test]
        public void BaseColor_NoMainTex_WriterEnabled_IsNotClaimed(
            [ValueSource(nameof(BaseColorWriterGates))] string gate)
        {
            var material = NewFixtureMaterial();
            material.SetFloat(gate, 1f);

            Assert.That(
                Interpret(material).Semantics.BaseColor.IsComplete,
                Is.False,
                $"BaseColor claimed a constant with '{gate}' enabled.");
        }

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

        [Test]
        public void Emission_Disabled_WriterEnabled_IsNotClaimed(
            [ValueSource(nameof(EmissiveWriterGates))] string gate)
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 0f);
            material.SetFloat(gate, 1f);

            Assert.That(
                Interpret(material).Semantics.Emission.IsComplete,
                Is.False,
                $"Emission claimed Constant(0) with '{gate}' enabled.");
        }

        [Test]
        public void Normal_BumpMapDisabled_WriterEnabled_IsNotClaimed(
            [ValueSource(nameof(NormalWriterGates))] string gate)
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseBumpMap", 0f);
            material.SetFloat(gate, 1f);

            Assert.That(
                Interpret(material).Semantics.Normal.IsComplete,
                Is.False,
                $"Normal claimed Unmodified with normal writer '{gate}' enabled.");
        }
    }
}
