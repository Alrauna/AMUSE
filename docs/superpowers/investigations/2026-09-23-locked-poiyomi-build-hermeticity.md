# Locked Poiyomi build hermeticity

Privacy note: This record uses the private Census Lab project as observation input. It names no avatar, scene, material, renderer, texture, vendor folder, or asset path. Counts of private observations are ranges. The record states its sanitization here.

Date: 2026-09-23.
Branch under test: `fix/poiyomi-generated-material-locking`.
Investigation status on 2026-09-23: Read-only. No production code changed. No product test ran. The observations cover editor sessions from 2026-09-22 and 2026-09-23.

## Question

Does the transient unlock window keep an AMUSE build hermetic? A build is hermetic when it writes no assets outside the NDMF generated container, triggers no asset database refresh between evidence capture and apply, and leaves the on-disk state unchanged so the next build of the same input starts from the same state.

## Method

The sources are the live editor console, the persistent editor session log, the probe summary file the Lab probe writes, and the Lab project folders. All reads were read-only. One Unity editor instance was reachable. Its `Application.dataPath` matched the Census Lab project exactly before any read.

## Observation 1: the vendor calls write assets and refresh during the build

Proven by log stacks on 2026-09-22 and 2026-09-23. Both the vendor unlock and the vendor relock ran inside AMUSE passes through `TransientUnlockAvailability.InvokeVendor`. Each call reached the vendor optimizer code that writes serialized shader files and calls the asset database refresh. The log records, inside the AMUSE close pass:

- Imports of generated locked shader folders inside the NDMF generated container.
- Asset pipeline refresh summaries of one to two seconds each.
- Shader compiler process launches after the refresh.
- One barrier pass that took over fifty seconds on a locked test avatar.

This executes the refresh behavior the 2026-09-20 transient unlock design named as verification task V2. The refresh is not harmless: it runs between the barrier's evidence capture and later passes, so any captured live-object evidence can go stale by construction.

## Observation 2: a generated locked shader folder sits in a vendor source area

Proven by folder listing on 2026-09-23. A generated locked shader folder of the vendor tool's naming shape exists inside a private vendor material folder in the Lab project. The Lab project also re-imported a private source material file several times during the 2026-09-22 sessions, which means the file on disk changed during builds.

The vendor tool writes its generated shader beside the material asset it locks at the pinned era. A clone that the build persists through the NDMF asset saver gets its generated folder inside the container, which is acceptable. A lock or unlock that resolves to a source material writes into the source area, which violates the repository rule that source assets are never mutation targets.

Attribution is open. The log proves that vendor lock calls ran during builds and that the source file changed. It does not prove which call path wrote the source-area folder. Candidates are the probe's full local preprocess chain, which includes the vendor upload callback, and earlier manual probing of a clone of the same material. The investigation records the fact and leaves the attribution to the preflight that the follow-up plan adds.

## Observation 3: every relock mints a new locked identity

Proven by the probe summary on 2026-09-22. The same locked material carried one locked shader identity before a build and a different one after the build. The 2026-09-20 round trip characterization predicted this: a lock cycle is not address-stable, and unlock residue changes the next lock's content address. The consequence is that the on-disk input of the next build differs from the input of the previous build after any window that relocks.

## Observation 4: the outcome variation across the session is explained, not random

The same unchanged test avatar, one renderer with over half a million triangles, produced three report families across the sessions:

1. `AMUSE could not prove any triangle on this renderer.` in the earlier runs.
2. `AMUSE separated material slot 0` in later runs, moving roughly a third of the triangles.
3. `AMUSE left material slot 0 unchanged` in the latest runs, each entry following a separation entry in the same play session.

Attribution, dated 2026-09-23:

- Family 1 predates the consent-free window commit. The locked material answered all-Unknown, which was the correct conservative refusal at that code state.
- Family 2 followed that commit. The window restored the material, the classifier proved the opaque majority, and apply separated.
- Family 3 is play mode double processing. One play session runs the preprocess chain over the scene avatar more than once, through two different host triggers. The second run sees the already separated mesh and the generated canonical material, so it correctly changes nothing.

No same-input flip within one product version was observed. The earlier session summary in chat overstated this as nondeterminism. The correction is recorded here. The standing defects are observations 1 through 3. They make non-hermetic builds possible, and family 3 shows the Lab console can show opposite outcomes for one avatar within one session, which misleads diagnosis.

## Observation 5: unrelated environment noise

The console holds recurring exceptions that are not part of this defect: the NDMF Harmony patch failures with `mprotect returned EACCES` on this host, a native plugin built for another architecture, and hook init failures in another vendor tool. They predate and survive this work. No action in this design depends on them.

## Finding

The transient unlock window is not hermetic. The vendor restore and relock calls write assets and refresh the asset database inside AMUSE passes, every relock changes the locked identity on disk, and at least one build-related vendor call path wrote into a private source area. The classification outcomes observed across the session remain explainable per product version, but the build violates the capture boundary the architecture requires, and the Lab cannot today prove that a measurement repeated tomorrow starts from the same input state.

## Decision

Dated 2026-09-23, approved in chat.

1. The product stops invoking the vendor unlock and relock for the swapped source pairs. The restore becomes an AMUSE-owned in-memory reconstruction from the locked serialization. The close pass reverts references to the untouched locked original instead of relocking the clone. The vendor relock remains only for generated canonical outputs on the play path, after all evidence capture.
2. The Lab gains a corpus-reset preflight. It snapshots the pristine corpus state, restores it before every probe run, and asserts no drift after the run. Drift becomes a named failure with a diff, never a silent wrong answer.
3. The refusal tier stays fail-closed. A locked material whose original shader does not attest, or a play path without an attested vendor tool, refuses as today.

This decision supersedes two choices of the 2026-09-20 transient unlock design for the source pair: the vendor-invoked restore and the active relock of the unlocked clone. That design's window shape, swap-in discipline, fallback, and generated-output locking from 2026-09-23 are unchanged. The design and plan are `docs/superpowers/specs/2026-09-23-locked-poiyomi-build-hermeticity-design.md` and `docs/superpowers/plans/2026-09-23-locked-poiyomi-build-hermeticity-plan.md`.

## Limits

- No uploaded bundle was built or inspected. The upload path statements inherit the limits of the 2026-09-20 and 2026-09-23 records.
- The source-area write attribution stays open until the preflight reports the first drift diff.
- The in-memory reconstruction depends on the characterized lock format of the pinned vendor era. Other eras remain refused, as today.

## Preflight result

Date: 2026-09-23. Phase A of the plan ran the same day.

The preflight exists in the private Lab project. Its first baseline covered about 3,900 watched files and byte-backed about 3,700. Its first restore removed 27 vendor-generated locked shader folders across many vendor source areas, not the one folder this investigation found. The pollution was systemic.

The removal broke nothing. A survey of every material in the project found 12 with unresolved shader references out of more than 2,000. The referenced shader identities have zero overlap with the deleted files, so that breakage predates the preflight and belongs to the vendor assets themselves.

One probe run on the test avatar after the wiring reported its accepted result, a restore that changed nothing, and a preflight PASS with zero drift. No generated folder appeared in any vendor source area. The write-path attribution of the original pollution stays open, because the preflight now prevents the drift that would have produced the evidence.

Phase B of the plan did not start under its original base condition. The owner overruled the condition the same day, and Phase B ran on the working branch. See the implementation result below.

## Implementation result

Date: 2026-09-23. Phase B of the plan ran the same day on the working branch under an owner override recorded in the plan.

The reconstruction exists and the window uses it. The production restore reads the locked serialization in memory: identity tags, original shader by recorded GUID, keyword set from the recorded tag, rename-suffixed values moved onto declared plain names, optimizer flag cleared. No vendor call runs before apply on any path. The close pass reverts every pair to the untouched locked original and relocks only generated canonical outputs on the play path. A non-play build needs no vendor tool at all.

Observed counts, dev editor instance. The five transient-unlock and reconstruction classes pass 47 of 47. The full product and research assemblies completed 2,381 cases with six failures. Five are the known pre-existing failures. The sixth pinned the superseded gate and was updated to the narrowed play-path refusal, then passed. One review fix matters beyond style: Vector4 values share the colors container on this editor's material serialization, so the reader routes colors entries onto Vector-declared plain names too.

Observed counts, Census Lab instance. One probe run with the new product code reported accepted=True. The preflight restored one externally drifted locked material before the run and reported PASS with zero drift after. The session log shows zero vendor-generated locked shader writes anywhere in the run and no vendor call inside any AMUSE pass. The single vendor frame in the window is the SDK upload callback after NDMF, which the design documents as ecosystem residue.

The write-path attribution for the original pollution stays open. The preflight prevents the drift that would have produced the evidence. No upload ran.
