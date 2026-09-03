# AMUSE Rebrand and Architectural Pivot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` for inline implementation. Use `superpowers:subagent-driven-development` only if the user separately authorizes subagents. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate the current Unity package-development repository from its narrow alpha-material identity to AMUSE. Preserve implemented behavior, Unity asset identity, conservative safety, and historical records.

**Architecture:** Cut over the identity in one coherent pass on the approved branch. Update agent policy, move the embedded package with all `.meta` identities intact, and rename namespaces/assemblies/tests together. Update current documentation and development-project branding. Then prove package resolution and behavioral equivalence. Keep the implemented exact classifier and immutable separation planner unchanged. Document future semantic architecture without scaffolding it.

**Tech Stack:** Unity 2022.3.22f1, C#, NUnit EditMode tests, embedded Unity/VPM package metadata, NDMF 1.14.4, PowerShell, Git, GitHub Actions listing/release templates. Unity MCP covers public-project validation.

## Global constraints

- Do not start until the user explicitly approves both `docs/superpowers/specs/2026-08-15-amuse-rebrand-design.md` and this plan.
- Work only on `chore/amuse-rebrand` based at merged `main` commit `7414672`, unless a fresh check proves a newer approved base is required.
- Do not commit, push, open a PR, or delete releases without separate authorization. Do not rename the GitHub repository, change repository settings/variables, tag, or publish without separate authorization.
- The only canonical product names are `AMUSE` and `Alrauna's Material Understanding & Simplification Engine`.
- The canonical machine identities are `com.alrauna.amuse`, `Alrauna.Amuse`, `Alrauna.Amuse.Editor`, and `Alrauna.Amuse.Tests.Editor`.
- Keep package version `0.0.1`.
- Preserve every implemented alpha classifier, exact geometry, fixture, and separation-planner semantic.
- Preserve all existing `.meta` contents and GUIDs for logically unchanged assets. Rename each asset and `.meta` partner together.
- Do not add dependencies, runtime components, assemblies, interfaces, registries, schemas, adapters, optimizer orchestration, or future-feature scaffolding.
- Keep the dated pre-AMUSE Superpowers specs/plans unchanged as historical provenance.
- Do not select, inspect beyond identity discovery, or modify the private Unity avatar testbed.
- Use Unity MCP only after discovering instances and selecting the editor whose actual project root is the public repository.
- Do not weaken or omit Git, test, CI, release, MCP, public/private fixture, or Unity asset safety policy while revising `AGENTS.md`.
- If unexpected user changes appear, stop before moving, editing, staging, or restoring overlapping files.

---

## Planned file map

### Create

- `docs/architecture/vision.md` — durable AMUSE purpose, safety, architectural direction, portability, and current-versus-future boundary.

### Modify

- `AGENTS.md` — concise AMUSE-era agent policy with proof-first and host-neutral/host-specific boundaries.
- `README.md` — AMUSE introduction, vision, current implementation, non-implemented scope, and development setup.
- `Packages/.gitignore` — allow `com.alrauna.amuse` instead of the old package folder.
- `Packages/packages-lock.json` — replace the old embedded package key/path with `com.alrauna.amuse` and `file:com.alrauna.amuse`.
- `ProjectSettings/ProjectSettings.asset` — set development project company/product to `Alrauna`/`AMUSE`.
- `Website/index.html` — set the browser title to `AMUSE Package Listing`.
- `Website/banner.png` — replace generic `VCC Example Listing` art with a minimal AMUSE banner.

### Move as one package tree

- `Packages/com.alrauna.alpha-material-optimizer/` -> `Packages/com.alrauna.amuse/`.

### Rename inside the moved package

- `Editor/Alrauna.AlphaMaterialOptimizer.Editor.asmdef` -> `Editor/Alrauna.Amuse.Editor.asmdef`.
- `Editor/Alrauna.AlphaMaterialOptimizer.Editor.asmdef.meta` -> `Editor/Alrauna.Amuse.Editor.asmdef.meta`.
- `Tests/Editor/Alrauna.AlphaMaterialOptimizer.Tests.Editor.asmdef` -> `Tests/Editor/Alrauna.Amuse.Tests.Editor.asmdef`.
- `Tests/Editor/Alrauna.AlphaMaterialOptimizer.Tests.Editor.asmdef.meta` -> `Tests/Editor/Alrauna.Amuse.Tests.Editor.asmdef.meta`.

### Modify inside the moved package

- `package.json` — package ID, display name, description, and future canonical repository URL.
- `Editor/Alrauna.Amuse.Editor.asmdef` — production assembly name.
- `Editor/AssemblyInfo.cs` — friend-assembly name.
- `Editor/Analysis/ExactUvGeometry.cs` — namespace only.
- `Editor/Analysis/TriangleAlphaClassifier.cs` — namespace only.
- `Editor/Analysis/MeshSeparationPlanner.cs` — namespace only.
- `Tests/Editor/Alrauna.Amuse.Tests.Editor.asmdef` — test assembly/root namespace/reference.
- `Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs` — usings/namespace only.
- `Tests/Editor/Analysis/MeshSeparationPlannerTests.cs` — using/namespace only.
- `Tests/Editor/ReferenceFixtures/ReferenceFixtureData.cs` — namespace and two package-relative catalog paths only.
- `Tests/Editor/ReferenceFixtures/ReferenceFixtureIntegrityTests.cs` — namespace only.
- `Tests/Editor/TestInfrastructureSmokeTests.cs` — namespace only.

### Expected unchanged tracked files

- `Packages/manifest.json` — no self-package entry exists for the embedded package.
- `Packages/vpm-manifest.json` — contains only NDMF resolution state.
- `Tools/Bootstrap-NdmfStandalone.ps1` — already derives paths without the old project identity.
- `.github/workflows/release.yml` and `.github/workflows/build-listing.yml` — already consume the external `PACKAGE_NAME` variable.
- all dated files under `docs/superpowers/specs/` and `docs/superpowers/plans/`, except the two new documents of this migration.
- classifier/planner fixture JSON content and all existing `.meta` file contents.

---

### Task 1: Reconfirm base and capture a recovery baseline

**Files:** Read-only inspection. Create no tracked file.

**Produces:** A current branch/base/status snapshot, old-identity inventory, GUID map, metadata snapshot, and Unity test-discovery baseline used by later tasks.

- [ ] **Step 1: Recheck branch, base, worktree, and remote containment**

Run:

```powershell
git fetch --prune origin
git status --porcelain=v2 --branch
git branch -vv
git log -5 --oneline --decorate
git merge-base --is-ancestor 3b9d469 origin/main
git merge-base --is-ancestor origin/main HEAD
```

Expected:

- branch is `chore/amuse-rebrand`.
- worktree contains only the two approved design-phase documents before implementation starts.
- separation-plan commit `3b9d469` is an ancestor of `origin/main`.
- branch base is current with the approved `origin/main`, or the plan asks the user before rebasing onto unexpected new work.

- [ ] **Step 2: Capture tracked old identity surfaces without editing them**

Run:

```powershell
git grep -n -i -E 'Alpha Material Optimizer|AlphaMaterialOptimizer|alpha-material-optimizer|alpha_material_optimizer|alpha material optimizer|(^|[^[:alnum:]_])AMO([^[:alnum:]_]|$)'
git ls-files | Select-String -Pattern 'alpha-material-optimizer|AlphaMaterialOptimizer|alpha_material_optimizer' -CaseSensitive:$false
```

Expected: current matches agree with the design inventory, plus intentional matches in the two migration documents.

- [ ] **Step 3: Capture the complete `.meta` GUID map and duplicate check**

Run this read-only PowerShell block and retain its console output for comparison:

```powershell
$metaFiles = git ls-files '*.meta'
$guidEntries = foreach ($file in $metaFiles) {
    $guidLine = Select-String -LiteralPath $file -Pattern '^guid: ' | Select-Object -First 1
    if ($guidLine) {
        [pscustomobject]@{
            File = $file
            Guid = $guidLine.Line.Substring(6)
        }
    }
}
$guidEntries | Sort-Object File | Format-Table -AutoSize
$guidEntries | Group-Object Guid | Where-Object Count -gt 1
```

Expected: 52 files, 52 GUID entries, zero duplicate groups. Save no generated baseline file in the repository.

- [ ] **Step 4: Capture package/assembly/test identity**

Run:

```powershell
Get-Content -Raw Packages/com.alrauna.alpha-material-optimizer/package.json | ConvertFrom-Json | ConvertTo-Json -Depth 10
Get-Content -Raw Packages/com.alrauna.alpha-material-optimizer/Editor/Alrauna.AlphaMaterialOptimizer.Editor.asmdef | ConvertFrom-Json | ConvertTo-Json -Depth 10
Get-Content -Raw Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Alrauna.AlphaMaterialOptimizer.Tests.Editor.asmdef | ConvertFrom-Json | ConvertTo-Json -Depth 10
rg -n '^namespace |^using Alrauna|InternalsVisibleTo' Packages/com.alrauna.alpha-material-optimizer
```

Expected: old package/assembly identities exactly match the design specification.

- [ ] **Step 5: Discover and pin only the public Unity editor, then read test discovery**

Read `mcpforunity://instances`. Select only the instance whose project root resolves to the current public checkout. Read `mcpforunity://project/info`, `mcpforunity://editor/state`, and `mcpforunity://tests` without running tests or mutating the project.

Expected baseline: Unity `2022.3.22f1`, public project root, editor ready/not compiling, and the current old namespace test names discoverable. Do not select the private testbed.

---

### Task 2: Update `AGENTS.md` as AMUSE working policy

**Files:**

- Modify: `AGENTS.md`

**Produces:** The approved AMUSE-era policy that governs the remaining migration work.

- [ ] **Step 1: Change identity and current package location**

Use the exact first introduction:

```markdown
# AMUSE Agent Policy

AMUSE — Alrauna's Material Understanding & Simplification Engine (`com.alrauna.amuse`) is an MIT-licensed material optimization system. Unity/NDMF is its current host integration.
```

Describe the distributable package at `Packages/com.alrauna.amuse/`. Keep the public repository/development project role, legally safe fixture rules, and private-testbed separation.

- [ ] **Step 2: Replace stale project-stage text with current, compact boundaries**

Remove the obsolete claim that the repository has placeholder code and no tests. State only that current production consists of Editor-only exact alpha analysis and immutable separation planning. Require agents to reinspect rather than treat that snapshot as permanent.

Replace the old ten-step alpha-only development roadmap with:

- narrow vertical increments.
- current alpha analysis/planning as one AMUSE subsystem.
- detailed future direction living in `docs/architecture/vision.md`.
- no speculative future scaffolding.

- [ ] **Step 3: Generalize safety without weakening alpha behavior**

Express:

```text
prove behavior-preserving under the active optimization policy -> transform
uncertain or unsupported -> preserve and explain
```

Keep the rule that additional uncertainty cannot increase aggressiveness. Retain current alpha semantics as the concrete example: only `ProvenOpaque` can become an opaque candidate, while `MustRemainTransparent` and `Unknown` remain transparent.

- [ ] **Step 4: Add the host-neutral/Unity-host seam narrowly**

State that reusable analysis/planning should consume normalized immutable inputs and avoid unnecessary live Unity, Editor, NDMF, asset, and MCP dependencies. State that Unity/NDMF owns current extraction/build integration and host-specific transformation. Do not prescribe a standalone library or new assemblies.

- [ ] **Step 5: Preserve stable policy section by section**

Compare the edited file against the section audit in the design. Confirm that these remain materially unchanged:

- private-testbed safety.
- start-of-task and Git discipline.
- Superpowers/Ponytail workflow.
- Unity asset/`.meta`/GUID rules.
- testing and regression expectations.
- MCP instance targeting and mutation rules.
- CI/release authorization boundaries.
- completion reporting.

- [ ] **Step 6: Validate `AGENTS.md` explicitly**

Run:

```powershell
rg -n -i 'Alpha Material Optimizer|AlphaMaterialOptimizer|alpha-material-optimizer|alpha_material_optimizer|alpha material optimizer|(^|[^A-Za-z0-9_])AMO([^A-Za-z0-9_]|$)' AGENTS.md
rg -n 'AMUSE|com\.alrauna\.amuse|Packages/com\.alrauna\.amuse|proof|behavior-preserving|normalized immutable|Unity/NDMF|Superpowers|Ponytail|\.meta|GUID|private|MCP|CI|release' AGENTS.md
git diff --check -- AGENTS.md
git diff -- AGENTS.md
```

Expected: zero stale old product identifiers. All required AMUSE/safety boundaries are present. No roadmap bloat or unrelated policy rewrite.

---

### Task 3: Move the Unity package and cut over all machine identities atomically

**Files:** All package moves/modifications listed in the file map, plus `Packages/.gitignore` and `Packages/packages-lock.json`.

**Consumes:** The Task 1 GUID/identity baseline and Task 2 approved policy.

**Produces:** One resolvable embedded package at `com.alrauna.amuse` with `Alrauna.Amuse` production/test identities and unchanged algorithms.

- [ ] **Step 1: Move the package tree with Git**

Run:

```powershell
git mv Packages/com.alrauna.alpha-material-optimizer Packages/com.alrauna.amuse
git mv Packages/com.alrauna.amuse/Editor/Alrauna.AlphaMaterialOptimizer.Editor.asmdef Packages/com.alrauna.amuse/Editor/Alrauna.Amuse.Editor.asmdef
git mv Packages/com.alrauna.amuse/Editor/Alrauna.AlphaMaterialOptimizer.Editor.asmdef.meta Packages/com.alrauna.amuse/Editor/Alrauna.Amuse.Editor.asmdef.meta
git mv Packages/com.alrauna.amuse/Tests/Editor/Alrauna.AlphaMaterialOptimizer.Tests.Editor.asmdef Packages/com.alrauna.amuse/Tests/Editor/Alrauna.Amuse.Tests.Editor.asmdef
git mv Packages/com.alrauna.amuse/Tests/Editor/Alrauna.AlphaMaterialOptimizer.Tests.Editor.asmdef.meta Packages/com.alrauna.amuse/Tests/Editor/Alrauna.Amuse.Tests.Editor.asmdef.meta
```

Expected: every package asset and `.meta` partner is still present under the new root. The commands create no new package-root `.meta`.

- [ ] **Step 2: Update package and root resolution metadata**

Set `Packages/com.alrauna.amuse/package.json` to these identity fields while retaining version, Unity version, VPM dependency, VRChat version, author name, and email:

```json
{
  "name": "com.alrauna.amuse",
  "displayName": "AMUSE",
  "version": "0.0.1",
  "unity": "2022.3",
  "description": "A Unity/NDMF material optimization engine that analyzes and simplifies material, texture, and rendering usage while preserving behavior.",
  "vpmDependencies": {
    "nadena.dev.ndmf": ">=1.14.4 <2.0.0-a"
  },
  "vrchatVersion": "2026.3.1",
  "author": {
    "name": "Alrauna",
    "email": "67886220+Alrauna@users.noreply.github.com",
    "url": "https://github.com/Alrauna/amuse"
  }
}
```

Update:

```text
Packages/.gitignore:
!com.alrauna.amuse

Packages/packages-lock.json key:
com.alrauna.amuse

Packages/packages-lock.json version:
file:com.alrauna.amuse
```

Do not modify `Packages/manifest.json` or `Packages/vpm-manifest.json` manually.

- [ ] **Step 3: Rename production/test assemblies and friend access**

Production asmdef:

```json
"name": "Alrauna.Amuse.Editor"
```

Test asmdef:

```json
"name": "Alrauna.Amuse.Tests.Editor",
"rootNamespace": "Alrauna.Amuse.Tests.Editor",
"references": [
  "Alrauna.Amuse.Editor"
]
```

Friend assembly:

```csharp
[assembly: InternalsVisibleTo("Alrauna.Amuse.Tests.Editor")]
```

Preserve all other asmdef properties and ordering.

- [ ] **Step 4: Rename namespaces/usings and fixture paths only**

Apply these exact textual mappings in current package C# files:

```text
Alrauna.AlphaMaterialOptimizer.Editor.Analysis
    -> Alrauna.Amuse.Editor.Analysis

Alrauna.AlphaMaterialOptimizer.Tests.Editor
    -> Alrauna.Amuse.Tests.Editor

Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/Data/
    -> Packages/com.alrauna.amuse/Tests/Editor/ReferenceFixtures/Data/
```

Do not rename `TriangleAlphaOutcome`, classifier/planner types, methods, test method names, fixture IDs, or JSON content.

- [ ] **Step 5: Parse metadata and verify references before Unity import**

Run:

```powershell
Get-Content -Raw Packages/com.alrauna.amuse/package.json | ConvertFrom-Json | Out-Null
Get-Content -Raw Packages/com.alrauna.amuse/Editor/Alrauna.Amuse.Editor.asmdef | ConvertFrom-Json | Out-Null
Get-Content -Raw Packages/com.alrauna.amuse/Tests/Editor/Alrauna.Amuse.Tests.Editor.asmdef | ConvertFrom-Json | Out-Null
Get-Content -Raw Packages/packages-lock.json | ConvertFrom-Json | Out-Null
rg -n 'com\.alrauna\.amuse|Alrauna\.Amuse|InternalsVisibleTo|fixture-(inputs|expectations)\.json' Packages/com.alrauna.amuse Packages/.gitignore Packages/packages-lock.json
```

Expected: all JSON parses. The new identities are internally consistent.

- [ ] **Step 6: Prove no current package identity is left under old paths**

Run:

```powershell
Test-Path Packages/com.alrauna.alpha-material-optimizer
git ls-files Packages/com.alrauna.amuse
git grep -n -i -E 'Alpha Material Optimizer|AlphaMaterialOptimizer|alpha-material-optimizer|alpha_material_optimizer|alpha material optimizer|(^|[^[:alnum:]_])AMO([^[:alnum:]_]|$)' -- Packages
```

Expected: old folder is absent. The new package tree is complete. Zero old identity matches exist anywhere under `Packages`.

- [ ] **Step 7: Compare logical asset GUIDs and inspect rename detection**

Repeat the Task 1 GUID extraction. Normalize the old/new package-root prefix when comparing logical suffixes.

Expected:

- 52 tracked `.meta` files still have 52 GUID entries.
- zero duplicate GUID groups.
- `package.json.meta` remains `c474695b7921e8141b9c57e2795b9a33`.
- production asmdef GUID remains `2cca9ae73dfb9a84fa800e585fe3a948`.
- test asmdef GUID remains `c28e6dde4cd041b4c87cc087e3b1094c`.
- all other path-to-GUID mappings match by logical suffix.

Run:

```powershell
git diff --summary --find-renames=50%
git diff --numstat --find-renames=50% -- Packages
```

Expected: Git recognizes package/asmdef moves. `.meta` files are not regenerated.

- [ ] **Step 8: Confirm production behavior files have identity-only diffs**

Inspect:

```powershell
git diff --word-diff=porcelain -- Packages/com.alrauna.amuse/Editor/Analysis Packages/com.alrauna.amuse/Editor/AssemblyInfo.cs
git diff --word-diff=porcelain -- Packages/com.alrauna.amuse/Tests/Editor
```

Expected: C# changes affect only namespace/using/friend strings and the two fixture paths. Algorithm bodies, assertions, fixture JSON, and test method names stay unchanged.

---

### Task 4: Update development-project and listing branding

**Files:**

- Modify: `ProjectSettings/ProjectSettings.asset`
- Modify: `Website/index.html`
- Modify: `Website/banner.png`

**Produces:** Current public development/listing surfaces no longer show template identities.

- [ ] **Step 1: Update Unity development-project identity**

Make only these YAML changes:

```yaml
companyName: Alrauna
productName: AMUSE
```

Do not touch other project settings.

- [ ] **Step 2: Update listing page title**

Change only:

```html
<title>AMUSE Package Listing</title>
```

Keep the VCC instructions and listing template behavior intact.

- [ ] **Step 3: Replace the generic banner narrowly**

Use the installed `imagegen` skill because this is a raster-asset edit. Replace `Website/banner.png` with a restrained 1280x256 banner containing:

- `AMUSE` as the primary text.
- `Alrauna's Material Understanding & Simplification Engine` as the secondary text.
- high-contrast, readable typography.
- no unapproved icon, mascot, shader imagery, feature claims, or additional product suffix.

Keep the existing filename and do not add alternate banner assets.

- [ ] **Step 4: Validate the narrow branding diff**

Run:

```powershell
rg -n 'companyName: Alrauna|productName: AMUSE' ProjectSettings/ProjectSettings.asset
rg -n '<title>AMUSE Package Listing</title>' Website/index.html
git diff -- ProjectSettings/ProjectSettings.asset Website/index.html
git diff --stat -- Website/banner.png
```

Visually inspect the new banner at its native dimensions. Expected: only the two YAML scalars, one HTML title, and the banner binary changed.

---

### Task 5: Rewrite current documentation and add the AMUSE vision

**Files:**

- Modify: `README.md`
- Create: `docs/architecture/vision.md`

**Produces:** Honest current documentation and a durable, non-scaffolding architecture north star.

- [ ] **Step 1: Rewrite README introduction and vision/current split**

Start with:

```markdown
# AMUSE

**AMUSE — Alrauna's Material Understanding & Simplification Engine** is a Unity/NDMF material optimization project focused on behavior-preserving analysis, planning, and transformation.
```

Include sections named `Vision`, `Current implementation`, `Not implemented yet`, and `Development setup`.

The current list must name only:

- deterministic reference fixtures.
- exact proof-oriented triangle alpha classification for current Point/Bilinear and Clamp/Repeat semantics.
- immutable separation candidate planning with source/material provenance.
- Editor-only Unity tests and NDMF development/bootstrap infrastructure.

The not-implemented list must plainly include automatic avatar transformation, shader adapters, state/animation tracing, atlasing, material normalization/combining, and generalized orchestration.

Preserve the exact bootstrap command:

```powershell
pwsh -NoProfile -File ./Tools/Bootstrap-NdmfStandalone.ps1
```

- [ ] **Step 2: Create the vision document with explicit present/future boundaries**

Create these sections in `docs/architecture/vision.md`:

```markdown
# AMUSE Architecture Vision
## Purpose
## Safety and optimization policy
## Architectural direction
## Semantic understanding
## Analysis and combined planning
## Unity/NDMF host integration
## Alpha subsystem
## Portability
## Future policy levels
## Current implementation versus future direction
## Non-goals for current development
```

Include the pipeline:

```text
host/application extraction
        -> normalized immutable renderer/material/state inputs
        -> shader and recognized-modifier semantic understanding
        -> analysis modules
        -> optimization candidates
        -> combined optimization planning
        -> safety, compatibility, and cost evaluation
        -> host-specific transformation
```

Document shader adapters, modifier semantics, state/animation relationships, texture/atlas planning, normalization/combining, and future material optimizations only as direction. State that unknown modifiers fail closed for affected optimization.

State that the default policy remains behavior-preserving. Any future non-default policy must be explicit, auditable, separately designed, and must not silently weaken the default.

- [ ] **Step 3: Assert honest claims and no speculative scaffolding**

Run:

```powershell
rg -n 'Vision|Current implementation|Not implemented yet|Development setup|Point|Bilinear|Clamp|Repeat|separation|NDMF' README.md
rg -n 'Safety and optimization policy|normalized immutable|modifier|state|atlas|combining|Alpha subsystem|Portability|Current implementation versus future direction' docs/architecture/vision.md
rg -n -i 'implemented.*(atlas|shader adapter|animation tracing|material combin)|supports.*(Poiyomi|lilToon|Xiexe|Sunao)' README.md docs/architecture/vision.md
git diff --check -- README.md docs/architecture/vision.md
```

Expected: required sections exist. The documents claim no future capability as implemented. The tasks add no source directories or code scaffolding.

- [ ] **Step 4: Preserve historical design records**

Run:

```powershell
git diff -- docs/superpowers/specs/2026-08-15-geometry-classifier-design.md docs/superpowers/specs/2026-08-15-reference-fixtures-design.md docs/superpowers/specs/2026-08-15-separation-plan-design.md docs/superpowers/plans/2026-08-15-geometry-classifier.md docs/superpowers/plans/2026-08-15-reference-fixtures.md docs/superpowers/plans/2026-08-15-separation-plan.md
```

Expected: no output.

---

### Task 6: Resolve/import the renamed package and run focused validation

**Files:** No intended tracked edits. Unity may update ignored local state. Reject unexpected tracked manifest/lock churn.

**Consumes:** Tasks 2-5 complete and internally reviewed.

**Produces:** Evidence that the package imports, assemblies compile, NDMF bootstrap resolves, and focused suites retain behavior under new identities.

- [ ] **Step 1: Run the NDMF bootstrap twice**

Run:

```powershell
pwsh -NoProfile -File ./Tools/Bootstrap-NdmfStandalone.ps1
pwsh -NoProfile -File ./Tools/Bootstrap-NdmfStandalone.ps1
```

Expected: both exit successfully. The second is idempotent. No tracked files change.

- [ ] **Step 2: Rediscover Unity instances and select the public project by actual path**

Read `mcpforunity://instances`. Select only the instance whose project root is the current public checkout. If an external rename changed the local directory, use its actual current path, not the historical path in this plan.

Read `mcpforunity://project/info` and `mcpforunity://editor/state`. Expected: Unity `2022.3.22f1`, public root, editor ready. Do not select the private testbed.

- [ ] **Step 3: Refresh/import and wait for compilation**

Request a Unity asset refresh on the selected public editor. Poll `mcpforunity://editor/state` until `data.compilation.is_compiling` and `is_domain_reload_pending` are false and `data.advice.ready_for_tools` is true.

Read Console errors and warnings. Expected: no relevant package-resolution, missing-script, asmdef, namespace, or compiler errors/warnings.

- [ ] **Step 4: Verify renamed test discovery**

Read/paginate EditMode test discovery and compare it to Task 1 modulo this prefix mapping:

```text
Alrauna.AlphaMaterialOptimizer.Tests.Editor
    -> Alrauna.Amuse.Tests.Editor
```

Expected: the same test method/case names and count are discoverable. No test remains under the old namespace.

- [ ] **Step 5: Run fixture integrity tests**

Run the EditMode group:

```text
Alrauna.Amuse.Tests.Editor.ReferenceFixtures.ReferenceFixtureIntegrityTests
```

Expected: all discovered cases pass, with no catalog path/load failures.

- [ ] **Step 6: Run exact classifier tests**

Run:

```text
Alrauna.Amuse.Tests.Editor.Analysis.TriangleAlphaClassifierTests
```

Expected: all discovered cases pass unchanged.

- [ ] **Step 7: Run separation planner tests**

Run:

```text
Alrauna.Amuse.Tests.Editor.Analysis.MeshSeparationPlannerTests
```

Expected: all discovered cases pass unchanged.

- [ ] **Step 8: Inspect scope after Unity validation**

Run:

```powershell
git status --short
git diff -- Packages/manifest.json Packages/vpm-manifest.json
Get-Content -Raw Packages/packages-lock.json | ConvertFrom-Json | Out-Null
```

Expected: manifest and VPM manifest unchanged. Lockfile contains only the intended embedded package identity change. Git tracks no generated Unity state.

---

### Task 7: Run complete migration validation and adversarial review

**Files:** No new intended files.

**Produces:** Final evidence for behavior preservation, identity completeness, asset integrity, and exact scope.

- [ ] **Step 1: Run the complete EditMode suite**

Run all EditMode tests through the selected public Unity editor with failed-test details enabled.

Expected: every discovered EditMode test passes. Report exact passed/failed/skipped counts. Do not claim the historical 90-test result or the 91-test discovery count as the new run result without observing it.

- [ ] **Step 2: Recheck compilation and Console**

Wait for the editor to become ready, then read Console errors and warnings.

Expected: zero relevant errors and zero relevant warnings after the complete run. Report any unrelated pre-existing entries separately rather than clearing them.

- [ ] **Step 3: Run exhaustive current-surface stale-name search**

Run:

```powershell
git grep -n -i -E 'Alpha Material Optimizer|AlphaMaterialOptimizer|alpha-material-optimizer|alpha_material_optimizer|alpha material optimizer|(^|[^[:alnum:]_])AMO([^[:alnum:]_]|$)' -- ':!docs/superpowers/**'
git ls-files | Select-String -Pattern 'alpha-material-optimizer|AlphaMaterialOptimizer|alpha_material_optimizer' -CaseSensitive:$false
rg -n -i 'Alpha Material Optimizer|AlphaMaterialOptimizer|alpha-material-optimizer|alpha_material_optimizer|alpha material optimizer|(^|[^A-Za-z0-9_])AMO([^A-Za-z0-9_]|$)' AGENTS.md README.md docs/architecture Packages ProjectSettings Website .github Tools
```

Expected:

- no old product identity in current policy, README, package, project settings, website, workflows, or tooling.
- path search returns no old package/asmdef path.
- old identifiers remain only in dated historical Superpowers records and the two AMUSE migration documents that inventory the transition.

- [ ] **Step 4: Verify all selected new identities**

Run:

```powershell
rg -n 'AMUSE|Alrauna''s Material Understanding & Simplification Engine|com\.alrauna\.amuse|Alrauna\.Amuse' AGENTS.md README.md docs/architecture Packages ProjectSettings Website
```

Expected: every canonical surface is present and consistently cased. No redundant `AMUSE Engine` or `AMUSE Toolkit` branding exists.

- [ ] **Step 5: Re-run GUID and package-integrity checks**

Repeat Task 3 Step 7. Then run:

```powershell
git diff --name-status --find-renames=50%
git diff --numstat --find-renames=50% -- Packages/com.alrauna.amuse
```

Expected: all logical assets retain GUIDs, no duplicates exist, and algorithm/fixture files show identity-only textual changes or pure moves.

- [ ] **Step 6: Validate JSON and unchanged dependency boundaries**

Run:

```powershell
Get-Content -Raw Packages/com.alrauna.amuse/package.json | ConvertFrom-Json | Out-Null
Get-Content -Raw Packages/com.alrauna.amuse/Editor/Alrauna.Amuse.Editor.asmdef | ConvertFrom-Json | Out-Null
Get-Content -Raw Packages/com.alrauna.amuse/Tests/Editor/Alrauna.Amuse.Tests.Editor.asmdef | ConvertFrom-Json | Out-Null
Get-Content -Raw Packages/packages-lock.json | ConvertFrom-Json | Out-Null
Get-Content -Raw Packages/manifest.json | ConvertFrom-Json | Out-Null
Get-Content -Raw Packages/vpm-manifest.json | ConvertFrom-Json | Out-Null
rg -n 'com\.coplaydev\.unity-mcp|CoplayDev|MCPForUnity' Packages/com.alrauna.amuse
```

Expected: all JSON parses. No CoplayDev/MCP dependency or API enters the distributable package. NDMF dependency range remains unchanged.

- [ ] **Step 7: Review unstaged and staged scope separately**

Run:

```powershell
git diff --check
git diff --stat
git diff --name-status --find-renames=50%
git diff --cached --check
git diff --cached --stat
git status --short --branch
```

Expected:

- no whitespace errors.
- only the two design-phase docs plus approved migration files changed.
- no staged changes unless the user separately requested staging.
- no generated `Library`, `Temp`, `Logs`, `UserSettings`, IDE, or private-testbed files.

- [ ] **Step 8: Run the adversarial checklist**

Confirm explicitly:

- `AGENTS.md` remains policy and retained all Git/test/MCP/asset/release protections.
- README/vision claim no future features as implemented.
- no partial old package/namespace/assembly/friend/test identity remains.
- no manifest/lock contradiction exists.
- no `.meta` regeneration, GUID churn, or duplicate GUID exists.
- no classifier/planner/fixture semantic edit occurred.
- no private-testbed mutation occurred.
- no repository setting, release, commit, push, or history rewrite occurred.

---

### Task 8: Prepare the external cutover handoff without executing it

**Files:** No tracked edits unless final validation finds a documentation defect.

**Produces:** A concise post-merge runbook and explicit authorization boundaries.

- [ ] **Step 1: Report required separately authorized GitHub actions**

List, but do not execute:

1. rename `Alrauna/alpha-material-optimizer-ndmf` to `Alrauna/amuse`.
2. set Actions variable `PACKAGE_NAME` to `com.alrauna.amuse`.
3. verify repository description/homepage, Pages environment, listing URL, branch protection, Actions variables/secrets/environments, and external listing sources.
4. verify generated release artifact names and VPM listing keys before any release.

- [ ] **Step 2: Provide the local remote update procedure**

After the external rename only:

```powershell
git remote set-url origin https://github.com/Alrauna/amuse.git
git fetch --prune origin
git remote -v
git status --short --branch
```

Do not run these commands until the repository rename actually happens.

- [ ] **Step 3: State VPM migration behavior**

Report that `com.alrauna.amuse` is a distinct package, not an upgrade. Existing users must remove `com.alrauna.alpha-material-optimizer` and add AMUSE explicitly. Do not add a compatibility shim or delete old published releases without a separately approved migration/release policy.

- [ ] **Step 4: Stop for review**

Report exact files changed, rename/GUID evidence, test counts/results, compiler/Console results, bootstrap results, stale-name classification, and skipped validation. Report remaining external actions, Git status, and private-testbed non-use/non-modification.

Do not commit, push, open a PR, rename the repository, change GitHub settings, or publish.

## Approval and execution gate

This plan is documentation only until explicitly approved together with `docs/superpowers/specs/2026-08-15-amuse-rebrand-design.md`.
