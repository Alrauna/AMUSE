# Investigation: the three lilToonMulti open questions

Date: 2026-09-30. Status: all three questions settled on 2026-09-30. No
production code changed. No test ran. This note is the only file this
investigation created.

Work branch: `research/liltoon-multi`. HEAD at run start:
`8b89533d525bcaf47b879f32c42bb42805dd7c3a`, the expected base. The tree
matched the brief: exactly the two untracked 2026-09-30 companion notes and
nothing else.

Privacy note: this record names public vendor facts, repository facts, and
aggregate counts only. It names no private avatar, renderer, or material. It
names no machine, instance, port, or absolute path. The census scan in
section 4 ran read-only against the Census Lab editor instance. The editor
itself confirmed the project identity before any read.

## 1. Scope and method

This note settles the three `[OPEN]` items in section 9 of the mapping note
`2026-09-30-liltoon-multi-to-regular-mapping.md`. Companion context:
`2026-09-30-liltoon-multi-outline-support-characterization.md`,
`2026-08-30-liltoon-family-applicability.md` (F0),
`2026-08-30-liltoon-opaque-conversion.md`, and
`2026-08-30-liltoon-cutout-alpha-semantics.md` (B2).

Upstream pin: lilToon tag `2.3.4`, commit
`252fd8cfc46106d4967e95b3f2c788418502f227`. Every upstream cite is
file:line relative to `Assets/lilToon/` at that commit. Repo cites are
repo-relative at the HEAD above.

Labels. `[SOURCE]` is a pinned upstream or repository fact.
`[MEASURED]` is the 2026-09-30 census observation. `[INFERENCE]` is a
bounded conclusion. `[FINDING]` is a recorded inconsistency that this
investigation does not fix. `[OPEN]` marks anything still unresolved.

Two method facts the verdicts rest on:

- The pinned `Shader/Includes/lil_pass_forward_normal.hlsl` ships as a plain
  include. The generator rewrites `.shader` assets and exactly one optimizer
  include (section 3), never this file. The pinned bytes are therefore the
  installed bytes for the tag, for every install. `[SOURCE]` `[INFERENCE]`
- Question 3 used one read-only asset-database query through the editor of
  the Census Lab project. No asset was created, modified, or deleted. No
  build, play-mode run, or test run happened. `[MEASURED]`

## 2. Question 1: which alpha block runs at LIL_RENDER 1

### 2.1 Every alpha site in `lil_pass_forward_normal.hlsl` `[SOURCE]`

The fragment carries two copies of the alpha machinery. `#if
defined(LIL_OUTLINE)` at `Shader/Includes/lil_pass_forward_normal.hlsl:173`
selects the outline copy. `#else` at `:261` selects the normal copy.
`#endif` at `:588` closes. Each copy runs its own `LIL_RENDER` dispatch. No
alpha write, clip, or discard sits under a `LIL_PASS_FORWARDADD` guard. The
`LIL_PASS_FORWARDADD` guards in the file (`:22, :65, :147, :252-256,
:448-482, :494-498, :552-583`) wrap lighting and rgb premultiply only.

Direct sites. F1 is `Shader/Includes/lil_pass_forward_normal.hlsl`.

| Site | Guard chain | Verbatim form | Compiled at LIL_RENDER 1 |
|---|---|---|---|
| F1:207, :211, :214 | outline copy, `LIL_FEATURE_DISSOLVE && LIL_RENDER != 0` (`:202`), runtime `fd.dissolveActive` (`:204`) | dissolve writes `fd.col.a = 1.0f`, `fd.col.a = 1.0f - fd.col.a`, `fd.col.a *= priorAlpha` | compiled, runtime-conditional |
| F1:229 | outline copy, `#if LIL_RENDER == 0` (`:227`) | `fd.col.a = 1.0;` | preprocessed out |
| F1:232-233 | outline copy, `#elif LIL_RENDER == 1` (`:230`) | coverage transform plus `discard` on outline alpha | **this compiles** |
| F1:236 | outline copy, `#elif LIL_RENDER == 2 && !defined(LIL_REFRACTION)` (`:234`) | `clip(fd.col.a - _Cutoff);` | preprocessed out |
| F1:374, :378, :381 | normal copy, `LIL_FEATURE_DISSOLVE && LIL_RENDER != 0` (`:369`), runtime `:371` | dissolve writes, mirror of `:207-214` | compiled, runtime-conditional |
| F1:396 | normal copy, `#if LIL_RENDER == 0` (`:394`) | `fd.col.a = 1.0;` | preprocessed out |
| F1:402-403 | normal copy, `#elif LIL_RENDER == 1` (`:397`) | coverage transform plus `discard` on surface alpha, transform line wrapped by `if(!_UseDither)` when `LIL_FEATURE_DITHER` (`:399-401`), discard unconditional | **this compiles** |
| F1:407-409 | normal copy, `LIL_RENDER == 2` arm (`:404`) under `LIL_TRANSPARENT_PRE` (`:406`) | `_PreColor`, `clip(fd.col.a - _PreCutoff)`, early return | preprocessed out |
| F1:411 | normal copy, `#else` of the `LIL_TRANSPARENT_PRE` block (`:410`) | `clip(fd.col.a - _Cutoff);` | preprocessed out |

The exact outline-copy cutout block (`:230-233`), verbatim:

```hlsl
#elif LIL_RENDER == 1
    // Cutout
    fd.col.a = saturate((fd.col.a - _Cutoff) / max(fwidth(fd.col.a), 0.0001) + 0.5);
    if(fd.col.a == 0) discard;
```

The exact normal-copy cutout block (`:397-403`), verbatim:

```hlsl
#elif LIL_RENDER == 1
    // Cutout
    #if defined(LIL_FEATURE_DITHER)
        if(!_UseDither)
    #endif
    fd.col.a = saturate((fd.col.a - _Cutoff) / max(fwidth(fd.col.a), 0.0001) + 0.5);
    if(fd.col.a == 0) discard;
```

Macro-hook sites. The invocation sits in F1, the write sits in the expansion
in `Shader/Includes/lil_common_frag.hlsl` (F3). `[SOURCE]`

| Hook | Invocation and guard | Expansion write | State at LIL_RENDER 1 |
|---|---|---|---|
| `OVERRIDE_UDIMDISCARD` | F1:156, `LIL_FEATURE_UDIMDISCARD` (`:155`) | F3:719 `discard` | active |
| `OVERRIDE_OUTLINE_COLOR` | F1:190, outline copy only | F3:389 `fd.col.a *= _OutlineColor.a;` | active, outline alpha only |
| `OVERRIDE_ALPHAMASK` | F1:196 and F1:363, `LIL_FEATURE_ALPHAMASK && LIL_RENDER != 0` | F3:471-474 four `_AlphaMaskMode` writes | active |
| `OVERRIDE_DISSOLVE` | F1:208 and F1:375, as the dissolve rows above | F3:490, F3:508, by reference | active, runtime-conditional |
| `OVERRIDE_DITHER` | F1:222 and F1:389, `LIL_FEATURE_DITHER && LIL_RENDER == 1` | F3:538 threshold quantize, F3:2023 distance-fade alpha | active when the feature is defined |
| `OVERRIDE_DISTANCE_FADE` | F1:594, `LIL_FEATURE_DISTANCE_FADE` | F3:2048 alpha write sits under `#elif LIL_RENDER == 2`; cutout keeps the rgb-only path F3:2049-2051 | no alpha write |
| `OVERRIDE_DEPTH_FADE` | F1:243 and F1:419, `LIL_RENDER == 2` | F3:1995 | preprocessed out |
| `LIL_PREMULTIPLY` | F1:260 and F1:502 | F3:554-555 expands empty for `LIL_RENDER != 2` | reads alpha, writes nothing |
| fur AO block | F1:424-432, `LIL_FUR && LIL_RENDER == 1` | fur shaders only | not compiled for cutout |

An exhaustive grep of the file confirms this is the complete set of
`fd.col.a` writes, `clip(` calls, and `discard` statements. `[SOURCE]`

### 2.2 The cutout pass asset `[SOURCE]`

`Shader/ltspass_cutout.shader` defines `#define LIL_RENDER 1` exactly once,
shader-wide inside the top-level `HLSLINCLUDE` (`:639`). The file has no
`#undef` and no second definition of `LIL_RENDER`. Per-pass defines are
`LIL_PASS_FORWARD` (`:793, :841`), `LIL_OUTLINE` (`:845, :946, :1030`),
`LIL_PASS_FORWARDADD` (`:894, :942`), `LIL_PASS_SHADOWCASTER` (`:986,
:1026`), and `LIL_PASS_META` (`:1054`). All four forward passes include
`Includes/lil_pass_forward.hlsl` (`:801, :850, :902, :951`), which dispatches
to `lil_pass_forward_normal.hlsl` for the non-Lite family
(`lil_pass_forward.hlsl:7-12`).

Verdict: no define in `ltspass_cutout.shader` changes which alpha block a
regular cutout material executes in the forward base pass. `LIL_OUTLINE`
selects between the two copies, and both copies carry the same
`LIL_RENDER == 1` equation. `LIL_FEATURE_DITHER` (`:681`) only adds the
`if(!_UseDither)` wrapper around the transform line. One caveat ties to
section 3: which `LIL_FEATURE_*` defines an installed cutout asset carries
is project state, because the generator rewrites the asset. The alpha-block
choice itself never depends on that state. `[SOURCE]` `[INFERENCE]`

### 2.3 Reconciliation with the repository citations `[FINDING]`

- F0 section 6 item 10 and section 7.2 quote `clip(fd.col.a - _Cutoff)` at
  `lil_pass_forward_normal.hlsl:236` as the regular cutout equation. At the
  pin, `:236` is the outline copy's `LIL_RENDER == 2` transparent clip. The
  citation is wrong twice over for a cutout claim: wrong region and wrong
  render mode. F0 needs a correction of both lines.
- B2 already quotes the coverage transform at `:402-403` as the forward
  cutout equation and puts the shadow-caster plain clip in
  `lil_common_frag_alpha.hlsl:99`. That matches the pin exactly. B2 needs no
  change.
- The mapping note section 5 phrases the conflict as "F0/B2 quote the
  regular cutout equation as `clip(...)`". Only F0 does. B2 does not. The
  conflict was narrower than the mapping note recorded.
- The opaque-conversion note section 5 restates render-mode facts but never
  states a cutout equation. It needs no change.
- Production comments match the pin.
  `LilToonCutoutMaterialSemantics` says the triangle coverage is the cutout
  transform of the sampled alpha and never states a plain clip. The
  transparent semantics comments say the transparent forward site is a
  plain clip, not the cutout coverage transform, which matches `:411`
  against `:402-403`. No production doc comment needs a change.

### 2.4 Verdict

Settled on 2026-09-30, source-only.

1. The regular cutout family (`Hidden/lilToonCutout`, `LIL_RENDER 1`) runs
   this equation in its FORWARD pass, at
   `lil_pass_forward_normal.hlsl:402-403`:

   `fd.col.a = saturate((fd.col.a - _Cutoff) / max(fwidth(fd.col.a), 0.0001) + 0.5);`
   then `if(fd.col.a == 0) discard;`

   With the asset's `LIL_FEATURE_DITHER` the transform line sits under
   `if(!_UseDither)`. The discard is unconditional. `[SOURCE]`

2. A future Multi mode-1 material compiles the same block from the same
   file at the same effective `LIL_RENDER 1`. The terminal include chain is
   shared (mapping note section 5), and the Multi keyword remap derives
   `LIL_RENDER 1` from the cutout keyword (`lil_replace_keywords.hlsl`
   remap table, `:13-14`). The equation is identical. The only textual
   difference is feature-carrier state: in Multi the dither feature define
   is keyword-carried instead of baked into the asset. `[SOURCE]`
   `[INFERENCE]`

3. Yes, the screen-derivative term appears in the non-outline path, at
   `:402`. The outline copy at `:232-233` applies the same equation to
   outline alpha. `[SOURCE]`

## 3. Question 2: do the Multi containers regenerate under the settings apply

### 3.1 The regeneration path `[SOURCE]`

Two writers rewrite `.shader` assets. All cites are
`Editor/lilToonSetting.cs` unless noted.

- Writer 1, the unconditional overload `ApplyShaderSetting(shaderSetting,
  reportTitle)` (`:488-511`). It enumerates every `.lilinternal` under
  `BaseShaderResources` (`:495`) and writes
  `Shader/<basename>.shader` through
  `lilShaderContainer.UnpackContainer` (`:497-498`). There is no name
  filter. Every template in the folder regenerates.
- Writer 2, the selected overload `ApplyShaderSetting(shaderSetting,
  reportTitle, shaders, doOptimize)` (`:513-548`). For each shader it maps
  the asset path to `BaseShaderResources/<basename>.lilinternal` and
  rewrites the asset (`:533-535`). The only skips are empty paths and
  `.lilcontainer` customs (`:529-531`). Multi assets are plain `.shader`
  files, and the shader lists admit them because
  `lilMaterialUtils.CheckShaderIslilToon` matches any name containing
  `lilToon` (`Editor/lilMaterialUtils.cs:723-726`).
- The template is not stored shader text.
  `BaseShaderResources/ltsmulti.lilinternal` is a short descriptor declaring
  `Shader "_lil/lilToonMulti"` with `lilProperties` and `lilSubShader*`
  directives. `lilShaderContainer.UnpackContainer`
  (`Editor/lilShaderContainerImporter.cs:192-324`) expands it by splicing
  `CustomShaderResources/<Section>/<RP>/*.lilblock` files and substituting
  `*TOKEN*` markers (`:107-125`, `:263-307`).
- Templates exist for all five Multi containers at the pin:
  `ltsmulti`, `ltsmulti_o`, `ltsmulti_ref`, `ltsmulti_fur`,
  `ltsmulti_gem`. Both writers therefore rewrite all five Multi
  `.shader` assets. `[SOURCE]`

The build chain runs Writer 2. `SetShaderSettingBeforeBuild(materials,
clips)` (`:897-949`) turns every feature flag off
(`TurnOffAllShaderSetting`, `:268-376`), scans materials and clips, applies
the result over the scanned shader list with `doOptimize` true (`:939`),
and `SetShaderSettingAfterBuild` (`:984-1013`) restores the all-on state
over the same list (`:1004-1005`). A project-wide overload
(`:961-982`) reaches the same writers through
`ApplyShaderSettingOptimized` (`:822-842`). Every one of these cycles
rewrites the Multi assets too, twice per build: once with features off,
once restored. `[SOURCE]`

### 3.2 The regions that vary per project `[SOURCE]`

| Region | Token | Content for Multi assets |
|---|---|---|
| feature block | `*LIL_SHADER_SETTING*` | `LIL_FEATURE_*` define lines from the live setting. The scan that sets them skips Multi and Lite materials (`:1019-1021`), so this block reflects non-Multi material and animation state only |
| Multi block | `*LIL_SHADER_SETTING_MULTI*` | only `LIL_OPTIMIZE_*` and integration defines (`BuildShaderSettingStringMulti`, `:550-566`, no-arg forms `:723-743`) |
| SRP version | `*LIL_SRP_VERSION*` | current SRP version triplet, empty for the built-in pipeline |
| variant pruning | `skip_variants` pragmas | per-project sets (shadows, lightmaps, decals, probe volumes, and more) |
| pass naming | `*LIL_LIGHTMODE_*`, light-mode fields | light-mode names from the setting lock |
| misc | `("Version", Int) = 0`, multi_compile sets | current version constant, Unity-dependent compile sets |

At build time the optimizer additionally rewrites
`Shader/Includes/lil_common_input_opt.hlsl` (`lilOptimizer.OptimizeInputHLSL`
called at `:924`) and restores it after the build. That file sits in the
include tree of every family, Multi included.

### 3.3 The Multi exclusions, and what they do not exclude `[SOURCE]`

- The per-material feature scan skips Multi:
  `Editor/lilToonSetting.cs:1019-1021`, verbatim:
  `var shaderName = material.shader.name;` /
  `if(lilShaderUtils.IsLiteShaderName(shaderName) || lilShaderUtils.IsMultiShaderName(shaderName)) return;`
  (`IsMultiShaderName`, `Editor/lilShaderUtils.cs:184-187`).
- `Shader/Includes/lil_replace_keywords.hlsl:6` is
  `#define LIL_IGNORE_SHADERSETTING`, unconditionally, in a file the Multi
  shaders include.
- Its consumer at `Shader/Includes/lil_common.hlsl:19-21`:
  `#if defined(LIL_LITE) || defined(LIL_MULTI) || defined(LIL_IGNORE_SHADERSETTING)`
  then `#define LIL_OPTIMIZE_APPLY_SHADOW_FA`. From `:27` on, the
  `LIL_MULTI` branch forces the `_Use*` feature toggles to literal true.

`[INFERENCE]` These exclusions decouple the compiled feature set from the
setting text at the HLSL layer. They do not exempt the Multi `.shader`
files from the file-level rewrite. Both facts hold at once: the editor
rewrites the Multi assets on every apply and every build, and the renderer
ignores whatever feature defines that rewrite leaves, because the Multi
include chain derives its feature state from keywords and hard defines.

### 3.4 Fit with the repository generator-shape work `[SOURCE]`

The attestation comment in
`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
(profile section) states the existing discipline: hash after canonicalizing
exactly the regions the generator is proven to vary, digest the whole
include tree, read the render mode from the live pass. The generator-shape
spec of 2026-09-07 proves the variation kinds for the regular assets and
names the same token machinery. The Multi containers add one new varying
region, the `*LIL_SHADER_SETTING_MULTI*` block, whose token domain is the
closed `LIL_OPTIMIZE_*` and integration define set. The same
canonicalization shape extends to the Multi containers with that one
addition.

One prior repo fact needs care in any future pin: the committed `.shader`
bytes at the vendor tag are stale relative to the tag's own generator, so a
Multi digest pin must be measured from an installed shape, exactly like the
existing profiles.

### 3.5 Verdict

Settled on 2026-09-30, source-only. The Multi containers regenerate. All
five `.shader` assets are inside both writer loops and inside the build-time
apply and restore cycles. The regions that vary per project are the token
table in section 3.2. No empirical experiment is needed, and none ran.

## 4. Question 3: corpus population on the specialized Multi containers

`[MEASURED 2026-09-30]` Read-only census against the Census Lab editor
instance. Identity was verified from the editor itself before the scan: the
editor reported the Census Lab project as its active project, outside this
repository, with the census corpus folders present. Two instances were
reachable. The dev editor instance of this repository was never targeted.

Scan: one asset-database query over all materials, grouped by exact shader
name, plus the `_TransparentMode` float value per material on a Multi
container, rounded to the nearest integer. Counts only. No paths, no
avatars, no renderers, no private names left the editor.

| Shader name | Materials | `_TransparentMode` distribution |
|---|---|---|
| `_lil/lilToonMulti` | 127 | mode 0: 127 |
| `Hidden/lilToonMultiOutline` | 9 | mode 0: 9 |
| `Hidden/lilToonMultiRefraction` | 0 | none |
| `Hidden/lilToonMultiFur` | 0 | none |
| `Hidden/lilToonMultiGem` | 0 | none |

2292 materials were scanned. Every material found on a Multi container
exposed the `_TransparentMode` property, and none had a missing shader.
The totals reproduce the 2026-09-30 scan recorded in the companion notes.

Verdict: settled on 2026-09-30. The three specialized containers hold no
corpus population at all. Both populated containers stay mode 0 only. This
closes mapping note section 9 item 3, counts only, and reconfirms that the
observed Multi corpus maps onto the two attested opaque identities.

## 5. Findings left for later correction

Nothing outside this note was changed. Three records need later attention.

1. `[FINDING]` F0 (`2026-08-30-liltoon-family-applicability.md`) section 6
   item 10 and section 7.2 cite `clip(fd.col.a - _Cutoff)` at
   `lil_pass_forward_normal.hlsl:236` as the regular cutout equation. The
   correct forward cutout equation is the coverage transform plus discard at
   `:402-403`. Line `:236` is the outline copy's transparent clip. F0
   section 7.3 cites `:232-234` for the outline cutout path, which is
   correct.
2. `[FINDING]` The mapping note section 5 attributes the plain-clip quote to
   F0 and B2. Only F0 carries it. B2 already quotes the transform at
   `:402-403`.
3. `[FINDING]` The mapping note section 7 item 6 asks whether the Multi
   assets regenerate. They do. Section 3 of this note is the answer to that
   open item. The mapping note section 9 items 1 and 2 and the outline
   note's open alpha question are likewise closed by sections 2 and 3.

No production doc comment, theorem premise, or test needs a change from
this investigation. `LilToonCutoutMaterialSemantics` states the cutout
transform without the wrong equation, and its `MaxProvableCutoff` premise
survives: for alpha identically 1 over the sampled domain, the transform
saturates to 1 for any finite cutoff, with the derivative term floored at
its constant. `[SOURCE]` `[INFERENCE]`

## 6. Verdicts

1. Question 1: settled. The non-outline forward pass at `LIL_RENDER 1`
   executes the screen-derivative coverage transform plus discard at
   `lil_pass_forward_normal.hlsl:402-403`. The outline copy executes the
   same equation on outline alpha at `:232-233`. The F0 line-236 citation
   named the outline copy's transparent clip. A future Multi mode-1 proof
   uses the same equation with keyword-carried feature state.
2. Question 2: settled. The Multi containers regenerate. Both
   `ApplyShaderSetting` overloads and the per-build apply and restore
   cycles rewrite all five Multi `.shader` assets from their
   `BaseShaderResources/*.lilinternal` templates. The varying regions are
   the feature block, the `LIL_OPTIMIZE_*` Multi block, the SRP version,
   the skip-variant sets, the light-mode names, and the version constant.
   `LIL_IGNORE_SHADERSETTING` decouples compiled behavior from those bytes.
   It does not stop the rewrite. A future Multi digest pin reuses the
   generator-shape canonicalization with the Multi block added.
3. Question 3: settled. Zero corpus materials sit on the refraction, fur,
   and gem containers as of 2026-09-30. The base container holds 127
   materials and the outline container 9, all at `_TransparentMode` 0.
