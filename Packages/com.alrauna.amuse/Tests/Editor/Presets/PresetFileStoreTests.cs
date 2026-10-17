using System.IO;
using NUnit.Framework;
using UnityEditor;
using Alrauna.Amuse.Editor.Presets;

namespace Alrauna.Amuse.Tests.Editor
{
    public sealed class PresetFileStoreTests
    {
        private const string TempFolder = "Assets/AmuseTests_PresetFileStore";

        private const string ValidBody =
            "{\n" +
            "  \"schemaVersion\": 4,\n" +
            "  \"name\": \"Safe\",\n" +
            "  \"description\": \"A valid preset body for the store test.\",\n" +
            "  \"features\": {\n" +
            "    \"alphaSeparator\": true\n" +
            "  },\n" +
            "  \"settings\": {\n" +
            "    \"preserveTransparencyMaxMipLevel\": 4,\n" +
            "    \"preserveTransparencyMinTextureSize\": 128,\n" +
            "    \"minimumOpaqueCoveragePercent\": 25,\n" +
            "    \"minimumOpaqueCoverageToPreservePercent\": 0,\n" +
            "    \"minimumOpaqueAlphaPercent\": 100,\n" +
            "    \"polygonAlphaUpperClampPercent\": 100,\n" +
            "    \"polygonMinimumOpaqueCoveragePercent\": 100,\n" +
            "    \"cutoutPreserveTransparencyMaxMipLevel\": 4,\n" +
            "    \"cutoutPreserveTransparencyMinTextureSize\": 128,\n" +
            "    \"cutoutMinimumOpaqueCoveragePercent\": 25,\n" +
            "    \"cutoutMinimumOpaqueCoverageToPreservePercent\": 0,\n" +
            "    \"allowDepthTestChange\": true,\n" +
            "    \"ignoreOutOfRangeMaterialSlots\": true,\n" +
            "    \"acceptDesktopAnisotropyDifferences\": false,\n" +
            "    \"acceptMobileAnisotropyDifferences\": false\n" +
            "  }\n" +
            "}";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", "AmuseTests_PresetFileStore");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        [Test]
        public void TheThreeShippedFilesParseInTheFixedOrder()
        {
            Assert.That(PresetFileStore.TryLoadAll(
                out var presets, out var failedFile, out var refusal),
                Is.True, failedFile + ": " + refusal);
            Assert.That(refusal, Is.EqualTo(PresetLoadRefusal.None));
            Assert.That(presets.Count, Is.EqualTo(3));
            Assert.That(presets[0].Name, Is.EqualTo("Safe"));
            Assert.That(presets[1].Name, Is.EqualTo("Normal"));
            Assert.That(presets[2].Name, Is.EqualTo("Aggressive"));
        }

        [Test]
        public void FailedLoadReturnsNoPartialPresets()
        {
            // Two files: one valid, one with malformed JSON. The
            // broken file stops the load. The store must answer no
            // presets at all, because a partial preset universe could
            // claim a match it cannot prove.
            File.WriteAllText(TempFolder + "/safe.json", ValidBody);
            File.WriteAllText(TempFolder + "/normal.json", "{ not json");
            AssetDatabase.ImportAsset(TempFolder + "/safe.json");
            AssetDatabase.ImportAsset(TempFolder + "/normal.json");

            var loaded = PresetFileStore.TryLoadAllFor(
                TempFolder, out var presets, out var failedFile,
                out var refusal);
            Assert.That(loaded, Is.False);
            Assert.That(presets, Is.Empty);
            Assert.That(failedFile, Is.EqualTo("normal.json"));
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.MalformedJson));
        }

        [Test]
        public void MissingShippedFile_RefusesWithFileMissing()
        {
            // Only safe.json exists. The fixed file walk reaches
            // normal.json, finds no imported text asset and no disk
            // file, and refuses by name.
            File.WriteAllText(TempFolder + "/safe.json", ValidBody);
            AssetDatabase.ImportAsset(TempFolder + "/safe.json");

            var result = PresetFileStore.TryLoadAllFor(
                TempFolder, out var presets, out var failedFile,
                out var refusal);

            Assert.That(result, Is.False);
            Assert.That(presets, Is.Empty);
            Assert.That(failedFile, Is.EqualTo("normal.json"));
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.FileMissing));
        }

        [Test]
        public void NullFolder_RefusesWithFileMissing()
        {
            var result = PresetFileStore.TryLoadAllFor(
                null, out var presets, out var failedFile,
                out var refusal);

            Assert.That(result, Is.False);
            Assert.That(presets, Is.Empty);
            Assert.That(failedFile, Is.EqualTo("Presets"));
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.FileMissing));
        }

        [Test]
        public void RootedFolderOutsideAssets_FallsBackToDirectDiskRead()
        {
            // A folder outside Assets can hold no imported text
            // asset, so the store must take its direct-disk fallback
            // branch for every file.
            var folder = Path.Combine(
                Path.GetTempPath(), "AmuseTests_PresetFileStoreOutside");
            WriteOutsidePreset(folder, "safe.json", "Safe");
            WriteOutsidePreset(folder, "normal.json", "Normal");
            WriteOutsidePreset(folder, "aggressive.json", "Aggressive");
            try
            {
                var result = PresetFileStore.TryLoadAllFor(
                    folder, out var presets, out var failedFile,
                    out var refusal);

                Assert.That(result, Is.True, failedFile + ": " + refusal);
                Assert.That(presets, Has.Count.EqualTo(3));
                Assert.That(presets[0].Name, Is.EqualTo("Safe"));
                Assert.That(presets[1].Name, Is.EqualTo("Normal"));
                Assert.That(presets[2].Name, Is.EqualTo("Aggressive"));
                Assert.That(refusal, Is.EqualTo(PresetLoadRefusal.None));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static void WriteOutsidePreset(
            string folder, string fileName, string presetName)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(
                Path.Combine(folder, fileName),
                ValidBody.Replace("\"name\": \"Safe\"", "\"name\": \""
                    + presetName + "\""));
        }
    }
}
