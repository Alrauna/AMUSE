# lilToon 2.4.0 attestation and test stall investigation

Status as of 2026-10-19. Branch at investigation time: `update-for-current-packages`.

Scope: read-only characterization of two problems observed on 2026-10-19. Machines and editor sessions appear by role only: the dev editor instance for this repository, the Census Lab editor instance, and a PC running the release build. No private avatar data appears in this record. Observed test counts are recorded as observed.

## Purpose

Two questions came out of the 2026-10-19 package update.

1. A full EditMode test run stalled for about five minutes. The owner attributes the pause to UI popups. How can tests run and fail without waiting for a user?
2. lilToon moved from 2.3.4 to 2.4.0. AMUSE refuses it. The owner requires source clones of both versions, an understanding of the diff, and upstream patch notes before tests may pass on 2.4.0.

This record characterizes. It decides nothing by itself.

## Method

Three parallel investigations ran on 2026-10-19.

| Investigation | Scope | Mode |
|---|---|---|
| Modal UI stall surface | Blocking UI call sites in the product package and the stall root cause | Read-only repo analysis |
| lilToon upstream diff | Scratch clones of upstream tags `2.3.4` and `2.4.0`, full diff, changelogs | Network and scratch directory outside the repository |
| Attestation pin surface | Pin inventory, verification flow, re-derivation procedure, test gaps | Read-only repo analysis |

Claims carry `file:line` references. Product and test paths are relative to `Packages/com.alrauna.amuse/` unless a path carries the research package root. Upstream facts carry upstream paths or URLs. Statements derived rather than observed carry `INFERENCE`.

Two load-bearing claims were re-verified directly during synthesis and are marked `Direct verification during synthesis` where they appear: the single-modal claim in section 2, and the admitted-version row set in section 5.

Three parallel adversarial reviews of an earlier draft of this record ran on 2026-10-19. This version integrates their findings. The reviews corrected five citations, removed two unqualified status words, and added the alternative popup candidates in section 3a.

## 1. The event

The package update changed the locked resolution in `Packages/vpm-manifest.json`. The requested versions in the `dependencies` block kept the old values. The table below reads Before from the `dependencies` block and After from the `locked` block. `Direct verification during synthesis`.

| Package | Before (requested) | After (locked) |
|---|---|---|
| `jp.lilxyzw.liltoon` | 2.3.4 | 2.4.0 |
| `d4rkpl4y3r.d4rkavataroptimizer` | 4.5.4 | 4.6.0 |
| `com.anatawa12.avatar-optimizer` | 1.9.17 | 1.9.20 |
| `nadena.dev.ndmf` | 1.14.4 | 1.14.8 |
| `com.vrchat.base` / `com.vrchat.avatars` | 3.10.5 | 3.10.5 |

The requested-versus-locked split is an evidence-stability hazard. A resolver pass can revert lilToon to 2.3.4 and invalidate the rerun evidence. The installed version at product-run and research-run time was 2.4.0. `INFERENCE` from the observed failures, which name version 2.4.0 in both suites.

Test results observed on 2026-10-19, same machine and editor:

| Suite and time | Summary as the runner reported it |
|---|---|
| Product, baseline, before update | 2735 total, 2735 passed, 0 failed, 0 skipped. The detail list showed 2 Inconclusive: the env-gated `DaoMergedConsumptionTests` pair, which the summary did not count |
| Product, after update | 2737 completed at the progress level. 21 failed, 2 Inconclusive, 2714 passed |
| Research, baseline | 144 total, 140 passed, 0 failed, 4 skipped |
| Research, after update | 144 total, 2 failed, 4 skipped |

The 2 count difference between the baseline summary and the after-update progress counter is the DAO Inconclusive pair. The runner's summary and progress counters account for it differently. Both rows above report the runner's own numbers.

The 21 product failures after the update:

- 19 in `AaoMergedConsumptionTests`. Message: the fixture must complete the real build, and the NDMF error report names the failing pass.
- 2 in `LilToonOpaqueTargetTests`. Message: the pinned opaque target failed source attestation, package version `2.4.0`. This is the direct attestation evidence and it is observed, not derived.

The 2 research failures are `VendorReachabilityTests.AnInstalledFamilyMatchesTheVersionAmuseAttests` and `VendorReachabilityTests.LilToonReachesProvenOpaqueThroughTheProductionPath`. Both assert installed lilToon version equals the pinned `2.3.4` and observed `2.4.0`.

The editor stopped serving external tool calls for about five minutes at the start of the product run, then recovered with the run at 681 of 2888 tests. The owner attributes the pause to UI popups and reports the same popups during build attempts in the Census Lab project and on the PC release build. The record does not capture how many popups appeared during the test run or how each was dismissed. No console excerpt or editor-log excerpt was captured for the stall window.

One more effect was observed and is recorded for completeness. Orphaned test meta files under `Assets/` grew from 4 at 12:11 to 8 after the after-update product run. The `AmuseTests_Temp` numbered meta files accumulate across product-suite runs. A reviewer sweep of the two package directories found no orphans. The orphans live under `Assets/`.

## 2. Modal UI surface of the product package

The product package has exactly one blocking UI call. `Direct verification during synthesis`: a search over the product package for `DisplayDialog`, cancelable progress bars, window calls, popups, and notifications returns only `VersionConsentDialog.cs:50-52`.

| Symbol | Location | Trigger | Blocking |
|---|---|---|---|
| `VersionConsentDialog.Present` calls `EditorUtility.DisplayDialog` | `Editor/Build/VersionConsentDialog.cs:50-52` | Consent gate falls back to it when the presenter is null and the editor is not in batch mode | Yes, a native modal, waits for user input, no timeout |
| `VersionConsentDialog.ShouldProceed` | `Editor/Build/VersionConsentDialog.cs:28-43` | The consent gate | Only through the presenter it calls |
| All `AmuseReports` reporting | `Editor/Build/AmuseReports.cs` | Refusals and status | No, NDMF error report entries |
| `GUIUtility.ExitGUI()` | `Editor/AmuseAvatarOptimizerEditor.cs:333` | Dry-run cancel | No, exits a GUI loop |

The package's only `SessionState` writes outside tests are build caching (`Editor/Build/PlayModeBuildStore.cs:78-93`). Tests also write a `SessionState` key for AAO plugin management (`Tests/Editor/Build/TransientUnlockTestLifecycle.cs:95-103`). No consent state is written anywhere.

## 3. Stall root cause

The chain, with evidence:

1. The merged-consumption fixture gate checks shader presence, not version. `RequireLilToonEnvironment` requires a non-null `Shader.Find` result and ignores the installed version (`Tests/Editor/Build/MergedConsumptionFixture.cs:142-152`, null check at `:147`). lilToon 2.4.0 passes the gate. `INFERENCE` from the version-blind check. Both `AaoMergedConsumptionTests` and `DaoMergedConsumptionTests` share this fixture.
2. The AAO suite drives the real pipeline. `AaoMergedConsumptionTests` call `AvatarProcessor.ProcessAvatar` on the real VRChat platform (`Tests/Editor/Build/AaoMergedConsumptionTests.cs:252-303`, `:281`), so the production plugin runs.
3. The production entry has no presenter injection point. The plugin wires `AmusePlatformFinishPass.Execute` without a presenter, and `Execute(BuildContext)` passes `consentPresenter = null` (`Editor/Build/AmusePlatformFinishPlugin.cs:274-275`, `Editor/Build/AmusePlatformFinishPass.cs:15-26`).
4. The consent pre-scan produces a subject for 2.4.0. `CollectTransferConsent` walks assigned materials (`Editor/Build/AmusePlatformFinishPass.cs:295-299` into `Editor/Semantics/UnityMaterialSemantics.cs:479-535`). The real lilToon material selects a supported family. Attestation refuses 2.4.0 because the admitted set holds only 2.3.0 through 2.3.4 (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:1996-2004`, `:2019`, `:393-405`). One consent subject results per unverified supported-family shader (`Editor/Semantics/UnityMaterialSemantics.cs:508-511`).
5. The gate falls through to the dialog. `ShouldProceed(subjects, Application.isBatchMode, consentPresenter ?? VersionConsentDialog.Present)` runs at `Editor/Build/AmusePlatformFinishPass.cs:321-325`. The interactive test runner is not batch mode, so the native dialog opens.
6. Unattended, the dialog waits forever. `EditorUtility.DisplayDialog` returns only on user input and never auto-declines. Observed: the product run blocked once for about five minutes, then proceeded normally. Observed: the 19 AAO tests failed with build-refusal messages, which is the recorded decline outcome. `INFERENCE`: each real-build test that ran while 2.4.0 was unattested armed a dialog, and each dialog was answered, most plausibly by the operator who reported the popups. The count and timeline were not captured.
7. Why the seam did not help. The test seams substitute vendor attestation and host admitted-version hooks (`Tests/Editor/Build/VerifiedLilToonTestSeams.cs:16-35`, `Editor/Semantics/AdmittedProducerVersions.cs:48-73`). The AAO suite deliberately uses the real vendor shader through the real plugin. In that combination no attestation seam applies and the presenter parameter is unreachable. The repo already knew this trap. `Tests/Editor/Build/AlphaSeparationPreparationTests.cs:3028-3039` documents that a supported-family material against an unattested source is batch-impassable and drives such fixtures below the avatar pass. The AAO suite did not apply that lesson.
8. Repeat exposure. The presenter must not remember acceptance across builds (`Editor/Build/VersionConsentDialog.cs:7-10`, decision V8). Decline is equally not remembered. Every later real-build test in the run re-armed the same dialog.

Batch mode was fail-closed as of 2026-10-19. `ShouldProceed` refuses without asking when `Application.isBatchMode` is true, and a unit test pins it (`Tests/Editor/Build/VersionConsentTests.cs:190-208`). The gap is the interactive test runner, which is neither batch mode nor human.

The observed run is consistent with two accounts, and the record cannot choose between them because no log or console excerpt was captured:

- Operator-present account. Each armed dialog was answered by the operator. The first dialog drew a long pause while the run looked hung. Later dialogs were answered quickly. This fits the owner's plural report of popups and the five-minute total.
- Once-per-session account. The one long block had a single once-per-session source, and later dialogs were answered or did not re-arm. This fits the single observed block window less well, because the run stayed blocked at its start while later polls succeeded immediately.

Either way the design conclusion is the same. The stall class, not a single dialog instance, must be removed. No modal may ever block a test run.

## 3a. Popup sources outside the product package

Section 2 is scoped to the product package. The stalled run executed four vendor packages, and the source-level sweep cannot see vendor editor DLLs. The reviews required these candidates to be dispositioned:

| Candidate | Disposition |
|---|---|
| VPM resolver dialog, `EditorUtility.DisplayDialog` at `Packages/com.vrchat.core.vpm-resolver/Editor/Resolver/Resolver.cs:89`, once per editor session when a resolve is pending | Not excluded. Its trigger condition is present: the manifest `dependencies` block still requests the old versions. Its once-per-session behavior fits a single block window. The owner's popups during build attempts fit the consent dialog instead |
| Resolver network or progress path | Not excluded as a slowdown, but it would not produce popups, and the owner attributes the pause to popups |
| Modal dialogs inside vendor editor DLLs (AAO, NDMF, d4rkAvatarOptimizer, VRChat SDK) | Not excluded. A source search cannot see compiled vendor assemblies. A repo-wide source scan of the vendor package sources found none, per the adversarial review |
| OS-level permission dialogs | Excluded. The popups reproduce on two operating systems, and native permission prompts differ per OS |
| Source-visible modals in AAO, NDMF, d4rkAvatarOptimizer, and VRChat SDK package sources | Excluded by a repo-wide source scan during the adversarial review |

The consent dialog remains the only established cause because its trigger condition is proven by the failure messages and the code chain. The resolver dialog is a plausible contributor to the single long block. Both candidates produce the same design requirement: tests must never be able to block on any modal.

## 4. lilToon upstream diff 2.3.4 to 2.4.0

Provenance: upstream repository `https://github.com/lilxyzw/lilToon`, cloned on 2026-10-19 into a scratch directory outside the repository. Upstream tags carry no `v` prefix.

| Tag | Commit | Date |
|---|---|---|
| `2.3.4` | `252fd8cfc46106d4967e95b3f2c788418502f227` | 2026-06-25 |
| `2.4.0` | `582a703a79cfc28ba67f540ed4ea63cc2c4b1e7b` | 2026-10-19 |

Reported diff totals between the tags: 23 files changed, 1655 insertions, 1058 deletions. The per-area rows below are approximate. The new-file pair is counted once and the localization row spans 5 files, so the rows do not sum exactly to the reported totals.

| Area | Files | Change |
|---|---|---|
| `Shader/Includes/VRC Light Volumes/LightVolumes.cginc` | 1 | About +1242 / -721, VRCLV 3 rewrite |
| `Shader/Includes/VRC Light Volumes/LightVolumesBuildConfig.cginc` with meta | 2 | New file |
| `Shader/Includes/lil_common_functions.hlsl` | 1 | About +22 / -7, APV sampling refactor |
| `Shader/Includes/lil_common_macro.hlsl`, `openlit_core.hlsl` | 2 | +2 / -2, near the VRC Light Volumes gating. The diff report associates these lines with that work. This record has not independently characterized them. `INFERENCE`: alpha-irrelevant |
| `Shader/ltsmulti_o.shader` and `BaseShaderResources/ltsmulti_o.lilinternal` | 2 | +2 / -2, Multi outline render queue fix |
| Editor scripts (`lilConstants.cs`, `lilMaterialUtils.cs`, `lilStartup.cs`, `lilToon2Ramp.cs`, `lilToonSetting.cs`, `lilInspector/lilGUIUtility.cs`, `lilToon.Editor.asmdef`) | 7 | About +41 / -14 |
| Localization `.po` files | 5 | About +306 / -315 |
| `CHANGELOG.md`, `CHANGELOG_JP.md`, `package.json` | 3 | +24 / -2 |

Only one shader asset changed in the entire diff: `ltsmulti_o.shader`. Within the upstream tag trees, every other shader asset and base resource is byte-identical between the tags, and the only include changes are the VRC Light Volumes and APV files named above.

Changelog for 2.4.0, quoted from upstream `Assets/lilToon/CHANGELOG.md`, entry dated 2026-10-19:

> Changed: Minimal VRCLV 3.0 support.
> Fixed: improve Korean localization. Fixed a scene build crash with UI Toolkit assets from a memory allocation failure. Handle invalid version check responses. Correct RenderQueue value for multi outline shader. Correct typo in mainTexCheckWords array for emission.

The GitHub release bodies for both tags are empty. The vendor documents changes only in the changelog files. The vendor claims no breaking changes. No 2.4.x patch release existed as of 2026-10-19.

### Per-area verdicts

| AMUSE concern area | Verdict | Evidence |
|---|---|---|
| Rendering modes and keywords | Unchanged | Each mode is a distinct shader asset. Only `ltsmulti_o.shader` differs. Mode switching entry `Assets/lilToon/Editor/lilMaterialUtils.cs` untouched except an AudioLink mask pruning fix |
| Cutout and alpha equations | Unchanged | `lil_common_frag_alpha.hlsl`, `lil_pass_forward_normal.hlsl`, `lil_pass_depthonly.hlsl` byte-identical. Cutout stays `clip(a - _Cutoff)` |
| MainTex second color and alpha | Unchanged | No diff in `lil_common_frag.hlsl` |
| Canonical opaque target | Unchanged | No shader named `lilToonOpaque*` exists in either tag. The opaque target is `lts.shader` with pass `ltspass_opaque.shader`. Both byte-identical in the tag trees |
| Conversion-relevant properties | Unchanged | No property block changes in any shader |
| Multi outline | Changed | SubShader queue tag moved from `Transparent-100` to `Geometry`, upstream fix 435. Blend and ZWrite untouched |
| Rendered light under APV or VRC Light Volumes | Changed | APV darkening fix and VRCLV 3. `INFERENCE`: lighting only, no alpha impact, since AMUSE reads texture alpha fields rather than lit color |
| Editor property pruning | Changed | `mainTexCheckWords` typo fix and AudioLink mask pruning. `INFERENCE`: affects vendor inspector cleanup, not the shader pipeline AMUSE reads |
| Thry editor integration | Absent | Zero Thry references in both trees |

Two caveats bound every Unchanged verdict above. First, these are tag-tree comparisons. The repo's own doctrine measures pins from installed packages because upstream committed generated artifacts can lag the generator. Second, the attestation conjunction pins asset GUIDs, which live in meta files. The tag diff enumerates meta files and shows no GUID changes outside the new include, but installed-package bytes are the authority. Admission work must re-confirm from installed 2.4.0 shapes.

## 5. Attestation pin surface

### What lilToon attestation pins

`Editor/Semantics/LilToon/LilToonSourceAttestation.cs` attests identity as a per-profile conjunction: shader name, asset GUID, `_lilToonVersion` stamp (`45f`), package name and version, pass asset name and GUID, canonical shader and pass digests, include-tree digest, and render mode. Class at `:353`, identity constants at `:355-364`, digests at `:366-381`, admitted rows at `:393-405`, cutout profile at `:437-452`, transparent profile at `:453-472`, outline wrappers at `:473-513`, one and two pass variants at `:514-540`, profile tables at `:553-716`, Multi container rows at `:2313-2333`.

The version range is not a comparison. It is a closed admitted set of exact rows, `AdmittedPackageVersions` at `:393-405`: one `(Version, IncludeDigest)` row per admitted version, 2.3.0 through 2.3.4, with 2.3.1 through 2.3.3 sharing one tree row. `IsAdmittedPackageVersion` at `:407-420` does an exact ordinal match. `IncludeDigestForVersion` at `:422-436` returns the row digest. `Direct verification during synthesis`: the rows and both methods were read at the source during synthesis. The comment above the rows records why the tree is the only per-version bytes: the shader and pass assets are byte-identical across 2.3.0-2.3.4, so the include tree carries the per-version difference.

Verification order at `Verify`, `:1960-2082`: name, asset GUID, format stamp with exact float compare, package name plus admitted version at `:2018-2026`, pass GUID, canonicalization provenance, include-tree pairing for the version (`TryVerifyIncludeTreeForVersion`, `:2122-2153`), shader and pass digests, render mode diagnostic at `:2078-2082`. Mismatch codes include `UnsupportedVersion`, `ModifiedShaderSource`, `MissingSourceEvidence`, `UnsupportedShader`, `UnsupportedShaderVariant`. A mismatch answers all-Unknown for the material, `AlphaUnknownKind.UnattestedShader` (`Editor/Semantics/UnityMaterialSemantics.cs:460-473`).

The canonical opaque target recipe lives in `Editor/Semantics/LilToon/LilToonOpaqueTarget.cs`. `CanonicalOpaqueTuple` is declared at `:55` and holds eighteen scalar writes at `:57-74`, measured from the installed 2.3.4 package on 2026-08-30 per the provenance comment at `:20-25`. The header at `:26-30` mandates re-measuring the tuple from a newly attested install before pins change.

### Consent interaction

Two subject sources compose per build (`Editor/Build/AmusePlatformFinishPass.cs:295-325`): host version subjects from `HostLifecycleCapability` and shader transfer subjects from the D8 pre-scan. `VersionConsentDialog.ShouldProceed` proceeds on empty subjects, refuses without asking in batch mode, and otherwise asks the presenter. Decline is a reported total no-op. Grant lets the pipeline run and sets the unlock-window consent flag. Consent is never persisted anywhere. The presenter contract forbids memory across builds (`Editor/Build/VersionConsentDialog.cs:7-10`, decision V8). The only `SessionState` writes in the package outside tests are build caching, not consent.

### Measured-pin procedure on record

The repo records how pins were derived. Task 0 measured the first digests on 2026-08-18 from a scratch installed 2.3.4 package. The upstream tag was never the source, because upstream committed generated shaders are stale relative to their own generator (`docs/superpowers/plans/2026-08-17-liltoon-semantics-adapter.md:21-24`, provenance comment `LilToonSourceAttestation.cs:366-371`). Later measurements follow the same rule. Each re-measurement reproduced all existing pins first (`docs/superpowers/investigations/2026-09-01-liltoon-transparent-normal-alpha-separation.md:940-957`, `LilToonSourceAttestation.cs:2316-2325`). Include-tree rows are computed by the production digest function over each official tag tree, anchored by reproducing the pinned 2.3.4 digest (`Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:2383-2390`).

### What 2.4.0 admission requires

Combining the diff verdicts with the pin inventory, admission is a measured-data task. The steps are requirements of the procedure, not authorizations:

1. Admission requires a new admitted row `("2.4.0", treeDigest)` measured from an installed 2.4.0 package by the recorded procedure, and a moved `PackageVersion` constant, which was `"2.3.4"` as the newest admitted as of 2026-10-19 (`LilToonSourceAttestation.cs:361-362`).
2. Regular profile digests are expected unchanged, because the shader and pass assets are byte-identical in the tag trees. Each still requires re-confirmation from the installed 2.4.0 shapes under the reproduce-all-pins-first rule. This includes the pinned asset GUIDs and the meta files that carry them.
3. The Multi outline digest requires re-measurement. `ltsmulti_o.shader` and its base resource changed upstream. The `Hidden/lilToonMultiOutline` row sits at `LilToonSourceAttestation.cs:2313-2333`.
4. The opaque tuple, queue, and render type require re-measurement from the installed 2.4.0 package per the `LilToonOpaqueTarget.cs:26-30` mandate, before pins change.
5. The `_lilToonVersion` stamp requires re-confirmation on 2.4.0 materials. The property block shows no upstream change, so `45f` is expected to hold. Expectation, not assumption. Re-measure.
6. The canonicalization vocabularies require confirmation against the 2.4.0 generator output. `OfficialSettingIdentifiers` count is pinned at 109 by `LilToonAttestationTests.cs:1242`. Extension happens only with closed-domain proof.
7. Every changed literal requires mirroring into the test fixtures that retype them, including the duplicated prior-tree fixture constants at `Tests/Editor/Semantics/LilToonSourceAttestationTests.cs:190-195`.
8. The supported-version strings in `README.md` and the package `CHANGELOG.md` will need updating.

### Known gaps in test coverage

| Gap | Statement | Evidence |
|---|---|---|
| G1 boundary breadth | No test names `2.4.0` or other next-minor forms. The only refusal witness version is `2.3.5`, used at two sites: the identity path at `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:1762` and the cutout path at `:2374` | `LilToonAttestationTests.cs` |
| G2 real-file drift | Every digest mismatch test feeds synthetic evidence. No test proves a changed vendor file reports `ModifiedShaderSource` through a real gather | `TryVerifyIncludeTreeForVersion` at `LilToonSourceAttestation.cs:2122-2153` is fixture-fed only |
| G3 no-modal guarantee | No architecture test forbids modal UI outside the consent dialog. No test proves an EditMode pipeline run never reaches the presenter | Single batch-mode unit at `VersionConsentTests.cs:190-208` |
| G4 consent memory invariant | The never-persist rule lives in a comment only. No test sweeps for consent persistence | `VersionConsentDialog.cs:7-10` |
| G5 admitted-set visibility | Tests duplicate admitted values as fixture literals. No test enumerates the private admitted set, so a row edit can pass for the wrong reason | `Tests/Editor/Semantics/LilToonSourceAttestationTests.cs:190-195` |

The research package touches the pins in one place. `Tests/Editor/Calibration/CensusVendorProbe.cs` reads the attestation constants in its probe method, around `:74-88`, rather than literals, so a `PackageVersion` bump moves the probe automatically. `VendorReachabilityTests` asserts installed version equality against that constant, which is stricter than production admission if older rows stay admitted.

## 6. Synthesis

1. The tag-tree diff shows no change to the files the alpha proofs cite. This is diff-level evidence over upstream tag trees, not proof about installed bytes. Admission stays gated on re-measurement from installed 2.4.0 shapes, including GUID and meta re-confirmation. `INFERENCE` on the conclusion: no change was found in this diff that alters the semantics the alpha proofs cite.
2. Admission of 2.4.0 is a measured-data task with one genuinely new digest, the Multi outline asset, plus the new include-tree row and re-confirmation of everything else from installed shapes.
3. At least one established cause exists for the stall: the interactive test runner can reach the production consent dialog through the AAO suite, and no test-side mechanism intercepts it. The mechanism predicts one armed dialog per real-build test, and the observed record shows one five-minute block with the rest of the run unaffected. The count and dismissal timeline were not captured. The resolver dialog is a plausible contributor. The design must remove the stall class for every modal source, product or vendor.
4. Owner requirement: tests must run and fail without stalling. A test-only presenter interception meets this directly. It lets the real-shader suite run, auto-answers decline, and produces ordinary failures with ordinary reports. An environment gate in the style of the DAO suite would make the tests skip instead of run, which does not meet the stated requirement on its own. One limit of an auto-decline default: refusal-asserting tests would pass for the wrong reason if a future admission change broke a previously admitted version, because an intended decline-refusal and a false attestation refusal share one signature. The design needs a synthetic-subject exercise of the interception path so it cannot rot, and a scoping and lifecycle rule for the switch.
5. Test strengthening targets the five gaps above. The vendor diff forces no new semantic tests because the diff changed no alpha or render semantics, subject to the installed-shape re-measurement gate in item 1.

## 7. Open questions for design

1. Should the admitted set keep the 2.3.x rows when 2.4.0 lands, or retire them? Precedent keeps every measured row. The research gate becomes stricter than production if rows stay while the newest-admitted constant moves.
2. Should the Multi container rows extend to 2.4.0 in the same change, or stay at 2.3.4 until the Multi outline digest is re-measured from two installed shapes? The upstream queue-tag change makes a digest difference the expected outcome, because the queue tag is part of the asset bytes the canonicalizer reads. Expectation, not measurement.
3. What default answer should a test-only presenter interception give? Decline matches the batch fail-closed direction. The masking limit in section 6 item 4 applies either way and must be answered with a synthetic-subject test plus a lifecycle rule.
4. Should a suite-wide no-modal architecture test land with this work, so the stall class cannot return silently, and should it cover vendor package sources as well as the product package?

These questions have conservative answers that follow repo precedent. The design document picks them or escalates.
