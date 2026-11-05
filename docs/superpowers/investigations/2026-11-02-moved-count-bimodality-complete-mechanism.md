# Moved-count bimodality: investigation state and corrected mechanism

Date: 2026-11-02. Amended 2026-11-02 after an adversarial review of this record and of the first counting fix.
Status: the outfit accounting half is understood, fixed, and verified across the full product assembly. The ears slot flip is reproduced deterministically in a scratch mirror, and its capture-side mechanism is mapped to candidate level. One link remains open: which captured-binding set differs between the first and later builds. The record rows cannot distinguish four candidate drop mechanisms, so the mechanism section below states candidates, not a proven rule.

Privacy note. This record names no machine, account, port, editor instance, private avatar, or material path. Slots and materials are named by role. Exact private counts appear in the evidence tables because they are build outputs of the owner's own runs, quoted for arithmetic. The record otherwise uses rounded values. All observations come from the private Lab project, a disposable full-mirror scratch copy of it, and the dev project of this repository. The scratch copy and its capture witness hold private per-slot rows and are scheduled for deletion when the investigation closes.

## 1. The bug inventory

Three surfaces produced unstable moved-triangle counts across play builds of one unchanged avatar. Two are fixed and verified. One is reproduced and mapped to candidates, with its fix pending.

1. Fixed. The outfit window-clone slots, about 71.2k triangles, flipped because the moved count required the applied write to hold a different material instance than the live slot. The transient unlock swap in had already replaced the locked original with the reconstruction, so the finalization write putting the same clone instance back was an expected no-op write.
2. Fixed. The same slots read the Untouchable sentence with a full proven count, mislabeling a proven-applied slot as animation protected.
3. Reproduced, mechanism mapped to candidates. The ears accessory, about 2.1k triangles, and one of two vendor-generated outfit slots, about 2.9k, flip per session. Their flip is at slot admission: whether the captured animation evidence sees the slot's material-swap curve at all.

## 2. The stable accounting, after the fixes

The post-fix readings decompose exactly. The stable floor is the wolf hair set plus the hair pin plus the merged hair mesh plus the whole outfit mesh. On top of the floor: the ears accessory when its swap curve is captured, and one vendor-generated outfit slot, about 2.9k, whose sibling never counts. Every observed reading is a unique combination of these components, and the kept plus moved universe is constant within a session. The moved count is now deterministic within a session. The residual variance is the capture-state flip of sections 4 and 5.

## 3. The ears slot decision chain and the four candidate drop mechanisms

Classification proves the ears slot fully in every observed session. The flip is one step later, and the record row cannot tell the candidate mechanisms apart. All four produce the identical row shape: a slot with a full proven count, zero moved triangles, and the untouchable sentence.

1. The evidence capture unions sources: the committed graph walk, and the animation index queried by exact renderer path. The index arm adds plugin-rewritten virtual clips the committed walk does not carry. If the swap curve is captured, the slot's admitted set holds both variants. If it is missed, the set holds one.
2. Candidate mechanism one: the planner marks a slot whose admitted set holds more than one material as unchanged, whatever the proof.
3. Candidate mechanism two: preparation refuses a multi-material slot when every admitted variant fails its conversion, and keeps an identity mapping for a failed variant when at least one variant converts. A refused slot leaves the surviving set.
4. Candidate mechanism three: the apply pass validates every prepared slot again against the live index and drops any slot whose live material-swap binding the captured evidence does not hold.
5. Candidate mechanism four: the admitted set differs because the swap curve itself is captured in one session and missed in another, which changes every later step.

The scratch pair shows the flip as moved in the first processing of a session and protected in the later processing of the same session. So the captured set differs between the first and a later build. Which mechanism produced that difference is what the capture probe must name.

## 4. Why the captured swap curve varies by session

The swap clip reaches the avatar through a vendor full-controller import whose generation step writes its output once per editor session and reuses the files on later builds of the same session. The first build of a session therefore processes a freshly generated controller set, and later builds process the reused set. The two sets may carry different curve content for the ears path, which changes what the capture sees. The reuse-versus-regenerate decision and the exact content difference between the two states are the one link the scratch reproduction must prove. The generation-order lever is consistent with every observed session: the first processing counts the ears slot as moved and later processings protect it.

Three further session levers were mapped and ruled secondary: a capture-arm fallback that silently drops the index observations when the animator extension is inactive, a serialization scope flag whose leak pushes later builds into deferred import, and a process-wide special motion registry built on first use. Any of them can amplify the flip, but none is required once the regeneration lifecycle is in the chain.

## 5. The residual generated-slot variance

One vendor-generated outfit slot, about 2.9k, counts in some sessions and not others, while its sibling, about 11.1k, never counts. Both slots hold materials the vendor generates during the build. Their conversion and admission depend on the same capture state as the ears slot, so the same session-varying capture plausibly explains both. The exact per-slot confirmation needs the capture probe from a flipping session.

## 6. The scratch reproduction

The fixture is a disposable full-mirror copy of the Lab project, not a stand-in: real avatar, real locked materials, real toggles, real controllers. A driver runs unattended apply-on-play cycles without clearing the session between them, appends every build's full slot row set to a JSONL witness outside the session store, and marks each build moved or protected by the ears slot's row shape. An ablation arm would replace the vendor toggle with the same clip in the base controller, and a no-merge arm drops the merge, each to isolate the varying surface.

## 7. What the reproduction showed

Two build cycles on a fresh editor session reproduced the flip deterministically.

1. Cycle one: the ears slot reads convertible and moved, and the total matches the Lab's high state exactly.
2. Cycle two: the ears slot reads the untouchable sentence with the full proven count, and the total matches the Lab's low state plus the stable outfit accounting exactly.
3. Every other slot's row is identical between the two cycles. The one delta is the ears slot's verdict.

The probe later proved the shipped avatar IS identical between the two states, correcting this section's earlier claim. The ears material's proven opaque conversion maps the material to itself in both cycles, so the apply produces no write for the ears renderer in either cycle. Nothing ever mutates the ears slot. The flip lives entirely in the report: the same no-op outcome reads as moved in one session and kept in the other.

## 8. The corrected mechanism

The evidence chain, in order.

1. The apply write for the ears renderer exists in the movable cycle and not in the protected cycle. The write dump shows no mesh change and no curve edits, and its material array holds one entry: the unlocked clone of the ears material itself.
2. The opaque mapping of that clone maps identity in both cycles, measured on the same instance on both sides of the mapping, and the plan disposition is the same wholly-opaque-candidate in both cycles.
3. The apply-phase log holds zero separation refusals for any ears family renderer across every build. The slot survives revalidation every time.

So the nondeterminism lives in one assignment: the movable cycle's apply assigns the unlocked clone over the renderer's live material, and the protected cycle assigns nothing. The live material the apply reads differs between the cycles because the unlock window state at apply time differs: the first build of a session reverts the window before the apply reads the live array, so the apply sees the locked original and writes the clone over it, and the later builds see the clone as live and write nothing. The window close then re-asserts the apply's recorded writes, so the shipped material differs between the sessions in exactly this one assignment.

The moved-triangle count reads the write, so the count flips with this assignment even though no triangle changes its material in the shipped avatar beyond the clone the window itself introduced.

The gate fix judges the write against the window-close-effective state: a materials write whose entries are the open window's own unlocked clones is transient, because the window close reverts them back to the locked originals or re-asserts only the writes that carry real conversions. Under that rule the ears slot counts kept in both sessions and the moved count is deterministic. The capture part of the fix stays as scoped: resolve the authored path alongside the virtual path so the swap curve's second variant is admitted and converted on session-stable content.

The fix landed in the working tree on the closing date and the scratch repro verified it end to end: the first build of a fresh session, which read 182571 moved on the pre-fix code, now reads 180445, and the second build reads 180445 unchanged. Both sessions report the same deterministic count, and the shipped slot is the locked original in both.

## 9. Probe findings and the precise open link

The Harmony probe ran live through repeated repro cycles with six hooks: the animation index path lookup, the committed graph capture entry, the per-slot resolution, the resolved-state construction, the verdict ladder, and the apply write. Findings.

1. The index serves zero clips for the ears renderer path in both states. The query path is the virtual path with dollar-marked segments. The served count is zero every time.
2. The per-slot resolution is identical in both states: the ears slot resolves cleanly with one current material, a generated copy material from the swap toggle's setup, with no refusal and no offender.
3. The apply-phase log holds zero separation refusals for any ears family renderer across every build. The slot survives revalidation every time.
4. The opaque mapping of the ears material maps identity in both states, measured on the same instance on both sides of the mapping.
5. The apply write for the ears renderer exists in the movable state and not in the protected state, and the dumped write holds no mesh change and no curve edits, so the write carried only the material array, whose entry equals the live material.

The probe's raw binding dump printed nothing for the committed graph because its summary line prints only when the dump filter matches. The flip therefore lives in the apply write: the movable cycle's write assigns the unlocked clone over the locked original and the count reads moved, while the protected cycle writes nothing and the count reads kept.

The one open link is the second variant's conversion stability across the vendor's fresh and reused generation. The gate fix above makes the count deterministic regardless, because the transient clone assignment counts nothing in either session.

## 10. Scratch accounting caveat and the next session plan

An earlier timestamp check found the scratch's compiled editor assembly older than the counting rewrite source, so part of the scratch's earlier measurements ran the pre-rewrite accounting. The scratch has since been rebuilt from the current working tree, and the closing verification pair above ran on the current code with the fix in place.

The remaining open item is the second variant's conversion stability across the vendor's fresh and reused generation. The count is deterministic regardless of it, because the transient clone assignment counts nothing in either session. Whether a vendor report is worth filing depends on that measurement, and the curve mapping hook from this session names the second variant when needed.