# Transient Unlock Implementation Plan

## Status

Implementation plan. Dated 2026-09-20, revised the same day after an adversarial review whose findings are folded in and recorded under Self-review notes. Design source: `docs/superpowers/specs/2026-09-20-poiyomi-transient-unlock-design.md`. Branch `feat/transient-unlock-window`, renamed from `investigate/poiyomi-locked-materials` on 2026-09-20. Base commit `bdf7930`, plus the same-day documentation changes pending commit: the d4rk Write Properties as Static Values investigation and the spec's consent and V2 amendments. Those documentation changes land as one docs commit before increment 1 starts.

The plan follows the spec's increment order in its section 15. Each increment runs as its own topic branch off `feat/transient-unlock-window` and lands only with its observed RED/GREEN evidence recorded.

## Global Constraints

- Production code lives only under `Packages/com.alrauna.amuse/Editor/`, mainly `Editor/Build/`. One public type per file, file name equals type name, namespaces mirror folders. Types stay `internal` where possible.
- Tests live under `Packages/com.alrauna.amuse/Tests/Editor/`, folders mirroring production, one test class per production type named `<Type>Tests`, NUnit constraint model.
- Vendor shaders and Thry are never installed. All lock behavior in tests runs through the production delegates and schema-only stand-in shaders under `Hidden/Alrauna/AmuseTests/*`. The existing verified seams in `Tests/Editor/Build/` are extended, not replaced.
- RED/GREEN discipline: every behavior change lands with a failing test observed first against a named plausible wrong implementation. An assertion that passes on first run is recorded as characterization, never dressed up as RED. A filtered run that reports 0 tests is a failure. Observed counts are recorded in the increment report.
- Every new closed-enum member carries a census row append, mirroring declaration order.
- Unity asset and `.meta` files move as one unit. No GUID churn. Generated `.csproj` and `.sln` files are never touched.
- No stage, commit, push, or PR without explicit per-increment authorization. Diffs are inspected before any handoff. `git diff --check` runs before each report.
- New documents use short active sentences, no contractions, no semicolons. Documents carry no machine paths, no instance identifiers, no private asset names.

## Increment 1 - Locked identity substrate

Spec anchors: sections 2, 5.1, 5.2, 12.

### Target

`Editor/Build/LockedMaterialIdentity.cs` (new record): recognizes Thry locked identity with both required signals, shader name starts with `Hidden/Locked/` and `_ShaderOptimizerEnabled` equals 1. Reads `OriginalShader`, `OriginalShaderGUID`, and `AllLockedGUIDS` tags. Records generated-shader asset presence. Stale tags alone are never lock evidence.

Selection pre-check in the barrier: a recognized locked material is no longer silently Unknown. With the original shader unresolvable or unattested it refuses with a named member instead of the generic path. A missing original shader keeps the 2026-09-19 orphan destination, `AdmittedMaterialSemanticsUnknown`. The pre-check never overrides an existing refusal path: a material variant presenting locked identity stays on its pre-existing refusal, and the variant question of spec section 17 stays parked unless observation shows a variant reaching the locked route.

Enum additions: both renderer refusal members are declared in this increment with report strings and census rows, but only `LockedPoiyomiOriginalShaderUnattested` is emitted here. `LockedPoiyomiThryUnattested` first fires in increment 2, because the Thry source-digest attestation that decides it does not exist before then. A missing original shader stays on the 2026-09-19 orphan refusal, unchanged.

### Change

1. Add the identity record with a pure classification function taking material serialization inputs, no live Unity mutation.
2. Wire the selection pre-check to emit `LockedPoiyomiOriginalShaderUnattested`.
3. Mandatory mutation surfaces: `Editor/Build/AmuseReportStrings.cs` gains title, description, and hint entries for both declared members, and the string tests iterate every member and fail on a missing entry. The research census files gain the renderer-refusal mirror rows with their snapshot test updates.
4. Stand-in assets: the positive identity case runs through the pure classification function on literal shader-name inputs, so no fixture needs the `Hidden/Locked/` prefix. A stand-in shader declaring `_ShaderOptimizerEnabled` with stale tags extends the existing `Hidden/Alrauna/AmuseTests/*` set for the negative cases. If a live fixture with a real `Hidden/Locked/` shader name proves necessary, it is a deliberate, documented exception to the stand-in naming rule, stated in the increment report. Materials exercise each emitted refusal.
5. Tests: `LockedMaterialIdentityTests`. RED against two named wrong implementations: detection by name prefix only, which falsifier F7 kills with an unlocked material carrying stale tags; detection by tag only, which F7 kills in the other direction.

### Acceptance

Focused EditMode filter for the new class reports nonzero observed counts with the RED runs observed first. Full `Alrauna.Amuse.Tests.Editor` passes. No behavior change beyond the one named refusal emission. Census rows and report strings land for both declared members.

## Increment 2 - The window as behavior-neutral pass-through

Spec anchors: sections 4, 6, 7, 8, 9, 11, 12, 14. Falsifiers F1 to F3 and F7 to F13. This is the infrastructure proof: a locked material round trips to a re-locked equivalent with no transformation applied.

### Target

- `Editor/Build/TransientUnlockAvailability.cs`: Thry attestation by `MonoScript` source digest against the pinned set, reflection seam resolution for `ShaderOptimizer.UnlockMaterials` and `LockMaterials` with expected shapes, cached per domain. All vendor contact behind the production delegates `TransientUnlockDelegate` and `TransientRelockDelegate`, with real implementations wrapping the reflection seam.
- Swap-in service: preconditions per spec section 5, clone with exact name parity, `BuildContext.AssetSaver.SaveAsset`, restore, verification contract, then slot array substitution, closure remap L to U through the existing curve-rewrite machinery, and object registry registration. One U per L across the avatar.
- Window state in `BuildContext`: the open swapped pairs.
- `Editor/Build/TransientUnlockWindowClose.cs`: a fourth pass, ordered last in the PlatformFinish sequence. One batched re-lock, batch verification after the call, per-pair fallback on failure, `TransientUnlockRelockFailed` per affected slot, U clones destroyed only on the fallback path. The fallback inverts the whole swap-in remap, slot arrays and every animation-closure reference, before any U clone is destroyed, because a destroyed U still referenced by a rewritten curve serializes as a missing reference.
- Enum additions: `AlphaSeparationSlotRefusal.TransientUnlockRestoreMismatch` and `AlphaSeparationSlotRefusal.TransientUnlockRelockFailed`, with report-string entries. No census rows: the census mirrors renderer-scoped refusals only, so the slot members have no census surface. Extending the census schema to slot refusals would be a separate reviewed research-package decision.
- Consent gate: the window ships consent-gated per build from the first commit, per spec section 12.

### Change

1. Availability and delegates, with fakes that script restore and re-lock outcomes.
2. Swap-in, close pass, fallback, refusals. Transformation selection stays closed: no material is admitted for analysis in this increment, so the pipeline body is untouched and the round trip is behavior-neutral by construction and by test.
3. Tests, one class per production type. The falsifier set, each as a named case with a no-op guard:
   - F1, a no-op restore is caught by the verification contract and never swaps in.
   - F2, a partial restore produces the named refusal, never a closure failure.
   - F3, no cycle step deletes or mutates L, its generated shader asset, or L's tags; extended to the whole cycle.
   - F7, an unlocked material with stale tags is not admitted.
   - F8, an unattested Thry digest refuses before any clone exists.
   - F9, an unpersisted clone is detected as a no-op restore.
   - F10, the close pass re-locks every open pair; none ships unlocked.
   - F11, a forced re-lock failure inverts the swap-in remap, slot arrays and closure references, back to L and records the named refusal; the test asserts no surviving reference to U. The object-registry L to U entry's disposition on fallback is a recorded V2 observation.
   - F12, name parity: the clone keeps L's exact name, and suffixed clip bindings stay live across the cycle.
   - F13, the fallback object is never destroyed; only references drop. The U clone is the only destroyable side.
4. The behavior-neutral round trip test: swapped pair goes in locked, comes out re-locked, marker equals 1, shader name is a `Hidden/Locked/` form, property table parity per falsifier F12, analyzed materials untouched.

### Acceptance

RED observed for F1, F10, F11, F12, and F13 against named wrong implementations: a verifier that trusts the delegate's return value, a close pass that skips already-locked pairs without verification, a close pass that leaves open pairs unlocked on exception paths, a clone renamed for diagnostics, and a sweep that destroys the fallback. Further named RED targets: F3 against a restore path that restores L in place instead of the clone, F8 against an availability check placed after the clone step, and the consent gate against a window that opens without a per-build grant. F2 and F9 share F1's detection machinery and are recorded with their first-run status. Full both test assemblies pass. Diffs inspected.

## Increment 3 - Transformation inside the window

Spec anchors: sections 7, 10, 12, 14. Falsifiers F4 to F6.

### Target

Admission opens for attested locked materials with attested Thry, still consent-gated. Capture, classification, planning, conversion, and apply run on the unlocked world with no special cases: capture reads U, the Poiyomi frontend computes semantics from the full property table, GPU texture routes run as for any unlocked material, the canonical opaque material derives from U through the existing per-family conversion, apply performs its single mutation sequence.

### Change

1. Selection admits the swapped world; dedupe keeps one U per L across slots and closure, including L referenced only by clips.
2. Curve mapping coexistence: the L to U identity mapping and the existing canonical mappings compose through the curve-rewrite machinery.
3. Sweep rules: AMUSE-owned clones and generated assets are destroyable; the fallback L is never destroyed, only dereferenced.
4. Tests: F4, a mixed closed set degrades per slot, the failing member keeps its original material while sibling slots still optimize. F5, suffixed clip bindings survive swap-in, transformation, and re-lock as live. F6, an unlock failure never widens refusal beyond the owning slot.
5. Regression: the increment 2 round-trip test still passes unchanged.

### Acceptance

RED observed for F4, F5, and F6 against named wrong implementations: renderer-wide refusal on one member's failure, a clone renamed for diagnostics, and slot refusal widened to the renderer. Full both test assemblies pass. Observed counts recorded.

## Increment 4 - Verification tasks and the consent move

Spec anchors: sections 12, 16.

### Target

V1 and V2 evidence recorded, then the consent gate moves per spec section 12, attested combinations run without per-build consent, unattested combinations ask.

### Change

1. V1, repository test: the attested canonical opaque target shader declares no `ThryShaderOptimizerLockButton` attribute. If it does, record and consent instead of inheriting silently.
2. V2, lab characterization, upload path and play-mode path: swap-in, restore, re-lock, and fallback inside a live NDMF build, the `AssetDatabase.Refresh` behavior of Thry's batch inside a build, the AssetSaver container interaction, and the on-disk completeness of the re-locked shader files before the SDK callback chain runs, including the d4rkAvatarOptimizer consumption observation. Results land as a dated investigation note.
3. V4, lab observation: the re-locked material's declared property table is a superset of the original locked table, including the suffixed-name case. Results join the same note.
4. Consent wording settled against the V2 and V4 results, closing the open question of spec section 17. The subject-content checklist covers the spec section 9 and section 12 residues plus the roundtrip note section 6 content: the observed round-trip losses and the reproduced order-100 failure trigger, a descriptor with uninitialized animation layers. The roundtrip probe 3 upload-lock-precondition falsifier is dispositioned into the V2 upload-path observation rather than a new falsifier number. The gate move lands only with both observations recorded.

### Acceptance

V1 test green. V2 and V4 notes dated and identifier-swept. The consent move is a separate commit with its own diff inspection.

## Increment 5 - Cache seam integration

Spec anchors: sections 13, 16 V3. Parked until the cache service from `2026-09-19-amuse-build-caching.md` exists. Do not start this increment before that dependency lands.

### Target

The cacheable unit is the derived analysis keyed by the locked serialization digest including tags, the Thry digest, the original shader digest, and the policy version. On a hit the window still opens and the round trip is the only per-build cost. On any doubt the entry is a miss.

### Change

1. Key construction and storage through the cache service's existing soundness contract.
2. V3 test, the false-hit guard: era-2 unlock residue changes the content address, so a stale entry can never hit. RED against a named wrong implementation that keys on material name or GUID alone.

### Acceptance

V3 red then green with observed counts. Full both test assemblies pass.

## Gates and stop conditions

Each increment ends with: diffs inspected staged and unstaged separately, `git diff --check` clean, focused filter counts observed, full assemblies green, and the increment report written with observed numbers.

Stop and return evidence, options, and a recommendation, without expanding production scope, when any of these appears:

- Live behavior contradicts a spec section 3 mechanism fact.
- The unlock window leaks on a path the falsifiers do not cover.
- The re-lock parity claim of section 7 fails observation.
- The refresh hazard of V2 materializes as clipped or unimported shader files at the SDK boundary.
- Thry attestation hits an unpin-able form, such as an assembly-embedded tool source.
- A broader abstraction or a new subsystem claim shows up, per the repository working discipline.

## Git authorization boundary

No staging, committing, pushing, PR creation, or branch deletion without explicit authorization in the implementing session. The pending same-day documentation changes land first as one docs commit, on explicit authorization. Each increment lands as its own topic branch off `feat/transient-unlock-window` and merges forward only after its evidence is recorded. The consent-gate move is its own authorized commit.

## Self-review notes

- The plan's increment order is the spec's section 15 order, unchanged. Increment 2 stays transformation-free on purpose: the infrastructure proof must not depend on analysis correctness.
- Falsifier numbers match the spec's section 14 assignments. No new falsifier numbers are introduced; new cases found during implementation extend the spec first.
- Naming proposed here, `LockedMaterialIdentity`, `TransientUnlockAvailability`, `TransientUnlockWindowClose`, and the delegates, follows the spec's section 14 delegate vocabulary. An implementing session may adjust names, but the delegate seam and the one-type-per-file rule are fixed.
- Increment 5 is parked per the spec's own section 15 sequencing, which is the authority. The caching investigation itself prefers the seam to land with or before the unlock path, so this parking means the first release ships with unlock revalidation uncached, an accepted deviation recorded here. Starting increment 5 before the cache service exists would create the dependency inversion the caching note rejects.
- Adversarial review, 2026-09-20: a reviewer subagent verified the plan against the spec, the five investigations, the architecture vision, and the production code seams, with verdict satisfies-with-gaps. All nine findings are folded into this revision: the fallback inverse-remap requirement, the increment 1 emission scope, the census surface correction, the report-string mutation surface, the named RED targets, the consent subject content, the stand-in naming resolution, the variant refusal sentence, and the caching citation.
