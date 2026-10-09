# Analysis Secondary Channel Safety Design Specification

Date: 2026-10-08. Branch: `fix/latent-bugs-and-impurities`. Base: `main` at `9eed463`.

Privacy note: this record is sanitized. It names no private avatar, renderer, material, texture, scene, asset folder, instance name, or test-job identifier. Aggregate counts appear as ranges. Machine references name roles only, for example the dev editor instance.

---

## 1. Overview

This design resolves the unhandled exception on non-finite secondary UV coordinates.
The primary UV channel validates finiteness before building triangle inputs.
Secondary UV channels UV1 through UV3 bypass finiteness validation.
When a shader layer references these channels, unvalidated coordinates enter classification.
The classifier then throws an uncaught `ArgumentException`.
This design makes sure that non-finite secondary UV coordinates fail closed to `Unknown`.

---

## 2. Requirements and Invariants

1. `TriangleAlphaInput.TryGetUvSet` must check coordinate finiteness.
2. Channel zero must continue to return the primary `Uv0` triplet.
3. When any coordinate contains `float.NaN` or infinity, `TryGetUvSet` returns false.
4. Callers reading through `TryGetUvSet` must receive `TriangleAlphaOutcome.Unknown`.
5. Valid finite secondary UV sets must continue to return true.
6. Secondary UV channel validation must never throw an `ArgumentException`.

---

## 3. Detailed Architecture and Implementation

### 3.1 UV Coordinate Finiteness Guard in TryGetUvSet

In `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`:
In `TriangleAlphaInput.TryGetUvSet`:

```csharp
internal bool TryGetUvSet(
    int channel,
    out Vector2 a,
    out Vector2 b,
    out Vector2 c)
{
    if (channel == 0)
    {
        if (!HasUv0)
        {
            a = default;
            b = default;
            c = default;
            return false;
        }

        a = Uv0;
        b = Uv1;
        c = Uv2;
        if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c))
        {
            a = default;
            b = default;
            c = default;
            return false;
        }

        return true;
    }

    var index = channel - 1;
    if (_extraUvSets == null ||
        index < 0 ||
        index >= _extraUvSets.Count ||
        _extraUvSets[index] == null)
    {
        a = default;
        b = default;
        c = default;
        return false;
    }

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

    a = uvList[_indexA];
    b = uvList[_indexB];
    c = uvList[_indexC];

    if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c))
    {
        a = default;
        b = default;
        c = default;
        return false;
    }

    return true;
}

private static bool IsFinite(Vector2 value)
{
    return !float.IsNaN(value.x) &&
           !float.IsInfinity(value.x) &&
           !float.IsNaN(value.y) &&
           !float.IsInfinity(value.y);
}
```

### 3.2 Host Channel Pass-Through Architecture

In `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`:
In `UnityRendererAlphaAnalysis.Classify`:
The host continues to pass secondary UV channels directly into `TriangleAlphaInput`.
`TriangleAlphaInput.TryGetUvSet` performs all coordinate finiteness checks.
This keeps mathematical proof validation localized within the analysis core.

---

## 4. Verification Plan

1. In `TriangleAlphaClassifierTests.cs`:
   - `TryGetUvSet_ReturnsTrue_ForValidPrimaryChannelZero`
   - `TryGetUvSet_ReturnsFalse_WhenPrimaryChannelContainsNaN`
   - `TryGetUvSet_ReturnsFalse_WhenPrimaryChannelContainsInfinity`
   - `TryGetUvSet_ReturnsFalse_WhenChannelIndexIsNegative`
   - `TryGetUvSet_ReturnsFalse_WhenChannelIndexExceedsAvailableSets`
   - `TryGetUvSet_ReturnsFalse_WhenSecondaryChannelContainsNaN`
   - `TryGetUvSet_ReturnsFalse_WhenSecondaryChannelContainsPositiveInfinity`
   - `TryGetUvSet_ReturnsFalse_WhenSecondaryChannelContainsNegativeInfinity`
   - `TryGetUvSet_ReturnsTrue_WhenSecondaryChannelContainsValidCoordinates`
2. In `AlphaSemanticsResolverTests.cs`:
   - `LayerSamplingSecondaryChannelWithNaN_YieldsUnknownOutcomeWithoutThrowing`
