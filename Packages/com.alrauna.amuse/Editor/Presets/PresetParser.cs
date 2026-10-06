using System;
using Newtonsoft.Json.Linq;

namespace Alrauna.Amuse.Editor.Presets
{
    /// <summary>
    /// Parses preset text against the closed version 1 schema. The
    /// parser refuses instead of guessing: an unknown key, a missing
    /// key, a wrong type, or an out-of-range value answers a named
    /// refusal and no preset, because a file must never half-apply.
    /// The tree walk is manual, so each cause maps to exactly one
    /// refusal value.
    /// </summary>
    internal static class PresetParser
    {
        internal const int CurrentSchemaVersion = 1;

        private static readonly string[] FileKeys =
            { "schemaVersion", "name", "description", "features", "settings" };

        private static readonly string[] FeatureKeys = { "alphaSeparator" };

        private static readonly string[] SettingKeys =
        {
            "preserveTransparencyMaxMipLevel",
            "preserveTransparencyMinTextureSize",
            "minimumOpaqueCoveragePercent",
            "minimumOpaqueAlphaPercent",
            "polygonAlphaUpperClampPercent",
            "polygonMinimumOpaqueCoveragePercent",
            "allowDepthTestChange",
            "ignoreOutOfRangeMaterialSlots",
        };

        internal static bool TryParse(
            string json,
            out OptimizerPreset preset,
            out PresetLoadRefusal refusal)
        {
            preset = null;
            refusal = PresetLoadRefusal.None;

            JToken root;
            try
            {
                root = JToken.Parse(json ?? string.Empty);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                refusal = PresetLoadRefusal.MalformedJson;
                return false;
            }

            if (!(root is JObject file))
            {
                refusal = PresetLoadRefusal.MalformedJson;
                return false;
            }

            if (!TryRead<JObject>(
                    file, "features", JTokenType.Object,
                    ref refusal, out var features)
                || !TryRead<JObject>(
                    file, "settings", JTokenType.Object,
                    ref refusal, out var settings))
            {
                return false;
            }

            if (!EnsureClosedKeySet(file, FileKeys, ref refusal)
                || !EnsureClosedKeySet(features, FeatureKeys, ref refusal)
                || !EnsureClosedKeySet(settings, SettingKeys, ref refusal))
            {
                return false;
            }

            if (!TryRead<int>(
                    file, "schemaVersion", JTokenType.Integer,
                    ref refusal, out var version)
                || !TryReadNonEmptyString(file, "name", ref refusal, out var name)
                || !TryReadNonEmptyString(
                    file, "description", ref refusal, out var description)
                || !TryRead<bool>(
                    features, "alphaSeparator", JTokenType.Boolean, ref refusal,
                    out var alphaSeparator)
                || !TryReadMipCap(settings, ref refusal, out var mipCap)
                || !TryReadMinTextureSize(settings, ref refusal, out var minSize)
                || !TryReadPercent(
                    settings, "minimumOpaqueCoveragePercent",
                    ref refusal, out var coverage)
                || !TryReadPercent(
                    settings, "minimumOpaqueAlphaPercent",
                    ref refusal, out var alphaClamp)
                || !TryReadPercent(
                    settings, "polygonAlphaUpperClampPercent",
                    ref refusal, out var polygonClamp)
                || !TryReadPercent(
                    settings, "polygonMinimumOpaqueCoveragePercent",
                    ref refusal, out var polygonCoverage)
                || !TryRead<bool>(
                    settings, "allowDepthTestChange",
                    JTokenType.Boolean, ref refusal, out var depthTest)
                || !TryRead<bool>(
                    settings, "ignoreOutOfRangeMaterialSlots",
                    JTokenType.Boolean, ref refusal, out var ignoreOutOfRange))
            {
                return false;
            }

            if (version != CurrentSchemaVersion)
            {
                refusal = PresetLoadRefusal.UnknownSchemaVersion;
                return false;
            }

            preset = new OptimizerPreset(
                name, description, alphaSeparator, mipCap, minSize,
                coverage, alphaClamp, polygonClamp, polygonCoverage,
                depthTest, ignoreOutOfRange);
            return true;
        }

        private static bool EnsureClosedKeySet(
            JObject obj,
            string[] allowed,
            ref PresetLoadRefusal refusal)
        {
            foreach (var property in obj.Properties())
            {
                if (Array.IndexOf(allowed, property.Name) < 0)
                {
                    refusal = PresetLoadRefusal.UnknownField;
                    return false;
                }
            }
            return true;
        }

        // One reader for every typed leaf, so the presence and type
        // refusals cannot drift apart between the schema branches.
        private static bool TryRead<T>(
            JObject obj,
            string key,
            JTokenType expected,
            ref PresetLoadRefusal refusal,
            out T value)
        {
            if (!obj.TryGetValue(key, out var token))
            {
                refusal = PresetLoadRefusal.MissingField;
                value = default;
                return false;
            }
            if (token.Type != expected)
            {
                refusal = PresetLoadRefusal.WrongValueType;
                value = default;
                return false;
            }
            value = token.Value<T>();
            return true;
        }

        // The shared reader answers the presence and type refusals;
        // an empty string is still a wrong value, so the empty check
        // stays here.
        private static bool TryReadNonEmptyString(
            JObject obj,
            string key,
            ref PresetLoadRefusal refusal,
            out string value)
        {
            if (!TryRead<string>(
                    obj, key, JTokenType.String,
                    ref refusal, out value))
            {
                return false;
            }
            if (string.IsNullOrEmpty(value))
            {
                refusal = PresetLoadRefusal.WrongValueType;
                value = null;
                return false;
            }
            return true;
        }

        private static bool TryReadMipCap(
            JObject obj,
            ref PresetLoadRefusal refusal,
            out int value)
        {
            if (!TryRead<int>(
                    obj, "preserveTransparencyMaxMipLevel",
                    JTokenType.Integer, ref refusal, out value))
            {
                return false;
            }
            if (value != -1 && (value < 0 || value > 10))
            {
                refusal = PresetLoadRefusal.ValueOutOfRange;
                return false;
            }
            return true;
        }

        private static bool TryReadMinTextureSize(
            JObject obj,
            ref PresetLoadRefusal refusal,
            out int value)
        {
            if (!TryRead<int>(
                    obj, "preserveTransparencyMinTextureSize",
                    JTokenType.Integer, ref refusal, out value))
            {
                return false;
            }
            var powerOfTwo = value >= 2 && (value & (value - 1)) == 0;
            if (value != -1 && (!powerOfTwo || value > 8192))
            {
                refusal = PresetLoadRefusal.ValueOutOfRange;
                return false;
            }
            return true;
        }

        private static bool TryReadPercent(
            JObject obj,
            string key,
            ref PresetLoadRefusal refusal,
            out int value)
        {
            if (!TryRead<int>(
                    obj, key, JTokenType.Integer, ref refusal, out value))
            {
                return false;
            }
            if (value < 0 || value > 100)
            {
                refusal = PresetLoadRefusal.ValueOutOfRange;
                return false;
            }
            return true;
        }
    }
}
