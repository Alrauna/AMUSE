# lilToon layer alpha equation, pinned from the installed vendor tree

Date: 2026-09-15. Status: stage 0 record for the layer alpha support
design of this date. Implementation has not started.

Source: the lilToon package installed in the dev project at
`Packages/jp.lilxyzw.liltoon/`, version 2.3.4 per its package manifest.
The package is untracked in Git. The AMUSE attestation digests this
installed tree at build time, so these pins describe exactly the tree
the product reads. No vendor source is committed here. This record
names shader properties and file locations. It names no avatar,
renderer, material, or texture asset.

Every claim below is [MEASURED] against the installed files unless it
carries another tag. Line numbers speak for this vendor version and
this date.

## 1. Composition order in the fragment

The main pass composes alpha in this order
[`lil_pass_forward_normal.hlsl:338-413`]:

1. Main color sample and tint. This is the alpha the existing
   frontends already model.
2. Second main texture layer, function `lilGetMain2nd`
   [`lil_common_frag.hlsl:724-811`].
3. Third main texture layer, function `lilGetMain3rd`
   [`lil_common_frag.hlsl:820-907`].
4. Alpha mask, macro `OVERRIDE_ALPHAMASK`
   [`lil_common_frag.hlsl:457-476`, applied at
   `lil_pass_forward_normal.hlsl:361-364`].
5. Main dissolve. The existing frontends gate this at mode zero.
6. Dither, cutout family only.
7. The cutout saturate and clip, or the transparent clip
   [`lil_pass_forward_normal.hlsl:392-413`].

The layer functions run before the alpha mask. A mask replace mode
overwrites the layered value. The composition order for the proof is
main alpha, then second layer, then third layer, then mask, then the
existing clip terms. The subpass file repeats the same order before its
own clip tail [`lil_common_frag_alpha.hlsl:40-115`].

## 2. Off-state reduction

The layer function sets `color2nd = _Color2nd` and returns with no
alpha write when `_UseMain2ndTex` is off
[`lil_common_frag.hlsl:739-740`]. The alpha writers sit inside the
toggle gate and inside `#if LIL_RENDER != 0`
[`lil_common_frag.hlsl:798-807`]. The design assumption holds: a layer
toggled off is alpha neutral. [MEASURED, re-verified this date]

## 3. The layer alpha chain, measured

Inside the toggle gate, the layer alpha builds in this order. The third
layer is symmetric with third names at the same lines plus 96.

1. Base: `color2nd.a = _Color2nd.a` times the texture sample alpha
   [`lil_common_frag.hlsl:739,748`]. `_Color2nd` defaults to white
   [`lts.shader:55`]. The color alpha is proof relevant and must be
   captured finite.
2. Sample coordinate: `uv2nd = fd.uv0` by default. `_Main2ndTex_UVMode`
   maps 1, 2, 3 to `fd.uv1`, `fd.uv2`, `fd.uv3`, and 4 to `fd.uvMat`,
   the view-dependent matcap coordinate
   [`lil_common_frag.hlsl:742-746`]. The property is an unsigned
   integer enum with default 0 [`lts.shader:59`].
3. Texture fetch: the macro `LIL_GET_SUBTEX`
   [`lil_common_macro.hlsl:2341`] calls `lilGetSubTex` with the layer
   scale and offset `_Main2ndTex_ST`, the layer scroll and rotate
   `_Main2ndTex_ScrollRotate`, the layer angle `_Main2ndTexAngle`, six
   decal flags, the MSDF flag `_Main2ndTexIsMSDF`, and the sampler
   `sampler_Main2ndTex` of that layer.
4. Coordinate math in `lilGetSubTex`
   [`lil_common_functions.hlsl:719-757`]: the non-decal path applies
   `lilCalcUV(uv, ST, SR2)` where `SR2 = (SR.xy, angle, SR.w)`, so the
   coordinate is scale and offset, then a rotation by angle plus a
   time term, plus a time scroll term [`lil_common_functions.hlsl:455-458`].
   The decal path applies `lilCalcDecalUV` with the copy, flip, and
   hide flags [`lil_common_functions.hlsl:473-506`] and adds a decal
   border alpha write `outCol.a *= lilIsIn0to1(uv2, ...)` when the
   decal flag is on [`lil_common_functions.hlsl:748`].
5. MSDF: when `_Main2ndTexIsMSDF` is on, the alpha becomes a function
   of the sampled RGB [`lil_common_functions.hlsl:747,754`].
6. Blend mask: `color2nd.a *=` one sample of `_Main2ndBlendMask`
   through `sampler_MainTex` at `fd.uvMain`, red channel, with no
   scale and offset [`lil_common_frag.hlsl:750-752`]. The property
   carries no tiling UI and defaults to white, so an unassigned mask
   multiplies by exactly one [`lts.shader:70`]. `fd.uvMain` is the
   double-side UV of `fd.uv0` followed by the main scale, offset, and
   scroll [`lil_common_frag.hlsl:258-267`]. The frontends already gate
   the backface shift and the main scroll, and require main identity
   scale and offset, so under those gates `fd.uvMain` equals `fd.uv0`
   exactly. [MEASURED, plus the existing gate facts]
7. Layer dissolve: `lilCalcDissolve` and `lilCalcDissolveWithNoise`
   multiply the alpha by a zero-or-one mask value when the rounded
   mode component of `_Main2ndDissolveParams` is nonzero
   [`lil_common_functions.hlsl:626-715`]. Mode zero writes nothing.
   The rounding is HLSL `round`, round half to even
   [`lil_common_functions.hlsl:638,684`].
8. Audio link: `if(_AudioLink2Main2nd)` multiplies the alpha by the
   audio link value [`lil_common_frag.hlsl:793-795`]. The toggle
   defaults off [`lts.shader:445`].
9. Distance fade: one lerp on the alpha driven by render distance
   [`lil_common_frag.hlsl:796`]. The strength component is
   `_Main2ndDistanceFade.z` [`lts.shader:81` shows the default
   `(0.1, 0.01, 0, 0)`]. This is depth dependent, so any nonzero
   strength must refuse.
10. Per-layer cull: `_Main2ndTex_Cull` 1 or 2 zeroes the alpha on one
    facing [`lil_common_frag.hlsl:797`]. This is facing dependent, so
    any nonzero value must refuse.
11. Alpha writers: mode 1 replaces, mode 2 multiplies, mode 3 adds
    with saturate, mode 4 subtracts with saturate
    [`lil_common_frag.hlsl:798-807`]. The RGB blend that follows uses
    only color [`lil_common_frag.hlsl:808`]. A mode above 4 fires no
    writer, so it is alpha neutral like mode 0.

## 4. The shipped shader compiles every layer path

The pass shader defines the whole feature set in its shared HLSL
block: `LIL_FEATURE_MAIN2ND`, `LIL_FEATURE_MAIN3RD`,
`LIL_FEATURE_LAYER_DISSOLVE`, `LIL_FEATURE_DECAL`,
`LIL_FEATURE_ANIMATE_DECAL`, `LIL_FEATURE_ALPHAMASK`,
`LIL_FEATURE_DISTANCE_FADE`, `LIL_FEATURE_AUDIOLINK`, and the per-texture
flags `LIL_FEATURE_Main2ndTex`, `LIL_FEATURE_Main2ndBlendMask`,
`LIL_FEATURE_Main2ndDissolveMask`, `LIL_FEATURE_Main2ndDissolveNoiseMask`,
and the third set
[`ltspass_transparent.shader:677-730`]. The keyword replacement table
maps Unity keyword names onto these flags for generated variants
[`lil_replace_keywords.hlsl:82-227`]. The shipped shaders carry no such
pragmas, so the keywords never vary and every compiled path is live.

Consequence: for the attested shipped family, the runtime material
properties alone decide layer behavior. No keyword capture is needed.
A generated variant with stripped features fails the attestation
digest and answers all unknown, as today. [MEASURED]

## 5. Stage A admission shape derived from the pins

A layer is alpha provable when every fact below holds. Each refusal
names the property.

| Fact | Gate | Reason |
|---|---|---|
| UV mode | `_MainNthTex_UVMode == 0` exact | modes 1 to 3 are stage B, mode 4 is view dependent |
| Angle | `_MainNthTexAngle == 0` exact | nonzero rotates the coordinate |
| Scroll rotate | vector exactly `(0, 0, 0, 0)` | any nonzero component is time dependent |
| Scale offset | identity | mirrors the main arm boundary and makes the decal round trip exact |
| Decal flags | all six zero | decal mode changes the coordinate and adds a border alpha write |
| MSDF flag | zero | MSDF replaces alpha with an RGB function |
| Tint alpha | `_ColorNth.a` finite | it multiplies the chain |
| Blend mask | unassigned, or red channel evidence at UV zero | assigned masks multiply the chain |
| Layer dissolve | rounded mode component is zero | nonzero modes multiply by a masked threshold |
| Audio link | `_AudioLink2MainNth == 0` exact | on toggles multiply by a runtime value |
| Distance fade | `_MainNthDistanceFade.z == 0` exact | nonzero strength is depth dependent |
| Layer cull | `_MainNthTex_Cull == 0` exact | nonzero values zero alpha per facing |
| Alpha mode | finite integer, any value | 1 to 4 compose, other values are alpha neutral |

The texture request per layer asks scale and offset, source identity,
sampling, and alpha channel evidence. The blend mask request asks
source identity and red channel. It rides the main sampler facts, like
the existing alpha mask. The sampler identity is measured in section 3.
The request shape is inferred from those sampling sites.

The rounding note: the dissolve gate compares the HLSL `round` of the
mode component. A proof that compares the raw value instead would
refuse a material the shader leaves off, and a proof that admits a
raw nonzero mode that rounds to zero would be wrong. The round is
half to even. [MEASURED, `lil_common_functions.hlsl:638`]

## 6. Consequences for the design record

1. The design predicted extra alpha writers beyond the four modes.
   The pins confirm five more proof relevant mechanisms per layer: the
   blend mask, the layer dissolve, the audio link toggle, the distance
   fade strength, and the per-layer cull. All become named gates.
2. The off-state reduction holds.
3. The four alpha mode writers sit inside the toggle gate, so the
   toggle stays in the evidence request and the mode stays readable.
4. The alpha mask runs after the layers. The shared mask term keeps
   its place on top. Its modes 3 and 4 stay refused as today. Widening
   them is out of scope for stage A.
5. The mode-4 subtraction makes the exactness of every multiplier
   load bearing. An admitted but wrongly computed mask factor can raise
   the composed alpha under subtraction. Every gate in the table above
   is therefore exact, not approximate.

## 7. Implementation record, 2026-09-15

Stage A landed on the layer alpha support branch on this date. The
product suite ran green after the full implementation: 2,249 passed,
0 failed, observed. The run includes every falsifier below.

1. The capture admits no-alpha-channel formats, and the importer
   theorem proves their sampled alpha exactly one without a texel.
2. The layer term admits the second and third main texture layers at
   UV0 with identity scroll, rotate, angle, and scale and offset, with
   every decal flag and the MSDF flag exactly off, and with the audio
   link, distance fade, cull, and dissolve gates exactly off.
3. All four alpha mode writers compose exactly. A multiply mode that
   would ride a saturating shape refuses with a named diagnostic,
   because the exact-one predicate of a product does not survive that
   association. Add modes compose through the disjunction combinator.
4. The no-alpha report names the renderer, the slot, and the texture
   property, and never changes a classification outcome.

Pending for this branch: the product owner's Lab re-characterization at
the branch tip, then the stage B design addendum for UV modes one to
three and per-layer scroll. The zero-triangle diagnosis branch of this
date closed the observation that used to gate the checkpoint.

## 8. Stage A checkpoint observed, 2026-09-15

The product owner rebuilt the private swap avatar in the Census Lab
editor against the stage A branch tip and observed the split. The
authoritative NDMF console lines name the outcome in counts: 37
renderers analyzed, 60155 triangles moved to opaque materials, 96
renderers kept everything original.

The second main texture layer of the skirt renderer is admitted: the
build records no refusal naming that layer toggle, and no
admitted-material semantics refusal anywhere in the final build. Other
transparent renderers of the same avatar keep their refusals where their
own alpha proves nothing; those are content facts, not gate defects.

Checkpoint discipline satisfied: suites green before the Lab run, the
Lab run observed after, and the stage B gate is released.
