# Poiyomi Material Semantics Adapter Design

**Date:** 2026-08-16

**Status:** Proposed for approval

## Problem statement

AMUSE now has an immutable normalized `MaterialSemantics` vocabulary, but no real shader producer. This milestone designs the first producer: a conservative Editor-only interpreter for one precisely identified Poiyomi Toon Shader release. It reads one supplied Unity `Material` as a resolved base-material state and constructs the existing BaseColor, Alpha, Emission, and Normal outputs without changing the semantic core.

The adapter is a source interpreter, not a compatibility claim for every material which happens to contain Poiyomi-looking properties. A complete output means the selected source equations, material values, texture import state, UV mapping, and sampler state all fit the existing closed semantic form. Incomplete evidence produces `Unknown` for the affected output and an actionable diagnostic.

## Goals

- Prove support for an explicit subset of the canonical, unlocked `.poiyomi/Poiyomi Toon` shader from Poiyomi Toon Shader 9.3.64.
- Construct the existing `MaterialSemantics` types directly; keep the semantic core, alpha classifier, and separation planner unchanged.
- Resolve shader/version identity before interpreting properties.
- Preserve missing-texture defaults only where the pinned shader source proves them.
- Model texture identity, UV channel/ST, shared sampler state, color interpretation, and finite constants exactly enough for downstream proof.
- Invalidate BaseColor, Alpha, Emission, and Normal independently.
- Return deterministic, output-scoped reasons when a valid material is outside the supported subset.
- Establish a public deterministic test seam plus optional real-Poiyomi/private integration validation without making Poiyomi a package or CI dependency.

## Non-goals

This milestone does not design or implement a generic adapter interface, registry, shader discovery service, ShaderLab evaluator, expression graph, texture readback, alpha resolver, animation/material-swap analysis, VRCFury interpretation, locked-shader parser, avatar traversal, NDMF pass, material combining, atlasing, baking, profitability, or mutation. It does not add Poiyomi as an AMUSE dependency or copy Poiyomi source into the repository.

It does not claim that four normalized outputs describe all Poiyomi lighting or rendering. Ordinary Poiyomi lighting remains host shading outside these source roles. An enabled optional layer which writes one of the represented roles, or introduces an unrepresented competing color/opacity/normal contribution, makes that affected output unknown.

## Authoritative research basis

The design pins sources rather than relying on property-name folklore.

| Source | Pinned evidence used |
| --- | --- |
| [Poiyomi 9.3.64 release](https://github.com/poiyomi/PoiyomiToonShader/releases/tag/v9.3.64) | Exact supported release and tag. |
| [Tag commit `e125e1c33cbfb860f59330799dd4d10a1097242d`](https://github.com/poiyomi/PoiyomiToonShader/tree/e125e1c33cbfb860f59330799dd4d10a1097242d) | Immutable source revision used for all shader conclusions. |
| [Canonical Poiyomi Toon shader](https://github.com/poiyomi/PoiyomiToonShader/blob/e125e1c33cbfb860f59330799dd4d10a1097242d/_PoiyomiShaders/Shaders/9.3/Toon/Poiyomi%20Toon.shader) | Property defaults, UV equations, sampler selection, base/alpha/normal/emission equations, and feature writes. |
| [Package manifest](https://github.com/poiyomi/PoiyomiToonShader/blob/e125e1c33cbfb860f59330799dd4d10a1097242d/package.json) | Package name `com.poiyomi.toon`, version `9.3.64`, MIT license. |
| [Shader metadata](https://github.com/poiyomi/PoiyomiToonShader/blob/e125e1c33cbfb860f59330799dd4d10a1097242d/_PoiyomiShaders/Shaders/9.3/Toon/Poiyomi%20Toon.shader.meta) | Canonical shader asset GUID. |
| [Poiyomi 9.3 Color & Normals source](https://github.com/poiyomi/PoiyomiDocs/blob/7d773e164c2c48de998ca5c8bde27246986f4464/versioned_docs/version-9.3/color-and-normals/color-and-normals.mdx) | User-facing meaning of Main Texture, Color, alpha, and normal map controls. |
| [Poiyomi 9.3 Alpha Options source](https://github.com/poiyomi/PoiyomiDocs/blob/7d773e164c2c48de998ca5c8bde27246986f4464/versioned_docs/version-9.3/color-and-normals/alpha-options.mdx) | Force Opaque, Alpha Mod, premultiply, and coverage features. |
| [Poiyomi 9.3 Emission source](https://github.com/poiyomi/PoiyomiDocs/blob/7d773e164c2c48de998ca5c8bde27246986f4464/versioned_docs/version-9.3/special-fx/emission.mdx) | Four additive emission slots and their optional modifiers. |
| [Poiyomi 9.3 locking source](https://github.com/poiyomi/PoiyomiDocs/blob/7d773e164c2c48de998ca5c8bde27246986f4464/versioned_docs/version-9.3/general/locking.mdx) | Locking generates a specialized shader and may fix or rename properties. |
| [Unity `TextureImporter.sRGBTexture`](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/TextureImporter-sRGBTexture.html) | Import-time sRGB-to-linear interpretation. |
| [Unity linear or gamma workflow](https://docs.unity3d.com/2022.3/Documentation/Manual/LinearRendering-LinearOrGammaWorkflow.html) | Color-property and texture conversion behavior in a linear project. |
| [Unity asset identity API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetDatabase.TryGetGUIDAndLocalFileIdentifier.html) | Stable project asset GUID plus local file identifier. |
| [Unity package lookup API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PackageManager.PackageInfo.FindForAssetPath.html) | Installed package name and version for a shader asset path. |

The exact supported source has:

- shader name `.poiyomi/Poiyomi Toon`;
- package `com.poiyomi.toon` version `9.3.64` when installed as a package;
- tag commit `e125e1c33cbfb860f59330799dd4d10a1097242d`;
- shader asset GUID `9444ce77bf4418748b1e8591b9d97f85`;
- normalized-source SHA-256 `31f2ff15615c5e2ac9b05fea08b6310731394d1b5a928b16048e7bde8f8b1755`.

The hash is over UTF-8 source with an optional BOM removed and all CRLF/CR line endings normalized to LF. Pinning those normalization rules avoids rejecting the same official source solely because package transport changed line endings.

## Considered approaches

### 1. Exact source attestation plus explicit output gates — selected

Recognize only the canonical unlocked shader at the pinned source, then interpret an allowlisted semantic subset. This produces the strongest claim with the least machinery and makes future support expansion an evidence-bearing change.

### 2. Shader-name and property-schema heuristics

Accepting `.poiyomi/Poiyomi Toon` plus familiar properties would cover more installs, but shader names and properties can survive semantic changes, forks, or generated variants. A heuristic identity cannot justify `Complete`; this approach is rejected.

### 3. Parse locked/generated shaders or build a shader expression evaluator

Locked shaders are common on deployed avatars, but their generated source, property fixing, renaming, and feature elimination make them a separate interpreter problem. General parsing or an expression DAG would greatly expand the milestone before one unlocked adapter proves useful. This is deferred.

## Entry point and result boundary

Keep the implementation Poiyomi-specific and internal:

```csharp
namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    internal static class PoiyomiMaterialSemantics
    {
        internal static PoiyomiSemanticResult AnalyzeBaseMaterial(Material material);

        // Narrow friend-test seam. The caller must already have established
        // that the material exposes the pinned property contract.
        internal static PoiyomiSemanticResult InterpretVerifiedMaterial(
            Material material,
            ColorSpace activeColorSpace);
    }
}
```

`AnalyzeBaseMaterial` verifies identity and passes `QualitySettings.activeColorSpace` to the interpreter. The explicit color-space parameter prevents deterministic tests from modifying the public project's Gamma `ProjectSettings`; it is a resolved Unity fact required by the color equations, not dependency injection or a generic adapter API. The test seam prevents the public suite from bundling Poiyomi. Both methods return newly constructed immutable semantic values and retain no `Material`, `Shader`, `Texture`, importer, or asset-database object.

The name `AnalyzeBaseMaterial` is deliberate. It analyzes the current values of the supplied material object. It does not assert that later animation, material swaps, VRCFury/SPS processing, renderer overrides, or build-time modifiers leave that state effective. A future caller must establish the effective-state boundary before promoting the result to an avatar-wide claim.

```csharp
internal sealed class PoiyomiSemanticResult
{
    internal bool IsSupportedMaterial { get; }
    internal MaterialSemantics Semantics { get; }
    internal IReadOnlyList<PoiyomiSemanticDiagnostic> Diagnostics { get; }
}

internal enum PoiyomiSemanticOutput
{
    Material,
    BaseColor,
    Alpha,
    Emission,
    Normal
}
```

All result and diagnostic collections are defensive immutable copies. No adapter registry, common shader interface, service object, dependency injection, or separate assembly is introduced. The tightly related types may remain in one production file initially.

### Consumer-driven production concepts

| Proposed concept | Concrete immediate need | Why it belongs now |
| --- | --- | --- |
| `PoiyomiMaterialSemantics` | Attest and interpret the first real source material. | It is the milestone's only producer. |
| `PoiyomiSemanticResult` | Return partial semantics together with refusal reasons without contaminating `SemanticOutput<T>`. | A user must be able to distinguish unsupported material identity from output-local uncertainty. |
| `PoiyomiSemanticDiagnostic` plus two small enums | Express one deterministic reason per unknown output. | Plain log text would be side-effectful and difficult to test; a general diagnostic framework is not needed. |
| `InterpretVerifiedMaterial(material, activeColorSpace)` internal seam | Test source equations publicly without vendoring Poiyomi or changing the Gamma public project's settings. | It directly enables legal deterministic tests for this producer and makes a required resolved environment fact explicit; it is not shared adapter infrastructure. |

No generic Unity-facts type is proposed. Material/property/texture extraction remains private Poiyomi code until a second real producer demonstrates repeated structure.

## Shader and version identification

Identity is a conjunction, not a score:

1. The material and shader are live Unity objects and the exact shader name is `.poiyomi/Poiyomi Toon`.
2. `AssetDatabase.GetAssetPath(shader)` resolves to a readable asset.
3. `AssetDatabase.TryGetGUIDAndLocalFileIdentifier` returns GUID `9444ce77bf4418748b1e8591b9d97f85`.
4. The normalized shader source has the pinned SHA-256.
5. If `PackageInfo.FindForAssetPath` reports a package, its name and version are exactly `com.poiyomi.toon` and `9.3.64`. A legacy `Assets/_PoiyomiShaders` install may have no package result; the GUID and exact source hash remain the proof.
6. The exact required property schema is present, including `shader_master_label`, `_ShaderOptimizerEnabled`, `_MainTex`, `_Color`, `_BumpMap`, and the four emission slots.

Failure of any applicable check returns `IsSupportedMaterial == false`, all four outputs `Unknown`, and one material-scoped diagnostic. Similar names, other Poiyomi versions, user-modified source, alternative official shaders, forks, and inaccessible source are unsupported rather than guessed.

The initial adapter explicitly excludes `.poiyomi/Poiyomi Toon Two Pass`, Outline Early, Grab Pass, World, Lil Fur variants, and every locked/generated shader. `_ShaderOptimizerEnabled != 0` is also rejected even if a malformed material still points at the canonical source.

## Common extraction rules

These rules apply independently wherever an output uses them.

### Finite values and exact modes

Every consumed float, color component, scale, and offset must be finite. Toggle and enum-like properties are accepted only at explicitly supported exact values; an interpolated, unrecognized, or non-finite value makes the affected output unknown. The adapter does not round a nearly supported value or silently substitute a default.

### UV mapping

Poiyomi computes ordinary texture coordinates as `uv[channel] * _Texture_ST.xy + _Texture_ST.zw`. Values `0`, `1`, `2`, and `3` map exactly to `UvMapping` channels 0–3. Pan must be exactly zero. Panosphere, world/local position, polar, distorted UV, matcap coordinates, stochastic sampling, and pixel mode are unsupported. A missing main texture can still yield a constant and therefore needs no UV or sampling claim.

### Texture source identity

For an assigned texture, use:

```text
unity-asset:<lowercase-guid>:<invariant-decimal-local-id>
```

The GUID and `long` local identifier come from `AssetDatabase.TryGetGUIDAndLocalFileIdentifier`. This is stable for the same project asset and distinguishes sub-assets. Scene-only, generated, transient, or otherwise unidentifiable texture objects make only their consuming outputs unknown. Never fall back to `GetInstanceID`, path text, object name, pixels, or reference equality.

### Sampling

The pinned shader samples `_MainTex`, `_BumpMap`, alpha-map, and emission-map data using the sampler declared by `_MainTex`. Therefore:

- Point and Bilinear are the only supported filters; Trilinear is unknown.
- U and V wrap modes must be equal and either Clamp or Repeat.
- Mirror, MirrorOnce, per-axis mismatch, mipmapped sampling, nonzero mip bias, or anisotropy greater than one are unknown because the v1 core cannot express them.
- Assigned auxiliary textures use their own source identity, ST, UV channel, and color interpretation, but `_MainTex`'s supported sampler state.
- If an auxiliary texture is assigned while `_MainTex` is absent, its sampling is unknown; the implicit built-in white sampler is not promoted to a guessed `TextureSampling` value.

Sampling failure invalidates every output which uses that sampler, not unrelated constants.

### Color space

BaseColor and Emission are linear-light semantics. They may be complete only while `QualitySettings.activeColorSpace == ColorSpace.Linear`. Material Color-property RGB is normalized to the equivalent linear shader value; alpha is retained as a scalar. Assigned color textures use their `TextureImporter.sRGBTexture` flag to select `TextureColorInterpretation.Srgb` or `Linear`. If an importer is unavailable or the project is in Gamma mode, the affected color output is unknown. Alpha-channel and canonical normal-map interpretation remain independently decidable.

## Supported semantic subset

The adapter uses positive rules for representable equations and conservative feature gates for every source block known to write a represented role.

### Summary support matrix

| Verified Poiyomi 9.3.64 state | BaseColor | Alpha | Emission | Normal |
| --- | --- | --- | --- | --- |
| Missing MainTex/BumpMap, emission disabled, simple feature profile | Complete color constant | Complete `_Color.a` constant, or one for Force Opaque | Complete zero constant | Complete Unmodified |
| MainTex × tint, MainTex alpha × color alpha, no normal/emission | Complete texture × constant | Complete alpha sample × constant | Complete zero constant | Complete Unmodified |
| UV1 plus finite non-identity ST and supported MainTex sampler | Complete | Complete | Complete if its own path is constant/supported | Complete if absent or independently supported |
| Unsupported color-only writer enabled | Unknown | Complete if the writer does not touch alpha | Independently evaluated | Independently evaluated |
| General RGBA emission map, no base replacement | Independently evaluated | Independently evaluated | Unknown | Independently evaluated |
| Emission replacement enabled | Unknown | Independently evaluated | Unknown unless its whole equation is supported | Independently evaluated |
| Non-unit normal strength | Independently evaluated | Independently evaluated | Independently evaluated | Unknown |
| Gamma project | Unknown | Independently evaluated | Unknown | Independently evaluated |
| Unsupported main sampler with assigned MainTex/Bump/Emission maps | Unknown for assigned main color | Unknown for assigned main alpha | Unknown for assigned map | Unknown for assigned map |
| Unscoped source/version/modifier uncertainty | Unknown | Unknown | Unknown | Unknown; whole material unsupported when source identity fails |

“Independently evaluated” means no completeness is inherited from the row; the output must still satisfy all of its own rules.

### BaseColor

The normalized role is Poiyomi's main surface RGB before alpha composition and ordinary host lighting.

| Pinned state | Result |
| --- | --- |
| `_MainTex` absent | `Constant(linear(_Color.rgb))` |
| `_MainTex` assigned and `_Color.rgb == (1,1,1)` after normalization | `TextureSample(main RGB)` |
| `_MainTex` assigned and other finite color | `TextureSampleTimesConstant(main RGB, linear(_Color.rgb))` |

Required source conditions include `_ColorThemeIndex == 0`, `_MainTexUV` in 0–3, zero `_MainTexPan`, `_MainPixelMode == 0`, and `_MainTexStochastic == 0`. The assigned texture must pass identity, sampler, and importer checks.

The output becomes unknown when an enabled source block changes or replaces the main color term, including color adjust, detail color, vertex coloring, backface color, RGBA color masking, dissolve edge/color, decals, anisotropic replacement, matcaps, cubemap contribution, AudioLink decal/volume color, flipbook, rim/depth-rim color, glitter, stylized reflection, pathing, mirror/text/internal-parallax effects, video/touch effects, voronoi/truchet effects, emission replacement, or alpha premultiplication. Ordinary Poiyomi lighting is not reclassified as BaseColor, but optional user layers which compete with this role are refused.

### Alpha

The supported alpha equation excludes render queue, cutoff, blend factors, and render-mode inference. Those are not part of the existing Alpha semantic.

| Pinned state | Result |
| --- | --- |
| Supported opacity features, `_AlphaForceOpaque == 1`, and no independent discard/coverage feature | `Constant(1)` |
| `_AlphaForceOpaque == 0`, `_MainIgnoreTexAlpha == 1` | `Constant(_Color.a)` |
| Same, ignore-alpha `0`, `_MainTex` absent | `Constant(_Color.a)` because the source default is white |
| Same, ignore-alpha `0`, `_MainTex` assigned, `_Color.a == 1` | `TextureSample(main Alpha)` |
| Same, ignore-alpha `0`, `_MainTex` assigned, other finite alpha | `TextureSampleTimesConstant(main Alpha, _Color.a)` |

For the non-force-opaque paths, `_AlphaMod` must be zero, alpha-map mode must be disabled, and distance/fresnel/angular/AudioLink alpha must be disabled. Vertex alpha, backface alpha, RGBA masks, dissolve, decals, flipbook alpha, rim alpha, video/touch alpha, and other traced alpha writers invalidate Alpha. Alpha-to-coverage, dithering, and any enabled discard/coverage feature also invalidate Alpha even if force-opaque later sets the scalar to one; otherwise a consumer could mistake “scalar one” for proven visible coverage.

No adapter result directly invokes or changes `TriangleAlphaClassifier`. A future resolver remains responsible for pixels, mesh UVs, and exact classification.

### Normal

| Pinned state | Result |
| --- | --- |
| `_BumpMap` absent | `Unmodified` because the pinned ShaderLab default is `"bump"` |
| `_BumpMap` assigned under the conditions below | `TangentSpaceNormalMap(sample)` |

An assigned normal requires `_BumpScale == 1`, `_BumpMapUV` in 0–3, zero `_BumpMapPan`, `_BumpMapStochastic == 0`, stable texture identity, supported main sampler, and a `TextureImporter` identifying a normal-map asset without green-channel inversion. At unit strength this is the existing canonical tangent-space normal role; Unity's platform storage swizzle is an import detail, not a new semantic expression.

Detail normals, RGBA-mask normal replacement, decals which perturb normals, parallax-derived normal changes, non-unit intensity, non-OpenGL inversion, or any other traced normal writer make Normal unknown. BaseColor, Alpha, and Emission remain independently eligible.

### Emission

The initial support is intentionally smaller than Poiyomi's four-layer model.

| Pinned state | Result |
| --- | --- |
| All four emission slots disabled | `Constant(Vector3.zero)` |
| Only slot 0 enabled, no assigned map, simple controls | `Constant(linear(_EmissionColor.rgb) * _EmissionStrength)` |
| Only slot 0 enabled, assigned map proven to sample alpha as one, simple controls | `TextureSample` or `TextureSampleTimesConstant` for map RGB |

“Simple controls” means slots 1–3 are disabled; theme, use-base-color-as-map, replace-base-color, fluorescence, center-out, scrolling, blinking, hue, light-based emission, AudioLink emission, emission masks, global emission mask, and all other slot modifiers are off; slot 0 pan is zero and UV is 0–3; all values are finite; and source identity, main sampler, and sRGB importer evidence are available.

The source multiplies an assigned emission map's RGB by that same sample's alpha. A mapped emission is complete only when the importer proves the source has no alpha and imports alpha as none, making sampled alpha exactly one. General RGBA emission maps are unknown. Multiple enabled slots are unknown because their sum is not one of the existing closed color forms.

## Output-local invalidation and diagnostics

Identity failure is material-wide. After identity succeeds, each output is evaluated separately and gets either a complete value or one primary reason for being unknown. Examples:

- an unsupported normal strength invalidates Normal only;
- an RGBA emission map invalidates Emission, and `_EmissionReplace0` additionally invalidates BaseColor;
- Gamma project color space invalidates BaseColor and Emission, but not otherwise supported Alpha or Normal;
- an unsupported main sampler invalidates assigned MainTex consumers and assigned auxiliary-texture consumers, but not constant outputs;
- an unstable emission texture identity invalidates Emission only.

Use a small closed diagnostic code set, not a logging framework:

```text
UnsupportedShader
UnsupportedVersion
ModifiedShaderSource
MissingSourceEvidence
UnsupportedFeature
UnsupportedUv
UnsupportedSampling
UnstableTextureIdentity
UnsupportedColorSpace
UnsupportedTextureImport
```

A diagnostic records the output, code, and an ordinal property/evidence detail string. Ordering is deterministic: Material, BaseColor, Alpha, Emission, Normal. Within an output, the interpreter reports the first failed rule in documented evaluation order: value/form, feature gate, UV, sampling, identity, import/color interpretation. Diagnostics are data; the adapter does not write the Unity Console.

## Malformed and unsupported inputs

- `null` material throws `ArgumentNullException`.
- A destroyed material or missing shader throws `ArgumentException`; the caller did not supply an analyzable material object.
- A valid non-Poiyomi material, unsupported Poiyomi version/variant, locked shader, modified source, inaccessible source, or source-evidence mismatch returns an unsupported result rather than throwing.
- Valid but unrepresentable material values return partial `Unknown` outputs with diagnostics.
- Asset-database/importer lookup failure for a valid transient texture is uncertainty, not an exception.

Expected uncertainty never becomes a default value and never makes optimization more aggressive.

## Public and private validation strategy

Poiyomi is MIT-licensed, but copying its large feature-rich shader into AMUSE or downloading it in ordinary CI is unnecessary. Public deterministic tests will:

1. use a tiny purpose-built test ShaderLab fixture containing only the relevant property contract;
2. call the internal verified-material interpreter seam to exercise exact material/default/feature/UV equations;
3. create tiny temporary texture assets to verify GUID/local-ID identity, ST, sampler, importer sRGB, alpha, and normal-map behavior;
4. test identity and normalized hashing separately using purpose-built source text and pinned constants;
5. test every supported form and one adversarial case for each refusal category;
6. prove output-local invalidation and deterministic diagnostics;
7. run the complete existing EditMode suite to protect classifier, geometry, planner, and semantic-core contracts.

The fixture is an executable specification of the traced equations, not a pretend Poiyomi distribution. Tests must first fail for the intended missing behavior, then production code is added, then the same focused test is observed green.

Optional integration validation may use an already installed official `com.poiyomi.toon@9.3.64` package and purpose-built materials. The private avatar testbed may be inspected read-only only after confirming its project root; it is not required for CI, supplies no publishable fixture, and must not be modified. Locked-avatar coverage is expected to remain unsupported in this milestone.

## Relationship to consumers and future stages

The adapter constructs the existing types without adding optimization annotations. A future alpha resolver may turn a complete Alpha texture expression into immutable pixels plus mesh UV evidence before invoking the unchanged classifier. A future atlas or material-combination analyzer may compare `TextureSourceId`, `UvMapping`, and texture-times-tint structure, but must treat an unknown relevant output as refusal and must separately account for any roles not represented by v1. None of those consumers is implemented here.

Animation/state analysis will later construct or compare several resolved results. Modifier analysis will later establish whether a base material is still the effective state. These are upstream evidence stages, not symbolic data added to this adapter.

## Pressure on the semantic core

The existing core is sufficient for the selected BaseColor, Alpha, constant/simple Emission, and Normal subset. Do not change it in this milestone.

| Observed pressure | Category | Decision |
| --- | --- | --- |
| Emission RGB multiplied by the same sample's alpha | A — safely defer; possibly C after a second concrete consumer | General RGBA emission maps remain Unknown. Do not add a shader-specific closed form yet. |
| Addition of up to four emission layers | A — safely defer | Multiple enabled layers remain Unknown. |
| Gamma-workflow color arithmetic | A — safely defer | Color outputs require a Linear project. |
| Mips, anisotropy, mirror modes, or independent U/V wrap | A — safely defer | Affected sampled outputs remain Unknown. |
| Arbitrary generated/locked shader expressions | A, not D | Refuse the shader. No expression DAG or evaluator. |

Category B is empty: no issue requires a generic Unity extraction boundary before a second adapter. Category C is not justified yet: one Poiyomi-only need is insufficient evidence for a new semantic form. Category D is not reached.

This is the YAGNI gate: direct Unity extraction stays in the Poiyomi producer, the friend-test seam is concrete and local, and no generic adapter infrastructure is created for hypothetical lilToon or future consumers.

## Deferred work

- Poiyomi versions other than 9.3.64 and every alternate official shader.
- Locked/generated Poiyomi shaders.
- General RGBA or multilayer emission.
- Alpha maps, adjusted alpha, cutoff/coverage semantics, and the future alpha resolver.
- Detail, decals, masks, special effects, advanced UV modes, stochastic/pixel sampling, mips, anisotropy, and mirror wrap.
- Animation, material swaps, renderer traversal, VRCFury/SPS state, and effective-state orchestration.
- Material combining, atlasing, baking, mesh transformation, NDMF integration, or optimization policy.
- A generic adapter registry or shared Unity extraction layer.

## Complexity and known risks

The production surface is one internal file, one result, one diagnostic value, two small enums, and private extraction helpers. The dominant complexity is the audited feature-gate table, not class structure. Tests add one original minimal shader fixture and one test class; no package or assembly is added.

Known risks are:

- exact source hashing intentionally rejects harmless downstream edits and every patch release until reviewed;
- unlocked-only support may cover few production avatars because locking is customary;
- a missed writer in the pinned shader would be a false completeness bug, so source-block tracing and adversarial tests are release-critical;
- Unity texture importer/platform behavior must match the declared sample meaning, especially normal decoding and source-alpha absence;
- linear Color-property parity must be verified in Unity rather than assumed from inspector appearance;
- optional effects that are visually zero but enabled are refused unless the source equation proves the zero collapses the entire contribution;
- the result is only a base-state interpretation and can become stale if the material mutates or another system later modifies effective state.

## Adversarial review conclusions

| Case | Outcome | Reason |
| --- | --- | --- |
| Null material | Malformed-input exception | No material object exists to analyze. |
| Destroyed material or missing shader | Malformed-input exception | Unity object state violates the entry contract. |
| Non-Poiyomi shader | Unsupported whole material | Valid input, wrong producer. |
| Poiyomi-looking name, alternate shader, old/new version, edited source, upgrade drift, or locked/generated shader | Unsupported whole material | Exact attestation fails; property resemblance is not evidence. |
| Required property absent from purported pinned source | Unsupported whole material | The source/schema proof is contradictory or modified, not a runtime feature case. |
| Undefined enum-like value or unsupported enabled feature | Unknown for every traced affected output | Valid material state, unrepresentable behavior. |
| Unsupported enabled feature whose write scope cannot be proven | Unknown for all potentially affected outputs | Scope uncertainty fails closed even if the feature currently appears visually inactive. |
| Enabled feature multiplied by a proven exact zero | Complete only if the pinned equation proves the entire contribution collapses and no side effect remains; otherwise Unknown | “Looks inactive” is not proof. |
| Missing MainTex or BumpMap | Complete only for the source-proven white/bump fallback paths | Defaults are derived from pinned ShaderLab, not assumed globally. |
| MainTex × tint or texture alpha × opacity | Complete under the per-output simple profile | These are exact existing closed forms. |
| UV1 or finite non-identity ST | Complete when the selected path uses ordinary `poiUV` | Both facts map exactly to `UvMapping`. |
| Per-axis wrap mismatch, Trilinear, Mirror, mips, or unsupported anisotropy | Unknown for consuming sampled outputs | V1 sampling cannot express the behavior. |
| Non-default normal strength | Normal Unknown | Canonical v1 normal has no strength operation. |
| Multiple emission layers or RGBA emission map | Emission Unknown; BaseColor also Unknown if replacement is enabled | Addition and RGB × same-alpha are outside v1. |
| Unsupported emission-only modifier | Emission Unknown; other outputs independently evaluated | The pinned source confines its write. |
| Non-asset/generated texture | Unknown for each consumer of that texture | No stable source token; no instance-ID fallback. |
| Two materials or repeated extraction using the same asset/state | Structurally equal complete semantics | GUID/local ID and immutable values are deterministic. |
| Explicit value equal to the ShaderLab default | Same outcome as the same resolved default value | This adapter interprets one resolved state, not serialization provenance. |
| Color-space/import mismatch | Unknown for affected color outputs | Linear-light meaning cannot be proven. |
| Unknown external modifier scope | All potentially affected outputs Unknown at the future modifier boundary | Base-material interpretation alone cannot attest effective state. |

The design was checked against these failure modes:

- **Renamed or forked shader:** exact name alone is insufficient; GUID and normalized source hash fail closed.
- **Same version label with edited source:** hash mismatch rejects it.
- **Official legacy install:** package metadata may be absent, but GUID plus hash still proves the source.
- **Locked shader:** generated source/name/property state is rejected, not treated as canonical.
- **Missing main texture:** only source-proven white/bump defaults become constants; auxiliary sampler state is not guessed.
- **Texture path rename:** GUID/local-ID identity remains stable.
- **Transient texture:** affected output becomes unknown; instance ID is never serialized into semantics.
- **U/V wrap mismatch or trilinear/mipped texture:** v1 cannot express it, so sampled outputs are unknown.
- **Gamma project:** linear-light BaseColor/Emission are unknown without changing the core's meaning.
- **Emission map with alpha:** its RGB-times-alpha equation is not misreported as plain RGB.
- **Force opaque plus dissolve/coverage:** scalar one is not promoted while independent visibility effects remain.
- **Unsupported feature in one role:** output-local invalidation prevents unnecessary loss of unrelated proof.
- **Modifier after extraction:** the API name and documented precondition prevent a base-material result from claiming effective avatar state.

The main practical limitation is intentional: many deployed Poiyomi materials are locked, so initial real-avatar coverage may be low. That is preferable to claiming exact semantics for generated shaders without proof. Evidence from this unlocked adapter should guide whether locked-source interpretation or a small new semantic form is worth a later milestone.
