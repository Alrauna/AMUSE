# AMUSE Rebrand and Architectural Pivot Design

**Date:** 2026-08-15

**Status:** Awaiting approval

## Decision summary

Rename the project to **AMUSE — Alrauna's Material Understanding & Simplification Engine** in one coherent migration. Keep all implemented classifier, fixture, and separation-planning behavior. Use these permanent identities:

| Surface | Selected identity |
|---|---|
| Product name | `AMUSE` |
| Full product name | `Alrauna's Material Understanding & Simplification Engine` |
| VPM package ID | `com.alrauna.amuse` |
| Unity package folder | `Packages/com.alrauna.amuse/` |
| C# root namespace | `Alrauna.Amuse` |
| Production assembly | `Alrauna.Amuse.Editor` |
| Test assembly | `Alrauna.Amuse.Tests.Editor` |
| Recommended GitHub repository | `Alrauna/amuse` |
| Architecture document | `docs/architecture/vision.md` |

This is a clean identity change. It is not a compatibility shim and not a redesign of the implemented algorithms. The GitHub repository rename, repository-variable changes, publishing, and releases stay separate external actions. Each needs separate authorization.

## Verified baseline

- PR #5 `Add immutable separation planning` is in `main` at commit `7414672`.
- Local `main`, `origin/main`, and the base of this branch are all `7414672`.
- Design branch: `chore/amuse-rebrand`.
- The worktree was clean before this design document was written.
- The repository has 135 tracked files. Thirty-five tracked paths contain the old package or assembly identity. They are inside the old package directory or have old asmdef filenames.
- The public Unity project is `<repo-root>`. It runs Unity `2022.3.22f1`.
- Read-only Unity MCP discovery found 91 tests. It showed the public editor idle and not compiling. The private `AlphaMaterialOptimizer-Testbed` instance was not selected. Inspection stopped at instance identity.
- All 52 tracked `.meta` files contain GUIDs. No duplicate GUID exists in the current tree.
- Connected GitHub repository metadata confirms the public repository `Alrauna/alpha-material-optimizer-ndmf` with default branch `main`. The local `gh` CLI is not authenticated. Thus repository variables and release history were not inspected through `gh`.

## Problem and pivot

The current repository identity defines the whole product as one alpha/material optimization. The implemented exact alpha classifier and immutable mesh-separation planner remain valuable, but they are now foundational AMUSE subsystems, not the product boundary.

AMUSE aims to understand material, texture, rendering, and state semantics well enough to propose and apply behavior-preserving simplifications. Unity/NDMF is the current host and transformation integration, not the conceptual definition of AMUSE.

The migration must change human branding and machine identities. It must not pretend that future semantic analysis, shader adapters, atlasing, animation tracing, material combining, or transformation passes already exist.

## Goals

- Establish one coherent AMUSE identity across current policy, package metadata, paths, namespaces, assemblies, tests, documentation, and user-visible listing material
- Preserve exact alpha-classification and separation-planning semantics byte-for-byte except for identity strings and paths
- Generalize the project-level safety policy from alpha-specific outcomes to proof-first behavior preservation, and keep the current alpha subsystem's exact contract
- Make host-neutral immutable analysis/planning versus Unity/NDMF extraction/transformation an explicit architectural property
- Distinguish current implementation from future direction
- Preserve Unity asset identity
- Verify package resolution, compiler/test discovery, NDMF bootstrap, and all relevant tests after the later implementation
- Keep dated design and implementation records historically accurate

## Non-goals

This migration does not implement or scaffold:

- semantic material IR, shader adapters, or modifier registries
- animation or state tracing
- atlasing, UV packing, dilation, or mip analysis
- material normalization or combining
- generalized render-mode classification
- optimizer orchestration or public plugin APIs
- mesh/material transformation or a new NDMF pass
- a standalone shared library
- compatibility shims without evidence of an installed-user requirement
- a full visual identity system

## Considered approaches

### 1. Coherent identity change — selected

Rename the package ID and folder, namespaces, assembly identities, current documentation, policy, and current user-visible metadata in one migration. Preserve dated historical documents and all `.meta` GUID contents.

This approach avoids a permanently split identity. The choice fits because production code is internal and Editor-only, with no serialized runtime-facing components in the tracked project.

### 2. Branding-only rename with old machine identities

Change README and display strings but keep `com.alrauna.alpha-material-optimizer` and `Alrauna.AlphaMaterialOptimizer`. This reduces immediate churn but leaves the obsolete identity in every dependency, assembly reference, and API name. It is rejected because the project is early and no compatibility requirement justifies the debt.

### 3. Multi-branch partial migration

Rename policy/docs first, then package identity, then namespaces/assemblies. This produces intermediate branches where documentation, package resolution, and compiler identities contradict one another. It is rejected for integration. The implementation plan still uses reviewable task checkpoints on one branch, but the branch is validated and integrated only as a coherent whole.

## Proof-first safety invariant

The general AMUSE default is:

```text
prove behavior-preserving under the active optimization policy -> transform
uncertain or unsupported -> preserve and explain
```

Additional uncertainty must never make a transformation more aggressive. The current alpha subsystem is a concrete instance of this rule:

- `TriangleAlphaOutcome.ProvenOpaque` can become an opaque candidate
- `MustRemainTransparent` stays on the transparent path
- `Unknown` stays on the transparent path
- malformed normalized inputs throw an error, with no repair or guess

The migration does not rename or generalize these outcomes. Future AMUSE modules can use different domain-specific evidence and result types. They still obey the broader invariant.

## Current implementation inventory

### Package and metadata

The current package metadata is at `Packages/com.alrauna.alpha-material-optimizer/package.json`:

- package ID `com.alrauna.alpha-material-optimizer`
- display name `Alpha Material Optimizer`
- version `0.0.1`
- a narrow alpha-separation description
- an author URL that points to `Alrauna/alpha-material-optimizer-ndmf`
- NDMF dependency `>=1.14.4 <2.0.0-a`

`Packages/packages-lock.json` records the embedded package under the old ID. The record uses `file:com.alrauna.alpha-material-optimizer`. `Packages/.gitignore` explicitly allows the old folder. `Packages/manifest.json` does not list the embedded package. `Packages/vpm-manifest.json` contains only the NDMF dependency and lock. Neither file currently contains the project's old ID.

### Production code

The package has one Editor-only production assembly and three production source files under `Editor/Analysis/`:

- `ExactUvGeometry.cs` — exact dyadic/rational geometry support
- `TriangleAlphaClassifier.cs` — exact conservative triangle/texture-alpha classification
- `MeshSeparationPlanner.cs` — immutable normalized topology input and deterministic separation-candidate planning

The current assembly is `Alrauna.AlphaMaterialOptimizer.Editor`. `AssemblyInfo.cs` grants internals access to `Alrauna.AlphaMaterialOptimizer.Tests.Editor`.

### Tests and fixtures

The Editor test assembly and root namespace are `Alrauna.AlphaMaterialOptimizer.Tests.Editor`. Current tests cover:

- exact classifier behavior for Point/Bilinear and Clamp/Repeat
- missing, degenerate, and unsupported uncertainty
- immutable mesh-separation planning and material-binding provenance
- deterministic reference-fixture catalogs and Unity fixture construction
- package test-infrastructure smoke behavior

`ReferenceFixtureData.cs` has two hard-coded package-relative catalog paths. They must move with the package.

### Tooling, workflows, and listing assets

- `Tools/Bootstrap-NdmfStandalone.ps1` derives the project and package locations it needs and contains no old product identity. Its behavior should stay unchanged.
- Release/listing workflows do not hard-code the old package ID. They consume the external GitHub Actions variable `PACKAGE_NAME`, use it as `Packages/${PACKAGE_NAME}`, and derive ZIP and `.unitypackage` artifact names from it.
- `Website/index.html` still has the generic title `VCC Listing`.
- `Website/banner.png` visibly says `VCC Example Listing`. This is not an old AMO name, but it is stale template branding. Replace it with a minimal AMUSE banner during this migration.
- `ProjectSettings/ProjectSettings.asset` still uses template values `companyName: VRChat` and `productName: vpm-package-maker`. The public development project should use `Alrauna` and `AMUSE`.

### Current documentation and policy

- `README.md` is only a narrow alpha-separation description plus NDMF bootstrap instructions.
- `AGENTS.md` defines the permanent project too narrowly. It also contains an obsolete early-baseline paragraph about placeholder code and no repository tests.
- No durable architecture/vision document exists today.

### Historical documents

The six dated files under `docs/superpowers/specs/` and `docs/superpowers/plans/` describe the reference-fixture, exact-classifier, and separation-planner work as it was designed and implemented. They contain 105 matching lines in the old-identity audit and some hard-coded historical project paths and test names.

Keep these files unchanged. They are implementation provenance, not current policy. The new AMUSE design, plan, README, and vision document give the present context. Exhaustive stale-name validation must report these dated records as intentional historical occurrences. It must not rewrite them.

## Canonical branding and descriptions

Use `AMUSE` as the normal short name. At the first meaningful introduction in major user-facing documentation, use:

> **AMUSE — Alrauna's Material Understanding & Simplification Engine**

Use ASCII `'` in metadata. Do not use `AMUSE Engine`, `AMUSE Toolkit`, or other redundant suffixes.

Candidate package descriptions:

1. **Recommended:** `A Unity/NDMF material optimization engine that analyzes and simplifies material, texture, and rendering usage while preserving behavior.`
2. `An intelligent material optimization engine for analyzing, simplifying, and transforming material and texture usage while preserving rendering behavior.`
3. `A proof-oriented Unity material optimization engine for behavior-preserving simplification of material, texture, and rendering usage.`
4. `A modular material understanding and simplification system for safe rendering optimization in Unity.`

Package metadata should use option 1. It communicates the broad AMUSE identity and names today's Unity/NDMF host accurately. The README and vision document can describe the broader host-neutral architecture, with explicit current/future labels.

## Package ID and VPM consequences

Select `com.alrauna.amuse`.

Unity defines a package `name` as its unique identifier. Unity explicitly states that an update cannot rename a package. A renamed package is a new package. VPM repository listings also use the package name as their key. The VPM CLI adds and removes packages by that unique name.

Consequences:

- Existing installations of `com.alrauna.alpha-material-optimizer` will not see `com.alrauna.amuse` as an in-place upgrade.
- The AMUSE install does not automatically remove the old package.
- Without explicit conflict metadata, both IDs can be present. Distinct renamed assembly identities avoid an immediate duplicate-name compile error. But duplicate optimizer code/tests or future integration stays unsupported.
- The supported migration is explicit. Back up the project. Confirm that no external code depends on the old assemblies/namespaces. Then remove the old package and add `com.alrauna.amuse`.
- Do not add `legacyPackages`, forwarding assemblies, namespace aliases, or dual package publication. Add them only with evidence that an installed user population needs them.
- A generated repository listing can keep old releases under the old package key. It can add AMUSE under the new key. Validate the generated listing deliberately. Do not delete published releases as part of this migration.
- Change the external `PACKAGE_NAME` repository variable to `com.alrauna.amuse` only as a separately authorized GitHub setting change. Otherwise release and listing workflows will read the removed old folder and produce old artifact names.

References:

- VRChat VPM package format: <https://vcc.docs.vrchat.com/vpm/packages/>
- VRChat VPM repository format: <https://vcc.docs.vrchat.com/vpm/repos/>
- Unity package manifest identity: <https://docs.unity3d.com/2022.3/Documentation/Manual/upm-manifestPkg.html>
- Unity versioning/rename rule: <https://docs.unity3d.com/2021.1/Documentation/Manual/upm-semver.html>

## Package-folder and Unity asset migration

Move:

```text
Packages/com.alrauna.alpha-material-optimizer/
    -> Packages/com.alrauna.amuse/
```

Use Git-visible moves. The package root itself has no tracked `.meta` file. Every asset below it keeps its existing `.meta` content and GUID.

Rename each asmdef file and its `.meta` partner together:

```text
Alrauna.AlphaMaterialOptimizer.Editor.asmdef
    -> Alrauna.Amuse.Editor.asmdef

Alrauna.AlphaMaterialOptimizer.Tests.Editor.asmdef
    -> Alrauna.Amuse.Tests.Editor.asmdef
```

Preserve at least these current identities exactly:

| Logical asset | Current GUID |
|---|---|
| `package.json.meta` | `c474695b7921e8141b9c57e2795b9a33` |
| production asmdef `.meta` | `2cca9ae73dfb9a84fa800e585fe3a948` |
| test asmdef `.meta` | `c28e6dde4cd041b4c87cc087e3b1094c` |
| `Editor.meta` | `49d21c5d62bd5be4fb8ead5191760364` |
| `Tests.meta` | `ff89996f8d255674f8fe2ad4eee5c5b8` |

Capture the complete path-to-GUID map before the move. After the move, compare it by package-relative suffix. Rerun duplicate-GUID detection. Never regenerate `.meta` files to make the move import.

## Namespace and assembly migration

Rename consistently:

```text
Alrauna.AlphaMaterialOptimizer.Editor.Analysis
    -> Alrauna.Amuse.Editor.Analysis

Alrauna.AlphaMaterialOptimizer.Tests.Editor
    -> Alrauna.Amuse.Tests.Editor
```

The two assembly identities become `Alrauna.Amuse.Editor` and `Alrauna.Amuse.Tests.Editor`. Update the test asmdef reference and the `InternalsVisibleTo` string in the same coherent edit.

Preserve the existing two-assembly boundary. Do not add Core, Host, Shader, Animation, Atlas, Combining, or orchestration assemblies. Logical boundaries and documentation are sufficient until implemented code needs a physical split.

The compatibility risk is limited but real:

- tracked production types are internal and Editor-only
- the tracked `Assets/` tree contains no scenes, prefabs, or serialized AMO components
- assembly GUIDs stay preserved
- external source code that directly references the old assembly or namespace names will break and must migrate
- this design makes no claim about uninspected external projects

## Behavior-preservation contract

Only identifiers, paths, metadata, policy, and documentation change. Do not refactor production logic or rewrite tests during the rebrand.

Preserve:

- exact float-to-dyadic and rational geometry
- Point/Bilinear support semantics
- Clamp/Repeat and mathematical floor/modulo behavior
- `ProvenOpaque`, `MustRemainTransparent`, and `Unknown` semantics
- support-region budget behavior
- deterministic fixture inputs and expectations
- immutable input/output copying
- source triangle ordinals, winding, ordering, and material-binding provenance
- monotonic safety and malformed-input behavior

Fixture JSON content and production algorithm bodies should show no semantic diff. Test method names should stay unchanged. Only namespaces, usings, assembly names, and package-relative paths change.

## `AGENTS.md` migration audit

`AGENTS.md` remains agent policy, not the product roadmap.

| Current section | Category | Proposed treatment |
|---|---|---|
| Title | Rename mechanically | Rename to `AMUSE Agent Policy`. |
| Project and repository boundaries | Architecturally revise | Introduce AMUSE and `com.alrauna.amuse`. Describe Unity/NDMF as the current host. Name the new package path. Keep the public fixture/legal boundaries. Replace the obsolete placeholder/no-tests baseline with concise current-state wording and a reinspect rule. |
| Private Unity avatar testbed | Preserve unchanged | Keep the read-only-by-default external-test-appliance rules and all publication/mutation prohibitions. Do not turn its external historical name into product branding. |
| Start-of-task discipline | Preserve unchanged | Keep all five pre-edit checks and the scope rules. |
| Branch and Git discipline | Preserve unchanged | Keep the protected `main`, clean branch, unrelated-work, validation, and no-commit/push rules. |
| Development approach | Architecturally revise | Keep narrow vertical increments. Remove the old alpha-only phase roadmap. Point detailed future architecture to `docs/architecture/vision.md`. State that implemented alpha analysis/planning is one subsystem. |
| Superpowers and Ponytail | Preserve unchanged | Retain the workflow priority, installed-doc inspection, correctness hierarchy, and minimalism constraints. |
| Safety and behavior preservation | Architecturally revise | Generalize to proof of behavior preservation under the active policy. Preserve fail-closed uncertainty, actionable diagnostics, nondestructive build output, and the current alpha-specific conservative behavior as an example. |
| Source and dependency boundaries | Architecturally revise narrowly | Add the host-neutral immutable analysis/planning versus Unity/NDMF extraction/transformation seam. Keep the Editor-only preference, the dependency ladder, license/attribution, and no premature standalone library. |
| Unity asset integrity | Preserve unchanged | Keep the asset/`.meta` pairing, GUID safety, generated-state exclusions, manifest/lock scrutiny, and private local-reference rules. |
| Testing policy | Architecturally revise narrowly | Preserve all test requirements. Broaden the deterministic examples beyond alpha. Retain the analysis -> planning -> transformation attribution and the public/private fixture boundaries. |
| Unity MCP use | Rename and revise narrowly | Change the product package path. Distinguish AMUSE core concepts from Unity/NDMF integration. Preserve instance discovery, public/private targeting, read-only preference, and the CoplayDev exclusion from product dependencies. |
| CI and release safety | Preserve unchanged | Keep public reproducibility, the cheapest deterministic gates, private dependency prohibitions, and the separately authorized publishing/settings rules. |
| Completion standard | Preserve unchanged | Keep the line-by-line requirements review, staged/unstaged scope checks, observed validation, and explicit reporting. |

The migration should not remove a whole safety section. Removal is limited to this obsolete text:

- the stale early-baseline claim that production is placeholder code with no tests
- the alpha-only permanent development roadmap

New AMUSE-level guidance is limited to repository identity, proof-first transformation, current-versus-future scope, the host-neutral/host-specific seam, and the vision-document pointer. Do not enumerate every shader family or future optimizer in `AGENTS.md`.

After editing, search `AGENTS.md` explicitly for every old identity variant. Verify zero stale product references.

## README and current documentation

Rewrite `README.md` around two explicit layers.

### Vision

Introduce AMUSE and describe the goal: understand material, texture, rendering, and state semantics well enough to enable safe simplification. Mention shader semantic adapters, modifier semantics, state/animation analysis, texture/atlas planning, material normalization/combining, alpha/overdraw analysis, and combined planning. Mark all of these as future direction only.

### Current implementation

State that AMUSE is in early development. It currently has:

- deterministic reference fixtures
- exact proof-oriented triangle alpha classification for implemented sampling/wrap semantics
- immutable behavior-preserving separation-candidate planning with explicit provenance
- Editor-only Unity tests and NDMF development/bootstrap infrastructure

State plainly that no automatic avatar transformation, atlasing, shader adapter, animation tracing, or material-combining pipeline is implemented yet. Preserve the working NDMF bootstrap instructions.

## Architecture/vision document

Create `docs/architecture/vision.md` as a durable north-star document, not an implementation plan. It should cover:

- AMUSE purpose and current status
- the proof-first, lossless-by-default transformation policy
- the flow: host extraction -> normalized immutable inputs -> semantic understanding -> analysis modules -> candidates -> combined planning -> safety/compatibility/cost evaluation -> host transformation
- shader-specific semantic adapters
- recognized modifier semantics and fail-closed unknown modifiers
- future material state/animation relationships
- future texture use, atlas, normalization, and combining direction
- the exact alpha classifier and separation planner as the implemented alpha subsystem
- Unity/NDMF as the current host
- portability as a design property, not a reason to extract a standalone library now
- future policy levels only as explicit, auditable opt-ins. The default stays behavior-preserving.

Do not prescribe empty directories, interfaces, registries, schemas, dependency injection, or plugin APIs.

## NDMF positioning and portability

The intended direction is:

```text
host/application extraction
        -> normalized immutable renderer/material/state inputs
        -> shader and recognized-modifier semantic understanding
        -> independent analysis modules
        -> optimization candidates
        -> combined optimization planning
        -> safety, compatibility, and cost evaluation
        -> host-specific transformation
```

Unity/NDMF currently owns extraction/build integration and future Unity-specific transformation. Pure analysis and planning should not depend unnecessarily on live Unity objects, Editor state, assets, NDMF state, or MCP. The current classifier/planner already show that seam.

Do not physically extract a shared library until a second real host or consumer requires it.

## Package version recommendation

Keep `0.0.1`.

The version is already early/experimental. The new package ID creates a distinct package lineage. A change of identity and version together, without a release-policy reason, would add noise, not compatibility. Reconsider only if a separately approved publication plan establishes a concrete versioning rule or finds a conflicting published `com.alrauna.amuse@0.0.1`.

## GitHub repository rename

Recommend `amuse`, not `amuse-unity`.

The repository contains the current Unity host. But the project should keep it as the umbrella AMUSE source. The name should not permanently encode Unity, VRChat, or NDMF. Use `amuse-unity` only if the organization later splits host implementations into separate repositories.

The external rename is not part of the tracked migration. After a separately authorized rename:

```powershell
git remote set-url origin https://github.com/Alrauna/amuse.git
git fetch --prune origin
git remote -v
git status --short --branch
```

Update or verify:

- the `package.json` author URL
- the repository description and homepage
- the Pages/VPM listing URL and the impact on existing subscribers
- `PACKAGE_NAME=com.alrauna.amuse`
- branch protection and Actions variables/secrets/environments
- any external listing source that names `Alrauna/alpha-material-optimizer-ndmf`
- release artifact names and generated repository listing keys
- local clones and the Unity MCP instance name/path, if the local directory is renamed later

GitHub commonly redirects old repository URLs after a rename. Current files should still use the canonical new URL. Verify Pages/listing behavior. Do not assume it.

## Adjacent branding decisions

- Update the Unity development-project `companyName` to `Alrauna` and `productName` to `AMUSE`.
- Update the listing page title to `AMUSE Package Listing`.
- Replace the generic `VCC Example Listing` banner with a simple AMUSE wordmark plus the full product name. Do not invent a broader logo system.
- Leave the favicon unchanged during this migration. No approved AMUSE icon exists, and the favicon has no searchable old product identity.
- Keep workflow names such as `Build Release` and `Build Repo Listing`. They describe jobs, not the old product.

## Validation design

### Before the move

- Record branch, base, and status.
- Record every tracked old-identity occurrence and old-identity path.
- Record a path-to-GUID map for all 52 `.meta` files.
- Record current package metadata, assembly references, and test discovery names/count.

### After the move

- Parse every changed JSON file.
- Verify package folder, ID, lock entry, asmdef names/references, friend assembly, namespaces, fixture paths, and test fully-qualified names.
- Verify that `Packages/manifest.json` and `Packages/vpm-manifest.json` stay unchanged unless Unity proves a necessary resolver change.
- Compare `.meta` GUIDs by package-relative logical asset. Require 52 GUID entries. Require zero duplicates.
- Inspect Git rename detection. No package asset should appear as delete plus unrelated create because of a lost identity.
- Run the NDMF bootstrap twice. Verify idempotence.
- Through Unity MCP:
  1. Rediscover instances.
  2. Select only the actual public project path.
  3. Refresh/import assets.
  4. Wait for compilation.
  5. Inspect package resolution, test discovery, and the Console.
  6. Run the focused fixture/classifier/planner suites.
  7. Run the complete EditMode suite.
- Require the same test methods modulo the namespace prefix, with no loss of discovery.
- Search all tracked current surfaces for old variants. Remaining matches must stay in dated historical or migration documents. Classify each match explicitly.
- Run `git diff --check`. Inspect unstaged and staged diffs separately. Confirm that no generated Unity state or unrelated files changed.

## Rollback and recovery

The migration stays recoverable through Git. It is a rename-focused branch, and every `.meta` file is preserved.

- If validation fails before integration, repair the branch or stop for direction. Do not reset, discard unrelated work, or regenerate assets.
- A rollback should reverse the coherent migration change. It reverses package path, lock entry, namespaces, assemblies, policy/docs, and website/project metadata together.
- Reverse package and asmdef moves with Git-visible moves, so `.meta` files stay paired.
- If the GitHub repository rename already happened, its reversal is a separate external setting action. Afterward, re-verify remotes, Pages/listing URLs, Actions settings, and package metadata.
- If a new VPM package is already published, do not delete releases as a rollback. Publish/migration remediation requires a separate release decision.

## Adversarial review

The review challenged the selected design against the requested failure modes:

- One coherent identity task includes package ID, folder, asmdefs, namespaces, `InternalsVisibleTo`, fixture paths, test FQNs, the lockfile key, `.gitignore`, and the author URL.
- `AGENTS.md` has a section-level preservation/revision map. It cannot become the roadmap, because the migration routes future feature detail to `docs/architecture/vision.md`.
- The migration retains the stable Git/testing/MCP/asset/release policy.
- The design explicitly expects `Packages/manifest.json` and `vpm-manifest.json` to stay unchanged. Lockfile migration is explicit.
- The design documents VPM new-package behavior and the unsupported dual-install risk. It does not call the migration an upgrade.
- External repository variables, Pages/listing URLs, and artifact names are explicit post-merge checks.
- The migration preserves all `.meta` files and asmdef `.meta` partners. GUID baseline and duplicate checks are mandatory.
- The plan requires the NDMF bootstrap and Unity import/test discovery after the folder/assembly move.
- README and vision have explicit implemented-versus-future boundaries.
- The design introduces no speculative production architecture or directories.
- The migration excludes production algorithms and fixture semantics from refactoring.
- The private testbed is never selected or modified.
- The tracked migration does not rewrite Git history, dated design records, published releases, or repository settings.

## Approval gate

Do not start the AMUSE migration until the user explicitly approves this design and `docs/superpowers/plans/2026-08-15-amuse-rebrand.md`.
