# Quality Presets Design

Date: 2026-10-05. Base: `main` at `f319186`, branch
`feature/optimizer-ui-rework`.

Investigation:
`docs/superpowers/investigations/2026-10-05-quality-presets-investigation.md`.

## 1. Overview

The inspector gains a preset row under the header with three buttons:
"Safe", "Normal", and "Aggressive". Each button applies one preset
defined in a JSON file inside the package. The owner picked the
d4rk-style button row over a dropdown on 2026-10-05, and picked JSON
with Newtonsoft over YAML.

At first all three files hold the same values, the shipped defaults. The
slice ships the plumbing and the file format. The tuning of the three
levels is a later data-only change.

The presets write component values only. They change no classification,
no proof rule, and no mutation shape.

## 2. Preset data files

Location, fixed file names, fixed order:

1. `Packages/com.alrauna.amuse/Presets/safe.json`
2. `Packages/com.alrauna.amuse/Presets/normal.json`
3. `Packages/com.alrauna.amuse/Presets/aggressive.json`

Each file ships with its `.meta` file. The release zip excludes only
`Tests/*`, so the folder ships to consumers.

Schema, version 1, all fields required:

```json
{
  "schemaVersion": 1,
  "name": "Safe",
  "description": "One short sentence about the preset.",
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

The `name` value is display text. The loader binds each file by its
fixed file name, so "safe" always means `safe.json`.

All three files start with the values above. The `name` and
`description` values differ per file. "Safe" is the documented restore
point: its values equal the shipped component defaults.

## 3. Field mapping and value ranges

The preset controls nine serialized fields. It never touches
`_amuseDisabled` or `_advancedSettingsRevealed`.

| Schema key | Serialized field | Allowed value |
|---|---|---|
| `features.alphaSeparator` | `_alphaSeparatorEnabled` | bool |
| `settings.preserveTransparencyMaxMipLevel` | `_preserveTransparencyMaxMipLevel` | `-1`, or `0` to `10` |
| `settings.preserveTransparencyMinTextureSize` | `_preserveTransparencyMinTextureSize` | `-1`, or a power of two from `2` to `8192` |
| `settings.minimumOpaqueCoveragePercent` | `_minimumOpaqueCoveragePercent` | `0` to `100` |
| `settings.minimumOpaqueAlphaPercent` | `_minimumOpaqueAlphaPercent` | `0` to `100` |
| `settings.polygonAlphaUpperClampPercent` | `_polygonAlphaUpperClampPercent` | `0` to `100` |
| `settings.polygonMinimumOpaqueCoveragePercent` | `_polygonMinimumOpaqueCoveragePercent` | `0` to `100` |
| `settings.allowDepthTestChange` | `_allowDepthTestChange` | bool |
| `settings.ignoreOutOfRangeMaterialSlots` | `_ignoreOutOfRangeMaterialSlots` | bool |

The ranges match the inspector's own controls: the mip popup lists Mip 0
through Mip 10, and the size popup lists 2 through 8192. The stored
polygon clamp of 100 is the inert sentinel, so a preset may write 100
without running the inspector's draw-time normalization.

## 4. Parsing and refusals

The parser is pure: it takes preset text and answers a preset record or
a named refusal. It never applies a partial preset. The closed refusal
enum `PresetLoadRefusal` holds exactly these values:

- `None` — the load or parse succeeded. This is the zero value.
- `FileMissing` — the package does not contain the file.
- `MalformedJson` — the text is not valid JSON or not a JSON object.
- `UnknownSchemaVersion` — `schemaVersion` is present but not `1`.
- `MissingField` — a required field is absent.
- `UnknownField` — a key outside the closed schema is present.
- `WrongValueType` — a key holds the wrong JSON type, or a string field
  is empty.
- `ValueOutOfRange` — a number is outside its allowed range.

Parsing walks the JSON tree against the closed schema field by field.
The typed-object deserializer path is rejected: its exceptions cannot
separate `MissingField` from `UnknownField` from `WrongValueType`
without parsing exception text. Newtonsoft supplies the tree; the
closed key sets and ranges live in one internal map.

A broken file must never half-apply. The inspector answers a refused
load with a warning help box and draws no preset row at all.

## 5. Component state

No serialized field is added or changed. The pressed state is computed,
never stored:

- A preset button draws pressed when the live component equals that
  preset on all nine fields. The comparison reads the component's public
  properties, not pending serialized values.
- After a manual edit of any of the nine fields, no button draws
  pressed.
- While the three files hold identical values, all three buttons draw
  pressed once any of them matches. This is the honest state under
  "all three do the same thing for now". It disappears when the values
  diverge.

Saved avatars are unaffected. No migration runs.

## 6. Inspector

Draw order at the top of `OnInspectorGUI`:

1. Header, with the subtext line from the title subtext design.
2. The preset row.
3. The placement help box, unchanged.
4. Everything below, unchanged.

The preset row draws a bold "Presets" label and three toggle buttons
with button style, one per file in the fixed order. Each button's
tooltip is the file's `description` value. A click updates the
serialized object through one `SerializedObject.Update`, writes all
nine fields, and calls `ApplyModifiedProperties` once, so Undo behaves
like every other control. The row draws whether or not "Disable AMUSE"
is checked; applying a preset while disabled only stores values.

On a refused load the row is replaced by a warning help box in plain
English. It names the file and the refusal value and tells the user the
row is off until the file is fixed or the package is reinstalled.

## 7. Invariants

1. A preset apply writes only the nine mapped fields.
2. A preset apply produces a state the inspector could produce by hand:
   same fields, in-range values, one `SerializedObject`, one apply.
3. Presets pick points on existing dials. They add no capability and
   weaken no proof. The classifier sees the same values it would see
   after a manual edit.
4. A refused file applies nothing and disables the row.

## 8. Dependencies

- `Packages/com.alrauna.amuse/package.json` gains
  `"com.unity.nuget.newtonsoft-json": "3.2.1"` under `dependencies`.
  The package is already in the project closure through
  `nadena.dev.ndmf` and `com.anatawa12.avatar-optimizer`.
- `Packages/com.alrauna.amuse/Editor/Alrauna.Amuse.Editor.asmdef` gains
  the reference `"com.unity.nuget.newtonsoft-json"`.
- `Packages/manifest.json` stays unchanged. Unity resolves the embedded
  package's declared dependency on refresh.

## 9. New production types

Editor assembly only, folder `Packages/com.alrauna.amuse/Editor/Presets/`,
namespace `Alrauna.Amuse.Editor.Presets`, one type per file, internal:

| File | Type | Role |
|---|---|---|
| `PresetLoadRefusal.cs` | `enum PresetLoadRefusal` | the closed refusal values |
| `OptimizerPreset.cs` | `sealed class OptimizerPreset` | immutable record: name, description, the nine values, and `Matches(component)` |
| `PresetParser.cs` | `static class PresetParser` | `TryParse(text, out preset, out refusal)` |
| `PresetFileStore.cs` | `static class PresetFileStore` | resolves the package folder with `PackageInfo.FindForAssembly`, loads the three files as `TextAsset`, parses each |
| `PresetApplier.cs` | `static class PresetApplier` | writes the nine serialized properties and applies once |

## 10. Test strategy

- `PresetParserTests`: fixture JSON strings cover the valid schema and
  every refusal value, including one out-of-range case per ranged field
  and a non-power-of-two size. First green run is recorded as
  characterization. One falsifier proves the tests can fail: remove a
  settings key from the parser's accepted set, observe the matching
  tests fail, restore.
- `OptimizerPresetTests`: a default component matches the shipped
  "Safe" record on all nine fields. Flipping one value breaks the
  match. Recorded as characterization.
- `PresetFileStoreTests`: loads the three real shipped files through
  `AssetDatabase` and asserts the names and the value sets. This pins
  the shipped data. A filtered run reporting 0 tests is a failure.
- `PresetApplierTests`: applies the shipped "Safe" record to a
  component's serialized object and asserts all nine fields. Falsifier:
  delete one property write, observe the test fail, restore.
- The button row itself is compile, suite, and a smoke check on the dev
  editor instance, mirroring the 2026-09-10 plan.
- The full EditMode suite passes with zero failures. The research
  assembly runs too and is unaffected.

## 11. Documentation

The README "Use" section gains one bullet: the preset row with Safe,
Normal, and Aggressive, all three shipping the same values for now.

## 12. Out of scope

- The actual value tuning that separates Safe, Normal, and Aggressive.
  It is a data-only change once the schema ships.
- User-editable preset files outside the package.
- Preset entries for future feature switches. The `features` object
  already has the closed home for them.
