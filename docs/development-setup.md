# Development setup

AMUSE depends on NDMF. NDMF is distributed through a VPM package repository, not through the Unity registry. VPM stores package repositories in a machine-global configuration, not in this repository. Thus, a fresh clone cannot resolve NDMF until you register that repository once on the machine. The steps below give the full path from clone to Unity.

## Prerequisites

- **.NET 8 SDK**: the prerequisite that the [VPM CLI documentation](https://vcc.docs.vrchat.com/vpm/cli/)
  requires.
- **VRChat VPM CLI**: install it once if it is absent:

  ```
  dotnet tool install --global vrchat.vpm.cli
  ```

- **PowerShell 7 (`pwsh`) on `PATH`**: use it for the NDMF standalone bootstrap below.

This setup uses the official .NET-based VPM CLI. It does not introduce AMUSE-specific machine-path or OS assumptions. VRChat documents a macOS setup, but its current documentation describes Linux support as untested. Thus, AMUSE does not claim stronger platform support for the VPM CLI than VRChat claims.

## Register the NDMF package repository (once per machine)

NDMF is published in the `bd_` VPM repository. Check whether it is already registered:

```
vpm list repos
```

If the listing does not include `dev.nadena.vpm`, add it:

```
vpm add repo https://vpm.nadena.dev/vpm.json
```

That URL is the repository endpoint published by the NDMF and Modular Avatar maintainer in the [official Modular Avatar installation documentation](https://modular-avatar.nadena.dev/docs/intro). The listing at that URL identifies itself as `dev.nadena.vpm` / `bd_`. It is the only repository that AMUSE asks you to trust. Run `vpm add repo` only when the check above shows that the repository is missing. The check, not repeated addition, makes this step safely repeatable.

This step is machine-global. This repository does not store it, and it does not travel with a clone. Therefore, these instructions state this step instead of assuming it.

## Restore VPM packages (once per clone)

```
vpm resolve project .
```

Confirm the restore by its postcondition, not by its exit code. In an observed test, `vpm resolve project` exited `0` when it could not resolve a package. It logged `Could not resolve package ...` instead. After a successful restore, `Packages/nadena.dev.ndmf/package.json` reports name `nadena.dev.ndmf` and version in the supported range.

The restore leaves the working tree clean. `Packages/.gitignore` already contains the ignore rules that VPM expects. Thus, a fresh clone stays clean through the restore and does not show a modified tracked file.

## Bootstrap the NDMF standalone dependencies

After you restore the VPM dependencies, run the NDMF standalone bootstrap before you open Unity. The script targets PowerShell 7, so it needs `pwsh` on `PATH`. PowerShell 7 is available for Windows, macOS, and Linux. The script resolves every path relative to its own location:

```powershell
pwsh -NoProfile -File ./Tools/Bootstrap-NdmfStandalone.ps1
```

NDMF packages standalone dependency assemblies under `Dependencies~`, which Unity intentionally ignores. The bootstrap verifies the resolved package version and payload. It then copies those files into an ignored, Unity-importable `Dependencies` directory. You can safely run it repeatedly, and it requires no junction or symlink privileges. Remove this workaround when an upstream NDMF release exposes its standalone dependencies directly.

## Open Unity

Open the project root in Unity 2022.3.22f1. At this point, NDMF is already resolved and bootstrapped. Thus, the VPM resolver has nothing to fetch when Unity loads.

## Tests

Tests run through the Unity Test Runner, EditMode mode. Run a focused filter first, then the full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` assemblies. A filtered run that reports 0 tests is a failure.
