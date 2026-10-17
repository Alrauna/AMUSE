# Alpha Separator Submenus Design

Date: 2026-10-16

Base: `main` at `13d01ba`. Work branch: `feat/alpha-separator-submenus`.

Investigation: `docs/superpowers/investigations/2026-10-16-alpha-separator-submenus-investigation.md`

Revision note: this spec incorporates the five-lens adversarial review of 2026-10-16 (accuracy, safety and security, architecture, ponytail, latent bugs) plus one code characterization pass. The review confirmed every static surface: component table, preset tables, inspector mechanics, parser shape, report precedent, and README claims. It rejected the first draft's pipeline seams four times, and this revision replaces them. The seams are now: arm-pure bucket caps inside capture, per-field scope selection in `GatherAlphaFields`, per-slot coverage selection in preparation and the dry run, and a serialization backfill on the component. The review also inverted one tooltip's safety direction, and this revision quotes corrected text.

## Overview

The Alpha Separator Settings foldout gains two submenus: `Transparent` and `Cutout`. The anisotropy group stays directly under the foldout. The seven existing policy rows move under `Transparent`. Four new settings appear under `Cutout`. Each cutout setting governs cutout proofing separately from its transparent counterpart. The preset schema moves to version 4 so presets control the cutout values.

The change has five parts.

1. The inspector regroups the foldout into the anisotropy group, a `Transparent` submenu with the existing seven rows, and a `Cutout` submenu with four new rows.
2. The component gains four serialized fields, a backfill marker, and a serialization callback. An upgraded component copies its own transparent values into the cutout fields once, so its proofing is identical to the base commit.
3. The build applies each arm's own values. Capture buckets pick their cap by arm. `GatherAlphaFields` scopes each field by arm. Preparation and the dry run select coverage percents per slot. Every family-blind stage stays family-blind.
4. The preset files move to schema version 4 with four new required keys, in one atomic change. Safe, Normal, and Aggressive mirror their transparent values into the cutout keys.
5. The dry run reads the same per-slot values as the build, so the preview never drifts from the plan.

## Two keys

The design uses two per-material facts, and the spec names both because conflating them caused every rejected seam in the first draft.

- The proof arm of a captured field is the captured threshold value: a field whose `CaptureThreshold` sits below one was binarized by a cutoff and proofs as cutout. A field at the 1.0 sentinel proofs through the exact-255 arm. This is a stored, per-field fact at capture time (`Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs:1269-1298`), it is already part of the field key (`Packages/com.alrauna.amuse/Editor/Host/AlphaFieldSet.cs:23-93`), and it decides proof mechanics: bucket caps and scope selection.
- The menu membership of a material is whether the material's own predicate request declares a cutoff. The predicate resolution is per material and preset-aware: Poiyomi dispatches on the live material's preset, and lilToon Multi resolves by mode (`Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:812-830`). This fact is recorded once per material at capture and decides policy: coverage gates, the preserve fold, and the report cause.

The stored family enum is never the key. It has one `Poiyomi` member for both Poiyomi menus and one `LilToonMulti` member for both Multi modes.

A cutout material whose cutoff equals exactly 1.0 declares, but its captured threshold is the 1.0 sentinel, so its field proofs through the exact-255 arm and consumes transparent mechanics, exactly as at the base commit. Its menu membership stays cutout, so its slot's coverage gates read the cutout values. Both readings are intentional and stated here so the plan cannot pick the other.

## Decisions

The investigation left ten open points. The owner closed three on 2026-10-16. This spec closes the rest.

1. The menu words map to proof arms. `Transparent` covers fields whose captured threshold sits at the 1.0 sentinel. `Cutout` covers fields whose captured threshold sits below one. Because the threshold is the capture arm, this is the same split the capture routes already make. The stored family enum plays no part.
2. The anisotropy group draws first, directly under the foldout: both acceptance toggles and the Active Sampling Proof row. This is the owner's `besides anisotropy` instruction, and the proof row is the display half of the acceptance pair.
3. The Cutout submenu holds exactly four controls. The three alpha-policy rows stay transparent-only, because every capture route forces the policy bounds inert under a declared sub-one cutoff, so their cutout twins would be dead controls. A cutout tolerance policy is separate future work that touches the capture contract. The two anisotropy acceptances stay global for the same reason as the first draft: a per-cutout acceptance needs a paired policy value and a second stale-policy comparison, and nobody asked for it.
4. Capture caps are arm-pure at the bucket. A capture bucket is keyed by source and captured threshold. A bucket below one holds only cutout-arm assignments and captures at the cutout cap. The 1.0 bucket holds only exact-arm assignments and captures at the transparent cap. One bucket therefore never sees two caps, and the cap-less stored field key cannot collapse differently-capped chains. This closes the stored-key gap the investigation named. The batch-level request cap becomes the wider of the two caps, so the readback supports both arms, and the session cache key already pairs the threshold with the cap, so the two arms cache separately.
5. The texture size minimum and the mip cap scope each field in `GatherAlphaFields`, keyed on that field's captured threshold. This is where the scope binds today: the size gate drops a field entirely, which no later stage can undo, and the cap truncates the chain. `ResolveRuntimeStates` parameters stay untouched.
6. The two coverage gates select per slot. A slot's live material carries one menu membership, recorded at capture, and the slot's pair is that material's pair. Classification already merges admitted materials by intersection, so a mixed admitted set proofs under the conjunction and cannot become more aggressive through this selection. The slot decision keeps its math; its inputs become per-slot percents selected by the caller.
7. The four cutout fields default to the transparent initial values: mip cap 4, texture size 128, coverage 25, preserve 5. A component serialized by an older version backfills all four from its own transparent values once at deserialization, so its proofing is identical to the base commit whatever the user had stored. A fresh component backfills from the initializers onto the same numbers, which changes nothing. The named assumption is Unity serialization behavior: a field absent from old serialized data takes its initializer, and `OnAfterDeserialize` runs after that fill.
8. The preset schema bumps to version 4 with the four cutout keys required, in one atomic change: the parser, the preset record, the applier, and all three shipped files land together, because one refused shipped file empties the whole preset row. A version 3 file answers `UnknownSchemaVersion`. This follows the version 1 to 2 and version 2 to 3 precedents. The owner chose preset-controlled cutout values over inspector-local values on 2026-10-16, so the pressed state stays a total match over all mapped fields.
9. The two submenus use identical row labels to their transparent counterparts. The submenu header is the scope. The cutout tooltips state that the setting governs cutout sources only. The owner chose identical labels over suffixed labels on 2026-10-16.
10. Both submenus initialize open, matching `_alphaSeparatorOpen` and the transformations foldout. The owner chose open over closed on 2026-10-16.
11. The stale README sentence about the acceptance toggles is fixed in this change, because this change already rewrites the README sections around it.

## Component state

`AmuseAvatarOptimizer` gains four serialized fields in one block after `_polygonMinimumOpaqueCoveragePercent` and before `_allowDepthTestChange`:

| Schema key | Serialized field | Property | Range | Default | Safe | Normal | Aggressive |
|---|---|---|---|---|---|---|---|
| `cutoutPreserveTransparencyMaxMipLevel` | `_cutoutPreserveTransparencyMaxMipLevel` | `CutoutPreserveTransparencyMaxMipLevel` | -1 or 0 to 10 | 4 | 4 | 4 | 3 |
| `cutoutPreserveTransparencyMinTextureSize` | `_cutoutPreserveTransparencyMinTextureSize` | `CutoutPreserveTransparencyMinTextureSize` | -1 or power of two 2 to 8192 | 128 | 128 | 128 | 128 |
| `cutoutMinimumOpaqueCoveragePercent` | `_cutoutMinimumOpaqueCoveragePercent` | `CutoutMinimumOpaqueCoveragePercent` | 0 to 100 | 25 | 25 | 25 | 25 |
| `cutoutMinimumOpaqueCoverageToPreservePercent` | `_cutoutMinimumOpaqueCoverageToPreservePercent` | `CutoutMinimumOpaqueCoverageToPreservePercent` | 0 to 100 | 5 | 0 | 5 | 10 |

Each field carries the same sentinel and range shape as its transparent counterpart. Each doc comment quotes the inspector label and states that the setting governs cutout sources only. No existing serialized field name, property name, or JSON key changes. The build reads each field once through the same defensive mappers the transparent values use.

The component also gains one serialized bool `_cutoutSettingsBackfilled`, default false, and implements `ISerializationCallbackReceiver`. `OnBeforeSerialize` is empty. `OnAfterDeserialize` returns at once when the marker is true, and otherwise copies the four transparent values into the four cutout fields and sets the marker. The callback touches only plain ints, never Unity API objects. This is the only new member kind on the runtime type, and the plan states it explicitly because the runtime file otherwise carries data and doc comments only.

Accepted window, owner decision of 2026-10-17: Unity fires no deserialization callback when a component is created. A brand-new component's first same-session batch that writes cutout values is backfilled over one time, and the marker latches in the same pass. The visible effect is one Safe preset click or one cutout slider edit that snaps back, and every later session is unaffected. The design accepts this window and documents it in the README.

## Data files

The three shipped files at `Packages/com.alrauna.amuse/Presets/` change in place. Each bumps `"schemaVersion"` to 4 and gains the four cutout keys after `polygonMinimumOpaqueCoveragePercent`, mirroring that file's transparent values as the table above shows. The preset descriptions stay unchanged, because every existing description sentence stays true.

## Parsing and refusals

`CurrentSchemaVersion` becomes 4. The four keys join `SettingKeys` as required leaves. The mip key reads through the existing mip range reader, the size key through the existing size range reader, and the two percents through `TryReadPercent`. The closed key set keeps its shape: an unknown key, a missing key, a wrong type, or an out-of-range value answers a named refusal and no preset. A version 3 file answers `UnknownSchemaVersion` before any key is read. The clamp-pair cross-field invariant is untouched. No new refusal value is needed. The cutover lands as one commit, per decision 8.

## Inspector

`DrawAlphaSeparatorSettings` keeps its header, test hook, and `ApplyModifiedProperties` placement. Its body becomes three groups in this order, owner-directed on 2026-10-17:

1. A `Transparent` foldout that draws the existing seven rows through the existing draw methods.
2. A `Cutout` foldout that draws the four new rows.
3. `DrawAcceptanceControls()`, unchanged, below the two submenus.

Both submenus use `EditorGUILayout.Foldout` with `EditorStyles.foldoutHeader` and private session bools `_transparentSettingsOpen` and `_cutoutSettingsOpen`, both initialized true. Each fires `OnFoldoutHeaderDrawnForTest` with its header text before the closed early return, mirroring the parent foldout.

`RowContents` stays one array. The four cutout contents append after `ActiveSamplingProofRowContent`, the shared label width measures the array exactly as before, and the array's doc comment names which rows each submenu draws. Nothing else consumes the array, so no second grouping exists.

`DrawMipCapPopup` and `DrawMinTextureSizePopup` gain a property path and a row content parameter and serve both submenus. `PercentSlider` already takes both, so the coverage rows call it directly. Out-of-band stored values warn and clamp exactly as the transparent rows do, through the same helpers.

The four cutout labels are identical to their transparent counterparts: `Smallest Tested Mipmap`, `Smallest Tested Texture`, `Minimum Opaque Coverage to Convert (Per Material)`, `Minimum Opaque Coverage to Preserve (Per Material)`.

The four cutout tooltips, exact text. They mirror the transparent tooltips sentence for sentence, with the cutout framing and the scope sentence added:

> Smallest Tested Mipmap: A mipmap is a smaller copy of a texture. Each copy is half the size of the copy before it. The GPU shows the small copies when the avatar is far away or small on screen. A cutout texture uses a hard cutoff. A texel at or above the cutoff is opaque. A texel below it is discarded. The cutout proof checks the copies before it moves a triangle onto an opaque material. One unproven texel in a checked copy stops the move. This setting sets the smallest copy the cutout proof checks. AMUSE ignores every smaller copy. Moving toward mip 0 can improve performance at the cost of quality at a distance. Moving away from mip 0 can improve quality at a distance at the cost of performance. Mip 0 is the full texture. A 2048 by 2048 texture renders at 128 by 128 at mip 4. The default of Mip 4 fits most viewing distances. Select All Mips to check every copy. This is the safest choice. Select a larger level when a cutout part looks solid at long distance where it should show through. Select a smaller level when AMUSE moves too few cutout triangles. This setting governs cutout sources only. The Transparent submenu holds the transparent setting.

> Smallest Tested Texture: This setting works like the cutout Smallest Tested Mipmap, but as an absolute size in texels instead of a mipmap number. A mipmap is a smaller copy of a texture. The cutout proof stops checking a texture when a copy is smaller than this size on either side. A texture smaller than this size on either side is never checked, so its triangles never move. Select All Sizes to check every copy of every texture. Select a smaller size to check more copies when a cutout part looks solid far away where it should show through. Select a larger size to check fewer copies when AMUSE moves too few cutout triangles. The default of 128 fits most viewing distances. This setting governs cutout sources only. The Transparent submenu holds the transparent setting.

> Minimum Opaque Coverage to Convert (Per Material): AMUSE moves proven opaque triangles of a mixed cutout material onto a separate opaque material. Each split adds one draw call, and draw calls cost CPU time. This setting sets the smallest share of proven opaque triangles a mixed cutout material needs before AMUSE does the split. The share counts every triangle of that material slot. Use 0 to always split when at least one triangle is proven opaque. Raise the value to skip splits that move too little. This setting governs cutout sources only. The Transparent submenu holds the transparent setting.

> Minimum Opaque Coverage to Preserve (Per Material): AMUSE splits a mixed cutout material. The proven opaque triangles move onto a separate opaque material. Each split adds one draw call. Some cutout materials are almost fully opaque. A split for such a material can cost more than it saves. A material whose unproven share sits below this value skips the split. It moves onto the opaque material in full. No proof is needed for the move. Triangles that would keep their transparency lose it. The share counts the triangles of one submesh. A material slot that binds several submeshes flattens only when every one of its submeshes sits below this value. This setting works together with the cutout Minimum Opaque Coverage to Convert (Per Material). The convert setting decides whether a split is big enough to pay for. This setting decides whether a material is opaque enough to skip the split. The value 0 never flattens a material. It is the Safe preset value. This setting governs cutout sources only. The Transparent submenu holds the transparent setting.

The transparent rows keep their exact existing labels and tooltips.

## Build pipeline changes

The finish plugin and the dry run read the four cutout properties beside the transparent ones and carry both arms' values into the proof. Five seams change. No others do.

1. Capture caps. The two caps reach the shared-bucket capture path as parameters beside the bounds. The batch request cap becomes the wider of the two caps, stamped where `WithRawMipCap` runs at the base commit. Inside `UnityMaterialEvidenceCapture`, each bucket's chain captures at its arm's cap: a bucket whose threshold sits below one captures at the cutout cap, and the 1.0 bucket captures at the transparent cap. The bucket threshold is already the bucket key, so the pick needs no new key dimension. The capturer delegate and the two `TryCaptureClosedAlphaMaterials` entry points grow the two caps, and every lambda materializing the delegate follows. The dry-run twin does the same.
2. Scope selection. `GatherAlphaFields` takes both arms' mip caps and texture size minimums. Each field scopes by its own captured threshold: below one reads the cutout pair, the sentinel reads the transparent pair. The size gate and the cap apply the field's own pair. The three call sites pass the two pairs; the structural probe passes the uncapped pair for both arms. `ResolveRuntimeStates` stays untouched.
3. Coverage selection. Preparation and the dry run select each slot's pair by the slot's live material menu membership, recorded at capture. Preparation passes the selected percents into the two gates as it does today. `SlotTransformDecision.Decide` and `DecideSlot` grow per-slot percent inputs, and the two gate signatures keep their shapes. The ladder math reads percents and never sees a family.
4. Predicate recording. Capture records one bool per material: whether the material's own predicate request declares a cutoff. `CapturedAlphaMaterial` carries it, `CaptureBatch` computes it where `AlphaPredicateRequestFor` resolves, and the coverage selection and the report cause read it.
5. Upgrade backfill. The component callback from the Component state section runs at deserialization and needs no build-side participation.

The mapping block keeps its defensive shape: an absent component yields inert values, and an out-of-band stored value clamps, exactly as the transparent values behave at the base commit. The dry-run cache fingerprint and the play-mode store gain the four cutout values.

## Reports

No new report events exist. The two existing folded-premise disclosures name the preserve setting that caused the fold. With two preserve settings, the report call carries which value caused the fold, and the string layer selects between the existing variant and a cutout variant that references the Cutout submenu's preserve setting. This follows the `PremultiplyNormalization` variant precedent. A fold caused by the transparent value produces byte-identical output to the base commit. Every other report string stays unchanged, because coverage gates still gate on proof and no moved triangle loses its provenance claim. The report layer reads only the selected percents and the fold cause, so it stays family-blind in the sense that matters: no report branches on a shader family.

## Invariants

1. A fresh component, whose four cutout values equal the transparent initializers, produces plans, refusals, reports, captures, and writes identical to the base commit for identical input.
2. A component serialized by an older version backfills the cutout values from its own transparent values once, and produces base-identical verdicts whatever the user had stored. After the backfill, presets and manual edits write the cutout fields normally.
3. A bucket below one captures at the cutout cap, and the 1.0 bucket captures at the transparent cap. Changing one arm's cap, size minimum, or coverage value never moves the other arm's verdicts, including through a texture shared between a cutout material and a transparent material, and including a cutout material whose cutoff equals exactly 1.0.
4. The planner, the classifier, apply, and the transient unlock passes read no arm values and change not at all. The slot decision's signature grows; its math does not.
5. The dry run and the build select the same per-slot values through the same shared ladder, so the preview cannot drift from the plan.
6. The inert-under-cutoff capture contract is untouched. A cutout tolerance policy remains future work.
7. The preset pressed state is a total match over all sixteen mapped fields. Applying a preset writes all sixteen and never the master switch or the reveal flag. The version 4 cutover is atomic across the parser, the record, the applier, and the three shipped files.
8. A version 3 or older preset file refuses with `UnknownSchemaVersion`, and nothing invents cutout values for it.
9. No existing serialized name changes. The one new serialized marker is the backfill bool.

## Dependencies

None. No new packages, no new assembly references, no new production types. The change grows existing signatures: the capturer delegate, the two capture entry points, `GatherAlphaFields`, `Decide` and `DecideSlot`, and the two capture wrapper lambdas. It adds one recorded bool on `CapturedAlphaMaterial` and one serialized bool on the component. The derived-evidence rebuild in `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs` keeps the recorded bool, so that file joins the blast radius.

## Test strategy

Tests run in the Unity Test Runner, EditMode. A filtered run that reports zero tests is a failure. Behavior changes observe RED first against a named wrong implementation, then GREEN with the minimal fix. An assertion that passes on first run is recorded as characterization.

Per class:

- `AmuseAvatarOptimizerTests`: a default test and a serialized round-trip test per new field, mirroring the existing pairs; the backfill suite: a legacy component serialized without the cutout fields backfills from its own transparent values, a second deserialization does not overwrite an edited cutout value, and a fresh component's backfill is a no-op on the same numbers.
- `OptimizerPresetTests`: `ShippedDefaults` gains the four fields; the flip table gains four rows; count comments say sixteen.
- `PresetParserTests`: the valid fixture bumps to version 4 and gains the four keys; the parse-all test covers sixteen values; the refusal matrix follows the existing per-shape convention: one range edge per reader shape routed through a cutout key, one missing-key representative, and `SchemaVersionThreeRefusesAsUnknown` written RED first against the version 4 parser; the clamp-pair invariant tests pass unchanged.
- `PresetFileStoreTests`: `ValidBody` gains the four keys; the shipped files load in the fixed order from the real JSON, which pins the atomic cutover.
- `PresetApplierTests`: `ApplyWritesAllSixteenFields`; applying each shipped preset writes its cutout values, Safe's preserve at 0 and Aggressive's mip cap at 3 among them.
- `AmuseAvatarOptimizerEditorTests`: new pins that the headers `Transparent` and `Cutout` draw, through the test hook with the reveal on; the cutout popups warn on out-of-band stored values; the existing header pins pass unchanged.
- Capture tests: a mixed batch with a cutout material and a transparent material sharing one texture captures two chains, the cutout arm at the cutout cap and the exact arm at the transparent cap; a cutout material whose cutoff equals 1.0 lands in the 1.0 bucket at the transparent cap; changing the cutout cap leaves the exact arm's chain levels unchanged.
- `AmusePlatformFinishPluginTests`: the mapper fan-out for the four cutout values, inert without a component, defensive out of band, exact in band; an end-to-end mixed renderer where a cutout slot and a transparent slot carry different caps, size minimums, and coverages, and each honors its own; a Poiyomi sibling pair, one cutout preset and one plain, proving under different pairs.
- `AlphaSeparationPreparationTests`: a cutout slot and a transparent slot on one renderer select their own coverage values; a fold caused by the cutout preserve value emits the cutout report variant.
- Dry-run tests: the ladder produces the build's verdicts for a mixed renderer at non-default values, so the preview cannot drift; the cache fingerprint changes when a cutout value changes, so a stale view cannot answer.

Falsifiers, numbered. Each names a plausible wrong implementation the test must fail.

- --- Falsifier 1: A shared-scope implementation applies one mip cap or one size minimum to both arms in `GatherAlphaFields`. The mixed-renderer fixture with a cutout cap of 0 and a transparent cap of 4 shows one arm over-reading or under-reading. The per-arm assertion fails it. ---
- --- Falsifier 2: A batch-uniform capture cap, stamped once per batch as at base, gives both arms one cap. The mixed-batch shared-texture fixture asserts each arm's chain level count and fails it. ---
- --- Falsifier 3: A bucket cap keyed on the predicate declaration instead of the captured threshold lets a cutoff-1.0 cutout material stamp the cutout cap into the 1.0 bucket. The exact arm's chain level count assertion fails it. ---
- --- Falsifier 4: A cross-family leak lets a cutout value move a transparent verdict, or the reverse. The isolation assertions fail it. ---
- --- Falsifier 5: A fingerprint that omits the cutout values leaves the dry-run view stale after a cutout edit. The fingerprint-change assertion fails it. ---
- --- Falsifier 6: A preset press that matches only the transparent fields presses while a cutout value differs. The sixteen-row flip table fails it. ---
- --- Falsifier 7: A parser that invents cutout values for a version 3 file answers a preset instead of `UnknownSchemaVersion`. The refusal test fails it. ---
- --- Falsifier 8: A coverage selection keyed on the stored family enum routes a Poiyomi cutout preset and a plain Poiyomi sibling to the same pair. The Poiyomi sibling fixture asserts different pairs and fails it. ---
- --- Falsifier 9: A missing backfill lets an upgraded component with transparent preserve 0 read cutout preserve 5 and flatten cutout slots the base never flattened. The backfill suite asserts equality with the transparent values and fails it. ---
- --- Falsifier 10: A dry run that keeps one coverage pair diverges from the build for a mixed renderer at non-default values. The shared-ladder test asserts equal verdicts and fails it. ---

## Documentation

`README.md` changes in three places.

Proof modes section, the navigation paragraph gains one sentence after the foldout sentence, exact text:

> The Transparent submenu holds the proof settings for transparent materials. The Cutout submenu holds the matching settings for cutout materials.

The stale toggles sentence, full replacement:

> New components start with both toggles on. The Safe, Normal, and Aggressive presets keep them on.

Preset migration paragraph, full replacement:

> Custom preset files use schema version 4. To migrate a version 3 file, set `"schemaVersion": 4` and add the four cutout keys to its settings: `cutoutPreserveTransparencyMaxMipLevel`, `cutoutPreserveTransparencyMinTextureSize`, `cutoutMinimumOpaqueCoveragePercent`, and `cutoutMinimumOpaqueCoverageToPreservePercent`. To migrate a version 2 file, first make it version 3: set `"schemaVersion": 3` and add `"minimumOpaqueCoverageToPreservePercent"`. Then make it version 4. To migrate a version 1 file, first make it version 2: set `"schemaVersion": 2` and add `"acceptDesktopAnisotropyDifferences"` and `"acceptMobileAnisotropyDifferences"`. Then make it version 3. Then make it version 4.

Historical records under `docs/superpowers/` are never retro-edited. The investigation, this spec, and the plan are the new dated records.

## Acceptance criteria

1. The Alpha Separator Settings foldout shows the anisotropy group, then the `Transparent` submenu with the seven existing rows, then the `Cutout` submenu with the four new rows. Both submenus start open. The block stays hidden behind the Advanced Settings reveal.
2. The four cutout rows carry the identical labels, and the cutout tooltips state the cutout scope and the correct safety directions.
3. Each arm honors its own values at the capture buckets, at the field scopes, and at both coverage gates. Fresh defaults reproduce base behavior exactly, and an upgraded component backfills from its own transparent values exactly once.
4. A cutoff-1.0 cutout material and a Multi mode 2 material consume transparent mechanics; a cutout preset Poiyomi and a lilToon cutout consume cutout mechanics; and no arm's value moves the other arm's verdicts.
5. The three preset files parse at version 4 with the cutout values, applying each preset writes all sixteen mapped fields and nothing else, and a version 3 file refuses with `UnknownSchemaVersion`.
6. The dry run produces the build's verdicts on a mixed renderer at non-default values, and its fingerprint and the play-mode store include the four cutout values.
7. A fold caused by the cutout preserve value reports the cutout variant that references the Cutout submenu. A fold caused by the transparent value reports byte-identical base output.
8. The full `Alrauna.Amuse.Tests.Editor` assembly passes with observed counts, and the report names any characterization runs.

## Validation and stop conditions

Validation runs through the Unity Test Runner in EditMode after an asset refresh: first the focused new-test filters, then the full `Alrauna.Amuse.Tests.Editor` assembly, then `Alrauna.Amuse.Research.Tests.Editor`. Observed counts land in the run record. Compilation and zero-test runs do not count as proof.

Stop and return evidence when any of these appears: a failing invariant under the stated semantics, a discovery that an arm value reaches a consumer the investigation and its reviews did not check, evidence that the recorded menu membership disagrees with the resolution's own per-material request selection for any admitted source, or a contradiction between this spec and the repository correctness policy.

## Authorization boundary

On 2026-10-16 the owner authorized this feature, the work branch, and one commit per finalized change: the investigation, this spec, the plan, and each implemented plan task. Staging is limited to the files a task names. Pushing, pull requests, branch deletion, and history changes stay unauthorized.

## Out of scope

Per-cutout alpha-policy tolerance settings, per-cutout anisotropy acceptance, Poiyomi Two Pass conversion support, changes to classification, planning, apply, or the transient unlock passes, a cap dimension on the stored field key, per-material cap vectors beyond the two arms, new shader families, migration tooling for user preset files, changes to any file this spec or the investigation's blast radius does not name, and the pre-existing divergent-fixture question in the preset tests.
