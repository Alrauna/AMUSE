using System.Linq;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    public sealed class PoiyomiOutlineAlphaSemanticsTests : PoiyomiFixtureTestBase
    {
        private Material OutlineEnabled()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_EnableOutlines", 1f);
            material.SetTexture("_MainTex", ImportTexture("outline_main"));
            return material;
        }

        [Test]
        public void OutlinesDisabled_HostileState_ChangesNothing()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_OutlineAlphaDistanceFade", 1f);
            material.SetFloat("_OutlineALColorEnabled", 1f);
            material.SetColor("_LineColor", new Color(1f, 1f, 1f, 0.25f));

            var result = Interpret(material);

            Assert.That(
                result.Diagnostics.Any(d => d.Output == PoiyomiSemanticOutput.Alpha),
                Is.False,
                "Alpha output must not emit a diagnostic.");
        }

        [Test]
        public void OutlinesEnabled_ProvenInputs_AlphaStaysComplete()
        {
            var material = OutlineEnabled();

            var result = Interpret(material);

            AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void OutlinesEnabled_LineColorAlphaBelowOne_IsUnknownWithDiagnostic()
        {
            var material = OutlineEnabled();
            material.SetColor("_LineColor", new Color(1f, 1f, 1f, 0.5f));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_LineColor");
        }

        [Test]
        public void OutlinesEnabled_DistanceFadeEnabled_IsUnknownWithDiagnostic()
        {
            var material = OutlineEnabled();
            material.SetFloat("_OutlineAlphaDistanceFade", 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_OutlineAlphaDistanceFade");
        }

        [Test]
        public void OutlinesEnabled_AudioLinkColorEnabled_IsUnknownWithDiagnostic()
        {
            var material = OutlineEnabled();
            material.SetFloat("_OutlineALColorEnabled", 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_OutlineALColorEnabled");
        }

        [Test]
        public void OutlinesEnabled_LineColorAlphaNaN_IsUnknownWithDiagnostic()
        {
            // --- Falsifier: a non-finite line color must refuse, not pass
            // through as a proven factor. ---
            var material = OutlineEnabled();
            material.SetColor("_LineColor", new Color(1f, 1f, 1f, float.NaN));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_LineColor");
        }

        [Test]
        public void OutlinesEnabled_UnprovenTexture_NonzeroPan_IsUnknownWithDiagnostic()
        {
            var material = OutlineEnabled();
            material.SetVector("_OutlineTexturePan", new Vector4(0f, 0.5f, 0f, 0f));
            material.SetTexture("_OutlineTexture", ImportTexture("outline"));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_OutlineTexturePan");
        }

        [Test]
        public void OutlinesEnabled_UnprovenTexture_PanOnOneAxisOnly_IsUnknownWithDiagnostic()
        {
            // --- Falsifier: the supported-form gate tests both pan
            // components. ---
            var material = OutlineEnabled();
            material.SetVector("_OutlineTexturePan", new Vector4(0.5f, 0f, 0f, 0f));
            material.SetTexture("_OutlineTexture", ImportTexture("outline"));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_OutlineTexturePan");
        }

        [Test]
        public void OutlinesEnabled_WholeTextureProven_AlphaStaysComplete()
        {
            var material = OutlineEnabled();
            material.SetTexture("_OutlineTexture", ImportTexture(
                "outlineSolid",
                importer => importer.alphaSource =
                    TextureImporterAlphaSource.None,
                sourceHasAlpha: false));

            AssertOutputComplete(Interpret(material),
                PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void OutlinesEnabled_UnprovenTexture_ChainAdmitted_AlphaIsTheMappedTerm()
        {
            var material = OutlineEnabled();
            material.SetTexture("_OutlineTexture", ImportTexture("outline"));

            var result = Interpret(material);

            // The mapped term is complete as a term: the per-triangle verdict
            // happens in the classifier over this value.
            AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
        }

        /// <summary>
        /// The chain-admitted row above only asserts completeness, which a
        /// collapse of the outline factor to the constant one would also
        /// satisfy. This row closes that under-assertion: the unproven
        /// outline texture carries an asymmetric alpha field no constant
        /// can represent (one non-opaque texel among opaque ones), so the
        /// admitted alpha must still carry the outline texture as a mapped
        /// sample in its product chain. A collapse drops the sample,
        /// shrinking the chain to the base factor alone, and fails both
        /// chain assertions.
        /// </summary>
        [Test]
        public void OutlinesEnabled_UnprovenTexture_HoleField_KeepsTheMappedOutlineFactor()
        {
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            pixels[0] = new Color32(255, 255, 255, 128);
            var outlineTexture = TestTextureImport.WritePng(
                TempFolder + "/outline_hole_field.png",
                4,
                4,
                pixels,
                importer => importer.mipmapEnabled = false);

            var material = OutlineEnabled();
            material.SetTexture("_OutlineTexture", outlineTexture);

            var result = Interpret(material);

            AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);

            var value = result.Semantics.Alpha.GetCompleteValue();
            Assert.That(
                value.Kind,
                Is.EqualTo(
                    ScalarSemanticValueKind.ProductChainOfTextureSamples));
            Assert.That(value.GetChainFactorCount(), Is.EqualTo(2));

            var outlineToken = ExpectedToken(outlineTexture);
            Assert.That(
                Enumerable.Range(0, value.GetChainFactorCount())
                    .Select(index => value.GetChainSample(index).Source.Value),
                Has.Member(outlineToken),
                "the outline contribution must survive as a mapped sample");
        }

        [Test]
        public void OutlinesEnabled_OverrideAlphaEnabled_StillRequiresBaseAlpha()
        {
            // --- Falsifier: _OutlineOverrideAlpha cannot replace the base chain.
            // Even when override alpha is 1, a transparent base color keeps the
            // material unproven/transparent. ---
            var material = OutlineEnabled();
            material.SetFloat("_OutlineOverrideAlpha", 1f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));

            var result = Interpret(material);
            AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
            var value = result.Semantics.Alpha.GetCompleteValue();
            Assert.That(value.Kind, Is.Not.EqualTo(ScalarSemanticValueKind.Constant));
        }

        [Test]
        public void OutlinesEnabled_OverrideAlphaNaN_IsUnknownWithDiagnostic()
        {
            var material = OutlineEnabled();
            material.SetFloat("_OutlineOverrideAlpha", float.NaN);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_OutlineOverrideAlpha");
        }

        [Test]
        public void OutlinesEnabled_WithReplaceMask_EvaluatesOutlineFactor()
        {
            // --- Falsifier: Replace alpha mask mode must not bypass outline
            // evaluation. ---
            var material = OutlineEnabled();
            material.SetFloat("_MainAlphaMaskMode", 1f); // Replace mode
            material.SetColor("_LineColor", new Color(1f, 1f, 1f, 0.5f));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_LineColor");
        }
    }
}
