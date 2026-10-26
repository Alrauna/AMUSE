using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Pins the vendor run record contract. The diff tests pass
    /// hand-built setting texts. The report tests pass hand-built
    /// counts, durations, reasons, and entries. Every test that
    /// touches a seam or an environment variable saves the prior
    /// value first and restores it in a finally block, so the real
    /// suite run keeps its own capture state.
    /// </summary>
    public sealed class VendorRunRecordTests
    {
        [Test]
        public void SettingsDiffReportsChangedLinesByName()
        {
            const string before =
                "m_Version: 1\nanisotropicTextures: 2\nm_Shadows: 1";
            const string after =
                "m_Version: 1\nanisotropicTextures: 1\nm_Shadows: 1";

            var changed = VendorRunRecord.DiffLines(before, after);

            Assert.That(changed,
                Is.EqualTo(new[] { "anisotropicTextures: 1" }),
                "one changed value line must give exactly that line");
            Assert.That(VendorRunRecord.DiffLines(before, before),
                Is.Empty,
                "equal texts must give no changed line");
        }

        [Test]
        public void SettingsDiffIgnoresLineOrder()
        {
            const string before = "m_Shadows: 1\nm_Version: 1";
            const string after = "m_Version: 1\nm_Shadows: 1";

            Assert.That(VendorRunRecord.DiffLines(before, after),
                Is.Empty,
                "a line order change is not a value change");
        }

        [Test]
        public void BuildReportCarriesTheStableHeaderCountsAndDurations()
        {
            var report = VendorRunRecord.BuildReport(
                discovered: 5,
                completed: 5,
                passed: 3,
                failed: 1,
                skipped: 1,
                inconclusive: 0,
                productDuration: TimeSpan.FromSeconds(10.5),
                researchDuration: TimeSpan.FromSeconds(2.5),
                skipReasons: new string[0],
                entries: new VendorVersionEntry[0]);

            var lines = report.Split('\n');
            Assert.That(lines[0].TrimEnd('\r'),
                Is.EqualTo("=== AMUSE vendor run record ==="),
                "the block must open with the stable header");
            Assert.That(report,
                Does.Contain(
                    "discovered 5, completed 5, passed 3, failed 1,"
                    + " skipped 1, inconclusive 0"),
                "the block must state every count");
            Assert.That(report, Does.Contain("product 10.5 s"),
                "the block must state the product duration");
            Assert.That(report, Does.Contain("research 2.5 s"),
                "the block must state the research duration");
            Assert.That(report, Does.Contain("sum 13.0 s"),
                "the block must state the computed sum of both durations");
        }

        [Test]
        public void BuildReportListsThePerTestSkipReasons()
        {
            const string reason =
                "Namespace.Test: the opt-in variable"
                + " AMUSE_DAO_INTEGRATION is not set to 1.";

            var report = VendorRunRecord.BuildReport(
                1, 1, 0, 0, 1, 0,
                TimeSpan.Zero, TimeSpan.Zero,
                new[] { reason },
                new VendorVersionEntry[0]);

            Assert.That(report, Does.Contain(reason),
                "the block must list each skip reason");
        }

        [Test]
        public void BuildReportNamesTheGatedFixtureAnUnsetGateExcluded()
        {
            var variable = VendorOptInGate.DaoIntegration;
            var saved = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, null);
            try
            {
                var report = VendorRunRecord.BuildReport(
                    1, 1, 1, 0, 0, 0,
                    TimeSpan.Zero, TimeSpan.Zero,
                    new string[0],
                    new VendorVersionEntry[0]);

                Assert.That(report, Does.Contain("opt-in gates set: none"),
                    "an unset gate must read as none in the gates line");
                Assert.That(report,
                    Does.Contain("DaoMergedConsumptionTests"),
                    "the excluded gated fixture must be named");
                Assert.That(report, Does.Contain(variable),
                    "the unset gate variable must be named");
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, saved);
            }
        }

        [Test]
        public void BuildReportListsAGateThatWasSetAndExcludesNothing()
        {
            var variable = VendorOptInGate.DaoIntegration;
            var saved = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, "1");
            try
            {
                var report = VendorRunRecord.BuildReport(
                    1, 1, 1, 0, 0, 0,
                    TimeSpan.Zero, TimeSpan.Zero,
                    new string[0],
                    new VendorVersionEntry[0]);

                Assert.That(report,
                    Does.Contain("opt-in gates set: AMUSE_DAO_INTEGRATION"),
                    "a set gate must be listed by its variable name");
                Assert.That(report,
                    Does.Contain("gated fixtures excluded: none"),
                    "a set gate excludes no mapped fixture");
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, saved);
            }
        }

        [Test]
        public void BuildReportCarriesTheNoteOfADriftedPatchEntry()
        {
            var entries = VendorEnvironmentCheck.Evaluate(
                new Dictionary<string, string>
                {
                    { "com.poiyomi.thryeditor", "2.74.4" },
                },
                new Dictionary<string, string>
                {
                    { "com.poiyomi.thryeditor", "2.74.3" },
                });

            var report = VendorRunRecord.BuildReport(
                1, 1, 1, 0, 0, 0,
                TimeSpan.Zero, TimeSpan.Zero,
                new string[0],
                entries);

            Assert.That(report, Does.Contain("ThryPatchUpgrade"),
                "the effective environment must name the drift state");
            Assert.That(report,
                Does.Contain("No gated real-lock run has yet exercised"),
                "the block must carry the not-yet-validated note");
        }

        [Test]
        public void BuildReportListsTheChangedSettingLinesFromTheCapture()
        {
            const string before =
                "anisotropicTextures: 2\nm_Shadows: 1";
            const string after =
                "anisotropicTextures: 1\nm_Shadows: 1";
            var savedReader = VendorRunRecord.ReadProjectFile;
            var savedProject = VendorRunRecord.ProjectSettingsBefore;
            var savedEditor = VendorRunRecord.EditorSettingsBefore;
            VendorRunRecord.ReadProjectFile = path =>
                path.EndsWith("ProjectSettings.asset", StringComparison.Ordinal)
                    ? after
                    : "m_SerializationMode: 2";
            try
            {
                VendorRunRecord.ProjectSettingsBefore = before;
                VendorRunRecord.EditorSettingsBefore =
                    "m_SerializationMode: 2";

                var report = VendorRunRecord.BuildReport(
                    1, 1, 1, 0, 0, 0,
                    TimeSpan.Zero, TimeSpan.Zero,
                    new string[0],
                    new VendorVersionEntry[0]);

                Assert.That(report,
                    Does.Contain(
                        "ProjectSettings.asset: anisotropicTextures: 1"),
                    "the block must list the changed setting line"
                    + " under its file name");
            }
            finally
            {
                VendorRunRecord.ReadProjectFile = savedReader;
                VendorRunRecord.ProjectSettingsBefore = savedProject;
                VendorRunRecord.EditorSettingsBefore = savedEditor;
            }
        }
    }
}
