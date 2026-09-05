# AGENTS.md — AMUSE / OMP

One file defines every OMP session in this repository. It holds the shared project context.

## Repository and product

AMUSE — Alrauna's Material Understanding & Simplification Engine — is a nondestructive build-time optimizer for VRChat avatars.

Product package:

`Packages/com.alrauna.amuse/`

Public reusable research tooling:

`Packages/com.alrauna.amuse.research/`

The research package must never ship in the product/VPM listing.

The root Unity project contains public synthetic fixtures, development support, tests, validation, and CI. Reusable code may be public. Private avatars, vendor assets, consent/identity data, and raw Census observations may not be public.

Public fixtures must be minimal, deterministic, redistributable, and legally publishable.

## Correctness and uncertainty

Preserve the intended visual appearance, avatar-controlled functional behavior, supported shader/material behavior, and compatibility assumptions that the active optimization policy declares.

Policy-authorized representation changes are valid. AMUSE does not require framebuffer identity under every hypothetical interaction.

A false positive violates the declared transformation contract or its preconditions. False negatives are safer, but unnecessary refusal remains a coverage defect.

Unknown information should invalidate only conclusions that depend on it. Do not expand a local unsupported fact into renderer-wide or avatar-wide refusal unless the dependency requires that scope. Increased uncertainty must never make a transformation more aggressive.

Programming and invariant failures remain defects. Never use a blanket catch to mark them as "unsupported."

## Repository reality and scope

Current code, tests, pinned source behavior, reproducible characterization, and real-avatar evidence have priority over old plans, summaries, or architectural intent.

Keep current work separate from discovered prerequisites, future architectural pressure, and speculative opportunities. Do not silently expand the active task.

Prefer the smallest complete solution for a real consumer. Do not introduce broad infrastructure or APIs without a present requirement. This includes universal shader/compiler infrastructure, broad semantic IRs, generalized mutation frameworks, global planners, cross-host abstractions, and third-party extension APIs.

Keep shader-specific behavior shader-specific unless materially different consumers justify a shared abstraction. Poiyomi and lilToon should apply pressure to shared seams independently.

## Semantic and render-state boundaries

`MaterialSemantics` describes narrow output facts such as base color, alpha, emission, and normal. It is not a shader property database, render-state model, optimizer API, or universal shader graph.

Keep render-state evidence and shader-specific conversion capabilities separate from generic semantic facts. Do not treat editor-facing mode labels as authoritative when effective blend, depth, queue, or shader state can differ.

Version-pinned shader support is acceptable during `0.x`. Attest correctness-relevant shader behavior against the supported source/version. Fail closed when the accepted source changes.

## NDMF and mutation boundary

Examine the effective build avatar after upstream nondestructive tools run. Use NDMF's actual lifecycle, ordering, generated-asset, and replacement facilities instead of parallel host abstractions.

Do not assume mutable Unity objects stay unchanged across unrelated NDMF phases. Capture evidence sufficiently close to its consumer so it still describes the effective build state.

Source meshes, materials, textures/import settings, animation assets, prefabs, and scenes are authoring evidence, not AMUSE mutation targets. Apply AMUSE-owned mutations only to the NDMF build copy and generated build assets.

Preferred boundary:

capture → analyze → prepare → validate → minimal Apply

## Alpha and texture direction

Alpha optimization is the current proving ground, not AMUSE's organizing principle.

For an original AlphaTest/AlphaBlend material, triangles not proven safe remain on the original material. Triangles proven visually opaque may move to an appended submesh that uses an AMUSE-generated canonical opaque material. AlphaTest and AlphaBlend remain distinct source modes.

A texture-backed triangle may still be proven opaque when its entire relevant sampled domain is opaque. Do not assume a readable texture ensures correct runtime sampling. Runtime sampling can depend on mipmaps, compression, filtering, wrapping, or color space.

## Unity package and MCP safety

Treat Unity assets and `.meta` files as one logical unit. Avoid GUID churn. Never commit generated/local Unity state such as `Library/`, `Temp/`, `Logs/`, `UserSettings/`, or generated IDE files.

Change package manifests and lockfiles only for intentional dependency work. Before restoring suspected Unity-generated toolchain/sysroot churn, inspect the complete diff. Confirm that neither file contains an intentional edit.

Unity MCP is development and observability tooling, never a product dependency or correctness oracle.

Before any Unity MCP write, broad operation, or use of an MCP result as reported validation:

1. enumerate reachable instances read-only;
2. inspect `Application.dataPath`;
3. normalize the path;
4. require an exact match to the intended project;
5. pin the intended instance when more than one is reachable.

For this repository, the required public-project identity is:

`Application.dataPath == <repo-root>/Assets`

A case-only match does not confirm identity. Stop instead of guessing.

## Census Lab

Private root: `Assets/!CENSUSLAB/`

Authoritative scene corpus: `Assets/!CENSUSLAB/Scenes/`

Private launcher location: `Assets/!CENSUSLAB/Scripts/Editor/`

Census Lab is for characterization and validation, not the correctness oracle. Prefer read-only use. Reduce failures to public synthetic fixtures where practical.

Preserve the privacy tiers: Tier 1 raw observations, Tier 2 run-local anonymized intermediate, and Tier 3 privacy-reviewed aggregate output. By default, only reviewed aggregate information may leave the Lab.

Never expose private names, paths, GUIDs, identifiers, per-avatar/per-renderer rows, or fingerprint-like structure. Never create publishable Census metrics without privacy review.

## Git and evidence

Treat existing changes as user-owned unless proven otherwise. Do not discard, overwrite, restage, or absorb unrelated work.

Do not stage, commit, amend, push, open or merge a PR, delete branches, rewrite history, publish, or change remotes/settings without explicit authorization.

Checked-out source and observed results have priority over agent reports. Never claim a test, build, reproduction, benchmark, or validation passed unless you ran and observed it.

## Working discipline

These rules bind every session.

Brief implementation work in a written prompt. The prompt states:

- the base branch and commit
- the exact scope and the allowed mutations
- the required RED/GREEN evidence
- the validation steps and the expected report
- the stop conditions and the Git authorization boundary

Never hide an unresolved decision inside an implementation prompt.

Stop and return evidence, options, and a recommendation when work reveals a broader abstraction, a new subsystem, a changed correctness contract, significant scope expansion, or a contradiction in the approved plan. A stop line limits production scope. It does not limit the investigation that explains the blocker.

Respect approval gates. Never use another tool to bypass a denied operation.

Before an important decision, test the assumptions. Name whether the assumed Unity, VRChat, NDMF, or shader behavior is verified or inferred. Check whether a mature ecosystem tool exposes a missing practical constraint. Check whether the requested guarantee is stricter than the product needs. Check whether uncertainty has too broad a scope, whether build ordering could make captured evidence stale, and whether the proposed tests fail with a plausible wrong implementation.

When a plan or design is infeasible, unnecessarily strict, too general, misaligned with Unity or NDMF reality, or debt-creating, say so directly.

Use the narrowest validation layer that can disprove the behavior. Then expand validation with the blast radius. Applicable layers include unit and semantic tests, characterization, Unity EditMode tests, NDMF build tests, public synthetic fixtures, source-preservation checks, and authorized Census validation. Unsupported cases must clearly show conservative refusal.

Empirical evidence is not automatically universal proof. Do not demand mathematical proof when the declared product contract needs a well-characterized compatibility guarantee.

When you find an independent prerequisite, do not mix it into the current work. Park the current work. Complete the prerequisite separately from fresh `main`. Resume the consumer from updated `main`.

