# Official lilToon 2.3.4 integration matrix investigation

**Status:** source-side characterization is complete. Implementation is not authorized.

**Branch:** `investigate/liltoon-official-integration-matrix`

**Base:** `origin/main` at `1a5c9d7` (`Merge pull request #8 from Alrauna/fix/liltoon-source-attestation-hardening`), containing hardening commit `8eaa4f428f1a278b06d9df1f7f7480b806ffe2e1`

**Scope:** official lilToon 2.3.4 BRP base-opaque shaders with official LTCGI, AudioLink package, and external VRC Light Volumes integrations. This document characterizes evidence for later work. It does not add positive support and does not weaken the standalone attestation predicate.

## Executive conclusion

For the exact current stable package sources selected below, all eight activation states have closed, union-shaped physical shader-source graphs. The three integrations occupy a fixed order. The selected VRC Light Volumes and AudioLink sources do not define any of the macros that select LTCGI code. Enabling them therefore does not change the effective LTCGI source in the characterized states.

All seven integrated states carry classification **A: safely composable**. This conclusion is deliberately narrow. It is true only when AMUSE proves the exact activation records and positions, exact lilToon 2.3.4 Layer-1 evidence, exact package identities, exact external executable closures, and absence of uncharacterized source or macro inputs. A matching package version without matching executable source is not sufficient.

The likely implementation shape is **A: one fixed official-integration validator with composable exact integration evidence**. It needs no registry, provider API, generic profile framework, or general HLSL preprocessor. Two exact canonical base/pass digest pairs suffice in the observed matrix: the existing standalone pair when LTCGI is absent, and the already-characterized LTCGI pair when it is present.

Production work should not start yet. AMUSE still lacks an authoritative lifecycle point that establishes when its analysis occurs relative to package resolution, lilToon generation, integrations, NDMF/build preprocessors, and later mutations. AMUSE also lacks a guarantee that all evidence comes from one coherent material/generated-source/package/external-source state. Those related but distinct boundaries are outside this investigation. They are prerequisites for sound positive support. As a result, this branch does not include an implementation plan.

## Central security answer

Two characterized shader states are equivalent enough for AMUSE to trust lilToon semantics only if all of the following are equal:

1. The existing lilToon 2.3.4 Layer-1 identity, package, shader GUID, version, include-tree, render-mode, and output-local semantic evidence.
2. The exact Layer-2 activation tuple, including raw record text, count, and characterized position.
3. The applicable exact canonical generated base/pass digest pair.
4. Each active external package's exact package name and selected version.
5. Every file in each active external executable shader-source closure, by package-relative path and exact-byte digest, including all transitive includes.
6. The characterized include edges and order that connect generated lilToon source to those closures.
7. The small fixed macro-provenance facts described below, especially the absence of uncharacterized LTCGI control macros.
8. The absence of additional external includes or other third-party shader source in the active graph.
9. A coherent, immutable snapshot: the material, generated lilToon source, package resolution, and external source files must describe the same build state.

Any mismatch or missing fact is `Unknown`. In that case, apply no optimization.

## Exact upstream versions

| Component | Selected version | Exact upstream revision | lilToon acceptance gate | Selection rationale |
|---|---:|---|---|---|
| lilToon | 2.3.4 | [`252fd8cfc46106d4967e95b3f2c788418502f227`](https://github.com/lilxyzw/lilToon/tree/252fd8cfc46106d4967e95b3f2c788418502f227) | target itself | Existing AMUSE target and PR #8 baseline. |
| LTCGI | 1.7.3 | [`b2014d6c6e76c551c30084973e54687941265d68`](https://github.com/PiMaker/ltcgi/tree/b2014d6c6e76c551c30084973e54687941265d68) | package `at.pimaker.ltcgi`, expression `1.4` | Current stable release. Its release specifically addresses current VRC Light Volumes integration compatibility. |
| AudioLink | 3.1.2 | [`5bd23af5b2aaefff1ac3f48379332f6f78f17f97`](https://github.com/VRChatCommunity/AudioLink/tree/5bd23af5b2aaefff1ac3f48379332f6f78f17f97) | package `com.llealloo.audiolink`, expression `2` | Current official release and current VRChat curated package version. |
| VRC Light Volumes | 2.1.3 | [`7ead7482f40b9612e6e4faafae835ffd9a73e149`](https://github.com/REDSIM/VRCLightVolumes/tree/7ead7482f40b9612e6e4faafae835ffd9a73e149) | package `red.sim.lightvolumes`, any version | Current stable 2.x release. The project's compatibility documentation identifies lilToon support from 2.0.0. Version 3.0 remains a development line rather than this target. |

The lilToon version expressions are compilation/activation gates, not executable-identity claims. They accept more versions than this investigation attests. Near-term positive support should select the exact versions above at first.

The package manifests' exact-byte SHA-256 values in the characterized revisions are:

| Package | Manifest SHA-256 |
|---|---|
| lilToon 2.3.4 | `92dd582d71d594493268ecc2e6ca6c664ec4f30098aa7ad91672d686ba4cbe5e` |
| LTCGI 1.7.3 | `1de0826df1cfa9d98cb5e6cbc25f95a6e7666bca61cff70174e77ed3f2302498` |
| AudioLink 3.1.2 | `481b26866a32b886f8257175336af331d60686af6a626d4fa1bc796c22bd761d` |
| VRC Light Volumes 2.1.3 | `40a3eee02b0dfcae2c70422ac4f199b3aee47e6fa46a06427c7e3dd54f18e8c5` |

These hashes give useful package-identity evidence, but executable attestation must also use the smaller source closures below.

### Characterized adjacent versions

The following adjacent releases have byte-identical active lilToon shader closures. AMUSE did not run them through the complete genuine eight-state matrix. They are legitimate candidates for later expansion, not near-term positive targets:

| Integration | Versions with selected closure bytes | Executable closure digest or file digest | Disposition |
|---|---|---|---|
| LTCGI | 1.7.0, 1.7.1, 1.7.2, 1.7.3 | `0454de0f3d8dd84070e2ec518e3041d972fc4ffd075adb87f7c1c3bfb92d4165` | 1.7.3 selected. Older variants deferred. |
| AudioLink | 3.1.1, 3.1.2 | `AudioLink.cginc` `f0d6a26e714e8f0da1fd1691226c61a7603280382d3d7c20038f39e59f34f04f` | 3.1.2 selected. 3.1.1 deferred. |
| VRC Light Volumes | 2.1.0, 2.1.1, 2.1.3 | `LightVolumes.cginc` `da3b6294a0ac533dd717d2b12a43443797760b67a43dd4cdabb0cd116d30157d` | 2.1.3 selected. Earlier 2.1 variants deferred. |

Other inspected AudioLink releases have different active-source hashes: 2.0.0 `0da220e...`, 2.1.0 `1da2296...`, and 3.1.0 `6952243...`. VRC Light Volumes 2.0.0 (`93cda975...`) and 2.0.1 (`5b28777c...`) also differ. The truncated values here are comparison notes, not proposed pins. These versions need separate characterization if the project ever prioritizes them.

### Pinned primary-source evidence

The version and graph conclusions above were checked against these exact upstream files, not inferred from package presence:

- lilToon 2.3.4 [`lilToon.Editor.asmdef`](https://github.com/lilxyzw/lilToon/blob/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/Editor/lilToon.Editor.asmdef) supplies the three Unity version defines and version expressions.
- lilToon 2.3.4 [`lilToonSetting.cs`](https://github.com/lilxyzw/lilToon/blob/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/Editor/lilToonSetting.cs) emits the Light Volumes and AudioLink package R1 records.
- lilToon 2.3.4 [`lilShaderContainerImporter.cs`](https://github.com/lilxyzw/lilToon/blob/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/Editor/lilShaderContainerImporter.cs) emits the two LTCGI records outside R1.
- lilToon 2.3.4 [`lil_pipeline_brp.hlsl`](https://github.com/lilxyzw/lilToon/blob/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/Shader/Includes/lil_pipeline_brp.hlsl), [`openlit_core.hlsl`](https://github.com/lilxyzw/lilToon/blob/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/Shader/Includes/openlit_core.hlsl), [`lil_common_input.hlsl`](https://github.com/lilxyzw/lilToon/blob/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/Shader/Includes/lil_common_input.hlsl), and [`lil_common_functions_thirdparty.hlsl`](https://github.com/lilxyzw/lilToon/blob/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/Shader/Includes/lil_common_functions_thirdparty.hlsl) establish the external include edges and order.
- The exact external executable inputs are AudioLink 3.1.2 [`AudioLink.cginc`](https://github.com/VRChatCommunity/AudioLink/blob/5bd23af5b2aaefff1ac3f48379332f6f78f17f97/Packages/com.llealloo.audiolink/Runtime/Shaders/AudioLink.cginc), VRC Light Volumes 2.1.3 [`LightVolumes.cginc`](https://github.com/REDSIM/VRCLightVolumes/blob/7ead7482f40b9612e6e4faafae835ffd9a73e149/Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc), and LTCGI 1.7.3 [`LTCGI.cginc`](https://github.com/PiMaker/ltcgi/blob/b2014d6c6e76c551c30084973e54687941265d68/Shaders/LTCGI.cginc) plus its five transitive includes listed below.

## Activation and generated-source matrix

The line indices below are zero-based and match the current Layer-2 representation. `R1+N` means the record occurs at offset `N` within the raw R1 region. Official lilToon tooling generated every source. No generated source was hand-edited.

The exact activators are:

- AudioLink package: exactly one `#define LIL_FEATURE_AUDIOLINK_PACKAGE`, at `R1+102` when VRC Light Volumes is absent and `R1+103` when it follows the Light Volumes record.
- External VRC Light Volumes: exactly one `#define LIL_FEATURE_VRCLIGHTVOLUMES`, at `R1+102`.
- LTCGI: exactly two `#define LIL_FEATURE_LTCGI` records outside R1, each immediately before an exact `#define LIL_PASS_FORWARD` record. The default line indices are 791 and 840 without R1 integrations, 792 and 841 with one, and 793 and 842 with both.

| State | Tuple (L/A/V) | R1 activation evidence | LTCGI evidence | Base canonical SHA-256 | Pass canonical SHA-256 | Default raw pass SHA-256 | Classification | Positive feasibility |
|---:|---|---|---|---|---|---|---|---|
| 1 | 0/0/0 | none | none | `5206bec25e82db5f8009b27fcc5ba94d7c41113031d4b6b0a2c25ca324a9c704` | `6b6c30c1cbe546fe753bcdc77f547441e3f9114ee80e9591bde2b8e6e7e5eb14` | `2221d2dbd1782de9f2371012b58a9d119294605a3f0be60bd9b734e808470171` | Existing control | Already supported |
| 2 | 1/0/0 | none | lines 791, 840 | `1a2ffc7fa6b3d54d5765de3c98ab1ff2e8ce7da4fd773e507c8c32568c369f56` | `c666f898543a5fe8ec39ac7374aa53b043d3c0e468a0943826bc022acfcbe5c2` | `a0a70214305c7209a26e1a10641c66993a88802bf99403b8b4c22594ee060ea7` | A | Yes, after analysis/snapshot boundary |
| 3 | 0/1/0 | AudioLink line 746, `R1+102` | none | standalone | standalone | `3829700a037c484031c6b838ece8e800398968435909cb02cd1b98477c9832f4` | A | Yes, after analysis/snapshot boundary |
| 4 | 0/0/1 | Light Volumes line 746, `R1+102` | none | standalone | standalone | `a3b3521f33442b8d28654c5102d1fdac166184ae89b1bd80470a313db3494689` | A | Yes, after analysis/snapshot boundary |
| 5 | 1/1/0 | AudioLink line 746, `R1+102` | lines 792, 841 | LTCGI | LTCGI | `ad6606c03183ff7d15a9d3a1a561803b9b36cc4271586adf8e02b2717741260a` | A | Yes, after analysis/snapshot boundary |
| 6 | 1/0/1 | Light Volumes line 746, `R1+102` | lines 792, 841 | LTCGI | LTCGI | `9e6012cf7e289cebdcf5bb503b2967aac157420634a158e8522231c63d3ad1d4` | A | Yes, after analysis/snapshot boundary |
| 7 | 0/1/1 | Light Volumes line 746, `R1+102`, then AudioLink line 747, `R1+103` | none | standalone | standalone | `9eb986ce557870e89cda05b8edb8e09f913e2ba61815964e7a5227711a5c4684` | A | Yes, after analysis/snapshot boundary |
| 8 | 1/1/1 | Light Volumes line 746, `R1+102`, then AudioLink line 747, `R1+103` | lines 793, 842 | LTCGI | LTCGI | `cc23ec7e4e7a225b8bec7838de3400d4188fdd9d55363330eddcc6f170ec5b81` | A | Yes, after analysis/snapshot boundary |

`standalone` and `LTCGI` in the digest columns refer to the exact full hashes shown in states 1 and 2. They are not loose aliases in a future predicate.

The generated base changes only for LTCGI, through the official `"LTCGI"="ALWAYS"` SubShader tag at zero-based line 639. AudioLink and VRC Light Volumes do not alter the base. Their pass changes are precisely their independent R1 records. LTCGI changes the pass at its two fixed forward-pass insertion sites. Pairwise and triple diffs show that the combined generated sources form the union of those independent records.

The existing standalone canonical pins survive every state without LTCGI. The same already-characterized LTCGI base/pass pins survive every state with LTCGI. A per-tuple canonical pin is therefore not justified.

## Genuine scratch characterization

AMUSE generated the empirical matrix in disposable projects under `/private/tmp`, using Unity exactly 2022.3.22f1 and embedded checkouts of the exact official revisions above. Each project installed one matrix tuple and invoked official lilToon shader-setting generation in batch mode. The investigation used the current AMUSE package only as a reference for reproducing its canonicalization and Layer-2 measurements. It did not change production code.

AudioLink's package uses Unity UI APIs. Its own manifest does not declare `com.unity.ugui`, while normal VRChat projects supply that host package. The initial AudioLink-only import exposed the omission. The four AudioLink scratch projects then received `com.unity.ugui` 1.0.0 as an ordinary Unity host dependency, and all eight projects completed batch generation successfully. That host dependency is not part of the active shader-source closure.

The all-integrations project was also regenerated after twelve ordinary official lilToon settings, used by the prior hardening investigation, were disabled. Its pass retained the LTCGI canonical digest, had raw digest `d8fa4031c08f442f3f9995f7441b2167f49516082c8fe20e45107aebf845fbd6`, and retained the same structural activation order at its shifted positions: Light Volumes 733, AudioLink 734, and LTCGI 780/830. Re-enabling all settings reproduced the default all-integrations digest in the table above. This shows that integration identity is positional and structural, not tied to default material feature settings.

The scratch projects are disposable research instruments, not repository fixtures. The investigation did not use or modify the Census Lab or its private avatars.

## Source-closure maps

### VRC Light Volumes 2.1.3

```text
generated ltspass_opaque.shader
  -> LIL_FEATURE_VRCLIGHTVOLUMES
  -> lil_pipeline_brp.hlsl
  -> openlit_core.hlsl
  -> Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc
```

The active external closure is the single file `Shaders/LightVolumes.cginc`, exact-byte SHA-256 `da3b6294a0ac533dd717d2b12a43443797760b67a43dd4cdabb0cd116d30157d`. It contains no transitive `#include`. The package-relative closure-list digest is `b16977e4e8d5bf9b169ed2ed3a82bd6b944d9b1b5dd9df4a2543b3dfb584c7c4`.

Other package shaders, compute shaders, scripts, editor tools, and samples do not enter the characterized lilToon compilation graph and stay outside the executable shader closure. Their absence from the closure does not make package version/manifest identity optional.

### AudioLink 3.1.2

```text
generated ltspass_opaque.shader
  -> LIL_FEATURE_AUDIOLINK_PACKAGE
  -> lil_common.hlsl
  -> lil_common_input.hlsl
  -> Packages/com.llealloo.audiolink/Runtime/Shaders/AudioLink.cginc
```

The active external closure is the single file `Runtime/Shaders/AudioLink.cginc`, exact-byte SHA-256 `f0d6a26e714e8f0da1fd1691226c61a7603280382d3d7c20038f39e59f34f04f`. It contains no transitive `#include`. The package-relative closure-list digest is `dfce3a8e8054e492a798de6db9762c39353c6695bd041870e0532325ef49d911`.

Other AudioLink shaders, prefabs, runtime scripts, editor tooling, examples, and assets do not enter the characterized graph.

### LTCGI 1.7.3

```text
generated ltspass_opaque.shader
  -> two fixed LIL_FEATURE_LTCGI forward-pass records
  -> lil_common_functions.hlsl
  -> lil_common_functions_thirdparty.hlsl
     -> Packages/at.pimaker.ltcgi/Shaders/LTCGI_structs.cginc
     -> Packages/at.pimaker.ltcgi/Shaders/LTCGI.cginc
        -> LTCGI_config.cginc
        -> LTCGI_structs.cginc (guarded duplicate)
        -> LTCGI_uniform.cginc
        -> LTCGI_functions.cginc
        -> LTCGI_shadowmap.cginc
```

| Package-relative file | Exact-byte SHA-256 |
|---|---|
| `Shaders/LTCGI.cginc` | `4cbee8f54c31c1b0c3a7ad1ef8eb55f83eea8e3574892095cb7720c0b30dae2e` |
| `Shaders/LTCGI_config.cginc` | `35d714af6eb486efad397b9e3e563f4da774703225228c8feb4f694454f3b4cd` |
| `Shaders/LTCGI_functions.cginc` | `f94e0cb1dae947f9272f5b5ec30ac3dbd177d33120d1d4bc27472577ffd7be58` |
| `Shaders/LTCGI_shadowmap.cginc` | `40cee2807ddcec818c12d98e556b40f9a328e5fcca59756e84724b9ef542c8bb` |
| `Shaders/LTCGI_structs.cginc` | `4e5f834ab4feb4048a25aef508d028a219b7accdb3d7d2f7904d58b8fc955b6e` |
| `Shaders/LTCGI_uniform.cginc` | `7ee902d686aa6cb7cb49e75d367ff7ad389e82898fb2a0d70db88f188a8d4216` |

The deterministic six-file package-relative closure-list digest is `0454de0f3d8dd84070e2ec518e3041d972fc4ffd075adb87f7c1c3bfb92d4165`.

For every closure-list digest in this document, the recipe is reproducible and deliberately simple. Compute the exact-byte SHA-256 of each file. Form `package/relative/path:lowercase-hex-digest` rows. Sort the rows by ordinal package-relative path. Join them with one LF and no final LF. Then compute the SHA-256 of the resulting UTF-8 bytes. A future implementation may instead compare the fixed path/digest records directly.

`Shaders/LTCGI_AudioLinkNoOp.cginc` is not active, because the characterized generated/lilToon sources never define `LTCGI_AUDIOLINK`. Its observed hash was `b88a...`, but this is not a proposed pin. A state that activates `LTCGI_AUDIOLINK` is a different, currently unsupported integration. LTCGI demo shaders, Amplify assets, scripts, and examples likewise stay outside the active closure.

## Include order and macro provenance

The exact BRP order in the selected sources is:

```text
external VRC Light Volumes
  -> remaining lilToon pipeline/common source
  -> external AudioLink
  -> lilToon functions and third-party bridge
  -> external LTCGI
```

That order creates a potential macro channel, so physical file hashes alone would not prove composition. AMUSE audited the channel rather than assuming it was harmless.

| Integration | Relevant consumed state | State exported downstream | Cross-integration result |
|---|---|---|---|
| VRC Light Volumes 2.1.3 | Its include guard and `SHADER_TARGET_SURFACE_ANALYSIS` | `VRC_LIGHT_VOLUMES_INCLUDED`, `VRCLV_VERSION=2`, fixed maximum-count definitions, and `LV_*` definitions/functions | Defines no AudioLink or LTCGI selector/control macro. Exact file identity is still required, because modified earlier source could create one. |
| AudioLink 3.1.2 | Include guard, lilToon's `glsl_mod`, `SHADER_TARGET_SURFACE_ANALYSIS`, and `SHADER_API_GLCORE` | `AUDIOLINK_CGINC_INCLUDED`, `AUDIOLINK_WIDTH=128`, ALPASS/data definitions, and functions | Does not define `LTCGI_AUDIOLINK` or another audited LTCGI control. Its exported names cannot select different LTCGI code in the characterized state. |
| LTCGI 1.7.3 | lilToon callback macros plus LTCGI controls including avatar/toggle/off, v2 callbacks, surface-analysis, visualization, `LTCGI_AUDIOLINK`, sampler, static/fast/cylinder modes | LTCGI structs, uniforms, functions, and guards | lilToon defines the exact v2 callback bridge immediately before the LTCGI includes. The selected earlier closures define none of LTCGI's optional control macros. `LTCGI_AUDIOLINK` is absent. |

This falsifies the concern that ordinary AudioLink package activation silently changes the selected LTCGI code: it does not set LTCGI's separate AudioLink selector. It also sets the limit of the conclusion. A locally modified AudioLink or Light Volumes file could define a downstream selector without changing its own apparent feature activation. Exact closure hashes reject that state. A known LTCGI closure combined with different earlier source or different generated macro state is not equivalent.

This case needs no generic preprocessing reconstruction. A future predicate can use fixed facts:

- exact generated activator records and structural positions\.
- exact selected earlier closure hashes\.
- exact pinned lilToon include tree and include order\.
- exact LTCGI callback bridge source already inside the pinned lilToon tree\.
- absence of all specifically audited optional LTCGI control activators, including `LTCGI_AUDIOLINK`\.
- absence of extra external include edges.

Macros that the compiler supplies remain part of the host boundary. This investigation targets Unity 2022.3.22f1, BRP, ordinary runtime shader compilation, and the existing base-opaque profile. Surface-analysis and other compiler modes are not new positive profiles.

## Composability analysis

For each pair/triple tuple:

| Tuple | Physical closure union? | Earlier integration changes later active code? | Generated changes beyond activators? | Evidence interaction | Result |
|---|---|---|---|---|---|
| LTCGI + AudioLink | Yes | No. `LTCGI_AUDIOLINK` remains absent | No | Exact AudioLink closure and absent LTCGI selector close the macro channel | A |
| LTCGI + Light Volumes | Yes | No audited LTCGI control is exported | No | Exact Light Volumes closure closes the earlier macro channel | A |
| AudioLink + Light Volumes | Yes | No AudioLink selector is altered | No | Fixed order and exact two single-file closures | A |
| LTCGI + AudioLink + Light Volumes | Yes | No, for the exact selected sources | No | Conjunction of the same fixed facts, with no new tuple-specific source | A |

The individual-only states are also A. The exact selected eight-state matrix contains no B or C states. This does not authorize arbitrary mixing of versions. AMUSE proves every tuple as one conjunction of exact evidence, and any unselected version or new macro fact remains D (`Unsupported/unclosed`) until separately characterized.

## Proposed future trust predicates

A subsequent branch should prefer one concrete validator with three fixed optional integration evidence blocks:

- **Standalone bit:** all three external activators absent. The existing standalone predicate stays unchanged.
- **Light Volumes bit:** one exact R1 record at the characterized slot, exact package/version/manifest, exact single-file closure, exact include edge, and bundled `LIL_FEATURE_VRCLIGHTVOLUMES_WITHOUTPACKAGE` not simultaneously represented.
- **AudioLink bit:** one exact R1 record at the characterized slot (after Light Volumes when both are active), exact package/version/manifest, exact single-file closure, and exact include edge.
- **LTCGI bit:** exactly two records outside R1 at the two characterized forward-pass sites, exact LTCGI base tag, exact package/version/manifest, exact six-file closure and include graph, exact lilToon callback bridge, and absence of uncharacterized LTCGI controls.

The canonical source predicate selects one of two existing exact pairs based only on the LTCGI bit. The overall predicate accepts only one of the explicitly characterized eight activation tuples and requires the exact union of the active closure blocks. This is composable evidence, not a generic extension system.

PR #8's evidence representation is substantially sufficient. R1 raw records and global activator occurrences already represent the activation language without baking in standalone absence. The smallest production extension appears to be fixed external package/source-closure evidence for these three packages. A direct check against the already-retained canonical source can validate LTCGI's adjacency to `LIL_PASS_FORWARD`. A generic syntax model is unnecessary. If implementation shows that repeated direct inspection is awkward, the only justified Layer-2 addition would be fixed predecessor/successor context on an activator occurrence.

Package installation without activation is not itself executable participation. An exact standalone generated state may continue through the standalone predicate even if an inactive optional package is installed, as long as the active shader graph and all standalone evidence remain exact. Do not mislabel it as a positive integrated state.

## Negative and falsification witnesses

Future tests should mutate one trust fact at a time and require `Unknown`/no optimization:

| Witness | Required failure reason |
|---|---|
| Correct activator, wrong package version | Selected exact package identity/version is absent. |
| Package installed, integration not activated | Integrated predicate does not match. Only exact standalone may apply if no external source participates. |
| Activator present, package/source missing | Required include edge and closure cannot resolve. |
| Modified external executable file | File and closure digest mismatch. |
| Missing transitive LTCGI file | Closure is incomplete. |
| Unexpected extra external include | Active graph is not the exact characterized graph. |
| Duplicate activator | Exact global count/removed-record structure fails. |
| Relocated activator | Exact R1 slot or LTCGI forward-pass adjacency fails. |
| Malformed activator | Exact raw record text and generator-emittable-language validation fail. |
| Uncharacterized activation combination or version mixture | No selected fixed tuple predicate matches. |
| External and bundled Light Volumes represented together | Mutually exclusive exact predicate fails. |
| LTCGI define at unexpected location/count | Two-site structural predicate and base-tag evidence fail. |
| AudioLink activation with inconsistent macro state | Exact generated record, earlier source, and AudioLink closure conjunction fails. |
| Additional third-party shader code | Exact include tree/edge set fails. |
| Same apparent feature state with substituted package source | Package revision/manifest and closure hashes fail. |
| Known LTCGI bytes with different upstream macro state | Exact generated source and all earlier closure evidence fail. LTCGI bytes alone are insufficient. |
| `LTCGI_AUDIOLINK` or another uncharacterized LTCGI control appears | Characterized absence predicate fails. |
| Source changes between evidence collection and use | Coherent-snapshot prerequisite fails, and AMUSE must discard the evidence. |

No heuristic equivalence, semantic guess, version range, or package-presence shortcut is acceptable.

## Intended support scope

### Near-term primary targets

- lilToon 2.3.4 with LTCGI 1.7.3, AudioLink 3.1.2, and VRC Light Volumes 2.1.3.
- each integration independently.
- all four pair/triple combinations.
- the existing standalone control.
- only the BRP base-opaque profile and the exact snapshot described here.

### Characterized but deferred

- LTCGI 1.7.0-1.7.2, whose active six-file closure matches 1.7.3.
- AudioLink 3.1.1, whose active include matches 3.1.2.
- VRC Light Volumes 2.1.0 and 2.1.1, whose active include matches 2.1.3.
- any combination that needs further work to establish the authoritative analysis lifecycle or a coherent evidence snapshot.

### Intentionally unsupported

- AudioLink 2.x and 3.1.0, VRC Light Volumes 2.0.x or 3.0 development versions, unsupported historical LTCGI variants, and any uncharacterized release\.
- `LTCGI_AUDIOLINK`, arbitrary LTCGI compile controls, or other macro overlays\.
- locally modified or substituted packages, custom shaders, transformed derivatives, arbitrary lilToon extensions, extra third-party includes, and unknown package overlays\.
- any unclosed source graph or state whose effective upstream macro inputs cannot be proved.

## Analysis lifecycle and snapshot assumptions

### Ordering and lifecycle boundary

Every conclusion assumes AMUSE has an authoritative analysis point with all of these lifecycle properties:

1. Unity has resolved the exact packages and version defines\.
2. official lilToon tooling has generated `lts.shader` and `ltspass_opaque.shader` for the material/settings state\.
3. integration tooling has made all intended source and material changes\.
4. no later tool will regenerate, swap, or mutate the material, generated source, include graph, or external files before compilation/use.

If a lilToon build preprocessor, another NDMF pass, or any later transformation can change this evidence after AMUSE analyzes it, the predicate is unsound regardless of its hash coverage.

### Snapshot coherence and TOCTOU boundary

Ordering alone is not sufficient. The material state, generated lilToon sources, package resolution, external executable closures, include graph, and relevant semantic evidence gathered by AMUSE must all describe one coherent build state. An analysis that reads those inputs in the correct order but at mutually inconsistent times would still be unsound. Package and source evidence must therefore stay coherent through the decision and use of the attestation result, and so avoid a time-of-check/time-of-use mismatch.

The previous investigation found no authoritative analysis/lifecycle contract, and the coherent-snapshot guarantee is likewise unresolved. This work does not solve either boundary. Establishing both is the remaining prerequisite before positive implementation can safely proceed.

## Remaining unknowns

- The authoritative Unity/NDMF/build lifecycle point at which AMUSE can observe and rely on the characterized state.
- How AMUSE can prove that material, generated-source, package, external-source, include-graph, and semantic evidence came from one coherent build state, without this investigation prescribing an API.
- Whether the deferred byte-equivalent adjacent versions merit full scratch-matrix validation and near-term support.
- Whether Unity platform/compiler macro variation outside the exact Unity 2022.3.22f1 BRP target needs an additional host predicate. This document makes no wider claim.

These unknowns do not undermine the source-side A classifications. They block turning those classifications into production attestations until AMUSE proves both the lifecycle-ordering and coherent-snapshot boundaries.

## Final recommendation

Use implementation shape **A** once the analysis-lifecycle and coherent-snapshot prerequisites are resolved: a single fixed official-integration validator, three concrete exact evidence blocks, an explicit eight-tuple activation language, and two canonical digest pairs selected by the LTCGI activation fact. Reject everything else as `Unknown`.

Do not build atomic per-combination profiles, a generic registry/provider/plugin architecture, or a general preprocessor. The observed combinations do not justify them.

No implementation plan accompanies this document, because the authoritative analysis lifecycle and coherent evidence snapshot remain architectural prerequisites. Production attestation code, semantic adapters, census tooling, NDMF ordering, and unrelated optimizer areas are unchanged on this branch.
</content>
