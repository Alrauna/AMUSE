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
    /// Task 7 adversarial cross-output and contract integration tests. They
    /// exercise the whole verified-material seam (and the public entry point for
    /// attestation) rather than one output: one-diagnostic-per-output ordering,
    /// snapshot immutability, shared-texture identity with independent UV roles,
    /// a single unsupported sampler failing every assigned sample while constant
    /// outputs survive, no invented sampler for aux maps, and the representation
    /// boundary that keeps an Unknown output from ever presenting a value a
    /// downstream opaque/separation consumer could read.
    ///
    /// Identity/attestation, hash normalization (line-ending equality),
    /// force-opaque-versus-coverage refusal, and per-output refusal cases are
    /// proven in the Task 2/4/5/6 suites and are deliberately not repeated here.
    /// </summary>
    public sealed class PoiyomiAdversarialTests : PoiyomiFixtureTestBase
    {
        private static PoiyomiSemanticResult Interpret(Material material)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Linear);
        }

        private static ColorSemanticValue BaseColor(PoiyomiSemanticResult result)
        {
            Assert.That(result.Semantics.BaseColor.IsComplete, Is.True);
            return result.Semantics.BaseColor.GetCompleteValue();
        }

        private static ColorSemanticValue Emission(PoiyomiSemanticResult result)
        {
            Assert.That(result.Semantics.Emission.IsComplete, Is.True);
            return result.Semantics.Emission.GetCompleteValue();
        }

        private static TextureSample MainSample(Texture texture)
        {
            return new TextureSample(
                new TextureSourceId(ExpectedToken(texture)),
                new UvMapping(0, Vector2.one, Vector2.zero),
                new TextureSampling(
                    TextureFilterMode.Bilinear, CoreWrapMode.Repeat));
        }

        // A texture whose source carries no alpha and imports alpha as none, so
        // its sampled alpha is provably one (needed for a mapped emission sample).
        private Texture2D AlphaOneMap(string name)
        {
            return ImportTexture(
                name,
                i => i.alphaSource = TextureImporterAlphaSource.None,
                sourceHasAlpha: false);
        }

        // --- Diagnostic ordering across outputs ----------------------------

        [Test]
        public void MultipleFailingOutputs_GiveOneDiagnosticPerOutputInOutputOrder()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_DetailEnabled", 1f);   // BaseColor, Emission, Normal
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_AlphaMod", 1f);        // Alpha
            material.SetTexture("_BumpMap", ImportTexture("multi_bump"));

            var result = Interpret(material);

            Assert.That(result.IsSupportedMaterial, Is.True);
            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            Assert.That(result.Semantics.Normal.IsComplete, Is.False);

            var outputs = result.Diagnostics.Select(d => d.Output).ToArray();
            Assert.That(
                outputs,
                Is.EqualTo(new[]
                {
                    PoiyomiSemanticOutput.BaseColor,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticOutput.Emission,
                    PoiyomiSemanticOutput.Normal,
                }),
                "Diagnostics appear once per output in declared output order.");
            Assert.That(
                outputs.Distinct().Count(),
                Is.EqualTo(outputs.Length),
                "Each unknown output contributes exactly one primary diagnostic.");
        }

        // --- Attestation gates the public entry point ----------------------

        [Test]
        public void PublicEntry_UnattestedSchemaCompleteShader_IsRefusedBeforeInterpretation()
        {
            // The fixture exposes the full Poiyomi property schema but is not the
            // pinned source; the public entry point refuses it at the material
            // boundary with one material diagnostic and never reaches an equation.
            var material = NewFixtureMaterial();

            var result = PoiyomiMaterialSemantics.AnalyzeBaseMaterial(material);

            Assert.That(result.IsSupportedMaterial, Is.False);
            Assert.That(result.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(
                result.Diagnostics[0].Output,
                Is.EqualTo(PoiyomiSemanticOutput.Material));
            Assert.That(
                result.Diagnostics[0].Code,
                Is.EqualTo(PoiyomiSemanticDiagnosticCode.UnsupportedShader));
            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
        }

        // --- Snapshot immutability -----------------------------------------

        [Test]
        public void ReturnedSemantics_AreSnapshot_UnaffectedByLaterMaterialMutation()
        {
            var originalTexture = ImportTexture("snapshot_main");
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", originalTexture);
            material.SetColor("_Color", Color.white);

            var result = Interpret(material);
            var expected = ColorSemanticValue.Texture(
                MainSample(originalTexture), TextureColorInterpretation.Srgb);
            Assert.That(BaseColor(result), Is.EqualTo(expected));

            // Mutate the source material after analysis.
            material.SetColor("_Color", new Color(0.5f, 0.5f, 0.5f, 1f));
            material.SetTexture("_MainTex", ImportTexture("snapshot_other"));

            Assert.That(
                BaseColor(result),
                Is.EqualTo(expected),
                "The returned semantics is a snapshot, not a live material view.");
            Assert.That(
                BaseColor(Interpret(material)),
                Is.Not.EqualTo(expected),
                "A fresh interpretation reflects the mutation, proving it real.");
        }

        // --- No invented sampler for auxiliary maps ------------------------

        [Test]
        public void MissingMainTex_WithAssignedAuxMaps_InventsNoSamplerState()
        {
            // The normal and emission maps are sampled with the MainTex sampler.
            // With no MainTex there is no sampler to prove, so both refuse rather
            // than invent one, while the constant color and forced-opaque alpha
            // stand.
            var material = NewFixtureMaterial();
            material.SetTexture("_BumpMap", ImportTexture(
                "aux_bump", i => i.textureType = TextureImporterType.NormalMap));
            material.SetFloat("_EnableEmission", 1f);
            material.SetFloat("_EmissionStrength", 1f);
            material.SetTexture("_EmissionMap", AlphaOneMap("aux_emission"));

            var result = Interpret(material);

            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.Normal,
                PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
            AssertOutputComplete(result, PoiyomiSemanticOutput.BaseColor);
            AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
        }

        // --- Shared texture identity with independent roles ----------------

        [Test]
        public void SharedTexture_InTwoOutputs_HasEqualIdentityWithIndependentUv()
        {
            var shared = AlphaOneMap("shared_map");
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", shared);       // BaseColor, uv 0
            material.SetColor("_Color", Color.white);
            material.SetFloat("_EnableEmission", 1f);       // Emission, uv 2
            material.SetFloat("_EmissionStrength", 1f);
            material.SetTexture("_EmissionMap", shared);
            material.SetFloat("_EmissionMapUV", 2f);
            material.SetTextureScale("_EmissionMap", new Vector2(3f, 3f));

            var result = Interpret(material);
            var baseSample = BaseColor(result).GetTextureSample();
            var emissionSample = Emission(result).GetTextureSample();

            Assert.That(
                emissionSample.Source,
                Is.EqualTo(baseSample.Source),
                "The same asset resolves to one shared identity across outputs.");
            Assert.That(baseSample.Coordinates.Channel, Is.EqualTo(0));
            Assert.That(emissionSample.Coordinates.Channel, Is.EqualTo(2));
            Assert.That(
                emissionSample.Coordinates.Scale, Is.EqualTo(new Vector2(3f, 3f)));
            Assert.That(
                emissionSample,
                Is.Not.EqualTo(baseSample),
                "Each output derives its own UV role from the shared identity.");
        }

        // --- One unsupported shared sampler -------------------------------

        [Test]
        public void UnsupportedSharedMainSampler_InvalidatesEverySample_ConstantSurvives()
        {
            // A trilinear MainTex sampler is unsupported and is shared by the
            // color, normal, and emission samples, so all three refuse; the
            // forced-opaque alpha is a constant and survives.
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture(
                "shared_trilinear", i => i.filterMode = FilterMode.Trilinear));
            material.SetColor("_Color", Color.white);
            material.SetTexture("_BumpMap", ImportTexture(
                "shared_bump", i => i.textureType = TextureImporterType.NormalMap));
            material.SetFloat("_EnableEmission", 1f);
            material.SetFloat("_EmissionStrength", 1f);
            material.SetTexture("_EmissionMap", AlphaOneMap("shared_emission"));

            var result = Interpret(material);

            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.BaseColor,
                PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.Normal,
                PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
            AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
        }

        // --- Representation boundary ---------------------------------------

        [Test]
        public void UnknownOutput_PresentsNoValue_ForDownstreamOpaqueOrSeparationUse()
        {
            // Representation boundary only (the classifier and separation planner
            // are neither called nor modified): an unproven output exposes no
            // value a downstream consumer could read as a concrete opaque result.
            // Uncertainty stays uncertain; it never defaults to a more aggressive
            // claim.
            var material = NewFixtureMaterial();
            material.SetFloat("_DetailEnabled", 1f);

            var baseColor = Interpret(material).Semantics.BaseColor;

            Assert.That(baseColor.IsComplete, Is.False);
            Assert.That(
                () => baseColor.GetCompleteValue(),
                Throws.InvalidOperationException,
                "An unknown output must yield no value, forcing conservative "
                    + "handling downstream.");
        }
    }
}
