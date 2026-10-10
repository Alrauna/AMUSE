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
            "  \"schemaVersion\": 1,\n" +
            "  \"name\": \"Safe\",\n" +
            "  \"description\": \"A valid preset body for the store test.\",\n" +
            "  \"features\": {\n" +
            "    \"alphaSeparator\": true\n" +
            "  },\n" +
            "  \"settings\": {\n" +
            "    \"preserveTransparencyMaxMipLevel\": 4,\n" +
            "    \"preserveTransparencyMinTextureSize\": 128,\n" +
            "    \"minimumOpaqueCoveragePercent\": 25,\n" +
            "    \"minimumOpaqueAlphaPercent\": 100,\n" +
            "    \"polygonAlphaUpperClampPercent\": 100,\n" +
            "    \"polygonMinimumOpaqueCoveragePercent\": 100,\n" +
            "    \"allowDepthTestChange\": true,\n" +
            "    \"ignoreOutOfRangeMaterialSlots\": true\n" +
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
        public void EveryShippedFileHoldsTheShippedDefaults()
        {
            Assert.That(PresetFileStore.TryLoadAll(
                out var presets, out var failedFile, out var refusal),
                Is.True, failedFile + ": " + refusal);

            foreach (var preset in presets)
            {
                Assert.That(preset.AlphaSeparatorEnabled, Is.True,
                    preset.Name);
                Assert.That(preset.PreserveTransparencyMaxMipLevel,
                    Is.EqualTo(4), preset.Name);
                Assert.That(preset.PreserveTransparencyMinTextureSize,
                    Is.EqualTo(128), preset.Name);
                Assert.That(preset.MinimumOpaqueCoveragePercent,
                    Is.EqualTo(25), preset.Name);
                Assert.That(preset.MinimumOpaqueAlphaPercent,
                    Is.EqualTo(100), preset.Name);
                Assert.That(preset.PolygonAlphaUpperClampPercent,
                    Is.EqualTo(100), preset.Name);
                Assert.That(preset.PolygonMinimumOpaqueCoveragePercent,
                    Is.EqualTo(100), preset.Name);
                Assert.That(preset.AllowDepthTestChange, Is.True,
                    preset.Name);
                Assert.That(preset.IgnoreOutOfRangeMaterialSlots,
                    Is.True, preset.Name);
            }
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
    }
}
