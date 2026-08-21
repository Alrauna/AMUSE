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
    }
}
