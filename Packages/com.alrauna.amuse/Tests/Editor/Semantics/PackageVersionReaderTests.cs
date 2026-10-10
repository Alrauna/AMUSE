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
    }
}
