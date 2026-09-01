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
        public void EveryNearIdentityMainTexHsvgComponentIsRefusedExactly()
        {
            var identity = new Vector4(0f, 1f, 1f, 1f);

            for (var index = 0; index < 4; index++)
            {
                var hsvg = identity;
                hsvg[index] = identity[index] + 0.000005f;
                var label = "component " + index + " = " + hsvg;

                // Fixture precondition: exactly one binary32 component departs
                // from the identity, yet Unity's epsilon-based Vector4
                // equality still reports the two vectors equal.
                Assert.That(
                    hsvg[index] == identity[index],
                    Is.False,
                    "the fixture must differ under exact comparison: " + label);
                Assert.That(
                    hsvg == identity,
                    Is.True,
                    "the fixture must sit inside Unity's approximate-equality " +
                    "ball: " + label);

                var material = NewFixtureMaterial();
                material.SetVector("_MainTexHSVG", hsvg);

                var result = Interpret(material);

                // Falsifies: proving lilToneCorrection inert with Unity's
                // epsilon-based Vector4 equality. The correction runs
                // unconditionally, so any departure from (0,1,1,1) changes the
                // emitted color however small it is.
                Assert.That(
                    result.Semantics.BaseColor.IsComplete,
                    Is.False,
                    "near-identity HSVG must refuse: " + label);
                AssertSingleDiagnostic(
                    result,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    "_MainTexHSVG");
            }
        }

        [Test]
        public void EveryNearZeroMainTexScrollRotateComponentIsRefusedExactly()
        {
            for (var index = 0; index < 4; index++)
            {
                var scrollRotate = Vector4.zero;
                scrollRotate[index] = 0.000005f;
                var label = "component " + index + " = " + scrollRotate;

                Assert.That(
                    scrollRotate.x == 0f && scrollRotate.y == 0f &&
                    scrollRotate.z == 0f && scrollRotate.w == 0f,
                    Is.False,
                    "the fixture must be nonzero under exact comparison: " +
                    label);
                Assert.That(
                    scrollRotate == Vector4.zero,
                    Is.True,
                    "the fixture must sit inside Unity's approximate-equality " +
                    "ball: " + label);

                var material = NewFixtureMaterial();
                material.SetTexture(
                    "_MainTex", ImportTexture("near_zero_scroll_" + index));
                material.SetVector("_MainTex_ScrollRotate", scrollRotate);

                var result = Interpret(material);

                // Falsifies: an epsilon-based zero test for the runtime
                // scroll/rotate path, which lilToon evaluates as
                // lilRotateUV(uv, z + w * LIL_TIME) + frac(xy * LIL_TIME).
                Assert.That(
                    result.Semantics.BaseColor.IsComplete,
                    Is.False,
                    "near-zero scroll/rotate must refuse: " + label);
                AssertSingleDiagnostic(
                    result,
                    LilToonSemanticOutput.BaseColor,
                    LilToonSemanticDiagnosticCode.UnsupportedUv,
                    "_MainTex_ScrollRotate");
            }
        }

        [Test]
        public void NearOneMainTexTintStaysAnExactTextureMultiplier()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("near_one_tint"));
            // 0.999999 sRGB decodes to 0.9999979 linear: not exactly one, but
            // well inside Unity's Vector3 approximate-equality ball around one.
            material.SetColor("_Color", new Color(0.999999f, 1f, 1f, 1f));

            var linear = material.GetColor("_Color").linear;
            var tint = new Vector3(linear.r, linear.g, linear.b);
            Assert.That(
                tint.x == 1f && tint.y == 1f && tint.z == 1f,
                Is.False,
                "the derived tint must differ from one under exact comparison");
            Assert.That(
                tint == Vector3.one,
                Is.True,
                "the derived tint must sit inside Unity's approximate-equality " +
                "ball around one");

            var value = Interpret(material).Semantics.BaseColor.GetCompleteValue();

            // Falsifies: collapsing a near-one tint to the unscaled texture
            // through Unity's epsilon-based Vector3 equality, which drops a
            // real multiplier from the modeled color.
            Assert.That(
                value.Kind,
                Is.EqualTo(ColorSemanticValueKind.TextureSampleTimesConstant));
            var multiplier = value.GetMultiplier();
            Assert.That(multiplier.x == tint.x, Is.True, "exact red multiplier");
            Assert.That(multiplier.y == tint.y, Is.True, "exact green multiplier");
            Assert.That(multiplier.z == tint.z, Is.True, "exact blue multiplier");
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
