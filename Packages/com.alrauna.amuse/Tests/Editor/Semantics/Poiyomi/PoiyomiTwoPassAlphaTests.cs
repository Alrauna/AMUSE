using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// Two Pass second-family alpha tests. The pinned Two Pass shader draws
    /// two Base passes. The first family reads <c>_Color.a</c> and its own
    /// <c>_AlphaForceOpaque</c> flag (note 4.1, vendor line 29780 and note
    /// 4.4, vendor line 30366). The second family reads
    /// <c>_TwoPassColor.a</c> (note 4.1, vendor line 29785) and its own
    /// <c>_AlphaForceOpaque2</c> flag (note 4.4, Two Pass source line 862).
    /// A Two Pass material proves alpha only when every family the material
    /// draws proves exactly one, so the claim completes only through the
    /// conjunction of the two families.
    /// </summary>
    public sealed class PoiyomiTwoPassAlphaTests : PoiyomiFixtureTestBase
    {
        private const string TwoPassColor = "_TwoPassColor";
        private const string ForceOpaque = "_AlphaForceOpaque";
        private const string ForceOpaque2 = "_AlphaForceOpaque2";
        private const string IgnoreMainTexAlpha = "_MainIgnoreTexAlpha";

        private static PoiyomiSemanticResult Interpret(Material material)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedTwoPassMaterial(
                material, ColorSpace.Linear);
        }

        private static void AssertExactlyOne(PoiyomiSemanticResult result)
        {
            AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
            var value = result.Semantics.Alpha.GetCompleteValue();
            Assert.That(
                value.Kind, Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(1f));
        }

        /// <summary>
        /// A Two Pass material whose first family is forced opaque and whose
        /// second family is left to prove its own chain from
        /// <paramref name="secondTintAlpha"/>.
        /// </summary>
        private Material DivergentFixture(float secondTintAlpha)
        {
            var material = NewTwoPassFixtureMaterial();
            material.SetFloat(ForceOpaque, 1f);
            material.SetFloat(ForceOpaque2, 0f);
            material.SetColor(TwoPassColor, new Color(1f, 1f, 1f, secondTintAlpha));
            return material;
        }

        // --- Falsifier 1: family divergence ---------------------------------

        [Test]
        public void TwoPassForceOpaqueFirstOnSecondOff_NeverCompletesAlpha()
        {
            // The first family writes exactly one, but the second family's
            // chain computes 0.5 from its own tint. An implementation that
            // reads only _AlphaForceOpaque claims a proven one and lets the
            // second family writer slip into it, so the unknown assertion is
            // the one that fails against that shape.
            var result = Interpret(DivergentFixture(0.5f));

            Assert.That(
                IsComplete(result, PoiyomiSemanticOutput.Alpha),
                Is.False,
                "force-opaque on the first family with the second family "
                    + "left to a sub-one tint must never complete alpha");
            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                TwoPassColor);
        }

        [Test]
        public void TwoPassForceOpaqueSecondOnFirstOff_NeverCompletesAlpha()
        {
            // The divergence mirrors: the second family forced and the first
            // family's own tint sub-one. The refusal names the first family's
            // tint, and the row fails any implementation that gates only the
            // second flag.
            var material = NewTwoPassFixtureMaterial();
            material.SetFloat(ForceOpaque, 0f);
            material.SetFloat(ForceOpaque2, 1f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));

            var result = Interpret(material);

            Assert.That(
                IsComplete(result, PoiyomiSemanticOutput.Alpha),
                Is.False,
                "force-opaque on the second family with the first family "
                    + "left to a sub-one tint must never complete alpha");
            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_Color");
        }

        [Test]
        public void TwoPassBothFamiliesForced_CompletesExactlyOne()
        {
            var material = NewTwoPassFixtureMaterial();
            material.SetFloat(ForceOpaque, 1f);
            material.SetFloat(ForceOpaque2, 1f);

            AssertExactlyOne(Interpret(material));
        }

        [Test]
        public void TwoPassBothChainsProveExactlyOne_CompletesOne()
        {
            // No force-opaque anywhere: each family's chain must prove one
            // from its own tint, so the completion exercises the second
            // family's read of _TwoPassColor.a rather than any shortcut.
            var material = NewTwoPassFixtureMaterial();
            material.SetFloat(ForceOpaque, 0f);
            material.SetFloat(ForceOpaque2, 0f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            material.SetColor(TwoPassColor, new Color(1f, 1f, 1f, 1f));

            AssertExactlyOne(Interpret(material));
        }

        [Test]
        public void TwoPassSecondTintSubOne_KeepsAlphaUnknown()
        {
            // The plain chain proves one from _Color.a, but the second
            // family's chain proves 0.5 from its own tint. The conjunction
            // refuses, and the diagnostic names the second tint, because
            // that tint is where the family's alpha value is decided.
            var material = NewTwoPassFixtureMaterial();
            material.SetFloat(ForceOpaque, 0f);
            material.SetFloat(ForceOpaque2, 0f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            material.SetColor(TwoPassColor, new Color(1f, 1f, 1f, 0.5f));

            var result = Interpret(material);

            Assert.That(
                IsComplete(result, PoiyomiSemanticOutput.Alpha),
                Is.False,
                "a second tint below one keeps the whole claim unknown");
            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                TwoPassColor);
        }

        [Test]
        public void TwoPassNonBinarySecondForceOpaque_RefusesNamingIt()
        {
            // The second flag follows the same exact-binary gate read as the
            // first: a value that is neither zero nor one cannot prove the
            // forced state, so the family chain must prove on its own and
            // the missing proof refuses naming the flag.
            var material = DivergentFixture(0.5f);
            material.SetFloat(ForceOpaque2, 0.5f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                ForceOpaque2);
        }

        [Test]
        public void TwoPassIgnoreTexAlphaAppliesToBothFamilies()
        {
            // The second Base pass reuses the first pass's alpha chain, so
            // the ignore flag discards the texture term for both families
            // and each tint's alpha stands alone. With both tints at one the
            // claim completes even with a texture assigned.
            var material = NewTwoPassFixtureMaterial();
            material.SetFloat(ForceOpaque, 0f);
            material.SetFloat(ForceOpaque2, 0f);
            material.SetFloat(IgnoreMainTexAlpha, 1f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            material.SetColor(TwoPassColor, new Color(1f, 1f, 1f, 1f));
            material.SetTexture("_MainTex", ImportTexture("twopass_ignored"));

            AssertExactlyOne(Interpret(material));
        }
    }
}
