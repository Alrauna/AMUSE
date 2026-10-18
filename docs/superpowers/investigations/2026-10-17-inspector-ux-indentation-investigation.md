# Inspector Child-Menu Indentation, Out-of-Range Toggle Relabel, and Cutout Mip Defaults Investigation

Date: 2026-10-17

Base: `main` at `15a8db7`. Work branch: `feat/inspector-ux-indentation`.

Privacy note: this investigation read only product code in this repository. It read no private avatar data and no Census data.

This document records facts about the current code. It changes nothing.

Evidence labels:

- `[SOURCE]` A fact read in this repository at the base commit. The citation gives a repository-relative path and line numbers.
- `[INFERENCE]` A deduction from listed facts. The deduction is named.
- `[RECOMMENDATION]` A proposed next step for the design round.

## 1. Purpose

The owner asked for three inspector changes:

1. Small indentations for every set of menu items that is a child of a submenu or is enabled by another button. The named cases: the Alpha Separator menu inside Show Material Transformations, all items in Settings, again the items spawned by the Advanced Settings toggle, and again the items inside Alpha Separator Settings.
2. The drawn label "Ignore Out-of-Range Material Slots" becomes "Allow Out-of-Range Material Slots".
3. The default, Safe, and Normal mip cap to test for cutout becomes 3. The Aggressive cutout mip cap becomes 2.

This document maps each request to the code it touches, records every pinned value and test, and names the open decisions for the design round.

## 2. The owner's words map to code families

| Owner phrase | Code family | Anchor |
|---|---|---|
| Alpha Separator menu inside Show Material Transformations | `DrawMaterialTransformations` body in the inspector editor | (`Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:199-341`) |
| All of the items in Settings | `DrawSettings` body | (`Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:1182-1240`) |
| Items spawned by the Advanced Settings toggle | The `if (reveal.boolValue)` block in `DrawSettings`, plus the `DrawAlphaSeparatorSettings` call it gates | (`Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:1208-1236` and `:178-181`) |
| Items inside Alpha Separator Settings | `DrawAlphaSeparatorSettings` with its Transparent and Cutout submenus and the acceptance group | (`Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:785-936`) |
| "Allow Out-of-Range Material Slots" label | Drawn `GUIContent` over the serialized bool `_ignoreOutOfRangeMaterialSlots` | (`Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:1210-1217`, `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs:201-203`) |
| Cutout mip defaults 3, 3, 3, 2 | Field initializer `_cutoutPreserveTransparencyMaxMipLevel`, the three shipped preset files, the cutout tooltip sentence, the fresh-default mechanism, and the value-pinning tests | (`Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs:138-140`, `Packages/com.alrauna.amuse/Presets/*.json:16`) |

## 3. The inspector surface at base `15a8db7`

### 3.1 Draw order and foldouts

`[SOURCE]` `OnInspectorGUI` draws, in order (`Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:142-187`): the header (`:144`), the preset row (`:145`), a root-placement HelpBox (`:149-165`), the top-level "Alpha Separator" toggle (`:166-174`, property `_alphaSeparatorEnabled`), `DrawMaterialTransformations()` (`:176`), then `if (DrawSettings()) { DrawAlphaSeparatorSettings(); }` (`:178-181`), then the build-status HelpBox (`:183-186`).

`[SOURCE]` All six foldouts use `EditorStyles.foldoutHeader` and fire `OnFoldoutHeaderDrawnForTest` with their exact header string. Fire sites: `:205`, `:234`, `:790`, `:818`, `:846`, `:1186`.

| # | Header | Draw site | Open-state storage | Default |
|---|---|---|---|---|
| 1 | "Show Material Transformations" | `:201-205` | field `_materialTransformationsOpen` (`:38`) | false |
| 2 | "Alpha Separator" (nested in 1) | `:230-234` | field `_alphaSeparationTransformationsOpen` (`:39`) | true |
| 3 | "Settings" | `:1184-1186` | field `_settingsOpen` (`:21`) | false |
| 4 | "Alpha Separator Settings" | `:787-790` | field `_alphaSeparatorOpen` (`:22`) | true |
| 5 | "Transparent" (nested in 4) | `:815-818` | field `_transparentSettingsOpen` (`:30`) | true |
| 6 | "Cutout" (nested in 4) | `:843-846` | field `_cutoutSettingsOpen` (`:31`) | true |

`[SOURCE]` No foldout state is serialized. The only serialized UI-state value is `_advancedSettingsRevealed` on the component, default false (`Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs:211-213`).

### 3.2 The Settings foldout

`[SOURCE]` `DrawSettings` (`:1182-1240`) draws, in order:

| Control | Property | Lines | Gated by Advanced Settings |
|---|---|---|---|
| "Disable AMUSE" | `_amuseDisabled` | `:1192-1197` | no |
| "Advanced Settings" | `_advancedSettingsRevealed` | `:1199-1207` | no; it is the toggle |
| "Ignore Out-of-Range Material Slots" | `_ignoreOutOfRangeMaterialSlots` | `:1210-1217` | yes (`:1208-1209`) |
| "Allow Depth Test Change on Moved Triangles" | `_allowDepthTestChange` | `:1219-1236` | yes |

`[SOURCE]` The method ends with `serializedObject.ApplyModifiedProperties()` (`:1238`) and returns `reveal.boolValue` (`:1239`). That return value is the sole gate for `DrawAlphaSeparatorSettings` (`:178-181`). `[SOURCE]` The ambient `EditorGUI.indentLevel` is 0 across the whole top-level flow. The complete indent-use list is in section 4.1.

### 3.3 Alpha Separator Settings

`[SOURCE]` `DrawAlphaSeparatorSettings` (`:785-805`) draws only when the advanced reveal is on. It computes the widest row label with `EditorStyles.label.CalcSize` and sets `EditorGUIUtility.labelWidth` to that width plus 2, then restores it (`:796-803`). The body draws the Transparent submenu (`:800`, body `:813-833`), the Cutout submenu (`:801`, body `:841-862`), then the acceptance group (`:802`, body `:873-936`), matching the stated order at `:781-783`. The Transparent and Cutout submenus each draw a mip-cap popup, a texture-size popup, and coverage sliders. Transparent additionally draws the alpha policy sliders (`:829-832`). The acceptance group draws two anisotropy PropertyFields, the active-sampling proof label (`:906-908`), and conditional HelpBoxes (`:873-936`).

### 3.4 Show Material Transformations

`[SOURCE]` `DrawMaterialTransformations` (`:199-341`) draws the foldout header (`:201-205`) and a "Refresh" button (`:217-228`), then the nested "Alpha Separator" foldout (`:230-238`) and two disclosure sentences (`:240-245`). Then exactly one branch draws:

| Branch | Lines | Notables |
|---|---|---|
| Play-mode record rows | `:260-288` | early `return` at `:287` |
| Dry-run creation block | `:290-299` | creates `_dryRun` when null |
| Cancelled branch | `:301-313` | "Compute" button `:306`, `return` `:312` |
| Computing branch | `:315-330` | "Cancel" button `:323`, `return` `:329` |
| Finished dry-run rows | `:332-340` | method closes at `:341` |

`[SOURCE]` Rows draw through `DrawTransformationRow` (`:351-425`). The slot-row layout deliberately compensates for the ambient indent: `halfWidth` subtracts `EditorGUI.indentLevel * 15` before splitting the row in two (`:394-395`).

## 4. Indentation mechanics at the base

### 4.1 Existing indent use

`[SOURCE]` The complete list of `EditorGUI.indentLevel` uses in the editor file, verified by a full-file sweep:

| Lines | Code |
|---|---|
| `:394-395` | `int indentOffset = EditorGUI.indentLevel * 15;` then the half-width computation |
| `:410-414` | save, force to 0, restore around the slot material ObjectField |
| `:417-419` | `EditorGUI.indentLevel++` and `--` around the slot sentence label |

`[SOURCE]` The house pattern is manual inline arithmetic. No custom indent scope type exists anywhere in the package, and Unity's built-in `EditorGUI.IndentLevelScope` is unused (repo-wide grep returns zero matches).

### 4.2 Which controls respond to ambient indent

`[SOURCE]` Every affected control is `EditorGUILayout` auto-layout, which shifts and shrinks with `EditorGUI.indentLevel`: the shared mip-cap popup and texture-size popup helpers (`:961-964`, `:1007-1010`, each serving both arms), every percent slider (`:1032-1035`), every toggle PropertyField (`:167`, `:879-880`, `:1192`, `:1201`, `:1210`, `:1219`), the proof label (`:906-908`), the out-of-band warning HelpBoxes (`:950-956`, `:986-993`, `:1023-1029`), and the fixed-width "Compute", "Cancel", and "Presets" controls, whose x-origin shifts while their widths stay fixed. The slot-row material ObjectField is indent-immune by design (`:410-414`), and the slot-row width formula stays correct under a raised ambient indent (`:394-395`).

`[SOURCE]` `DrawAlphaSeparatorSettings` computes `EditorGUIUtility.labelWidth` from label text only (`:796-799`). The widest labels measure roughly 270 to 290 px (`:535-537`, `:550-552`, `:706-708`, `:722-724`). `[INFERENCE]` At depth 3 an auto-layout row loses 45 px at the house constant of 15 px per level. In a docked pane near 300 to 330 px the row width at depth 3 falls to roughly 255 to 285 px, at or below the fixed label width. The value column then reaches zero width, so the popups and sliders become uninteractable. The named assumptions are the pane width and the measured label widths. The design round must make the label width indent-aware or cap it by the available width. This risk is recorded as open point 3.

### 4.3 Early returns inside the affected drawers

`[SOURCE]` The drawers that must indent their bodies contain early returns: `DrawMaterialTransformations` returns while closed (`:206-209`), again while the Alpha Separator foldout is closed (`:235-238`), and inside the Play-record branch (`:287`); `DrawSettings` returns while closed (`:1189`); the Transparent and Cutout submenu drawers return while closed (`:821`, `:849`). `[INFERENCE]` A manual `EditorGUI.indentLevel++` before those returns and a matching `--` on every path is error-prone. Unity's built-in `EditorGUI.IndentLevelScope` is an `IDisposable` that restores the level on scope exit, so a `using` scope survives every early return. The named assumption is that `EditorGUI.IndentLevelScope` exists in Unity 2022.3. It is documented 2022.3 API, it is unused in this package, and the first execution task proves it by compiling and running the focused tests.

### 4.4 Indent depth

`[SOURCE]` The package's own width math treats one indent level as 15 px (`:394`). `[INFERENCE]` "Small indentations" is one level per nesting step, which the existing constant already defines.

## 5. The out-of-range toggle at the base

### 5.1 Storage and drawing

`[SOURCE]` `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs:201-203` declares `[ToggleLeft] [SerializeField] private bool _ignoreOutOfRangeMaterialSlots = true;`. The field has no `[Tooltip]` attribute and no doc comment. The ignore-semantics prose lives on the accessor doc comment at `:314-319`. The default true is pinned by `DefaultIgnoreOutOfRangeMaterialSlotsIsTrue` (`Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs:35-46`).

`[SOURCE]` The editor draws it with `EditorGUILayout.PropertyField` and an explicit `GUIContent` (`Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:1210-1217`). Because the field carries `[ToggleLeft]`, the property drawer `ToggleLeftDrawer` (`Packages/com.alrauna.amuse/Editor/ToggleLeftDrawer.cs:15-28`) draws it as `EditorGUI.ToggleLeft` inside `EditorGUI.BeginProperty` and `EndProperty`, which keeps the prefab-override menu and display (`BeginProperty` `:18`, `ToggleLeft` `:20-21`, `EndProperty` `:27`).

`[SOURCE]` Drawn label, exact (`:1214`): `Ignore Out-of-Range Material Slots`. Drawn tooltip, verbatim (`:1215-1217`):

> Some animation files animate material slots that do not exist on this mesh. By default, AMUSE safely ignores these extra slot animations and optimizes the valid slots. Turn this setting off to refuse meshes with extra slot animations.

### 5.2 Semantics of a relabel

`[SOURCE]` The stored value means "ignore" (true ignores out-of-range slot animations). `[INFERENCE]` The requested label "Allow Out-of-Range Material Slots" states the same behavior from the opposite side: stored true must draw checked under the new label. The change is therefore a display inversion over the same serialized bool: draw the negation and write back the negation. The named assumption is Unity serialization behavior: a fresh component and every existing scene keep their stored values, so no avatar's build behavior changes. Renaming and re-semanting the stored field instead would flip every scene that stored false, because the old value could not migrate into an inverted field without extra serialization logic. `[SOURCE]` The three preset files store the ignore semantics (`ignoreOutOfRangeMaterialSlots` false for Safe, true for Normal and Aggressive at `Presets/*.json:21`), and `OptimizerPreset.Matches` compares the field (`Packages/com.alrauna.amuse/Editor/Presets/OptimizerPreset.cs:110-111`). A display relabel touches none of them.

`[SOURCE]` `PropertyField` sets `EditorGUI.showMixedValue` from `property.hasMultipleDifferentValues` before the drawer runs. `ToggleLeft` renders the mixed dash when that flag is set. `[INFERENCE]` A hand-drawn inverted toggle must set and restore `EditorGUI.showMixedValue` itself, or multi-object selection loses the mixed-value display and a click writes the negation to all targets without the visual cue. `[RECOMMENDATION]` Wrap the hand-drawn toggle in `EditorGUI.BeginProperty` and `EndProperty` with `EditorGUI.ToggleLeft` and explicit mixed-value handling, exactly matching what `PropertyField` plus `ToggleLeftDrawer` provide today, instead of adding a second property drawer type.

### 5.3 Occurrence sweep

`[SOURCE]` The human-readable phrase appears in the drawn label (`Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:1214`) and in README preset prose (`README.md:21-23`, the sentence "It still disables **Ignore Out-of-Range Material Slots** and **Allow Depth Test Change on Moved Triangles**"). `[SOURCE]` No test asserts the label or tooltip string. A sweep over `Packages/com.alrauna.amuse/Tests/` returns zero hits for the label text. The nearest text is a comment about the Advanced Settings reveal at `AmuseAvatarOptimizerEditorTests.cs:158`. The `"Smallest Tested Mipmap"` literals at `:212` and `:244` are arguments to `OutOfBandWarning`, not label assertions.

`[SOURCE]` The identifier `ignoreOutOfRangeMaterialSlots` and its Pascal variant appear across the editor file, the runtime component, the preset code and JSON, `Editor/Build`, `Editor/DryRun`, tests, and docs. A display-only relabel touches none of them. Historical records under `docs/superpowers/` keep the old wording and are never retro-edited (`docs/superpowers/README.md`).

`[SOURCE]` The Safe preset description prose (`Presets/safe.json:4`) says Safe "does not allow slot or depth-test exceptions", which stays true under the new label, because Safe leaves the Allow checkbox off.

## 6. The cutout mip cap at the base

### 6.1 Storage and the fresh-component problem

`[SOURCE]` `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs:139-140` declares `_cutoutPreserveTransparencyMaxMipLevel = 4`. Its doc comment states "The default of 4 matches the transparent setting." (`:134-135`). The opacity arm `_preserveTransparencyMaxMipLevel = 4` (`:35-43`) is out of scope and stays 4. `[SOURCE]` There is no `Reset()` method in the file. `[SOURCE]` The legacy backfill copies the stored opacity values into the cutout fields whenever the serialized marker `_cutoutSettingsBackfilled` is false (`:366-381`, marker `:130`, copy `:373-374`).

`[SOURCE]` A payload that lacks the marker field deserializes the marker as false. A freshly added component also deserializes with the marker false, because a fresh component has no stored payload either. The repo's own test comments state this: "A first round trip runs the one-time upgrade backfill. This matches the engine, which backfills on the first save." (`Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs:443-449`, same pattern in `Tests/Editor/DryRun/DryRunCacheTests.cs:146-152`). `[INFERENCE]` The backfill cannot distinguish a legacy payload from a fresh component. An initializer-only change from 4 to 3 is therefore undone: at the first serialization round trip the backfill copies the opacity arm's 4 over the fresh cutout 3. `DefaultComponentMatchesTheShippedNormalPreset` masks this, because it never round-trips the component. The design round must pick a fresh-default mechanism. `[RECOMMENDATION]` Add a `Reset()` implementation. Unity calls `Reset()` only in the editor, when the component is first added or the user presses Reset, so it exactly identifies the fresh authoring case. `Reset()` sets the cutout fields to their new defaults and sets `_cutoutSettingsBackfilled` to true, so the backfill never overwrites them. Legacy payloads and stored payloads keep today's behavior exactly: legacy inherits the stored opacity values once, stored values are never touched. The named assumption is that `Reset()` fires on `AddComponent` in the editor, which the renamed default test in section 7.1 observes directly.

`[SOURCE]` The backfill reads stored values, not the initializer (`:373-374`), so upgraded payloads are unaffected by any initializer change (`[INFERENCE]`, assumption stated in the copy lines).

### 6.2 Shipped presets

`[SOURCE]` Files live under `Packages/com.alrauna.amuse/Presets/`, all at `schemaVersion` 4:

| File | `cutoutPreserveTransparencyMaxMipLevel` | opacity arm, for contrast |
|---|---|---|
| `Presets/safe.json:16` | 4 | 4 (`:9`) |
| `Presets/normal.json:16` | 4 | 4 (`:9`) |
| `Presets/aggressive.json:16` | 3 | 3 (`:9`) |

`[SOURCE]` `PresetParser.TryReadMipCap` accepts -1 and 0 through 10 (`Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs:263-281`, range check `:275`). The planned 3, 3, and 2 values are inside the range. The parser needs no change. `[SOURCE]` `PresetApplier.Apply` writes the sixteen mapped fields (`Packages/com.alrauna.amuse/Editor/Presets/PresetApplier.cs:15-63`), and `OptimizerPreset.Matches` compares all sixteen (`Packages/com.alrauna.amuse/Editor/Presets/OptimizerPreset.cs:84-116`).

`[INFERENCE]` Every component saved before the change keeps its stored cutout 4. After the change it no longer matches the shipped Normal preset (stored 4 against preset 3), so the preset row shows nothing pressed for existing avatars. The cutout tooltip's "The default of Mip 3" sentence describes new components. This is accepted owner-requested behavior and is recorded here so the design round states it.

### 6.3 Inspector popup and tooltips

`[SOURCE]` `DrawMipCapPopup` (`:939-969`) offers the fixed option list "All Mips", "Mip 0" through "Mip 10". Index 0 stores -1 (`:944-948`). The out-of-band band is -1 to 10 (`:1093-1094`). Both arms share the popup helper. Only the `GUIContent` differs. `[SOURCE]` The cutout tooltip embeds the default sentence at `:674-676`: "The default of Mip 4 fits most viewing distances." The opacity tooltip embeds the same sentence at `:508-511` and must not change. Only the cutout tooltip changes.

### 6.4 README and architecture doc

`[SOURCE]` `README.md:20-26` is the active user-facing prose. Two sentences are affected:

- "Safe turns on both anisotropy acceptance settings. It still disables **Ignore Out-of-Range Material Slots** and **Allow Depth Test Change on Moved Triangles** ..." (`:21-23`) uses the old label name.
- "Aggressive uses 10 percent and sets Smallest Tested Mipmap to Mip 3." (`:26`) is arm-ambiguous after the change. The opacity arm stays 3 while the cutout arm becomes 2.

`[SOURCE]` The active architecture doc carries the same arm-split risk: "The default alpha policy limits evidence to mip level 4 and a minimum size of 128 texture pixels." (`docs/architecture/vision.md:19`).

## 7. Test pins at the base

### 7.1 Cutout mip value pins

`[SOURCE]` The table lists every test that pins a planned value:

| Test | Anchor | Pins | Planned change |
|---|---|---|---|
| `ApplyingEachShippedPresetWritesItsCutoutValues` table | `Tests/Editor/Presets/PresetApplierTests.cs:104-106` | Safe 4, Normal 4, Aggressive 3 on the cutout arm, asserted `:149-152` | 3, 3, 2 |
| `ApplyWritesAllSixteenFields` | `PresetApplierTests.cs:242-243` | cutout 4, driven by `ShippedDefaults()` at `:235` | 3 |
| `ShippedDefaults()` helper | `Tests/Editor/Presets/OptimizerPresetTests.cs:14` | second positional 4 is the cutout arm. The first 4 is the opacity arm | second 4 becomes 3 |
| `FlippingAnyOneFieldValueBreaksTheMatch` flip table | `OptimizerPresetTests.cs:63` | flips cutout to 3 to break the match | must flip to a value that differs from the new preset value 3 |
| `DefaultCutoutPreserveTransparencyMaxMipLevelIs4` | `Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs:487-501` | default 4, name embeds the number | assert 3 and rename the test. This test also observes the fresh-default mechanism from 6.1 |
| `ValidSchemaParsesAllSixteenValues` and `ValidJson` | `Tests/Editor/Presets/PresetParserTests.cs:108-109`, `:22` | parses embedded cutout 4 | 3, and every `string.Replace` needle built on the cutout line must track the new number, or the helper silently no-ops |
| `ValidBody` in the file-store test | `Tests/Editor/Presets/PresetFileStoreTests.cs:28` | embedded cutout 4, Safe-shaped, parsed not asserted | sync to 3 for honesty. No assertion depends on it |

`[SOURCE]` The legacy-body builders in the parser tests strip the cutout key by matching the exact `"cutoutPreserveTransparencyMaxMipLevel": 4` text of `ValidJson`: `LegacyVersionTwoBody` (`:47-48`), `LegacyVersionOneBody` (`:64-65`), `LegacyVersionThreeBody` (`:83-84`), plus `WithCutoutMipCap` (`:354-359`). `[INFERENCE]` If `ValidJson` changes to 3 without them, each strip silently no-ops and the fixtures stop being realistic legacy bodies. All five needles must move with `ValidJson`.

`[SOURCE]` The opacity arm keeps every value. `ApplyingShippedAggressiveSetsSmallestTestedMipmapToThree` (`PresetApplierTests.cs:61-92`) asserts only `PreserveTransparencyMaxMipLevel` (`:79`, `:84-86`) and stays green.

### 7.2 Pins that must stay green

- `[SOURCE]` `PreserveTransparencyMipPolicyScopesTheProofEndToEnd` (`Tests/Editor/Build/AlphaSeparationPreparationTests.cs:4929-4986`) runs default-cap arms that pass `null` and therefore run at the component default cutout cap, which is 4 today (`:4951-4952`, `:4972-4973`). The fixture holes are real: a tail hole at mip 5 and an early hole at mip 2 (`:4942`, `:4949`). `[INFERENCE]` The arms tolerate a cap of 3, because 5 stays outside cap 3 and mip 2 stays inside it. The named assumption is the mip arithmetic of the fixture. The test doc comment at `:4917-4919` hard-codes "through mip 4" prose and should be synced when the default flips.
- `[SOURCE]` `OutOfBandCutoutMipCapStaysAndWarns` pins the -1 to 10 band, not the default (`Tests/Editor/AmuseAvatarOptimizerEditorTests.cs:223-252`).
- `[SOURCE]` Backfill tests pin copy-from-stored-opacity semantics with explicit values 7 and 9 (`Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs:338-434`). These must stay green under any fresh-default mechanism.
- `[SOURCE]` `DefaultComponentMatchesTheShippedNormalPreset` (`OptimizerPresetTests.cs:33-51`) breaks unless the fresh default, `normal.json`, and `ShippedDefaults()` change in the same commit step.

### 7.3 UI label and layout pins

`[SOURCE]` No test asserts any drawn control label or tooltip string. Five foldout header strings are asserted: "Show Material Transformations", "Alpha Separator", and not-"Alpha Separation" (`AmuseAvatarOptimizerEditorTests.cs:143-145`), then "Alpha Separator Settings", "Transparent", "Cutout" (`:174-179`). The "Settings" header string is unasserted. The "Compute" and "Cancel" button strings are asserted at `:732-735` and `:716` inside `CancelledDryRunDisplaysCancelledMessageAndComputeButton` (`:677-743`). `[INFERENCE]` A display relabel and pure indentation break no existing assertion. The named assumption is that the host-window repaint tests assert which controls draw, not where they draw.

## 8. Docs mentions

`[SOURCE]` Active docs to update: the two README sentences in 6.4, the architecture sentence in 6.4, the cutout tooltip sentence (`Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:674-676`), the field doc comment sentence "The default of 4 matches the transparent setting." (`Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs:134-135`), which becomes false when the cutout default diverges from the opacity default, and the fixture prose at `Tests/Editor/Build/AlphaSeparationPreparationTests.cs:4917-4919`.

`[SOURCE]` Historical records that mention the old label or the mip cap stay as written. For example `docs/superpowers/investigations/2026-09-10-alpha-separator-settings.md:21` carries the old label, and `docs/superpowers/specs/2026-10-16-alpha-separator-submenus-design.md:44,56,93` plus `docs/superpowers/plans/2026-10-16-alpha-separator-submenus-plan.md:43,194` carry the old cutout values. Records are dated and never retro-edited.

## 9. Candidate shape for the design round

`[RECOMMENDATION]`

1. Indentation: raise `EditorGUI.indentLevel` by one inside `using (new EditorGUI.IndentLevelScope())` around each child set: the whole body of Show Material Transformations (Refresh button, Alpha Separator foldout, and view), the whole body of Settings, the advanced-revealed block inside Settings, and the body of Alpha Separator Settings, with its foldout header itself riding the advanced-revealed level. Net depths: transformation view children 1, Settings always-on children 1, advanced-revealed children 2, Alpha Separator Settings header 2 and its contents 3.
2. Make the Alpha Separator Settings label width indent-aware, so the value column survives depth 3 in narrow panes (open point 3).
3. Relabel: replace the PropertyField for `_ignoreOutOfRangeMaterialSlots` with a BeginProperty-wrapped `EditorGUI.ToggleLeft` that draws `!property.boolValue`, restores on `EditorGUI.EndChangeCheck` with the negation, and handles `EditorGUI.showMixedValue`, with a rewritten tooltip in the Allow voice. Keep the serialized name and the stored semantics.
4. Defaults: fresh-component default 3 through a `Reset()` implementation that also sets the backfill marker, per 6.1. Preset files `safe.json` 4 to 3, `normal.json` 4 to 3, `aggressive.json` 3 to 2. Cutout tooltip sentence to the Mip 3 wording. Field doc comment sentence rewritten. The seven test pins in 7.1 updated, including the renamed runtime default test, the flip-table value, and all five Replace needles. README and architecture sentences rewritten.

## 10. Open points for the design round

1. Confirm the cumulative depth reading: Alpha Separator Settings contents at depth 3, not depth 2.
2. Confirm the Refresh button indents with the Show Material Transformations body rather than staying at depth 0.
3. The depth-3 label width policy: subtract the indent offset from the computed width, or cap the width by the available rect, and name the minimum value column.
4. The fresh-default mechanism: the `Reset()`-plus-marker recommendation in 6.1, or a named alternative, with the legacy upgrade behavior unchanged.
5. README wording: the Safe sentence in the Allow voice, and the Aggressive sentence split by arm. Also the architecture sentence split by arm.
6. The Allow-voice replacement tooltip for the relabeled toggle.
7. The flip-table replacement value in `FlippingAnyOneFieldValueBreaksTheMatch`. It must differ from 3. Candidate 4.
8. Whether `PresetFileStoreTests.ValidBody` syncs to 3. No assertion depends on it.
9. Exact replacement sentences for the cutout tooltip and the runtime field doc comment.

## 11. Verification record

- 2026-10-17: written at base `15a8db7` from three parallel read-only scouts (inspector draw flow, cutout mip policy, documentation conventions), spot-verified against the editor and runtime sources in this session.
- 2026-10-17: adversarial review by three parallel reviewers (fact verification, completeness, conventions). Findings: one CRITICAL (the legacy backfill also runs on fresh components, so an initializer-only default change is undone at the first round trip), two MAJOR (the hand-drawn toggle loses mixed-value display without explicit `EditorGUI.showMixedValue` handling, and the fixed label width can collapse the value column at depth 3 in narrow panes), three further MAJOR-or-MINOR fact corrections (three legacy-body Replace needles, the missing architecture-doc mention, the missing fixture-prose mention), two WRONG claims removed (a nonexistent runtime tooltip on the field, a wrong foldout-header assertion count), one branch-map rewrite in 3.4, and a citation and style pass (labels on factual sentences, repository-relative paths, front-matter spacing, no em-dashes or prose semicolons). All corrections are incorporated in this revision.
