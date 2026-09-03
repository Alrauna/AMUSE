# lilToon Material Semantics Adapter Design

**Date:** 2026-08-17 (amended 2026-08-18)

**Status:** Amended three times. Task 0 validated Region A, the R2 slot, and R3 by measurement.

**Branch:** `feat/liltoon-semantics-adapter` (base `b53bb17`)

## Amendment summary (third revision, 2026-08-18)

This revision responds to the Task 0 result (13 of 14 assertions passed, one failed). It also responds to the follow-up review of the new R2 and R3 wording.

A. **Region A is unchanged.** Task 0 validated it by measurement. Run lengths are `[0, 102]` default and `[0, 90]` stripped. `LIL_RENDER` always sits in an empty block-0 region.

B. **Region B is removed** as a general concept. It ended at `#define LIL_PASS_FORWARD`. It therefore missed the one variation it existed to cover.

C. **R2 becomes one exact substitution slot**, traced through the pinned BRP template and generator. Drop a `skip_variants` line only if three conditions hold. The preceding line must be exactly `#define LIL_PASS_FORWARD`. The line must carry exactly one keyword. That keyword must be exactly `SHADOW_VERY_HIGH`. This closed, generator-produced domain comes from `GetSkipVariantsShadows()` plus the dedup pass.

D. **Blank-line normalization is removed.** Task 0 disproved its premise. The empty marker expansion leaves no indentation-only line.

E. Constant `skip_variants` expansions (decals, add-light-shadows, probe volumes, AO) stay hashed.

F. The `skip_variants` deduplication pass in `UnpackContainer` is now documented. It determines the possible expansions at the slot.

G. **The project root is an explicit canonicalization input.** Live evidence derives it from `Directory.GetParent(Application.dataPath)`. Resolution never uses the process working directory.

H. **Attested include identity is exact ordinal.** `StringComparer.OrdinalIgnoreCase` is removed. A casing difference refuses conservatively. No filesystem detection restores permissive matching.

## Amendment summary (second revision, 2026-08-18)

This revision responds to review of the first amendment. Changes:

A. R1 and R2 now cover only two structurally located regions. These are the `HLSLINCLUDE` setting run and the `HLSLPROGRAM` pragma prologue. The rules no longer drop same-shaped lines anywhere in the file. A `LIL_FEATURE_*` define or a `skip_variants` pragma outside those regions stays hashed. As a side effect, the four constant-expansion `skip_variants` lines are now hashed, not dropped.

B. R3 normalizes an include path only when the path is proven to resolve inside the attested `Includes/` tree. The relative path within the tree is preserved. Basename matching is gone. A redirected include no longer canonicalizes to the trusted include.

C. The digest constants are removed. The generated shaders committed to the lilToon repository are stale relative to the generator of their own tag. The pins must therefore come from a measured real install.

## Amendment summary (first revision, 2026-08-18)

This revision responded to review of the 2026-08-17 draft. Changes:

1. The five proven shared Unity texture facts are retained and approved. `IsHdrColorImport` is **removed** from the shared class.
2. The HDR/range proof is reframed **positively**: prove the sampled color is in `[0,1]`. The proof is implemented lilToon-locally over an allow-list. The `graphicsFormat.ToString()` substring heuristic is deleted. That heuristic was fail-open.
3. `_lilToonVersion` is compared as an exact `float`. `Mathf.RoundToInt` is removed.
4. Generated-source attestation is rebuilt around two digests: a **canonical-remainder digest** and a whole-directory include digest. `LIL_RENDER == 0` is now proven from the *current* resolved pass. The previously pinned ten-file include list was **incomplete** and is replaced.
5. The 65-entry family taxonomy is demoted from production to research evidence. Production recognizes one target.
6. Per-task commits are removed from the plan. Git finalization is a separate gate.

Everything else from the accepted 2026-08-17 design is preserved.

## Problem statement

AMUSE has one real shader frontend (Poiyomi), one normalized semantic IR (`MaterialSemantics`), and one shader-independent consumer (`AlphaSemanticsResolver`). No test shows whether that boundary is a genuine architecture or an accident of a single producer.

This milestone designs a second, independent first-party frontend for lilToon. The purpose is dual. It adds real lilToon support. It also produces evidence. The evidence shows which parts of the Poiyomi implementation were shader-specific. It shows which parts were generic Unity host evidence. It also tests whether the semantic vocabulary survives contact with a materially different shader.

## Goals

- Prove BaseColor, Alpha, Emission, and Normal exactly for one precisely delimited lilToon configuration. Return `Unknown` with a scoped diagnostic everywhere else.
- Construct the existing `MaterialSemantics` types without modifying the semantic core.
- Establish an attestation model that fails closed against per-project lilToon shader regeneration, including hand edits to generated assets.
- Classify every apparently reusable concern from the Poiyomi frontend. Classes: semantic-core, generic host evidence, shader-family knowledge, shader-specific interpretation. The evidence comes from both implementations.
- Record semantic-core pressure without acting on it.

## Non-goals

Shader conversion, feature transplantation, a feature or data-flow graph, HLSL static analysis, a schema language or DSL, a public adapter API, an adapter registry or factory, third-party plugin discovery, an NDMF build pass, animation or material-swap analysis, a render-state model, atlasing, material combining, texture baking, global planning, and end-to-end avatar optimization are all out of scope. None is scaffolded.

Interpreting any lilToon variant other than the base opaque target is also a non-goal. Freezing the researched family taxonomy into production code is also a non-goal.

## Authoritative research basis

Every behavioral claim below was traced against pinned upstream source. No claim was inferred from property names or documentation.

| Source | Pinned evidence used |
| --- | --- |
| [lilToon 2.3.4 release](https://github.com/lilxyzw/lilToon/releases/tag/2.3.4) | Supported release and tag, published 2026-06-25. |
| [Tag commit `252fd8cfc46106d4967e95b3f2c788418502f227`](https://github.com/lilxyzw/lilToon/tree/252fd8cfc46106d4967e95b3f2c788418502f227) | Immutable revision used for all conclusions. |
| `Assets/lilToon/package.json` | Package `jp.lilxyzw.liltoon`, version `2.3.4`, MIT, Unity 2022.3. |
| `Assets/lilToon/Shader/lts.shader` and its `.meta` | Property contract, defaults, `UsePass` wiring, canonical asset GUID. |
| `Assets/lilToon/Shader/ltspass_opaque.shader` | `LIL_RENDER 0`, the compile-time `LIL_FEATURE_*` block, `_lilToonVersion = 45`, pass bodies. |
| `Assets/lilToon/BaseShaderResources/lts.lilinternal`, `ltspass_opaque.lilinternal` | Generation templates. `LIL_RENDER 0` originates in the package template, not in the project setting. |
| `Assets/lilToon/CustomShaderResources/BRP/Default.lilblock`, `DefaultUsePass.lilblock` | Fixed pass template and the exact positions of every generator substitution marker. |
| `Assets/lilToon/Editor/lilShaderContainerImporter.cs` | The complete substitution list applied during generation. `GetMultiCompileForward` (BRP branch ends `#define LIL_PASS_FORWARD`). `GetSkipVariants*` constant literals. The trailing `fix duplication of skip_variants` pass reduces each surviving line to its final unseen keyword. It deletes lines with none. |
| `Assets/lilToon/Editor/lilToonSetting.cs` | `ApplyShaderSetting` regenerates `Shader/*.shader`. The build preprocessor invokes it automatically. |
| `Assets/lilToon/Editor/lilConstants.cs` | `currentVersionValue = 45`. |
| `Assets/lilToon/Shader/Includes/*` (37 files) | Every traced equation, and the complete lilToon-owned include closure. |
| [Unity `TextureImporter.sRGBTexture`](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/TextureImporter-sRGBTexture.html) | Import-time sRGB interpretation. |
| [Unity `GraphicsFormat`](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Experimental.Rendering.GraphicsFormat.html) | Imported texture format, used for the positive range proof. |
| [Unity asset identity API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetDatabase.TryGetGUIDAndLocalFileIdentifier.html) | Stable GUID plus local file identifier. |
| [Unity package lookup API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PackageManager.PackageInfo.FindForAssetPath.html) | Installed package name and version. |

## Three structural differences from Poiyomi

### 1. lilToon is a shader family whose semantics live outside the shader asset

`lts.shader` is ~53 KB of `Properties` plus four `UsePass` directives into `Hidden/ltspass_opaque`. It contains no equations. All behavior lives in 37 shared include files reached through the pass shader. Rendering mode is a compile-time `LIL_RENDER` constant, not a material property.

### 2. The `.shader` files are generated artifacts, regenerated per project

`ApplyShaderSetting` writes every `Shader/*.shader`. The inputs are a `.lilinternal` template, `.lilblock` fragments, and the project `ShaderSetting`. The lilToon build preprocessor invokes it automatically. Two projects on the same release legitimately hold byte-different shader assets. Hashing the whole file would refuse nearly every real project.

### 3. Compile-time feature stripping is not monotone in the direction that matters

Stripping is safe for features AMUSE requires **off**. `_UseParallax = 0` behaves identically whether or not `LIL_FEATURE_PARALLAX` is compiled in.

Stripping is **not** safe for features AMUSE actively **claims**. Suppose a project strips `LIL_FEATURE_BumpMap`. The material has `_UseBumpMap = 1` and an assigned map. The shader then ignores the map entirely. But a frontend that reads only material properties would report `TangentSpaceNormalMap`. That is a false positive.

The same applies to `LIL_FEATURE_NORMAL_1ST`, `LIL_FEATURE_EMISSION_1ST`, and `LIL_FEATURE_EmissionMap`. `LIL_FEATURE_MainTex` is exempt: `lil_common_input.hlsl` force-defines it.

## Supported target

Production recognizes exactly one shader. Everything else is refused as unsupported.

- Shader name `lilToon`, asset GUID `df12117ecd77c31469c224178886498e` (`Assets/lilToon/Shader/lts.shader`).
- Resolved pass shader `Hidden/ltspass_opaque`, GUID `61b4f98a5d78b4a4a9d89180fac793fc`, `LIL_RENDER 0`.
- Package `jp.lilxyzw.liltoon` version `2.3.4` when installed as a package.
- Material `_lilToonVersion` exactly `45f`.

### Why no family-recognition mechanism is required

Correctness of the base opaque claim needs only one answer: is this exactly the supported shader? A 65-entry table would improve diagnostic wording for other variants. It would do nothing else. It stays out of production until a second variant class receives real semantic support.

The design keeps the GUID check beside the name check. A renamed or relocated copy of the asset could resolve a different `UsePass` target. The GUID check costs one ordinal comparison and fails closed.

The researched taxonomy is preserved below as evidence, not as code.

## Attestation strategy

Six checks run in conjunction. The first failure yields one material-scoped diagnostic and an all-`Unknown` result.

1. **Shader identity.** `shader.name == "lilToon"` and the asset GUID matches. Failure: `UnsupportedShader`.
2. **Material shader-format stamp.** `_lilToonVersion` exists, is finite, and is **exactly** `45f`. Failure: `UnsupportedVersion`.
3. **Package identity.** When `PackageInfo.FindForAssetPath` resolves, the name must be `jp.lilxyzw.liltoon` and the version `2.3.4`. When it does not resolve, the install is a legacy `Assets/lilToon` install. The check is then skipped, like the conditional package check in the Poiyomi frontend.
4. **Include-tree digest.** Every file under the `Includes/` directory of the resolved shader is hashed. The `(relative path, hash)` pairs are sorted ordinally. The digest of that listing must match the pin. Failure: `ModifiedShaderSource`. An unreadable directory yields `MissingSourceEvidence`.
5. **Generated-asset canonical digests.** The material shader and the resolved pass shader are each canonicalized (below) and hashed against their pins. Failure: `ModifiedShaderSource`. An include directive that cannot be proven to target the attested tree stays unnormalized. It therefore lands here rather than passing.
6. **Current render mode.** The resolved pass must contain exactly one `#define LIL_RENDER <value>` line. The value must be `0`. Failure: `UnsupportedShaderVariant`.

Text normalization for every hash matches the existing Poiyomi rule exactly. Strip an optional leading UTF-8 BOM. Convert CRLF and lone CR to LF. Hash the UTF-8 bytes. Use lowercase hex.

### Canonicalization rules for generated assets

Three literal line rules. Each is **bounded to the structural position the generator is proven to control**. A line of the same shape anywhere else stays hashed. No grammar, no macro expansion, no conditional evaluation.

The line kinds referenced below:

- **D1** — a valueless `#define <IDENT>`. `IDENT` starts with `LIL_FEATURE_` or `LIL_OPTIMIZE_`, or equals `LIL_INPUT_OPTIMIZED`. A define **with** a value is never D1.
- **D2** — `#pragma skip_variants <one or more tokens>`.

There is no whitespace-line rule. Task 0 disproved the assumption that motivated one. See R2 below.

#### R1 — the setting region

For each line whose trimmed text is exactly `HLSLINCLUDE`, region A is the maximal contiguous run of immediately following lines. Every line in the run must be D1 or D2. The run ends at the first line that is neither. A blank line does **not** extend the run. Nothing can therefore bridge into unrelated content.

Inside region A, D1 and D2 lines are dropped. This is exactly the `*LIL_SHADER_SETTING*` substitution. `lilToonSetting.BuildShaderSettingString(setting, isFile: false)` emits, in order, only conditional valueless `LIL_FEATURE_*` / `LIL_OPTIMIZE_*` defines. It then emits up to three conditional `lil_skip_variants_{reflections,addlight,lightmaps}` markers. It emits an optional `LIL_INPUT_OPTIMIZED` last. It emits no blank lines in this mode and no valued define.

The boundary is self-enforcing in the one place that matters most. The pass shader has **two** `HLSLINCLUDE` blocks. The Shader-scope block comes from `ltspass_opaque.lilinternal`. Its first line is `#define LIL_RENDER 0`. The SubShader-scope block comes from `Default.lilblock`. Its first line is the setting substitution.

`LIL_RENDER 0` is a *valued* define, so it is not D1. The Shader-scope region A is therefore empty. `LIL_RENDER` sits structurally outside canonicalization. No scope tracking is required for that.

Task 0 measured region A run lengths on two installs. The default install measured `[0, 102]`. The stripped install measured `[0, 90]`. `LIL_RENDER` sat in block 0 both times. Region A is validated by measurement and unchanged.

The four `skip_variants` lines that follow `#pragma fragmentoption` come from the fixed `lil_skip_variants_{decals,addlightshadows,probevolumes,ao}` markers. Their expansions `GetSkipVariants*()` are **constant string literals**. They depend on no setting, pipeline, or Unity version. The lines lie outside region A and are **hashed**.

#### R2 — the shadow skip-variant substitution slot

Region B, the general "program pragma prologue" concept, is **removed**. Task 0 showed that it was both wrong and unnecessary. It ended at a `#define`. It thereby missed the one variation it existed to cover. Its blank-line clause addressed a whitespace artifact that does not occur. One exact slot replaces R2.

**Where the slot is.** In `CustomShaderResources/BRP/Default.lilblock` the shadow marker always occupies the line **immediately after** `#pragma lil_multi_compile_forward`:

```
#pragma vertex vert
#pragma fragment frag
#pragma lil_multi_compile_forward
#pragma lil_skip_variants_base_shadows      (line 49, base forward pass)
...
#pragma lil_multi_compile_forward
#pragma lil_skip_variants_outline_shadows   (line 97, outline forward pass)
```

`GetMultiCompileForward` on the BRP branch expands to a fixed list. The **final line of that list is always `#define LIL_PASS_FORWARD`**. An optional `#define LIL_FEATURE_LTCGI` precedes it when LTCGI is installed. It never follows it. The slot is therefore identified after generation by one invariant: **the immediately preceding line is exactly `#define LIL_PASS_FORWARD`**.

Task 0 confirms that this invariant discriminates. Across both generated states, every other `skip_variants` line is preceded by a `#define LIL_OPTIMIZE_*` (region A), a blank line, or another `skip_variants` line. The only line preceded by `#define LIL_PASS_FORWARD` was the base-shadow expansion.

**Every possible expansion in 2.3.4.** `ApplyShaderSetting` first runs `sb.Replace(SKIP_VARIANTS_BASE_SHADOWS, useBaseShadow ? "" : SKIP_VARIANTS_SHADOWS)`. It then runs `sb.Replace(SKIP_VARIANTS_SHADOWS, GetSkipVariantsShadows())`. The return value of `GetSkipVariantsShadows()` is the fixed literal

```
#pragma skip_variants SHADOWS_SCREEN _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
_MAIN_LIGHT_SHADOWS_SCREEN _ADDITIONAL_LIGHT_SHADOWS SCREEN_SPACE_SHADOWS_ON
SHADOW_LOW SHADOW_MEDIUM SHADOW_HIGH SHADOW_VERY_HIGH
```

(one line in the source, wrapped here). The result then passes through the `UnpackContainer` `fix duplication of skip_variants` pass:

```csharp
var match = Regex.Match(line, @"(^\s+#pragma\s+skip_variants\s+)(\w+\s*)*");
...
for (int i = 2; i < match.Groups.Count; i++)
    if (skips.Add(match.Groups[i].Value)) { temp.Append(...); isValid = true; }
if (isValid) sb.AppendLine(temp.ToString());
```

The pattern has two capture groups. `Groups.Count == 3` follows, and the loop examines **only `Groups[2]`**. In .NET, the `.Value` of a repeated group is its **last** capture. A surviving line is therefore rewritten to its **final keyword alone**. A line whose final keyword was consumed earlier in the file is **deleted entirely**.

The final keyword of `GetSkipVariantsShadows()` is the fixed literal `SHADOW_VERY_HIGH`. No other generator string ends in `SHADOW_VERY_HIGH`. Also, `skips` accumulates only final keywords. Nothing except a prior shadow line can therefore consume it.

**The generator-produced keyword domain at this slot is therefore the closed one-element set `{ SHADOW_VERY_HIGH }`.**

Measurement corroborates this. Every emitted `skip_variants` line in both Task 0 states is a single keyword. Each keyword equals the final keyword of its source literal. The observed keywords are `_MIXED_LIGHTING_SUBTRACTIVE`, `_REFLECTION_PROBE_BOX_PROJECTION`, `_DBUFFER_MRT3`, `_ADDITIONAL_LIGHT_SHADOWS`, `_SCREEN_SPACE_OCCLUSION`, and `SHADOW_VERY_HIGH` at the slot.

So the slot has exactly two generated forms:

| Condition | Generated at the slot |
| --- | --- |
| `useBaseShadow == true` | **no line at all**. The empty replacement is collapsed by the `"\r\n            \r\n" → "\r\n"` normalization that the generator runs. No indentation-only residue remains. |
| `useBaseShadow == false` | **exactly** `#pragma skip_variants SHADOW_VERY_HIGH`, or no line at all if that keyword was consumed earlier |

**Selecting condition.** `useBaseShadow = (LIL_FEATURE_SHADOW && LIL_FEATURE_RECEIVE_SHADOW) || LIL_FEATURE_BACKLIGHT`, from `lilToonSetting.BuildShaderSettingString(bool, ref bool, ref bool)`. The outline slot uses `useOutlineShadow = LIL_FEATURE_OUTLINE_RECEIVE_SHADOW`. Both are ordinary project settings, so both must be supported.

**Is the output exclusively variant-selection metadata?** Yes. `#pragma skip_variants` removes shader *variants* from compilation. It defines no macro, changes no expression, and adds no include directive. It therefore cannot affect BaseColor, Alpha, Emission, or Normal. Dropping it from the digest cannot conceal a semantic change.

**The rule.** Drop a line only when **all three** hold:

1. The line immediately preceding it in the raw text is exactly `#define LIL_PASS_FORWARD`.
2. The line is a `#pragma skip_variants` directive carrying **exactly one** keyword.
3. The keyword belongs to the proven generator-produced shadow set. That set is exactly `SHADOW_VERY_HIGH`.

A `#pragma skip_variants` line anywhere else stays hashed. Examples: injected two lines later, inside a pass body, or after any other define. A multi-keyword line at the slot also stays hashed. So does a single-keyword line at the slot with any other keyword. The keyword condition is a closed literal, not a pattern. It is not a variant system and must not become one.

This correctly covers the outline slot as well as the base slot. Both are the same generator-controlled substitution at the same structural position. Both are project-variable. Both emit only variant metadata. `ltspass_opaque` contains two `#define LIL_PASS_FORWARD` lines for exactly this reason.

The absent form leaves no line at all. Dropping the present form therefore makes the two states textually identical. No blank-line normalization is needed. That is why R2 needs no whitespace clause and why the previous one was unjustified.

The custom-shader injection markers (`*LIL_SUBSHADER_INSERT*`, `*LIL_SUBSHADER_INSERT_POST*`, `*LIL_INSERT_PASS_PRE*`, `*LIL_INSERT_PASS_POST*`) are unaffected by R1 and R2. They remain fully hashed.

#### R3 — include path identity

R3 applies **only** to a line whose trimmed text matches `(//)?#include "<path>"` and nothing else. A quoted string anywhere else is never rewritten. Examples: `Fallback`, `Tags`, `CustomEditor`, a string inside code. The optional `//` is required. lilToon runs `sb.Replace("\"Includes", …)` as a blind textual substitution. It also rewrites the commented include directives in `lts.shader`.

A path is normalized **only when it is proven to resolve to a file inside the attested `Includes/` tree**:

1. Build two candidate resolutions of `<path>`. One is relative to the directory of the shader asset being canonicalized. One is relative to the project root.
2. Accept only if at least one candidate full path equals the full path of a file enumerated during include-tree digesting. If both candidates resolve to attested files and those files differ, the path is **ambiguous**. An ambiguous path is not normalized.
3. The replacement is `Includes/<relative path of the resolved file within the attested tree>`. Use `/` separators and **preserve subdirectories**. Example: `Includes/VRC Light Volumes/LightVolumes.cginc`.

**The project root is an explicit input, not the process working directory.**

Canonicalization takes it as a parameter, which is also the test seam. Live evidence gathering derives it once from `Directory.GetParent(Application.dataPath).FullName`. The scratch and unit canonicalizers pass their known root directly. Resolution against `"."` would silently couple attestation to the current directory of the Editor process. That directory is not a property of the shader being attested.

**Path identity is exact ordinal.** The trusted map from resolved full path to tree-relative path is keyed with `StringComparer.Ordinal`. A casing difference therefore fails to resolve. The line stays unnormalized. The digest then refuses. This holds even on a case-insensitive filesystem where the two paths name the same file. That false negative is accepted deliberately.

Case-insensitive matching would let `Includes/LIL_COMMON.HLSL` assume the identity of an attested `Includes/lil_common.hlsl`. The design adds no platform or filesystem detection to recover the permissive behavior.

A path that resolves outside the attested tree, resolves to nothing, or is ambiguous stays **byte-identical**. It contributes its original text to the digest. That text cannot match the pin. The material therefore refuses.

This closes a redirect hole in the previous rule, which normalized on basename alone. Under the old rule, an edit from `#include "Includes/lil_common_frag.hlsl"` to `#include "Evil/lil_common_frag.hlsl"` produced the *same* canonical text as the trusted include. The hostile file had the same basename in another directory. The `Includes/` tree digest stayed untouched. The whole equation set could then be swapped with no detection. Under the new rule, the redirected path resolves outside the attested tree.

The path stays unnormalized. The digest mismatches.

Everything not covered by R1–R3 is hashed byte-for-byte. This covers pass bodies, tags, and blend and stencil state. It also covers every `#include` that is not proven-attested. It also covers every `#pragma` other than an in-region `skip_variants`. It also covers every valued `#define`.

#### Residual, and why it is not a divergence

Two residuals exist. Both are bounded. Neither is a soundness gap.

Inside region A, an injected valueless `LIL_FEATURE_*` line is dropped from the digest. The compile-time feature scan reads the whole file. AMUSE therefore observes exactly the feature set that the shader compiler observes. An injected feature define makes the feature genuinely compiled in. The AMUSE claim tracks it.

At the R2 slot, an injected `#pragma skip_variants` line placed immediately after a `#define LIL_PASS_FORWARD` is dropped. `skip_variants` is pure variant-selection metadata. It defines no macro, changes no expression, and adds no include directive. No such injection can therefore change any of the four traced equations. An injected line anywhere else stays hashed.

### Digest pins come from measurement, not from the repository

The three pins must come from a real generated install. They cannot be taken from the lilToon repository snapshot. The generated shaders committed there are **stale relative to the generator of their own tag**.

Evidence: `ltspass_opaque.shader` at tag `2.3.4` contains `#pragma skip_variants PROBE_VOLUMES_OFF PROBE_VOLUMES_L1 PROBE_VOLUMES_L2`. But `lilShaderContainerImporter.GetSkipVariantsProbeVolumes()` at that same tag returns the empty string. The pragma text survives only as a comment. A shader generated by the generator of that tag would carry a blank line there. The committed artifact therefore predates that change. It is not reproducible from the pinned source.

Task 0 executed this on 2026-08-18 against a scratch Unity 2022.3.22f1 project. The project used the `jp.lilxyzw.liltoon-2.3.4.zip` release artifact. Task 0 drove both states through lilToon itself via `ApplyShaderSetting`. The real generator confirmed the staleness directly. The probe-volume pragma disappeared. The file hash moved `bb5eaf4d…` → `efcb1fc6…`.

The measured pins are:

| Artifact | Canonical digest |
| --- | --- |
| `Shader/lts.shader` | `5206bec25e82db5f8009b27fcc5ba94d7c41113031d4b6b0a2c25ca324a9c704` |
| `Shader/ltspass_opaque.shader` | `6b6c30c1cbe546fe753bcdc77f547441e3f9114ee80e9591bde2b8e6e7e5eb14` |
| `Shader/Includes/**` (37 files) | `6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46` |

Default and stripped settings produce byte-identical canonical text. Line counts go `1068 → 966` and `1057 → 966`. Their raw pass hashes differ. Equal canonical text with different raw hashes is what the canonicalization exists to achieve.

If the default and stripped installs do not canonicalize identically, the region rules are incomplete. The milestone then stops. It does not widen the rules to fit the sample.

### Sufficiency argument

**A. Which regions of the resolved pass vary legitimately with ShaderSetting?**

The trace runs through `lilShaderContainerImporter.UnpackContainer` and `CustomShaderResources/BRP/Default.lilblock`. Only two regions vary. One is the `*LIL_SHADER_SETTING*` block. It expands to valueless `LIL_FEATURE_*` / `LIL_OPTIMIZE_*` defines plus one `#pragma skip_variants`.

The other is the `SKIP_VARIANTS_*` markers. They expand to further `#pragma skip_variants` lines. R1 and R2 remove both.

The remaining substitutions are fixed for this target and pipeline. They are: shader name, editor name, pass shader name, subshader tags, SRP version, lightmode names, and the `_lilToonVersion` default. R3 removes the install-path substitution.

Measured on the pinned expansion: the pass has 1071 lines. R1 and R2 remove 108 of them. **The only valued `#define` in the entire pass is `LIL_RENDER 0`**. It is retained. Of 113 valueless defines, 103 follow the shader settings and are removed. The other 10 (`LIL_PASS_FORWARD`, `LIL_PASS_FORWARDADD`, `LIL_PASS_SHADOWCASTER`, `LIL_PASS_META`, `LIL_OUTLINE`) are template-fixed pass markers and are retained.

**B. Can anything outside those regions affect the four claims?**

Yes. Pass bodies, includes, and `LIL_RENDER` all can. That is precisely why everything outside R1–R3 is hashed rather than reasoned about.

A useful consequence: the render pipeline needs no separate check. A URP or HDRP project generates the pass from a different `.lilblock`. That `.lilblock` has different lightmodes and a different pipeline include. The canonical digest therefore cannot match the BRP pin. Non-BRP projects refuse automatically.

**C. Are the pinned includes the complete transitive dependency closure?**

The 2026-08-17 draft pinned ten files and was **wrong**. A trace of the include graph from the pass shader showed the draft omitted at least:

- `lil_pass_forward.hlsl` (the forward dispatcher)
- `lil_pipeline_brp.hlsl`
- `openlit_core.hlsl`
- `lil_common_functions_thirdparty.hlsl` (included unconditionally by `lil_common_functions.hlsl`)
- `lil_vert_audiolink.hlsl` and `lil_vert_outline.hlsl` (both included unconditionally by `lil_common_vert.hlsl`)
- `VRC Light Volumes/LightVolumes.cginc`

Rather than repair the reachability analysis and risk missing another edge, the design now digests **every file in the `Includes/` directory**. That is 37 files, including the non-source `license.txt`. Directory enumeration removes reachability reasoning from the trusted computing base entirely. It also detects added files. A per-file list cannot detect those.

Four external include roots remain outside the closure. AMUSE cannot pin them: AudioLink (`com.llealloo.audiolink`), LTCGI (`at.pimaker.ltcgi`), VRC Light Volumes (`red.sim.lightvolumes`), and Unity itself (`ProbeVolume.hlsl` and the BRP `.cginc` headers). All four are reached only from lighting code. The four claims do not depend on lighting. BaseColor is captured at `fd.albedo` before any lighting block. Alpha is a compile-time literal. Normal is computed before lighting.

Emission couples to the outside only through AudioLink. The material gate already forces AudioLink off through `_UseAudioLink == 0`. This dependency situation is recorded as an explicit assumption. Re-check the assumption if a claim ever moves after the lighting stage.

**D. Can the generated pass change a claimed semantic while the GUID and include hashes stay the same?**

Yes, in three ways. All three are now detected:

| Attack | Detection |
| --- | --- |
| Edit `#define LIL_RENDER 0` to `2` | `LIL_RENDER` is a valued define. It survives canonicalization and sits inside the hashed remainder. Check 6 also reads it as an explicit fact. Two independent detections. |
| Delete a `#define LIL_FEATURE_BumpMap` | The scan reads it explicitly as compile-time feature evidence. Normal refuses. This is not a digest change. Feature defines are legitimately variable. The explicit read is what covers them. |
| Inject arbitrary HLSL at a custom-shader marker (`*LIL_SUBSHADER_INSERT*`, `*LIL_INSERT_PASS_PRE*`, …), or hand-edit a pass body | R1–R3 do not match injected text. It stays in the hashed remainder. The digest changes. |

A stale generated asset from an older lilToon is caught by the digest, by the include-tree digest, or by `_lilToonVersion`.

**Residual exposure.** Any variation not covered by R1–R3 changes the digest and refuses. That is fail-closed by construction. Unmodeled variation produces false negatives, never false positives. The cost: a legitimate variation that the rules do not model refuses valid materials. The blocking verification task exists to find such a variation.

### Compile-time feature evidence

Stripping is not monotone for claimed features. Three outputs therefore also require evidence that a specific symbol is defined in the resolved pass. The scan is a line scan for `#define <SYMBOL>` over a closed set. It evaluates no conditionals and parses no HLSL.

| Symbol | Required by | If absent |
| --- | --- | --- |
| `LIL_FEATURE_NORMAL_1ST` | Normal, only when `_UseBumpMap == 1` and a map is assigned | Normal → `Unknown` |
| `LIL_FEATURE_BumpMap` | same | Normal → `Unknown` |
| `LIL_FEATURE_EMISSION_1ST` | Emission, only when `_UseEmission == 1` | Emission → `Unknown` |
| `LIL_FEATURE_EmissionMap` | Emission, only when `_UseEmission == 1` and `_EmissionMap` is assigned | Emission → `Unknown` |

BaseColor and Alpha need no such evidence. The BaseColor strippable dependencies (`LIL_FEATURE_MAIN_TONE_CORRECTION`, `MAIN_GRADATION_MAP`, `MainColorAdjustMask`, `ANIMATE_MAIN_UV`) are all the identity at the parameter values BaseColor already requires. Stripped and unstripped forms therefore agree. `LIL_FEATURE_MainTex` cannot be stripped. Alpha is a compile-time constant.

## Common extraction rules

### Color space

Color outputs require `QualitySettings.activeColorSpace == ColorSpace.Linear`. Constants convert with `Color.linear`. Gamma projects keep color outputs `Unknown`.

### Exactness convention

"Exactly" means mathematical identity under exact real arithmetic on the traced expression. The semantic core and the Poiyomi frontend already use this convention. Floating-point rounding inside an identity transform is not a change of meaning. Example: `lilRotateUV(uv, 0)`, which computes `((uv - 0.5) * I) + 0.5`.

### Positive sampled-range proof

`lilToneCorrection` runs unconditionally on the main sample when compiled in. It is the identity at `_MainTexHSVG == (0,1,1,1)` **only while the sampled value stays within `[0,1]`**. At those parameters it computes `pow(abs(c), 1)`. It converts to HSV. It applies `h + 0`, `saturate(s * 1)`, and `saturate(v * 1)`. It converts back.

The RGB→HSV→RGB round trip is exact. The `saturate` calls are the identity only on the unit range. The proof is therefore framed positively:

> Can AMUSE prove that every effective sampled color value for this texture is finite and confined to `[0,1]`?

Only a texture whose imported `GraphicsFormat` is in a pinned allow-list returns success. The allow-list holds unsigned-normalized and sRGB formats. Every other format **refuses**. So does any texture whose format or importer cannot be read.

Signed-normalized, half, float, shared-exponent, and BC6H formats are not in the list and therefore refuse. So does any format Unity adds later. Nothing is clamped, approximated, or assumed bounded.

This is the inverse of the 2026-08-17 draft `IsHdrColorImport`. That check asked "does this look like HDR?" over `graphicsFormat.ToString()` substrings. It returned `false` for any format it did not recognize. A `false` result permitted a `Complete` claim. That heuristic was fail-open and is deleted.

The predicate stays inside the lilToon frontend. Its trigger is a lilToon-specific equation. It has exactly one concrete consumer. It moves to shared code only when a second frontend needs the identical contract.

### UV mapping and affine composition

The lilToon main UV is always UV0. There is no main-texture channel selector. The traced expression is

```
uvMain = lilCalcDoubleSideUV(uv0, facing, _ShiftBackfaceUV)
uvMain = lilCalcUV(uvMain, _MainTex_ST, _MainTex_ScrollRotate)
```

At `_ShiftBackfaceUV == 0` and `_MainTex_ScrollRotate == (0,0,0,0)`, it reduces to `uv0 * _MainTex_ST.xy + _MainTex_ST.zw`. That is exactly `UvMapping(0, scale, offset)`.

Secondary maps sample with `LIL_SAMPLE_2D_ST(tex, samp, fd.uvMain)`. Their ST therefore composes **on top of** the main transform. Poiyomi uses independent per-channel ST instead. Affine ∘ affine is affine. The closed `UvMapping` expresses it exactly:

```
scale  = _MainTex_ST.xy * _BumpMap_ST.xy
offset = _MainTex_ST.zw * _BumpMap_ST.xy + _BumpMap_ST.zw
```

A genuinely different composition rule that fits the existing vocabulary without extension is a positive result.

The emission map is the exception. `LIL_GET_EMITEX` applies `lilCalcUV` to a **selected** channel chosen by `_EmissionMap_UVMode`. It does not apply `lilCalcUV` to `uvMain`. Emission therefore uses direct uncomposed mapping.

### Sampler coupling

`sampler_MainTex` is the Unity auto sampler for `_MainTex`. Its state comes from that asset. lilToon samples `_BumpMap` with `sampler_MainTex`. It samples `_EmissionMap` with its own `sampler_EmissionMap`:

- BaseColor and Normal read sampler state from the **`_MainTex`** asset.
- Emission reads sampler state from the **`_EmissionMap`** asset.

The single "the `_MainTex` asset supplies every sampler" rule is Poiyomi-specific. The underlying question is identical and does transfer. The question: does this `Texture` carry supported sampler state?

Supported state is unchanged: `Point` or `Bilinear`, equal `Clamp`/`Repeat` wrap, no mipmaps, zero mip bias, anisotropy at most 1. The no-mipmap rule also neutralizes `LIL_SAMPLE_2D_POM`. That macro expands to `SampleGrad` when parallax-occlusion mapping is compiled in. It expands to a plain `Sample` otherwise. With no mip chain, both fetch level 0. The compile-time difference therefore cannot change the value.

## Supported semantic subset

All rules apply only after attestation succeeds. Each output is proven independently.

| Output | Complete when | Otherwise |
| --- | --- | --- |
| BaseColor | Constant `_Color`, or `_MainTex` sample optionally times `_Color` | `Unknown` |
| Alpha | Always `Constant(1)`, subject only to coverage gates | `Unknown` |
| Emission | `Constant(0)` when nothing emits; additive slot-1 color, optionally times an `_EmissionMap` sample | `Unknown` |
| Normal | `Unmodified`, or a canonical tangent-space `_BumpMap` | `Unknown` |

### BaseColor

The output models `fd.albedo`, assigned at `lil_pass_forward_normal.hlsl:443`. On `LIL_RENDER 0`, the alpha-mask, dissolve, dither, depth-fade, fur, and premultiply blocks are excluded at compile time. They need no material gate. The remaining writers are parallax, main, AudioLink, main-2nd, and main-3rd.

Exact-zero gates: `_Invisible`, `_ShiftBackfaceUV`, `_UseParallax`, `_UsePOM`, `_UseAudioLink`, `_UseMain2ndTex`, `_UseMain3rdTex`, `_MainGradationStrength`.

Exact-value gates: `_MainTex_ScrollRotate == (0,0,0,0)`, `_MainTexHSVG == (0,1,1,1)`, `_MainColorAdjustMask` unassigned.

Additional requirements:

- linear color space
- finite `_Color.rgb`
- when `_MainTex` is assigned: a supported sampler on the `_MainTex` asset and a stable identity
- when `_MainTex` is assigned: a provable sRGB/linear interpretation
- when `_MainTex` is assigned: a **positively proven** `[0,1]` sampled range

Value: `Constant(_Color.linear.rgb)` with no texture. Otherwise `Texture` or `TextureTimesConstant`.

### Alpha

`lil_pass_forward_normal.hlsl:394-396` assigns `fd.col.a = 1.0` unconditionally on `LIL_RENDER 0`, after every alpha-writing block. The subpass alpha path in `lil_common_frag_alpha.hlsl` sits entirely inside `#if LIL_RENDER > 0`. The opaque variant therefore never clips.

Alpha is therefore `Constant(1)` from attested shader identity. It is independent of `_Color.a`, `_MainTex` alpha, `_AlphaMaskMode`, `_Cutoff`, and `_UseDither`. Two coverage gates remain. They remove fragments rather than change the value. Gate one: `_Invisible == 0` (`lil_common_vert.hlsl:444` returns early). Gate two: `_UDIMDiscardCompile == 0` (`OVERRIDE_UDIMDISCARD` runs at `lil_pass_forward_normal.hlsl:155`, outside any `LIL_RENDER` guard).

This is the sharpest contrast in the milestone. Poiyomi reaches `Constant(1)` through a material property after eighteen gates. lilToon needs only two gates. It reaches the identical value from attested shader identity.

### Emission

`lil_common_frag.hlsl:1866` computes

```
emissionBlend = _EmissionBlend * lilCalcBlink(_EmissionBlink) * emissionColor.a
fd.col.rgb    = lilBlendColor(fd.col.rgb, emissionColor.rgb, emissionBlend, _EmissionBlendMode)
```

`lilBlendColor` returns `lerp(dst, outCol, srcA)`. Only mode `1` (Add) gives `outCol = dst + src`, hence `dst + a * src`. That is a true additive emission. Modes 0, 2, and 3 are not additive and are not representable. The default is `1`. `lilCalcBlink` returns exactly `1` when `_EmissionBlink.x == 0`.

Zero claim: `_UseEmission == 0` plus exactly-zero `_UseEmission2nd`, `_UseReflection`, `_UseMatCap`, `_UseMatCap2nd`, `_UseRim`, `_UseRimShade`, `_UseGlitter`, `_UseBacklight`, `_UseAudioLink`, `_DissolveParams.x`, and `_BackfaceColor.a == 0`. These gates apply to the non-zero claim too.

The non-zero claim also requires:

- `LIL_FEATURE_EMISSION_1ST`
- linear color space
- exactly-zero `_EmissionMainStrength`, `_EmissionFluorescence`, `_EmissionUseGrad`, `_AudioLink2Emission`, `_EmissionParallaxDepth`
- `_EmissionBlendMode == 1`
- `_EmissionBlink.x == 0`
- finite `_EmissionBlend` and `_EmissionColor`
- `_EmissionBlendMask` unassigned
- `_EmissionMap_ScrollRotate == (0,0,0,0)`

With `k = _EmissionBlend * _EmissionColor.a * _EmissionColor.linear.rgb`, no map gives `Constant(k)`. An assigned map also requires `LIL_FEATURE_EmissionMap` and `_EmissionMap_UVMode ∈ {0,1,2,3}` (mode `4` selects rim UV). It also requires a supported sampler on the `_EmissionMap` asset and a stable identity. It also requires a provable color interpretation and a **provably sampled alpha of exactly one**.

That last requirement is unavoidable. `emissionColor.a` multiplies the blend factor. An RGBA map therefore scales its own emission by its own alpha. The result is `rgb * a` of one sample. The closed vocabulary cannot express that.

### Normal

`_UseBumpMap == 0` yields `Unmodified`. `_UseBumpMap == 1` with `_BumpMap` unassigned also yields `Unmodified`, provably. The `"bump"` default is the Unity value `(0.5, 0.5, 1, 0.5)`. `lilUnpackNormalScale` maps it to exactly `(0,0,1)`.

An assigned map requires:

- `LIL_FEATURE_NORMAL_1ST` and `LIL_FEATURE_BumpMap`
- `_BumpScale == 1` exactly
- exactly-zero `_UseBump2ndMap`, `_UseAnisotropy`, `_UseParallax`, `_UsePOM`, `_ShiftBackfaceUV`
- `_MainTex_ScrollRotate == (0,0,0,0)`
- a finite composed `UvMapping`
- a supported sampler on the **`_MainTex`** asset
- a stable identity
- the canonical Unity normal-map import

The `_MainTex` sampler dependency is worth stating plainly. Whether a material Normal output is provable depends on the import settings of a *different* texture. An unassigned `_MainTex` leaves Normal `Unknown`, even with `_BumpMap` present. `_BumpScale` is `Range(-10,10)`, so negative and amplified normals are reachable. Both stay `Unknown`.

## Diagnostics

The shape matches the Poiyomi frontend: an output scope, a closed code, and a detail string. The enum is distinct. Diagnostics are data. The frontend never writes the Console.

`LilToonSemanticOutput`: `Material`, `BaseColor`, `Alpha`, `Emission`, `Normal`.

| Code | Raised for |
| --- | --- |
| `UnsupportedShader` | Not the supported lilToon target. |
| `UnsupportedShaderVariant` | Supported asset identity, but the current pass does not declare `LIL_RENDER 0`. |
| `UnsupportedVersion` | `_lilToonVersion` or package version mismatch. |
| `ModifiedShaderSource` | An include-tree or canonical-asset digest mismatch. |
| `MissingSourceEvidence` | An include directory, shader asset, or pass asset is unreadable, or package evidence is inconsistent. |
| `MissingFeatureCompilation` | A required `LIL_FEATURE_*` define is absent from the resolved pass. |
| `UnsupportedFeature` | An enabled feature, or a control outside its supported value. |
| `UnsupportedUv` | Unsupported UV mode, non-zero scroll/rotate, or a non-finite composed mapping. |
| `UnsupportedSampling` | Unsupported filter, wrap, mip, bias, or anisotropy on the governing texture. |
| `UnstableTextureIdentity` | No stable GUID plus local file identifier. |
| `UnsupportedColorSpace` | Project is not in linear color space. |
| `UnsupportedTextureImport` | No importer, unprovable sRGB, unprovable sampled alpha, unprovable `[0,1]` range, or non-canonical normal import. |

## Malformed versus unsupported

This policy is unchanged. A null or destroyed `Material`, or one with no shader, throws. Valid but unsupported state returns `Unknown` with a diagnostic. The design never converts unsupported behavior into a guessed default.

## Poiyomi versus lilToon comparison

| Concern | Poiyomi 9.3.64 | lilToon 2.3.4 | Class | Action |
| --- | --- | --- | --- | --- |
| Semantic output forms | `ColorSemanticValue`, `ScalarSemanticValue`, `NormalSemanticValue`, `SemanticOutput<T>` | Identical, no extension needed | **A** | Reuse unchanged |
| `TextureSample` / `UvMapping` / `TextureSampling` | Direct per-texture ST on a selected channel | Affine composition on UV0, or direct on a selected channel for emission | **A** | Reuse unchanged |
| Texture asset identity | GUID plus local file id | Identical need and failure contract | **B** | **Extract** |
| sRGB / linear interpretation | `TextureImporter.sRGBTexture` | Identical need and failure contract | **B** | **Extract** |
| Sampled-alpha-is-one proof | Emission RGB independence | Emission RGB independence, different equation | **B** | **Extract** |
| Canonical normal-map import | `NormalMap` type, no green flip | Identical; `lilUnpackNormalScale` is the same DXT5nm unpack | **B** | **Extract** |
| Sampler state of a `Texture` | Filter, wrap, mip, bias, aniso | Identical rule and thresholds | **B** | **Extract** |
| Sampled `[0,1]` range proof | Not required | Required for `lilToneCorrection` identity | **D** | lilToon-only, one consumer |
| Which texture supplies the sampler | Always `_MainTex` | `_MainTex` for base and normal, `_EmissionMap` for emission | **D** | Keep separate |
| Normalized hashing rule | BOM strip, CRLF/CR to LF, SHA-256 | Identical rule, different inputs | **B/C boundary** | Duplicate for now |
| Attestation anchor | One shader asset hash | Include-tree digest, two canonical-asset digests, live `LIL_RENDER` read | **C** | Keep separate |
| Generated-source handling | Locked shaders refused outright | Generation is the normal case. Canonicalize the proven-variable regions. | **C** | Opposite conclusions |
| Compile-time feature evidence | Not applicable | Required for Normal and Emission | **C** | lilToon-only |
| UV channel selection | `_MainTexUV`, `_BumpMapUV`, `_EmissionMapUV` floats | UV0 only for main and normal; `_EmissionMap_UVMode` enum for emission | **D** | Keep separate |
| Render mode source | `_AlphaForceOpaque` property | `LIL_RENDER` compile-time constant, read from the live pass | **D** | Keep separate |
| Alpha equation | `_Color.a` times `_MainTex.a`, eighteen gates | `Constant(1)`, two coverage gates | **D** | Keep separate |
| BaseColor equation | `_Color` times `_MainTex`, forty gates | `_Color` times `_MainTex`, eight gates plus three value proofs | **D** | Keep separate |
| Unconditional color transform | None | `lilToneCorrection` always runs, no toggle | **D** | lilToon-only |
| Emission composition | Four additive slots | One blend with four modes, Add only | **D** | Keep separate |
| Exact-off gate helper | `FirstFailedZeroGate` | Same shape, entirely different name lists | **D** helper, **C** data | Duplicate |
| Diagnostic type shape | Output, code, detail | Same shape, overlapping but distinct code set | **C** | Duplicate |
| Finite-value checks | `IsFinite` overloads | Identical | trivial | Duplicate |

### A, B, C, D summary

- **A — semantic-core concepts.** All value types, `SemanticOutput<T>`, `TextureSample`, `UvMapping`, `TextureSampling`, `TextureSourceId`, and the enums. Both frontends use them unchanged. No modification is proposed.
- **B — generic Unity host evidence.** Five texture facts. Each has two concrete consumers with identical meaning, input contract, failure behavior, and host assumptions. This category was empty after the Poiyomi milestone. The second frontend populated it.
- **C — shader-family knowledge.** Property names, gate lists, the pinned digests, the compile-time symbol set, and the canonicalization rules. Not extracted.
- **D — shader-specific interpretation.** Every equation, UV rule, and sampler coupling. Also the tone-correction identity proof and its range predicate. Also the emission blend-mode algebra and the `LIL_RENDER` alpha derivation. Kept wholly separate.

## Proposed shared extraction

One internal static class, `Alrauna.Amuse.Editor.Semantics.UnityTextureEvidence`, holding exactly five methods:

```
bool TryGetSourceId(Texture, out TextureSourceId)
bool TryGetSampling(Texture, out TextureSampling)
bool TryGetColorInterpretation(Texture, out TextureColorInterpretation)
bool TryProveSampledAlphaIsOne(Texture)
bool IsCanonicalNormalMapImport(Texture)
```

All five commonization conditions hold for each method. The meaning is the same. The input contract is the same (`Texture`). Null and destroyed inputs return false in both. The failure behavior is the same.

The host assumptions are the same (Editor-only, `AssetDatabase` plus `TextureImporter`). Ownership sits outside either frontend.

The Poiyomi frontend is repointed by behavior-preserving delegation. Helpers made strictly dead by that delegation are deleted. It keeps `TryGetMainTextureSampling(Material)` as a thin local wrapper. Reason: "the sampler always comes from `_MainTex`" is Poiyomi-specific knowledge. All existing Poiyomi tests must pass unmodified.

The class gets no shader property names, no optimization policy, no NDMF types, no diagnostics, and no `Material` parameters. It is not a material extraction framework. The plan guards that with an explicit checkpoint.

## Deliberately duplicated

| Duplicated | Why kept separate |
| --- | --- |
| `FirstFailedZeroGate` / `AreExactlyZero` | Same eight-line shape. The gate lists encode entirely different shader knowledge. Revisit at a third consumer. |
| `IsFinite` overloads | Three trivial one-liners. |
| `ComputeNormalizedSourceHash` | The rule is identical and is a real shared contract. But it currently sits inside `PoiyomiMaterialSemantics` beside Poiyomi-specific attestation. Strongest candidate for the next extraction. |
| Diagnostic and result types | Shapes match, code sets do not. A shared diagnostic framework across two frontends is the premature generalization this milestone resists. |
| `TryReadBinary` | lilToon toggles are `Int`-typed with different validity ranges. |
| Sampled range proof | One consumer. Extract only when a second frontend needs the identical contract. |

## Pressure on the semantic core

No change to `MaterialSemantics` is proposed. Every pressure is absorbed by returning `Unknown`.

| Observed pressure | Producers | Decision |
| --- | --- | --- |
| RGB multiplied by the alpha of the same sample | **Poiyomi and lilToon** | Still `Unknown` in both. Promoted from "one producer" to "two independent producers, same shape". Leading candidate for a future closed form. Two producers and zero consumers are not yet sufficient. |
| Cutout coverage (`clip(alpha - _Cutoff)`) | lilToon | New. The vocabulary models an alpha *value*, not coverage. Cutout stays uninterpreted. Matters because `AlphaSemanticsResolver` would otherwise read a cutout value as a blend factor. |
| Premultiplied alpha | Poiyomi (gated), lilToon (unconditional on `LIL_RENDER 2`) | Both refuse. Second producer confirms generality. |
| Non-additive blend modes | lilToon | New. `Unknown`. No blend-mode concept is added. |
| Unconditional parameterized color transform | lilToon | New. Handled by proving the parameters *and the input range* are the identity, not by modeling the transform. |
| Layered/summed emission | Poiyomi (four slots), lilToon (two) | Both refuse. |
| Affine UV composition | lilToon | **No pressure.** `UvMapping` expresses it exactly. |
| Compile-time feature stripping | lilToon | Pressure on **attestation**, not the IR. A category Poiyomi could not surface. |
| Generated, per-project shader source | lilToon | Pressure on **attestation**, not the IR. Resolved by canonical-remainder digests. |
| Gamma color workflow | Both | Both refuse. |

Category B is no longer empty. Category C is still not justified. Category D is not reached.

## Observed declarative patterns

Observations only. No schema is designed.

- **Exact-off gate lists — the most schema-shaped data in the codebase.** But the two lists differ in kind. The Poiyomi forty-entry list enumerates features that *write into* the term. The lilToon list mixes writers, UV determinants, and value-identity proofs. A schema would have to express five requirements as explicit concepts: "must equal `(0,1,1,1)`", "must be unassigned", "must be finite", "must be exactly 1", and "must be provably in `[0,1]`". Two frontends do not give enough evidence to design that vocabulary.
- **Texture-slot descriptors.** Both frontends repeat the property, UV descriptor, sampler source, and color requirement per slot. But lilToon per-slot sampler source and UV composition differ. A shared descriptor would need two shapes immediately.
- **Attestation descriptors.** Poiyomi pins one file. lilToon pins three digests, a live define read, and canonicalization rules. The two models share only the hashing rule.
- **Per-output compile-time dependency sets.** This is the first thing in AMUSE that resembles a dependency edge. One producer exists. It stays a hard-coded check.

The honest conclusion: a declarative shader profile is **not** justified by two frontends. The gate lists look alike. What they must express does not.

## Researched family taxonomy (evidence, not production)

lilToon 2.3.4 ships 65 shader assets. This section records the taxonomy so a future milestone need not re-derive it. The design deliberately does **not** freeze it into code.

| Class | Members | `LIL_RENDER` |
| --- | --- | --- |
| Base opaque | `lts` (`lilToon`) | 0 |
| Base opaque + outline | `lts_o` | 0 |
| Outline only | `lts_oo`, `lts_cutout_oo`, `lts_trans_oo` | 0 / 1 / 2 |
| Cutout | `lts_cutout`, `lts_cutout_o` | 1 |
| Transparent | `lts_trans`, `lts_onetrans`, `lts_twotrans`, their `_o` forms, `lts_overlay`, `lts_overlay_one` | 2 |
| Tessellation | `lts_tess*` (10) | 0 / 1 / 2 |
| Lite | `ltsl*` (12) | 0 / 1 / 2 |
| Multi | `ltsmulti*` (5) | keyword-driven |
| Fur | `lts_fur*`, `lts_furonly*` (6) | mixed |
| Gem / Refraction / FakeShadow | `lts_gem`, `lts_ref`, `lts_ref_blur`, `lts_fakeshadow` | — |
| Internal pass | `ltspass_*`, `ltsother_*` (13) | 0 / 1 / 2 |

The variant to pass-shader mapping comes from the `lilPassShaderName` of each `.lilinternal`. `LIL_RENDER` comes from the `HLSLINCLUDE` of the pass shader. Lite runs a different fragment path (`lil_pass_forward_lite.hlsl`). Multi force-defines every feature on. Fur, gem, refraction, overlay, and tessellation each carry their own equations. Each class needs its own tracing before it can be interpreted.

## What the second adapter taught us

1. Category B is real. Five texture facts are host-generic. Two independent consumers proved that.
2. `UvMapping` survived a composition rule it was not designed for.
3. The same semantic value can come from radically different evidence classes. `Alpha = Constant(1)` comes from a material property in Poiyomi. It comes from attested shader identity in lilToon.
4. Attestation has no single shape. A second shader can invalidate the anchor of the first shader entirely.
5. Compile-time configuration can defeat fail-closed design. A frontend that reads only material state can produce false positives on a shader with strippable features.
6. A negative "does this look unsafe?" predicate is fail-open by construction. The `IsHdrColorImport` draft permitted a `Complete` claim for any format it did not recognize. Range and safety predicates must be positive allow-lists.
7. Reachability-based dependency closures are error-prone. The first attempt missed seven files. Directory enumeration removes that reasoning from the trusted base.
8. The `rgb * a` emission form is general, not a Poiyomi artifact.
9. Coverage semantics are a genuine gap in the IR.
10. Superficially declarative gate lists are not the same kind of data across frontends.

## Public and private validation strategy

lilToon is MIT-licensed. But copying its includes into AMUSE or downloading them in CI is unnecessary. Public deterministic tests will:

1. Use a small purpose-built ShaderLab fixture that exposes only the consumed property contract.
2. Exercise equations through an internal verified-material seam. The seam takes already-resolved attestation facts. The fixture never needs real digests.
3. Create tiny temporary texture assets to test identity, ST composition, sampler state, sRGB, sampled alpha, range proof, and normal import.
4. Test canonicalization, digesting, the define scan, `_lilToonVersion`, package identity, and the verification conjunction independently against purpose-built inputs.
5. Cover every supported form. Add at least one adversarial case per refusal category. Include the stripped-feature hazard and the unrecognized-format refusal.
6. Prove output-local invalidation and deterministic diagnostics.
7. Run the complete existing EditMode suite unchanged, including all Poiyomi tests. This proves the shared extraction is behavior-preserving.

One validation cannot run in the public repository alone. The three pinned digests must be checked against a real `jp.lilxyzw.liltoon@2.3.4` install. The install must run with default settings and with modified settings. That check is a blocking first task. It runs read-only. It is the gate on whether the canonicalization rules are complete.

No upstream lilToon source file is copied into this repository.

## Stop-condition findings

| Condition | Finding |
| --- | --- |
| `MaterialSemantics` requires a general expression DAG | **Not triggered.** |
| A feature or data-flow graph is required | **Not triggered.** |
| Semantic-core contracts require redesign | **Not triggered.** Zero core changes. |
| Support requires approximation disguised as exactness | **Not triggered.** The one risk is the `lilToneCorrection` identity. That risk is closed by proving parameters *and* a positively attested input range. |
| A generic schema or DSL is needed first | **Not triggered.** |
| Shader-specific knowledge must move into `MaterialSemantics` | **Not triggered.** |
| Live mutable Unity objects retained in semantic values | **Not triggered.** |
| NDMF types enter the semantic core | **Not triggered.** |
| Work expands into animation, render state, portability, atlasing, combining, or planning | **Not triggered.** |
| A proposed shared abstraction lacks two identical consumers | **Not triggered.** The range proof has one consumer. It was moved out of the shared class. |
| Complete fail-closed attestation requires a parser or substantial new subsystem | **Not triggered**, but this was close. Inverting the lilToon generator would have been a new subsystem. Three literal line rules plus three digests avoid it. If the blocking verification shows the rules are incomplete, this condition fires and support narrows further. |

## Deferred work

- Every lilToon variant class other than the base opaque target.
- lilToon releases other than 2.3.4, and non-BRP render pipelines.
- Coverage semantics, premultiplied alpha, non-additive blend modes, layered emission.
- Second normal maps, detail layers, masks, matcap, rim, glitter, reflection, parallax, dissolve, AudioLink, UDIM discard.
- `_MainTexHSVG` values other than the identity, gradation maps, and HDR main textures.
- Extraction work for `ComputeNormalizedSourceHash` and the range proof.
- Animation, material swaps, renderer traversal, effective-state resolution, NDMF integration.
- Any shared diagnostic framework, adapter registry, family taxonomy in code, or declarative shader profile.

## Complexity and known risks

- **Unestablished digests.** The three pins do not exist yet. They cannot be derived from the repository. The generated shaders in the repository are stale relative to their own tag generator. The blocking first task in the plan produces them by measuring a real install. The measurement is cross-checked between default and stripped settings. This is the main open risk.
- **Region-rule completeness.** R1 and R2 are bounded to the two regions that the generator provably controls. If a real install shows setting-dependent output at a third position, the default and stripped digests will differ. The milestone then stops. Widening the regions to fit an observed file is explicitly forbidden.
- **Refusal breadth.** Any lilToon update, any pipeline other than BRP, and any unmodeled generation variation refuse everything. That is intended fail-closed behavior. The diagnostic names the artifact.
- **Pass-shader resolution.** `Shader.Find("Hidden/ltspass_opaque")` could resolve a shadowing asset. The GUID check and the canonical digest both guard against that.
- **External include roots.** AudioLink, LTCGI, VRC Light Volumes, and Unity headers cannot be pinned. The reachability argument in section C is what makes that acceptable. Re-check it if any claim moves after the lighting stage.
- **Analysis timing.** lilToon regenerates shaders during `PreprocessBuild`. If AMUSE analyzes before and transforms after, the attested state could shift underneath it. That is out of scope here. It is a real integration risk for the eventual NDMF pass.
