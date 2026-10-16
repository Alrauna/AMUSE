# Deferred minors remediation implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the five deferred documentation and test minors left by the 2026-10-15 full repo audit round.

**Architecture:** Five independent one-site edits on the audit branch `audit/full-repo-audit-2026-10-15`, on top of commit 116403a. No behavior change. One commit per task.

**Tech Stack:** C# against Unity 2022.3, NUnit via Unity Test Framework, EditMode only. Compilation happens inside Unity; there is no dotnet build.

**Spec:** `docs/superpowers/specs/2026-10-15-deferred-minors-remediation-design.md`

## Global Constraints

- No production behavior change in any task.
- No pinned attestation constant (shader GUID, name, version, digest) changes.
- Tests run through the Unity Test Runner, EditMode mode, one class per filtered run. A filtered run that reports 0 tests is a failure.
- Every touched file passes the identifier sweep before its commit. The sweep checks for an at sign joined to a hexadecimal hash, drive-letter paths, home-directory paths, four-digit ports, and private names known to the session.
- Never stage the `Assets/AmuseTests_Temp *.meta` working-tree artifacts.
- Commit message style matches the branch: lowercase type prefix, one line, no body.
- If a site does not match the quoted text, stop and report. The line numbers were read at 116403a.

---

### Task 1: Name the unlock eligibility universe in signature and doc

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockAvailability.cs` (doc block above `WindowEligibleForConsent`, and its parameter list)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (the one call site, near line 626)

**Interfaces:**
- Consumes: nothing new.
- Produces: `WindowEligibleForConsent(IEnumerable<Material> assignedAndSwapCurvedMaterials, Func<Material, bool> originalShaderAttested)`. The parameter name change is internal; the method stays `internal static` with one caller.

- [ ] **Step 1: Confirm the one caller**

Grep `WindowEligibleForConsent` under `Packages/com.alrauna.amuse/Editor`. Expected: exactly one call site, in `AmusePlatformFinishPlugin.cs`, passing `AssignedAndSwapCurvedMaterials(context, graph)`. If more callers exist, update each (the rename is compile-forced) and list them in the report.

- [ ] **Step 2: Rename the parameter and fix the doc**

In `TransientUnlockAvailability.cs`, change the doc sentence from:

```
        /// Whether any assigned material makes the unlock window eligible
        /// on this build: a recognized locked identity whose original
        /// shader attests.
```

to:

```
        /// Whether any assigned or swap-curved material makes the unlock
        /// window eligible on this build: a recognized locked identity
        /// whose original shader attests.
```

and change the parameter from `IEnumerable<Material> assignedMaterials` to `IEnumerable<Material> assignedAndSwapCurvedMaterials`. Update the `foreach` loop variable reference from `assignedMaterials` to the new name.

- [ ] **Step 3: Update the call site argument name if it uses named arguments**

The call site at `AmusePlatformFinishPlugin.cs` passes arguments positionally. If it uses positional arguments, no change is needed; confirm by reading it. If named, rename to match.

- [ ] **Step 4: Compile**

Refresh Unity assets and wait for compilation. Expected: no errors. Expected: no new warnings.

- [ ] **Step 5: Run the owning test class**

Run `TransientUnlockAvailabilityTests`, EditMode, one class per filtered run. Expected: all pass, count greater than 0.

- [ ] **Step 6: Identifier sweep and commit**

Sweep both touched files. Then:

```bash
git add Packages/com.alrauna.amuse/Editor/Build/TransientUnlockAvailability.cs Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs
git commit -m "fix: name the unlock eligibility universe in signature and doc"
```

---

### Task 2: Correct the shader format stamp normalization reason

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs` (the `ShaderFormatStamp` doc block, near line 2573)

**Interfaces:**
- Consumes: nothing new.
- Produces: a corrected doc comment. No code change.

- [ ] **Step 1: Confirm the key comparer**

Read the memo dictionary declaration that uses `ShaderFormatStamp`'s tuple in its key. Confirm the key is the named tuple compared with the default comparer (tuple equality, per-element `EqualityComparer<T>.Default`, so `float.Equals`, where NaN equals NaN). Record what you saw in the report. If the key uses a custom comparer instead, stop and report: the corrected reason text below would be wrong.

- [ ] **Step 2: Rewrite the doc block**

Change:

```
        /// The shader format stamp one gather embeds, read from the captured
        /// material. Absent normalizes to 0: NaN never equals NaN, so an
        /// unnormalized absent stamp would make its memo key unmatchable.
        /// </summary>
```

to:

```
        /// The shader format stamp one gather embeds, read from the captured
        /// material. Absent normalizes to 0. The tuple key compares the
        /// float with float.Equals, where NaN does equal NaN, so the raw
        /// stamp would still match. The normalization is canonicalization:
        /// it keeps the special float out of the key, so the key's
        /// matchability never depends on NaN equality semantics.
        /// </summary>
```

- [ ] **Step 3: Compile**

Refresh Unity assets and wait for compilation. Expected: no errors.

- [ ] **Step 4: Grep the site**

Grep `unmatchable` in the file. Expected: 0 hits. Grep `canonicalization` in the file. Expected: 1 hit.

- [ ] **Step 5: Identifier sweep and commit**

Sweep the touched file. Then:

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs
git commit -m "docs: correct the shader-format-stamp normalization reason"
```

---

### Task 3: Note the shared pre-filter bound's repeat-scale reuse

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs` (the comment block above `PreFilterSafeCoordinateBound`, near line 287)

**Interfaces:**
- Consumes: nothing new.
- Produces: an extended comment. No code change.

- [ ] **Step 1: Extend the comment**

In the comment block that ends with `// margin, so the caller skips the pre-filter and lets the exact`, insert one more sentence pair after that line's thought completes. Change the block's final lines from:

```
        // margin, so the caller skips the pre-filter and lets the exact
        // intersection test decide.
        private const double PreFilterSafeCoordinateBound = 256;
```

to:

```
        // margin, so the caller skips the pre-filter and lets the exact
        // intersection test decide.
        //
        // The repeat-scale walks reuse this helper unmodified. The margin
        // argument depends only on the per-coordinate magnitude, never on
        // the caller, and a short margin fails conservative: a falsely
        // separated box only loses a witness, and a lost witness never
        // proves opacity.
        private const double PreFilterSafeCoordinateBound = 256;
```

- [ ] **Step 2: Compile**

Refresh Unity assets and wait for compilation. Expected: no errors.

- [ ] **Step 3: Grep the site**

Grep `repeat-scale walks reuse` in the file. Expected: 1 hit.

- [ ] **Step 4: Identifier sweep and commit**

Sweep the touched file. Then:

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs
git commit -m "docs: note the shared pre-filter bound's repeat-scale reuse"
```

---

### Task 4: Restore the dropped word in the clamp gate comment

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs` (the doc block above `BilinearClampSubstitutesErasedTexels`, near line 1542)

**Interfaces:**
- Consumes: nothing new.
- Produces: a corrected doc comment. No code change.

- [ ] **Step 1: Insert the word**

Change:

```
        /// candidate whose support interval intersects the domain. Opaque
        /// count too. Erasure substitutes when the erased
```

to:

```
        /// candidate whose support interval intersects the domain. Opaque
        /// candidates count too. Erasure substitutes when the erased
```

The three sibling blocks (`BilinearRepeatSubstitutesErasedTexels`, the classified clamp gate, and the classified repeat gate) already read "Opaque candidates count too." Do not touch them.

- [ ] **Step 2: Compile**

Refresh Unity assets and wait for compilation. Expected: no errors.

- [ ] **Step 3: Grep the site**

Grep `Opaque$` in the file with a line-anchored search, or grep `Opaque\ncount` across lines. Expected: 0 hits. Grep `Opaque candidates count too`. Expected: 4 hits.

- [ ] **Step 4: Identifier sweep and commit**

Sweep the touched file. Then:

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs
git commit -m "docs: restore the dropped word in the clamp gate comment"
```

---

### Task 5: Drop the racing completion assertion after an abandoned cancel

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs` (inside `CancellingWhileInFlightDisposesTheSourceWhenTheTaskCompletes`, near line 1301)

**Interfaces:**
- Consumes: nothing new.
- Produces: the same test with one fewer assertion. The test's subject, disposal after an abandoned cancel, keeps full coverage.

- [ ] **Step 1: Delete the racing assertion**

In `CancellingWhileInFlightDisposesTheSourceWhenTheTaskCompletes`, change:

```
            dryRun.Cancel(timeoutMs: 0);
            Assert.That(
                inFlight.IsCompleted,
                Is.False,
                "the task must still be running when Cancel abandons it");
            Assert.That(dryRun.IsCancelled, Is.True);
```

to:

```
            dryRun.Cancel(timeoutMs: 0);
            Assert.That(dryRun.IsCancelled, Is.True);
```

Leave the rest of the test untouched: the disposed-state wait, the follow-up `Cancel` tolerance assertion, and the `IsCompleted` uses in the other two cancel tests (`CancelAfterNormalCompletionToleratesDisposedSource` polls before asserting; it does not race).

- [ ] **Step 2: Run the owning test class**

Run `AlphaSeparationDryRunTests`, EditMode, one class per filtered run. Expected: all pass, count greater than 0. Run it twice if the first run is green, to show the flake window is gone rather than lucky.

- [ ] **Step 3: Grep the site**

Grep `must still be running when Cancel abandons` under `Tests/`. Expected: 0 hits.

- [ ] **Step 4: Identifier sweep and commit**

Sweep the touched file. Then:

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs
git commit -m "test: drop the racing completion assertion after abandoned cancel"
```

---

## Final verification

- [ ] **Run both touched test classes once more**: `TransientUnlockAvailabilityTests` and `AlphaSeparationDryRunTests`. Expected: all pass, counts greater than 0.
- [ ] **Grep the five sites**: `assignedAndSwapCurvedMaterials`, `canonicalization`, `repeat-scale walks reuse`, `Opaque candidates count too`, and the absence of `must still be running when Cancel abandons`.
- [ ] **Confirm the branch**: five new commits on top of 116403a, each touching the files its task names.
- [ ] **Identifier sweep**: run over every file changed by this plan.
