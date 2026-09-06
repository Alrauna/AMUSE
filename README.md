# AMUSE

AMUSE: Alrauna's Material Understanding & Simplification Engine is a non-destructive Unity material optimization project for VRChat avatars built on NDMF. It focuses on behavior-preserving analysis, planning, and transformation.

## Install

VCC and ALCOM both need two repositories, in this order:

1. **NDMF** (the framework AMUSE runs on): add `https://vpm.nadena.dev/vpm.json`.
2. **AMUSE**: add `https://alrauna.github.io/AMUSE/index.json`.

[![Add to VCC](https://img.shields.io/badge/Add%20to%20VCC-AMUSE-8A2BE2)](vcc://vpm/addRepo?url=https%3A%2F%2Falrauna.github.io%2FAMUSE%2Findex.json)

Then add the **AMUSE** package to your avatar project from the package manager. VCC shows a one-time warning for any community repository; that is expected.

## Use

Add the **AMUSE Avatar Optimizer** component to the avatar root and build as usual.

- The first build that meets an unverified shader or host version shows one consent dialog. Accepting treats that source with the nearest verified version's rules; declining is a total no-op for that build.
- Every refusal is visible in the NDMF report window, with a reason and a hint.
- PC only. Android and Quest builds get a named refusal.
- **Disable AMUSE** on the component makes the build treat it as absent: nothing runs, nothing is reported.
- Combining AMUSE with d4rkAvatarOptimizer is not validated in 0.1.0.

## What 0.1.0 does

Triangles of an AlphaTest or AlphaBlend material that are proven visually opaque move to an appended submesh with a generated canonical opaque material. Triangles that cannot be proven stay on the original material, unchanged. Tessellation, fur, and gem variants are not admitted; unsupported means a named refusal with the renderer or material untouched - never a silent guess.

Supported shaders: Poiyomi Toon 9.3.64 (including Two Pass) and lilToon 2.3.0-2.3.4 with their cutout, transparent, outline, and one-pass/two-pass variants.

## Development

See [docs/development-setup.md](docs/development-setup.md) for the path from a fresh clone to Unity, and [docs/architecture/vision.md](docs/architecture/vision.md) for the design direction. Tests run through the Unity Test Runner, EditMode mode.
