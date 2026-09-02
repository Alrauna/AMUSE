# lilToon Regular Transparent Normal → Canonical Opaque Separation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `Hidden/lilToonTransparent` (regular Transparent **Normal**, lilToon 2.3.4) as the
third alpha-separation source family, converting triangles proven visually opaque onto the
existing AMUSE-generated canonical `lilToon` opaque material.

**Architecture:** One new pinned identity inside the existing lilToon frontend. The current
`LilToonOpaqueConversion` is first split by ownership into a target module, a shared
result/constant support file, and one source-eligibility module per source family; the
transparent family then adds a fourth production module plus an alpha-semantics module. No
interface, registry, IR, planner, or mode-parameterized engine. The mesh, planning, apply,
dedup, and curve-rewrite layers are untouched.

**Tech Stack:** Unity 2022.3.22f1 EditMode tests (NUnit), C# 9, NDMF 1.14.8, no new dependency.
lilToon 2.3.4 is **not** installed in this project and must not be; every test runs against
public deterministic synthetic stand-in shaders.

**Spec:** `docs/superpowers/specs/2026-09-01-liltoon-transparent-normal-alpha-separation-design.md`
(cited as **§n**). Its investigation is
`docs/superpowers/investigations/2026-09-01-liltoon-transparent-normal-alpha-separation.md`
(cited as **T1 §n**). Read both before Task 1.

---

## Global Constraints

Every task's requirements implicitly include this section.

**Authorization**

- Implementation requires a **separate, explicitly authorized branch**. This plan does not
  authorize creating it. Do not begin Task 1 until the controller names the branch.
- Base: `a3c547b6064b20709289a1062c11b7fd72818568` (merge commit of PR #42).
- **Not authorized by this plan:** staging, committing, amending, pushing, opening or merging a
  PR, rebasing, stashing, discarding changes, deleting branches, rewriting history, changing
  remotes or repository settings, publishing. Each task ends with a commit step; execute it
  **only** if the controller has separately granted commit authorization. Otherwise stop at the
  task's verification step and report.
- `Packages/manifest.json` and `Packages/packages-lock.json` carry pre-existing user-owned Unity
  toolchain/sysroot modifications. **Never** touch, stage, or include them.
- Never commit Unity-generated state: `Library/`, `Temp/`, `Logs/`, `UserSettings/`.
- Census Lab and private avatar data: not used, not inspected, not modified. No private Unity
  instance name, path, hash, or port may appear in any file or report.
- Do not install lilToon into this project. Do not vendor lilToon source.

**Pinned constants — copy verbatim, never re-derive**

| Constant | Value |
|---|---|
| Package | `jp.lilxyzw.liltoon` `2.3.4`, `_lilToonVersion == 45` |
| `TransparentShaderName` | `Hidden/lilToonTransparent` |
| `TransparentShaderGuid` | `165365ab7100a044ca85fc8c33548a62` |
| `TransparentPassShaderName` | `Hidden/ltspass_transparent` |
| `TransparentPassShaderGuid` | `2683fad669f20ec49b8e9656954a33a8` |
| `TransparentRenderMode` | `2` |
| `TransparentShaderCanonicalDigest` | `ea247d3cd6ecb09ad4aeefdcad37480c0dffa40d594a3b457624097f2372ba13` |
| `TransparentPassCanonicalDigest` | `700a607661f2cc43550452795d8eae0634509dbd07b4e8c381d9412fcc52517f` |
| `IncludeTreeDigest` | unchanged, already pinned |
| Source queue / `RenderType` | `2460` / `TransparentCutout` |
| Target | `lilToon`, GUID `df12117ecd77c31469c224178886498e`, queue `2000`, `RenderType=Opaque` |

**Gate bounds (§2, §9)**

`_Cutoff <= 1` (transparent; **not** cutout's `0.9999`) · `_AlphaBoostFA >= 1` ·
`_DistanceFade.z == 0` · finite `_SubpassCutoff <= 1` · `_MainTex_ST == (1,1,0,0)` exact per
binary32 component · `_MainTex_ScrollRotate == (0,0,0,0)` exact.

**Clean cutover (§13)**

The **type** `LilToonOpaqueConversion` and both **old asset paths** disappear. No alias, no
`[Obsolete]`, no re-export, no partial-class bridge. Every call site migrates in the same change.
The cutout gate list, cutout request, and cutout bound are never parameterized, widened, or
shared. The opaque lilToon and Poiyomi paths stay byte-for-byte unaffected.

Clean cutover is a *type and namespace* requirement, not an instruction to churn Unity asset
identity. Two assets are **renamed** rather than deleted so their GUIDs survive; see File
Structure.

**Stop conditions (§18)**

Stop, preserve evidence, and return to the controller — do not work around — if:

1. Re-derived digests disagree with the two pinned constants.
2. A `_DitherMaskLOD` slice-15 alpha below `1` is observed on any supported target path.
3. Any of gates 1–11 needs a different rule for transparent than for cutout.
4. The clone path needs any change for a transparent source.
5. The §11 split cannot be done without an interface, a registry, or a mode parameter.
6. A proof-relevant fact turns out not to be expressible by the existing capture and
   exact-singleton admission.
7. Any §14 falsifier cannot be written as a deterministic public synthetic test.

**Unity run protocol — REQUIRED before and during every Unity operation**

Every step below that says *Unity run protocol* means exactly this sequence. Never shorten it,
and never substitute a shell-invoked Unity command: the MCP call supplies the operation.

*Instance identity, before any Unity call in a session and again after any reconnect:*

1. Enumerate reachable Unity instances **read-only**.
2. Read `Application.dataPath` from the candidate instance.
3. Normalize the path (resolve symlinks and `/private` prefixes, strip trailing separators).
4. Require an **exact, case-sensitive** match to `<repo-root>/Assets`. A case-only match is not
   identity.
5. If more than one instance is reachable, **pin** only that public AMUSE instance for the
   session.
6. **Stop** if identity is ambiguous, or if no instance matches exactly. Do not guess, and do not
   fall back to a single reachable instance without the exact match.
7. Never target, name, or report another instance's name, path, hash, or port — not in a test,
   not in a comment, not in the Task 7 report.

*After any script change:*

8. Refresh Unity's asset database and request compilation.
9. Wait for compilation and the domain reload to finish before any further call.
10. Read the Console.

*For every EditMode run:*

11. Start the run with `run_tests`, EditMode, with the exact class filter the step names.
12. Poll the returned job with `get_test_job` until it reports completion. A started run is not a
    result.
13. Read the Console again.
14. Record, for that step: the exact class filter, the passed count, the failed count, the
    skipped count, and every Console error and warning — or their explicit absence.

Task 7 alone runs the full product and research EditMode suites. Tasks 1–6 run only their named
class filters.

**Per-task discipline**

- Do not run project-wide formatters or linters. Task 7 owns the full-suite runs.
- Every Unity operation follows the Unity run protocol above, including the Console read.
- A successful compile is never validation. Never claim a run passed unless it was observed.

---

## File Structure

**Renamed (identity preserved — non-index filesystem move, `.meta` moved with the asset)**

| From → To | GUID that must survive |
|---|---|
| `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueConversion.cs` → `…/LilToon/LilToonOpaqueTarget.cs` | `b60b82aa490a24e929afd693ed059d96` |
| `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueConversionTests.cs` → `…/LilToon/LilToonOpaqueTargetTests.cs` | `86eedea91214449ecb959f6d50ab64b0` |

Move both assets with a mechanism that does **not** touch the Git index: the harness Edit `MV`
operation, or an equivalent plain filesystem move. `git mv` is forbidden here because it stages,
and staging is outside this plan's authorization. Without an index update Git reports the old
tracked path as deleted and the new path as untracked; that status shape is expected and
acceptable. Identity is therefore validated by path absence, path presence, file content, and
`.meta` hash — never by Git rename detection.

Each renamed asset keeps its `.meta` file **byte-for-byte identical**. A pure move changes no
`.meta` content, so both of these SHA-256 hashes must survive unchanged:

| `.meta` after the move | SHA-256 that must be unchanged |
|---|---|
| `…/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs.meta` | `eadc3b7413f2143bfa3e3bceb3dd3752dc9727ceb5bfc017170292fa5550aa6f` |
| `…/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs.meta` | `21830037034d0e27683718b101e8bba7c97d751b494f3d91b10a6fb92f1153b0` |

If either hash changes, **stop** and inspect the complete `.meta` diff. Do not accept, hide, or
re-serialize incidental metadata churn.

The target module and the target test suite are the largest remaining fragments of each original
file, so renaming them preserves identity for the majority of the content and confines new GUIDs
to genuinely new assets.

**Created (production)**

| Path | Responsibility |
|---|---|
| `…/LilToon/LilToonOpaqueConversionResult.cs` (new `.meta`) | Shared eligibility support: the outcome/refusal enums, the eligibility struct, the two exact blend predicates, the Unity enum constants. Constants and result types only — never a gate table, dispatch, mode parameter, or shared `Evaluate*` body. |
| `…/LilToon/LilToonCutoutSourceEligibility.cs` (new `.meta`) | Cutout source facts: queue 2450, `RenderType`, `MaxProvableCutoff` `0.9999`, `_Cutoff` request, gates 1–12. |
| `…/LilToon/LilToonTransparentSourceEligibility.cs` (new `.meta`) | Transparent source facts: queue 2460, `RenderType`, `MaxProvableCutoff` `1f`, the four-property request, gates 1–15. |
| `…/LilToon/LilToonTransparentMaterialSemantics.cs` (new `.meta`) | Transparent alpha request and interpretation. |

`LilToonOpaqueTarget.cs` is **not** in this table: it arrives by rename, carrying
`b60b82aa490a24e929afd693ed059d96`.

**Created (test)**

| Path | Responsibility |
|---|---|
| `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueConversionResultTests.cs` (new `.meta`) | Outcome and refusal vocabulary. |
| `…/LilToon/LilToonCutoutSourceEligibilityTests.cs` (new `.meta`) | Cutout bound, cutout gates. |
| `…/LilToon/LilToonTransparentAlphaTests.cs` (new `.meta`) | §14 rows 1–14, 19–20 (alpha side). |
| `…/LilToon/LilToonTransparentSourceEligibilityTests.cs` (new `.meta`) | §14 rows 15–17. |
| `…/LilToon/LilToonTransparentConversionTest.shader` (new `.meta`) | Transparent stand-in: queue 2460 and the transparent property schema. |

`LilToonOpaqueTargetTests.cs` is **not** in this table: it arrives by rename, carrying
`86eedea91214449ecb959f6d50ab64b0`.

**Deleted**

None. Both original assets are renamed, not deleted. The obsolete *type* and both obsolete
*paths* still disappear, which is what §13's clean cutover requires.

**Modified**

`Editor/Semantics/LilToon/LilToonSourceAttestation.cs` ·
`Editor/Semantics/UnityMaterialSemantics.cs` ·
`Editor/Build/AlphaSeparationPreparation.cs` ·
`Editor/Build/AmusePlatformFinishPlugin.cs` (doc comment only) ·
`Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs` ·
`Tests/Editor/Semantics/Characterization/NeutralClaimGatingTests.cs` ·
`Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs` ·
`Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs` ·
`Tests/Editor/Build/VerifiedLilToonTestSeams.cs` ·
`Tests/Editor/Build/AlphaSeparationApplyTests.cs` ·
`Tests/Editor/Build/AlphaSeparationPreparationTests.cs` ·
`Tests/Editor/Build/AlphaSeparationPersistenceTests.cs` ·
`docs/architecture/shader-frontend-comparison.md`

A Unity `.meta` file is part of its asset and moves with it in the same change; never delete or
regenerate the `.meta` of a renamed asset. `LilToonOpaqueConversionTest.shader` keeps its name
and GUID: it is a fixture identity, and renaming it would churn a GUID for no behavioral gain.

---

## Task 1: Split the conversion module by ownership

Behavior-preserving. Nothing about capture, eligibility, or the clone changes; only where the
code lives and which module owns which request. The existing suites are the regression oracle.

**Files:**
- Rename (non-index move, `.meta` moved with it, GUID `b60b82aa490a24e929afd693ed059d96` and
  `.meta` SHA-256 `eadc3b7413f2143bfa3e3bceb3dd3752dc9727ceb5bfc017170292fa5550aa6f` preserved):
  `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueConversion.cs` →
  `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueConversionResult.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:321-324`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:22,476,535,553,557,567,711,732`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:215`
- Rename (non-index move, `.meta` moved with it, GUID `86eedea91214449ecb959f6d50ab64b0` and
  `.meta` SHA-256 `21830037034d0e27683718b101e8bba7c97d751b494f3d91b10a6fb92f1153b0` preserved):
  `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueConversionTests.cs` →
  `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueConversionResultTests.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutSourceEligibilityTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/VerifiedLilToonTestSeams.cs:56,179,197,199,202,213,216`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs:2217,2225,2228,2230,2232,2275`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs:1579,2137,2142`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPersistenceTests.cs:451-452`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs:388-392`
- Delete: nothing.

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - `internal enum LilToonOpaqueConversionOutcome { Refused, Convertible }`
  - `internal enum LilToonOpaqueConversionRefusal { … }` — 14 members, unchanged names
  - `internal readonly struct LilToonOpaqueConversionEligibility` with
    `Outcome`, `Refusal`, `static Refused(LilToonOpaqueConversionRefusal)`, `static Convertible()`
  - `internal static bool LilToonOpaqueConversionFactors.IsUnitSourceFactorAtAlphaOne(float)`
  - `internal static bool LilToonOpaqueConversionFactors.IsZeroDestinationFactorAtAlphaOne(float)`
  - `internal const float LilToonOpaqueConversionFactors.{BlendOpAdd, BlendOpMax, BlendFactorZero, BlendFactorOne, BlendFactorSrcAlpha, BlendFactorOneMinusSrcAlpha, LEqualDepthComparison, ColorMaskAll, DepthWriteOn}`
  - `LilToonOpaqueTarget.CanonicalOpaqueProperties` → `IReadOnlyList<(string Property, float Value)>`, 18 entries
  - `LilToonOpaqueTarget.{CanonicalOpaqueRenderQueue, RenderTypeTagName, CanonicalOpaqueRenderType}`
  - `LilToonOpaqueTarget.RecipeSchemaProperties` → `IReadOnlyCollection<string>`, 18 entries
  - `LilToonOpaqueTarget.RecipeEvidenceRequest` → `MaterialEvidenceRequest`
  - `LilToonOpaqueTarget.ReadEffectiveRenderState(Material, out int, out string)`
  - `LilToonOpaqueTarget.TryFindNonCanonicalFact(Material, out string)`
  - `LilToonOpaqueTarget.PrepareCanonicalOpaqueClone(Material, Shader)` and `(Material, CapturedMaterialEvidence)`
  - `LilToonCutoutSourceEligibility.{SupportedCutoutRenderQueue, SupportedCutoutRenderType, MaxProvableCutoff}`
  - `LilToonCutoutSourceEligibility.SourceEvidenceRequest` → `MaterialEvidenceRequest`
  - `LilToonCutoutSourceEligibility.ConversionEvidenceRequest` → `MaterialEvidenceRequest`
  - `LilToonCutoutSourceEligibility.EligibilitySchemaProperties` → `IReadOnlyCollection<string>`, 19 entries
  - `LilToonCutoutSourceEligibility.EvaluateVerifiedEligibility(CapturedMaterialEvidence, int, string)`

**Implementation decision, recorded for review.** §8 says preparation's
`ConversionRequestForFamily` returns "a once-built `Combine(RecipeEvidenceRequest,
SourceEvidenceRequest)`", and `UnityMaterialSemantics`'s capture schema needs the same
*definition*. Capture and derived admission must resolve against one decision-specific request:
the properties captured for a family and the properties its conversion decision reads have to be
the same set, or a gate reads evidence nobody gathered. Building the combination twice would let
two `Combine` call sites drift apart silently. The combined object is therefore built **once, on
the source module** as `ConversionEvidenceRequest`, and both consumers read that one property.
This respects §11's ownership rules: the source module owns its own conversion request and reads
the target's public recipe request; nothing source-specific enters `LilToonOpaqueTarget`.

This is a single-definition rule, not an object-identity rule. Do not add a test that asserts
reference equality and nothing else — assert the request's *contents*, which is what the schema
tests below already do.

- [ ] **Step 1: Write the failing ownership tests**

Before touching anything, record the two current `.meta` hashes so the moves can be checked
against them:

```bash
sha256sum \
  Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueConversion.cs.meta \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueConversionTests.cs.meta
```

Expected, and already verified against the working tree:

| `.meta` | SHA-256 |
|---|---|
| `…/Editor/Semantics/LilToon/LilToonOpaqueConversion.cs.meta` | `eadc3b7413f2143bfa3e3bceb3dd3752dc9727ceb5bfc017170292fa5550aa6f` |
| `…/Tests/Editor/Semantics/LilToon/LilToonOpaqueConversionTests.cs.meta` | `21830037034d0e27683718b101e8bba7c97d751b494f3d91b10a6fb92f1153b0` |

Now move `LilToonOpaqueConversionTests.cs` to `LilToonOpaqueTargetTests.cs` with a **non-index**
move — the harness Edit `MV` operation or a plain filesystem move — and move
`LilToonOpaqueConversionTests.cs.meta` to `LilToonOpaqueTargetTests.cs.meta` the same way. Do
**not** use `git mv`: it stages, and staging is not authorized. Re-hash the moved `.meta` and
require `21830037034d0e27683718b101e8bba7c97d751b494f3d91b10a6fb92f1153b0` unchanged, and
confirm the file still carries GUID `86eedea91214449ecb959f6d50ab64b0`. If the hash differs,
**stop** and inspect the complete `.meta` diff.

Then leave the target-facing tests in
place; move the result-vocabulary and cutout-eligibility tests out into the two new test files
(mechanical). Then add these two genuinely new cases to `LilToonOpaqueTargetTests.cs` — the
recipe request must now be exactly 18 properties, and the `_Cutoff` property must have left the
target:

```csharp
        /// <summary>
        /// The target's request is the recipe and nothing else. Falsifies a
        /// split that moved the code but kept the "tuple + 1" schema, which
        /// would leave _Cutoff — a source-eligibility fact — inside the
        /// target's evidence contract.
        /// </summary>
        [Test]
        public void RecipeEvidenceRequest_IsExactlyTheEighteenRecipeProperties()
        {
            var request = LilToonOpaqueTarget.RecipeEvidenceRequest;

            Assert.That(request.ShaderName, Is.True);
            Assert.That(request.ActiveColorSpace, Is.False);
            CollectionAssert.AreEquivalent(
                ExpectedRecipeSchema, request.PresenceProperties);
            CollectionAssert.AreEquivalent(
                ExpectedRecipeSchema, request.ScalarProperties);
            Assert.That(request.ColorProperties, Is.Empty);
            Assert.That(request.VectorProperties, Is.Empty);
            Assert.That(request.TextureProperties, Is.Empty);
            CollectionAssert.DoesNotContain(
                request.ScalarProperties,
                "_Cutoff",
                "_Cutoff is source-eligibility evidence, not target evidence");
        }
```

with, in the same file, the independently stated schema (18 names, no `_Cutoff`):

```csharp
        private static readonly string[] ExpectedRecipeSchema =
        {
            "_SrcBlend", "_DstBlend", "_AlphaToMask", "_ZWrite", "_ZTest",
            "_OffsetFactor", "_OffsetUnits", "_ColorMask",
            "_SrcBlendAlpha", "_DstBlendAlpha", "_BlendOp", "_BlendOpAlpha",
            "_SrcBlendFA", "_DstBlendFA", "_SrcBlendAlphaFA",
            "_DstBlendAlphaFA", "_BlendOpFA", "_BlendOpAlphaFA",
        };
```

Create `Tests/Editor/Semantics/LilToon/LilToonCutoutSourceEligibilityTests.cs` and add:

```csharp
        /// <summary>
        /// The cutout source owns exactly one property. Falsifies a split
        /// that left _Cutoff on the target, and a split that widened the
        /// source request with recipe render state it reads but does not own.
        /// </summary>
        [Test]
        public void SourceEvidenceRequest_IsExactlyCutoff()
        {
            var request = LilToonCutoutSourceEligibility.SourceEvidenceRequest;

            CollectionAssert.AreEqual(
                new[] { "_Cutoff" }, request.PresenceProperties);
            CollectionAssert.AreEqual(
                new[] { "_Cutoff" }, request.ScalarProperties);
            Assert.That(request.ColorProperties, Is.Empty);
            Assert.That(request.VectorProperties, Is.Empty);
            Assert.That(request.TextureProperties, Is.Empty);
        }

        /// <summary>
        /// The combined object both the capture schema and the conversion
        /// boundary read is still the same 19 properties the single request
        /// carried before the split: the split must not change what is
        /// captured, only who owns each half.
        /// </summary>
        [Test]
        public void ConversionEvidenceRequest_IsTheRecipePlusCutoff()
        {
            var request =
                LilToonCutoutSourceEligibility.ConversionEvidenceRequest;

            Assert.That(request.ShaderName, Is.True);
            Assert.That(request.ScalarProperties.Count, Is.EqualTo(19));
            CollectionAssert.AreEquivalent(
                ExpectedConversionSchema, request.ScalarProperties);
            CollectionAssert.AreEquivalent(
                ExpectedConversionSchema, request.PresenceProperties);
        }
```

reusing the existing 19-name `ExpectedConversionSchema` array moved over from
`LilToonOpaqueConversionTests.cs:40-48`.

Create `Tests/Editor/Semantics/LilToon/LilToonOpaqueConversionResultTests.cs` holding the two
vocabulary tests moved out of the renamed target suite (they were
`LilToonOpaqueConversionTests.cs:146-165` before the rename — outcome names, refusal names) and
their `ExpectedRefusalNames` array unchanged at 14 members.

- [ ] **Step 2: Run the new tests to verify they fail**

Unity run protocol: refresh Unity → wait for compilation and domain reload → `run_tests`
EditMode → poll `get_test_job` to completion → read the Console → record the filter, passed,
failed, skipped, and every Console error or warning.
Class filter: `Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonOpaqueTargetTests`.
Expected: compile error `CS0103`/`CS0246` — `LilToonOpaqueTarget`,
`LilToonCutoutSourceEligibility`, and `LilToonOpaqueConversionResult` do not exist yet.

- [ ] **Step 3: Create the shared support file**

`Editor/Semantics/LilToon/LilToonOpaqueConversionResult.cs`. Move
`LilToonOpaqueConversionOutcome`, `LilToonOpaqueConversionRefusal`, and
`LilToonOpaqueConversionEligibility` verbatim from `LilToonOpaqueConversion.cs:22-106`,
including doc comments, with exactly two doc edits: in the `Refusal` doc, replace
"unreachable on the attested cutout shader" with "unreachable on the attested lilToon alpha
sources", and replace "`PrepareCanonicalOpaqueClone` throws for it" with
"`LilToonOpaqueTarget.PrepareCanonicalOpaqueClone` throws for it". Then add the constants and
predicates moved from `LilToonOpaqueConversion.cs:434-463`, promoted from `private` to
`internal` because they now cross a file boundary:

```csharp
    /// <summary>
    /// Exact render-state predicates and the Unity enum constants they read,
    /// shared by the two lilToon source-eligibility modules. This is a
    /// constants-and-predicates file and must stay one: a mode parameter, a
    /// gate table, a dispatch, or a shared Evaluate* body here is out of
    /// scope and returns to the controller (design §11).
    /// </summary>
    internal static class LilToonOpaqueConversionFactors
    {
        // Unity blend enums: Zero=0, One=1, SrcAlpha=5, OneMinusSrcAlpha=10;
        // UnityEngine.Rendering.BlendOp: Add=0, Max=4.
        internal const float BlendOpAdd = 0f;
        internal const float BlendOpMax = 4f;
        internal const float BlendFactorZero = 0f;
        internal const float BlendFactorOne = 1f;
        internal const float BlendFactorSrcAlpha = 5f;
        internal const float BlendFactorOneMinusSrcAlpha = 10f;

        // UnityEngine.Rendering.CompareFunction.LessEqual
        internal const float LEqualDepthComparison = 4f;

        // UnityEngine.Rendering.ColorWriteMask.All
        internal const float ColorMaskAll = 15f;

        // UnityEngine.Rendering.DepthWrite.On
        internal const float DepthWriteOn = 1f;

        /// <summary>One and SrcAlpha both evaluate to 1 at alpha 1.</summary>
        internal static bool IsUnitSourceFactorAtAlphaOne(float factor)
        {
            return factor == BlendFactorOne || factor == BlendFactorSrcAlpha;
        }

        /// <summary>Zero and OneMinusSrcAlpha both evaluate to 0 at alpha 1.</summary>
        internal static bool IsZeroDestinationFactorAtAlphaOne(float factor)
        {
            return factor == BlendFactorZero ||
                   factor == BlendFactorOneMinusSrcAlpha;
        }
    }
```

- [ ] **Step 4: Create the target module**

Move `Editor/Semantics/LilToon/LilToonOpaqueConversion.cs` to
`Editor/Semantics/LilToon/LilToonOpaqueTarget.cs` with a **non-index** move — the harness Edit
`MV` operation or a plain filesystem move — and move `LilToonOpaqueConversion.cs.meta` to
`LilToonOpaqueTarget.cs.meta` the same way. No `git mv`. Re-hash the moved `.meta` and require
`eadc3b7413f2143bfa3e3bceb3dd3752dc9727ceb5bfc017170292fa5550aa6f` unchanged, and confirm GUID
`b60b82aa490a24e929afd693ed059d96` is still present. If the hash differs, **stop** and inspect
the complete `.meta` diff.

That renamed asset — the same asset as the old `LilToonOpaqueConversion.cs` — is now edited
down to the target. Rename the type to `LilToonOpaqueTarget`. **Keep** (line numbers are the
pre-rename ones): the class doc `:108-135`, dropping the sentence about deciding whether a cutout
material may be normalized, because the module no longer decides eligibility;
`CanonicalOpaqueTuple` `:140-174`; `CanonicalOpaqueProperties` `:176-178`; the three canonical
constants `:180-182`; `ReadEffectiveRenderState` `:487-506`; `TryFindNonCanonicalFact`
`:508-547`; and both `PrepareCanonicalOpaqueClone` overloads `:551-700`. **Cut** (moving to the
two modules created in Steps 3 and 5): `SupportedCutoutRenderQueue`,
`SupportedCutoutRenderType`, `MaxProvableCutoff`, `CutoffProperty`, `ConversionSchema`,
`ConversionRequiredSchemaProperties`, `ConversionEvidenceRequest`, `EvaluateVerifiedEligibility`,
`Read`, `BuildConversionSchema`, the blend constants, and the two predicates. Replace the removed
`BuildConversionSchema` with the 18-property projection:

```csharp
        /// <summary>
        /// The recipe's own property names, projected from the tuple rather
        /// than retyped so the two cannot drift. Before the ownership split
        /// this was the tuple plus a nineteenth source-eligibility property;
        /// the schema is now an identity on the tuple, not a concatenation.
        /// </summary>
        private static readonly string[] RecipeSchema = BuildRecipeSchema();

        internal static IReadOnlyCollection<string>
            RecipeSchemaProperties { get; } =
                new ReadOnlyCollection<string>(RecipeSchema);

        /// <summary>
        /// The target's own request: the recipe, its presence, and nothing
        /// else. The recipe writes no colors, vectors, or textures, and no
        /// source-eligibility property belongs here.
        /// </summary>
        internal static MaterialEvidenceRequest RecipeEvidenceRequest { get; } =
            new MaterialEvidenceRequest(
                shaderName: true,
                activeColorSpace: false,
                presenceProperties: RecipeSchema,
                scalarProperties: RecipeSchema,
                colorProperties: Array.Empty<string>(),
                vectorProperties: Array.Empty<string>(),
                textureProperties: Array.Empty<TexturePropertyEvidenceRequest>());

        private static string[] BuildRecipeSchema()
        {
            var schema = new string[CanonicalOpaqueTuple.Length];
            for (var index = 0; index < CanonicalOpaqueTuple.Length; index++)
            {
                schema[index] = CanonicalOpaqueTuple[index].Property;
            }

            return schema;
        }
```

`RecipeSchema` must be declared textually **after** `CanonicalOpaqueTuple`: C# runs static field
initializers in declaration order.

- [ ] **Step 5: Create the cutout source-eligibility module**

`Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs`. Move `SupportedCutoutRenderQueue`
`:183`, `SupportedCutoutRenderType` `:184`, `MaxProvableCutoff` `:186-195`, `CutoffProperty`
`:199-206`, `EvaluateVerifiedEligibility` `:241-432`, and `Read` `:465-483` verbatim. Rename the
schema field and prefix the moved predicate and constant references with
`LilToonOpaqueConversionFactors.`. Add the two requests and the schema builder:

```csharp
        private static readonly string[] SourceSchema = { CutoffProperty };

        /// <summary>
        /// The cutout source's own eligibility evidence: one property. The
        /// recipe never writes it, and it is not target evidence.
        /// </summary>
        internal static MaterialEvidenceRequest SourceEvidenceRequest { get; } =
            new MaterialEvidenceRequest(
                shaderName: false,
                activeColorSpace: false,
                presenceProperties: SourceSchema,
                scalarProperties: SourceSchema,
                colorProperties: Array.Empty<string>(),
                vectorProperties: Array.Empty<string>(),
                textureProperties: Array.Empty<TexturePropertyEvidenceRequest>());

        /// <summary>
        /// The single object the capture schema and the conversion boundary
        /// both read, built once: the target's recipe request plus this
        /// family's source evidence. One canonical definition, so the
        /// properties captured for this family and the properties its
        /// conversion decision reads cannot drift apart, and no second
        /// Combine call site can disagree with this one.
        /// </summary>
        internal static MaterialEvidenceRequest
            ConversionEvidenceRequest { get; } =
                MaterialEvidenceRequest.Combine(
                    LilToonOpaqueTarget.RecipeEvidenceRequest,
                    SourceEvidenceRequest);

        /// <summary>
        /// The 19 property names this module reads off the SOURCE material,
        /// in a fixed order the finiteness sweep and <see cref="Read"/> index
        /// by. Sharing a name with the recipe does not make a source
        /// render-state fact target evidence.
        /// </summary>
        private static readonly string[] EligibilitySchema =
            BuildEligibilitySchema();

        internal static IReadOnlyCollection<string>
            EligibilitySchemaProperties { get; } =
                new ReadOnlyCollection<string>(EligibilitySchema);

        private static string[] BuildEligibilitySchema()
        {
            var recipe = LilToonOpaqueTarget.RecipeSchemaProperties;
            var schema = new string[recipe.Count + SourceSchema.Length];
            var index = 0;
            foreach (var property in recipe)
            {
                schema[index++] = property;
            }

            foreach (var property in SourceSchema)
            {
                schema[index++] = property;
            }

            return schema;
        }
```

Inside the moved `EvaluateVerifiedEligibility` and `Read`, replace every `ConversionSchema`
occurrence with `EligibilitySchema`. The gate bodies, their order, their comments, and every
refusal member stay byte-identical.

- [ ] **Step 6: Migrate every call site and retire the old type**

Production:

- `UnityMaterialSemantics.cs:324`: `LilToonOpaqueConversion.ConversionEvidenceRequest` →
  `LilToonCutoutSourceEligibility.ConversionEvidenceRequest`.
- `AlphaSeparationPreparation.cs:476-477` → `LilToonCutoutSourceEligibility.ConversionEvidenceRequest`;
  `:535` → `LilToonOpaqueTarget.ReadEffectiveRenderState`;
  `:553-555` → `LilToonCutoutSourceEligibility.EvaluateVerifiedEligibility`;
  `:567-568` → `LilToonOpaqueTarget.PrepareCanonicalOpaqueClone`;
  `:711` → `LilToonCutoutSourceEligibility.ConversionEvidenceRequest`;
  `:732` → `LilToonOpaqueTarget.CanonicalOpaqueProperties`;
  `:22` doc `<see cref="LilToonOpaqueConversion"/>` → `<see cref="LilToonOpaqueTarget"/>`.
- `AmusePlatformFinishPlugin.cs:215` doc text `LilToonOpaqueConversion` → `LilToonOpaqueTarget`.

Tests:

- `VerifiedLilToonTestSeams.cs:56,197,199,213` → the split types; `:179` doc text.
- `AlphaSeparationApplyTests.cs:2217,2225,2228,2230,2232,2275` → `LilToonOpaqueTarget`.
- `AlphaSeparationPreparationTests.cs:1579` → `LilToonCutoutSourceEligibility.MaxProvableCutoff`.
- `AlphaSeparationPersistenceTests.cs:451-452`: replace the single audited entry with four, so a
  moved or emptied source fails rather than passes vacuously:

```csharp
                ("Semantics/LilToon/LilToonOpaqueConversionResult.cs",
                    "class LilToonOpaqueConversionFactors"),
                ("Semantics/LilToon/LilToonOpaqueTarget.cs",
                    "class LilToonOpaqueTarget"),
                ("Semantics/LilToon/LilToonCutoutSourceEligibility.cs",
                    "class LilToonCutoutSourceEligibility"),
```

  (the fourth, `LilToonTransparentSourceEligibility.cs`, is added in Task 4.)

- `UnityMaterialSemanticsTests.cs:388-392`: the assertion currently reads its expectation from
  the production constant, which would let a wrong split test itself. Replace with a literal:

```csharp
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "_SrcBlend", "_DstBlend", "_AlphaToMask", "_ZWrite",
                    "_ZTest", "_OffsetFactor", "_OffsetUnits", "_ColorMask",
                    "_SrcBlendAlpha", "_DstBlendAlpha", "_BlendOp",
                    "_BlendOpAlpha", "_SrcBlendFA", "_DstBlendFA",
                    "_SrcBlendAlphaFA", "_DstBlendAlphaFA", "_BlendOpFA",
                    "_BlendOpAlphaFA", "_Cutoff",
                },
                captureSchema.PresenceProperties,
                "the cutout capture schema's presence dimension must stay " +
                "exactly the recipe plus the cutout source's own _Cutoff");
```

Confirm both moves landed, using file facts rather than Git rename detection:

```bash
sha256sum \
  Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs.meta \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs.meta
```

Required:

| Fact | Requirement |
|---|---|
| `Editor/Semantics/LilToon/LilToonOpaqueTarget.cs` + `.meta` | both exist |
| `Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs` + `.meta` | both exist |
| Old `LilToonOpaqueConversion.cs` / `LilToonOpaqueConversionTests.cs` + their `.meta` | absent |
| `LilToonOpaqueTarget.cs.meta` SHA-256 | `eadc3b7413f2143bfa3e3bceb3dd3752dc9727ceb5bfc017170292fa5550aa6f` |
| `LilToonOpaqueTargetTests.cs.meta` SHA-256 | `21830037034d0e27683718b101e8bba7c97d751b494f3d91b10a6fb92f1153b0` |
| GUIDs inside those `.meta` files | `b60b82aa490a24e929afd693ed059d96`, `86eedea91214449ecb959f6d50ab64b0` |

Either hash differing means the `.meta` was rewritten rather than moved: **stop** and inspect the
complete metadata diff. The identifier `LilToonOpaqueConversion` must no longer appear anywhere
in the package.

- [ ] **Step 7: Run the tests to verify they pass**

Unity run protocol: refresh Unity → wait for compilation and domain reload → `run_tests`
EditMode → poll `get_test_job` to completion → read the Console → record the filter, passed,
failed, skipped, and every Console error or warning.
Class filter: `Alrauna.Amuse.Tests.Editor.Semantics.LilToon`,
`Alrauna.Amuse.Tests.Editor.Build`.
Expected: PASS, with the same test count as before the split plus the three new cases.

- [ ] **Step 8: Mechanical ownership review**

Not a test — §14 asserts no source text. Read `LilToonOpaqueTarget.cs` and confirm it contains
no occurrence of `Refusal`, `BlendFactor`, `Eligibility`, or `_Cutoff`. Confirm neither source
module contains a recipe value, a clone operation, or a read-back check. If either check fails,
the split is wrong — fix it before Task 2.

- [ ] **Step 9: Commit** (only with commit authorization)

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon \
        Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs \
        Packages/com.alrauna.amuse/Editor/Build \
        Packages/com.alrauna.amuse/Tests/Editor
git commit -m "refactor: split lilToon opaque conversion by ownership"
```

---

## Task 2: Transparent source attestation profile

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:352-362` (constants), `:412-420` (profiles), `:1168-1173` (verify wrappers), `:1358-1363` (gather wrappers)
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces:
  - `LilToonSourceAttestation.{TransparentShaderName, TransparentShaderGuid, TransparentPassShaderName, TransparentPassShaderGuid, TransparentRenderMode, TransparentShaderCanonicalDigest, TransparentPassCanonicalDigest}`
  - `LilToonSourceAttestation.TryVerifyLilToonTransparentIdentity(LilToonSourceEvidence, out LilToonSemanticDiagnostic)`
  - `LilToonSourceAttestation.GatherTransparentSourceEvidence(Shader, CapturedMaterialEvidence)`

- [ ] **Step 1: Write the failing attestation tests**

Append to `LilToonAttestationTests.cs`, mirroring the cutout block at `:1480-1563`:

```csharp
        // --- transparent profile ---

        /// <summary>
        /// Mirrors <see cref="CutoutEvidence"/> for the pinned transparent
        /// identity: the same <see cref="UsePin"/> sentinel mechanics, the
        /// same shared package/format/include-tree pins, and the transparent
        /// name, GUIDs, render mode, and canonical digests by default.
        /// </summary>
        private static LilToonSourceEvidence TransparentEvidence(
            string shaderName = "Hidden/lilToonTransparent",
            string assetGuid = "165365ab7100a044ca85fc8c33548a62",
            bool hasVersion = true,
            float version = 45f,
            bool hasPackage = true,
            string packageName = "jp.lilxyzw.liltoon",
            string packageVersion = "2.3.4",
            string passGuid = "2683fad669f20ec49b8e9656954a33a8",
            string shaderDigest = UsePin,
            string passDigest = UsePin,
            string includeDigest = UsePin,
            bool hasRenderMode = true,
            int renderMode = 2,
            IReadOnlyCollection<string> features = null,
            bool hasShaderCanonicalization = true,
            LilToonCanonicalizationAnalysis shaderCanonicalization = null,
            bool hasPassCanonicalization = true,
            LilToonCanonicalizationAnalysis passCanonicalization = null)
        {
            return new LilToonSourceEvidence(
                shaderName,
                assetGuid,
                hasVersion,
                version,
                hasPackage,
                packageName,
                packageVersion,
                passGuid,
                ReferenceEquals(shaderDigest, UsePin)
                    ? LilToonSourceAttestation
                        .TransparentShaderCanonicalDigest
                    : shaderDigest,
                ReferenceEquals(passDigest, UsePin)
                    ? LilToonSourceAttestation.TransparentPassCanonicalDigest
                    : passDigest,
                ReferenceEquals(includeDigest, UsePin)
                    ? LilToonSourceAttestation.IncludeTreeDigest
                    : includeDigest,
                hasRenderMode,
                renderMode,
                features ?? new string[0],
                hasShaderCanonicalization
                    ? shaderCanonicalization ?? EmptyShaderAnalysis()
                    : null,
                hasPassCanonicalization
                    ? passCanonicalization ?? PassAnalysis(DefaultStandaloneRecords())
                    : null);
        }

        [Test]
        public void VerifyTransparent_CanonicalEvidence_Succeeds()
        {
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        TransparentEvidence(), out var diagnostic),
                Is.True);
            Assert.That(diagnostic, Is.Null);
        }

        [Test]
        public void TransparentConstants_AreTheMeasuredPins()
        {
            Assert.That(
                LilToonSourceAttestation.TransparentShaderName,
                Is.EqualTo("Hidden/lilToonTransparent"));
            Assert.That(
                LilToonSourceAttestation.TransparentShaderGuid,
                Is.EqualTo("165365ab7100a044ca85fc8c33548a62"));
            Assert.That(
                LilToonSourceAttestation.TransparentPassShaderName,
                Is.EqualTo("Hidden/ltspass_transparent"));
            Assert.That(
                LilToonSourceAttestation.TransparentPassShaderGuid,
                Is.EqualTo("2683fad669f20ec49b8e9656954a33a8"));
            Assert.That(
                LilToonSourceAttestation.TransparentRenderMode, Is.EqualTo(2));
            Assert.That(
                LilToonSourceAttestation.TransparentShaderCanonicalDigest,
                Is.EqualTo(
                    "ea247d3cd6ecb09ad4aeefdcad37480c0dffa40d594a3b4576240" +
                    "97f2372ba13"));
            Assert.That(
                LilToonSourceAttestation.TransparentPassCanonicalDigest,
                Is.EqualTo(
                    "700a607661f2cc43550452795d8eae0634509dbd07b4e8c381d94" +
                    "12fcc52517f"));
        }

        [TestCase("Hidden/lilToonOnePassTransparent")]
        [TestCase("Hidden/lilToonTwoPassTransparent")]
        [TestCase("Hidden/lilToonTransparentOutline")]
        [TestCase("Hidden/lilToonTransparen")]
        [TestCase("hidden/liltoontransparent")]
        public void VerifyTransparent_NearMissShaderName_Refuses(string name)
        {
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        TransparentEvidence(shaderName: name),
                        out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(
                    LilToonSemanticDiagnosticCode.UnsupportedShader));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        public void VerifyTransparent_WrongRenderMode_Refuses(int mode)
        {
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        TransparentEvidence(renderMode: mode), out _),
                Is.False);
        }

        [Test]
        public void VerifyTransparent_WrongShaderDigest_Refuses()
        {
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        TransparentEvidence(
                            shaderDigest: new string('0', 64)),
                        out _),
                Is.False);
        }

        [Test]
        public void VerifyTransparent_WrongPassDigest_Refuses()
        {
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        TransparentEvidence(passDigest: new string('0', 64)),
                        out _),
                Is.False);
        }

        [Test]
        public void VerifyTransparent_WrongPassGuid_Refuses()
        {
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        TransparentEvidence(passGuid: new string('0', 32)),
                        out _),
                Is.False);
        }

        /// <summary>
        /// Profile-leakage guards in both directions. Falsifies a third
        /// profile that widened a shared conjunction instead of adding an
        /// exact identity.
        /// </summary>
        [Test]
        public void ExistingVerifiers_StillRejectTransparentIdentity()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    TransparentEvidence(), out var opaqueDiagnostic),
                Is.False);
            Assert.That(
                opaqueDiagnostic.Code,
                Is.EqualTo(
                    LilToonSemanticDiagnosticCode.UnsupportedShader));
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    TransparentEvidence(), out var cutoutDiagnostic),
                Is.False);
            Assert.That(
                cutoutDiagnostic.Code,
                Is.EqualTo(
                    LilToonSemanticDiagnosticCode.UnsupportedShader));
        }

        [Test]
        public void TransparentVerifier_RejectsCutoutAndOpaqueIdentities()
        {
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        CutoutEvidence(), out _),
                Is.False);
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        Evidence(), out _),
                Is.False);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Unity run protocol: refresh Unity → wait for compilation and domain reload → `run_tests`
EditMode → poll `get_test_job` to completion → read the Console → record the filter, passed,
failed, skipped, and every Console error or warning.
Class filter: `LilToonAttestationTests`.
Expected: compile error `CS0117` — `LilToonSourceAttestation` has no
`TransparentShaderCanonicalDigest` / `TryVerifyLilToonTransparentIdentity`.

- [ ] **Step 3: Add the constants, the profile, and the two wrappers**

In `LilToonSourceAttestation.cs`, after the cutout constants at `:362`:

```csharp
        // Transparent source identity (design §6). The two canonical digests
        // were measured on 2026-09-01 from an installed
        // jp.lilxyzw.liltoon@2.3.4 in a throwaway project outside AMUSE,
        // using a byte-identical copy of this file, in a run that first
        // reproduced all five digests already pinned above, and were
        // identical across two independent Editor sessions (T1 §3.4). Never
        // re-derive these from the lilToon repository: the generator rewrites
        // every ltspass_*.shader at import.
        internal const string TransparentShaderName =
            "Hidden/lilToonTransparent";
        internal const string TransparentShaderGuid =
            "165365ab7100a044ca85fc8c33548a62";
        internal const string TransparentPassShaderName =
            "Hidden/ltspass_transparent";
        internal const string TransparentPassShaderGuid =
            "2683fad669f20ec49b8e9656954a33a8";
        internal const int TransparentRenderMode = 2;
        internal const string TransparentShaderCanonicalDigest =
            "ea247d3cd6ecb09ad4aeefdcad37480c0dffa40d594a3b457624097f2372ba13";
        internal const string TransparentPassCanonicalDigest =
            "700a607661f2cc43550452795d8eae0634509dbd07b4e8c381d9412fcc52517f";
```

After `CutoutProfile` at `:420`:

```csharp
        private static readonly LilToonSourceProfile TransparentProfile =
            new LilToonSourceProfile(
                TransparentShaderName,
                TransparentShaderGuid,
                TransparentPassShaderName,
                TransparentPassShaderGuid,
                TransparentRenderMode,
                TransparentShaderCanonicalDigest,
                TransparentPassCanonicalDigest);
```

After `TryVerifyLilToonCutoutIdentity` at `:1173`:

```csharp
        /// <summary>
        /// Verifies the pinned regular Transparent Normal identity (design
        /// §6): the transparent shader, its pass, <c>LIL_RENDER 2</c>, and the
        /// transparent canonical digests, under the shared
        /// package/format/include-tree pins. Mismatch fails closed with a
        /// diagnostic; there is no name-only fallback. The near-miss vendor
        /// names Hidden/lilToonOnePassTransparent and
        /// Hidden/lilToonTwoPassTransparent share this pass asset and are
        /// refused on the shader identity.
        /// </summary>
        internal static bool TryVerifyLilToonTransparentIdentity(
            LilToonSourceEvidence evidence,
            out LilToonSemanticDiagnostic diagnostic)
        {
            return Verify(evidence, TransparentProfile, out diagnostic);
        }
```

After `GatherCutoutSourceEvidence` at `:1363`:

```csharp
        /// <summary>
        /// Gathers identity evidence for the pinned transparent identity: the
        /// material shader is read directly and only the pass the transparent
        /// profile names (<c>Hidden/ltspass_transparent</c>) is resolved. A
        /// pass that does not resolve is omitted rather than guessed, so
        /// verification fails closed.
        /// </summary>
        internal static LilToonSourceEvidence GatherTransparentSourceEvidence(
            Shader shader,
            CapturedMaterialEvidence evidence)
        {
            return Gather(shader, evidence, TransparentProfile);
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Unity run protocol: refresh Unity → wait for compilation and domain reload → `run_tests`
EditMode → poll `get_test_job` to completion → read the Console → record the filter, passed,
failed, skipped, and every Console error or warning.
Class filter: `LilToonAttestationTests`.
Expected: PASS, Console clean.

- [ ] **Step 5: Commit** (only with commit authorization)

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs
git commit -m "feat: pin the lilToon Transparent Normal source identity"
```

---

## Task 3: Transparent alpha semantics and its stand-in fixture

Implements §7, §8 and §14 rows 1–4, 6–10, 14, plus the §7.1 neutral-claim parity site. The
stand-in shader and fixture-base accessor are scaffolding this task's deliverable needs, so they
are folded in here.

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentConversionTest.shader`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs:26-29,82-90,102-115`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Characterization/NeutralClaimGatingTests.cs:143-239`

**Interfaces:**
- Consumes: `LilToonSourceAttestation.ShaderFormatVersionProperty` (Task 2 unchanged).
- Produces:
  - `LilToonTransparentMaterialSemantics.AlphaEvidenceRequest` → `MaterialEvidenceRequest`
  - `LilToonTransparentMaterialSemantics.InterpretVerifiedTransparentAlpha(CapturedMaterialEvidence)` → `SemanticOutput<ScalarSemanticValue>`
  - `LilToonTransparentMaterialSemantics.InterpretVerifiedTransparentMaterial(Material, ColorSpace, IReadOnlyCollection<string>)` → `LilToonSemanticResult`
  - `LilToonFixtureTestBase.TransparentConversionShaderName` (`Hidden/Alrauna/AmuseTests/LilToonTransparentConversionTest`)
  - `LilToonFixtureTestBase.NewTransparentFixtureMaterial()` / `CreateTransparentConversionMaterial()`

- [ ] **Step 1: Create the transparent stand-in shader**

`Tests/Editor/Semantics/LilToon/LilToonTransparentConversionTest.shader`. It is **not** a copy of
the cutout stand-in: the queue is `AlphaTest+10` (2460), `_DstBlend` defaults to 10, and it adds
the three transparent-only proof properties plus `_DistanceFadeColor` for schema completeness.

```shaderlab
// Executable specification of the lilToon regular Transparent Normal property
// contract AMUSE consumes for the transparent-to-opaque conversion. It is a
// purpose-built stand-in for deterministic tests, not a pretend lilToon
// distribution, and contains no upstream lilToon source.
Shader "Hidden/Alrauna/AmuseTests/LilToonTransparentConversionTest"
{
    Properties
    {
        [HideInInspector] _lilToonVersion ("Version", Int) = 45

        _Invisible ("Invisible", Int) = 0
        _UDIMDiscardCompile ("UDIMDiscardCompile", Int) = 0
        _UDIMDiscardMode ("UDIMDiscardMode", Int) = 0
        _ShiftBackfaceUV ("ShiftBackfaceUV", Int) = 0
        _UseParallax ("UseParallax", Int) = 0
        _UseMain2ndTex ("UseMain2ndTex", Int) = 0
        _UseMain3rdTex ("UseMain3rdTex", Int) = 0
        _AlphaMaskMode ("AlphaMaskMode", Int) = 0
        // Declared but deliberately NOT part of the transparent alpha
        // evidence request: LIL_RENDER 2 compiles the runtime dither path
        // out entirely, so an authored toggle is inert here (design §8).
        _UseDither ("UseDither", Int) = 0
        _IDMask1 ("IDMask1", Int) = 0
        _IDMask2 ("IDMask2", Int) = 0
        _IDMask3 ("IDMask3", Int) = 0
        _IDMask4 ("IDMask4", Int) = 0
        _IDMask5 ("IDMask5", Int) = 0
        _IDMask6 ("IDMask6", Int) = 0
        _IDMask7 ("IDMask7", Int) = 0
        _IDMask8 ("IDMask8", Int) = 0
        _IDMaskControlsDissolve ("IDMaskControlsDissolve", Int) = 0
        _IDMaskPrior8 ("IDMaskPrior8", Int) = 0

        _Cutoff ("Cutoff", Range(0,1)) = 0.5
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _DissolveParams ("DissolveParams", Vector) = (0,0,0.5,0.1)
        _MainTex_ScrollRotate ("ScrollRotate", Vector) = (0,0,0,0)

        // Transparent-only proof properties, at the vendor defaults.
        _AlphaBoostFA ("AlphaBoostFA", Float) = 10
        _SubpassCutoff ("SubpassCutoff", Range(0,1)) = 0.5
        _DistanceFade ("DistanceFade", Vector) = (0.1,0.01,0,0)
        _DistanceFadeColor ("DistanceFadeColor", Color) = (0,0,0,1)

        // Fresh transparent render state. Differs from the cutout stand-in
        // in exactly one default: _DstBlend is OneMinusSrcAlpha, which gate 9
        // already admits (T1 §4.4, §7).
        _SrcBlend ("SrcBlend", Float) = 1
        _DstBlend ("DstBlend", Float) = 10
        _AlphaToMask ("AlphaToMask", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
        _ZTest ("ZTest", Float) = 4
        _OffsetFactor ("OffsetFactor", Float) = 0
        _OffsetUnits ("OffsetUnits", Float) = 0
        _ColorMask ("ColorMask", Float) = 15
        _SrcBlendAlpha ("SrcBlendAlpha", Float) = 1
        _DstBlendAlpha ("DstBlendAlpha", Float) = 10
        _BlendOp ("BlendOp", Float) = 0
        _BlendOpAlpha ("BlendOpAlpha", Float) = 0
        _SrcBlendFA ("SrcBlendFA", Float) = 1
        _DstBlendFA ("DstBlendFA", Float) = 1
        _SrcBlendAlphaFA ("SrcBlendAlphaFA", Float) = 0
        _DstBlendAlphaFA ("DstBlendAlphaFA", Float) = 1
        _BlendOpFA ("BlendOpFA", Float) = 4
        _BlendOpAlphaFA ("BlendOpAlphaFA", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest+10" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert(float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            fixed4 frag() : SV_Target
            {
                return fixed4(1, 1, 1, 1);
            }
            ENDCG
        }
    }
}
```

Add the fixture-base accessors, mirroring `:82-85` and `:102-105`:

```csharp
        protected const string TransparentConversionShaderName =
            "Hidden/Alrauna/AmuseTests/LilToonTransparentConversionTest";
```

```csharp
        protected Material NewTransparentFixtureMaterial()
        {
            return Track(CreateTransparentConversionMaterial());
        }
```

```csharp
        /// <summary>
        /// Creates a transparent-stand-in material for the
        /// transparent-to-opaque conversion tests by shader name, without
        /// subclassing this base. The caller owns destruction.
        /// </summary>
        internal static Material CreateTransparentConversionMaterial()
        {
            return CreateFixtureMaterial(TransparentConversionShaderName);
        }
```

- [ ] **Step 2: Write the failing alpha tests**

Create `Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs`. Its helper block is a
deliberate duplicate of the cutout suite's (`LilToonCutoutAlphaTests.cs:116-340`) — §F of
`docs/architecture/shader-frontend-comparison.md` records duplicated fixture infrastructure as
the standing convention. The file header and helpers:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TextureWrapMode = UnityEngine.TextureWrapMode;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// The lilToon regular Transparent Normal alpha interpretation (design
    /// §7, §8; T1 §9.1). Every row here names the exact incorrect
    /// implementation it falsifies, and the rows marked "copy detector" are
    /// the ones a verbatim copy of the cutout suite would fail.
    /// </summary>
    public sealed class LilToonTransparentAlphaTests : LilToonFixtureTestBase
    {
        private const string MainTextureProperty = "_MainTex";
        private const string ColorProperty = "_Color";
        private const string CutoffProperty = "_Cutoff";
        private const string AlphaBoostFaProperty = "_AlphaBoostFA";
        private const string SubpassCutoffProperty = "_SubpassCutoff";
        private const string DistanceFadeProperty = "_DistanceFade";
        private const string DissolveParamsProperty = "_DissolveParams";
        private const string ScrollRotateProperty = "_MainTex_ScrollRotate";
        private const string UseDitherProperty = "_UseDither";
        private const string IdMaskPrior8Property = "_IDMaskPrior8";

        /// <summary>
        /// The exact transparent alpha scalar schema, stated independently of
        /// production. Three properties more than cutout
        /// (_AlphaBoostFA, _SubpassCutoff — and _DistanceFade as a vector),
        /// and one fewer: _UseDither is absent because LIL_RENDER 2 compiles
        /// the runtime dither path out (design §8; T1 §6 row 16).
        /// </summary>
        private static readonly string[] ExpectedAlphaScalars =
        {
            "_lilToonVersion",
            "_Invisible",
            "_UDIMDiscardCompile",
            "_UDIMDiscardMode",
            "_ShiftBackfaceUV",
            "_UseParallax",
            "_UseMain2ndTex",
            "_UseMain3rdTex",
            "_AlphaMaskMode",
            "_IDMask1",
            "_IDMask2",
            "_IDMask3",
            "_IDMask4",
            "_IDMask5",
            "_IDMask6",
            "_IDMask7",
            "_IDMask8",
            "_IDMaskControlsDissolve",
            "_Cutoff",
            "_AlphaBoostFA",
            "_SubpassCutoff",
        };

        private static readonly string[] ExpectedAlphaColors = { "_Color" };

        private static readonly string[] ExpectedAlphaVectors =
        {
            "_DissolveParams",
            "_MainTex_ScrollRotate",
            "_DistanceFade",
        };

        private static Color32[] SolidGrid(int width, int height, byte alpha)
        {
            var pixels = new Color32[width * height];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(255, 255, 255, alpha);
            }

            return pixels;
        }

        private static AlphaTextureData Field(int width, int height, byte value)
        {
            var bytes = new byte[width * height];
            for (var index = 0; index < bytes.Length; index++)
            {
                bytes[index] = value;
            }

            return new AlphaTextureData(width, height, bytes);
        }

        private static AlphaMipChain Chain(params AlphaTextureData[] levels)
        {
            return new AlphaMipChain(levels);
        }

        private static AlphaMipChain AllOpaqueChain()
        {
            return Chain(Field(2, 2, 255), Field(1, 1, 255));
        }

        /// <summary>Mip 0 fully opaque, mip 1 fully non-opaque.</summary>
        private static AlphaMipChain OpaqueThenTransparentChain()
        {
            return Chain(Field(2, 2, 255), Field(1, 1, 0));
        }

        private static AlphaTextureData OpaqueGridWithTransparentTexelAlpha(
            int transparentX,
            int transparentY)
        {
            var bytes = new byte[4 * 4];
            for (var index = 0; index < bytes.Length; index++)
            {
                bytes[index] = 255;
            }

            bytes[transparentY * 4 + transparentX] = 0;
            return new AlphaTextureData(4, 4, bytes);
        }

        private static AlphaFieldProvider ProvidingFor(
            CapturedMaterialEvidence evidence,
            AlphaMipChain chain)
        {
            Assert.That(
                evidence.TryGetTexture(MainTextureProperty, out var assignment),
                Is.True,
                "the transparent request captures _MainTex");
            Assert.That(
                assignment.IsAssigned &&
                assignment.Texture != null &&
                assignment.Texture.HasSourceIdentity,
                Is.True,
                "resolver-seam tests key chains on a resolved source identity");

            var expected = assignment.Texture.SourceIdentity;
            return (TextureSourceId source, TextureChannel channel,
                out AlphaMipChain result) =>
            {
                if (source.Equals(expected))
                {
                    result = chain;
                    return true;
                }

                result = null;
                return false;
            };
        }

        /// <summary>Nondegenerate lower-left corner triangle.</summary>
        private static TriangleAlphaInput CornerTriangle()
        {
            return TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.05f, 0.05f),
                new Vector2(0.45f, 0.05f),
                new Vector2(0.05f, 0.45f));
        }

        /// <summary>
        /// Hull stops at u = 0.7 in a 4-wide texture: inside texel column 2
        /// for point filtering, but within the half-texel bilinear reach of
        /// the transparent column-3 texel.
        /// </summary>
        private static TriangleAlphaInput HalfTexelOutsideHullTriangle()
        {
            return TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.05f, 0.05f),
                new Vector2(0.7f, 0.05f),
                new Vector2(0.05f, 0.7f));
        }

        /// <summary>
        /// Crosses the u = 1 seam (u in [0.85, 1.1]): Repeat wraps into texel
        /// column 0, Clamp pins into column 3.
        /// </summary>
        private static TriangleAlphaInput SeamCrossingTriangle()
        {
            return TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.85f, 0.05f),
                new Vector2(1.1f, 0.05f),
                new Vector2(0.85f, 0.4f));
        }

        private static CapturedMaterialEvidence CaptureTransparentEvidence(
            Material material)
        {
            return UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material,
                    LilToonTransparentMaterialSemantics.AlphaEvidenceRequest),
            })[0];
        }

        private AlphaResolution ResolveThroughTransparentFrontend(
            Material material,
            AlphaMipChain chain)
        {
            var captured = CaptureTransparentEvidence(material);
            var alpha = LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentAlpha(captured);
            return AlphaSemanticsResolver.Resolve(
                alpha, ProvidingFor(captured, chain));
        }

        private Material NewGateOffMaterialWithOpaqueTexture(string textureName)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetTexture(
                MainTextureProperty,
                ImportMipmapTexture(
                    textureName, 4, 4, SolidGrid(4, 4, 255)));
            return material;
        }

        private static void AssertAlphaGateUnknown(
            LilToonSemanticResult result,
            string propertyName)
        {
            Assert.That(
                result.Semantics.Alpha.IsComplete,
                Is.False,
                $"{propertyName}: alpha must stay Unknown");
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Alpha)
                    .Any(d =>
                        d.Code == LilToonSemanticDiagnosticCode
                            .UnsupportedFeature &&
                        d.Detail.Contains(propertyName)),
                Is.True,
                "expected an UnsupportedFeature alpha diagnostic naming " +
                propertyName);
        }

        private static LilToonSemanticResult InterpretTransparent(
            Material material)
        {
            return LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentMaterial(
                    material, ColorSpace.Linear, AllFeatures);
        }
```

Then the rows. §14 row 15's animation coverage and rows 12–13, 16–21 belong to later tasks; this
step writes rows 1, 2, 3, 4, 6, 7, 8, 9, 10, 11, 14 and the request-shape case:

```csharp
        [Test]
        public void AlphaEvidenceRequest_MatchesTheIndependentExactSchema()
        {
            var request =
                LilToonTransparentMaterialSemantics.AlphaEvidenceRequest;

            Assert.That(request.ShaderName, Is.True);
            Assert.That(request.ActiveColorSpace, Is.False);
            Assert.That(request.PresenceProperties, Is.Empty);
            CollectionAssert.AreEquivalent(
                ExpectedAlphaScalars, request.ScalarProperties);
            CollectionAssert.AreEquivalent(
                ExpectedAlphaColors, request.ColorProperties);
            CollectionAssert.AreEquivalent(
                ExpectedAlphaVectors, request.VectorProperties);
            Assert.That(request.TextureProperties.Count, Is.EqualTo(1));
            Assert.That(
                request.TextureProperties[0].PropertyName,
                Is.EqualTo("_MainTex"));
            Assert.That(
                request.TextureProperties[0].Evidence,
                Is.EqualTo(
                    TextureEvidenceKinds.ScaleOffset |
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.Sampling |
                    TextureEvidenceKinds.AlphaChannel));

            // Copy detector: a widened or copied cutout request would carry
            // _UseDither, which LIL_RENDER 2 compiles out.
            CollectionAssert.DoesNotContain(
                request.ScalarProperties, UseDitherProperty);
        }

        // --- row 1: every mip is classified -------------------------------

        [Test]
        public void TransparentTexelOnlyInALowerMip_ForcesMustRemainTransparent()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_mip");

            var resolution = ResolveThroughTransparentFrontend(
                material, OpaqueThenTransparentChain());

            // Falsifies: an implementation that classifies mip 0 only.
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void AllOpaqueChain_ProvesTheCornerTriangle()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_chain");

            var resolution =
                ResolveThroughTransparentFrontend(material, AllOpaqueChain());

            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        // --- row 2: footprint dilation and wrap ---------------------------

        [Test]
        public void PointProvesAndBilinearRefusesTheHalfTexelOutsideHull()
        {
            var pointMaterial = NewTransparentFixtureMaterial();
            pointMaterial.SetTexture(
                MainTextureProperty,
                ImportMipmapTexture(
                    "t_point", 4, 4, SolidGrid(4, 4, 255),
                    FilterMode.Point, TextureWrapMode.Clamp));
            var bilinearMaterial = NewTransparentFixtureMaterial();
            bilinearMaterial.SetTexture(
                MainTextureProperty,
                ImportMipmapTexture(
                    "t_bilinear", 4, 4, SolidGrid(4, 4, 255),
                    FilterMode.Bilinear, TextureWrapMode.Clamp));

            var chain = Chain(OpaqueGridWithTransparentTexelAlpha(3, 0));
            var pointResolution =
                ResolveThroughTransparentFrontend(pointMaterial, chain);
            var bilinearResolution =
                ResolveThroughTransparentFrontend(bilinearMaterial, chain);

            // Falsifies: hull-only classification without footprint dilation.
            Assert.That(
                pointResolution.Classify(HalfTexelOutsideHullTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque),
                "point filtering never reads outside the hull");
            Assert.That(
                bilinearResolution.Classify(HalfTexelOutsideHullTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent),
                "bilinear filtering reaches half a texel beyond the hull");
        }

        [Test]
        public void RepeatWrapsIntoTheTransparentTexelAndClampDoesNot()
        {
            var repeatMaterial = NewTransparentFixtureMaterial();
            repeatMaterial.SetTexture(
                MainTextureProperty,
                ImportMipmapTexture(
                    "t_repeat", 4, 4, SolidGrid(4, 4, 255),
                    FilterMode.Point, TextureWrapMode.Repeat));
            var clampMaterial = NewTransparentFixtureMaterial();
            clampMaterial.SetTexture(
                MainTextureProperty,
                ImportMipmapTexture(
                    "t_clamp", 4, 4, SolidGrid(4, 4, 255),
                    FilterMode.Point, TextureWrapMode.Clamp));

            var chain = Chain(OpaqueGridWithTransparentTexelAlpha(0, 0));

            // Falsifies: missing wrap normalization.
            Assert.That(
                ResolveThroughTransparentFrontend(repeatMaterial, chain)
                    .Classify(SeamCrossingTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent),
                "Repeat wraps the hull into the transparent texel");
            Assert.That(
                ResolveThroughTransparentFrontend(clampMaterial, chain)
                    .Classify(SeamCrossingTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque),
                "Clamp pins the same hull away from the transparent texel");
        }

        // --- row 3: the tint multiplier ------------------------------------

        [Test]
        public void ColorAlphaBelowOne_YieldsUniformMustRemainTransparent()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_mult");
            material.SetColor(ColorProperty, new Color(1f, 1f, 1f, 0.8f));

            var resolution =
                ResolveThroughTransparentFrontend(material, AllOpaqueChain());

            // Falsifies: ignoring _Color.a.
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void ColorAlphaAboveOne_RefusesAsUnsupportedMultiplier()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_mult_hi");
            material.SetColor(ColorProperty, new Color(1f, 1f, 1f, 1.5f));

            var resolution =
                ResolveThroughTransparentFrontend(material, AllOpaqueChain());

            Assert.That(resolution.IsResolved, Is.False);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteColorAlpha_IsUnknownNamingColor(float alphaValue)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_mult_nan");
            material.SetColor(ColorProperty, new Color(1f, 1f, 1f, alphaValue));

            // Falsifies: routing a non-finite multiplier into the resolver's
            // uniform-transparent fallthrough.
            AssertAlphaGateUnknown(
                InterpretTransparent(material), ColorProperty);
        }

        // --- row 4: the transparent cutoff bound (copy detector) ----------

        [TestCase(0.9999f)]
        [TestCase(1.0f)]
        public void CutoffAtOrBelowOne_ProvesTheCornerTriangle(float cutoff)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_cutoff_ok");
            material.SetFloat(CutoffProperty, cutoff);

            var resolution =
                ResolveThroughTransparentFrontend(material, AllOpaqueChain());

            // Falsifies: copying cutout's 0.9999 bound, which refuses 1.0.
            // The transparent site is a plain clip(a - c), and at a = 1 the
            // difference 1 - c is nonnegative for every finite c <= 1
            // (T1 §9.2).
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [TestCase(1.001f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void CutoffAboveOneOrNonFinite_IsUnknownNamingCutoff(
            float cutoff)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_cutoff_hi");
            material.SetFloat(CutoffProperty, cutoff);

            // Falsifies: modelling the cutout fwidth coverage transform here,
            // which would call 1.001 partial rather than fully discarded.
            AssertAlphaGateUnknown(
                InterpretTransparent(material), CutoffProperty);
        }

        // --- row 6: runtime gates, including the B2 counterexample --------

        [TestCase("_Invisible", 1f)]
        [TestCase("_UDIMDiscardCompile", 1f)]
        [TestCase("_UDIMDiscardMode", 1f)]
        [TestCase("_ShiftBackfaceUV", 1f)]
        [TestCase("_UseParallax", 1f)]
        [TestCase("_AlphaMaskMode", 1f)]
        [TestCase("_AlphaMaskMode", 2f)]
        [TestCase("_AlphaMaskMode", 3f)]
        [TestCase("_AlphaMaskMode", 4f)]
        [TestCase("_IDMask1", 1f)]
        [TestCase("_IDMask8", 1f)]
        [TestCase("_IDMaskControlsDissolve", 1f)]
        public void ActiveGate_KeepsAlphaUnknownNamingTheProperty(
            string property,
            float value)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_gate");
            material.SetFloat(property, value);

            // Falsifies: gating on the compiled feature set rather than
            // runtime material state.
            AssertAlphaGateUnknown(InterpretTransparent(material), property);
        }

        [Test]
        public void IdMaskControlsDissolveCounterexample_NeverCompletes()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_counter");
            material.SetFloat("_IDMaskControlsDissolve", 1f);
            material.SetFloat(IdMaskPrior8Property, 1f);
            material.SetVector(
                DissolveParamsProperty, new Vector4(0f, 0f, 0.5f, 0.1f));

            // The B2 adversarial counterexample: the vertex IDMask path can
            // force the sampled alpha chain to zero even at dissolve mode 0.
            AssertAlphaGateUnknown(
                InterpretTransparent(material), "_IDMaskControlsDissolve");
        }

        [Test]
        public void DissolveModeOne_IsUnknownNamingDissolveParams()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_dissolve");
            material.SetVector(
                DissolveParamsProperty, new Vector4(1f, 0f, 0.5f, 0.1f));

            AssertAlphaGateUnknown(
                InterpretTransparent(material), DissolveParamsProperty);
        }

        // --- row 7: _UseDither is inert here (copy detector) --------------

        [Test]
        public void ActiveUseDither_StillProvesTheCornerTriangle()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_dither");
            material.SetFloat(UseDitherProperty, 1f);

            var resolution =
                ResolveThroughTransparentFrontend(material, AllOpaqueChain());

            // Falsifies: a verbatim copy of the cutout gate array, which
            // would refuse. LIL_RENDER 2 compiles the dither path out
            // entirely (T1 §6 row 16), so an authored toggle is inert. This
            // is the positive row that makes the copy detectable.
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        // --- row 8: distance fade (copy detector) -------------------------

        [Test]
        public void DistanceFadeEnabled_IsUnknownNamingDistanceFade()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_fade_on");
            material.SetVector(
                DistanceFadeProperty, new Vector4(0.1f, 0.01f, 0.5f, 0f));

            // Falsifies: omitting the only post-clip alpha writer, and
            // gating on _DistanceFadeColor.a instead of _DistanceFade.z.
            AssertAlphaGateUnknown(
                InterpretTransparent(material), DistanceFadeProperty);
        }

        [Test]
        public void DistanceFadeNonFinite_IsUnknownNamingDistanceFade()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_fade_nan");
            material.SetVector(
                DistanceFadeProperty,
                new Vector4(0.1f, 0.01f, 0f, float.NaN));

            AssertAlphaGateUnknown(
                InterpretTransparent(material), DistanceFadeProperty);
        }

        [Test]
        public void DistanceFadeDisabled_ProvesTheCornerTriangle()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_fade_off");

            // The shipped default (0.1, 0.01, 0, 0) has z == 0 and is inert.
            var resolution =
                ResolveThroughTransparentFrontend(material, AllOpaqueChain());

            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        // --- row 9: depth fade is dead code -------------------------------

        [Test]
        public void DepthFade_IsNeitherScannedNorRequested()
        {
            var request =
                LilToonTransparentMaterialSemantics.AlphaEvidenceRequest;

            // Falsifies: speculative _DepthFade* gates, and an
            // implementation that assumes the block is live. The pinned
            // package never defines LIL_FEATURE_DEPTH_FADE, so the block is
            // unreachable (T1 §5.5).
            foreach (var property in request.ScalarProperties
                         .Concat(request.VectorProperties)
                         .Concat(request.ColorProperties))
            {
                Assert.That(
                    property.StartsWith("_DepthFade", StringComparison.Ordinal),
                    Is.False,
                    "depth fade is dead code and must not be requested: " +
                    property);
            }

            CollectionAssert.DoesNotContain(
                AllFeatures, "LIL_FEATURE_DEPTH_FADE");
        }

        // --- row 10: the 2nd and 3rd layer alpha writers ------------------

        [TestCase("_UseMain2ndTex")]
        [TestCase("_UseMain3rdTex")]
        public void ActiveLayer_KeepsAlphaUnknownNamingTheLayerToggle(
            string property)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_layer");
            material.SetFloat(property, 1f);

            // Falsifies: missing the LIL_RENDER != 0 layer alpha writers.
            AssertAlphaGateUnknown(InterpretTransparent(material), property);
        }

        // --- row 11: the ForwardAdd premultiply (copy detector) -----------

        [TestCase(1f)]
        [TestCase(10f)]
        public void AlphaBoostFaAtOrAboveOne_ProvesTheCornerTriangle(
            float boost)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_boost_ok");
            material.SetFloat(AlphaBoostFaProperty, boost);

            var resolution =
                ResolveThroughTransparentFrontend(material, AllOpaqueChain());

            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [TestCase(0.5f)]
        [TestCase(0f)]
        [TestCase(float.NaN)]
        public void AlphaBoostFaBelowOne_IsUnknownNamingTheProperty(
            float boost)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_boost_bad");
            material.SetFloat(AlphaBoostFaProperty, boost);

            // Falsifies: treating ForwardAdd as if it were the base pass.
            // The base premultiply rgb *= a is the identity at a = 1; the
            // ForwardAdd premultiply saturate(a * _AlphaBoostFA) is not
            // (T1 §5.3).
            AssertAlphaGateUnknown(
                InterpretTransparent(material), AlphaBoostFaProperty);
        }

        // --- row 5 (alpha side): the subpass shadow clip ------------------

        [TestCase(0.5f)]
        [TestCase(1.0f)]
        public void SubpassCutoffAtOrBelowOne_ProvesTheCornerTriangle(
            float subpassCutoff)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_sub_ok");
            material.SetFloat(SubpassCutoffProperty, subpassCutoff);

            // 0.5 is the shipped default: a bound tighter than the measured
            // slice-15 result would silently lose the whole default
            // population (T1 §9.4).
            var resolution =
                ResolveThroughTransparentFrontend(material, AllOpaqueChain());

            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void SubpassCutoffJustAboveOne_IsUnknownNamingTheProperty()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_sub_eps");
            material.SetFloat(
                SubpassCutoffProperty, MathF.BitIncrement(1f));

            // Falsifies: omitting the subpass shadow condition entirely, or
            // treating SHADOW_CASTER as identical to the target's.
            AssertAlphaGateUnknown(
                InterpretTransparent(material), SubpassCutoffProperty);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void NonFiniteSubpassCutoff_IsUnknownNamingTheProperty(
            float value)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_sub_nan");
            material.SetFloat(SubpassCutoffProperty, value);

            AssertAlphaGateUnknown(
                InterpretTransparent(material), SubpassCutoffProperty);
        }

        // --- row 14: exact UV identity ------------------------------------

        [TestCase(1f, 1f, 0f, 0.0001f)]
        [TestCase(2f, 1f, 0f, 0f)]
        [TestCase(1f, 1f, 0.000005f, 0f)]
        public void NonIdentityMainTexSt_IsRefusedAtTheFamilyBoundary(
            float scaleX,
            float scaleY,
            float offsetX,
            float offsetY)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_st");
            material.SetTextureScale(
                MainTextureProperty, new Vector2(scaleX, scaleY));
            material.SetTextureOffset(
                MainTextureProperty, new Vector2(offsetX, offsetY));

            // Falsifies: delegating lilToon ST to PR #42's family-blind
            // affine resolver, and using Unity's epsilon-based Vector2
            // equality instead of per-binary32-component tests.
            // lilRotateUV has no zero-angle early-out at this version
            // (T1 §5.6), so transparent inherits the identity-only boundary.
            var result = InterpretTransparent(material);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Alpha)
                    .Any(d =>
                        d.Code ==
                        LilToonSemanticDiagnosticCode.UnsupportedUv),
                Is.True);
        }

        [TestCase(0.0001f, 0f, 0f, 0f)]
        [TestCase(0f, 0.0001f, 0f, 0f)]
        [TestCase(0f, 0f, 0.0001f, 0f)]
        [TestCase(0f, 0f, 0f, 0.0001f)]
        public void NonZeroScrollRotateComponent_IsUnknownNamingScrollRotate(
            float x, float y, float z, float w)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_scroll");
            material.SetVector(
                ScrollRotateProperty, new Vector4(x, y, z, w));

            AssertAlphaGateUnknown(
                InterpretTransparent(material), ScrollRotateProperty);
        }

        // --- row 16: compilation-variant invariance -----------------------

        [Test]
        public void AlphaVerdict_IsInvariantUnderFeaturesAndColorSpace()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_invariance");
            var superset = AllFeatures
                .Concat(new[] { "LIL_FEATURE_UNRELATED" })
                .ToArray();

            var withAll = LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentMaterial(
                    material, ColorSpace.Linear, AllFeatures)
                .Semantics.Alpha;
            var withSuperset = LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentMaterial(
                    material, ColorSpace.Linear, superset)
                .Semantics.Alpha;
            var withEmpty = LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentMaterial(
                    material, ColorSpace.Linear, Array.Empty<string>())
                .Semantics.Alpha;
            var withGamma = LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentMaterial(
                    material, ColorSpace.Gamma, AllFeatures)
                .Semantics.Alpha;

            // Falsifies: a verdict that depends on the define set rather than
            // runtime gates — a broken callback-100 invariance claim.
            Assert.That(withAll.IsComplete, Is.True);
            Assert.That(withSuperset.IsComplete, Is.True);
            Assert.That(withEmpty.IsComplete, Is.True);
            Assert.That(withGamma.IsComplete, Is.True);
            Assert.That(withSuperset.Value, Is.EqualTo(withAll.Value));
            Assert.That(withEmpty.Value, Is.EqualTo(withAll.Value));
            Assert.That(withGamma.Value, Is.EqualTo(withAll.Value));
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Unity run protocol: refresh Unity → wait for compilation and domain reload → `run_tests`
EditMode → poll `get_test_job` to completion → read the Console → record the filter, passed,
failed, skipped, and every Console error or warning.
Class filter: `LilToonTransparentAlphaTests`.
Expected: compile error `CS0246` — `LilToonTransparentMaterialSemantics` does not exist.

- [ ] **Step 4: Write the transparent alpha semantics**

`Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs`. It follows
`LilToonCutoutMaterialSemantics` structurally — same private helpers
(`FirstFailedZeroGate`, `RecordUnknown`, `IsFinite` ×2, `RequireAnalyzableMaterial`), same
texture arm, same `Texture` / `TextureTimesConstant` result shape. Four deltas:

1. `AlphaCoverageGates` omits `_UseDither` (17 entries).
2. `MaxProvableCutoff = 1f`, with the sign-preservation justification.
3. Three new gates after the cutoff gate: `_AlphaBoostFA`, `_SubpassCutoff`, `_DistanceFade`.
4. The request adds `_AlphaBoostFA` and `_SubpassCutoff` scalars and `_DistanceFade` vector, and
   drops `_UseDither`.

The gate sequence — every gate evaluated before any value is constructed (§7.1):

```csharp
        /// <summary>
        /// The transparent clip bound (design §9 gate 12; T1 §9.2). The
        /// transparent forward site is a plain clip(fd.col.a - _Cutoff), not
        /// the cutout coverage transform, so the cutout twice-margin bound
        /// 0.9999 is deliberately NOT reused: for any finite c &lt;= 1 the
        /// exact difference 1 - c is nonnegative, round-to-nearest preserves
        /// that sign, and clip keeps the fragment. At c == 1 the difference is
        /// exactly zero, which clip keeps. Above 1 the difference is a nonzero
        /// negative well above the underflow threshold, and clip discards.
        /// </summary>
        private const float MaxProvableCutoff = 1f;

        /// <summary>
        /// The ForwardAdd premultiply lower bound (T1 §5.3). The base pass
        /// premultiply is fd.col.rgb *= fd.col.a, the identity at a = 1; the
        /// ForwardAdd pass instead applies saturate(fd.col.a *
        /// _AlphaBoostFA), which is the identity at a = 1 only when the boost
        /// saturates to at least one.
        /// </summary>
        private const float MinProvableAlphaBoostFa = 1f;

        /// <summary>
        /// The subpass shadow clip bound (T1 §9.4, measured). At a = 1 the
        /// dither sample returns 1 at all sixteen positions of the
        /// _DitherMaskLOD slice the alpha selects, so the shadow clip reduces
        /// to clip(1 - _SubpassCutoff) and keeps by the same sign-preservation
        /// argument as the forward cutoff.
        /// </summary>
        private const float MaxProvableSubpassCutoff = 1f;
```

and, inserted between the cutout function's step (4) and step (5):

```csharp
            // (5) ForwardAdd premultiply. Unlike the base pass, the
            // FORWARD_ADD premultiply is not an identity at a = 1 unless the
            // boost is at least one; below it the additive pass composites a
            // darkened colour the opaque target would not.
            if (!evidence.TryGetScalar(
                    AlphaBoostFaProperty, out var alphaBoostFa) ||
                !IsFinite(alphaBoostFa) ||
                alphaBoostFa < MinProvableAlphaBoostFa)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    AlphaBoostFaProperty);
            }

            // (6) Subpass shadow clip. The SHADOW_CASTER pass clips against
            // _SubpassCutoff after a dither sample that is uniformly one at
            // a = 1; above the bound the source casts no shadow where the
            // opaque target would.
            if (!evidence.TryGetScalar(
                    SubpassCutoffProperty, out var subpassCutoff) ||
                !IsFinite(subpassCutoff) ||
                subpassCutoff > MaxProvableSubpassCutoff)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    SubpassCutoffProperty);
            }

            // (7) Distance fade. At LIL_RENDER 2 the distance-fade block
            // writes fd.col.a after the clip, so an enabled fade is the one
            // post-clip alpha writer this family must refuse. The gate is
            // the .z strength component, not _DistanceFadeColor.a: the two
            // arms diverge, and only .z disables the alpha write.
            if (!evidence.TryGetVector(
                    DistanceFadeProperty, out var distanceFade) ||
                !IsFinite(distanceFade) ||
                distanceFade.z != 0f)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    DistanceFadeProperty);
            }
```

- [ ] **Step 5: Run the tests to verify they pass**

Unity run protocol: refresh Unity → wait for compilation and domain reload → `run_tests`
EditMode → poll `get_test_job` to completion → read the Console → record the filter, passed,
failed, skipped, and every Console error or warning.
Class filter: `LilToonTransparentAlphaTests`.
Expected: PASS, Console clean.

- [ ] **Step 6: Extend the neutral-claim parity site**

In `NeutralClaimGatingTests.cs`, add a `LilToonTransparentNeutralClaimGatingTests` class beside
`LilToonNeutralClaimGatingTests` (`:143-239`), following that class's shape exactly. Its gate
list is an explicit test-local copy of the 17 reviewed transparent gate names plus the three
transparent-only writers — never read from production, which would pass vacuously if the
production list were emptied:

```csharp
    public sealed class LilToonTransparentNeutralClaimGatingTests
        : LilToonFixtureTestBase
    {
        // Full reviewed list. _UseDither is deliberately absent: it is
        // compiled out at LIL_RENDER 2 and is not a gate on this family.
        private static readonly string[] AlphaCoverageGates =
        {
            "_Invisible",
            "_UDIMDiscardCompile",
            "_UDIMDiscardMode",
            "_ShiftBackfaceUV",
            "_UseParallax",
            "_UseMain2ndTex",
            "_UseMain3rdTex",
            "_AlphaMaskMode",
            "_IDMask1", "_IDMask2", "_IDMask3", "_IDMask4",
            "_IDMask5", "_IDMask6", "_IDMask7", "_IDMask8",
            "_IDMaskControlsDissolve",
        };

        [Test]
        public void Alpha_NoMainTex_CoverageGateEnabled_IsNotClaimed(
            [ValueSource(nameof(AlphaCoverageGates))] string gate)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat(gate, 1f);

            var result = LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentMaterial(
                    material, ColorSpace.Linear, AllFeatures);

            Assert.That(
                result.Semantics.Alpha.IsComplete,
                Is.False,
                gate + ": an enabled coverage writer must block the claim " +
                "even with no _MainTex assigned");
        }
    }
```

- [ ] **Step 7: Run the parity test**

Unity run protocol: refresh Unity → wait for compilation and domain reload → `run_tests`
EditMode → poll `get_test_job` to completion → read the Console → record the filter, passed,
failed, skipped, and every Console error or warning.
Class filter: `NeutralClaimGatingTests`.
Expected: PASS, including the pre-existing Poiyomi and lilToon classes.

- [ ] **Step 8: Record the settled animated-`_UseDither` contract**

No experiment. Current source settles it, so the contract is fixed here and the end-to-end test
lands in Task 6 Step 1, after Task 5 wires the family:

- `UnityAnimationEvidenceCapture.ResolveProofRelevant` returns `Irrelevant` when
  `TryResolveGeneratedProperty` fails and `CouldAddressRelevantProperty` finds no requested
  property (`UnityAnimationEvidenceCapture.cs:530-533,550`).
- `CouldAddressRelevantProperty` consults only `relevance.ScalarProperties`, `ColorProperties`,
  `VectorProperties`, and the derived scale-offset names (`:670-679`).
- `_UseDither` is absent from both transparent requests **by design** (§8): `LIL_RENDER 2`
  compiles the runtime dither path out.

Therefore an animated `material._UseDither` binding on a transparent-only renderer resolves to
`Irrelevant` and is **ignored as provably inert**. That is the shipped contract, and it is
correct rather than merely conservative: the property cannot affect this family's output.

Write nothing in this task beyond the static `_UseDither = 1` positive falsifier already in
row 7 above, which stays exactly as written. If Task 6's end-to-end test observes anything other
than `Irrelevant` for a transparent-only renderer, **stop** under stop condition 6; do not change
the contract to match the observation.

A cutout source keeps its existing `_UseDither` request entry and its existing gate. Nothing in
this contract touches the cutout family.

- [ ] **Step 9: Commit** (only with commit authorization)

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon \
        Packages/com.alrauna.amuse/Tests/Editor/Semantics/Characterization/NeutralClaimGatingTests.cs
git commit -m "feat: interpret lilToon Transparent Normal alpha"
```

---

## Task 4: Transparent source eligibility

Implements §9 and §14 rows 5, 8, 11 (eligibility side), 15 and 17.

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentSourceEligibility.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueConversionResult.cs` (+3 refusal members)
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentSourceEligibilityTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueConversionResultTests.cs` (14 → 17 names)
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPersistenceTests.cs` (fourth audited entry)

**Interfaces:**
- Consumes: `LilToonOpaqueTarget.RecipeEvidenceRequest`, `LilToonOpaqueConversionFactors.*`,
  `LilToonOpaqueConversionEligibility` (Task 1).
- Produces:
  - `LilToonTransparentSourceEligibility.{SupportedTransparentRenderQueue = 2460, SupportedTransparentRenderType = "TransparentCutout", MaxProvableCutoff = 1f, MinProvableAlphaBoostFa = 1f, MaxProvableSubpassCutoff = 1f}`
  - `LilToonTransparentSourceEligibility.SourceEvidenceRequest`
  - `LilToonTransparentSourceEligibility.ConversionEvidenceRequest`
  - `LilToonTransparentSourceEligibility.EligibilitySchemaProperties` (21 scalars)
  - `LilToonTransparentSourceEligibility.EvaluateVerifiedEligibility(CapturedMaterialEvidence, int, string)`
  - `LilToonOpaqueConversionRefusal.{UnsupportedForwardAddAlphaBoost, UnsupportedDistanceFade, UnsupportedSubpassCutoff}`

**Implementation decision, recorded for review.** §9 gate 1 says "every conversion-read scalar
present" and gate 14 says "`_DistanceFade.z == 0` (vector finite)". `_DistanceFade` is a vector,
not a scalar. To keep §9's stated gate-order rationale intact — *a NaN capture must never dress
itself up as a plausible named refusal* — gate 1 also checks the vector's **presence**
(→ `ConversionPropertyAbsent`) and gate 2 also checks all four of its **components** for
finiteness (→ `ConversionPropertyNotFinite`). Gate 14 then tests only `z != 0`. This is strictly
the rationale §9 gives, applied to the one non-scalar property.

- [ ] **Step 1: Write the failing eligibility tests**

Create `Tests/Editor/Semantics/LilToon/LilToonTransparentSourceEligibilityTests.cs`:

```csharp
using System;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// The transparent source-eligibility gates (design §9). Gates 1-11 are
    /// the merged cutout rules unchanged; gates 12-15 are family-specific and
    /// each row below names the incorrect implementation it falsifies.
    /// <para>
    /// Eligible stand-ins are materials of the transparent stand-in shader,
    /// whose fresh defaults are the positive baseline: queue 2460,
    /// RenderType TransparentCutout, _Cutoff 0.5, _AlphaBoostFA 10,
    /// _SubpassCutoff 0.5, _DistanceFade.z 0, _DstBlend 10.
    /// </para>
    /// </summary>
    public sealed class LilToonTransparentSourceEligibilityTests
        : LilToonFixtureTestBase
    {
        /// <summary>
        /// The 21 scalars this module reads off the source: the 18 recipe
        /// names plus _Cutoff, _AlphaBoostFA and _SubpassCutoff. Stated
        /// literally; a test that read production would let a wrong schema
        /// test itself.
        /// </summary>
        private static readonly string[] ExpectedEligibilityScalars =
        {
            "_SrcBlend", "_DstBlend", "_AlphaToMask", "_ZWrite", "_ZTest",
            "_OffsetFactor", "_OffsetUnits", "_ColorMask",
            "_SrcBlendAlpha", "_DstBlendAlpha", "_BlendOp", "_BlendOpAlpha",
            "_SrcBlendFA", "_DstBlendFA", "_SrcBlendAlphaFA",
            "_DstBlendAlphaFA", "_BlendOpFA", "_BlendOpAlphaFA",
            "_Cutoff", "_AlphaBoostFA", "_SubpassCutoff",
        };

        private static CapturedMaterialEvidence Capture(Material material)
        {
            return UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material,
                    LilToonTransparentSourceEligibility
                        .ConversionEvidenceRequest),
            })[0];
        }

        private static LilToonOpaqueConversionEligibility EvaluateFor(
            Material material)
        {
            LilToonOpaqueTarget.ReadEffectiveRenderState(
                material, out var queue, out var renderType);
            return LilToonTransparentSourceEligibility
                .EvaluateVerifiedEligibility(
                    Capture(material), queue, renderType);
        }

        private static void AssertRefusal(
            LilToonOpaqueConversionEligibility result,
            LilToonOpaqueConversionRefusal expected)
        {
            Assert.That(
                result.Outcome,
                Is.EqualTo(LilToonOpaqueConversionOutcome.Refused));
            Assert.That(result.Refusal, Is.EqualTo(expected));
        }

        private static void AssertConvertible(
            LilToonOpaqueConversionEligibility result)
        {
            Assert.That(
                result.Outcome,
                Is.EqualTo(LilToonOpaqueConversionOutcome.Convertible),
                "refusal was " + result.Refusal);
        }

        [Test]
        public void SourceEvidenceRequest_IsExactlyTheFourSourceProperties()
        {
            var request =
                LilToonTransparentSourceEligibility.SourceEvidenceRequest;

            CollectionAssert.AreEquivalent(
                new[] { "_Cutoff", "_AlphaBoostFA", "_SubpassCutoff" },
                request.ScalarProperties);
            CollectionAssert.AreEquivalent(
                new[] { "_Cutoff", "_AlphaBoostFA", "_SubpassCutoff" },
                request.PresenceProperties);
            CollectionAssert.AreEqual(
                new[] { "_DistanceFade" }, request.VectorProperties);
            Assert.That(request.ColorProperties, Is.Empty);
            Assert.That(request.TextureProperties, Is.Empty);
        }

        [Test]
        public void EligibilitySchema_IsTheRecipePlusTheThreeSourceScalars()
        {
            CollectionAssert.AreEquivalent(
                ExpectedEligibilityScalars,
                LilToonTransparentSourceEligibility
                    .EligibilitySchemaProperties);
        }

        [Test]
        public void SupportedQueueAndRenderType_AreTheTransparentDefaults()
        {
            Assert.That(
                LilToonTransparentSourceEligibility
                    .SupportedTransparentRenderQueue,
                Is.EqualTo(2460));
            Assert.That(
                LilToonTransparentSourceEligibility
                    .SupportedTransparentRenderType,
                Is.EqualTo("TransparentCutout"));
        }

        [Test]
        public void MaxProvableCutoff_IsOne_NotTheCutoutTwiceMargin()
        {
            // Copy detector: 0.9999 here would silently refuse every
            // material authored at exactly 1.
            Assert.That(
                LilToonTransparentSourceEligibility.MaxProvableCutoff,
                Is.EqualTo(1f));
        }

        [Test]
        public void FreshTransparentStandIn_IsConvertible()
        {
            AssertConvertible(EvaluateFor(NewTransparentFixtureMaterial()));
        }

        // --- gates 3-11: the merged cutout rules, unchanged (row 17) ------

        [Test]
        public void CustomRenderQueue_RefusesUnsupportedRenderQueue()
        {
            var material = NewTransparentFixtureMaterial();
            material.renderQueue = 2475;

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.UnsupportedRenderQueue);
        }

        [Test]
        public void CustomRenderType_RefusesUnsupportedRenderType()
        {
            var material = NewTransparentFixtureMaterial();
            material.SetOverrideTag("RenderType", "Transparent");

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.UnsupportedRenderType);
        }

        [TestCase("_ZTest", 8f,
            LilToonOpaqueConversionRefusal.UnsupportedDepthComparison)]
        [TestCase("_ZWrite", 0f,
            LilToonOpaqueConversionRefusal.UnsupportedDepthWrite)]
        [TestCase("_ColorMask", 7f,
            LilToonOpaqueConversionRefusal.UnsupportedColorMask)]
        [TestCase("_OffsetFactor", -1f,
            LilToonOpaqueConversionRefusal.UnsupportedDepthOffset)]
        [TestCase("_OffsetUnits", -1f,
            LilToonOpaqueConversionRefusal.UnsupportedDepthOffset)]
        [TestCase("_BlendOp", 2f,
            LilToonOpaqueConversionRefusal.UnsupportedBlendEquation)]
        [TestCase("_DstBlend", 5f,
            LilToonOpaqueConversionRefusal.UnsupportedBlendEquation)]
        [TestCase("_BlendOpAlpha", 2f,
            LilToonOpaqueConversionRefusal.UnsupportedAlphaBlendEquation)]
        [TestCase("_BlendOpFA", 0f,
            LilToonOpaqueConversionRefusal
                .UnsupportedForwardAddBlendEquation)]
        [TestCase("_DstBlendFA", 0f,
            LilToonOpaqueConversionRefusal
                .UnsupportedForwardAddBlendEquation)]
        public void AuthoredRenderState_RefusesWithTheExactlyNamedRefusal(
            string property,
            float value,
            LilToonOpaqueConversionRefusal expected)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat(property, value);

            // Falsifies: silently normalizing authored render state the
            // alpha proof does not preserve. _BlendOpFA = Add in particular
            // would double-composite ForwardAdd against the base pass.
            AssertRefusal(EvaluateFor(material), expected);
        }

        [Test]
        public void TransparentDstBlendDefault_IsAdmittedByGateNine()
        {
            var material = NewTransparentFixtureMaterial();

            // OneMinusSrcAlpha evaluates to 0 at alpha 1, so the canonical
            // transparent default is already admitted; the recipe's 10 -> 0
            // write is an identity there (T1 §7).
            Assert.That(material.GetFloat("_DstBlend"), Is.EqualTo(10f));
            AssertConvertible(EvaluateFor(material));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void NonFiniteScalar_RefusesConversionPropertyNotFinite(
            float value)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_ZTest", value);

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
        }

        [Test]
        public void MaterialMissingTheRecipe_RefusesConversionPropertyAbsent()
        {
            // The plain semantic stand-in declares no render state at all.
            AssertRefusal(
                EvaluateFor(NewFixtureMaterial()),
                LilToonOpaqueConversionRefusal.ConversionPropertyAbsent);
        }

        // --- gate 12: the transparent cutoff bound (row 4) ----------------

        [TestCase(0.5f)]
        [TestCase(0.9999f)]
        [TestCase(1.0f)]
        public void CutoffAtOrBelowOne_IsConvertible(float cutoff)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_Cutoff", cutoff);

            AssertConvertible(EvaluateFor(material));
        }

        [Test]
        public void CutoffAboveOne_RefusesClipThresholdDiscardsOpaqueAlpha()
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_Cutoff", 1.001f);

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal
                    .ClipThresholdDiscardsOpaqueAlpha);
        }

        // --- gate 13: ForwardAdd premultiply (row 11) ---------------------

        [TestCase(1f)]
        [TestCase(10f)]
        public void AlphaBoostFaAtOrAboveOne_IsConvertible(float boost)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_AlphaBoostFA", boost);

            AssertConvertible(EvaluateFor(material));
        }

        [TestCase(0.5f)]
        [TestCase(0f)]
        public void AlphaBoostFaBelowOne_RefusesTheNamedForwardAddRefusal(
            float boost)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_AlphaBoostFA", boost);

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal
                    .UnsupportedForwardAddAlphaBoost);
        }

        // --- gate 14: distance fade (row 8) -------------------------------

        [Test]
        public void DistanceFadeEnabled_RefusesUnsupportedDistanceFade()
        {
            var material = NewTransparentFixtureMaterial();
            material.SetVector(
                "_DistanceFade", new Vector4(0.1f, 0.01f, 0.5f, 0f));

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.UnsupportedDistanceFade);
        }

        [Test]
        public void DistanceFadeNonFinite_RefusesConversionPropertyNotFinite()
        {
            var material = NewTransparentFixtureMaterial();
            material.SetVector(
                "_DistanceFade",
                new Vector4(0.1f, 0.01f, 0f, float.NaN));

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
        }

        // --- gate 15: the subpass shadow clip (row 5) ---------------------

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1.0f)]
        public void SubpassCutoffAtOrBelowOne_IsConvertible(float value)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_SubpassCutoff", value);

            // 0.5 is the shipped default: a bound tighter than the measured
            // slice-15 result loses the whole default population.
            AssertConvertible(EvaluateFor(material));
        }

        [Test]
        public void SubpassCutoffJustAboveOne_RefusesUnsupportedSubpassCutoff()
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_SubpassCutoff", MathF.BitIncrement(1f));

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.UnsupportedSubpassCutoff);
        }

        [Test]
        public void NonFiniteSubpassCutoff_RefusesBeforeTheNamedGate()
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_SubpassCutoff", float.NaN);

            // Gate order is load-bearing: a NaN must not dress itself up as
            // a plausible named refusal.
            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
        }

        // --- deliberately ungated ------------------------------------------

        [TestCase("_AlphaToMask", 1f)]
        [TestCase("_SrcBlendAlphaFA", 1f)]
        [TestCase("_DstBlendAlphaFA", 0f)]
        [TestCase("_UseDither", 1f)]
        public void DeliberatelyUngatedProperty_StaysConvertible(
            string property,
            float value)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat(property, value);

            // Each is proven inert at a = 1 (T1 §4.4, §5.6, §6). A gate here
            // would be a free false negative.
            AssertConvertible(EvaluateFor(material));
        }

        [Test]
        public void CutoutEligibility_IsUnchangedByTheTransparentFamily()
        {
            var cutout = NewOpaqueConversionMaterial();
            cutout.SetFloat("_Cutoff", 1f);

            LilToonOpaqueTarget.ReadEffectiveRenderState(
                cutout, out var queue, out var renderType);
            var evidence = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    cutout,
                    LilToonCutoutSourceEligibility
                        .ConversionEvidenceRequest),
            })[0];

            // Falsifies: a parameterized gate list leaking the transparent
            // <= 1 bound into cutout, whose bound stays 0.9999.
            AssertRefusal(
                LilToonCutoutSourceEligibility.EvaluateVerifiedEligibility(
                    evidence, queue, renderType),
                LilToonOpaqueConversionRefusal
                    .ClipThresholdDiscardsOpaqueAlpha);
        }
    }
}
```

Update `LilToonOpaqueConversionResultTests.ExpectedRefusalNames` to 17 entries, appending
`"UnsupportedForwardAddAlphaBoost"`, `"UnsupportedDistanceFade"`, `"UnsupportedSubpassCutoff"`
after `"ClipThresholdDiscardsOpaqueAlpha"`.

- [ ] **Step 2: Run the tests to verify they fail**

Unity run protocol: refresh Unity → wait for compilation and domain reload → `run_tests`
EditMode → poll `get_test_job` to completion → read the Console → record the filter, passed,
failed, skipped, and every Console error or warning.
Class filter: `LilToonTransparentSourceEligibilityTests`.
Expected: compile error `CS0246`/`CS0117` — the type and the three refusal members do not exist.

- [ ] **Step 3: Add the three refusal members**

In `LilToonOpaqueConversionResult.cs`, after `ClipThresholdDiscardsOpaqueAlpha`:

```csharp
        // Transparent-only post-clip writers (design §9 gates 13-15).
        UnsupportedForwardAddAlphaBoost,
        UnsupportedDistanceFade,
        UnsupportedSubpassCutoff,
```

- [ ] **Step 4: Write the transparent eligibility module**

`Editor/Semantics/LilToon/LilToonTransparentSourceEligibility.cs`, structurally the sibling of
`LilToonCutoutSourceEligibility` with the following exact differences:

- `SupportedTransparentRenderQueue = 2460`, `SupportedTransparentRenderType = "TransparentCutout"`.
- `MaxProvableCutoff = 1f`, `MinProvableAlphaBoostFa = 1f`, `MaxProvableSubpassCutoff = 1f`,
  each carrying the justification comment from Task 3 Step 4.
- `SourceSchema = { "_Cutoff", "_AlphaBoostFA", "_SubpassCutoff" }` and
  `SourceVectorSchema = { "_DistanceFade" }`; `SourceEvidenceRequest` passes the vector array as
  `vectorProperties`.
- Gate 1 additionally requires `evidence.TryGetVector(DistanceFadeProperty, out var
  distanceFade)`, refusing `ConversionPropertyAbsent`.
- Gate 2 additionally sweeps `distanceFade.x/y/z/w`, refusing `ConversionPropertyNotFinite`.
- Gates 3–12 are byte-identical to the cutout module's, reading
  `SupportedTransparentRenderQueue` / `SupportedTransparentRenderType` / this module's
  `MaxProvableCutoff`.
- Three new gates after gate 12:

```csharp
            // 13. ForwardAdd premultiply. The FORWARD_ADD pass applies
            //     saturate(fd.col.a * _AlphaBoostFA) rather than the base
            //     pass's fd.col.rgb *= fd.col.a. At a = 1 the base site is
            //     an identity and the ForwardAdd site is one only when the
            //     boost saturates to at least 1; below that the additive
            //     pass composites a darker colour than the opaque target.
            if (Read(values, AlphaBoostFaProperty) < MinProvableAlphaBoostFa)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal
                        .UnsupportedForwardAddAlphaBoost);
            }

            // 14. Distance fade. At LIL_RENDER 2 the block writes fd.col.a
            //     after the clip, so it is the one post-clip alpha writer.
            //     The gate is the .z strength component; _DistanceFadeColor.a
            //     drives the RGB arm and does not disable the alpha write.
            //     Non-finite components already refused at gate 2.
            if (distanceFade.z != 0f)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedDistanceFade);
            }

            // 15. Subpass shadow clip. The SHADOW_CASTER pass clips against
            //     _SubpassCutoff after a dither sample measured uniformly 1
            //     at a = 1 (T1 §9.4), so the clip reduces to
            //     clip(1 - _SubpassCutoff) and keeps iff the bound holds.
            //     The target casts shadows unconditionally, so a source that
            //     clips its shadow here is not convertible.
            if (Read(values, SubpassCutoffProperty) > MaxProvableSubpassCutoff)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedSubpassCutoff);
            }
```

- Add the fourth audited entry to `AlphaSeparationPersistenceTests.AuditedProductionFiles`:

```csharp
                ("Semantics/LilToon/LilToonTransparentSourceEligibility.cs",
                    "class LilToonTransparentSourceEligibility"),
                ("Semantics/LilToon/LilToonTransparentMaterialSemantics.cs",
                    "class LilToonTransparentMaterialSemantics"),
```

- [ ] **Step 5: Run the tests to verify they pass**

Unity run protocol: refresh Unity → wait for compilation and domain reload → `run_tests`
EditMode → poll `get_test_job` to completion → read the Console → record the filter, passed,
failed, skipped, and every Console error or warning.
Class filter: `LilToonTransparentSourceEligibilityTests`,
`LilToonOpaqueConversionResultTests`, `LilToonCutoutSourceEligibilityTests`,
`AlphaSeparationPersistenceTests`.
Expected: PASS, Console clean.

- [ ] **Step 6: Commit** (only with commit authorization)

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon \
        Packages/com.alrauna.amuse/Tests/Editor
git commit -m "feat: gate lilToon Transparent Normal source eligibility"
```

---

## Task 5: Wire the family through selection, capture, and preparation

Implements §5, §11's "what stays where it is", and §14 rows 12, 13, 15, 20, 21.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:11-17,200-235,244-280,291-306,308-340,342-362,364-417`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:464-572,703-736`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/VerifiedLilToonTestSeams.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

**Interfaces:**
- Consumes: everything produced by Tasks 1–4.
- Produces:
  - `CapturedAlphaMaterialFamily.LilToonTransparent`
  - `UnityMaterialSemantics.LilToonTransparentCaptureRequest` (private)
  - a transparent arm in `TrySelectAlphaMaterialRequests`, `TryCaptureClosedAlphaMaterials`,
    `AnalyzeAlphaMaterial`
  - a transparent branch in `AlphaSeparationPreparation.ConvertAdmittedMaterial`
  - `VerifiedLilToonTestSeams.VerifiedTransparentConversionStep` (test seam, reusing the existing
    `VerifiedLilToonConversion` delegate type)

**Implementation decision, recorded for review.** §11 specifies "+1 `case`" in
`ConvertAdmittedMaterial`. The cutout case body (`:464-572`) and the transparent one differ in
exactly three expressions: the gather wrapper, the verify wrapper, and the eligibility evaluator.
Implement them as **one shared case label pair** —
`case LilToonCutout: case LilToonTransparent:` — with three per-family conditionals, rather than
duplicating ~60 lines of admission, overwrite-rule, and seam logic. The request and the recipe
already come from the family lookups. If this cannot be done without a fourth conditional, stop
and report: that would be the mode-parameterized dispatch §11 forbids.

- [ ] **Step 1: Write the failing wiring tests**

Add to `LilToonTransparentAlphaTests.cs` (rows 12, 13, 20 selection side):

```csharp
        // --- rows 12-13: exact-name selection ------------------------------

        [TestCase("Hidden/lilToonOnePassTransparent")]
        [TestCase("Hidden/lilToonTwoPassTransparent")]
        [TestCase("Hidden/lilToonTransparentOutline")]
        public void NearMissTransparentName_IsNeverSelectedOrAdmitted(
            string shaderName)
        {
            // Both near misses declare the SAME pass asset
            // (Hidden/ltspass_transparent) and the same LIL_RENDER 2, queue
            // 2460 and RenderType as the supported family. Falsifies:
            // prefix/substring matching, Contains("Transparent"), grouping by
            // LIL_RENDER, by queue, or by pass-asset identity alone.
            Assert.That(
                shaderName,
                Is.Not.EqualTo(LilToonSourceAttestation.TransparentShaderName));
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        TransparentEvidenceNamed(shaderName), out _),
                Is.False);
        }
```

(where `TransparentEvidenceNamed` is a small local builder mirroring Task 2's
`TransparentEvidence`, or the test lives in `LilToonAttestationTests` beside it — put it wherever
the evidence builder already exists and do not duplicate the builder.)

Add to `UnityMaterialSemanticsTests.cs`, mirroring
`CutoutCaptureSchemaCarriesConversionEvidenceAlphaRelevanceDoesNot` (`:340-419`):

```csharp
        [Test]
        public void TransparentCaptureSchemaCarriesConversionEvidence()
        {
            var material = NewMaterial(
                "schema-transparent.shader",
                LilToonSourceAttestation.TransparentShaderName,
                TransparentProperties());

            var selected =
                UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                    material,
                    out var family,
                    out var alphaRelevance,
                    out var captureSchema);

            Assert.That(selected, Is.True);
            Assert.That(
                family,
                Is.EqualTo(
                    CapturedAlphaMaterialFamily.LilToonTransparent));
            Assert.That(
                alphaRelevance,
                Is.SameAs(
                    LilToonTransparentMaterialSemantics
                        .AlphaEvidenceRequest),
                "alpha relevance must remain the transparent request itself");

            foreach (var conversionOnly in
                     new[] { "_ZWrite", "_Cutoff", "_SubpassCutoff" })
            {
                CollectionAssert.Contains(
                    captureSchema.ScalarProperties, conversionOnly);
            }

            // The transparent capture must not widen the cutout or opaque
            // requests, and must not gather the compiled-out dither toggle.
            CollectionAssert.DoesNotContain(
                captureSchema.ScalarProperties, "_UseDither");
            CollectionAssert.DoesNotContain(
                captureSchema.ScalarProperties, "_EnableOutlines");
        }

        [Test]
        public void ExistingRequests_AreNotMutatedByTheTransparentFamily()
        {
            // Falsifies: a shared or widened request object.
            CollectionAssert.DoesNotContain(
                LilToonCutoutMaterialSemantics.AlphaEvidenceRequest
                    .ScalarProperties,
                "_SubpassCutoff");
            CollectionAssert.Contains(
                LilToonCutoutMaterialSemantics.AlphaEvidenceRequest
                    .ScalarProperties,
                "_UseDither");
            CollectionAssert.DoesNotContain(
                LilToonCutoutSourceEligibility.SourceEvidenceRequest
                    .ScalarProperties,
                "_AlphaBoostFA");
        }
```

with a `TransparentProperties()` helper beside the existing `CutoutProperties()`, declaring the
transparent schema.

- [ ] **Step 2: Run the tests to verify they fail**

Unity run protocol: refresh Unity → wait for compilation and domain reload → `run_tests`
EditMode → poll `get_test_job` to completion → read the Console → record the filter, passed,
failed, skipped, and every Console error or warning.
Class filter: `UnityMaterialSemanticsTests`.
Expected: compile error `CS0117` — `CapturedAlphaMaterialFamily` has no `LilToonTransparent`.

- [ ] **Step 3: Add the family member and its six arms**

`UnityMaterialSemantics.cs`:

```csharp
    internal enum CapturedAlphaMaterialFamily
    {
        Unsupported,
        Poiyomi,
        LilToon,
        LilToonCutout,
        LilToonTransparent,
    }
```

`ClassifyShaderName` — after the cutout arm at `:277`:

```csharp
            if (string.Equals(
                    shaderName,
                    LilToonSourceAttestation.TransparentShaderName,
                    StringComparison.Ordinal))
            {
                return (
                    CapturedAlphaMaterialFamily.LilToonTransparent,
                    LilToonTransparentMaterialSemantics.AlphaEvidenceRequest);
            }
```

`BuildCapturedAlphaMaterials` — after `:228`:

```csharp
                else if (families[index] ==
                    CapturedAlphaMaterialFamily.LilToonTransparent)
                {
                    lilToon = LilToonSourceAttestation
                        .GatherTransparentSourceEvidence(
                            shaders[index], evidence[index]);
                }
```

`AlphaRequestForFamily` — after `:302`:

```csharp
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    return LilToonTransparentMaterialSemantics
                        .AlphaEvidenceRequest;
```

Capture request — after `:324`:

```csharp
        private static readonly MaterialEvidenceRequest
            LilToonTransparentCaptureRequest =
                MaterialEvidenceRequest.Combine(
                    LilToonTransparentMaterialSemantics.AlphaEvidenceRequest,
                    LilToonTransparentSourceEligibility
                        .ConversionEvidenceRequest);
```

`CaptureRequestForFamily` — after `:336`:

```csharp
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    return LilToonTransparentCaptureRequest;
```

`IsAttestedAlphaMaterial` — after `:358`:

```csharp
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    return material.LilToonEvidence != null &&
                        LilToonSourceAttestation
                            .TryVerifyLilToonTransparentIdentity(
                                material.LilToonEvidence, out _);
```

`AnalyzeAlphaMaterial` — after `:407`:

```csharp
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    if (captured.LilToonEvidence == null ||
                        !LilToonSourceAttestation
                            .TryVerifyLilToonTransparentIdentity(
                                captured.LilToonEvidence, out _))
                    {
                        return AllUnknown();
                    }

                    alpha = LilToonTransparentMaterialSemantics
                        .InterpretVerifiedTransparentAlpha(captured.Evidence);
                    break;
```

Also update the `ClassifyShaderName` doc at `:237-243` and the class doc at `:45-53`: the
"with a third family it becomes a third branch, and that is when a registry earns its first
honest argument" sentence is now due. Replace it with the measured finding — a fourth exact-name
branch is still one map with two consumers, and nothing dispatches polymorphically over the
frontends (`docs/architecture/shader-frontend-comparison.md`, last row of the promotion table).

- [ ] **Step 4: Wire preparation**

`AlphaSeparationPreparation.cs`. Change the cutout case label at `:464` to a pair:

```csharp
                case CapturedAlphaMaterialFamily.LilToonCutout:
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                {
                    var isTransparent = captured.Family ==
                        CapturedAlphaMaterialFamily.LilToonTransparent;
```

Inside the body: replace the hard-coded request at `:476-477` with
`ConversionRequestForFamily(captured.Family)`; replace the gather at `:540-543` and verify at
`:544-546` with

```csharp
                        var sourceEvidence = isTransparent
                            ? LilToonSourceAttestation
                                .GatherTransparentSourceEvidence(
                                    live.shader, derived)
                            : LilToonSourceAttestation
                                .GatherCutoutSourceEvidence(
                                    live.shader, derived);
                        var attested = isTransparent
                            ? LilToonSourceAttestation
                                .TryVerifyLilToonTransparentIdentity(
                                    sourceEvidence, out _)
                            : LilToonSourceAttestation
                                .TryVerifyLilToonCutoutIdentity(
                                    sourceEvidence, out _);
                        if (!attested)
                        {
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }
```

and the eligibility at `:552-555` with

```csharp
                        var eligibility = isTransparent
                            ? LilToonTransparentSourceEligibility
                                .EvaluateVerifiedEligibility(
                                    derived, queue, renderType)
                            : LilToonCutoutSourceEligibility
                                .EvaluateVerifiedEligibility(
                                    derived, queue, renderType);
```

`ConversionRequestForFamily` — after `:711`:

```csharp
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    return LilToonTransparentSourceEligibility
                        .ConversionEvidenceRequest;
```

`CanonicalPropertiesForFamily` — after `:732`:

```csharp
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    return LilToonOpaqueTarget.CanonicalOpaqueProperties;
```

- [ ] **Step 5: Add the transparent test seam**

`VerifiedLilToonTestSeams.cs`: add `VerifiedTransparentConversionStep`, the exact shape of the
existing cutout seam function (`:190-218`) with
`LilToonTransparentSourceEligibility.EvaluateVerifiedEligibility` substituted, still returning
`LilToonOpaqueConversionRefusal` and still passing
`Shader.Find(LilToonFixtureShaderNames.OpaqueTarget)` as the attested target. Add the matching
transparent branch to the request-selection helper at `:55-56`, using
`LilToonTransparentSourceEligibility.ConversionEvidenceRequest`.

- [ ] **Step 6: Run the tests to verify they pass**

Unity run protocol: refresh Unity → wait for compilation and domain reload → `run_tests`
EditMode → poll `get_test_job` to completion → read the Console → record the filter, passed,
failed, skipped, and every Console error or warning.
Class filter: `UnityMaterialSemanticsTests`, `AlphaSeparationPreparationTests`,
`LilToonTransparentAlphaTests`, `LilToonTransparentSourceEligibilityTests`.
Expected: PASS, Console clean.

- [ ] **Step 7: Commit** (only with commit authorization)

```bash
git add Packages/com.alrauna.amuse/Editor Packages/com.alrauna.amuse/Tests
git commit -m "feat: route the lilToon Transparent Normal family end to end"
```

---

## Task 6: End-to-end contract, mutation audit, locality, and the architecture record

Implements §14 rows 15, 18, 19, 20, 21 and §12's documentation row.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPersistenceTests.cs`
- Modify: `docs/architecture/shader-frontend-comparison.md:172-180`

**Interfaces:**
- Consumes: everything from Tasks 1–5.
- Produces: no new production symbol.

- [ ] **Step 1: Write the failing end-to-end tests**

Add to `AlphaSeparationPreparationTests.cs`, mirroring the existing cutout end-to-end cases:

- **Row 18 — prepared-clone contract.** A transparent stand-in source converts to a clone
  carrying the opaque target shader, all 18 canonical values read back, queue 2000,
  `RenderType=Opaque`; a target shader missing a recipe property throws
  `InvalidOperationException` before any clone exists; a read-back disagreement throws after
  `DestroyImmediate`. Assert the exception type and that no clone leaked, exactly as the cutout
  cases do.
- **Row 15 — animation closure.** For **each** of `_Color`, `_Cutoff`, `_AlphaBoostFA`,
  `_SubpassCutoff`, `_DistanceFade`, `_DissolveParams`, `_AlphaMaskMode`,
  `_MainTex_ScrollRotate`, `_MainTex_ST`, and one representative clause-2 gate, drive a
  non-singleton or disagreeing curve on a transparent slot and assert the slot refuses. This is
  the row that falsifies a request that omits a proof-relevant property: an omitted property
  makes its binding *unrecognized* rather than *refused*, so assert the refusal member, not
  merely "not converted".
- **The settled animated-`_UseDither` contract** (Task 3 Step 8), test named
  `AnimatedUseDither_IsIgnoredAsProvablyInert`. Build a transparent-only renderer — every slot a
  transparent source, no cutout and no Poiyomi slot — whose triangles are otherwise
  `ProvenOpaque`. Drive an animated `material._UseDither` binding on it through the **real**
  relevance and preparation path, not a hand-built resolution: the same
  `UnityAnimationEvidenceCapture` relevance pass and the same
  `AlphaSeparationPreparation` entry the build uses. Assert that the conversion result is
  observably equivalent to the same renderer prepared without the binding. Assert **observable
  facts**, not object identity across runs, because the two scenarios are two independent
  preparation runs and each mints its own clone:

  | Assertion | Both scenarios |
  |---|---|
  | Triangle outcome | `ProvenOpaque` |
  | Conversion | completes, no refusal member of any kind |
  | Clone count | exactly one canonical opaque clone |
  | Clone shader identity | the attested opaque target shader |
  | Clone render state | queue `2000`, `RenderType=Opaque` |
  | Clone recipe | all 18 canonical values equal, read back property by property |
  | Source preservation | the same source material, mesh, and animation assets unchanged |

  Additionally, the bound scenario carries no unrecognized-binding refusal. Do **not** require
  the two runs to return the same clone object. `Is.SameAs` is permitted only *within* a single
  run, and only where within-run deduplication requires two references to share one clone. Also
  assert in the same test that a cutout source with the same animated binding still resolves
  through the cutout `_UseDither` request and still hits the cutout gate, so the transparent
  omission does not remove cutout `_UseDither` relevance.
  Falsifies: a transparent request that quietly carries `_UseDither`, and a relevance pass that
  refuses an inert binding. If the transparent-only resolution is anything other than
  `Irrelevant`, **stop** under stop condition 6 rather than adjusting the assertion.
- **Row 20 — locality.** One refused transparent slot on a renderer that also carries an
  admitted Poiyomi slot and an admitted lilToon-cutout slot leaves both siblings converted; and
  an all-`Unknown` transparent outcome never becomes `ProvenOpaque` for each of: unsupported
  texture format, streamed mips, missing readback, degenerate triangle, NaN UV, region overflow.
- **Row 21 — regression parity.** Assert the Poiyomi and cutout conversion paths produce
  byte-identical results to their pre-existing expectations with a transparent sibling present.

Add to `AlphaSeparationPersistenceTests.cs`:

- **Row 19 — mutation audit.** Extend the existing NDMF-build source-preservation test to a
  transparent source: SHA of the source material's serialized properties, the source mesh, every
  texture and its import settings, every animation clip, every prefab, and the scene are
  unchanged; only `CreatedClones` and the generated mesh differ. Verify teardown destroys only
  `CreatedClones`.

- [ ] **Step 2: Run the tests to verify they fail**

Unity run protocol: refresh Unity → wait for compilation and domain reload → `run_tests`
EditMode → poll `get_test_job` to completion → read the Console → record the filter, passed,
failed, skipped, and every Console error or warning.
Class filter: `AlphaSeparationPreparationTests`, `AlphaSeparationPersistenceTests`.
Expected: FAIL on the new cases only. If any new case passes before the wiring is exercised, the
case is not falsifying anything — rewrite it.

- [ ] **Step 3: Make them pass**

No production change should be required: Tasks 1–5 deliver the behavior. If a production change
*is* required, it means a gate or a wiring arm is missing — add it and record which §14 row
caught it. If it requires a change to the clone path, **stop** (stop condition 4).

- [ ] **Step 4: Run the tests to verify they pass**

Unity run protocol: refresh Unity → wait for compilation and domain reload → `run_tests`
EditMode → poll `get_test_job` to completion → read the Console → record the filter, passed,
failed, skipped, and every Console error or warning.
Class filter: `Alrauna.Amuse.Tests.Editor.Build`.
Expected: PASS, Console clean.

- [ ] **Step 5: Record the architecture finding**

Append one row to the repeated-pressure table in
`docs/architecture/shader-frontend-comparison.md` (header at `:172`):

```markdown
| Opaque-conversion target shared by two source families in one frontend | **2** (lilToon cutout, lilToon Transparent Normal) | **1** (`LilToonOpaqueTarget`) | measured: one identical 18-property recipe, one identical clone path, byte-identical referenced pass sets | **Extracted** — the target only. The two source eligibilities stay separate: 11 shared predicates, 4 divergent gates, two different cutoff bounds, and no polymorphic call site. Extraction passed the second-consumer test on measurement, not anticipation. |
```

- [ ] **Step 6: Commit** (only with commit authorization)

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Build \
        docs/architecture/shader-frontend-comparison.md
git commit -m "test: prove the transparent conversion contract end to end"
```

---

## Task 7: Full validation and implementation report

**Files:** none modified. This task runs and observes.

- [ ] **Step 1: Run the focused affected classes (§15.1–2)**

Unity run protocol per run, recording the filter, passed, failed, skipped, and Console output for
each. Class filters: `LilToonTransparentAlphaTests`,
`LilToonTransparentSourceEligibilityTests`, `LilToonOpaqueTargetTests`,
`LilToonCutoutSourceEligibilityTests`, `LilToonOpaqueConversionResultTests`,
`LilToonAttestationTests`, `LilToonCutoutAlphaTests`, `LilToonBaseColorTests`,
`LilToonEmissionTests`, `LilToonNormalTests`, `LilToonAdversarialTests`,
`AlphaSeparationPreparationTests`, `AlphaSeparationApplyTests`, `AlphaSemanticsResolverTests`,
`TriangleAlphaClassifierTests`, `NeutralClaimGatingTests`, `AlphaSeparationPersistenceTests`,
`UnityMaterialSemanticsTests`.

- [ ] **Step 2: Run the full product and research EditMode suites (§15.3)**

Run both to completion. Record totals. Any failure blocks the report.

- [ ] **Step 3: Inspect the Unity Console (§15.4)**

Record errors and warnings, or their absence, for every run above.

- [ ] **Step 4: Verify source preservation and teardown (§15.5)**

Confirm falsifier 19's evidence was produced and observed, not merely compiled.

- [ ] **Step 5: Inspect the diffs separately (§15.6)**

```bash
git diff --stat
git diff --cached --stat
git diff --check
git status --porcelain=v1
```

Confirm: `Packages/manifest.json` and `Packages/packages-lock.json` are modified but **unstaged
and untouched by this work**; no `Library/`, `Temp/`, `Logs/`, `UserSettings/` entry appears; and
no asset changed GUID.

Because the two moves deliberately never touch the index, `git status` will report each old path
as a deleted tracked file and each new path as untracked. That is the expected shape; do **not**
require a rename classification and do **not** stage anything to obtain one. Verify identity
from the files instead:

```bash
sha256sum \
  Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs.meta \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs.meta
```

| `.meta` | GUID that must be present | SHA-256 that must be unchanged |
|---|---|---|
| `…/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs.meta` | `b60b82aa490a24e929afd693ed059d96` | `eadc3b7413f2143bfa3e3bceb3dd3752dc9727ceb5bfc017170292fa5550aa6f` |
| `…/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs.meta` | `86eedea91214449ecb959f6d50ab64b0` | `21830037034d0e27683718b101e8bba7c97d751b494f3d91b10a6fb92f1153b0` |

A moved `.meta` must be byte-identical to the file it came from; a changed hash means it was
rewritten rather than moved, so **stop** and inspect the complete metadata diff. Also confirm
both old `.cs` paths and both old `.meta` paths are absent, and that the number of `.meta` files
in the package rose by exactly the number of new assets — no `.meta` disappeared without its
asset.

- [ ] **Step 6: Write the implementation report (§21)**

State: the branch and base SHA; that the §18 constants were transcribed exactly as measured; the
exact supported and refused profile as shipped; the file map actually produced, including both
non-index moves with their verified GUIDs **and** their verified `.meta` SHA-256 hashes, and
every migrated call site; per-class test
counts — filter, passed, failed, skipped — with the Unity Console state for each run; the
source-preservation and teardown evidence from falsifier 19; separately inspected staged and
unstaged diffs plus `git diff --check`; confirmation that the two package files are untouched and
unstaged and that no Unity-generated state is included; the pinned Unity instance's exact
`Application.dataPath` match, with no other instance's name, path, hash, or port mentioned; the
observed result of `AnimatedUseDither_IsIgnoredAsProvablyInert`; the two recorded implementation
decisions (the combined request's single definition and its home, the shared lilToon case
label); and an explicit statement that Census Lab and private avatar data were not used.

---

## Self-Review

**1. Spec coverage.**

| Spec section | Task |
|---|---|
| §1 supported contract | 3, 4, 5 |
| §2 positive profile | 2 (identity), 4 (queue/type/bounds) |
| §3 non-goals | enforced by Task 1 Step 8 review and §14 rows 9, 14 |
| §4 current-state reconstruction | Task 1 (basis for the split) |
| §5 data flow | Task 5 |
| §6 attestation profile | Task 2 |
| §7 theorem + §7.1 neutral claim | Task 3 |
| §8 evidence request + three-way ownership | Tasks 1 (split), 3 (alpha), 4 (source) |
| §9 fifteen gates | Task 4 |
| §10 target preparation | Task 1 (moved unchanged), Task 6 row 18 |
| §11 architectural split | Task 1, recorded in Task 6 Step 5 |
| §12 file map | File Structure section; every path assigned |
| §13 clean cutover | Task 1 Steps 6, 8 |
| §14 rows 1–21 | 1 (mip), 2 (footprint/wrap), 3 (multiplier), 4 (cutoff), 5 (subpass), 6 (runtime gates), 7 (dither), 8 (distance fade), 9 (depth fade), 10 (layers), 11 (boost), 14 (UV) → Task 3; 5/8/11/17 eligibility side, 15 partial → Task 4; 12, 13, 15, 20, 21 → Tasks 4–6; 16 → Task 3; 18, 19 → Task 6 |
| §15 validation protocol | Task 7 |
| §16 mixed-family behavior | Task 6 rows 20, 21 |
| §17 lifecycle | Task 3 row 16 (invariance) |
| §18 constants and stop conditions | Global Constraints, Task 2 |
| §19 deferred work | out of scope by construction; no task |
| §20 git boundary | Global Constraints |
| §21 report | Task 7 Step 6 |

Two gaps found in §12's file map and closed here: the **transparent stand-in shader** is required
(the cutout stand-in is queue 2450 and cannot serve a 2460 source) and is added in Task 3; and
the **old `LilToonOpaqueConversionTests.cs` path** must disappear while its asset identity
survives, so the File Structure section names it as a non-index move that preserves both the
GUID and the `.meta` bytes, rather than as a new asset.

**2. Placeholder scan.** No `TBD`, no "handle edge cases", no "write tests for the above", no
"similar to Task N". Task 6's rows are described rather than quoted because they extend existing
test classes whose helper vocabulary the implementer will be reading in the same file; each row
names its exact falsifier, assertion target, and refusal member.

**3. Type consistency.** `LilToonOpaqueTarget.RecipeEvidenceRequest` /
`RecipeSchemaProperties`, `<family>SourceEligibility.SourceEvidenceRequest` /
`ConversionEvidenceRequest` / `EligibilitySchemaProperties` /
`EvaluateVerifiedEligibility`, `LilToonOpaqueConversionFactors.*`,
`LilToonTransparentMaterialSemantics.AlphaEvidenceRequest` /
`InterpretVerifiedTransparentAlpha` / `InterpretVerifiedTransparentMaterial`,
`LilToonSourceAttestation.TryVerifyLilToonTransparentIdentity` /
`GatherTransparentSourceEvidence`, and
`CapturedAlphaMaterialFamily.LilToonTransparent` are spelled identically in every task that
names them. `MaxProvableCutoff` deliberately exists on both source modules with different values
(`0.9999f` cutout, `1f` transparent) and is never shared.
