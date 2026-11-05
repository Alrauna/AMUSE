# Vendor-integrated test rework design

Status: 2026-10-25, third revision. The first revision had two adversarial reviews on the same date. The evidence review reported 0 CRITICAL, 3 MAJOR, and 6 MINOR findings. The soundness review reported 1 CRITICAL, 2 MAJOR, and 12 MINOR findings. The second revision applied every accepted finding. A scoped soundness re-review of the second revision on the same date returned "accept" with 2 MINOR citation findings, which the second revision also applied. This third revision applies the owner answers of the second round on 2026-10-25. Answer A1 closes owner item O1. Answer A2 makes the 30-minute suite duration a goal that never gates. Answer A3b aligns the bundled 9.3 support path. Answer A3c stops a thryeditor upgrade inside 2.74.x from failing or skipping. On the same date a third-round soundness review and a third-round evidence review returned accept with fixes. The soundness review reported 0 CRITICAL, 2 MAJOR, and 6 MINOR findings. The evidence review reported 0 CRITICAL, 0 MAJOR, and 2 MINOR findings. The fixes landed on 2026-10-25. The design turns the owner decisions of 2026-10-25 into a buildable plan for the staged rework in `docs/superpowers/investigations/2026-10-25-vendor-integrated-test-rework-investigation.md` section 8. Decision IDs use the prefix `V`. Branch at design time: `rework/vendor-integrated-tests`.

Privacy note: this record covers AMUSE source, public vendor packages, and dated test observations from the dev editor instance. It names no private avatar, machine, account, port, instance, or GUID. Vendor roots use placeholders such as `<poiyomi-toon>` and `<thryeditor>`. AMUSE paths are relative to `Packages/com.alrauna.amuse/` unless they start with `Packages/`, `docs/`, or `.github/`. Vendor paths start with their package directory under `Packages/`.

Reading guide: `[INFERENCE]` marks a conclusion from cited facts that no command checked. Owner decisions are paraphrased and never quoted. Decision text without an owner decision is design work grounded in the investigation record, and unnamed details inside it are builder choices. Vendor paths cite their package directories under `Packages/`, except Poiyomi Toon and the Thry editor, which keep the record placeholders `<poiyomi-toon>` and `<thryeditor>`.

## Intent

AMUSE tests must run against the real installed vendor packages that every VRChat avatar project uses, as pinned in `Packages/vpm-manifest.json`. Stand-in doubles drift from vendor behavior. On 2026-10-24 the vendor-integrated environment produced 31 failures, 2 inconclusive results, and 0 skips (investigation record section 3). Every failure traces to an interaction between a test fixture and an installed vendor plugin (record section 4). This design restores a green suite in that environment, converts silent drift into named failures, and adds production-order vendor coverage. It decides nothing that the owner reserved.

## Goals

1. A green full EditMode run of both test assemblies in the pinned environment, on the dated environment the record names.
2. Named environment handling. A run reports which vendor versions it ran, which optional vendors were absent, and which tests skipped for that reason.
3. The product reads structural facts from data that survives vendor copies, per decision Q3. Refusals stay meaningful when Modular Avatar processes the avatar.
4. The AAO atlas admission stops pinning the AAO version, per decision Q8, and still fails closed.
5. Production-order vendor fixtures in stage 5, per decision Q5. The 30-minute suite duration is a goal and never a gate (owner answer A2 of 2026-10-25).
6. Every falsifier that the rework retires gets a replacement that proves the same rule.
7. No false positive anywhere. A refusal is a coverage loss, never a moved unproven triangle.

## Non-goals

- Decoupling the product from the VRChat SDK. Owner decision A1, 2026-10-25: the SDK dependency stays. It closes owner item O1.
- Admitting locked Poiyomi bundled originals. That track follows `docs/superpowers/investigations/2026-10-25-bundled-locked-original-readiness-investigation.md` section 6 on its own branch.
- New CI jobs, CLI test scripts, or workflow changes. None exist and none are added by this design.
- Running product tests in the private Census Lab project. The rule stands with more force after stage 5 (record section 8, last paragraph).
- Changing vendor packages or asking vendors to change. The upstream ask stays in code comments, as spec 2026-10-04 section 8 does.
- Removing synthetic doubles that no vendor can replace. Record section 5.3 lists them and they stay.

## Anti-goals

- A green suite that is green because a vendor silently stopped running in it. A skip must name the vendor and the version, and the report must show it.
- Treating an unattributed vendor failure as an AMUSE defect, or the reverse. The harness names the failing plugin.
- Blanket catches that turn programming failures into skips. Invariant failures throw (AGENTS.md correctness policy).
- Copying vendor source or vendor assets into the repository. Tests reference installed packages only.

## Constraints

- The owner decisions of 2026-10-25 bind. The first round is recorded in decisions V1, V3, V4, V5, V6, and V13. The second round of the same date is recorded in V1, V5, and V12, and in the closed items O1 and O2.
- The canonical environment is the `locked` block of `Packages/vpm-manifest.json` (decision V2).
- Test assemblies are the two existing ones: `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` (AGENTS.md, Testing and QA).
- The product test assembly already references `VRC.SDKBase.Editor` with the `AMUSE_VRCSDK3_AVATARS` version define (`Tests/Editor/Alrauna.Amuse.Tests.Editor.asmdef:8-9, 28-30`). `[INFERENCE]` The suite is meant to compile without the SDK, because `Tests/Editor/Build/FixtureAvatarIdentity.cs:47-56` expects to skip with a named message when the SDK is absent.
- Licenses: nine vendors are MIT. VRCFury carries its own license with a personal grant and a commercial grant that forbids redistribution and modification (investigation record section 7.1). Tests reference installed packages and copy nothing. A real Thry lock writes vendor-owned cache data and makes a vendor network call. Both are disclosed and gated (V13).
- NDMF plugin-disable keys are editor session state. A fixture that fails before it restores a key removes a vendor from every later build in that session (`Tests/Editor/Build/TransientUnlockTestLifecycle.cs:88-110`, record section 7.3).
- Vendor load hooks act at domain reload before any test runs. The harness records their effects. It cannot prevent them (record section 7.3).
- Simple English in every human-read text this work touches. No machine paths, no host names, no GUID values in documents or comments.

## Approaches

The investigation record section 8 lists the shapes the slices considered:

1. **Staged vendor integration with named skips.** Chosen by the owner in decision Q1, with the reporting obligations of V1. Each stage ships alone. Every retired falsifier keeps a same-rule replacement.
2. **Ground checks only, every double kept.** Rejected. It does not remove drift, which is the stated purpose (record section 1).
3. **Replace every double at once.** Rejected. It loses falsifier grain and risks one large broken cutover.
4. **Required vendors, clone fails to compile without them.** Rejected by the owner in Q1. It contradicts the optional-dependency principle for every vendor except NDMF.

For the stage 5 harness there were two candidate shapes. Decision V5 chooses between them.

## Decisions

### V1. Enforcement mode: optional with named skips (owner Q1)

Owner decision Q1, 2026-10-25: vendors are optional with named skips. Every package outside AMUSE, except NDMF, works as an optional dependency.

The record section 8 listed optional-with-skips as a rejected shape: "a green run then no longer describes one environment, and two code paths stay alive per suite." This design answers that objection with four obligations:

1. Every skip names the missing package and the pinned version it expected. The mechanism is `Assert.Ignore` with a named reason, as `Tests/Editor/Build/FixtureAvatarIdentity.cs:47-56` already does for the SDK.
2. The suite-start check of V7 compares every installed vendor version against the `locked` block. A version mismatch is a named failure that names both versions. Drift is never a skip. Design call: the owner answer governs absence, not drift. This rule narrows "optional" to absence-only and answers the record's objection that a green run must describe one environment. Obligation 4 adds one named exception.
3. The environment record of V7 writes the effective environment of every run: which locked versions were present, which optional vendors were absent, which tests skipped with which reason. A green run then describes exactly one environment: the locked environment minus the named absent vendors.
4. Thry editor patch drift, owner decision A3c, 2026-10-25. Future 2.74.x releases are treated as free of breaking changes for now. No precedent beyond that is assumed. Tests must catch a breaking change in an AMUSE dependency. Obligation: the suite-start check neither fails nor skips a thryeditor upgrade inside the 2.74.x line, that is an installed patch version newer than the locked one with the same major and minor. It records the effective patch version, and obligation 3 writes it into the environment record. The gated real-lock suite of V13 runs against the installed version, so a breaking 2.74.x patch fails a test there. A downgrade inside the 2.74.x line and a major or minor bump, 2.75.0 or later, stay named failures under obligation 2. Design call on the downgrade: the owner assumption covers future releases, and a downgrade is not a future release. Design call on the boundary: the owner assumption covers 2.74.x only. Validation rule for a drifted patch: when the suite-start check records a thryeditor upgrade inside 2.74.x that differs from the locked version, the environment record states that no gated real-lock run has yet exercised that version. The new patch counts as validated only after one dated run of the gated real-lock suite passes against it, and the environment record holds that dated pass. Design call: the canonical validation run does not set the real-lock gate by default. The real lock writes project-level state and makes a vendor network call (V13). A default validation run must not write project state or make a vendor network call, so the default run stays the validation of the suite, and the dated gated run is the validation of the new patch version. Design call: every vendor other than the Thry editor keeps obligation 2 unchanged. No owner assumption of compatibility exists for them, so a version mismatch must stay a named failure. A suite that treats an unassumed mismatch as compatible could run against a breaking version that no test targets. This design adds no compatibility handling for a vendor that has no owner assumption.

Rationale: it matches the owner principle, and the reporting obligations restore the property the record missed. `Assume.That` is banned for environment gating. An unmet assumption reports as inconclusive, which is exactly the ambiguity of record section 9 question 9.

### V2. Canonical environment (design call, owner Q2 unanswered)

The canonical environment for recorded test counts is the `locked` block of `Packages/vpm-manifest.json`, which starts at line 37. The top-level `dependencies` block is not canonical. The two blocks already disagree: `dependencies` says lilToon 2.4.0 (`Packages/vpm-manifest.json:15-16`) while `locked` says 2.4.1 (`:58-59`), and the record section 2 describes the environment as 2.4.1. The `locked` block carries the resolved truth. A thryeditor upgrade inside the 2.74.x line, an installed patch newer than the locked one, does not make a run non-canonical. The environment record names the effective patch version and its validation state (V1 obligation 4, owner answer A3c of 2026-10-25).

### V3. The event refusal reads data that survives Modular Avatar copies (owner Q3, details are design calls)

Owner decision Q3, 2026-10-25: the product reads structural facts from data that survives Modular Avatar copies.

Facts this rests on:

- NDMF virtualizes clips through `VirtualClip.Clone`. A non-marker clip with events is rebuilt as a fresh clip that copies only curves, so its events are dropped (`Packages/nadena.dev.ndmf/Editor/API/AnimatorServices/VirtualObjects/VirtualClip.cs:213-244`). Marker clips keep their original object (`:148-151`) and commit by identity, because `Prepare` returns the kept clip (`:427-429`).
- The commit registers every committed object back to its original: `CommitContext.CommitObject` calls `ObjectRegistry.TryRegisterReplacedObject(objRef, unityObj)` (`Packages/nadena.dev.ndmf/Editor/API/AnimatorServices/ICommitable.cs:72-78`), and the clone context holds `NodeToReference[node] = ObjectRegistry.GetReference(unityObj)` (`Packages/nadena.dev.ndmf/Editor/API/AnimatorServices/CloneContext.cs:143-147`).
- `ObjectRegistry.GetReference` answers the stored reference, or a fresh self reference for an object with none (`Packages/nadena.dev.ndmf/Editor/API/ObjectRegistry.cs:151-155, 158-190`). The reference resolution reaches the root original through any clone depth: `NodeToReference` stores the already-resolved reference of the pre-commit object, and `TryRegisterReplacedObject` stores that reference object for the new clone (`ObjectRegistry.cs:246-263`), so `ObjectReference.Object` (`Packages/nadena.dev.ndmf/Editor/API/ObjectReference.cs:40`) yields the root original in one call. As defense in depth, the implementation walks the chain to a fixpoint with a visited set and a small depth cap, and refuses by name when the cap is hit.
- Modular Avatar runs its clone pass on all platforms, so the committed graph at PlatformFinish consists of clones on every build with Modular Avatar installed (record section 4.3).

The product change: the structural event scan in `CommittedControllerGraph.Enumerate` scans, per enumerated clip, the committed clip and every distinct original that the registry chain resolves to. Either carrying an animation event refuses with the existing `AnimationEventRefusal` (`Editor/Host/CommittedControllerGraph.cs:155-160` holds the single-clip scan as of 2026-10-25).

What the refusal protects when events do not reach the uploaded avatar: NDMF's event drop is version-dependent behavior, not a pinned contract AMUSE may rely on. A marker clip keeps its events and commits by identity. A plugin can clone a clip whose events survive. The provenance read fires the refusal from the surviving authored fact, so the refusal observes the authored event no matter what a current or future NDMF does with it. When events survive to the upload, the refusal is a true positive. As of 2026-10-25 the refusal is a coverage loss on avatars whose events NDMF provably drops. That direction is safe, and the refusal reason stays named.

The same change revises the two production comment sites that state the retired contract: `Editor/Build/AmusePlatformFinishPlugin.cs:199-204` says the stored graph keeps a pre-virtualization view for checks that need real authored clips, and `:267-273` says the structural refusals read real authored clips and that events survive only before virtualization. Both move to the committed-graph-plus-provenance statement and carry the V3 decision ID.

Why not the other data source: walking the descriptor layers and Modular Avatar component fields for authored controllers couples AMUSE to the Modular Avatar serialized schema, which drifts. Pass 1 runs at PlatformFinish, after the Resolving phase, so pre-Resolving scene state is gone. The registry route reads provenance that NDMF itself maintains.

`AGENTS.md` states pass 1 "runs extension-free on real authored clips before animator virtualization". That sentence becomes wrong after this change. Stage 6 revises it (V14). This design lists the change and does not edit `AGENTS.md`.

### V4. The 8 platform tests run in two tiers (owner Q4, tier mechanics are design calls)

Owner decision Q4, 2026-10-25: the product does the read. When a check would block other virtual world platforms outside VRChat, such as ChilloutVR and Basis, that check is optional and informational, never a hard failure.

The defect: `TestVrchatPlatform` returns the qualified name `WellKnownPlatforms.VRChatAvatar30` (`Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:8935-8937`) while being a different object than `VRChatPlatform.Instance`. NDMF selects passes by qualified name (`Packages/nadena.dev.ndmf/Editor/API/Model/SolverPass.cs:101-104`), so vendor VRChat passes run, and they then throw on the identity check (`Packages/nadena.dev.ndmf/Editor/VRChat/VRChatPlatform.cs:137-145`).

The fix and the tiers:

1. No test platform may claim a qualified name owned by another provider object. The NDMF built-in names are `nadena.dev.ndmf.generic` and `nadena.dev.ndmf.vrchat.avatar3` (`Packages/nadena.dev.ndmf/Editor/API/Fluent/WellKnownPlatforms.cs:25-26`). `TestVrchatPlatform` is retired or renamed to a `com.alrauna.amuse.tests.*` name. The audit covers the other test platforms too, including the one that claims the NDMF generic name (`Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:8952-8959`). The audit enumerates every `INDMFPlatformProvider` in both test assemblies. The generic-name claimers include the consent test platform in the same file (`Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:925-932`).
2. Tier B, hard requirement: every AMUSE behavior assertion of the 8 tests runs on the renamed dedicated platform, through the mirrored five-pass topology that `Tests/Editor/Build/AlphaSeparationApplyTests.cs:196-215` already establishes. Tier B compiles and runs without the VRChat SDK. Vendor VRChat passes never select there.
3. Tier A, informational and VRChat-gated: the same fixtures run through the real `VRChatPlatform.Instance`, so the vendor VRChat passes of Modular Avatar execute against them. Tier A gates on package presence only: it skips with a named reason when `com.vrchat.avatars` or `com.vrchat.base` is absent. A version drift of an installed SDK package is a suite-start failure under V1, never a skip.

"Informational" means, concretely:

- The mechanism is `Assert.Ignore` with a reason that names the missing package and its locked version. The Unity Test Framework reports it as skipped with that message.
- `Assert.Inconclusive` is banned in this tier. `Assume.That` is banned for gating (V1).
- The tier carries an NUnit category, for example `[Category("VendorVRChatPlatform")]`, so a filtered run can exclude it and the report can list it.
- No AMUSE behavior assertion may exist only inside tier A. Every tier A assertion has a tier B twin. This is the rule that makes an informational result unable to hide an AMUSE false positive: skipping tier A loses only vendor coexistence evidence, and the report names that loss.

A host platform without the VRChat SDK, such as a ChilloutVR or Basis project, keeps tier B green and skips tier A by name. That is the compatibility the owner asked for.

### V5. Harness shape and the suite duration goal (harness is a design call, duration goal is owner Q5, revised by answer A2 of 2026-10-25)

Harness shape. Stage 5 drives the full production order through the SDK callback dispatcher, as `Tests/Editor/Build/DaoMergedConsumptionTests.cs:181-186` already does for d4rkAvatarOptimizer. One dispatcher call runs the real registered callbacks in production order: NDMF part one at order -11000, VRCFury at -10000, NDMF part two at -1025, d4rkAvatarOptimizer last (record section 4.5). The two NDMF hooks share one ambient build context: the first hook creates the `BuildContext` and stores it on a `ContextHolder` component, and the second hook reuses the stored context (`Packages/nadena.dev.ndmf/Editor/VRChat/BuildFrameworkPreprocessHook.cs:21-23, 46-48, 79`). The two-part manual NDMF harness is rejected: it re-implements dispatcher ordering and context persistence by hand, which is more code at lower fidelity. Rationale: fidelity to the production order of record section 4.5 is the assignment's stated criterion, and the dispatcher is the shipped precedent.

Tier placement. The dispatcher requires the VRChat SDK, so stage 5 production-order fixtures sit in the informational tier A of V4, with an opt-in gate when a run writes project-level state, as `AMUSE_DAO_INTEGRATION=1` does (`Tests/Editor/Build/DaoMergedConsumptionTests.cs:104-112`).

Duration goal. Owner decision Q5, 2026-10-25, revised by owner decision A2 the same date: a suite duration of about 30 minutes is the target, and a longer run is acceptable. The 30-minute figure is a goal and never a gate. A canonical validation run, a full run of both assemblies in the pinned environment, still counts as validation when it takes more than 30 minutes. No stage, no fixture, and no report rule fails or skips on elapsed time. The harness measures the durations the Unity Test Framework reports for the two full runs, sums them, and records them with the date in the run record. Run 2 of 2026-10-24 took about 1650 seconds (record section 3). The environment record of V7 lists, for every run, which opt-in gates were set and which gated fixtures the run excluded. There is no in-suite meta-test for the total, because no single test can observe its whole suite.

### V6. The AAO atlas admission drops the version pin (owner Q8, retained facts are a design call)

Owner decision Q8, 2026-10-25: the atlas admission stops checking the AAO version.

As of 2026-10-25 the shape predicate conjunct 3 requires the installed AAO version to sit in `{"1.9.17"}` (`Editor/Semantics/AaoAtlasTextureAttestation.cs:28-39, 90-97`). The installed 1.9.20 fails it, so no real AAO atlas can enter the admission in this environment (record section 6.3).

The change: delete conjunct 3 from `TryIdentifyAtlasShape`, and delete the AAO version seam with it: the `ProductionAdmittedVersions` table, `AaoAtlasTextureAttestation.Versions`, and the `ClearVersionCacheForSession` line in `AmuseSessionCaches.Clear` (`Editor/Build/AmuseSessionCaches.cs:34`). The seam has a test side: seven test files set or read `AaoAtlasTextureAttestation.Versions`, including tests that pin the conjunct 3 refusals. Those version-seam setups and refusal pins retire with the seam, and the compiler enumerates every site. The corroboration and forge refusal tests stay, because Falsifier 6 depends on them. Clean cutover. The persisted sub-asset branch of `GeneratedTextureAttestation.TryIdentifyProducer` already checks only the name markers and never checked a version (`Editor/Semantics/GeneratedTextureAttestation.cs:54-63`), so the two branches become consistent.

What still fails closed, and why dropping the pin is sound:

1. Conjunct 1 stays: a live `Texture2D` with no asset path (`Editor/Semantics/AaoAtlasTextureAttestation.cs:79-83`).
2. Conjunct 2 stays: the pinned name markers `" (AAO UV Packed)"` and `"AAO Monotone "` (`:85-91`).
3. Conjunct 4 stays: the slot material resolves one hop through the NDMF registry to an asset-backed material (`:112-127`).
4. Downstream gates stay: format allowlist, mip residency, wrap and filter sampling. A future AAO that changes atlas content shape hits those gates and refuses by name.

A version change can then shrink coverage in two safe ways: a renamed atlas fails conjunct 2, and a restructured atlas fails a capture gate. Neither admits an uncharacterized shape. The residual risk shifts in one accepted way: without conjunct 3, a forged marker texture on a registered clone slot no longer requires AAO to be installed at all. The risk stays acceptable because the generated route decodes the real atlas pixels either way, so a forged texture is measured, not trusted, and the owner asked for the removal. The 2026-10-04 residual shape still holds: a forged marker texture must also land on a registered clone material to pass conjunct 4 (spec 2026-10-04 section 9).

The real 1.9.20 atlas becomes testable without a new dated admission. Stage 2 adds the ground check.

### V7. Session-state discipline, side-effect recording, cleanup, and error attribution (stage 1 harness)

1. Plugin-disable keys. Every fixture that sets an NDMF plugin-disable key saves the prior value and restores it in a teardown or `finally` path that also runs on failure. The existing AAO lifecycle shows the save and restore pair as `DisableAaoForFixture` and `RestoreAaoAfterFixture` (`Tests/Editor/Build/TransientUnlockTestLifecycle.cs:99-110`). The window fixtures call this pair from their own setup and teardown methods, and NUnit runs a teardown after a failed test too. New code may use `finally`, but must not rely on the success path. A base-fixture teardown asserts that every key the suite touched is back at its entry value, so the offender test is named immediately.
2. Suite-start check. Before the first test, a suite fixture asserts: no known `nadena.dev.ndmf.plugin-disabled.*` key is set, and every installed vendor version equals its `locked` version from `Packages/vpm-manifest.json`. A mismatch fails with both versions named (V1). An absent optional vendor is recorded, not failed. When `com.vrchat.avatars` is installed, the `AMUSE_VRCSDK3_AVATARS` define must be active and the discovered test set must contain the tier A category, so a broken version define can never turn installed-SDK coverage into silent false skips (`Tests/Editor/Alrauna.Amuse.Tests.Editor.asmdef:25-31` carries the empty version expression). The one exception is a thryeditor upgrade inside the 2.74.x line, an installed patch newer than the locked one. The check records it and never fails or skips on it (V1 obligation 4). A downgrade inside the line and a 2.75.0 or later bump stay named failures.
3. Static test seams. Every static seam (`PresenterForTests`, `TransientUnlockTestKnobs`, `MarkerStubBindings`, `SyntheticPluginScope.IsArmed`) restores its null or default in a `finally` path. The existing dispose patterns stay the model.
4. Side-effect record. The harness digests `ProjectSettings/ProjectSettings.asset` and `ProjectSettings/EditorSettings.asset` before a run and reports changed values after it. It records the scripting defines it can observe. Load hooks act before the first test, so the harness records vendor writes such as the VRCFury settings writes (`Packages/com.vrcfury.vrcfury/Editor-Avatars/Hooks/UnityFixes/SaneUnitySettingsHook.cs:12-65`, record section 7.3) and never prevents them. A fixture must not assert against the committed value of a vendor-written setting. A define write that starts a recompile mid-run kills the run, so the record also states the discovered test count and the completed test count of every run. A run whose completed count is below its discovered count is not green and not validation. Run 1 of 2026-10-24 shows partial runs are real: 3171 of 3268 tests completed (record section 3).
5. NDMF generated output. A teardown removes the `AMUSE *` output containers that ambient builds leave under `Packages/nadena.dev.ndmf/__Generated` (`Packages/nadena.dev.ndmf/Editor/AvatarProcessor.cs:64`, record section 7.3). Deletion stays inside the test-owned set by name prefix. This is one more reason product tests never run in the Census Lab project.
6. Vendor failure attribution. When a build fails inside a vendor pass, the fixture assertion points at the NDMF error report and names the failing plugin and pass. The existing completion assertion already refers to the report (`Tests/Editor/Build/MergedConsumptionFixture.cs:613-618`). The harness extends the message with the failing pass name so a vendor failure is named a vendor failure.
7. Duration record. The harness writes the two run durations and their sum with a date (V5).
8. Environment record. The harness record lists, for every run, the opt-in gates that were set, the gated fixtures the run excluded, and the per-test skip reasons with the named absent package and its locked version (V1 obligation 3, V5).

### V8. Falsifier survival audit (stage 1b)

About 150 dedicated-platform tests run with the all-platform Modular Avatar passes active, and with AAO unless a suite disables it (record section 8, stage 1b). For each numbered falsifier in those suites, the audit lands exactly one of two verdicts:

1. Survival assertion: the fixture asserts that the planted state reaches the targeted pass, for example that the captured evidence contains the planted binding. This is a new assertion that runs green after the fix and is RED against a fixture whose planted state a vendor pass destroyed.
2. Scoping: the suite sets the NDMF plugin-disable key for the vendor whose pass destroys the planted state by design, with the V7 restore discipline. The AAO window fixtures are the precedent (`Tests/Editor/Build/TransientUnlockTestLifecycle.cs:88-110`).

A catch-all around vendor failures is not a verdict. It hides the interaction the audit exists to expose, and it violates the no-blanket-catch rule. The audit output is a per-falsifier verdict table in the plan document, not in this spec.

### V9. The three unattributed questions become first diagnosis tasks

1. Q6, the writer of the swap-in `MaterialPropertyBlock`. First task of stage 1, because the swap-in fixture of record section 4.3 cannot be rebuilt without it. Procedure: build a minimal reproduction with one renderer and one float material clip. Run it through a build on the real VRChat platform. Probe `Renderer.HasPropertyBlock()` after each vendor pass and after each NDMF scope boundary. Check the animator rebind first, because `Editor/Build/AmusePlatformFinishPlugin.cs:208-216` documents that the phase-end animator rebind re-applies stale curve values over the live renderer state. Invariant, load-bearing: any outcome that touches the check order in `Editor/Build/LockedMaterialAdmission.cs:44-47` must keep refusing a locked slot that carries a live property block, unless the diagnosis first proves the diagnosed writer cannot carry an alpha-relevant value on a proven slot. The check exists because a block would give the locked original and the unlocked copy different values whatever the animations carry. A reorder that only reports the more specific cause first is safe. A reorder that lets a blocked locked slot swap in is the declared false positive.
2. Q7, the pass that removes the marker-clip observation. First task of stage 1b, which exists to attribute exactly this class. Procedure: run the marker fixture once per vendor with that vendor's plugin-disable key set, one vendor at a time, and record which disabling restores the marker refusal count. Start with Modular Avatar and AAO, the two plugins that rewrite committed controllers (record section 4.3, third row).
3. Q9, the 2 inconclusive tests of run 2. First task of stage 1. `[INFERENCE]` The two are the gated d4rk tests `DaoMergeRunsInTheSdkSequenceAndPreservesSoundness` and `AaoAndDaoCombinedKeepsTheMergedSoundness`: exactly two tests are gated by `Assume.That` on `AMUSE_DAO_INTEGRATION` (`Tests/Editor/Build/DaoMergedConsumptionTests.cs:104-112`), and the record states they did not run on 2026-10-24 (section 4.4), while run 2 reported exactly 2 inconclusive and no skips (section 3). Procedure to confirm, not guess: read the two inconclusive result names from the run 2 result, then read the test bodies. The fix follows V1: environment gating moves to `Assert.Ignore` with a named reason, and the report shows a skip. The sweep is suite-wide, not d4rk-only: five `Assume.That` environment gates exist as of 2026-10-25, in `Tests/Editor/Build/DaoMergedConsumptionTests.cs:106-112` and `:116-118` (gate variable and d4rk absence), `Tests/Editor/Build/AaoMergedConsumptionTests.cs:102` and `:155` (lilToon absence), and `Tests/Editor/Build/Semantics/LilToon/LilToonOpaqueTargetTests.cs:1182-1185` (lilToon absence). Stage 1 converts all five to `Assert.Ignore` with the missing package and pinned version named.

### V10. Ground checks and the tripwire replacement (stage 2)

1. For each attested family, one test loads the installed shader by pinned GUID and runs the production attestation end to end. Loading by GUID is mandatory: a 10.x shader carries the 9.3 release name, so `Shader.Find` returns the wrong shader (record section 6.2). The lilToon digest-equality question at `Editor/Semantics/LilToon/LilToonSourceAttestation.cs:419-424` settles in this stage.
2. One test loads a real Poiyomi 10.x material and asserts the production answer is unattested. This is the strongest available drift falsifier (record section 6.2).
3. The lock-tool tripwire changes form. The test-local `Thry.ThryEditor.ShaderOptimizer` (`Tests/Editor/Build/TransientUnlockVendorStandIn.cs:8-41`) shares the full name of the installed type, so inside the test assembly a name binds to the stand-in and the counters can no longer prove anything about production calls (record section 5.2). Replacement that proves the same rule, namely production never calls the vendor lock tool: a source-scan test over `Editor/` that refuses references to the vendor lock tool type, in the style of `ResearchSourceApiBanTests`. The scan matches the vendor qualified type name, not a name substring, because AMUSE property names and vendor operands contain the same word (`Editor/Semantics/Poiyomi/PoiyomiProperties.cs:10`, `Editor/Semantics/Poiyomi/PoiyomiLockCondition.cs:53`). The stand-in type and its counters are deleted in the stage 5 cutover, after the real-lock characterization of V13 exists.
4. One real-AAO atlas ground check: run a real `optimizeTexture` atlas of the installed AAO 1.9.20 through `TryIdentifyAtlas` and the generated capture route. RED before the V6 change in this environment, because conjunct 3 refuses 1.9.20.

### V11. Real lilToon replaces stand-in happy paths (stage 3)

The pinned name and GUID pairs of lilToon 2.4.1 all match the installed package (record section 6.1). Stage 3 replaces the stand-in happy paths and the lilToon verified seams with real materials through real attestation. The rule that governs every retirement: each retired stand-in test names its replacement test, or stays synthetic with a stated reason from record section 5.3, such as exact keyword control (`Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs:138-302`) or a refusal shape no vendor ships (`Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs:715-841`). The minted stand-ins that only imitate an admitted happy path retire with their replacements in place.

### V12. Real Poiyomi bundled 9.3 replaces its stand-ins (stage 4)

The two bundled 9.3 rows match the installed 10.0.27 package by name and GUID, and the pinned digests record byte equality across 10.0.20 to 10.0.27 (record section 6.2). Stage 4 is the same cutover as V11 for the bundled 9.3 shaders, loaded by pinned GUID. Stage 4 measures the import and compile cost of the Poiyomi shader and records it (V5). The measurement is recorded data and never a gate. Owner decision A3b, 2026-10-25: release package testing is dropped. The bundled 9.3 shader in Poiyomi 10 is the support path. Design call: the bundled 9.3 shader in Poiyomi 10 is the primary support path. No stage tests the 9.3.64 release package, which is not installed. The two 9.3.64 release rows stay in the product data (`Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:110-121`). Locked reconstruction tests stay synthetic in this environment. As of 2026-10-25 the bundled rows carry `LockedOriginalAdmitted = false`, so no installed shader satisfies an admitted locked original (record section 6.2). The sibling admission design flips that flag on its own branch, and that flip is the event that reopens this choice. A real Thry lock can feed the aligner characterization of V13, never an admitted happy path.

### V13. Production-order fixtures and the real Thry lock (stage 5)

The owner approved the sibling readiness record question 4, "May a test run a real vendor lock in the dev project" (owner decision Q4, 2026-10-25, readiness record section 9, question 4).

1. Production-order fixtures: Modular Avatar merge-animator and VRCFury toggle fixtures run through the SDK dispatcher in the production order of record section 4.5, with the existing triangle soundness oracle. They sit in tier A of V4 and behind an opt-in gate when they write project-level state. VRCFury runs only through SDK callbacks, never inside `AvatarProcessor.ProcessAvatar` (record section 4.4). Every tier A assertion keeps its tier B twin (V4). The VRCFury twin injects an equivalent animation input by hand, because VRCFury runs only through SDK callbacks.
2. One real Thry lock characterization: the real tool locks a synthetic stand-in material, so no generated asset lands in the vendor tree, and the result is compared with `LockedSourceFixture` output. Cleanup order, load-bearing: delete the test's own locked materials first, then delete the test's own entries under `Assets/_LockedShaderCache/`, because the vendor documents that deleting a cache entry other locked materials hold breaks those materials (readiness record section 4.6, citing `<thryeditor>/Editor/LockedShaderCache.cs:24-38`). The teardown asserts the cache folder holds no test residue.
3. Network disclosure: the lock calls the vendor usage counter (`<thryeditor>/Editor/ShaderOptimizer.cs:910`, readiness record section 4.6). The gate message and `docs/development-setup.md` disclose this call, so a runner opts in knowingly.
4. License note: VRCFury's commercial grant forbids redistribution and modification. Tests only reference installed packages. The setup document tells commercial users to obtain VRCFury from its official channel (record section 7.1, staged in V14).

### V14. Documentation updates (stage 6)

1. `AGENTS.md`: revise the pass 1 sentence in the Architecture section after V3 lands, to state the committed-graph-plus-provenance read. Listed here, not edited by this document. The two production comment sites of V3 land with the V3 code change itself, not in stage 6.
2. `README.md`: update vendor statements, including the sentence that d4rkAvatarOptimizer is not validated in 0.1.0 (`README.md:36`), which the d4rk tests must not contradict.
3. `docs/architecture/shader-frontend-comparison.md:74, 196-197, 243-247`: align with the vendor-integrated reality.
4. `docs/development-setup.md`: name the extra VPM repositories a fresh clone needs, the vendor global side effects, the VRCFury license note for commercial users, the opt-in gates, and the duration goal.
5. Root `.gitignore` and `Packages/.gitignore`: the vendor comments describe the packages as local-test-only. The root file says "Vendor packages installed for local testing only", and `Packages/.gitignore:12-13` says "Vendor packages for local testing". They describe the permanent dev environment after this rework and get current wording.
6. Dated records under `docs/superpowers/` keep their text.

### V15. The Census Lab rule and the opt-in gates (design call)

Product tests create and delete folders under `Assets/`. Stage 5 adds write surfaces. The existing rule stands: product tests never run in the private Census Lab project (record section 8, last paragraph). Every new project-level write sits behind a named opt-in environment variable with a message that names the disposable-project requirement, following `Tests/Editor/Build/DaoMergedConsumptionTests.cs:104-112`. Design call: the canonical validation run does not set the real-lock gate by default, because the gate writes project-level state and makes a vendor network call. The validation of a drifted thryeditor patch is a separate dated gated run (V1 obligation 4).

## Test strategy

RED/GREEN per AGENTS.md: a behavior change needs a failing test observed first against a named plausible wrong implementation, then a minimal fix. A first-run pass is recorded as characterization. Every falsifier carries a no-op guard.

Named plausible wrong implementations:

- W1 (V3): reads only the committed clone's events. Misses every authored event that Modular Avatar's clone dropped. F1 fails it.
- W2 (V3): resolves provenance through any source other than the NDMF registry, such as a walk over descriptor layers and Modular Avatar component fields. It couples the product to the Modular Avatar serialized schema, and for a marker clip committed by identity it observes no distinct original, so it misses the event. F2 fails it.
- W3 (V4): keeps a test platform that claims the VRChat name. Vendor VRChat passes select by name and throw on the identity check. Observed 2026-10-24. F3 fails it.
- W4 (V4): puts an AMUSE behavior assertion only inside the VRChat-gated tier, or gates any environment check with `Assert.Inconclusive` or `Assume.That`. F4 and F13 fail it.
- W5 (V6): admits any path-less texture that sits in a container, or any path-less marker-named texture without the registry corroboration. F5 and F6 fail it.
- W6 (V1): treats a version mismatch against the locked block as a skip, or fails a recorded thryeditor upgrade inside the 2.74.x line. The first hides drift. The second breaks the A3c exception. F7 and F14 fail it.
- W7 (V13): deletes the shared lock cache before the test's own materials, or leaves residue under `Assets/_LockedShaderCache/`. F8 fails it.
- W8 (V7): restores a plugin-disable key only on the success path. A failing fixture silences a vendor for the rest of the session. F9 fails it.
- W9 (V10): loads an attested shader by name instead of pinned GUID and picks the 10.x shader. F10 fails it.
- W10 (V8): wraps vendor failures in a catch and calls the falsifier survived. F11 fails it.

Numbered falsifiers:

- `--- Falsifier 1: an event scan that reads only the committed clip misses an authored event on a clip that Modular Avatar virtualized, and reports no refusal where the surviving provenance carries one. The Modular Avatar ambient fixture of V3 must fail it. ---`
- `--- Falsifier 2: a provenance read that resolves the committed clip through any source other than the NDMF registry chain, or that reads only one registry hop where two plugins cloned in sequence, misses the authored event. The chain fixture registers clone to clone to authored by hand, as the 2026-10-04 registry fixtures do, and the marker fixture commits by identity. Both must fail it. ---`
- `--- Falsifier 3: a test platform whose QualifiedName equals a name owned by another provider object selects that provider's passes and then fails their identity checks. The platform identity audit must fail it. ---`
- `--- Falsifier 4: a tier A test whose AMUSE behavior assertion has no tier B twin turns a product regression into a named skip on an SDK-less environment. The tier audit must fail it. ---`
- `--- Falsifier 5: an admission that accepts any path-less texture inside a container admits a forged foreign texture with an arbitrary name. The persisted-branch forge fixture must fail it. ---`
- `--- Falsifier 6: an admission that drops the registry corroboration with the version pin admits a marker-named texture on an unregistered slot material. The existing corroboration refusal test must stay green and keep failing it. ---`
- `--- Falsifier 7: a suite-start check that skips on a version mismatch reports green on an environment that is not the locked one. The mismatch fixture uses a vendor without an owner assumption, such as lilToon, or a Thry editor bump to 2.75.0. It must fail it. ---`
- `--- Falsifier 8: a lock characterization whose teardown deletes a cache entry that a still-referencing material holds breaks that material. The residue assertion must fail it. ---`
- `--- Falsifier 9: a fixture that restores its plugin-disable key after the assertions leaves the key set when an assertion throws. The base teardown must fail it. ---`
- `--- Falsifier 10: a ground check that resolves the shader by release name loads the 10.x shader on a 10.x install and attests the wrong bytes. The GUID-load assertion must fail it. ---`
- `--- Falsifier 11: an audit that catches vendor exceptions around a falsifier reports survival without proving the planted state reached the pass. The survival-assertion review must fail it. ---`
- `--- Falsifier 12: a reorder of the locked-slot checks admits a locked slot that carries a live property block and no binding mismatch, so the block's runtime values never enter the proof. The blocked-slot fixture with a clean binding set must fail it. ---`
- `--- Falsifier 13: an environment gate that still uses Assume.That reports inconclusive instead of a named skip in a legal SDK-less or vendor-less environment. The stage 1 gate audit over both test assemblies must fail it. ---`
- `--- Falsifier 14: a suite-start check that fails or skips a thryeditor upgrade inside the 2.74.x line, or that records a drifted patch without the not-yet-validated note, misstates the A3c state. The fixture with an installed 2.74.4 against a locked 2.74.3 and the fixture with 2.75.0 must fail it. ---`

Replacement rule check: the retired counter tripwire maps to the source scan (V10.3), the stand-in happy paths map to real-material twins (V11, V12) or to named synthetic retentions, and the `TestVrchatPlatform` coverage maps to the two tiers (V4). No falsifier retires without a named successor.

## Stage order

1. **Stage 1, harness, isolation, setup, and the three fixture fixes.** V7 items 1 to 7, V4, V3, V9 items 1 and 3, and the VRChat-shaped descriptor fix: every fixture that builds through the VRChat platform carries all base layers including FX, because the single-Base fixture made Modular Avatar throw (record section 4.2, `Tests/Editor/Build/MergedConsumptionFixture.cs:270-291`). Also refresh the stale "no VRChat SDK" fixture comment (`Tests/Editor/Build/AlphaSeparationApplyTests.cs:332-335`). Exit: a green full run in the pinned environment, or a named skip list plus a green remainder.
2. **Stage 1b, falsifier survival audit.** V8 and V9 item 2. Runs parallel to stage 2.
3. **Stage 2, ground checks.** V10. Exit: one admitted ground check per attested family, one real unattested 10.x answer, the source-scan tripwire green, and the real AAO 1.9.20 atlas admitted through the V6 change.
4. **Stage 3, real lilToon.** V11.
5. **Stage 4, real Poiyomi bundled 9.3.** V12. The stage measures and records the import and compile cost of the Poiyomi shader (V5). The measurement never gates the stage.
6. **Stage 5, production-order fixtures and the real Thry lock.** V13.
7. **Stage 6, remaining documentation.** V14.

Stage 1 must land before the others, because every later stage reads the harness. Stage 1 also converts every `Assume.That` environment gate in either test assembly to a named `Assert.Ignore` (V9.3). Stages 3 and 4 depend on stage 2. Stage 5 depends on stage 1b and on stage 2, because its soundness oracle reads ground-checked real sources. Stage 6 is last.

## Docs to update

Listed in V14. No other document changes. Dated records keep their text.

## Open items for the owner

1. O1, the VRChat SDK dependency gap (closed 2026-10-25, owner decision). The gap: the product lists `com.vrchat.avatars` in `vpmDependencies` (`Packages/com.alrauna.amuse/package.json:16-19`), and the plugin runs only on the VRChat platform name (`Editor/Build/AmusePlatformFinishPlugin.cs:250`). Owner decision A1, 2026-10-25: the SDK dependency stays unchanged. Design call: the owner Q1 optional-dependency principle covers the vendors other than the SDK. The owner chose that scope in answer A1. The suite itself still runs without the SDK with named skips (V4).
2. O2, the suite duration response (closed 2026-10-25, owner decision). Owner decision A2, 2026-10-25: a suite run longer than 30 minutes is acceptable. The 30-minute figure is a goal and never a gate (V5). Durations stay measured and recorded.
3. O3, the bundled locked original track. The readiness record's open questions received owner answers on 2026-10-25, and the design exists: `docs/superpowers/specs/2026-10-25-bundled-locked-original-admission-design.md`. The sibling design is revised on 2026-10-25 for the owner answers of the second round. Answer A3b removes release-package testing and names the bundled 9.3 shader in Poiyomi 10 as the support path. That answer changes the sibling track only. This spec keeps that track out of scope (V12 boundary) and treats its decisions as the sibling design's property.
