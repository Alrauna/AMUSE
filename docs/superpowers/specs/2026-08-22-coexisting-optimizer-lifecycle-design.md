# AMUSE Coexisting Optimizer Lifecycle Design

## Status and scope

**Approved as AMUSE's normative coexisting-optimizer lifecycle architecture. Implementation has not started.**

- Branch: `design/coexisting-optimizer-lifecycle`
- Base: `origin/main` at `d2bb574`
- Intended repository change: this design document only
- Census Lab/private avatars: not used

This is a production architecture specification, not an implementation plan. It defines the smallest authoritative, fail-closed lifecycle in which AMUSE consumes the effective avatar state produced by ordinary build tools, performs one proof and mutation operation, and coexists with a deliberately bounded post-AMUSE remainder. DAO is the only v1 integration that justifies explicit bilateral cooperation.

This branch adds no production code, tests, Unity assets, package changes, NDMF plugin, SDK callback, DAO integration, profile implementation, or Candidate-B certification. It does not invoke the implementation-planning workflow.

## Prior decisions incorporated

This specification incorporates these records:

- `docs/architecture/vision.md`;
- `docs/superpowers/investigations/2026-08-22-single-stage-optimization-lifecycle.md`;
- `docs/superpowers/investigations/2026-08-22-coexisting-optimizer-lifecycle.md`;
- `docs/superpowers/investigations/2026-08-22-meshia-coexistence-lifecycle.md`;
- `docs/superpowers/investigations/2026-08-22-sdk-build-environment-contract.md`;
- `docs/superpowers/specs/2026-08-21-analysis-snapshot-ordering-design.md`;
- `docs/superpowers/specs/2026-08-21-upload-conditional-authorization-design.md`;
- `docs/superpowers/specs/2026-08-21-liltoon-build-callback-handoff-design.md`;
- `docs/superpowers/audits/2026-08-22-general-purpose-transformation-boundaries.md`.

The following conclusions are binding:

1. The targeted alpha transformation is semantically NDMF-complete once every supported proof-relevant later actor is ordered before AMUSE, modeled, proven irrelevant, contractually preserving, or causes conservative refusal.
2. Compatibility through resulting state is the default interoperability model.
3. Modular Avatar, VRCFury principal generation, Meshia, and AAO ordinarily produce input state before AMUSE. They do not require AMUSE-specific adapters.
4. Meshia's simplified mesh is the mesh AMUSE analyzes. Meshia provenance and source-to-destination mappings are neither required nor valid substitutes for a fresh proof.
5. DAO requires a narrow bilateral lifecycle and preservation contract when it operates after AMUSE.
6. DAO Candidate A is the mandatory safe floor. Candidate B is optional, fixed-profile, exact-operation admission.
7. DAO Shader Toggles and fused DAO/AMUSE planning are outside v1.
8. Uncertainty cannot make AMUSE more aggressive.

## Goals

- Establish one authoritative semantic barrier after all NDMF `Optimizing` work.
- Eagerly capture immutable effective state, then perform semantics, proof, planning, preparation, and mutation once.
- Consume ordinary prior-tool output without tool-specific adapters.
- Retain NDMF `BuildContext`, generated-asset, reporting, and build-failure services.
- Bound correctness to explicit host assumptions and concrete supported post-AMUSE actors without whole-tail introspection.
- Guarantee AMUSE-before-DAO and exactly-once DAO execution in the supported bilateral path.
- Give DAO a mandatory, independently safe Candidate-A floor and an optional exact Candidate-B admission path.
- Keep cooperation metadata temporary, clone-local, minimal, and profile-specific.
- Keep the bilateral dependency one-way: DAO's optional integration may depend on AMUSE's narrow cooperation protocol, while AMUSE core never depends on DAO.
- Preserve future room for multi-renderer, material, texture, UV, and animation transformations without designing a universal optimizer framework.

## Non-goals

This design does not create or require:

- an NDMF resolved-tail API;
- reflection over `BuildStepPlan`, `PluginResolver`, `ConcretePass`, or equivalent internals;
- a PlatformFinish tail scanner or SDK callback inventory framework;
- a generic plugin registry, allowlist architecture, or compatibility matrix;
- MA, VRCFury, Meshia, or AAO adapters;
- a universal avatar IR, theorem language, transformation DSL, dependency graph, or planner;
- a persistent user-facing compatibility component;
- a third generic interoperability package without concrete implementation pressure;
- arbitrary transferable semantic proofs;
- Candidate-B certification for any DAO operation;
- DAO Shader Toggle preservation;
- fused DAO/AMUSE execution;
- rollback or transactions;
- a post-DAO validation callback;
- positive AMUSE mutation during Apply-on-Play in v1;
- implementation or an implementation plan.

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

AMUSE need not literally be the first PlatformFinish pass. Every relevant state producer must precede the barrier. A pre-AMUSE PlatformFinish pass must be characterized as an input producer or proven irrelevant to the current theorem. Every concrete supported post-AMUSE actor must be ordered, modeled, proven irrelevant, contractually preserving, or make the affected capability unavailable.

No supported plugin interleaves within the AMUSE pass. Extraction through mutation is one bounded AMUSE operation, not a handoff between lifecycle callbacks.

## PlatformFinish as a deliberate phase-intent exception

NDMF describes PlatformFinish primarily as platform cleanup and validation. AMUSE is not platform cleanup; it performs substantive optimization. The selected placement is therefore a deliberate phase-intent exception, not a relabeling of AMUSE's role.

The exception is justified because the phase boundary provides authority unavailable in NDMF 1.14.4's public `Optimizing` ordering surface:

- all ordinary `Optimizing` producers have completed;
- Meshia is automatically before AMUSE even without AAO;
- AAO and Modular Avatar optimizing work are automatically before AMUSE;
- future ordinary `Optimizing` producers are observed without identity edges;
- `BuildContext` is still active;
- NDMF generated-asset saving, object/context services, diagnostics, and failure propagation remain available;
- a DAO-owned NDMF bridge can execute after AMUSE before `Finish()`.

The authority benefit outweighs the nominal phase-role mismatch for the current design. This conclusion must be revalidated for each supported NDMF version. It is not a claim that arbitrary optimization in PlatformFinish is always appropriate.

## Rejected and retained alternatives

### Final `Optimizing` with explicit ordering

This remains technically viable and preserves nominal phase intent. It is inferior because authority scales through known-plugin enumeration. Meshia needs an explicit dependency when AAO is absent, and future same-phase producers create continuing ordering maintenance. Incidental type-name order is never authority.

### Complete AMUSE operation after NDMF

A later SDK callback could perform extraction through mutation entirely after NDMF calls `Finish()`. This is not the rejected cross-callback authorization model because no AMUSE proof or authorization state would cross callbacks.

It remains a fallback architecture, not v1, because it loses `BuildContext`, NDMF asset saving, and NDMF diagnostics; requires custom persistent generated-asset ownership and recovery; and makes DAO ordering difficult without Modular Avatar across the NDMF `-1025` and editor-only-removal `-1024` boundary.

### Upstream tail API and reflection

No resolved-tail API is a v1 prerequisite. An upstream semantic-barrier or tail API may be reconsidered only if a concrete PlatformFinish writer demonstrates a requirement that public lifecycle ordering cannot satisfy.

Reflection over NDMF scheduling internals is explicitly rejected for the current architecture. It is a last-resort compatibility technique only after every public lifecycle alternative has been shown insufficient for a concrete requirement.

## Semantic barrier

The AMUSE semantic barrier is the point at which completed external transformations become immutable proof input for one AMUSE operation.

Before the barrier, tool identity is normally irrelevant. A completed tool is acceptable when its resulting ordinary Unity state can be completely extracted and understood for the proposed transformation.

After the barrier, every concrete supported actor that can change a dependency of the proposed theorem must have one of these dispositions:

- `ORDER`: move it before AMUSE;
- `MODEL`: include its characterized later behavior in the theorem;
- `IRRELEVANT`: prove that it cannot change this theorem's dependencies;
- `PRESERVE`: require an explicit preservation contract, such as DAO Candidate A/B;
- `REFUSE`: make the affected positive capability unavailable.

The barrier does not assert that AMUSE is globally the final writer.

## Host and lifecycle trust boundary

### V1 proof boundary

AMUSE is responsible for proving correctness with respect to:

- the effective Unity state captured at its barrier;
- exact host and platform behavior where capability depends on it;
- AMUSE's own preparation and mutation;
- concrete post-AMUSE actors that AMUSE explicitly supports or relies upon;
- DAO Candidate-A and Candidate-B cooperation;
- modeled later behavior, including the characterized lilToon target theorem.

AMUSE is not responsible for whole-build-program verification against arbitrary third-party editor code for which no public observation mechanism exists.

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

1. **Unknown semantic state.** A proof-relevant reachable fact is missing or unsupported in the captured effective state. AMUSE fails closed internally and preserves the affected input.
2. **Concrete later actor with unknown behavior.** A known or publicly detectable installed actor may change a theorem dependency after AMUSE. The affected capability refuses until the actor is ordered earlier, modeled, proven irrelevant, or contractually preserving.
3. **Unobservable hypothetical host violation.** A future or unknown plugin might misuse a later lifecycle phase but is neither part of the supported contract nor publicly observable. It is outside the v1 proof boundary. It becomes compatibility pressure when concrete evidence appears.

Category 3 is not treated as safe. It is also not justification for a registry, scanner, or reflection over the whole build program.

If a concrete incompatible combination is known but cannot be detected reliably, AMUSE must not claim support for that combination. Automatic per-build refusal may require a future public mechanism; the absence of that mechanism does not authorize guessing.

## Lifecycle and environment capability

Capability is assessed per transformation candidate or independently separable domain, not as one global environment Boolean.

Its inputs are the minimum facts required by that candidate:

- build path;
- tested host, platform, and lifecycle versions;
- the candidate's proof-relevant domains;
- explicit contracts for concrete supported pre- and post-barrier actors;
- availability and execution ownership of DAO cooperation when DAO is active;
- exact modeled later behavior on which the theorem relies.

Its result states:

- permitted or refused;
- the supported lifecycle assumption used;
- required remainder dispositions;
- whether DAO protection is required and available;
- a diagnostic refusal category and reason.

Static version or package checks are sufficient only for actors explicitly covered by a tested contract. They are not a substitute for whole-tail verification and do not attempt to enumerate unknown plugins.

An unrelated unknown domain does not disable an independent candidate when separation is itself proven. If reliable domain separation is unavailable, refusal broadens to the smallest scope AMUSE can establish safely.

## Eager effective-state extraction

At the barrier, host extraction must copy every proof-relevant fact before AMUSE mutates live state. Proof, planning, and preparation consume only AMUSE-owned immutable values.

Host extraction may include, as required by a concrete transformation:

- renderer, mesh, and material-slot relationships;
- vertices, topology, indices, submeshes, and relevant vertex attributes or UV channels;
- shader identity, normalized material state, and relevant texture assignments;
- texture sampling, importer facts, dimensions, format, and required pixels;
- reachable material swaps, animated properties, and renderer state;
- stable diagnostic and immediate-mutation locators.

Live meshes, materials, textures, controllers, and clips are extraction sources and immediate mutation targets, not proof evidence. Unity instance identity does not establish semantic equivalence.

Shared host records must remain transformation-general where existing evidence already requires it, but they grow only for concrete consumers. Alpha semantics, alpha proof, and mesh separation planning remain purpose-specific. This design does not introduce a giant generic snapshot or optimizer IR.

## Dynamic state and reachability

Prior tools may create or rewrite controllers, clips, material swaps, animated material properties, and renderer state. State-based composition remains valid because AMUSE reasons over the resulting reachable state rather than tool provenance.

Before a positive transformation:

```text
every proof-relevant reachable state is represented
    OR
the candidate refuses
```

Material swaps must be represented as immutable reachable material states associated with the affected renderer slots. Unknown animation behavior is semantic refusal, not a lifecycle failure. This specification defines the contract boundary; it does not design the complete reachability solver.

## State-based prior-tool composition

### Meshia

Meshia completes in `Optimizing`. AMUSE proves against Meshia's resulting ordinary mesh in PlatformFinish. This guarantees Meshia before AMUSE even when AAO is absent. AMUSE requires no Meshia adapter, provenance, collapse history, instruction metadata, or source-triangle mapping.

### AAO

AAO's final mesh, material, texture, and animation results are ordinary barrier input. Meshia followed by AAO is handled the same way: AMUSE proves against AAO's resulting state.

### Modular Avatar

Modular Avatar's generated hierarchy, controllers, clips, material assignments, and optimizing cleanup are barrier input. Unsupported reachable behavior refuses semantically. No MA adapter is introduced.

### VRCFury

The characterized principal VRCFury generation is before AMUSE and becomes barrier input. Its scoped later parameter-compression behavior is irrelevant only to the current theorem under the tested revision; that finding does not generalize to other transformations or revisions.

## DAO cooperation: separate lifecycle and preservation contracts

The bilateral integration has two independent contracts:

1. **Lifecycle contract:** AMUSE completes its authorized mutation before cooperating DAO begins any mutation.
2. **Semantic-preservation contract:** DAO may transform an AMUSE-protected target only through an explicitly characterized Candidate-A-safe operation or an exactly admitted Candidate-B operation.

Satisfying the lifecycle contract does not certify semantic preservation. Satisfying a semantic profile does not establish execution order.

## DAO bridge architecture

The selected architecture is a small DAO-owned, optional NDMF PlatformFinish bridge ordered explicitly after the AMUSE plugin. DAO owns the bridge because it invokes DAO internals and must track the exact DAO operations it admits.

The bridge consumes typed temporary cooperation state through NDMF 1.14.4's public `BuildContext.GetState<T>()`. It does not require reflection or a persistent avatar component.

DAO operating alone retains its normal integration. AMUSE operating alone has no dependency on DAO. Unrelated plugin authors need not participate.

### Protocol ownership and dependency direction

NDMF 1.14.4 keys `BuildContext.GetState<T>()` by the exact runtime `Type`. AMUSE and the DAO bridge must therefore request the same AMUSE-owned contract type; structurally similar private types would create different state entries and cannot interoperate.

AMUSE owns the narrow build-time cooperation protocol and schema because AMUSE owns the protected-target theorem, Candidate-A protection meaning, and Candidate-B profile semantics. DAO's optional AMUSE integration and NDMF bridge may take a compile-time dependency on that narrow AMUSE integration API.

The dependency direction is normative:

```text
AMUSE core
    -> AMUSE-owned narrow integration contract
    <- DAO optional AMUSE integration / NDMF bridge

AMUSE core
    -X-> DAO
```

AMUSE core must not take a compile-time dependency on DAO. The exact AMUSE assembly or package layout is an implementation detail; this design does not introduce a third generic interoperability package. Such a package requires later concrete pressure from another real bilateral consumer.

The public Editor-only integration surface contains only the exact shared types required for cooperation:

- cooperation protocol identity and version;
- protected-target association and Candidate-A protection data;
- fixed Candidate-B profile identity and its profile-specific attestation fields;
- invocation-local execution-ownership state required by the bilateral lifecycle.

It must not expose AMUSE's semantic engine, proof internals, purpose-specific planners, a generic IR, arbitrary theorem representations, or a public extension registry. DAO owns its bridge and exact-operation admission logic; AMUSE owns the meanings DAO consumes.

### Per-build execution ownership

Package presence does not authorize DAO's SDK callback to defer. The existing callback may defer only when execution of the cooperating bridge for that specific build invocation is positively guaranteed.

The per-build invariant is:

```text
normal DAO SDK callback XOR cooperating DAO NDMF bridge
```

Exactly one owns execution. Never both; never neither.

The design requires a build-local execution-ownership handshake or equivalent authoritative mechanism with states conceptually equivalent to:

- normal callback owns execution;
- bridge ownership positively armed for this invocation;
- execution started;
- execution completed or failed.

The exact flag or type is intentionally deferred, but ownership must not depend solely on package presence, callback discovery order, or accidental disappearance of the DAO component.

Ownership is single-use and invocation-scoped. A bridge failure is a terminal attempted execution: it aborts the normal build and must not permit the SDK callback to run DAO a second time. Completion, failure, or abandonment of an invocation must make its ownership state unusable by every later invocation.

The normal callback may defer only after it establishes that:

- this invocation has an active supported NDMF context;
- NDMF will execute the relevant PlatformFinish group for this invocation;
- the DAO bridge is registered and supported;
- AMUSE is active under a supported host contract;
- the bridge has positively accepted ownership.

If any condition is absent or uncertain, the normal callback retains ownership and behaves normally.

If normal DAO execution occurs because bridge ownership was not armed, the supported post-AMUSE cooperation contract is unavailable for that invocation. AMUSE must not emit protection metadata on the assumption that DAO will run later. V1 refuses the affected AMUSE positive capability unless a separate, explicitly characterized pre-AMUSE DAO state-based path has been established; this specification establishes no such path.

### Required lifecycle cases

- **Normal upload, supported NDMF and AMUSE active:** bridge ownership is armed; the normal callback defers; AMUSE then bridge execute exactly once.
- **NDMF ApplyOnBuild disabled:** bridge execution is not guaranteed; the normal callback must not defer.
- **AMUSE installed but inactive or unsupported:** bridge ownership is not armed; the normal callback retains normal DAO behavior.
- **Bridge unavailable or unsupported host version:** the normal callback retains normal DAO behavior.
- **DAO callback observed first at the `-1025` tie:** it may defer only after positive bridge ownership for this invocation has already been established; otherwise it executes normally.
- **NDMF observed first at the `-1025` tie:** the bridge executes once; a later DAO callback observes authoritative completion and does not execute again.
- **Modular Avatar moves DAO's callback to `-15`:** the same ownership protocol applies; callback order does not replace the handshake.
- **Apply-on-Play:** AMUSE positive mutation is unavailable in v1, so the AMUSE cooperation path is not armed. DAO follows its independently supported existing Play Mode behavior.

An implementation that cannot positively arm ownership before the first possible DAO callback does not satisfy this architecture. It must retain normal DAO execution and withhold AMUSE/DAO cooperation rather than risk both or neither.

### Bridge executable-viability gate

Architectural selection does not establish that current DAO can safely execute from an NDMF pass. The first DAO integration implementation has a hard validation gate. It must demonstrate that bridge execution:

- has an authoritative invocation-local mechanism that can positively arm bridge ownership before the earliest possible DAO `-1025` callback;
- makes that armed ownership observable to the normal DAO callback even when DAO is discovered first at the `-1025` tie;
- preserves exactly-once ownership when NDMF is discovered first at the same tie;
- preserves DAO configuration until consumed;
- executes exactly once;
- works in both `-1025` callback discovery orders;
- produces meshes, materials, and assets that survive `BuildContext.Finish()`;
- remains coherent with NDMF ObjectRegistry and context behavior;
- propagates normal build failures;
- leaves later PlatformFinish and `Finish()` processing coherent;
- preserves DAO-alone behavior;
- preserves AMUSE-alone behavior;
- behaves correctly with and without Modular Avatar.

If ownership cannot be positively established before that earliest callback, normal DAO behavior must run and AMUSE/DAO positive cooperation must be withheld. The implementation must never guess that the bridge will run.

Failure to demonstrate the pre-callback ownership mechanism or any other part of this gate reopens the DAO lifecycle mechanism. It must not weaken the ordering, ownership, failure, or preservation contracts silently.

## Cooperation metadata

Typed temporary `BuildContext` state is the selected protection/profile carrier because it is build-local, clone-local, public in NDMF 1.14.4, and naturally discarded with the build context. AMUSE and DAO must access it through the same exact AMUSE-owned runtime contract type.

The minimum Candidate-A target association contains only:

- cooperation protocol version;
- protected target kind;
- direct clone-local object association;
- renderer slot or protected-region relationship;
- logical AMUSE output role;
- Candidate-A protection requirement;
- optional Candidate-B profile identifier.

It supports multiple protected targets. It is not stored on original assets, does not affect runtime behavior, does not use user-visible names as authority, and does not use Unity instance IDs as durable identity.

Candidate-B records may add only the fixed normalized attestation fields required by that exact profile. This design does not require a generic proof-relevant fingerprint or a second canonicalization abstraction beneath the profile. Such a field may be added later only if concrete implementation evidence shows that the minimum association cannot prevent target confusion.

## Logical generated-output identity

Coexistence requires only build-local association, not a universal identity system. A target record relates the generated live objects to:

- the renderer slot or protected mesh region;
- the logical AMUSE output role;
- Candidate-A protection;
- the optional fixed Candidate-B profile.

DAO may merge or remap structures only through an explicitly admitted operation that maintains an operation-local mapping from the protected input relationship to its successor. No persistent GUID scheme, global object graph, or cross-build logical identity is introduced.

## Opaque target preservation profile

Candidate B uses one AMUSE-owned fixed identifier, conceptually:

```text
com.alrauna.amuse/opaque-target-preservation/v1
```

The exact spelling is an implementation detail. The version identifies one fixed theorem, one fixed set of normalized attestation fields, and one interpretation of every field. Adding, removing, or redefining a proof-sensitive obligation requires a new profile version.

The profile covers, as applicable to its eventual exact scope:

- protected triangle/material relationships;
- opaque render-state requirements;
- surviving-fragment alpha behavior;
- absence of new unmodeled coverage suppression;
- `_Invisible`, ID-mask, and UDIM suppression state;
- clipping, dither, dissolve, and related coverage behavior;
- culling and pass behavior;
- relevant topology, deformation, and UV assumptions;
- reachable animation and material-swap behavior.

AMUSE emits the profile only for a target state it has exactly proved and completely attested. DAO may understand several explicit profile versions. An unknown profile version always falls back to Candidate A.

The profile is not a generic assertion list or theorem language.

## Candidate A: mandatory safe floor

Candidate A is independently safe even if no Candidate-B operation has ever been certified. It is an allow-only policy:

```text
protected target
    -> explicitly characterized Candidate-A-safe operation
         -> allow that operation

    -> Candidate-B-certified operation
         -> Candidate B

    -> unknown or uncharacterized operation
         -> exclude or isolate the protected target
```

Candidate A must not mean that arbitrary DAO Basic or structural behavior is allowed whenever generic predicates appear to hold. Every allowed Candidate-A operation must be explicitly characterized against the current theorem-scoped DAO Basic conclusion.

Candidate-A-safe behavior may include only operations whose exact implementation has been established to preserve the protected relationship without proof-sensitive semantic rewriting. For example, structural consolidation may be admitted only when its characterized operation preserves:

- each protected triangle's association with the protected material semantics;
- proof-relevant vertex attributes, topology, deformation, culling, and pass behavior;
- renderer and material reachability;
- a lossless, explicit material-slot remap when slots change;
- all coverage-sensitive shader, property, texture, and UV behavior.

Candidate A forbids uncharacterized shader replacement or specialization, proof-sensitive property changes, proof-relevant texture or sampling changes, coverage suppression, proof-relevant UV overwrite, and unmodeled animation rewriting.

Before DAO mutation begins, inability to optimize a protected scope normally preserves or isolates the smallest safely identifiable scope. Build abortion is not the ordinary fallback. If isolation itself cannot be classified safely, DAO skips the broader affected optimization scope.

The shared lilToon shader must not be made globally incompatible. Protection is target-specific; unprotected materials using the same shader retain ordinary DAO behavior.

## Candidate B: exact operation admission

Candidate B is optional and operation-specific:

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

For v1, a closed table or switch is sufficient. Admission is keyed conceptually by:

- exact supported DAO integration identity;
- exact AMUSE profile identifier and version;
- exact supported shader and closure identity;
- exact DAO operation kind;
- exact required parameters and input state.

The admission mechanism is architecture. Certification of exact-value specialization, material merging, MaterialID use, texture arrays, UV transport, or other proposed operations is separate evidence and is not granted by this document.

“DAO Full is trusted” is never an admission rule. Unknown DAO revisions, profiles, shader closures, operations, arrangements, UV use, parameters, or attestations select Candidate A.

## Shader Toggles and fused planning

DAO Shader Toggles on AMUSE-protected geometry are outside Candidate B v1. `_IsActiveMeshN`, generated mesh IDs, vertex-stage suppression, and rewritten visibility animation form a new coverage system requiring a separate theorem. Candidate A excludes the protected target from such an operation.

Deep Candidate C cooperation remains rejected. There is no shared mesh planner, cross-optimizer IR, combined executor, or generic operation graph. Reconsider only if measurements later show substantial duplicated work or lost opportunity that Candidate A/B cannot address.

## Post-DAO remainder

For the current target theorem:

- characterized lilToon callback behavior is modeled;
- scoped VRCFury late parameter compression is irrelevant only under its tested revision and theorem;
- a concrete supported later actor may remain only through an explicit model, irrelevance result, or preservation contract;
- a known proof-relevant later actor without one of those dispositions refuses the affected capability.

No post-DAO AMUSE validation callback exists in the normal architecture. There is no later authorization state, build-attempt handoff, or second proof. Adding such a callback requires new evidence that the single-stage theorem is insufficient.

Current NDMF 1.14.4 PlatformFinish inspection found no concrete later writer that threatens the targeted alpha theorem. `GeneratePortableComponents` writes platform component state, and `CheckMipStreamingPass` diagnoses texture configuration rather than automatically mutating the target semantics. This fact is version- and theorem-scoped, not a universal PlatformFinish safety claim.

## Unknown-tool policy

An unknown tool before the barrier is acceptable when it has completed and AMUSE can fully extract and understand its proof-relevant resulting state. Tool identity is not required.

AMUSE has no generic obligation or mechanism to discover arbitrary remaining PlatformFinish writers. For concrete actors AMUSE explicitly supports or relies upon, public ordering, exact-version characterization, and bilateral contracts establish the remainder.

A known or detectable later writer that may change a proof-relevant domain refuses the affected capability until resolved. If reliable domain separation is impossible, refusal expands to the smallest safely established broader scope. A hypothetical unobservable future writer remains outside the v1 host assumption until concrete evidence makes it a compatibility requirement.

## Failure semantics

### Before first mutation

- Semantic uncertainty, unsupported reachability, unavailable lifecycle authority, and Candidate-B ineligibility normally preserve or skip the smallest affected scope.
- Deterministic preparation constructs and validates required outputs before assigning them.
- No rollback is required.
- Candidate-B failure selects Candidate A.
- Candidate-A inability isolates or skips the protected scope rather than normally aborting the build.

### After first mutation

- Any AMUSE failure that could leave inconsistent output aborts the normal build.
- If a DAO operation has begun mutation and cannot guarantee coherent output, the build aborts.
- Violation of a supposedly certified preservation invariant aborts the build.
- Rollback is not promised; the build clone is disposable.

Normal build failure must propagate through NDMF and the host so no invalid bundle or upload proceeds. Bridge viability testing must establish this behavior for DAO execution from PlatformFinish.

## Apply-on-Play

Positive AMUSE mutation is unavailable during Apply-on-Play in v1. The investigated NDMF path invokes SDK preprocessing but ignores its Boolean result, so ordinary build-abort behavior is not an enforceable safety boundary there.

The AMUSE/DAO cooperating bridge path is therefore not armed for Apply-on-Play. DAO retains its independently supported existing Play Mode behavior. AMUSE may analyze or report without mutation.

Apply-on-Play mutation requires a separate investigation of execution ownership, repeatability, generated assets, failure containment, and cleanup. It is not permanently impossible, but it is not inferred from normal-upload correctness.

## Host and version contracts

Exact tested versions participate wherever correctness depends on lifecycle behavior, including:

- NDMF phase order, public context state, generated assets, ObjectRegistry/context coherence, `Finish()`, and error propagation;
- VRChat SDK callback order and normal build-abort behavior;
- DAO bridge ownership and exact operation implementations;
- modeled lilToon or VRCFury behavior.

Unknown versions disable only capabilities whose required contract cannot be established. Pure analysis or unrelated proven capabilities may remain available. Semver alone is not evidence; support follows observed tests and source characterization.

The supported lifecycle assumption must be documented in diagnostics and compatibility documentation. AMUSE must not claim global authority over arbitrary editor code.

## Diagnostics

Interoperability is automatic; artists do not select compatibility modes. Diagnostics explain the conservative result for developers and power users.

Required categories include:

- AMUSE candidate accepted;
- semantic or reachability refusal;
- lifecycle or host refusal;
- AMUSE target protected;
- Candidate B admitted;
- Candidate B unavailable and Candidate A used;
- protected scope isolated or skipped by Candidate A;
- DAO cooperation unavailable;
- DAO execution ownership not guaranteed;
- unsupported DAO integration or profile version;
- shader/closure or profile-attestation mismatch;
- known later writer blocks the affected capability;
- post-mutation build abort.

Where possible, a diagnostic identifies the affected renderer, material, protected role, refused capability, and safe outcome. Diagnostics do not participate in correctness.

## Test architecture

No tests are added by this design branch. Later implementation must provide the following layers.

### Pure contract tests

- cooperation protocol and profile identity/versioning;
- Candidate-A allow-only behavior;
- unknown Candidate-A operations isolate or skip;
- Candidate-B exact operation admission;
- unknown versions, shaders, attestations, and operations fail closed;
- target association and multiple protected targets;
- candidate-scoped lifecycle refusal.

### Synthetic Unity integration tests

- MA followed by AMUSE;
- VRCFury followed by AMUSE;
- Meshia followed by AMUSE without AAO;
- Meshia followed by AAO followed by AMUSE;
- AMUSE alone and DAO alone;
- AMUSE followed by cooperating DAO, with and without Modular Avatar;
- both `-1025` callback discovery orders;
- NDMF ApplyOnBuild disabled;
- AMUSE installed but inactive or unsupported;
- bridge unavailable and unsupported host version;
- bridge ownership positively armed before the earliest possible DAO `-1025` callback;
- exactly-once DAO execution and authoritative ownership transitions;
- AMUSE and DAO resolving the same `BuildContext` state through the exact AMUSE-owned runtime contract type;
- generated DAO meshes, materials, and assets surviving `Finish()`;
- ObjectRegistry/context coherence and later PlatformFinish coherence;
- normal build failure propagation;
- representative combined pipeline.

Ordering tests must trace AMUSE completion and DAO start directly. Output coincidence is not sufficient evidence.

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

The fresh test extraction is an oracle for certification tests, not a production post-DAO authorization callback.

### Fallback and failure tests

- unsupported DAO revision, profile, operation, shader, or arrangement;
- Candidate B falling back to Candidate A;
- Candidate-A unknown operation isolating the smallest safe scope;
- Candidate A unavailable causing skip rather than ordinary pre-mutation abort;
- known later-writer refusal where public detection exists;
- AMUSE or DAO post-mutation failure aborting the build;
- no invalid output artifact proceeding after failure.

### Census and performance work

Census Lab may later measure coexistence frequency, refusal reasons, Candidate-A/B opportunity, and real-world optimization pressure. It never certifies correctness and is not used or modified by this design.

Controlled benchmarks may later measure renderers, material slots, transparent triangles, overdraw, GPU time, and relevant CPU/render-thread effects. No universal performance score is defined.

## Relationship to upload-conditional authorization

The earlier upload-conditional design remains a valid exceptional architecture for a future transformation whose proof genuinely depends on evidence produced after AMUSE must mutate.

It is narrowed as follows:

- it is not the default AMUSE lifecycle;
- it is not required by the current alpha theorem;
- the normal path carries no proof or authorization state across callbacks;
- the normal path has no late validation gate or build-attempt identity problem;
- AMUSE does not mutate speculatively while awaiting later approval.

A future capability that truly needs post-mutation evidence must independently satisfy the older architecture's stricter lifecycle, identity, fresh-extraction, cleanup, and failure-gate requirements. This document neither deletes nor broadens that exceptional design.

## Generality boundaries

Shared boundaries may support future transformations involving multiple renderers and meshes, material-slot restructuring, UV changes, generated textures, atlasing, material combining, control textures, and submesh merging. The shared concepts are limited to:

- semantic barrier and host lifecycle contract;
- candidate-scoped lifecycle capability;
- eager immutable host extraction;
- deterministic preparation before mutation;
- build-local generated-output association;
- first-mutation failure boundary;
- temporary post-AMUSE protection records.

Purpose-specific proof, plans, profile attestations, and operation certifications remain narrow. New general abstractions require a second concrete consumer or demonstrated pressure.

## Future pressure triggers

Reconsider this architecture only when concrete evidence establishes one of these pressures:

- an installed PlatformFinish writer changes an AMUSE theorem dependency;
- a supported NDMF version changes PlatformFinish mutation, context, asset, or failure guarantees;
- a public NDMF barrier/tail API materially simplifies a demonstrated requirement;
- a transformation genuinely requires post-NDMF evidence;
- Apply-on-Play demand is paired with proven failure containment;
- Candidate A causes material measured optimization loss;
- a separate DAO Shader Toggle theorem is developed;
- multiple real transformations require a broader immutable representation;
- measured duplicated DAO/AMUSE work justifies reconsidering fused planning;
- the DAO bridge fails its executable-viability gate.

The appropriate response to a trigger is a focused investigation. It is not automatic expansion into a generic compatibility framework.

## Implementation gates and unresolved details

Before production implementation can claim the architecture is available, later work must establish:

1. the exact supported host/version contract;
2. the exact build-local DAO execution-ownership mechanism, including authoritative arming before the earliest possible DAO `-1025` callback;
3. the DAO bridge executable-viability gate;
4. the minimal AMUSE-owned typed `BuildContext` cooperation record and one-way DAO-to-AMUSE-integration dependency;
5. exact Candidate-A-safe DAO operations;
6. the fixed opaque-profile fields;
7. any Candidate-B certification as a separate evidence-backed result;
8. the smallest reliable build-path classification that keeps Apply-on-Play mutation unavailable.

These are implementation and validation questions within the selected architecture. A failure of the DAO bridge viability gate specifically reopens the DAO lifecycle mechanism rather than weakening exactly-once execution or AMUSE-before-DAO.

## Completion criteria for a future implementation

A future implementation conforms to this specification only when it demonstrates:

- all relevant producers precede the AMUSE barrier;
- AMUSE performs eager extraction through mutation once;
- proof uses immutable captured values;
- Meshia precedes AMUSE without depending on AAO;
- AMUSE owns the exact shared protocol types and AMUSE core has no compile-time dependency on DAO;
- bridge ownership is positively armed before the earliest possible DAO `-1025` callback in both discovery orders;
- DAO execution has positive per-build ownership and exactly-once semantics;
- Candidate A admits only explicitly characterized safe operations;
- unknown Candidate A operations isolate or skip protected scope;
- Candidate B admits only exact certified operations and always falls back safely;
- generated assets survive and context/object associations remain coherent;
- post-mutation failures abort normal builds;
- no post-DAO authorization callback exists for the current path;
- Apply-on-Play positive mutation remains unavailable until separately proven;
- unsupported and unknown evidence never broadens optimization;
- no whole-tail scanner, reflection dependency, or generic plugin framework has entered the design.
