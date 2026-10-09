# Latent Bugs and Architectural Impurities Investigation

Date: 2026-10-08. Branch: fix/latent-bugs-and-impurities. Base: main at c375101.

Privacy note: this record is sanitized. It names no private avatar, renderer, material, texture, scene, asset folder, instance name, or test-job identifier. Machine references name roles only, for example the dev editor instance or the Census Lab editor instance. Public vendor shader identifiers stay exact.

## 1. Summary

A parallel scan and adversarial review on 2026-10-08 evaluated the complete AMUSE editor codebase. The review confirmed ten genuine findings across three subsystems. The findings include six latent bugs in core mathematical and lifecycle code. The findings also include four architectural or performance impurities. The adversarial review removed five invalid claims and false positives.

This document records the exact locations, root causes, risks, and proposed fixes.

## 2. Build and Host Findings

### Finding 1: Virtualized Controller Clips Omitted During Window Close
- Location: Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs lines 420 to 450.
- Category: Latent bug and dangling asset reference.
- Description: The CommittedClips method enumerates only innate controllers from the avatar root object. It omits components that implement IVirtualizeAnimatorController on child transforms. The swap-in pass rewrites virtual clips from all controllers on child transforms. Because CommittedClips omits those controllers, InvertCommittedCurves never inverts their committed clips. The subsequent cleanup step then destroys the unlocked clone material. The clips on virtualized child controllers retain dangling references to the destroyed material.
- Risk: Missing material references and console warnings in user avatar builds that use virtualized animator controllers.
- Proposed Fix: Update CommittedClips in TransientUnlockWindowClose.cs to search for IVirtualizeAnimatorController components on child transforms.

### Finding 2: Incomplete Curve Reference Verification During Cleanup
- Location: Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs lines 395 to 415.
- Category: Latent bug and redundant execution.
- Description: CurveInversionWasComplete checks only the avatar animation refusal code. It does not verify that zero keyframes reference the unlocked clone material. If InvertCommittedCurves skips a curve, the check still returns true. The caller then destroys the clone material immediately. In addition, the method enumerates the complete controller graph again for every open material pair.
- Risk: Broken curve bindings when curve inversion skips bindings. Redundant controller graph enumeration on multi-material avatars.
- Proposed Fix: Scan committed clips to confirm zero keyframes reference the unlocked clone material. Cache the enumerated controller graph across material pair checks.

### Finding 3: Asymmetric Type Filtering in Curve Inversion
- Location: Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs lines 304 to 360.
- Category: Latent bug.
- Description: InvertCommittedCurves filters committed bindings with type matching helpers. The helper returns false when a binding type is SkinnedMeshRenderer and the recorded type is Renderer. If the swap-in step recorded a base Renderer binding and the clip uses SkinnedMeshRenderer, the check rejects the binding. The curve keeps the reference to the clone material.
- Risk: Unlocked clone material destroyed while an animation curve still points to it.
- Proposed Fix: Replace all keyframes that point to the clone material across all committed clips without rigid type filtering.

## 3. Analysis Module Findings

### Finding 4: 32-Bit Integer Overflow in Repeat Mode Candidate Region Calculations
- Location: Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs lines 730, 833, 1339, 1424.
- Category: Latent bug.
- Description: In repeat sampling modes, candidate count calculations subtract and add 32-bit signed integers before a cast to long. When coordinate bounds exceed 32-bit signed integer limits, the arithmetic overflows to a negative integer. The subsequent long cast produces a negative value. The complexity limit guard evaluates to false and allows unbounded loops. Clamp sampling modes do not suffer from this issue because texture dimensions bound clamp coordinates.
- Risk: Extreme texture coordinate bounds bypass safety limits and freeze the editor.
- Proposed Fix: Cast repeat coordinates to long before arithmetic operations. Reject negative coordinate differences before the loop starts.

### Finding 5: Infinite Loop on Boundary Texels in Point Repeat Classification
- Location: Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs lines 1339 to 1360 and lines 1424 to 1445.
- Category: Latent bug.
- Description: ClassifyBilinearRepeat guards against cell values at boundary limits. ClassifyPointRepeat and HasMappedWitnessPointRepeat lack these guards. When a coordinate limit equals the maximum 32-bit integer, loop increment wraps to the minimum 32-bit integer. The loop continuation condition remains true forever.
- Risk: The editor hangs indefinitely on meshes with extreme UV coordinates.
- Proposed Fix: Add boundary cell checks to ClassifyPointRepeat and HasMappedWitnessPointRepeat. Return an Unknown outcome when coordinates reach integer limits.

### Finding 6: Missing Array Bounds Check in Triangle UV Input
- Location: Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs lines 99 to 101.
- Category: Latent bug.
- Description: TryGetUvSet validates the channel index and verifies that the UV list exists. It does not verify that vertex indices A, B, and C are within the list count. If a mesh contains truncated UV channel buffers, index access throws an ArgumentOutOfRangeException.
- Risk: Unhandled exception during analysis instead of conservative refusal.
- Proposed Fix: Check that vertex indices are non-negative and less than the UV buffer count. Return false when an index is out of bounds.

### Finding 7: CPU Overhead from BigInteger Conversions in Candidate Texel Loops
- Location: Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs lines 750, 753, 856, 859, 1346, 1349.
- Category: Performance impurity.
- Description: FloorMod in ExactUvGeometry accepts a BigInteger value. The candidate loop passes regular 32-bit integers. The C# runtime implicitly converts each integer to a BigInteger struct on each iteration. A single triangle evaluation can execute up to 65536 iterations per mip level. This generates avoidable CPU branching and conversion overhead during mesh analysis.
- Risk: Unnecessary CPU overhead during classification on large meshes.
- Proposed Fix: Add an internal static int FloorMod overload that operates strictly on 32-bit integers.

### Finding 8: Duplicate and Fragile Cross Product in Hull Generation
- Location: Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs lines 739 to 746.
- Category: Architectural impurity.
- Description: ExactUvGeometry.Cross subtracts numerators directly without scaling by denominators. The method assumes denominators always equal one. ExactUvGeometry.Orientation already implements an exact rational cross product with common denominator scaling.
- Risk: Incorrect convex hull calculation if fractional coordinates enter the method.
- Proposed Fix: Remove ExactUvGeometry.Cross and use ExactUvGeometry.Orientation in CreateHull.

## 4. Semantics Module Findings

### Finding 9: Missing Mode Zero Property Write on Multi Container Clones
- Location: Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs lines 259 to 275 and 325 to 345.
- Category: Latent bug.
- Description: When preparing an opaque clone for a Multi container shader, the code updates shader keywords for mode zero. It does not set the float property _TransparentMode to zero. The cloned material retains the original mode value of 1 or 2. If the clone is inspected later, mode validation detects a mismatch between keywords and property values.
- Risk: Material validation fails during subsequent build passes or test assertions.
- Proposed Fix: Set float property _TransparentMode to zero on Multi container opaque clones. Check this property in canonical verification.

### Finding 10: Unhandled Non-Finite Values in LilToon Alpha Mask Interpretation
- Location: Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs lines 290 to 305.
- Category: Latent bug.
- Description: The method checks that scale and offset exist. It does not check that scale and offset values are finite. It passes scale and offset to the UvMapping constructor. The constructor throws an ArgumentException on NaN or infinite values. This breaks the fail-closed contract.
- Risk: Unhandled ArgumentException crashes the build when materials have corrupted float properties.
- Proposed Fix: Check that scale and offset values are finite. Return an UnsupportedFeature diagnostic when non-finite values are present.

### Finding 11: Incorrect Sampler State Assigned to Blend Mask Texture Samples
- Location: Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonLayerAlphaTerm.cs lines 360 to 370.
- Category: Latent bug.
- Description: The code assigns the layer texture sampler to the blend mask TextureSample. The lilToon shader samples the blend mask with the sampler state of _MainTex. If the layer texture uses different wrap modes or filter modes than _MainTex, the classifier uses incorrect texture sampling parameters.
- Risk: Incorrect texel sampling when layer textures and main textures have different texture import settings.
- Proposed Fix: Pass the main texture sampler state to LilToonLayerAlphaTerm.Interpret and assign it to the blend mask sample.

## 5. Implementation Plan and Safety Recommendations

The findings must be addressed in prioritized order across focused pull requests:

1. Phase 1: Mathematical core safety in Analysis (Findings 4, 5, 6, 7, 8). These fixes stabilize pure arithmetic and eliminate coordinate overflow risks.
2. Phase 2: Host and Build lifecycle safety (Findings 1, 2, 3). These fixes protect against leaked material references on virtualized controllers and ensure complete curve restoration.
3. Phase 3: Semantics precision and robustness (Findings 9, 10, 11). These fixes prevent unhandled exceptions and align sampler states with shader reality.

Each phase must include failing tests before implementation. All changes must maintain strict fail-closed invariants.
