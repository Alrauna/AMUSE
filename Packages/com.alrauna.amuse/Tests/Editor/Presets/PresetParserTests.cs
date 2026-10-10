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

        private static void Refuses(string json, PresetLoadRefusal expected)
        {
            Assert.That(PresetParser.TryParse(
                json, out _, out var refusal), Is.False);
            Assert.That(refusal, Is.EqualTo(expected));
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
            Refuses("{ not json", PresetLoadRefusal.MalformedJson);
        }

        [Test]
        public void JsonArrayRefusesMalformed()
        {
            Refuses("[]", PresetLoadRefusal.MalformedJson);
        }

        [Test]
        public void WrongSchemaVersionRefuses()
        {
            var json = WithSettings(
                "\"schemaVersion\": 1", "\"schemaVersion\": 2");
            Refuses(json, PresetLoadRefusal.UnknownSchemaVersion);
        }

        [Test]
        public void UnknownTopLevelFieldRefuses()
        {
            var json = WithSettings(
                "\"schemaVersion\": 1", "\"schemaVersion\": 1, \"extra\": 1");
            Refuses(json, PresetLoadRefusal.UnknownField);
        }

        [Test]
        public void UnknownFeatureFieldRefuses()
        {
            var json = WithSettings(
                "\"alphaSeparator\": true",
                "\"alphaSeparator\": true, \"extra\": true");
            Refuses(json, PresetLoadRefusal.UnknownField);
        }

        [Test]
        public void UnknownSettingFieldRefuses()
        {
            var json = WithSettings(
                "\"allowDepthTestChange\": true",
                "\"allowDepthTestChange\": true, \"extra\": 1");
            Refuses(json, PresetLoadRefusal.UnknownField);
        }

        [Test]
        public void MissingFieldRefuses()
        {
            var json = WithSettings(
                "  \"description\": \"Shipped defaults.\",\n", "");
            Refuses(json, PresetLoadRefusal.MissingField);
        }

        [Test]
        public void WrongValueTypeForSchemaVersionRefuses()
        {
            var json = WithSettings(
                "\"schemaVersion\": 1", "\"schemaVersion\": \"1\"");
            Refuses(json, PresetLoadRefusal.WrongValueType);
        }

        [Test]
        public void EmptyNameRefuses()
        {
            var json = WithSettings(
                "\"name\": \"Safe\"", "\"name\": \"\"");
            Refuses(json, PresetLoadRefusal.WrongValueType);
        }

        [Test]
        public void MipCapAboveTenRefuses()
        {
            Refuses(WithMipCap(11), PresetLoadRefusal.ValueOutOfRange);
        }

        [Test]
        public void MipCapBelowMinusOneRefuses()
        {
            Refuses(WithMipCap(-2), PresetLoadRefusal.ValueOutOfRange);
        }

        [Test]
        public void NonPowerOfTwoTextureSizeRefuses()
        {
            Refuses(
                WithMinTextureSize(100), PresetLoadRefusal.ValueOutOfRange);
        }

        [Test]
        public void TextureSizeAboveLimitRefuses()
        {
            Refuses(
                WithMinTextureSize(16384), PresetLoadRefusal.ValueOutOfRange);
        }

        [Test]
        public void TextureSizeOfOneRefuses()
        {
            Refuses(WithMinTextureSize(1), PresetLoadRefusal.ValueOutOfRange);
        }

        private static string WithMipCap(int value)
        {
            return ValidJson.Replace(
                "\"preserveTransparencyMaxMipLevel\": 4",
                "\"preserveTransparencyMaxMipLevel\": " + value);
        }

        private static string WithMinTextureSize(int value)
        {
            return ValidJson.Replace(
                "\"preserveTransparencyMinTextureSize\": 128",
                "\"preserveTransparencyMinTextureSize\": " + value);
        }

        [Test]
        public void AcceptedEdgeValuesParseSuccessfully()
        {
            foreach (var mipCap in new[] { -1, 0, 10 })
            {
                Assert.That(PresetParser.TryParse(
                    WithMipCap(mipCap), out _, out var refusal),
                    Is.True, "mip cap " + mipCap + ": " + refusal);
            }
            foreach (var size in new[] { -1, 2, 8192 })
            {
                Assert.That(PresetParser.TryParse(
                    WithMinTextureSize(size), out _, out var refusal),
                    Is.True, "texture size " + size + ": " + refusal);
            }
        }

        [Test]
        public void PercentAboveHundredRefuses()
        {
            var json = WithSettings(
                "\"minimumOpaqueCoveragePercent\": 25",
                "\"minimumOpaqueCoveragePercent\": 101");
            Refuses(json, PresetLoadRefusal.ValueOutOfRange);
        }

        [Test]
        public void IntegerBeyondIntRangeRefusesValueOutOfRange()
        {
            Refuses(WithSettings(
                    "\"schemaVersion\": 1",
                    "\"schemaVersion\": 3000000000"),
                PresetLoadRefusal.ValueOutOfRange);
        }

        [Test]
        public void IntegerBeyondLongRangeRefusesValueOutOfRange()
        {
            Refuses(WithSettings(
                    "\"schemaVersion\": 1",
                    "\"schemaVersion\": 99999999999999999999"),
                PresetLoadRefusal.ValueOutOfRange);
        }

        [Test]
        public void ClampAtOrAboveOpaquePercentRefuses()
        {
            Refuses(WithSettings(
                    "\"minimumOpaqueAlphaPercent\": 100",
                    "\"minimumOpaqueAlphaPercent\": 60")
                .Replace("\"polygonAlphaUpperClampPercent\": 100",
                    "\"polygonAlphaUpperClampPercent\": 60"),
                PresetLoadRefusal.ClampAboveOpaquePercent);
        }

        [Test]
        public void ClampBelowOpaquePercentParses()
        {
            var body = WithSettings(
                    "\"minimumOpaqueAlphaPercent\": 100",
                    "\"minimumOpaqueAlphaPercent\": 60")
                .Replace("\"polygonAlphaUpperClampPercent\": 100",
                    "\"polygonAlphaUpperClampPercent\": 59");
            Assert.That(PresetParser.TryParse(
                body, out _, out var refusal), Is.True, refusal.ToString());
        }

        [Test]
        public void InertClampWithAnyOpaqueParses()
        {
            // The shipped clamp 100 stays the inert sentinel over an
            // opaque percent of 0, which reads as effective 100.
            var body = WithSettings(
                "\"minimumOpaqueAlphaPercent\": 100",
                "\"minimumOpaqueAlphaPercent\": 0");
            Assert.That(PresetParser.TryParse(
                body, out _, out var refusal), Is.True, refusal.ToString());
        }
    }
}
