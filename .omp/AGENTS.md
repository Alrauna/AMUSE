# AGENTS.md — AMUSE / OMP shared context

This file is shared by every OMP session in this repository. It defines project facts and safety boundaries, not the active role.

The Desktop launchers append exactly one role overlay:

- `.omp/roles/controller/AGENTS.md`
- `.omp/roles/implementation/AGENTS.md`

If neither overlay is present in the session context, do not assume either role or begin consequential work. Ask the user which role is intended.

## Repository and product

AMUSE — Alrauna's Material Understanding & Simplification Engine — is a nondestructive build-time optimizer for VRChat avatars.

Product package:

`Packages/com.alrauna.amuse/`

Public reusable research tooling:

`Packages/com.alrauna.amuse.research/`

The research package must never ship in the product/VPM listing.

The root Unity project contains public synthetic fixtures, development support, tests, validation, and CI. Reusable code may be public; private avatars, vendor assets, consent/identity data, and raw Census observations may not.

Public fixtures must be minimal, deterministic, redistributable, and legally publishable.

## Correctness and uncertainty

Preserve intended visual appearance, avatar-controlled functional behavior, supported shader/material behavior, and the compatibility assumptions declared by the active optimization policy.

Policy-authorized representation changes are valid. AMUSE does not require framebuffer identity under every hypothetical interaction.

A false positive violates the declared transformation contract or its preconditions. False negatives are safer, but unnecessary refusal remains a coverage defect.

Unknown information should invalidate only conclusions that depend on it. Do not escalate a local unsupported fact into renderer-wide or avatar-wide refusal unless the dependency genuinely requires that scope. Increasing uncertainty must never make a transformation more aggressive.

Programming and invariant failures remain defects; never blanket-catch them into "unsupported."

## Repository reality and scope

Current code, tests, pinned source behavior, reproducible characterization, and real-avatar evidence outrank old plans, summaries, or architectural intent.

Keep current work distinct from discovered prerequisites, future architectural pressure, and speculative opportunities. Do not silently expand the active task.

Prefer the smallest complete solution for a real consumer. Do not introduce universal shader/compiler infrastructure, broad semantic IRs, generalized mutation frameworks, global planners, cross-host abstractions, or third-party extension APIs without a present requirement.

Keep shader-specific behavior shader-specific unless materially different consumers justify a shared abstraction. Poiyomi and lilToon should pressure shared seams independently.

## Semantic and render-state boundaries

`MaterialSemantics` describes narrow output facts such as base color, alpha, emission, and normal. It is not a shader property database, render-state model, optimizer API, or universal shader graph.

Render-state evidence and shader-specific conversion capabilities remain separate from generic semantic facts. Do not treat editor-facing mode labels as authoritative when effective blend, depth, queue, or shader state can diverge.

Version-pinned shader support is acceptable during `0.x`. Correctness-relevant shader behavior must be attested against the supported source/version and fail closed when accepted source changes.

## NDMF and mutation boundary

Reason about the effective build avatar after upstream nondestructive tools have run. Use NDMF's actual lifecycle, ordering, generated-asset, and replacement facilities rather than parallel host abstractions.

Do not assume mutable Unity objects remain unchanged across unrelated NDMF phases. Capture evidence close enough to its consumer that it still describes effective build state.

Source meshes, materials, textures/import settings, animation assets, prefabs, and scenes are authoring evidence, not AMUSE mutation targets. AMUSE-owned mutation belongs on the NDMF build copy and generated build assets.

Preferred boundary:

capture → analyze → prepare → validate → minimal Apply

## Alpha and texture direction

Alpha optimization is the current proving ground, not AMUSE's organizing principle.

For an original AlphaTest/AlphaBlend material, triangles not proven safe remain on the original material; triangles proven visually opaque may move to an appended submesh using an AMUSE-generated canonical opaque material. AlphaTest and AlphaBlend remain distinct source modes.

A texture-backed triangle may still be proven opaque when its entire relevant sampled domain is opaque. Do not assume that making a texture readable resolves correctness for mipmapped, compressed, filtered, wrapped, or color-space-dependent runtime sampling.

## Unity package and MCP safety

Treat Unity assets and `.meta` files as one logical unit. Avoid GUID churn and never commit generated/local Unity state such as `Library/`, `Temp/`, `Logs/`, `UserSettings/`, or generated IDE files.

Package manifests and lockfiles change only for intentional dependency work. Before restoring suspected Unity-generated toolchain/sysroot churn, inspect the complete diff and confirm no intentional edit shares either file.

Unity MCP is development and observability tooling, never a product dependency or correctness oracle.

Before any Unity MCP write, broad operation, or MCP result used as reported validation:

1. enumerate reachable instances read-only;
2. inspect `Application.dataPath`;
3. normalize the path;
4. require an exact match to the intended project;
5. pin the intended instance when more than one is reachable.

For this repository, the required public-project identity is:

`Application.dataPath == <repo-root>/Assets`

A case-only match is not confirmed identity. Stop rather than guess.

## Census Lab

Private root: `Assets/!CENSUSLAB/`

Authoritative scene corpus: `Assets/!CENSUSLAB/Scenes/`

Private launcher location: `Assets/!CENSUSLAB/Scripts/Editor/`

Census Lab is for characterization and validation, not the correctness oracle. Prefer read-only use and reduce failures to public synthetic fixtures where practical.

Preserve the privacy tiers: Tier 1 raw observations, Tier 2 run-local anonymized intermediate, and Tier 3 privacy-reviewed aggregate output. Only reviewed aggregate information may leave the Lab by default.

Never expose private names, paths, GUIDs, identifiers, per-avatar/per-renderer rows, or fingerprint-like structure. Never create publishable Census metrics without privacy review.

## Git and evidence

Treat existing changes as user-owned unless proven otherwise. Do not discard, overwrite, restage, or absorb unrelated work.

Do not stage, commit, amend, push, open or merge a PR, delete branches, rewrite history, publish, or change remotes/settings without explicit authorization for that action.

Checked-out source and observed results outrank agent reports. Never claim a test, build, reproduction, benchmark, or validation passed unless it was actually run and observed.
