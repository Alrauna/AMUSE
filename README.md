# AMUSE

**AMUSE: Alrauna's Material Understanding & Simplification Engine** is a Unity/NDMF material optimization project. It focuses on behavior-preserving analysis, planning, and transformation.

## Vision

AMUSE aims to understand how an avatar uses materials, textures, geometry, and rendering state. It combines that evidence into a deterministic optimization plan. It applies only transformations that are proven safe for every supported state. Longer-term directions include shader-semantic adapters, state and animation analysis, texture and atlas planning, material normalization and combining, and alpha/overdraw optimization. Unsupported or ambiguous behavior stays unchanged.

See [docs/architecture/vision.md](docs/architecture/vision.md) for the architectural direction and safety model.

## Current implementation

AMUSE is in early development. The current package provides a small, Editor-only analysis foundation:

- exact triangle UV and alpha classification for supported texture semantics;
- conservative `ProvenOpaque`, `ProvenTransparent`, and `Unknown` outcomes;
- deterministic mesh-separation planning from triangle classifications;
- synthetic reference-fixture infrastructure and focused EditMode tests; and
- an NDMF-compatible Unity package and development project.

## Not implemented yet

AMUSE does not yet transform meshes or materials. It does not trace animation or material swaps, understand arbitrary shader behavior, execute an NDMF optimization pass, or provide user-facing optimization controls. The current alpha subsystem is one input to the broader engine. It does not define the engine's scope.

## Development setup

AMUSE depends on NDMF 1.14.4. NDMF is distributed through a VPM package repository, not through the Unity registry. VPM stores package repositories in a machine-global configuration, not in this repository. Thus, a fresh clone cannot resolve NDMF until you register that repository once on the machine. The steps below give the full path from clone to Unity.

### Prerequisites

- **.NET 8 SDK**: the prerequisite that the [VPM CLI documentation](https://vcc.docs.vrchat.com/vpm/cli/)
  requires.
- **VRChat VPM CLI**: install it once if it is absent:

  ```
  dotnet tool install --global vrchat.vpm.cli
  ```

- **PowerShell 7 (`pwsh`) on `PATH`**: use it for the NDMF standalone bootstrap below.

This setup uses the official .NET-based VPM CLI. It does not introduce AMUSE-specific machine-path or OS assumptions. VRChat documents a macOS setup, but its current documentation describes Linux support as untested. Thus, AMUSE does not claim stronger platform support for the VPM CLI than VRChat claims.

### Register the NDMF package repository (once per machine)

NDMF is published in the `bd_` VPM repository. Check whether it is already registered:

```
vpm list repos
```

If the listing does not include `dev.nadena.vpm`, add it:

```
vpm add repo https://vpm.nadena.dev/vpm.json
```

That URL is the repository endpoint published by the NDMF and Modular Avatar maintainer in the [official Modular Avatar installation documentation](https://modular-avatar.nadena.dev/docs/intro). The listing at that URL identifies itself as `dev.nadena.vpm` / `bd_`. It includes `nadena.dev.ndmf` 1.14.4. It is the only repository that AMUSE asks you to trust. Run `vpm add repo` only when the check above shows that the repository is missing. The check, not repeated addition, makes this step safely repeatable.

This step is machine-global. This repository does not store it, and it does not travel with a clone. Therefore, these instructions state this step instead of assuming it.

### Restore VPM packages (once per clone)

```
vpm resolve project .
```

Confirm the restore by its postcondition, not by its exit code. In an observed test, `vpm resolve project` exited `0` when it could not resolve a package. It logged `Could not resolve package ...` instead. After a successful restore, `Packages/nadena.dev.ndmf/package.json` reports name `nadena.dev.ndmf` and version `1.14.4`.

The restore leaves the working tree clean. `Packages/.gitignore` already contains the ignore rules that VPM expects. Thus, a fresh clone stays clean through the restore and does not show a modified tracked file.

### Bootstrap the NDMF standalone dependencies

After you restore the VPM dependencies, run the temporary NDMF standalone bootstrap before you open Unity. The script targets PowerShell 7, so it needs `pwsh` on `PATH`. PowerShell 7 is available for Windows, macOS, and Linux. The script resolves every path relative to its own location:

```powershell
pwsh -NoProfile -File ./Tools/Bootstrap-NdmfStandalone.ps1
```

NDMF 1.14.4 packages standalone dependency assemblies under `Dependencies~`, which Unity intentionally ignores. The bootstrap verifies the resolved package version and payload. It then copies those files into an ignored, Unity-importable `Dependencies` directory. You can safely run it repeatedly, and it requires no junction or symlink privileges. Remove this workaround when an upstream NDMF release exposes its standalone dependencies directly.

### Open Unity

Open the project root in Unity 2022.3.22f1. At this point, NDMF is already resolved and bootstrapped. Thus, the VPM resolver has nothing to fetch when Unity loads.
