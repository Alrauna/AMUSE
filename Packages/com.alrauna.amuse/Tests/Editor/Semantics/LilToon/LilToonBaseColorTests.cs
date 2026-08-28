using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// BaseColor models fd.albedo, assigned immediately before lighting. Every
    /// block that writes fd.col.rgb before that point must be proven inert, and
    /// the unconditional tone-correction path must be proven the identity.
    /// </summary>
    public sealed class LilToonBaseColorTests : LilToonFixtureTestBase
    {
        [Test]
        public void NoMainTex_IsLinearConstantColor()
        {
            var material = NewFixtureMaterial();
            material.SetColor("_Color", new Color(0.5f, 0.25f, 0.75f, 1f));

            var baseColor = Interpret(material).Semantics.BaseColor;

            Assert.That(baseColor.IsComplete, Is.True);
            var value = baseColor.GetCompleteValue();
            Assert.That(value.Kind, Is.EqualTo(ColorSemanticValueKind.Constant));
            var linear = new Color(0.5f, 0.25f, 0.75f, 1f).linear;
            Assert.That(
                value.GetConstantValue(),
                Is.EqualTo(new Vector3(linear.r, linear.g, linear.b)));
        }

        [Test]
        public void MainTexWithWhiteColor_IsPlainTextureSample()
        {
            var material = NewFixtureMaterial();
            var texture = ImportTexture("basecolor");
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", new Vector2(2f, 3f));
            material.SetTextureOffset("_MainTex", new Vector2(0.25f, 0.5f));

            var baseColor = Interpret(material).Semantics.BaseColor;

            Assert.That(baseColor.IsComplete, Is.True);
            var value = baseColor.GetCompleteValue();
            Assert.That(value.Kind, Is.EqualTo(ColorSemanticValueKind.TextureSample));

            var sample = value.GetTextureSample();
            Assert.That(sample.Coordinates.Channel, Is.EqualTo(0));
            Assert.That(sample.Coordinates.Scale, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(
                sample.Coordinates.Offset, Is.EqualTo(new Vector2(0.25f, 0.5f)));
            Assert.That(
                UnityTextureEvidence.TryGetSourceId(texture, out var expectedId),
                Is.True);
            Assert.That(sample.Source, Is.EqualTo(expectedId));
        }

        [Test]
        public void MainTexWithTint_IsTextureTimesConstant()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("tinted"));
            material.SetColor("_Color", new Color(0.5f, 0.5f, 0.5f, 1f));

            var value = Interpret(material).Semantics.BaseColor.GetCompleteValue();

            Assert.That(
                value.Kind,
                Is.EqualTo(ColorSemanticValueKind.TextureSampleTimesConstant));
            var linear = new Color(0.5f, 0.5f, 0.5f, 1f).linear;
            Assert.That(
                value.GetMultiplier(),
                Is.EqualTo(new Vector3(linear.r, linear.g, linear.b)));
        }

        [Test]
        public void GammaColorSpace_IsUnknown()
        {
            var material = NewFixtureMaterial();

            var result = LilToonMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Gamma, AllFeatures);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedColorSpace,
                "Gamma");
        }

        [TestCase("_Invisible")]
        [TestCase("_ShiftBackfaceUV")]
        [TestCase("_UseParallax")]
        [TestCase("_UsePOM")]
        [TestCase("_UseAudioLink")]
        [TestCase("_UseMain2ndTex")]
        [TestCase("_UseMain3rdTex")]
        [TestCase("_MainGradationStrength")]
        public void EnabledWriter_KeepsBaseColorUnknown(string property)
        {
            var material = NewFixtureMaterial();
            material.SetFloat(property, 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        [Test]
        public void NonIdentityToneCorrection_IsUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetVector("_MainTexHSVG", new Vector4(0.1f, 1f, 1f, 1f));

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_MainTexHSVG");
        }

        [Test]
        public void AssignedColorAdjustMask_IsUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainColorAdjustMask", ImportTexture("mask"));

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_MainColorAdjustMask");
        }

        [Test]
        public void NonZeroScrollRotate_IsUnsupportedUv()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("scroll"));
            material.SetVector("_MainTex_ScrollRotate", new Vector4(0.1f, 0f, 0f, 0f));

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedUv,
                "_MainTex_ScrollRotate");
        }

        [Test]
        public void TrilinearMainTex_IsUnsupportedSampling()
        {
            var material = NewFixtureMaterial();
            material.SetTexture(
                "_MainTex",
                ImportTexture(
                    "trilinear",
                    importer => importer.filterMode = FilterMode.Trilinear));

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
        }

        [Test]
        public void SceneOnlyMainTex_IsUnstableIdentity()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", Track(new Texture2D(2, 2)));

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnstableTextureIdentity,
                "_MainTex");
        }

        // --- positive sampled-range proof ---

        [Test]
        public void BoundedLdrMainTex_ProvesUnitRange()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("ldr"));

            Assert.That(Interpret(material).Semantics.BaseColor.IsComplete, Is.True);
        }

        [Test]
        public void FloatFormatMainTex_IsRefused()
        {
            var material = NewFixtureMaterial();
            var hdr = ImportHdrTexture("hdrmain");

            // The fixture is importer-backed on purpose: this must fail because
            // the effective GraphicsFormat is outside the bounded allow-list,
            // not merely because no TextureImporter exists.
            var path = AssetDatabase.GetAssetPath(hdr);
            Assert.That(
                AssetImporter.GetAtPath(path) as TextureImporter,
                Is.Not.Null,
                "HDR fixture must have a TextureImporter.");
            Assert.That(
                hdr.graphicsFormat.ToString(),
                Does.Contain("SFloat").Or.Contain("Float"),
                "HDR fixture must import to a floating-point GraphicsFormat; " +
                "observed " + hdr.graphicsFormat);

            material.SetTexture("_MainTex", hdr);

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedTextureImport,
                "_MainTex");
        }

        [Test]
        public void MainTexWithoutImporter_IsRefused()
        {
            var material = NewFixtureMaterial();
            var native = CreateNativeTextureAsset("nativemain");

            // Stable identity and a bounded format, but no TextureImporter, so
            // neither the colour interpretation nor the range can be proven.
            // Unproven evidence must refuse, never pass.
            material.SetTexture("_MainTex", native);

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedTextureImport,
                "_MainTex");
        }
    }
}
