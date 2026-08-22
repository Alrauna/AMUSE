# General-Purpose Transformation Boundaries Audit

## Status and scope

**Preliminary conclusions approved in chat; written audit awaiting review.**

- Branch: `audit/general-purpose-transformation-boundaries`
- Base: `origin/main` at `1fa0356841f00d2965422782f8cf18f3fc1c39ab`
- Base result: merge of PR #12, `design/upload-conditional-authorization`
- Intended repository change: this audit document only
- Production source, research source, tests, assets, package state, CI, and prior design records: unchanged
- Unity, Census Lab, private avatars, and external private fixtures: not used

This is a retrospective source-and-design audit. It classifies existing responsibilities and records constraints for later architecture and implementation planning. It does not correct source, introduce an abstraction, or plan an implementation.

## Audit question

> Where has AMUSE's current alpha-material optimization purpose legitimately produced purpose-specific code, and where has that purpose accidentally leaked into shared architectural boundaries that future AMUSE transformations would need to reuse?

## Binding acceptance criteria

The merged upload-conditional design supplies both binding criteria:

> No shared production boundary may unnecessarily assume that an AMUSE transformation is a single-renderer alpha split. The architecture must permit future proof-backed transformations spanning multiple renderers, meshes, UV sets, materials, generated textures, and material-slot topology without implementing those transformations prematurely.

> Purpose-specific algorithms should remain purpose-specific when their narrow purpose is real. Generalize shared boundaries, not every algorithm.

Both criteria are enforced here. A narrow alpha name, one-renderer input, one-mesh plan, UV0 dependency, or opaque/transparent result is not a defect when it is intrinsic to a contained alpha responsibility. Conversely, a boundary is not considered reusable merely because alpha separation is its only current consumer.

## Non-goals

This audit does not:

- modify, refactor, or test production or research code;
- create an implementation plan;
- start the SDK build-environment investigation;
- design or implement atlasing, material combining, UV repacking, control textures, shader portability, mutation, or generated assets;
- create a generic transformation, snapshot, identity, asset, provider, registry, sequencing, or shader-IR framework;
- require every existing API to support future transformation cardinalities;
- resolve external host lifecycle behavior.

## Methodology

The audit used static inspection of:

- `AGENTS.md`;
- `docs/architecture/vision.md`;
- `docs/architecture/shader-frontend-comparison.md`;
- all production source under `Packages/com.alrauna.amuse/Editor/`;
- relevant production tests under `Packages/com.alrauna.amuse/Tests/Editor/`;
- research collection boundaries under `Packages/com.alrauna.amuse.research/Editor/Collection/` and their tests;
- completed design records for geometry classification, separation planning, material semantics, texture alpha evidence, renderer alpha analysis, snapshot ordering, source attestation, and upload-conditional authorization;
- recent Git history establishing why the main boundaries were introduced.

The inspection proceeded responsibility-first: determine what a component owns, then judge whether its cardinality or vocabulary is intrinsic to that responsibility. The explicit candidates from the merged design were followed by a repository-wide search for hidden one-renderer, one-mesh, alpha-only, UV0-only, fixed-slot, identity, policy-in-semantics, planning/mutation, and framework-overreach assumptions. Finally, six hypothetical transformations were traced until the first existing boundary that would need new work.

## Evidence standards

- Source and tests take precedence over names and speculative intent.
- Current architecture documents clarify intended ownership but cannot override contradictory source.
- Historical specs are evidence of the responsibility a boundary was designed to own, not a permanent mandate.
- A future capability's absence is not a defect unless an existing shared boundary would obstruct or distort it.
- A Category-C finding requires a concrete shared-boundary obstruction, not merely foreseeable pressure.
- Where no production boundary exists yet, this audit says so rather than treating the alpha facade as its substitute.

## Architecture responsibility map

| Responsibility | Current component(s) | Role classification | Observed scope |
|---|---|---|---|
| Unity renderer, mesh, material, and geometry reads | `UnityRendererAlphaAnalysis` | Host integration mixed with purpose-specific orchestration | One supported renderer and its one mesh |
| Generic Unity texture/importer facts | `UnityTextureEvidence` | Shared host evidence | Five proven semantic facts |
| Alpha pixel evidence | `UnityAlphaFieldEvidence` | Purpose-specific host evidence | Predicate-equivalent alpha field |
| Source/package capture | `PoiyomiSourceEvidence`, `LilToonSourceEvidence`, `LilToonIncludeTree` | Shader-specific captured evidence | Pinned shader-family identity and closure |
| Immutable semantic facts | `MaterialSemantics`, `SemanticOutput<T>`, semantic value and texture records | Reusable domain representation | Four deliberately small output facts |
| Shader semantic interpretation | Poiyomi and lilToon material frontends | Shader-specific semantic producers | Attested base-material state |
| Alpha semantic proof | `AlphaSemanticsResolver`, `AlphaResolution` | Purpose-specific proof | Normalized alpha to exact classifier input |
| Exact geometry and alpha classification | `ExactUvGeometry`, `TriangleAlphaClassifier` | Purpose-specific proof | One triangle, exact supported UV/sample domain |
| Mesh separation planning | `MeshSeparationInput`, `MeshSeparationPlan`, `MeshSeparationPlanner` | Purpose-specific planning | One mesh, opaque-candidate versus preserved membership |
| Renderer-alpha result and diagnostics | `RendererAlphaAnalysis`, `SubmeshAlphaAnalysis`, refusal enums | Purpose-specific facade/result | One renderer's current alpha analysis |
| Renderer discovery | `AvatarCensusCollector` only | Research integration | Caller-supplied avatar-root census |
| Research observation | `RendererObservationBuilder`, census vocabulary and records | Research/census-specific | Alpha coverage measurement |
| Product-wide renderer discovery and orchestration | Not implemented | Domain absent | No production all-avatar optimizer entry point |
| Transformation preparation and mutation | Not implemented | Domain absent | Defined architecturally, not in source |
| Expected/final canonical state and relationship composition | Not implemented | Prospective shared architecture | Defined by the merged design |
| Generated-output identity | Not implemented | Prospective transformation/shared seam | Defined as deterministic logical identity |

The map shows that the current product contains pure alpha algorithms, reusable semantic facts, shader-specific producers, and one host-facing alpha facade. It does not contain a general transformation pipeline that could already be fixed to alpha cardinality.

## Classification definitions

### A — Correctly purpose-specific

The narrow behavior is the component's legitimate algorithmic purpose. Leave it alone.

### B — Purpose-specific implementation behind a sufficient shared boundary

The implementation is narrow, but its narrowness does not constrain reusable AMUSE architecture. No current architectural correction is required.

### C — Accidental architectural fixation

A shared or reusable boundary unnecessarily embeds an alpha-optimizer assumption and would obstruct foreseeable transformations. It must be corrected at an assigned C0-C4 horizon before affected work builds on it.

### D — Premature abstraction

An existing abstraction is more generic or extensible than demonstrated use justifies. Record it without simplifying it on this branch.

## Executive summary

The audit records **12 material findings: 7 A, 5 B, 0 C, and 0 D**.

AMUSE's current narrowness is overwhelmingly legitimate. Exact triangle alpha proof, alpha evidence, opaque/preserved mesh planning, renderer-alpha orchestration, shader-family attestation, and census observation all have real purpose-specific responsibilities. Tests reinforce those responsibilities without promoting them into universal contracts.

The reusable semantic boundary is also healthier than its first consumer might suggest. `MaterialSemantics` records behavioral facts rather than optimizer policy; `UvMapping` supports indexed UV sets rather than UV0 alone; `TextureSourceId` is opaque rather than Unity-object-bound; and the separation planner treats material binding indices as uninterpreted provenance rather than inferring Unity slot identity.

No current shared production boundary requires a future optimizer to enter through `UnityRendererAlphaAnalysis`, consume `MeshSeparationPlan`, split geometry, operate on one renderer, or produce opaque/transparent outputs. Product-wide discovery, transformation preparation, mutation, expected-state composition, generated-output identity, and general orchestration do not yet exist. Their absence is future development pressure, not current Category-C fixation.

The merged upload-conditional architecture therefore stands unchanged. Its already-approved coherent-extraction migration remains necessary, but that is a snapshot-purity and lifecycle requirement, not evidence that a shared boundary has been accidentally fixed to alpha.

## Complete finding table

| ID | Classification | Area | Finding | Consequence | Timing |
|---|---|---|---|---|---|
| F1 | A | Exact alpha proof | Exact UV and triangle classification legitimately model one alpha predicate over one triangle and UV0. | Leave unchanged. | — |
| F2 | A | Alpha semantic resolution | Alpha resolution legitimately accepts only semantic forms and mappings the exact classifier can prove. | Leave narrow. | — |
| F3 | A | Alpha texture evidence | Predicate-equivalent alpha bytes are a legitimate narrow evidence product. | Eagerly capture later; do not generalize content. | — |
| F4 | A | Separation planning | One-mesh opaque/preserved membership is the real purpose of the separation plan. | Keep purpose-specific; no universal plan base. | — |
| F5 | A | Renderer-alpha facade | One-renderer scope and conservative slot/topology refusal are contained alpha-facade behavior. | Preserve facade if useful; separate shared extraction beneath it. | — |
| F6 | A | Shader frontends and attestation | Shader equations, gates, diagnostics, and attestation shapes are genuinely family-specific. | Keep separate. | — |
| F7 | A | Research census | Renderer-level alpha coupling serves an explicitly alpha census and is not product orchestration. | Leave purpose-specific. | — |
| F8 | B | Material semantic vocabulary | Shared semantics record facts, not optimization decisions or complete host validation state. | Retain the small vocabulary. | — |
| F9 | B | UV and texture semantic records | Shared records support indexed UV mappings and shader-independent sampling; UV0 is confined downstream. | No correction required. | — |
| F10 | B | Binding provenance | The planner preserves opaque caller-supplied binding indices; Unity's one-to-one slot rule is outside it. | Do not copy facade restrictions into shared extraction. | — |
| F11 | B | Identity and addressing | Current texture source identity is opaque; host asset identity, diagnostics, semantic addresses, and logical outputs remain distinct roles. | Preserve those distinctions. | — |
| F12 | B | Multi-output preparation seam | No existing shared preparation API forces alpha cardinality; the merged design makes preparation transformation-specific. | Implement the approved seam later without universalizing the alpha plan. | — |

## Detailed findings

### F1 — Exact UV and triangle alpha proof is correctly purpose-specific

- **Classification:** A
- **Files:** `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`; `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs`
- **Types/APIs:** `TriangleAlphaOutcome`, `TriangleAlphaInput`, `AlphaTextureData`, `AlphaSamplingSettings`, `TriangleAlphaClassifier.Classify`, `ExactUvGeometry`
- **Current responsibility:** Prove whether one triangle is wholly opaque, must remain transparent, or is unknown under an exact bounded sampling model.
- **Assumption evaluated:** One triangle, one supplied UV set named UV0, one scalar alpha field, and alpha-specific Point/Bilinear plus Clamp/Repeat semantics.
- **Evidence:** `TriangleAlphaInput` contains three positions and three UV values plus `HasUv0`; `TriangleAlphaClassifier.Classify` returns `Unknown` when required geometry or UV evidence is unavailable and partitions only the exact supported sample cases. The geometry helper exists solely to implement those exact support regions. The architecture vision identifies this as the current alpha subsystem, and classifier tests exercise exact alpha behavior rather than generic mesh transformation.
- **Architectural significance:** These restrictions define the theorem being proved. Generalizing the input to all vertex attributes or texture meanings would obscure the proof and would not create a reusable transformation seam.
- **Affected shared boundary:** None. The types are internal alpha-analysis inputs.
- **Future pressure:** UV repacking or other geometry analyzers require their own geometry representations; they do not pass through this classifier.
- **Consequence:** Leave the classifier and exact geometry narrow.
- **Confidence:** High.

### F2 — Alpha semantic resolution is correctly purpose-specific

- **Classification:** A
- **File:** `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`
- **Types/APIs:** `AlphaFieldProvider`, `AlphaResolution`, `AlphaSemanticsResolver.Resolve`
- **Current responsibility:** Bridge normalized scalar alpha semantics to the existing exact classifier without reading host state.
- **Assumption evaluated:** Only identity mapping on UV set 0 and the classifier's closed sampling vocabulary can enter the classified path.
- **Evidence:** `IsSupportedMapping` explicitly accepts channel 0, unit scale, and zero offset because the classifier has no transform or UV-channel input. Unsupported mappings return `UnsupportedUvMapping`; new semantic value kinds fail closed. Tests cover unsupported channels, transforms, sampling, multipliers, and missing evidence.
- **Architectural significance:** The resolver is an adapter into a narrower proof engine, not the shared UV model. Its refusal is proof containment, not a claim that UV0 is AMUSE's universal UV domain.
- **Affected shared boundary:** It consumes, but does not narrow, `MaterialSemantics` records.
- **Future transformations:** Atlas and UV-only transformations bypass this resolver.
- **Consequence:** Leave it purpose-specific and exhaustive.
- **Confidence:** High.

### F3 — Alpha field production is narrow by purpose, while live retention is an orthogonal migration issue

- **Classification:** A
- **File:** `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs`
- **Types/APIs:** `UnityAlphaFieldEvidence`, `TryGetAlphaField`
- **Current responsibility:** Resolve semantic texture source IDs to predicate-equivalent alpha bytes for `AlphaFieldProvider`.
- **Assumption evaluated:** Texture content is represented only as the alpha-equality predicate required by the classifier.
- **Evidence:** The class documents that byte 255 means exactly sampled alpha one and refuses other channels, formats, mip behavior, unreadable textures, and unsupported objects. It lives in `Host` and feeds only the alpha resolver. The merged upload-conditional design explicitly permits eager alpha bytes to remain purpose-specific.
- **Architectural significance:** An atlas cannot reuse this lossy predicate field as general texture content, but nothing presents it as general texture extraction. The retained `Dictionary<TextureSourceId, Texture2D>` does violate the future no-live-proof-read policy; PR #12 already requires eager capture.
- **Affected shared boundary:** No alpha-fixation correction. The future coherent extraction boundary must replace delayed live reads, not broaden this evidence into universal pixels.
- **Future transformations:** General texture transformations need separate content domains when concrete algorithms require them.
- **Consequence:** Eagerly capture this narrow evidence during the approved extraction migration; do not generalize it.
- **Confidence:** High.

### F4 — Mesh separation planning is correctly purpose-specific

- **Classification:** A
- **File:** `Packages/com.alrauna.amuse/Editor/Analysis/MeshSeparationPlanner.cs`
- **Types/APIs:** `MeshSeparationInput`, `SubmeshSeparationInput`, `MeshSeparationPlan`, `SubmeshSeparationPlan`, `MeshSeparationPlanner.Create`
- **Current responsibility:** Convert completed triangle-alpha outcomes for one mesh into immutable opaque-candidate and preserved memberships.
- **Assumption evaluated:** One input mesh, per-submesh triangle groups, and opaque-versus-preserved membership that can imply zero, one, or two logical sides per source group.
- **Evidence:** The implementation contains only alpha outcomes, source topology, binding provenance, ordinals, counts, and split dispositions. It performs no host lookup or mutation. `MeshSeparationPlannerTests` verify conservative membership, arbitrary bindings, repeated bindings, immutable inputs, deterministic order, and no policy/profitability decisions. The original design calls the plan one candidate-analysis result, and PR #12 explicitly says it is not AMUSE's general transformation model.
- **Architectural significance:** One-mesh and opaque/preserved cardinality are the algorithm's actual output. Making it support generated materials, textures, renderers, or arbitrary operations would turn a proof result into a speculative framework.
- **Affected shared boundary:** None. Transformation-specific preparation will consume this plan without promoting it to a universal plan.
- **Future transformations:** Atlas, merge, UV, and shader-feature plans will be separate concrete domains.
- **Consequence:** Leave unchanged; do not add a shared transformation base or host postconditions.
- **Confidence:** High.

### F5 — One-renderer alpha analysis is an acceptable facade

- **Classification:** A
- **File:** `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`
- **Types/APIs:** `UnityRendererAlphaAnalysis.Analyze`, `RendererAlphaAnalysis`, `SubmeshAlphaAnalysis`, `RendererAnalysisRefusal`
- **Current responsibility:** Read one supported Unity renderer's current base state, invoke semantic and alpha proof, and return one immutable alpha-separation result.
- **Assumption evaluated:** One renderer contributes one mesh; supported material count equals submesh count; every submesh is triangles; material slot index equals submesh index in the admitted Unity layout.
- **Evidence:** The class summary explicitly says it drives the alpha components over one renderer. It refuses unsupported renderer types, property blocks, absent mesh, unequal material/submesh counts, and non-triangle topology. Tests name and enforce those exact conservative facade contracts. The pure planner beneath it accepts arbitrary source binding indices, proving the equality is not embedded in planning.
- **Architectural significance:** A renderer is a legitimate unit for this alpha facade because it binds the mesh and materials being analyzed. The class currently mixes live extraction with reasoning, which PR #12 requires separating for coherent snapshots. That separation does not require widening the facade into a build-wide optimizer.
- **Affected shared boundary:** The forthcoming host extraction records beneath the facade, not the facade's result type.
- **Future transformations:** Multi-renderer operations use product-wide discovery and their own proof/preparation paths; they need not call this facade.
- **Consequence:** The facade may remain. Later extraction work must not treat its admitted slot layout, UV0 reads, or singular renderer as universal host state.
- **Confidence:** High.

### F6 — Shader equations and source attestation are correctly family-specific

- **Classification:** A
- **Files:** `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`; `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`; `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
- **Types/APIs:** Poiyomi/lilToon semantic result, diagnostic, source-evidence, interpretation, and identity-verification APIs
- **Current responsibility:** Attest a pinned shader implementation and translate its actual equations into shared semantic facts or output-scoped unknowns.
- **Assumption evaluated:** Shader property names, feature gates, source/package identities, canonicalization, include closure, render-mode evidence, and diagnostic vocabularies differ per family.
- **Evidence:** `shader-frontend-comparison.md` documents materially different attestation models, sampler ownership, feature evidence, render-mode evidence, and equations. It also records that no polymorphic adapter call site exists and rejects an `IShaderAdapter`/registry. Source implements independent evidence types and verifiers while both produce the same `MaterialSemantics` values.
- **Architectural significance:** Generalizing irreconcilable shader evidence would erase exact proof conditions. Shared architecture begins at captured host facts and normalized semantics, not a fabricated universal attestation shape.
- **Affected shared boundary:** `MaterialSemantics` is the sufficient common output for current behavior facts. Future-attestation requirements remain concrete shader-family contributions.
- **Future transformations:** Richer feature semantics may consume additional captured facts but do not justify merging attestation models.
- **Consequence:** Keep equations, gates, diagnostics, and attestation separate.
- **Confidence:** High.

### F7 — Census coupling to renderer-alpha analysis is correctly research-specific

- **Classification:** A
- **Files:** `Packages/com.alrauna.amuse.research/Editor/Collection/AvatarCensusCollector.cs`; `Packages/com.alrauna.amuse.research/Editor/Collection/RendererObservationBuilder.cs`; `Packages/com.alrauna.amuse.research/Editor/Collection/CensusVocabulary.cs`
- **Types/APIs:** `AvatarCensusCollector.Collect`, `RendererObservationBuilder.Build`
- **Current responsibility:** Measure alpha-analysis coverage and refusal outcomes over renderers beneath a caller-supplied root.
- **Assumption evaluated:** One observed renderer maps to one alpha analysis and census record.
- **Evidence:** The builder explicitly calls `UnityRendererAlphaAnalysis`, derives counts from the returned plan rather than recomputing proof, and maps alpha-specific enums to census vocabulary. Its comments say the overload is a test seam, not an extension point. Hierarchy paths and asset names are retained as observation context; `AvatarCensusCollector` documents that duplicate paths are accepted because nothing indexes by them.
- **Architectural significance:** Research observation is allowed to match the phenomenon being measured. It neither owns mutation nor defines product-wide transformation cardinality or semantic addressing.
- **Affected shared boundary:** None.
- **Future transformations:** A future census may add other measurements independently; product architecture need not conform to this schema.
- **Consequence:** Leave purpose-specific.
- **Confidence:** High.

### F8 — `MaterialSemantics` is a sufficient small shared vocabulary, not policy or validation state

- **Classification:** B
- **File:** `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs`
- **Types/APIs:** `MaterialSemantics`, `SemanticOutput<T>`, `ColorSemanticValue`, `ScalarSemanticValue`, `NormalSemanticValue`
- **Current responsibility:** Represent complete-or-unknown behavioral facts for base color, alpha, emission, and a narrow normal form.
- **Assumption evaluated:** Four fixed outputs and three closed scalar/color forms are the entire material behavior or complete host validation snapshot.
- **Evidence:** Source contains no optimizer-policy concepts such as candidate, split, atlas, merge, or repack. Tests demonstrate independent partial knowledge, structural equality, tint differences without a baking decision, and UV coupling without an atlas decision. The material-semantics design explicitly calls the vocabulary the smallest current set and rejects an expression DAG or extensible dictionary until real consumers require them. PR #12 explicitly states that `MaterialSemantics` is not complete host validation state.
- **Architectural significance:** The vocabulary can remain deliberately small while richer facts are introduced alongside or beyond it. A complete late snapshot must separately include renderer, mesh, texture, animation, source, package, global, and relationship domains.
- **Affected shared boundary:** Normalized semantic facts only.
- **Future transformations:** Material merge or feature simplification may require additional semantic domains, but they are not forced to encode policy inside this class.
- **Consequence:** Retain the small fact vocabulary; never treat it as universal shader IR or `FreshFinalSnapshot`.
- **Confidence:** High.

### F9 — Shared UV and texture semantic records are not alpha-fixed

- **Classification:** B
- **Files:** `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs`; `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`
- **Types/APIs:** `TextureSourceId`, `UvMapping`, `TextureSampling`, `TextureSample`, `TextureChannel`, `UnityTextureEvidence`
- **Current responsibility:** Describe which source/channel is sampled, through which indexed affine UV mapping and supported sampling behavior, and extract five proven Unity texture facts.
- **Assumption evaluated:** Texture meaning is only alpha, UV meaning is only triangle-alpha sampling, and UV0 is universal.
- **Evidence:** `UvMapping.Channel` accepts every nonnegative index; tests use UV1 and verify channel/transform differences. Color, scalar, and normal values all consume `TextureSample`; `TextureChannel` includes RGBA. Both shader frontends consume all five `UnityTextureEvidence` facts for multiple outputs. Only `AlphaSemanticsResolver` restricts a downstream proof to identity UV0. The reflection guard intentionally prevents unproven evidence growth.
- **Architectural significance:** Shared semantic records describe current supported facts without selecting an optimization. Their closed sampling domain may conservatively return unknown and can be extended only when a real consumer proves another meaning.
- **Affected shared boundary:** Shared semantic facts and bounded Unity evidence.
- **Future transformations:** UV repacking and atlasing will need geometry/content records not present here; those domains can coexist without replacing the alpha classifier.
- **Consequence:** No correction. Do not turn `UnityTextureEvidence` into a general extraction framework.
- **Confidence:** High.

### F10 — Material binding provenance contains the current Unity slot assumption

- **Classification:** B
- **Files:** `Packages/com.alrauna.amuse/Editor/Analysis/MeshSeparationPlanner.cs`; `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`
- **Types/APIs:** `SubmeshSeparationInput.SourceMaterialBindingIndex`, `SubmeshSeparationPlan.SourceMaterialBindingIndex`
- **Current responsibility:** Carry a caller-defined binding identifier through alpha separation without interpreting it.
- **Assumption evaluated:** A submesh permanently maps one-to-one to a material slot with the same integer index.
- **Evidence:** The planner validates only non-negativity and preserves the value. `MeshSeparationPlannerTests.MultipleSubmeshesPreserveExplicitBindingsAndSourceOrder` uses bindings `4, 2, 4`, and the original design explicitly says duplicates are valid and the planner never infers a binding from submesh index or material count. The Unity facade supplies `submesh` as the binding only after requiring equal counts; its tests separately characterize that admitted host mapping.
- **Architectural significance:** The narrow Unity layout does not leak through the pure planning boundary. Future shared host records must represent actual ordered slot and submesh relationships rather than copying the facade's equality as a universal invariant.
- **Affected shared boundary:** Future host extraction and canonical relationships, not the current separation planner.
- **Future transformations:** Slot consolidation, layered draws, or changed submesh topology require their own relationship representation.
- **Consequence:** Preserve opaque provenance and keep host layout rules at the host boundary.
- **Confidence:** High.

### F11 — Existing identity roles are separable from future semantic addressing

- **Classification:** B
- **Files:** `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs`; `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`; `Packages/com.alrauna.amuse.research/Editor/Collection/AvatarCensusCollector.cs`; `Packages/com.alrauna.amuse.research/Editor/Collection/RendererObservationBuilder.cs`
- **Types/APIs:** `TextureSourceId`, `UnityTextureEvidence.TryGetSourceId`, `CensusAssetIdentity`, `RelativePath`
- **Current responsibility:** Give semantic texture samples an opaque source token, derive stable asset-backed tokens where available, and retain research diagnostic context.
- **Assumption evaluated:** GUIDs, instance IDs, names, paths, object references, slot indices, or submesh indices are universal semantic identity.
- **Evidence:** `TextureSourceId` performs only ordinal opaque-token equality. `TryGetSourceId` uses asset GUID plus local ID and explicitly refuses scene-only/generated/unidentifiable textures instead of fabricating identity. Research paths are documented as non-unique hints that nothing indexes by. Production source contains no renderer or generated-output semantic-address API. PR #12 explicitly separates host lookup/diagnostics, domain-owned semantic addresses, and transformation-owned logical output identities.
- **Architectural significance:** Asset-backed input identity is useful for current semantic coupling but cannot identify every generated output. That limitation is contained because generated-output addressing does not yet exist and is assigned a separate role by the approved architecture.
- **Affected shared boundary:** Future canonical domain addressing and generated-output resolution.
- **Future transformations:** The first generated mesh/material/texture must receive deterministic logical identity during preparation rather than relying on `TryGetSourceId`, name, or instance ID.
- **Consequence:** Preserve the role separation; do not repurpose an existing identifier as a universal identity system.
- **Confidence:** High for current source; exact future address shapes remain intentionally unresolved.

### F12 — Multi-output preparation is not constrained by an existing universal alpha plan

- **Classification:** B
- **Files:** `Packages/com.alrauna.amuse/Editor/Analysis/MeshSeparationPlanner.cs`; `docs/superpowers/specs/2026-08-21-upload-conditional-authorization-design.md`
- **Types/APIs:** Current `MeshSeparationPlan`; prospective transformation-specific preparation, expected-state contribution, and logical output identity roles
- **Current responsibility:** The source type describes only alpha separation; production preparation and mutation do not yet exist.
- **Assumption evaluated:** One input mesh must become one or exactly two outputs, all transformations modify geometry, and a shared mutation architecture must consume `MeshSeparationPlan`.
- **Evidence:** Current production contains no executor, NDMF plugin, prepared transformation, expected state, generated-asset API, or universal plan. The plan itself stops at logical memberships and does not assign final submeshes or create materials. PR #12 rejects plans carrying host postconditions and selects transformation-specific preparation capable of multiple immutable outputs and expected claims.
- **Architectural significance:** The first alpha preparer may naturally describe multiple output meshes/materials and relationships without redefining the plan as universal. Other transformations can have different prepared outputs and need not mention alpha or geometry.
- **Affected shared boundary:** The future shared composer/lifecycle consumes canonical claims and requirements, not a universal transformation plan.
- **Future transformations:** Atlas, material merge, UV-only, shader-only, and coordinated transformations each require concrete proof and preparation domains.
- **Consequence:** Implement the approved role separation later; do not introduce a generic preparation interface before evidence requires one.
- **Confidence:** High regarding current absence and approved ownership; concrete output descriptors remain intentionally undesigned.

## Explicit known-candidate review

### `UnityRendererAlphaAnalysis` one-renderer scope

Approved as F5/A. One renderer is the legitimate facade input for current alpha analysis. Nothing outside the facade treats it as the unit of every AMUSE transformation. Research code consumes it only to measure that exact subsystem. The reusable correction already required by PR #12 is to separate bounded coherent host extraction from proof and planning, not to make this facade multi-renderer.

### `MeshSeparationPlan` one-mesh opaque/preserved structure

Approved as F4/A. The plan is explicitly alpha-separation-specific in source and design. No mutation architecture treats it as universal, and no shared transformation base exists.

### Submesh and material-slot assumptions

Approved as F10/B. The pure planner carries opaque binding provenance and tests non-identity/repeated bindings. The equality and count restrictions exist only in the admitted Unity alpha facade. Future extraction and expected state must model real relationships rather than inherit the facade's restriction.

### Texture and UV boundaries

Approved as F3/A and F9/B. Alpha pixel evidence remains narrow. Shared semantic records are not alpha-only and support indexed UV mappings. UV0 restrictions are downstream proof restrictions. No generalized texture/UV system is warranted now.

### Multi-output preparation feasibility

Approved as F12/B. Preparation does not yet exist, so no current API prevents several meshes, materials, textures, changed slots, or cross-asset relationships. The merged design assigns those outputs to a transformation-specific preparer rather than `MeshSeparationPlan`.

### Identity and addressing

Approved as F11/B. Current source tokens and research diagnostic identities have bounded roles. No source treats Unity instance identity, names, paths, slot indices, or submesh indices as semantic equivalence. Future semantic addresses and logical output identities are separate responsibilities.

### `MaterialSemantics` scope

Approved as F8/B. It is a small normalized fact vocabulary, not complete renderer/material/mesh/texture/animation/build validation state, and contains no optimization decisions.

### Generated-output identity

No current implementation exists. This is not a Category-C absence. PR #12 already requires deterministic transformation-owned logical identity and prohibits name/instance-ID-only proof identity.

## Whole-codebase fixation review

The source and relevant tests were searched for assumptions equivalent to one proof/analysis/transformation per renderer, one plan/output per mesh, mandatory geometry change, mandatory opaque/transparent splitting, fixed slot topology, alpha-only texture meaning, universal UV0, asset-isolated reasoning, semantic policy, or inseparable planning and mutation.

The material occurrences were all contained:

- one renderer and one mesh appear in the named alpha facade;
- one mesh and opaque/preserved membership appear in the alpha separation plan;
- UV0 appears in the exact alpha input, resolver restriction, and alpha facade extraction;
- slot equality appears in the Unity alpha facade's admitted layout;
- opaque/transparent vocabulary appears in alpha proof, plan, tests, and alpha census observation;
- live Unity reads appear in host extraction/frontends and are already identified for coherent-extraction migration;
- generated outputs, preparation, mutation, and general orchestration are absent rather than modeled with alpha types.

No later optimizer would need to bypass an implemented shared transformation seam, because no such seam exists yet. The merged design's prospective shared lifecycle consumes domain-owned canonical state and concrete requirements rather than alpha plans.

## Semantic-purpose review

No optimization policy was found in the shared semantic model. There are no concepts equivalent to `CanAtlas`, `CanMergeMaterials`, `ShouldSplit`, `IsOptimizationCandidate`, `UseOpaqueOptimization`, or `CanRepackUv`.

The semantic layer records:

- constants;
- texture source and channel;
- indexed UV mapping with scale and offset;
- bounded sampling behavior;
- color interpretation;
- base color, scalar alpha, emission, and narrow normal meaning;
- complete-versus-unknown knowledge per output.

Shader frontends own feature gates and equations because those gates establish whether an output fact is complete. They do not emit optimization choices. `AlphaSemanticsResolver` is the first component that asks an optimization-specific proof question, and `MeshSeparationPlanner` is the first component that produces transformation-specific membership.

`MaterialSemantics` can remain small while richer feature/data-flow semantics are introduced alongside or beyond it. Replacing it with a giant shader IR now would be premature; treating it as all possible shader meaning or complete validation state would also be incorrect.

## Cardinality and ownership review

| Question | Current answer | Architectural conclusion |
|---|---|---|
| Multiple renderers? | Research enumerates many; production alpha facade handles one at a time; no product orchestrator exists. | Facade cardinality is contained. |
| Multiple meshes? | One per renderer alpha call; many calls are possible; no shared transformation scope exists. | Future coordinated proof owns its scope. |
| Multiple generated meshes/materials/textures? | Not implemented. | Transformation-specific preparation must establish them. |
| Multiple materials? | One ordered material array per renderer; repeated references supported. | Current alpha analysis supports its admitted layout only. |
| Multiple UV sets? | Semantic mapping supports indexed sets; alpha resolver/facade support UV0. | Shared semantics are not UV0-fixed. |
| Changed material slots/submesh topology? | Current analysis is read-only and separation plan defers final topology. | Future preparation/expected relationships own changes. |
| Cross-renderer coordination? | Not implemented. | New concrete optimizer domain, not alpha-facade widening. |
| Cross-asset relationships? | Semantic samples relate materials to source textures; full protected relationship state is not implemented. | Add bounded relationships when a real transformation needs them. |
| One prepared action affecting several targets? | Preparation/application do not exist. | Merged design permits it without specifying an interface. |

Ownership is likewise contained: pure alpha proof owns no Unity object, the current host facade owns live reads, shader frontends own shader equations, the planner owns only immutable memberships, and research owns only observation. Future mutation targets remain separate from proof-bearing values under the approved architecture.

## Identity and addressing review

Current identifiers serve four different roles:

1. `TextureSourceId` correlates normalized semantic samples with evidence through an opaque token.
2. Asset GUID/local ID provides one stable Unity-backed token for identifiable source textures.
3. Research hierarchy paths, names, and asset identities provide bounded observation context and are not keys for product proof.
4. Submesh and binding indices provide local ordered provenance inside one immutable source/plan.

None is a universal semantic address. No production renderer address or generated-output identity exists yet. The merged design correctly requires domain-owned semantic addresses, unique resolution before equality, deterministic transformation-owned logical output identity, and explicit protected relationships.

The key implementation guard is negative: do not turn `GetInstanceID`, a hierarchy path, an asset name, `TryGetSourceId`, a slot index, or a submesh index into a universal identity merely because it is available during the first vertical slice.

## Framework-overreach review

No Category-D finding was established.

- `SemanticOutput<T>` has several concrete typed uses and enforces the shared complete/unknown rule.
- `IEquatable<T>` implementations support deterministic structural comparison across semantic values.
- `AlphaFieldProvider` and `BaseMaterialSemanticsProvider` are single-link test/evidence seams with direct production implementations, not registries or public extension points.
- `UnityMaterialSemantics` uses two explicit branches and expressly rejects a registry before a third justified family.
- Poiyomi/lilToon duplicate helpers, result types, diagnostics, and attestation structures rather than abstracting superficial similarity.
- `UnityTextureEvidence` exposes exactly five facts with two demonstrated consumers and has a reflection guard against speculative expansion.
- No generic transformation interface, operation graph, dependency graph, asset compiler, snapshot manager, or universal result exists.

The repository contains documented future abstraction pressure, but it has generally responded by preserving duplication or returning `Unknown`, not by constructing a framework.

## Thought-experiment stress tests

### Scenario A — atlas transformation

1. **Entry:** A future atlas-specific discovery/proof path selects three renderers, several meshes, materials, and textures.
2. **Existing components that remain:** Basic `MaterialSemantics` facts may remain usable where complete; shader-specific frontends and source attestation remain relevant; the approved host lifecycle and canonical comparison responsibilities remain.
3. **First boundary requiring new work:** Atlas-specific texture content, mesh/UV evidence, proof, plan, and preparation do not exist.
4. **Classification:** Domain absent, not Category C. The transformation does not enter through `UnityRendererAlphaAnalysis` or `MeshSeparationPlan`.

**Result:** Current shared source does not force a one-renderer alpha split. Future work adds concrete atlas domains only when authorized.

### Scenario B — semantic material merge with a control texture

1. **Entry:** A future merge analyzer compares relevant material feature inputs and proves spatial encoding is behavior-preserving.
2. **Existing components that remain:** Current normalized constants, texture samples, channels, UV mappings, sampling facts, and shader attestation can remain where applicable.
3. **First boundary requiring new work:** Current semantics do not describe arbitrary rim/mask/dependency behavior, and no merge plan or generated control-texture preparation exists.
4. **Classification:** Domain absent/future semantic pressure, not Category C. No policy needs to enter `MaterialSemantics`.

**Result:** Add concrete facts and merge preparation when a real optimizer requires them; do not generalize alpha planning.

### Scenario C — UV-only transformation

1. **Entry:** A future UV analyzer consumes mesh UV/layout and texture-region facts without requesting alpha proof.
2. **Existing components that remain:** `UvMapping` can continue describing material sample coordinates; shader frontends and host lifecycle responsibilities remain.
3. **First boundary requiring new work:** No general immutable mesh/UV extraction, UV proof, or UV-output preparation exists.
4. **Classification:** Domain absent, not Category C. `AlphaSemanticsResolver`'s UV0 restriction is intrinsic and is bypassed.

**Result:** No shared architecture requires alpha classification or a mesh split merely to transform UVs.

### Scenario D — shader-feature simplification

1. **Entry:** A future feature analyzer consumes captured shader/material facts and proves a feature unreachable or constant.
2. **Existing components that remain:** Shader-specific source attestation, normalized current output facts, and the approved lifecycle/canonical-state roles remain relevant.
3. **First boundary requiring new work:** The four-output vocabulary does not describe arbitrary feature reachability, and no material/shader-only preparation exists.
4. **Classification:** Domain absent/future semantic pressure, not Category C. No current shared boundary mandates geometry mutation.

**Result:** A shader-only transformation can coexist with current semantics and avoid every alpha geometry type.

### Scenario E — multi-renderer and multi-asset coordinated transformation

1. **Entry:** A future optimizer selects a bounded cross-renderer asset set and proves a coordinated result.
2. **Existing components that remain:** Per-domain semantic facts and the merged design's build-scoped expected-state, relationship, composition, handoff, and late-gate responsibilities remain.
3. **First boundary requiring new work:** Product-wide discovery, coordinated proof, and multi-target preparation are not implemented.
4. **Classification:** Domain absent, not Category C. The one-renderer alpha facade is not the product orchestration boundary.

**Result:** Current code supplies no shared one-renderer bottleneck; later work must not create one by elevating the alpha facade.

### Scenario F — richer shader feature understanding

1. **Entry:** A future analyzer requests feature inputs, constants, texture/channel sources, masks, dependencies, or outputs beyond the four current semantic fields.
2. **Existing components that remain:** Existing complete facts and shader-specific attestation remain valid; unknown remains conservative.
3. **First boundary requiring new work:** A sibling or extended semantic domain and captured inputs are required; the exact form is not established.
4. **Classification:** Future pressure, not Category C. `MaterialSemantics` need not become or be replaced by a universal shader IR merely to admit another semantic layer.

**Result:** Richer vocabulary can evolve without replacing the host lifecycle, alpha proof, or existing semantic values.

## Category-C severity and timing

No Category-C findings were identified, so no C0-C4 resolution horizon is assigned.

In particular:

- no finding contradicts the merged upload-conditional architecture;
- no current shared boundary must be corrected before immutable extraction migration;
- no existing preparation, expected-state, mutation, or handoff type encodes alpha cardinality, because those production types do not yet exist;
- future optimizer pressure is not promoted to a blocker without a current obstruction.

## Required corrections before implementation planning

**None.**

There are no Category-C findings affecting the upcoming upload-conditional production milestones.

This conclusion does not waive work already required by the merged design: coherent eager extraction, separation of capture from pure interpretation, alpha-specific preparation, canonical expected-state and relationship records, deterministic logical output identities, mutation/handoff integration, and late validation remain required architecture implementation work.

## Future pressure, not a current blocker

These pressures are guardrails, not additional implementation requirements. Some boundaries are expected to be instantiated by work the merged upload-conditional design already requires; others remain deferred until a later concrete optimizer supplies the trigger. In either case, absence today is not a Category-C defect.

| Future pressure | Trigger horizon | Current disposition | Expected trigger |
|---|---|---|---|
| Shared mesh extraction breadth | Current coherent-extraction migration, bounded to the alpha proof | Keep the first extraction purpose-scoped; do not claim UV0 or current attributes are the universal mesh domain. | Instantiate the bounded mesh capture required by the current alpha proof. Revisit its breadth when a real proof requires another UV set, vertex-attribute domain, topology fact, or mesh relationship not represented there. |
| General texture content evidence | Later concrete texture optimizer | Keep predicate-equivalent alpha evidence separate from general pixels or regions. | Revisit when a production texture transformation needs immutable content, region, mip, format, or channel evidence that the alpha predicate cannot represent. |
| Generated-output identity | Current alpha preparation/mutation vertical slice | Keep source asset identity separate from deterministic logical output identity. | Trigger when the first production alpha transformation generates a mesh, material, texture, or shader artifact that `ExpectedTransformedState` must reference. |
| Cross-domain relationship representation | Current alpha preparation/expected-state vertical slice, limited to actual alpha effects | Add only bounded relationships used by a concrete proof or validation contract. | Trigger for each relationship the first production alpha transformation actually creates, removes, or rewrites and must protect during late validation; extend later only when another transformation changes another relationship. |
| Richer feature/data-flow semantics | Later concrete optimizer | Keep `MaterialSemantics` small and fact-oriented; add no optimizer policy. | Revisit when a real optimizer must reason about feature inputs, constants, texture/channel sources, masks, dependencies, or outputs beyond the current vocabulary. |
| Material/submesh/slot topology | Current alpha vertical slice if alpha separation changes these relationships; otherwise later | Keep the alpha facade's admitted one-to-one input layout local to that facade. | Trigger to the exact extent alpha preparation changes material bindings, submesh topology, or slot relationships that expected state must represent; revisit broader layouts when a later transformation needs them. |
| Transformation orchestration | Later, when independently prepared transformations interact | Coordinate known work concretely; do not create a sequencing framework. | Revisit when two independently prepared production transformations require ordering, cooperation, conflict resolution, or one consumes the other's output. |
| Global planning | Later joint-candidate optimizer | Keep purpose-specific candidate plans independent while no joint decision exists. | Revisit when multiple individually valid candidate transformations compete, depend on one another, or must be selected jointly. |
| Cross-renderer scope | Later cross-renderer optimizer | Keep renderer-alpha analysis as a per-renderer facade. | Revisit when one concrete proof or prepared action must coordinate several renderers as one correctness unit. |
| Richer shader IR or feature portability representation | Later concrete feature-reconstruction or portability work | Keep shader equations specific and reuse current semantic facts where sufficient. | Revisit only when concrete feature reconstruction or portability work demonstrates that the existing semantic layers are insufficient. |
| Public shader frontend extensibility | Later polymorphic or external consumer | Keep explicit internal dispatch and shader-family implementations. | Revisit when a real polymorphic consumer or supported external frontend requires a stable extension contract. |

## Components explicitly approved as purpose-specific

Future work should not repeatedly attempt to generalize the following merely because they contain alpha, one-renderer, one-mesh, or shader-specific concepts:

- `TriangleAlphaClassifier` and `TriangleAlphaOutcome`;
- `TriangleAlphaInput`, including its UV0-specific contract;
- `AlphaTextureData` and alpha sampling enums;
- `ExactUvGeometry` as used by exact alpha classification;
- `AlphaSemanticsResolver`, `AlphaResolution`, and `AlphaFieldProvider`;
- `UnityAlphaFieldEvidence`'s predicate-equivalent alpha contract;
- `MeshSeparationInput`, `MeshSeparationPlan`, and `MeshSeparationPlanner`;
- submesh separation dispositions and opaque/preserved membership;
- `RendererAlphaAnalysis`, `SubmeshAlphaAnalysis`, and renderer-alpha refusal vocabulary;
- `UnityRendererAlphaAnalysis` as a one-renderer alpha facade;
- Poiyomi-specific equations, gates, diagnostics, and source attestation;
- lilToon-specific equations, gates, diagnostics, source canonicalization, include closure, and attestation;
- alpha-specific research census mappings and per-renderer observations.

Approval does not bless live proof reads or promote facade restrictions into shared extraction. `UnityAlphaFieldEvidence` and the shader frontends still require the coherent eager-capture migration already mandated by PR #12.

## Uncertainties and insufficient evidence

- No production preparation, mutation, expected-state, canonical-address, generated-output, or product-orchestration code exists, so this audit can validate the merged ownership rules but cannot classify concrete implementations that have not been written.
- The exact canonical address shape for renderers, meshes, materials, textures, and generated outputs remains intentionally unresolved. Only the separation of semantic identity from lookup/diagnostics is binding.
- The current four-output semantic vocabulary has been validated by two toon-shader frontends. Whether it remains sufficient for a substantially different shader family is unproven; this is a revisit trigger, not a defect.
- The current Unity alpha facade refuses unequal material/submesh counts. This audit establishes containment, not complete knowledge of every Unity draw-layout behavior.
- Animation/material reachability remains incomplete. That limits positive coverage and extraction scope but does not make the alpha plan a universal state model.
- External lifecycle and SDK callback facts belong to `investigate/sdk-build-environment-contract` and were not re-investigated here.

## Relationship to the upload-conditional architecture

The merged design remains internally consistent with the current source:

- existing alpha proof and planning can remain purpose-specific;
- live host reads must move into coherent eager capture;
- transformation-specific preparation prevents the alpha plan from becoming universal;
- build-scoped canonical claims and relationships permit multiple domains and targets;
- domain-owned addresses and logical output identities avoid Unity-object semantic identity;
- `MaterialSemantics` remains one normalized fact domain, not complete validation state;
- the late lifecycle, handoff, and gate are independent of which transformation domain contributed claims.

No C0 contradiction was found, and no design question needs to be reopened before the next investigation.

## Implications for `investigate/sdk-build-environment-contract`

The next investigation remains unchanged and should stay lifecycle-focused. It must establish upload-path detection, callback inventory and ordering, build-attempt association, concurrency/supersession, cancellation/reload behavior, exact host-version identity, failure enforcement, and SDK-facing assembly/package constraints.

It should not:

- redesign alpha proof or separation planning;
- choose atlas, UV, merge, or shader-portability domains;
- use `MaterialSemantics` as the complete validation snapshot;
- assume one renderer identifies a build attempt;
- assume an instance ID or object reference establishes semantic equivalence;
- resolve future pressure whose revisit trigger has not occurred.

## Implications for later implementation planning

Later planning may proceed from the merged architecture after the SDK investigation. It should preserve these audit constraints:

1. Migrate live reads into bounded coherent immutable capture without turning the alpha facade's UV0, slot, or renderer cardinality into universal records.
2. Keep alpha proof and `MeshSeparationPlan` unchanged unless a concrete alpha correctness defect is independently established.
3. Make the first preparation alpha-specific while allowing its concrete result to describe every output and relationship that alpha separation actually creates.
4. Compose canonical domain claims and relationships rather than composing universal transformation-plan objects.
5. Keep application targets and Unity lookup handles outside proof-bearing values.
6. Assign generated outputs deterministic logical identities when the generated-output revisit trigger occurs.
7. Do not add orchestration, global planning, or shader-IR machinery until the corresponding concrete trigger occurs.

## Recommended next branch

After this audit is reviewed, committed, and merged, the next branch remains:

`investigate/sdk-build-environment-contract`

No architecture revision branch is required by this audit. This branch must not transition automatically into the investigation.

## Validation

This is a documentation/source audit, so no Unity run or production test modification is required. Written-artifact validation observed before review:

- Every finding was checked against cited source, tests, and approved design records.
- All 12 material findings use A/B/C/D consistently: 7 A, 5 B, 0 C, and 0 D.
- No Category-C blocker was inferred solely from a missing future domain.
- Every meaningful future-pressure item includes a concrete revisit trigger.
- All six thought experiments inspect current boundaries without designing future systems.
- `git diff --check` reported no tracked whitespace errors; the equivalent no-index check for the new document reported no whitespace diagnostic.
- The no-index diff stat reports one new file with 600 inserted lines.
- `git status --short --untracked-files=all` identifies only this audit document; staged and tracked unstaged diffs are empty.
- The repository remains on `audit/general-purpose-transformation-boundaries` at the approved base.

Unity and the private Census Lab were not used or modified.
