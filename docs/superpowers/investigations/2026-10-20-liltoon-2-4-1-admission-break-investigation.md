# lilToon 2.4.1 admission break investigation

Status as of 2026-10-20. Branch at investigation time: `feature/liltoon-2-4-1`, cut from `main` the same day.

Privacy note: this record covers a public upstream shader release and AMUSE's own pinned public constants. It contains no private avatar data. Machines and editor sessions appear by role only. The investigation downloaded the upstream tag trees from the official lilToon repository on 2026-10-20 into a scratch directory outside this repository. No path to that directory appears in this record.

## Purpose

lilToon 2.4.1 released on 2026-10-20. AMUSE admitted 2.4.0 one day earlier. The owner asked three questions.

1. What changed upstream between 2.4.0 and 2.4.1?
2. Why does AMUSE break with 2.4.1?
3. What does 2.4.1 support require?

This record characterizes. It decides nothing by itself.

Prior art: `2026-10-19-liltoon-2-4-0-attestation-and-test-stall-investigation.md` and `2026-10-19-separator-subswitches-and-liltoon-range-approval-investigation.md` in this folder. The second record describes the code before the approval range landed. The approval range and the consent suppression landed on 2026-10-20. This record describes the code after that landing.

## Method

Three parallel read-only investigations ran on 2026-10-20.

| Investigation | Scope | Mode |
|---|---|---|
| Upstream diff | Both official tag trees, the upstream commit, the release page | Network plus scratch trees outside the repository |
| Admission path | Identity conjunction, consent, capture, build, and preview behavior for an installed 2.4.1 | Read-only repo analysis |
| Alpha semantics impact | Per-input comparison of every classification input AMUSE reads | Read-only repo and tree analysis |

Claims carry `file:line` references. Product and test paths are relative to `Packages/com.alrauna.amuse/` unless stated otherwise. Upstream facts carry tree-relative paths or public URLs. Derived statements carry `INFERENCE`.

Synthesis re-verified the load-bearing claims by direct source reads on 2026-10-20.

* The owner decision comment and the approved-range constants (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:366-375`).
* The admitted rows (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:396-427`).
* The D2 pairing functions (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:642-739`, `:2819-2833`).
* The version disjunct (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:2326-2334`).
* The tree check and its digest matcher (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:2398-2441`).
* The packaged Multi tree check (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:2546-2566`) and the loose-install counterpart (`:2778-2800`).
* The D3 consent suppression (`Editor/Semantics/UnityMaterialSemantics.cs:588-606`).
* The D3 capture admission arm (`Editor/Semantics/UnityMaterialSemantics.cs:375-427`).
* The build capturer site (`Editor/Build/AmusePlatformFinishPass.cs:534-558`).
* The strict preview capturer (`Editor/DryRun/AlphaSeparationDryRun.cs:1026-1050`).
* The pairing test pins (`Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:1838-1881`).
* The D3 vendor fixture witness (`Tests/Editor/Build/VerifiedLilToonTestSeams.cs:459-481`).
* The Multi refusal words (`Editor/Semantics/LilToon/LilToonMultiResolution.cs:290-291`).
* The slot report string (`Editor/Build/AmuseReportStrings.cs:460-462`).
* The approved-range statement (`README.md:42`).

## 1. The upstream diff

2.4.1 is a single-commit patch release. The commit is `e698a2f791bd28dd1aabf227c74f14bedeca3587`, named "fix: Incorrect light probe brightness #378". The GitHub release body is empty. The changelog files are the only release notes. The changelog entry reads "Fixed: Incorrect light probe brightness" and carries the date 2026-10-20.

Exactly four files changed between the tags.

| File (tree-relative to `Assets/lilToon/`) | Change |
|---|---|
| `CHANGELOG.md` | New 2.4.1 section, four lines |
| `CHANGELOG_JP.md` | Same entry in Japanese |
| `package.json` | `version` `2.4.0` to `2.4.1`, and the release `url` field |
| `Shader/Includes/openlit_core.hlsl` | Three hunks, seven insertions, five deletions |

### The openlit hunk

`openlit_core.hlsl` ships the OpenLit lighting library. It is not new in 2.4.x. Exactly one file includes it: `Shader/Includes/lil_pipeline_brp.hlsl:19`. Every built-in-pipeline pass template includes that file, so every regular shader variant compiles it. The consumer maps the result into lighting fields only: `Shader/Includes/lil_common_macro.hlsl:2118-2122` writes the outputs into `o.lightColor`.

The change gives the light-probe evaluation its own direction.

* 2.4.0 tree, `openlit_core.hlsl:116-134`: `ComputeLightDirection` has one direction output. The probe direction equals the combined main light direction, which includes a per-axis absolute value and the custom direction override.
* 2.4.1 tree, `openlit_core.hlsl:116-136`: a second output `lightDirectionForSH9` carries `sh9Dir + mainDir` with no absolute value and no override. The probe evaluation `V = GetV(...)` at `:179-180` now uses this second direction.

This restores the formula that `Shader/Includes/lil_common_functions.hlsl:808-810` already used in both trees. Upstream issue 378 reports the brightness regression and names this file. `INFERENCE`: the intent is a lighting fix. The mechanism itself is observed.

Explicitly unchanged, checked against both trees: all property blocks, all render queue and `RenderType` tags, all generated shader and pass assets, all `.meta` files and GUIDs, all editor C# files, and the format stamp. `version.json` keeps value 45. `Editor/lilConstants.cs:12` keeps `currentVersionValue = 45`. The generated shaders keep `_lilToonVersion ("Version", Int) = 45`, for example `Shader/lts.shader:570`.

## 2. Why AMUSE breaks

AMUSE attests lilToon identity as a conjunction of pinned facts. The include tree is the only per-version bytes in that conjunction. The tree digest covers every non-meta file under the shader's `Includes` folder. The enumeration skips `.meta` paths (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:83-88`) and folds the rest into one digest (`:1765`). The 2.4.1 change touches `openlit_core.hlsl`, which lives in that folder. One changed file changes the whole tree digest.

The version machinery has two layers.

1. The admitted set is a closed row list, `AdmittedPackageVersions` at `Editor/Semantics/LilToon/LilToonSourceAttestation.cs:396-427`. It holds exact rows for 2.3.0 through 2.4.0. Each row binds one version to one tree digest.
2. The approved range is numeric. The floor is `2.3.0` and the upper bound is `2.5.0` (`:374-375`). A version inside the range is approved (`:460`). Approval is not admission. An approved version without a row pairs the greatest admitted row at or below it. This is the D2 nearest-row rule (`:642-681`, applied at `:723-739`).

For 2.4.1 the chain is:

1. The version term passes. 2.4.1 is not a row, but it is approved, so the disjunct at `:2326-2334` passes. No `UnsupportedVersion` refusal exists.
2. D2 pairs 2.4.1 with the 2.4.0 row. The expected tree digest is the 2.4.0 digest `549c3b552f8f001f2a2f6059acf4c8f0df6e9475f80b8d155784dabb68e7b670` (`:393-394`, row at `:426`).
3. The installed 2.4.1 tree digest differs, because `openlit_core.hlsl` changed. `TryVerifyIncludeTreeForVersion` compares the presented digest against the paired expectation (`:2431-2441`). The match fails. The diagnostic is `ModifiedShaderSource` with detail `Includes`.
4. The Multi path fails the same way at its tree step. For an installed package the packaged branch runs the same version-paired tree check at `:2546-2560`, and the refusal is `LilToonMultiResolutionRefusal.AttestationFailed` (`:2555`). `TryIdentifyMultiVersionByTree` (`:2562-2570`, `:2778`) is the counterpart for a loose install with no package version. It never runs for the primary install shape.

The root cause is one design assumption. The D2 nearest-row rule assumes an in-range patch ships the 2.4.0 include tree. 2.4.1 is the first in-range patch that changes a file inside the digested tree. The design has no measured answer for a byte-changed in-range patch, so exact attestation refuses the source as modified.

The test suite pins this exact shape. `ApprovedRangePatchWithTheNearestRowTreeVerifies` proves a byte-identical in-range 2.4.1 verifies through the 2.4.0 row. `ApprovedRangeVersionWithAForeignTreeReportsModifiedSource` proves a foreign tree answers `ModifiedShaderSource`, never `UnsupportedVersion` (`Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:1838-1881`). The D3 vendor fixture declares version `2.4.1` as its stand-in for an approved, non-row, byte-changed install (`Tests/Editor/Build/VerifiedLilToonTestSeams.cs:459-481`).

## 3. Observed behavior per surface

The owner decision of 2026-10-19 defines approved semantics: approved means the build never asks, and analysis runs under the current pins (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:366-373`). The D3 mechanism implements this. The attestation itself still fails closed on changed bytes.

| Surface | 2.4.0 install | 2.4.1 install |
|---|---|---|
| Version term | Admitted row | Approved, no row, pairs 2.4.0 |
| Include-tree term | Matches | Mismatch, `ModifiedShaderSource` |
| Consent dialog | None | None. The D3 suppression removes the ask for an approved version with an identity failure (`Editor/Semantics/UnityMaterialSemantics.cs:588-606`) |
| Build, regular material | Attested, proven | Admitted by the D3 approval arm (`Editor/Semantics/UnityMaterialSemantics.cs:375-427`) through the one build site that passes `admitApprovedVersions: true` (`Editor/Build/AmusePlatformFinishPass.cs:534-558`). Analysis runs with identity verification skipped. Outcomes match a 2.4.0 install. `INFERENCE` from section 4 |
| Build, Multi material | Attested, proven | Refused record. Analysis answers all-Unknown with the cause "the shader source does not verify" (`Editor/Semantics/LilToon/LilToonMultiResolution.cs:290-291`). The slot row reads "The material's lilToon Multi state is outside the states AMUSE can prove" (`Editor/Build/AmuseReportStrings.cs:460-462`). No separation happens for that material |
| DryRun preview, both families | Proven rows | Every triangle Unknown. The preview capturer stays strict and has no approval arm (`Editor/DryRun/AlphaSeparationDryRun.cs:1026-1050`) |

So the break has three faces.

1. The EditMode preview loses all proof for 2.4.1 materials.
2. Multi materials lose separation at build time and report the attestation cause.
3. Regular materials at build time lose their identity proof silently. The build trusts the approved range and analyzes under the 2.4.0 pins.

The conversion boundary shows the same trust decision. `IsApprovedSource` admits an approved version past the identity check (`Editor/Build/AlphaSeparationMaterialConverter.cs:134`, `:385-392`).

## 4. Semantic impact verdict

The third investigation compared every classification input AMUSE reads, across both trees.

| Input | Verdict |
|---|---|
| Include-tree digest | Changed. `openlit_core.hlsl` sits inside the digested folder |
| Package version string | Changed, `2.4.0` to `2.4.1` |
| Alpha mask math (`lil_common_frag.hlsl:452-483`) | Unchanged |
| Layer alpha terms in `lil_common_frag.hlsl` | Unchanged |
| Distance fade alpha arms (`lil_common_frag.hlsl:2013-2058`) | Unchanged |
| Dissolve and cutout discard (`lil_common_frag_alpha.hlsl`, full file) | Unchanged |
| Opaque alpha force and cutout discard (`lil_pass_forward_normal.hlsl:388-403`) | Unchanged |
| Depth-only pass (`lil_pass_depthonly.hlsl`) | Unchanged |
| `_lilToonVersion` stamp | Unchanged, 45 |
| Render queue values and `RenderType` tags | Unchanged |
| Multi outline queue tag `ltsmulti_o` | Unchanged |
| Blend factors per render mode | Unchanged |
| Shader, pass, and container names, GUIDs, canonical digests | Unchanged |

The three alpha-cited includes are `lil_common_frag_alpha.hlsl`, `lil_pass_forward_normal.hlsl`, and `lil_pass_depthonly.hlsl` (`docs/superpowers/specs/2026-10-19-liltoon-2-4-0-admission-and-headless-tests-design.md:105`). `openlit_core.hlsl` is not among them. Nothing alpha-related consumes its outputs. The changed functions emit light directions and light colors only.

Verdict: the 2.4.1 break is identity attestation only, not semantics. The alpha conclusions for a 2.4.1 material equal the 2.4.0 conclusions. This rests on a byte-level tree comparison, not on an installed-shape proof. The silent build-path trust in section 3 is therefore sound for the tag-tree bytes. It stays sound for the shipped package once the section 5 comparison item and the package-bytes check in section 6 land. That soundness is incidental. It comes from the upstream diff, not from a check.

Method limit of the comparison: the investigation ran without a shell, so whole-tree byte equality rests on directory listings, line counts, token sweeps with identical hit lists, and full reads of every cited region. The four changed files account for every observed delta.

## 5. What 2.4.1 support requires

Admission is a measured-data task. The attestation comments record the rules. Digest literals never come from the vendor repository, because its committed generated shaders are stale. New pins come from installed package shapes, and the recorded practice reproduces the existing pins first (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:377-412`).

1. Measure the 2.4.1 include-tree digest from installed 2.4.1 shapes on at least two settings shapes. Add the row to `AdmittedPackageVersions`.
2. Byte-compare the three alpha-cited includes between an installed 2.4.0 shape and each installed 2.4.1 shape under the pinned normalization. Record the result in the new row's comment, in the S11 style (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:396-412`). This closes the installed-shape leg of the section 4 verdict.
3. Confirm the regular shader and pass canonical digests reproduce on 2.4.1 shapes. The templates are unchanged upstream, so the existing pins are expected to hold. Expectation is not measurement.
4. Confirm the extended family digests the same way: cutout, transparent, outline wrappers, one-pass and two-pass variants.
5. Re-confirm the `_lilToonVersion` stamp 45 on installed 2.4.1 materials.
6. Measure the Multi container digests from two installed 2.4.1 shapes. Add the Multi row. The base and outline digests are expected to equal the 2.4.0 column values, because no Multi template changed. Expectation is not measurement.
7. Confirm the pinned GUIDs on the installed 2.4.1 package. No `.meta` file changed upstream.
8. Move the `PackageVersion` constant to `2.4.1` as the newest admitted, per precedent.
9. Update the documentation surfaces that name the attested maximum.

Test surfaces that move with the new row:

| Test surface | Pin as of 2026-10-20 | Required change |
|---|---|---|
| `AdmittedVersionRows_AreTheMeasuredSet` (`Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:1766-1793`) | Rows 2.3.0 through 2.4.0, both Multi rows | Add the 2.4.1 rows |
| Pairing tests (`:1838-1881`) | `2.4.1` and `2.4.9` as non-row witnesses | `2.4.1` becomes a row. A later patch, for example `2.4.2`, must become the witness |
| D3 vendor fixture (`Tests/Editor/Build/VerifiedLilToonTestSeams.cs:459-481`) | `ApprovedRangeVendorVersion = "2.4.1"`, an approved non-row version | Nothing breaks. The fixture digests mismatch every pin by construction. Its documented approved non-row identity becomes stale, so move the witness, or derive it at runtime per open question 2 |
| Digest audit and per-version tree cases (`:2242-2262`, `:2737-2790`) | 2.4.0 digest literals | Mirror the 2.4.1 row |
| Fixture row mirrors (`Tests/Editor/Semantics/LilToonSourceAttestationTests.cs:193-212`) | Duplicated row and Multi digest constants | Mirror the new rows |
| Approved-version witnesses (`Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs:965`, `:992`, `:1089-1091`, `:1112`) | Version `2.4.1` as the approved, non-row witness | Move to a version outside the rows, or derive the witness from the row list at runtime |
| No-row transport and state-refusal witnesses (`Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs:816-820`, `:1017-1018`) | A version-less refusal transport, and 2.4.0 as a captured version in a state refusal | Keep passing. Confirm |
| Zero-consent-subjects expectation (`Tests/Editor/Build/MergedConsumptionFixture.cs:140-142`, `:911-914`) | Installed version attests | Holds once 2.4.1 attests. Confirm |
| Research reachability gate (`Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs:98`, `:155-166`) | Reachable means a row member or any approved in-range version. An installed 2.4.1 passes it as of 2026-10-20 | No change needed for the gate itself |
| Research production-path gate (`Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs:471-479`) | The installed version is an exact member of `AdmittedRows()`. The last row equals `PackageVersion` (`:74-80`) | Fails on an installed 2.4.1 until the row lands. Moves automatically with the row and the constant |

## 6. Design considerations and open questions

The row mechanism is the recorded procedure for a new version. An alternative exists in principle: teach the pairing to forgive include changes that the semantic verdict proves irrelevant. This record does not recommend it. It would weaken a byte check on a general rule, and the row mechanism already covers the need without a new policy class.

1. Which version becomes the non-row pairing witness after 2.4.1 becomes a row? A future `2.4.2` is the natural candidate, but it does not exist yet. The tests can pin the pairing property with a synthetic version form instead. `INFERENCE`: `2.4.9` already serves as one such form in the existing test.
2. Should the D3 fixture witness move to `2.4.2`, or should the fixture construct its version from the row list at runtime so the witness survives every future row addition?
3. The DryRun stays silent about why 2.4.1 triangles are unproven. The build Multi path names the cause. Should the preview surface the same named cause for approved-but-unverified sources? This is a reporting coverage question, not an admission question.
4. The silent build-path trust for regular materials is an owner decision from 2026-10-19. The 2.4.1 row makes exact attestation pass and shrinks the trusted population back to genuinely unlisted versions. No consent behavior changes.
5. Does the shipped 2.4.1 VPM package carry the same include bytes as the tag tree? The trees match in file set, and the attestation discipline admits only installed-shape measurements. The row literal must come from an installed 2.4.1.

## 7. Verification record

- 2026-10-20: written from three parallel read-only investigations. Synthesis re-verified the load-bearing claims by direct reads. The verified list sits in the Method section.
- 2026-10-20: three parallel adversarial reviews ran on the draft: fact and citation verification, causal-chain attack, and conventions and sanitization. Together they reported two MAJOR and fifteen MINOR findings. This version integrates all of them. The largest corrections: the checklist gained the installed-shape comparison of the three alpha-cited includes, the Multi failure step now names the packaged branch, the research gate row separates reachability from exact membership, and several test citations moved to the real pin sites.
- 2026-10-20: two reviewers independently reproduced the whole-tree byte comparison of the two tag trees. Both confirmed exactly four changed files and byte-identical alpha-cited includes. One used a recursive diff. One used per-file hashes.
