# Inspector UX Indentation, Out-of-Range Relabel, and Cutout Mip Defaults — Design

Date: 2026-10-17

Base: `main` at `15a8db7`. Work branch: `feat/inspector-ux-indentation`.

Investigation: `docs/superpowers/investigations/2026-10-17-inspector-ux-indentation-investigation.md`

Revision note: 2026-10-17, after a five-lens parallel adversarial review (accuracy against the investigation, safety and security, architectural purity, ponytail adherence, latent bugs). Incorporated: sentence-scoped documentation replacements, a full-field `Reset()` that preserves the context-menu Reset contract, an explicit field-initializer decision, the backfill-marker doc comment update, mixed-value restore hardened with try/finally, the four-needle count, the corrected decision mapping, the recorded existing-avatar consequence, label-width tests pinned to the existing editor test class, and the recorded ruling that the Alpha Separator view stays flat at depth 1.

## Overview

The owner asked on 2026-10-17 for three inspector changes: small indentations for every child set of menu items, a relabel of the out-of-range slots toggle to the Allow voice, and new cutout mip cap defaults. The change has three parts:

1. Indent four child regions of `AmuseAvatarOptimizerEditor` with Unity's built-in `EditorGUI.IndentLevelScope`, and make the Alpha Separator Settings label width indent-aware.
2. Redraw the `_ignoreOutOfRangeMaterialSlots` toggle in the Allow voice, inverting the drawn value over the unchanged stored bool.
3. Move the fresh-component cutout mip cap default from 4 to 3 through the field initializer plus a `Reset()` implementation, move the three shipped preset cutout values to 3, 3, and 2, and update every sentence and test that pins the old values.

No build pipeline, analysis, host, semantics, or report code changes. No serialized field name, type, or stored meaning changes.

## Two keys

The plan must not conflate these pairs.

1. Drawn value versus stored value. The toggle's stored bool means "ignore" (true ignores out-of-range slot animations). The new label "Allow Out-of-Range Material Slots" states the same behavior from the opposite side, so the drawn value is the negation of the stored value. Stored values never change shape or meaning. An implementation that renames or re-semants the stored field is wrong (Falsifier 2).
2. Fresh component versus legacy payload. The legacy backfill copies stored opacity values into the cutout fields whenever `_cutoutSettingsBackfilled` deserializes false. Fresh components also deserialize the marker as false, so the backfill cannot distinguish them and would overwrite a plain initializer change. The fresh default therefore ships through `Reset()`, which Unity calls only in the editor when the component is first added or the user presses Reset. Legacy upgrade behavior stays byte-identical (Falsifier 1).

## Decisions

Decisions 1, 2, 3, 4, 8, 9, 10, and 11 close the investigation's open points 1 through 9, with point 9 closed jointly in decisions 4 and 8. Decisions 5, 6, and 7 record owner-requested mechanics and their forced consequences. The owner's request of 2026-10-17 is the source for decisions 1, 2, and 5.

1. Cumulative depths (closes investigation open point 1). Show Material Transformations body children draw at depth 1. Settings always-on children draw at depth 1. Advanced-revealed children draw at depth 2. The Alpha Separator Settings foldout header draws at depth 2 and its contents at depth 3. The owner's "and then again" wording is read cumulatively. Ruling, recorded 2026-10-17 after the ponytail lens noted the grammar difference: the nested "Alpha Separator" view inside Show Material Transformations stays flat at depth 1, header and contents alike. The owner asked for one indentation for that menu and named no second level for it, while the Settings chain names each level explicitly. The owner's enumeration is exhaustive for that foldout.
2. The Refresh button indents with the Show Material Transformations body (closes open point 2). It is a child of that foldout. Leaving it at depth 0 would break the visual nesting of the body it belongs to.
3. Label width becomes indent-aware (closes open point 3). A new internal static helper `AlphaSeparatorLabelWidth(float widestLabelWidth)` returns `Mathf.Ceil(widestLabelWidth) + 2f - EditorGUI.indentLevel * 15f`. `DrawAlphaSeparatorSettings` calls it inside the depth-3 body scope, so it subtracts the full row indent (45 px at depth 3) and preserves today's label-to-value proportion at every depth. No floor is added, because today's formula has none and today already tolerates narrow panes at depth 0. The helper is extracted exactly so the proportion is unit-testable.
4. Fresh-default mechanism (closes open point 4, jointly with decision 8 for open point 9). Two runtime changes pin the fresh default.
   - The field initializer moves: `_cutoutPreserveTransparencyMaxMipLevel = 4` becomes `= 3`. The initializer and `Reset()` must agree, so the declared default and the factory default cannot drift apart. The initializer alone is not the mechanism, because the backfill overwrites it at the first serialization round trip (Falsifier 1).
   - Add `private void Reset()` to `AmuseAvatarOptimizer`. It assigns every serialized field its declared default, with the cutout mip cap at its new default 3, and it sets `_cutoutSettingsBackfilled = true`. The full-field scope preserves the context-menu Reset contract exactly: today, with no `Reset()` defined, Unity's Reset command restores every field to its fresh default, and this method keeps that meaning while adding the marker set. The marker set prevents the first round trip from copying the opacity arm over the fresh 3.
   The backfill body itself does not change. Legacy payloads (payloads without the cutout fields) keep inheriting their stored opacity values once, exactly as today. Existing saved components keep their stored values, because their marker is already true. The marker's doc comment at `Runtime/AmuseAvatarOptimizer.cs:122-131` is updated to name both meanings: true after the upgrade backfill copies the transparent values into the cutout fields, or after Reset establishes the factory defaults. The runtime field doc comment sentence "The default of 4 matches the transparent setting." becomes "The default is 3. The transparent setting defaults to 4." (closes open point 9 together with decision 8).
5. Preset values (part of the owner request). `Presets/safe.json` cutout 4 becomes 3. `Presets/normal.json` cutout 4 becomes 3. `Presets/aggressive.json` cutout 3 becomes 2. `schemaVersion` stays 4. The parser range -1 and 0 through 10 already admits the new values, so parsing and refusals do not change. Components saved before this change keep their stored cutout 4 and no longer show the Normal preset pressed, because `Matches` compares stored values. This is accepted owner-requested behavior, recorded as the investigation assigned.
6. Toggle relabel mechanics. `DrawSettings` replaces the `EditorGUILayout.PropertyField` call for `_ignoreOutOfRangeMaterialSlots` with a hand-drawn control that keeps every affordance `PropertyField` plus `ToggleLeftDrawer` provide today: `EditorGUI.BeginProperty` around `EditorGUI.ToggleLeft`, drawn value `!property.boolValue`, write-back `property.boolValue = !allow` on `EditorGUI.EndChangeCheck`, and explicit `EditorGUI.showMixedValue = property.hasMultipleDifferentValues` set and restored around the call so multi-object selection keeps its mixed dash.
7. New test seam for the drawn value. Add `internal static Action<bool> OnOutOfRangeToggleDrawnForTest` to the editor, fired with the drawn Allow value right after the toggle draws. This mirrors the existing `OnFoldoutHeaderDrawnForTest` house pattern and is the only new production member besides `Reset()` and the label-width helper.
8. Cutout tooltip sentence (closes open points 6 and 9). The cutout tooltip sentence "The default of Mip 4 fits most viewing distances." becomes "The default of Mip 3 fits most viewing distances." The opacity tooltip keeps Mip 4. The Allow-voice tooltip for the relabeled toggle is exactly:

   > Some animation files animate material slots that do not exist on this mesh. When this setting is on, AMUSE ignores these extra slot animations and optimizes the valid slots. Turn this setting off to refuse meshes with extra slot animations.

9. README and architecture sentences (closes open point 5). The replacements are sentence-scoped, not line-scoped:
   - In `README.md:21-23`, the Safe sentence becomes: "Safe turns on both anisotropy acceptance settings. It keeps **Allow Out-of-Range Material Slots** and **Allow Depth Test Change on Moved Triangles** off, and it never flattens a mixed material." The range ends mid-line. The trailing `Normal` at `:23` begins the next sentence ("Normal and Aggressive enable all four settings.") and stays.
   - In `README.md:26`, the Aggressive sentence becomes: "Aggressive uses 10 percent and sets Smallest Tested Mipmap to Mip 3. Cutout textures use Mip 2."
   - In `docs/architecture/vision.md:19`, only the sentence "The default alpha policy limits evidence to mip level 4 and a minimum size of 128 texture pixels." becomes: "The default alpha policy limits evidence to mip level 4, and to mip level 3 for cutout sources, with a minimum size of 128 texture pixels." The rest of that paragraph stays.
10. Flip-table value (closes open point 7). `FlippingAnyOneFieldValueBreaksTheMatch` flips `_cutoutPreserveTransparencyMaxMipLevel` to 4, which differs from the new preset value 3.
11. `ValidBody` syncs to 3 (closes open point 8). The body stays Safe-shaped and honest. No assertion depends on the value.

## Component state

Schema key `_cutoutPreserveTransparencyMaxMipLevel` only. All other serialized fields keep name, type, default, and meaning.

| Schema key | Serialized field | Property | Range | Old default | New default |
|---|---|---|---|---|---|
| cutout mip cap | `_cutoutPreserveTransparencyMaxMipLevel` | `CutoutPreserveTransparencyMaxMipLevel` | -1 or 0 to 10 | 4 | 3 |

New members: `private void Reset()` on the runtime component (sets the field above to 3 and `_cutoutSettingsBackfilled` to true), `internal static float AlphaSeparatorLabelWidth(float)` and `internal static Action<bool> OnOutOfRangeToggleDrawnForTest` on the editor. Nothing else is added, and no member is removed.

## Data files

| File | Key | Old | New |
|---|---|---|---|
| `Packages/com.alrauna.amuse/Presets/safe.json:16` | `cutoutPreserveTransparencyMaxMipLevel` | 4 | 3 |
| `Packages/com.alrauna.amuse/Presets/normal.json:16` | `cutoutPreserveTransparencyMaxMipLevel` | 4 | 3 |
| `Packages/com.alrauna.amuse/Presets/aggressive.json:16` | `cutoutPreserveTransparencyMaxMipLevel` | 3 | 2 |

Opacity-arm values stay 4, 4, and 3. All other keys in all three files stay byte-identical.

## Inspector changes

Draw changes, in file order. Early returns inside every scoped region are covered by the disposable scopes, so no manual decrement exists anywhere.

1. `DrawMaterialTransformations`: after the foldout header draws and fires its test hook, wrap everything else in `using (new EditorGUI.IndentLevelScope())`. The Refresh button, the Alpha Separator foldout, and the whole view draw at depth 1. The slot-row width formula already compensates for the ambient indent.
2. `DrawSettings`: after the closed-foldout early return, wrap the body in one `IndentLevelScope`. "Disable AMUSE" and "Advanced Settings" draw at depth 1. The `if (reveal.boolValue)` block wraps its two toggles in a nested `IndentLevelScope`, so they draw at depth 2. The out-of-range toggle inside that block is the relabeled control from the next section.
3. `DrawAlphaSeparatorSettings`: enter two nested `IndentLevelScope` instances so the foldout header draws at depth 2. Inside a third scope the body draws at depth 3. `EditorGUIUtility.labelWidth` is set inside that third scope from `AlphaSeparatorLabelWidth(widest)` and restored as today. The Transparent and Cutout submenu drawers and the acceptance group draw unchanged inside the depth-3 scope.

The top-level "Alpha Separator" toggle, the header, the preset row, and the HelpBoxes stay at depth 0.

## Toggle relabel

In `DrawSettings`, inside the advanced block, the relabeled control draws exactly:

```csharp
var outOfRange = serializedObject.FindProperty(
    "_ignoreOutOfRangeMaterialSlots");
var rowRect = EditorGUILayout.GetControlRect();
var allowContent = new GUIContent(
    "Allow Out-of-Range Material Slots",
    "Some animation files animate material slots that do not " +
    "exist on this mesh. When this setting is on, AMUSE ignores " +
    "these extra slot animations and optimizes the valid slots. " +
    "Turn this setting off to refuse meshes with extra slot " +
    "animations.");
EditorGUI.BeginProperty(rowRect, allowContent, outOfRange);
var previousMixed = EditorGUI.showMixedValue;
EditorGUI.showMixedValue = outOfRange.hasMultipleDifferentValues;
try
{
    EditorGUI.BeginChangeCheck();
    var allow = EditorGUI.ToggleLeft(rowRect, allowContent,
        !outOfRange.boolValue);
    OnOutOfRangeToggleDrawnForTest?.Invoke(allow);
    if (EditorGUI.EndChangeCheck())
    {
        outOfRange.boolValue = !allow;
    }
}
finally
{
    EditorGUI.showMixedValue = previousMixed;
}
EditorGUI.EndProperty();
```

The stored name, the stored meaning, the preset keys, the runtime accessor, and every build-path consumer stay untouched. A plain repaint writes nothing.

## Parsing and refusals

None. `TryReadMipCap` already accepts -1 and 0 through 10. `schemaVersion` stays 4. No refusal enum gains or loses a value.

## Reports

No report string changes. No refusal path changes. The stored-value fingerprints (`ComputePolicyFingerprint`, the dry-run fingerprint) read stored values, so a default change causes their designed clean-miss invalidation and nothing else.

## Invariants

Each invariant is testable and must hold after the change.

1. Serialization never changes: no field is renamed, retyped, or re-semanted, and no stored scene or prefab value moves because of this round. The one documented-meaning widening is the backfill marker's doc comment, which now also names the Reset path (decision 4). Stored values, stored meaning, and read paths are unchanged.
2. Legacy upgrade is byte-identical: a payload without the cutout fields still inherits its stored opacity values once, and an edited stored cutout value still survives every reload.
3. The preset schema version stays 4 and the accepted parser range stays -1 and 0 through 10.
4. The opacity arm is untouched: default 4, preset values 4, 4, 3, and the tooltip sentence keeps Mip 4.
5. The six foldout header strings and their test-hook fire sites are unchanged, and every existing header assertion stays green.
6. No drawn label or tooltip changes except the relabeled toggle and the cutout mip sentence from decision 8.
7. The relabeled toggle keeps the prefab-override menu, the mixed-value dash, and the left-aligned checkbox look, and a plain repaint never writes the stored value.
8. Depths are exactly: transformation view children 1, Settings children 1, advanced-revealed children 2, Alpha Separator Settings header 2 and contents 3, everything else 0.
9. `EditorGUI.indentLevel` is always restored to its ambient value after every draw path, including every early return.

## Dependencies

None upstream. The change grows three members named in the component state table. Blast-radius files: `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`, `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`, the three preset JSONs, `README.md`, `docs/architecture/vision.md`, and the test files listed below. No other file may change.

## Test strategy

Tests run in the Unity Test Runner, EditMode. A filtered run that reports zero tests is a failure. Behavior changes observe RED first against a named wrong implementation, then GREEN with the minimal fix. An assertion that passes on first run is recorded as characterization. Layout-only changes follow the 2026-10-05 precedent: RED/GREEN does not apply to pure indentation, and the existing repaint suite is the smoke validation.

Per class:

- `Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs`. Rename `DefaultCutoutPreserveTransparencyMaxMipLevelIs4` to `DefaultCutoutPreserveTransparencyMaxMipLevelIs3` and assert 3. New test `FreshCutoutDefaultSurvivesASerializationRoundTrip`: add the component fresh, assert cutout 3, round-trip the component through serialization, assert cutout 3 again. `UpgradedComponentBackfillsCutoutValuesFromItsOwnTransparentValues`, `BackfillRunsOnceAndKeepsEditedCutoutValues`, and `FreshComponentKeepsEditedCutoutValuesAcrossAReload` must stay green untouched.
- `Tests/Editor/Presets/OptimizerPresetTests.cs`. `ShippedDefaults()` second positional 4 becomes 3. Flip-table row becomes `("_cutoutPreserveTransparencyMaxMipLevel", 4)`. `DefaultComponentMatchesTheShippedDefaults` and `DefaultComponentMatchesTheShippedNormalPreset` must stay green, which proves the fresh default, the initializer or Reset path, and `normal.json` moved together.
- `Tests/Editor/Presets/PresetApplierTests.cs`. Expected table rows become `("Safe", 3, 128, 25, 0)`, `("Normal", 3, 128, 25, 5)`, `("Aggressive", 2, 128, 25, 10)`. `ApplyWritesAllSixteenFields` asserts 3. `ApplyingShippedAggressiveSetsSmallestTestedMipmapToThree` stays green untouched (opacity arm).
- `Tests/Editor/Presets/PresetParserTests.cs`. `ValidJson` cutout 4 becomes 3, asserted value becomes 3, and all four `string.Replace` needles move with it: `WithCutoutMipCap` and the three legacy-body builders (`LegacyVersionTwoBody`, `LegacyVersionOneBody`, `LegacyVersionThreeBody`).
- `Tests/Editor/Presets/PresetFileStoreTests.cs`. `ValidBody` cutout becomes 3.
- `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`. New `OutOfRangeToggleDrawsTheInvertedValueAndWritesNothingOnRepaint`: stored true draws Allow checked, stored false draws it unchecked, and a plain repaint changes neither stored value. New label-width `[Test]` methods live directly in this class (the house mirror rule has one test class per production type, so no sibling test class is created): at indent 0 the helper returns `ceil(widest) + 2`, and each indent level subtracts 15. Existing header, out-of-band, repaint, and dry-run tests must stay green untouched; they are the smoke validation for the indentation.
- `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`. `PreserveTransparencyMipPolicyScopesTheProofEndToEnd` must stay green (it tolerates cap 3). Its doc-comment prose at `:4917-4919` syncs from "through mip 4" to the cap-3 wording.

Falsifiers, numbered:

- --- Falsifier 1: an implementation that changes only the field initializer to 3 without `Reset()`. `FreshCutoutDefaultSurvivesASerializationRoundTrip` fails on its post-round-trip assertion, because the first round trip runs the backfill and copies the opacity 4 over the fresh 3. ---
- --- Falsifier 2: an implementation that renames or inverts the stored field instead of inverting the drawn value. `OutOfRangeToggleDrawsTheInvertedValueAndWritesNothingOnRepaint` fails on the stored-value assertion, and `DefaultIgnoreOutOfRangeMaterialSlotsIsTrue` plus the preset Matches semantics fail with it. ---
- --- Falsifier 3: an implementation of `AlphaSeparatorLabelWidth` that ignores `EditorGUI.indentLevel` (returns `ceil(widest) + 2` always). The indent-3 case of the helper test fails. ---
- --- Falsifier 4: an implementation that updates the preset JSONs but not the runtime default, or the reverse. `DefaultComponentMatchesTheShippedNormalPreset` fails, because a fresh component and `normal.json` then disagree on the cutout cap. ---

## Documentation

The exact replacement sentences are in decisions 4, 8, and 9. Historical records under `docs/superpowers/` are never retro-edited.

## Acceptance criteria

1. With Advanced Settings off, the Settings body draws one level deep and the advanced block is hidden.
2. With Advanced Settings on, the two consent toggles draw one level deeper than "Disable AMUSE", and Alpha Separator Settings draws its header at that level with its contents one level deeper still.
3. The Show Material Transformations body, including Refresh and the Alpha Separator view, draws one level deep.
4. The toggle reads "Allow Out-of-Range Material Slots", draws checked when the stored value is true, and a plain repaint writes nothing.
5. A fresh component reports cutout mip cap 3 before and after a serialization round trip. A legacy payload still inherits its stored opacity values once.
6. The three preset buttons apply cutout caps 3, 3, and 2, and the pressed state stays honest for a fresh component against Normal.
7. The full `Alrauna.Amuse.Tests.Editor` assembly passes, and `Alrauna.Amuse.Research.Tests.Editor` still passes.

## Validation and stop conditions

Validation order: refresh Unity and wait for readiness, run the focused filters named in the test strategy, then the full `Alrauna.Amuse.Tests.Editor` assembly, then `Alrauna.Amuse.Research.Tests.Editor`. Record observed counts. Compilation and zero-test runs do not count as proof.

Stop and return evidence when: `EditorGUI.IndentLevelScope` proves unavailable or behaves differently in 2022.3.22f1, `Reset()` proves not to fire on editor `AddComponent` in the EditMode test harness, the depth-3 layout still clips the value column after the label-width fix, or any invariant in this spec fails.

## Authorization boundary

The owner authorized this round on 2026-10-17, including the work branch, one commit per finalized document, and one commit per implementation task. Staying unauthorized: pushing, opening or merging pull requests, deleting branches, rewriting history, and changing remotes or settings.

## Out of scope

Indenting the Transparent and Cutout submenus beyond the Alpha Separator Settings body depth, a fourth depth level, any opacity-arm change, any preset schema change, any new drawer type, any localization or editor-window work, and the Census or research packages.
