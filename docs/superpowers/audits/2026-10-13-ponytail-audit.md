# Ponytail Audit of the AMUSE Repository

Date: 2026-10-13

Revision note: first issue 2026-10-13 as commit `e6f6c7e`. A controller inventory check the same day found that the integration pass dropped one merged finding: the `DeleteTempFolder` triplication of the Build test slice. Row 56 restores it. The merged count is 56, not 55. The net line is unchanged, because the dropped row's 25 lines were already inside the recorded sum.

## Status and scope

- Branch: `ponytail-audit`
- Base: `8a2607bf030fd9db2f3428f4ac13c81260b21aa8` on `main`
- Intended repository change: this audit document only
- Production source, tests, research source, assets, package state, CI, and prior records: unchanged
- Unity, the Census Lab project, private avatars, and external fixtures: not used

This record applies the `ponytail-audit` skill to the whole repository. It ranks what to delete, shrink, or replace with standard library or platform equivalents. It lists findings and applies nothing. It is a one-shot report.

## Privacy note

This audit reads only public repository source. It records no private avatar, no private identifier, and no machine-specific fact.

## Audit question

> What in this repository is dead, duplicated, or more general than any caller justifies?

## Non-goals

This audit does not:

- report correctness bugs, security holes, or performance problems
- apply any cut
- judge the vision direction
- plan an implementation

## Method

Eight parallel read-only scouts scanned disjoint slices on 2026-10-13:

- `Editor/Build/`
- `Editor/Host/`
- `Editor/Analysis/`
- `Editor/Semantics/`
- Editor root files, `Editor/DryRun/`, `Editor/Presets/`, the research Editor sources, `Tools/`, and workflows
- `Tests/Editor/Build/`
- `Tests/Editor/Analysis/` and `Tests/Editor/Host/`
- the remaining test folders and the research tests

Every dead, unused, or one-caller claim carries a repository-wide grep across product, tests, and the research package. A symbol used only by tests counts as production-dead, and the finding says so. The controller merged cross-slice duplicates: the tracked-object pattern and the anisotropic-filtering pin each appeared in two slices and merge into one finding here. Estimates are line counts for the named cut.

## Tags

- `delete:` dead code or unused flexibility. Replacement: nothing.
- `stdlib:` a hand-rolled form of a standard library function. The finding names it.
- `yagni:` an abstraction, layer, or data path with no production consumer.
- `shrink:` the same logic in fewer lines. The finding names the shorter form.

## Findings

Ranked, biggest cut first. Paths drop the `Packages/com.alrauna.amuse/` prefix except the research package paths.

| # | Tag | Cut | Replacement | Path | Est. lines | Confidence |
|---|---|---|---|---|---|---|
| 1 | yagni | the base color, emission, and normal semantic pipelines in both shader frontends, plus `ColorSemanticValue` and `NormalSemanticValue` | nothing. `MaterialSemantics` keeps its alpha slot | `Editor/Semantics/` | 1400 | high |
| 2 | shrink | the private NDMF apply-lifecycle helpers copied across four alpha-separation suites (`NewSwapClip`, `NewController`, `AddRenderer`, `DestroyGenerated`, `DestroyCommittedClone`, committed-clip readers, `DescribeCurve`, `SingleTriangleMesh`) | one shared internal static helper class under `Tests/Editor/Build/` | `Tests/Editor/Build/AlphaSeparationApplyTests.cs` | 280 | high |
| 3 | shrink | the tracked-object pattern (`List` field, `Track<T>`, teardown loop) copied in ten Build test classes and nine Analysis and Host test classes, with drifted null guards and destroy flags | the existing `Tests/Editor/Shared/TestTransientScope` | `Tests/Editor/Build/AlphaSeparationApplyTests.cs` and 18 more files | 250 | high |
| 4 | shrink | the four `HasMappedWitness*` walks in `TriangleAlphaClassifier` under density policy 0 | a one-line predicate per walk: `ClassifyX(triangle, texture, envelope, 0) != TriangleAlphaOutcome.ProvenOpaque` | `Editor/Analysis/TriangleAlphaClassifier.cs` | 230 | high |
| 5 | shrink | the three mirror-image policy arm runners `RunCutoutPolicyArm`, `RunMipPolicyArm`, `RunAlphaPolicyArm` | one parameterized runner | `Tests/Editor/Build/AlphaSeparationPreparationTests.cs` | 110 | high |
| 6 | shrink | the anisotropic-filtering save, set, and restore pin copied into eleven test fixtures | one shared `IDisposable` pin under `Tests/Editor/Shared/` | `Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs` and 10 more files | 90 | high |
| 7 | yagni | the `ResearchMaterialSemanticsProvider` delegate, its `Analyze(Renderer, provider)` overload, and the legacy-provider branch in the private capture | migrate the research census collector to the captured-evidence route, then delete the seam | `Editor/Host/UnityRendererAlphaAnalysis.cs` | 70 | medium |
| 8 | shrink | the three nested arm fixture classes `LocalityArmFixture`, `TransparentArmFixture`, `DepthTestPolicyArmFixture` | one base arm fixture holding the shared `Run` and `Create` skeleton | `Tests/Editor/Build/AlphaSeparationPreparationTests.cs` | 70 | high |
| 9 | shrink | the four erased-texel substitute pre-passes in `TriangleAlphaClassifier` | two walks parameterized by the interval function | `Editor/Analysis/TriangleAlphaClassifier.cs` | 60 | medium |
| 10 | yagni | the seven one-caller optimizer-to-proof wrappers (`ProofMipCapFrom`, `MinTextureSizeFrom`, `MinimumCoverageFrom`, `MinimumPreserveCoverageFrom`, `OpaquePercentFrom`, `PolygonClampPercentFrom`, `DensityCapFromCoveragePercent`) | call the Analysis methods directly with `optimizer?.Field ?? inert` | `Editor/Build/AmusePlatformFinishPlugin.cs` | 60 | medium |
| 11 | delete | the two `CaptureObservedForTests` wrappers in the animation capture | an observations parameter on the existing `CaptureGraphThroughSeams` entry | `Editor/Host/UnityAnimationEvidenceCapture.cs` | 60 | high |
| 12 | shrink | the create, fill, name, add, and save container-texture fixture block repeated nine times in `UnityGeneratedTextureEvidenceTests` | one local `AddContainerTexture` helper | `Tests/Editor/Host/UnityGeneratedTextureEvidenceTests.cs` | 60 | high |
| 13 | yagni | the eight per-row data fields on `MaterialTransformationRow` with their null and -1 sentinel plumbing | keep `Kind` plus `Sentence`. The drawer reads nothing else | `Editor/DryRun/MaterialTransformationRow.cs` | 55 | high |
| 14 | delete | three of the four `UnityAlphaFieldEvidence.TryCapture` overloads | tests call the full overload with explicit arguments | `Editor/Host/UnityAlphaFieldEvidence.cs` | 45 | high |
| 15 | shrink | the front half of `LilToonSourceAttestation.GatherMultiContainerEvidence`, byte-identical to `Gather` | one shared gather helper | `Editor/Semantics/LilToon/LilToonSourceAttestation.cs` | 45 | medium |
| 16 | shrink | `VerifiedOpaqueMaterial` and `VerifiedTransparentMaterial`, defined ten times across five classes | two shared statics, or a bool parameter on `CreateVerifiedMaterial` | `Tests/Editor/Build/AlphaSeparationApplyTests.cs` | 45 | high |
| 17 | shrink | the `SnapshotFloats` plus `AssertUnchanged` pair, byte-identical in the lilToon and Poiyomi opaque-conversion suites | one internal static shared helper | `Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs` | 45 | high |
| 18 | shrink | the two-step census-record-plus-report pair repeated at eleven production sites | one shared helper in the shape `TransientUnlockSwapIn.RecordRefusal` already uses | `Editor/Build/AlphaSeparationPreparation.cs` | 45 | medium |
| 19 | delete | the renderer-analysis convenience ladder: `Analyze(Renderer, RegisteredSourceLookup)`, `Capture(Renderer)`, `Capture(Renderer, RegisteredSourceLookup)`, and the one-argument `HostStructuralRefusalFor` | tests call the kept forms. `Analyze(Renderer)` calls the private capture core | `Editor/Host/UnityRendererAlphaAnalysis.cs` | 42 | high |
| 20 | shrink | the `CommittedClip` plus `CurveMaterials` helpers in the two transient-unlock suites, where only one copy descends blend trees | one blend-tree-aware helper on `TransientUnlockTestLifecycle` | `Tests/Editor/Build/TransientUnlockTransformationTests.cs` | 40 | high |
| 21 | shrink | the fixture-base boilerplate duplicated between `LilToonFixtureTestBase` and `PoiyomiFixtureTestBase` | one shared base or shared static helpers | `Tests/Editor/Semantics/Poiyomi/PoiyomiFixtureTestBase.cs` | 40 | medium |
| 22 | shrink | the lower-left corner triangle fixture copied in four files | one shared `TriangleAlphaInput` fixture helper | `Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs` | 39 | high |
| 23 | shrink | `BuildAvatarRoot` and `CreateAvatarRoot`, duplicated verbatim in four classes | one shared `CreateOptedRoot` on `FixtureAvatarIdentity` | `Tests/Editor/Build/TransientUnlockTransformationTests.cs` | 35 | high |
| 24 | shrink | the `ImportSourceAsset` twin, byte-identical in the two replacement-texture suites | one internal static helper with the folder as parameter | `Tests/Editor/Semantics/ReplacementTextureIdentityTests.cs` | 35 | high |
| 25 | shrink | `OddBoundaryPixels`, byte-identical in three Host test files | one shared `Color32[]` fixture helper | `Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs` | 30 | high |
| 26 | shrink | `EvidenceNamingAlphaSource`, body-identical in `AdmittedMaterialStatesTests` and `AlphaFieldSetTests` | one shared fixture builder | `Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs` | 30 | high |
| 27 | delete | `RuntimeStateResolution.AnalyzeRuntimeStatesForTests`, a test entry point living in product code | tests compose the two internal calls themselves | `Editor/Analysis/RuntimeStateResolution.cs` | 27 | high |
| 28 | yagni | `TransientUnlockSwapIn.Availability`, a nested class wrapping exactly one delegate property | pass the `TransientUnlockRestoreDelegate` directly | `Editor/Build/TransientUnlockSwapIn.cs` | 25 | medium |
| 29 | stdlib | the hand-rolled Unknown-membership loop in `HasAnyUnknownOutcome` | `System.Linq` `Contains` behind the same guards | `Editor/Build/AlphaSeparationPreparation.cs` | 25 | high |
| 30 | shrink | the same four fixture lines opening all nine `EffectiveMaterialMaterializationTests` tests | one `NewTwoSlotFixture` helper | `Tests/Editor/Host/EffectiveMaterialMaterializationTests.cs` | 25 | medium |
| 31 | delete | `UnityMaterialSemantics.AnalyzeBaseMaterial`, a frontend-trial dispatcher whose only callers are its own tests | nothing | `Editor/Semantics/UnityMaterialSemantics.cs` | 25 | high |
| 32 | delete | `AlphaFieldSet.WithField` and `AlphaFieldSet.Count` | tests build sets with the public constructor, a pattern the same files already use | `Editor/Host/AlphaFieldSet.cs` | 24 | high |
| 33 | shrink | the 25-line concatenating `TransformationRows` property on the editor, which has no production caller | a one-line internal test seam, or the two test call sites concat the internal lists | `Editor/AmuseAvatarOptimizerEditor.cs` | 24 | high |
| 34 | delete | the two-argument `CurveInversionWasComplete` overload | nothing | `Editor/Build/TransientUnlockWindowClose.cs` | 22 | high |
| 35 | delete | the five test-only no-out alpha interpreter overloads | tests pass `out _` | `Editor/Semantics/` | 20 | high |
| 36 | shrink | the 18-line valid preset JSON literal duplicated across `PresetParserTests` and `PresetFileStoreTests` | one shared test-assembly constant | `Tests/Editor/Presets/PresetFileStoreTests.cs` | 18 | medium |
| 37 | shrink | the uniform byte-array fill loops in four test files | `Array.Fill`, which the repo already uses elsewhere | `Tests/Editor/Analysis/AlphaMipChainTests.cs` | 16 | high |
| 38 | shrink | the always-refuse alpha field provider stub redefined in four test files | one shared test provider | `Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs` | 16 | medium |
| 39 | shrink | the stand-in shader minting template repeated across test files | one `TestShaderWriter.WriteStandInShader` overload | `Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs` | 16 | medium |
| 40 | shrink | the parallel transient tracking in `AlphaSeparationDryRunTests`, where a local list duplicates `TestTransientScope.Track` | route tracking through the scope | `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs` | 14 | high |
| 41 | delete | `ExactUvGeometry.HalfUlp` | the tests that use it as an oracle keep a local copy | `Editor/Analysis/ExactUvGeometry.cs` | 13 | high |
| 42 | delete | `ExactUvGeometry.FloorMod(BigInteger, int)`, which production never calls | the reflection test pins the int overload directly | `Editor/Analysis/ExactUvGeometry.cs` | 13 | high |
| 43 | delete | `HostLifecycleCapability.SupportedAssumption`, a built string with one test reader and no production reader | nothing | `Editor/Build/HostLifecycleCapability.cs` | 12 | high |
| 44 | delete | `LilToonSourceAttestation.GatherOpaqueTargetSourceEvidence` | tests call `TryGatherSourceEvidenceForShaderName` | `Editor/Semantics/LilToon/LilToonSourceAttestation.cs` | 12 | high |
| 45 | delete | `AaoAtlasTextureAttestation.SetAdmittedVersionsForTests`, a forwarded seam its own doc comment declares addressed by no test | nothing | `Editor/Semantics/AaoAtlasTextureAttestation.cs` | 10 | high |
| 46 | delete | the `DryRunFingerprint.Renderer` wrapper that returns its input verbatim | callers use the body string directly | `Editor/DryRun/DryRunCache.cs` | 10 | high |
| 47 | delete | the four-argument `TriangleAlphaClassifier.Classify` overload | tests pass the noise percent explicitly | `Editor/Analysis/TriangleAlphaClassifier.cs` | 10 | high |
| 48 | shrink | `AuthoredSubmeshBounds` and `AuthoredMeshBounds`, duplicated across two test files | one shared mesh-fixture helper | `Tests/Editor/Host/MeshCloneFinalizationCharacterizationTests.cs` | 10 | high |
| 49 | shrink | the `DryRunCache` avatar slot, a dictionary pinned to one entry by its own key guard | one nullable field plus the existing key check, and one `Clear` method | `Editor/DryRun/DryRunCache.cs` | 10 | medium |
| 50 | delete | the unused `MaterialTransformationRowKind.Disclosure` member and its builder, which no emitter produces | nothing | `Editor/DryRun/MaterialTransformationRow.cs` | 9 | high |
| 51 | delete | `LilToonSourceAttestation.Canonicalize`, a one-line wrapper whose only callers are test helpers | the test helper appends `.CanonicalSource` | `Editor/Semantics/LilToon/LilToonSourceAttestation.cs` | 9 | high |
| 52 | delete | the `TransientUnlockWindowClose.ExecuteClose` entry that only forwards to `Execute` | nothing | `Editor/Build/TransientUnlockWindowClose.cs` | 5 | high |
| 53 | yagni | the one-caller `AlphaSeparationSplitTests.CreateSplitTexture` wrapper | inline the two calls at the single call site | `Tests/Editor/Build/AlphaSeparationSplitTests.cs` | 8 | high |
| 54 | delete | `LilToonActivatorOccurrence.Text`, a write-only property | nothing | `Editor/Semantics/LilToon/LilToonSourceAttestation.cs` | 7 | high |
| 55 | delete | the unread `AlphaProbeLevel.Mip` property and constructor parameter | nothing | research `Tests/Editor/Calibration/AlphaEvidenceProbe.cs` | 4 | high |
| 56 | shrink | `DeleteTempFolder`, duplicated byte-for-byte in three transient-unlock test classes, all deleting `TransientUnlockTestLifecycle.TempFolder` | move it once onto `TransientUnlockTestLifecycle` beside `TempFolder` | `Tests/Editor/Build/TransientUnlockSwapInTests.cs` | 25 | high |

net: -3775 lines, -0 deps possible.

## Caveats the design round must resolve

1. Finding 1 contradicts declared direction. The facts are high confidence: production constructs the base color, emission, and normal outputs as constant Unknown and never reads them. But `docs/architecture/vision.md` and the 2026-08-22 audit finding F8 bless the four-output vocabulary as current design. The cut is an owner decision, not a defect. The design round must present it as a decision and record the outcome.
2. Finding 7 requires a research-side migration first. The census collector is the only non-test caller. The migration must preserve census behavior, and the research package never ships.
3. Findings 11, 14, 19, 27, 31, and 35 remove production symbols whose only callers are tests. Each removal moves call-site work into the test assembly. The suite outcomes must stay identical.
4. Findings 9, 10, 15, 18, and 30 are medium confidence. Each needs a verification step before any cut.
5. Findings 4 and 9 touch `TriangleAlphaClassifier`, the exact proof core. Any shrink there must preserve exact classification outcomes on the existing fixture matrix. The audit records the opportunity. It does not weigh proof-grade explicitness against line count. That weighing belongs to the design round.
6. The eleven `PinPerTextureAnisotropicFiltering` copies in finding 6 include the six Build copies that the Build slice reported separately. This record counts them once.

net: -3775 lines, -0 deps possible.
