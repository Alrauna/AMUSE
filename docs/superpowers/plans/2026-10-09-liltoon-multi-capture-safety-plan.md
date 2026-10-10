# lilToon Multi Capture Safety Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Multi capture request carry the conversion schema, close the Multi family request switch, and gate the resolved transparent mode against a cutoff-binarized `_MainTex` field.

**Architecture:** One evidence request union in `Editor/Semantics/LilToon/LilToonMultiResolution.cs` serves capture and conversion readers from one object. One switch case in `Editor/Semantics/UnityMaterialSemantics.cs` closes the family request switch. One field-predicate gate in the shared `LilToonAlphaInterpreter` refuses a transparent claim over a binarized field, bound per family at the two frontend call sites.

**Tech Stack:** C# (Unity 2022.3), Unity Test Framework (EditMode), NUnit constraint model.

**Spec:** `docs/superpowers/specs/2026-10-09-liltoon-multi-capture-safety-design.md`

## Global Constraints

- Use simple English. Short active sentences. One idea per sentence.
- No semicolons in prose.
- No contractions.
- Never record absolute or machine-specific paths. Use repository-relative paths only.
- Never record host names, user account names, home-directory paths, ports, or Unity MCP instance names.
- Date every status claim. As of 2026-10-09.
- Never stage, commit, push, or modify Git history without explicit user authorization.
- The dev editor instance with `Application.dataPath == <repo-root>/Assets` is the only authorized test instance.
- No vendor shader is installed. Tests use the committed Multi container stand-ins and the `Hidden/Alrauna/AmuseTests/*` stand-ins.
- One test class per production type. Test methods are behavior sentences. Use `Assert.That(actual, Is.EqualTo(expected))`.
- A filtered run that reports 0 tests is a verification failure, never a pass.
- This pair runs first in the execution order. Pair 6 shares `Editor/Semantics/UnityMaterialSemantics.cs` and edits other regions.

---

### Task 1: Runtime Confirmation Probe for the Finding 2 Downstream Direction

This task characterizes the defect before any production change.
The probe runs GREEN before the fix.
That is intended. It is characterization, not a RED test.
No existing pinned test bakes the pre-fix behavior.
Task 4 converts this probe into the pinned refusal test.

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonMultiSourceEligibilityTests.cs`

**Interfaces:**
- Consumes: `LilToonMultiResolution.MultiEvidenceRequest`, `LilToonMultiResolution.Resolve(CapturedMaterialEvidence, LilToonSourceEvidence, LilToonMultiContainerProfile, bool, out CapturedAlphaMaterialFamily, out int, out LilToonMultiResolutionRefusal)`, `LilToonMultiResolutionTests.MatchingSourceEvidence(string)`, `LilToonMultiResolutionTests.MatchingProfile(string)`, `LilToonMultiResolutionTests.BaseContainerName`, `LilToonMultiResolutionRecord.Admitted(LilToonSourceEvidence)`, `UnityMaterialSemantics.AnalyzeAlphaMaterial`, `UnityMaterialSemantics.AlphaPredicateRequestFor`, `UnityRendererAlphaAnalysis.GatherAlphaFields(IReadOnlyList<CapturedAlphaMaterial>, int, int)`, `AlphaFieldSet.TryGetFor`, `AlphaSemanticsResolver.Resolve`, `AlphaResolution.Classify`
- Produces: the probe test `ResolvedTransparentMultiClassificationDirectionProbe` and the private grid helper `MixedAlphaGrid(byte)`, both converted by Task 4

- [ ] **Step 1: Write the probe**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonMultiSourceEligibilityTests.cs`, add the usings `Alrauna.Amuse.Editor.Analysis` and `Alrauna.Amuse.Editor.Semantics.Poiyomi`. The file already imports `Alrauna.Amuse.Editor.Host`, `Alrauna.Amuse.Editor.Semantics`, and `Alrauna.Amuse.Editor.Semantics.LilToon`.

Add the private helper beside the other private helpers:

```csharp
private static Color32[] MixedAlphaGrid(byte alpha)
{
    var pixels = new Color32[4 * 4];
    for (var index = 0; index < pixels.Length; index++)
    {
        pixels[index] = new Color32(255, 255, 255, alpha);
    }

    return pixels;
}
```

Add the probe test:

```csharp
// Finding 2 runtime confirmation (investigation 2026-10-09). The probe
// runs before any production change. It pins the capture mechanism and
// reports the observed classification direction through the test output.
// Task 4 converts this test into the pinned fail-closed expectation and
// records the observed direction in that test's comment.
[Test]
public void ResolvedTransparentMultiClassificationDirectionProbe()
{
    var containerName = LilToonMultiResolutionTests.BaseContainerName;
    var material = Track(CreateCutoutSchemaStandIn(
        TempFolder,
        containerName,
        2f,
        new[] { "UNITY_UI_CLIP_RECT" }));
    material.SetTexture(
        "_MainTex",
        ImportMipmapTexture(
            "multi_direction_probe",
            4, 4, MixedAlphaGrid(128)));
    material.SetFloat("_Cutoff", 0.3f);

    var evidence = UnityMaterialEvidenceCapture.Capture(new[]
    {
        new MaterialEvidenceCaptureInput(
            material,
            LilToonMultiResolution.MultiEvidenceRequest,
            UnityMaterialSemantics.AlphaPredicateRequestFor(
                material, CapturedAlphaMaterialFamily.LilToonMulti)),
    })[0];

    var sourceEvidence =
        LilToonMultiResolutionTests.MatchingSourceEvidence(containerName);
    var resolved = LilToonMultiResolution.Resolve(
        evidence,
        sourceEvidence,
        LilToonMultiResolutionTests.MatchingProfile(containerName),
        keywordsRequested: true,
        out var family,
        out _,
        out var refusal);
    Assert.That(resolved, Is.True, refusal.ToString());
    Assert.That(
        family,
        Is.EqualTo(CapturedAlphaMaterialFamily.LilToonTransparent));

    var captured = new CapturedAlphaMaterial(
        family,
        evidence,
        default(PoiyomiSourceEvidence),
        sourceEvidence,
        multiResolution: LilToonMultiResolutionRecord.Admitted(
            sourceEvidence));

    Assert.That(
        captured.Evidence.TryGetTexture("_MainTex", out var main),
        Is.True,
        "fixture precondition: _MainTex must be captured");
    Assert.That(
        main.Texture.CaptureThreshold,
        Is.EqualTo(0.3f),
        "fixture precondition: the production capture must binarize _MainTex");

    var analysis = UnityMaterialSemantics.AnalyzeAlphaMaterial(captured);
    var fields = UnityRendererAlphaAnalysis.GatherAlphaFields(
        new[] { captured }, 0, 1);
    var request = UnityMaterialSemantics.AlphaRequestForFamily(family);
    AlphaFieldProvider fieldsFor = (source, channel, out chain) =>
        fields.TryGetFor(
            captured.Evidence, request, source, channel, out chain);
    var resolution = AlphaSemanticsResolver.Resolve(
        analysis.Semantics.Alpha, fieldsFor, 0);
    var triangle = TriangleAlphaInput.WithUv0(
        Vector3.zero,
        Vector3.right,
        Vector3.up,
        new Vector2(0.25f, 0.25f),
        new Vector2(0.75f, 0.25f),
        new Vector2(0.25f, 0.75f));

    TestContext.Out.WriteLine(
        "observed alpha complete: " +
        analysis.Semantics.Alpha.IsComplete);
    TestContext.Out.WriteLine(
        "observed resolution.IsResolved: " + resolution.IsResolved);
    TestContext.Out.WriteLine(
        "observed classify: " + resolution.Classify(triangle));

    // Pre-fix mechanism pin: the transparent claim consumes the binarized
    // field without refusing. Task 4 replaces this assertion with the
    // fail-closed refusal.
    Assert.That(
        analysis.Semantics.Alpha.IsComplete,
        Is.True,
        "pre-fix mechanism: the transparent claim must not refuse yet");
}
```

- [ ] **Step 2: Run the probe**

Run `ResolvedTransparentMultiClassificationDirectionProbe` in the Unity Test Runner, EditMode.
Expected: PASS on the mechanism assertions.
The test output line `observed classify:` names the direction.
The expected value is `ProvenOpaque` per the spec's mechanism inference.
Record whatever the run prints.

- [ ] **Step 3: Record the observed outcome**

Record the printed values of `observed alpha complete`, `observed resolution.IsResolved`, and `observed classify` in the task result to the integrator.
Keep the probe test in place.
Task 4 converts it and carries the observed direction into the pinned test's comment.
The pinned expectation is the fail-closed refusal regardless of the observed direction.

---

### Task 2: Finding 1 - Union the Conversion Request into the Multi Request

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiResolution.cs:67-89`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonMultiSourceEligibilityTests.cs`

**Interfaces:**
- Consumes: `LilToonMultiSourceEligibility.ConversionEvidenceRequest` (`MaterialEvidenceRequest`), `LilToonOpaqueTarget.RecipeSchemaProperties`, `EffectiveRenderState.ReadEffectiveRenderState`
- Produces: `LilToonMultiResolution.MultiEvidenceRequest` carrying every name `LilToonMultiSourceEligibility.EvaluateVerifiedEligibility` reads

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonMultiSourceEligibilityTests.cs`, add the private helper beside `EvaluateMulti`:

```csharp
private static LilToonOpaqueConversionEligibility
    EvaluateUnderProductionRequest(Material material, int mode)
{
    EffectiveRenderState.ReadEffectiveRenderState(
        material, out var queue, out var renderType);
    var captured = UnityMaterialEvidenceCapture.Capture(new[]
    {
        new MaterialEvidenceCaptureInput(
            material,
            LilToonMultiResolution.MultiEvidenceRequest),
    })[0];
    return LilToonMultiSourceEligibility.EvaluateVerifiedEligibility(
        captured, queue, renderType, mode);
}
```

Add the three tests:

```csharp
// Finding 1 falsifier: the eligibility schema opens with the canonical
// recipe names, and the Multi capture under the production request names
// none of them. A resolved, fully proven Multi material then throws
// "Property '_SrcBlend' was not requested." inside EvaluateVerifiedEligibility.
// --- Falsifier: a Multi request that misses one recipe name crashes the conversion boundary. ---
[Test]
public void ProductionMultiRequestCarriesTheCanonicalRecipeSchema()
{
    foreach (var recipeProperty in LilToonOpaqueTarget.RecipeSchemaProperties)
    {
        CollectionAssert.Contains(
            LilToonMultiResolution.MultiEvidenceRequest.ScalarProperties,
            recipeProperty,
            recipeProperty + " must ride the one Multi capture");
    }
}

// The production capture schema plus the production evaluator at mode 1.
// The committed cutout container stand-in already declares the recipe
// scalars, so the widened capture evaluates instead of throwing.
// --- Falsifier: a conversion boundary that throws on the first admissible Multi conversion fails this fixture. ---
[Test]
public void ProductionMultiCaptureEvaluatesModeOneEligibilityToConvertible()
{
    var material = CutoutModeContainerMaterial(BaseContainerShaderPath);
    Assert.DoesNotThrow(
        () => EvaluateUnderProductionRequest(material, CutoutMode));
    AssertConvertible(
        EvaluateUnderProductionRequest(material, CutoutMode));
}

// The mode-2 twin on the committed transparent container stand-in.
// --- Falsifier: a widened request that flips a mode-2 admission into a refusal fails this fixture. ---
[Test]
public void ProductionMultiCaptureEvaluatesModeTwoEligibilityToConvertible()
{
    var material = TransparentModeContainerMaterial(
        BaseTransparentContainerShaderPath);
    Assert.DoesNotThrow(
        () => EvaluateUnderProductionRequest(material, TransparentMode));
    AssertConvertible(
        EvaluateUnderProductionRequest(material, TransparentMode));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the three tests in the Unity Test Runner, EditMode.
Expected: all three FAIL.
`ProductionMultiRequestCarriesTheCanonicalRecipeSchema` fails on `_SrcBlend`.
The two evaluation tests fail with `ArgumentException` and the message `"Property '_SrcBlend' was not requested."` thrown from `EvaluateVerifiedEligibility`.

- [ ] **Step 3: Implement the union**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiResolution.cs`, add the fifth combination member:

```csharp
internal static readonly MaterialEvidenceRequest
    MultiEvidenceRequest =
        MaterialEvidenceRequest.Combine(
            LilToonMaterialSemantics.AlphaEvidenceRequest,
            LilToonCutoutMaterialSemantics.AlphaEvidenceRequest,
            LilToonTransparentMaterialSemantics.AlphaEvidenceRequest,
            LilToonMultiSourceEligibility.ConversionEvidenceRequest,
            new MaterialEvidenceRequest(
                shaderName: true,
                activeColorSpace: false,
                presenceProperties: Array.Empty<string>(),
                scalarProperties: BuildMultiScalarSchema(),
                colorProperties: Array.Empty<string>(),
                vectorProperties:
                    LilToonMultiModeGate.ConsultedVectorProperties,
                textureProperties: BuildMultiTextureRequests(),
                captureKeywords: true));
```

Extend the doc comment above the request with one sentence after the sentence about the interpreters:

```csharp
    /// the conversion eligibility evaluator is the second conversion
    /// reader: its schema opens with the canonical recipe names, so the
    /// recipe evidence rides the one Multi capture beside the alpha and
    /// Multi state facts.
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the three tests in the Unity Test Runner, EditMode.
Expected: all three PASS.
Run `LilToonMultiResolutionTests` and `LilToonMultiModeGateTests` in the same runner session.
Expected: every run reports more than zero tests and all pass.

---

### Task 3: Finding 2, Part A - Close the Family Request Switch

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:802-831`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`

**Interfaces:**
- Consumes: `LilToonMultiResolution.MultiEvidenceRequest`, `CapturedAlphaMaterialFamily.LilToonMulti`
- Produces: `AlphaRequestForFamily(CapturedAlphaMaterialFamily.LilToonMulti)` returning the combined Multi request, which also becomes the capture predicate through the `AlphaPredicateRequestFor` default arm

- [ ] **Step 1: Write the failing test**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`, add `using Alrauna.Amuse.Editor.Semantics.LilToon;` if absent.
Place the test beside `MultiContainersClassifyToTheMultiFamilyWithKeywordCapture`:

```csharp
// Finding 2 falsifier, part A: the family request switch answers null
// for the Multi family. The null reaches DeclaresCutoffFor, which then
// keeps the union declaration by fallback instead of by name.
// --- Falsifier: a family switch that leaves the Multi member on the null default arm fails this fixture. ---
[Test]
public void AlphaRequestForTheMultiFamilyIsTheCombinedMultiRequest()
{
    Assert.That(
        UnityMaterialSemantics.AlphaRequestForFamily(
            CapturedAlphaMaterialFamily.LilToonMulti),
        Is.SameAs(LilToonMultiResolution.MultiEvidenceRequest));

    var material = NewMaterial(
        "multi-predicate-request.shader",
        "_lil/lilToonMulti",
        MultiProperties());
    Assert.That(
        UnityMaterialSemantics.AlphaPredicateRequestFor(
            material,
            CapturedAlphaMaterialFamily.LilToonMulti),
        Is.SameAs(LilToonMultiResolution.MultiEvidenceRequest));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run `AlphaRequestForTheMultiFamilyIsTheCombinedMultiRequest` in the Unity Test Runner, EditMode.
Expected: FAIL. `AlphaRequestForFamily` returns null for the Multi family today.

- [ ] **Step 3: Implement the case**

In `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`, add the case inside `AlphaRequestForFamily` after the `LilToonTransparent` case:

```csharp
case CapturedAlphaMaterialFamily.LilToonMulti:
    return LilToonMultiResolution.MultiEvidenceRequest;
```

- [ ] **Step 4: Run the test to verify it passes**

Run `AlphaRequestForTheMultiFamilyIsTheCombinedMultiRequest` in the Unity Test Runner, EditMode.
Expected: PASS.
Run `LilToonMultiSourceEligibilityTests` in the same session.
Expected: every run reports more than zero tests and all pass.

---

### Task 4: Finding 2, Part B - The Resolved-Mode Field-Predicate Gate

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonMultiSourceEligibilityTests.cs`

**Interfaces:**
- Consumes: `CapturedTextureEvidence.CaptureThreshold`, `LilToonSemanticDiagnosticCode.UnsupportedFeature`, `LilToonSemanticOutput.Alpha`
- Produces: `LilToonAlphaInterpreter.Interpret(CapturedMaterialEvidence, List<LilToonSemanticDiagnostic>, string[], float, LilToonAlphaGate[], bool requiresExactMainField)`. Cutout binds `false`. Transparent binds `true`.

- [ ] **Step 1: Write the failing interpreter gate test**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs`, add the test beside the other assigned-texture tests:

```csharp
// Finding 2 falsifier, part B: a transparent claim over a field the
// capture binarized by _Cutoff reads byte 255 as alpha exactly one.
// The Poiyomi frontend refuses this disagreement naming _Cutoff. The
// transparent frontend gains the same gate. The proven-one importer
// theorem arm and the identity refusal keep their precedence ahead of
// the gate, because neither reads the field.
// --- Falsifier: a transparent frontend that consumes a binarized field under the exact-one rule fails this fixture. ---
[Test]
public void BinarizedMainFieldUnderTheTransparentClaimRefusesNamingCutoff()
{
    var material = NewTransparentFixtureMaterial();
    material.SetTexture(
        MainTextureProperty,
        ImportMipmapTexture(
            "transparent_claim_binarized",
            4, 4, SolidGrid(4, 4, 128)));
    material.SetFloat(CutoffProperty, 0.3f);

    var captured = UnityMaterialEvidenceCapture.Capture(new[]
    {
        new MaterialEvidenceCaptureInput(
            material,
            LilToonMultiResolution.MultiEvidenceRequest),
    })[0];
    Assert.That(
        captured.TryGetTexture(MainTextureProperty, out var main),
        Is.True,
        "fixture precondition: _MainTex must be captured");
    Assert.That(
        main.Texture.CaptureThreshold,
        Is.EqualTo(0.3f),
        "fixture precondition: the capture must binarize _MainTex");

    var alpha = LilToonTransparentMaterialSemantics
        .InterpretVerifiedTransparentAlpha(captured, out var reason);

    Assert.That(
        alpha.IsComplete,
        Is.False,
        "a binarized field cannot answer the exact-one rule");
    Assert.That(reason, Is.Not.Null);
    Assert.That(
        reason.Kind,
        Is.EqualTo(AlphaUnknownKind.UnsupportedFeature));
    Assert.That(
        reason.Property,
        Does.Contain("_Cutoff"));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run `BinarizedMainFieldUnderTheTransparentClaimRefusesNamingCutoff` in the Unity Test Runner, EditMode.
Expected: FAIL on the first assertion. The transparent interpretation completes over the binarized field today.

- [ ] **Step 3: Implement the gate and the two bindings**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs`:

Change the `Interpret` signature by appending one parameter:

```csharp
internal static SemanticOutput<ScalarSemanticValue> Interpret(
    CapturedMaterialEvidence evidence,
    List<LilToonSemanticDiagnostic> diagnostics,
    string[] coverageGates,
    float maxProvableCutoff,
    LilToonAlphaGate[] transparentOnlyGates,
    bool requiresExactMainField)
```

Document the new parameter in the method's doc comment:

```csharp
    /// <param name="requiresExactMainField">
    /// Whether the family's theorem reads the main alpha field under the
    /// exact-255 exact-one rule. Transparent binds true. Cutout binds
    /// false: its own request declares the cutoff, and its coverage
    /// transform reads the binarized field by its own rule. When true,
    /// an assigned _MainTex captured under a sub-one capture threshold
    /// refuses naming _Cutoff before any field read, in the shape of the
    /// Poiyomi frontend's field-predicate agreement check.
    /// </param>
```

Inside the assigned-texture arm, in the final `else` branch, insert the gate between the source-identity refusal and the sample construction:

```csharp
if (!assignment.Texture.HasSourceIdentity)
{
    return RecordUnknown<ScalarSemanticValue>(
        diagnostics,
        LilToonSemanticOutput.Alpha,
        LilToonSemanticDiagnosticCode
            .UnstableTextureIdentity,
        MainTextureProperty);
}

if (requiresExactMainField &&
    assignment.Texture.CaptureThreshold < 1f)
{
    return RecordUnknown<ScalarSemanticValue>(
        diagnostics,
        LilToonSemanticOutput.Alpha,
        LilToonSemanticDiagnosticCode.UnsupportedFeature,
        CutoffProperty);
}

// uvMain is UV0 under the identity gates above.
var mainSample = new TextureSample(
    assignment.Texture.SourceIdentity,
    new UvMapping(0, assignment.Scale, assignment.Offset),
    assignment.Texture.Sampling);
```

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs`, append the binding at the end of `InterpretCutoutAlpha`:

```csharp
return LilToonAlphaInterpreter.Interpret(
    evidence,
    diagnostics,
    AlphaCoverageGates,
    MaxProvableCutoff,
    Array.Empty<LilToonAlphaGate>(),
    requiresExactMainField: false);
```

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs`, append the binding at the end of `InterpretTransparentAlpha`:

```csharp
return LilToonAlphaInterpreter.Interpret(
    evidence,
    diagnostics,
    AlphaCoverageGates,
    MaxProvableCutoff,
    TransparentOnlyGates,
    requiresExactMainField: true);
```

- [ ] **Step 4: Run the test to verify it passes**

Run `BinarizedMainFieldUnderTheTransparentClaimRefusesNamingCutoff` in the Unity Test Runner, EditMode.
Expected: PASS.
Run `LilToonCutoutAlphaTests` and `LilToonTransparentAlphaTests` in the same session.
Expected: every run reports more than zero tests and all pass.
The cutout suite must stay fully green, because its binding keeps the binarized field its own theorem reads.

- [ ] **Step 5: Convert the probe into the pinned refusal test**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonMultiSourceEligibilityTests.cs`, replace `ResolvedTransparentMultiClassificationDirectionProbe` and its pre-fix mechanism assertion with:

```csharp
// Finding 2 pinned refusal. Task 1's probe ran this state pre-fix.
// Before running this test, write the classify value the Task 1 probe
// printed into the next comment line and keep it as the record.
// The production capture binarizes _MainTex by the union's cutout
// declaration, so a transparent-resolved Multi refuses naming _Cutoff
// instead of proving triangles from a lying field.
// --- Falsifier: a resolved transparent Multi that proves opaque from a binarized field fails this fixture. ---
[Test]
public void ResolvedTransparentMultiWithBinarizedMainFieldStaysUnknownNamingCutoff()
{
    var containerName = LilToonMultiResolutionTests.BaseContainerName;
    var material = Track(CreateCutoutSchemaStandIn(
        TempFolder,
        containerName,
        2f,
        new[] { "UNITY_UI_CLIP_RECT" }));
    material.SetTexture(
        "_MainTex",
        ImportMipmapTexture(
            "multi_direction_probe",
            4, 4, MixedAlphaGrid(128)));
    material.SetFloat("_Cutoff", 0.3f);

    var evidence = UnityMaterialEvidenceCapture.Capture(new[]
    {
        new MaterialEvidenceCaptureInput(
            material,
            LilToonMultiResolution.MultiEvidenceRequest,
            UnityMaterialSemantics.AlphaPredicateRequestFor(
                material, CapturedAlphaMaterialFamily.LilToonMulti)),
    })[0];

    var sourceEvidence =
        LilToonMultiResolutionTests.MatchingSourceEvidence(containerName);
    var resolved = LilToonMultiResolution.Resolve(
        evidence,
        sourceEvidence,
        LilToonMultiResolutionTests.MatchingProfile(containerName),
        keywordsRequested: true,
        out var family,
        out _,
        out var refusal);
    Assert.That(resolved, Is.True, refusal.ToString());
    Assert.That(
        family,
        Is.EqualTo(CapturedAlphaMaterialFamily.LilToonTransparent));

    var captured = new CapturedAlphaMaterial(
        family,
        evidence,
        default(PoiyomiSourceEvidence),
        sourceEvidence,
        multiResolution: LilToonMultiResolutionRecord.Admitted(
            sourceEvidence));

    Assert.That(
        captured.Evidence.TryGetTexture("_MainTex", out var main),
        Is.True,
        "fixture precondition: _MainTex must be captured");
    Assert.That(
        main.Texture.CaptureThreshold,
        Is.EqualTo(0.3f),
        "fixture precondition: the production capture must binarize _MainTex");

    var analysis = UnityMaterialSemantics.AnalyzeAlphaMaterial(captured);
    Assert.That(
        analysis.Semantics.Alpha.IsComplete,
        Is.False,
        "a binarized field cannot answer the transparent exact-one rule");
    Assert.That(analysis.AlphaUnknownReason, Is.Not.Null);
    Assert.That(
        analysis.AlphaUnknownReason.Kind,
        Is.EqualTo(AlphaUnknownKind.UnsupportedFeature));
    Assert.That(
        analysis.AlphaUnknownReason.Property,
        Does.Contain("_Cutoff"));
}
```

Delete the resolver, field-set, triangle, and `TestContext.Out` statements from the probe. They have no meaning after the interpretation refuses. Delete the private helper `MixedAlphaGrid` only if no other test uses it. The pinned test uses it.

- [ ] **Step 6: Run the converted test to verify it passes**

Run `ResolvedTransparentMultiWithBinarizedMainFieldStaysUnknownNamingCutoff` in the Unity Test Runner, EditMode.
Expected: PASS.

---

### Task 5: Regression Sweep

**Files:**
- No production or test file changes.

**Interfaces:**
- Consumes: every test class touched or bound by Tasks 1 through 4
- Produces: the verification evidence for the whole pair

- [ ] **Step 1: Run the touched suites**

Run all of the following in the Unity Test Runner, EditMode, each as its own filtered run:

- `LilToonMultiSourceEligibilityTests`
- `LilToonMultiResolutionTests`
- `LilToonMultiModeGateTests`
- `LilToonTransparentAlphaTests`
- `LilToonCutoutAlphaTests`
- `UnityMaterialSemanticsTests`
- `AlphaSeparationPreparationTests`

Expected: every filtered run reports more than zero tests and all pass.
A filtered run that reports 0 tests is a verification failure.
Re-run the filter with a corrected class name before concluding anything.

- [ ] **Step 2: Report the pair result**

Report to the integrator: the observed probe direction from Task 1, the test counts per suite, and the touchpoint note that pair 6 rebases on `Editor/Semantics/UnityMaterialSemantics.cs` regions 802-831 and 836-851.
