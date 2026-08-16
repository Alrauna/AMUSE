# AMUSE

**AMUSE — Alrauna's Material Understanding & Simplification Engine** is a Unity/NDMF material optimization project focused on behavior-preserving analysis, planning, and transformation.

## Vision

AMUSE aims to understand how an avatar uses materials, textures, geometry, and rendering state; combine that evidence into a deterministic optimization plan; and apply only transformations proven safe for every supported state. Longer-term directions include shader-semantic adapters, state and animation analysis, texture and atlas planning, material normalization and combining, and alpha/overdraw optimization. Unsupported or ambiguous behavior stays unchanged.

See [docs/architecture/vision.md](docs/architecture/vision.md) for the architectural direction and safety model.

## Current implementation

AMUSE is in early development. The current package provides a small, Editor-only analysis foundation:

- exact triangle UV and alpha classification for supported texture semantics;
- conservative `ProvenOpaque`, `ProvenTransparent`, and `Unknown` outcomes;
- deterministic mesh-separation planning from triangle classifications;
- synthetic reference-fixture infrastructure and focused EditMode tests; and
- an NDMF-compatible Unity package and development project.

## Not implemented yet

AMUSE does not yet transform meshes or materials, trace animation or material swaps, understand arbitrary shader behavior, execute an NDMF optimization pass, or expose user-facing optimization controls. The current alpha subsystem is one input to the broader engine, not the definition of its scope.

## Development setup

After restoring the VPM dependencies, run the temporary NDMF standalone bootstrap before opening Unity:

```powershell
pwsh -NoProfile -File ./Tools/Bootstrap-NdmfStandalone.ps1
```

NDMF 1.14.4 packages standalone dependency assemblies under `Dependencies~`, which Unity intentionally ignores. The bootstrap verifies the resolved package version and payload, then copies those files into an ignored, Unity-importable `Dependencies` directory. It is safe to run repeatedly and requires no junction or symlink privileges. Remove this workaround once an upstream NDMF release exposes its standalone dependencies directly.
