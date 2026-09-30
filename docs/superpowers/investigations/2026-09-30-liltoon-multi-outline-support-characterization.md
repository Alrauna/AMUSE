# Investigation: lilToonMultiOutline support inputs

Date: 2026-09-30. Status: characterization complete. No production code changed.
Work branch: `research/liltoon-multi`, cut from `main` at `8b89533`.

Privacy note: this record is sanitized. It names no private avatar, renderer,
material, texture, hierarchy, path, instance, port, or hash from the private
corpus. It names shaders, keywords, properties, report keys, versions, GUIDs,
and queue numbers, because those are public vendor or product data. Corpus
observations appear only as aggregate counts, never as per-avatar or
per-renderer rows.

## 1. Question and scope

The product question: what must AMUSE add to support triangle-level alpha
separation for materials on `Hidden/lilToonMultiOutline`? This note collects
the facts and names the decisions. It does not design the full semantics, pin
digests, or touch production code. Those are the next steps.

Labels: `[SOURCE]` marks a pinned upstream or repo fact with a file and line.
`[MEASURED]` marks an observation executed on 2026-09-30 against the Census Lab
editor instance, read-only. `[INFERENCE]` marks a bounded conclusion.
`[DECISION NEEDED]` marks a controller choice.

## 2. Method and sources

- Upstream pin: lilToon tag `2.3.4`, commit `252fd8cfc46106d4967e95b3f2c788418502f227`
  (`Assets/lilToon/package.json` declares `2.3.4`). The second admitted tag
  `2.3.3` (commit `6ef0cc43b7d56d27f516ea27717faf08486b2842`) was also pinned.
  All upstream cites are relative to `Assets/lilToon/` at that commit.
- Repo state: `main` at `8b89533`. Production cites are repo-relative.
- Lab observation, 2026-09-30: read-only reads of the Census Lab editor
  instance console through the editor automation bridge. The instance identity
  was verified before any read. The retained NDMF console report was read
  through reflection and reduced to aggregates in-editor, so raw private
  report text never left the editor process.
- Corpus scan, 2026-09-30: read-only asset scan of the Census Lab project. The
  scan kept only shader names, mode scalars, queue numbers, and counts.

## 3. The two Multi shader assets `[SOURCE]`

| Fact | `_lil/lilToonMulti` | `Hidden/lilToonMultiOutline` |
|---|---|---|
| Asset | `Shader/ltsmulti.shader` | `Shader/ltsmulti_o.shader` |
| GUID | `9294844b15dca184d914a632279b24e1` | `51b2dee0ab07bd84d8147601ff89e511` |
| Queue tag | Geometry (2000) (`ltsmulti.shader:640`) | Transparent-100 (2900) (`ltsmulti_o.shader:640`) |
| RenderType tag | Opaque | Opaque |
| Passes | FORWARD, FORWARD_ADD, SHADOW_CASTER, META | those four plus FORWARD_OUTLINE, FORWARD_ADD_OUTLINE |
| Outline input define | absent | `LIL_MULTI_INPUTS_OUTLINE` (`ltsmulti_o.shader:673`) |
| Shadow caster | normal | compiles with `LIL_OUTLINE` (`ltsmulti_o.shader:1042`) |

The property blocks of the two assets are byte-identical. Both carry the full
outline property set and the `For Multi` block `_UseOutline`, `_TransparentMode`,
`_UseClippingCanceller`, `_AsOverlay` (`:560-563`). The container difference is
exactly the four rows above. Neither asset declares `LIL_RENDER`. Neither uses
`UsePass`. There is no separate Multi pass asset, so the attestation shape
differs from every pinned lilToon family today.

Mode is keyword-carried `[SOURCE]`:

- `_TransparentMode` is a 0..6 enum at `ltsmulti.shader:561`. Only editor
  tooling reads it. No HLSL reads it.
- `Editor/lilMaterialUtils.cs:397-399` derives mode keywords at edit time:
  `UNITY_UI_ALPHACLIP` for cutout, `UNITY_UI_CLIP_RECT` for transparent and
  fur, `ETC1_EXTERNAL_ALPHA` for cutout with `_UseDither` 1.
- `Shader/Includes/lil_replace_keywords.hlsl:57-63` converts those keywords to
  `LIL_RENDER` 2, 1, or 0 inside the shader. `_COLOROVERLAY_ON` becomes the
  alpha mask feature (`:223-226`). `_DETAIL_MULX2` becomes outline tone
  correction.
- `_AsOverlay` gates `SetShaderPassEnabled("ShadowCaster", ...)`
  (`lilMaterialUtils.cs:400-404`). In the built-in pipeline only the
  ShadowCaster pass exists, so `_AsOverlay` 1 turns off shadow casting. This is
  editor-time state stored on the material. No shader code reads `_AsOverlay`.

Mode keywords are not re-derived at build `[SOURCE]`. The array overload
`SetupMultiMaterial(Material[], AnimationClip[])` (`lilMaterialUtils.cs:476-498`)
runs at the SDK callback and force-enables exactly three animation-derived
keywords on all Multi materials: `GEOM_TYPE_LEAF` from `_RimDirStrength`, and
`EFFECT_HUE_VARIATION` from `_MainTexHSVG` or `_MainGradationStrength`. Those
are color features, not alpha features. On NDMF ApplyOnPlay builds the whole
lilToon preprocess body returns early when the caller is
`nadena.dev.ndmf.ApplyOnPlay` and `isOptimizeInNDMF` is false
(`External/Editor/VRChatModule.cs:55-57`; default false at
`Editor/lilToonSetting.cs:137`). On upload builds the callback 100 hook runs
after the NDMF dispatch `[INFERENCE]`, consistent with F0 section 9.

## 4. Alpha sites in the Multi container `[SOURCE]`

- Base surface alpha chain: `OVERRIDE_MAIN`, then Main2nd and Main3rd alpha
  modes, then alpha mask, then dissolve, then dither
  (`lil_pass_forward_normal.hlsl:278-279, 348-356`;
  `lil_common_frag.hlsl:797-807, 893-903, 455-476, 489-511, 530-545`).
- The clip sites are the alpha blocks. Normal branch:
  `lil_pass_forward_normal.hlsl:394-413`. Outline branch:
  `lil_pass_forward_normal.hlsl:226-237`. Each block branches on the effective
  `LIL_RENDER`: 0 forces `fd.col.a = 1.0`; 1 rescales and discards; 2 clips on
  `_Cutoff`.
- Feature gates ride on `LIL_RENDER`: alpha mask needs `LIL_RENDER != 0`
  (`:362-364`), dither needs `LIL_RENDER == 1` (`:388-390`), the additive
  premultiply needs `LIL_RENDER == 2` (`lil_common_frag.hlsl:553-560`).
- Outline alpha comes from `_OutlineTex.a` times `_OutlineColor.a`
  (`lil_common_frag.hlsl:362-367, 380-389`). In opaque mode the outline alpha
  block forces it to 1.0, so the outline fragment cannot clip or fade.
- Outline vertex collapse conditions: `_Invisible` or `_OutlineDisableInVR`
  (`lil_common_vert.hlsl:69-74`), and near-zero width with
  `_OutlineDeleteMesh` (`:352-357`). Defaults: `_OutlineWidth` 0.08,
  `_OutlineZTest` 2 (Less), `_OutlineCull` 1, `_OutlineZWrite` 1
  (`ltsmulti_o.shader:540-546, 608-635`).

Consequence for opaque mode: at `_TransparentMode` 0 the container behaves as
an opaque surface plus a solid-width outline shell. Neither alpha mask, dither,
nor the transparent premultiply is reachable. This is the simplification the
support design can use.

## 5. Vendor queue behavior for Multi `[SOURCE]`

`SetupMaterialWithRenderingMode` captures the original queue but restores it
only for non-Multi (`lilMaterialUtils.cs:21, 266`). The Multi opaque case sets
`RenderType` override to empty and queue to -1 (`:44-52`). A material with
queue -1 uses the shader default: 2000 for the base asset, 2900 for the
outline asset. Cutout Multi writes 2450, transparent Multi writes 2460 (3000
in the worlds override at `:337-351`).

## 6. What AMUSE does today `[SOURCE]`

- Selection is an exact name map. `UnityMaterialSemantics.ClassifyShaderName`
  (`Editor/Semantics/UnityMaterialSemantics.cs:540-650`) lists the admitted
  lilToon names. The five Multi names sit in the Unsupported near-miss test
  list (`Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs:716-720`).
- An unsupported name yields no family and no request. The material becomes the
  unsupported sentinel, its alpha semantics answer all-Unknown, and the slot
  refuses as semantics-unknown
  (`Editor/Host/UnityAnimationEvidenceCapture.cs:571-575`).
- Conversion would refuse with `AlphaSeparationSlotRefusal.
  OpaqueConversionUnsupportedFamily` per slot
  (`Editor/Build/AlphaSeparationPreparation.cs:885-891`), but Multi slots
  refuse earlier, at slot analysis, because their semantics are Unknown.
- No attestation record exists for either Multi asset
  (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:341-524`).
- The attestation mode check `TryScanRenderMode` requires exactly one
  `#define LIL_RENDER <int>` in the pass asset
  (`LilToonSourceAttestation.cs:894-896, 1342-1346`). Multi assets never
  declare `LIL_RENDER`, so the existing scan cannot verify a Multi profile. A
  Multi attestation needs its own mode evidence, for example the keyword
  derivation table in section 3. This is the concrete form of the F0 note that
  the single-asset attestation shape differs from all others.

### The S8 target model, the template for Multi

S8 admitted the three regular outline wrappers as second profiles in their
families, and the canonical clone resolves the target by source
(`ResolveCanonicalTargetShaderName`,
`LilToonSourceAttestation.cs:1803-1806`): any outline wrapper moves to
`Hidden/lilToonOutline`, everything else moves to `lilToon`. The clone keeps
the outline passes, so the outline is never silently dropped.

For Multi the analogous target is the source asset itself. The vendor models
Multi opaque as state on one asset, not as a different asset (F0 section 8
predicted exactly this). Moving a `Hidden/lilToonMultiOutline` source to the
base Multi asset would drop the outline passes, the same hazard S8 avoided.
Staying on the source asset keeps the passes and makes the outline question
moot in opaque mode, because the outline fragment alpha is forced to 1.0 there
(section 4). `[INFERENCE]` the same-asset target removes the need for a second
outline-alpha theorem in the mode-0 slice.

## 7. Lab observations `[MEASURED]`, 2026-09-30

NDMF console, read as aggregates over the five retained builds:

- The newest build reports 30 slot entries under
  `amuse.slotAnalysis.Refusal`, 13 renderer entries under
  `amuse.renderer.AdmittedMaterialSemanticsUnknown`, 1
  `amuse.renderer.UnsupportedRendererType`, 2
  `amuse.slotSeparation.OpaqueCoverageBelowMinimum`, and 1 apply summary under
  `amuse.summary.Title`. Every AMUSE entry is severity Information. Each entry
  also logs one Unity console warning.
- Older retained builds show the wider refusal vocabulary:
  `amuse.slotSeparation.OpaqueConversionRefused`,
  `amuse.texture.UnsupportedFormat`, `amuse.texture.UnavailableCapture`,
  `amuse.renderer.MaterialDependencyClosureFailed`,
  `amuse.renderer.UnprovenMaterialSlotMapping`. No entry repeated with an
  identical key and arguments, so the dedup fix from the 2026-09-29 note holds
  in the Lab.
- No Multi-specific key exists. Today a Multi material can only surface as the
  generic semantics-unknown refusal.

Corpus scan (counts are whole-corpus aggregates):

- 2292 materials scanned.
- 127 use `_lil/lilToonMulti`. 9 use `Hidden/lilToonMultiOutline`.
- All 136 report `_TransparentMode` 0. All 136 report `_AsOverlay` 0.
- All 136 report queue 2000. The 9 outline-container materials therefore carry
  an explicit queue override, because their shader default is 2900. The
  RenderType override tag was not captured.

Reading: the corpus pressure is entirely opaque-mode Multi. The observed
MultiOutline population sits at the same queue as opaque surfaces, so a queue
gate modeled on the cutout 2450 or transparent 2460 gates would refuse the
whole observed population if it demanded 2900. The gate set must accept the
observed opaque-mode state or define a canonical queue first.

## 8. What support requires

1. Attestation, new shape. Name, GUID, package version rows for 2.3.0 through
   2.3.4, `_lilToonVersion` 45, canonical source digest of the Multi asset,
   include-tree digest per version. The Multi shader, meta, and editor files
   are byte-identical between 2.3.3 and 2.3.4; the only include difference is
   one `LIL_ADDITIONAL_LIGHT_MODE` line in `lil_common_macro.hlsl`. The
   `LIL_RENDER` scan must be replaced by Multi mode evidence: the keyword
   derivation table of section 3, or a pinned keyword-to-mode map. Attestation
   covers both assets with one profile each; the containers share one property
   block, so one recipe serves both.
2. Mode gates. Capture and gate `_TransparentMode`, the mode keywords
   (`UNITY_UI_ALPHACLIP`, `UNITY_UI_CLIP_RECT`, `ETC1_EXTERNAL_ALPHA`,
   `_COLOROVERLAY_ON`), and `_AsOverlay` pass-enable state as material state,
   and require them to agree. The three animation-derived keywords added after
   NDMF are alpha-irrelevant and need no gate.
3. Eligibility. Queue and RenderType gates must model the Multi opaque state:
   explicit 2000 or the shader default path, RenderType Opaque or empty. Depth,
   blend, color mask, offset, and cutoff gates can reuse the cutout evaluator
   shape, because the property block is shared. Outline state gates
   (`_UseOutline`, `_OutlineWidth`, `_OutlineFixWidth`, `_OutlineDeleteMesh`,
   `_OutlineDisableInVR`, `_OutlineZTest`, `_OutlineZWrite`, `_OutlineCull`,
   `_OutlineColorMask`) mirror the T2 gate rows.
4. Recipe and target. Target = the source asset itself, canonical state on a
   clone: the base 18 canonical writes, mode keyword set for opaque, and a
   defined canonical queue and RenderType row.
5. Refusal vocabulary. Today `OpaqueConversionUnsupportedFamily` is the only
   Multi answer. A mode that AMUSE refuses, for example cutout-mode Multi,
   deserves a named refusal value with its own report key, per the closed
   refusal enum policy.

## 9. Decisions needed

1. `[DECISION NEEDED]` Slice scope. Options: (a) `Hidden/lilToonMultiOutline`
   at mode 0 only, 9 observed materials; (b) both Multi containers at mode 0,
   136 observed materials, shared machinery; (c) mode 1 cutout later. The
   machinery cost is shared; (b) is nearly the same size as (a) and covers the
   whole observed opaque population. Recommendation: (b).
2. `[DECISION NEEDED]` Canonical queue row for the Multi target: keep the
   source's explicit queue, write the vendor Multi opaque form (RenderType
   empty, queue -1, shader default), or write an explicit 2000. Recommendation:
   write the vendor Multi opaque form, because it matches vendor conversion
   output and removes the explicit-override case from the evidence model.
3. `[DECISION NEEDED]` Whether `_AsOverlay` 1 (shadow casting disabled) is
   supportable in the first slice or refuses with a named value. The clone
   must preserve pass-enable state either way; verify that `new Material`
   copies it before relying on it.
4. `[DECISION NEEDED]` Whether the alpha-mask keyword `_COLOROVERLAY_ON` can be
   proven off by mode alone in mode 0 (vendor sets it only for non-opaque
   modes) or needs its own gate. The source-grounded derivation table suggests
   mode alone suffices in mode 0; a falsifier test should pin it.

## 10. Status

Characterization complete on 2026-09-30. The F0 roadmap row 7 prerequisite
(cutout slice shipped) is discharged. Next step on this branch: a design spec
for the chosen slice, then digests measured from the pinned tag artifacts, then
RED/GREEN tasks. No production change has been made.
