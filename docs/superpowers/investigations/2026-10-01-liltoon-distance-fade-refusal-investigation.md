# The `_DistanceFade` slot refusals in the Census Lab

Privacy note: This record uses the play-mode console state of the Census Lab
editor instance and one read-only material scan of the census corpus as
evidence. The console held no AMUSE reports on 2026-10-01, so the console
section records an absence. The scan produced aggregate counts by shader
family only. This record names no avatar, renderer, material, texture, or
asset path. It writes no instance names, hashes, ports, or machine paths.

Date: 2026-10-01.
Branch: `feat/liltoon-distance-fade`.
Base: `main` at `87d7f47`.
Investigation status on 2026-10-01: no production code had changed at the end
of the investigation. The evidence came from repository source, from the
pinned vendor tag, and from one read-only scan against the Census Lab editor
instance. The scan ran only after the pinned editor confirmed the Census Lab
project identity. Both corpus folders were present at scan time.

## Question

An earlier play-mode build on the Census Lab project produced an AMUSE
refusal that names the property `_DistanceFade`. This record answers four
questions. What does the vendor feature compute? Which AMUSE gates name the
property, per supported family? Which corpus materials carry the feature?
What does support require, and what does the current refusal get right?

## Console evidence

On 2026-10-01 the console of the pinned Census Lab editor instance held no
AMUSE refusal reports. A filter for `DistanceFade` returned zero entries. A
filter for `Distance fade` returned zero entries. The console held only
unrelated VRChat audio plugin load errors. The refusal observation predates
this editor session and this record treats it as ground truth.

The report path is reconstructable from source. The transparent family
records an Alpha diagnostic with the `UnsupportedFeature` code and the
property name. See
`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs:442-452`.
The label map turns the property into the feature label. See
`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs:615`.
The slot report renders the label as "the Distance fade feature". See
`Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs:151-155`. The slot
refusal reason is `AdmittedMaterialSemanticsUnknown`. The 2026-09-27 alpha
mask record traced that same path.

## Census population on 2026-10-01

`[MEASURED]` One read-only scan enumerated all materials in the Census Lab
project asset database and read only material properties. It loaded 2328
materials. 1351 carry a lilToon-family shader name. The scan grouped by exact
shader name and counted the materials whose `_DistanceFade.z` is nonzero.

Five shader-name groups carry a nonzero strength:

| Shader name | Materials with nonzero `.z` | Observed `(x, y, z, w)` tuples |
|---|---|---|
| `lilToon` | 16 | `(0.1, 0.01, 0.5, 1)` on 5, `(0.1, 0.01, 1, 0)` on 11 |
| `_lil/lilToonMulti` at opaque mode | 8 | `(0.1, 0.01, 0.5, 1)` on all 8 |
| `Hidden/lilToonTransparent` | 3 | `(0.1, 0.01, 0.5, 1)` on all 3 |
| `Hidden/lilToonTransparentOutline` | 1 | `(0.12, 0.02, 0.65, 0)` |
| `Hidden/lilToonTwoPassTransparent` at two-pass mode | 1 | `(0.1, 0.01, 0.5, 1)` |

This table is a per-shader-family aggregate. It is not a per-renderer or
per-slot table.

The scan also read `_DistanceFadeColor.a` for every family group that
declares the property. Every group the scan returned showed the count of
nonzero color alphas equal to the group size. The observed groups are
`Hidden/lilToonTransparent` at 94 of 94, `_lil/lilToonMulti` at 127 of 127,
`Hidden/lilToonCutout` at 36 of 36, `Hidden/lilToonCutoutOutline` at 14 of
14, and `Hidden/lilToonFur` at 3 of 3. The vendor default color alpha is 1,
and the vendor facts below explain the pattern. The scan result truncated
before it printed the remaining family groups, so this record states the
pattern only for the groups it observed.

The scan did not read `_DistanceFadeMode`. The vendor facts below show that
the mode selects the depth source and does not change any support rule.

## Vendor facts at the pinned tag

Upstream pin: lilToon tag `2.3.4`, commit
`252fd8cfc46106d4967e95b3f2c788418502f227`. The attestation comment states
the same pin. See
`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:531`.
Every vendor cite is file:line relative to `Assets/lilToon/` at that tag.
The distance fade block at `Shader/Includes/lil_common_frag.hlsl:2017-2064`
is byte-identical between tags `2.3.4` and `1.10.3`. The property block is
identical across tags `1.4.1` through `1.10.3`. The official manual
documents only the color fade. The alpha behavior below is source-derived.

### Property set

`[SOURCE]` `lts.shader:429-433` and `ltsmulti.shader:429-433`, identical
text:

```
[lilHDR]        _DistanceFadeColor          ("sColor", Color) = (0,0,0,1)
[lilFFFB]       _DistanceFade               ("sDistanceFadeSettings", Vector) = (0.1,0.01,0,0)
[lilEnum]       _DistanceFadeMode           ("sDistanceFadeModes", Int) = 0
[lilHDR]        _DistanceFadeRimColor       ("sColor", Color) = (0,0,0,0)
[PowerSlider(3.0)]_DistanceFadeRimFresnelPower ("sFresnelPower", Range(0.01, 50)) = 5.0
```

The vector components carry the labels Start Distance, End Distance,
Strength, and Backface Force. `_DistanceFadeParams` and `_DistanceFadeColor2`
do not exist. The per-layer pair `_Main2ndDistanceFade` and
`_Main3rdDistanceFade` is a separate feature with the same vector shape.

### Mode, strength, and compilation

`[SOURCE]` The mode popup has exactly two values: 0 is Vertex and 1 is
Position. It selects the depth source. There is no None value. The fragment
reads `float depth = _DistanceFadeMode ? fd.depthObject : fd.depth;` at
`lil_common_frag.hlsl:2021` and `:2035`. The strength component `.z` scales
every effect arm.

The regular pass assets define the feature by default:
`ltspass_opaque.shader:676`, `ltspass_cutout.shader:676`, and
`ltspass_transparent.shader:709` all carry
`#define LIL_FEATURE_DISTANCE_FADE`. The render modes are `LIL_RENDER 0` at
`ltspass_opaque.shader:639`, `1` at `ltspass_cutout.shader:639`, and `2` at
`ltspass_transparent.shader:672`. The shader setting optimizer can strip the
define, so the compiled state of a regular asset is project state. The Multi
containers instead derive the feature from the `_FADING_ON` keyword:
`Shader/Includes/lil_replace_keywords.hlsl:198-201` maps it to
`LIL_FEATURE_DISTANCE_FADE`. The vendor editor derives that keyword from the
strength: `Editor/lilMaterialUtils.cs:359` reads the vector through
`IsFeatureOnVectorZ`, which tests `.z != 0.0f` (`:506-509`), and `:416` sets
`_FADING_ON` from the result.

### Fragment math

`[SOURCE]` `Shader/Includes/lil_common_frag.hlsl:2017-2064`, verbatim shape:

```hlsl
float depth = _DistanceFadeMode ? fd.depthObject : fd.depth;
float distFade = saturate((depth - _DistanceFade.x) / (_DistanceFade.y - _DistanceFade.x));
// outline and fur passes:
distFade = distFade * _DistanceFade.z;
// other passes:
distFade = fd.facing < (_DistanceFade.w - 1.0) ? _DistanceFade.z : distFade * _DistanceFade.z;

float3 fadeColor = _DistanceFadeColor.rgb;
// rim sub-arm, only with world normals present:
fadeColor = lerp(fadeColor, _DistanceFadeRimColor.rgb * fd.col.rgb,
                 fadeRim * _DistanceFadeRimColor.a);

#if defined(LIL_PASS_FORWARDADD)
    fd.col.rgb = lerp(fd.col.rgb, 0.0, distFade);
#elif LIL_RENDER == 2
    fd.col.rgb = lerp(fd.col.rgb, fadeColor * _DistanceFadeColor.a, distFade);
    fd.col.a   = lerp(fd.col.a, fd.col.a * _DistanceFadeColor.a, distFade);
#else
    fd.col.rgb = lerp(fd.col.rgb, fadeColor, distFade);
#endif
```

Four facts drive the support analysis.

1. Every arm scales its lerp weight by the strength `.z`. With `.z == 0` the
   weight is exactly 0 in every arm, including the backface forced arm and
   the ForwardAdd arm. The whole feature is then an exact no-op for any
   values of the other properties.
2. The transparent alpha arm lerps toward `fd.col.a * _DistanceFadeColor.a`.
   With the default color alpha 1 the target equals the source and the write
   is an exact identity at every distance. A color alpha below 1 bounds the
   runtime alpha at `base * _DistanceFadeColor.a`.
3. The color arm runs at every render mode: opaque and cutout share the
   `#else` arm, transparent uses its own arm, ForwardAdd drives the add
   contribution to black. The mode changes only whether alpha is written.
4. A second function `lilDistanceFadeAlphaOnly` fades cutout alpha, and it
   is reachable only through the Dither macro. See
   `lil_common_frag.hlsl:531-545` at the pin. The forward-pass call sits
   inside `if(_UseDither == 1)` and runs before the cutout coverage
   transform, which the 2026-09-30 Multi note places at
   `lil_pass_forward_normal.hlsl:402-403` with the dither hook before it. The
   shadow caster call additionally requires a perspective shadow projection.
   So cutout coverage and cutout shadows respond to distance only when the
   material also uses dither.

### Per-layer fades

`[SOURCE]` The second layer attenuates its own alpha at
`lil_common_frag.hlsl:802` and the third at `:898`. The lerp is not guarded
by render mode. The `LIL_RENDER != 0` guards at `:804` and `:900` wrap only
the alpha write-back. The attenuated layer alpha still scales the layer
color blend at opaque mode. This confirms that the existing layer gates,
which admit only zero strength, stay required for cutout and transparent.

## Code trace

### Transparent family

All six attested transparent shader names route into one family. See
`Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:744-785`.
They share one evidence request. The request captures `_DistanceFade` as a
vector and does not capture `_DistanceFadeColor`. See
`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs:188-196`
and
`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentSourceEligibilityTests.cs:121-124`.

Two gates refuse on the strength today.

- The semantics gate refuses a non-finite vector or a nonzero strength. See
  `LilToonTransparentMaterialSemantics.cs:442-452`. The refusal produces the
  slot report described above. Two tests pin it:
  `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs:1838-1861`.
- The conversion eligibility gate refuses with
  `UnsupportedDistanceFade`. See
  `LilToonTransparentSourceEligibility.cs:416-424`, with the presence check
  at `:221-224` and the finiteness sweep at `:232-243`. One test pins it:
  `LilToonTransparentSourceEligibilityTests.cs:395-414`.

### Multi containers

The mode gate transcribes the vendor keyword derivation. The `_FADING_ON`
row carries the producer `ProducesDistanceFade`, which tests the vector
strength. See
`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiModeGate.cs:229`
and `:519-525`. The mirrored transparent eligibility rows require the vector,
sweep it for finiteness, and refuse the nonzero strength. See
`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiSourceEligibility.cs:281-330`.
One test pins the refusal:
`Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonMultiSourceEligibilityTests.cs:521-537`.

The eight census Multi materials with a nonzero strength all sit at opaque
effective mode. An opaque-mode Multi material is already opaque, donates no
triangles, and reaches no distance fade gate. No census Multi material
refuses on the feature today.

### Cutout family

`[FINDING]` The cutout evidence request captures the two per-layer fade
vectors but not `_DistanceFade`. See
`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs:159-164`.
No cutout gate names `_DistanceFade` anywhere in the family.

At `LIL_RENDER 1` the vendor forward pass runs the color lerp for every
material with a nonzero strength, and it runs the alpha fade for materials
that also use dither. A converting cutout material therefore loses the color
fade on moved triangles today, and loses the distance-dependent coverage too
when dither is on. AMUSE moves proven-opaque cutout triangles to the
canonical opaque target. The family neither captures nor gates the feature,
so the conversion proceeds. This is a preservation gap. The census cutout
groups show zero nonzero strengths on 2026-10-01, so the gap is latent, not
live.

### Opaque family

The regular opaque material is target-only. It is never a separation source.
The sixteen census opaque materials with a nonzero strength therefore never
interact with a conversion. The generated canonical target is an attested
opaque material. Whether it carries vendor-default fade values is unverified
on 2026-10-01 and belongs to the support design.

## Build identity

On 2026-10-01 the embedded package copy in the Census Lab project matched
repository HEAD byte for byte on seven load-bearing files:
`LilToonTransparentMaterialSemantics.cs`,
`LilToonTransparentSourceEligibility.cs`, `LilToonMultiModeGate.cs`,
`LilToonMultiSourceEligibility.cs`, `LilToonCutoutMaterialSemantics.cs`,
`LilToonMaterialSemantics.cs`, and `AmuseReports.cs`. The comparison used
SHA-256 over file bytes.

## Finding

1. The triggering state is a nonzero strength component. Five census
   transparent-family materials carry it. Three use
   `Hidden/lilToonTransparent`, one uses
   `Hidden/lilToonTransparentOutline`, and one uses
   `Hidden/lilToonTwoPassTransparent`. All three shader names are attested
   AMUSE families.
2. The refusal is conservative and the visual outcome is already correct. A
   material with a nonzero strength donates no triangles today. The slot
   stays on the original material with the vendor fade intact.
3. The refusal mislabels a legitimate authored state as an unsupported
   feature. The vendor manual documents the color fade as a normal feature.
   The report should describe retention, not missing support.
4. The alpha classification loss is real but small. With the observed color
   alpha 1 the transparent alpha write is an exact identity at every
   distance, so the alpha question would be provable. The color arm still
   forbids the move, so the classification result would be unused.
5. There is no un-gated arm for transparent materials. Every lerp weight
   carries the strength factor, so a zero strength provably kills every arm
   including ForwardAdd and the rim sub-arm.
6. The real correctness gap is the cutout family. It neither captures nor
   gates `_DistanceFade` while the vendor fades cutout color at every
   distance and cutout coverage under dither.

## What support requires

One rule covers every donor family. A finite zero strength keeps today's
behavior. A nonzero strength retains the material: every triangle stays on
the original material, and the slot analysis reports a named retention
outcome instead of an unsupported-feature refusal.

1. Cutout correctness first. Add `_DistanceFade` to the cutout evidence
   request. Add the same strength rule to the cutout alpha gates, covering
   the dither-coupled alpha arm and the color arm together.
2. Transparent disposition. Replace the semantics refusal path for a nonzero
   strength with the retention outcome. Keep the finiteness refusal for a
   non-finite strength. Keep the eligibility refusal for conversion, because
   a faded donor genuinely cannot donate. Decide whether the eligibility
   value keeps the name `UnsupportedDistanceFade`.
3. Multi parity. The mirrored transparent rows keep refusing the nonzero
   strength, and the report carries the same retention wording. The mode
   gate transcription already matches the vendor derivation and needs no
   change.
4. Reporting. The retention sentence should name the feature, the property,
   and the retained outcome. It must not use the unsupported-feature
   wording.
5. Tests. The pinned tests flip:
   `LilToonTransparentAlphaTests.cs:1838-1861`,
   `LilToonTransparentSourceEligibilityTests.cs:395-404`, and the Multi
   mirror at `LilToonMultiSourceEligibilityTests.cs:521-537`. New falsifier
   cases must hold: a nonzero strength must never donate in any family, a
   cutout material with dither and a nonzero strength must not donate, a
   non-finite strength must retain, a zero strength must behave exactly as
   today, and no retention report may use the unsupported wording.
6. A design decision the investigation does not settle: what the alpha
   semantics resolve to for a retained material. The candidates are a new
   Unknown reason kind that carries retention, or a complete alpha answer
   plus a slot-level retention flag. The second keeps the provable alpha
   answer alive for a future stronger transformation.

A stronger transformation exists and stays out of this slice. The canonical
opaque target could carry the donor's fade state, so moved triangles keep
the color fade. That direction needs the color property capture, a binary32
argument that the alpha arm is inert when the color alpha is 1, and a
position on the vendor build-time feature scan for the generated target. It
needs its own design spec.

## Out of scope

The per-layer fades `_Main2ndDistanceFade` and `_Main3rdDistanceFade` are
modeled and gated today. The vendor lerp is unguarded by render mode, so the
existing zero-strength admission stays correct as written. The Poiyomi
`_AlphaDistanceFade` property is a separate frontend with its own gate list.
The Lite family compiles the main distance fade out entirely. The opaque
family remains target-only on this branch.

Status (2026-10-02): implemented on branch `feat/liltoon-distance-fade`, where the full EditMode run observed 2363 passed and 0 failed in `Alrauna.Amuse.Tests.Editor` and 138 passed and 0 failed in `Alrauna.Amuse.Research.Tests.Editor`.
