# AMUSE Coexisting Optimizer Lifecycle Design

## Status and scope

**AMUSE approves this document as its normative lifecycle architecture for coexisting optimizers. Implementation has not started.**

- Branch: `design/coexisting-optimizer-lifecycle`
- Base: `origin/main` at `d2bb574`
- Intended repository change: this design document only
- Census Lab and private avatars: not used

This is a production architecture specification, not an implementation plan. It defines the smallest fail-closed lifecycle for AMUSE. In this lifecycle, AMUSE consumes the effective avatar state that ordinary build tools produce. AMUSE then does one proof-and-mutation operation. AMUSE also coexists with a deliberately bounded remainder of work that runs after it. d4rkAvatarOptimizer (DAO) is the only v1 integration that needs explicit two-way cooperation.

This branch adds no production code, tests, Unity assets, package changes, NDMF plugin, SDK callback, DAO integration, profile implementation, or Candidate-B certification. It does not start the implementation-planning workflow.

## Prior decisions incorporated

This specification builds on these records:

- `docs/architecture/vision.md`
- `docs/superpowers/investigations/2026-08-22-single-stage-optimization-lifecycle.md`
- `docs/superpowers/investigations/2026-08-22-coexisting-optimizer-lifecycle.md`
- `docs/superpowers/investigations/2026-08-22-meshia-coexistence-lifecycle.md`
- `docs/superpowers/investigations/2026-08-22-sdk-build-environment-contract.md`
- `docs/superpowers/specs/2026-08-21-analysis-snapshot-ordering-design.md`
- `docs/superpowers/specs/2026-08-21-upload-conditional-authorization-design.md`
- `docs/superpowers/specs/2026-08-21-liltoon-build-callback-handoff-design.md`
- `docs/superpowers/audits/2026-08-22-general-purpose-transformation-boundaries.md`

The following conclusions are binding:

1. The targeted alpha transformation is complete under NDMF once every supported later actor that can affect the proof has one of these outcomes: AMUSE orders it before AMUSE runs, AMUSE models it, AMUSE proves it does not matter, the actor keeps a contract to preserve the result, or AMUSE refuses the affected capability.
2. Compatibility through the resulting state is the default model for interoperability.
3. Modular Avatar, VRCFury's main generation step, Meshia, and AAO normally produce input state before AMUSE runs. They need no AMUSE-specific adapter.
4. Meshia's simplified mesh is the mesh AMUSE analyzes. Meshia's history of the change and its source-to-destination mappings are not required. They are not a valid substitute for a fresh proof.
5. DAO needs a narrow two-way lifecycle and a preservation contract when it runs after AMUSE.
6. DAO Candidate A is the mandatory safe floor. Candidate B is optional. It admits only a fixed profile and an exact operation.
7. DAO Shader Toggles and fused DAO/AMUSE planning stay out of v1.
8. Uncertainty must never make AMUSE more aggressive.

## Goals

- Set up one authoritative semantic barrier after all NDMF `Optimizing` work finishes.
- Capture immutable effective state early. Then do semantics, proof, planning, preparation, and mutation exactly once.
- Consume ordinary output from prior tools without tool-specific adapters.
- Keep NDMF's `BuildContext`, generated-asset, reporting, and build-failure services.
- Bound correctness to explicit host assumptions and to concrete, supported post-AMUSE actors. Do not inspect the whole tail of the build.
- Guarantee that AMUSE runs before DAO, and that DAO runs exactly once, in the supported two-way path.
- Give DAO a mandatory, independently safe Candidate-A floor and an optional, exact Candidate-B admission path.
- Keep cooperation metadata temporary, clone-local, minimal, and specific to one profile.
- Keep the two-way dependency one-way in code. DAO's optional integration may depend on AMUSE's narrow cooperation protocol. AMUSE core never depends on DAO.
- Preserve room for future transformations across renderers, materials, textures, UVs, and animation. Do not design a universal optimizer framework now.

## Non-goals

This design does not create or need:

- an NDMF API that exposes the resolved tail
- reflection over `BuildStepPlan`, `PluginResolver`, `ConcretePass`, or similar internals
- a PlatformFinish tail scanner or an SDK callback inventory framework
- a generic plugin registry, allowlist architecture, or compatibility matrix
- adapters for MA, VRCFury, Meshia, or AAO
- a universal avatar IR, theorem language, transformation DSL, dependency graph, or planner
- a persistent, user-facing compatibility component
- a third generic interoperability package without a concrete need for it
- proofs that transfer between arbitrary transformations
- Candidate-B certification for any DAO operation
- preservation of DAO Shader Toggles
- fused DAO/AMUSE execution
- rollback or transactions
- a post-DAO validation callback
- positive AMUSE mutation during Apply-on-Play in v1
- an implementation, or an implementation plan

## Normative lifecycle

The selected lifecycle is:

```text
completed prior transforms, including all NDMF Optimizing work
    -> any relevant characterized pre-AMUSE PlatformFinish producer
    -> AMUSE semantic barrier
         lifecycle/environment capability
         eager immutable extraction
         purpose-specific semantics
         proof
         plan
         deterministic preparation
         AMUSE mutation
         temporary protection/profile records
    -> cooperating DAO NDMF bridge
         Candidate B only for an exact certified operation
         Candidate A otherwise
    -> characterized validators and modeled or irrelevant remainder
    -> BuildContext.Finish()
    -> normal host continuation
```

AMUSE need not be literally the first PlatformFinish pass. Every relevant state producer must run before the barrier. AMUSE must characterize a pre-AMUSE PlatformFinish pass as an input producer, or prove that it does not matter to the current theorem. Every concrete, supported actor that runs after the barrier must have one of these outcomes: AMUSE orders it, models it, proves it does not matter, the actor keeps a preservation contract, or AMUSE makes the affected capability unavailable.

No supported plugin runs inside the AMUSE pass. Extraction through mutation is one bounded AMUSE operation. It is not a handoff between lifecycle callbacks.

## PlatformFinish as a deliberate phase-intent exception

NDMF describes PlatformFinish mainly as platform cleanup and validation. AMUSE is not platform cleanup. AMUSE performs substantive optimization. So the chosen placement is a deliberate exception to phase intent, not a relabeling of AMUSE's role.

The exception is justified. The phase boundary gives AMUSE authority that NDMF 1.14.4's public `Optimizing` ordering surface cannot give:

- All ordinary `Optimizing` producers have already run.
- Meshia is automatically before AMUSE, even without AAO.
- AAO and Modular Avatar `Optimizing` work is automatically before AMUSE.
- AMUSE observes future ordinary `Optimizing` producers without needing identity edges to them.
- `BuildContext` is still active.
- NDMF's generated-asset saving, object and context services, diagnostics, and failure propagation remain available.
- A DAO-owned NDMF bridge can run after AMUSE, before `Finish()`.

For this design, the authority AMUSE gains outweighs the mismatch in phase role. Each supported NDMF version needs a fresh check of this conclusion. This is not a claim that any optimization work belongs in PlatformFinish.

## Rejected and retained alternatives

### Final `Optimizing` with explicit ordering

This alternative still works and keeps the phase's nominal intent. It is worse because its authority depends on enumerating known plugins. Meshia needs an explicit dependency edge when AAO is absent, and each future same-phase producer adds ordering work to maintain. Incidental type-name order is never a source of authority.

### Complete AMUSE operation after NDMF

A later SDK callback could run extraction through mutation entirely after NDMF calls `Finish()`. This is not the rejected cross-callback authorization model, because no AMUSE proof or authorization state would cross callbacks.

It remains a fallback architecture, not the v1 choice. It loses `BuildContext`, NDMF asset saving, and NDMF diagnostics. It needs custom, persistent ownership and recovery of generated assets. It also makes DAO ordering hard without Modular Avatar, across the NDMF `-1025` and editor-only-removal `-1024` boundary.

### Upstream tail API and reflection

No API that exposes the resolved tail is required for v1. AMUSE may reconsider an upstream semantic-barrier or tail API only when a concrete PlatformFinish writer shows a need that public lifecycle ordering cannot meet.

This design rejects reflection over NDMF scheduling internals. Reflection is a last-resort compatibility technique. AMUSE may use it only after every public lifecycle alternative has failed to meet a concrete requirement.

## Semantic barrier

The AMUSE semantic barrier is the point where completed external transformations become immutable proof input for one AMUSE operation.

Before the barrier, tool identity is normally irrelevant. A completed tool is acceptable when AMUSE can fully extract and understand its resulting ordinary Unity state for the proposed transformation.

After the barrier, every concrete, supported actor that can change a dependency of the proposed theorem must have one of these outcomes:

- `ORDER`: AMUSE moves it before AMUSE runs.
- `MODEL`: AMUSE includes its characterized later behavior in the theorem.
- `IRRELEVANT`: AMUSE proves it cannot change this theorem's dependencies.
- `PRESERVE`: the actor keeps an explicit preservation contract, such as DAO Candidate A or B.
- `REFUSE`: AMUSE makes the affected positive capability unavailable.

The barrier does not claim that AMUSE is the last writer in every case.

## Host and lifecycle trust boundary

### V1 proof boundary

AMUSE is responsible for proving correctness against:

- the effective Unity state captured at its barrier
- exact host and platform behavior, wherever a capability depends on it
- AMUSE's own preparation and mutation
- concrete post-AMUSE actors that AMUSE explicitly supports or relies on
- DAO Candidate-A and Candidate-B cooperation
- modeled later behavior, including the characterized lilToon target theorem

AMUSE is not responsible for verifying the whole build program against arbitrary third-party editor code that has no public way to observe it.

The target theorem has this antecedent:

```text
supported host lifecycle assumption
+ complete immutable proof input
+ successful AMUSE proof and mutation
+ satisfied explicit remainder contracts
-------------------------------------------------
target transformation theorem holds
```

### Three uncertainty classes

1. **Unknown semantic state.** A fact the proof needs is missing or unsupported in the captured effective state. AMUSE fails closed and keeps the affected input unchanged.
2. **A concrete later actor with unknown behavior.** A known or publicly detectable installed actor may change a theorem dependency after AMUSE runs. The affected capability stays refused until AMUSE orders the actor earlier, models it, proves it does not matter, or the actor keeps a preservation contract.
3. **An unobservable hypothetical host violation.** A future or unknown plugin might misuse a later lifecycle phase. It is not part of the supported contract, and AMUSE cannot publicly observe it. It stays outside the v1 proof boundary. It becomes a compatibility concern only when concrete evidence appears.

Class 3 is not safe by default. It also does not justify a registry, a scanner, or reflection over the whole build program.

AMUSE must not claim support for a known incompatible combination that it cannot detect reliably. A future public mechanism may enable automatic per-build refusal. The absence of that mechanism does not license a guess.

## Lifecycle and environment capability

AMUSE assesses capability per transformation candidate, or per independently separable domain. It does not use one global environment flag.

Capability needs only the minimum facts a candidate requires:

- the build path
- the tested host, platform, and lifecycle versions
- the candidate's proof-relevant domains
- explicit contracts for concrete, supported actors before and after the barrier
- whether DAO cooperation is available, and who owns its execution, when DAO is active
- the exact modeled later behavior the theorem relies on

The capability result states:

- permitted or refused
- the supported lifecycle assumption AMUSE used
- required dispositions for the remainder
- whether DAO protection is required, and whether it is available
- a diagnostic refusal category and reason

A static version or package check is enough only for an actor with an explicit, tested contract. It cannot substitute for verifying the whole tail, and it does not try to enumerate unknown plugins.

An unrelated unknown domain does not disable an independent candidate, as long as AMUSE proves the separation between them. When AMUSE cannot reliably prove that separation, refusal widens to the smallest scope AMUSE can safely establish.

## Eager effective-state extraction

At the barrier, host extraction must copy every proof-relevant fact before AMUSE mutates live state. Proof, planning, and preparation consume only these AMUSE-owned immutable values.

Depending on the concrete transformation, host extraction may include:

- renderer, mesh, and material-slot relationships
- vertices, topology, indices, submeshes, and relevant vertex attributes or UV channels
- shader identity, normalized material state, and relevant texture assignments
- texture sampling, importer facts, dimensions, format, and required pixels
- reachable material swaps, animated properties, and renderer state
- stable diagnostic locators and locators for immediate mutation

Live meshes, materials, textures, controllers, and clips are sources for extraction and targets for immediate mutation. They are not proof evidence. Unity's instance identity does not establish semantic equivalence.

Shared host records must stay general across transformations, where existing evidence already requires that. But they grow only for a concrete consumer. Alpha semantics, alpha proof, and mesh-separation planning stay specific to their purpose. This design does not introduce one giant generic snapshot or an optimizer IR.

## Dynamic state and reachability

Prior tools may create or rewrite controllers, clips, material swaps, animated material properties, and renderer state. State-based composition stays valid, because AMUSE reasons over the resulting reachable state, not over which tool produced it.

Before a positive transformation:

```text
every proof-relevant reachable state is represented
    OR
the candidate refuses
```

AMUSE must represent material swaps as immutable reachable material states, tied to the affected renderer slots. Unknown animation behavior causes semantic refusal, not a lifecycle failure. This specification sets the contract boundary. It does not design the complete reachability solver.

## State-based prior-tool composition

### Meshia

Meshia completes its work in `Optimizing`. AMUSE proves its case against Meshia's resulting ordinary mesh in PlatformFinish. This guarantees that Meshia runs before AMUSE, even when AAO is absent. AMUSE needs no Meshia adapter, provenance data, collapse history, instruction metadata, or source-triangle mapping.

### AAO

AAO's final mesh, material, texture, and animation results are ordinary barrier input. AMUSE handles Meshia followed by AAO the same way: AMUSE proves its case against AAO's resulting state.

### Modular Avatar

Modular Avatar's generated hierarchy, controllers, clips, material assignments, and optimizing cleanup are barrier input. Unsupported reachable behavior causes semantic refusal. This design adds no MA adapter.

### VRCFury

The characterized main VRCFury generation step runs before AMUSE and becomes barrier input. Its later, scoped parameter-compression behavior does not matter, but only to the current theorem, and only under the tested revision. That finding does not generalize to other transformations or revisions.

## DAO cooperation: separate lifecycle and preservation contracts

The two-way integration has two independent contracts:

1. **Lifecycle contract:** AMUSE completes its authorized mutation before cooperating DAO starts any mutation.
2. **Semantic-preservation contract:** DAO may transform an AMUSE-protected target only through an explicitly characterized Candidate-A-safe operation, or through an exactly admitted Candidate-B operation.

Satisfying the lifecycle contract does not certify semantic preservation. Satisfying a semantic profile does not establish execution order.

## DAO bridge architecture

The selected architecture is a small, optional NDMF PlatformFinish bridge that DAO owns. NDMF orders it explicitly after the AMUSE plugin. DAO owns the bridge, because the bridge calls DAO internals and must track the exact DAO operations it admits.

The bridge reads typed, temporary cooperation state through NDMF 1.14.4's public `BuildContext.GetState<T>()`. It needs no reflection and no persistent avatar component.

DAO running alone keeps its normal integration. AMUSE running alone has no dependency on DAO. Authors of unrelated plugins need not take part.

### Protocol ownership and dependency direction

NDMF 1.14.4 keys `BuildContext.GetState<T>()` by the exact runtime `Type`. So AMUSE and the DAO bridge must request the same AMUSE-owned contract type. Structurally similar but separate private types would create different state entries, and they could not work together.

AMUSE owns the narrow build-time cooperation protocol and its schema. AMUSE owns the protected-target theorem, the meaning of Candidate-A protection, and Candidate-B profile semantics. DAO's optional AMUSE integration and its NDMF bridge may take a compile-time dependency on that narrow AMUSE integration API.

The dependency direction is normative:

```text
AMUSE core
    -> AMUSE-owned narrow integration contract
    <- DAO optional AMUSE integration / NDMF bridge

AMUSE core
    -X-> DAO
```

AMUSE core must not take a compile-time dependency on DAO. The exact AMUSE assembly or package layout is an implementation detail. This design does not introduce a third generic interoperability package. Such a package needs later, concrete pressure from another real two-way consumer.

The public, editor-only integration surface contains only the exact shared types cooperation needs:

- cooperation protocol identity and version
- the protected-target association and Candidate-A protection data
- the fixed Candidate-B profile identity and its profile-specific attestation fields
- invocation-local execution-ownership state that the two-way lifecycle needs

It must not expose AMUSE's semantic engine, proof internals, purpose-specific planners, a generic IR, arbitrary theorem representations, or a public extension registry. DAO owns its bridge and its exact-operation admission logic. AMUSE owns the meanings DAO consumes.

### Per-build execution ownership

Package presence alone does not let DAO's SDK callback defer. The existing callback may defer only when it is positively guaranteed that the cooperating bridge will execute for this specific build.

The per-build rule is:

```text
normal DAO SDK callback XOR cooperating DAO NDMF bridge
```

Exactly one of the two owns execution: never both, never neither.

The design needs a build-local execution-ownership handshake, or an equivalent authoritative mechanism, with states conceptually equal to:

- the normal callback owns execution
- bridge ownership is positively armed for this invocation
- execution has started
- execution has completed or failed

The exact flag or type is deliberately left open. But ownership must not depend only on package presence, callback discovery order, or the DAO component's accidental disappearance.

Ownership is single-use and scoped to one invocation. A bridge failure is a terminal attempted execution. It aborts the normal build, and it must not let the SDK callback run DAO a second time. When an invocation completes, fails, or is abandoned, its ownership state must become unusable for every later invocation.

The normal callback may defer only after it confirms all of these:

- this invocation has an active, supported NDMF context
- NDMF will run the relevant PlatformFinish group for this invocation
- the DAO bridge is registered and supported
- AMUSE is active under a supported host contract
- the bridge has positively accepted ownership

If any condition is missing or uncertain, the normal callback keeps ownership and behaves normally.

If normal DAO execution happens because bridge ownership was never armed, the supported post-AMUSE cooperation contract is unavailable for that invocation. AMUSE must not emit protection metadata on the assumption that DAO will run later. In v1, AMUSE refuses the affected positive capability, unless a separate, explicitly characterized pre-AMUSE DAO state-based path exists. This specification does not establish such a path.

### Required lifecycle cases

- **Normal upload, supported NDMF and AMUSE active:** bridge ownership is armed. The normal callback defers. AMUSE, then the bridge, each run exactly once.
- **NDMF ApplyOnBuild disabled:** bridge execution is not guaranteed. The normal callback must not defer.
- **AMUSE installed but inactive or unsupported:** bridge ownership is not armed. The normal callback keeps normal DAO behavior.
- **Bridge unavailable, or host version unsupported:** the normal callback keeps normal DAO behavior.
- **DAO callback observed first at the `-1025` tie:** it may defer only after positive bridge ownership for this invocation already exists. Otherwise it runs normally.
- **NDMF observed first at the `-1025` tie:** the bridge runs once. A later DAO callback observes that completion as authoritative and does not run again.
- **Modular Avatar moves DAO's callback to `-15`:** the same ownership protocol applies. Callback order does not replace the handshake.
- **Apply-on-Play:** positive AMUSE mutation is unavailable in v1, so AMUSE never arms the cooperation path. DAO follows its own, independently supported Play Mode behavior.

An implementation that cannot positively arm ownership before the first possible DAO callback does not satisfy this architecture. It must keep normal DAO execution and withhold AMUSE/DAO cooperation, rather than risk running both or neither.

### Bridge executable-viability gate

Choosing this architecture does not prove that current DAO can safely run from an NDMF pass. The first DAO integration implementation must pass a hard validation gate. It must show that bridge execution:

- has an authoritative, invocation-local mechanism that can positively arm bridge ownership before the earliest possible DAO `-1025` callback
- makes that armed ownership observable to the normal DAO callback, even when DAO is discovered first at the `-1025` tie
- keeps exactly-once ownership when NDMF is discovered first at the same tie
- preserves DAO configuration until DAO consumes it
- runs exactly once
- works in both `-1025` callback discovery orders
- produces meshes, materials, and assets that survive `BuildContext.Finish()`
- stays coherent with NDMF's ObjectRegistry and context behavior
- propagates normal build failures
- leaves later PlatformFinish and `Finish()` processing coherent
- preserves DAO-alone behavior
- preserves AMUSE-alone behavior
- behaves correctly with and without Modular Avatar

If the implementation cannot positively establish ownership before that earliest callback, normal DAO behavior must run, and AMUSE/DAO positive cooperation must be withheld. The implementation must never guess that the bridge will run.

If the implementation fails to show the pre-callback ownership mechanism, or fails any other part of this gate, that reopens the DAO lifecycle mechanism. It must not silently weaken the ordering, ownership, failure, or preservation contracts.

## Cooperation metadata

Typed, temporary `BuildContext` state is the selected carrier for protection and profile data. It is build-local, clone-local, public in NDMF 1.14.4, and it is discarded naturally with the build context. AMUSE and DAO must access it through the same exact, AMUSE-owned runtime contract type.

The minimum Candidate-A target association contains only:

- the cooperation protocol version
- the protected target kind
- a direct, clone-local object association
- the renderer-slot or protected-region relationship
- the logical AMUSE output role
- the Candidate-A protection requirement
- an optional Candidate-B profile identifier

It supports multiple protected targets. It is not stored on original assets. It does not affect runtime behavior. It does not use user-visible names as authority, and it does not use Unity instance IDs as durable identity.

Candidate-B records may add only the fixed, normalized attestation fields that exact profile requires. This design does not require a generic, proof-relevant fingerprint, or a second canonicalization layer beneath the profile. AMUSE may add such a field later, only if concrete implementation evidence shows the minimum association cannot prevent target confusion.

## Logical generated-output identity

Coexistence needs only build-local association, not a universal identity system. A target record relates the generated live objects to:

- the renderer slot or protected mesh region
- the logical AMUSE output role
- Candidate-A protection
- the optional, fixed Candidate-B profile

DAO may merge or remap structures only through an explicitly admitted operation. That operation must keep an operation-local mapping from the protected input relationship to its successor. This design introduces no persistent GUID scheme, no global object graph, and no cross-build logical identity.

## Opaque target preservation profile

Candidate B uses one AMUSE-owned, fixed identifier, conceptually:

```text
com.alrauna.amuse/opaque-target-preservation/v1
```

The exact spelling is an implementation detail. The version identifies one fixed theorem, one fixed set of normalized attestation fields, and one interpretation for each field. Adding, removing, or redefining a proof-sensitive obligation needs a new profile version.

The profile covers, as far as its eventual exact scope reaches:

- protected triangle-to-material relationships
- opaque render-state requirements
- alpha behavior of surviving fragments
- the absence of new, unmodeled coverage suppression
- `_Invisible`, ID-mask, and UDIM suppression state
- clipping, dither, dissolve, and related coverage behavior
- culling and pass behavior
- relevant topology, deformation, and UV assumptions
- reachable animation and material-swap behavior

AMUSE emits the profile only for a target state it has exactly proved and completely attested. DAO may understand several explicit profile versions. An unknown profile version always falls back to Candidate A.

The profile is not a generic assertion list or a theorem language.

## Candidate A: mandatory safe floor

Candidate A stays safe on its own, even if AMUSE has never certified a Candidate-B operation. It is an allow-only policy:

```text
protected target
    -> explicitly characterized Candidate-A-safe operation
         -> allow that operation

    -> Candidate-B-certified operation
         -> Candidate B

    -> unknown or uncharacterized operation
         -> exclude or isolate the protected target
```

Candidate A must not mean that any DAO Basic or structural behavior is allowed whenever a generic predicate seems to hold. Every allowed Candidate-A operation must be explicitly characterized against the current, theorem-scoped DAO Basic conclusion.

Candidate-A-safe behavior may include only operations whose exact implementation is established to preserve the protected relationship, without rewriting proof-sensitive semantics. For example, AMUSE may admit structural consolidation only when its characterized operation preserves:

- each protected triangle's association with the protected material semantics
- proof-relevant vertex attributes, topology, deformation, culling, and pass behavior
- renderer and material reachability
- a lossless, explicit material-slot remap when slots change
- all coverage-sensitive shader, property, texture, and UV behavior

Candidate A forbids uncharacterized shader replacement or specialization. It also forbids proof-sensitive property changes, proof-relevant texture or sampling changes, coverage suppression, proof-relevant UV overwrite, and unmodeled animation rewriting.

Before DAO mutation starts, when AMUSE cannot optimize a protected scope, it normally preserves or isolates the smallest scope it can safely identify. Build abortion is not the ordinary fallback. If AMUSE cannot classify isolation itself as safe, DAO skips the broader affected optimization scope.

The shared lilToon shader must not become globally incompatible. Protection is target-specific. Unprotected materials that use the same shader keep ordinary DAO behavior.

## Candidate B: exact operation admission

Candidate B is optional and specific to one operation:

```text
target not protected
    -> normal DAO behavior

target protected, profile unknown
    -> Candidate A

target protected, profile understood
    -> exact proposed operation certified?
         no  -> Candidate A
         yes -> exact profile attestation and operation preconditions match?
                  no  -> Candidate A
                  yes -> Candidate B operation
```

For v1, a closed table or switch is enough. Admission is keyed conceptually by:

- the exact, supported DAO integration identity
- the exact AMUSE profile identifier and version
- the exact, supported shader and closure identity
- the exact DAO operation kind
- the exact required parameters and input state

The admission mechanism is architecture. Certification of exact-value specialization, material merging, MaterialID use, texture arrays, UV transport, or other proposed operations is separate evidence. This document does not grant that certification.

"DAO Full is trusted" is never an admission rule. Unknown DAO revisions, profiles, shader closures, operations, arrangements, UV use, parameters, or attestations all select Candidate A.

## Shader Toggles and fused planning

DAO Shader Toggles on AMUSE-protected geometry stay outside Candidate B v1. `_IsActiveMeshN`, generated mesh IDs, vertex-stage suppression, and rewritten visibility animation form a new coverage system. That system needs a separate theorem. Candidate A excludes the protected target from such an operation.

Deep Candidate C cooperation stays rejected. There is no shared mesh planner, cross-optimizer IR, combined executor, or generic operation graph. AMUSE may reconsider this only if measurements later show real duplicated work or lost opportunity that Candidate A or B cannot fix.

## Post-DAO remainder

For the current target theorem:

- AMUSE models the characterized lilToon callback behavior
- the scoped, late VRCFury parameter compression does not matter, but only under its tested revision and this theorem
- a concrete, supported later actor may remain only through an explicit model, an irrelevance result, or a preservation contract
- a known, proof-relevant later actor without one of those outcomes causes AMUSE to refuse the affected capability

The normal architecture has no post-DAO AMUSE validation callback. There is no later authorization state, no build-attempt handoff, and no second proof. Adding such a callback needs new evidence that the single-stage theorem is not enough.

Inspection of current NDMF 1.14.4 PlatformFinish found no concrete later writer that threatens the targeted alpha theorem. `GeneratePortableComponents` writes platform component state. `CheckMipStreamingPass` diagnoses texture configuration. It does not automatically mutate the target semantics. This fact is scoped to this version and this theorem. It is not a universal safety claim about PlatformFinish.

## Unknown-tool policy

An unknown tool before the barrier is acceptable when it has completed, and AMUSE can fully extract and understand its proof-relevant resulting state. Tool identity is not required.

AMUSE has no generic obligation or mechanism to discover every remaining PlatformFinish writer. For concrete actors AMUSE explicitly supports or relies on, public ordering, exact-version characterization, and two-way contracts establish the remainder.

A known or detectable later writer that may change a proof-relevant domain causes AMUSE to refuse the affected capability, until AMUSE resolves the conflict. If reliable domain separation is impossible, refusal expands to the smallest broader scope AMUSE can safely establish. A hypothetical, unobservable future writer stays outside the v1 host assumption, until concrete evidence turns it into a compatibility requirement.

## Failure semantics

### Before first mutation

- Semantic uncertainty, unsupported reachability, unavailable lifecycle authority, and Candidate-B ineligibility normally cause AMUSE to preserve or skip the smallest affected scope.
- Deterministic preparation builds and validates required outputs before AMUSE assigns them.
- No rollback is required.
- Candidate-B failure selects Candidate A.
- When Candidate A cannot proceed, AMUSE isolates or skips the protected scope, rather than aborting the build as the normal response.

### After first mutation

- Any AMUSE failure that could leave inconsistent output aborts the normal build.
- If a DAO operation has started mutation and cannot guarantee coherent output, the build aborts.
- Violating a supposedly certified preservation invariant aborts the build.
- Rollback is not promised. The build clone is disposable.

Normal build failure must propagate through NDMF and the host, so no invalid bundle or upload proceeds. Bridge-viability testing must establish this behavior for DAO execution from PlatformFinish.

## Apply-on-Play

Positive AMUSE mutation is unavailable during Apply-on-Play in v1. The investigated NDMF path calls SDK preprocessing but ignores its Boolean result. So ordinary build-abort behavior is not an enforceable safety boundary there.

The AMUSE/DAO cooperating bridge path is therefore never armed for Apply-on-Play. DAO keeps its own, independently supported Play Mode behavior. AMUSE may analyze or report, but it does not mutate.

Apply-on-Play mutation needs a separate investigation into execution ownership, repeatability, generated assets, failure containment, and cleanup. It is not permanently impossible, but normal-upload correctness does not imply it.

## Host and version contracts

Exact tested versions matter wherever correctness depends on lifecycle behavior, including:

- NDMF phase order, public context state, generated assets, ObjectRegistry and context coherence, `Finish()`, and error propagation
- VRChat SDK callback order and normal build-abort behavior
- DAO bridge ownership and the exact operation implementations
- modeled lilToon or VRCFury behavior

Unknown versions disable only the capabilities whose required contract AMUSE cannot establish. Pure analysis, or other proven capabilities, may stay available. Semver alone is not evidence. Support follows observed tests and source characterization.

AMUSE must document the supported lifecycle assumption in diagnostics and compatibility documentation. AMUSE must not claim global authority over arbitrary editor code.

## Diagnostics

Interoperability is automatic. Artists do not select a compatibility mode. Diagnostics explain the conservative result to developers and power users.

Required categories include:

- AMUSE candidate accepted
- semantic or reachability refusal
- lifecycle or host refusal
- AMUSE target protected
- Candidate B admitted
- Candidate B unavailable, Candidate A used instead
- protected scope isolated or skipped by Candidate A
- DAO cooperation unavailable
- DAO execution ownership not guaranteed
- unsupported DAO integration or profile version
- shader, closure, or profile-attestation mismatch
- a known later writer blocks the affected capability
- post-mutation build abort

Where possible, a diagnostic names the affected renderer, material, protected role, refused capability, and safe outcome. Diagnostics do not affect correctness.

## Test architecture

This design branch adds no tests. A later implementation must provide these layers.

### Pure contract tests

- cooperation protocol and profile identity and versioning
- Candidate-A allow-only behavior
- unknown Candidate-A operations cause isolation or a skip
- Candidate-B exact operation admission
- unknown versions, shaders, attestations, and operations fail closed
- target association, and multiple protected targets
- candidate-scoped lifecycle refusal

### Synthetic Unity integration tests

- MA followed by AMUSE
- VRCFury followed by AMUSE
- Meshia followed by AMUSE, without AAO
- Meshia followed by AAO followed by AMUSE
- AMUSE alone, and DAO alone
- AMUSE followed by cooperating DAO, with and without Modular Avatar
- both `-1025` callback discovery orders
- NDMF ApplyOnBuild disabled
- AMUSE installed but inactive or unsupported
- bridge unavailable, and unsupported host version
- bridge ownership positively armed before the earliest possible DAO `-1025` callback
- exactly-once DAO execution, and authoritative ownership transitions
- AMUSE and DAO resolving the same `BuildContext` state through the exact AMUSE-owned runtime contract type
- generated DAO meshes, materials, and assets surviving `Finish()`
- ObjectRegistry and context coherence, and later PlatformFinish coherence
- normal build failure propagation
- a representative combined pipeline

Ordering tests must trace AMUSE's completion and DAO's start directly. Output coincidence alone is not sufficient evidence.

### Adversarial preservation tests

Every future Candidate-B certification must exercise:

```text
effective input
    -> AMUSE proof and mutation
    -> exact DAO operation
    -> fresh test-only semantic extraction
    -> exact opaque-profile postcondition
```

Adversarial cases include material swaps, animated properties, clipping, dither, dissolve, `_Invisible`, ID-mask and UDIM suppression, culling, pass selection, UV ambiguity, texture sampling, topology, and deformation.

The fresh test extraction is an oracle for certification tests. It is not a production post-DAO authorization callback.

### Fallback and failure tests

- an unsupported DAO revision, profile, operation, shader, or arrangement
- Candidate B falling back to Candidate A
- a Candidate-A unknown operation isolating the smallest safe scope
- Candidate A unavailable causing a skip, rather than an ordinary pre-mutation abort
- known later-writer refusal, where public detection exists
- an AMUSE or DAO post-mutation failure aborting the build
- no invalid output artifact proceeding after a failure

### Census and performance work

Census Lab may later measure how often coexistence happens, why AMUSE refuses, the Candidate-A/B opportunity, and real-world optimization pressure. It never certifies correctness, and this design does not use or modify it.

Controlled benchmarks may later measure renderers, material slots, transparent triangles, overdraw, GPU time, and relevant CPU or render-thread effects. This design defines no universal performance score.

## Relationship to upload-conditional authorization

The earlier upload-conditional design remains a valid exceptional architecture for a future transformation whose proof genuinely depends on evidence produced after AMUSE must mutate.

It is narrowed as follows:

- it is not the default AMUSE lifecycle
- the current alpha theorem does not require it
- the normal path carries no proof or authorization state across callbacks
- the normal path has no late validation gate and no build-attempt identity problem
- AMUSE does not mutate speculatively while it waits for later approval

A future capability that truly needs post-mutation evidence must independently satisfy the older architecture's stricter requirements for lifecycle, identity, fresh extraction, cleanup, and failure gates. This document neither deletes nor broadens that exceptional design.

## Generality boundaries

Shared boundaries may support future transformations across multiple renderers and meshes, material-slot restructuring, UV changes, generated textures, atlasing, material combining, control textures, and submesh merging. The shared concepts stay limited to:

- the semantic barrier and host lifecycle contract
- candidate-scoped lifecycle capability
- eager, immutable host extraction
- deterministic preparation before mutation
- build-local, generated-output association
- the first-mutation failure boundary
- temporary post-AMUSE protection records

Purpose-specific proof, plans, profile attestations, and operation certifications stay narrow. A new general abstraction needs a second concrete consumer, or demonstrated pressure.

## Future pressure triggers

Reconsider this architecture only when concrete evidence establishes one of these pressures:

- an installed PlatformFinish writer changes an AMUSE theorem dependency
- a supported NDMF version changes PlatformFinish mutation, context, asset, or failure guarantees
- a public NDMF barrier or tail API materially simplifies a demonstrated requirement
- a transformation genuinely needs post-NDMF evidence
- Apply-on-Play demand appears together with proven failure containment
- Candidate A causes a material, measured loss in optimization
- a separate theorem is developed for DAO Shader Toggles
- multiple real transformations need a broader immutable representation
- measured duplicated DAO/AMUSE work justifies reconsidering fused planning
- the DAO bridge fails its executable-viability gate

The right response to a trigger is a focused investigation. It is not automatic expansion into a generic compatibility framework.

## Implementation gates and unresolved details

Before production implementation can claim this architecture is available, later work must establish:

1. the exact supported host and version contract
2. the exact build-local DAO execution-ownership mechanism, including authoritative arming before the earliest possible DAO `-1025` callback
3. the DAO bridge executable-viability gate
4. the minimal, AMUSE-owned, typed `BuildContext` cooperation record, and the one-way dependency from DAO to the AMUSE integration
5. the exact Candidate-A-safe DAO operations
6. the fixed opaque-profile fields
7. any Candidate-B certification, as a separate, evidence-backed result
8. the smallest reliable build-path classification that keeps Apply-on-Play mutation unavailable

These are implementation and validation questions inside the selected architecture. A failure of the DAO bridge viability gate specifically reopens the DAO lifecycle mechanism. It does not weaken exactly-once execution or AMUSE-before-DAO.

## Completion criteria for a future implementation

A future implementation conforms to this specification only when it demonstrates:

- all relevant producers precede the AMUSE barrier
- AMUSE performs eager extraction through mutation exactly once
- proof uses only immutable captured values
- Meshia precedes AMUSE without depending on AAO
- AMUSE owns the exact shared protocol types, and AMUSE core has no compile-time dependency on DAO
- bridge ownership is positively armed before the earliest possible DAO `-1025` callback, in both discovery orders
- DAO execution has positive, per-build ownership and exactly-once semantics
- Candidate A admits only explicitly characterized safe operations
- unknown Candidate A operations cause isolation or a skip of the protected scope
- Candidate B admits only exact certified operations, and always falls back safely
- generated assets survive, and context and object associations stay coherent
- post-mutation failures abort normal builds
- no post-DAO authorization callback exists for the current path
- Apply-on-Play positive mutation stays unavailable, until it is separately proven safe
- unsupported and unknown evidence never broadens optimization
- no whole-tail scanner, reflection dependency, or generic plugin framework has entered the design
</content>
<parameter name="i">Rewrite spec in STE-flavored English</parameter>
