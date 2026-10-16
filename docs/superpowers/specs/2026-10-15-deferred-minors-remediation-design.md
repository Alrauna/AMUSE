# Deferred minors remediation design

Date: 2026-10-15
Status: design, written after the final review of the 2026-10-15 full repo audit remediation round
Input: the final review triage of `audit/full-repo-audit-2026-10-15` at commit 116403a

## Scope

Five deferred minors from the audit round. All are comments, doc text, a signature name, or one test assertion. No behavior change. Work lands on the same audit branch, on top of 116403a.

## Decisions

### D1. Describe the unlock eligibility universe exactly

Finding: the doc and parameter name under-describe the input since the round widened the universe to assigned plus swap-curved materials.

Site: `Editor/Build/TransientUnlockAvailability.cs` `WindowEligibleForConsent`, doc block and `assignedMaterials` parameter. Caller: `Editor/Build/AmusePlatformFinishPlugin.cs` passes `AssignedAndSwapCurvedMaterials(context, graph)`.

Decision: rename the parameter to `assignedAndSwapCurvedMaterials` and update the doc sentence to name both groups. One internal caller, so the rename is compile-forced. Why: a parameter name that names a smaller set is the same defect as a wrong doc sentence, and the fix is two lines.

### D2. Correct the shader format stamp normalization reason

Finding: the doc gives a wrong reason. It says NaN never equals NaN, so an unnormalized absent stamp would make the memo key unmatchable. The memo key compares the `(bool, float)` tuple with the default comparer, which uses `float.Equals`. There, NaN does equal NaN. The claimed failure mode cannot happen.

Site: `Editor/Semantics/LilToon/LilToonSourceAttestation.cs`, `ShaderFormatStamp` doc block.

Decision: keep the normalization code. Rewrite the reason. The normalization is canonicalization: it keeps the special float out of the key, so key matchability never depends on NaN equality semantics. Why keep the code: canonical keys are simpler to reason about, and the code is correct. Risk of the change: none, comment only. The implementer confirms the tuple key uses the default comparer before editing.

### D3. Note the shared pre-filter bound's repeat-scale reuse

Finding: the rounding margin for `PreFilterSafeCoordinateBound` was derived for the clamp walk. The repeat-scale walks now reuse the helper unmodified, and nobody re-derived the margin for them.

Site: `Editor/Analysis/TriangleAlphaClassifier.cs`, the comment block above `PreFilterSafeCoordinateBound`.

Decision: no new math. Extend the comment with the reuse fact and why it is safe: the margin argument depends only on the per-coordinate magnitude, never on the caller; and a short margin fails conservative, because a falsely separated box only loses a witness, and a lost witness never proves opacity. Why not re-derive: the stand-down past the bound already caps the harm at performance, and the full suite passed with the reuse in place.

### D4. Restore the dropped word in the clamp gate comment

Finding: the bilinear clamp substitution gate doc lost the word "candidates". It reads "Opaque count too." Three sibling blocks read "Opaque candidates count too."

Site: `Editor/Analysis/TriangleAlphaClassifier.cs`, doc block above `BilinearClampSubstitutesErasedTexels`.

Decision: insert the word. Comment only.

### D5. Drop the racing completion assertion after an abandoned cancel

Finding: `CancellingWhileInFlightDisposesTheSourceWhenTheTaskCompletes` asserts the in-flight task is still running right after `Cancel(timeoutMs: 0)`. Cancel abandons without waiting, so the fixture task can complete before the assertion runs. A correct run can then fail.

Site: `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`, the `IsCompleted` assertion after `Cancel(timeoutMs: 0)`.

Decision: delete the assertion and its failure message. The test's subject is disposal after an abandoned cancel, and the remaining assertions cover it: `IsCancelled` is true, the source reaches the disposed state, and a later `Cancel` tolerates the disposed source. Why delete and not rework: the deleted pin guards a future synchronous-complete `Cancel(0)` that nobody has asked for. Yagni.

## Out of scope

- Any behavior change in production code.
- A fresh rounding-margin derivation for the repeat-scale walks.
- The research package.
- The eight editor-generated `AmuseTests_Temp *.meta` working-tree artifacts. They stay unstaged.

## RED/GREEN note

No task changes behavior. RED does not apply to comment and name edits. Task 5 changes a test by deleting a flaky assertion; its verification is the full test class passing after the edit, and the class still covering its subject.

## Acceptance

1. Unity compiles with no errors or new warnings after each task.
2. `TransientUnlockAvailabilityTests` and `AlphaSeparationDryRunTests` pass, one class per filtered run.
3. A grep proves each of the five sites shows the corrected text.
4. No pinned attestation constant changes. No production behavior test changes.
5. Every touched file passes the identifier sweep before its commit.
