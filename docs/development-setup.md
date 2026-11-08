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

## Continuous integration

Two workflows run the EditMode suites on GitHub. Both run on hosted Windows runners. A licensed job activates a burner Unity account. The maintainer approves every licensed pull request run before it starts. That approval is the trust boundary of the gate. It means the maintainer read the full change first, including the workflows and the scripts.

### The gate

`pr.yml` runs on every pull request and on every push to `main`. It holds six jobs.

- `scripts` runs the Pester tests of the CI scripts and checks that `scripts/ci/lock.json` matches `Packages/vpm-manifest.json`.
- `cheap` checks the changed lines with `git diff --check` and checks the contents of the release package. The whitespace check skips the YAML files that Unity writes: `.meta`, `.asset`, `.unity`, `.prefab`, `.mat`, `.anim`, and `.controller`.
- `scope` says whether the `unity` job is needed. For a pull request, the job is not needed only when the pull request targets the default branch and every changed path is on a short allow list. The list holds `docs/` files of type `.md` and common image types, `README.md`, `CHANGELOG.md`, and `CONTRIBUTING.md` in the repository root, `.github/dependabot.yml`, `.github/workflows/build-listing.yml`, and `.github/workflows/release.yml`. The list is in `scripts/ci/get-change-scope.ps1`. For a push to `main`, the job is not needed when a merged pull request made the pushed commit. That pull request had its own run, and the rule "require branches to be up to date" means the pushed files equal the tested files. Keep that rule turned on. A direct push to `main` has no such pull request, so it runs the job. A pull request against another branch, an empty change, an unusual path, and any error all mean that the job is needed. A label, a title, or a commit message cannot skip the job.
- `unity` installs the locked Editor, activates the license, installs the locked vendor packages with hash checks, and runs the product and research assemblies as two steps of one job. A download that meets a short network fault runs again, up to three runs in all, with pauses of 5 and 20 seconds. The faults are an HTTP 408, 429, or 5xx answer, a timeout, and a broken connection. An HTTP 4xx answer and a hash mismatch fail at once. The job runs the tests as a local low-privilege user. A probe fails the job when that user can read runner memory or write to a folder that administrator steps run from.
- `fork-notice` turns red when `scope` says the `unity` job is needed and the pull request comes from a fork or from Dependabot. Such a pull request gets no secrets, so the `unity` job skips it. The notice asks the maintainer to read the change and to push it to a branch in this repository. The pull request must then come from that branch. A fork or Dependabot pull request that changes only allow-listed paths needs no Unity run, so it gets no notice.
- `validate` is the required check. It passes only when `scripts`, `cheap`, and `scope` pass and `fork-notice` is skipped. It also needs `unity` to pass, unless `scope` said exactly `false`. In that case `unity` must be skipped.

A pull request can rewrite `pr.yml` or `scripts/ci/get-change-scope.ps1`. The skip decision runs the code of the pull request, as the rest of `validate` does. A pull request that rewrites either file can force a skip, so a change to them does not always need a Unity run. The review of changes to `.github/` and `scripts/ci/` is the only barrier. Settings outside the repository can add a second barrier. Examples are a required review and a code owner rule for those paths.

### The weekly canary

`canary.yml` runs once a week and on a manual start. It floats every vendor package to its newest stable release. Unity stays at the version in `ProjectSettings/ProjectVersion.txt`. It skips releases that are younger than 48 hours when the publisher shows a release date. It never gates a merge, holds no write permission, and changes nothing in the repository. A red run is the signal that a vendor moved. The job summary holds one table row per package. The columns are package, old, new, date source, and skipped younger.

The licensed jobs of both workflows share one concurrency group. They queue in order instead of cancelling a waiting run. The gate and the canary never sign in to the one account at the same time.

### The local gate

`scripts/verify.ps1` runs the same checks on this machine, in a temporary clone of the committed state. Uncommitted changes are not in the clone. The script prints a warning only when the working tree holds uncommitted changes under `scripts/`, `.github/`, `Packages/com.alrauna.amuse/`, or in `Packages/vpm-manifest.json`. A full run does five steps: the lock check, the release exclusion check, the vendor package install, and the two test assemblies. `-CheckOnly` runs the two checks only. No download starts and no Editor starts. The local gate uses the license of the local Unity Hub. It creates no user and touches no license.

### Bump the package lock

1. Change the locked versions in `Packages/vpm-manifest.json`.
2. Run `scripts/ci/new-lock.ps1 -Out scripts/ci/lock.json`. The script needs the network.
3. Read the printed diff. Every package takes its hash from its publisher listing or its GitHub release digest. Only VRCFury has no hash route.
4. Commit `Packages/vpm-manifest.json` and `scripts/ci/lock.json` together.

The gate fails when the manifest and the lock disagree. `scripts/ci/check-lock.ps1` prints each difference.

### Rotate the Unity half of the lock

1. Change `ProjectSettings/ProjectVersion.txt` to the new version.
2. Run `scripts/ci/new-lock.ps1`. The script prints the installer SHA-256 and the signer subject.
3. Compare both values with a second source. Use the Unity release page for the hash. Check the installer signature on the maintainer machine for the subject.
4. Edit `scripts/ci/lock.json` with the confirmed values.
5. Commit `ProjectSettings/ProjectVersion.txt` and `scripts/ci/lock.json` together.

The lock stores the same subject for the installer and for `licensingClientSubject`. The reason is simple. The licensing client ships inside the installed Editor. The lock builder downloads the installer only, so it cannot read the client subject. A mismatch on the first run prints the observed subject. Confirm it against a signature check and update the lock.

### Human setup steps

The maintainer does these once. The spec section 10 holds the full form.

1. Prepare the burner Unity account. Give it a unique password of 20 or more characters with no quote and no space. Turn two-factor sign-in off. Give the account its own mailbox. Set the account up on a clean machine. Add no payment detail, no asset, and no phone number that you share with your main accounts.
2. Sign in to the Unity Hub once with that account on that clean machine. Activate Personal. Sign out. The account then holds an entitlement.
3. Create the Environments `unity-gate`, `unity-main`, and `unity-canary`. Set the allowed refs and the reviewers as the spec describes. Turn "Allow administrators to bypass" off on all three. Turn "prevent self-review" off on `unity-gate`.
4. Add `UNITY_EMAIL` and `UNITY_PASSWORD` to all three Environments with the one account. Delete the old `UNITY_LICENSE` secrets.
5. Set the repository settings. Require full SHA pinning for actions with an allowlist. Require approval for all outside collaborators. Set the default token permission to read-only.
6. Limit the `release` Environment to `main`. Set a tag ruleset that blocks tag updates and tag deletions. Tag creation stays allowed.
7. Check that no rule merges Dependabot pull requests on its own. A pull request that changes only allow-listed paths can reach a green `validate` with no approval of the `unity-gate` Environment. Read such a pull request before you merge it. Branch protection of `main` requires no review as of 2026-11-08 (UTC), by ruling O4 of the spec.
