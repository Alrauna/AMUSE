using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The source-scan tripwire over the product Editor source. Production
    /// never calls the vendor lock tool. The scan matches the vendor
    /// qualified type name only, never a name substring, because AMUSE
    /// property names and vendor operands contain the same word. The
    /// scan covers the production Editor source, so test fixtures stay
    /// out of scope by construction. Since the 2026-10-26 stand-in
    /// cutover no stand-in type exists, and this tripwire is the whole
    /// production protection.
    /// </summary>
    public sealed class VendorLockToolSourceBanTests
    {
        private const string VendorLockToolTypeName =
            "Thry.ThryEditor.ShaderOptimizer";

        private static string ProductionRoot()
        {
            var package = PackageInfo.FindForAssembly(
                typeof(VendorLockToolSourceBanTests).Assembly);
            Assert.That(
                package,
                Is.Not.Null,
                "could not resolve the AMUSE package from the test"
                + " assembly, so the product Editor source could not"
                + " be scanned");
            return package.assetPath + "/Editor";
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
