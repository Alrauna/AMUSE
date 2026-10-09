# Analysis Mathematical Safety: Design and Specification

Date: 2026-10-08.
Branch plan: fix/latent-bugs-and-impurities. Base: main at c375101.
No source assets are modified.
All mutations remain inside the NDMF build copy.

Privacy note: this document contains no private avatar, renderer, material, scene, or machine identity. Public vendor shader identifiers stay exact.

Labels: `[SOURCE]` is a fact read from cited code lines in this repository.
`[INFERENCE]` is a conclusion from evidence.

---

## 1. Summary

This design fixes five mathematical safety issues and performance impurities in the Analysis module.
The changes harden coordinate arithmetic against 32-bit integer overflow.
The changes eliminate loop rollover on boundary texels in point and bilinear repeat sampling.
The changes prevent unhandled index exceptions on truncated extra UV sets.
The changes remove duplicate geometry methods and eliminate unnecessary BigInteger CPU overhead in hot classification loops.

All changes are headless and pure.
They require no Unity Editor dependencies.
They preserve exact fail-closed classification.

---

## 2. Background and Motivation

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:728-735, 833-840, 1347-1352, 1424-1430`.
Candidate region count calculations subtract and add 32-bit signed integers before casting to long.
When extreme repeat coordinates produce differences that exceed 32-bit integer limits, the arithmetic overflows to a negative integer.
The subsequent cast to long produces a negative count.
The complexity guard `candidateCount > MaxSupportRegions` evaluates to false.
The classifier then bypasses the complexity limit.
This defect affects both classification methods and witness search methods.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:722-731, 827-835, 1330-1368, 1424-1445`.
`ClassifyBilinearRepeat` and `HasMappedWitnessBilinearRepeat` check `maxCellX >= int.MaxValue`.
They then add one to compute `maximumX`.
When `maxCellX` equals `int.MaxValue - 1`, `maximumX` equals `int.MaxValue`.
The loop increment `unwrappedX++` then rolls over from `int.MaxValue` to `int.MinValue`.
The loop condition `unwrappedX <= maximumX` remains true forever.
In addition, `ClassifyPointRepeat` and `HasMappedWitnessPointRepeat` do not check cell bounds at all.
Extreme coordinates cause infinite loops in both bilinear and point repeat sampling.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:88-103`.
`TriangleAlphaInput.TryGetUvSet` validates channel indices and checks for null arrays.
It does not verify vertex indices `_indexA`, `_indexB`, and `_indexC` against array bounds.
Meshes with truncated extra UV buffers cause `ArgumentOutOfRangeException`.
The AMUSE specification requires invalid or missing UV sets to return false and yield an Unknown outcome.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs:856-868`.
`FloorMod` accepts a `BigInteger` value.
In candidate iteration loops, 32-bit integer variables convert to `BigInteger` structs on each iteration.
Each triangle evaluation executes up to 65536 iterations per mip level.
This creates avoidable CPU branching and conversion overhead.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs:684-695, 718-725`.
`ExactUvGeometry.Cross` subtracts coordinate numerators directly without scaling by denominators.
It is called only from lines 685 and 691 in `CreateHull` to test collinearity and winding order.
`ExactUvGeometry.Orientation` already implements exact rational cross products with common denominator scaling.

---

## 3. Detailed Design

### 3.1 Repeat Mode Candidate Region Overflow Protection
In `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`:
Update `ClassifyBilinearRepeat`, `HasMappedWitnessBilinearRepeat`, `ClassifyPointRepeat`, and `HasMappedWitnessPointRepeat`.
Cast coordinate bounds to long before arithmetic.

For classification methods that return `TriangleAlphaOutcome`:
```csharp
var spanX = (long)maximumX - minimumX + 1L;
var spanY = (long)maximumY - minimumY + 1L;
if (spanX <= 0L || spanY <= 0L)
{
    return TriangleAlphaOutcome.Unknown;
}
if (spanX > MaxSupportRegions || spanY > MaxSupportRegions)
{
    return TriangleAlphaOutcome.Unknown;
}
var candidateCount = spanX * spanY;
if (candidateCount > MaxSupportRegions)
{
    return TriangleAlphaOutcome.Unknown;
}
```

For witness search methods that return `bool`:
```csharp
var spanX = (long)maximumX - minimumX + 1L;
var spanY = (long)maximumY - minimumY + 1L;
if (spanX <= 0L || spanY <= 0L ||
    spanX > MaxSupportRegions || spanY > MaxSupportRegions ||
    spanX * spanY > MaxSupportRegions)
{
    return true;
}
```
Returning true on complexity abort preserves fail-closed safety by assuming a witness exists.

### 3.2 Point and Bilinear Repeat Boundary Cell Guards
In `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`:
Check derived bounds `minimumX`, `maximumX`, `minimumY`, and `maximumY` before loop execution in all four repeat methods.
Check that no boundary coordinate equals or exceeds integer limits:
```csharp
if (minimumX <= int.MinValue || maximumX >= int.MaxValue ||
    minimumY <= int.MinValue || maximumY >= int.MaxValue)
{
    return TriangleAlphaOutcome.Unknown;
}
```
For witness methods, return true when coordinates reach integer limits.
This prevents loop counter rollover and eliminates infinite loops in both point and bilinear repeat.

### 3.3 Safe Extra UV Set Index Bounds Validation
In `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`:
Update `TryGetUvSet` to validate vertex indices against channel array bounds:
```csharp
var uvList = _extraUvSets[index];
if ((uint)_indexA >= (uint)uvList.Count ||
    (uint)_indexB >= (uint)uvList.Count ||
    (uint)_indexC >= (uint)uvList.Count)
{
    a = default;
    b = default;
    c = default;
    return false;
}
```
Unsigned comparison handles negative indices and out of range indices in one test.
When an index is invalid, the method returns false without throwing an exception.

### 3.4 Native 32-Bit Integer FloorMod Overload
In `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs`:
Add a 32-bit integer overload of `FloorMod`:
```csharp
internal static int FloorMod(int value, int modulus)
{
    if (modulus <= 0)
    {
        throw new ArgumentOutOfRangeException(nameof(modulus));
    }
    var remainder = value % modulus;
    if (remainder < 0)
    {
        remainder += modulus;
    }
    return remainder;
}
```
Retain the existing `BigInteger` overload for callers that use arbitrary precision integers.
Hot integer iteration loops in `TriangleAlphaClassifier` automatically bind to the native integer overload.

### 3.5 Replacement of Fragile Cross Method
In `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs`:
Update `CreateHull` lines 684 to 695:
```csharp
var orientation = Orientation(unique[0], unique[1], unique[2]);
if (orientation == 0)
{
    unique.Sort(ComparePoints);
    return new[] { unique[0], unique[unique.Count - 1] };
}

if (orientation < 0)
{
    var swap = unique[1];
    unique[1] = unique[2];
    unique[2] = swap;
}

return unique;
```
Delete the private method `Cross` on lines 718 to 725.
This preserves counter-clockwise winding order and uses exact rational orientation for all points.

---

## 4. Verification and Test Plan

1. Unit test `RepeatCandidateRegionOverflowReturnsUnknown`:
   Construct repeat domains with `minCellX = -1500000000` and `maxCellX = 1500000000`.
   Verify that `ClassifyPointRepeat` and `ClassifyBilinearRepeat` return `Unknown` without throwing or hanging.
2. Unit test `WitnessRepeatOverflowReturnsTrue`:
   Pass overflow bounds to `HasMappedWitnessPointRepeat` and `HasMappedWitnessBilinearRepeat`.
   Verify that both methods return true to preserve fail-closed witness detection.
3. Unit test `RepeatExtremeBoundaryCoordinatesPreventLoopRollover`:
   Pass `maximumX = int.MaxValue - 1` and `maximumX = int.MaxValue` to bilinear and point repeat methods.
   Verify immediate return without hanging or infinite loop iterations.
4. Unit test `TruncatedAndNegativeExtraUvIndicesReturnFalseSafely`:
   Pass triangles with vertex index -1 and index 10 when the extra UV channel list contains only 3 elements.
   Verify that `TryGetUvSet` returns false and sets default vectors.
5. Unit test `FloorModIntegerOverloadMatchesBigIntegerOverload`:
   Evaluate `FloorMod` across negative and positive integer ranges.
   Assert exact value identity between the integer overload and the `BigInteger` overload.
6. Unit test `CollinearAndWindingPointsInCreateHullUseOrientation`:
   Evaluate `CreateHull` on collinear rational points and clockwise rational points with different denominators.
   Verify correct collinear reduction and counter-clockwise vertex order.
