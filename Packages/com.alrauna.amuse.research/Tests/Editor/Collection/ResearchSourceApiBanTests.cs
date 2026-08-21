using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Alrauna.Amuse.Research.Tests.Editor.Collection
{
    /// <summary>
    /// Layer 2 of mutation safety, as a test rather than a promise: the research
    /// package's production source may not name a mutating or importing API.
    /// <para>
    /// Tests/ is deliberately out of scope. A calibration case has to set a
    /// property block to construct the very refusal it measures, and a test has
    /// to destroy the objects it created.
    /// </para>
    /// </summary>
    public sealed class ResearchSourceApiBanTests
    {
        /// <summary>
        /// Read-only AssetDatabase lookups are permitted; everything else on
        /// AssetDatabase is not. The blanket ban the harness design first wrote
        /// was broader than the mutation-safety concern that motivated it, and
        /// tier 1 loses its entire purpose without asset identity.
        /// </summary>
        private static readonly string[] AllowedAssetDatabaseMembers =
        {
            "AssetDatabase.GetAssetPath",
            "AssetDatabase.AssetPathToGUID",
        };

        private static readonly string[] BannedLiterals =
        {
            "AssetImporter",
            "TextureImporter",
            "ModelImporter",
            "EditorUtility.SetDirty",
            "Undo.",
            "PrefabUtility.",
            "EditorSceneManager.Save",
            "SetPropertyBlock",
            ".isReadable =",
            "Texture2D.Apply",
            "Object.Destroy",
        };

        /// <summary>
        /// The instantiating property reads, matched on a word boundary. As a
        /// bare substring ".material" also matches ".materialSlotIndex", and a
        /// scan that cries wolf gets weakened or deleted; the boundary makes it
        /// match the accident and not the field. It correspondingly does not
        /// match ".sharedMaterials" or ".sharedMesh", which is exactly the
        /// distinction this layer exists to draw.
        /// </summary>
        private static readonly string[] BannedPatterns =
        {
            @"\.material\b",
            @"\.materials\b",
            @"\.mesh\b",
        };

        private static string ProductionRoot()
        {
            return Path.GetFullPath(
                "Packages/com.alrauna.amuse.research/Editor");
        }

        [Test]
        public void ProductionSourceNamesNoMutatingApi()
        {
            var root = ProductionRoot();
            Assert.That(
                Directory.Exists(root), Is.True,
                "Research package Editor source not found at " + root);

            var files =
                Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);

            // A mis-globbed path must fail rather than pass vacuously.
            Assert.That(
                files.Length, Is.GreaterThan(0),
                "No source files scanned; the scan proved nothing.");

            var offences = new List<string>();
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                var name = Path.GetFileName(file);

                foreach (var banned in BannedLiterals)
                {
                    if (text.Contains(banned))
                    {
                        offences.Add(name + ": " + banned);
                    }
                }

                foreach (var pattern in BannedPatterns)
                {
                    if (Regex.IsMatch(text, pattern))
                    {
                        offences.Add(name + ": " + pattern);
                    }
                }

                foreach (Match match in Regex.Matches(
                             text, @"AssetDatabase\.\w+"))
                {
                    var allowed = false;
                    foreach (var permitted in AllowedAssetDatabaseMembers)
                    {
                        if (match.Value == permitted)
                        {
                            allowed = true;
                        }
                    }

                    if (!allowed)
                    {
                        offences.Add(name + ": " + match.Value);
                    }
                }
            }

            CollectionAssert.IsEmpty(offences);
        }

        [Test]
        public void ProductionSourceHoldsNoCalibrationOrSeamType()
        {
            // Review change 2, asserted rather than promised. The semantics seam
            // is one internal pass-through parameter on
            // RendererObservationBuilder.Build and nothing else; a type that
            // exists only to be called by a test does not belong in production.
            var root = ProductionRoot();

            var offences = new List<string>();
            var carriers = new List<string>();
            foreach (var file in Directory.GetFiles(
                         root, "*.cs", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.IndexOf(
                        "Calibration",
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    offences.Add(name);
                }

                if (File.ReadAllText(file)
                    .Contains("BaseMaterialSemanticsProvider"))
                {
                    carriers.Add(Path.GetFileName(file));
                }
            }

            CollectionAssert.IsEmpty(offences);
            CollectionAssert.AreEqual(
                new[] { "RendererObservationBuilder.cs" }, carriers);
        }
    }
}
