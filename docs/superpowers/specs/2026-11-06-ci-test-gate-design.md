# CI test gate and weekly canary — design

Date: 2026-11-06. Basis: a grilling session between the controller and the user.
Labels: `[SOURCE]` is a fact read in this tree. `[DECISION]` is a choice the user made.
`[CALL]` is a choice the author made. The user did not rule on it. `[LIMIT]` is a known limit.
`[UNVERIFIED]` is a belief that the spike must prove before the design relies on it.

This record speaks for its date. It does not authorize code. The implementation plan comes next.

## 1. Intent

As of 2026-11-06, no workflow runs the AMUSE tests. `pr.yml` is a placeholder that always passes.
A change can merge with a broken suite. The goal is a gate that runs the real EditMode suites
on every proposed change, and a weekly canary that runs the suites against newer vendor releases.
The canary reminds the maintainer to update attestation rows before users meet a new shader version.

## 2. Goals, non-goals, anti-goals

Goals:

- G1. `validate` becomes a real required check. It passes only when both test assemblies pass.
- G2. The gate runs on Direct3D 11. Poiyomi and lilToon shaders need it. The alpha capture also needs a graphics device.
- G3. A weekly canary runs the suites against the newest vendor releases. Unity stays fixed.
- G4. The same scripts serve the gate, the canary, and a local check. Only the version source differs.

Non-goals:

- N1. No automated release, tag, or listing change. `release.yml` stays manual. The only edits to `release.yml` and `build-listing.yml` are the ones in D12.
- N2. No change to product code, test code, or the correctness contract.
- N3. No Android or Quest test run.

Anti-goals. Each of these makes the work a failure even if the tests pass:

- A1. A secret reaches code that nobody reviewed.
- A2. A test skip or exclusion that nobody can see.
- A3. A green `validate` when the tests did not run.
- A4. A third-party action in a job that can reach a secret.

## 3. Facts

- `[SOURCE]` The repo is public. Only `pr.yml`, `release.yml`, and `build-listing.yml` exist.
  `pr.yml` has one job named `validate`. Branch protection requires it. The job name must not change.
- `[SOURCE]` The product code reads alpha through `Graphics.Blit` into an R8_UNorm target and then `AsyncGPUReadback`.
  Some tests create a `RenderTexture`. So `-nographics` cannot run the suite.
- `[SOURCE]` The suites run against the real vendor packages in the `locked` block of `Packages/vpm-manifest.json`.
  The repo never commits them. `docs/development-setup.md` lists one VPM source per vendor.
- `[SOURCE]` `VendorSuiteStartCheck` fails the product suite at start when a locked vendor version differs from the installed one.
- `[SOURCE]` Three fixtures sit behind opt-in variables: `AMUSE_DAO_INTEGRATION`, `AMUSE_REAL_THRY_LOCK`,
  and `AMUSE_PRODUCTION_ORDER_DISPATCH`. An unset variable gives a named skip. The Thry lock fixture calls a vendor counter over the network.
- `[SOURCE]` The documented full run takes about 30 minutes on a developer machine. That time is a goal and never a gate.
- `[SOURCE]` `Packages/manifest.json` holds a git-URL dependency on the Unity MCP editor tooling. It is development tooling, not a product dependency.
- `[SOURCE]` `release.yml` uses four third-party actions pinned by commit. `build-listing.yml` checks out a third-party repository and uses `actions/cache`.
- `[SOURCE]` The editor version is in `ProjectSettings/ProjectVersion.txt`.

## 4. Threat model

The decision is "maximum paranoia". The user accepts dropping the whole idea if the risk cannot be bounded.

Assets, most valuable first: the maintainer's trust in a green check, the license secret, the repository write token, the release path.

Attackers:

- T1. A fork PR author. Their code can run in the Unity Editor.
- T2. A hostile or compromised vendor package. The canary downloads the newest releases and runs them with the license present.
- T3. A compromised third-party action or a poisoned cache.
- T4. A compromised token for an in-repo branch. It can edit the workflow itself.
- T5. A tampered download of Unity or a package.

Facts that shape the design:

- Test code is arbitrary code. Any job that runs it can read every secret that the job holds.
- A `pull_request` workflow from an in-repo branch runs the branch's own copy of the workflow file.
- Hosted runners give no egress control without a third-party tool. `[LIMIT]` Egress cannot be restricted.
- The only secret in scope is a Unity license. `[UNVERIFIED]` Repository and organization settings cannot be read from the tree.
  The repo write token is never given to a Unity step.

## 5. Approaches

| | Approach | Trade-off | Outcome |
|---|---|---|---|
| A | Linux container with the GameCI test runner | Easy to set up. It cannot run Direct3D 11. It adds a third-party action next to the secret. | Rejected |
| B | Hosted Windows runner. Our own scripts install Unity and the packages. No marketplace action | More script to write. Direct3D 11 through the software adapter is `[UNVERIFIED]`. | Chosen. The spike gates it |
| C | Self-hosted runner on the maintainer machine | Real GPU. Fork PRs would run code on a personal machine. | Rejected by the user |
| D | No remote Unity run. A local `scripts/verify.ps1` before each push | No remote proof of a green suite. No secret in any workflow. | The pre-agreed exit if the spike fails |

`[DECISION]` The user chose B with D as the exit. The user rejected A and C.

## 6. Decisions

### D1. Two lanes

`[DECISION]` The gate lane runs on `pull_request` and on `push` to `main`. It uses the exact versions in the `locked` block.
The canary lane runs weekly and on manual dispatch. It floats every vendor package. It never gates a merge and it never edits the repo.

### D2. Trust model for the licensed jobs

`[DECISION]` The Unity jobs run only on code the maintainer accepted.

- A fork PR never reaches a Unity job. The job carries an `if`. The expression is
  `github.event_name != 'pull_request' || github.event.pull_request.head.repo.id == github.repository_id`.
  A check on `pull_request.head.repo` alone would skip `push` runs, where that field is empty.
  GitHub also withholds secrets from fork runs. Dependabot runs get no Actions secrets either, so they follow the fork route.
- `[CALL]` Three Environments, never repository secrets. The first review round on 2026-11-06 showed that one Environment cannot
  ask for approval on branches and skip it on `main`. Required reviewers apply to every job that uses the Environment.
  Branch rules match the ref, and a `pull_request` run uses `refs/pull/<n>/merge`.
  - `unity-gate`. The maintainer is the required reviewer. Allowed refs: `refs/pull/*/merge` and `ci/*`. It serves PRs and the spike.
  - `unity-main`. No reviewer. Allowed ref: `main` only. It serves `push` to `main`. The job picks it with an expression on the event name.
  - `unity-canary`. No reviewer. Allowed ref: `main` only. It serves the schedule and manual dispatch.
  - "Allow administrators to bypass" is off on all three.
- `[LIMIT]` An Environment filters refs, never triggers. Any writer can dispatch the canary on `main`. The canary has no approval step.
- `[LIMIT]` Any writer can open an in-repo PR or push a `ci/*` branch. The reviewer click is then the only barrier to the license.
- Approval means the maintainer read the full diff first. That includes `.github/` and `scripts/`. A branch can edit the workflow that runs it.
- `pull_request_target` and `workflow_run` are banned in the new files.
- `[CALL]` `validate` fails on a fork PR with a clear message. The maintainer reads it and pushes it to an in-repo branch.
  `validate` is not a trust boundary, because a PR can rewrite `pr.yml`. See section 11.
- `[CALL]` "Prevent self-review" stays off on `unity-gate`. With one maintainer, it would block every run. A stolen maintainer token could then approve its own run.

### D3. Action policy

`[DECISION]` The new workflows use `actions/checkout` and nothing else. Each reference is a full commit SHA.
`persist-credentials` is `false`. The workflow default is `permissions: {}`. A job asks for `contents: read` and nothing more.
Dependabot version updates need a `.github/dependabot.yml` with the `github-actions` ecosystem. The builder adds it. Dependabot PRs get no Actions secrets, so they follow the fork route.

- `[CALL]` The repo setting "require full SHA pinning" goes on. The allowlist keeps GitHub-owned actions and the four named,
  SHA-pinned actions in `release.yml`. This stops the setting from breaking the release workflow.
  Removing the third-party release actions is a separate piece of work.
- `[CALL]` The setting "require approval for all outside collaborators" goes on.
- `[CALL]` The default `GITHUB_TOKEN` permission in repo settings is read-only.

### D4. Our own installer scripts

`[CALL]` PowerShell scripts under `scripts/ci/` do three jobs: install Unity, install the vendor packages, and run the tests.
These scripts replace `vpm resolve` in CI. Reason: the script can enforce the hash check on every download.
It also removes the .NET tool install, which is one more download to trust.
The scripts install exactly the packages in the `locked` block. They need no dependency resolution.
A transitive dependency that is not in the block makes the script fail closed. It names the missing package.

- Unity. The script downloads the installer for the version in `ProjectVersion.txt`.
  It checks the Authenticode signature for Unity Technologies. It also checks the md5 value in Unity's release API. A mismatch stops the job.
  `[LIMIT]` md5 is weak. The signature is the real check. The release API gives no stronger value. This was observed on 2026-11-06.
- `[CALL]` The signer check pins the full subject string, not a substring. The spike prints the observed subject, and the plan records it.
- Vendor packages. The script reads each publisher's VPM listing and downloads the zip. The committed source file names one source per package.
  A listing that holds the same name cannot shadow it.
  The script checks the SHA-256 that the listing gives for that zip. When the listing gives none, it uses the digest GitHub shows on the release asset.
  A package with no hash route stops the job, unless the plan names it as a dropped vendor.
  `[LIMIT]` A listing hash is regenerated with the file. A GitHub release digest comes from the current asset. So this check catches transport damage.
  It does not catch a publisher who replaces a file under the same version. Open decision O2 covers this.
- The VPM source URLs live in one committed file. They are the sources in `docs/development-setup.md`, with one change.
  The VRChat packages come from the public listing that VRChat publishes on GitHub Pages. The `packages.vrchat.com` endpoints answered 403 on 2026-11-06.
- `[MEASURED]` Hash routes, as probed on 2026-11-06. Most listings give `zipSHA256`. The d4rkAvatarOptimizer listing gives none, but GitHub shows a SHA-256 digest
  on the release asset, and the script takes the hash from there. The VRCFury listing gives no hash and its download has no digest.
  So VRCFury has no hash route. See the S8 rows below.
- `[CALL]` The script removes the Unity MCP git-URL entry from `Packages/manifest.json` in the CI checkout. The checkout is not committed.
  This keeps git-fetched tooling out of a licensed editor session.
- `[CALL]` The script also removes the `com.unity.toolchain.*` entries from the same manifest. They are Linux cross-compile tools that no test needs.
- `[DECISION]` The canary reads each hash from the publisher at run time. The user wants no human step there. Open decision O2 asks about the gate lane.

### D5. Unity invocation

`[CALL]` The test step runs the editor in batch mode with `-runTests`, `-testPlatform EditMode`, one `-assemblyNames` value, and `-force-d3d11`.
It does not pass `-nographics`. The step reads the result XML after the run. It fails when:

- the XML is missing,
- the run has zero tests,
- the named assembly did not run,
- any test failed or errored.

The step writes the counts and the name of each failing test to the job summary. It never passes the license or the token as a command-line argument.
The script never prints a raw Unity log. The repository is public, so job logs are public. It prints counters and a fixed allowlist of lines.
A licensing line is counted, never shown. `[LIMIT]` Redaction cannot hide structured data such as the license XML. Hostile code can print it in encoded form.
The license is a burner, so the response to any licensing anomaly is to rotate it.
`[UNVERIFIED]` Unity may need the account email and password as well as the license file. They can only be passed as command-line arguments,
and any local process can read those. If the spike shows that need, the plan stops and asks the maintainer. D5 forbids that route as it stands.
A secret goes to the one step that needs it, through an environment variable of that step. It never goes to the job level.

### D6. Scope of the suites

`[DECISION]` Four gates:

- product assembly `Alrauna.Amuse.Tests.Editor`,
- research assembly `Alrauna.Amuse.Research.Tests.Editor`, which includes the source tripwire,
- the release exclusion check from `release.yml`, run on a zip built in the job but not published,
- `git diff --check`.

The two assemblies run as separate jobs so they fail on their own. Each job installs Unity again. This doubles the install cost.
If two licensed editors cannot run at once under one license, both assemblies run in one job, one after the other.

- `[CALL]` The three opt-in variables stay unset in both lanes. Each skip shows in the run record, by name.
  Turning one on later is a separate decision. The Thry lock fixture talks to a vendor server. The other two delete or write project output.
- `[UNVERIFIED]` The research assembly passes with no private input. The spike runs it.

### D7. The aggregate check

`[DECISION]` `validate` is one job that `needs` every other job and runs with `if: always()`.
It passes only when every needed job has the result `success`. A skipped job is a failure.
This closes the skipped-job path of A3. It does not close the forged-workflow path. See section 11.

### D8. The canary

`[DECISION]` Unity stays at the version in `ProjectVersion.txt`. Every other package floats to its newest stable release,
including releases outside the range that AMUSE supports. A red run reminds the maintainer to update.
`[DECISION]` The signal is the failed run and the email that GitHub sends. The canary has no write permission and opens no issue.
`[DECISION]` Pins protect the gate lane only. The canary ignores pins by design. A hijacked new vendor release can run in the canary.
The damage stays inside the burner Unity account, because the canary has no write token, no cache, and no secret except the license.

- The canary job summary lists each package as `name old -> new`.
- `[CALL]` The canary rewrites the `locked` block of `Packages/vpm-manifest.json` in its own checkout to match what it installed.
  Without this, `VendorSuiteStartCheck` fails the suite at the start. Then the maintainer learns only that versions differ and nothing else.
  The rewrite never leaves the runner.
- The floating version rule takes the highest stable version in the listing. It ignores prereleases.
- A new transitive dependency that is not in the `locked` block makes the canary fail closed. The summary names it.
- `[CALL]` A new SDK release that needs another Unity version makes the run red. That is a valid reminder.

### D9. No cache and no artifacts

`[DECISION]` No `actions/cache` in either lane. Cache poisoning is a known attack on Actions. The cost is a full install on every run.
`[CALL]` Every new workflow sets `cache-mode: none` at the top level. It blocks cache restore and save for every job in that workflow.
It is a documented workflow key. The runner then holds no cache write access to abuse. Open decision O1 covers `build-listing.yml`.
`[DECISION]` No artifact upload. The job summary carries the failures. The Unity log goes to the job log.
For deeper study the maintainer runs `scripts/verify.ps1`.

### D10. Script hygiene

- No expression that holds untrusted text, such as a branch name or a PR title, goes into a `run:` body. The value goes through an environment variable.
- Every job and every Unity step has `timeout-minutes`. The spike uses 300 for the job, because the first import is cold. The spike record sets the real values.
- `[CALL]` The runner image label is a fixed version, not `latest`. A runner image change then needs a reviewed edit.
- A concurrency group per ref cancels older runs of the same ref. The canary has its own group and never cancels.

### D11. Local fallback

`[DECISION]` `scripts/verify.ps1` runs the same install and test scripts on the maintainer machine in a temporary clone.
It uses a license activated in the local Unity Hub. It is also the exit if the spike fails.

### D12. Changes to the old workflows

`[DECISION]` The maintainer accepted these on 2026-11-06. They go beyond CI. They harden the release path.

- `build-listing.yml` and `release.yml` each get `cache-mode: none` at the top level. This closes the cache path from section 11. `[UNVERIFIED]` The `actions/cache` step in `build-listing.yml` may print a warning or an error when the cache is off. A manual run of `build-listing.yml` after the merge shows which.
- The `build` job in `release.yml` gets `environment: release`. The maintainer limits that Environment to the `main` ref in repository settings.
  A manual start from any other branch then fails before the job gets the write token. A job that names an Environment that does not exist creates it with no rules, so the Environment must be configured before the first release.
- `[DECISION]` The maintainer sets a tag ruleset that blocks tag updates and tag deletions. Tag creation stays allowed, because `release.yml` makes tags.
- `[DECISION]` The four third-party actions in `release.yml` stay for now. Replacing them is a separate piece of work and needs a ruling on whether anyone uses the `.unitypackage` file.
- `[CALL]` The plan may add a first step to the release flow that requires a green gate run on the release commit. That is optional and sits in a later record.

## 7. Step 0: the spike

A throwaway workflow on this branch answers the `[UNVERIFIED]` items before any final workflow exists.
It triggers on `push` to branches that match `ci/**`. A `workflow_dispatch` trigger would work only after the file exists on `main`.
It runs under `unity-gate` with approval. The result goes into a dated investigation note with no private identifiers.

Questions and pass rules:

| # | Question | Pass rule |
|---|---|---|
| S1 | Does the Unity installer download and verify? | Signature valid and checksum equal |
| S2 | Does the license file alone activate Unity? | Editor starts in batch mode with the file only. If not, record that an account email and password are also needed. See D5 |
| S3 | Does `-force-d3d11` start on a runner with no GPU? | The editor starts and reports a Direct3D 11 device |
| S4 | Does alpha capture work on that device? | `SystemInfo.supportsAsyncGPUReadback` is true. R8_UNorm is supported for render and read. `UnityAlphaFieldEvidenceTests` pass |
| S5 | Do Poiyomi and lilToon import and compile their shaders? | No shader error line in any log. The product tests under the `Semantics` namespace pass. The spike runs them and fails a step on any shader error line |
| S6 | How long does one lane take, how much disk does it use, and does one license work in two jobs at the same time? | Time and free disk recorded. The one-job spike cannot test the concurrency half. D6 holds the fallback if that half fails |
| S7 | Does the research assembly pass with no private input? | Zero failures, more than zero tests |
| S8 | Does every package in the locked block have a hash route? | One verified hash per package, or a named gap. Known before the spike: VRCFury has none. The spike records the observed hash for it |

Outcome table:

| Result | Path |
|---|---|
| S1 to S5 and S7 pass | Build D1 to D11 as written |
| S3 or S4 fails | The tests that need the device cannot run in CI. Take path D (local gate). Cheap checks only in CI. The record names what CI cannot prove |
| S8 gap for a vendor | That vendor falls out of CI, or the maintainer approves another check. The plan must name it. VRCFury is the known case. `VendorSuiteStartCheck` treats an absent optional vendor as a recorded fact, so dropping it is allowed by the suite. The VRCFury production-order fixture is opt-in and stays off in CI anyway |
| S2 needs an account password | Stop and ask the maintainer. A password on a command line is visible to local processes, and D5 forbids it |

The plan must not build the final workflow until the spike record exists.

## 8. Failure modes

- The license becomes unusable. Both Unity jobs go red. `validate` goes red. The fix is a new license and a secret update.
  The local gate stays available.
- A vendor changes a package under the same version. The hash check fails the install only when the hash comes from a source that the change did not touch. See the D4 limit.
- A VPM listing is down. The install fails. The run goes red. A rerun is safe.
- The Unity installer link moves. The install fails closed.
- A fork PR arrives. `validate` goes red with a message. The maintainer decides.

## 9. Left to the builder

- Exact PowerShell structure under `scripts/ci/`, within D4 and D5.
- The runner image label. The spike picks it.
- The cron time of the canary. A low-traffic hour is the default.
- Whether the job summary uses one table or two.
- How `scripts/verify.ps1` finds the local Unity install.

## 10. Human steps

The maintainer does these. The builder cannot.

1. Make one new Unity account for CI. Use a unique password. Add no payment detail and no asset. Never use the main account.
2. Activate a free license for that account in the local Unity Hub. Keep the license file. Never commit it.
3. Create the Environments `unity-gate`, `unity-main` and `unity-canary`. Set allowed refs and reviewers as in D2.
   Turn "Allow administrators to bypass" off on all three. Turn "prevent self-review" off on `unity-gate`.
4. Add the license file text as the secret `UNITY_LICENSE` in all three Environments.
5. Set the repository settings in D3.
6. Review the spike branch diff. Approve the spike run in the Actions tab.

## 11. Known limits

- Egress from a hosted runner cannot be restricted. A hostile vendor package in the canary can reach the network while the license is present.
- `[LIMIT]` The blast radius is larger than the CI account. Code in the editor runs as the runner user. In a run that has cache write access, it could change the files of a
  JavaScript action that runs a post step, such as checkout. The post step runs with the Actions runtime token, and that token writes the repository cache.
  `build-listing.yml` restores a cache and then publishes the VPM listing. `cache-mode: none` closes this path for the new workflows. Open decision O1 closes it for the old one.
  `[INFERENCE]` The technique is public research. It was not reproduced here.
- The gate proves the tests pass on one software graphics device. A hardware GPU can differ. A past record already notes that
  capability results differ between machines (`docs/development-setup.md`, capability-dependent tests).
- Branch approval is a manual read of the diff. A careless approval defeats D2.
- Opt-in fixtures stay off in CI. Their behavior is not covered remotely.
- `validate` is not a trust boundary. A PR can rewrite `pr.yml` so that `validate` passes. The test verdict is also computed inside the VM that runs vendor code.
  The maintainer reads the diff before merging. Nothing else stops a forged result.
- GitHub may disable scheduled workflows after a long period without repository activity. `[UNVERIFIED]` The canary could stop without notice.

## 12. Open decisions after the adversarial review of 2026-11-06

Five reviewers read the spec and the spike files. These findings need a ruling from the maintainer. The rest of the findings are fixed in this record and in the spike files.

- O1. `[DECISION]` Accepted. Add `cache-mode: none` to `build-listing.yml` and `release.yml`. This changes two files that N1 left alone. D12 yields to this ruling.
- O2. `[DECISION]` Accepted. The gate lane commits a SHA-256 for the Unity installer and each locked zip. A bump needs a human edit.
  The canary keeps reading hashes from the publisher. The spike prints the observed installer hash, so the first committed value comes from a reviewed run.
- O3. `[DECISION]` Deferred by the maintainer until a test. The spike installs VRCFury and records its observed hash. The ruling follows the spike record. Until then, VRCFury stays in the spike only.
- O4. `[DECISION]` Accepted as proposed. No required review and no CODEOWNERS file.
- R1. `[DECISION]` Accepted. `cache-mode: none` in the old workflows lands now, on this branch. See D12.
- R2. `[DECISION]` Accepted. The `release` Environment limited to `main`, and the tag ruleset. See D12.
- R3. `[DECISION]` Deferred. Replacing the four third-party actions in `release.yml`.
