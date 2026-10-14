<!-- Privacy note: This investigation record sanitizes all private identifiers, avatar names, asset paths, and machine details according to repository privacy rules. Private test avatars are characterized by role only. Observed counts are given as ranges. -->

# Technical Investigation: Edit-Time Material Refusals, Streaming Mipmaps, and Multi-Mesh Sampling

Date: 2026-10-14.

Base: `bbae08f` on branch `feat/material-transformations-ux`.

---

## 1. Overview and Problem Statement

As of 2026-10-14, the Show Material Transformations foldout in the inspector preview displays:
`Refused: AMUSE currently does not support this material`
for several materials on an avatar.

When the same avatar enters Play Mode or runs an avatar build, AMUSE converts those exact materials without error.

This investigation identifies:
1. Why opaque materials with streaming textures displayed `Untouchable` with full polygon counts while transparent materials with streaming textures displayed `Refused`.
2. Whether edit-time evaluation of streaming textures is a fundamental engine constraint or an artifact of dry run interceptor design.
3. How AMUSE processes multiple meshes that share and sample the same material.
4. How the engine and the user interface can accurately provide transformation information without reporting false refusals.

---

## 2. Evidence and Technical Trace

### A. The Opaque versus Transparent Asymmetry

Inspection of materials on test avatars in the Census Lab editor instance revealed an important pattern:
- Several materials use textures with `streamingMipmaps: true`.
- Opaque materials (such as body skin, face base, and hair) displayed:
  `Untouchable: 0 of out X polygons may be moved`
  where `X` ranged between 1,000 and 25,000 polygons.
- Transparent materials (such as socks, cap accents, and face effects) displayed:
  `Refused: AMUSE currently does not support this material`
  with polygon counts of `-1`.

The root cause of this asymmetry is the shader semantic evidence request:
- In `<repo-root>/Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`, opaque and outline lilToon shaders declare zero texture properties.
- Because an opaque shader has a constant alpha value of one, AMUSE proves opacity without reading texture files.
- In `<repo-root>/Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`, `DroppedStreamingProperties()` evaluates only declared texture properties.
- Because opaque materials declare no texture properties, `DroppedStreamingProperties()` returns an empty list.
- Opaque materials bypass the streaming texture interceptor entirely.
- `DeriveAlreadyOpaqueBySlot` marks them as already opaque.
- `SlotTransformDecision` reports `SlotTransformOutcome.AlreadyOpaque` with exact polygon counts from the mesh geometry.
- The inspector renders `AlreadyOpaque` as `Untouchable: 0 of X polygons may be moved`.
- In contrast, transparent materials (`Hidden/lilToonTransparent`) declare texture properties such as `_MainTex` and `_AlphaMask`.
- When their textures have streaming mipmaps enabled, `DroppedStreamingProperties()` marks them as skipped.
- The skipped materials enter `outcome.UnattestedOrdinals`.
- `UnityAnimationEvidenceCapture` assigns them the `Unsupported` family sentinel.
- `AdmittedMaterialStates.ResolveSlot` fails closed with `AdmittedMaterialSemanticsUnknown`.
- `RefusedSlotRow()` formats the unresolved slot as `Refused: AMUSE currently does not support this material`.

---

### B. Edit-Time Texture Capture Reality

In `<repo-root>/Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs`, texture evidence capture checks `texture2D.streamingMipmaps` at line 214.
When true, it branches to `UnityStreamingTextureEvidence.TryCapture()`.

`UnityStreamingTextureEvidence.TryCapture()` contains two distinct stages:

1. **In-Memory Decoding Stage (`SourceImageAlphaReader.cs`):**
   - At line 97, it calls `SourceImageAlphaReader.TryReadSourceAlphaChain()`.
   - `SourceImageAlphaReader` reads authoring image bytes from disk using `File.ReadAllBytes()`.
   - It decodes PNG and TGA textures directly in memory using `ImageConversion.LoadImage()`.
   - It constructs the `AlphaMipChain` in memory and destroys temporary texture objects immediately in a `finally` block.
   - **It writes zero assets to disk and causes zero asset re-imports.**

2. **Disk-Writing Clone Fallback:**
   - Lines 103 to 230 execute only if `SourceImageAlphaReader` returns false.
   - This fallback creates a temporary folder at `Assets/Amuse.StreamingReadback`.
   - It copies the texture asset, changes importer streaming settings, and calls `SaveAndReimport()`.
   - It deletes the temporary clone in a `finally` block.

In `<repo-root>/Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`, `InterceptStreamingTextures` was introduced to prevent disk writes in edit mode.
However, it treats all streaming textures as an all-or-nothing exclusion.
It skips textures even when `SourceImageAlphaReader` can decode them in memory without disk writes.
Because the textures sampled by the transparent materials are authoring PNG files, `SourceImageAlphaReader` can decode them in memory during edit time.

---

### C. Multi-Mesh Sampling Architecture

When multiple mesh renderers share a single material:
1. AMUSE processes renderers sequentially in `AlphaSeparationDryRun.Step()`.
2. Each renderer owns a distinct mesh with distinct vertex positions, polygon counts, and UV coordinates.
3. `UnityRendererAlphaAnalysis.CaptureGeometry()` extracts the specific submesh polygon buffer for that renderer.
4. `RuntimeStateResolution.ClassifyRuntimeStates()` evaluates the UV domain of each polygon against the material alpha field.
5. In a Play Mode build, left and right limb meshes both evaluate against the shared sock material. Both meshes prove opaque and separate their polygons.
6. In the current dry run, because the shared material is dropped by `InterceptStreamingTextures`, every mesh that references that material reports `Refused`.

---

## 3. Analysis of Proposed Solutions

### Solution Component 1: Allow In-Memory Decoding in Edit Mode

We can permit `SourceImageAlphaReader` to decode streaming textures during dry runs while blocking the disk-writing clone fallback:
- In `UnityStreamingTextureEvidence`, add a scoped control flag: `internal static bool AllowDiskFallback { get; set; } = true;`.
- Add an `IDisposable` scope or enforce a strict `try-finally` block:
  ```csharp
  var previous = UnityStreamingTextureEvidence.AllowDiskFallback;
  UnityStreamingTextureEvidence.AllowDiskFallback = false;
  try
  {
      // dry run capture
  }
  finally
  {
      UnityStreamingTextureEvidence.AllowDiskFallback = previous;
  }
  ```
- When `AllowDiskFallback` is false, `TryCapture` returns false immediately if `SourceImageAlphaReader` cannot read the source file.
- `AlphaSeparationDryRun` passes textures to capture rather than skipping them unconditionally.
- When `SourceImageAlphaReader` succeeds, AMUSE computes exact polygon splits at edit time.
- `NoteKind.StreamingMipmaps` is emitted only when at least one streaming texture fails in-memory decode and is deferred to build time. It is not emitted when all streaming textures decode successfully in memory.

### Solution Component 2: Accurate Status for Deferred Materials and Geometry Extraction

When a material cannot be resolved in edit mode (for example, compressed streaming textures or locked materials awaiting build-time transient unlock):
- In the current code, `AlphaSeparationDryRun.ComputeRows()` returns early before calling `UnityRendererAlphaAnalysis.CaptureGeometry()`.
- To preserve polygon counts for deferred or refused slots, `ComputeRows()` must execute `UnityRendererAlphaAnalysis.CaptureGeometry()` even when slots are unresolved.
- Submesh geometry is an inherent property of the mesh on the renderer. It does not depend on material resolution.
- Reading `extraction.Snapshot.Submeshes[submeshIndex].Indices.Count / 3` provides the exact submesh polygon count `X`.
- The dry run already knows whether the material shader is recognized and supported by AMUSE.
- The dry run must not report `Refused: AMUSE currently does not support this material`.
- The dry run must report a deferred status sentence, such as:
  `Deferred: 0 of out X polygons moved (evaluated at build time)`
- This keeps the two-line layout and communicates exact polygon numbers without false refusal warnings.

---

## 4. Risks and Edge Cases

1. **Memory Pressure:**
   - Decoding large image files into memory in edit mode could consume memory if held long term.
   - Mitigation: `SourceImageAlphaReader` immediately destroys the temporary `Texture2D` in its `finally` block and retains only the compressed bitmask `AlphaMipChain`.

2. **File Formats Not Supported by `SourceImageAlphaReader`:**
   - `SourceImageAlphaReader` returns false for PSD files, JPEG images, textures with `alphaSource == FromGrayScale`, procedural textures, and textures where importer downscaling causes source dimensions to disagree with target dimensions.
   - Mitigation: These textures safely hit the deferred status fallback without triggering disk writes.

3. **Attested Locked Materials:**
   - Materials locked by Poiyomi remain locked in scene state to prevent dirtying user scene files.
   - The check must verify both `LockedMaterialIdentity.RecognizedLockedIdentity(material)` and `LockedMaterialIdentity.OriginalShaderAttested(material)`.
   - If the original shader is attested, the material reports the deferred status.
   - If the original shader is unattested or missing, the material is truly unconvertable and reports the refusal status.
