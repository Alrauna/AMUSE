using System;
using NUnit.Framework;
using UnityEngine;
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
        public void UnavailableCaptureHint_NamesTheUpstreamReplacement()
        {
            var hint = AmuseReportStrings.Get(
                "amuse.texture.UnavailableCapture:hint");

            Assert.That(hint, Does.Contain(
                "replaced the material texture with an in-memory copy"));
            Assert.That(hint, Does.Contain("Trace and Optimize"));
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