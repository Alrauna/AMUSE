# Edit-Time Streaming Texture In-Memory Decoding and Accurate Deferred Material Status — Design

Date: 2026-10-14

Base: `bbae08f` on branch `feat/material-transformations-ux`

Investigation: `docs/superpowers/investigations/2026-10-14-preview-material-refusals-streaming-investigation.md`

## 1. Overview

As of 2026-10-14, the Show Material Transformations foldout in the inspector preview displays:
`Refused: AMUSE currently does not support this material`
for materials with streaming mipmaps or locked shaders, even when those materials convert successfully during an avatar build.

This design introduces two targeted improvements:
1. Enables in-memory streaming texture decoding during edit-time dry runs via `SourceImageAlphaReader`.
2. Differentiates truly unsupported materials from materials that evaluate at build time.
Each slot row displays accurate status and preserves exact submesh polygon counts.

---

## 2. Core Decisions

### Decision 1: Scoped Disk Fallback Guard in `UnityStreamingTextureEvidence`

`UnityStreamingTextureEvidence.TryCapture()` executes two stages:
1. In-memory file decoding via `SourceImageAlphaReader.TryReadSourceAlphaChain()`.
2. Disk-writing asset clone fallback under `Assets/Amuse.StreamingReadback`.

To prevent disk writes during edit-time dry runs while enabling in-memory decoding:
- Add a scoped control property to `UnityStreamingTextureEvidence`:
  `internal static bool AllowDiskFallback { get; set; } = true;`
- Provide an `IDisposable` struct `DisallowDiskFallbackScope`:
  ```csharp
  internal readonly struct DisallowDiskFallbackScope : IDisposable
  {
      private readonly bool _previous;
      internal DisallowDiskFallbackScope(bool previous) => _previous = previous;
      public void Dispose() => UnityStreamingTextureEvidence.AllowDiskFallback = _previous;
  }

  internal static DisallowDiskFallbackScope DisallowDiskFallback()
  {
      var prev = AllowDiskFallback;
      AllowDiskFallback = false;
      return new DisallowDiskFallbackScope(prev);
  }
  ```
- In `UnityStreamingTextureEvidence.TryCapture()`, check `AllowDiskFallback` before line 103.
- When `AllowDiskFallback` is false, `TryCapture()` returns false immediately if `SourceImageAlphaReader` returns false.
- It never touches the temporary asset directory and never invokes `AssetDatabase.CopyAsset()`.

### Decision 2: Dry Run Execution with In-Memory Streaming Decoding

In `AlphaSeparationDryRun.cs`:
- Wrap dry run capture in `using (UnityStreamingTextureEvidence.DisallowDiskFallback())`.
- In `CaptureFor()`, allow streaming textures to reach `UnityAlphaFieldEvidence.TryCapture()`.
  Because `AllowDiskFallback` is false, `SourceImageAlphaReader` decodes authoring PNG and TGA files in memory.
  If in-memory decoding is impossible (for example, compressed or unsupported formats), `TryCapture()` returns false without writing to disk.
- Remove redundant pre-filtering in `DroppedStreamingProperties()`.
  This avoids duplicate file reading and prevents logic drift between dry run checks and actual decoders.
- Emit `NoteKind.StreamingMipmaps` only when a streaming texture fails in-memory decoding and defers to build time.
  Update both `Probe()` and `RecordConsentFacts()` to check whether the streaming texture requires build-time deferral before emitting the note.

### Decision 3: Safe Submesh Polygon Extraction on Unresolved Slots

In `AlphaSeparationDryRun.cs`, when a slot is unresolved (`!slotResults[slotIndex].IsResolved`), the dry run must preserve the exact submesh polygon count without throwing exceptions:
- Do not call `extraction.Snapshot.Submeshes[slotIndex]`, because `extraction.Snapshot` is null when all slots on a renderer are unresolved.
- Instead, read the submesh triangle count directly from the live renderer's shared mesh:
  ```csharp
  private static int SubmeshPolygonCount(Renderer renderer, int slotIndex)
  {
      Mesh mesh = null;
      if (renderer is SkinnedMeshRenderer smr)
      {
          mesh = smr.sharedMesh;
      }
      else if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter))
      {
          mesh = filter.sharedMesh;
      }

      if (mesh == null || slotIndex < 0 || slotIndex >= mesh.subMeshCount)
      {
          return -1;
      }

      if (mesh.GetTopology(slotIndex) != MeshTopology.Triangles)
      {
          return -1;
      }

      return (int)(mesh.GetIndexCount(slotIndex) / 3);
  }
  ```
- This helper is completely safe, zero-allocation, and works for any renderer state.

### Decision 4: Distinguishing Deferred Build-Time Materials from Refused Materials

When a slot is unresolved (`!slotResults[slotIndex].IsResolved`), `AlphaSeparationDryRun` inspects the slot's live material to distinguish deferred materials from truly unsupported materials:
- Let `material = renderer.sharedMaterials[slotIndex]`.
- A slot is **Deferred** if either of the following holds:
  1. `material` has an attested locked shader:
     `LockedMaterialIdentity.RecognizedLockedIdentity(material)` is true and `LockedMaterialIdentity.OriginalShaderAttested(material)` is true.
  2. `material` uses an admitted supported shader family (such as lilToon transparent or Poiyomi) and contains at least one streaming mipmap texture that could not decode in memory.
- A slot is **Refused** if it is not deferred:
  - Unrecognized shader family (such as custom proprietary shaders).
  - Locked material with an unattested or missing original shader.
  - Unrecognized animation curves or structural binding failure.

For a deferred slot, emit:
`MaterialTransformationRow.Slot(rendererPath, rendererTypeName, slotIndex, 0, totalTriangles, RowText.DeferredSlotSentence(totalTriangles));`

For a refused slot, emit:
`MaterialTransformationRow.Slot(rendererPath, rendererTypeName, slotIndex, -1, totalTriangles, RowText.RefusedSlotSentence());`

### Decision 5: Renderer Refusal Box Suppression for Deferred Renderers

In `AlphaSeparationDryRun.ComputeRows()`:
- When `resolved.Refusal != RendererAnalysisRefusal.None`, check whether every unresolved slot on that renderer is deferred.
- If every slot is deferred, do not insert `RendererRefusedRow`.
  This prevents conflicting user messages where an orange refusal HelpBox appears above slots that are merely deferred to build time.
- If at least one slot is genuinely refused, or if geometry extraction failed with a renderer refusal, insert `RendererRefusedRow` as a warning HelpBox.

### Decision 6: Status Sentence Builders in `RowText`

In `MaterialTransformationRow.cs`, update `RowText`:
```csharp
internal static string DeferredSlotSentence(int total)
{
    return "Deferred: 0 of out " + total.ToString(CultureInfo.InvariantCulture) +
           " polygons may be moved (evaluates at build time)";
}
```

The second line of each slot row displays one of five explicit states:
1. `Convertable: X out of X polygons may be moved`
   Trigger: All polygons on this submesh are proven opaque.
2. `Splittable: Y of out X polygons may be moved`
   Trigger: A non-zero proper subset of polygons on this submesh is proven opaque.
3. `Untouchable: 0 of out X polygons may be moved`
   Trigger: Zero polygons move (already opaque or coverage below threshold).
4. `Deferred: 0 of out X polygons may be moved (evaluates at build time)`
   Trigger: The material is supported by AMUSE, but requires build-time evaluation.
5. `Refused: AMUSE currently does not support this material`
   Trigger: The material is genuinely unsupported by AMUSE.

---

## 3. Invariants and Architectural Purity

1. **Dry Run Purity:**
   The dry run never writes files to disk, never creates temporary project assets, and never mutates scene objects.
2. **Layering Direction:**
   `Build/` and `DryRun/` call `Host/` and `Analysis/`. `Host/` never calls `DryRun/`.
3. **RAII State Scope:**
   `UnityStreamingTextureEvidence.AllowDiskFallback` is controlled via `DisallowDiskFallbackScope`. It always restores prior state even during unhandled exceptions.
4. **Fail-Closed Correctness:**
   If in-memory decoding fails or source images are unavailable, AMUSE defers to build time. It never guesses alpha values.
5. **Privacy and Path Cleanliness:**
   No absolute paths, user paths, machine ports, or private asset names exist in code, comments, or tests.

---

## 4. Ponytail Adherence

Applying the Ponytail ladder:
1. **Rule 1 (YAGNI):** No new caching layers, no complex decode pipelines.
2. **Rule 2 (Codebase Reuse):** Reuse existing `SourceImageAlphaReader` and existing `Submesh` index buffers.
3. **Rule 3 (Smallest Diff):**
   - Add `DisallowDiskFallbackScope` and `AllowDiskFallback` flag in `UnityStreamingTextureEvidence.cs`.
   - Update `AlphaSeparationDryRun.cs` capture and slot classification logic.
   - Add `DeferredSlotSentence()` in `RowText.cs`.
   - Update `AmuseAvatarOptimizerEditor.cs` tests to cover deferred sentences.

---

## 5. Test Strategy

1. **`UnityStreamingTextureEvidenceTests`:**
   - Verify `DisallowDiskFallbackScope` prevents temporary folder creation and asset cloning when authoring images cannot be read.
   - Verify in-memory decoding succeeds for readable PNG streaming textures under `DisallowDiskFallbackScope`.

2. **`AlphaSeparationDryRunTests`:**
   - Verify that a cutout material with a streaming PNG texture decodes in memory and produces `Splittable` or `Convertable` without refusal.
   - Verify that a streaming texture that cannot decode in memory produces `Deferred: 0 of out X polygons may be moved (evaluates at build time)` with the exact submesh polygon count `X`.
   - Verify that an attested locked material produces `Deferred: 0 of out X polygons may be moved (evaluates at build time)`.
   - Verify that an unrecognized shader produces `Refused: AMUSE currently does not support this material`.
   - Verify that a renderer with only deferred slots suppresses the renderer-level refusal HelpBox.

3. **`MaterialTransformationRowTests`:**
   - Verify that `DeferredSlotSentence(total)` formats with the exact expected string.
