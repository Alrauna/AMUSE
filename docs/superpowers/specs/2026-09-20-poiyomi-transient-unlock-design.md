# Transient unlock for locked Poiyomi materials - design

## Status

Design spec. Dated 2026-09-20. Branch `feat/transient-unlock-window`, renamed from `investigate/poiyomi-locked-materials` on 2026-09-20 when the branch turned from research to implementation. This spec makes no production change. Controller direction, recorded 2026-09-20 after design review: transient unlock is infrastructure for the long term. The project's future transformations rely on Thry's locking as the re-protection mechanism, within its documented limits.

Evidence base. Every mechanism fact below is live-verified in `2026-09-20-thry-lock-unlock-roundtrip.md` and grounded in `2026-09-19-poiyomi-locked-materials-characterization.md`. No load-bearing claim rests on source reading alone.

Framing rule. The Thry lock is a reversible source transform with no encryption. This spec documents the mechanism as observed behavior. It assigns no intent and takes no position beyond that behavior.

## 1. Goal

Locked Poiyomi materials, whose original shader is the attested Poiyomi Toon 9.3.64, enter AMUSE's pipeline as ordinary unlocked materials. AMUSE opens an unlock window at PlatformFinish, swaps every admitted locked material for its restored unlocked form, runs all transformations inside the window, and closes the window by re-locking every swapped material through Thry. If the window cannot close cleanly, AMUSE swaps the untouched locked originals back in and the shipped state equals the 2026-09-19 behavior.

The window is the deliverable that outlives this feature. Today it carries alpha separation. Future transformations from the architecture vision register inside it and inherit unlocked materials, the re-lock boundary, and the fallback discipline without new lock handling.

## 2. Non-goals

- Non-Thry locks, and locked materials whose original shader is not the attested 9.3.64 pin. Those stay on the 2026-09-19 sentinel path.
- Dependency on the upload-time lock callback. AMUSE re-locks within its own pass; the order-100 callback is not load-bearing for materials AMUSE swapped. Section 9 states this precisely.
- The full floor diagnostics polish. The locked identity record and its refusal names are in scope, because admission needs them.
- Thry Editor UI behavior, and the Thry shader cache's own garbage collection.
- Modifying the user's original locked material or its generated shader asset, in any step, ever.

## 3. Verified mechanism summary

One sentence per load-bearing fact, each from the round-trip note.

- `Thry.ThryEditor.ShaderOptimizer.LockMaterials` and `UnlockMaterials` are public static, take `IEnumerable<Material>`, and exist at both tooling pins.
- Restore executes live at both pins and cross-era, and rebinds the clone to the original shader by the pinned GUID.
- Re-lock after restore executes live at both pins; the cycle is loss-neutral relative to the user's own lock, because the keyword and tiling losses happened at the user's original lock and re-lock adds none.
- The generated locked shader declares a reduced property table; the full values survive in the serialization and become reachable through typed getters after restore.
- Property-consistent keywords survive; unlock leaves stale `OriginalShader*` tags; the lock-state float returns to 0 on unlock.
- Thry's upload callback locks correctly on a well-formed avatar and aborts on a descriptor with null `baseAnimationLayers`. Under this design its behavior is irrelevant to materials AMUSE swapped, because they are already locked when it runs.

## 4. Core shape: the unlock window

Definitions.

- Unlock window: the span of an AMUSE build between the swap-in of the first unlocked form and the window close of the last. Opened inside the barrier pass after the lifecycle gate and consent. Closed by a dedicated final pass, so the sequence reads: structural check, bindings capture, barrier, apply, window close.
- Swapped pair: one locked material L on the effective build avatar and its unlocked form U. U is a clone of L, persisted through NDMF's AssetSaver, restored through Thry, and verified. L is kept untouched as the fallback. One U exists per L across the whole avatar.
- Fallback: the untouched L. On any window-close failure the slot references revert from U to L, which reproduces the 2026-09-19 shipped state exactly.

Sequence inside one build.

1. Gate, consent, selection as on 2026-09-19, plus locked-identity recognition.
2. For each closed-set member that is locked: preconditions, clone, persist, restore, verify, then swap in. The slot material array and every animation-closure reference to L remap to U. The mapping registers with NDMF's object registry.
3. The existing pipeline runs unchanged on the unlocked world: capture, classification, planning, conversion, apply. Nothing downstream knows a lock existed.
4. Window close, always exactly one of two outcomes per swapped pair: re-lock U through the reflected batch, or revert the slot references to L and record `TransientUnlockRelockFailed`. A third outcome, shipping U unlocked, is prohibited and has its own falsifier.

Why this shape.

1. It is the infrastructure the owner specified. Future transformations see ordinary unlocked materials with full property tables, and one re-lock boundary re-protects everything at once.
2. The active re-lock removes the dependency on the upload-time callback. The probes reproduced that callback's failure mode on a malformed descriptor. Under this design the callback does not act on materials AMUSE swapped, because they are already locked when it executes and its filter skips them.
3. Failure always degrades to the 2026-09-19 behavior through a reference swap to the untouched fallback, which is always available and never destroyed.
4. The vendor contract at ship is restored by the re-lock. The shipped locked material is a re-transform of the user's own lock state, render-equivalent per the round-trip verification, and the same transform Thry's own upload path applies to any unlocked material.

## 5. Preconditions, fail closed

Every precondition failure refuses that material before any swap. Never a partial admission.

1. Locked identity: shader name starts with `Hidden/Locked/` and `_ShaderOptimizerEnabled` equals 1. Both signals. Stale tags alone are not lock evidence.
2. Original shader attestation: `OriginalShaderGUID` resolves through the AssetDatabase to a shader that passes the existing 9.3.64 name, GUID, package, and digest pins. Missing original means the orphan case, refused as on 2026-09-19.
3. Thry attestation: the installed `ShaderOptimizer` source digest is in the pinned set of section 11.
4. Reflection seam resolution with the expected method shape.
5. AssetSaver availability, already gated by `HostLifecycleCapability`.
6. Fallback integrity: L is a valid material object and is never mutated by any step. Since L may be the user's own project asset when no upstream tool cloned it, the fallback is never destroyed, only dereferenced. The clone U is AMUSE-owned container content and is the only destroyable side.

## 6. Clone, persistence, restore, verification

- Clone. `Object.Instantiate` of L. The clone keeps L's exact material name. Name parity is load-bearing: the rename-animated property suffix derives from the material name, and post-lock animation clips bind suffixed names that must resolve again after the re-lock. Falsifier 12 pins this.
- Persistence. `BuildContext.AssetSaver.SaveAsset(U)`. Required: Thry's lock filter skips materials without an asset path, so an in-memory clone would make `UnlockMaterials` run without effect. The AssetSaver is the repository's sanctioned persistence form, and the container is deleted by NDMF after the build.
- Restore. `UnlockMaterials` on a list containing only U, through the reflection seam, with vendor exceptions converted to named refusals.
- Verification contract, all required before swap-in: clone shader name equals the `OriginalShader` tag value; clone shader GUID equals the resolved `OriginalShaderGUID`; `_ShaderOptimizerEnabled` equals 0; the original shader asset still exists and passes its pins; the generated locked shader asset still exists untouched, because L still needs it if the fallback fires. Any mismatch is `TransientUnlockRestoreMismatch`.
- Reference safety. The clone carries the `AllLockedGUIDS` tag listing L, so Thry's reference accounting keeps the generated asset alive. Falsifier 3 pins the rule for the whole cycle, not just restore.

## 7. Swap-in and the unlocked world

- Substitution is by closed set. A slot's closed set may contain L alongside unlocked materials; every L in every set maps to the same U. Slot arrays take U. Animation-closure references to L remap to U through the existing curve-rewrite machinery, so a material-swap keyframe that referenced L now references U and keeps working while the window is open.
- Provenance. The L to U replacement registers with NDMF's object registry at swap-in, so error reports and downstream tools resolve U back to L.
- After swap-in, the existing pipeline is untouched: capture reads U, the Poiyomi frontend computes semantics from the full property table, the GPU texture routes run as for any unlocked material, conversion derives the canonical opaque material from U, and apply performs its single mutation sequence. No animatability special case exists; the locked material never stays in the slot.
- Animation reach parity. The shipped re-locked U has the same animatable surface as the user's original lock: plain-name bindings for re-baked properties are dead exactly as they were before AMUSE, and suffixed bindings live again after the re-lock, provided name parity per section 6. AMUSE adds and removes nothing.

## 8. Window close

- Close is a dedicated final pass in the PlatformFinish sequence, ordered after apply. It reads the swapped pairs from build state and, for each, re-locks U through one batched reflected `LockMaterials` call.
- Success: U is locked again, `_ShaderOptimizerEnabled` equals 1, the shader name is a `Hidden/Locked/` form, and the pair is closed. Batch verification after the call; any pair that did not lock takes the fallback.
- Failure of the batch or of a pair: the swap-in remap inverts for the affected pairs, slot arrays and every animation-closure reference revert to L before any U clone is destroyed, and `TransientUnlockRelockFailed` is recorded per affected slot. A destroyed U still referenced by a rewritten curve would serialize as a missing reference. The shipped state equals the 2026-09-19 behavior for those slots.
- Leak discipline. A build that completes must have zero swapped pairs still unlocked at ship. The close pass covers the ordinary path; an unexpected exception between swap-in and close is build-fatal by the existing AMUSE convention, and a build-fatal abort never ships. Falsifier 10 tests the close pass directly, and falsifier 11 tests the fallback on a forced re-lock failure.
- Play-mode builds. The active re-lock is an editor API call and runs identically on the play-mode path, so the window closes there too. The paths note's play-mode exclusion, which existed because the upload-time lock never runs in play mode, is no longer needed. Verification task V2 covers the play-mode variant.

## 9. Relationship to the order-100 upload lock

AMUSE's re-lock happens inside PlatformFinish, which precedes the order-100 upload callback. When that callback runs, every AMUSE-swapped material is already locked and its filter skips them. The reproduced failure class, a throwing callback aborting a build, is therefore unchanged ecosystem behavior that no longer gates AMUSE's correctness for swapped materials. Two residues stay honest in the consent subject: non-swapped unlocked materials on the same avatar depend on that callback exactly as they did without AMUSE, and the VRCSDK abort message a user sees on a malformed descriptor is unrelated to AMUSE.

## 10. Analysis and conversion inside the window

- The canonical opaque material is derived from U with the existing per-family conversion, so its recipe, attestation, and depth-test policy treatment are identical to unlocked materials.
- Apply-time shape: the slot holds the re-locked U, the appended submesh holds the canonical material, and the mesh split matches the 2026-09-19 behavior. AMUSE's generated assets declare neither Thry marker property, so the order-100 variant-clearing callback's property predicate does not match them. Verification task V1 covers the lock-button attribute on the attested canonical target shader, which is a pre-existing exposure of the current unlocked flow if present.
- The restore losses cannot reach the shipped avatar beyond what the user's own lock already lost. Property-inconsistent keywords were reconciled away at the user's original lock. Stripped-texture tiling was deleted at the user's original lock. The unlock, re-lock cycle adds neither, which the round-trip note verifies.

## 11. Thry attestation and drift

- Pin by source digest. Both tool pins ship as C# source. At admission the harness locates the `MonoScript` assets named `ShaderOptimizer`, requires exactly one hit, hashes the file, and compares against the pinned digest set: one digest for the 9.3.64 embedded tool, one for the 2.74.2 standalone tool, recorded at pin time from the verified archives.
- The reflection seam resolves the type by full name across loaded assemblies, then `UnlockMaterials` and `LockMaterials` with the expected parameter shapes. Resolution is cached per domain in a keyed static and never grown into an evidence cache.
- Unattested future versions extend the per-build consent pattern, keyed on the Thry digest, exactly as host versions extend D8 as of 2026-09-19. A consent grant never makes a version tested.
- Era-2 residue note. A re-locked material whose state differs from any prior lock produces a new content-addressed cache entry under `Assets/_LockedShaderCache`. That is Thry's own storage behavior for any lock, and the cache garbage collector owns its budget. Recorded as a known interaction, not an AMUSE defect.

## 12. Refusals and consent

- New closed-enum members: `RendererAnalysisRefusal.LockedPoiyomiThryUnattested`, `RendererAnalysisRefusal.LockedPoiyomiOriginalShaderUnattested`, `AlphaSeparationSlotRefusal.TransientUnlockRestoreMismatch`, and `AlphaSeparationSlotRefusal.TransientUnlockRelockFailed`. This resolves the enum-spelling decision the paths note deferred: dedicated members, because gating and diagnostics must distinguish a known locked refusal from generic Unknown.
- Census note. The research census mirrors declaration order for renderer-scoped refusals, so the two `RendererAnalysisRefusal` additions carry census row appends. The two slot refusal members have no census surface and are carried by report strings. Extending the census schema to slot refusals would be a separate reviewed research-package decision.
- Consent. The feature ships consent-gated per build until verification tasks V2 and V4 pass, then the gate moves to the D8 pattern: attested combinations run without per-build consent, unattested combinations ask. The consent subject names, in neutral language: the unlock window, the active re-lock, the render-equivalence basis, the fallback behavior, and the fact that the upload-time lock is not load-bearing for swapped materials. It also discloses that tools running after the NDMF build may re-process the re-locked output. With d4rkAvatarOptimizer Write Properties as Static Values enabled, the re-locked shader is re-parsed and re-optimized like any locked shader; upstream documents locked materials with rename-animated properties as the supported combination for that feature, and the suffixed bindings stay animatable (`2026-09-20-d4rk-write-properties-as-static-values.md`, sections 4 and 5.3).

## 13. Performance and the cache seam

Observed per-material costs, live: restore 270 to 621 ms within one era, 2957 ms cross-era; re-lock 1215 to 4875 ms, or cache-hit fast. The caching investigation's seam applies with one refinement: the cacheable unit is the derived analysis keyed by the locked serialization digest including tags, the Thry digest, the original shader digest, and the policy version. On a hit, the window still opens, but the capture and conversion work is served from the cache and the round trip is the only per-build cost. On any doubt the entry is a miss, per the caching note's soundness contract.

## 14. Testing strategy

- Vendor independence. Tests never install Poiyomi or Thry. The reflection seam sits behind production delegates: `TransientUnlockAvailability` for attestation and resolution, `TransientUnlockDelegate` for restore, `TransientRelockDelegate` for re-lock. Fakes script restore and re-lock outcomes, and the real admission, swap-in, window-close, fallback, and refusal logic runs on schema-only stand-in shaders under `Hidden/Alrauna/AmuseTests/*`.
- RED/GREEN. Every behavior change lands with a failing test observed first against a named plausible wrong implementation.
- Falsifiers. F1, a no-op restore refuses and never swaps in. F2, a partial restore produces the named refusal, never a closure failure. F3, no cycle step deletes or mutates L's generated shader asset or L itself. F4, a mixed closed set degrades per slot. F5, suffixed clip bindings survive swap-in and re-lock as live. F6, an unlock failure never widens refusal beyond the owning slot. F7, an unlocked material with stale tags is not admitted as locked. F8, an unattested Thry digest refuses before any clone is made. F9, an unpersisted clone is detected as a no-op restore. F10, the close pass re-locks every open pair; none ships unlocked. F11, a forced re-lock failure inverts the swap-in remap back to L and records the named refusal, with no surviving reference to U. F12, rename-suffix parity holds across the cycle so suffixed bindings stay live. F13, the fallback L is never destroyed even when it is a project asset.
- Live validation. The lab harness already exercises restore and re-lock against the real vendor at both pins and cross-era. Lab runs validate the mechanism; repository tests validate AMUSE logic. Neither substitutes for the other.

## 15. Implementation sequence

One branch per increment, each with its own RED/GREEN evidence, in this order.

1. Locked identity substrate: identity record, the two attestation refusals, selection pre-check, census rows. No unlock.
2. The window as behavior-neutral pass-through: unlock route, swap-in, window close with re-lock and fallback, falsifiers F1 to F3 and F7 to F13. After this increment, a locked material round trips to a re-locked equivalent with no transformation applied, which is the infrastructure proof.
3. Transformation inside the window: wire capture, classification, conversion, apply, curve mapping, and the object registry to the swapped world, falsifiers F4 to F6.
4. Verification tasks V1 and V2, then the consent gate move per section 12.
5. Cache seam integration per section 13, after the cache service exists.

Each increment ends with its diffs inspected and observed test counts recorded. The implementation prompt states the base branch, the exact allowed mutations, the required RED/GREEN evidence, the validation steps, and the stop conditions, per repository discipline.

## 16. Verification tasks

- V1, canonical target exposure. Confirm the attested canonical opaque target shader does not declare the `ThryShaderOptimizerLockButton` attribute. If it does, the upload-time lock processes AMUSE's canonical materials on machines with Thry installed, which is a pre-existing condition of the current unlocked flow that must be recorded and consented, not inherited without disclosure.
- V2, in-build window characterization. Characterize swap-in, restore, re-lock, and fallback inside a live NDMF build, upload path and play-mode path, including the `AssetDatabase.Refresh` behavior of Thry's batch inside a build and the interaction with NDMF's AssetSaver container. The lab sessions ran restore and re-lock outside builds. V2 must also observe that the re-locked shader files are complete and imported on disk before the SDK callback chain runs, because post-NDMF tools such as d4rkAvatarOptimizer read shaders from disk; its parse-skip failure direction is benign but must be observed, not assumed (`2026-09-20-d4rk-write-properties-as-static-values.md`, section 5.3).
- V3, cache false-hit guard. Confirm the analysis cache keys on the full locked serialization including tags, because unlock residue changes era-2 content addresses.
- V4, re-lock parity. Confirm the re-locked material's declared property table is a superset of the original locked table, so animation reach never regresses across the cycle, including the suffixed-name case of falsifier 12.

## 17. Open questions

- Whether `Material.GetRoot` behaves differently for material variants under the lock filter. Variants are refused as of 2026-09-19 for other reasons; this only matters if variants reach the locked route.
- Whether Thry's cache garbage collection can delete a generated shader asset mid-build while L or U still references it. Falsifier 3 watches the cycle; the broader GC question stays open.
- Exact consent wording and the consent-gate default, settled against V2 and V4 results at implementation time.
