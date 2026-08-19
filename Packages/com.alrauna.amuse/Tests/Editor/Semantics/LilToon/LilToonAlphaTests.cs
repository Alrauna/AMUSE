using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// On LIL_RENDER 0 the forward pass assigns fd.col.a = 1.0 unconditionally,
    /// after every alpha-writing block, and the subpass alpha path is excluded
    /// entirely by #if LIL_RENDER &gt; 0. Alpha is therefore a constant from
    /// attested shader identity; only fragment-removing mechanisms remain.
    /// </summary>
    public sealed class LilToonAlphaTests : LilToonFixtureTestBase
    {
        [Test]
        public void OpaqueVariant_IsConstantOne()
        {
            var material = NewFixtureMaterial();

            var alpha = Interpret(material).Semantics.Alpha;

            Assert.That(alpha.IsComplete, Is.True);
            var value = alpha.GetCompleteValue();
            Assert.That(value.Kind, Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(1f));
        }

        [Test]
        public void OpaqueVariant_IgnoresColorAlphaAndMainTexAlpha()
        {
            var material = NewFixtureMaterial();
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.25f));
            material.SetTexture("_MainTex", ImportTexture("alphatex"));

            var value = Interpret(material).Semantics.Alpha.GetCompleteValue();

            Assert.That(value.Kind, Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(1f));
        }

        [TestCase("_Invisible")]
        [TestCase("_UDIMDiscardCompile")]
        public void CoverageMechanism_KeepsAlphaUnknown(string property)
        {
            var material = NewFixtureMaterial();
            material.SetFloat(property, 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Alpha,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        [Test]
        public void NonFiniteCoverageProperty_KeepsAlphaUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UDIMDiscardCompile", float.NaN);

            var result = Interpret(material);

            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Alpha,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_UDIMDiscardCompile");
        }
    }
}
