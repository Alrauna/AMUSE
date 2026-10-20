# Separator sub-switches and lilToon range approval investigation

Status as of 2026-10-19. Branch at investigation time: `feature/separator-toggles-liltoon-2-4`, cut from `main` at `2354b6c`.

Privacy note: this investigation read only product code in this repository. It read no private avatar data and no Census data. Machines appear by role only.

This document records facts about the current code. It decides nothing by itself. Evidence labels:

- `[SOURCE]` A fact read in this repository at the base commit. The citation gives a repository-relative path and line numbers.
- `[INFERENCE]` A deduction from listed facts. The deduction is named.

Product and test paths are relative to `Packages/com.alrauna.amuse/` unless a path carries another root.

## 1. Purpose

The owner asked for two changes on 2026-10-19.

1. Two checkboxes below the Alpha Separator checkbox, indented, visible only when Alpha Separator is checked on: `Cutout Separator` and `Transparent Separator`. Both checked on by default. A checked-off Alpha Separator must keep governing both visibility and function.
2. lilToon versions before 2.5 are approved. The owner words this as "all updates to 2.4".

The existing Alpha Separator tooltip already promises the first feature: "A later feature adds its own switch beside this one." (`Editor/AmuseAvatarOptimizerEditor.cs:170-175`, runtime doc comment `Runtime/AmuseAvatarOptimizer.cs:22-29`.) `[SOURCE]`

`main` already admits lilToon 2.4.0 as a measured row. The approval question is therefore about versions past 2.4.0 up to, but not including, 2.5. `[SOURCE]` (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:402-420`, `Packages/vpm-manifest.json` with lilToon 2.4.0 in `dependencies` and `locked`.)

## 2. Method

Three parallel read-only investigations ran on 2026-10-19.

| Investigation | Scope | Mode |
|---|---|---|
| UI surface | Inspector draw flow, indentation and visibility conventions, editor test hooks, runtime fields, presets, modal ban | Read-only repo analysis |
| Attestation surface | Admitted set, consent path for an unlisted 2.4.x, mechanisms for range approval, pinned tests | Read-only repo analysis |
| Pipeline surface | Render-mode representation, slot flow, policy precedents, refusal vocabularies, dry run, revalidation, Multi | Read-only repo analysis |

## 3. The owner's words map to code families

| Owner phrase | Code family | Anchor |
|---|---|---|
| Two checkboxes below Alpha Separator, top level, indented | Draw site after the Alpha Separator row in `OnInspectorGUI` | (`Editor/AmuseAvatarOptimizerEditor.cs:167-175`) |
| Visible only when Alpha Separator is on | The Advanced Settings reveal is the only conditional block precedent | (`Editor/AmuseAvatarOptimizerEditor.cs:1265-1302`) |
| Checked on by default | Field initializers plus `Reset()`, as `_alphaSeparatorEnabled = true` | (`Runtime/AmuseAvatarOptimizer.cs:30-32`, `Reset()` `:226-247`) |
| Function follows the master switch | Barrier return for the master switch. Per-slot policy refusals are the slot-scope precedent | (`Editor/Build/AmusePlatformFinishPass.cs:253-256`, section 7.3) |
| lilToon before 2.5 approved | `IsAdmittedPackageVersion` and the consent subject pre-scan | (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:429-442`, `Editor/Semantics/UnityMaterialSemantics.cs:478-536`) |

## 4. UI surface facts

### 4.1 Draw flow

`[SOURCE]` `OnInspectorGUI` draws in order: header, preset row, root-placement HelpBox, the Alpha Separator toggle (`:167-175`), `DrawMaterialTransformations()` (`:177`), the Settings reveal gate (`:179-182`), build-status HelpBox (`:184-187`). The Alpha Separator row is a plain `EditorGUILayout.PropertyField` over `_alphaSeparatorEnabled` with a `GUIContent` tooltip. The checkbox-left look comes from `[ToggleLeft]` on the field plus `ToggleLeftDrawer` (`Editor/ToggleLeftDrawer.cs:13-30`), which wraps `EditorGUI.ToggleLeft` in `BeginProperty` (`:18`) and `EndProperty` (`:27`). That wrapper keeps the prefab override menu and mixed-value display.

`[INFERENCE]` The new pair belongs after the Alpha Separator block and before `DrawMaterialTransformations()`, at the top level outside every foldout. The top-level flow runs at ambient indent 0, so the pair needs one explicit indent level to read as children.

### 4.2 Indentation and visibility

`[SOURCE]` Nesting uses `using (new EditorGUI.IndentLevelScope())` throughout the editor: `DrawMaterialTransformations` body (`:207`), `DrawSettings` body (`:1249`) and its revealed block (`:1267`), three stacked scopes in `DrawAlphaSeparatorSettings` (`:791-793`, `:806`). The package's width math treats one indent level as 15 px (`:398-399`).

`[SOURCE]` The only toggle-driven show/hide precedent is the Advanced Settings reveal: `if (reveal.boolValue) { using (new EditorGUI.IndentLevelScope()) { ... } }` (`:1265-1302`). Dependent rows draw one extra level deeper.

`[SOURCE]` No explicit invalidation is needed. A toggle click writes through the property drawer and IMGUI redraws the inspector. No `Repaint()` call is tied to toggle changes.

`[SOURCE]` The visibility gate must read a value that is current inside the same GUI pass. The property drawer writes the serialized property synchronously (`Editor/ToggleLeftDrawer.cs:22-24`). The C# property `AlphaSeparatorEnabled` updates only at `ApplyModifiedProperties()` (`Editor/AmuseAvatarOptimizerEditor.cs:177`). `[INFERENCE]` Gating the pair on the C# property would lag one repaint behind the master toggle. Gating on the serialized property's `boolValue` avoids that. The record states no preference between inside or outside the existing Update and Apply pair. The design round picks the gate source and placement.

`[SOURCE]` The Alpha Separator Settings label width machinery (`AlphaSeparatorLabelWidth` `:835-847`, `AlphaPopupMinimum` 72 at `:852`) is unit-tested and scoped to that foldout. It does not constrain a top-level checkbox pair.

### 4.3 Test hooks

`[SOURCE]` Editor hooks are `internal static` delegates declared at `Editor/AmuseAvatarOptimizerEditor.cs:55-63`: `OnTransformationRowDrawnForTest` (`:55`), `OnFoldoutHeaderDrawnForTest` (`:62`), `OnOutOfRangeToggleDrawnForTest` (`:63`), plus row, helpbox, button, and object-field hooks. `OnOutOfRangeToggleDrawnForTest` (`Action<bool>`, fired `:1279-1282`) is the model for asserting a checkbox drew and with which value. No hook fired for a plain `PropertyField` at the base commit.

`[SOURCE]` `RepaintThroughHostWindow` (`Tests/Editor/AmuseAvatarOptimizerEditorTests.cs:1080-1120`) hosts a real editor in an `EditorWindow` and drives `OnInspectorGUI`. It force-opens foldouts by reflection (`:1090-1095`). Tests that need the pair visible while Alpha Separator is off must drive `_alphaSeparatorEnabled` through `SetSwitch` (`:1045`). Absence assertions use `Does.Not.Contain` on the header hook (`:145`). No test pins row order, tooltip text, or multi-object editing for the top-level rows.

### 4.4 Modal ban

`[SOURCE]` `Tests/Editor/Build/ModalUiBanTests.cs:15-28` bans three literals across the package `Editor/` tree: `EditorUtility.DisplayDialog(`, `EditorUtility.DisplayDialogComplex(`, and `EditorUtility.DisplayCancelableProgressBar(`. The single exemption is `Build/VersionConsentDialog.cs`. A checkbox pair is pure IMGUI and cannot violate the ban.

## 5. Runtime component and serialization

`[SOURCE]` `Runtime/AmuseAvatarOptimizer.cs` holds `_amuseDisabled` (`:19-20`), `_alphaSeparatorEnabled = true` (`:30-32`, property `:262`), the scalar policy fields, `_allowDepthTestChange = true` (`:179-180`), `_acceptDesktopAnisotropyDifferences = true` (`:189-190`), `_acceptMobileAnisotropyDifferences = true` (`:200-201`), `_ignoreOutOfRangeMaterialSlots = true` (`:204-205`), and `_advancedSettingsRevealed` (`:212-215`, declared with no initializer, so false by the bool default and set false in `Reset()` at `:250`). No field carries `[Tooltip]`. Tooltips live at the draw site.

`[SOURCE]` `Reset()` (`:226-247`) mirrors every initializer. New fields must be added there.

`[SOURCE]` The upgrade semantics are documented on the existing property: "The default of true keeps every avatar saved before this field behaving as before, because Unity fills a missing serialized field from the initializer." (`:256-261`.) `[INFERENCE]` Two new `bool` fields initialized to `true` therefore deserialize as checked on every previously saved scene or prefab. No backfill marker is needed for the new pair, because `true` is the wanted legacy value and the initializer already provides it.

`[SOURCE]` The `OnAfterDeserialize` backfill (`:404-415`) writes the marker `_cutoutSettingsBackfilled = true` and copies exactly the four transparent-to-cutout int fields. It touches no other bool.

## 6. Presets

`[SOURCE]` `OptimizerPreset` (`Editor/Presets/OptimizerPreset.cs:10-28`) is an 18-member record: `Name`, `Description`, and sixteen mapped policy fields. `AlphaSeparatorEnabled` is the second mapped member (`:12-13`, property `:32`). `Matches` compares exactly the sixteen mapped fields (`:62-96`, master switch term `:71`). `PresetApplier.Apply` writes `_alphaSeparatorEnabled` first (`Editor/Presets/PresetApplier.cs:19-20`) and covers exactly the sixteen mapped fields. `PresetParser` is fail-closed against closed key sets: `FeatureKeys = { "alphaSeparator" }` (`Editor/Presets/PresetParser.cs:23`), 15 `SettingKeys` (`:25-42`), `EnsureClosedKeySet` rejects unknown keys (`:94-96`, `:178`), schema version 4 with hard equality (`:18`, `:81`). Shipped files carry `"features": { "alphaSeparator": true }` at line 6 of `Presets/safe.json`, `Presets/normal.json`, and `Presets/aggressive.json`.

`[SOURCE]` Adding the pair to presets requires: new feature keys, `TryRead` calls, record fields, `Matches` terms, applier writes, shipped JSON lines, and a schema version bump to 5, because both directions of the closed key set refuse mixed versions. The Presets suite pins the sixteen-field shape: the flip table and `ShippedDefaults` (`Tests/Editor/Presets/OptimizerPresetTests.cs:60-66`), `ApplyWritesAllSixteenFields` (`PresetApplierTests.cs`), `ValidSchemaParsesAllSixteenValues` and the legacy-body `string.Replace` needles (`PresetParserTests.cs`), and `ValidBody` (`PresetFileStoreTests.cs`).

`[INFERENCE]` Leaving the pair out of presets keeps their stored values across preset apply and reset. The master switch precedent argues for inclusion. The minimal-diff argument argues for exclusion. This is a design decision, not a code constraint.

## 7. Pipeline facts

### 7.1 Render-mode representation

`[SOURCE]` No opaque/cutout/transparent enum exists. Mode is represented three ways:

- `CapturedAlphaMaterialFamily` (`Editor/Semantics/UnityMaterialSemantics.cs:14-40`): `Unsupported, Poiyomi, PoiyomiTwoPass, LilToon, LilToonCutout, LilToonTransparent, LilToonMulti`. For lilToon the family is the mode. Poiyomi collapses cutout and transparent into one family (documented `:88-96`).
- `CapturedAlphaMaterial.PredicatesDeclareCutoff` (property `:93`, stored `:160`, computed `:403-421`, re-derived for resolved Multi `:564-568`). This is the only cross-family cutout provenance per material. The doc states the stored family alone cannot answer it.
- `EffectiveRenderState` (`Editor/Semantics/EffectiveRenderState.cs:10-17`): live `renderQueue` and `RenderType` tag. The type's own doc states neither fact belongs in the evidence request. The eligibility evaluators receive it as a parameter (`Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs:124,167,172-177`, `Editor/Semantics/LilToon/LilToonTransparentSourceEligibility.cs:167,240-245`). The live reads sit at `Editor/Build/AlphaSeparationMaterialConverter.cs:57,111,203`, `Editor/Semantics/CanonicalOpaqueVerification.cs:54`, and `Editor/DryRun/AlphaSeparationDryRun.cs:1308`. The state is never captured into evidence.

`[SOURCE]` Provenance at decision time is per material, not per plan entry. The verdict ladder is deliberately mode-blind (`Editor/Analysis/SlotTransformDecision.cs:64-67`). Cutout membership is reattached per slot through `CoverageVectorsBySlot` (`SlotTransformDecision.cs:157-190`), keyed on the slot's current material's `PredicatesDeclareCutoff`, and travels into preparation as `cutoutMembershipBySlot` (`Editor/Build/AlphaSeparationPreparation.cs:114-115`, consumed `:691-693`, `:711-713`).

`[SOURCE]` The existing per-arm policy structure already splits the world by this same signal. The Transparent submenu and Cutout submenu carry separate mip caps, texture floors, and coverage sliders, and `CoverageVectorsBySlot` picks the arm by cutoff membership.

`[INFERENCE]` The owner's two switches plausibly map onto this existing split: a slot whose current material declares cutoff belongs to the Cutout Separator, and every other separable slot belongs to the Transparent Separator. Section 9 carries this as an open question.

### 7.2 Consumption sites of the master switch and the per-arm policy fields

`[SOURCE]` Build and dry-run behavior consumers of `_alphaSeparatorEnabled`, complete as of 2026-10-19:

| Site | Behavior off |
|---|---|
| `Editor/Build/AmusePlatformFinishPass.cs:253-256` | The pass returns before any analysis, mutation, or report |
| `Editor/Build/PlayModeBuildStore.cs:57` | Different policy fingerprint, so cached play-mode rows are not reused |
| `Editor/DryRun/AlphaSeparationDryRun.cs:199-204` | Dry run completes with `BannerKind.SeparatorOff`, zero renderer rows |
| `Editor/DryRun/AlphaSeparationDryRun.cs:1382-1383` | Distinct dry-run cache fingerprint |
| `Editor/Presets/*` | Stored as policy and applied, section 6 |
| `Editor/AmuseAvatarOptimizerEditor.cs:247-252` | Live fingerprint recompute gates the play-mode record display |

`[SOURCE]` The per-arm policy fields enter the build at the policy capture in the barrier: the mip, size, and coverage quartets read at `Editor/Build/AmusePlatformFinishPass.cs:406-461`, the `AlphaPolicyActive` disclosure marker at `:436-438`, and the sampling-policy select at `:447-461`. `Editor/Build/AlphaSeparationApply.cs:85-99` feeds the captured policy into `AmuseReports.AvatarSummary`. The summary names the policy only when a setting left its inert default.

`[SOURCE]` Build flow per renderer: barrier policy capture (`:449-461`), evidence capture, family and predicate selection (`UnityMaterialSemantics.cs:248`, `:403-421`), Multi resolution (`:544-571`), runtime state resolution (`Editor/Analysis/RuntimeStateResolution.cs:88-110`), per-slot refusal reporting (`AmusePlatformFinishPass.cs:634-665`), classification and plan, coverage vectors (`AmusePlatformFinishPass.cs:770-776`, dry-run twin `AlphaSeparationDryRun.cs:793-799`), and `RetainPreparedSeparation`. `AlphaSeparationPreparation.Prepare` runs the preserve fold gate (`:145-169`), anisotropy refusals (`:172-206`), and the candidate loop (`:379-473`) with the coverage gate (`:425-435`) and the marker-clip check (`:438-473`). `AlphaSeparationApply` revalidates and mutates.

### 7.3 Policy precedents

`[SOURCE]` Policy suppression has exactly three shapes as of 2026-10-19:

| Knob | Suppression reporting |
|---|---|
| Split-size threshold | `AlphaSeparationSlotRefusal.OpaqueCoverageBelowMinimum` (`Editor/Build/AlphaSeparationRecords.cs:77-83`, "a policy refusal, not a proof fact") with report strings `amuse.slotSeparation.OpaqueCoverageBelowMinimum{,:description,:hint}` (`Editor/Build/AmuseReportStrings.cs:564-572`). Dry run shows `SlotTransformOutcome.CoverageBelowMinimum` (`Editor/DryRun/DryRunRowBuilder.cs:144-164`) |
| Anisotropy acceptance | `AlphaSeparationSlotRefusal.AnisotropyDifferences` (`AlphaSeparationRecords.cs:84-93`), strings at `AmuseReportStrings.cs:573-580`, dry run "Refused" sentence. Also re-checked live at apply (`Editor/Build/AlphaSeparationApply.cs:148-157`) |
| Preserve threshold | Not a refusal. The slot folds and emits Information disclosures `ForcedOpaque*` (`Editor/Build/AmuseReports.cs:341-421`). The disclosure already carries per-arm keys selected through `causedByCutoutPreset` (`AmuseReports.cs:398-417`), fed by `cutoutMembershipBySlot` (`AlphaSeparationPreparation.cs:685-693`, `:711-713`) |
| Master switch | Build: silence, barrier return. Dry run: one banner (`BannerKind.SeparatorOff`) |

`[SOURCE]` The refusal hint strings name the existing checkbox labels verbatim (`AmuseReportStrings.cs:560-590`), for example "Enable 'Accept Desktop Anisotropy Differences'".

`[SOURCE]` No `NotRequested` or `SkippedByPolicy` value exists anywhere. The refusal vocabularies are closed: `RendererAnalysisRefusal` (`Editor/Host/RendererAlphaAnalysis.cs:15-106`, all proof facts), `AlphaSeparationSlotRefusal` (`AlphaSeparationRecords.cs:23-150`), `AlphaUnknownKind` (`Editor/Semantics/AlphaUnknownReason.cs:9-47`, every value owns a report sentence, enforced `:5-7`), `PreparationRefusal` (`Editor/Build/AmusePreparationDecision.cs:8-22`).

`[INFERENCE]` A sub-switch that suppresses separable slots is slot-scoped policy suppression. The `OpaqueCoverageBelowMinimum` precedent matches it: a new `AlphaSeparationSlotRefusal` member with its three report strings, a new `SlotTransformOutcome` member with a dry-run sentence, and inclusion in both fingerprints. The ladder is order-fixed, so the rank of a mode-off refusal against `AnisotropyDifferences` is a design decision. Section 9 carries it.

### 7.4 Candidate gate points

`[SOURCE]` The table lists where a per-mode policy could gate, what each point sees as of 2026-10-19:

| Point | Sees | Note |
|---|---|---|
| Barrier switch (`AmusePlatformFinishPass.cs:253-256`) | Component bools only | Cannot see modes. Modes exist only after capture |
| Policy capture (`AmusePlatformFinishPass.cs:406-461`) | The per-arm fields, including both acceptance toggles | Where the existing per-arm knobs enter build state |
| Coverage vector builder (`SlotTransformDecision.cs:157-190`) | Per-slot cutoff membership and percents | Build and dry run share it. Suppression here would masquerade as fold or coverage outcomes |
| Ladder (`SlotTransformDecision.cs:68-124,259-310`) | Plan and slot results | One shared function. A new outcome member reaches the dry run directly |
| Preserve fold gate (`AlphaSeparationPreparation.cs:145-169`) | Preserve percent, plan, `cutoutMembershipBySlot` | A disabled arm's fold must be suppressed, or `ForcedOpaque*` disclosures fire for a disabled arm |
| Candidate loop (`AlphaSeparationPreparation.cs:379-473`) | Per-slot coverage and conversion | Mirrors `:425-435` for a new slot refusal member |
| Conversion entry (`AlphaSeparationMaterialConverter.cs:39-62`) | Family and live render state | Reusing `OpaqueConversion*` refusals here would misname a policy refusal as a support refusal |
| Apply guard (`AlphaSeparationApply.cs:141-181`) | Live acceptance toggles versus captured policy | Scalar knobs are not re-checked at apply. Their effect is baked into the prepared plan |
| Dry run `Begin` (`AlphaSeparationDryRun.cs:193-233`) | Component bools | A new `BannerKind` needs a spec sentence first (`Editor/DryRun/MaterialTransformationRow.cs:23-31`) |
| Fingerprints (`PlayModeBuildStore.cs:50-73`, `AlphaSeparationDryRun.cs:1379-1401`) | All knobs | New switches must join, or cached rows go stale |

`[INFERENCE]` Two consumption models are open for the design round. The scalar-knob pattern consumes the switches at prepare time and bakes the result into the prepared plan, so apply needs no new guard. The acceptance-toggle pattern re-reads the switches at apply and refuses stale prepared state, which needs new `PreparationRefusal` and `AlphaSeparationSlotRefusal` members plus strings. Section 9 carries the choice.

### 7.5 lilToon Multi

`[SOURCE]` Within Multi resolution the render mode is one scalar per material: `TryReadMode` reads `LilToonProperties.TransparentMode` once (`Editor/Semantics/LilToon/LilToonMultiResolution.cs:364`), and `GateAndMap` maps the whole material onto exactly one regular family (`:300-324`). The same property is also read by the opaque-target code (`Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:145-146`, `:283-284`, write at `:271`). Multi layers such as Main2nd are feature keywords, not render modes.

`[INFERENCE]` A per-mode toggle gates a Multi material wholesale wherever family or `PredicatesDeclareCutoff` is read. It cannot split one material's slots. Mixed provenance can only arise across materials within one slot's animated admitted set, where the decision vector still keys on the single current material (`SlotTransformDecision.cs:172-183`).

## 8. lilToon attestation facts

### 8.1 Current admitted state

`[SOURCE]` `Editor/Semantics/LilToon/LilToonSourceAttestation.cs` (3087 lines) attests identity per profile: shader name and GUID, `_lilToonVersion` stamp 45f (`:363`, re-confirmed on installed 2.4.0), package name and version, pass GUID, canonical shader and pass digests, include-tree digest, render mode. `PackageVersion = "2.4.0"` (`:362`) is the newest admitted. `IncludeTreeDigest` (`:382-383`) is the 2.4.0 tree. The admitted set is a closed row array `AdmittedPackageVersions` (`:402-420`): exact rows 2.3.0 through 2.3.4 plus the newest pair. The include-tree digest is the only per-version bytes, because shader and pass assets are byte-identical across rows and reproduce on 2.4.0 (row block comment `:385-401`).

`[SOURCE]` `IsAdmittedPackageVersion` (`:429-442`) is an exact ordinal match against the rows. No comparison, no range. `IncludeDigestForVersion` (`:443-457`) returns the row digest or null. `AdmittedRows()` (`:423-426`) returns an immutable copy for first-party test and research consumers.

`[SOURCE]` `Verify` (`:1981-2095`) checks in order: shader name and GUID, format stamp, package name plus `IsAdmittedPackageVersion` (`:2039-2045`, the `UnsupportedVersion` diagnostic for a refused version), pass GUID, canonicalization provenance, then the per-version include-tree check `TryVerifyIncludeTreeForVersion` (`:2144-2178`), then shader and pass digests. A non-row version refuses at `:2042-2045` and never reaches the tree check. Inside the tree check, the packaged path delegates to `IncludeDigestForVersion`, an exact ordinal row lookup. The digest-set scan in the same function (`:2155-2167`) is keyed by digest and serves loose installs only. No nearest-row or at-or-below logic exists anywhere in the file as of 2026-10-19.

`[SOURCE]` Multi rows: `AdmittedMultiVersions` (`:2422-2432`, comment `:2416-2421`) holds exactly `("2.3.4", base, outline)` and `("2.4.0", same base, other outline)`. The comment binds version, tree, and container digest as a unit. No cross combination attests. `TryVerifyMultiContainer` (`:2187-2296`) enforces the binding in three steps: admitted version with a Multi column (`:2216-2230` with `TryMultiColumnDigest`), that version's tree (`:2232-2242`), that version's column digest (`:2255-2291`). The container digest lookup `TryResolveMultiRowDigest` (`:2390-2403`) resolves through `TryMultiColumnDigest` (`:2504-2523`), also an exact version-keyed lookup that answers false for non-row versions.

### 8.2 Consent path for an unlisted 2.4.x as of 2026-10-19

`[SOURCE]` Trace for an installed lilToon 2.4.1:

1. Host lifecycle is version-blind to shaders (`Editor/Build/HostLifecycleCapability.cs:137-243`).
2. The D8 pre-scan `UnityMaterialSemantics.CollectTransferConsent` (`Editor/Semantics/UnityMaterialSemantics.cs:478-536`) walks distinct assigned and swap-reachable materials, skips unsupported and both Poiyomi families, and captures each lilToon material.
3. `IsAttestedAlphaMaterial` (`:971-1014`) runs `Verify`. `IsAdmittedPackageVersion("2.4.1")` fails, diagnostic `UnsupportedVersion` (`LilToonSourceAttestation.cs:2042-2045`).
4. One consent subject is produced per unverified supported-family shader (`UnityMaterialSemantics.cs:505-531`). Any attestation failure for a supported family produces a subject, not only `UnsupportedVersion`.
5. The gate `VersionConsentDialog.ShouldProceed` (`Editor/Build/VersionConsentDialog.cs:19-43`) proceeds on empty subjects, refuses without asking in batch mode, and otherwise asks the presenter. One `EditorUtility.DisplayDialog` per build is the package's only modal (`:45-53`).
6. Decline is a reported total no-op (`AmusePlatformFinishPass.cs:347-364`). Grant passes `verifyIdentity=false` down the transferred analysis path (`UnityMaterialSemantics.cs:448-451`), so the material is interpreted through the current pins with no identity proof. All-Unknown is what downstream analysis sees when consent is declined.
7. Consent is never persisted (`VersionConsentDialog.cs:5-10`). `ConsentMemoryBanTests` and `ConsentSeamTests` pin this.

`[INFERENCE]` The owner experiences every lilToon patch past 2.4.0 as one dialog per build and a total no-op on decline.

`[SOURCE]` The transient unlock window sits on its own gate. Window eligibility requires a recognized locked identity whose locked original resolves through the AssetDatabase and passes the pinned Poiyomi identity (`Editor/Build/TransientUnlockAvailability.cs:66-88`, `Editor/Build/LockedMaterialIdentity.cs:190-219`, `:252-306`). The gate is the locked original's Poiyomi pin. lilToon attestation plays no role in it. `AmusePlatformFinishPass.cs:337-342` computes `windowEligible` from that gate alone.

### 8.3 Mechanisms available for range approval

`[INFERENCE]` Three mechanisms are constructible from existing parts. Their effects are deductions, named as such.

1. Version-list extension. Add measured rows per future patch version. Needs installed packages and measured digests per version under the recorded procedure. The repo holds no digest data beyond 2.4.0 (`AdmittedPackageVersions` `:402-420` and `AdmittedMultiVersions` `:2422-2432` are the only version-keyed digests, and "2.4.1" appears only as a refusal TestCase at `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:1809`). No 2.4.x patch release existed as of 2026-10-19 per the same-day investigation record. This mechanism cannot be executed from this repository for versions that do not exist yet.
2. Range rule with nearest-row pairing. Widen the version check to a bounded range such as `[2.3.0, 2.5.0)` and add new pairing logic so a non-row in-range version resolves to the greatest admitted row at or below its version, both for the tree check and for the Multi column lookup. At the base commit no such pairing exists: `IncludeDigestForVersion` (`:443-457`) and `TryMultiColumnDigest` (`:2504-2523`) are exact lookups that answer null and false for non-row versions, and a non-row version refuses at `:2042-2045` before the tree check. The pairing logic must therefore land in `IsAdmittedPackageVersion`, `IncludeDigestForVersion`, and `TryMultiColumnDigest` (with `TryVerifyMultiContainer` `:2234-2248` and `TryResolveMultiRowDigest` `:2390-2403` unchanged in shape). Effect when it exists: a 2.4.x whose bytes match a measured row attests fully and silently. A 2.4.x whose tree differs from every measured row reports `ModifiedShaderSource`, which still produces a consent subject under the section 8.2 trace, so the dialog survives exactly where bytes changed. Profile digests, GUIDs, and the stamp are version-independent pins and keep failing closed on any change. Range precedent exists in host version handling (`HostLifecycleCapability.cs:385-442` floor-and-bound lexicographic compare, `:469-475` bound routing), not in any shader frontend. The version grammar `TryParsePackageVersion` is `M.m.p` only (`:394-431`), so unparseable and prerelease forms stay refused. The transient unlock window is unaffected either way, because its gate attests only the locked original's Poiyomi identity (section 8.2).
3. Consent default change. Auto-grant or persist grants. The design as of 2026-10-19 forbids production consent behavior changes (`VersionConsentDialog.cs:5-10`, decision V8, `ConsentMemoryBanTests`). A grant skips identity verification entirely, which is broader and weaker than mechanism 2.

`[INFERENCE]` Mechanism 2 is the only one that approves future 2.4.x from this repository without weakening a byte check. Its one judgment call: a 2.4.x with changed shader bytes still asks once per build through the existing dialog. Full silence for changed bytes would be a consent default change, which is mechanism 3.

### 8.4 Pinned tests and doc surfaces a range approval touches

`[SOURCE]` Test pins:

| Test | Pin | Relation to range approval |
|---|---|---|
| `AdmittedVersionRows_AreTheMeasuredSet` (`Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:1764-1800`) | Exact row list `2.3.0` through `2.4.0` and both Multi digest rows | Unchanged if rows stay closed. The range is not a row |
| `Verify_WrongPackageVersion_IsRefused` (`:1802-1808`) | `"2.3.5"` refuses `UnsupportedVersion` | In-range floor decision changes what this pin must assert |
| `Verify_NonAdmittedVersionForms_Refuse` (`:1809-1826`) | `"2.4.1"`, `"2.5.0"`, `"2.3.10"`, `"2.4"`, `"02.4.0"` refuse | `"2.4.1"` flips under mechanism 2. `"2.5.0"` and unparseable forms stay |
| `GatheredTreeMatchingAnOlderRowWhileVersionIsNewer_IsModifiedSource` (`:1901-1931`) | Admitted version over a foreign tree = `ModifiedShaderSource` | Extends to range-admitted versions under mechanism 2 |
| Fixture mirrors (`Tests/Editor/Semantics/LilToonSourceAttestationTests.cs:190-216`) | Duplicated row constants | Unchanged if rows stay closed |
| Research gates (`Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs:79-135`) | The installed lilToon version is an exact member of `AdmittedRows()` (`:102-113`) and `AdmittedRows().Last().Version == PackageVersion` (`:80-86`) | The code stays unchanged, but an installed range-approved non-row version would fail the membership check. The gate needs a range-aware update or an explicit decision to stay row-strict |

`[SOURCE]` Doc surfaces that state supported versions: `README.md:42` still reads "lilToon 2.3.0-2.3.4" while the package `CHANGELOG.md` already says 2.3.0 to 2.4.0. The repository agent guide's overview also states the 2.3.0 to 2.3.4 range and is stale against the code as of 2026-10-19.

## 9. Open questions for the design round

1. Byte-changed 2.4.x: keep the one-dialog consent path (mechanism 2, recommended), or silence every 2.4.x regardless of bytes (mechanism 3, a production consent behavior change)? The owner's phrase "approved" needs this boundary named.
2. Range bounds and pairing: one range `[2.3.0, 2.5.0)` with nearest-row pairing, or exact rows for 2.3.x plus a `[2.4.0, 2.5.0)` range? Nearest-row pairing needs a defined answer for a hypothetical 2.3.5+ and for the range floor.
3. Transient unlock window: mechanism 2 opens the window for byte-identical unlisted 2.4.x, including locked-material conversion on grant paths that reach it. Confirm this is intended for unlisted versions.
4. Arm mapping: cutoff-declaring slots to `Cutout Separator` and all other separable slots to `Transparent Separator` (section 7.1 inference), or a family-based mapping instead?
5. Preset membership of the two switches: join `FeatureKeys` with a schema bump to 5 (master-switch precedent, section 6), or stay component-local (minimal diff)?
6. Reporting shape for suppressed slots: new `AlphaSeparationSlotRefusal` member plus `SlotTransformOutcome` member with sentences (doctrine: nothing is silent at slot scope). Names and strings to be chosen. The rank of the new refusal against `AnisotropyDifferences` in the order-fixed ladder is part of this decision, as is whether a disabled arm's slots may still emit anisotropy refusals.
7. Both sub-switches off at once: what the build reports and what the dry run shows. Slot rows for every suppressed slot, or an avatar-scoped banner in the dry run in addition?
8. Consumption model: scalar-knob pattern (consume at prepare, no apply guard) or acceptance-toggle pattern (re-read at apply, refuse stale state). Section 7.4.
9. Summary disclosure: `AlphaPolicyActive` names the policy only when a setting left its inert default (`AmusePlatformFinishPass.cs:436-438`). Decide whether a disabled arm is named there, and reword the hint strings that name checkbox labels verbatim (`AmuseReportStrings.cs:560-590`).
10. Research gate: make the installed-version membership range-aware, or leave it row-strict by decision.
11. Whether the preserve fold for a disabled arm is suppressed at the fold gate (`AlphaSeparationPreparation.cs:145-169`) so no `ForcedOpaque*` disclosure fires for a disabled arm. Both sub-switches on must reproduce the behavior at the base commit exactly.
12. Exact drawn labels, tooltips, test-hook names, refusal and outcome member names, report strings, and the visibility-gate source and placement (section 4.2).
13. Doc surface: README supported-version string (already one release stale), package CHANGELOG, and the repository agent guide's stale overview sentence.

## 10. Verification record

- 2026-10-19: written from three parallel read-only scout investigations (UI surface, attestation surface, pipeline surface), spot-integrated against the sources in this session.
- 2026-10-19: three parallel adversarial reviews of an earlier draft ran on 2026-10-19: fact verification, completeness and safety, conventions and sanitization. Together they reported 11 MAJOR and 21 MINOR findings. This version integrates all of them. The largest corrections: the record no longer claims nearest-row pairing exists (it must be built, section 8.3), the master-switch consumption list gained the policy-capture and summary surfaces (section 7.2), the transient unlock window consequence is recorded (sections 8.2 and 8.3), the research-gate effect is corrected (section 8.4), and all design statements moved out of fact sections into section 9.
- 2026-10-19: two reviewers independently re-verified the attestation section end to end, including the consent trace. Three reviewer line-range conflicts were resolved by direct reads in this session: the 15 px constant (`Editor/AmuseAvatarOptimizerEditor.cs:398-399`), `Reset()` (`Runtime/AmuseAvatarOptimizer.cs:226-247`) and the backfill (`:404-415`), and the rows test range (`Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:1764-1800`).
- 2026-10-19: corrected in place during the spec reviews, before the plan consumed this record. The transient unlock window's admission gate attests only the locked original's Poiyomi identity (`Editor/Build/LockedMaterialIdentity.cs:190-219`, `:252-306`). The earlier claim that lilToon attestation gates the window was wrong and sections 8.2 and 8.3 are amended. The `CanonicalOpaqueVerification.cs` path in section 7.1 moved from `Editor/Build/` to `Editor/Semantics/`.
