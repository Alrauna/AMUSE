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

AMUSE depends on NDMF 1.14.4, which is distributed through a VPM package repository rather
than the Unity registry. VPM stores package repositories in a machine-global configuration,
not in this repository, so a fresh clone cannot resolve NDMF until that repository has been
registered once on the machine. The steps below are the whole path from clone to Unity.

### Prerequisites

- **.NET 8 SDK** — the prerequisite the [VPM CLI documentation](https://vcc.docs.vrchat.com/vpm/cli/)
  requires.
- **VRChat VPM CLI** — install once if absent:

  ```
  dotnet tool install --global vrchat.vpm.cli
  ```

- **PowerShell 7 (`pwsh`) on `PATH`** — for the NDMF standalone bootstrap below.

This setup uses the official .NET-based VPM CLI and introduces no AMUSE-specific machine-path
or OS assumptions. VRChat documents a macOS setup, while its current documentation describes
Linux support as untested. AMUSE therefore does not claim stronger platform support for the
VPM CLI than VRChat itself.

### Register the NDMF package repository (once per machine)

NDMF is published in the `bd_` VPM repository. Check whether it is already registered:

```
vpm list repos
```

If the listing does not include `dev.nadena.vpm`, add it:

```
vpm add repo https://vpm.nadena.dev/vpm.json
```

That URL is the repository endpoint published by the NDMF and Modular Avatar maintainer in
the [official Modular Avatar installation documentation](https://modular-avatar.nadena.dev/docs/intro);
the listing served there identifies itself as `dev.nadena.vpm` / `bd_` and carries
`nadena.dev.ndmf` 1.14.4. It is the only repository AMUSE asks you to trust. Run `vpm add repo`
only when the check above shows the repository is missing — the check, not a repeated add, is
what makes this step safely repeatable.

This step is machine-global. It is not stored in this repository and does not travel with a
clone, which is why it is written out here rather than assumed.

### Restore VPM packages (once per clone)

```
vpm resolve project .
```

Confirm the restore by its postcondition, not by its exit code: `vpm resolve project` was
observed to exit `0` even when a package could not be resolved, logging
`Could not resolve package ...` instead. A successful restore leaves
`Packages/nadena.dev.ndmf/package.json` reporting name `nadena.dev.ndmf` and version `1.14.4`.

The restore leaves the working tree clean. `Packages/.gitignore` already carries the ignore
rules VPM expects, so a fresh clone stays clean through the restore rather than showing a
modified tracked file.

### Bootstrap the NDMF standalone dependencies

After restoring the VPM dependencies, run the temporary NDMF standalone bootstrap before opening Unity. The script targets PowerShell 7, so it needs `pwsh` on `PATH` — available for Windows, macOS, and Linux — and resolves every path relative to its own location:

```powershell
pwsh -NoProfile -File ./Tools/Bootstrap-NdmfStandalone.ps1
```

NDMF 1.14.4 packages standalone dependency assemblies under `Dependencies~`, which Unity intentionally ignores. The bootstrap verifies the resolved package version and payload, then copies those files into an ignored, Unity-importable `Dependencies` directory. It is safe to run repeatedly and requires no junction or symlink privileges. Remove this workaround once an upstream NDMF release exposes its standalone dependencies directly.

### Open Unity

Open the project root in Unity 2022.3.22f1. NDMF is already resolved and bootstrapped at this
point, so the VPM resolver has nothing to fetch on load.
