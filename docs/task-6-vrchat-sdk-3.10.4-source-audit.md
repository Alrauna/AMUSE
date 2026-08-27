# Task 6 VRChat SDK source-audit spike

## 1. Question

Can AMUSE, against Unity 2022.3.22f1, NDMF 1.14.4, and VRChat SDK Base/Avatars 3.10.4, call the retained
`IPlatformAnimatorBindings.GetInnateControllers(avatarRoot)` after `AnimatorServicesContext` has activated,
virtualized, committed, and deactivated without changing effective avatar/controller/playable semantics?

This report distinguishes direct source facts, reasoned lifecycle inferences, and residual unknowns. It does not
claim that `GetInnateControllers` is universally pure.

## 2. Pinned versions and source provenance

- Unity: the installed editor is `/Applications/Unity/Hub/Editor/2022.3.22f1`; the spike did not open it or run Unity.
- NDMF: the AMUSE development project pins `nadena.dev.ndmf` 1.14.4 in `Packages/vpm-manifest.json`, and the embedded
  package declares 1.14.4 in `package.json`. The authoritative upstream tag `1.14.4` resolves to annotated tag
  `b3e045d6be06aa3e8e0be3c77c351e50a61fcaf0`, peeled commit
  `7cf8a13444ac19e46ac2b4146bad209de15dc42d`. The three load-bearing local source files are byte-identical to the
  tag archive:
  - `VRChatPlatformAnimatorBindings.cs`: SHA-256
    `4b3b6aed8471b9bbbeac3680332b5a3bb107ebf4b45dbef5a59b956331e31aca`.
  - `VirtualControllerContext.cs`: SHA-256
    `90b9c86122078a6ddeb1e2c5276e10fa73fce1c588295047ed092d81a97cfd91`.
  - `GenericPlatformAnimatorBindings.cs`: SHA-256
    `0b50ade45c30ada13cb2f2242848fd65e9431f7485ee5f08093278723beeb3df`.
- VRChat SDK: official assets from the `vrchat/packages` GitHub release tagged `3.10.4`:
  - `com.vrchat.avatars-3.10.4.zip`, release asset 450379306, SHA-256
    `6cb00b0c23cfac2bff244b641142763d158875ff85cc3f64e9ea76464677c7bf`.
  - `com.vrchat.base-3.10.4.zip`, release asset 450379114, SHA-256
    `86e8187a7d8f5fb5a54d442b64d09899be2fab2ef1b7822a3c202549ff05ff6b`.
  Both hashes exactly match the digests published by the official release API. Their embedded `package.json` files
  declare package names `com.vrchat.avatars` / `com.vrchat.base`, version `3.10.4`, Unity `2022.3`; Avatars requires
  Base 3.10.4. Sources were extracted only under `/private/tmp/amuse-task6-sdk-3.10.4`, outside both AMUSE worktrees.

No nearby SDK version, third-party rewrite, Census Lab content, or AMUSE package installation was used.

## 3. Reproduced NDMF call chain

Direct source facts:

1. `VirtualControllerContext.OnActivate` selects `VRChatPlatformAnimatorBindings.Instance` when the root has a
   `VRCAvatarDescriptor` (`VirtualControllerContext.cs:188-199`).
2. It obtains the lazy innate sequence and forces it completely with `GroupBy`, `Last`, and `ToList`
   (`VirtualControllerContext.cs:203-209`).
3. Each retained tuple is placed in `_layerStates` and forced through `Controllers[type]`, virtualizing it
   (`VirtualControllerContext.cs:213-226`).
4. `OnDeactivate` commits every `_layerStates` entry having a non-null virtual controller, builds a key-to-committed
   controller dictionary, and calls the retained platform binding's `CommitControllers`
   (`VirtualControllerContext.cs:318-379`, especially 338-356).
5. VRChat commit edits descriptor base and special layers, delegates generic child-Animator assignment, and sets
   `customizeAnimationLayers = true` (`VRChatPlatformAnimatorBindings.cs:152-187`). It assigns a committed controller
   and clears `isDefault` only where the committed dictionary contains that layer type (166-184).
6. A later call to the retained binding starts the iterator body afresh (`VRChatPlatformAnimatorBindings.cs:100-150`).

The descriptor condition is exactly `baseAnimationLayers == null || baseAnimationLayers.All(l => l.isDefault)`
(`VRChatPlatformAnimatorBindings.cs:109-110`). When true, NDMF creates the registered descriptor editor, locates its
`OnEnable`, invokes it, and destroys the editor (112-117).

## 4. Exact VRChat SDK descriptor editor type

The exact type is the global editor class `AvatarDescriptorEditor3`, declared as a partial class and registered by
`[CustomEditor(typeof(VRCAvatarDescriptor))]` in
`com.vrchat.avatars/Editor/VRCSDK/SDK3A/Components3/VRCAvatarDescriptorEditor3.cs:9-10`.

NDMF uses `Editor.CreateEditor(vrcAvatarDescriptor)` and then reflection on the resulting runtime editor type, so this
is the type whose public `OnEnable` is explicitly invoked.

## 5. `OnEnable` call graph

The complete relevant source-visible chain is:

```text
VRChatPlatformAnimatorBindings.GetInnateControllers
  -> GenericPlatformAnimatorBindings.GetInnateControllers (read/yield only)
  -> descriptor null/all-default predicate
  -> Editor.CreateEditor(VRCAvatarDescriptor) -> AvatarDescriptorEditor3
  -> AvatarDescriptorEditor3.OnEnable
       -> cache target as avatarDescriptor
       -> GetComponent<PipelineManager>
          -> AddComponent<PipelineManager> only if absent
       -> _doCustomizeAnimLayers getter
          -> serializedObject.FindProperty("customizeAnimationLayers")
       -> ResetAnimLayersToDefault only when customizeAnimationLayers is false
          -> ClearArray(baseAnimationLayers)
          -> ClearArray(specialAnimationLayers)
          -> InitAnimLayer for the canonical base and special layer lists
             -> InsertArrayElementAtIndex
             -> write type
             -> write isDefault=true
          -> ApplyModifiedProperties
       -> EnforceAnimLayerSetup(true)
          -> _animator getter -> target.GetComponent<Animator>()
          -> human: inspect base layers; SetLayerMaskFromController for Gesture/FX;
             add missing Additive/Gesture default layers
          -> non-human: delete Additive/Gesture base layers
          -> special layers: set isDefault=true when !isDefault && controller==null
       -> ApplyModifiedProperties
       -> InitEyeLook
          -> Resources.Load<Texture> into static editor cache if needed
          -> EditorPrefs.SetBool for two foldouts
       -> Init_Expressions (empty)
       -> Init_Colliders (empty)
  -> DestroyImmediate(editor)
  -> write descriptor.customizeAnimationLayers=true
  -> enumerate base/special layers; use assigned controller or GetFallbackController
     -> AssetDatabase.LoadAssetAtPath<AnimatorController>
  -> yield non-null controller tuples
```

Sources: `VRCAvatarDescriptorEditor3.cs:19-47`; `VRCAvatarDescriptorEditor3AnimLayerGui.cs:12-25`;
`VRCAvatarDescriptorEditor3AnimLayerInit.cs:12-204`; `VRCAvatarDescriptorEditor3EyeLook.cs:33-40`;
`VRCAvatarDescriptorEditor3_Expressions.cs:10-12`; `VRCAvatarDescriptorEditor3_Colliders.cs:8-10`.

`Editor.CreateEditor` may itself participate in Unity editor lifecycle callbacks. That does not widen the write set:
the explicitly invoked callback is the same `OnEnable`, and the fixed-point reasoning below holds for any repeated
execution of that callback during each creation.

## 6. Relevant state writes

| Declaring type / method | Source | Target and write | Condition/value | Semantic relevance |
| --- | --- | --- | --- | --- |
| `AvatarDescriptorEditor3.OnEnable` | `VRCAvatarDescriptorEditor3.cs:28-35` | New editor instance fields `avatarDescriptor`, `pipelineManager`; avatar root gains `PipelineManager` if absent | Cache target/component; add component only when absent | Editor fields are nonsemantic. Component addition changes build-object structure, but first activation necessarily establishes it and NDMF commit does not remove it. The post call's add branch is unreachable in this lifecycle. |
| `ResetAnimLayersToDefault` | `VRCAvatarDescriptorEditor3AnimLayerInit.cs:83-105` | Clears/rebuilds `baseAnimationLayers` and `specialAnimationLayers`; writes each element's `type` and `isDefault=true` | Only if `customizeAnimationLayers == false` | Semantic in isolation. Post-deactivation branch is unreachable because first enumeration sets customize true at NDMF line 122 and commit reasserts it at line 164. Controller refs/masks in rebuilt elements are defaults from new serialized elements. |
| `InitAnimLayer` | same file:125-133 | Array insertion; `type`; `isDefault` | Reset, or missing human Additive/Gesture | Semantic in isolation. In post-editor-reachable states the reset branch is impossible; human missing-layer insertion would have happened on the first callback and, with a mapped layer, makes later editor entry false after commit. |
| `SetLayerMaskFromController` | same file:108-123 | Layer `mask = controller.layers[0].avatarMask` | Gesture/FX, non-default, non-null controller | Playable-layer semantic. First callback/commit establishes the mask. More strongly, a post call whose editor predicate is true cannot have a committed non-default base Gesture/FX layer, so this write is not newly reachable there. |
| `DeleteAnimLayers` via `EnforceAnimLayerSetup` | `VRCAvatarDescriptorEditor3AnimLayerGui.cs:101-108`; init file:171-185 | Deletes Additive/Gesture base elements | Animator exists and is non-human | Semantic. First callback reaches the same fixed point; commit never re-adds these elements, so repeat deletion has no target. |
| `EnforceAnimLayerSetup(true)` | init file:188-201 | Special-layer `isDefault = true` | `!isDefault && animatorController == null`; `isOnEnable` is true | Semantic. First callback normalizes every such entry. Commit either leaves an unresolved/default entry default or assigns a non-null committed controller while clearing default. It cannot create `!default && null`; repeat has no new target. |
| `SerializedObject.ApplyModifiedProperties` | `VRCAvatarDescriptorEditor3.cs:41-42`; init file:104 | Applies the serialized writes above to the descriptor | After reset/enforcement | Semantic only to the extent of the enumerated serialized writes; there is no additional SDK-source write. On a reachable repeat those writes are absent/already fixed. |
| `InitEyeLook` | `VRCAvatarDescriptorEditor3EyeLook.cs:33-40` | Static `_linkIcon`; two `EditorPrefs` booleans | Cache resource if null; set foldout prefs true | Nonsemantic editor/cache state; no avatar/controller/playable write. |
| `Init_Expressions` | `VRCAvatarDescriptorEditor3_Expressions.cs:10-12` | None | Empty method | No expression write. |
| `Init_Colliders` | `VRCAvatarDescriptorEditor3_Colliders.cs:8-10` | None | Empty method | No collider write. (`UpdateAutoColliders` is inspector drawing code and is not called by `OnEnable`.) |
| NDMF `GetInnateControllers` | `VRChatPlatformAnimatorBindings.cs:120-122` | `VRCAvatarDescriptor.customizeAnimationLayers = true` | Every descriptor enumeration after editor branch | Semantic guard flag, but first activation necessarily sets it and commit reasserts it. Repeat writes the identical already-established value. |
| NDMF `CommitControllers.EditLayers` | same file:166-184 | `animatorController`, `isDefault=false`, Gesture/FX `mask` | Matching committed key | First-commit write, not a post-enumeration write; load-bearing for reachability/fixed-point proof. |
| Generic NDMF commit | `GenericPlatformAnimatorBindings.cs:37-45` | Child `Animator.runtimeAnimatorController` | Key is live `Animator` | First-commit write only. Later generic enumeration (`24-35`) merely reads/yields and does not assign. |
| NDMF fallback lookup | `VRChatPlatformAnimatorBindings.cs:40-81, 128-146` | Editor asset lookup/cache only | Default/null layer with mapped type | No descriptor or controller-asset mutation in source. All mapped official fallback files exist in the exact Avatars artifact. |
| Editor destruction | NDMF file:114-117 | Destroys temporary editor object | Descriptor predicate true | Temporary editor lifetime only; descriptor/component writes already accounted above. |

No `OnEnable` path writes controller asset contents, expression menu/parameters, `customExpressions`, collider configuration,
lip-sync state, or other descriptor fields. The expression and collider initializers are empty in 3.10.4.

## 7. Side-effect classification table

| Side effect | Category | Exact reasoning |
| --- | ---: | --- |
| Child-Animator enumeration and tuple production | 2 — Nonsemantic editor/cache effect | Read/yield only; no state write. |
| Descriptor-editor object creation, editor instance-field caches, destruction | 2 — Nonsemantic editor/cache effect | Temporary editor state only. Any callback writes are classified separately. |
| `PipelineManager` creation branch | 1 — Idempotent build-state reassertion | The first activation necessarily runs the same callback and adds it if absent; commit does not remove it. On the immediate post-deactivation call the branch is false. This is not a universal claim if an intervening actor removes the component. |
| Reset/rebuild both animation-layer arrays | 1 — Idempotent build-state reassertion | Semantic in isolation but unreachable post-deactivation: NDMF has necessarily set and committed `customizeAnimationLayers=true`. |
| Human Additive/Gesture insertion | 1 — Idempotent build-state reassertion | First callback inserts missing layers. If the editor predicate could otherwise recur, their mapped fallbacks are enumerated and commit makes the base set not all-default, preventing recurrence. |
| Non-human Additive/Gesture deletion | 1 — Idempotent build-state reassertion | First callback deletes all matching entries; commit does not recreate them. Repeat is a no-op. |
| Gesture/FX mask normalization | 1 — Idempotent build-state reassertion | First callback/commit uses the first layer mask of the controller. In the subset where the editor predicate remains true, no committed non-default mapped base layer exists, so no new mask write is reachable. |
| Special-layer `!default && null -> default` repair | 1 — Idempotent build-state reassertion | First callback repairs it. Commit can produce only non-default plus non-null for a matched key, never non-default plus null. |
| `ApplyModifiedProperties` | 1 — Idempotent build-state reassertion | It applies only the accounted serialized changes; the reachable repeat has no new semantic delta. |
| `_linkIcon` resource cache and foldout `EditorPrefs` | 2 — Nonsemantic editor/cache effect | Editor-global UI/cache state, not avatar/controller/playable state. |
| Empty expression/collider initializers | 2 — Nonsemantic editor/cache effect | No write. |
| `customizeAnimationLayers = true` | 1 — Idempotent build-state reassertion | First enumeration and commit necessarily established the exact value. It is a real assignment, not literal purity. |
| Fallback `AssetDatabase` loads | 2 — Nonsemantic editor/cache effect | Asset lookup/load only; no avatar or controller asset write in this path. |

No relevant effect remains in Category 3 or Category 4 for the exact immediate post-deactivation lifecycle. Several effects
would be Category 3 in an arbitrary call (array reset, masks, layer repair, component addition); their classification as
Category 1 is deliberately lifecycle-scoped and rests on first-activation/commit guarantees.

## 8. Post-first-commit reachability analysis

The descriptor-editor path is **still reachable** after first commit. Exact binary metadata from the official 3.10.4
`VRCSDK3A.dll` shows `AnimLayerType` values `Base`, `Deprecated0`, `Additive`, `Gesture`, `Action`, `FX`, `Sitting`,
`TPose`, and `IKPose`. NDMF maps every value except `Deprecated0`. A customized descriptor with no Animator (or a
non-human Animator) and an empty base array, or only all-default `Deprecated0` base entries with no assigned controller,
can therefore remain null/all-default after commit.

That reachability does not imply semantic mutation:

1. If customize was false on first activation, first `OnEnable` rebuilds canonical mapped layers. NDMF then sets customize
   true; at least the canonical mapped base layers are yielded when available and committed non-default. The later editor
   predicate is false. Even if a mapped fallback lookup were unavailable, customize is still true and the same initializer
   has already reached its fixed point.
2. If customize was true, reset is skipped on both calls. First `EnforceAnimLayerSetup(true)` performs all human/non-human
   and special-layer normalization before enumeration.
3. Commit only adds non-null committed controllers, clears default, and sets masks. It never creates any condition repaired
   by the next callback: it does not set customize false, does not remove `PipelineManager`, does not add non-human
   Additive/Gesture layers, and does not create a special layer that is simultaneously non-default and null.
4. Therefore, when the later predicate remains true (degenerate/unresolved base sets), the repeated callback performs only
   temporary editor/cache effects and identical reassertions. It cannot change the effective descriptor controller graph or
   playable-layer configuration.

Classification among the requested A-E choices: **C — possible, but only an idempotent re-assertion of state already
necessarily established**, with many branches reducing to a literal no-op. The call is not literally side-effect free.

## 9. Any narrower public NDMF observation route

No narrower public route was found in NDMF 1.14.4.

- `IPlatformAnimatorBindings` exposes `GetInnateControllers`, `CommitControllers`, and behavior hooks, but no committed-map
  accessor (`IPlatformAnimatorBindings.cs:14-112`).
- Retaining `VirtualControllerContext` exposes `Controllers`, but its values are `VirtualAnimatorController`, not committed
  `RuntimeAnimatorController` objects (`VirtualControllerContext.cs:160-167`).
- The committed controller is stored in private nested `LayerState.SavedCommittedController`
  (`VirtualControllerContext.cs:42-62`).
- `CommitContext.CommitObject` and `ICommittable<T>` are internal; `VirtualNode.OriginalObject` is protected and its exposed
  accessor is internal.
- `AnimatorServicesContext` clears its controller-context reference on deactivation (`AnimatorServicesContext.cs:51-58`).

Thus pre-commit tuple snapshots remain stale evidence, and accessing saved committed objects would require prohibited
private/internal access. The existing retained-binding route is the only proven public route found.

## 10. Residual unknowns

- Unity's internal implementation of `Editor.CreateEditor` is not part of the SDK source. It may automatically invoke
  `OnEnable` before NDMF's explicit invocation. This does not affect the theorem because every source-visible callback write
  has been proven fixed-point for any repetition in the lifecycle.
- The theorem assumes the exact AMUSE plan's immediate capture/deactivate/barrier sequence: no intervening actor removes the
  newly ensured `PipelineManager`, sets customize false, or otherwise mutates the descriptor between commit and observation.
  Such an intervening mutation would be a different lifecycle and is not authorized by this report.
- This is source proof, not a Unity behavioral fixture. No Unity test was run because the spike expressly prohibited
  implementation and the exact source closes the former callback uncertainty.

These residuals do not leave a Category 3/4 write reachable under the stated lifecycle.

## 11. Verdict A / B / C

**VERDICT A — Task 6 can resume unchanged.**

Exact narrow theorem:

> Against Unity 2022.3.22f1, NDMF 1.14.4, and official VRChat SDK Base/Avatars 3.10.4, in AMUSE's immediate
> capture-pass -> AnimatorServices activation/commit/deactivation -> extension-free barrier-pass lifecycle, a retained
> `IPlatformAnimatorBindings.GetInnateControllers(root)` call may re-create `AvatarDescriptorEditor3` and execute its
> `OnEnable`, including more than once. First activation plus commit necessarily establishes
> `customizeAnimationLayers=true`, the PipelineManager presence, and the fixed point of every layer normalization that can
> remain reachable. Commit cannot create a newly repairable state. Consequently every reachable post-deactivation write is
> either nonsemantic editor/cache state or an idempotent reassertion, and the call cannot change the effective descriptor,
> controller, or playable-layer graph.

This theorem does **not** say `GetInnateControllers` is universally side-effect free.

## 12. Exact evidence supporting the verdict

- NDMF forces first enumeration: `VirtualControllerContext.cs:203-226`.
- SDK `OnEnable` complete call set: `VRCAvatarDescriptorEditor3.cs:19-47`.
- Reset is gated solely by customize false: main editor lines 38-39; NDMF necessarily sets true at
  `VRChatPlatformAnimatorBindings.cs:122` and commit reasserts it at 164.
- Complete layer repair logic: `VRCAvatarDescriptorEditor3AnimLayerInit.cs:83-202`.
- Commit's possible layer-state outputs: `VRChatPlatformAnimatorBindings.cs:166-184`.
- Expression and collider initialization are empty in this exact SDK: expression lines 10-12; collider lines 8-10.
- Eye initialization writes only resource/UI preference state: eye file lines 33-40.
- Post path reachability is real, not assumed away: official 3.10.4 enum metadata contains unmapped `Deprecated0`, and
  empty arrays satisfy `All` vacuously; NDMF's switch maps only the other eight values (`VRChatPlatformAnimatorBindings.cs:40-81`).
- No public committed-map alternative exists: public interface/context evidence listed in section 9.

## 13. Recommended next architectural decision

Resume Task 6 on `feat/alpha-runtime-state-envelope` without changing the two-pass design, but preserve the narrow wording:

- test the retained post-deactivation enumeration as already planned;
- describe the call as semantics-preserving/idempotent for the pinned immediate lifecycle, never as side-effect free;
- record that descriptor-editor re-entry is reachable for degenerate customized arrays but source-proven harmless after the
  first activation/commit fixed point;
- keep the current prohibition on stale pre-commit snapshots, reflection, opaque-key guessing, and private NDMF APIs;
- stop again if the planned focused fixture contradicts this source theorem.

Do not start Task 7 until Task 6's existing test/review/verification gate is completed on the feature branch by a separately
authorized implementation task.

## 14. Lifecycle-completeness follow-up (2026-08-24)

Architectural review requested one final check for source-defined editor lifecycle
callbacks outside the already audited `OnEnable` path.

All eight `AvatarDescriptorEditor3` partial declarations in the same
hash-verified official VRChat SDK 3.10.4 source were exhaustively checked for
lifecycle methods. Across the complete partial class, the only defined `On*`
methods are:

- `OnEnable`
- `OnSceneGUI`
- `OnInspectorGUI`

The complete partial class defines no `Awake`, `OnDisable`, `OnDestroy`,
`OnValidate`, `Reset`, `OnBeforeSerialize`, `OnAfterDeserialize`, or
finalizer, and it does not implement `IDisposable`.

`OnEnable` was already fully audited above and remains fixed-point/idempotent
in the authorized immediate lifecycle. `OnSceneGUI` and `OnInspectorGUI` are
interactive editor entry points; they are not invoked merely by NDMF's temporary
`Editor.CreateEditor(...) -> OnEnable -> DestroyImmediate(...)` sequence.
Therefore no additional VRChat SDK source-defined creation or destruction
lifecycle callback weakens Verdict A.

The official provenance was re-verified for this follow-up:

- `com.vrchat.avatars-3.10.4.zip`
- SHA-256:
  `6cb00b0c23cfac2bff244b641142763d158875ff85cc3f64e9ea76464677c7bf`

This follow-up remains version-pinned to Unity 2022.3.22f1, NDMF 1.14.4, and
official VRChat SDK Base/Avatars 3.10.4. It does not broaden the report beyond
the conclusion that post-deactivation `GetInnateControllers(root)` is
semantics-preserving/idempotent for AMUSE's immediate lifecycle; it does not
claim literal side-effect freedom or universal harmlessness.
