# Texel size verification refinement design

Privacy note. This record uses public product source, public vendor behavior, and scratch measurements. It names no avatar, material, texture, animation, hierarchy, machine path, or generated asset identifier. Texture sizes and bake prints in the falsifier fixtures are synthetic. Sanitized measurements speak for their date.

Status: 2026-11-05. Revised after two independent adversarial reviews. The owner approved writing this design and running adversarial review in parallel. That approval authorizes no implementation, no asset writes, no staging, and no commits.

## Intent and basis

The reproduction step refuses a locked material when a baked texel size literal disagrees with the texture that the values snapshot carries. The 2026-11-05 gap investigation found this refusal covering two slots that the unlocked path proves. This design amends that check and pairs the amendment with one new identity fact. It does not widen any other rule.

Basis:

- `docs/superpowers/specs/2026-10-23-locked-source-alignment-design.md`, decision A5.4.
- `docs/superpowers/investigations/2026-11-05-locked-unlocked-moved-count-gap-investigation.md`.
- `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/LockedSourceReproduction.cs`, the baked texel size branch and `ExpectedTexelSize`.
- Scratch measurements dated 2026-11-05: the pipeline succeeds against the scene locked material and against a plain in-memory clone of it. The build refuses with the texel detail. The refusal sentence is fixed and names no property.

## The problem

The check compares the baked literal in the generated text against a fresh print of the texture dimensions on the values snapshot. The vendor prints the bake from the texture state at lock time. Four facts follow.

First, the bake is history. A texture can change size, assignment, or identity after the lock. The vendor never refreshes the bake. A disagreement between bake and current texture therefore cannot prove that the generated text is not vendor output. For locks made by the bundled optimizer build in this repository, a stale bake implies a texture change after lock, because the reuse hash includes the texel vector. Sharing between materials remains reachable only for locks made by older optimizer builds. The rule change covers both causes the same way.

Second, the check writes nothing. A baked texel size passes and the plan writes nothing for it. The check is a pure verification gate on its own branch. No other rule consumes its outcome. The second pass re-runs only the define split and the removed block checks.

Third, the gate has real cost. The investigation measured a 12727 triangle coverage loss on the affected avatar, all of it behind this one refusal detail.

Fourth, the measured refusal is an anomaly. The scene locked material passes the equality, and a plain in-memory clone passes it, while the build refuses. The build snapshot therefore resolved a texture fact whose print differs from the print of the material's own texture. The design does not yet know which snapshot or why. The amendment must keep failing closed on that anomaly while restoring coverage for stale and shared bakes.

## Goals

1. Accept a baked texel size when the bake is a well formed vendor print, whatever the current texture says.
2. Keep refusing bakes that are not well formed prints.
3. Keep refusing when the build snapshot does not carry the same texture identity the locked material carries.
4. Keep every other reproduction rule unchanged.
5. Keep refusal sentences fixed and property free.

## Non-goals and anti-goals

- Do not change the aligner, attestation, admission gates, the unlock window lifecycle, or vendor code.
- Do not start writing texel sizes into the clone. The bake writes nothing, before and after this design.
- Do not name properties or textures in refusal sentences.
- Do not build the diagnosis tooling into the product. Scratch tooling stays in the scratch project.
- Do not treat this amendment as permission to relax the baked scale offset rule, the baked literal round trip, or any other A5 rule.
- Keep both bundled admission flags false.

## Global constraints

- Unity 2022.3, the existing production assemblies, no new dependencies.
- The aligner stays pure. The reproduction stays pure.
- Unknown information refuses only the conclusion that depends on it. A texture that fails to resolve keeps the existing empty texture behavior.
- Records use simple english. No private identifiers, machine paths, or vendor shader copies.

## Approaches

| Approach | Benefit | Risk or cost | Decision |
|---|---|---|---|
| Require the bake to equal a fresh texture print | Strongest equality today | Refuses legitimate locks on texture drift and on build path value snapshots. Measured 12727 triangle loss | Reject |
| Accept every baked texel size without any check | Maximum coverage | Accepts hand edited generated text as vendor output. Breaks the alignment guarantee | Reject |
| Accept a self consistent vendor print, and verify snapshot texture identity separately | Coverage restored, hand edits still refused, snapshot anomalies still fail closed | A stale bake passes, and the shipped material can render texel dependent code unlike the locked material. The vendor unlock diverges the same way | Choose |

## TX1. The texel check verifies print self consistency

Replace the equality comparison between the baked literal and `ExpectedTexelSize` with a shape check on the literal. The comparison is textual. A literal passes when three facts hold. The float4 text round trips under the existing round trip rule. Each of the first two components equals the print of one over the matching size component, both printed in the parameterless invariant format that the vendor uses for float4 components, the same format the float4 round trip rule already enforces. The two size components are positive integers in that format. A bake that round trips but breaks this relation, or whose size components are not positive integers, refuses with the literal shape sentence. A bake whose float4 text is malformed refuses with the literal shape sentence. A bake whose components do not print back refuses with the round trip sentence. The empty texture bake of one, one, one, one passes the shape like any other print.

The current texture stops being an input to this branch. The recovery of the measured coverage is an expectation until TX6 measures it. A build path snapshot that loses a texture can still refuse in the define rule with another sentence. The record keeps the refusal count if that happens.

A bake that disagrees with the live texture is accepted, and the shipped material then derives its texel size from the live texture. The locked material renders the baked size, so the two can differ in any feature that reads it. The vendor's own unlock has the same behavior, so the shipped material equals the vendor unlocked material. The proof reads no texel size, so no proof outcome changes. This is a recorded exception to goal 2 of the 2026-10-23 design, limited to texel size reading code paths.

## TX2. Decision ownership

A5.4 of the 2026-10-23 design gains at its texel item an inline amendment note dated 2026-11-05. The note says the equality against the texture print becomes a print self consistency check on the literal by TX1 of this design, and that the alignment side and the facts record stay unchanged. The reproduction side of A5.4 changes. The alignment side, which decides that a texel entry is baked and records its literal, does not change. Facts equality across eras does not change.

## TX3. The snapshot identity fact

The restore entry does not hold the locked original today. The restore delegate widens to carry the clone and the locked original, and the reconstruction builds one values snapshot of each before the plan. The verification then gains one fact. For every texture property of the typed table, the identity token that the clone snapshot resolves must equal the identity token that the locked original snapshot resolves for the same name, through the aligned rename mapping. A disagreement refuses with one new fixed sentence that names no property. This fact separates the two causes. A stale or shared bake keeps coverage, because identity still agrees. A build snapshot anomaly still fails closed, because identity disagrees.

The fact runs where the reconstruction builds the plan, before any write. It adds no second alignment pass and no second reconstruction pass.

## TX4. Red test and falsifiers

One new characterization boundary in the reproduction tests, plus the recast of one existing test. The fixture pattern stays pure and synthetic. The RED observation for the named wrong implementation, keeping the equality: a fixture whose bake is a well formed print of one size and whose texture resolves to a different size refuses today and passes under this design.

Falsifiers:

- Falsifier 1: a bake whose reciprocal components do not match its size components must refuse with the literal shape sentence. The existing differing size test carries this case and is recast here.
- Falsifier 2: a bake with zero or negative size components must refuse with the literal shape sentence.
- Falsifier 3: a bake that does not round trip as a float4 must refuse with the round trip sentence.
- Falsifier 4: values with no texture on the slot and a well formed bake of a 512 by 512 print pass. A wrong implementation that keeps the texture print comparison must refuse on this fixture, because that comparison expects the print of one, one, one, one for no texture. A second no texture fixture with the unit bake passes under both implementations and guards the empty texture rule.
- Falsifier 5: a baked scale offset still round trips and still wins over the live texture scale and offset, and the texture define rule still reads the current texture set. A wrong implementation that also relaxes those rules must fail.
- Falsifier 6: a clone snapshot whose texture identity differs from the locked original snapshot for the same name refuses with the new identity sentence. A plausible wrong implementation that skips the identity fact must fail.

Fixtures carry synthetic names and sizes. No vendor shader text enters the test assembly.

## TX5. Era and pipeline neutrality

All three eras run the same branch. Fact 5 of the owning design prints every float4 component with the parameterless invariant format in every era, and texel components are never negative, so the negative value wrap of the historical era never applies. One fixture on era Thry2743, the installed era, covers the reciprocal rule. The unlock window, the dry run, and the build share one reproduction entry, so one rule change covers all three callers. The dry run keeps deferring locked slots. Its deferred sentence does not change.

## TX6. Sequencing and the snapshot measurement

The snapshot measurement precedes the rule change. The scratch probe runs on the reconstruction entry and records which value snapshot the build refuses on and why. The amendment ships only after that record shows the refusing snapshot resolves the same texture identity that the locked material carries. If the record shows a different identity, the anomaly is a defect of its own, and this design stops at the identity fact. The recovery of the measured coverage stays an expectation until a locked cycle after the change runs clean.

## Cutover

Deleted with the equality: `ExpectedTexelSize`, `InvertFloat`, and the texel refusal sentence constant. Kept: `TryTextureOnU`, because the snapshot identity fact uses its rename mapping, and the texture value size fields, because the dry run plan view reads them. The existing differing size characterization test is rewritten as falsifier 1 and keeps refusing under the new rule. The test's local copy of the removed sentence goes with the constant. `CHANGELOG.md` gains one entry for the shipped refusal behavior change without avatar specific counts.

## Review

Two independent adversarial reviews ran in parallel against the first revision on 2026-11-05. One review attacked soundness and one attacked test strategy and blast radius. Both found blocking findings and both were integrated. The soundness review found that the build refusal is an anomaly the equality alone cannot classify, which produced the snapshot identity fact in TX3 and the sequencing in TX6. The blast radius review found the vacuous empty texture falsifier, the missing cutover list, and the wrong sentence mapping. Two further plan reviews in parallel found that the restore delegate seam cannot carry the locked original without widening, that the gate needs a same cycle scene capture, and that the rename aware read must stay. A second round of plan reviews in parallel found that the scene capture routes through the clone were mechanically dead, that the sequence order join was not alignable, and that the gate failed open on unknown slots. Their amendments produced the swap-in hook capture keyed by material identity, the identity key join with exact sentence counts, and the fail closed gate rule. This revision is design acceptance pending the TX6 measurement.
