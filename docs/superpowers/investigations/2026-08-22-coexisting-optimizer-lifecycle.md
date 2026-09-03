# AMUSE coexisting-optimizer lifecycle investigation

## Status and scope

**Preliminary Outcome B approved. Durable investigation record awaiting review.**

This document records an architectural investigation. It does not implement an NDMF plugin, AMUSE alpha mutation, DAO cooperation, a plugin adapter, or a callback registry. It does not implement a generalized interoperability framework.

- Branch: `investigate/coexisting-optimizer-lifecycle`
- Base: `origin/main` at `2d0913be2b7c072c891a66bf7267856432fc4f78`
- Required predecessor: the merged [single-stage optimization lifecycle investigation](2026-08-22-single-stage-optimization-lifecycle.md) is present on the base
- Census Lab/private avatars: not used or modified
- Production code: not modified
- External tools: inspected at exact public source revisions. Not modified
- Unity probes: the investigation required no new probe. Exact source distinguished the lifecycle and structural-opportunity hypotheses
- Performance evidence: source-derived structural projections, not runtime benchmarks

The primary result is **Outcome B — state-based composition plus narrow cooperation**.

State-based composition remains the default interoperability model. Modular Avatar, VRCFury, AAO, and completed prior transformations normally leave ordinary Unity state for AMUSE to analyze without tool-specific provenance. DAO is the one demonstrated v1 case where a narrow bilateral contract helps. It can retain materially more safe optimization than ordinary state and incidental callback order give.

The decision hierarchy is:

1. **Safest minimum:** Candidate A — authoritative AMUSE-before-DAO order plus protected-target no-rewrite behavior.
2. **Recommended v1:** restricted Candidate B — the same authoritative order plus a fixed, versioned opaque-target preservation profile. Candidate A stays the mandatory fallback.
3. **Longer term:** Candidate C fused planning/execution only if future profiling shows material costs that Candidates A and B cannot recover.

Candidate B is the **highest-performing realistically supportable v1 contract in the architectural and optimization-opportunity sense**. This record does not yet include a measured VRChat runtime benchmark.

## Decision summary

The preferred broad lifecycle is:

```text
prior generators and optimizers
    -> ordinary effective Unity state
    -> AMUSE semantic barrier
    -> eager immutable extraction
    -> interpretation, proof, and planning
    -> authorized AMUSE mutation
    -> cooperating DAO optimization where supported
    -> bounded, modeled, irrelevant, or refused remainder
```

The binding single-stage result remains:

> The targeted alpha transformation is semantically NDMF-complete once every remaining proof-relevant later actor is ordered before AMUSE, modeled, proven irrelevant, or causes conservative refusal.

This investigation does not reopen later lilToon callback-100 observation, generated-shader observation, or a late AMUSE SDK commit gate. It also does not reopen cross-callback build-attempt identity for the targeted alpha transformation.

The strongest discovered broad semantic-barrier candidate is the start of NDMF `PlatformFinish`. Every NDMF `Optimizing` plugin necessarily completes before it. It is not the selected production mutation phase. NDMF describes `PlatformFinish` as platform-specific cleanup and validation territory and `Optimizing` as optimization territory. A subsequent design must choose between deliberate `PlatformFinish` mutation and an authoritative final-`Optimizing` ordering strategy.

DAO requires a separate ordering guarantee:

> When both DAO and AMUSE are active in the supported configuration, AMUSE must complete its authorized transformation before DAO begins any cooperating post-AMUSE optimization.

This investigation deliberately does not choose the mechanism for that guarantee. Options include a DAO NDMF participant, a DAO hook, an AMUSE bridge, SDK callback restructuring, or another narrow mechanism.

## Question

Can AMUSE establish a sufficiently authoritative effective-avatar state before proof? Can it bound the remainder after mutation in realistic projects? Those projects contain DAO, AAO, VRCFury, Modular Avatar, NDMF, lilToon, and unknown add-ons.

More specifically:

- which preceding operations AMUSE can treat as ordinary state producers.
- which later operations AMUSE must order, model, prove irrelevant, or refuse.
- whether DAO should normally run before or after AMUSE.
- what DAO Full opportunities are lost under blanket no-rewrite protection.
- whether a fixed semantic-preservation profile can safely recover a useful subset.
- whether the evidence justifies fused DAO-AMUSE execution for v1.
- whether any cooperation burden must extend beyond DAO and AMUSE.

The decision criteria, in order, are:

1. Correctness and proof safety.
2. Final avatar optimization quality.
3. Automatic user experience.
4. Ecosystem compatibility.
5. Maintenance and coupling burden.

Unknown or unsupported evidence never increases optimization aggressiveness.

## Binding inputs

This investigation consumes the following approved findings without reopening them:

- source/equivalence proof and opaque-target proof are separate obligations.
- exact generated lilToon shader text is unnecessary for the targeted path.
- a narrow exact-version alpha/coverage projection represents callback 100.
- target closure includes `_Invisible`, ID-mask suppression, all relevant UDIM row masks, and preservation or modeling of other coverage behavior.
- that targeted path requires no later AMUSE SDK authorization.
- only `ProvenOpaque` triangles may move to an opaque counterpart.
- ambiguous animation, material substitution, shader closure, feature activation, or later mutation causes refusal.

## Non-goals

This investigation does not:

- implement production compatibility code.
- implement DAO-AMUSE ordering or semantic-preservation cooperation.
- modify DAO, AAO, VRCFury, Modular Avatar, NDMF, or lilToon.
- implement an opaque-target creator or mesh mutation executor.
- implement a complete animation/material-swap reachability extractor.
- create a generalized plugin interoperability interface.
- create a callback registry, global planner, mutation DSL, or provider framework.
- select `PlatformFinish` as the production phase.
- select the DAO ordering mechanism.
- certify arbitrary DAO, AAO, VRCFury, Modular Avatar, shader, or NDMF versions.
- certify DAO Basic as generically semantics-preserving.
- benchmark measured VRChat runtime performance.
- revise `docs/architecture/vision.md` or earlier architecture specifications.
- start `design/coexisting-optimizer-lifecycle`.
- use the Census Lab, private avatars, or uploads.

## Exact environment

| Component | Exact identity | Evidence |
|---|---|---|
| Repository base | `2d0913be2b7c072c891a66bf7267856432fc4f78` | branch creation and Git ancestry |
| Unity | `2022.3.22f1`, revision `887be4894c44` | `ProjectSettings/ProjectVersion.txt` |
| NDMF | `1.14.4`, upstream `7cf8a13444ac19e46ac2b4146bad209de15dc42d` | embedded package and prior exact pin |
| VRChat SDK Base/Avatars | `3.10.4` | prior exact-package lifecycle investigation |
| lilToon | `2.3.4`, upstream `252fd8cfc46106d4967e95b3f2c788418502f227` | binding single-stage record |
| d4rkAvatarOptimizer | `4.6.0`, upstream `b2e500869610f7eea7645c8384eaecdf00167be4` | exact public source checkout on 2026-08-22 |
| Avatar Optimizer | `1.9.18-beta.1`, upstream `6e6babc53c4086e7b1038b50dc01b1e36f065ef1` | exact public source checkout on 2026-08-22 |
| Modular Avatar | `1.18.3`, upstream `f8c5fd98463e1024cae0608d5449b3c1fb6b6c84` | exact public source checkout on 2026-08-22 |
| VRCFury | package source reports placeholder `0.0.0`; upstream `dd7b8c9b538f1ddbb8ed2b1c6060094b5103816f` | exact public source checkout on 2026-08-22 |

The VRCFury revision, not the placeholder source-package version, is the authoritative identity for this characterization.

## Evidence hierarchy

- **Existing AMUSE contract:** behavior established by merged AMUSE code or an approved durable record.
- **Exact-version source fact:** behavior enforced by the exact pinned source. It is not generalized to another version.
- **Source-derived projection:** a structural outcome derived from exact control flow and an explicitly defined synthetic fixture. It is not empirical runtime evidence.
- **Empirical evidence:** behavior observed through a controlled exact-version probe. This investigation created no new empirical evidence.
- **Inference:** a conclusion derived from source facts and identified as such.
- **Unknown:** insufficiently established evidence. It cannot authorize mutation.

External-tool precedent is never promoted into AMUSE proof merely because the tool intends semantic preservation.

## Sources inspected

### Existing AMUSE records and implementation

- [single-stage optimization lifecycle](2026-08-22-single-stage-optimization-lifecycle.md)
- [SDK build-environment contract](2026-08-22-sdk-build-environment-contract.md)
- [analysis snapshot and ordering](../specs/2026-08-21-analysis-snapshot-ordering-design.md)
- [lilToon build-callback handoff](../specs/2026-08-21-liltoon-build-callback-handoff-design.md)
- [general-purpose transformation boundary audit](../audits/2026-08-22-general-purpose-transformation-boundaries.md)
- [separation-plan design](../specs/2026-08-15-separation-plan-design.md)

This investigation inspected the current AMUSE repository from Unity renderer extraction through immutable mesh-separation planning. It currently analyzes and plans. It has no NDMF plugin, opaque-target creator, mesh/material mutation executor, or complete animation/material-swap reachability extractor.

### Exact external source

The investigation inspected:

- NDMF phases, ordering constraints, plugin identity, VRChat preprocess hooks, `BuildContext` lifetime, and `PlatformFinish` passes.
- [DAO build callback selection](https://github.com/d4rkc0d3r/d4rkAvatarOptimizer/blob/b2e500869610f7eea7645c8384eaecdf00167be4/Editor/AvatarBuildHook.cs), [main optimizer](https://github.com/d4rkc0d3r/d4rkAvatarOptimizer/blob/b2e500869610f7eea7645c8384eaecdf00167be4/Editor/d4rkAvatarOptimizer.cs), [shader analyzer and transformer](https://github.com/d4rkc0d3r/d4rkAvatarOptimizer/blob/b2e500869610f7eea7645c8384eaecdf00167be4/Editor/ShaderAnalyzer.cs), [shader-author contract](https://github.com/d4rkc0d3r/d4rkAvatarOptimizer/blob/b2e500869610f7eea7645c8384eaecdf00167be4/Documentation~/ForShaderAuthors.md), and [documented preset tradeoffs](https://github.com/d4rkc0d3r/d4rkAvatarOptimizer/blob/b2e500869610f7eea7645c8384eaecdf00167be4/Documentation~/ForAdvancedUsers.md).
- [AAO NDMF plugin definition](https://github.com/anatawa12/AvatarOptimizer/blob/6e6babc53c4086e7b1038b50dc01b1e36f065ef1/Editor/OptimizerPlugin.cs) and [component-information API](https://github.com/anatawa12/AvatarOptimizer/blob/6e6babc53c4086e7b1038b50dc01b1e36f065ef1/API-Editor/ComponentInformation.cs).
- [Modular Avatar plugin definition](https://github.com/bdunderscore/modular-avatar/blob/f8c5fd98463e1024cae0608d5449b3c1fb6b6c84/Editor/PluginDefinition/PluginDefinition.cs) and editor-only cleanup behavior.
- [VRCFury main preprocess hook](https://github.com/VRCFury/VRCFury/blob/dd7b8c9b538f1ddbb8ed2b1c6060094b5103816f/com.vrcfury.vrcfury/Editor-Avatars/Hooks/VrcPreuploadHook.cs) and [late parameter compressor](https://github.com/VRCFury/VRCFury/blob/dd7b8c9b538f1ddbb8ed2b1c6060094b5103816f/com.vrcfury.vrcfury/Editor-Avatars/Hooks/ParameterCompressorHook.cs).

## Reconstructed build timeline

This section reconstructs the exact pinned pipeline as follows.

### 1. NDMF early SDK hook at `-11000`

NDMF creates its `BuildContext` and executes its early phases through `Transforming`.

Modular Avatar performs its principal Resolving and Transforming work inside this interval. It generates or rewrites hierarchy, armature, meshes and bones, controllers and clips, menus and parameters, and material setters and swaps. It also generates or rewrites reactive components and related ordinary Unity state.

### 2. VRCFury main SDK hook at `-10000`

The pinned VRCFury hook executes after NDMF Transforming and before NDMF Optimizing. It can generate controllers, clips, material-property curves and swaps, hierarchy, components, materials, and other avatar state.

### 3. NDMF optimize SDK hook at `-1025`

NDMF resumes the existing `BuildContext`, runs `Optimizing`, then `PlatformFinish`, then finishes the context.

AAO performs its main mutation in `Optimizing`. Modular Avatar also performs later optimization/garbage-collection work there. Their completed results are therefore available before the candidate AMUSE `PlatformFinish` barrier.

### 4. Candidate AMUSE semantic barrier

The beginning of NDMF `PlatformFinish` is the strongest discovered broad semantic-barrier candidate:

```text
MA Resolving/Transforming
    -> VRCFury main generation
    -> AAO and all other NDMF Optimizing work
    -> =====================================
       candidate AMUSE PlatformFinish barrier
       =====================================
    -> bounded SDK remainder
```

This candidate orders every NDMF `Optimizing` plugin before AMUSE without enumerating plugin identities. It does not automatically bound other `PlatformFinish` passes or later SDK callbacks.

It also does not settle the final phase choice. A final `Optimizing` pass with authoritative explicit constraints may better respect NDMF phase intent. The design branch must choose.

### 5. DAO callback

DAO selects its callback numerically based on whether Modular Avatar is available:

- without Modular Avatar: `-1025`, equal to the NDMF optimize hook.
- with Modular Avatar: `-15`, after NDMF completion.

Equal numeric callback order is not an acceptable proof contract. The ordering of equal callbacks depends on discovery and stable sorting behavior that is not a public semantic guarantee.

The presence of Modular Avatar produces a useful observed ordering. But AMUSE cannot make MA installation a correctness requirement for DAO coexistence.

### 6. Later exact actors

- lilToon callback `100` remains MODEL under the approved single-stage theorem.
- The pinned VRCFury parameter compressor runs near `int.MaxValue - 100`.
- editor-only component cleanup and terminal helper callbacks run near `int.MaxValue`.

The VRCFury compressor modifies expression/controller parameter state and adds compression logic to the Action controller. It is IRRELEVANT only to the current targeted alpha theorem. Reason: the pinned code does not add or change the mesh, material, clip, or material-property bindings covered by that proof. This does not generalize to future AMUSE semantic domains or future VRCFury revisions.

## State-based composition

The default interoperability rule is:

```text
tool performs transformation
    -> transformation completes
    -> ordinary mesh/material/controller/hierarchy state remains
    -> AMUSE interprets that state
    -> producer identity is unnecessary
```

AMUSE needs tool provenance only when:

- the resulting state does not represent the required semantics.
- the prior tool retained hidden state that controls a future proof-relevant write.
- the relevant operation has not completed before the snapshot.
- the tools require explicit ordering.
- a versioned cooperation contract supplies proof authority unavailable from ordinary state.

An unknown producer before the barrier is not automatically unsafe. If it completed and AMUSE completely supports the resulting state, its history is irrelevant. After the barrier, an unknown proof-relevant writer is unsafe for the affected candidates. AMUSE must order it, model it, prove it irrelevant, or refuse it.

## External-actor authority map

| Actor/pass | Exact identity | Relative order to candidate barrier | Proof-relevant domains | Result observable? | Provenance needed? | Class | Evidence | Candidate consequence |
|---|---|---|---|---|---|---|---|---|
| Modular Avatar Resolving/Transforming | `1.18.3`, `f8c5fd9...` | before | hierarchy, mesh/bones, controllers, clips, materials/swaps | yes | no | ORDER | exact source | analyze generated ordinary state |
| Modular Avatar Optimizing cleanup | same | before | hierarchy/component deletion | yes | no | ORDER | exact source | removed objects cease to be candidates |
| VRCFury main generation | `dd7b8c9...` | before | controller, clips, material properties/swaps, hierarchy, generated assets | yes | no | ORDER | exact source | analyze final reachable states; incomplete reachability refuses |
| AAO optimizer | `1.9.18-beta.1`, `6e6babc...` | before under candidate barrier | meshes, materials, slots, controllers, hierarchy, textures | yes | no after completion | ORDER | exact source | consume AAO output |
| Unknown completed NDMF optimizer | exact identity unknown | before by phase | any ordinary supported domain | yes when supported | no | ORDER/REFUSE | phase fact plus semantic support | identity unnecessary; unsupported state refuses |
| AMUSE target mutation | not implemented | at barrier | mesh/submesh/material target | planned authoritative write | AMUSE-owned | ORDER | future requirement | one immutable authorized decision before mutation |
| DAO Basic-compatible structural work | `4.6.0`, `b2e5008...` | must be after AMUSE when cooperating | renderer, mesh, slots, paths, controllers | result appears later | no history if contract bounds future work | ORDER/MODEL | exact source plus future contract | retain structural work under scoped conditions |
| DAO protected-material semantic rewrite | same | after AMUSE | shader, material, textures, UV0.z MaterialID, animation | appears too late for prior proof | bilateral postcondition required | MODEL/REFUSE | future exact-version contract | admitted Candidate-B subset or Candidate-A fallback |
| DAO Shader Toggles on protected geometry | same | after AMUSE | vertex coverage, mesh visibility animation, material properties | appears later | separate theorem required | REFUSE for v1 Candidate B | exact source | Candidate-A fallback for affected geometry |
| lilToon callback 100 | `2.3.4`, `252fd8c...` | after | target shader realization | modeled | no | MODEL | binding prior record | exact target projection applies |
| SDK editor-only object removal | SDK `3.10.4` | after | hierarchy deletion | yes afterward | no | IRRELEVANT to false-positive theorem | binding prior record | possible opportunity loss only |
| VRCFury parameter compressor | `dd7b8c9...` | after | expression/controller parameters | yes | no for scoped theorem | IRRELEVANT only to current target | exact source | no covered binding change at pinned revision |
| Unknown later proof-relevant writer | unknown | after | unknown | insufficiently bounded | possibly | REFUSE | unknown | candidate/domain-specific refusal |

The key questions are whether AMUSE can see the effective result and whether it needs to know who produced it. For MA, VRCFury, and AAO, current evidence answers yes and no respectively, subject to AMUSE supporting the resulting semantics. The cooperating DAO future write is the demonstrated exception.

## Modular Avatar coexistence

Modular Avatar is primarily a generator/transpiler rather than an optimizer competitor for this scope. Its principal outputs become ordinary hierarchy, mesh, animation, controller, menu, parameter, and material state before the candidate AMUSE barrier.

Its later cleanup removes construction components after their effects are realized. AMUSE does not need those components or MA provenance if the resulting state is semantically supported.

Current evidence does not justify an MA-specific AMUSE contract.

MA does affect the current DAO callback selection, but that incidental relationship is not promoted into the AMUSE ordering contract.

## VRCFury coexistence

The main VRCFury proof-relevant generation completes before NDMF Optimizing. Its resulting controllers, clips, material-property bindings, swaps, hierarchy, and generated assets are ordinary Unity state.

AMUSE must eventually extract complete supported material-state reachability from that state. Current AMUSE does not have a complete animation/material-swap extractor, so unsupported reachability remains a refusal. That missing general capability does not justify a VRCFury-specific adapter.

Current evidence does not justify a VRCFury-specific AMUSE contract.

The late parameter-compressor conclusion is deliberately narrow. It is irrelevant only to the current targeted alpha theorem at the pinned revision. Reason: it does not alter the covered mesh/material/clip/material-property bindings. Future AMUSE semantics involving expression parameter behavior must characterize it separately.

## AAO coexistence

AAO performs the majority of its mutation in NDMF `Optimizing`. That work includes mesh edits, skinned-mesh and material-slot merging, object/component sweeps, material/property/texture cleanup, animation mappings, and animator graph optimization.

AAO exposes the qualified plugin identity `com.anatawa12.avatar-optimizer`. The standard NDMF plugin-ordering mechanism can target it when an explicit edge is needed. Its internal late type-name placement is implementation behavior, not the preferred AMUSE proof authority.

For v1 compatibility, AAO should finish before AMUSE. AMUSE can then consume the resulting AAO mesh, material, controller, and hierarchy state. This may miss an opportunity for AAO to optimize AMUSE-added slots, but it avoids proving the later AAO original-state-dependent transformations.

The AAO component-information and shader-information APIs are useful ecosystem precedent. Current evidence does not show an information deficit requiring an AAO-AMUSE contract.

Current evidence does not justify an AAO-specific AMUSE contract.

## DAO transformation and write-domain map

The DAO `Optimize()` workflow can perform the following operations, depending on settings and compatibility:

| Operation | Domains written | Ordinary final state? | Relevance to AMUSE |
|---|---|---|---|
| editor-only and unused-object cleanup | hierarchy/components | yes | deleted candidates disappear; ordering matters for opportunity |
| material deduplication | material references | yes | normally consumable |
| static-to-skinned conversion | renderer/mesh/bones | yes | geometry and renderer semantics must be supported |
| blendshape reachability and renderer deletion | mesh/renderer/controller | yes | completed output is consumable |
| skinned-renderer merging | renderer/mesh/bones/bindposes/bounds | yes | proof-sensitive geometry and culling facts must remain valid |
| Shader Toggles/NaNimation merging | mesh, hierarchy, shader coverage, animation | yes but introduces new semantics | separate proof required for protected geometry |
| material-slot consolidation | submesh indices/material bindings | yes | triangle-to-material mapping must remain authoritative |
| different-property material merging | shader/material/UV0.z | yes | Candidate-B profile required for protected targets |
| static property specialization | generated shader/material | yes | Candidate-B exact postcondition or fallback |
| texture-array construction | textures/material/shader/MaterialID | yes | allowed only when protected semantics are preserved |
| material-swap optimization | material mappings/animation | yes | swaps affected by protected target require exact reachability |
| animation/controller/path rewriting | controllers/clips/bindings/hierarchy | yes | must preserve every proof-relevant reachable state |
| final hierarchy cleanup | hierarchy/components | yes | completed result is observable |

DAO output is not intrinsically dependent on transformation history. Final shader, mesh, material, controller, and texture state encode the runtime result in principle. The current AMUSE implementation cannot interpret arbitrary DAO-generated shader and MaterialID semantics. So DAO-first Full output causes safe opportunity loss, not a requirement for a universal provenance record.

## DAO ordering comparison

### DAO before AMUSE

AMUSE would receive the completed DAO renderer, mesh, material, shader, texture-array, and controller output.

DAO Basic output may remain understandable as ordinary state under the scoped conditions below. DAO Full can generate custom shaders, MaterialID transport, texture arrays, and rewritten animation outside the current AMUSE semantic model. Current AMUSE must refuse affected alpha candidates.

This direction preserves DAO consolidation but loses AMUSE alpha opportunities. It also places AMUSE after the DAO SDK callback. That position sits outside the natural active NDMF mutation interval unless another host arrangement appears.

DAO-before-AMUSE is therefore a compatibility fallback, not the preferred primary coexistence lifecycle.

### AMUSE before DAO

DAO receives the geometry AMUSE separated, preserved transparent material role, and proof-sensitive opaque target.

Basic-compatible structural consolidation can remain useful. Full semantic rewriting can invalidate the theorem AMUSE used. DAO must supply an exact preservation guarantee or avoid rewriting the protected target.

This direction exposes the strongest combined opportunity:

- AMUSE retains safe opaque/transparent separation.
- DAO can still reduce renderer, mesh, material, and controller overhead.
- selected DAO Full transformations can remain available under Candidate B.
- unsupported operations fall back to Candidate A rather than invalidating the proof.

AMUSE-before-DAO is therefore the preferred direction, conditional on authoritative ordering.

## Stable ordering requirement

The exact requirement is:

> When both DAO and AMUSE are active in the supported configuration, AMUSE must complete its authorized transformation before DAO begins any cooperating post-AMUSE optimization.

Current callback numbers do not establish this generally:

- DAO without Modular Avatar uses `-1025`, equal to the NDMF optimize hook.
- DAO with Modular Avatar uses `-15`, after NDMF.

The contract must not depend on equal-callback discovery order or require Modular Avatar installation.

The implementation mechanism remains a design decision. This investigation does not choose among:

- DAO becoming or adding an NDMF participant.
- a DAO lifecycle hook.
- an AMUSE bridge.
- SDK callback restructuring.
- another narrow bilateral mechanism.

## DAO Basic is not generically semantics-preserving

DAO documents deliberate Basic-preset tradeoffs. AMUSE must classify them against the current targeted theorem rather than describe them as globally harmless.

| DAO Basic behavior | Current targeted-theorem classification | Required treatment |
|---|---|---|
| probe-anchor selection during renderer merging | alpha/coverage-irrelevant for the admitted opaque target, but potentially RGB-lighting-relevant | do not claim full appearance equivalence from the alpha theorem |
| root-bone selection during renderer merging | proof-relevant when it affects deformation, bounds, or culling | AMUSE must not perturb the selection inputs, or must model/refuse |
| arbitrary world/station animation ignored | proof-relevant if it can write renderer, transform, material, or coverage state | outside observable supported reachability; explicit unsupported/refusal boundary |
| unstable `SV_VertexID`/`SV_PrimitiveID` | irrelevant only when every ID-sensitive proof path is proven inert | otherwise refuse |
| vertex-attribute normalization | irrelevant when added/copied channels are unused by the proof | preserve/model any proof-relevant channel or refuse |
| UV and topology rewriting | proof-relevant when consumed by alpha/coverage or deformation analysis | preserve exact required components/mapping or refuse |

The scoped conclusion is:

> DAO Basic can be compatible after AMUSE for the admitted targeted alpha profile when AMUSE's mutation does not perturb DAO's renderer-selection inputs and every ID-, UV-, deformation-, animation-, and coverage-sensitive case is proven inert, modeled, or refused.

This is not blanket DAO Basic certification.

## Cooperation candidates

### Candidate A — no-rewrite protection

Candidate A consists of:

- authoritative AMUSE-before-DAO order.
- a protected target/material designation.
- DAO may retain transformations that do not semantically rewrite the protected target.
- DAO skips unsupported or uncertain protected-target operations.

Candidate A is the mandatory safe fallback.

The existing DAO shader comment `//d4rkAO:incompatible_shader` is useful precedent. Exact DAO documentation and source show that it disables:

- `Write Properties as Static Values`.
- Shader Toggles.
- `Merge Different Property Materials`.
- dependent texture-array and MaterialID paths.

All other optimizations remain eligible under the DAO own checks. The existing marker is shader-wide, however. It is therefore too coarse for a shared vendor lilToon shader or a single protected material. Candidate A records required behavior, not that exact implementation surface.

#### Candidate A opportunity loss

| Disabled Full capability | Opportunity intentionally lost on a protected target |
|---|---|
| static-value writing | no constant folding, exact feature specialization, or generated specialized shader |
| Shader Toggles | no consolidation of renderers whose visibility curves differ through `_IsActiveMeshN` |
| different-property material merging | compatible but distinct materials remain separate slots and draw roles |
| texture arrays | compatible distinct textures remain separate `Texture2D` assets |
| MaterialID transport | no UV0.z material selection or shader-side per-material value arrays |
| toggle-dependent mesh consolidation | a protected material can prevent the containing renderer from entering DAO's shader-toggle fallback |

Candidate A remains correct and low-complexity. It is not always performance-optimal.

### Candidate B — fixed semantic-preservation cooperation

Restricted Candidate B adds a fixed, versioned AMUSE opaque-target preservation profile.

DAO may perform only explicitly admitted transformations. AMUSE must establish preservation of that profile for the exact supported DAO, shader, and profile combination. If preservation cannot be established, DAO automatically uses Candidate A for the affected protected target.

Candidate B must not become an arbitrary predicate language or general plugin protocol. A fixed profile names one AMUSE theorem and its required postconditions.

#### Required protected postconditions

At minimum, the profile requires:

1. Every protected triangle remains mapped to its correct generated material or MaterialID branch.
2. The target remains in the required opaque render-state family, including relevant queue, pass, blend, depth-write, and tag behavior.
3. Every surviving admitted forward fragment continues to output alpha exactly one.
4. The implementation introduces no new unmodeled vertex, primitive, or fragment suppression path.
5. `_Invisible`, ID-mask, and UDIM suppression remain inert in every admitted reachable protected state.
6. Clipping-canceller, culling, deformation, topology, and pass behavior required by the theorem remain preserved or modeled.
7. Material animation and swaps cannot select a protected state that violates the profile.
8. UV components and geometry facts consumed by the proof remain preserved.
9. Uncertainty or unsupported state causes Candidate-A fallback.

These postconditions protect the AMUSE authorization theorem. They do not claim that every independent DAO tradeoff is erased.

#### Plausible Candidate-B operation subset

Exact DAO `4.6.0` source identifies the following potentially useful subset. It is **not production-certified by this investigation**.

| Operation | Plausible admission condition |
|---|---|
| exact-value constant specialization | the generated result preserves the profile's exact required value and reviewed control-flow consequence |
| specialization of proof-irrelevant properties | exact source establishes that the property cannot affect the scoped theorem |
| merging protected opaque materials | their proof-relevant states, render states, keywords, passes, and reachable values are identical |
| MaterialID representation of other differences | only state outside the proof-sensitive profile varies by MaterialID |
| compatible texture arrays | affected texture semantics are outside the theorem or explicitly preserved by it; DAO's size, format, mip, filter, wrap, and color-space checks pass |
| UV0.z MaterialID transport | required AMUSE and source semantics do not depend on overwritten UV components; relevant UV0.xy behavior remains preserved |
| Basic-compatible structural consolidation | the scoped root-bone, probe-anchor, ID, UV, deformation, animation, and coverage conditions above hold |

Production certification requires targeted source attestation and executable tests for the exact supported DAO/shader/profile path. An unknown DAO version, unsupported shader/profile, unsupported operation, failed attestation, or uncertain preservation receives no positive capability.

#### Why this does not require proving DAO's whole transformer

The admitted surface can remain bounded to:

- one exact AMUSE opaque-target theorem.
- one fixed preservation profile.
- exact supported DAO and shader revisions.
- a named subset of DAO operations.
- exact fail-closed postconditions.
- targeted tests of the generated paths.

Arbitrary shaders, arbitrary DAO transformations, user-defined semantic predicates, and unrelated plugins remain outside the contract.

The current source does not itself certify the profile. The DAO transformer parses and re-emits shader source, replaces constants, and changes structures and function signatures. It transports mesh/material IDs, creates arrays, and injects visibility checks. It may also alter pass or variant code. Candidate B therefore requires explicit bilateral ownership and exact-version evidence before production use.

### Shader Toggles are outside v1 Candidate B

DAO Shader Toggles introduce `_IsActiveMeshN`, mesh-ID selection, vertex-stage suppression, and controller/clip rewrites from renderer or GameObject visibility into material-property animation.

This is a new proof-relevant coverage mechanism after AMUSE. Supporting it safely requires a separate theorem covering:

- exact controller-curve mapping.
- default-state and Write Defaults behavior.
- mesh-ID and triangle-region mapping.
- generated shader suppression behavior.
- every admitted reachable visibility state.
- interactions with unobservable arbitrary world/station animation.

That is not a minor extension of the current opaque-target profile. Protected geometry requiring DAO Shader Toggles uses Candidate A in v1.

### Candidate C — fused planning and execution

Candidate C would let DAO consume AMUSE proof facts while creating its final merged mesh/material output. It could avoid some duplicate mesh work, intermediate generated state, or redundant mapping.

It would also require DAO and AMUSE to share:

- a versioned proof-fact schema.
- triangle and material-region identity.
- mutation and failure semantics.
- controller and material-reachability mapping.
- mesh planning and generated-asset ownership.
- compatibility maintenance across both products.

Opaque and transparent geometry still require different render-state behavior and normally distinct draw roles. Fused execution cannot erase that fundamental boundary merely by sharing a planner.

This investigation rejects Candidate C for v1. It remains only a possible longer-term direction. Profiling must show meaningful duplicated build work or runtime costs that Candidates A and B cannot recover.

## Source-derived performance fixtures

The following fixtures are analytical projections from exact DAO `4.6.0` control flow. They establish retained and lost structural opportunity. They are not Unity observations or measured runtime benchmarks.

### Fixture T — independently toggleable renderers

Fixture:

- four compatible skinned renderers.
- same bones, layer, shader, and shared material.
- 100 AMUSE-proven opaque triangles per renderer.
- distinct visibility animation on each renderer.
- manually represented AMUSE-like protected opaque target state.

| Arrangement | Final skinned renderers | Final slots | Distinct materials | Generated shader | Authorized opaque triangles | Refused candidates |
|---|---:|---:|---:|---:|---:|---:|
| Candidate A: AMUSE then DAO Full, no rewrite | 4 | 4 | 1 shared | 0 | 400 | 0 |
| unrestricted DAO rewrite after AMUSE, unsafe baseline | 1 | 1 | 1 generated | 1 | intended 400 but not authorized | 0 only by unsafe assumption |
| DAO Full then current AMUSE | 1 | 1 | 1 generated | 1 | 0 | 400 |
| restricted Candidate B v1 | 4 | 4 | 1 shared | 0 for protected target | 400 | 0 |

On this fixture, Candidate A and restricted Candidate B lose three renderer updates and three draw slots. Unrestricted Shader Toggles keep them. Candidate B deliberately does not recover that opportunity in v1 because visibility translation is a new coverage theorem.

### Fixture M — differing constants and textures

Fixture:

- one skinned renderer with four submeshes.
- four same-shader, same-render-queue materials.
- materials differ only in constant RGB properties and four compatible same-dimension `_MainTex` textures.
- 100 AMUSE-proven opaque triangles per submesh.
- no material swaps or proof-relevant animation.
- four protected opaque target roles preserve the appearance differences.

| Arrangement | Final skinned renderers | Final slots | Final materials | Texture representation | Generated shader | Opaque / transparent triangles |
|---|---:|---:|---:|---|---:|---:|
| Candidate A | 1 | 4 | 4 | four `Texture2D` assets | 0 for protected target | 400 / 0 |
| unrestricted DAO Full after AMUSE, unsafe baseline | 1 | 1 | 1 | one four-layer `Texture2DArray` | 1 | intended 400 / 0 but not authorized |
| DAO Full then current AMUSE | 1 | 1 | 1 | one four-layer `Texture2DArray` | 1 | 0 / 400 |
| restricted Candidate B after certification | 1 | 1 | 1 | one four-layer `Texture2DArray` | 1 attested path | 400 / 0 |

Candidate A loses three material/draw roles and compatible texture-array consolidation on this fixture. Restricted Candidate B can plausibly recover that opportunity while preserving the fixed target profile.

### Runtime implications and evidence limits

Reducing renderers can reduce skinning and renderer-update overhead. Reducing material/submesh slots can reduce draw calls. Texture arrays can enable one material to represent otherwise distinct texture selections. Static specialization can remove dynamic property and shader-feature work.

The AMUSE opaque split can retain a separate opaque draw role. But the opaque triangles can gain depth writes and early-Z/occlusion behavior, and they can avoid transparent sorting and overdraw behavior. Neither the fixture counts nor these known mechanisms establish a measured net VRChat runtime result.

This record invents no aggregate performance score. Real runtime effects depend on triangle distribution, screen coverage, material complexity, platform, driver, visibility, animation, and the surrounding avatar.

## State-only counterexamples

The investigation actively searched for cases where final ordinary state or current snapshot state is insufficient.

| Counterexample | Missing or late information | Correctness/opportunity effect | Treatment |
|---|---|---|---|
| DAO automatic/settings-driven Full behavior | future DAO mode may depend on editor/static configuration not represented on the avatar before DAO runs | later proof-sensitive rewrite | authoritative order plus Candidate A/B contract |
| DAO Full output before AMUSE | generated shader/MaterialID/texture-array semantics are not supported by current AMUSE | opportunity loss, not inherently unrecoverable semantics | REFUSE affected candidates |
| AAO original-state context if AAO remains later | AAO can retain context outside final avatar state | future mesh/material write cannot be predicted from snapshot alone | ORDER AAO before AMUSE |
| future material-swap/controller rewrite | reachable material state changes after proof | correctness | ORDER, MODEL, or REFUSE |
| Shader Toggles after AMUSE | new `_IsActiveMeshN` coverage and visibility rewrite | correctness | separate theorem; Candidate A fallback in v1 |
| MA construction-component removal | producer provenance disappears | no loss when final state is supported | ordinary state sufficient |
| VRCFury controller generation | semantics remain in controllers/clips, but current extraction is incomplete | conservative opportunity loss | build general extractor; no VRCFury adapter |
| unknown later callback | write domain not established | cannot authorize affected theorem | domain-specific REFUSE |

The distinction is important:

- information may be absent from final state.
- information may exist in final state but lack an AMUSE interpreter.
- information may describe a future operation not yet represented in state.

Only the first and third necessarily require ordering or cooperation. The second can remain a conservative unsupported case.

## Unknown-tool policy

### Unknown before AMUSE

An unknown preceding tool is acceptable when:

- its proof-relevant work completed.
- its resulting state is ordinary Unity state.
- AMUSE supports every semantic domain used by the candidate.
- no hidden future write remains.

Tool identity does not itself grant or remove capability.

### Unknown after AMUSE

AMUSE classifies a later unknown actor by domain where reliable evidence permits:

- a proven PhysBone-only write can be IRRELEVANT to the current alpha theorem.
- a controller-parameter-only write can be IRRELEVANT only if it cannot change covered bindings.
- an unknown mesh, material, shader, texture, animation, or property writer requires refusal for affected candidates.
- callback-name inference cannot replace the absence of a reliable write-domain contract.

Unknown does not automatically disable all AMUSE work. It also never authorizes a candidate. Refusal should be as candidate- and domain-specific as the evidence permits.

This evidence does not justify a generic callback registry.

## Combined realistic pipelines

| Environment | Characterized effective order | Result |
|---|---|---|
| AMUSE alone | AMUSE barrier then modeled/irrelevant remainder | supported architecture subject to implementation prerequisites |
| DAO and AMUSE | AMUSE then Candidate A/B DAO cooperation | primary v1 coexistence target |
| Modular Avatar, DAO, AMUSE | MA then AMUSE then DAO | tractable; DAO contract still required for Full protected targets |
| VRCFury and AMUSE | VRCFury then AMUSE | ordinary-state composition |
| Modular Avatar, VRCFury, AMUSE | MA then VRCFury then MA cleanup then AMUSE | ordinary-state composition |
| AAO and AMUSE | AAO then AMUSE | compatibility-first; maximal combined optimization deferred |
| DAO, AAO, AMUSE | AAO then AMUSE then DAO cooperation | source-compatible architecture; not empirically certified |
| DAO, VRCFury, Modular Avatar, AMUSE | MA then VRCFury then AMUSE then DAO cooperation | tractable architecture; not empirically certified |
| DAO and VRCFury without MA | DAO/NDMF `-1025` ambiguity | unsupported until authoritative order exists |

This record claims no exhaustive Cartesian version matrix.

## Minimum coexistence contract

The demonstrated v1 contract is bilateral and fail-closed.

### Shared requirements

1. Authoritative AMUSE-before-DAO ordering in supported combined operation.
2. Protected-target identification limited to the AMUSE opaque theorem.
3. Exact supported version/profile identity.
4. Candidate A no-rewrite behavior always available.
5. Candidate B operations admitted only through exact preservation evidence.
6. Automatic Candidate-A fallback on any unsupported or uncertain condition.
7. No responsibilities for unrelated plugins.

### Candidate A requirement

DAO must be able to leave a protected target semantically unrewritten. It must also retain independently safe structural work where its existing checks and the scoped Basic conditions permit.

### Candidate B requirement

DAO and AMUSE may recognize a fixed opaque-target preservation profile. DAO may apply an explicitly certified operation only when it guarantees the postconditions of that profile for the exact DAO/shader/profile combination. This investigation does not define the marker format, API, callback, data representation, or implementation ownership split.

### Fail-closed versioning

The following cannot gain positive capability:

- unknown DAO version.
- unsupported or modified shader closure.
- unknown profile version.
- operation outside the certified subset.
- uncertain property, texture, UV, animation, material-swap, or geometry preservation.
- unknown later proof-relevant writer.
- failed or missing targeted attestation.

Each case uses Candidate A when the ordering and no-rewrite guarantee remain available. Otherwise AMUSE refuses the affected candidate.

## Ecosystem burden

The v1 burden is intentionally narrow:

- **DAO and AMUSE** own the bilateral ordering and protected-profile contract.
- **AAO** receives no AMUSE-specific responsibility from current evidence.
- **VRCFury** receives no AMUSE-specific responsibility from current evidence.
- **Modular Avatar** receives no AMUSE-specific responsibility from current evidence.
- **lilToon** receives no new responsibility beyond the already-characterized exact target semantics.
- **unrelated Unity add-on authors** receive no new responsibility.

This is why Outcome B does not justify a generalized interoperability framework.

## Outcome

**Outcome B — State-based composition plus narrow cooperation.**

The exact architectural conclusion is:

> Prior generators and optimizers should normally finish and leave ordinary Unity state for AMUSE to analyze. DAO is the one demonstrated v1 case requiring authoritative AMUSE-before-DAO ordering and a narrow bilateral protected-target contract. Candidate A is the mandatory safe fallback. A restricted, fixed, versioned Candidate B is the recommended v1 target where exact preservation evidence admits selected DAO Full transformations.

Technically, MA, VRCFury, and AAO can be ordered before the strongest discovered broad barrier and consumed through their resulting state. DAO Full after AMUSE can rewrite shader, material, texture, UV, and animation semantics covered by the AMUSE authorization theorem. Blanket no-rewrite protection is safe but can lose material merging, texture arrays, static specialization, and toggle-dependent renderer consolidation. A fixed preservation profile can plausibly recover the first three for a bounded exact-version subset. Shader Toggles introduce a new coverage theorem and remain excluded for v1 protected geometry.

In plain language, AMUSE normally should not care which tool made the avatar state it sees. DAO is special only because the best combined result lets AMUSE prove and split first. DAO then optimizes that result. DAO may keep Full-mode work only under one condition. It must promise that the opaque result stays within the exact AMUSE safety proof. Otherwise it leaves that protected material alone.

## Architecture impact

A separate design branch should carry the following assumptions:

- state-based composition is the default, not per-tool adapters.
- the strongest discovered broad barrier is the beginning of `PlatformFinish`, but phase selection remains open.
- all NDMF `Optimizing` producers should finish before AMUSE under the selected design.
- DAO cooperating work must start only after authorized AMUSE mutation completes.
- equal numeric SDK callback order is not authoritative.
- Candidate A must exist independently of Candidate B.
- Candidate B must be a fixed profile, not a generalized semantic language.
- protected Shader Toggles require a separate theorem and are not v1 Candidate B.
- Candidate C is not justified without profiling evidence.
- unknown versions and unknown later writers remain fail-closed.

This investigation does not revise existing architecture specifications.

## Implementation consequences, not an implementation plan

Future production work cannot claim this Outcome B merely by choosing a callback number or adding a marker. Before positive combined operation, it must establish:

- the selected AMUSE semantic barrier and exact NDMF ordering behavior.
- complete eager immutable proof input.
- a nondestructive opaque-target and mesh mutation implementation.
- the authoritative AMUSE-before-DAO ordering guarantee.
- Candidate A protected-target behavior.
- a fixed Candidate-B preservation profile.
- exact supported DAO/shader/profile identities.
- targeted attestation and tests for each admitted Candidate-B operation.
- automatic Candidate-A fallback and actionable diagnostics.
- conservative refusal when neither Candidate A nor Candidate B is authoritative.
- no persistent mutation of source avatar assets.

This list records requirements only. It deliberately defines no classes, interfaces, schemas, APIs, callbacks, or implementation mechanism.

## Remaining unsupported combinations and risks

- DAO Full or Shader Toggles without an authoritative bilateral contract.
- DAO without Modular Avatar while its callback remains tied with NDMF at `-1025`.
- Shader Toggles on AMUSE-protected geometry in v1.
- arbitrary generated shaders outside the AMUSE semantic model.
- unknown or modified DAO, shader, or preservation-profile versions.
- unclassified proof-relevant `PlatformFinish` or later SDK writers.
- complete material-property-block and animation/material-swap reachability until implemented.
- AMUSE-before-AAO maximal combined optimization.
- arbitrary world/station animation that writes proof-relevant avatar state.
- multi-optimizer/version combinations not characterized by exact source or tests.
- runtime performance claims until representative VRChat measurements exist.

Each unsupported case preserves or refuses affected candidates rather than guessing.

## Probe decision

A disposable Unity fixture manually representing an AMUSE-like split would have been meaningful. The absence of a production AMUSE executor was not treated as a reason to reject probing.

This investigation ran no probe because exact DAO source directly distinguished the options:

- an incompatible shader skips optimized-material generation.
- different-property merging requires successful shader parsing.
- texture-array discovery skips an unparsed group.
- Shader Toggle renderer fallback requires compatible materials.
- generated Shader Toggles inject `_IsActiveMeshN` and vertex suppression.
- Full enables the relevant static, different-property, and `_MainTex` texture-array paths.

A fixture could show those branches but could not certify the semantic postconditions of Candidate B. That certification belongs with the future exact contract and executable target path.

This investigation created no disposable Unity assets or project state.

## Validation performed

Investigation validation consisted of:

- branch, base, status, log, and ancestry checks.
- review of the binding single-stage and SDK lifecycle records.
- current AMUSE analysis/planning capability inspection.
- exact NDMF `1.14.4` phase, constraint, hook, and `BuildContext` source inspection.
- exact DAO `4.6.0` callback, preset, mesh merge, material merge, texture-array, MaterialID, shader-transform, animation-rewrite, incompatibility, and documented-tradeoff inspection.
- exact AAO `1.9.18-beta.1` phase and API inspection.
- exact Modular Avatar `1.18.3` phase and cleanup inspection.
- exact VRCFury revision inspection of the main hook and late parameter compressor.
- reconstruction of the external-actor timeline and write-domain map.
- source-derived comparison of DAO-before-AMUSE, Candidate A, restricted Candidate B, and Candidate C.
- two synthetic counterfactual fixtures selected to exercise Shader Toggles and different-property/texture-array opportunities.
- line-by-line review against the approved preliminary findings.
- working-tree review to confirm that the work created only this durable investigation record.

Production code did not change, so this investigation required and ran no production tests. This investigation used no Unity probe, Census Lab, private avatar, upload, external project mutation, or runtime benchmark. It did not modify DAO, AAO, VRCFury, or Modular Avatar.

## Recommended next branch

After a reviewer approves, finalizes, and merges this record, the recommended next branch is:

`design/coexisting-optimizer-lifecycle`

It should start from the then-current `origin/main` and consume this Outcome B without reopening the approved single-stage or coexistence findings. It should choose the production AMUSE phase and authoritative DAO ordering mechanism. It should specify Candidate A and the restricted Candidate-B profile. It should define the minimum validation needed for fail-closed operation.

This record starts no design branch.
