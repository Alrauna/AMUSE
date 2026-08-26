# AMUSE Agent Policy

This file supplements the global agent policy with AMUSE-specific requirements.

## Repository boundaries

AMUSE (`com.alrauna.amuse`) is a proof-first material optimization system currently integrated through Unity/NDMF.

The repository is both public source and a minimal Unity package-development/test project.

- Product package: `Packages/com.alrauna.amuse/`
- Public research tooling: `Packages/com.alrauna.amuse.research/` — never released with the product/VPM listing.
- Root Unity project: synthetic fixtures, development, tests, validation, and CI.

Keep reusable source here even when only private fixtures exercise it. The boundary is **public code vs. private data**, not production vs. research code.

Public fixtures MUST be minimal, deterministic, redistributable, and legally publishable. NEVER commit private avatars, purchased/unredistributable assets or shaders, credentials, or Census Lab content.

The current Editor-only alpha-analysis and mesh-separation implementation is a snapshot, not a permanent architecture definition. Durable direction belongs in `docs/architecture/vision.md`.

## AMUSE-Census-Lab

AMUSE-Census-Lab is the private real-avatar integration/census environment. It references this working tree's packages locally and is never a second source-code home or source of public fixtures.

Private avatars, consent/identity data, vendor packages, and raw/intermediate census data remain there. Only privacy-reviewed aggregate output may leave. Reusable AMUSE/research code remains in this repository.

NEVER:

- copy or publish private Lab content;
- assume Lab content is redistributable;
- alter an avatar to make AMUSE pass;
- persistently mutate Lab assets/settings merely to satisfy a test.

Prefer read-only Lab use. Persistent Lab changes require explicit task scope and must be necessary, minimal, and reversible. Permission to mutate the Lab never overrides AMUSE's nondestructive-optimization rules.

The Lab may live anywhere. Never identify it by a hard-coded path or directory convention.

## Correctness model

AMUSE transforms only behavior proven safe under the active optimization policy:

- proven safe → transform;
- uncertain/unsupported → preserve and diagnose.

More uncertainty MUST NEVER produce more aggressive optimization.

For the current alpha subsystem:

- `ProvenOpaque` may become opaque;
- `MustRemainTransparent` and `Unknown` remain transparent.

False negatives are acceptable when proof is insufficient. False positives are correctness bugs.

Do not assume arbitrary shader, material, animation, UV, texture, or other dynamic state is safe.

Never modify original avatar source assets during optimization; generated/build-time transformations must remain nondestructive.

## Architecture and dependencies

Most optimizer intelligence SHOULD remain Editor-only and operate on normalized, immutable, deterministic inputs where practical.

Keep analysis/planning reasonably separable from Unity/NDMF host extraction, build integration, live Unity objects, assets, MCP, and Editor state. Do not create a standalone shared library until a real second consumer exists.

Before adding dependencies, specifically check Unity, NDMF, the VRChat SDK, C#, and existing project dependencies.

## Unity integrity

Treat every Unity asset and `.meta` file as one logical unit. Avoid GUID churn; treat GUID changes as compatibility/reference changes and use Unity-aware moves where references matter.

NEVER commit generated/local Unity state such as `Library/`, `Temp/`, `Logs/`, `UserSettings/`, or generated IDE files.

`Packages/manifest.json`, `Packages/packages-lock.json`, and `Packages/vpm-manifest.json` should change only for intentional package/dependency configuration.

Unity may add host-specific toolchain/sysroot dependencies based on the local Editor installation. These MUST NOT be committed unless AMUSE has a documented in-repository requirement for that build capability.

If `manifest.json`/`packages-lock.json` contain only confirmed host-generated churn, inspect the full diff first, then restore them:

```bash
git checkout HEAD -- Packages/manifest.json Packages/packages-lock.json
```

NEVER use that restore when intentional changes share either file. Do not carry, stage, or fold machine-generated package churn into unrelated branches.

Census Lab local-package references are intentional. Do not replace them with copied package contents.

NDMF/generated optimization assets are disposable outputs unless explicitly designated source fixtures; never persist them into source avatars accidentally.

## AMUSE testing

Apply the global testing policy.

Prefer focused deterministic tests for geometry/UV/alpha/texture semantics, normalized material/state analysis, animation analysis, optimization/transformation planning, and profitability logic.

Where practical, test deterministic analysis and optimization plans before mutation/integration so failures remain attributable.

Synthetic fixtures are executable specifications. Prefer tiny public fixtures isolating one semantic rule. Unsupported/ambiguous cases must demonstrate conservative refusal.

Applicable smoke validation includes package/assembly loading, Unity compilation, NDMF initialization, representative processing, structurally valid output, unchanged source assets, and absence of unexpected Console errors.

Use Census Lab as real-world integration evidence, never as the only oracle. For reproducible Lab failures, when practical:

1. understand the private failure;
2. reduce it to a minimal public fixture;
3. add a regression test;
4. fix it;
5. retest both cases.

## Unity MCP

Unity MCP is for integration and observability, not the primary correctness oracle. Discover reachable instances/editor state/tool availability before acting and prefer read-only, summary-first queries.

CoplayDev MCP is development tooling only. It MUST NOT become a dependency of `Packages/com.alrauna.amuse/`, its `vpmDependencies`, or product code. AMUSE must remain usable without it.

The public development Unity project is rooted at `<repo-root>` with `Application.dataPath == <repo-root>/Assets`.

Before any MCP write/broad operation or any MCP test result that will be reported:

1. enumerate reachable instances read-only;
2. normalize both candidate paths by resolving relative/symbolic segments, using `/`, and removing trailing separators;
3. require an exact path match.

A case-only match is not confirmed identity; stop rather than guess. Never use a hard-coded absolute path or assume the reachable Editor is correct.

MCP may inspect project/avatar state, Console output, run Unity tests/builds, exercise NDMF, and reproduce integration failures. It MUST NOT silently mutate fixtures or replace deterministic repository tests.

## CI and completion

AMUSE correctness CI MUST remain reproducible without Census Lab, private avatars, commercial assets, an interactive MCP session, or unrelated secrets.

Add deterministic gates as appropriate for compilation, EditMode/unit tests, public fixtures, NDMF/package integration, package validation, and release construction.

In addition to the global completion report, state:

- whether Census Lab was used;
- whether it was modified;
- relevant unsupported or conservatively refused cases.