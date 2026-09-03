# Poiyomi Material Semantics Adapter Design

**Date:** 2026-08-16

**Status:** Proposed for approval

## Problem statement

AMUSE now has an immutable normalized `MaterialSemantics` vocabulary, but no real shader producer. This milestone designs the first producer: a conservative Editor-only interpreter for one precisely identified Poiyomi Toon Shader release. It reads one supplied Unity `Material` as a resolved base-material state. It constructs the existing BaseColor, Alpha, Emission, and Normal outputs without changes to the semantic core.

The adapter is a source interpreter. It is not a compatibility claim for every material that happens to contain Poiyomi-looking properties. A complete output means the selected source equations, material values, texture import state, UV mapping, and sampler state all fit the existing closed semantic form. Incomplete evidence produces `Unknown` for the affected output and an actionable diagnostic.

## Goals

- Prove support for an explicit subset of the canonical, unlocked `.poiyomi/Poiyomi Toon` shader from Poiyomi Toon Shader 9.3.64.
- Construct the existing `MaterialSemantics` types directly. Keep the semantic core, the alpha classifier, and the separation planner unchanged.
- Resolve shader and version identity before the interpreter reads properties.
- Preserve missing-texture defaults only where the pinned shader source proves them.
- Model texture identity, UV channel/ST, shared sampler state, color interpretation, and finite constants exactly enough for downstream proof.
- Invalidate BaseColor, Alpha, Emission, and Normal independently.
- Return deterministic, output-scoped reasons when a valid material is outside the supported subset.
- Establish a public deterministic test seam plus optional real-Poiyomi/private integration validation without a Poiyomi package or CI dependency.

## Non-goals

This milestone does not design or implement:

- a generic adapter interface, registry, or shader discovery service
- a ShaderLab evaluator or expression graph
- texture readback, an alpha resolver, or animation and material-swap analysis
- VRCFury interpretation or a locked-shader parser
- avatar traversal or an NDMF pass
- material combining, atlasing, baking, profitability, or mutation

The design does not add Poiyomi as an AMUSE dependency. It does not copy Poiyomi source into the repository.

The design does not claim that four normalized outputs describe all Poiyomi lighting or rendering. Ordinary Poiyomi lighting stays host shading outside these source roles. An enabled optional layer can write one of the represented roles. It can also add a competing color, opacity, or normal effect that no output represents. Either case makes the affected output unknown.

## Authoritative research basis

The design pins sources. It does not use property-name folklore.

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
| [Poiyomi 9.3 locking source](https://github.com/poiyomi/PoiyomiDocs/blob/7d773e164c2c48de998ca5c8bde27246986f4464/versioned_docs/version-9.3/general/locking.mdx) | Locking generates a specialized shader and can fix or rename properties. |
| [Unity `TextureImporter.sRGBTexture`](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/TextureImporter-sRGBTexture.html) | Import-time sRGB-to-linear interpretation. |
| [Unity linear or gamma workflow](https://docs.unity3d.com/2022.3/Documentation/Manual/LinearRendering-LinearOrGammaWorkflow.html) | Color-property and texture conversion behavior in a linear project. |
| [Unity asset identity API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetDatabase.TryGetGUIDAndLocalFileIdentifier.html) | Stable project asset GUID plus local file identifier. |
| [Unity package lookup API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PackageManager.PackageInfo.FindForAssetPath.html) | Installed package name and version for a shader asset path. |

The exact supported source has:

- shader name `.poiyomi/Poiyomi Toon`
- package `com.poiyomi.toon` version `9.3.64` when installed as a package
- tag commit `e125e1c33cbfb860f59330799dd4d10a1097242d`
- shader asset GUID `9444ce77bf4418748b1e8591b9d97f85`
- normalized-source SHA-256 `31f2ff15615c5e2ac9b05fea08b6310731394d1b5a928b16048e7bde8f8b1755`

The hash covers UTF-8 source without an optional BOM and with all CRLF/CR line endings changed to LF. These normalization rules prevent a false rejection of the same official source when package transport changes line endings.

## Considered approaches

### 1. Exact source proof plus explicit output gates — selected

Recognize only the canonical unlocked shader at the pinned source. Then interpret an allowlisted semantic subset. This gives the strongest claim with the least machinery. Future support expansion then becomes an evidence-bearing change.

### 2. Shader-name and property-schema heuristics

An identity check by shader name plus familiar properties would cover more installs. But shader names and properties can survive semantic changes, forks, and generated variants. A heuristic identity cannot justify `Complete`. The design rejects this approach.

### 3. Parse locked/generated shaders or build a shader expression evaluator

Locked shaders are common on deployed avatars. But their generated source, property fixing, renaming, and feature elimination make them a separate interpreter problem. General parsing or an expression DAG would greatly expand this milestone before one unlocked adapter proves useful. The design defers this work.

## Entry point and result boundary

Keep the implementation Poiyomi-specific and internal:

```csharp
namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    internal static class PoiyomiMaterialSemantics
    {
        internal static PoiyomiSemanticResult AnalyzeBaseMaterial(Material material);

        // Narrow friend-test seam. The caller must first establish that
        // the material exposes the pinned property contract.
        internal static PoiyomiSemanticResult InterpretVerifiedMaterial(
            Material material,
            ColorSpace activeColorSpace);
    }
}
```

`AnalyzeBaseMaterial` verifies identity and passes `QualitySettings.activeColorSpace` to the interpreter. The explicit color-space parameter keeps deterministic tests from changing the public project's Gamma `ProjectSettings`. The color equations require this resolved Unity fact. The parameter is not dependency injection or a generic adapter API. The test seam keeps Poiyomi out of the public suite. Both methods return newly constructed immutable semantic values. They retain no `Material`, `Shader`, `Texture`, importer, or asset-database object.

The name `AnalyzeBaseMaterial` is deliberate. It analyzes the current values of the supplied material object. It does not assert that later animation, material swaps, VRCFury/SPS processing, renderer overrides, or build-time modifiers keep that state effective. A future caller must establish the effective-state boundary before it promotes the result to an avatar-wide claim.

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

All result and diagnostic collections are defensive immutable copies. The design adds no adapter registry, common shader interface, service object, dependency injection, or separate assembly. The tightly related types can stay in one production file initially.

### Consumer-driven production concepts

| Proposed concept | Concrete immediate need | Why it belongs now |
| --- | --- | --- |
| `PoiyomiMaterialSemantics` | Verify and interpret the first real source material. | It is the milestone's only producer. |
| `PoiyomiSemanticResult` | Return partial semantics together with refusal reasons without contaminating `SemanticOutput<T>`. | A user must distinguish unsupported material identity from output-local uncertainty. |
| `PoiyomiSemanticDiagnostic` plus two small enums | Express one deterministic reason per unknown output. | Plain log text would cause side effects and resist tests. A general diagnostic framework is not needed. |
| `InterpretVerifiedMaterial(material, activeColorSpace)` internal seam | Test source equations publicly without vendoring Poiyomi or changing the Gamma public project's settings. | It directly enables legal deterministic tests for this producer and makes a required resolved environment fact explicit. It is not shared adapter infrastructure. |

The design proposes no generic Unity-facts type. Material, property, and texture extraction stay private Poiyomi code until a second real producer shows repeated structure.

## Shader and version identification

Identity is a conjunction, not a score:

1. The material and shader are live Unity objects, and the exact shader name is `.poiyomi/Poiyomi Toon`.
2. `AssetDatabase.GetAssetPath(shader)` resolves to a readable asset.
3. `AssetDatabase.TryGetGUIDAndLocalFileIdentifier` returns GUID `9444ce77bf4418748b1e8591b9d97f85`.
4. The normalized shader source has the pinned SHA-256.
5. If `PackageInfo.FindForAssetPath` reports a package, its name and version are exactly `com.poiyomi.toon` and `9.3.64`. A legacy `Assets/_PoiyomiShaders` install can have no package result. The GUID and the exact source hash then remain the proof.
6. The exact required property schema is present, including `shader_master_label`, `_ShaderOptimizerEnabled`, `_MainTex`, `_Color`, `_BumpMap`, and the four emission slots.

Failure of any applicable check returns `IsSupportedMaterial == false`, all four outputs `Unknown`, and one material-scoped diagnostic. Similar names, other Poiyomi versions, user-modified source, alternative official shaders, forks, and inaccessible source are unsupported. The adapter does not guess.

The initial adapter explicitly excludes `.poiyomi/Poiyomi Toon Two Pass`, Outline Early, Grab Pass, World, Lil Fur variants, and every locked/generated shader. The adapter also rejects `_ShaderOptimizerEnabled != 0`, even if a malformed material still points at the canonical source.

## Common extraction rules

These rules apply independently wherever an output uses them.

### Finite values and exact modes

Every consumed float, color component, scale, and offset must be finite. The adapter accepts toggle and enum-like properties only at explicitly supported exact values. An interpolated, unrecognized, or non-finite value makes the affected output unknown. The adapter does not round a nearly supported value. It does not silently substitute a default.

### UV mapping

Poiyomi computes ordinary texture coordinates as `uv[channel] * _Texture_ST.xy + _Texture_ST.zw`. Values `0`, `1`, `2`, and `3` map exactly to `UvMapping` channels 0–3. Pan must be exactly zero. Panosphere, world/local position, polar, distorted UV, matcap coordinates, stochastic sampling, and pixel mode are unsupported. A missing main texture can still yield a constant. It then needs no UV or sampling claim.

### Texture source identity

For an assigned texture, use:

```text
unity-asset:<lowercase-guid>:<invariant-decimal-local-id>
```

The GUID and the `long` local identifier come from `AssetDatabase.TryGetGUIDAndLocalFileIdentifier`. This identity is stable for the same project asset and distinguishes sub-assets. Scene-only, generated, transient, or otherwise unidentifiable texture objects make only their consuming outputs unknown. Never use `GetInstanceID`, path text, object name, pixels, or reference equality as the identity.

### Sampling

The pinned shader samples `_MainTex`, `_BumpMap`, alpha-map, and emission-map data with the sampler that `_MainTex` declares. Therefore:

- Point and Bilinear are the only supported filters. Trilinear is unknown.
- U and V wrap modes must be equal and either Clamp or Repeat.
- Mirror, MirrorOnce, a per-axis mismatch, mipmapped sampling, a nonzero mip bias, or anisotropy greater than one are unknown, because the v1 core cannot express them.
- Assigned auxiliary textures use their own source identity, ST, UV channel, and color interpretation. They use the supported sampler state of `_MainTex`.
- An auxiliary texture with an absent `_MainTex` has unknown sampling. The implicit built-in white sampler does not become a guessed `TextureSampling` value.

Sampling failure invalidates every output that uses that sampler. It does not invalidate unrelated constants.

### Color space

BaseColor and Emission are linear-light semantics. They can be complete only while `QualitySettings.activeColorSpace == ColorSpace.Linear`. The adapter normalizes material Color-property RGB to the equivalent linear shader value. It keeps alpha as a scalar. Assigned color textures use their `TextureImporter.sRGBTexture` flag to select `TextureColorInterpretation.Srgb` or `Linear`. If an importer is unavailable, or the project is in Gamma mode, the affected color output is unknown. The adapter can decide Alpha-channel and canonical normal-map interpretation independently.

## Supported semantic subset

The adapter uses positive rules for representable equations. It uses conservative feature gates for every source block known to write a represented role.

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

“Independently evaluated” means the row gives no completeness to the output. The output must still satisfy all of its own rules.

### BaseColor

The normalized role is the main surface RGB of Poiyomi before alpha composition and ordinary host lighting.

| Pinned state | Result |
| --- | --- |
| `_MainTex` absent | `Constant(linear(_Color.rgb))` |
| `_MainTex` assigned and `_Color.rgb == (1,1,1)` after normalization | `TextureSample(main RGB)` |
| `_MainTex` assigned and other finite color | `TextureSampleTimesConstant(main RGB, linear(_Color.rgb))` |

Required source conditions include `_ColorThemeIndex == 0`, `_MainTexUV` in 0–3, zero `_MainTexPan`, `_MainPixelMode == 0`, and `_MainTexStochastic == 0`. The assigned texture must pass identity, sampler, and importer checks.

The output becomes unknown when an enabled source block changes or replaces the main color term. Traced writers include:

- color adjust, detail color, vertex coloring, backface color, RGBA color masking
- dissolve edge/color, decals, anisotropic replacement, matcaps, cubemap contribution
- AudioLink decal/volume color, flipbook, rim/depth-rim color, glitter, stylized reflection, pathing
- mirror/text/internal-parallax effects, video/touch effects, voronoi/truchet effects
- emission replacement, alpha premultiplication

The adapter does not reclassify ordinary Poiyomi lighting as BaseColor. But it refuses optional user layers that compete with this role.

### Alpha

The supported alpha equation excludes render queue, cutoff, blend factors, and render-mode inference. Those are not part of the existing Alpha semantic.

| Pinned state | Result |
| --- | --- |
| Supported opacity features, `_AlphaForceOpaque == 1`, and no independent discard/coverage feature | `Constant(1)` |
| `_AlphaForceOpaque == 0`, `_MainIgnoreTexAlpha == 1` | `Constant(_Color.a)` |
| Same, ignore-alpha `0`, `_MainTex` absent | `Constant(_Color.a)` because the source default is white |
| Same, ignore-alpha `0`, `_MainTex` assigned, `_Color.a == 1` | `TextureSample(main Alpha)` |
| Same, ignore-alpha `0`, `_MainTex` assigned, other finite alpha | `TextureSampleTimesConstant(main Alpha, _Color.a)` |

For the non-force-opaque paths, `_AlphaMod` must be zero. The alpha-map mode must be disabled. Distance/fresnel/angular/AudioLink alpha must be disabled. Vertex alpha, backface alpha, RGBA masks, dissolve, decals, flipbook alpha, rim alpha, video/touch alpha, and other traced alpha writers invalidate Alpha. Alpha-to-coverage, dithering, and any enabled discard/coverage feature also invalidate Alpha, even if force-opaque later sets the scalar to one. Otherwise a consumer could mistake “scalar one” for proven visible coverage.

No adapter result directly invokes or changes `TriangleAlphaClassifier`. A future resolver stays responsible for pixels, mesh UVs, and exact classification.

### Normal

| Pinned state | Result |
| --- | --- |
| `_BumpMap` absent | `Unmodified` because the pinned ShaderLab default is `"bump"` |
| `_BumpMap` assigned under the conditions below | `TangentSpaceNormalMap(sample)` |

An assigned normal requires all of these:

- `_BumpScale == 1`
- `_BumpMapUV` in 0–3
- zero `_BumpMapPan`
- `_BumpMapStochastic == 0`
- stable texture identity
- a supported main sampler
- a `TextureImporter` that identifies a normal-map asset without green-channel inversion

At unit strength this is the existing canonical tangent-space normal role. Unity's platform storage swizzle is an import detail. It is not a new semantic expression.

Detail normals, RGBA-mask normal replacement, decals that perturb normals, parallax-derived normal changes, non-unit intensity, and non-OpenGL inversion make Normal unknown. So does any other traced normal writer. BaseColor, Alpha, and Emission stay independently eligible.

### Emission

The initial support is intentionally smaller than the four-layer model of Poiyomi.

| Pinned state | Result |
| --- | --- |
| All four emission slots disabled | `Constant(Vector3.zero)` |
| Only slot 0 enabled, no assigned map, simple controls | `Constant(linear(_EmissionColor.rgb) * _EmissionStrength)` |
| Only slot 0 enabled, assigned map proven to sample alpha as one, simple controls | `TextureSample` or `TextureSampleTimesConstant` for map RGB |

“Simple controls” means all of these:

- Slots 1–3 are disabled.
- Theme, use-base-color-as-map, replace-base-color, fluorescence, center-out, scrolling, blinking, hue, light-based emission, and AudioLink emission are off.
- Emission masks, the global emission mask, and all other slot modifiers are off.
- Slot 0 pan is zero and UV is 0–3.
- All values are finite.
- Source identity, main sampler, and sRGB importer evidence are available.

The source multiplies the RGB of an assigned emission map by the alpha of that same sample. A mapped emission is complete only when the importer proves that the source has no alpha channel and imports alpha as none. The sampled alpha is then exactly one. General RGBA emission maps are unknown. Multiple enabled slots are unknown, because their sum is not one of the existing closed color forms.

## Output-local invalidation and diagnostics

Identity failure is material-wide. After identity succeeds, the adapter evaluates each output separately. Each output gets either a complete value or one primary reason why it is unknown. Examples:

- an unsupported normal strength invalidates Normal only
- an RGBA emission map invalidates Emission, and `_EmissionReplace0` also invalidates BaseColor
- Gamma project color space invalidates BaseColor and Emission, but not otherwise supported Alpha or Normal
- an unsupported main sampler invalidates assigned MainTex consumers and assigned auxiliary-texture consumers, but not constant outputs
- an unstable emission texture identity invalidates Emission only

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

A diagnostic records the output, the code, and an ordinal property/evidence detail string. The ordering is deterministic: Material, BaseColor, Alpha, Emission, Normal. Within an output, the interpreter reports the first failed rule in documented evaluation order: value/form, feature gate, UV, sampling, identity, import/color interpretation. Diagnostics are data. The adapter does not write to the Unity Console.

## Malformed and unsupported inputs

- A `null` material throws `ArgumentNullException`.
- A destroyed material or a missing shader throws `ArgumentException`. The caller did not supply an analyzable material object.
- A valid non-Poiyomi material, an unsupported Poiyomi version or variant, a locked shader, modified source, inaccessible source, or a source-evidence mismatch returns an unsupported result. It does not throw.
- Valid but unrepresentable material values return partial `Unknown` outputs with diagnostics.
- An asset-database or importer lookup failure for a valid transient texture is uncertainty. It is not an exception.

Expected uncertainty never becomes a default value. It never makes optimization more aggressive.

## Public and private validation strategy

Poiyomi is MIT-licensed. But AMUSE does not need a copy of its large feature-rich shader. Ordinary CI does not need to download it. Public deterministic tests:

1. use a tiny purpose-built test ShaderLab fixture with only the relevant property contract
2. call the internal verified-material interpreter seam to exercise exact material, default, feature, and UV equations
3. create tiny temporary texture assets to verify GUID/local-ID identity, ST, sampler, importer sRGB, alpha, and normal-map behavior
4. test identity and normalized hashing separately with purpose-built source text and pinned constants
5. test every supported form and one adversarial case for each refusal category
6. prove output-local invalidation and deterministic diagnostics
7. run the complete existing EditMode suite to protect the classifier, geometry, planner, and semantic-core contracts

The fixture is an executable specification of the traced equations. It is not a pretend Poiyomi distribution. A test must first fail for the intended missing behavior. Then add the production code. Then run the same focused test and see it pass.

Optional integration validation can use an already installed official `com.poiyomi.toon@9.3.64` package and purpose-built materials. The private avatar testbed allows read-only inspection only after you confirm its project root. It is not required for CI. It gives no publishable fixture. Do not modify it. Locked-avatar coverage stays unsupported in this milestone.

## Relationship to consumers and future stages

The adapter constructs the existing types. It adds no optimization annotations. A future alpha resolver can turn a complete Alpha texture expression into immutable pixels plus mesh UV evidence. It then invokes the unchanged classifier. A future atlas or material-combination analyzer can compare `TextureSourceId`, `UvMapping`, and texture-times-tint structure. It must treat an unknown relevant output as refusal. It must also account separately for roles that v1 does not represent. This milestone implements none of those consumers.

Animation/state analysis will later construct or compare several resolved results. Modifier analysis will later establish whether a base material is still the effective state. These are upstream evidence stages. They are not symbolic data inside this adapter.

## Pressure on the semantic core

The existing core is enough for the selected BaseColor, Alpha, constant/simple Emission, and Normal subset. Do not change it in this milestone.

| Observed pressure | Category | Decision |
| --- | --- | --- |
| Emission RGB multiplied by the same sample's alpha | A — safely defer. Possibly C after a second concrete consumer. | General RGBA emission maps remain Unknown. Do not add a shader-specific closed form yet. |
| Addition of up to four emission layers | A — safely defer. | Multiple enabled layers remain Unknown. |
| Gamma-workflow color arithmetic | A — safely defer. | Color outputs require a Linear project. |
| Mips, anisotropy, mirror modes, or independent U/V wrap | A — safely defer. | Affected sampled outputs remain Unknown. |
| Arbitrary generated/locked shader expressions | A, not D. | Refuse the shader. No expression DAG or evaluator. |

Category B is empty. No issue requires a generic Unity extraction boundary before a second adapter. Category C has no justification yet. One Poiyomi-only need is not enough evidence for a new semantic form. Category D is not reached.

This is the YAGNI gate. Direct Unity extraction stays in the Poiyomi producer. The friend-test seam is concrete and local. The design creates no generic adapter infrastructure for hypothetical lilToon or future consumers.

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

The production surface is one internal file, one result, one diagnostic value, two small enums, and private extraction helpers. The dominant complexity is the audited feature-gate table, not class structure. Tests add one original minimal shader fixture and one test class. The design adds no package or assembly.

Known risks:

- Exact source hashing intentionally rejects harmless downstream edits and every patch release until review.
- Unlocked-only support can cover few production avatars, because most authors lock their shaders.
- A missed writer in the pinned shader would be a false completeness bug. Source-block tracing and adversarial tests are therefore release-critical.
- Unity texture importer and platform behavior must match the declared sample meaning. Normal decoding and source-alpha absence are the critical cases.
- Engineers must verify linear Color-property parity in Unity. Inspector appearance is not proof.
- The design refuses optional effects that are enabled but visually zero. An exception applies only when the source equation proves that the zero collapses the entire contribution.
- The result is only a base-state interpretation. It can become stale if the material mutates or another system later changes effective state.

## Adversarial review conclusions

| Case | Outcome | Reason |
| --- | --- | --- |
| Null material | Malformed-input exception | No material object exists to analyze. |
| Destroyed material or missing shader | Malformed-input exception | Unity object state violates the entry contract. |
| Non-Poiyomi shader | Unsupported whole material | Valid input, wrong producer. |
| Poiyomi-looking name, alternate shader, old/new version, edited source, upgrade drift, or locked/generated shader | Unsupported whole material | Exact source proof fails. Property resemblance is not evidence. |
| Required property absent from purported pinned source | Unsupported whole material | The source/schema proof is contradictory or modified, not a runtime feature case. |
| Undefined enum-like value or unsupported enabled feature | Unknown for every traced affected output | Valid material state, unrepresentable behavior. |
| Unsupported enabled feature whose write scope cannot be proven | Unknown for all potentially affected outputs | Scope uncertainty fails closed even if the feature currently appears visually inactive. |
| Enabled feature multiplied by a proven exact zero | Complete only if the pinned equation proves that the whole contribution collapses and no side effect remains. Otherwise Unknown. | “Looks inactive” is not proof. |
| Missing MainTex or BumpMap | Complete only for the source-proven white/bump fallback paths | The defaults come from pinned ShaderLab, not a global assumption. |
| MainTex × tint or texture alpha × opacity | Complete under the per-output simple profile | These are exact existing closed forms. |
| UV1 or finite non-identity ST | Complete when the selected path uses ordinary `poiUV` | Both facts map exactly to `UvMapping`. |
| Per-axis wrap mismatch, Trilinear, Mirror, mips, or unsupported anisotropy | Unknown for consuming sampled outputs | V1 sampling cannot express the behavior. |
| Non-default normal strength | Normal Unknown | The canonical v1 normal has no strength operation. |
| Multiple emission layers or RGBA emission map | Emission Unknown. BaseColor also Unknown if replacement is enabled. | Addition and RGB × same-alpha are outside v1. |
| Unsupported emission-only modifier | Emission Unknown. Other outputs independently evaluated. | The pinned source confines its write. |
| Non-asset/generated texture | Unknown for each consumer of that texture | No stable source token. No instance-ID fallback. |
| Two materials or repeated extraction using the same asset/state | Structurally equal complete semantics | GUID/local ID and immutable values are deterministic. |
| Explicit value equal to the ShaderLab default | Same outcome as the same resolved default value | This adapter interprets one resolved state, not serialization provenance. |
| Color-space/import mismatch | Unknown for affected color outputs | Linear-light meaning cannot be proven. |
| Unknown external modifier scope | All potentially affected outputs Unknown at the future modifier boundary | Base-material interpretation alone cannot prove effective state. |

The design was checked against these failure modes:

- **Renamed or forked shader:** the exact name alone is not enough. The GUID and the normalized source hash fail closed.
- **Same version label with edited source:** the hash mismatch rejects it.
- **Official legacy install:** package metadata can be absent. But the GUID plus the hash still proves the source.
- **Locked shader:** the design rejects the generated source, name, and property state. It does not treat them as canonical.
- **Missing main texture:** only the source-proven white/bump defaults become constants. The adapter does not guess auxiliary sampler state.
- **Texture path rename:** GUID/local-ID identity stays stable.
- **Transient texture:** the affected output becomes unknown. The design never serializes an instance ID into semantics.
- **U/V wrap mismatch or trilinear/mipped texture:** v1 cannot express it. So sampled outputs are unknown.
- **Gamma project:** linear-light BaseColor and Emission are unknown. The meaning of the core does not change.
- **Emission map with alpha:** the design does not misreport its RGB-times-alpha equation as plain RGB.
- **Force opaque plus dissolve/coverage:** the design does not promote a scalar of one while independent visibility effects remain.
- **Unsupported feature in one role:** output-local invalidation prevents unnecessary loss of unrelated proof.
- **Modifier after extraction:** the API name and the documented precondition stop a base-material result from claiming effective avatar state.

The main practical limitation is intentional. Many deployed Poiyomi materials are locked. So initial real-avatar coverage can be low. That is better than a claim of exact semantics for generated shaders without proof. Evidence from this unlocked adapter should guide whether locked-source interpretation or a small new semantic form is worth a later milestone.
