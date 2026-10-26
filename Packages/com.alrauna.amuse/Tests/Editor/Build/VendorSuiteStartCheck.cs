using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The suite-start check of the product test assembly. It runs once
    /// before the first test. It fails the suite start when a known NDMF
    /// plugin-disable key is set, when any locked vendor version
    /// mismatches its installed version, or when an installed VRChat
    /// avatar SDK lost its compile define. Absent packages and accepted
    /// Thry patch drifts are recorded in one dated console line, never
    /// failed.
    /// </summary>
    internal static class VendorSuiteStartCheck
    {
        internal const string VrcAvatarsPackage = "com.vrchat.avatars";
        internal const string VrcAvatarsDefine = "AMUSE_VRCSDK3_AVATARS";

        private const string ManifestRelativePath =
            "Packages/vpm-manifest.json";

        // The known NDMF plugin-disable keys. The default reads the
        // exact known plugin set of the session-state guard, and the
        // guard's suite-start assertion reads this seam, so an
        // injected list stays in effect.
        internal static Func<IEnumerable<string>> KnownDisableKeys =
            DefaultKnownDisableKeys;

        private const string SuiteStartTag = "[AMUSE vendor suite start]";

        // One entry pattern of the locked block. Each member of the
        // block holds its version first.
        private static readonly Regex LockedEntryPattern = new Regex(
            @"""(?<name>[^""]+)""\s*:\s*\{\s*""version""\s*:\s*""(?<version>[^""]*)""");

        // The seam the RunAll tests inject through. Null reads the real
        // version sources.
        internal static Func<IReadOnlyList<VendorVersionEntry>>
            EntriesOverride;

        internal static void RunAll()
        {
            VendorSessionStateGuard.AssertNoKnownPluginDisabled();
            var entries = CollectEntries();
            AssertNoMismatch(entries);
            AssertVrcAvatarsDefineMatchesTheInstalledPackages();
            RecordTheEffectiveEnvironment(entries);
        }

        private static IReadOnlyList<VendorVersionEntry> CollectEntries()
        {
            if (EntriesOverride != null)
            {
                return EntriesOverride();
            }
            return VendorEnvironmentCheck.Evaluate(
                ReadInstalledVersions(), ReadLockedVersions());
        }

        private static void AssertNoMismatch(
            IReadOnlyList<VendorVersionEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (entry.State == VendorVersionState.Mismatch)
                {
                    Assert.Fail(
                        "The installed vendor environment is not the locked"
                        + " one. " + entry.Package + " is locked at "
                        + entry.Locked + " and installed at "
                        + entry.Installed + ". Restore the locked version"
                        + " before the run.");
                }
            }
        }

        private static void AssertVrcAvatarsDefineMatchesTheInstalledPackages()
        {
#if AMUSE_VRCSDK3_AVATARS
            // The define is active. The asmdef version define resolved
            // the installed SDK, so the compile path is intact.
#else
            // The define is inactive here. The code must still compile
            // and must fail loudly when the SDK is installed, so a
            // broken version define cannot silently drop the SDK
            // compile path of this assembly.
            if (ReadInstalledVersions().ContainsKey(VrcAvatarsPackage))
            {
                Assert.Fail(
                    "The package " + VrcAvatarsPackage + " is installed,"
                    + " but the define " + VrcAvatarsDefine + " is not"
                    + " active. Fix the version define of the test"
                    + " assembly before the run.");
            }
#endif
        }

        private static void RecordTheEffectiveEnvironment(
            IReadOnlyList<VendorVersionEntry> entries)
        {
            var line = new StringBuilder();
            line.Append(SuiteStartTag)
                .Append(' ')
                .Append(DateTime.UtcNow.ToString(
                    "yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Append(" effective environment:");
            foreach (var entry in entries)
            {
                line.Append(" | ").Append(entry.Package)
                    .Append(" locked ").Append(entry.Locked)
                    .Append(", installed ")
                    .Append(entry.Installed ?? "absent")
                    .Append(", ").Append(entry.State);
            }
            line.Append(" | define ").Append(VrcAvatarsDefine);
#if AMUSE_VRCSDK3_AVATARS
            line.Append(" active");
#else
            line.Append(" inactive");
#endif
            Debug.Log(line.ToString());
        }

        private static IReadOnlyDictionary<string, string>
            ReadInstalledVersions()
        {
            var installed = new Dictionary<string, string>(
                StringComparer.Ordinal);
            foreach (var package in PackageManagerInfo.GetAllRegisteredPackages())
            {
                installed[package.name] = package.version;
            }
            return installed;
        }

        private static IReadOnlyDictionary<string, string>
            ReadLockedVersions()
        {
            var locked = new Dictionary<string, string>(
                StringComparer.Ordinal);
            var manifestPath = Path.Combine(
                Directory.GetParent(Application.dataPath)
                    .FullName.Replace('\\', '/'),
                ManifestRelativePath);
            if (File.Exists(manifestPath))
            {
                var block = LockedBlockOf(File.ReadAllText(manifestPath));
                foreach (Match match in LockedEntryPattern.Matches(block))
                {
                    locked[match.Groups["name"].Value] =
                        match.Groups["version"].Value;
                }
            }
            if (locked.Count == 0)
            {
                Assert.Fail(
                    "The suite-start check cannot read the locked block of "
                    + ManifestRelativePath
                    + ". It refuses to pass on a missing or unreadable"
                    + " lock.");
            }
            return locked;
        }

        private static string LockedBlockOf(string manifest)
        {
            var lockedAt = manifest.IndexOf(
                "\"locked\"", StringComparison.Ordinal);
            if (lockedAt < 0)
            {
                return string.Empty;
            }
            var openAt = manifest.IndexOf('{', lockedAt);
            if (openAt < 0)
            {
                return string.Empty;
            }
            var depth = 0;
            for (var i = openAt; i < manifest.Length; i++)
            {
                if (manifest[i] == '{')
                {
                    depth++;
                }
                else if (manifest[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return manifest.Substring(openAt, i - openAt + 1);
                    }
                }
            }
            return string.Empty;
        }

        private static IEnumerable<string> DefaultKnownDisableKeys()
        {
            return VendorSessionStateGuard.KnownDisableKeys();
        }
    }
}
