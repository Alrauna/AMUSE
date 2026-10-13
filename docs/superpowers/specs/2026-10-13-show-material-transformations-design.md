# Show Material Transformations Dropdown — Design

Date: 2026-10-13

Base: `3ff7ed6` on branch `feat/show-material-transformations`

Investigation: `docs/superpowers/investigations/2026-10-13-show-material-transformations-investigation.md`

Revision note: none. This is the first issue. An adversarial review round runs before any implementation work starts. Its confirmed fixes will land as a revision note in this file before the implementation plan is written.

## Overview

The AmuseAvatarOptimizer inspector gains a new top-level foldout, "Show Material Transformations". Inside it sits a nested foldout, "Alpha Separation". When the user opens the nested foldout, AMUSE runs its real proof pipeline on the avatar in the scene and draws what would happen, renderer by renderer and slot by slot.

The feature has five parts.

1. A display model of transformation rows. Pure records. No Unity mutation.
2. An edit-time dry run orchestrator. It reuses the Host capture, the Semantics frontends, and the Analysis classification. It stops before preparation. It creates nothing and writes nothing.
3. An extraction of the runtime-state resolution pipeline out of the build pass into the Analysis layer, so the build and the dry run share one classifier.
4. A session cache keyed by content fingerprints, so repeated foldout opens stay cheap and stale results invalidate.
5. Inspector drawing for the foldout, the rows, the disclosure sentences, and a Refresh button.

The owner chose the edit-time exact dry run on 2026-10-13. The owner set one condition: no deferrals. Every mechanism the design reasonably needs is in scope, including GPU readback at edit time and edit-time animation closure capture.

## Decisions

These decisions close the investigation open points and the new questions the owner choice raised.

1. **Strategy B, the edit-time exact dry run.** The owner chose it on 2026-10-13. The dry run applies the same proof rules to the scene avatar that a build applies to the build avatar. It is exact over its input state. It cannot see later rewrites, because AMUSE runs at PlatformFinish, after NDMF and other tools change the avatar (investigation section 5). The design handles this boundary with a standing disclosure line and conditional disclosure rows. It never claims the next build is guaranteed.
2. **Foldout placement and persistence.** The top-level foldout sits between the alpha separator block and the status HelpBox in `OnInspectorGUI`. Both foldout states are session bools in the editor, matching `_settingsOpen` and `_alphaSeparatorOpen`. The foldouts sit outside the Advanced Settings reveal, like the Alpha Separator toggle. The dropdown shows outcomes, not policy. No serialized component field is added, so presets, Undo, and Reset stay untouched (investigation open points 2, 3, 5).
3. **A new module for orchestration.** New folder `Editor/DryRun/` with namespace `Alrauna.Amuse.Editor.DryRun`. It calls Host, Analysis, and Semantics. It never calls Build. Build owns the NDMF lifecycle and mutation. The dry run owns none of that. One internal type per file, file name equals type name.
4. **Edit-time animation closure.** The structural graph comes from `CommittedControllerGraph.Enumerate(avatarRoot, VRChatPlatformAnimatorBindings.Instance)`, the same extension-free traversal the build's structural check runs before virtualization. The animation evidence comes from `UnityAnimationEvidenceCapture.Capture`, the production-shaped entry that needs no NDMF context and takes real policy bounds. The fallback bindings singleton supplies `IsSpecialMotion`. Known boundary: a build unions NDMF `AnimationIndex` virtual clips, which carry upstream tool rewrites. The dry run sees authored clips only. At edit time nothing has rewritten the avatar yet, so authored clips are the true input state. The disclosure covers tools that rewrite later.
5. **Edit-time texture evidence.** The GPU blit and readback route `UnityAlphaFieldEvidence.TryCapture` runs unchanged at edit time on authored textures. It needs no CPU readability. The material-level batch `UnityMaterialEvidenceCapture.Capture(inputs, bounds)` supplies the orchestration. Three edit-time refusals surface as named display rows, never as defects. First, a non-Windows active build target refuses texture capture wholesale, because the Windows import is not loaded. The dropdown shows a disclosure row. Second, textures with the streaming-mipmaps import flag route to the clone capture at build time. That route creates, reimports, and deletes a temporary asset. An inspector draw must never mutate project assets, so the dry run refuses those textures with a named row. A Windows build can prove more for them. Third, the mip residency check folds the editor quality mip limit, so the editor view can refuse a level a build would capture. The disclosure covers this class.
6. **No mutation, no report emission.** The dry run creates no materials, no meshes, no assets, and no clones. It calls no preparation and no apply. It emits no NDMF `ErrorReport` entries and adds no report strings, so the report completeness test gains no surface. It writes no component field. Display rows reuse the existing refusal sentences from `AmuseReportStrings` through its pure key builders, so the avatar shows one vocabulary for the same facts in the console and in the dropdown. This is a deliberate reference from DryRun into the Build namespace string table. The table is pure data. The alternative, a second sentence set, invites drift.
7. **One classifier, extracted.** The private static pipeline of the build pass moves to the Analysis layer: `ResolveRuntimeStates`, `ClassifyRuntimeStates`, `MaterialSlotsFor`, and `ClosureWayForSlot`, currently in `AmusePlatformFinishPass`. The new home is `Editor/Analysis/RuntimeStateResolution.cs`, an internal static class. The pass delegates to it. The dry run calls it. Analysis already references Host evidence records, so the dependency direction is established. The architecture document already assigns runtime-state resolution to Analysis. No logic changes in the move. The existing `RunBarrier` tests pin the behavior. A pure move is characterization, not RED.
8. **Session cache with fingerprints.** A static session cache in `Editor/DryRun/DryRunCache.cs`, single avatar slot, following the `AmuseBuildStatusStore` precedent. The avatar fingerprint covers: the avatar root instance ID, the component policy integers and switches, the set of animator controller assets with their asset file ticks, and the structural graph outcome. The renderer fingerprint covers: renderer path, renderer type, mesh instance ID, per-slot material instance IDs, and every admitted texture keyed by asset GUID plus source and meta file ticks, or instance ID plus update count and image contents hash for non-asset textures. A fingerprint mismatch recomputes that renderer. A manual Refresh button clears the cache. The cache dies on domain reload and recomputes on the next open. The forbidden capability latches in `UnityAlphaFieldEvidence` stay untouched.
9. **Row vocabulary.** The dropdown shows four row kinds in fixed order. Banner rows state avatar-level facts: AMUSE off, Alpha Separator off, root misplaced, structural graph refusal, non-Windows build target. The disclosure row states the computed-on boundary in two sentences. Renderer rows state the renderer path, the type, and the outcome summary with counts. Slot rows sit indented under their renderer and state the per-slot outcome: would split with the proven triangle count, would fold onto the opaque copy under the preserve policy, already opaque, unchanged because unproven, or refused with the named sentence from the report string table. Texture capture refusals fold into their slot row. All sentences follow the simple English rules.
10. **Compute trigger and budget.** The dry run computes lazily on foldout open, one renderer per draw pass until complete, from the cache when fingerprints match. The first open pays the texture readbacks. A disclosure sentence warns that the first open can take a moment. No async machinery, no background jobs, no frame budget tuning. If this proves too slow in practice, a later record can revisit it.
11. **Host gates stay build concerns.** Version, consent, and build-path gates decide whether a build runs. The dropdown shows analysis results regardless of them. The component switches that change the analysis input, master disable and the Alpha Separator toggle, produce banner rows instead of results.
12. **Simple English everywhere.** Every label, tooltip, row, and disclosure sentence uses short active sentences. No contractions. One idea per sentence. The exact text lives in the Inspector section.

## Component state

No serialized field is added or changed. The runtime component is untouched. The editor adds two private session bools:

- `private bool _materialTransformationsOpen;`
- `private bool _alphaSeparationTransformationsOpen;`

The dry run reads policy from the component instance at compute time: `_amuseDisabled`, `_alphaSeparatorEnabled`, `_preserveTransparencyMaxMipLevel`, `_preserveTransparencyMinTextureSize`, `_minimumOpaqueCoveragePercent`, `_minimumOpaqueCoverageToPreservePercent`, `_minimumOpaqueAlphaPercent`, `_polygonMinimumOpaqueCoveragePercent`, `_polygonAlphaUpperClampPercent`, `_acceptDesktopAnisotropyDifferences`, `_acceptMobileAnisotropyDifferences`, `_ignoreOutOfRangeMaterialSlots`, `_allowDepthTestChange`. All are already serialized on the component. The fingerprint includes every one of them.

## Inspector

The foldout titles are exact:

> Show Material Transformations

> Alpha Separation

The disclosure row under the nested foldout title, always present when any result shows:

> This view runs the same proof on the avatar in the scene. Other tools change the avatar later at build time. This view cannot see those changes. A run in Play mode or an upload shows the true result.

The first-open note, shown until the first successful full compute in this session:

> The first open measures textures on the GPU. It can take a moment.

The non-Windows banner row, when the active build target is not StandaloneWindows64:

> The active build target is not Windows. Texture proof needs the Windows import. This view can prove fewer triangles than a Windows build.

The streaming-texture row, when at least one admitted texture carries the streaming-mipmaps import flag:

> Some textures use streaming mipmaps. This view does not read them, because reading would change project files. A build can prove more for those textures.

The banner rows for component facts:

> AMUSE is off. Turn it on to see results.

> The Alpha Separator is off. Nothing will move. Turn it on to see results.

> This component must sit on the avatar root object.

The structural refusal row shows the named avatar refusal sentence from the report string table, prefixed by the renderer name when one renderer caused it, per the existing renderer-name convention.

Renderer row shape, one line each, in hierarchy order:

> <renderer path> (<type>): would move <N> triangles. <K> of <M> triangles proven opaque.

> <renderer path> (<type>): no change. Nothing proven.

> <renderer path> (<type>): refused. <named refusal sentence>

Slot rows sit indented one level under their renderer:

> slot <i>: would split. <N> of <M> triangles proven opaque.

> slot <i>: would fold onto the opaque copy. The unproven share is below the preserve setting.

> slot <i>: already opaque. It stays as it is.

> slot <i>: unchanged. <N> of <M> triangles proven opaque. The share is below the convert setting.

> slot <i>: refused. <named slot refusal sentence>

The Refresh button draws at the top of the nested foldout, right aligned, labeled:

> Refresh

Empty states: the nested foldout shows the banner rows when no renderer result exists. When the avatar has no renderer at all, it shows:

> No renderer on this avatar.

## Dry run pipeline

The orchestrator type `AlphaSeparationDryRun` runs this sequence. Everything is read only.

1. **Avatar facts.** Read the component switches. If AMUSE is off or the Alpha Separator is off, produce banner rows and stop. If the component sits on a child, produce the root banner and stop.
2. **Structural graph.** Enumerate the authored controllers with `CommittedControllerGraph.Enumerate` and the fallback bindings. A refusal produces the structural row and stops. The graph result feeds every renderer.
3. **Per renderer, in hierarchy order.** For each `Renderer` under the avatar root: run the host structural check, capture animation evidence with `UnityAnimationEvidenceCapture.Capture` using the real policy bounds, run `UnityRendererAlphaAnalysis` for the plan or the analysis refusal, resolve runtime states and classify triangles through the extracted `RuntimeStateResolution`, and build rows. A renderer refusal produces the refused row with the named sentence. Slot texture refusals attach to their slot rows.
4. **Cache write.** Store the renderer outcomes under the fingerprints. A partial pass stores what completed, so an interrupted first open resumes where it stopped.
5. **Display model.** The rows are pure records: row kind, renderer path, renderer type, slot index, count fields, and one display sentence. The inspector draws them. The model knows nothing about IMGUI.

The dry run never calls `AlphaSeparationPreparation.Prepare`, `AlphaSeparationApply`, or any clone builder. It never touches `AmuseReports` emission.

## Extraction and module boundaries

`Editor/Analysis/RuntimeStateResolution.cs` receives the four private statics of the build pass as internal statics with identical signatures and bodies. The pass calls them. The dry run calls them. Tests may call them directly through `InternalsVisibleTo`.

Dependency directions after the change:

- `Build` calls `Analysis`, `Host`, `Semantics`, `DryRun` never calls `Build`, and `DryRun` calls `Analysis`, `Host`, `Semantics`. The one exception is decision 6: the display sentences come from the `AmuseReportStrings` table in Build. That reference is pure data flow.

## Cache

`Editor/DryRun/DryRunCache.cs` is an internal static class. One avatar slot. Session scope. API shape:

- `bool TryGetRenderer(string fingerprint, out IReadOnlyList<MaterialTransformationRow> rows)`
- `void StoreRenderer(string fingerprint, IReadOnlyList<MaterialTransformationRow> rows)`
- `void Clear()`

Fingerprint construction is a pure internal static on the same class or a neighbor, so tests can assert invalidation directly. The Refresh button calls `Clear()` and forces a repaint.

## Invariants

1. The dry run never mutates any Unity object, asset, import setting, or project file. The one capture route that would, the streaming clone route, is refused at edit time.
2. The dry run emits no NDMF report entry and adds no report string key.
3. Unknown stays Unknown. An unproven triangle never displays as "would move". A dry run that claims more than the proof supports is a defect.
4. Every refusal surfaces as a named value with an existing sentence. No blanket catch converts a programming defect into a display row.
5. A stale cache can never serve a row whose fingerprint no longer matches. Policy changes, texture reimports, controller changes, and mesh changes all invalidate.
6. The build path keeps its exact behavior. The extraction is a pure move. Any barrier test outcome change is a stop condition.
7. The research package stays untouched. No research type appears in product code.
8. All display sentences use short active sentences. No contractions. One idea per sentence. No semicolons in prose.
9. The inspector never blocks the UI thread for a whole avatar in one pass. Computation is one renderer per draw pass until complete.
10. No absolute path, host name, port, or private identifier appears in any new file.

## Test strategy

New test folder `Tests/Editor/DryRun/`, mirroring production. Existing assemblies and harnesses apply: EditMode NUnit, `InternalsVisibleTo`, stand-in shaders under `Hidden/Alrauna/AmuseTests/*`, no vendor shaders, the `RunBarrier` harness for build-side behavior.

Per class:

- `RuntimeStateResolution` move: the existing `RunBarrier` suite must pass unchanged. Record as characterization. No new test asserts the move itself.
- `MaterialTransformationRowTests`: pure row construction from analysis outputs. Covers would-split, fold, already opaque, unchanged, refused, and the count fields. Behavior change territory: a row that claims movement for unproven triangles. Falsifier 1: a row builder that reads total triangles instead of proven ordinals produces a would-split row for a fully unproven slot. The unproven-slot fixture kills it.
- `AlphaSeparationDryRunTests`: end-to-end compute on a scene fixture with a stand-in cutout shader and runtime-created textures, in the style of the existing evidence tests. Covers: proven triangle displays as would-split; unknown stays unchanged; renderer analysis refusal shows the named sentence; the structural animation-event refusal stops with the structural row; AMUSE off and separator off produce banners and no compute; texture capture refusals attach to slot rows.
- `DryRunCacheTests`: fingerprint invalidation. Falsifier 2: a cache keyed only by renderer path serves stale rows after a policy slider changes. The fingerprint must include the policy integers, and the test changes one and asserts recompute. Falsifier 3: a cache keyed without texture ticks serves stale rows after a texture asset tick changes. The test fakes the tick input and asserts recompute. Both falsifiers carry no-op guards.
- No-mutation guard test: count project assets and scene materials before and after a full dry run. Assert equality. Guard against any future route into the streaming clone path or a clone builder.
- `AmuseAvatarOptimizerEditorTests` additions: the host window repaint test opens both foldouts and asserts a painted pass with rows present for a fixture avatar, and a painted pass with banners when the separator is off.

RED/GREEN applies to every behavior change above. A first-run pass is recorded as characterization. A filtered run that reports zero tests is a failure. Every run records observed counts.

## Documentation

The README gains a section after the Alpha Separator settings section. Exact text:

> ### Show Material Transformations
>
> Open the Show Material Transformations foldout on the AMUSE component to see what the Alpha Separator would do on this avatar. The nested Alpha Separation view runs the same proof on the avatar in the scene. It shows, per renderer and per material slot, what would move, what would fold, what stays, and what is refused with a reason.
>
> The view reads the scene state. Other tools can change the avatar later during a build. This view cannot see those changes. A run in Play mode or an upload shows the true result. The first open measures textures on the GPU. It can take a moment. The Refresh button recomputes everything.

## Acceptance criteria

1. The inspector shows the two foldouts at the stated position, outside Advanced Settings, and no serialized field changes on the component.
2. Opening the nested foldout on an avatar with a stand-in cutout material shows the renderer and slot rows that match a manual classification of the fixture, with correct counts.
3. An unproven slot never shows a would-move claim.
4. Each named refusal shows its existing report sentence.
5. The banners appear for AMUSE off, separator off, child placement, structural refusal, and non-Windows target, and no renderer compute runs behind the first two banners.
6. A policy slider change invalidates the cached rows without the Refresh button.
7. The Refresh button clears the cache and repaints.
8. A full dry run creates no asset, no material, no mesh, and no report entry.
9. The existing full product test assemblies pass with unchanged outcomes after the extraction. Observed counts are recorded.
10. Every new display sentence passes the simple English sweep.

## Validation and stop conditions

Order of validation: Unity refresh after new files, focused new-test filters, the full `Alrauna.Amuse.Tests.Editor` assembly, then the full `Alrauna.Amuse.Research.Tests.Editor` assembly. Record observed counts for every run. A filtered run with zero tests is a failure.

Stop and return evidence when:

- the extraction changes any existing test outcome,
- edit-time capture proves impossible on the stand-in fixtures for a reason this spec does not name,
- the dry run cannot complete without mutating project state,
- a display row would need a sentence that no existing report string covers and the report table would have to grow.

The last stop condition triggers the report-string completeness path from the spec, not a silent new string.

## Authorization boundary

Dated owner authorizations, all from 2026-10-13: the feature branch and commits between finalized changes; the strategy B choice with no deferrals; adversarial review rounds between phases; subagent-driven execution of the plan. No push, no pull request, no tag, no release, no publication is authorized. Stage only the paths a plan task names. Treat the two untracked Unity temp meta files at the repository root as user owned and never stage them.

## Out of scope

A scene-view live preview through the NDMF `IRenderFilter` preview system. Predicting what upstream tools will do. Changes to the last-run status store. New NDMF report entries or report strings for the dry run. Any product behavior change for users who never open the foldout. Performance work beyond the fingerprint cache. Changes to the research package.
