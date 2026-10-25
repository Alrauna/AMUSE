# Bundled locked original readiness investigation

Status as of 2026-10-25. Read-only characterization before a design. No product code, test, or tracked document changed except this record. Branch at investigation time: `rework/vendor-integrated-tests`. Second revision, the same day: two adversarial reviews, one for evidence and one for soundness, both returned "accept with fixes". This revision integrates their findings.

Privacy note: this record covers AMUSE source, earlier AMUSE records, the public vendor packages installed in the dev project, and probes in the dev editor instance. Probe materials were throwaway assets that the probe created and deleted. No private avatar, renderer, material, or asset names appear. The private scene of the 2026-10-24 record appears only as its reported ranges. Vendor roots use the placeholders `<poiyomi-toon>` for the installed Poiyomi Toon 10.0.27 package and `<thryeditor>` for the installed thryeditor 2.74.3 package. AMUSE paths are relative to `Packages/com.alrauna.amuse/` unless they start with `docs/` or `Packages/`.

Evidence labels:

- `[SOURCE]` A fact read in a file. The citation gives a path and line numbers.
- `[MEASURED]` A fact from a command, a scan, or a probe that ran for this record on 2026-10-25, with the observed result.
- `[INFERENCE]` A conclusion from cited facts that no command checked.

## 1. Question

On 2026-10-25 the owner asked AMUSE to admit locked Poiyomi materials whose original shader is one of the two bundled 9.3 rows. The rows are `.poiyomi/Old Versions/9.3/Poiyomi Toon` and `.poiyomi/Old Versions/9.3/Poiyomi Toon Two Pass`. Both carry `LockedOriginalAdmitted = false` (decision K8 of `docs/superpowers/specs/2026-10-23-poiyomi-per-shader-attestation-design.md`, rows at `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:122-133`).

The record `docs/superpowers/investigations/2026-10-24-bundled-locked-original-admission-investigation.md` found that the flag is not the only blocker. Its section 5 lists the open work. This record answers that list so that a design can start. The record decides nothing.

## 2. Method

Four slices ran in parallel on 2026-10-25.

| Slice | Scope | Unity use |
|---|---|---|
| Block tie | Cause of the aligner refusal "A block outcome stays undecided." | Pure in-memory aligner calls. No asset. |
| Suffix | Cause of the aligner refusal "The rename suffix of the generated names is ambiguous." and the status of the K8.1 reasons | Pure in-memory aligner calls. No asset. |
| Lock probe | Real thryeditor 2.74.3 locks of both bundled shaders, then the production identity seam and aligner | Wrote scratch assets, then deleted them |
| Surface | Every gate, test, document, and time cost that the flag touches | None |

Every probe ran as a pure in-memory call or as a checked write in the dev editor instance. The block tie and lock probe notes record a check that `Application.dataPath` of the dev editor instance is the Assets folder of this repository before each write or long call. No probe touched the Census Lab. No NDMF build and no dry run ran.

## 3. Answer

1. Changing the flag alone is safe for correctness on 2026-10-25, and it moves no triangle. `[MEASURED]` Six of six fresh real locks refused in the aligner (section 4.1). `[INFERENCE]` With the flag on, they would still refuse there, because the identity gates pass on the measured tags and the aligner refusal comes next. The change would add aligner time to every build and every dry run with bundled locks (section 4.5).
2. The two reasons that K8.1 gives for the refusal are closed by the locked source alignment design as implemented (section 4.4). The refusal now rests on the aligner coverage, not on a proof defect. The value side of that closure has never run on a real aligned pair. Step 4 of section 6 covers it.
3. Two aligner defects can block a real lock. Every measured lock hit the block tie. The suffix refusal blocked none of the measured locks. Both are now explained and reproduced:
   - The block tie has a proven cause that is general, not specific to light volumes (section 4.2).
   - The suffix refusal has two proven triggers (section 4.3).
4. A real lock can run in the dev project. The 2026-10-24 record asked for a lock measurement in a project that is not the Census Lab. The surface slice of 2026-10-25 went further and said that a real lock can never run in this repository. The lock probe disproves that. The lock has two side effects that a test harness must handle (section 4.6).
5. The build cost is still open for the owner. The aligner runs serially once per distinct generated shader per build (section 4.5).

## 4. Findings

### 4.1 Real locks of the bundled shaders

`[MEASURED]` The lock probe created seven materials in a scratch folder. It loaded both bundled shaders by asset path, never by `Shader.Find`, because a 10.x shader carries the release name. Both loaded shaders carry the bundled row names and the canonical row GUIDs.

| Case | Shader | Shape |
|---|---|---|
| a | Toon | All defaults |
| b | Toon | Name with spaces and symbols. Base color tag 1, emission color tag 2. |
| c | Toon | Base color tag 1, emission color tag 2 |
| d | Toon | Light volumes 1, the declared default |
| e | Toon | Light volumes 0 |
| f | Two Pass | All defaults |
| g | Two Pass | Base color tag 1, emission color tag 2 |

`[SOURCE]` A tag here is the vendor material override tag `<property>Animated`. Value 1 keeps the property live under its plain name. Value 2 renames it with the cleaned material name as suffix (`<thryeditor>/Editor/ShaderOptimizer.cs:1362-1391`).

`[MEASURED]` The public vendor entry `Thry.ThryEditor.ShaderOptimizer.LockMaterials` locked all seven. Cases a and d share one generated shader, because their values are equal. Six distinct generated shaders compiled without error.

`[MEASURED]` Identity. Every locked material gave the same result: recognized locked identity true, original attested false, precheck refusal `LockedPoiyomiOriginalShaderUnattested`. Every recorded name tag equals the resolved bundled name. This is the K8 row rule, observed live (`Editor/Build/LockedMaterialIdentity.cs:277-301`).

`[MEASURED]` Aligner. The production aligner ran once per distinct generated shader, one after another on a worker thread. Six of six refused.

| Case | Refusal | Original line | Align time |
|---|---|---|---|
| a, d | A block outcome stays undecided. | 10263 | 30.6 s |
| b | A block outcome stays undecided. | 10263 | 30.7 s |
| c | A block outcome stays undecided. | 10263 | 30.1 s |
| e | A block outcome stays undecided. | 11431 | 26.0 s |
| f | A block outcome stays undecided. | 10345 | 52.7 s |
| g | A block outcome stays undecided. | 10345 | 53.0 s |

`[MEASURED]` No fresh lock gave the suffix refusal. Cases b, c, and g carry a renamed property, and b carries a symbol name that the vendor cleans to hex digits.

### 4.2 Cause of the block tie

`[SOURCE]` The aligner resolves each `//ifex` block and each `#ifdef` or `#ifndef` block without `&` or `|` with two hypotheses, kept and removed (`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:731-753, 882-1076`). A plain `#if` line is an ordinary line, not a block. The tie loop runs only when both hypotheses stay alive and the block body holds at least one surviving line (`:982-985`). Otherwise both hypotheses resolve the block as unobservable, with no tie (`:1062-1064`). In the loop, a lookahead of 8 surviving lines (`:95`) walks each candidate forward to the end of the enclosing block. When all candidates reach that end alive, the aligner throws "A block outcome stays undecided." at the block start (`:1053-1057`). When a nested block ties inside a candidate lookahead, the aligner rethrows that nested refusal (`:1009-1015`). So the reported line is the first nested tie that the walk reaches, not always the outermost undecided block.

`[SOURCE]` The first tie site of the measured locks in the bundled Toon file is a wrapper `//ifex _LightingEnableLightVolumes==0 && isNotAnimated(_LightingEnableLightVolumes)` at line 10261. Its body is one `#ifndef VRC_LIGHT_VOLUMES_INCLUDED` guard, lines 10263 to 11210, and blank lines. The wrapper closes at line 11212. No surviving line sits between the guard end and the wrapper end.

The tie has two shapes.

- M1, the guard is kept. The kept hypothesis consumes the guard text. The removed hypothesis consumes nothing. Both reach the wrapper end with nothing left to compare. The aligner ties at the guard line. This is the refusal at line 10263.
- M2, the guard is removed. The wrapper markers are comments, so the kept hypothesis of the wrapper consumes nothing through the removed guard. The removed hypothesis consumes nothing. Both candidates then walk the text after the block as identical twins. Such a wrapper ties at its own line only when nothing survives between it and the enclosing boundary. Otherwise the tie fires later, at the first nested undecided block, and the walk reports that nested line.

`[MEASURED]` A synthetic suite of six cases ran through the production `Align`. Every case with a kept guard tied at the guard line. That includes one single guard with no duplicate. Every case with removed guards tied at the wrapper line.

`[MEASURED]` A real text pair ran through the production `Align`: bundled Toon lines 9962 to 11240 under a minimal shader header, with a generated side that follows the vendor rules. The kept guard tied at the guard line in 130 ms. The removed guard tied at the wrapper line in 10 ms. The emulation needed the removal of the 26 `[branch]` markers in the guard body, per fact 13 of the alignment design. Without that step the walk refused with a different sentence.

`[SOURCE]` thryeditor 2.74.3 removes an `#ifdef` or `#ifndef` branch only when its keyword sits in the harvested `#pragma shader_feature` keyword set, outside a protected keyword set, and the keyword state asks for removal. An `#ifdef` goes only when the keyword is off. An `#ifndef` goes only when it is on (`<thryeditor>/Editor/ShaderOptimizer.cs:2172-2199`, `:261-262`). The harvest takes plain and local shader feature pragmas (`:2244-2254`). Before it parses, the lock calls the vendor keyword fixer, which sets keywords from the float values and enables the first option of a keyword enum (`<thryeditor>/Editor/ShaderOptimizer.cs:944, 1062`, `<thryeditor>/Editor/ThryEditor.cs:1519-1583`). `[MEASURED]` 356 lines of the bundled Toon file start with `#pragma shader_feature`. None of them, and none of 29 further commented pragma lines, names `VRC_LIGHT_VOLUMES_INCLUDED`. `[SOURCE]` Both light volume wrappers carry the same condition (`<poiyomi-toon>/_PoiyomiShaders/Shaders/9.3/Toon/Poiyomi Toon.shader:10261, 47677`). The vendor drops the body of an excluded wrapper (`<thryeditor>/Editor/ShaderOptimizer.cs:2047-2064, 2125-2144`). `[INFERENCE]` So the lock removes the light volume guards only through their wrappers, and it keeps both or removes both. Other guards follow the keyword state that the lock sets.

The prior hypothesis of the 2026-10-24 record was "two identical guard blocks, of which the lock keeps one". It is disproven as the cause. The duplicate is not needed, and the lock does not keep one of two. The lock probe note read the repeated guard text as support for that hypothesis. This record retires that reading. The 2026-10-24 record also reports that two generated shaders of the private scene held the guard once. The vendor rules predict zero or two copies. That count stays unreconciled, because no probe may read the private scene for this record. The proven mechanism does not depend on it.

`[SOURCE]` The site at line 11431 has the M1 shape: `//ifex _StochasticMode!=0` at line 11430 wraps only `#ifdef _STOCHASTICMODE_DELIOT_HEITZ`, lines 11431 to 11433, and closes at line 11434 (`<poiyomi-toon>/_PoiyomiShaders/Shaders/9.3/Toon/Poiyomi Toon.shader:11428-11443`). The keyword fixer enables `_STOCHASTICMODE_DELIOT_HEITZ` for the default value, so the lock keeps that guard.

`[INFERENCE]` Case e refused at line 11431, but the walk never decided the light volume wrapper at 10261. The lock removed the guard of that wrapper. Its two candidates consumed nothing and walked forward as twins, which is M2. Inside that lookahead the kept stochastic guard tied at 11431, which is M1. The nested tie rethrew through the wrapper lookahead, so the reported line is 11431. The block tie slice expected the wrapper line 10261 for this case. The measurement stands and replaces that expectation. After a fix for M1 alone, case e would still refuse, because the 10261 wrapper stays undecided under M2.

`[MEASURED]` The block tie slice counted 815 M1 capable sites in the bundled Toon file and 1325 in the Two Pass file. Review found that the count treated plain `#if` lines as blocks, which the aligner does not resolve. The Toon file holds 2667 resolvable blocks and the Two Pass file holds 4202. A review recount under the aligner block rules gives about 480 M1 capable sites in Toon and about 750 in Two Pass, with about 46 and 44 before line 10263. The recount is approximate, because it ignores `#else` bodies and some line details.

`[SOURCE]` The block extent finder counts directive lines with no block comment tracking, while the walk drops block comments first (`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:797-845`). `[MEASURED]` The bundled Toon file holds 20 directive shaped lines inside block comments, and the Two Pass file holds 32. No measured refusal shows a wrong extent on 2026-10-25.

`[INFERENCE]` The tie is a general aligner gap. A fix that targets only the light volume guard leaves hundreds of other sites. The first tie in walk order refuses the whole material.

### 4.3 Causes of the suffix refusal

`[SOURCE]` `FindSuffix` runs before the walk (`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:271-279`). It collects identifiers of the generated text that the original does not hold as whole tokens (`:399-466`), splits each one into suffix candidates (`:483-551`), and intersects the sets. Zero or more than one value throws the refusal with line 0 (`:371-397`). A wrong single suffix fails later at the walk. It cannot give a false positive (`:1721-1760`).

Trigger J, digit-led runs. `[SOURCE]` The identifier scanner starts a run only at a letter or an underscore (`:2368-2388`). The whole-token filter needs a non-word character on both sides (`:1453-1468`). A literal such as `0x7ffffff` is collected as `x7ffffff`. The filter does not find that form as a whole token, so it stays as an artifact with an empty candidate set. The intersection becomes empty. `[MEASURED]` A dump of the production `CollectArtifacts` on a kept-everything generated text of the bundled Toon file returned 11 artifacts. Ten were such literals, each with an empty set. `[MEASURED]` The literal sources sit in feature regions: AudioLink, LTCGI, and Voronoi. A lock at default values removes them, which agrees with the clean suffix stage of all six fresh locks.

Trigger N, a declared name inside the material name. `[SOURCE]` The 2.74.3 suffix is the cleaned material name, or the cleaned `thry_rename_suffix` tag when present (`<thryeditor>/Editor/ShaderOptimizer.cs:708-730, 752-755`). When the cleaned name holds a declared property name, the split finds extra split points.

- Tie shape: every renamed property has the length of the embedded name. The intersection holds two values.
- Straddle shape: the embedded name is longer than one renamed property and shorter than another. The sets are disjoint. The intersection is empty.

`[MEASURED]` The production `FindSuffix` on a clean synthetic pair gave these results:

| Material name | Renamed set | Result |
|---|---|---|
| `Body` | `_Color` | Suffix `_Body` |
| `Body_Color_1` | `_Color` | Ambiguous refusal, tie shape |
| `Body_Matcap_1` | `_Cutoff` | Ambiguous refusal, tie shape |
| `Cut_Cutoff_On` | `_Color`, `_EmissionColor` | Ambiguous refusal, straddle shape |
| `Cutoff_X` | `_Color` | Wrong suffix `_X`. The walk refuses later. |
| `Body` | `_MainTex` | Suffix `_Body` |

`[MEASURED]` A local model of the scan, checked against these production results, classified 31 synthetic names over six renamed sets. Eight names refused in at least one set. All eight had the tie or straddle shape.

`[INFERENCE]` The 2026-10-24 share of about one in four suffix refusals is a mix of both triggers. A grep of each generated shader for the literal shapes, plus the feature state at lock time, would split the share. That needs the private scene and stays open.

### 4.4 Status of the K8 reasons

`[SOURCE]` K8.1 gives two reasons: reconstruction does not read the 2.74 suffix tag, and the restore verification checks identity facts, not values (`docs/superpowers/specs/2026-10-23-poiyomi-per-shader-attestation-design.md:170`). The same reasons sit in the code comment at `Editor/Build/LockedMaterialIdentity.cs:269-273`.

- Reason a is closed. `[SOURCE]` The aligner takes only the original text, the generated text, and the property table (`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:185-188`). It derives the suffix from the text. Reconstruction builds its plan from the aligned record (`Editor/Semantics/Poiyomi/AlignedLockedSource.cs:23-49`, `Editor/Build/LockedMaterialReconstruction.cs:141-154`). `[MEASURED]` `thry_locked_rename_suffix` occurs only in test code. A test pins that a disagreeing tag is ignored (`Tests/Editor/Build/LockedMaterialReconstructionTests.cs:219-223`).
- Reason b is closed. `[SOURCE]` `RestoreMismatchReason` adds a seventh fact. It re-runs the define split and the removed block checks on the finished clone and ends in `LockedSourceReproduction.CheckConsistency` (`Editor/Build/TransientUnlockSwapIn.cs:323-410`). The restore writes keywords, the render queue, `RenderType`, and `VRCFallback` from the aligned facts (`Editor/Build/LockedMaterialReconstruction.cs:242-252`).

`[INFERENCE]` The value checks run only for a pair that aligns. No real bundled lock has aligned yet. So the clone, the restore, the seventh fact, the define split rerun, the color inverse conversion of A5.4, and the queue and tag restore have not run on real bundled input. Fail closed still holds, because a mismatch refuses. The exposure is coverage, not a false positive.

The fidelity findings 1 to 8 of `docs/superpowers/investigations/2026-10-23-locked-reconstruction-fidelity-investigation.md`, checked against bundled admission:

| Finding | Status under admission |
|---|---|
| 1, 1a, 4: suffix and tag | Closed. The suffix comes from text. No tag is read. |
| 2: queue, `RenderType`, `VRCFallback` | Closed by the restore plan. Stripped textures stay an accepted output gap. |
| 3: duplicate properties | Closed when aligned (A3.6). The bundled Two Pass file declares two stencil helper properties twice. |
| 5: baked literal changed after the lock | Closed (A5.4, A5.5) |
| 6: structure fixed at lock | Closed (A5.9, A6.2) |
| 7: original differs from the lock source | Closed by whole-text alignment. A release lock refuses against the bundled original, and the reverse. |
| 8.1: clone retention | To the design target. `[INFERENCE]` No build ran. |
| 8.2, 8.3: animations and property blocks | Closed as refusals (A10, A9). The curve rewrite of A10.4 stays a coverage loss. |

`[SOURCE]` K8.2 still holds after a flag change. A material locked on the release name whose GUID now resolves to the bundled file fails the tag equality gate first (`Editor/Build/LockedMaterialIdentity.cs:281-288`). The restore name fact would also refuse it (`Editor/Build/TransientUnlockSwapIn.cs:333-341`). The protection is the tag gate, not the flag.

`[INFERENCE]` No fidelity route becomes a new false positive under admission. Each route aligns or refuses.

### 4.5 Build cost

`[SOURCE]` The aligner runs in pass two of `SwapIn`, inside the finish pass, before capture (`Editor/Build/AmusePlatformFinishPass.cs:376-386`, `Editor/Build/TransientUnlockSwapIn.cs:179-200`). The loop is a plain `foreach` on the build thread. Preparation runs no aligner work. The dry run calls the same memo (`Editor/DryRun/AlphaSeparationDryRun.cs:1566-1569`).

`[SOURCE]` The memo holds one entry per generated asset path, keyed also by write time, original path, and original text digest (`Editor/Build/LockedSourceAlignmentMemo.cs:47-64`). A refusal is stored like a success (`:41-43`). `AmuseSessionCaches.Clear` empties the memo at finish pass entry and at the end of apply (`Editor/Build/AmusePlatformFinishPass.cs:206`, `Editor/Build/AlphaSeparationApply.cs:1085`). So each build pays the full cost again.

`[MEASURED]` Align time per distinct generated shader. The fresh rows come from the lock probe of 2026-10-25. The scene row comes from the 2026-10-24 record.

| Measurement | Time per distinct generated shader |
|---|---|
| Fresh Toon locks, 2026-10-25 | 26 to 31 s |
| Fresh Two Pass locks, 2026-10-25 | 52 to 53 s |
| Private scene Toon locks, 2026-10-24 | 44 to 82 s |

`[INFERENCE]` The fresh locks cost less than the scene numbers. The scene numbers likely include machine load. Plan with the higher range.

`[SOURCE]` Pass two skips a locked material whose identity check answers false before it calls the aligner (`Editor/Build/TransientUnlockSwapIn.cs:184-188, 200`). `[INFERENCE]` So on 2026-10-25 a bundled lock costs no aligner time. Admission adds the full aligner cost to every avatar with bundled locks, even while the aligner still refuses them, because a refused lock that reaches the walk pays the same cost as an aligned one. At the scene range, 20 to 40 distinct generated shaders cost about 15 to 55 minutes of aligner time per build.

`[INFERENCE]` Both numbers are early exit costs. Every measured walk refused in the first tenth of its file: line 10263 or 11431 of about 66,800 lines, or line 10345 of about 104,600 lines. A full successful walk can cost several times more, and no measurement bounds it. The dry run pays the same cost again, because it calls the same memo and the flag change routes bundled tags into the dry run locked arm.

### 4.6 Real locks as test input

`[MEASURED]` A real lock runs in the dev project. The surface slice stated that a real lock can never run in this repository. The lock probe disproves that. The probe ran the lock, cleaned up, and left `git status --short` equal to its starting state.

`[MEASURED]` The lock writes outside the material folder. The vendor writes each generated shader and a `.thrylock` sidecar to `Assets/_LockedShaderCache/<cleaned shader name>/<hash>/` (`[SOURCE]` `<thryeditor>/Editor/LockedShaderCache.cs:24-38`). The probe created that folder and deleted it.

`[SOURCE]` The lock makes a network call. `ShaderOptimizer` calls `Helper.RegisterEditorUse` at `<thryeditor>/Editor/ShaderOptimizer.cs:910`. That method sends one usage count per user and one per project to a vendor web counter, unless the editor preferences already record a count (`<thryeditor>/Editor/Helpers/Helper.cs:50-70`, URLs in `<thryeditor>/Editor/DataStructs/Constants.cs:21-22`). The vendor inspector calls the same method (`<thryeditor>/Editor/ThryEditor.cs:605`). `[INFERENCE]` The probe of 2026-10-25 can have sent that count from the dev editor instance. A test that locks needs an owner decision on this vendor network behavior.

`[SOURCE]` What can use real installed bundled files without a lock: the identity tests. A pinned row GUID resolves to the installed bundled file, which carries the pinned digest (`Editor/Build/LockedMaterialIdentity.cs:245-263, 351-374`). A test can set the tags on a material and exercise the real row end to end, with no asset write.

`[SOURCE]` What stays synthetic: a written copy of a shader gets a new GUID and digest, so it cannot match a row (`Tests/Editor/Build/LockedSourceFixture.cs:9-30`). Exact-control aligner refusal tests, such as the M1, M2, J, and N shapes, keep synthetic texts.

`[INFERENCE]` A real lock acceptance test is now possible in this repository. It needs a gate, such as the existing opt-in for integration runs that write project state. Its cleanup must delete its own materials first and its own cache entries after them. `[SOURCE]` The vendor documents that a deletion of cache entries that other locked materials hold breaks those materials (`<thryeditor>/Editor/LockedShaderCache.cs:29-36`).

## 5. Surface that changes on admission

`[SOURCE]` Current lines on 2026-10-25.

| Surface | Location |
|---|---|
| Flag on the two bundled rows | `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:122-133` |
| Name gate and its K8.4 comment | same file `:199-221` |
| Locked original seam and the K8.1 comment | `Editor/Build/LockedMaterialIdentity.cs:269-301` |
| Precheck refusal | `Editor/Build/LockedMaterialIdentity.cs:134-161` |
| Window eligibility gate, not in the 2026-10-24 table | `Editor/Build/TransientUnlockAvailability.cs:52-78` |
| Swap-in gate | `Editor/Build/TransientUnlockSwapIn.cs:95-96, 182-200` |
| Dry run locked arm | `Editor/DryRun/AlphaSeparationDryRun.cs:1639-1646, 1669-1679` |
| Test that pins the bundled refusal | `Tests/Editor/Build/LockedMaterialIdentityTests.cs:197-220` |
| Companion K8 seam tests | `Tests/Editor/Build/LockedMaterialIdentityTests.cs:181-195, 221-239, 240-255` |
| Row characterization tests | `Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs:421-652`, bundled flags at `:528, 541, 644-651` |
| Dry run deferral test | `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs:1770-1793` |
| Refusal text that names the bundled shader | `Editor/Build/AmuseReportStrings.cs:174-191` |
| Support statements | `README.md:42`, `Packages/com.alrauna.amuse/README.md:42`, `AGENTS.md:7`, `docs/architecture/vision.md:55`, `Packages/com.alrauna.amuse/CHANGELOG.md:8, 13, 21` |

## 6. Readiness order for a design

1. Fix the block tie for M1 and M2 as a class, not per site. Section 7 lists the shapes. The tests must cover a nested tie under an undecided ancestor, as in case e, and a block whose extent scan crosses a commented directive shaped line.
2. Fix trigger J and trigger N of the suffix rule. Also fix the Properties window detection. `[MEASURED]` In the bundled Toon file, unbalanced braces in display strings drive the brace count negative at source line 55, so only lines 5 to 54 count as Properties lines (suffix slice, 2026-10-25). A renamed Properties line after line 54 gives no artifact.
3. State how the tie and suffix fixes stay safe for the two admitted release rows. The aligner runs for them on 2026-10-25, and the bundled and release files share the text after the Properties block. The release shader is not installed, so the argument must rest on the synthetic refusal and acceptance tests of the A11 pattern.
4. Measure at least one real lock of each bundled row in the dev project through the whole swap-in path, not the aligner alone. Cover default values, renamed properties, and a feature such as AudioLink that keeps digit-led literals. The measurement must run the clone, the restore, the seventh fact, and the consistency checks, and must record every refusal sentence and the time of each full walk.
5. Decide the build and dry run time budget from the full walk times of step 4.
6. Before the flag change, add a test that pins the window eligibility and the consent note for bundled locked avatars. `[SOURCE]` `WindowEligibleForConsent` answers true when any locked material attests (`Editor/Build/TransientUnlockAvailability.cs:60-78`). Decide the report wording for a locked material that attests and then fails to align.
7. Then change the flag on the two bundled rows and update the surfaces of section 5.

Steps 1 and 2 are aligner changes. They need refusal and acceptance tests per the A11 pattern of `docs/superpowers/specs/2026-10-23-locked-source-alignment-design.md`.

## 7. Candidate fix shapes

The invariant: a wrong decision must never prove opacity that the locked shader does not have. A wrong kept or removed record can change which alpha lines count as live. A fix may only add decisions that the generated text forces, or keep refusing.

Block tie:

| Shape | Idea | Risk |
|---|---|---|
| T1, empty lookahead grounding | When no surviving line sits between the block end and the enclosing end, decide kept if the kept hypothesis consumed real text. Refuse when the text that the kept hypothesis consumed could also match at the position of the removed hypothesis. | Low only with that repeat check. A duplicated body after the block is the concrete wrong case: the kept hypothesis matches the later copy, and a removed body counts as live. M2 needs a separate rule, because both hypotheses consume nothing. |
| T2, lazy hypotheses across the boundary | Keep both candidates alive past the enclosing end. Decide at the first contradiction. Refuse at end of file or at a step cap. | Low for soundness. Higher cost and more state. Decides M1 only. The two candidates of an M2 wrapper sit at the same positions and never contradict, so T2 refuses them at the cap. |
| T3, vendor unit rule | Treat a wrapper whose body is one guard as one unit with two outcomes. Refuse unless the generated text matches one unit outcome and rules out the other. | The unit assumption is not a vendor rule on 2026-10-25. The lock removes keyword branches and wrapper bodies by independent rules, and it sets the keyword state itself. A kept wrapper with a removed guard matches the unit removed outcome, so a wrong record passes silently. A6 then forces wrong values on `U`. The rule needs a check that the two outcomes agree, or it must stay off keyword-gated guards. |
| T4, status quo | Keep refusing. | None. Admission gains nothing. |

`[INFERENCE]` In M2 both hypotheses produce the same generated text, so the choice cannot change the alpha proof. The choice still changes the record. `[SOURCE]` The removed hypothesis records nested blocks as `NotEvaluated`. The kept hypothesis records the inner guard as `Removed` (`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:943-946, 1095-1130`). Under A6.1 a removed `#ifndef K` demands `K` in the keywords of `U`. For `VRC_LIGHT_VOLUMES_INCLUDED` that demand can never be met, because the name is never a material keyword. The record shape also decides whether A6.3 forces values on `U`. A design must pick the record shape first and the opacity question second.

Suffix:

| Shape | Fixes | Risk |
|---|---|---|
| J1, compare the full maximal run | Trigger J | Low. It only drops artifacts that the original holds. |
| A, split only at closed vendor head shapes | Trigger N, both shapes | Low. The rename grammar is closed. |
| B, split only at a token boundary | Trigger N, tie shape only | Low |
| C, use the suffix tag as a selector checked against the text | Trigger N, all shapes, when the tag exists | Low to medium. Needs an amendment of A1.1, an input channel for the tag into the aligner, and a decision on the pinned test that ignores a disagreeing tag. |

Any split rule must keep the derived tail strip for `_ST` and `_TexelSize` names (`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:529-551`). Without it a renamed texture gives the wrong remainder, and every texture rename refuses.

## 8. What this record does not cover

- No NDMF build and no dry run ran. No aligned pair exercised reconstruction.
- The 9.3.64 release shader is not installed. No release lock ran.
- All probe locks used default textures. Locks with assigned textures add defines and texel bakes that these locks lack.
- The probe ran in the color space of the dev project. A lock made in a gamma project bakes colors differently.
- thryeditor versions newer than 2.74.3 were not measured.
- The full-file real pair of the block tie slice did not complete within the probe budget. The trimmed real pair and the synthetic suite carry the proof.
- The site counts of section 4.2 are approximate.
- The split of the 2026-10-24 suffix refusals between triggers J and N needs the private scene.

## 9. Open questions for the owner

1. Which block tie shape should the design use for M1: T1 or T2? Which rule should it use for M2, given that the record shape must respect A6.1 and A6.3 as section 7 states?
2. Which suffix shapes should the design use: J1 with A, J1 with B, or J1 with C?
3. What build time and what dry run time may one avatar spend in the aligner? The answer must wait for a measured full walk, because every number of 2026-10-25 is an early exit cost.
4. May a test run a real vendor lock in the dev project, given the vendor cache folder and the vendor usage counter call?
5. Should the flag change wait for steps 4 to 6 of section 6: the measured full swap-in of real locks, the time budget, and the consent and report decisions?
