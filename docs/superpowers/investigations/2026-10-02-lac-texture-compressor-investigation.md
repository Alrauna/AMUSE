# LAC Texture Compressor and the texture identity gate

Privacy note: This record uses only public package sources and this repository's own tests. It names no avatar, renderer, material, texture, animation clip, or asset path. It writes no instance names, hashes, ports, or machine paths. It holds no per-renderer or per-slot table.

Date: 2026-10-02.
Branch: `investigation/lac-texture-compressor`.
Base: `main` at `6517db9`.
Investigation status on 2026-10-02: no production code changed. The work was a static read of the public Limitex source, a static read of this repository, and one focused EditMode test run in the dev editor instance.

## Question

Avatars that carry the LAC Texture Compressor component produce materials AMUSE cannot analyze. This record answers two questions. Where does the component act in the NDMF build pipeline? Why do its materials become unanalyzable?

## Method

On 2026-10-02 this session read the public Limitex Avatar Compressor repository at tree `3f3146e` and the AMUSE sources on this branch. No package was installed. The focused characterization run used the dev editor instance after the project identity check passed, and it ran two test classes in `Alrauna.Amuse.Tests.Editor`: `UnityTextureEvidenceTests` and `GeneratedTextureAttestationTests`. The observed result was 31 passed, 0 failed, 0 skipped, in 5.4 seconds.

## Pipeline placement

The component is `TextureCompressor` in package `dev.limitex.avatar-compressor`. On 2026-10-02 the public main manifest read version 0.1.0. The 2026-09-27 investigation lists Avatar Compressor 0.9.0 as the installed version in the Census Lab project, so the installed copy can differ from the read source.

One NDMF plugin registers the pass. Its qualified name is `dev.limitex.avatar-compressor.texture`. It runs in the Optimizing phase, after Modular Avatar and before TexTransTool and Avatar Optimizer. It activates the `AnimatorServicesContext` extension. AMUSE's passes run in the PlatformFinish phase, so AMUSE reads the avatar after the compressor finishes.

The pass needs the component on the avatar and reads only the first one. It then does this work on the build copy:

1. It collects every material from every renderer, from animation object curves, and from every other component through serialization.
2. It clones every collected material with `Object.Instantiate`, renames the clone with a `_clone` suffix, registers the pair in NDMF's object registry, and points every renderer slot at the clone.
3. When lilToon baking is on, it bakes lilToon texture adjustments into the main texture with the `Hidden/ltsother_baker` shader and clears the consumed input slots.
4. When unused-slot detection is on, it clears texture slots whose lilToon feature toggle is off and not animated.
5. For each collected texture above its size filters, it resizes into a new in-memory `Texture2D`, compresses it with `EditorUtility.CompressTexture`, forces the streaming mipmaps flag on, registers the pair in the object registry, and assigns the copy into the cloned material.
6. It rewrites animation object curves to the clones and compressed copies, and it destroys its own component.

The desktop format selector produces DXT1, DXT5, BC7, and BC5. The mobile selector produces ASTC 4x4, 6x6, and 8x8. Frozen settings can force BC5 and ASTC on any platform. The replacement texture keeps the source name plus a `_compressed` suffix. It is never saved as an asset.

## Root cause

AMUSE refuses every one of these replacement textures at its identity gate, and the refusal is by design.

The alpha capture starts in `UnityAlphaFieldEvidence.TryCapture`. Its third gate resolves a stable project identity through `UnityTextureEvidence.TryGetSourceId`, which needs `AssetDatabase.TryGetGUIDAndLocalFileIdentifier` to succeed. A plain in-memory `Texture2D` has no asset database entry, so the call fails and the capture refuses with `UnavailableCapture`. The identity gate sits at the front of the gate order, so the capture never reaches the format, mip, or route gates. This is pinned by the test `TryGetSourceId_SceneOnlyTexture_IsRefused`.

The generated-texture allowlist cannot rescue the copy. `GeneratedTextureAttestation.TryIdentifyProducer` requires an asset path and a sub-asset slot inside an attested container. It admits two producer shapes: AAO name markers inside an NDMF `SubAssetContainer`, and the VRCFury build container. A LAC copy is not a sub-asset at all, so no row could match it as of 2026-10-02.

Three further gates would refuse even if an identity existed:

- The format allowlist admits RGBA32, ARGB32, Alpha8, RGB24, DXT1, DXT5, and BC7. LAC's BC5 normal maps, its ASTC outputs, and frozen ASTC or BC5 settings refuse with `UnsupportedFormat`.
- LAC forces the streaming mipmaps flag on. A streaming texture takes the readable-clone route, which copies the asset and its meta file and reimports them. An asset-less object has no asset to copy, so that route also refuses with `UnavailableCapture`.
- The build target gate admits StandaloneWindows64 only.

The downstream chain then degrades analysis exactly as observed. The capture refusal is recorded beside the slot. The resolver answers `MissingTextureEvidence`. Every triangle that samples the texture stays Unknown. No triangle is proven opaque, so no separation is planned, and the material reads as unanalyzable. The report hint for `UnavailableCapture` already describes this shape and, as of 2026-10-02, names Avatar Optimizer's atlas setting as the only known producer.

## What the compressor does not break

The material clone layer is clean. A clone keeps the source shader, so frontend selection and shader attestation are unaffected. The clone is registered in the object registry, and `RegisteredSourceIdentity` resolves registered clones read-only. The animation curve rewrite is also registered, so the animation closure stays consistent. Textures below the size filters, excluded textures, frozen textures, and textures referenced by animation keep their asset identity, so their slots stay analyzable. The symptom is therefore exactly the slots whose textures the compressor replaced.

## Relation to the 2026-09-27 record

The 2026-09-27 investigation pinned the same mechanism for the Avatar Optimizer atlas pass. The response was the generated-texture producer allowlist. That allowlist works because AAO and VRCFury persist their outputs as sub-assets of one container. LAC's copy is a bare in-memory object, so the current attestation basis does not fit it.

## Support options, none implemented

This investigation changed no code. The options carry different risks.

1. Keep refusing and name LAC in the `UnavailableCapture` hint. Small report change, no contract change.
2. Ask upstream to persist replacements as build assets in an attested container, or to expose a durable identity. This is the only path that lets AMUSE prove the shipped pixels without new policy.
3. Add a LAC producer row and capture the live resident object through the generated route. This needs a correctness decision first. LAC identifies its copies only by a name suffix, and the VRCFury precedent avoids trusting names. The generated route also runs before the streaming route, so the forced streaming flag would be harmless there. BC5 and ASTC outputs would still refuse.
4. Resolve identity through the object registry back to the source texture. This is unsound, because the replacement's alpha content differs from the source after resizing and block compression. Proving the source field would prove the wrong texture.

Option 3 is a correctness-contract change and needs a design decision before any production work. Option 1 is the smallest honest improvement. Option 2 is the durable fix.
