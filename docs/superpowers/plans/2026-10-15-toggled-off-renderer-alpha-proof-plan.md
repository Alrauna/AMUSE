# Toggled-Off Renderer Alpha Proof Characterization and Regression Defense Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to execute this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide permanent regression protection for disabled renderers. AMUSE must analyze, prove, and separate renderers that animation curves enable.

**Architecture:** AMUSE enumerates all renderers with `GetComponentsInChildren<Renderer>(true)`. It ignores `m_Enabled` float curves during material proof. When AMUSE optimizes a disabled renderer, it replaces the material or appends an opaque submesh in place. Existing `m_Enabled` float curves remain on the component and toggle original and new submeshes together.

**Tech Stack:** Unity 2022.3.22f1, C#, NDMF, NUnit via Unity Test Framework, EditMode only.

**Spec:** `docs/superpowers/specs/2026-10-15-toggled-off-renderer-alpha-proof-design.md`

**Investigation:** `docs/superpowers/investigations/2026-10-15-toggled-off-renderer-alpha-proof-investigation.md`

## Privacy Note

This record characterizes test infrastructure in the dev editor instance.
This document sanitizes all avatar, renderer, material, texture, animation, and hierarchy names.
It describes assets by role.
It expresses exact counts as ranges.

## Working discipline

- Branch: `fix/toggled-off-renderer-alpha-proof`. Base: `main`.
- Allowed mutations, exhaustive:
  `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`
  `docs/superpowers/plans/2026-10-15-toggled-off-renderer-alpha-proof-plan.md`
- Never touch: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, generated `.csproj`/`.sln`, `Packages/nadena.dev.ndmf/`, `Packages/com.alrauna.amuse.research/`, and any file not listed above.
- Every finalized task ends with exactly one commit of the paths the task names. Do not stage unrelated files.
- Stop and return evidence if code differs from this plan. Stop if a test fails for a reason the task does not name. Stop if compilation fails.
- Unity validation: request an asset refresh through the Unity MCP refresh tool. Wait for readiness before running tests. Run tests through the Unity MCP test runner. A filtered run that reports zero tests is a failure. Record observed pass and fail counts in the task report.

## Global constraints

- Simple English (ASD-STE100): short active sentences, one idea per sentence, no semicolons, no contractions, no em-dashes.
- No private asset identifiers, machine paths, user names, ports, or hex hashes.
- Dates must be explicit (2026-10-15). Do not write unqualified 'today' or 'currently'.

---

## Task 1: Add regression test for toggled-off renderer in `AlphaSeparationApplyTests.cs`

- [ ] 1.1 In `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`, add the test method `DisabledRendererWithToggledOnAnimationIsAdmittedAndOptimized`:
  - Create the avatar root with `GameObject` and add `Alrauna.Amuse.Runtime.AmuseAvatarOptimizer`.
  - Configure proof bounds with `FixtureProofScope.PinAllSizes(root)`.
  - Create a single-triangle mesh with `AlphaSeparationTestLifecycle.SingleTriangleMesh()`.
  - Create a verified material with `PoiyomiFixtureTestBase.CreateVerifiedOpaqueMaterial()`.
  - Add a child `SkinnedMeshRenderer` with `AlphaSeparationTestLifecycle.AddRendererPadded(root, "body", mesh, material)`.
  - Explicitly set `renderer.enabled = false`.
  - Create an in-memory `AnimationClip` that animates `SkinnedMeshRenderer.m_Enabled` from 0 to 1 on `"body"` using `AnimationCurve.Linear(0f, 0f, 1f, 1f)`.
  - Attach the controller with `AlphaSeparationTestLifecycle.NewController(root, "AMUSE toggled renderer graph", clip)`.
  - Run the build with `AvatarProcessor.ProcessAvatar(root, ApplyTestPlatform.Instance)`.
  - Assert that `state.AnalyzedRendererCount` equals 1.
  - Assert that `state.AppliedRendererCount` equals 1.
  - Assert that `state.AppliedOpaqueTriangleCount` equals 1.
  - Assert that `renderer.sharedMaterials[0]` equals the converted opaque clone.
  - Assert that the committed runtime clip preserves the `m_Enabled` float curve with two keyframes.
  - Assert that the authored in-memory clip retains its original curves.
  - Assert that no unexpected feature refusals fired with `AssertNoFeatureRefusals(state)`.
  - Include the test comment: `// Falsifier: An erroneous disabled renderer filter in AmusePlatformFinishPlugin causes AppliedRendererCount == 0.`
  - Clean up generated assets, controller clones, and tracked objects in a finally block.
- [ ] 1.2 Request a Unity asset database refresh through `refresh_unity`.
- [ ] 1.3 Run EditMode tests in `Alrauna.Amuse.Tests.Editor` filtered to `AlphaSeparationApplyTests.DisabledRendererWithToggledOnAnimationIsAdmittedAndOptimized` through `run_tests`.
- [ ] 1.4 Observe that the test passes and record the count.
- [ ] 1.5 Commit: `git add Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs docs/superpowers/plans/2026-10-15-toggled-off-renderer-alpha-proof-plan.md && git commit -m "test(build): assert disabled renderers toggled by animation optimize cleanly"`
