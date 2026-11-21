# Atlasing Milestone 3: lilToon Main Color Path Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

Privacy note: This record contains public product planning only. It contains no private asset observations, machine identifiers, or vendor source excerpts.

Date: 2026-11-20.
Status on 2026-11-20: Executable plan for Milestone 3 of `docs/superpowers/plans/2026-11-19-0-2-0-minimal-atlasing-plan.md`. Milestone 2 merged into main on 2026-11-20. The owner asked on 2026-11-20 to go ahead with Milestone 3. The execution is inline.
Status on 2026-11-21: Implemented on branch `atlasing-m3-liltoon-main-color`. RED was observed on 2026-11-20: 65 filtered tests ran, and every lilToon admission and value row refused as `UnattestedShader`. A second RED, with the exact-row term removed on purpose, failed `ExactRowRefusesAnApprovedNonRowVersion`. GREEN: the atlas classes and `LilToonAttestationTests` ran 460 tests with 460 passed. On 2026-11-21 the product and research EditMode assemblies ran with no filter in the dev editor instance: 3687 total, 3662 passed, 0 failed, and 25 skipped. The skips are the vendor opt-in gates.

**Goal:** The atlas pass admits the `_MainTex` route of exactly attested lilToon rows with the same pipeline as the Poiyomi row. Every other lilToon use keeps its bindings and gets one named refusal.

**Architecture:** Only the capture changes. `AtlasUseCapture` selects the route table from the attested shader row. Grouping, placement, budget, pages, meshes, and the write are shader-independent and do not change.

**Tech Stack:** C#, Unity 2022.3.22f1, NDMF, NUnit EditMode tests, the installed lilToon 2.4.1 package.

**Spec:** `docs/superpowers/specs/2026-11-19-minimal-atlasing-design.md`, sections 4, 5, and 14. Route facts: `docs/superpowers/investigations/2026-11-19-atlasing-route-tables.md`, section 5.

## Global Constraints

- Package root for every path below: `Packages/com.alrauna.amuse/`.
- Rows: `lilToon`, `Hidden/lilToonCutout`, `Hidden/lilToonTransparent`, `Hidden/lilToonOutline`, `Hidden/lilToonCutoutOutline`, `Hidden/lilToonTransparentOutline`, `Hidden/lilToonOnePassTransparent`, `Hidden/lilToonTwoPassTransparent`, `Hidden/lilToonOnePassTransparentOutline`, `Hidden/lilToonTwoPassTransparentOutline`.
- Member: `_MainTex` on UV0. Neutral: none. Allowed keywords: none.
- Required values: see decision M3.
- Version consent does not extend an atlas row (design section 3).
- Every report string and comment uses simple English.
- Before any Unity MCP call, confirm that `Application.dataPath` belongs to this project.

## Plan Decisions

- **M1. Exact rows.** The capture gathers lilToon evidence through `LilToonSourceAttestation.TryGatherSourceEvidenceForShaderName` and verifies it through `TryVerifyIdentityForShaderName`. That conjunction also admits an approved version that is not a row. So a new `LilToonSourceAttestation.TryVerifyExactRowIdentity(LilToonSourceEvidence)` adds one term: a packaged install must carry an admitted row version. A loose install already needs an admitted include tree.
- **M2. Multi.** A material on a Multi container shader that resolves and verifies through `TryResolveMultiContainer` and `TryVerifyMultiContainer` refuses with `NoRouteTable`. A Multi name that does not verify refuses with `UnattestedShader`.
- **M3. Required values.** Flags compare exactly, so a NaN flag refuses.
  - `_ShiftBackfaceUV`, `_UseParallax`, `_UseMain2ndTex`, `_UseMain3rdTex`, `_UseGlitter`, and `_UseAudioLink` are 0.
  - The first component of `_DissolveParams` lies strictly between -0.5 and 0.5. The shader rounds the value. The rounding of an exact half differs between platforms, so both halves refuse.
  - Every `_UDIMDiscardRow<r>_<c>` flag is 0, or `_UDIMDiscardUV` is a number other than 0. The shader selects UV0 with an exact compare.
  - Every `_IDMask1` to `_IDMask8` and `_IDMaskPrior1` to `_IDMaskPrior8` flag is 0, or `_IDMaskFrom` is at least 1. The shader reads `_IDMaskFrom` as an unsigned integer. A value between 0 and 1 can convert to 0 and read UV0, so this rule is stricter than the record of 2026-11-19.
  - `_MainTex_ST` is exactly (1, 1, 0, 0) and `_MainTex_ScrollRotate` is exactly zero. They refuse with `NonIdentityTransform`.
- **M4. Route name.** `AtlasGroupKey.MainColorRouteName` becomes `MainColor`. The key already holds the shader row, so the route name no longer names a family.

## Review Focus

1. A lilToon shader outside the ten rows, such as fur, must refuse. Task 1 owns `LilToonFurShaderRefuses`.
2. A fractional `_IDMaskFrom` must refuse. Task 1 owns `LilToonFractionalIdMaskSourceRefuses`.
3. An exact half on the dissolve mode must refuse. Task 1 owns the `-0.5` and `0.5` rows of `LilToonDissolveModeRefuses`.
4. An approved lilToon version that is not a row must refuse. Task 1 owns `ExactRowRefusesAnApprovedNonRowVersion`.
5. The lilToon page material must keep the source keyword set. The Task 2 end-to-end test runs the existing keyword invariant in `AtlasPass`.

---

### Task 1: lilToon route table and capture

**Files:**
- Modify: `Editor/Semantics/AtlasRouteTable.cs` (`LilToonMainColor`)
- Modify: `Editor/Semantics/LilToon/LilToonSourceAttestation.cs` (`TryVerifyExactRowIdentity`)
- Modify: `Editor/Host/AtlasUseCapture.cs` (row selection)
- Modify: `Editor/Analysis/AtlasGroupKey.cs` (M4)
- Test: `Tests/Editor/Host/AtlasUseCaptureTests.cs`, `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`

- [ ] **Step 1: Write the failing tests**
  - `FreshLilToonMaterialIsAdmittedOnEveryRow`: one row per shader GUID constant. The record exists and `ShaderRowName` equals the shader name.
  - `LilToonRequiredValueNotHeldRefuses`: one row per M3 flag set to 1. `RequiredValueNotHeld`.
  - `LilToonDissolveModeRefuses` with 1, 0.5, -0.5, and NaN. `LilToonDissolveModeBelowHalfIsAdmitted` with 0.49.
  - `LilToonUdimDiscardFlagOnUv0Refuses`: one row per row flag. `LilToonUdimDiscardOnUv1IsAdmitted`.
  - `LilToonIdMaskFlagFromUv0Refuses`: one row per flag. `LilToonFractionalIdMaskSourceRefuses` with 0.5. `LilToonIdMaskFromUv1IsAdmitted`.
  - `LilToonNonIdentityScaleRefuses` and `LilToonScrollRotateRefuses`: `NonIdentityTransform`.
  - `LilToonUnlistedTextureRefuses`: `_EmissionMap` assigned. `UnlistedTexture`.
  - `LilToonAnimatedConditionPropertyRefuses`: `material._UseGlitter`. `AnimatedRouteProperty`.
  - `LilToonPropertyBlockOnConditionRefuses`: `_ShiftBackfaceUV`. `PropertyBlockOnRouteProperty`.
  - `LilToonMultiRefusesWithoutARouteTable`: `NoRouteTable`.
  - `LilToonFurShaderRefuses`: `UnattestedShader`.
  - `ExactRowRefusesAnApprovedNonRowVersion` (2.4.3), `ExactRowAdmitsThePinnedVersion`, `ExactRowAdmitsALooseInstallWithAnAdmittedTree`.
- [ ] **Step 2: Run the filters `AtlasUseCaptureTests` and `LilToonAttestationTests`. Expect compile errors.**
- [ ] **Step 3: Implement M1 to M4.**
- [ ] **Step 4: Run the same filters. Every test passes.**
- [ ] **Step 5: `git diff --check`. Commit.**

### Task 2: lilToon end-to-end fixture

**Files:**
- Test: `Tests/Editor/Build/AtlasEndToEndTests.cs`

- [ ] **Step 1: Write `TwoLilToonMaterialsShareOnePageWithCorrectRendering`.** It copies the Poiyomi fixture on the GUID-loaded `lilToon` shader. One page, two admitted uses, one shared page material, unchanged sources, Float32 UV0, and a render difference of at most 1 of 255 per channel.
- [ ] **Step 2: Observe the test.** Before Task 1, the capture refuses every lilToon use as `UnattestedShader`, so the page count is 0. Run it on the Task 1 code and record the result.
- [ ] **Step 3: Run both full assemblies. Record the counts. Update the master plan and `CHANGELOG.md`. Commit.**
