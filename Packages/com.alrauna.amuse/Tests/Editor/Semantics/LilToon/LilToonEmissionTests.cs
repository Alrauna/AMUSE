using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// lilToon's emission blends into fd.col.rgb through lilBlendColor. Only
    /// blend mode 1 (Add) is a true additive term, and the blend factor carries
    /// emissionColor.a, so an RGBA map would scale its own emission.
    /// </summary>
    public sealed class LilToonEmissionTests : LilToonFixtureTestBase
    {
        [Test]
        public void EmissionDisabledAndNoWriters_IsConstantZero()
        {
            var material = NewFixtureMaterial();

            var emission = Interpret(material).Semantics.Emission;

            Assert.That(emission.IsComplete, Is.True);
            var value = emission.GetCompleteValue();
            Assert.That(value.Kind, Is.EqualTo(ColorSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(Vector3.zero));
        }

        [TestCase("_UseEmission2nd")]
        [TestCase("_UseReflection")]
        [TestCase("_UseMatCap")]
        [TestCase("_UseMatCap2nd")]
        [TestCase("_UseRim")]
        [TestCase("_UseRimShade")]
        [TestCase("_UseGlitter")]
        [TestCase("_UseBacklight")]
        [TestCase("_UseAudioLink")]
        public void EnabledEmissiveWriter_BlocksZeroClaim(string property)
        {
            var material = NewFixtureMaterial();
            material.SetFloat(property, 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        [Test]
        public void OpaqueBackfaceColor_BlocksZeroClaim()
        {
            var material = NewFixtureMaterial();
            material.SetColor("_BackfaceColor", new Color(1f, 0f, 0f, 1f));

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_BackfaceColor");
        }

        // --- dissolve gate, required on BOTH claims ---

        [Test]
        public void ActiveDissolve_WithEmissionDisabled_BlocksZeroClaim()
        {
            // The zero-emission shortcut must not hide dissolve behavior.
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 0f);
            material.SetVector("_DissolveParams", new Vector4(1f, 0f, 0.5f, 0.1f));

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_DissolveParams");
        }

        [Test]
        public void ActiveDissolve_WithEmissionEnabled_BlocksSlotClaim()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetVector("_DissolveParams", new Vector4(2f, 0f, 0.5f, 0.1f));

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_DissolveParams");
        }

        [Test]
        public void NonFiniteDissolveParams_BlocksClaim()
        {
            var material = NewFixtureMaterial();
            material.SetVector(
                "_DissolveParams", new Vector4(0f, float.NaN, 0.5f, 0.1f));

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_DissolveParams");
        }

        [Test]
        public void DefaultDissolveParams_DoNotBlockZeroClaim()
        {
            // The shipped default is (0,0,0.5,0.1): mode zero, so inert.
            var material = NewFixtureMaterial();
            material.SetVector("_DissolveParams", new Vector4(0f, 0f, 0.5f, 0.1f));

            Assert.That(Interpret(material).Semantics.Emission.IsComplete, Is.True);
        }

        // --- supported slot-1 forms ---

        [Test]
        public void EmissionWithoutMap_IsScaledConstant()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.25f, 0.5f));
            material.SetFloat("_EmissionBlend", 0.5f);

            var value = Interpret(material).Semantics.Emission.GetCompleteValue();

            Assert.That(value.Kind, Is.EqualTo(ColorSemanticValueKind.Constant));
            var linear = new Color(1f, 0.5f, 0.25f, 0.5f).linear;
            var expected = new Vector3(linear.r, linear.g, linear.b) * (0.5f * 0.5f);
            Assert.That(value.GetConstantValue(), Is.EqualTo(expected));
        }

        [Test]
        public void EmissionWithOpaqueMap_IsTextureTimesConstant()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetColor("_EmissionColor", new Color(1f, 1f, 1f, 0.5f));
            material.SetTexture("_EmissionMap", ImportOpaqueColorMap("emissive"));
            material.SetTextureScale("_EmissionMap", new Vector2(2f, 2f));
            material.SetFloat("_EmissionMap_UVMode", 2f);

            var value = Interpret(material).Semantics.Emission.GetCompleteValue();

            Assert.That(
                value.Kind,
                Is.EqualTo(ColorSemanticValueKind.TextureSampleTimesConstant));
            var sample = value.GetTextureSample();
            // Emission uses direct mapping on the selected channel, not the
            // composed main UV.
            Assert.That(sample.Coordinates.Channel, Is.EqualTo(2));
            Assert.That(sample.Coordinates.Scale, Is.EqualTo(new Vector2(2f, 2f)));
            Assert.That(value.GetMultiplier(), Is.EqualTo(Vector3.one * 0.5f));
        }

        [Test]
        public void EmissionMapWithAlpha_IsUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetTexture(
                "_EmissionMap",
                ImportTexture(
                    "rgbaEmissive",
                    importer =>
                        importer.alphaSource = TextureImporterAlphaSource.FromInput));

            var result = Interpret(material);

            // rgb multiplied by the same sample's alpha is not representable.
            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedTextureImport,
                "_EmissionMap");
        }

        [TestCase(0f)]
        [TestCase(2f)]
        [TestCase(3f)]
        public void NonAdditiveBlendMode_IsUnknown(float mode)
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetFloat("_EmissionBlendMode", mode);

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_EmissionBlendMode");
        }

        [Test]
        public void EnabledBlink_IsUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetVector("_EmissionBlink", new Vector4(1f, 0f, 3.141593f, 0f));

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_EmissionBlink");
        }

        [TestCase("_EmissionMainStrength")]
        [TestCase("_EmissionFluorescence")]
        [TestCase("_EmissionUseGrad")]
        [TestCase("_AudioLink2Emission")]
        [TestCase("_EmissionParallaxDepth")]
        public void EnabledEmissionModifier_IsUnknown(string property)
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetFloat(property, 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        [Test]
        public void AssignedBlendMask_IsUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetTexture("_EmissionBlendMask", ImportTexture("emimask"));

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_EmissionBlendMask");
        }

        [Test]
        public void RimUvMode_IsUnsupportedUv()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetTexture("_EmissionMap", ImportOpaqueColorMap("rimuv"));
            material.SetFloat("_EmissionMap_UVMode", 4f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedUv,
                "_EmissionMap_UVMode");
        }

        [Test]
        public void EveryNearZeroEmissionMapScrollRotateComponentIsRefusedExactly()
        {
            for (var index = 0; index < 4; index++)
            {
                var scrollRotate = Vector4.zero;
                scrollRotate[index] = 0.000005f;
                var label = "component " + index + " = " + scrollRotate;

                // Fixture precondition: exactly one binary32 component is
                // nonzero, yet Unity's epsilon-based Vector4 equality still
                // reports the vector equal to zero.
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
                material.SetFloat("_UseEmission", 1f);
                material.SetTexture(
                    "_EmissionMap",
                    ImportOpaqueColorMap("near_zero_emi_scroll_" + index));
                material.SetVector("_EmissionMap_ScrollRotate", scrollRotate);

                var result = Interpret(material);

                // Falsifies: an epsilon-based zero test for the emission map's
                // own runtime scroll/rotate, which moves the sampled
                // coordinate for any nonzero component.
                Assert.That(
                    result.Semantics.Emission.IsComplete,
                    Is.False,
                    "near-zero emission scroll/rotate must refuse: " + label);
                AssertSingleDiagnostic(
                    result,
                    LilToonSemanticOutput.Emission,
                    LilToonSemanticDiagnosticCode.UnsupportedUv,
                    "_EmissionMap_UVMode");
            }
        }

        [Test]
        public void NearOneEmissionTintStaysAnExactTextureMultiplier()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetFloat("_EmissionBlend", 1f);
            // 0.999999 sRGB decodes to 0.9999979 linear: not exactly one, but
            // well inside Unity's Vector3 approximate-equality ball around one.
            material.SetColor("_EmissionColor", new Color(0.999999f, 1f, 1f, 1f));
            material.SetTexture(
                "_EmissionMap", ImportOpaqueColorMap("near_one_emissive"));

            var stored = material.GetColor("_EmissionColor");
            var linear = stored.linear;
            var tint = new Vector3(linear.r, linear.g, linear.b) * (1f * stored.a);
            Assert.That(
                tint.x == 1f && tint.y == 1f && tint.z == 1f,
                Is.False,
                "the derived tint must differ from one under exact comparison");
            Assert.That(
                tint == Vector3.one,
                Is.True,
                "the derived tint must sit inside Unity's approximate-equality " +
                "ball around one");

            var value = Interpret(material).Semantics.Emission.GetCompleteValue();

            // Falsifies: collapsing a near-one emission tint to the unscaled
            // map through Unity's epsilon-based Vector3 equality.
            Assert.That(
                value.Kind,
                Is.EqualTo(ColorSemanticValueKind.TextureSampleTimesConstant));
            var multiplier = value.GetMultiplier();
            Assert.That(multiplier.x == tint.x, Is.True, "exact red multiplier");
            Assert.That(multiplier.y == tint.y, Is.True, "exact green multiplier");
            Assert.That(multiplier.z == tint.z, Is.True, "exact blue multiplier");
        }

        [Test]
        public void EmissionMapSampler_ComesFromEmissionMapNotMainTex()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetTexture(
                "_MainTex",
                ImportTexture(
                    "trilinearmain",
                    importer => importer.filterMode = FilterMode.Trilinear));
            material.SetTexture("_EmissionMap", ImportOpaqueColorMap("cleanemi"));

            var emission = Interpret(material).Semantics.Emission;

            // _MainTex is unsupported for BaseColor but irrelevant to emission.
            Assert.That(emission.IsComplete, Is.True);
        }

        [TestCase("LIL_FEATURE_EMISSION_1ST")]
        [TestCase("LIL_FEATURE_EmissionMap")]
        public void StrippedFeature_KeepsEmissionUnknown(string missing)
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetTexture("_EmissionMap", ImportOpaqueColorMap("strip"));
            var features = System.Array.FindAll(AllFeatures, f => f != missing);

            var result = Interpret(material, features);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.MissingFeatureCompilation,
                missing);
        }

        [Test]
        public void GammaColorSpaceWithEmissionOn_IsUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);

            var result = LilToonMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Gamma, AllFeatures);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
        }

        [Test]
        public void GammaColorSpaceWithEmissionOff_IsStillConstantZero()
        {
            var material = NewFixtureMaterial();

            var result = LilToonMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Gamma, AllFeatures);

            // A proven zero is independent of the working colour space.
            Assert.That(result.Semantics.Emission.IsComplete, Is.True);
            Assert.That(
                result.Semantics.Emission.GetCompleteValue().GetConstantValue(),
                Is.EqualTo(Vector3.zero));
        }
    }
}
