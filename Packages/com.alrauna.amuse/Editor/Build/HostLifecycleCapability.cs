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
        private const string SupportedUnityVersion = "2022.3.22f1";
        private const string SupportedNdmfVersion = "1.14.4";
        private const string SupportedVrchatSdkBaseVersion = "3.10.4";
        private const string SupportedVrchatSdkAvatarsVersion = "3.10.4";
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

            if (!EqualsOrdinal(facts.UnityVersion, SupportedUnityVersion))
            {
                return Refused(HostLifecycleRefusal.UnsupportedUnityVersion);
            }

            if (!EqualsOrdinal(facts.NdmfVersion, SupportedNdmfVersion))
            {
                return Refused(HostLifecycleRefusal.UnsupportedNdmfVersion);
            }

            if (!EqualsOrdinal(facts.VrchatSdkBaseVersion, SupportedVrchatSdkBaseVersion))
            {
                return Refused(HostLifecycleRefusal.UnsupportedVrchatSdkBaseVersion);
            }

            if (!EqualsOrdinal(facts.VrchatSdkAvatarsVersion, SupportedVrchatSdkAvatarsVersion))
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
                "Unity 2022.3.22f1; NDMF 1.14.4; VRChat SDK Base/Avatars 3.10.4; " +
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
    }
}
