# CI spike record

Record date: 2026-11-07. Status: complete. This record speaks for its date. It never authorizes new subsystems.

## Privacy note

This record describes continuous integration runs only. No private avatar data exists in this work. The record names no account, no email address, no password, no license serial, no token, no machine, and no port. The Unity account was a burner account for these runs, and the record never names it. The record repeats three public values that the design spec already holds: the SHA-256 of the public Unity installer, the Authenticode signer subject of that installer, and the observed SHA-256 of the VRCFury zip. Everything else that a run printed stayed in the private job logs. The public logs printed redacted allowlisted lines only.

## Purpose

The design spec `docs/superpowers/specs/2026-11-06-ci-test-gate-design.md` asks eleven questions, S1 to S11, before any final workflow can exist. A throwaway workflow answered them. It ran on hosted Windows runners. It started on pushes to `ci/**` branches. One Environment held the burner account secrets, and the maintainer approved every run. After the ruling on open decision O6, a run had two jobs in sequence, each with its own approval. Mode `admin` ran the Editor as the runner account. Mode `lowuser` ran the activation, the Editor, and the tests as a local user that was not an administrator. A probe in each job tried to open the runner processes for memory reads and tried to write to the places that administrator steps run from. It printed counts and yes or no answers only.

The branch removed the spike files from the tree on 2026-11-07 (UTC). The git history of the branch holds them. This note is the record of their runs.

## Runs 1 to 12

All runs happened on 2026-11-07 (UTC). Counts and times are as observed.

### Run 1, mode admin

- S1 passed. The installer md5 matched the Unity release API. The Authenticode signature was valid for Unity Technologies. The install took 5 minutes. The runner had 142 GB of free disk.
- The package step failed on a script bug in the dependency check.
- 11 packages with a hash route passed their hash check. VRCFury had no hash route.

### Run 2, mode admin

- S8 passed.
- The Editor exited with code 1 in under a minute. The log held one line that said no valid license.
- S2 failed. The license file alone did not license the machine. A Personal license file is bound to the machine that made it.
- S3, S4, S5, and S7 could not run, because no Editor started.

### Run 3

The spec holds no measured outcome under this number. Run 3 was planned as the first test of account activation with the account email and password. The ruling on O6 then reshaped it into the two job modes. The measured record resumes with run 4.

### Run 4, mode admin

The whole job passed in about 27 minutes on a hosted `windows-2022` runner.

- S1 passed again. The install took 4 minutes.
- S2 passed. The licensing client inside the Editor install, version 1.15.0, activated the burner account with the account email and password. The client exited with code 0 after about 5 minutes. The return step exited with code 0.
- S3 and S4 passed. The runner has no GPU. The Editor got a Direct3D 11 device named Microsoft Basic Render Driver. Async readback worked. An R8_UNorm target rendered and read back. The blit test returned the expected value.
- S5 passed. No log held a shader error line. The 1415 tests under the `Semantics` namespace passed.
- S4 and S7 passed. The 134 capture tests passed. The 151 research tests passed.
- S8 passed for every package except VRCFury, which has no hash route. The cold project import and the probe took 8 minutes. The semantics tests took 8 minutes.
- S9 observed no residue in processes. No process command line held the credentials after activation. A scan of 3001 files under the Unity data folders found no credentials and no JWT-shaped token.
- S10 recorded the admin baseline. The admin account could open both runner processes for memory reads. It could write to every place that an administrator step runs from.

The paired low-user job of this run failed at once in its prepare step. `net user` asked a yes or no question for a password longer than 14 characters and got no answer. The fix is the `/y` switch. Nothing of the credentials was used in that job.

### Run 5, mode lowuser

S11 failed. The other licensed steps worked.

- The low user was created and was not an administrator.
- The licensing client, run as the low user, activated the burner account and exited with code 0.
- S10 passed for memory reads. The low user could not open the runner worker or the runner listener for memory reads. A control on its own process worked.
- S10 passed for writes. The low user could not write to the admin scripts folder, the workspace, the runner temp folder, the runner actions folder, the Editor install, the runner root, the `bin` folder, or the `externals` folder. It could write only to the license folder, which the script granted to it.
- S9 observed one file under the low user profile that held the email or the password. The scan did not tell which. No process command line held the credentials.
- S11 failed. The Editor exited with code 1 in under a minute, with and without graphics. Its log said that it could not verify the licensing client signature and that no access token was available. The cause was open.

### Run 6, mode lowuser

- The licensing client was valid as the low user. Editor licensing worked. The client connected, the license updated, and the entitlements resolved. The signature message in the Editor log says that it ignores the problem, so that message was not the cause.
- The cause of the S11 failure was the Unity Package Manager. Its local server process stopped with exit code 1. The Editor could not connect to it after 30 seconds and exited with code 1.
- S9 observed one file under the low user profile that held the email. No file held the password. No file held a JWT-shaped token. No process command line held the credentials.
- One line of the Editor log named the Personal license serial of the burner account, with the last group masked by Unity. That line reached the public log of this run. The script removed serials from every printed line after this run.

### Run 7, mode lowuser

- The serial no longer appeared in the public log. The S11 result did not change.
- The log of the package manager was not at the place where the Editor normally writes it under the profile of the low user. The server probably stopped before it could write a log.

### Run 8, mode lowuser

- The script created the profile folders. The result did not change.
- The package manager server, run by itself as the low user, printed its own error. It is a Node program. It stopped with `EPERM: operation not permitted, lstat` on the agent folder, a parent of the Editor install. Node checks every parent folder of its script with a stat call. The low user had no right to read the attributes of that folder.

### Run 9, mode lowuser

- The script granted the low user the right to read attributes and to pass through on the agent folder and the runner temp folder, on those two only. The result did not change. The package manager server stopped with the same error on the same folder. The S10 results did not change.

### Run 10, mode lowuser

- The script granted read and execute on the two parent folders, with no inheritance. It printed the rights of the drive root, the agent folder, and the runner temp folder. The package manager server still stopped. The rights list found the cause.
- S11 failed again. The package manager server stopped, so the Editor could not start.
- The drive root gives the Users group read and execute, and the right to create files and folders. The agent folder and the runner temp folder inherit that. So the low user could already read both, and it needed no grant.
- The deny rule that the script set with `icacls` held the right to synchronize, together with the write rights. A deny on synchronize fails every open that waits on the result, even a read. A plain attribute read without a handle worked. That is why Unity itself ran and the package manager server did not.
- The Node program opens each parent folder with a handle. The deny rule caused the stop. The cause was the rule of the spike, not Unity.

### Run 11, mode lowuser

All steps passed. The Editor ran as the non-admin user.

- The script set the deny rule from .NET, with each right named and without the right to synchronize. It removed the grants of runs 9 and 10, because they were not needed. A local test showed that the new rule holds the write rights and the delete rights only.
- S10 passed. The low user could not open the memory of the runner processes. It could not write to the admin scripts, the workspace, the runner temp folder, the runner actions folder, the Editor install, the runner root, its `bin` folder, or its `externals` folder. It could write to its own license folder only.
- S11 passed. The probe passed. The product capture tests passed 134 of 134. The product shader tests passed 1415 of 1415. The research tests passed 151 of 151. The license return exited with code 0.
- Step times: install 3 minutes, cold import and probe 8 minutes, capture tests 1 minute, shader tests 7 minutes, research tests under 1 minute.
- D3D11 on the software device and the R8_UNorm readback worked as in run 4.
- Residue: one file under the low user profile held the email. No file held the password. The cleanup step deleted the user and its profile.
- The public log held no email, token, or serial.

### Run 12, mode lowuser

All steps passed. This run used the vendor versions of the maintainer dev project.

- 11 of 12 vendor sources verified by hash. VRCFury 1.1442.0 has no hash route. Its observed hash is in the pinned values below.
- The product capture tests passed 134 of 134. The product shader tests passed 1415 of 1415. The research tests passed 151 of 151. The shader and compile error counts were 0. A D3D11 device was present.
- Step times: install 4 minutes, first import and probe 9 minutes, shader tests 9 minutes.
- The public log held no email, token, or serial.

## Answers to S1 to S11

- S1 pass. Signature valid, md5 equal, in runs 1 and 4. Install times 4 to 5 minutes.
- S2 fail in run 2 with a copied license file. Pass from run 4 on with activation through the licensing client. The client sits in the Editor install. The return step exits with code 0.
- S3 pass. `-force-d3d11` starts on a runner with no GPU, in runs 4, 11, and 12. The device is the Microsoft Basic Render Driver software adapter.
- S4 pass. Async readback is supported, R8_UNorm renders and reads, and the capture tests pass 134 of 134, in runs 4, 11, and 12.
- S5 pass. No shader error line in any log, and the 1415 tests under the `Semantics` namespace pass, in runs 4, 11, and 12.
- S6 partial. Times and free disk are recorded above. The concurrency half was never tested. The final design runs both assemblies as steps of one job, so it does not rely on that half.
- S7 pass. The research assembly passes with 151 tests and zero failures, in runs 4, 11, and 12.
- S8 named gap. 11 of 12 packages verified by hash. VRCFury has no hash route. Its observed hash is recorded below.
- S9 informational. No process command line held the credentials in any mode. In admin mode, a scan of 3001 files found nothing. In low-user mode, one file under the low user profile held the email. No file held the password. No file held a JWT-shaped token. Run 6 put a masked serial in a public log, and runs from 7 on removed serials.
- S10 as expected in both modes. The admin baseline could read runner memory and write to every administrator-run place. The low user could do neither. Its one allowed write was its own license folder.
- S11 failed in runs 5 to 10. Run 10 also stopped on the package manager server, so no Editor started. It passed in runs 11 and 12.

## Root causes

1. A Unity Personal license file binds to the machine that made it. A copy on a new machine does not license the Editor. The activation uses the account email and password through the licensing client in the Editor install.
2. `net user` asks a yes or no question when a password is longer than 14 characters. The `/y` switch answers it.
3. The deny rule of the hardening held the right to synchronize. A deny on synchronize fails every open that waits on the result, including a read. The package manager server opens each parent folder with a handle, so it stopped. The rule, not Unity, caused the failure.

## Pinned public values

Observed in runs 11 and 12 on 2026-11-07 (UTC). These values are public and are already in the spec.

- Installer SHA-256 for Unity 2022.3.22f1: `e42d3637666483009c16fee551ca083ba17f4f1b36055c4ca62c552f124dfb01`.
- Authenticode signer subject of that installer: `CN=Unity Technologies SF, O=Unity Technologies SF, L=San Francisco, S=California, C=US, SERIALNUMBER=3183233, OID.2.5.4.15=Private Organization, OID.1.3.6.1.4.1.311.60.2.1.2=California, OID.1.3.6.1.4.1.311.60.2.1.3=US`.
- Observed SHA-256 of the VRCFury 1.1442.0 zip, which has no publisher hash route: `3104aba8fd29712d31f87a2966afe5fcf081ea38267abf3fc0cf30916fd27390`.

## Outcome

The ruling on O6 closed with a pass on 2026-11-07 (UTC). The final design runs the activation, the Editor, and the tests as the non-admin user. The admin mode stays as the fallback. The branch removed the spike files from the tree on 2026-11-07 (UTC), and the git history holds them. On 2026-11-07 (UTC), no run of the final workflows had happened yet. The plan record governs their first runs.

## Known debt

On 2026-11-07 (UTC), `Get-Json` and the other listing helpers stay duplicated in three entry scripts, and this branch leaves that deduplication as post-merge debt.
