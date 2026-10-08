# lilToon gem and exact opaque mimicry: can any configuration imitate an opaque material?

Date: 2026-10-07. Branch: `feat/census-refusal-coverage`. Base for the read:
`912e9d1` plus uncommitted-free tree. Method: three parallel read-only
investigations, in-repo pins, and the vendor source at a pinned tag.

Privacy note: this record is sanitized. It names no private avatar, renderer,
material, texture, scene, or hierarchy names. The avatar in prior records is
"the avatar under test". lilToon shader names are public vendor identifiers
and stay exact. Vendor file citations name the vendor repository and tag, not
a machine.

Labels: `[SOURCE]` is a fact read at cited lines. `[INFERENCE]` is a
conclusion drawn from cited facts.

## 1. The question and the verdict

The user's gate question: can a gem material, under any configuration,
mimic the appearance of an opaque material exactly?

**Verdict: no. For any non-black appearance, exact opaque mimicry is
impossible in principle. One degenerate configuration exists, and it renders
pure black.** `[SOURCE: vendor math, section 3]`

The three facts that force this verdict, none reachable by property values:

1. Gem output multiplies the scene background sample into its color. The
   refraction block is unconditional. No toggle exists.
2. Gem output carries a per-pixel view factor `abs(dot(N,V))` and a
   hard-coded `0.75` darkening. No property removes either.
3. Gem output never receives scene light, shadow, or attenuation. An opaque
   output multiplies by all of them.

A material configuration cannot cancel a scene variable or restore dropped
lighting. So no gem configuration equals an opaque render. `[INFERENCE]`

Consequence for AMUSE: gem triangles can never be proven movable to a
canonical opaque material under the product contract. The gem refusal is a
necessary refusal, not a coverage defect. `[INFERENCE]`

## 2. What this repository already pinned about gem

`[SOURCE]` for all of this section.

- The Multi mode gate keeps a gem name-family rule in code:
  `ProducesOutlineTone` returns false when the shader name contains
  `Refraction`, `Fur`, or `Gem` after the last name separator
  (`LilToonMultiModeGate.cs:538-549`, the `Gem` test at `:546`).
- The vendor keyword derivation gives the gem branch forced-off rows for
  alpha mask, distance fade, outline tone, and the UV2 requirement. The gate
  transcribes these with vendor line citations
  (`LilToonMultiModeGate.cs:219-236, 493-531`).
- A Multi material in gem mode (vendor mode 6) refuses at the admitted-mode
  check before any keyword runs (`LilToonMultiModeGate.cs:67-70, 303-307`).
  The specialized Multi gem container refuses by container identity alone
  (`LilToonMultiResolution.cs:39-47`).
- The regular gem family was characterized against the vendor source on
  2026-08-30: declared name `Hidden/lilToonGem`, `LIL_RENDER 2`, real
  `GrabPass`, a `FORWARD_PRE` pass, blend `One One` over `Zero One`,
  `ZWrite = 0`, `Cull = 0`, queue `Transparent-100`, tag `RenderType=Opaque`
  (`2026-08-30-liltoon-family-applicability.md:86, 161-196`).
- The audit on 2026-10-06 recorded gem materials refusing at slot scope with
  `AdmittedMaterialSemanticsUnknown`, and the 2026-10-07 record documented
  the second refusal path: a swap-only unattested material fails the closed
  capture batch and refuses renderer-wide (way 4).

## 3. Vendor math at the pinned tag

Pinned source: `lilxyzw/lilToon`, tag `2.3.4`. The tag matches the version in
`Packages/jp.lilxyzw.liltoon/package.json`. Fragment body:
`Shader/Includes/lil_pass_forward_gem.hlsl`. `[SOURCE]` throughout.

### 3.1 Output dependency classes

- Own textures and properties: main texture and `_Color`, optional normal,
  anisotropy, AudioLink, matcap, rim, glitter, emission blocks, and the gem
  block (`_RefractionStrength`, `_RefractionFresnelPower`,
  `_GemChromaticAberration`, `_GemEnvContrast`, `_GemEnvColor`,
  `_GemParticleColor`, `_GemVRParallaxStrength`, `_Reflectance`,
  `_Smoothness`, `_ReflectionCube*`) (`lil_pass_forward_gem.hlsl:142-302`).
- Background samples: three unconditional samples per pixel, one per RGB
  channel, of the scene color store (`lil_pass_forward_gem.hlsl:218-220`).
  Built-in RP backs this with a real `GrabPass` (`lts_gem.shader:743`).
- Camera and view vectors: view vector, head-blended stereo vector, view
  matrix projection, Fresnel `abs(dot(N,V))`, screen UV
  (`lil_pass_forward_gem.hlsl:24-26, 139, 191, 215`).
- Lighting: scene light arrives per vertex, but gem never multiplies its base
  color by light color, shadow, or attenuation. Shadow transfer and
  attenuation are compiled out. Toon shading and backlight are excluded
  (`lil_common_macro.hlsl:1945-1948`, `lil_common_frag.hlsl:916, 1232`).
- Environment probes: scene probes or a custom cube, sampled three times
  with chromatic offsets (`lil_pass_forward_gem.hlsl:236-242`).
- Alpha: output alpha is `mainTex.a * _Color.a`. The gem pass never clips,
  dithers, or premultiplies (`lil_common_frag.hlsl:353-358`).
- Fog: gem fades to black, not to the scene fog color
  (`lil_pass_forward_gem.hlsl:305-307`).
- Pre pass: `FORWARD_PRE` writes constant zero with `Blend One Zero` and
  erases the gem footprint before the grab and the main pass
  (`lil_pass_forward_gem.hlsl:77-93`).
- Blend and depth: additive color blend, two-sided, no depth write, queue in
  the transparent range (`Editor/lilMaterialUtils.cs:250-272`).

### 3.2 Why no property kills the background dependence

- The refraction block has no `_UseGem` or `_UseRefraction` toggle
  (`lil_pass_forward_gem.hlsl:214-223`).
- `_RefractionStrength = 0` and `_GemChromaticAberration = 0` only zero the
  sample offsets. The three background samples still run at the unshifted
  screen UV. Output keeps the `* background` factor.
- `_GemEnvContrast` only reshapes the samples. At 1 the term is
  `saturate(background)`. Large values drive it toward 0 and the output to
  black. No value makes it identically 1 for an arbitrary background.
- The normal refraction shader has an alpha escape
  (`lerp(refractCol, base.rgb, alpha)`). The gem path has no equivalent.

### 3.3 Alpha independence, and why it does not help

Gem RGB is provably independent of alpha under every configuration: the only
alpha-to-RGB path is fog, and the gem fog color is zero
(`lil_pass_forward_gem.hlsl:305`). This fact does not create support. The
optimization does not ask "is RGB alpha-independent". It asks whether the
triangle can render on the canonical opaque material with the same visible
result. It cannot, for the reasons in section 1. `[INFERENCE]`

### 3.4 The degenerate exact conjunction

One configuration matches an opaque render exactly. It requires main texture
RGB and `_Color.rgb` at black, `_GemEnvColor.rgb` at zero, all optional
effect blocks off, single-sided culling or neutralized sparkle, and a scene
with no active fog. Gem output is then `(0,0,0)` for every view and scene.
An opaque material with the same black inputs also outputs `(0,0,0)`. This
is the single exact conjunction, and it has no practical value: it proves
only that two black renders match. `[SOURCE: vendor math; INFERENCE on
usefulness]`

### 3.5 Confidence limits

- All claims pin to tag `2.3.4`. Gem is a recent vendor feature. A future
  version could add a toggle around the refraction block. A re-read at the
  new tag would be cheap and required before any pin work.
- Optional blocks alias into the gem fragment when their toggles are on.
  They only add more scene-dependent terms. The verdict is stable against
  them.
- Per-pipeline differences exist: Built-in RP grabs the background after the
  pre pass blackens the footprint. URP and HDRP sample a pre-transparent
  capture. None of the differences restore an escape from the background
  multiply.
- Gem writes texture alpha where an opaque draw writes 1. Consumers of
  destination alpha would diverge even in the black case.
- The gem queue sits in the transparent range. Draw order interactions with
  other transparent draws have no opaque counterpart.

## 4. What support would have needed, and why the question is settled

The established per-family support shape is: one pinned identity row, one
exact-name map entry, one pinned conjunction check, one alpha answer, one
conversion arm that writes nothing for the already-opaque case. The
admission-shape slice verified each part in the tree and enumerated the gem
version: a no-pass identity row like the Multi containers, a `Classify`
branch, a verify case, and an ordered gate walk like the Poiyomi preset.

That machinery is now moot for conversion. The blocker is not admission
machinery. The vendor math has no configuration whose visible image a
canonical opaque material can reproduce. A pinned conjunction cannot pin a
conjunction that does not exist. `[INFERENCE]`

## 5. What remains worth doing

1. Nothing in this investigation justifies a gem frontend or any conversion
   path for gem materials. The 2026-10-07 coverage record already ranked
   family admission last on this avatar. Gem drops out of that ranking.
2. The refusal-scope work keeps its value, unchanged:
   - way-4 capturer ordinal scoping, so a swap-only unattested material
     refuses per slot instead of renderer-wide;
   - the consent pre-scan gap for swap-only materials, a separate decision.
3. If a future vendor tag adds a refraction kill switch, re-read section 3
   at that tag. The rest of the verdict rests on the view factor, the
   `0.75` constant, and the dropped lighting, which a kill switch would not
   remove. Even then, exact mimicry would need those three to vanish too.
   `[INFERENCE]`

## 6. Limits

- Read-only work. No Unity instance was touched. No measurement ran.
- Vendor claims come from the pinned tag read this session, not from a
  re-derivation of digests. No digest was computed or recorded.
- The admission-shape enumeration assumed slice B would admit a
  configuration. Its per-part verification of the support shape in the tree
  stands on its own and stays true.
