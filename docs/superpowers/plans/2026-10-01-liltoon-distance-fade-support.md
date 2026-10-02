# lilToon Distance Fade Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Support the vendor distance fade feature on every supported
lilToon and lilToonMulti identity: a finite nonzero strength retains the
material by named rule instead of reporting an unsupported feature, and the
cutout family gains the capture and gates it lacks.

**Architecture:** One shared strength evaluator feeds both lilToon alpha
frontends. Retention rides the existing Unknown path as a new named kind,
carried through the diagnostic code, the `AlphaUnknownReason`, the slot
refusal value, and the report sentence. Conversion eligibility keeps
refusing with `UnsupportedDistanceFade` and gains the cutout mirror.

**Tech Stack:** C# against Unity 2022.3 APIs, NUnit through the Unity Test
Runner, EditMode only. Compilation happens inside Unity. There is no dotnet
build and no CLI test path.

**Spec:** `docs/superpowers/specs/2026-10-01-liltoon-distance-fade-support-design.md`

## Global Constraints

- Vendor pin: lilToon tag `2.3.4`, commit
  `252fd8cfc46106d4967e95b3f2c788418502f227`. Do not re-derive vendor facts.
- Tests run in the Unity Test Runner, EditMode, on the dev editor instance
  of this repository. Before any Unity tool call, verify
  `Application.dataPath == <repo-root>/Assets`.
- A filtered run that reports 0 tests is a failure. Record observed pass
  and fail counts for every run.
- No staging or committing without explicit user authorization in the
  session. The commit steps below run only once authorized.
- Documents and comments use simple English: short active sentences, no
  semicolons, no contractions, no machine paths, no private identifiers.
- All production types stay `internal`. File name equals type name. One
  public type per file.
- Never weaken a valid test. The non-finite and zero-strength tests must
  keep passing unchanged.
- Scope boundary: no Poiyomi change, no opaque-family change, no
  `_DistanceFadeColor` capture, no Census Lab work. Census Lab validation
  is a separate authorized activity and a stop condition here.

---

### Task 1: Baseline test run

**Files:**
- None. This task only observes.

**Interfaces:**
- Produces: the baseline failure set of both test assemblies on the base
  commit, recorded in the task report.

- [ ] **Step 1: Run the product assembly**

Run the full `Alrauna.Amuse.Tests.Editor` assembly in the Unity Test
Runner, EditMode. Record the observed pass and fail counts. List every
failing test name.

- [ ] **Step 2: Run the research assembly**

Run the full `Alrauna.Amuse.Research.Tests.Editor` assembly, EditMode.
Record the observed pass and fail counts.

- [ ] **Step 3: Record the baseline**

Keep the failure set. Every later task compares against it. A failure that
is not in the baseline is a regression from that task.

---

### Task 2: Retention vocabulary

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/AlphaUnknownReason.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs`

**Interfaces:**
- Produces: `LilToonSemanticDiagnosticCode.FeatureRetention`,
  `AlphaUnknownKind.FeatureRetention`,
  `AlphaUnknownReason.FeatureRetention(string feature, string property)`,
  `RendererAnalysisRefusal.AdmittedMaterialRetainedByFeature`. Later tasks
  record diagnostics with the new code and the resolution maps the kind to
  the new refusal value.

- [ ] **Step 1: Write the failing completeness test**

In `Tests/Editor/Build/AmuseReportStringsTests.cs`, find the test that
iterates one `AlphaUnknownReason` per kind and asserts
`AmuseReports.FeatureSentence(reason)` is not empty (the assertion at
`:57-61`). Add one entry to its reason collection:

```csharp
AlphaUnknownReason.FeatureRetention("Distance fade", "_DistanceFade"),
```

- [ ] **Step 2: Run the test to verify it fails**

Run `AmuseReportStringsTests` in the Unity Test Runner, EditMode.
Expected: compile error, because `AlphaUnknownKind.FeatureRetention` and
the factory do not exist yet. That compile failure is the RED state.

- [ ] **Step 3: Add the diagnostic code**

In `LilToonMaterialSemantics.cs`, append to
`LilToonSemanticDiagnosticCode` after `UnsupportedTextureImport`:

```csharp
/// <summary>
/// A supported vendor feature is active, and the rule for it is
/// retention, not refusal. The property still names the fact, and the
/// alpha answer stays Unknown because no triangle can move.
/// </summary>
FeatureRetention,
```

- [ ] **Step 4: Add the reason kind and factory**

In `AlphaUnknownReason.cs`, append to `AlphaUnknownKind` after
`UnsupportedMultiState`:

```csharp
/// <summary>
/// A supported vendor feature is active and the family retains the
/// material by rule. <see cref="AlphaUnknownReason.Feature"/> names the
/// feature in words and <see cref="AlphaUnknownReason.Property"/> names
/// the exact shader property, exactly like UnsupportedFeature. The
/// slot refusal value differs: retention, not missing support.
/// </summary>
FeatureRetention,
```

Extend the `Feature` and `Property` doc comments: each stays non-null for
`UnsupportedFeature`, `UnsupportedMultiState`, and `FeatureRetention`.
Add the factory beside `UnsupportedFeature`:

```csharp
internal static AlphaUnknownReason FeatureRetention(
    string feature, string property)
{
    return new AlphaUnknownReason(
        AlphaUnknownKind.FeatureRetention, feature, property, null);
}
```

- [ ] **Step 5: Branch the reason mapping**

In `LilToonMaterialSemantics.cs`, replace the return inside
`AlphaUnknownReasonFor` (`:565-566`) with:

```csharp
return diagnostic.Code == LilToonSemanticDiagnosticCode.FeatureRetention
    ? AlphaUnknownReason.FeatureRetention(
          FeatureLabelFor(diagnostic.Detail), diagnostic.Detail)
    : AlphaUnknownReason.UnsupportedFeature(
          FeatureLabelFor(diagnostic.Detail), diagnostic.Detail);
```

- [ ] **Step 6: Add the refusal value and the resolution branch**

In `UnityRendererAlphaAnalysis.cs`, add after
`AdmittedMaterialSemanticsUnknown` (`:86`):

```csharp
/// <summary>
/// A supported vendor feature is active on an admitted material, and
/// the family's rule retains the material. This is an expected
/// outcome, not missing support. The report sentence names the
/// feature and the property.
/// </summary>
AdmittedMaterialRetainedByFeature,
```

In `AdmittedMaterialStates.cs`, inside `ResolveSlot`, replace the slot
refusal selection (the `LockedIdentityRefusal` conditional before the
`SlotResolutionResult.Refused` return) with:

```csharp
var slotRefusal =
    material.LockedIdentityRefusal != RendererAnalysisRefusal.None
        ? material.LockedIdentityRefusal
        : capturedSemantics.AlphaUnknownReason != null &&
          capturedSemantics.AlphaUnknownReason.Kind ==
              AlphaUnknownKind.FeatureRetention
            ? RendererAnalysisRefusal.AdmittedMaterialRetainedByFeature
            : RendererAnalysisRefusal.AdmittedMaterialSemanticsUnknown;
```

The locked-identity precedence keeps its place: it wins before retention,
exactly as it won before the generic value.

- [ ] **Step 7: Add the report sentence**

In `AmuseReports.cs`, add to the `FeatureSentence` switch:

```csharp
case AlphaUnknownKind.FeatureRetention:
    var retainedFeature = reason.Feature != null
        ? "the " + reason.Feature + " feature"
        : "a shader feature";
    var retainedProperty = reason.Property != null
        ? " (property " + reason.Property + ")"
        : "";
    return "AMUSE retains this material because " + retainedFeature +
        " is active" + retainedProperty + ". A moved triangle would " +
        "lose its distance fade, so no triangle moves.";
```

- [ ] **Step 8: Run the vocabulary tests**

Run `AmuseReportStringsTests` plus the full
`Alrauna.Amuse.Tests.Editor` assembly, EditMode. Expected: the
completeness test passes. Every other result matches the Task 1 baseline.

- [ ] **Step 9: Commit (only with session authorization)**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs \
  Packages/com.alrauna.amuse/Editor/Semantics/AlphaUnknownReason.cs \
  Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs \
  Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs \
  Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs
git commit -m "feat: name feature retention in the refusal vocabulary"
```

---

### Task 3: Cutout capture

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutConversionTest.shader`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonSemanticTest.shader`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: the cutout alpha evidence request carries the `_DistanceFade`
  vector, so Task 5's gate and Task 6's eligibility gates read captured
  facts. The shared property name constant lands in Task 4; this task uses
  the literal string, and Task 4 cuts the literal over.

- [ ] **Step 1: Update the capture shape test**

In `LilToonCutoutAlphaTests.cs`, the test that pins the capture request
shape asserts the vector list (`:114-119` area). Add `"_DistanceFade"` to
the expected vector array, after `"_MainTex_ScrollRotate"`:

```csharp
"_MainTex_ScrollRotate",
"_DistanceFade",
"_Main2ndTex_ScrollRotate",
```

- [ ] **Step 2: Run the shape test to verify it fails**

Run `LilToonCutoutAlphaTests`, EditMode. Expected: the shape test fails
because the request does not carry the vector yet. That is the RED state.

- [ ] **Step 3: Extend the evidence request**

In `LilToonCutoutMaterialSemantics.cs`, inside the
`AlphaEvidenceRequest` `vectorProperties` array (`:159-164`), insert
`"_DistanceFade",` after `"_MainTex_ScrollRotate",`. Keep the vendor
default order stable otherwise.

- [ ] **Step 4: Declare the property in the cutout fixtures**

Add the vendor default declaration to each fixture shader that captures
through the cutout request, beside the other vector declarations:

```shaderlabors
_DistanceFade ("DistanceFade", Vector) = (0.1,0.01,0,0)
```

Exact files: `LilToonCutoutConversionTest.shader` (beside the
`_Main2ndDistanceFade` block at `:50`), `LilToonSemanticTest.shader`
(beside `:56`), and the cutout builder string in
`LilToonFixtureTestBase.cs` (the `:237-244` region; the transparent
builder already carries the line at `:274-275`).

- [ ] **Step 5: Run the affected classes and repair capture fallout**

Run `LilToonCutoutAlphaTests`, then the full
`Alrauna.Amuse.Tests.Editor` assembly, EditMode. Expected: the shape test
passes. If any other test fails with a capture refusal naming
`_DistanceFade`, that test's fixture shader is missing the declaration:
add the same vendor default line there. Any other new failure is a
regression and stops the task.

- [ ] **Step 6: Commit (only with session authorization)**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/
git commit -m "feat: capture the distance fade vector for the cutout family"
```

---

### Task 4: Shared evaluator and the transparent gate flip

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonDistanceFadeSemantics.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs`

**Interfaces:**
- Consumes: `LilToonSemanticDiagnosticCode.FeatureRetention` and
  `AlphaUnknownKind.FeatureRetention` from Task 2.
- Produces: `LilToonDistanceFadeAnswer` with members `Absent`, `Inert`,
  `Retained`, `NonFinite`, and
  `LilToonDistanceFadeSemantics.Evaluate(CapturedMaterialEvidence)` plus
  `LilToonDistanceFadeSemantics.DistanceFadeProperty`. Task 5 and Task 6
  consume both.

- [ ] **Step 1: Flip the pinned transparent test**

In `LilToonTransparentAlphaTests.cs`, rename
`DistanceFadeEnabled_IsUnknownNamingDistanceFade` (`:1838-1849`) to
`DistanceFadeStrengthOn_RetainsNamingDistanceFade` and change the
expectation to the retention kind. Keep the falsifier comment: a gate on
`_DistanceFadeColor.a` instead of `_DistanceFade.z` must still fail this
test.

```csharp
[Test]
public void DistanceFadeStrengthOn_RetainsNamingDistanceFade()
{
    var material = NewGateOffMaterialWithOpaqueTexture("t_fade_on");
    material.SetVector(
        DistanceFadeProperty, new Vector4(0.1f, 0.01f, 0.5f, 0f));

    // Falsifies: omitting the only post-clip alpha writer, and gating
    // on _DistanceFadeColor.a instead of _DistanceFade.z.
    AssertAlphaGateUnknown(
        InterpretTransparent(material), DistanceFadeProperty);
}
```

Extend the local `AssertAlphaGateUnknown` helper, or add an overload, so
the retention kind passes it: the helper asserts the alpha answer is
Unknown and the reason names the property. The kind assertion moves into
this test:

```csharp
Assert.That(
    reason.Kind,
    Is.EqualTo(AlphaUnknownKind.FeatureRetention));
```

- [ ] **Step 2: Run the test to verify it fails**

Run `LilToonTransparentAlphaTests`, EditMode. Expected:
`DistanceFadeStrengthOn_RetainsNamingDistanceFade` fails because the gate
still records `UnsupportedFeature`. That is the RED state.

- [ ] **Step 3: Create the shared evaluator**

Create `LilToonDistanceFadeSemantics.cs`:

```csharp
using Alrauna.Amuse.Editor.Host;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>The outcome of the shared distance fade strength rule.
    /// </summary>
    internal enum LilToonDistanceFadeAnswer
    {
        /// <summary>The shader carries no such property.</summary>
        Absent,

        /// <summary>Finite vector with a zero strength: exact no-op.</summary>
        Inert,

        /// <summary>Finite vector with a nonzero strength.</summary>
        Retained,

        /// <summary>Any non-finite component.</summary>
        NonFinite,
    }

    /// <summary>
    /// The shared distance fade strength rule for every lilToon donor
    /// family. The vendor fragment block scales every arm by
    /// <c>_DistanceFade.z</c> (lil_common_frag.hlsl:2017-2064 at tag
    /// 2.3.4), so a finite zero strength is an exact no-op and a finite
    /// nonzero strength retains the material: the color arm runs at
    /// every render mode, so a moved triangle would lose its fade at
    /// some camera distance. A non-finite component is a broken capture
    /// and keeps the non-finite vocabulary.
    /// </summary>
    internal static class LilToonDistanceFadeSemantics
    {
        internal const string DistanceFadeProperty = "_DistanceFade";

        internal static LilToonDistanceFadeAnswer Evaluate(
            CapturedMaterialEvidence evidence)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            if (!evidence.TryGetVector(DistanceFadeProperty, out var fade))
            {
                return LilToonDistanceFadeAnswer.Absent;
            }

            if (float.IsNaN(fade.x) || float.IsInfinity(fade.x) ||
                float.IsNaN(fade.y) || float.IsInfinity(fade.y) ||
                float.IsNaN(fade.z) || float.IsInfinity(fade.z) ||
                float.IsNaN(fade.w) || float.IsInfinity(fade.w))
            {
                return LilToonDistanceFadeAnswer.NonFinite;
            }

            return fade.z != 0f
                ? LilToonDistanceFadeAnswer.Retained
                : LilToonDistanceFadeAnswer.Inert;
        }
    }
}
```

- [ ] **Step 4: Rewrite the transparent gate through the evaluator**

In `LilToonTransparentMaterialSemantics.cs`, replace the gate at
`:439-452` (absence, finiteness, and strength in one condition) with:

```csharp
// (7) The distance fade strength. The vendor block scales every arm
//     by the .z strength, so a finite zero strength is an exact
//     no-op. A finite nonzero strength retains the material: the
//     color arm runs at every render mode, so a moved triangle would
//     lose its fade. The gate is the .z strength component, not
//     _DistanceFadeColor.a: the two arms diverge, and only .z
//     disables the alpha write.
switch (LilToonDistanceFadeSemantics.Evaluate(evidence))
{
    case LilToonDistanceFadeAnswer.Inert:
        break;
    case LilToonDistanceFadeAnswer.Retained:
        return RecordUnknown<ScalarSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Alpha,
            LilToonSemanticDiagnosticCode.FeatureRetention,
            LilToonDistanceFadeSemantics.DistanceFadeProperty);
    default:
        // Absent and NonFinite keep today's refusal wording.
        return RecordUnknown<ScalarSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Alpha,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            LilToonDistanceFadeSemantics.DistanceFadeProperty);
}
```

Cut the private `DistanceFadeProperty` constant from this file and use
the shared constant everywhere it appeared (`:54`, `:189`, and this gate).

- [ ] **Step 5: Run the transparent class and the full assembly**

Run `LilToonTransparentAlphaTests`, then the full
`Alrauna.Amuse.Tests.Editor` assembly, EditMode. Expected: the renamed
test passes. `DistanceFadeNonFinite_IsUnknownNamingDistanceFade`,
`DistanceFadeDisabled_ProvesTheCornerTriangle`, and both eligibility
tests pass unchanged. Every other result matches the baseline.

- [ ] **Step 6: Commit (only with session authorization)**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonDistanceFadeSemantics.cs \
  Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs
git commit -m "feat: retain materials with an active distance fade strength"
```

---

### Task 5: Cutout semantics gate

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs`

**Interfaces:**
- Consumes: `LilToonDistanceFadeSemantics` from Task 4 and the capture
  from Task 3.
- Produces: cutout alpha answers retention for a finite nonzero strength.

- [ ] **Step 1: Write the failing cutout tests**

In `LilToonCutoutAlphaTests.cs`, add three tests beside the existing
alpha gate tests, reusing that file's material builder helpers exactly as
the transparent flips reuse theirs:

```csharp
// --- Falsifier: a cutout gate that refuses zero strength, that admits
// --- a nonzero strength, or that reports any kind but retention for a
// --- finite nonzero strength fails one of these fixtures. ---
[Test]
public void DistanceFadeStrengthOn_RetainsAtCutout()
{
    var material = NewGateOffMaterialWithOpaqueTexture("c_fade_on");
    material.SetVector(
        "_DistanceFade", new Vector4(0.1f, 0.01f, 0.5f, 0f));

    AssertAlphaGateUnknown(InterpretCutout(material), "_DistanceFade");
    Assert.That(
        InterpretCutoutWithReason(material).Kind,
        Is.EqualTo(AlphaUnknownKind.FeatureRetention));
}
```

For the kind assertion, extend the file's `InterpretCutout` helper
(`:709-712`) with one overload that passes the same evidence it already
builds to `InterpretVerifiedCutoutMaterial` and returns the out
`AlphaUnknownReason` (the out-parameter shape lives at
`LilToonCutoutMaterialSemantics.cs:228-241`):

```csharp
private static AlphaUnknownReason InterpretCutoutWithReason(
    Material material)
{
    // Build the evidence exactly as InterpretCutout does, then:
    LilToonCutoutMaterialSemantics
        .InterpretVerifiedCutoutMaterial(evidence, out var reason);
    return reason;
}
```

```csharp
[Test]
public void DistanceFadeStrengthZero_ProvesAtCutout()
{
    var material = NewGateOffMaterialWithOpaqueTexture("c_fade_off");

    // The shipped default (0.1, 0.01, 0, 0) has z == 0 and is inert.
    // Resolve and classify exactly as the transparent mirror does in
    // DistanceFadeDisabled_ProvesTheCornerTriangle
    // (LilToonTransparentAlphaTests.cs:1864-1876): the cutout class's
    // resolve, chain, and triangle helpers replace the transparent
    // ones.
    var resolution =
        ResolveThroughCutoutFrontend(material, AllOpaqueChain());

    Assert.That(
        resolution.Classify(CornerTriangle()),
        Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
}

[Test]
public void DistanceFadeNonFinite_UnknownNamesPropertyAtCutout()
{
    var material = NewGateOffMaterialWithOpaqueTexture("c_fade_nan");
    material.SetVector(
        "_DistanceFade", new Vector4(0.1f, 0.01f, 0f, float.NaN));

    AssertAlphaGateUnknown(
        InterpretCutout(material), "_DistanceFade");
}
```

Adapt the helper names to the file's real ones: the builder, the
interpreter entry point, and the gate assertion follow the local naming
that the transparent class mirrors. The zero-strength test is
characterization on the base code: it must pass before and after the
gate, and the plan records it as such, never as RED.

- [ ] **Step 2: Run the tests to verify they fail**

Run `LilToonCutoutAlphaTests`, EditMode. Expected: the retention test
fails because no cutout gate exists and the alpha proof completes. The
zero-strength test passes (characterization). The non-finite test fails
the same way. That is the RED state for the two failing tests.

- [ ] **Step 3: Insert the cutout gate**

In `LilToonCutoutMaterialSemantics.cs`, insert the gate in
`InterpretAlpha` after the `_Cutoff` gate (`:367-371`) and before the
`_Color` gate (`:373-386`), mirroring the transparent family's gate
order:

```csharp
// The distance fade strength. The vendor block scales every arm by
// the .z strength, so a finite zero strength is an exact no-op. At
// LIL_RENDER 1 the color arm still runs, and the dither path lerps
// alpha before the coverage transform, so a finite nonzero strength
// retains the material.
switch (LilToonDistanceFadeSemantics.Evaluate(evidence))
{
    case LilToonDistanceFadeAnswer.Inert:
        break;
    case LilToonDistanceFadeAnswer.Retained:
        return RecordUnknown<ScalarSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Alpha,
            LilToonSemanticDiagnosticCode.FeatureRetention,
            LilToonDistanceFadeSemantics.DistanceFadeProperty);
    default:
        return RecordUnknown<ScalarSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Alpha,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            LilToonDistanceFadeSemantics.DistanceFadeProperty);
}
```

- [ ] **Step 4: Run the class and the full assembly**

Run `LilToonCutoutAlphaTests`, then the full
`Alrauna.Amuse.Tests.Editor` assembly, EditMode. Expected: all three new
tests pass. Every other result matches the baseline.

- [ ] **Step 5: Commit (only with session authorization)**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs
git commit -m "feat: gate cutout alpha on the distance fade strength"
```

---

### Task 6: Cutout conversion eligibility

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs`
- Test: the cutout eligibility test class in
  `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/`, the one
  that pins `EvaluateVerifiedEligibility` for this family.

**Interfaces:**
- Consumes: the capture from Task 3.
- Produces: cutout conversion refuses absence, non-finite components, and
  a nonzero strength, with the same three refusal members the transparent
  family uses.

- [ ] **Step 1: Write the failing eligibility tests**

In the cutout eligibility test class, add three tests modeled on
`LilToonTransparentSourceEligibilityTests.cs:395-414`, using this class's
fixture builder (`NewTransparentFixtureMaterial` there, the cutout
equivalent here):

```csharp
// --- Falsifier: a cutout conversion that admits a finite nonzero
// --- distance fade strength, or that refuses absence or a non-finite
// --- component with any member but the conversion vocabulary's own,
// --- fails one of these fixtures. ---
[Test]
public void DistanceFadeStrengthOn_RefusesUnsupportedDistanceFade()
{
    var material = NewCutoutFixtureMaterial();
    material.SetVector(
        "_DistanceFade", new Vector4(0.1f, 0.01f, 0.5f, 0f));

    AssertRefusal(
        EvaluateFor(material),
        LilToonOpaqueConversionRefusal.UnsupportedDistanceFade);
}

[Test]
public void DistanceFadeNonFinite_RefusesConversionPropertyNotFinite()
{
    var material = NewCutoutFixtureMaterial();
    material.SetVector(
        "_DistanceFade", new Vector4(0.1f, 0.01f, 0f, float.NaN));

    AssertRefusal(
        EvaluateFor(material),
        LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
}

[Test]
public void DistanceFadeAbsent_RefusesConversionPropertyAbsent()
{
    var material = NewCutoutFixtureMaterial();
    material.SetVector("_DistanceFade", new Vector4(
        float.NaN, float.NaN, float.NaN, float.NaN));
    // A shader without the property cannot be expressed on a compiled
    // stand-in, so absence is exercised at the evidence level: build
    // the evidence without the vector, exactly as the transparent
    // class does for its absence test.
    AssertRefusal(
        EvaluateForEvidenceWithoutVector(),
        LilToonOpaqueConversionRefusal.ConversionPropertyAbsent);
}
```

Match the local assertion helper names to the cutout class's existing
ones. If the cutout class has no absence fixture, build the evidence by
stripping the vector from the captured record, the way the transparent
tests produce a missing property.

- [ ] **Step 2: Run the tests to verify they fail**

Run the cutout eligibility class, EditMode. Expected: the strength test
fails because no cutout gate exists, so the material converts. That is
the RED state.

- [ ] **Step 3: Add the gates**

In `LilToonCutoutSourceEligibility.cs`:

1. Add `"_DistanceFade"` to `SourcePresenceSchema`, beside the other
   presence names.
2. Insert the three gates after gate 12 (`ClipThresholdDiscardsOpaqueAlpha`)
   and before the "Deliberately ungated" comment:

```csharp
// 13. Distance fade. The vendor forward pass lerps the fragment
//     color by the fade weight at LIL_RENDER 1, and the dither path
//     lerps alpha before the coverage transform. A moved triangle
//     would lose both, so a finite nonzero strength refuses. The
//     vector rides the alpha capture request, so its absence refuses
//     as ConversionPropertyAbsent, exactly like the transparent
//     family.
if (!evidence.TryGetVector(
        LilToonDistanceFadeSemantics.DistanceFadeProperty,
        out var distanceFade))
{
    return LilToonOpaqueConversionEligibility.Refused(
        LilToonOpaqueConversionRefusal.ConversionPropertyAbsent);
}

if (float.IsNaN(distanceFade.x) || float.IsInfinity(distanceFade.x) ||
    float.IsNaN(distanceFade.y) || float.IsInfinity(distanceFade.y) ||
    float.IsNaN(distanceFade.z) || float.IsInfinity(distanceFade.z) ||
    float.IsNaN(distanceFade.w) || float.IsInfinity(distanceFade.w))
{
    return LilToonOpaqueConversionEligibility.Refused(
        LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
}

if (distanceFade.z != 0f)
{
    return LilToonOpaqueConversionEligibility.Refused(
        LilToonOpaqueConversionRefusal.UnsupportedDistanceFade);
}
```

- [ ] **Step 4: Run the class and the full assembly**

Run the cutout eligibility class, then the full
`Alrauna.Amuse.Tests.Editor` assembly, EditMode. Expected: the three new
tests pass. Every other result matches the baseline.

- [ ] **Step 5: Commit (only with session authorization)**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/
git commit -m "feat: refuse cutout conversion on an active distance fade"
```

---

### Task 7: Multi parity falsifiers

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonMultiSourceEligibilityTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

**Interfaces:**
- Consumes: Tasks 4 to 6. No production change in this task.

- [ ] **Step 1: Write the failing mode-1 eligibility falsifier**

In `LilToonMultiSourceEligibilityTests.cs`, beside
`DistanceFadeStrengthOnAtTransparentModeRefuses` (`:521-537`), add:

```csharp
/// <summary>
/// Mode 1 delegates to the cutout evaluator, so the strength refusal
/// must arrive through delegation, with the mirrored row's own member.
/// </summary>
// --- Falsifier: a mode-1 delegation that loses the distance fade
// --- refusal, or that refuses with any member but
// --- UnsupportedDistanceFade, fails this fixture. ---
[Test]
public void DistanceFadeStrengthOnAtCutoutModeRefusesThroughDelegation()
{
    var material = CutoutModeContainerMaterial(
        BaseCutoutContainerShaderPath);
    material.SetVector(
        "_DistanceFade", new Vector4(0.1f, 0.01f, 0.5f, 0f));
    material.SetVector(
        "_DissolveParams", new Vector4(0f, 0f, 0.5f, 0.1f));
    material.shaderKeywords = new[] { "UNITY_UI_ALPHACLIP" };

    Assert.That(
        EvaluateMultiAtMode(material, CutoutMode),
        Is.EqualTo(LilToonOpaqueConversionRefusal.UnsupportedDistanceFade));
}
```

Match the local helper names (`CutoutModeContainerMaterial`,
`BaseCutoutContainerShaderPath`, `CutoutMode`, the keyword constant) to
the class's existing mode-1 fixtures, exactly as
`DistanceFadeStrengthOnAtTransparentModeRefuses` uses its transparent
helpers.

- [ ] **Step 2: Run the test to verify it fails**

Run `LilToonMultiSourceEligibilityTests`, EditMode. Expected: the new
falsifier fails on the base code, because mode 1 delegated to a cutout
evaluator with no distance fade gate. That is the RED state, and it is
only honest while Task 6's cutout eligibility gates are not on disk yet.
Write and run this step before starting Task 6, or accept the outcome the
next sentence describes. If Task 6 already landed, the test passes on
first run: record it as characterization parity, never as RED. Do not
stash or reorder landed work to manufacture a RED run.

- [ ] **Step 3: Write the mode-2 semantics retention falsifier**

In `AlphaSeparationPreparationTests.cs`, beside the Multi resolution
tests at `:1934-2104`, add one test that builds a mode-2 Multi material
through `LilToonFixtureTestBase.CreateCutoutSchemaStandIn` with the
transparent-mode keyword set, sets `_DistanceFade` to
`(0.1f, 0.01f, 0.5f, 1f)`, runs
`UnityMaterialSemantics.AnalyzeAlphaMaterial`, and asserts:

```csharp
Assert.That(
    analysis.AlphaUnknownReason.Kind,
    Is.EqualTo(AlphaUnknownKind.FeatureRetention));
Assert.That(
    analysis.AlphaUnknownReason.Property,
    Is.EqualTo("_DistanceFade"));
```

Use the same try/finally material disposal and fixture lifecycle as the
two neighbor tests.

- [ ] **Step 4: Write the keyword bypass falsifier**

Add one mode-1 test: `_DistanceFade.z` nonzero with the `_FADING_ON`
keyword deliberately absent from the material's keyword set. The expected
answer is the mode gate's own refusal (a keyword state the vendor
derivation cannot produce), not admission, not retention. This pins that
the keyword state cannot bypass the strength rule.

- [ ] **Step 5: Run the Multi classes and the full assembly**

Run `LilToonMultiSourceEligibilityTests` and
`AlphaSeparationPreparationTests`, then the full
`Alrauna.Amuse.Tests.Editor` assembly, EditMode. Expected: all new
falsifiers pass, and the existing
`DistanceFadeStrengthOnAtTransparentModeRefuses` passes unchanged. Every
other result matches the baseline.

---

### Task 8: Slot resolution and report wiring

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/` and
  `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/`, at the
  assertions named in the steps.

**Interfaces:**
- Consumes: Tasks 2 to 6. Production code changed only in Task 2; this
  task aligns the remaining pinned expectations.

- [ ] **Step 1: Locate the pinned slot refusal assertions**

Run:

```bash
grep -rn "AdmittedMaterialSemanticsUnknown" Packages/com.alrauna.amuse/Tests/
```

For every failing-test fixture whose material carries a nonzero
`_DistanceFade.z`, update the expected refusal value to
`RendererAnalysisRefusal.AdmittedMaterialRetainedByFeature`. Fixtures
whose materials carry a zero strength keep the old value.

- [ ] **Step 2: Write the slot retention test**

Where the resolution tests build slots from fixture materials, add one
test: a transparent fixture material with `_DistanceFade` set to
`(0.1f, 0.01f, 0.5f, 1f)` resolves to a slot refusal whose value is
`AdmittedMaterialRetainedByFeature`, whose reason kind is
`FeatureRetention`, and whose material keeps its original assignment with
no clone and no moved triangles. Reuse the harness of the neighboring
slot refusal tests for setup and disposal.

- [ ] **Step 3: Run the build test classes**

Run `AlphaSeparationPreparationTests`, `AmuseReportStringsTests`, and the
full `Alrauna.Amuse.Tests.Editor` assembly, EditMode. Expected: all pass,
and every result matches the baseline except the intentionally flipped
expectations.

- [ ] **Step 4: Commit (only with session authorization)**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/
git commit -m "test: pin retention outcomes for the distance fade"
```

---

### Task 9: Full validation and finish

**Files:**
- Modify: `docs/superpowers/investigations/2026-10-01-liltoon-distance-fade-refusal-investigation.md`
  (append one dated status line: implemented on this branch, with the
  observed test counts).

- [ ] **Step 1: Run both assemblies**

Run the full `Alrauna.Amuse.Tests.Editor` and
`Alrauna.Amuse.Research.Tests.Editor` assemblies, EditMode. Compare every
failure against the Task 1 baseline. The failure set must be identical to
the baseline.

- [ ] **Step 2: Check the diff**

Run `git diff --check`. Fix any whitespace defect. Read the full diff once
against the spec: every spec section must map to a landed change, and no
unrelated change may appear.

- [ ] **Step 3: Sweep the changed documents**

Sweep every changed Markdown file for an at sign joined to a hexadecimal
hash, drive-letter paths, home-directory paths, four-digit ports, and
every private asset name known to the session. Every hit is a defect.
Fix the hits, then state that the sweep ran.

- [ ] **Step 4: Report**

Report the observed test counts for both assemblies, the diff summary,
and the baseline comparison. State the remaining risks: the cutout
eligibility gate is fresh coverage with no live census population, and
the retention report keeps the refusal key and severity in this slice.

---

## Self-Review Record

- Spec coverage: the retention rule maps to Tasks 2, 4, and 5. The cutout
  correctness section maps to Tasks 3, 5, and 6. The Multi parity section
  maps to Task 7. The report vocabulary maps to Tasks 2 and 8. The
  stronger transformation stays out, per the spec.
- Placeholder scan: every code step carries its code. The two
  helper-name adaptations (Task 5 Step 1, Task 6 Step 1, Task 7 Step 1)
  name the exact neighbor whose local names bind them, which is the
  repo's own convention for per-family test classes.
- Type consistency: `LilToonDistanceFadeAnswer` members are `Absent`,
  `Inert`, `Retained`, `NonFinite` everywhere. The property constant is
  `LilToonDistanceFadeSemantics.DistanceFadeProperty` everywhere. The
  refusal members are the existing
  `LilToonOpaqueConversionRefusal` values plus the new
  `RendererAnalysisRefusal.AdmittedMaterialRetainedByFeature`.
