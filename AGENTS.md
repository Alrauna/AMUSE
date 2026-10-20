# Repository Guidelines

## Project Overview

AMUSE (Alrauna's Material Understanding & Simplification Engine) is a nondestructive build-time optimizer for VRChat avatars. It ships as a Unity package, `Packages/com.alrauna.amuse`, hosted by NDMF. During an avatar build it classifies mesh triangles as proven-opaque, must-stay-transparent, or unknown. Proven-opaque triangles may move to AMUSE-generated canonical opaque materials. Unproven triangles always keep their original material.

The avatar root carries the `AmuseAvatarOptimizer` component. Presets Safe, Normal, and Aggressive set aggressiveness. The first build that meets an unverified shader or host version shows one consent dialog. Accepting treats that source with the nearest verified version rules. Declining is a total no-op for that build. PC builds only. Android and Quest builds get a named refusal. Supported shader sources: Poiyomi Toon 9.3.64 including Two Pass, and lilToon 2.3.0 through versions below 2.5 (approved range), exact rows through 2.4.0. See `README.md` for product behavior.

A second package, `Packages/com.alrauna.amuse.research`, holds read-only research tooling (Census collection and calibration). It must never ship in the product or the VPM listing.

Public fixtures must be minimal, deterministic, redistributable, and legally publishable. Private avatars, vendor assets, consent and identity data, and raw Census observations may not be public.

## Architecture & Data Flow

One NDMF plugin, `AmusePlatformFinishPlugin` (`Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`), registers five passes in `BuildPhase.PlatformFinish`:

1. `AmuseStructuralGraphCheck` runs extension-free on real authored clips before animator virtualization, stores the enumerated controller graph and its structural refusal in state, and reports nothing itself.
2. `AmuseAnimatorBindingsCapture` retains `IPlatformAnimatorBindings` from the active `AnimatorServicesContext` into build state before the extension deactivates.
3. `AmusePlatformFinishPass` runs extension-free, so NDMF commits the animator graph first. It gates on `HostLifecycleCapability.Evaluate` (versions, platform, build path, build services). Per renderer it captures closed animation and material evidence, resolves per-slot runtime states, classifies triangle alpha exactly, plans mesh separation, and retains `PreparedAlphaSeparation`.
4. `AlphaSeparationApply` revalidates every candidate against live state, finalizes, sweeps unreferenced AMUSE-owned clones, and performs the single mutation: animation curves, then `sharedMesh`, then `sharedMaterials`.
5. `TransientUnlockWindowClose` runs extension-free after the animator scope closes, reverts every open pair to its locked original, re-asserts apply's recorded writes, and destroys the unlocked clone last.

Module layering and dependency direction:

- `Build/` owns the NDMF lifecycle and mutation. It calls every other module.
- `Host/` is the only module that touches live Unity objects. It captures immutable evidence: renderer snapshots, animation closure, and GPU alpha readback through an R8_UNorm blit (`UnityAlphaFieldEvidence.cs`). The Semantics conversion recipes are sanctioned exceptions that mutate only AMUSE-owned transients.
- `Analysis/` is the pure proof core: exact rational-interval triangle classification (`TriangleAlphaClassifier.cs`), exact UV envelopes (`ExactUvGeometry.cs`, `AffineUvTransform.cs`), runtime-state resolution, and mesh planning. It never mutates Unity state.
- `Semantics/` holds the shader frontends. `Poiyomi/` and `LilToon/` attest shader identity first (pinned GUIDs, package versions, SHA-256 canonical digests), then answer alpha semantics. `MaterialSemantics.cs` defines the shader-independent value algebra.
- `DryRun/` is the EditMode preview. It reruns the same proof pipeline over the scene avatar, strictly read-only, and emits display rows (`DryRunRowBuilder.cs`).
- `Runtime/` (assembly `Alrauna.Amuse.Runtime`, all platforms) holds the `AmuseAvatarOptimizer` component. It implements `INDMFEditorOnly`, so NDMF strips it from uploads.

Data flow: live avatar, then immutable captured evidence, then exact classification, then plan, then revalidation, then minimal Apply. Source assets are never mutated. Only the NDMF build copy and AMUSE-generated transients change. Never modify source meshes, materials, textures, import settings, animation assets, prefabs, or scenes to make AMUSE succeed. Treat Unity assets and `.meta` files as one unit. Never churn GUIDs.

Correctness policy: a false positive violates the declared transformation contract. A false negative is safer, but an unnecessary refusal remains a coverage defect. Unknown information invalidates only the conclusions that depend on it. Do not expand a local unsupported fact into renderer-wide or avatar-wide refusal unless the dependency requires that scope. Programming and invariant failures throw and block the build. Never blanket-catch them into "unsupported."

## Key Directories

|Path|Purpose|
|---|---|
|`Packages/com.alrauna.amuse/Editor/`|Production editor code. Two production assemblies total, this one plus `Runtime/`.|
|`Packages/com.alrauna.amuse/Editor/Build/`|NDMF plugin, five passes, lifecycle gate, consent, prepare and apply, reports|
|`Packages/com.alrauna.amuse/Editor/Host/`|Unity evidence capture. `Host/Shaders/` holds the alpha predicate shader.|
|`Packages/com.alrauna.amuse/Editor/Analysis/`|Pure classification and planning|
|`Packages/com.alrauna.amuse/Editor/Semantics/`|Value algebra plus `Poiyomi/` and `LilToon/` frontends|
|`Packages/com.alrauna.amuse/Editor/DryRun/`|Read-only EditMode preview of the proof pipeline|
|`Packages/com.alrauna.amuse/Runtime/`|`AmuseAvatarOptimizer` component and preset data types|
|`Packages/com.alrauna.amuse/Tests/Editor/`|Product EditMode tests. Folders mirror production.|
|`Packages/com.alrauna.amuse.research/`|Research package. Public source, never released.|
|`docs/superpowers/specs/`|Dated design specs, `YYYY-MM-DD-<topic>-design.md`|
|`docs/superpowers/plans/`|Dated implementation plans with RED/GREEN tasks|
|`docs/superpowers/investigations/`|Dated read-only characterization notes|
|`Tools/`|`Bootstrap-NdmfStandalone.ps1`|

## Development Commands

First-time setup, from `docs/development-setup.md`:

```bash
dotnet tool install --global vrchat.vpm.cli
vpm list repos                                    # check NDMF repo registration
vpm add repo https://vpm.nadena.dev/vpm.json      # only when missing
vpm resolve project .                             # restores Packages/nadena.dev.ndmf
pwsh -NoProfile -File ./Tools/Bootstrap-NdmfStandalone.ps1
# then open the project in Unity 2022.3.22f1
```

`vpm resolve` can exit 0 without resolving. Confirm `Packages/nadena.dev.ndmf/package.json` exists at version 1.14.4 afterward. The bootstrap script materializes NDMF `Dependencies~` into `Packages/nadena.dev.ndmf/Dependencies/`, pinned to NDMF 1.14.4, and verifies the file list and digests. It is idempotent.

- Tests run through the Unity Test Runner, EditMode mode. No CLI test script and no CI test job exists. In-repo practice: refresh Unity after new files, run a focused EditMode filter, then the full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` assemblies.
- Release: `.github/workflows/release.yml` (manual dispatch) zips the single package named by the repo variable `vars.PACKAGE_NAME`, excludes `Tests/`, and fails the release if test content re-enters. `build-listing.yml` publishes the VPM listing to GitHub Pages. `pr.yml` is a deliberate no-op branch-protection gate.
- No lint or formatter is configured. Use `git diff --check` for whitespace.

## Code Conventions & Common Patterns

- C# against Unity 2022.3 APIs. Editor-only except the runtime component. Assemblies: `Alrauna.Amuse.Editor`, `Alrauna.Amuse.Runtime`.
- File name equals type name. One public type per file. A few grouped files such as `MaterialSemantics.cs` are intentional exceptions. Production types are `internal` where possible. `Editor/AssemblyInfo.cs` grants `InternalsVisibleTo` to the test and research assemblies.
- Namespaces mirror folders: `Alrauna.Amuse.Editor.Build`, `.Host`, `.Analysis`, `.Semantics`, `.Semantics.LilToon`, `.Semantics.Poiyomi`, `.DryRun`; runtime namespace `Alrauna.Amuse.Runtime`.
- Closed refusal enums per scope: `HostLifecycleRefusal`, `AvatarAnimationRefusal`, `RendererAnalysisRefusal`, `AlphaSeparationSlotRefusal`, `PreparationRefusal`, `TextureCaptureRefusalReason`, `AlphaUnknownKind`. Unsupported means a named refusal value. `None` is not a refusal; recorders throw on it.
- Fail closed. Parsers reject unknown input. `TriangleAlphaOutcome.Unknown` never proves opacity. `MustRemainTransparent` is absorbing. Classification never exits early on Unknown. Degenerate triangles and missing UV0 classify as Unknown.
- Evidence discipline. Capture immutable records such as `CapturedAnimationEvidence` and `CapturedMaterialEvidence` close to their consumer. Immutable evidence never holds live Unity references. Live capabilities stay separate on `AmusePlatformFinishState`. Revalidate live state immediately before mutation.
- Attestation is pinned data. Shader GUIDs, exact names, package versions, and measured SHA-256 digests live in `LilToonSourceAttestation.cs`, `PoiyomiSourceAttestation.cs`, and sibling attestation files. Never re-derive digests from vendor repositories. Unattested materials answer all-Unknown.
- Test seams are production delegates, for example `VerifiedPoiyomiConversion`, `VerifiedLilToonConversion`, `VersionConsentPresenter`. Tests substitute only vendor attestation and run real production logic.
- XML doc comments sit on load-bearing rules and state why. They carry design-decision IDs that link to `docs/superpowers/specs/` records.

### Documentation and session rules

- Every English text a human reads uses simple english or ASD-STE100 Simplified Technical English: short active sentences, one idea per sentence, no semicolons, no contractions.
- Documents never contain machine paths, host names, user names, ports, GUIDs, instance names, or private identifiers. Use `<repo-root>`-relative paths. The rules bind scratch, plans, reports, and handoff notes.
- Date every status claim. Never write an unqualified `today` or `currently`. Records in `docs/superpowers/` speak for their date and never authorize new subsystems.
- Before finishing document work, sweep changed files for identifiers: at sign joined to hexadecimal, drive-letter paths, home-directory paths, four-digit ports, private asset names learned in the session. Every hit is a defect. Fix the hits, then state that the sweep ran.
- Characterize private avatars by role, never by name. Replace exact private counts with ranges. A sanitized record opens with a privacy note.
- Never stage, commit, amend, push, open or merge a PR, delete branches, rewrite history, publish, or change remotes or settings without explicit authorization. Treat existing changes as user-owned.
- Superpowers skills govern process (brainstorming, writing-plans, tdd, code-review, verification-before-completion). Ponytail governs implementation minimalism. Ponytail never overrides correctness or planning gates.

## Important Files

|File|Role|
|---|---|
|`Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`|NDMF entry point, the five passes, shared build state|
|`Packages/com.alrauna.amuse/Editor/Build/HostLifecycleCapability.cs`|Version, platform, build-path, and services gate|
|`Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`|Evidence-gated preparation, no avatar mutation|
|`Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`|Revalidation and the single mutation|
|`Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs`|Refusal and outcome reporting|
|`Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`|Exact per-triangle alpha proof|
|`Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`|Frontend selection by exact shader identity|
|`Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs`|Shader-independent value algebra|
|`Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`|Avatar-root component, `INDMFEditorOnly`|
|`Packages/com.alrauna.amuse/package.json`|Package identity and the NDMF dependency range|
|`Packages/manifest.json` and `Packages/vpm-manifest.json`|Unity and VPM dependencies|
|`Packages/.gitignore`|The whitelist that decides which embedded packages are tracked|
|`Tools/Bootstrap-NdmfStandalone.ps1`|NDMF standalone bootstrap, pinned to 1.14.4|
|`docs/development-setup.md`|Clone-to-Unity setup path|
|`docs/architecture/vision.md`|Long-term direction. Code does not overrule it.|
|`docs/superpowers/README.md`|Interpretation rules for dated records|
|`.omp/RULES.md`|Sticky session rules: no absolute paths, no private identifiers, simple english|

## Runtime/Tooling Preferences

- Dev environment: Unity 2022.3.22f1 (`ProjectSettings/ProjectVersion.txt`). The package declares `unity: "2022.3"`.
- Compilation happens inside Unity. There is no dotnet build, no Node, no Bun. `dotnet` appears only as the VPM CLI prerequisite. PowerShell 7 runs the one bootstrap script.
- Generated `*.csproj` and `*.sln` files are gitignored. Never commit or hand-edit them.
- `Packages/nadena.dev.ndmf/` is intentionally untracked. A fresh clone restores it with `vpm resolve project .`. A new embedded package needs a whitelist line in `Packages/.gitignore`.
- Unity MCP is development and observability tooling, never a product dependency or correctness oracle. Before any MCP write or broad operation: enumerate reachable instances read-only, inspect `Application.dataPath`, and require an exact match to this project root. Stop instead of guessing.

## Testing & QA

- NUnit through Unity Test Framework, EditMode only. Two test assemblies: `Alrauna.Amuse.Tests.Editor` (product) and `Alrauna.Amuse.Research.Tests.Editor` (research).
- One test class per production type, named `<Type>Tests`. Characterization suites use `<Subject>CharacterizationTests`. Test folders mirror production folders. Method names are behavior sentences, for example `WhollyOpaqueSlotReplacesOnlyItsMaterial` or `SchemaVersionThreeRefusesAsUnknown`.
- Assertions use the NUnit constraint model: `Assert.That(actual, Is.EqualTo(expected))`.
- Vendor shaders are never installed in this repository. Tests run on schema-only stand-in shaders under `Hidden/Alrauna/AmuseTests/*` through the verified seams in `Tests/Editor/Build/`.
- `Tests/Editor/ReferenceFixtures/` holds public synthetic JSON-driven texture and mesh fixtures with independent outcome oracles: `ProvenOpaque`, `MustRemainTransparent`, `Unknown`. Determinism is tested, not assumed.
- RED/GREEN: a behavior change needs a failing test observed first, for a named plausible wrong implementation, then a minimal fix. Never weaken a valid test. An assertion that passes on first run is recorded as characterization, never dressed up as RED.
- Falsifiers are numbered adversarial cases, marked `--- Falsifier N: ... ---`, that a plausible wrong implementation must fail. They carry no-op guards so later changes cannot silence them.
- A filtered run that reports 0 tests is a failure. A successful compile is never validation. Record observed counts and Console output.
- The research test assembly runs `ResearchSourceApiBanTests`, which scans research production source for banned mutating Unity APIs. Research tooling stays read-only by construction.
- Census Lab is a private Unity project outside this repository, used for characterization and validation. It is never the correctness oracle. Prefer read-only use. Tests create and delete folders under `Assets/`, so they must never run in the private Lab project. Preserve its privacy tiers: raw observations stay private, only reviewed aggregate output may leave. Name machines and instances by role only.
