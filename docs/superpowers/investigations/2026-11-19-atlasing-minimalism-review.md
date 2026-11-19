# Minimal 0.2.0 atlasing review

Privacy note: This record contains public product design review only. It names no private avatar, machine, instance, port, or vendor source text.

Date: 2026-11-19.
Status on 2026-11-19: Read-only review by an isolated reviewer. It changed no other file. No Unity, build, or test run happened. Every claim comes from reading the design, the plan, the handoff, the spikes, and product source. A claim marked [INFERENCE] has no run evidence. Statements about product behavior describe the source at branch main on 2026-11-19.
Outcome on 2026-11-19: The user accepted the order change, all deferrals except the second shader family, the component-only presets, and Play-mode atlasing under the alpha separation gate. The controlling result is `docs/superpowers/specs/2026-11-19-minimal-atlasing-design.md`.

Inputs read in full: the design (sections 1 to 17), the plan, the handoff, and all atlasing spikes. Product code read: `Editor/Build/AlphaSeparationRecords.cs`, `AmusePlatformFinishPlugin.cs`, the barrier gate in `AmusePlatformFinishPass.cs`, `Editor/Host/UnityStreamingTextureEvidence.cs`, `CapturedMaterialEvidence.cs`, `MaterialEvidenceRequest.cs`, `Editor/Analysis/AdmittedMaterialStates.cs`, `Presets/PresetParser.cs`, `Build/PlayModeBuildStore.cs`, `Runtime/AmuseAvatarOptimizer.cs`, and the file list of `Editor/{Build,Host,Analysis,Semantics,DryRun}/`.

Short verdict: The design is not tangled by accident. One decision causes most of the tangle. It is the order "atlas first, alpha after atlas" (answers 52 and 53). That order forces the design to run the alpha proof on AMUSE-made textures. Almost every blocker of the 2026-11-19 reviews sits on that path. Run the atlas stage after the existing alpha separator and most of that path disappears. The rest of the cuts are deferrals of opt-in or unproven features. Their absence changes nothing for a default user.

---

## 1. The minimal 0.2.0 core

Data flow in one line: live avatar after the alpha separator, then closed per-use evidence (static material, enumerated textures, route table row), then pure group, region, and placement decisions, then owned page and mesh output, then one write with an identity re-check, then one report line per page set.

What 0.2.0 must contain:

1. One new stage that runs after the existing alpha separator and after `TransientUnlockWindowClose`. It reads the live avatar. It never calls into separator code. The separator never calls into it (section 5).
2. A route table per attested shader row. Each row lists the texture properties that the route moves (members), the properties that are known not to read the route channel (neutral), and the features that read UV values as data (bans). An assigned texture or an enabled feature that is on no list refuses the use. One family and one role (Color) in the first merge.
3. A use is admitted only when its slot has exactly one static material, no property block entry on a route property, no animated route property, an asset-backed texture, an unlocked shader, and UV inside the unit square. Everything else is one named refusal per use. Reuse `CapturedMaterialSlotEvidence` and `AnimatedPropertyAdmission` for the animation facts.
4. One group key per use: shader row, route, role signature, sampler facts, and a generic whole-material key. A page set is the set of pages for one group. The group key is also the merge-ready check.
5. One region per use: the bounding rectangle of the used UV. Exact-key sharing of identical regions. One deterministic packer with a finite page ladder and the existing sort order. All regions of one use go on one page, or the use stays unatlased.
6. One border function (protected mip, filter, anisotropy level, block size). Protected levels copy from the source chain. The unprotected tail comes from Unity mip generation of the page [INFERENCE, needs one fixture].
7. Acquisition through the existing owned import copy route. One output format function by role and by "source has alpha". Alpha is never dropped.
8. Mesh output: clone the mesh, duplicate shared vertices, rewrite the route channel in place as Float32, keep every other attribute, bone weight, and blend shape. Material output: one AMUSE-owned clone per atlased use with page textures and identity tiling.
9. One budget with three byte numbers (group retained, avatar retained, temporary). Numeric values come from release measurement. Tests inject limits through one delegate.
10. Three serialized fields (enabled, maximum page size, protected mip), default-on through field initializers and Reset. No preset change. One named-refusal enum modeled on `AlphaSeparationSlotRefusal`. One report line per page set and one per refusal.

Not in 0.2.0: visibility groups, material swaps, density normalization, separate alpha mask, strict mode, compression matrix, connected-component regions, spare channels, generated-texture acquisition, locked sources, alpha analysis of atlas output.

---

## 2. Findings

Labels: DELETE, DEFER (to a later version, with the door kept open), MERGE, SIMPLIFY, KEEP-BUT-FIX. A "USER DECISION CHALLENGE" marks a place where a recorded user decision causes concrete harm.

### DELETE

**D1. USER DECISION CHALLENGE (answers 52 and 53 of 2026-11-16): Delete the in-atlas alpha pipeline. Run atlas after the separator.**

Design sections: 3 steps 5 to 8, 6, 8, 15.3, 17.2 items 1, 10, 11, 12, 13, 15, 18, 20. Plan: Gate G0.7 and Milestone 3.

What it is: The design applies atlas first. It then analyzes final compressed AMUSE pages with the alpha predicate. It proposes one separate candidate per selection unit, one shared fallback, a growth account, removal to a fixed point, and a fallback that restores sources without alpha separation.

Why it is excess, with concrete cases:

- A false opaque path comes from the order itself. Item 1 records that `UnityTextureEvidence.TryProveSampledAlphaIsOne` proves alpha one when the format has no alpha channel. The chain capture admits DXT1 and reads 255 on every texel. The throwaway spike section 5 shows the case. A constant alpha of 96 became 255 after DXT1 and the predicate reported opaque. Item 1 needs an evidence identity for AMUSE pages and two guards. No page enters the alpha capture if the separator already ran.
- The separate candidate has no measured benefit. Spike section 5 measured 174816 bytes for separate pages and 174800 bytes for the shared page. Design sections 1 and 6 say alpha removal is optional and atlasing alone saves no draws.
- The plan ships a regression first. Plan section 5 (Milestone 2) says "No alpha separation on atlased uses". With atlasing default-on, every atlased use loses the shipped 0.1.0 separator until Milestone 3 ends. Item 11 accepts the same loss for every apply-time removal.
- Item 11 requires splitting classification from preparation inside `AlphaSeparationPreparation.Prepare` (898 lines) and `AmusePlatformFinishPass.Execute` (1369 lines). That edits the shipped proof path.
- The plan wants to replace `PreparedAlphaSeparation.OpaqueBySource` (`Editor/Build/AlphaSeparationRecords.cs`, lines near 185 to 261). That is a rewrite of the separator output record.
- Handoff section 3 states that the candidate and fallback lifecycle is an engineering proposal and not a user decision.

Smaller replacement: Atlas runs on the avatar the separator leaves behind. The separator proves opacity on source evidence, as it does on 2026-11-19. The opaque clone ignores alpha, so an atlas cannot make it wrong. The remaining transparent or cutout slots keep alpha in their pages. Section 10 already accepts mip and compression changes there. The opaque slot and the transparent slot have different render state. Their group keys differ. So they get separate page sets with no extra mechanism. Answer 53 ("prefer separate pages when supported, share only where necessary") holds in full, and sharing is never necessary.

Risk: I found no case where a post-atlas proof proves more than the source proof [INFERENCE]. Ask the user for such a case. If the user keeps answer 52, take option B in section 5. Option B costs the evidence identity work and loses separation on atlased uses, but it adds no new proof.

**D2. USER DECISION CHALLENGE (handoff section 3, "Keep strict sampling opt-in"): Delete strict sampling from 0.2.0.**

Design sections 10, 15.2, 17.2 item 17. Plan Milestone 3 bullet "Add the strict-mode path only if Gate 0 supplied a useful strict case".

Why: Item 17 states that no useful strict case fits the allowed detection rules. The one positive case (follow-up spike section 5) needs a periodicity search that the design forbids. The one-texel final level cannot keep two constants (design 10, follow-up spike section 5). A control that leaves most uses unatlased is a trap for the user who turns it on. It is opt-in, so default users see no change.

Replacement: Nothing in 0.2.0. The border function takes a protected level count. Strict later means "all levels". Risk: low. Item 17 itself says removal needs a user decision, so ask.

**D3. Delete the settings snapshot class (plan Milestone 1, `Editor/Analysis/OptimizerSettingsSnapshot.cs`).**

Why: Foundation section 6 found three enumeration sites. They are `DryRun/AlphaSeparationDryRun.cs` (near line 395), `Build/AmusePlatformFinishPass.cs` (near line 471), and `Build/PlayModeBuildStore.cs` `ComputePolicyFingerprint` (near lines 308 to 333). After the cuts in D4 and F3 the atlas adds three fields. Adding three fields to three explicit lists is smaller than a new type that those same three sites must also consume.

Replacement: Add the fields to the three lists. Keep the existing fingerprint test pattern. Add one test that changes each new field and expects a new fingerprint. Risk: the old drift risk stays. It is not worse.

**D4. USER DECISION CHALLENGE (design 17.1, "Presets", 2026-11-19): Delete preset atlas keys, the schema bump, and the preset migration from 0.2.0.**

Evidence: The decision says shipped presets carry atlas fields at the global default as a placeholder. A placeholder equal to the default carries no information. The plan Milestone 1 test says a new-schema preset "applies its atlas values". That overwrites a user's explicit atlas choice with the default. `PresetParser.cs` (lines 18 to 45) is closed to unknown keys and to other schema versions. A key addition therefore forces schema 6, a version branch, and refusal tests. `OptimizerPreset.Matches` (line 77) compares only separator values on 2026-11-19, so presets stay valid without atlas keys.

Replacement: The atlas fields are component-only in 0.2.0. Preset files stay schema 5 and need no migration. When the user chooses per-preset atlas values, add schema 6 then. Risk: the user wanted presets to expose the fields early. Cost of waiting is one later schema bump that is needed anyway.

**D5. Delete the "output evidence identity", the self-proof guards, and cache eviction by instance (items 1 and 18).**

Follows from D1. No AMUSE page enters the alpha capture. Keep one structural test: the separator capture never sees an atlas page, because the stage order forbids it. The dry run reads the scene avatar, so it cannot see build outputs.

**D6. Delete growth-after-removal, realized-allocation overshoot reporting, and post-selection removal to a fixed point (design 12, item 13, plan Milestone 3 bullet).**

In 0.2.0 each use is independent once admitted, and a failed page refuses only its own group before any write. There is no shared candidate state to unwind. A page set that fails refuses its uses before the single write.

**D7. Delete RGB-only conversion and alpha removal as concepts (design 6, 15.3, item 1).**

Rule for 0.2.0: an output keeps alpha when the source texture has an alpha channel. The spike shows alpha removal needs its own proof and is "optional". Door: a later proof can add a smaller format for a named recipe.

**D8. Delete any atlas rows from the dry run.**

Design 11 forbids an interactive atlas simulation. Plan Milestone 7 lists `Editor/DryRun/`. `AlphaSeparationDryRun.cs` is 2087 lines. Only the "scene snapshot versus build result" label stays. No atlas row in `DryRunRowBuilder.cs`.

### DEFER

**F1. Visibility and toggle groups (design 4, plan Milestone 4 first half, G0.4 patterns, `CapturedVisibilityEvidence.cs`). Defer to 0.3.0.**

Why: The design says shared visibility is "a grouping fact, not a storage-sharing mechanism". The only consumer is renderer merging, which is 0.3.0. Texture memory does not change with toggles, because VRChat loads every texture. A page set that spans several toggle groups is still valid. Any later partition of a page set by visibility is a subset that keeps the same pages and the same material key. Subsets are safe. Supersets are not. So splitting later cannot invalidate a 0.2.0 page set.

Door: the page set record keeps its member list. Risk: none for 0.2.0 correctness.

**F2. Material swaps and animated variants (design 7, items 9 partly, plan Milestone 4 second half). Defer to 0.2.x.**

0.2.0 admits only slots with one static material and no animated route property. Use the existing `CapturedAnimationEvidence` and `CapturedMaterialSlotEvidence` (`Editor/Host/CapturedAnimationEvidence.cs`, line 135) and `AnimatedPropertyAdmission`.

Why: This is the feature that forces atlas into the animator-services scope and creates the "one final writer" pressure. Without it, atlas needs no clip edit. It can run as its own pass.

Door: record output per use, not per source material. The existing `AlphaSeparationCurveEdit` and `MapCurve` (`AlphaSeparationApply.cs`, near line 807) give the later retarget code. Coverage cost: outfit toggles by material swap stay unatlased.

**F3. Density normalization (design 9, 15.4, plan Milestone 5). Defer to 0.2.x.**

Why: The control is opt-in and defaults off, so a default user sees no difference. It adds area measurement, a group target, a placement scale, the dimension rounding rule (item 15), the reference map rule, and the unusable-measurement rule.

Door: image preparation takes a target size per region. 0.2.0 passes the acquired size.

**F4. "Allow Separate Alpha Mask Generation" (design 10). Defer with F3.**

Why: Its purpose, in design 10, is to keep alpha detail while color is downsampled. Without normalization nothing is downsampled. It also needs per-vendor recipes and the mask product falsifiers (vendor role contracts section 4).

**F5. Compression matrix, overrides, and automatic compression fallback (design 10 decisions, plan Milestone 7). Defer the controls. Keep one internal function.**

Why: The matrix is four roles by three material kinds, twelve cells. Plan G0.6 asks to "count the compression-matrix cells that offer a real choice". The first format candidates are unapproved (design 15.5). The normal and mask spikes show BC3 and BC7 limits. A replacement format also needs a second encode and temporary-work accounting.

Replacement: `OutputFormat Select(role, sourceHasAlpha)` in code. The format table is chosen in Gate 0 from the quality evidence. Door: the later matrix overrides that same function.

**F6. Connected-component regions, whole-island offset removal, repeated spans, partial overlap sharing (design 6, 15.1, plan Milestone 3 first bullet). Defer to 0.2.x.**

0.2.0 region: the bounding rectangle of the used UV of one use. UV outside the unit square refuses the use as "tiled". Sharing: exact key of texture identity, interpretation, sampler, and rectangle.

Why: Component adjacency, label-boundary cuts, fragmentation bounds, and the thin-triangle and gradient-cut falsifiers all come from this one feature. Plan G0.2 lists them. A rectangle also satisfies "regions of used UV, not the whole image".

Door: the layout input is a list of regions. A later region producer replaces the rectangle producer.

**F7. Spare UV channel allocation and channel liveness (design 5, G0.4, Milestone 6). Defer to 0.2.x.**

0.2.0 rewrites the route's own channel in place. This needs a positive list of what reads that channel, and the route table gives it (S1). A spare channel needs a negative proof that nothing reads it. Foundation sections 5 and 10 say an empty array proves nothing and that native Poiyomi 10 channel stripping does not apply to bundled 9.3.

Door: the route table row names a channel. A later row can name a spare one.

**F8. Generated texture acquisition (no asset path) (design 10 acquisition). Defer to 0.2.x.**

0.2.0 admits asset-backed textures only. Others get a named refusal. The decision itself says "otherwise preserve". Evidence: feasibility review section 4 states no full-pixel capture of generated input is established. The spike covered two constant RGBA cases. An atlas of an AAO atlas gains nothing.

**F9. Roles beyond Color: Normals, Emission, Masks. Defer one role per step.**

0.2.0 proves the route table and the packer with Color. A material with an assigned normal map that is on no list refuses by closure (S1). Evidence: the normal and vendor render spikes state "no tolerance or vendor recipe is approved" and show strength-order and compression limits. Each later role is one table row plus one real-shader fixture. This is a judgment call. If the user wants Normals in 0.2.0, add it as the first row after Color.

**F10. Locked sources and slots from the transient unlock window. Defer to 0.2.x.**

The relock spike section 7 lists "A slot from the transient unlock window" as untested. The user condition says the path must be functional. A locked source gets the named refusal "locked source". Keep the cheap live check that an unlocked AMUSE output is locked by the vendor hook during an SDK build (relock spike section 4). That part needs no AMUSE code.

**F11. Multi-vendor GPU anisotropy evidence (G0.2) moves to release notes.**

The user decided on 2026-11-19 that tap differences follow the compression risk acceptance. The border stays an analytic function with one-GPU evidence from the precision spike section 6. The author cannot obtain other vendors' evidence by reading [INFERENCE]. State the one-GPU limit in the release note.

**F12. Second shader family. Defer to 0.2.x.**

0.2.0 claims one family. The second family is the proof that the route table is vendor neutral. Plan Milestone 2 already limits itself to one family.

### MERGE

**M1. One group key for render group, atlas group, page-set key, and merge-ready check.**

Design 3 defines render group, toggle group, atlas group, and page set. Item 7 defines whole-material equality. Plan section 7 says "The merge-ready check is this equality across the page set". It is one relation. Define `AtlasGroupKey` once: shader row, route, role signature, sampler facts, and the whole-material key. Group means equal key. Page set means the pages of a group. The report line per page set is the merge-ready statement.

**M2. One budget.**

Plan G0.3 names retained allocation, peak temporary bytes, cumulative copied pixels, source reads, encodes, predicate reads, variants, pages, and fragmented components. Predicate reads, variants, and fragmented components disappear with D1, F2, and F6. Pages and encodes are bounded by bytes. Cumulative copied pixels follow from bytes. Keep three numbers: group retained bytes, avatar retained bytes, temporary bytes. The user decision asks for a candidate guard and an aggregate guard. They are the first two. Numeric values still come from Milestone 8.

**M3. One mip setting for atlas.**

Design 10 has "coarsest protected mip", "coarsest tested mip", and "minimum tested texture size" for atlas. The tested ones exist only for the alpha proof of atlas output (D1). Keep one stored protected mip. The existing separator controls stay as they are.

**M4. One refusal vocabulary, reusing the existing pattern.**

Design item 12 lists four categories. "Failed final proof" goes with D1. Keep three: reached limit, unsupported recipe, missing host capability. Add one enum `AtlasUseRefusal` next to `AlphaSeparationSlotRefusal`. Report through `AmuseReports` and `AmuseReportStrings`. Invariants throw, as AGENTS requires.

### SIMPLIFY

**S1. Closure by enumeration replaces items 2, 3, 4, 5, and 21.**

Each attested row gets three lists: members (textures moved, with role, transform property, and sampler rule), neutral (texture properties that do not read the route channel), and bans (enabled features that read UV values as data or read page size). Rule: any assigned texture not on the member or neutral list refuses the use. Any enabled feature on the ban list refuses the use. Items 2 to 6 and 21 become data rows plus one test per list entry, which the plan already wants as falsifiers.

Concrete case: a lilToon material with an assigned shadow mask or a Poiyomi material with UDIM discard on. Both are on a list or they refuse. No liveness proof is needed.

Risk: the lists come from source review (design 17.2 item 2 gives the starting content). Unlisted new features cannot appear inside one pinned shader digest.

**S2. One pass, no prepare and apply gap. Item 8 shrinks to an identity check.**

Plan section 3 notes that on 2026-11-19 preparation and apply sit in one animator scope. The revalidation inventory exists because a gap separates them. The atlas stage in 0.2.0 has no animator dependency (F2). Capture, plan, generate, and write run in one synchronous pass. Before the write, compare the renderer's `sharedMesh` reference and `sharedMaterials` array and slot count with the capture. That honors "revalidate immediately before mutation" without a content hash inventory. Risk: a later version that adds animation edits needs the gap back.

**S3. Whole-material key by generic property enumeration (item 7).**

The design lists named facts and one falsifier per fact. Product code already iterates all shader properties: `Build/LockedSourceAlignmentMemo.cs` (lines 147 to 152) and `Host/EffectiveMaterialMaterialization.cs` (near line 318). Build the key from every non-texture property, tags, keywords, disabled passes, queue, instancing, and GI flags by enumeration. Test with a synthetic shader that has each property type. A forgotten fact is then impossible by construction. Risk: the key is stricter than necessary, which only makes more groups. That is safe.

**S4. Mip construction: copy protected levels, let Unity make the tail.**

Design 15.2 asks for a deterministic tail generated from the preceding assembled level. That implies an sRGB-aware CPU downsampler and sRGB, non-power-of-two, and coverage-preserving fixtures (G0.2). Alternative: build the page base, call Unity mip generation, then overwrite protected levels with the source chain levels. Unity handles color space. [INFERENCE: one fixture must confirm that overwriting levels keeps the tail.] Risk: tail differs across Unity versions. The design already accepts tail contamination.

**S5. Build target: reuse the existing gate.**

Design 17.1 asks for "data keyed by build target". The product already gates on `StandaloneWindows64` in `Build/PlatformAlphaSamplingPolicy.cs` (line 27), `Host/UnityAlphaFieldEvidence.cs` (line 915), and `DryRun/AlphaSeparationDryRun.cs` (line 246). Atlas calls the same gate and returns one named refusal for other targets. Adding a target later extends that switch. No new table type.

**S6. Growth accounting is group-local and treats sources as retained.**

Design 8 and 15.3 count source textures still referenced by preserved or unprocessed uses, avatar-wide, in stable order. Avatar-wide reference tracking cannot be proved complete (design 15.3 admits "a missing proof ... means the source remains counted"). Count every distinct source of a group as retained. Guard: page set bytes against a factor of the group's distinct source bytes. Avatar guard: the sum over groups. The factor is a release number from Milestone 8. Risk: the guard refuses some uses that would really save memory. That is the safe direction.

**S7. UV rounding bound as a table over the finite ladder.**

The precision spike (section 4) gives `delta <= c * 2^-24 * (P * max(1,|u_page|) + S * max(1,|u_source|))`. The page ladder is finite and 0.2.0 admits UV in the unit square only (F6). Compute the bound once per ladder size in a fixture and admit by page size and UV range. No per-use numeric evaluation at build time. Float32 UV storage is mandatory (precision spike section 4).

**S8. Acquisition extends the existing clone route.**

`Host/UnityStreamingTextureEvidence.cs` (lines near 237 to 308) already copies the asset to an owned temporary asset, sweeps stale clones, names clones by GUID, and removes them. The throwaway spike section 2 showed that `CopyAsset` plus an uncompressed import setting gives the base level with zero differing bytes for three cases. Reuse the folder, sweep, and naming code. Add a pixel-returning method. Do not write a second clone mechanism.

**S9. Quality check: one local metric per role.**

The user wants a texture-output check per generated image (2026-11-17). Keep one function: maximum per-channel difference in decoded sample space, and angular error for normals later. Four bounds, one per role, set in Gate 0. Drop the "aggregate measure" from G0.1. Design 15.5 says an average cannot hide a local defect, so the local bound is the contract. Normalization loss separation (G0.1) disappears with F3.

**S10. Vertex duplication on raw vertex streams.**

Design 5 requires copying every attribute and keeping skinning and blend shapes. Do it on the raw vertex buffers and the bone and blend shape arrays so a new attribute is never forgotten. [INFERENCE: the Unity raw buffer API supports this on a cloned mesh, and bone weights and blend shape frames need separate arrays.] Test with a mesh that has every attribute. `AlphaSeparationPreparation.cs` (near line 793) already clones with `Object.Instantiate(mesh)`. Start from that.

### KEEP-BUT-FIX

**K1. Default-on atlasing: keep. Considered and rejected a default-off first release.**

The case for default-off: atlasing alone saves no slots and no draws (design 1, 8) and can grow memory. The case against: the migration spike table row "Save and reload" shows Unity writes the initialized value into the saved component. A later flip of the default would not reach components saved in 0.2.0. So default-on now is the only cheap choice. Limit the exposure by the narrow admission in section 1.

**K2. Split gate: no barrier change.**

The barrier stores `state.Lifecycle` and `HasExecuted` before it checks the opt-in component and the separator switch (`AmusePlatformFinishPass.cs`, private `Execute`, starts near line 209). The atlas pass reads `state.Lifecycle`, the structural graph result, and `state.AnimatorBindings` from the shared state. It checks its own switch. The consent dialog stays alpha only. This satisfies design 17.1 "Alpha separator off" without touching separator code. Fix needed: confirm the structural refusal is readable from state without the barrier reporting it.

**K3. Sampler facts.**

Extend `TextureSampling` (`Semantics/MaterialSemantics.cs`, line 143: filter and one wrap field) with wrap per axis, mip bias, and anisotropy level. Item 3 already requires this. Keep. Group key includes it.

**K4. Float32 and vertex format test (item 14).** Keep. Cheap and measured (precision spike section 4: half precision moved samples up to four texels).

**K5. Property block and renderer-wide animation refusal (item 9).** Keep the refusal. A property block entry on a route property refuses that slot. A renderer-wide entry refuses the renderer. This is a small check and a plain false-positive guard.

**K6. Gap: Play mode build path.**

The design and plan never state whether atlasing runs on `AmuseBuildPath.ApplyOnPlay` (used in `AmusePlatformFinishPass.cs` near line 512 and `PlayModeBuildStore.cs`). Every Play entry would encode pages. Add one sentence in Gate 0. The smallest choice is the same gate as the separator (`lifecycle.MayUsePositiveMutation`) with the temporary budget as the only cost control. This is a decision, so ask. The relock spike section 3 item 5 says Play outputs stay unlocked. That is acceptable for 0.2.0 [INFERENCE].

**K7. Output ownership list.**

The atlas pass replaces the mesh that the separator cloned. The separator's clone then has no reference. Put one owned-object list on `AmusePlatformFinishState` so the atlas stage destroys a predecessor clone that is AMUSE-owned. Without it the clone leaks until domain reload. Use the existing sweep pattern in `AlphaSeparationApply.cs` (lines near 1142 and 1151).

---

## 3. Concepts

Target: about fifteen concepts. The design introduces more than fifty terms.

Keep (15): use, route (a row in the route table), region, group, page set, page, placement, border, protected mip, tail, role, acquired image, named refusal, budget, owned-object list.

Merge:

- render group, atlas group, page-set key, merge-ready check, whole-material equality: into group.
- crop: into region.
- overlap, sampling support, padding: into border.
- recipe, selector, UV route, channel: into route.
- UV-value consumer, channel closure, page-size reader, shader-intrinsic motion, per-pass coordinates: into the route table lists (members, neutral, bans).
- slot batch: into the rule "all regions of one use go on one page".
- all work counters: into budget.
- candidate guard, aggregate guard: the two retained numbers of the budget.
- prepared source: into acquired image until normalization exists.

Defer (with their feature):

- toggle group, visibility pattern, admitted variants, material state: 0.3.0 or 0.2.x (F1, F2).
- density target, reference map, strength, scale, retained-resolution exception: F3.
- separate alpha mask: F4.
- compression category, material category, override, automatic compression fallback, replacement: F5.
- connected component, offset removal, repeated span, label-boundary cut: F6.
- spare channel, liveness: F7.
- strict mode: D2.

Delete:

- selection unit, planned shared page, separate candidate, shared fallback, output slot: D1.
- tested mip and minimum tested size for atlas: D1 and M3.
- evidence identity of AMUSE output, self-proof guard, cache eviction by instance: D5.
- RGB-only recipe, alpha removal: D7.
- growth after removal, overshoot, fixed-point removal: D6.
- build-target row: S5.
- settings snapshot: D3.
- preset placeholder, schema bump: D4.
- apply-time revalidation inventory: S2.
- aggregate quality measure: S9.

---

## 4. Gate and plan impact

Gate 0:

- G0.1 quality contract: shrinks to four role bounds, one local metric, and the Color role first. Drops aggregate measure, normalization loss split, and final alpha proofs.
- G0.2 sampling contract: shrinks to one border function for one route, protected mip choice, UV rounding table over the ladder, and Repeat, trilinear, anisotropy, and unequal map resolution fixtures. Drops thin diagonal and gradient cut falsifiers (F6), coverage-preserving mip fixtures (S4), strict (D2), and multi-vendor GPU evidence (F11).
- G0.3 counters: becomes M2, three numbers.
- G0.4 host and version rules: drops visibility patterns (F1), swap sampler analysis, locked route (F10), spare channel liveness (F7). Keeps route tables for one row (S1), sampler facts (K3), and the Play-mode sentence (K6).
- G0.5 migration: shrinks to field initializers and Reset on three fields. No preset work (D4). The migration spike already gives the evidence for the proxy field.
- G0.6 defaults: shrinks to maximum size, page ladder, protected mip default, and the role-to-format table. No matrix cell count.
- G0.7 integration contracts: drops evidence identity, alpha controls on atlas path, classify and prepare split, revalidation inventory (S2). Keeps the closed refusal set (M4), the split gate (K2), and Windows only (S5).
- G0.8 review and approval: stays.

Milestones:

- M1 settings snapshot and migration: shrinks to three fields, three fingerprint entries, one inspector toggle and field. Snapshot class and preset work leave.
- M2 first static path: becomes the whole of 0.2.0. Remove from it: evidence identity falsifiers, section 17.2 revalidation inventory, compression category selection, the decoded-input reuse bound. Add: stage ordering after Close, owned-object list (K7), vertex duplication test (S10).
- M3 regions, final alpha, page selection: delete. Move only "complete-use placement" and "small source on large page" is not needed (no atlas alpha analysis). Region cuts go to F6.
- M4 visibility and swaps: out of 0.2.0.
- M5 density: out of 0.2.0.
- M6 recipes: the second family and the other roles. Strict, separate mask leave. Locked live check stays as a regression fixture.
- M7 UI: three controls, one report line per page set, one line per refusal, allocation change with cause. No matrix, strength, strict, or mask controls. No dry-run atlas rows.
- M8 release qualification: keep numeric budget values and the one fixture that shows lower allocation. Drop "with and without separate-page generation" measurement.

The 2026-11-19 review found about thirty majors. By my reading the items tied to D1, F1, F2, F3, F6, and F7 account for most of items 1, 8, 10, 11, 12 (partly), 13, 15, 18, 20, and 22. That count is [INFERENCE] from the item list.

---

## 5. Tangle between atlasing and alpha separation, and the seam

The tangle: the design makes atlas decide before the separator and then feeds atlas output back into the separator's proof. That creates a loop. Atlas needs the source alpha result (hints). The separator needs atlas pages (final proof). The design then breaks the loop with a candidate, a fallback, a classify and prepare split, and an identity rule. The loop is the cause of items 1, 10, 11, 18, and 20.

Cleanest seam, one direction: **atlas depends on the avatar the separator produced. The separator knows nothing about atlas.**

- Order: separator passes as on 2026-11-19, then `TransientUnlockWindowClose`, then a new extension-free atlas pass. It reads live renderers and its own fresh evidence. It writes `sharedMesh` and `sharedMaterials` with no clip edit (F2).
- Atlas reads from the shared build state only these things: `state.Lifecycle`, the structural graph result, the animator bindings, and the AMUSE-owned object list (K7). It writes only its own outputs.
- Atlas never reads `PreparedAlphaSeparation` or `OpaqueBySource`. Those stay separator internal. The plan step "replace the avatar-wide source-to-one-opaque lookup" is not needed.
- Slot indices: the separator appends slots. Atlas captures after it, so it sees final indices. A binding at an appended index is already refused by the separator's collision guard. [INFERENCE: verify with one fixture.]
- The opaque clone and the original transparent slot differ in render state, so they fall into different groups. Vertices shared along the split boundary need duplication. That is required by the design in any order (design 5).
- Locked originals: after Close, locked materials are back on the slots. Atlas refuses them (F10). So atlas never touches the unlock window.
- Single writer rule: design 12 asks that alpha separation and atlas "must not independently write competing" arrays. Sequential writers over disjoint stages do not compete. Handoff section 3 calls the single writer an engineering proposal. Document the order as the contract.

Open check before adoption: write `sharedMesh` and `sharedMaterials` after Close and confirm the values reach the NDMF build container at its serialize step [INFERENCE]. The plugin comment says Close exists because the animator commit rebinds animation state over material arrays. In 0.2.0 atlas admits only unanimated slots, so the rebind should not touch them. One live fixture settles it.

Option B if the user keeps atlas-first: run atlas before the separator and let the separator see AMUSE pages as unattested textures. Their alpha answer is Unknown, so the separator keeps its pre-separation behavior on atlased uses. This is design 6's "unsupported separation keeps the valid atlas" with no identity minting and no AMUSE page proof. It costs the 0.1.0 opaque conversion on every atlased use. Do not mint an evidence identity for AMUSE pages in 0.2.0.
