using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Semantics.LilToon;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Characterization
{
    /// <summary>
    /// Characterization: which outputs does one texture's import state
    /// invalidate?
    ///
    /// Architectural question — the two frontends couple their outputs to
    /// <c>_MainTex</c>'s sampler differently, and neither blast radius was
    /// pinned. Poiyomi samples every assigned map through the <c>_MainTex</c>
    /// sampler, so one unsupported import invalidates all four outputs. lilToon
    /// borrows that sampler only for the normal: emission declares its own, and
    /// alpha is attested rather than sampled, so both survive.
    ///
    /// This is the property a third frontend is most likely to get wrong,
    /// because it is invisible unless several texture slots are assigned at
    /// once. The two classes below are deliberately separate; no shared base is
    /// introduced to unify them.
    /// </summary>
    public sealed class PoiyomiSamplerBlastRadiusTests : PoiyomiFixtureTestBase
    {
        private static PoiyomiSemanticResult Interpret(Material material)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Linear);
        }

        /// <summary>
        /// Every output populated and provable. Alpha is deliberately taken off
        /// the forced-opaque short-circuit, because a forced-opaque material
        /// never consults the sampler and would understate the coupling.
        /// </summary>
        private Material AllOutputsMaterial(bool unsupportedMain)
        {
            var material = NewFixtureMaterial();

            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);

            material.SetTexture(
                "_MainTex",
                unsupportedMain
                    ? ImportTexture("blast_main", i => i.filterMode = FilterMode.Trilinear)
                    : ImportTexture("blast_main"));

            material.SetTexture(
                "_BumpMap",
                ImportTexture(
                    "blast_bump",
                    i => i.textureType = TextureImporterType.NormalMap));

            material.SetFloat("_EnableEmission", 1f);
            material.SetTexture(
                "_EmissionMap",
                ImportTexture(
                    "blast_emis",
                    i => i.alphaSource = TextureImporterAlphaSource.None,
                    sourceHasAlpha: false));

            return material;
        }

        [Test]
        public void Baseline_AllFourOutputsAreProven()
        {
            // A blast-radius test that started from an accidentally-unknown
            // baseline would prove nothing, so the baseline is asserted first.
            var result = Interpret(AllOutputsMaterial(unsupportedMain: false));

            AssertOutputComplete(result, PoiyomiSemanticOutput.BaseColor);
            AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
            AssertOutputComplete(result, PoiyomiSemanticOutput.Emission);
            AssertOutputComplete(result, PoiyomiSemanticOutput.Normal);
        }

        [Test]
        public void UnsupportedMainSampler_InvalidatesAllFourOutputs()
        {
            var result = Interpret(AllOutputsMaterial(unsupportedMain: true));

            // Poiyomi routes every assigned sample through the _MainTex
            // sampler, including the emission map and the bump map, so the
            // blast radius is total.
            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.BaseColor,
                PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.Emission,
                PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.Normal,
                PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
        }
    }

    public sealed class LilToonSamplerBlastRadiusTests : LilToonFixtureTestBase
    {
        /// <summary>
        /// Every output populated and provable. The main texture must be a
        /// bounded LDR import so the tone-correction range proof holds.
        /// </summary>
        private Material AllOutputsMaterial(bool unsupportedMain)
        {
            var material = NewFixtureMaterial();

            material.SetTexture(
                "_MainTex",
                unsupportedMain
                    ? ImportTexture("blast_main", i => i.filterMode = FilterMode.Trilinear)
                    : ImportTexture("blast_main"));

            material.SetFloat("_UseBumpMap", 1f);
            material.SetTexture("_BumpMap", ImportNormalMap("blast_bump"));

            material.SetFloat("_UseEmission", 1f);
            material.SetTexture("_EmissionMap", ImportOpaqueColorMap("blast_emis"));

            return material;
        }

        [Test]
        public void Baseline_AllFourOutputsAreProven()
        {
            var semantics = Interpret(AllOutputsMaterial(unsupportedMain: false))
                .Semantics;

            Assert.That(semantics.BaseColor.IsComplete, Is.True, "BaseColor");
            Assert.That(semantics.Alpha.IsComplete, Is.True, "Alpha");
            Assert.That(semantics.Emission.IsComplete, Is.True, "Emission");
            Assert.That(semantics.Normal.IsComplete, Is.True, "Normal");
        }

        [Test]
        public void UnsupportedMainSampler_InvalidatesOnlyBaseColorAndNormal()
        {
            var result = Interpret(AllOutputsMaterial(unsupportedMain: true));

            // BaseColor samples _MainTex; Normal borrows its sampler.
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Normal,
                LilToonSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");

            // Emission declares sampler_EmissionMap, and alpha is attested from
            // LIL_RENDER rather than sampled. Both survive a failure that
            // invalidates their siblings — the behaviour that distinguishes this
            // frontend from Poiyomi.
            Assert.That(
                result.Semantics.Emission.IsComplete,
                Is.True,
                "Emission uses its own sampler and must survive.");
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Emission).Count,
                Is.EqualTo(0));

            Assert.That(
                result.Semantics.Alpha.IsComplete,
                Is.True,
                "Alpha is attested, never sampled, and must survive.");
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Alpha).Count,
                Is.EqualTo(0));
        }
    }
}
