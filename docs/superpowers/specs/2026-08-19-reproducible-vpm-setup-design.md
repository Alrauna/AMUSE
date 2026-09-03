# Reproducible VPM Setup — Design

Milestone: `chore/reproducible-vpm-setup`
Base commit: `10d25c1` (`origin/main`, merge of PR #13)
Date: 2026-08-19

## Executive decision summary

**A clean AMUSE clone cannot resolve `nadena.dev.ndmf` 1.14.4.** The VPM
implementation that AMUSE uses has no mechanism to declare additional
repositories in the project. AMUSE also never documented the machine-global
step that supplies a repository. The repository declares the *dependency*
correctly. It never declared where the dependency can be *found*.

We verified this against the actual binaries in use, not from memory. VPM
stores package repositories only in a machine-global settings file. No
metadata that AMUSE can commit today will fix this. The fix is a documented
and deterministic setup sequence that uses the official CLI.

| Decision | Outcome |
| --- | --- |
| Project-local VPM repository declaration | **Not available in the VPM implementation that AMUSE uses.** The VPM project manifest schema has no `repositories` key. Repositories live only in machine-global settings. |
| New checked-in metadata | **None.** No committed metadata changes resolution. Checked-in metadata is unchanged, except for the existing source-control file `Packages/.gitignore` (row below). |
| New script | **None.** The official CLI is already idempotent. A wrapper adds no behavior. |
| Change to `Packages/vpm-manifest.json` | **None.** Already correct. |
| Change to `Packages/com.alrauna.amuse/package.json` | **None.** `vpmDependencies` already correct. |
| Change to `Packages/manifest.json` / `packages-lock.json` | **None.** |
| Change to `Tools/Bootstrap-NdmfStandalone.ps1` | **None.** It solves a strictly later step. |
| Change to `Packages/.gitignore` | **One line adopted:** `!com.vrchat.core.*/`. This is the rule that the official VPM restore itself converges on. See [Adopting the resolver-converged ignore rule](#adopting-the-resolver-converged-ignore-rule). |
| Change to root `.gitignore` / `.gitattributes` / CI | **None.** |
| Change to production C# or tests | **None.** |
| Tracked files changed by implementation | **Two:** `README.md` and `Packages/.gitignore` (plus this design record). |
| NDMF version | **Unchanged**, 1.14.4. |
| Committed resolved package contents | **None.** `Packages/nadena.dev.ndmf/` stays ignored. |

## Problem statement

Two different failures were conflated before:

```
clean clone
   |
   +-- (1) VPM has no repository that serves nadena.dev.ndmf   <-- THIS MILESTONE
   |         => "package not found" / resolve produces nothing
   |
   +-- (2) NDMF 1.14.4 ships Dependencies~, which Unity ignores  <-- already solved
             => Tools/Bootstrap-NdmfStandalone.ps1
```

`Tools/Bootstrap-NdmfStandalone.ps1` throws the error string
`"Resolved NDMF package not found at <path>. Restore VPM dependencies first."`.
That error is exactly failure (1). The bootstrap is correct. This milestone
does not change it.

## Inventory of the current dependency path

| Concern | Current state |
| --- | --- |
| Package-level dependency declaration | `Packages/com.alrauna.amuse/package.json` -> `vpmDependencies: { "nadena.dev.ndmf": ">=1.14.4 <2.0.0-a" }` |
| Project-level dependency declaration | `Packages/vpm-manifest.json` -> `dependencies` and `locked`, both pinned to `1.14.4`. `locked` carries no URL or repository field |
| Resolver shipped in-repo | `Packages/com.vrchat.core.vpm-resolver` 0.1.29 (`[InitializeOnLoad]`, prompts on Unity load) plus `com.vrchat.core.bootstrap` |
| Resolution engine | `Editor/Dependencies/vpm-core-lib.dll` (`VRC.PackageManagement.Core`) |
| Resolved package location | `Packages/nadena.dev.ndmf/`, ignored by `Packages/.gitignore`. At base commit the file has `/*/` with `!com.vrchat.core.*` and `!com.alrauna.amuse`. It lacks the `!com.vrchat.core.*/` rule that the official restore converges on |
| Documented setup | `README.md` says "After restoring the VPM dependencies...". It never says how to restore them |
| CI | `build-listing.yml` and `release.yml` are the stock VRChat listing/release templates. Neither restores VPM dependencies |

## Source trace

The fix depends on tooling behavior, so provenance matters here. Every
claim below was measured against a primary artifact. No claim comes from
memory.

### 1. Repositories are machine-global, and the project manifest cannot declare them

We decoded the UTF-16 user-string heap of both copies of `vpm-core-lib.dll`:

- the checked-in Unity resolver copy (`com.vrchat.core.vpm-resolver` 0.1.29), and
- the copy inside the official `VRChat.VPM.CLI` 0.1.28 NuGet package.

Both contain `VRChatCreatorCompanion`, `settings.json`, `Repos`, `{0}.json`,
`vpm-manifest.json`, and the members `AddRepo`, `UserRepoExists`,
`SanitizeUserRepos`, `ClearUserRepos`, `userRepos`. The project-manifest
type `VPMProjectManifest` exposes only `Dependencies` and locked state. It
has **no `repositories` member and no URL field for a locked package**.
Repository knowledge exists only in the machine-global settings file.

This is the whole bug. The repository can declare *what* it needs. It
cannot declare *where* to get it. Stated precisely: **the VPM
implementation used by AMUSE currently has no mechanism to declare
additional repositories in the project.** If a future VPM release adds
such a mechanism, revisit the conclusion of this milestone. The conclusion
holds for the versions in use.

### 2. The authoritative NDMF repository endpoint

Fetched live:

```
GET https://vpm.nadena.dev/vpm.json
  -> 200, redirects to https://repositories.vpm.nadena.dev/repositories/nadena.dev/vpm.json
```

**Primary provenance — maintainer documentation.** The official Modular
Avatar installation documentation (<https://modular-avatar.nadena.dev/docs/intro>)
comes from the same author as NDMF. It tells users to add exactly
`https://vpm.nadena.dev/vpm.json` and names that repository **`bd_`**. This
is independent maintainer authority for the endpoint. It is not an
inference from the bytes served at that URL.

**Corroboration — the served listing agrees with that documentation:**

| Field | Value |
| --- | --- |
| `id` | `dev.nadena.vpm` |
| `name` / `author` | `bd_` |
| `url` | `https://vpm.nadena.dev/vpm.json` |
| `nadena.dev.ndmf` versions | 147, including `1.14.4` |
| `1.14.4` `zipSHA256` | supplied by the listing (`2b2ad360...c205a9e5`) |
| `1.14.4` `vpmDependencies` | none — NDMF is a leaf. Nothing else must resolve |

This document therefore records `https://vpm.nadena.dev/vpm.json` as
authoritative.
The maintainer documentation is the primary source. The identity and
contents of the served listing support it. The URL is not recorded merely
because it worked before.

NDMF is **not** in the official or curated VRChat repositories. Both cached
listings contain zero `nadena.*` packages. The repository addition is
therefore required.

### 3. The official mechanism for additional repositories

Primary source: <https://vcc.docs.vrchat.com/vpm/cli/>

**Documented prerequisite:** "You'll need the .NET 8 SDK installed." AMUSE
documents the .NET 8 SDK as the supported prerequisite, not a newer SDK. A
newer SDK or runtime may also run the CLI. That observation is
characterization only and is never part of the documented AMUSE setup. In
particular, `DOTNET_ROLL_FORWARD` is not part of the documented setup.

```
dotnet tool install --global vrchat.vpm.cli
vpm list repos                 # enumerates Official, Curated and User repos
vpm add repo <path>            # local or remote listing; writes to Settings
vpm resolve project [<name>]   # restores packages from vpm-manifest.json
```

`vpm resolve project` is documented as the same restore that Unity performs
on project open.

**Documented exit codes matter here.** The documentation says `vpm add
repo` "returns 0 if the repo was added and 1 if it was not". An add of an
already-present repository is thus an unsuccessful *add*, although the
desired *state* is already correct. The setup sequence is written around
that (see Idempotence model).

### 3a. Platform support

The setup uses the official .NET-based VPM CLI. It introduces no
AMUSE-specific assumptions about machine paths or operating systems.
VRChat documents a macOS setup. Its current documentation describes Linux
support as untested. AMUSE therefore does not claim more platform support
for the VPM CLI than VRChat itself. The AMUSE repository policy stays
platform-neutral. Platform-neutral policy does not mean that every
external tool has equal vendor support on every OS.

### 4. Where the machine-global state actually lives

| Consumer | Store on macOS | Basis |
| --- | --- | --- |
| Unity's in-Editor resolver (checked-in `vpm-core-lib`: `SpecialFolder.GetFolderPath`, no XDG literals) | `~/.local/share/VRChatCreatorCompanion` | **Observed.** We compiled a probe with the Mono that Unity 2022.3.22f1 bundles. `SpecialFolder.LocalApplicationData` resolves to `~/.local/share`, and it **honors `XDG_DATA_HOME`**. |
| Official VPM CLI (newer `vpm-core-lib`: contains explicit `XDG_DATA_HOME` and `.local` literals) | `$XDG_DATA_HOME`, else `~/.local/share/VRChatCreatorCompanion` | Binary literals, then **confirmed in V2**. With `XDG_DATA_HOME` set, the CLI created its store in the scratch directory. |

Both consumers converge on the same directory on macOS. A repository added
by the CLI is therefore also visible to the Unity resolver. `XDG_DATA_HOME`
isolates **both** consumers at once. That is what makes an isolated
clean-state proof possible (see Verification).

This behavior is specific to Mono, not a .NET rule. A probe under the
.NET 10 SDK on this machine resolves `LocalApplicationData` to
`~/Library/Application Support`. The probe ignores `XDG_DATA_HOME` and an
overridden `HOME`. The XDG literals in the CLI are expected to override
that Mono behavior. This is why V2 must observe the behavior and not
assume it.

### 5. The exact inherited state that made previous setups work

On the current development machine,
`~/.local/share/VRChatCreatorCompanion/settings.json` contains 23
`userRepos`, including:

```json
{ "name": "bd_", "id": "dev.nadena.vpm", "url": "https://vpm.nadena.dev/vpm.json" }
```

One entry, added by hand at some point, is the entire reason that NDMF ever
resolved on this machine. The entry is not in the Git repository. It is not
documented. It does not travel with a clone. Two incidental observations
follow. They explain the environment but are **not** in scope:

- Every `userRepos[].localPath` value is a Windows path
  (`C:\Users\User\AppData\Local\...`). This settings file came from a
  Windows checkout. The Unix tools ignore the stale `localPath` and
  re-cache under `Repos/<id>.json`.
- `/Applications/ALCOM.app` is installed and shares that directory. Some
  resolution on this machine was thus performed by ALCOM, not by Unity or
  the CLI.

## Decision

Document the official CLI sequence in `README.md`. Align the existing
`Packages/.gitignore` with the state that the official restore converges
on. A documented clean-clone restore then does not make the repository
dirty. Add nothing else. No script, no new metadata file, no dependency
change.

### Documented setup sequence

```
ONCE PER MACHINE
   install the .NET 8 SDK
   dotnet tool install --global vrchat.vpm.cli      (if absent)
   vpm list repos
   if dev.nadena.vpm is absent:
       vpm add repo https://vpm.nadena.dev/vpm.json

PER CLONE
   git clone
   vpm resolve project .
   pwsh -NoProfile -File ./Tools/Bootstrap-NdmfStandalone.ps1
   open Unity
```

The `vpm list repos` check is a prerequisite *detection* step, not a
workaround. It keeps the machine-global stage correct. It does not ask a
second `vpm add repo` invocation to exit 0. This milestone documents no
Creator Companion GUI ritual, because a deterministic CLI mechanism exists.

### Adopting the resolver-converged ignore rule

The isolated clean-clone proof showed this result. The first
`vpm resolve project .` deterministically appends `!com.vrchat.core.*/` to
the tracked `Packages/.gitignore`. This is **not** incidental machine
churn. It is not comparable to the macOS Unity toolchain additions. It is
the source-control model of VRChat itself for VPM projects. That model
ignores resolved VPM packages and retains the `com.vrchat.core` resolver
packages. The ignore file of the repository was missing the rule that the
official tooling maintains.

A documented clean-clone restore must not leave the repository dirty. The
rule is therefore adopted into `Packages/.gitignore`, not documented as
something to discard. The existing line `!com.vrchat.core.*` (no trailing
slash) does not satisfy the tooling. The tooling converges on the
directory-scoped form. The exact line was absent, and the change added
exactly that line. No other pattern changed. The file was not redesigned.

Verified consequence: with the rule present, `vpm resolve project .`
leaves `Packages/.gitignore` and `Packages/vpm-manifest.json`
byte-identical and `git status` empty. This holds on the first run and on
a repeat.

### Why no script

The machine-global stage is a once-per-machine prerequisite. A
`vpm list repos` check guards it. `vpm resolve project` is already
state-idempotent. A wrapper would add a maintained artifact. Its only
content is a conditional that the reader can perform directly. It would
also need tests on three platforms to earn its place. This follows
AGENTS.md ("prefer the native supported mechanism", "do not add a
dependency or code before implemented behaviour needs it") and the Ponytail
ladder.

### Rejected alternatives

| Alternative | Why rejected |
| --- | --- |
| Commit a project-local repository listing JSON and `vpm add repo ./that/file` | Mirrors upstream listing data that goes stale immediately. It still points at remote package archives. It duplicates a trust decision that naming the authoritative endpoint expresses better. It also does not remove the machine-global step. It only changes what is registered. |
| Reference NDMF as a UPM git dependency in `Packages/manifest.json` | Bypasses VPM entirely. It diverges from the VRChat packaging model that AMUSE ships under. It changes the dependency mechanism in a milestone that is explicitly not a dependency change. |
| Commit `Packages/nadena.dev.ndmf/` | Prohibited by the task and by AGENTS.md. A machine-generated resolved copy is not the source of truth. |
| Document adding the repo through the Creator Companion / ALCOM GUI | A deterministic official CLI exists. A GUI ritual is neither scriptable nor verifiable. |

## Supply-chain boundary

The registered endpoint is HTTPS. It is the endpoint that the
NDMF/Modular Avatar maintainer documents. The README names it explicitly,
so the trust decision is visible, not implied. The listing **supplies** a
`zipSHA256` per version. This milestone did not source-trace the
verification behavior of VPM. It therefore makes no claim about whether or
how that hash is checked. It does not need to make that claim. Nothing
else is registered. No verification is disabled. No remote script is
fetched or executed. The package manager performs the download through its
normal supported path.

## Idempotence model

Two different properties must not be conflated:

- **state-idempotent** — running the step again leaves the same correct end state.
- **exit-success-idempotent** — running the step again also exits 0.

The documented setup requires the first property everywhere. It requires
the second only where the tool actually offers it. It never manufactures
the second by re-running a command that documents a nonzero exit for the
already-satisfied case.

| Step | Stage | Property claimed |
| --- | --- | --- |
| Install .NET 8 SDK | once per machine | Prerequisite. Presence is checked, not re-installed. |
| `dotnet tool install --global vrchat.vpm.cli` | once per machine | Prerequisite, run only if absent. Not a command that must blindly succeed twice. |
| `vpm list repos` -> add only if `dev.nadena.vpm` absent | once per machine | **State-idempotent by detection.** The guard is what makes the stage repeatable. A duplicate `vpm add repo` is never required. |
| `vpm add repo https://vpm.nadena.dev/vpm.json` | once per machine | Documented as returning 0 when added and 1 when not. A duplicate add is therefore **not** claimed exit-success-idempotent. `vpm-core-lib` carries a `UserRepoExists` guard. That guard is expected to prevent a duplicate `userRepos` entry. That is a state property, characterized in V5, not asserted here. |
| `vpm resolve project .` | per clone | **State-idempotent.** The locked version is already satisfied. No change to `vpm-manifest.json`, no re-download, no Git dirt. |
| `Tools/Bootstrap-NdmfStandalone.ps1` | per clone | State-idempotent **and** exit-success-idempotent. It hashes source against destination and exits 0 early. |

The whole sequence, run twice, must leave `git status` clean and
`Packages/vpm-manifest.json` byte-identical.

## Verification

### Clean-environment proof

A plain re-run on this machine proves nothing. This machine already
carries the `dev.nadena.vpm` entry. Isolation uses a lever built into the
CLI:

```
XDG_DATA_HOME=<scratch>/clean-vpm-config
```

`XDG_DATA_HOME` redirects the entire `VRChatCreatorCompanion` store of the
CLI into a scratch directory. The store includes `settings.json`, the
`Repos/` cache, and the package cache. Under isolation, the verification
never reads, writes, moves, or deletes the real
`~/.local/share/VRChatCreatorCompanion`. ALCOM state stays untouched. This
guarantee covers the isolated steps only. V7 deliberately runs without
isolation. There, inspecting the real store proved not to be a byte-level
no-op. See the V7 note under *Observed characterization*.

Before any isolated command runs, the verification captures a **baseline
of the real store**. The baseline holds the `settings.json` content hash,
the `userRepos` count and ids, and a listing of `Repos/` with sizes and
mtimes. "The real store was untouched" is thus an observation against a
recorded state, not an assumption.

The clone under test is a fresh `git clone` of this branch into the
scratchpad. No resolved package can thus land in the real worktree.

The `XDG_DATA_HOME` support was read out of the CLI binary, not from
documentation. Step V2 below **must observe** that the isolated store is
actually created in the scratch directory. If it is not, the isolation is
void. The milestone then stops for a redesign. It does not report an
unproven result.

### Verification matrix

| # | Scenario | Expected observation |
| --- | --- | --- |
| V1 | Supported prerequisite present, CLI runs | Install the **.NET 8 SDK** (the documented prerequisite) and the CLI. `vpm --version` succeeds under it. Record the exact SDK version and CLI version. Any behavior observed under the pre-existing .NET 10 SDK is characterization only. It is not what the README documents. |
| V2 | **HARD GATE — isolation is real** | Capture the baseline of the real store first (see above). With `XDG_DATA_HOME` set to scratch, the CLI creates or uses a `VRChatCreatorCompanion/settings.json` **in scratch**. The real store is also byte-for-byte unchanged against the baseline. Both halves must hold. If not proven: **STOP** and report. Do not improvise another isolation mechanism without review. |
| V3 | **Failure before the fix** | Fresh clone and isolated empty store. `vpm resolve project .` runs. `nadena.dev.ndmf` does **not** resolve. `Packages/nadena.dev.ndmf/` stays absent. This is the reproduction. |
| V4 | Fix in isolation | Run `vpm add repo https://vpm.nadena.dev/vpm.json`, then `vpm resolve project .`. `Packages/nadena.dev.ndmf/package.json` reports `1.14.4`. |
| V5 | Idempotence, per the corrected model | (a) Exactly one `dev.nadena.vpm` entry exists. (b) The `vpm list repos` detection step removes the need for a duplicate add. If a duplicate `vpm add repo` runs anyway, its exit code is **characterized**, not treated as a failure of state idempotence. (c) Against a clone that carries the adopted ignore rule, resolve #1 and resolve #2 each leave `Packages/.gitignore` **and** `Packages/vpm-manifest.json` byte-identical. The `git status` of the clone stays completely clean. |
| V6 | Bootstrap after resolution | `pwsh -NoProfile -File ./Tools/Bootstrap-NdmfStandalone.ps1` succeeds in the isolated clone. It then succeeds again and reports "already bootstrapped". |
| V7 | Configured machine already satisfied | Real worktree, no isolation. `vpm list repos` already reports `dev.nadena.vpm`. The guard of the machine-global stage thus skips it, and **no duplicate repository is added**. Inspecting the real store is **not** a byte-level no-op. See the V7 note under *Observed characterization*. |
| V8 | Unity resolves without the missing-package error | Public project only. Identify it per AGENTS.md by `Application.dataPath == <repo-root>/Assets` through read-only MCP discovery. Confirm that NDMF is present and that no missing-package or compile error appears. The private testbed is never used. |
| V9 | Git cleanliness | `git status` shows only `README.md`, `Packages/.gitignore`, and this design record. No resolved package is tracked. No `com.unity.toolchain.*` or `com.unity.sysroot*` entries remain in `Packages/manifest.json` or `packages-lock.json`. |

EditMode tests are **not** run for this milestone. The milestone changes
no production C#, no test, no package metadata, and no assembly
definition. V8 was intended to cover the only compile-relevant risk, that
the project still opens with NDMF present. V8 could not run (see *V8 was
not run* below). That risk is therefore reported as outstanding, not
treated as cleared. The verification confirmed that the final diff touches
no compiled file. The exemption therefore still holds. A compiled change
voids this exemption.

### macOS Unity toolchain churn

`Packages/manifest.json` and `packages-lock.json` contain **no**
`com.unity.toolchain.*` or `com.unity.sysroot*` entries at base commit.

**This occurred during the milestone.** A running Unity Editor added
`com.unity.toolchain.macos-arm64-linux-x86_64` to `manifest.json`. It added
`com.unity.sysroot`, `com.unity.sysroot.linux-x86_64`, and the toolchain
entry to `packages-lock.json`. The verification inspected both diffs in
full, and they contained **nothing else**. A targeted
`git checkout -- Packages/manifest.json Packages/packages-lock.json` was
therefore safe and lost no intentional change. Both files are back at
HEAD. This may recur while that Editor stays open. The same targeted
restoration applies, but only after a full check that the diff contains
nothing else. Whether those dependencies belong in AMUSE is out of scope.

## Execution checklist

1. Install the **.NET 8 SDK** and the official CLI to an isolated
   `--tool-path` under the scratchpad. This avoids a change to the global
   dotnet tools of the user during verification. Confirm V1 and record
   both versions.
2. Capture the real-store baseline. Then run V2 as a hard gate. Run V3 and
   capture the observed pre-fix failure verbatim.
3. Run V4, V5, V6 in the isolated clone.
4. Write the `README.md` "Development setup" change. It holds the
   prerequisites (.NET 8 SDK, VPM CLI, `pwsh`), the once-per-machine
   repository stage as a `vpm list repos` check before `vpm add repo`, the
   per-clone restore, the existing bootstrap, and the open step for Unity.
   Include the authoritative repository URL with a one-line note on its
   maintainer provenance. Include the platform-support wording from
   section 3a. Keep the existing NDMF bootstrap paragraph intact.
5. Run V7 on the real worktree.
6. Run V8 against the public project via read-only MCP discovery.
7. Run V9. Inspect unstaged and staged diffs separately.
8. Report observed results. Include anything that could not be observed,
   and why.

## Observed characterization

These observations qualify claims made above. They were measured, not
predicted.

| Observation | Detail |
| --- | --- |
| Toolchain used | .NET SDK 8.0.424 (runtime 8.0.30), installed to the scratchpad. VPM CLI 0.1.28. The pre-existing .NET 10.0.400 SDK was not used for the documented path. |
| `XDG_DATA_HOME` isolation | Confirmed for the CLI, not merely inferred from binary literals. The CLI created a fresh store with `userRepos: []` under the scratch directory. The real store was byte-identical afterwards. Reconfirmed on the re-proof run. |
| V7 — inspecting the real store is not read-only | `vpm list repos` ran without isolation. It **normalized and re-cached machine-global repository state**. It rewrote `settings.json` (7296 -> 7162 bytes) and re-fetched every repository cache under `Repos/`. The logical set was preserved: 23 repositories, identical id set, NDMF URL intact, `userProjects` and all other settings keys unchanged. The same run replaced the stale Windows `localPath` values with valid macOS cache paths. This was accepted as an observed tooling side effect and not restored further. Inspection of the real VPM store must therefore **not** be described as guaranteed read-only or as a strict byte-level no-op. |
| `vpm resolve project` exit code | **Exits 0 even when resolution fails.** The pre-fix run logged `Could not get match for nadena.dev.ndmf 1.14.4` and `Could not resolve package nadena.dev.ndmf 1.14.4`, and still returned 0. Success must therefore be judged by the resolved `package.json` version, never by the exit code. The README says so explicitly. |
| `vpm resolve project` and `Packages/.gitignore` | The first run on the unmodified file deterministically appended `!com.vrchat.core.*/` and then converged. Runs 2 and 3 changed nothing. Diagnosed as the VPM source-control rule of VRChat itself, not disposable churn, and **adopted into the repository** (see [Adopting the resolver-converged ignore rule](#adopting-the-resolver-converged-ignore-rule)). Re-proved afterwards: with the rule present, two consecutive resolves leave the file byte-identical and `git status` empty. |
| Duplicate `vpm add repo` | The documentation states a return of 1 when the repo was not added. **Observed on CLI 0.1.28: exit 0**, with `[WRN] You already have a repo with that url`. The entry count stayed at exactly one. Characterization only. The documented setup does not depend on either value, because the `vpm list repos` check gates the add. |
| Repository identity as served | `vpm list repos` renders the added entry as `dev.nadena.vpm | bd_ (bd_)`. This matches the maintainer documentation. |

### V8 was not run

The Unity MCP bridge reported `No Unity Editor instances found` on two
consecutive read-only attempts, and again on a later retry.
`set_active_instance` returned `No Unity instances are currently connected.
Start Unity and press 'Start Session'.` The verification therefore could
not evaluate the AGENTS.md identity rule at all. That rule selects the
instance whose normalized `Application.dataPath` equals
`<repo-root>/Assets`.

Two Unity Editors run on this machine, one on the public project and one
on the private testbed. Without read-only MCP discovery, a choice between
them would be a guess. The policy therefore requires stopping. The
verification used no substitute: not process paths, not window titles, not
filesystem inspection, and not the private testbed. This milestone also
rejected launching a second Editor against the public project. That
project is already held by a running Editor.

V8 therefore remains outstanding. This record makes no claim about the
package or compile state of the public project. V8 can run again unchanged
after a bridge session starts inside the Editor of the public project.

## Expected persistent diff

```
M  README.md
M  Packages/.gitignore
?? docs/superpowers/specs/2026-08-19-reproducible-vpm-setup-design.md
```

Nothing else. `Packages/vpm-manifest.json`, `Packages/manifest.json`,
`Packages/packages-lock.json`, `Packages/com.alrauna.amuse/package.json`,
`Tools/Bootstrap-NdmfStandalone.ps1`, CI, production C#, and tests are all
unchanged. The NDMF version constraint stays at 1.14.4.

## Out of scope

- `feat/end-to-end-alpha-analysis`
- non-readable alpha evidence
- production C#
- semantic architecture
- CI
- rewriting the NDMF standalone bootstrap
- the macOS -> Linux Unity toolchain churn decision
- `.gitattributes`
- historical `docs/superpowers/` records
- the private testbed
- any dependency upgrade, NDMF included
