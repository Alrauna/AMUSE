# Ponytail Maintainability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task by task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correct the audited reporting defects and reduce repeated rule ownership without changing the transformation contract.

**Architecture:** Keep the existing modules and assemblies. Give each fixed rule one owner. Join authored identity to Play results through one transient native root snapshot owned by the existing store. Keep identity, policy validity, and refusal display separate from proof and mutation. Use small complete cutovers with no compatibility shims.

**Tech Stack:** Unity 2022.3.22f1, C#, NUnit EditMode tests, NDMF, and the existing Fluent 2.6.1 website runtime.

**Spec:** `docs/superpowers/specs/2026-10-21-ponytail-maintainability-design.md`, PTM1 revision finalized at `fc19e86`.

Privacy note: This plan uses public source and synthetic cases. It contains no private observations or machine identifiers.

Status: 2026-10-21, PTM1 plan revision finalized after five parallel adversarial lenses and scoped confirmation. No blocking finding or owner choice remains in this plan. Task 1 is complete in `c0509cb`. Task 2 resumes at the candidate host and lifecycle gate before product edits. The owner selected subagent-driven execution and authorized commits between finalized changes.

## Global Constraints

- Unity 2022.3.22f1 and the existing C# language and assembly configuration stay unchanged.
- Use existing libraries only. No new dependency is required.
- Analysis remains pure. Host owns live Unity evidence. Build owns lifecycle and mutation. Preview remains read-only.
- Unproven triangles stay unproven. Unknown facts invalidate only dependent conclusions.
- Keep each named refusal, its scope, and its precedence unless PTM3 corrects the display of that refusal.
- Programming and invariant failures throw. Never turn them into unsupported results.
- Keep serialized row-kind numeric values 0 through 4 unchanged.
- New Unity files and their `.meta` files form one change. Existing metadata identifiers stay unchanged.
- Before any Unity write, broad operation, or reported validation, verify the exact intended `Application.dataPath` and pin the dev editor instance.
- Use public synthetic fixtures. Never record machine paths, private identifiers, endpoint identifiers, or raw private observations.
- Human-facing English uses short active sentences. No contractions or semicolons in prose.
- Commits include explicit task paths only. Never push, publish, merge, or change repository settings.
- Keep the first-blocker resolver. Do not discover later hidden blockers or add deferral evidence carriers.
- Keep shader support ranges, pins, consent policy, proof mathematics, the five passes, live revalidation, and unlock-close ordering unchanged.
- Preserve all captured user-owned configuration, settings, scene, and metadata changes. Never stage them. Capture their bytes before validation and restore only changes caused by this run.
- The research package must not enter the product archive or listing. Never run tests in the private Lab project.
- Model routing is session-only. Do not save the temporary review-role overrides.

## Review Focus

1. A saved prefab changes its global identity during Play. Its own report must still load after exit in every domain and scene reload combination. A delayed idle cleanup must not destroy a snapshot captured by immediate re-entry. Task 2 owns these cases.
2. A policy can change during background work and then return to its original value. No stale rows or cache writes may return. Task 3 owns the sticky rejection test.
3. A permanent swap refusal can follow a streaming current material. The recorded permanent cause must win without deeper state resolution. Task 4 owns this fixture.
4. Old numeric row payloads can outlive a domain reload in the same session. They must draw the same row shape after the typed cutover. Task 7 owns numeric JSON cases.
5. A user can switch rows while the menu is open and then press Enter. One action must use the second URL. Task 13 owns this browser case.

---

## Execution and verification protocol

The controller owns integration, Unity and browser operations, reviews, and all git writes. Workers edit only their task files. They never run tests, builds, lint, or formatters while working. They never dispatch children or commit.

For a bug task, a worker first writes its behavioral regression and returns for the RED gate. The controller runs it in the pinned dev editor. The worker receives the observed failure before it writes the production fix. Existing failing baseline checks and the fresh-process Poiyomi failure are already observed. Do not rerun them just to confirm that evidence. Pure moves use existing behavioral coverage rather than artificial failing wiring tests.

Use the installed editor bridge. Start a focused run with `run_tests` and the JSON fields `mode: "EditMode"`, `groupNames`, and `assemblyNames`. Read its terminal result through `get_test_job`. These are tool operations, not new product dependencies. Record observed counts and failures. A zero-test filter fails validation. A failed job without an aggregate result is not a passing suite.

For each task, the controller runs the focused check and a changed-path smoke where behavior changes. It then requests a read-only task review for spec compliance and quality. Fix important findings before finalization. Stage only that task's explicit changed files after review and validation. Use the commit message named below. Include a relevant documentation or changelog update after proof. Do not bump the package version.

Use one dated `Unreleased` section in `Packages/com.alrauna.amuse/CHANGELOG.md`. Do not edit released entries. A comment-only task already updates its owning documentation and needs no duplicate release bullet. Research test documentation belongs in `docs/development-setup.md`, not in product release claims.

Keep the task ledger in the plan-specific Superpowers workspace. It holds task status, sanitized evidence, review disposition, and commit references. Stop for a new owner choice. Do not silently narrow acceptance or add a fallback to avoid a failed check.

## File responsibilities

| Files | Responsibility |
| --- | --- |
| `Editor/Build/PlayModeBuildStore.cs` in the product package | Play ownership, policy key, and stored row contract |
| `Editor/Build/PlayModeRootIdentitySnapshot.cs` and its `.meta` in the product package | One native Play-lifetime holder for root references and authored keys |
| `Editor/AmuseAvatarOptimizerEditor.cs` and `Editor/DryRun/AlphaSeparationDryRun.cs` in the product package | Inspector display and run validity |
| `Editor/DryRun/DryRunRowBuilder.cs` in the product package | Refusal and deferred row construction |
| `Editor/Analysis/RuntimeStateResolution.cs` in the product package | Pair captured slots with their resolution results |
| Poiyomi interpreter, facade, and source attestation files | Own the schema without a static initialization cycle |
| Existing semantic and research test files | Protect behavior and the no-write tripwire |
| `Tests/Editor/Shared/TestTransientScope.cs` in the product package | Own saved synthetic scenes for identity-sensitive fixtures |
| `Tests/Editor/ReferenceFixtures/EmptyIdentityScene.unity` and its `.meta` | Minimal public seed copied for saved-scene fixtures beside the runner's untitled scene |
| One new Build cache helper with its `.meta` file | Own the fixed reset list only |
| `Website/app.js` | Own the one menu action and selected URL |

All complete paths appear in their owning task below. Tests use existing synthetic helpers and stand-in shaders. Do not add a fixture framework.

### Task 1: Correct the two portable capture-test contracts

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs`
- Modify: `docs/development-setup.md`
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md`

**Interfaces:** Consumes the unchanged `UnityAlphaFieldEvidence.TryCapture` contract. Produces portable tests only. No production interface changes.

Execution status on 2026-10-21: complete in `c0509cb`. Do not dispatch Task 1 again. Its final evidence appears in the execution record.

- [ ] **Step 1: Preserve the observed RED evidence.** The baseline recorded two failures: `AnRgbOnlyFormatSamplesAlphaExactlyOneAtEveryLevel` and `ReadableArgb32_IsAdmittedAndSeparatesMaximumFromSubmaximum`. The capability probe found RGB24 Sample support true and ARGB32 Sample support false on the dev backend. Do not rerun the old assertions to confirm them.
- [ ] **Step 2: Correct the two tests.** Delete only the RGB24 host-capability assertion. Keep every full-chain alpha check. In the ARGB32 test, inspect exact Sample support for the fixture's actual graphics format. On the unsupported branch call the real capture entry and assert false, `TextureCaptureRefusalReason.UnavailableCapture`, and null evidence. On the supported branch keep the existing exact 255, 128, and 254 distinction checks. Update both XML comments to state what each branch proves. Do not skip a test or change production gates.
- [ ] **Step 3: Run the focused fixture.** Run `Alrauna.Amuse.Tests.Editor.Host.UnityAlphaFieldEvidenceTests`. Expect a non-zero count and no failure. Record the correction as characterization, not a production RED/GREEN fix.
- [ ] **Step 4: Run both full AMUSE assemblies.** Expect terminal aggregate results with no failure before the product changes begin. If another baseline failure appears, diagnose it and stop before any out-of-scope correction.
- [ ] **Step 5: Update the test documentation.** Explain backend capability-dependent capture tests in development setup. Add one dated Unreleased test-maintenance bullet.
- [ ] **Step 6: Obtain the task review.** Require spec compliance and quality approval for the observed code and evidence.
- [ ] **Step 7: Commit.** Commit only the listed changed files as `test(host): respect exact texture sampling capability`.

### Task 2: Give Play records proved root ownership

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Build/PlayModeRootIdentitySnapshot.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Build/PlayModeRootIdentitySnapshot.cs.meta`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/ReferenceFixtures/EmptyIdentityScene.unity`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/ReferenceFixtures/EmptyIdentityScene.unity.meta`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Shared/TestTransientScope.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/PlayModeBuildStoreTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`
- Modify: `README.md`
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md`

**Interfaces:**
- Keep `ComputeAvatarKey(Transform) -> string`, `Save`, `TryLoad`, `Clear`, and `SaveOutcome` signatures. An empty key authorizes no save, load, or clear. Use `Amuse_PlayMode_v2_` as the record prefix.
- Add `internal sealed class PlayModeRootIdentitySnapshot : ScriptableObject` with `internal void Initialize(GameObject[] roots, string[] keys)` and `internal string KeyFor(GameObject root)`. `Initialize` throws when either array is null or the lengths differ. Retain the constructed arrays without copying them. Serialize paired native references and key strings. Match two existing GameObjects with Unity native `==`, never CLR identity or an integer join.
- Extend the existing `TestTransientScope` with `internal GameObject CreateSavedSceneRoot(string name)` and `internal void SaveOwnedScene()`. Copy the public empty scene fixture into the scope folder with `AssetDatabase.CopyAsset`, then open the copy with `EditorSceneManager.OpenScene(..., OpenSceneMode.Additive)`. Own one copied scene per scope. Create the root there, save only that scene, and leave it active for more fixture objects. `SaveOwnedScene` saves only the owned copy after additions. Do not use additive `NewScene` beside the runner's untitled scene.

- Pin scope teardown order: if the owned copy is active, activate the still-loaded prior scene or another loaded scene first. Close only the owned copy without saving, destroy remaining tracked objects, then delete the scope folder and metadata. Scene close destroys its GameObjects. Clear scene ownership fields even after failure. Never save or close the runner's untitled scene or the user's scene. No fixture hierarchy.
- The snapshot belongs only to store lifecycle. Never register it with the owned-clone sweep or the shared cache-reset helper from Task 10.

Let the dev editor generate the new product metadata once. Do not replace generated metadata.

- [ ] **Step 1: Carry forward the observed evidence.** The direct full-key candidate failed for saved prefabs. CLR `ReferenceEquals` failed legitimate owners. Do not rerun those wrong candidates to confirm them. Native matching passed all four reload combinations and an observed mid-Play reload. Read-only IL inspection of the installed SDK dispatcher showed unchanged argument-zero forwarding to every callback, no root reassignment, and no root clone. NDMF forwards that same root into `BuildContext`. This supports installed-host identity, not callback success or all-host behavior. No SDK dispatcher was executed.
- [ ] **Step 2: Prepare the candidate gate before product edits.** Use a throwaway native snapshot and a probe-only NDMF pass under `Assets/AmuseTests_PlayIdentityProbe`. Persist cleanup state across reload. Use saved loose and prefab roots with optimizer components, including a nested root. Keep fixtures inactive from before Play entry through cleanup. Refuse if any unowned active-in-hierarchy avatar could auto-process. Use the public `RuntimeUtil.FindAvatarRoots(root: null, includeInactive: false)` scan across loaded scenes. It covers registered root types, including SDK descriptors and `NDMFAvatarRoot`. Do not name internal root-type fields.
- [ ] **Step 3: Observe the in-place host join.** In Play, replay the temporary rename and fake sibling. Check every owned fixture hierarchy for connected prefab instances. If one remains, unpack only that Play copy completely with automated interaction, then prove its captured references still native-match. Never unpack, apply, or save a source prefab or scene. Pin `nadena.dev.ndmf.platform.AmbientPlatform.DefaultPlatform` and require `QualifiedName == WellKnownPlatforms.VRChatAvatar30`. Invoke `AvatarProcessor.ProcessAvatar(root, platform)` under `OverrideTemporaryDirectoryScope(null)`. Do not name internal `VRChatPlatform` or use a manual-bake clone.

The probe pass observes the real `BuildContext.AvatarRootObject` at PlatformFinish and resolves its authored key from the throwaway snapshot. Its distinct row payloads use probe-owned session records, not inaccessible product internals. `NullAssetSaver` has no container, so AMUSE's `MissingBuildContextServices` refusal is expected and does not fail this identity-mechanism gate. Record the executed public plugin qualified names. Source plus IL supports the installed root flow without executing the SDK dispatcher. Stop for an unapproved environment or mechanism.
- [ ] **Step 4: Complete the gate matrix.** Cover all four reload combinations, a Play script reload, canceled entry plus idle reload, second entry, and idle cleanup racing immediate entry. Process each fixture once per Play session. After reload, inspect recovery and records without reprocessing. Before second entry, remove a surviving fixture-only tag through `GetComponents<Component>()` and the exact full name `nadena.dev.ndmf.runtime.AlreadyProcessedTag`. Do not name its internal type in code or clear a foreign tag. Do not save that removal. Observe reloads. Capture and restore both option values and file bytes per leg. Remove all owned assets on every exit. No identifier output.
- [ ] **Step 5: Implement the declared fixture extension, then write ownership regressions.** Generate the empty public scene fixture and its metadata once in the pinned dev editor before the runner starts. It contains no authored root, vendor asset, or private data. Do not handwrite scene YAML. Copy and open it per scope rather than save a source scene. Write `TwoSameNamedSavedRootsKeepTheirOwnRows` and `UnsavedRootsNeverBorrowARecord`. Add cases for clear isolation, rename, reorder, replacement, same-prefab twins, duplicate scene names, nested avatars, and unproved roots. Save only the owned copy after adding roots. Recompute from a retained destroyed Transform and prove no survivor is loaded or cleared. Do not require opaque-key eviction. Keep the assertions below.

```csharp
Assert.That(loadedFirst.avatarRows[0].sentence, Is.EqualTo("First result."));
Assert.That(loadedSecond.avatarRows[0].sentence, Is.EqualTo("Second result."));
Assert.That(loadedSecondAfterFirstClear, Is.Not.Null);
```

- [ ] **Step 6: Observe RED before production edits.** Run the two new ownership methods through the focused EditMode filter. Expect non-zero counts and a failure in wrong-root row attribution or unproved-root admission on the old name key. A compile error or missing runner is not RED.
- [ ] **Step 7: Implement the native holder and one key rule.** Add the internal snapshot type and let the editor generate its metadata. Keep it non-persistent and `HideAndDontSave`. In Edit mode, return only the valid authored scene identity. In Play, use only a captured live native reference. Enumerate optimizer components in normal loaded saved scenes, including inactive and nested objects. Reject persistent assets, preview scenes, and invalid identities. The context-root activation gate stays unchanged. Do not add a scene-root parent filter, integer join, marker, alias, or generic registry. No holder is needed for an empty candidate set.
- [ ] **Step 8: Wire the store-owned lifecycle.** Put `[InitializeOnLoad]` on the store. Subscribe independently of inspector instances. Recover any owned native snapshot before capture or teardown. Capture at `ExitingEditMode`. Use one private `SessionState` pending Boolean, re-arm the temporary update guard on load while entry is pending, and remove it when Play starts. On `EnteredEditMode`, recover and destroy only the owned snapshot and clear pending state. A Play-side reload retains the snapshot for lazy recovery.
- [ ] **Step 9: Close idle and canceled-entry cleanup.** Use `EditorApplication.delayCall` for the idle-load check after native restoration. Recheck both Play flags before destruction so immediate entry retains its new snapshot. The pending guard uses the same idle predicate, clears its flag, and unsubscribes. Recover via `Resources.FindObjectsOfTypeAll<PlayModeRootIdentitySnapshot>()`, then filter exact type, non-persistence, and owned flags. Do not use a finder that hides `HideAndDontSave` objects. Multiple owned snapshots throw. Do not turn invariant failures into missing identity.
- [ ] **Step 10: Migrate the old fixtures in the same cutover.** Delete clone-name normalization and the two obsolete suffix tests. Update the corrupt-payload prefix helper. Move the inspector Play-record fixture and `CaptureAndSavePlayModeBuildRecordAssignsUntouchableSentenceToUnappliedCandidate` to saved roots through the scope extension. Keep row, moved-triangle, and display assertions. Keep a valid-key precondition so a no-op save cannot replace the path. Add no helper-call or object-count tests.
- [ ] **Step 11: Run focused checks.** Run `Alrauna.Amuse.Tests.Editor.Build.PlayModeBuildStoreTests`, `Alrauna.Amuse.Tests.Editor.AmuseAvatarOptimizerEditorTests`, and the named apply-record method. Require terminal non-zero results with no failure. Do not rerun unrelated apply cases for a fixture-only migration.
- [ ] **Step 12: Smoke product code with admitted services.** Use the safe direct NDMF entry with a non-null owned output folder, `Assets/AmuseTests_PlayIdentitySmokeOutput`. Require it absent before creation and restore the output override before leaving Play. Do not reuse the null-saver mode for this positive smoke. Repeat the gate matrix on product code, store inside the rename, load each root's rows after exit, and verify fake-sibling clear isolation. Observe the actual inspector. Include reload recovery, cancellation, idle cleanup, immediate re-entry, and repeat Play. Prove reset and sweep leave the snapshot alive. Remove only owned output and preserve captured source bytes.
- [ ] **Step 13: Update the documentation and obtain task review.** Explain authored-key and native-reference ownership, empty-key no-record cases, and Refresh isolation in README. Add one Unreleased reporting bullet after proof. Require spec compliance and quality approval, including the script and metadata unit.
- [ ] **Step 14: Commit.** Commit only the listed task files as `fix(preview): isolate Play records by proved root identity`.

### Task 3: Share the policy key and reject invalidated runs

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/DryRunCacheTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/PlayModeBuildStoreTests.cs`
- Modify: `README.md`
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md`

**Interfaces:** Add `internal bool ValidatePolicy(string livePolicyFingerprint)` and `internal bool PolicyInvalidated { get; }` on `AlphaSeparationDryRun`. The caller computes the live key through `PlayModeBuildStore.ComputePolicyFingerprint`. The inspector reuses the live key it already computed for Play lookup. The driver computes it once per step. `ValidatePolicy` compares the supplied key with the captured key, latches the first mismatch, cancels pending work, and returns false after invalidation. A new run starts with a clear latch. Keep the formatter unchanged.

- [ ] **Step 1: Write validity regressions and observe RED.** Use the existing fixture's known computed row and counts as a no-op guard before changing policy. Complete a run, change one policy value, and reject old rows. Change policy during background work, observe invalidation, restore the old value, and keep rejection. Change `IgnoreOutOfRangeMaterialSlots` between renderer steps and reject mixed-policy output. Cover target changes and every early banner or structural-refusal return. Keep the existing unchanged-policy cache-hit case. Observe rows and available actions through existing hooks so the RED test does not require a new member to compile. Assert behavior, not fixed help-box prose or helper calls.

```csharp
Assert.That(drawnRows.Any(row => row.Kind == MaterialTransformationRowKind.Slot), Is.False);
Assert.That(drawnButtons, Does.Not.Contain("Compute"));
```

- [ ] **Step 2: Implement the single captured policy lifetime.** Capture target and shared key before all early returns. Snapshot `IgnoreOutOfRangeMaterialSlots` with the other mapped settings. Delete `BuildPolicyFingerprint`. Validate before a step can publish or cache results. Reuse one live-key computation within that step. Keep controller and graph keys. Add no policy framework and no automatic recompute on repaint. Delete the incidental `ComputePolicyFingerprintProducesConsistentString` field-count test rather than repin it. In `DryRunCacheTests.DryRunPolicyFingerprint`, replace reflection and the private-method existence assertion with `PlayModeBuildStore.ComputePolicyFingerprint(component, BuildTarget.StandaloneWindows64)`. Keep its backfill setup and behavioral cutout, transparent, separator, target, and cache-hit assertions.
- [ ] **Step 3: Integrate inspector precedence.** Pass the already computed live key to validation before displaying retained preview rows. Render `Settings changed. Select Refresh to compute new results.` for an invalidated run before the ordinary cancelled branch. Hide its Compute affordance. Refresh discards the old run through the existing flow. Closing and reopening the foldout does not clear the latch. Preserve ordinary user cancellation and error reporting.
- [ ] **Step 4: Run focused checks.** Run the preview, inspector, build-store, and `Alrauna.Amuse.Tests.Editor.DryRun.DryRunCacheTests` fixtures. Use the existing `RepaintThroughHostWindow` and row/action hooks. Expect non-zero counts and no failure.
- [ ] **Step 5: Smoke the actual inspector.** On a public synthetic root, complete a preview, edit policy, reopen the foldout, and observe the rejection without old rows. Exercise Review Focus 2. Callback assertions alone are not visual proof.
- [ ] **Step 6: Update the documentation.** Explain the Refresh-only recovery in README. Add one Unreleased policy-validity bullet.
- [ ] **Step 7: Obtain the task review.** Require spec compliance and quality approval.
- [ ] **Step 8: Commit.** Commit as `fix(preview): reject runs after policy changes`.

### Task 4: Give recorded refusal causes display precedence

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md`

**Interfaces:** Keep `IsSlotDeferred(Renderer, int, SlotResolutionResult, CapturedMaterialSlotEvidence) -> bool`. Keep the first-blocker resolver and all evidence records unchanged.

- [ ] **Step 1: Write real combined-cause regressions and observe RED.** Reuse `AttestedLockedMaterialReportsDeferredSentenceWithPolygonCount` and the existing animation-capture fixtures. Add invalid-swap plus locked-current and invalid-swap plus streaming-current cases. Add a streaming current material followed by a permanently unsupported swap material, so the swap is the recorded blocker. Include mixed sibling slots and an early renderer refusal with no slot results.

```csharp
Assert.That(refusedRow.ProvenTriangles, Is.EqualTo(-1));
Assert.That(rows.Any(row => row.Kind == MaterialTransformationRowKind.Renderer), Is.True);
```

- [ ] **Step 2: Gate the existing capability checks by the recorded cause.** Closure failures and every other named permanent cause return false before material shape can override them. Permit the locked-current arm only for the recorded semantics-unknown locked-shader cause with a non-refused locked identity, corroborated by the existing current-material checks. A locked swap value with insufficient recorded recovery evidence stays refused. Preserve the valid locked-current case. For the existing streaming arm, require exactly the seam-only conditions named in PTM3. Do not add a streaming producer, new carrier, or later-blocker scan.
- [ ] **Step 3: Preserve the actual streaming path.** Keep the resolved `MissingTextureEvidence` row behavior and avatar streaming note. Do not create a permanent test claiming a production streaming-only deferred row. A throwaway seam characterization may exercise the retained unnamed-cause branch.
- [ ] **Step 4: Run the preview fixture.** Run `Alrauna.Amuse.Tests.Editor.DryRun.AlphaSeparationDryRunTests`. Expect non-zero tests and no failure.
- [ ] **Step 5: Smoke the combined cause.** Pass the invalid-swap fixture through real capture and inspect the refusal row and renderer warning. The locked-only fixture must remain deferred. Review Focus 3 must report the swap refusal, not a promise about the current material.
- [ ] **Step 6: Update Unreleased.** Record the named-cause reporting fix.
- [ ] **Step 7: Obtain the task review.** Require spec compliance and quality approval.
- [ ] **Step 8: Commit.** Commit as `fix(preview): preserve recorded refusal causes`.

### Task 5: Remove the Poiyomi static initialization dependency

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs`
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md`

**Interfaces:** `PoiyomiAlphaInterpreter.AlphaRequiredSchemaProperties` becomes the only alpha schema field. Delete only the facade's `AlphaRequiredSchemaProperties` field. Keep the facade's private full-schema `RequiredSchemaProperties` array and its two facade consumers unchanged. Keep every evidence-request property and the used facade.

- [ ] **Step 1: Retain the observed failure evidence.** The preflight loader at `Temp/AmuseStaticEntryProbe.cs` entered the unchanged interpreter first in a fresh bundled-Mono process and observed nested type-initialization failures ending in `ArgumentNullException`. Facade-first passed the required schema checks. Do not repeat the old run merely to confirm it.
- [ ] **Step 2: Move the schema before request initialization.** Move the four-property array into the interpreter before all request initializers. Retarget the interpreter request builder and source attestation reader. Keep the values `shader_master_label`, `_ShaderOptimizerEnabled`, `_MainTex`, and `_Color`. Leave full-schema ownership and facade consumers intact. No lazy wrapper or new schema type.
- [ ] **Step 3: Run focused semantics checks.** Run the Poiyomi semantics group `Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi`. Expect non-zero tests and no changed outcomes.
- [ ] **Step 4: Prove both fresh entry orders.** Refresh the dev editor and wait for compilation before using the new assembly. Run the existing temporary loader in two new bundled-Mono processes. Enter interpreter first, then facade first in separate processes. Both must exit 0 and expose the required schema on both requests. This is the changed-path smoke, not a warmed type test. Do not add a permanent subprocess harness.

The earlier loader file was removed. Recreate a minimal throwaway bundled-Mono reflection loader from the spec's PTM4 fresh-process requirements and Step 4's schema checks. Do not assume saved loader source exists. No permanent harness or repeated baseline run.
- [ ] **Step 5: Update Unreleased.** Record the initialization-order fix.
- [ ] **Step 6: Obtain the task review.** Require spec compliance and quality approval.
- [ ] **Step 7: Commit.** Commit as `fix(poiyomi): own alpha schema before request initialization`.

### Task 6: Keep slot evidence with its resolution results

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/RuntimeStateResolution.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPass.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/DryRunRowBuilder.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/RuntimeStateResolutionTests.cs`
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md`

**Interfaces:** Add `IReadOnlyList<CapturedMaterialSlotEvidence> Slots { get; }` to `ResolvedRuntimeStates`. Its constructor becomes `(RendererAnalysisRefusal refusal, IReadOnlyList<CapturedAlphaMaterial> currentMaterials, IReadOnlyList<CapturedMaterialSlotEvidence> slots, SlotResolutionResult[] slotResults)`. Remove the `slots` argument from `DryRunRowBuilder.BuildResolvedRefusalRows`. Keep `MaterialSlotsFor` for preparation's independent use. No old overload.

- [ ] **Step 1: Characterize attribution at the real seam.** Extend existing resolver fixtures only where coverage is missing. Exercise typed renderer bindings, current material plus swap alternatives, a closure-failed slot, a valid sibling, all-slots-refused, and renderer-wide early refusal. Assert material attribution and outcomes. Do not assert reference equality, list construction count, or delegation.
- [ ] **Step 2: Return the existing list without another construction or copy.** Populate `Slots` at all three constructor returns. Renderer-wide early refusal gets shared empty slot and result arrays. All-slots-refused keeps both lists. Successful and mixed cases keep order and existing current-material behavior.
- [ ] **Step 3: Migrate every consumer and delete redundant state.** Use `resolved.Slots` in build and preview. Delete both duplicate `MaterialSlotsFor` calls and the preview `_inFlightSlots` field, assignment, and reset. Read the retained result's slots when rows complete. Let `BuildResolvedRefusalRows` read its result's slots. `BuildRows` keeps its slot parameter and receives the paired list. Update the obsolete attribution comments.
- [ ] **Step 4: Run focused behavior checks.** Run resolver, preview, plugin, preparation, and apply groups. Preserve `RuntimeStateProductionEntry_PostClosureSlotRefusalKeepsTheValidSibling`, `RuntimeStateProductionEntry_AllSlotsClosureFailedStillRefuseTheRenderer`, and typed renderer closure cases.
- [ ] **Step 5: Smoke slot attribution.** Build a public synthetic mixed-slot fixture and inspect correct slot attribution without source writes.
- [ ] **Step 6: Update Unreleased.** Record the single-owner evidence change.
- [ ] **Step 7: Obtain the task review.** Require spec compliance and quality approval.
- [ ] **Step 8: Commit.** Commit as `refactor(analysis): retain slots with resolved states`.

### Task 7: Use the typed row-kind wire contract everywhere

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs`
- Inspect existing imports: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
- Inspect existing imports: `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPass.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/PlayModeBuildStoreTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs`
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md`

**Interfaces:** Move `MaterialTransformationRowKind` to the Build namespace beside the DTO. Use `Banner = 0`, `Note = 1`, `Renderer = 2`, `Slot = 3`, `Errored = 4`. `SerializedTransformationRow.kind` uses that enum. Remove the old definition and cast.

- [ ] **Step 1: Protect numeric payload behavior.** Deserialize synthetic JSON with each old numeric kind and draw the resulting row shape. Keep the real source/material row fields. Assert the expected `Kind` and inspector shape, not a test-only conversion echo. Cover all five assigned numbers without changing undefined-value behavior. This is compatibility characterization.
- [ ] **Step 2: Cut over the enum and every writer.** Replace literals in the pass and apply code, including `row.kind == 3` and the apply slot-row initializer. Change the DTO and reader directly. No second enum, alias, or converter.
- [ ] **Step 3: Migrate test fixtures and namespace-qualified uses.** Replace numeric DTO initializers, integer casts, and numeric comparisons in apply, plugin, store, and inspector tests. Update fully qualified `.DryRun.MaterialTransformationRowKind` references and imports in all consumers. Search both AMUSE packages for the old qualified name and numeric DTO uses before declaring the cutover complete.
- [ ] **Step 4: Run payload consumer checks.** Run the store, row, preview, inspector, plugin, and apply groups. Expect non-zero counts and no failure.
- [ ] **Step 5: Smoke the old numeric payloads.** Exercise Review Focus 4 through `JsonUtility` and the actual row drawer. The serialized numeric values must remain unchanged.
- [ ] **Step 6: Update Unreleased.** Record the explicit row-contract maintenance change.
- [ ] **Step 7: Obtain the task review.** Require spec compliance and quality approval.
- [ ] **Step 8: Commit.** Commit as `refactor(reports): type stored transformation row kinds`.

### Task 8: Reuse the approved-identity rule in consent

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md`

**Interfaces:** Keep `IsApprovedIdentityFailure(CapturedAlphaMaterial) -> bool` and `CollectTransferConsentCore` signatures. No new interface or test seam.

- [ ] **Step 1: Select existing behavior protection.** Use `ApprovedVersionProducesNoSubjectOnTheConsentCore`, `ApprovedByteChangedMultiProducesNoSubject`, `ApprovedIdentityFailureAloneEarnsTheCauseFlag`, `ApprovedAndUnapprovedSameNamedMaterialsProduceOnlyTheOutOfRangeSubject`, and `ApprovedOnlyBuildGrantsNoShaderNames`. Keep state-refused and unsupported cases. Do not add a forwarding test.
- [ ] **Step 2: Replace only the repeated predicate.** Call `IsApprovedIdentityFailure(captured[index])` after the existing attestation filter. Delete the duplicated approval and identity-refusal locals. Preserve captured-record invariants and all consent outputs.
- [ ] **Step 3: Run the focused semantics fixture.** Run `Alrauna.Amuse.Tests.Editor.Semantics.UnityMaterialSemanticsTests`. Expect non-zero tests and no changed outputs.
- [ ] **Step 4: Smoke real capture to consent.** Observe the same subjects, approved flag, and granted names for regular, Multi identity-refused, Multi state-refused, out-of-range, and unsupported inputs. Use public stand-in material fixtures and the real core.
- [ ] **Step 5: Update Unreleased.** Record the shared-rule maintenance change.
- [ ] **Step 6: Obtain the task review.** Require spec compliance and quality approval.
- [ ] **Step 7: Commit.** Commit as `refactor(semantics): share approved identity failure rule`.

### Task 9: Correct shader measurement-status prose

**Files:**
- Modify comments only: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
- Modify comments only: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiResolution.cs`
- Modify comments only: `Packages/com.alrauna.amuse/Tests/Editor/Build/VerifiedLilToonTestSeams.cs`

**Interfaces:** No code, pin, schema, version, or fixture-value change.

- [ ] **Step 1: Check the owning evidence.** Read the 2.4.1 row comments and the 2026-10-20 admission design. Keep single-shape Multi status separate from outstanding regular-family and opaque-tuple work. Do not rerun vendor measurements.
- [ ] **Step 2: Correct only the stale statements.** Remove the universal two-shape claim. Retain each row's actual evidence status. Remove repeated version lists from the resolver and seam comments. Refer to the owning admitted rows instead.
- [ ] **Step 3: Verify comment-only scope.** Review the exact diff. Confirm that no attestation literal or executable token changed. The comments are the documentation update. Do not add a source-text test, count test, or a changelog claim that measurements ran.
- [ ] **Step 4: Run the document checks.** Sweep for forbidden identifiers and whitespace defects.
- [ ] **Step 5: Obtain the task review.** Require comment-only scope and evidence accuracy approval.
- [ ] **Step 6: Commit.** Commit as `docs(liltoon): keep measurement status with admitted rows`.

### Task 10: Own session-cache reset membership once

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Build/AmuseSessionCaches.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Build/AmuseSessionCaches.cs.meta`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPass.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md`

**Interfaces:** Add `internal static class AmuseSessionCaches` with `internal static void Clear()`. It owns only the existing fixed reset sequence.

Let the dev editor generate the new metadata once when it imports the script. Do not replace an already generated metadata file.

- [ ] **Step 1: Move the fixed seven calls without changing order.** Keep `UnityGeneratedTextureEvidence.ClearCache`, `UnityAlphaFieldEvidence.ClearCache`, `ReplacementTextureIdentity.ClearSession`, both replacement-tool version cache clears, lilToon's gather memo clear, and Poiyomi's source-hash memo clear.
- [ ] **Step 2: Replace both lists.** Call `AmuseSessionCaches.Clear()` at the original build-entry position before the lifecycle-refusal return. Call it at the original clone-sweep teardown position. Do not merge the moments, add a registry, or alter clone destruction. The native Play identity snapshot is not a reset member or a clone. Do not register or destroy it here. Store lifecycle keeps it alive through both reset moments until Play exit.
- [ ] **Step 3: Run lifecycle checks.** Run plugin and apply fixtures, including `SweepDestroysExactlyTheUnreferencedClones` and `PlannedSplitInvalidatedLateSweepsItsCloneWithoutAnyWrite`. Do not add reset-member-count or helper-call tests.
- [ ] **Step 4: Smoke two builds.** Use two sequential public synthetic builds with changed texture input. Observe fresh output and teardown.
- [ ] **Step 5: Update Unreleased.** Record the fixed-list ownership change.
- [ ] **Step 6: Obtain the task review.** Require spec compliance and quality approval, including the script and `.meta` as one unit.
- [ ] **Step 7: Commit.** Commit as `refactor(build): share the session cache reset list`.

### Task 11: Extend the research no-write tripwire

**Files:**
- Modify: `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/ResearchSourceApiBanTests.cs`
- Modify: `docs/development-setup.md`

**Interfaces:** Add a test-local `FindOffences(string source) -> IReadOnlyList<string>` that performs the existing literal, pattern, and AssetDatabase checks. The production-source scan and synthetic snippet cases use that same implementation. No production guard or parser dependency.

- [ ] **Step 1: Write synthetic forbidden-call cases and observe RED.** Include `File.WriteAllText`, `File.AppendAllText`, `File.Copy`, `new StreamWriter`, a `FileStream` opened for write, `PlayerPrefs.SetString`, `EditorPrefs.SetString`, `SessionState.SetString`, `HttpClient.PostAsync`, `UnityWebRequest.SendWebRequest`, and socket transmission. Assert a named offence for each source snippet. Include allowed `File.ReadAllText`, `File.OpenRead`, `Path.Combine`, an explicitly read-only `FileStream`, and the two allowed AssetDatabase lookups. Use source strings only. Do not execute a forbidden snippet.
- [ ] **Step 2: Extend the existing fixed checks.** Add direct persistence and transmission forms needed by those cases. Keep production `Editor` scope, test-source exclusion, non-vacuous scan, current Unity mutation checks, and the existing asset lookup allowlist. Writable-stream checks must not reject an explicitly read-only stream. Keep regex/literal limits explicit. Do not claim this is a sandbox.
- [ ] **Step 3: Run the focused guard and actual production scan.** Run `Alrauna.Amuse.Research.Tests.Editor.Collection.ResearchSourceApiBanTests`. Expect named offences for adversarial snippets, no offence for allowed snippets, and no offence in real research production source.
- [ ] **Step 4: Run the full research assembly.** Expect a terminal aggregate with non-zero counts and no failure.
- [ ] **Step 5: Update development test instructions.** State the tripwire's scope and limits. Do not add research content to the product changelog or archive.
- [ ] **Step 6: Obtain the task review.** Require privacy coverage, preserved allowed reads, and no new framework.
- [ ] **Step 7: Commit.** Commit as `test(research): reject direct persistence and transmission APIs`.

### Task 12: Remove the incidental lilToon property count

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/ShaderSemanticsArchitectureTests.cs`
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md`

**Interfaces:** No production change. Keep the existing naming and uniqueness behavior.

- [ ] **Step 1: Delete the exact-48 assertion.** Use the existing Poiyomi sibling's non-empty discovered-set check. Keep every property-value, underscore-prefix, and uniqueness assertion. Do not replace 48 with another count or add a source-text test for this edit.
- [ ] **Step 2: Run existing contract coverage.** Run `Alrauna.Amuse.Tests.Editor.Semantics.ShaderSemanticsArchitectureTests` and the `.*LilToon.*` group pattern in the product assembly. A valid new property must not require a count edit. Do not invent a production property to prove that statement. An in-memory counterexample is enough if needed.
- [ ] **Step 3: Update Unreleased.** State the durable property contract.
- [ ] **Step 4: Obtain the task review.** Require preserved naming, uniqueness, and property behavior checks.
- [ ] **Step 5: Commit.** Commit as `test(semantics): remove the incidental property count pin`.

### Task 13: Register the website download action once

**Files:**
- Modify: `Website/app.js`
- Inspect for the browser smoke: `Website/index.html`
- Inspect for the browser smoke: `Website/styles.css`
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md`

**Interfaces:** Keep the existing DOM and package URL data. One stable `change` listener reads a selected-URL variable. Every row-button click updates that variable from `event.currentTarget`. Open with `window.open(url, '_blank', 'noopener')`.

- [ ] **Step 1: Retain the real-runtime before evidence.** The isolated Fluent 2.6.1 browser run observed one window action after the first menu open and two after the second. Mouse and Enter both emitted `change`. No need to rerun the old handler to confirm this. The owner authorized read-only access to the pinned CDN runtime and its module dependencies.
- [ ] **Step 2: Make the smallest handler cutover.** Move download listener registration outside the row-button handler. Delete the per-open wrapper and incorrect removal. Store the URL on every row activation, including while the menu is open. Prevent that row click from also reaching the document's outside-menu close handler. Keep the chosen menu available. Use the row button as the geometry and URL owner. Do not add a click listener for the menu item or change unrelated modal code.
- [ ] **Step 3: Run the actual browser surface with the pinned runtime.** Load source markup, the existing stylesheet and theme setup, and the changed handler block in an isolated page. Intercept `window.open`. Exercise mouse, keyboard opening, Enter activation, repeated close/open cycles, both rows, and the button's rendered content. Open the first row's menu, switch to the second row while open, then press Enter. Assert one action with the second row's URL and `noopener`. Verify outside clicks close the menu and keyboard opening keeps it on screen. Do not force a direct SVG click where Fluent disables SVG pointer events.
- [ ] **Step 4: Observe visual proof.** Capture the real menu surface and confirm it remains usable after row switching. No package download, dependency install, vendoring, upload, or hosted deployment is required or authorized.
- [ ] **Step 5: Close the probe tab.** Release the isolated browser surface.
- [ ] **Step 6: Update Unreleased.** Record the listing download fix without claiming hosted execution.
- [ ] **Step 7: Obtain the task review.** Require one action per activation, correct row ownership, and keyboard use.
- [ ] **Step 8: Commit.** Commit as `fix(website): register one package download action`.

### Task 14: Verify the integrated branch and record the evidence

**Files:**
- Update execution evidence in this plan after checks complete
- No new product feature or test framework

**Interfaces:** Consumes all finalized task changes. Produces observed acceptance evidence and the final whole-branch review.

- [ ] **Step 1: Run both full assemblies once on the integrated code.** Run `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor`. Require terminal aggregate results with non-zero counts and no failure. Report warnings and errors. Do not substitute compilation or a partial failed job.
- [ ] **Step 2: Smoke saved-root ownership on final code.** Repeat Task 2's safe in-place NDMF and native recovery matrix, including saved prefabs, nested roots, the rename window, all four domain/scene reload combinations, and mid-Play script reload. Include canceled entry, idle cleanup racing immediate entry, and a fresh second entry. Use public fixtures and preserve authored scene setup.
- [ ] **Step 3: Smoke the final inspector.** Complete a preview, edit policy, reopen the foldout, and observe rejection with no stale rows. Include the observed-mismatch and revert case.
- [ ] **Step 4: Smoke mixed refusal attribution.** Run the public mixed-slot build fixture. Confirm refusal scope and source mesh, material, animation, and importer preservation.
- [ ] **Step 5: Smoke final cache lifetime.** Run two sequential synthetic builds with changed texture input. Confirm fresh output and transient teardown.
- [ ] **Step 6: Smoke both final Poiyomi entry orders.** Use the fresh-process mechanism from PTM4 against the final compiled assembly. Require both schema checks to pass.
- [ ] **Step 7: Smoke the final download menu.** Run the real Fluent mouse, Enter, repeated-open, and open-menu row-switch matrix. Observe one correct intercepted action per activation.
- [ ] **Step 8: Check the release boundary locally.** Exercise the single-product package membership recipe. Expect zero test entries, zero research entries, and the production editor assembly definition present. Do not run or claim the hosted release workflow.
- [ ] **Step 9: Request final adversarial whole-branch review.** Supply the finalized spec, plan, full branch diff package, and sanitized task ledger. Cover accuracy, safety and security, architecture, ponytail, latent bugs, and deferred findings. Fix important findings through a subagent, run covering checks, and obtain scoped confirmation. Stop for an owner choice rather than park a load-bearing defect silently.
- [ ] **Step 10: Record final evidence.** Record observed counts, smoke outcomes, limits, review disposition, and unchanged user-owned files in this plan.
- [ ] **Step 11: Run record checks.** Sweep changed documents and comments for forbidden identifiers. Run whitespace checks.
- [ ] **Step 12: Commit the verification record.** Commit as `docs: record maintainability fix verification`. Do not push or merge.
- [ ] **Step 13: Restore session-only review routing.** Restore its pre-run value without saving any permanent setting.

## Self-review checklist

- [x] Each spec section and the approved baseline correction has an owning task.
- [x] Each step names one concrete action and its checkable outcome.
- [x] Interfaces, type names, enum values, and called methods agree across tasks.
- [x] Each Review Focus case has a test or actual-surface check in its owning task.
- [x] No task adds a wiring test, a source-text pin, a fallback, or an unrequested framework.
- [x] Every finalized change has a focused verification gate, review gate, explicit file scope, and commit.

## Adversarial plan review

Five parallel reviewers checked spec and investigation accuracy, safety and security, architecture, ponytail, and latent bugs on 2026-10-21. The corrections add the reflected formatter test to Task 3, protect root-fixture teardown through domain reload, cover destruction without automatic eviction, and qualify the alpha-only schema move. Scoped accuracy and safety checks confirmed that every raised finding was addressed. No blocking plan finding remained on 2026-10-21.

## Recovered baseline summary

On 2026-10-21, the controller recovered the cached aggregate from the test manager without rerunning tests. The exposed failed-job serializer omits this aggregate. That tool defect was reported. The dependency was not modified.

| Run | Total | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| Full AMUSE baseline before product edits | 2970 | 2964 | 2 | 4 |

Progress counters and aggregate totals differ. Do not combine them or derive a pass count from progress. Task 1 must still obtain a clean terminal aggregate after the approved test corrections.

## Execution record on 2026-10-21

Task 1 is complete in commit `c0509cb`. The task review approved spec compliance and quality. A scoped review confirmed the final test-comment citation.

| Final prerequisite run | All outcomes | Passed | Failed | Ignored | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Both full AMUSE assemblies | 2972 | 2966 | 0 | 4 | 2 |

A native callback retained all four result categories. The four ignored vendor checks need the absent Poiyomi package. The two inconclusive SDK-sequence checks need a disposable public integration project. Their destructive opt-in stayed off. No vendor package was installed.

At the first pause, Task 2 stopped at the probe setup prerequisite. Unity refused to create a new additive scene while an untitled scene was open. The runtime scene-creation API also refused use outside Play mode. The controller did not save or close the original scene.

At that pause, no Play transition had run. The identity candidate was not proved or disproved. No product source changed. The probe restored both Enter Play Mode option values and removed its owned folder and metadata. The original scene setup, active scene, saved scene bytes, editor-settings bytes, and five pre-existing modified files matched their captured state. The editor remained in Edit mode with no dirty scene.

The owner then saved the open scene and requested resume. Tasks 2 through 14 remain incomplete. The spec and its acceptance criteria remain unchanged.

### Resumed identity gate on 2026-10-21

The controller pinned the dev editor and observed zero unsaved scenes and zero dirty scenes. The restored temporary helper compiled. The probe then completed one real Play entry and exit with scene reload enabled.

| Saved root | Entry, rename, and fake-sibling checks combined | Play-to-Edit identity correlation |
| --- | --- | --- |
| Loose root | Passed | Passed |
| Prefab root | Failed | Failed |

The prefab-instance status was true before Play, false during Play, and true after exit. The probe replayed the host rename window. It did not invoke the host build pipeline. The combined entry check does not identify which subcheck failed. The failed exit check does show that the proposed key cannot prove the required prefab round trip.

The gate returned `FAIL`. The scene-reload-disabled gate did not run after this failure. No production source changed. The controller restored both runtime option values and their serialized file bytes, removed the owned probe assets, and preserved the original scene setup and captured user-owned files.

Task 2 needs an owner decision about a revised identity design or a change to the saved-prefab Play-record requirement. Do not implement the proposed full identity-string key or add an unapproved fallback.

### Revised identity plan on 2026-10-21

The owner chose to preserve prefab Play reports. The revised spec is committed as `fc19e86`. Native reference matching passed the public reload probes. CLR wrapper matching failed and was rejected. No host or complete product-lifecycle result is inferred from those probes.

Task 1 stays complete. The revised Task 2 replaces the failed direct key with the store-owned native snapshot, adds the saved-scene fixture scope, names every affected test, and closes lifecycle and reload gate gaps. Task 10 must not own the snapshot. Task 14 repeats the final identity matrix. This plan revision needs parallel adversarial review before execution.

### Plan review corrections on 2026-10-21

The review found that `VRChatPlatform` is internal. The corrected gate uses the public ambient provider with a VRChat identity check. Its null output intentionally yields a named AMUSE refusal, while the probe owns its identity payloads. It processes a fixture only once per Play session and handles any surviving fixture-only processing tag before re-entry.

The dispatcher-clone concern was closed by read-only IL inspection plus NDMF source. That is not a claim that the full SDK sequence ran.

The Unity runner creates an active untitled scene. A native API probe showed that creating another additive scene fails, but opening an existing saved scene additively succeeds while the untitled scene stays active and loaded. A copied empty public scene fixture therefore replaces the proposed additive scene creation in the test helper. No runner or user scene is saved, closed, or replaced.

### Revised plan review finalized on 2026-10-21

Accuracy, safety and security, architecture, ponytail, and latent-bug reviewers approved the corrected plan. Scoped confirmation closed all Important findings and the proposed owner-choice premise. Read-only SDK forwarding evidence removed that premise without running the callback chain.

The final corrections replace internal-type references with public APIs or exact fixture-only type-name matching. They name the admitted product smoke output, keep the refused mechanism probe separate, use copied saved scenes beside the runner's untitled scene, and pin failure-safe teardown. The constraints copy covers all captured user-owned files.

The null-output refusal path does save a banner record before returning. The positive product smoke still needs a non-null owned output to prove the admitted path. The reviewers confirmed this distinction. No host-pipeline or product-lifecycle pass is claimed before execution.
