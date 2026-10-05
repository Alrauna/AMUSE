using NUnit.Framework;
using Alrauna.Amuse.Editor.Presets;

namespace Alrauna.Amuse.Tests.Editor
{
    public sealed class PresetParserTests
    {
        private const string ValidJson = @"{
  ""schemaVersion"": 1,
  ""name"": ""Safe"",
  ""description"": ""Shipped defaults."",
  ""features"": { ""alphaSeparator"": true },
  ""settings"": {
    ""preserveTransparencyMaxMipLevel"": 4,
    ""preserveTransparencyMinTextureSize"": 128,
    ""minimumOpaqueCoveragePercent"": 25,
    ""minimumOpaqueAlphaPercent"": 100,
    ""polygonAlphaUpperClampPercent"": 100,
    ""polygonMinimumOpaqueCoveragePercent"": 100,
    ""allowDepthTestChange"": true,
    ""ignoreOutOfRangeMaterialSlots"": true
  }
}";

        private static string WithSettings(string find, string replace)
        {
            return ValidJson.Replace(find, replace);
        }

        [Test]
        public void ValidSchemaParsesAllNineValues()
        {
            var parsed = PresetParser.TryParse(
                ValidJson, out var preset, out var refusal);
            Assert.That(parsed, Is.True, refusal.ToString());
            Assert.That(preset.Name, Is.EqualTo("Safe"));
            Assert.That(preset.Description, Is.EqualTo("Shipped defaults."));
            Assert.That(preset.AlphaSeparatorEnabled, Is.True);
            Assert.That(preset.PreserveTransparencyMaxMipLevel, Is.EqualTo(4));
            Assert.That(preset.PreserveTransparencyMinTextureSize,
                Is.EqualTo(128));
            Assert.That(preset.MinimumOpaqueCoveragePercent, Is.EqualTo(25));
            Assert.That(preset.MinimumOpaqueAlphaPercent, Is.EqualTo(100));
            Assert.That(preset.PolygonAlphaUpperClampPercent, Is.EqualTo(100));
            Assert.That(preset.PolygonMinimumOpaqueCoveragePercent,
                Is.EqualTo(100));
            Assert.That(preset.AllowDepthTestChange, Is.True);
            Assert.That(preset.IgnoreOutOfRangeMaterialSlots, Is.True);
        }

        [Test]
        public void BrokenJsonRefusesMalformed()
        {
            Assert.That(PresetParser.TryParse(
                "{ not json", out _, out var refusal), Is.False);
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.MalformedJson));
        }

        [Test]
        public void JsonArrayRefusesMalformed()
        {
            Assert.That(PresetParser.TryParse(
                "[]", out _, out var refusal), Is.False);
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.MalformedJson));
        }

        [Test]
        public void WrongSchemaVersionRefuses()
        {
            var json = WithSettings(
                "\"schemaVersion\": 1", "\"schemaVersion\": 2");
            Assert.That(PresetParser.TryParse(
                json, out _, out var refusal), Is.False);
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.UnknownSchemaVersion));
        }

        [Test]
        public void UnknownTopLevelFieldRefuses()
        {
            var json = WithSettings(
                "\"schemaVersion\": 1", "\"schemaVersion\": 1, \"extra\": 1");
            Assert.That(PresetParser.TryParse(
                json, out _, out var refusal), Is.False);
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.UnknownField));
        }

        [Test]
        public void UnknownFeatureFieldRefuses()
        {
            var json = WithSettings(
                "\"alphaSeparator\": true",
                "\"alphaSeparator\": true, \"extra\": true");
            Assert.That(PresetParser.TryParse(
                json, out _, out var refusal), Is.False);
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.UnknownField));
        }

        [Test]
        public void UnknownSettingFieldRefuses()
        {
            var json = WithSettings(
                "\"allowDepthTestChange\": true",
                "\"allowDepthTestChange\": true, \"extra\": 1");
            Assert.That(PresetParser.TryParse(
                json, out _, out var refusal), Is.False);
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.UnknownField));
        }

        [Test]
        public void MissingFieldRefuses()
        {
            var json = WithSettings(
                "  \"description\": \"Shipped defaults.\",\n", "");
            Assert.That(PresetParser.TryParse(
                json, out _, out var refusal), Is.False);
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.MissingField));
        }

        [Test]
        public void WrongValueTypeForSchemaVersionRefuses()
        {
            var json = WithSettings(
                "\"schemaVersion\": 1", "\"schemaVersion\": \"1\"");
            Assert.That(PresetParser.TryParse(
                json, out _, out var refusal), Is.False);
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.WrongValueType));
        }

        [Test]
        public void EmptyNameRefuses()
        {
            var json = WithSettings(
                "\"name\": \"Safe\"", "\"name\": \"\"");
            Assert.That(PresetParser.TryParse(
                json, out _, out var refusal), Is.False);
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.WrongValueType));
        }

        [Test]
        public void MipCapAboveTenRefuses()
        {
            var json = WithSettings(
                "\"preserveTransparencyMaxMipLevel\": 4",
                "\"preserveTransparencyMaxMipLevel\": 11");
            Assert.That(PresetParser.TryParse(
                json, out _, out var refusal), Is.False);
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.ValueOutOfRange));
        }

        [Test]
        public void MipCapBelowMinusOneRefuses()
        {
            var json = WithSettings(
                "\"preserveTransparencyMaxMipLevel\": 4",
                "\"preserveTransparencyMaxMipLevel\": -2");
            Assert.That(PresetParser.TryParse(
                json, out _, out var refusal), Is.False);
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.ValueOutOfRange));
        }

        [Test]
        public void NonPowerOfTwoTextureSizeRefuses()
        {
            var json = WithSettings(
                "\"preserveTransparencyMinTextureSize\": 128",
                "\"preserveTransparencyMinTextureSize\": 100");
            Assert.That(PresetParser.TryParse(
                json, out _, out var refusal), Is.False);
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.ValueOutOfRange));
        }

        [Test]
        public void TextureSizeAboveLimitRefuses()
        {
            var json = WithSettings(
                "\"preserveTransparencyMinTextureSize\": 128",
                "\"preserveTransparencyMinTextureSize\": 16384");
            Assert.That(PresetParser.TryParse(
                json, out _, out var refusal), Is.False);
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.ValueOutOfRange));
        }

        [Test]
        public void PercentAboveHundredRefuses()
        {
            var json = WithSettings(
                "\"minimumOpaqueCoveragePercent\": 25",
                "\"minimumOpaqueCoveragePercent\": 101");
            Assert.That(PresetParser.TryParse(
                json, out _, out var refusal), Is.False);
            Assert.That(refusal,
                Is.EqualTo(PresetLoadRefusal.ValueOutOfRange));
        }
    }
}
