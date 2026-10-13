# Show Material Transformations Dropdown — Design

Date: 2026-10-13

Base: `3ff7ed6` on branch `feat/show-material-transformations`

Investigation: `docs/superpowers/investigations/2026-10-13-show-material-transformations-investigation.md`

Revision note: first issue 2026-10-13 as commit `09ad56c`. Four adversarial reviews ran the same day against the first issue: accuracy against code and investigation, safety and security, architecture and ponytail adherence, and latent bugs. They confirmed three critical defects and twenty-four major defects in the first issue. This revision incorporates every confirmed fix. The load-bearing changes: the cache key is now the composite of an avatar fingerprint and a renderer fingerprint with single-slot eviction. The slot verdict gates move out of preparation into a pure Analysis decision function that preparation now delegates to. The extraction inventory is complete. The pipeline names every production capture input, including property block state and the locked material check. The disclosure rows now cover the direction where this view proves more than a build, EditorOnly removal, consent gated conversions, and unsupported build targets. A second amendment, dated 2026-10-13 from the implementation plan review round, names the three permitted Build symbols in decision 3, adds the errored row sentence to the Inspector section, and corrects the console substitution statement in decision 6.

## Overview

The AmuseAvatarOptimizer inspector gains a new top-level foldout, "Show Material Transformations". Inside it sits a nested foldout, "Alpha Separation". When the user opens the nested foldout, AMUSE runs its real proof pipeline on the avatar in the scene and draws what would happen, renderer by renderer and slot by slot.

The feature has six parts.

1. A display model of transformation rows. Pure records. No Unity mutation.
2. An edit-time dry run orchestrator. It reuses the Host capture, the Semantics frontends, and the Analysis classification and decision functions. It stops before preparation. It persists nothing.
3. An extraction into the Analysis layer, so the build and the dry run share one classifier, one policy mapping, and one slot verdict function.
4. A session cache keyed by a composite fingerprint, so repeated foldout opens stay cheap and stale results invalidate.
5. Inspector drawing for the foldout, the rows, the disclosure sentences, and a Refresh button.
6. Honest boundary disclosures, so the view never claims more than it computes.

The owner chose the edit-time exact dry run on 2026-10-13. The owner set one condition: no deferrals. Every mechanism the design reasonably needs is in scope, including GPU readback at edit time and edit-time animation closure capture.

## Decisions

These decisions close the investigation open points and the questions the owner choice raised.

1. **Strategy B, the edit-time exact dry run.** The owner chose it on 2026-10-13. The investigation recommended strategy A and judged B adversely in its section 8. The owner's dated choice supersedes that recommendation. The dry run applies the same proof rules to the scene avatar that a build applies to the build avatar. It is exact over its input state. It cannot see later rewrites, because AMUSE runs at PlatformFinish, after NDMF and other tools change the avatar (investigation section 5). The boundary has two directions, and the design discloses both. Later rewrites can remove or change what this view sees, so a build can prove less. Upstream tools can also add animation evidence through NDMF virtual clips, so a build can refuse slots this view proves. The view never claims the next build is guaranteed.
2. **Foldout placement and persistence.** The top-level foldout sits between the alpha separator block and the status HelpBox in `OnInspectorGUI`. Both foldout states are session bools in the editor, matching `_settingsOpen` and `_alphaSeparatorOpen`. The foldouts sit outside the Advanced Settings reveal, like the Alpha Separator toggle. The dropdown shows outcomes, not policy. No serialized component field is added, so presets, Undo, and Reset stay untouched (investigation open points 3 and 5, and the section 8 inference). Investigation open point 2, store keying, is answered by decision 8: one avatar slot.
3. **A new module for orchestration.** New folder `Editor/DryRun/` with namespace `Alrauna.Amuse.Editor.DryRun`. It calls Host, Analysis, and Semantics. It never calls Build, with three named exceptions, all pure reads with no persistence. First, the display sentences come from the `AmuseReportStrings` table in Build through its pure key builders and its pure `Get` and `Has` lookups. Second, the sampling selection comes from `PlatformAlphaSamplingPolicy.TrySelect`. Third, the locked material pre-check comes from `LockedMaterialIdentity.PreCheckRefusal`, the same delegate every production capture branch passes. No other Build symbol is permitted. The edges stay acyclic because Build never references DryRun. The precedent exists: the inspector already reads `AmuseBuildStatusStore` from Build. This is a deliberate reference from DryRun into the Build namespace. The alternative, a second sentence set or a second sampling and locked check, invites the exact drift the design forbids.
4. **Edit-time animation closure.** The structural graph comes from `CommittedControllerGraph.Enumerate(avatarRoot, VRChatPlatformAnimatorBindings.Instance)`, the same extension-free traversal the build's structural check runs before virtualization. The animation evidence comes from `UnityAnimationEvidenceCapture.Capture`, the production-shaped entry that needs no NDMF context and takes real policy bounds. The fallback bindings singleton supplies `IsSpecialMotion`. The dry run passes `LockedMaterialIdentity.PreCheckRefusal` as the locked refusal check, matching every production capture branch. Known boundaries, all disclosed: a build unions NDMF `AnimationIndex` virtual clips, which carry upstream tool rewrites and additions. The dry run sees authored clips only. At edit time nothing has rewritten the avatar yet, so authored clips are the true input state, but added virtual clip evidence is invisible to the view. A build converts locked Poiyomi materials after consent and transferred shaders after the D8 consent. The dry run runs the unconsented variant, so it matches a build where consent is declined or not yet granted. Decision 11 handles the display.
5. **Edit-time texture evidence.** The GPU blit and readback route `UnityAlphaFieldEvidence.TryCapture` runs unchanged at edit time on authored textures. It needs no CPU readability. The material-level batch `UnityMaterialEvidenceCapture.Capture(inputs, bounds)` supplies the orchestration, with the production capture arguments named in the pipeline section. The dry run filters streaming-mipmap textures out of the capture inputs before it calls the batch capture, and emits the avatar-level streaming row from that filter. The dry run never invokes `UnityStreamingTextureEvidence`, so the one capture route that writes project assets is unreachable at edit time. Three more named facts surface as rows, never as defects. First, a non-Windows active build target refuses texture capture wholesale, because the Windows import is not loaded. The dropdown shows a banner row. Second, an unsupported sampling target, one that is neither desktop Windows nor a mobile target the sampling policy knows, makes a real build refuse the whole avatar. The dropdown shows a banner row and suppresses transformation verdicts, matching the build that moves nothing. Third, the mip residency gate folds the editor quality settings of this session. The same gate runs inside a build in the same editor process, so this is a session fact, not a build divergence. Slot rows show its named refusal through the existing texture capture strings.
6. **No persistence, no report emission.** The dry run creates no persistent object: no asset, no import setting change, no scene object write, no component write. Transient AMUSE-owned capture transients, the property block material clones and the GPU readback temporaries, are created and destroyed inside the compute scope, exactly as in a build. The dry run calls no preparation and no apply. It emits no NDMF `ErrorReport` entries and adds no report strings, so the report completeness test gains no surface. Display rows reuse the existing refusal sentences from `AmuseReportStrings` through its pure key builders, so the avatar shows one vocabulary in the console and in the dropdown. Sentences carry format placeholders. One pure interpolation helper joins the table, and the dry run calls it. Console emission keeps its SimpleError substitution unchanged, because the console renders through NDMF error objects the dry run never creates. No new string keys appear. The dry run draws the bare title sentence per row. Details and hints stay console-only.
7. **One classifier, one policy mapping, one verdict function, extracted.** The build pass private pipeline moves to the Analysis layer as internal statics with bodies unchanged. The complete moved surface: `ResolveRuntimeStates`, `ClassifyRuntimeStates`, `MaterialSlotsFor` (already internal, with the preparation and test callers retargeted), `ClosureWayForSlot`, the private nested result class `ResolvedRuntimeStates`, and the six private helpers `FilterBlockEntriesForSlot`, `Refused`, `IntersectResolvedOutcomes`, `UnprovenOutcomes`, `NoAlphaChannelPropertiesFor`, `CaptureRefusalsFor`. The seven policy mapping helpers move beside them: `ProofMipCapFrom`, `MinTextureSizeFrom`, `MinimumCoverageFrom`, `MinimumPreserveCoverageFrom`, `OpaquePercentFrom`, `PolygonClampPercentFrom`, `DensityCapFromCoveragePercent`. New home: `Editor/Analysis/RuntimeStateResolution.cs` for the pipeline, and the policy mappings join it there. The pass delegates. The dry run calls the same functions. In addition, the slot verdict gates leave `AlphaSeparationPreparation.Prepare` as a new pure Analysis decision function, `SlotTransformDecision`: plan, resolved slot outcomes, and derived policy in, per-slot verdict out, covering the preserve fold, the convert coverage gate, the anisotropy disclosure, and the already opaque outcome. `Prepare` delegates its gate decisions to it. Build and dry run cannot drift. Visibility promotion is the only signature change. The existing `RunBarrier` suite pins the behavior of the whole move. The test seam `AnalyzeRuntimeStatesForTests` re-points at the new home. No logic changes anywhere in the move.
8. **Session cache with a composite fingerprint.** A static session cache in `Editor/DryRun/DryRunCache.cs`, one avatar slot. The avatar fingerprint is first class. It covers: the avatar root instance ID, every component policy integer and switch named in the component state section, the set of animator controller assets with their asset file ticks, and the structural graph outcome. The renderer fingerprint covers: renderer path, renderer type, mesh identity keyed by asset GUID plus source and meta file ticks when the mesh is an asset and instance ID otherwise, each slot material keyed the same way, and every admitted texture keyed the same way. Non-asset textures use instance ID plus update count. The renderer cache key is the avatar fingerprint joined with the renderer fingerprint, built by one internal pure function that both lookup and store call. A change to either side recomputes the renderer. Storing under a new avatar fingerprint evicts the previous avatar's entries, so the slot stays single. The avatar fingerprint is evaluated once per foldout-open cycle on the first draw pass, not once per renderer pass. Fingerprint evaluation uses cheap fields only. No image contents hash anywhere. A texture whose update count changes every frame, a live RenderTexture, would invalidate every pass, so such textures produce a named Refresh-only row and are excluded from the per-pass fingerprint. The cache dies on domain reload and recomputes on the next open. The forbidden capability latches in `UnityAlphaFieldEvidence` stay untouched.
9. **Row vocabulary.** The dropdown shows five row kinds in fixed order. Banner rows state avatar-level facts: AMUSE off, Alpha Separator off, root misplaced, structural graph refusal, non-Windows build target, unsupported sampling target. Disclosure rows state the computed-on boundary. Renderer rows state the renderer path, the type, and the outcome summary. Slot rows sit indented under their renderer. Note rows carry conditional facts: streaming textures present, consent gated conversions present, EditorOnly objects present, live textures present. The exact sentences live in the Inspector section. Count sentences avoid plural nouns, so no form ever reads wrong.
10. **Compute trigger, budget, and liveness.** The dry run computes lazily on foldout open. One renderer's capture and classification run per draw pass. Avatar-level work, the fingerprint and the structural graph, runs once per open. After a pass that computed a renderer, the editor requests a repaint on itself until the run completes, so the sequence advances without user input. The renderer list is re-enumerated at the start of each draw pass. A renderer that no longer exists is skipped and produces no row. A later full pass recomputes if it returns. A pass whose renderer throws an unexpected exception marks that fingerprint as errored in the session, draws the errored row, and does not recompute it until Refresh. Errored is a display state distinct from refused. The first open pays the texture readbacks. A disclosure sentence warns that the first open can take a moment. No async machinery and no background jobs.
11. **Host gates and consent at edit time.** Version, build-path, and host service gates decide whether a build runs. The dropdown shows analysis results regardless of them, and this omission is recorded here deliberately: the standing disclosure points at a real run for the authoritative result. Consent differs, because consent changes the analysis itself: granted shader transfer consent switches the capturer and the resolver to their transferred variants. The dry run always computes the unconsented variant. When the analysis meets a locked Poiyomi material or a transfer subject, the locked or transfer refusal shows in the slot row from the existing table, and the consent note row explains that a build with granted consent may convert further.
12. **Simple English everywhere.** Every label, tooltip, row, and disclosure sentence uses short active sentences. No contractions. One idea per sentence. The exact text lives in the Inspector section.

## Component state

No serialized field is added or changed. The runtime component is untouched. The editor adds two private session bools:

- `private bool _materialTransformationsOpen;`
- `private bool _alphaSeparationTransformationsOpen;`

The dry run reads policy from the component instance at compute time and maps it through the extracted policy helpers, never through raw values: `_amuseDisabled`, `_alphaSeparatorEnabled`, `_preserveTransparencyMaxMipLevel`, `_preserveTransparencyMinTextureSize`, `_minimumOpaqueCoveragePercent`, `_minimumOpaqueCoverageToPreservePercent`, `_minimumOpaqueAlphaPercent`, `_polygonMinimumOpaqueCoveragePercent`, `_polygonAlphaUpperClampPercent`, `_acceptDesktopAnisotropyDifferences`, `_acceptMobileAnisotropyDifferences`, `_ignoreOutOfRangeMaterialSlots`, `_allowDepthTestChange`. The sampling target comes from `PlatformAlphaSamplingPolicy.TrySelect` over the active build target and the two acceptance switches. All thirteen fields are in the avatar fingerprint.

## Inspector

The foldout titles are exact:

> Show Material Transformations

> Alpha Separation

The disclosure rows under the nested foldout title. The first is always present when any result shows:

> This view runs the same proof on the avatar in the scene. Other tools change the avatar later at build time. This view cannot see those changes. A tool can also add animations the build must obey. Then the build proves less than this view shows. A run in Play mode or an upload shows the true result.

> The first open measures textures on the GPU. It can take a moment.

The banner rows:

> AMUSE is off. Turn it on to see results.

> The Alpha Separator is off. Nothing will move. Turn it on to see results.

> This component must sit on the avatar root object.

> The active build target is not Windows. Texture proof needs the Windows import. This view can prove fewer triangles than a Windows build.

> The active build target supports no sampling policy. A build on this target applies nothing. This view shows no results for it.

The note rows, each conditional:

> Some textures use streaming mipmaps. This view does not read them, because reading would change project files. A build can prove more for those textures.

> This avatar holds locked or transfer protected materials. A build with granted consent may convert further than this view shows.

> Some objects are marked EditorOnly. The build removes them before AMUSE runs. This view shows what those renderers would get if they stayed.

> Some textures change every frame. This view computes them on Refresh only.

The structural refusal row shows the named avatar refusal sentence through the interpolation helper, prefixed by the renderer name when one renderer caused it, per the existing renderer-name convention.

Renderer row shapes, one line each, in hierarchy order. The renderer summary maps from its slot verdict set:

> <renderer path> (<type>): Triangles moved: <N>. Triangles proven: <K> of <M>. <R> of <S> slots refused.

> <renderer path> (<type>): no movement. Triangles proven: <K> of <M>.

> <renderer path> (<type>): no change. Nothing proven.

> <renderer path> (<type>): refused. <named refusal sentence>

`<N>` counts every triangle that leaves its original material: proven triangles from split slots and all triangles of folded slots. When a fold contributes to `<N>`, the renderer row appends:

> The preserve policy moves the rest.

Slot rows sit indented one level under their renderer:

> slot <i>: would split. Triangles proven: <K> of <M>.

> slot <i>: would fold onto the opaque copy. The unproven share is below the preserve setting.

> slot <i>: already opaque. It stays as it is.

> slot <i>: unchanged. Triangles proven: <K> of <M>. The share is below the convert setting.

> slot <i>: refused. <named slot refusal sentence>

The errored row, drawn when a renderer compute threw:

> <renderer path> (<type>): could not be computed. Press Refresh to try again.

Slot texture capture refusals fold into their slot row through the existing texture capture strings. The dry run asserts `AmuseReportStrings.Has(key)` before it renders any sentence. An empty sentence is a test failure, never a displayable row.

Out-of-range slots under the tolerance toggle produce no slot row, matching the console behavior of a build. The renderer row's `<S>` counts the renderer's real slot count, so missing rows stay visible as a difference.

The Refresh button draws at the top of the nested foldout, right aligned, labeled:

> Refresh

Empty states: the nested foldout shows the banner rows when no renderer result exists. When the avatar has no renderer at all, it shows:

> No renderer on this avatar.

## Dry run pipeline

The orchestrator type `AlphaSeparationDryRun` runs this sequence. It reads. It never writes.

1. **Avatar facts.** Read the component switches. If AMUSE is off or the Alpha Separator is off, produce banner rows and stop. If the component sits on a child, produce the root banner and stop. Select the sampling policy for the active build target. An unsupported target produces the sampling banner and stops.
2. **Structural graph.** Enumerate the authored controllers with `CommittedControllerGraph.Enumerate` and the fallback bindings. A refusal produces the structural row through the interpolation helper and stops. The graph result feeds every renderer.
3. **Per renderer, in hierarchy order, one renderer per pass.** For each `Renderer` under the avatar root, re-enumerated each pass:
   - Capture the property block state with `EffectiveMaterialMaterialization.CaptureBlockState(renderer)`.
   - Capture animation evidence with `UnityAnimationEvidenceCapture.Capture`, passing the renderer path, the current slot materials, the graph, the fallback bindings, the derived policy bounds, `LockedMaterialIdentity.PreCheckRefusal`, the component `_ignoreOutOfRangeMaterialSlots` value, and the renderer type name. A null-returning registered source lookup stands in for the build's registry, because authored materials carry no build registry entry.
   - Run `UnityRendererAlphaAnalysis.Capture` for the analysis refusal or the plan, and `CaptureGeometry` for the mesh validation refusals the build sees.
   - Resolve runtime states and classify triangles through the extracted `RuntimeStateResolution`, passing the block state entries.
   - Derive slot verdicts through the extracted `SlotTransformDecision`.
   - Build rows. A renderer refusal produces the refused row with its named sentence. Slot texture refusals attach to their slot rows. Renderers under EditorOnly objects carry the EditorOnly note.
4. **Cache write.** Store the renderer rows under the composite fingerprint. A partial pass stores what completed, so an interrupted first open resumes where it stopped. An errored pass stores the errored state for that fingerprint.
5. **Display model.** The rows are pure records: row kind, renderer path, renderer type, slot index, count fields, and one display sentence. The inspector draws them. The model knows nothing about IMGUI.

The dry run never calls `AlphaSeparationPreparation.Prepare`, `AlphaSeparationApply`, any clone builder, or `UnityStreamingTextureEvidence`. It emits no NDMF report entry. The transient unlock swap machinery of the build is absent at edit time. That absence is part of the authored-state boundary the disclosure names.

## Extraction and module boundaries

`Editor/Analysis/RuntimeStateResolution.cs` receives the pipeline statics, the nested result class, and the policy mapping helpers named in decision 7. `Editor/Analysis/SlotTransformDecision.cs` receives the pure verdict function extracted from preparation. The pass and the preparation delegate to them. The dry run calls them. Tests may call them directly through `InternalsVisibleTo`.

Dependency directions after the change:

- `Build` calls `Analysis`, `Host`, and `Semantics`. `DryRun` calls `Analysis`, `Host`, and `Semantics`. `DryRun` never calls `Build`, except the `AmuseReportStrings` table, its key builders, and its `Get` and `Has` lookups named in decision 3.

## Cache

`Editor/DryRun/DryRunCache.cs` is an internal static class. One avatar slot. Session scope. API shape:

- `bool TryGetAvatar(string avatarFingerprint, out IReadOnlyList<MaterialTransformationRow> avatarRows)`
- `void StoreAvatar(string avatarFingerprint, IReadOnlyList<MaterialTransformationRow> avatarRows)`
- `bool TryGetRenderer(string compositeKey, out IReadOnlyList<MaterialTransformationRow> rows)`
- `void StoreRenderer(string avatarFingerprint, string rendererFingerprint, IReadOnlyList<MaterialTransformationRow> rows)`
- `void MarkErrored(string compositeKey)`
- `void Clear()`

`StoreRenderer` joins the two fingerprints through the one internal pure function, evicts entries stored under a different avatar fingerprint, and stores. `Clear()` backs the Refresh button and forces a repaint.

## Invariants

1. The dry run persists nothing. No asset, no import setting change, no scene object write, no component write. The streaming clone route is unreachable at edit time by filter, not by hope. Transient capture transients live and die inside the compute scope.
2. The dry run emits no NDMF report entry and adds no report string key. A display row asserts the sentence key exists before it renders.
3. An unproven triangle never displays as proven. Unproven triangles display as moved only through the preserve-policy fold row, which names the policy. A missing virtual clip is Unknown at edit time. The display treats it as Unknown, never as absence of constraint.
4. Every refusal surfaces as a named value with an existing sentence. No blanket catch converts a programming defect into a refusal row. An unexpected exception surfaces as a console error and marks the errored display state for that fingerprint.
5. A stale cache can never serve a row whose composite fingerprint no longer matches. Policy changes, texture reimports, material edits, shader swaps, controller changes, mesh changes, and avatar identity changes all invalidate.
6. The build path keeps its exact behavior. Every extraction is a pure move or a delegation, pinned by the existing barrier suite. Any existing test outcome change is a stop condition.
7. The research package stays untouched. No research type appears in product code.
8. All display sentences use short active sentences. No contractions. One idea per sentence. No semicolons in prose. Count sentences avoid plural nouns.
9. One renderer's capture and classification run per draw pass. Avatar-level fingerprint and graph work runs once per open.
10. No absolute path, host name, port, or private identifier appears in any new file.

## Test strategy

New test folder `Tests/Editor/DryRun/`, mirroring production. Existing assemblies and harnesses apply: EditMode NUnit, `InternalsVisibleTo`, stand-in shaders under `Hidden/Alrauna/AmuseTests/*`, no vendor shaders, the `RunBarrier` harness for build-side behavior.

Per class:

- Extraction move: the existing `RunBarrier` suite must pass unchanged. Record as characterization. No new test asserts delegation itself.
- `RuntimeStateResolutionTests`: a thin class with two direct behavior asserts, one closure-refused slot and one unproven slot. No delegation asserts. This satisfies the one test class per production type convention without duplicating barrier coverage.
- `SlotTransformDecisionTests`: the verdict function in isolation. Covers split, fold, below-convert coverage refusal, already opaque, and anisotropy disclosure. Falsifier 1: a verdict function that skips the convert coverage gate labels a below-threshold slot as split. The below-threshold fixture kills it. Falsifier 2: a verdict function that skips the preserve gate leaves a qualifying slot on the unchanged row. The fold fixture kills it. Both falsifiers carry no-op guards. The same fixtures run through `Prepare` in the barrier suite, so delegation cannot drift.
- `MaterialTransformationRowTests`: pure row construction from verdicts and analysis outputs. Covers would-split, fold, already opaque, unchanged, refused, the mixed-refusal renderer summary, the no-movement renderer summary, and the count fields. Falsifier 3: a row builder that reads total triangles instead of proven ordinals produces a would-split row for a fully unproven slot. The unproven-slot fixture kills it.
- `AlphaSeparationDryRunTests`: end-to-end compute on a scene fixture with a stand-in cutout shader and runtime-created textures, in the style of the existing evidence tests. Covers: a proven triangle displays as would-split; unknown stays unchanged; a renderer analysis refusal shows the named sentence; the structural animation-event refusal stops with the structural row; AMUSE off and separator off produce banners and no compute; texture capture refusals attach to slot rows; a property block on the fixture renderer changes the displayed verdict the same way a build classification would.
- `DryRunCacheTests`: fingerprint invalidation through one injectable inputs record that production fills and tests fake. Falsifier 4: a cache keyed without the avatar fingerprint serves another avatar's rows. The avatar-swap fixture kills it. Falsifier 5: a cache keyed without policy fields serves stale rows after a policy change. The slider fixture kills it. Both falsifiers compare fingerprints built from production-shaped inputs, and each carries a no-op guard.
- No-persistence guard: one fixture with a streaming-mipmap texture and one renderer with a property block. Before and after a full dry run: asset count and path delta under the project, loaded `Material` instance count through `Resources.FindObjectsOfTypeAll<Material>()`, and absence of the streaming readback folder the clone route would create. A leaked transient clone or a reached clone route fails the test. The guard cannot catch a clone route that completes and cleans perfectly, and this test does not claim to.
- Build-target seam: the dry run takes the active build target through an injectable provider, following the existing test override pattern. The banner tests drive the seam. The capture refusal path asserts through the internal admitted-target predicate. No test switches the real build target.
- `AmuseAvatarOptimizerEditorTests` additions: the host window repaint harness gains a completion probe. The new test repaints in a loop until the dry run reports completion, then asserts the rows. A single-pass fixture pins one renderer, so the harness change and the fixture stay honest. A second fixture asserts banners when the separator is off.

RED/GREEN applies to every behavior change above. A first-run pass is recorded as characterization. A filtered run that reports zero tests is a failure. Every run records observed counts.

## Documentation

The README gains a section after the Alpha Separator settings section. Exact text:

> ### Show Material Transformations
>
> Open the Show Material Transformations foldout on the AMUSE component to see what the Alpha Separator would do on this avatar. The nested Alpha Separation view runs the same proof on the avatar in the scene. It shows, per renderer and per material slot, what would move, what would fold, what stays, and what is refused with a reason.
>
> The view reads the scene state. Other tools can change the avatar later during a build, and some tools add animations the build must obey. This view cannot see those changes. A run in Play mode or an upload shows the true result. The first open measures textures on the GPU. It can take a moment. The Refresh button recomputes everything.

## Acceptance criteria

1. The inspector shows the two foldouts at the stated position, outside Advanced Settings, and no serialized field changes on the component.
2. Opening the nested foldout on an avatar with a stand-in cutout material shows the renderer and slot rows that match a manual classification of the fixture, with correct counts, through the same policy mapping, resolution, and verdict functions the build delegates to.
3. An unproven slot never shows a would-split claim. A below-convert-coverage slot shows the unchanged row. A preserve-qualifying slot shows the fold row.
4. Each named refusal shows its existing report sentence through the interpolation helper. No row ever renders an empty sentence.
5. The banners appear for AMUSE off, separator off, child placement, structural refusal, non-Windows target, and unsupported sampling target. No renderer compute runs behind the first two banners or the unsupported sampling banner.
6. A policy slider change invalidates the cached rows without the Refresh button. An avatar swap never serves the previous avatar's rows.
7. The Refresh button clears the cache, clears errored marks, and repaints.
8. A full dry run persists nothing: no asset, no import change, no scene write, no report entry, and no leaked transient material.
9. The existing full product test assemblies pass with unchanged outcomes after the extractions. Observed counts are recorded.
10. Every new display sentence passes the simple English sweep.

## Validation and stop conditions

Order of validation: Unity refresh after new files, focused new-test filters, the full `Alrauna.Amuse.Tests.Editor` assembly, then the full `Alrauna.Amuse.Research.Tests.Editor` assembly. Record observed counts for every run. A filtered run with zero tests is a failure.

Stop and return evidence when:

- any existing test changes outcome after an extraction or a delegation,
- edit-time capture proves impossible on the stand-in fixtures for a reason this spec does not name,
- the dry run cannot complete without persisting state beyond the named transients,
- a display row needs a sentence that no existing report string covers and the report table would have to grow.

The last stop condition triggers the report-string completeness path, never a silent new string.

## Authorization boundary

Dated owner authorizations, all from 2026-10-13. They cover: the feature branch and commits between finalized changes. The strategy B choice with no deferrals. Adversarial review rounds between phases. Subagent-driven execution of the plan. No push, no pull request, no tag, no release, no publication is authorized. Stage only the paths a plan task names. Treat the two untracked Unity temp meta files at the repository root as user owned and never stage them.

## Out of scope

A scene-view live preview through the NDMF `IRenderFilter` preview system. Predicting what upstream tools will do. Simulating granted consent variants beyond the unconsented computation and its note row. Changes to the last-run status store. New NDMF report entries or report strings for the dry run. Any product behavior change for users who never open the foldout. Performance work beyond the fingerprint cache. Changes to the research package.
