using System;
using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Every refusal cause a report can name must have a plain English
    /// title and description in the string table. A cause without strings
    /// would surface as raw enum jargon to the user, so the completeness
    /// check enumerates the full vocabulary.
    /// </summary>
    public sealed class AmuseReportStringsTests
    {
        [Test]
        public void EveryRendererRefusalCauseHasPlainEnglishStrings()
        {
            foreach (RendererAnalysisRefusal cause in
                Enum.GetValues(typeof(RendererAnalysisRefusal)))
            {
                if (cause == RendererAnalysisRefusal.None)
                {
                    continue;
                }

                var key = AmuseReportStrings.RendererKey(cause);
                Assert.That(
                    AmuseReportStrings.Has(key), Is.True,
                    "missing title for " + cause);
                Assert.That(
                    AmuseReportStrings.Has(key + ":description"), Is.True,
                    "missing description for " + cause);
                Assert.That(
                    AmuseReportStrings.Has(key + ":hint"), Is.True,
                    "missing hint for " + cause);
            }
        }

        [Test]
        public void EveryAlphaUnknownKindHasPlainEnglishSentence()
        {
            var reasons = new[]
            {
                AlphaUnknownReason.UnsupportedShader("lilToon"),
                AlphaUnknownReason.UnattestedShader("lilToon"),
                AlphaUnknownReason.UnsupportedFeature(
                    "Dissolve", "_DissolveParams"),
                AlphaUnknownReason.UnsupportedMultiState(
                    "the clipping canceller is enabled"),
                AlphaUnknownReason.FeatureRetention("Distance fade", "_DistanceFade"),
            };

            foreach (var reason in reasons)
            {
                Assert.That(
                    AmuseReports.FeatureSentence(reason),
                    Is.Not.Empty,
                    "missing sentence for " + reason.Kind);
            }

            // The Multi cause sentence comes from the string table, so a
            // table regression surfaces here and not only in a report.
            Assert.That(
                AmuseReportStrings.Has(
                    "amuse.slotAnalysis.UnsupportedMultiState"),
                Is.True,
                "missing the Multi cause sentence");
        }

        [Test]
        public void MappingRefusalDescriptionFormatsBothSlotCounts()
        {
            var description = string.Format(
                AmuseReportStrings.Get(
                    "amuse.renderer.UnprovenMaterialSlotMapping:description"),
                "3 material slots",
                "1 material slot");

            // Falsifier: an implementation that rewords the sentences
            // without placeholders renders the old text, states no
            // count, and leaves the reader unable to tell which side
            // is wrong.
            Assert.That(
                description, Does.Contain("mesh supports 3 material slots"));
            Assert.That(
                description, Does.Contain("renderer has 1 material slot"));
        }

        [Test]
        public void ForcedOpaqueEntryExistsWithThreeParts()
        {
            var key = "amuse.slotSeparation.ForcedOpaque";
            Assert.That(
                AmuseReportStrings.Has(key), Is.True,
                "missing title for the forced-opaque entry");
            Assert.That(
                AmuseReportStrings.Has(key + ":description"), Is.True,
                "missing description for the forced-opaque entry");
            Assert.That(
                AmuseReportStrings.Has(key + ":hint"), Is.True,
                "missing hint for the forced-opaque entry");

            var description = AmuseReportStrings.Get(key + ":description");
            Assert.That(
                description, Does.Contain("no opacity proof"),
                "the description must state the missing opacity proof");
            Assert.That(
                description,
                Does.Contain("Minimum Opaque Coverage to Preserve"),
                "the description must name the exact setting");
        }

        [Test]
        public void FoldedPremultiplyEntriesNameTheSettingNotExactColors()
        {
            var key = "amuse.slotSeparation.PremultiplyNormalizationFolded";
            Assert.That(
                AmuseReportStrings.Has(key), Is.True,
                "missing title for the folded premultiply entry");
            Assert.That(
                AmuseReportStrings.Has(key + ":description"), Is.True,
                "missing description for the folded premultiply entry");
            Assert.That(
                AmuseReportStrings.Has(key + ":hint"), Is.True,
                "missing hint for the folded premultiply entry");

            var description = AmuseReportStrings.Get(key + ":description");
            Assert.That(
                description,
                Does.Contain("Minimum Opaque Coverage to Preserve"),
                "the folded entry must name the preserve setting");
            Assert.That(
                description,
                Does.Not.Contain(
                    "The opaque material reproduces those colors exactly."),
                "a folded slot moved unproven triangles, so the entry " +
                "must not claim exact colors");
            Assert.That(
                description, Does.Contain("may differ"),
                "the folded entry must state the honest color premise");
        }

        [Test]
        public void DisclosureDescriptionsDescribeTheMovedSetAsOpaqueTriangles()
        {
            var descriptions = new[]
            {
                AmuseReportStrings.Get(
                    "amuse.slotSeparation.DepthTestDivergence:description"),
                AmuseReportStrings.Get(
                    "amuse.slotSeparation.PremultiplyNormalization" +
                    ":description"),
            };

            foreach (var description in descriptions)
            {
                Assert.That(
                    description,
                    Does.Contain(
                        "moved the opaque triangles of material slot"),
                    "the disclosure must describe the moved set as opaque " +
                    "triangles: " + description);
                Assert.That(
                    description, Does.Not.Contain("proven-opaque"),
                    "a disclosure of a moved set must not claim a proof " +
                    "the fold does not carry: " + description);
            }
        }

        [Test]
        public void RefusalEntryKeepsTheProvenOpaquePremise()
        {
            // The refusal moves nothing, so its premise stays genuine.
            var description = AmuseReportStrings.Get(
                "amuse.slotSeparation.OpaqueCoverageBelowMinimum" +
                ":description");
            Assert.That(
                description, Does.Contain("proven-opaque share"),
                "a refused slot moves nothing, so the entry keeps the " +
                "proven premise");
        }

        [Test]
        public void EveryHostRefusalCauseHasPlainEnglishStrings()
        {
            foreach (HostLifecycleRefusal cause in
                Enum.GetValues(typeof(HostLifecycleRefusal)))
            {
                if (cause == HostLifecycleRefusal.None)
                {
                    continue;
                }

                var key = AmuseReportStrings.HostKey(cause);
                Assert.That(
                    AmuseReportStrings.Has(key), Is.True,
                    "missing title for " + cause);
                Assert.That(
                    AmuseReportStrings.Has(key + ":description"), Is.True,
                    "missing description for " + cause);
            }
        }

        [Test]
        public void EveryAvatarAnimationRefusalCauseHasPlainEnglishStrings()
        {
            foreach (AvatarAnimationRefusal cause in
                Enum.GetValues(typeof(AvatarAnimationRefusal)))
            {
                if (cause == AvatarAnimationRefusal.None)
                {
                    continue;
                }

                var key = AmuseReportStrings.AvatarKey(cause);
                Assert.That(
                    AmuseReportStrings.Has(key), Is.True,
                    "missing title for " + cause);
                Assert.That(
                    AmuseReportStrings.Has(key + ":description"), Is.True,
                    "missing description for " + cause);
                Assert.That(
                    AmuseReportStrings.Has(key + ":hint"), Is.True,
                    "missing hint for " + cause);
            }
        }

        [Test]
        public void EveryTextureCaptureRefusalReasonHasPlainEnglishStrings()
        {
            foreach (TextureCaptureRefusalReason reason in Enum.GetValues(
                         typeof(TextureCaptureRefusalReason)))
            {
                if (reason == TextureCaptureRefusalReason.None)
                {
                    continue;
                }

                var key = AmuseReportStrings.TextureCaptureKey(reason);
                Assert.That(
                    AmuseReportStrings.Has(key), Is.True,
                    "missing title for " + reason);
                Assert.That(
                    AmuseReportStrings.Has(key + ":description"), Is.True,
                    "missing description for " + reason);
                Assert.That(
                    AmuseReportStrings.Has(key + ":hint"), Is.True,
                    "missing hint for " + reason);
            }
        }

        [Test]
        public void EverySlotResolutionFailureHasPlainEnglishStrings()
        {
            foreach (AlphaResolutionFailure failure in Enum.GetValues(
                         typeof(AlphaResolutionFailure)))
            {
                // SemanticsUnknown promotes to a slot refusal at
                // ResolveSlot and never reaches this key family.
                if (failure == AlphaResolutionFailure.None ||
                    failure == AlphaResolutionFailure.SemanticsUnknown)
                {
                    continue;
                }

                var key = AmuseReportStrings.SlotResolutionKey(failure);
                Assert.That(
                    AmuseReportStrings.Has(key), Is.True,
                    "missing title for " + failure);
                Assert.That(
                    AmuseReportStrings.Has(key + ":description"), Is.True,
                    "missing description for " + failure);
                Assert.That(
                    AmuseReportStrings.Has(key + ":hint"), Is.True,
                    "missing hint for " + failure);
                Assert.That(
                    AmuseReports.ResolutionFailureSentence(failure),
                    Is.Not.Empty,
                    "missing failure sentence for " + failure);
            }
        }

        [Test]
        public void SlotResolutionBuildersThrowOnNoneAndSemanticsUnknown()
        {
            // None never names a fact. SemanticsUnknown promotes to a
            // slot refusal at ResolveSlot and never reaches this path.
            Assert.Throws<InvalidOperationException>(
                () => AmuseReportStrings.SlotResolutionKey(
                    AlphaResolutionFailure.None));
            Assert.Throws<InvalidOperationException>(
                () => AmuseReportStrings.SlotResolutionKey(
                    AlphaResolutionFailure.SemanticsUnknown));
            Assert.Throws<InvalidOperationException>(
                () => AmuseReports.ResolutionFailureSentence(
                    AlphaResolutionFailure.None));
            Assert.Throws<InvalidOperationException>(
                () => AmuseReports.ResolutionFailureSentence(
                    AlphaResolutionFailure.SemanticsUnknown));
        }

        [Test]
        public void ConsentAndSummaryStringsExist()
        {
            Assert.That(
                AmuseReportStrings.Has("amuse.consent.Declined"), Is.True);
            Assert.That(
                AmuseReportStrings.Has("amuse.consent.Declined:description"),
                Is.True);
            Assert.That(
                AmuseReportStrings.Has("amuse.consent.Subject"), Is.True);
            Assert.That(
                AmuseReportStrings.Has("amuse.summary.Title"), Is.True);
            Assert.That(
                AmuseReportStrings.Has("amuse.summary.Title:description"),
                Is.True);
            Assert.That(
                AmuseReportStrings.Has("amuse.slotAnalysis.CaptureRefused"),
                Is.True);
        }

        [Test]
        public void RefusalNamingFixKeysExist()
        {
            // The per-cause loops cover the keys the key builders
            // produce. These literal checks pin the exact keys the
            // report naming fixes changed, so a table edit or a key
            // builder change cannot drop one silently.
            var keys = new[]
            {
                "amuse.slotSeparation.OpaqueConversionRefused:description",
                "amuse.renderer.LockedPoiyomiOriginalShaderUnattested",
                "amuse.renderer.LockedPoiyomiOriginalShaderUnattested:description",
                "amuse.renderer.MaterialDependencyClosureFailed",
                "amuse.renderer.MaterialDependencyClosureFailed:description",
                "amuse.texture.UnsupportedFormat:description",
                "amuse.texture.UnsupportedFormat:hint",
            };
            foreach (var key in keys)
            {
                Assert.That(AmuseReportStrings.Has(key), Is.True, key);
            }
        }

        // An unhandled closure failure value throws. This matches
        // the FeatureSentence discipline: no silent empty sentence.
        [Test]
        public void UnhandledClosureFailureThrows()
        {
            Assert.Throws<InvalidOperationException>(
                () => AmuseReports.ClosureFailureSentence(
                    (MaterialDependencyClosureFailure)999));
        }

        [Test]
        public void UnavailableCaptureHint_NamesTheUpstreamReplacement()
        {
            var hint = AmuseReportStrings.Get(
                "amuse.texture.UnavailableCapture:hint");

            Assert.That(hint, Does.Contain(
                "replaced the material texture with an in-memory copy"));
            Assert.That(hint, Does.Contain("Trace and Optimize"));
            Assert.That(hint, Does.Contain("LAC Texture Compressor"),
                "the hint names both known producers of in-memory copies");
        }

        [Test]
        public void UnavailableCaptureHint_NamesTheCharacterizedAtlasRule()
        {
            var hint = AmuseReportStrings.Get(
                "amuse.texture.UnavailableCapture:hint");

            Assert.That(hint, Does.Contain(
                "characterized version of Avatar Optimizer"),
                "the hint separates admitted atlases from the refusals " +
                "that remain");
            Assert.That(hint, Does.Contain("generated route"));
        }

        [Test]
        public void UnavailableCaptureHint_DropsTheFalseImportAdvice()
        {
            var hint = AmuseReportStrings.Get(
                "amuse.texture.UnavailableCapture:hint");

            // Falsifier: the old advice sent readers hunting for an
            // import problem that does not exist for a texture an
            // upstream build step created.
            Assert.That(hint, Does.Not.Contain(
                "Check that the texture is a real imported asset"));
        }
    }

    public sealed class AmuseBuildStatusStoreTests
    {
        [Test]
        public void RecordedStatusIsReadableWithoutAKey()
        {
            try
            {
                AmuseBuildStatusStore.Record("analyzed 3 renderers");
                Assert.That(
                    AmuseBuildStatusStore.TryGet(out var summary),
                    Is.True);
                Assert.That(summary, Is.EqualTo("analyzed 3 renderers"));
            }
            finally
            {
                AmuseBuildStatusStore.Forget();
            }
        }

        [Test]
        public void EmptySlotHasNoStatus()
        {
            AmuseBuildStatusStore.Forget();
            Assert.That(
                AmuseBuildStatusStore.TryGet(out _), Is.False);
        }
    }
}