# AMUSE Upload-Conditional Authorization Design

## Status and scope

**Architecture approved in chat. Written specification awaiting review. This document is uncommitted.**

- Branch: `design/upload-conditional-authorization`
- Base: `origin/main` at `999147107996e2eaeceef49ff61bd3b4d28fc251`
- Base result: merge of PR #11, `docs: characterize lilToon build callback handoff`
- Intended repository change: this design document only
- Census Lab/private avatars: not used

This is a production architecture design, not an implementation plan. It defines how AMUSE can perform an NDMF-time transformation whose proof depends on facts that become authoritative only later in the normal VRChat avatar build/upload lifecycle. It carries forward the approved lifecycle outcome:

> Outcome B — NDMF conditional mutation plus a late validation gate.

No production source, test, assembly definition, manifest, lock file, Unity asset, project setting, CI workflow, census tooling, NDMF plugin, SDK callback, or positive lilToon integration is added or modified by this branch.

This branch does not invoke the implementation-planning workflow. A later implementation plan is blocked on review of this design, the general-purpose-boundary audit, and the SDK build-environment investigation.

## Prior decisions incorporated

This design is grounded in the current source tree and these completed records:

- `docs/architecture/vision.md`.
- `docs/architecture/shader-frontend-comparison.md`.
- `docs/superpowers/specs/2026-08-20-end-to-end-alpha-analysis-design.md`.
- `docs/superpowers/specs/2026-08-21-analysis-snapshot-ordering-design.md`.
- `docs/superpowers/specs/2026-08-21-liltoon-attestation-hardening-design.md`.
- `docs/superpowers/specs/2026-08-21-liltoon-official-integration-matrix-design.md`.
- `docs/superpowers/specs/2026-08-21-liltoon-build-callback-handoff-design.md`.

The following earlier conclusions are binding here:

1. AMUSE is fail-closed. Only sufficient proof permits transformation. More uncertainty cannot make AMUSE more aggressive.
2. Pure semantics, proof, and planning consume AMUSE-owned immutable values. Live Unity objects are not proof evidence.
3. One explicitly ordered late NDMF `Optimizing` pass is the principal mutation boundary for supported NDMF-visible state.
4. Within that pass, eager extraction, semantic interpretation, proof, planning, preparation, and mutation form one bounded operation. No supported plugin interleaves while the pass executes.
5. lilToon 2.3.4 generates authoritative upload-time shader state in its VRChat SDK callback at order `100`, after NDMF has finished.
6. Candidate A, late attestation plus late mutation, is rejected for the investigated lifecycle. It failed reliable new-asset serialization and introduced an unclosed lilToon regeneration cycle.
7. Candidate B is supported architecturally for the normal upload lifecycle only when lifecycle suitability is known before mutation and a complete late validation reliably aborts an invalid build.
8. Apply-on-Play is not an enforceable late-gate lifecycle under the investigated versions. Future-dependent conditional proof is unavailable there.
9. Late validation requires complete fresh extraction of every protected proof dependency. A shader hash or a separately handwritten late fingerprint is insufficient.
10. The final build is compared with the state AMUSE intended to create, not blindly with the original state and not with whatever mutation happened to produce.
11. Unity object identity may assist lookup or diagnostics but does not establish semantic equivalence. NDMF may replace or deduplicate semantically identical objects.
12. Unknown or uncharacterized proof-relevant callbacks after the gate make future-dependent positive proof unavailable.
13. Every positive lilToon proof that depends on callback-`100`-generated source, including standalone lilToon, uses the same late authorization lifecycle.
14. Cleanup cannot depend solely on SDK postprocess because preprocess refusal may skip postprocess entirely.

## Current production architecture

AMUSE currently has one Editor-only production assembly, `Alrauna.Amuse.Editor`. It references NDMF but contains no AMUSE NDMF plugin, mutation executor, SDK build callback, conditional handoff, or build association.

Already suitable immutable or pure components include:

- `MaterialSemantics` and its semantic value records.
- `AlphaSemanticsResolver`.
- `TriangleAlphaClassifier` and `ExactUvGeometry`.
- `MeshSeparationInput`, `MeshSeparationPlan`, and `MeshSeparationPlanner`.
- constructed Poiyomi and lilToon source-evidence values.

The existing host-facing vertical slice still mixes or defers live reads:

- `UnityRendererAlphaAnalysis` reads renderer, mesh, material, texture, and global state while also invoking semantic reasoning and planning.
- `UnityAlphaFieldEvidence` retains live `Texture2D` references and extracts pixel evidence lazily.
- both shader frontends gather copied source-attestation evidence, then interpret a live `Material` again.
- `UnityTextureEvidence` reads texture, importer, and AssetDatabase state through separate calls.
- global color space is read during semantic interpretation.
- no production animation/material-reachability extractor exists.

This means the pure alpha algorithms do not need a new architecture, but host extraction must become eager and coherent before it can serve conditional authorization.

## Goals

- Authorize future-dependent transformations only when the current invocation has an enforceable late commit gate.
- Keep proof and mutation at the correct late NDMF point.
- Derive the intended post-transformation state deterministically before mutating Unity.
- Use the same canonical domain definitions for prepared expectations and fresh late extraction.
- Protect every proof dependency covered by the authorization, including intentionally unchanged facts and relationships.
- Compose multiple conditional transformations into one conflict-free build-wide authorization.
- Merge and conflict-check both expected-state claims and future-attestation requirements before mutation.
- Keep prepared immutable values separate from live application targets.
- Associate one single-use conditional batch with the correct build attempt.
- Keep the late SDK gate small, read-only, and fail-closed.
- Separate general host lifecycle compatibility from shader-specific future-attestation compatibility.
- Preserve a path to multi-renderer, multi-mesh, UV, texture, material, and slot-topology transformations without implementing them.
- Remain concrete enough for a later audited implementation plan to identify files, types, seams, and validation tasks.

## Non-goals

This design does not create or specify:

- production code or tests.
- an implementation plan.
- an AMUSE NDMF plugin or VRChat SDK callback.
- upload-path detection or callback discovery by reflection.
- standalone or integrated positive lilToon support.
- LTCGI, AudioLink-package, or VRC Light Volumes support.
- an Apply-on-Play conditional lifecycle.
- a generic transformation interface, provider registry, factory, or public extension API.
- a transformation or mutation DSL.
- a generic dependency or sequencing graph.
- a universal avatar snapshot or semantic asset database.
- a universal Unity-object identity or addressing system.
- a transactional object store or generalized asset compiler.
- a shader IR.
- atlas, UV-repacking, material-combining, or control-texture algorithms.
- a complete animation/reachability system.
- a broad lifecycle compatibility matrix.

## Terminology

### OriginalSnapshot

The immutable collection of proof-relevant facts eagerly captured for one attempted proof at the justified NDMF lifecycle point. It is a conceptual role composed from small records, not necessarily one object.

### Plan

A purpose-specific deterministic description of a safe candidate transformation. The current `MeshSeparationPlan` remains an alpha-separation plan and is not the general transformation model of AMUSE.

### Transformation preparation

The purpose-specific deterministic step that consumes `OriginalSnapshot + Plan` and produces both the concrete immutable output descriptors AMUSE intends to apply and the expected canonical proof-relevant post-transformation contribution. Preparation occurs before Unity mutation.

### Prepared transformation

Transformation-specific immutable values created by preparation. This term does not imply a shared interface, base class, registry, or DSL.

### Application targets

Live host handles used only to locate and apply prepared outputs. They are kept outside proof and prepared values. Their identity does not establish proof validity.

### ExpectedTransformedState

The composed canonical state that must exist after all work represented by one conditional batch. It includes modified outputs, unchanged proof dependencies carried forward from `OriginalSnapshot`, and proof-relevant relationships between domains.

### Future-attestation requirement

A concrete immutable condition over evidence that becomes authoritative after NDMF, such as a lilToon build-generated source/profile condition.

### Conditional batch

The complete set of compatible future-dependent transformations for one build attempt, after expected-state and future-attestation composition succeeds. Not every AMUSE transformation belongs to this batch.

### Conditional authorization

The immutable, build-associated contract retained across NDMF and the late SDK gate. It contains the recorded host capability assumptions, complete expected state, composed future requirements, validation scope, and diagnostic identities.

### FreshFinalSnapshot

The complete freshly extracted canonical state required to validate one conditional authorization at the late gate.

### Host lifecycle capability

The bounded result that states whether the exact invocation, host versions, callback environment, ordering, failure behavior, build association, and recovery behavior constitute an enforceable late gate. The mechanism that establishes this capability remains an investigation prerequisite.

### Semantic address

A domain-owned key used to resolve an expected claim to exactly one represented final-state counterpart. Address resolution is separate from value equality.

### Logical output identity

A deterministic transformation-owned identity assigned during preparation to an intended generated output. Mutation maps it to a Unity object. Late validation resolves its represented counterpart. An asset name or Unity instance ID alone is not a logical output identity.

## Considered architectures

### Transformation-specific preparation plus a shared authorization contract — selected

Small shared extractors produce canonical immutable records. Purpose-specific proof and planning remain narrow. A transformation-specific preparation step produces the exact immutable outputs and expected-state contribution from one source. Build-wide keyed composition detects conflicts. Mutation combines prepared values with separate live targets. A late gate reuses the same extraction definitions and validates the composed authorization.

This is the smallest model that prevents expectation/mutation drift while avoiding a generic transformation framework.

### Plans carry host postconditions directly — rejected

Adding output layout and late validation semantics directly to `MeshSeparationPlan` would mix an alpha proof plan with host mutation and authorization concerns. It would also tempt shared code to adopt the current one-mesh, opaque/transparent shape.

### Generic operation graph or transaction DSL — rejected

A graph or DSL could encode hypothetical future asset transformations but has no second concrete transformation to constrain it. It would introduce generic sequencing, dependency, mutation, and asset concepts before AMUSE has evidence for their shape.

## Lifecycle overview

```text
supported invocation and callback environment
        ↓
host lifecycle capability
        ↓
late ordered NDMF Optimizing pass
        ↓
purpose-scoped eager OriginalSnapshot extraction
        ↓
semantic interpretation → proof → purpose-specific Plan
        ↓
transformation-specific preparation
        ↓
immutable prepared outputs
+ canonical expected-state contribution
+ future-attestation contribution, when required
        ↓
build-wide composition and conflict detection
        ↓
reserve build-associated conditional authorization
        ↓
preflight application targets and output prerequisites
        ↓
begin first build-clone mutation or generated-output application
        ↓  PRE-MUTATION / POST-MUTATION SAFETY BOUNDARY
apply remaining prepared values to separate live targets
        ↓
arm authorization
        ↓
NDMF Finish
        ↓
lilToon callback 100 and other supported pre-gate mutators
        ↓
AMUSE late SDK gate
        ↓
atomically consume armed authorization
+ re-establish recorded host capability assumptions
+ complete fresh shared extraction
+ exact canonical expected/final comparison
+ concrete future-attestation validation
        ↓
complete match → authorize continuation
anything else → abort build
```

The late gate is an authorization point, not a second optimizer. It never plans, prepares, mutates, repairs, or regenerates.

## Production responsibility boundaries

### Host lifecycle assessment

Before future-dependent evidence may participate in proof, host-facing code must establish a capability showing that this exact build attempt has an enforceable late commit gate. It owns host version, invocation, callback environment, ordering, failure, association, and reload questions. It does not own shader-specific source compatibility.

The rest of AMUSE consumes the capability as an immutable assumption. Host-version checks must remain centralized conceptually here rather than becoming scattered checks in extractors, planners, preparers, mutation code, validation comparisons, or shader semantics.

### Shared eager extraction

Host extraction converts bounded live Unity, Editor, file, package, and global state into small AMUSE-owned canonical immutable records. Extractors own no optimization decision. They may refuse when required facts cannot be captured completely.

NDMF and late SDK extraction use the same definitions. The two calls may request different scopes, but they cannot use different meanings for the same field or domain.

### Pure semantic interpretation, proof, and planning

These layers consume captured values only. Existing alpha algorithms stay purpose-specific. Adding a new semantic or optimization domain does not require changing the conditional lifecycle unless it reveals a genuinely missing shared responsibility.

### Transformation-specific preparation

Preparation is where AMUSE deterministically describes what it intends to create. A concrete preparer produces immutable output descriptors, canonical expected-state claims derived from those descriptors, logical output identities, protected relationships, and any future requirements required by that proof.

Prepared outputs and expected semantics come from the same result. The executor does not independently regenerate a second interpretation of the plan, and expected state is not defined by rereading mutation output.

### Expected-state and requirement composition

A small keyed composer merges expected canonical claims and concrete future-attestation requirements. It detects incompatible overlap before mutation. It is not a dependency graph and does not infer sequencing.

### NDMF mutation boundary

Host mutation combines immutable prepared values with separate live application targets. It owns Unity and NDMF asset creation, assignment, nondestructive build-clone mutation, and target application guards. It does not own proof semantics.

The pre-mutation/post-mutation safety boundary is crossed when the first write to the build clone or the first generated-output application starts. It is not delayed until the complete conditional batch succeeds or the handoff becomes armed. Before that first application, target and output preflight may still refuse the conditional transformation without changing the clone. Once any application has occurred, the clone may be only partially transformed. Every later failure must fail the build. Build abort, not rollback or undo, is the safety mechanism.

### Conditional authorization handoff

A bounded process-local store associates one composed conditional batch with one build attempt. It owns reservation, arming, atomic single-use consumption, cleanup, legitimate concurrency, proven supersession, and stale-state refusal. Stored proof-bearing values contain no Unity objects.

### Late SDK gate

The gate resolves the build association, consumes one armed authorization, verifies the original host capability contract still holds, requests complete fresh extraction, compares canonical state, validates concrete future requirements, and returns success or failure.

### Shader-specific future attestation

Shader-family code owns its package/source/profile rules and canonicalization. It consumes captured values and returns a concrete compatibility decision. It does not determine whether the host lifecycle supplies an enforceable commit gate.

### Diagnostics

Diagnostics distinguish expected conservative refusal from AMUSE invariant failure, retain bounded transformation/proof identities, and report mismatched domains without becoming a generalized logging framework.

### Existing types and required seams

The design preserves or changes current responsibilities as follows. Names for new concepts are descriptive and may be adjusted after the prerequisite audit. The information and ownership boundaries are fixed.

| Current area | Design consequence |
|---|---|
| `MaterialSemantics` and semantic value records | Remain reusable normalized semantic values. They are not the complete host validation state. |
| `AlphaSemanticsResolver`, `TriangleAlphaClassifier`, `ExactUvGeometry` | Remain pure, alpha-specific proof logic. |
| `MeshSeparationPlan` and planner | Remain purpose-specific and immutable; no shared transformation base is added. |
| `UnityRendererAlphaAnalysis` | Its live renderer/mesh/material extraction must be separated from pure alpha reasoning and planning. Its one-renderer result may remain an alpha-specific facade if useful. |
| `UnityAlphaFieldEvidence` | Its alpha contract may remain narrow, but its proof bytes must be captured eagerly rather than through retained `Texture2D` objects. |
| `UnityTextureEvidence` | Remains a host extraction source for the facts its consumers actually need. Calls required by one proof must feed a coherent immutable capture. |
| Poiyomi/lilToon material frontends | Split live material/source capture from interpretation so interpretation consumes captured values only. Shader-specific equations and diagnostics remain separate. |
| `LilToonSourceAttestation` | Existing canonicalization and identity logic remain shader-specific building blocks. Future callback-generated expectation profiles and late capture are separate reviewed work. |
| Existing Editor assembly | May continue hosting most production logic. Only a concrete SDK dependency may justify a thin additional Editor assembly. |

New production concepts genuinely required by this architecture are bounded canonical host records, transformation-specific alpha preparation, keyed expected-state/future-requirement composition, host lifecycle capability, one build-associated conditional authorization/store, and the small late validation boundary. This list does not imply one class per concept.

## Dependency and assembly boundaries

- Pure proof values, semantic interpretation, planning, preparation logic, composition, and canonical equality contain no live `UnityEngine.Object`, NDMF state, VRChat SDK state, AssetDatabase access, or Editor state.
- Existing pure values may continue using immutable Unity value structs such as `Vector2` and `Vector3`. Host independence here means no live object or lifecycle dependency, not necessarily zero `UnityEngine` reference.
- Unity and `UnityEditor` dependencies belong in extraction, application-target resolution, mutation, source/package capture, and host diagnostics.
- NDMF dependencies belong in the thin ordered-pass and generated-asset boundary.
- VRChat SDK dependencies belong in a thin late-gate boundary. A separate optional Editor assembly may be appropriate because the public project does not currently install the SDK callback APIs, but exact package references, assembly names, and conditional compilation await the SDK investigation.
- Shader-specific attestation code consumes captured values and stays independent of NDMF and SDK lifecycle types.
- No assembly split is required solely to mirror every conceptual responsibility. Existing namespace and test boundaries may remain until a real dependency needs stronger enforcement.

## Shared immutable host extraction model

### Scope

There is no universal `AvatarSnapshot` and no snapshot manager.

NDMF extraction captures the facts required by the proof being attempted. Late extraction captures every represented domain and relationship required to validate the complete composed conditional authorization. The latter is the union of protected scopes across conditional transformations and future requirements.

Existing transformations need not carry records for irrelevant future domains. New concrete needs may introduce new small domain records.

### Minimum current renderer-alpha migration

The current alpha vertical slice requires at least:

- a renderer record containing renderer kind, diagnostic location, property-block refusal fact, mesh relationship, and ordered material-slot structure.
- a mesh record containing copied topology, indices, positions, required UV data, submesh layout, and submesh-to-slot relationships.
- a material capture containing shader identity and every material value, texture binding, texture transform, keyword, tag, render-mode fact, or other input consumed by the supported frontend.
- texture sampling/importer/source facts required by the material equations.
- eagerly copied alpha texture evidence required by `AlphaSemanticsResolver` and `TriangleAlphaClassifier`.
- captured proof-relevant global settings such as color space.
- enough reachable-state evidence to establish that the relevant state of the attempted proof is static or completely represented.

The migration must:

1. stop `UnityAlphaFieldEvidence` from retaining a live `Texture2D` for later proof reads.
2. separate live material capture from Poiyomi/lilToon interpretation.
3. prevent a shader frontend from attesting copied source and then rereading a live material as proof input.
4. separate renderer/mesh/material extraction from `UnityRendererAlphaAnalysis` reasoning and planning.
5. capture global values once rather than reading them during pure interpretation.
6. retain live renderer/mesh/material handles only as immediate application targets outside the proof model.

### What may remain live

Live objects may exist transiently inside an extraction call and may be retained separately as immediate mutation targets. Diagnostic instance IDs may be retained as non-authoritative context. No proof, plan, prepared value, expected-state value, or handoff proof value may depend on later live reads.

### Animation and material reachability

The incomplete animation/material-reachability support is a coverage limitation, not necessarily a blocker for all infrastructure or all production cases.

Initial production work may support a bounded case when extraction positively establishes that every relevant renderer/material/property state is static or otherwise completely represented. A case requiring animation, material-swap, or reachable-value reasoning that AMUSE cannot yet perform returns `Unknown` before mutation.

A later animation-reachability milestone expands the extraction vocabulary and proof coverage. It does not require a new conditional-authorization lifecycle.

### Purpose-specific evidence remains purpose-specific

Eager alpha bytes may remain an alpha-specific proof input. This design does not generalize all texture content or UV evidence in anticipation of atlasing. The follow-up audit will determine whether any shared texture or UV boundary introduced during migration has become unnecessarily alpha-specific.

## OriginalSnapshot, Plan, and preparation

`OriginalSnapshot` proves why a plan is safe. It is immutable and purpose-scoped.

`Plan` describes the purpose-specific candidate. It remains separate from mutation. The current `MeshSeparationPlan` may continue carrying one mesh and alpha-specific opaque/preserved membership because that is its real algorithmic purpose.

Preparation performs the deterministic transition:

```text
OriginalSnapshot + Plan
    → transformation-specific immutable prepared outputs
    + canonical expected post-transformation contribution
    + deterministic logical output identities
    + protected relationship claims
    + concrete future-attestation contribution, if required
```

The prepared output descriptor is the source from which the host creates Unity output. Its canonical semantic projection is produced in the same preparation operation. There is no separate mutation-generation algorithm and expectation-generation algorithm that may drift.

For alpha separation, the first preparer may be an alpha-specific concrete type or function. This design does not require it to implement a shared transformation interface.

## ExpectedTransformedState

`ExpectedTransformedState` ultimately covers the complete proof-relevant state protected by the conditional authorization, not only fields AMUSE modifies.

For every original dependency used by the proof, preparation or composition determines its expected post-transformation value:

- modified dependencies receive their prepared expected value.
- intentionally unchanged dependencies carry their original canonical value forward.
- removed or replaced domains receive explicit intended absence/replacement relationships.
- newly generated domains receive deterministic logical output identities and canonical values.

An unexpected change to an intentionally unchanged material, texture, shader, animation, slot, relationship, package fact, or global setting can invalidate the proof as surely as a transformed mesh mismatch.

Expected state is composed from small records and bounded relationship records. It is not one ever-growing class with fields for every possible optimization.

## Canonical domains, addressing, and relationships

### Symmetric canonicalization

Expected and final values use the same canonical domain constructors and definitions before comparison:

```text
prepared output descriptor → domain canonicalizer → expected canonical value
fresh host extraction      → same canonicalizer   → final canonical value
```

The gate never compares raw prepared host data with a separately normalized extracted value.

Exact equality is the default. A normalization is permitted only when it is narrow, explicit, source- or behavior-proven, and owned by the affected domain.

### Address resolution precedes equality

For every expected claim, the late gate first resolves its semantic address to exactly one represented final-state domain. Only then does it compare canonical values.

- missing correspondence refuses.
- ambiguous correspondence refuses.
- a different Unity object with the same complete canonical represented value may compare equal.
- Unity identity may aid association, lookup, or diagnostics but does not establish semantic identity.

Concrete domains own their bounded addresses. The current renderer-alpha slice may use a verified renderer diagnostic path/kind and its ordered mesh/slot relationships. A future generated output may use a deterministic logical identity established by preparation. This does not justify a generic object-addressing framework.

### Protected relationships

Proof-relevant associations are protected state, not implicit consequences of individually equal objects. Concrete records or bounded relationship records must represent relationships such as:

- renderer to ordered material slots.
- renderer and mesh to submesh/material-slot mapping.
- material to texture bindings.
- material to texture transforms and sampling assumptions.
- generated control or atlas texture to consuming material input.
- UV set/layout to texture region where the proof depends on that mapping.
- animation binding or material swap to its reachable target.

Individually matching renderer, mesh, material, and texture records do not authorize a build whose relationships differ.

### Logical output identities

Transformation-owned logical output identities are created deterministically during preparation and remain in the intended-state description. Mutation maps them to actual generated Unity objects. Late extraction must resolve their final represented counterparts unambiguously.

Names, asset paths, GUIDs, or instance IDs may participate only when their exact domain contract makes them reliable location evidence. An asset name or instance ID alone is never proof identity.

## Conditional-batch composition

Only transformations that depend on the late commit enter the conditional batch. A transformation whose complete proof is authoritative during NDMF may remain independent of this upload-only lifecycle.

Before conditional mutation, composition merges both:

1. canonical expected-state and relationship claims. And
2. concrete future-attestation requirements.

For each domain-owned key or concrete future requirement:

- disjoint claims combine.
- identical claims coalesce.
- compatible constraints combine into the stricter compatible requirement.
- incompatible claims refuse the whole conditional batch before mutation.

There is no implicit ordering and no last-writer-wins behavior.

For the first lilToon consumer, compatible/incompatible future requirements are decided by concrete lilToon expectation logic. Conflicting package identities, generated-source profiles, activation tuples, closure requirements, or other attestation conditions refuse before mutation. No attestation registry is required.

If one transformation genuinely consumes the output of another transformation, they must currently be prepared as one coordinated transformation, handled through an explicitly fixed known preparation order justified by that concrete vertical slice, or conservatively refused. A generic sequencing graph waits for a real second transformation to establish its requirements.

## Interaction with non-conditional transformations

Keyed composition can detect only represented effects. Therefore, before conditional authorization is finalized, every AMUSE-controlled transformation capable of affecting a protected domain must satisfy at least one of these conditions:

- its effect is already reflected in the conditional starting state.
- it contributes its exact expected effect to conditional composition.
- it is proven disjoint from the protected domains and relationships.
- it is coordinated with the conditional transformation.
- or the combination is conservatively refused.

An independently authoritative transformation may not mutate a protected domain after conditional composition unless its effect is already part of the authorization.

This is a bounded orchestration rule for the known transformations of AMUSE, not generic side-effect or dependency tracking.

## Host lifecycle capability

### Required production capability

Before future-dependent proof can authorize mutation, AMUSE needs an immutable capability establishing:

- a supported normal avatar build/upload invocation.
- exact characterized Unity, NDMF, and SDK lifecycle versions.
- a reliable build-attempt association.
- an enforceable late SDK refusal gate.
- an actual callback environment in which all supported proof-relevant semantic mutators precede the gate.
- explicit characterization of any later callback as invariant-preserving.
- failure semantics that prevent prefab/bundle creation and upload.
- safe cancellation and domain-reload behavior for the supported contract.

If the capability cannot be established, future-dependent evidence is unavailable and no conditional mutation occurs.

### Mechanism remains unresolved

This design does not assume a production upload detector, callback reflection scheme, numeric gate order, equal-order behavior, alphabetical ordering, or `int.MaxValue` finality.

`investigate/sdk-build-environment-contract` must prove the actual mechanisms for:

- supported upload-path detection before mutation.
- callback inventory inspection or an equivalent enforceable contract.
- ordering guarantees available to AMUSE.
- exact build-attempt association.
- version identity.
- domain reload and cancellation behavior.

The architecture consumes the proven capability. It does not conceal the missing mechanism behind an optimistic Boolean.

### Capability is part of authorization assumptions

The authorization records the exact host capability contract under which mutation was allowed. At the late gate, the current invocation and callback environment must still satisfy that recorded contract. It is insufficient for the current environment to appear independently supported under a different contract.

## Callback-environment authorization

Callback authority is checked twice:

1. before mutation, to decide whether conditional capability exists at all.
2. at the late gate, to prove the current environment still satisfies the recorded capability and that the gate remains authoritative.

The policy is:

```text
all proof-relevant supported mutators before gate
+ every later callback specifically invariant-preserving
+ no unknown/equal-order ambiguity affecting finality
        → callback environment may authorize

anything else
        → no pre-mutation capability or late refusal
```

The probe order `200` from the investigation is evidence from one controlled environment, not a production answer.

## Build association and handoff model

### One conditional batch per build attempt

After composition, one immutable conditional authorization covers the complete batch for one build attempt. It may retain multiple transformation/proof diagnostic identities and protect multiple renderers and assets.

Its conceptual contents are:

- unique AMUSE batch/attempt token.
- proven host build association.
- recorded host lifecycle capability contract.
- complete composed `ExpectedTransformedState`.
- complete protected extraction scope.
- composed concrete future-attestation requirements.
- bounded diagnostic identities and context.

These are conceptual fields, not a requirement for one giant class. Domain records may remain separately composed values.

### Process-local store

The handoff is a bounded process-local map, not a single global current-build variable and not persistent proof storage. It supports separate legitimate concurrent build attempts when the host association proves they are distinct.

A newer attempt invalidates an older entry only when the host association mechanism proves that it supersedes the same logical build attempt. Ambiguity refuses rather than guessing or deleting the authorization of another attempt.

The root instance ID of the disposable probe proved same-process viability. It does not by itself define the production association contract and is not semantic proof.

### Reservation, arming, and single use

The minimum handoff state progression is:

```text
reserved → mutate → armed → atomically consumed → validated or refused
```

- A reserved entry cannot validate.
- An armed entry is eligible for exactly one validation attempt.
- Atomic consumption prevents repeated or competing validation.
- Only a complete successful late validation authorizes continuation.
- No handoff state by itself implies acceptance.

The NDMF boundary:

1. reserves the exact immutable batch.
2. performs every available target, association, and generated-output prerequisite check before the first write.
3. crosses the safety boundary when the first build-clone mutation or generated-output application starts.
4. applies the remaining prepared outputs.
5. arms only after the complete conditional batch succeeds.
6. removes the reservation on failure.
7. permits conservative skip only when failure occurred before step 3 and the clone remains unmodified.
8. fails the NDMF build when any failure occurs at or after step 3, including halfway through a multi-transformation or multi-output batch, or when arming fails.

AMUSE does not attempt transactional rollback, undo, or continuation as though a partially applied batch had never begun.

The late gate atomically takes the armed entry into a local immutable value and removes store state in a guaranteed cleanup path.

### Association failure

Missing, ambiguous, stale, superseded, unarmed, already-consumed, or mismatched authorization after conditional mutation aborts. The gate never falls back to a nearby or most-recent entry.

## Cleanup and recovery

AMUSE-owned handoff state follows these rules:

- **Successful validation:** consume and remove before allowing continuation.
- **Validation refusal:** remove in a `finally`-equivalent path and abort.
- **Failure before first mutation:** remove the reservation. When the clone remains unchanged and no independent host error requires failure, conservatively skip the conditional transformation.
- **Failure or exception after first mutation starts:** remove the reservation and fail the NDMF build, even when only one output or one prepared transformation was applied.
- **Arming failure:** remove the reservation and fail the NDMF build because mutation has already completed or partially completed.
- **Late exception:** remove and abort. Never convert exception into acceptance.
- **Legitimate concurrent attempt:** retain its independent entry.
- **Proven superseding attempt:** invalidate the replaced attempt. A late callback from it refuses.
- **Ambiguous relationship between attempts:** preserve isolation and refuse the ambiguous operation rather than guessing.
- **Detectable cancellation:** remove the associated entry.
- **Repeated callback:** no armed entry remains, so refuse.
- **Postprocess:** may perform defensive cleanup but is not required for correctness.
- **Editor restart/startup:** start with an empty store. No proof-bearing state is restored from disk.
- **Domain reload:** process-local state is lost. Conditional capability remains unavailable unless the SDK investigation proves the active build necessarily aborts or establishes another bounded safe marker.

No proof-bearing authorization persists across restart. A persistent handoff would add stale-proof and recovery complexity without a demonstrated need.

## Late validation flow

The late SDK gate executes this exact responsibility sequence:

1. resolve the host build association.
2. atomically take exactly one matching armed authorization.
3. verify that the current lifecycle, versions, callback inventory, ordering, and failure behavior still satisfy the recorded capability contract.
4. collect the complete protected extraction scope.
5. freshly extract every protected domain and relationship using the shared definitions.
6. resolve every expected semantic address to exactly one final represented counterpart.
7. compare canonical expected and final values exactly.
8. capture final shader/package/source/closure evidence required by future conditions.
9. validate every composed concrete future-attestation requirement.
10. authorize continuation only on complete success.
11. otherwise abort, with guaranteed AMUSE cleanup.

The gate does not optimize, plan, prepare, mutate, repair, regenerate lilToon, weaken proof requirements, or silently ignore missing evidence.

## Semantic equality rules

Each completely captured domain has one canonical immutable representation. Expected preparation and fresh extraction use the same canonicalization before exact comparison.

Examples of exact represented equality include:

- ordered slot and binding sequences.
- renderer kind and protected renderer facts.
- mesh topology, index order, relevant vertex attributes, UV values, and submesh relationships.
- shader identity, render mode, and every material input consumed by proof.
- material-to-texture assignments, transforms, and sampling assumptions.
- texture source/importer/sampling/content evidence consumed by proof.
- animation bindings, swaps, and reachable values within the supported model.
- proof-relevant globals.
- package, source, include, activation, and closure evidence.

Floats compare as exact captured represented values. There is no epsilon or heuristic similarity.

A digest may represent complete bytes only when the domain explicitly defines it as its canonical value and accepts the documented collision-resistance assumption. A digest cannot compensate for omitted fields.

Domain normalization is allowed only when proven. lilToon canonicalization, for example, may normalize generator-variable source regions only alongside the raw/Layer-2 and activation evidence needed to preserve distinctions removed by that normalization.

AMUSE does not claim universal semantic equivalence for Unity objects. Equality is defined only over completely represented domains whose relevance to the proof is understood. Unrepresented proof-relevant state is `Unknown` or refusal.

## Future-attestation boundary and lilToon integration

The host authorization mechanism is not lilToon-specific. It carries concrete future requirements and, once that separately reviewed work exists, invokes the concrete validator selected for the first vertical slice without introducing a provider registry.

For a future lilToon 2.3.4 condition, the immutable expectation may require:

- the generated canonical shader/pass digest pair.
- raw and Layer-2 evidence.
- the exact activation tuple, including inactive controls.
- lilToon package identity/version.
- include and external executable closure.
- macro and inclusion evidence.
- any shader-family-specific material/source relationship required by the proof.

The late lilToon frontend captures authoritative build-generated evidence and validates the concrete expectation. Host lifecycle capability remains separately owned.

Every positive lilToon proof dependent on callback-`100`-generated source, including standalone lilToon, must use this model. This document does not add final production expectations, pins, or validators and does not enable standalone, LTCGI, AudioLink-package, or VRC Light Volumes positive support.

## Failure and diagnostic model

### Binding pre-mutation/post-mutation invariant

Before the first build-clone mutation or generated-output application starts:

> Uncertainty means no conditional transformation. The unmodified or otherwise independently safe build may continue.

From the moment the first build-clone mutation or generated-output application starts:

> The clone may be partially transformed. Any later target failure, generated-asset failure, mutation failure, exception, reservation/arming problem, missing evidence, association problem, capability drift, mismatch, or validation failure aborts the build because AMUSE can no longer safely fall back to the pre-transformation clone state.

The boundary is the first application, not successful completion of the batch and not transition to the armed handoff state. Build abort is the recovery mechanism. This design does not require transactional rollback or undo.

### Expected conservative refusal

Examples include:

- unsupported lifecycle or host version.
- callback environment not proved authoritative.
- future-dependent proof requested on Apply-on-Play.
- incomplete extraction.
- unsupported material or texture evidence.
- animation/reachability outside the currently represented static domain.
- incompatible prepared claims discovered before mutation.
- unavailable shader-specific future compatibility.
- target, association, or output-prerequisite preflight failure discovered before the first write.

These produce no conditional mutation. Existing domain-specific refusal/result conventions should be reused where practical.

### Post-mutation build refusal

Examples include:

- missing, ambiguous, stale, superseded, or unarmed authorization.
- current capability no longer satisfying the recorded contract.
- missing or ambiguous semantic address resolution.
- protected canonical state or relationship mismatch.
- final shader attestation failure.
- package/closure/activation mismatch.
- exception during late extraction or validation.
- any target, generated-output, mutation, reservation, or arming failure after the first application starts.

These abort before serialization/upload.

### AMUSE invariant defect

Examples include:

- contradictory prepared claims escaping pre-mutation composition.
- a reserved/armed state transition that should be impossible.
- mutation not consuming the preparation result it was given.
- expected projection contradicting its own prepared outputs.
- accepting partial validation.
- using live Unity data as hidden proof input.

A late mismatch may result from either an external mutator or an AMUSE defect. The safety response is identical: abort. Diagnostics should preserve enough bounded detail to identify the transformation/proof, stage, semantic address, domain, and mismatch category.

The design does not require one universal error enum. Host-level results may use a small stable category set and retain domain-specific details from existing result types.

## Failure modes

| Failure point | Representative condition | Required response |
|---|---|---|
| Capability assessment | unsupported invocation/version, unproved callback authority, unreliable association, unsafe reload contract | future-dependent evidence unavailable; do not mutate |
| NDMF extraction/proof | missing canonical field, unsupported material/texture/static-state evidence | `Unknown` or conservative refusal; otherwise-safe build may continue |
| Preparation/composition | contradictory expected claims, incompatible future requirements, unaccounted protected-domain interaction | refuse the conditional batch before mutation |
| Reservation/application preflight | reservation, target guard, association, or generated-output prerequisite fails before the first build-clone write/application | remove reservation; the unchanged otherwise-safe build may continue |
| Mutation/application/arming | any first application has occurred, then a later target, generated asset, output application, prepared transformation, exception, reservation, or arming operation fails | remove reservation and fail the NDMF build; do not roll back or continue the partially transformed clone |
| Late association/capability | missing or ambiguous entry, wrong attempt, stale/superseded entry, capability drift, callback environment drift | consume/clean what is safely attributable and abort |
| Late extraction/addressing | incomplete fresh capture, missing or ambiguous semantic counterpart | abort |
| Late equality | any protected canonical value or relationship differs | abort |
| Future attestation | source, profile, package, activation, include, macro, or closure condition fails | abort |
| Late internal exception | extraction, comparison, or validator throws | guaranteed AMUSE cleanup and abort |

No failure at or after the first application degrades into an optimization skip because the build clone may already be partially changed.

## Initial host-version support policy

The initial upload-conditional host lifecycle is characterized against exactly:

- Unity `2022.3.22f1`.
- NDMF `1.14.4`.
- VRChat SDK Base/Avatars `3.10.4`.

Exact host version gating is centralized in the host lifecycle capability. Installation or package resolution does not imply conditional support.

The package currently permits `nadena.dev.ndmf >=1.14.4 <2.0.0-a` and resolves 1.14.4 in the public project. The broader install range need not be changed merely to express the narrower first conditional capability. On an uncharacterized host version, the conditional path is unavailable unless a separate reviewed contract supports it.

Shader-specific version compatibility remains separate. lilToon `2.3.4` is the characterized first family, but a lilToon profile does not define which SDK/NDMF lifecycle is safe.

If the SDK environment investigation cannot establish reliable exact version identity or shows that exact support is impractical under existing public APIs, a separate host-version decision/investigation is required before implementation.

## Security and correctness invariants

1. False negatives are acceptable. False positives are correctness defects.
2. More uncertainty cannot increase optimization.
3. Future-dependent mutation requires a proven host capability before mutation.
4. The pre-mutation/post-mutation boundary is crossed when the first build-clone mutation or generated-output application starts.
5. Before that first application, conservative refusal may leave the otherwise-safe unchanged build to continue.
6. At or after that first application, any unresolved condition or failure aborts, including failure halfway through a multi-output or multi-transformation batch.
7. Build abort, not transactional rollback, undo, or atomic mutation machinery, is the post-mutation safety mechanism.
8. The late environment must satisfy the same recorded capability contract, not merely another supported-looking contract.
9. Proof, planning, preparation, expected state, and handoff proof values contain no live Unity objects.
10. Prepared values and application targets remain separate until host mutation.
11. Prepared outputs and expected semantics have one deterministic transformation-specific source.
12. Expected state includes every protected proof dependency, changed or unchanged.
13. Proof-relevant relationships are protected state.
14. Expected and fresh values use symmetric canonical domain definitions.
15. Address resolution must be unique before equality is evaluated.
16. Exact canonical equality is the default. Fuzzy equivalence is forbidden.
17. No prepared claim or future requirement is resolved by last writer wins.
18. A reserved or armed handoff does not authorize continuation.
19. Authorization is single-use and bound to one proven build attempt.
20. Legitimate concurrent attempts remain isolated. Supersession requires proof.
21. Missing or ambiguous handoff after mutation aborts.
22. The late gate is read-only with respect to optimization.
23. Unknown or uncharacterized callbacks after the gate prevent future-dependent positive support.
24. Cleanup does not rely solely on postprocess.
25. Proof-bearing state is not restored across Editor restart.
26. Original avatar source assets are never mutated.

## Test architecture

### Pure unit tests

- symmetric canonicalization of preparation expectations and extracted values.
- exact domain equality and proven normalization boundaries.
- address resolution success, missing refusal, and ambiguous refusal.
- proof-relevant relationship equality.
- deterministic logical output identities.
- carry-forward of unchanged proof dependencies.
- expected-state keyed composition, coalescing, and conflict refusal.
- concrete future-attestation requirement composition and conflict refusal.
- conditional/non-conditional protected-domain interaction decisions.
- host capability contract comparison and drift refusal.
- reserved, armed, consumed, accepted, and refused handoff semantics.
- single-use atomic consumption.
- legitimate concurrency versus proven supersession.
- classification of failures before versus at/after the first build-clone mutation or generated-output application.
- partial multi-output application always selecting abort rather than skip or rollback.

### Host extraction tests

- equivalent NDMF-time and late extraction produces equal canonical records.
- every current proof-relevant renderer, mesh, material, texture, source, global, and relationship field is captured.
- unsupported or incomplete fields refuse rather than disappear.
- shader source attestation and material interpretation consume one coherent capture.
- alpha pixel evidence is eager and immutable.
- no proof path retains a lazy `Texture2D`, `Material`, `Mesh`, renderer, importer, or global read.
- static-state eligibility is positively established.
- animation/material-reachability cases outside the represented domain return `Unknown`.

### Transformation contract tests

- `OriginalSnapshot + Plan` produces deterministic prepared outputs and expected state.
- host mutation consumes the exact prepared output descriptors.
- expected canonical values are projected from the same preparation result.
- logical output identities connect all prepared outputs and relationships correctly.
- multi-target output descriptions work without one-renderer assumptions.
- incompatible preparation claims refuse before mutation.
- current alpha safety invariants remain unchanged.

### NDMF integration tests

- exact late `Optimizing` ordering against the supported environment.
- capability is required before future-dependent proof.
- unsupported lifecycle refuses before mutation.
- all AMUSE-controlled protected-domain effects are accounted for before composition.
- generated-asset ownership and nondestructive mutation.
- reserve, mutate, arm, and cleanup sequencing.
- preflight failure before the first application leaves the clone unchanged and may conservatively skip.
- failure on a later output after one output was applied fails the NDMF build.
- failure in a later prepared transformation after an earlier one was applied fails the NDMF build.
- mutation/arming failure leaves no armed authorization and never continues a partially transformed clone.
- no rollback or undo path is required or used.
- one build-wide conditional batch may cover multiple targets.
- source avatar assets remain unchanged.

### SDK gate tests

- correct build association.
- missing, ambiguous, stale, concurrent, superseded, and repeated associations.
- atomic single-use consumption.
- current environment must satisfy the recorded capability contract.
- callback inventory or order drift refuses.
- complete fresh extraction of the union scope of the authorization.
- unique semantic address resolution.
- exact canonical state and relationship comparison.
- final concrete shader attestation.
- mismatch, missing evidence, and exception abort.
- cleanup occurs without postprocess.
- domain-reload behavior is tested once the investigation establishes a supported contract.

### End-to-end exact-version tests

- a successful conditional build emits and loads a bundle.
- a transformed-state mismatch emits no bundle.
- a mismatch in an intentionally unchanged proof dependency emits no bundle.
- a relationship mismatch emits no bundle.
- a missing/stale authorization emits no bundle.
- a callback-environment mismatch emits no bundle.
- a future-attestation mismatch emits no bundle.
- a synthetic failure halfway through multi-output application emits no bundle.
- a subsequent recovery build succeeds.
- original source assets remain unchanged.

### Later integration end-to-end tests

After separate review and implementation, add exact official-package tests for:

- standalone lilToon.
- LTCGI.
- AudioLink package.
- VRC Light Volumes.
- characterized combinations.

These profiles are not automatically part of the first host lifecycle milestone.

## General-purpose transformation acceptance criteria

The binding criterion is:

> No shared production boundary introduced by this design may unnecessarily assume that an AMUSE transformation is a single-renderer alpha split. The architecture must permit future proof-backed transformations spanning multiple renderers, meshes, UV sets, materials, generated textures, and material-slot topology without implementing those transformations prematurely.

The balancing criterion is:

> Purpose-specific algorithms should remain purpose-specific when their narrow purpose is real. Generalize shared boundaries, not every algorithm.

Shared extensibility comes from:

- build-scoped multi-target expected state.
- small composable canonical domain and relationship records.
- deterministic logical output identities.
- transformation-specific preparation.
- keyed conflict detection.
- a lifecycle/handoff/gate that does not assume which domains changed.

It does not come from generic transformation, mutation, asset, provider, or dependency frameworks.

## Future-transformation thought experiments

### Atlas transformation

Suppose one transformation spans three renderers, several meshes, eight materials, fourteen source textures, generated atlases, changed UVs, and reduced material slots.

An atlas-specific proof and plan can feed an atlas-specific preparer. The result can contain atlas bytes, mesh/UV outputs, material outputs, slot changes, deterministic generated-output identities, consuming relationships, and the complete expected protected state. The shared composer and late gate already accept multiple targets and domains.

New atlas-specific texture and UV semantics would be required, but the host lifecycle architecture would not be replaced.

**Acceptance result: passes.**

### Semantic material merge with a generated control texture

A merge-specific preparer can describe the control texture, combined material, consuming texture binding, affected material slots, and any topology changes. Expected state protects the new relationships as well as unchanged shader/sampling assumptions.

No alpha split or one-renderer boundary is required.

**Acceptance result: passes.**

### UV-only transformation

A UV repacker can prepare mesh UV values and transformed texture regions without an alpha plan or geometry split. It contributes the UV-to-texture-region relationships and preserves unchanged material/shader assumptions.

**Acceptance result: passes.**

### Shader-feature simplification

A shader-feature proof can prepare only material/shader configuration changes. The expected state need not claim a geometry modification, while still protecting unchanged dependencies used by the proof.

**Acceptance result: passes.**

### Multi-renderer and multi-asset transformation

One concrete prepared transformation may contribute claims across a bounded set of renderers and assets. The conditional authorization is build-scoped and composition refuses incompatible overlap.

**Acceptance result: passes.**

## Known limitations

- The exact host mechanism for upload-path detection is unresolved.
- The exact enforceable callback inventory and ordering mechanism is unresolved.
- The production build-attempt association is unresolved.
- Domain reload during an active conditional build is unsupported pending investigation.
- Exact runtime host-version identification is unresolved.
- Apply-on-Play is unsupported for future-dependent proof.
- No positive callback-generated lilToon attestation profile is implemented.
- Official integrated lilToon packages remain unsupported positively.
- The current animation/material-reachability model is incomplete. This limits coverage: cases positively established as static/completely represented may still be supported, while cases needing unavailable reachability reasoning return `Unknown`.
- No generic sequencing exists for dependent transformations. Known interactions must be coordinated concretely or refused.
- The exact future file/type layout and SDK assembly dependency are intentionally deferred until the prerequisite audit and investigation close.

## Explicit investigation dependencies

### `investigate/sdk-build-environment-contract`

This investigation must establish, using public/pinned APIs and exact-version evidence:

1. how AMUSE distinguishes the supported normal upload/build invocation before mutation.
2. how AMUSE inspects or otherwise enforces the actual preprocess callback inventory.
3. which ordering guarantees are contractual, including equal-order behavior and callbacks after the proposed gate.
4. how one build attempt is associated across NDMF and the late SDK callback.
5. how legitimate concurrency and genuine supersession are distinguished.
6. what happens on cancellation and domain reload.
7. how exact Unity/NDMF/SDK version identity is established.
8. whether the characterized late failure behavior remains enforceable through the production API shape.

These are implementation blockers for conditional capability, not details this design may guess.

### Host-version decision/investigation

If the SDK investigation leaves reliable exact gating unresolved, a separate host-version decision or investigation must close it before implementation. Host version checks remain centralized in lifecycle capability either way.

## Unresolved questions

The architecture decision is complete, but these host mechanisms remain intentionally unresolved pending the named investigation:

1. Which supported API or pinned-source fact identifies a normal upload/build invocation before NDMF mutation?
2. How can AMUSE authoritatively inspect or otherwise constrain the current callback inventory without relying on discovery order?
3. What production callback order and tie behavior make the late gate authoritative for the supported environment?
4. What build-attempt identity survives from NDMF to the SDK gate and distinguishes concurrency from proven supersession?
5. Does an assembly/domain reload necessarily abort the active builder, or is another bounded non-proof marker required?
6. How are cancellation and exceptional builder termination surfaced for prompt handoff cleanup?
7. Which APIs reliably identify the exact Unity, NDMF, and SDK versions used by the capability contract?
8. What exact SDK-facing assembly/package arrangement allows the callback boundary without making core reasoning depend on the SDK?

None of these questions may be answered by assumption during implementation. Failure to prove a required mechanism leaves conditional capability unavailable.

## Post-design general-purpose-boundary audit

The immediate follow-up after this design is reviewed and merged is:

`audit/general-purpose-transformation-boundaries`

It is classification-only and does not fix production code. It examines these exact candidates:

- one-renderer scope in `UnityRendererAlphaAnalysis`.
- one-mesh and opaque/transparent shapes in `MeshSeparationPlan`.
- submesh-to-material assumptions.
- whether shared texture or UV records gain alpha-only fields during migration.
- whether mutation preparation can express multiple output assets.
- whether expected-state addressing becomes tied to renderer instance identity.
- whether `MaterialSemantics` is mistakenly treated as the complete validation state.
- whether generated texture identities can remain opaque and deterministic.

It classifies findings approximately as:

- **A — correctly purpose-specific:** leave alone.
- **B — purpose-specific implementation behind a sufficient shared boundary:** acceptable.
- **C — accidental architectural fixation:** must be resolved before the later implementation plan builds on that boundary.
- **D — premature abstraction:** simplify if appropriate in separately reviewed work.

The audit may block or revise the later implementation plan when it finds Category-C fixation in a shared boundary the production architecture would use. Its classification-only scope does not make its findings optional. Correctly purpose-specific A/B findings remain untouched.

## Recommended branch sequence

```text
design/upload-conditional-authorization
        ↓
audit/general-purpose-transformation-boundaries
        ↓
investigate/sdk-build-environment-contract
        ↓
host-version decision / investigation if still necessary
        ↓
reviewed implementation plan
        ↓
production implementation branches
```

This branch does not start any follow-up branch.

## Anticipated implementation milestones

This architecture is too broad to treat as one undifferentiated implementation plan or branch. Without creating an implementation plan now, later planning should decompose it into coherent milestone plans around:

1. immutable shared extraction migration for the bounded renderer-alpha slice.
2. alpha-specific preparation, canonical expected-state records, relationship protection, and keyed composition.
3. NDMF mutation integration and bounded build-associated handoff.
4. late SDK gate, capability enforcement, cleanup, and recovery.
5. concrete standalone lilToon future-attestation support after separate review.
6. later reviewed integration profiles.
7. animation/material-reachability expansion as a coverage milestone.

The audit and SDK investigation may revise or decompose these milestones before an implementation plan is approved.

Intermediate milestones may establish reusable extraction, preparation, composition, or host seams, but no future-dependent conditional mutation may be enabled or shipped until the enforceable capability, late gate, cleanup, and complete end-to-end abort behavior are implemented and validated together.

## Design self-review checklist

The written design is acceptable only if review confirms:

- no placeholder or unresolved mechanism is presented as implemented fact.
- responsibilities have one owner and do not contradict one another.
- live Unity objects never become proof data.
- prepared outputs and expected semantics have one deterministic source.
- expected state includes modified and unchanged proof dependencies.
- prepared and extracted values use symmetric canonicalization.
- semantic address resolution is separate from equality.
- proof-relevant relationships are protected.
- the late gate remains read-only and cannot become a second optimizer.
- Apply-on-Play cannot mutate first and refuse too late.
- callback finality is not inferred from a large numeric order.
- lilToon concepts do not define the host lifecycle capability.
- shared boundaries do not assume one renderer, one mesh, or alpha separation.
- alpha-specific algorithms and evidence remain purpose-specific where appropriate.
- no generic transformation, sequencing, dependency, attestation, snapshot, identity, or asset framework is introduced prematurely.
- unresolved SDK mechanisms are explicit investigation blockers.
- incomplete animation/reachability is treated as conservative coverage limitation where static completeness can be proved.
- the audit can block or revise the implementation plan on Category-C shared-boundary findings.
- the later work is decomposed rather than represented as one implementation task.

## Validation for this design branch

This branch requires documentation validation only:

- inspect the complete unstaged and staged diffs separately.
- run `git diff --check`.
- run `git diff --stat`.
- run `git diff --name-status`.
- verify this design document is the only changed file.
- verify no manifest/lock or host-generated Unity package churn.
- verify no Census Lab or private state was accessed.

No Unity run is required because production code and tests must remain unchanged.

The document must remain uncommitted until the user reviews and approves the written specification. After approval, documentation-only corrections may be made, the design file alone may be staged and committed, and the branch may be pushed for a draft PR targeting `main`. It must not be merged without separate authorization.
