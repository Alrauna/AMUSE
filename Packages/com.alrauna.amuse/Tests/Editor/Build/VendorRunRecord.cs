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
    /// The discovered count reads from the assembly node of the test
    /// tree the runner loaded at run start. A listener registered at
    /// domain load sees the run start event, so the block measures
    /// the catalog and not only the delivered results. The catalog is
    /// filter blind, so a filtered run completes fewer leaves by
    /// selection, and the block states that basis under the counts.
    /// The completed count reads from the leaf results the run
    /// delivered, counted once per leaf, because the callback stream
    /// double-delivered a whole run in this project before. A run
    /// that dies before the teardown prints no block at all. The
    /// zero research duration of a product-only run states that the
    /// research assembly was not part of that run.
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
            VendorOptInGate.ProductionOrderDispatch,
            VendorOptInGate.RealLacPass,
        };

        // The fixtures an unset gate excludes from a run. A later task
        // that adds a gated fixture extends this map.
        internal static readonly (string Variable, string Fixture)[]
            GatedFixtures =
        {
            (VendorOptInGate.DaoIntegration,
                "Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests"),
            (VendorOptInGate.RealThryLock,
                "Alrauna.Amuse.Tests.Editor.Build."
                + "RealThryLockCharacterizationTests"),
            (VendorOptInGate.ProductionOrderDispatch,
                "Alrauna.Amuse.Tests.Editor.Build."
                + "VendorProductionOrderTests"),
            (VendorOptInGate.RealLacPass,
                "Alrauna.Amuse.Tests.Editor.Build."
                + "RealLacPassGroundCheckTests"),
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

        // The basis line under the counts. The teardown sets it from
        // its discovered source before it builds the block. Null
        // prints no basis line.
        internal static string DiscoveredBasis;

        // The discovered figure of the captured run. The value reads
        // from the run start listener, which sees the loaded tree
        // before the first test of the run.
        private static int? discoveredAtStart;

        // The leaf count of this assembly node in the tree the runner
        // loaded for the last run start of this editor session.
        internal static int? DiscoveredInLoadedTree;

        // The api instance of the run start listener. It lives for
        // the whole editor session, unlike the tracker api of one
        // run.
        private static TestRunnerApi listenerApi;

        private static TestRunnerApi api;

        private static ResultTracker tracker;

        internal static void CaptureBeforeRun()
        {
            ProjectSettingsBefore = ReadProjectFile(ProjectSettingsFile);
            EditorSettingsBefore = ReadProjectFile(EditorSettingsFile);
            definesBefore = ReadScriptingDefines();
            entriesAtStart = VendorSuiteStartCheck.CollectEntries();
            startedAt = DateTimeOffset.UtcNow;
            discoveredAtStart = DiscoveredInLoadedTree;
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
                beforeCounts.TryGetValue(line, out var beforeCount);
                afterCounts.TryGetValue(line, out var afterCount);
                if (beforeCount > afterCount
                    && NameTotalOf(beforeCounts, line)
                        > NameTotalOf(afterCounts, line))
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
            if (!string.IsNullOrEmpty(DiscoveredBasis))
            {
                block.Append("discovered basis: ")
                    .AppendLine(DiscoveredBasis);
            }
            block.Append("durations: product ")
                .Append(SecondsOf(productDuration))
                .Append(" s, research ");
            if (researchDuration == TimeSpan.Zero)
            {
                block.Append("not part of this run");
            }
            else
            {
                block.Append(SecondsOf(researchDuration)).Append(" s");
            }
            block.Append(", sum ")
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
            var discovered = discoveredAtStart ?? tracker.CompletedLeaves;
            DiscoveredBasis = discoveredAtStart.HasValue
                ? "the leaf count of this assembly node in the test tree"
                + " the runner loaded for the run. The catalog is filter"
                + " blind, so a filtered run completes fewer leaves by"
                + " selection, not by loss."
                : "the leaf results this run delivered. No run-start"
                + " tree was observed for this run.";
            var report = BuildReport(
                discovered,
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

        // The total count of all lines that share the setting name of
        // the given line. A name that loses one duplicate entry keeps
        // its name total lower, which tells a duplicate removal from
        // a value change of a name that stays.
        private static int NameTotalOf(
            Dictionary<string, int> counts,
            string line)
        {
            var name = NameOf(line);
            var total = 0;
            foreach (var pair in counts)
            {
                if (string.Equals(NameOf(pair.Key), name,
                        StringComparison.Ordinal))
                {
                    total += pair.Value;
                }
            }
            return total;
        }

        private static string NameOf(string line)
        {
            var colon = line.IndexOf(':');
            return colon < 0 ? line : line.Substring(0, colon);
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

        // Registers the run start listener at domain load, before any
        // run starts, so the run start event with the loaded tree
        // reaches the record.
        [InitializeOnLoadMethod]
        private static void RegisterRunStartListener()
        {
            listenerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            listenerApi.RegisterCallbacks(new RunStartListener());
        }

        private sealed class RunStartListener : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                DiscoveredInLoadedTree =
                    CountLeavesOfThisAssembly(testsToRun);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }
        }

        // Finds the node of this assembly in the loaded tree and
        // returns its leaf count. The node name is the dll file name
        // of this assembly. The tree never leaves the editor, and the
        // record never prints a node path.
        private static int? CountLeavesOfThisAssembly(ITestAdaptor node)
        {
            var dllName = typeof(VendorRunRecord).Assembly.GetName().Name
                + ".dll";
            var pending = new Stack<ITestAdaptor>();
            pending.Push(node);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (current.IsTestAssembly
                    && string.Equals(current.Name, dllName,
                        StringComparison.Ordinal))
                {
                    return current.TestCaseCount;
                }
                foreach (var child in current.Children)
                {
                    pending.Push(child);
                }
            }
            return null;
        }

        /// <summary>
        /// Counts the leaf results of the run and collects the reason
        /// of every skipped leaf. Every leaf counts once. The tracker
        /// remembers the delivered leaf full names, because the
        /// callback stream double-delivered a whole run in this
        /// project before. The delegator logs a throwing callback
        /// instead of failing the run.
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

            private readonly HashSet<string> deliveredLeafNames =
                new HashSet<string>(StringComparer.Ordinal);

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
                // The stream can deliver one leaf twice. Count and
                // record only the first delivery of each leaf.
                if (!deliveredLeafNames.Add(result.Test.FullName))
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
