using System;
using Newtonsoft.Json.Linq;

namespace Alrauna.Amuse.Editor.Presets
{
    /// <summary>
    /// Parses preset text against the closed version 4 schema. The
    /// parser refuses instead of guessing: an unknown key, a missing
    /// key, a wrong type, or an out-of-range value answers a named
    /// refusal and no preset, because a file must never half-apply.
    /// The version is read before every version-specific key, so an
    /// older file answers UnknownSchemaVersion instead of a key
    /// refusal. The tree walk is manual, so each cause maps to
    /// exactly one refusal value.
    /// </summary>
    internal static class PresetParser
    {
        internal const int CurrentSchemaVersion = 4;

        private static readonly string[] FileKeys =
            { "schemaVersion", "name", "description", "features", "settings" };

        private static readonly string[] FeatureKeys = { "alphaSeparator" };

        private static readonly string[] SettingKeys =
        {
            "preserveTransparencyMaxMipLevel",
            "preserveTransparencyMinTextureSize",
            "minimumOpaqueCoveragePercent",
            "minimumOpaqueCoverageToPreservePercent",
            "minimumOpaqueAlphaPercent",
            "polygonAlphaUpperClampPercent",
            "polygonMinimumOpaqueCoveragePercent",
            "cutoutPreserveTransparencyMaxMipLevel",
            "cutoutPreserveTransparencyMinTextureSize",
            "cutoutMinimumOpaqueCoveragePercent",
            "cutoutMinimumOpaqueCoverageToPreservePercent",
            "allowDepthTestChange",
            "ignoreOutOfRangeMaterialSlots",
            "acceptDesktopAnisotropyDifferences",
            "acceptMobileAnisotropyDifferences",
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

            // The version gates every version-specific key. Read it
            // first, so an older file refuses with UnknownSchemaVersion
            // instead of a missing-key refusal against the new schema.
            if (!TryRead<int>(
                    file, "schemaVersion", JTokenType.Integer,
                    ref refusal, out var version))
            {
                return false;
            }
            if (version != CurrentSchemaVersion)
            {
                refusal = PresetLoadRefusal.UnknownSchemaVersion;
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

            if (!TryReadNonEmptyString(file, "name", ref refusal, out var name)
                || !TryReadNonEmptyString(
                    file, "description", ref refusal, out var description)
                || !TryRead<bool>(
                    features, "alphaSeparator", JTokenType.Boolean, ref refusal,
                    out var alphaSeparator)
                || !TryReadMipCap(
                    settings, "preserveTransparencyMaxMipLevel",
                    ref refusal, out var mipCap)
                || !TryReadMinTextureSize(
                    settings, "preserveTransparencyMinTextureSize",
                    ref refusal, out var minSize)
                || !TryReadPercent(
                    settings, "minimumOpaqueCoveragePercent",
                    ref refusal, out var coverage)
                || !TryReadPercent(
                    settings, "minimumOpaqueCoverageToPreservePercent",
                    ref refusal, out var preserveCoverage)
                || !TryReadPercent(
                    settings, "minimumOpaqueAlphaPercent",
                    ref refusal, out var alphaClamp)
                || !TryReadPercent(
                    settings, "polygonAlphaUpperClampPercent",
                    ref refusal, out var polygonClamp)
                || !TryReadPercent(
                    settings, "polygonMinimumOpaqueCoveragePercent",
                    ref refusal, out var polygonCoverage)
                || !TryReadMipCap(
                    settings, "cutoutPreserveTransparencyMaxMipLevel",
                    ref refusal, out var cutoutMipCap)
                || !TryReadMinTextureSize(
                    settings, "cutoutPreserveTransparencyMinTextureSize",
                    ref refusal, out var cutoutMinSize)
                || !TryReadPercent(
                    settings, "cutoutMinimumOpaqueCoveragePercent",
                    ref refusal, out var cutoutCoverage)
                || !TryReadPercent(
                    settings, "cutoutMinimumOpaqueCoverageToPreservePercent",
                    ref refusal, out var cutoutPreserveCoverage)
                || !TryRead<bool>(
                    settings, "allowDepthTestChange",
                    JTokenType.Boolean, ref refusal, out var depthTest)
                || !TryRead<bool>(
                    settings, "ignoreOutOfRangeMaterialSlots",
                    JTokenType.Boolean, ref refusal, out var ignoreOutOfRange)
                || !TryRead<bool>(
                    settings, "acceptDesktopAnisotropyDifferences",
                    JTokenType.Boolean, ref refusal, out var acceptDesktop)
                || !TryRead<bool>(
                    settings, "acceptMobileAnisotropyDifferences",
                    JTokenType.Boolean, ref refusal, out var acceptMobile))
            {
                return false;
            }

            if (polygonClamp != 100)
            {
                var effectiveOpaque = alphaClamp <= 0 ? 100 : alphaClamp;
                if (polygonClamp >= effectiveOpaque)
                {
                    refusal = PresetLoadRefusal.ClampAboveOpaquePercent;
                    return false;
                }
            }

            preset = new OptimizerPreset(
                name, description, alphaSeparator,
                mipCap, cutoutMipCap,
                minSize, cutoutMinSize,
                coverage, cutoutCoverage,
                preserveCoverage, cutoutPreserveCoverage,
                alphaClamp, polygonClamp, polygonCoverage,
                depthTest, ignoreOutOfRange,
                acceptDesktop, acceptMobile);
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
            if (typeof(T) == typeof(int))
            {
                // A token beyond long range carries a BigInteger, so
                // the boxed value is not a long. Refuse it by name
                // instead of letting the conversion throw.
                if (!(token is JValue jValue) || !(jValue.Value is long raw))
                {
                    refusal = PresetLoadRefusal.ValueOutOfRange;
                    value = default;
                    return false;
                }
                if (raw < int.MinValue || raw > int.MaxValue)
                {
                    refusal = PresetLoadRefusal.ValueOutOfRange;
                    value = default;
                    return false;
                }
                value = (T)(object)(int)raw;
                return true;
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
            string key,
            ref PresetLoadRefusal refusal,
            out int value)
        {
            if (!TryRead<int>(
                    obj, key,
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
            string key,
            ref PresetLoadRefusal refusal,
            out int value)
        {
            if (!TryRead<int>(
                    obj, key,
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
