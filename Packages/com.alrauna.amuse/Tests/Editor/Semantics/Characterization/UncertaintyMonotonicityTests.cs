using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Semantics;
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
    /// Shared assertion for the monotonicity invariant. Removing evidence may
    /// only ever lose information: an output must come back structurally equal,
    /// or become <c>Unknown</c>. It must never turn <c>Unknown</c> into
    /// <c>Complete</c>, and never return a different <c>Complete</c> value.
    ///
    /// This is a single generic assertion over the semantic core's own
    /// structural equality, not a test framework. Every mutation below degrades
    /// an existing asset's import in place, so texture identity is unchanged and
    /// only the evidence is reduced; swapping in a different asset would change
    /// the value legitimately and would not test this property.
    /// </summary>
    internal static class MonotonicityAssert
    {
        internal static void NoInformationGain<T>(
            SemanticOutput<T> baseline,
            SemanticOutput<T> reduced,
            string output,
            string mutation)
            where T : class
        {
            if (!reduced.IsComplete)
            {
                return;
            }

            Assert.That(
                baseline.IsComplete,
                Is.True,
                $"{output}: removing evidence ({mutation}) turned Unknown into "
                    + "Complete. Uncertainty must never widen a claim.");
            Assert.That(
                reduced.Equals(baseline),
                Is.True,
                $"{output}: removing evidence ({mutation}) produced a different "
                    + "Complete value.");
        }

        internal static void NoInformationGain(
            MaterialSemantics baseline,
            MaterialSemantics reduced,
            string mutation)
        {
            NoInformationGain(
                baseline.BaseColor, reduced.BaseColor, "BaseColor", mutation);
            NoInformationGain(baseline.Alpha, reduced.Alpha, "Alpha", mutation);
            NoInformationGain(
                baseline.Emission, reduced.Emission, "Emission", mutation);
            NoInformationGain(baseline.Normal, reduced.Normal, "Normal", mutation);
        }

        /// <summary>
        /// Degrades an already-imported asset in place. The asset path, GUID, and
        /// local file id are untouched, so the semantic identity is stable across
        /// the baseline and reduced observations.
        /// </summary>
        internal static void Reimport(
            Texture texture,
            Action<TextureImporter> configure)
        {
            var path = AssetDatabase.GetAssetPath(texture);
            Assert.That(
                string.IsNullOrEmpty(path),
                Is.False,
                "Monotonicity mutations require an importer-backed asset.");

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            configure(importer);
            importer.SaveAndReimport();
        }
    }

    /// <summary>
    /// Architectural question — can removing evidence ever produce a more
    /// informative claim? Nothing in the repository tested this before; each
    /// frontend only had scattered single instances.
    /// </summary>
    public sealed class PoiyomiUncertaintyMonotonicityTests : PoiyomiFixtureTestBase
    {
        private static PoiyomiSemanticResult Interpret(Material material)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Linear);
        }

        private Texture2D _main;
        private Texture2D _bump;
        private Texture2D _emission;

        private Material FullyProvenMaterial()
        {
            var material = NewFixtureMaterial();

            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);

            _main = ImportTexture("mono_main");
            material.SetTexture("_MainTex", _main);

            _bump = ImportTexture(
                "mono_bump",
                i => i.textureType = TextureImporterType.NormalMap);
            material.SetTexture("_BumpMap", _bump);

            material.SetFloat("_EnableEmission", 1f);
            _emission = ImportTexture(
                "mono_emis",
                i => i.alphaSource = TextureImporterAlphaSource.None,
                sourceHasAlpha: false);
            material.SetTexture("_EmissionMap", _emission);

            return material;
        }

        [TestCase("MainTexBecomesMipmapped")]
        [TestCase("BumpMapLosesNormalImport")]
        [TestCase("BumpMapGreenChannelFlipped")]
        [TestCase("EmissionMapGainsImportedAlpha")]
        public void RemovingEvidence_NeverAddsInformation(string mutation)
        {
            var material = FullyProvenMaterial();

            var baseline = Interpret(material).Semantics;
            Assert.That(
                baseline.BaseColor.IsComplete && baseline.Alpha.IsComplete &&
                baseline.Emission.IsComplete && baseline.Normal.IsComplete,
                Is.True,
                "The monotonicity baseline must start fully proven.");

            switch (mutation)
            {
                case "MainTexBecomesMipmapped":
                    MonotonicityAssert.Reimport(
                        _main, i => i.mipmapEnabled = true);
                    break;
                case "BumpMapLosesNormalImport":
                    MonotonicityAssert.Reimport(
                        _bump, i => i.textureType = TextureImporterType.Default);
                    break;
                case "BumpMapGreenChannelFlipped":
                    MonotonicityAssert.Reimport(
                        _bump, i => i.flipGreenChannel = true);
                    break;
                case "EmissionMapGainsImportedAlpha":
                    MonotonicityAssert.Reimport(
                        _emission,
                        i => i.alphaSource =
                            TextureImporterAlphaSource.FromGrayScale);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation));
            }

            var reduced = Interpret(material).Semantics;

            MonotonicityAssert.NoInformationGain(baseline, reduced, mutation);
        }
    }

    public sealed class LilToonUncertaintyMonotonicityTests : LilToonFixtureTestBase
    {
        private Texture2D _main;
        private Texture2D _bump;
        private Texture2D _emission;

        private Material FullyProvenMaterial()
        {
            var material = NewFixtureMaterial();

            _main = ImportTexture("mono_main");
            material.SetTexture("_MainTex", _main);

            material.SetFloat("_UseBumpMap", 1f);
            _bump = ImportNormalMap("mono_bump");
            material.SetTexture("_BumpMap", _bump);

            material.SetFloat("_UseEmission", 1f);
            _emission = ImportOpaqueColorMap("mono_emis");
            material.SetTexture("_EmissionMap", _emission);

            return material;
        }

        private void AssertBaselineFullyProven(MaterialSemantics baseline)
        {
            Assert.That(
                baseline.BaseColor.IsComplete && baseline.Alpha.IsComplete &&
                baseline.Emission.IsComplete && baseline.Normal.IsComplete,
                Is.True,
                "The monotonicity baseline must start fully proven.");
        }

        [TestCase("MainTexBecomesMipmapped")]
        [TestCase("BumpMapLosesNormalImport")]
        [TestCase("BumpMapGreenChannelFlipped")]
        [TestCase("EmissionMapGainsImportedAlpha")]
        public void RemovingImportEvidence_NeverAddsInformation(string mutation)
        {
            var material = FullyProvenMaterial();

            var baseline = Interpret(material).Semantics;
            AssertBaselineFullyProven(baseline);

            switch (mutation)
            {
                case "MainTexBecomesMipmapped":
                    MonotonicityAssert.Reimport(
                        _main, i => i.mipmapEnabled = true);
                    break;
                case "BumpMapLosesNormalImport":
                    MonotonicityAssert.Reimport(
                        _bump, i => i.textureType = TextureImporterType.Default);
                    break;
                case "BumpMapGreenChannelFlipped":
                    MonotonicityAssert.Reimport(
                        _bump, i => i.flipGreenChannel = true);
                    break;
                case "EmissionMapGainsImportedAlpha":
                    MonotonicityAssert.Reimport(
                        _emission,
                        i => i.alphaSource =
                            TextureImporterAlphaSource.FromGrayScale);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation));
            }

            var reduced = Interpret(material).Semantics;

            MonotonicityAssert.NoInformationGain(baseline, reduced, mutation);
        }

        /// <summary>
        /// The sharpest case in the milestone: compile-time stripping is the one
        /// evidence class that is invisible in material state. Removing a symbol
        /// models "the project stripped this feature" and must only ever lose
        /// information.
        /// </summary>
        [Test]
        public void RemovingEachCompiledFeature_NeverAddsInformation(
            [ValueSource(nameof(AllFeatures))] string stripped)
        {
            var material = FullyProvenMaterial();

            var baseline = Interpret(material).Semantics;
            AssertBaselineFullyProven(baseline);

            var remaining = AllFeatures
                .Where(f => !string.Equals(f, stripped, StringComparison.Ordinal))
                .ToArray();

            var reduced = Interpret(material, remaining).Semantics;

            MonotonicityAssert.NoInformationGain(
                baseline, reduced, "stripped " + stripped);
        }

        [Test]
        public void RemovingEveryCompiledFeature_NeverAddsInformation()
        {
            var material = FullyProvenMaterial();

            var baseline = Interpret(material).Semantics;
            AssertBaselineFullyProven(baseline);

            var reduced = Interpret(material, new string[0]).Semantics;

            MonotonicityAssert.NoInformationGain(
                baseline, reduced, "no features compiled");

            // Alpha depends on no feature symbol at all: it is attested from
            // LIL_RENDER, so it must survive total feature stripping. Losing it
            // here would mean the attestation channel had leaked into the
            // feature channel.
            Assert.That(
                reduced.Alpha.IsComplete,
                Is.True,
                "Alpha is attested, not feature-gated, and must survive.");
        }
    }
}
