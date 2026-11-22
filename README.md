# AMUSE

AMUSE: Alrauna's Material Understanding & Simplification Engine is a non-destructive Unity material optimization project for VRChat avatars built on NDMF. It uses material and shader understanding to reduce avatar rendering and resource cost. Each optimization states which differences it permits and which appearance and avatar controls it preserves.

## Install

VCC and ALCOM both need two repositories, in this order:

1. **NDMF** (the framework AMUSE runs on): add `https://vpm.nadena.dev/vpm.json`.
2. **AMUSE**: add `https://alrauna.github.io/AMUSE/index.json`.

[![Add to VCC](https://img.shields.io/badge/Add%20to%20VCC-AMUSE-8A2BE2)](https://alrauna.github.io/AMUSE/)

Then add the **AMUSE** package to your avatar project from the package manager. VCC shows a one-time warning for any community repository. That is expected.

## Use

Add the **AMUSE Avatar Optimizer** component to the avatar root and build as usual.

- The preset row under the header picks Safe, Normal, or Aggressive.
  Safe turns on both anisotropy acceptance settings. It keeps
  **Allow Out-of-Range Material Slots** and **Allow Depth Test Change on
  Moved Triangles** off, and it never flattens a mixed material. Normal
  and Aggressive enable all four settings. Normal flattens a nearly opaque
  material when fewer than 5 percent of its triangles are unproven.
  Aggressive uses 10 percent and sets Smallest Tested Mipmap to Mip 3.
  Cutout textures use Mip 2.
- The two **Accept Anisotropy Differences** toggles and the preset files
  that carry them live in the Alpha Separator Settings. See **Proof modes
  and acceptance** below.
- The first build that meets an unverified shader or host version shows one consent dialog. Accepting treats that source with the nearest verified version's rules. Declining is a total no-op for that build.
- Every refusal is visible in the NDMF report window, with a reason and a hint.
- PC only. Android and Quest builds get a named refusal.
- **Disable AMUSE** on the component makes the build treat it as absent: nothing runs, nothing is reported.
- **Alpha Separator** is on by default. Turn it off to skip alpha separation for that avatar.
- **Atlasing** is on by default. It moves the main color textures of supported materials onto shared atlas pages. Turn it off to skip atlasing for that avatar. See **Atlasing** below.
- Combining AMUSE with d4rkAvatarOptimizer is not validated in 0.1.0. Opt-in gated tests of this repository observe the combined sequence in a disposable project. They are observation, not validation.

## What 0.1.0 does

Triangles of an AlphaTest or AlphaBlend material that are proven visually opaque move to an appended submesh with a generated canonical opaque material. Triangles that cannot be proven stay on the original material, unchanged. Tessellation, fur, and gem variants are not admitted. Unsupported means a named refusal with the renderer or material untouched - never a silent guess.

Supported shaders: Poiyomi Toon 9.3.64 and the 9.3 shader that Poiyomi 10.x packages bundle, each including Two Pass. Supported lilToon versions are 2.3.0 through any version below 2.5. Those versions are approved without a consent prompt. The build asks once per build for versions outside that range. AMUSE rebuilds a locked Poiyomi material from its generated locked shader when its recorded original is a 9.3.64 release shader or one of the two bundled 9.3 rows. The bundled locked rows follow the same reconstruction contract as the release rows. A locked material that refuses keeps its original slot. Poiyomi and lilToon support cutout, transparent, outline, and one-pass/two-pass variants.

## Atlasing

Atlasing runs after alpha separation. A use is one material slot of one renderer. The pass moves the `_MainTex` region, the main color texture, of each supported use onto shared AMUSE pages. It rewrites UV0 on cloned meshes. It writes one shared material per page. Each page has mip maps and streaming mip maps. Each protected mip level is an exact copy from the source. Source meshes, materials, textures, and import settings stay unchanged.

Atlasing in 0.2.0 does not reduce draw calls or material slots. A page set is merge-ready. One material configuration serves every slot of the set. A later AMUSE version can merge the materials of a merge-ready set. The pages add texture memory. The source textures stay in the avatar, because AMUSE cannot prove that nothing else uses them.

Two slots share one page set when every value that their shader can read is equal. The values are compared by name, not by list position. The saved render queue counts only through the queue that stays in effect. A saved queue that equals the shader queue matches the shader default. A keyword that the shader does not declare does not count. A saved property of an unknown shape stops the key walk with an error.

On the lilToon 2.4.1 rows, the values of a feature that its switch turns off do not count, for example the rim values with the rim switch off. A feature stays counted when an animation or a property block can change its switch. A saved value of a name that the shader neither lists nor declares as a uniform does not count. The names that a VRChat fallback shader can show always count. A `toonstandard` fallback tag keeps every value counted. Poiyomi rows and other lilToon versions use only the rules that hold for every shader.

Atlasing runs only for Windows builds. The supported cases:

- Poiyomi 9.3 main color route. The member texture is `_MainTex` on UV0. The four attested Poiyomi 9.3 rows support it: the 9.3.64 release shader, its Two Pass form, and the two 9.3 rows that Poiyomi 10.x packages bundle. An enabled local feature keyword must be on the allowed list of seven keywords. The required property values must hold, for example the UV selector on UV0, stochastic sampling off, and the ambient occlusion blocker off. The tiling and offset of `_MainTex` must be identity. A fresh material assigns the five neutral textures of the route: `_ToonRamp`, `_SkinLUT`, `_ClothDFG`, `_DistortionFlowTexture`, and `_DistortionFlowTexture1`. Any other assigned texture refuses the use.
- lilToon main color route. The member texture is `_MainTex` on UV0. The ten exactly attested lilToon rows of versions 2.3.0 through 2.4.1 support it: the opaque, cutout, transparent, outline, one pass, and two pass shaders. Every feature switch must be zero. A use refuses when a feature reads or moves UV0: backface UV shift, parallax, the second and third main textures, glitter, AudioLink, dissolve, UV tile discard on UV0, or ID masks from UV0. The tiling and offset of `_MainTex` must be identity. On versions below 2.4.1 the neutral list is empty, so any other assigned texture refuses the use.
- lilToon 2.4.1 companion textures. On lilToon 2.4.1 the route also moves the textures that lilToon reads at the main UV0 coordinate. Each one goes onto a companion page with the main page layout at its own size: the alpha mask, the normal map, the shadow border mask, the matcap blend mask, the rim color and rim shade textures, the emission map on UV0, the reflection color texture, the main color adjust mask, the second main texture and its blend mask, and on outline rows the outline texture and the outline width mask. A companion size must be the main size divided by one power of two. Its tiling and offset must be identity. A texture behind an off switch does not refuse the use, and its values do not count. A route texture behind an off switch still counts when a VRChat fallback shader can show it, for example the normal map or the emission map, so different textures there keep the slots apart. A texture on another UV channel stays bound. `_BaseMap` and `_BaseColorMap` get the main page, because no lilToon code reads them. The matcap textures stay bound. The second main texture may be on, but not as a decal, not turned, not animated, and without dissolve. The third main texture still refuses.
- Exact rows only. Version consent does not extend to atlasing. An approved lilToon version that is not an exact row refuses. A lilToon Multi material refuses with its own name. A locked material refuses.

Every refused use prints one console line. The line names the slot and the renderer. The slot keeps the bindings it had. A slot that alpha separation moved keeps that result. Three causes stop the whole pass for the avatar:

- The build target is not Windows.
- The stored maximum page size is outside 1 to 8192.
- The stored protected mip level count is outside 1 to 14.

The other causes refuse one slot:

- The renderer is not a mesh or skinned mesh renderer, has no mesh, or the slot has no material.
- An animation changes the mesh, the slot count, the slot material, or a route property.
- A property block targets the member texture, its transform, or a condition property.
- The shader matches no attested row, the material is locked, or the attested row has no route table.
- A feature keyword or a required value is outside the route.
- The main texture is missing. The main texture or a companion texture is not 2D, not an asset on disk, or in a format or wrap mode the page cannot copy. A companion size is not the main size divided by one power of two.
- On an outline row, a companion reads the main texture sampler, and the outline texture sampler differs from the main texture sampler.
- The tiling or offset is not identity.
- The mesh has no UV0, or a UV value of the use lies outside the unit square.
- The source mip chain is too short or not block aligned at a protected level.
- The region does not fit an empty page of the maximum size.
- A budget or a failed source read refuses the whole group.
- All slots of a group use the same source textures. A page would only copy them, so the group builds no page.
- The slot shares its mesh part with another slot, the part is not triangles, or the part has no triangles.

The budget has three values, set on 2026-11-21 from measurements of production code on public fixtures:

- Group factor. The pages of one group may hold at most 25 percent more bytes than the source textures of the group. The factor is 5/4. Borders and packing gaps are the only cause of a ratio above 1. The public fixtures measure at most 1.061. A small texture with anisotropic filtering gets a border of 16 texels on each side. So a group of small textures, for example 8 by 8 texels, refuses.
- Avatar limit. The pages of one avatar may hold at most 524,288,000 bytes in total. This is 500 MiB. It is the VRChat SDK limit for the uncompressed size of a PC avatar bundle.
- Peak limit. Building one group may hold at most 4,294,967,296 bytes of editor memory at one time. This is 4 GiB. A full 8192 page with up to five distinct 8192 by 8192 RGBA32 sources stays inside the limit. A sixth such source refuses the group.

A budget refusal line states its value and its limit in megabytes and in bytes.

The build console shows one summary entry per avatar. It counts the atlased material slots out of all slots. It counts the slots that did not atlas per cause. Each page set entry lists its pages and its slots. Each refused slot entry names the slot, the renderer, and the material. Its description ends with the exact value that stopped the slot, for example a property and its value. Click the renderer, the material, or the texture under an entry to select it.

The **Atlasing** switch sits below the **Alpha Separator** block on the component. It is on by default. Turn it off to skip atlasing for that avatar. Open **Settings**, turn on **Advanced Settings**, and open **Atlas Settings** for the numeric settings. **Maximum Page Size** defaults to 8192. Its range is 1 to 8192. **Protected Mip Levels** defaults to 1. Its range is 1 to 14. A stored value outside its range shows a warning in the inspector. The build refuses it. Presets do not read or write the atlas settings.

Each placement on a page gets a border of copied texels. The border keeps anisotropic sampling away from neighboring regions. The border is sized for an anisotropy ratio of 16. Ratio 16 is the highest anisotropic level in Unity. The anisotropy part of the border is 1 texel at anisotropy level 0 and 16 texels at any other level. The reach measurement of 2026-11-20 gave 1.326 texels at anisotropy level 0 and 14.564 texels at anisotropy level 16. Each value is a Euclidean distance in base level texels. This evidence comes from one GPU. That one measurement is the stated limit of the border guarantee.

The **Show Material Transformations** view has an **Atlasing** view below the **Alpha Separator** view. The Alpha Separator view excludes atlasing, and its label states this. The Atlasing view is described in [Show Material Transformations](#show-material-transformations). The build console lists the final atlas results.

## Proof modes and acceptance

Open **Settings**, turn on **Advanced Settings**, and open **Alpha
Separator Settings**. The Transparent submenu holds the proof settings
for transparent materials. The Cutout submenu holds the matching
settings for cutout materials. You find two toggles there: **Accept
Desktop Anisotropy Differences** and **Accept Mobile Anisotropy
Differences**. The **Active Sampling Proof** row shows the proof mode
the current build target reads. New components start with both toggles
on. The Safe, Normal, and Aggressive presets keep them on.

With a toggle off, the proof is conservative. The proof keeps anisotropic
evidence, so some triangles may not prove. With a toggle on, the proof is
accepted. AMUSE proves opacity without anisotropic footprints. VRChat can
filter textures anisotropically, so converted parts can lose transparency
near texture edges or at oblique views.

Acceptance changes the proof scope. It does not change how Unity filters
your textures at runtime.

The Android and iOS build targets select the mobile acceptance setting.
AMUSE texture capture does not support these targets. The setting does
not enable mobile conversion, and an Android build keeps its named
refusal. Any other build target has no sampling-policy selection. The
build reports the missing policy and keeps the conservative output.

The last build status and the build report name the selected proof mode.
They name it even when no triangle moves.

Custom preset files use schema version 5. A schema 4 file refuses with
the schema version message. To migrate a version 4 file, set
`"schemaVersion": 5` and add `"cutoutSeparator": true` and
`"transparentSeparator": true` to its features. To migrate a version 3
file, set `"schemaVersion": 4` and add the four cutout keys to its
settings: `cutoutPreserveTransparencyMaxMipLevel`,
`cutoutPreserveTransparencyMinTextureSize`,
`cutoutMinimumOpaqueCoveragePercent`, and
`cutoutMinimumOpaqueCoverageToPreservePercent`. Then make it version 5.
To migrate a version 2 file, first make it version 3: set
`"schemaVersion": 3` and add `"minimumOpaqueCoverageToPreservePercent"`.
Then make it version 4. Then make it version 5. To migrate a version 1
file, first make it version 2: set `"schemaVersion": 2` and add
`"acceptDesktopAnisotropyDifferences"` and
`"acceptMobileAnisotropyDifferences"`. Then make it version 3. Then make
it version 4. Then make it version 5.

A brand-new component's first cutout edit in the session that adds it is
replaced once by the transparent values. Every later edit sticks.

### Show Material Transformations

Open the Show Material Transformations foldout on the AMUSE component to see what the Alpha Separator would do on this avatar. The nested Alpha Separator view runs the same proof on the avatar in the scene. Each material slot displays as two lines: the top line displays the target mesh renderer and source material side by side, and the bottom line reports the transformation status.
The view reads the scene state. Other tools can change the avatar later during a build, and some tools add animations the build must obey. This view cannot see those changes. A run in Play mode or an upload shows the true result. The first open measures textures on the GPU. It can take a moment. The Refresh button recomputes everything.
If a setting changes after a preview run starts, the preview rejects that run and hides its results. It shows the settings-changed message. Changing the setting back does not restore the rejected results. Select Refresh to compute new results.

The nested Atlasing view runs the atlas analysis of a build on the avatar in the scene. It shows a summary sentence and one box per page set. A box lists the pages and the renderer and material of each slot. The **S** button selects the materials of the set. The **Not atlased** list shows each slot that did not atlas with its cause. Point at a cause to read the full console text and the hint. The view computes when it opens, when an atlas setting changes, and on Refresh. A build can still differ, because other tools change the avatar first and a build drops a group when it cannot read a texture.

### Play mode results

Enter Play mode and AMUSE measures the avatar in the scene. The inspector then shows the accurate Play mode result instead of the preview. A banner marks that state.

Each result belongs to one avatar root. The key is the global identity of that root in the saved scene. During Play, the same root is matched by a live native reference, so a rename or a same-name copy during the build cannot swap results between avatars. Results live in the editor session and end with it.

A root without a proved identity never holds a result. An avatar in a scene that was never saved, or a copy created during Play, reads as new. Its key is empty, so nothing saves, loads, or clears for it.

The Refresh button is per root. It clears only the record of the avatar you refresh. Other avatars keep their own records.

## Reading the console reports

One material on several slots prints one report line per slot. The repetition is deliberate, so a manual test can read the full list.

When a slot's refusal comes from a texture the route could not read, the slot's single line names the texture property, the channel, and the reason. A texture line on its own means the slot kept its proof and the line is a diagnostic only.

Every refusal line names its cause. A conversion refusal names the material that refused. A locked-material refusal says why AMUSE did not rebuild the locked material. The cause can be a property block on a renderer or an animation that the locked shader reads differently. A closure refusal names the renderer and the way the closure failed. A format refusal names the storage format and the texture. A Poiyomi alpha mask refusal names the alpha mask.

The atlas pass prints one line per page set. The line names the shader row. It states that the set is merge-ready, the bytes the pages add, and the bytes of the sources that stay in the avatar. It also states that atlasing in 0.2.0 does not reduce draw calls or material slots.

A refused atlas use prints one line. The line names the material slot and the renderer. A budget refusal states its value and its limit in bytes.

NDMF may print "changed outside of NDMF animator services; cloning a second time" between AMUSE pass lines. Those lines are NDMF re-cloning controllers that an upstream tool changed. They are not AMUSE failures, and AMUSE reads the re-cloned state, so its evidence stays current.

The last build status shows in every AMUSE Avatar Optimizer inspector in the editor session. It describes the session's most recent AMUSE build, and the text names the build path.

## Development

See [docs/development-setup.md](docs/development-setup.md) for the path from a fresh clone to Unity, and [docs/architecture/vision.md](docs/architecture/vision.md) for the design direction. Tests run through the Unity Test Runner, EditMode mode.
