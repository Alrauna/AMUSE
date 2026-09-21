# Poiyomi locked materials - solution paths

## Status and scope

Investigation record. Dated 2026-09-19. Branch `investigate/poiyomi-locked-materials`, based on `main` at `a3f9dca`. This note makes no production change. It records six candidate paths, the review verdicts on each, and a staged recommendation. The companion note `2026-09-19-poiyomi-locked-materials-characterization.md` records the verified lock facts this note builds on.

Controller decisions of 2026-09-19: scope is the Thry lock on Poiyomi Toon only. Ground truth comes from pinned sources first, with a throwaway editor project only if a load-bearing question cannot settle from source. Deliverable is this pair of investigation notes, with no design spec until a path is chosen.

Owner direction, recorded 2026-09-19 after review: transient unlock, path 3, is the owner's preferred forward path. The staged order in section 8 remains the recorded recommendation. The preference moves path 3 up the roadmap subject to its named conditions: the live two-pin restore characterization, consent gating, and the disclosed order-100 re-lock dependency. The build-caching note `2026-09-19-amuse-build-caching.md` records the cache seam that should land with or before it.

Method. Six designers worked in parallel, one path each, on the verified phase-1 facts. Two independent reviewers then checked feasibility against the working tree and vision alignment against `docs/architecture/vision.md`. Every complexity figure below is a relative design estimate from review, not a measured cost. Every coverage figure is a design-time hypothesis, not an observation.

## 1. The spread at a glance

Review ranked the six paths by implementation cost and by per-build editor cost. The order below runs from lowest implementation complexity toward maximum coverage and evidence strength.

| # | Path | Mechanism in one line | Implementation cost | Per-build cost | Coverage of locked materials | Vision verdict |
|---|---|---|---|---|---|---|
| 1 | Floor | Detect, name, and degrade per slot | Lowest | Negligible | None by design | Aligned |
| 2 | Tag-attested frontend | Attest the lock by tags plus the original digest, read retained serialization | Medium | Low, memoized | Unlocked-grade on supported eras | Aligned |
| 3 | Transient unlock | Unlock the build copy, reuse the unlocked frontend, let the pipeline re-lock | Low to medium | Low, plus a reflection seam | Unlocked-grade, widest per cost | Strained |
| 4 | GPU black box | Render the locked material, prove alpha from observation | Medium-high | Highest of the six, still sub-second for small sets | Conservative, drift-immune | Aligned |
| 5 | Source attestation | Attest the generated source structurally, extract baked truth by alignment | Highest | Low once cached | Proof-grade, era-bound | Aligned |
| 6 | Tiered pipeline | Ordered tiers over paths 1 to 5 with consent governance | Glue over the others | Sum of the used tiers | Maximum, governed | Aligned |

Risk asymmetry is the sharpest portfolio fact. Paths 1, 2, 4, 5, and 6 fail toward refusal. Only path 3 can make a build worse than refusal, because it ships the build copy unlocked and depends on Thry's order-100 re-lock.

## 2. Path 1 - floor: detect, name, and degrade

Teach family selection about locked Poiyomi. A `Hidden/Locked/` name plus the Thry tag set produces a named locked family member instead of the generic sentinel. Diagnostics carry the original shader identity and the lock era. The current per-slot degradation stays. No semantic claims, no optimization of locked materials.

Verified seams: `ClassifyShaderName` in `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`, the sentinel branch in `Editor/Host/UnityAnimationEvidenceCapture.cs`, the refusal enums in `Editor/Host/UnityRendererAlphaAnalysis.cs`, and the report string tables. Review found no load-bearing defects. Feasibility verdict: viable.

The shared record from this path, a captured locked-Poiyomi identity holding the original GUID, name, rename suffix, and era, is the substrate every later path consumes. Review advised one enum decision before any code: either a dedicated refusal member that separates a known locked refusal from generic Unknown, or report-layer naming over the existing member. Exactly one spelling should ship.

What it buys: honest reporting and a correct next action for the user. What it cannot do: optimize any locked material.

## 3. Path 2 - tag-attested frontend: serialization trust with a pinned origin

Admit locked Poiyomi as a third attested identity. Attestation: `Hidden/Locked/` name, `_ShaderOptimizerEnabled` equal to 1, `OriginalShaderGUID` resolving to an installed shader asset that passes the existing 9.3.64 name, GUID, package, and digest pins, required Thry tags present, existing property schema present. Semantics: read the alpha facts from the retained serialization exactly as the unlocked frontend does, with the lock model applied. Untagged properties are constants, so state admission gets simpler. Tag-1 properties animate under their original name. Tag-2 properties animate under the suffixed name and need a rename map in the animation closure. Render state reads from live material values, which stay property-bound after a lock. Stripped textures read as unassigned.

Feasibility verdict: conditional. Defects to fix during implementation:

- Era-1 locks never write `thry_locked_rename_suffix`. A literal selection requirement on that tag zeroes coverage for the dominant installed base. The suffix policy per era must be decided, fail-closed or with the verified material-name fallback.
- For tag-2 properties outside the illegal-rename duplicate list, the generated Properties block no longer declares the original name. The capture route is shader-declaration-driven. The rename coverage promise needs a capture check, or suffixed names must join the locked capture schema.
- The selection pre-check changes the documented selection contract. The contract text and its consumers need a re-audit.

Consent: untested lock eras extend the per-build consent pattern, keyed on the original shader GUID. A grant never makes a version tested.

This path delivers most of the coverage win at a fraction of the cost of path 5. Review noted the two are one roadmap with two trust tiers, not competing options.

## 4. Path 3 - transient unlock: reuse the unlocked frontend

At PlatformFinish, unlock each locked material on a build-copy clone only, through Thry's restore surface behind a version-pinned reflection seam. Run the existing unlocked 9.3.64 frontend and conversion with zero new property logic. Then let the order-100 upload lock re-lock the build, or re-lock actively with the same reflected surface VRCFury already uses.

Feasibility verdict: conditional, and the only vision-strained path. The load-bearing facts:

- The required end-state condition, the order-100 re-lock reproducing a locked build, is outside AMUSE control. Poiyomi issue 64 records silent lock failures in the field with no public root cause. If the re-lock fails, the stripper deletes all variants of the unlocked Thry shader and the avatar ships fallback. On 2026-09-19 that blast radius hits already-unlocked materials. With this path it would extend to every AMUSE-analyzed locked material. Consent must disclose this dependency by name.
- A partial restore shape, prefix cleared while the marker stays 1, would put the clone into the closed batch and produce a renderer-wide closure failure. Post-restore verification must check the full state pair and route the failure to a named refusal.
- Play-mode builds must keep the 2026-09-19 behavior, so the pass is disabled there. Review verified the lifecycle build-path gate supports this.
- Restore fidelity is verified line by line at both pins but never executed. A live two-pin characterization is a prerequisite before any consent default changes.

In-build active re-lock was evaluated and rejected. The only complete lock path uses Thry's asset-editing batch with an unconditional `AssetDatabase.Refresh`, the exact mid-build hazard VRCFury documents.

This path buys the largest coverage per unit cost, and it is the only path whose vendor knowledge would live outside the Poiyomi frontend. Review recommends sequencing it last and strictly behind consent.

## 5. Path 4 - GPU black box: observation-only proof

Do not read any vendor property, tag, or source. Render the locked material over each triangle's UV domain through a material-level route beside the existing texture-level GPU evidence core in `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs`. Read back the alpha field. Classify triangles from observed alpha. The render is the ground truth, so baked branch state, alpha masks, and UV transforms are absorbed without any vendor-format pin.

The apply step uses a verified clone: AMUSE flips the blend-state properties on its own NDMF clone and keeps the locked shader program, which preserves everything the probes observed, including screen-UV-static effects. The revalidation probe re-runs on the clone before apply.

Feasibility verdict: conditional. Defects to fix: the design's name parser matched a wrong segment shape, and must use the verified multi-segment form `Hidden/Locked/<originalName>/<id>`; the probe scales must not reuse the texture-mip policy controls and need their own policy surface.

Stated evidence bounds, for consent wording: probes observe two backgrounds, sampled times, probed views, and pass isolation only to probe resolution. Time-sampling aliasing shrinks but never reaches zero. Animated, audio-link, and view-dependent materials stay Unknown by design. The classification inherits the existing mip-4 and minimum-size alpha policy precedent. Per-build cost is the highest of the six, roughly tens of milliseconds of probe work per locked material, still sub-second for small sets.

This is the only path that survives arbitrary Thry and Poiyomi drift, because it reads nothing vendor-owned. That makes it the natural fallback tier.

## 6. Path 5 - source attestation: structural fingerprint plus baked-truth alignment

Treat the generated locked source as a second evidence corpus. Attest it structurally: original GUID plus the existing digest pins, lock-era fingerprint, structural invariants such as the retained Properties block, the `OPTIMIZER_ENABLED` define, the lock-button default rewritten to 1, and the keyword define set equal to `OriginalKeywords`. Extract baked values by aligning the generated text against the digest-pinned canonical source, because the lock substitutes literals at use sites and property names vanish from the body. Every divergence must classify as one verified transform form. Anything else refuses. A baked value is accepted only when every occurrence of the token prints the same literal.

The policy recommendation from design and review: baked-parsed values are primary for baked facts, because post-lock edits of serialized values are render-inert. Serialization stays primary for property-bound render state, renamed-animated properties, and textures. Divergence becomes a recorded diagnostic, never a refusal, since refusing would be a coverage defect.

Feasibility verdict: conditional. The central defect: the prefix branch as designed routes every locked material into the name-classified closed batch, so an attestation failure produces a renderer-wide closure failure instead of the per-slot behavior the design claims. The selection pre-check pattern from path 2 repairs this and must be adopted. One additional defect: the rename-suffix tag is requested but gated inconsistently.

This is the most aggressive reading tool in the repository and the most expensive to maintain, with writer-grammar re-pins per optimizer release. Its staged verdict is the honest one: land the reduced variant together with path 2, and build the full aligner when divergence diagnostics or policy demand proof-grade values. The aligner also doubles as the instrument that measures the serialization-equals-bake assumption.

## 7. Path 6 - tiered pipeline: the integration home

One resolver walks ordered tiers per material. Tier 0 is the shipped unlocked attestation, the 2026-09-19 behavior. Tier 1 is path 2. Tier 2 is path 3, consent-gated and play-mode-gated. Tier 3 is path 4, consent-gated. The floor is path 1 with the shared identity record. Consent extends the existing per-build dialog with a structured grant key holding the original shader GUID, the mechanism, and a scope string. Revalidation recomputes the grant key on the live target. A mismatch re-resolves, and a downgrade withdraws only the dependent slots.

Feasibility verdict: conditional on inheriting the fixes of the paths it composes. Defects found: Tier 1 keyword-driven alpha admission contradicts the verified float-driven alpha gates, so keywords stay attestation-only at every tier on 9.3.64. Upward re-resolution must be bounded by the grant keys the user actually gave in this build. Tier 2 must inherit the play-mode disable. The surviving-candidate check is a filter as of 2026-09-19, so grant-key revalidation is new logic placed there, not an existing behavior.

Its value is architectural: each mechanism's version cliff becomes graceful degradation toward a floor that never lies. Review ranks it the largest total only when counted standalone. Its incremental cost over already-shipped tiers is small.

## 8. Recommendation

Stage the work in this order.

1. Ship the floor, path 1. It is fully verified, has zero behavioral risk, fixes the mislabeled refusal the characterization note records for 2026-09-19, and produces the identity record every later path consumes. Decide the refusal-enum spelling once, first.
2. Ship path 2 with its selection pre-check, the per-era suffix policy, and the tag-2 capture check. This is the main coverage increment at bounded cost.
3. Build path 4's observation harness as the drift-resilient fallback tier. Keep the verified-clone flip behind its own decision, since it is the one place this otherwise read-only path writes vendor-owned property names.
4. Add path 5's aligner when divergence data or policy demand proof-grade values.
5. Add path 3 last, strictly behind per-build consent, only after a live two-pin restore characterization, and with the order-100 re-lock dependency named in the consent subject.
6. Introduce the path 6 resolver once at least two tiers exist. Before that, each shipped path stands alone through the same seams.

## 9. Guards against overclaim

- Coverage figures in this note are design hypotheses. The characterization note contains no measured avatar results.
- Consent wording must never present an untested host as tested, and must name the order-100 dependency for path 3.
- The lock is a reversible transform with no encryption. Neither a circumvention nor a protection framing is accurate. Describe the mechanism neutrally.
- Material-value retention claims are era-qualified. 9.3.64 locks deleted null-texture texenv entries.
- Stripper immunity is a property predicate, not a name. Any AMUSE-generated shader must declare neither Thry marker property, or must default the lock property to 1.
- The locked-name shape is `Hidden/Locked/<originalName>/<id>` with a multi-segment original name. One central parser should own it.

## 10. Sources

- The verified facts in `2026-09-19-poiyomi-locked-materials-characterization.md`, sections 1 to 10.
- The six path designs and two review verdicts from the 2026-09-19 research session. Session artifacts are not durable references. The durable content is this note and its companion.
- `docs/architecture/vision.md`, sections on transformation contracts, refusal scope, version assumptions, consent, and shader-specific knowledge.
