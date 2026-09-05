# Host range re-attestation of the task-6 fixed-point theorem

Date: 2026-09-04. Basis: branch `investigate/host-range-reattestation` at `868f8d9`. Method: source diff of pinned official upstream artifacts. No Unity run.

## 1. Question and bounded scope

The task-6 audit states VERDICT A for Unity 2022.3.22f1, NDMF 1.14.4, and VRChat SDK Base/Avatars 3.10.4 (audit:221-232). The verdict authorizes the one retained AMUSE call, `bindings.GetInnateControllers(avatarRoot)` at `Packages/com.alrauna.amuse/Editor/Host/CommittedControllerGraph.cs:96`. The question here: does the same fixed-point theorem hold for every NDMF and VRChat SDK version that the AMUSE code gate admits today?

Two kinds of change can break the theorem (brief framing, audit sections 3 and 5):

1. The SDK adds a new write to the `OnEnable` call graph that is neither non-semantic editor state nor an idempotent reassertion.
2. NDMF changes the commit path so it no longer establishes the preconditions of the fixed point, above all `customizeAnimationLayers = true`.

Scope limits: source evidence only, from pinned official artifacts. Downloads went to `/tmp/amuse-reattest/`, outside the repository. The baseline is the audit SDK `OnEnable` call graph (audit:72-108) and the audit NDMF call chain (audit:39-57). This note re-derives against those two sections.

## 2. Repository and base state

The repo sits on branch `investigate/host-range-reattestation` at `868f8d9` (merge of PR #49), with a clean tree.

Retained call and lifecycle anchors, re-cited and confirmed by read:

| Anchor | Evidence |
| --- | --- |
| Retained call inside `Enumerate` | `Editor/Host/CommittedControllerGraph.cs:96` (method :88-90) |
| Capture pass with active `AnimatorServicesContext` | `Editor/Build/AmusePlatformFinishPlugin.cs:147-150` |
| Barrier without the extension, so it runs after NDMF deactivation and commit | `AmusePlatformFinishPlugin.cs:152-154` |
| `RequireAnimatorServicesContextInactive` guard | `AmusePlatformFinishPlugin.cs:267-285`, called at :308 |
| The `Enumerate` call | `AmusePlatformFinishPlugin.cs:346-347` |

Widened ranges and the gate:

- `Packages/com.alrauna.amuse/package.json:7-8` declares `"nadena.dev.ndmf": ">=1.14.4 <2.0.0-a"`, the only vpm dependency.
- `Editor/Build/HostLifecycleCapability.cs:79-88` declares `NdmfFloor {1,14,4}`, `NdmfUpperBound {2,0,0}`, `VrchatSdkFloor {3,10,4}`, `VrchatSdkUpperBound {4,0,0}`. The checks run at :117-131.
- `HostLifecycleCapability.cs:306-312`, `PackageVersionAdmitted` refuses any prerelease suffix. The supported-assumption text sits at :150-155. Code-admitted versions are stable releases only.
- Design spec `docs/superpowers/specs/2026-09-04-host-lifecycle-version-policy-design.md`: D2 :77-80, D3 :82-86, D3-RESIDUAL :88-104. The requirement at :97-99 reads: "The widening admits SDK versions where the re-entry write set is unverified. A bounded re-attestation investigation of the task-6 theorem for SDK versions above 3.10.4 is REQUIRED before any AMUSE release ships the widened range."

`HostLifecycleCapability.cs` is the only version-gating code. The package has no Runtime assembly.

## 3. Version inventory

SDK, repo `github.com/vrchat/packages`, releases in `[3.10.4, 4.0.0)`. Release listing re-verified through the API on 2026-09-04: the newest release is 3.10.5, published 2026-09-04T17:06:34Z. Nothing above 3.10.5 exists below 4.0.0.

| Version | Published (UTC) | Prerelease | Gate admission | Artifact and SHA-256 |
| --- | --- | --- | --- | --- |
| 3.10.4 | 2026-06-17T16:22:23Z | no | admitted (audit baseline) | `com.vrchat.avatars-3.10.4.zip` `6cb00b0c23cfac2bff244b641142763d158875ff85cc3f64e9ea76464677c7bf` and `com.vrchat.base-3.10.4.zip` `86e8187a7d8f5fb5a54d442b64d09899be2fab2ef1b7822a3c202549ff05ff6b` |
| 3.10.5-beta.1 | 2026-08-21T21:23:57Z | yes | refused by gate, examined for completeness | `com.vrchat.avatars-3.10.5-beta.1.zip` `d3485505c20c62f1e90e40cc9b787b06b892e74f25dffc3773ad559716437535` and `com.vrchat.base-3.10.5-beta.1.zip` `e3c8d153c7f6400986903377da978a504267b76b1cfea1ff292e7c7acda13ac2` |
| 3.10.5-beta.2 | 2026-08-28T22:43:15Z | yes | refused by gate, examined for completeness | `com.vrchat.avatars-3.10.5-beta.2.zip` `5800eb703f47ae544cb207ef17b8d0f6845e66899fe32fcda891b4ffc74ae312` and `com.vrchat.base-3.10.5-beta.2.zip` `fa5ede80520c1b554465913ef0b1948277e0975f116909b16fbc9d955ea3cb72` |
| 3.10.5 | 2026-09-04T17:06:34Z | no | admitted | `com.vrchat.avatars-3.10.5.zip` `03bdea0c24257070f0e7a73c9033742a1ce0f67463b12a6c2ad29608b1f33a77` and `com.vrchat.base-3.10.5.zip` `fbfb3e7a38778dcb55d7a860286819e6f0726d10d5039f61474bd1b9c629029e` |

The 3.10.4 digests equal the published audit values (audit:27-30). Provenance check passes.

NDMF, repo `github.com/bdunderscore/ndmf`, tags in `[1.14.4, 2.0.0)`. Tag inventory re-verified with `git ls-remote --tags`: the highest tag is 1.14.8. No 1.15 or 2.x tag exists. All five versions are stable, so all five are code-admitted.

| Version | Tag commit (peeled) | Channel and SHA-256 | Provenance note |
| --- | --- | --- | --- |
| 1.14.4 | `7cf8a13444ac19e46ac2b4146bad209de15dc42d` (tag `b3e045d6be06aa3e8e0be3c77c351e50a61fcaf0`) | tag tarball `ndmf-1.14.4.tar.gz` `cdd60d29427e0e5bb15b122e1f266836b6e2a1a7a99ef447f2b889f28e03926f` | audit baseline (audit:17-18) |
| 1.14.5 | `030ebbf6e6b71e56340dd2b2d9552da96d073094` (tag `d91949332dae20fd52d37fc32227b69f031725d9`) | tag tarball `ndmf-1.14.5.tar.gz` `3f36442f2fbd5b1abfe0847d12c09c8ea4ae7d08b74c0561e35cf1cd7a71b93d` | no GitHub release row exists (`/repos/bdunderscore/ndmf/releases/tags/1.14.5` returns 404). Obtained through the pinned-tag channel, the same NDMF provenance the audit used (audit:17-19). Counts as obtained. |
| 1.14.6 | `e090faefb9302d186b3f0cdb8978def2c1f7083f` (tag `10fd4040699ae5bcf5ab095da3a9e0196d226212`) | release zip `nadena.dev.ndmf-1.14.6.zip` `9eb5734b50a838aaa6b246526e3f86bc0f881a32e4cbe294d6ff5d1000368e37` | release 2026-08-22 |
| 1.14.7 | `8ccc91c7ae871c4eaf2f9f7d8f87ac8bd3c8af33` (tag `89c29992b2e738bdecf3c520f7ee3e452b7c90c0`) | release zip `nadena.dev.ndmf-1.14.7.zip` `82564a9b559239db5be9e351b40357ba8feb6373d3fc4e30dabe3ff76aa06054` | release 2026-08-28 |
| 1.14.8 | `89c8f6d18918cdd54fa20148070bea0578ccc1b6` (tag `31d794d2bc44fbea0e846e6fa88df2b607c895db`) | release zip `nadena.dev.ndmf-1.14.8.zip` `e1103ea150b9f1d49d415d632ed4da4e46cd8121a1f1f7ea917b155193a1ab83` | release 2026-08-29 |

Embedded package identities: every extracted tree declares its exact name, version, and `unity: 2022.3` in `package.json`. Each Avatars zip requires its matching base version (`vpmDependencies["com.vrchat.base"]`).

Versions the method could not get: none.

## 4. SDK result

Method: extract the Avatars zip per version, because the six editor files live there (audit:63). Locate the six files by name at `Editor/VRCSDK/SDK3A/Components3/`, then digest. All six files exist at every version at the same relative path. No rename, no move.

Baseline digests at 3.10.4:

| File | SHA-256 at 3.10.4 |
| --- | --- |
| `VRCAvatarDescriptorEditor3.cs` | `b70f0e8da94ed07abaf75384698e1cbf8a85810df408131c88127af715380f68` |
| `VRCAvatarDescriptorEditor3AnimLayerGui.cs` | `0773ea8229e7a90971fa412766f19d338a378b37d94ed48a9f6d851fc09164bd` |
| `VRCAvatarDescriptorEditor3AnimLayerInit.cs` | `b5eb07a60328a34247344c3bdbb8cc4d334509d3494dee9154e8afcaa7b86cde` |
| `VRCAvatarDescriptorEditor3EyeLook.cs` | `a6337b5edd478e287f6c10df873f192e8f5777865d6201a5859f9f8aaf547ce7` |
| `VRCAvatarDescriptorEditor3_Expressions.cs` | `d6466ef1fb0ac06297aedac6eb2cd560b1428ffdc60ca534a775059bcf56a1de` |
| `VRCAvatarDescriptorEditor3_Colliders.cs` | `0c1878dbb508dcd9b6fc18fdcd0f90c4af6a149d15c560474012271abe1961ca` |

Per-version result:

| Version | Files changed from 3.10.4 | Digest of the changed file | Class of change | Initializer re-verification |
| --- | --- | --- | --- | --- |
| 3.10.4 | none (baseline reference row) | n/a | n/a | both empty (quoted below) |
| 3.10.5-beta.1 | `VRCAvatarDescriptorEditor3_Colliders.cs` only | `04e7b95b67d57c5057aac2d429c0f4bb4ea50af49350274a440cf6bb35a18699` | (a), inspector and scene GUI only, not in the OnEnable graph | both empty, Expressions byte-identical to baseline, Colliders body still empty at :8-10 |
| 3.10.5-beta.2 | `VRCAvatarDescriptorEditor3_Colliders.cs` only | `04e7b95b67d57c5057aac2d429c0f4bb4ea50af49350274a440cf6bb35a18699` | same as beta.1 | same as beta.1 |
| 3.10.5 | `VRCAvatarDescriptorEditor3_Colliders.cs` only | `04e7b95b67d57c5057aac2d429c0f4bb4ea50af49350274a440cf6bb35a18699` | same as beta.1 | both empty (quoted below) |

Five of six files are byte-identical across all four versions, so the audited OnEnable graph (audit:72-108) carries over with no shift. One file changed: `VRCAvatarDescriptorEditor3_Colliders.cs`, first at 3.10.5-beta.1 (2026-08-21), unchanged since. The file went from 489 to 465 lines.

The Colliders change, classified. The diff touches four methods. Full diff captured at `/tmp/amuse-reattest/colliders-3104-to-3105.diff`.

| Method (3.10.5 lines) | Change | Class |
| --- | --- | --- |
| `DrawInspector_Colliders` (:13-55) | computes `avatarWorldToLocal` and passes it to `MirrorCollider` | (a) inspector GUI, not reachable from OnEnable |
| `MirrorCollider` (:56-86) | mirrors position and rotation through avatar space instead of raw world space | (a) GUI math for the inspector |
| `UpdateAutoColliders` (:160-164) | body replaced by `avatarDescriptor.UpdateAutoColliders()`, kept as a private stub for community tooling that calls it by reflection | (a) caller is `DrawInspector_Colliders`, unchanged |
| `DrawScene_Colliders` (:194-451) | same matrix threading and `MirrorCollider` call site | (a) scene GUI |

Old-to-new map from the diff hunk headers: old 17-22 to new 17-24. Old 45-57 to new 49-61. Old 64-79 to new 68-90. Old 147-192 to new 158-168. Old 222-227 to new 196-203. Old 250-256 to new 229-235.

`Init_Colliders` and `Init_Expressions` are the only Colliders-file and Expressions-file methods that OnEnable calls. Both remain empty in every version. Quotes:

- `Init_Expressions`, 3.10.5 lines 10-12: `void Init_Expressions() { }`. The file digest is identical in all four versions.
- `Init_Colliders`, 3.10.4 lines 8-10 and 3.10.5 lines 8-10: `private void Init_Colliders() { }`. The beta.1 and beta.2 files are byte-identical to the 3.10.5 file.

`OnEnable` at 3.10.5 lines 19-47 keeps the exact audited call set. It caches the descriptor and gets `PipelineManager`. It resets only when `customizeAnimationLayers` is false. It then enforces layer setup and applies serialized properties. Finally it runs the eye-look init and the two empty initializers. The declaring file is byte-identical to 3.10.4 (`b70f0e8d...`).

[REASONING] The runtime method `avatarDescriptor.UpdateAutoColliders()` lives in the compiled SDK runtime, outside the six source files. The OnEnable graph never reaches it, so this note does not audit it.

## 5. NDMF result

Method: tag tarball for 1.14.4 and 1.14.5, release zip for 1.14.6 through 1.14.8. The three pinned files sit at the same relative paths in every tree. No rename, no move.

Per-version result, three pinned files:

| Version | `VRChatPlatformAnimatorBindings.cs` | `GenericPlatformAnimatorBindings.cs` | `VirtualControllerContext.cs` |
| --- | --- | --- | --- |
| 1.14.4 | `4b3b6aed...31aca` (baseline) | `0b50ade4...3bdf` (baseline) | `90b9c861...cfd91` (baseline) |
| 1.14.5 | byte-identical to baseline | byte-identical to baseline | byte-identical to baseline |
| 1.14.6 | byte-identical to baseline | byte-identical to baseline | byte-identical to baseline |
| 1.14.7 | byte-identical to baseline | byte-identical to baseline | `f69ba14e...958a2` (changed) |
| 1.14.8 | byte-identical to baseline | byte-identical to baseline | `f69ba14e...958a2` (same as 1.14.7) |

Full digests:

- `VRChatPlatformAnimatorBindings.cs`: `4b3b6aed8471b9bbbeac3680332b5a3bb107ebf4b45dbef5a59b956331e31aca`, byte-identical 1.14.4 through 1.14.8. All audit line anchors carry over with no shift: default predicate :109-110, editor create and invoke and destroy :112-117 (`Editor.CreateEditor` :114, `OnEnable` invoke :116, `DestroyImmediate` :117), `customizeAnimationLayers = true` write :120-122 (assignment :122), commit `EditLayers` :152-187 (assignment region :166-184, customize reassert :164).
- `GenericPlatformAnimatorBindings.cs`: `0b50ade45c30ada13cb2f2242848fd65e9431f7485ee5f08093278723beeb3df`, byte-identical across all five versions.
- `VirtualControllerContext.cs`: baseline `90b9c86122078a6ddeb1e2c5276e10fa73fce1c588295047ed092d81a97cfd91` holds for 1.14.4, 1.14.5, and 1.14.6. 1.14.7 and 1.14.8 share `f69ba14e946b5c700381db6a5871657df8196767f9fe4ada124f2006692958a2`.

The one `VirtualControllerContext` change, 1.14.6 to 1.14.7, is a single hunk (458 to 459 lines):

```text
                     CacheInvalidationToken++;
                     _layerStates[k] = new LayerState(null) { VirtualController = v };
-                }
+                },
+                _ => CacheInvalidationToken++
             );
```

Old lines 181-186 map to new lines 181-187. Every later line shifts by +1. The `FilteredDictionaryView` construction in `OnActivate` gains a third lambda. `FilteredDictionaryView.cs` gained an optional `Action<K> removalCallback` parameter (14 insertions, 4 deletions), invoked after a successful `Remove` and once per key in the prune path. So the new lambda increments `CacheInvalidationToken` when a layer-state key leaves the `Controllers` view. The 1.14.7 changelog states the intent: "Fixed stale animation-index/controller caches and incorrect layer behavior when virtual animator layers or object curves are removed or replaced." [SOURCE: `ndmf-1.14.7/CHANGELOG.md`, `[1.14.7] - [2026-08-28]` heading, entry `[#830]`]

Line map for the audited regions, 1.14.4 to 1.14.7/1.14.8 (content identical in every case):

- `OnActivate` bindings selection: :188-199 to :189-200. The `GetInnateControllers(root)` call: :203 to :204. The forced `Controllers[type]` virtualization: :226 to :227.
- `OnDeactivate` commit path: :318-379 to :319-380. Commit dictionary build: :338-352 to :339-353. The `_platformBindings.CommitControllers(root, controllers)` call: :356 to :357.

Break-kind-2 answers:

1. Does the commit path still establish `customizeAnimationLayers = true` before the retained call runs? Yes. `OnDeactivate` (1.14.8 :319-380) still builds the committed dictionary and calls `CommitControllers` (:357). `CommitControllers` in the byte-identical bindings file reasserts `customizeAnimationLayers = true` (:164) and assigns committed controllers with `isDefault = false` and Gesture/FX masks (:170-184).
2. Does `GetInnateControllers` still perform its own write, rebuild, and predicate? Yes, byte-identical: predicate :109-110, editor create and invoke and destroy :112-117, write :120-122, then the fallback and yield walk :124-149.

Classification of the single hunk: (a), non-semantic NDMF-internal editor cache state. [REASONING] The lambda increments an NDMF counter on view removal. It writes no descriptor field, no controller asset, and no playable-layer state, and it sits outside the SDK OnEnable graph. The commit path and both bindings files are byte-identical, so the commit establishes exactly the audited preconditions.

Change-surface observation, outside the three pins: the 1.14.6-to-1.14.7 full-tree diff shows dozens of changed files (`CloneContext.cs`, `VirtualAnimatorController.cs`, `FilteredDictionaryView.cs`, and others). The theorem sources are the three pinned files plus the SDK six, per the audit scope. This note reads `FilteredDictionaryView.cs` only because the pinned hunk semantics depend on it. The other files lie outside this method.

## 6. Theorem status

| Version | Status | Deciding evidence |
| --- | --- | --- |
| SDK 3.10.4 + NDMF 1.14.4 | HOLDS | Baseline, audit:221-232. |
| NDMF 1.14.5 | HOLDS | Three pinned files byte-identical to 1.14.4. The audit argument transfers unchanged. |
| NDMF 1.14.6 | HOLDS | Same. |
| NDMF 1.14.7 | HOLDS WITH A NEW ARGUMENT | The single hunk is class (a). [REASONING] The added lambda increments an NDMF-internal cache token on view removal. It writes no descriptor, controller, or playable state. The commit path and the write set are byte-identical to the audited path. So every reachable post-deactivation write stays non-semantic or idempotent. |
| NDMF 1.14.8 | HOLDS WITH A NEW ARGUMENT | The three pinned files equal 1.14.7 byte for byte. Same argument. |
| SDK 3.10.5 | HOLDS WITH A NEW ARGUMENT | [REASONING] The five OnEnable-graph files are byte-identical to 3.10.4. The changed Colliders file touches inspector and scene GUI only. `Init_Colliders`, the one Colliders method OnEnable calls, is still empty at :8-10. So the OnEnable write set equals the audited set, and the audit fixed-point reasoning applies unchanged. |
| SDK 3.10.5-beta.1 and beta.2 | excluded by gate, examined for completeness | `PackageVersionAdmitted` refuses the prerelease suffix (`HostLifecycleCapability.cs:306-312`). Their Colliders file equals the 3.10.5 file (`04e7b95b...`). If the gate admitted them, the 3.10.5 argument would transfer. |

## 7. Options for the controller

1. Keep the widened ranges as declared. Cost: no code change. The theorem holds for every version admitted today. Risk: the ranges admit future releases that this note cannot see, so each new release inside the ranges re-opens the hole until someone re-attests.
2. Narrow the ceilings to just above the highest attested versions: `NdmfUpperBound {1,14,9}`, `VrchatSdkUpperBound {3,10,6}`. Cost: two constant edits now, plus a re-attestation each time a new 1.14.x or 3.10.x ships. Gain: the gate refuses any version whose sources nobody attested.
3. Re-pin exactly (NDMF 1.14.4, SDK 3.10.4) and accept the coverage loss. Cost: users on SDK 3.10.5 or NDMF 1.14.5+ cannot run AMUSE. The audit argument stays maximally narrow, and the re-attestation work above loses its value.
4. Move the proof from a version gate to a runtime check of the descriptor state. Snapshot the descriptor before the retained call and compare after: `customizeAnimationLayers`, both layer arrays, `PipelineManager` presence, collider configuration. Cost: new snapshot and compare code, a defined state vector, and a residual, because editor-cache writes stay unproven and only descriptor semantics get guarded. Gain: the safety argument survives future upstream changes instead of trailing them.

Recommendation: option 2 gives the strongest guarantee per unit of cost today. Option 4 is the durable end state if the gate should stop tracking upstream releases. This note recommends. The controller decides.

## 8. Stop conditions and their status

1. Theorem BROKEN for an admitted version: NOT TRIGGERED. No SDK-side new write in the OnEnable graph (section 4) and no NDMF-side commit-path change (section 5).
2. An admitted version unobtainable: NOT TRIGGERED. All admitted artifacts downloaded and digested. Provenance note: NDMF 1.14.5 has no GitHub release row (404), but the tag tarball is the audit NDMF provenance channel, so 1.14.5 counts as obtained.
3. A Unity run or build observation needed: NOT TRIGGERED. All evidence is source-level in the pinned artifacts. The residual stays open in one sentence. Nobody observed the within-stream stability of `Editor.CreateEditor` in Unity. This note cannot observe it.
4. A second retained-call hazard the task-6 audit did not cover: NOT TRIGGERED. Two candidates examined and cleared: the 1.14.7 `FilteredDictionaryView` removal callback (NDMF-internal cache token, section 5) and the SDK runtime `UpdateAutoColliders` move (inspector GUI reachability only, section 4).
5. A later SDK version renames, moves, or deletes one of the six files: NOT TRIGGERED. All six files exist at the same relative path in all four versions.

## 9. What this note proves and does not prove

Proves:

- This note got every admitted version from its official channel and digested it. The admitted set: SDK 3.10.4 and 3.10.5, NDMF 1.14.4 through 1.14.8.
- The task-6 theorem holds for every admitted version, through byte-identity transfer or the two bounded new arguments in section 6.
- The SDK OnEnable call graph at 3.10.5 equals the audited 3.10.4 call graph. The NDMF commit path at 1.14.7 and 1.14.8 equals the audited 1.14.4 path.

Does not prove:

- Anything about Unity runtime behavior. The `Editor.CreateEditor` residual stays open (stop condition 3).
- Anything about compiled runtime code. This note did not decompile the SDK runtime DLLs, for example the new `VRCAvatarDescriptor.UpdateAutoColliders`. The theorem never reaches them.
- Anything about NDMF source outside the three pinned files. The 1.14.7 release changed dozens of files. This note pins the three theorem-bearing files and reads `FilteredDictionaryView.cs` only to classify the pinned hunk.
- Anything about future releases inside the widened ranges.

## 10. Citations

Repository state: branch `investigate/host-range-reattestation`, commit `868f8d9cf561b3cb2a7a1bc666b317befa1a85a0` (merge of PR #49), clean tree at investigation time. The task-6 audit is `docs/task-6-vrchat-sdk-3.10.4-source-audit.md`, cited as audit:N.

Repo anchors:

- `Packages/com.alrauna.amuse/Editor/Host/CommittedControllerGraph.cs:88-96`.
- `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:147-154`, `:267-285`, `:308`, `:346-347`.
- `Packages/com.alrauna.amuse/package.json:7-8`.
- `Packages/com.alrauna.amuse/Editor/Build/HostLifecycleCapability.cs:79-88`, `:117-131`, `:150-155`, `:306-312`.
- `docs/superpowers/specs/2026-09-04-host-lifecycle-version-policy-design.md:77-104`.

Upstream SDK: repo `github.com/vrchat/packages`, release tags as in section 3, asset digests in section 3. The six files sit at `Editor/VRCSDK/SDK3A/Components3/` inside each `com.vrchat.avatars-<version>.zip`, with per-file digests in section 4. OnEnable evidence read from the 3.10.5 tree: `VRCAvatarDescriptorEditor3.cs:19-47`, `VRCAvatarDescriptorEditor3_Expressions.cs:10-12`, `VRCAvatarDescriptorEditor3_Colliders.cs:8-10`. Baseline Colliders evidence read from the 3.10.4 tree: lines 8-10.

Upstream NDMF: repo `github.com/bdunderscore/ndmf`, tag and commit identities in section 3, artifact digests in section 3, per-file digests in section 5. Files read from the 1.14.8 tree: `Editor/API/AnimatorServices/PlatformBindings/VRChatPlatformAnimatorBindings.cs:100-190` (predicate :109-110, editor create and invoke and destroy :112-117, write :120-122, commit :152-187), `Editor/API/AnimatorServices/VirtualControllerContext.cs` (1.14.7 tree: `OnActivate` :169-231, removal callback :186, `OnDeactivate` :319-381, `CommitControllers` call :357), `Editor/API/AnimatorServices/PlatformBindings/GenericPlatformAnimatorBindings.cs` (byte-identical, digest only). Change evidence: 1.14.7 `CHANGELOG.md`, `[1.14.7] - [2026-08-28]`, entry `[#830]`.

Local evidence copies, kept for orchestrator verification: archives under `/tmp/amuse-reattest/dl/`, extracted trees under `/tmp/amuse-reattest/x/` (`sdk-3.10.4`, `sdk-3.10.5-beta.1`, `sdk-3.10.5-beta.2`, `sdk-3.10.5`, `ndmf-1.14.4` through `ndmf-1.14.8`), full Colliders diff at `/tmp/amuse-reattest/colliders-3104-to-3105.diff`.

## 11. Privacy statement

This note uses public upstream artifacts only: official VRChat package releases and the public NDMF repository. No private names, GUIDs, paths, Census Lab content, or per-avatar data appear. All downloads and extracted trees sat under `/tmp/amuse-reattest/`, outside the repository. The repository gained exactly this one file.
