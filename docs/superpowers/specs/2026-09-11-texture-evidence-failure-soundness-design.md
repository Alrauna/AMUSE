# Texture evidence failure soundness design

Date: 2026-09-11. Status: approved for implementation. Companion
investigation: 2026-09-11-alpha-separator-merged-renderer-conversion.

## Problem

When the texture evidence for a sample that the alpha semantics depend
on fails to bind, the alpha resolution currently falls back to a
constant of full opacity. Every polygon of the affected material then
proves opaque and moves to opaque materials, while the same polygons
render translucent at runtime. The contract requires the opposite: a
conversion may only rest on evidence that exists.

The observed failure: AAO, anatawa12's Avatar Optimizer, merges the
avatar's skinned mesh renderers through its TraceAndOptimize
component's Merge Skinned Mesh option before NDMF's PlatformFinish
passes run. On the AAO merged renderer, the material evidence capture
binds an empty first texture slot for a lilToon transparent material
with an alpha mask, and the alpha resolution turns the unbound main
texture sample into a constant of full opacity. The failure point is
the capture and the resolution inside the AmusePlatformFinishPass, not
AAO. AAO only changes the renderer state that AMUSE analyzes.

The failure class includes: a capture that refuses for any named
reason, a chain missing from the captured evidence, and mip levels that
are not resident because the editor's texture quality settings limit
them. AAO TraceAndOptimize's Merge Skinned Mesh option is a trigger
context because it changes the state AMUSE analyzes. The invariant is
general and does not depend on that tool.

## Contract

1. Every texture sample that the resolved alpha semantics depends on
   must bind to a captured chain. If any required sample has no bound
   chain, the alpha resolution is unknown for that material. Unknown
   keeps the material's polygons on their original materials.
2. An unbound sample must never resolve to a constant. The shader
   behavior of sampling an unassigned texture must not leak into the
   evidence model: the evidence model reasons about bound textures
   only.
3. A capture refusal is a named refusal at the slot level, in the
   existing refusal enum style. The refusal names the texture and the
   reason family: unavailable capture, non-resident mips, or
   unsupported format.
4. Unknown information invalidates only the conclusions that depend on
   it. A texture used by one material must not refuse other materials.
   A renderer with mixed slots may convert the slots whose evidence is
   complete and keep the slots whose evidence failed.
5. The report must not count moved triangles for a material whose
   evidence failed. The summary keeps its existing shape.

## Mip residency policy

The editor can limit resident mip levels globally. A texture whose
consulted levels are not resident cannot produce exact per-texel
evidence for those levels. The design choice:

- the capture consults the resident levels it can and treats every
  non-resident consulted level as unknown
- a polygon whose proof needs a non-resident level stays unknown and
  keeps its original material
- this is a per-level degradation, not a renderer refusal, and it
  matches the existing rule that unknown information invalidates only
  the conclusions that depend on it

The predicate probe already models this gate; the production capture
adopts the same gate instead of producing a chain of unknown
provenance.

## Non-goals

- No new shader work. The predicate shaders and the inert byte
  identity contract are unchanged.
- No change to the mask composition algebra. The composition is
  correct; the failure is the unbound sample fallback.
- No renderer-wide refusal for a single failed texture.

## Test strategy

RED first, each against a named wrong implementation.

1. Resolver level: a transparent material with an alpha mask whose
   composed main sample has no bound chain, with the mask term present,
   must resolve unknown. The wrong implementation resolves a constant
   and proves.
2. Capture level: a texture whose capture refuses produces no chain,
   and the material resolution reports the named refusal instead of a
   chain.
3. End to end: a merged-renderer style build with one material whose
   evidence is unavailable moves zero triangles for that material and
   keeps it original.

The full product and research assemblies must stay green. The lab
avatar from the investigation is the manual validation surface after
the fix: the summary must report zero moved triangles for the affected
materials with the sliders at their inert defaults.
