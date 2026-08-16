# AMUSE Rebrand and Architectural Pivot Design

**Date:** 2026-08-15

**Status:** Awaiting approval

## Decision summary

Rename the project coherently to **AMUSE — Alrauna's Material Understanding & Simplification Engine** while preserving all implemented classifier, fixture, and separation-planning behavior. Use these permanent identities:

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

This is a clean identity cutover, not a compatibility shim and not a redesign of implemented algorithms. The GitHub repository rename, repository-variable changes, publishing, and releases remain separate external actions requiring separate authorization.

## Verified baseline

- PR #5, `Add immutable separation planning`, is merged into `main` at `7414672`.
- Local `main`, `origin/main`, and this branch's base are all `7414672`.
- Design branch: `chore/amuse-rebrand`.
- The worktree was clean before this design document was created.
- The repository has 135 tracked files. Thirty-five tracked paths contain the old package or assembly identity because they are inside the old package directory or have old asmdef filenames.
- The public Unity project is `E:/AI/Git/alpha-material-optimizer-ndmf`, running Unity `2022.3.22f1`.
- Read-only Unity MCP discovery found 91 tests and showed the public editor idle and not compiling. The private `AlphaMaterialOptimizer-Testbed` instance was not selected or inspected beyond instance identity.
- All 52 tracked `.meta` files contain GUIDs, and no duplicate GUID exists in the current tree.
- GitHub's connected repository metadata confirms a public repository named `Alrauna/alpha-material-optimizer-ndmf` with default branch `main`. The local `gh` CLI is not authenticated, so repository variables and release history were not inspected through `gh`.

## Problem and pivot

The current repository identity defines the whole product as one alpha/material optimization. The implemented exact alpha classifier and immutable mesh-separation planner remain valuable, but they are now foundational AMUSE subsystems rather than the product boundary.

AMUSE aims to understand material, texture, rendering, and state semantics well enough to propose and apply behavior-preserving simplifications. Unity/NDMF is the current host and transformation integration, not the conceptual definition of AMUSE.

The migration must therefore change both human branding and machine identities without pretending that future semantic analysis, shader adapters, atlasing, animation tracing, material combining, or transformation passes already exist.

## Goals

- Establish one coherent AMUSE identity across current policy, package metadata, paths, namespaces, assemblies, tests, documentation, and user-visible listing material.
- Preserve exact alpha-classification and separation-planning semantics byte-for-byte except for identity strings and paths.
- Generalize the project-level safety policy from alpha-specific outcomes to proof-first behavior preservation while retaining the current alpha subsystem's exact contract.
- Make host-neutral immutable analysis/planning versus Unity/NDMF extraction/transformation an explicit architectural property.
- Distinguish current implementation from future direction.
- Preserve Unity asset identity and verify package resolution, compiler/test discovery, NDMF bootstrap, and all relevant tests after the later implementation.
- Keep dated design and implementation records historically accurate.

## Non-goals

This migration does not implement or scaffold:

- semantic material IR, shader adapters, or modifier registries;
- animation or state tracing;
- atlasing, UV packing, dilation, or mip analysis;
- material normalization or combining;
- generalized render-mode classification;
- optimizer orchestration or public plugin APIs;
- mesh/material transformation or a new NDMF pass;
- a standalone shared library;
- compatibility shims without evidence of an installed-user requirement;
- a full visual identity system.

## Considered approaches

### 1. Coherent identity cutover — selected

Rename the package ID/folder, namespaces, assembly identities, current documentation, policy, and current user-visible metadata in one migration. Preserve dated historical documents and preserve all `.meta` GUID contents.

This avoids a permanently split identity and is appropriate because production code is internal, Editor-only, and has no serialized runtime-facing components in the tracked project.

### 2. Branding-only rename with old machine identities

Change README and display strings but retain `com.alrauna.alpha-material-optimizer` and `Alrauna.AlphaMaterialOptimizer`. This reduces immediate churn but makes obsolete identity permanent in every dependency, assembly reference, and API name. It is rejected because the project is early and no compatibility requirement justifies the debt.

### 3. Multi-branch partial migration

Rename policy/docs first, then package identity, then namespaces/assemblies. This produces intermediate branches where documentation, package resolution, and compiler identities contradict one another. It is rejected for integration. The implementation plan still uses reviewable task checkpoints on one branch, but the branch is validated and integrated only as a coherent whole.

## Proof-first safety invariant

AMUSE's general default is:

```text
prove behavior-preserving under the active optimization policy -> transform
uncertain or unsupported -> preserve and explain
```

Additional uncertainty must never make a transformation more aggressive. The current alpha subsystem remains a concrete instance of this rule:

- `TriangleAlphaOutcome.ProvenOpaque` may become an opaque candidate;
- `MustRemainTransparent` remains on the transparent path;
- `Unknown` remains on the transparent path;
- malformed normalized inputs throw rather than being repaired or guessed.

The migration does not rename or generalize these outcomes. Future AMUSE modules may use different domain-specific evidence and result types while obeying the broader invariant.

## Current implementation inventory

### Package and metadata

Current package metadata is rooted at `Packages/com.alrauna.alpha-material-optimizer/package.json`:

- package ID `com.alrauna.alpha-material-optimizer`;
- display name `Alpha Material Optimizer`;
- version `0.0.1`;
- narrow alpha-separation description;
- author URL pointing to `Alrauna/alpha-material-optimizer-ndmf`;
- NDMF dependency `>=1.14.4 <2.0.0-a`.

`Packages/packages-lock.json` records the embedded package by the old ID with `file:com.alrauna.alpha-material-optimizer`. `Packages/.gitignore` explicitly allows the old folder. `Packages/manifest.json` does not list the embedded package, and `Packages/vpm-manifest.json` contains only the NDMF dependency/lock; neither currently contains the project's old ID.

### Production code

The package has one Editor-only production assembly and three production source files under `Editor/Analysis/`:

- `ExactUvGeometry.cs` — exact dyadic/rational geometry support;
- `TriangleAlphaClassifier.cs` — exact conservative triangle/texture-alpha classification;
- `MeshSeparationPlanner.cs` — immutable normalized topology input and deterministic separation candidate planning.

The current assembly is `Alrauna.AlphaMaterialOptimizer.Editor`. `AssemblyInfo.cs` grants internals access to `Alrauna.AlphaMaterialOptimizer.Tests.Editor`.

### Tests and fixtures

The Editor test assembly and root namespace are `Alrauna.AlphaMaterialOptimizer.Tests.Editor`. Current tests cover:

- exact classifier behavior for Point/Bilinear and Clamp/Repeat;
- missing/degenerate/unsupported uncertainty;
- immutable mesh-separation planning and material-binding provenance;
- deterministic reference-fixture catalogs and Unity fixture construction;
- package test-infrastructure smoke behavior.

`ReferenceFixtureData.cs` has two hard-coded package-relative catalog paths that must move with the package.

### Tooling, workflows, and listing assets

- `Tools/Bootstrap-NdmfStandalone.ps1` derives the project/package locations it needs and contains no old product identity. Its behavior should remain unchanged.
- Release/listing workflows do not hard-code the old package ID. They consume the external GitHub Actions variable `PACKAGE_NAME`, use it as `Packages/${PACKAGE_NAME}`, and derive ZIP and `.unitypackage` artifact names from it.
- `Website/index.html` still has the generic title `VCC Listing`.
- `Website/banner.png` visibly says `VCC Example Listing`. It is not an old AMO name, but it is stale template branding and should be replaced with a minimal AMUSE banner during this migration.
- `ProjectSettings/ProjectSettings.asset` still uses template values `companyName: VRChat` and `productName: vpm-package-maker`; the public development project should use `Alrauna` and `AMUSE`.

### Current documentation and policy

- `README.md` is only a narrow alpha-separation description plus NDMF bootstrap instructions.
- `AGENTS.md` defines the permanent project too narrowly and contains an obsolete early-baseline paragraph claiming placeholder code and no repository tests.
- No durable architecture/vision document currently exists.

### Historical documents

The six dated files under `docs/superpowers/specs/` and `docs/superpowers/plans/` describe the reference-fixture, exact-classifier, and separation-planner work as it was designed and implemented. They contain 105 matching lines in the old-identity audit and some hard-coded historical project paths/test names.

Keep these files unchanged. They are implementation provenance, not current policy. The new AMUSE design, plan, README, and vision document provide the present context. Exhaustive stale-name validation must report these dated records as intentional historical occurrences rather than rewriting them.

## Canonical branding and descriptions

Use `AMUSE` ordinarily. At the first meaningful introduction in major user-facing documentation, use:

> **AMUSE — Alrauna's Material Understanding & Simplification Engine**

Use ASCII `'` in metadata. Do not use `AMUSE Engine`, `AMUSE Toolkit`, or other redundant suffixes.

Candidate package descriptions:

1. **Recommended:** `A Unity/NDMF material optimization engine that analyzes and simplifies material, texture, and rendering usage while preserving behavior.`
2. `An intelligent material optimization engine for analyzing, simplifying, and transforming material and texture usage while preserving rendering behavior.`
3. `A proof-oriented Unity material optimization engine for behavior-preserving simplification of material, texture, and rendering usage.`
4. `A modular material understanding and simplification system for safe rendering optimization in Unity.`

Package metadata should use option 1. It communicates the broad AMUSE identity while accurately naming today's Unity/NDMF host. The README and vision document may describe the broader host-neutral architecture, with explicit current/future labels.

## Package ID and VPM consequences

Select `com.alrauna.amuse`.

Unity defines a package `name` as its unique identifier and explicitly states that a package cannot be renamed through an update; a renamed package is a new package. VPM repository listings are likewise keyed by package name, and the VPM CLI adds/removes packages by that unique name.

Consequences:

- Existing installations of `com.alrauna.alpha-material-optimizer` will not see `com.alrauna.amuse` as an in-place upgrade.
- Adding AMUSE does not automatically remove the old package.
- With no explicit conflict metadata, both IDs may be present. Even if distinct renamed assembly identities avoid an immediate duplicate-name compile error, duplicate optimizer code/tests or future integration would be unsupported.
- The supported migration is explicit: remove the old package, then add `com.alrauna.amuse`, after backing up the project and confirming no external code depends on old assemblies/namespaces.
- Do not add `legacyPackages`, forwarding assemblies, namespace aliases, or dual package publication without evidence that an installed user population needs them.
- A generated repository listing may retain old releases under the old package key while adding AMUSE under the new key. Validate the generated listing deliberately and do not delete published releases as part of this migration.
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

Capture the complete path-to-GUID map before moving, compare it after moving by package-relative suffix, and rerun duplicate-GUID detection. Never regenerate `.meta` files to make the move import.

## Namespace and assembly migration

Rename consistently:

```text
Alrauna.AlphaMaterialOptimizer.Editor.Analysis
    -> Alrauna.Amuse.Editor.Analysis

Alrauna.AlphaMaterialOptimizer.Tests.Editor
    -> Alrauna.Amuse.Tests.Editor
```

The two assembly identities become `Alrauna.Amuse.Editor` and `Alrauna.Amuse.Tests.Editor`. Update the test asmdef reference and `InternalsVisibleTo` string in the same coherent edit.

Preserve the existing two-assembly boundary. Do not add Core, Host, Shader, Animation, Atlas, Combining, or orchestration assemblies. Logical boundaries and documentation are sufficient until implemented code needs a physical split.

Compatibility risk is limited but real:

- tracked production types are internal and Editor-only;
- the tracked `Assets/` tree contains no scenes, prefabs, or serialized AMO components;
- assembly GUIDs are preserved;
- external source code that directly references old assembly or namespace names will break and must migrate;
- no claim is made about uninspected external projects.

## Behavior-preservation contract

Only identifiers, paths, metadata, policy, and documentation change. Do not refactor production logic or rewrite tests during the rebrand.

Preserve:

- exact float-to-dyadic and rational geometry;
- Point/Bilinear support semantics;
- Clamp/Repeat and mathematical floor/modulo behavior;
- `ProvenOpaque`, `MustRemainTransparent`, and `Unknown` semantics;
- support-region budget behavior;
- deterministic fixture inputs and expectations;
- immutable input/output copying;
- source triangle ordinals, winding, ordering, and material-binding provenance;
- monotonic safety and malformed-input behavior.

Fixture JSON content and production algorithm bodies should have no semantic diff. Test method names should remain unchanged; only namespaces, usings, assembly names, and package-relative paths change.

## `AGENTS.md` migration audit

`AGENTS.md` remains agent policy, not the product roadmap.

| Current section | Category | Proposed treatment |
|---|---|---|
| Title | Rename mechanically | Rename to `AMUSE Agent Policy`. |
| Project and repository boundaries | Architecturally revise | Introduce AMUSE and `com.alrauna.amuse`; describe Unity/NDMF as current host; name the new package path; keep public fixture/legal boundaries; replace the obsolete placeholder/no-tests baseline with concise current-state/reinspect wording. |
| Private Unity avatar testbed | Preserve unchanged | Keep read-only-by-default external-test-appliance rules and all publication/mutation prohibitions. Do not turn its external historical name into product branding. |
| Start-of-task discipline | Preserve unchanged | Keep all five pre-edit checks and scope rules. |
| Branch and Git discipline | Preserve unchanged | Keep protected `main`, clean branch, unrelated-work, validation, and no-commit/push rules. |
| Development approach | Architecturally revise | Keep narrow vertical increments; remove the old alpha-only phase roadmap; point detailed future architecture to `docs/architecture/vision.md`; state that implemented alpha analysis/planning is one subsystem. |
| Superpowers and Ponytail | Preserve unchanged | Retain workflow priority, installed-doc inspection, correctness hierarchy, and minimalism constraints. |
| Safety and behavior preservation | Architecturally revise | Generalize to proof of behavior preservation under the active policy; preserve fail-closed uncertainty, actionable diagnostics, nondestructive build output, and current alpha-specific conservative behavior as an example. |
| Source and dependency boundaries | Architecturally revise narrowly | Add host-neutral immutable analysis/planning versus Unity/NDMF extraction/transformation; keep Editor-only preference, dependency ladder, license/attribution, and no premature standalone library. |
| Unity asset integrity | Preserve unchanged | Keep asset/`.meta` pairing, GUID safety, generated-state exclusions, manifest/lock scrutiny, and private local-reference rules. |
| Testing policy | Architecturally revise narrowly | Preserve all test requirements; broaden deterministic examples beyond alpha; retain analysis -> planning -> transformation attribution and public/private fixture boundaries. |
| Unity MCP use | Rename and revise narrowly | Change the product package path; distinguish AMUSE core concepts from Unity/NDMF integration; preserve instance discovery, public/private targeting, read-only preference, and CoplayDev exclusion from product dependencies. |
| CI and release safety | Preserve unchanged | Keep public reproducibility, cheapest deterministic gates, private dependency prohibitions, and separately authorized publishing/settings rules. |
| Completion standard | Preserve unchanged | Keep line-by-line requirements review, staged/unstaged scope checks, observed validation, and explicit reporting. |

No whole safety section should be removed. Obsolete text recommended for removal is limited to:

- the stale early-baseline claim that production is placeholder code with no tests;
- the alpha-only permanent development roadmap.

New AMUSE-level guidance is limited to repository identity, proof-first transformation, current-vs-future scope, the host-neutral/host-specific seam, and the vision-document pointer. Do not enumerate every shader family or future optimizer in `AGENTS.md`.

After editing, search `AGENTS.md` explicitly for every old identity variant and verify zero stale product references.

## README and current documentation

Rewrite `README.md` around two explicit layers.

### Vision

Introduce AMUSE and describe the goal of understanding material, texture, rendering, and state semantics to enable safe simplification. Mention shader semantic adapters, modifier semantics, state/animation analysis, texture/atlas planning, material normalization/combining, alpha/overdraw analysis, and combined planning only as future direction.

### Current implementation

State that AMUSE is in early development and currently provides:

- deterministic reference fixtures;
- exact proof-oriented triangle alpha classification for implemented sampling/wrap semantics;
- immutable behavior-preserving separation candidate planning with explicit provenance;
- Editor-only Unity tests and NDMF development/bootstrap infrastructure.

State plainly that no automatic avatar transformation, atlasing, shader adapter, animation tracing, or material-combining pipeline is implemented yet. Preserve the working NDMF bootstrap instructions.

## Architecture/vision document

Create `docs/architecture/vision.md` as a durable north-star document, not an implementation plan. It should cover:

- AMUSE purpose and current status;
- proof-first, lossless-by-default transformation policy;
- host extraction -> normalized immutable inputs -> semantic understanding -> analysis modules -> candidates -> combined planning -> safety/compatibility/cost evaluation -> host transformation;
- shader-specific semantic adapters;
- recognized modifier semantics and fail-closed unknown modifiers;
- future material state/animation relationships;
- future texture use, atlas, normalization, and combining direction;
- the exact alpha classifier and separation planner as the implemented alpha subsystem;
- Unity/NDMF as the current host;
- portability as a design property, not a reason to extract a standalone library now;
- future policy levels only as explicit, auditable opt-ins; the default remains behavior-preserving;
- a clear current-versus-future table.

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

Unity/NDMF currently owns extraction/build integration and future Unity-specific transformation. Pure analysis and planning should not depend unnecessarily on live Unity objects, Editor state, assets, NDMF state, or MCP. The current classifier/planner already demonstrate that seam.

Do not physically extract a shared library until a second real host or consumer requires it.

## Package version recommendation

Keep `0.0.1`.

The version is already early/experimental, and the new package ID creates a distinct package lineage. Changing both identity and version without a release-policy reason would add noise, not compatibility. Reconsider only if a separately approved publication plan establishes a concrete versioning rule or finds a conflicting published `com.alrauna.amuse@0.0.1`.

## GitHub repository rename

Recommend `amuse`, not `amuse-unity`.

The repository contains the current Unity host but is intended to remain the umbrella AMUSE source rather than permanently encode Unity, VRChat, or NDMF in the project identity. Use `amuse-unity` only if the organization later splits host implementations into separate repositories.

The external rename is not part of the tracked migration. After a separately authorized rename:

```powershell
git remote set-url origin https://github.com/Alrauna/amuse.git
git fetch --prune origin
git remote -v
git status --short --branch
```

Update/verify:

- `package.json` author URL;
- repository description and homepage;
- Pages/VPM listing URL and existing subscriber impact;
- `PACKAGE_NAME=com.alrauna.amuse`;
- branch protection and Actions variables/secrets/environments;
- any external listing source that names `Alrauna/alpha-material-optimizer-ndmf`;
- release artifact names and generated repository listing keys;
- local clones and the Unity MCP instance name/path if the local directory is later renamed.

GitHub commonly redirects old repository URLs after a rename, but current files should use the canonical new URL and Pages/listing behavior must be verified rather than assumed.

## Adjacent branding decisions

- Update Unity development-project `companyName` to `Alrauna` and `productName` to `AMUSE`.
- Update the listing page title to `AMUSE Package Listing`.
- Replace the generic `VCC Example Listing` banner with a simple AMUSE wordmark plus the full product name. Do not invent a broader logo system.
- Leave the favicon unchanged during this migration because no approved AMUSE icon exists and it contains no searchable old product identity.
- Keep workflow names such as `Build Release` and `Build Repo Listing`; they describe jobs, not the old product.

## Validation design

### Before the move

- Record branch/base/status.
- Record every tracked old-identity occurrence and old-identity path.
- Record a path-to-GUID map for all 52 `.meta` files.
- Record current package metadata, assembly references, and test discovery names/count.

### After the move

- Parse every changed JSON file.
- Verify package folder, ID, lock entry, asmdef names/references, friend assembly, namespaces, fixture paths, and test fully-qualified names.
- Verify `Packages/manifest.json` and `Packages/vpm-manifest.json` remain unchanged unless Unity proves a necessary resolver change.
- Compare `.meta` GUIDs by package-relative logical asset, require 52 GUID entries, and require zero duplicates.
- Inspect Git rename detection; no package asset should appear as delete-plus-unrelated-create due to lost identity.
- Run the NDMF bootstrap twice and verify idempotence.
- Through Unity MCP, rediscover instances, select only the actual public project path, refresh/import, wait for compilation, inspect package resolution/test discovery/Console, run focused fixture/classifier/planner suites, then the complete EditMode suite.
- Require the same test methods modulo the namespace prefix and no loss of discovery.
- Search all tracked current surfaces for old variants. Remaining matches must be confined to dated historical or migration documents and explicitly classified.
- Run `git diff --check`, inspect unstaged and staged diffs separately, and confirm no generated Unity state or unrelated files changed.

## Rollback and recovery

The migration remains recoverable through Git because it is a rename-focused branch and every `.meta` file is preserved.

- If validation fails before integration, repair the branch or stop for direction; do not reset, discard unrelated work, or regenerate assets.
- A rollback should reverse the coherent migration change, including package path, lock entry, namespaces, assemblies, policy/docs, and website/project metadata together.
- Reverse package and asmdef moves with Git-visible moves so `.meta` files stay paired.
- If the GitHub repository has already been renamed, reversing it is a separate external setting action; re-verify remotes, Pages/listing URLs, Actions settings, and package metadata afterward.
- If a new VPM package has been published, do not delete releases as rollback. Publish/migration remediation requires a separate release decision.

## Adversarial review

The selected design was challenged against the requested failure modes:

- Package ID, folder, asmdefs, namespaces, `InternalsVisibleTo`, fixture paths, test FQNs, lockfile key, `.gitignore`, and author URL are included in one coherent identity task.
- `AGENTS.md` has a section-level preservation/revision map and cannot become the roadmap because future feature detail is routed to `docs/architecture/vision.md`.
- Stable Git/testing/MCP/asset/release policy is retained.
- `Packages/manifest.json` and `vpm-manifest.json` are explicitly expected to remain unchanged; lockfile migration is explicit.
- VPM new-package behavior and unsupported dual-install risk are documented rather than called an upgrade.
- External repository variables, Pages/listing URLs, and artifact names are explicit post-merge checks.
- All `.meta` files and asmdef `.meta` partners are preserved; GUID baseline and duplicate checks are mandatory.
- Bootstrap and Unity import/test discovery are required after the folder/assembly move.
- README and vision have explicit implemented-versus-future boundaries.
- No speculative production architecture or directories are introduced.
- Production algorithms and fixture semantics are excluded from refactoring.
- The private testbed is never selected or modified.
- Git history, dated design records, published releases, and repository settings are not rewritten by the tracked migration.

## Approval gate

Do not begin the AMUSE migration until the user explicitly approves this design and `docs/superpowers/plans/2026-08-15-amuse-rebrand.md`.
