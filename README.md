# AMUSE

AMUSE: Alrauna's Material Understanding & Simplification Engine is a non-destructive Unity material optimization project for VRChat avatars built on NDMF. It uses material and shader understanding to reduce avatar rendering and resource cost. Each optimization states which differences it permits and which appearance and avatar controls it preserves.

## Install

VCC and ALCOM both need two repositories, in this order:

1. **NDMF** (the framework AMUSE runs on): add `https://vpm.nadena.dev/vpm.json`.
2. **AMUSE**: add `https://alrauna.github.io/AMUSE/index.json`.

[![Add to VCC](https://img.shields.io/badge/Add%20to%20VCC-AMUSE-8A2BE2)](https://alrauna.github.io/AMUSE/)

Then add the **AMUSE** package to your avatar project from the package manager. VCC shows a one-time warning for any community repository; that is expected.

## Use

Add the **AMUSE Avatar Optimizer** component to the avatar root and build as usual.

- The preset row under the header picks Safe, Normal, or Aggressive.
  Safe turns on both anisotropy acceptance settings. It still disables
  **Ignore Out-of-Range Material Slots** and **Allow Depth Test Change on
  Moved Triangles**, and it never flattens a mixed material. Normal
  and Aggressive enable all four settings. Normal flattens a nearly opaque
  material when fewer than 5 percent of its triangles are unproven.
  Aggressive uses 10 percent. The other preset values stay the same.
- The two **Accept Anisotropy Differences** toggles and the preset files
  that carry them live in the Alpha Separator Settings. See **Proof modes
  and acceptance** below.
- The first build that meets an unverified shader or host version shows one consent dialog. Accepting treats that source with the nearest verified version's rules; declining is a total no-op for that build.
- Every refusal is visible in the NDMF report window, with a reason and a hint.
- PC only. Android and Quest builds get a named refusal.
- **Disable AMUSE** on the component makes the build treat it as absent: nothing runs, nothing is reported.
- **Alpha Separator** is on by default. Turn it off to skip alpha separation for that avatar.
- Combining AMUSE with d4rkAvatarOptimizer is not validated in 0.1.0.

## What 0.1.0 does

Triangles of an AlphaTest or AlphaBlend material that are proven visually opaque move to an appended submesh with a generated canonical opaque material. Triangles that cannot be proven stay on the original material, unchanged. Tessellation, fur, and gem variants are not admitted; unsupported means a named refusal with the renderer or material untouched - never a silent guess.

Supported shaders: Poiyomi Toon 9.3.64 (including Two Pass) and lilToon 2.3.0-2.3.4 with their cutout, transparent, outline, and one-pass/two-pass variants.

## Proof modes and acceptance

Open **Settings**, turn on **Advanced Settings**, and open **Alpha
Separator Settings**. You find two toggles there: **Accept Desktop
Anisotropy Differences** and **Accept Mobile Anisotropy Differences**.
The **Active Sampling Proof** row shows the proof mode the current build
target reads. New components leave both toggles off. The Safe, Normal,
and Aggressive presets turn them on.

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

Custom preset files use schema version 3. To migrate a version 2 file, set
`"schemaVersion": 3` and add `"minimumOpaqueCoverageToPreservePercent"` to
its settings. To migrate a version 1 file, set `"schemaVersion": 3`, add
both anisotropy acceptance keys and the new preserve key, and keep the
version 2 acceptance values you want.

## Reading the console reports

One material on several slots prints one report line per slot. The repetition is deliberate, so a manual test can read the full list.

When a slot's refusal comes from a texture the route could not read, the slot's single line names the texture property, the channel, and the reason. A texture line on its own means the slot kept its proof and the line is a diagnostic only.

Every refusal line names its cause. A conversion refusal names the material that refused. A locked-material refusal says that AMUSE has not verified the original shader. A closure refusal names the renderer and the way the closure failed. A format refusal names the storage format and the texture. A Poiyomi alpha mask refusal names the alpha mask.

NDMF may print "changed outside of NDMF animator services; cloning a second time" between AMUSE pass lines. Those lines are NDMF re-cloning controllers that an upstream tool changed. They are not AMUSE failures, and AMUSE reads the re-cloned state, so its evidence stays current.

The last build status shows in every AMUSE Avatar Optimizer inspector in the editor session. It describes the session's most recent AMUSE build, and the text names the build path.

## Development

See [docs/development-setup.md](docs/development-setup.md) for the path from a fresh clone to Unity, and [docs/architecture/vision.md](docs/architecture/vision.md) for the design direction. Tests run through the Unity Test Runner, EditMode mode.
