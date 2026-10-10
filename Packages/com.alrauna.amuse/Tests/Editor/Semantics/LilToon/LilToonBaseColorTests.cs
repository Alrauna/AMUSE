using System.Linq;
using Alrauna.Amuse.Editor.Host;
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
            // In a Linear project the material converts every colour
            // between sRGB and linear on set and get, so the stored value
            // can sit one ULP off the authored literal. The proof reads
            // the value the shader actually sees, so the expectation
            // derives from the stored value, not from the literal.
            var linear = material.GetColor("_Color").linear;
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
        public void MirrorWrapMainTex_IsUnsupportedSampling()
        {
            var material = NewFixtureMaterial();
            material.SetTexture(
                "_MainTex",
                ImportTexture(
                    "mirror",
                    importer => importer.wrapMode =
                        UnityEngine.TextureWrapMode.Mirror));

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

        // --- captured-evidence seam ---

        [Test]
        public void FullMaterialEvidenceRequest_MatchesTheIndependentExactSchema()
        {
            var request = LilToonMaterialSemantics.FullMaterialEvidenceRequest;

            Assert.That(request.ShaderName, Is.True);
            Assert.That(request.ActiveColorSpace, Is.False);
            Assert.That(request.PresenceProperties, Is.EquivalentTo(new[]
            {
                "_MainTexHSVG",
                "_MainTex_ScrollRotate",
                "_DissolveParams",
                "_BumpScale",
            }));
            Assert.That(request.ScalarProperties, Is.SupersetOf(new[]
            {
                "_ShiftBackfaceUV", "_UseParallax", "_UsePOM", "_UseAudioLink",
                "_UseMain2ndTex", "_UseMain3rdTex", "_MainGradationStrength",
                "_UseEmission2nd", "_UseReflection", "_UseMatCap",
                "_UseMatCap2nd", "_UseRim", "_UseRimShade", "_UseGlitter",
                "_UseBacklight", "_EmissionMainStrength", "_EmissionFluorescence",
                "_EmissionUseGrad", "_AudioLink2Emission", "_EmissionParallaxDepth",
                "_UseBump2ndMap", "_UseAnisotropy",
                "_UseEmission", "_EmissionBlendMode", "_EmissionBlend",
                "_EmissionMap_UVMode", "_UseBumpMap", "_BumpScale",
            }));
            // Combine unions in exactly three alpha scalars: _lilToonVersion,
            // _Invisible, and _UDIMDiscardCompile. _Invisible is also a
            // base-color writer gate, so the union holds 31 distinct names.
            Assert.That(request.ScalarProperties.Count, Is.EqualTo(31));
            Assert.That(request.ColorProperties, Is.EquivalentTo(new[]
            {
                "_Color", "_BackfaceColor", "_EmissionColor",
            }));
            Assert.That(request.VectorProperties, Is.EquivalentTo(new[]
            {
                "_MainTexHSVG", "_MainTex_ScrollRotate", "_DissolveParams",
                "_EmissionBlink", "_EmissionMap_ScrollRotate",
            }));
            // CopyTextures sorts by property name.
            Assert.That(request.TextureProperties.Count, Is.EqualTo(5));
            Assert.That(
                request.TextureProperties.Select(property => property.PropertyName),
                Is.EquivalentTo(new[]
                {
                    "_MainTex", "_MainColorAdjustMask",
                    "_EmissionMap", "_EmissionBlendMask", "_BumpMap",
                }));
            Assert.That(
                request.TextureProperties[0].PropertyName,
                Is.EqualTo("_BumpMap"));
            Assert.That(
                request.TextureProperties[0].Evidence,
                Is.EqualTo(
                    TextureEvidenceKinds.ScaleOffset |
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.CanonicalNormalMap));
            Assert.That(
                request.TextureProperties[1].PropertyName,
                Is.EqualTo("_EmissionBlendMask"));
            Assert.That(
                request.TextureProperties[1].Evidence,
                Is.EqualTo(TextureEvidenceKinds.None),
                "the blend mask is a presence check over the assignment alone");
            Assert.That(
                request.TextureProperties[2].PropertyName,
                Is.EqualTo("_EmissionMap"));
            Assert.That(
                request.TextureProperties[2].Evidence,
                Is.EqualTo(
                    TextureEvidenceKinds.ScaleOffset |
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.Sampling |
                    TextureEvidenceKinds.ColorInterpretation |
                    TextureEvidenceKinds.SampledAlphaIsOne));
            Assert.That(
                request.TextureProperties[3].PropertyName,
                Is.EqualTo("_MainColorAdjustMask"));
            Assert.That(
                request.TextureProperties[3].Evidence,
                Is.EqualTo(TextureEvidenceKinds.None),
                "the adjust mask is a presence check over the assignment alone");
            Assert.That(
                request.TextureProperties[4].PropertyName,
                Is.EqualTo("_MainTex"));
            Assert.That(
                request.TextureProperties[4].Evidence,
                Is.EqualTo(
                    TextureEvidenceKinds.ScaleOffset |
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.Sampling |
                    TextureEvidenceKinds.ColorInterpretation |
                    TextureEvidenceKinds.BoundedColorRange),
                "the unit-range fact rides the main texture, the one tone-" +
                "correction input");
        }

        [Test]
        public void BaseColor_ReadsTheTintFromTheSnapshotNotTheLiveMaterial()
        {
            var material = NewFixtureMaterial();
            material.SetColor("_Color", new Color(1f, 0f, 0f, 1f));
            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material, LilToonMaterialSemantics.FullMaterialEvidenceRequest),
            })[0];
            material.SetColor("_Color", new Color(0f, 1f, 0f, 1f));

            var result = LilToonMaterialSemantics
                .InterpretVerifiedMaterialFromEvidence(
                    captured, ColorSpace.Linear, AllFeatures);

            // The expectation states the snapshot fact: the tint is the
            // conversion of the colour the capture saw, never the colour the
            // material carries now. Both conversions are exact at zero and one.
            var expectedTint = new Color(1f, 0f, 0f, 1f).linear;

            Assert.That(result.IsSupportedMaterial, Is.True);
            Assert.That(result.Semantics.BaseColor.IsComplete, Is.True);
            var value = result.Semantics.BaseColor.GetCompleteValue();
            Assert.That(value.Kind, Is.EqualTo(ColorSemanticValueKind.Constant));
            Assert.That(
                value.GetConstantValue(),
                Is.EqualTo(new Vector3(expectedTint.r, expectedTint.g, expectedTint.b)));
        }
    }
}
