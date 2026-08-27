# Pinned Poiyomi Opaque Conversion Implementation Plan

> **Execution:** This plan is executed **serially in the current Claude Code chat**, task by task, in the order below. No subagent, parallel dispatch, or worktree isolation is authorized; do not delegate any task unless the user explicitly authorizes delegation later. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the independently testable Poiyomi opaque-conversion core: decide whether an attested unlocked pinned Poiyomi Toon 9.3.64 material may be normalized to its canonical Opaque counterpart, and if so produce a transient validated clone carrying the complete version-pinned canonical Opaque tuple.

**Architecture:** One new static class, `PoiyomiOpaqueConversion`, holding the version-pinned recipe as data, a conversion-only `MaterialEvidenceRequest`, a conversion-owned source-evidence wrapper, a pure eligibility evaluator over already-captured evidence plus two effective render-state facts, and transient clone preparation with read-back validation. Source attestation reuses the existing Poiyomi identity implementation through the already-parameterized `GatherSourceEvidence` core. No consumer is wired, no admitted-evidence handoff is built, and no abstraction is introduced over either.

**Tech Stack:** Unity 2022.3.22f1, C#, NUnit EditMode tests, existing `Alrauna.Amuse.Editor` and `Alrauna.Amuse.Tests.Editor` assemblies. No new dependency, assembly, or package metadata change.

**Spec:** `docs/superpowers/specs/2026-08-27-render-state-opaque-conversion-design.md` — authoritative. Committed at `adb72e1`; its §5.2 amendment recording that `GatherSourceEvidence` becomes assembly-internal is committed alongside this plan.

**Plan status:** approved and committed. Implementation changes it produces are left **uncommitted and unstaged** for controller review.

## Global Constraints

### Correctness invariants

- The canonical recipe is the **23 properties** of spec §1.2 plus effective queue `2000` plus the `RenderType` override tag `Opaque` = **25 canonical facts**.
- The conversion evidence request declares **24 presence names** (23 recipe + `_EnableOutlines`) and **25 scalar names** (those 24 + `_ShaderOptimizerEnabled`).
- `_AddBlendOp` and `_AddBlendOpAlpha` stay **outside** conversion evidence, schema, eligibility, and relevance. The recipe never writes them, so the unchanged blend operation cancels once the factors are proven equivalent at α = 1 (spec §5.4).
- `AlreadyOpaque` is determined from the 25 canonical facts **before** any transformation-only gate (spec §5.3 step 2).
- `_EnableOutlines` may differ on an `AlreadyOpaque` material: it is preserved and no mutation occurs. It is the **only** conversion-read property outside the 25 canonical facts, so it is the only gate a canonical material can fail (spec §5.5).
- A **non**-canonical material with `_Cutoff > 1` must refuse `ClipThresholdDiscardsOpaqueAlpha`. `_Cutoff`'s canonical value is `0`, so `_Cutoff > 1` and "all 25 facts match" are mutually exclusive.
- Enabled outlines refuse conversion. Do not model outline alpha.
- `_ZTest` must already be `4` (LEqual); conversion requires it rather than normalizing to it.
- ForwardAdd factors: `_AddSrcBlend ∈ {1, 5}`, `_AddDstBlend == 1`. The operation is not constrained.
- The source material is never written. Preparation uses `new Material(source)`.
- Read-back disagreement is an **invariant failure**: destroy the clone, then throw. It is not a refusal, and there is no `try`/`catch` around any Unity call.

### Scope boundaries — do not implement

- No production consumer and no production caller of `PoiyomiOpaqueConversion`.
- No admitted-evidence handoff (spec §7.1). It is a recorded prerequisite and an open controller decision.
- No change to `CapturedAlphaMaterial`, `SlotResolutionResult`, `AdmittedMaterialStates`, or `MaterialSemantics`.
- No alpha-separation mesh or submesh work; no `MeshSeparationPlan` change.
- No NDMF generated-asset integration; no `IAssetSaver` use; no `AmuseBuildOperation` wiring.
- No texture-evidence work; no outline-alpha modelling.
- No lilToon render-state or conversion work.
- No generic render-state IR, conversion interface, provider framework, shader registry, pass abstraction, planner, or mutation IR.
- No animation runner, binding list, additive-layer flag, or conversion-specific admission pipeline.
- **No dependency injection, delegate seam, or test-only hook** added to force an otherwise-unreachable branch.
- **No characterization probe.** The fixture uses the vendor-faithful `Range(0, 1.001)`; `1.0` and `1.001` are constructible inside it; larger and non-finite evaluator inputs are supplied through captured evidence (see "Out-of-range evaluator inputs" below); preparation writes `_Cutoff = 0`. No Unity write-coercion question affects any implementation decision here.

### Out-of-range evaluator inputs

`EvaluateVerifiedEligibility` is pure over `CapturedMaterialEvidence`, so its inputs need not be reachable by writing a live `Material`. Use the existing evidence primitive:

```csharp
var evidence = Capture(material, PoiyomiOpaqueConversion.ConversionEvidenceRequest);
var probed   = evidence.WithScalar("_Cutoff", float.NaN);   // or 2f, +∞, −∞
```

`CapturedMaterialEvidence.WithScalar` is documented as a primitive that applies no admission policy and writes whatever value it is given while preserving presence, which is exactly what these fixtures need. This is an existing seam, not a new one.

### Process

- Do not commit, push, or open a PR without separate authorization. Do not touch `feat/alpha-separation-vertical-slice`.
- Before every reported Unity test result, discover instances read-only and select only the instance whose normalized, case-exact `Application.dataPath` equals the normalized `<repo-root>/Assets`.
- Never use or modify the Census Lab for this plan. The vendor source facts are already captured in the spec; do not re-read vendor source to implement.
- Inspect the complete `Packages/manifest.json` and `Packages/packages-lock.json` diff before restoring any Unity-generated host toolchain churn. Restore only when the entire relevant diff is exactly that machine-generated state.
- Each new `.cs` file must be committed with its Unity-generated `.meta`. Treat asset and `.meta` as one unit.

## File map

| File | Change | Responsibility |
| --- | --- | --- |
| `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs` | **Add** | Canonical recipe data, conversion evidence request and schema, conversion source-evidence wrapper, effective render-state reading, canonical-fact comparison, pure eligibility evaluation, clone preparation and validation. |
| `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` | Modify | Single accessibility change: `GatherSourceEvidence` from `private` to `internal`. No behavior change. |
| `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiSemanticTest.shader` | Modify | Declare the 24 conversion-read properties with the vendor's exact types, defaults and ranges, plus `_AddBlendOp` for the non-dependency assertion. |
| `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs` | **Add** | The whole conversion test surface: recipe independence and counts, attestation, `AlreadyOpaque` ordering, transformation gates, boundaries, relevance isolation, preparation, per-field falsifiability, clone hygiene. |
| `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Characterization/IrrelevantChangeInvarianceTests.cs` | Modify | Extend the existing `IrrelevantFloats` set to the newly declared render-state properties. |

**Counts:** 5 files touched — **2 added** (`PoiyomiOpaqueConversion.cs`, `PoiyomiOpaqueConversionTests.cs`), 3 modified. Plus **2** Unity-generated `.meta` files, one per new `.cs`. Total expected `git status` entries at completion: **7**. No other new file, asset, or package metadata change is planned.

### One test file, deliberately

All conversion tests live in `PoiyomiOpaqueConversionTests.cs`. Splitting evaluation from preparation would force a shared test-data abstraction to carry the expected canonical tuple across two files. Keeping them together lets the single literal `ExpectedCanonicalTuple` drive both the recipe-independence assertions and the per-field falsifiability loop directly, which is the point of the independence rule in spec §8.1.

### Why `GatherSourceEvidence` becomes `internal`

Spec §5.2 requires conversion attestation to close over its own request rather than run the alpha capture path. The existing core is already parameterized for exactly this:

```csharp
internal static PoiyomiSourceEvidence GatherAlphaSourceEvidence(
    Shader shader, CapturedMaterialEvidence evidence)
{
    return GatherSourceEvidence(shader, evidence, AlphaRequiredSchemaProperties);
}

private static PoiyomiSourceEvidence GatherSourceEvidence(
    Shader shader,
    CapturedMaterialEvidence evidence,
    IReadOnlyCollection<string> requiredSchemaProperties)
```

Widening one keyword lets the conversion-owned wrapper pass its own schema array while `PoiyomiOpaqueConversion` owns that array. Body, signature and every existing caller stay unchanged. **Do not** duplicate hashing, GUID lookup, package checks, locked-state gathering, or the identity conjunction; `TryVerifyPoiyomiIdentity` is reused unchanged.

## Test execution

Focused run:

```text
mode: EditMode
test_names: Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiOpaqueConversionTests
include_failed_tests: true
```

All Poiyomi and characterization tests (the fixture shader's blast radius):

```text
mode: EditMode
test_names:
  Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiOpaqueConversionTests
  Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiMaterialSemanticsTests
  Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiBaseColorAlphaTests
  Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiAlphaMaskTests
  Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiEmissionTests
  Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiNormalTests
  Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiAdversarialTests
  Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiTextureEvidenceTests
  Alrauna.Amuse.Tests.Editor.Semantics.Characterization.PoiyomiIrrelevantChangeInvarianceTests
include_failed_tests: true
```

Final run: the complete public EditMode suite with no filter, plus a Console inspection for unexpected errors.

---

## Task 1: Extend the fixture shader to the conversion schema

**Files:**

- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiSemanticTest.shader`
- Add: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`

**Interfaces:** none yet — fixture plus one schema assertion.

- [ ] **Step 1: Add the failing schema test**

Create `PoiyomiOpaqueConversionTests.cs` in namespace `Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi`, deriving from `PoiyomiFixtureTestBase` (which already owns `NewFixtureMaterial()`, `Track<T>`, `ImportTexture`, and temp-folder teardown).

One test asserting the fixture material has every one of the 24 conversion-read property names, stated **literally in the test** (not read from production):

```csharp
private static readonly string[] ExpectedConversionSchema =
{
    "_Mode", "_AlphaForceOpaque", "_BlendOp", "_BlendOpAlpha", "_Cutoff",
    "_SrcBlend", "_DstBlend", "_SrcBlendAlpha", "_DstBlendAlpha",
    "_AddSrcBlend", "_AddDstBlend", "_AddSrcBlendAlpha", "_AddDstBlendAlpha",
    "_AlphaToCoverage", "_ZWrite", "_ZTest", "_AlphaPremultiply",
    "_OutlineSrcBlend", "_OutlineDstBlend", "_OutlineSrcBlendAlpha",
    "_OutlineDstBlendAlpha", "_OutlineBlendOp", "_OutlineBlendOpAlpha",
    "_EnableOutlines",
};
```

Assert `ExpectedConversionSchema.Length == 24` and `material.HasProperty(name)` for each.

**Expected RED:** the test fails on the first missing property. The fixture currently declares none of `_BlendOp`, `_BlendOpAlpha`, `_SrcBlendAlpha`, `_DstBlendAlpha`, the four `_Add*` factors, `_ZWrite`, `_ZTest`, `_EnableOutlines`, or the six `_Outline*` fields — verified: `grep -cE "_BlendOp|_ZWrite|_ZTest|_EnableOutlines|_Outline|_AddSrcBlend"` returns `0`.

- [ ] **Step 2: Minimum fixture change**

Add the missing declarations to the shader's `Properties` block with the **vendor's exact types, defaults and ranges**, and change the existing `_Cutoff` line:

```
_Cutoff ("Alpha Cutoff", Range(0, 1.001)) = 0.5
```

That range is vendor-faithful and is what makes the `1.0` and `1.001` boundary cases constructible on a live material.

Also declare `_AddBlendOp ("RGB Blend Op", Int) = 4` — the vendor's default — **only** so Task 6 can vary it and assert it changes nothing. It is not a conversion-read property and must not appear in `ExpectedConversionSchema`.

Extend the existing "Render-state properties the normalized Alpha equation deliberately IGNORES" comment block rather than starting a new section, and record there that `_Cutoff`'s upper bound is `1.001` in the vendor source and why that matters.

Do not touch the `SubShader`, the trivial unlit pass, or any existing property's name or default other than `_Cutoff`'s range.

- [ ] **Step 3: Validate no regression**

The fixture is shared. Run the **full Poiyomi and characterization set** and confirm every pre-existing test still passes. Adding properties cannot change a named-property read, and the only in-place edit widens a range no existing test exceeds (`PoiyomiBaseColorAlphaTests` writes `0.3`; `UnityMaterialEvidenceCaptureTests` writes `0.25`).

---

## Task 2: Canonical recipe constants and the conversion evidence request

**Files:**

- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`
- Add: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`

**Interfaces:**

- Consumes: `MaterialEvidenceRequest`, `TexturePropertyEvidenceRequest` (empty).
- Produces:

```csharp
internal static class PoiyomiOpaqueConversion
{
    internal const int    CanonicalOpaqueRenderQueue = 2000;
    internal const string RenderTypeTagName          = "RenderType";
    internal const string CanonicalOpaqueRenderType  = "Opaque";

    internal static IReadOnlyList<(string Property, float Value)>
        CanonicalOpaqueProperties { get; }

    internal static IReadOnlyCollection<string> ConversionRequiredSchemaProperties { get; }

    internal static MaterialEvidenceRequest ConversionEvidenceRequest { get; }
}
```

- [ ] **Step 1: Add the failing independence test**

Per spec §8.1, the test states the expected tuple **literally**, transcribed from spec §1.2, and asserts production matches it. It must not derive its expectation from `CanonicalOpaqueProperties`.

```csharp
private static readonly (string Property, float Value)[] ExpectedCanonicalTuple =
{
    ("_Mode", 0f),                    ("_AlphaForceOpaque", 1f),
    ("_BlendOp", 0f),                 ("_BlendOpAlpha", 4f),
    ("_Cutoff", 0f),                  ("_SrcBlend", 1f),
    ("_DstBlend", 0f),                ("_SrcBlendAlpha", 1f),
    ("_DstBlendAlpha", 1f),           ("_AddSrcBlend", 1f),
    ("_AddDstBlend", 1f),             ("_AddSrcBlendAlpha", 0f),
    ("_AddDstBlendAlpha", 1f),        ("_AlphaToCoverage", 0f),
    ("_ZWrite", 1f),                  ("_ZTest", 4f),
    ("_AlphaPremultiply", 0f),        ("_OutlineSrcBlend", 1f),
    ("_OutlineDstBlend", 0f),         ("_OutlineSrcBlendAlpha", 1f),
    ("_OutlineDstBlendAlpha", 0f),    ("_OutlineBlendOp", 0f),
    ("_OutlineBlendOpAlpha", 4f),
};
```

Tests:

- `ExpectedCanonicalTuple.Length == 23` and `CanonicalOpaqueProperties.Count == 23`.
- The two sets are equal as (name, value) pairs, order-insensitive.
- `CanonicalOpaqueRenderQueue == 2000`, `CanonicalOpaqueRenderType == "Opaque"`.
- `ConversionRequiredSchemaProperties.Count == 24` and equals `ExpectedConversionSchema` from Task 1.
- `ConversionEvidenceRequest.ScalarProperties.Count == 25`, contains `_ShaderOptimizerEnabled`, and **does not contain** `_AddBlendOp` or `_AddBlendOpAlpha`.
- `ConversionEvidenceRequest.ShaderName` is `true`; colour, vector and texture collections are empty.

**Expected RED:** compile failure — `PoiyomiOpaqueConversion` does not exist.

- [ ] **Step 2: Minimum implementation**

Create `PoiyomiOpaqueConversion.cs` in namespace `Alrauna.Amuse.Editor.Semantics.Poiyomi`. Declare the 23 pairs once as a `static readonly` array exposed through a `ReadOnlyCollection`, the 24-name schema array, and the request:

```csharp
ConversionEvidenceRequest = new MaterialEvidenceRequest(
    shaderName: true,
    activeColorSpace: false,
    presenceProperties: ConversionRequiredSchemaProperties,
    scalarProperties: <the 24 + "_ShaderOptimizerEnabled">,
    colorProperties: Array.Empty<string>(),
    vectorProperties: Array.Empty<string>(),
    textureProperties: Array.Empty<TexturePropertyEvidenceRequest>());
```

Derive the scalar list from the schema array plus one constant rather than retyping 25 names, so the two cannot drift. `MaterialEvidenceRequest`'s constructor already rejects duplicates within a category and across typed categories, so a transcription slip fails at construction.

Add a file header comment naming `PoiyomiMaterialSemantics.CanonicalNormalizedSourceHash` as the source the tuple was derived from, per spec §9.

- [ ] **Step 3: Validate.** Focused run. The test class compiles; the recipe tests pass.

---

## Task 3: Conversion source evidence, closed independently

**Files:**

- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`

**Interfaces:**

- Consumes: `PoiyomiMaterialSemantics.GatherSourceEvidence(Shader, CapturedMaterialEvidence, IReadOnlyCollection<string>)` — accessibility widened here; `PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(in PoiyomiSourceEvidence, out PoiyomiSemanticDiagnostic)` — unchanged.
- Produces:

```csharp
/// Narrow conversion entry to the shared Poiyomi source-evidence gatherer.
/// Verification stays PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity.
internal static PoiyomiSourceEvidence GatherConversionSourceEvidence(
    Shader shader,
    CapturedMaterialEvidence evidence);
```

It takes **already-captured** evidence. It does not accept a `Material`, does not capture, and must never recapture live state.

- [ ] **Step 1: Add the failing evidence tests**

These conversion source-evidence tests capture using `ConversionEvidenceRequest` **alone**, and must not use or combine `PoiyomiMaterialSemantics.AlphaEvidenceRequest` to make conversion attestation succeed — that would defeat the closure this task exists to prove. (Task 8 may reference both requests, solely to prove relevance isolation and non-widening.) Assert on the **gathered evidence fields**, which is what this wrapper is responsible for:

- Fixture material → `GatherConversionSourceEvidence(...).HasRequiredSchema` is `true`, proving the conversion schema is satisfied by the extended fixture and that the wrapper passes its own array.
- Fixture material with `_ShaderOptimizerEnabled` set non-zero → gathered `IsLocked` is `true`, proving the conversion request carries what locked-state gathering reads.
- A material whose shader lacks the conversion schema (any simple built-in shader) → gathered `HasRequiredSchema` is `false`.
- The gathered fixture evidence can be handed to the existing `TryVerifyPoiyomiIdentity`, which returns `false` with a diagnostic — an **expected identity failure**, because the public fixture is not the pinned vendor shader.

Do **not** assert which diagnostic the verifier emits or in what order. The public fixture fails on shader name and hash before reaching the locked or schema branches, so asserting that ordering here would encode an impossible expectation. The verifier's diagnostic ordering is already covered by the existing identity-verification tests; do not duplicate them.

**Expected RED:** compile failure — `GatherConversionSourceEvidence` does not exist; `GatherSourceEvidence` is inaccessible from `PoiyomiOpaqueConversion`.

- [ ] **Step 2: Minimum implementation**

Change exactly one keyword in `PoiyomiMaterialSemantics.cs`:

```diff
-        private static PoiyomiSourceEvidence GatherSourceEvidence(
+        internal static PoiyomiSourceEvidence GatherSourceEvidence(
```

Leave `GatherAlphaSourceEvidence` and every existing caller untouched. Then add the wrapper to `PoiyomiOpaqueConversion`:

```csharp
internal static PoiyomiSourceEvidence GatherConversionSourceEvidence(
    Shader shader, CapturedMaterialEvidence evidence)
{
    return PoiyomiMaterialSemantics.GatherSourceEvidence(
        shader, evidence, ConversionRequiredSchemaProperties);
}
```

No hashing, GUID lookup, package check, locked-state gathering, or identity logic is written here.

- [ ] **Step 3: Validate.** Focused run plus the full Poiyomi set — the accessibility change must not alter any existing alpha attestation result.

---

## Task 4: Effective render state and the canonical-fact comparison

**Files:**

- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`

**Interfaces:**

```csharp
internal static void ReadEffectiveRenderState(
    Material material, out int renderQueue, out string renderType);

/// First canonical fact the candidate disagrees with, or false when all 25 match.
internal static bool TryFindNonCanonicalFact(
    Material candidate, out string factName);
```

- [ ] **Step 1: Add the failing comparison tests**

- A material with every canonical value applied by hand, `renderQueue = 2000`, and `SetOverrideTag("RenderType", "Opaque")` → `TryFindNonCanonicalFact` is `false`.
- `ReadEffectiveRenderState` on a material with no queue override reports the shader's declared queue rather than `-1`.

Per-field falsifiability arrives in Task 7, where a *prepared* clone is perturbed. Keeping it there avoids asserting the detector against a hand-built material that production never produces.

**Expected RED:** compile failure — neither member exists.

- [ ] **Step 2: Minimum implementation**

`ReadEffectiveRenderState` reads `material.renderQueue` and `material.GetTag(RenderTypeTagName, false)`. `TryFindNonCanonicalFact` compares the 23 properties with exact `==` against the recipe, then queue, then tag ordinally, reporting the first disagreement in a deterministic order: recipe order, then `"renderQueue"`, then `"RenderType"`. A property the material does not have is reported by name as a disagreement.

- [ ] **Step 3: Validate.** Focused run.

---

## Task 5: `AlreadyOpaque` classified before the transformation gates

**Files:**

- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`

**Interfaces:**

```csharp
internal enum PoiyomiOpaqueConversionOutcome { Refused, AlreadyOpaque, Convertible }

internal enum PoiyomiOpaqueConversionRefusal
{
    None,
    UnattestedMaterial,
    ConversionPropertyAbsent,
    ConversionPropertyNotFinite,
    OutlinesEnabled,
    PremultipliedAlphaEnabled,
    AlphaToCoverageEnabled,
    UnsupportedDepthComparison,
    UnsupportedBlendEquation,
    UnsupportedForwardAddBlendEquation,
    ClipThresholdDiscardsOpaqueAlpha,
}

internal readonly struct PoiyomiOpaqueConversionEligibility
{
    internal PoiyomiOpaqueConversionOutcome Outcome { get; }
    internal PoiyomiOpaqueConversionRefusal Refusal { get; }
}

internal static PoiyomiOpaqueConversionEligibility EvaluateVerifiedEligibility(
    CapturedMaterialEvidence evidence,
    int effectiveRenderQueue,
    string effectiveRenderType);
```

This is the same identity-gate/interpretation split the frontend already uses (`TryVerifyPoiyomiIdentity` gating `InterpretVerifiedAlpha` / `InterpretVerifiedMaterial`). It is not a new test-only seam.

- [ ] **Step 1: Add the failing ordering tests**

The regressions that fix the evaluation order, per spec §5.3 step 2 and §8.4:

- All 25 canonical facts matching, `_EnableOutlines = 1` → `AlreadyOpaque`.
- All 25 canonical facts matching, `_EnableOutlines` non-finite (via `WithScalar`) → `AlreadyOpaque`.
- All 25 canonical facts matching, `_EnableOutlines = 0` → `AlreadyOpaque`.
- **Contrast:** every canonical fact except `_Cutoff`, with `_Cutoff = 1.001` → `Refused` / `ClipThresholdDiscardsOpaqueAlpha`, **not** `AlreadyOpaque`, because `_Cutoff`'s canonical value is `0` so the comparison fails and evaluation proceeds.
- A property absent from the evidence → `ConversionPropertyAbsent`, checked before the canonical comparison.

**Expected RED:** compile failure — the types do not exist.

- [ ] **Step 2: Minimum implementation**

Implement only steps 1 and 2 of spec §5.3: readability of the 24, then the 25-fact comparison, returning `AlreadyOpaque` or falling through to a temporary `Convertible`. No gate yet. This deliberately leaves Task 6's tests failing.

Evaluate the 23 recipe properties from `evidence` and take queue and tag from the two parameters. `TryFindNonCanonicalFact` operates on a live `Material` and is used by preparation; the evaluator is pure over evidence and must not call it.

- [ ] **Step 3: Validate.** Focused run; ordering tests pass.

---

## Task 6: Transformation gates

**Files:**

- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`

**Interfaces:** no new members; `EvaluateVerifiedEligibility` gains steps 3–10.

- [ ] **Step 1: Add the failing gate tests**

Every fixture below is **otherwise non-canonical** so the row exercises its gate rather than the classification. State that precondition in a comment.

- **Finiteness**: any of the 24 non-finite, supplied via `WithScalar` → `ConversionPropertyNotFinite`.
- **Outlines** (spec §8.4, non-canonical half): `_EnableOutlines = 1` → `OutlinesEnabled`; also `0.5`, `0.005`, `-1`. **And the case with every other fact eligible and base alpha exactly 1 still refuses** — a perfect base-alpha proof does not rescue enabled outlines. `_EnableOutlines = 0` on an otherwise eligible material → `Convertible`.
- **Premultiply / coverage**: `_AlphaPremultiply = 1` → `PremultipliedAlphaEnabled`; `_AlphaToCoverage = 1` → `AlphaToCoverageEnabled`.
- **Depth**: `_ZTest ∈ {0, 2, 3, 5, 6, 7, 8}` → `UnsupportedDepthComparison`; `4` passes.
- **Base blend**: `_BlendOp = 0`, `_SrcBlend ∈ {1, 5}`, `_DstBlend ∈ {0, 10}` pass; others → `UnsupportedBlendEquation`.
- **ForwardAdd factors**: `_AddSrcBlend ∈ {1, 5}` pass, others refuse; `_AddDstBlend = 1` passes, `{0, 7, 10}` refuse — both → `UnsupportedForwardAddBlendEquation`.
- **`_AddBlendOp` non-dependency** (spec §5.4): a material eligible with `_AddBlendOp = 0` is **equally eligible** with `4` (the vendor default) and with every other value. Asserting this pins the non-dependency, so reintroducing a gate fails a test.
- **Cutoff boundary** (spec §8.7), on a Fade-derived otherwise-non-canonical fixture: `0`, `0.5`, `1.0`, `-0.5` → `Convertible`; `1.001` → `ClipThresholdDiscardsOpaqueAlpha` (settable directly on the fixture material); `2.0` and `NaN`, `±∞` supplied via `WithScalar` → `ClipThresholdDiscardsOpaqueAlpha` and `ConversionPropertyNotFinite` respectively.
- **Preset table** (spec §8.5): all nine preset tuples, each built with `_EnableOutlines = 0`, `_ZTest = 4` and canonical ForwardAdd factors. Opaque → `AlreadyOpaque`; Cutout, TransClipping, Fade → `Convertible`; Transparent → `PremultipliedAlphaEnabled`; Additive, SoftAdditive, Multiplicative, 2xMultiplicative → `UnsupportedBlendEquation`.
- **`_Mode` is not authoritative**: `_Mode = 0` with an Additive base blend refuses; `_Mode = 4` with One/Zero, `_ZTest = 4`, outlines off, premultiply off → `Convertible`.

- [ ] **Step 2: Minimum implementation**

Add steps 3–10 in spec order, first failure wins. Straight-line comparisons; no table abstraction, no predicate objects, no pass model.

- [ ] **Step 3: Validate.** Focused run. Confirm the ordering tests from Task 5 still pass — adding gates must not disturb the `AlreadyOpaque` short-circuit.

---

## Task 7: Clone preparation and invariant validation

**Files:**

- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`

**Interfaces:**

```csharp
/// Clones, applies the canonical tuple, re-reads and validates. Destroys the
/// clone and throws InvalidOperationException naming the first disagreeing
/// fact. Never returns a partially prepared material.
internal static Material PrepareCanonicalOpaqueClone(Material source);
```

Its precondition is an **attested and eligible** source. Do not write a test that violates that precondition to force the throw branch, and do not add a seam to inject a failure.

- [ ] **Step 1: Add the failing preparation tests**

- Preparing from a non-canonical fixture yields a material where **all 25 canonical facts** read back canonical, asserted against `ExpectedCanonicalTuple` plus queue and tag.
- **Per-field falsifiability**: for each of the 25 canonical facts in turn, perturb exactly that one **on an already-prepared clone** and assert `TryFindNonCanonicalFact` is `true` **and names that fact**. Driven directly by `ExpectedCanonicalTuple` plus two rows for queue and tag, which is why both live in this one file.
- **Source immutability**: snapshot every source property before and assert unchanged after, for the prepared path and for a refused evaluation.
- The clone is not reference-equal to the source and shares `source.shader`.
- **Clone persistence**: `AssetDatabase.Contains(clone)` is `false` and `AssetDatabase.GetAssetPath(clone)` is empty — nothing was persisted.

- [ ] **Step 2: Minimum implementation**

```csharp
var clone = new Material(source);
foreach (var (property, value) in CanonicalOpaqueProperties) clone.SetFloat(property, value);
clone.renderQueue = CanonicalOpaqueRenderQueue;
clone.SetOverrideTag(RenderTypeTagName, CanonicalOpaqueRenderType);

if (TryFindNonCanonicalFact(clone, out var fact) || clone.shader != source.shader)
{
    Object.DestroyImmediate(clone);
    throw new InvalidOperationException(/* names `fact` */);
}
return clone;
```

The destruction-plus-throw branch is implemented because the invariant must hold, not because a test forces it. No `try`/`catch`. No naming of the clone — spec §6.1 leaves naming to the consumer; add a comment recording that handoff obligation.

`PrepareCanonicalOpaqueClone` takes no asset saver. That, the compiled dependency set, and the clone-persistence assertions above are the protection against accidental persistence; **do not** add a test that scans production source text for `IAssetSaver`, `AssetDatabase.Create` or `AddObjectToAsset`.

- [ ] **Step 3: Validate.** Focused run, and confirm the temp folder teardown leaves no stray material.

---

## Task 8: Relevance isolation and the semantics invariance extension

**Files:**

- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Characterization/IrrelevantChangeInvarianceTests.cs`

**Interfaces:**

- Consumes: `UnityAnimationEvidenceCapture.ResolveProofRelevant(CapturedFloatBinding, string, MaterialEvidenceRequest, out AnimatedPropertyRef)` and `ProofRelevantBindingResolution`. Pure functions of a binding and a request — no renderer, no graph, no admission.

- [ ] **Step 1: Add the isolation tests**

For a `CapturedFloatBinding` on `material._ZWrite`, and likewise `material._EnableOutlines`:

- under `PoiyomiMaterialSemantics.AlphaEvidenceRequest` → `Irrelevant` (ordinary alpha analysis untouched);
- under `PoiyomiOpaqueConversion.ConversionEvidenceRequest` → `RendererWide`.

For `material._AddBlendOp` → `Irrelevant` under **both** requests (spec §5.4).

This is the one place the test file names `AlphaEvidenceRequest`, and it does so to prove non-widening rather than to attest anything.

- [ ] **Step 2: Extend the existing invariance set**

`PoiyomiIrrelevantChangeInvarianceTests.IrrelevantFloats` currently pins `_Mode`, `_Cutoff`, `_SrcBlend`, `_DstBlend` as not changing any semantic output. Add the newly declared render-state properties, including `_EnableOutlines` and `_AddBlendOp`.

This is the cheapest existing expression of spec §3.3 and §8.9's coverage half — unknown conversion-relevant state refuses only conversion — at the semantics layer. It adds no new file and no new concept; it widens an invariance the repository already asserts. **Do not** add any other assertion to that file.

- [ ] **Step 3: Validate.** Full Poiyomi and characterization set.

---

## Task 9: Completion sweep

- [ ] **Step 1:** Run the complete public EditMode suite with no filter. Inspect the Console for unexpected errors.
- [ ] **Step 2:** Confirm source assets are unchanged and `git status` shows exactly the expected **7** entries: 5 touched files plus 2 generated `.meta` files, with no host churn in `Packages/manifest.json` or `packages-lock.json`.
- [ ] **Step 3:** Inspect staged and unstaged diffs separately; run `git diff --check`.
- [ ] **Step 4:** Adversarial self-review against the Global Constraints, specifically: no production caller exists; `_AddBlendOp` appears in production only inside the §5.4 comment; `AlreadyOpaque` precedes every gate; no `IAssetSaver`; no injected seam; no change to `CapturedAlphaMaterial`, `SlotResolutionResult`, `AdmittedMaterialStates`, or `MaterialSemantics`.
- [ ] **Step 5:** Report changed files, observed validation, skipped validation and why, remaining unsupported cases, deferred architectural pressure, and whether Census Lab was used or modified (it must not be). Stop for controller review without committing.
