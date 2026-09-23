using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    public sealed class PoiyomiRimLightingAlphaTests : PoiyomiFixtureTestBase
    {
        private static PoiyomiSemanticResult Interpret(Material material)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Linear);
        }

        // A material on the non-forced alpha path with the mask mode off, so
        // alpha is proven from _MainTex.a and/or _Color.a.
        private Material NonForcedMaterial()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            return material;
        }

        // A non-forced material on the Two Pass stand-in shader.
        private Material NonForcedTwoPassMaterial()
        {
            var material = NewTwoPassFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            return material;
        }

        [Test]
        public void
            RimSlot1_Enabled_WithZeroApplyAlpha_KeepsAlphaComplete()
        {
            // --- Falsifier 1: a plausible wrong implementation keeps the
            // committed enable gates and refuses every enabled rim slot by
            // name. It fails this case.
            var material = NonForcedMaterial();
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_RimApplyAlpha", 0f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void
            BothRimSlots_Enabled_WithZeroApplyAlpha_KeepsAlphaComplete()
        {
            // --- Falsifier 2: a plausible wrong implementation admits slot
            // one only, or reads only slot one's enable float.
            var material = NonForcedMaterial();
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_EnableRim2Lighting", 1f);
            material.SetFloat("_RimApplyAlpha", 0f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void
            RimSlot1_Enabled_WithAddApplyAlpha_RefusesNamingApplyAlpha()
        {
            // --- Falsifier 3: a plausible wrong implementation refuses the
            // rim family but names the enable float instead of the scalar
            // that carries the alpha decision.
            var material = NonForcedMaterial();
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_RimApplyAlpha", 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_RimApplyAlpha");
        }

        [Test]
        public void
            RimSlot1_Enabled_WithMultiplyApplyAlpha_RefusesNamingApplyAlpha()
        {
            // --- Falsifier 4: a plausible wrong implementation checks the
            // add mode only and treats the multiply mode as inert, claiming
            // an alpha the vendor rescales (pinned 9.3.64 source, vendor
            // line 25843).
            var material = NonForcedMaterial();
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_RimApplyAlpha", 2f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_RimApplyAlpha");
        }

        [Test]
        public void
            RimSlots_Disabled_WithNonZeroApplyAlpha_RefusesNamingApplyAlpha()
        {
            // --- Falsifier 5: a plausible wrong implementation binds the
            // scalar check on the enable floats. The vendor call sites guard
            // on the enable keywords, not on the floats, so the gate must
            // bind whatever the floats say. This case closes the
            // float-keyword soundness hole the design names in section 8.
            var material = NonForcedMaterial();
            material.SetFloat("_EnableRimLighting", 0f);
            material.SetFloat("_EnableRim2Lighting", 0f);
            material.SetFloat("_RimApplyAlpha", 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_RimApplyAlpha");
        }

        [Test]
        public void
            RimSlot1_Enabled_WithNonFiniteApplyAlpha_RefusesNamingApplyAlpha()
        {
            // --- Falsifier 6: a plausible wrong implementation treats a
            // garbage scalar as inert. The fail-closed direction refuses it.
            var material = NonForcedMaterial();
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_RimApplyAlpha", float.NaN);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_RimApplyAlpha");
        }

        [Test]
        public void
            ForcedOpaque_WithNonZeroApplyAlpha_StillClaimsConstantOne()
        {
            // No-op guard: the forced short-circuit precedes the gate check,
            // matching the vendor order where the forcing runs after every
            // rim write. It must pass before and after the change.
            var material = NonForcedMaterial();
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_RimApplyAlpha", 1f);
            material.SetFloat("_AlphaForceOpaque", 1f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void
            ReplaceMaskProof_WithRimEnabled_KeepsItsExactlyOneProof()
        {
            // Design case 8 no-op guard. The gate walk precedes mask
            // interpretation, so an admitted rim must not disturb the
            // mask proof.
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 1f);
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_RimApplyAlpha", 0f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void
            TwoPass_RimSlot1_Enabled_WithZeroApplyAlpha_KeepsAlphaComplete()
        {
            // --- Falsifier 7: a plausible wrong implementation admits the
            // plain family only. The Two Pass source carries the same rim
            // mechanism (pinned source, vendor lines 25933 to 25940), so the
            // contract covers both families.
            var material = NonForcedTwoPassMaterial();
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_RimApplyAlpha", 0f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void
            TwoPass_WithAddApplyAlpha_RefusesNamingApplyAlpha()
        {
            // --- Falsifier 8: a plausible wrong implementation gates the
            // scalar on the plain family only.
            var material = NonForcedTwoPassMaterial();
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_RimApplyAlpha", 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_RimApplyAlpha");
        }
    }
}
