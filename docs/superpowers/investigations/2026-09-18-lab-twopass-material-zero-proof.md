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

## 8. Same-day probe addendum (2026-09-18)

Read-only probes through the development bridge, after section 7 was written:

1. The depth-test policy is confirmed live in the Lab build: the build
   reported the materials resolved, so the eligibility admitted the source.
   The old depth refusal is gone.
2. The render-state gates are all clean on the Lab editor: build target
   admitted, the mask format is on the admitted allowlist (DXT1), no mipmap
   limit is active (global limit zero, master limit zero), the mask is not a
   streaming texture.
3. A direct red-channel capture of the mask texture succeeds with no
   refusal and returns a mip chain. The capture arm is not the blocker at
   the texture level.
4. The alpha mask replace semantics are implemented correctly in the shipped
   transparent frontend: for a replace-mode sample mask, the semantic output
   is complete and the main texture alpha is discarded. The main texture's
   alpha channel would otherwise be a suspect: about 27 percent of its
   texels sit at 254, strictly below one.
5. The texture coordinates of the flagged submesh read fine through both
   the legacy getter and the channel API, so the stage-B legacy-getter
   defect does not apply to the zero-proof.
6. The captured chain content is verified correct. A red-channel capture of
   the flagged material's mask in the Lab editor returns twelve levels with
   no missing evidence: 54.03 percent of level 0 texels hold the exactly
   opaque verdict, 45.44 percent at the policy's consulted level 4, zero
   erased bytes, zero out-of-vocabulary bytes, and the per-level decay
   matches the direct graphics readback. The capture and the semantics are
   both exonerated: the semantic output for a replace mask is complete and
   discards the main texture alpha (verified in source), and the captured
   chain is exact. The unknown factor therefore arises in the seam between
   the captured chain and the per-triangle outcome: the alpha field set
   lookup for the mask source, the resolution's texture-coordinate mapping,
   or the classifier input wiring.

What stays open: which factor of the resolved alpha chain is unknown. The
in-editor reflection probes that drive the full resolution hit the bridge's
30 second transport window, because the closed capture re-runs the vendor
attestation digest on every call. The instrumented run belongs in the
development project: a debug-instrumented classification of a stand-in
fixture shaped like this material (DXT1 sRGB mask, replace mode, two-region
coverage) will name the unknown factor, and the RED test from section 7
applies unchanged.

## 9. Reproduction on the development branch (2026-09-18)

The defect now reproduces in the development project's own test suite. The
new falsifier `Dxt1SrgbMask_WhiteRegionTriangleProvesOpaque` in
`Tests/Editor/Build/AlphaSeparationPreparationTests.cs` builds a transparent
stand-in whose alpha mask imports compressed (DXT1) under an sRGB import with
mips, in replace mode, with one triangle whose domain sits inside the exactly
opaque region. Observed RED: the materials resolve, no refusal fires, and the
white-region triangle fails to prove (expected 1, was 0). This pins the
zero-proof in a deterministic, CI-runnable fixture and isolates it from
everything Lab-specific.

A temporary debug probe (`AmuseDbgDxt1Probe.cs`, tagged AMUSE-DBG, for
removal) drives the same pipeline stage by stage. Observed: selection and
closed capture succeed; the field set holds both fields (main alpha and mask
red, threshold 1); the resolution is a classified, non-uniform resolution
with a UV mapping; the per-triangle classification then throws a
NullReferenceException. The production classification stage crashes on this
material shape rather than answering Unknown or ProvenOpaque.

## 10. Blocker and the confirmed contradiction (2026-09-18, later)

The compile-pipeline wedge resolved after a forced script recompilation (no
editor restart needed). On the fresh assembly, both facts now hold
simultaneously:

1. The end-to-end falsifier `Dxt1SrgbMask_WhiteRegionTriangleProvesOpaque`
   still fails: the materials resolve, no refusal fires, and the white-region
   triangle proves 0 of 1.
2. The direct seam-driven probe of the same material proves the same
   triangle `ProvenOpaque` through `GatherAlphaFields` + `ResolveFor` +
   `Classify`: resolution `IsResolved=True`, chain level 0 exactly
   8192/16384 (the white half), classify outcome `ProvenOpaque`.

So the capture, the field set, the resolution, and the classifier all behave
correctly when driven through the verified seams. The zero-proof is
introduced by whatever the end-to-end prepare path does differently — prime
suspects: the renderer snapshot geometry capture (`Capture(renderer)` — the
UV0 domain the build actually consults), the policy bounds threading through
`RunBarrier` (the fixture pins sizes; the analysis may see different bounds
than the probe's inert defaults), or the per-triangle input construction in
the production `Analyze`.

The falsifier stays RED as the regression pin. Next step: attach the managed
debugger (or continue the AMUSE-DBG instrumentation) inside the end-to-end
`Analyze(renderer)` path and dump its resolution and chain against the
probe's — the first divergence is the defect.

## 11. Blocker and next step

The contradiction is confirmed on the fresh assembly: the unit-level seam
path (capture → gather → resolve → classify, all production code) proves the
white-region triangle `ProvenOpaque`, while the end-to-end prepare path
(`RunBarrier` → `ResolveRuntimeStates` with the component's default policy:
mip cap 4, min texture size 128) proves 0 of 1. The prepare path threads
`maxMipLevel=4, minTextureSize=128` into `GatherAlphaFields` correctly, and
the policy bounds are inert defaults on both paths.

The remaining divergence sits inside `AdmittedMaterialStates.ResolveSlot` —
the state-admission composition between the captured evidence, the animation
closure, and the per-state resolutions — or in `DistinctResolutions`. Next
session: instrument `ResolveSlot` (AMUSE-DBG) to dump its admitted states
and each state's resolution for the falsifier fixture, and diff against the
direct probe's single Classified resolution. The RED falsifier
(`Dxt1SrgbMask_WhiteRegionTriangleProvesOpaque`) is committed and stays as
the regression pin for the eventual fix.

## 12. Relation to existing records

The 2026-09-17 notes stand unchanged. This build confirms their Finding A is
fixed and moves the open defect from conversion eligibility to per-triangle
classification. The FORWARD_BACK pre-pass proof from the 2026-09-17 note
stays open and is unrelated to this zero-proof outcome.

Branch: `feat/ztest-alpha-policy` at `24ddb87`. This note is uncommitted. Git
authorization stays with the product owner.

## 13. Resolution: the zero-proof is the exact wrap-blend rule (2026-09-18)

Privacy note: same sanitization as section 1. This section records
instrumented runs of public synthetic fixtures in the development editor
instance. It names no private avatar, renderer, material, or texture.

Method: temporary AMUSE-DBG trace hooks in `ResolveRuntimeStates`,
`AdmittedMaterialStates.ResolveSlot`, and `IntersectResolvedOutcomes`, plus a
reflection dump helper. The hooks observed the real falsifier build through
the production entry. Removal is tracked below and in the fix commit.

### 13.1 What the trace showed

The end-to-end prepare path behaved exactly per contract at every stage:

1. Policy: mip cap 4, minimum texture size 1 (the fixture pins All Sizes),
   density cap 0, no proof-relevant bindings, one slot, one admitted
   material.
2. `ResolveSlot` returned one classified resolution. Chain level 0 held
   exactly 8192 of 16384 opaque verdicts, levels 1 to 4 held exactly half
   each. Mapping identity, channel 0, bilinear repeat sampling. This is the
   same resolution the direct seam run produced.
3. `DistinctResolutions` kept 1 of 1.
4. The snapshot UV0 values equaled the authored fixture values exactly.
5. The resolution itself classified the authored triangle input
   `MustRemainTransparent`.

So the suspect list from section 11 is cleared. The capture, the field set,
`ResolveSlot`, `DistinctResolutions`, the snapshot geometry, and the
classifier input wiring are all exact.

### 13.2 Which level refutes, and why

A per-level bisect of the same resolution on the same input gave
`ProvenOpaque` at levels 0 to 3 and `MustRemainTransparent` at level 4.

At level 4 the mask is 8 by 8. The white half spans texels 0 to 3. The
triangle domain starts at 0.05 of the texture width, which is 0.4 texels.
The bilinear support of a texel under repeat spans half a texel on each
side, so the wrapped black column 7 carries the support interval from minus
0.5 to plus 0.5 texels. The domain edge at 0.4 texels sits inside that
interval. A bilinear sample at the domain edge provably blends the wrapped
black column, so the sampled alpha is provably below one.
`MustRemainTransparent` is absorbing across the consulted chain, so one
refuting level keeps the triangle on the original material.

This is the documented contract, not a defect: the hardware may select any
consulted level, and a triangle that provably samples a sub-one blend at
some consulted level is not proven opaque. The falsifier fixture's premise,
"far from the region boundary at every consulted level", is false at level
4. The fixture carries the defect, not the pipeline.

### 13.3 Why section 10 saw a contradiction

The direct seam probe captured with minimum texture size 128. A 128 by 128
texture answers that scope at level 0 only, so the probe's chain held one
level and the domain proves there. The falsifier pins All Sizes, so the
prepare path consults levels 0 to 4. The two runs used different proof
scopes. Section 11's claim that the policy threading matched was wrong on
that parameter. The old probe run logs confirm it: the direct run dumped one
chain level, the prepare run dumped five.

### 13.4 Consequence for the Lab renderer

The bounding-box addendum in section 4 counted a triangle when the triangle's
box sat in pure white at one level. It did not model the half-texel bilinear
support margin, the repeat seam, or the all-level conjunction. So "at least
228408 provable" was never established under the actual proof rule. The
observed zero on that renderer is consistent with the exact rule: a dense
garment layout at a 128 by 128 coarsest consulted level leaves most
triangles within half a texel of some non-opaque texel at some level.

### 13.5 Fix plan

1. Correct the falsifier fixture so its triangle domain clears every
   non-opaque support at every consulted level. A domain inside 0.3 to 0.4
   of the texture width clears the wrapped seam and the black half at all
   levels 0 to 4. The falsifier then goes green and pins the true contract.
2. Add one characterization test that keeps a seam-adjacent domain
   unproven, so a later classifier change cannot silently drop the wrap
   support rule. This assertion passes on first run and is recorded as
   characterization, never as RED.
3. Remove the temporary trace hooks, the dump helper, and the temporary
   probe.
4. Observe the falsifier green, then the full product and research EditMode
   assemblies. Counts land in section 14.

## 14. Validation observed (2026-09-18)

All runs went through the Unity Test Runner in the development editor
instance, EditMode, on this branch after the fix and the instrumentation
removal. Observed counts:

1. `Dxt1SrgbMask_WhiteRegionTriangleProvesOpaque` passed. The fixture
   correction turned the RED pin green without any production change.
2. `Dxt1SrgbMask_SeamBlendTriangleStaysUnproven` passed. First observation,
   recorded as characterization.
3. Full product and research assemblies: 2258 tests, 2258 passed, 0 failed,
   0 skipped, in 210 seconds. Two environment-gated DAO integration tests
   reported Inconclusive outside their gated integration project, which the
   gate itself declares.
4. A research assembly probe run passed with 1 of 1, confirming the research
   assembly loads and executes in the same session.

The temporary trace hooks, the dump helper, and the temporary probe are
removed in the fix commit. A repository-wide source search for the debug tag
returns zero hits after removal. The falsifier and its companion test remain
as the permanent pins: one proves the wrap-clear contract, one pins the
conservative refusal at the seam.
