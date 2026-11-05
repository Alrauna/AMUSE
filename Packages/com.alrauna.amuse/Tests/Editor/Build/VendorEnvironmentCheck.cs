using System;
using System.Collections.Generic;
using System.Linq;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The state of one locked vendor package against its installed
    /// version. Match and Absent describe an environment the suite can
    /// run in. Mismatch names an environment the suite must refuse.
    /// ThryPatchUpgrade records the one drift the owner accepts for
    /// now, owner decision A3c of 2026-10-25.
    /// </summary>
    internal enum VendorVersionState
    {
        Match,
        Mismatch,
        Absent,
        ThryPatchUpgrade
    }

    /// <summary>
    /// One locked package, its installed version, and the state of the
    /// pair. The note is null for every state except ThryPatchUpgrade,
    /// which carries the not-yet-validated note text.
    /// </summary>
    internal readonly struct VendorVersionEntry
    {
        internal readonly string Package;
        internal readonly string Locked;
        internal readonly string Installed;
        internal readonly VendorVersionState State;
        internal readonly string Note;

        internal VendorVersionEntry(
            string package,
            string locked,
            string installed,
            VendorVersionState state,
            string note)
        {
            Package = package;
            Locked = locked;
            Installed = installed;
            State = state;
            Note = note;
        }
    }

    /// <summary>
    /// Compares installed vendor packages with the locked block of the
    /// VCC manifest. The evaluator is pure string data. The caller
    /// reads both version sources and passes them in, so the rules are
    /// testable against hand-built maps. The locked side never reads
    /// the top-level dependencies block of the manifest.
    /// </summary>
    internal static class VendorEnvironmentCheck
    {
        internal const string ThryEditorPackage =
            "com.poiyomi.thryeditor";

        // The recorded note of a drifted Thry patch. The suite treats
        // the drifted version as unvalidated until a dated gated
        // real-lock pass against it exists in the run records.
        internal const string DriftedPatchNote =
            "No gated real-lock run has yet exercised the drifted"
            + " installed version. The version counts as validated only"
            + " after one dated gated real-lock pass against it. Owner"
            + " decision A3c of 2026-10-25 treats a newer patch inside"
            + " the 2.74.x line as free of breaking changes for now.";

        // The one accepted drift line. Owner decision A3c of 2026-10-25
        // covers 2.74.x patches only. No other line and no downgrade
        // follows it.
        private const int ThryLineMajor = 2;
        private const int ThryLineMinor = 74;

        internal static IReadOnlyList<VendorVersionEntry> Evaluate(
            IReadOnlyDictionary<string, string> installed,
            IReadOnlyDictionary<string, string> locked)
        {
            var entries = new List<VendorVersionEntry>();
            foreach (var package in locked.Keys.OrderBy(
                name => name, StringComparer.Ordinal))
            {
                var lockedVersion = locked[package];
                installed.TryGetValue(package, out var installedVersion);
                var state = StateFor(package, lockedVersion, installedVersion);
                entries.Add(new VendorVersionEntry(
                    package,
                    lockedVersion,
                    installedVersion,
                    state,
                    state == VendorVersionState.ThryPatchUpgrade
                        ? DriftedPatchNote
                        : null));
            }
            return entries;
        }

        private static VendorVersionState StateFor(
            string package,
            string lockedVersion,
            string installedVersion)
        {
            if (installedVersion == null)
            {
                return VendorVersionState.Absent;
            }
            if (string.Equals(
                    installedVersion, lockedVersion, StringComparison.Ordinal))
            {
                return VendorVersionState.Match;
            }
            if (IsThryPatchUpgrade(package, lockedVersion, installedVersion))
            {
                return VendorVersionState.ThryPatchUpgrade;
            }
            return VendorVersionState.Mismatch;
        }

        private static bool IsThryPatchUpgrade(
            string package,
            string lockedVersion,
            string installedVersion)
        {
            if (package != ThryEditorPackage)
            {
                return false;
            }
            if (!TryReadThreePartVersion(lockedVersion, out var lockedNumber)
                || !TryReadThreePartVersion(
                    installedVersion, out var installedNumber))
            {
                return false;
            }
            return lockedNumber.Major == ThryLineMajor
                && installedNumber.Major == ThryLineMajor
                && lockedNumber.Minor == ThryLineMinor
                && installedNumber.Minor == ThryLineMinor
                && installedNumber.Build > lockedNumber.Build;
        }

        private static bool TryReadThreePartVersion(
            string text, out Version version)
        {
            return Version.TryParse(text, out version)
                && version.Build >= 0;
        }
    }
}
