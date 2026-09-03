# lilToon 2.3.4 opaque-conversion characterization

## 1. Question and bounded scope

This note is the combined B1 pre-design characterization that §15 of
`2026-08-30-liltoon-opaque-conversion.md` requires. The probe measured exactly
this candidate:

- regular lilToon
- non-Lite, non-Tessellation, non-Multi
- no outline
- cutout source
- no-outline opaque target

This note does not characterize support for outline, cutout-outline,
transparent, Lite, Tessellation, Multi, Gem, Fur, Refraction, or any other
family. This note is not the B2 alpha-semantics design, a lifecycle verdict,
production implementation, or an R1-R6 refactor.

Labels used below:

- `[MEASURED]`: observed from the installed package by the executed Unity probe
- `[SOURCE]`: read from the official pinned source or installed package files
- `[INFERENCE]`: a bounded conclusion from those facts
- `[DECISION NEEDED]`: a choice that B1 cannot resolve

## 2. Direct answer

**B1 is complete for the bounded first-slice candidate.** `[MEASURED]` The
probe ran the complete measurement two times, and the two outputs were
byte-identical. The measured items were:

- the installed source and target assets
- the hidden pass assets
- the render modes
- the canonical digests
- the shader-assignment behavior
- the queue/tag transitions
- all 18 proposed canonical recipe properties

**B2 remains necessary.** `[INFERENCE]` B1 establishes representation identity
and mutation/read-back behavior. B1 does not prove:

- which cutout triangles are opaque
- that the restricted cutout equation is independent of callback-100 shader
  regeneration
- which alpha-mask/dither/dissolve states must refuse

Those are the B2 obligations for alpha semantics and callback independence.

No measurement invalidated the narrow regular/no-outline/cutout first-slice
scope. §10 corrects one earlier factual claim. In the measured Unity and
package versions, opaque shader assignment reset the effective custom
`RenderType` override to the tag that the opaque shader declares. The vendor
utility did not leave a stale effective override behind.

## 3. Exact environment and package identity

| Fact | Value | Evidence |
|---|---|---|
| AMUSE branch/base | `investigate/liltoon-opaque-characterization` at `ec42d6536f2b3074ef5b90b81b4dfe8b8162c824` | `[MEASURED]` Git before scratch setup |
| Unity | `2022.3.22f1` (`887be4894c44`) | `[MEASURED]` `Application.unityVersion` and scratch `ProjectVersion.txt` |
| Package | `jp.lilxyzw.liltoon` `2.3.4` | `[MEASURED]` `PackageInfo.FindForAssetPath` |
| Package Manager source | `Embedded` | `[MEASURED]` official release archive unpacked under the `Packages/` directory of the scratch project |
| Official release artifact | `jp.lilxyzw.liltoon-2.3.4.zip` | `[SOURCE]` package manifest URL and official GitHub release |
| Release archive SHA-256 | `34d172761c51aa9469a904704109086aafa6125a4fa0e058766e2ddc73d3b303` | `[MEASURED]` downloaded artifact |
| Official tag/commit | `2.3.4` / `252fd8cfc46106d4967e95b3f2c788418502f227` | `[MEASURED]` official remote tag lookup, `[SOURCE]` installed package manifest version |

The scratch project lived under the operating system temporary directory,
outside AMUSE. The probe confirmed that normalized `Application.dataPath` was
exactly the `Assets` directory of the scratch project. The probe selected and
queried no AMUSE or Census Lab Unity instance. The probe saved no material or
scene asset.

## 4. Reproducible method

1. `[SOURCE]` Download the official 2.3.4 package from the URL recorded by the
   pinned `package.json`. Verify the archive hash above and the commit of the
   official `2.3.4` tag.
2. Create a clean Unity 2022.3.22f1 project outside AMUSE. Install the official
   archive as the embedded package `Packages/jp.lilxyzw.liltoon`. Open it once
   in batch mode. Batch mode imports the package and runs the normal lilToon
   editor initialization.
3. `[MEASURED]` Resolve `Hidden/lilToonCutout`, `lilToon`,
   `Hidden/ltspass_cutout`, and `Hidden/ltspass_opaque` with `Shader.Find`. Get
   the Unity identities with `AssetDatabase.TryGetGUIDAndLocalFileIdentifier`.
   Get the package identity with `PackageInfo.FindForAssetPath`.
4. `[MEASURED]` Read the installed material shader `UsePass` declarations. Read
   the installed hidden-pass `#define LIL_RENDER` values. Confirm that the
   objects are exactly the `lilShaderManager` fields `ltsc`, `lts`, `ltspc`,
   and `ltspo`.
5. `[MEASURED]` Create only transient in-memory materials. For the direct path,
   create `new Material(cutoutSource)`. Assign the opaque target shader. Then
   assign the proposed canonical tuple, explicit queue 2000, and explicit
   `RenderType=Opaque`. For the vendor path, invoke the installed 2.3.4
   `lilMaterialUtils.SetupMaterialWithRenderingMode` opaque branch. Use regular,
   no-outline, non-Lite, non-Tessellation, non-Multi arguments.
6. `[MEASURED]` Record both the serialized `m_CustomRenderQueue` and the
   effective `Material.renderQueue`/shader queue. Probe the inherited (`-1`),
   explicit 2000, and custom 2475 cases. Probe `SetOverrideTag` set and clear
   on both source and target, and across the shader swap.
7. `[MEASURED]` For every recipe property, record the fresh cutout value, the
   immediate post-swap value, the assigned canonical value, and the read-back.
   A second sentinel material sets the distinct values 101 through 118 before
   cloning. Unchanged defaults then cannot disguise reset behavior.
8. `[MEASURED]` Compute digests from the installed files with the AMUSE
   attestation algorithm:
   - Normalize UTF-8, BOM, and newline bytes.
   - Remove exactly the generator-variable setting and the shadow-skip regions.
   - Normalize only includes that resolve into the installed `Shader/Includes`
     tree.
   - Compute the SHA-256 of the canonical material and pass sources.
   - Compute the sorted path-plus-normalized-hash digest of all 37 non-meta
     include files.
9. Quit Unity. Launch Unity again. Rerun the complete probe. Compare the
   outputs. The two 694-row outputs were byte-identical. The probe recomputed
   the five digests after the second run, and the values were the same.

The target shader, target pass, and include-tree digests exactly equal the
existing AMUSE opaque attestation pins. This cross-check also confirms that the
characterization used the intended canonicalization, not ordinary whole-file
hashes.

## 5. Installed shader and pass identities

| Role | Shader name | GUID | Local file ID | Installed asset | Default queue |
|---|---|---|---:|---|---:|
| cutout source | `Hidden/lilToonCutout` | `85d6126cae43b6847aff4b13f4adb8ec` | 4800000 | `Shader/lts_cutout.shader` | 2450 |
| opaque target | `lilToon` | `df12117ecd77c31469c224178886498e` | 4800000 | `Shader/lts.shader` | 2000 |
| cutout pass asset | `Hidden/ltspass_cutout` | `ad219df2a46e841488aee6a013e84e36` | 4800000 | `Shader/ltspass_cutout.shader` | 2000 |
| opaque pass asset | `Hidden/ltspass_opaque` | `61b4f98a5d78b4a4a9d89180fac793fc` | 4800000 | `Shader/ltspass_opaque.shader` | 2000 |

All rows are `[MEASURED]` from the real installed package. Asset paths in this
note are relative to `Packages/jp.lilxyzw.liltoon/`.

### Pass relationship and render-mode measurements

| Fact | Cutout source | Opaque target |
|---|---|---|
| `lilShaderManager` material field | `ltsc`, same Unity object | `lts`, same Unity object |
| material shader `UsePass` identities | `Hidden/ltspass_cutout/{FORWARD, FORWARD_ADD, SHADOW_CASTER, META}` | `Hidden/ltspass_opaque/{FORWARD, FORWARD_ADD, SHADOW_CASTER, META}` |
| `lilShaderManager` pass field | `ltspc`, same Unity object | `ltspo`, same Unity object |
| hidden-pass render define | exactly one: `#define LIL_RENDER 1` | exactly one: `#define LIL_RENDER 0` |
| family classification | regular, no-outline, cutout | regular, no-outline, opaque |

`[MEASURED]` Each hidden pass asset reports seven declared named passes:
`FORWARD`, `FORWARD_OUTLINE`, `FORWARD_ADD`, `FORWARD_ADD_OUTLINE`,
`SHADOW_CASTER`, `SHADOW_CASTER_OUTLINE`, and `META`. The no-outline material
shaders reference only the four no-outline identities in the table.

`[SOURCE]` The installed `lilShaderManager` field map and rendering-mode enum
classify `ltsc` as regular cutout and `lts` as regular opaque. `[INFERENCE]`
The measured objects are therefore the exact no-outline regular source and
target that the vendor opaque branch selects. A name-based guess is not
necessary.

## 6. Canonical attestation digests

| Installed evidence | Canonical SHA-256 |
|---|---|
| cutout material shader `lts_cutout.shader` | `c83d73a26ab86e933f8cacb8c71307d8715fcc1693cdc08d209011bb0f836178` |
| cutout pass `ltspass_cutout.shader` | `ecd1caedc99c4569fb17898de16ce2025c21e2d191e06532098370a1291bfe92` |
| opaque material shader `lts.shader` | `5206bec25e82db5f8009b27fcc5ba94d7c41113031d4b6b0a2c25ca324a9c704` |
| opaque pass `ltspass_opaque.shader` | `6b6c30c1cbe546fe753bcdc77f547441e3f9114ee80e9591bde2b8e6e7e5eb14` |
| shared 37-file `Shader/Includes` tree | `6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46` |

All rows are `[MEASURED]` from the installed package, not from the upstream Git
tree. The last three values equal the existing AMUSE opaque pins. The first two
are the new raw values that a future cutout-source attestation needs.

## 7. Exact clone and shader-assignment result

A fresh cutout material had source GUID `85d6126cae43b6847aff4b13f4adb8ec`,
serialized custom queue `-1`, effective queue 2450, and effective
`RenderType=TransparentCutout`. `new Material(source)` preserved those facts.
`[MEASURED]`

Immediately after the probe assigned the opaque target shader to that clone:

- The shader name/GUID became `lilToon` /
  `df12117ecd77c31469c224178886498e`.
- The serialized custom queue stayed `-1`. The effective queue therefore
  changed from the source shader default 2450 to the target shader default
  2000.
- The effective `RenderType` changed from `TransparentCutout` to `Opaque`.
- All 18 recipe properties remained present and kept their cutout values.
- A distinct-value sentinel run confirmed that shader reassignment preserved
  each of those 18 stored property values. The values did not return matching
  target defaults by coincidence.

When the source clone carried explicit queue 2000 or custom queue 2475, direct
shader assignment reset the serialized custom queue to `-1`. The immediate
effective queue therefore became the opaque shader default 2000 in both cases.
This shader-assignment reset differs from the vendor path and from the
canonical AMUSE path. The vendor path restores the prior raw queue. The
canonical AMUSE path assigns explicit 2000.

## 8. Render queue and `RenderType`

### Queue measurements

`Material.renderQueue` in this Unity version reports the effective value when
the serialized `m_CustomRenderQueue` is `-1`. The getter therefore reads 2450
or 2000, not `-1`. The raw serialized column is required to distinguish
inheritance from an explicit queue.

| Source queue precondition | Source raw / effective | Direct swap raw / effective | Vendor opaque raw / effective | Direct canonical raw / effective |
|---|---:|---:|---:|---:|
| inherited/default (`renderQueue` assigned `-1`) | `-1 / 2450` | `-1 / 2000` | `-1 / 2000` | `2000 / 2000` |
| explicit 2000 | `2000 / 2000` | `-1 / 2000` | `2000 / 2000` | `2000 / 2000` |
| representative custom 2475 | `2475 / 2475` | `-1 / 2000` | `2475 / 2475` | `2000 / 2000` |

All rows are `[MEASURED]`. `[INFERENCE]` Explicit canonical queue 2000 remains
a meaningful part of the proposed AMUSE recipe. It removes the
inherited-versus-explicit ambiguity. It also intentionally declines the vendor
custom-queue preservation behavior.

### Effective `RenderType` and override behavior

| Probe | Initial | After set | After clear |
|---|---|---|---|
| cutout source, set `Opaque` | `TransparentCutout` | `Opaque` | `TransparentCutout` |
| opaque target, set `TransparentCutout` | `Opaque` | `TransparentCutout` | `Opaque` |

`[MEASURED]` A `SetOverrideTag("RenderType", "")` call with an empty value
removed the effective override. It restored the tag that the current shader
declares.

A separate source used the unique override `B1CustomRenderType`. The cutout
source and its clone both read it before the swap. Direct opaque shader
assignment then read `Opaque`. The vendor opaque conversion also read `Opaque`.

`[MEASURED]` Shader reassignment therefore reset the *effective* source
override in this environment. This probe does not assert the undocumented
internal layout of the Unity tag map. Its contract observation is the public
`GetTag` read-back.

The direct canonical path explicitly assigned `RenderType=Opaque` and read back
`Opaque`. `[INFERENCE]` The measured shader swap already produces the same
effective value. The explicit write and validation stay in the recipe as the
boring, deterministic contract.

## 9. Canonical recipe property matrix

All properties in the table were present on both source and target. `After
swap` is before the canonical writes. `Direct read-back` follows the exact
assigned canonical value. `Vendor read-back` follows the installed vendor
opaque conversion on a fresh cutout material.

| Property | Fresh cutout | After swap | Assigned canonical | Direct read-back | Vendor read-back | Swap behavior |
|---|---:|---:|---:|---:|---:|---|
| `_SrcBlend` | 1 | 1 | 1 | 1 | 1 | preserved |
| `_DstBlend` | 0 | 0 | 0 | 0 | 0 | preserved |
| `_AlphaToMask` | 1 | 1 | 0 | 0 | 0 | preserved, then changed by recipe |
| `_ZWrite` | 1 | 1 | 1 | 1 | 1 | preserved |
| `_ZTest` | 4 | 4 | 4 | 4 | 4 | preserved |
| `_OffsetFactor` | 0 | 0 | 0 | 0 | 0 | preserved |
| `_OffsetUnits` | 0 | 0 | 0 | 0 | 0 | preserved |
| `_ColorMask` | 15 | 15 | 15 | 15 | 15 | preserved |
| `_SrcBlendAlpha` | 1 | 1 | 1 | 1 | 1 | preserved |
| `_DstBlendAlpha` | 10 | 10 | 10 | 10 | 10 | preserved |
| `_BlendOp` | 0 | 0 | 0 | 0 | 0 | preserved |
| `_BlendOpAlpha` | 0 | 0 | 0 | 0 | 0 | preserved |
| `_SrcBlendFA` | 1 | 1 | 1 | 1 | 1 | preserved |
| `_DstBlendFA` | 1 | 1 | 1 | 1 | 1 | preserved |
| `_SrcBlendAlphaFA` | 0 | 0 | 0 | 0 | 0 | preserved |
| `_DstBlendAlphaFA` | 1 | 1 | 1 | 1 | 1 | preserved |
| `_BlendOpFA` | 4 (`Max`) | 4 | 4 | 4 | 4 | preserved |
| `_BlendOpAlphaFA` | 4 (`Max`) | 4 | 4 | 4 | 4 | preserved |

`[MEASURED]` The sentinel run assigned 101 through 118 in table order. Every
value read back unchanged from the source, the clone before the swap, and the
clone after the opaque shader assignment. Reassignment did not reset, hide,
clamp, or change any recipe property. Every canonical assignment then read back
exactly.

`[MEASURED]` The installed vendor conversion and the direct canonical
assignment produced identical values for all 18 scalar properties. This
included a source whose values were deliberately noncanonical. The fresh
cutout defaults were already canonical except `_AlphaToMask`. `_AlphaToMask`
was 1 for cutout and became 0 in both conversion paths.

## 10. Vendor recipe versus direct canonical assignment

| Behavior | Installed vendor opaque conversion | Direct clone plus proposed canonical assignment |
|---|---|---|
| Target shader | measured opaque `lilToon` object | same measured object |
| 18 scalar recipe values | exact canonical tuple | exact canonical tuple |
| inherited raw queue `-1` | preserved as `-1`, effective 2000 | explicitly 2000 |
| explicit/custom queue | preserves 2000 or 2475 | explicitly 2000 |
| source `RenderType` override | effective value became target default `Opaque` during shader reassignment | explicitly writes and reads back `Opaque` |
| textures, keywords, pass enables | not changed by the invoked vendor method `[SOURCE]` | not touched by the characterized direct path `[MEASURED]` |

The only observed behavioral differences that matter to the proposed recipe are
queue canonicalization and the explicitness of the `RenderType` state. Scalar
state and target identity agree.

### Concrete correction to the earlier investigation

`2026-08-30-liltoon-opaque-conversion.md` §6 said that the non-Multi vendor
path would leave a stale effective `RenderType` override in place.
`[MEASURED]` That claim is false for Unity 2022.3.22f1 with the installed 2.3.4
package. Opaque shader assignment changed a unique source override to effective
`Opaque` before any explicit tag write. The vendor method itself still contains
no non-Multi `SetOverrideTag` call `[SOURCE]`. The observed reset is Unity
shader-assignment behavior. This correction to the earlier note is narrow and
preserves this distinction.

## 11. What B1 proves and does not prove

### Proved for the bounded installed environment

- `[MEASURED]` Exact package, material-shader, hidden-pass, GUID, local-ID,
  render-mode, `UsePass`, and canonical-digest identities.
- `[MEASURED]` The three target canonical digests reproduce the current AMUSE
  opaque attestation. The source shader and pass digest values are available
  to a future cutout attestation design.
- `[MEASURED]` The probe reassigned a clone from the regular no-outline cutout
  shader to the regular no-outline opaque shader. The reassignment lost or hid
  no proposed recipe property.
- `[MEASURED]` Every proposed scalar canonical write is supported and reads
  back exactly. Vendor and direct scalar results agree.
- `[MEASURED]` Exact serialized/effective queue transitions and public
  `RenderType` set, clear, and swap read-backs.
- `[INFERENCE]` No B1 observation requires support for outline, Lite,
  Tessellation, Multi, transparent, or another shader family. No B1 observation
  justifies general shader-family infrastructure.

### Not proved

- No probe proved a triangle, texture domain, or material visually opaque.
- This note designed no cutout alpha equation and no evidence request.
- No result covers alpha mask, dither, dissolve, layered alpha, `_Cutoff`
  animation, or another alpha-affecting feature.
- No result proves callback-100 independence, NDMF lifecycle safety,
  upload-time validity, or Apply-on-Play behavior.
- No result covers another Unity version, lilToon version, project shader
  setting, render pipeline, or integration package.
- This note includes no visual render comparison. B1 characterizes identities
  and material state transitions only.
- This note alone authorizes no production attestation constants, no support
  policy, and no conversion code.

B1 produced no new architectural decision. `[DECISION NEEDED]` B2 must still
decide the conservative cutout alpha proof. B2 must also show its
callback-independence boundary. If even the restricted core depends on
generated optional alpha paths, the Outcome B lifecycle work from the earlier
investigation becomes a prerequisite. B1 gives no evidence either way.

## 12. Revised blockers and next task

- **B1:** discharged for regular, non-Lite, non-Tessellation, non-Multi,
  no-outline cutout to no-outline opaque under the pinned install.
- **B2:** still blocking. It must define and falsify the non-opaque cutout
  alpha theorem. This work includes `_Color.a`, `_MainTex` alpha, `_Cutoff`,
  and conservative refusal of every callback-generated optional alpha path
  that it cannot make invariant.
- **R1-R6 and conversion implementation:** remain downstream. None began here.

**Next recommended task:** the separately reviewed B2 lilToon cutout
alpha-semantics design only. It carries the callback-independence obligation
and the falsifier that the controlling investigation already records. Do not
start the conversion refactor or the implementation before B2 is complete.
