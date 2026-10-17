# Alpha Separator Submenus Investigation

Date: 2026-10-16

Base: `main` at `13d01ba`. Work branch: `feat/alpha-separator-submenus`.

Privacy note: this investigation read only product code in this repository. It read no private avatar data and no Census data.

This document records facts about the current code. It changes nothing.

Evidence labels:

- `[SOURCE]` A fact read in this repository at the base commit. The citation gives a repository-relative path and line numbers.
- `[INFERENCE]` A deduction from listed facts. The deduction is named.
- `[RECOMMENDATION]` A proposed next step for the design round.

## 1. Purpose

The owner asked for two new submenus inside the Alpha Separator Settings foldout.

1. A submenu named `Transparent`.
2. A submenu named `Cutout`.
3. All existing options except the anisotropy options move into `Transparent`.
4. Where cutout sources follow the same proofing logic as transparent sources, `Cutout` gets its own matching options. These options are not true duplicates. Each one governs cutout proofing separately.

This note records the state the design must touch. It covers the inspector surface, the proof pipeline seams, the preset system, and the test pins. It ran three parallel read-only sweeps over the inspector surface, the proof pipeline consumption, and the preset, documentation, and test-pin surface. An adversarial accuracy review followed.

## 2. The owner's words map to code families

`[SOURCE]` AMUSE groups captured materials into families. `CapturedAlphaMaterialFamily` has the members `Unsupported`, `Poiyomi`, `PoiyomiTwoPass`, `LilToon`, `LilToonCutout`, `LilToonTransparent`, and `LilToonMulti` (`Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:14-52`).

`[INFERENCE]` The UI word `Transparent` maps to the sources AMUSE treats as AlphaBlend at base `13d01ba`. These are lilToon transparent, lilToon Multi mode 2, and every Poiyomi material without a cutout declaration. The UI word `Cutout` maps to the sources AMUSE treats as AlphaTest at base `13d01ba`. These are lilToon cutout, lilToon Multi mode 1, and every Poiyomi material with a cutout declaration. The design round should confirm this reading.

`[SOURCE]` The Poiyomi split is per material, not per family. `AlphaPredicateRequestFor` picks a declaring request for a cutout material and the plain variant otherwise (`UnityMaterialSemantics.cs:804-828`). A Poiyomi material declares its cutoff on the `_MainTex` request (`Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:2829-2842`).

`[SOURCE]` The lilToon split is per family. The cutout request declares the cutoff on `_MainTex` and the transparent request does not (`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaEvidenceRequests.cs:175-178,240-246`).

`[SOURCE]` The `PoiyomiTwoPass` family runs the same per-material predicate split (`Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:821-823`). Its conversion refuses through the unsupported-family default (`UnityMaterialSemantics.cs:22-31`), so no submenu value changes its conversion outcome. Its capture still follows the cutout split.

## 3. The settings surface at base `13d01ba`

### 3.1 The rows

`[SOURCE]` The inspector is `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`. The foldout `Alpha Separator Settings` draws ten rows in this order (`AmuseAvatarOptimizerEditor.cs:677-698`):

| # | Label | Control | Serialized field | Default |
|---|---|---|---|---|
| 1 | Smallest Tested Mipmap | Popup | `_preserveTransparencyMaxMipLevel` | 4 |
| 2 | Smallest Tested Texture | Popup | `_preserveTransparencyMinTextureSize` | 128 |
| 3 | Minimum Opaque Coverage to Convert (Per Material) | Slider | `_minimumOpaqueCoveragePercent` | 25 |
| 4 | Minimum Opaque Coverage to Preserve (Per Material) | Slider | `_minimumOpaqueCoverageToPreservePercent` | 5 |
| 5 | Alpha Upper Clamp to Convert (Per Texture) | Slider | `_minimumOpaqueAlphaPercent` | 100 |
| 6 | Minimum Opaque Coverage to Convert (Per Polygon) | Slider | `_polygonMinimumOpaqueCoveragePercent` | 100 |
| 7 | Alpha Upper Clamp to Convert (Per Polygon) | Slider | `_polygonAlphaUpperClampPercent` | 100 |
| 8 | Accept Desktop Anisotropy Differences | Toggle | `_acceptDesktopAnisotropyDifferences` | true |
| 9 | Accept Mobile Anisotropy Differences | Toggle | `_acceptMobileAnisotropyDifferences` | true |
| 10 | Active Sampling Proof | Read-only row | none | none |

`[SOURCE]` The fields live on `AmuseAvatarOptimizer` in `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs:41-144`. Rows 1 and 2 store -1 as an all-levels or all-sizes sentinel. Rows 3 to 7 carry `[Range(0, 100)]`. Row 7 uses the stored 100 as an inert sentinel (`Runtime/AmuseAvatarOptimizer.cs:95-107`).

### 3.2 The anisotropy group

`[SOURCE]` Rows 8 to 10 form one group. `DrawAcceptanceControls` draws both toggles and then the proof row (`AmuseAvatarOptimizerEditor.cs:710-773`). The proof row calls `PlatformAlphaSamplingPolicy.TrySelect` on each repaint. It persists nothing. It only reports which acceptance setting the active build target reads.

`[INFERENCE]` The owner's words `besides anisotropy` cover three rows: the two toggles and the proof row. The proof row is the display half of the acceptance pair, so it stays beside the toggles.

### 3.3 The foldout mechanics

`[SOURCE]` `RowContents` at `AmuseAvatarOptimizerEditor.cs:655-667` lists all ten row contents in draw order. It is the single source of truth for the draw calls and for the shared label width measured at lines 685 to 688. One `serializedObject.ApplyModifiedProperties()` at line 698 stores every row.

`[SOURCE]` The foldout state `_alphaSeparatorOpen` is a private non-persisted bool. Its initializer is true (`AmuseAvatarOptimizerEditor.cs:22`), so it resets open after every domain reload. The sibling transformations foldout follows the same open convention (`AmuseAvatarOptimizerEditor.cs:31`). A doc comment states the foldout states live in the editor session, so presets, Undo, and Reset stay untouched (`AmuseAvatarOptimizerEditor.cs:24-28`). The test hook `OnFoldoutHeaderDrawnForTest` fires once per drawn header with the header text (`AmuseAvatarOptimizerEditor.cs:679-680`).

`[SOURCE]` The whole foldout stays hidden until the `Advanced Settings` reveal in the `Settings` foldout turns on. The reveal is the serialized `[ToggleLeft]` field `_advancedSettingsRevealed` (`Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs:150-158`). The reveal toggle persists. The foldout states do not.

`[SOURCE]` The alpha-policy rows 5 to 7 draw together in `DrawAlphaPolicyControls` (`AmuseAvatarOptimizerEditor.cs:888-915`). The polygon clamp normalizes against the per-texture clamp on every change through `NormalizePolygonClamp` (`AmuseAvatarOptimizerEditor.cs:917-941`). The pair rule keeps the tolerated noise band strictly below the opaque percent (`Packages/com.alrauna.amuse/Editor/Analysis/AlphaPolicyBounds.cs:50-60`).

`[INFERENCE]` Rows 5 to 7 must stay in one menu. The clamp normalization reads the per-texture clamp drawn earlier in the same repaint. A split across two submenus would couple two foldouts through a hidden draw-order dependency.

## 4. How each setting reaches the proof

### 4.1 The two capture arms

`[SOURCE]` The capture path splits on the declared cutoff. A texture request can declare a `CutoutThreshold`. Capture reads the scalar and stores it per request (`Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs:1268-1298`). A declared cutoff below one binarizes the field and switches the alpha policy off for that source. The code comment states the rule: a texel between the noise gate and the shader cutoff is discarded at runtime and must never read opaque, so a cutoff source keeps the policy inert on every route (`Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs:337-347`).

`[SOURCE]` All four capture routes enforce the rule:

| Route | Enforcement |
|---|---|
| GPU blit | `effectiveBounds = cutoffThreshold < 1f ? Inert : bounds` (`UnityAlphaFieldEvidence.cs:344-347`) |
| Disk read | the plain threshold route runs when a cutoff applies, and the masked policy route only without one (`Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs:103-110`) |
| Streaming | threshold-based verdicts gate on `threshold >= 1.0f` (`Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs:290-297`) |
| Generated | the route keeps inert bounds under a cutoff, exactly as the other routes (`Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs:140-148`) |

`[SOURCE]` Shared captures bucket per cutoff threshold. The shared builder is keyed by the cutoff value (`UnityMaterialEvidenceCapture.cs:1044-1046,1066-1081`), and assignments with different thresholds must never share one field (`UnityMaterialEvidenceCapture.cs:1083-1098`). The per-material predicate gate decides which requests declare a cutoff (`UnityMaterialEvidenceCapture.cs:1268-1275`, request doc at 399-410). Requests inside one bucket merge to the smallest requested raw mip cap (`UnityMaterialEvidenceCapture.cs:1103-1105`). The session cache key pairs the clamped cutoff with the bounds and the raw mip cap (`UnityAlphaFieldEvidence.cs:330-333`).

`[SOURCE]` The full-opaque fast fact requires `captureThreshold == 1f`. It computes for every route in the `CapturedTextureEvidence` constructor (`UnityMaterialEvidenceCapture.cs:543-557`).

### 4.2 The consumption map

`[SOURCE]` Every setting flows through one mapping block in the finish plugin (`Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:692-720`) and a twin in the dry run (`Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs:352-370`). The binding columns state the behavior at base `13d01ba`:

| Setting | Mapper | Consumed by | Binds cutout at this base | Binds transparent at this base |
|---|---|---|---|---|
| mip cap | `ProofMipCapFrom` (`Packages/com.alrauna.amuse/Editor/Analysis/RuntimeStateResolution.cs:655-657`) | `WithRawMipCap` at capture (`AmusePlatformFinishPlugin.cs:1226-1228`), proof scope (`Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:805-816`) | yes | yes |
| min texture size | `MinTextureSizeFrom` (`RuntimeStateResolution.cs:665-667`) | same proof scope, `TextureSizeScope` (`UnityRendererAlphaAnalysis.cs:845-860`) | yes | yes |
| coverage to convert | `MinimumCoverageFrom` (`RuntimeStateResolution.cs:675-678`) | slot decision (`Packages/com.alrauna.amuse/Editor/Analysis/SlotTransformDecision.cs:155-164,195-199`), preparation (`Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:364-368`) | yes | yes |
| coverage to preserve | `MinimumPreserveCoverageFrom` (`RuntimeStateResolution.cs:684-687`) | `PreserveSlotQualifies` (`SlotTransformDecision.cs:125-152`), preparation (`AlphaSeparationPreparation.cs:148-152`) | yes | yes |
| clamp to convert (per texture) | `OpaquePercentFrom` then `AlphaPolicyBounds.From` (`RuntimeStateResolution.cs:696+`, `Packages/com.alrauna.amuse/Editor/Analysis/AlphaPolicyBounds.cs:25-29`) | capture bounds on the masked chain | no, inert under a cutoff | yes |
| clamp to convert (per polygon) | `PolygonClampPercentFrom` then `NoiseBound` (`RuntimeStateResolution.cs:715+`, `AlphaPolicyBounds.cs:14-15`) | erase rule (`Packages/com.alrauna.amuse/Editor/Host/SourceImageMaskedChain.cs:98-104`) | no, inert under a cutoff | yes |
| coverage (per polygon) | `DensityCapFromCoveragePercent` (`RuntimeStateResolution.cs:733+`) | classifier window erasure (`Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:738-741,938`) | no in effect, erased texels exist only on the masked chain | yes |
| anisotropy acceptances | `PlatformAlphaSamplingPolicy.TrySelect` (`Packages/com.alrauna.amuse/Editor/Build/PlatformAlphaSamplingPolicy.cs:18-43`) | policy projection (`Packages/com.alrauna.amuse/Editor/Analysis/AlphaSamplingPolicy.cs:37-70`), classifier (`TriangleAlphaClassifier.cs:381-391`) | yes | yes |

`[SOURCE]` The clamp rows 5 and 7 have more consumers than the erase rule. The same `NoiseBound` gates streaming erasure (`Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs:290-294`) and reaches the GPU and generated shaders as `_NoiseBound` (`UnityAlphaFieldEvidence.cs:345-353`, `UnityGeneratedTextureEvidence.cs:146-152`). Both clamp percents also feed the disclosure marker (`AmusePlatformFinishPlugin.cs:713-715`). The binding columns stay as stated.

`[SOURCE]` The plan and slot-decision stages are family-blind. `SubmeshSeparationPlan` holds opaque and transparent ordinals with no family field (`Packages/com.alrauna.amuse/Editor/Analysis/MeshSeparationPlanner.cs:107-123`). `SlotTransformDecision.Decide` takes only percents (`SlotTransformDecision.cs:67-111`). The family-specific behavior starts at capture request selection and at conversion eligibility (`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs:25-41` holds the cutout clip bound, and the repo forbids sharing it at lines 14 to 17).

## 5. Which options have a cutout analog at base `13d01ba`

### 5.1 The structural split

`[INFERENCE]` The seven moving options fall into three structural groups, and the group decides how a cutout twin works.

1. Per-slot decisions (rows 3 and 4). One material slot carries one source material with one family. A cutout twin of a slot decision reads the slot family and picks that family value. The seam is the family-blind slot decision and its preparation call sites.
2. Per-texture scopes (rows 1 and 2). One source texture can serve several materials across both families. A cutout twin must scope per family. The captured bytes stay per predicate.
3. Texture policy bounds (rows 5 to 7). These never reach a cutoff source. A cutout twin has no consumer at this base.

### 5.2 The per-texture scope pressure

`[SOURCE]` The chain builder stores only the prefix through the capture mip cap (`UnityAlphaFieldEvidence.cs:132-135,407-414`). A captured chain can narrow later at the proof scope through `LimitedTo` (`Packages/com.alrauna.amuse/Editor/Analysis/AlphaMipChain.cs:162`, applied at `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:824,835`). It can never widen.

`[SOURCE]` Captured bytes encode the capture predicate. `AlphaFieldKey` carries the threshold and the bounds, and its class doc states that 255 means different things per predicate (`Packages/com.alrauna.amuse/Editor/Host/AlphaFieldSet.cs:10-20`). Narrowing levels cannot re-binarize bytes.

`[INFERENCE]` One captured chain can serve both families only when both families read the same predicate. A texture shared across the two families already captures once per cutoff bucket at base (`UnityMaterialEvidenceCapture.cs:1066-1098`). Per-family scope values therefore ride each family's own request. Two named gaps remain for the design round. First, the stored field key has no cap dimension, so same-predicate requests that request different caps would collapse onto one stored chain (`AlphaFieldSet.cs:13-16,37-59`). Second, a source with two disagreeing deciding assignments fails closed as a refusal, which is not a per-family answer (`AlphaFieldSet.cs:137-217`, refusal at 214-217).

### 5.3 The applicability table

| Option | Cutout twin meaningful at this base | Seam for the twin |
|---|---|---|
| Smallest Tested Mipmap | yes | mapper fan-out, then per-family requests per 5.2 |
| Smallest Tested Texture | yes | same |
| Coverage to Convert | yes | family-aware slot decision (`SlotTransformDecision.cs:67-111`, `AlphaSeparationPreparation.cs:364-368`) |
| Coverage to Preserve | yes | `PreserveSlotQualifies` (`SlotTransformDecision.cs:125-152`), `AlphaSeparationPreparation.cs:148-152` |
| Clamp to Convert (Per Texture) | no | the inert-under-cutoff rule consumes nothing |
| Coverage to Convert (Per Polygon) | no | erased texels never exist on a cutoff source |
| Clamp to Convert (Per Polygon) | no | same as the per-texture clamp |

`[INFERENCE]` At base `13d01ba`, the Cutout submenu starts with four controls. The three alpha-policy rows are transparent-only by design. Their cutout twins would be dead controls. The owner's word `applicable` excludes them.

`[INFERENCE]` A cutout twin of the polygon coverage or either clamp becomes meaningful only if a future design gives cutoff sources a tolerance policy. That future work touches the capture contract, because the inert bounds are what the cutoff arm binarizes by (`UnityAlphaFieldEvidence.cs:337-347`). It is out of scope here.

## 6. What a new cutout field needs

`[SOURCE]` The plumbing recipe for one new proof-affecting serialized field:

1. Field, accessor, and doc comment on the component beside the existing fields (`Runtime/AmuseAvatarOptimizer.cs:41-148`).
2. A defensive mapper in the plugin block and its dry-run twin (`AmusePlatformFinishPlugin.cs:692-720`, `AlphaSeparationDryRun.cs:352-370`).
3. A consumer branch at the family seam (section 5.2 or 5.3).
4. A join in the dry-run cache fingerprint, which serializes the twelve mapped settings and the master switch (`AlphaSeparationDryRun.cs:1524-1546`).
5. A join in the PlayModeBuildStore record (`Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs:57-65`).
6. If capture changes, a join in the capture session keys (`UnityAlphaFieldEvidence.cs:330-333`).
7. If presets control it, the eight preset steps in section 7.

`[INFERENCE]` The two anisotropy acceptances stay global per the owner instruction. They bind both modes at base `13d01ba` (`AlphaSamplingPolicy.cs:37-70` projects per sampled field with no family input). A per-cutout acceptance would need a paired policy value and a second stale-policy comparison at apply (`Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:144-192`). Nobody asked for that, so this note records it as a known future seam and stops.

`[INFERENCE]` Each new cutout field should default to the same value as its transparent counterpart. A component serialized by an older version gets the field initializer value for the new field, so behavior stays identical after the upgrade. The named assumption is Unity serialization behavior: a field absent from old serialized data takes its initializer.

## 7. The preset system

`[SOURCE]` The closed schema is at version 3 (`Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs:18`). It carries eleven setting keys and one feature key (`PresetParser.cs:20-38`). `OptimizerPreset.Matches` compares all twelve mapped values, and a preset button presses only on a total match (`Packages/com.alrauna.amuse/Editor/Presets/OptimizerPreset.cs:66-92`). `PresetApplier` writes all twelve fields and never the master switch (`Packages/com.alrauna.amuse/Editor/Presets/PresetApplier.cs:19-50`).

`[SOURCE]` One new preset key needs eight steps: the parser key list, a typed read with a range reader, the preset property with a `Matches` clause, the applier write, all three shipped JSON files, a schema version bump, a README migration paragraph, and the five pinned test fixtures. The history precedent is version 2 adding the acceptance keys and version 3 adding the preserve key. Old files refuse as `UnknownSchemaVersion`, and nothing invents their missing values. One refused shipped file empties the whole preset row (`Packages/com.alrauna.amuse/Editor/Presets/PresetFileStore.cs:76-84`).

`[SOURCE]` `PresetApplier` states that the apply covers exactly the twelve mapped fields (`Packages/com.alrauna.amuse/Editor/Presets/PresetApplier.cs:8`), and that applying replaces both acceptance settings, so no earlier acceptance survives unless the selected preset grants it (`PresetApplier.cs:10-11`).

`[INFERENCE]` The design round has two honest options for presets. Option A adds cutout keys in schema version 4 and presets control both modes. Option B keeps cutout values out of presets, and presets then write only the transparent values. Option B needs a stated rule for the pressed state. The button would still press, because `Matches` compares only the mapped fields. The press would then misrepresent wholesale control while the cutout values differ.

## 8. Test pins

`[SOURCE]` The inspector pins. `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs:141-143` asserts the drawn headers contain `Show Material Transformations` and `Alpha Separator`, and that they do not contain `Alpha Separation`. No assertion names `Alpha Separator Settings`. The hook fires that header (`AmuseAvatarOptimizerEditor.cs:680`), but a test reaches it only when the test opens the `Settings` foldout and sets the reveal. The design round adds new pins instead of extending existing ones.

`[SOURCE]` The preset pins that a new key touches: `ValidSchemaParsesAllTwelveValues` (`Tests/Editor/Presets/PresetParserTests.cs:64-87`), `ShippedDefaults` at `Tests/Editor/Presets/OptimizerPresetTests.cs:10-14` and `FlippingAnyOneFieldValueBreaksTheMatch` at `:54-102`, `ApplyWritesAllTwelveFields` (`Tests/Editor/Presets/PresetApplierTests.cs:94-166`, which also pins that a preset never writes the master switch at line 163), the shipped-files test (`Tests/Editor/Presets/PresetFileStoreTests.cs:54-66`), and the `ValidBody` fixture (`PresetFileStoreTests.cs:12-33`).

`[SOURCE]` The wording pins. The NDMF refusal hint names only the desktop toggle label verbatim. The mobile toggle appears as the words `(or mobile)` (`Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:579-581`). The test asserts only the desktop substring (`Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:3299-3300`). Renaming the desktop label breaks that pair. No test pins the mobile label.

`[SOURCE]` The mapper pins. `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:1374-1470` pins the policy mappers as inert without a component, defensive out of band, and exact in band. New cutout mappers mirror this shape.

`[SOURCE]` Two fixture helpers write settings through the serialized object. `PinAllSizes` writes the minimum texture size (`Tests/Editor/Build/FixtureProofScope.cs:21-33`), and `AcceptDesktopAnisotropyDifferences` writes one acceptance field (`FixtureProofScope.cs:48-62`). The acceptance helper has 57 call sites across five test files. No helper writes the mobile acceptance.

`[INFERENCE]` New tests follow the repository conventions: one test class per production type, behavior-sentence names, RED before GREEN, and numbered falsifiers for the plausible wrong implementations. The natural falsifiers here are: a cutout value that leaks into transparent proofing, a transparent value that leaks into cutout proofing, and a preset press that reports matched while a cutout value differs.

## 9. Docs mentions

`[SOURCE]` `README.md` names the foldout and the toggles in four places: the preset bullet at lines 20 to 26, the anisotropy location sentence at lines 27 to 29, the proof modes section at lines 44 to 67, and the migration paragraph at lines 70 to 74.

`[SOURCE]` One README sentence is stale against the code. Lines 49 to 50 say new components leave both toggles off and the presets turn them on. The runtime initializers default both toggles to true (`Runtime/AmuseAvatarOptimizer.cs:132-133,143-144`), tests pin that default (`Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs:227-238,244-255`), and a fresh component matches the Normal preset (`Tests/Editor/Presets/OptimizerPresetTests.cs:31-50`, match assert at line 44). This defect predates this investigation. A docs-touching round should fix it or record it as a separate defect.

`[SOURCE]` Historical records under `docs/superpowers/` are never retro-edited. New UI wording gets new dated records. This note follows that rule.

## 10. Candidate shape for the design round

`[RECOMMENDATION]` The submenu skeleton:

1. `Alpha Separator Settings` keeps the anisotropy group directly under the foldout: the two toggles and the Active Sampling Proof row.
2. The `Transparent` submenu holds rows 1 to 7 with their current labels, fields, and defaults.
3. The `Cutout submenu` holds four new fields that mirror rows 1 to 4. Defaults equal the transparent values, so the upgrade is behavior-preserving.
4. Rows 5 to 7 stay transparent-only. The Cutout menu omits them. A future cutout tolerance policy is a separate design with capture-contract work.

`[RECOMMENDATION]` Field naming follows the mode it governs. One shape is a `cutout` prefix on each mirrored field, for example a cutout twin of `_preserveTransparencyMaxMipLevel`. The labels then need a per-mode scope marker, because the two menus draw in one shared label column. The design round decides the words.

`[RECOMMENDATION]` The per-texture scopes (rows 1 and 2) take per-family values on each family's own capture request. A texture used by one family sees no extra capture. A texture shared across the two families already captures once per cutoff bucket (`UnityMaterialEvidenceCapture.cs:1066-1098`), so no new capture appears at base behavior. The design must state the field-key invariant from section 5.2, because the stored key has no cap dimension.

## 11. Open points for the design round

1. Confirm the family mapping for the menu words in section 2.
2. Confirm the anisotropy group scope: the two toggles and the proof row stay directly under `Alpha Separator Settings`.
3. Confirm the Cutout menu starts with four controls and omits the alpha-policy rows.
4. Pick the per-family scope treatment for rows 1 and 2, and resolve the two field-key gaps in section 5.2.
5. Pick the preset option: schema version 4 with cutout keys, or inspector-local cutout values with a stated pressed-state rule.
6. Pick the field names, row labels, and tooltips.
7. Pick the default open or closed state for each new submenu foldout. At base `13d01ba`, `_alphaSeparatorOpen` initializes open (`AmuseAvatarOptimizerEditor.cs:22`), and the session-state comment at `AmuseAvatarOptimizerEditor.cs:24-28` keeps presets, Undo, and Reset untouched.
8. Join every new proof-affecting field into the dry-run fingerprint and the PlayModeBuildStore record.
9. Decide the fate of the stale README sentence in section 9.
10. Confirm that the per-slot coverage twins read the slot family and not the renderer majority, since one slot carries one material.

## 12. Verification record

The investigation ran three parallel read-only sweeps on 2026-10-16 at base `13d01ba`: the inspector surface, the proof pipeline consumption, and the preset, documentation, and test-pin surface. Spot re-reads in this session confirmed the inert-under-cutoff rule, the preset schema version, and the README conflict before publication.

Three parallel adversarial accuracy reviews then verified the sections against the code at the same base. The reviews confirmed the row table and draw order, the enforcement of the inert-under-cutoff rule on all four routes, the consumption map, and the applicability conclusion that rows 5 to 7 have no cutoff consumer. The reviews found 8 wrong statements, 7 citation drifts, 5 missed items, and 1 style finding. This document incorporated every confirmed fix:

1. The foldout state initializes open, not closed (`AmuseAvatarOptimizerEditor.cs:22`).
2. The shared-texture mechanism is one capture per cutoff bucket, not a strictest-cutoff merge (`UnityMaterialEvidenceCapture.cs:1066-1098`).
3. The generated route cites its own inert rule (`UnityGeneratedTextureEvidence.cs:140-148`).
4. One capture cannot serve two predicates, because captured bytes encode the predicate (`AlphaFieldSet.cs:10-20`).
5. The applier acceptance sentence governs only the two acceptance toggles (`PresetApplier.cs:8-11`).
6. Under the inspector-local preset option, the button still presses and misrepresents wholesale control.
7. No test pins the `Alpha Separator Settings` header, and the wording pin covers only the desktop label.
8. The corrected test ranges, README ranges, fixture-helper range, fingerprint wording, and base-qualified status wording.
