using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Pins the one-modal rule (H2). The product package opens exactly
    /// one modal, the consent dialog. Banned literals are closed; the
    /// walker is duplicated from the research source-ban test on
    /// purpose, because no shared home exists without a product-to-
    /// research test dependency or production test machinery.
    /// </summary>
    public sealed class ModalUiBanTests
    {
        private static readonly string[] BannedLiterals =
        {
            "EditorUtility.DisplayDialog(",
            "EditorUtility.DisplayDialogComplex(",
            "EditorUtility.DisplayCancelableProgressBar(",
        };

        private static readonly string[] ExemptFiles =
        {
            "Build/VersionConsentDialog.cs",
        };

        [Test]
        public void NoProductEditorSourceOpensAModalOutsideTheConsentDialog()
        {
            var root = PackageInfo.FindForAssembly(typeof(ModalUiBanTests).Assembly)
                .assetPath + "/Editor";
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .ToList();

            Assert.That(files.Count, Is.GreaterThan(50),
                "the scan root must hold the real Editor tree, so the ban " +
                "cannot pass vacuously");

            var violations = new List<string>();
            foreach (var file in files)
            {
                var relative = file.Substring(root.Length + 1)
                    .Replace('\\', '/');
                if (ExemptFiles.Contains(relative))
                {
                    continue;
                }

                foreach (var literal in BannedLiterals)
                {
                    if (File.ReadAllText(file).Contains(literal))
                    {
                        violations.Add(Path.GetFileName(file) + " -> " + literal);
                    }
                }
            }

            Assert.That(violations, Is.Empty,
                "modal UI is sanctioned only in VersionConsentDialog.cs");
        }
    }
}
