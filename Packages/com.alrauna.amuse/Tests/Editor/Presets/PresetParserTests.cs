using NUnit.Framework;
using Alrauna.Amuse.Editor.Presets;

namespace Alrauna.Amuse.Tests.Editor
{
    public sealed class PresetParserTests
    {
        private const string ValidJson = @"{
  ""schemaVersion"": 4,
  ""name"": ""Safe"",
  ""description"": ""Shipped defaults."",
  ""features"": { ""alphaSeparator"": true },
  ""settings"": {
    ""preserveTransparencyMaxMipLevel"": 4,
    ""preserveTransparencyMinTextureSize"": 128,
    ""minimumOpaqueCoveragePercent"": 25,
    ""minimumOpaqueCoverageToPreservePercent"": 0,
    ""minimumOpaqueAlphaPercent"": 100,
    ""polygonAlphaUpperClampPercent"": 100,
    ""polygonMinimumOpaqueCoveragePercent"": 100,
    ""cutoutPreserveTransparencyMaxMipLevel"": 4,
    ""cutoutPreserveTransparencyMinTextureSize"": 128,
    ""cutoutMinimumOpaqueCoveragePercent"": 25,
    ""cutoutMinimumOpaqueCoverageToPreservePercent"": 0,
    ""allowDepthTestChange"": true,
    ""ignoreOutOfRangeMaterialSlots"": true,
    ""acceptDesktopAnisotropyDifferences"": false,
    ""acceptMobileAnisotropyDifferences"": false
  }
}";

        private static string WithSettings(string find, string replace)
        {
            return ValidJson.Replace(find, replace);
        }

        // A realistic version 2 body: version 2 and none of the
        // version 3 preserve key or the version 4 cutout keys.
        private static string LegacyVersionTwoBody()
        {
            return WithSettings(
                    "\"schemaVersion\": 4", "\"schemaVersion\": 2")
                .Replace(
                    "    \"minimumOpaqueCoverageToPreservePercent\": 0,\n",
                    "")
                .Replace(
                    "    \"cutoutPreserveTransparencyMaxMipLevel\": 4,\n" +
                    "    \"cutoutPreserveTransparencyMinTextureSize\": 128,\n" +
                    "    \"cutoutMinimumOpaqueCoveragePercent\": 25,\n" +
                    "    \"cutoutMinimumOpaqueCoverageToPreservePercent\": 0,\n",
                    "");
        }

        // A realistic version 1 body: version 1 and none of the
        // version 2 acceptance keys, the version 3 preserve key, or
        // the version 4 cutout keys.
        private static string LegacyVersionOneBody()
        {
            return WithSettings(
                    "\"schemaVersion\": 4", "\"schemaVersion\": 1")
                .Replace(
                    "    \"minimumOpaqueCoverageToPreservePercent\": 0,\n",
                    "")
                .Replace(
                    "    \"cutoutPreserveTransparencyMaxMipLevel\": 4,\n" +
                    "    \"cutoutPreserveTransparencyMinTextureSize\": 128,\n" +
                    "    \"cutoutMinimumOpaqueCoveragePercent\": 25,\n" +
                    "    \"cutoutMinimumOpaqueCoverageToPreservePercent\": 0,\n",
                    "")
                .Replace(
                    "    \"ignoreOutOfRangeMaterialSlots\": true,\n" +
                    "    \"acceptDesktopAnisotropyDifferences\": false,\n" +
                    "    \"acceptMobileAnisotropyDifferences\": false\n",
                    "    \"ignoreOutOfRangeMaterialSlots\": true\n");
        }

        // A realistic version 3 body: version 3 and none of the
        // version 4 cutout keys.
        private static string LegacyVersionThreeBody()
        {
            return WithSettings(
                    "\"schemaVersion\": 4", "\"schemaVersion\": 3")
                .Replace(
                    "    \"cutoutPreserveTransparencyMaxMipLevel\": 4,\n" +
                    "    \"cutoutPreserveTransparencyMinTextureSize\": 128,\n" +
                    "    \"cutoutMinimumOpaqueCoveragePercent\": 25,\n" +
                    "    \"cutoutMinimumOpaqueCoverageToPreservePercent\": 0,\n",
                    "");
        }

        private static void Refuses(string json, PresetLoadRefusal expected)
        {
            Assert.That(PresetParser.TryParse(
                json, out _, out var refusal), Is.False);
            Assert.That(refusal, Is.EqualTo(expected));
        }

        [Test]
        public void ValidSchemaParsesAllSixteenValues()
        {
            var parsed = PresetParser.TryParse(
                ValidJson, out var preset, out var refusal);
            Assert.That(parsed, Is.True, refusal.ToString());
            Assert.That(preset.Name, Is.EqualTo("Safe"));
            Assert.That(preset.Description, Is.EqualTo("Shipped defaults."));
            Assert.That(preset.AlphaSeparatorEnabled, Is.True);
            Assert.That(preset.PreserveTransparencyMaxMipLevel, Is.EqualTo(4));
            Assert.That(preset.CutoutPreserveTransparencyMaxMipLevel,
                Is.EqualTo(4));
            Assert.That(preset.PreserveTransparencyMinTextureSize,
                Is.EqualTo(128));
            Assert.That(preset.CutoutPreserveTransparencyMinTextureSize,
                Is.EqualTo(128));
            Assert.That(preset.MinimumOpaqueCoveragePercent, Is.EqualTo(25));
            Assert.That(preset.CutoutMinimumOpaqueCoveragePercent,
                Is.EqualTo(25));
            Assert.That(preset.MinimumOpaqueCoverageToPreservePercent,
                Is.EqualTo(0));
            Assert.That(preset.CutoutMinimumOpaqueCoverageToPreservePercent,
                Is.EqualTo(0));
            Assert.That(preset.MinimumOpaqueAlphaPercent, Is.EqualTo(100));
            Assert.That(preset.PolygonAlphaUpperClampPercent, Is.EqualTo(100));
            Assert.That(preset.PolygonMinimumOpaqueCoveragePercent,
                Is.EqualTo(100));
            Assert.That(preset.AllowDepthTestChange, Is.True);
            Assert.That(preset.IgnoreOutOfRangeMaterialSlots, Is.True);
            Assert.That(preset.AcceptDesktopAnisotropyDifferences, Is.False);
            Assert.That(preset.AcceptMobileAnisotropyDifferences, Is.False);
        }

        [Test]
        public void AcceptedTrueValuesParseThrough()
        {
            var json = WithSettings(
                "\"acceptDesktopAnisotropyDifferences\": false",
                "\"acceptDesktopAnisotropyDifferences\": true");
            Assert.That(PresetParser.TryParse(
                json, out var preset, out var refusal),
                Is.True, refusal.ToString());
            Assert.That(preset.AcceptDesktopAnisotropyDifferences, Is.True);
            Assert.That(preset.AcceptMobileAnisotropyDifferences, Is.False);
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
                "\"schemaVersion\": 4", "\"schemaVersion\": 5");
            Refuses(json, PresetLoadRefusal.UnknownSchemaVersion);
        }

        [Test]
        public void SchemaVersionOneRefusesAsUnknown()
        {
            // A real version 1 file carries only the version 1 keys.
            // It refuses with UnknownSchemaVersion, not with a
            // missing-key refusal, and nothing invents its missing
            // acceptance values.
            var legacy = LegacyVersionOneBody();
            Refuses(legacy, PresetLoadRefusal.UnknownSchemaVersion);
        }

        [Test]
        public void SchemaVersionTwoRefusesAsUnknown()
        {
            // A real version 2 file carries only the version 2 keys.
            // It refuses with UnknownSchemaVersion, not with a
            // missing-key refusal, and nothing invents its missing
            // preserve value.
            var legacy = LegacyVersionTwoBody();
            Refuses(legacy, PresetLoadRefusal.UnknownSchemaVersion);
        }

        // --- Falsifier 7: A parser that invents cutout values for a version 3 file answers a preset instead of UnknownSchemaVersion. The refusal test fails it. ---
        [Test]
        public void SchemaVersionThreeRefusesAsUnknown()
        {
            // A real version 3 file carries only the version 3 keys.
            // It refuses with UnknownSchemaVersion, not with a
            // missing-key refusal, and nothing invents its missing
            // cutout values.
            var legacy = LegacyVersionThreeBody();
            Refuses(legacy, PresetLoadRefusal.UnknownSchemaVersion);
        }

        [Test]
        public void MissingAcceptanceKeyRefuses()
        {
            var json = ValidJson.Replace(
                "    \"acceptDesktopAnisotropyDifferences\": false,\n",
                "");
            Refuses(json, PresetLoadRefusal.MissingField);
        }

        [Test]
        public void NonBooleanAcceptanceKeyRefuses()
        {
            var json = WithSettings(
                "\"acceptMobileAnisotropyDifferences\": false",
                "\"acceptMobileAnisotropyDifferences\": 1");
            Refuses(json, PresetLoadRefusal.WrongValueType);
        }

        [Test]
        public void UnknownTopLevelFieldRefuses()
        {
            var json = WithSettings(
                "\"schemaVersion\": 4", "\"schemaVersion\": 4, \"extra\": 1");
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
                "\"schemaVersion\": 4", "\"schemaVersion\": \"4\"");
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

        // One range-edge refusal per reader shape, each routed
        // through a cutout key. The cutout values must pass the same
        // range readers as their transparent counterparts.
        [Test]
        public void CutoutMipCapAboveTenRefuses()
        {
            Refuses(
                WithCutoutMipCap(11), PresetLoadRefusal.ValueOutOfRange);
        }

        [Test]
        public void CutoutTextureSizeAboveLimitRefuses()
        {
            Refuses(
                WithCutoutMinTextureSize(16384),
                PresetLoadRefusal.ValueOutOfRange);
        }

        [Test]
        public void CutoutPercentAboveHundredRefuses()
        {
            Refuses(WithSettings(
                    "\"cutoutMinimumOpaqueCoveragePercent\": 25",
                    "\"cutoutMinimumOpaqueCoveragePercent\": 101"),
                PresetLoadRefusal.ValueOutOfRange);
        }

        [Test]
        public void MissingCutoutKeyRefuses()
        {
            var json = ValidJson.Replace(
                "    \"cutoutMinimumOpaqueCoveragePercent\": 25,\n", "");
            Refuses(json, PresetLoadRefusal.MissingField);
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

        private static string WithPreserveCoverage(int value)
        {
            return ValidJson.Replace(
                "\"minimumOpaqueCoverageToPreservePercent\": 0",
                "\"minimumOpaqueCoverageToPreservePercent\": " + value);
        }

        private static string WithCutoutMipCap(int value)
        {
            return ValidJson.Replace(
                "\"cutoutPreserveTransparencyMaxMipLevel\": 4",
                "\"cutoutPreserveTransparencyMaxMipLevel\": " + value);
        }

        private static string WithCutoutMinTextureSize(int value)
        {
            return ValidJson.Replace(
                "\"cutoutPreserveTransparencyMinTextureSize\": 128",
                "\"cutoutPreserveTransparencyMinTextureSize\": " + value);
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
                    "\"schemaVersion\": 4",
                    "\"schemaVersion\": 3000000000"),
                PresetLoadRefusal.ValueOutOfRange);
        }

        [Test]
        public void IntegerBeyondLongRangeRefusesValueOutOfRange()
        {
            Refuses(WithSettings(
                    "\"schemaVersion\": 4",
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

        [Test]
        public void NullBodyRefusesMalformed()
        {
            Refuses(null, PresetLoadRefusal.MalformedJson);
        }

        [Test]
        public void CoveragePercentBelowZeroRefuses()
        {
            Refuses(WithSettings(
                    "\"minimumOpaqueCoveragePercent\": 25",
                    "\"minimumOpaqueCoveragePercent\": -1"),
                PresetLoadRefusal.ValueOutOfRange);
        }

        [Test]
        public void PreserveCoverageAboveHundredRefuses()
        {
            Refuses(
                WithPreserveCoverage(101),
                PresetLoadRefusal.ValueOutOfRange);
        }

        [Test]
        public void MissingPreserveCoverageRefuses()
        {
            var json = ValidJson.Replace(
                "    \"minimumOpaqueCoverageToPreservePercent\": 0,\n", "");
            Refuses(json, PresetLoadRefusal.MissingField);
        }

        [Test]
        public void MissingFeaturesObjectRefuses()
        {
            var json = WithSettings(
                "  \"features\": { \"alphaSeparator\": true },\n", "");
            Refuses(json, PresetLoadRefusal.MissingField);
        }

        [Test]
        public void NonObjectFeaturesRefusesWrongValueType()
        {
            Refuses(WithSettings(
                    "\"features\": { \"alphaSeparator\": true }",
                    "\"features\": []"),
                PresetLoadRefusal.WrongValueType);
        }

        [Test]
        public void SubSentinelClampWithZeroOpaqueParses()
        {
            // A clamp below the sentinel 100 takes the comparison
            // branch, but an opaque percent of 0 reads as effective
            // 100, so a clamp of 59 stays inside the bound.
            var body = WithSettings(
                    "\"minimumOpaqueAlphaPercent\": 100",
                    "\"minimumOpaqueAlphaPercent\": 0")
                .Replace("\"polygonAlphaUpperClampPercent\": 100",
                    "\"polygonAlphaUpperClampPercent\": 59");
            Assert.That(PresetParser.TryParse(
                body, out var preset, out var refusal),
                Is.True, refusal.ToString());
            Assert.That(preset.MinimumOpaqueAlphaPercent, Is.EqualTo(0));
            Assert.That(
                preset.PolygonAlphaUpperClampPercent, Is.EqualTo(59));
        }
    }
}
