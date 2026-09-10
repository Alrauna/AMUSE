# Classifier Performance Optimization and Slot Tolerance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Accelerate AMUSE's triangle alpha classification by replacing `BigInteger` loop iteration with primitive integer math and a 2D separating-axis pre-filter, and add a user-configurable toggle to ignore out-of-range material slot animation bindings.

**Architecture:** 
1. `AmuseAvatarOptimizer` exposes `_ignoreOutOfRangeMaterialSlots` with an Advanced Settings toggle in `AmuseAvatarOptimizerEditor` using an ASD-STE100 tooltip.
2. `UnityAnimationEvidenceCapture` respects `ignoreOutOfRangeSlots` to skip out-of-range slot bindings instead of refusing the entire renderer with `SlotOutOfRange`.
3. `TriangleAlphaClassifier` converts bounding-box iteration to 32-bit `int` and adds a conservative floating-point 2D Separating Axis Test (SAT) pre-filter before invoking exact rational `ExactUvGeometry.Intersects`.

**Tech Stack:** C# 9 / Unity 2022.3, NUnit (Unity Test Framework EditMode), NDMF API.

**Spec:** `docs/superpowers/specs/2026-09-09-classifier-performance-and-slot-tolerance-design.md`

## Global Constraints

- Every English text that a human reads uses ASD-STE100 Simplified Technical English: short active sentences, one idea per sentence, no semicolons, no contractions.
- Never record an absolute or machine-specific path in any document, comment, commit message, or test. Use `<repo-root>`-relative paths.
- Never expose private Census names, paths, GUIDs, per-avatar/per-renderer rows, or fingerprint-like identifiers.
- Never modify source meshes, materials, textures, animation assets, prefabs, or scenes.
- Preserve exact mathematical proofs: the SAT pre-filter only proves disjointness and never proves intersection; all potential overlaps execute `ExactUvGeometry.Intersects`.
- Production types remain internal to `Alrauna.Amuse.Editor` and `Alrauna.Amuse.Runtime`.

---

### Task 1: Out-of-Range Slot Tolerance Setting and Component Implementation

**Files:**
- Modify: `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseAvatarOptimizerTests.cs`

**Interfaces:**
- Produces: `AmuseAvatarOptimizer.IgnoreOutOfRangeMaterialSlots` (`bool`, default `false`).
- Produces: `_ignoreOutOfRangeMaterialSlots` serialized property and inspector toggle in `DrawAdvancedSettings()`.

- [ ] **Step 1: Write failing tests for IgnoreOutOfRangeMaterialSlots property and serialization**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseAvatarOptimizerTests.cs`:
Add tests:
- `DefaultIgnoreOutOfRangeMaterialSlotsIsFalse`: verifies property returns `false` on a fresh component.
- `IgnoreOutOfRangeMaterialSlotsSerializedPropertyCanBeToggled`: verifies the serialized property round-trips to `true`.

```csharp
[Test]
public void DefaultIgnoreOutOfRangeMaterialSlotsIsFalse()
{
    var go = new GameObject("Root");
    try
    {
        var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
        Assert.That(optimizer.IgnoreOutOfRangeMaterialSlots, Is.False);
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(go);
    }
}

[Test]
public void IgnoreOutOfRangeMaterialSlotsSerializedPropertyCanBeToggled()
{
    var go = new GameObject("Root");
    try
    {
        var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
        var serializedObject = new SerializedObject(optimizer);
        var property = serializedObject.FindProperty("_ignoreOutOfRangeMaterialSlots");
        Assert.That(property, Is.Not.Null);
        property.boolValue = true;
        serializedObject.ApplyModifiedProperties();

        Assert.That(optimizer.IgnoreOutOfRangeMaterialSlots, Is.True);
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(go);
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run via Unity MCP `run_tests` targeting `AmuseAvatarOptimizerTests`.
Expected: FAIL with compilation error (property and field do not exist yet).

- [ ] **Step 3: Implement field, property, and inspector toggle**

In `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`:
Add:
```csharp
[SerializeField]
private bool _ignoreOutOfRangeMaterialSlots;

/// <summary>
/// When true, AMUSE ignores animation bindings that target material
/// slot indices outside the mesh material count. When false, an
/// out-of-range slot animation causes the renderer to refuse
/// optimization.
/// </summary>
public bool IgnoreOutOfRangeMaterialSlots =>
    _ignoreOutOfRangeMaterialSlots;
```

In `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`:
In `DrawAdvancedSettings()`:
```csharp
EditorGUILayout.PropertyField(
    serializedObject.FindProperty("_ignoreOutOfRangeMaterialSlots"),
    new GUIContent(
        "Ignore Out-of-Range Material Slots",
        "Some animation files animate material slots that do not exist on this mesh. " +
        "By default, AMUSE refuses to optimize this mesh to prevent visual errors. " +
        "Turn this setting on to ignore these extra slot animations and optimize the valid slots.\n\n" +
        "Risk: If an animation later changes this mesh to have more material slots, " +
        "or if another tool relies on the untouched slot count, visual errors can occur."));
```

- [ ] **Step 4: Run tests to verify they pass**

Run via Unity MCP `run_tests` targeting `AmuseAvatarOptimizerTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseAvatarOptimizerTests.cs
git commit -m "feat: add IgnoreOutOfRangeMaterialSlots setting to optimizer component"
```

---

### Task 2: Out-of-Range Slot Tolerance Pipeline Wiring

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`

**Interfaces:**
- Consumes: `AmuseAvatarOptimizer.IgnoreOutOfRangeMaterialSlots`.
- Modifies: `UnityAnimationEvidenceCapture.Capture` accepts `bool ignoreOutOfRangeSlots = false`.
- Behavior: when `ignoreOutOfRangeSlots` is `true`, slot bindings with `slot >= currentSlots.Count` are skipped (`continue;`) instead of returning `Failed(SlotOutOfRange)`.

- [ ] **Step 1: Write failing tests for out-of-range slot binding tolerance**

In `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`:
Add tests:
- `OutOfRangeSlotBindingFailsClosedByDefault`: verifies that an animation targeting slot 1 on a 1-slot renderer fails with `SlotOutOfRange` when `ignoreOutOfRangeSlots` is `false`.
- `OutOfRangeSlotBindingIsIgnoredWhenToleranceEnabled`: verifies that the same binding is skipped and the renderer closes successfully when `ignoreOutOfRangeSlots` is `true`.

```csharp
[Test]
public void OutOfRangeSlotBindingFailsClosedByDefault()
{
    var clip = new LiveClipObservation("Test", false, Array.Empty<LiveFloatObservation>(), new[]
    {
        new LiveObjectObservation("Renderer", typeof(MeshRenderer).FullName, "m_Materials.Array.data[1]", new Object[] { new Material(Shader.Find("Hidden/Alrauna/AmuseTests/Opaque")) })
    });
    var slotMat = new Material(Shader.Find("Hidden/Alrauna/AmuseTests/Opaque"));
    var evidence = UnityAnimationEvidenceCapture.CaptureObservedForTests(
        "Renderer",
        new[] { clip },
        new[] { slotMat },
        ValidGraph(),
        ignoreOutOfRangeSlots: false);

    Assert.That(evidence.IsClosed, Is.False);
    Assert.That(evidence.ClosureFailure, Is.EqualTo(MaterialDependencyClosureFailure.SlotOutOfRange));
}

[Test]
public void OutOfRangeSlotBindingIsIgnoredWhenToleranceEnabled()
{
    var clip = new LiveClipObservation("Test", false, Array.Empty<LiveFloatObservation>(), new[]
    {
        new LiveObjectObservation("Renderer", typeof(MeshRenderer).FullName, "m_Materials.Array.data[1]", new Object[] { new Material(Shader.Find("Hidden/Alrauna/AmuseTests/Opaque")) })
    });
    var slotMat = new Material(Shader.Find("Hidden/Alrauna/AmuseTests/Opaque"));
    var evidence = UnityAnimationEvidenceCapture.CaptureObservedForTests(
        "Renderer",
        new[] { clip },
        new[] { slotMat },
        ValidGraph(),
        ignoreOutOfRangeSlots: true);

    Assert.That(evidence.IsClosed, Is.True);
    Assert.That(evidence.ClosureFailure, Is.EqualTo(MaterialDependencyClosureFailure.None));
}
```

- [ ] **Step 2: Run tests to verify failure**

Run via Unity MCP `run_tests` targeting `UnityAnimationEvidenceCaptureTests`.
Expected: FAIL (parameter `ignoreOutOfRangeSlots` not defined).

- [ ] **Step 3: Implement tolerance parameter in evidence capture and plugin**

In `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`:
Add `bool ignoreOutOfRangeSlots = false` parameter to:
- `Capture`
- `CaptureGraph`
- `CaptureObserved`
- `CaptureObservedForTests`
- `CaptureGraphForTests`

In the `observation.Objects` loop:
```csharp
if (slot >= currentSlots.Count)
{
    if (ignoreOutOfRangeSlots)
    {
        continue;
    }

    return Failed(
        MaterialDependencyClosureFailure.SlotOutOfRange);
}
```

In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`:
Around line 428, read:
```csharp
var ignoreOutOfRangeSlots = optimizer != null && optimizer.IgnoreOutOfRangeMaterialSlots;
```
Pass `ignoreOutOfRangeSlots` into `UnityAnimationEvidenceCapture.Capture(...)`.

- [ ] **Step 4: Run tests to verify they pass**

Run via Unity MCP `run_tests` targeting `UnityAnimationEvidenceCaptureTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs
git commit -m "feat: support ignoring out-of-range slot bindings when tolerance enabled"
```

---

### Task 3: Integer Loop Bounds in TriangleAlphaClassifier

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`

**Interfaces:**
- Modifies: `TriangleAlphaClassifier.ClassifyBilinearRepeat` and `ClassifyBilinearClamp` bounding box traversal to use 32-bit `int` for loop bounds and variables.
- Modifies: `CellIndex` returns `int`.

- [ ] **Step 1: Inspect existing tests**

Run existing `TriangleAlphaClassifierTests` via Unity MCP `run_tests`.
Confirm all existing tests currently pass (baseline).

- [ ] **Step 2: Convert CellIndex and bounding box bounds to int**

In `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`:
Update `CellIndex`:
```csharp
private static int CellIndex(
    ExactRational coordinate,
    BigInteger texelScale)
{
    var div = ExactUvGeometry.FloorDiv(
        coordinate.Numerator,
        coordinate.Denominator * texelScale);
    return (int)div;
}
```
Update `ClassifyBilinearRepeat`:
```csharp
var minimumX = CellIndex(
    ExactUvGeometry.Minimum(domain, true),
    domain.TexelScale) - 1;
var maximumX = CellIndex(
    ExactUvGeometry.Maximum(domain, true),
    domain.TexelScale) + 1;
var minimumY = CellIndex(
    ExactUvGeometry.Minimum(domain, false),
    domain.TexelScale) - 1;
var maximumY = CellIndex(
    ExactUvGeometry.Maximum(domain, false),
    domain.TexelScale) + 1;
var candidateCount = (long)(maximumX - minimumX + 1) *
                     (maximumY - minimumY + 1);
if (candidateCount > MaxSupportRegions)
{
    return TriangleAlphaOutcome.Unknown;
}

for (var unwrappedY = minimumY; unwrappedY <= maximumY; unwrappedY++)
{
    var y = ExactUvGeometry.FloorMod(unwrappedY, texture.Height);
    for (var unwrappedX = minimumX; unwrappedX <= maximumX; unwrappedX++)
    {
        var x = ExactUvGeometry.FloorMod(unwrappedX, texture.Width);
        if (texture.GetAlpha(x, y) == byte.MaxValue)
        {
            continue;
        }
        if (ExactUvGeometry.Intersects(
            domain,
            BilinearRepeatInterval(unwrappedX, domain.TexelScale),
            BilinearRepeatInterval(unwrappedY, domain.TexelScale)))
        {
            return TriangleAlphaOutcome.MustRemainTransparent;
        }
    }
}
return TriangleAlphaOutcome.ProvenOpaque;
```
Update `BilinearRepeatInterval` to accept `int index` instead of `BigInteger index`:
```csharp
private static ExactInterval BilinearRepeatInterval(
    int index,
    BigInteger texelScale)
{
    var halfTexel = texelScale / 2;
    var center = new BigInteger(index) * texelScale;
    return new ExactInterval(
        true,
        new ExactRational(center - halfTexel),
        false,
        true,
        new ExactRational(center + 3 * halfTexel),
        false);
}
```
Apply the same `int` index updates to `ClassifyBilinearClamp` and `BilinearClampInterval`.

- [ ] **Step 3: Run existing tests to verify zero regressions**

Run via Unity MCP `run_tests` targeting `TriangleAlphaClassifierTests`.
Expected: PASS with identical verdicts.

- [ ] **Step 4: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs
git commit -m "perf: convert bounding box traversal to integer coordinates in classifier"
```

---

### Task 4: Conservative 2D SAT Triangle-Box Pre-Filter

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`

**Interfaces:**
- Produces: `ConservativeBilinearSupportOverlapsTriangle` static method in `TriangleAlphaClassifier`.
- Inputs: Triangle vertices in texel space (`double`), texel support box $[X - 0.5, X + 1.5] \times [Y - 0.5, Y + 1.5]$.
- Output: `false` if definitely separated (disjoint), `true` if potentially overlapping.

- [ ] **Step 1: Write tests for ConservativeBilinearSupportOverlapsTriangle**

In `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`:
Add tests:
- `PreFilterRejectsSeparatedTexelSupportBox`: verifies disjoint texel box returns `false`.
- `PreFilterRetainsOverlappingTexelSupportBox`: verifies overlapping texel box returns `true`.
- `PreFilterNeverRejectsBoundaryTouchingTexel`: verifies near-edge texel within epsilon returns `true` (conservative guarantee).

- [ ] **Step 2: Run tests to verify failure**

Run via Unity MCP `run_tests` targeting `TriangleAlphaClassifierTests`.
Expected: FAIL (method does not exist yet).

- [ ] **Step 3: Implement 2D SAT pre-filter**

In `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`:
Add:
```csharp
/// <summary>
/// Conservatively tests whether the bilinear reconstruction support box
/// of an unwrapped texel [unwrappedX - 0.5, unwrappedX + 1.5] x [unwrappedY - 0.5, unwrappedY + 1.5]
/// can intersect the triangle. Returns false only when the shapes are
/// definitely separated. Returns true if they overlap or are within
/// numerical tolerance, requiring exact rational proof.
/// </summary>
private static bool ConservativeBilinearSupportOverlapsTriangle(
    double boxMinX, double boxMaxX,
    double boxMinY, double boxMaxY,
    double v0x, double v0y,
    double v1x, double v1y,
    double v2x, double v2y)
{
    // Axis 1 & 2: AABB check
    var triMinX = Math.Min(v0x, Math.Min(v1x, v2x));
    var triMaxX = Math.Max(v0x, Math.Max(v1x, v2x));
    if (boxMaxX < triMinX || boxMinX > triMaxX) return false;

    var triMinY = Math.Min(v0y, Math.Min(v1y, v2y));
    var triMaxY = Math.Max(v0y, Math.Max(v1y, v2y));
    if (boxMaxY < triMinY || boxMinY > triMaxY) return false;

    // Axis 3, 4, 5: Triangle edge normal half-planes
    // Edge normal N = (-(vB.y - vA.y), vB.x - vA.x)
    if (EdgeSeparates(v0x, v0y, v1x, v1y, v2x, v2y, boxMinX, boxMaxX, boxMinY, boxMaxY)) return false;
    if (EdgeSeparates(v1x, v1y, v2x, v2y, v0x, v0y, boxMinX, boxMaxX, boxMinY, boxMaxY)) return false;
    if (EdgeSeparates(v2x, v2y, v0x, v0y, v1x, v1y, boxMinX, boxMaxX, boxMinY, boxMaxY)) return false;

    return true;
}

private static bool EdgeSeparates(
    double aX, double aY,
    double bX, double bY,
    double cX, double cY,
    double boxMinX, double boxMaxX,
    double boxMinY, double boxMaxY)
{
    var nx = -(bY - aY);
    var ny = bX - aX;

    // Sign of third triangle vertex
    var cDot = nx * (cX - aX) + ny * (cY - aY);
    if (Math.Abs(cDot) < 1e-12) return false; // Degenerate edge

    // Find the box vertex that extends farthest in the direction of the triangle interior
    // If even that extreme box vertex is on the outside, the entire box is outside.
    double extremeX = (cDot > 0) ? ((nx >= 0) ? boxMaxX : boxMinX) : ((nx >= 0) ? boxMinX : boxMaxX);
    double extremeY = (cDot > 0) ? ((ny >= 0) ? boxMaxY : boxMinY) : ((ny >= 0) ? boxMinY : boxMaxY);

    var extremeDot = nx * (extremeX - aX) + ny * (extremeY - aY);
    if (cDot > 0)
    {
        return extremeDot < -1e-9;
    }
    else
    {
        return extremeDot > 1e-9;
    }
}
```

In `ClassifyBilinearRepeat` and `ClassifyBilinearClamp`:
Extract floating-point texel-space vertex coordinates before the loops.
Before calling `ExactUvGeometry.Intersects`:
```csharp
if (!ConservativeBilinearSupportOverlapsTriangle(
        unwrappedX - 0.5, unwrappedX + 1.5,
        unwrappedY - 0.5, unwrappedY + 1.5,
        v0x, v0y, v1x, v1y, v2x, v2y))
{
    continue;
}
```

- [ ] **Step 4: Run tests to verify correctness**

Run via Unity MCP `run_tests` targeting `TriangleAlphaClassifierTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs
git commit -m "perf: add conservative 2D SAT pre-filter to bilinear classification"
```

---

### Task 5: End-to-End Suite Validation and Performance Verification

**Files:**
- Test: Full EditMode test suite (`Alrauna.Amuse.Tests.Editor`)

- [ ] **Step 1: Run full EditMode test suite on Dev project**

Run via Unity MCP `run_tests` targeting `Alrauna.Amuse.Tests.Editor`.
Record total test count and duration.
Expected: All tests pass with 0 failures and 0 skips.

- [ ] **Step 2: Run benchmark probe in Census Lab**

Pin the Census Lab editor instance.
Re-run the 100-triangle benchmark on the recorded cutout submesh.
Observe the speedup (expected: reduction from ~4.4 ms per triangle to <0.05 ms per triangle).
Confirm that the recorded avatar optimizes when `IgnoreOutOfRangeMaterialSlots = true`.
Pin the active instance back to Dev.

- [ ] **Step 3: Check git diff hygiene**

Run:
```bash
git diff --check
git status
```
Ensure no unstaged churn, no package manifest churn.
