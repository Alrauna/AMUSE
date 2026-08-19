# AMUSE Agent Policy

This file governs agentic work in this repository. Direct user instructions take precedence. Treat `MUST`, `NEVER`, and `REQUIRE` as hard requirements; treat `SHOULD` and `PREFER` as defaults that may be changed when the task gives a concrete reason.

## Project and repository boundaries

AMUSE — Alrauna's Material Understanding & Simplification Engine (`com.alrauna.amuse`) is an MIT-licensed material optimization system. Unity/NDMF is its current host integration. AMUSE analyzes material, texture, rendering, and state semantics to plan and apply simplifications only when the active optimization policy has sufficient evidence that they preserve the required behavior.

This Git repository serves two roles:

- It is the public source repository.
- It is a minimal Unity package-development and test project.

Where policy or a task needs the repository root, derive it at run time from `git rev-parse --show-toplevel`; this document calls that value `<repo-root>`. Prefer repository-relative paths, written with forward slashes, in code, documentation, tests, and task instructions. NEVER hard-code an absolute checkout path, drive letter, or user home directory into policy, tooling, or a task rule: the same repository is developed from Windows, macOS, and Linux checkouts, and no platform is canonical. When an absolute path is genuinely required, build and compare it through the platform's own path API instead of concatenating separators by hand.

The distributable package lives under `Packages/com.alrauna.amuse/`. Keep product code, package metadata, and package tests inside that package unless Unity project-level integration requires otherwise. The root Unity project exists for reproducible development, synthetic fixtures, automated tests, package validation, and CI.

Public fixtures MUST be purpose-built, redistributable, deterministic, minimal, and legally safe to publish. NEVER add private avatars, purchased assets, unredistributable shaders, credentials, or other private testbed content to this repository.

The current production implementation is Editor-only and contains exact alpha analysis plus immutable mesh-separation planning. These are foundational AMUSE subsystems, not the complete product. Reinspect the repository rather than treating this snapshot as a permanent architecture claim.

## Private Unity avatar testbed

A separate private Unity project contains real VRChat avatars and real-world dependencies. It references this working tree's package as a local Unity package and is accessible through the CoplayDev-connected Unity MCP. Treat it as an external integration-test appliance, NEVER as optimizer source or a source of publishable fixtures.

Agents MUST NOT:

- copy private testbed assets or derived private content into this repository;
- assume any testbed asset, shader, package, path, or avatar is publishable;
- persistently alter testbed scenes, prefabs, materials, assets, project settings, or package state merely to make a test pass;
- “fix” an optimizer failure by changing the avatar under test;
- commit or publish content from the private testbed.

Persistent testbed changes require explicit task scope. Prefer read-only inspection. Before any write, destructive, or broad Unity MCP operation, confirm from the task's explicit scope that the connected instance is the intended private testbed and not the public development project, state why the mutation is necessary, keep it minimal and reversible, and avoid saved fixture changes when equivalent validation is possible without them.

The testbed may live anywhere on disk and MUST NOT be identified by a hard-coded path, a drive letter, or a sibling-directory convention. Identify the public development project positively, by the rule in *Unity MCP use*, and treat every other reachable Unity project as unacceptable for public-project test or evidence work unless a task explicitly says otherwise. Never inspect or modify the testbed merely to work out which project is the public one.

## Start-of-task discipline

Before editing:

1. Read this file and the direct task.
2. Inspect `git status`, the current branch, relevant diffs, and recent history. Existing changes belong to the user unless proven otherwise; NEVER overwrite, discard, restage, or fold unrelated work into the task.
3. Inspect the files, call sites, tests, package metadata, and workflows relevant to the requested change. Repository handoffs and prior summaries are context, not authority; verify claims against current state. Historical specs and plans under `docs/superpowers/` record the environment of the milestone that produced them; their absolute paths, drive letters, shells, and machine details are history, not current policy.
4. Inspect the currently installed skill documentation when a skill applies. Do not assume a skill name, trigger, or workflow from memory.
5. Identify the narrowest validation that can prove the requested result.

Do not expand scope silently. If a newly discovered concern is unrelated, report it and leave it for a separate task or branch.

## Branch and Git discipline

`main` is the known-good integrated state. For non-trivial work, start from an up-to-date `main` and create a focused topic branch. Use understandable prefixes such as `feat/`, `fix/`, `test/`, `chore/`, `docs/`, or `refactor/`. Keep one coherent concern per branch; do not start unrelated work on an existing topic branch.

If the checkout already contains changes, determine their ownership and relation to the task before switching branches, creating a worktree, staging, or editing. Stop for direction when safe isolation is not possible. Do not directly push implementation work to `main`, rewrite shared/published history for cosmetic reasons, or commit/push unless the task authorizes it.

Before completion, inspect the working-tree and staged diffs separately, verify that only intended files changed, and run the required validation. When coherent branch work is complete, recommend review before unrelated work; use a fresh task/branch and update a handoff when that materially helps continuity.

## Development approach

Prefer narrow vertical increments. Do not attempt the entire optimizer unless explicitly tasked. Current alpha analysis and separation planning are one subsystem within AMUSE; later work requires explicit scope or a demonstrated dependency from the current phase.

Keep the durable product and architecture direction in `docs/architecture/vision.md`, not in this policy. Document future boundaries without creating speculative directories, interfaces, registries, schemas, dependency injection, or orchestration frameworks before implemented behavior needs them.

## Superpowers and Ponytail

Superpowers governs process and engineering discipline. Use the installed Superpowers skills when their documented triggers apply. In the current installation this includes, among others, brainstorming before creative behavior or architecture work; writing plans for multi-step implementation; test-driven development for features and bug fixes; systematic debugging before speculative fixes; verification before completion claims; review workflows; and branch/worktree workflows where appropriate. Follow the installed skill rather than duplicating its full procedure here. If a required capability is unavailable, use the skill's documented fallback where possible and report the limitation. A direct task constraint, such as a one-file edit, overrides a skill's default artifact location or extra-file convention.

Ponytail governs implementation simplicity and scope restraint. For coding, code design, dependency choice, refactoring, and code review, use the installed `ponytail:ponytail` skill when its trigger applies. Inspect its installed documentation and follow the version present in the environment rather than relying on remembered invocation syntax or defaults. Its ladder is: question speculative need, reuse repository code, prefer the standard library or native platform, reuse installed dependencies, and only then add the minimum code required. The separate audit, review, debt, gain, and help skills are one-shot tools with their own installed triggers; do not substitute them for the core coding workflow.

Ponytail NEVER justifies skipping required tests, weakening safety or correctness, deleting useful diagnostics, ignoring compatibility boundaries, or replacing observed validation with an assumption. Resolve tension in this order:

1. correctness and behavior preservation;
2. project safety invariants;
3. explicit task requirements;
4. required Superpowers workflow;
5. Ponytail minimalism.

Use Superpowers to determine how work is performed safely; use Ponytail to challenge how much code needs to exist.

## Safety and behavior preservation

AMUSE is proof-first. Under the active optimization policy:

- proven behavior-preserving: transform;
- uncertain or unsupported: preserve and explain why.

Additional uncertainty MUST NEVER make optimization more aggressive. For the current alpha subsystem, only `ProvenOpaque` may become an opaque candidate; `MustRemainTransparent` and `Unknown` remain on the transparent path. False negatives are acceptable when proof is insufficient. False positives—content transformed without sufficient proof—are correctness bugs.

Do not assume arbitrary shaders, material swaps, animations, UV animation, texture behavior, or other dynamic state is safe. Do not trade avatar behavior for optimization coverage. Prefer actionable diagnostics for skipped optimization over unsafe guessing.

Never modify original avatar source assets as part of optimization. Build-time/generated transformations must use the intended nondestructive pipeline.

## Source and dependency boundaries

Keep runtime footprint minimal. Separate, when actually needed:

- serialized or runtime-facing components;
- Editor-only analysis and build logic;
- tests.

Most optimizer intelligence SHOULD be Editor-only. Reusable analysis and planning SHOULD consume normalized immutable inputs, remain deterministic, and avoid unnecessary coupling to NDMF, the VRChat SDK, live Unity objects, assets, MCP, or Editor state. Unity/NDMF owns the current host extraction, build integration, and host-specific transformation boundaries. Keep analysis and planning separable from those boundaries where practical, but do not extract a standalone shared library until another real consumer justifies it.

Before adding a dependency, check Unity, NDMF, the VRChat SDK, the C# standard library, and existing project dependencies. A new dependency requires a concrete need and justification. Do not copy code wholesale from reference projects merely because their license permits it; understand any reused logic and preserve required attribution for copied or substantially derived work.

## Unity asset integrity

Treat each Unity asset and its `.meta` file as one logical unit. Agents MUST NOT casually delete, regenerate, replace, or separate `.meta` files from their assets. GUID churn is potentially destructive: treat it as a compatibility and reference change, not ordinary cleanup. Prefer Unity-aware moves and renames where references matter. Before accepting Unity asset changes, inspect for unexpected GUID changes and broken references where practical.

NEVER commit generated or local Unity state such as `Library/`, `Temp/`, `Logs/`, `UserSettings/`, generated IDE solution/project files, or other ignored build/editor artifacts.

`Packages/manifest.json`, `Packages/packages-lock.json`, and `Packages/vpm-manifest.json` SHOULD change only when dependency or package configuration actually changes. Inspect incidental Unity Package Manager churn rather than blindly staging or accepting it.

The private testbed's local package reference to the working-tree package is intentional. Normal development and testing MUST NOT replace it with copied, exported, or duplicated package contents. Assets, materials, and meshes generated during NDMF or build-time optimization are disposable build outputs unless the task explicitly defines them as source fixtures; do not persist them back into avatar or package source accidentally.

## Testing policy

Testing is part of implementation. New deterministic behavior normally requires focused tests. A reproducible bug fix normally requires a regression test that fails before the fix and passes afterward. NEVER weaken, delete, skip, or rewrite a valid test merely because the implementation cannot pass it.

Unit tests SHOULD cover deterministic logic such as geometry classification, UV handling, alpha sampling, texture filter/wrap semantics, normalized material semantics, material-state set operations, animation-binding analysis, optimization planning, transformation planning, and profitability calculations. Keep tests fast, minimal, isolated where possible, deterministic, and understandable from failures.

Where practical, test analysis and optimization planning independently from mutation. The same deterministic input SHOULD first produce a deterministic optimization plan before mesh, material, animation, or NDMF transformation is exercised. This separation should make failures attributable to classification or state-analysis defects, optimization-plan defects, or transformation and execution defects. It is a testing boundary, not a prescribed class hierarchy or implementation architecture.

Synthetic reference fixtures are executable specifications. Prefer tiny fixtures that isolate one semantic rule over production avatars, and use machine-readable expected results where useful. Unsupported or ambiguous inputs must demonstrate conservative refusal rather than guessed optimization.

Smoke tests complement unit tests; they do not replace them. As applicable, verify that Unity imports without relevant compile errors, the package resolves, assemblies load, NDMF integration initializes, representative processing completes, generated output is structurally valid, source assets remain unchanged, and no unexpected Console errors appear.

Use the private testbed for real-world integration and compatibility, not as the only test oracle. When it reveals a reproducible bug:

1. understand the failure in the private testbed;
2. reduce it to a minimal public synthetic or redistributable fixture when practical;
3. add the public regression test;
4. implement the fix;
5. retest both the minimal case and the private avatar.

Never claim a test passed unless it was actually run and its result observed.

## Unity MCP use

Unity MCP is for integration and observability, not the primary correctness oracle. Discover the current instances, project information, editor state, resources, and enabled tool groups before acting; tool availability can change. Prefer summary-first, paged, read-only queries.

CoplayDev Unity MCP is a development-project dependency only. The root Unity project may depend on it solely for agent observability, test execution, and Unity Editor automation. It MUST NOT be added to `Packages/com.alrauna.amuse/package.json`, the package's `vpmDependencies`, or product runtime or Editor code as a functional dependency. The distributable package must remain independently usable without CoplayDev installed; do not copy MCP APIs, binaries, generated files, or configuration into the product package.

The public development project exists for deterministic repository tests and package development; the private avatar testbed exists for real-avatar integration testing. The public development project is the Unity project rooted at `<repo-root>`, and its Unity data path is `<repo-root>/Assets`. Before any write or broad operation, and before any test run whose result will be reported, agents MUST use read-only discovery to enumerate the reachable instances and select the one whose `Application.dataPath` equals `<repo-root>/Assets` once both sides are normalized: resolve relative and symbolic segments, unify separators to `/`, and drop any trailing separator. Compare the normalized values exactly. Because filesystems differ in case sensitivity across and within platforms, two paths that match only by letter case are unconfirmed identity: stop and report rather than guessing. Never compare against a hard-coded absolute path, and never assume a reachable Unity Editor is the correct instance.

Appropriate uses include confirming the connected project and package, inspecting hierarchy/components/materials/renderers, reading Console output, discovering or running Unity tests, exercising NDMF builds, entering Play Mode when required, inspecting generated avatar state, and reproducing failures that cannot reasonably be tested outside the Editor.

Do not use MCP to silently change fixtures, to replace deterministic repository tests, or to operate on a project whose identity is uncertain. Enabling a tool group or invoking a tool does not authorize persistent testbed mutations.

## CI and release safety

CI MUST reproduce important validation without the private testbed. Progressively add the cheapest deterministic gates that cover project/package compilation, fast unit/EditMode tests, reference-fixture tests, NDMF/package integration, package validation, and release construction as those layers become real.

Ordinary correctness CI MUST NOT depend on private avatars, commercial assets, the developer's Unity MCP session, or secrets that are unrelated to the test. When a feature introduces a validation layer that should remain an ongoing gate, update CI in the same branch.

Inspect existing release and listing automation before changing it. Do not modify release workflows casually or treat a release as the first validation. Publishing, tagging, deleting releases, changing repository settings, or otherwise performing deployment-like operations requires explicit authorization separate from local/PR validation.

## Completion standard

Before saying work is complete:

1. review the task requirements line by line;
2. inspect unstaged and staged diffs and confirm only intended files changed;
3. run the complete relevant validation and observe its exit/result;
4. check for contradictions, unsafe assumptions, and scope drift.

Report:

- what changed;
- what tests or validation ran and their observed results;
- what validation was skipped and why;
- remaining risks or unsupported cases;
- whether the private Unity MCP testbed was used and whether it was modified.

Do not equate code written, one successful compile, or an agent/subagent report with completion. Evidence precedes completion claims.
