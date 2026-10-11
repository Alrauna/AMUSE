Privacy note: this record contains sanitized Census Lab findings. It omits private asset names, paths, identifiers, counts, and per-avatar results.

# lilToon opaque conversion regression investigation

Date: 2026-10-10. Status on this date: investigation, runtime research, and adversarial review complete. The runtime questions listed below remain unresolved.

Repository baseline: `main` at `adfcd17`, after PR #139 and PR #140.

This record reports an investigation. It does not approve a design, a spec, a repair, or a change to the correctness contract.

## Evidence labels

- `[SOURCE]` means a fact from repository source or linked primary documentation.
- `[MEASURED]` means a result from a read-only probe in this session on 2026-10-10.
- `[REPORTED]` means the user's observation, not an independent measurement.
- `[INFERENCE]` means a conclusion from the cited facts.
- `[LIMIT]` means a boundary of the available evidence.

A mip level is a smaller version of a texture. Anisotropic filtering samples an elongated texture region at shallow viewing angles.

## Question and scope

The user reported that personal avatars lost regular lilToon cutout and transparent conversion after PR #139 or PR #140. `[REPORTED]`

The user prioritized conversion over reporting. The investigation inspected the Census Lab NDMF Console and used three parallel read-only source investigations.

The follow-up request adds online research about VRChat runtime filtering. It also requires an adversarial review of this record. No design or spec is in scope.

## Local answer

PR #139 introduced a confirmed contributor to the lost opacity proofs. It changed how AMUSE interprets texture anisotropy level 1 under Forced On. `[SOURCE]` `[MEASURED]`

The Census Lab editor instance uses `AnisotropicFiltering.ForceEnable`. The observed lilToon main textures use `anisoLevel == 1`. `[MEASURED]`

The classifier cannot prove a local opaque region under anisotropic sampling when a consulted mip contains both opaque and non-opaque texels. `[SOURCE]`

The existing reporting path does not explain this resolved-but-unproven outcome. With zero opaque candidates, preparation returns `null` without a conversion refusal. `[SOURCE]`

No probe observed the reported failing build take that exit. Applying the source chain to that build remains an inference. `[INFERENCE]` `[LIMIT]`

PR #140 repaired a separate LTCGI source-attestation defect from PR #139. Strict opaque-target attestation passed in the Census Lab editor instance. `[SOURCE]` `[MEASURED]`

These results do not prove that changing the sampler assumption restores the full animation-closed build. The differential probe covered main-texture classification only. `[LIMIT]`

## Observed NDMF Console

All observations in this section are dated 2026-10-10.

The selected NDMF Console report contained the following public report categories:

- `amuse.renderer.AdmittedMaterialSemanticsUnknown`.
- `amuse.slotAnalysis.Refusal`.
- `amuse.summary.Title`.

The selected summary reported no triangles moved. Its slot detail templates identified `UnsupportedShader`, not `UnattestedShader`. `[MEASURED]`

Other retained NDMF reports contained more refusal categories and different conversion outcomes. They were not the selected report. `[MEASURED]`

Do not attribute the selected unsupported-shader messages to the recognized regular lilToon materials. Their direct alpha analysis returned complete semantics. `[MEASURED]` `[LIMIT]`

## Read-only probes

The intended Census Lab project identity matched the reachable editor's `Application.dataPath` before the probes. The session pinned that editor instance.

All results below are dated 2026-10-10. The probes returned sanitized categories or Boolean results. They did not return private asset identities.

### Family selection and alpha semantics

`UnityMaterialSemantics.TrySelectAlphaMaterialRequests` found regular `LilToonCutout` and `LilToonTransparent` materials in the loaded scenes. `[MEASURED]`

No `LilToonMulti` source family appeared in this scan. This does not establish a property of the entire private corpus. `[MEASURED]` `[LIMIT]`

`CaptureAlphaMaterials` followed by strict `AnalyzeAlphaMaterial` returned `Alpha.IsComplete == true` for the observed regular cutout and transparent materials. `[MEASURED]`

The results contained no `AlphaUnknownReason` for these regular materials. This separates semantic resolution from the later triangle-classification failure. `[MEASURED]`

### Sampler and texture evidence

The editor reported `QualitySettings.anisotropicFiltering == ForceEnable`. The observed lilToon main samplers used Bilinear filtering, Repeat wrapping, and anisotropy level 1. `[MEASURED]`

The captured main-texture evidence reported `Sampling.Aniso == Anisotropic`, `HasAlphaChannel == true`, and `AlphaCaptureRefusal == None`. `[MEASURED]`

The captured mip data included mixed, fully opaque, and fully non-opaque levels. These are aggregate categories, not per-texture results. `[MEASURED]`

The observed main-texture imports used the Default texture type. The scan found both alpha-transparency states and no mip coverage preservation. `[MEASURED]`

### Synthetic classifier differential

The probe used the loaded production `TriangleAlphaClassifier`. It supplied a synthetic mixed alpha field and a triangle inside an opaque region.

The geometry, field, filter, wrap, and zero UV envelope stayed fixed. Only the sampler's anisotropy classification changed.

`TextureAnisoMode.None` returned `ProvenOpaque`. `TextureAnisoMode.Anisotropic` returned `Unknown`. `[MEASURED]`

This probe exercised the classification gate. It did not measure GPU filtering or render an image. `[LIMIT]`

### Differential on real geometry

The probe sampled real source triangles. It used the same captured main-texture fields, mesh geometry, UV scale, and UV offset for both sampler assumptions.

The first comparison consulted the complete captured mip chain. It found lost individual-mip proofs, but no lost complete-chain proof among the sampled triangles. `[MEASURED]`

That comparison did not use the avatar's saved mip scope. It therefore did not represent the build's accepted mip range. `[LIMIT]`

The second comparison applied each source optimizer's saved maximum mip level and minimum texture size. No alpha flattening or polygon noise allowance appeared in the scene-policy scan. `[MEASURED]`

The finish plugin reads the saved mip cap and minimum texture size. The field collector caps each proof chain to that scope. `[SOURCE]`

Sources: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:559-562` and `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:724-810`.

Under this saved scope, sampled triangles in both regular cutout and transparent families lost their complete main-texture proof. `[MEASURED]`

The old level-1 sampler assumption returned `ProvenOpaque` across every consulted mip. The new assumption did not. `[MEASURED]`

The probe observed unchanged renderer mesh and material references. It did not change quality configuration or source assets. `[MEASURED]`

This was a sampled main-texture differential. It did not replay material swaps, animation closure, conversion eligibility, clone generation, apply, or the full NDMF build. `[LIMIT]`

The real-geometry probe used `Vector2.Scale` plus offset and a zero UV envelope. It did not reproduce the resolver's exact affine transform.

### Base-state renderer analysis

`UnityRendererAlphaAnalysis.Analyze(Renderer)` returned `RendererAnalysisRefusal.None` for the inspected renderers with regular lilToon alpha materials. `[MEASURED]`

Their regular lilToon submeshes produced `Unknown` and `MustRemainTransparent`, with no `ProvenOpaque` outcome. `[MEASURED]`

This entry point uses the full mip chain and zero noise allowance. It does not use the saved build policy or close animation. `[SOURCE]` `[LIMIT]`

### Opaque-target attestation

The probe resolved the canonical opaque target for the observed regular lilToon source materials.

`TryGatherSourceEvidenceForShaderName` succeeded. Strict `TryVerifyIdentityForShaderName` also succeeded. `[MEASURED]`

This probe did not create an opaque clone. It rules out the earlier Allman source-attestation defect for the targets that the probe examined. `[LIMIT]`

## Causal source changes

### PR #139 changed the sampler classification

Before PR #139, `UnityTextureEvidence.TryGetSampling` classified anisotropy only when `texture.anisoLevel > 1`. `[SOURCE]`

PR #139 also classifies level 1 as anisotropic when `LevelOneSamplesAnisotropically()` returns true. That method reads the live editor quality mode. `[SOURCE]`

The causal decision is `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs:148-151`. The mode read is `UnityTextureEvidence.cs:165-170`.

The comparison base is `c375101`. The change landed in `bca07f0` through PR #139.

### The existing classifier rejects mixed anisotropic levels

`TriangleAlphaClassifier.Classify` first accepts a fully opaque level. It also preserves a fully non-opaque level. `[SOURCE]`

For a mixed level, `Sampling.Aniso == Anisotropic` returns `Unknown` before the local filter proof runs. `[SOURCE]`

Source: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:346-365`.

`AlphaResolution.Classify` requires every consulted mip to prove opacity. A later transparent result takes precedence over an earlier unknown result. `[SOURCE]`

Source: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:374-451`.

The new sampler classification activates this existing broad refusal on the Lab's level-1 textures. `[INFERENCE]` The real-geometry differential confirms lost proofs under the saved mip scope. `[MEASURED]`

### The existing reporting path hides zero candidates

`AdmittedMaterialStates.ResolveSlot` reports `AdmittedMaterialSemanticsUnknown` when semantic resolution fails. A resolved equation with unknown triangle outcomes does not take this path. `[SOURCE]`

Source: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs:298-330`.

The build reports slot-analysis refusals only for unresolved slots. `[SOURCE]`

Source: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:709-733`.

`AlphaSeparationPreparation.Prepare` returns `null` when `plan.HasAnyOpaqueCandidates` is false. This exit occurs before conversion and coverage gates. `[SOURCE]`

Source: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:118-120`.

Thus, `OpaqueCoverageBelowMinimum` does not explain an all-unknown result. Its later gate is unreachable when there are no candidates. `[SOURCE]`

The renderer description says that materials resolved but every triangle returned unknown. Its emission predicate instead tests semantic-resolution failure. `[SOURCE]`

Sources: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:129-136` and `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs:302-324`.

This wording can misdirect the reader toward triangle classification. It does not report the resolved, zero-candidate outcome described here. `[INFERENCE]`

### The fixtures avoid the triggering quality mode

The lilToon fixture base saves the editor quality mode, sets it to `AnisotropicFiltering.Enable`, and restores it during teardown. `[SOURCE]`

Its comment explicitly explains that Forced On makes default level-1 fixture textures anisotropic and blocks the local proofs. `[SOURCE]`

Source: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs:48-70`.

The evidence tests cover level 1 under Forced On, Per Texture, and Disable. They assert the sampler classification, not end-to-end conversion. `[SOURCE]`

Source: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs:561-639`.

These tests support the intended mode-dependent gate. They do not establish conversion coverage across editor quality modes. `[LIMIT]`

### Other PR #139 changes not isolated by these probes

PR #139 also retracted some non-opaque resolver claims to `Unknown`. It added NaN multiplier refusals. `[SOURCE]`

Sources: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:239-242`, `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:546-550`, and `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:643-647`.

The same PR refactored conversion-outcome mapping in `AlphaSeparationPreparation`. The earlier code already deferred clone creation until eligibility succeeded. `[SOURCE]`

Source: the `c375101` to `bca07f0` diff for `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`.

The sampler-only differential did not isolate resolver, conversion, or apply changes in a full build. No measured result ties these co-changes to the loss. `[LIMIT]`

The record therefore identifies a confirmed lost prerequisite. It does not establish the sampler change as the only contributor to every reported build failure. `[LIMIT]`


## PR #140 and ruled-out explanations

PR #139 also restricted LTCGI token normalization to the `SubShader` scope. Its first scope walk missed the shipped Allman brace form. `[SOURCE]`

PR #140 added the pending `SubShader` state for that form. The repair appears in `LilToonSourceAttestation.cs:1275-1335`. `[SOURCE]`

The earlier investigation describes the defect before the repair:
`docs/superpowers/investigations/2026-10-10-ltcgi-liltoon-consent-regression-investigation.md`.

The strict live target-attestation result in this record supersedes that record's unmeasured post-repair target state. It does not invalidate its historical findings.

The following explanations did not match the inspected regular lilToon path:

- A general source-identity rejection. Strict regular alpha analysis completed.
- Missing main-texture alpha evidence. Capture succeeded without a capture refusal.
- A remaining opaque-target Allman defect. Strict target attestation passed.
- A Multi capture failure. The loaded-scene scan found no Multi source family.

These exclusions apply to the inspected source state. They do not cover every animation-admitted material or every avatar in the private corpus. `[LIMIT]`

## Unity filtering facts

The [Unity 2022.3 `Texture.anisoLevel` documentation](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture-anisoLevel.html) states the following. `[SOURCE]`

- Level 1 normally means no anisotropic filtering.
- Level 0 stays non-anisotropic when global filtering is Forced On.
- Forced On raises texture levels 1 through 9 to level 9.
- Metal, Vulkan, and OpenGL control mip filtering and anisotropy independently.
- On other graphics APIs, anisotropy also enables trilinear filtering.

These facts support the conservative gate for the measured editor mode. They do not establish the mode of any VRChat client. `[LIMIT]`

The earlier design calls the editor-to-player mapping a residual limitation. It also asserts that VRChat normally uses Per Texture. `[SOURCE]`

Source: `docs/superpowers/specs/2026-10-09-semantics-evidence-robustness-design.md:360`.

The primary VRChat sources below contradict the broad Per Texture claim. The editor's live configuration still does not prove a client's configuration. `[SOURCE]` `[LIMIT]`

## VRChat runtime research

Research in this section is dated 2026-10-10. Two read-only subagents researched public sources. The parent inspected the cited primary evidence. These are findings, not a proposed sampling contract.

### Published PC profiles

The official [VRChat Performance Options](https://docs.vrchat.com/docs/vrchat-configuration-window) page says that PC users can select graphics profiles. It names VRC Ultra as the default. `[SOURCE]`

The published profile screenshots all show `Anisotropic Textures: Forced On`. The session read these rows directly in the linked images. `[SOURCE]`

- [VRC Low](https://files.readme.io/a5a014e-VRC_Low.png).
- [VRC Medium](https://files.readme.io/44ddc96-VRC_Medium.png).
- [VRC High](https://files.readme.io/f2086c5-VRC_High.png).
- [VRC Ultra](https://files.readme.io/0321a11-VRC_Ultra.png).
- [VRC Mobile](https://files.readme.io/e10968c-VRC_Mobile.png).

The page metadata reports an update date of 2025-11-13. It does not establish when VRChat captured each screenshot. `[SOURCE]` `[LIMIT]`

These published rows contradict a universal claim that VRChat normally uses Per Texture. They do not measure the running client in this session. `[INFERENCE]` `[LIMIT]`

### User and world controls

The documented graphics menu exposes profile selection. The [configuration file](https://docs.vrchat.com/docs/configuration-file) and [launch options](https://docs.vrchat.com/docs/launch-options) pages expose no separate anisotropy control. `[SOURCE]`

This is a bounded documentation search, not proof that an undocumented control cannot exist. `[LIMIT]`

The public [VRCQualitySettings documentation source](https://github.com/vrchat-community/creator-docs/blob/main/Docs/docs/worlds/udon/vrc-graphics/vrc-quality-settings.md) exposes no anisotropy property. `[SOURCE]`

Its world overrides concern shadow distance and shadow cascade splits. These do not establish a world-controlled anisotropy change. `[SOURCE]` `[LIMIT]`

### Dated developer discussion

A [2024-08-11 discussion on the official VRChat forum](https://ask.vrchat.com/t/anisotropic-filtering-defaults-on-quest-android-ios-any-potential-issues/26094) describes desktop filtering as forced on by default. It gives a level of 16x. `[SOURCE]`

The [2024.3.3 release notes](https://docs.vrchat.com/docs/vrchat-202433) independently describe desktop texture-filtering quality as maxed out by default. They do not state an exact desktop level. `[SOURCE]`

The forum post also says that texture level 0 disables anisotropy. This agrees with Unity's documented level-0 exception. `[SOURCE]`

The stated desktop level differs from Unity's general Forced On rule for texture levels 1 through 9. The client implementation remains unmeasured. `[LIMIT]`

Do not derive an exact desktop footprint from the forum number alone. The post discusses a proposed mobile change, not a release contract. `[LIMIT]`

### Quest and mobile texture overrides

The official [VRChat 2024.3.3 release notes](https://docs.vrchat.com/docs/vrchat-202433) describe automatic filtering upgrades on Quest and mobile. `[SOURCE]`

The release notes state that avatar `_MainTex` textures use anisotropy 4x. Other avatar textures use 2x. The avatar texture filter stays Bilinear. `[SOURCE]`

The notes state that avatars always use the baseline. They also name exceptions such as Point sampling, which the client leaves unchanged. `[SOURCE]`

The notes describe world textures separately. They do not authorize applying world texture rules to avatars. `[LIMIT]`

This is first-party evidence that uploaded texture configuration does not alone define runtime filtering on every platform. `[INFERENCE]`

The notes do not expose the exact API or order of the changes. Do not combine their per-texture levels with the Mobile screenshot into a precise effective sampler. `[LIMIT]`

The release note does not name iOS separately. It does not prove that regular lilToon is supported on a mobile platform. `[LIMIT]`

### Asset-bundle boundary

The [Unity 2022.3 AssetBundles manual](https://docs.unity3d.com/2022.3/Documentation/Manual/AssetBundlesIntro.html) defines a bundle as an archive of platform-specific assets. `[SOURCE]`

It explains that serialized objects and texture resource data reside in a bundle. It does not state that loading a bundle replaces application quality configuration. `[SOURCE]` `[LIMIT]`

This supports a distinction between texture asset data and client-wide quality configuration. It does not alone prove VRChat's upload implementation. `[INFERENCE]`

Unity describes [`QualitySettings.anisotropicFiltering`](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/QualitySettings-anisotropicFiltering.html) as a global mode. VRChat describes quality profiles from its own client project. `[SOURCE]`

The upload project's global mode is therefore not evidence that an avatar forces the same mode in the client. `[INFERENCE]`

No inspected source explicitly guarantees that an avatar bundle carries or enforces the upload project's global mode. No bundle serialization experiment ran. `[LIMIT]`

### SDK handling of texture anisotropy

The public-web research found no SDK guarantee that `anisoLevel` remains unchanged through avatar upload. It also found no documented build-time rewrite. `[LIMIT]`

The parent searched the installed SDK 3.10.5 text sources for `anisoLevel`, `anisotropicFiltering`, and `SetGlobalAnisotropicFilteringLimits`. The search returned no matches. `[MEASURED]`

Search scope: `Packages/com.vrchat.base` and `Packages/com.vrchat.avatars`, including files excluded by Git.

The visible avatar builder delegates export to `VRC_SdkBuilder`. It calls `RunExportAndTestAvatarBlueprint` or `RunExportAvatarBlueprint`. `[SOURCE]`

Source: `Packages/com.vrchat.avatars/Editor/VRCSDK/SDK3A/VRCSdkControlPanelAvatarBuilder.cs:2996-3007`.

The installed base SDK includes compiled editor code. The text search does not inspect that code or the VRChat client. `[LIMIT]`

Thus, the investigation did not prove that the SDK preserves the value. The documented mobile override independently proves that runtime filtering can differ from uploaded texture configuration. `[LIMIT]` `[SOURCE]`

### Answers and unresolved facts

The documented controls select client quality profiles. No separate anisotropy control appeared in the inspected menu, configuration, launch, or world-API documentation. `[SOURCE]` `[LIMIT]`

The published PC profiles support Forced On, not Per Texture. Their screenshot dates and later client state remain unmeasured. `[SOURCE]` `[LIMIT]`

The documented mobile avatar baseline includes 4x main-texture filtering and 2x other-texture filtering. The exact API composition and iOS coverage remain unknown. `[SOURCE]` `[LIMIT]`

No inspected upload or world API supplies the exact sampler state of every consuming client. The editor mode alone cannot establish that state. `[LIMIT]` `[INFERENCE]`

These results support platform-specific runtime reasoning. They do not select a sampling contract or prove a bounded anisotropic footprint. `[INFERENCE]` `[LIMIT]`

### Unresolved research questions

1. Which documented profiles and texture overrides remain unchanged in later clients?
2. Which filtering defaults apply to iOS?
3. Which APIs implement the mobile overrides, and in which order?
4. Does a mobile override preserve texture level 0 as an exception?
5. How does the client achieve the stated desktop 16x level?
6. Does compiled SDK export code preserve or rewrite `anisoLevel`?
7. Which hardware limits alter the documented sampler on real devices?

The research found no primary source that resolves these questions. It did not inspect a running VRChat client or reverse-engineer client code. `[LIMIT]`

## Evidence limits and safety

No full before-and-after NDMF build ran in this session. No GPU image comparison or live VRChat client probe ran. `[LIMIT]`

The session did not change source meshes, materials, textures, import configuration, animations, prefabs, or scenes. It did not change editor quality configuration.

No tests ran in the Census Lab. The probes used in-memory execution and read-only source inspection.

Do not treat the old sampler assumption as a safe repair. Unity documents real anisotropic sampling under the measured Forced On mode. `[SOURCE]`

The functionality question remains distinct from the reporting gap. Naming the limitation does not restore lost proofs. `[INFERENCE]`

## Adversarial review

A task subagent completed an adversarial review on 2026-10-10. It inspected source history, source predicates, cited primary documentation, privacy, and causal scope.

The initial review found one medium finding and four low findings. It found no critical or high blockers.

The record now names unexcluded PR #139 co-changes and limits the sampler differential to a lost prerequisite. It does not claim full-build isolation.

The record also adds saved-scope citations, labels the failing-build exit as inference, tightens sampler citations, and includes Disable test coverage.

The reviewer corrected its claim that PR #139 newly made clone creation lazy. The source diff shows that laziness and eligibility gating already existed.

On 2026-10-10, the reviewer inspected the corrected sections and reported that all review blockers were resolved. No design or spec resulted.

The reviewer did not replay the measured Lab probes. It did not inspect the private project or run product validation.
