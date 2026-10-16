# Additive Animation Layer Provenance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to execute this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore per-clip additive provenance in AMUSE animation evidence so renderers are refused for additive layers only when an additive layer's own clip animates a proof-relevant float property or carries a material-slot object binding on that renderer.

**Architecture:** The capture walk already iterates committed layers before flattening clips. A required `IsFromAdditiveLayer` fact threads from `CommittedLayer.BlendingMode` through `LiveClipObservation` into `CapturedClipEvidence`. The two refusal predicates (renderer scope in `RuntimeStateResolution`, slot scope in `AlphaSeparationPreparation`) then switch from the deleted graph-wide `HasAdditiveLayer` boolean to per-clip provenance, plus one new object pass over additive-source clips at renderer scope. The plan lands in two compilable states: Task 1 adds provenance without touching predicates, and Task 2 switches the predicates and deletes the boolean in one commit, so every test-run step runs against a compiling assembly.

**Tech Stack:** Unity 2022.3.22f1, C#, NDMF 1.14.4, NUnit via Unity Test Framework, EditMode only.

**Spec:** `docs/superpowers/specs/2026-10-16-additive-animation-layer-provenance-design.md`

**Investigation:** `docs/superpowers/investigations/2026-10-16-additive-animation-layer-proof-investigation.md`

## RED/GREEN labeling note

The spec labels the pose-layer admission test and the additive-swap test RED.
Because tasks land in commit order, those two integration tests run after the predicate change and pass on their first run.
Their RED evidence lives at schema level in Task 2 Step 2.2, which is observed failing against the old predicate.
This plan records that deviation explicitly.
Never rewrite history to manufacture a RED run.

## Privacy Note

This record characterizes test infrastructure in the dev editor instance.
This document sanitizes all avatar, renderer, material, texture, animation, and hierarchy names.
It describes assets by role.
It expresses exact counts as ranges.

## Working discipline

- Branch: `additive-animation-layer-proofing`. Base: `main` at `1fd711b` (branch already exists; investigation and spec are committed on it).
- Allowed mutations, exhaustive:
  - `Packages/com.alrauna.amuse/Editor/Host/LiveAnimationObservation.cs`
  - `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`
  - `Packages/com.alrauna.amuse/Editor/Host/CapturedAnimationEvidence.cs`
  - `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs` (XML doc comment only)
  - `Packages/com.alrauna.amuse/Editor/Analysis/RuntimeStateResolution.cs`
  - `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`
  - `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`
  - `Packages/com.alrauna.amuse/Tests/Editor/Host/LiveAnimationObservationVirtualClipTests.cs`
  - `Packages/com.alrauna.amuse/Tests/Editor/Analysis/RuntimeStateResolutionTests.cs`
  - `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`
  - `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`
  - `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`
  - `docs/superpowers/plans/2026-10-16-additive-animation-layer-provenance-plan.md`
- Never touch: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, generated `.csproj`/`.sln`, `Packages/nadena.dev.ndmf/`, `Packages/com.alrauna.amuse.research/`, any enum member list, any string table, any census category, and any file not listed above. The one authorized exception is the XML doc comment inside `RendererAnalysisRefusal` in `UnityRendererAlphaAnalysis.cs`, which Task 2 rewrites.
- Every finalized task ends with exactly one commit of the paths the task names. Do not stage unrelated files. The pre-existing untracked `AmuseTests_Temp *.meta` files and the unrelated investigation doc in the working tree are user-owned. Never stage them.
- Stop and return evidence if code differs from this plan. Stop if a test fails for a reason the task does not name. Stop if compilation fails at any step that expects a compiling assembly.
- Unity validation: request an asset refresh through `refresh_unity` and wait for readiness. Run EditMode tests through `run_tests` on the dev editor instance with a class or class-and-method filter. A filtered run that reports zero tests is a failure. Record observed pass and fail counts in the task report. Before running tests, verify the active Unity instance reports `Application.dataPath` equal to this repository's `Assets` folder. Never run tests in the Census Lab editor instance.

## Global constraints

- Simple English (ASD-STE100) in all comments and docs the change touches: short active sentences, one idea per sentence, no semicolons, no contractions, no em-dashes. The two `ponytail:` ceiling comments from the spec are exempt from the semicolon rule in their code-comment form.
- No private asset identifiers, machine paths, user names, ports, or hex hashes anywhere.
- Dates must be explicit (2026-10-16). Do not write unqualified "today" or "currently".
- RED/GREEN: observe new behavior tests fail first for the named reason where this plan says RED, then implement, then observe them pass. Record observed counts. A first-run pass on a test this plan labels characterization is recorded as characterization, never dressed up as RED.
- Falsifier tests use the comment form `--- Falsifier N: ... ---` and carry a no-op guard assertion this plan names.
- Refusal enum members, string tables, census categories, report writers, play-mode rows, and dry-run structure stay byte-identical.
- Both new constructor parameters (`CapturedClipEvidence`, `LiveClipObservation`) are required positional parameters. No optional parameters, no defaults.

---

## Task 1: Thread per-clip additive provenance through capture

The tree stays fully compilable throughout this task.
`HasAdditiveLayer` keeps its current consumers until Task 2.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/LiveAnimationObservation.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/CapturedAnimationEvidence.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/LiveAnimationObservationVirtualClipTests.cs`

**Interfaces:**
- Consumes: `CommittedLayer.BlendingMode` (`Editor/Host/CommittedControllerGraph.cs:25-52`); `LiveClipObservation` record declared at `Editor/Host/LiveAnimationObservation.cs:59` with factories `ObserveClip` and `ObserveVirtualClip` and the shared private core `ObserveBindings` (near `:140-173`); `CaptureObserved` record building (`Editor/Host/UnityAnimationEvidenceCapture.cs:781-785`).
- Produces: `LiveClipObservation.IsFromAdditiveLayer` and required trailing `bool isFromAdditiveLayer` parameters on the record constructor, on `ObserveClip`, and on `ObserveVirtualClip`; `CapturedClipEvidence(name, isSpecialMotion, isFromAdditiveLayer, floatBindings, objectBindings)` with required second parameter and `IsFromAdditiveLayer` property. `CapturedAnimationEvidence` is unchanged in this task.

- [ ] **Step 1.1: Convert one graph layer in the production-route capture test and assert provenance**

In `UnityAnimationEvidenceCaptureTests.cs`, `ProductionGraphRouteObservesEveryClipAndHostSpecialFact` (near `:981-1046`) builds two Override layers.
Change the second layer's blending mode to `AnimatorLayerBlendingMode.Additive`.
No clip set changes, so the existing count and index assertions stay valid.
Add assertions: the clip observed on the additive layer has `IsFromAdditiveLayer` true, and the clip observed on the Override layer has `IsFromAdditiveLayer` false.

- [ ] **Step 1.2: Write the index-route provenance test**

Add `IndexRouteObservationsCarryNoAdditiveProvenance` to `UnityAnimationEvidenceCaptureTests.cs`.
Build a committed graph result whose single layer is Override and whose clip animates a proof-relevant float property of the fixture renderer path.
Call `CaptureWithAnimationIndex` with a non-null `AnimationIndex` whose `GetClipsForObjectPath` returns an observation for that same clip, following the existing test doubles for `AnimationIndex` in this file.
Assert the resulting `evidence.Clips` contains an occurrence with `IsFromAdditiveLayer` false for the index-sourced clip, and that the graph-sourced occurrence of the same clip also carries `IsFromAdditiveLayer` false.
Include the marker comment:
`--- Falsifier 4: an implementation that defaults index-sourced occurrences to true re-widens the refusal for every renderer whose clips the index observes twice. This test must fail it. ---`
No-op guard: the assertions above name the occurrence flags directly, so any later change to the default surfaces here.

- [ ] **Step 1.3: Run the two tests to verify RED**

Run: `refresh_unity`, then `run_tests` filtered to `UnityAnimationEvidenceCaptureTests.ProductionGraphRouteObservesEveryClipAndHostSpecialFact` and `UnityAnimationEvidenceCaptureTests.IndexRouteObservationsCarryNoAdditiveProvenance`.
Expected: compile errors, because `IsFromAdditiveLayer` does not exist yet.
Record the observed compile failure as the RED state for both tests.

- [ ] **Step 1.4: Implement the provenance threading**

`LiveAnimationObservation.cs`:
- Add `bool isFromAdditiveLayer` as a required constructor parameter to `LiveClipObservation` (after `isSpecialMotion`), plus `internal bool IsFromAdditiveLayer { get; }`.
- Add the same required trailing parameter to `ObserveClip` and `ObserveVirtualClip`, and pass it through the shared `ObserveBindings` core into the record.
- XML doc on the new member: "True when this clip occurrence was observed on a layer whose blending mode is additive. Layer weight is not modeled."

`UnityAnimationEvidenceCapture.cs`:
- In `CaptureWithAnimationIndex`'s committed-layer walk (near `:322-330`), pass `layer.BlendingMode == AnimatorLayerBlendingMode.Additive`.
- In the same method's index loop (near `:332-339`), pass `false` for every `ObserveVirtualClip` observation. Add this exact ceiling comment on the index loop:
  `// ponytail: index occurrences marked false; the committed walk is the provenance authority; build real virtual provenance if a capture route stops deriving from the committed controllers`
- In `CaptureGraph`'s walk (near `:386-394`), pass `layer.BlendingMode == AnimatorLayerBlendingMode.Additive`.
- In `CaptureObserved`, thread `observation.IsFromAdditiveLayer` into the `CapturedClipEvidence` construction (near `:781-785`).
- `HasAdditiveLayer` and the OR-ing loop near `:448-457` stay untouched in this task.

`CapturedAnimationEvidence.cs`:
- `CapturedClipEvidence` (at `:83-103`): insert `bool isFromAdditiveLayer` as the second constructor parameter, store it, add `internal bool IsFromAdditiveLayer { get; }`, and state the conservative meaning in the XML doc.

- [ ] **Step 1.5: Fix the remaining host test callers**

In `UnityAnimationEvidenceCaptureTests.cs`: every direct `ObserveClip(...)` call passes the required third argument `false` (`:107,220,369-370,2027` and the `ObservationWithMaterialSwap` helper). Every hand-built `LiveClipObservation` passes the required argument (`:302,1970,2000,2234,2274,2300` plus local helpers).
In `LiveAnimationObservationVirtualClipTests.cs`: `ObserveClip` and `ObserveVirtualClip` callers (`:59,100,116`) pass `false`. `ObserveVirtualClipMatchesObserveClipSemantics` (`:53-62`) passes the same fact to both twins.
The `HasAdditiveLayer` assertions in this file (`:975,:1325`) stay valid and untouched in this task.

- [ ] **Step 1.6: Run the host capture tests**

Run: `refresh_unity`, then `run_tests` filtered to `UnityAnimationEvidenceCaptureTests` and `LiveAnimationObservationVirtualClipTests`.
Expected: all pass, including the two Step 1.1/1.2 tests now GREEN.
Record observed counts.

- [ ] **Step 1.7: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/LiveAnimationObservation.cs Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs Packages/com.alrauna.amuse/Editor/Host/CapturedAnimationEvidence.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs Packages/com.alrauna.amuse/Tests/Editor/Host/LiveAnimationObservationVirtualClipTests.cs
git commit -m "feat(host): capture per-clip additive layer provenance"
```

---

## Task 2: Switch both refusal predicates and delete the graph-wide boolean

The tree stays fully compilable throughout this task.
The schema tests in Step 2.1 compile against Task 1's records and fail against the old predicates.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/RuntimeStateResolution.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs` (XML doc only)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/RuntimeStateResolutionTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` (mechanical compile fixes only)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs` (mechanical compile fixes only)

**Interfaces:**
- Consumes: `CapturedClipEvidence.IsFromAdditiveLayer` (Task 1); the unchanged delegate shapes `structuralRefusalFor`, `resolveProofRelevant`, `unrecognizedObjectMaterialBinding`, `parseMaterialSlotBinding`.
- Produces: `ResolveRuntimeStates` refuses `RendererAnalysisRefusal.AdditiveLayerWithProofRelevantMaterialProperty` when a RendererWide float binding originates from an additive-source clip, or when an object binding from an additive-source clip parses against this renderer via `parseMaterialSlotBinding`. `AlphaSeparationPreparation` refuses `AlphaSeparationSlotRefusal.ConversionStateUnderAdditiveLayer` only when a conversion-relevant float binding originates from an additive-source clip. `CapturedAnimationEvidence` no longer has `HasAdditiveLayer`. The structural, unrecognized-float, unrecognized-object, and blend-tree checks keep their exact form and order as of 2026-10-16.

- [ ] **Step 2.1: Write the failing schema-only tests (RED)**

In `RuntimeStateResolutionTests.cs`, extend the hand-built evidence pattern (`EmptyRequest()`, `UnsupportedMaterial()`, records built near `:23-65`).
The two existing `CapturedAnimationEvidence` constructions drop nothing yet; the new tests hand-build `CapturedClipEvidence` with the Task 1 provenance argument.
The fixtures set the graph-wide `hasAdditiveLayer` constructor argument explicitly: `false` where the test pins provenance-driven behavior, `true` where it pins the graph-wide behavior as of 2026-10-16.

1. `AdditiveSourceFloatBindingRefusesAndOverrideSourceDoesNot`: two separately resolved evidences with identical RendererWide float bindings, clip provenance `true` versus `false`, graph-wide additive `false` in both. The `true` one refuses with `AdditiveLayerWithProofRelevantMaterialProperty`. The `false` one resolves.
2. `AdditiveSourceObjectBindingRefuses`: clip provenance `true`, graph-wide additive `false`, object binding parsing as a material-slot binding for the renderer path. Refuses with `AdditiveLayerWithProofRelevantMaterialProperty`. Include the marker:
   `--- Falsifier 2: an implementation that narrows the renderer predicate but forgets the object-binding pass leaves the additive material swap admitted. This test must fail it. ---`
   No-op guard: assert the object binding parsed (slot index equals the fixture slot) before the refusal assertion.
3. `OverrideSourceObjectBindingStillAdmitsSwap`: the `false` twin of test 2 resolves and the swap appears in the slot admission. This is the control.

- [ ] **Step 2.2: Run to verify RED**

Run: `refresh_unity`, then `run_tests` filtered to the three new tests.
Expected: compile passes. Test 1 fails on its override-source half. On the old predicate, both fixtures resolve, because the old predicate never reads provenance. Record the exact observed failures. Test 2 fails because no refusal fires. Test 3 passes on the old code, because it is the control.
Record observed counts and each failure reason.

- [ ] **Step 2.3: Implement the narrowed predicates and the deletion**

`RuntimeStateResolution.cs`:
- Add `using System.Linq;` if absent.
- The flatten loop (near `:155-160`) collects pairs: `floats` becomes `List<(CapturedFloatBinding Binding, bool FromAdditiveLayer)>`, `objects` becomes `List<(CapturedObjectBinding Binding, bool FromAdditiveLayer)>`.
- The structural call at `:162-163` keeps its signature: project `floats.Select(p => p.Binding).ToList()` and `objects.Select(p => p.Binding).ToList()` at the call site.
- The float classification loop destructures: `foreach (var (binding, fromAdditiveLayer) in floats)`. When a binding resolves `RendererWide`, set `anyRelevantFromAdditive |= fromAdditiveLayer` alongside the existing `relevantBindings.Add`.
- The unrecognized-object loop destructures the same way and passes `binding` to `unrecognizedObjectMaterialBinding`. Its place and form are otherwise unchanged.
- After it, add the additive object pass: for each object pair with `FromAdditiveLayer`, call `parseMaterialSlotBinding(binding.Path, binding.TypeName, binding.PropertyName, rendererPath, rendererTypeName, out _)`; any `true` sets the additive refusal condition.
- Replace the guard at `:207-212` with `if (anyRelevantFromAdditive || anyAdditiveSourceObjectBinding)`. Add this ceiling comment above it:
  `// ponytail: weight-blind; a weight-0 additive layer refuses its own clips; model weights only if Census shows coverage loss`
- The blend-tree check at `:214-220` keeps `relevantBindings.Count > 0 && evidence.HasUnnormalizedDirectBlendTree` unchanged.

`AlphaSeparationPreparation.cs`:
- In the conversion loop (near `:253-285`), track `anyConversionBindingFromAdditiveLayer |= clipEvidence.IsFromAdditiveLayer` when a binding resolves `RendererWide`.
- Replace the twin guard at `:293-301` with that flag. The blend-tree twin at `:303-312` stays unchanged.

`CapturedAnimationEvidence.cs`:
- Delete the `hasAdditiveLayer` constructor parameter, the `HasAdditiveLayer` property, and their assignment. `hasUnnormalizedDirectBlendTree` keeps its positional slot.

`UnityAnimationEvidenceCapture.cs`:
- Delete the additive term from the OR-ing loop (near `:448-457`) and the deleted argument from the record construction (near `:800`).

`UnityRendererAlphaAnalysis.cs` (XML doc only, near `:32-36`):
- Rewrite the refusal's doc comment: the renderer carries a proof-relevant float binding or a material-slot object binding whose clip was observed on an additive layer. Keep the member name.

Mechanical compile fixes:
- `RuntimeStateResolutionTests.cs`: the two existing `CapturedAnimationEvidence` constructions (near `:23,:61`) drop the deleted positional bool.
- `AlphaSeparationApplyTests.cs`: the four direct-record constructions (`:3754,3839,6152,6325`) drop the deleted positional bool. All four use `Array.Empty<CapturedClipEvidence>()` for clips, so no provenance argument is needed there.
- `AmusePlatformFinishPluginTests.cs`: the fixture-precondition reads `evidence.HasAdditiveLayer` at `:4096` and `:4774` become per-clip provenance assertions on `evidence.Clips`, preserving each test's fixture precondition meaning. No other test in this file changes in this task.

- [ ] **Step 2.4: Run every touched test class to verify GREEN**

Run: `refresh_unity`, then `run_tests` filtered to `RuntimeStateResolutionTests`, then to `AlphaSeparationPreparationTests`, then to `UnityAnimationEvidenceCaptureTests`, then to `AlphaSeparationApplyTests`, then to `AmusePlatformFinishPluginTests`.
Expected: all pass, including the pre-existing `RuntimeStateIntegration_AdditiveLayerWithRelevantPropertyRefuses` (`:4073`), `ConversionStateUnderAdditiveLayer_RefusesEveryCandidateSlot` (`:577`), and the Step 1.1/1.2 capture tests.
Record observed counts per class.

- [ ] **Step 2.5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/RuntimeStateResolution.cs Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs Packages/com.alrauna.amuse/Editor/Host/CapturedAnimationEvidence.cs Packages/com.alrauna.amuse/Tests/Editor/Analysis/RuntimeStateResolutionTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs
git commit -m "feat: refuse additive-sourced bindings and drop graph-wide additive flag"
```

Note: `AlphaSeparationPreparationTests.cs` is listed even though Task 2 expects no edit there, in case a fixture uses the deleted property; include it in the commit only if it changed.

---

## Task 3: Integration tests for the renderer-scope behavior

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Consumes: the full pipeline fixtures, `AnalyzeVerifiedRuntimeStates` (near `:7455`) whose result exposes `.Refusal` and `.OpaqueCandidateTriangleCount`, `SetFirstLayerBlendingMode` (`:5861`, writes `layers[0]` only), `AddAnimatedMaterialAssignment` (`:5934-5958`), the capture helper `CaptureVerifiedRuntimeStateEvidence` with its pinned single-layer preconditions (near `:7522-7526`).
- Produces: integration-level evidence for the narrowed predicates and the migrated swap fixture test.

- [ ] **Step 3.1: Relax the capture helper's layer-count preconditions**

`CaptureVerifiedRuntimeStateEvidence` hard-asserts one graph layer with one clip (near `:7522-7526`).
Parameterize the expected layer and clip counts with default arguments equal to the current values, so every existing caller compiles unchanged and the new multi-layer tests pass explicit counts.
No assertion semantics change for existing callers.

- [ ] **Step 3.2: Rewrite the additive-swap fixture test**

Rewrite `RuntimeStateIntegration_AdditiveFlagAloneDoesNotRefuseRenderer` (`:4752`) into `RuntimeStateIntegration_AdditiveLayerMaterialSwapRefuses`.
Keep the exact fixture: the only layer carries an `m_Materials.Array.data[0]` object curve (`AddAnimatedMaterialAssignment`) and is set additive (`SetFirstLayerBlendingMode`, called near `:4768`).
Assert `result.Refusal == RendererAnalysisRefusal.AdditiveLayerWithProofRelevantMaterialProperty` on the `AnalyzeVerifiedRuntimeStates` result.
Keep fixture-precondition assertions on per-clip provenance (`IsFromAdditiveLayer` true) in place of the old `evidence.HasAdditiveLayer` reads.
Include the marker:
`--- Falsifier 2: an implementation that narrows the renderer predicate but forgets the object-binding pass leaves the additive material swap admitted. This test must fail it. ---`
No-op guard: assert the captured clip evidence contains one object binding before the refusal assertion.
This test runs after Task 2 and passes on first run. Record it as characterization.

- [ ] **Step 3.3: Add the pose-layer admission test**

Add `RuntimeStateIntegration_AdditivePoseLayerDoesNotRefuseOverrideAnimatedRenderer`.
Build the fixture inline (two roots in one test is an existing pattern, for example near `:2651`):
- Root A: first layer Override animating a proof-relevant float property of the fixture renderer (reuse the float-curve helper the `:4073` fixture uses), plus a second layer made additive via `controller.AddLayer` and `controller.layers[1].blendingMode = AnimatorLayerBlendingMode.Additive`, whose clip animates only a transform.
- Root B: identical fixture without the additive layer.
Run both through the same barrier path. Assert neither root refuses with `AdditiveLayerWithProofRelevantMaterialProperty`, and that root A's analysis outcome equals root B's: equal `OpaqueCandidateTriangleCount` and equal per-slot resolved-or-refused outcomes.
This test runs after Task 2 and passes on first run. Record it as characterization.

- [ ] **Step 3.4: Add the shared-clip conservatism test**

Add `RuntimeStateIntegration_SharedClipOnAdditiveAndOverrideLayersStillRefuses`.
One clip animating a proof-relevant float property is referenced from the Override first layer and again from a second additive layer, with the Override layer enumerated first.
Assert the additive refusal fires on the `AnalyzeVerifiedRuntimeStates` result.
Include the marker:
`--- Falsifier 1: an implementation that marks provenance from the first observation only, or that drops additive occurrences when the same clip also appears on an Override layer, lets the additive contribution through. This test must fail it. ---`
No-op guard: assert the captured evidence contains two clip occurrences for the same clip with exactly one carrying `IsFromAdditiveLayer` true.

- [ ] **Step 3.5: Add the additive float regression guard**

Add `RuntimeStateIntegration_AdditiveLayerFloatAnimationStillRefuses`, mirroring the `:4073` fixture: the additive layer animates the proof-relevant float. Assert the refusal fires. Comment that this passes on the implementation as of 2026-10-16 as well. This is a characterization guard.

- [ ] **Step 3.6: Run the build-path test class**

Run: `refresh_unity`, then `run_tests` filtered to `AmusePlatformFinishPluginTests`.
Expected: all pass. Record observed counts.

- [ ] **Step 3.7: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "test(build): pin additive provenance admission and swap refusal"
```

---

## Task 4: Full assembly validation and parity checks

**Files:**
- Modify: none expected. Fixes only, within the already-listed files.

**Interfaces:**
- Consumes: everything above.
- Produces: full-assembly green evidence, dry-run parity observation, research parity evidence, and the final sweep.

- [ ] **Step 4.1: Run the full product test assembly**

Run: `refresh_unity`, then `run_tests` on the full `Alrauna.Amuse.Tests.Editor` assembly.
Expected: all pass. A run reporting zero tests is a failure. Record observed pass and fail counts. A failure this plan does not name is a stop condition.

- [ ] **Step 4.2: Verify the dry run shares the implementation**

Read `Editor/DryRun/AlphaSeparationDryRun.cs`: the capture call sites (near `:863-900`) and the resolve call site (near `:602`).
Confirm no separate additive logic exists there and no compile errors reference it.
Record the observation. No edit is expected.

- [ ] **Step 4.3: Verify the research package parity**

Run: `run_tests` filtered to the research assembly `Alrauna.Amuse.Research.Tests.Editor`.
Expected: all pass. This proves the unchanged enum needs no research-side change. Record observed counts.

- [ ] **Step 4.4: Whitespace and identifier sweep**

Run `git diff --check` on the branch range.
Sweep every changed file for private identifiers, machine paths, four-digit ports, at-signs joined to hexadecimal hashes, and unqualified "today".
Fix hits within the changed files. State in the report that the sweep ran and what it found.

- [ ] **Step 4.5: Commit (only if Step 4.4 or fixes changed files)**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/LiveAnimationObservation.cs Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs Packages/com.alrauna.amuse/Editor/Host/CapturedAnimationEvidence.cs Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs Packages/com.alrauna.amuse/Editor/Analysis/RuntimeStateResolution.cs Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs Packages/com.alrauna.amuse/Tests/Editor/Host/LiveAnimationObservationVirtualClipTests.cs Packages/com.alrauna.amuse/Tests/Editor/Analysis/RuntimeStateResolutionTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs docs/superpowers/plans/2026-10-16-additive-animation-layer-provenance-plan.md
git commit -m "chore: validation fixes for additive provenance"
```
