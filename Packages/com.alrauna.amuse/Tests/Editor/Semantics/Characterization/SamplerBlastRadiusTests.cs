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
    /// Characterization: what does one texture's import state invalidate?
    ///
    /// Architectural question — the two frontends couple alpha to
    /// <c>_MainTex</c> differently, and neither behavior was pinned. Poiyomi
    /// samples the alpha through the <c>_MainTex</c> sampler, so one unsupported
    /// import invalidates it. lilToon's alpha is attested rather than sampled,
    /// so it survives.
    ///
    /// This is the property a third frontend is most likely to get wrong,
    /// because it is invisible unless several texture slots are assigned at
    /// once. The two classes below are deliberately separate; no shared base is
    /// introduced to unify them.
    /// </summary>
    public sealed class PoiyomiSamplerBlastRadiusTests : PoiyomiFixtureTestBase
    {
        /// <summary>
        /// Every texture slot populated. Alpha is deliberately taken off
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
                    ? ImportTexture("blast_main", i => i.wrapMode = UnityEngine.TextureWrapMode.Mirror)
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
        public void Baseline_AlphaIsProven()
        {
            // A blast-radius test that started from an accidentally-unknown
            // baseline would prove nothing, so the baseline is asserted first.
            var result = Interpret(AllOutputsMaterial(unsupportedMain: false));

            AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void UnsupportedMainSampler_InvalidatesAlpha()
        {
            var result = Interpret(AllOutputsMaterial(unsupportedMain: true));

            // Poiyomi routes the alpha sample through the _MainTex sampler, so
            // the blast radius reaches alpha itself.
            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
        }
    }

    public sealed class LilToonSamplerBlastRadiusTests : LilToonFixtureTestBase
    {
        /// <summary>
        /// Every texture slot populated. The main texture must be a
        /// bounded LDR import so the alpha attestation inputs stay clean.
        /// </summary>
        private Material AllOutputsMaterial(bool unsupportedMain)
        {
            var material = NewFixtureMaterial();

            material.SetTexture(
                "_MainTex",
                unsupportedMain
                    ? ImportTexture("blast_main", i => i.wrapMode = UnityEngine.TextureWrapMode.Mirror)
                    : ImportTexture("blast_main"));

            material.SetFloat("_UseBumpMap", 1f);
            material.SetTexture("_BumpMap", ImportNormalMap("blast_bump"));

            material.SetFloat("_UseEmission", 1f);
            material.SetTexture("_EmissionMap", ImportOpaqueColorMap("blast_emis"));

            return material;
        }

        [Test]
        public void Baseline_AlphaIsProven()
        {
            var semantics = Interpret(AllOutputsMaterial(unsupportedMain: false))
                .Semantics;

            Assert.That(semantics.Alpha.IsComplete, Is.True, "Alpha");
        }

        [Test]
        public void UnsupportedMainSampler_AlphaSurvives()
        {
            var result = Interpret(AllOutputsMaterial(unsupportedMain: true));

            // Alpha is attested from LIL_RENDER rather than sampled, so it
            // survives an unsupported _MainTex sampler.
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
