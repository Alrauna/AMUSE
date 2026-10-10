# Semantics Evidence Robustness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the eleven semantics defects of audit Findings 10, 16, 17, 30, 31, 33, 40, 41, 42, 43, and 44: the sampled-alpha normal-map hole, the opaque snapshot and live read mix, the two absent-name feature-off reads, the scalar fold's dead false thread, the aniso level-1 gate, the verifyIdentity asymmetry, the two alpha-mask over-refusals, the LTCGI strip scope, the clone leak on unexpected throw, the attestation profile default, and the post-conversion tint guard.

**Architecture:** All changes live in `Editor/Semantics/` and one additive capture fact in `Editor/Host/`. Every change fails closed. No proof widens. The alpha-only evidence requests keep their exact content.

**Tech Stack:** C# (Unity 2022.3), Unity Test Framework (EditMode).

**Spec:** `docs/superpowers/specs/2026-10-09-semantics-evidence-robustness-design.md`

## Global Constraints

- Use simple English. Short active sentences. One idea per sentence.
- No semicolons in prose.
- No contractions.
- Never record absolute or machine-specific paths. Use repository-relative paths only.
- Never record host names, user account names, home-directory paths, ports, or Unity MCP instance names.
- Date every status claim. As of 2026-10-09.
- Never stage, commit, push, or modify Git history without explicit user authorization.
- The dev editor instance with `Application.dataPath == <repo-root>/Assets` is the only authorized test instance.
- Run every test through the Unity Test Runner in EditMode, filtered to the named class. A filtered run that reports 0 tests is a failure.
- A first-run-passing assertion is characterization, never RED. The tasks below say so where it applies.
- Probe scripts for Findings 10, 31, and 44 are scratch. Run them, record the observed counts in the task, and delete them. Never commit a probe.
- File touchpoints from the audit execution order: pair 1 lands before this pair in `Editor/Semantics/UnityMaterialSemantics.cs`. Pair 3 lands before this pair in `Editor/Host/UnityMaterialEvidenceCapture.cs`; this pair's constructor step must also update the positional construction pair 3's new Finding 6 test adds in that file. Re-read a shared region before editing it if either pair landed while this plan was open.

---

### Task 1: Delete the Scalar Fold's threadMaps Parameter (Finding 30)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/MaterialSemanticsTests.cs`

**Interfaces:**
- Consumes: `ScalarProductFold.Fold(ScalarSemanticValue, ScalarSemanticValue, string, Action<string>)`
- Produces: A fold with no false-thread option. An admitted mapped factor can never lose its map at the type level.

- [ ] **Step 1: Write the characterization test**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/MaterialSemanticsTests.cs`, add to the existing test class:

```csharp
[Test]
public void Fold_WithMappedFactor_KeepsTheMapInTheProductChain()
{
    var source = new TextureSourceId("test:fold_map");
    var mapping = new UvMapping(0, Vector2.one, Vector2.zero);
    var sampling = new TextureSampling(
        TextureFilterMode.Point,
        TextureWrapMode.Repeat,
        TextureAnisoMode.None);
    var sampleA = new TextureSample(source, mapping, sampling);
    var sampleB = new TextureSample(source, mapping, sampling);
    var map = AffineAlphaMap.FromBinary32(2f, -0.5f);

    var folded = ScalarProductFold.Fold(
        ScalarSemanticValue.MappedTexture(sampleA, TextureChannel.Alpha, map),
        ScalarSemanticValue.Texture(sampleB, TextureChannel.Alpha),
        "_FoldProperty",
        property => Assert.Fail("no refusal is expected"));

    Assert.That(folded, Is.Not.Null);
    Assert.That(
        folded.Kind,
        Is.EqualTo(ScalarSemanticValueKind.ProductChainOfTextureSamples));
    Assert.That(folded.GetChainFactorCount(), Is.EqualTo(2));
    Assert.That(folded.GetChainMap(0), Is.Not.Null);
    Assert.That(folded.GetChainMap(1), Is.Null);
}
```

Adapt constructor argument order to the actual `TextureSampling` and `TextureSourceId` signatures if they differ. The existing helpers in this file and in `Tests/Editor/Semantics/LilToon/LilToonAlphaMaskSemanticsTests.cs:133-160` show the working shapes.

- [ ] **Step 2: Run the test and record the outcome**

Run `Fold_WithMappedFactor_KeepsTheMapInTheProductChain` in the Unity Test Runner, EditMode, filtered to `MaterialSemanticsTests`. This assertion passes against the current `threadMaps: true` path. It is characterization, stated as such. No RED exists for this finding because all four call sites pass true and no observable defect is reachable. Record the pass in the task notes.

- [ ] **Step 3: Delete the parameter and thread maps unconditionally**

In `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs`, in `ScalarProductFold.Fold` (lines 1044-1157):
- Delete the `bool threadMaps` parameter.
- Replace `var maps = threadMaps ? new List<AffineAlphaMap?>() : null;` with `var maps = new List<AffineAlphaMap?>();`.
- Update the method doc. Remove the parameter sentence. State that the fold always threads one map per factor, so an admitted mapped factor never loses its map.

Remove the `threadMaps: true` argument at the four call sites:
- `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs:399-409`
- `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs:459-469`
- `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:1407-1416`
- `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:1590-1599`

- [ ] **Step 4: Run the tests to verify they pass**

Run `MaterialSemanticsTests`, `LilToonCutoutAlphaTests`, `LilToonTransparentAlphaTests`, and `PoiyomiAlphaMaskTests` in the Unity Test Runner, EditMode. Verify the characterization test passes against the new signature and every existing fold-driven test passes.

---

### Task 2: Refuse Requested-But-Absent Names in the Two lilToon Reads (Finding 17)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonLayerAlphaTerm.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAlphaTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonLayerAlphaTermTests.cs`

**Interfaces:**
- Consumes: `CapturedMaterialEvidence.TryGetTexture(string, out CapturedTextureAssignment)`, which returns false for a requested name whose property is absent from the material and throws for a name outside the request.
- Produces: Refusals naming the property, matching `LilToonAlphaMaskSemantics.cs:257-263` and the layer texture read at `LilToonLayerAlphaTerm.cs:281-291`.

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAlphaTests.cs`, build the synthetic evidence with the construction pattern of `CreateMaskEvidence` (`Tests/Editor/Semantics/LilToon/LilToonAlphaMaskSemanticsTests.cs:126-179`). The evidence must satisfy every read the interpreter performs before the main texture: present zero scalars for the cutout coverage gates, an Inert `_DistanceFade` vector, a `_Color` entry with a finite alpha, a `_AlphaMaskMode` entry of zero with the mask scale and value entries present, and a `_MainTex` `TextureEntry` that holds `hasValue: false` with a default assignment.

```csharp
[Test]
public void MainTexture_RequestedButAbsentFromEvidence_RefusesInsteadOfDeclaredDefault()
{
    var evidence = CreateEvidenceWithMainTexEntryAbsent();

    var alpha = LilToonCutoutMaterialSemantics
        .InterpretVerifiedCutoutAlpha(evidence);

    Assert.That(alpha.IsComplete, Is.False);
    Assert.That(
        alpha.Diagnostics[0].Code,
        Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedFeature));
    Assert.That(alpha.Diagnostics[0].Detail, Is.EqualTo("_MainTex"));
}
```

Match the `SemanticOutput` accessor names and the `InterpretVerifiedCutoutAlpha` overload to the call the cutout tests already use. Today this evidence takes the declared-default arm and returns a complete constant, so the assertion is RED.

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonLayerAlphaTermTests.cs`, reuse the file's synthetic evidence builder (`LilToonLayerAlphaTermTests.cs:169-260`) with one change: the `_Main2ndBlendMask` `TextureEntry` holds `hasValue: false`.

```csharp
[Test]
public void LayerBlendMask_RequestedButAbsentFromEvidence_RefusesInsteadOfDroppingFactor()
{
    var evidence = CreateLayerEvidenceWithBlendMaskEntryAbsent();
    var diagnostics = new List<LilToonSemanticDiagnostic>();

    var term = LilToonLayerAlphaTerm.Interpret(
        evidence, third: false, diagnostics);

    Assert.That(
        term.Kind,
        Is.EqualTo(LilToonLayerAlphaTermKind.Refused));
    // Assert the refusal names _Main2ndBlendMask through the diagnostics
    // list. Mirror the accessor the file's existing refusal tests use for
    // LilToonSemanticDiagnostic. The term itself carries no refusal text.
}
```

Today the mask factor is silently dropped and the term answers without a refusal, so the assertion is RED.

- [ ] **Step 2: Run the tests to verify they fail**

Run the two new tests in the Unity Test Runner, EditMode, filtered to their classes. Verify both fail against the current arms.

- [ ] **Step 3: Split both reads**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs:258-283`, replace:

```csharp
if (!evidence.TryGetTexture(
        MainTextureProperty, out var assignment) ||
    !assignment.IsAssigned)
```

with:

```csharp
if (!evidence.TryGetTexture(MainTextureProperty, out var assignment))
{
    return RecordUnknown<ScalarSemanticValue>(
        diagnostics,
        LilToonSemanticOutput.Alpha,
        LilToonSemanticDiagnosticCode.UnsupportedFeature,
        MainTextureProperty);
}

if (!assignment.IsAssigned)
```

Keep the declared-default comment block on the `!assignment.IsAssigned` arm. Update the comment's first sentence: the arm fires for a declared slot that binds no texture, never for a slot absent from the material.

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonLayerAlphaTerm.cs:352-395`, replace:

```csharp
if (evidence.TryGetTexture(
        blendMaskProperty, out var blendMask) &&
    blendMask.IsAssigned)
```

with:

```csharp
if (!evidence.TryGetTexture(blendMaskProperty, out var blendMask))
{
    return Refuse(diagnostics, blendMaskProperty);
}

if (blendMask.IsAssigned)
```

Keep the inner body of the assigned case unchanged.

- [ ] **Step 4: Run the tests to verify they pass**

Run `LilToonAlphaTests`, `LilToonLayerAlphaTermTests`, `LilToonCutoutAlphaTests`, and `LilToonTransparentAlphaTests` in the Unity Test Runner, EditMode. Verify the two new tests pass and every existing test passes.

---

### Task 3: Admit the Exactly Representable Mask Constants (Finding 40)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAlphaMaskSemanticsTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs`

**Interfaces:**
- Consumes: `ScalarProductFold.Fold` after Task 1, with no `threadMaps` argument.
- Produces: `LilToonAlphaMaskTermKind.ConstantMultiplier` with `Constant` set and `ReplacesMainAlpha` false.

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAlphaMaskSemanticsTests.cs`, extend the `CreateMaskEvidence` helper with an `isAssigned` parameter that defaults to true and drives the `CapturedTextureAssignment(isAssigned: ...)` argument:

```csharp
[Test]
public void AlphaMask_UnassignedWithSubOneMultiplyConstant_YieldsConstantMultiplier()
{
    var evidence = CreateMaskEvidence(
        Vector2.one, Vector2.zero,
        mode: 2f, maskScale: 0.5f, maskValue: 0f,
        isAssigned: false);
    var diagnostics = new List<LilToonSemanticDiagnostic>();

    var term = LilToonAlphaMaskTerm.Interpret(evidence, diagnostics);

    Assert.That(
        term.Kind,
        Is.EqualTo(LilToonAlphaMaskTermKind.ConstantMultiplier));
    Assert.That(term.Constant, Is.EqualTo(0.5f));
    Assert.That(term.ReplacesMainAlpha, Is.False);
    Assert.That(diagnostics, Is.Empty);
}
```

Today this shape refuses `UnsupportedFeature` on `_AlphaMaskScale`, so the assertion is RED.

```csharp
[Test]
public void AlphaMask_AssignedZeroScaleWithoutIdentity_YieldsConstantMultiplierWithoutIdentityDemand()
{
    var evidence = CreateMaskEvidenceWithoutSourceIdentity(
        mode: 2f, maskScale: 0f, maskValue: 0.4f);
    var diagnostics = new List<LilToonSemanticDiagnostic>();

    var term = LilToonAlphaMaskTerm.Interpret(evidence, diagnostics);

    Assert.That(
        term.Kind,
        Is.EqualTo(LilToonAlphaMaskTermKind.ConstantMultiplier));
    Assert.That(term.Constant, Is.EqualTo(0.4f));
    Assert.That(diagnostics, Is.Empty);
}
```

Build `CreateMaskEvidenceWithoutSourceIdentity` from `CreateMaskEvidence` with `hasSourceIdentity: false`. Today the identity check refuses `UnstableTextureIdentity` before the scale-zero arm can answer, so the assertion is RED.

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs`, follow the composition tests at `LilToonCutoutAlphaTests.cs:1077-1091`:

```csharp
[Test]
public void AlphaMask_ConstantMultiplier_ComposesOverTheLayeredAlphaChain()
{
    var material = NewGateOffMaterialWithOpaqueTexture("c_m2_constant_multiplier");
    material.SetFloat("_AlphaMaskMode", 2f);
    material.SetFloat("_AlphaMaskScale", 0.5f);
    material.SetFloat("_AlphaMaskValue", 0f);

    var resolution = ResolveThroughCutoutFrontend(material, AllOpaqueChain());

    // The unassigned mask's term is saturate(0.5 + 0) = 0.5, so the
    // opaque main chain multiplies by exactly 0.5.
    Assert.That(resolution, Is.Not.Null);
    Assert.That(
        resolution.Kind,
        Is.EqualTo(ScalarSemanticValueKind.TextureTimesConstant));
    Assert.That(resolution.GetMultiplier(), Is.EqualTo(0.5f));
}
```

Adapt the resolution accessor names to the file's existing helper vocabulary. Today this shape refuses, so the assertion is RED.

- [ ] **Step 2: Run the tests to verify they fail**

Run the three new tests in the Unity Test Runner, EditMode. Verify all three fail against the current refusals.

- [ ] **Step 3: Add the kind, rephrase the two arms, and compose**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs`:
- Add `ConstantMultiplier` to `LilToonAlphaMaskTermKind` with the doc sentence from spec section 3.3.
- Add the `ConstantMultiplierOf(float value)` factory next to `ConstantTerm` (lines 139-145), with `replacesMainAlpha: false` and `Constant` set.
- Rephrase the unassigned arm (lines 257-280):

```csharp
var term = Mathf.Clamp01(scale + value);
return mode == 1f
    ? ConstantTerm(term)
    : term >= 1f
        ? MainUnchanged()
        : ConstantMultiplierOf(term);
```

- Move the scale-zero arm from lines 316-330 to directly after the `scale == 1f && value >= 1f` arm at lines 282-289, and rephrase it:

```csharp
if (scale == 0f)
{
    var constant = Mathf.Clamp01(value);
    return mode == 1f
        ? ConstantTerm(constant)
        : constant >= 1f
            ? MainUnchanged()
            : ConstantMultiplierOf(constant);
}
```

- Delete the old in-place scale-zero arm.
- Update the class doc paragraph that enumerates the admitted shapes. Add the two multiply-mode constant admissions and the moved-check rationale.

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs`, after both `ComposeLayer` calls and before the `maskSample != null` block, add:

```csharp
if (maskTerm.Kind == LilToonAlphaMaskTermKind.ConstantMultiplier)
{
    alphaChain = ScalarProductFold.Fold(
        alphaChain,
        ScalarSemanticValue.Constant(maskTerm.Constant),
        AlphaMaskModeProperty,
        property => diagnostics.Add(
            LilToonSemanticDiagnostic.Alpha(
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                property)),
        threadMaps: true);
    if (alphaChain == null)
    {
        return SemanticOutput<ScalarSemanticValue>.Unknown();
    }
}
```

Match the diagnostic-construction expression of the existing fold call at lines 399-409 exactly. If Task 1 already deleted the `threadMaps` argument, omit it here.

- [ ] **Step 4: Run the tests to verify they pass**

Run `LilToonAlphaMaskSemanticsTests`, `LilToonCutoutAlphaTests`, and `LilToonTransparentAlphaTests` in the Unity Test Runner, EditMode. Verify the three new tests pass and every existing mask test passes.

---

### Task 4: Re-check Finiteness after Color.linear (Finding 44)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonBaseColorTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonEmissionTests.cs`

**Interfaces:**
- Consumes: the file's existing `IsFinite` helpers.
- Produces: named `UnsupportedFeature` refusals on `_Color` and `_EmissionColor` when the conversion or the product leaves the finite range.

- [ ] **Step 1: Run the probe and record the observed counts**

Before any code change, run a scratch editor script through the editor's C# execution surface. The script constructs a material with `_Color` set to `(-1, 0.5, 0.5)` and to `(-0.01, 1, 1)`, reads `material.color.linear` for each, and prints every component. It then computes the emission product shape `(Vector3)(color.linear) * (1f * color.a)` for the same inputs and prints every component. Record the observed component values in this task's notes with the date. Delete the script.

- [ ] **Step 2: Write the failing test only if the probe showed a non-finite value**

If any observed component is NaN or infinity, add to `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonBaseColorTests.cs`:

```csharp
[Test]
public void BaseColor_TintNonFiniteAfterLinearConversion_Refuses()
{
    var material = NewBaseColorMaterial("c_linear_nonfinite_tint");
    material.SetColor("_Color", new Color(-1f, 0.5f, 0.5f, 1f));

    var result = LilToonMaterialSemantics.InterpretVerifiedMaterial(
        material,
        ColorSpace.Linear,
        Array.Empty<string>());

    Assert.That(result.IsSupportedMaterial, Is.True);
    Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
    Assert.That(
        result.Semantics.BaseColor.Diagnostics[0].Code,
        Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedFeature));
    Assert.That(
        result.Semantics.BaseColor.Diagnostics[0].Detail,
        Is.EqualTo("_Color"));
}
```

Add the emission twin `Emission_TintNonFiniteAfterLinearConversion_Refuses` to `LilToonEmissionTests.cs` with `_EmissionColor` as the detail. Adapt helper names to each file's existing vocabulary. Verify both fail today: the analysis currently aborts with an exception from the downstream constant factory, which the test observes as a failure. If the probe showed no non-finite value, skip this step, record the probe counts in the task notes, and state that the guard lands as a defensive invariant with no RED.

- [ ] **Step 3: Add the post-conversion guards**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`, in `InterpretBaseColor` after `var linear = color.linear;` (around line 306):

```csharp
if (!IsFinite(linear.r) || !IsFinite(linear.g) || !IsFinite(linear.b))
{
    return RecordUnknown<ColorSemanticValue>(
        diagnostics,
        LilToonSemanticOutput.BaseColor,
        LilToonSemanticDiagnosticCode.UnsupportedFeature,
        ColorProperty);
}
var tint = new Vector3(linear.r, linear.g, linear.b);
```

In `InterpretEmission`, after `var tint = new Vector3(linear.r, linear.g, linear.b) * (blend * color.a);` (around line 852):

```csharp
if (!IsFinite(tint.x) || !IsFinite(tint.y) || !IsFinite(tint.z))
{
    return RecordUnknown<ColorSemanticValue>(
        diagnostics,
        LilToonSemanticOutput.Emission,
        LilToonSemanticDiagnosticCode.UnsupportedFeature,
        EmissionColorProperty);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run `LilToonBaseColorTests` and `LilToonEmissionTests` in the Unity Test Runner, EditMode. Verify the new tests pass when they exist, and every existing base-color and emission test passes.

---

### Task 5: Exclude Normal-Map Imports from the Sampled-Alpha Predicate (Finding 10)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs`

**Interfaces:**
- Consumes: the file's `TryGetTextureImporter` and `TextureImporterType.NormalMap`.
- Produces: `TryProveSampledAlphaIsOne` returns false for every normal-map import. Both emission-proof consumers keep their existing `UnsupportedTextureImport` refusal arms.

- [ ] **Step 1: Run the probe and record the observed values**

Before any code change, run a scratch editor script through the editor's C# execution surface. The script generates a small RGB-only PNG in a temporary folder under the project, imports it with `TextureImporterType.NormalMap` and `flipGreenChannel = false`, waits for the import, blits the built texture into a temporary render texture, and reads back one texel. It prints the alpha component and the built `graphicsFormat`. It repeats the read with `importer.sRGBTexture = true` and prints the format again. Record the observed alpha and formats in this task's notes with the date. Delete the script and the temporary asset.

- [ ] **Step 2: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs`, follow the `Import` helper used at `UnityTextureEvidenceTests.cs:749-776`:

```csharp
[Test]
public void TryProveSampledAlphaIsOne_NormalMapImport_IsNotProven()
{
    var texture = Import(
        "noalpha_normalmap",
        sourceHasAlpha: false,
        configure: importer =>
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.flipGreenChannel = false;
        });

    Assert.That(
        UnityTextureEvidence.TryProveSampledAlphaIsOne(texture),
        Is.False);
}

[Test]
public void TryProveSampledAlphaIsOne_FlippedGreenNormalMapImport_IsNotProven()
{
    var texture = Import(
        "noalpha_normalflip",
        sourceHasAlpha: false,
        configure: importer =>
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.flipGreenChannel = true;
        });

    Assert.That(
        UnityTextureEvidence.TryProveSampledAlphaIsOne(texture),
        Is.False);
}
```

Match the `Import` helper's real parameter list. Both tests are RED: today the predicate returns true, because the source has no alpha and `alphaSource` is `None`.

- [ ] **Step 3: Add the texture-type exclusion**

In `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`, inside the importer arm of `TryProveSampledAlphaIsOne` (lines 207-210):

```csharp
if (TryGetTextureImporter(texture, out var importer))
{
    if (importer.textureType == TextureImporterType.NormalMap)
    {
        // The desktop normal-map conversion swizzles the normal x
        // component into the alpha channel. The sampled alpha is that
        // component, not one, whatever the source alpha facts say.
        return false;
    }

    return !importer.DoesSourceTextureHaveAlpha() &&
           importer.alphaSource == TextureImporterAlphaSource.None;
}
```

Update the method doc: a normal-map import is never proven, because the conversion writes the normal x component into alpha.

Then decide the secondary question from the probe. If the probe showed a stale `sRGBTexture` flag producing an sRGB-named built format on the normal-map import, extend `TryGetColorInterpretation` in the same file so the importer arm returns false for a normal-map import whose built format names sRGB:

```csharp
if (TryGetTextureImporter(texture, out var importer))
{
    if (importer.textureType == TextureImporterType.NormalMap &&
        UnityEngine.Experimental.Rendering.GraphicsFormatUtility
            .IsSRGBFormat(texture.graphicsFormat))
    {
        return false;
    }

    interpretation = importer.sRGBTexture
        ? TextureColorInterpretation.Srgb
        : TextureColorInterpretation.Linear;
    return true;
}
```

If the probe showed the conversion always produces a linear format, record that fact in the task notes and change nothing here.

- [ ] **Step 4: Run the tests to verify they pass**

Run `UnityTextureEvidenceTests` in the Unity Test Runner, EditMode. Verify the two new tests pass and every existing predicate test passes, including `TryProveSampledAlphaIsOne_SourceWithoutAlpha_IsProven` at line 647, which uses the default texture type.

---

### Task 6: Make Aniso Level 1 Mode-Dependent (Finding 31)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs`

**Interfaces:**
- Consumes: `QualitySettings.anisotropicFiltering`.
- Produces: `TryGetSampling` reports `TextureAnisoMode.Anisotropic` for `anisoLevel == 1` exactly when the quality mode is Forced On or Enable On Build.

- [ ] **Step 1: Run the probe and record the outcome**

Before any code change, run a scratch editor script through the editor's C# execution surface. The script sets `QualitySettings.anisotropicFiltering` to Forced On and to Per Texture in turn. For each mode it blits a generated level-1 texture through a strongly anisotropic footprint and reads the sampled value back, comparing it against a reference blit whose sampling is known. Record the observed comparison per mode in this task's notes with the date. Delete the script.

Decision gate: if the probe confirms anisotropic sampling at level 1 under Forced On, continue with this task. If the probe shows level 1 stays bilinear under Forced On, stop this task. Record the finding as refuted by the probe in the task notes, and change nothing except the gate comment, which then documents the confirmed level-1 behavior.

- [ ] **Step 2: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs`, next to the existing aniso tests at lines 496-512:

```csharp
[Test]
public void TryGetSampling_LevelOneUnderForcedOn_IsAnisotropic()
{
    var original = QualitySettings.anisotropicFiltering;
    try
    {
        QualitySettings.anisotropicFiltering =
            AnisotropicFiltering.ForcedOn;
        var texture = Import("aniso_level1_forced", sourceHasAlpha: false);
        texture.anisoLevel = 1;

        Assert.That(
            UnityTextureEvidence.TryGetSampling(texture, out var sampling),
            Is.True);
        Assert.That(
            sampling.Aniso,
            Is.EqualTo(TextureAnisoMode.Anisotropic));
    }
    finally
    {
        QualitySettings.anisotropicFiltering = original;
    }
}
```

Add the three twins with the same body and the mode swapped:
- `TryGetSampling_LevelOneUnderPerTexture_IsNone`, asserting `TextureAnisoMode.None` under `AnisotropicFiltering.PerTexture`.
- `TryGetSampling_LevelOneUnderDisable_IsNone`, asserting `TextureAnisoMode.None` under `AnisotropicFiltering.Disable`.
- `TryGetSampling_LevelOneUnderEnableOnBuild_IsAnisotropic`, asserting `TextureAnisoMode.Anisotropic` under `AnisotropicFiltering.EnableOnBuild`.

Match the `TextureSampling` accessor name for the aniso member. The Forced On and Enable On Build tests are RED: the gate maps level 1 to none today.

- [ ] **Step 3: Make the gate mode-dependent**

In `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`, replace lines 146-148:

```csharp
var aniso = texture.anisoLevel > 1 ||
            (texture.anisoLevel == 1 && LevelOneSamplesAnisotropically())
    ? TextureAnisoMode.Anisotropic
    : TextureAnisoMode.None;
```

Add the helper with its doc comment from spec section 3.6:

```csharp
private static bool LevelOneSamplesAnisotropically()
{
    var mode = QualitySettings.anisotropicFiltering;
    return mode == AnisotropicFiltering.ForcedOn ||
           mode == AnisotropicFiltering.EnableOnBuild;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run `UnityTextureEvidenceTests` in the Unity Test Runner, EditMode. Verify the four new tests pass and every existing sampling test passes, including the level-2 and level-16 tests at lines 496-512.

---

### Task 7: Anchor the LTCGI Strip to the SubShader Block (Finding 41)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`

**Interfaces:**
- Consumes: the line array inside `AnalyzeCanonicalization`.
- Produces: `RemoveAppendedLtcgiTagToken` runs only on Tags lines whose innermost open brace scope is a SubShader block.

- [ ] **Step 1: Update the pinned test and write the falsifiers**

`[SOURCE]` `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:617-625` bakes the unscoped strip with a bare Tags line. Updating it is mandated, not a test weakening.

Replace `Canonicalize_AppendedLtcgiTagToken_IsRemoved` with:

```csharp
[Test]
public void Canonicalize_AppendedLtcgiTagTokenInsideSubShader_IsRemoved()
{
    const string clean =
        "Shader \"s\" {\n" +
        "    SubShader {\n" +
        "        Tags {\"RenderType\" = \"Opaque\" \"Queue\" = \"Geometry\"}\n" +
        "    }\n" +
        "}\n";
    const string tagged =
        "Shader \"s\" {\n" +
        "    SubShader {\n" +
        "        Tags {\"RenderType\" = \"Opaque\" \"Queue\" = \"Geometry\" \"LTCGI\"=\"ALWAYS\"}\n" +
        "    }\n" +
        "}\n";

    Assert.That(Canon(tagged), Is.EqualTo(Canon(clean)));
}
```

Add the two falsifiers:

```csharp
[Test]
public void Canonicalize_AppendedLtcgiTagTokenOutsideSubShaderScope_IsRetained()
{
    const string tagged =
        "        Tags {\"RenderType\" = \"Opaque\" \"LTCGI\"=\"ALWAYS\"}\n";

    Assert.That(Canon(tagged), Does.Contain("\"LTCGI\"=\"ALWAYS\""));
}

[Test]
public void Canonicalize_PassTagsLineWithLtcgiToken_IsRetained()
{
    const string clean =
        "Shader \"s\" {\n" +
        "    SubShader {\n" +
        "        Pass {\n" +
        "            Tags {\"LightMode\" = \"ForwardBase\"}\n" +
        "        }\n" +
        "    }\n" +
        "}\n";
    const string tagged =
        "Shader \"s\" {\n" +
        "    SubShader {\n" +
        "        Pass {\n" +
        "            Tags {\"LightMode\" = \"ForwardBase\" \"LTCGI\"=\"ALWAYS\"}\n" +
        "        }\n" +
        "    }\n" +
        "}\n";

    Assert.That(Canon(tagged), Is.Not.EqualTo(Canon(clean)));
}
```

Run all three plus the retained-token falsifiers at lines 629-640 in the Unity Test Runner, EditMode. Verify the updated inside test fails, because today the strip ignores scope, and record the outcome.

- [ ] **Step 2: Compute the strip scope**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`, add the walker from spec section 3.7:

```csharp
private static bool[] ComputeLtcgiTagStripScope(string[] lines)
{
    var eligible = new bool[lines.Length];
    var scopeKinds = new List<string>();
    for (var i = 0; i < lines.Length; i++)
    {
        var trimmed = lines[i].Trim();
        var isComment = trimmed.StartsWith("//", StringComparison.Ordinal);
        var firstToken = trimmed.Length == 0 || isComment
            ? string.Empty
            : trimmed.Split(' ')[0];

        eligible[i] = !isComment &&
            trimmed.StartsWith("Tags {", StringComparison.Ordinal) &&
            scopeKinds.Count > 0 &&
            scopeKinds[scopeKinds.Count - 1] == "SubShader";

        if (isComment)
        {
            continue;
        }

        var opens = CountCharacter(trimmed, '{');
        var closes = CountCharacter(trimmed, '}');
        for (var open = 0; open < opens; open++)
        {
            scopeKinds.Add(
                firstToken == "SubShader" && open == 0
                    ? "SubShader"
                    : "other");
        }
        for (var close = 0; close < closes; close++)
        {
            if (scopeKinds.Count > 0)
            {
                scopeKinds.RemoveAt(scopeKinds.Count - 1);
            }
        }
    }

    return eligible;
}

private static int CountCharacter(string text, char character)
{
    var count = 0;
    foreach (var candidate in text)
    {
        if (candidate == character)
        {
            count++;
        }
    }

    return count;
}
```

Add the doc comment from spec section 3.7 on `ComputeLtcgiTagStripScope`, including the under-strip fail-closed sentence.

- [ ] **Step 3: Gate the emit loop on the scope**

In `AnalyzeCanonicalization`, compute the scope before the emit loop and use it in the append:

```csharp
var ltcgiTagStripScope = ComputeLtcgiTagStripScope(lines);
```

```csharp
builder.Append(
    NormalizeIncludeLine(
        ltcgiTagStripScope[i]
            ? RemoveAppendedLtcgiTagToken(line, trimmed)
            : line,
        shaderDirectory, projectRoot, includeTree));
```

- [ ] **Step 4: Run the tests to verify they pass**

Run `LilToonAttestationTests` in the Unity Test Runner, EditMode. Verify the three new or updated tests pass, both retained-token falsifiers at lines 629-640 still pass, and every digest and canonicalization test passes.

---

### Task 8: Return a Refusal Instead of the Opaque Profile (Finding 43)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToonSourceAttestationTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`

**Interfaces:**
- Consumes: the pinned profile table and `Gather`.
- Produces: `TryGatherSourceEvidenceForShaderName(Shader, CapturedMaterialEvidence, out LilToonSourceEvidence, out LilToonSemanticDiagnostic)`. No gather runs for an unmatched shader name.

- [ ] **Step 1: Write the failing test and update the pinned signature**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToonSourceAttestationTests.cs`:

```csharp
[Test]
public void TryGatherSourceEvidenceForShaderName_UnknownShaderName_ReturnsFalseWithNamedRefusal()
{
    // Capture a stand-in material under the standard cutout request, the
    // way LilToonOpaqueTargetTests.cs:890-908 builds its captures. The
    // stand-in shader's name matches no pinned profile.
    var source = ConversionEligibleStandIn();
    var captured = UnityMaterialEvidenceCapture.Capture(new[]
    {
        new MaterialEvidenceCaptureInput(
            source, LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
    })[0];

    var matched = LilToonSourceAttestation
        .TryGatherSourceEvidenceForShaderName(
            source.shader, captured, out var evidence, out var refusal);

    Assert.That(matched, Is.False);
    Assert.That(evidence, Is.Null);
    Assert.That(refusal, Is.Not.Null);
    Assert.That(
        refusal.Code,
        Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShader));
}
```

Reuse that file's own stand-in helper in place of `ConversionEligibleStandIn` if the name differs. This test does not compile against the current API. That compile failure is the RED: the current method returns evidence for any name and has no refusal channel.

Update the pinned test at `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs:890-908` to the Try signature. The matched path returns the same evidence, so the assertion `Assert.That(targetEvidence.ShaderName, Is.EqualTo(target.name))` survives unchanged.

- [ ] **Step 2: Replace the lookup and the gather entry**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`:
- Replace `ProfileForShaderName` (lines 1746-1772) with:

```csharp
private static bool TryGetProfileForShaderName(
    string shaderName,
    out LilToonSourceProfile profile)
{
    profile = null;
    if (string.Equals(shaderName, OutlineShaderName, StringComparison.Ordinal))
    {
        profile = OutlineOpaqueProfile;
        return true;
    }
    // ... one arm per pinned name, in the existing order ...
    if (string.Equals(shaderName, TransparentShaderName, StringComparison.Ordinal))
    {
        profile = TransparentProfile;
        return true;
    }

    return false;
}
```

- Replace `GatherSourceEvidenceForShaderName` (lines 1818-1825):

```csharp
internal static bool TryGatherSourceEvidenceForShaderName(
    Shader shader,
    CapturedMaterialEvidence captured,
    out LilToonSourceEvidence evidence,
    out LilToonSemanticDiagnostic refusal)
{
    if (shader == null) throw new ArgumentNullException(nameof(shader));
    if (captured == null)
    {
        throw new ArgumentNullException(nameof(captured));
    }

    evidence = null;
    if (!TryGetProfileForShaderName(shader.name, out var profile))
    {
        refusal = MaterialDiagnostic(
            LilToonSemanticDiagnosticCode.UnsupportedShader,
            $"shader name '{shader.name}'");
        return false;
    }

    refusal = null;
    evidence = Gather(shader, captured, profile, shader.name);
    return true;
}
```

Keep `MaterialDiagnostic` exactly as `VerifyFamily` builds it at lines 1735-1739.

- [ ] **Step 3: Update the production caller**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:437-445`, replace the gather call:

```csharp
if (!LilToonSourceAttestation.TryGatherSourceEvidenceForShaderName(
        target, evidence, out var targetEvidence, out var diagnostic))
{
    throw new InvalidOperationException(
        "The attested lilToon opaque target failed source " +
        "attestation: " + diagnostic?.Detail);
}
```

The verify call and its existing throw stay unchanged below.

- [ ] **Step 4: Run the tests to verify they pass**

Run `LilToonSourceAttestationTests` and `LilToonOpaqueTargetTests` in the Unity Test Runner, EditMode. Verify the new refusal test passes, the updated pinned test passes, and every conversion and attestation test passes.

---

### Task 9: Try and Finally around the Clone Body (Finding 42)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`

**Interfaces:**
- Consumes: `UnityEngine.Object.DestroyImmediate`.
- Produces: `PrepareCanonicalOpaqueClone(Material, Shader)` destroys the clone on every throw after creation.

- [ ] **Step 1: Pin the named failures as still leak-free**

`[INFERENCE]` No RED test is reachable through compliant inputs. No compliant input throws unexpectedly inside the clone body. That is the finding's own point: the hazard needs an abnormal failure such as a capture request defect. This task's assertions are characterization, stated as such, and the structural finally is the change gate for the unexpected path.

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`, follow the `LoadedMaterialCount` pattern at `LilToonOpaqueTargetTests.cs:870-880`. Add:

```csharp
[Test]
public void PrepareCanonicalOpaqueClone_ReadBackFailure_DestroysTheClone()
```

The test drives the inner overload with a source whose clone reads back non-canonical, asserts the `InvalidOperationException`, and asserts `LoadedMaterialCount()` equals the count before the call. Add the twin `PrepareCanonicalOpaqueClone_TargetMismatch_DestroysTheClone`. If an equivalent pin already exists, extend it with the count assertion instead of duplicating it.

Run both in the Unity Test Runner, EditMode. Record the pass. They pass before the change, because the named paths destroy explicitly. That is expected and stated.

- [ ] **Step 2: Wrap the body in try and finally**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:231-352`, restructure `PrepareCanonicalOpaqueClone(Material source, Shader attestedTarget)`:

```csharp
var clone = new Material(source);
var completed = false;
try
{
    clone.name = string.Empty;
    clone.shader = attestedTarget;
    foreach (var (property, value) in CanonicalOpaqueTuple)
    {
        clone.SetFloat(property, value);
    }

    clone.renderQueue = CanonicalOpaqueRenderQueue;
    clone.SetOverrideTag(RenderTypeTagName, CanonicalOpaqueRenderType);

    if (attestedTarget == source.shader)
    {
        clone.SetFloat("_TransparentMode", 0f);
        WriteMultiModeZeroKeywordSet(clone);
    }
    else
    {
        clone.DisableKeyword("UNITY_UI_ALPHACLIP");
        clone.DisableKeyword("UNITY_UI_CLIP_RECT");
        clone.DisableKeyword("ETC1_EXTERNAL_ALPHA");
        clone.DisableKeyword("_COLOROVERLAY_ON");
    }

    if (TryFindNonCanonicalFact(clone, out var fact))
    {
        throw new InvalidOperationException(
            "Generated opaque material did not read back canonical '" +
            fact + "'.");
    }

    if (clone.shader != attestedTarget)
    {
        throw new InvalidOperationException(
            "Generated opaque material did not take the attested " +
            "opaque target shader.");
    }

    completed = true;
    return clone;
}
finally
{
    if (!completed)
    {
        UnityEngine.Object.DestroyImmediate(clone);
    }
}
```

Delete the three now-redundant destroy calls: the non-canonical read-back site at lines 294-297, the target mismatch site at lines 302-305, and the keyword read-back site at lines 354-357 inside `WriteMultiModeZeroKeywordSet`. Each keeps its throw and its message.

Update the exception doc on the inner overload: the finally after clone creation destroys the clone on every throw, including the four named failures and any unexpected failure. Update the `WriteMultiModeZeroKeywordSet` doc: the failure throws, and the caller's finally destroys the clone.

- [ ] **Step 3: Run the tests to verify they pass**

Run `LilToonOpaqueTargetTests` in the Unity Test Runner, EditMode. Verify the two leak pins pass, every conversion test passes, and the material counts stay flat across every conversion test.

---

### Task 10: Route the Opaque Reads through the Captured Evidence (Finding 16)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonBaseColorTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/AlphaFieldSetTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAlphaMaskSemanticsTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonLayerAlphaTermTests.cs`

**Interfaces:**
- Consumes: `TextureEvidenceKinds`, `CapturedTextureEvidence`, `MaterialEvidenceRequest.Combine`.
- Produces: `LilToonMaterialSemantics.FullMaterialEvidenceRequest`, `UnityTextureEvidence.TryProveColorValuesInUnitRange`, and `InterpretVerifiedMaterialFromEvidence`.

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonBaseColorTests.cs`, following the `AlphaEvidenceRequest_MatchesTheIndependentExactSchema` pattern of `Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs:123-128`:

```csharp
[Test]
public void FullMaterialEvidenceRequest_MatchesTheIndependentExactSchema()
{
    var request = LilToonMaterialSemantics.FullMaterialEvidenceRequest;

    Assert.That(request.ShaderName, Is.True);
    Assert.That(request.ActiveColorSpace, Is.False);
    Assert.That(request.PresenceProperties, Is.EquivalentTo(new[]
    {
        "_MainTexHSVG",
        "_MainTex_ScrollRotate",
        "_DissolveParams",
        "_BumpScale",
    }));
    Assert.That(request.ScalarProperties, Is.SupersetOf(new[]
    {
        "_ShiftBackfaceUV", "_UseParallax", "_UsePOM", "_UseAudioLink",
        "_UseMain2ndTex", "_UseMain3rdTex", "_MainGradationStrength",
        "_UseEmission2nd", "_UseReflection", "_UseMatCap",
        "_UseMatCap2nd", "_UseRim", "_UseRimShade", "_UseGlitter",
        "_UseBacklight", "_EmissionMainStrength", "_EmissionFluorescence",
        "_EmissionUseGrad", "_AudioLink2Emission", "_EmissionParallaxDepth",
        "_UseBump2ndMap", "_UseAnisotropy",
        "_UseEmission", "_EmissionBlendMode", "_EmissionBlend",
        "_EmissionMap_UVMode", "_UseBumpMap", "_BumpScale",
    }));
    Assert.That(request.ColorProperties, Is.EquivalentTo(new[]
    {
        "_Color", "_BackfaceColor", "_EmissionColor",
    }));
    Assert.That(request.VectorProperties, Is.EquivalentTo(new[]
    {
        "_MainTexHSVG", "_MainTex_ScrollRotate", "_DissolveParams",
        "_EmissionBlink", "_EmissionMap_ScrollRotate",
    }));
    Assert.That(
        request.TextureProperties.Select(property => property.Name),
        Is.EquivalentTo(new[]
    // Use the same accessor the AlphaEvidenceRequest_MatchesTheIndependentExactSchema
    // pattern tests use for the texture property name. If the request type
    // names the member differently, follow that test.
    {
        "_MainTex", "_MainColorAdjustMask",
        "_EmissionMap", "_EmissionBlendMask", "_BumpMap",
    }));
}
```

Add the exact-count assertion the cutout schema test uses, with the counts from the literal lists above plus the three alpha scalars `Combine` unions in.

Add the snapshot test:

```csharp
[Test]
public void BaseColor_ReadsTheTintFromTheSnapshotNotTheLiveMaterial()
{
    var material = NewBaseColorMaterial("c_snapshot_tint");
    material.SetColor("_Color", new Color(1f, 0f, 0f, 1f));
    var captured = UnityMaterialEvidenceCapture.Capture(new[]
    {
        new MaterialEvidenceCaptureInput(
            material, LilToonMaterialSemantics.FullMaterialEvidenceRequest),
    })[0];
    material.SetColor("_Color", new Color(0f, 1f, 0f, 1f));

    var result = LilToonMaterialSemantics
        .InterpretVerifiedMaterialFromEvidence(
            captured, ColorSpace.Linear, Array.Empty<string>());

    var expectedTint = new Color(1f, 0f, 0f, 1f).linear;

    Assert.That(result.IsSupportedMaterial, Is.True);
    Assert.That(result.Semantics.BaseColor.IsComplete, Is.True);
    Assert.That(
        result.Semantics.BaseColor.Value,
        Is.EqualTo(new Vector3(expectedTint.r, expectedTint.g, expectedTint.b)));
}
```

Match the `SemanticOutput` value accessor to the one the existing base-color assertions use. The expected tint comes from the conversion in the test itself, so the test states the snapshot fact rather than a hardcoded conversion.

Both tests do not compile against the current API. That compile failure is the RED, stated as such: the request constant and the seam do not exist yet.

- [ ] **Step 2: Add the bounded-color-range captured fact**

In `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`:
- Move `TryProveSampledColorInUnitRange` and `BoundedColorFormats` from `LilToonMaterialSemantics.cs:393-445` into this file as `internal static bool TryProveColorValuesInUnitRange(Texture texture)`. Update the justification comment: the predicate is a captured, request-scoped fact with one lilToon consumer today.

In `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs`:
- Add `BoundedColorRange` to `TextureEvidenceKinds` and to `AllEvidence` at lines 27-35.
- Add the fact beside the color interpretation at lines 1358-1361:

```csharp
var hasBoundedColorRange =
    (evidence & TextureEvidenceKinds.BoundedColorRange) != 0 &&
    UnityTextureEvidence.TryProveColorValuesInUnitRange(texture);
```

- Add the bool `colorValuesProvenInUnitRange` to `CapturedTextureEvidence` after `isCanonicalNormalMap`, with the constructor parameter and assignment.
- Update the production `CapturedTextureEvidence` construction with the new value.

Update the test constructor call sites:
- `Packages/com.alrauna.amuse/Tests/Editor/Host/AlphaFieldSetTests.cs:83` uses positional arguments. Insert `false` after the `isCanonicalNormalMap` argument.
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAlphaMaskSemanticsTests.cs:133` and `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonLayerAlphaTermTests.cs:170` use named arguments. They survive the insertion, and each gains an explicit `colorValuesProvenInUnitRange: false` for clarity.

Run the three touched test classes to confirm no regression before continuing.

- [ ] **Step 3: Add the full request and route the reads**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`:
- Add `FullMaterialEvidenceRequest` exactly as spec section 3.10.2 lists it, built with `MaterialEvidenceRequest.Combine(AlphaEvidenceRequest, ...)`.
- Change the two capture sites at lines 141 and 179 to capture under `FullMaterialEvidenceRequest`.
- Change `InterpretBaseColor`, `InterpretEmission`, and `InterpretNormal` to take `CapturedMaterialEvidence` instead of `Material`. Apply the read mapping from spec section 3.10.3:
  - Colors, vectors, scalars: `TryGetColor`, `TryGetVector`, `TryGetScalar`, refusing `UnsupportedFeature` with the property name when the accessor returns false.
  - Presence: `evidence.HasProperty(p)`.
  - Gates: `FirstFailedZeroGate(evidence, gates)` and `TryReadBinary(evidence, p, out b)`.
  - Textures: `evidence.TryGetTexture(p, out var a)` with `a.IsAssigned`.
  - Scale and offset: `a.HasScaleOffset`, `a.Scale`, `a.Offset`, refusing `UnsupportedUv` with the property name when absent.
  - Texture facts: `a.Texture.HasSourceIdentity`, `a.Texture.Sampling`, `a.Texture.HasColorInterpretation`, `a.Texture.SampledAlphaIsProvenOne`, `a.Texture.IsCanonicalNormalMap`, `a.Texture.ColorValuesProvenInUnitRange`.
  - Source identity: `a.Texture.SourceIdentity` where the code built a `TextureSample`.
- Rewrite `TryGetMainUvMapping`, `TryGetEmissionUvMapping`, and `TryGetComposedUvMapping` to take the evidence and the assignment.
- Delete `TryProveSampledColorInUnitRange` and `BoundedColorFormats` from this file.
- Add the seam:

```csharp
internal static LilToonSemanticResult InterpretVerifiedMaterialFromEvidence(
    CapturedMaterialEvidence captured,
    ColorSpace activeColorSpace,
    IReadOnlyCollection<string> compiledFeatures)
```

with the doc contract from spec section 3.10.3. The private `InterpretVerifiedMaterial` overload keeps its shape and calls the same three interpreters with `captured`.

- [ ] **Step 4: Run the tests to verify they pass**

Run in the Unity Test Runner, EditMode: `LilToonBaseColorTests`, `LilToonEmissionTests`, `LilToonNormalTests`, `LilToonAlphaTests`, `LilToonCutoutAlphaTests`, `LilToonTransparentAlphaTests`, `LilToonAlphaMaskSemanticsTests`, `LilToonLayerAlphaTermTests`, `LilToonAdversarialTests`, `AlphaFieldSetTests`, and the Characterization suite (`IrrelevantChangeInvarianceTests`, `UncertaintyMonotonicityTests`, `SharedEvidenceAgreementTests`, `NeutralClaimGatingTests`, `SamplerBlastRadiusTests`). Verify the two new tests pass and every existing test passes.

Finally, run the whole `Tests/Editor/Semantics` and `Tests/Editor/Build` suites once. Verify 0 failures and confirm the filtered runs each reported more than 0 tests. A filtered run that reports 0 tests is a failure.

---

### Task 11: Honor the verifyIdentity Parameter in Every Family Arm (Finding 33)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`

**Interfaces:**
- Consumes: `AnalyzeAlphaMaterialCore(CapturedAlphaMaterialFamily, CapturedAlphaMaterialEvidence, bool verifyIdentity)`, `IsAttestedAlphaMaterial`, `TryVerifyPoiyomiIdentity`
- Produces: one verification gate rule for all five families, gated by the parameter

- [ ] **Step 1: Write the failing test**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`, add `TransferredAnalysis_PoiyomiFamily_HonorsVerifyIdentityParameter`. Build a Poiyomi-family captured material whose attestation evidence is deliberately stale, following the existing Poiyomi captured-evidence helpers in the same suite. Call the transferred analysis twice on the same material. The call with `verifyIdentity: false` returns an analysis. The call with `verifyIdentity: true` refuses with the unattested-shader unknown reason.

```csharp
[Test]
public void TransferredAnalysis_PoiyomiFamily_HonorsVerifyIdentityParameter()
{
    var captured = StalePoiyomiCapturedMaterial();

    var honored = AnalyzeTransferred(captured, verifyIdentity: false);
    var guarded = AnalyzeTransferred(captured, verifyIdentity: true);

    Assert.That(guarded.AlphaUnknownReason.Kind,
        Is.EqualTo(AlphaUnknownKind.UnattestedShader));
    Assert.That(honored.AlphaUnknownReason.Kind,
        Is.Not.EqualTo(AlphaUnknownKind.UnattestedShader));
}
```

RED: both calls refuse today, which proves the parameter is ignored for the Poiyomi family.

- [ ] **Step 2: Run the test to verify it fails**

Run `TransferredAnalysis_PoiyomiFamily_HonorsVerifyIdentityParameter` in the Unity Test Runner, EditMode. Verify the honored call refuses today.

- [ ] **Step 3: Implement the gate**

In `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`, wrap the two Poiyomi verify calls inside `AnalyzeAlphaMaterialCore` in `verifyIdentity &&`, exactly like the three lilToon arms. Do the same for the Poiyomi arms of `IsAttestedAlphaMaterial`. Read the exact member names at lines 1000 to 1013 and 926 to 940 before editing. Update the doc sentence on `AnalyzeAlphaMaterialTransferred` so it states the rule for all five families. The behavior change is neutral today, because the transferred path never grants Poiyomi names.

- [ ] **Step 4: Run the tests to verify they pass**

Run the new test, the existing transferred-analysis tests, and the Poiyomi analysis suites. Verify the new test passes, the strict-attestation suites stay green, and the filtered runs each reported more than 0 tests. A filtered run that reports 0 tests is a failure.
