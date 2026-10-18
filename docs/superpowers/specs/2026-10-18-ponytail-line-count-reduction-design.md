# Ponytail Line Count Reduction Design

Status as of 2026-10-18.

Spec for: `docs/superpowers/investigations/2026-10-18-ponytail-line-count-reduction-investigation.md`
Audit table: `docs/superpowers/audits/2026-10-13-ponytail-audit.md`

## 1. Decision record

The owner decided the five open points on 2026-10-18:

1. Cut the three non-alpha semantic pipelines. The vocabulary stays in git history and in `docs/architecture/vision.md`. Re-add when a real consumer exists.
2. Keep proof-grade explicitness in `TriangleAlphaClassifier`. The two classifier shrinks are out of scope. The proof core keeps its verbatim duplication.
3. Delete `ThryLockToolDigestRecord.cs`. Evidence decision, recorded here as the file's header demands: repository-wide grep finds zero references. The file itself states it holds no consumer by design. The type pins no attestation data. Its removal removes no evidence used by any test, gate, or report.
4. Approve the full mechanical core as scope.
5. Proceed now with baseline-relative test gates.

## 2. Goal

Reduce repository line count by the selected findings. Zero behavior change except two approved removals: the three non-alpha pipelines, which production never reads, and the digest record file. Classification outcomes, refusal reporting, and the build mutation stay byte-for-byte identical in behavior.

Estimated net cut: about 2790 lines `[ESTIMATE]` (mechanical core about 1360, pipelines about 1400, digest record 30). Test files that pin only removed surface shrink further. The plan records observed counts, not estimates.

## 3. Non-goals

- No edit inside `Editor/Analysis/TriangleAlphaClassifier.cs`.
- No edit to `docs/architecture/vision.md`.
- No edit to the runtime component, its serialized fields, or any asset, prefab, scene, or import setting.
- No new package dependency. No package manifest change. No digest re-derivation. No GUID churn.
- No public API change. `AmuseAvatarOptimizer` stays untouched.

## 4. Selected findings

The spec selects exactly these groups from the investigation. Section numbers cite the investigation.

- G1 `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` (3.2): delete the dead `AlphaCoverageGates`, `AlphaFeatureGates`, and `TextureBackedAlphaGates` arrays, the dead property and detail consts, and the pure forwarding members to `PoiyomiSourceAttestation` and `UnityTextureEvidence`. Keep `TryGetSupportedUvMapping` and `TryGetMainTextureSampling`. Retarget every caller to the owning type. ~200 `[ESTIMATE]`.
- G2 attestation version seam (3.3): each attestation type declares its own `private static readonly AdmittedProducerVersions Versions` with its own pinned admitted set. Make those two fields internal on `AaoAtlasTextureAttestation` and `ReplacementTextureAttestation`. Delete the forwards on both types. Retarget about 118 call sites to `<AttestationType>.Versions.<member>`. Restate the doc-comment contract at the two fields. ~110 `[ESTIMATE]`.
- G3 `AdmittedMaterialStates` facade (3.5): delete the four forwards. Retarget 2 production and 20 test callers to `AnimatedPropertyAdmission`. 58.
- G4 preparation wrapper layer (3.6): delete the four one-call forwards and the dead `IsGrantedShader`. Retarget 7 call sites. 55.
- G5 dead refusal channel (3.7): delete `PreparationRefusal.OpaqueConversionRefused`, the `RefusalDetail` property, and the detail parameter. No report key exists for the member. 24.
- G6 lilToon deduplication (3.8): one shared render-state gate evaluator for gates 5 through 12 only, keyed by the family cutoff bound and cutoff refusal. The per-family tails stay per family: the cutout distance-fade arms and the transparent alpha-boost and subpass-cutoff arms keep their own code, order, and refusal values. One shared `Read` helper on `EvidenceGates`. Replace the hand-rolled NaN sweeps with `EvidenceGates.IsFinite`. Replace the four hand-rolled loops with standard library calls. One shared `LilToonProperties` const class. `TryVerifyIdentityForShaderName` may share only the literal name-to-family-set dispatch. Its verification stays family-set based with the same three-way grouping, the same unrecognized-name fall-through to the transparent family set, and the same null-evidence throw. `OutlineScalarSchema` cut carries a verify gate: cut only after a grep proves no fixture pins the captured request contents. ~300 `[ESTIMATE]`.
- G7 preset and dry-run shapes (3.9): `OptimizerPreset` becomes a positional record with identical constructor and property shapes. Inline the three one-call `DryRunRowBuilder` wrappers. Delete the `SlotCountPhrase` duplicate and the write-only `RendererFingerprintBody`. ~107 `[ESTIMATE]`.
- G8 test consolidation (3.10): merge the three policy-arm runners into one parameterized runner. Route the five remaining `Track<T>` classes through `TestTransientScope`, preserving each site's `allowDestroyingAssets` choice. Delete `CommittedClipCarrying`. Extract the shared `CurveMaterials`, lock-tag block, animation builders, and probe blit core. Extract the container-texture and corner-triangle fixture helpers and the arm-fixture base where the repetition survives grep at execution time. ~430 `[ESTIMATE]`.
- G9 smaller production cuts (3.11): the six cited cuts. 48.
- G10 the three non-alpha pipelines (3.1, owner decision 1): remove the `BaseColor`, `Emission`, and `Normal` slots from `MaterialSemantics`. Remove `ColorSemanticValue`, `NormalSemanticValue`, and their vocabularies. Remove the three-output interpretation from both frontends and from `UnityMaterialSemantics`. Keep `AnalyzeBaseMaterial` names, shader identity attestation, `IsSupportedMaterial`, the alpha path, and alpha diagnostics. The output enums keep only `Material` and `Alpha` members. The research test assembly constructs the four-slot `MaterialSemantics` in three fixture helpers (`CollectorSeamCountingTests.cs:44-101`). G10 rewrites those helpers to the alpha-only constructor. Tests that pin only the removed outputs die with the surface. Tests that pin alpha behavior stay. ~1400 `[ESTIMATE]` production-side.
- G11 digest record (3.10, owner decision 3): delete the file. 30.

## 5. Design rules

1. Deletions change no behavior. Retargets move call sites to the owning type. No wrapper replaces a deleted wrapper.
2. Attestation data never moves between files except the version-seam doc comments. GUIDs, digests, and version tables stay byte-identical.
3. The research package stays read-only in production code. G8 and G11 touch its test assembly and one dead file. G10 also rewrites three fixture helpers in its test assembly. The API ban test must stay green.
4. Unity serialization: dropping the never-written `SerializedTransformationRow` fields is safe because old saves carry extra fields and Unity ignores unknown fields on deserialize. The spec records this as the reason the field drop needs no version bump.
5. Refusal enums stay closed. G5 removes one member with zero producers and zero consumers. Tests that enumerate refusal values walk a smaller set and must stay green.
6. New files: at most `Editor/Semantics/LilToon/LilToonProperties.cs`. Everything else edits existing files.
7. Module layering is unchanged. `Analysis` stays free of `UnityEditor`. Poiyomi extractions stay in their closed namespace.
8. Every test consolidation preserves each suite's observed outcomes and its destroy semantics. Each consolidation carries a case inventory: the merged form covers the union of the original cases, and renamed tests map onto the named baseline set.

## 6. The pipelines cut in detail

G10 touches the largest surface. The cut order inside the change:

1. `MaterialSemantics` loses the three slots and keeps alpha. Every constructor call site in both packages updates in the same change.
2. Both frontends lose the three interpret paths, their gate arrays for those outputs, their output-scoped diagnostic codes for those outputs, and their `SemanticOutput<T>.Unknown()` constructions. The alpha interpret paths, alpha gates, identity attestation, and `IsSupportedMaterial` stay.
3. `UnityMaterialSemantics` loses the three-output result assembly and keeps the alpha resolution.
4. Tests: suites named for the three outputs die entirely. Mixed suites lose only the three-output assertions. Characterization suites that pin the Unknown-claiming behavior of the removed outputs die. The suite outcomes for alpha must match the baseline.
5. The research collector keeps compiling because it reads only `IsSupportedMaterial`. The research test assembly compiles again after step 4 rewrites its three four-slot fixture helpers. The research assembly gates the G10 milestone.

The report string tables keep every key they have. No key exists for the removed outputs.

## 7. Validation

The recorded baseline on 2026-10-18: 24 deterministic failures in `Alrauna.Amuse.Tests.Editor` (10 apply, 5 preparation, 1 finish plugin, 2 report strings, 6 dry run) and a green research assembly (140 passed, 0 failed, 4 skipped). Source: the same-day diagnosis record.

The baseline set is the named failing-test set recorded in the diagnosis record, not a count. Gate for every finalized change: the observed failure set is a subset of the named baseline set, and the passed count per suite is at or above the baseline passed count. A count-based gate would let one fixed baseline failure hide one new failure. The named-set gate closes that hole. A filtered run that reports zero tests is a failure. Focused filters guide development. Before every finalized-change commit, the full product and research assemblies run against this gate. Compilation happens in Unity. The executor verifies the Unity instance identity against this project root before any MCP write.

If the fixture-scope repair track lands first, the baseline turns green and the gates tighten to zero from that point.

## 8. Risks

The investigation's risk register applies, amended:

- Proof-core edits are out of scope by owner decision, which retires the highest-severity risk.
- The pipeline cut is the largest surface. Its stop condition: any alpha-path behavior change. The alpha suites and the falsifier suites are its guard.
- G2's call-site migration is wide but mechanical. Compilation catches misses.
- Test consolidation must not flip any `allowDestroyingAssets` choice.
- The digest decision record lives in section 1 of this spec.

## 9. Verification record

Four parallel adversarial reviews of this spec ran on 2026-10-18: accuracy against the investigation and the code, safety and security, architectural purity and ponytail adherence, and latent bugs. They reported nine must-fix defects. The load-bearing confirmations: the alpha path is independent of the three removed outputs, the G5 removal touches no report key and no enumeration, the G2 seam members are pure delegates, and the group selection covers the investigation item by item. The controller confirmed each defect against the sources and incorporated the corrections above. The investigation record gained the `OutlineScalarSchema` finding and the corrected gate-span description in the same pass.
