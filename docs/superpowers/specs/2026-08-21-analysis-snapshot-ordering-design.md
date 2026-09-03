# AMUSE analysis snapshot and ordering investigation

## Status

**The investigation is complete. The design is proposed. Production work is blocked on review.**

This document records a source-backed lifecycle result and a deliberately narrow architecture. It does not implement an NDMF plugin, snapshot types, shader integration, or transformations. One lifecycle gap remains for positive build-time lilToon attestation. The document calls this gap out as a separate prerequisite, not as a broad abstraction.

- Branch: `investigate/analysis-snapshot-ordering`
- Base: `origin/main` at `047a391a` (`Merge pull request #9 ... liltoon-official-integration-matrix`)
- NDMF package investigated: `nadena.dev.ndmf` 1.14.4, upstream commit [`7cf8a13444ac19e46ac2b4146bad209de15dc42d`](https://github.com/bdunderscore/ndmf/tree/7cf8a13444ac19e46ac2b4146bad209de15dc42d)

The embedded NDMF 1.14.4 source under `Packages/nadena.dev.ndmf/` was compared with that upstream revision. Its product source was identical. The differences were repository-only tests, samples, and dependency bootstrapping. The conclusions below apply to this exact version, not to current NDMF.

AMUSE's package range is `nadena.dev.ndmf >=1.14.4 <2.0.0-a`. `Packages/vpm-manifest.json` and the embedded package resolve 1.14.4 exactly. `Alrauna.Amuse.Editor.asmdef` references `nadena.dev.ndmf`, but the repository currently exports no AMUSE `Plugin<T>`, pass, SDK build callback, or mutation executor. The lifecycle recommendations are therefore requirements for a future integration, not a description of code that already runs.

## Decision summary

No single universal lifecycle point in the presently observed VRChat build pipeline makes all AMUSE trust-relevant state final.

There are instead two distinct findings:

1. **NDMF-visible avatar state has a sound boundary.** For supported NDMF and pre-NDMF-Optimizing mutators, AMUSE should eventually use one late `Optimizing` pass, explicitly ordered after each supported same-phase mutator. That pass should eagerly extract immutable proof data, then reason and plan only from those values, then apply the resulting mutation immediately in the same pass. AMUSE may retain a Unity object separately as the immediate mutation target, but never as proof data.
2. **Positive build-time lilToon source attestation does not yet have a sound boundary.** lilToon 2.3.4 regenerates build shader settings and source in its VRChat SDK preprocess callback at order `100`. NDMF runs `Optimizing` through `PlatformFinish` earlier, at callback order `-1025`. So no NDMF pass can observe the final source that lilToon will generate for that upload. Snapshot purity fixes coherence after extraction. It cannot repair an extraction point that comes too early.

The smallest justified result is therefore **C, a focused extraction migration**. Late snapshot plus immediate analysis, planning, and mutation forms the core NDMF architecture. A separate lifecycle investigation must run before AMUSE enables positive official lilToon integration. The one-pass NDMF model does not require revalidation. A design that carries a conditional proof or mutation across the lilToon/SDK callback boundary would have to validate every dependency that can change across that boundary. This branch does not establish that either a cross-callback mutation design or a validation-gate design is sound.

Keep three conclusions distinct:

- **Core AMUSE NDMF architecture: resolved.** One explicitly ordered late `Optimizing` pass owns eager extraction, immutable reasoning, planning, and immediate mutation for supported NDMF-visible state.
- **Host extraction migration: required.** The current mixed and live host adapters must narrow into eager immutable inputs. The already-pure analysis and planning layers stay intact.
- **Positive build-time lilToon source attestation: blocked.** A separate callback-handoff probe must determine whether late mutation is safe, whether conditional early mutation plus a complete late validation gate is safe, or whether neither is safe.

## Problem statement

AMUSE authorizes a transformation only when it can name which state it proved. Today, important host adapters read mutable Unity objects, AssetDatabase records, package metadata, and files at different times. Some normalized and planning layers are already immutable, but the current vertical slice can still combine evidence from moments that a build-time mutator could distinguish.

The failure is architectural time-of-check/time-of-use:

1. extract material or source evidence.
2. later re-read a material, mesh, texture, importer, animation, package, or source file.
3. prove and plan using the combination.
4. mutate a target that may no longer hold those facts.

More uncertainty must reduce optimization. A stale proof is not merely a missed optimization. It can create a false positive.

## Lifecycle ordering and snapshot coherence are separate

**Lifecycle ordering** answers when AMUSE executes, and which build mutations it runs before or after. NDMF phase order, explicit dependency edges, or SDK callback order establish it.

**Snapshot coherence** answers whether all proof inputs describe the same observed build state, and whether the proof uses only those facts. Eager extraction into immutable AMUSE-owned values establishes it, together with a rule against later live re-reads during interpretation, proof, and planning.

Ordering without a snapshot permits material, source, or mesh evidence from different moments. A snapshot taken before a later mutator is coherent but stale. Soundness needs both.

For supported Unity build plugins, passes do not interleave while one pass executes. So "one coherent build state" need not mean a database transaction. It means a bounded, eager extraction operation at a justified lifecycle point, followed by reasoning over captured values only, followed by immediate mutation before control returns to the host.

## Evidence strength

This investigation labels its foundations as follows:

- **Documented guarantee:** an upstream API contract or lifecycle description.
- **Source guarantee:** behavior directly enforced by the pinned implementation.
- **Empirical observation:** a local exact-version comparison or experiment.
- **Inference:** a conclusion that follows from the above, but is not itself promised by an API.

The selected correctness boundary depends on source guarantees and explicit constraints. It does not depend on alphabetical discovery order or on an untested convention.

## Exact NDMF 1.14.4 lifecycle

### Phases and execution

[`BuildPhase.BuiltInPhases`](https://github.com/bdunderscore/ndmf/blob/7cf8a13444ac19e46ac2b4146bad209de15dc42d/Editor/API/Attributes/BuildPhase.cs) defines this exact order:

1. `FirstChance`
2. internal `InternalPrePlatformInit`
3. `PlatformInit`
4. `Resolving`
5. `Generating`
6. `Transforming`
7. `Optimizing`
8. `PlatformFinish`

The internal phase is not a public registration surface. The source descriptions call `Optimizing` the optimization phase and `PlatformFinish` platform cleanup and validation, but a phase name is not a prohibition: a plugin can still write Unity state from `PlatformFinish`.

[`AvatarProcessor.ProcessAvatar`](https://github.com/bdunderscore/ndmf/blob/7cf8a13444ac19e46ac2b4146bad209de15dc42d/Editor/AvatarProcessor.cs) walks phases and topologically ordered passes in sequence, and invokes every pass on the same `BuildContext`. A later pass directly sees any mutation an earlier pass made. There is no pass-local clone or isolation layer.

### Plugin and pass registration

A plugin implements `Configure`, calls `InPhase(phase)`, optionally constrains the returned sequence, and adds passes with `Run`. A sequence gives `BeforePlugin(qualifiedName)`, `AfterPlugin(qualifiedName)`, `WaitFor(...)`, and `AfterPass(...)`. A declared pass also gives `BeforePlugin(...)` and `BeforePass(...)`.

The exact implementation is in [`Sequence/Constraints.cs`](https://github.com/bdunderscore/ndmf/blob/7cf8a13444ac19e46ac2b4146bad209de15dc42d/Editor/API/Fluent/Sequence/Constraints.cs) and [`Sequence.cs`](https://github.com/bdunderscore/ndmf/blob/7cf8a13444ac19e46ac2b4146bad209de15dc42d/Editor/API/Fluent/Sequence/Sequence.cs). Passes added to one sequence receive explicit sequence edges. Separate sequences in the same phase stay unordered relative to each other unless dependency edges connect them.

Plugin start and end phantom nodes give `BeforePlugin` and `AfterPlugin` their whole-plugin meaning within a phase. [`PluginResolver`](https://github.com/bdunderscore/ndmf/blob/7cf8a13444ac19e46ac2b4146bad209de15dc42d/Editor/API/Solver/PluginResolver.cs) constructs a separate graph for each phase. A constraint cannot order passes across phases. Different phases are ordered by the phase list instead.

[`TopoSort`](https://github.com/bdunderscore/ndmf/blob/7cf8a13444ac19e46ac2b4146bad209de15dc42d/Editor/API/Solver/TopoSort.cs) enforces graph edges transitively and rejects cycles. The exact-version [`BeforeAfterPlugin` test](https://github.com/bdunderscore/ndmf/blob/7cf8a13444ac19e46ac2b4146bad209de15dc42d/UnitTests~/PluginResolverTests/BeforeAfterPlugin.cs) exercises plugin-level before/after order.

If another plugin has no compatible edge, its relative order is not a correctness guarantee. Resolver discovery is sorted by fully qualified type name, and unconstrained nodes inherit fallback order, but AMUSE must not read that as a contractual order. A constraint that names an absent plugin gives no useful order against real passes. A constraint that names a missing pass is ignored.

There is also a pinned-version hazard: `Sequence.AfterPass(string)` constructs the edge in the opposite direction from its name in 1.14.4, and no exact-version upstream unit test was found for that method. AMUSE should not base its boundary on that API in 1.14.4. Whole-plugin `AfterPlugin` is enough for the proposed model. Any future pass-level dependency needs an exact-version probe or upstream resolution first.

### Build clone, identity, and visibility

`BuildContext.AvatarRootObject` is the avatar instance under build. [`BuildContext`](https://github.com/bdunderscore/ndmf/blob/7cf8a13444ac19e46ac2b4146bad209de15dc42d/Editor/API/BuildContext.cs) does not itself clone the root. The surrounding host or manual entry point supplies the build clone. Manual processing explicitly instantiates one. The VRChat SDK likewise invokes preprocessors on its own build object.

`BuildContext.GetState<T>` can retain AMUSE-owned state across passes, but storage does not preserve validity. `ObjectRegistry` tracks provenance and replacements for diagnostics. It is not an immutable snapshot, a stable current-object handle, or a promise that a stored `Material`, `Mesh`, or `Renderer` reference still names the effective object after later passes.

Later passes see any earlier assignment, replacement, or destruction immediately, because they share the build object graph. So a retained Unity reference can become stale even though it still compares non-null under Unity's overloaded semantics, until or unless destroyed.

### VRChat callback split

NDMF 1.14.4 does not run all phases in one SDK callback. [`BuildFrameworkPreprocessHook`](https://github.com/bdunderscore/ndmf/blob/7cf8a13444ac19e46ac2b4146bad209de15dc42d/Editor/VRChat/BuildFrameworkPreprocessHook.cs) uses:

- callback order `-11000` to create the context and run through `Transforming`.
- callback order `-1025` to resume that context and run `Optimizing` through `PlatformFinish`, then finish and serialize generated assets.

This split makes mutations by SDK preprocessors between those orders visible to NDMF `Optimizing`. It also means SDK preprocessors after `-1025` stay invisible to every NDMF phase.

NDMF cleanup at SDK callback order `0` deletes its temporary asset directory after the build. `Finish` performs extension cleanup, UV distribution recalculation for temporary meshes, and asset serialization. Those operations confirm that arbitrary filesystem reads are not transactional. They do not add a semantic mutation barrier around third-party callbacks.

### What NDMF does and does not guarantee

NDMF guarantees, by pinned source:

- total phase order.
- topological enforcement of declared same-phase constraints.
- sequential visibility of mutations to later passes.
- one continuing `BuildContext` across its VRChat callback split.

NDMF does not guarantee:

- that unconstrained plugins have a semantic order.
- that `PlatformFinish` plugins are read-only.
- that filesystem and package reads form an atomic transaction.
- that no SDK preprocessor runs after NDMF.
- or that stored Unity references still describe the facts observed earlier.

A pass may technically read files while it mutates the avatar. NDMF neither prohibits that, nor makes the reads atomic.

## Current AMUSE live-state audit

### A. Renderer topology and state

`Editor/Host/UnityRendererAlphaAnalysis.cs` reads live:

- renderer concrete type.
- `Renderer.HasPropertyBlock()`.
- `SkinnedMeshRenderer.sharedMesh`, or the sibling `MeshFilter.sharedMesh`.
- `Renderer.sharedMaterials`.
- mesh and submesh counts and topology.

It copies the material array that Unity returns, and it eventually copies geometry into analysis inputs, but it retains the `Material` objects long enough for downstream semantic extraction and texture collection to re-read them. It does not capture the property-block contents. Their presence causes conservative refusal. The renderer proof does not presently include render queue, keywords, tags, per-slot property values, enabled state, or broader renderer settings.

Mutators can replace the mesh, replace materials, change slot assignments, add or remove submeshes, or attach property blocks. Any such change can invalidate slot mapping, triangle classification, and the separation plan.

### B. Mesh and geometry evidence

The renderer analyzer reads live `vertices`, `uv`, `vertexCount`, `subMeshCount`, `GetTopology`, and `GetIndices`. It then constructs immutable triangle inputs and `MeshSeparationInput` values. The pure classifier and planner do not re-read the `Mesh`.

This is snapshot-pure after extraction, but the extractor combines renderer, mesh, material, and texture work in one method. A mesh replacement before or during that sequence could mismatch the eventual target. The intended one-pass host model prevents plugin interleaving, but production extraction still needs an explicit copy boundary.

### C. Material state

`Editor/Semantics/UnityMaterialSemantics.cs` dispatches a live `Material` to the Poiyomi or lilToon frontend. Both frontends first gather copied source-attestation evidence, then interpret the live `Material` again through `HasProperty`, `GetFloat`, `GetColor`, `GetVector`, `GetTexture`, texture scale, and texture offset calls. They also read the global `QualitySettings.activeColorSpace`.

So source identity and material values are not currently one immutable input. A shader or property change between attestation and interpretation invalidates the semantic result. The current frontends do not broadly snapshot render queue, keyword sets, override tags, or all serialized properties. They read only the exact properties their supported semantics need.

### D. Texture and importer evidence

`Editor/Semantics/UnityTextureEvidence.cs` reads:

- AssetDatabase GUID/local file identity and asset path.
- live texture filter, wrap, mip, bias, anisotropy, and dimensions/format where relevant.
- `TextureImporter` sRGB, source alpha, alpha source, type, and green-channel settings.

`Editor/Host/UnityAlphaFieldEvidence.cs` currently stores a dictionary that maps `TextureSourceId` to a live `Texture2D`. It extracts pixel evidence lazily later, using `isReadable`, format, dimensions, mip count, and `GetPixels32`. So sampling/importer evidence and alpha bytes can describe different moments. Once built, `AlphaTextureData` is an immutable byte/value record.

A texture reassignment, reimport, importer change, generated texture replacement, or pixel rewrite invalidates the matching sample and every semantic/proof node that depends on it.

### E. Shader and source identity

Poiyomi source gathering reads live shader identity and optimizer state, AssetDatabase path/GUID, shader file text, and `PackageInfo`.

lilToon gathering reads live material/shader/version state, AssetDatabase GUID/path, package identity, project root, generated shader and pass sources, include trees, and canonical hashes. Directory enumeration and file reads occur independently. Its copied `LilToonSourceEvidence` is immutable enough for attestation, but later material interpretation still reads the host object.

The completed official-integration matrix adds exact external executable closure, activation evidence, include order, and macro provenance to the required tuple. A generated-source rewrite, include change, package update, shader reassignment, or activation change invalidates attestation and every semantic conclusion derived from it.

### F. Animation and reachable material state

Current production AMUSE has no host extractor for animator controllers, object-reference material swaps, or animated material properties. The renderer analyzer explicitly proves only the currently assigned, base state. This is a missing evidence domain, not a live-reference leak in a completed subsystem.

Future extraction must eagerly capture the reachable material identities and relevant value variations after controller-generating tools have run. A controller/clip rewrite, binding/path change, new material swap, or animated-property change invalidates the reachable-state set, and every combined renderer semantics/plan derived from it.

### G. Other build-context evidence

The current code reads `QualitySettings.activeColorSpace`, AssetDatabase state, package resolution, and project-relative filesystem state. It does not currently consume NDMF `BuildContext`, object-registry state, or a production mutation target. The research census calls the same live analyzers, but it is measurement tooling, not a build mutation pipeline.

## Snapshot-purity classification

| Component | Classification | Reason |
|---|---|---|
| `MaterialSemantics` and semantic value records | 1. Already snapshot-pure | Value objects contain normalized facts and texture source IDs, not Unity objects. |
| `AlphaSemanticsResolver` | 1. Already snapshot-pure | Pure over semantic values plus an injected evidence lookup. |
| `TriangleAlphaClassifier`, `ExactUvGeometry`, `MeshSeparationPlanner` | 1. Already snapshot-pure | Consume copied deterministic inputs and return immutable results. |
| `UnityTextureEvidence` | 2. Extraction boundary | Directly reads Unity/AssetDatabase/importer state and returns values. |
| Poiyomi/lilToon source evidence records | 2. Extraction boundary after construction | Captured values are usable without re-reading files, but construction is live. |
| `UnityRendererAlphaAnalysis` | 3. Mixed extraction/reasoning | Reads renderer/mesh/material/texture host state and invokes proof/planning in one public operation. |
| Poiyomi/lilToon material frontends | 3. Mixed extraction/reasoning | Attest copied source evidence, then interpret the live material. |
| `UnityAlphaFieldEvidence` | 4. Live-state dependency to isolate | Retains `Texture2D` and extracts pixels lazily. |
| Future renderer/material/mesh handles used only to apply a plan | 5. Mutation-only host dependency | Not implemented yet. Permitted only under the target policy below. |

The migration is broader than adding one wrapper object, but it stays bounded: pure analysis and planning already have the desired shape. The remaining work is to make host extraction eager, and to stop semantic frontends from re-reading Unity objects after capture.

## Representative mutation sources

The pinned revisions are mechanism evidence, not a promise to support every version.

### NDMF itself — 1.14.4 / `7cf8a134`

NDMF invokes all registered passes on the build clone and makes prior mutations visible. It may create and serialize temporary assets, and it performs finalization. It supplies ordering machinery, not a global read barrier. AMUSE can order against named NDMF plugins within a phase.

### Avatar Optimizer — 1.9.18-beta.1 / [`6e6babc5`](https://github.com/anatawa12/AvatarOptimizer/blob/6e6babc53c4086e7b1038b50dc01b1e36f065ef1/Editor/OptimizerPlugin.cs)

Qualified plugin name: `com.anatawa12.avatar-optimizer`.

It performs resolving work, then does its main optimization in `Optimizing`: mesh/submesh removal, renderer merging, material/texture optimization, asset replacement, and animation-related rewriting. Its source comments intentionally seek late fallback placement by namespace/type ordering, rather than declaring a universal after edge. That fallback is not enough for AMUSE correctness.

AMUSE can explicitly place its `Optimizing` sequence `AfterPlugin("com.anatawa12.avatar-optimizer")`. This is a real graph edge, and it is stronger than reliance on discovery order.

### TexTransTool — 1.0.2 / [`741b7dc3`](https://github.com/ReinaS-64892/TexTransTool/blob/741b7dc3febc1d77269f267f4cf139db0f12492a/Editor/NDMF/NDMFPlugin.cs)

Qualified plugin name: `net.rs64.tex-trans-tool`.

It performs material/texture work and UV changes in `Transforming`, then additional negotiation and cleanup in `Optimizing`. Its [`MaterialModifier`](https://github.com/ReinaS-64892/TexTransTool/blob/741b7dc3febc1d77269f267f4cf139db0f12492a/Runtime/CommonComponent/MaterialModifier.cs) can change the shader, render queue, and arbitrary material properties. Other passes replace textures, mesh UVs, shared meshes, and material assignments. Its optimizing sequence declares itself before Avatar Optimizer.

An AMUSE `Optimizing` sequence explicitly after both TexTransTool and Avatar Optimizer sees their supported final results.

### Modular Avatar — 1.18.3 / [`f8c5fd98`](https://github.com/bdunderscore/modular-avatar/blob/f8c5fd98463e1024cae0608d5449b3c1fb6b6c84/Editor/PluginDefinition/PluginDefinition.cs)

Qualified plugin names include `nadena.dev.modular-avatar` and `nadena.dev.modular-avatar.late-transform-stages`.

Modular Avatar resolves references, changes hierarchy and meshes, generates and rewrites animator/controller/clip state including object-reference material swap curves, and runs an `Optimizing` garbage-collection pass. Its late transforming plugin explicitly orders after the main Modular Avatar plugin and TexTransTool. AMUSE needs final reachable animation/material state and live targets, so it should order explicitly after the Modular Avatar optimizing plugin, as well as naturally after its earlier phases.

### VRCFury — pinned source [`b5e9f963`](https://github.com/VRCFury/VRCFury/blob/b5e9f9630e40e93c47fe06f5aa71897dba92cfca/com.vrcfury.vrcfury/Editor-Avatars/Hooks/VrcPreuploadHook.cs)

The pinned avatar build integration is not an NDMF plugin. Its `VrcPreuploadHook` is a VRChat SDK preprocessor at callback order `-10000`: after NDMF's `-11000` early hook, and before NDMF's `-1025` optimizing hook. It can alter meshes, material assignments, animations/controllers, and materials. The [`SPS patcher`](https://github.com/VRCFury/VRCFury/blob/b5e9f9630e40e93c47fe06f5aa71897dba92cfca/com.vrcfury.vrcfury/Editor-Common/Builder/Haptics/SpsPatcher.cs) reads and writes generated shader source, and assigns the patched shader.

For this exact integration shape, NDMF `Optimizing` observes VRCFury's completed preupload mutation. `AfterPlugin` cannot express this relationship, because VRCFury is not an NDMF plugin. SDK callback order is the evidence instead.

### lilToon — 2.3.4 / [`252fd8cf`](https://github.com/lilxyzw/lilToon/tree/252fd8cfc46106d4967e95b3f2c788418502f227)

lilToon is not an NDMF plugin. [`VRChatModule`](https://github.com/lilxyzw/lilToon/blob/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/External/Editor/VRChatModule.cs) is a VRChat SDK callback at order `100`, after NDMF has finished. Its avatar preprocessor enumerates final renderer materials and animation clips, then calls `SetShaderSettingBeforeBuild` and multi-material setup. [`lilToonSetting.cs`](https://github.com/lilxyzw/lilToon/blob/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/Editor/lilToonSetting.cs) can regenerate or import shader settings and generated source, based on those materials, clips, packages, and integrations. Postprocess restores and rebuilds settings later.

The `isOptimizeInNDMF` check only changes Apply-on-Play behavior detected from the caller stack. It does not move the actual upload callback before NDMF `Optimizing`.

This is decisive: the final build-time generated lilToon source does not exist at any NDMF phase. AMUSE cannot claim positive attestation of that source from an NDMF-only snapshot.

## Mutation/lifecycle matrix

"Earliest" and "latest" below refer to the representative pinned systems, not to every possible Unity extension.

| Evidence | Representative mutator | Earliest observed mutation | Latest observed mutation | Can AMUSE order against it? | Snapshot needed? | Invalidates |
|---|---|---|---|---|---|---|
| Mesh topology/UVs/indices | Modular Avatar, TexTransTool, Avatar Optimizer, VRCFury | `Resolving`/`Transforming` | NDMF `Optimizing` | Yes for named NDMF plugins. VRCFury is before NDMF `Optimizing` by SDK callback order | Copy vertices, UVs, topology, indices and slot mapping | Geometry classification, submesh plan, mutation target assumptions |
| Renderer/material assignment | Modular Avatar, TexTransTool, Avatar Optimizer, VRCFury | `Transforming` or VRCFury `-10000` | NDMF `Optimizing` | Yes under the same supported-order policy | Copy target identity, slot sequence, material identities. Retain renderer only as target | Effective material set, renderer analysis and plan |
| Material properties | TexTransTool, Avatar Optimizer, VRCFury, lilToon setup | `Transforming` | lilToon SDK callback `100` | Only for NDMF-visible state. LilToon's later write requires a proven handoff or refusal | Copy every property used by semantics | Material semantics and every dependent proof |
| Shader assignment | TexTransTool, Avatar Optimizer, VRCFury SPS, lilToon multi-material/setup | `Transforming` | lilToon callback `100` | Not universally from NDMF | Copy shader identity/path/GUID and relevant schema | Attestation, semantics, material target validity |
| Generated shader source | VRCFury SPS, lilToon generation/regeneration | VRCFury callback `-10000` | lilToon preprocess `100`. Postprocess later restores global source | VRCFury yes from NDMF. LilToon no from NDMF | Read exact content once. Capture canonical content/hash and closure | Shader attestation and all derived semantics |
| Texture pixels/importer/sampling | TexTransTool, Avatar Optimizer, Unity importer/environment | `Transforming` | NDMF `Optimizing` for supported build plugins. Environmental reimport can occur outside graph | Supported plugin writes yes. Environmental race only fail-closed | Eagerly copy source ID, sampling, importer facts, dimensions/format, required pixels | Texture evidence, material semantics, triangle proof |
| Package/external shader source | Package manager, source packages, lilToon regeneration | Before build. Package/environment changes are outside NDMF | Could change while the Editor is open. Activation consumed at lilToon callback `100` | No plugin edge for Package Manager/external process | Capture identity plus exact bytes/hashes/ordered closure in one extraction operation | Integration tuple, attestation, semantic result |
| Animation-reachable material state | Modular Avatar, Avatar Optimizer, VRCFury, lilToon reads clips | `Resolving`/`Transforming` | NDMF `Optimizing` for controller mutators. LilToon consumes at `100` | Supported NDMF/VRCFury results visible in `Optimizing`. Cannot attest lilToon's subsequent generation there | Copy controllers/clips/bindings/reachable values needed by proof | Effective-state set, renderer/global plan |

No candidate boundary appears after the final row for all evidence: NDMF `Optimizing` is late enough for the representative avatar mutators, while lilToon source generation runs later than all NDMF phases.

## Candidate execution architectures

### A. Single late AMUSE pass

One explicitly ordered `Optimizing` pass performs extraction, semantics, proof, planning, and mutation.

This is sound and simple for the state visible at NDMF `Optimizing`. It is the selected core architecture. The phase permits optimization mutations, earlier phases and VRCFury stay visible, and named same-phase mutators can be explicit predecessors.

It does not solve later SDK callbacks. AMUSE must also run after every supported same-lifecycle mutator that can invalidate a proof dependency, including relevant `PlatformFinish` writers, unless a specific characterization proves that mutator preserves the transformation's required invariants. It is not sound merely to apply AMUSE before a later mutator. Unknown or uncharacterized downstream NDMF plugins and SDK callbacks therefore force conservative refusal for the affected optimizations.

### B. Snapshot pass plus later mutation pass

This creates a validity interval that NDMF does not protect. `BuildContext` storage preserves bytes, not target state. Any intervening pass can replace meshes, materials, or controllers, or invalidate source evidence. Live Unity references make it worse.

This model is justified only when a required host API forces the split. It would then need dependency-specific fingerprints and a recheck/recompute step immediately before mutation. No current AMUSE need justifies that complexity inside NDMF.

### C. Late snapshot plus immediate planning/mutation

This is a refined A, and the selected model:

1. enter one explicitly ordered late `Optimizing` pass.
2. eagerly capture all facts the requested proof needs.
3. use immutable values only for interpretation, proof, and planning.
4. retain separate mutation-target handles.
5. validate only the target liveness/identity needed to apply the plan.
6. mutate before the pass returns.

Supported plugins cannot interleave during the pass, so no general revalidation or transaction is needed. Existing pure layers can stay unchanged. Host frontends need adaptation to accept captured values.

### D. Revalidation model

Revalidation can make an earlier snapshot safe only when it checks every dependency that can affect the proof, and recomputes or refuses when any fingerprint differs. It may be necessary across an SDK callback boundary, but it is not a cheap hash of the final plan: material values, reachable states, mesh/slot topology, texture/importer bytes, source closure, package identities, and activation evidence all participate.

Inside one NDMF pass it adds no useful interoperability. Across lilToon's callback it may help, but a late AMUSE callback must first prove it can mutate the build safely after NDMF has finalized generated assets, and after every other supported callback that could invalidate its proof, except a specifically characterized later invariant-preserving consumer. That host responsibility stays unresolved here.

### E. Cross-callback handoff candidates

The next investigation must compare two possible handoffs and retain a third, negative outcome. This document selects none of them.

**Candidate A — late attestation plus late mutation.** A late `Optimizing` pass may perform only justified preparation before NDMF `Finish`. After the lilToon callback `100` generates the authoritative build source, a later AMUSE SDK callback would re-extract final source/material state, attest it, perform any required proof/planning, create or replace assets, and mutate the avatar. This candidate must prove final lilToon visibility. Safe mutation and serialization after `Finish`. Reliable target identity. Explicit order after lilToon and every other supported mutator that could invalidate the proof, except a specifically characterized later invariant-preserving consumer. And the absence of a regeneration cycle caused by AMUSE changing inputs lilToon used.

**Candidate B — conditional NDMF mutation plus late validation gate.** AMUSE would capture coherent NDMF-visible state and conditionally transform during its late `Optimizing` pass, retaining only bounded immutable handoff evidence. After lilToon generation, a later AMUSE SDK callback would validate the future condition and either allow the build or abort it. The gate must validate every dependency that may have changed across the boundary, not only a final shader hash or integration flag: renderer/material assignment, shader assignment, relevant material properties, animation-reachable state, mesh/slot identity when mutable, generated source, package/external closure, and activation evidence. It must also prove that a callback failure prevents serialization, upload, or use of the invalid transformed build, for both an upload/build path and an Apply-on-Play or equivalent development path.

**Candidate C — neither.** If late mutation cannot be made safe, and complete late validation cannot be proven, AMUSE keeps the sound NDMF-only architecture and keeps positive build-time lilToon integrated attestation unsupported.

The probe must separately characterize the VRChat upload/build path and the Apply-on-Play or equivalent NDMF development path. It must not assume their callback behavior, serialization, or failure semantics are identical.

## Selected architecture

The authoritative **NDMF analysis point** is one AMUSE pass in `BuildPhase.Optimizing`, explicitly `AfterPlugin` every supported NDMF plugin that can alter proof dependencies in that phase. It runs naturally after all earlier NDMF phases, and, in the pinned VRChat lifecycle, after VRCFury's `-10000` callback.

Within that pass, AMUSE performs late eager extraction, followed immediately by pure semantics, proof, planning, and mutation. It does not split the authoritative snapshot and mutation across passes, unless a future proven host constraint requires it.

This design is authoritative only for the declared supported integration set, and only when AMUSE runs after every supported mutator that could invalidate the proof, except a later mutator specifically characterized as preserving the required invariants. Nobody should describe it as globally final relative to arbitrary `PlatformFinish` writers or later SDK callbacks. Positive official lilToon integrated attestation stays disabled until the separate callback-handoff question is resolved.

One dedicated future NDMF plugin with one principal pass is enough for this core. Multiple passes are not justified for correctness, and they would create an invalidation interval.

## Definition of an AMUSE analysis snapshot

An AMUSE analysis snapshot is:

> The immutable set of proof-relevant facts eagerly captured at one justified host lifecycle point and consumed, without live host re-reads, by one semantic proof and its plan.

It is a contract, not necessarily one giant object. Records should follow actual proof inputs, rather than introduce a generic framework. Likely bounded records include:

| Domain | Minimum captured form |
|---|---|
| Renderer | Stable diagnostic ID/path, renderer kind, property-block refusal bit, ordered material-slot identities, mesh identity, and only the renderer facts the proof uses |
| Mesh | Copied topology, indices, positions, required UV channels/vertex attributes, submesh-to-slot mapping, and a target identity guard |
| Material | Shader identity, every property/texture/transform/keyword/tag actually used by the supported frontend, and color-space input |
| Texture | Source ID, sampling, importer interpretation, dimensions/format, and eagerly copied required pixel/channel data |
| Shader source | Asset/package identity, exact captured generated source, canonical form/hash, ordered include graph, and macro provenance |
| External dependency | Exact package identity/version and exact captured executable closure plus activation evidence |
| Animation state | Reachable material identities, swaps, relevant property bindings/value domains, and controller/clip identity needed for diagnostics |

The design should reuse existing immutable semantic and planning records. Stable IDs and hashes are useful for diagnostics or a future required recheck, but they do not replace the actual captured values that reasoning needs.

### Host-reference versus proof-data policy

This should become a project-wide invariant:

> A `UnityEngine.Object` reference may be retained as an immediate mutation target. Proof-relevant facts about that object must be captured separately, and must not be silently re-read during semantics, proof, or planning.

Target handles live outside the immutable proof model. Immediately before applying a plan, the host adapter may check that the target still exists and has the expected identity/slot shape needed to perform the write. That is an application guard, not an invitation to recompute semantics from live state. If the guard fails, refuse. Never apply the old plan to a replacement.

Filesystem paths, GUIDs, instance IDs, and object references are identities and locations. They are not proof that content is unchanged.

## Validity and invalidation

### Core invariant

AMUSE may authorize a transformation only from evidence captured after every **supported upstream or same-lifecycle mutator** that could invalidate a proof dependency. A later mutator is compatible only when a specific characterization shows it preserves every invariant the transformation needs. It is not enough to apply AMUSE first and let an uncharacterized tool mutate the result later.

If AMUSE cannot establish that order or preservation contract, it must refuse the affected optimization. This rule applies uniformly to same-phase NDMF plugins, `PlatformFinish` writers, SDK callbacks after NDMF, and material/shader, mesh-topology, animation/controller, texture, or importer mutators. Unsupported or unknown downstream semantic mutation cannot make AMUSE more aggressive: uncertain downstream mutation means an unsupported ordering contract, and therefore `Unknown`/no optimization. This rule needs only a small, explicitly reviewed, supported-tool surface, not a generic plugin registry or invalidation framework.

### Dependency-scoped invalidation

- A material property, texture assignment, shader assignment, keyword/tag, color-space input, or attested-source change invalidates that material's semantics.
- A texture pixel/importer/sampling/source-ID change invalidates that texture evidence and the dependent material/triangle results.
- A mesh topology/index/vertex/UV/submesh change invalidates geometry classification and the renderer separation plan.
- A renderer mesh/slot/property-block change invalidates the renderer effective state and plan.
- A shader/package/include/external-closure/activation change invalidates shader attestation and all semantics derived from it.
- An animator/controller/clip/binding/material-swap change invalidates the reachable-state set and the dependent renderer/global plan.
- Any invalidated leaf invalidates every proof and plan that consumed it. Unrelated snapshots need not be invalidated.

No incremental dependency engine is needed at the start. A renderer/material-scoped extraction can be discarded and recomputed as a unit. Correctly ordered single-pass execution normally prevents invalidation, rather than handling it dynamically.

### Response to invalidation

- **Before reasoning:** fail extraction, or restart the bounded extraction, if the inconsistency traces to an ordinary import refresh.
- **During same-pass reasoning/planning:** no supported plugin can interleave. An observed target failure causes refusal.
- **Between passes/callbacks:** re-extract and recompute, or perform complete dependency revalidation followed by refusal or recompute. Never use a stale proof.
- **Known later mutator:** order AMUSE after it, or specifically prove that it preserves all required transformation invariants. Otherwise refuse the affected transformation.
- **Unknown downstream plugin or callback:** do not claim support for the affected optimization. An attempt to predict arbitrary mutation is unnecessary and unsound.

## Filesystem and package TOCTOU boundary

AMUSE's threat model distinguishes:

1. **Supported pipeline mutation.** Ordering must prevent this, or re-extraction/recompute must handle it.
2. **Ordinary environment activity.** Package resolution, importer refresh, or source regeneration may occur while the Editor is active. Extraction should read each required file once into memory, construct the include/closure graph from those captured bytes, capture package identities and activation facts in the same bounded operation, and fail closed on missing, changing, or inconsistent inputs. Downstream reasoning uses captured bytes/hashes only.
3. **Malicious external rewriting.** An adversarial process that races individual reads sits outside the supported threat model. AMUSE does not need transactional filesystem snapshots, locks, MVCC, or a generalized file watcher.

For ordinary operation, one eager extraction that fingerprints and retains all required file contents is enough. A hash proves which bytes AMUSE used. It does not prove that a later external consumer used the same bytes. So build-time lilToon support still needs the correct late lifecycle point, or a revalidation/handoff contract.

## Ordering policy for supported integrations

The future AMUSE integration should maintain a small, reviewed set of explicit constraints associated with actually supported tools/versions:

- `AfterPlugin("net.rs64.tex-trans-tool")` in `Optimizing` when TexTransTool is supported.
- `AfterPlugin("com.anatawa12.avatar-optimizer")` in `Optimizing` when Avatar Optimizer is supported.
- `AfterPlugin("nadena.dev.modular-avatar")` for its optimizing work when Modular Avatar is supported.
- rely on the pinned SDK callback relationship for the examined VRCFury shape, and version-gate the conclusion instead of inventing an NDMF plugin edge.
- do not claim final lilToon generated-source visibility from an NDMF pass.

The exact names here are architectural evidence, not an instruction to hard-code a generic registry on this branch. The implementation milestone should add only the constraints its shipped support surface actually requires.

AMUSE must normally run after every supported same-lifecycle proof-dependency mutator, including a relevant `PlatformFinish` writer. A later consumer may stay after AMUSE only when its exact supported behavior is characterized as preserving the transformation's required invariants. A `BeforePlugin` edge alone does not establish that preservation. Running merely "late in Optimizing," relying on discovery order, or mutating before an uncharacterized later tool is not enough.

## Responsibilities of the future AMUSE NDMF integration

The future host integration should:

- select the authoritative NDMF extraction point.
- declare explicit supported ordering constraints.
- eagerly construct proof inputs and separate target handles.
- invoke host-agnostic semantic interpretation, proof, and planning.
- apply mutations immediately.
- refuse on extraction or target-identity failure.
- emit diagnostics that name the unsupported/inconsistent evidence.

It should not move Unity/NDMF dependencies into analysis types, expose live objects to semantic reasoning, create a general event bus, or attempt to supervise arbitrary plugins. `BuildContext.GetState<T>` may carry diagnostics or non-authoritative coordination data, but the selected single-pass design does not need it for proof validity.

## Application to official lilToon integrations

The desired future positive attestation tuple is still correct:

1. reach an authoritative extraction point.
2. capture final material/shader assignment and all relevant material values.
3. capture generated lilToon source and pass source.
4. capture exact lilToon and external package identities.
5. capture the exact active LTCGI, AudioLink, or VRC Light Volumes executable closure.
6. capture Layer-2 activation evidence, include order, and macro provenance.
7. freeze those facts.
8. validate the official integration tuple.
9. derive `MaterialSemantics` from captured material values only.
10. prove, plan, and mutate only after every supported invalidating mutator in the selected lifecycle, with any later consumer specifically characterized as preserving the required invariants.

The snapshot architecture resolves the **coherence** half of the earlier blocker: generated source, package identity, external closure, activation, material values, and semantics can be one immutable proof input.

It does **not** resolve the **lifecycle** half for uploads. lilToon creates the relevant build-optimized source at callback order `100`, after NDMF's final callback at `-1025`. An NDMF-only implementation would attest pre-build source, and it could not truthfully claim it proved the exact build state.

So positive LTCGI, AudioLink package, and external VRC Light Volumes support stays blocked. The missing result is either a source-backed and empirically verified host shape that can observe lilToon's completed generation and safely apply/serialize AMUSE mutation, or a complete conditional-mutation validation gate whose failure reliably prevents an invalid build from being emitted or used. An upstream integration contract that moves or fixes generation before the AMUSE NDMF boundary could also close the gap. Until one of those results is proven, integrated cases must stay unknown/preserved.

## Project-wide consequences

- **Renderer alpha analysis:** the pure classifier/planner layers are already suitable. Renderer/mesh/material extraction must become eager and target-separated.
- **Texture evidence:** eager pixel/importer capture removes the current lazy `Texture2D` dependency and makes material/geometry evidence coherent.
- **Poiyomi:** the same captured-material/source boundary applies. This branch does not change its semantics or support surface.
- **Animation/material state:** the future reachability extractor must run after controller-generating tools and produce immutable reachable states before proof.
- **Future shader families:** each frontend consumes captured shader/material/source facts, with no family-specific live reads during interpretation.
- **Census:** measurement can use the same immutable extraction records for reproducibility, without making census orchestration part of production proof.
- **Feature portability:** normalized semantic facts stay host-agnostic, and become easier to compare because their provenance is an explicit snapshot.

This is a project-wide boundary, not a lilToon workaround. lilToon merely shows why snapshot coherence cannot substitute for lifecycle placement.

## Production migration scope

The evidence selects **C. Broader extraction migration required**, but "broader" stays bounded to host-facing code:

1. introduce the smallest proof-specific immutable input records the existing renderer alpha vertical slice needs.
2. eagerly capture texture bytes/importer/sampling facts instead of retaining `Texture2D` for lazy reads.
3. split Poiyomi/lilToon material extraction from interpretation, so interpretation accepts captured values.
4. split renderer/mesh extraction from pure analysis/planning, and keep a separate immediate mutation target.
5. add one late ordered NDMF pass only after these seams can support it.
6. keep positive build-time lilToon integration disabled pending the callback investigation.

This does not justify a generic snapshot manager, event system, dependency graph, plugin registry, transactional filesystem, or multi-pass cache.

No implementation plan comes from this gate. The lifecycle gap and design review must close first.

## Remaining unknowns

1. Does a VRChat SDK preprocessor after lilToon callback `100` observe the exact generated shader/material state produced for that build?
2. Can it safely create, replace, and serialize AMUSE meshes/materials after NDMF `Finish`?
3. Can a conditional NDMF transformation retain bounded immutable evidence and later validate every cross-boundary proof dependency, rather than only a source hash or activation state?
4. Does failure from that late validation callback reliably prevent serialization, upload, or use of an invalid conditionally transformed avatar?
5. Can an AMUSE callback be ordered reliably after lilToon and after every other supported mutator that could invalidate its result, without reliance on callback discovery order?
6. Would either handoff change the material, assignment, animation, or other state lilToon used for generation, and therefore require another generation pass?
7. How do the upload/build path and the Apply-on-Play or equivalent NDMF development path differ in visibility, ordering, mutation, serialization, and failure behavior?
8. What exact target identity/handoff survives from an NDMF pass into a later SDK callback, without retaining stale Unity references?
9. Does the NDMF 1.14.4 `AfterPass` edge-direction discrepancy have an upstream clarification or fix relevant to AMUSE's supported range? The selected design does not depend on it.
10. Which exact versions of representative integrations will AMUSE initially promise to order against? The pinned revisions here establish mechanisms, not a final compatibility matrix.

## Recommended next branch

After design review, use a focused investigation branch such as:

`investigate/liltoon-build-callback-handoff`

Its only purpose should be a synthetic, disposable comparison of three outcomes around NDMF `-1025`, lilToon `100`, and a candidate later AMUSE SDK hook:

1. **Late attestation plus late mutation:** prove final lilToon visibility, safe post-`Finish` mutation/asset serialization, reliable targets and ordering, and no lilToon regeneration feedback cycle.
2. **Conditional NDMF mutation plus late validation/build-abort gate:** prove bounded immutable handoff, complete validation of every cross-boundary proof dependency, and failure semantics that prevent an invalid transformed build from being serialized, uploaded, or used.
3. **Neither:** retain the NDMF-only architecture, and refuse positive build-time lilToon attestation if either positive model lacks a complete safety proof.

The probe must explicitly compare upload/build with Apply-on-Play, and must not infer callback order from discovery order. It should not implement official integration semantics.

Only after that result should the design propose a separate production plan for the immutable extraction migration and NDMF integration.

## Review gate

Review should confirm or reject these claims before production work:

- `Optimizing` plus explicit same-phase predecessor edges is the authoritative NDMF-visible boundary.
- one-pass late snapshot plus immediate mutation is sufficient for that boundary.
- AMUSE runs after every supported proof-dependency mutator, unless a specifically characterized later mutator preserves all required invariants.
- proof data contains no live Unity references.
- target references are separate, immediate, and fail closed on mismatch.
- ordinary filesystem races need eager captured bytes and refusal, not transactions.
- positive official lilToon integration stays blocked by callback placement, not by snapshot design.
- the next work compares late mutation, conditional NDMF mutation plus a complete late validation/build-abort gate, and neither. It is not implementation of AMUSE infrastructure or lilToon support.
