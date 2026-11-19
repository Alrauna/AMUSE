# Atlasing locked-material relock spike

Privacy note: This record contains public product and vendor-package findings only. The live probe used synthetic in-memory objects. It contains no private avatar observations, machine identifiers, asset identifiers, or vendor source excerpts beyond short line references.

Date: 2026-11-19.
Status on 2026-11-19: Source spike and one approved live probe complete. No product code changed. The probe residue was deleted. No file outside this record and the linked design documents changed.

## 1. Question

On 2026-11-19 the user stated a working assumption: materials relock during the build. The question is whether that holds for a new AMUSE-owned material that uses an unlocked Poiyomi shader. Atlas outputs for Poiyomi uses would be such materials. Current alpha-separation outputs already are.

## 2. Method

A read-only scout traced the installed sources. I then checked the load-bearing lines myself. The pins on 2026-11-19 were `com.poiyomi.thryeditor` 2.74.4 and `com.poiyomi.toon` 10.0.29. NDMF and VRCFury are the installed copies under `Packages/`. The user approved a live probe on 2026-11-19. Section 4 describes it.

## 3. Source findings

1. NDMF saves generated objects before the vendor lock runs. Its late hook has callback order -1025 (`Packages/nadena.dev.ndmf/Editor/VRChat/BuildFrameworkPreprocessHook.cs:65`). That hook calls `BuildContext.Finish` (`:87`). `Finish` calls `Serialize` (`Packages/nadena.dev.ndmf/Editor/API/BuildContext.cs:553`). `Serialize` walks every asset that the avatar references and saves each object that is not persistent into the build asset container (`:222-268`).
2. The vendor upload lock runs later, at callback order 100 (`Packages/com.poiyomi.thryeditor/Editor/ShaderOptimizer.cs:3213-3215`). It collects renderer materials, materials in descriptor layer clips, and materials in clips of every Animator under the avatar (`:3220-3238`).
3. The lock keeps only materials that have an asset path, are not yet locked, and use a shader with the vendor lock button (`:930-935`). Because of finding 1, AMUSE outputs have an asset path at that point. So the lock selects AMUSE outputs.
4. The lock bakes the current values of untagged properties into the generated program as literals. This includes texture transforms, texel sizes, and UV selectors. Atlas values that AMUSE wrote before the lock therefore enter the locked program. Textures stay as material references.
5. VRCFury patches the vendor lock callback. The patch skips the lock unless an SDK build is active (`Packages/com.vrcfury.vrcfury/Editor-Common/Hooks/DisablePluginsWhenNotUploadingHook.cs:20-64`). A build is active when the SDK build type is not None, or when the SDK control panel is building (`Packages/com.vrcfury.vrcfury/Editor-Common/Hooks/IsActuallyUploadingHook.cs:11-18`). The lock itself also returns early in Play mode (`ShaderOptimizer.cs:3219`). So Play mode outputs and test copies stay on the unlocked shader. The unlocked-shader stripper runs only in player builds, so those outputs render through the unlocked shader [INFERENCE].
6. A lock failure does not stop the build. The callback returns true in every case (`ShaderOptimizer.cs:3241-3243`). If an AMUSE output stays unlocked in a player build, the vendor stripper removes all variants of that unlocked shader. The slot then renders broken.
7. A live probe on 2026-09-23 ran the local SDK preprocess chain with an earlier vendor pin. It reported a locked canonical output. See `docs/superpowers/investigations/2026-09-23-poiyomi-generated-material-locking.md`. The scout missed finding 1 and first concluded that the lock skips AMUSE outputs. The NDMF source and the live probe refute that conclusion.

## 4. Live probe

The user approved this probe on 2026-11-19. It ran in the dev editor instance. The editor's data path matched this repository. The active build target was Windows.

Setup:

- Avatar A had a VRChat avatar descriptor and the AMUSE component. Its proof settings matched the existing fixtures: all sizes, and desktop anisotropy differences accepted. One renderer used an in-memory material on the bundled Poiyomi 9.3 shader. That material had the Fade render state and force-opaque on, so AMUSE could convert it. A second renderer used a plain in-memory material on the same shader.
- Avatar B had a descriptor and no AMUSE component. Its root Animator held an in-memory controller. One clip swapped the renderer's material to a material that nothing else references.
- The AMUSE consent presenter was replaced for the probe. It would have declined and recorded any request. It received no request.
- The vendor lock notice was suppressed for the probe. The probe restored the vendor setting afterwards.
- The probe called the installed SDK preprocess chain once per avatar. The NDMF temporary asset root was pinned, as the existing production-order fixtures do.

Run 1 left the SDK build type at None. The chain accepted both avatars. NDMF saved every material into its build container. AMUSE converted the Fade slot into its canonical opaque clone. VRCFury logged that it blocked the vendor lock, because no upload was active. Every material kept the unlocked shader.

Run 2 set the SDK build type to Test and restored None afterwards. A Build and Test uses that value [INFERENCE]. The chain accepted both avatars in about ten seconds. Results:

|Material|Asset path after the chain|Shader after the chain|
|---|---|---|
|AMUSE canonical opaque clone|NDMF build container|Locked, own cached program|
|Plain in-memory material|NDMF build container|Locked, shared cached program|
|Material swap base on avatar B|NDMF build container|Locked, shared cached program|
|Material that only a clip references|NDMF build container|Locked, shared cached program|
|Source material that AMUSE replaced|NDMF build container|Unlocked, no longer referenced|

The locked materials carried the vendor's original-shader tag. The AMUSE clone got a different cached program from the plain materials. Its render state differs, so its baked literals differ.

Probe notes:

- The first run blocked on a vendor dialog. The user closed it. The tool bridge then repeated the timed-out request several times. Each repeat destroyed its own objects.
- The probe wrote NDMF build containers, VRCFury build logs, and two locked programs in the vendor lock cache. A cleanup step deleted all of them through the asset database. A file listing before and after the probe matched. The vendor settings file was restored to its earlier empty state. The AMUSE consent seam was restored to null.

## 5. Consequences for atlasing

On 2026-11-19, the vendor lock relocks AMUSE outputs during an SDK build. That holds when the SDK build type is set. A Build and Test or an Upload sets it [INFERENCE]. An atlas output for a Poiyomi use, including a slot from the transient unlock window, gets locked after AMUSE runs. The shipped program then contains the atlas transforms and selectors as literals.

Each distinct output configuration yields its own locked program. The lock names cached programs by content. The probe showed two programs for two configurations. Shared output configuration across a page set limits that growth. This is consistent with the merge-ready goal.

The lock writes generated shaders into the project's lock cache during the build. That behavior already applies to AMUSE outputs in 0.1. Atlasing does not add a new vendor call.

## 6. Drift found

On 2026-09-23 a decision required AMUSE to lock generated Poiyomi outputs in Play mode. Current product source contains no vendor lock call. `TransientUnlockWindowClose` states that no build path calls the vendor. This record does not resolve that difference. It belongs to the 0.1 lock work, not to atlasing.

## 7. Remaining checks

- A probe through the SDK control panel. The probe set the build type directly. It did not build a bundle or run the stripper.
- Whether a Unity material copy keeps the vendor override tags for renamed animated properties. This affects only outputs that carry animated properties.
- A slot from the transient unlock window. The probe used an unlocked source material.
