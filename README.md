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

- The preset row under the header picks the shipped Safe preset. It
  holds the shipped defaults.
- The first build that meets an unverified shader or host version shows one consent dialog. Accepting treats that source with the nearest verified version's rules; declining is a total no-op for that build.
- Every refusal is visible in the NDMF report window, with a reason and a hint.
- PC only. Android and Quest builds get a named refusal.
- **Disable AMUSE** on the component makes the build treat it as absent: nothing runs, nothing is reported.
- **Alpha Separator** is on by default. Turn it off to skip alpha separation for that avatar.
- Combining AMUSE with d4rkAvatarOptimizer is not validated in 0.1.0.

## What 0.1.0 does

Triangles of an AlphaTest or AlphaBlend material that are proven visually opaque move to an appended submesh with a generated canonical opaque material. Triangles that cannot be proven stay on the original material, unchanged. Tessellation, fur, and gem variants are not admitted; unsupported means a named refusal with the renderer or material untouched - never a silent guess.

Supported shaders: Poiyomi Toon 9.3.64 (including Two Pass) and lilToon 2.3.0-2.3.4 with their cutout, transparent, outline, and one-pass/two-pass variants.

## Reading the console reports

One material on several slots prints one report line per slot. The repetition is deliberate, so a manual test can read the full list.

When a slot's refusal comes from a texture the route could not read, the slot's single line names the texture property, the channel, and the reason. A texture line on its own means the slot kept its proof and the line is a diagnostic only.

NDMF may print "changed outside of NDMF animator services; cloning a second time" between AMUSE pass lines. Those lines are NDMF re-cloning controllers that an upstream tool changed. They are not AMUSE failures, and AMUSE reads the re-cloned state, so its evidence stays current.

The last build status shows in every AMUSE Avatar Optimizer inspector in the editor session. It describes the session's most recent AMUSE build, and the text names the build path.

## Development

See [docs/development-setup.md](docs/development-setup.md) for the path from a fresh clone to Unity, and [docs/architecture/vision.md](docs/architecture/vision.md) for the design direction. Tests run through the Unity Test Runner, EditMode mode.
