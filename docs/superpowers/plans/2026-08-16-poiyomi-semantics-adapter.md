# Poiyomi Material Semantics Adapter Implementation Plan

> **For agents that run this plan:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` by default. Use `superpowers:subagent-driven-development` only if the user separately authorizes subagents. Track execution with the checkboxes below.

**Goal:** Implement the approved exact, output-local Poiyomi 9.3.64 producer for the existing normalized `MaterialSemantics` core.

**Architecture:** One internal Editor-only Poiyomi producer first attests the canonical unlocked shader. The producer then interprets one supplied base `Material` into four independent semantic outputs and deterministic diagnostics. Direct Unity extraction stays local to the producer. A narrow verified-material friend-test seam permits deterministic public tests without bundling Poiyomi or inventing an adapter framework.

**Tech Stack:** Unity 2022.3.22f1, C#, UnityEditor `AssetDatabase`/`TextureImporter`/Package Manager APIs, Unity `Material`/`Shader`/`Texture`, SHA-256 from the standard library, NUnit EditMode tests, existing `Alrauna.Amuse.Editor` and `Alrauna.Amuse.Tests.Editor` assemblies.

## Global constraints

- The approved specification is `docs/superpowers/specs/2026-08-16-poiyomi-semantics-adapter-design.md`.
- Start only after the user approves the design and this plan. Work on `feat/poiyomi-semantics-adapter`, based on `0c22c7d`, unless the user asks for a fresh rebased branch.
- Use red/green TDD. Observe each focused red for the intended reason before production code. Then observe the same scope green.
- Keep all production types `internal` and Editor-only.
- Build results with the existing `MaterialSemantics` vocabulary. Do not change the vocabulary unless a new approval gate explicitly authorizes a core change.
- Do not modify `TriangleAlphaClassifier`, `ExactUvGeometry`, `MeshSeparationPlanner`, existing semantic types/tests, package metadata, asmdefs, manifests/locks, workflows, project settings, website, or private testbed.
- Do not add dependencies, copy Poiyomi source, download Poiyomi in CI, or add a registry/interface/framework.
- Treat each Unity asset and `.meta` file as one unit. Create new metadata through Unity import where practical, and inspect all new GUIDs.
- Do not commit, push, open a PR, publish, tag, or change settings without separate authorization.
- If a test shows that the approved closed core cannot represent a required case, stop at a new design approval gate. Do not grow an expression graph during execution.

---

## Planned files

**Create:**

- `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi.meta`
- `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`
- `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs.meta`
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi.meta`
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs`
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs.meta`
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiSemanticTest.shader`
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiSemanticTest.shader.meta`

**Modify only if Unity requires test-fixture registration:** none expected. The existing Editor and test asmdefs already cover descendants.

Keep entry point, result, diagnostics, source attestation, and tightly coupled helpers in the one production file. Split only if the approved implementation becomes materially harder to review. The tiny ShaderLab fixture is original AMUSE test content and must not copy Poiyomi implementation text.

---

### Task 1: Specify the result and conservative input boundary

**Files:**

- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs`
- Create after red: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`
- Create/import the corresponding folders and `.meta` pairs

**Interfaces:**

- `PoiyomiMaterialSemantics.AnalyzeBaseMaterial(Material)`
- `PoiyomiMaterialSemantics.InterpretVerifiedMaterial(Material, ColorSpace)` as the narrow deterministic friend-test seam
- `PoiyomiSemanticResult`
- `PoiyomiSemanticDiagnostic`
- `PoiyomiSemanticOutput`
- `PoiyomiSemanticDiagnosticCode`

- [ ] **Step 1: Add failing boundary/result tests**

Add tests that require:

- A null material throws `ArgumentNullException`.
- A destroyed material or a null shader throws `ArgumentException`.
- A valid non-Poiyomi shader returns `IsSupportedMaterial == false`.
- Unsupported results contain four unknown outputs and one Material diagnostic.
- Result diagnostics are immutable defensive data in Material/BaseColor/Alpha/Emission/Normal order.
- A retrieved semantic result never retains the input material.

Run the focused test class through the verified public `<repo-root>` Unity instance:

```text
test_names: Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiMaterialSemanticsTests
include_failed_tests: true
```

Expected red: the test assembly cannot resolve the new Poiyomi types. Do not use the private avatar testbed as a substitute if the public project is unavailable.

- [ ] **Step 2: Add the minimum result model and entry point**

Create the namespace `Alrauna.Amuse.Editor.Semantics.Poiyomi`. Use one small closed diagnostic enum with only the approved codes. Give each diagnostic value an output, a code, and non-null detail. Copy diagnostics once into a read-only collection. The unsupported helper constructs `MaterialSemantics` with all four outputs explicitly unknown. The public production entry supplies `QualitySettings.activeColorSpace` to the verified interpreter. Tests can supply explicit resolved color-space evidence without changing project settings.

Do not log, serialize, localize, add severities, attach Unity objects, or define a generic adapter contract.

- [ ] **Step 3: Run the focused tests green**

Expected: boundary/result tests pass with no unexpected Console errors. Inspect the diff before you continue.

---

### Task 2: Attest the exact canonical Poiyomi source

**Files:**

- Modify: production and test files from Task 1

**Pinned constants:**

```text
Shader name: .poiyomi/Poiyomi Toon
Package: com.poiyomi.toon
Version: 9.3.64
Shader GUID: 9444ce77bf4418748b1e8591b9d97f85
Normalized SHA-256: 31f2ff15615c5e2ac9b05fea08b6310731394d1b5a928b16048e7bde8f8b1755
```

- [ ] **Step 1: Add failing source-normalization and identity tests**

Cover:

- Optional UTF-8 BOM removal.
- LF, CRLF, and CR produce the same normalized bytes.
- One changed source character changes the hash.
- Wrong shader name, wrong GUID, wrong package name/version, missing source, and wrong hash each fail closed with the correct material diagnostic.
- A legacy Assets install can pass without package metadata when name/GUID/hash/schema match.
- The producer rejects an alternate official-looking name and `_ShaderOptimizerEnabled != 0`.
- A missing required property is modified/unsupported source evidence, not an exception.

Do not place the official Poiyomi shader in the repository. Test the normalizer with original short strings. Put identity checks behind narrow functions that accept already-read evidence.

Expected red: identity currently accepts or cannot evaluate these cases.

- [ ] **Step 2: Implement exact identity verification**

Use `AssetDatabase.GetAssetPath`, `AssetDatabase.TryGetGUIDAndLocalFileIdentifier`, `PackageInfo.FindForAssetPath`, `File.ReadAllText`, `Encoding.UTF8`, and `SHA256` directly. Normalize exactly as specified. Require the property schema used by Tasks 3–6.

Evaluation order and primary diagnostics:

1. Exact name/variant and unlocked state — `UnsupportedShader`.
2. Asset path/GUID/package evidence — `MissingSourceEvidence` or `UnsupportedVersion`.
3. Normalized source hash — `ModifiedShaderSource`.
4. Required property schema — `ModifiedShaderSource`.

Catch only expected I/O/evidence failures and return unsupported. Do not hide programmer errors with a catch-all.

- [ ] **Step 3: Run identity tests green**

Expected: all line-ending variants are deterministic, and every near miss returns all outputs unknown. Recheck the constants against the official tag before you accept an update to them.

---

### Task 3: Extract exact texture evidence

**Files:**

- Create before tests: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiSemanticTest.shader`
- Modify: production and test C# files

**Test-fixture properties:**

The original minimal shader exposes the exact property names/types/defaults that the verified interpreter needs: `_Color`, `_ColorThemeIndex`, `_MainTex`, `_MainTexUV`, `_MainTexPan`, `_MainPixelMode`, `_MainTexStochastic`, `_MainIgnoreTexAlpha`, `_BumpMap`, `_BumpMapUV`, `_BumpMapPan`, `_BumpMapStochastic`, `_BumpScale`, alpha controls, feature toggles, and four simple emission slots. Its SubShader can be a minimal pass. It must not reproduce Poiyomi equations.

- [ ] **Step 1: Add failing identity, UV, sampler, and importer tests**

Create temporary texture assets under a test-owned temporary Assets folder. Remove them in teardown. Cover:

- GUID/local ID token format, stable equality, sub-asset distinction where practical, and path rename stability.
- Generated/transient texture refusal, with no instance-ID fallback.
- UV0–UV3 plus finite ST conversion.
- Unsupported UV values, nonzero pan, stochastic mode, and pixel mode.
- Point/Bilinear plus equal Clamp/Repeat acceptance.
- Refusal of Trilinear, Mirror/MirrorOnce, U/V mismatch, mipmaps, nonzero mip bias, and anisotropy greater than one.
- Auxiliary texture sampling uses the assigned MainTex sampler.
- An assigned auxiliary texture with missing MainTex has unsupported sampling.
- sRGB and linear color-import interpretation.
- A missing importer for a color texture, source alpha present/absent, normal-map type, and normal green-channel inversion.

Expected red: the verified interpreter lacks these extraction rules.

- [ ] **Step 2: Implement local extraction helpers**

Add only the private methods that the producer needs: finite checks, exact toggle/mode checks, UV/ST conversion, asset token construction, main-sampler extraction, color interpretation, source-alpha proof, and normal-import proof. Return a small success/value-or-diagnostic result local to the file, or use direct `Try...` methods. Do not add a general result monad or a Unity extraction library.

Use the main texture's `filterMode`, `wrapModeU`, `wrapModeV`, mip count/bias, and anisotropy for every assigned Poiyomi 9.3.64 sample. Use the auxiliary asset only for identity, its own transform/UV, and importer meaning.

- [ ] **Step 3: Run extraction tests green**

Expected: all exact supported states map structurally to existing `TextureSourceId`, `UvMapping`, `TextureSampling`, and interpretation values. Every unsupported state has a deterministic reason.

---

### Task 4: Produce BaseColor and Alpha independently

**Files:**

- Modify: production and test C# files

- [ ] **Step 1: Add failing BaseColor equation tests**

Call the verified interpreter with explicit `ColorSpace.Linear` evidence. Do not change the public project's Gamma setting. Cover:

- Missing MainTex gives the linear `_Color.rgb` constant.
- Assigned MainTex with an identity tint gives an RGB texture sample.
- Assigned MainTex with a non-identity tint gives sample-times-linear-constant.
- `_ColorThemeIndex`, UV/pan/pixel/stochastic, sampler, identity, importer, and non-finite failures.
- Each traced main-color writer produces BaseColor Unknown.
- An Emission-only unsupported state leaves BaseColor complete unless its replace-base flag is enabled.
- Explicit Gamma evidence makes BaseColor unknown. Independently supported Alpha/Normal remain unchanged.
- The ordinary `AnalyzeBaseMaterial` path uses the project's actual `QualitySettings.activeColorSpace`, not a hard-coded test value.

Feature-gate tests must include at least one toggle from every source-writing group in the design. The groups are: color adjust, details, vertex/backface, RGBA mask, dissolve, decals, anisotropic replacement, matcap/cubemap, AudioLink color, flipbook, rim/depth-rim, glitter/stylized reflection, pathing/mirror/text/parallax, video/touch, voronoi/truchet, emission replacement, and premultiply.

Expected red: BaseColor is unknown or incorrect.

- [ ] **Step 2: Implement BaseColor production**

Read only the pinned properties. Normalize Color-property RGB to its linear shader value. Return the simplest exact existing kind: Constant, TextureSample, or TextureSampleTimesConstant. Use exact structural identity for `(1,1,1)`. Do not introduce epsilon comparison or algebraic simplification into the core.

- [ ] **Step 3: Add failing Alpha equation tests**

Cover:

- Force opaque under the simple visibility profile gives constant one.
- Force opaque plus a discard/coverage feature remains unknown.
- The ignore-MainTex-alpha control gives the `_Color.a` constant.
- Missing MainTex uses source-proven white alpha.
- Assigned MainTex gives an Alpha-channel sample or sample-times-constant.
- `_AlphaMod`, alpha map, distance/fresnel/angular/AudioLink alpha, vertex/backface alpha, masks, dissolve, decals, flipbook/rim/video/touch alpha, A2C, dithering, unsupported ignore-alpha values, and non-finite alpha invalidate Alpha.
- Render queue, cutoff, and blend mode alone do not change the normalized Alpha equation.
- A BaseColor-only unsupported state leaves Alpha complete.

Expected red: Alpha is unknown, or the implementation accepts an unsafe feature state.

- [ ] **Step 4: Implement Alpha production and run focused tests green**

Implement only the four approved forms. Do not invoke the classifier, inspect mesh UVs/pixels, infer opaque candidacy, or model cutoff/render state.

Expected: all BaseColor/Alpha tests pass, including output-local invalidation and exact diagnostics.

---

### Task 5: Produce Normal without widening its vocabulary

**Files:**

- Modify: production and test C# files

- [ ] **Step 1: Add failing Normal tests**

Cover:

- A missing BumpMap gives `Unmodified`.
- An assigned normal-map asset at unit strength gives `TangentSpaceNormalMap` with its own identity/UV and the MainTex sampler.
- Non-unit/negative/non-finite strength, unsupported UV, pan, stochastic, a missing MainTex sampler, a transient texture, non-normal import, or green-channel inversion gives Normal Unknown.
- Detail normal, RGBA-mask normal, decal normal, and parallax normal writers give Normal Unknown.
- Each Normal-only failure leaves otherwise supported BaseColor, Alpha, and Emission unchanged.

Expected red: Normal remains unknown, or the implementation accepts unsafe cases.

- [ ] **Step 2: Implement the two approved Normal forms**

Map the pinned `"bump"` default to `Unmodified`. For an assigned map, require the exact canonical Unity normal import and `_BumpScale == 1`. Do not add scale, channel-flip, blend, or multi-normal forms to `MaterialSemantics`.

- [ ] **Step 3: Run Normal tests green**

Expected: the approved normal path is exact, and every modifier fails closed at Normal only.

---

### Task 6: Produce the deliberately narrow Emission subset

**Files:**

- Modify: production and test C# files

- [ ] **Step 1: Add failing Emission tests**

Cover:

- All four slots disabled gives a zero constant.
- Slot 0 only, with no map, gives a linear emission color times finite strength.
- A map proven to have sampled alpha one gives an RGB sample or sample-times-constant.
- An RGBA map gives Emission Unknown, not a silent drop of sample alpha.
- Enabled slots 1–3, or multiple enabled slots, give Emission Unknown.
- Theme, base-color-as-map, replace, mask/global mask, fluorescence, center-out, scrolling, blinking, hue, light-based, AudioLink, unsupported UV/pan, sampler, identity, color import, Gamma project, and non-finite controls each give Emission Unknown.
- `_EmissionReplace0` also invalidates BaseColor. An ordinary unsupported emission modifier does not.
- An Emission failure does not invalidate Alpha or Normal.

Expected red: Emission remains unknown, or the implementation overclaims general Poiyomi equations.

- [ ] **Step 2: Implement only zero, constant, and alpha-one mapped emission**

Use the simplest existing Color semantic form. Require source-alpha absence plus importer alpha-none evidence before you treat mapped emission RGB as independent of sample alpha. Do not add Add, same-sample-alpha multiplication, layer arrays, or expression nodes.

- [ ] **Step 3: Run Emission tests green**

Expected: the narrow subset passes, and every known pressure case remains explicit Unknown.

---

### Task 7: Adversarial contract and identity integration tests

**Files:**

- Modify: test C# file only, unless a demonstrated defect requires the minimum production correction

- [ ] **Step 1: Add cross-output adversarial tests**

Add tests for:

- Exact diagnostic order, with one primary diagnostic per unknown output.
- A modified-source identity failure prevents the public entry point from reaching `InterpretVerifiedMaterial`.
- Line-ending-only source changes preserve the identity hash.
- A property value changed after analysis does not mutate the returned semantics.
- Missing MainTex plus an assigned auxiliary map does not invent sampler state.
- The same texture used by multiple outputs has equal source identity but independent UV/channel/interpretation roles.
- One shared unsupported MainTex sampler invalidates all assigned samples, while constant outputs survive.
- Force opaque never bypasses dissolve/discard/coverage refusal.
- The producer supports unlocked exact source. It rejects an official-looking alternate and a locked-looking generated shader.
- Unknown semantics never become an opaque classifier input or a separation-plan result. This is a representation/boundary assertion only. The test does not call or modify those systems.

- [ ] **Step 2: Run the entire Poiyomi test class**

Expected: zero failures and zero skips. Review every `Complete` assertion against the pinned shader source, not only against the production branch structure.

- [ ] **Step 3: Optional official-package integration**

Do this step only if the public Unity project already has the official `com.poiyomi.toon@9.3.64` package installed. Create purpose-built unsaved in-memory materials against the canonical shader. Verify identity plus representative simple output cases. Do not add or update the package merely for this optional step.

If you use the private testbed, first discover all Unity instances and verify the intended private project root. Do read-only inspection only. Do not save scenes, prefabs, materials, assets, settings, or package changes. Record that locked materials are intentionally unsupported.

---

### Task 8: Full verification and approval handoff

**Files:** none expected

- [ ] **Step 1: Run focused and full EditMode validation**

Run the focused Poiyomi test class on the verified public `<repo-root>` Unity instance. Then run all EditMode tests. Record total, passed, failed, skipped, duration, and relevant Console errors. Expected: zero failures/skips and no unexpected Console errors.

If no public Unity Editor is running, do not substitute the private testbed. Report the skipped validation and the exact reason.

- [ ] **Step 2: Run static boundary checks**

```powershell
rg -n "TriangleAlphaClassifier|ExactUvGeometry|MeshSeparationPlanner|CanBake|CanCombine|Expression|DAG|Registry|I.*Adapter|VRCFury|NDMF" Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi
rg -n "GetInstanceID|Texture2D\.GetPixels|ReadPixels|Graphics\.Blit|Debug\.(Log|LogWarning|LogError)" Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi
```

Expected: no prohibited production coupling. Test names and comments can mention a boundary only when the test asserts its absence. Inspect every match. Do not treat the command as a blind zero-match gate.

- [ ] **Step 3: Inspect Git and Unity asset scope**

```powershell
git diff --check
git diff --stat
git diff -- Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi
git diff --cached --check
git diff --cached --stat
git status --short
```

Confirm that only the two already approved docs and the planned Poiyomi production/test assets changed. Inspect each new `.meta` for a unique stable GUID and correct pairing. Confirm that the semantic core, classifier, geometry, planner, asmdefs, package metadata, manifests/locks, workflows, website, project settings, and private testbed are untouched.

- [ ] **Step 4: Re-run the semantic-core YAGNI gate**

Classify every newly observed unsupported equation:

- A: defer safely as Unknown.
- B: a generic extraction boundary that two concrete producers justify.
- C: one small new closed semantic form with concrete consumers.
- D: an expression DAG, only after repeated composition pressure.

Expected for this milestone: all current pressure remains A. No core change. If execution produces B, C, or D evidence, stop and write a design amendment for approval before you implement it.

- [ ] **Step 5: Report for review**

Report:

- Branch and base commit.
- Exact pinned Poiyomi source/version and attestation outcome.
- Supported forms and important refusal cases.
- Texture identity, UV, sampling, color-space/default handling.
- Output-local diagnostics.
- Focused/full test results and skipped validation.
- Semantic-core pressure and final YAGNI recommendation.
- All changed files and Git-scope checks.
- Public/private Unity MCP use, and whether either project was modified.
- Remaining risk, especially locked-material coverage.

Stop for review. Commit/push/PR/publishing/settings remain separately authorized.
