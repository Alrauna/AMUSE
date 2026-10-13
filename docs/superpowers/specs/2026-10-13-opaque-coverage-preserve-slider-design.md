# Opaque Coverage Preserve Slider Design

Date: 2026-10-13

Base: `main` at `a8f2790`. Work branch: `feat/min-opaque-coverage-preserve`.

Investigation: `docs/superpowers/investigations/2026-10-13-opaque-coverage-preserve-slider-investigation.md`

Revision note: this spec incorporates the five-lens adversarial review of 2026-10-13. The review confirmed the consent model and the consumer sweep. It changed three designs: the fold applies per material slot, not per submesh alone; the forced report fires after the slot survives preparation; the plan rebuild lives in the Analysis module.

## Overview

AMUSE gains one user-facing power: a material that is almost fully opaque can skip the split and move onto the opaque material in full. The owner pays a possible visual artifact and saves the draw call.

The change has four parts.

1. The four existing percent sliders gain the words `to Convert` in their labels.
2. A new slider, `Minimum Opaque Coverage to Preserve (Per Material)`, sits below the convert slider. Its range is 0 to 100. Its default 0 changes nothing.
3. The preparation pass gains a preserve gate. When every submesh of one material slot holds a share of not-proven-opaque triangles strictly below the slider value, the whole slot flattens onto the opaque material with no proof for the remaining triangles. One report event per flattened submesh, emitted after the slot survives preparation, names the unproven triangle count.
4. The preset files move to schema version 3. Safe gains both anisotropy acceptances and sets the new slider to 0. Normal sets 5. Aggressive sets 10.

## Decisions

The investigation left eight open points. This spec closes them, with review amendments.

1. `Proven alpha faces` means the transparent bucket: every triangle not proven opaque. Both `MustRemainTransparent` and `Unknown` count in the share. Reason: the planner keeps no third list, the existing convert gate already treats unknown as not-proven-opaque, and the strict reading would let unknown-heavy materials flatten on thinner evidence. Increased uncertainty must never make a transformation more aggressive.
2. The share qualifies per submesh. The fold applies per material slot, all or nothing. Finalization writes the opaque result into the shared slot index and rewrites the slot's curve through the opaque map. A partially folded slot would destroy the surviving sibling's conserved transparency through that shared index and curve. Therefore a slot's submeshes fold only when every plan entry sharing the slot's `SourceMaterialBindingIndex` has disposition `Split` and individually qualifies. If any submesh of the slot is `Unchanged` or `Split` but does not qualify, nothing folds for that slot. The label says `(Per Material)` because the decision unit is the material slot.
3. The preset schema bumps to version 3 with the new key required. A version 2 file answers `UnknownSchemaVersion`. This follows the version 1 to 2 precedent.
4. The flatten report is a standalone per-slot event with its own report key, following the `PremultiplyNormalization` pattern: one base message, one `:description` sentence, and one `:hint`. It is not a refusal, so it does not join the `AlphaSeparationSlotRefusal` enum. It fires once per folded submesh whose slot survives preparation, with no deduplication, mirroring the plain coverage refusal. It never fires at fold-decision time, because at that point the slot can still refuse for a marker clip, a conversion refusal, or a renderer-wide refusal.
5. Preparation carries the fold facts in a local map from source submesh index to unproven triangle count. The map exists only to place the disclosure beside the divergence and premultiply reports, after the entry joins the candidate slots. No provenance marker is stored on the plan entry or the prepared record. The avatar summary counts slots whose assigned material result changed. A fold whose opaque mapping equals the live material, which the opaque lilToon arm produces, adds no summary count, because nothing changed. The forced event still fires and still carries the unproven triangle count.
6. The in-code `ShippedDefaults` fixture gains the new field at 0, which matches both the fresh component default and `safe.json`. The fixture and the file stay divergent on the two pre-existing booleans, as they already are at the base commit. The defaults-match test keeps asserting the fresh component against the fixture and gains the twelfth field.
7. The rebuild lives in the Analysis module. `MeshSeparationPlanner` gains one pure entry point that takes the original plan and the folded entry list, or the fold decision, and returns a new `MeshSeparationPlan` with owner-computed `HasAnyOpaqueCandidates`, `RequiresAnySplit`, and triangle counts. Build code has never derived plan aggregates, and it must not start. `AlphaSeparationPreparation` keeps only the fold decision, the slot-uniform rule, and the disclosure map. This keeps the Build-calls-Analysis direction and makes the stale-aggregate wrong implementation structurally unwritable.
8. Safe's description and both anisotropy tooltip sentences change, because the planned values make their current text false. The user-facing sentences that describe moved triangles as `proven-opaque` change, because a folded slot moves unproven triangles too. Exact wording sits in the Documentation section.

## Data files

The three shipped files at `Packages/com.alrauna.amuse/Presets/` change in place. Full schema, version 3, with `safe.json` values:

```json
{
  "schemaVersion": 3,
  "name": "Safe",
  "description": "Accepts desktop and mobile anisotropy differences. It does not allow slot or depth-test exceptions, and it never flattens a mixed material.",
  "features": {
    "alphaSeparator": true
  },
  "settings": {
    "preserveTransparencyMaxMipLevel": 4,
    "preserveTransparencyMinTextureSize": 128,
    "minimumOpaqueCoveragePercent": 25,
    "minimumOpaqueCoverageToPreservePercent": 0,
    "minimumOpaqueAlphaPercent": 100,
    "polygonAlphaUpperClampPercent": 100,
    "polygonMinimumOpaqueCoveragePercent": 100,
    "allowDepthTestChange": false,
    "ignoreOutOfRangeMaterialSlots": false,
    "acceptDesktopAnisotropyDifferences": true,
    "acceptMobileAnisotropyDifferences": true
  }
}
```

`normal.json` keeps its current values, gains `"minimumOpaqueCoverageToPreservePercent": 5`, bumps to `"schemaVersion": 3`, and takes the Normal description below. `aggressive.json` keeps its current values, gains the new key at 10, bumps to version 3, and takes the Aggressive description below.

## Field mapping and value ranges

| Schema key | Serialized field | Property | Range | Default | Safe | Normal | Aggressive |
|---|---|---|---|---|---|---|---|
| `minimumOpaqueCoverageToPreservePercent` | `_minimumOpaqueCoverageToPreservePercent` | `MinimumOpaqueCoverageToPreservePercent` | 0 to 100 | 0 | 0 | 5 | 10 |

The new field sits directly after `_minimumOpaqueCoveragePercent` in the component, the JSON files, and the draw order. The JSON key sits directly after `minimumOpaqueCoveragePercent`.

## Parsing and refusals

`CurrentSchemaVersion` becomes 3. The new key joins `SettingKeys` as a required percent leaf read through `TryReadPercent`. The closed key set keeps its shape: an unknown key, a missing key, a wrong type, or an out-of-range value answers a named refusal and no preset. A version 2 file answers `UnknownSchemaVersion` before any key is read. No new refusal value is needed.

## Component state

The runtime component gains one serialized int field with `[Range(0, 100)]` and default 0, one read-only property, and one doc comment that quotes the inspector label and states the artifact consent. The build reads the field once through a defensive mapper that treats a stored value below zero as 0 and a stored value above 100 as 100. The high side is the dangerous direction for this gate: an out-of-band value must never make the fold more aggressive than the 100 ceiling. The value travels into `AlphaSeparationPreparation.Prepare` as a new parameter. Apply never re-reads it, because apply never re-derives dispositions. The field is not a consent flag in the `HostLifecycleCapability` sense. A value above 0 is the consent.

## Inspector

`RowContents` gains one `GUIContent` at index 3, after `MaterialCoverageRowContent` and before `TextureClampRowContent`. `DrawAlphaSeparatorSettings` gains one direct `PercentSlider` call with the new field and content, sitting between the `DrawCoverageSlider` call and the `DrawAlphaPolicyControls` call. No new wrapper method: `DrawAlphaPolicyControls` already calls `PercentSlider` directly three times. The shared label width re-measures itself.

New label: `Minimum Opaque Coverage to Preserve (Per Material)`.

New tooltip, exact text:

> AMUSE splits a mixed material. The proven opaque triangles move onto a separate opaque material. Each split adds one draw call. Some materials are almost fully opaque. A split for such a material can cost more than it saves. A material whose unproven share sits below this value skips the split. It moves onto the opaque material in full. No proof is needed for the move. Triangles that would keep their transparency lose it. The share counts the triangles of one submesh. A material slot that binds several submeshes flattens only when every one of its submeshes sits below this value. This setting works together with Minimum Opaque Coverage to Convert (Per Material). The convert setting decides whether a split is big enough to pay for. This setting decides whether a material is opaque enough to skip the split. The value 0 never flattens a material. It is the default.

## Build pipeline changes

The fold pass runs at the top of `AlphaSeparationPreparation.Prepare`, after the plan is built and before the anisotropy reporting loop. It works in two steps.

1. Decide. For every material slot binding index, collect the plan entries that share it. The slot qualifies when every entry has disposition `Split` and every entry's transparent share sits strictly below the preserve value:

```
totalTriangles = opaqueOrdinals.Count + transparentOrdinals.Count
qualifies when: transparentOrdinals.Count * 100
                < preservePercent * totalTriangles
```

2. Fold and rebuild. For each qualifying slot, every entry becomes a folded `SubmeshSeparationPlan`: disposition `WhollyOpaqueCandidate`, every triangle ordinal in the opaque list, an empty transparent list. The pass records the unproven count per folded submesh in a local map. It then calls the new Analysis entry point, which returns a new `MeshSeparationPlan` over the folded entries with recomputed aggregates. Every later consumer, including the anisotropy loop, the candidate loop, apply, and the transient unlock passes, sees an ordinary `WhollyOpaqueCandidate` entry. No candidate-loop gate code changes.

`HasAnyOpaqueCandidates` is invariant under the fold, because folding only moves triangles from the transparent count to the opaque count, and a plan with no opaque candidates holds no `Split` entries to fold. A below-threshold submesh whose unproven triangles follow anisotropic sampling flattens without the `AnisotropyDifferences` hint. That is intentional: the preserve value is the consent for the unproven move, so the acceptance hint would be noise.

## Report event

`AmuseReports` gains one method, `SlotSeparationForcedOpaque`, with the renderer, the slot index, the unproven triangle count, and the optional renderer name. `AmuseReportStrings` gains the key `amuse.slotSeparation.ForcedOpaque` with the full three-part shape: the base message, a `:description` sentence, and a `:hint`. The base message names the renderer, the slot, and the unproven count. The description states that the triangles moved onto the opaque material without opacity proof because the material's unproven share sits below the `Minimum Opaque Coverage to Preserve` setting. The hint tells the user to lower that setting on the AMUSE Avatar Optimizer component to keep this material's transparency. The event fires in the candidate loop beside the divergence and premultiply disclosures, after the entry joins the candidate slots, once per folded surviving submesh.

Two existing disclosures carry a false premise for folded slots, because their sentences say the moved triangles are `proven-opaque`. Their shared premise word becomes `opaque`, which is true for folded and genuine candidates alike. The premultiply disclosure's exact-color sentence stays true only for genuinely proven slots, so the premultiply disclosure gains a folded variant under the key `amuse.slotSeparation.PremultiplyNormalizationFolded`, selected by a folded flag on the report call, that names the preserve setting instead of claiming exact colors. The plan reads `AmuseReportStrings.cs` first and matches its exact entry shape for all new entries.

Two counter doc comments carry the same stale premise. The `AppliedOpaqueTriangleCount` declaration doc at `AmusePlatformFinishPlugin.cs:67` and the `CountOpaqueTriangles` doc at `AlphaSeparationApply.cs:896` change from `proven-opaque rendering` to `opaque rendering`. `CountOpaqueTriangles` also accumulates over every candidate whose binding index matches a changed slot, instead of stopping at the first, so two folded submeshes of one slot both count.

## Invariants

1. A preserve value of 0 produces plans, refusals, reports, and writes identical to the base commit for identical input. The avatar summary's per-renderer triangle count is the one exception: the counter fix in the Report event section counts every surviving candidate of a changed slot instead of the first, which can raise the count for a slot that binds several submeshes even at preserve 0. The base behavior undercounted that slot.
2. An `Unchanged` submesh never flattens, and its presence blocks the fold for its whole slot. Its transparent share is 100 percent.
3. A share exactly at the threshold does not flatten. Only a strictly smaller share qualifies.
4. Unknown triangles count as unproven and raise the share. More uncertainty never increases flattening. Adding an unknown triangle can never turn a non-qualifying submesh into a qualifying one.
5. A qualifying slot folds all of its submeshes together. A non-qualifying or partially qualifying slot folds none. A folded entry follows the exact `WhollyOpaqueCandidate` path: in-place material swap, curve rewrite through the same mapping, no appended slot, no mesh clone unless a sibling split survives.
6. The forced report fires once per folded submesh that survives preparation. It never deduplicates across submeshes of one slot. A slot refused for a marker clip, a conversion cause, or a renderer-wide cause produces no forced event.
7. The four label renames change display strings, doc comments, and one exception message only. No serialized field name, property name, or JSON key changes, so old saved components deserialize unchanged with the field default 0.
8. A preset file without schema version 3 refuses with a named refusal and never half-applies.

## Label renames

| Current | New |
|---|---|
| `Minimum Opaque Coverage (Per Material)` | `Minimum Opaque Coverage to Convert (Per Material)` |
| `Alpha Upper Clamp (Per Texture)` | `Alpha Upper Clamp to Convert (Per Texture)` |
| `Minimum Opaque Coverage (Per Polygon)` | `Minimum Opaque Coverage to Convert (Per Polygon)` |
| `Alpha Upper Clamp (Per Polygon)` | `Alpha Upper Clamp to Convert (Per Polygon)` |

Sites: the four label literals in `AmuseAvatarOptimizerEditor.cs`, the four label quotes in the runtime doc comments, the plugin doc comments at lines 1134, 1182, and 1209 to 1210, the exception message at line 1238, and the test input string at `AmuseAvatarOptimizerEditorTests.cs:170`. The test input sites at lines 109 and 143 carry popup labels and stay unchanged.

## Dependencies

None. No new packages, no new assembly references, no new production types. All new members live on existing types in existing files.

## Test strategy

Tests run in the Unity Test Runner, EditMode. A filtered run that reports zero tests is a failure. Behavior changes observe RED first against a named wrong implementation, then GREEN with the minimal fix. An assertion that passes on first run is recorded as characterization.

Per class:

- `AmuseAvatarOptimizerTests`: `DefaultMinimumOpaqueCoverageToPreservePercentIs0` and `MinimumOpaqueCoverageToPreservePercentSerializedPropertyRoundTrips`, mirroring the coverage pair. Characterization for the default; the round-trip guards serialization.
- `OptimizerPresetTests`: the fixture gains the twelfth field at 0; the flip table gains a row; count comments say twelve.
- `PresetParserTests`: the valid fixture gains the key; `ValidSchemaParsesAllTwelveValues`; a `WithPreserveCoverage` helper; out-of-range and missing-key refusals for the new key; RED test `SchemaVersionTwoRefusesAsUnknown` written first against the version 2 parser, then GREEN after the bump.
- `PresetFileStoreTests`: `ValidBody` gains the key; shipped names, order, and count pins unchanged; the real-file load fails if the JSON and parser land out of order.
- `PresetApplierTests`: RED first, `ApplyingShippedSafeRevokesBothAcceptanceSettings` fails after `safe.json` flips, observed and recorded; then the test is rewritten as `ApplyingShippedSafeGrantsBothAcceptanceSettings`. `ApplyWritesAllElevenFields` becomes `ApplyWritesAllTwelveFields`.
- `AlphaSeparationPreparationTests`: the fold gate suite. The fixtures:
  - Below threshold, one slot, one submesh: folds, disposition `WhollyOpaqueCandidate`, ordinals folded, one forced event.
  - At threshold: stays `Split`, no event.
  - Zero: never folds; the rebuilt plan equals the planner output.
  - `Unchanged` sibling blocks the slot: an `Unchanged` submesh on an otherwise qualifying slot folds nothing.
  - Unknown-heavy submesh: 1 proven-opaque, 4 must-remain-transparent, 95 unknown at preserve 5. Share 99, does not qualify.
  - Mixed slot: submesh A at 98 percent proven-opaque and submesh B at 50 percent on one slot at preserve 5. Nothing folds. A per-submesh fold implementation folds A and fails.
  - Two folded submeshes of one slot: both fold and the events carry each submesh's own unproven count.
  - Folded slot refused by a marker clip: marker refusal recorded, zero forced events.
  - Folded slot with premultiply normalization: the folded premultiply variant fires, not the exact-color sentence.
  - Rebuilt plan: `RequiresAnySplit` false after a full fold, true when a genuine split survives. RED first: the stale-aggregate implementation fails this assertion.
- `AlphaSeparationApplyTests`: end to end, a fully folded renderer swaps materials in place, rewrites its curves, adds no appended slot, and creates no clone. A renderer with one folded slot and one genuine split slot still clones for the split and keeps both materials honest. A folded slot whose opaque mapping equals the live material produces no summary count.
- `AmuseReportsSlotTests` and `AmuseReportStringsTests`: the new three-part message names the renderer, slot, count, and cause. The reworded `proven-opaque` premises and the folded premultiply variant pin here.
- `AmuseAvatarOptimizerEditorTests`: the renamed input string at line 170. No drawn-label assertions exist, and none are added.

Falsifiers, numbered. Each names a plausible wrong implementation the test must fail.

- --- Falsifier 1: A strict-numerator gate counts only `MustRemainTransparent` triangles. The unknown-heavy fixture reads 4 percent and qualifies, so it flattens where the bucket reading does not. The does-not-qualify assertion fails it. ---
- --- Falsifier 2: A per-submesh fold that ignores slot-mates folds submesh A of a mixed slot and leaves sibling B split on the shared index, where the material write and curve rewrite destroy B's conserved transparency. The slot-uniform rule folds nothing. The no-fold and material-unchanged assertions fail it. ---
- --- Falsifier 3: An at-or-below boundary gate flattens a submesh whose share equals the threshold. The at-threshold test asserts `Split` survives, so the boundary gate fails. ---
- --- Falsifier 4: A per-slot deduplicated report emits one event for two folded submeshes of one slot. The two-event assertion with per-submesh counts fails it. ---
- --- Falsifier 5: A gate that folds `Unchanged` submeshes, or that lets an `Unchanged` sibling ride a qualifying slot, fails the unchanged-blocks test. ---
- --- Falsifier 6: An Analysis entry point that returns the original aggregates, or a Build-side rewrite that never recomputes them, fails the preparation assertion that `RequiresAnySplit` is false after a full fold and true when a genuine split survives. ---
- --- Falsifier 7: A fold-time report emission claims a move for a slot that a marker clip then refuses. The zero-forced-events assertion on the marker-clip fixture fails it. ---

## Documentation

`README.md` changes in three places.

Use section preset bullet, replacement text:

> Safe turns on both anisotropy acceptance settings. It still disables **Ignore Out-of-Range Material Slots** and **Allow Depth Test Change on Moved Triangles**, and it never flattens a mixed material. Normal and Aggressive enable all four settings. Normal flattens a nearly opaque material when fewer than 5 percent of its triangles are unproven. Aggressive uses 10 percent. The other preset values stay the same.

Proof modes section, replacement text:

> New components leave both toggles off. The Safe, Normal, and Aggressive presets turn them on.

Preset migration paragraph, full replacement text:

> Custom preset files use schema version 3. To migrate a version 2 file, set `"schemaVersion": 3` and add `"minimumOpaqueCoverageToPreservePercent"` to its settings. To migrate a version 1 file, set `"schemaVersion": 3`, add both anisotropy acceptance keys and the new preserve key, and keep the version 2 acceptance values you want.

Preset descriptions, exact text:

- Safe: `Accepts desktop and mobile anisotropy differences. It does not allow slot or depth-test exceptions, and it never flattens a mixed material.`
- Normal: `Accepts desktop and mobile anisotropy differences, slot exceptions, and depth-test changes. It flattens a nearly opaque material to save a draw call.`
- Aggressive: `Accepts desktop and mobile anisotropy differences, slot exceptions, and depth-test changes. It flattens more materials and admits more texels than Normal.`

Anisotropy tooltip sentences, both toggles, replacement for `A shipped preset turns this setting off.`:

> The Safe, Normal, and Aggressive presets turn this setting on.

Historical records under `docs/superpowers/` are never retro-edited. The investigation, this spec, and the plan are the new dated records.

## Acceptance criteria

1. The inspector shows the four renamed labels and the new slider below the convert slider, range 0 to 100, default 0, inside the advanced Alpha Separator Settings foldout.
2. A qualifying slot flattens per invariants 2 to 5, with one report event per folded surviving submesh, and the event names renderer, slot, unproven count, cause, and setting.
3. A preserve value of 0 reproduces base behavior exactly.
4. The three preset files parse at version 3 with the new values, Safe grants both acceptances, and a version 2 file refuses with `UnknownSchemaVersion`.
5. Applying each preset writes all twelve mapped fields and nothing else.
6. All four rename sites listed in the rename section carry the new strings, and every serialized name is unchanged.
7. No user-facing sentence claims that a folded slot's moved triangles are proven-opaque or exact-color, including the divergence and premultiply disclosures.
8. The full `Alrauna.Amuse.Tests.Editor` assembly passes with observed counts, and the report names any characterization runs.

## Validation and stop conditions

Validation runs through the Unity Test Runner in EditMode after an asset refresh: first the focused new-test filters, then the full `Alrauna.Amuse.Tests.Editor` assembly, then `Alrauna.Amuse.Research.Tests.Editor`. Observed counts land in the plan's run record. Compilation and zero-test runs do not count as proof.

Stop and return evidence when any of these appears: a failing invariant under the stated semantics, a discovery that the fold path reaches a consumer the investigation and its reviews did not check, evidence that refusal paths inside one slot are not slot-uniform at apply time, a request-shaped pressure toward per-slot aggregate shares or a new shader family, or a contradiction between this spec and the repository correctness policy.

## Authorization boundary

On 2026-10-13 the owner authorized this feature, the work branch, and one commit per finalized change: the investigation, this spec, the plan, and each implemented plan task. Staging is limited to the files a task names. Pushing, pull requests, branch deletion, and history changes stay unauthorized.

## Out of scope

Classification, the planner's existing pure analysis, the apply revalidation rules, per-slot aggregate shares, new shader families, a summary counter split, migration tooling for user preset files, changes to any file this spec or the investigation's blast radius does not name, and the pre-existing question of a genuinely whole-opaque submesh sharing a slot with a later-refused split sibling.
