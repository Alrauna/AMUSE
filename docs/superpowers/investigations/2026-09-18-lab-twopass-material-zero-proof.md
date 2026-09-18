# Census Lab recheck: the depth-test policy works, and classification proves zero

Privacy note: This record characterizes one garment material from the Census
Lab corpus and one renderer that uses it. It names no avatar, renderer,
material, texture, or hierarchy identifier. It records no per-renderer table
beyond the single investigated renderer. Texture facts stay at the level the
diagnosis needs. Every status claim is dated.

Date: 2026-09-18. Status: stage 0 investigation record. No production code
changed. Existing investigation notes are unchanged; this note extends them.

Method: read-only session against the Census Lab editor instance through the
development bridge. Console reads, one status-store read through reflection,
and three in-memory probes (asset introspection, one graphics readback, one
mesh and coverage computation). No scene, asset, or file in the Census Lab
project was modified. Repository facts come from this checkout.

## 1. What the Lab runs

The Census Lab project embeds the two AMUSE packages as links into this
repository's working tree. The working tree is the branch
`feat/ztest-alpha-policy` at `24ddb87`, clean. So the Lab build ran the new
depth-test policy code.

## 2. What the console shows

The last Lab build processed the AMUSE passes. The semantic barrier ran for
about 6.9 seconds. The alpha separation apply pass ran. The build reported
one renderer error:

- `AMUSE could not prove any triangle on this renderer.`

That string is `amuse.renderer.AdmittedMaterialSemanticsUnknown`. Its
description says: the materials resolved, but every triangle answer came back
unknown. The component status store said: analyzed 0 renderers and moved 0
triangles; 1 renderer kept everything original.

## 3. Finding A: the depth-test policy works

The 2026-09-17 notes predicted the old refusal `UnsupportedDepthComparison`
for this material. That refusal is gone. The report says the materials
resolved, which only happens when the eligibility admitted the source. The
policy is live and does what the design said. Finding A of the 2026-09-17
note is closed by this build.

## 4. Finding B: classification proves zero on a provable renderer

The material's alpha chain is pure mask replace: the fragment alpha equals
the sampled mask red channel. So one proof fact decides everything: is the
mask red channel exactly one over each triangle's consulted domain?

Measured runtime facts:

| Fact | Value |
|---|---|
| Mask texture format | DXT1, sRGB import, 2048 by 2048, 12 mips, not CPU readable |
| Importer note | The 4096 source image is capped to 2048 at import |
| Main texture | BC7, 1024 by 1024 |
| Mask mode, scale, value | 1 (replace), 1, 0 — the admitted pair |
| Cutoff | 0.001 |
| Main and mask scale and offset | identity |

Probe 1, red channel of the mask at mip 0 through the graphics pipeline:
54.03 percent of texels decode to exactly 255, no texel sits at 254, 35.88
percent sit at 0, and 138468 of 262144 four-by-four blocks are pure white.
The white regions survive compression exactly.

Probe 2, coverage over the renderer's mesh. The flagged submesh holds 524288
triangles with 263169 first-channel texture coordinates; 955 coordinates sit
outside the zero-to-one range (the sampler wraps them). A conservative
bounding-box check of every triangle against the pure-white map:

| Consulted mip level | Triangles with an all-white box |
|---|---|
| 0 | 269389 |
| 1 | 268938 |
| 2 | 264878 |
| 3 | 252851 |
| 4 | 228408 |

At least 228408 triangles, about 44 percent of the submesh, have a mask
domain that is exactly one at every consulted level. The count is
conservative for triangles with in-range coordinates, because a bounding box
covers the triangle.

## 5. Conclusion

The pipeline proved 0 of 524288 triangles where at least 228408 are provable
from the captured facts. That is a genuine defect: an unknown factor poisons
the whole alpha chain for this slot. A true negative cannot look like this,
because the mask texture holds large exactly-one regions and the geometry
reaches them.

Prime suspects, in order:

1. The mask red-channel evidence route answers unknown or empty for this
   texture shape: DXT1, sRGB, no separate alpha channel, importer-capped
   size, twelve mips.
2. The mesh or texture-coordinate extraction leaves the mask domain
   unevaluated for this mesh shape: 524288 triangles, 263169 coordinates,
   955 out-of-range coordinates under wrap sampling.

Texture exactness and honest interleaving are ruled out by probes 1 and 2.

## 6. Side observation

The Lab build console lists several `AMUSE test` passes. The Lab compiles the
product test assembly through the package links, so the test infrastructure's
passes register in a real build. They ran 0 milliseconds each. This is worth
a packaging decision later: the test assembly ships inside the product
package links today.

## 7. Next actions

1. In the development project, with the debug-instrumentation pattern, trace
   one flagged material through classification: which factor of the alpha
   chain is unknown, and which evidence record carries it.
2. Check the alpha-field capture's channel selection for a DXT1 sRGB mask
   against the mask semantics' red requirement, including the no-alpha
   importer theorem path.
3. Check the texture-coordinate extraction on this mesh shape, including the
   out-of-range coordinates and wrap sampling.
4. RED first with a stand-in fixture that reproduces the shape: DXT1, sRGB,
   importer-capped, with a two-region mask and a mesh whose triangles sit
   inside the white region.

## 8. Relation to existing records

The 2026-09-17 notes stand unchanged. This build confirms their Finding A is
fixed and moves the open defect from conversion eligibility to per-triangle
classification. The FORWARD_BACK pre-pass proof from the 2026-09-17 note
stays open and is unrelated to this zero-proof outcome.

Branch: `feat/ztest-alpha-policy` at `24ddb87`. This note is uncommitted. Git
authorization stays with the product owner.
