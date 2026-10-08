# Census Refusal Coverage Ponytail Review: Branch Characterization

Date: 2026-10-07. Branch: `feat/census-refusal-coverage`, base `main` at `035d5b2`, review point `1dcc9b8`.

Labels: `[SOURCE]` is a fact read in this repository. `[INFERENCE]` is a deduction. `[RECOMMENDATION]` is a proposed next step.

## 1. Purpose

The repository owner requested a parallel ponytail review of the current branch. The review follows the ponytail-review rules. That method looks for over-engineering only. Correctness, security, and performance are outside the scope of this review. This record states what the parallel review found. It changes no production files.

## 2. How the review ran

`[SOURCE]` The branch diff against `main` at `035d5b2` touches 48 files. The review inspected the code changes across three parallel domains:
1. Host and animation evidence capture (`Editor/Host/` and `Tests/Editor/Host/`).
2. Material semantics and analysis (`Editor/Semantics/`, `Editor/Analysis/`, and related tests).
3. Build lifecycle and identity (`Editor/Build/` and related tests).

Three parallel reviewer tasks executed concurrently. Each reviewer inspected the `git diff main..HEAD` output for its target files. Markdown planning documents, investigations, and metadata files were outside review scope.

`[SOURCE]` The review point for every citation in this note is commit `1dcc9b8` on branch `feat/census-refusal-coverage`.

## 3. Findings

The review identified eleven complexity cuts across production code and test helpers. The eleven cuts together allow 91 fewer lines with zero behavior change.

### 3.1 Host: separate loops for batch capture outcomes

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs` lines 717 to 739 process closed batch capture results with two separate loops. The first loop writes unattested sentinel records for failed ordinals. The second loop writes surviving materials for remaining ordinals. Both loops write into the array `capturedByIndex` using indices from `attestedIndices`.

`[RECOMMENDATION]` Combine both passes into a single loop over the indices of `attestedIndices`. Check `failedOrdinals.Contains(index)`. If true, write the unattested sentinel material. If false, write the next surviving material from `batchOutcome.Captured` and increment the survivor counter. This change removes redundant array lookups and saves 6 lines.

### 3.2 Host test: list clone and mutation in test capturer

`[SOURCE]` `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs` lines 2149 to 2172 construct a list of survivors from `batch.Captured`. The test capturer clones the batch list. It removes items at failed ordinals. It then inserts unattested sentinel items at those same ordinals. Finally, it loops over the list and filters out any item whose index appears in `failedSet`. The inserted sentinels are always skipped.

`[RECOMMENDATION]` Delete the list clone, the reverse deletion loop, and the insertion loop. Filter the survivors directly from `batch.Captured` with `Where((_, index) => !failedSet.Contains(index)).ToList()`. This change removes dead computation and saves 20 lines.

### 3.3 Semantics: duplicate capture loop in plain overload

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs` lines 304 to 343 implement `TryCaptureClosedAlphaMaterials`. Lines 357 to 409 implement `TryCaptureClosedAlphaMaterialsTransferred`. The plain overload duplicates parameter checks, request array allocation, batch capture execution, and outcome classification.

`[RECOMMENDATION]` Delegate `TryCaptureClosedAlphaMaterials` directly to `TryCaptureClosedAlphaMaterialsTransferred`. Pass `System.Array.Empty<string>()` as the granted shader names parameter. The transferred method checks if granted names contain the shader name. An empty collection never contains the shader name. The execution path is identical. This delegation removes duplicate loop logic and saves 25 lines.

### 3.4 Semantics: duplicate branches setting constant one outline factor

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` lines 1504 to 1512 contain two adjacent branches:
```csharp
if (!outlineTexture.IsAssigned)
{
    outlineFactor = ScalarSemanticValue.Constant(1f);
}
else if (outlineTexture.Texture != null &&
         outlineTexture.Texture.SampledAlphaIsProvenOne)
{
    outlineFactor = ScalarSemanticValue.Constant(1f);
}
```
Both branches assign the exact same constant value to `outlineFactor`.

`[RECOMMENDATION]` Merge the two conditions into one `if` statement using the logical OR operator. This change saves 5 lines.

### 3.5 Semantics: duplicate MappedTexture construction in mask branch

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` lines 2098 to 2107 construct `ScalarSemanticValue.MappedTexture(maskSample, TextureChannel.Red, mapped)` identically in both the `replace` branch and the `else` branch.

`[RECOMMENDATION]` Evaluate the mapped texture once into a local variable before the `if` statement. Assign the local variable to either `replacement` or `multiplier`. This change removes duplicate construction arguments and saves 4 lines.

### 3.6 Semantics test: single-caller assertion helper

`[SOURCE]` `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOutlineAlphaSemanticsTests.cs` lines 21 to 27 declare a private helper method:
```csharp
private static void AssertUnsupportedOutputIsAbsent(PoiyomiSemanticResult result)
{
    Assert.That(
        result.Diagnostics.Any(d => d.Output == PoiyomiSemanticOutput.Alpha),
        Is.False,
        "Alpha output must not emit a diagnostic.");
}
```
Line 39 contains the only call to this helper in the entire repository.

`[RECOMMENDATION]` Inline the single assertion directly into line 39. Delete the private helper method. This change removes unnecessary indirection and saves 6 lines.

### 3.7 Build: single-caller helper RecordedListedOriginalName

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/LockedMaterialIdentity.cs` lines 246 to 253 declare a private helper method:
```csharp
private static bool RecordedListedOriginalName(
    Material material,
    IReadOnlyCollection<string> grantedShaderNames)
{
    var original = RecordedOriginalShaderName(material);
    return !string.IsNullOrEmpty(original) &&
           grantedShaderNames.Contains(original);
}
```
Line 242 in `GrantedAwareOriginalAttestation` is the only caller.

`[RECOMMENDATION]` Inline the two-step evaluation into the lambda expression in `GrantedAwareOriginalAttestation`. Delete the private helper method. This change saves 7 lines.

### 3.8 Build: redundant ternary check in plugin finish pass

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` lines 819 to 821 pass granted names to `RetainPreparedSeparation`:
```csharp
allowDepthTestChange,
transferShaders
    ? shaderTransfer.GrantedShaderNames
    : null);
```
Line 492 already defines `grantedShaderNames` as `shaderTransfer.GrantedShaderNames.Count > 0 ? shaderTransfer.GrantedShaderNames : null`. Line 528 sets `transferShaders = grantedShaderNames != null`. Therefore, `transferShaders ? shaderTransfer.GrantedShaderNames : null` evaluates to `grantedShaderNames`.

`[RECOMMENDATION]` Pass `grantedShaderNames` directly as the argument. This change saves 2 lines.

### 3.9 Build: single-caller helper ClosureFailureWayFor

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` lines 1398 to 1407 declare a private helper method:
```csharp
private static MaterialDependencyClosureFailure ClosureFailureWayFor(
    CapturedAnimationEvidence evidence)
{
    foreach (var record in evidence.SlotClosureFailures)
    {
        return record.Failure;
    }

    return MaterialDependencyClosureFailure.None;
}
```
Line 842 contains the only caller.

`[RECOMMENDATION]` Inline the check into line 841. If `evidence.SlotClosureFailures.Count > 0`, pass `evidence.SlotClosureFailures[0].Failure`. Otherwise pass `null` or `MaterialDependencyClosureFailure.None`. Delete the helper method. This change saves 8 lines.

### 3.10 Build test: manual loop zeroing new Color32 array

`[SOURCE]` `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationSplitTests.cs` lines 723 to 726 contain:
```csharp
var pixels = new Color32[16];
for (var i = 0; i < pixels.Length; i++)
{
    pixels[i] = new Color32(0, 0, 0, 0);
}
```
In C#, elements of a newly allocated struct array default to `default(Color32)`. `default(Color32)` has all fields set to zero.

`[RECOMMENDATION]` Delete the `for` loop. Keep only `var pixels = new Color32[16]`. This change saves 4 lines.

### 3.11 Build test: manual loop filling sequential integers

`[SOURCE]` `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` lines 5154 to 5158 populate an array of sequential integers:
```csharp
var ordinals = new int[materials.Count];
for (var index = 0; index < ordinals.Length; index++)
{
    ordinals[index] = index;
}
```

`[RECOMMENDATION]` Replace the allocation and manual loop with `System.Linq.Enumerable.Range(0, materials.Count).ToArray()`. This change saves 4 lines.

## 4. Savings Summary

| Finding | Target File | Tag | Line Reduction |
|---|---|---|---|
| 3.1 | `Editor/Host/UnityAnimationEvidenceCapture.cs` | `shrink` | -6 |
| 3.2 | `Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs` | `shrink` | -20 |
| 3.3 | `Editor/Semantics/UnityMaterialSemantics.cs` | `shrink` | -25 |
| 3.4 | `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` | `shrink` | -5 |
| 3.5 | `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` | `shrink` | -4 |
| 3.6 | `Tests/Editor/Semantics/Poiyomi/PoiyomiOutlineAlphaSemanticsTests.cs` | `yagni` | -6 |
| 3.7 | `Editor/Build/LockedMaterialIdentity.cs` | `yagni` | -7 |
| 3.8 | `Editor/Build/AmusePlatformFinishPlugin.cs` | `shrink` | -2 |
| 3.9 | `Editor/Build/AmusePlatformFinishPlugin.cs` | `yagni` | -8 |
| 3.10 | `Tests/Editor/Build/AlphaSeparationSplitTests.cs` | `shrink` | -4 |
| 3.11 | `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` | `stdlib` | -4 |
| **Total** | | | **-91** |

Every proposed cut preserves existing behavior. No public contract changes. No shader semantics changes. No build refusal changes.
