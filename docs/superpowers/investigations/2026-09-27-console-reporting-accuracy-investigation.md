# Reporting accuracy in the AMUSE NDMF console

Privacy note: This record reads one private avatar's build in the Census Lab project as background evidence. It names no avatar, scene, renderer, material, texture, or asset path. It holds no per-renderer or per-slot table. It describes tools by product name and code by repository-relative paths. It writes no instance names, hashes, ports, or machine paths. Counts are observed counts.

Date: 2026-09-27.
Branch: `feat/console-report-hardening`.
Base: `main` at `0052076`.
Investigation status on 2026-09-27: no production code changed. The work was source analysis of the AMUSE repository and of the installed NDMF 1.14.4 sources, plus console evidence already gathered from Census Lab builds on 2026-09-27. No build ran for this record and no Census Lab file was edited.

## Question

The previous record identified Avatar Optimizer's atlas pass as the producer of the source-less `_MainTex`, and its report hint as misleading. This record widens the question. What can make AMUSE's console reporting wrong or misleading today, either inside AMUSE's own report path, or through assets that upstream build callbacks generate and AMUSE consumes? The goal is a hardening list for this branch, not a fix.

## How AMUSE reports today

AMUSE reports through NDMF's error report with `ErrorSeverity.Information`, so a refusal never blocks a build. Each renderer report carries the renderer as its context object, so the NDMF console links to the live object. The report families:

- One line per refused slot, with the slot index, the refusal name, the build-copy renderer name, the offender material's project path or name, and one sentence naming the exact shader fact.
- One line per texture capture refusal, with the slot index, the texture property, the channel, the reason family, and the identity fact, so the text says whether the texture has a source identity.
- One line per renderer-scope refusal.
- One avatar summary line: "AMUSE analyzed {0} renderers and moved {1} triangles to opaque materials. {2} renderers kept everything original."
- A session-scoped status store that records that summary text under the processed avatar root's instance ID, plus the hint "Open this component to see the same status."

## Internal findings

### 1. The summary sentence overstates coverage

The third summary argument is the renderer-scope refusal counter. A renderer whose every material slot was refused at slot level does not refuse at renderer scope. Such a renderer counts into "analyzed {0}", moves zero triangles, and stays absent from "{2} renderers kept everything original". A reader of the summary cannot see that those renderers produced no proof at all. Evidence: `AmusePlatformFinishPlugin.cs` counts `AnalyzedRendererCount` for every renderer that reached classification, `RecordRendererRefusal` is the only writer of the renderer-scope counter, and `AlphaSeparationApply.cs` passes that counter as the "kept everything original" argument. The 2026-09-26 avatar had exactly this shape: two slot refusals and one capture refusal, all invisible in the summary numbers.

Hardening options: reword the sentence to name renderer-scope refusals, or count a renderer as kept-original when every one of its slots stayed unresolved. The second keeps the plain meaning of the sentence.

### 2. The last-build status store cannot surface on play and upload builds

The store records under `context.AvatarRootObject.GetInstanceID()`, the processed build copy. The only reader is the inspector of the `AmuseAvatarOptimizer` component, and it queries its own game object's instance ID. NDMF strips `INDMFEditorOnly` components from every processed build copy, so the processed object never carries the reader. On a play build the processed copy replaces the scene avatar and is destroyed at play end. On an upload build the processed copy is transient. On both paths no surviving object holds the recorded key, and the hint "Open this component to see the same status." cannot be fulfilled. Evidence: `AmuseReports.AvatarSummary`, `AmuseBuildStatusStore`, and `AmuseAvatarOptimizerEditor.cs` lines 53 to 57.

Hardening options: resolve the processed root back to its source object through NDMF's object registry and record under the source's identity, or record per build path and show the store in the NDMF error window, or drop the hint. The registry route is the one that matches the ecosystem contract.

### 3. Report text names build-copy objects

Slot lines embed `renderer.gameObject.name` and the offender material's project path, with the material name as fallback. Upstream tools rename and clone during the build, so the text can name an object or path that does not exist in the authoring scene. The context-object link keeps the report reachable, but the text alone can misdirect. Evidence: `AmuseReports.SlotAnalysisRefusal` and `MaterialDescription`.

Hardening options: resolve the offender through NDMF's object registry when a producer registered the replacement, and prefer the resolved source name; keep the live object link as the primary attribution.

### 4. Shared materials print duplicate lines

One material on two slots of one renderer produces one identical capture-refusal line per slot. This is deliberate, one line per slot with no aggregation, and a manual test reads the full list. The 2026-09-27 build log shows the shape: NDMF reported the same-material multi-slot fact for this avatar in the Optimizing phase. No change is proposed; the record states the intent so the review does not call it a defect.

### 5. The animator re-clone noise lands in AMUSE's window

NDMF's `VirtualControllerContext` logs "Controller ... was changed outside of NDMF animator services; cloning a second time" at every context activation where the avatar descriptor's controller no longer matches the last committed object. The 2026-09-27 build log shows eight such lines per activation, for every virtualized layer, repeated across the PlatformFinish passes that declare the animator context. AMUSE declares that context by design: the bindings capture needs the host bindings, and the apply pass needs the reactivated animation index. The writer that changed the descriptor between cycles sits upstream of AMUSE and is not identified by static reading. AMUSE's evidence stays correct, because the reactivated context re-clones from the live descriptor before AMUSE reads it, and the apply pass validates against that live view. The defect is attribution: a reader sees the lines between AMUSE pass names and reads them as AMUSE output.

Hardening options: one traced build with NDMF's animator debugging identifies the upstream writer; until then, document in the README that these NDMF lines next to AMUSE passes are re-clone notices about upstream writes, not AMUSE failures. AMUSE cannot reword NDMF's log.

### 6. Positive results

The audit found no silent refusal path: every slot and renderer refusal has a report call site, and the avatar-scope `AmusePreparationDecision.Refused` has no call site at all today. The texture report prints the reason family and the identity fact exactly as captured. Every message uses Information severity, so reporting never blocks a build.

## Upstream findings

### 7. Unsaved generator outputs have no identity, and the hint misleads

The 2026-09-27 atlas record measured the case: Avatar Optimizer's atlas pass builds in-memory textures, saves nothing, and registers nothing, so AMUSE's identity gate refuses with "has no source identity". The source audit generalizes it. Avatar Optimizer calls `SaveAsset` nowhere in this version, so its cloned materials and meshes are also in-memory objects. VRCFury creates its objects through its own factory outside NDMF's build and registry. For every such output, two report texts go wrong at once: the capture refusal hint says "Check that the texture is a real imported asset", which is false advice for a build-generated texture, and material lines fall back to the clone's name because the clone has no project path.

Hardening options: reword the no-identity hint to name the mechanism, an upstream build step replaced this texture with an in-memory copy, and to point at the texture-atlas setting of Trace and Optimize as the known producer. For materials, resolve through the object registry before falling back to the clone name, because Avatar Optimizer registers its material replacements there.

### 8. Saved generator outputs carry honest identity

NDMF's `AssetSaver` writes real on-disk containers under a generated per-build folder at build start and deletes that folder on the next build. Assets saved through `context.SaveAsset`, for example Modular Avatar's outputs, become persistent objects with a project path mid-build. They pass AMUSE's identity gate, and a capture of them describes exactly the pixels the build uses, so the report's identity sentence stays truthful. The deciding line between findings 7 and 8 is one question: does the producer save the object. Recorded so the review treats the two families differently.

### 9. The registry is the honest reconstruction route, and the ecosystem already uses it

NDMF's object registry maps a build-copy object back to its registered source, and its documentation tells producers to register every clone. Avatar Optimizer registers replaced meshes, materials, and clips, and its own mesh-mask processor resolves the original texture through `ObjectRegistry.GetReference` when it needs the importer. Modular Avatar registers replaced meshes. Atlas textures remain the registered exception, as the previous record established. AMUSE consumes none of this today. Registry resolution would improve findings 2, 3, and 7 with one mechanism.

## Recommendation

Order the hardening work on this branch as follows. First, the two pure report-text changes: the summary sentence and its counter in finding 1, and the no-identity hint in finding 7. Both are string-level, carry no correctness risk, and fix reported text that is wrong today on avatars the Lab already exercises. Second, registry-based naming and status keying for findings 2, 3, and 7, one mechanism in three places, behind the existing evidence rules: resolve only through registered replacements, and fall back to today's text when the registry has no entry. Third, documentation for findings 4 and 5, and one traced build to name the upstream writer behind the re-clone noise. No finding requires a production correctness change; all of them change what the console says.

## Scope and limits

This record ran no build and changed no code. Findings 1, 2, 3, 6, and 8 rest on repository and NDMF source at the pinned versions: AMUSE at this branch base, NDMF 1.14.4, Avatar Optimizer 1.9.17, Modular Avatar 1.18.2, VRCFury 1.1426.0. Finding 5's writer attribution is open and needs one traced build. Finding 7's VRCFury half rests on code reading, not on a measured VRCFury-produced texture. The manual-bake path of finding 2 is not established: whether a manual bake keeps the reader component and a matching instance identity was not measured. Later tool versions can change any upstream behavior named here.
