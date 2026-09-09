# Classifier Performance Optimization and Out-of-Range Slot Tolerance Design

Date: 2026-09-09.

## 1. Overview

This design addresses two requirements in the AMUSE build pipeline:
1. **Performance**: Acceleration of `TriangleAlphaClassifier` for complex meshes with cutout and transparent textures. We replace `BigInteger` loop iteration with primitive integer math. We add a conservative 2D Separating Axis Test (SAT) pre-filter to reject non-overlapping texels without heap allocation.
2. **Tolerance Policy**: A user-configurable toggle in `AmuseAvatarOptimizer` under Advanced Settings. When enabled, this toggle allows AMUSE to ignore animation bindings that target material slots outside the mesh material count. This permits optimization of valid mesh slots on avatars that use shared multi-avatar animation clips.

## 2. Classifier Performance Optimization (Approach 1)

### 2.1 Problem Analysis
In `TriangleAlphaClassifier.ClassifyBilinearRepeat` and `ClassifyBilinearClamp`, each triangle classification iterates over a bounding box of candidate texels:
- On cutout textures, the average bounding box contains 650 to 5,450 texels.
- Loop variables (`minimumX`, `maximumX`, `minimumY`, `maximumY`, `unwrappedX`, `unwrappedY`) use `BigInteger`. Every loop iteration executes `BigInteger` comparisons, additions, and modulo operations.
- For each non-opaque texel, the classifier calls `ExactUvGeometry.Intersects`. This executes four Sutherland-Hodgman polygon clipping passes using `BigInteger` rational arithmetic. It also allocates `List<ExactUvPoint>` instances on the managed heap.
- Because a triangle covers only a small part of its rectangular bounding box, thousands of clipping passes execute for texels that never touch the triangle.

### 2.2 Primitive Integer Loop Bounds
Texture dimensions in Unity do not exceed 16,384. Mip level coordinates fit safely within 32-bit signed integers.
1. `CellIndex` computes the cell index as `int`:
   `int CellIndex(ExactRational coordinate, BigInteger texelScale)`
2. The loop bounds `minimumX`, `maximumX`, `minimumY`, and `maximumY` become `int`.
3. Loop variables `unwrappedX` and `unwrappedY` become `int`.
4. Texture lookup uses direct integer modulo:
   `int x = ExactUvGeometry.FloorMod(unwrappedX, texture.Width);`
   `int y = ExactUvGeometry.FloorMod(unwrappedY, texture.Height);`
This eliminates all `BigInteger` overhead during bounding box traversal.

### 2.3 Conservative Separating Axis Test (SAT) Pre-Filter
Before AMUSE calls `ExactUvGeometry.Intersects`, it evaluates a fast 2D Separating Axis Test in floating-point coordinates.

#### Texel Support Rectangle
For bilinear sampling, texel `(x, y)` has a reconstruction kernel support of two texels along each axis:
$$[x - 0.5, x + 1.5] \times [y - 0.5, y + 1.5]$$

#### Pre-Filter Algorithm
1. The filter tests the triangle bounding box against the texel support box. If they do not overlap, the texel is rejected immediately.
2. The filter tests the three triangle edge normals against the texel support box. For each edge, if all four corners of the box lie entirely outside the half-plane of the edge with a conservative safety epsilon, the texel cannot intersect the triangle.
3. The filter rejects non-overlapping texels in nanoseconds with zero heap allocations.
4. If the conservative test does not separate the shapes, AMUSE executes the existing exact rational `ExactUvGeometry.Intersects` method.

#### Soundness Guarantee
The floating-point pre-filter only proves **disjointness** (separation). It never proves intersection. A false negative is impossible because the bounding interval includes conservative floating-point rounding margins. When the pre-filter indicates potential overlap, the exact rational solver makes the final proof decision.

## 3. Out-of-Range Slot Tolerance Policy

### 3.1 Problem Analysis
Avatar creators frequently use shared animation controllers across multiple outfits and avatars. An animation clip may animate `m_Materials.Array.data[1]` and `m_Materials.Array.data[2]` for an outfit variant. However, a specific accessory mesh may have only one material slot (`m_Materials.Array.data[0]`).
Currently, `UnityAnimationEvidenceCapture.Capture` fails the entire renderer with `MaterialDependencyClosureFailure.SlotOutOfRange`. This leaves the mesh unoptimized.

### 3.2 Component and Inspector Additions

#### Runtime Component (`AmuseAvatarOptimizer.cs`)
- Add field:
  `[SerializeField] private bool _ignoreOutOfRangeMaterialSlots = false;`
- Add public property:
  `public bool IgnoreOutOfRangeMaterialSlots => _ignoreOutOfRangeMaterialSlots;`
- Default value is `false` to maintain fail-closed safety.

#### Editor Inspector (`AmuseAvatarOptimizerEditor.cs`)
- Add a toggle in `DrawAdvancedSettings()`:
  Control name: `Ignore Out-of-Range Material Slots`
- Tooltip in ASD-STE100 Simplified Technical English:
  `Some animation files animate material slots that do not exist on this mesh.`
  `By default, AMUSE refuses to optimize this mesh to prevent visual errors.`
  `Turn this setting on to ignore these extra slot animations and optimize the valid slots.`
  `Risk: If an animation later changes this mesh to have more material slots, or if another tool relies on the untouched slot count, visual errors can occur.`

### 3.3 Evidence Capture Behavior
In `UnityAnimationEvidenceCapture.cs`:
1. The capture method receives the policy flag `ignoreOutOfRangeSlots`.
2. When parsing material slot bindings:
   ```csharp
   if (slot >= currentSlots.Count)
   {
       if (ignoreOutOfRangeSlots)
       {
           continue;
       }
       return Failed(MaterialDependencyClosureFailure.SlotOutOfRange);
   }
   ```
3. When the toggle is active, AMUSE skips out-of-range slot bindings. Valid slots on the renderer continue through normal classification and separation.
4. When the toggle is off, AMUSE retains the existing fail-closed behavior.

## 4. Test Strategy

### 4.1 Unit Tests (Classifier Performance and Soundness)
- Test equivalence: verify that the optimized classifier produces identical results to the baseline implementation across all test fixtures.
- Test conservative rejection: verify that the SAT pre-filter never rejects an intersecting texel.
- Benchmark: measure triangle classification speedup on complex synthetic meshes.

### 4.2 Unit Tests (Slot Tolerance)
- Test default fail-closed behavior: an out-of-range slot animation refuses the renderer with `SlotOutOfRange`.
- Test toggle enabled: an out-of-range slot animation is ignored. Valid slots on the renderer successfully separate.
- Test inspector persistence: verify serialization and round-trip of the new property on `AmuseAvatarOptimizer`.
