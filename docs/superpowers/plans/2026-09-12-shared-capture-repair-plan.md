# Shared-capture predicate repair plan

Privacy note: This plan names public synthetic fixtures and vendor packages only. It contains no private assets, names, paths, or identifiers.

Date: 2026-09-12.
Base branch: `test/capture-predicate-isolation` at `3a5142d` (worktree from `main` at `d05171d`).

## Root cause

`UnityMaterialEvidenceCapture.Capture` groups all assignments of one texture source in a batch onto one `SharedTextureBuilder`, unions the requested evidence kinds, and binarizes the shared alpha field by the **minimum declared cutoff threshold** in the batch (`texture.CutoutThreshold < shared.CutoutThreshold`). A texel between two materials' declared cutoffs is opaque under the lower cutoff and not under the higher, so the min-threshold field over-proves for every stricter material in the batch. The stale comment claims only Poiyomi requests texture evidence; lilToon transparent and cutout both do, with different cutoff contracts.

## Fix

Scope the shared builder by `(TextureSourceId, CutoutThreshold)` instead of by source alone:

- `identified` becomes a dictionary of source to a dictionary of threshold to `SharedTextureBuilder`.
- `SharedTextureBuilder` takes its threshold at construction and makes it readonly.
- The evidence-kind union stays batch-wide within one threshold bucket: kinds decide which facts are captured, never the field values.
- Delete the min-threshold fold.

Every capture route (`UnityAlphaFieldEvidence`, `UnityGeneratedTextureEvidence`, `UnityStreamingTextureEvidence`) already keys session caches by the clamped cutoff, so per-threshold captures never collide and capture cost stays at one blit or readback per distinct threshold per texture (typically one or two).

## RED to GREEN

The four `SharedCutoutTexture*` and `SharedCutoutTextureKeepsEachMaterialsThreshold` cases on this branch fail today on `Expected: MustRemainTransparent But was: ProvenOpaque` and must pass after the fix, together with the whole fixture class (33 cases). The two-cutoff order cases pin that no first-wins or min-threshold drift survives the repair.

## Boundaries

- No semantic contract change: each family keeps its own request, and the transparent exact-255 arm stays policy-widened while declared-cutoff arms stay inert.
- The batch-wide evidence-kind union and its enforcement-point comment stay, narrowed to one threshold bucket.
- Validation: full EditMode assembly on this branch must stay green except vendor-absent ignores.

## Stop conditions

Stop and return evidence if the per-threshold capture regresses any existing capture test, if a route's cache key is found to drop the cutoff, or if the fix requires touching the classifier or the admission layer.
