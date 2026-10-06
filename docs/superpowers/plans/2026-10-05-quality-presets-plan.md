# Quality Presets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Safe, Normal, and Aggressive preset row to the optimizer inspector, backed by three JSON files shipped inside the package, all three holding the shipped defaults for now.

**Architecture:** A pure JSON parser with a closed refusal enum, a file store that reads the three files from the installed package, an applier that writes nine serialized fields through one `SerializedObject`, and a computed pressed state on a d4rk-style row of three buttons. No serialized component state changes.

**Tech Stack:** Unity 2022.3 editor C#, Newtonsoft JSON (`com.unity.nuget.newtonsoft-json` 3.2.1, already in the dependency closure), NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-10-05-quality-presets-design.md`

**Working discipline:** Branch `feature/optimizer-ui-rework` at `f319186`, cut from `main`. Allowed mutations: the files named in the tasks below, plus `README.md`. The uncommitted change to `.github/workflows/pr.yml` is user-owned; do not touch, stage, or absorb it. Commit steps run only with explicit session authorization. Stop and return evidence when an observed fact contradicts the spec's mapping table or ranges.

## Global Constraints

- Production code is editor-only except the runtime component assembly. Never mutate Unity source assets.
- Labels and tooltips use plain technical English. Short active sentences. No contractions.
- XML doc comments state why, not what.
- Fail closed: a preset file error is a named refusal, never a partial apply and never a blanket "unsupported".
- RED/GREEN: observe the failing test first against a named wrong implementation. A test that passes on first run is recorded as characterization. Falsifier steps prove a test can fail; restore the code after each falsifier observation.
- A filtered test run that reports 0 tests is a failure. Record observed counts.
- Unity MCP safety, before any tool use on the editor: read `mcpforunity://instances`; inspect `Application.dataPath` of the candidate; require an exact match to the repository `Assets` folder; pin the instance when more than one is connected. Tests never run in the Census Lab project.
- No absolute or machine-specific paths in code, comments, docs, or commit messages.
- Treat Unity assets and `.meta` files as one unit. Let Unity generate new `.meta` files on refresh; commit them beside their assets.

---

### Task 1: Dependency wiring

**Files:**
- Modify: `Packages/com.alrauna.amuse/package.json`
- Modify: `Packages/com.alrauna.amuse/Editor/Alrauna.Amuse.Editor.asmdef`

**Interfaces:**
- Consumes: nothing new.
- Produces: the editor assembly references Newtonsoft JSON. Every later task compiles against it.

- [ ] **Step 1: Declare the dependency**

In `Packages/com.alrauna.amuse/package.json`, add after `"vpmDependencies"`:

```json
  "dependencies": {
    "com.unity.nuget.newtonsoft-json": "3.2.1"
  },
```

- [ ] **Step 2: Reference the assembly**

In `Packages/com.alrauna.amuse/Editor/Alrauna.Amuse.Editor.asmdef`, add the reference to the `references` array:

```json
        "com.unity.nuget.newtonsoft-json"
```

- [ ] **Step 3: Refresh and confirm resolution**

Refresh the dev editor instance and confirm zero compile errors. Confirm `Packages/packages-lock.json` still pins `com.unity.nuget.newtonsoft-json` at `3.2.1`. `Packages/manifest.json` must remain unchanged.

- [ ] **Step 4: Commit**

```bash
git add Packages/com.alrauna.amuse/package.json Packages/com.alrauna.amuse/Editor/Alrauna.Amuse.Editor.asmdef
git commit -m "build: reference newtonsoft json for the preset loader"
```

---

### Task 2: Refusal enum, preset record, and match rule

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Presets/PresetLoadRefusal.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Presets/OptimizerPreset.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Presets/OptimizerPresetTests.cs`

**Interfaces:**
- Consumes: `AmuseAvatarOptimizer` public properties `AlphaSeparatorEnabled`, `PreserveTransparencyMaxMipLevel`, `PreserveTransparencyMinTextureSize`, `MinimumOpaqueCoveragePercent`, `MinimumOpaqueAlphaPercent`, `PolygonAlphaUpperClampPercent`, `PolygonMinimumOpaqueCoveragePercent`, `AllowDepthTestChange`, `IgnoreOutOfRangeMaterialSlots`.
- Produces: `PresetLoadRefusal` with values `None, FileMissing, MalformedJson, UnknownSchemaVersion, MissingField, UnknownField, WrongValueType, ValueOutOfRange`; `OptimizerPreset` with an 11-argument constructor `(string name, string description, bool alphaSeparatorEnabled, int preserveTransparencyMaxMipLevel, int preserveTransparencyMinTextureSize, int minimumOpaqueCoveragePercent, int minimumOpaqueAlphaPercent, int polygonAlphaUpperClampPercent, int polygonMinimumOpaqueCoveragePercent, bool allowDepthTestChange, bool ignoreOutOfRangeMaterialSlots)`, read-only properties of the same names in PascalCase, and `internal bool Matches(AmuseAvatarOptimizer component)`. Task 3 constructs records with this constructor; tasks 4, 5, and 6 read the properties.

- [ ] **Step 1: Create the refusal enum**

```csharp
namespace Alrauna.Amuse.Editor.Presets
{
    /// <summary>
    /// Why a preset file did not load. The value list is closed: a new
    /// cause means a new named value, never a reused one. None is the
    /// zero value, so a fresh field means "no problem".
    /// </summary>
    internal enum PresetLoadRefusal
    {
        None = 0,
        FileMissing,
        MalformedJson,
        UnknownSchemaVersion,
        MissingField,
        UnknownField,
        WrongValueType,
        ValueOutOfRange,
    }
}
```

- [ ] **Step 2: Create the preset record**

```csharp
using Alrauna.Amuse.Runtime;

namespace Alrauna.Amuse.Editor.Presets
{
    /// <summary>
    /// One parsed preset file. Immutable. The build never reads this
    /// type. The inspector compares it against the live component and
    /// applies it through the serialized object.
    /// </summary>
    internal sealed class OptimizerPreset
    {
        internal string Name { get; }
        internal string Description { get; }
        internal bool AlphaSeparatorEnabled { get; }
        internal int PreserveTransparencyMaxMipLevel { get; }
        internal int PreserveTransparencyMinTextureSize { get; }
        internal int MinimumOpaqueCoveragePercent { get; }
        internal int MinimumOpaqueAlphaPercent { get; }
        internal int PolygonAlphaUpperClampPercent { get; }
        internal int PolygonMinimumOpaqueCoveragePercent { get; }
        internal bool AllowDepthTestChange { get; }
        internal bool IgnoreOutOfRangeMaterialSlots { get; }

        internal OptimizerPreset(
            string name,
            string description,
            bool alphaSeparatorEnabled,
            int preserveTransparencyMaxMipLevel,
            int preserveTransparencyMinTextureSize,
            int minimumOpaqueCoveragePercent,
            int minimumOpaqueAlphaPercent,
            int polygonAlphaUpperClampPercent,
            int polygonMinimumOpaqueCoveragePercent,
            bool allowDepthTestChange,
            bool ignoreOutOfRangeMaterialSlots)
        {
            Name = name;
            Description = description;
            AlphaSeparatorEnabled = alphaSeparatorEnabled;
            PreserveTransparencyMaxMipLevel = preserveTransparencyMaxMipLevel;
            PreserveTransparencyMinTextureSize = preserveTransparencyMinTextureSize;
            MinimumOpaqueCoveragePercent = minimumOpaqueCoveragePercent;
            MinimumOpaqueAlphaPercent = minimumOpaqueAlphaPercent;
            PolygonAlphaUpperClampPercent = polygonAlphaUpperClampPercent;
            PolygonMinimumOpaqueCoveragePercent = polygonMinimumOpaqueCoveragePercent;
            AllowDepthTestChange = allowDepthTestChange;
            IgnoreOutOfRangeMaterialSlots = ignoreOutOfRangeMaterialSlots;
        }

        /// <summary>
        /// True when the component equals this preset on all nine
        /// mapped fields. The pressed state is computed from this
        /// comparison and never stored, so a manual edit of one value
        /// leaves every button unpressed.
        /// </summary>
        internal bool Matches(AmuseAvatarOptimizer component)
        {
            return component.AlphaSeparatorEnabled == AlphaSeparatorEnabled
                && component.PreserveTransparencyMaxMipLevel
                    == PreserveTransparencyMaxMipLevel
                && component.PreserveTransparencyMinTextureSize
                    == PreserveTransparencyMinTextureSize
                && component.MinimumOpaqueCoveragePercent
                    == MinimumOpaqueCoveragePercent
                && component.MinimumOpaqueAlphaPercent
                    == MinimumOpaqueAlphaPercent
                && component.PolygonAlphaUpperClampPercent
                    == PolygonAlphaUpperClampPercent
                && component.PolygonMinimumOpaqueCoveragePercent
                    == PolygonMinimumOpaqueCoveragePercent
                && component.AllowDepthTestChange == AllowDepthTestChange
                && component.IgnoreOutOfRangeMaterialSlots
                    == IgnoreOutOfRangeMaterialSlots;
        }
    }
}
```

- [ ] **Step 3: Write the match tests**

Create `Packages/com.alrauna.amuse/Tests/Editor/Presets/OptimizerPresetTests.cs`. The folder and its `.meta` file appear after the Unity refresh in step 5.

```csharp
using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Runtime;
using Alrauna.Amuse.Editor.Presets;

namespace Alrauna.Amuse.Tests.Editor
{
    public sealed class OptimizerPresetTests
    {
        internal static OptimizerPreset ShippedDefaults()
        {
            return new OptimizerPreset(
                "Safe", "Shipped defaults.", true,
                4, 128, 25, 100, 100, 100, true, true);
        }

        [Test]
        public void DefaultComponentMatchesTheShippedDefaults()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(ShippedDefaults().Matches(optimizer), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FlippingAnyOneFieldValueBreaksTheMatch()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();

                var serialized = new UnityEditor.SerializedObject(optimizer);
                serialized.FindProperty("_minimumOpaqueCoveragePercent")
                    .intValue = 30;
                serialized.ApplyModifiedProperties();

                Assert.That(ShippedDefaults().Matches(optimizer), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
```

- [ ] **Step 4: Falsifier 1**

Temporarily delete the `AllowDepthTestChange` comparison from `Matches`. Run the filter `DefaultComponentMatchesTheShippedDefaults`.
Expected: PASS, which is the defect: the match no longer reads that field. Restore the comparison.

- [ ] **Step 5: Refresh, compile, run**

Refresh the dev editor instance, confirm zero compile errors, then run the EditMode filter `DefaultComponentMatchesTheShippedDefaults;FlippingAnyOneFieldValueBreaksTheMatch`.
Expected: 2 tests, 2 passed, 0 failed. Record the run as characterization: both pass on first run, and Falsifier 1 proved the first test can fail.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Presets Packages/com.alrauna.amuse/Tests/Editor/Presets
git commit -m "feat: add the preset record with its closed refusal values"
```

---

### Task 3: The pure parser

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetParserTests.cs`

**Interfaces:**
- Consumes: `PresetLoadRefusal`, the `OptimizerPreset` constructor from Task 2.
- Produces: `internal static bool PresetParser.TryParse(string json, out OptimizerPreset preset, out PresetLoadRefusal refusal)`. On false, `preset` is null and `refusal` names the cause. On true, `refusal` is `None`. Task 4 consumes this signature.

- [ ] **Step 1: Write the parser tests**

Create `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetParserTests.cs`:

```csharp
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
        public void TextureSizeOfOneRefuses()
        {
            var json = WithSettings(
                "\"preserveTransparencyMinTextureSize\": 128",
                "\"preserveTransparencyMinTextureSize\": 1");
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
```

- [ ] **Step 2: Implement the parser**

```csharp
using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

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

            if (!TryReadObject(file, "features", ref refusal, out var features)
                || !TryReadObject(file, "settings", ref refusal, out var settings))
            {
                return false;
            }

            if (!EnsureClosedKeySet(file, FileKeys, ref refusal)
                || !EnsureClosedKeySet(features, FeatureKeys, ref refusal)
                || !EnsureClosedKeySet(settings, SettingKeys, ref refusal))
            {
                return false;
            }

            if (!TryReadInt(file, "schemaVersion", ref refusal, out var version)
                || !TryReadNonEmptyString(file, "name", ref refusal, out var name)
                || !TryReadNonEmptyString(
                    file, "description", ref refusal, out var description)
                || !TryReadBool(
                    features, "alphaSeparator", ref refusal,
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
                || !TryReadBool(
                    settings, "allowDepthTestChange",
                    ref refusal, out var depthTest)
                || !TryReadBool(
                    settings, "ignoreOutOfRangeMaterialSlots",
                    ref refusal, out var ignoreOutOfRange))
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
            refusal = PresetLoadRefusal.None;
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

        private static bool TryReadObject(
            JObject obj,
            string key,
            ref PresetLoadRefusal refusal,
            out JObject value)
        {
            if (!obj.TryGetValue(key, out var token))
            {
                refusal = PresetLoadRefusal.MissingField;
                value = null;
                return false;
            }
            if (!(token is JObject child))
            {
                refusal = PresetLoadRefusal.WrongValueType;
                value = null;
                return false;
            }
            value = child;
            return true;
        }

        private static bool TryReadInt(
            JObject obj,
            string key,
            ref PresetLoadRefusal refusal,
            out int value)
        {
            if (!obj.TryGetValue(key, out var token))
            {
                refusal = PresetLoadRefusal.MissingField;
                value = 0;
                return false;
            }
            if (token.Type != JTokenType.Integer)
            {
                refusal = PresetLoadRefusal.WrongValueType;
                value = 0;
                return false;
            }
            value = token.Value<int>();
            return true;
        }

        private static bool TryReadNonEmptyString(
            JObject obj,
            string key,
            ref PresetLoadRefusal refusal,
            out string value)
        {
            if (!obj.TryGetValue(key, out var token))
            {
                refusal = PresetLoadRefusal.MissingField;
                value = null;
                return false;
            }
            if (token.Type != JTokenType.String || string.IsNullOrEmpty(
                    token.Value<string>()))
            {
                refusal = PresetLoadRefusal.WrongValueType;
                value = null;
                return false;
            }
            value = token.Value<string>();
            return true;
        }

        private static bool TryReadBool(
            JObject obj,
            string key,
            ref PresetLoadRefusal refusal,
            out bool value)
        {
            if (!obj.TryGetValue(key, out var token))
            {
                refusal = PresetLoadRefusal.MissingField;
                value = false;
                return false;
            }
            if (token.Type != JTokenType.Boolean)
            {
                refusal = PresetLoadRefusal.WrongValueType;
                value = false;
                return false;
            }
            value = token.Value<bool>();
            return true;
        }

        private static bool TryReadMipCap(
            JObject obj,
            ref PresetLoadRefusal refusal,
            out int value)
        {
            if (!TryReadInt(
                    obj, "preserveTransparencyMaxMipLevel",
                    ref refusal, out value))
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
            if (!TryReadInt(
                    obj, "preserveTransparencyMinTextureSize",
                    ref refusal, out value))
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
            if (!TryReadInt(obj, key, ref refusal, out value))
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
```

Notes for the implementer:
- `using UnityEngine;` is needed for the `Debug`-free file only if the analyzer demands it; remove it if unused.
- The `version != CurrentSchemaVersion` check runs after all reads so `UnknownSchemaVersion` never masks a structural refusal in the same file.
- `JTokenType.Integer` rejects `4.0` on purpose: a preset stores whole numbers.

- [ ] **Step 3: Refresh, compile, run**

Refresh the dev editor instance, confirm zero compile errors, then run the EditMode tests by name list: `ValidSchemaParsesAllNineValues`, `BrokenJsonRefusesMalformed`, `JsonArrayRefusesMalformed`, `WrongSchemaVersionRefuses`, `UnknownTopLevelFieldRefuses`, `UnknownFeatureFieldRefuses`, `UnknownSettingFieldRefuses`, `MissingFieldRefuses`, `WrongValueTypeForSchemaVersionRefuses`, `EmptyNameRefuses`, `MipCapAboveTenRefuses`, `MipCapBelowMinusOneRefuses`, `NonPowerOfTwoTextureSizeRefuses`, `TextureSizeOfOneRefuses`, `TextureSizeAboveLimitRefuses`, `PercentAboveHundredRefuses`.
Expected: 16 tests, 16 passed, 0 failed. Record the run as characterization.

- [ ] **Step 4: Falsifier 2**

Temporarily remove `"allowDepthTestChange"` from `SettingKeys`. Run the two named tests.
Expected: `ValidSchemaParsesAllNineValues` FAILS with refusal `UnknownField`, and `UnknownSettingFieldRefuses` PASSES for the wrong reason (it asserts `UnknownField`, which the mutation still produces, so it no longer proves the setting-key closure specifically). Record the observation, restore `SettingKeys`, and rerun.
Expected: 2 tests, 2 passed.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetParserTests.cs
git commit -m "feat: parse preset files against the closed schema"
```

---

### Task 4: Shipped preset files and the file store

**Files:**
- Create: `Packages/com.alrauna.amuse/Presets/safe.json`
- Create: `Packages/com.alrauna.amuse/Presets/normal.json`
- Create: `Packages/com.alrauna.amuse/Presets/aggressive.json`
- Create: `Packages/com.alrauna.amuse/Editor/Presets/PresetFileStore.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetFileStoreTests.cs`

**Interfaces:**
- Consumes: `PresetParser.TryParse` from Task 3.
- Produces: `internal static bool PresetFileStore.TryLoadAll(out List<OptimizerPreset> presets, out string failedFile, out PresetLoadRefusal refusal)`. On true, `presets` holds exactly three records in the order safe, normal, aggressive, and `refusal` is `None`. On false, `failedFile` names the file without folder and `presets` may hold the earlier files. Task 6 consumes this signature.

- [ ] **Step 1: Create the three files**

`safe.json`:

```json
{
  "schemaVersion": 1,
  "name": "Safe",
  "description": "Checks every tested texture size and keeps every tolerance at its safest value.",
  "features": {
    "alphaSeparator": true
  },
  "settings": {
    "preserveTransparencyMaxMipLevel": 4,
    "preserveTransparencyMinTextureSize": 128,
    "minimumOpaqueCoveragePercent": 25,
    "minimumOpaqueAlphaPercent": 100,
    "polygonAlphaUpperClampPercent": 100,
    "polygonMinimumOpaqueCoveragePercent": 100,
    "allowDepthTestChange": true,
    "ignoreOutOfRangeMaterialSlots": true
  }
}
```

`normal.json` and `aggressive.json`: identical values, different name and description. `normal.json`: `"name": "Normal"`, `"description": "The same safe checks for now. A place for the default balance later."`. `aggressive.json`: `"name": "Aggressive"`, `"description": "The same safe checks for now. A place for faster checks later."`.

- [ ] **Step 2: Implement the store**

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Presets
{
    /// <summary>
    /// Loads the three shipped preset files from the installed package.
    /// The file names are fixed, so "safe" always means safe.json. The
    /// folder resolves from the package metadata, the same pattern the
    /// header uses for the version, so no path is ever built by hand.
    /// </summary>
    internal static class PresetFileStore
    {
        internal static readonly string[] FileNames =
            { "safe", "normal", "aggressive" };

        internal static bool TryLoadAll(
            out List<OptimizerPreset> presets,
            out string failedFile,
            out PresetLoadRefusal refusal)
        {
            presets = new List<OptimizerPreset>();
            failedFile = null;
            refusal = PresetLoadRefusal.None;

            var folder = PresetsFolder();
            if (folder == null)
            {
                failedFile = "Presets";
                refusal = PresetLoadRefusal.FileMissing;
                return false;
            }

            foreach (var name in FileNames)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    folder + "/" + name + ".json");
                if (asset == null)
                {
                    failedFile = name + ".json";
                    refusal = PresetLoadRefusal.FileMissing;
                    return false;
                }
                if (!PresetParser.TryParse(
                        asset.text, out var preset, out refusal))
                {
                    failedFile = name + ".json";
                    return false;
                }
                presets.Add(preset);
            }
            return true;
        }

        private static string PresetsFolder()
        {
            var info = PackageInfo.FindForAssembly(
                typeof(PresetFileStore).Assembly);
            return info == null ? null : info.assetPath + "/Presets";
        }
    }
}
```

- [ ] **Step 3: Write the shipped-data test**

```csharp
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
```

- [ ] **Step 4: Refresh, compile, run**

Refresh the dev editor instance. Confirm Unity generated `.meta` files beside the three JSON files. Confirm zero compile errors, then run the EditMode filter `TheThreeShippedFilesParseInTheFixedOrder;EveryShippedFileHoldsTheShippedDefaults`.
Expected: 2 tests, 2 passed, 0 failed. Record the observed counts.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Presets Packages/com.alrauna.amuse/Editor/Presets/PresetFileStore.cs Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetFileStoreTests.cs
git commit -m "feat: ship the three preset files and load them from the package"
```

---

### Task 5: The applier

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Presets/PresetApplier.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetApplierTests.cs`

**Interfaces:**
- Consumes: `OptimizerPreset` from Task 2.
- Produces: `internal static void PresetApplier.Apply(OptimizerPreset preset, UnityEditor.SerializedObject serializedObject)`. The caller runs `serializedObject.Update()` first. The apply writes exactly the nine mapped fields and calls `ApplyModifiedProperties` once. Task 6 consumes this signature.

- [ ] **Step 1: Implement the applier**

```csharp
using UnityEditor;

namespace Alrauna.Amuse.Editor.Presets
{
    /// <summary>
    /// Writes a preset onto the component through the serialized
    /// object, so Undo and Reset treat the change like every other
    /// control. The apply covers exactly the nine mapped fields and
    /// never the master switch or the inspector-only reveal flag.
    /// </summary>
    internal static class PresetApplier
    {
        internal static void Apply(
            OptimizerPreset preset,
            SerializedObject serializedObject)
        {
            serializedObject.FindProperty("_alphaSeparatorEnabled")
                .boolValue = preset.AlphaSeparatorEnabled;
            serializedObject.FindProperty(
                    "_preserveTransparencyMaxMipLevel")
                .intValue = preset.PreserveTransparencyMaxMipLevel;
            serializedObject.FindProperty(
                    "_preserveTransparencyMinTextureSize")
                .intValue = preset.PreserveTransparencyMinTextureSize;
            serializedObject.FindProperty("_minimumOpaqueCoveragePercent")
                .intValue = preset.MinimumOpaqueCoveragePercent;
            serializedObject.FindProperty("_minimumOpaqueAlphaPercent")
                .intValue = preset.MinimumOpaqueAlphaPercent;
            serializedObject.FindProperty(
                    "_polygonAlphaUpperClampPercent")
                .intValue = preset.PolygonAlphaUpperClampPercent;
            serializedObject.FindProperty(
                    "_polygonMinimumOpaqueCoveragePercent")
                .intValue = preset.PolygonMinimumOpaqueCoveragePercent;
            serializedObject.FindProperty("_allowDepthTestChange")
                .boolValue = preset.AllowDepthTestChange;
            serializedObject.FindProperty("_ignoreOutOfRangeMaterialSlots")
                .boolValue = preset.IgnoreOutOfRangeMaterialSlots;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
```

- [ ] **Step 2: Write the apply test**

```csharp
using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Runtime;
using Alrauna.Amuse.Editor.Presets;

namespace Alrauna.Amuse.Tests.Editor
{
    public sealed class PresetApplierTests
    {
        [Test]
        public void ApplyWritesAllNineFields()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();

                // Move every field away from the shipped values first,
                // so the apply must write each one back.
                var serialized = new UnityEditor.SerializedObject(optimizer);
                serialized.FindProperty("_alphaSeparatorEnabled")
                    .boolValue = false;
                serialized.FindProperty("_preserveTransparencyMaxMipLevel")
                    .intValue = 1;
                serialized.FindProperty("_preserveTransparencyMinTextureSize")
                    .intValue = 256;
                serialized.FindProperty("_minimumOpaqueCoveragePercent")
                    .intValue = 50;
                serialized.FindProperty("_minimumOpaqueAlphaPercent")
                    .intValue = 90;
                serialized.FindProperty("_polygonAlphaUpperClampPercent")
                    .intValue = 80;
                serialized.FindProperty(
                        "_polygonMinimumOpaqueCoveragePercent")
                    .intValue = 70;
                serialized.FindProperty("_allowDepthTestChange")
                    .boolValue = false;
                serialized.FindProperty("_ignoreOutOfRangeMaterialSlots")
                    .boolValue = false;
                serialized.ApplyModifiedProperties();

                var preset = OptimizerPresetTests.ShippedDefaults();
                serialized.Update();
                PresetApplier.Apply(preset, serialized);

                Assert.That(optimizer.AlphaSeparatorEnabled, Is.True);
                Assert.That(optimizer.PreserveTransparencyMaxMipLevel,
                    Is.EqualTo(4));
                Assert.That(optimizer.PreserveTransparencyMinTextureSize,
                    Is.EqualTo(128));
                Assert.That(optimizer.MinimumOpaqueCoveragePercent,
                    Is.EqualTo(25));
                Assert.That(optimizer.MinimumOpaqueAlphaPercent,
                    Is.EqualTo(100));
                Assert.That(optimizer.PolygonAlphaUpperClampPercent,
                    Is.EqualTo(100));
                Assert.That(optimizer.PolygonMinimumOpaqueCoveragePercent,
                    Is.EqualTo(100));
                Assert.That(optimizer.AllowDepthTestChange, Is.True);
                Assert.That(optimizer.IgnoreOutOfRangeMaterialSlots,
                    Is.True);
                Assert.That(optimizer.AmuseDisabled, Is.False,
                    "a preset never touches the master switch");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
```

- [ ] **Step 3: Falsifier 3**

Temporarily delete the `_polygonAlphaUpperClampPercent` write from `Apply`. Run the filter `ApplyWritesAllNineFields`.
Expected: FAIL on the polygon clamp assertion. Restore the write and rerun.
Expected: 1 test, 1 passed.

- [ ] **Step 4: Refresh, compile, run**

Refresh the dev editor instance, confirm zero compile errors, then run the filter `ApplyWritesAllNineFields`.
Expected: 1 test, 1 passed, 0 failed. Record the observed counts.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Presets/PresetApplier.cs Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetApplierTests.cs
git commit -m "feat: apply a preset through one serialized object write"
```

---

### Task 6: The preset row in the inspector

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`

**Interfaces:**
- Consumes: `PresetFileStore.TryLoadAll`, `OptimizerPreset.Matches`, `PresetApplier.Apply` from tasks 2 to 5.
- Produces: no API. Verification is compile, suite, and an editor smoke check.

- [ ] **Step 1: Add the state fields and the using**

Add `using System.Collections.Generic;` and `using Alrauna.Amuse.Editor.Presets;` at the top of the file. Beside the foldout fields, add:

```csharp
        private List<OptimizerPreset> _presets;
        private string _presetProblemFile;
        private PresetLoadRefusal _presetProblem;
```

- [ ] **Step 2: Draw the row**

In `OnInspectorGUI`, call `DrawPresetRow();` directly after `DrawHeader();` and before the placement help box. Add the method:

```csharp
        /// <summary>
        /// The preset row. One button per shipped preset file, in the
        /// fixed order safe, normal, aggressive. A button presses only
        /// while the component matches that preset on all nine fields,
        /// so the pressed state is always honest and never stored. A
        /// broken preset file turns the row off with a warning,
        /// because a row over a partial preset universe could claim a
        /// match it cannot prove.
        /// </summary>
        private void DrawPresetRow()
        {
            if (_presets == null)
            {
                PresetFileStore.TryLoadAll(
                    out _presets, out _presetProblemFile,
                    out _presetProblem);
            }
            if (_presets == null
                || _presetProblem != PresetLoadRefusal.None)
            {
                EditorGUILayout.HelpBox(
                    "The preset file " + _presetProblemFile + " is not " +
                    "valid: " + _presetProblem + ". The preset row " +
                    "stays off. Fix the file or reinstall the package.",
                    MessageType.Warning);
                return;
            }

            var component = (AmuseAvatarOptimizer)target;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    "Presets", EditorStyles.boldLabel, GUILayout.Width(50));
                foreach (var preset in _presets)
                {
                    var matched = preset.Matches(component);
                    var clicked = GUILayout.Toggle(
                        matched,
                        new GUIContent(preset.Name, preset.Description),
                        GUI.skin.button);
                    if (clicked && !matched)
                    {
                        serializedObject.Update();
                        PresetApplier.Apply(preset, serializedObject);
                    }
                }
            }
        }
```

Implementer notes:
- `GUILayout.Toggle` answers the new visual state. Clicking the pressed button answers false, and the `clicked && !matched` guard makes that a no-op instead of a second apply.
- The files load once per editor instance. Package files do not change under a loaded editor in practice; a script recompilation builds a fresh editor and reloads them.
- The row draws whether or not "Disable AMUSE" is checked. Applying while disabled stores values only; the build stays off.

- [ ] **Step 3: Compile and run the full suite**

Refresh the dev editor instance, confirm zero compile errors, then run the full `Alrauna.Amuse.Tests.Editor` EditMode suite.
Expected: every test passes, 0 failed. Record the observed counts. Then run the `Alrauna.Amuse.Research.Tests.Editor` suite.
Expected: unaffected, 0 failed.

- [ ] **Step 4: Smoke check on the dev editor instance**

Following the Unity MCP safety gate, pin the instance and select an avatar root that carries `AmuseAvatarOptimizer`.
Expected: the row sits under the header and above the placement help box. All three buttons press together, because the three files hold identical values. Click "Normal": the pressed state stays on all three and the values equal the defaults. Change one policy slider: no button presses. Click "Safe": every slider returns to the shipped default. Undo returns the previous values.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs
git commit -m "feat: draw the preset row under the optimizer header"
```

---

### Task 7: Documentation and final validation

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: everything shipped by tasks 1 to 6.
- Produces: none.

- [ ] **Step 1: Add the README bullet**

In the "Use" section beside the existing 0.1.0 bullets, add:

```markdown
- The preset row under the header picks Safe, Normal, or Aggressive. All
  three presets ship the same values for now, the shipped defaults.
```

- [ ] **Step 2: Full validation**

Run the full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` EditMode suites.
Expected: 0 failed across both. Record the observed counts. Run `git diff --check` for whitespace. Inspect the full working-tree diff and confirm only the intended files changed.

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: note the preset row in the use section"
```
