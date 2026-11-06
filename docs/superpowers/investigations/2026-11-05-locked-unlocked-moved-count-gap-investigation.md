# Locked and unlocked moved-count gap investigation

Record date: 2026-11-05. Status: measurement in progress. This record speaks for its date. It never authorizes new subsystems.

## Privacy note

This record describes a private avatar by role only. It names no avatar, renderer, material, animation, or hierarchy names. Exact counts quoted from owner builds appear only where the owner quoted them. The scratch mirror project is a full copy of the private Lab project used for measurement.

## The question

The owner reports a moved-triangle gap between two build states of the same avatar. Builds report 180445 moved triangles when the avatar's Poiyomi materials are locked. Builds report 193172 when the same materials are unlocked. The gap is 12727 triangles. The session fix for the session flip landed before this investigation. That fix made locked builds deterministic. This investigation asks why locked and unlocked states classify differently at all, and which side of the gap is wrong.

## What locked means

The vendor lock is the Poiyomi optimizer lock, driven by the Thry editor. Locking swaps the material's shader to a generated variant whose name starts with `Hidden/Locked/` and whose optimizer property default is rewritten to one. The material keeps the generated shader pointer plus bookkeeping tags. Unlock swaps the original shader back and restores what the lock stripped. The stripped set includes texture references, keywords, renamed property copies, and render queue state. The vendor stores the originals in tags on the material and in a content addressed cache. The authoritative locked test reads the shader's property default, not the material's float. The material float is a user interface mirror.

AMUSE has its own locked identity predicate. It requires the `Hidden/Locked/` shader name prefix and the optimizer float equal to one. Tags never decide identity. This predicate gates the unlock window.

## The two AMUSE paths

Locked sources run through the unlock window. The window opens pairs of locked original and reconstructed clone. The reconstruction aligns the generated shader text against the attested original, builds a value reproduction plan from the locked serialization, applies it to the clone, and verifies consistency. Capture reads the swapped world. Locked-only admission gates refuse a material when a renderer shows a property block or when animation binds a property whose aligned state is baked, renamed, or unused. Any refusal keeps the material's slots in place.

Unlocked sources are read directly. No window opens. No locked-only gate runs. Capture reads the live unlocked material. The same property block and animation bindings become ordinary captured evidence.

## Ranked causes for the gap

First, locked-only admission refusal. The property block and animation gates run only in the locked build. In the unlocked build the same evidence converts. The discriminator is a named locked-only refusal line in the locked build's console or record, absent in the unlocked build.

Second, window never opens. An attestation or restore refusal leaves the locked build analyzing a locked material with no window. The capture refuses locked evidence outright, so every triangle of the slot stays kept. The discriminator is the unattested or restore mismatch sentence in the locked build.

Third, reconstruction drift. The reconstructed clone differs from the vendor-unlocked material in an alpha relevant value, keyword, or queue. The discriminator is a per-row proven triangle difference between the two runs, and a value dump difference between the clone and the unlocked material.

Causes one and two are structural asymmetries. They are guaranteed differences between the two paths. Cause three needs value drift. The observed whole-slot scale of the gap fits one or two best.

## Measurement plan

The scratch mirror project holds the avatar with its materials locked. New editor tooling flips the lock state through the vendor's own entry points. The tool menus live under the scratch measurement menu section. They report state, unlock all, and lock all. The tool never flips the mirror float by hand. The vendor batch save is config gated, so the tool saves assets itself.

The measurement runs build cycles unlocked and diffs the saved per-renderer slot rows against the locked baseline pair already recorded under the current code. The diff keys each slot by renderer path and slot index and compares moved, proven, total, and sentence. Named refusal lines and the kept census line come from each run's record. The census line was added for exactly this purpose. Kept slots otherwise print nothing.

## Result

Observed 2026-11-05. The scratch pair reproduced both owner numbers exactly. Four unlocked cycles read 193172 each. A re-locked cycle read 180445. The gap is avatar driven and stable in both states.

The row diff names the whole gap. One renderer, two material slots. In the locked run both slots keep everything. In the unlocked run both slots convert fully. The two slots carry two locked hair product materials from one commercial hair asset. Their combined triangle total is the whole 12727.

The locked run prints the refusal chain for both slots. First, the unlock window refuses the pair. The reconstruction does not pass verification. The reason is a baked texel size that does not match the texture, with the shared generated shader hint. Second, runtime state resolution refuses the slot. The locked shader variant left on the slot is a generated variant that AMUSE does not attest, so the locked evidence is unreadable. The slot proves nothing. The unlock window refusal is cause two from the ranked list, triggered by the alignment texel size rule.

The unlocked run reads the same two materials directly. Nothing refuses. The proof marks every triangle opaque, and the count includes them.

## Classification

The unlocked count is the correct one. The unlocked path is the product's ordinary proof path over readable sources. The locked path carries a coverage defect. Its alignment rejects a generated shader on the texel size rule, so AMUSE refuses two materials that the same code proves when unlocked. The refusal is safe. It keeps the originals and claims nothing. It is still an unnecessary refusal by the correctness policy.

The likely shape of the defect: the vendor bakes texel size constants into the generated shader at lock time. The bake can drift from the texture's current size or land oddly when the generated shader is shared. The alignment rule treats that disagreement as a reconstruction mismatch. The vendor's own unlock never revalidates the bake, so the strict check refuses locks the vendor accepts. The alignment contract should treat a stale or shared bake as era drift, not as a mismatch. The exact rule change needs the alignment texel size code and the two generated shaders read side by side before any fix.

The fix direction is a refinement of the texel size rule in the locked source alignment. It needs a failing test first, with fixtures built from the two refused generated shaders characterized by role. The unlocked measurement needs no fix.

## Tools

The scratch project gained one editor tool file with three menu items. Report prints a per material survey grouped by lock state to a capture file and the console. Unlock and lock call the vendor batch entry points for every scene material and save after. The tool logs the material count and warns that the vendor has no undo. The tool reproduced both states and both numbers in the same session.

## Texel identity gate measurement

Section date: 2026-11-05. This section records the snapshot measurement gate from the texel size verification refinement plan. The gate decides whether the planned snapshot identity fact is safe to build. It changes no product file.

### What was measured

The scratch probe hooked two production entries through reflection. The first entry clones a locked material and verifies the restore. Its prefix stored the locked material keyed by material instance number. Its postfix bound the returned clone and marked the restore outcome for that key. The second entry rebuilds the unlocked material on the clone. Its prefix stored the clone and the clone's generated shader before the rebuild ran. Its postfix read that stored shader, resolved the recorded original through the clone's recorded identity tag chain, read the original property table, and dumped one fact line per texture slot. Each fact line carried the material key, the slot role, the clone snapshot token, and the locked material snapshot token. Each token read resolved the renamed slot name through the aligned record, the same mapping the reproduction texture read uses. The tokens stayed in the scratch capture file and never entered this repository.

The scratch state was locked before the cycle. The survey reported every Poiyomi material locked and none unlocked. One locked build cycle ran through the existing driver entry. The cycle read the locked baseline total, so the cycle matched the earlier locked runs.

### Join rule

The probe joined fact blocks to outcomes by material instance number, never by order. The swap-in postfix marks which reconstructions refused. A refused reconstruction counts as unjoined only when its material key carries no fact block. No fact block carried a material key without a marked outcome.

### Counts

- Marked refused reconstructions: 2. Both are the two known hair product material instances named by the row diff.
- Cycle console lines with the exact texel refusal sentence: 2. This count equals the marked refusal count.
- Texture slots enumerated across the two refused reconstructions: 274. Each refusal enumerated 137 texture slots.
- Slots where both the clone snapshot and the locked material snapshot produced a readable identity fact: 56.
- Slots that agree on identity: 56. Every compared slot agrees.
- Slots that differ: 0.
- Slots left scene unknown: 0.
- Slots with no identity fact on either side: 218. Every one of these slots reads as unused in the aligned record. The generated shader declares none of them and the locked serialization holds none of them. The planned comparison treats an absent fact on both sides as equal, so these slots carry no identity question.
- Of the 56 agreeing slots, 12 carry a texture on both sides. The other 44 read as empty on both sides.
- Renamed texture slots among the compared set: 0. The two materials carry no renamed texture slots. The rename aware read path ran on every slot, but no renamed slot existed to prove. Rename coverage stays a concern for the identity fact tests.

### Verdict

Every compared slot agrees on identity. No slot stayed scene unknown. No refused reconstruction stayed unjoined. The refusal sentence count equals the marked refusal count. The gate passes. The refusing build snapshot carries the same texture identity that the locked material carries.

## Locked cycle recovery measurement

Section date: 2026-11-05. This section records the recovery check after the texel shape rule and the texture identity fact landed in the product package. The scratch project ran the changed package. The check verifies the recovery expectation from the Result section. It names no private material or asset. Slot roles and measured counts only.

### What was measured

The scratch refreshed against the changed package and recompiled. Reflection confirmed the removed equality members are gone and both new refusal details are live in the compiled product assembly. The lock state report listed 43 scene materials. 30 carry locked shader variants, 13 are not optimizer materials, and none are unlocked. The scratch state stayed locked from the earlier gate run.

One locked build cycle ran through the existing driver entry with the cycle count set to one. The driver appended 88 slot rows for one build to the same witness file. The cycle read its full build, exit, and capture without a hand accepted dialog.

### Counts

- Locked cycle moved triangle total: 193172. This equals the unlocked baseline of 193172 from the Result section, and it exceeds the earlier locked total of 180445 by exactly the measured 12727 gap.
- The two hair product material slots of the commercial hair asset on the merged skinned mesh renderer both converted. One moved 4984 triangles and the other moved 7743 triangles. Their combined 12727 triangles equal the whole measured gap, and each slot moved every triangle it holds.
- Cycle rows with the exact texel refusal sentence: 0. No other new refusal sentence appeared on the two slots. Both rows print the full conversion disposition.
- Other locked slot rows on unrelated renderers kept their earlier dispositions. Their refusal kinds are unchanged by this amendment.

### Verdict

Observed 2026-11-05. The recovery expectation from the Result section held. A locked build converts both hair product material slots and reads the unlocked baseline total. The measured coverage loss of 12727 triangles is closed. The stop condition did not trigger. The amendment recovers the measured coverage with no new refusal.
