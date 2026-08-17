using System.Linq;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CoreWrapMode = Alrauna.Amuse.Editor.Semantics.TextureWrapMode;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// Task 5 Normal equation tests. Only the two approved forms are provable:
    /// the pinned <c>"bump"</c> default as <c>Unmodified</c>, and a unit-strength
    /// canonical tangent-space normal map sampled with the shared MainTex
    /// sampler. Non-unit strength, every traced normal writer, unsupported
    /// UV/sampling/identity/import, and a missing MainTex sampler each fail
    /// closed at Normal only.
    /// </summary>
    public sealed class PoiyomiNormalTests : PoiyomiFixtureTestBase
    {
        // Enabled source blocks the design traces as perturbing or replacing the
        // tangent-space normal: detail normals, RGBA-mask normal replacement,
        // the four decals, and internal/offset parallax.
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

        private static PoiyomiSemanticResult Interpret(Material material)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Linear);
        }

        private static NormalSemanticValue Normal(PoiyomiSemanticResult result)
        {
            Assert.That(
                result.Semantics.Normal.IsComplete,
                Is.True,
                "Normal expected complete.");
            return result.Semantics.Normal.GetCompleteValue();
        }

        // A material whose MainTex supplies a supported sampler and whose
        // BumpMap is a unit-strength canonical tangent-space normal map.
        private Material AssignedNormalMaterial()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("nrm_main"));
            material.SetTexture("_BumpMap", ImportTexture(
                "nrm_bump", i => i.textureType = TextureImporterType.NormalMap));
            return material;
        }

        // --- Unmodified default --------------------------------------------

        [Test]
        public void MissingBumpMap_IsUnmodified()
        {
            var value = Normal(Interpret(NewFixtureMaterial()));

            Assert.That(value, Is.EqualTo(NormalSemanticValue.Unmodified()));
        }

        // --- Supported tangent-space normal --------------------------------

        [Test]
        public void AssignedNormalMap_UnitStrength_IsTangentSpaceNormalMap()
        {
            var material = AssignedNormalMaterial();

            var value = Normal(Interpret(material));

            var expected = NormalSemanticValue.TangentSpaceNormalMap(
                new TextureSample(
                    new TextureSourceId(
                        ExpectedToken(material.GetTexture("_BumpMap"))),
                    new UvMapping(0, Vector2.one, Vector2.zero),
                    new TextureSampling(
                        TextureFilterMode.Bilinear, CoreWrapMode.Repeat)));
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        public void AssignedNormalMap_UsesItsOwnUvChannelAndSt()
        {
            var material = AssignedNormalMaterial();
            material.SetFloat("_BumpMapUV", 1f);
            material.SetTextureScale("_BumpMap", new Vector2(2f, 3f));
            material.SetTextureOffset("_BumpMap", new Vector2(0.1f, 0.2f));

            var sample = Normal(Interpret(material)).GetTextureSample();

            Assert.That(sample.Coordinates.Channel, Is.EqualTo(1));
            Assert.That(sample.Coordinates.Scale, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(
                sample.Coordinates.Offset, Is.EqualTo(new Vector2(0.1f, 0.2f)));
        }

        // --- Strength ------------------------------------------------------

        [Test]
        public void NonUnitStrength_IsUnsupportedFeature(
            [Values(2f, -1f, 0.5f, 0f)] float strength)
        {
            var material = AssignedNormalMaterial();
            material.SetFloat("_BumpScale", strength);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Normal,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_BumpScale");
        }

        [Test]
        public void NonFiniteStrength_IsUnsupportedFeature()
        {
            var material = AssignedNormalMaterial();
            material.SetFloat("_BumpScale", float.NaN);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Normal,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_BumpScale");
        }

        // --- Traced normal writers -----------------------------------------

        [Test]
        public void NormalWriterEnabled_IsUnsupportedFeature(
            [ValueSource(nameof(NormalFeatureGates))] string property)
        {
            var material = AssignedNormalMaterial();
            material.SetFloat(property, 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Normal,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        // --- UV / sampling / identity / import -----------------------------

        [Test]
        public void UnsupportedUvChannel_IsUnsupportedUv()
        {
            var material = AssignedNormalMaterial();
            material.SetFloat("_BumpMapUV", 4f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Normal,
                PoiyomiSemanticDiagnosticCode.UnsupportedUv);
        }

        [Test]
        public void NonZeroPan_IsUnsupportedUv()
        {
            var material = AssignedNormalMaterial();
            material.SetVector("_BumpMapPan", new Vector4(0.1f, 0f, 0f, 0f));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Normal,
                PoiyomiSemanticDiagnosticCode.UnsupportedUv);
        }

        [Test]
        public void Stochastic_IsUnsupportedFeature()
        {
            var material = AssignedNormalMaterial();
            material.SetFloat("_BumpMapStochastic", 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Normal,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_BumpMapStochastic");
        }

        [Test]
        public void MissingMainTexSampler_IsUnsupportedSampling()
        {
            // The normal is sampled with the MainTex sampler; without an
            // assigned MainTex there is no sampler to prove.
            var material = NewFixtureMaterial();
            material.SetTexture("_BumpMap", ImportTexture(
                "nrm_nosampler", i => i.textureType = TextureImporterType.NormalMap));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Normal,
                PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
        }

        [Test]
        public void TrilinearMainSampler_IsUnsupportedSampling()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture(
                "nrm_trilinear", i => i.filterMode = FilterMode.Trilinear));
            material.SetTexture("_BumpMap", ImportTexture(
                "nrm_bump_tri", i => i.textureType = TextureImporterType.NormalMap));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Normal,
                PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
        }

        [Test]
        public void TransientBumpMap_IsUnstableIdentity()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("nrm_id_main"));
            material.SetTexture("_BumpMap", Track(new Texture2D(4, 4)));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Normal,
                PoiyomiSemanticDiagnosticCode.UnstableTextureIdentity,
                "_BumpMap");
        }

        [Test]
        public void NonNormalImport_IsUnsupportedTextureImport()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("nrm_imp_main"));
            // Imported as an ordinary color texture, not a normal map.
            material.SetTexture("_BumpMap", ImportTexture("nrm_notnormal"));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Normal,
                PoiyomiSemanticDiagnosticCode.UnsupportedTextureImport,
                "_BumpMap");
        }

        [Test]
        public void GreenChannelInverted_IsUnsupportedTextureImport()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("nrm_flip_main"));
            material.SetTexture("_BumpMap", ImportTexture(
                "nrm_flipped",
                i =>
                {
                    i.textureType = TextureImporterType.NormalMap;
                    i.flipGreenChannel = true;
                }));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Normal,
                PoiyomiSemanticDiagnosticCode.UnsupportedTextureImport,
                "_BumpMap");
        }

        // --- Output-local invalidation -------------------------------------

        [Test]
        public void NormalOnlyFailure_LeavesOtherOutputsUnchanged()
        {
            var material = AssignedNormalMaterial();
            material.SetFloat("_BumpScale", 2f);

            var result = Interpret(material);

            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.Normal,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_BumpScale");
            AssertOutputComplete(result, PoiyomiSemanticOutput.BaseColor);
            AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
            // Emission is not yet produced; a Normal failure must not leak a
            // diagnostic into it (output-local invalidation).
            Assert.That(
                result.Diagnostics.Any(
                    d => d.Output == PoiyomiSemanticOutput.Emission),
                Is.False,
                "A Normal-only failure must not emit an Emission diagnostic.");
        }
    }
}
