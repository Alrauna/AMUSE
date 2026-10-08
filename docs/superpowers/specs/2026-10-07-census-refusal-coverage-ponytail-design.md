# Census Refusal Coverage Ponytail Review Design

Date: 2026-10-07. Base: `main` at `035d5b2`, branch `feat/census-refusal-coverage`, review point `1dcc9b8`.

Investigation: `docs/superpowers/investigations/2026-10-07-census-refusal-coverage-ponytail-investigation.md`.

## 1. Overview

This design applies the eleven complexity cuts from the ponytail review of branch `feat/census-refusal-coverage`. The branch loses 91 lines.

All eleven cuts are behavior-preserving. No public API contract changes. No shader semantics equation changes. No build refusal reason changes. No report formatting changes. The cuts eliminate redundant loops, unnecessary helper methods, duplicate code branches, and dead test computation.

## 2. Host and Animation Evidence Capture

### 2.1 Combine batch outcome loops in UnityAnimationEvidenceCapture

In `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`, lines 717 to 739 process unattested and surviving materials through two separate loops. The first loop handles unattested items. The second loop handles survivor items.

The design combines both passes into one loop:

```csharp
var failedOrdinals = new HashSet<int>(batchOutcome.UnattestedOrdinals);
var survivorOrdinal = 0;
for (var index = 0; index < attestedIndices.Count; index++)
{
    var admittedIndex = attestedIndices[index];
    if (failedOrdinals.Contains(index))
    {
        var lockedRefusal = lockedRefusalCheck?.Invoke(
            admitted[admittedIndex]) ?? RendererAnalysisRefusal.None;
        capturedByIndex[admittedIndex] =
            UnityMaterialSemantics.UnattestedMaterial(
                lockedRefusal,
                admitted[admittedIndex],
                resolveRegisteredSource);
    }
    else
    {
        capturedByIndex[admittedIndex] =
            batchOutcome.Captured[survivorOrdinal++];
    }
}
```

This rewrite replaces two separate passes over `attestedIndices` with one single pass. It preserves the exact ordering and array indices of `capturedByIndex`.

### 2.2 Streamline survivor filtering in UnityAnimationEvidenceCaptureTests

In `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`, lines 2149 to 2172 clone `batch.Captured`, delete items at failed ordinals, re-insert unattested sentinels, and then filter against `failedSet`.

The design replaces that mutation dance with direct LINQ filtering:

```csharp
var failedSet = new HashSet<int>(failedOrdinals);
var survivors = batch.Captured
    .Where((_, index) => !failedSet.Contains(index))
    .ToList();
outcome = new ClosedAlphaCaptureOutcome(
    survivors, failedOrdinals);
return true;
```

This change yields the identical list of survivors without temporary mutations.

## 3. Material Semantics and Poiyomi

### 3.1 Delegate plain batch capture to transferred capture in UnityMaterialSemantics

In `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`, `TryCaptureClosedAlphaMaterials` (lines 304 to 343) duplicates the entire request array construction, batch capture call, and outcome partitioning loop present in `TryCaptureClosedAlphaMaterialsTransferred` (lines 357 to 409).

The design replaces the body of `TryCaptureClosedAlphaMaterials` with a single delegation call:

```csharp
internal static bool TryCaptureClosedAlphaMaterials(
    IReadOnlyList<Material> materials,
    IReadOnlyList<CapturedAlphaMaterialFamily> families,
    MaterialEvidenceRequest request,
    AlphaPolicyBounds bounds,
    out ClosedAlphaCaptureOutcome outcome,
    RegisteredSourceLookup resolveRegisteredSource = null)
{
    return TryCaptureClosedAlphaMaterialsTransferred(
        materials,
        families,
        request,
        bounds,
        System.Array.Empty<string>(),
        out outcome,
        resolveRegisteredSource);
}
```

When `grantedShaderNames` is empty, `Enumerable.Contains` returns false for every member. The transferred overload then executes the exact attestation check that the plain overload performed.

### 3.2 Merge duplicate outline factor constant branches in PoiyomiMaterialSemantics

In `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`, lines 1504 to 1512 contain two adjacent branches that both set `outlineFactor = ScalarSemanticValue.Constant(1f)`.

The design combines the two checks into a single `if` statement:

```csharp
ScalarSemanticValue outlineFactor;
if (!outlineTexture.IsAssigned ||
    (outlineTexture.Texture != null &&
     outlineTexture.Texture.SampledAlphaIsProvenOne))
{
    outlineFactor = ScalarSemanticValue.Constant(1f);
}
else
{
    // ... existing UV channel evaluation and texture sampling ...
}
```

The resulting factor is unchanged.

### 3.3 Extract common MappedTexture construction in PoiyomiMaterialSemantics

In `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`, lines 2098 to 2107 construct `ScalarSemanticValue.MappedTexture(maskSample, TextureChannel.Red, mapped)` identically in two branches.

The design constructs the mapped texture value once:

```csharp
var factor = ScalarSemanticValue.MappedTexture(
    maskSample, TextureChannel.Red, mapped);
if (replace)
{
    replacement = factor;
}
else
{
    multiplier = factor;
}
```

This change avoids duplicating the constructor call and its arguments.

### 3.4 Inline single-caller assertion helper in PoiyomiOutlineAlphaSemanticsTests

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOutlineAlphaSemanticsTests.cs`, the private method `AssertUnsupportedOutputIsAbsent` (lines 21 to 27) has only one caller at line 39.

The design inlines the assertion into the test method:

```csharp
Assert.That(
    result.Diagnostics.Any(d => d.Output == PoiyomiSemanticOutput.Alpha),
    Is.False,
    "Alpha output must not emit a diagnostic.");
```

The private helper method is deleted.

## 4. Build Lifecycle and Identity

### 4.1 Inline RecordedListedOriginalName in LockedMaterialIdentity

In `Packages/com.alrauna.amuse/Editor/Build/LockedMaterialIdentity.cs`, private helper `RecordedListedOriginalName` (lines 246 to 253) is called only by `GrantedAwareOriginalAttestation` at line 242.

The design inlines the evaluation into the lambda:

```csharp
internal static Func<Material, bool> GrantedAwareOriginalAttestation(
    Func<Material, bool> baseAttestation,
    IReadOnlyCollection<string> grantedShaderNames)
{
    if (grantedShaderNames == null || grantedShaderNames.Count == 0)
    {
        return baseAttestation;
    }

    return material =>
    {
        if (baseAttestation != null
            ? baseAttestation(material)
            : OriginalShaderAttested(material))
        {
            return true;
        }

        var original = RecordedOriginalShaderName(material);
        return !string.IsNullOrEmpty(original) &&
               grantedShaderNames.Contains(original);
    };
}
```

The private helper method `RecordedListedOriginalName` is deleted.

### 4.2 Simplify grantedShaderNames argument in AmusePlatformFinishPlugin

In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`, lines 819 to 821 use a ternary expression:

```csharp
transferShaders
    ? shaderTransfer.GrantedShaderNames
    : null
```

Line 492 assigns `grantedShaderNames` to `shaderTransfer.GrantedShaderNames` if its count exceeds zero, or `null` otherwise. Line 528 sets `transferShaders = grantedShaderNames != null`. Therefore, the ternary expression always equals `grantedShaderNames`.

The design passes `grantedShaderNames` directly:

```csharp
RetainPreparedSeparation(
    state,
    extraction.MutationTarget,
    rendererPath,
    plan,
    evidence,
    admittedLiveMaterials,
    poiyomiConversion,
    lilToonConversion,
    minimumOpaqueCoveragePercent,
    rendererTypeName,
    allowDepthTestChange,
    grantedShaderNames);
```

### 4.3 Inline ClosureFailureWayFor in AmusePlatformFinishPlugin

In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`, `ClosureFailureWayFor` (lines 1398 to 1407) has one caller at line 842.

The design inlines the first failure lookup directly at line 841:

```csharp
closureWaySentence:
refusal == RendererAnalysisRefusal.MaterialDependencyClosureFailed &&
evidence.SlotClosureFailures.Count > 0
    ? AmuseReports.ClosureFailureSentence(
        evidence.SlotClosureFailures[0].Failure)
    : null);
```

The private helper method `ClosureFailureWayFor` is deleted.

### 4.4 Remove manual zeroing loop in AlphaSeparationSplitTests

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationSplitTests.cs`, lines 723 to 726 iterate through a newly allocated `Color32` array to set every element to `new Color32(0, 0, 0, 0)`.

In C#, elements of a new `Color32` struct array default to `(0, 0, 0, 0)`.

The design deletes the loop:

```csharp
private static Texture2D ImportSplitHollowTexture(string name)
{
    var pixels = new Color32[16];

    return TestTextureImport.WritePng(
        SplitTempFolder + "/" + name + ".png",
        4,
        4,
        pixels,
        // ...
```

### 4.5 Use Enumerable.Range in AmusePlatformFinishPluginTests

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`, lines 5154 to 5158 populate an array with sequential indices using a loop.

The design replaces the loop with standard library LINQ:

```csharp
var ordinals = System.Linq.Enumerable.Range(0, materials.Count).ToArray();
```

## 5. Non-Goals

- No changes to public APIs or data structures.
- No changes to shader attestation tables, hashes, or digests.
- No changes to analysis rules or threshold math.
- No changes to report sentences or log formats.
- No deletions of tests or test assertions.
