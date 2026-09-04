using System;
using nadena.dev.ndmf;
using UnityEditor;

namespace Alrauna.Amuse.Editor.Build
{
    internal enum AmuseBuildPath
    {
        NonPlayNdmfBuild,
        ApplyOnPlay,
        Unknown,
    }

    internal enum HostLifecycleRefusal
    {
        None,
        UnsupportedUnityVersion,
        UnsupportedNdmfVersion,
        UnsupportedVrchatSdkBaseVersion,
        UnsupportedVrchatSdkAvatarsVersion,
        UnsupportedPlatform,
        UnsupportedBuildPath,
        MissingBuildContextServices,
    }

    internal sealed class HostLifecycleFacts
    {
        internal string UnityVersion { get; }
        internal string NdmfVersion { get; }
        internal string VrchatSdkBaseVersion { get; }
        internal string VrchatSdkAvatarsVersion { get; }
        internal string PlatformQualifiedName { get; }
        internal AmuseBuildPath BuildPath { get; }
        internal bool HasAssetSaver { get; }
        internal bool HasAssetContainer { get; }
        internal bool HasObjectRegistry { get; }
        internal bool HasErrorReport { get; }

        internal HostLifecycleFacts(
            string unityVersion,
            string ndmfVersion,
            string vrchatSdkBaseVersion,
            string vrchatSdkAvatarsVersion,
            string platformQualifiedName,
            AmuseBuildPath buildPath,
            bool hasAssetSaver,
            bool hasAssetContainer,
            bool hasObjectRegistry,
            bool hasErrorReport)
        {
            UnityVersion = unityVersion;
            NdmfVersion = ndmfVersion;
            VrchatSdkBaseVersion = vrchatSdkBaseVersion;
            VrchatSdkAvatarsVersion = vrchatSdkAvatarsVersion;
            PlatformQualifiedName = platformQualifiedName;
            BuildPath = buildPath;
            HasAssetSaver = hasAssetSaver;
            HasAssetContainer = hasAssetContainer;
            HasObjectRegistry = hasObjectRegistry;
            HasErrorReport = hasErrorReport;
        }
    }

    internal sealed class HostLifecycleCapability
    {
        /// <summary>The admitted Unity stream major (design D1).</summary>
        private const int SupportedUnityMajor = 2022;

        /// <summary>The admitted Unity stream minor (design D1).</summary>
        private const int SupportedUnityMinor = 3;

        /// <summary>The lowest admitted Unity 2022.3 patch (design D1).</summary>
        private const int SupportedUnityPatchFloor = 22;

        /// <summary>The only admitted Unity release type: final releases.</summary>
        private const char UnityFReleaseType = 'f';

        /// <summary>NDMF admitted range floor 1.14.4 (design D2).</summary>
        private static readonly int[] NdmfFloor = { 1, 14, 4 };

        /// <summary>NDMF admitted range exclusive upper bound 2.0.0 (design D2).</summary>
        private static readonly int[] NdmfUpperBound = { 2, 0, 0 };

        /// <summary>VRChat SDK Base and Avatars admitted range floor 3.10.4 (design D3).</summary>
        private static readonly int[] VrchatSdkFloor = { 3, 10, 4 };

        /// <summary>VRChat SDK Base and Avatars exclusive upper bound 4.0.0 (design D3).</summary>
        private static readonly int[] VrchatSdkUpperBound = { 4, 0, 0 };
        private const string SupportedPlatform = "nadena.dev.ndmf.vrchat.avatar3";

        private HostLifecycleCapability(
            bool mayUsePositiveMutation,
            HostLifecycleRefusal refusal,
            string supportedAssumption)
        {
            MayUsePositiveMutation = mayUsePositiveMutation;
            Refusal = refusal;
            SupportedAssumption = supportedAssumption;
        }

        internal bool MayUsePositiveMutation { get; }
        internal HostLifecycleRefusal Refusal { get; }
        internal string SupportedAssumption { get; }

        internal static HostLifecycleCapability Evaluate(HostLifecycleFacts facts)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            if (!UnityVersionAdmitted(facts.UnityVersion))
            {
                return Refused(HostLifecycleRefusal.UnsupportedUnityVersion);
            }

            if (!PackageVersionAdmitted(facts.NdmfVersion, NdmfFloor, NdmfUpperBound))
            {
                return Refused(HostLifecycleRefusal.UnsupportedNdmfVersion);
            }

            if (!PackageVersionAdmitted(
                facts.VrchatSdkBaseVersion, VrchatSdkFloor, VrchatSdkUpperBound))
            {
                return Refused(HostLifecycleRefusal.UnsupportedVrchatSdkBaseVersion);
            }

            if (!PackageVersionAdmitted(
                facts.VrchatSdkAvatarsVersion, VrchatSdkFloor, VrchatSdkUpperBound))
            {
                return Refused(HostLifecycleRefusal.UnsupportedVrchatSdkAvatarsVersion);
            }

            if (!EqualsOrdinal(facts.PlatformQualifiedName, SupportedPlatform))
            {
                return Refused(HostLifecycleRefusal.UnsupportedPlatform);
            }

            if (facts.BuildPath != AmuseBuildPath.NonPlayNdmfBuild)
            {
                return Refused(HostLifecycleRefusal.UnsupportedBuildPath);
            }

            if (!facts.HasAssetSaver || !facts.HasAssetContainer ||
                !facts.HasObjectRegistry || !facts.HasErrorReport)
            {
                return Refused(HostLifecycleRefusal.MissingBuildContextServices);
            }

            return new HostLifecycleCapability(
                true,
                HostLifecycleRefusal.None,
                "Unity 2022.3.22f1 or newer 2022.3 f-release; NDMF 1.14.4 to any 1.x below 2.0.0, " +
                "no prerelease; VRChat SDK Base/Avatars 3.10.4 to any 3.x below 4.0.0, no prerelease; " +
                "NDMF platform nadena.dev.ndmf.vrchat.avatar3; non-Play NDMF build.");
        }

        internal static HostLifecycleCapability CaptureAndEvaluate(BuildContext context)
        {
            var path = EditorApplication.isPlayingOrWillChangePlaymode
                ? AmuseBuildPath.ApplyOnPlay
                : AmuseBuildPath.NonPlayNdmfBuild;
            var assetSaver = context.AssetSaver;

            return Evaluate(new HostLifecycleFacts(
                UnityEngine.Application.unityVersion,
                PackageVersion("nadena.dev.ndmf"),
                PackageVersion("com.vrchat.base"),
                PackageVersion("com.vrchat.avatars"),
                context.PlatformProvider?.QualifiedName,
                path,
                assetSaver != null,
                assetSaver?.CurrentContainer != null,
                context.ObjectRegistry != null,
                context.ErrorReport != null));
        }

        private static bool EqualsOrdinal(string actual, string expected)
        {
            return string.Equals(actual, expected, StringComparison.Ordinal);
        }

        private static string PackageVersion(string packageName)
        {
            try
            {
                foreach (var package in UnityEditor.PackageManager.PackageInfo
                             .GetAllRegisteredPackages())
                {
                    if (EqualsOrdinal(package.name, packageName))
                    {
                        return package.version;
                    }
                }

                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static HostLifecycleCapability Refused(HostLifecycleRefusal refusal)
        {
            return new HostLifecycleCapability(false, refusal, null);
        }

        /// <summary>
        /// Unity admission policy: admits the 2022.3 stream at or above patch
        /// 22 with release type f. Another stream, patch, or release type
        /// refuses, as does input the grammar cannot parse or a null input.
        /// The Windows StandaloneWindows64 default-format tables and the GPU
        /// blit and readback route were characterized on this stream; the
        /// per-domain latch re-measures the readback half on the live host.
        /// </summary>
        private static bool UnityVersionAdmitted(string version)
        {
            return TryParseUnityVersion(
                version, out var major, out var minor, out var patch, out var releaseType)
                && major == SupportedUnityMajor
                && minor == SupportedUnityMinor
                && patch >= SupportedUnityPatchFloor
                && releaseType == UnityFReleaseType;
        }

        /// <summary>
        /// Parses Unity's <c>M.m.p&lt;type&gt;&lt;n&gt;</c> grammar: three
        /// digit runs, one release-type letter from f, c, a, b, p, then a
        /// digit run. A missing type suffix, a type letter with no digits
        /// after it, a foreign character, and any component count other than
        /// three are unparseable.
        /// </summary>
        private static bool TryParseUnityVersion(
            string version,
            out int major,
            out int minor,
            out int patch,
            out char releaseType)
        {
            major = 0;
            minor = 0;
            patch = 0;
            releaseType = default;
            if (string.IsNullOrEmpty(version))
            {
                return false;
            }

            var pos = 0;
            if (!TryReadDigitRun(version, ref pos, out major)
                || !TryReadExpected(version, ref pos, '.')
                || !TryReadDigitRun(version, ref pos, out minor)
                || !TryReadExpected(version, ref pos, '.')
                || !TryReadDigitRun(version, ref pos, out patch))
            {
                return false;
            }

            if (pos == version.Length)
            {
                return false; // the release-type suffix is mandatory
            }

            releaseType = version[pos];
            if (!IsUnityReleaseType(releaseType))
            {
                return false;
            }

            pos++;
            return TryReadDigitRun(version, ref pos, out _)
                && pos == version.Length; // nothing may follow the revision digits
        }

        /// <summary>Consumes one expected separator character.</summary>
        private static bool TryReadExpected(string version, ref int pos, char expected)
        {
            if (pos >= version.Length || version[pos] != expected)
            {
                return false;
            }

            pos++;
            return true;
        }

        /// <summary>
        /// The Unity release-type letters. Every member parses; only f
        /// admits, so the other release types refuse as parsed versions
        /// rather than as unparseable input.
        /// </summary>
        private static bool IsUnityReleaseType(char candidate)
        {
            return candidate == 'f' || candidate == 'c' || candidate == 'a'
                || candidate == 'b' || candidate == 'p';
        }

        /// <summary>
        /// NDMF and VRChat SDK admission policy, one rule applied twice:
        /// admits when the parsed version sits in
        /// <c>[floor, exclusiveUpperBound)</c> and carries no prerelease
        /// suffix. Unparseable input, a prerelease suffix, and null refuse
        /// with the caller's named cause.
        /// </summary>
        private static bool PackageVersionAdmitted(
            string version, int[] floor, int[] exclusiveUpperBound)
        {
            return TryParsePackageVersion(
                       version, out var components, out var count, out var hasPrereleaseSuffix)
                && !hasPrereleaseSuffix
                && PackageVersionInRange(components, count, floor, exclusiveUpperBound);
        }

        /// <summary>
        /// Parses the package grammar <c>M.m.p</c> with an optional
        /// <c>-</c> prerelease suffix. Two or three dot-separated digit runs
        /// are valid; one component, four or more, an empty part, and any
        /// foreign character (including <c>+</c>) are unparseable. The
        /// <c>-</c> suffix is reported separately because it refuses as a
        /// prerelease rather than as unparseable input.
        /// </summary>
        private static bool TryParsePackageVersion(
            string version, out int[] components, out int count, out bool hasPrereleaseSuffix)
        {
            components = null;
            count = 0;
            hasPrereleaseSuffix = false;
            if (string.IsNullOrEmpty(version))
            {
                return false;
            }

            var parsed = new int[3];
            var pos = 0;
            while (true)
            {
                if (!TryReadDigitRun(version, ref pos, out var value) || count == parsed.Length)
                {
                    return false;
                }

                parsed[count++] = value;
                if (pos == version.Length)
                {
                    break;
                }

                if (version[pos] == '-')
                {
                    hasPrereleaseSuffix = true;
                    break;
                }

                if (version[pos] != '.')
                {
                    return false; // any other foreign character, including '+'
                }

                pos++;
            }

            if (count < 2)
            {
                return false;
            }

            components = parsed;
            return true;
        }

        /// <summary>
        /// The design's truncation rule: with n the input's component count,
        /// the floor and the bound compare truncated to their first n
        /// components, so absent trailing components are not compared.
        /// 1.14 admits against a 1.14.4 floor, while 1.14.0 refuses below it.
        /// </summary>
        private static bool PackageVersionInRange(
            int[] components, int count, int[] floor, int[] exclusiveUpperBound)
        {
            return CompareComponents(components, floor, count) >= 0
                && CompareComponents(components, exclusiveUpperBound, count) < 0;
        }

        /// <summary>
        /// Lexicographic integer comparison over the first
        /// <paramref name="count"/> components: the first differing component
        /// decides, and inputs that agree throughout compare equal. Numeric
        /// parts never compare as strings.
        /// </summary>
        private static int CompareComponents(int[] left, int[] right, int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (left[i] != right[i])
                {
                    return left[i] < right[i] ? -1 : 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// Reads one run of ASCII digits at <paramref name="pos"/> as an
        /// integer and advances past it. Returns false without moving when
        /// the run is empty. An absurdly long run saturates at
        /// <see cref="int.MaxValue"/> instead of wrapping, so it reads above
        /// every bound.
        /// </summary>
        private static bool TryReadDigitRun(string version, ref int pos, out int value)
        {
            value = 0;
            var digits = 0;
            while (pos < version.Length)
            {
                var digit = version[pos] - '0';
                if (digit < 0 || digit > 9)
                {
                    break;
                }

                value = value <= (int.MaxValue - digit) / 10 ? value * 10 + digit : int.MaxValue;
                pos++;
                digits++;
            }

            return digits > 0;
        }
    }
}
