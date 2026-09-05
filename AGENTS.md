# Repository Guidelines

## Project Overview

AMUSE (Alrauna's Material Understanding & Simplification Engine) is a nondestructive build-time optimizer for VRChat avatars. It ships as a Unity Editor package, `Packages/com.alrauna.amuse`, hosted by NDMF. During an avatar build it classifies mesh triangles as proven-opaque, must-stay-transparent, or unknown. Proven-opaque triangles may move to AMUSE-generated canonical opaque materials. Unproven triangles always keep their original material.

A second package, `Packages/com.alrauna.amuse.research`, holds read-only research tooling (Census collection and calibration). It must never ship in the product or the VPM listing.

Public fixtures must be minimal, deterministic, redistributable, and legally publishable. Private avatars, vendor assets, consent and identity data, and raw Census observations may not be public.

The correctness policy, the optimization policy, and the session rules live in the policy sections at the end of this file.

## Architecture & Data Flow

One NDMF plugin, `AmusePlatformFinishPlugin` (`Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`), registers three PlatformFinish passes:

1. `AmuseAnimatorBindingsCapture` retains `IPlatformAnimatorBindings` from the active `AnimatorServicesContext` into `BuildContext` state before the extension deactivates.
2. `AmusePlatformFinishPass` runs extension-free, so NDMF commits the animator graph first. It gates on `HostLifecycleCapability.Evaluate` (versions, platform, build path, build services). Per renderer it captures closed animation and material evidence, resolves per-slot runtime states, classifies triangle alpha exactly, plans mesh separation, and retains `PreparedAlphaSeparation`.
3. `AlphaSeparationApply` revalidates every candidate against live state, finalizes, sweeps unreferenced AMUSE-owned clones, and performs the single mutation: animation curves, then `sharedMesh`, then `sharedMaterials`.

Module layering, dependency direction:

- `Build/` owns the NDMF lifecycle and mutation. It calls every other module.
- `Host/` is the only module that touches live Unity objects. It captures immutable evidence: renderer snapshots, animation closure, and GPU alpha readback through an R8_UNorm blit (`UnityAlphaFieldEvidence.cs`).
- `Analysis/` is the pure proof core: exact rational-interval triangle classification (`TriangleAlphaClassifier.cs`), UV envelopes (`ExactUvGeometry.cs`, `AffineUvTransform.cs`), runtime-state resolution, and mesh planning. It never mutates Unity state.
- `Semantics/` holds the shader frontends. `Poiyomi/` and `LilToon/` attest shader identity first (pinned GUIDs, package versions, SHA-256 canonical digests), then answer alpha semantics. `MaterialSemantics.cs` defines the shader-independent value algebra.

Data flow: live avatar, then immutable captured evidence, then exact classification, then plan, then revalidation, then minimal apply. Source assets are never mutated. Only the NDMF build copy and AMUSE-generated transients change.

## Key Directories

| Path | Purpose |
| --- | --- |
| `Packages/com.alrauna.amuse/Editor/` | All production code. One editor-only assembly. |
| `Packages/com.alrauna.amuse/Editor/Build/` | NDMF plugin, passes, lifecycle gate, prepare and apply |
| `Packages/com.alrauna.amuse/Editor/Host/` | Unity evidence capture. `Host/Shaders/` holds the alpha predicate shader. |
| `Packages/com.alrauna.amuse/Editor/Analysis/` | Pure classification and planning |
| `Packages/com.alrauna.amuse/Editor/Semantics/` | Value algebra plus `Poiyomi/` and `LilToon/` frontends |
| `Packages/com.alrauna.amuse/Tests/Editor/` | Product EditMode tests. Folders mirror production. |
| `Packages/com.alrauna.amuse.research/` | Research package. Never released. |
| `docs/superpowers/specs/` | Dated design specs, `YYYY-MM-DD-<topic>-design.md` |
| `docs/superpowers/plans/` | Dated implementation plans with RED/GREEN tasks |
| `docs/superpowers/investigations/` | Dated read-only characterization notes |
| `Tools/` | `Bootstrap-NdmfStandalone.ps1` |

## Development Commands

First-time setup, from `README.md`:

```bash
dotnet tool install --global vrchat.vpm.cli
vpm list repos                                    # check NDMF repo registration
vpm add repo https://vpm.nadena.dev/vpm.json      # only when missing
vpm resolve project .                             # restores Packages/nadena.dev.ndmf
pwsh -NoProfile -File ./Tools/Bootstrap-NdmfStandalone.ps1
# then open the project in Unity 2022.3.22f1
```

`vpm resolve` can exit 0 without resolving. Confirm `Packages/nadena.dev.ndmf/package.json` exists at version 1.14.4 afterward.

- Tests run through the Unity Test Runner, EditMode mode. No CLI test script and no CI test job exists. In-repo practice: refresh Unity after new files, run a focused EditMode filter, then the full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` assemblies.
- Release: `.github/workflows/release.yml` (manual dispatch) zips one package, hard-excludes `Tests/`, and fails the build if test content re-enters.
- No lint or formatter is configured. Use `git diff --check` for whitespace.

## Code Conventions & Common Patterns

- C# against Unity 2022.3 APIs. Editor-only. One production assembly, `Alrauna.Amuse.Editor`.
- File name equals type name. One public type per file. Production types are `internal` where possible. `Editor/AssemblyInfo.cs` grants `InternalsVisibleTo` to the test and research assemblies.
- Namespaces mirror folders: `Alrauna.Amuse.Editor.Build`, `.Host`, `.Analysis`, `.Semantics`, `.Semantics.LilToon`, `.Semantics.Poiyomi`.
- Closed refusal enums per scope, for example `HostLifecycleRefusal`, `RendererAnalysisRefusal`, `AlphaSeparationSlotRefusal`. Unsupported means a named refusal value. Programming defects throw and block the build.
- Fail closed. Parsers reject unknown input. Classification never exits early on Unknown. `MustRemainTransparent` is absorbing.
- Evidence discipline. Capture immutable records such as `CapturedAnimationEvidence` close to their consumer. Immutable evidence never holds live Unity references. Revalidate live state immediately before mutation.
- Attestation is pinned data. Shader GUIDs, exact names, package versions, and measured SHA-256 digests live in `LilToonSourceAttestation.cs` and `PoiyomiMaterialSemantics.cs`. Never re-derive digests from vendor repositories. Unattested materials answer all-Unknown.
- Test seams are production delegates, for example `VerifiedPoiyomiConversion`. Tests substitute only vendor attestation and run real production logic.
- XML doc comments on load-bearing rules. State why, not what.

## Important Files

- `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` — NDMF entry point and the three passes
- `Packages/com.alrauna.amuse/Editor/Build/HostLifecycleCapability.cs` — version, platform, build-path, and services gate
- `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs` and `AlphaSeparationApply.cs` — prepare, then the single mutation
- `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs` — exact per-triangle alpha proof
- `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs` — frontend selection by exact shader name
- `Packages/com.alrauna.amuse/package.json` — package identity and the NDMF dependency range
- `Packages/manifest.json` and `Packages/vpm-manifest.json` — Unity and VPM dependencies
- `Packages/.gitignore` — the whitelist that decides which embedded packages are tracked
- `Tools/Bootstrap-NdmfStandalone.ps1` — materializes NDMF `Dependencies~` into `Dependencies/`, pinned to NDMF 1.14.4
- `.omp/RULES.md` — sticky session rules: no absolute paths, no private identifiers, ASD-STE100 style

## Runtime/Tooling Preferences

- Dev environment: Unity 2022.3.22f1 (`ProjectSettings/ProjectVersion.txt`). The package declares `unity: "2022.3"`.
- The build gate admits Unity 2022.3 patch 22 and newer f-releases, NDMF `[1.14.4, 2.0.0)`, VRChat SDK Base and Avatars `[3.10.4, 4.0.0)`, and non-Play NDMF builds only.
- Compilation happens inside Unity. There is no dotnet build, no Node, no Bun. PowerShell 7 runs the one bootstrap script.
- Generated `*.csproj` and `*.sln` files are gitignored. Never commit or hand-edit them.
- `Packages/nadena.dev.ndmf/` is intentionally untracked. A fresh clone restores it with `vpm resolve project .`. A new embedded package needs a whitelist line in `Packages/.gitignore`.

## Testing & QA

- NUnit through Unity Test Framework, EditMode only. Two assemblies: `Alrauna.Amuse.Tests.Editor` (product) and `Alrauna.Amuse.Research.Tests.Editor` (research).
- One test class per production type, named `<Type>Tests`. Test folders mirror production folders. Method names are behavior sentences, for example `MissingNdmfPackageRefusesWithNdmfReason`.
- Assertions use the NUnit constraint model: `Assert.That(actual, Is.EqualTo(expected))`.
- Vendor shaders are never installed in this repository. Tests run on schema-only stand-in shaders under `Hidden/Alrauna/AmuseTests/*` through the verified seams in `Tests/Editor/Build/`.
- `Tests/Editor/ReferenceFixtures/` holds public synthetic JSON-driven texture and mesh fixtures with independent outcome oracles: `ProvenOpaque`, `MustRemainTransparent`, `Unknown`. Determinism is tested, not assumed.
- RED/GREEN: a behavior change needs a failing test observed first, for a named plausible wrong implementation, then a minimal fix. Never weaken a valid test. An assertion that passes on first run is recorded as characterization, never dressed up as RED.
- Falsifiers are numbered adversarial cases, marked `--- Falsifier N: ... ---`, that a plausible wrong implementation must fail. They carry no-op guards so later changes cannot silence them.
- A filtered run that reports 0 tests is a failure. A successful compile is never validation. Record observed counts and Console output.

## Repository reality and scope

Current code, tests, pinned source behavior, reproducible characterization, and real-avatar evidence have priority over old plans, summaries, or architectural intent.

Keep current work separate from discovered prerequisites, future architectural pressure, and speculative opportunities. Do not silently expand the active task.

Prefer the smallest complete solution for a real consumer. Do not introduce broad infrastructure or APIs without a present requirement. This includes universal shader/compiler infrastructure, broad semantic IRs, generalized mutation frameworks, global planners, cross-host abstractions, and third-party extension APIs.

Keep shader-specific behavior shader-specific unless materially different consumers justify a shared abstraction. Poiyomi and lilToon should apply pressure to shared seams independently.

## Correctness and uncertainty

Preserve the intended visual appearance, avatar-controlled functional behavior, supported shader/material behavior, and compatibility assumptions that the active optimization policy declares.

Policy-authorized representation changes are valid. AMUSE does not require framebuffer identity under every hypothetical interaction.

A false positive violates the declared transformation contract or its preconditions. False negatives are safer, but unnecessary refusal remains a coverage defect.

Unknown information should invalidate only conclusions that depend on it. Do not expand a local unsupported fact into renderer-wide or avatar-wide refusal unless the dependency requires that scope. Increased uncertainty must never make a transformation more aggressive.

Programming and invariant failures remain defects. Never use a blanket catch to mark them as "unsupported."

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

Private root: a separate Unity project outside this repository (the Census Lab project).

Authoritative scene corpus: the `!CENSUSLAB/Scenes/` folder of the Census Lab project.

Private launcher location: `!CENSUSLAB/Scripts/Editor/` in the Census Lab project.

Census Lab is for characterization and validation, not the correctness oracle. Prefer read-only use. Reduce failures to public synthetic fixtures where practical. Tests create and delete folders under `Assets/`, so they must never run in the private Lab project.

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
