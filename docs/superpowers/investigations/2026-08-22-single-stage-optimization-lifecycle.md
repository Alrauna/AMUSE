# AMUSE single-stage optimization lifecycle investigation

## Status and scope

**Preliminary Outcome A is approved. This durable investigation record awaits review.**

This document records an investigation. It does not implement an NDMF plugin, material conversion, mesh mutation, animation-reachability extractor, lilToon generator model, SDK callback, Apply-on-Play support, or coexistence behavior.

- Branch: `investigate/single-stage-optimization-lifecycle`
- Base: `origin/main` at `f969b5ba6426b7a92baec635bae5977f82d190c5`
- Required predecessor: the merged `investigate/sdk-build-environment-contract` record is present on the base
- Census Lab and private avatars: not used or modified
- Production code: not modified
- DAO and AAO: inspected as public MIT-licensed source precedent only. Not installed or executed
- Probe artifacts: no new probe was needed. Exact source plus existing controlled evidence answered the distinguishing questions

The primary result is Outcome A: single-stage is sufficient for the targeted architecture. It comes with a deliberately bounded host-envelope statement:

> The targeted alpha transformation is semantically NDMF-complete once every remaining proof-relevant later actor is ordered before AMUSE, modeled, proven irrelevant, or causes conservative refusal.

This investigation removes the need for later lilToon generated-source observation and cross-callback build-attempt authorization for the targeted alpha transformation. It does not establish safe execution in an arbitrary third-party plugin environment. Establishing a bounded and characterized remainder of the real avatar build pipeline belongs to `investigate/coexisting-optimizer-lifecycle`.

## Decision summary

The targeted transformation has two separate proof obligations:

1. **Source/equivalence proof:** prove that geometry selected from the original material already has the visual alpha and coverage behavior required for conversion. This obligation covers texture alpha, material color alpha, alpha masks, cutoff, dissolve, dither, animation, sampling, geometry, UVs, and rasterization. Every other source-material contributor stays relevant whenever the original shader semantics use it.
2. **Transformed opaque-target theorem:** prove that the prepared, exactly attested opaque lilToon target will have the required output alpha and coverage behavior after lilToon callback 100.

The second obligation can be discharged by a narrow semantic projection of exact lilToon `2.3.4` source. Recreating the generated shader text is unnecessary. For the exact base opaque target, `LIL_RENDER == 0` forces surviving forward fragments to alpha exactly one and excludes the cutout/transparent alpha termination branches. Coverage closure also requires the target to make every reachable `_Invisible`, ID-mask, and UDIM suppression path inert, or to prove the corresponding compiled feature absent. Source inspection found that the existing production check of only `_Invisible` and `_UDIMDiscardCompile` is not exhaustive. ID-mask vertex suppression is a separate path. Pixel-mode UDIM discard depends on the row masks and does not directly check `_UDIMDiscardCompile` in the fragment call site.

Those additional facts are available or conservatively rejectable during late NDMF. They narrow the positive profile, but they do not create a later lifecycle dependency. Ambiguous animation, material substitution, source closure, feature activation, or later mutation yields refusal before mutation.

Consequently, for this path:

- Callback 100 is modeled rather than observed afterward.
- Exact generated shader text is not a proof input.
- No later AMUSE SDK authorization is required.
- No cross-callback build-attempt identity is required.
- Conditional authorization stays conceptually available only for future transformations proven genuinely future-dependent.
- Apply-on-Play is no longer excluded by this specific late-authorization dependency, although this record does not certify support or UX.
- Coexistence and ordering relative to DAO, AAO, VRCFury, Modular Avatar, and other tools remain deferred.

## Question

Can AMUSE completely prove the current targeted alpha-separation transformation, create its immutable plan, and mutate once during a late NDMF `Optimizing` stage without depending on a later AMUSE SDK callback?

More precisely: can every proof-relevant fact that would otherwise become authoritative later be handled by one of four treatments?

- **ORDER:** the relevant operation completes before AMUSE captures proof input.
- **MODEL:** the later proof-relevant result is deterministic from exact inputs available to AMUSE.
- **IRRELEVANT:** the later operation cannot affect the scoped proof.
- **REFUSE:** ordering, modeling, or irrelevance is insufficient, so the affected candidate stays on the original path.

The investigation tests this question only against the current alpha vertical slice and the exact lilToon pressure case. It does not claim that every future AMUSE transformation is NDMF-complete.

## Non-goals

This investigation does not:

- Implement production code or revise the upload-conditional design.
- Implement the currently absent opaque material target or mesh mutation executor.
- Solve exhaustive arbitrary Unity animation reachability.
- Reproduce lilToon's shader generator.
- Build a shader IR, universal snapshot, mutation DSL, provider system, or orchestration framework.
- Install or run DAO or AAO.
- Select ordering relative to DAO, AAO, VRCFury, Modular Avatar, or another third-party tool.
- Characterize an arbitrary callback inventory or certify a general plugin environment.
- Implement Apply-on-Play or preview UX.
- Use the Census Lab, private avatars, or uploads.
- Revise `docs/architecture/vision.md` or an earlier investigation or specification.

## Exact environment

| Component | Exact identity | Evidence |
|---|---|---|
| Repository base | `f969b5ba6426b7a92baec635bae5977f82d190c5` | branch creation and Git ancestry |
| Unity | `2022.3.22f1`, revision `887be4894c44` | `ProjectSettings/ProjectVersion.txt` |
| NDMF | `1.14.4`, upstream `7cf8a13444ac19e46ac2b4146bad209de15dc42d` | embedded package and prior pin |
| VRChat SDK Base/Avatars | `3.10.4` | prior exact-package lifecycle investigation |
| lilToon | `2.3.4`, upstream `252fd8cfc46106d4967e95b3f2c788418502f227` | exact public source checkout and prior pin |
| d4rkAvatarOptimizer precedent | upstream `b2e500869610f7eea7645c8384eaecdf00167be4` | public source checkout on 2026-08-22 |
| Avatar Optimizer precedent | `1.9.18-beta.1`, upstream `6e6babc53c4086e7b1038b50dc01b1e36f065ef1` | public source checkout on 2026-08-22 |

This investigation needed no environment mutation and no Unity execution. The public repository stayed the only project in scope.

## Evidence hierarchy

- **Existing AMUSE contract:** a rule already established by merged AMUSE code or approved records.
- **Exact-version source fact:** behavior enforced by the exact pinned source. This record does not generalize the fact to other versions.
- **Empirical evidence:** behavior observed by an existing controlled exact-version probe or matrix.
- **Inference:** a conclusion derived from the preceding evidence and marked as such.
- **Unknown:** a fact not established with adequate evidence. It cannot authorize mutation.

The proof-authority tables name the evidence class for each material claim. DAO or AAO precedent never counts as proof.

## Sources inspected

### Existing AMUSE records

- [lilToon attestation investigation](../specs/2026-08-21-liltoon-attestation-investigation-design.md)
- [lilToon attestation hardening](../specs/2026-08-21-liltoon-attestation-hardening-design.md)
- [lilToon official integration matrix](../specs/2026-08-21-liltoon-official-integration-matrix-design.md)
- [analysis snapshot and ordering](../specs/2026-08-21-analysis-snapshot-ordering-design.md)
- [lilToon build-callback handoff](../specs/2026-08-21-liltoon-build-callback-handoff-design.md)
- [upload-conditional authorization](../specs/2026-08-21-upload-conditional-authorization-design.md)
- [general-purpose transformation boundary audit](../audits/2026-08-22-general-purpose-transformation-boundaries.md)
- [SDK build-environment contract](2026-08-22-sdk-build-environment-contract.md)
- [separation-plan design](../specs/2026-08-15-separation-plan-design.md)

### Current AMUSE implementation

The investigation traced the current renderer analysis, material semantics, source attestation, alpha resolution, texture evidence, exact triangle classifier, and immutable separation planner. Current product code analyzes and plans. It does not yet create an opaque material counterpart or apply the planned mesh mutation.

### Exact external implementation source

The investigation inspected:

- NDMF phase ordering, solver constraints, VRChat hooks, `BuildContext` finish, platform-finish passes, and Play-mode reinitialization hooks.
- lilToon callback 100, shader-setting collection, animation and material scanning, input optimization, importer and generator paths, base opaque templates, common vertex and fragment code, alpha code, UDIM implementation, ID-mask implementation, and official integration activation.
- VRChat SDK `3.10.4`, only through the already-approved exact lifecycle record, where later callback behavior was relevant.
- DAO and AAO as bounded technical precedent.

Source inspection, together with the existing exact-version callback and eight-state integration evidence, was sufficient. A new disposable generator oracle would have repeated already-observed behavior without answering an open source question.

## Prior lifecycle and why it failed

The previous upload-conditional architecture was:

```text
NDMF proof
    -> conditional mutation
    -> later lilToon and SDK work
    -> AMUSE SDK validation
    -> authorization or refusal
```

The SDK lifecycle investigation established that stock SDK `3.10.4` cannot provide authoritative pre-mutation association between a high-level supported request, its cloned avatar, NDMF mutation, and a later refusal gate. That finding applies when correctness depends on later evidence.

It does not establish that later evidence is necessary. Transformations that were already NDMF-complete on their own stayed unaffected.

The single-stage candidate is:

```text
late NDMF state
    -> eager immutable extraction
    -> source/equivalence proof
    -> exact target implementation and input identity
    -> narrow callback-100 semantic projection
    -> transformed opaque-target theorem
    -> immutable combined decision/plan
    -> mutation once
    -> no later AMUSE authorization dependency
```

## Two independent proof obligations

### Obligation A: source/equivalence proof

The source proof asks whether each triangle selected for an opaque counterpart already has the required effective visual alpha and coverage under the original material, in every supported reachable state.

This is the complete renderer-to-plan chain:

```text
renderer and property block
    -> mesh, submesh, topology, indices, positions, and UVs
    -> source material assignment in every admitted reachable state
    -> exact source shader semantics
    -> alpha expression and all source coverage controls
    -> texture/import/sampling evidence where referenced
    -> exact continuous geometry/UV classification
    -> ProvenOpaque / MustRemainTransparent / Unknown
    -> immutable separation candidate plan
```

No target-side opaque theorem can replace this obligation. In particular:

- Texture alpha stays relevant when the source alpha expression samples it.
- Material color alpha stays relevant when the source multiplies it or otherwise uses it.
- Alpha masks, cutoff, dissolve, dither, distance fade, and other source controls stay relevant when source semantics use them.
- Texture wrap, filter, importer format, CPU/GPU alpha-predicate equivalence, geometry, and UV domains stay relevant for sampled alpha.
- Material swaps and animated material properties stay relevant to reachable source state.
- Source-side fragment suppression, vertex suppression, culling, deformation, and pass selection stay relevant to visual coverage and behavior preservation.

Only `ProvenOpaque` triangles may become opaque candidates. `MustRemainTransparent` and `Unknown` stay on the original preserved path. A later target-conversion rejection abandons the candidate without changing that original path.

### Obligation B: transformed opaque-target theorem

The target theorem asks whether the prepared target material and the exact future callback-100 result preserve the required behavior while rendering surviving fragments with alpha exactly one.

Statements that `_MainTex` alpha, `_Color.a`, alpha masks, cutoff, dissolve, and dither are irrelevant apply **only** inside this exact transformed, attested base-opaque lilToon theorem. They do not apply to the source/equivalence proof.

The exact positive target profile is deliberately narrow:

- Base `lilToon` shader identity: not multi, outline, lite, tessellation, fur, gem, refraction, derivative, custom container, or another named variant.
- Exact lilToon `2.3.4` package, source, template, and include provenance.
- Exact supported render-pipeline and integration closure.
- Base opaque pass relationship through `Hidden/ltspass_opaque`.
- Generated `LIL_RENDER == 0`.
- Every target material and animation input that coverage closure needs, captured as immutable values.
- Refusal of every uncharacterized source modification, macro injection, custom hook, material replacement, or later semantic writer.

## Exact lilToon callback-100 input model

### Complete input surface

Exact-version source shows that callback 100 consumes or depends on:

- The avatar root's renderer `sharedMaterials`.
- Animation clips reached through child `Animator` components and VRC descriptor playable layers.
- Object-reference material-swap curves and animated material-property bindings.
- Material parent relationships on supported Unity versions.
- All serialized material properties and textures inspected by shader-feature analysis.
- Current shader identities and `UsePass` relationships.
- Persisted lilToon shader settings.
- Static editor state, including `forceOptimize` and modified-shader state.
- `Application.unityVersion` and the Unity-version workaround branch.
- Active render pipeline and generator/importer pipeline information.
- Compile-time package and integration symbols.
- Base templates, block expansion, includes, optimized input source, and external integration source.
- `PlayerSettings.colorSpace`, for some optimized scalar and color constants.
- AssetDatabase/import state, and whether generation succeeds or is skipped.

The callback calls `SetShaderSettingBeforeBuild(materials, clips)`, then `SetupMultiMaterial(materials, clips)`. `SetupMultiMaterial` mutates only materials whose shader is recognized as a lilToon multi shader. It is therefore irrelevant to the admitted base target. Encountering a multi target or later substitution causes refusal.

### Which inputs affect the target theorem

The target theorem does not need the whole output text. Its proof-relevant inputs are:

- Exact implementation, template, include, and integration identity.
- Base target shader and opaque pass family.
- Generator activation and feature macro state, where either can select coverage code.
- Target material values and all admitted reachable animated values for coverage controls.
- Target mesh data used by ID-mask or UDIM suppression, if either feature can be active.
- Rasterization, deformation, and pass facts that the conversion must preserve from the source.
- Absence, or characterized irrelevance, of later proof-relevant writers.

Color-space-dependent optimized constants, lighting features, emission features, and ordinary RGB equations are irrelevant to this alpha-and-coverage theorem only where exact source shows that they cannot affect coverage or overwrite opaque alpha.

## Narrow semantic projection of callback 100

### Opaque alpha value

Exact `ltspass_opaque.lilinternal` defines `LIL_RENDER 0`. Exact `lts.lilinternal` selects `Hidden/ltspass_opaque` and opaque render tags. In `lil_pass_forward_normal.hlsl`:

- Alpha mask and dissolve may alter the intermediate `fd.col.a`.
- Dither runs only when `LIL_RENDER == 1`.
- Cutout discard and transparent clip branches exist only for `LIL_RENDER == 1` or `2`.
- The `LIL_RENDER == 0` branch then assigns `fd.col.a = 1.0`.
- Distance fade changes alpha only for `LIL_RENDER == 2`.
- The normal output returns the resulting `fd.col`.

So, in the exact admitted base opaque path, `_Color.a`, `_MainTex` alpha, alpha-mask state, cutoff, dissolve, and dither cannot change the final alpha of a surviving forward fragment. That statement is target-local. It says nothing about whether the original source triangle qualified for conversion.

### Coverage closure

Alpha exactly one for surviving fragments is not enough. Exact source inspection found the following proof-relevant ways the admitted normal opaque target can suppress or alter fragment coverage.

#### `_Invisible`

`lil_common_vert.hlsl` returns an initialized, empty vertex output when `_Invisible` is true for the base non-outline path. The target theorem must prove `_Invisible == 0` for every admitted reachable target state. Missing, non-finite, animated-but-unresolved, or nonzero evidence refuses the candidate.

#### ID-mask vertex suppression

When `LIL_FEATURE_IDMASK` is compiled, `lil_common_vert.hlsl` computes `IDMask` from vertex ID or selected UV data, `_IDMaskIndex1..8`, `_IDMask1..8`, `_IDMaskPrior1..8`, `_IDMaskIsBitmap`, and `_IDMaskControlsDissolve`. A masked vertex receives a NaN clip-space position, which suppresses its primitive.

`_IDMaskCompile` takes part in callback feature activation, but it is not the shader's runtime guard around the vertex suppression block. So checking `_IDMaskCompile == 0` alone would not prove inert coverage if the feature was compiled for another used material or animated binding.

The smallest supported rule is:

- Prove `LIL_FEATURE_IDMASK` absent from the generated target pass, **or**
- Prove every reachable target `_IDMask1..8` and `_IDMaskPrior1..8` suppression flag is exactly zero, with no unresolved animation or substitution.

The index, source, and mode fields become irrelevant when all suppression flags are exactly zero. This record refuses any broader active-ID-mask target rather than modeling it with a general shader IR.

#### UDIM vertex and pixel discard

When `LIL_FEATURE_UDIMDISCARD` is compiled:

- Vertex mode suppresses primitives when `_UDIMDiscardMode == 0`, `_UDIMDiscardCompile == 1`, and `LIL_CHECK_UDIMDISCARD` matches.
- Pixel mode executes `discard` when `_UDIMDiscardMode == 1` and `LIL_CHECK_UDIMDISCARD` matches.
- The `lilUDIMDiscard` function receives `_UDIMDiscardCompile`, but its returned predicate does not use that argument. So this record cannot prove pixel-mode inertness from `_UDIMDiscardCompile == 0` alone.
- The actual predicate uses the selected UV channel and `_UDIMDiscardRow0_0` through `_UDIMDiscardRow3_3`, with a fixed `0.001` threshold.

The smallest supported rule is:

- Prove `LIL_FEATURE_UDIMDISCARD` absent, **or**
- Prove all sixteen reachable target UDIM row-mask values are finite and at or below the exact discard threshold in every admitted state, which makes both the vertex and the pixel predicates false. A stricter exact-zero gate is acceptable, and simpler.

This record refuses any active or unresolved UDIM target. `_UDIMDiscardCompile == 0` stays useful generator-activation evidence, but it is not exhaustive coverage evidence.

#### Alpha-derived termination

The exact normal forward include contains cutout `discard` and transparent `clip` sites, but their preprocessor conditions exclude them for `LIL_RENDER == 0`. The subpass alpha include is also enclosed by `LIL_RENDER > 0`. So alpha mask, dissolve, cutoff, and dither do not terminate fragments in the admitted opaque target.

#### Other vertex/raster coverage behavior

Clipping-canceller state can change near-plane coverage. Culling, vertex deformation, topology, pass selection, and platform rasterization also affect visual coverage, and the alpha-one theorem does not erase them. The combined transformation must prove that the prepared target preserves the source-required values and behavior, or refuse conservatively. Where callback 100 can compile a relevant feature, its activation and target inputs belong in the model.

Outline deletion sits outside the admitted base non-outline target. Fur, tessellation, lite, gem, refraction, multi, custom, and derivative paths also sit outside the target. This record refuses them rather than extending the base theorem to cover them.

#### Custom and integration overrides

The exact base source defines hook macros that a distinct custom shader or external included source could override. Exact shader identity, canonical source provenance, complete include closure, and activation evidence therefore stay prerequisites.

The current production positive profile is standalone. The existing eight-state official integration matrix establishes exact source composability and deterministic closure identity for its selected combinations, but it does not by itself certify that every external closure is coverage-inert under this strengthened theorem. Each integrated profile must either pass review against the coverage hooks and join the exact projection, or stay refused. This narrows current positive support. It does not change the single-stage lifecycle result.

### Exhaustiveness result

For the exact admitted standalone base opaque forward profile, inspection of every `clip`, `discard`, empty/NaN vertex-output path, render-mode branch, and relevant feature hook establishes this bounded result:

- `_Invisible`, ID-mask suppression, and UDIM suppression are the explicit target-local primitive/fragment removal paths that need a dedicated inertness proof.
- Opaque alpha assignment excludes the alpha-derived cutout/transparent termination paths.
- Source/target equality, or a separate exact model, stays required for clipping canceller, culling, deformation, topology, pass, and other non-alpha coverage facts.
- Custom, derivative, and integration code is not assumed inert. This record requires separate attestation and review, or refusal.

This investigation supersedes the earlier statement that `_Invisible` and `_UDIMDiscardCompile` alone were exhaustive. Production code stays intentionally unchanged on this branch. A later implementation must add the stricter gates before this target theorem may authorize mutation.

## Determinism without generator reproduction

### Deterministic proof-relevant result

Callback 100 has ambient and static inputs, but the target theorem needs only the projection above. For every admitted input tuple:

- If generation runs, the exact attested template and generator produce the same opaque pass family and alpha/coverage branches.
- If generation is skipped, the currently attested base opaque source has the same projected theorem.
- Feature stripping or constant folding may remove inactive code or replace inputs with constants, but it cannot turn an admitted inert target gate into active suppression.
- An exact source, package, closure, activation, or reachable-value mismatch causes refusal.

The projection is deterministic with respect to its complete relevant input set. Inputs that affect shader text but provably cannot affect the projection do not become proof inputs merely because the generator reads them.

### Why exact textual output is unnecessary

The transformation needs a theorem about alpha and coverage, not a byte-for-byte prediction of every generated shader line. Recreating the full generator would add a second implementation, and its textual equivalence would itself need proof and maintenance.

The smaller sufficient chain is:

```text
exact implementation and closure identity
    + exact target pass identity
    + exact reachable coverage inputs
    + exact feature activation relevant to coverage
    -> projected alpha/coverage theorem
```

Raw and canonical generated-source digests stay valuable as implementation-identity evidence and as investigation oracles. This target does not need them as later per-build authorization evidence.

## Animation and material reachability

Current production extraction reads current/base renderer and material state. It does not yet build an eager, immutable, value-only model of all admitted reachable material assignments and proof-relevant material-property values.

This stays a correctness and implementation gap. It is not evidence of a necessary late lifecycle dependency, because the positive profile may conservatively require one of the following before mutation:

- Proof that no relevant material swap or property animation exists.
- A future bounded extractor that proves every admitted reachable value and assignment.
- Refusal when controller, clip, binding, override, material substitution, or other reachability is unsupported or ambiguous.

This investigation does not claim to solve exhaustive arbitrary Unity animation semantics. It only establishes that ambiguity can remove positive capability at NDMF time rather than force a later authorization callback.

## Proof-authority timeline

| Stage/actor | Proof-relevant effect | Treatment | Evidence class | Late authorization required? |
|---|---|---|---|---|
| NDMF `FirstChance` through `Transforming` | Earlier component, controller, hierarchy, material, and mesh generation | **ORDER** | exact NDMF source | no |
| SDK component preprocess at `-2048` | Component-provided preprocessing before late NDMF | **ORDER** | prior exact SDK lifecycle evidence | no |
| NDMF late hook at `-1025` | Runs `Optimizing` through `PlatformFinish` and finishes the context | AMUSE's intended single-stage envelope | exact NDMF source | no |
| SDK network-ID assignment at `-1024` | Assigns IDs unrelated to renderer alpha | **IRRELEVANT** | exact/empirical prior lifecycle evidence | no |
| SDK EditorOnly removal at `-1024` | Can remove a whole object after NDMF | **IRRELEVANT** to false-positive alpha conversion; may make work dead | exact/empirical prior lifecycle evidence plus inference | no |
| lilToon callback `100` | Scans materials/animations, strips features, optimizes inputs, regenerates source | **MODEL** through the narrow target projection | exact lilToon source plus prior empirical generation evidence | no |
| lilToon `SetupMultiMaterial` | Mutates multi-shader materials | **IRRELEVANT** for exact base target; **REFUSE** multi/substitution | exact lilToon source | no |
| NDMF Play-mode PhysBone/constraint hooks at `int.MaxValue` | Reinitialize dynamics/constraints only in Play Mode | **IRRELEVANT** to mesh/material/shader alpha | exact NDMF source | no |
| Any uncharacterized later semantic writer | Could replace or mutate proof input/output | **REFUSE** affected candidate | unknown by definition | no mutation, therefore no late authorization |

The final row is not a claim that no such actor exists. Bounding and characterizing the real remainder belongs to the coexistence investigation.

## Current alpha proof-authority map

| Fact | Source | Earliest trustworthy stage | Immutable after capture? | Potential later actor | Treatment | Confidence/evidence | Late AMUSE authorization? |
|---|---|---|---|---|---|---|---|
| Target renderer/root and property-block state | renderer extraction | late NDMF after earlier transforms | must become value-only | unknown renderer writer | **ORDER/REFUSE** | existing contract | no |
| Mesh identity, topology, submeshes, indices, positions, UVs | mesh extraction | late NDMF | must be copied into immutable input/plan | unknown mesh/UV writer | **ORDER/REFUSE** | existing contract | no |
| Source material binding for each submesh | renderer slots plus reachable swaps | late NDMF | not in current implementation for reachable states | animation/substitution | **MODEL if bounded; otherwise REFUSE** | implementation gap | no |
| Source material alpha equation | exact shader adapter | late NDMF | yes after normalized extraction | source/material writer | **ORDER/REFUSE** | existing contract | no |
| Source texture alpha and sampling | texture/importer plus shader semantics | late NDMF | current live texture retention must become eager values where used | texture/import mutation | **ORDER/REFUSE** | existing exact classifier contract plus implementation gap | no |
| Source material color, alpha mask, cutoff, dissolve, dither, distance fade | source shader semantics | late NDMF | must include every used reachable value | animation/material writer | **MODEL if bounded; otherwise REFUSE** | shader-specific | no |
| Exact continuous geometry/UV alpha result | pure classifier | after immutable source extraction | yes | none if inputs remain authoritative | **ORDER** | existing contract/tests | no |
| Separation candidate membership | immutable planner | after classification | yes | later target rejection may abandon only | **ORDER** | existing contract/tests | no |
| lilToon package/template/include/provenance | source attestation | late NDMF | coherent snapshot required | source/package mutation | **MODEL/REFUSE** | exact-version source and existing attestation | no |
| Base opaque pass and `LIL_RENDER == 0` after callback 100 | exact template/generator identity | derivable at late NDMF | semantic projection is immutable | callback 100 | **MODEL** | exact-version source | no |
| Target surviving-fragment alpha equals one | exact normal forward code | derivable at late NDMF | yes for admitted tuple | callback feature generation | **MODEL** | exact-version source | no |
| Target `_Invisible` inertness | target reachable values | late NDMF | requires immutable reachability | animation/material writer | **MODEL/REFUSE** | exact-version source | no |
| Target ID-mask inertness | compiled feature plus target flags | late NDMF | requires immutable reachability | callback feature activation/animation | **MODEL/REFUSE** | exact-version source; newly identified gap | no |
| Target UDIM inertness | compiled feature plus all row masks | late NDMF | requires immutable reachability | callback feature activation/animation | **MODEL/REFUSE** | exact-version source; newly strengthened gap | no |
| Target cull/deformation/clipping/pass equivalence | source and prepared-target state | late NDMF | requires exact construction/equality | callback or material writer | **MODEL/REFUSE** | required transformation invariant | no |
| Integration closure cannot override coverage | exact external closure | late NDMF | coherent snapshot required | integration source/macro state | **MODEL after review; otherwise REFUSE** | current eight-state matrix is necessary but not sufficient | no |
| Build-attempt identity | host lifecycle | previously before mutation | unavailable in SDK `3.10.4` | invocation ambiguity | **IRRELEVANT** to NDMF-complete path | inference from removed late dependency | no |

## Counterexamples and refusal boundaries

The investigation actively tested the desired conclusion against future-dependent candidates.

| Candidate counterexample | Finding | Classification |
|---|---|---|
| Callback-generated pass selected only after NDMF | Base opaque pass and `LIL_RENDER` are derivable from exact template/generator identity | **MODEL** |
| Feature stripping changes generated source | It changes text and compiled features, but the admitted target projection includes every coverage-relevant activation/value | **MODEL** |
| Generator skip versus run | Both branches share the exact projected opaque theorem when current source is attested | **IRRELEVANT/MODEL** |
| `_MainTex`, `_Color.a`, alpha mask, dissolve, cutoff, or dither changes target alpha | Intermediate alpha can change, but exact opaque branch overwrites surviving-fragment alpha and excludes cutout/transparent termination | **IRRELEVANT to target theorem only** |
| ID-mask suppression | Independent vertex-removal path not covered by current production alpha gates | **MODEL inert flags or REFUSE** |
| Pixel-mode UDIM with `_UDIMDiscardCompile == 0` | Fragment call site can still use row-mask predicate when feature is compiled | **MODEL all row masks or REFUSE** |
| Clipping canceller changes near-plane coverage | Not alpha termination; must be preserved/modelled with source-target state | **MODEL/REFUSE** |
| Animation changes target coverage gate | Current extractor is incomplete | **MODEL only when bounded; otherwise REFUSE** |
| Callback substitutes a multi material | Outside base target and `SetupMultiMaterial` can mutate it | **REFUSE** |
| Official integration changes preprocessor hooks | Existing matrix proves closure identity/composability, not automatically strengthened coverage closure | **MODEL after exact review; otherwise REFUSE** |
| Custom/derivative shader injects coverage behavior | Distinct identity or source closure; no base-theorem inheritance | **REFUSE** |
| SDK network IDs | Cannot affect the alpha/coverage proof | **IRRELEVANT** |
| EditorOnly removal | May eliminate output but cannot make an opaque classification unsafe | **IRRELEVANT** to semantic false positive |
| Unknown later mesh/material/texture writer | No proof of ordering, model, or irrelevance | **REFUSE** |
| Hidden arbitrary generator state | No hidden state found that can alter the admitted projection while all exact implementation, closure, feature, and value evidence remains equal; unknown additions refuse | **MODEL/REFUSE** |

The counterexamples narrow positive coverage and point to required future implementation tests. None of them forces later observation when refusal stays allowed.

## Supported official integration states

The approved official integration matrix characterized the eight selected package states formed by the supported VRC Light Volumes, AudioLink, and LTCGI activation combinations. Its evidence established:

- Exact selected closures are source-composable.
- Activation shows through exact package, macro, structure, and closure evidence.
- Standalone and LTCGI-selected families produce distinct canonical evidence where expected.
- Missing, extra, mismatched, or locally modified closure state causes refusal.

That matrix stays evidence that callback generation is deterministic from exact integration inputs. The strengthened target coverage theorem adds another admission condition: the external closure must pass review to show it cannot override fragment/vertex coverage hooks relevant to the base opaque target. Until that review exists for a selected profile, it stays refused. No arbitrary generator or integration generalization follows.

## Apply-on-Play implication

The previous conditional architecture made future-dependent positive mutation upload-only, because Apply-on-Play does not honor the same late SDK refusal result.

An NDMF-complete transformation needs no later AMUSE refusal. Its proof and mutation stay self-contained within the NDMF invocation, so this particular upload-specific restriction disappears naturally.

This is an architectural implication, not an implementation or certification claim. Apply-on-Play activation, idempotence, source safety, preview behavior, diagnostics, and UX stay future work.

## Build-attempt identity implication

The targeted path becomes:

```text
complete proof -> mutation -> no later AMUSE authorization dependency
```

It therefore no longer needs cross-callback association between a high-level SDK request, the NDMF clone, and a later validation callback. The SDK `3.10.4` build-attempt identity failure stays relevant only to a future transformation whose correctness demonstrably depends on authoritative later evidence.

This investigation does not design or revive the missing identity mechanism.

## DAO precedent

d4rkAvatarOptimizer runs automatic optimization from one SDK preprocess callback. Its `Optimize()` workflow clears caches, scans shaders when required, analyzes animation clips and material swaps, merges compatible meshes and materials, creates textures and optimized materials, rewrites animations, and then removes its optimizer component.

Relevant precedent:

- Gather animation and material-swap state before mutation.
- Make decisions and create replacement assets in one workflow.
- Refuse individual shader/material merges when parsing or compatibility checks fail.
- Preserve diagnostics that explain why a merge did not occur.

DAO also exposes user-selected tradeoffs and relies on its own shader parser and final-enough-state assumptions. Those do not meet AMUSE's exact proof standard. The transferable lesson is workflow shape and conservative local rejection, not authorization by precedent.

## AAO precedent

Avatar Optimizer deliberately places its main workflow in NDMF `Optimizing` and uses a late-sorting plugin identity. Its sequence parses animator state before it gathers shader/material information. Material-property queries return unknown when relevant animation exists. Its texture-atlas path refuses when material assignment, UV use, UV transform, renderer ownership, texture type, or shader information is incomplete.

AAO's lilToon `ShaderInformation` registers version-bounded, GUID-based semantic information rather than reproducing lilToon generated text. This stands as direct precedent for using a narrow semantic projection and refusing unknown inputs.

AMUSE must strengthen the transferable idea with:

- Exact source/template/include and integration closure identity.
- Transformation-specific semantic theorems.
- Eager immutable proof inputs.
- Exact source/equivalence proof.
- Fail-closed version, feature, animation, and later-actor handling.

AAO does not establish AMUSE safety, and this investigation did not test it in combination with AMUSE.

## Explicit coexistence boundary

This investigation did not determine:

- Whether DAO should run before or after AMUSE.
- Whether AMUSE should run before or after AAO.
- How VRCFury or Modular Avatar compose with AMUSE.
- Whether arbitrary NDMF plugin order is safe.
- Which plugin-specific contracts or APIs AMUSE should expose.
- Whether the real installed callback/plugin remainder is fully bounded.

Those questions are reserved for `investigate/coexisting-optimizer-lifecycle`. Any unknown third-party semantic writer stays a refusal in the present theorem.

## Remaining future-dependent facts

For the admitted targeted alpha profile, no proof fact needs later observation after conservative refusal applies.

The following future transformation classes could still be genuinely future-dependent:

- Correctness needs exact post-callback generated text rather than a proved semantic projection.
- A later operation performs material substitution whose result cannot be derived earlier.
- A supported later writer consumes hidden input unavailable during NDMF.
- A host conversion makes a proof-relevant texture, mesh, or material fact authoritative only later.
- The required real-pipeline remainder cannot be bounded through order, model, irrelevance, or refusal.

Conditional authorization stays conceptually available only for such a class, once the dependency is proved. It should not become the normal architecture merely because a later callback exists.

## Outcome

**Outcome A — single-stage is sufficient for the targeted architecture.**

The exact conclusion is:

> The targeted alpha transformation is semantically NDMF-complete once every remaining proof-relevant later actor is ordered before AMUSE, modeled, proven irrelevant, or causes conservative refusal.

Technically, exact lilToon implementation and closure identity, plus exact target inputs, support a small deterministic alpha-and-coverage projection of callback 100. Source geometry eligibility and target opacity are separate proofs. The target proof does not need generated shader text, but it does need strengthened coverage gates for `_Invisible`, ID-mask suppression, UDIM row masks, and preservation or modeling of other coverage behavior. Ambiguity causes refusal before mutation.

In plain language: AMUSE does not need to wait for lilToon to emit thousands of shader lines to learn the few exact facts this conversion needs. It must first prove that the original triangle was visually safe to convert. It must then prove that the exact prepared opaque target will not introduce alpha or coverage changes. If either proof is incomplete, AMUSE leaves the triangle alone.

## Architecture impact

A separate approved architecture revision should reconsider the following assumptions from the upload-conditional design:

- Later generated-source observation is always required for positive lilToon proof.
- Every positive alpha transformation needs an SDK commit gate.
- Upload-only execution is inherent to the targeted alpha path.
- `HostLifecycleCapability` and build-attempt identity are normal prerequisites for alpha mutation.
- Exact future shader text is the necessary proof authority, rather than exact implementation identity plus a reviewed semantic projection.
- Conditional authorization is the default, instead of an exceptional capability for proved future dependencies.

This investigation does not edit or supersede that prior design. It supplies evidence for a later revision decision.

## Implementation consequences, not an implementation plan

Future implementation cannot claim this Outcome A merely by moving existing calls into one NDMF pass. Before positive mutation, it must at minimum establish:

- Eager immutable renderer, mesh, material, texture, and source evidence.
- The complete source/equivalence proof for every candidate triangle.
- A bounded reachable-state rule for material swaps and proof-relevant animation, with refusal outside it.
- Exact prepared-target construction and identity.
- Strengthened opaque-target coverage closure, including ID-mask and all UDIM row masks.
- Exact coverage handling for any admitted integration closure.
- A characterized later-actor envelope, or refusal when this record cannot establish one.
- One immutable combined decision before the first mutation.
- Nondestructive NDMF-owned mutation and actionable refusal diagnostics.

This list records proof prerequisites only. It deliberately defines no production classes, interfaces, registries, or plan artifacts.

## Follow-up input for `investigate/coexisting-optimizer-lifecycle`

The next investigation should consume, not reopen, the lilToon semantic result. It should answer:

- How does AMUSE establish the bounded remainder of the actual NDMF and SDK pipeline?
- Which external transformations can be ordered before AMUSE?
- Which later behaviors can be modeled or proved irrelevant?
- When do installed but uncharacterized tools require candidate refusal?
- How do NDMF constraints and extension/dependency declarations expose, or fail to expose, those guarantees?
- How do mesh, UV, material, animation, and generated-asset ownership compose across optimizers?
- Is any narrow inter-plugin contract justified by concrete coexistence evidence?

This record starts no coexistence branch.

## Validation performed

Investigation validation consisted of:

- Branch, base, status, and ancestry checks.
- Complete review of the required merged AMUSE lifecycle, attestation, matrix, snapshot, authorization, boundary, and SDK records.
- Current production call-path inspection, from renderer extraction through immutable separation planning.
- Exact NDMF `1.14.4` phase and callback source inspection.
- Exact lilToon `2.3.4` callback, generator, template, input optimizer, vertex, fragment, alpha, ID-mask, UDIM, and integration-activation source inspection.
- Targeted enumeration of every `clip`, `discard`, empty/NaN vertex-output, opaque render-mode, and coverage-feature site in the admitted source closure.
- DAO and AAO source-only precedent inspection at the exact revisions recorded above.
- Comparison against the existing callback probe and official eight-state integration evidence.
- Working-tree review to confirm that only this durable investigation record was created.

This investigation needed and ran no production tests, because production code did not change. It ran no new Unity probe, because exact source inspection answered the coverage question and existing probes already established callback generation and integration-state behavior. It caused no Census Lab, private avatar, DAO/AAO installation, upload, or persistent external project mutation.

## Recommended next branch

After review and merge of this record, the recommended next branch is:

`investigate/coexisting-optimizer-lifecycle`

It should start from the then-current `origin/main` and preserve the present single-stage theorem as a bounded input. This record should not start it automatically from this branch.
