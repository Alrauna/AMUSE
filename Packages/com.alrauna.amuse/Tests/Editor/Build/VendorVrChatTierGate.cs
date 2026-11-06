using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The gate of the vendor VRChat tier, tier A of the platform
    /// split. Tier A runs the tier B fixture bodies through the real
    /// VRChat platform, so the vendor VRChat passes of installed
    /// plugins execute against them. That needs the VRChat SDK
    /// packages. Without either package the gate stops the test
    /// through a named skip that names the absent package and its
    /// locked version, so a host without the SDK reads a skip in the
    /// report instead of a failure. A version drift of an installed
    /// SDK package is a suite-start failure, never a skip, because the
    /// suite-start check compares every locked package with its
    /// installed version.
    /// </summary>
    internal static class VendorVrChatTierGate
    {
        /// <summary>The NUnit category every tier A test carries.</summary>
        internal const string TierACategory = "VendorVRChatPlatform";

        internal const string VrcAvatarsPackage = "com.vrchat.avatars";
        internal const string VrcBasePackage = "com.vrchat.base";

        /// <summary>The locked version of both VRChat SDK packages.</summary>
        internal const string LockedSdkVersion = "3.10.6";

        /// <summary>
        /// The installed package id source. The default reads the Unity
        /// package database. A test injects its own source to pin the
        /// skip message and the suite-start category rule without
        /// changing the project.
        /// </summary>
        internal static Func<IEnumerable<string>> InstalledPackageIds =
            DefaultInstalledPackageIds;

        private static IEnumerable<string> DefaultInstalledPackageIds()
        {
            foreach (var package in PackageInfo.GetAllRegisteredPackages())
            {
                yield return package.name;
            }
        }

        /// <summary>
        /// Stops the calling test through a named skip unless both VRChat
        /// SDK packages are installed. The message names the first
        /// absent package and the locked version, following the loud
        /// ignore pattern of
        /// <see cref="FixtureAvatarIdentity.RequireVrcDescriptorSupport"/>.
        /// </summary>
        internal static void RequireInstalledVrcSdkPackages()
        {
            var installed = new HashSet<string>(
                InstalledPackageIds(), StringComparer.Ordinal);

            if (!installed.Contains(VrcAvatarsPackage))
            {
                throw new IgnoreException(
                    "The package " + VrcAvatarsPackage + " is not"
                    + " installed in this project. The tier A vendor"
                    + " VRChat tests build through the real VRChat"
                    + " platform, so they cannot run here. The locked"
                    + " version is " + LockedSdkVersion + ". Install "
                    + VrcAvatarsPackage + " " + LockedSdkVersion
                    + " to run them.");
            }

            if (!installed.Contains(VrcBasePackage))
            {
                throw new IgnoreException(
                    "The package " + VrcBasePackage + " is not"
                    + " installed in this project. The tier A vendor"
                    + " VRChat tests build through the real VRChat"
                    + " platform, so they cannot run here. The locked"
                    + " version is " + LockedSdkVersion + ". Install "
                    + VrcBasePackage + " " + LockedSdkVersion
                    + " to run them.");
            }
        }
    }
}
