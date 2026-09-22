# The poiyomi decal slots versus the liltoon main layers - characterization

## Privacy note

This record opens with a privacy statement. It describes private Census
Lab fixtures by role only. It names no avatar, renderer, material,
texture, mesh, or hierarchy. It records no machine paths, no host
names, no ports, and no instance names or hashes. The designated
locked denim garment fixture appears by role, and its decal facts
appear as property values. Every status claim carries its date.
Property names, enum values, and vendor line facts are public facts of
the pinned shader sources.

## 1. Scope and method

Date: 2026-09-22. Branch: `feat/poiyomi-decal-slots`, cut from `main`
at the merge of the parity pull request. This session changes no
production code. It produces this record only.

The question: the poiyomi decal slots should be roughly equivalent to
the liltoon main color second and third layers. Determine whether that
is the case, and what the differences are, with the alpha path first.

Sources, read 2026-09-22:

- The pinned Poiyomi Toon Shader 9.3.64 source, the same source the
  frontend attests. The main shader file carries the decal block. The
  Two Pass source carries the same decal block, so the family shares
  the mechanism.
- The pinned lilToon 2.3.4 source. The layer logic sits in the shared
  fragment include, the normal forward pass include, the functions
  include, and the macro include.

Both sources were read from the installed copies in the Census Lab
project, read-only. One probe touched the designated locked fixture
through the sanctioned clone route: copy, unlock with the vendor
method the production unlock window calls, read the decal flags, and
delete the clone in the same handler. The fixture stayed locked and
untouched. A sweep confirmed the deletion.

## 2. The poiyomi decal mechanism, observed in the pinned 9.3.64 source

### 2.1 Slots and compile gating

There are four decal slots. Slot 0 uses unsuffixed properties, slots 1
to 3 use numbered properties. Each slot toggle is a float plus a
borrowed builtin keyword. The toggles sit at source lines 255, 348,
441, and 534, and the keyword pairs are `GEOM_TYPE_BRANCH`,
`GEOM_TYPE_BRANCH_DETAIL`, `GEOM_TYPE_FROND`, and
`DEPTH_OF_FIELD_COC_VIEW`. A slot's fragment block compiles only when
its keyword is set. On locked materials the vendor lock tool also
strips the block when the toggle float is zero at lock time. A float
of one with no keyword, or a keyword with a float of zero, is
inconsistent state. The frontend reads floats, so the floats are the
evidence, and the keyword duality is a design question for admission.

### 2.2 Where decals run in the main pass

The main fragment orders the alpha chain this way:

1. Chain alpha starts at line 29780 as main texture alpha times color
   alpha.
2. The alpha mask block composes at lines 29864 to 29876.
3. The alpha options block runs at line 29888. It carries alpha mod,
   the alpha global mask read, fresnel and angular terms, the audio
   link add, distance fade, dithering, and sharpened alpha to
   coverage.
4. The four decal slots run at line 29976 through the decal apply
   function, slot 0 first.
5. The vendor premultiply runs at line 30216.
6. The coverage and clip stages follow.

So a decal composes onto the chain alpha after the alpha options and
before the premultiply and coverage decisions.

### 2.3 The alpha writer inside a slot

The per-slot apply function starts at line 22906. It writes the chain
alpha only inside the override-alpha block at lines 22958 to 22978.
The block runs only when the slot's `OverrideAlpha` float is nonzero.
The six modes are:

1. Replace. `alpha = lerp(alpha, finalAlpha, weight)`.
2. Multiply. `alpha = lerp(alpha, saturate(alpha * finalAlpha), weight)`.
3. Add. `alpha = lerp(alpha, saturate(alpha + finalAlpha), weight)`.
4. Subtract. `alpha = lerp(alpha, saturate(alpha - finalAlpha), weight)`.
5. Min. `alpha = lerp(alpha, min(alpha, finalAlpha), weight)`.
6. Max. `alpha = lerp(alpha, max(alpha, finalAlpha), weight)`.

`finalAlpha` is the mixed decal alpha from line 22951. The weight is
the decal mask channel for the two masked modes and one for the
others. The two bounds modes also skip the write outside the decal
square when the slot is not tiled. The mixed alpha carries the slot's
alpha intensity from line 22981.

The `alphaOverride` parameter that the apply function receives is
never written in the pinned source. The writes target the fragment
alpha directly.

### 2.4 The mixed alpha's factor chain

The decal color alpha is the texture alpha times the theme color
alpha at line 22851, then the decal mask channel times the tiling
clip at line 22853. The apply function blends the global mask read
into it, zeroes it by mirrored-uv hide modes, and zeroes it by the
face mask. Line 22951 then multiplies by `saturate(BlendAlpha + audio
link add)`, and line 22981 multiplies by the alpha intensity.

### 2.5 The non-alpha effects

Every enabled slot also composes three non-alpha effects. The base
color lerp at line 22988 uses the slot's color blend mode. The
emission add at line 22989 uses the decal color and the emission
strength. The global mask write at lines 22983 to 22986 puts the
mixed alpha into a global mask register that later modules read. The
only later alpha reader of a global mask in the alpha path is the
dissolve, and the dissolve already sits in the alpha coverage gate
list. Under the current gates, the write cannot reach the alpha
output.

### 2.6 The decal UV

The decal UV function sits at lines 22699 to 22728. It reads one of
the mesh UV channels by an enum, applies symmetry and mirrored-uv
flip and hide, adds a view-dependent offset from the slot depth
setting, rotates around the slot position with an optional time speed,
and remaps by scale, position, and side offset. The depth offset is
not gated by the parallax module. Any nonzero slot depth makes the
decal coordinate view-dependent. The tiling clip multiplies the decal
alpha by an in-bounds factor when the slot is not tiled.

### 2.7 The alpha-inert configuration

When a slot's `OverrideAlpha` float is zero, the slot writes no alpha.
The three non-alpha effects still run. The slot is then an identity on
the chain alpha, whatever its texture, color, blend alpha, intensity,
video, hue, channel separation, or mask settings are. This is the
configuration the closed alpha gate currently refuses without
inspecting.

### 2.8 The separate audio link decal module

The audio link decal module is a separate feature from the four
slots. Its apply function sits at line 24733. Its final write at line
24833 is `alpha = lerp(alpha, alpha * audioLinkValue, ControlsAlpha)`.
When audio link is unavailable, the value is forced to zero at lines
24810 to 24815, so a nonzero controls-alpha becomes a constant scale
of the chain alpha. When audio link is available, the value is
runtime-dependent. The call site at lines 29998 to 30003 guards on
compile keywords and on the lock-time strip directive. It has no
runtime gate on the enable float.

## 3. The liltoon main layer mechanism, observed in the pinned 2.3.4 source

### 3.1 Sampling and decal UV

The second layer sampler sits in the shared fragment include from line
740, the third from line 843. Each layer reads UV0 to UV3 or the
matcap UV by a mode float. The subtex macro in the macro include at
lines 2333 to 2341 routes through the subtex functions. The decal UV
function at lines 473 to 506 applies copy, scale and offset, flip
copy, flip mirror, left and right hide, and a rotation around the
scale pivot, with optional time scroll and spin. In decal mode the
subtex functions multiply the sampled alpha by an in-bounds factor at
lines 748 and 787. The layer also carries an MSDF mode.

### 3.2 The alpha writers

The layer alpha modes sit at lines 799 to 805 for the second layer and
894 to 901 for the third, each under a non-opaque render gate:

1. Replace. `col.a = layerAlpha`.
2. Multiply. `col.a = col.a * layerAlpha`.
3. Add. `col.a = saturate(col.a + layerAlpha)`.
4. Subtract. `col.a = saturate(col.a - layerAlpha)`.

Mode zero leaves the chain alpha untouched. After a nonzero mode
composes, the layer's own alpha is set to one so the color blend runs
at full strength. The layer alpha itself is the texture alpha times
the color alpha, times the blend mask red, times the audio link value,
through the distance fade lerp, through the facing cull, and through
the layer dissolve.

### 3.3 The color stage

The lighting-stage color blends at lines 464 to 466 and the add-pass
blends at lines 477 to 479 compose the layer color by its blend mode.
They are rgb-only and never reach the chain alpha.

### 3.4 The production model

`LilToonLayerAlphaTerm` already models this system. Its stage A
envelope admits a layer at UV0 with exact identity transforms, every
decal flag and the MSDF flag off, the audio link toggle off, the
distance fade strength zero, the per-layer cull zero, and the layer
dissolve mode zero. It treats mode zero as an inert term and modes one
to four as exact writers.

## 4. Equivalence verdict

The hypothesis is correct for the alpha core and wrong as a blanket
claim.

What maps one to one:

- The alpha-inert configuration. A liltoon layer at mode zero and a
  poiyomi slot at `OverrideAlpha` zero are both silent on the chain
  alpha.
- The four writer modes. Replace, multiply, saturating add, and
  saturating subtract exist on both sides with the same formulas and
  the same names.
- Both compose onto the chain alpha before the coverage and clip
  stages, so the same proof domain applies.

What does not map:

- Poiyomi adds min and max modes with no liltoon counterpart.
- Poiyomi weights the write by a mask channel and gates it by bounds
  modes. Liltoon writes unweighted.
- Poiyomi reads a global mask into the value and writes a global mask
  out. Liltoon does neither.
- Poiyomi adds emission from the slot. Liltoon does not.
- Poiyomi's depth setting makes the slot coordinate view-dependent
  without any module gate. Liltoon's decal mode has no view
  dependence.
- Poiyomi has four slots, video sources, channel separation, and a
  tiling toggle. Liltoon has two layers.
- Liltoon composes the layer alpha before its own color blend and
  then forces the layer strength to one. Poiyomi composes the override
  alpha and blends color by an independent mixed alpha.
- Liltoon-only factors: the decal bounds factor on sampling, the layer
  dissolve, the distance fade, the facing cull, the blend mask, and
  MSDF.
- The color blend mode sets differ between the vendors. Mapping them
  is design work for the color outputs, out of scope here.

## 5. Consequence for the frontend

The current state: the decal toggles sit in the base color, alpha, and
normal feature gate arrays of the poiyomi frontend, at lines 163 to
166, 244 to 247, and 326 to 329. Any enabled decal refuses those
outputs by name. The alpha refusal surfaces first on the alpha output
because the parity slices cleared the earlier refusals.

The smallest honest slice, following the user's direction: admit an
enabled decal slot as an alpha identity when its `OverrideAlpha` float
is proven zero. The alpha output then proceeds. The base color, normal,
and emission outputs keep their own refusals, so a material with an
enabled decal still refuses as a renderer until its other outputs
resolve. The designated fixture illustrates this: its observed decal
flags on 2026-09-22 were slot 0 enabled with `OverrideAlpha` zero,
slots 1 to 3 disabled, the audio link decal module off, and the detail
feature on. The inert-slot slice clears the alpha refusal, and the
detail feature still blocks the renderer through its own gates.

A writer slice after that: modes one to four with the mask weight and
the bounds modes. This needs the mixed alpha's whole factor chain
proven, which means a texture readback envelope, a mask readback
envelope, and a global-mask read envelope. It maps onto the existing
liltoon layer algebra, so the shape exists.

Modes five and six should refuse by default. Max can lift a sub-unit
alpha, so it cannot sit inside a claimed exactly-one alpha. Min
preserves the lower bound but changes any value above the mixed alpha,
so only a proven constant mixed alpha of one or a proven sub-unit
domain could admit it, which the design phase should treat as an open
question.

Any decal slice must also pin the audio link decal module, because
that module writes alpha independently of the four slots.

## 6. Related finding, recorded not expanded

The audio link decal module is a pre-existing coverage gap of the
current gates, independent of this branch's decal work. Its alpha
write at vendor line 24833 runs when the module keyword is set, even
when the audio link module toggle is off, because the alpha feature
gate list names the audio link module toggle but not the decal
module's own enable or controls-alpha floats. With audio link
unavailable the write is a constant scale of the chain alpha, which a
constant-aware proof could in principle carry but no gate currently
inspects. This needs its own owner decision, note row, and RED and
GREEN obligations. It is recorded here so it is not lost.

## 7. Open questions

1. Keyword duality. Should admission require the toggle float and the
   slot keyword to agree? The frontend reads floats today.
2. Two Pass family coverage. The decal block is present in the Two
   Pass source. The envelope must declare the family it admits.
3. Min and max modes. Whether any provable sub-domain exists.
4. The detail feature gate. The fixture needs a detail envelope
   before any renderer admission, separate from this work.
5. The color blend mode sets. Needed only when the decal color
   outputs get their own slice.

## 8. Validation of this session

No production code changed and no repository test changed. The
evidence is the pinned source reads cited by line and one fixture
probe. The probe ran on 2026-09-22 in the Census Lab editor instance
after its identity check. Observed fixture decal flags: slot 0
enabled, `OverrideAlpha` zero for all four slots, bounds mode zero,
the audio link decal enable and controls-alpha zero, decal tiling one,
UV zero, video off, hue shift off, channel separation off, symmetry
zero, mirrored uv zero, face mask zero, no global mask read or write,
mask channel zero, and the `GEOM_TYPE_BRANCH` keyword set. The clone
was deleted in the same handler and the deletion was swept.
