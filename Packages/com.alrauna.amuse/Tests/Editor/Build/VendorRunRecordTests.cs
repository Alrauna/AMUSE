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
        public void SettingsDiffReportsADuplicateLineRemoval()
        {
            const string before =
                "anisotropicTextures: 2\nanisotropicTextures: 2\nm_Shadows: 1";
            const string after =
                "anisotropicTextures: 2\nm_Shadows: 1";

            var changed = VendorRunRecord.DiffLines(before, after);

            Assert.That(changed, Is.EqualTo(new[] { "anisotropicTextures: 2" }),
                "a removed duplicate of a name that stays must be"
                + " reported");
        }

        [Test]
        public void BuildReportMarksARunWithoutTheResearchAssembly()
        {
            var report = VendorRunRecord.BuildReport(
                1, 1, 1, 0, 0, 0,
                TimeSpan.FromSeconds(10.5),
                TimeSpan.Zero,
                new string[0],
                new VendorVersionEntry[0]);

            Assert.That(report, Does.Contain("research not part of this run"),
                "a zero research duration must state that the research"
                + " assembly did not run");
            Assert.That(report, Does.Contain("sum 10.5 s"),
                "the sum must still record the product duration");
        }

        [Test]
        public void BuildReportStatesTheBasisOfTheDiscoveredCount()
        {
            var savedBasis = VendorRunRecord.DiscoveredBasis;
            VendorRunRecord.DiscoveredBasis =
                "the leaf count of this assembly node in the test tree.";
            try
            {
                var report = VendorRunRecord.BuildReport(
                    5, 5, 5, 0, 0, 0,
                    TimeSpan.Zero, TimeSpan.Zero,
                    new string[0],
                    new VendorVersionEntry[0]);

                Assert.That(report,
                    Does.Contain(
                        "discovered basis: the leaf count of this"
                        + " assembly node in the test tree."),
                    "the block must state where the discovered figure"
                    + " comes from");
            }
            finally
            {
                VendorRunRecord.DiscoveredBasis = savedBasis;
            }
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
            var variables = new[]
            {
                VendorOptInGate.RealThryLock,
                VendorOptInGate.DaoIntegration,
                VendorOptInGate.ProductionOrderDispatch,
                VendorOptInGate.RealLacPass,
            };
            var saved = new string[variables.Length];
            for (var i = 0; i < variables.Length; i++)
            {
                saved[i] = Environment.GetEnvironmentVariable(
                    variables[i]);
                Environment.SetEnvironmentVariable(variables[i], null);
            }
            try
            {
                var report = VendorRunRecord.BuildReport(
                    1, 1, 1, 0, 3, 0,
                    TimeSpan.Zero, TimeSpan.Zero,
                    new string[0],
                    new VendorVersionEntry[0]);

                Assert.That(report, Does.Contain("opt-in gates set: none"),
                    "an unset gate must read as none in the gates line");
                Assert.That(report,
                    Does.Contain("DaoMergedConsumptionTests"),
                    "the excluded gated fixture must be named");
                Assert.That(report,
                    Does.Contain("RealThryLockCharacterizationTests"),
                    "every mapped gated fixture must be named");
                Assert.That(report,
                    Does.Contain("VendorProductionOrderTests"),
                    "every mapped gated fixture must be named");
                Assert.That(report,
                    Does.Contain("RealLacPassGroundCheckTests"),
                    "every mapped gated fixture must be named");
                foreach (var variable in variables)
                {
                    Assert.That(report, Does.Contain(variable),
                        "the unset gate variable must be named");
                }
            }
            finally
            {
                for (var i = 0; i < variables.Length; i++)
                {
                    Environment.SetEnvironmentVariable(
                        variables[i], saved[i]);
                }
            }
        }

        [Test]
        public void BuildReportListsASetProductionOrderGateAndSparesItsFixture()
        {
            var setVariable = VendorOptInGate.ProductionOrderDispatch;
            var otherVariables = new[]
            {
                VendorOptInGate.RealThryLock,
                VendorOptInGate.DaoIntegration,
            };
            var savedSet = Environment.GetEnvironmentVariable(setVariable);
            var savedOthers = new string[otherVariables.Length];
            Environment.SetEnvironmentVariable(setVariable, "1");
            for (var i = 0; i < otherVariables.Length; i++)
            {
                savedOthers[i] = Environment.GetEnvironmentVariable(
                    otherVariables[i]);
                Environment.SetEnvironmentVariable(otherVariables[i], null);
            }
            try
            {
                var report = VendorRunRecord.BuildReport(
                    1, 1, 1, 0, 3, 0,
                    TimeSpan.Zero, TimeSpan.Zero,
                    new string[0],
                    new VendorVersionEntry[0]);

                Assert.That(report,
                    Does.Contain(
                        "opt-in gates set: "
                        + VendorOptInGate.ProductionOrderDispatch),
                    "a set gate must be listed by its variable name");
                Assert.That(report,
                    Does.Contain("DaoMergedConsumptionTests"),
                    "an unset gate still names its mapped fixture");
                Assert.That(report,
                    Does.Contain(
                        "RealThryLockCharacterizationTests ("
                        + VendorOptInGate.RealThryLock + " unset)"),
                    "an unset gate still names its mapped fixture with"
                    + " its variable");
                Assert.That(report,
                    Does.Not.Contain(
                        "VendorProductionOrderTests ("
                        + setVariable + " unset)"),
                    "a set gate must spare its mapped fixture");
            }
            finally
            {
                for (var i = 0; i < otherVariables.Length; i++)
                {
                    Environment.SetEnvironmentVariable(
                        otherVariables[i], savedOthers[i]);
                }
                Environment.SetEnvironmentVariable(setVariable, savedSet);
            }
        }

        [Test]
        public void BuildReportListsAGateThatWasSetAndExcludesNothing()
        {
            var variables = new[]
            {
                VendorOptInGate.RealThryLock,
                VendorOptInGate.DaoIntegration,
                VendorOptInGate.ProductionOrderDispatch,
                VendorOptInGate.RealLacPass,
            };
            var saved = new string[variables.Length];
            for (var i = 0; i < variables.Length; i++)
            {
                saved[i] = Environment.GetEnvironmentVariable(
                    variables[i]);
                Environment.SetEnvironmentVariable(variables[i], "1");
            }
            try
            {
                var report = VendorRunRecord.BuildReport(
                    1, 1, 1, 0, 0, 0,
                    TimeSpan.Zero, TimeSpan.Zero,
                    new string[0],
                    new VendorVersionEntry[0]);

                foreach (var variable in variables)
                {
                    Assert.That(report, Does.Contain(variable),
                        "a set gate must be listed by its variable name");
                }
                Assert.That(report,
                    Does.Contain("gated fixtures excluded: none"),
                    "a set gate excludes no mapped fixture");
            }
            finally
            {
                for (var i = 0; i < variables.Length; i++)
                {
                    Environment.SetEnvironmentVariable(
                        variables[i], saved[i]);
                }
            }
        }

        [Test]
        public void BuildReportListsTheRealLacPassGateItselfWhenSet()
        {
            var otherVariables = new[]
            {
                VendorOptInGate.RealThryLock,
                VendorOptInGate.DaoIntegration,
                VendorOptInGate.ProductionOrderDispatch,
            };
            var savedOthers = new string[otherVariables.Length];
            var savedGate = Environment.GetEnvironmentVariable(
                VendorOptInGate.RealLacPass);
            Environment.SetEnvironmentVariable(
                VendorOptInGate.RealLacPass, "1");
            for (var i = 0; i < otherVariables.Length; i++)
            {
                savedOthers[i] = Environment.GetEnvironmentVariable(
                    otherVariables[i]);
                Environment.SetEnvironmentVariable(otherVariables[i], null);
            }
            try
            {
                var report = VendorRunRecord.BuildReport(
                    1, 1, 1, 0, 0, 0,
                    TimeSpan.Zero, TimeSpan.Zero,
                    new string[0],
                    new VendorVersionEntry[0]);

                Assert.That(report,
                    Does.Contain(
                        "opt-in gates set: "
                        + VendorOptInGate.RealLacPass),
                    "a set real LAC pass gate must be listed on the"
                    + " gates line by its variable name");
            }
            finally
            {
                for (var i = 0; i < otherVariables.Length; i++)
                {
                    Environment.SetEnvironmentVariable(
                        otherVariables[i], savedOthers[i]);
                }
                Environment.SetEnvironmentVariable(
                    VendorOptInGate.RealLacPass, savedGate);
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
