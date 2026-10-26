using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The source-scan tripwire over the product Editor source. Production
    /// never calls the vendor lock tool. The scan matches the vendor
    /// qualified type name only, never a name substring, because AMUSE
    /// property names and vendor operands contain the same word. The
    /// test-local stand-in type under Tests stays out of scope until the
    /// task that deletes it runs.
    /// </summary>
    public sealed class VendorLockToolSourceBanTests
    {
        private const string VendorLockToolTypeName =
            "Thry.ThryEditor.ShaderOptimizer";

        private static string ProductionRoot()
        {
            return Path.GetFullPath(
                "Packages/com.alrauna.amuse/Editor");
        }

        [Test]
        public void ProductionSourceNeverCallsTheVendorLockTool()
        {
            var root = ProductionRoot();
            Assert.That(
                Directory.Exists(root), Is.True,
                "product Editor source was not found for the scan");

            var files = Directory.GetFiles(
                root, "*.cs", SearchOption.AllDirectories);

            // A scan over no files would pass vacuously. It must fail.
            Assert.That(
                files.Length, Is.GreaterThan(0),
                "no source files were scanned. The scan proved nothing");

            var offenders = new List<string>();
            foreach (var file in files)
            {
                if (File.ReadAllText(file).Contains(
                        VendorLockToolTypeName))
                {
                    offenders.Add(Path.GetFileName(file));
                }
            }

            Assert.That(
                offenders, Is.Empty,
                "production source names the vendor lock tool type: " +
                string.Join(", ", offenders));
        }
    }
}
