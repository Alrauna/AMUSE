# AMUSE lilToon build-callback handoff investigation

## Status

**Investigation complete and reviewed; Outcome B is approved for the normal VRChat avatar build/upload path only. Production design and implementation remain separate, blocked work.**

This document is an investigation result, not an implementation plan. No production lilToon support, AMUSE NDMF plugin, host-extraction migration, integration validator, digest pin, callback framework, snapshot manager, or dependency engine was implemented.

- Branch: `investigate/liltoon-build-callback-handoff`
- Base: `origin/main` at `848715c` (`Merge pull request #10 ... analysis-snapshot-ordering`)
- Previous result confirmed on `main`: `ab5e118` is an ancestor of `848715c`
- Probe project: disposable, synthetic, outside the repository
- Census Lab/private avatars: not used

## Decision summary

Select **Outcome B — NDMF conditional mutation plus a late validation gate**, with a deliberately narrow support contract:

1. Before any future-dependent conditional transformation, AMUSE establishes that the current invocation uses a supported lifecycle with an enforceable late refusal gate.
2. AMUSE's transformation then occurs in one explicitly ordered late NDMF `Optimizing` pass.
3. That pass retains only AMUSE-owned immutable values describing the proof, plan, expected post-transformation semantics, and expected future lilToon attestation profile.
4. lilToon generates the upload-time shader state in its SDK callback at order `100` against the already-transformed build clone.
5. An AMUSE SDK preprocess callback at a contractually chosen order after every supported semantic mutator re-extracts all proof-relevant final facts and verifies the actual callback environment. This is not a small shader-hash gate.
6. The gate accepts only if the fresh final semantics equal the expected transformed semantics and the final lilToon attestation matches; otherwise it returns `false` and the SDK aborts before prefab or bundle creation.

The result is **upload-only** for proofs that depend on future source state unavailable during NDMF. Apply-on-Play is not equivalent and remains unsupported for those proofs: pinned NDMF source ignores the preprocess callback's Boolean result, and lilToon may skip generation on that call path. AMUSE must classify the build path before mutating. No enforceable late commit gate means no conditional transformation whose correctness depends on that gate. This does not make every AMUSE optimization upload-only: a transformation completely authorized by NDMF-visible evidence may follow its independently supported lifecycle contract.

Candidate A is rejected. Late mutation can change already referenced objects and can use newly created AssetDatabase-backed assets, but new in-memory meshes/materials assigned after NDMF `Finish()` were lost from the serialized bundle. More importantly, representative late material changes alter inputs lilToon uses for generation; lilToon's in-progress build guard prevented a second generation. That is an unclosed semantic cycle.

The lifecycle blocker is therefore resolved at the architecture-investigation level for a bounded normal upload support contract, not at the production-support level. Every positive lilToon proof that depends on callback-`100`-generated source—including standalone lilToon and characterized integration states—requires this late authorization. Positive LTCGI, AudioLink-package, and external VRC Light Volumes states remain `Unknown` until separately reviewed production work implements and validates the contract.

## Selected lifecycle contract

This is a conceptual contract, not a production API or class design.

### Phase 1 — authoritative NDMF conditional transformation

1. Classify the current build path and prove that it supplies an enforceable late gate.
2. Enter one explicitly ordered late NDMF `Optimizing` pass.
3. Eagerly extract immutable proof-relevant values.
4. Interpret semantics only from those captured values.
5. Compute the proof and transformation plan.
6. Derive the expected post-transformation semantic state.
7. Apply the transformation while NDMF still owns generated assets.
8. Retain bounded AMUSE-owned handoff values describing the conditional proof, expected transformed state, and future lilToon attestation requirements.

### Phase 2 — lilToon final generation

9. NDMF finishes.
10. lilToon callback `100` observes the already-transformed clone.
11. lilToon generates the authoritative build-time shader state.

### Phase 3 — AMUSE commit gate

12. AMUSE runs after every supported proof-relevant semantic mutator.
13. Re-extract the complete proof-relevant state from the current build clone using the same semantic extraction definitions as the NDMF-side proof.
14. Capture final lilToon source, package, closure, activation, and macro/include evidence.
15. Verify that the actual registered callback environment is within the supported ordering contract.
16. Compare the fresh final snapshot with the expected transformed state.
17. Validate the final lilToon attestation condition.
18. On a complete match, remove handoff state, return success, and allow prefab/bundle creation.
19. On any mismatch, missing evidence, exception, or unsupported callback environment, remove AMUSE handoff state through a guaranteed cleanup path, return failure, and abort before serialization.

The three states are distinct:

- `OriginalSnapshot` proves why the plan is safe.
- `ExpectedTransformedState` defines the proof-relevant semantics that should exist after AMUSE's NDMF mutation.
- `FreshFinalSnapshot` is newly extracted after lilToon and all supported pre-gate mutators.

Authorization requires semantic equivalence between `FreshFinalSnapshot` and `ExpectedTransformedState`, plus satisfaction of the expected future lilToon attestation. The gate must never compare the final state blindly with the original pre-transformation snapshot.

## Evidence vocabulary

- **Documented/API guarantee:** stated public contract.
- **Pinned source guarantee:** enforced by the exact investigated source or assembly.
- **Empirical observation:** reproduced by the exact-version disposable probe.
- **Inference:** a conclusion derived from those facts.

Correctness does not depend on callback discovery order, alphabetical type order, or equal-order behavior.

## Exact investigated versions and revisions

| Component | Version | Exact revision/evidence |
|---|---:|---|
| Unity | `2022.3.22f1` | editor revision `887be4894c44` |
| NDMF | `1.14.4` | upstream `7cf8a13444ac19e46ac2b4146bad209de15dc42d`; embedded package source checked against this revision |
| lilToon | `2.3.4` | `252fd8cfc46106d4967e95b3f2c788418502f227` |
| VRChat SDK Base | `3.10.4` | official cached package; `VRCSDKBase-Editor` informational revision `1.0.0+acba6151ac582ed3bc7563404e6b6e9dbcf32a3d` |
| VRChat SDK Avatars | `3.10.4` | official cached package paired with Base 3.10.4 |
| LTCGI | `1.7.3` | matrix revision `b2014d6c6e76c551c30084973e54687941265d68`; source implication only, not installed in this probe |
| AudioLink | `3.1.2` | matrix revision `5bd23af5b2aaefff1ac3f48379332f6f78f17f97`; source implication only |
| VRC Light Volumes | `2.1.3` | matrix revision `7ead7482f40b9612e6e4faafae835ffd9a73e149`; source implication only |
| VRCFury | known comparison only | callback `-10000` at prior pinned revision `b5e9f9630e40e93c47fe06f5aa71897dba92cfca`; not installed in the controlled probe |

The probe copied the exact official package contents into a disposable project and copied the current AMUSE package only to invoke the production canonicalizer by reflection. Nothing from the probe was retained as repository tooling.

## Exact callback and serialization timeline

| Stage/order | Actor | Evidence | Relevant behavior |
|---|---|---|---|
| build request | lilToon | pinned source | `OnBuildRequested(Avatar)` sets `forceOptimize`; the low-level probe set the same flag because it directly invoked the builder export method |
| clone | VRChat SDK | pinned assembly | the selected avatar is cloned for the build |
| `-11000` | NDMF early hook | pinned source + observed | creates `BuildContext`, runs `FirstChance` through `Transforming`, then serializes |
| `-10000` if installed | VRCFury | prior pinned source | known position between NDMF hooks; absent from this probe |
| `-2048` | SDK `PreprocessCallbackBehaviours` | observed | SDK built-in callback |
| `-1025` | NDMF late hook | pinned source + observed | runs `Optimizing` through `PlatformFinish`, calls `BuildContext.Finish()`, destroys the context holder, returns `context.Successful` |
| `-1024` | SDK network IDs and editor-only removal | observed | `AssignAvatarNetworkIDs`; `RemoveAvatarEditorOnly` |
| `100` | lilToon | pinned source + observed | reads renderer materials and animation clips, generates/sets shader state, calls `SetupMultiMaterial` |
| `>100`, probe `200` | candidate AMUSE gate | observed | captures final source and full proof-input fingerprint, accepts or returns `false` |
| `int.MaxValue` | NDMF PhysBone/constraint hooks | pinned source + observed | present after the gate; on normal upload their mutation bodies require `Application.isPlaying`, so they do not mutate upload semantics |
| after preprocess success | VRChat SDK | pinned assembly | saves a temporary prefab, destroys the clone, builds the asset bundle |
| preprocess failure | VRChat SDK | pinned assembly + observed | destroys the clone and returns before temporary-prefab save and bundle construction |
| postprocess `0` | NDMF cleanup | pinned source | cleans NDMF temporary assets; not called when preprocess fails |
| postprocess `100` | lilToon | pinned source | restores shader settings; not called when preprocess fails |
| after successful build | SDK uploader | pinned assembly | upload is entered only after `Build` succeeds |

`VRCBuildPipelineCallbacks.OnPreprocessAvatar` orders callbacks by numeric `callbackOrder`. Equal-order ordering is not an AMUSE correctness primitive. The controlled callback inventory exactly contained `-11000`, `-2048`, `-1025`, two `-1024` callbacks, probe orders `50/99/101/150/200`, lilToon `100`, and the two NDMF `int.MaxValue` hooks.

No supported semantic mutator was observed after order `200` in this environment. This does not authorize a universal order or make documentation-only compatibility policy sufficient. Before treating the gate as authoritative, production must reliably inspect or otherwise verify the actual registered SDK preprocess callback inventory for the current build environment. A callback after the gate is acceptable only when it is characterized as invariant-preserving; a supported mutator must be ordered before AMUSE. Any unknown, unordered, or uncharacterized later callback makes future-dependent positive optimization unavailable. The next production design must find the smallest enforceable mechanism for this check without creating a generic plugin registry. Choosing `int.MaxValue`, relying on equal-order discovery, or relying on alphabetical order does not solve it.

## Probe architecture

The disposable project built a minimal avatar containing a quad mesh, two lilToon materials, multiple renderers dedicated to individual mutations, and one material-swap animation clip.

- An actual NDMF `Optimizing` probe pass cloned/saved mesh and material assets with `BuildContext.AssetSaver`, applied the representative Candidate B mesh transformation, and stored value-only evidence.
- SDK callbacks at `99` and `101` captured immediately before and after lilToon.
- Order `150` applied one adversarial witness at a time.
- Order `200` performed the candidate validation.
- The real SDK builder emitted a `.vrca`, which the probe loaded and inspected.
- Raw normalized SHA-256, current AMUSE canonical digests, include-closure digests, selected Layer-2 activation counts, renderer paths/slots, semantic material fingerprints, texture/importer evidence, mesh topology, animation references, and color space were captured.
- A scratch static dictionary was keyed by the cloned avatar root instance ID, but contained no Unity object reference as proof. This is probe mechanics, not a production design.

The visibility run inserted unmistakable sentinel comments at order `50`. lilToon removed them at `100` and produced new raw and canonical digests, proving that order `101` observed the source generated for that build rather than a pre-existing file.

## Late visibility and lilToon state delta

### Generated source

Immediately before lilToon, with sentinels:

- `lts.shader` raw: `ba28090776ba12c0dd546390fb15663bebb58f27b287f95ed2089f6da51bc112`
- `ltspass_opaque.shader` raw: `3534a0b37d32c0f24f2fa42778cef5a5217f8d47d09551bd48289ab3bbbf8dfc`

Immediately after lilToon:

- `lts.shader` raw: `4f0cca3cd45fe15933e3d6550082b9bd856db27a611cf0b723de11427f6010a3`
- `ltspass_opaque.shader` raw: `ec52d2609bfd4e2dfa934283cc75b4f662acd94b0ddee8e2f43d1c021d077ddb`
- `lts.shader` canonical: `3f401bf4ab0e7069c7203b52dd60ef95d7d637244921e778f9414219443f2504`
- opaque-pass canonical: `dd9bd7c9832620d8fbb7af1c7a06faa7a5dc82c78ebd7f675becf8d5b8304f77`
- include closure: `fd2d2376fec8c474aec624d5dd6f37f7342d6d05da480cfb6996aca2a9f5f469`

These are probe evidence, not production pins.

### Avatar state

For the simple material state, renderer paths, renderer-to-mesh relationships, material-slot sequence, mesh contents, material shaders, relevant values, texture assignments, and animation material references were unchanged across callback `100`. The generated shader files and include closure changed. This is empirical evidence for the simple case, not a general promise.

Pinned source also calls `SetupMultiMaterial(materials, clips)`. Multi-material behavior was not characterized exhaustively. Therefore Candidate B cannot classify generated source as the only possible delta. The late gate must re-extract the complete AMUSE proof-input snapshot and compare it with the expected post-transformation semantics; it cannot rely on a source digest alone or compare blindly with the original snapshot.

NDMF `Finish()` also deduplicated a semantically identical cloned material back to a project asset and rewrote animation/object associations. Unity instance identity therefore did not survive as a usable proof invariant even before lilToon. Renderer path/slot plus immutable semantic fingerprints survived; object IDs remain diagnostics/build association only.

## Candidate A: late mutation and asset serialization

The callback itself returned successfully and the SDK produced a bundle, but serialization results differed by ownership:

| Late mutation after lilToon | Callback succeeds | Present in final bundle | Valid serialized asset | Result |
|---|---:|---:|---:|---|
| modify already referenced NDMF mesh | yes | yes | yes | vertex marker and mesh name survived |
| assign cloned in-memory mesh | yes | no | no | final `MeshFilter.sharedMesh` was null |
| assign newly created in-memory mesh | yes | no | no | final `MeshFilter.sharedMesh` was null |
| assign cloned in-memory material | yes | no | no | final material slot was null |
| swap existing material slots | yes | yes | yes | reversed slots survived |
| modify relevant property on referenced material | yes | yes | yes | `_Cutoff` marker survived |
| create in-memory submesh/material split | yes | no | no | replacement mesh and second material were null |
| create Mesh and Material in one AssetDatabase asset, then assign | yes | yes | yes | both marker assets survived and loaded |

The source assets under `Assets/ProbeData` remained byte-identical. The late AssetDatabase asset was deleted in postprocess. The run produced no probe-specific missing-reference exception, but null serialized references are themselves decisive failure evidence.

NDMF-owned generated assets serialize because NDMF saves them before `Finish()`. Already referenced object mutation is included when the later SDK prefab is saved. New in-memory objects assigned after `Finish()` are not automatically collected. AssetDatabase-backed late assets can work empirically, but would require a new ownership/cleanup system and still would not solve Candidate A's semantic cycle.

### Regeneration feedback

When `_UseEmission` and `_UseAudioLink` were present before lilToon, the generated opaque-pass raw digest changed from the baseline `ec52...` to `33347584b1968243c30b3376ad4263c4672f0f760c90e82846b6e60ae0c23bd9`. The current canonicalizer intentionally normalized the compared profile to the same canonical digest, which also demonstrates why Layer-2 conditions cannot be discarded.

When the same inputs were changed after callback `100`, an attempted second `SetShaderSettingBeforeBuild` left source unchanged. Pinned lilToon source explains this: `ShouldOptimization()` refuses while `modifiedShaders` is non-empty for the in-progress build. Thus the representative late mutation changed lilToon generation inputs, but the simple supported generation call could not close the cycle.

**Candidate A verdict: reject.** It fails both reliable asset serialization for AMUSE-style new/replacement objects and the no-regeneration-cycle requirement. AssetDatabase-backed late assets do not rescue the architecture. Future production design must not carry Candidate A forward unless materially changed upstream lifecycle behavior justifies a new investigation.

## Candidate B failure semantics

Pinned SDK assembly behavior is stronger than an observed UI convention:

1. preprocess callbacks run in numeric order;
2. the first `false` logs an abort and returns `false`;
3. the builder destroys the cloned avatar and exits before prefab save or asset-bundle construction;
4. upload is entered only after build success.

The intentional-failure probe observed:

- builder result `false`;
- no emitted bundle at the reported path;
- no postprocess callback;
- original project assets byte-identical;
- a subsequent clean Candidate B build succeeded, emitted and loaded a bundle, and ran postprocess.

An invalid transformed upload artifact did not escape. However, all postprocessors are skipped on refusal, including NDMF cleanup and lilToon's shader restore. AMUSE's gate must remove its own handoff state in a `finally`-equivalent path. Other packages' temporary state may persist until their existing recovery/startup or a later successful build; the probe's later successful build recovered. This residue does not make an invalid bundle usable, but it is a production cleanup requirement and diagnostic concern.

Exceptions from preprocess callbacks are also converted to failure by pinned SDK source. Postprocess exceptions are caught after a successful bundle and are not a validation mechanism.

## Cross-boundary dependency contract

The contract is bounded by AMUSE's actual proof inputs, but it requires complete late re-extraction of those inputs. It is not a generic dependency graph, not a separately hand-written late fingerprint, and not merely a small hash gate. The NDMF side records expected post-transformation meaning; the late side uses the same extraction definitions to capture what actually exists.

| Dependency | Class | Required treatment |
|---|---|---|
| cloned build/root association | B | use only to find the current handoff entry; verify expected build token/root ID; never treat the Unity reference as proof |
| renderer diagnostic path/type and material-slot sequence | B | re-extract and compare with expected transformed renderer semantics |
| mesh semantic contents/topology/submesh layout used by the plan | B | re-extract and compare with expected transformed mesh semantics |
| material Unity instance identity | D as proof | NDMF may deduplicate it; replace with renderer path/slot plus semantic content. Keep IDs only for diagnostics |
| material shader identity and render mode | B | re-extract and compare |
| every material value consumed by extraction/proof | B | re-extract and compare with expected transformed material semantics; do not choose a hand-written subset |
| texture assignment/content/importer evidence consumed by proof | B | re-extract and compare with expected transformed texture evidence |
| animation/controller/clip bindings and reachable material swaps | B | re-extract and compare with expected transformed reachability semantics |
| lilToon generated `lts.shader` and opaque pass | B | late authoritative capture; validate the characterized canonical pair and required raw/Layer-2 facts |
| lilToon include/external source closure | B | late closure digest and required inclusion/macro evidence |
| package identities/versions | B | immutable value capture plus late equality check |
| integration activation tuple | B | late equality with the characterized state; unexpected activation refuses |
| color space and proof-relevant global settings | B | cheap late equality check |
| `SetupMultiMaterial` or another supported callback changing proof inputs | C | complete late extraction and comparison; if semantic equivalence cannot be established, refuse |
| actual SDK callback inventory | B | verify for the current environment before authorization; all supported mutators must precede the gate |
| unknown callback after the gate | D | refuse future-dependent positive optimization until it is ordered or characterized as invariant-preserving |

Class A is intentionally small: exact pinned source establishes that ordinary callback execution is sequential and that the observed post-gate NDMF hooks do not mutate normal upload state. Mutable avatar, asset, file, package, and global facts are revalidated instead of being assumed stable.

## Bounded handoff identity and lifetime

The probe demonstrated that the same cloned avatar root instance ID was visible in NDMF and later SDK callbacks, and that a static value dictionary survived the boundary. Conceptually, a future conditional authorization must carry enough immutable meaning to validate:

- build association token/root diagnostic ID;
- proof/plan diagnostic ID;
- expected transformed renderer paths/types/slot semantics;
- expected transformed mesh topology/content needed by the proof;
- expected transformed material shader and relevant values;
- expected transformed texture/importer evidence;
- expected transformed animation and material-reachability semantics;
- expected proof-relevant global values;
- expected final lilToon generated-source conditions, canonical/raw Layer-2 conditions, activation tuple, package identities, external executable closure, and macro/include conditions.

These are conceptual fields, not a proposal for one giant class. The handoff must not contain Unity objects as proof evidence. The late callback may use the current root only to associate the build, locate fresh extraction targets, and produce diagnostics. Instance identity does not establish semantic stability.

AMUSE-owned entries must remove themselves on acceptance, refusal, exception, superseding build request, detectable cancellation, and appropriate stale/startup/domain-reload recovery. Cleanup cannot depend solely on NDMF postprocess, lilToon postprocess, or a later successful build because preprocess refusal skips postprocess. A static dictionary was sufficient only to prove same-process viability; storage, concurrency, recovery, and diagnostics remain production design work.

## Adversarial witnesses

| Witness after lilToon, before gate | Result | Contract field that refused it |
|---|---|---|
| material-slot swap | refused; no bundle | ordered slot semantic fingerprint |
| semantically identical material replacement | accepted | deliberately equivalent; instance identity is not proof-relevant |
| shader change on a replacement material | refused; no bundle | material/shader semantic fingerprint and attestation availability |
| relevant `_Cutoff` change | refused; no bundle | material semantic fingerprint |
| texture assignment change | refused; no bundle | material/texture fingerprint |
| semantically identical mesh replacement | accepted | deliberately equivalent; object identity is not proof-relevant |
| mesh topology change | refused; no bundle | mesh topology fingerprint |
| animation material-swap/controller change | refused; no bundle | animation semantic fingerprint |
| generated-source trailing change | refused; no bundle | raw generated-source digest even where canonical form was unchanged |
| unexpected AudioLink-package activation macro | refused; no bundle | pass digest and activation tuple |
| external include-closure change | refused; no bundle | closure digest |
| unconditional gate refusal | refused; no bundle | explicit failure path |

The two accepted identity-only replacements are not escaping proof-relevant mutations: their extracted renderer/slot semantics were byte-for-byte equivalent within the characterized domains. Requiring instance identity would reject NDMF's own deduplication and is neither stable nor necessary for authorization. This does not permit arbitrary replacement or general-purpose semantic equivalence; AMUSE may compare only the exact proof-relevant domains its shared extraction model completely captures. Anything outside that model remains unsupported or unequal.

The successful path applied the representative mesh transformation during NDMF, captured the final generated source after lilToon, matched the complete semantic and attestation contract, built and loaded the bundle, and required no second mutation. The deliberately mismatched paths aborted.

## Upload versus Apply-on-Play

| Property | Normal SDK avatar build/upload | NDMF Apply-on-Play |
|---|---|---|
| SDK preprocess chain runs | yes | pinned NDMF source calls it |
| Boolean failure honored | yes; builder aborts before bundle | **no**; `ApplyOnPlay` ignores the returned Boolean |
| lilToon generation | yes after build request forces optimization | conditional; lilToon detects the `ApplyOnPlay` caller and returns early unless `isOptimizeInNDMF` is enabled |
| same authoritative source guaranteed | yes for the characterized upload path | no |
| AMUSE late callback can run | yes | it may be invoked, but cannot be a safety gate because refusal is ignored |
| Candidate A | rejected | rejected |
| Candidate B | supported architecture | unsupported |

Apply-on-Play was not driven empirically in Play Mode. The two decisive differences are pinned-source behavior, not an inference from upload results. Production's first lifecycle contract should explicitly say upload-only.

Build-path classification is a precondition to mutation, not a late diagnostic. Under the investigated versions, an Apply-on-Play invocation must make future-dependent positive lilToon proof unavailable during NDMF, producing `Unknown`/no such optimization before any conditional transformation occurs. Otherwise AMUSE could mutate, later refuse, and have the host ignore that refusal. This restriction applies only when proof depends on future callback-`100` source; an independently complete NDMF-time proof may have another supported lifecycle contract. Apply-on-Play is not claimed permanently impossible and may be revisited if upstream lifecycle behavior changes.

## Downstream callbacks and authority

The controlled environment had no proof-relevant semantic mutator after the proposed order `200`. The only later observed preprocess callbacks were NDMF's two `int.MaxValue` runtime reinitialization hooks, whose mutation bodies require Play Mode and therefore do not alter the normal upload clone.

This result is environment-scoped. A production gate must run after every supported material, mesh, shader, texture, and animation mutator and must enforce that claim against the actual registered callback inventory. If a newly installed callback has a later order and is not characterized as invariant-preserving, future-dependent positive support must refuse before conditional mutation. Choosing order `200`, choosing a very large order, or choosing `int.MaxValue` does not solve equal-order ambiguity; discovery/alphabetical order is never a guarantee.

## Candidate verdicts

### Candidate A — rejected

- late source visibility: proven;
- late mutation callback execution: proven;
- new/replacement in-memory asset serialization: disproven;
- AssetDatabase workaround: empirically possible but would add ownership/cleanup work;
- no regeneration cycle: disproven for a representative material-input change;
- overall: not sound and closed under the investigated lifecycle.

### Candidate B — accepted for normal upload only

- NDMF-time mutation remained in the final bundle: proven;
- lilToon saw the already transformed avatar before generating: ordered by pinned callback source and observed timeline;
- final source visible late: proven with sentinels and digest changes;
- expected transformed state: required; the final state is compared with post-mutation expectations, not the original snapshot;
- bounded contract: yes, but it is the complete actual AMUSE proof-input snapshot plus final attestation, not a small hash or separate late fingerprint;
- handoff association: same-process value handoff proven; production representation/recovery still to design;
- adversarial semantic mismatches: detected;
- late failure prevents bundle/upload: pinned source guarantee and empirical result;
- recovery build: succeeded;
- downstream ordering: proven only for the controlled supported set; actual callback-inventory enforcement is a production prerequisite;
- Apply-on-Play: unsupported for future-dependent conditional proof and must be refused before mutation.

### Final outcome — B

The normal upload lifecycle has a sound bridge if production support adopts the full fail-closed contract above. Any missing dependency, uncharacterized later mutator, unsupported build path, missing handoff, or attestation mismatch makes future-dependent positive proof unavailable or refuses the transformed build. Lifecycle suitability must be known before mutation; final semantic and attestation suitability is committed by the late gate.

## Relationship to the existing snapshot design

The previous architecture remains intact:

`late NDMF Optimizing -> eager extraction -> immutable semantics -> proof -> plan -> immediate mutation`

Candidate B adds a narrow authorization condition around the already-applied transformation:

`immutable conditional proof + transformed-state contract -> lilToon 100 -> full fresh late extraction + final attestation -> accept or abort`

More explicitly:

`FreshFinalProofInputs == ExpectedTransformedProofInputs`

and:

`FinalLilToonAttestation satisfies ExpectedFutureAttestationCondition`

The late extraction must reuse the same semantic definitions as the NDMF-side proof. Separate hand-written late fingerprints could silently omit a proof dependency and are not sufficient. This requirement does not itself design the reuse API.

The model does not authorize deferred NDMF mutation, live Unity objects in proof, a generic snapshot manager, or a second optimizer. Host extraction may still migrate independently into immutable inputs. For every positive lilToon state whose proof depends on callback-`100`-generated source, the NDMF proof remains conditional until the late gate accepts.

## Implications for official lilToon integrations

No standalone or integrated lilToon validator, support profile, or production pin was added.

Outcome B is a lifecycle rule for any positive lilToon proof that depends on callback-`100`-generated source, not only for external integrations. A future gate may authorize characterized standalone lilToon, LTCGI 1.7.3, AudioLink 3.1.2, VRC Light Volumes 2.1.3, and their already-characterized combinations through the same model. For each of the eight lilToon 2.3.4 states, the late conditions include:

- exact expected generated canonical digest pair;
- every non-canonical/raw Layer-2 activation fact that distinguishes the state;
- exact activation tuple, including inactive controls;
- package identity/version for lilToon and the applicable LTCGI 1.7.3, AudioLink 3.1.2, or VRC Light Volumes 2.1.3 package;
- external source/include closure and required macro/inclusion evidence;
- the complete freshly re-extracted AMUSE renderer/material/mesh/texture/animation proof inputs compared with expected transformed semantics.

Standalone positive build-generated attestation and the LTCGI, AudioLink-package, and external VRC Light Volumes positive states remain unimplemented. Integrated states remain disabled until their already-characterized profiles are wired through the reviewed upload-only gate and tested with their exact official packages. Missing, ambiguous, mixed, or unexpected activation stays `Unknown`. Apply-on-Play remains `Unknown` for future-dependent positive build-generated states.

## Remaining unknowns and limitations

- No official integration package was installed in the executable probe; their lifecycle implications are source/matrix-backed, not a new runtime result.
- VRCFury was not installed; its known `-10000` position is prior pinned evidence.
- `SetupMultiMaterial` was not exhaustively exercised. This is why the selected contract requires full late semantic re-extraction.
- No real network upload was performed. The exact SDK builder produced/loaded local `.vrca` bundles, and pinned builder source establishes upload only follows successful build.
- Apply-on-Play was source-characterized, not run in Play Mode.
- The scratch static dictionary does not solve domain reload, cancellation before the gate, concurrent/multi-avatar build association, or production diagnostics.
- Headless Apple Silicon logged unrelated Oculus audio-plugin architecture warnings and lilToon Metal shader compiler warnings. Successful control and Candidate B builds still produced loadable bundles; refusal cases were decided before serialization.
- The probe deliberately used reflection and compact fingerprints suitable for falsification, not a production API.

## Recommended next production milestone

The next branch should be the dedicated production **design** branch `design/upload-conditional-authorization`. The name reflects an AMUSE host-authorization mechanism whose first concrete consumer is lilToon. It must produce a reviewed design before any implementation plan or implementation. It should specify:

1. supported build-path detection before conditional mutation;
2. explicit NDMF ordering and the required host-extraction migration;
3. immutable proof inputs and expected transformed state;
4. bounded build handoff values, identity, concurrency, and cleanup;
5. complete late fresh re-extraction using the shared semantic model;
6. enforceable inspection/verification of the actual SDK callback inventory;
7. final standalone and integrated lilToon attestation conditions from the completed matrix;
8. fail-closed SDK reporting, diagnostics, recovery, and test strategy;
9. explicit Apply-on-Play refusal for future-dependent proof.

That design is not created here. Only after it is reviewed should an implementation plan be written.

## Validation record

- Architectural review approved Outcome B for normal upload and requested the lifecycle, callback-inventory, expected-transformed-state, semantic-equality, cleanup, and next-milestone refinements now recorded here.
- Repository gate: fetched origin; confirmed `main == origin/main == 848715c`; created this branch directly from `origin/main`; verified zero divergence.
- Prohibited macOS toolchain/sysroot manifest churn appeared twice, was inspected in full, and restored only after confirming it was exactly the AGENTS.md-described generated state.
- Disposable Unity probe: final run exited `0`; successful visibility, late-mutation, feedback-control, Candidate B, equivalent-replacement, and recovery builds emitted loadable bundles.
- Refusal probes: all proof-relevant semantic/source/closure witnesses returned unsuccessful with no bundle and no postprocess.
- Project source assets: one aggregate and every recorded per-mode before/after fingerprint were byte-identical in the corrected final run.
- Documentation finalization did not rerun Unity; it relies on the completed disposable probe and changes only this design document.
- Repository probe/product code: none added or changed.
- Census Lab/private content: not accessed or modified.
- Implementation plan: not created.
