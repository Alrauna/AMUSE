# lilToon 2.3.4 opaque-conversion characterization

## 1. Question and bounded scope

This note discharges the combined B1 pre-design characterization required by
`2026-08-30-liltoon-opaque-conversion.md` §15. The measured candidate is exactly:

- regular lilToon;
- non-Lite, non-Tessellation, non-Multi;
- no outline;
- cutout source;
- no-outline opaque target.

Outline, cutout-outline, transparent, Lite, Tessellation, Multi, Gem, Fur,
Refraction, and every other family were not characterized for support. This is
not B2 alpha-semantics design, a lifecycle verdict, production implementation,
or an R1-R6 refactor.

Labels used below:

- `[MEASURED]`: observed from the installed package by the executed Unity probe;
- `[SOURCE]`: read from the official pinned source or installed package files;
- `[INFERENCE]`: a bounded conclusion from those facts;
- `[DECISION NEEDED]`: a choice that B1 cannot resolve.

## 2. Direct answer

**B1 is discharged for the bounded first-slice candidate.** `[MEASURED]` The
installed source/target assets, hidden pass assets, render modes, canonical
digests, shader-assignment behavior, queue/tag transitions, and all 18 proposed
canonical recipe properties were measured twice with byte-identical probe
output.

**B2 remains necessary.** `[INFERENCE]` B1 establishes representation identity
and mutation/read-back behavior. It does not prove which cutout triangles are
opaque, whether the restricted cutout equation is independent of callback-100
shader regeneration, or which alpha-mask/dither/dissolve states must refuse.
Those are B2's alpha-semantics and callback-independence obligations.

No measurement invalidated the narrow regular/no-outline/cutout first-slice
scope. One earlier factual claim is corrected in §10: in the measured Unity and
package versions, assigning the opaque shader reset the effective custom
`RenderType` override to the opaque shader's declared tag. The vendor utility did
not leave a stale effective override behind.

## 3. Exact environment and package identity

| Fact | Value | Evidence |
|---|---|---|
| AMUSE branch/base | `investigate/liltoon-opaque-characterization` at `ec42d6536f2b3074ef5b90b81b4dfe8b8162c824` | `[MEASURED]` Git before scratch setup |
| Unity | `2022.3.22f1` (`887be4894c44`) | `[MEASURED]` `Application.unityVersion` and scratch `ProjectVersion.txt` |
| Package | `jp.lilxyzw.liltoon` `2.3.4` | `[MEASURED]` `PackageInfo.FindForAssetPath` |
| Package Manager source | `Embedded` | `[MEASURED]` official release archive unpacked under the scratch project's `Packages/` directory |
| Official release artifact | `jp.lilxyzw.liltoon-2.3.4.zip` | `[SOURCE]` package manifest URL and official GitHub release |
| Release archive SHA-256 | `34d172761c51aa9469a904704109086aafa6125a4fa0e058766e2ddc73d3b303` | `[MEASURED]` downloaded artifact |
| Official tag/commit | `2.3.4` / `252fd8cfc46106d4967e95b3f2c788418502f227` | `[MEASURED]` official remote tag lookup; `[SOURCE]` installed package manifest version |

The scratch project lived under the operating system's temporary directory,
outside AMUSE. The probe confirmed that normalized `Application.dataPath` was
exactly the scratch project's `Assets` directory. No AMUSE or Census Lab Unity
instance was selected or queried. No material or scene asset was saved.

## 4. Reproducible method

1. `[SOURCE]` Download the official 2.3.4 package URL recorded by the pinned
   `package.json`; verify the archive hash above and the official `2.3.4` tag's
   commit.
2. Create a clean Unity 2022.3.22f1 project outside AMUSE. Install the official
   archive as the embedded package `Packages/jp.lilxyzw.liltoon`. Open it once
   in batch mode to import and run lilToon's normal editor initialization.
3. `[MEASURED]` Resolve the following with `Shader.Find`, then obtain their
   Unity identities with
   `AssetDatabase.TryGetGUIDAndLocalFileIdentifier` and their package identity
   with `PackageInfo.FindForAssetPath`:
   `Hidden/lilToonCutout`, `lilToon`, `Hidden/ltspass_cutout`, and
   `Hidden/ltspass_opaque`.
4. `[MEASURED]` Read the installed material shader `UsePass` declarations and
   the installed hidden-pass `#define LIL_RENDER` values. Confirm the objects
   are exactly the `lilShaderManager` fields `ltsc`, `lts`, `ltspc`, and
   `ltspo`.
5. `[MEASURED]` Create only transient in-memory materials. For the direct path:
   `new Material(cutoutSource)`, assign the opaque target shader, then assign
   the proposed canonical tuple, explicit queue 2000, and explicit
   `RenderType=Opaque`. For the vendor path, invoke the installed 2.3.4
   `lilMaterialUtils.SetupMaterialWithRenderingMode` opaque branch with regular,
   no-outline, non-Lite, non-Tessellation, non-Multi arguments.
6. `[MEASURED]` Record both the serialized `m_CustomRenderQueue` and the
   effective `Material.renderQueue`/shader queue. Probe inherited (`-1`),
   explicit 2000, and custom 2475 cases. Probe `SetOverrideTag` set/clear on
   both source and target and across the shader swap.
7. `[MEASURED]` For every recipe property, record fresh cutout value, immediate
   post-swap value, assigned canonical value, and read-back. A second sentinel
   material sets distinct values 101 through 118 before cloning so unchanged
   defaults cannot disguise reset behavior.
8. `[MEASURED]` Compute digests from the installed files with AMUSE's
   attestation algorithm: UTF-8/BOM/newline normalization; exact removal of the
   generator-variable setting and shadow-skip regions; normalization only of
   includes that resolve into the installed `Shader/Includes` tree; SHA-256 of
   the canonical material/pass sources; and the sorted path-plus-normalized-hash
   digest of all 37 non-meta include files.
9. Quit Unity, launch it again, rerun the complete probe, and compare outputs.
   The two 694-row outputs were byte-identical. Recomputing the five digests
   after the second run produced the same values.

The target shader, target pass, and include-tree digests exactly equal AMUSE's
existing opaque attestation pins. This cross-check is also a guard that the
characterization used the intended canonicalization rather than ordinary
whole-file hashes.

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
shaders reference only the four no-outline identities shown in the table.

`[SOURCE]` The installed `lilShaderManager` field map and rendering-mode enum
classify `ltsc` as regular cutout and `lts` as regular opaque. `[INFERENCE]` The
measured objects are therefore the precise no-outline regular source and target
selected by the vendor opaque branch; no name-based guess is needed.

## 6. Canonical attestation digests

| Installed evidence | Canonical SHA-256 |
|---|---|
| cutout material shader `lts_cutout.shader` | `c83d73a26ab86e933f8cacb8c71307d8715fcc1693cdc08d209011bb0f836178` |
| cutout pass `ltspass_cutout.shader` | `ecd1caedc99c4569fb17898de16ce2025c21e2d191e06532098370a1291bfe92` |
| opaque material shader `lts.shader` | `5206bec25e82db5f8009b27fcc5ba94d7c41113031d4b6b0a2c25ca324a9c704` |
| opaque pass `ltspass_opaque.shader` | `6b6c30c1cbe546fe753bcdc77f547441e3f9114ee80e9591bde2b8e6e7e5eb14` |
| shared 37-file `Shader/Includes` tree | `6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46` |

All rows are `[MEASURED]` from the installed package, not derived from the
upstream Git tree. The last three values equal the existing AMUSE opaque pins.
The first two are the new raw values needed for future cutout-source attestation.

## 7. Exact clone and shader-assignment result

A fresh cutout material had source GUID
`85d6126cae43b6847aff4b13f4adb8ec`, serialized custom queue `-1`, effective
queue 2450, and effective `RenderType=TransparentCutout`. `new Material(source)`
preserved those facts. `[MEASURED]`

Immediately after assigning the opaque target shader to that clone:

- shader name/GUID became `lilToon` /
  `df12117ecd77c31469c224178886498e`;
- serialized custom queue remained `-1`, so effective queue changed from the
  source shader default 2450 to the target shader default 2000;
- effective `RenderType` changed from `TransparentCutout` to `Opaque`;
- all 18 recipe properties remained present and retained their cutout values;
- a distinct-value sentinel run confirmed that shader reassignment preserved
  each of those 18 stored property values rather than coincidentally returning
  matching target defaults.

When the source clone carried explicit queue 2000 or custom queue 2475, direct
shader assignment reset the serialized custom queue to `-1`. Thus the immediate
effective queue became the opaque shader default 2000 in both cases. This
shader-assignment reset is distinct from both the vendor path, which restores
the prior raw queue, and the canonical AMUSE path, which assigns explicit 2000.

## 8. Render queue and `RenderType`

### Queue measurements

`Material.renderQueue` in this Unity version reports the effective value when
serialized `m_CustomRenderQueue == -1`; the getter therefore reads 2450 or 2000,
not `-1`. The raw serialized column is required to distinguish inheritance from
an explicit queue.

| Source queue precondition | Source raw / effective | Direct swap raw / effective | Vendor opaque raw / effective | Direct canonical raw / effective |
|---|---:|---:|---:|---:|
| inherited/default (`renderQueue` assigned `-1`) | `-1 / 2450` | `-1 / 2000` | `-1 / 2000` | `2000 / 2000` |
| explicit 2000 | `2000 / 2000` | `-1 / 2000` | `2000 / 2000` | `2000 / 2000` |
| representative custom 2475 | `2475 / 2475` | `-1 / 2000` | `2475 / 2475` | `2000 / 2000` |

All rows are `[MEASURED]`. `[INFERENCE]` Explicit canonical queue 2000 remains a
meaningful part of AMUSE's proposed recipe: it removes the inherited-versus-
explicit ambiguity and intentionally declines the vendor's custom-queue
preservation behavior.

### Effective `RenderType` and override behavior

| Probe | Initial | After set | After clear |
|---|---|---|---|
| cutout source, set `Opaque` | `TransparentCutout` | `Opaque` | `TransparentCutout` |
| opaque target, set `TransparentCutout` | `Opaque` | `TransparentCutout` | `Opaque` |

`[MEASURED]` Passing an empty value to
`SetOverrideTag("RenderType", "")` removed the effective override and restored
the current shader's declared tag.

A separate source used the unique override `B1CustomRenderType`. The cutout
source and its clone both read it before the swap. Direct opaque shader
assignment then read `Opaque`; the vendor opaque conversion also read `Opaque`.
`[MEASURED]` Therefore shader reassignment reset the *effective* source override
in this environment. This probe does not assert the undocumented internal
layout of Unity's tag map; its contract observation is the public `GetTag`
read-back.

The direct canonical path explicitly assigned `RenderType=Opaque` and read back
`Opaque`. `[INFERENCE]` Keeping that explicit write and validation remains the
boring deterministic contract even though the measured shader swap already
produces the same effective value.

## 9. Canonical recipe property matrix

All properties below were present on both source and target. `After swap` is
before canonical writes. `Direct read-back` follows the exact assigned canonical
value. `Vendor read-back` follows the installed vendor opaque conversion on a
fresh cutout material.

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
value read back unchanged from the source, the clone before swap, and the clone
after opaque shader assignment. No recipe property was reset, hidden, clamped,
or changed by reassignment. Every canonical assignment then read back exactly.

`[MEASURED]` The installed vendor conversion and direct canonical assignment
produced identical values for all 18 scalar properties, including from a source
whose values were deliberately noncanonical. The fresh cutout defaults were
already canonical except `_AlphaToMask`, which was 1 for cutout and became 0 in
both conversion paths.

## 10. Vendor recipe versus direct canonical assignment

| Behavior | Installed vendor opaque conversion | Direct clone plus proposed canonical assignment |
|---|---|---|
| Target shader | measured opaque `lilToon` object | same measured object |
| 18 scalar recipe values | exact canonical tuple | exact canonical tuple |
| inherited raw queue `-1` | preserved as `-1`; effective 2000 | explicitly 2000 |
| explicit/custom queue | preserves 2000 or 2475 | explicitly 2000 |
| source `RenderType` override | effective value became target default `Opaque` during shader reassignment | explicitly writes and reads back `Opaque` |
| textures, keywords, pass enables | not changed by the invoked vendor method `[SOURCE]` | not touched by the characterized direct path `[MEASURED]` |

The only observed behavioral differences relevant to the proposed recipe are
queue canonicalization and the explicitness of the `RenderType` state. Scalar
state and target identity agree.

### Concrete correction to the earlier investigation

`2026-08-30-liltoon-opaque-conversion.md` §6 said that the non-Multi vendor path
would leave a stale effective `RenderType` override in place. `[MEASURED]` That
is false for Unity 2022.3.22f1 with the installed 2.3.4 package: opaque shader
assignment changed a unique source override to effective `Opaque` before any
explicit tag write. The vendor method itself still contains no non-Multi
`SetOverrideTag` call `[SOURCE]`; the observed reset is Unity shader-assignment
behavior. The earlier note is narrowly corrected to preserve this distinction.

## 11. What B1 proves and does not prove

### Proved for the bounded installed environment

- `[MEASURED]` Exact package, material-shader, hidden-pass, GUID, local-ID,
  render-mode, `UsePass`, and canonical-digest identities.
- `[MEASURED]` The target's three canonical digests reproduce AMUSE's current
  opaque attestation; the source shader/pass values are available to a future
  cutout attestation design.
- `[MEASURED]` A clone can be reassigned from the regular no-outline cutout
  shader to the regular no-outline opaque shader without losing or hiding any
  proposed recipe property.
- `[MEASURED]` Every proposed scalar canonical write is supported and reads back
  exactly; vendor and direct scalar results agree.
- `[MEASURED]` Exact serialized/effective queue transitions and public
  `RenderType` set/clear/swap read-backs.
- `[INFERENCE]` No B1 observation requires support for outline, Lite,
  Tessellation, Multi, transparent, or another shader family, and no general
  shader-family infrastructure is justified.

### Not proved

- No triangle, texture domain, or material was proven visually opaque.
- No cutout alpha equation or evidence request was designed.
- No result covers alpha mask, dither, dissolve, layered alpha, `_Cutoff`
  animation, or another alpha-affecting feature.
- No result proves callback-100 independence, NDMF lifecycle safety, upload-time
  validity, or Apply-on-Play behavior.
- No result covers another Unity version, lilToon version, project shader
  setting, render pipeline, or integration package.
- No visual render comparison was performed; B1 characterizes identities and
  material state transitions only.
- No production attestation constants, support policy, or conversion code are
  authorized by this note alone.

There is no new architectural decision from B1. `[DECISION NEEDED]` B2 must
still decide the conservative cutout alpha proof and demonstrate its
callback-independence boundary. If even the restricted core depends on generated
optional alpha paths, the earlier investigation's Outcome B lifecycle work
becomes a prerequisite; B1 provides no evidence either way.

## 12. Revised blockers and next task

- **B1:** discharged for regular, non-Lite, non-Tessellation, non-Multi,
  no-outline cutout to no-outline opaque under the pinned install.
- **B2:** still blocking. It must define and falsify the non-opaque cutout alpha
  theorem, including `_Color.a`, `_MainTex` alpha, `_Cutoff`, and conservative
  refusal of every callback-generated optional alpha path it cannot make
  invariant.
- **R1-R6 and conversion implementation:** remain downstream; none began here.

**Next recommended task:** the separately reviewed B2 lilToon cutout
alpha-semantics design only, carrying the callback-independence obligation and
falsifier already recorded in the controlling investigation. Do not begin the
conversion refactor or implementation before B2 is resolved.
