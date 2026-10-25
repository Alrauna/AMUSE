using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// A standing guard for the named-skip rule: a test that needs a
    /// missing vendor package or an opt-in variable must stop through
    /// Assert.Ignore or through VendorOptInGate.Require. An assumption
    /// instead reports an inconclusive result and hides the gate from
    /// the report, while a named skip names the missing dependency.
    /// <para>
    /// The scan reads every source file with the cs extension under
    /// the Tests folder of both test assemblies. This file is the
    /// only exclusion, because the falsifier comment below names the
    /// banned call. Any other hit fails with its file and line.
    /// </para>
    /// </summary>
    public sealed class EnvironmentGateAuditTests
    {
        private const string BannedCall = "Assume.";

        private const string OwnFileName =
            "EnvironmentGateAuditTests.cs";

        private const string ResearchPackageName =
            "com.alrauna.amuse.research";

        private static readonly string[] ConvertedGateFiles =
        {
            "DaoMergedConsumptionTests.cs",
            "AaoMergedConsumptionTests.cs",
            "LilToonOpaqueTargetTests.cs",
            "VendorOptInGate.cs",
            "ResearchSourceApiBanTests.cs",
        };

        // --- Falsifier 13: an environment gate that still uses Assume.That reports inconclusive instead of a named skip. ---
        [Test]
        public void EnvironmentGatingNeverUsesAssumeThat()
        {
            var package = PackageInfo.FindForAssembly(
                typeof(EnvironmentGateAuditTests).Assembly);
            Assert.That(
                package,
                Is.Not.Null,
                "could not resolve the AMUSE package from the test"
                + " assembly, so the test sources could not be scanned");

            var research = PackageInfo.GetAllRegisteredPackages()
                .FirstOrDefault(p => p.name == ResearchPackageName);
            Assert.That(
                research,
                Is.Not.Null,
                "could not resolve the research package, so its test"
                + " sources could not be scanned");

            var scannedNames = new HashSet<string>();
            var offenders = new List<string>();
            var roots = new[]
            {
                package.assetPath + "/Tests",
                research.assetPath + "/Tests",
            };
            foreach (var root in roots)
            {
                Assert.That(
                    Directory.Exists(root),
                    Is.True,
                    "expected test sources at " + root
                    + ", so this scan proved nothing");
                foreach (var file in Directory.GetFiles(
                             root, "*.cs", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileName(file);
                    scannedNames.Add(name);
                    if (name == OwnFileName)
                    {
                        continue;
                    }

                    offenders.AddRange(ReportBannedUses(
                        Normalize(file), File.ReadAllText(file)));
                }
            }

            // The no-op guard: the detector fires on a synthetic file,
            // and the scan covered the files that carried the five
            // converted gates. A vacuous scan fails here.
            Assert.That(
                ReportBannedUses(
                    "synthetic.cs",
                    "Assume.That(target, Is.Not.Null);").ToList(),
                Is.EqualTo(new[] { "synthetic.cs:1" }),
                "the detector must report the banned call it reads");
            Assert.That(
                scannedNames,
                Is.SupersetOf(ConvertedGateFiles),
                "the scan must have covered both test assemblies");

            offenders.Sort(StringComparer.Ordinal);
            Assert.That(
                offenders,
                Is.Empty,
                "an environment gate must stop its test through a named"
                + " skip. These sites still use the banned call: "
                + string.Join(", ", offenders));
        }

        private static IEnumerable<string> ReportBannedUses(
            string fileName, string text)
        {
            var lines = text.Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                if (lines[index].Contains(BannedCall))
                {
                    yield return fileName + ":" + (index + 1);
                }
            }
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
