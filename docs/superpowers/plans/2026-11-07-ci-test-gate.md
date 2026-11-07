# CI test gate and weekly canary implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `validate` becomes a real required check that runs both EditMode assemblies on a hosted Windows runner. A weekly canary runs the same suites against the newest vendor releases.

**Architecture:** Own PowerShell scripts under `scripts/ci/` install Unity and the vendor packages, activate a burner account, and run the tests as a non-admin local user. Workflows call the scripts and use `actions/checkout` only. A committed lock file holds every hash of the gate lane.

**Tech Stack:** GitHub Actions on `windows-2022`, PowerShell 7, Pester 5, Unity 2022.3.22f1.

**Spec:** `docs/superpowers/specs/2026-11-06-ci-test-gate-design.md`. Read it first. Section 7 holds the spike runs 1 to 12. Run 12 passed on the final pins.

## Global Constraints

- Every reference to `actions/checkout` uses the full SHA `3d3c42e5aac5ba805825da76410c181273ba90b1` and `persist-credentials: false`. No other action appears in a new workflow.
- Every new workflow sets `permissions: {}` at the top, `cache-mode: none` at the top, and a job asks for `contents: read` only.
- No `pull_request_target`. No `workflow_run`. No artifact upload. No cache.
- The job name `validate` does not change. Branch protection requires it.
- The runner image is `windows-2022`. Every job and every Unity step has `timeout-minutes`.
- A fork PR and a Dependabot PR do not start a Unity job. The `if` of the `unity` job is `(github.event_name != 'pull_request' || github.event.pull_request.head.repo.id == github.repository_id) && github.actor != 'dependabot[bot]'`. Dependabot branches live in this repo and pass the first half. The `if` is a convenience, because a fork PR can rewrite `pr.yml`. What holds is that the Environment approval comes before a job reaches a runner, and that GitHub withholds secrets from fork runs and from Dependabot runs.
- One shared concurrency group `unity-license` with `cancel-in-progress: false` sits on every job that activates a license, in both workflows. The gate and the canary then never sign in to the one account at the same time. The per-ref group with cancel stays on the cheap jobs only.
- Step timeouts must sum to less than the job timeout, so the license return step always gets its turn.
- The lock files, the check scripts, `pr.yml` and the other workflow files are covered by review and not by enforcement. A PR can edit them. The approval of `unity-gate` is the trust boundary (spec D2 and section 11). `validate` is not a trust boundary.
- Environments: `unity-gate` for pull requests, `unity-main` for pushes to `main`, `unity-canary` for the schedule and manual runs. Secrets are `UNITY_EMAIL` and `UNITY_PASSWORD`, and only the activation step sees them.
- Public logs: scripts print counters and redacted allowlisted lines only. They never print a raw Unity log, a raw licensing client output, an email, a password, or a serial.
- No untrusted text (branch name, PR title) in a `run:` body. Pass it through an environment variable.
- Opt-in fixtures `AMUSE_DAO_INTEGRATION`, `AMUSE_REAL_THRY_LOCK`, `AMUSE_PRODUCTION_ORDER_DISPATCH` stay unset.
- Text that a human reads uses simple English. No machine paths, host names, user names, ports, GUIDs, or instance names in any file.
- Never commit without the maintainer saying so. Stage explicit paths. The three `ProjectSettings` files that show as modified belong to the maintainer. Never stage them.
- Every printed string that can hold text from a test, a vendor or the Editor passes through `Redact` first. This includes failing test names.

## Decisions this plan adds

- `[CALL]` One Unity job per lane. It installs Unity once, activates once, and runs the product assembly and then the research assembly as separate steps. Reason: spec D6 allows it, and spec section 11 warns that parallel sign-ins on one account can be throttled. Concurrency of two editors on one license stays untested.
- `[CALL]` The canary uses the committed Unity installer hash. Only vendor packages float.
- `[CALL]` The isolation probe from the spike stays in every licensed run as a guard. The run fails when the low user can read runner memory or write to an admin-run place. The license folder is not such a place. The low user needs write access there, so the probe lists it as an allowed write.
- `[CALL]` Scripts are tested with Pester 5. A job without secrets runs those tests on `windows-2022`. `[INFERENCE]` The image has Pester 5. Task 1 checks it.
- `[CALL]` Admin mode of the spike is dropped. Every licensed run uses the low user.
- `[CALL]` The graphics device probe of the spike is dropped. The capture tests prove the D3D11 device and the R8_UNorm readback.
- `[SOURCE]` The `cache-mode: none` edits and `environment: release` of spec D12 are already on this branch. They are not part of this plan. The three repository settings of spec D3 are maintainer steps in Task 13.
- The spike scripts in `scripts/ci/spike.ps1` are the source of working code. Move the code, keep its behavior, and delete the spike in Task 12.

## Review Focus

Each line names an input that the spec implies. The owning task pins it with a test.

1. A manifest bump with no matching lock edit. Expected: `validate` fails before any Unity job starts, because `unity` needs `scripts`. Task 2 and Task 8.
2. A test run that reports zero tests, a missing result file, or a missing assembly. Expected: red, never green. Task 6.
3. A vendor package that needs a dependency outside the lock. Expected: fail closed and name the package. Task 3.
4. A password that ends in a backslash or holds a quote. Expected: backslash doubled, quote refused before any process starts. Task 1.
5. A skipped Unity job. Expected: `validate` fails. Task 8.
6. A canary release that is younger than 48 hours. Expected: skipped, and the summary says so. Task 9.

---

## File structure

| Path | Responsibility |
|---|---|
| `scripts/ci/lib/Common.ps1` | Pure helpers: output, redaction, credential rules, result checks |
| `scripts/ci/lib/Isolation.ps1` | Low-user setup, deny rules, environment, process helpers, cleanup |
| `scripts/ci/lock.json` | Committed hashes of the gate lane: installer, signer subject, each vendor zip |
| `scripts/ci/sources.json` | One VPM source URL per vendor and one source per package |
| `scripts/ci/check-lock.ps1` | Compares `lock.json` with the locked block. No network |
| `scripts/ci/new-lock.ps1` | Human run. Builds `lock.json` from the publishers and prints a diff |
| `scripts/ci/install-unity.ps1` | Download, verify, install the Editor |
| `scripts/ci/install-packages.ps1` | Install vendor packages in `gate` or `canary` mode |
| `scripts/ci/resolve-canary.ps1` | Pick the newest stable version per package, with the cooldown |
| `scripts/ci/license.ps1` | Activate and return the license |
| `scripts/ci/run-tests.ps1` | One Unity test run, result checks, summary |
| `scripts/ci/isolation-probe.ps1` | The guard from the spike probe, without the graphics part |
| `scripts/ci/prepare-run.ps1` | Admin script copy, low user, folder rights, deny rules |
| `scripts/ci/prepare-project.ps1` | Private project copy for the low user and the Package Manager profile folders |
| `scripts/ci/check-release-exclusion.ps1` | Builds the release zip and checks that no test content is inside |
| `scripts/ci/tests/*.Tests.ps1` | Pester tests, one file per script or library |
| `scripts/verify.ps1` | Local gate in a temporary clone |
| `.github/workflows/pr.yml` | Gate. Jobs `scripts`, `cheap`, `unity`, `validate` |
| `.github/workflows/canary.yml` | Weekly canary |
| `.github/dependabot.yml` | `github-actions` updates |

---

### Task 1: Common library and the Pester harness

**Files:**
- Create: `scripts/ci/lib/Common.ps1`, `scripts/ci/tests/Common.Tests.ps1`

**Interfaces:**
- Produces, all in `Common.ps1` and dot-sourced by every script:
  - `Say([string]$text)` writes to the host and to `$env:GITHUB_STEP_SUMMARY` when set.
  - `Redact([string]$line) -> [string]` same rules as the spike.
  - `Test-UnityCredential([string]$email, [string]$password)` throws on an empty value, an email outside the plain shape, or a password with a quote or whitespace. Trims both first. Returns a pair `@{Email; Password}`.
  - `ConvertTo-LicensingArguments([string]$email, [string]$password) -> [string]` returns the quoted client argument string. Doubles trailing backslashes.
  - `Read-NUnitResult([string]$xmlPath) -> pscustomobject` with `Total, Passed, Failed, Skipped, Inconclusive, FailedNames, Assemblies`. `Assemblies` holds the `name` attribute of each `test-suite` element whose `type` is `Assembly`, without the `.dll` suffix. Throws when the file is missing.
  - `Assert-TestRun($result, [string]$assembly, [int]$unityExit)` throws on zero tests, an assembly that is not in `Assemblies`, any failure, any inconclusive test, fewer than one passed test, or a non-zero exit.

- [ ] **Step 1: Check Pester.** Run `pwsh -c "(Get-Module -ListAvailable Pester | Sort-Object Version -Descending | Select-Object -First 1).Version"`. Expected: 5.x. If it is older, run `Install-Module Pester -MinimumVersion 5.0 -Scope CurrentUser` after the maintainer agrees.
- [ ] **Step 2: Write failing tests** in `Common.Tests.ps1`. Test names: `RedactMasksEmailSerialGuidAndLongTokens`, `RedactSplitsDoubleColons`, `FailedTestNameWithDoubleColonIsNeverPrintedRaw`, `CredentialRejectsQuoteAndWhitespaceInPassword`, `CredentialRejectsEmailWithoutDomain`, `CredentialTrimsBothValues`, `ArgumentsDoubleTrailingBackslash` (input `abc\\` gives `abc\\\\` inside the quotes), `ArgumentsKeepInnerBackslashes`, `ReadNUnitResultExtractsAssemblyNames`, `ReadNUnitResultThrowsOnMissingFile`, `AssertTestRunRejectsZeroTests`, `AssertTestRunRejectsMissingAssembly`, `AssertTestRunRejectsOneFailure`, `AssertTestRunRejectsInconclusiveTest`, `AssertTestRunRejectsAllSkipped`, `AssertTestRunRejectsNonZeroExitWithAllPassing`. Build XML fixtures inline in `BeforeAll`.
- [ ] **Step 3: Run** `Invoke-Pester scripts/ci/tests/Common.Tests.ps1 -Output Detailed`. Expected: all FAIL (functions missing).
- [ ] **Step 4: Move and adapt** the helpers from `scripts/ci/spike.ps1` into `Common.ps1`. `Assert-TestRun` is new. `Test-UnityCredential` replaces the inline checks of the `ActivateLicense` phase.
- [ ] **Step 5: Run** the same command. Expected: all PASS.
- [ ] **Step 6: Commit** with the maintainer's word. Stage `scripts/ci/lib/Common.ps1` and `scripts/ci/tests/Common.Tests.ps1`.

### Task 2: Lock file, sources, and the lock check

**Files:**
- Create: `scripts/ci/lock.json`, `scripts/ci/sources.json`, `scripts/ci/check-lock.ps1`, `scripts/ci/new-lock.ps1`, `scripts/ci/tests/Lock.Tests.ps1`

**Interfaces:**
- `lock.json` shape: `{ "unity": { "version", "installerSha256", "signerSubject" }, "licensingClientSubject", "packages": { "<name>": { "version", "sha256" | null, "unhashedReason" } } }`. `sha256` is null only when `unhashedReason` is set. Only `com.vrcfury.vrcfury` may be null (spec O3).
- `sources.json` shape: `{ "sources": { "<key>": "<listing url>" }, "packages": { "<name>": "<key>" } }`. The values are the nine sources and the twelve names that the spike holds in its `InstallPackages` phase.
- Produces `Test-LockMatchesManifest([string]$lockPath, [string]$manifestPath, [string]$versionFilePath, [string]$sourcesPath) -> string[]` (list of differences, empty when equal). Compares every locked name and version, the Unity version, the null rule, and that every locked name maps to a source key that exists in `sources.json`.
- `check-lock.ps1` runs that function on the repo files. It exits 1 and prints each difference.
- `new-lock.ps1 -Out <path>` has two parts. The package part reads the locked block, downloads each zip from its listing, takes the hash from the listing or the GitHub digest or computes it, and writes the file. A computed hash is allowed only for a package that has no publisher hash, and the script then prints it. The Unity part downloads the installer for the version in `ProjectVersion.txt`, cross-checks the md5 with the release API, computes the SHA-256, and prints the observed signer subject of the installer and of the licensing client for the maintainer to confirm. It prints a diff against the committed lock. It never writes `lock.json` unless `-Out` names it.

- [ ] **Step 1: Write failing tests** in `Lock.Tests.ps1`: `LockMatchingManifestHasNoDifferences`, `ManifestVersionBumpWithoutLockEditIsReported` (review focus 1), `PackageMissingFromLockIsReported`, `PackageMissingFromManifestIsReported`, `PackageWithoutSourceKeyIsReported`, `NullHashWithoutReasonIsReported`, `NullHashOnAnotherPackageThanVrcfuryIsReported`, `UnityVersionMismatchIsReported`. Use temp files.
- [ ] **Step 2: Run** `Invoke-Pester scripts/ci/tests/Lock.Tests.ps1`. Expected: FAIL.
- [ ] **Step 3: Implement** `Test-LockMatchesManifest` in `check-lock.ps1` (dot-sourceable, runs only when invoked directly) and the two JSON files.
- [ ] **Step 4: Generate the first lock.** Run `scripts/ci/new-lock.ps1 -Out scripts/ci/lock.json`. Expected Unity values for 2022.3.22f1 are in spec section 7 (the line headed "Pinned values"). The script must print the same installer SHA-256 and the same signer subject. Any difference stops the task. Read the diff by eye. Expected: 11 packages hashed, VRCFury null with the reason `no hash route`.
- [ ] **Step 5: Run** the tests and `./scripts/ci/check-lock.ps1`. Expected: PASS and exit 0.
- [ ] **Step 6: Commit** with the maintainer's word.

### Task 3: Install packages

**Files:**
- Create: `scripts/ci/install-packages.ps1`, `scripts/ci/tests/InstallPackages.Tests.ps1`

**Interfaces:**
- `install-packages.ps1 -Mode gate|canary [-PlanPath <path>] [-ProjectPath <path>]`. `-ProjectPath` defaults to the repo root. The local gate passes a temporary clone. In `gate` mode the expected hash comes from `lock.json` only. In `canary` mode `-PlanPath` names the output of Task 9 and the hash comes from the publisher at run time.
- Functions, dot-sourceable: `Resolve-ListingEntry($listing, [string]$name, [string]$version) -> entry or $null`, `Get-PublisherHash($entry) -> @{Hash; Route}`, `Assert-ZipHash([string]$path, [string]$expected, [string]$name)` throws on mismatch, `Find-MissingDependencies([string]$packagesDir, [string[]]$allowedNames) -> string[]`, `Edit-UnityManifest([string]$path)` removes the Unity MCP git entry and `com.unity.toolchain.*`.
- A package with no hash and no `unhashedReason` stops the job. A package that is not in its own named listing stops the job. In `gate` mode a dependency outside the lock stops the job and names the package (review focus 3). In `canary` mode the allowed names are the names in the plan file, and a dependency outside the plan stops the job.
- A package with a null hash prints its observed SHA-256 to the log, so the log keeps the VRCFury hash (spec O3).

- [ ] **Step 1: Write failing tests**: `HashMismatchThrowsAndNamesThePackage`, `PackageNotInItsOwnNamedListingStops`, `GateModeRejectsDependencyOutsideTheLock`, `CanaryModeAllowsDependencyThatIsInThePlan`, `UnhashedPackageWithoutReasonStops`, `NullHashPackagePrintsObservedHash`, `ManifestEditRemovesMcpAndToolchainOnly`, `PublisherHashPrefersListingOverGithubDigest`. Fixtures: small listing objects and a zip made in a temp folder.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Move and adapt** the `InstallPackages` phase of the spike. In `gate` mode do not read the listing for a hash at all. Still read the listing for the URL. `[LIMIT]` A vendor that removes the locked version from its listing turns the gate red until the lock moves. The hash check still holds.
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with the maintainer's word.

### Task 4: Install Unity

**Files:**
- Create: `scripts/ci/install-unity.ps1`, `scripts/ci/tests/InstallUnity.Tests.ps1`

**Interfaces:**
- `Get-UnityDownload([string]$version) -> @{Url; Md5}` (release API, host must start with `https://download.unity3d.com/`).
- `Assert-UnityInstaller([string]$path, $lock, [string]$expectedMd5)` throws unless the SHA-256 equals `lock.unity.installerSha256`, the md5 equals the API value, the Authenticode status is `Valid`, and the subject equals `lock.unity.signerSubject` exactly. The check runs before the installer starts. On a mismatch it prints the observed SHA-256 and subject (both are public values), so the maintainer can start the rotation in Task 12 step 2.

- [ ] **Step 1: Write failing tests** with a temp file and a mocked `Get-AuthenticodeSignature`: `WrongSha256Throws`, `WrongMd5Throws`, `SubstringOfSubjectIsNotEnough`, `InvalidSignatureThrows`, `ForeignHostUrlThrows`.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement** from the `InstallUnity` phase. Install to `$env:RUNNER_TEMP\UnityEditor`, silent, then delete the installer.
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with the maintainer's word.

### Task 5: Isolation, license, and the isolation probe

**Files:**
- Create: `scripts/ci/lib/Isolation.ps1`, `scripts/ci/license.ps1`, `scripts/ci/isolation-probe.ps1`, `scripts/ci/tests/Isolation.Tests.ps1`

**Interfaces:**
- `Isolation.ps1` holds the spike code for mode `lowuser` only. Functions: `New-LowUser` (writes a random password into a file that only Administrators can read, and returns nothing), `Get-LowCredential() -> PSCredential` (reads that file), `Grant-LowAccess()` (Modify on the `amuse-low` folder and the license folder, Read and Execute on the Editor install), `Add-DenyWrite([string]$path, [string]$label)` (named rights, no synchronize), `Get-LowEnvironment() -> hashtable` (profile variables repointed, `GITHUB*`, `RUNNER*`, `ACTIONS*`, `ImageOS`, `ImageVersion` blanked), `Start-AsLowUser([string]$file, [string]$arguments, [string]$stdoutPath, [string]$stderrPath, [int]$timeoutMs) -> @{Finished; ExitCode}`, `Stop-LowUserProcesses`, `Remove-LowUser`, `Copy-AdminScripts()`.
- `prepare-run.ps1` is the orchestrator of the step after the Unity install. It calls `Copy-AdminScripts`, `New-LowUser`, `Grant-LowAccess`, and `Add-DenyWrite` on the agent folder and the runner folder. `prepare-project.ps1` copies the repo to the private project folder with `robocopy` (exit code 8 or more fails) and creates the profile folders of the Package Manager. Both run in the admin copy.
- `license.ps1 -Action Activate|Return`. `Activate` reads `UNITY_EMAIL` and `UNITY_PASSWORD`, clears them, calls `Test-UnityCredential`, checks that the client sits at the expected path, is validly signed, and has the exact subject in `lock.licensingClientSubject`, and runs the client as the low user. After it exits, it measures where the credentials still appear (process command lines, Unity data folders, the profile, the out folder) and prints counts only. The measure is the `Measure-CredentialResidue` code of the spike. It prints counts only.
- `isolation-probe.ps1` runs as the low user and exits 1 when the low user opens any runner process memory, or writes to any admin-run place that the spike listed. The license folder is the one allowed write. It prints counts and yes or no answers only.

- [ ] **Step 1: Write failing tests** for the pure parts only: `LowEnvironmentBlanksGithubAndRunnerVariables`, `LowEnvironmentRepointsProfileVariablesToTheLowProfile`, `DenyRuleNamesNoSynchronizeRight` (build the rule and assert its rights), `ProbeTargetListHoldsTheLicenseFolderAsAnAllowedWrite`, `ProbeVerdictFailsWhenAnyNonAllowedPathIsWritable`, `ProbeVerdictFailsWhenAnyRunnerProcessIsReadable`. Split the probe verdict into a pure function `Get-IsolationVerdict($facts) -> string[]`.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Move and adapt** the spike code. Do not add the `(NP)` grants. Keep the admin-only script copy and the private project copy in a folder named `amuse-low` on the system drive.
- [ ] **Step 4: Run** the Pester tests. Expected: PASS. The live behavior is proved in Task 8, because no local run can apply these rights safely. Never run `Copy-AdminScripts` or the deny rules on a developer machine.
- [ ] **Step 5: Commit** with the maintainer's word.

### Task 6: Test runner

**Files:**
- Create: `scripts/ci/run-tests.ps1`, `scripts/ci/tests/RunTests.Tests.ps1`

**Interfaces:**
- `run-tests.ps1 -Assembly <name> [-ProjectPath <path>] [-AsLowUser]` runs the Editor with `-runTests -testPlatform EditMode -assemblyNames <name> -force-d3d11 -testResults <path>`. It runs as the low user when `-AsLowUser` is set. The local gate leaves it off. It never passes `-nographics`. It prints the counters, the name of each failing test (first 40, through `Redact`), and the shader and compile error counts.
- `Complete-TestRun([string]$xmlPath, [string]$assembly, [int]$unityExit, $logFacts)` lives in `Common.ps1`. It reads the result, prints the counters, calls `Assert-TestRun`, and throws on any shader or compile error count. `run-tests.ps1` calls it and nothing else for the verdict.
- `Get-LogFacts([string]$logPath) -> @{Shader; Compile; Licensing}` lives in `Common.ps1`. Add its tests here.

- [ ] **Step 1: Write failing tests**: `CountsShaderErrorLines`, `CountsCompileErrorLines`, `IgnoresBenignLicensingClientSignatureLine`, `CompleteTestRunThrowsWhenTheResultFileIsMissing`, `CompleteTestRunThrowsOnShaderErrorLinesWithAllTestsPassing`, `CompleteTestRunPrintsFailedNamesThroughRedact` (review focus 2).
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement** from `Invoke-TestRun` in the spike. The log is deleted after the facts are read.
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with the maintainer's word.

### Task 7: Release exclusion check

**Files:**
- Create: `scripts/ci/check-release-exclusion.ps1`, `scripts/ci/tests/ReleaseExclusion.Tests.ps1`

**Interfaces:**
- `Test-ReleaseZip([string[]]$entries) -> string[]` returns violations. Two rules of the `Verify Release Excludes Tests` step of `release.yml` apply to a zip: no entry that matches `(^|/)Tests(/|\.meta$)`, and the entry `Editor/Alrauna.Amuse.Editor.asmdef` is present. The third rule of that step (the `.meta` list of the `.unitypackage`) does not apply, because the gate makes no `.unitypackage`. Normalize every entry to forward slashes before the check.
- The script builds the zip with `System.IO.Compression` from `Packages/com.alrauna.amuse`, leaves out `Tests/` and `Tests.meta` the same way `release.yml` does, lists the entries, and calls the function. It writes no file outside the runner temp folder. The package name is a constant in the script. `release.yml` stays untouched (spec N1).

- [ ] **Step 1: Write failing tests**: `TestsFolderEntryIsAViolation`, `TestsMetaEntryIsAViolation`, `CleanListPasses`, `MissingEditorAsmdefIsAViolation`, `NestedTestsFolderIsAViolation`, `BackslashSeparatedTestsEntryIsAViolation`.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS. Also run the script on the real tree. Expected: exit 0.
- [ ] **Step 5: Commit** with the maintainer's word.

### Task 8: Gate workflow

**Files:**
- Modify: `.github/workflows/pr.yml`

**Interfaces:**
- Triggers: `pull_request` and `push` to `main`. No `workflow_dispatch`: an Environment ref rule rejects a dispatch from a branch that it does not list. The cheap jobs use the group `pr-${{ github.ref }}` with `cancel-in-progress: ${{ github.ref != 'refs/heads/main' }}`.
- Jobs (all `windows-2022` except where noted):
  - `scripts`: checkout, run Pester on `scripts/ci/tests`, run `check-lock.ps1`. No secrets.
  - `cheap`: checkout with `fetch-depth: 0`. On `pull_request`, run `git diff --check` between the base ref (passed through an env var) and `HEAD`. On `push`, run `git diff --check` between `github.event.before` and `HEAD` (through an env var). Then run `check-release-exclusion.ps1`. No secrets.
  - `unity`: `needs: [scripts, cheap]`, so a lock drift stops the run before the licensed job. `if` is the condition of Global Constraints. `environment` is `${{ github.event_name == 'push' && 'unity-main' || 'unity-gate' }}`. The job sits in the concurrency group `unity-license` with `cancel-in-progress: false`. Steps in this order: checkout, `install-unity.ps1`, `prepare-run.ps1`, `license.ps1 -Action Activate` (the only step with the secrets), `install-packages.ps1 -Mode gate`, `prepare-project.ps1`, `isolation-probe.ps1` (no `continue-on-error`), `run-tests.ps1 -AsLowUser -Assembly Alrauna.Amuse.Tests.Editor`, `run-tests.ps1 -AsLowUser -Assembly Alrauna.Amuse.Research.Tests.Editor`, `license.ps1 -Action Return` with `if: always()` and its own timeout, cleanup with `if: always()` and its own timeout. Every step after `prepare-run.ps1` runs the admin copy of the scripts. `if: always()` is right for the return step, because a cancelled run must still free the activation.
  - `fork-notice`: `if` is `github.event_name == 'pull_request' && (github.event.pull_request.head.repo.id != github.repository_id || github.actor == 'dependabot[bot]')`. It fails with a message that tells the maintainer to push the change to an in-repo branch.
  - `validate`: `needs` every job above, `if: always()`, `ubuntu-latest`. A shell step reads the results from an env var as JSON. It passes only when `scripts`, `cheap` and `unity` are `success` and `fork-notice` is `skipped`. Any other result fails `validate`. On a fork PR, `unity` is skipped and `fork-notice` fails, so `validate` is red. GitHub counts a skipped job as a passing check, so this shell step is what closes the skipped path.
- The old placeholder comment is removed. The job name stays `validate`.

- [ ] **Step 1: Write the workflow** with the jobs above. Step timeouts, from the spike runs with margin: install 20, prepare 10, activation 20, package install 30, project prepare 15, probe 10, each test step 60, return 10, cleanup 10. Their sum is 245. The job timeout is 300.
- [ ] **Step 2: Review the file** against Global Constraints. Check each line of the list by reading the file.
- [ ] **Step 3: Smoke run.** With the maintainer's word, push the branch and open a draft PR into `main`. Approve the `unity-gate` run. Expected: `scripts`, `cheap`, `unity` and `validate` all green, and `fork-notice` skipped. Read the public log for an email, a JWT-shaped token and a serial shape. Expected: none.
- [ ] **Step 4: Red proof (review focus 5).** On throwaway branches that are never merged: (a) break one product test on purpose. Expected: `unity` red and `validate` red. (b) Add `if: false` to `unity`. Expected: `validate` red. (c) Edit `lock.json` so that one version differs from the manifest. Expected: `scripts` red, `unity` skipped, `validate` red. (d) Make the isolation probe fail on purpose, for example by granting the low user write access to the admin script copy. Expected: `unity` red and `validate` red. Delete the branches after the maintainer agrees.
- [ ] **Step 5: Commit** with the maintainer's word.

### Task 9: Canary

**Files:**
- Create: `.github/workflows/canary.yml`, `scripts/ci/resolve-canary.ps1`, `scripts/ci/tests/ResolveCanary.Tests.ps1`

**Interfaces:**
- `Select-CanaryVersion($versions, [datetimeoffset]$now, [int]$cooldownHours = 48, $releaseDates) -> @{Version; Skipped; DateSource}` returns the highest stable version. Compare numbers per segment, never as text. Prereleases (a suffix after a dash) are ignored. A version that is younger than the cooldown is skipped and named. A source with no release date has no cooldown, and `DateSource` is `none`, and the summary says so.
- Date routes, one per package, decided in the first step: the listing `date` or `published` field where the listing has one, and the GitHub release `published_at` where the url is a GitHub release. The first step of the task prints which routes exist for the twelve packages. The plan file records the route per package.
- `resolve-canary.ps1` writes a plan file: for each package `name`, `old`, `new`, `url`, `dateSource`, `skippedYounger`. It rewrites the `locked` block of `Packages/vpm-manifest.json` in the checkout so `VendorSuiteStartCheck` passes. It fails closed when a new transitive dependency is outside the plan.
- The workflow: `schedule` weekly at a low-traffic hour and `workflow_dispatch`. `environment: unity-canary`. Same job steps as `unity` except `install-packages.ps1 -Mode canary -PlanPath`. A job summary table `name old -> new`. No write permission. The job sits in the shared `unity-license` concurrency group and never cancels.

- [ ] **Step 1: Find the date routes.** Read the twelve listings and the GitHub release data once, and write down which package has which date field. Put the result in the plan file format and in the test fixtures. If a route needs a network call that gives no date, the summary says `none` for that package.
- [ ] **Step 1b: Write failing tests**: `PicksHighestStableVersion`, `TenDotZeroDotTwentyNineBeatsTwoDotFourDotOne`, `OneDot1442BeatsOneDot999`, `PrereleaseIsIgnored` (`10.0.30-beta` against `10.0.29`), `VersionYoungerThan48HoursIsSkippedAndNamed` (review focus 6), `VersionWithoutReleaseDateIsKeptAndFlagged`, `ListingKeyOrderDoesNotChangeTheChoice`, `NewTransitiveDependencyOutsideThePlanFailsClosed`, `LockedBlockRewriteMatchesInstalledVersions`.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement** the script and the workflow.
- [ ] **Step 4: Run** the tests. Expected: PASS.
- [ ] **Step 5: Smoke run.** The workflow file must reach `main` before `workflow_dispatch` works. After the merge, the maintainer starts one manual run. Expected: green or a red run that names the packages that moved. Record the result in the spec.
- [ ] **Step 6: Commit** with the maintainer's word.

### Task 10: Local gate

**Files:**
- Create: `scripts/verify.ps1`

**Interfaces:**
- `scripts/verify.ps1 [-UnityPath <path>]`. Clones the repo to a temporary folder, runs `check-lock.ps1`, `install-packages.ps1 -Mode gate -ProjectPath <clone>`, `run-tests.ps1 -ProjectPath <clone>` for both assemblies with the local Unity license and without `-AsLowUser`, and the release exclusion check. It looks for Unity under the Hub install folder for the version in `ProjectVersion.txt`, then falls back to `-UnityPath`. It does not create a low user and it does not touch the license.

- [ ] **Step 1: Write the script.** Reuse the lib functions. Do not print private data.
- [ ] **Step 2: Smoke run** on the maintainer machine with the maintainer's word, in a temporary clone only. Expected: both assemblies pass.
- [ ] **Step 3: Commit** with the maintainer's word.

### Task 11: Dependabot

**Files:**
- Create: `.github/dependabot.yml`

- [ ] **Step 1: Write** an `updates` entry with ecosystem `github-actions`, directory `/`, weekly. The `unity` job condition leaves out Dependabot PRs. They get the `fork-notice` job and no secrets.
- [ ] **Step 2: Commit** with the maintainer's word.

### Task 12: Remove the spike and write the records

**Files:**
- Delete: `.github/workflows/ci-spike.yml`, `scripts/ci/spike.ps1`, `scripts/ci/spike/SpikeProbe.cs`
- Create: `docs/superpowers/investigations/2026-11-07-ci-spike-record.md`
- Modify: `docs/development-setup.md`, `docs/superpowers/specs/2026-11-06-ci-test-gate-design.md`

- [ ] **Step 1: Write the investigation note.** It opens with a privacy note. It records runs 1 to 12 as observed counts and times, the root causes (the licensing file is machine-bound, the `net user /y` switch, the synchronize right in the deny rule), and the results of S1 to S11. No hash, GUID, email or machine name except the public Unity installer hash, the public signer subject and the VRCFury hash that are already in the spec.
- [ ] **Step 2: Add a CI section** to `docs/development-setup.md`: what the gate and the canary do, `scripts/verify.ps1`, how to bump the package lock (`new-lock.ps1`, then read the diff), the human steps of spec section 10, and how to rotate the Unity half of the lock. The Unity rotation is: change `ProjectVersion.txt`, run `new-lock.ps1`, compare the printed installer SHA-256 and subject with a second source (the Unity release page and a signature check on a machine of the maintainer), then edit `lock.json` and commit both files together.
- [ ] **Step 3: Add known limits** to the spec section 11: VRCFury has no hash route, Unity registry packages are not hash-locked, the runner image can change under the fixed label, the fork-route `if` was not tested from a real fork, the licensing client can change signer over time and then needs a lock edit, and a vendor can remove a locked version from its listing.
- [ ] **Step 4: Delete the spike files.** Check with a search that nothing refers to them.
- [ ] **Step 5: Sweep** the changed files for an at sign joined to hexadecimal, a drive-letter path, a home-directory path, a four-digit port, a private asset name. Fix every hit.
- [ ] **Step 6: Commit** with the maintainer's word.

### Task 13: Merge and switch on

- [ ] **Step 1:** The maintainer reviews the full diff, including `.github/` and `scripts/`, and approves the draft PR run.
- [ ] **Step 2:** After the merge, the maintainer confirms the `release` Environment allows `main` only, the tag ruleset is Active, and the three repository settings of spec D3 are on (full SHA pinning with the allowlist, approval for all outside collaborators, read-only default token).
- [ ] **Step 3:** Start the canary once by hand (Task 9 step 5). Record the result.
- [ ] **Step 4:** Close the items in spec section 12 that this plan settled, and date each change.
