# Ponytail audit remediation implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** close all fourteen findings of the 2026-11-05 ponytail audit with falsifier-first soundness fixes and small hygiene fixes.

**Architecture:** two falsifier-first fixes at the proof and capture boundaries, then cache resets, documentation reconciliation, an abort sweep on the apply path, and workflow pinning. Every production behavior change lands after its falsifier is observed failing in the Unity Test Runner, except the two mechanism pins and the text-only decisions the design exempts.

**Tech stack:** C# against Unity 2022.3, NUnit EditMode tests through the Unity Test Runner, GitHub Actions YAML.

**Spec:** `docs/superpowers/specs/2026-11-05-ponytail-audit-remediation-design.md`

## Global Constraints

- Tests run only through the Unity Test Runner in EditMode, after a Unity refresh. A filtered run reporting 0 tests is a failure. Record observed pass and fail counts for every run.
- After each task's refresh, read the Unity console: zero errors and zero new warnings, per the design's acceptance item 1.
- The pre-filter bound stays 256 (`PreFilterSafeCoordinateBound`). No constant changes.
- The D2 allowlist is exactly Alpha8, R8, RGB24, RGBA32, ARGB32. No refusal enum value is added.
- After Task 4, `AmuseSessionCaches.Clear()` performs exactly eight resets and its contract comment says eight.
- Every workflow `uses:` ref becomes a full 40-character commit SHA. The external automation checkout gains a `ref`. The four existing release SHA pins stay untouched.
- The release concurrency group is `release` with `cancel-in-progress: false`.
- The lilToon request becomes exactly 2.4.1. The locked block stays untouched.
- Never modify a source asset, an import setting, a pinned attestation constant, a digest, or a scene.
- Fail closed. Rethrow originals. Never blanket-catch a programming failure into a refusal.
- One commit per implementation task, Tasks 1 through 8. Task 0 and Task 9 commit nothing.
- The identifier sweep means a grep over each touched file for drive-letter and home-directory paths, an at sign joined to a hex run, four-digit port numbers, host or instance names, and private asset names learned in the session. Fix every hit before the commit.
- Commit steps stage an explicit file list. Never `git add .` and never `git commit -a`. The pre-existing ProjectSettings churn is never staged.
- Falsifier tests carry the repo's `--- Falsifier N: <named wrong implementation> ---` marker and a no-op guard, per the convention used across the test tree.
- Line numbers were measured at commit `9ccfea8`. If a line drifted, re-anchor on the named symbol before editing.

## Review Focus

1. A repeat-wrapped texture wider than 256 texels whose triangle touches a texel support near the separation margin must classify MustRemainTransparent, never ProvenOpaque by pre-filter noise. Pinned by the Task 1 falsifier.
2. A compressed streaming import whose decode crosses the cutoff must answer from the imported decode, never from the authoring byte. Pinned by the Task 2 falsifier.
3. An edit to a separate `.anim` clip between preview runs must serve rows equal to a full recompute. Pinned by the Task 3 falsifier.
4. An exception after swap-in during a build must leave open pairs reverted, clones destroyed, and must surface the original exception. Pinned by the Task 6 helper test. The wiring-side check lands as a recorded limit unless a knob applies.
5. Two same-version release dispatches must serialize. Checked by the YAML inspection in Task 8. This surface has no runtime test. The limit is recorded there.

---

### Task 0: Baseline

**Files:** none. Observation only.

- [ ] **Step 1: Record the baselines**

Refresh Unity. Run the full `Alrauna.Amuse.Tests.Editor` assembly, then the full `Alrauna.Amuse.Research.Tests.Editor` assembly. Record observed pass and fail counts for both.

Then run each of these classes by name and record its pass and fail counts: `TriangleAlphaClassifierTests`, `UnityStreamingTextureEvidenceTests`, `UnityAlphaFieldEvidenceTests`, `DryRunCacheTests`, `AlphaSeparationDryRunTests`, `AmuseSessionCachesTests`, `AmuseAvatarOptimizerEditorTests`, `AlphaSeparationApplyTests`, `AmusePlatformFinishPluginTests`, `TransientUnlockWindowCloseTests`, `TransientUnlockSwapInTests`, `LilToonSourceAttestationTests`. These per-class numbers are the baseline every later verify step compares against.

---

### Task 1: Repeat pre-filter stand-down (D1, finding 1)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs` (four repeat walks at lines 768-771, 929-932, 1612-1615, 1765-1768, and the comment above `PreFilterSafeCoordinateBound`)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`

**Interfaces:**
- Produces: a per-walk gate computed from the extracted vertex extents plus a pinned conservative margin of 2 texels, compared against `PreFilterSafeCoordinateBound`. The walk-internal window margins are 1.5 for the bilinear walks and 1 for the point walks, so 2 covers both. The gate is verdict-neutral either way, because it only disables an optimization. No signature changes. The mapped wrappers need no edit because they delegate to the shared walks.

- [ ] **Step 1: Write the falsifier and the control**

Falsifier, marked `--- Falsifier 1: the repeat walk that trusts the pre-filter past its bound ---`: build an `AlphaTextureData` of a 4093-class width directly, no GPU, with repeat wrap and bilinear filtering. Place unwrapped triangle coordinates near 4000 texels, constructed with the file's inline `new ExactRational(n, d)` idiom used by the existing repeat cases. Place one non-opaque texel whose support the triangle crosses at an exact nonzero distance inside the roughly 1e-7 error stack. Assert MustRemainTransparent. The no-op guard asserts the fixture's own invariants: the witness texel is non-opaque and the exact overlap is nonempty, so a degenerate fixture can never fake a pass.

Control `ClampWalkAtSameMagnitudesKeepsItsVerdictAcrossTheStandDown`: the same geometry under clamp wrap, asserting MustRemainTransparent. That verdict is derivable before any run, because the clamp walks already stand down past the bound and the exact test decides, both before and after this task.

- [ ] **Step 2: Run both tests to verify the falsifier fails**

Refresh Unity, check the console is clean, and run the two tests by name. Expected: the falsifier FAILs with actual ProvenOpaque, which is the defect. The control PASSes. Record counts.

- [ ] **Step 3: Implement the stand-down**

In each of the four repeat walks, before the candidate loop, measure the largest absolute texel coordinate the walk can hand the pre-filter: vertex extents plus the 2-texel margin. When it exceeds the bound, disable the pre-filter for the whole walk so every candidate runs the exact intersection test. Below the bound, nothing changes. The clamp walks stay untouched.

- [ ] **Step 4: Update the bound comment**

Replace the sentence above `PreFilterSafeCoordinateBound` that says the repeat walks run it on unwrapped boxes so the bound never caps their magnitudes. The new text states the stand-down and cites the 2026-11-05 design as the closure of the 2026-10-15 D3 note.

- [ ] **Step 5: Verify green**

Refresh Unity, console clean. Run the two tests: both PASS. Run the full `TriangleAlphaClassifierTests` class: green with the Task 0 class baseline's counts. Any verdict change on an existing case is a stop condition.

- [ ] **Step 6: Sweep and commit**

Sweep both touched files. Stage exactly those two files and commit `fix: stand the repeat pre-filter down above its bound`.

---

### Task 2: Streaming direct-route format gate (D2, finding 2)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs` (the direct-arm entry at line 170), `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs` (new allowlist beside the extension allowlist at line 178)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityStreamingTextureEvidenceTests.cs`

**Interfaces:**
- Produces: `internal static bool AdmitsImportedFormat(TextureFormat format)` on `SourceImageAlphaReader`, admitting exactly Alpha8, R8, RGB24, RGBA32, ARGB32. `UnityStreamingTextureEvidence.TryCapture` consults it as `AdmitsImportedFormat(source.format)` immediately before the line-170 direct-route call. Skipped formats take the existing clone arm unchanged.

- [ ] **Step 1: Write the falsifier**

Falsifier, marked `--- Falsifier 2: the direct route that trusts authoring bytes for compressed imports ---`: use the existing `WritePng` importer fixture helper with its `configureImporter` callback. Configure compressed texture compression, block-aligned dimensions, streaming mipmaps on. Place a texel band that quantizes across the cutoff. Inside the test, assert three preconditions from the imported asset: the imported format is actually compressed, the decoded imported texel crosses the cutoff, and the authoring byte does not. Derive the expected verdict with `SourceImageMaskedChain.DecodeByte` applied to the decoded imported texel at the material cutoff, and name that concrete verdict in the assertion. The no-op guard is the three-precondition block itself: without it a mis-built fixture can pass or fail for the wrong reason.

Stop condition: if no test band can satisfy all three preconditions, stop the task and report. Never weaken the fixture to make it fit.

- [ ] **Step 2: Run to verify it fails**

Refresh Unity, console clean. Run the falsifier. Expected: FAIL, because the direct route proves opaque from authoring bytes. Record counts.

- [ ] **Step 3: Implement the gate**

Add `AdmitsImportedFormat` with its doc comment. Gate the line-170 entry on it. No other route changes. No new refusal value.

- [ ] **Step 4: Verify green**

Refresh Unity, console clean. Run the falsifier: PASS. Run the full `UnityStreamingTextureEvidenceTests` and `UnityAlphaFieldEvidenceTests` classes: green with their Task 0 class baselines.

- [ ] **Step 5: Sweep and commit**

Sweep both touched files. Stage exactly those files and commit `fix: gate the streaming direct route on uncompressed formats`.

---

### Task 3: Clip identity in the preview fingerprint (D4, finding 4)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs` (`BuildControllerFingerprint`, lines 1914-1931)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`

**Interfaces:**
- Consumes: the private harness already inside `AlphaSeparationDryRunTests` (`NewAvatarRoot`, `NewDryRun`, `Run`, and the capture-count observables beside the twin falsifier `RelockToTheSamePathChangesTheCacheFingerprint`). No test accessor reaches `BuildControllerFingerprint` directly, so the falsifier drives the real pipeline and observes the `DryRunCache` behavior from outside.
- Produces: the controller fingerprint joins `AssetIdentity(clip)` for every committed layer clip the graph walk holds. No public signature change.

- [ ] **Step 1: Write the falsifier**

Falsifier, marked `--- Falsifier 3: the fingerprint that misses clip file edits ---`, hosted in `AlphaSeparationDryRunTests` beside its twin. Create the clip with `AssetDatabase.CreateAsset` inside a `TestTransientScope` temp folder, hand it to a controller layer, build the avatar root and dry run with the file's private helpers, and compute rows. Then edit the `.anim` file on disk with one extra cutoff curve, call `AssetDatabase.ImportAsset`, and recompute. Assert the renderer cache misses, and assert the served fresh rows equal a full recompute, taken by clearing the cache and recomputing the same edited state. The no-op guard asserts the clip edit actually changed the file's imported tick.

- [ ] **Step 2: Run to verify it fails**

Refresh Unity, console clean. Run the falsifier. Expected: FAIL, because the cache hits and serves stale rows. Record counts.

- [ ] **Step 3: Implement the join**

In `BuildControllerFingerprint`, join `AssetIdentity(clip)` over the committed layer clips. Keep the clip count. Non-persistent clips hash as instance ids, which is safe because a domain reload wipes the cache.

- [ ] **Step 4: Verify green**

Refresh Unity, console clean. Run the falsifier: PASS. Run the full `DryRunCacheTests` and `AlphaSeparationDryRunTests` classes: green with their Task 0 class baselines.

- [ ] **Step 5: Sweep and commit**

Sweep both touched files. Stage exactly those files and commit `fix: join clip identity into the preview controller fingerprint`.

---

### Task 4: Streaming cache joins the session reset (D5 and D7, findings 5 and 7)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs` (clear method and emptiness probe beside its private static cache, doc comment at lines 113-118 and declaration at lines 119-123), `Packages/com.alrauna.amuse/Editor/Build/AmuseSessionCaches.cs` (`Clear` and its contract comment), `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs` (Refresh, lines 242-253), `Packages/com.alrauna.amuse/CHANGELOG.md` (Unreleased section)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseSessionCachesTests.cs`

**Interfaces:**
- Produces: `ClearCache()` on the streaming evidence type, named after the house precedent `UnityGeneratedTextureEvidence.ClearCache()`, and `internal static bool CacheIsEmpty` as the emptiness probe. `AmuseSessionCaches.Clear()` with the streaming cache as its eighth member, appended after the locked-source alignment memo. Refresh calling `AmuseSessionCaches.Clear()` beside its existing clears.

- [ ] **Step 1: Add the structural members first**

Add `ClearCache()` and `CacheIsEmpty` to the streaming evidence type. Neither exists at the audited commit, so the falsifier cannot compile before this step. Refresh Unity, console clean, no behavior change.

- [ ] **Step 2: Write the falsifier and verify it fails**

Falsifier, marked `--- Falsifier 4: the reset that forgets the streaming cache ---`: populate the streaming cache through its capture path, call `AmuseSessionCaches.Clear()`, assert `CacheIsEmpty`. Refresh Unity and run it. Expected: FAIL, because `Clear()` still omits the eighth reset. Record counts.

- [ ] **Step 3: Implement the reset**

Append the streaming cache reset to `AmuseSessionCaches.Clear()` as the eighth member, after the locked-source alignment memo. Update the contract comment from seven to eight, naming the streaming cache in its fixed position. Add the `Clear()` call to Refresh beside its existing clears.

- [ ] **Step 4: Verify green**

Refresh Unity, console clean. Run the falsifier: PASS. Run the full `AmuseSessionCachesTests` and `AmuseAvatarOptimizerEditorTests` classes: green with their Task 0 class baselines.

- [ ] **Step 5: Changelog and agreement check**

Add a dated Unreleased bullet recording the streaming reset. Grep that the changelog line about the shared helper and the contract comment both say eight. This is the D7 closure: the count line stays eight because this task restores the eighth member.

- [ ] **Step 6: Sweep and commit**

Sweep all touched files. Stage exactly those files and commit `fix: clear the streaming evidence cache with the session reset`.

---

### Task 5: Moved-count docs and dead overload (D3, finding 3)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (doc at lines 226-243), `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs` (delete lines 1048-1081: the stale doc comment 1048-1058 plus the four-argument overload 1059-1081. Lines 1083 onward are `SharedMeshOf`'s doc comment and must stay. Guard comment near lines 1232-1243), `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs` (doc comment near lines 6394-6399)

**Interfaces:**
- Produces: no signature changes. The four-argument `CountOpaqueTriangles` overload and its doc disappear. The two-argument overload at lines 755-772 is the only survivor and does not reference the twin.

- [ ] **Step 1: Verify the premise**

Grep the whole tree, tests and research included, for the four-argument overload. Confirm zero callers before deleting.

- [ ] **Step 2: Edit**

Delete lines 1048-1081. Rewrite the plugin state doc to the surviving-plan rule, citing the 2026-11-01 investigation and the 2026-11-02 amendment. Rewrite the stale test doc comment to state the behavior `ASelfMappedSurvivingSlotReportsItsProvenMovedTriangles` pins. Correct the guard comment: it excludes candidate slots whose write was a no-op, and it keeps its true Split clause.

- [ ] **Step 3: Verify**

Refresh Unity, console clean. Run `AlphaSeparationApplyTests` and `AmusePlatformFinishPluginTests`: green with their Task 0 class baselines. Grep the three rewritten sites for the abandoned-rule phrases, for example a per-write comparison between the written material and the held material: none may remain in the rewritten text.

- [ ] **Step 4: Sweep and commit**

Sweep all touched files. Stage exactly those files and commit `refactor: reconcile moved-count docs and drop the dead count overload`.

---

### Task 6: Abort sweep (D13, finding 13)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs` (new internal helper), `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPass.cs` (try-catch around the region after the `SwapIn` call at lines 381-386, extending to the end of the pass's `Execute`, a method starting near line 192), `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs` (try-catch around the `ApplyFinalization(finalization, state)` call inside `Execute`, near line 74, where the build context is in scope)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`

**Interfaces:**
- Produces: `internal static void RunAbortSweep(BuildContext context)` on `TransientUnlockWindowClose`. The reversion half calls `Execute`. The sweep half reads `context.GetState<AmusePlatformFinishState>().Separation` and calls `SweepUnreferencedClones` with per-renderer empty lists in the shape of lines 186-190. A null `Separation` lands inside the sweep half's own best-effort catch. Each half is wrapped in its own catch that logs through `Debug.LogException`, and the callers rethrow the original exception. Do not modify `ApplyFinalization` itself: six unit tests call it directly with hand-built states outside any build. An exception inside `SwapIn` itself stays uncovered, per the design's recorded residual.

- [ ] **Step 1: Write the helper test**

Construct a prepared state from the documented parts: `new BuildContext(root, TransientUnlockTestLifecycle.TempFolder)`, a positive `HostLifecycleCapability` on the finish state so `Execute`'s guard passes, `AnimatorBindings` set following the construction patterns near lines 664 and 1148, one `SwappedPair` built with the constructor-plus-initializer pattern at line 756, and a non-null `Separation` with a `Renderers` list for the sweep half. Call `RunAbortSweep`. Assert the pair reverted and the clones were destroyed. Expected green on first run: this is a mechanism pin for new code.

- [ ] **Step 2: Resolve the wiring-test question**

Check the `TransientUnlockTestKnobs` hooks. The expected outcome, established by the review, is that no knob fires inside the production pass-three post-SwapIn region or the pass-four mutation region: the knobs run in the test plugin's own passes, `BeforeClose` fires inside the close pass, and `RestoreOverride` throws inside `SwapIn`, which is the recorded residual. Confirm that on HEAD. If a knob nevertheless fits, write the wiring test that triggers it, asserts the sweep ran, and asserts the original exception type propagates. Record which path was taken in the commit message body.

- [ ] **Step 3: Implement the helper and the catch sites**

Add `RunAbortSweep`. Wrap the pass-three region and the pass-four `ApplyFinalization` call site in catch-and-rethrow blocks that call the helper.

- [ ] **Step 4: Verify**

Refresh Unity, console clean. Run the new tests plus the full `TransientUnlockWindowCloseTests`, `TransientUnlockSwapInTests`, and `AlphaSeparationApplyTests` classes: green with their Task 0 class baselines.

- [ ] **Step 5: Sweep and commit**

Sweep all touched files. Stage exactly those files and commit `fix: best-effort abort sweep when a build aborts after swap-in`, with the knob-availability outcome in the body.

---

### Task 7: Copy pin conversion (D12, finding 12)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` (lines 8343-8345)

**Interfaces:**
- Produces: one assertion converted from `Is.EqualTo` to `Does.Contain` on its count-bearing prefix. The `Does.Not.Contains("Splittable:")` guard at lines 8340-8342 stays.

- [ ] **Step 1: Convert**

Change the `Convertable: 2 out of 2 polygons may be moved` assertion to `Does.Contain("Convertable: 2 out of 2")`. Touch nothing else in the file.

- [ ] **Step 2: Verify**

Refresh Unity, console clean. Run `AmusePlatformFinishPluginTests`: green, counts identical to its Task 0 class baseline, and the diff shows one converted assertion, not a deletion.

- [ ] **Step 3: Sweep and commit**

Sweep the touched file. Stage exactly that file and commit `test: pin the convertible count without pinning the sentence`.

---

### Task 8: Text, manifest, and workflow corrections (D6, D8, D9, D10, D11, D14)

**Files:**
- Modify: `.github/workflows/build-listing.yml`, `.github/workflows/release.yml`, `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs` (lines 624-625), `docs/architecture/vision.md` (line 7), `Packages/vpm-manifest.json` (lines 15-16), `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToonSourceAttestationTests.cs` (comment lines 410-411 only, with the assertion block 412-418 untouched)

**Interfaces:**
- Produces: no code signature changes. The workflows gain SHA pins, an external checkout `ref`, and the release concurrency block.

- [ ] **Step 1: Text corrections**

Tooltip sentence at lines 624-625 takes the Safe-preset form of its cutout twin at lines 797-798. Vision line 7 becomes the dated statement naming 2026-11-05, the date this edit lands, with the commit and version coupling dropped. Manifest request becomes 2.4.1, locked block untouched. Stamp comment at lines 410-411 becomes the two-fact rewrite citing commit `b106675`, stating what the test pins and what the measurement says, leaving the assertion block untouched.

- [ ] **Step 2: Resolve the SHAs**

For each mutable tag in both workflow files, resolve the full commit SHA with `git ls-remote` against the upstream repository and tag. The external automation checkout has no tag, so resolve its default branch head with `git ls-remote <url> HEAD`. Keep the resolved SHAs for the commit body. If the network is unreachable, stop this task and report. Do not guess SHAs.

- [ ] **Step 3: Edit the workflows**

Replace each mutable tag with its SHA. Add `ref` to the external checkout with its SHA. Add the workflow-level concurrency group `release` with `cancel-in-progress: false` to the release workflow. The four existing release SHA pins stay untouched.

- [ ] **Step 4: Verify**

Grep that every `uses:` line in both files carries a full SHA. Grep the concurrency block. Grep the manifest for the 2.4.1 request and the 2.4.1 lock. Grep each corrected text site. Parse-check both YAML files with any available tool. The review established this machine has no YAML tool, so the expected path is the recorded careful read. Refresh Unity and run `AmuseAvatarOptimizerEditorTests` and `LilToonSourceAttestationTests`: green with their Task 0 class baselines.

- [ ] **Step 5: Sweep and commit**

Sweep all touched files. Stage exactly those files and commit `chore: pin workflow actions, add release concurrency, correct text and manifest`, with the resolved SHAs in the body.

---

### Task 9: Full validation

**Files:** none. Validation only.

- [ ] **Step 1: Acceptance sweep**

Verify each acceptance criterion of the design: decisions D1 through D14 each map to an executed diff. The grep proofs of acceptance items 4 through 7 run and pass. The four falsifiers are recorded as failed-before and passed-after with counts.

- [ ] **Step 2: Full assemblies**

Refresh Unity. Run the full `Alrauna.Amuse.Tests.Editor` assembly and the full `Alrauna.Amuse.Research.Tests.Editor` assembly. Record observed counts and compare against the Task 0 baselines. Expected: zero failures and no unexplained count change.

- [ ] **Step 3: Tree hygiene**

Run `git status --porcelain` and `git diff --check`. Expected: only this round's files plus the pre-existing ProjectSettings churn, and no whitespace errors. Run `git log --oneline 9ccfea8..HEAD` and confirm exactly eight commits, one per implementation task. Tasks 0 and 9 commit nothing.

- [ ] **Step 4: Report**

Report the executed decisions, the observed counts, the commit list, and every recorded limit: the Refresh wiring verified by code observation, the SwapIn residual, the knob availability outcome of Task 6, and the YAML tool availability of Task 8.
