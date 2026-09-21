# Thry pin recording and the live unlock window - V2 and V4

## Privacy note

This record opens with a sanitization statement. It characterizes private
Census Lab fixtures by role only. It names no avatar, renderer, material,
texture, mesh, hierarchy, scene, or folder identifier. It replaces exact
triangle counts with ranges. It records no machine paths, no host names, no
ports, and no instance identifiers or hashes. Every status claim below is
dated. The two pinned digest values live in the repository commit named in
section 2, not in this note.

## 1. Scope and method

Date: 2026-09-21. This session recorded the two pinned Thry tool digests
(repository commit), verified the availability machinery against the era 1
embedded tooling, and then produced the first live in-build window
observations. The upload path observation and the play mode observation
close verification tasks V2 and V4 of the 2026-09-20 transient unlock
design. The consent gate move stays out of scope and lands only after this
evidence is reviewed.

Method. Two editor instances were reachable through the development bridge:
the dev editor instance for this repository, and the Census Lab editor
instance. Each interaction named its instance, and each session on an
instance checked the project data path identity first. The Census Lab
project embeds the two AMUSE packages as links into this repository working
tree, so the committed state became the Lab state after a refresh. Test
suites never ran in the Lab. All Lab probes were read-only except the
fixture preparation in section 4 and the two build runs.

## 2. The digest recording

Date: 2026-09-21, repository commit `c3c6b3e` on
`feat/transient-unlock-window`, base `c58170b`. The commit fills
`TransientUnlockAvailability.PinnedSourceDigests` with two SHA-256 values.

Both values come from freshly fetched vendor archives. The VPM listing
published archive digests that matched the pins in the 2026-09-19
characterization for both packages, and the downloaded archives reproduced
those archive digests byte for byte. The session extracted
`Editor/ShaderOptimizer.cs` from each verified archive and hashed the file
bytes. The first pin is the embedded tool source in the toon 9.3.64
archive, the second is the standalone tool source in the thryeditor 2.74.2
archive.

The test that pinned the empty table was rewritten to pin the new truthful
state. It now asserts the exact two literals, in era order, and keeps a
fail-closed assertion for a machine with zero locator hits. This
repository installs no vendor shader, so the repository answer stays fail
closed. An old-table RED run has no repository-environment behavior to
observe, because a machine with zero locator hits answers false with an
empty table and with a filled table alike.

Gate run, dev editor instance, full EditMode population of both test
assemblies: 2311 of 2311 completed, with exactly the 5 proven environment
failures known from the base tree (4 in AaoMergedConsumptionTests, 1 in
AlphaSeparationPreparationTests). The updated pin test passed inside that
population.

## 3. The locator and the seam, verified live

Date: 2026-09-21. The exactly-one-hit MonoScript rule was probed in the
Census Lab project, where the only lock tooling is the editor embedded in
the shader package. The locator search found one name match. The match is
the package embedded script under the shader package's editor tree. The
file digest matched the first recorded pin exactly.

The reflection seam resolved the vendor type by full name in the Lab, and
both entry points resolved with the expected shape: static, boolean
return, a first parameter assignable from a material enumerable, and every
later parameter optional. No locator fix was needed. `AssetDatabase`
searches cover package embedded scripts.

After the Lab refreshed the linked packages, the production functions
answered on the Lab machine: the source attested, and the seam resolved.

## 4. Fixture restoration

Date: 2026-09-21, before the build runs. The designated fixture material
was not in its prepared state at session start. The material was unlocked,
with stale lock residue tags and the lock flag at 0, and its era 1
generated shader folder beside it was empty. The preparation session had
locked it, and something later removed the generated asset and unlocked
the asset again. The restored material answered the unlocked original
shader name, and the lock residue tags matched the known unlock residue.

The session restored the prepared state by locking the fixture material
through the embedded era 1 tool. The lock succeeded. The cold lock took
about 10 seconds including generated shader import. The fixture then
showed the locked shader form with the material identity tail, the lock
flag at 1, and the reference tag present.

Finding for the fixture owner: the prepared locked state did not survive
to this session. The unlock left the stale tags the characterization
predicts, so a locked signature that requires both the name prefix and the
flag classifies the regressed fixture correctly. Whether the embedded era
1 tooling contains an auto recovery path that unlocks materials whose
generated asset was deleted is still open. Fixture holders should re-check
the lock flag before each session.

## 5. V2, the upload path, observed live

Date: 2026-09-21. The avatar that holds the fixture material is a test rig
with one dense garment renderer of several hundred thousand triangles,
plus a full avatar descriptor with animation controllers. The session
drove the local SDK preprocess callback chain only. No upload, no network.

Two harness lessons came out of the first attempts, and both are relevant
to anyone who repeats this observation.

1. The chain must receive a plain clone, not a scene prefab instance.
   NDMF's build context refuses any avatar that contains prefab parts, and
   the SDK then reports the refused hook. A runtime instantiated copy with
   all prefab connections unpacked passes.
2. VRCFury marks an avatar it processes outside an upload as a test copy,
   and its once-only guard then blocks later chain runs on that object.
   The marker component is removable, and the guard is VRCFury's, not
   AMUSE's.

The accepted run used a plain, fully unpacked clone of the avatar with the
locked fixture in its slot. The consent dialog appeared on the Census Lab
editor as a modal titled AMUSE: unverified versions. Its body carried the
committed consent subject text, verified word for word against the source
string, under the standard lead line and closing question. The session
granted consent through the dialog's proceed control, and the window
executed.

Observed outcome of the granted run:

- The SDK callback chain finished successfully.
- The shipped slot holds the re-locked clone. Name parity holds. The lock
  flag is 1. The clone persists inside NDMF's generated asset container,
  as the swap-in design requires.
- The re-lock produced a fresh content addressed locked shader name with
  the unlocked cycle residue baked in, distinct from the original lock's
  name, exactly as the cache keys in the 2026-09-20 roundtrip note demand.
- A leak scan over every renderer on the processed avatar found exactly
  one locked form material, with the flag at 1. Zero swapped pairs shipped
  unlocked.
- The fallback object was untouched. The fixture material file and its
  original generated shader file hashed identically before and after the
  run.

Pass timings from the NDMF log, one material: structural check 22 ms,
bindings capture 125 ms, semantic barrier 217062 ms, apply 8 ms, window
close 3648 ms. The barrier number includes the minutes the run spent
blocked on the interactive consent dialog, so it does not measure
computation. The window close number is clean. It covers one batched
vendor re-lock, batch verification, and the shipped slot re-assertion, and
it sits in the same band as the standalone era 1 re-lock cost of 1215 ms
recorded in the roundtrip note. A per-material restore cost was not
isolated inside the barrier without instrumentation.

## 6. V2, the play mode path, observed live

Date: 2026-09-21. The session entered play mode on the Census Lab editor
with the fixture avatar in the scene. NDMF ran its play mode build. No
VRCFury play mode skip fired, so the optimization phases ran.

Observed outcome:

- The full AMUSE pass sequence ran. Structural check 19 ms, bindings
  capture 99 ms, semantic barrier 1636 ms, apply 0 ms, window close
  800 ms.
- The play avatar shipped the re-locked clone with the flag at 1. The
  re-lock reproduced the same content addressed locked shader name as the
  upload path run, because both runs restored the same material state
  before locking. Era 1 re-locking is deterministic per input state.
- The analysis refusal on the fixture renderer stayed conservative. The
  window closes per pair regardless, and zero unlocked forms shipped.

Consent divergence, recorded for the design owner. No consent dialog
appeared on the play mode path, and the build proceeded to grant. The
dialog logic is identical code on both paths, so the mechanism is not
verified from outside. The most plausible reading is that the native
dialog cannot present during the play mode transition and the presenter
answered affirmatively. The consent gate move for the upload path must
not assume the play path engages the same way. This finding belongs to
the consent move session, which is still out of scope.

## 7. The two V2 hazards

Refresh hazard. The era 1 embedded tool writes generated shader files and
refreshes the asset database inside its lock. In the granted upload run
the re-locked material's generated shader asset landed inside NDMF's
generated asset container. After the close pass the file existed on disk,
the asset database resolved it, and shader lookups by name succeeded
before the SDK callback chain finished. The clipped or unimported file
outcome did not materialize. Date of observation: 2026-09-21.

Post NDMF readers. The Lab installs several post NDMF tools. On the play
mode run the chain invoked the preprocessors again on the avatar's
emulator clone. d4rkAvatarOptimizer's hook ran after NDMF, found no
optimizer component on the avatar, and skipped it. The benign skip
direction is therefore observed, not assumed. A d4rk run with an active
optimizer component on a re-locked avatar is still unobserved. VRCFury
and the emulator consumed the re-locked avatar without errors on the same
run.

## 8. V4, re-lock parity

Date: 2026-09-21, measured on the upload path run. The declared property
table of the re-locked clone's shader contains every property of the
original locked table and added none. Both tables declared the same
property count. The plumbing properties, the main texture, the color, and
the cutoff are all present after the cycle.

The fixture carries no rename animated property, so the suffixed name sub
case of V4 has no live observation. The suffixed binding survival is
covered in the repository by falsifier F5 and the name parity falsifier
F12, which run the real logic on stand in shaders. A live fixture with a
tag 2 rename animated property remains open for a later Lab session.

## 9. Fallback observation

The forced re-lock failure was not exercised live. The two cheap levers
are intrusive: they require deleting or corrupting vendor state mid build,
which risks the cache hazard the stop conditions name. The fallback logic
is covered in the repository by falsifier F11 and by the probe verified
directions in the task 2 and task 3 reports. Live fallback observation
stays open for a session that can inject a vendor failure cleanly.

## 10. Relation to the spec

Spec section 3 mechanism facts all held in build on 2026-09-21: the
window opened after consent, restore executed through the embedded era 1
tool, the re-lock executed, the closed pair shipped the re-locked clone,
the fallback object was never touched, and no swapped pair shipped
unlocked on either path. No section 3 fact was contradicted.

Divergences and notes against the older records:

- The re-lock name form observed here is the era 1 name with a local file
  identity tail, not the bare material identity tail that the 2026-09-20
  roundtrip note shows for a first era 1 lock. The difference is consistent
  with locking a restored clone that persists in a generated container.
  It changes no conclusion.
- The consent engagement differs between the upload path, where the modal
  appeared and blocked, and the play mode path, where none appeared. See
  section 6. This feeds the consent move session.
- The prepared locked fixture regressed before this session. See
  section 4.

## 11. Next steps

1. The consent move landed the same day, see section 12.1. The play
   path engagement question from section 6 stays open for follow-up,
   because the moved gate no longer asks on any path.
2. Fixture owner: re-check the fixture lock flag before each Lab session,
   and decide whether the generated asset deletion that regressed the
   fixture needs a guard.
3. A later Lab session: V4 with a rename animated fixture, and a live
   forced fallback if a clean injection point appears.
4. Design owner: the d4rk active-component observation on a re-locked
   avatar, listed in section 7, stays open.
5. Coverage: exact alpha semantics for premultiplied Poiyomi materials,
   per section 12.2.

## 12. Same-day addendum: the consent move and the all-unknown
## diagnosis

Privacy note: same sanitization as section 1. Date: 2026-09-21, later
the same day.

### 12.1 The consent move landed

The owner authorized the consent move after reviewing the V2 and V4
evidence. Repository commit `e6c73a8` on
`feat/transient-unlock-window` removes the window's per-build consent
subject. An eligible build on a machine where the vendor side attests
now opens the window without asking. An unready vendor side never
grants, and the renderer pre-check still refuses by name before any
clone exists, so the fail-closed direction of spec section 5 is
unchanged. The D8 subjects for host versions and shader sources are
untouched and still ask through the same consolidated dialog.

RED evidence: the test that pinned the old ask contract failed against
the new production code with exactly the expected message, while the
window's own run assertions inside the same test stayed green, which
proves the silent grant. GREEN evidence: the full both-assembly EditMode
population ran 2311 of 2311 with exactly the 5 proven environment
failures.

### 12.2 Why the fixture renderer reports all-unknown

The owner reported the renderer report
`amuse.renderer.AdmittedMaterialSemanticsUnknown` on the fixture. A
read-only probe diagnosed it the same day.

Method. The probe cloned the fixture material in memory, persisted the
clone as a temporary asset, unlocked the clone through the embedded era
1 tool, drove the production Poiyomi frontend entry on the unlocked
clone, and read the semantic result and its diagnostics. The temporary
asset was deleted after the probe.

Findings, in order:

1. The vendor restore returns success only for a persisted clone. An
   in-memory clone keeps its locked form after a successful return.
   This matches the spec section 6 persistence requirement and
   falsifier F9, and the window's own verification catches it.
2. On a persisted clone, restore works: the clone binds the original
   shader and the lock flag returns to 0.
3. The production Poiyomi frontend admits the unlocked material. The
   material is supported.
4. The alpha output is incomplete with one named diagnostic:
   the unsupported feature `_AlphaPremultiply`. Other outputs carry
   unsupported feature diagnostics for `_DetailEnabled`.

Conclusion. The garment enables Poiyomi's premultiply feature. The
frontend does not yet support alpha semantics for premultiplied
materials, so every triangle answers unknown, the renderer keeps its
original materials, and the report fires. This is the designed
conservative outcome for an unsupported feature, not a regression from
the unlock window. The report is filed at information severity; the
console prefix that calls every report an error belongs to NDMF, not
to AMUSE.

Coverage next step: exact alpha semantics for premultiplied Poiyomi
materials is a real product extension with its own design and RED and
GREEN obligations. Until it lands, this garment keeps its original
materials by contract.
