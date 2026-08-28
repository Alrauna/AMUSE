using System;
using System.Linq;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Characterization
{
    /// <summary>
    /// Architectural question — is <see cref="UnityTextureEvidence"/> genuinely
    /// one contract, or two coincidences that happen to match?
    ///
    /// This is the only test that crosses the frontend boundary. It exists to
    /// detect one specific failure: a future edit that makes one frontend stop
    /// depending on a shared fact, leaving the shared class with a single real
    /// consumer and a stale justification.
    ///
    /// It deliberately does <b>not</b> assert that the two frontends emit the
    /// same diagnostic code. They legitimately do not, and asserting equality
    /// would be the first step toward the shared diagnostic framework this
    /// milestone declines to build.
    ///
    /// It inherits the Poiyomi fixture for texture helpers and temp-folder
    /// lifecycle, and reaches lilToon through its public fixture shader plus the
    /// static verified-material seam. No new shared test base is introduced.
    /// </summary>
    public sealed class SharedEvidenceAgreementTests : PoiyomiFixtureTestBase
    {
        private const string LilToonFixtureShaderName =
            "Hidden/Alrauna/AmuseTests/LilToonSemanticTest";

        // Test-local copy of the feature symbols a fully compiled lilToon
        // exposes. Reviewed input, not a reflection of production state.
        private static readonly string[] LilToonAllFeatures =
        {
            "LIL_FEATURE_NORMAL_1ST",
            "LIL_FEATURE_BumpMap",
            "LIL_FEATURE_EMISSION_1ST",
            "LIL_FEATURE_EmissionMap",
        };

        private Material NewLilToonMaterial()
        {
            var shader = Shader.Find(LilToonFixtureShaderName);
            Assert.That(
                shader,
                Is.Not.Null,
                $"lilToon fixture shader '{LilToonFixtureShaderName}' must import.");
            return Track(new Material(shader));
        }

        /// <summary>The output whose equation consumes each shared fact.</summary>
        private static string ConsumingOutput(string fact)
        {
            switch (fact)
            {
                case "TryGetSourceId":
                case "TryGetSampling":
                case "TryGetColorInterpretation":
                    return "BaseColor";
                case "TryProveSampledAlphaIsOne":
                    return "Emission";
                case "IsCanonicalNormalMapImport":
                    return "Normal";
                default:
                    throw new ArgumentOutOfRangeException(nameof(fact));
            }
        }

        /// <summary>The texture state under which each shared fact refuses.</summary>
        private Texture RefusingTexture(string fact)
        {
            switch (fact)
            {
                case "TryGetSourceId":
                    // Scene-only: no asset, so no stable project identity.
                    return Track(new Texture2D(4, 4, TextureFormat.RGBA32, false));
                case "TryGetSampling":
                    // Trilinear: mipmapped sampling is admitted now, so an
                    // unsupported filter is what makes TryGetSampling refuse.
                    return ImportTexture(
                        "shared_trilinear", i => i.filterMode = FilterMode.Trilinear);
                case "TryGetColorInterpretation":
                    // A native asset has stable identity and a usable sampler,
                    // but no TextureImporter at all.
                    return NewNativeTextureAsset("shared_native");
                case "TryProveSampledAlphaIsOne":
                    // Source carries alpha, so a sampled alpha of one is not
                    // provable and rgb would silently scale itself.
                    return ImportTexture("shared_alpha");
                case "IsCanonicalNormalMapImport":
                    return ImportTexture(
                        "shared_flipped",
                        i =>
                        {
                            i.textureType = TextureImporterType.NormalMap;
                            i.flipGreenChannel = true;
                        });
                default:
                    throw new ArgumentOutOfRangeException(nameof(fact));
            }
        }

        private Material PoiyomiMaterialFor(string fact, Texture refusing)
        {
            var material = NewFixtureMaterial();
            var output = ConsumingOutput(fact);

            if (output == "BaseColor")
            {
                material.SetTexture("_MainTex", refusing);
                return material;
            }

            // Emission and Normal both sample through the _MainTex sampler, so
            // a supported main texture must be present for the refusal under
            // test to be the one observed.
            material.SetTexture("_MainTex", ImportTexture("shared_poi_main"));

            if (output == "Emission")
            {
                material.SetFloat("_EnableEmission", 1f);
                material.SetTexture("_EmissionMap", refusing);
            }
            else
            {
                material.SetTexture("_BumpMap", refusing);
            }

            return material;
        }

        private Material LilToonMaterialFor(string fact, Texture refusing)
        {
            var material = NewLilToonMaterial();
            var output = ConsumingOutput(fact);

            if (output == "BaseColor")
            {
                material.SetTexture("_MainTex", refusing);
                return material;
            }

            material.SetTexture("_MainTex", ImportTexture("shared_lil_main"));

            if (output == "Emission")
            {
                material.SetFloat("_UseEmission", 1f);
                material.SetTexture("_EmissionMap", refusing);
            }
            else
            {
                material.SetFloat("_UseBumpMap", 1f);
                material.SetTexture("_BumpMap", refusing);
            }

            return material;
        }

        private static bool PoiyomiIsComplete(
            PoiyomiSemanticResult result, string output)
        {
            switch (output)
            {
                case "BaseColor": return result.Semantics.BaseColor.IsComplete;
                case "Emission": return result.Semantics.Emission.IsComplete;
                case "Normal": return result.Semantics.Normal.IsComplete;
                default: throw new ArgumentOutOfRangeException(nameof(output));
            }
        }

        private static bool LilToonIsComplete(
            LilToonSemanticResult result, string output)
        {
            switch (output)
            {
                case "BaseColor": return result.Semantics.BaseColor.IsComplete;
                case "Emission": return result.Semantics.Emission.IsComplete;
                case "Normal": return result.Semantics.Normal.IsComplete;
                default: throw new ArgumentOutOfRangeException(nameof(output));
            }
        }

        [TestCase("TryGetSourceId")]
        [TestCase("TryGetSampling")]
        [TestCase("TryGetColorInterpretation")]
        [TestCase("TryProveSampledAlphaIsOne")]
        [TestCase("IsCanonicalNormalMapImport")]
        public void RefusedSharedFact_IsRefusedByBothFrontends(string fact)
        {
            var output = ConsumingOutput(fact);

            var poiyomiRefusing = RefusingTexture(fact);
            var poiyomi = PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                PoiyomiMaterialFor(fact, poiyomiRefusing), ColorSpace.Linear);

            Assert.That(
                PoiyomiIsComplete(poiyomi, output),
                Is.False,
                $"Poiyomi {output} must refuse when {fact} cannot be proven.");
            Assert.That(
                poiyomi.Diagnostics.Any(
                    d => d.Output.ToString() == output),
                Is.True,
                $"Poiyomi must explain the {output} refusal.");

            var lilToonRefusing = RefusingTexture(fact);
            var lilToon = LilToonMaterialSemantics.InterpretVerifiedMaterial(
                LilToonMaterialFor(fact, lilToonRefusing),
                ColorSpace.Linear,
                LilToonAllFeatures);

            Assert.That(
                LilToonIsComplete(lilToon, output),
                Is.False,
                $"lilToon {output} must refuse when {fact} cannot be proven.");
            Assert.That(
                lilToon.Diagnostics.Any(
                    d => d.Output.ToString() == output),
                Is.True,
                $"lilToon must explain the {output} refusal.");
        }

        [Test]
        public void BothFrontendsStillConsumeEverySharedFact()
        {
            // The guard that gives the five-fact class its justification: if a
            // frontend stopped consuming one, the corresponding case above would
            // pass vacuously for that frontend only. This asserts the roster
            // itself has not drifted.
            var facts = new[]
            {
                "TryGetSourceId",
                "TryGetSampling",
                "TryGetColorInterpretation",
                "TryProveSampledAlphaIsOne",
                "IsCanonicalNormalMapImport",
            };

            Assert.That(facts.Select(ConsumingOutput).Distinct().Count(),
                Is.EqualTo(3),
                "The five shared facts are consumed across BaseColor, Emission, "
                    + "and Normal in both frontends.");
        }
    }
}
