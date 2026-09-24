# Locking AMUSE-Generated Poiyomi Materials in Play Mode

Privacy note: This record contains no private avatar, scene, material, renderer, or asset identifiers. It describes product code and synthetic tests.

Date: 2026-09-23.

Design status on 2026-09-23: The behavior choice was approved in chat. This spec records the design. It does not report an implementation result.

## Goal

AMUSE must lock its generated Poiyomi opaque material during a Play mode build. The close pass must do this before the build finishes.

A material counts as locked only when its shader name starts with `Hidden/Locked/` and `_ShaderOptimizerEnabled` equals `1`.

## Evidence

The 2026-09-23 investigation reproduced the unlocked generated output in the Census Lab Play mode scene. AMUSE created and assigned the canonical output. The close pass relocked the temporary source clone, but it did not relock the generated output.

A local SDK preprocess run on an in-memory clone ended with the generated output locked. No upload or network call ran. This result does not prove the contents of an uploaded bundle.

The evidence and limits are recorded in `docs/superpowers/investigations/2026-09-23-poiyomi-generated-material-locking.md`.

## Terms

- `L` is the original locked material.
- `U` is the temporary unlocked clone that AMUSE uses during the build.
- `C` is the AMUSE-generated canonical opaque material derived from `U`.
- A transient pair is the `L` and `U` pair held by the existing unlock window.

## Scope

- Apply this change only when the build path is `AmuseBuildPath.ApplyOnPlay`.
- Relock only generated canonical outputs whose admitted source family is `CapturedAlphaMaterialFamily.Poiyomi`.
- Do not add conversion support for `PoiyomiTwoPass` or change lilToon behavior.
- Keep the normal SDK preprocess path unchanged. It already locked the generated output in the observed local preprocess run.
- Do not change `L`, source assets, shader assets, the upload callback, package dependencies, or Census Lab assets.

## Behavior

1. Keep each admitted source material's family beside its prepared source-to-output mapping.
2. During Play mode close, submit each open pair's U and its live Poiyomi C outputs in one relock batch. Submit each material once. Apply can sweep C before close when no candidate slot survives. Do not submit a swept output.
3. Verify every submitted material after the batch. Require both the locked shader name and the lock flag.
4. If all materials for a pair pass verification, keep the recorded canonical output in renderer slots and committed animation curves.
5. If U or any live C fails verification, restore every reference to that pair's U and C to L. Update renderer material arrays and committed object-reference curves. Record `TransientUnlockRelockFailed` for each affected slot.
6. Destroy AMUSE-owned U and C clones only after the committed animation graph was enumerated and the references were inverted. If the graph cannot be enumerated, retain the clones and record `TransientUnlockCloneRetained`.
7. On non-Play build paths, leave the close-pass relock batch unchanged. The SDK preprocess step remains responsible for the generated output on that path.

Superseded on 2026-09-23: the hermeticity design (`docs/superpowers/specs/2026-09-23-locked-poiyomi-build-hermeticity-design.md`) removed the unlocked clone from the relock batch on every path. The close pass submits only live generated canonical outputs keyed by the pair's clone, and the source pair reverts to the locked original by reference reversion. Behavior item 2 above still describes the earlier batch shape for history.

Superseded again on 2026-09-24: the vendor lock handover removed the play path relock entirely, so the close pass locks nothing on any path. Behavior item 6 above is history. The lock tool's own SDK preprocess stage, which runs after NDMF, now locks every Poiyomi material on the avatar, including AMUSE-generated ones, on upload and on play entry alike. The plan is `docs/superpowers/plans/2026-09-24-vendor-lock-handover-plan.md`.

## Tests

Use the existing transient-unlock transformation fixture and test-only Thry stand-in. Do not install vendor shaders or run these tests in the Census Lab.

The success case must show that a Play mode close leaves `C` assigned, with a `Hidden/Locked/` shader and a lock flag of `1`. The non-Play case must show that the close pass does not relock `C` early.

The failure case must leave `L` in renderer slots and committed object-reference curves when the test lock delegate locks `U` but not `C`. It must record `TransientUnlockRelockFailed`.

For a split failure, the fallback must restore L in the appended renderer slot and its committed curve.

## Acceptance

- Observe the Play mode success test fail before production changes because the close pass relocks only U.
- Observe the success test pass after the close pass includes and verifies Poiyomi C outputs.
- Observe the failure test fail before fallback changes because a committed curve still names C after fallback.
- Observe the failure test pass after fallback restores U and C references to L.
- Observe the appended split-curve test fail before fallback changes and pass after it restores L in the appended renderer slot and committed curve.
- Observe the swept-output test fail before the close skips destroyed outputs. Observe it pass after the fix without a relock refusal.
- Run the focused transient-unlock EditMode tests and the product and research EditMode assemblies in the dev editor instance. Require nonzero test counts and no compile errors.
- Verify the editor's `Application.dataPath` equals `<repo-root>/Assets` before each test run. Never use the Census Lab editor instance.
- Do not claim an uploaded bundle was tested. No upload is part of this work.

## Stop conditions

Stop and report evidence if the dev editor instance does not point to the public project, if a RED failure has a different cause, or if the change requires lilToon behavior or a new subsystem. Do not broaden this spec without another approved design.
