using System.Collections.Generic;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Pins the vendor environment contract. The evaluator tests pass
    /// hand-built installed and locked maps, so they never touch the
    /// project. The two RunAll tests inject entries through the
    /// EntriesOverride seam. Each one sets a no-op guard flag that
    /// proves RunAll read the seam instead of the real sources. Each
    /// one also silences the suite-start record line through the
    /// RecordLine seam, so an injected entry set never prints a
    /// second dated line into a full-run console.
    /// </summary>
    public sealed class VendorEnvironmentCheckTests
    {
        // --- Falsifier 7: a suite-start check that skips on a version mismatch reports green on an environment that is not the locked one. ---
        [Test]
        public void VersionMismatchAgainstLockedBlockFails()
        {
            var entry = EvaluateSingleEntry(
                SingleEntryMap("jp.lilxyzw.liltoon", "2.5.0"),
                SingleEntryMap("jp.lilxyzw.liltoon", "2.4.1"));

            Assert.That(entry.Package, Is.EqualTo("jp.lilxyzw.liltoon"));
            Assert.That(entry.Locked, Is.EqualTo("2.4.1"));
            Assert.That(entry.Installed, Is.EqualTo("2.5.0"));
            Assert.That(entry.State, Is.EqualTo(VendorVersionState.Mismatch));
            Assert.That(entry.Note, Is.Null);
        }

        [Test]
        public void ThryeditorMinorBumpFails()
        {
            var entry = EvaluateSingleEntry(
                SingleEntryMap("com.poiyomi.thryeditor", "2.75.0"),
                SingleEntryMap("com.poiyomi.thryeditor", "2.74.3"));

            Assert.That(entry.State, Is.EqualTo(VendorVersionState.Mismatch));
            Assert.That(entry.Note, Is.Null);
        }

        [Test]
        public void ThryeditorDowngradeInsideTheLineFails()
        {
            var entry = EvaluateSingleEntry(
                SingleEntryMap("com.poiyomi.thryeditor", "2.74.2"),
                SingleEntryMap("com.poiyomi.thryeditor", "2.74.3"));

            Assert.That(entry.State, Is.EqualTo(VendorVersionState.Mismatch));
            Assert.That(entry.Note, Is.Null);
        }

        // --- Falsifier 14: a suite-start check that fails or skips a thryeditor upgrade inside the 2.74.x line misstates the A3c state. ---
        [Test]
        public void ThryeditorPatchUpgradeInsideTheLineNeitherFailsNorSkips()
        {
            var entry = EvaluateSingleEntry(
                SingleEntryMap("com.poiyomi.thryeditor", "2.74.4"),
                SingleEntryMap("com.poiyomi.thryeditor", "2.74.3"));

            Assert.That(entry.State,
                Is.EqualTo(VendorVersionState.ThryPatchUpgrade));
        }

        [Test]
        public void DriftedPatchEntryCarriesTheNotYetValidatedNote()
        {
            var entry = EvaluateSingleEntry(
                SingleEntryMap("com.poiyomi.thryeditor", "2.74.4"),
                SingleEntryMap("com.poiyomi.thryeditor", "2.74.3"));

            Assert.That(entry.State,
                Is.EqualTo(VendorVersionState.ThryPatchUpgrade));
            Assert.That(entry.Note, Is.Not.Null,
                "the drifted patch state must carry the note");
            Assert.That(entry.Note,
                Does.Contain("No gated real-lock run has yet exercised"),
                "the note must state the not-yet-validated status");
            Assert.That(entry.Note,
                Does.Contain(
                    "counts as validated only after one dated gated real-lock pass"),
                "the note must name the validation condition");
        }

        [Test]
        public void AbsentOptionalVendorIsRecordedNotFailed()
        {
            var entry = EvaluateSingleEntry(
                SingleEntryMap("nadena.dev.ndmf", "1.14.8"),
                SingleEntryMap("com.vrcfury.vrcfury", "1.1434.0"));

            Assert.That(entry.Package, Is.EqualTo("com.vrcfury.vrcfury"));
            Assert.That(entry.Locked, Is.EqualTo("1.1434.0"));
            Assert.That(entry.Installed, Is.Null);
            Assert.That(entry.State, Is.EqualTo(VendorVersionState.Absent));
            Assert.That(entry.Note, Is.Null);
        }

        [Test]
        public void MatchingVersionsGiveMatchEntries()
        {
            var entries = VendorEnvironmentCheck.Evaluate(
                new Dictionary<string, string>
                {
                    { "com.poiyomi.thryeditor", "2.74.3" },
                    { "jp.lilxyzw.liltoon", "2.4.1" },
                },
                new Dictionary<string, string>
                {
                    { "com.poiyomi.thryeditor", "2.74.3" },
                    { "jp.lilxyzw.liltoon", "2.4.1" },
                });

            Assert.That(entries, Has.Count.EqualTo(2));
            foreach (var entry in entries)
            {
                Assert.That(entry.State, Is.EqualTo(VendorVersionState.Match),
                    entry.Package);
                Assert.That(entry.Note, Is.Null, entry.Package);
            }
        }

        // --- Falsifier 7 second witness: a RunAll that calls the skip path on a mismatch reports green on a drifted environment. ---
        [Test]
        public void RunAllFailsOnAnInjectedMismatchEntry()
        {
            var overrideWasRead = false;
            VendorSuiteStartCheck.EntriesOverride = () =>
            {
                overrideWasRead = true;
                return new[]
                {
                    new VendorVersionEntry(
                        "jp.lilxyzw.liltoon", "2.4.1", "2.5.0",
                        VendorVersionState.Mismatch, null),
                };
            };
            var savedRecordLine = VendorSuiteStartCheck.RecordLine;
            VendorSuiteStartCheck.RecordLine = _ => { };
            try
            {
                var thrown = Assert.Throws<AssertionException>(
                    VendorSuiteStartCheck.RunAll);

                Assert.That(thrown.Message, Does.Contain("jp.lilxyzw.liltoon"),
                    "the failure must name the package");
                Assert.That(thrown.Message, Does.Contain("2.4.1"),
                    "the failure must name the locked version");
                Assert.That(thrown.Message, Does.Contain("2.5.0"),
                    "the failure must name the installed version");
                Assert.That(overrideWasRead, Is.True,
                    "RunAll must read the EntriesOverride seam");
            }
            finally
            {
                VendorSuiteStartCheck.RecordLine = savedRecordLine;
                VendorSuiteStartCheck.EntriesOverride = null;
            }
        }

        [Test]
        public void RunAllRecordsAnInjectedDriftedPatchWithoutFailing()
        {
            var overrideWasRead = false;
            VendorSuiteStartCheck.EntriesOverride = () =>
            {
                overrideWasRead = true;
                return VendorEnvironmentCheck.Evaluate(
                    SingleEntryMap("com.poiyomi.thryeditor", "2.74.4"),
                    SingleEntryMap("com.poiyomi.thryeditor", "2.74.3"));
            };
            var savedRecordLine = VendorSuiteStartCheck.RecordLine;
            VendorSuiteStartCheck.RecordLine = _ => { };
            try
            {
                Assert.DoesNotThrow(VendorSuiteStartCheck.RunAll,
                    "a drifted patch inside the 2.74.x line is recorded,"
                    + " never failed");
                Assert.That(overrideWasRead, Is.True,
                    "RunAll must read the EntriesOverride seam");
            }
            finally
            {
                VendorSuiteStartCheck.RecordLine = savedRecordLine;
                VendorSuiteStartCheck.EntriesOverride = null;
            }
        }

        private static VendorVersionEntry EvaluateSingleEntry(
            Dictionary<string, string> installed,
            Dictionary<string, string> locked)
        {
            var entries = VendorEnvironmentCheck.Evaluate(installed, locked);

            Assert.That(entries, Has.Count.EqualTo(1),
                "the hand-built maps describe exactly one package");
            return entries[0];
        }

        private static Dictionary<string, string> SingleEntryMap(
            string package, string version)
        {
            return new Dictionary<string, string> { { package, version } };
        }
    }
}
