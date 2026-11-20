# AMUSE atlasing handoff

Privacy note: This record contains public product planning and synthetic fixture results only. It contains no private asset observations or machine identifiers.

Date: 2026-11-18.
Status on 2026-11-18: Design investigation is paused for handoff. No product implementation is authorized. The delivery plan is a gated draft, not an executable plan.
Status on 2026-11-19: The user decided the anisotropic case. Four adversarial reviews then completed, and the user answered six review questions. Design sections 15.2 and 17 record these. The plan draft was revised to match. No product implementation is authorized.
Status on 2026-11-19, later: A minimalism review found that the atlas-first order caused most of the design complexity. The user accepted its changes on 2026-11-19. The new controlling records are the 2026-11-19 minimal design and plan. The 2026-11-16 design and plan are historical. Sections 2 to 6 below describe the older design unless a line states otherwise.
Status at the end of 2026-11-19: Milestone 1 of `docs/superpowers/plans/2026-11-19-0-2-0-minimal-atlasing-plan.md` is implemented and committed on branch `atlasing-m1-settings-and-gate`. The executable plan `docs/superpowers/plans/2026-11-19-atlasing-milestone-1-settings-and-gate.md` records decisions D1 to D6 and the result. On 2026-11-19 the full test assemblies ran in EditMode in the dev editor instance, with no filter. `Alrauna.Amuse.Tests.Editor` ran 3303 tests with 3278 passed, 0 failed, and 25 skipped. The skips are the vendor opt-in gates. `Alrauna.Amuse.Research.Tests.Editor` ran 151 tests with 151 passed and 0 failed.
Status on 2026-11-20: Milestone 2 of `docs/superpowers/plans/2026-11-19-0-2-0-minimal-atlasing-plan.md`, the Poiyomi main color atlas path, is implemented on branch `atlasing-m2-poiyomi-main-color`. The executable plan `docs/superpowers/plans/2026-11-19-atlasing-milestone-2-poiyomi-main-color.md` records the seven tasks and their review rulings. On 2026-11-20 the product and research EditMode assemblies ran in the dev editor instance, with no filter. The combined payload reported 3603 total, 3578 passed, 0 failed, and 25 skipped. The GPU reach fixture `AtlasGpuReachTests` re-measured the border reach on 2026-11-20 on the dev editor GPU: 1.326 texels at anisotropy level 0 under a border of 1 texel, and 8.661 texels at level 16 under a border of 16 texels. Each row fails at its border reach plus one half texel, so both rows held. The upload fixture menu item `AMUSE Tests/Create atlas upload fixture` in the test assembly creates the public avatar with two 4096 sources, a 4064 texel UV rectangle each, and one shared page of 8192 by 4096 texels, under `Assets/_PoiyomiShadersAtlasUpload`. The gitignore pattern `Assets/_PoiyomiShaders*` already ignores that folder. Its proof run on 2026-11-20 created the folder, wrote nothing under tracked folders, and removed the folder through the asset database. The owner build and VRChat upload of that avatar is pending owner action since 2026-11-20. The two upload facts, whether the page streams in the built bundle and whether VRChat accepts the page, are open.

## 1. Start here

AMUSE 0.2.0 will add merge-ready texture atlasing. Material combining belongs to 0.3.0. The user wants simple internals and a simple interface. Push back when an accepted idea harms safety or maintenance. The user delegates engineering choices and does not want repeated preference questions about facts that tools can establish.

Read these controlling records first:

1. `docs/superpowers/specs/2026-11-19-minimal-atlasing-design.md`.
2. `docs/superpowers/plans/2026-11-19-0-2-0-minimal-atlasing-plan.md`.
3. `docs/superpowers/investigations/2026-11-19-atlasing-minimalism-review.md`.
4. The historical design `docs/superpowers/specs/2026-11-16-merge-ready-atlasing-design.md`, for the decisions about deferred features.
5. `docs/superpowers/investigations/2026-11-17-atlasing-sampling-followup-spike.md`.
6. `docs/superpowers/investigations/2026-11-17-atlasing-migration-spike.md`.
7. `docs/superpowers/investigations/2026-11-19-atlasing-coordinate-precision-spike.md`.

In the historical design, section 17 controls where an earlier section is less specific.

The historical design contains older review history. Later dated decisions supersede conflicting earlier proposals. Until 2026-11-19 the design determined alpha from final atlas output. The minimal design of 2026-11-19 replaces that order. Alpha separation now runs first on source evidence, and no alpha proof runs on an AMUSE page.

The user authorized investigations, disposable spikes, and independent subagent review. This does not authorize production implementation, commits, publication, or a release. Earlier isolated reviews do not approve later contract changes.

## 2. Resolved user decisions

The UV-rounding question was open when this handoff was first written on 2026-11-18. The user answered it later on 2026-11-18. The user accepted this rule:

> Permit a separately bounded UV-rounding error in ordinary and strict modes. Reject a route when its bound cannot be established. This does not permit contamination from neighboring regions.

Section 15.2 of the controlling design records the decision. It is a policy decision. It is not proof of a bound. Do not invent a numerical bound.

The user did accept a different rule on 2026-11-17. Compression can cause visible shading changes despite passing texture-error limits. Qualify shader recipes with public fixtures. Apply texture-output checks to each generated image. Do not build a system that renders every avatar to judge quality. That acceptance does not waive alpha proofs or sampling protection. It also does not approve UV-rounding error.

On 2026-11-19 the user decided the anisotropic case and answered six review questions. Design sections 15.2 and 17.1 record them:

- Anisotropic tap differences follow the compression-risk acceptance. The existing per-platform anisotropy settings govern their alpha consequences.
- Atlasing runs on the Windows build target only in 0.2.0. Build it so that Android and iOS can follow by adding evidence and data rows.
- Decision on 2026-11-19: Atlas uses on unlocked Poiyomi outputs, including slots from the transient unlock window, are admitted if the path is functional. A live locked-output check must pass before release. See `docs/superpowers/investigations/2026-11-19-atlasing-locked-relock-spike.md` and design section 17.4.
- Decision on 2026-11-19: An atlas use that apply removes restores its source bindings without alpha separation in 0.2.0. This is design section 17.2 item 11.
- Atlas conversions admit only exactly attested shader rows. Version consent does not extend them.
- Alpha separation and atlasing have split enable gates.
- An older preset without atlas keys leaves the component's atlas values unchanged. Shipped presets carry the atlas fields with global defaults as placeholders.

## 3. Settled product choices

Do not reopen these choices without new evidence or a concrete conflict. Review on 2026-11-19 found that three items are engineering proposals, not user decisions: one separate candidate then one shared fallback, the packer details, and the single output writer. Design sections 6, 8, and 15 label them as proposals. Treat them as open to evidence like any engineering choice.

- Deliver merge-ready atlasing, not minimum-memory search. Atlasing alone does not promise fewer material slots or draws.
- Enable atlasing by default for absent fields in older components. Preserve explicitly stored false values. Older presets without atlas keys leave atlas values unchanged, as decided on 2026-11-19.
- Retain existing alpha-separator controls. Their accepted approximations smooth over authoring mistakes.
- Never subdivide triangles. Vertex duplication must preserve every vertex attribute, skinning, and blend shape.
- Use regions rather than whole images. Region cutting changes triangle membership between crops, not triangle geometry.
- Normalize whole-island integer offsets only when every dependent mapping, every UV-value consumer, and every admitted state stays equivalent. Never wrap individual vertices.
- Preserve extreme tiled uses when they exceed limits. Do not silently shrink them to fit.
- Use shader-specific UV recipes. Select channels automatically. An absent mesh channel is not proof that it is unused.
- Remap UV0 only when every dependent use remains valid. Additional channels are not a universal solution for fixed UV0 routes.
- Group by compatible material behavior and established effective visibility. Matching parameter or clip names are insufficient.
- Keep all admitted material variants on one coordinate layout. Retarget animation bindings to use-specific outputs. Do not rewrite UVs at runtime.
- Try one separate opaque/transparent candidate, then one shared fallback. Generate and analyze each candidate independently.
- Prefer a valid shared opaque/transparent atlas over restoring sources when separate storage cannot qualify within limits.
- Keep source-based alpha results only for preserved source uses. Never apply an atlas verdict to restored source textures.
- Preserve required stored alpha until final-output evidence grants permission. Alpha-dropping compression cannot grant itself opaque permission.
- Use one deterministic finite packer. Do not rotate or mirror regions. Do not search layouts, formats, or crop-merging strategies.
- Paging resolves fit, sampling, or alpha-storage splits. It does not chase minor savings. Paging alone does not add material slots.
- Require candidate and aggregate allocation guards, plus temporary-memory and cumulative-work limits. Their numerical values remain unset.
- Make density normalization opt-in and loss-only. Its initial enabled strength is full. Interpolate the resolution scale linearly.
- Compute one area-weighted density target per atlas group before paging and alpha cuts. Prefer main color as the density reference where usable.
- Preserve relative map resolutions. Compatible shared storage takes priority over exact density equality.
- Keep Allow Separate Alpha Mask Generation opt-in and best effort. It can retain higher-resolution alpha within the acquisition ceiling.
- Skip unsupported mask extraction while retaining ordinary permitted downsampling. Do not bake tint, animation, or independently sampled products.
- Keep strict sampling opt-in. Protect the prepared normalized images through every accessible mip. Do not add shader sampling restrictions.
- Keep coarsest protected mip separate from coarsest tested mip. Use the same choice vocabulary, not a shared stored value.
- Apply alpha minimum-size rules to prepared source-equivalent resolution. A larger page does not make a small source eligible.
- Use Color, Emission, Normals, and Masks compression categories against Opaque, Cutout, and Transparent material categories.
- Keep Emission limited to emission images. Emission masks remain Masks. Automatic is the default for each matrix entry.
- Keep Allow Automatic Compression Fallback enabled by default. Permit at most one replacement per output within a fixed layout.
- Exclude uncompressed generated atlas outputs. Temporary uncompressed buffers remain allowed and must count toward limits.
- Prefer source files with Unity import meaning and the effective imported resolution ceiling. Do not restore stale files over upstream generated results.
- Use one final output writer. Revalidate live facts before mutation. Unsupported facts remain local. Programming failures stop the build.

The controlling design supplies the detailed equations, dependencies, and exceptions. This list does not replace it.

## 4. Evidence and its limits

### Adversarial review on 2026-11-19

Four isolated reviewers checked the design, plan, handoff, and evidence records against product and vendor source. They found four blockers. An AMUSE-owned page could prove itself opaque through the no-alpha-format rule. Both vendors read raw UV values as data. The anisotropic decision ignored the existing anisotropy settings. Gate 0 asked for limits that need production code. Design section 17.2 lists the corrections. They are proposals and need review.

### Locked-material relock spike

The 2026-11-19 source spike found that NDMF saves AMUSE outputs into its build container before the vendor upload lock runs. An approved live probe on the same date confirmed it. With the SDK build type set, the AMUSE canonical clone and a material that only a clip references were locked. VRCFury blocks the lock when no SDK build is active, so Play mode outputs stay unlocked. The probe residue was deleted. See `docs/superpowers/investigations/2026-11-19-atlasing-locked-relock-spike.md`.

### Review of the section 17.2 corrections

On 2026-11-19 an isolated read-only reviewer checked the 19 corrections against product and vendor source. It found no blockers and eight majors. The majors were the self-proof chain capture, the incomplete UV-value consumer list, sampler facts, the fallback lifetime change, the missing host-capability refusal, the alpha separation switches, closure over a rewritten channel, and plan order. Design section 17.2 now includes the required changes and five added items. The user accepted correction 11 on 2026-11-19.

### Coordinate precision spike

The 2026-11-19 spike measured coordinate precision on one GPU. Without anisotropic filtering, a candidate rounding bound held in every measured pixel. Half-precision page UVs moved samples by up to four texels, so remapped UVs need Float32. Anisotropic filtering exceeded the bound on rare pixels by about three times. See `docs/superpowers/investigations/2026-11-19-atlasing-coordinate-precision-spike.md`.

### Latest sampling follow-up

The 2026-11-17 follow-up recovered 13 execution batches and 494 GPU comparisons. Each recovered batch reported zero surviving owned objects and restoration of the previous render target. These were disposable characterization comparisons, not Unity Test Runner tests.

The bilinear rule copies complete level-specific sampling neighborhoods. Compression also requires complete blocks and matching phase. In one protected-through-level-two fixture, an eight-pixel border matched before compression but failed after BC3 encoding. A 16-pixel border matched that compressed fixture. Do not turn 16 into a universal constant.

The thin-triangle case retained small translation-sensitive errors despite larger borders. A diagnostic removed the translation and the error disappeared. Another diagnostic used exactly representable probe positions and also matched. Those diagnostics support a precision explanation. They do not establish a universal hardware bound or authorize snapping product UVs.

Repeat boundaries and whole-island offsets matched in the measured cases. Clamp and non-unit texture transforms supplied negative controls. Unequal map resolutions matched with aligned normalized placement. The smaller map needed a larger relative copy neighborhood to preserve its compression blocks.

A repeated grayscale source produced a narrow strict-mode crop candidate. Its encoded payload decreased by about 75 percent. Required RGB matched in complete mip-center comparisons and thin-region probes. Alpha still changed after BC7 encoding. This is not a general mask recipe or permission for opaque conversion.

Strict mode still cannot pack arbitrary independent images through a shared full mip chain. The final one-texel level cannot preserve two different constants. No shader clamp or new periodicity-search system is approved.

One decoded-border diagnostic timed out at the transport. History then showed two successful entries with the same preview. Only its first-level result was recovered. Do not include those entries in the 13 batches or infer their cleanup counts. A tool issue was reported.

The Console query returned two asynchronous-readback errors and three warnings. The spike used render-target readback and did not establish the warnings' origin. Do not claim a clean Console.

### Migration spike

The 2026-11-17 migration spike completed 14 native-prefab assertions and five current-parser scenarios. An absent default-true field loaded true. A distinct explicit false survived. Native save, reload, first edit, editor Reset, and Undo retained the tested behavior.

Prefer ordinary field initializers plus Reset over a new atlas migration marker. This used an existing field as a proxy. Actual atlas fields, preset migration, prefab variants, scene loading, and domain reload remain unproved.

The current preset schema is five and is closed. Missing required fields, unknown keys, and future versions refuse. Add a bounded version migration later. Do not make old required fields optional or silently broaden support for older schemas.

### Earlier evidence

Use these records for source details and measured scope:

- `docs/superpowers/investigations/2026-11-16-atlasing-throwaway-spike.md`: acquisition, generated-input capture, vendor UV probes, and final compressed alpha evidence on fixed layouts.
- `docs/superpowers/investigations/2026-11-16-atlasing-sampling-spike.md`: initial grid-phase, Repeat, shared-tail, and compression falsifiers.
- `docs/superpowers/investigations/2026-11-16-atlasing-normal-mask-spike.md`: normal reduction, angular error, and threshold-sensitive mask cases.
- `docs/superpowers/investigations/2026-11-16-atlasing-vendor-role-contracts.md`: actual normal decoding, alpha equations, and rejected baking shortcuts.
- `docs/superpowers/investigations/2026-11-16-atlasing-vendor-render-spike.md`: real installed vendor normal and alpha rendering, including strength-dependent compression loss.
- `docs/superpowers/investigations/2026-11-16-atlasing-foundation-investigation.md`: code-aware integration findings.
- `docs/superpowers/investigations/2026-11-16-atlasing-feasibility-review.md`: analytical constraints and remaining feasibility gaps.
- `docs/superpowers/investigations/2026-11-16-alpha-controls-and-uv-islands-investigation.md`: earlier alpha-control removal proposals. The user chose to retain those controls.

None of these records establishes universal shader support, safe numerical defaults, a complete packer, or release readiness.

## 5a. Next work after the minimalism review

On 2026-11-19 the user accepted these answers. Alpha separation runs first and atlasing runs afterward as its own pass. Both shader families stay in 0.2.0. Density normalization, the separate alpha mask, strict sampling, the compression matrix, visibility groups, material swaps, spare UV channels, roles beyond Color, locked sources, generated textures, and UV island regions move to later versions. Atlas fields stay on the component and presets stay at schema five until pre-release work fixes the atlas fields. Atlasing runs in Play mode under the same gate as alpha separation. Next work is Gate 0 in section 13 of the minimal design. Section 5 below describes the older plan.

Gate 0 item 2 closed on 2026-11-19 with no blocking finding. See `docs/superpowers/investigations/2026-11-19-atlasing-opaque-alpha-source-check.md`. Every opaque output that alpha separation can make forces alpha to one in every pass. Alpha separation refuses to convert Poiyomi Two Pass.

Gate 0 items 1 through 10 closed on 2026-11-19. Items 3, 4, and 5 closed by throwaway fixtures on a test platform. See `docs/superpowers/investigations/2026-11-19-atlasing-gate-zero-fixtures.md`. Item 1 closed by source review. See `docs/superpowers/investigations/2026-11-19-atlasing-route-tables.md`. Items 6 and 7 closed from source review in the design. Item 8 closed by fixtures and user choices. See `docs/superpowers/investigations/2026-11-19-atlasing-page-fixtures.md`. Item 9 closed by definition in design section 10. Item 10 closed when the user approved the design on 2026-11-19.

## 5. Next work and approval gates

The UV-rounding decision was resolved on 2026-11-18. The coordinate precision spike ran on 2026-11-19. See `docs/superpowers/investigations/2026-11-19-atlasing-coordinate-precision-spike.md`. It gives a candidate bound without anisotropic filtering. It shows that anisotropic filtering can exceed that bound on rare pixels. On 2026-11-19 the user chose to treat those anisotropic differences like the accepted compression risk. Section 15.2 of the design records the decision. A vendor shader route still needs its own check. The anisotropic support border still needs a proof that does not depend on one GPU. Preserve unsupported dependent uses instead of guessing.

Close the quality contract with role-specific numerical limits and supported recipes. Distinguish prepared-source quality from compressed-source equivalence. Keep the colorful compression failures, changed alpha, and sampled-product falsifiers.

Measure retained allocation, peak temporary memory, and cumulative work on bounded public fixtures. Include rejected separate candidates followed by shared fallback. Derive candidate and aggregate growth limits from evidence. Do not infer safety from final page dimensions alone.

The remaining Gate 0 items also cover visibility capture, version-specific UV routes, locked reconstruction, migration, and fixed defaults. They remain open as of 2026-11-18. Obtain isolated review of the revised contracts and explicit user approval of the full design.

Only then write an executable plan for the first milestone. The draft lists eight milestones. Do not implement the entire speculative type map first. Generalize output ownership when the first complete atlas path consumes it.

## 6. Integration facts to recheck against source

Earlier source inspection found preparation and apply inside one animator-services scope. The final unlock-window close runs after that scope commits. Some older comments describe a different barrier arrangement. Follow the actual plugin configuration.

`Editor/Build/AlphaSeparationRecords.cs` contains a source-to-one-opaque-material lookup. It cannot represent different atlas results for different uses. Replace that limiting assumption at the consumer seam. Existing finalization records already represent full renderer assignments and binding-specific curve edits. Keep one final writer.

The runtime component already has cutout backfill behavior and Reset logic. Do not remove the old cutout marker as atlas cleanup. Its migration preserves earlier user values rather than merely supplying new defaults. Do not remove serialized inspector presentation state without a scoped reason.

The existing image evidence primarily establishes alpha. It is not a full color/normal atlas reader. Existing geometry capture is also alpha-focused. Do not assume it preserves every attribute required for a full remap.

The CI test script takes an assembly, not a method-filter argument. Use the Unity Test Runner for focused filters. Never launch another editor against an already open project. No language server was configured at the earlier inspection. Recheck availability before code intelligence work.

Paths in this section are relative to `Packages/com.alrauna.amuse/`. Re-read affected source before editing. Source and repository policy outrank this handoff.

## 7. Worktree ownership and runtime state

Observed on 2026-11-18: The worktree is on main at commit d19cdd4. There are no staged changes. Four tracked files have user-owned changes:

- `Packages/packages-lock.json`.
- `ProjectSettings/EditorSettings.asset`.
- `ProjectSettings/ProjectSettings.asset`.
- `ProjectSettings/QualitySettings.asset`.

The untracked `lac_vpm.json` is also user-owned. Do not overwrite, absorb, or stage these changes.

The untracked design, delivery draft, and investigation records listed above belong to this design session. This handoff adds one more document. No product changes, commits, branch changes, releases, or dependency installations occurred in this session.

Observed on 2026-11-19: A later session added `docs/superpowers/investigations/2026-11-19-atlasing-coordinate-precision-spike.md`. It also edited this handoff and section 15.2 of the design. It made no product changes, commits, branch changes, or asset changes. Its transient Unity objects were all destroyed.

As of 2026-11-18, no retained eval runtime is registered. Earlier Python variables and in-memory C# harness strings are unavailable. Rebuild only the setup needed for the next authorized experiment. Do not assume the old helper functions exist.

No agent result or test job is owed at this handoff. Earlier disposable prefab fixtures were removed. Sampling experiments created no asset files. Recheck live editor routing before any new operation.

## 8. Safety and delivery constraints

Before Unity writes, broad operations, or reported validation, enumerate reachable editors and pin the exact dev project by `Application.dataPath`. Never infer the target from an instance label. Never run tests in the private editor.

Preserve source meshes, materials, textures, import settings, animation assets, prefabs, and scenes. Disposable fixtures must remain public and synthetic. Keep each asset and its metadata together. Never copy vendor assets or source into public fixtures.

Do not record absolute paths, editor identities, ports, private asset names, or other private identifiers. Date status claims. Use simple English. Sweep changed documents for identifiers before delivery.

Do not stage, commit, push, create a PR, publish, or deploy without explicit authorization. Keep the research package out of product and VPM releases. Use the selective eval commit API only if a later user explicitly authorizes a commit.
