# The `_MainTex` build-copy clone in the Census Lab

Privacy note: This record uses a private avatar in the Census Lab project as test input. It names no avatar, scene, renderer, material, texture, animation clip, or asset path. It describes optimizer components by product name and by their texture-related settings, because those settings are the finding. It holds no per-renderer or per-slot table. It writes no instance names, hashes, ports, or machine paths. Counts are observed session counts.

Date: 2026-09-27.
Branch: `feat/maintex-alpha-test-support`.
Base: `main` at `0052076`.
Investigation status on 2026-09-27: no production code changed. The work was read-only against the Census Lab editor instance and against installed package sources. The direct fingerprint confirmation needs one operator-run play-mode build. The conclusion carries without it.

## Question

The 2026-09-26 investigation and its 2026-09-27 follow-up measured one remaining refusal. A lilToon cutout slot with an assigned Main texture refuses capture at build time with the reason `UnavailableCapture`, and the enriched report says the texture has no source identity. The texture the capturer sees on the build copy is an in-memory object that no project asset backs. This record answers two questions. Which pass in the play-mode chain replaces the assigned imported texture with that source-less object? What support options does the evidence allow?

## Method

All live queries ran against the pinned Census Lab editor instance. Before the first query, and again after every pause, the instance check returned the project's Assets folder path and the corpus folder marker, and both matched the Census Lab project. The dev editor instance was never addressed.

The clone hunt was static text analysis of the installed package sources in the Census Lab project. The three large tools ship C# source, so no decompilation was needed. For each candidate, this session read the pass entry point and every texture-creation site, and then read the trigger conditions. AMUSE code came from the dev repository, not from the Lab's embedded copy. The Lab's embedded AMUSE copy was never read, compared, or edited, so no assumption about its sync state was needed.

The console read on 2026-09-27 returned the NDMF pass log of the most recent completed play-mode build. The confirmation build is an operator run that stays in play mode, so a read-only query can fingerprint the built avatar's textures.

## The measured build chain

On 2026-09-27, the console log of the most recent play-mode build showed the whole chain in order. The play start clone step ran first. A registered VRCFury component cloned the scene avatar and VRCFury's services processed that clone. The service list contains animator, menu, collider, and audio fixes. No service in the list creates material texture copies. VRCFury registers no NDMF plugin in this project, so its only build entry is that play-start step and its SDK hook wrapper.

The SDK preprocess step ran next. Its callback list drove the NDMF build. The NDMF phases ran in order: Resolving, Generating, Transforming, Optimizing, PlatformFinish. The Optimizing phase held every texture-capable optimizer pass. Their observed times on this avatar:

- The Avatar Optimizer texture pass `T&O: OptimizeTexture` ran in 1716 ms. This is the only texture-copy pass in the chain that did real work on this avatar.
- The Avatar Compressor texture pass ran in 2 ms. Its trigger component is absent from the avatar, so the pass returned early.
- The Avatar Optimizer size-limit pass `MaxTextureSizeProcessor` ran in 0 ms. Its trigger component is absent from the avatar.
- The Avatar Optimizer slot merge pass ran in 0 ms. Its manual merge components are absent.
- No d4rkAvatarOptimizer execution lines appear anywhere in the log. Its component sits on the avatar and is enabled, but its texture merging toggles are all off, so its pipeline cannot copy a texture even when it runs.

The PlatformFinish phase ran after that. AMUSE's passes ran there and produced the measured refusal. The NDMF streaming pass also runs in PlatformFinish. Its source reads materials, sorts textures into persistent and temporary lists, and emits warnings. It writes nothing, so it cannot be the producer.

One NDMF error line in the log names a structural fact that the earlier investigation also measured: a mesh on this avatar uses the same material on more than one slot.

## The static clone-site census

The census asked one question per tool. Can this tool create a texture object during the build and assign it into a material texture slot that AMUSE reads? The results:

- Avatar Optimizer 1.9.17: three sites can. The atlas pass, the size-limit pass, and the manual material merge passes. The atlas pass is part of Trace and Optimize. The avatar's Trace and Optimize component has the atlas setting on. The other two sites are inert on this avatar, as the pass times above show.
- Avatar Compressor: its texture pass can copy textures, but the trigger component is absent, so the pass exits before any work.
- d4rkAvatarOptimizer: its texture merging toggles are off on this avatar. Its merge code copies texture references from source materials when merging is off, so it keeps the original asset references.
- Modular Avatar 1.18.2: two sites can create textures. One resizes expression menu icons and saves the result as a build asset. One builds a scratch copy while filtering mesh vertices by a mask, and never assigns that scratch copy into a material. Neither site touches a lilToon material's Main texture.
- VRCFury: one site creates textures for its haptic mask feature, for its own new materials. One scaling utility has no callers in this version.
- VRChat SDK 3.10.4: texture creation exists only in import, thumbnail, and editor UI code. The play-mode avatar step instantiates the avatar and shares texture assets.
- NDMF 1.14.4: the streaming pass reads and warns only.

By elimination and by the pass log, one producer remains: the Avatar Optimizer atlas pass.

## The producing pass

The atlas pass groups the avatar's material textures into connected components and rebuilds each group as one atlas. On 2026-09-27, its source shows the exact properties of its output objects:

- It creates each atlas as a plain in-memory `Texture2D`. It never saves that object as an asset, and the file contains no asset-saver call.
- The non-trivial output copies the source texture's name plus a fixed suffix, `(AAO UV Packed)`. A solid-color output is a one-pixel texture named `AAO Monotone` followed by the color.
- It recompresses the atlas back into the source texture's format. A BC7 source stays BC7.
- It copies wrap mode, filter mode, anisotropy, mip bias, and the streaming settings from the source texture.
- The atlas mip count is the smaller of the size-derived count and the source mip count.
- It rewrites the mesh UVs of the group and points the material at the atlas.

The pass runs in the NDMF Optimizing phase. AMUSE runs in the PlatformFinish phase, which follows it. So at capture time the slot's texture is an atlas object that no project asset backs. The identity gate in `UnityTextureEvidence.TryGetSourceId` asks the asset database for the object's project identity. That call fails for an in-memory object, so the capture refuses with `UnavailableCapture` before the format and mip gates run. This matches the measured refusal exactly, including the measured fact that the run never reached the clone route.

The name suffix makes the confirmation direct. If the built avatar's cutout slot holds a texture whose name ends with `(AAO UV Packed)` or starts with `AAO Monotone`, holds no project identity, and is not readable, the producer is proven by observation.

## Live confirmation

On 2026-09-27, the direct fingerprint build had not yet run when this record reached its delivered state. The conclusion does not depend on it. Three measured facts carry it. First, the atlas pass executed on this avatar in the Optimizing phase with 1716 ms of work, and it is the only pass in the chain that has both the capability and a real execution. Second, every other capable pass or hook is inert on this avatar, by absent trigger components, disabled settings, or absent execution lines. Third, the object the atlas pass produces, an unsaved in-memory texture with no registry entry, has exactly the properties that the measured refusal requires.

The confirmation step is one query after any future play-mode build. The built avatar's cutout slot must hold a texture with no project identity, a name that ends with `(AAO UV Packed)` or starts with `AAO Monotone`, the source format, copied streaming settings, and a non-readable CPU copy. If a future build shows the original asset on that slot instead, this conclusion is wrong and the record must be reopened.

## What the ecosystem does about build-copy identity

NDMF ships a public object registry for exactly this problem. A pass that replaces an object registers the pair of the old object and the new object. A later pass asks the registry for the reference of the build-copy object and receives the chain back to the source object. The NDMF best-practice documentation tells producers to register every clone.

The census shows how the installed tools follow that contract:

- Avatar Optimizer registers replaced meshes, replaced materials, and replaced animation clips in the registry. Its atlas textures are the exception: the atlas file registers nothing.
- Modular Avatar registers replaced meshes.
- d4rkAvatarOptimizer keeps its mappings inside its own single pass and needs no cross-pass identity.
- The registry answers a query for an unregistered object with a reference to that object itself. It never guesses a source from a name or a path.

So identity reconstruction for this atlas object is not available today from the registry, and the registry is the only honest reconstruction route in this ecosystem.

## Options

Option one, reconstruct identity from the authoring asset. For this refusal the route does not exist. The atlas object is unregistered, so the registry returns the object itself. Its name embeds the source texture's name, but AMUSE's identity rule forbids fabricating identity from names. A deeper problem sits under that: the atlas is not a copy. Its pixels moved, its UVs moved, and its mip chain can differ. Capturing the source asset's alpha field and applying it to the atlas would prove the wrong sampling domain. Honest reconstruction needs atlas-aware domain reasoning, or an upstream registration that maps the atlas back to its source. The registration change belongs to the atlas producer, not to AMUSE.

Option two, keep the refusal and sharpen the named guidance. The report already separates no-identity refusals from route refusals. The next step is report text only: when the texture has no source identity, the hint can name the mechanism, an upstream optimizer replaced the material texture with an in-memory atlas copy, and point at the atlas setting of the Trace and Optimize component as the likely producer. This carries no correctness risk and turns each future refusal into a self-explaining report.

Option three, capture the clone without asset identity. A per-build transient identity for unsaved build objects would let capture run, but it breaks the current evidence rule that identity comes from project facts, and every consumer of the source id would need a new identity kind. That is a new dated spec, not a fix.

## Recommendation

Keep the refusal. Ship option two, the sharper report text, as a small report-string change on a future branch. Ask the Avatar Optimizer project to register its atlas textures in the NDMF object registry. If that registration lands, revisit option one with a design that treats the atlas as a transformed domain, not as a clone.

## Scope and limits

This investigation changed no production code and added no instrumentation. It ran no tests in the Census Lab. The static findings are pinned to the installed versions on 2026-09-27: Avatar Optimizer 1.9.17, Modular Avatar 1.18.2, Avatar Compressor 0.9.0, d4rkAvatarOptimizer 4.5.4, VRCFury 1.1426.0, VRChat SDK 3.10.4, NDMF 1.14.4. The lab editor showed an out-of-date notice for Avatar Optimizer, so later versions can differ. The AtlasConnectedComponent eligibility rules depend on live scene state, so the producing-pass conclusion rests on the executed pass, the enabled setting, and the live fingerprint check, not on a full reimplementation of those rules.
