using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CoreWrapMode = Alrauna.Amuse.Editor.Semantics.TextureWrapMode;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// Task 6 Emission equation tests. The supported subset is deliberately
    /// narrow: zero when nothing emits, slot-0 linear color times strength, and a
    /// slot-0 map whose sampled alpha is provably one. Every higher slot, every
    /// slot-0 modifier, every traced external emissive writer (decal/rim/matcap/
    /// etc.), an RGBA map, unsupported UV/sampling/identity/import, a Gamma
    /// project, and non-finite controls each fail closed at Emission only.
    /// </summary>
    public sealed class PoiyomiEmissionTests : PoiyomiFixtureTestBase
    {
        // Slot-0 modifiers that add to, tint, replace, or animate the slot term.
        // _EmissionReplace0 is proven off by the shared base-color writer gate
        // (it also writes BaseColor) and is covered separately.
        private static readonly string[] EmissionSlot0Modifiers =
        {
            "_EmissionColorThemeIndex",
            "_EmissionBaseColorAsMap",
            "_EmissionFluorescence",
            "_EmissionHueShiftEnabled",
            "_EmissionCenterOutEnabled",
            "_EnableGITDEmission",
            "_EmissionBlinkingEnabled",
            "_ScrollingEmission",
            "_EmissionAL0Enabled",
            "_EmissionMaskInvert",
            "_EmissionMask0GlobalMask",
        };

        private static readonly string[] HigherEmissionSlots =
        {
            "_EnableEmission1",
            "_EnableEmission2",
            "_EnableEmission3",
        };

        // A representative set of traced external features that emit alongside the
        // slots. Each must block the zero claim even with every emission slot off.
        private static readonly string[] ExternalEmissiveWriters =
        {
            "_DecalEnabled",
            "_BackFaceEnabled",
            "_RGBMaskEnabled",
            "_MatcapEnable",
            "_CubeMapEnabled",
            "_EnableRimLighting",
            "_EnableFlipbook",
            "_EnableDissolve",
        };

        private static PoiyomiSemanticResult Interpret(
            Material material, ColorSpace colorSpace = ColorSpace.Linear)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                material, colorSpace);
        }

        private static ColorSemanticValue Emission(PoiyomiSemanticResult result)
        {
            Assert.That(
                result.Semantics.Emission.IsComplete,
                Is.True,
                "Emission expected complete.");
            return result.Semantics.Emission.GetCompleteValue();
        }

        private Material Slot0Material()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_EnableEmission", 1f);
            return material;
        }

        private Material Slot0MapMaterial(Texture emissionMap)
        {
            var material = Slot0Material();
            material.SetTexture("_MainTex", ImportTexture("em_main"));
            material.SetTexture("_EmissionMap", emissionMap);
            return material;
        }

        // A slot-0 emission map whose source carries no alpha and imports alpha as
        // none, so its sampled alpha is provably one.
        private Texture2D AlphaOneMap(string name)
        {
            return ImportTexture(
                name,
                i => i.alphaSource = TextureImporterAlphaSource.None,
                sourceHasAlpha: false);
        }

        private static TextureSample MainSampled(Texture emissionMap)
        {
            return new TextureSample(
                new TextureSourceId(ExpectedToken(emissionMap)),
                new UvMapping(0, Vector2.one, Vector2.zero),
                new TextureSampling(
                    TextureFilterMode.Bilinear, CoreWrapMode.Repeat));
        }

        // --- Zero constant -------------------------------------------------

        [Test]
        public void AllSlotsDisabled_IsZeroConstant()
        {
            var value = Emission(Interpret(NewFixtureMaterial()));

            Assert.That(
                value, Is.EqualTo(ColorSemanticValue.Constant(Vector3.zero)));
        }

        [Test]
        public void AllSlotsDisabled_InGammaProject_IsStillZeroConstant()
        {
            // Zero emission is color-space independent; a Gamma project must not
            // invalidate a material that provably emits nothing.
            var value = Emission(
                Interpret(NewFixtureMaterial(), ColorSpace.Gamma));

            Assert.That(
                value, Is.EqualTo(ColorSemanticValue.Constant(Vector3.zero)));
        }

        // --- Slot 0 constant color -----------------------------------------

        [Test]
        public void Slot0NoMap_IsLinearColorTimesStrengthConstant()
        {
            var material = Slot0Material();
            material.SetColor("_EmissionColor", new Color(0.6f, 0.3f, 0.9f, 1f));
            material.SetFloat("_EmissionStrength", 2f);

            var stored = material.GetColor("_EmissionColor").linear;
            var expected = new Vector3(stored.r, stored.g, stored.b) * 2f;

            Assert.That(
                Emission(Interpret(material)),
                Is.EqualTo(ColorSemanticValue.Constant(expected)));
        }

        // --- Slot 0 mapped emission (proven sampled alpha one) -------------

        [Test]
        public void Slot0MapAlphaOne_IdentityTint_IsTextureSample()
        {
            var map = AlphaOneMap("em_id");
            var material = Slot0MapMaterial(map);
            material.SetFloat("_EmissionStrength", 1f);

            Assert.That(
                Emission(Interpret(material)),
                Is.EqualTo(ColorSemanticValue.Texture(
                    MainSampled(map), TextureColorInterpretation.Srgb)));
        }

        [Test]
        public void Slot0MapAlphaOne_NonIdentityTint_IsTextureTimesConstant()
        {
            var map = AlphaOneMap("em_scaled");
            var material = Slot0MapMaterial(map);
            material.SetFloat("_EmissionStrength", 2f);

            Assert.That(
                Emission(Interpret(material)),
                Is.EqualTo(ColorSemanticValue.TextureTimesConstant(
                    MainSampled(map),
                    TextureColorInterpretation.Srgb,
                    new Vector3(2f, 2f, 2f))));
        }

        [Test]
        public void Slot0Map_UsesItsOwnUvChannelAndSt()
        {
            var map = AlphaOneMap("em_uv");
            var material = Slot0MapMaterial(map);
            material.SetFloat("_EmissionStrength", 1f);
            material.SetFloat("_EmissionMapUV", 2f);
            material.SetTextureScale("_EmissionMap", new Vector2(2f, 4f));
            material.SetTextureOffset("_EmissionMap", new Vector2(0.3f, 0.6f));

            var sample = Emission(Interpret(material)).GetTextureSample();

            Assert.That(sample.Coordinates.Channel, Is.EqualTo(2));
            Assert.That(sample.Coordinates.Scale, Is.EqualTo(new Vector2(2f, 4f)));
            Assert.That(
                sample.Coordinates.Offset, Is.EqualTo(new Vector2(0.3f, 0.6f)));
        }

        // --- Slot selection ------------------------------------------------

        [Test]
        public void HigherSlotEnabled_IsUnsupportedFeature(
            [ValueSource(nameof(HigherEmissionSlots))] string slot)
        {
            var material = NewFixtureMaterial();
            material.SetFloat(slot, 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                slot);
        }

        [Test]
        public void MultipleSlotsEnabled_IsUnsupportedFeature()
        {
            var material = Slot0Material();
            material.SetFloat("_EnableEmission1", 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_EnableEmission1");
        }

        // --- Slot-0 modifiers and external writers -------------------------

        [Test]
        public void Slot0ModifierEnabled_IsUnsupportedFeature(
            [ValueSource(nameof(EmissionSlot0Modifiers))] string property)
        {
            var material = Slot0Material();
            material.SetFloat(property, 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        [Test]
        public void ExternalEmissiveWriterEnabled_BlocksZeroConstant(
            [ValueSource(nameof(ExternalEmissiveWriters))] string property)
        {
            // Every emission slot is disabled, yet a traced external emissive
            // feature is on: the result must be Unknown, never a false zero.
            var material = NewFixtureMaterial();
            material.SetFloat(property, 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        [Test]
        public void AssignedEmissionMask_IsUnsupportedFeature()
        {
            var material = Slot0Material();
            material.SetTexture("_EmissionMask", ImportTexture("em_mask"));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_EmissionMask");
        }

        // --- Color space and finiteness ------------------------------------

        [Test]
        public void GammaProject_Slot0Enabled_IsUnsupportedColorSpace()
        {
            var material = Slot0Material();
            material.SetFloat("_EmissionStrength", 1f);

            AssertUnsupportedOutput(
                Interpret(material, ColorSpace.Gamma),
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedColorSpace);
        }

        [Test]
        public void NonFiniteColor_IsUnsupportedFeature()
        {
            var material = Slot0Material();
            material.SetColor("_EmissionColor", new Color(float.NaN, 0f, 0f, 1f));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_EmissionColor");
        }

        [Test]
        public void NonFiniteStrength_IsUnsupportedFeature()
        {
            var material = Slot0Material();
            material.SetFloat("_EmissionStrength", float.PositiveInfinity);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_EmissionStrength");
        }

        // --- Mapped emission failure paths ---------------------------------

        [Test]
        public void MapUnsupportedUvChannel_IsUnsupportedUv()
        {
            var material = Slot0MapMaterial(AlphaOneMap("em_uvbad"));
            material.SetFloat("_EmissionStrength", 1f);
            material.SetFloat("_EmissionMapUV", 4f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedUv);
        }

        [Test]
        public void MapNonZeroPan_IsUnsupportedUv()
        {
            var material = Slot0MapMaterial(AlphaOneMap("em_pan"));
            material.SetFloat("_EmissionStrength", 1f);
            material.SetVector("_EmissionMapPan", new Vector4(0.1f, 0f, 0f, 0f));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedUv);
        }

        [Test]
        public void MapMissingMainSampler_IsUnsupportedSampling()
        {
            // Assigned emission map, no MainTex: no shared sampler to prove.
            var material = Slot0Material();
            material.SetFloat("_EmissionStrength", 1f);
            material.SetTexture("_EmissionMap", AlphaOneMap("em_nosampler"));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
        }

        [Test]
        public void MapTrilinearMainSampler_IsUnsupportedSampling()
        {
            var material = Slot0Material();
            material.SetFloat("_EmissionStrength", 1f);
            material.SetTexture("_MainTex", ImportTexture(
                "em_trilinear", i => i.filterMode = FilterMode.Trilinear));
            material.SetTexture("_EmissionMap", AlphaOneMap("em_tri"));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
        }

        [Test]
        public void MapTransientTexture_IsUnstableIdentity()
        {
            var material = Slot0Material();
            material.SetFloat("_EmissionStrength", 1f);
            material.SetTexture("_MainTex", ImportTexture("em_id_main"));
            material.SetTexture("_EmissionMap", Track(new Texture2D(4, 4)));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnstableTextureIdentity,
                "_EmissionMap");
        }

        [Test]
        public void MapNoImporter_IsUnsupportedTextureImport()
        {
            var material = Slot0Material();
            material.SetFloat("_EmissionStrength", 1f);
            material.SetTexture("_MainTex", ImportTexture("em_imp_main"));
            material.SetTexture("_EmissionMap", NewNativeTextureAsset("em_native"));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedTextureImport,
                "_EmissionMap");
        }

        [Test]
        public void MapWithAlpha_IsUnsupportedTextureImport()
        {
            // A general RGBA emission map is unknown rather than silently
            // dropping the sample alpha the source would multiply by.
            var material = Slot0MapMaterial(ImportTexture("em_rgba"));
            material.SetFloat("_EmissionStrength", 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedTextureImport,
                "_EmissionMap");
        }

        // --- Output-local invalidation -------------------------------------

        [Test]
        public void EmissionReplace0_AlsoInvalidatesBaseColor()
        {
            var material = Slot0Material();
            material.SetFloat("_EmissionReplace0", 1f);

            var result = Interpret(material);

            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_EmissionReplace0");
            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.BaseColor,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_EmissionReplace0");
        }

        [Test]
        public void OrdinaryEmissionModifier_LeavesBaseColorComplete()
        {
            var material = Slot0Material();
            material.SetFloat("_EmissionFluorescence", 1f);

            var result = Interpret(material);

            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_EmissionFluorescence");
            AssertOutputComplete(result, PoiyomiSemanticOutput.BaseColor);
        }

        [Test]
        public void EmissionOnlyFailure_LeavesAlphaAndNormalComplete()
        {
            var material = Slot0Material();
            material.SetFloat("_EmissionFluorescence", 1f);

            var result = Interpret(material);

            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_EmissionFluorescence");
            AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
            AssertOutputComplete(result, PoiyomiSemanticOutput.Normal);
        }
    }
}
