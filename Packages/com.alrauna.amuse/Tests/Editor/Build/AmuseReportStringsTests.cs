using System;
using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;

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
        }
    }

    public sealed class AmuseBuildStatusStoreTests
    {
        [Test]
        public void RecordedStatusIsReadableByAvatarInstance()
        {
            var id = 90210;
            try
            {
                AmuseBuildStatusStore.Record(id, "analyzed 3 renderers");
                Assert.That(
                    AmuseBuildStatusStore.TryGet(id, out var summary),
                    Is.True);
                Assert.That(summary, Is.EqualTo("analyzed 3 renderers"));
            }
            finally
            {
                AmuseBuildStatusStore.Forget(id);
            }
        }

        [Test]
        public void UnknownAvatarHasNoStatus()
        {
            Assert.That(
                AmuseBuildStatusStore.TryGet(13371337, out _), Is.False);
        }
    }
}