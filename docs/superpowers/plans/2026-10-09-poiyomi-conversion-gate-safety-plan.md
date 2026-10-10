# Poiyomi Conversion Gate Safety Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Gate the outline blend tuple behind an identity-at-alpha-one admission, refuse a NaN cutoff before the already-opaque classification, thread the second family's alpha pair through the blend proof, memoize the pinned source digest, and mirror the capture-refusal check in the bound-mask route.

**Architecture:** Changes stay inside `Editor/Semantics/Poiyomi/` and the Poiyomi Editor tests. Conversion stays a pure evaluation over captured evidence. The alpha semantics stay the single owner of outline feature facts. Test shaders are the `Hidden/Alrauna/AmuseTests/*` stand-ins. No vendor shader is installed.

**Tech Stack:** C# (Unity 2022.3), Unity Test Framework (EditMode), ShaderLab for the two stand-in shaders.

**Spec:** `docs/superpowers/specs/2026-10-09-poiyomi-conversion-gate-safety-design.md`

## Global Constraints

- Use simple English. Short active sentences. One idea per sentence.
- No semicolons in prose.
- No contractions.
- Never record absolute or machine-specific paths. Use repository-relative paths only.
- Never record host names, user account names, home-directory paths, ports, or Unity MCP instance names.
- Date every status claim. As of 2026-10-09.
- Never stage, commit, push, or modify Git history without explicit user authorization.
- The dev editor instance with `Application.dataPath == <repo-root>/Assets` is the only authorized test instance.
- One test class per production type. Test methods are behavior sentences. Use the NUnit constraint model `Assert.That(actual, Is.EqualTo(expected))`.
- Every test run uses the Unity Test Runner in EditMode with a class filter. A filtered run that reports 0 tests is a failure.
- A first-run-passing assertion is characterization, never RED. Each task below names which new test must fail before the fix and which existing pinned test must keep passing.
- Do not add abstraction layers, mode parameters, or shared gate tables. Each fix is a local change to the named method.

---

### Task 1: Finiteness Sweep Before the No-op Classification (Finding 37)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`

**Interfaces:**
- Consumes: `PoiyomiOpaqueConversion.EvaluateVerifiedEligibility`, `IsFunctionallyOpaque`, the `EvaluateWith` evidence-override seam
- Produces: `ConversionPropertyNotFinite` refusal for a NaN `_Cutoff` on an otherwise canonical material, instead of a false `AlreadyOpaque`

- [ ] **Step 1: Write the failing test**

In `PoiyomiOpaqueConversionTests`, beside the existing `AlreadyOpaque_*` tests:

```csharp
/// <summary>
/// A canonical material whose cutoff is not finite must refuse by
/// name. NaN fails the cutoff comparison inside the no-op
/// classification, so the finiteness sweep must run first.
/// </summary>
[Test]
public void CanonicalMaterialWithNonFiniteCutoff_RefusesAsNotFinite()
{
    var material = MakeCanonical(NewFixtureMaterial());
    material.SetFloat("_EnableOutlines", 0f);

    AssertRefusal(
        EvaluateWith(material, "_Cutoff", float.NaN),
        PoiyomiOpaqueConversionRefusal.ConversionPropertyNotFinite);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the Unity Test Runner in EditMode, filtered to `PoiyomiOpaqueConversionTests`. Confirm the filter selects tests, then confirm `CanonicalMaterialWithNonFiniteCutoff_RefusesAsNotFinite` fails because `EvaluateWith` returns `AlreadyOpaque` today. Confirm `AlreadyOpaque_WhenEveryCanonicalFactMatches` still passes.

- [ ] **Step 3: Reorder the sweep**

In `EvaluateVerifiedEligibility`, move the finiteness loop from step 3 to directly after the schema loop, before the `IsFunctionallyOpaque` call. The order becomes: schema, finiteness, already-opaque, premultiply read, coverage, depth, base RGB, ForwardAdd, clip. Update the method's order comment:

```csharp
// Order is load-bearing: the finiteness sweep is a data-validity
// refusal, so it precedes the no-op classification. Every gate whose
// only purpose is to authorize mutation still follows the no-op
// classification, because a refusal mutates nothing.
```

Leave `IsFunctionallyOpaque` and every other step unchanged.

- [ ] **Step 4: Run tests to verify they pass**

Run the Unity Test Runner in EditMode, filtered to `PoiyomiOpaqueConversionTests`. Verify the filter reports a nonzero count. Verify the new test passes and `NonFiniteConversionProperty_RefusesForEverySchemaProperty`, `PresetOpaque_IsAlreadyOpaque`, and `AuthoredOpaqueMaterialWithShaderDefaults_EvaluatesToAlreadyOpaque` keep passing.

---

### Task 2: Outline Blend Tuple Gate (Finding 3)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Characterization/IrrelevantChangeInvarianceTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`

**Interfaces:**
- Consumes: `OpaqueConversionFactors.BlendOpAdd`, `BlendOpMax`, `BlendFactorOne`, `BlendFactorZero`, `BlendFactorOneMinusSrcAlpha`, `IsUnitSourceFactorAtAlphaOne`, `IsZeroDestinationFactorAtAlphaOne`; the `ConvertibleFade`, `EvaluateFor`, `AssertRefusal`, `AssertConvertible` test helpers
- Produces: `PoiyomiOpaqueConversionRefusal.UnsupportedOutlineBlendEquation` for any outline pair outside the admitted sets; conversion unchanged for admitted pairs

- [ ] **Step 1: Write the failing tests**

In `PoiyomiOpaqueConversionTests`, beside the blend-equation tests:

```csharp
[Test]
public void AdditiveOutlineRgbPair_RefusesWithNamedRefusal()
{
    var material = ConvertibleFade();
    material.SetFloat("_OutlineSrcBlend", 1f);
    material.SetFloat("_OutlineDstBlend", 1f);

    AssertRefusal(
        EvaluateFor(material),
        PoiyomiOpaqueConversionRefusal.UnsupportedOutlineBlendEquation);
}

[Test]
public void AdditiveOutlineAlphaPair_RefusesWithNamedRefusal()
{
    var material = ConvertibleFade();
    material.SetFloat("_OutlineSrcBlendAlpha", 0f);
    material.SetFloat("_OutlineDstBlendAlpha", 1f);

    AssertRefusal(
        EvaluateFor(material),
        PoiyomiOpaqueConversionRefusal.UnsupportedOutlineBlendEquation);
}

[Test]
public void OpaqueEquivalentOutlineRgbPairs_StayConvertible(
    [Values(1f, 5f)] float srcBlend,
    [Values(0f, 10f)] float dstBlend)
{
    var material = ConvertibleFade();
    material.SetFloat("_OutlineBlendOp", 0f);
    material.SetFloat("_OutlineSrcBlend", srcBlend);
    material.SetFloat("_OutlineDstBlend", dstBlend);

    AssertConvertible(EvaluateFor(material));
}

[Test]
public void OpaqueEquivalentOutlineAlphaPairs_StayConvertible(
    [Values(0f, 1f, 10f)] float dstBlendAlpha,
    [Values(0f, 4f)] float blendOpAlpha)
{
    var material = ConvertibleFade();
    material.SetFloat("_OutlineSrcBlendAlpha", 1f);
    material.SetFloat("_OutlineDstBlendAlpha", dstBlendAlpha);
    material.SetFloat("_OutlineBlendOpAlpha", blendOpAlpha);

    AssertConvertible(EvaluateFor(material));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the Unity Test Runner in EditMode, filtered to `PoiyomiOpaqueConversionTests`. Confirm the filter reports a nonzero count. Confirm `AdditiveOutlineRgbPair_RefusesWithNamedRefusal` and `AdditiveOutlineAlphaPair_RefusesWithNamedRefusal` fail because evaluation returns `Convertible` today. The two parametrized admitted-set tests pass today; they are boundary guards that turn RED only if the new gate over-refuses.

- [ ] **Step 3: Add the refusal member and the gate**

In `PoiyomiOpaqueConversion.cs`:

Add to `PoiyomiOpaqueConversionRefusal` after `UnsupportedForwardAddBlendEquation`:

```csharp
UnsupportedOutlineBlendEquation,
```

Insert as step 9 in `EvaluateVerifiedEligibility`, between the ForwardAdd gate and the clip threshold, using the exact block from spec section 3.2:

```csharp
if (Read(values, "_OutlineBlendOp") !=
        OpaqueConversionFactors.BlendOpAdd ||
    !OpaqueConversionFactors.IsUnitSourceFactorAtAlphaOne(
        Read(values, "_OutlineSrcBlend")) ||
    !OpaqueConversionFactors.IsZeroDestinationFactorAtAlphaOne(
        Read(values, "_OutlineDstBlend")) ||
    Read(values, "_OutlineSrcBlendAlpha") !=
        OpaqueConversionFactors.BlendFactorOne ||
    (Read(values, "_OutlineDstBlendAlpha") !=
        OpaqueConversionFactors.BlendFactorZero &&
     Read(values, "_OutlineDstBlendAlpha") !=
        OpaqueConversionFactors.BlendFactorOne &&
     Read(values, "_OutlineDstBlendAlpha") !=
        OpaqueConversionFactors.BlendFactorOneMinusSrcAlpha) ||
    (Read(values, "_OutlineBlendOpAlpha") !=
        OpaqueConversionFactors.BlendOpAdd &&
     Read(values, "_OutlineBlendOpAlpha") !=
        OpaqueConversionFactors.BlendOpMax))
{
    return PoiyomiOpaqueConversionEligibility.Refused(
        PoiyomiOpaqueConversionRefusal.UnsupportedOutlineBlendEquation);
}
```

Renumber the clip-threshold comment to step 10. Do not add any outline property to `ConversionSchema`, `ConversionRequiredSchemaProperties`, or `ConversionEvidenceRequest`.

In `Tests/Editor/Semantics/Characterization/IrrelevantChangeInvarianceTests.cs`, add one comment above the six `_Outline*` entries in `IrrelevantFloats`:

```csharp
// The six _Outline* blend fields stay irrelevant to the semantic
// output. Conversion now gates them, because the canonical recipe
// rewrites the tuple. The semantics layer never reads them, so the
// invariance claim holds.
```

Change no assertion in that file.

- [ ] **Step 4: Run tests to verify they pass**

Run the Unity Test Runner in EditMode, filtered to `PoiyomiOpaqueConversionTests` and then to `IrrelevantChangeInvarianceTests`. Verify both filters report nonzero counts. Verify the four new tests pass, `DisabledOutlines_AreConvertible` keeps passing, `PresetAdditive_RefusesOnBlendEquation` and the other preset tests keep passing, and every invariance test keeps passing. Confirm `ConversionEvidenceRequest_CarriesNoOutlineSymbol` still passes.

---

### Task 3: Second Family Alpha Pair in the Blend Gate (Finding 18)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiSemanticTest.shader`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiTwoPassSemanticTest.shader`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiMultipassRuleTests.cs`

**Interfaces:**
- Consumes: `IsProvenOpaqueBlend`, `CapturedMaterialEvidence.TryGetScalar`, `CreateAlphaEvidenceRequest`
- Produces: the second-family gate reads `_SrcBlendAlpha2` and `_DstBlendAlpha2`; the recorded claim states the destination-alpha caveat

- [ ] **Step 1: Extend the stand-ins, then write the failing test**

In both `PoiyomiSemanticTest.shader` and `PoiyomiTwoPassSemanticTest.shader`, add beside `_BlendOpAlpha2` in the second-pass blend block:

```shaderlab
_SrcBlendAlpha2 ("Alpha Source Blend", Int) = 1
_DstBlendAlpha2 ("Alpha Destination Blend", Int) = 10
```

The defaults mirror the base family's declared defaults `_SrcBlendAlpha = 1` and `_DstBlendAlpha = 10`. The declarations are prerequisites: capture sets `HasValue` from the shader's declared property types, so an undeclared property can never reach the gate.

In `PoiyomiMultipassRuleTests`, beside `AdditiveSecondPassRefusesCompleteAlpha`:

```csharp
/// <summary>
/// The second family's own alpha pair governs its pass. A divergent
/// pair must refuse the claim even when the first family's pair is
/// canonical. Falsifies a proof that rests on the wrong family's
/// properties.
/// </summary>
[Test]
public void DivergentSecondFamilyAlphaPair_RefusesCompleteAlpha()
{
    var material = NewFixtureMaterial();
    material.SetFloat("_AlphaForceOpaque", 1f);
    material.SetFloat("_SrcBlend2", 5f);
    material.SetFloat("_DstBlend2", 10f);
    material.SetFloat("_SrcBlendAlpha2", 0f);
    material.SetFloat("_DstBlendAlpha2", 10f);

    var alpha = Interpret(material);

    Assert.That(alpha.IsComplete, Is.False);
}
```

Also add explicit canonical sets to `StandardAlphaPairsOnBothPassesStayComplete`, after the existing `_SrcBlend2` and `_DstBlend2` lines:

```csharp
material.SetFloat("_SrcBlendAlpha2", 1f);
material.SetFloat("_DstBlendAlpha2", 10f);
```

- [ ] **Step 2: Run tests to verify they fail**

Run the Unity Test Runner in EditMode, filtered to `PoiyomiMultipassRuleTests`. Confirm the filter reports a nonzero count. Confirm `DivergentSecondFamilyAlphaPair_RefusesCompleteAlpha` fails because the gate reads the base pair and completes today. Confirm `StandardAlphaPairsOnBothPassesStayComplete` passes.

- [ ] **Step 3: Thread the pair through the gate**

In `PoiyomiMaterialSemantics.cs`:

Add the two constants beside the existing 2-family constants at lines 57-60:

```csharp
private const string SrcBlendAlpha2Property = "_SrcBlendAlpha2";
private const string DstBlendAlpha2Property = "_DstBlendAlpha2";
```

Change the `IsProvenOpaqueBlend` signature to take seven parameters, adding `string srcBlendAlphaProperty` and `string dstBlendAlphaProperty` after `string blendOpAlphaProperty`. In the body, replace the two reads of `SrcBlendAlphaProperty` and `DstBlendAlphaProperty` with reads of the new parameters. No other body change.

Update the base-family call at lines 1049-1056 to pass `SrcBlendAlphaProperty, DstBlendAlphaProperty` as the sixth and seventh arguments.

Update the second-family call at lines 1071-1077 to pass `SrcBlendAlpha2Property, DstBlendAlpha2Property`.

In `CreateAlphaEvidenceRequest`, add to the scalar set directly after `BlendOpAlpha2Property,`:

```csharp
SrcBlendAlpha2Property,
DstBlendAlpha2Property,
```

Record the destination-alpha caveat in the pass-two gate comment block above the second call:

```csharp
// Destination-alpha caveat: this pair proves the second pass's own
// alpha output is exactly one. The destination factor still decides
// how that output composes with the framebuffer's alpha channel. The
// recorded claim covers the rendered pixel, not the framebuffer
// alpha channel.
```

Record the same caveat above the two new entries in `CreateAlphaEvidenceRequest`.

- [ ] **Step 4: Run tests to verify they pass**

Run the Unity Test Runner in EditMode, filtered to `PoiyomiMultipassRuleTests` and then to `PoiyomiTwoPassAlphaTests` and `PoiyomiAlphaTests`. Verify every filter reports nonzero counts. Verify the new test passes, `StandardAlphaPairsOnBothPassesStayComplete` keeps passing with the explicit sets, and every Two Pass and base alpha test keeps passing.

---

### Task 4: Source Digest Memo (Finding 38)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs` (class `PoiyomiSourceAttestationTests`)

**Interfaces:**
- Consumes: `ComputeNormalizedSourceHash`, `GatherSourceEvidence`
- Produces: `TryReadNormalizedSourceHashCached`, at most one read and one hash per unchanged shader asset per Editor session

- [ ] **Step 1: Write the failing tests**

In `PoiyomiSourceAttestationTests`, beside the `NormalizedHash_*` tests. The class does not derive from `PoiyomiFixtureTestBase`, so it declares its own folder constant and creates the folder before writing. Each test writes its own uniquely named temp file and deletes it in a `finally` block. Add `using System.IO;` at the top of the file, because the file does not import it today:

```csharp
private const string HashMemoFolder = "Assets/AmuseTests_Temp";

[Test]
public void CachedSourceHash_ReturnsTheFirstHashWhileMtimeIsUnchanged()
{
    Directory.CreateDirectory(HashMemoFolder);
    var path = Path.Combine(
        HashMemoFolder,
        "hash_memo_unchanged_" + Guid.NewGuid().ToString("N") + ".shader");
    try
    {
        File.WriteAllText(path, "first source");
        var stamp = File.GetLastWriteTimeUtc(path);
        var first = PoiyomiMaterialSemantics
            .TryReadNormalizedSourceHashCached(path, out var hash);
        Assert.That(first, Is.True);

        File.WriteAllText(path, "second source");
        File.SetLastWriteTimeUtc(path, stamp);

        var second = PoiyomiMaterialSemantics
            .TryReadNormalizedSourceHashCached(path, out var cached);
        Assert.That(second, Is.True);
        Assert.That(
            cached,
            Is.EqualTo(hash),
            "an unchanged write time must reuse the memoized digest");
    }
    finally
    {
        File.Delete(path);
    }
}

[Test]
public void CachedSourceHash_RefreshesWhenFileContentChanges()
{
    Directory.CreateDirectory(HashMemoFolder);
    var path = Path.Combine(
        HashMemoFolder,
        "hash_memo_refresh_" + Guid.NewGuid().ToString("N") + ".shader");
    try
    {
        File.WriteAllText(path, "first source");
        PoiyomiMaterialSemantics.TryReadNormalizedSourceHashCached(
            path, out _);

        File.WriteAllText(path, "second source");
        File.SetLastWriteTimeUtc(
            path, File.GetLastWriteTimeUtc(path).AddSeconds(2));

        var refreshed = PoiyomiMaterialSemantics
            .TryReadNormalizedSourceHashCached(path, out var hash);
        Assert.That(refreshed, Is.True);
        Assert.That(
            hash,
            Is.EqualTo(PoiyomiMaterialSemantics
                .ComputeNormalizedSourceHash("second source")),
            "a changed write time must refresh the memo");
    }
    finally
    {
        File.Delete(path);
    }
}

[Test]
public void CachedSourceHash_MissingFileThrows()
{
    Directory.CreateDirectory(HashMemoFolder);
    var path = Path.Combine(
        HashMemoFolder,
        "hash_memo_missing_" + Guid.NewGuid().ToString("N") + ".shader");

    Assert.Throws<FileNotFoundException>(() =>
        PoiyomiMaterialSemantics.TryReadNormalizedSourceHashCached(
            path, out _));
}
```

`HashMemoFolder` is the existing transient test folder, recorded as a repository-relative path.

- [ ] **Step 2: Run tests to verify they fail**

Run the Unity Test Runner in EditMode, filtered to `PoiyomiSourceAttestationTests`. Confirm the filter reports a nonzero count. Confirm all three new tests fail to compile or fail because `TryReadNormalizedSourceHashCached` does not exist today. That is the RED state: the memoized behavior does not exist.

- [ ] **Step 3: Add the memo**

In `PoiyomiMaterialSemantics.cs`, add the memo field and helper from spec section 3.4 beside `ComputeNormalizedSourceHash`, with the documented summary. Then replace the read-and-hash body inside `GatherSourceEvidence`'s existing `try` block:

```csharp
try
{
    if (File.Exists(assetPath) &&
        TryReadNormalizedSourceHashCached(assetPath, out var cachedHash))
    {
        normalizedHash = cachedHash;
        hasReadableSource = true;
    }
}
```

Keep both `catch` blocks exactly as they are. `File.GetLastWriteTimeUtc` and `File.ReadAllText` throw the same exception family, so a failed attempt stays uncached and keeps today's behavior. Do not add locking; capture runs on the Editor main thread.

- [ ] **Step 4: Run tests to verify they pass**

Run the Unity Test Runner in EditMode, filtered to `PoiyomiSourceAttestationTests`. Verify the filter reports a nonzero count. Verify the three new tests pass and every `Identity_*` and `NormalizedHash_*` test keeps passing.

---

### Task 5: Named Mask Capture Refusal (Finding 39)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiAlphaMaskTests.cs`

**Interfaces:**
- Consumes: `TryInterpretAlphaMask`, `CapturedTextureEvidence.RedCaptureRefusal`, `TextureCaptureRefusalReason`, the `BoundMaskMaterial`, `ImportTexture`, `Interpret`, `AssertUnsupportedOutput` test helpers
- Produces: `UnsupportedFeature` naming `_AlphaMask` when the red capture refused, in both red-reading arms; the saturating arm unchanged

- [ ] **Step 1: Write the failing tests**

In `PoiyomiAlphaMaskTests`, beside the bound-mask tests. The mask import uses `ARGB4444`, which sits outside the capture format allowlist in `UnityAlphaFieldEvidence.IsAdmittedFormat`, so the red capture refuses with `UnsupportedFormat` while the source identity stays intact:

```csharp
[Test]
public void ReplaceBoundMask_RefusedRedCapture_IsUnsupportedFeatureNamingTheMask()
{
    var material = BoundMaskMaterial();
    material.SetTexture(
        Mask,
        ImportTexture(
            "bound_mask_refused_red",
            i => i.textureFormat = TextureImporterFormat.ARGB4444));

    AssertUnsupportedOutput(
        Interpret(material),
        PoiyomiSemanticOutput.Alpha,
        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
        Mask);
}

[Test]
public void ReplaceBoundMask_AffineArm_RefusedRedCapture_IsUnsupportedFeatureNamingTheMask()
{
    var material = BoundMaskMaterial();
    material.SetFloat(BlendStrength, 1f);
    material.SetFloat(MaskValue, 0.5f);
    material.SetTexture(
        Mask,
        ImportTexture(
            "bound_mask_affine_refused_red",
            i => i.textureFormat = TextureImporterFormat.ARGB4444));

    AssertUnsupportedOutput(
        Interpret(material),
        PoiyomiSemanticOutput.Alpha,
        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
        Mask);
}

[Test]
public void ReplaceBoundMask_SaturatedPair_IgnoresRefusedRedCapture()
{
    var material = BoundMaskMaterial();
    material.SetFloat(BlendStrength, 1f);
    material.SetFloat(MaskValue, 1f);
    material.SetTexture(
        Mask,
        ImportTexture(
            "bound_mask_saturated_refused_red",
            i => i.textureFormat = TextureImporterFormat.ARGB4444));

    var alpha = Interpret(material);

    Assert.That(alpha.IsComplete, Is.True);
}
```

Use the file's own property-name constants for `BlendStrength` and `MaskValue`; if the file names them differently, follow the file.

- [ ] **Step 2: Run tests to verify they fail**

Run the Unity Test Runner in EditMode, filtered to `PoiyomiAlphaMaskTests`. Confirm the filter reports a nonzero count. Confirm the first two tests fail because no diagnostic names `_AlphaMask` today: the route builds the red-field term and the missing capture surfaces later, unnamed, at the fold. Confirm `ReplaceBoundMask_AdmittedPair_ProvesThroughTheRedField` keeps passing.

- [ ] **Step 3: Mirror the sibling check**

In `TryInterpretAlphaMask`, add the same guard in both red-reading arms, immediately after each arm's existing main-sampling and parallax gates and before the `maskSample` construction:

```csharp
if (mask.Texture.RedCaptureRefusal !=
    TextureCaptureRefusalReason.None)
{
    AddDiagnostic(
        diagnostics,
        PoiyomiSemanticOutput.Alpha,
        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
        AlphaMaskProperty);
    return false;
}
```

The first site is the `(1, 0)` arm. The second site is the affine `(1, value < 1, invert off)` arm. Do not add the guard to the saturating `(1, value >= 1)` arm, because that arm consults no texel of the mask. Do not change the `UnstableTextureIdentity` check. Do not add any guard to the unassigned-mask arm.

- [ ] **Step 4: Run tests to verify they pass**

Run the Unity Test Runner in EditMode, filtered to `PoiyomiAlphaMaskTests`. Verify the filter reports a nonzero count. Verify the three new tests pass and `ReplaceBoundMask_AdmittedPair_ProvesThroughTheRedField`, `ReplaceBoundMask_WithHole_ClassifiesTheHoleUnknown`, `ReplaceBoundMask_InvertOnValueZero_ProvesTheInvertedField`, and every unassigned-mask test keep passing.
