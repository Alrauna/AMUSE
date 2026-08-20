# Reproducible VPM Setup — Design

Milestone: `chore/reproducible-vpm-setup`
Base commit: `10d25c1` (`origin/main`, merge of PR #13)
Date: 2026-08-19

## Executive decision summary

**A clean AMUSE clone cannot resolve `nadena.dev.ndmf` 1.14.4 because the VPM
implementation AMUSE uses currently has no project-local additional-repository declaration
mechanism, and AMUSE never documented the machine-global step that supplies one.** The
repository declares the *dependency* correctly and never declared where the dependency can
be *found*.

That VPM stores package repositories only in a machine-global settings file was verified
against the actual binaries in use, not from memory. Therefore no metadata AMUSE can commit
today will fix this; the fix is a documented, deterministic, official-CLI setup sequence.

| Decision | Outcome |
| --- | --- |
| Project-local VPM repository declaration | **Not available in the VPM implementation AMUSE uses.** No `repositories` key in the VPM project manifest schema; repositories live only in machine-global settings. |
| New checked-in metadata | **None.** No committed metadata changes resolution; checked-in metadata is unchanged except the existing source-control support file `Packages/.gitignore` (row below). |
| New script | **None.** The official CLI is already idempotent; a wrapper adds no behavior. |
| Change to `Packages/vpm-manifest.json` | **None.** Already correct. |
| Change to `Packages/com.alrauna.amuse/package.json` | **None.** `vpmDependencies` already correct. |
| Change to `Packages/manifest.json` / `packages-lock.json` | **None.** |
| Change to `Tools/Bootstrap-NdmfStandalone.ps1` | **None.** It solves a strictly later step. |
| Change to `Packages/.gitignore` | **One line adopted:** `!com.vrchat.core.*/`, the rule the official VPM restore itself converges on. See [Adopting the resolver-converged ignore rule](#adopting-the-resolver-converged-ignore-rule). |
| Change to root `.gitignore` / `.gitattributes` / CI | **None.** |
| Change to production C# or tests | **None.** |
| Tracked files changed by implementation | **Two:** `README.md` and `Packages/.gitignore` (plus this design record). |
| NDMF version | **Unchanged**, 1.14.4. |
| Committed resolved package contents | **None.** `Packages/nadena.dev.ndmf/` stays ignored. |

## Problem statement

Two distinct failures were previously conflated:

```
clean clone
   |
   +-- (1) VPM has no repository that serves nadena.dev.ndmf   <-- THIS MILESTONE
   |         => "package not found" / resolve produces nothing
   |
   +-- (2) NDMF 1.14.4 ships Dependencies~, which Unity ignores  <-- already solved
             => Tools/Bootstrap-NdmfStandalone.ps1
```

`Tools/Bootstrap-NdmfStandalone.ps1` explicitly throws
`"Resolved NDMF package not found at <path>. Restore VPM dependencies first."`, which is
exactly failure (1) surfacing. The bootstrap is correct and is not touched here.

## Inventory of the current dependency path

| Concern | Current state |
| --- | --- |
| Package-level dependency declaration | `Packages/com.alrauna.amuse/package.json` -> `vpmDependencies: { "nadena.dev.ndmf": ">=1.14.4 <2.0.0-a" }` |
| Project-level dependency declaration | `Packages/vpm-manifest.json` -> `dependencies` and `locked`, both pinned to `1.14.4`; `locked` carries no URL or repository field |
| Resolver shipped in-repo | `Packages/com.vrchat.core.vpm-resolver` 0.1.29 (`[InitializeOnLoad]`, prompts on Unity load) plus `com.vrchat.core.bootstrap` |
| Resolution engine | `Editor/Dependencies/vpm-core-lib.dll` (`VRC.PackageManagement.Core`) |
| Resolved package location | `Packages/nadena.dev.ndmf/`, ignored by `Packages/.gitignore` — at base commit `/*/` with `!com.vrchat.core.*` and `!com.alrauna.amuse`, missing the `!com.vrchat.core.*/` rule the official restore converges on |
| Documented setup | `README.md` says "After restoring the VPM dependencies..." and never says how to restore them |
| CI | `build-listing.yml` and `release.yml` are the stock VRChat listing/release templates; neither restores VPM dependencies |

## Source trace

Provenance matters here because the fix depends on tooling behaviour, so every claim below
was measured against a primary artifact rather than recalled.

### 1. Repositories are machine-global; the project manifest cannot declare them

Decoded the UTF-16 user-string heap of both copies of `vpm-core-lib.dll`:

- checked-in Unity resolver copy (`com.vrchat.core.vpm-resolver` 0.1.29), and
- the copy inside the official `VRChat.VPM.CLI` 0.1.28 NuGet package.

Both contain `VRChatCreatorCompanion`, `settings.json`, `Repos`, `{0}.json`,
`vpm-manifest.json`, and members `AddRepo`, `UserRepoExists`, `SanitizeUserRepos`,
`ClearUserRepos`, `userRepos`. The project-manifest type `VPMProjectManifest` exposes only
`Dependencies`/locked state — there is **no `repositories` member, and no per-locked-package
URL field**. Repository knowledge exists only in the machine-global settings file.

This is the whole bug: the repository can declare *what* it needs but not *where* to get
it. Stated precisely: **the VPM implementation used by AMUSE currently has no project-local
additional-repository declaration mechanism.** If a future VPM release adds one, this
milestone's conclusion should be revisited; the conclusion holds for the versions in use.

### 2. The authoritative NDMF repository endpoint

Fetched live:

```
GET https://vpm.nadena.dev/vpm.json
  -> 200, redirects to https://repositories.vpm.nadena.dev/repositories/nadena.dev/vpm.json
```

**Primary provenance — maintainer documentation.** The official Modular Avatar
installation documentation (<https://modular-avatar.nadena.dev/docs/intro>), maintained by
the same author as NDMF, instructs users to add exactly `https://vpm.nadena.dev/vpm.json`
and identifies that repository as **`bd_`**. This is independent maintainer authority for
the endpoint, not an inference from the bytes served at it.

**Corroboration — the served listing agrees with that documentation:**

| Field | Value |
| --- | --- |
| `id` | `dev.nadena.vpm` |
| `name` / `author` | `bd_` |
| `url` | `https://vpm.nadena.dev/vpm.json` |
| `nadena.dev.ndmf` versions | 147, including `1.14.4` |
| `1.14.4` `zipSHA256` | supplied by the listing (`2b2ad360...c205a9e5`) |
| `1.14.4` `vpmDependencies` | none — NDMF is a leaf; nothing else must resolve |

`https://vpm.nadena.dev/vpm.json` is therefore recorded as authoritative on maintainer
documentation, corroborated by the identity and contents of the listing served there. It is
not carried over merely because it happened to work before.

NDMF is **not** in VRChat's official or curated repositories (checked both cached listings:
zero `nadena.*` packages), so the repository addition is genuinely required.

### 3. The official mechanism for additional repositories

Primary source: <https://vcc.docs.vrchat.com/vpm/cli/>

**Documented prerequisite:** "You'll need the .NET 8 SDK installed." AMUSE documents the
.NET 8 SDK, not a newer SDK, as the supported prerequisite. Whether a newer SDK or runtime
also happens to run the CLI is recorded as characterization only and is never presented as
the AMUSE setup; in particular `DOTNET_ROLL_FORWARD` is not part of the documented setup.

```
dotnet tool install --global vrchat.vpm.cli
vpm list repos                 # enumerates Official, Curated and User repos
vpm add repo <path>            # local or remote listing; writes to Settings
vpm resolve project [<name>]   # restores packages from vpm-manifest.json
```

`vpm resolve project` is documented as the same restore Unity performs on project open.

**Documented exit codes matter here.** `vpm add repo` "returns 0 if the repo was added and
1 if it was not" — so adding an already-present repository is an unsuccessful *add*, even
though the desired *state* is already correct. The setup sequence is written around that
(see Idempotence model).

### 3a. Platform support

The setup uses the official .NET-based VPM CLI and introduces no AMUSE-specific machine-path
or OS assumptions. VRChat documents a macOS setup, while its current documentation describes
Linux support as untested. AMUSE therefore does not claim stronger platform support for the
VPM CLI than VRChat itself. The AMUSE repository policy remains platform-neutral; that does
not mean every external tool has equal vendor support on every OS.

### 4. Where the machine-global state actually lives

| Consumer | Store on macOS | Basis |
| --- | --- | --- |
| Unity's in-Editor resolver (checked-in `vpm-core-lib`: `SpecialFolder.GetFolderPath`, no XDG literals) | `~/.local/share/VRChatCreatorCompanion` | **Observed.** Compiled a probe with Unity 2022.3.22f1's bundled Mono: `SpecialFolder.LocalApplicationData` resolves to `~/.local/share`, and it **honours `XDG_DATA_HOME`**. |
| Official VPM CLI (newer `vpm-core-lib`: contains explicit `XDG_DATA_HOME` and `.local` literals) | `$XDG_DATA_HOME`, else `~/.local/share/VRChatCreatorCompanion` | Binary literals, then **confirmed in V2**: with `XDG_DATA_HOME` set, the CLI created its store in the scratch directory. |

Both therefore converge on the same directory on macOS, so a repository added by the CLI is
also visible to Unity's resolver, and `XDG_DATA_HOME` isolates **both** consumers at once.
That is what makes an isolated clean-state proof possible (see Verification).

Note that this is Mono-specific behaviour, not a .NET rule: a probe under this machine's
.NET 10 SDK resolves `LocalApplicationData` to `~/Library/Application Support` and ignores
both `XDG_DATA_HOME` and an overridden `HOME`. The CLI's own XDG literals are what is
expected to override that, which is precisely why V2 must observe it rather than assume it.

### 5. The exact inherited state that made previous setups work

On the current development machine, `~/.local/share/VRChatCreatorCompanion/settings.json`
contains 23 `userRepos`, including:

```json
{ "name": "bd_", "id": "dev.nadena.vpm", "url": "https://vpm.nadena.dev/vpm.json" }
```

That single entry — added manually, at some point, by hand — is the entire reason NDMF has
ever resolved here. It is not in the Git repository, is not documented, and does not travel
with a clone. Two incidental observations, recorded because they explain the environment
but are **not** in scope:

- every `userRepos[].localPath` is a Windows path (`C:\Users\User\AppData\Local\...`),
  i.e. this settings file was carried over from a Windows checkout; the Unix tools ignore
  the stale `localPath` and re-cache under `Repos/<id>.json`;
- `/Applications/ALCOM.app` is installed and shares that same directory, so some of the
  resolution on this machine was performed by ALCOM rather than by Unity or the CLI.

## Decision

Document the official CLI sequence in `README.md`, and align the existing
`Packages/.gitignore` with the state the official restore converges on so that a documented
clean-clone restore does not dirty the repository. Add nothing else — no script, no new
metadata file, no dependency change.

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

The `vpm list repos` check is a prerequisite *detection* step, not a workaround: it is what
keeps the machine-global stage correct without asking a second `vpm add repo` invocation to
exit 0. No Creator Companion GUI ritual is documented, because a deterministic CLI mechanism
exists.

### Adopting the resolver-converged ignore rule

The isolated clean-clone proof showed that the first `vpm resolve project .` deterministically
appends `!com.vrchat.core.*/` to the tracked `Packages/.gitignore`. This is **not** incidental
machine churn, and it is not comparable to the macOS Unity toolchain additions: it is VRChat's
own source-control model for VPM projects, which ignores resolved VPM packages while retaining
the `com.vrchat.core` resolver packages. The repository's ignore file was simply missing the
rule the official tooling maintains.

A documented clean-clone restore must not leave the repository dirty, so the rule is adopted
into `Packages/.gitignore` rather than documented as something to discard. The existing line
`!com.vrchat.core.*` (no trailing slash) does not satisfy the tooling, which converges on the
directory-scoped form; the exact line was absent and exactly that line was added, with no other
pattern touched and no redesign of the file.

Verified consequence: with the rule present, `vpm resolve project .` leaves
`Packages/.gitignore` and `Packages/vpm-manifest.json` byte-identical and `git status` empty,
on the first run and on a repeat.

### Why no script

The machine-global stage is a once-per-machine prerequisite guarded by a `vpm list repos`
check, and `vpm resolve project` is already state-idempotent. A wrapper would add a
maintained artifact whose only content is a conditional the reader can perform directly,
and would have to be tested on three platforms to earn its place.
This follows AGENTS.md ("prefer the native supported mechanism", "do not add a dependency
or code before implemented behaviour needs it") and the Ponytail ladder.

### Rejected alternatives

| Alternative | Why rejected |
| --- | --- |
| Commit a project-local repository listing JSON and `vpm add repo ./that/file` | Mirrors upstream listing data that goes stale immediately, still points at remote package archives, and duplicates a trust decision better expressed by naming the authoritative endpoint. It also does not remove the machine-global step — it only changes what is registered. |
| Reference NDMF as a UPM git dependency in `Packages/manifest.json` | Bypasses VPM entirely, diverges from the VRChat packaging model AMUSE ships under, and changes the dependency mechanism in a milestone that is explicitly not a dependency change. |
| Commit `Packages/nadena.dev.ndmf/` | Prohibited by the task and by AGENTS.md: a machine-generated resolved copy is not the source of truth. |
| Document adding the repo through the Creator Companion / ALCOM GUI | A deterministic official CLI exists; a GUI ritual is neither scriptable nor verifiable. |

## Supply-chain boundary

The registered endpoint is HTTPS, is the endpoint the NDMF/Modular Avatar maintainer
documents, and is named explicitly in the README so the trust decision is visible rather
than implied. The listing **supplies** a `zipSHA256` per version; this milestone did not
source-trace VPM's verification behaviour and therefore makes no claim about whether or how
that hash is checked, which it does not need to. Nothing else is registered, no verification
is disabled, and no remote script is fetched or executed — the package manager performs the
download through its normal supported path.

## Idempotence model

Two different properties must not be conflated:

- **state-idempotent** — running the step again leaves the same correct end state;
- **exit-success-idempotent** — running the step again also exits 0.

The documented setup requires the first everywhere. It requires the second only where the
tool actually offers it, and never manufactures it by re-running a command that documents a
nonzero exit for the already-satisfied case.

| Step | Stage | Property claimed |
| --- | --- | --- |
| Install .NET 8 SDK | once per machine | Prerequisite. Presence is checked, not re-installed. |
| `dotnet tool install --global vrchat.vpm.cli` | once per machine | Prerequisite, run only if absent. Not a command that must blindly succeed twice. |
| `vpm list repos` -> add only if `dev.nadena.vpm` absent | once per machine | **State-idempotent by detection.** The guard is what makes the stage repeatable; a duplicate `vpm add repo` is never required. |
| `vpm add repo https://vpm.nadena.dev/vpm.json` | once per machine | Documented as returning 0 when added and 1 when not, so a duplicate add is **not** claimed exit-success-idempotent. `vpm-core-lib` carries a `UserRepoExists` guard, which is expected to prevent a duplicate `userRepos` entry — a state property, characterized in V5, not asserted here. |
| `vpm resolve project .` | per clone | **State-idempotent.** Locked version already satisfied -> no change to `vpm-manifest.json`, no re-download, no Git dirt. |
| `Tools/Bootstrap-NdmfStandalone.ps1` | per clone | State- **and** exit-success-idempotent: hashes source against destination and exits 0 early. |

Whole sequence run twice must leave `git status` clean and `Packages/vpm-manifest.json`
byte-identical.

## Verification

### Clean-environment proof

A plain re-run on this machine proves nothing, because this machine already carries the
`dev.nadena.vpm` entry. Isolation uses the CLI's own documented-by-implementation lever:

```
XDG_DATA_HOME=<scratch>/clean-vpm-config
```

which redirects the CLI's entire `VRChatCreatorCompanion` store — `settings.json`, `Repos/`
cache, package cache — into a scratch directory. Under isolation the real
`~/.local/share/VRChatCreatorCompanion` is never read, written, moved, or deleted, and ALCOM
state is untouched. This guarantee covers the isolated steps only; V7 deliberately runs
without isolation, and inspecting the real store there proved not to be a byte-level no-op —
see the V7 note under *Observed characterization*.

Before any isolated command runs, a **baseline of the real store is captured** — the
`settings.json` content hash, its `userRepos` count and ids, and a listing of `Repos/` with
sizes and mtimes — so that "the real store was untouched" is an observation compared against
a recorded prior state rather than an assumption.

The clone under test is a fresh `git clone` of this branch into the scratchpad, so no
resolved package can land in the real worktree.

Because `XDG_DATA_HOME` support was read out of the CLI binary rather than from
documentation, step V2 below **must observe** that the isolated store is actually created
in the scratch directory. If it is not, the isolation is void and the milestone stops for
a redesign rather than reporting an unproven result.

### Verification matrix

| # | Scenario | Expected observation |
| --- | --- | --- |
| V1 | Supported prerequisite present, CLI runs | Install the **.NET 8 SDK** (the documented prerequisite) and the CLI; `vpm --version` succeeds under it. Record the exact SDK version and CLI version. Any behaviour observed under the pre-existing .NET 10 SDK is characterization only and is not what the README documents. |
| V2 | **HARD GATE — isolation is real** | Baseline of the real store captured first (see above). With `XDG_DATA_HOME` set to scratch, a `VRChatCreatorCompanion/settings.json` is created/used **in scratch**, **and** the real store is byte-for-byte unchanged against the baseline. Both halves must hold. If not proven: **STOP** and report — do not improvise another isolation mechanism without review. |
| V3 | **Failure before the fix** | Fresh clone + isolated empty store, `vpm resolve project .` -> `nadena.dev.ndmf` does **not** resolve; `Packages/nadena.dev.ndmf/` absent. This is the reproduction. |
| V4 | Fix in isolation | `vpm add repo https://vpm.nadena.dev/vpm.json` then `vpm resolve project .` -> `Packages/nadena.dev.ndmf/package.json` reports `1.14.4`. |
| V5 | Idempotence, per the corrected model | (a) exactly one `dev.nadena.vpm` entry exists; (b) the `vpm list repos` detection step prevents a duplicate-add requirement — and if a duplicate `vpm add repo` is exercised, its exit code is **characterized**, not treated as a failure of state idempotence; (c) against a clone carrying the adopted ignore rule, resolve #1 and resolve #2 each leave `Packages/.gitignore` **and** `Packages/vpm-manifest.json` byte-identical and the clone's `git status` completely clean. |
| V6 | Bootstrap after resolution | `pwsh -NoProfile -File ./Tools/Bootstrap-NdmfStandalone.ps1` in the isolated clone succeeds, then succeeds again reporting "already bootstrapped". |
| V7 | Configured machine already satisfied | Real worktree, no isolation: `vpm list repos` already reports `dev.nadena.vpm`, so the machine-global stage is skipped by its own guard and **no duplicate repository is added**. Inspecting the real store is **not** a byte-level no-op — see the V7 note under *Observed characterization*. |
| V8 | Unity resolves without the missing-package error | Public project only, identified per AGENTS.md by `Application.dataPath == <repo-root>/Assets` via read-only MCP discovery. Confirm NDMF is present and no missing-package/compile error. Private testbed is never used. |
| V9 | Git cleanliness | `git status` shows only `README.md`, `Packages/.gitignore`, and this design record. No resolved package tracked. No `com.unity.toolchain.*` / `com.unity.sysroot*` entries retained in `Packages/manifest.json` or `packages-lock.json`. |

EditMode tests are **not** run for this milestone: no production C#, test, package metadata,
or assembly definition changes. V8 was intended to cover the only compile-relevant risk (that
the project still opens with NDMF present); it could not be run — see *V8 was not run* below —
so that risk is reported as outstanding rather than treated as cleared. The final diff was
confirmed to touch no compiled file, so the exemption itself still holds; had implementation
touched anything compiled, it would have been void.

### macOS Unity toolchain churn

`Packages/manifest.json` and `packages-lock.json` contain **no** `com.unity.toolchain.*` or
`com.unity.sysroot*` entries at base commit.

**This occurred during the milestone.** A running Unity Editor added
`com.unity.toolchain.macos-arm64-linux-x86_64` to `manifest.json` and `com.unity.sysroot`,
`com.unity.sysroot.linux-x86_64` plus the toolchain entry to `packages-lock.json`. Both
diffs were inspected in full and contained **nothing else**, so a targeted
`git checkout -- Packages/manifest.json Packages/packages-lock.json` was safe and lost no
intentional change; both files are back at HEAD. It may recur while that Editor stays open,
and the same targeted restoration applies — but only after confirming the diff contains
nothing else. Whether those dependencies belong in AMUSE is out of scope.

## Execution checklist

1. Install the **.NET 8 SDK** and the official CLI to an isolated `--tool-path` under the
   scratchpad (avoids mutating the user's global dotnet tools during verification); confirm
   V1 and record both versions.
2. Capture the real-store baseline, then run V2 as a hard gate. Run V3 — capture the
   observed pre-fix failure verbatim.
3. Run V4, V5, V6 in the isolated clone.
4. Write the `README.md` "Development setup" change: prerequisites (.NET 8 SDK, VPM CLI,
   `pwsh`), the once-per-machine repository stage written as a `vpm list repos` check before
   `vpm add repo`, the per-clone restore, the existing bootstrap, then opening Unity. Include
   the authoritative repository URL with its one-line maintainer-provenance rationale, and
   the platform-support wording from section 3a. Keep the existing NDMF bootstrap paragraph
   intact.
5. Run V7 on the real worktree.
6. Run V8 against the public project via read-only MCP discovery.
7. Run V9; inspect unstaged and staged diffs separately.
8. Report observed results, including anything that could not be observed and why.

## Observed characterization

Recorded because these observations qualify claims made above; they were measured, not
predicted.

| Observation | Detail |
| --- | --- |
| Toolchain used | .NET SDK 8.0.424 (runtime 8.0.30), installed to the scratchpad; VPM CLI 0.1.28. The pre-existing .NET 10.0.400 SDK was not used for the documented path. |
| `XDG_DATA_HOME` isolation | Confirmed for the CLI, not merely inferred from binary literals: a fresh store with `userRepos: []` was created under the scratch directory, and the real store was byte-identical afterwards. Reconfirmed on the re-proof run. |
| V7 — inspecting the real store is not read-only | `vpm list repos` run without isolation **normalized and re-cached machine-global repository state**: it rewrote `settings.json` (7296 -> 7162 bytes) and re-fetched every repository cache under `Repos/`. The logical set was preserved — 23 repositories, identical id set, NDMF URL intact, `userProjects` and all other settings keys unchanged — and the stale Windows `localPath` values were replaced with valid macOS cache paths. Accepted as an observed tooling side effect and not restored further. Inspection of the real VPM store must therefore **not** be described as guaranteed read-only or as a strict byte-level no-op. |
| `vpm resolve project` exit code | **Exits 0 even when resolution fails.** The pre-fix run logged `Could not get match for nadena.dev.ndmf 1.14.4` / `Could not resolve package nadena.dev.ndmf 1.14.4` and still returned 0. Success must therefore be judged by the resolved `package.json` version, never by the exit code. The README says so explicitly. |
| `vpm resolve project` and `Packages/.gitignore` | First run on the unmodified file deterministically appended `!com.vrchat.core.*/` and then converged (runs 2 and 3 changed nothing). Diagnosed as VRChat's own VPM source-control rule, not disposable churn, and **adopted into the repository** — see [Adopting the resolver-converged ignore rule](#adopting-the-resolver-converged-ignore-rule). Re-proved afterwards: with the rule present, two consecutive resolves leave the file byte-identical and `git status` empty. |
| Duplicate `vpm add repo` | Documentation states it returns 1 when the repo was not added. **Observed on CLI 0.1.28: exit 0** with `[WRN] You already have a repo with that url`, and the entry count stayed at exactly one. Characterization only — the documented setup does not depend on either value, because the `vpm list repos` check gates the add. |
| Repository identity as served | `vpm list repos` renders the added entry as `dev.nadena.vpm | bd_ (bd_)`, matching the maintainer documentation. |

### V8 was not run

The Unity MCP bridge reported `No Unity Editor instances found` on two consecutive read-only
attempts, and again on a later retry; `set_active_instance` returned `No Unity instances are
currently connected. Start Unity and press 'Start Session'.` The AGENTS.md identity rule —
select the instance whose normalized `Application.dataPath` equals `<repo-root>/Assets` —
therefore could not be evaluated at all.

Two Unity Editors are running on this machine, one on the public project and one on the
private testbed. Without read-only MCP discovery, choosing between them would be a guess, so
the policy requires stopping. No substitute was used: not process paths, not window titles,
not filesystem inspection, and not the private testbed. Launching a second Editor against the
public project was also rejected, because that project is already held by a running Editor.

V8 therefore remains outstanding, and no claim is made about the public project's package or
compile state. It is re-runnable unchanged once a session is started on the bridge from inside
the public project's Editor.

## Expected persistent diff

```
M  README.md
M  Packages/.gitignore
?? docs/superpowers/specs/2026-08-19-reproducible-vpm-setup-design.md
```

Nothing else. `Packages/vpm-manifest.json`, `Packages/manifest.json`,
`Packages/packages-lock.json`, `Packages/com.alrauna.amuse/package.json`,
`Tools/Bootstrap-NdmfStandalone.ps1`, CI, production C#, and tests are all unchanged, and the
NDMF version constraint is untouched at 1.14.4.

## Out of scope

`feat/end-to-end-alpha-analysis`; non-readable alpha evidence; production C#; semantic
architecture; CI; rewriting the NDMF standalone bootstrap; the macOS -> Linux Unity
toolchain churn decision; `.gitattributes`; historical `docs/superpowers/` records; the
private testbed; and any dependency upgrade, NDMF included.
