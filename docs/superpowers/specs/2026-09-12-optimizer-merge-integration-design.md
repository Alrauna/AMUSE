# Optimizer merge integration test design

Privacy note: This design uses sanitized scene traits and public synthetic inputs. It contains no private asset data or identifiers.

Date: 2026-09-12.
Status on 2026-09-12: The user approved the layered approach. Detailed test work and production changes are not implemented.
Evidence: [Optimizer merge investigation](../investigations/2026-09-12-optimizer-merge-evidence-isolation.md).
Execution: [Test implementation plan](../plans/2026-09-12-optimizer-merge-integration-plan.md).
Inspected branch: `chore/harden-integration-testing` at `578b5eb`.
Inspected integration base: `main` at `d05171d`.

## Goal

Tests must catch transparent triangles that become opaque because another optimizer merges their renderers. They must also show that eligible opaque triangles still convert.

The fixture must reproduce relevant interactions from the Census Lab scene. It must not copy the scene or reproduce its exact hierarchy and geometry counts.

A predicate is a rule that classifies a texture sample. A capture route is the method that obtains texture evidence. An oracle is an expected result independent of AMUSE classification.

## Scope

The approved design has three levels:

1. Capture regressions that expose the confirmed evidence-isolation defect.
2. Mixed-material renderer tests that exercise production capture, resolution, and application.
3. Actual AAO and DAO pipeline tests that establish merging and final behavior.

Private source inspection remains read-only. Final private visual acceptance follows a tested production fix. No automated product tests run in the Census Lab project.

This design does not authorize a general optimizer framework, a shader compiler, an optimizer-specific refusal, or changes to source assets. It does not certify every configuration of AAO or DAO.

## Required behavior

### R1. Preserve the material meaning

A transparent material with inert alpha clamps requires full-opacity evidence. A cutout material that survives its cutoff does not establish full opacity for a transparent sibling.

The transparent result must remain unchanged when a cutout material joins the renderer. This rule applies to distinct textures and shared textures.

### R2. Preserve predicate identity

Capture reuse must distinguish contexts that produce different predicates. A source texture identity alone does not identify a thresholded field.

The distinction must survive request selection, capture reuse, field lookup, runtime material-state resolution, and application. Do not fix one boundary while another collapses the fields again.

### R3. Preserve local uncertainty

Missing evidence must keep dependent triangles on their original material. Unrelated eligible slots must still convert. Blanket refusal of mixed-mode renderers is not successful support.

Existing missing-evidence regressions remain valid. Present evidence with the wrong predicate needs separate regressions.

### R4. Require actual optimizer execution

AAO integration must use Trace and Optimize with automatic skinned-mesh merging. A hand-built merged mesh or an explicit Merge Skinned Mesh component cannot substitute for that path.

A required integration run must fail if the expected renderer merge does not occur. It must also fail if texture optimization is the selected case but no texture or UV transformation occurs.

DAO integration must execute its SDK callback in the applicable SDK sequence. A direct NDMF build cannot prove DAO execution.

### R5. Require successful builds

The fixture must produce a successful build without unexpected errors. Do not permit an unsuccessful build because later passes still ran.

Do not suppress callback failures, accept zero tests, or count missing-package skips as integration coverage. A required profile must fail its environment check when dependencies are missing.

### R6. Preserve triangle identity and accounting

The expected result belongs to each synthetic triangle, not to a material count or UV corner. Identify synthetic triangles through their distinct geometry positions.

Match all expected triangles after each build. Require a one-to-one match. Reject missing triangles, duplicate triangles, unexpected triangles, and unmatched output slots.

Do not use private names or optimizer-generated names as identity. Do not infer an opaque conversion only from a material name.

### R7. Use an independent alpha oracle

The base fixture contains separate zero-alpha, partial-alpha, and full-alpha regions. Its expected labels come from the authored regions and the chosen policy.

A second texture contains a non-opaque interior region inside a triangle whose vertices sample opaque texels. This defeats a vertex-only oracle.

A mip case contains binary detail that becomes partial alpha at a consulted coarse mip. This defeats a mip-zero-only oracle.

After UV packing, compare triangle identities with the original expected labels. Do not apply the original UV partition to remapped UV coordinates.

### R8. Preserve runtime behavior

The scene-shaped case includes skinning, blend shapes, visibility curves, shared materials, multiple slots, and inactive geometry. Bone weights and bind poses must be valid.

A material-swap case is a separate adversarial extension. It is not claimed as an observed source-scene trait. It must test a slot whose alternatives need different alpha predicates.

Test state transitions through committed animation bindings. Do not test only an empty controller or a count of rewritten curves.

### R9. Match the capture environment

The scene-shaped case includes unreadable streaming textures and a generated texture path. Generated textures must satisfy the actual producer and container checks.

The test must establish the route that it intends to exercise. Assigning an AAO-like texture name is not sufficient.

Record the declared mip chain, consulted mip policy, and route outcome for public synthetic fixtures. Do not change private texture imports to make a test pass.

### R10. Preserve useful optimization

Each positive case includes known safe triangles that must convert. A test must fail if AMUSE avoids the defect by converting nothing.

At least one sibling slot must remain independently eligible when another slot lacks evidence. Both material orders must produce the same per-triangle result.

### R11. Preserve source assets and teardown

Build disposable copies of public synthetic avatars. Keep a separate source fixture for preservation assertions.

Compare source mesh data, material properties, textures and importer configuration, and animation curves before and after the build. Use owned temporary asset folders with collision guards.

Restore temporary global configuration in `finally`. Delete only assets that the test owns. Never use a broad asset save or cleanup to hide fixture errors.

DAO deletes and recreates its generated output folder at optimization start. Run DAO cases only in a disposable public integration project. A copied avatar inside a shared project does not isolate that deletion.

### R12. Preserve package boundaries

Core regressions must use redistributable fixtures without installed vendor shaders. Existing verified attestation seams are permitted only at that boundary.

Real optimizer tests must use the installed optimizer and real attested lilToon shader. They must not mock mesh merging, material capture, predicate generation, classification, or application.

Research tooling must not enter the product package. Tests and synthetic NDMF observer plugins must remain excluded from releases.

## Fixture design

### Small causal fixture

Use a 128 by 128 texture with explicit alpha bytes `0`, `128`, and `255`. Use a nondegenerate triangle wholly inside each region. Keep each triangle away from filtering boundaries.

Use a transparent material with positive cutoff `0.01`. Use a cutout material with cutoff `0.25`. These are synthetic values, not copied private material values.

Test distinct textures first. Then share one texture between the materials. Add two cutout materials with different cutoff values to expose minimum-threshold reuse independently of shader family.

Use inert alpha bounds. Keep the original cutoff behavior of each material. Missing texture evidence must not be the cause of the failing positive case.

### Actual merge fixture

Extend the existing AAO fixture rather than create a general fixture system. Use several child renderers with unique public names and distinct geometry positions.

Provide valid bone weights, bind poses, root bones, normals, and bounds. Use compatible probe configuration. Attach a VRChat descriptor and explicit valid animation layers.

Include transparent, cutout, and opaque material roles. Some renderers share a material. Other renderers use distinct textures with identical alpha regions.

The core oracle uses geometry identities and expected opacity labels. It does not depend on source material object identity after upstream cloning.

### Scene-shaped fixture

Add a small rig, blend shapes, visibility animation, inactive geometry, multiple material slots, and separate main and mask textures. Include a saturated multiply mask and a non-inert mask case.

Use the installed versions from the investigation. Keep this fixture separate from the small failure signal.

Add one synthetic scale case with 72,000 vertices. This crosses the 16-bit index boundary. The count is a test design value, not a private avatar measurement.

## Required test matrix

| Case | Load-bearing input | Required result |
| --- | --- | --- |
| Capture A | Separate requests, shared generated texture | Transparent partial triangle remains transparent |
| Capture B | Two cutout thresholds on one texture | Each material receives its own predicate |
| Renderer A | Transparent and cutout slots, distinct textures | Joining slots does not change transparent labels |
| Renderer B | Same inputs with reversed slot order | Identical per-triangle outcome |
| Renderer C | Shared source with different predicates | Field lookup does not use a sibling chain |
| Renderer D | Missing evidence beside a valid slot | Local preservation and useful sibling conversion |
| Geometry A | Opaque corners with a non-opaque interior | Interior triangle remains transparent |
| Sampling A | Partial alpha only at a consulted mip | Triangle remains transparent |
| AAO A | Merge disabled, other configuration fixed | Baseline labels and expected source partition |
| AAO B | Automatic merge enabled, texture optimization disabled | Actual merge and correct labels |
| AAO C | Automatic merge and texture optimization enabled | Actual merge, transformed texture or UVs, correct labels |
| DAO A | Applicable SDK callback sequence | Actual DAO execution and correct final labels |
| Combined A | AAO, AMUSE, and DAO in the applicable sequence | Correct labels before and after DAO |
| Animation A | Visibility and blend-shape transitions | Supported transitions preserve the fixture result |
| Animation B | Material alternatives with different predicates | Every admitted alternative preserves partial alpha |
| Scale A | Synthetic 32-bit index mesh | Complete triangle accounting and correct labels |

This matrix is not a Cartesian product. Each row protects a distinct failure mechanism or compatibility boundary.

## Observation points

Use test-only NDMF observation at the end of `Optimizing` and after AMUSE application. Pin the actual phase and ordering constraints. Do not put observers in production.

At the first point, establish the merged renderer state and material-mode mixture. At the second point, compare the complete triangle oracle. After the DAO callback, compare that oracle again.

For AAO texture cases, establish that the texture or UV transformation actually happened. For source-preservation checks, compare the untouched source fixture separately.

Public diagnostics must identify the failed stage and the expected behavior. Avoid large dumps of every renderer and material when a failed assertion already identifies the boundary.

## Branch code disposition

Replace the invalid AAO fixture and its corner-only oracle. Remove diagnostic-only tests when behavioral tests cover their useful condition.

Replace the registry-reflection test with a build-survival test for the AMUSE component. Retain or remove the small component-information registration only after that test establishes its effect.

Do not rewrite unrelated tests or remove valid refusal coverage. Reassess tests that demand exactly one capture call if correct predicate isolation changes capture grouping. Capture call count is not the behavior under test.

## Production fix boundary

The investigation confirms a generic defect already present on `main`. Follow the repository prerequisite rule. Implement that repair separately from the optimizer-harness branch after an approved production repair plan.

The repair must satisfy R1–R3 at all three lossy boundaries. It must preserve existing cutout semantics and useful conversion. A per-renderer refusal, a new AAO identity check, or a minimum-cutoff cache patch alone cannot satisfy the contract.

This document does not select a new public API or a general predicate representation. The test plan ends at an explicit repair-plan gate after the failing tests exist.

## Acceptance and evidence

The required profile must report observed pass, failure, and skip counts. A skipped mandatory case fails acceptance. Record the exact tool versions and build path.

Before declaring support, require all of the following:

1. Observe the capture and mixed-renderer regressions fail before the fix.
2. Observe the real AAO merged case fail on the relevant triangle outcome rather than on fixture admission.
3. Observe the same tests pass after the fix.
4. Keep a positive opaque-conversion control in every relevant case.
5. Run the product and research EditMode assemblies after integration.
6. Exercise both the supported SDK build path and the play-mode path used by the report.
7. Observe the original private visual scenario after the fix.
8. Make sure that source assets and unrelated workspace changes remain intact.

Do not convert the public capture experiment into a claim that the complete private failure is fixed. The private visual gate remains necessary.
