# Bundled locked original admission investigation

Status as of 2026-10-24. Read-only characterization. No product code changed. No test ran. Branch at investigation time: `main`.

Privacy note: this record covers AMUSE source, earlier AMUSE records, public vendor packages, and one live read-only probe in the Census Lab editor instance. The probe reports aggregate counts and fixed refusal sentences only. Private avatar, renderer, material, shader cache, and asset names do not appear. Exact private counts appear as ranges. Vendor paths use the placeholders `<poiyomi-10.0.25>` and `<thryeditor-2.74.3>`. AMUSE paths are relative to `Packages/com.alrauna.amuse/` unless they start with `docs/`.

Evidence labels:

- `[SOURCE]` A fact read in a file. The citation gives a path and line numbers.
- `[MEASURED]` A fact from a command, a scan, or a live probe that ran for this record on 2026-10-24.
- `[INFERENCE]` A conclusion from cited facts that no command checked.

## 1. Question

A private characterization avatar moved no triangles on its locked Poiyomi materials. An earlier answer in this session showed that the refusal is real. The bundled 9.3 rows of `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs` carry `LockedOriginalAdmitted` set to false. That is decision K8 of `docs/superpowers/specs/2026-10-23-poiyomi-per-shader-attestation-design.md`. This record asks what AMUSE needs before it can admit a locked material whose original shader is a bundled 9.3 shader. The record decides nothing.

## 2. Method

Four slices ran in parallel on 2026-10-24. All slices were read-only.

| Slice | Scope |
|---|---|
| Shader scan | Count constructs in the two bundled 9.3 shader files and compare them with the closed list of the aligner |
| Lock tool check | Compare the installed lock tool, thryeditor 2.74.3, with the verified facts of the alignment design |
| Surface map | Trace every gate, test, and document that depends on the flag |
| Live probe | Run the production aligner on the real generated locked shaders of the Census Lab scene |

The live probe called `PoiyomiLockedSourceAlignment.Align` through reflection. It read shader text and shader property tables. It wrote no asset and no material. It did not run a build.

## 3. What decides admission as of 2026-10-24

`[SOURCE]` Two rules decide whether a locked Poiyomi material can open the unlock window.

1. The identity rule. The recorded `OriginalShader` tag must equal the name of the shader that the recorded GUID resolves to. The matched row must also admit locked originals. The code is `LockedMaterialIdentity.AttestsLockedOriginal` at `Editor/Build/LockedMaterialIdentity.cs:277-301`.
2. The alignment rule. The aligner must explain the generated locked shader from the original shader text. The code is `AlignedFactsOf` in `Editor/Build/TransientUnlockSwapIn.cs:429-457` and `PoiyomiLockedSourceAlignment.Align`.

The flag is read in two places: `IsLockedOriginalAdmittedName` at `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:206-219` and `AttestsLockedOriginal`. Swap-in, reconstruction, admission, the memo, and the value view hold no release name and no digest. They do not change when the flag changes.

## 4. Findings

### 4.1 The lock tool is already in the era list

`[SOURCE]` The package `<poiyomi-10.0.25>` depends on thryeditor version 2.74.3 or newer. It contains no lock code of its own. It only calls the thryeditor `ShaderOptimizer`.

`[SOURCE]` thryeditor 2.74.3 has no behavior that depends on the shader family. The lock path uses the shader name only for naming, tags, and cache keys. No code names `Shaders/10.0`.

`[SOURCE]` The lock facts of the alignment design agree with 2.74.3 for facts 1 to 9 and 11 to 13. Fact 14 differs. In 2.74.3 the material group hash also records texture presence and texel size, and it prints colors and vectors at round-trip precision. The alignment design says the 9.3.64 tool ignored those. The difference is in the safe direction. The shared-shader explanation for a baked texel size mismatch cannot occur for a 2.74.3 lock.

`[SOURCE]` 2.74.3 does not rename a tag-2 property whose name ends in `UV` or `Pan`. The property stays live under its plain name. The investigation of 2026-10-23 already records this at its section 2.

`[INFERENCE]` A lock of a bundled material can only come from thryeditor 2.74.3 or newer. The era list holds 2.74.3. A newer tool version is not measured. The aligner runs every era and refuses text that no era explains, so an unknown version gives a refusal and never a false positive.

### 4.2 The bundled files stay inside the closed list

`[MEASURED]` A scan of the two bundled files found no construct outside the closed list of the alignment design. Both files have zero `// [Inlined]` markers, zero `GrabPass`, zero geometry, hull, or domain pragmas, zero `[maxtessfactor(`, and zero `_LightModes`. Both files include only Unity built-in files.

`[MEASURED]` The Toon file has 66,815 lines, 1,475 `//ifex` blocks, 148 distinct conditions, and 1,192 `#ifdef` and `#ifndef` lines. These match the release counts of facts 10 and 17, with 10 more lines in the Properties block. The Two Pass file has 104,570 lines, 2,217 `//ifex` blocks, and 150 distinct conditions. The two extra conditions test `_Stencil2Type`. Both files reach a nesting depth of 4. No condition uses `||`, `<`, `>=`, or `<=`.

`[MEASURED]` The five changed property names of the investigation of 2026-10-23 each occur once, on their declaration line. None occurs inside a program block. The lock cannot bake or rename them. None is read by the alpha proof.

`[MEASURED]` The Two Pass file declares `_FFBFStencilHelp0` and `_FFBFStencilHelp1` twice each. The Toon file has no duplicate property name. Fact 7 and rule A3.6 cover the duplicate Properties lines. The release file is not installed locally. The record cannot say whether the release file has the same duplicates. `[INFERENCE]` The 98-line Properties diff of the earlier investigation suggests that it does.

### 4.3 Locks that the flag cannot affect

`[SOURCE]` The release shader and the bundled shader share one asset GUID. A material locked while the release was installed records the release name and the shared GUID. After a move to a 10.x package, the GUID resolves to the bundled file. The recorded name does not equal the resolved name. The identity rule refuses the material at `Editor/Build/LockedMaterialIdentity.cs:283-289`. The flag change does not affect this case. That is the protection of K8.2. The scene probe did not observe this case.

`[MEASURED]` The Census Lab scene holds locked slots on two recorded original shaders. More than 90 slots record the bundled Toon name. They resolve to the 9.3 folder of the Poiyomi 10.0.25 package. Fewer than 10 slots record `.poiyomi/Poiyomi Toon`. They resolve to the 10.0 folder of the same package. That is the Poiyomi 10 shader. It has the same display name as the 9.3.64 release shader and a different asset. AMUSE does not support it, so the flag does not affect these slots.

`[MEASURED]` Correction. An earlier answer in this session called those slots locks on the release name. That was a guess. The probe shows that they lock the Poiyomi 10 shader.

### 4.4 The production aligner refuses every real lock in the scene

`[MEASURED]` The probe ran the production aligner on every distinct generated locked shader of the scene. The scene holds between 20 and 40 of them. Each run read the attested original text of the shader that the recorded GUID resolves to. In every run the recorded name tag equals the resolved shader name. The installed lock tool is thryeditor 2.74.3. The scene does not record which tool version made each lock.

| Aligner result | Share of distinct generated shaders |
|---|---|
| Aligned | None |
| "A block outcome stays undecided." at original line 10263 | About three in five |
| "The rename suffix of the generated names is ambiguous." | About one in four |
| "The original text includes a file outside the Unity include set." (the Poiyomi 10 shader locks of section 4.3) | About one in seven |

`[MEASURED]` Every generated shader that reached the full walk took between 44 and 82 seconds in the aligner. The runs ran one after another on a background thread. The refusals about includes took under one second.

`[MEASURED]` All block refusals stop at the same original line. It is the first `#ifndef VRC_LIGHT_VOLUMES_INCLUDED` line in the second program block. The line sits inside an `//ifex _LightingEnableLightVolumes==0` block. The same guard block occurs again at original line 47679 with the same text. Two generated shaders were read, and each holds the guard once.

`[INFERENCE]` Two identical guard blocks, of which the lock keeps one, can explain the refusal. The hypotheses "first kept, second removed" and "first removed, second kept" then give the same generated text. The aligner reports a tie at the end of the file at `Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:1043-1057`. This explanation is not proven.

`[MEASURED]` The suffix refusals carry no line number. They come from the whole-file suffix rule A4.2. The cause is not determined.

`[INFERENCE]` The text after the Properties block is the same in the 9.3.64 release file and in the bundled file (`docs/superpowers/investigations/2026-10-23-poiyomi-bundled-9-3-source-identity-investigation.md`, section 4). The block refusal can therefore also affect real locks of the release rows. No release shader is installed locally, so the record did not measure it.

## 5. Answer

The flag is not the blocker. Changing it alone is safe on this evidence and gains nothing. With the flag on, the aligner still refuses all real generated shaders of the scene. No triangle moves, and no false positive can occur.

The work that remains, in order:

1. Find the cause of the block refusal. Reproduce it with synthetic shader text first.
2. Find the cause of the suffix refusal. Reproduce it the same way.
3. Decide the time budget. Each distinct generated shader costs 44 to 82 seconds. If a build runs the aligner one shader after another, a scene with about 25 distinct locked shaders needs about 20 to 35 minutes. That is an inference. The record did not time a build.
4. Measure that the aligner accepts at least one real lock pair of the bundled Toon shader and one of the bundled Two Pass shader, in a project that is not the Census Lab. This is the locked-material counterpart of the K5 measurement. Record it in a dated investigation.
5. Then change the flag on the two bundled rows, update the tests and the documents of the surface table below.

The K5 measurement of 2026-10-23 stays valid for unlocked materials. It does not cover locks, because the aligner proof is separate from the property and source comparison of K5.

## 6. What this record does not cover

- No build ran. The refusal counts come from the aligner called directly, not from the build pass.
- The 9.3.64 release shader is not installed locally. No release lock was aligned. Any statement about the release case is an inference.
- No vendor lock ran. Locking mutates materials, so it must not run in the Census Lab project.
- The cause of each refusal in section 4.4 is not proven.
- Only the Toon shader was probed. The Two Pass shader has no locked material in the scene.
- thryeditor versions newer than 2.74.3 were not measured.

## 7. Open questions for the owner

1. Does the owner want the aligner refusals of section 4.4 fixed on their own branch, before any change to the flag?
2. What time budget does one distinct locked shader get in a build? The measured cost is 44 to 82 seconds.
3. Does the owner want the 9.3.64 release rows measured too? That needs a project outside the Census Lab with the release installed, because a lock must run there.

## Surface that changes if admission is adopted

`[SOURCE]` These rows come from the surface slice. They are a deduction from the code. Adoption stays a decision for the owner.

| Surface | Location |
|---|---|
| Flag value on the two bundled rows | `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:122-133` |
| Test that pins the bundled refusal | `Tests/Editor/Build/LockedMaterialIdentityTests.cs:198-217` |
| Row characterization tests | `Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs:418-541, 644-651` |
| Dry run deferral test | `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs:1772-1792` |
| Refusal text that names the bundled shader | `Editor/Build/AmuseReportStrings.cs:174-191` |
| Support statement | `README.md:42`, `Packages/com.alrauna.amuse/README.md:42`, `AGENTS.md`, `docs/architecture/vision.md:55`, `Packages/com.alrauna.amuse/CHANGELOG.md` |

The tag equality pins at `LockedMaterialIdentityTests.cs:219-251` stay. A live fixture cannot match a bundled row honestly, because a written test asset gets a random GUID and a random digest. A test must use a schema-only stand-in shader and the existing attestation seams, as `Tests/Editor/Build/LockedSourceFixture.cs` does.
