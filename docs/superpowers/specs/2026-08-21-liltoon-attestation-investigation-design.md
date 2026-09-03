# lilToon Attestation Investigation Design

**Date:** 2026-08-21

**Status:** The ecosystem and mechanism audit is complete. The architecture is closed for focused standalone hardening. Integrated profiles stay unsupported. Production work stays stopped.

**Branch:** `investigate/liltoon-attestation` from `origin/main` at `cacc481`

## Decision

A genuine `jp.lilxyzw.liltoon` 2.3.4 installation fails AMUSE attestation when the project also installs LTCGI. lilToon's official generator detects the `LILTOON_LTCGI` compile symbol. It then makes three source changes that the existing pins did not characterize:

- it appends `"LTCGI"="ALWAYS"` to the base shader's `SubShader` tags.
- it emits `#define LIL_FEATURE_LTCGI` in the base forward pass.
- it emits the same define in the outline forward pass inside the shared opaque pass shader.

These are the only differences after AMUSE's existing canonicalization. Remove only the tag from the in-memory canonical base shader, and its digest changes from the Lab value to the existing pin. Remove only the two generated defines from the in-memory canonical pass, and the same happens for the pass pin. No Lab file changed to run that experiment.

The smallest safe response is **not** to ignore those lines. `LIL_FEATURE_LTCGI` activates an external HLSL include closure under `at.pimaker.ltcgi`. Package name and version alone cannot prove that active source stayed unmodified. A rule that simply drops the LTCGI tag and defines would trust an unpinned external preprocessor input. That would break AMUSE's fail-closed boundary.

The first recommended follow-up was one explicit compound attestation profile for:

> base opaque lilToon 2.3.4, generated with official LTCGI 1.7.3 integration, with both the generated lilToon digest pair and the exact active LTCGI include closure attested.

The approved fresh scratch cross-check confirmed that the proposed LTCGI generated digest pair stays stable across default and legitimately stripped lilToon settings. It also disproved one part of this design: the proposed seven-file external set was not the complete *active* closure. `LTCGI_AudioLinkNoOp.cginc` sits behind the inactive symbol `LTCGI_AUDIOLINK`. lilToon's distinct `LIL_FEATURE_AUDIOLINK` symbols do not activate it. The measured active closure has six files and a different digest.

That dependency difference triggered the first requested stop condition. The next preprocessor-provenance audit found a more basic blocker: an installed `com.llealloo.audiolink` package makes lilToon's generator emit `LIL_FEATURE_AUDIOLINK_PACKAGE` inside R1, where AMUSE deliberately removes it from the canonical pass digest. That symbol pulls in unattested `AudioLink.cginc` before lilToon's attested LTCGI entry. HLSL includes share one macro namespace. So that external file can define `LTCGI_AUDIOLINK`, or change another downstream control symbol, without changing the proposed base digest, pass digest, lilToon include-tree digest, or six-file LTCGI digest.

The required closed-world property is therefore **not proven, and is false for the proposed evidence set**. The six-file active-closure profile must not go into production in its current form. Keep the current standalone pins unchanged.

The continuation audit below finds a narrow but broader R1 boundary defect, not an LTCGI-only anomaly. Official lilToon 2.3.4 has exactly three upstream-declared external package shader-source activators for the supported BRP base-opaque target: `LIL_FEATURE_VRCLIGHTVOLUMES`, `LIL_FEATURE_AUDIOLINK_PACKAGE`, and `LIL_FEATURE_LTCGI`. R1 removes the first two from their official generated positions. Its wildcard also lets a third-party activator hide in that region, including `LIL_FEATURE_LTCGI`. This is not a claim that covers every arbitrary third-party modification. Every other characterized R1 variation stays inside already-attested lilToon source, conservatively handled output semantics, or structural generated evidence that remains hashed.

The smallest safe direction is therefore two conceptual evidence layers. Layer 1 keeps the present canonical semantic and generator evidence. Layer 2 independently measures the exact third-party integration activation record from raw generated source, before canonicalization. Every active contributed external source closure and its relevant upstream macro provenance must then be attested as part of one atomic profile. This is a fixed lilToon 2.3.4 characterization, not a generic integration framework. Until a specific integrated tuple completes that evidence gate, only standalone lilToon stays supported. This branch still makes no production change, widens no rule, and accepts no new digest.

The bounded ecosystem and mechanism audit refines that recommendation without widening it. Layer 2 must be a raw **canonicalization-provenance and integration-activation record**. Every token R1 removes must belong to the closed official 2.3.4 generator domain, and the three external activators must also satisfy the exact absence/presence, count, and structural rules of the selected profile. Official custom shaders and the representative build-time shader patcher found by this audit produce different assets or shader identities, and already refuse. Ordinary material-only transformations belong to the live semantic/material-state evidence layer. Unknown generator plugins, uncharacterized injection, and transformations that cannot be ordered before the evidence snapshot intentionally stay Unknown.

The investigation is now architecturally complete for a focused production hardening design that strengthens the existing standalone profile. It is not complete for any integrated profile: LTCGI, AudioLink, and external VRC Light Volumes each still need an approved activation tuple and closed source/provenance evidence. This branch creates no implementation plan before review.

## Scope and environment

The repository began clean on `main`. `origin/main` was fetched and it matched local `main`. The investigation branch was created directly from that commit. No pre-existing user changes were present.

Unity MCP discovery found two instances. Before every reported Unity run, routing was pinned using the exact instance identifier, and `Application.dataPath` was checked:

- the public development instance reported exactly `<repo-root>/Assets`, with identical case.
- the Census Lab instance reported `<Census-Lab-root>/Assets`, which is not the public path.

The investigation used the Lab only for read-only source inspection, in-memory evidence collection, in-memory `Material` construction and destruction, and one existing EditMode characterization test. This investigation wrote no scene, material asset, package, shader setting, generated shader, or project setting.

At each continuation gate, `git rev-parse --show-toplevel` again resolved this repository. The current branch was exactly `investigate/liltoon-attestation`. `HEAD`, `origin/main`, and their merge base were all `cacc4814582b6c655399b3bba31c378d024dd820`. The investigation document remained the only intended branch artifact. Unity regenerated only the prohibited host-specific `com.unity.toolchain.macos-arm64-linux-x86_64` and transitive `com.unity.sysroot*` entries in `Packages/manifest.json` and `Packages/packages-lock.json`. On each recurrence the investigation inspected the complete two-file diff, then restored those generated files to `HEAD`, as repository policy requires. No unrelated user change was present or altered.

## Observed failure

The investigation reproduced the existing production-path characterization in the positively identified Census Lab:

- package: `jp.lilxyzw.liltoon` 2.3.4.
- shader: `lilToon`.
- result: `AlphaFailure = SemanticsUnknown`.
- census attestation: `ShaderFamilyAttestation.None`.

The focused test

`Alrauna.Amuse.Research.Tests.Editor.Calibration.VendorReachabilityTests.LilToonIsNotAttestedInThisEnvironmentDespiteMatchingItsPin`

passed 1/1. Its pass confirms that the existing negative characterization still reproduces. It is not evidence that the behavior is desirable.

A separate read-only reflection probe called the production `GatherSourceEvidence` and `TryVerifyLilToonIdentity` methods directly. The verifier returned:

| Field | Value |
| --- | --- |
| verified | `false` |
| diagnostic output | `Material` |
| diagnostic code | `ModifiedShaderSource` |
| diagnostic detail | `lilToon` |

The detail matters. Verification fails on the canonical digest of the material's own shader. It does not reach the pass-digest or render-mode predicates.

## Current attestation chain

### Entry and frontend selection

`UnityMaterialSemantics.AnalyzeBaseMaterial(material)` runs one exclusive trial:

1. a null/destroyed material or missing shader returns all `Unknown`.
2. Poiyomi attestation and semantics run first.
3. if Poiyomi does not attest, `LilToonMaterialSemantics.AnalyzeBaseMaterial` runs.
4. an unsupported lilToon result is all `Unknown`.

`LilToonMaterialSemantics.AnalyzeBaseMaterial` calls `LilToonSourceAttestation.GatherSourceEvidence`, then `TryVerifyLilToonIdentity`. Only successful identity verification reaches `InterpretVerifiedMaterial`.

The census separately classifies a material as lilToon only when this same frontend reports `IsSupportedMaterial`. So the failed identity conjunction produces both `SemanticsUnknown` and `ShaderFamilyAttestation.None`.

### Evidence extraction

For the supplied `Material`, `GatherSourceEvidence` reads these concrete inputs:

1. `material.shader.name`.
2. The shader asset GUID from `AssetDatabase.TryGetGUIDAndLocalFileIdentifier`.
3. `_lilToonVersion` existence and its exact float value from the material.
4. The Unity asset path from `AssetDatabase.GetAssetPath(shader)`.
5. Package identity and version from `PackageInfo.FindForAssetPath(shaderAssetPath)`, when a package owns the asset.
6. The project root, derived from the parent of `Application.dataPath`.
7. The shader file at the absolute form of the Unity asset path.
8. Every non-`.meta` file recursively under the sibling `Includes/` directory.
9. The pass shader resolved by exact name `Hidden/ltspass_opaque`.
10. The pass shader's GUID, asset path, and source file.
11. All valueless `LIL_FEATURE_*` defines scanned from the resolved pass.
12. The unique integer value of `#define LIL_RENDER` scanned from the resolved pass.

`GatherSourceEvidence` reads no project-local lilToon setting file directly. Its effect enters only through generated shader text. Unity import state supplies the resolved `Shader` objects, asset paths, GUIDs, and package ownership. AMUSE does not hash Unity's compiled shader binary.

### Hashing and canonicalization

Every text hash:

- removes one optional leading UTF-8 BOM.
- normalizes CRLF and lone CR to LF.
- hashes UTF-8 bytes with SHA-256.
- renders lowercase hexadecimal.

The include-tree hash covers every non-`.meta` file as normalized text. It builds ordinally sorted `relative/path:file-hash` rows, joins them with LF, and hashes that listing. An added, removed, renamed, unreadable, or modified member changes or removes the evidence.

AMUSE canonicalizes the generated material shader and generated pass shader before hashing:

- **R1:** after each exact `HLSLINCLUDE`, drop the maximal contiguous run of valueless `LIL_FEATURE_*`, `LIL_OPTIMIZE_*`, `LIL_INPUT_OPTIMIZED`, and `skip_variants` lines.
- **R2:** drop exactly `#pragma skip_variants SHADOW_VERY_HIGH`, only when it immediately follows `#define LIL_PASS_FORWARD`.
- **R3:** normalize a whole-line include path only when its exact resolved full path is a member of the already-attested include tree. An unresolved, ambiguous, redirected, or differently cased path stays hashed.

Everything else stays byte-significant after newline normalization.

### Verification order and first-failure behavior

`TryVerifyLilToonIdentity` checks this conjunction in order and returns on the first failure:

1. shader name equals `lilToon`.
2. shader GUID equals `df12117ecd77c31469c224178886498e`.
3. `_lilToonVersion` exists, is finite, and equals `45f` exactly.
4. if package evidence exists, package name equals `jp.lilxyzw.liltoon` and version equals `2.3.4`.
5. pass GUID equals `61b4f98a5d78b4a4a9d89180fac793fc`.
6. include-tree digest matches.
7. material-shader canonical digest matches.
8. pass-shader canonical digest matches.
9. the pass contains exactly one readable `LIL_RENDER`, equal to `0`.

The compiled-feature set is not an identity conjunct. It carries forward into semantic interpretation, so Normal and Emission can refuse output-locally when the material stripped required features.

## Expected evidence versus the Census Lab

| Evidence | Expected | Census Lab | Result |
| --- | --- | --- | --- |
| Shader name | `lilToon` | `lilToon` | match |
| Shader path | package-owned generated base shader | `Packages/jp.lilxyzw.liltoon/Shader/lts.shader` | match |
| Shader GUID | `df12117ecd77c31469c224178886498e` | same | match |
| `_lilToonVersion` | exact `45f` | `45f` | match |
| Package | `jp.lilxyzw.liltoon` | same | match |
| Package version | `2.3.4` | `2.3.4` | match |
| Pass name | `Hidden/ltspass_opaque` | same | match |
| Pass GUID | `61b4f98a5d78b4a4a9d89180fac793fc` | same | match |
| Include-tree digest | `6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46` | same | match |
| Base canonical digest | `5206bec25e82db5f8009b27fcc5ba94d7c41113031d4b6b0a2c25ca324a9c704` | `1a2ffc7fa6b3d54d5765de3c98ab1ff2e8ce7da4fd773e507c8c32568c369f56` | **first failure** |
| Pass canonical digest | `6b6c30c1cbe546fe753bcdc77f547441e3f9114ee80e9591bde2b8e6e7e5eb14` | `c666f898543a5fe8ec39ac7374aa53b043d3c0e468a0943826bc022acfcbe5c2` | mismatch, not reached |
| Unique `LIL_RENDER` | `0` | `0` | match, not reached |

The Lab pass exposes 102 compiled `LIL_FEATURE_*` symbols. Their presence matches its current broad project setting. They do not cause the identity failure, because R1 removes the setting-controlled block from the canonical digest.

## Exact difference and cause

The Lab base shader has 721 normalized/canonical lines. Its only pin-relevant difference is the generator-added tag token:

```text
"LTCGI"="ALWAYS"
```

Remove that exact token from the already-canonical in-memory text, and it produces the existing base pin `5206bec2...`.

The Lab pass has 1074 normalized raw lines, and 969 lines under AMUSE's current split-based count after canonicalization. The canonical text contains two exact lines:

```text
#define LIL_FEATURE_LTCGI
```

They sit immediately before `#define LIL_PASS_FORWARD` in the forward and outline-forward programs. Remove exactly those two full lines from the already-canonical in-memory text, and it produces the existing pass pin `6b6c30c1...`.

The upstream 2.3.4 generator accounts for all three differences:

- `GetSubShaderTags` appends the tag under `#if LILTOON_LTCGI`.
- the BRP `GetMultiCompileForward` expansion emits the define under the same compile symbol.
- lilToon's startup state records whether LTCGI is present, and regenerates shaders when that state changes.

The Lab's mutable `Editor/CurrentRP.txt` records `BRP`, `Metal`, and `LTCGI`. The official release archive records `BRP`, `Direct3D11`, and no LTCGI marker. That file holds generated environment state, not modified product logic.

## Authenticity comparison

The investigation downloaded the official public release archives into a temporary directory, and compared them read-only against the installed packages.

### lilToon 2.3.4

Official archive SHA-256:

`34d172761c51aa9469a904704109086aafa6125a4fa0e058766e2ddc73d3b303`

The Lab's `package.json`, `BaseShaderResources/`, `CustomShaderResources/`, and `Shader/Includes/` are byte-identical to the official archive. `Editor/` is identical except for the expected mutable `CurrentRP.txt`. The official generator and the current project environment fully explain the generated shader differences.

Conclusion: the Lab's lilToon installation is not a modified or repackaged impostor.

### LTCGI 1.7.3

Official tag archive SHA-256:

`3a3346356155abf79b6d681c52e95a9f8ba998fd36095b78c88c860e6460ffa7`

The Lab's `package.json`, `Editor/`, and complete `Shaders/` directory are byte-identical to the official `v1.7.3` tag. The remaining differences in the complete package comparison are only omitted demo/propaganda artifacts and one package-management metadata file. None sits in the active shader closure.

Conclusion: the active LTCGI source is also genuine in this Lab. AMUSE cannot rely on that conclusion at runtime until it measures equivalent source evidence itself.

## Legitimate variation characterization

| Configuration | Evidence | Attestation consequence |
| --- | --- | --- |
| Fresh/default lilToon 2.3.4 without LTCGI | Task 0 scratch measurement | existing three pins match |
| Legitimately stripped lilToon settings without LTCGI | Task 0 disabled Normal, Emission, and shadow reception | raw pass changes. All three canonical pins stay identical |
| Current Lab lilToon settings | broad feature set, plus VRCLightVolumes optimization | setting-controlled differences are removed by R1. Include tree still matches |
| Current Lab with official LTCGI 1.7.3 | official generator adds one tag and two active defines | base and pass canonical digests change. Existing profile refuses |

The existing evidence therefore disproves outcome C in its broad form: generated shader output is not fundamentally the wrong target. R1/R2 successfully collapse ordinary legitimate lilToon setting variation. The missed dimension is an optional installed dependency that changes both generated source and active transitive source.

The common differently named lilToon variants sit outside the current supported semantic target. They are not alternate spellings of it. Direct Lab probes measured their first failure:

| Shader | First failure |
| --- | --- |
| `Hidden/lilToonOutline` | `UnsupportedShader` on shader name |
| `Hidden/lilToonCutout` | `UnsupportedShader` on shader name |
| `Hidden/lilToonTransparent` | `UnsupportedShader` on shader name |
| `_lil/lilToonMulti` | `UnsupportedShader` on shader name |
| `Hidden/lilToonLite` | `UnsupportedShader` on shader name |

That behavior is correct for the current contract. Those variants use different assets, passes, render modes, or equations, and need separate semantic research. This investigation does not broaden support to them.

## Why LTCGI cannot simply be canonicalized away

The generated tag is integration metadata that the LTCGI controller uses to select renderers. The generated define is stronger: it activates code in lilToon's pinned `lil_common_functions_thirdparty.hlsl`, including external files from `Packages/at.pimaker.ltcgi/Shaders/`.

In genuine LTCGI 1.7.3, that code:

- makes sure a normal is available for lighting.
- computes an LTCGI contribution.
- adds diffuse/specular contribution to `lilLightData.lightColor`.
- does not alter AMUSE's pre-lighting BaseColor term, opaque Alpha constant, material Emission term, or material normal-map role.

That semantic separation explains why supporting the configuration is reasonable. It does not make external source safe to ignore. HLSL includes share one preprocessor namespace. A modified external include could define or redefine tokens consumed later, even when the official implementation only changes lighting. If AMUSE dropped the two activation defines while hashing neither the external files nor their identity, it would accept modified relevant shader logic.

So:

- shader name alone is not enough.
- package presence/version alone is not enough.
- the three LTCGI-generated lines are not a wildcard-safe canonicalization region.
- the external source closure must take part in any accepted compound identity.

## Architectural options

### 1. Keep current behavior and define LTCGI as unsupported

This is safe and needs no code. It defines supported lilToon as the exact standalone base-opaque 2.3.4 profile already measured. The genuine Lab stays outside that boundary, because it activates an external shader dependency.

Trade-off: every lilToon material in this Lab stays unattested, so a census still measures zero lilToon coverage. This is acceptable as a temporary fail-closed result, but it leaves a known legitimate configuration unsupported.

### 2. Add an explicit compound LTCGI profile — rejected in this shape by the provenance audit

Keep the existing standalone tuple unchanged. Add a second, atomic profile whose evidence is:

- the same shader name, shader GUID, material format stamp, lilToon package identity/version, pass GUID, include-tree digest, and `LIL_RENDER=0` checks.
- the exact observed LTCGI-generated base digest `1a2ffc7f...`.
- the exact observed LTCGI-generated pass digest `c666f898...`.
- package `at.pimaker.ltcgi` at exact version `1.7.3`.
- the complete six-file active transitive LTCGI include closure measured by the fresh scratch project, with normalized source digest `0c986d95a2100136615a183699cb5543101a8b13b5fa2260032ad612cf9279a0`.

The six relative paths are:

- `LTCGI.cginc`
- `LTCGI_config.cginc`
- `LTCGI_functions.cginc`
- `LTCGI_shadowmap.cginc`
- `LTCGI_structs.cginc`
- `LTCGI_uniform.cginc`

The earlier candidate set also contained `LTCGI_AudioLinkNoOp.cginc`. The fresh transitive preprocessing walk excludes it: its only include sits nested under `#ifdef LTCGI_AUDIOLINK`, and the official 1.7.3 configuration leaves that symbol undefined in both measured states. The similarly named `LIL_FEATURE_AUDIOLINK`, `LIL_FEATURE_AUDIOLINK_VERTEX`, and `LIL_FEATURE_AUDIOLINK_LOCAL` symbols are present but do not satisfy that guard. Every active include reached from the lilToon LTCGI entry resolves inside the six-file closure. The external closure reaches no Unity or platform header.

The investigation derived this set recursively from the actual lilToon third-party entry with active preprocessor symbols. It did not copy the set from the Census Lab file list. It is recorded evidence, not yet an accepted production pin.

Use the same normalized per-file hashing and ordinal `path:hash` listing rule as the lilToon tree. Resolve every expected path from the LTCGI package asset root. A missing, added-to-the-fixed-closure, unreadable, redirected, or modified required source file causes a refusal. A source file that adds another include necessarily changes its own hash, and refuses before AMUSE can trust that new dependency.

Treat the two generated digest pairs as atomic profiles. Do not independently allow either base digest with either pass digest. A mixed standalone/LTCGI tuple refuses.

This set was smaller than hashing the whole LTCGI shader tree, but it does not preserve every active-source difference as identity evidence. The final provenance audit found an external include that can run before the six-file closure while R1 removes its activation define. So the proposed tuple is not sufficient, and must not go into production.

### 3. Canonicalize the LTCGI tag and defines

Rejected. It produces the existing pins in this Lab, but it makes active external source invisible to attestation. Package version does not cure that: an embedded or locally modified package can still report `1.7.3`.

### 4. Attest templates and generator inputs, then reproduce generated output

This option is architecturally coherent but disproportionate here. It would need AMUSE to attest the generator, templates, project settings, render pipeline, compile symbols, external-package state, and the deterministic generation algorithm, then prove the live generated assets equal the expected output. The existing canonical-remainder design already handles ordinary settings with much less trusted machinery.

## Evaluation against the requested outcomes

| Candidate outcome | Finding |
| --- | --- |
| A. Correct target, wrong pins | Disproved. Existing pins still exactly describe fresh/default and stripped standalone 2.3.4. |
| B. Correct target, multiple legitimate deterministic variants | Supported, if AMUSE treats LTCGI as a compound source identity rather than two extra hashes alone. |
| C. Generated output fundamentally wrong target | Disproved by the default/stripped cross-check. Ordinary settings already canonicalize correctly. |
| D. Lab modified or outside intended contract | Lab source is genuine. It sits outside the *current* standalone contract only because LTCGI was never part of that contract. |
| E. Another cause | Confirmed: an official optional dependency integration changes generated source and activates an unattested external include closure. |

## Proposed meaning of "supported lilToon"

For this milestone, support stays deliberately narrow:

> A material is supported only when it uses the canonical base opaque `lilToon` asset from `jp.lilxyzw.liltoon` 2.3.4, carries shader-format stamp 45, resolves the canonical opaque pass with `LIL_RENDER=0`, and matches one complete attested source profile.

The currently implemented profile is:

1. **Standalone:** the current generated base/pass pins and complete lilToon include-tree pin.

The final provenance audit blocks the formerly proposed LTCGI profile. It is not part of the supported definition.

Characterized ordinary lilToon `ShaderSetting` variation is semantically safe to canonicalize in R1/R2 only when the raw removed tokens are proven to belong to the closed official generator domain, and satisfy the profile's exact dependency-activation record. The current verifier does not yet enforce that raw-provenance condition. The generated assets stay part of trust identity. They are not unverified derived output. Optional dependencies that activate more source form a new compound profile. They do not inherit trust from package installation.

Refuse:

- any lilToon version other than 2.3.4.
- any shader-format stamp other than exact 45.
- any shader name/GUID or pass GUID outside the supported base opaque target.
- any non-BRP output whose canonical digest differs.
- any hand edit or custom-shader insertion outside existing proven canonical regions.
- any modified, missing, redirected, or wrong-version required source.
- any unrecognized optional dependency integration or new generated digest tuple.
- every differently named outline, cutout, transparent, lite, multi, tessellation, fur, gem, refraction, or fake-shadow variant.

This is a semantic contract, not an unexplained allow-list of hashes. The hashes give exact evidence for named upstream artifacts and explicit legitimate generation dimensions.

## Fresh scratch-project cross-check

The approved follow-up used a newly created temporary project, not the public project or Census Lab. `ProjectVersion.txt` and the running process both reported Unity `2022.3.22f1`. The capture recorded its temporary `Application.dataPath` directly. The project embedded the official archives whose SHA-256 values are recorded above, in *Authenticity comparison*: lilToon 2.3.4 and LTCGI 1.7.3.

The investigation drove both states through lilToon's real `ApplyShaderSetting`. The stripped state disabled first- and second-normal features, the first bump-map source, first- and second-emission features, the first emission-map source, shadow reception, outline shadow reception, and backlight. This is a legitimate settings-only change, and it makes both the setting region and the shadow-variant slot vary.

| Evidence | Default | Stripped | Result |
| --- | --- | --- | --- |
| Raw base digest | `1a2ffc7fa6b3d54d5765de3c98ab1ff2e8ce7da4fd773e507c8c32568c369f56` | same | equal |
| Raw pass digest | `a0a70214305c7209a26e1a10641c66993a88802bf99403b8b4c22594ee060ea7` | `0cc98579707885cd592284343149fe64f9fd240686241efae8ed28d5f9bc3b9c` | **different, and non-vacuous** |
| Canonical base digest | `1a2ffc7fa6b3d54d5765de3c98ab1ff2e8ce7da4fd773e507c8c32568c369f56` | same | proposed digest confirmed |
| Canonical pass digest | `c666f898543a5fe8ec39ac7374aa53b043d3c0e468a0943826bc022acfcbe5c2` | same | proposed digest confirmed |
| lilToon include tree | 37 files, `6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46` | same package tree | existing pin confirmed |
| `"LTCGI"="ALWAYS"` | exactly 1 | exactly 1 | confirmed |
| `LIL_FEATURE_LTCGI` | HLSL programs 1 and 2, source lines 792 and 841 | HLSL programs 1 and 2, source lines 780 and 830 | exactly 2 in each. Each immediately precedes `LIL_PASS_FORWARD` |

The line numbers move because the setting-controlled run is shorter in the stripped source. Their structural positions do not move: they stay the base-forward and outline-forward program expansions, immediately before `#define LIL_PASS_FORWARD`.

### Active external dependency closure

The walk began at the active `LIL_FEATURE_LTCGI` block in attested `lil_common_functions_thirdparty.hlsl`. That block includes `LTCGI_structs.cginc`, defines the LTCGI v2 callback symbols, and includes `LTCGI.cginc`. Recursive preprocessing then reached the six files listed in option 2.

The normalized rows are:

```text
LTCGI.cginc:4cbee8f54c31c1b0c3a7ad1ef8eb55f83eea8e3574892095cb7720c0b30dae2e
LTCGI_config.cginc:35d714af6eb486efad397b9e3e563f4da774703225228c8feb4f694454f3b4cd
LTCGI_functions.cginc:f94e0cb1dae947f9272f5b5ec30ac3dbd177d33120d1d4bc27472577ffd7be58
LTCGI_shadowmap.cginc:40cee2807ddcec818c12d98e556b40f9a328e5fcca59756e84724b9ef542c8bb
LTCGI_structs.cginc:4e5f834ab4feb4048a25aef508d028a219b7accdb3d7d2f7904d58b8fc955b6e
LTCGI_uniform.cginc:7ee902d686aa6cb7cb49e75d367ff7ad389e82898fb2a0d70db88f188a8d4216
```

Their ordinal `path:hash` listing digest is `0c986d95a2100136615a183699cb5543101a8b13b5fa2260032ad612cf9279a0`. Default and stripped generation use the same closure. Every active include resolves inside it. These six files reach zero active Unity/platform-header edges, so the investigation did not need to infer any external header identity.

The only other literal include in those sources is the inactive `LTCGI_AudioLinkNoOp.cginc` edge. The fresh two-state cross-check correctly found it inactive under those two states. The later provenance audit disproved the assumption that activating it must change hashed evidence: an earlier unattested AudioLink include can define `LTCGI_AUDIOLINK` while all six LTCGI files stay unchanged.

## Final preprocessor provenance audit

### Result: category D exists

The required property does not hold. The relevant include order in the official lilToon 2.3.4 source is:

1. the generated pass defines `LIL_FEATURE_LTCGI`.
2. `lil_common.hlsl` includes `lil_common_input.hlsl`.
3. when `LIL_FEATURE_AUDIOLINK_PACKAGE` is present, `lil_common_input.hlsl` includes `Packages/com.llealloo.audiolink/Runtime/Shaders/AudioLink.cginc`.
4. only afterward does `lil_common.hlsl` include `lil_common_functions.hlsl`, which includes `lil_common_functions_thirdparty.hlsl` and enters LTCGI.

Official lilToon creates the package-controlled symbol in two steps. `lilToon.Editor.asmdef` maps `com.llealloo.audiolink` version expression `2` to the C# compile symbol `LILTOON_AUDIOLINK`. `BuildShaderSettingStringMulti` then emits `#define LIL_FEATURE_AUDIOLINK_PACKAGE`. The emitted line is a valueless `LIL_FEATURE_*` setting line immediately after `HLSLINCLUDE`, so AMUSE R1 removes it. `CompiledFeatures` collects it, but exact equality of that collection is not an identity conjunct, and it does not cause refusal.

The following table covers the conditional and macro inputs that can change the LTCGI include set, select materially different LTCGI executable code, or change a shader semantic relevant to the four AMUSE outputs. A symbol with more than one letter has both an expected official provenance and an alternate provenance channel.

| Symbol or symbol group | Class | Exact provenance and effect |
| --- | --- | --- |
| `LIL_FEATURE_LTCGI` | B / D | Officially emitted twice by attested lilToon generated pass evidence, and required at the two forward-program positions. The earlier external AudioLink include can nevertheless `#undef` it before the attested third-party block tests it. |
| `LIL_FUNCTIONS_INCLUDED`, `LIL_FUNCTIONS_THIRDPARTY_INCLUDED` | B / D | Guards defined by hashed lilToon include sources. The external include runs before either guarded file, and can predefine a guard, removing the downstream LTCGI entry, or replace what that entry expects. |
| `Sample`, `LTCGI_V2_CUSTOM_INPUT`, `LTCGI_V2_DIFFUSE_CALLBACK`, `LTCGI_V2_SPECULAR_CALLBACK` | B | Defined immediately around the LTCGI entry by hashed `lil_common_functions_thirdparty.hlsl`. They select the v2 callback path and lilToon's contribution accumulation. A prior external include can attempt definitions, but the attested source unconditionally redefines these before LTCGI consumes them. |
| `LIL_FEATURE_AUDIOLINK_PACKAGE` | D | Emitted by the official lilToon generator only because the external AudioLink package satisfies its asmdef version define. R1 removes the line from the canonical digest, and the accepted identity does not require an exact compiled-feature set. It adds the unattested include before LTCGI. |
| `LIL_FEATURE_AUDIOLINK`, `LIL_LWTEX` | B | Generated/attested lilToon feature inputs to lilToon's own AudioLink helper. They do not define `LTCGI_AUDIOLINK`. AMUSE already handles their material-facing writers conservatively. They do not close the package-include channel above. |
| `LTCGI_INCLUDED`, `LTCGI_CONFIG_INCLUDED`, `LTCGI_STRUCTS_INCLUDED`, `LTCGI_UNIFORM_INCLUDED`, `LTCGI_FUNCTIONS_INCLUDED`, `LTCGI_SHADOWMAP_INCLUDED` | A / D | Include guards defined by the six hashed LTCGI files. Because the AudioLink source runs first, it can predefine any guard and suppress that file while supplying alternate declarations or code. |
| `LTCGI_SPECULAR_OFF`, `LTCGI_DIFFUSE_OFF`, `LTCGI_TOGGLEABLE_SPEC_DIFF_OFF`, `LTCGI_ALWAYS_LTC_DIFFUSE`, `LTCGI_BLENDED_DIFFUSE_SAMPLING`, `LTCGI_DISABLE_LUT2`, `LTCGI_STATIC_TEXTURES`, `LTCGI_CYLINDER`, `LTCGI_AVATAR_MODE`, `LTCGI_FAST_SAMPLING` | A / D | Their intended enabled/disabled state is encoded in hashed `LTCGI_config.cginc`. Avatar mode also derives `LTCGI_ALWAYS_LTC_DIFFUSE` inside hashed `LTCGI.cginc`. LTCGI tests them by presence, and does not overwrite them with a fixed absent state. So the earlier external include can define them and select different code with unchanged LTCGI files. |
| `LTCGI_BICUBIC_LIGHTMAP`, `LTCGI_DISTANCE_FADE_APPROX`, `LTCGI_STATIC_UNIFORMS`, `MAX_SOURCES`, and the numeric cutoff, LUT, blur, and distance macros | A | Unconditionally defined to the measured values by hashed `LTCGI_config.cginc` before use. Their official provenance is closed by the six-file digest. A conflicting prior definition gets overwritten by the attested definition, or rejected by a compiler as a redefinition. |
| `LTCGI_API_V2` | A + B | Derived in hashed `LTCGI.cginc` from the three unconditionally supplied lilToon v2 callback macros in class B. |
| `LTCGI_AUDIOLINK` | A / D | The official LTCGI controller can uncomment its definition in hashed `LTCGI_config.cginc`, which would change the closure digest and cause a refusal. Independently, the earlier unattested `AudioLink.cginc` can define it while `LTCGI_config.cginc` stays byte-identical. That activates AudioLink-dependent struct fields and executable color multiplication in LTCGI. |
| `AUDIOLINK_WIDTH`, `AUDIOLINK_CGINC_INCLUDED` | A / D | When `LTCGI_AUDIOLINK` is set, hashed config tests these to choose the no-op include. Config defines the included guard after taking that edge. A real or modified external AudioLink file normally supplies its own AudioLink guards/dimensions first, and the proposed profile attests neither value. |
| `LTCGI_SAMPLER`, `LTCGI_SAMPLER_RAW` | A / D | Hashed `LTCGI_uniform.cginc` supplies the default pair only under `#ifndef LTCGI_SAMPLER`. An earlier external definition can select a different sampler pair and materially change LTCGI texture sampling. |
| `LTCGI_VISUALIZE_SAMPLE_UV` | D | No definition exists in the attested lilToon source or the six LTCGI files. Defining it before the entry replaces normal sampled color with diagnostic UV color in executable LTCGI code. |
| `SHADER_TARGET_SURFACE_ANALYSIS`, `SHADER_TARGET_SURFACE_ANALYSIS_MOJOSHADER` | C | Compiler-mode inputs, absent from package source and supplied only for Unity/platform surface-analysis compilation. LTCGI deliberately uses them to replace unavailable sampling and declarations. They are not project package inputs or normal runtime variants. |
| `UNITY_DECLARE_TEX2DARRAY_NOSAMPLER`, `UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD`, `UNITY_PI`, `UNITY_HALF_PI`, `UNITY_TWO_PI` | C | Unity platform-header macros/constants already in lilToon's Unity compilation environment. LTCGI consumes them, but does not source them from another project package. |
| `UNITY_NO_DXT5nm`, `UNITY_ASTC_NORMALMAP_ENCODING` | C | Unity/platform normal-texture encoding inputs used by hashed lilToon normal decoding, not by the LTCGI contribution itself. They are the deliberately understood platform dimension relevant to AMUSE's Normal claim. |

The D classification is not merely hypothetical. A scratch-only witness added a package-path `AudioLink.cginc` containing `#define LTCGI_AUDIOLINK`, without modifying lilToon or any of the six LTCGI files. A baseline preprocessing dependency walk reached the six-file closure. The AudioLink state also reached the external AudioLink file and `LTCGI_AudioLinkNoOp.cginc`, and the final macro table contained `LTCGI_AUDIOLINK`.

Separately, injecting exactly `#define LIL_FEATURE_AUDIOLINK_PACKAGE` into the default generated pass's R1 setting region kept the canonical pass digest at `c666f898543a5fe8ec39ac7374aa53b043d3c0e468a0943826bc022acfcbe5c2`. The base source and digest stayed untouched, the lilToon include tree stayed untouched, and the fixed six LTCGI files kept closure digest `0c986d95a2100136615a183699cb5543101a8b13b5fa2260032ad612cf9279a0`.

So the accepted compound evidence can stay identical while a materially different relevant LTCGI path compiles. At that review gate the audit stopped at the category-D finding, without choosing a replacement. The continuation below performs that requested architectural comparison, and still makes no production correction.

### Cross-check outcome and remaining review gate

Every requested generated-source assertion passed. The dependency assertion exposed the six-versus-seven-file correction above, so the cross-check stops here, as required. The investigation widened no matching rule, accepted no additional generated hash, and added no production profile.

The final provenance audit answered that review gate negatively. The six-file active closure is not a fail-closed boundary under the existing canonical evidence, because a canonicalized-away lilToon setting symbol can introduce source before the closure. The continuation below selects the shape of a replacement boundary, but does not approve or implement an integrated profile.

## Canonicalization dependency audit

### Audit scope and method

This continuation re-read the production R1/R2/R3 implementation and its focused tests, the official lilToon 2.3.4 setting generator and shader-container importer, both relevant editor asmdefs and their `versionDefines`, startup/package detection, the base-opaque templates, and every package-path include reachable from the supported BRP target. The search covered literal and generated include paths, compile symbols, integration callbacks, insert/replace hooks, and every use of the generated R1 categories. The result is deliberately scoped to the existing base opaque `lilToon` target. Differently named lilToon variants stay outside the semantic contract.

The relevant include order is:

1. Unity BRP headers.
2. optional external VRC Light Volumes source through `lil_pipeline_brp.hlsl`.
3. `lil_common.hlsl` and lilToon's common macro/input sources.
4. optional external AudioLink source through `lil_common_input.hlsl`.
5. lilToon's common functions and third-party entry.
6. optional external LTCGI source.

So both VRC Light Volumes and AudioLink can change macro state that later lilToon or LTCGI code consumes. Attesting only the later LTCGI closure cannot close either earlier input.

### R1: what is removed

The official generator emits 109 possible setting symbols into the R1-shaped region. Their trust role, rather than their raw count, decides the outcome. In this table A-F refer to the task's behavioral classes: attested-tree-only code, conservatively handled material semantics, external inclusion, external macro state, package contributor identity, and other trust-relevant behavior.

| Generated category | Generator provenance and behavioral role | A-F classification | Canonicalization verdict |
| --- | --- | --- | --- |
| Core `LIL_FEATURE_*` gates | lilToon `ShaderSetting` selects base-color and UV processing, alpha/coverage, emission, normal, lighting, reflection, and resource declarations. Their executable implementations sit in the fully hashed lilToon include tree. | A, and some B | Safe only under the existing output-local conservative interpretation described below. They add no outside source. |
| Third-party `LIL_FEATURE_*` activators | Compile symbols and package detection emit VRC Light Volumes and AudioLink package gates. LTCGI is officially emitted outside R1, but the wildcard accepts the same token inside R1. | C, D, E, F | **Not safe to erase.** These tokens change the executable source graph or preprocessor state outside the attested lilToon tree. |
| Bundled `LIL_FEATURE_VRCLIGHTVOLUMES_WITHOUTPACKAGE` | VRC SDK presence plus the Light Volumes setting selects lilToon's bundled `Includes/VRC Light Volumes/LightVolumes.cginc`. | A | Safe for dependency identity, because the selected file is already in the hashed lilToon tree. It affects lighting only, not the four material terms. |
| `LIL_OPTIMIZE_APPLY_SHADOW_FA` | Selects a shadow application branch in hashed lilToon forward code. | A | Safe for the four scoped outputs. It changes lighting only. |
| `LIL_OPTIMIZE_USE_FORWARDADD` and `_SHADOW` | Record settings that also make the generator remove passes or pragmas outside R1. | A, F | The erased defines are redundant identity evidence: their material executable effect stays visible in canonical source structure. |
| `LIL_OPTIMIZE_USE_VERTEXLIGHT` and `_LIGHTMAP` | Select generated skip-variant sets for lighting keywords. | A, F | Safe for the scoped material outputs under the R1 skip-variant finding below. |
| `LIL_INPUT_OPTIMIZED` | Marks optimized vertex/input layout and is consumed by hashed lilToon Light Volumes stage/data conditions. | A | Adds no outside source. For modified shaders the actual GUID-resolved optimized input include sits outside R1 and stays digest-visible. |
| R1 `#pragma skip_variants` lines | Generator expands fixed groups for shadows, lightmaps, decals, forward-add lights and shadows, probe volumes, AO, light lists, and reflections. A global de-duplication set can leave only the last unseen keyword at a later slot. | A, F | Safe for this target: they control compiler variant availability only, add no includes or macros, and the BRP pass's skipped keywords are lighting/fog/instancing/shadow concerns rather than the statically generated material feature gates. |

The core semantic gates stay safe-to-ignore only because variation is bounded twice. First, every selected code path sits inside the attested lilToon tree. Second, `LilToonMaterialSemantics` refuses or handles material writers conservatively:

- BaseColor checks the material properties that enable invisible/backface-UV, parallax/POM, lilToon AudioLink modulation, main layers, gradation, tone, UV, and masks. Stripping can remove code, while enabled writers cause a refusal where necessary.
- Opaque `LIL_RENDER=0` forces Alpha to one after the separately checked invisible/UDIM coverage controls. Cutout, transparent, dissolve, and dither semantics sit outside this target.
- Emission validates material writers and also requires the relevant compiled `LIL_FEATURE_EMISSION_1ST` and `LIL_FEATURE_EmissionMap` evidence before claiming the term.
- Normal validates material writers and also requires `LIL_FEATURE_NORMAL_1ST` and `LIL_FEATURE_BumpMap` before claiming the term.

That conservative use of selected compiled features is not an exact identity check. It cannot make package/integration activators safe to erase.

### Every external-source activation channel found

Official 2.3.4 source contains exactly three upstream-declared external package shader-source activators for the supported BRP base-opaque target. The asmdefs contain no other third-party shader-integration `versionDefines`. The complete literal package-include search found only these integrations plus render-pipeline/Unity headers.

| Channel | Activation cause | Raw generated evidence | Current treatment | Executable external source and macro effect | Can current accepted evidence stay unchanged? |
| --- | --- | --- | --- | --- | --- |
| VRC Light Volumes package | Editor asmdef maps any installed `red.sim.lightvolumes` version to `LILTOON_VRCLIGHTVOLUMES`. `LIL_OPTIMIZE_USE_VRCLIGHTVOLUMES` must also be true. | R1 `#define LIL_FEATURE_VRCLIGHTVOLUMES` | R1 erases it. Compiled-feature equality is not required. | `Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc`, included before lil common code. It can export arbitrary macros to all downstream source. | **Yes.** Synthetic R1 injection kept the compound canonical pass digest. Base, lilToon tree, and LTCGI closure evidence do not change. |
| AudioLink package | Editor asmdef maps `com.llealloo.audiolink` version expression `2` to `LILTOON_AUDIOLINK`. The generator emits the gate unconditionally when that compile symbol exists. | R1 `#define LIL_FEATURE_AUDIOLINK_PACKAGE` | R1 erases it. Compiled-feature equality is not required. | `Packages/com.llealloo.audiolink/Runtime/Shaders/AudioLink.cginc`, included before lil functions/LTCGI. It can define `LTCGI_AUDIOLINK`, include guards, feature controls, or sampler macros. | **Yes.** The recorded witness kept all proposed compound evidence while activating different LTCGI code and an additional include. |
| LTCGI package | Editor asmdef maps `at.pimaker.ltcgi` version expression `1.4` to `LILTOON_LTCGI`. | Official BRP output has one base tag and two `LIL_FEATURE_LTCGI` lines immediately before the two forward `LIL_PASS_FORWARD` defines, outside R1. | Official activation changes both canonical digests and is visible. R1's wildcard nevertheless erases the same token if it appears in the setting region. | Two package-path entries reach the characterized LTCGI closure. LTCGI consumes and exports macros in the shared preprocessor namespace. | **Yes, if hidden in R1.** Removing the two official forward lines from scratch source reproduced standalone digest `6b6c30c1...`. Injecting one R1 `LIL_FEATURE_LTCGI` kept that digest while activating LTCGI globally. |

The VRC-SDK fallback `LIL_FEATURE_VRCLIGHTVOLUMES_WITHOUTPACKAGE` selects a bundled lilToon include and is not a third-party source channel. The render-pipeline files include Unity BRP headers unconditionally and contain URP/LWRP/HDRP package paths, but pipeline selection changes generated content outside R1, so it does not collapse to the accepted BRP digests. `PROBE_VOLUMES_L1/L2` selects a Unity core-package header, and is a deliberately identified Unity/platform input.

The optimized-custom-input path is also not hidden: when shader optimization and modified shaders are active, the importer inserts the GUID-resolved include text outside R1. Custom container data can insert or replace source, but the official base/pass data supply no external insertion, and any such structural output stays canonical-digest visible. The generator's reflection enumerates overloads on the attested `lilToonSetting` type. It is not an arbitrary assembly callback. The investigation found no generic external shader-code hook.

### R2 trust analysis

R2 removes one exact `#pragma skip_variants SHADOW_VERY_HIGH`, only when it immediately follows exact `#define LIL_PASS_FORWARD`. Official generator source explains the form: the fixed shadow skip group is globally de-duplicated, so in a legitimately stripped state only the final unseen high-shadow keyword can remain at that later forward slot.

The directive removes a compiler variant. It does not alter executable source within any retained variant, add an include, define a macro, or select a material feature equation. Its effect is limited to availability of the highest shadow variant, and so to lighting behavior outside BaseColor, Alpha, Emission, and Normal. A different keyword, multiple keywords, or a different location stays hashed. The investigation found no R2 trust-boundary collapse.

### R3 trust analysis

R3 normalizes only a whole-line live or commented include whose lexically resolved full path is an exact ordinal member of the include tree that AMUSE has already fully hashed. It tests both the shader-directory and project-root interpretations. Two different member candidates stay ambiguous and unnormalized. External, unresolved, traversal, redirected, or case-different paths stay raw evidence.

So relative and package-looking paths collapse only when they identify the same already-attested member. Package redirects or alternate external paths cannot normalize into membership. A symlink or hardlink that the tree actually enumerates as a member contributes the bytes read through that member to the tree digest, so retargeting or changing its content changes evidence. A path outside the tree stays unnormalized. The general requirement that evidence collection and compilation observe a stable filesystem is an operational atomicity/TOCTOU assumption, not a second R3 equivalence. The investigation found no R3 trust-boundary collapse.

### Shape of the problem

The evidence supports **Outcome A**, with one important enforcement consequence: only a small explicit set of package-integration defines affects the dependency boundary, but R1's syntactic wildcard cannot distinguish that set from safe feature variation. The other generated feature symbols do not broadly alter external source or transitive external macro state, so Outcome B overstates the problem. Official source, generated structure, and a transitive closure walk suffice to close a selected profile without reproducing the full preprocessor environment, so Outcome C is also too pessimistic.

The safe/trust distinction is therefore behavioral, not naming-based:

- **Safe canonical variation:** generator-controlled differences whose executable code stays inside the already-attested lilToon tree, whose external structural consequences stay separately canonical-digest visible, and whose possible effects on the four claimed material outputs are either characterized irrelevant or conservatively handled.
- **Trust-affecting variation:** any raw generated fact that activates outside source, selects which package contributes code, or lets outside code change macros consumed by later relevant source. It must stay independently visible even when it occurs in an otherwise canonical region.

### Candidate trust models

| Strategy | Security result | Cost and disposition |
| --- | --- | --- |
| 1. Keep integrations unsupported | Fail-closed now. Existing standalone measurements and pins stay unchanged. | Lowest cost and the required interim state, but no integrated coverage. |
| 2. Preserve selected dependency activators and attest each active closure | Sufficient for the exact 2.3.4 channels, if the raw activation record enforces exact counts/locations and explicit absence as well as presence. | Smallest concrete future implementation. It needs fixed characterization for the selected integration tuple, not a registry. |
| 3. Separate semantic canonical evidence from dependency identity | Best statement of the trust model. R1/R2/R3 stay the semantic layer. A raw activation projection plus closure/provenance evidence is the dependency layer. | **Recommended architecture.** For this milestone, its implementation should be strategy 2's fixed checks, not a generic abstraction. |
| 4. Hash whole integration shader packages | Proves a broader source boundary, but does not prove whether that source was activated or whether an earlier package changed its macro state. | May simplify closure enumeration at substantial false-negative cost, but is insufficient without the activation layer and upstream provenance. |
| 5. Reconstruct the generator or full preprocessor environment | Could prove more state, but would greatly enlarge trusted machinery and still need a defined Unity/platform boundary. | Disproportionate for three explicit channels. Reject unless future source invalidates the fixed characterization. |

The recommended direction is therefore a two-layer atomic profile:

1. keep the present name/GUID/version, lilToon include-tree, canonical generated digests, render-mode, and output-local semantic evidence.
2. before canonicalization, extract the exact integration activation record for `LIL_FEATURE_LTCGI`, `LIL_FEATURE_AUDIOLINK_PACKAGE`, and `LIL_FEATURE_VRCLIGHTVOLUMES`, including required structural positions/counts and forbidden occurrences.
3. attest the complete active transitive source closure of every enabled integration, including all upstream external source that can affect its preprocessor state.
4. treat deliberately identified Unity/platform headers and compiler inputs as an explicit host boundary, rather than silently as package evidence.
5. accept only a complete atomic tuple. Never mix a semantic digest pair, activation record, or external closure from different profiles.

For standalone, all three third-party activators must be absent everywhere in raw generated evidence. For a future LTCGI-only profile, the generated tag and exactly two official forward-position LTCGI defines must be present, while the AudioLink-package and external-Light-Volumes activators must be absent. If a target intentionally includes either earlier package, AMUSE must characterize and attest its own full active closure and macro provenance first.

This direction does not need a change to the canonical digest pins. It does reveal that the current standalone verifier's wildcard acceptance predicate is not by itself closed: a hidden R1 `LIL_FEATURE_LTCGI`, AudioLink-package, or external-Light-Volumes define can keep a standalone canonical digest. Production design must add the independent negative activation condition before it can claim the stronger property. This document makes no production correction.

### Intended security property

AMUSE's lilToon source attestation should guarantee:

> Two shader states accepted as the same lilToon semantic profile have the same trust-relevant generated integration activation record, the same attested executable external source closure, and the same relevant upstream package-controlled preprocessor inputs. Any difference AMUSE canonicalizes away stays confined to attested lilToon source, and is explicitly characterized either as irrelevant to BaseColor, Alpha, Emission, and Normal, or as conservatively reflected in AMUSE's output-local evidence and refusal behavior. Unity/platform compiler inputs are an explicit supported-host boundary, not an implicit package identity claim.

The authentic standalone default and stripped states measured in this investigation satisfy the source-side intent: they have no external integration activation and differ only in characterized generator settings. The existing standalone pins stay valid and must not change. The production verifier does not yet enforce the required global absence of hidden third-party activators, so its acceptance predicate needs the orthogonal activation check before the formal property is fully satisfied.

A future integrated profile satisfies the property only if it has an exact raw activation record, exact canonical semantic evidence, all active contributed external closures, and provenance for every earlier external macro input capable of changing relevant downstream code. Package name/version is metadata, not sufficient source identity.

### Remaining unknowns for integrated-profile design

The architecture is closed enough to reject generic preprocessor reconstruction and select a minimal evidence shape, but no integrated profile is ready for implementation. Before an integrated-profile implementation plan can be designed, the chosen first target must supply:

- an explicit decision between LTCGI-only with AudioLink and external VRC Light Volumes forbidden, or a larger intentional compound configuration.
- fresh official-package generation across the relevant package-presence combinations, confirming exact activator counts and locations rather than only synthetic generator-equivalent injection.
- for every allowed external integration, its exact package version, complete active transitive shader closure, literal and conditional include edges, macros it consumes, guards/features/samplers it can export, and deliberately identified Unity/platform edges.
- negative witnesses proving that an absent, extra, duplicated, relocated, or conflicting activator refuses, even when R1/R2/R3 canonical output stays unchanged.
- confirmation that no selected integration can change the four claimed outputs through an earlier unattested include or macro channel.
- an atomic evidence-collection design that avoids filesystem change between hashing and shader compilation, or an explicit operational assumption and refusal behavior for that race.

The VRC Light Volumes asmdef accepts any installed package version, so AMUSE cannot characterize its source boundary from the lilToon declaration alone. AudioLink needs equivalent closure characterization if it is allowed. Otherwise exact absence must be part of the profile. The previously measured six-file LTCGI closure is reusable only for a state whose upstream AudioLink and external-Light-Volumes channels are proven absent.

This gate creates no integrated-profile implementation plan. Review should first confirm the security property, the two-layer fixed-profile direction, and which exact integration tuple, if any, deserves the next characterization milestone.

## Ecosystem and mechanism boundary audit

### Precise upstream boundary

The defensible finding is:

> Official lilToon 2.3.4 exposes exactly three upstream-declared external package shader-source activation channels for the supported BRP base-opaque generation path: external VRC Light Volumes, AudioLink package source, and LTCGI.

This statement is exhaustive for that upstream path, not for every third-party modification that can call itself a lilToon extension. Four mutually checking inventories in the official 2.3.4 source support it:

- `lilToon.Editor.asmdef` has shader-generating external-package `versionDefines` only for `red.sim.lightvolumes`, `com.llealloo.audiolink`, and `at.pimaker.ltcgi`. Its VRC SDK defines select SDK/bundled behavior, not another external shader include. `lilToon.Editor.External.asmdef` adds only VRC SDK defines.
- `BuildShaderSettingStringMulti` emits the external Light Volumes and AudioLink package gates. The BRP multi-compile expansion and subshader-tag generator emit LTCGI evidence.
- the complete literal package-path include inventory under the official shader include tree contains those three third-party packages plus Unity render-pipeline/core headers. No fourth third-party package path exists.
- all `LILTOON_*` compile-symbol branches capable of changing this generated shader path reduce to those three integrations, render-pipeline/platform selection, VRC SDK bundled fallback, or whether asset modification tooling itself is enabled.

`LIL_FEATURE_VRCLIGHTVOLUMES_WITHOUTPACKAGE` is not a fourth external channel: it selects lilToon's bundled Light Volumes source, already inside the attested include tree. `LILTOON_DISABLE_ASSET_MODIFICATION` can disable the custom importer, but it adds no executable shader source, and the live generated assets still have to pass attestation.

This finding does not claim that a third party cannot patch a file, generate a derivative, or ship another custom shader. Those mechanisms are classified below, and normally refuse rather than become supported integrations.

### Official custom shader and extension mechanisms

Official 2.3.4 custom generation is asset-oriented, not a global plugin callback into the canonical shader:

- `.lilcontainer` is handled by a dedicated `ScriptedImporter`. `UnpackContainer` builds shader text, and `ShaderUtil.CreateShaderAsset` makes the imported container's own shader object. Its Unity asset identity is the container asset's identity, not the canonical `lts.shader` GUID or the canonical opaque-pass GUID.
- `.lilblock` files are plain text fragments that `UnpackContainer` expands. `CustomShaderResources` supplies the standard property/subshader fragments. A container may select those fragments or a fragment adjacent to itself.
- `lilSubShaderInsert`, `lilSubShaderInsertPost`, `InsertPass*`, `InsertUsePass*`, and `Replace` read adjacent files or data entries and splice or replace generated text. The resulting insertion/replacement is ordinary generated source, not hidden importer state.
- `lilCustomShaderDatas.lilblock` sits adjacent to the asset being unpacked, and supplies shader/editor names plus optional inserts/replacements. The official base-resource copy contains only an empty `ShaderName` and the lilToon inspector name. It declares no insert or replacement.
- `custom.hlsl` and `custom_insert.hlsl` have no special filename handling, file, or reference in the official 2.3.4 package. A custom container or third-party derivative may include files with those names like any other include, but that does not create a canonical-base hook.
- Custom inspectors and material keywords do not by themselves edit generated shader source. If an inspector triggers regeneration, changes material properties, or writes a file, classification follows the resulting source/material evidence rather than the inspector's name.

These mechanisms naturally refuse under the current identity conjunction:

1. A normal `.lilcontainer` has a different asset GUID/path. Even if it declares shader name `lilToon`, the GUID check fails. In addition, `GatherSourceEvidence` reads the container file at its asset path, not its imported `Shader Source` subasset, so its canonical digest is not the canonical generated `lts.shader` digest.
2. A custom shader that uses the canonical opaque pass through `UsePass` still has a different material shader identity. Sharing a pass does not inherit the base shader's attestation.
3. Inserts, replacements, external includes, changed pass names, and custom executable text in a copied or patched canonical asset stay byte-significant outside R1/R2/R3. R3 does not normalize external include paths into the attested tree.
4. Editing a file in `Shader/Includes` changes the complete include-tree digest. Editing `CustomShaderResources`, a base `.lilinternal`, or its adjacent data changes the next generated source. AMUSE attests the live output, not the template.

One caveat is already known: a template, data file, postprocessor, or direct writer can cause a valueless `LIL_FEATURE_*`, `LIL_OPTIMIZE_*`, `LIL_INPUT_OPTIMIZED`, or R1 skip line to appear in the erased run. That does not make the custom extension supported. The narrow generic refusal condition requires every removed R1 token to be a structurally valid member of the official generator-emittable domain, and then applies exact profile rules to the three dependency activators. An unknown R1 token refuses, even when the canonical digest would otherwise match.

R1 or R2 removes no executable statement or include directive, and R3 changes only the spelling of an include already proven to resolve to the hashed lilToon tree. So custom executable code or a custom external include, inserted before or after lilToon's normal includes, changes canonical evidence. The only invisible extension lever found is preprocessor/variant state shaped like an erased generator line. The closed provenance record suffices to refuse it, without parsing the extension.

### Mutation of the canonical generated assets

Official lilToon exposes regeneration entry points, but no arbitrary third-party callback registry in the canonical generator. `ApplyShaderSetting` enumerates upstream `.lilinternal` inputs, calls the fixed `lilShaderContainer.UnpackContainer`, writes the corresponding generated `.shader`, and separately reimports custom `.lilcontainer` folders. Reflection in `BuildShaderSettingString` enumerates methods on the attested `lilToonSetting` type. It does not discover methods from external assemblies.

The two official `AssetPostprocessor` uses are also bounded: one registers imported `.lilcontainer` shader objects, and one migrates newly imported lilToon materials. Neither gives a hook that splices external shader text into canonical `lts.shader` or `ltspass_opaque.shader`.

Another Unity editor package can still call public/internal APIs through reflection, change settings, overwrite templates, alter package files, or rewrite generated files. The mechanism alone is not trusted:

- setting-only regeneration stays inside the characterized R1/R2 domain.
- optimized custom-input generation inserts the resolved input include outside R1, and so changes canonical evidence.
- source changes outside canonical regions change the base/pass digest.
- include-tree changes change the tree digest.
- a changed or redirected include outside the tree stays raw under R3, and changes the digest.
- a hidden R1-shaped change is accepted only if Layer 2 mistakes it for official output, which the closed removed-token domain and exact integration record are designed to prevent.

Package overlays, legacy `Assets` installs, copied metas, alternate paths, and source patches therefore do not gain trust from package name/version. They must independently satisfy exact GUID and live source evidence. Deliberate SHA-256 forgery, and a file changing between the evidence read and compiler read, are cryptographic/atomicity assumptions, not extension formats AMUSE should model.

### Representative build-time transformation boundary

This part uses official third-party repositories only as mechanism evidence. It does not measure prevalence, and creates no support commitment.

| Tool/mechanism inspected | Observed transformation | Attestation consequence | A-E class |
| --- | --- | --- | --- |
| Avatar Optimizer | Its official NDMF plugin runs its main work in `Optimizing`, duplicates assets/materials, gathers shader/material information, and can optimize material properties/textures or deliberately convert a merge target to another shader. Its Shader Information API describes a shader. It is not a shader-source injection API. | A copied material that keeps the canonical shader gets re-evaluated from its live properties. A deliberate shader replacement has a different shader identity. The investigation found no canonical-source false-positive channel. | A, or semantic/material evidence |
| TexTransTool | Its official plugin modifies materials in `Transforming`, and does more work in `Optimizing` before Avatar Optimizer. `MaterialModifier` can explicitly replace `Material.shader`, render queue, or material properties on a mutable material. | A shader override is a different identity and refuses, unless independently supported. Property-only changes belong to live semantic evidence. Ordering must make sure AMUSE observes the post-transformation material. | A, or semantic/material evidence. E if ordered after AMUSE |
| Modular Avatar | Its official plugin operates in `Resolving`, `Transforming`, and cleanup in `Optimizing`. The inspected plugin definition and source search expose object/animation/material assignment behavior, not canonical lilToon source rewriting. | A material/shader swap must be part of the material-state set AMUSE analyzes. A derivative shader refuses normally. Unknown reachable states stay conservative. This is not a reason to weaken source attestation. | A, or semantic/material evidence. E if ordered after AMUSE |
| VRCFury SPS | Its official patcher reads and rewrites shader source, injects SPS code/includes, assigns a new material shader, writes a temporary `.shader`, and deliberately renames it to `Hidden/SPSPatched/<hash>`, or the locked equivalent. It can unpack lilToon containers to get source, but still emits a derivative. | The changed name, asset, GUID, and generated source naturally fail canonical lilToon attestation. AMUSE should not recognize the derivative heuristically. | A |
| VRCFury material actions or other animator-generating tools | They can set properties or swap materials through the build result/animations without changing the canonical shader package. | These are material-state and animation-reachability questions. If the complete relevant state set is not established, semantics stay Unknown. They are not new source-attestation identities. | Semantic/material evidence. E if changed after AMUSE's snapshot |

AMUSE currently has no exported NDMF plugin, pass, `BuildPhase`, or ordering constraint in either package. The semantic frontend is called directly by Editor host analysis, tests, and the census collector. So there is no implemented relative stage from which to claim that AMUSE runs before or after these tools.

That absence is now an explicit production-design prerequisite:

> The future AMUSE pass must take its source and material-state evidence snapshot after every allowed transformation capable of changing those facts, and no later pass may make a semantically relevant source/material change without invalidating or repeating the analysis.

NDMF's phase names alone do not prove that property. Plugins within a phase need explicit ordering. A transformation strictly after a correctly defined AMUSE snapshot is class E only when the pipeline contract establishes that it cannot feed back into the optimized decision. Otherwise it is a lifecycle violation, and AMUSE must run later or refuse. This investigation does not select the future NDMF phase or plugin dependencies.

### Safety classification

| Class | Mechanisms in this audit | Required outcome |
| --- | --- | --- |
| A. Distinct identity | Normal `.lilcontainer`/`.lilblock` custom shaders, custom inserts/includes, VRCFury SPS derivatives, explicit TexTransTool shader overrides, ToonLit/other shader conversion, source edits outside canonical regions, and include-tree edits. | Existing name/GUID/source evidence refuses naturally. Do not add derivative recognition. |
| B. Known dependency activation | The three precisely qualified official upstream activators only. | Eligible for exact Layer-2 activation facts and explicitly attested external closures. No integration is accepted merely because its package is installed. |
| C. Hidden but generically detectable mutation | A generator, template, data file, or editor writer places a non-official or wrongly positioned token in R1, or hides one of the three activators in a canonicalized run. | Future hardening validates the complete removed-token provenance/domain and exact integration positions/counts before it accepts the canonical digest. It refuses the modified behavior rather than interpreting it. |
| D. Open-ended external mutation | Unknown generator plugins, deliberate in-process/compiler manipulation, an external source graph whose upstream macros or closure cannot be bounded, or arbitrary modifications that cannot be observed atomically. | Unsupported. Refuse semantics. Do not add a registry, adapter, or universal preprocessor model. |
| E. Outside the analyzed lifecycle | A build transformation proven to occur after AMUSE's authoritative snapshot without feeding back into its decision. | Document and enforce ordering. If non-feedback cannot be proven, re-order/re-analyze or refuse. Do not assume harmlessness. |

### Conservative ecosystem policy

Eligible for explicit characterization:

- canonical upstream lilToon at an exact supported version and target.
- an official upstream-declared package integration with an exact raw activation record and closed executable source/provenance boundary.
- exceptionally, a demonstrably widespread integration, only after separate coverage evidence shows material value and AMUSE can close the same trust boundary. This audit identifies no such additional candidate.

Intentionally unsupported:

- arbitrary `.lilcontainer`, `.lilblock`, `custom.hlsl`, or `custom_insert.hlsl` derivatives.
- niche extensions and locally modified shader code.
- copied or patched canonical assets whose complete R1 provenance is not official.
- unknown generator plugins, postprocessors, package overlays, or source injection.
- VRCFury SPS and other transformed derivative shaders.
- shader overrides, package integrations, or external closures AMUSE has not characterized exactly.
- any material/animation state set or build ordering whose relevant state cannot be proven complete.
- any configuration requiring AMUSE to simulate an open-ended preprocessor or extension ecosystem.

For every unsupported case the result is the same: likely lilToon or not, attestation fails, every semantic output is Unknown, and no optimization runs.

### Identification is not attestation

"Looks like lilToon" is useful diagnostic metadata. It is not authorization to optimize. A future diagnostic may identify likely lilToon from a name, property vocabulary, package path, or custom-container ancestry, while separately reporting why strong attestation failed. The optimization path must keep depending only on exact attestation.

The current census intentionally uses the strong semantic frontend result as `ShaderFamilyAttestation`. So an unattested derivative gets `None`. Improving likely-family statistics is a separate future measurement concern, and must not weaken the production predicate or change the census schema in this milestone.

### Final architecture reassessment

The two-layer architecture survives the broader audit with one refinement:

1. **Layer 1 — canonical semantic/generated evidence:** keep R1/R2/R3 and the current exact identity, include-tree, generated digest, render-mode, and output-local semantic checks.
2. **Layer 2 — canonicalization provenance and dependency identity:** before canonicalization, require the complete R1-removed record to match the closed official generator domain. Partition the three dependency activators. Enforce exact absence/presence, counts, and locations for the selected profile. Attest every enabled external closure plus relevant earlier macro provenance.

This is sufficient because arbitrary extension code has only three outcomes: it produces a distinct/digest-visible shader, it changes raw erased evidence that Layer 2 refuses, or it escapes the observable/ordered lifecycle and stays unsupported. AMUSE does not need to understand the extension's semantics to refuse it.

The smallest first production hardening scope is standalone-only:

- keep every existing pin and R1/R2/R3 rule.
- validate the complete raw removed-token domain against official 2.3.4 generator output.
- require all three external activators to be absent everywhere, and reject duplicates or wrong positions.
- add focused negative witnesses for hidden known and unknown R1 tokens.
- leave every integrated and custom configuration unsupported.

There is enough evidence to design that focused hardening branch after review. There is not enough evidence to implement an LTCGI, AudioLink, Light Volumes, custom-shader, or transformed-derivative profile. This investigation is complete for the architectural question, and deliberately stops before an implementation plan.

### Answers to the completion gate

1. **Yes, precisely qualified:** the three activators are exhaustive for official lilToon 2.3.4's upstream-declared external package shader-source channels on the supported BRP base-opaque path. They are not exhaustive of arbitrary third-party patching.
2. **Official custom shaders are distinct:** normal custom containers, blocks, inserts, and includes generate a different asset/identity or digest-visible source. They do not invisibly extend the canonical base. Mutation inside R1 is the one generic caveat, and the complete removed-token provenance invariant refuses it.
3. **The audit found no pre-snapshot false positive in the representative mainstream tools it names:** they either create a derivative shader, explicitly replace the shader, or mutate material state that AMUSE must observe. Post-snapshot mutation stays an ordering prerequisite, because AMUSE has no NDMF pass yet.
4. **Yes:** the two-layer architecture is sufficient when Layer 2 covers complete canonicalization provenance and unknown mechanisms fail closed.
5. **Intentional refusals:** all arbitrary custom/modified/derivative shaders, unknown generators and injections, unclosed integrations, unproven material-state sets, and unbounded lifecycle/compiler mutations.
6. **Yes for standalone hardening, no for integration support:** the evidence is closed enough for a focused production design that prevents R1-hidden activators and unknown erased tokens while preserving current pins. Every integrated profile stays a separate characterization milestone.

## Required validation for focused standalone hardening

### Positive cases

- existing standalone default lilToon 2.3.4 attests.
- existing standalone stripped-settings lilToon 2.3.4 attests.
- every R1 token emitted by the official default and characterized stripped generators is recognized by the closed provenance domain.
- legitimate R1 setting variation keeps the existing canonical pins and standalone result.

### Negative cases

- wrong lilToon package version rejects.
- either generated source digest modified rejects.
- any of the three external activators in standalone raw source rejects, including R1-hidden, duplicated, or relocated forms that keep the existing digest.
- an unknown `LIL_FEATURE_*`, `LIL_OPTIMIZE_*`, or other R1-removed define rejects even when canonical output is unchanged.
- a malformed, extra, or wrongly positioned removed-region record rejects.
- a custom include, executable insertion, replacement, redirected include, or edited include-tree member rejects through existing digest evidence.
- a custom `.lilcontainer` declaring shader name `lilToon` rejects on asset/source identity.
- a transformed derivative shader rejects rather than inheriting canonical lilToon support.
- unknown shader with similar properties rejects.
- common differently named lilToon variants stay rejected.

Integrated-profile validation stays deferred. Any future profile must also prove its exact activator record, external package version, complete active closure, upstream macro provenance, and default/stripped stability before it can become a positive case.

### Repository and Lab validation

- focused attestation tests pass in the public project.
- the full public EditMode suite passes.
- no Unity host-toolchain churn is staged or committed.
- before each Unity result, the investigation re-establishes instance identity from exact `Application.dataPath`.
- once an NDMF pass exists, ordering tests prove the evidence snapshot follows every allowed relevant material/source transformation, and stays valid afterward.
- a Lab production-path test is required only for an approved exact profile, and uses in-memory fixtures without modifying Lab package, generated shader, setting, scene, prefab, material, or asset state.

## Evidence anchors

The repository implementation and characterization anchors used for this investigation are:

- `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`
- `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`
- `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`
- `Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs`
- `docs/superpowers/specs/2026-08-17-liltoon-semantics-adapter-design.md`
- `docs/superpowers/plans/2026-08-17-liltoon-semantics-adapter.md`
- `docs/superpowers/specs/2026-08-20-census-lab-preparation-results.md`

The upstream artifacts compared were the official [lilToon 2.3.4 release](https://github.com/lilxyzw/lilToon/releases/tag/2.3.4) and official [LTCGI releases](https://github.com/pimaker/ltcgi/releases), selecting tag `v1.7.3`. Archive digests are recorded above, so a later review can verify that it is comparing the same downloads. The archives and comparison scratch data stayed temporary, and the investigation did not add them to the repository.

Representative third-party mechanism evidence came from official source at fixed commits: Avatar Optimizer's [NDMF phase configuration](https://github.com/anatawa12/AvatarOptimizer/blob/6e6babc53c4086e7b1038b50dc01b1e36f065ef1/Editor/OptimizerPlugin.cs) and [Shader Information API](https://github.com/anatawa12/AvatarOptimizer/blob/6e6babc53c4086e7b1038b50dc01b1e36f065ef1/.docs/content/docs/developers/shader-information/index.md), TexTransTool's [NDMF phase configuration](https://github.com/ReinaS-64892/TexTransTool/blob/741b7dc3febc1d77269f267f4cf139db0f12492a/Editor/NDMF/NDMFPlugin.cs) and [material modifier](https://github.com/ReinaS-64892/TexTransTool/blob/741b7dc3febc1d77269f267f4cf139db0f12492a/Runtime/CommonComponent/MaterialModifier.cs), Modular Avatar's [plugin definition](https://github.com/bdunderscore/modular-avatar/blob/f8c5fd98463e1024cae0608d5449b3c1fb6b6c84/Editor/PluginDefinition/PluginDefinition.cs), and VRCFury's [SPS shader patcher](https://github.com/VRCFury/VRCFury/blob/b5e9f9630e40e93c47fe06f5aa71897dba92cfca/com.vrcfury.vrcfury/Editor-Common/Builder/Haptics/SpsPatcher.cs). These sources show mechanisms and ordering only. They are not prevalence measurements or support requirements.

## Validation performed in this investigation

- public development project full EditMode baseline: 812 passed, 0 failed, 0 skipped.
- Census Lab focused negative reachability reproduction: 1 passed, 0 failed, 0 skipped.
- direct evidence extraction and verifier diagnostic capture.
- in-memory canonical subtraction proving the exact three-line/token LTCGI delta reproduces both existing pins.
- official lilToon 2.3.4 release comparison.
- official LTCGI 1.7.3 tag comparison.
- direct refusal characterization for five common differently named lilToon variants.
- fresh Unity 2022.3.22f1 scratch generation with the official packages for default and stripped settings.
- independent raw and canonical digest comparison for both generated states.
- recursive active-preprocessor dependency walk from lilToon's LTCGI entry, resolving the six-file external closure with no unresolved or Unity/platform edges.
- normalized per-file and closure digest measurement for the active LTCGI source.
- full relevant preprocessor-symbol provenance inventory for the proposed compound profile.
- scratch-only AudioLink pre-include witness proving `LTCGI_AUDIOLINK` can come from unattested source.
- canonicalization witness proving `LIL_FEATURE_AUDIOLINK_PACKAGE` retains proposed pass digest `c666f898...` while adding that earlier include.
- complete R1 category/use audit across the official generator and attested include sources, separating internal semantic gates, optimizer records, optimized-input state, skip variants, and third-party activators.
- complete official 2.3.4 asmdef and literal package-include inventory for the supported BRP base-opaque target.
- VRC Light Volumes pre-include witness proving earlier external source can export macro state consumed by later LTCGI code.
- standalone witness proving a hidden R1 `LIL_FEATURE_LTCGI` retains existing pass pin `6b6c30c1...` while activating external LTCGI source.
- source-backed R2 variant-availability audit and R3 path-identity/ambiguity audit, with no analogous trust collapse found.
- comparison of five replacement trust models and definition of the two-layer fixed-profile evidence boundary.
- full official custom-generation mechanism trace through `.lilcontainer`, `.lilblock`, `CustomShaderResources`, adjacent custom data, insert/replace expansion, importer identity, and canonical base regeneration.
- negative official-source search confirming `custom.hlsl` and `custom_insert.hlsl` have no special canonical-base hook in 2.3.4.
- official generator/postprocessor hook inventory confirming no arbitrary external callback registry for canonical shader generation.
- representative official-source inspection of Avatar Optimizer, TexTransTool, Modular Avatar, and VRCFury shader/material transformation mechanisms and NDMF phase declarations.
- repository-wide confirmation that AMUSE currently exports no NDMF plugin/pass or ordering constraint.
- A-E mechanism classification and refinement of Layer 2 from three-symbol extraction to complete removed-token provenance plus exact activation identity.
- repository status and manifest/lock diffs, inspected after Unity validation. Unity had added host-specific macOS-to-Linux toolchain/sysroot entries. The complete diff contained only that prohibited machine churn. The investigation restored those two generated files to `HEAD` before the audit continued.

The ecosystem continuation did not run Unity or access the Census Lab: its new claims came from the official 2.3.4 archive, repository source, and read-only official third-party repositories. It changed no production code, test, package metadata, census schema, runner, serialization, provenance, Poiyomi behavior, or unrelated milestone.
