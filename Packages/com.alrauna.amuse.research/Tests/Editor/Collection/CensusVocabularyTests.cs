using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Research.Census;
using Alrauna.Amuse.Research.Collection;
using NUnit.Framework;

namespace Alrauna.Amuse.Research.Tests.Editor.Collection
{
    /// <summary>
    /// Drift detection for the census vocabulary. Every mirror in
    /// CensusCategories is a snapshot of AMUSE's own enums; these tests are the
    /// half of the snapshot contract that watches AMUSE rather than the census,
    /// and they are the reason a new AMUSE value fails loudly in CI instead of
    /// being miscounted in a private run.
    /// </summary>
    public sealed class CensusVocabularyTests
    {
        [Test]
        public void FriendGrantsExposeAmuseInternalsToBothResearchAssemblies()
        {
            // Naming RendererAnalysisRefusal here proves the grant to the test
            // assembly; this file would not compile without it. CensusVocabulary
            // names the same internal enum in its own signature, so it could not
            // compile without the grant to the collector assembly. Asserted as
            // behaviour rather than assumed: if either grant is missing, every
            // later collector test fails for a reason that looks unrelated.
            Assert.That(
                CensusVocabulary.ToCensus(RendererAnalysisRefusal.None),
                Is.EqualTo(RendererRefusal.None));
        }

        [Test]
        public void RendererRefusalMirrorsAmuse()
        {
            CollectionAssert.AreEquivalent(
                System.Enum.GetNames(typeof(RendererRefusal)),
                System.Enum.GetNames(typeof(RendererAnalysisRefusal)));
        }

        [Test]
        public void AlphaResolutionFailureMirrorsAmuse()
        {
            CollectionAssert.AreEquivalent(
                System.Enum.GetNames(typeof(AlphaResolutionFailure)),
                System.Enum.GetNames(
                    typeof(Alrauna.Amuse.Editor.Analysis
                        .AlphaResolutionFailure)));
        }

        [Test]
        public void SeparationDispositionMirrorsAmuse()
        {
            CollectionAssert.AreEquivalent(
                System.Enum.GetNames(typeof(SeparationDisposition)),
                System.Enum.GetNames(
                    typeof(Alrauna.Amuse.Editor.Analysis
                        .SubmeshSeparationDisposition)));
        }

        [Test]
        public void EveryAmuseAlphaFailureMaps()
        {
            // Exhaustiveness, checked by driving every value through the
            // mapping. A missing arm throws rather than guessing, so a gap
            // surfaces here rather than as a miscategorized census row.
            foreach (Alrauna.Amuse.Editor.Analysis.AlphaResolutionFailure value
                     in System.Enum.GetValues(
                         typeof(Alrauna.Amuse.Editor.Analysis
                             .AlphaResolutionFailure)))
            {
                Assert.That(
                    System.Enum.IsDefined(
                        typeof(AlphaResolutionFailure),
                        CensusVocabulary.ToCensus(value)),
                    Is.True,
                    "Unmapped: " + value);
            }
        }

        [Test]
        public void EveryAmuseDispositionMaps()
        {
            foreach (Alrauna.Amuse.Editor.Analysis.SubmeshSeparationDisposition
                         value in System.Enum.GetValues(
                             typeof(Alrauna.Amuse.Editor.Analysis
                                 .SubmeshSeparationDisposition)))
            {
                Assert.That(
                    System.Enum.IsDefined(
                        typeof(SeparationDisposition),
                        CensusVocabulary.ToCensus(value)),
                    Is.True,
                    "Unmapped: " + value);
            }
        }

        [Test]
        public void UnattestedMaterialHasNoShaderFamily()
        {
            var material = new UnityEngine.Material(
                UnityEngine.Shader.Find("Standard"));
            try
            {
                Assert.That(
                    new CensusShaderFamily().Of(material),
                    Is.EqualTo(ShaderFamilyAttestation.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void NullMaterialHasNoShaderFamily()
        {
            // An empty material slot is an ordinary observation, not an error.
            Assert.That(
                new CensusShaderFamily().Of(null),
                Is.EqualTo(ShaderFamilyAttestation.None));
        }

        [Test]
        public void AmuseDeclaresNoShaderFrontendTheCensusDoesNotMeasure()
        {
            // The census names Poiyomi and lilToon directly in its attestation
            // trial; production depends on no naming convention. This is the
            // other half of that bargain: a pin against a literal, so a third
            // vendor adapter fails here in the commit that adds it and a person
            // decides whether the census should measure it.
            //
            // Blind spot, recorded rather than hidden: a frontend added inside
            // an existing vendor namespace creates no new namespace and would
            // not fail this test.
            var namespaces = new System.Collections.Generic.SortedSet<string>(
                System.StringComparer.Ordinal);
            foreach (var type in typeof(RendererAnalysisRefusal).Assembly
                         .GetTypes())
            {
                if (type.Namespace == null) continue;
                if (!type.Namespace.StartsWith(
                        "Alrauna.Amuse.Editor.Semantics.",
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                namespaces.Add(type.Namespace);
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    "Alrauna.Amuse.Editor.Semantics.LilToon",
                    "Alrauna.Amuse.Editor.Semantics.Poiyomi",
                },
                namespaces);
        }
    }
}
