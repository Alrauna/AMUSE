# Separator sub-switches and lilToon range approval design

Status as of 2026-10-19. Branch: `feature/separator-toggles-liltoon-2-4`. This version integrates five parallel adversarial reviews (accuracy, safety, security, architecture, ponytail and latent bugs) run on 2026-10-19. Their blocking findings and the corrections are listed at the end.

Evidence base: `docs/superpowers/investigations/2026-10-19-separator-subswitches-and-liltoon-range-approval-investigation.md` (referenced as "the investigation", as corrected on 2026-10-19). This document argues from the investigation and decides what it left open.

## Owner decisions recorded 2026-10-19

1. Fully silent approval: every lilToon version in the approval range runs with no consent dialog, including versions whose shader bytes differ from every measured tree.
2. The two new switches join the preset system. Preset schema bumps 4 to 5.

## Goals

1. Two checkboxes, `Cutout Separator` and `Transparent Separator`, draw below the Alpha Separator checkbox, indented one level, visible only when Alpha Separator is on, and default checked on.
2. Each switch removes its arm from alpha separation when off. Alpha Separator off keeps governing everything: the pair hides and its stored values change no build behavior.
3. Every lilToon version from 2.3.0 up to, but not including, 2.5.0 runs with no consent dialog and full separation function, subject to the Multi limit in D3.
4. Existing behavior is unchanged for every other case: Alpha Separator on with both switches on reproduces the base-commit result, and versions outside the approval range keep the base-commit consent path.

## Non-goals

1. No Poiyomi attestation or consent work.
2. No new measured digest rows. The admitted set stays exactly the six regular rows and two Multi rows at the base commit.
3. No changes to the consent dialog itself, to the presenter contract, to batch-mode refusal, or to consent persistence. `VersionConsentDialog` is untouched.
4. No CI, packaging, or host version changes.
5. No dry-run changes for the approval range. The edit-time view keeps its strict attestation. The risk register carries this limit.

## Decisions

### A. The component pair

A1. Two serialized fields on the runtime component (`Runtime/AmuseAvatarOptimizer.cs`), placed after `_alphaSeparatorEnabled` (`:30-32`):

```csharp
/// <summary>
/// Master switch for separating cutout-sourced slots. Off means a
/// slot whose current material declares cutoff never separates. The
/// default of true keeps every avatar saved before this field
/// behaving as before, because Unity fills a missing serialized
/// field from the initializer.
/// </summary>
[ToggleLeft] [SerializeField] private bool _cutoutSeparatorEnabled = true;

/// <summary>
/// Master switch for separating transparent-sourced slots. Off means
/// a slot whose current material does not declare cutoff never
/// separates. Default true, same upgrade rule as above.
/// </summary>
[ToggleLeft] [SerializeField] private bool _transparentSeparatorEnabled = true;
```

A2. Properties `CutoutSeparatorEnabled` and `TransparentSeparatorEnabled` in the style of `AlphaSeparatorEnabled` (`:256-262`). `Reset()` gains both, set true (`:226-247`). No backfill marker: the initializer already provides the wanted legacy value (investigation section 5).

A3. Draw site: `Editor/AmuseAvatarOptimizerEditor.cs`, after the Alpha Separator block's `ApplyModifiedProperties()` (`:167-175`), before `DrawMaterialTransformations()` (`:177`). The gate reuses the property the master row already resolved and reads the serialized value, not the C# property:

```csharp
var masterSwitch = serializedObject.FindProperty("_alphaSeparatorEnabled");
EditorGUILayout.PropertyField(masterSwitch, MasterSwitchContent);
serializedObject.ApplyModifiedProperties();
if (masterSwitch.boolValue)
{
    using (new EditorGUI.IndentLevelScope())
    {
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("_cutoutSeparatorEnabled"),
            new GUIContent("Cutout Separator",
                "Turn this off to skip separation for slots whose " +
                "material uses cutout alpha. Suppressed slots are " +
                "reported as skipped."));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("_transparentSeparatorEnabled"),
            new GUIContent("Transparent Separator",
                "Turn this off to skip separation for slots whose " +
                "material uses blended alpha. Suppressed slots are " +
                "reported as skipped."));
        serializedObject.ApplyModifiedProperties();
    }
}
```

After the master row's `ApplyModifiedProperties()` the serialized object holds the fresh value, so the gate is repaint-exact (investigation section 4.2). The tooltip sentences above are pinned copy. No `Repaint()` is added (investigation section 4.2).

A4. Touched copy: the master switch's own tooltip promises "A later feature adds its own switch beside this one." (`:170-174`) and the runtime doc comment repeats the promise (`Runtime/AmuseAvatarOptimizer.cs:22-29`). This change is that feature, and the switches draw below and indented, not beside. Both sentences update to name the two sub-switches and their placement.

A5. Test hooks: `internal static Action<bool> OnCutoutSeparatorToggleDrawnForTest` and `OnTransparentSeparatorToggleDrawnForTest`, declared beside the existing hooks (`:55-63`), fired with the stored value inside the gated block, in the style of `OnOutOfRangeToggleDrawnForTest` (`:1279-1282`). The Modal UI ban is unaffected (investigation section 4.4).

### B. Presets, schema 5

B1. `FeatureKeys` gains `"cutoutSeparator"` and `"transparentSeparator"` (`Editor/Presets/PresetParser.cs:23`). `PresetParser` reads both as required booleans beside `"alphaSeparator"` through `TryRead` (`:104-106` pattern), so a missing key refuses `MissingField` (`:203-208`). Unknown keys refuse through `EnsureClosedKeySet` (`:94-96`, `:178-188`). `CurrentSchemaVersion` becomes 5 (`:18`) with the hard equality check (`:81`), so schema 4 files refuse with the existing schema message.

B2. `OptimizerPreset` gains `CutoutSeparatorEnabled` and `TransparentSeparatorEnabled` mapped members beside `AlphaSeparatorEnabled` (`Editor/Presets/OptimizerPreset.cs:10-28`). `Matches` compares both (`:69-102`). `PresetApplier.Apply` writes both directly after the master switch write (`Editor/Presets/PresetApplier.cs:19-20`). The applier doc comment updates to eighteen mapped fields.

B3. Shipped preset files gain both features as `true` in the `features` block at line 6 of `Presets/safe.json`, `Presets/normal.json`, and `Presets/aggressive.json`. Because shipped presets carry both true, a component with a switch off no longer matches any shipped preset, exactly like any other deviating policy field.

B4. Test surface, updated not weakened: the flip table gains two rows (`Tests/Editor/Presets/OptimizerPresetTests.cs:59-77`), `ShippedDefaults` gains the two members (`:10-16`), `ApplyWritesAllSixteenFields` and `ValidSchemaParsesAllSixteenValues` rename to Eighteen and gain the fields, `ValidBody` syncs (`PresetFileStoreTests.cs`), the legacy-body `string.Replace` needles track the new features lines (`PresetParserTests.cs`), and the existing pin that schema 5 refuses moves its needle to 6, because 5 becomes the current schema (the inversion test near `PresetParserTests.cs:159`).

### C. Per-arm function

C1. Arm mapping. A slot's arm is the arm `CoverageVectorsBySlot` already computes: the slot's current material's `PredicatesDeclareCutoff` true means the cutout arm, false means the transparent arm (`Editor/Analysis/SlotTransformDecision.cs:157-190`). This reuses the only cross-family provenance signal and matches the existing Transparent and Cutout submenus. A Multi material carries one mode and gates wholesale (investigation section 7.5).

C2. Whole-slot rule. The gate is per slot, keyed on the slot's current material, the same key every per-arm knob uses. A slot's animated admitted swap set may contain materials of the other family. Those members keep converting when the slot's current material is in an enabled arm. This is the base-commit behavior for mixed swap sets and is documented here as the intended reading of the switches. A suppressed-arm swap member inside an enabled slot keeps the existing identity-fallback machinery for unconverted members (`Editor/Build/AlphaSeparationPreparation.cs:562-570`).

C3. Consumption model: the scalar-knob pattern (investigation section 7.4). The switches are read at the barrier into build state beside the captured policy, consumed at preparation time, and their effect is baked into the prepared plan. `AlphaSeparationApply` gains no new guard, because suppressed slots never enter the prepared set; the apply guard keeps covering only the sampling target and the two acceptance toggles (`Editor/Build/AlphaSeparationApply.cs:141-181`). Both fingerprints gain both switches: `PlayModeBuildStore.ComputePolicyFingerprint` (`Editor/Build/PlayModeBuildStore.cs:50-73`) and `AlphaSeparationDryRun.BuildPolicyFingerprint` (`Editor/DryRun/AlphaSeparationDryRun.cs:1379-1401`), which also covers the inspector's live fingerprint recompute (`Editor/AmuseAvatarOptimizerEditor.cs:255-258`).

C4. New display outcome `SlotTransformOutcome.SeparatorOffByPolicy` (`Editor/Analysis/SlotTransformDecision.cs:12-20`). It is produced in `DecideSlot` (`:259-310`), which gains a `separatorOffBySlot` vector parameter built once beside `CoverageVectorsBySlot` from `cutoutMembershipBySlot` and the two captured switches, so the build and the dry run compute the same outcome from one shared function.

C5. Ladder rank. `AlreadyOpaque` stays first, per the pinned ladder comment that it wins over everything (`SlotTransformDecision.cs:53-62`). `SeparatorOffByPolicy` ranks second, before the preserve fold, anisotropy, coverage, and split outcomes, and fires only for slots with a candidate, non-Unchanged plan entry, mirroring the build, where an unchanged slot carries no refusal of its own (`Editor/Build/AlphaSeparationPreparation.cs:371-376`). An already-opaque slot in a disabled arm keeps its base-commit sentence and gains no policy refusal. The pinned ladder comment gains one sentence documenting the policy slot in the order.

C6. Dry-run sentence: "Skipped: the matching separator switch is off." wired as a hand-written case in `DryRunRowBuilder.SlotSentence` (`Editor/DryRun/DryRunRowBuilder.cs:138-163`) and `RowText`. The `default:` arm fails open into "Untouchable", so a test must fail if the case is removed. No new `BannerKind`: the feature itself is on, so reporting stays at slot scope. Both sub-switches off produces slot rows for every separable slot, not a banner.

C7. New slot refusal `AlphaSeparationSlotRefusal.SeparatorOffByPolicy` (`Editor/Build/AlphaSeparationRecords.cs:23-150`, "a policy refusal, not a proof fact" doc style) with report strings `amuse.slotSeparation.SeparatorOffByPolicy{,:description,:hint}` (`Editor/Build/AmuseReportStrings.cs`):

- description: "The matching separator switch is off, so this slot did not separate."
- hint: "Enable 'Cutout Separator' or 'Transparent Separator' on the AMUSE component."

One member serves both arms and the strings name both switches, a coarser disclosure than the per-checkbox hints; accepted and recorded. The hint names the checkboxes verbatim, matching the house pattern (`:552-590`). `AmuseReports.RecordRefusal` and census follow the `OpaqueCoverageBelowMinimum` precedent (`AmuseReports.cs:216-233`).

C8. Preparation wiring. The disabled-arm set is computed from `cutoutMembershipBySlot` and the two captured switches. In `AlphaSeparationPreparation.Prepare`:

1. The preserve fold gate (`:145-169`) skips disabled-arm slots, so no `ForcedOpaque*` disclosure ever fires for a disabled arm.
2. The anisotropy scan (`:172-206`) skips disabled-arm candidate slots; they record `SeparatorOffByPolicy` instead.
3. The three renderer-wide refusal gates between the scan and the candidate loop (`ConversionBindingUnrecognized` `:296-303`, `ConversionStateUnderAdditiveLayer` `:322-331`, `ConversionStateUnderUnnormalizedDirectBlendTree` `:333-341`, each via `RefuseEveryCandidateSlot`) refuse only enabled-arm candidate slots. Disabled-arm candidate slots record `SeparatorOffByPolicy`.
4. The candidate loop (`:379-473`, modeled on the coverage gate `:423-441`) records `SeparatorOffByPolicy` for disabled-arm slots before any coverage, marker-clip, or conversion work.

The dry run computes the same outcome through the shared ladder, so build and preview cannot drift.

C9. Summary disclosure. `AlphaPolicyActive` (`Editor/Build/AmusePlatformFinishPass.cs:435-438`) stays exactly what it is today: the two tolerance terms. It is not widened. A separate conditional summary sentence with a new report key names the disabled switches, appended when any switch is off and renderer analysis was reached, following the `amuse.summary.*` plumbing (`Editor/Build/AmuseReportStrings.cs:407-419`, `Editor/Build/AmuseReports.cs:744-766`). Sentence: "Some slots did not separate because a separator switch is off."

C10. Master-switch interaction. Alpha Separator off returns at the barrier before any capture (`AmusePlatformFinishPass.cs:253-256`), so stored sub-switch values are unreachable at build time. The inspector hides the pair. Both switches on reproduces the base-commit result: with both true the new checks never fire.

C11. Test surface for the pair and the arm gating, new tests unless named:

1. Runtime: both fields default true on a fresh component, both survive a serialize round trip, `Reset()` sets both true, and an old payload without the fields deserializes true.
2. Editor: each hook fires once with the stored value when the master switch is on; neither fires when it is off; the pair draws at indent level 1 below the master row through the host-window harness; tooltips pinned by copy constant if the house test style pins copy, otherwise pinned by hook count only.
3. Fingerprints: flipping either switch changes both fingerprints.
4. Ladder: an arm-off candidate slot yields `SeparatorOffByPolicy` ahead of fold, anisotropy, and coverage outcomes. An already-opaque arm-off slot stays `AlreadyOpaque`. An unchanged slot yields no outcome change.
5. Preparation: a cutout-arm slot with Cutout Separator off records the refusal and appears in no prepared plan. Same for the transparent arm. A disabled-arm slot inside a renderer that also trips the additive-layer gate records the policy refusal, not the additive refusal. No `ForcedOpaque*` disclosure fires for a disabled arm.
6. Dry run: the skipped sentence renders for arm-off slots, and the SlotSentence case-removal guard fails.
7. Summary: the new sentence appears when a switch is off and analysis was reached, and does not appear when both are on.
8. Presets: covered by B4.

### D. lilToon approval range

D1. Range predicate. `LilToonSourceAttestation` gains:

```csharp
/// <summary>
/// Owner decision 2026-10-19: every lilToon version from the floor up
/// to, but excluding, the bound is approved. Approved means the build
/// never asks and analysis runs under the current pins. Attestation
/// keeps failing closed on changed bytes. Approval bypasses only the
/// consent ask, never a digest comparison.
/// </summary>
internal static readonly string ApprovedVersionFloor = "2.3.0";
internal static readonly string ApprovedVersionUpperBound = "2.5.0";

internal static bool IsApprovedPackageVersion(string packageVersion)
```

Parsing and comparison rules, all pinned:

1. The predicate runs on the raw string and requires the canonical form: the string must equal the invariant formatting of its parsed components, with no leading zeros and no trailing component. `HostLifecycleCapability.TryParsePackageVersion` is private to the Build layer and accepts leading zeros (`Editor/Build/HostLifecycleCapability.cs:400-436`, `TryReadDigitRun` `:580-599`), so the attestation class gains its own private strict `M.m.p` parse. A grammar-equivalence test pins both parsers against a shared table of forms, so the range edges cannot drift from the host grammar.
2. Floor and bound compare as a numeric triple, never ordinally. Ordinal comparison would admit `2.10.0` below the bound and mispair `2.3.10`.
3. Null, empty, whitespace, unparseable, prerelease, four-component, and non-canonical forms answer false. A loose install carries no package version, so `HasPackage == false` never approves.

`IsAdmittedPackageVersion` keeps its exact-row semantics (`:429-442`) and is unchanged. The admitted rows, `AdmittedRows()`, and every digest constant are unchanged.

D2. Approval-aware gates and nearest-row pairing. Three version gates become approval-aware with the explicit disjunct `IsAdmittedPackageVersion(v) || IsApprovedPackageVersion(v)`, keeping every digest comparison behind them unchanged:

1. `Verify`'s package gate (`LilToonSourceAttestation.cs:2039-2046`), so an approved non-row version reaches the tree check instead of refusing `UnsupportedVersion`.
2. `TryVerifyMultiContainer` step 3 (`:2231-2243`).
3. `TryResolveMultiRowDigest`'s packaged branch (`:2390-2395`).

The pairing fallback: for a parsed, in-range, non-row version, `IncludeDigestForVersion` (`:443-457`) resolves the greatest admitted row at or below the version, scanning the regular rows as a numeric-triple-sorted list, and `TryMultiColumnDigest` (`:2504-2523`) does the same over the Multi rows. Each fallback scans its own array, so a future row insertion needs no pairing change. Effects: a 2.4.x whose bytes match a measured row attests honestly (`ModifiedShaderSource` stays the answer for a foreign tree, extending the `:1901-1931` pin). A version below the floor or at or above the bound gets null and false exactly as at the base commit. Loose installs resolve bytes through the existing digest-set scan (`:2155-2167`) and gain nothing.

D3. Consent suppression and version-keyed routing. The granted-name mechanism is name-keyed end to end (`UnityMaterialSemantics.cs:524-527`, name-only `Contains` at `:371-372`, `Editor/Build/AlphaSeparationMaterialConverter.cs:379-385`), and the transferred capturer installs only when at least one name was granted (`Editor/Build/AmusePlatformFinishPass.cs:325-329`, `:375`, `:518-536`, `:611-614`). Approval therefore cannot ride the name set. It is a parallel version-keyed arm at every gate the grant mechanism owns:

1. Pre-scan: `CollectTransferConsent` (`UnityMaterialSemantics.cs:478-536`) produces no identity-failure subject for a supported-family lilToon material whose captured package version is approved. State-refusal subjects for in-range Multi materials keep their behavior: a Multi refused on mode, keywords, clipping canceller, or overlay pass still produces its subject with the refusal words (`:505-524`). Only identity failures are suppressed. Host lifecycle subjects are untouched.
2. Capturer activation: the capturer-swap condition becomes "any granted shader name or any approved lilToon package version among the captured materials". The approved check reads the captured `LilToonSourceEvidence.PackageVersion` each site already holds.
3. Capture admission: the grant check inside the transferred capture path (`:368-375`) gains the approved-version disjunct, lilToon families only.
4. Conversion boundary: `SourceIdentityAllowed` / `IsGrantedShader` (`AlphaSeparationMaterialConverter.cs:130-133`, `:378-385`) gains the same version-keyed disjunct from the captured evidence.

Family confinement is structural and pinned: the approved disjunct is reachable only for `LilToon`, `LilToonCutout`, `LilToonTransparent`, and `LilToonMulti` families, because only those captures carry the lilToon package version. A Poiyomi or unsupported-family material can never satisfy it and keeps its base behavior. `GrantedShaderNames` stays name-keyed and is never populated by approval, so a same-named 2.5.0 asset cannot ride an approved 2.4.x version, and a test discriminates exactly that.

Multi limit, recorded: `IsAttestedAlphaMaterial` answers `MultiResolution.IsResolved` regardless of the identity bypass (`UnityMaterialSemantics.cs:984-989`), so consent never bypassed Multi identity and approval does not start. For an in-range Multi whose bytes differ from every measured row, approval removes only the ask. The material keeps its slot-scope `UnsupportedMultiState` rows and does not separate, in build and dry run alike. Nearest-row pairing is the only Multi rescue, and it covers byte-identical versions.

Net behavior: no dialog for any in-range version, interactive or batch, attested or not. Out-of-range unverified versions keep the base-commit subject, dialog, batch refusal, and decline-as-no-op path.

D4. Decline scope. A declined out-of-range subject stays a total no-op for the whole build (base-commit behavior, `AmusePlatformFinishPass.cs:341-364`), including for in-range materials in the same build. This is today's decline contract and is left unchanged.

D5. Transient unlock window: no interaction. The window's admission gate attests only the locked material's original shader through the pinned Poiyomi identity (`Editor/Build/TransientUnlockAvailability.cs:66-88`, `Editor/Build/LockedMaterialIdentity.cs:190-219`, `:252-306`). The lilToon approval range neither opens nor closes the window for any version. D5 exists to record this non-interaction so no reviewer or implementer reinvents a window link.

D6. Tests, updated not weakened:

1. Retargeted refuse pins: `Verify_WrongPackageVersion_IsRefused` (`Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:1802-1808`) and `VerifyCutout_WrongPackageVersion_IsRefused` (`:2564-2575`) move from `"2.3.5"` to below-floor and at-bound versions such as `"2.2.0"` and `"2.5.0"`.
2. `Verify_NonAdmittedVersionForms_Refuse` (`:1809-1826`): `"2.5.0"`, `"2.4"`, `"02.4.0"` stay refusals. `"2.4.1"` and `"2.3.10"` move to the new verify cases.
3. New pairing cases: `"2.4.1"` with the `2.4.0` tree verifies. `"2.3.10"` pairs the `2.3.4` row, not `2.3.1`. `"2.4.1"` with a foreign tree reports `ModifiedShaderSource`. The Multi fixtures with `"2.3.5"` (`Tests/Editor/Semantics/LilToonSourceAttestationTests.cs:470-484`) move to in-range pairing cases with the `2.3.4` columns. `Verify_AcceptsAdmittedPatchVersionWithItsOwnTree` and `Verify_VersionWithAnotherVersionsTree_IsModifiedSource` (`LilToonAttestationTests.cs:2595-2639`) gain in-range non-row twins.
4. Range grammar cases: `"2.10.0"` and `"2.3.100"` stay outside the range. `"2.4.0.1"`, a prerelease form, `"02.4.0"`, and `" 2.4.0 "` stay unapproved and refuse. The grammar-equivalence table pins the two parsers together.
5. Consent cases: an approved material produces zero subjects and separates (full-pipeline case: byte-changed in-range install, zero subjects, slots separate, no `SourceIdentity` refusal). An in-range Multi with state refusals still produces its subject. An unapproved unverified material still produces a subject. A Poiyomi material still never produces a lilToon grant. A loose install (no version) still produces a subject. An approved 2.4.x material and a same-named 2.5.0 material in one build: the 2.5.0 material produces a subject and never rides the approval. Batch mode still refuses without asking (`VersionConsentTests.cs:190-208` pin untouched).
6. Consent-shape fixtures that pin "an unattested supported-family material produces a subject" (`Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:855-860`, `Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs:908-930`, `Tests/Editor/Build/AlphaSeparationPreparationTests.cs:3028-3032`) re-pin with out-of-range versions such as `"2.5.0"` so the subject path stays tested.
7. Research gates: the installed-version check accepts a row member or an in-range version, unit-tested directly on the predicate (`Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs:101-113`), keeping the `AdmittedRows().Last().Version == PackageVersion` probe pin (`:80-86`).

D7. Docs, all stated surfaces:

1. `Packages/com.alrauna.amuse/README.md:42` supported-version sentence: 2.3.0 through any version below 2.5, with the approval rule.
2. `Packages/com.alrauna.amuse/README.md:73-84` preset schema section: schema 5 and the new keys.
3. Repository-root `README.md:42` supported-version sentence: same rule.
4. Package `CHANGELOG.md`: the approval rule and the schema bump.
5. Repository agent guide overview: the same rule replacing the stale 2.3.0 to 2.3.4 sentence.
6. `docs/architecture/vision.md:55`: the current-support sentence states the approval rule.

## Risk register

| Risk | Mitigation |
|---|---|
| An in-range version with changed shader bytes is analyzed under the current pins with no identity proof and no build-path disclosure. A false proof could move triangles that are not truly opaque. This is the accepted cost of the owner's fully silent decision, recorded 2026-10-19 | The range is `[2.3.0, 2.5.0)`, which includes never-measured hypothetical 2.3.5-2.3.9 patches paired to the 2.3.4 pins. The dry run keeps strict attestation, so the edit-time preview is the one surface that still shows the strict view. Regular-family slots preview as unknown. In-range Multi keeps slot-scope `UnsupportedMultiState` rows in both paths. Every digest comparison inside attestation still fails closed, so a future row insertion for a measured version restores full honesty without code change |
| The dry run keeps strict attestation, so a byte-changed in-range version previews as unknown while the build analyzes it | Accepted limit, stated in Non-goals. The edit-time view is conservative, never wrong about what moves |
| Preset schema bump refuses existing user preset files at schema 4 | Fail-closed with the existing schema message. Product decision recorded 2026-10-19 |
| A switch flip between prepare and apply leaves a prepared plan built under the old policy | Scalar-knob pattern accepts this window, same as the coverage knobs at the base commit. Fingerprints protect the cached views |
| Name-keyed grant leakage exists at the base commit (one granted name covers all versions of that shader name in one build) | Approval adds no name-keyed power: `GrantedShaderNames` is never populated by approval, and the version-keyed disjunct is family-confined. The D6 case pins the 2.5.0 discrimination |

## Landing order

Atomic groups the plan must respect, because partial groups go red or silently wrong:

1. Schema 5 group: parser keys, `CurrentSchemaVersion`, record members, `Matches`, applier writes, shipped JSON lines, and the B4 pins in one commit.
2. Each new enum member lands in the same commit as its strings and sentences: `SlotTransformOutcome.SeparatorOffByPolicy` with the `SlotSentence` case and `RowText` sentence, `AlphaSeparationSlotRefusal.SeparatorOffByPolicy` with its three report strings.
3. The approval group: D1 predicate, D2 gates and pairing, D3 routing, and the D6 retargets in one commit. The retargeted refuse cases fail against the old predicate, and the moved `"2.4.1"` case fails against the new one.
4. Runtime fields precede or accompany the editor and preset code that references them.
5. Research gate and docs land after the approval group.

## Halt points for the owner

1. Any existing test can only be made to pass by weakening its assertion rather than re-pinning its behavior.
2. The approval routing cannot be contained to supported-family lilToon materials and reaches Poiyomi, unsupported families, or host subjects.
3. The range predicate or pairing change would need to alter `IsAdmittedPackageVersion` row semantics, the admitted rows, or any digest constant.
4. The preset schema bump breaks a consumer outside the parser, record, applier, tests, and the README prose.
5. A full EditMode run reports 0 tests in any filtered run, or any modal appears during a run.

## Acceptance criteria

1. With Alpha Separator on and both switches on, classification, plans, reports, and mutations match the base commit on the same inputs.
2. Alpha Separator off hides the pair, and the build ignores the stored switch values end to end.
3. Cutout Separator off: every candidate slot whose current material declares cutoff records `SeparatorOffByPolicy`, the dry run shows the skipped sentence for them, no such slot moves, no `ForcedOpaque*` disclosure fires for that arm, and the transparent arm is unaffected. Transparent Separator off mirrors.
4. Any lilToon version in `[2.3.0, 2.5.0)`: no identity-failure consent subject, no dialog in interactive or batch mode, and analysis proceeds under the current pins. A byte-identical in-range version attests through the nearest-row pairing. A byte-changed in-range regular-family version still answers `ModifiedShaderSource` inside attestation, the transient unlock window is untouched for every version, and an in-range byte-changed Multi keeps `UnsupportedMultiState` slot rows.
5. `"2.5.0"`, versions below 2.3.0, and non-canonical or unparseable forms keep the base-commit consent path, including in the same build as approved materials.
6. Preset schema 5: shipped presets parse, apply writes both switches, matching requires them, and schema 4 input refuses with the schema message.
7. Research gates pass with any in-range installed version, observable by direct unit tests of the range-acceptance predicate and the widened gate, and the probe pin holds.
8. All six doc surfaces in D7 state the approval rule or schema 5.
9. The full product and research EditMode suites pass with observed counts recorded.

## Review integration record

Five parallel adversarial reviews of the first draft ran on 2026-10-19: accuracy, safety, security, architecture, ponytail and latent bugs. Blocking findings, all integrated above:

1. CRITICAL, found by all five: the nearest-row pairing was unreachable because D1 froze the three version gates that guard it. Fixed in D2.
2. CRITICAL, found by security, safety, architecture, and ponytail: the approval routing named the wrong site and missed the capturer-activation, capture-admission, and conversion-boundary gates, so the byte-changed case would have silently done nothing. Fixed in D3.
3. MAJOR, found by architecture: the transient unlock window never involves lilToon attestation. D5 rewritten as a recorded non-interaction, and the investigation corrected in place (`cb0d5b2`).
4. MAJOR, found by safety: the Multi asymmetry was unstated and contradicted the goals. Recorded as the D3 Multi limit and carried in the risk register.
5. MAJOR, found by security and accuracy: the parse helper lives in the Build layer and accepts leading zeros, which made the `"02.4.0"` pin unsatisfiable. Fixed in D1 with a Semantics-local canonical-form parse and a grammar-equivalence pin.
6. MAJOR, found by architecture: the three renderer-wide refusal gates would report additive-layer and binding refusals for disabled-arm slots. Fixed in C8.
7. MAJOR, found by architecture: mixed-arm animated admitted sets were undefined. Fixed in C2 as whole-slot gating with the documented swap-set caveat.
8. MAJOR, found by architecture and safety: `AlphaPolicyActive` could not name the new deviation and its fixed sentence misdescribes it. Fixed in C9 with a separate summary key.
9. Remaining MAJOR and MINOR findings: ladder rank versus `AlreadyOpaque` pinned in C5, `DecideSlot` named in C4, numeric comparison and boundary cases in D1/D6, schema-5 needle inversion in B4, the C test surface added as C11, README preset-schema section and vision sentence added to D7, editor fingerprint range and preset anchors re-pinned, `B1` wording corrected to attribute the two closed-set directions precisely, and the coarse two-switch hint accepted and recorded in C7.

## Decision IDs

Code comments implementing these decisions reference A1 through A5, B1 through B4, C1 through C11, D1 through D7, and point to this document.
