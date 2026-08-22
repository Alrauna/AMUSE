# AMUSE SDK build-environment contract investigation

## Status and scope

**Preliminary findings and Outcome D approved; durable investigation record awaiting review.**

This document records a host-lifecycle investigation. It does not implement a VRChat SDK callback, an AMUSE-controlled build entrypoint, build-attempt storage, callback discovery, a HostLifecycleCapability API, production mutation, or positive lilToon support.

- Branch: `investigate/sdk-build-environment-contract`
- Base: `origin/main` at `2d5f8ed41c12e5e785ee00fbcda70ca7e28cafe7` (merge of PR #13)
- Required predecessor: `1fa0356841f00d2965422782f8cf18f3fc1c39ab` (merge of PR #12) is an ancestor of the base
- Public Unity project: repository root, positively identified through its normalized `Application.dataPath`
- Census Lab/private avatars: not used or modified
- Production code: not modified
- Probe artifacts: disposable; none retained in the repository

The primary result is **Outcome D — Required guarantee not enforceable** for the exact stock host environment characterized here. This is a result about the availability of a positive host capability, not a rejection of AMUSE's fail-closed architecture contract.

## Decision summary

The stock combination of Unity `2022.3.22f1`, NDMF `1.14.4`, VRChat SDK Base `3.10.4`, and VRChat SDK Avatars `3.10.4` cannot provide the positive `HostLifecycleCapability` required before future-dependent mutation.

The missing guarantee is specifically **authoritative pre-mutation invocation and build-attempt association**:

- the SDK's high-level request/start signals refer to the source avatar;
- preprocessing receives a separately created clone;
- no authoritative attempt token connects the request, clone, NDMF mutation, and late SDK validation;
- other routes can invoke the same preprocess chain without the same high-level signals;
- temporal uniqueness or a single pending request narrows ambiguity but does not eliminate it.

Therefore future-dependent mutation remains unavailable for this stock host contract. Independently NDMF-complete transformations are unaffected because they do not depend on future callback evidence or a late commit gate.

Four distinctions are binding:

1. **Architecture contract:** remains valid. It requires positive host lifecycle capability before mutation and conservative refusal otherwise.
2. **Stock SDK 3.10.4 `HostLifecycleCapability`:** unavailable for future-dependent mutation.
3. **Prior assumption or hope:** the expectation that SDK 3.10.4 would furnish authoritative invocation and attempt identity is falsified.
4. **Positive future-dependent support:** blocked unless a new host mechanism is established.

The smallest design question that may later be reopened is:

> Must positive support use an AMUSE-controlled build entrypoint, or can a newer or upstream host API provide authoritative per-attempt identity and invocation classification from request through late validation?

This investigation does not answer or design either alternative.

## Question

Can the exact stock Unity, NDMF, and VRChat SDK environment establish before NDMF mutation that the current invocation belongs to one supported normal SDK build attempt, then carry unambiguous association to an authoritative late preprocess refusal gate?

The required result is stronger than detecting that some build was recently requested. AMUSE must know before the first conditional mutation that:

- this invocation is a supported lifecycle;
- late refusal will be honored;
- the NDMF root and late validation root belong to the same attempt;
- no competing, stale, superseded, direct, or Apply-on-Play invocation can consume the authorization;
- the exact callback environment makes the late validation final for proof-relevant semantics.

## Prior binding architecture

The merged [upload-conditional authorization design](../specs/2026-08-21-upload-conditional-authorization-design.md) requires a positive host capability before any future-dependent evidence may authorize mutation. The capability owns host version, invocation classification, callback environment, ordering, failure enforcement, attempt association, cancellation, and reload safety.

The merged [lilToon build-callback handoff investigation](../specs/2026-08-21-liltoon-build-callback-handoff-design.md) established the intended two-stage lifecycle:

1. apply a conditional transformation during a late NDMF `Optimizing` pass while NDMF still owns generated assets;
2. after lilToon and every other supported semantic mutator, re-extract final evidence and accept or refuse in a VRChat SDK preprocess callback.

The architecture already says that failure to establish the capability produces no mutation. Outcome D is therefore the architecture operating fail-closed. It does not invalidate that structure.

## Non-goals

This investigation does not:

- implement an AMUSE-controlled build entrypoint;
- investigate a newer VRChat SDK version;
- start a host-version branch;
- implement `HostLifecycleCapability` or any production callback;
- select a numeric production gate order;
- design persistent or static attempt storage;
- enable standalone or integration-dependent positive lilToon support;
- move mutation from NDMF to an SDK callback;
- expand semantic certification of external plugins;
- use the Census Lab or private fixtures;
- modify package, project, asset, CI, release, or production source state.

## Exact environment

| Component | Exact identity | Evidence |
|---|---|---|
| Unity | `2022.3.22f1`, revision `887be4894c44` | `ProjectSettings/ProjectVersion.txt` and public-project Unity discovery |
| NDMF | `1.14.4` | embedded `Packages/nadena.dev.ndmf/package.json` and VPM manifest |
| VRChat SDK Base | `3.10.4` | official cached VCC package archive |
| VRChat SDK Avatars | `3.10.4` | official cached VCC package archive |
| SDK Base archive SHA-256 | `86e8187a7d8f5fb5a54d442b64d09899be2fab2ef1b7822a3c202549ff05ff6b` | matched the adjacent supplied checksum |
| SDK Avatars archive SHA-256 | `6cb00b0c23cfac2bff244b641142763d158875ff85cc3f64e9ea76464677c7bf` | matched the adjacent supplied checksum |
| Repository base | `2d5f8ed41c12e5e785ee00fbcda70ca7e28cafe7` | `origin/main` when the branch was created |

The SDK packages were temporarily resolved into the public Unity project for exact-version inspection, then removed. Unity-generated XR settings, package dependency changes, and host-specific toolchain churn were restored or moved to a disposable backup. The final branch returned to a clean tree before this document was created.

The temporary SDK load emitted two classes of host noise:

- NDMF/Harmony `mprotect returned EACCES` errors in this macOS execution environment;
- an Oculus spatializer bundle containing no compatible Apple-silicon architecture.

No successful Unity test or clean-console claim is derived from that temporary load. Neither error is evidence for or against request-to-clone association.

## Evidence hierarchy

- **Public contract:** documented API behavior that downstream code may normally rely on.
- **Pinned implementation fact:** behavior enforced by the exact investigated source or decompiled SDK assembly; valid only for the pinned environment unless reconfirmed.
- **Empirical observation:** behavior reproduced by a controlled exact-version probe.
- **Inference:** a conclusion derived from the preceding evidence, identified as such.
- **Unknown:** a mechanism not established by adequate evidence; it cannot authorize mutation.

Strong conclusions below distinguish these categories. A private field or decompiled control flow is not presented as a public SDK promise. An earlier probe observation is not generalized beyond its environment.

## Source inspection and provenance

The investigation inspected:

- NDMF `1.14.4` source embedded in this repository;
- official cached VRChat SDK Base and Avatars `3.10.4` packages after checksum verification;
- decompiled `VRCSDKBase-Editor.dll` and `VRCSDK3A-Editor.dll` from those exact packages;
- public SDK API source shipped in the packages;
- current AMUSE assembly definitions, package metadata, and binding architecture records;
- the results and retained evidence from the prior exact-version disposable build-callback probe.

The principal SDK types and methods inspected were:

- `VRC.SDKBase.Editor.BuildPipeline.VRCBuildPipelineCallbacks` initialization and preprocess/postprocess dispatch;
- `VRC.SDK3.Builder.VRCAvatarBuilder.ExportCurrentAvatarResource` and avatar export flow;
- `VRCSdkControlPanelAvatarBuilder` build, build-and-upload, build-and-test, event, and cleanup paths;
- `IVRCSdkBuilderApi`, `IVRCSDKBuildRequestedCallback`, and related public interfaces;
- `VRC_SdkBuilder.ActiveBuildType` storage and reset sites.

The principal NDMF paths inspected were:

- `BuildFrameworkPreprocessHook`;
- `BuildFrameworkOptimizeHook`;
- `ApplyOnPlay` preprocessing invocation;
- NDMF phase and sequence ordering, including `Sequence.AfterPlugin` and `Sequence.AfterPass(string)`;
- the PhysBone and constraint reinitialization callbacks.

## Reconstructed lifecycle

### Normal high-level avatar build

1. The SDK control-panel builder calls `OnVRCSDKBuildRequested(Avatar)`.
2. It raises `OnSdkBuildStart`, sets `BuildState` to building, and yields for approximately 100 ms.
3. The lower-level avatar builder selects the source avatar and updates source-side descriptor or pipeline metadata as required.
4. It clones the selected avatar.
5. The clone is passed to `VRCBuildPipelineCallbacks.OnPreprocessAvatar`.
6. The preprocess dispatcher enumerates registered callbacks in ascending numeric `callbackOrder`.
7. NDMF creates its build context at `-11000`, processes through `Transforming`, and later runs `Optimizing` through `PlatformFinish` at `-1025` before calling `BuildContext.Finish()`.
8. Later SDK and third-party callbacks observe the same in-process clone.
9. If every preprocess callback returns success, the builder saves a temporary prefab, destroys the clone, builds the asset bundle, and invokes postprocess callbacks.
10. Upload is entered only after the build succeeds.

### Preprocess failure

If a preprocess callback returns `false`, the dispatcher stops and returns failure. If it throws, the dispatcher catches the exception and also returns failure. The avatar builder then destroys the clone and returns before temporary-prefab save and asset-bundle construction. Postprocess callbacks are skipped.

### Apply-on-Play

Pinned NDMF source calls `VRCBuildPipelineCallbacks.OnPreprocessAvatar` directly for Apply-on-Play, ignores its Boolean result, and does not call the SDK's high-level build-request signal. The root is the source avatar temporarily renamed as a clone rather than the normal builder's independently instantiated clone.

Apply-on-Play can therefore enter substantially the same callback machinery while lacking the failure enforcement and request lifecycle required by future-dependent mutation.

## Supported invocation detection

### Available signals

SDK 3.10.4 exposes several partial signals:

- `IVRCSDKBuildRequestedCallback.OnVRCSDKBuildRequested` identifies Avatar versus Scene;
- `IVRCSdkBuilderApi.OnSdkBuildStart` reports a target object;
- builder `BuildState` reports broad activity;
- `VRC_SdkBuilder.ActiveBuildType` may contain `Publish`, `Test`, or `None`;
- the SDK builder eventually invokes preprocessing with a cloned avatar root.

### Why the signals are insufficient

- The request and start signals identify the source target, not the later clone or a unique attempt.
- No attempt token is passed to preprocess callbacks.
- `ActiveBuildType` is not set by plain public `Build()` and can remain stale when direct API paths do not execute the UI reset handlers.
- `TryGetBuilder<T>` is available only while the SDK control-panel window is open.
- Direct lower-level builder calls bypass part of the high-level lifecycle.
- Apply-on-Play invokes preprocessing without the official request event and ignores failure.
- The high-level build yields before starting the synchronous low-level build, allowing multiple requests or unrelated preprocessing to overlap the observation window.

An AMUSE rule such as “exactly one request is pending” would be a temporal heuristic. It would not prove that the current clone was created by that request, nor exclude a direct/manual preprocess call during the same interval.

### Result

**No reliable supported-build detector exists before NDMF mutation in the stock SDK 3.10.4 lifecycle.**

Conceptually, AMUSE can observe “a build request happened” and later “an avatar entered preprocessing,” but the stock host supplies no receipt proving that both observations belong to the same transaction.

## Callback inventory

`VRCBuildPipelineCallbacks` discovers implementations by scanning `AppDomain.CurrentDomain.GetAssemblies()`, calling `GetTypes()`, and instantiating non-abstract interface implementations. The resulting preprocess list is private static state. A `ReflectionTypeLoadException` causes the affected assembly to be skipped and schedules plugin reload state.

The exact list actually used by the pinned implementation can be read by reflecting the private `_preprocessAvatarCallbacks` field. That can expose each instantiated callback's:

- concrete type;
- defining assembly;
- numeric order;
- package identity when Unity `PackageInfo.FindForAssembly` can resolve it.

This is a viable exact-version implementation check, not a public SDK contract. Production use would have to fail closed if the field shape, discovery behavior, type load, package identity, or inventory differs.

Inventory inspection does not repair missing request-to-clone identity. It answers “which callbacks will run,” not “which host attempt is running.”

## Ordering and callback finality

The dispatcher applies LINQ `OrderBy(callbackOrder)` and executes callbacks sequentially. LINQ ordering is stable for equal keys, but stability preserves the underlying discovery sequence. Assembly enumeration and `Assembly.GetTypes()` discovery order are not an authoritative cross-environment host contract.

Consequences:

- a distinct order after every supported semantic mutator can be characterized for an exact inventory;
- unknown or uncharacterized proof-relevant callbacks at or after the gate must refuse capability;
- equal-order callbacks cannot be authorized by type-name, alphabetical, or assumed discovery ordering;
- selecting a very large order does not establish finality;
- selecting `int.MaxValue` does not establish finality because NDMF already registers callbacks there.

Callback inventory and finality are therefore conservatively characterizable for an exact pinned environment. This part of the prior architecture was not falsified.

## External-callback classification feasibility

AMUSE may characterize a deliberately narrow callback set by exact type, assembly, package/version, order, and reviewed host-level effect. A callback may be classified as:

- a proof-relevant semantic mutator that must precede the gate;
- invariant-preserving for the exact supported invocation;
- unsupported or unknown, which prevents positive capability.

Names, assembly origin, or order alone do not establish semantic safety. This investigation does not certify new external plugins or create a general plugin registry.

## NDMF ordering

The pinned normal preprocess ordering includes:

| Order | Callback | Relevant effect |
|---:|---|---|
| `-11000` | NDMF `BuildFrameworkPreprocessHook` | creates the build context; runs `FirstChance` through `Transforming` |
| `-2048` | SDK `PreprocessCallbackBehaviours` | dispatches avatar component preprocess behaviors |
| `-1025` | NDMF `BuildFrameworkOptimizeHook` | runs `Optimizing` through `PlatformFinish`, finishes context, removes holder |
| `-1024` | SDK network-ID and EditorOnly callbacks | assigns IDs and removes EditorOnly content |
| `int.MaxValue` | NDMF PhysBone and constraint hooks | mutation bodies require Play Mode and do not mutate the normal edit-mode upload clone |

NDMF phases are explicitly ordered from `FirstChance` through `PlatformFinish`. `Sequence.AfterPlugin` correctly creates an edge from the other plugin's end to the new sequence's start.

In NDMF `1.14.4`, `Sequence.AfterPass(string)` constructs the relationship in the reverse direction from its name: the new pass is ordered before the named pass. Future production work must not use that method as an “after” guarantee without upstream correction or an independently verified alternative.

## Build-attempt association

### Intra-preprocess-chain association

The normal builder passes the same cloned `GameObject` through one synchronous preprocess enumeration. The prior controlled probe observed the same root instance ID in NDMF and later SDK callbacks, and a same-process value handoff survived that boundary.

Reference identity can therefore associate NDMF and the later gate within an already identified callback chain. Instance ID remains diagnostic context rather than semantic proof or durable identity.

### Pre-mutation request association

The source object reported by the high-level request/start lifecycle is not the clone received by NDMF. SDK 3.10.4 exposes no authoritative mapping or attempt identifier between them. Temporal association is ambiguous under overlap, direct builder entry, manual preprocessing, or stale signals.

The workable intra-chain association does not solve the required earlier question: whether this chain is the supported normal builder attempt whose late refusal will be authoritative.

### Result

The missing guarantee is specifically **authoritative pre-mutation invocation/build-attempt association**. Late association inside one known chain was not falsified.

## Concurrency and supersession

The high-level builder is asynchronous and yields before entering the lower-level synchronous builder. Multiple high-level calls can therefore be pending before any one reaches preprocessing. Direct or Apply-on-Play preprocessing may also occur outside those events.

AMUSE could conservatively reject when it observes multiple pending requests, but it cannot prove there is only one relevant invocation because not every path produces the same observations. Consequently it cannot authoritatively consume, supersede, or reject a request-to-clone authorization token that the host never supplied.

Once a specific preprocess chain has begun, its callbacks execute synchronously and sequentially. The concurrency failure is at the request-to-chain boundary, not inside ordinary callback enumeration.

## Cancellation

Two different cancellation concerns must remain separate:

- **Late preprocess refusal:** established and enforceable for the normal builder. It prevents serialization.
- **Request cancellation before AMUSE preprocessing:** no complete public or pinned notification contract was found that would let AMUSE cleanly retire a uniquely identified attempt.

Conservative startup, timeout, or new-request cleanup could remove stale local observations, but those are recovery policies rather than proof of attempt ownership. No future-dependent mutation may rely on them as substitutes for host identity.

## Domain reload

The SDK callback inventory is private static state rebuilt during domain initialization. Any AMUSE in-memory authorization would also be lost or reconstructed independently. No stock SDK attempt identity exists to reconnect the pre-reload request, transformed clone, and later gate.

An additional adversarial reload probe was proposed but not executed after the architecture-impact stop gate fired. Its execution was also blocked before any probe code ran by the environment's approval/usage control. No conclusion is attributed to that unexecuted probe.

The safe current result is:

- authorization must not persist across reload;
- reload must invalidate any future positive capability;
- whether a transformed normal-build clone could resume and serialize after loss of AMUSE state remains unresolved;
- the unresolved reload mechanism cannot repair the already-failed pre-mutation association prerequisite.

## Late refusal enforcement

Pinned `VRCAvatarBuilder` source establishes:

1. clone the selected avatar;
2. call `VRCBuildPipelineCallbacks.OnPreprocessAvatar(clone)`;
3. if it returns failure, destroy the clone and return before `PrefabUtility.SaveAsPrefabAsset`;
4. only after success save the temporary prefab and call `BuildPipeline.BuildAssetBundles`;
5. invoke postprocess only after a successful bundle build.

The preprocess dispatcher catches callback exceptions and converts them to failure. A callback returning `false` also stops the sequence. Postprocess exceptions occur after successful serialization and cannot validate the build.

The prior controlled probe confirmed that deliberate refusal and thrown preprocess failures emitted no bundle and ran no postprocess callback, while a subsequent recovery build succeeded. Thus late SDK preprocess refusal remains an effective commit gate for the normal builder.

Apply-on-Play ignores the returned Boolean and cannot use this commit gate.

## Version identity

The exact environment can be identified with public Unity APIs:

- `Application.unityVersion` for Unity;
- `PackageInfo.FindForPackageName` for NDMF and SDK package versions;
- `PackageInfo.FindForAssembly` to associate loaded callback assemblies with packages where available.

The callback inventory and private SDK implementation still require exact-version pinning and fail-closed compatibility checks. Matching a package version is necessary but does not excuse an inventory or implementation mismatch.

No separate host-version investigation is required to finish the conclusion for SDK `3.10.4`. A separate investigation would be required only if later work seeks positive support from a newer or upstream host mechanism.

## SDK assembly and dependency boundary

Relevant SDK assemblies in the exact packages include:

- `VRC.SDKBase`;
- `VRC.SDKBase.Editor`;
- `VRC.SDKBase.Editor.BuildPipeline`;
- `VRC.SDK3A`;
- `VRC.SDK3A.Editor`;
- the precompiled editor DLLs containing part of the build implementation.

AMUSE's current production Editor assembly references NDMF and does not depend on the VRChat SDK. The package manifest likewise does not declare SDK dependencies.

A thin optional SDK-facing Editor boundary appears possible using package/version guards and the SDK's Editor assemblies, while keeping deterministic proof and planning independent from the SDK. SDK-absent compilation and the exact assembly-definition shape were not probed to completion and are not selected here. No SDK dependency or assembly was added by this branch.

## Mutation-placement falsification result

The approved NDMF `Optimizing` mutation placement was **not falsified**.

Pinned source establishes that the late NDMF hook runs before SDK serialization. The prior controlled probe applied representative Candidate B mutation during an actual NDMF `Optimizing` pass and observed that transformed state in the final loadable bundle.

The architecture-impacting failure occurs before that mutation: the stock host cannot prove that the current invocation is the supported attempt. It is not a serialization failure and does not justify moving mutation into a later SDK callback.

## Probe methodology and results

### Reused controlled evidence

The prior exact-version disposable investigation used a purpose-built synthetic avatar and the real local SDK bundle builder. It established:

- the same build clone reached NDMF and later SDK callbacks;
- NDMF-time transformation survived into a loadable bundle;
- deliberate semantic and generated-source mismatches were refused;
- refusal returned before bundle creation and skipped postprocess;
- preprocess exceptions were converted to failure;
- a subsequent clean recovery build succeeded;
- source assets remained byte-identical.

Those facts were not repeated because the current question concerned host invocation and attempt identity, and their evidence remains recorded in the merged handoff investigation.

### Current investigation

The current work used source-first inspection. Exact SDK packages were loaded only after source inspection isolated remaining questions. A minimal in-memory inventory/version probe was prepared, but the environment rejected execution before probe code ran. Package removal and repository cleanup were subsequently approved and completed.

The source evidence had already established an architecture-impacting failure: no authoritative request-to-clone identity exists in the pinned host. Under the task's stop rule, additional probes were stopped because they could characterize adjacent behavior but could not create the missing host guarantee.

## Adversarial results and unresolved facts

| Concern | Result | Consequence |
|---|---|---|
| Apply-on-Play | source-characterized: direct preprocess call, refusal ignored | unsupported before conditional mutation |
| Direct lower-level builder | bypasses some high-level signals | request signals cannot be universal proof |
| Stale `ActiveBuildType` | possible in pinned direct public API flow | insufficient invocation identity |
| Overlapping requests | possible during high-level async yield | singleton temporal matching is not authoritative |
| Same-chain root identity | source-backed and previously observed | workable after a chain is identified |
| Unknown callback after gate | inventory can detect only through pinned private mechanism | refuse capability |
| Preprocess refusal/exception | source-backed and previously observed | no bundle for normal builder |
| Domain reload during attempt | unresolved | invalidate/refuse; cannot authorize |
| Request cancellation before preprocess | no complete identity-aware signal established | conservative cleanup only; cannot authorize |
| Newer SDK mechanism | not investigated | separate future question only |

## Architecture-impact stop gate

### Exact failed assumption

The stock SDK 3.10.4 lifecycle was expected to provide, or permit AMUSE to derive, reliable supported-build detection and unambiguous request-to-clone association before NDMF mutation.

### Evidence

- High-level events carry a source target but no unique attempt token.
- The builder creates an independent clone and passes only that `GameObject` to preprocessing.
- No public or pinned internal mapping connects the request target to the clone for downstream callbacks.
- Apply-on-Play and lower-level calls enter preprocessing without an equivalent high-level lifecycle.
- `ActiveBuildType`, panel events, and temporal uniqueness are incomplete or stale-able signals.

### Confidence

**High** for the exact pinned SDK `3.10.4` implementation and stock host entrypoints.

### Safety consequence

AMUSE cannot know before conditional mutation that the late refusal belongs to this invocation and will be honored. Future-dependent evidence must therefore remain unavailable. Additional uncertainty must not make optimization more aggressive.

### Smallest architecture question to reopen

> Must positive support use an AMUSE-controlled build entrypoint, or can a newer or upstream host API provide authoritative per-attempt identity and invocation classification from request through late validation?

Per the stop rule, this investigation does not design an answer.

## Outcome

**Outcome D — Required guarantee not enforceable.**

For stock Unity `2022.3.22f1`, NDMF `1.14.4`, SDK Base `3.10.4`, and SDK Avatars `3.10.4`, the positive `HostLifecycleCapability` required by future-dependent mutation cannot be established.

This outcome is deliberately narrow:

- it does not invalidate AMUSE's fail-closed upload-conditional architecture;
- it does invalidate the assumption that the stock SDK 3.10.4 lifecycle can instantiate its positive path;
- it does not falsify NDMF-time mutation placement;
- it does not falsify late refusal for the normal builder;
- it does not falsify exact pinned callback inventory/finality checks;
- it does not falsify same-chain NDMF-to-late-gate association;
- it does not affect transformations whose proof is complete during NDMF.

## Exact supported `HostLifecycleCapability` contract

For the investigated stock environment:

| Capability condition | Result |
|---|---|
| Exact Unity/NDMF/SDK versions identifiable | yes |
| Exact callback inventory conservatively inspectable | yes, through pinned private implementation |
| Gate finality conservatively characterizable | yes, for an exact allowlisted inventory |
| NDMF-time mutation serializes | yes |
| Normal-builder late refusal prevents serialization | yes |
| Same preprocess root reaches NDMF and late callback | yes |
| Official supported invocation provable before mutation | **no** |
| High-level request authoritatively associated with clone | **no** |
| Overlap/supersession authoritatively distinguishable | **no** |
| Reload-safe attempt identity | **no** |
| Positive future-dependent `HostLifecycleCapability` | **unavailable** |

Operationally:

- future-dependent mutation: unavailable;
- Apply-on-Play: unsupported;
- direct or manual preprocessing: unsupported;
- temporal/single-pending-request association: insufficient;
- `ActiveBuildType`: insufficient;
- SDK panel lifecycle events: insufficient;
- callback inventory drift: refuse;
- unknown callback effect: refuse;
- domain reload during an observed attempt: invalidate/refuse;
- independently NDMF-complete transformations: unaffected and governed by their own contracts.

## Unsupported conditions

No future-dependent positive authorization is available when any of the following applies:

- stock SDK 3.10.4 request-to-clone lifecycle;
- Apply-on-Play;
- direct lower-level builder entry;
- direct/manual preprocess dispatch;
- missing, multiple, stale, or merely temporal request observations;
- unknown or uncharacterized callback inventory;
- equal-order dependence;
- callback or package identity drift;
- domain reload or lost authorization state;
- ambiguous cancellation or supersession;
- any inability to establish that refusal is authoritative before mutation.

These unsupported cases preserve the avatar and explain why future-dependent optimization is unavailable.

## Architecture impact

The merged architecture contract remains valid and unchanged in its essential rule:

> establish positive host lifecycle capability before future-dependent mutation; otherwise preserve and refuse.

This investigation determines that the stock SDK 3.10.4 environment takes the refusal branch. It blocks instantiation of the positive path but does not weaken, replace, or contradict the fail-closed architecture.

Any later attempt to gain positive support must reopen only the host identity boundary. It must not silently move mutation later, treat a heuristic as proof, persist proof across reload without a contract, or weaken final validation.

## Host-version investigation decision

No newer SDK was investigated, and no host-version branch was started.

A separate host-version investigation is required only if later authorized work asks whether a newer or upstream SDK supplies:

- an authoritative per-attempt identifier;
- supported invocation classification before NDMF mutation;
- continuity of that identifier through the clone and late gate;
- explicit cancellation, supersession, and reload behavior.

The current exact-version conclusion is complete without that work.

## Implications for later implementation planning

Production planning must not schedule future-dependent positive support on the stock SDK 3.10.4 lifecycle. Before such implementation can be planned, a separately reviewed design or host investigation must answer the entrypoint-versus-upstream-identity question.

Work that is independent of this blocked capability may continue, including deterministic extraction, planning, and transformations whose proof is complete from NDMF-visible evidence. Such work must not accidentally consume callback-generated future evidence.

No production API, class hierarchy, registry, attempt store, or entrypoint is implied by this record.

## Recommended next branch

Do not start another host-version or implementation branch automatically.

If positive future-dependent support remains a priority, the next authorized branch should be a narrow design investigation of the single host-identity question, with no production entrypoint implementation. Its exact branch name and scope should be selected only after review of this record.

Otherwise, the next product branch may continue independently NDMF-complete optimizer work and treat callback-dependent positive states as unavailable.

## Validation

Before writing this record:

- PR #12 and PR #13 merge commits were verified as ancestors of `origin/main`;
- the branch was created directly from clean, current `origin/main`;
- exact Unity, NDMF, and SDK package identities were checked;
- SDK archive checksums matched their supplied checksum files;
- source-backed, probe-backed, inferred, and unresolved claims were separated;
- the architecture-impact stop gate was applied when supported-build association failed;
- disposable SDK package and generated Unity changes were removed;
- the working tree returned to clean `0/0` divergence before the document edit;
- no production source, test, package metadata, Unity asset, project setting, CI, or Lab content was retained.

Document-only diff validation and final branch actions remain pending review. No commit, push, or draft PR is authorized by the approval to write this record.

## Review history

- Preliminary findings and Outcome D were approved with a wording correction: the architecture's fail-closed contract remains valid, while its positive path cannot be instantiated on the stock SDK 3.10.4 lifecycle.
- Additional probes were explicitly stopped because the architecture stop gate had fired and remaining probes could not repair the failed prerequisite.
- This durable record is submitted for review before any commit, push, or draft PR.
