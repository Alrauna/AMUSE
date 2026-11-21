# Minimal atlasing delivery plan for 0.2.0

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans for an approved executable task plan. Use superpowers:subagent-driven-development only when the user authorizes that execution method. Steps use checkbox syntax for tracking. This draft does not authorize execution.

Privacy note: This record contains public product planning only. It contains no private asset observations, machine identifiers, or vendor source excerpts.

Date: 2026-11-19.
Status on 2026-11-19: Delivery sequence for the design in `docs/superpowers/specs/2026-11-19-minimal-atlasing-design.md`. It replaces `docs/superpowers/plans/2026-11-16-0-2-0-atlasing-implementation-plan.md`, which stays as a historical record. Gate 0 closed and the user approved the design on 2026-11-19. The executable plan for Milestone 1 is `docs/superpowers/plans/2026-11-19-atlasing-milestone-1-settings-and-gate.md`. It needs owner review before implementation.

**Goal:** Ship atlas infrastructure in 0.2.0: shared pages, safe UV rewrites, and a merge-ready check per page set, for both shader families, with the alpha separation result kept on every use.

**Architecture:** One new extension-free pass after `TransientUnlockWindowClose`. It captures evidence per use, makes pure grouping and placement decisions, generates owned pages, meshes, and materials, checks renderer identity, and writes once. Alpha separation code does not change.

**Tech stack:** C#, Unity 2022.3.22f1, NDMF, NUnit EditMode tests, and the installed vendor packages. No new dependency or assembly.

## Global constraints

- Preserve source meshes, materials, textures, import settings, animation assets, prefabs, and scenes.
- Do not change alpha separation code, except to expose the owned-object list and the lifecycle values that the atlas pass reads.
- Never subdivide triangles. Copy every vertex attribute, bone weight, and blend shape frame when duplication is necessary.
- Never drop alpha that a source has. Never bake tint or animated values into textures.
- Do not add a shader interpreter, a reachability solver, a packing search, a format search, a settings snapshot type, or a result cache.
- Unsupported facts refuse only the dependent use. Programming failures stop the build.
- Public fixtures are deterministic, minimal, and redistributable. Vendor assets stay untracked.
- Pin the exact dev editor asset directory before Unity writes, broad operations, or reported validation. Never run tests in the private editor.
- No staging, commits, publication, or deployment without separate authorization.

## Gate 0

Design section 13 lists the items. Each item gets a dated result, a source or fixture reference, and a pass or a blocking finding. The three live fixtures (writes after the window close, the mip tail, appended slots) run first, because a failure there changes the pass position. Item 2 closed on 2026-11-19 with no blocking finding. It added the alpha channel to the output check.

## Milestone 1: Settings and an empty pass

Executable plan: `docs/superpowers/plans/2026-11-19-atlasing-milestone-1-settings-and-gate.md`. Inspector controls and Play-mode rows move to Milestone 4.

Status on 2026-11-19: Implemented and committed on branch `atlasing-m1-settings-and-gate`. The executable plan above records decisions D1 to D6. On 2026-11-19 the full test assemblies ran in EditMode in the dev editor instance, with no filter. `Alrauna.Amuse.Tests.Editor` ran 3303 tests with 3278 passed, 0 failed, and 25 skipped. The skips are the vendor opt-in gates. `Alrauna.Amuse.Research.Tests.Editor` ran 151 tests with 151 passed and 0 failed. The Console held one `ArgumentOutOfRangeException` with parameter `propertyIndex` after the runs. No test failed. The Editor log shows the same stack in earlier sessions, in the vendor lilToon asset postprocessor during a fixture material import. It does not touch the changed code.

- [x] Settings tests: defaults, an explicit false that survives a round trip, Reset, the fingerprint, and a preset apply that keeps the atlas fields.
- [x] Gate tests: atlasing on and alpha separation off runs without the consent dialog. Both off analyzes, mutates, and reports nothing. A non-Windows target, an unverified host version, and settings outside their ranges each give one named refusal.
- [x] Observe RED. Add the three fields, the pass after the window close, and the gate. Observe GREEN.

## Milestone 2: First complete path, one row

Scope: the Poiyomi 9.3 main color route. Color role only.

Status on 2026-11-20: Implemented on branch `atlasing-m2-poiyomi-main-color`. The executable plan `docs/superpowers/plans/2026-11-19-atlasing-milestone-2-poiyomi-main-color.md` records the seven tasks and the `EmptySubmesh` review ruling. On 2026-11-20 the product and research EditMode assemblies ran in the dev editor instance, with no filter, at commit `f4f3893`. The combined payload reported 3617 total, 3592 passed, 0 failed, and 25 skipped. The skips are the vendor opt-in gates. The GPU reach fixture `AtlasGpuReachTests` re-measured the border reach on 2026-11-20: 1.326 texels at anisotropy level 0 against the border of 1, and 14.564 texels at anisotropy level 16 against the border of 16, each as the Euclidean distance in base level texels. The owner build and VRChat upload of the 8192 fixture avatar is pending owner action since 2026-11-20. The two upload facts, page streaming in the built bundle and VRChat acceptance of the page, are open.

- [x] Write an end-to-end public fixture with two compatible materials and different regions. Assert correct rendered output, unchanged sources, a complete output mesh with Float32 route UVs, one shared material per page, deterministic placement, and an unchanged slot count.
- [x] Write the falsifiers of design section 14 that apply to this row. Include a transparent slot with its opaque clone in different groups, and a refused use that keeps its alpha separation output.
- [x] Observe RED. Implement acquisition through the existing import copy route, the route table, the group key, placement, page generation with the protected-level copy and streaming mip maps, the output check, the mesh and material output, the identity check, the write, and cleanup. Throw on output invariants. Observe GREEN through the real build lifecycle.
- [ ] Build and upload one public fixture avatar with an 8192 page. Record whether the page streams in the built bundle and whether VRChat accepts the page. Pending owner action since 2026-11-20. The test assembly menu item `AMUSE Tests/Create atlas upload fixture` creates the avatar under `Assets/_PoiyomiShadersAtlasUpload`, a folder the gitignore already ignores. Its uncompressed RGB24 page of 8192 by 4096 texels is about 134 MB with its mip chain.

## Milestone 3: Second row

Executable plan: `docs/superpowers/plans/2026-11-20-atlasing-milestone-3-liltoon-main-color.md`.

Status on 2026-11-21: Implemented on branch `atlasing-m3-liltoon-main-color`. The full product and research EditMode run on 2026-11-21 reported 3687 total, 3662 passed, 0 failed, and 25 skipped.

- [x] Add the lilToon main color route table with one falsifier per list entry. Add the end-to-end fixture for lilToon. Observe RED and GREEN.

## Milestone 4: Report and interface

Executable plan: `docs/superpowers/plans/2026-11-21-atlasing-milestone-4-report-and-interface.md`.

Status on 2026-11-21: Implemented on branch `atlasing-m4-report-and-interface`. The full product and research EditMode run on 2026-11-21 reported 3697 total, 3672 passed, 0 failed, and 25 skipped. The real inspector was checked in the dev editor instance on 2026-11-21.

- [x] One report line per page set with the merge-ready result and the allocation change with its cause. One line per refused use. The dry run label states that the preview excludes atlasing. Verify the actual inspector.

## Milestone 5: Release qualification

Executable plan: `docs/superpowers/plans/2026-11-21-atlasing-milestone-5-release-qualification.md`.

Status on 2026-11-21: Implemented on branch `atlasing-m5-release-qualification`. The budget values are a group factor of 5/4, an avatar limit of 524,288,000 bytes, and a peak limit of 4,294,967,296 bytes. The full product and research EditMode run on 2026-11-21 reported 3699 total, 3674 passed, 0 failed, and 25 skipped. The relock fixture ran 30 tests with its gate set on 2026-11-21, and all 30 passed.

- [x] Set the three budget values from measurements of production code on public fixtures. Record each value with its reason.
- [x] Run the product and research EditMode assemblies in the pinned dev project. Record the observed counts. A filtered run with zero tests fails.
- [x] Run the relock regression fixture behind the vendor opt-in gate.
- [x] Update `README.md` with the supported cases, the refusals, the one-GPU anisotropy limit, and the statement that 0.2.0 does not reduce draw calls. Sweep changed documents for private identifiers.

## Coverage

| Design section | Milestone |
| --- | --- |
| 2 Pipeline position | Gate 0, 1 |
| 3 Decisions | 1, 2 |
| 4 Admission and refusals | 1, 2, 3 |
| 5 Route table | 2, 3 |
| 6 Group key | 2 |
| 7 Placement and pages | 2 |
| 8 Sampling and output | Gate 0, 2 |
| 9 Mesh and write | 2 |
| 10 Budget | Gate 0, 2, 5 |
| 11 Settings and report | 1, 4 |

Next action on 2026-11-21: The owner builds and uploads the 8192 fixture avatar through the VRChat SDK and records the two upload facts. That is the last open item before the 0.2.0 release decision.
