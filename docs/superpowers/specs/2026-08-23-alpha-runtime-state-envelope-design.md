# AMUSE Alpha Runtime State Envelope Design

## Status and scope

**Approved as AMUSE's normative architecture for proving alpha opacity under runtime state.**

- Branch: `feat/alpha-runtime-state-envelope`
- Base: `origin/main` at `d9facec`
- Repository change from the design phase: this design document only
- Census Lab/private avatars: not used

This specification defines the minimum correctness architecture that lets AMUSE classify a face `ProvenOpaque` only when it is proven opaque under every runtime state that can affect that proof. It covers what animation state AMUSE observes, how that observation becomes immutable evidence, how a finite set of admitted material states is derived from it, and how the existing alpha proof pipeline is reused unchanged across those states.

The design phase that produced this document added no production code, no tests, no Unity assets, no package changes, and no NDMF pass changes. Implementation proceeds under a separate plan and is governed by this specification; the *Non-goals* list below remains binding on that implementation.

### Non-goals

The following are explicitly out of scope and must not be designed, scaffolded, or partially implemented under this specification: mesh or material mutation; source-scene modification; persistent caching, fingerprints, or invalidation graphs; the DAO bridge and Candidate A/B cooperation; shader portability, material combining, atlasing, UV repacking, texture arrays, and generated control textures; exact Animator state-machine reachability; exhaustive VRChat parameter reachability; a generic animation or shader intermediate representation; provenance adapters for Modular Avatar, VRCFury, or AAO; and rollback or transaction mechanisms.

## Prior decisions incorporated

This specification builds on:

- `docs/architecture/vision.md`;
- `docs/superpowers/specs/2026-08-22-coexisting-optimizer-lifecycle-design.md`;
- `docs/superpowers/plans/2026-08-22-platformfinish-lifecycle-foundation.md`;
- `docs/superpowers/investigations/2026-08-23-platformfinish-followups.md`;
- `docs/superpowers/investigations/2026-08-22-single-stage-optimization-lifecycle.md`;
- `docs/superpowers/investigations/2026-08-22-coexisting-optimizer-lifecycle.md`.

The following prior conclusions are binding here:

1. AMUSE is proof-first and fail-closed. Additional uncertainty never makes optimization more aggressive.
2. The AMUSE semantic barrier in NDMF `PlatformFinish` is the point at which completed external transformations become immutable proof input.
3. Compatibility is established through final resulting state, never through tool provenance.
4. Host extraction is eager. Proof, planning, and preparation consume only AMUSE-owned immutable values.
5. Unknown animation behavior is a semantic refusal, not a lifecycle failure.

## The theorem

> A face is `ProvenOpaque` only if its alpha semantics are opaque for every relevant runtime state conservatively admitted by AMUSE.

*Relevant runtime state* means state capable of changing an input on which the current alpha proof depends. Relevance is not asserted globally; it is derived from the semantic dependencies declared by the shader frontends (see *Relevance follows dependency*).

Soundness of the whole architecture reduces to one obligation:

```text
admitted states  ⊇  reachable states
```

Every other component — resolution, classification, intersection — is existing, tested behavior. If the admitted set is a superset of what can actually occur at runtime, and the face is proven opaque in every admitted state, the face is opaque at runtime.

The consequences of the two error directions are asymmetric and are treated asymmetrically:

- an admitted set larger than the reachable set produces **false negatives**: geometry stays on the transparent path. This is acceptable and expected.
- an admitted set smaller than the reachable set produces **false positives**: geometry is transformed without proof. This is a correctness bug, never a tradeoff.

Where the admitted set cannot be established as a superset, AMUSE refuses. Refusal is the defined outcome, not a failure.

## Relevance follows dependency

AMUSE must not hard-code a global list of "irrelevant" animation. Relevance is derived from the proof's own dependency structure.

The shader frontends already declare their dependencies as data. `MaterialEvidenceRequest` — specifically the alpha evidence requests built by `PoiyomiMaterialSemantics.CreateAlphaEvidenceRequest` and its lilToon counterpart — enumerates exactly the shader name, presence-schema properties, scalar gates, color properties, vector properties, and texture properties on which the supported alpha equation depends, together with the texture evidence kinds it consumes. Downstream, `AlphaSemanticsResolver` adds the mapping and sampling conditions it can prove.

The union of those requests across the frontends attested for a build **is** the animation relevance filter. An animated binding is proof-relevant exactly when it names a property in that union, or when it is one of the structural bindings enumerated below.

### Dependency closure is normative

The relevance filter must be closed over admitted materials before it is used. A material introduced by animation may belong to a different shader family than the initially assigned material, and may therefore introduce alpha-semantic dependencies on properties the initial family never requested. Deriving property relevance from the initially assigned family alone would under-populate the admitted set — a false-positive direction.

The discovery order is normative:

```text
discover structural material-slot swaps
      ▼
enumerate every admitted material
      ▼
attest/identify every admitted material family
      ▼
union the alpha MaterialEvidenceRequests of all admitted families
      ▼
use that closed union to determine proof-relevant animated property bindings
```

Each step must complete before the next begins. Proof-relevant property bindings are determined from the **closed union**, never from a single slot's current family.

If the closure cannot be established — a slot whose admitted material set cannot be fully enumerated, or an admitted material whose family cannot be attested — the renderer is refused. A partially closed union is not a filter and must never be used as one.

### Consequences

This has three consequences that are part of the design, not incidental:

- adding a property to a frontend's alpha evidence request automatically extends animation relevance; the two cannot drift apart.
- ordinary bone movement, transform animation, and blendshape deformation are not relevant to the *current* alpha equation, because the resolved equation reads material scalars, UV0, and texture texels only. This is a derived result for the current equation, not a permanent claim. If a future supported alpha equation depends on geometry or world-space state, that dependency appears in its evidence request and becomes relevant automatically.
- the animation subsystem never interprets shader meaning. It produces substituted property values; the frontends interpret them.

### Proof-relevant structural bindings

Beyond property values, the following invalidate the mapping the proof itself rests on and are relevant regardless of shader family:

- object-reference curves on renderer material slots (material swaps);
- object-reference curves replacing the renderer's mesh;
- any change to the material slot count, which breaks the submesh-to-slot correspondence.

## Observation boundary: the committed controller graph

AMUSE reads animation evidence from the **committed real controller graph**, not from NDMF's virtual animator graph.

### Why the virtual graph is insufficient

NDMF represents VRChat proxy animations as marker clips. `VirtualClip.IsMarkerClip` is set when `IPlatformAnimatorBindings.IsSpecialMotion` holds for the source asset; such clips are immutable and are committed by identity rather than cloned. Marker clips expose **no** curve bindings through the virtual API, and their backing motion is not reachable through any public NDMF API. `ICommittable` and `CommitContext.CommitObject` are internal; `VirtualNode.OriginalObject` is protected and exposed only internally. Reflection and private-field access are out of scope.

The only correlation the virtual graph permits is display name, and name matching cannot authorize an opacity proof: `VirtualClip.FromMarker` is public and `VirtualState.Motion` has a public setter, so a third party can wrap an arbitrary clip whose name collides with a stock proxy and hide proof-relevant animation from AMUSE. Content-derived fingerprints over the public surface (`FrameRate`, `WrapMode`, `Legacy`, `LocalBounds`, `Settings`, `UseHighQualityCurves`) are equally forgeable.

**No name-based or fingerprint-based marker authorization is permitted anywhere in AMUSE.**

Staying on the virtual graph would leave only one sound rule. A hidden marker binding can target any path and any property, so no layer-, controller-, or renderer-scoped containment is sound, and the refusal would have to cover every renderer on any avatar containing any marker. Marker clips are expected in essentially every real avatar through stock and fallback playable layers, so that rule would reduce coverage to approximately zero.

### The committed-graph route

Three public facts make a better route available:

- deactivating the animator extension contexts commits every virtual controller to real Unity objects, and marker clips are committed **by identity**, never cloned;
- NDMF's resolver deactivates an active extension deterministically before any pass that neither requires it nor declares compatibility with it;
- `IPlatformAnimatorBindings` is a public interface with public `IsSpecialMotion(Motion)` and `GetInnateControllers(GameObject)` members, and the instance NDMF used is reachable through the public `VirtualControllerContext.PlatformBindings`.

AMUSE therefore observes through two passes:

```text
capture pass   declares AnimatorServicesContext
               retains the IPlatformAnimatorBindings reference
               performs no analysis and no mutation
      │
      │  NDMF deactivates the animator contexts and commits
      ▼
barrier pass   declares no animator extension
               reads the committed real controller graph
               eager capture → immutable evidence → pure proof
```

In the committed graph every motion is a real Unity object. Corpus membership is therefore established **positively**, by NDMF's own predicate, with no name matching and no reflection. Every binding is readable through `AnimationUtility`.

Controllers are enumerated from `IPlatformAnimatorBindings.GetInnateControllers(root)` together with the public `IVirtualizeAnimatorController` and `IVirtualizeMotion` component interfaces. AMUSE takes no dependency on the VRChat SDK; the `nadena.dev.ndmf.vrchat` assembly is not referenced either, because its define constraint would prevent the public development project and CI from compiling.

### Special motions after commit

**Special motions have no marker-specific authorization path.** After commit they are ordinary clips: their real bindings are read normally and enter admitted-state construction like any other clip's. There is no proxy exemption, no corpus-union theorem, and no special-case refusal.

`IsSpecialMotion(motion)` is still evaluated and recorded, but **only as host evidence and diagnostic** — it records that the pinned host definition was consulted and which motions the host considers special. It never authorizes, gates, or relaxes any proof.

### Analysis-only, precisely stated

AMUSE authors no change to source assets and no change to build-avatar semantics on this branch. It proves and plans only.

AMUSE's extension declarations do, however, engage NDMF's own context lifecycle. On deactivation NDMF re-clones the virtualized controllers, normalizes first-layer weights, writes the results back to the avatar's animator bindings, and saves the resulting objects through `context.AssetSaver`. These are NDMF's operations, performed under NDMF's own contract, on the transient build avatar only. They are not AMUSE mutations, and AMUSE asserts nothing about them beyond their being NDMF's documented behavior.

Two consequences are recorded explicitly:

1. "Analysis-only" means **AMUSE-authored** mutation, not process-wide absence of writes. Any statement that this branch changes nothing must be read with that qualification.
2. `context.AssetSaver` becomes load-bearing during *analysis*, earlier than the merged PlatformFinish review anticipated in deferred finding 4. The obligation to cross-check the lifecycle capability's saver facts against the `IAssetSaver` actually handed to the caller is carried forward unchanged, but its trigger point has moved earlier and must not be deferred past the first pass that declares an animator extension.

## Admitted-state construction

For each renderer, AMUSE derives a finite set of **admitted material states**: for every material slot, a finite set of admitted captured materials, and for every proof-relevant property, a finite set of admitted exact values.

### Material swaps

Object-reference curves are step functions; Unity performs no interpolation between `ObjectReferenceKeyframe`s. The admitted material set for a slot is therefore exactly enumerable:

```text
admitted(slot) = { current assignment } ∪ { every keyframe value of every
                   object curve targeting that slot }
```

Each admitted material is captured through the existing selective `MaterialEvidenceRequest` path. The capture is unchanged and remains provenance-unaware: it does not matter which tool produced a material or why it appears in a curve.

A slot is refused when any admitted material is unattested or resolves all-Unknown, and — per the theorem — a face can be `ProvenOpaque` only if it is proven opaque under **every** admitted assignment for its slot.

### Property values

V1 admits **finite-exact property animation only**: a value is admitted only when it is one of a finite set of exact values that the runtime can actually take, with no interpolated intermediate.

Two distinct mechanisms can produce intermediates, and both must be excluded.

**Within a curve.** A curve contributes admitted values only when its value at every time equals one of its own keyframe values — a constant curve, a single-key curve, or a curve whose segments are all constant/stepped. Any interpolating segment is refused, including every weighted-tangent form. Keyframe minimum and maximum are **not** a sound runtime bound: a cubic Hermite segment can overshoot both endpoints. Exact Hermite bounds and any interval or range representation are deferred; A′ does not need them.

**Across sources.** Unity blends generic float bindings across layer weights, transitions and crossfades, and blend-tree children. Two contributing sources that disagree on a proof-relevant property therefore make the reachable set a continuum spanning them, not the two endpoints. A partially weighted override layer blends the clip value with the underlying value in exactly the same way.

Consequently, for a **float, color, or vector** property, V1 admits it only when its admitted set is a **singleton**: every contributing source — every keyframe of every clip that can write it, on every layer, together with the material's serialized default — agrees on one exact value. Any disagreement is refused.

This is narrower than it may appear, and it is deliberate. It still admits the common case of a property animated but always re-asserted to the same value, which Approach C would have refused outright. It does not admit a gate genuinely toggled between two values, because an intermediate would break the binary reads the frontends perform and the resulting state is genuinely unproven, not merely unmodeled.

**Material swaps are unaffected by this restriction.** Object references cannot be interpolated: no blend, transition, or layer weight can synthesize a material that is not one of the keyframe values. Which admitted material wins under a given blend is irrelevant, because every one of them is admitted. Exact enumeration therefore remains sound for swaps, and swaps remain the primary source of admitted-state multiplicity.

The material's own serialized default value is always included in the admitted set. This is what makes Write Defaults require no modeling: with Write Defaults on, an unwritten property reverts to the serialized default; with it off, it retains a previously written clip value. Either way the reachable set is contained in the clip values together with the default.

### Layer and blend combination

Under the singleton rule, override layers, layer weights, transitions and crossfades, and normalized blend trees cannot produce a value outside the admitted set: a blend among equal values is that value, and object references cannot be blended at all. No separate machinery is needed for them, and a behaviour that changes another layer's weight cannot escape the admitted set.

Two forms are refused outright for any proof-relevant property, independently of the singleton rule, because their output is not bounded by their inputs even when those inputs agree:

- **additive layers**, whose output is a base value plus weighted differences from a reference pose;
- **Direct Blend Trees with normalization disabled**, whose weighted sum is bounded only by unbounded parameters.

### State machine behaviours

**No containment theorem is available.** NDMF 1.14.4 handles exactly three VRChat behaviour types specially — `VRCAnimatorLayerControl`, `VRCAnimatorPlayAudio`, and `VRCAvatarParameterDriver` — and passes every other behaviour through untouched. That is a handling table for the types NDMF must rewrite, not a whitelist, and NDMF asserts no completeness. The VRChat SDK is not installed in the public development project, so any platform-level restriction on surviving avatar behaviours cannot be inspected or pinned locally. AMUSE therefore has no proven bound on what an arbitrary behaviour may do.

The previously drafted rule — that an unknown behaviour is irrelevant when its layer carries no proof-relevant binding — is **withdrawn as unsound**. A behaviour is not layer-local merely because it is attached to that layer: `VRCAvatarParameterDriver` writes parameters shared across the whole Animator, `VRCPlayableLayerControl` weights an entire playable layer, and `VRCAnimatorLayerControl` weights a different layer in the same controller. Attachment point does not bound effect.

What *does* bound effect is A′'s own over-approximation. AMUSE performs no reachability analysis: it already admits every clip on every layer of every controller. So a behaviour that only selects states, writes parameters, or changes layer or playable weights **cannot escape the admitted set** — state and parameter selection is already fully over-approximated, and weight-induced blending is contained by the singleton rule together with the additive and unnormalized-Direct-Blend-Tree refusals.

The residual hazard is therefore exactly one thing: **a behaviour running arbitrary code that writes proof-relevant state directly**, outside the animation system — assigning `sharedMaterials`, setting a material property, applying a property block, or replacing a mesh. Such an effect is not layer-local, not path-scoped, and not observable from the controller graph. No scoping is sound against it.

The rule is therefore:

- Behaviours are recognized **by type**, against a version-pinned allowlist. Type identity is read from the behaviour instance; no SDK assembly reference is required.
- A type may be placed on the allowlist only with a **recorded justification** that its effect is confined to parameters, layer or playable weights, or state selection — that is, to effects already inside A′'s over-approximation.
- Any behaviour whose type is not on the allowlist causes an **avatar-scoped refusal**, because arbitrary code has unbounded reach.

The allowlist starts empty and grows only with evidence. Populating and justifying it is an implementation-time verification obligation, not an assumption this design may make in advance. This is fail-closed: an unjustified type costs coverage, never correctness.

### Animation events

An `AnimationEvent` is not a curve binding. It invokes a method by name on the animated hierarchy, which is **runtime-code behavior outside the admitted curve/value model entirely** — no part of the admitted-state construction observes it.

Two host facts are established and pinned:

- NDMF **drops** animation events when it clones a clip that carries them. The `VirtualClip` constructor builds a fresh `AnimationClip` and copies only curves, because Unity offers no way to delete events. Ordinary cloned clips therefore reach the committed graph without events.
- **Special/marker motions are the exception.** They are committed by identity and never cloned, so any events they carry survive verbatim.

Whether such an event can execute, and with what effect, in the supported VRChat avatar runtime **cannot presently be bounded**: the VRChat SDK is absent from the public development project and no public API available here characterizes it. An event's target method is resolved by name against the hierarchy and its effect is unbounded, so no layer-, clip-, or renderer-scoped containment is sound — the same reasoning that makes an unallowlisted behaviour avatar-scoped.

The rule is therefore: **any reachable committed clip containing an animation event yields avatar-scoped `AnimationEventPresent`.**

This is a **conservative refusal, not a claim that the event definitely executes.** It costs coverage and can never cost correctness. If the executability and effect of avatar animation events are later characterized against the pinned platform, this refusal narrows or disappears; until then, an uncharacterized runtime writer refuses.

### Structural invalidation

A renderer is refused outright when an object-reference curve can replace its mesh, or when the material slot count can change. AMUSE does not attempt animated-mesh identity reconciliation, and no generic reconciliation system is designed here.

### Bound on the admitted-state product

The admitted-state product per renderer is capped at a provisional **4096** states. The cap is an implementation parameter, not a semantic constant.

The cap is evaluated on the enumerated product **before any state is materialized and before any geometry work**, so reaching it costs nothing and produces a named refusal.

Because admitted float properties are singletons, they contribute a factor of one, so the product is in practice the material-swap product. 4096 corresponds to four slots with eight admitted materials each, or twelve slots with two — comfortably above anything currently plausible. There is no repository evidence suggesting a better bound: the census records nothing about animated alpha-relevant bindings, and measurement is deferred until after correctness. The number is therefore explicitly unmeasured, and revising it requires evidence rather than preference.

The cap is retained despite the singleton rule because it must still hold if a later revision widens float admission, and because it is the mechanism that keeps a pathological swap graph from being materialized.

## Proof composition

Correctness is defined over **all distinct semantic resolutions** of the admitted states. Deduplication is a **performance measure only**: failing to deduplicate costs work and can never change the proof, because classifying the same resolution twice yields the same outcome and intersection is idempotent.

Most admitted states collapse to the same `AlphaResolution` — typically uniform-opaque, uniform-transparent, or refused — so equivalent resolutions are deduplicated per slot before any triangle is classified, and the distinct-resolution count rather than the state count multiplies triangle work.

Any dedup equivalence relation must be **exact or conservative**: it may treat two resolutions as distinct when they would have behaved identically, but it must never merge two resolutions that could classify any triangle differently. An approximate or heuristic equivalence is not permitted, because merging distinct resolutions would silently shrink the set the intersection is taken over — a false-positive direction.

The proof then composes as:

```text
for each slot:
    admitted states → resolutions → deduplicate
for each triangle:
    ProvenOpaque  ⟺  ProvenOpaque under every distinct resolution
```

`AlphaSemanticsResolver` and `TriangleAlphaClassifier` are used **unchanged**. `MaterialSemantics`, `ScalarSemanticValue`, and both shader frontends gain no new vocabulary. The single new pure operation is deriving immutable captured evidence with one property value substituted — immutable in, immutable out, no live Unity object involved.

The intersection is per triangle, not per submesh or per renderer, so a renderer whose admitted states disagree on some faces still contributes the faces on which they agree.

## Failure semantics

The design distinguishes two categories, and the distinction is structural rather than conventional.

**Domain limitation → named refusal.** An expected, unsupported, or conservatively unprovable construct returns a named refusal value in the existing `RendererAnalysisRefusal` idiom, extended with animation-, structure-, and controller-scoped members. Refusal preserves the input and explains why. This includes at minimum:

- unsupported curve form on a proof-relevant property;
- a proof-relevant float, color, or vector property whose contributing sources disagree, so its admitted set is not a singleton;
- additive-layer or unnormalized Direct Blend Tree contribution to a proof-relevant property;
- unattested or all-Unknown admitted material;
- animated mesh replacement or slot-count change;
- admitted-state product above the cap;
- a `StateMachineBehaviour` whose type is not on the allowlist, refused at avatar scope;
- `AnimationEventPresent`: a reachable committed clip carrying an animation event, refused at avatar scope;
- material-swap dependency closure that cannot be established, because a slot's admitted material set cannot be fully enumerated or an admitted material's family cannot be attested;
- unsupported `RuntimeAnimatorController` form, including `AnimatorOverrideController` and any subtype AMUSE does not walk;
- synced-layer motion overrides.

**Implementation defect → propagate.** An unexpected exception is not caught. `catch (Exception) → skip renderer` is explicitly rejected as a general mechanism. This is safe on this branch precisely because nothing is mutated: a propagated defect blocks the build before any change exists, and no rollback is relevant.

The two categories must remain distinguishable in the result. A refusal that cannot explain itself is treated as the defect it is, following the precedent already set by `AmusePreparationDecision.Refused`.

## Evidence immutability

The capture boundary established by the previous branch is preserved and extended:

```text
live Unity / controller objects
        │  eager bounded capture
        ▼
immutable evidence
        │
        ▼
pure reasoning
```

Proof, admitted-state construction, and classification must not lazily call back into `AnimatorController`, `AnimationClip`, `AnimationCurve`, `Material`, `Renderer`, `Mesh`, or any other mutable Unity object.

The existing Unity-object field guard is shallow: it checks a type's own fields plus their direct generic arguments and does not walk the captured graph. Deferred PlatformFinish finding 1 anticipated that this would stop covering the graph as it grew, and the animation evidence graph is exactly that growth — renderer, slot, admitted material, captured evidence, texture. The guard is therefore **generalized into a recursive check over the whole captured graph** and reused for animation evidence. It remains a safety net rather than the mechanism; immutability is still held by construction. No generic snapshot framework is introduced; the guard is a small shared host-capture boundary.

## Implementation-time verification obligations

The following are **open questions, not settled facts**. Each is cheap to settle with a synthetic, deterministic, public fixture during implementation; none requires the Census Lab. None may be resolved by assumption, and the parts of the boundary that depend on each are named so that a wrong answer is visible rather than silent.

1. **`GetInnateControllers(root)` side-effect safety.** Whether it is side-effect-safe and idempotent enough for this lifecycle use. It is observed to set `customizeAnimationLayers` and, under some conditions, to instantiate the avatar descriptor's editor. NDMF already invokes it during context activation, so re-invocation is *expected* to be idempotent, but this is unverified. Affects: whether AMUSE may call it in the barrier pass, or must instead capture what it needs in the capture pass.
2. **Exact per-slot material-property binding naming.** The precise `EditorCurveBinding.propertyName` form Unity uses when a material property is animated on a specific material slot, and whether a binding applies to one slot or to several. Affects: correctness of mapping animated bindings to slots, and therefore whether an admitted set can be under-populated.
3. **Texture-reference object curves on materials.** Whether Unity supports animating a material's texture reference by object curve at all. Affects: whether texture assignment must be an admitted-state dimension, or is structurally impossible.
4. **`MaterialPropertyBlock` application.** Whether animated material properties are applied at runtime through a `MaterialPropertyBlock`. Affects: how the existing static `HasPropertyBlock` whole-renderer refusal and animated-property handling are reconciled into one coherent rule.
5. **Committed-controller behavior for `AnimatorOverrideController` and synced layers.** The exact committed forms, where relevant to detecting the two named refusals reliably. Affects: whether those refusals detect their conditions completely, or can be silently bypassed.
6. **`IPlatformAnimatorBindings` lifetime across context deactivation.** Whether the reference captured while `AnimatorServicesContext` is active remains valid and usable after that context has been deactivated and committed — specifically whether `IsSpecialMotion` and `GetInnateControllers` still behave correctly on it at that point. This is **load-bearing for the two-pass committed-controller architecture**: if the reference does not survive the boundary, the capture pass cannot hand it to the barrier pass and the observation route does not work as designed. If verification shows it does not survive, that must be reported as an **architectural blocker** requiring a design revision — obtaining the bindings by another public route, restructuring the pass split, or reconsidering the observation source. It must not be worked around with a test fixture that papers over the real lifecycle, and no implementation may proceed past this point on the assumption that it holds.
7. **The `StateMachineBehaviour` allowlist.** Which behaviour types may be allowlisted, and the recorded justification for each that its effect is confined to parameters, layer or playable weights, or state selection. Separately, whether the pinned VRChat platform restricts surviving avatar behaviours to a known set at all — if that is ever established, the allowlist becomes justifiable wholesale and the avatar-scoped refusal narrows accordingly. Until then the allowlist is empty and every behaviour type refuses.
8. **Blending of generic material-property float curves.** Whether Unity in fact interpolates material-property float bindings across layer weights, transitions, and blend-tree children, or applies them discretely. V1 assumes it does, which is the conservative direction and is why float admission is restricted to singletons. Affects: only how far float admission could be widened later; a wrong assumption here costs coverage and can never cost correctness.

Obligations 2, 3, and 4 bound the completeness of the relevance filter. Until they are settled, the relevance filter cannot be claimed complete, and the claim must not be made in code comments, tests, or diagnostics.

Obligation 6 is the only one that can invalidate the architecture rather than merely bound it, and it is therefore settled first, before any other implementation work.

Obligation 6 verified 2026-08-23 by `Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorBindingsLifetimeGateTests.cs` (`CapturedBindingsRemainUsableAfterContextDeactivation`): the captured `IPlatformAnimatorBindings` remains usable after `AnimatorServicesContext` deactivates and commits.

Obligation 8 is the one case where the unverified assumption is deliberately conservative rather than merely unknown, and it is recorded here so that it is revisited as an opportunity rather than mistaken for a settled limit. Within-curve behavior was observed 2026-08-24 by `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs`: `AnimationCurve.Linear(0f, 0f, 1f, 1f)` evaluates to `0.5f` at `0.5f`; keys `(0f, 0f, outTangent=+Infinity)` and `(1f, 1f, inTangent=+Infinity)` evaluate to `0f` at `0.5f`; and equal-endpoint keys `(0f, 1f, outTangent=2f)` and `(1f, 1f, inTangent=-2f)` evaluate to `1.5f` at `0.5f`, rather than `1f`. The latter records Hermite overshoot, so keyframe endpoints are not a finite-exact admission rule. Cross-source blending of generic material-property float curves still requires a Play Mode observation and remains open; the conservative singleton rule stands.

Obligation 2 partially observed 2026-08-23 by `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs` (`UnityGeneratesTheMaterialBindingsWeParse`), on Unity 2022.3.22f1 with a `SkinnedMeshRenderer` carrying two Standard-shader materials. `AnimationUtility.GetAnimatableBindings` exists, returns bindings, and surfaces material bindings, so discovery from Unity itself is available. The property-naming part of the obligation is settled: a scalar property generates a single unsuffixed form (`material.<PropertyName>`, e.g. `material._Cutoff`); a colour property generates four component-suffixed forms (`material.<PropertyName>.<r|g|b|a>`, e.g. `material._Color.r`); and a Vector4-valued property (ST tiling/offset, `_HDR`, `_TexelSize`) generates four component-suffixed forms (`material.<PropertyName>.<x|y|z|w>`, e.g. `material._MainTex_ST.x`).

The slot-targeting part of the obligation is **not settled and is recorded as unobserved**. `GetAnimatableBindings` generated no `material[1].`-indexed prefix, no separate binding set attributable to the second slot, and — pinned by the committed comparison test `TheGeneratedMaterialBindingSetDoesNotVaryWithSlotCount` in the same file — the identical 132-entry `material.*` set for a renderer carrying only one material. The generated `propertyName` therefore carries no information that lets a caller positively determine which material slot a `material.*` binding targets; slot 0 and slot 1 are indistinguishable from the binding name alone in this observed environment. This triggers the brief's conservative branch for the slot-targeting sub-question only: implementation must not default an unresolvable `material` binding to slot 0, and must instead route it to a new conservative refusal (`RendererAnalysisRefusal.UnresolvedAnimatedMaterialSlot`), per the plan's Task 11.

The slot-targeting paragraph above records a fact about *syntax*, and its
prescription is **superseded 2026-08-23 by the Task 3 sampling observation
below**, which settles the application *semantics* the syntax could not.

Obligation 3 **settled 2026-08-23**, negative, by
`Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs`
(`NoTextureReferenceObjectCurvesAreGeneratedForMaterials`,
`StructuralBindingCategoriesAreDiscovered`), on Unity 2022.3.22f1 with a
`SkinnedMeshRenderer` carrying two Standard-shader materials.
`AnimationUtility.GetAnimatableBindings` generates **no** object-reference curve
whose property name begins with `material`: the complete `isPPtrCurve` collection
for this renderer contains only
`m_Materials.Array.data[0]` and `m_Materials.Array.data[1]`, both typed
`SkinnedMeshRenderer`. Animating a material's texture reference is therefore not
an offered dimension in this environment, and texture assignment does **not**
become an admitted-state dimension.

The generated structural categories are settled alongside it.
`m_Materials.Array.data[n]` is an **object-reference** category
(`isPPtrCurve == true`), one binding per existing slot, and no `m_Mesh` binding is
generated for a `SkinnedMeshRenderer`. `m_Materials.Array.size` is **not
generated** by discovery. A controlled sampling characterization deliberately
authored it as a float curve targeting a slot count of one on a two-slot renderer;
the independent `m_LocalScale.x` control reached `3.5`, but the sampled and
restored slot counts both remained two
(`AuthoredMaterialArraySizeFloatCurveDoesNotChangeSlotCountWhenSampled`). Because
the authored float curve produced no observed array-size effect, the curve
category that can carry a working `m_Materials.Array.size` animation remains
**unobserved**.

Material-slot object-curve targeting is **observed, positive**, by
`MaterialSlotObjectCurveActuallySwapsTheSlot`: with the mandatory
`m_LocalScale.x` control confirmed at its animated value, an
`m_Materials.Array.data[0]` object curve replaced **exactly slot 0**, left slot 1
holding its original material, and did not change the slot count. The
conservative "undetermined slot" branch is therefore **not** taken for
object-reference curves. One harness precondition is recorded with it and is a
property of EditMode sampling rather than of runtime: with no `Animator` on the
sampled root, `AnimationMode` applies float curves but silently applies no
object-reference curve at all (`MaterialSlotObjectCurveSamplingRequiresAnAnimatorOnTheSampledRoot`).

Obligation 4 **settled 2026-08-23** by
`BareMaterialBindingAppliesViaARendererWideMaterialPropertyBlock`, and it also
settles the slot-application semantics of a bare `material.<Property>` binding on
a multi-slot renderer. Sampling a `material._Cutoff` curve valued `0.42` on a
two-slot renderer whose materials carry distinct serialized `_Cutoff` values
(`0.10` in slot 0, `0.90` in slot 1), with the control confirmed:

- neither material object was mutated — `sharedMaterials[0]` still read `0.10`
  and `sharedMaterials[1]` still read `0.90`, and both slots still held the
  fixture's own material instances;
- `renderer.HasPropertyBlock()` became `true`;
- the renderer-wide `renderer.GetPropertyBlock(block)` was non-empty and carried
  `_Cutoff == 0.42`;
- the per-material-index overloads `renderer.GetPropertyBlock(block, 0)` and
  `renderer.GetPropertyBlock(block, 1)` were both **empty**, carried no `_Cutoff`,
  and returned `0` from `GetFloat("_Cutoff")`;
- nothing persisted after `StopAnimationMode()`: both materials kept their
  serialized values and `HasPropertyBlock()` returned to `false`.

Animated material properties are therefore applied as **renderer-wide
`MaterialPropertyBlock` state**, not by mutating material objects and not through
any per-material-index block. A renderer-wide block is not slot-scoped, so a bare
`material.<Property>` binding overrides that property for **every** material slot
the renderer draws. The semantics are renderer-wide, not slot-0-only and not
unresolved. This is the observation from which the mapping rule for bare material
bindings is selected, and it must not be selected from binding syntax.

One rule is recorded here for implementation and is **not** settled by
observation, because no observation can settle it: `GetAnimatableBindings`
establishes what Unity *generates* in this fixture, not what every clip in the
ecosystem contains. Clips are authored, generated, and rewritten by many tools,
and AMUSE reads whatever the committed graph holds. During capture, a renderer
material-property binding whose syntax AMUSE does not recognize, and which could
name a proof-relevant material property, MUST produce a **named conservative
refusal**. It must never be silently classified as irrelevant: silently ignoring
an unparsed binding that in fact drives a proof input is a false positive, which
this project treats as a correctness bug rather than a tradeoff.

## Testing strategy

Testing follows the repository's existing separation between analysis and mutation, and this branch's work is entirely on the analysis side.

- **Capture and analysis are tested independently.** Admitted-state construction is exercised over immutable inputs without a live controller graph, so a failure is attributable to capture, to admitted-state construction, or to proof composition.
- **Synthetic fixtures are executable specifications.** Tiny fixtures that isolate one rule are preferred over production avatars: one constant curve, one interpolating curve, one property re-asserted to the same value by two clips, one property written to differing values by two clips, one two-material swap, one additive layer, one unnormalized Direct Blend Tree, one mesh-replacement curve, one over-cap product, one clip carrying a single animation event and an otherwise identical clip carrying none.
- **Conservative refusal is tested as a first-class outcome**, not as an absence of results. Every named refusal has a test that demonstrates the refusal and its reason.
- **The intersection property is tested directly**: a renderer whose admitted states disagree on some faces must yield exactly the faces on which they agree.
- **Determinism**: the same immutable input yields the same admitted set, the same deduplicated resolutions, and the same plan.
- The recursive evidence guard is itself tested against a nested type that would have passed the shallow check.

## What this design does not decide

- The concrete `RendererAnalysisRefusal` member names and their ordering.
- Whether the capture pass and barrier pass are separate NDMF passes of one plugin or one pass pair with shared build state.
- The final admitted-state cap, pending evidence.
- Anything in the *Non-goals* list.

Each is settled during implementation planning or by later evidence, and none of them can change the theorem.
