# Show Material Transformations Dropdown

Date: 2026-10-13

Base: `3ff7ed6` on branch `feat/show-material-transformations`

This document records facts about the current code. It changes nothing.

Evidence labels:

- `[SOURCE]` A fact read in this repository at the base commit. The citation gives a repository-relative path and line numbers. A fact from a cited external primary source also carries this label.
- `[INFERENCE]` A deduction from named facts.
- `[RECOMMENDATION]` A proposal for the design round.

Paths under the AMUSE package drop the `Packages/com.alrauna.amuse/` prefix. Paths under the upstream optimizer name the external GitHub repository `d4rkc0d3r/d4rkAvatarOptimizer`.

## 1. Purpose

The owner requested a feature on a new branch: a "Show Material Transformations" dropdown in the AmuseAvatarOptimizer inspector. It holds an "Alpha Separation" dropdown inside. It shows the user what transformations will and will not occur across the entire avatar. The owner named d4rkAvatarOptimizer's "show mesh & material dropdown" as the model.

The owner set the workflow: parallel investigation, adversarial review of the investigation, design spec, adversarial review of the spec, implementation plan, adversarial review of the plan, then subagent-driven execution. Commits land between finalized changes. The run halts when a choice needs owner input.

This investigation covers the upstream reference behavior, the current inspector surface, the data a build computes, the lifetime of that data, the NDMF facilities in the pinned version, and the seam where the dropdown sits.

## 2. Reference behavior in d4rkAvatarOptimizer

[SOURCE] The upstream repository is `d4rkc0d3r/d4rkAvatarOptimizer` on GitHub. `d4rkpl4y3r` is the author handle in the package id and code namespace. It is not a GitHub path. Findings cite `Editor/` paths on the `main` branch at commit `d61f293` from 2026-09-29.

[SOURCE] The upstream label is "Show Mesh & Material Merge Preview". The exact string "Show Mesh & Material Information" does not exist upstream. The foldout binds to the serialized component field `ShowMeshAndMaterialMergePreview`. Its default is true (`Editor/d4rkAvatarOptimizer.cs:68`). The editor code draws it at `Editor/d4rkAvatarOptimizerEditor.cs:267-291`.

[SOURCE] The foldout shows one block per predicted mesh group. Inside a block, material slots that will merge into one material draw as one vertical column of rows. Each row is a renderer object field plus a material object field (`DrawMatchedMaterialSlot`, `Editor/d4rkAvatarOptimizerEditor.cs:1487-1505`). A lone row in a column means "kept as is". Merge versus keep is positional. No per-material verdict text or icon exists in the inspector preview.

[SOURCE] "Why not" reasons live in a separate window, not the inspector. The window `Editor/WhyNoMaterialMerge.cs` evaluates one picked pair of slots and prints the failure sentence from `CanCombineMaterialsError` (`Editor/d4rkAvatarOptimizer.cs:5081`).

[SOURCE] The preview is an edit-time dry run. It calls the same decision functions the build uses. The preview calls `FindPossibleSkinnedMeshMerges` and then `FindAllMergeAbleMaterials` per mesh group (`Editor/d4rkAvatarOptimizerEditor.cs:1030-1051`). The build's `CombineSkinnedMeshes` calls `FindPossibleSkinnedMeshMerges` (`Editor/d4rkAvatarOptimizer.cs:5601`), which itself calls `FindAllMergeAbleMaterials` (`:1766`). The preview differs from a real build only in input state. The input is the scene avatar before nondestructive tools run. Upstream flags that gap when it detects such a tool. A warning icon with a tooltip draws on the preview foldout (`Editor/d4rkAvatarOptimizerEditor.cs:1457-1463` and `913-921`). HelpBoxes for VRCFury and Modular Avatar state that previews "can be inaccurate" (`:877-895`).

[SOURCE] The preview computes lazily on first draw and caches in editor fields. Selection changes, setting edits, a foldout right click, or a refresh button clear the caches. Auto refresh disables itself when a pass exceeds a 500 ms budget (`Editor/AvatarOptimizerSettings.cs:28-32`, disable behavior at `Editor/d4rkAvatarOptimizerEditor.cs:186-190` and `952-953`).

[SOURCE] The upstream decision functions run at edit time on scene and component state. They stay affordable only through caching and that time budget. They are not side-effect free. `FindPossibleSkinnedMeshMerges` writes component state (`Editor/d4rkAvatarOptimizer.cs:1709`).

[INFERENCE] The upstream edit-time preview stays affordable through caching, a time budget, and accepted side effects. AMUSE decision functions do not run in that regime. Section 5 states what they require.

## 3. Current AMUSE inspector surface

[SOURCE] The package has one custom editor: `AmuseAvatarOptimizerEditor` (`Editor/AmuseAvatarOptimizerEditor.cs:18-19`). A property drawer `ToggleLeftDrawer` serves `[ToggleLeft]` bools (`Editor/ToggleLeftDrawer.cs:11-12`).

[SOURCE] `OnInspectorGUI` order (`Editor/AmuseAvatarOptimizerEditor.cs:26-68`): header with title and version, preset row, a root-placement HelpBox, the always visible `Alpha Separator` toggle, `if (DrawSettings()) DrawAlphaSeparatorSettings()`, then the last block reads `AmuseBuildStatusStore.TryGet` and draws one HelpBox.

[SOURCE] Foldout convention: session bool fields `_settingsOpen` and `_alphaSeparatorOpen` plus `EditorGUILayout.Foldout(state, title, EditorStyles.foldoutHeader)`, early return when closed (`Editor/AmuseAvatarOptimizerEditor.cs:318-324` and `662-667`). A second pattern persists reveal state on the component: `[ToggleLeft] _advancedSettingsRevealed` (`Runtime/AmuseAvatarOptimizer.cs:158-160`).

[SOURCE] Row convention: static `GUIContent` fields with Title Case labels and long plain-English tooltips. Tooltips use blank-line paragraphs, short active sentences, no contractions, and inline definitions such as "A texel is one pixel of a texture." (`Editor/AmuseAvatarOptimizerEditor.cs:122-282`). A section measures its widest label and sets `EditorGUIUtility.labelWidth` for alignment (`:324-346`).

[SOURCE] Read-only display precedent: the proof-mode row draws a computed value with `EditorGUILayout.LabelField` and adds conditional HelpBoxes. It writes nothing (`:358-412`).

[SOURCE] Everything user visible writes through `serializedObject` with `ApplyModifiedProperties()`.

[SOURCE] Inspector tests exist (`Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`). Three techniques exist. Pure static helpers get direct asserts (`:108-112`). `SerializedObject` helpers drive stored state (`:202-215`). A host `EditorWindow` draws real IMGUI. Its `OnGUI` calls the editor once, and reflection forces the foldout fields (`:230-279`).

## 4. What a build already computes

[SOURCE] The barrier pass classifies every renderer and retains results in NDMF build state, type `AmusePlatformFinishState` (`Editor/Build/AmusePlatformFinishPlugin.cs:15-204`). It holds analyzed, applied, and untouched renderer counts, moved triangle counts, refusal count buckets per reason, the avatar refusal, the prepared tree `Separation`, and the finalization writes `AppliedFinalization`.

[SOURCE] The prepared tree types live in `Editor/Build/AlphaSeparationRecords.cs`. `PreparedAlphaSeparation` (`:164-236`) holds `Renderers` in visit order and the avatar-wide `OpaqueBySource` map. `PreparedRendererSeparation` (`:241-322`) holds the renderer path, type name, mesh plan, and candidate slots. `PreparedSlotSeparation` (`:327-367`) holds the slot plan and per-slot flags.

[SOURCE] Refusal vocabularies exist at three scopes. `AlphaSeparationSlotRefusal` (`Editor/Build/AlphaSeparationRecords.cs:18-155`) names slot, renderer, avatar, and unlock-window reasons. `RendererAnalysisRefusal` (`Editor/Host/UnityRendererAlphaAnalysis.cs:17-106`) names reasons for refusing a renderer before planning. `HostLifecycleRefusal` names host gate reasons (`Editor/Build/HostLifecycleCapability.cs`). Every refusal means a named value. Unsupported never means a blanket catch.

[SOURCE] Reporting goes through NDMF `ErrorReport` with one `Information` entry per event and a context object, so the NDMF console links to the renderer or avatar root (`Editor/Build/AmuseReports.cs:8-118`). Strings follow the key shape `amuse.<area>.<Cause>` with `:description` and `:hint` entries (`Editor/Build/AmuseReportStrings.cs`). A completeness test fails the build when a new enum value lacks strings.

[SOURCE] A summary also lands in `AmuseBuildStatusStore` (`Editor/Build/AmuseReports.cs:759-785`). It is one static string, session scoped, deliberately not keyed by avatar. The component inspector draws it as the bottom HelpBox.

## 5. Lifetime and reachability of that data

[SOURCE] All per-renderer and per-slot results live inside the build's `BuildContext` state. Nothing serializes them. No SessionState, no disk, no static store except the single status string of `AmuseBuildStatusStore`. The build copy of the avatar dies with the build.

[SOURCE] No edit-time path computes classification today. Grep shows the analysis and preparation types have callers only in `Editor/Build` and in tests. The string "Show Material Transformations" exists in no production file. It did not exist in the repository at the base commit.

[SOURCE] All five AMUSE passes run in the NDMF `PlatformFinish` phase (`Editor/Build/AmusePlatformFinishPlugin.cs:225-260`). The NDMF phase order runs `Optimizing` before `PlatformFinish` (`Packages/nadena.dev.ndmf/Editor/API/Attributes/BuildPhase.cs:93-106`). NDMF also purges EditorOnly objects during `Resolving`. The scene avatar therefore differs from the build avatar even with no third-party tool installed.

[SOURCE] Building blocks for an edit-time computation exist but are not free. `UnityRendererAlphaAnalysis` reads `sharedMesh` and `sharedMaterials` read only (`Editor/Host/UnityRendererAlphaAnalysis.cs:210-223`). `AlphaSeparationPreparation.Prepare` is declared at `Editor/Build/AlphaSeparationPreparation.cs:93`. It creates AMUSE clone materials through the canonical clone builders of the two shader frontends (`:933-935`, `:1030-1032`, `:1207-1209`) and emits console reports through `:810`. Exact triangle proof needs GPU alpha readback evidence and the committed animation closure. Both exist in that form only during a build.

[INFERENCE] An edit-time exact preview is inexact by construction. AMUSE runs at PlatformFinish, after NDMF and other tools rewrite the avatar. A preview computed on the scene avatar sees pre-NDMF state, so it cannot match the build the way the build matches itself. Upstream d4rk has the same gap and warns about it when it detects such tools.

[INFERENCE] A last-run record is exact by construction. It reports what the build decided, on the state the build actually saw. Under unchanged inputs it answers "what will occur" as well as "what did occur".

## 6. NDMF facilities in the pinned version

[SOURCE] The manifest pins `nadena.dev.ndmf` in the range with floor 1.14.4 (`Packages/com.alrauna.amuse/package.json:18`). The restored package is exactly 1.14.4 (`Packages/nadena.dev.ndmf/package.json:3`). NDMF ships `ErrorReport`, `SimpleError` with title, details, and hint keys, `Localizer`, and the NDMF Console window that renders entries with clickable context links (`Packages/nadena.dev.ndmf/Editor/ErrorReporting/`).

[SOURCE] NDMF 1.14.4 ships an edit-time preview namespace `nadena.dev.ndmf.preview` with `IRenderFilter` and `ComputeContext` (`Packages/nadena.dev.ndmf/Editor/PreviewSystem/`). `PreviewBuilder` does not exist in this version. AMUSE uses no preview API today.

[SOURCE] Package identity: `com.alrauna.amuse` version `0.1.0-pre.5`, Unity `2022.3` (`Packages/com.alrauna.amuse/package.json:4-5`).

## 7. Conventions that bind the design

[SOURCE] Documents under `docs/superpowers/` are dated records. Status words such as "today" describe the record date (`docs/superpowers/README.md`).

[INFERENCE] A record never gets a retro edit. Changes add new dated records.

[SOURCE] Every English text a human reads uses short active sentences. One idea per sentence. No contractions. No semicolons in prose (`.omp/RULES.md`). This binds labels, tooltips, report sentences, and this document.

[SOURCE] Tests are EditMode NUnit. A filtered run that reports zero tests is a failure. Behavior changes observe RED first against a named wrong implementation, then GREEN. Falsifiers carry no-op guards (`docs/superpowers/plans/2026-10-13-opaque-coverage-preserve-slider-plan.md:23`, falsifier blocks at `Tests/Editor/Build/AlphaSeparationPreparationTests.cs:2066-2067` and `:2202-2213`).

[SOURCE] New report strings must extend the report string tables. The completeness test fails otherwise.

[SOURCE] Production types are `internal` where possible. File name equals type name. One public type per file. Namespaces mirror folders.

## 8. Where the seam sits

[SOURCE] The natural placement for a new top-level foldout is in `OnInspectorGUI` between the alpha separator block and the status HelpBox (`Editor/AmuseAvatarOptimizerEditor.cs:60-68`). A session bool plus `EditorStyles.foldoutHeader` matches the house pattern. The "Alpha Separation" foldout sits inside it.

[SOURCE] The data seam is the gap between build state and inspector. Today only the single status string crosses it. A structured per-renderer record needs a new session store beside `AmuseBuildStatusStore`, or a new edit-time computation.

[INFERENCE] Three honest strategies exist.

- Strategy A: persist a structured last-run record during the build and display it. Exact, small, matches the status store precedent. Shows nothing before the first run of the session.
- Strategy B: compute an edit-time exact dry run like d4rk. Requires capture and classification outside a build, on pre-NDMF state. Large new subsystem and inexact by construction per section 5.
- Strategy C: compute a cheap edit-time category view, gate facts and renderer and material categories, with no outcome promises. Fast and honest but duplicates admission logic at edit time, which can drift from build logic.

[RECOMMENDATION] Strategy A. It is the smallest complete solution that keeps AMUSE exact. It reuses data the build already computes. B violates the scope policy and AMUSE exactness. C drifts and can promise wrongly. A subsection header can state the last-run semantics, so the user is never misled.

[INFERENCE] Consumer compatibility holds under the session-bool foldout of open point 3. A display-only foldout then adds no serialized component field. Presets and Undo stay untouched. `OptimizerPreset` and `PresetApplier` need no change. The report string completeness test extends only if new report keys appear. The research package is unaffected.

## 9. Blast radius

[SOURCE] Strategy A touches, at most: the inspector (`Editor/AmuseAvatarOptimizerEditor.cs`), one new display model plus one new session store in `Editor/Build/`, the summary emitter that records the store (`Editor/Build/AmuseReports.cs` or the apply pass), and tests under `Tests/Editor/`. New files follow one type per file.

[INFERENCE] Strategy B adds a capture pipeline outside `BuildContext` and touches Host evidence code. Strategy C adds a second admission path. Both exceed the smallest complete solution.

## 10. Validation shape

[SOURCE] Display logic follows the existing test seams. Pure helpers assert directly. Stored state drives through `SerializedObject`. Drawing proves through the host window repaint test (`Tests/Editor/AmuseAvatarOptimizerEditorTests.cs:230-279`). Store behavior asserts through the internal static API, like the status store tests (`Tests/Editor/Build/AmuseReportStringsTests.cs:399-425`).

[SOURCE] Build-side record tests drive the plugin barrier with the `RunBarrier` harness. Tests never call `AlphaSeparationPreparation.Prepare` directly. The harness pattern lives in `Tests/Editor/Build/`.

[INFERENCE] RED/GREEN applies to behavior changes. A first display-only foldout with no behavior change yields characterization tests for store round trips and row construction, plus one repaint smoke test. Any transform-claim change needs a falsifier.

## 11. Open points for the design round

1. Strategy choice: A, B, or C from section 8. This is the owner decision the run halts on.
2. Store keying: single-slot last run wins, as `AmuseBuildStatusStore` does, or keyed per avatar. Single-slot is the precedent.
3. Foldout persistence: session bool like `_settingsOpen`, or a serialized `[ToggleLeft]` field on the component. Session bool is the foldout precedent and keeps presets untouched.
4. Row vocabulary: which per-renderer and per-slot outcomes display, and with which sentences. The candidate inventory is the section 4 data model. Simple English binds every sentence.
5. Placement inside or outside the Advanced Settings reveal. The Alpha Separator toggle sits outside. The new dropdown shows outcomes, not policy, so outside matches.
6. Whether the dropdown also shows gate-level facts that explain "nothing ran", for example feature disabled, host refused, or consent declined. Build state already carries these.

## 12. Verification record

Four parallel scouts gathered the facts for sections 2 through 7 on 2026-10-13. The strategy analysis in sections 8 through 11 came from a controller synthesis of those facts on the same day. Three adversarial accuracy reviews then ran on this document. They checked repository citations, external claims, and language conventions. The reviews found citation and language defects. This document incorporates every confirmed fix. No review challenged the strategy recommendation in section 8.
