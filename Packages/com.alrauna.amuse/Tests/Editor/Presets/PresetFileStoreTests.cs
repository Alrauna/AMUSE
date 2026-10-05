using NUnit.Framework;
using Alrauna.Amuse.Editor.Presets;

namespace Alrauna.Amuse.Tests.Editor
{
    public sealed class PresetFileStoreTests
    {
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
    }
}
