using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The run record of the product test assembly. The suite fixture
    /// captures the project settings digest before the first test and
    /// prints one report block after the last test. The block records
    /// the counts, the durations, the opt-in gates that were set, the
    /// gated fixtures the run excluded, the per-test skip reasons, the
    /// effective vendor environment, the observable scripting defines,
    /// and the changed setting lines. The record observes vendor
    /// writes, such as the VRCFury load-hook settings writes, and
    /// never prevents them.
    /// <para>
    /// The tracker counts leaf results through the test framework
    /// callback stream. Registration starts at the first test, so the
    /// discovered count and the completed count both read from the
    /// leaf results the run delivered. A run that dies before the
    /// teardown prints no block at all. The zero research duration of
    /// a product-only run states that the research assembly was not
    /// part of that run.
    /// </para>
    /// </summary>
    internal static class VendorRunRecord
    {
        internal const string Header = "=== AMUSE vendor run record ===";

        private const string ProjectSettingsFile =
            "ProjectSettings/ProjectSettings.asset";

        private const string EditorSettingsFile =
            "ProjectSettings/EditorSettings.asset";

        // The opt-in gate variables of the suite. The report names the
        // ones the environment set to 1.
        private static readonly string[] GateVariables =
        {
            VendorOptInGate.RealThryLock,
            VendorOptInGate.DaoIntegration,
        };

        // The fixtures an unset gate excludes from a run. A later task
        // that adds a gated fixture extends this map.
        internal static readonly (string Variable, string Fixture)[]
            GatedFixtures =
        {
            (VendorOptInGate.DaoIntegration,
                "Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests"),
        };

        // The read seams of the capture. Tests inject hand-built texts
        // and restore the default in a finally block.
        internal static Func<string, string> ReadProjectFile =
            DefaultReadProjectFile;

        private static Func<string> ReadScriptingDefines =
            DefaultReadScriptingDefines;

        // The capture state of the current run. ReportAfterRun reads
        // it. A domain reload that loses the state also loses the
        // tracker, so the report then states the missing capture.
        internal static string ProjectSettingsBefore;

        internal static string EditorSettingsBefore;

        private static string definesBefore;

        private static IReadOnlyList<VendorVersionEntry> entriesAtStart;

        private static DateTimeOffset? startedAt;

        private static TestRunnerApi api;

        private static ResultTracker tracker;

        internal static void CaptureBeforeRun()
        {
            ProjectSettingsBefore = ReadProjectFile(ProjectSettingsFile);
            EditorSettingsBefore = ReadProjectFile(EditorSettingsFile);
            definesBefore = ReadScriptingDefines();
            entriesAtStart = VendorSuiteStartCheck.CollectEntries();
            startedAt = DateTimeOffset.UtcNow;
            StartTracking();
        }

        internal static IReadOnlyList<string> DiffLines(
            string before,
            string after)
        {
            var beforeCounts = CountsOf(LinesOf(before));
            var afterCounts = CountsOf(LinesOf(after));
            var changed = new List<string>();
            foreach (var line in DistinctLinesOf(after))
            {
                beforeCounts.TryGetValue(line, out var beforeCount);
                if (afterCounts[line] > beforeCount)
                {
                    changed.Add(line);
                }
            }
            foreach (var line in DistinctLinesOf(before))
            {
                if (!HasName(afterCounts, line))
                {
                    changed.Add(line);
                }
            }
            return changed;
        }

        internal static string BuildReport(
            int discovered,
            int completed,
            int passed,
            int failed,
            int skipped,
            int inconclusive,
            TimeSpan productDuration,
            TimeSpan researchDuration,
            IReadOnlyList<string> skipReasons,
            IReadOnlyList<VendorVersionEntry> entries)
        {
            var block = new StringBuilder();
            block.AppendLine(Header);
            block.Append("date: ").AppendLine(DateTime.UtcNow.ToString(
                "yyyy-MM-dd", CultureInfo.InvariantCulture));
            block.Append("counts: discovered ").Append(discovered)
                .Append(", completed ").Append(completed)
                .Append(", passed ").Append(passed)
                .Append(", failed ").Append(failed)
                .Append(", skipped ").Append(skipped)
                .Append(", inconclusive ").Append(inconclusive)
                .AppendLine();
            block.Append("durations: product ")
                .Append(SecondsOf(productDuration))
                .Append(" s, research ").Append(SecondsOf(researchDuration))
                .Append(" s, sum ")
                .Append(SecondsOf(productDuration + researchDuration))
                .AppendLine(" s");
            AppendGates(block);
            AppendSkipReasons(block, skipReasons);
            AppendEnvironment(block, entries);
            AppendDefines(block);
            AppendChangedSettings(block);
            return block.ToString();
        }

        /// <summary>
        /// Prints the report block of the captured run. The suite
        /// teardown calls this after the last test.
        /// </summary>
        internal static void ReportAfterRun()
        {
            if (tracker == null || ProjectSettingsBefore == null
                || startedAt == null)
            {
                Debug.Log("[AMUSE vendor run record] "
                    + DateTime.UtcNow.ToString(
                        "yyyy-MM-dd", CultureInfo.InvariantCulture)
                    + " the before-run capture is missing. This run"
                    + " prints no report block.");
                StopTracking();
                return;
            }
            var productDuration = DateTimeOffset.UtcNow - startedAt.Value;
            var report = BuildReport(
                tracker.CompletedLeaves,
                tracker.CompletedLeaves,
                tracker.Passed,
                tracker.Failed,
                tracker.Skipped,
                tracker.Inconclusive,
                productDuration,
                TimeSpan.Zero,
                tracker.SkipReasons,
                entriesAtStart);
            StopTracking();
            Debug.Log(report);
        }

        private static void AppendGates(StringBuilder block)
        {
            var set = new List<string>();
            foreach (var variable in GateVariables)
            {
                if (Environment.GetEnvironmentVariable(variable) == "1")
                {
                    set.Add(variable);
                }
            }
            block.Append("opt-in gates set: ").AppendLine(
                set.Count == 0 ? "none" : string.Join(", ", set));
            var excluded = new List<string>();
            foreach (var gated in GatedFixtures)
            {
                if (Environment.GetEnvironmentVariable(gated.Variable)
                    != "1")
                {
                    excluded.Add(
                        gated.Fixture + " (" + gated.Variable + " unset)");
                }
            }
            block.Append("gated fixtures excluded: ").AppendLine(
                excluded.Count == 0 ? "none" : string.Join(", ", excluded));
        }

        private static void AppendSkipReasons(
            StringBuilder block,
            IReadOnlyList<string> skipReasons)
        {
            block.AppendLine("skip reasons:");
            if (skipReasons == null || skipReasons.Count == 0)
            {
                block.AppendLine("  none");
                return;
            }
            foreach (var reason in skipReasons)
            {
                block.Append("  - ").AppendLine(reason);
            }
        }

        private static void AppendEnvironment(
            StringBuilder block,
            IReadOnlyList<VendorVersionEntry> entries)
        {
            block.AppendLine("effective environment:");
            if (entries == null || entries.Count == 0)
            {
                block.AppendLine("  no entries");
                return;
            }
            foreach (var entry in entries)
            {
                block.Append("  - ").Append(entry.Package)
                    .Append(" locked ").Append(entry.Locked)
                    .Append(", installed ")
                    .Append(entry.Installed ?? "absent")
                    .Append(", ").Append(entry.State).AppendLine();
                if (entry.Note != null)
                {
                    block.Append("    note: ").AppendLine(entry.Note);
                }
            }
        }

        private static void AppendDefines(StringBuilder block)
        {
            var current = ReadScriptingDefines() ?? "unavailable";
            var before = definesBefore;
            if (before == null || before == current)
            {
                block.Append("scripting defines: ").AppendLine(current);
                return;
            }
            block.Append("scripting defines: ").Append(before)
                .Append(" -> ").AppendLine(current);
        }

        private static void AppendChangedSettings(StringBuilder block)
        {
            block.AppendLine("changed setting lines:");
            var lines = new List<string>();
            CollectFileChanges(
                lines, "ProjectSettings.asset", ProjectSettingsBefore,
                ReadProjectFile(ProjectSettingsFile));
            CollectFileChanges(
                lines, "EditorSettings.asset", EditorSettingsBefore,
                ReadProjectFile(EditorSettingsFile));
            if (lines.Count == 0)
            {
                block.AppendLine("  none");
                return;
            }
            foreach (var line in lines)
            {
                block.Append("  ").AppendLine(line);
            }
        }

        private static void CollectFileChanges(
            List<string> lines,
            string file,
            string before,
            string after)
        {
            if (before == null)
            {
                lines.Add(file + ": no before-run capture");
                return;
            }
            foreach (var line in DiffLines(before, after))
            {
                lines.Add("- " + file + ": " + line);
            }
        }

        private static string SecondsOf(TimeSpan duration)
        {
            return duration.TotalSeconds.ToString(
                "0.0", CultureInfo.InvariantCulture);
        }

        private static List<string> LinesOf(string text)
        {
            var lines = new List<string>();
            if (text == null)
            {
                return lines;
            }
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim('\r', ' ', '\t');
                if (line.Length > 0)
                {
                    lines.Add(line);
                }
            }
            return lines;
        }

        private static List<string> DistinctLinesOf(string text)
        {
            var distinct = new List<string>();
            foreach (var line in LinesOf(text))
            {
                if (!distinct.Contains(line))
                {
                    distinct.Add(line);
                }
            }
            return distinct;
        }

        private static Dictionary<string, int> CountsOf(
            List<string> lines)
        {
            var counts = new Dictionary<string, int>(
                StringComparer.Ordinal);
            foreach (var line in lines)
            {
                counts.TryGetValue(line, out var count);
                counts[line] = count + 1;
            }
            return counts;
        }

        private static bool HasName(
            Dictionary<string, int> counts,
            string line)
        {
            var colon = line.IndexOf(':');
            var name = colon < 0 ? line : line.Substring(0, colon);
            foreach (var other in counts.Keys)
            {
                var otherColon = other.IndexOf(':');
                var otherName = otherColon < 0
                    ? other
                    : other.Substring(0, otherColon);
                if (string.Equals(name, otherName,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static string DefaultReadProjectFile(string relativePath)
        {
            var root = Directory.GetParent(Application.dataPath)
                .FullName.Replace('\\', '/');
            var path = Path.Combine(root, relativePath);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        private static string DefaultReadScriptingDefines()
        {
            return PlayerSettings.GetScriptingDefineSymbols(
                NamedBuildTarget.Standalone);
        }

        private static void StartTracking()
        {
            StopTracking();
            tracker = new ResultTracker();
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(tracker);
        }

        private static void StopTracking()
        {
            if (tracker != null && api != null)
            {
                api.UnregisterCallbacks(tracker);
            }
            if (api != null)
            {
                UnityEngine.Object.DestroyImmediate(api);
                api = null;
            }
            tracker = null;
        }

        /// <summary>
        /// Counts the leaf results of the run and collects the reason
        /// of every skipped leaf. The test framework delivers the
        /// events through the shared callback stream, and the delegator
        /// logs a throwing callback instead of failing the run.
        /// </summary>
        private sealed class ResultTracker : ICallbacks
        {
            internal int CompletedLeaves;

            internal int Passed;

            internal int Failed;

            internal int Skipped;

            internal int Inconclusive;

            internal readonly List<string> SkipReasons =
                new List<string>();

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite)
                {
                    return;
                }
                CompletedLeaves++;
                switch (result.TestStatus)
                {
                    case TestStatus.Passed:
                        Passed++;
                        break;
                    case TestStatus.Failed:
                        Failed++;
                        break;
                    case TestStatus.Skipped:
                        Skipped++;
                        SkipReasons.Add(result.Test.FullName + ": "
                            + Flatten(result.Message));
                        break;
                    case TestStatus.Inconclusive:
                        Inconclusive++;
                        break;
                }
            }
        }

        private static string Flatten(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return "no reason recorded";
            }
            return message.Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
