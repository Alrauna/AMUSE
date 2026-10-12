using System;
using System.Reflection;
using NUnit.Framework;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    public sealed class PackageVersionReaderTests
    {
        [Test]
        public void ListPackagesReceivesIncludeIndirectTrue()
        {
            bool? seenOffline = null;
            bool? seenIndirect = null;
            var original = PackageVersionReader.ListPackages;
            PackageVersionReader.ListPackages = (offline, includeIndirect) =>
            {
                seenOffline = offline;
                seenIndirect = includeIndirect;
                return null;
            };
            try
            {
                PackageVersionReader.ReadInstalledOrNull("any.package.name");
            }
            finally
            {
                PackageVersionReader.ListPackages = original;
            }

            Assert.That(seenOffline, Is.True);
            Assert.That(seenIndirect, Is.True);
        }

        [Test]
        public void NullRequestReadsAsFailedListing()
        {
            var original = PackageVersionReader.ListPackages;
            PackageVersionReader.ListPackages = (_, _) => null;
            try
            {
                Assert.That(
                    PackageVersionReader.ReadInstalledOrNull("any.package"),
                    Is.Null);
            }
            finally
            {
                PackageVersionReader.ListPackages = original;
            }
        }

        [Test]
        public void VersionFor_MatchesByNameAndReturnsItsVersion()
        {
            var packages = new[]
            {
                MakePackage("other.package", "1.0.0"),
                MakePackage("jp.lilxyzw.liltoon", "0.9.0"),
            };

            Assert.That(
                PackageVersionReader.VersionFor(packages, "jp.lilxyzw.liltoon"),
                Is.EqualTo("0.9.0"));
            Assert.That(
                PackageVersionReader.VersionFor(packages, "absent.package"),
                Is.Null);
        }

        private static UnityEditor.PackageManager.PackageInfo MakePackage(
            string name, string version)
        {
            // PackageInfo has an internal constructor, and its name and
            // version properties are read-only. The fields behind them
            // are private, so this helper builds real instances through
            // reflection instead of property writes.
            var type = typeof(UnityEditor.PackageManager.PackageInfo);
            var info = (UnityEditor.PackageManager.PackageInfo)Activator.CreateInstance(
                type, nonPublic: true);
            type.GetField("m_Name", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(info, name);
            type.GetField("m_Version", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(info, version);
            return info;
        }
    }
}
