# Quality presets backed by package data files: current-state characterization

Date: 2026-10-05. Branch: `feature/optimizer-ui-rework`, base `main` at
`f319186`.

Labels: `[SOURCE]` is a fact read in this repository or in public
optimizer source. `[INFERENCE]` is a deduction. `[RECOMMENDATION]` is a
proposed next step.

## 1. Purpose

The product owner asked for a preset selector in the component inspector
with three entries: "Safe", "Normal", and "Aggressive". Choosing a
preset sets the feature switches and the settings values of the
component. The preset definitions live in files inside the package, in
JSON or YAML. At first all three presets carry the same values, so only
the plumbing ships before the tuning work. The owner named
d4rkAvatarOptimizer's preset row as the visual reference and asked the
design not to inherit its mechanics when a better shape exists. This
note records the state and the decision space. It changes nothing.

## 2. The settings universe

`[SOURCE]`
`Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs` serializes
eleven fields. Two of them are out of scope for presets:

- `_amuseDisabled` is the master switch. A quality choice must never
  flip the master switch as a side effect.
- `_advancedSettingsRevealed` is inspector-only state. The build never
  reads it.

`[SOURCE]` One field is a feature switch: `_alphaSeparatorEnabled`,
default true. The inspector draws it at top level as "Alpha Separator".
The field's documentation says later features add sibling switches
beside it. The preset schema needs a home for those future switches from
the start.

`[SOURCE]` Eight fields are settings values: the mip cap (default 4),
the minimum texture size (default 128), the per-material coverage
(default 25), the per-texture alpha clamp (default 100), the per-polygon
alpha clamp (default 100), the per-polygon coverage (default 100),
`_allowDepthTestChange` (default true), and
`_ignoreOutOfRangeMaterialSlots` (default true). The inspector draws the
first six under "Alpha Separator Settings" and the last two under
"Advanced Settings" in the Settings foldout.

`[INFERENCE]` The shipped defaults are the conservative point: the two
tolerance pairs sit inert at 100. A "Safe" preset that copies the
defaults is a documented restore point. It cannot weaken the proof,
because the values are the same ones a fresh component carries.

## 3. The d4rkAvatarOptimizer reference

`[SOURCE]` Citations use the installed VPM dependency
`Packages/d4rkpl4y3r.d4rkavataroptimizer` at version 4.5.4. The public
repository at `https://github.com/d4rkc0d3r/d4rkAvatarOptimizer` holds
version 4.6.1 on its main branch. Both copies agree in the parts below.

`[SOURCE]` d4rk defines presets in code.
`Editor/d4rkAvatarOptimizer.cs` holds a private static list of name and
dictionary pairs named `SettingsPresets` at line 333 with three entries:
"Standard", "Shader Toggles", and "Aggressive". Each dictionary maps
`nameof(Settings.X)` keys to boxed values. Each preset covers a partial
subset of the settings fields.

`[SOURCE]` d4rk applies a preset by reflection. `SetPreset` at line 417
walks the dictionary and calls `typeof(Settings).GetField(key).SetValue`
on the per-component `public Settings settings` object. It then calls
`ApplyAutoSettings()`, which derives further fields from the chosen
values. The stored preset values are not always the final state.

`[SOURCE]` d4rk shows the active preset by partial match.
`IsPresetActive` at line 402 returns true when every field the preset
lists equals the live value. Fields the preset does not list are
ignored. Fields stored as int compare only against 0 and 1.

`[SOURCE]` The inspector draws a horizontal box under the header: a bold
"Presets" label and one `GUILayout.Toggle` with button style per preset.
Clicking applies the preset, clears UI caches, and marks the component
dirty. The "Shader Toggles" preset hides when the component reports no
custom shader support, so preset availability can be conditional.

`[SOURCE]` d4rk also gates combinations of settings with
`CanChangeSetting`, and `SetPreset` writes the listed fields without
consulting that gate. A separate editor window holds global defaults for
new components. Presets and global defaults are different mechanisms.

## 4. What the design should not copy

`[INFERENCE]` Five d4rk mechanics conflict with this repository's rules
or the request:

1. Presets as compiled C# contradict the request for data files.
2. Reflection over field names hides the mapping from the compiler and
   from tests. AMUSE convention uses closed typed maps and named
   refusal enums.
3. Partial matching can show a pressed button that does not describe the
   component. A total match over a closed field set is honest and cheap.
4. `ApplyAutoSettings` mutates more state after the apply. AMUSE has no
   auto layer. A preset apply must write exactly the file's values.
5. `SetPreset` bypasses the interactive `CanChangeSetting` gate. AMUSE
   settings are independent today. The design must not invent a
   dependency web just to mirror one.

## 5. File format and location

`[RECOMMENDATION]` JSON, not YAML. Unity exposes no public YAML parser.
Newtonsoft JSON is already in the project dependency closure:
`Packages/packages-lock.json` pins `com.unity.nuget.newtonsoft-json`
3.2.1, required by `nadena.dev.ndmf` and by
`com.anatawa12.avatar-optimizer`. The built-in `JsonUtility` cannot
support a fail-closed parser: it silently ignores unknown JSON members
and cannot tell an absent field from a default one. A preset file with a
typo must be refused, not half-applied. Newtonsoft rejects unknown
members and reports wrong types precisely.

`[SOURCE]`
`Packages/com.alrauna.amuse/Editor/Alrauna.Amuse.Editor.asmdef` does not
reference Newtonsoft yet. Direct use needs the assembly reference and an
explicit dependency entry in `package.json`. Both are intentional
dependency work under the repository rules.

`[RECOMMENDATION]` Put the files at
`Packages/com.alrauna.amuse/Presets/safe.json`, `normal.json`, and
`aggressive.json`, each with its `.meta` file. The release workflow zips
everything in the package except `Tests/*` and verifies that only test
content is gone, so a Presets folder ships to consumers. The loader
resolves the package folder with `PackageInfo.FindForAssembly`, the same
pattern the header uses for the version, and reads the files as
`TextAsset` through `AssetDatabase`. No absolute path is ever built.

`[INFERENCE]` Rejected alternatives: Unity's `UnityEngine.Presets.Preset`
assets serve import-time and drag contexts, not a component button that
must also cover feature switches. `StreamingAssets` is a player-build
concept and ships bytes into consumer builds. A user-editable folder
outside the package is a later feature and out of scope.

## 6. Schema sketch

`[RECOMMENDATION]` One closed schema, full coverage required:

```json
{
  "schemaVersion": 1,
  "name": "Safe",
  "description": "One sentence about the preset.",
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

`[RECOMMENDATION]` One internal map binds each schema key to the
serialized field name it writes. A closed refusal enum, for example
`PresetLoadRefusal`, names every rejection: unknown schema version,
unknown feature, unknown setting, missing field, wrong value type, and
value out of range. The selector draws the refusal text and applies
nothing. The loader binds each file by its fixed file name, so "safe"
always means `safe.json`. The `name` value is display text only.

## 7. The selector

`[RECOMMENDATION]` A dropdown, not a button row. Entries: "Safe",
"Normal", "Aggressive", and "Custom". Custom is computed, never stored:
the editor compares the live values of the nine preset-controlled fields
against every loaded preset. A total match names that preset. No match
shows "Custom". Place the popup under the header, above the placement
help box, so the choice is visible without opening Settings. The
inspector already uses popups for the mip cap and the texture size, so
the pattern exists.

`[INFERENCE]` While the three files hold identical values, all three
match the component after any of them is applied. The popup then shows
the last chosen name. That stays honest for now and costs nothing
later: when the files diverge, the same total-match rule picks the right
name.

`[INFERENCE]` A d4rk-style row of three toggle buttons would match the
reference visually. It costs three controls in one inspector row and a
pressed-state rule. The dropdown keeps one control and an explicit
Custom state. The owner asked for the better shape where one exists.

## 8. Behavior invariants

`[RECOMMENDATION]` Three invariants bind the design:

1. A preset apply writes only the nine fields the schema defines. It
   never touches `_amuseDisabled` or `_advancedSettingsRevealed`.
2. A preset apply produces a state the inspector could produce by hand:
   same fields, in-range values, one `SerializedObject`, one
   `ApplyModifiedProperties`, so Undo behaves like every other control.
3. Presets pick points on existing dials. They add no capability and
   weaken no proof. The classifier sees the same values it would see
   after a manual edit.

## 9. Testing seams

`[SOURCE]` The repository runs NUnit EditMode tests only. Test folders
mirror production folders. One test class covers one production type.

`[RECOMMENDATION]` Split the work into a pure loader and a thin IO
shell. The pure loader takes preset text and returns a record or a named
refusal. Tests feed it fixture JSON strings, the same shape as the
public synthetic JSON fixtures. One test reads the three shipped files
through `AssetDatabase` and asserts each parses with its expected name
and values, which pins the shipped data. The apply test creates a
component on a temporary object, as the runtime tests already do, and
asserts all nine fields after apply. A plausible wrong implementation,
for example a mapping that writes the mip cap but skips the polygon
clamp, fails that test.

## 10. Constraints the design must keep

`[SOURCE]` New labels and tooltips use plain technical English.
"Preset", "Safe", "Normal", "Aggressive", and "Custom" pass. The release
zip keeps a `Presets/` folder. The asmdef reference and the
`package.json` dependency entry are part of the preset slice. A new type
gets its own file with a matching name.

## 11. Decision, 2026-10-05

The owner decided the open points the same day:

- Selector shape: the d4rk-style row of three buttons, not the
  recommended dropdown. Section 7's recommendation is superseded. The
  pressed state stays computed and honest: a button presses only on a
  total match of all nine fields, so identical files press together.
- File format: JSON with Newtonsoft.
- The design lives in
  `docs/superpowers/specs/2026-10-05-quality-presets-design.md`.

## 12. Verification record

`[SOURCE]` On 2026-10-05 this note was checked against the working tree
and the public d4rk sources. Facts about d4rk were read from the
installed copy at version 4.5.4 and from the public main branch at
version 4.6.1. Both agree on the preset list, the reflection apply, the
partial match, the preset row, and the conditional availability. The
Newtonsoft version was read from the lockfile. No claim needed a
correction.
