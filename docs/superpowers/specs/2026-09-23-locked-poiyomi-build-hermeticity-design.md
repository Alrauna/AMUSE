# Hermetic Locked Poiyomi Builds

Privacy note: This record contains no private avatar, scene, material, renderer, or asset identifiers. It describes product code, synthetic tests, and Census Lab tooling by role.

Date: 2026-09-23.

Design status on 2026-09-23: The direction was approved in chat after the Lab investigation. This spec records the design. It does not report an implementation result. It supersedes two choices of `docs/superpowers/specs/2026-09-20-poiyomi-transient-unlock-design.md` for the swapped source pair, named in section 2.

Result status on 2026-09-23: Implemented on the working branch the same day. The plan record and the investigation's implementation result carry the observed counts.

Superseded on 2026-09-24: the vendor lock handover design removed the play path relock entirely. Locking of Poiyomi materials, including AMUSE-generated ones, is the lock tool's job in the SDK preprocess stage that runs after NDMF, on upload and on play entry alike. Every behavior item above that names a play path relock, a relock batch, or a vendor readiness answer describes the earlier design for history. The plan is `docs/superpowers/plans/2026-09-24-vendor-lock-handover-plan.md`.

## Goal

An AMUSE build that admits locked Poiyomi materials must be hermetic. It invokes no vendor lock or unlock for the swapped source pairs, writes nothing outside the NDMF generated container, triggers no asset database refresh between evidence capture and apply, and leaves the on-disk lock state unchanged so the next build of the same input starts from the same state.

The Census Lab must be able to prove corpus stability per run. A probe run that modifies corpus files fails a named preflight check.

## Evidence

The investigation `docs/superpowers/investigations/2026-09-23-locked-poiyomi-build-hermeticity.md` records, dated 2026-09-23:

- The vendor restore and relock calls through the reflection seam write serialized shader files and refresh the asset database inside AMUSE passes.
- Every relock mints a new locked shader identity, so the next build's on-disk input differs.
- A generated locked shader folder exists inside a private vendor source area, and a private source material changed on disk during builds. The write path attribution is open.
- The lock format is characterized live at the pinned era by `docs/superpowers/investigations/2026-09-20-thry-lock-unlock-roundtrip.md`: values survive in the material serialization, the original shader identity survives in tags, keywords survive in a tag when property-consistent, and rename-suffixed properties move their values to suffixed names.

## Terms

- `L` is the original locked material, untouched, as today.
- `U` is the temporary unlocked clone, persisted through the NDMF asset saver, as today.
- `C` is the AMUSE-generated canonical opaque material, as today.
- Reconstruction is the new AMUSE-owned restore that produces `U` from `L` in memory.
- Reference reversion is the new primary close outcome: slot arrays and recorded closure references return from `U` to `L`.
- The preflight is the Lab-side corpus reset and drift check.

## Scope

- Change only the locked Poiyomi family whose original shader passes the existing pins.
- Keep the window shape of the 2026-09-20 design: gate, selection, swap-in, unlocked world, close pass, fallback discipline, object registry provenance.
- Keep the 2026-09-23 generated-output locking: `C` locks on the play path inside the close pass through the vendor seam, and the normal preprocess path stays responsible for `C` outside play mode.
- Remove the vendor unlock for `U` and the vendor relock for `U`. This supersedes the restore step of section 6 and the relock outcome of section 8 in the 2026-09-20 design for the source pair.
- Do not change shader attestation pins, the Poiyomi semantic frontends, the classifier, apply's mutation sequence, lilToon behavior, or package dependencies.
- Do not add a new shader conversion family, a public API, or a new subsystem.

## Behavior

### Reconstruction

1. Admission keeps the existing locked identity contract: the `Hidden/Locked/` name prefix and the optimizer float of exactly one, plus the original shader tags resolving to a shader that passes the existing pins.
2. `U` is a clone of `L` as today, name-identical, persisted through the asset saver.
3. Reconstruction sets the clone's shader to the resolved original shader asset, sets the optimizer float to zero, restores the keyword set from the recorded keywords tag, and moves every rename-suffixed serialized value back to the plain property name the original shader declares. The suffix follows the characterized rule of the pinned era.
4. Reconstruction runs no vendor call, writes no file, and triggers no refresh.
5. The existing restore verification contract applies unchanged after reconstruction: shader name and GUID equal the recorded tags, the optimizer float is zero, the original shader asset passes its pins, and the generated locked shader asset of `L` still exists untouched.

### Gating

6. The non-play build paths no longer require the vendor tool for the window. A locked material whose original shader attests admits without any vendor presence.
7. The play path still requires an attested vendor tool, because `C` must lock there. Without it, the play path refuses the transformation fail-closed before any clone exists.
8. The named refusal for an unattested vendor tool narrows to the play path. Its enum member and census row stay. Its report text names the play-path reason.

### Close

9. The primary close outcome for each open pair is reference reversion: every slot that holds `U` takes `L` back, every recorded closure reference written at swap-in inverts from `U` to `L`, and only then does the destroy step remove `U`. The reversion uses the existing fallback machinery, which becomes the primary path.
10. The shipped state for unconverted slots is the user's own `L`, which is locked, render-equivalent to a relocked clone by the round-trip verification, and carries the author's own animatable surface.
11. On the play path, the close pass additionally submits each live `C` to the existing vendor relock batch and verifies it, as on 2026-09-23. A failed `C` lock keeps the existing per-pair fallback and refusal names.
12. A build that completes ships no unlocked AMUSE-owned material on any path. This invariant is unchanged.

### Preflight

13. The Lab preflight keeps a pristine baseline snapshot and a digest manifest of the corpus folders before a probe run, restores every drifted or unexpected file, and reports what it restored.
14. After a probe run, the preflight re-digests the corpus. Any drift fails the run with a named error and a file list. The report is role-based and contains no private identifiers.

## Tests

Use the existing synthetic locked fixtures and the vendor stand-in. No vendor shader is installed for repository tests.

- Reconstruction tests drive literal synthetic locked materials that carry the era's tag set, suffixed properties, and serialized values. They must prove: typed values become reachable after the shader swap, suffixed values move to plain names, the keyword tag restores the keyword set, the optimizer float clears, and the verification contract accepts a correct reconstruction and refuses a partial one.
- Gating tests must prove a non-play build admits a locked material with the vendor seam absent, and a play build refuses fail-closed with the seam absent.
- Close tests must prove the primary reversion returns `L` to slots and committed curves, destroys `U` only after inversion, and performs no vendor relock call for `U` on any path. The play-path `C` lock tests of 2026-09-23 keep passing.
- Existing tests that assert the vendor relock of `U` pin the superseded contract. Update them to the reversion contract in the same change. Do not delete their fallback coverage.

## Acceptance

- Observe the reconstruction tests fail before reconstruction exists, against a plausible wrong implementation that reads the shader property table instead of the serialization.
- Observe the gating test fail before the gate narrows, because the non-play path refuses without a vendor.
- Observe the close test fail before the close changes, because the close relocks `U`.
- Run the focused transient-unlock and reconstruction EditMode tests, then the full product and research EditMode assemblies, in the dev editor instance with nonzero counts and no compile errors.
- Verify the editor's `Application.dataPath` equals `<repo-root>/Assets` before each run. Never use the Census Lab editor instance for product tests.
- In the Census Lab, run the preflight twice around a probe run. Observe the first run restore the known source-area pollution and the second run report zero drift. Record observed counts.
- Do not claim an uploaded bundle was tested. No upload is part of this work.

## Stop conditions

Stop and return evidence if the reconstruction cannot reproduce a characterized locked fixture within the verification contract, if the reversion close cannot keep the committed-curve inversion complete, or if the play-path `C` lock regresses. Stop before adding a second vendor era, changing the semantic frontends, or moving the preflight into the product package. Do not run product tests in the Census Lab.
