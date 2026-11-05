# Development setup

AMUSE depends on NDMF. NDMF is distributed through a VPM package repository, not through the Unity registry. VPM stores package repositories in a machine-global configuration, not in this repository. Thus, a fresh clone cannot resolve NDMF until you register that repository once on the machine. The steps below give the full path from clone to Unity.

## Prerequisites

- **.NET 8 SDK**: the prerequisite that the [VPM CLI documentation](https://vcc.docs.vrchat.com/vpm/cli/)
  requires.
- **VRChat VPM CLI**: install it once if it is absent:

  ```
  dotnet tool install --global vrchat.vpm.cli
  ```

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

That URL is the repository endpoint published by the NDMF and Modular Avatar maintainer in the [official Modular Avatar installation documentation](https://modular-avatar.nadena.dev/docs/intro). The listing at that URL identifies itself as `dev.nadena.vpm` / `bd_`. The vendor test environment below registers more repositories, one per vendor source of the locked set. Run `vpm add repo` only when the check above shows that the repository is missing. The check, not repeated addition, makes this step safely repeatable.

This step is machine-global. This repository does not store it, and it does not travel with a clone. Therefore, these instructions state this step instead of assuming it.

## Register the vendor repositories (once per machine, for the test suites)

The test suites run against the installed vendor packages. The `locked` block of `Packages/vpm-manifest.json` is the canonical environment. A fresh clone that wants the full suite registers one repository per vendor source. The VRChat SDK packages come from the official VRChat curated repository, which VCC registers by itself. Register each other repository only when it is missing:

| Repository listing | Packages it supplies in the locked set |
| --- | --- |
| `https://poiyomi.github.io/vpm/index.json` | Poiyomi Toon, Thry Editor |
| `https://lilxyzw.github.io/vpm-repos/vpm.json` | lilToon |
| `https://vpm.anatawa12.com/vpm.json` | Avatar Optimizer |
| `https://d4rkc0d3r.github.io/vpm-repos/main.json` | d4rkAvatarOptimizer |
| `https://vcc.vrcfury.com/` | VRCFury |
| `https://redsim.github.io/vpmlisting/index.json` | Red Sim Light Volumes |
| `https://vpm.pimaker.at/index.json` | LTCGI |

These repositories supply third-party vendor packages for local testing. Register only the ones your work needs. The suite treats an absent optional vendor as a recorded fact and a named skip, never as a failure.

## Restore VPM packages (once per clone)

```
vpm resolve project .
```

Confirm the restore by its postcondition, not by its exit code. In an observed test, `vpm resolve project` exited `0` when it could not resolve a package. It logged `Could not resolve package ...` instead. After a successful restore, `Packages/nadena.dev.ndmf/package.json` reports name `nadena.dev.ndmf` and version in the supported range.

The restore leaves the working tree clean. `Packages/.gitignore` already contains the ignore rules that VPM expects. Thus, a fresh clone stays clean through the restore and does not show a modified tracked file.

## Open Unity

Open the project root in Unity 2022.3.22f1. At this point, NDMF is already resolved. Thus, the VPM resolver has nothing to fetch when Unity loads. The VRChat SDK supplies the Harmony and immutable-collection assemblies that NDMF needs, so no extra step is required.

## Tests

Tests run through the Unity Test Runner, EditMode mode. Run a focused filter first, then the full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` assemblies. A filtered run that reports 0 tests is a failure.

### The vendor environment checks

A suite fixture runs once before the first test of the product assembly. It fails the suite start when a locked vendor version mismatches its installed version, with both versions named. It records an absent optional vendor and an accepted patch drift inside the locked Thry line in one dated console line, never as a failure. A test whose own vendor is absent skips with the missing package and its locked version in the reason.

After the last test, the product assembly prints one dated run record block. The block records the counts, the durations, the opt-in gates that were set, the gated fixtures the run excluded, the per-test skip reasons, the effective vendor environment, the observable scripting defines, and the changed setting lines. The record observes vendor writes and never prevents them.

### Vendor side effects and licenses

The installed vendors act on the project and on the editor. The suites record these effects. They never prevent them, and no fixture asserts against a vendor-written setting.

- VRCFury holds a load hook that writes editor and quality settings when the editor loads. The hook runs before the first test, so a run digest never sees its writes.
- The Thry editor keeps a lock cache under `Assets/_LockedShaderCache` and leaves install residue under `Assets`. The repository gitignore covers both.
- The lilToon asset postprocessor can log a console error around material creation. No test fails on that noise.
- VRCFury's license forbids redistribution and modification. The tests only reference installed packages. Commercial users obtain VRCFury from its official channel.

### Opt-in gates

Some fixtures write project-level vendor state. Each of them sits behind a named opt-in environment variable. An unset gate reports a named skip with its disclosure. Set a gate to 1 only in a disposable project, never in a project whose state matters.

- `AMUSE_DAO_INTEGRATION`: the d4rkAvatarOptimizer combined-sequence tests. They delete project-level d4rkAvatarOptimizer output.
- `AMUSE_REAL_THRY_LOCK`: the real Thry lock characterization. The lock writes vendor-owned cache data under `Assets/_LockedShaderCache` and calls the vendor usage counter over the network.
- `AMUSE_PRODUCTION_ORDER_DISPATCH`: the production-order fixtures that build through the SDK callback dispatcher. The dispatcher runs VRCFury, and VRCFury writes generated controllers under `Packages/com.vrcfury.temp` during the build.

### Duration

The full two-assembly EditMode run aims for about 30 minutes. The duration is a goal and never a gate. No stage, fixture, or report rule fails or skips on elapsed time. The import cost of the Poiyomi shaders is recorded data that never gates.

### Research source tripwire

`Alrauna.Amuse.Research.Tests.Editor` holds a source tripwire. The test scans the Editor source of the research package. It refuses names of mutating, importing, persisting, and transmitting APIs. The Tests directory of that package is out of scope, because tests must create and destroy objects. Two read-only AssetDatabase lookups are allowed. All other AssetDatabase members are refused. The synthetic snippets in that test are plain strings. The test never compiles or runs a snippet.

The checks are case-sensitive substring matches on raw source text. They match API names. They do not analyze behavior. The list is a small fixed set of representative names. An indirect, wrapped, or renamed call can pass unseen. A file stream opened with `FileMode.Open` and no explicit `FileAccess` passes unseen. The tripwire catches accidental misuse. It is not a sandbox and not a proof against hostile code.

## Capability-dependent capture tests

The alpha capture gate needs exact Sample support for a texture's graphics format. Graphics backends report that support differently:

- The RGB24 test covers the one admitted exemption. The exemption is only from source-format Sample support, and other capture gates still apply. The test asserts alpha exactly 255 across the full mip chain.
- The ARGB32 test follows the exact sampling gate on the fixture format. Without exact support, it asserts the real capture refusal with no evidence. With exact support, it asserts 255, which passes the opaque predicate, and 128 and 254, which do not.

Thus, a capability observation from one machine does not describe another machine's test results.
