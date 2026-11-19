# Merge-ready atlasing implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans for an approved executable task plan. Use superpowers:subagent-driven-development only when the user authorizes that execution method. Steps use checkbox syntax for tracking. This draft does not authorize execution.

Privacy note: This record contains public product planning only. It contains no private asset observations, machine identifiers, or vendor source excerpts.

Date: 2026-11-16.
Status on 2026-11-16: Draft delivery sequence for review. The design still requires user approval. This is not an execution-ready task plan. Gate 0 must close before product tasks receive final interfaces and executable RED/GREEN steps. No product implementation is authorized.
Status on 2026-11-19: Revised after four adversarial reviews and the user decisions of 2026-11-18 and 2026-11-19. Design section 17 controls. Gate 0 now defines work counters, and a release gate in Milestone 8 sets their numeric values. Milestone 2 is narrowed to one route. No product implementation is authorized.
Status on 2026-11-19, later: Historical record. `docs/superpowers/plans/2026-11-19-0-2-0-minimal-atlasing-plan.md` replaces this plan for 0.2.0.

**Goal:** Deliver nondestructive, merge-ready texture atlasing in 0.2.0 without implementing material combining from 0.3.0.

**Architecture:** Capture immutable facts, make pure grouping and layout decisions, prepare owned outputs, then revalidate and apply once. Compose atlasing and alpha separation before the existing final writer. Keep shader rules in the existing frontends and keep live capabilities out of immutable evidence.

**Tech stack:** C#, Unity 2022.3.22f1, NDMF, NUnit EditMode tests, and the installed vendor packages. No new dependency or assembly is proposed.

**Spec:** `docs/superpowers/specs/2026-11-16-merge-ready-atlasing-design.md`.

## Global constraints

- Preserve source meshes, materials, textures, import settings, animation assets, prefabs, and scenes.
- Preserve existing asset and metadata pairs. Do not change existing asset identities.
- Retain all existing alpha-separator controls and their meanings.
- Never subdivide triangles. Copy every vertex attribute when duplication is necessary.
- Do not bake tint or animated material values into textures.
- Do not add a shader interpreter, Animator reachability solver, packing search, format search, or result cache.
- Unsupported facts affect only the conversions that depend on them. Programming failures stop the build.
- Try at most one separate candidate and its one shared fallback per selection unit.
- Permit at most one Automatic compression replacement per generated texture within a layout.
- Preserve required alpha until final-output evidence establishes permission. Alpha removal is optional.
- Strict sampling does not add shader-level sampling restrictions.
- Generated atlas outputs use verified compressed formats only.
- Public fixtures must be deterministic, minimal, and redistributable. Vendor assets stay untracked.
- Pin the exact dev editor asset directory before Unity writes, broad operations, or reported validation. Never run tests in the private editor.
- Research tooling never enters the product or VPM package.
- No staging, commits, branch changes, publication, or deployment without separate authorization.

## Review focus

1. One source material can serve different visibility groups. A conversion must not retarget unrelated bindings. Milestones 2 and 4 own this falsifier.
2. A shader can read an apparently empty UV channel. Do not overwrite it without established liveness. Milestones 2 and 6 own this falsifier.
3. A small source can enter a large page. Page size must not expand its alpha-test eligibility. Milestone 3 owns this falsifier.
4. A removed material reference can leave its texture referenced elsewhere. Do not claim that allocation as saved. Milestones 2 and 8 own this falsifier.
5. A new false setting can resemble an absent old field. Migration must preserve explicit choices and the first edit. Milestones 1 and 7 own this falsifier.

---

## 1. Why this is a gated draft

The spikes establish narrow feasibility, not a complete production contract. The normal render spike showed strength-dependent compression loss. Uncompressed alpha extraction matched, but compression changed alpha and some cutout output. Finite protected mips worked in selected aligned fixtures. Useful strict-mode packing and measured resource limits remain open.

Do not conceal these gaps inside guessed constants, permissive defaults, or permanent refusal stubs. Do not produce a large set of speculative record types before their first consumer exists. This draft fixes the delivery order, responsibility map, and required outcomes. After Gate 0, expand the first milestone into exact interfaces and executable steps. Expand each later milestone from the code produced by its predecessor.

This is deliberate rolling task planning, not permission to omit any agreed feature. Every milestone is required for the full release. A scope cut requires user approval.

## 2. Gate 0: Close the contracts before product execution

**Files:** Update the controlling design and the relevant existing investigation records. Do not change production files to make a characterization pass.

**Consumes:** The design and the acquisition, sampling, normal, mask, and vendor-render evidence already recorded under `docs/superpowers/investigations/`.

**Produces:** A reviewed supported-case table, fixed policies, measured limits, and design approval. These become the exact inputs to executable task planning.

- [ ] **G0.1: Close the quality contract.** For Color, Emission, Normals, and Masks, specify the prepared-source reference, comparison space, local bound, aggregate measure, and supported consumer states. Separate accepted normalization loss from atlas and compression loss. Qualify shader recipes with public fixtures and apply texture-output checks to each generated image. User acceptance on 2026-11-17 permits visible shading changes despite passing texture-error limits. Do not promise a small visual change under every light. Do not add per-avatar rendering to judge quality. Numeric limits, supported recipes, final alpha proofs, and sampling protection remain required.
- [ ] **G0.2: Close the sampling contract.** Prove source-equivalent level mapping, filtering support, grid phase, and block alignment for each proposed route. Exercise a thin diagonal region, a gradient cut, a source-edge Repeat case, trilinear transitions, anisotropy, unequal map resolutions, and the shared tail. Add sRGB, non-power-of-two prepared size, and coverage-preserving mip fixtures. Copy protected levels from the full prepared-source chain. Establish the UV-rounding bound for one actual vendor route under the 2026-11-18 decision. Keep Float32 storage for every remapped UV channel and verify the output vertex format. Establish the anisotropic support border with evidence from more than one GPU vendor. Honor the per-platform anisotropy settings for alpha consequences, as decided on 2026-11-19. As of 2026-11-19, no useful strict-mode case fits the allowed detection rules. Bring evidence of a useful case to the user rather than ship an ineffective control.
- [ ] **G0.3: Define the work and growth counters.** For retained allocation, peak temporary bytes, cumulative copied pixels, source reads, encodes, predicate reads, variants, pages, and fragmented components, state the unit, the scope, the trip rule, and the measuring instrument. State what happens when an aggregate counter trips before later units run. Tests inject limits through a production delegate seam. Milestone 8 sets the numeric values from production measurements. No invented constant ships.
- [ ] **G0.4: Close host and version rules.** Define recognized constant and identical effective visibility patterns. Include parent activation, renderer enablement, and transitions. Verify addressable UV routes and liveness for the actual supported shader rows and applicable passes. Declare every UV-value consumer per row across channels zero through seven. Define sampler compatibility, including textures that borrow the main sampler. Name the page-size readers and the per-pass coordinate routes. Define whole-material equality capture. Admit only exactly attested rows. The consent dialog does not extend atlas recipes. Settle the locked-material route after the relock spike. Unknown relationships stay separate. An installed vendor version does not establish all versions in the product range.
- [ ] **G0.5: Prove migration without new machinery first.** Use disposable serialized public fixtures to distinguish missing atlas fields from explicit false values. Exercise Reset, Undo, load, save, and the first edit. Check old preset input separately from component deserialization. Prefer field defaults and a bounded preset migration when they meet the contract.
- [ ] **G0.6: Freeze defaults and choices.** Record the meaning and default of maximum size, the finite page ladder, protected-mip choices, and approved format entries from the evidence above. Count the compression-matrix cells that offer a real choice. Keep protected and tested mip controls independent but use the same choice vocabulary. Do not infer these values from the separator's unrelated defaults. The shipped presets carry the atlas fields with global defaults as placeholders, as decided on 2026-11-19.
- [ ] **G0.7: Close integration contracts.** Define the evidence identity for AMUSE-owned outputs. An output never proves alpha one from its format. Define the split enable gates, the apply-time revalidation inventory, the property-block and renderer-wide animation rule, the meaning of existing alpha controls on the atlas path, the classify and prepare split, and the closed set of named refusals beside the throwing invariants. Design section 17.2 gives the proposals. Atlasing runs only on the Windows build target in 0.2.0. Keep target-specific facts in data keyed by build target.
- [ ] **G0.8: Review the closed design and obtain user approval.** Review policy changes in isolation. Then write the executable plan for Milestone 1. No production task starts merely because this draft exists.

**Exit evidence:** Each gate has a dated result, source or fixture reference, explicit limits, and a pass or a blocking finding. No unresolved value is replaced with an invented default. If a gate fails, resolve that contract before product execution.

Migration evidence on 2026-11-17: `docs/superpowers/investigations/2026-11-17-atlasing-migration-spike.md` records 14 native-prefab assertions and five current-parser scenarios. A missing default-true field loaded true while a stored false survived. The first false edit, Reset, and Undo also survived the exercised paths. Prefer field initializers and Reset values over a new atlas migration marker. G0.5 remains open for the actual atlas representation and bounded preset migration. This evidence does not authorize implementation.

Sampling evidence on 2026-11-17: `docs/superpowers/investigations/2026-11-17-atlasing-sampling-followup-spike.md` records 494 GPU comparisons across 13 fully recovered batches. It supports a level-specific bilinear block-neighborhood rule and a narrow strict crop candidate. It does not qualify general anisotropy or a vendor recipe. Translation-sensitive numerical errors need their own bounded policy. A border increase does not resolve them. G0.2 remains open. Do not treat the accepted compression risk as approval of a coordinate-rounding allowance.

Decision update on 2026-11-19: The user decisions of 2026-11-18 and 2026-11-19 supersede the last sentence above. A separately bounded UV-rounding error is permitted. Anisotropic tap differences follow the compression-risk acceptance, limited by the per-platform anisotropy settings for alpha. See design sections 15.2 and 17.1.

## 3. Responsibility and file map

Existing paths below were inspected or identified from the checked-out source. New modules are proposed locations, not existing interfaces. Their exact fields and methods belong in the executable milestone plan after Gate 0.

| Responsibility | Existing integration points | Proposed new module when needed |
| --- | --- | --- |
| Stored choices and resolved configuration | `Runtime/AmuseAvatarOptimizer.cs`, `Editor/Presets/`, build, dry run, and Play-mode fingerprint | `Editor/Analysis/OptimizerSettingsSnapshot.cs` |
| Bounded shader texture and coordinate facts | `Editor/Semantics/UnityMaterialSemantics.cs`, `LilToon/`, `Poiyomi/` | `Editor/Semantics/AtlasMaterialEvidence.cs` |
| Effective full-image acquisition | `Editor/Host/UnityStreamingTextureEvidence.cs`, `UnityGeneratedTextureEvidence.cs` | `Editor/Host/UnityAtlasImageCapture.cs` |
| Visibility facts | `Editor/Host/UnityAnimationEvidenceCapture.cs`, `CapturedAnimationEvidence.cs` | `Editor/Host/CapturedVisibilityEvidence.cs` |
| Topology, density, and finite placement decisions | `Editor/Analysis/`, existing affine geometry and admitted states | Add focused pure modules only in the milestone that consumes them |
| Owned textures and final renderer assignments | `Editor/Build/AlphaSeparationRecords.cs`, preparation, apply, and window close | Replace the limiting prepared-output representation when the first atlas consumer needs it |
| Preview, reports, and controls | `Editor/DryRun/`, `AmuseAvatarOptimizerEditor.cs`, `Build/AmuseReports.cs`, `Build/PlayModeBuildStore.cs` | No second result store or configuration authority |

Paths in this table are relative to `Packages/com.alrauna.amuse/`. Tests mirror production under `Tests/Editor/`. Add a new file and its metadata together through the existing Unity workflow.

### Interfaces that must remain stable

`PresetParser.TryParse(string json, out OptimizerPreset preset, out PresetLoadRefusal refusal)` remains the preset import entry. Extend its bounded version handling, not the caller's parsing responsibilities.

Keep the capture, prepare, validate, and apply separation. The current plugin runs preparation and apply inside one animator-services scope. Window close runs after that scope commits and reasserts the recorded final references. Do not follow older extension-free barrier comments instead of the actual plugin configuration.

The existing finalization records already describe complete renderer material arrays and binding-specific curve edits. Reuse that role. The source-to-one-opaque-result lookup cannot represent atlas outputs for different uses. Replace that assumption at the actual integration seam. Do not add a second writer or keep obsolete aliases after cutover.

## 4. Milestone 1: One settings snapshot and proven migration

**Files:** `Runtime/AmuseAvatarOptimizer.cs`, `Editor/Analysis/OptimizerSettingsSnapshot.cs`, `Editor/Presets/OptimizerPreset.cs`, `PresetParser.cs`, `PresetApplier.cs`, `Editor/Build/AmusePlatformFinishPass.cs`, `PlayModeBuildStore.cs`, and `Editor/DryRun/AlphaSeparationDryRun.cs`.

**Tests:** Extend the existing Runtime, Presets, DryRun, and Build settings tests. Add snapshot tests only for behavior that is not covered through those consumers.

**Consumes:** Gate 0 defaults and migration representation.
**Produces:** One immutable captured configuration consumed by build, dry run, and fingerprint calculation. The serialized component remains the only editable authority.

- [ ] Write consumer-visible migration tests. A valid schema-five preset retains every old alpha value and leaves the component's atlas values unchanged. A preset of the new schema applies its atlas values, which are the global-default placeholders. An explicit false in the component stays false after a schema-five preset applies. Unknown future versions and unknown keys refuse without partial application. Preserve the existing treatment of older unsupported schemas unless the approved migration explicitly includes them.
- [ ] Write gate tests. Alpha separation off and atlasing on still runs the shared prerequisites. Both off analyzes, mutates, and reports nothing, and consent never asks. Atlasing alone never shows the consent dialog.
- [ ] Observe RED for the new behavior. Implement the bounded migration and snapshot. Run the focused fixtures and observe GREEN. Do not add a registry or a second settings object that can be edited independently.
- [ ] Verify that the first edit survives serialization, Reset restores the defined defaults, Undo restores the previous state, and changing an output-affecting setting invalidates the dependent result. Do not activate an incomplete atlas path through newly stored defaults.

## 5. Milestone 2: First complete static atlas path

**Files:** The material evidence and image-capture modules in the table, `Editor/Host/UnityRendererGeometryExtractor.cs`, and the existing build preparation, records, apply, and final window-close files. Add only the pure region and layout modules this path needs.

**Tests:** New focused atlas integration tests under `Tests/Editor/Build/`. Extend the existing geometry, apply, and window-close fixtures. Use the installed vendor material helpers.

**Consumes:** The settings snapshot, the Gate 0 recipe, captured geometry and material state. Limits come from the test seam.
**Produces:** One supported static conversion from effective input through final build references, for one shader family and one named route. The output passes the whole-material equality check without combining material slots.

Scope: one shader family and one named route from the supported-case table. Disk import-copy acquisition only. Constant visibility only. Windows build target only. No alpha separation on atlased uses. The section 15.2 border construction is included from the start.

- [ ] Build the render harness that later milestones reuse. It renders the real installed vendor shader for a fixture. Record its cost on the CI runner.
- [ ] Write an end-to-end public fixture with compatible materials and different image regions. Assert correct rendered output, unchanged sources, complete mesh attributes, Float32 remapped UVs in the built mesh, correct final texture references, deterministic placement, and unchanged material-slot count from atlasing alone. Add an unrelated source reference and assert it remains counted. Add a non-Windows target and assert one named refusal with sources preserved.
- [ ] Write the self-proof falsifiers of design section 17.2 item 1. An AMUSE-owned output never proves alpha one from its format. An AMUSE-owned texture without an alpha channel yields no alpha chain. A transparent source in a BC1 page never proves opaque at the final analysis. An AMUSE output never matches a generated-texture name that the existing attestation admits. The dry run never reads AMUSE-owned outputs.
- [ ] Write the route checks for the one rewritten channel, as design section 17.2 items 2, 3, and 21 define them. Add one falsifier per UV-value consumer that reads that channel on the chosen row. Add a texture on the same channel that is not a page member. Add a preserved Repeat-tiled texture that borrows the main sampler. Add two members with anisotropy levels one and sixteen.
- [ ] Observe RED. Implement the smallest complete path with source-file acquisition at the effective import ceiling, output evidence identity, compression category and format selection, finite packing, owned output generation, coordinate conversion, revalidation of the section 17.2 inventory, and one final assignment. Preflight decode and staging allocations against the injected limits. Bound per-build decoded-input reuse by effective identity, interpretation, and resolution. Preserve dependent source uses for unsupported routes. Throw on output invariants.
- [ ] Observe GREEN through the actual build lifecycle and real shader output. Change each required live fact between prepare and apply and assert no invalid atlas assignment occurs. Prove cleanup and final references after animator commit. Do not write tests that merely inspect new record wiring.

This is the first place to generalize prepared output ownership. It must represent per-use results and shared textures. The slot-scoped opaque mapping already exists. Replace only the avatar-wide source-to-one-opaque lookup. Keep alpha-only behavior intact for uses that atlasing does not touch. Do not perform a standalone rewrite of the build pipeline first.

## 6. Milestone 3: Regions, final alpha, and bounded page selection

**Files:** The pure region and layout modules introduced in Milestone 2, `Editor/Host/UnityRendererAlphaAnalysis.cs`, and build preparation, records, apply, and reports.

**Tests:** Atlas region and selection tests plus the existing classifier, separation, and finalization fixtures.

**Consumes:** The static path, source alpha hints, approved support bounds, and resource account.
**Produces:** Region cuts without triangle subdivision, one separate proposal, one shared fallback, and final-output alpha decisions.

- [ ] Add falsifiers for a mixed-alpha island, disconnected coincident geometry, seams, overlapping crops, integer-offset Repeat islands, unsupported repeated spans, degenerate UVs, fragmented components, and unequal map resolutions. Assert original triangle ownership and geometry remain intact. Check protected-level neighborhoods and grid phase on both sides of a cut.
- [ ] Add lifecycle falsifiers for a separate candidate that fails final alpha, fit, and allocation in turn. Assert at most one separate attempt and one shared attempt, rejected output cleanup before fallback, no extra format search, independent final evidence, and local source preservation when both fail.
- [ ] Pin complete-slot batch placement and aligned variants. Paging alone never adds material slots. Copy protected levels from the full prepared-source chain, keep independent protected regions out of shared compression blocks, and construct one deterministic unprotected tail. Test that a later crop cannot overwrite an earlier crop's required pixels.
- [ ] Add the strict-mode path only if Gate 0 supplied a useful strict case. Cover every accessible level and the one-texel tail rejection with falsifiers.
- [ ] Add a slot that qualifies for folding. Assert that existing alpha controls apply to the final atlas analysis as they apply to a source analysis.
- [ ] Add the small-source-on-large-page case. Assert alpha eligibility follows prepared source-equivalent size rather than page or crop size. No eligible level means Unknown. Never truncate tested scope to protected scope. An alpha-dropping output cannot grant itself opaque permission.
- [ ] Observe RED, implement the selected rules, and observe GREEN. Compare estimated allocation with actual generated dimensions, formats, blocks, and mip chains. Preserve valid atlasing when separation cannot qualify. Do not move triangles between proposed memberships to rescue a failed candidate.
- [ ] Implement post-selection dependency removal to a fixed point. A failed shared page invalidates all outputs that require it, not unrelated pages. Remove every affected admitted state consistently. A removed use restores its source bindings without alpha separation and the report names it. Report the realized allocation and any overshoot. Do not repack, regenerate, or revisit earlier allocation decisions after selection.
- [ ] Split source alpha classification from ordinary preparation, as design section 17.2 item 11 defines it. Assert that an atlased use creates no opaque clone. Assert that the alpha separation switch and its cutout and transparent sub-switches govern the separate candidate (item 20).
- [ ] Evict a rejected candidate's cache entries by instance (item 18). Assert that source chains survive the eviction. Inject an invariant failure and assert that it reaches the build as an exception (item 12).

## 7. Milestone 4: Visibility groups and material animation

**Files:** `Editor/Host/UnityAnimationEvidenceCapture.cs`, the visibility evidence module, `Editor/Analysis/AdmittedMaterialStates.cs`, grouping decisions, build preparation, apply, and final window close.

**Tests:** Extend animation capture, admitted-state, material-swap, and transient transformation integration fixtures.

**Consumes:** Gate 0 recognized visibility patterns and the selected page lifecycle.
**Produces:** Merge-ready groups and use-specific animation outputs across all admitted states.

- [ ] Add matched visibility functions with parent activation and renderer enablement. Add different functions with the same parameter or clip name. Assert only established equivalence groups together. Mutually exclusive surfaces must not share stored pixels merely because they do not appear together.
- [ ] Add one source material used by separate groups, a material swap with matching layouts, and a swap variant that cannot share the coordinate assignment. Assert per-binding output references and unchanged keyframe times. The incompatible use preserves its source path without refusing an unrelated group.
- [ ] Observe RED, implement bounded capture and grouping, and observe GREEN through animator commit. Reject a swap slot that would straddle selection units. Do not introduce runtime UV changes or a reachability solver.

## 8. Milestone 5: Density and aligned map resolutions

**Files:** Focused pure density calculation beside the region planner, image preparation, aligned layout decisions, and reports.

**Tests:** Pure numerical fixtures and vendor-backed output fixtures using the existing mesh and image preparation path.

**Consumes:** Established group membership, acquired image dimensions, effective undeformed physical areas, and approved reduction recipes.
**Produces:** Optional loss-only normalization with one group target before paging and alpha cuts.

- [ ] Pin the design's area-weighted calculation with unequal physical areas, transform scale, mirrored UVs, repeated source references, multiple variants, and shared storage spanning different densities. Count each physical triangle once. Shared storage takes priority over exact density equality.
- [ ] Assert disabled normalization and strength zero preserve scale. Full strength never enlarges a source. Intermediate strength interpolates scale linearly. One unusable measurement in a shared placement keeps that placement at scale one while unrelated placements can reduce. Main color supplies the reference where usable, otherwise the highest-density usable participating map does.
- [ ] Observe RED, implement the calculation and approved role reductions, and observe GREEN in actual output. Preserve dependent relative map resolutions. Milestone 6 adds the retained-resolution exception for generated alpha. Round prepared dimensions up to the alignment of the coarsest protected level without exceeding the acquisition ceiling. Otherwise keep scale one.

## 9. Milestone 6: Complete verified shader recipes

**Files:** `Editor/Semantics/LilToon/`, `Editor/Semantics/Poiyomi/`, the atlas material facts, image capture and preparation, and locked reconstruction integration where necessary.

**Tests:** Vendor-backed semantics and build fixtures, plus real rendered output. Do not copy vendor source or assets into fixtures.

**Consumes:** The full path and the version-specific supported-case table.
**Produces:** A nonempty verified surface in each claimed shader family, with local named refusals for unsupported dependent uses.

- [ ] Verify fixed UV0 remapping and selectable routes separately. Include an occupied channel, an apparently empty but read channel, a procedural selector, animated selectors or transforms, and shader-intrinsic motion. Extend the Milestone 2 route checks to every other route. Add one falsifier per UV-value consumer in each row's declared list from design section 17.2 item 2. Add Poiyomi pixel mode, sharpened alpha-to-coverage, and an active lilToon shadow-mask level floor (item 4). Add the lilToon outline fragment and vertex transforms and the second normal-map scale mask (item 5). Add one shader-intrinsic motion falsifier per vendor (item 6). Add one falsifier per whole-material fact (item 7). Add a per-slot and a renderer-wide property-block or curve on a recipe property (item 9). Preserve all original attribute dimensions, skinning, blend shapes, normals, and tangents during vertex duplication. Never disable a feature to free a channel.
- [ ] Verify Color, Emission, Normals, and Masks through their actual consumers. Include normal strengths one half, one, and two, dependent strength masks, source color interpretation, actual final compressed alpha, and relevant non-forward passes. Do not infer normal quality from one lighting view or infer continuous transparency from an opacity proof.
- [ ] Verify opt-in separate alpha generation with live non-unit tint and an inactive mask route. Include compression and minification falsifiers. Skip unsupported extraction while keeping ordinary permitted reduction. An existing sampled product must not be baked into one filtered image.
- [ ] Observe RED and GREEN for each new supported case. Locked outputs follow design section 17.4. Rely on the vendor upload lock. Add a live check behind the existing vendor opt-in gate. It sets the SDK build type as the 2026-11-19 probe did. It covers an atlas output and a slot from the transient unlock window. The user approved the route on 2026-11-19 on the condition that it is functional. If this check does not pass before release, these uses are not admitted. No vendor unlock or relock invocation is added to the build. Unsupported versions and routes retain their declared compatibility behavior rather than borrowing assumptions from native Poiyomi 10.

## 10. Milestone 7: UI, presets, preview, and disclosures

**Files:** `Runtime/AmuseAvatarOptimizer.cs`, `Editor/AmuseAvatarOptimizerEditor.cs`, `Editor/Presets/`, `Editor/DryRun/`, `Editor/Build/AmuseReports.cs`, `PlayModeBuildStore.cs`, and `README.md`.

**Tests:** Existing inspector, preset, dry-run, and report fixtures. Verify the actual inspector visually.

**Consumes:** All closed controls and functioning execution paths.
**Produces:** Simple controls with one stored source of truth and truthful scene-preview and build results.

- [ ] Prove default-on atlasing for absent old options and preservation of explicit false. Keep normalization and separate alpha generation opt-in. Expose the approved maximum-size choice, linear strength, and strict sampling only after their gates pass. Keep existing alpha controls and adopt the shared protected/tested mip vocabulary without changing stored meanings.
- [ ] Expose the requested compression matrix with Color, Emission, Normals, and Masks against Opaque, Cutout, and Transparent. Milestone 2 already implements category and format selection. Automatic is the default. Emission masks remain Masks. Transparent wins a shared texture's storage category, then Cutout, then Opaque. Required alpha evidence overrides an incompatible format choice. Automatic fallback defaults on and remains bounded to one replacement. Report substitutions. When fallback is off, honor the override or follow the existing atlas and source fallback without silently choosing another format.
- [ ] Assert settings changes invalidate dependent results. Scene preview remains read-only and is labeled as a scene snapshot. Build results include upstream changes. Label the session summary correctly. Do not create another cache, promise an interactive packing simulation, or present future combining gains as achieved savings.
- [ ] Verify the actual inspector, Reset, Undo, preset selection, and first edit. Explain detail loss, possible transparency changes, allocation growth, and preserved inputs beside their relevant results or controls. Show one primary named refusal with an actionable limit where available. Do not remove existing presentation state merely as unrelated cleanup.

## 11. Milestone 8: Release qualification

**Files:** The public fixture suite, existing test assemblies, `README.md`, and the established release documentation. Keep release packaging limited to the product package.

**Consumes:** All earlier milestones with observed focused checks.
**Produces:** Release evidence for the whole agreed scope. This is not release authorization.

- [ ] Run the product and research EditMode assemblies in the pinned dev project. Observe nonzero test counts and Console results. Include real installed vendor tests and lifecycle fixtures. Preserve the existing CI trust gate and vendor lock. Do not weaken tests or release exclusions.
- [ ] Set the numeric work and growth limits from measurements of production code on public fixtures. Record each value with its rationale. Replace the test seam values in the shipped defaults.
- [ ] Render public mixed-alpha, animated, normalized, and locked-output fixtures. Render a strict fixture only if Gate 0 supplied a useful strict case. Show one fixture with lower deduplicated retained allocation. Show shared-output material compatibility without actually combining material slots. Report preserved cases as well as accepted cases.
- [ ] Measure the same fixture with and without separate-page generation. Record accepted separate candidates, output counts, build-time change, peak temporary work, and discarded work. Check both per-candidate and aggregate guards. Many small increases cannot bypass the aggregate guard. Fragmentation and unused space remain counted.
- [ ] Exercise shared-page invalidation across renderers. Remove dependent conversions to a fixed point without repacking. Preserve unrelated outputs, all admitted states, and consistent coordinates. Confirm no source assets changed and every unused owned object was destroyed.
- [ ] Verify user documentation, supported cases, limitations, source provenance, and the research-package exclusion. Sweep changed documents for private identifiers. Report observed results and remaining unsupported cases without claiming runtime speed or residency from allocation estimates.

## 12. Verification protocol for executable milestone plans

A milestone's executable plan must name its exact production interface, exact fixture method, wrong implementation that the assertion rejects, and focused Unity filter. A missing-symbol failure is not enough when an existing path can expose the behavioral failure. Use installed vendors for real supported surfaces and independent synthetic fixtures for controlled falsifiers.

Run each focused check after its coherent change. Observe RED before a behavior fix and GREEN afterward. A zero-test filtered result fails validation. Run the full product and research assemblies at the integration milestones, not repeatedly during file edits. Do not claim compilation alone as behavior proof.

Use Unity Test Runner or the existing `scripts/ci/run-tests.ps1` route in the correct dev project. The runner takes an assembly, not an invented method-filter argument. Do not launch a second editor against a project already open. Record counts and sanitized Console findings, not editor or test-job identifiers.

Update the relevant product documentation with each completed behavior. Delete disposable code. Do not commit it or product changes without explicit authorization. No release version, tag, listing, or publication change follows automatically from a completed milestone.

## 13. Coverage and handoff

| Design scope | Owning milestone |
| --- | --- |
| Merge readiness, no material combining, one final writer | 2, 4, 8 |
| Settings snapshot, absent defaults, old presets, explicit choices | 1, 7 |
| Effective source acquisition and generated input | 2, 6 |
| UV routes, liveness, full mesh preservation | 2, 6 |
| Islands, region cuts, Repeat, overlapping storage, finite packing | 2, 3 |
| Final alpha, source-relative mip scope, separate and shared fallback | 3 |
| Visibility equivalence, swaps, use-specific animation | 4 |
| Loss-only density, shared target, relative map resolution | 5 |
| Compression roles, emission, retained alpha, separate mask | 2, 6, 7 |
| Strict and ordinary sampling, protected versus tested levels | Gate 0, 3, 6, 7 |
| Allocation, temporary work, cleanup, local invalidation | Gate 0, 2, 3, 8 |
| Output evidence identity, split gates, revalidation, refusals | Gate 0, 1, 2 |
| UV-value consumers, sampler sharing, page-size readers, consent scope, locked outputs | Gate 0, 6 |
| UI, preview truth, disclosures, no claimed draw savings | 7, 8 |
| Real vendors, source integrity, release qualification | 6, 8 |

Self-review on 2026-11-16: Every design scope has an owner above. The five review-focus failures have explicit test requirements. No new production interface is claimed to exist. No unknown numeric policy is disguised as an executable instruction. The order delivers one complete static path before expanding animation, normalization, or shader coverage. Later milestones do not authorize partial release or remove agreed features.

Next action: Close Gate 0 and obtain design approval. Then write and review the exact Milestone 1 task plan against the source at that time. Recommend inline execution with focused independent review at the complete-path milestones. Shared lifecycle and ownership make premature parallel implementation unsafe.
