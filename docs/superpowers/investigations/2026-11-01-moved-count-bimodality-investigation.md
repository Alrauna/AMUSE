# Moved-triangle bimodality investigation

Privacy note. This record names no machine, account, port, editor instance, private avatar, material, or asset path. Renderers and materials are named by role. Exact private counts appear as rounded ranges. All observations come from the private Lab project and the shared editor log between 2026-11-01 late evening and 2026-11-01 early morning local, through the dev editor instance of this repository. The Lab stayed read-only except for the owner's own builds.

## Question

The owner's locked test build moves about 182.6k triangles in some editor sessions and about 109.3k in others. The same avatar, the same settings, and the same package produce both values across editor restarts. The unlocked comparison stays stable at about 133.0k. The gap itself is out of scope. The bug is the flip.

## Method

A five-worker read-only pool mapped every run-to-run variable surface in the product source: texture readback, enumeration order, cross-build state carryover, live editor inputs, and merged-mesh slot mapping. In parallel, the shared editor log yielded the complete report block of both outcome runs, and the play-mode record of the high run gave its exact per-slot decomposition. The Lab editor supplied the record through its session store. Nothing was built or edited by this investigation.

## Findings

1. The flip is real and current. Both summary lines sit in the same shared log, both from 2026-11-01, both after the Lab compiled the locked-source fix at about 01:30. Stale code is excluded. The low run analyzed the same 25 renderers and kept exactly one more renderer fully original.

2. The flip set is exact. The high run decomposes as: wolf hair set about 33.1k, hair pin about 17.0k, merged hair mesh about 56.2k, merged outfit mesh about 74.1k, ears accessory about 2.1k. The low run equals the high run minus the merged outfit mesh's six non-separable slots, about 71.2k, minus the ears accessory, about 2.1k. The swing is about 73.3k. The one extra kept-original renderer is the ears accessory.

3. The flip is console-silent. In the low run the flipped slots print nothing: no slot refusal, no separation note, no left-unchanged line. Every refusal the two runs do print is byte-identical, including the same locked-shader refusals with the same content hashes and the same two reconstruction refusals on the merged hair mesh. Successful whole-slot moves are silent by design, and animation-protected slots, the untouchable shape, print only into the play record. A bimodal pair therefore leaves no distinguishing trace in the console.

4. Ruled out by the pool with file-line evidence: string hash seed ordering, every cross-build cache (all seven reset per build), AMUSE slot mapping (positional end to end), texture capture refusal (none logged in either run. The refusal vocabulary has no silent member at whole-texture level), consent and build target (identical summary sentences), and the Lab assembly being stale.

5. Surviving mechanisms live in one layer, the animation evidence over renderer paths. All are restart-scoped and renderer-scoped: which material-swap curves the evidence connects to the merged renderer paths after the optimizer renames and re-parents them, whether the animator rebind lands a time-zero material or a property block over those renderers before the barrier reads them, and which capture arm answers. The locked admission reads the live block fact first, and the 2026-10-25 property-block diagnosis proved such blocks land mid-build through the animator commit chain. The optimizer's merge grouping iterates reference-keyed dictionaries whose order follows per-process instance ids, so per-restart reordering can move a renderer across a sequencing boundary.

6. The report layer hides the flip, whatever the sub-mechanism. The summary counts renderers and triangles but never names the silently kept slots. The play record holds the untouchable rows, but a restart wipes it before anyone reads it.

## Conclusion

The flip is a real cross-restart nondeterminism in the animation-evidence layer, not a locked-path regression and not a coverage gap. The exact flipping slot set is known. The deciding evidence for the sub-mechanism is one bimodal pair where both play records survive: build, keep the editor open, read the record, restart, build again, read again. The untouchable census of the low run names the protecting bindings and settles which mechanism fired.

## Next actions

1. The owner builds one pair, leaving the Lab editor open after each run. The record read needs no scene change.
2. Land a report fix after the pair is captured: the summary names every silently kept slot group with its triangle count, so a future flip self-documents in the console. No product code changes during the live repro loop, because the Lab compiles this package live and a mid-loop reload would disturb the conditions.

## Confirmed by experiment, 2026-11-01

The owner ran the controlled cycle. One session built twice: both runs moved about 109.3k and both records show the flipped slot set as untouchable. A full editor restart, same scene, same settings, one build: moved about 182.6k, and the record shows the same slots moving. Within one editor session the outcome is stable. The restart is the trigger, and it flips in both directions.

The deciding evidence is the per-slot sentence, not the count. The flip set is exactly the merged outfit renderer's six non-separable slots plus one ears slot. In the low state each prints the untouchable sentence with zero movable polygons. In the high state the same slots move silently.

Severity. The outfit slots are material-swapped by the avatar's animations in at least one state. If the bindings are real and a high session misses them, about 73.3k swapped-animated triangles move, which is a false positive against the transformation contract. If the bindings are phantom in the low state, the low state is over-protective. One of the two stable outcomes is wrong, and a restart decides which one ships.

Open root cause. The varying step is the curve retargeting onto merged renderer paths in the NDMF and optimizer chain, upstream of this package's evidence capture. The next diagnostic is a binding dump for the merged outfit renderer's path in both states: capture the platform animator bindings in a low session and in a high session and diff them. A scratch probe inside the low session answers it in one build.

## Census entry landed, 2026-11-01

The report fix landed after the experiment pair. Every analyzed play build now prints one census entry that names how many material slots with how many triangles stayed fully on their original materials. Three tests cover it: two report tests for the entry and its silence, and one capture test that runs the real row flow and counts the unapplied candidate row. The tests failed first on the missing report method, then passed after the implementation. The full product assembly then ran green with 3453 passed, 0 failed, and 19 vendor opt-in skips. The next low run prints its census in the console, so a bimodal pair self-documents even when both records die with their editor sessions.

## Session D: the flip is inside one editor session, 2026-11-01

The owner ran a fresh editor session with two back-to-back builds. The census entries named both runs. The first build kept 24 slots with about 188.8k triangles and moved the high amount. The second build kept 31 slots with about 262.1k triangles and moved the low amount. The kept difference is exactly the flip set: 7 slots, about 73.3k triangles, six slots of the merged outfit renderer plus one ears slot. The per-session stability model died. The flip is a first-processing versus later-processing difference inside one session.

The record of the second run shows the same original materials in the same slots with fully proven triangles and the untouchable sentence. So the avatar is pristine between runs and classification still proves the slots. The drop happens later, at apply validation.

## The decision chain and the kill switch, 2026-11-01

A four-worker read-only pool mapped the whole chain with file-line evidence.

1. The retarget that makes merged slots carry material swaps is the optimizer's object mapping. It rebinds object-reference curves onto the merged renderer paths at the end of the optimizer's scope, always before this package's capture. The ordering never differs between runs. Only the content of the retarget can differ.
2. The evidence captures bindings from the committed graph and from the animation index by exact ordinal path. The apply pass validates every prepared slot again against the live index. When a live material-swap binding sits on the slot that the captured evidence does not hold, the apply refuses the slot with the absent-from-evidence rule and the row becomes the untouchable sentence with zero moved triangles and full proven count.
3. First-versus-later differences live in three places. The generated temporary asset folder is deleted at every play exit, so a fresh session's first build starts on the previous session's leftovers while every later build starts clean. A serialization scope static flag, once leaked, pushes every later build's asset writes into deferred import mode. One process-wide special motion registry is built lazily on first use. Upstream regeneration writes its full output only on the first build of a session and reuses it afterwards, so what the optimizer parses can differ between the first and later builds.

The census, the record, and the pool findings together shrink the open question to one comparison: which binding set differs between the first and the second build of one session, at which hop. A scratch probe logs the capture bindings, every apply validation decision, and the serialization flag per build. It arms only in the Lab project. It failed to arm once on a Harmony load path and now loads Harmony from the already loaded domain assembly with named failure messages.

## Root cause pinned by instrumented pair, 2026-11-01

The final probe run logged the full opaque mapping beside the applied write and the live slot for every outfit slot. The result closes the question. Both builds map every outfit unlock clone to itself. The locked reconstruction is already the opaque form, so the conversion correctly needs no new material. Only one slot of the outfit renderer receives a generated canonical material. The apply validation refused nothing in either build, and classification proved every slot fully in both builds.

The flip sits in one accounting gate. The moved triangle count treated a slot as moved only when the applied write held a different material instance than the live slot. A window clone slot that writes its own instance back is an expected no-op write, because the window swap in already replaced the locked original with the reconstruction. Whether the write and the slot held the same instance crossed with the editor session, so about 73.3k proven triangles flipped between counted and uncounted. The shipped avatar was identical in both states. The untouchable row was the same artifact: a proven fully opaque slot whose write was a same-instance no-op was labeled as animation protected.

## Fix landed, 2026-11-01

The count now follows the plan facts. A slot whose live material is one of the open window's unlocked clones counts as moved, whatever instance the write holds, because the swap in already changed the slot materially. A plain self-mapped opaque slot outside the window still counts zero, which an earlier intended rule keeps. Two tests cover both arms. The real pair in the Lab served as the red evidence, because the synthetic tests landed after a compile race. The live low run showed the identity mapping with the zero count and the live high run showed the same mapping with the full count. The full product assembly ran green after the fix with 3474 cases completed and no failure.

Amended 2026-11-02. The adversarial review found three defects in this first counting implementation and the count was rewritten to follow the surviving plan directly. The defects: a Split slot whose live material was a window clone counted its ordinals twice, once through the changed slot path and once through the appended submesh path. A refused slot whose live material was a window clone counted as moved although its candidate never survived. A renderer whose every surviving slot was identity mapped produced no write at all, so the new counting never ran and the undercount survived for exactly that renderer. The rewritten count sums the proven ordinals of every surviving candidate and marks each surviving slot applied, with no instance comparison and no clone membership. The census entry and the row accounting now key on the same surviving plan. Three tests cover the survivor ordinals, the identity mapped counting, and the refused slot exclusion. The full product assembly ran green after the rewrite with 3456 passed, 0 failed, and 19 opt-in skips.

The census entry from the earlier fix stays as the cross-run witness. The next Lab pair should print one stable number for both builds, and the outfit rows should read as convertible rather than untouchable.
