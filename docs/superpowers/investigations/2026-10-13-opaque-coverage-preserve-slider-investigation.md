# Opaque Coverage Preserve Slider Investigation

Date: 2026-10-13

Base: `main` at `a8f2790`. Work branch: `feat/min-opaque-coverage-preserve`.

This document records facts about the current code. It changes nothing.

Evidence labels:

- `[SOURCE]` A fact read in this repository at the base commit. The citation gives a repository-relative path and line numbers.
- `[INFERENCE]` A deduction from listed facts. The deduction is named.
- `[RECOMMENDATION]` A proposed next step for the design round.

## 1. Purpose

The owner asked for four changes to the Alpha Separator Settings.

1. Rename each existing slider. The words `to Convert` go at the end of the name, before the parenthesis.
2. Add a new slider named `Minimum Opaque Coverage to Preserve (Per Material)` just below the renamed `Minimum Opaque Coverage to Convert (Per Material)` slider. Its range is 0 to 100. When the share of faces that are not proven opaque sits below this value, the whole material moves to the Opaque material. No proof is required for the moved faces. The purpose is to save a draw call at the cost of possible visual artifacts.
3. The Safe preset gains acceptance of desktop and mobile anisotropy differences, and sets the new slider to 0.
4. The Normal preset sets the new slider to 5. The Aggressive preset sets it to 10.

This investigation covers the settings surface, the preset system, the anisotropy settings, and the pipeline seam where the new gate must sit.

## 2. The settings surface today

### 2.1 The component fields

`[SOURCE]` The settings live on `AmuseAvatarOptimizer` in `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`. The percent fields sit at lines 57 to 107:

| Field | Line | Default | Inspector name |
|---|---|---|---|
| `_minimumOpaqueCoveragePercent` | 68 | 25 | Minimum Opaque Coverage (Per Material) |
| `_minimumOpaqueAlphaPercent` | 81 | 100 | Alpha Upper Clamp (Per Texture) |
| `_polygonAlphaUpperClampPercent` | 95 | 100 | Alpha Upper Clamp (Per Polygon) |
| `_polygonMinimumOpaqueCoveragePercent` | 107 | 100 | Minimum Opaque Coverage (Per Polygon) |

`[SOURCE]` Every percent field carries `[Range(0, 100)]`. The read-only properties sit at lines 184 to 206. The anisotropy consent fields are `_acceptDesktopAnisotropyDifferences` at line 122 and `_acceptMobileAnisotropyDifferences` at line 133. Both default to false. Their properties sit at lines 232 to 241.

`[SOURCE]` Each field doc comment quotes its inspector name. The coverage doc is at lines 57 to 68, the per-texture clamp doc at lines 70 to 81, the per-polygon clamp doc at lines 83 to 95, and the per-polygon coverage doc at lines 97 to 107.

### 2.2 The inspector rows

`[SOURCE]` The inspector is `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`. Every label and tooltip is a `GUIContent` literal. The rows in draw order inside the `Alpha Separator Settings` foldout:

| Order | Row content field | Label line | Control |
|---|---|---|---|
| 1 | `MipCapRowContent` | 122 | Popup, `Smallest Tested Mipmap` |
| 2 | `MinTextureRowContent` | 151 | Popup, `Smallest Tested Texture` |
| 3 | `MaterialCoverageRowContent` | 170 | Slider, `Minimum Opaque Coverage (Per Material)` |
| 4 | `TextureClampRowContent` | 185 | Slider, `Alpha Upper Clamp (Per Texture)` |
| 5 | `PolygonCoverageRowContent` | 203 | Slider, `Minimum Opaque Coverage (Per Polygon)` |
| 6 | `PolygonClampRowContent` | 220 | Slider, `Alpha Upper Clamp (Per Polygon)` |
| 7 | `AcceptDesktopRowContent` | 238 | Toggle |
| 8 | `AcceptMobileRowContent` | 249 | Toggle |
| 9 | `ActiveSamplingProofRowContent` | 259 | Read-only proof mode label |

`[SOURCE]` `RowContents` at lines 271 to 282 lists the nine row contents in draw order. It is the single source of truth. It feeds the draw calls and the shared label width at lines 302 to 305. A label change re-measures the column with no second edit.

`[SOURCE]` The draw sequence sits in `DrawAlphaSeparatorSettings` at lines 292 to 313: the two popups, then `DrawCoverageSlider` at line 308, then `DrawAlphaPolicyControls` at line 309, then `DrawAcceptanceControls` at line 310. `DrawCoverageSlider` at lines 496 to 500 drives `PercentSlider` with `_minimumOpaqueCoveragePercent`. `PercentSlider` at lines 471 to 494 is the shared slider helper. It warns on out-of-band stored values and clamps the drawn value.

`[SOURCE]` The whole foldout stays hidden until the `Advanced Settings` reveal turns on. `DrawSettings` returns the reveal flag at line 690, and the gate `if (DrawSettings())` at line 60 with the call at line 62 controls it.

## 3. The rename targets

The owner said `each of the existing slider names`. Four rows are sliders. The two popups and the two toggles carry no parenthesis and stay unchanged. The read-only proof label stays unchanged.

| Current label | New label | Editor line | Other sites |
|---|---|---|---|
| Minimum Opaque Coverage (Per Material) | Minimum Opaque Coverage to Convert (Per Material) | `AmuseAvatarOptimizerEditor.cs:170` | `AmuseAvatarOptimizer.cs:63-64` doc |
| Alpha Upper Clamp (Per Texture) | Alpha Upper Clamp to Convert (Per Texture) | `AmuseAvatarOptimizerEditor.cs:185` | `AmuseAvatarOptimizer.cs:76-77` doc |
| Minimum Opaque Coverage (Per Polygon) | Minimum Opaque Coverage to Convert (Per Polygon) | `AmuseAvatarOptimizerEditor.cs:203` | `AmuseAvatarOptimizer.cs:101-103` doc, `AmusePlatformFinishPlugin.cs:1209-1210` doc, `AmusePlatformFinishPlugin.cs:1238` exception text |
| Alpha Upper Clamp (Per Polygon) | Alpha Upper Clamp to Convert (Per Polygon) | `AmuseAvatarOptimizerEditor.cs:220` | `AmuseAvatarOptimizer.cs:90-91` doc, `AmusePlatformFinishPlugin.cs:1182` doc |

`[SOURCE]` No test asserts a drawn label. `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs:109,143,170` passes label strings as inputs to the `OutOfBandWarning` helper. These are message-construction inputs, not assertions. They survive a rename, and updating them keeps the fixtures honest.

`[SOURCE]` The two anisotropy tooltips end with the sentence `A shipped preset turns this setting off.` The desktop tooltip spans `AmuseAvatarOptimizerEditor.cs:238-245` with the sentence at lines 244 to 245. The mobile tooltip spans lines 249 to 255 with the sentence at lines 254 to 255. Today the Safe preset turns both settings off, and Normal and Aggressive turn them on. After the planned Safe change, no shipped preset turns them off. Both tooltip sentences become wrong and need new text. `[INFERENCE]` The plugin doc at line 1134 says `Minimum Opaque Coverage Percentage`, which is a paraphrase of the old label. It should follow the rename for consistency.

`[SOURCE]` The historical records under `docs/superpowers/` quote the old labels. Per `docs/superpowers/README.md` every record there is a dated statement of what was designed, decided, or observed at its date, and a record that conflicts with current code is history. `[INFERENCE]` This document treats those records as never retro-edited. The change adds new dated records instead of touching the old ones.

## 4. The preset system

### 4.1 The shipped files

`[SOURCE]` Three preset files ship in `Packages/com.alrauna.amuse/Presets/`: `safe.json`, `normal.json`, `aggressive.json`. `PresetFileStore.cs:22-23` fixes the file names and order. Current values:

| Setting | Safe | Normal | Aggressive |
|---|---|---|---|
| `preserveTransparencyMaxMipLevel` | 4 | 4 | 4 |
| `preserveTransparencyMinTextureSize` | 128 | 128 | 128 |
| `minimumOpaqueCoveragePercent` | 25 | 25 | 25 |
| `minimumOpaqueAlphaPercent` | 100 | 100 | 95 |
| `polygonAlphaUpperClampPercent` | 100 | 100 | 94 |
| `polygonMinimumOpaqueCoveragePercent` | 100 | 100 | 95 |
| `allowDepthTestChange` | false | true | true |
| `ignoreOutOfRangeMaterialSlots` | false | true | true |
| `acceptDesktopAnisotropyDifferences` | false | true | true |
| `acceptMobileAnisotropyDifferences` | false | true | true |

`[SOURCE]` The Safe description is `Keeps anisotropy checks and does not allow slot or depth-test exceptions.` (`safe.json:4`). Normal and Aggressive share the description `Accepts desktop and mobile anisotropy differences, slot exceptions, and depth-test changes.`

### 4.2 The preset plumbing

`[SOURCE]` `OptimizerPreset` (`Editor/Presets/OptimizerPreset.cs`) is an immutable record with eleven mapped fields. Its constructor takes thirteen arguments. `Matches` at lines 66 to 85 compares all eleven fields against the live component. The doc comments at lines 58 to 63 and `AmuseAvatarOptimizerEditor.cs:73-77` both say `eleven`.

`[SOURCE]` `PresetApplier.Apply` (`Editor/Presets/PresetApplier.cs:11-49`) writes exactly the eleven mapped fields through the serialized object. It never touches `_amuseDisabled` or `_advancedSettingsRevealed`. The doc at lines 5 to 12 says `eleven mapped fields` and states that applying replaces both acceptance settings.

`[SOURCE]` `PresetParser` (`Editor/Presets/PresetParser.cs`) parses against the closed version 2 schema. `CurrentSchemaVersion` is 2 at line 18. The parser refuses an unknown key, a missing key, a wrong type, or an out-of-range value with a named refusal and no preset (doc at lines 6 to 13). The version is read before every version-specific key, so an older file answers `UnknownSchemaVersion`. Percent leaves go through `TryReadPercent`.

`[SOURCE]` The preset row renders one toggle button per preset (`AmuseAvatarOptimizerEditor.cs:100-115`). A button presses only while the component matches that preset on all mapped fields. The press calls `PresetApplier.Apply`.

### 4.3 Tests that pin preset behavior

`[SOURCE]` These pins break or bind the planned changes:

- `Tests/Editor/Presets/OptimizerPresetTests.cs:10-15` builds `ShippedDefaults()` from literals. The fixture mirrors the fresh component defaults, not the shipped `safe.json` file. It passes true for `allowDepthTestChange` and `ignoreOutOfRangeMaterialSlots` at line 14, while `safe.json:15-16` sets both to false. `DefaultComponentMatchesTheShippedDefaults` at lines 17 to 30 asserts a fresh component matches this in-code record, which is named `Safe`. Today the shipped `safe.json` file does not match a fresh component on those two booleans. `FlippingAnyOneFieldValueBreaksTheMatch` at lines 32 to 83 loops an eleven-row table at lines 36 to 49.
- `Tests/Editor/Presets/PresetParserTests.cs:7-25` pins a valid JSON fixture with all ten settings keys. `ValidSchemaParsesAllElevenValues` at lines 53 to 72 asserts every value. The method name encodes the count. Percent range refusal tests follow the `PercentAboveHundredRefuses` pattern. Schema version refusal tests cover the version 1 to 2 precedent.
- `Tests/Editor/Presets/PresetFileStoreTests.cs` pins the shipped count, order, and names. Its `ValidBody` fixture must gain any new key.
- `Tests/Editor/Presets/PresetApplierTests.cs:12-58` runs `ApplyingShippedSafeRevokesBothAcceptanceSettings` against the real shipped `safe.json`. This test asserts the exact behavior the owner plans to invert. `ApplyWritesAllElevenFields` at lines 61 to 132 encodes the field count in its name.

`[INFERENCE]` A fresh component already matches no shipped preset file at the base commit. Safe differs from a fresh component on `allowDepthTestChange` and `ignoreOutOfRangeMaterialSlots`. Normal and Aggressive differ on both anisotropy acceptances. The planned Safe acceptance flip does not change fresh-component matching. The design-round decision is different: the in-code `Safe` fixture and the shipped `safe.json` file already diverge, and the new preserve field must pick a value for both, which decides whether they stay divergent.

## 5. The anisotropy acceptance settings

`[SOURCE]` The two toggles consent to proof changes per build target. The field docs (`AmuseAvatarOptimizer.cs:113-133`) state that acceptance lets the proof drop anisotropic footprints on that target, that false keeps the proof conservative, and that each field never changes the other.

`[SOURCE]` The build reads the fields only through `PlatformAlphaSamplingPolicy.TrySelect` (`AmusePlatformFinishPlugin.cs:635-640` at the barrier, `AlphaSeparationApply.cs:148-152` at apply). The result is one of three policies in `Editor/Analysis/AlphaSamplingPolicy.cs:13-19`. The accepted policy projects sampling with no anisotropic footprints (`AlphaSamplingPolicy.cs:21-56`). The proof stays exact for everything else.

`[SOURCE]` A refused split whose shortfall follows anisotropic sampling reports the named `AnisotropyDifferences` refusal with an acceptance hint (`AlphaSeparationRecords.cs:85-91`, gate at `AlphaSeparationPreparation.cs:302-341`).

`[INFERENCE]` The owner's instruction adds both acceptances to Safe. Safe keeps its other conservative values, so Safe remains distinct from Normal through `allowDepthTestChange`, `ignoreOutOfRangeMaterialSlots`, and the new preserve value.

## 6. The separation pipeline seam

### 6.1 Classification and planning

`[SOURCE]` `TriangleAlphaOutcome` (`Editor/Analysis/TriangleAlphaClassifier.cs:10-15`) has exactly three values at lines 12 to 14: `ProvenOpaque`, `MustRemainTransparent`, `Unknown`.

`[SOURCE]` `MeshSeparationPlanner.Create` (`Editor/Analysis/MeshSeparationPlanner.cs:168-214`) buckets each submesh's triangles. `ProvenOpaque` goes to `OpaqueTriangleOrdinals`. Everything else, both `MustRemainTransparent` and `Unknown`, goes to `TransparentTriangleOrdinals`. There is no third ordinal list.

`[SOURCE]` The planner derives a disposition per submesh (`MeshSeparationPlanner.cs:195-199`):

| Condition | Disposition |
|---|---|
| No proven-opaque triangles | `Unchanged` |
| No transparent triangles | `WhollyOpaqueCandidate` |
| Both kinds present | `Split` |

`[SOURCE]` A slot can bind several submeshes (`AlphaSeparationPreparation.cs:122-125`). The plan keeps one entry per source submesh, and every existing gate evaluates per submesh.

### 6.2 Settings flow into the gate

`[SOURCE]` The pass maps the component value through a defensive mapper. `MinimumCoverageFrom` (`AmusePlatformFinishPlugin.cs:1140-1150`) treats a stored value below zero as 0. The value reaches `AlphaSeparationPreparation.Prepare` as the `minimumOpaqueCoveragePercent` parameter (`AlphaSeparationPreparation.cs:104`). The gate is `OpaqueCoverageBelow` at lines 686 to 695:

```
totalTriangles = opaqueOrdinals.Count + transparentOrdinals.Count
refuse split when: opaqueOrdinals.Count * 100
                   < minimumOpaqueCoveragePercent * totalTriangles
```

`[SOURCE]` The gate's doc states exact integer arithmetic, that a share exactly at the minimum proceeds, and that unknown and transparent triangles both count in the denominator because neither is proven opaque.

### 6.3 The candidate loop

`[SOURCE]` `Prepare` walks `plan.Submeshes` (`AlphaSeparationPreparation.cs:296-357`):

1. An `Unchanged` submesh holds no candidate and carries no refusal. The loop skips it at lines 298 to 305.
2. A `Split` submesh below the convert minimum refuses at lines 308 to 356. The refusal cause is `OpaqueCoverageBelowMinimum` at lines 345 to 354, or the `AnisotropyDifferences` hint when the shortfall follows anisotropic sampling. The two causes report differently. The `AnisotropyDifferences` hint is deduplicated per slot through the shared `reportedAnisotropySlots` set at lines 125, 144, and 332, so several submeshes binding one slot produce one refusal for that slot. The plain `OpaqueCoverageBelowMinimum` branch has no guard. `RecordSlotRefusal` (`AmusePlatformFinishPlugin.cs:114-124`) increments a per-reason bucket, and `AmuseReports.SlotSeparationRefusal` (`AmuseReports.cs:223-287`) always reports. Two submeshes binding one slot, both below the minimum, produce two census increments and two console entries for that slot.
3. Every other candidate passes the marker-clip check at lines 361 to 409.
4. Conversion then runs per admitted material at lines 428 to 508. `ConvertAdmittedMaterial` is called at lines 465 to 478, the mapping fills at line 502, and the slot with its `OpaqueOfAdmitted` map joins `candidateSlots` at lines 571 to 573. The premultiply report fires at lines 590 to 600.

`[SOURCE]` A renderer whose plan has no candidate slots returns no preparation at lines 605 to 608. A mesh clone is created only when the plan requires a split and at least one surviving candidate keeps the `Split` disposition (lines 629 to 638).

### 6.4 Apply and revalidation

`[SOURCE]` Apply revalidates every candidate against live state (`AlphaSeparationApply.cs:279-383`). The checks are: the renderer's material block state still equals the preparation block state (lines 293 to 317), live material-swap bindings were captured and map through `OpaqueOfAdmitted`, marker clips refuse, and for `Split` slots the appended index carries no unrecorded binding (`ValidateCandidateSlot`, lines 688 to 782). Nothing re-derives the classification. A prepared disposition is never recomputed.

`[SOURCE]` Finalization assigns materials (`AlphaSeparationApply.cs:424-454`). A surviving `WhollyOpaqueCandidate` slot swaps its material in place: `materials[slotIndex] = opaque`. Its own curves rewrite through the same map. A surviving `Split` slot keeps its live material and gains one appended slot per split, carrying the opaque result. `FinalizeClone` at lines 795 to 869 rebuilds split submeshes: transparent ordinals stay, opaque ordinals move to the appended submesh.

`[SOURCE]` The summary counter `AppliedOpaqueTriangleCount` (`AmusePlatformFinishPlugin.cs:69`) accumulates through `CountOpaqueTriangles` (`AlphaSeparationApply.cs:900-952`). For a changed slot it adds the candidate plan's opaque ordinal count at line 935. For appended submeshes it adds their triangle counts at lines 941 to 949. The summary line reports it after the mutation (`AlphaSeparationApply.cs:84-99`).

## 7. Where the preserve gate sits

`[RECOMMENDATION]` The new gate belongs in the candidate loop of `AlphaSeparationPreparation.Prepare`, beside the existing coverage gate. The mechanism:

1. The new field `_minimumOpaqueCoverageToPreservePercent` defaults to 0. Zero is inert. Range 0 to 100. A mapper with the same defensive read as `MinimumCoverageFrom` carries it into `Prepare`.
2. For a `Split` submesh, compute the transparent share the same way the convert gate computes the opaque share: `transparentOrdinals.Count * 100 < preservePercent * totalTriangles`. When true, the submesh flattens.
3. Flatten means: rewrite that submesh plan entry to `WhollyOpaqueCandidate` with every ordinal folded into the opaque list and the transparent list empty. The rewritten plan replaces the original before candidate slots are built. The slot then follows the existing `WhollyOpaqueCandidate` path: in-place material swap, curve rewrite, no mesh clone for it, no appended submesh.
4. The flatten decision runs before the convert gate for that submesh. A flattened submesh reports no coverage refusal and no anisotropy hint. It reports one new per-slot event that names the forced triangle count, so the report stays honest about unproven moves.
5. The summary counter keeps counting whole-submesh opaque ordinals. The doc comment of `CountOpaqueTriangles` changes from `proven-opaque rendering` to `opaque rendering`, because forced triangles are no longer proven.

`[INFERENCE]` Consequences that fall out of the mechanism:

- A preserve value of 0 changes nothing. `0 < 0` is never true. Old avatars keep their behavior.
- An `Unchanged` submesh has a transparent share of 100 percent and can never flatten. A material with no proven-opaque triangle never flattens.
- A `WhollyOpaqueCandidate` submesh is already whole-opaque. The gate never sees it.
- More unknown triangles raise the transparent share, so uncertainty always makes flattening less likely, never more likely. This follows the repository rule that increased uncertainty must never make a transformation more aggressive.

`[SOURCE]` A review pass for this document checked every production consumer of `SubmeshSeparationPlan` and of the disposition values for compatibility with a folded entry. The sweep covered the anisotropy loop at `AlphaSeparationPreparation.cs:128-162`, the candidate loop, marker-clip checks, conversion, the divergence and premultiply reports, the clone decision at lines 629 to 638, apply split counting and appended slots, the in-place swap, curve edits, `ValidateCandidateSlot`, `FinalizeClone`, `CountOpaqueTriangles`, the clone sweep, and the transient unlock passes. All are safe for a folded entry. Two findings shape the design:

- The clone decision reads `plan.RequiresAnySplit` and `candidateSlots.Any(Split)`. The plan object is immutable, so after an in-loop rewrite `RequiresAnySplit` stays stale-true. The second conjunct still fails for a folded entry, so a renderer whose only split was folded creates no clone. No production consumer reads any plan aggregate after the rewrite.
- `HasAnyUnknownOutcome` becomes unreachable for a folded entry, because both of its call sites sit behind an `Unchanged` filter or the split-only coverage gate.

`[INFERENCE]` The rewrite therefore has two shape options. It can replace the submesh entry inside the plan and leave the immutable aggregates stale, relying on the guarded clone conjunct. Or it can rebuild the plan so the aggregates stay honest. The design round picks one.

`[INFERENCE]` The owner's words `percentage of proven alpha faces` map to the transparent bucket. The planner has no third list, and the existing gate's doc already treats unknown as not-proven-opaque. The alternative reading, which counts only `MustRemainTransparent`, would let unknown-heavy materials flatten on thinner evidence. The design round should confirm the bucket reading.

`[INFERENCE]` The owner's word `material` maps to a submesh share under the existing convention. The convert slider says `(Per Material)` and its gate evaluates per submesh. The preserve slider follows the same shape, so the two sliders stay comparable.

## 8. Blast radius

`[SOURCE]` The change touches these files:

| File | Change |
|---|---|
| `Runtime/AmuseAvatarOptimizer.cs` | New field, property, doc comments; four label quotes follow the rename |
| `Editor/AmuseAvatarOptimizerEditor.cs` | Rename four labels; new row content plus draw call; update two anisotropy tooltip sentences; update `eleven` wording if count comments change |
| `Editor/Presets/OptimizerPreset.cs` | New property, constructor argument, `Matches` clause, count comments |
| `Editor/Presets/PresetApplier.cs` | New field write, count comments |
| `Editor/Presets/PresetParser.cs` | New required settings key, schema version decision |
| `Presets/safe.json`, `normal.json`, `aggressive.json` | New key, Safe acceptance flip, description updates |
| `Editor/Build/AmusePlatformFinishPlugin.cs` | New mapper, `Prepare` call site, doc comment updates, exception text label |
| `Editor/Build/AlphaSeparationPreparation.cs` | Preserve gate, plan rewrite, new report event |
| `Editor/Build/AlphaSeparationApply.cs` | `CountOpaqueTriangles` doc comment update, doc at line 896 |
| `Editor/Build/AlphaSeparationRecords.cs` | Home of the new per-slot event kind, if it follows the refusal-kind pattern, or of a new record field, per the design round |
| `Editor/Build/AmuseReports.cs` + `AmuseReportStrings.cs` | New report key and strings |
| `README.md` | Preset bullet, proof modes section, preset migration paragraph |
| Test classes in section 9 | New and updated pins |

`[SOURCE]` No test asserts a drawn label, so the rename alone breaks no test. The preset tests in section 4.3 do break on the value and acceptance changes, by design.

## 9. Validation shape

`[SOURCE]` Tests run through the Unity Test Runner in EditMode. The classes to mirror:

- `Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs`: default and serialized round-trip pairs, for example `DefaultMinimumOpaqueCoveragePercentIs25` at line 172.
- `Tests/Editor/Presets/OptimizerPresetTests.cs`, `PresetParserTests.cs`, `PresetFileStoreTests.cs`, `PresetApplierTests.cs`: the pins listed in section 4.3.
- `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`: the seam class for the candidate loop. It pins the exact surface the preserve gate lands in, for example `UnchangedSlotWithAnisotropicSampling_RecordsAnisotropyDifferencesRefusal` at line 9451 and `PremultiplyDisclosure_FiresOncePerPreparedSlot` at line 2213.
- `Tests/Editor/Build/AlphaSeparationApplyTests.cs`: the coverage-threshold suite at lines 3087 to 3306 sets the stored percent and asserts named refusal counts across three tests: `CoverageBelowMinimumRefusesTheSplitCandidate`, `CoverageAtTheMinimumPreparesTheSplitCandidate`, and `WhollyOpaqueCandidatesNeverRefuseForCoverage`. The preserve gate needs the same shape: a fixture below and above the threshold, and an assertion that the flattened slot produces no refusal and one forced-opaque event.
- `Tests/Editor/Build/AmuseReportsSlotTests.cs` and `Tests/Editor/Build/AmuseReportStringsTests.cs`: the report message surface, for example `AmuseReportsSlotTests.cs:224-232` asserts a report names its refusal kind exactly. A new report key pins here.
- `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`: out-of-band stored-value behavior and repaint tests, if the new slider needs them.

The repository rules require RED then GREEN for behavior changes, observed test counts, and a failure for any filtered run that reports zero tests.

## 10. Open points for the design round

1. Confirm the transparent-bucket reading of `proven alpha faces` over the strict `MustRemainTransparent` reading.
2. Confirm per-submesh evaluation under the `(Per Material)` label, matching the convert slider.
3. Decide the schema version treatment: bump to version 3 with the new required key, following the version 1 to 2 precedent.
4. Decide the new report event name, its key, and whether the avatar summary gains a forced-triangle count.
5. Decide whether the folded plan entry keeps a marker for provenance, or whether the per-slot report carries all provenance.
6. Decide the value of the new preserve field in the in-code `ShippedDefaults` fixture versus the shipped `safe.json` file, which already diverge on two booleans, and what the defaults-match test should then assert.
7. Confirm the Safe description text and both anisotropy tooltip sentences.
8. Pick the rewrite shape for the folded plan entry: in-loop entry replacement with stale immutable aggregates, or a plan rebuild with recomputed aggregates.

## 11. Verification record

Every citation above was read from the working tree at base `a8f2790` on 2026-10-13. The investigation is read-only. It ran four parallel read-only sweeps over the settings surface, the preset system, the pipeline seam, and the documentation conventions. Every load-bearing fact was re-checked against the cited file in this session. Three parallel adversarial accuracy reviews then verified the sections against the code. The reviews confirmed all mechanism inferences, the inert zero, the immunity of `Unchanged` submeshes, and the safety of the folded plan entry for every production consumer. They found citation drifts, two preset-test misstatements, and one refusal-deduplication conflation, and this document incorporates every confirmed fix.
