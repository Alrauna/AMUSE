# Atlasing Milestone 1: Settings and Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

Privacy note: This record contains public product planning only. It contains no private asset observations, machine identifiers, or vendor source excerpts.

Date: 2026-11-19.
Status on 2026-11-19: Executable plan for Milestone 1 of `docs/superpowers/plans/2026-11-19-0-2-0-minimal-atlasing-plan.md`. The user approved the design on 2026-11-19. The user approved this plan, decisions D1 to D6, and subagent-driven execution on 2026-11-19.

**Goal:** Add the three atlas settings and a sixth build pass that applies every avatar-scope atlas gate and names each refusal. Admitted builds stop at the point where Milestone 2 adds the first atlas path.

**Architecture:** One new extension-free pass, `AtlasPass`, runs after `TransientUnlockWindowClose`. It reads the lifecycle verdict and one new flag, `SharedGatesPassed`, from `AmusePlatformFinishState`. The barrier sets that flag after the shared gates pass. The atlas pass reads its settings from the build avatar component. It never reads `PreparedAlphaSeparation`.

**Tech Stack:** C#, Unity 2022.3.22f1, NDMF, NUnit EditMode tests through the Unity Test Runner.

**Spec:** `docs/superpowers/specs/2026-11-19-minimal-atlasing-design.md`, sections 2, 3, 4 conditions 1 and 2, 11, and 14.

## Global Constraints

- Package root for every path below: `Packages/com.alrauna.amuse/`.
- Atlasing is on by default. Unset fields take their defaults through field initializers and Reset. An explicit false stays false.
- Maximum page size default 8192. A maximum page size above 8192 is not accepted.
- Protected mip level count default 1.
- Presets stay at schema five. Preset files and `PresetApplier` never read or write atlas fields.
- One consent dialog serves both features. The user decision of 2026-11-19 is that AMUSE must not add a second dialog. The existing dialog text names AMUSE and the unverified versions, not a feature, so its text does not change.
- When both switches are off, nothing is analyzed, mutated, or reported.
- Atlasing runs only on the Windows build target in 0.2.0. Other targets report one named refusal.
- Do not change alpha separation behavior when alpha separation is on. The barrier changes only where atlasing alone needs the shared gates.
- Every report string uses simple English: short active sentences, no semicolons, no contractions.
- Before any Unity MCP call, confirm that `Application.dataPath` belongs to this project. Use only the dev editor instance.
- Do not stage or commit without owner authorization. Each task ends with a review stop, not a commit.

## Plan Decisions

These rules are not literal in the design. The plan derives them from the design. The owner can change them at review.

- **D1. One shared consent dialog (user decision, 2026-11-19).** The barrier runs the shared gates when at least one feature is on: the structural graph refusal and the consent dialog. The dialog asks at most once per build. When only atlasing is on, the dialog lists the host version subjects only. Shader transfer subjects concern alpha separation only, so the barrier collects them only when alpha separation is on. Accept lets both features run. Decline, or batch mode with a subject, keeps both features idle. A granted consent never extends the atlas route tables. Atlas uses still admit only exactly attested shader rows.
- **D2. One state flag.** The barrier sets `SharedGatesPassed` after the consent decision allows the build. The atlas pass runs only when that flag is true. So the atlas pass never repeats the lifecycle, structural graph, or consent checks.
- **D3. Reports.** The barrier owns every shared report: the lifecycle refusal, the structural graph refusal, and the consent entries. The atlas pass reports only its own refusals.
- **D4. Setting ranges.** The maximum page size is valid from 1 to 8192. The protected mip level count is valid from 1 to 14. An 8192 page has 14 mip levels, so 14 protected levels is the most that can exist.
- **D5. Scope moved to Milestone 4.** Inspector controls, the inspector limit of 8192, Play-mode banner rows, and the dry run label belong to Milestone 4. In Milestone 1, atlas refusals reach the console only.
- **D6. Upload checks moved to Milestone 2.** The checks that a generated page streams and that VRChat accepts an 8192 page need a page. Milestone 2 makes the first page.

## Review Focus

1. The editor active build target differs between machines. Every atlas gate test must pin the target through `AmusePlatformFinishPlugin.ActiveBuildTargetForTests`. Task 2 owns this.
2. A refusing structural graph must give exactly one console entry, with one feature on or with both on. Task 2 owns `AnAtlasOnlyAvatarWithARefusingGraphReportsTheRefusalOnce` and the existing `AvatarAnimationRefusalReportsExactlyOneEntry`.
3. A declined consent must keep both features idle. Task 2 owns `DeclinedConsentKeepsAtlasingIdleAndSilent` and `AtlasOnlyDeclinedConsentKeepsAtlasingIdle`.
4. Applying a preset must not change the atlas fields. Task 1 owns `ApplyingAPresetKeepsTheAtlasFields`.
5. With alpha separation off, the barrier must not open the unlock window or start alpha work after the shared gates. Task 2 owns `AtlasOnlyBuildOpensNoUnlockWindow`.

---

### Task 1: Atlas settings on the component

**Files:**
- Modify: `Runtime/AmuseAvatarOptimizer.cs` (fields after `_ignoreOutOfRangeMaterialSlots`, the `Reset` body, public getters)
- Modify: `Editor/Build/PlayModeBuildStore.cs:308-334` (`ComputePolicyFingerprint`)
- Test: `Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs`
- Test: `Tests/Editor/Build/PlayModeBuildStoreTests.cs`
- Test: `Tests/Editor/Presets/PresetApplierTests.cs`

**Interfaces:**
- Produces, on `AmuseAvatarOptimizer`:
  - `public const int MaximumAtlasPageSizeLimit = 8192;`
  - `public const int MaximumProtectedMipLevelCount = 14;`
  - `public bool AtlasingEnabled { get; }`, serialized field `_atlasingEnabled`, initializer `true`, with `[ToggleLeft]`.
  - `public int MaximumAtlasPageSize { get; }`, serialized field `_maximumAtlasPageSize`, initializer `8192`.
  - `public int ProtectedMipLevelCount { get; }`, serialized field `_protectedMipLevelCount`, initializer `1`.

- [ ] **Step 1: Write the failing tests**

In `AmuseAvatarOptimizerTests`:
- Add one row to `SwitchFieldRows`: `"_atlasingEnabled", true, false, o => o.AtlasingEnabled`. The two existing row tests then cover the default and the round trip.
- `DefaultAtlasPageSettingsAre8192And1`: a new component reads `MaximumAtlasPageSize == 8192` and `ProtectedMipLevelCount == 1`.
- `ResetRestoresTheAtlasDefaults`: set `_atlasingEnabled` false, `_maximumAtlasPageSize` 4096, and `_protectedMipLevelCount` 2 through `SerializedObject`. Call `Reset` through reflection, as `ResetRestoresSeparatorSubSwitchesToTrue` does. Assert true, 8192, and 1.
- `ExplicitAtlasingOffSurvivesAJsonRoundTrip`: set `_atlasingEnabled` false. Copy with `EditorJsonUtility.ToJson` and `FromJsonOverwrite` into a second new component. Assert `AtlasingEnabled` is false.

In `PlayModeBuildStoreTests`:
- `ComputePolicyFingerprintChangesWithEachAtlasField`: take a baseline on `StandaloneWindows64`. Change each field alone and restore it: atlasing off, page size 4096, protected count 2. Each fingerprint differs from the baseline.

In `PresetApplierTests`:
- `ApplyingAPresetKeepsTheAtlasFields`: set false, 4096, and 2. Apply a parsed shipped preset. Assert that the three values stay false, 4096, and 2. This test passes on first run, so record it as characterization, not RED.

- [ ] **Step 2: Run the tests and verify that they fail**

Run in the dev editor instance: Unity Test Runner, EditMode, assembly `Alrauna.Amuse.Tests.Editor`, with filters `AmuseAvatarOptimizerTests`, `PlayModeBuildStoreTests`, and `PresetApplierTests`.
Expected: compile errors for the missing getters, or failures in every new test except `ApplyingAPresetKeepsTheAtlasFields`.

- [ ] **Step 3: Add the three fields, the getters, the two constants, and the Reset lines**

Each field gets an XML doc comment that states its default and the design section. Append the three values to the end of the `ComputePolicyFingerprint` join, so existing positions keep their order. Do not touch `OnAfterDeserialize`, `PresetApplier`, or `OptimizerPreset`.

- [ ] **Step 4: Run the same filters and verify that they pass**

Expected: every test in the three classes passes. Record the observed counts.

- [ ] **Step 5: Review stop**

Run `git diff --check`. Show the diff to the owner. Do not stage or commit.

### Task 2: The atlas pass and its gates

**Files:**
- Create: `Editor/Build/AtlasUseRefusal.cs` and its `.meta` (Unity generates the `.meta`)
- Create: `Editor/Build/AtlasPass.cs` and its `.meta`
- Modify: `Editor/Build/AmusePlatformFinishPlugin.cs` (state fields, pass registration after `TransientUnlockWindowClose.PassName`)
- Modify: `Editor/Build/AmusePlatformFinishPass.cs:259-375` (shared gates for either feature, see step 3), and `:1022` (`ActivatedOptimizer` from `private` to `internal`)
- Modify: `Editor/Build/PlatformAlphaSamplingPolicy.cs` (desktop target constant)
- Modify: `Editor/Build/AmuseReports.cs` (atlas refusal entry)
- Modify: `Editor/Build/AmuseReportStrings.cs` (atlas strings and key builder)
- Create: `Tests/Editor/Build/AtlasPassTests.cs` and its `.meta`
- Modify: `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` (mirror plugin, the structural tests near line 2740)
- Modify: `Tests/Editor/Build/AmuseReportStringsTests.cs`
- Modify: every other test mirror that copies the production pass order. Search for `TransientUnlockWindowClose.PassName` in `Tests/Editor/Build/`. Line 485 of `TransientUnlockRealLockTests.cs` is one.

**Interfaces:**
- Consumes: the Task 1 getters and constants.
- Produces:
  - `internal enum AtlasUseRefusal { None, UnsupportedBuildTarget, MaximumPageSizeOutOfRange, ProtectedMipLevelCountOutOfRange }`. Milestone 2 appends the use-scope members.
  - On `AmusePlatformFinishState`: `bool HasAtlasExecuted`, `AtlasUseRefusal AtlasRefusal`, `bool ReachedAtlasAnalysis`, and `bool SharedGatesPassed`. The doc comment of `SharedGatesPassed` lists the gates: lifecycle, the opt-in component, at least one switch on, the structural graph, and consent.
  - `internal static class AtlasPass` with `internal const string PassName = "AMUSE atlas";` and `internal static void Execute(BuildContext context)`.
  - `internal const BuildTarget DesktopTarget = BuildTarget.StandaloneWindows64;` on `PlatformAlphaSamplingPolicy`. The existing Windows `case` uses it.
  - `internal static string AtlasKey(AtlasUseRefusal cause)` returns `"amuse.atlas." + cause`.
  - `internal static void AtlasRefusal(GameObject avatarRoot, AtlasUseRefusal cause)` on `AmuseReports`. One Information entry with the avatar root as context. It throws `InvalidOperationException` on `None`.

- [ ] **Step 1: Write the failing tests**

`AtlasPassTests` drives the passes directly, as `AmusePlatformFinishPluginTests` does. Use `AvatarProcessor.ProcessAvatar(root, TestGenericPlatform.Instance)`. Seed `GenericPlatformAnimatorBindings.Instance` as the bindings. Call `AmuseStructuralGraphCheck.Execute(context)`, then `AmusePlatformFinishPass.Execute(context, facts, presenter)`, then `AtlasPass.Execute(context)`. The class owns a private `SupportedFacts(string unityVersion = "2022.3.22f1")` with NDMF and SDK versions that need no consent. The version `"2022.3.23f1"` raises one host consent subject. Every test pins the build target in `SetUp` and restores it in `TearDown`. Capture entries with `ErrorReport.CaptureErrors`. Count entries by `SimpleError.TitleKey`. The presenter counts its calls and keeps the subjects that it got.

- `AtlasOnlyBuildOnWindowsReachesAtlasAnalysis`: alpha separation off, Windows, verified host. `ReachedAtlasAnalysis` is true. `AtlasRefusal` is `None`. The presenter call count is 0. No `amuse.` entry. `AnalyzedRendererCount` is 0.
- `NonWindowsTargetRefusesAtlasingWithOneNamedEntry`, rows `StandaloneLinux64` and `Android`: alpha separation off. `AtlasRefusal == UnsupportedBuildTarget`. Exactly one `amuse.atlas.UnsupportedBuildTarget` entry. `ReachedAtlasAnalysis` is false.
- `MaximumPageSizeOutsideTheRangeRefusesWithItsName`, rows 8193 and 0: exactly one `amuse.atlas.MaximumPageSizeOutOfRange` entry.
- `MaximumPageSizeAtTheLimitIsAccepted`, rows 8192 and 1: `ReachedAtlasAnalysis` is true.
- `ProtectedMipLevelCountOutsideTheRangeRefusesWithItsName`, rows 0 and 15: exactly one `amuse.atlas.ProtectedMipLevelCountOutOfRange` entry.
- `ProtectedMipLevelCountInsideTheRangeIsAccepted`, rows 1 and 14: `ReachedAtlasAnalysis` is true.
- `AtlasOnlyUnverifiedHostShowsTheSharedDialogOnce`: alpha separation off, `"2022.3.23f1"`, the presenter returns true. The presenter call count is 1. Its subjects equal `state.Lifecycle.ConsentSubjects`. `ReachedAtlasAnalysis` is true.
- `AtlasOnlyDeclinedConsentKeepsAtlasingIdle`: alpha separation off, `"2022.3.23f1"`, the presenter returns false. Exactly one `amuse.consent.Declined` entry. `SharedGatesPassed` and `ReachedAtlasAnalysis` are false. No `amuse.atlas.` entry.
- `BothFeaturesShareOneDialog`: both on, `"2022.3.23f1"`, the presenter returns true. The presenter call count is 1. `ReachedAtlasAnalysis` is true.
- `DeclinedConsentKeepsAtlasingIdleAndSilent`: both on, `"2022.3.23f1"`, the presenter returns false. `AtlasRefusal` is `None`. `ReachedAtlasAnalysis` is false. No `amuse.atlas.` entry.
- `AtlasOnlyBuildOpensNoUnlockWindow`: alpha separation off, verified host. `context.GetState<TransientUnlockWindowState>().ConsentGranted` is false. `ReachedRendererAnalysis` is false.
- `LifecycleRefusalKeepsAtlasingSilent`: alpha separation off, `"2022.3.21f1"`. Exactly one `amuse.host.` entry from the barrier. No `amuse.atlas.` entry.
- `BothSwitchesOffReportsNothing`: both off, Linux, page size 8193, `"2022.3.23f1"`. The presenter call count is 0. No `amuse.` entry. `AtlasRefusal` is `None`. `ReachedAtlasAnalysis` is false.
- `AtlasPassRunsOnlyOnce`: a second `AtlasPass.Execute` on the same context throws `InvalidOperationException`.

In `AmusePlatformFinishPluginTests`:
- Add the atlas pass to `PlatformFinishTestPlugin` after the window close. Do the same in every other mirror that copies the production order.
- `PlatformFinishBarrierRunsAfterAnonymousOptimizingProducer` and its `VendorVRChat` twin also assert `amuse.HasAtlasExecuted` is true. The twin runs the production plugin, so it proves the registration.
- `ASwitchedOffAvatarWithARefusingGraphStaysSilentButStoresTheRefusal` also sets `_atlasingEnabled` false. The contract is now that both switches off stay silent. Update its doc text to say so.
- New `AnAtlasOnlyAvatarWithARefusingGraphReportsTheRefusalOnce`: the same fixture with only alpha separation off. `CountAvatarEntries(reports)` is 1. The barrier reports it.
- `AvatarAnimationRefusalReportsExactlyOneEntry` stays unchanged. Confirm that it runs through `PlatformFinishTestPlatform`, so that the atlas pass runs in it. If it does not, add `AvatarAnimationRefusalWithBothFeaturesReportsOnceThroughTheMirror` with that platform.
- Check every other test that sets `_alphaSeparatorEnabled` false, for example in `AaoMergedConsumptionTests.cs`. Atlasing is on by default, so the barrier now runs the shared gates in those builds. A test whose contract is "AMUSE stays silent" also sets `_atlasingEnabled` false. A test whose contract is about alpha separation only stays unchanged. Record each changed test and why.

In `AmuseReportStringsTests`:
- `EveryAtlasUseRefusalCauseHasPlainEnglishStrings`: for every member except `None`, the title, `:description`, and `:hint` keys exist.
- `AtlasRefusalThrowsOnNone`.

- [ ] **Step 2: Run the tests and verify that they fail**

Run: EditMode, assembly `Alrauna.Amuse.Tests.Editor`, filters `AtlasPassTests`, `AmusePlatformFinishPluginTests`, `AmuseReportStringsTests`.
Expected: compile errors for the missing types. After a compile stub of the enum and an empty `Execute`, the gate tests fail on their assertions. Record which wrong implementation each failure rules out.

- [ ] **Step 3: Change the barrier gates and implement `AtlasPass.Execute(BuildContext context)`**

Barrier changes in `AmusePlatformFinishPass`:

1. Line 264: return only when both `AlphaSeparatorEnabled` and `AtlasingEnabled` are false. Update the comment: the shared gates run when at least one feature is on.
2. The bindings check and the structural graph gate stay as they are. They now run for either feature. Update the comment at line 295 that names "the feature switch on".
3. Collect the shader transfer subjects and the window eligibility only when alpha separation is on. Otherwise use no transfer subjects and no window. The host subjects always join the dialog.
4. After the consent decision allows the build, set `state.SharedGatesPassed = true`.
5. Then, when alpha separation is off, return before `if (windowEligible)`. No window note, no swap-in, and no alpha work run.

Atlas pass. The order of the checks is part of the contract. The tests pin the outcomes, and this order makes them deterministic:

1. `context` null throws `ArgumentNullException`. Read the state. `HasAtlasExecuted` true throws `InvalidOperationException`. Set it true.
2. `state.Lifecycle` null throws `InvalidOperationException`, because the barrier must run first.
3. `AmusePlatformFinishPass.ActivatedOptimizer(context)` null: return.
4. `!optimizer.AtlasingEnabled`: return.
5. `!state.SharedGatesPassed`: return. The barrier reported the cause.
6. Page size outside 1 to `MaximumAtlasPageSizeLimit`: refuse `MaximumPageSizeOutOfRange`.
7. Protected count outside 1 to `MaximumProtectedMipLevelCount`: refuse `ProtectedMipLevelCountOutOfRange`.
8. `AmusePlatformFinishPlugin.ActiveBuildTarget() != PlatformAlphaSamplingPolicy.DesktopTarget`: refuse `UnsupportedBuildTarget`.
9. Set `ReachedAtlasAnalysis` true. Milestone 2 continues here.

"Refuse" means: set `state.AtlasRefusal`, call `AmuseReports.AtlasRefusal`, and return. Register the pass with `sequence.Run(AtlasPass.PassName, AtlasPass.Execute)` after the window close, extension-free. Add a comment that states why: the window close writes the final references of alpha separation, and the atlas pass reads them.

Report strings, exact copy:

| Key | Title | Description | Hint |
| --- | --- | --- | --- |
| `UnsupportedBuildTarget` | AMUSE did not atlas this avatar. | Atlasing runs only for Windows builds. Every material keeps its textures. | Build for Windows to use atlasing. |
| `MaximumPageSizeOutOfRange` | AMUSE did not atlas this avatar. | The maximum atlas page size must be from 1 to 8192. VRChat does not accept a larger texture. | Set the maximum page size to 8192 or less. |
| `ProtectedMipLevelCountOutOfRange` | AMUSE did not atlas this avatar. | The protected mip level count must be from 1 to 14. | Set the protected mip level count to a value from 1 to 14. |

- [ ] **Step 4: Run the same filters and verify that they pass**

Expected: every test passes. Record the observed counts.

- [ ] **Step 5: Review stop**

Run `git diff --check`. Show the diff to the owner. Do not stage or commit.

### Task 3: Full verification and records

**Files:**
- Modify: `docs/superpowers/plans/2026-11-19-0-2-0-minimal-atlasing-plan.md` (mark Milestone 1 with the date and the observed counts)
- Modify: `docs/superpowers/investigations/2026-11-18-atlasing-handoff.md` (one dated status line)

- [ ] **Step 1: Run the full assemblies**

Run `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` in EditMode in the dev editor instance. Expected: 0 failures. A filtered run that reports 0 tests is a failure. Read the Console for errors and record them.

- [ ] **Step 2: Update the two records**

State the date, the observed pass and fail counts, and the decisions D1 to D6.

- [ ] **Step 3: Sweep the changed documents and comments**

Search for an at sign joined to hexadecimal, drive-letter paths, home-directory paths, four-digit ports, semicolons, contractions, and unqualified `currently` or `today`. Fix every hit.

- [ ] **Step 4: Review stop**

Run `git diff --check`. Show the full diff and the counts to the owner. Do not stage or commit.

## Coverage

| Design requirement | Task |
| --- | --- |
| Section 2: one pass after `TransientUnlockWindowClose`, extension-free | 2 |
| Section 2: reads lifecycle, structural graph, never `PreparedAlphaSeparation` | 2 |
| Section 3: default on, explicit false stays false, Reset gives the defaults | 1 |
| Section 3: independent switches, one shared consent dialog, both off is silent | 2 |
| Section 3: Play mode under `MayUsePositiveMutation` | 2 (step 3, check 3) |
| Section 4 conditions 1 and 2 | 2 |
| Section 11: three fields, defaults, the 8192 limit as a named refusal | 1, 2 |
| Section 11: three setting lists | 1 (fingerprint). The dry run reads the same fingerprint. The build pass reads the getters. |
| Section 14: atlas on and alpha off without the dialog for verified versions, one shared dialog otherwise, both off silent | 2 |
| Section 14: 8193 refuses with its name, 8192 is accepted | 2 |
| Section 14: a non-Windows target refuses with one named refusal | 2 |
