# Ponytail dead-code audit

Date: 2026-09-24. Branch: `chore/0.1.0-pre.3-prerelease-audit`, renamed from
`fix/ponytail-audit` the same day, base `main` at `cf7bef1`.

Method. Static reference tracing over both packages, plus git history mining
with content search. No Unity instance ran for this audit. No test ran. Every
reference count below comes from repository-wide text search on 2026-09-24.

Scope. Everything the repository owns: the product package production and
test code, the product runtime assembly, the research package, and the
package manifests. The audit honors the exclusion list. Attestation pins,
reachable refusal members, absorbing refusal states, falsifier guards,
evidence-capture seams, and vendor-attestation test seams are not findings.

## Ranked findings

### F1. delete: the retired refusal name and its report strings

`RendererAnalysisRefusal.LockedPoiyomiThryUnattested` in
`Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:98`
is produced by no build path since commit `29a6e0e` on 2026-09-24. The
vendor readiness answer that produced it died in the same commit.

Reference inventory, whole repository:

- Product enum member and its retention doc, lines 88 to 98 of the file
  above. No other product reference exists.
- Three report strings with a retention comment,
  `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:174-187`.
  No production code reads these keys. `AmuseReports.RendererKey` builds
  keys from the live enum, so the entries can only fire through the member.
- Research census mirror member `Census.RendererRefusal.LockedPoiyomiThryUnattested`,
  `Packages/com.alrauna.amuse.research/Editor/Census/CensusCategories.cs:50`.
- One switch arm in `CensusVocabulary.ToCensus`,
  `Packages/com.alrauna.amuse.research/Editor/Collection/CensusVocabulary.cs:68-70`.
  This arm is the only compile-time coupling. Removing the product member
  without removing the arm breaks the research assembly build.
- One pinned name in the census snapshot list,
  `Packages/com.alrauna.amuse.research/Tests/Editor/Census/CensusCategorySnapshotTests.cs:48`.
- Dated design records under `docs/superpowers/` name the member as history.

Two tests constrain the removal. `CensusCategorySnapshotTests` pins the
census-side name list. `CensusVocabularyTests.RendererRefusalMirrorsAmuse`
asserts one-directional containment: every product refusal name must exist
in the census mirror. The mirror may hold retired names beyond the product
set. The precedent is `MaterialPropertyOverridesPresent`, removed from the
product on 2026-09-14 and retained in the mirror with a test that pins it.

The product enum doc at lines 13 to 15 claims the census mirrors product
declaration order. That claim is already false. The census list keeps
`MaterialPropertyOverridesPresent` at position three while the product
enum no longer declares it. The sentence needs a truthful reword in the
same slice.

### F2. relocate: the Thry source-digest block

`TransientUnlockAvailability` in
`Packages/com.alrauna.amuse/Editor/Build/TransientUnlockAvailability.cs`
carries a dead attestation block: `PinnedSourceDigests`, `ThrySourceAttested`,
`ComputeSourceAttested`, `SourceDigestOfFile`, `ClearCache`, the
`sourceAttestedCache` field, and the `OptimizerScriptName` constant.

Reference inventory: no production caller anywhere. The only callers are
two tests in
`Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockAvailabilityTests.cs`:
`ThePinnedDigestTableCarriesTheTwoRecordedToolDigests` and
`SourceDigestIsTheSha256OfTheFileBytes`. The third test in that file
exercises the live restore and stays.

The live members of the class are `CreateProductionRestore` and
`WindowEligibleForConsent`. Both have production callers in
`AmusePlatformFinishPlugin.cs` and `TransientUnlockSwapIn.cs`. The class
doc, lines 31 to 45, describes the digest block and must be rewritten when
the block moves.

What the digests attest: the lowercase hex SHA-256 of
`Editor/ShaderOptimizer.cs` as shipped in `com.poiyomi.toon` 9.3.64
embedded tooling, era 1, and in `com.poiyomi.thryeditor` 2.74.2 standalone
tooling, era 2. They were recorded on 2026-09-21 from freshly fetched
vendor archives. Each archive digest matched the pins in the 2026-09-19
characterization first. The 2026-09-21 investigation record states in its
privacy note that the two values live in the repository code table and not
in the record. So a docs-only move would be the first place the values
appear in a document, and the code table remains the only faithful home.

History: introduced by `911ed0a` on 2026-09-20, orphaned by `29a6e0e` on
2026-09-24.

### F3. delete: inert scripting in the vendor stand-in

`Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockVendorStandIn.cs`
keeps a scripted vendor behavior that no test can reach. Repository-wide
search shows `CurrentMode` is assigned only inside `Reset`, always to
`Mode.Real`. No test sets `NoOpRestore`, `PartialRestore`,
`SkipUnpersisted`, or `ThrowOnRelock`. The mode branches, the
`lockedShaderByMaterial` dictionary, `ShaderForRecordedOriginal`,
`LockedFormShader`, and `SetFlag` exist only to serve those branches.

The `LockMaterials(string)` decoy at line 79 documents its purpose as
shape-picking for a reflection seam. No reflection seam has existed in
production since `29a6e0e`. Nothing resolves the decoy and no test asserts
about it.

The live role is the call-count tripwire: `LockCallCount` and
`UnlockCallCount`, asserted zero across `AmusePlatformFinishPluginTests`,
`TransientUnlockTransformationTests`, and `TransientUnlockWindowCloseTests`.
The vendor-shaped signatures must stay. A reintroduced vendor call resolves
this type by name and shape at runtime. If the signatures were deleted
too, such a call would throw instead of counting, and the zero-count
assertions would pass vacuously.

The locked stand-in shaders are live fixtures. `LockedStandIn.shader` and
`LockedStandInOriginal.shader` are loaded by name from
`TransientUnlockTestLifecycle`, `LockedMaterialReconstructionTests`, and
`AmusePlatformFinishPluginTests`. They stay.

History: the stand-in arrived with `911ed0a` on 2026-09-20. Tests drove
the modes before `29a6e0e`. The mode surface became inert on 2026-09-24.

### F4. shrink: false doc sentences on live types

Three doc blocks describe vendor behavior that no path performs:

- `TransientUnlockVendorOutcome` doc,
  `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockAvailability.cs:11-16`.
  It says the outcome reports one vendor call, with the vendor boolean and
  its exceptions folded in.
- `TransientUnlockDelegate` doc, same file, lines 23 to 29. It says the
  restore runs through the vendor seam and converts vendor exceptions.
- `InvertReferences` doc,
  `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs:231-237`.
  It says the caller destroys nothing until every pair has passed through.
  The close loop at lines 100 to 109 inverts, reasserts, and destroys one
  pair before the next pair inverts. The per-pair ordering claims at lines
  15 to 18 and 87 to 90 are true. The cross-pair sentence is false.

The production restore is the in-memory reconstruction through
`LockedMaterialReconstruction.TryApply`. The per-pair destroy order is the
designed order since `29a6e0e` rewrote the loop.

The two type names carry the same false claim. `TransientUnlockVendorOutcome`
and `TransientUnlockDelegate` name a vendor that no path consults. Ten
references exist across the product assembly and its tests.

### F5. shrink: the census semantics seam carries a stale expiry comment

`BaseMaterialSemanticsProvider` in
`Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:209-212`
is commented as kept temporarily for the census collector. The seam is
live. The research `RendererObservationBuilder.Build` overload consumes
it, `CollectorSeamCountingTests` drives it, and
`ResearchSourceApiBanTests.ProductionSourceHoldsNoCalibrationOrSeamType`
pins it as the one allowed carrier. The product threads it through the
`Analyze` overload and the `Capture` legacy path.

The seam is within the audit exclusion list, because it is the census test
seam. The comment is the defect. Nothing temporary remains when a live
cross-package consumer exists. The truthful comment states the consumer
and the migration direction, without a date that never arrives.

### F6. relocate: the instance surface of UnityAlphaFieldEvidence

`Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs` has
two faces. The static face is the production capture engine:
`TryCapture`, `IsAdmittedFormat`, `IsLevelResident`,
`WithoutEvidencePlaceholder`, and the shader asset paths. The instance
face is a capture-now query-later bag: two constructors, `TryGetAlphaField`,
and the instance dictionary.

Repository-wide search finds no production construction of the instance.
`UnityMaterialEvidenceCapture` calls the static `TryCapture`. The
production `AlphaFieldProvider` adapters in
`UnityRendererAlphaAnalysis.cs:654` and `AdmittedMaterialStates.cs:262`
build their own lambdas over captured slots. The instance surface is
consumed only by `UnityAlphaFieldEvidenceTests` and
`AlphaEvidenceClassifierIntegrationTests`.

Both constructors self-describe as kept for a historical call shape. The
immutability characterization they enable is valuable and must survive.
The bag itself is test scaffolding inside a production host file.

### F7. shrink: a test-named entry point with a production caller

`UnityAnimationEvidenceCapture.CaptureGraphForTests` is named for tests.
Its doc at
`Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs:171-174`
says product integration must call the graph entry point above. Both the
name and the doc are false. `AmusePlatformFinishPlugin.cs:622-637` calls
it on the fixture-seam route of the real barrier, whenever a selector seam
is injected and no animation index is active.

The method is a pass-through to the private `CaptureGraph` with the same
parameter list. It needs a truthful name and a truthful doc, or a fold
into `CaptureGraph` itself.

### F8. delete: a test that cannot fail

`TestInfrastructureSmokeTests.PackageEditorTestAssemblyIsExecutable` in
`Packages/com.alrauna.amuse/Tests/Editor/TestInfrastructureSmokeTests.cs`
contains one `Assert.Pass`. It cannot fail, so it proves nothing. The
repository rule that a filtered run reporting zero tests is a failure
already covers assembly discoverability at run time. The research test
assembly has no such smoke test and loses nothing.

History: the file predates the 2026-08-16 rebrand commit `7dc1a6d`.

### F9. keep: the inert-defaults capture overloads

Found during the far-future review on 2026-09-24, after the design
question of what breaks in later years. `UnityAlphaFieldEvidence` exposes
three short `TryCapture` overloads that redirect to the full engine with
inert defaults: the alpha form at line 144, the threshold-and-bounds form
at line 154, and the channel form at line 171. No production caller uses
any of them. `UnityMaterialEvidenceCapture` calls the full form with
explicit policy. The consumers are the friend test assemblies, six test
files, and the instance constructor that F6 relocates.

Disposition: keep, with one truthful doc sentence naming the consumer and
the reason the conveniences stay. They are five-line pure redirects beside
the engine they serve, and they are the documented way the friend tests
drive real capture under inert bounds. Moving them would churn six test
files to relocate fifteen lines. Without the sentence, a future audit
re-finds them as F6 in miniature, one merge later.

## What the audit cleared

The following were checked and found lean on 2026-09-24. No action:

- The Build pipeline types: plugin, state, preparation, apply, records,
  swap-in, window state, close, locked identity, reconstruction,
  structural graph check, bindings capture, lifecycle gate, consent
  dialog, preparation decision. Every member traced to a caller.
- The Analysis exact core and the Semantics frontends, including the
  attestation pins in `LilToonSourceAttestation.cs` and
  `PoiyomiMaterialSemantics.cs`. Pins are excluded evidence.
- The shared cutout seam `MaterialCutoutSpecification` and
  `ToonMaterialCutoutSemantics`: two families consume both.
- `AlphaEvidenceProbe`, `CensusVendorProbe`, `AlphaEvidenceCharacterizationTests`:
  research calibration with recorded rationale.
- The census records, anonymizer, aggregator, and report: every public
  member has a test or a collector consumer.
- `RunEndSceneHygiene` exists once per test assembly. `SetUpFixture` is
  assembly-scoped, so the mirror is the minimum correct shape.
- Test helpers: `TransientUnlockTestLifecycle`, `FixtureProofScope`,
  `FixtureAvatarIdentity`, `OptimizerMergeObservation`,
  `CensusObservationBuilders`, `CollectorTestScene`. Each has a documented
  single purpose and consumers.
- Manifests. The product declares one dependency range, NDMF, and uses it.
  The research package declares none. The dev project VPM set installs
  lilToon and the two optimizer packages, which the attestation
  measurement and the AAO and DAO consumption tests consume. The Unity
  module set matches the VRChat template. Nothing declared is unused.
- The `.gitignore` whitelist under `Packages/` matches the two first-party
  packages and the tracked VRChat core packages.

## Parked, outside the mandate

1. Capturer asymmetry in the barrier fallback. The no-index branch at
   `AmusePlatformFinishPlugin.cs:604-619` drops an injected capturer seam:
   it passes `effectiveCapturer` where the index branch passes
   `capturer ?? effectiveCapturer`. This is live behavior, not bloat. It
   belongs to a correctness review.
2. Census seam migration from `BaseMaterialSemanticsProvider` to
   `CapturedAlphaMaterialSemanticsResolver`. A research behavior change.
   The stale comment fix in F5 does not depend on it.
3. The two `Kept for the historical call shape` constructors collapse into
   F6. If F6 is rejected, the constructors still deserve the F5-style
   comment treatment.

## Answers to the investigation questions

1. Reference inventories are embedded in findings F1 through F8. Doc
   references exist only for F1 and F2, in the dated records named there.
2. Refusal name removal breaks the research assembly at compile time
   through the `CensusVocabulary` switch arm, and breaks the census
   snapshot test if the mirror member is removed with it. Archived census
   observations: the research package holds no serializer and no parser
   for observation files, verified by repository-wide search for JSON,
   serialization, and file IO on 2026-09-24. Archives live by role in the
   private Census Lab project store. The member was producible from
   2026-09-20 to 2026-09-24, so archives from that window can contain the
   name. A retired name kept in the census mirror keeps replay and record
   construction working. Deleting the mirror member too makes any replay
   of that window fail loudly at enum parse. A parser tolerance rule would
   weaken the fail-closed design for every future name, not just this one.
3. The pinned digests attest the two vendor lock tool sources named in F2,
   against the versions named there, cross-checked against the 2026-09-19
   characterization archive pins. The cheapest faithful preservation is a
   data-only record in the research package: the two values plus their
   provenance doc. The locator and hashing machinery is re-derivable and
   needs no home. A research evidence type must respect
   `ResearchSourceApiBanTests`: no `AssetDatabase.FindAssets` in research
   production source, so the record must be data only.
4. History. `LockedPoiyomiThryUnattested` was introduced by `75f69ca` on
   2026-09-20 and orphaned by `29a6e0e` on 2026-09-24. The digest block
   was introduced by `911ed0a` on 2026-09-20 and orphaned by `29a6e0e` on
   2026-09-24. The stand-in mode surface was driven by tests before
   `29a6e0e` and inert after it. The window-close cross-pair sentence
   became false in `29a6e0e`, which rewrote the loop to per-pair order.
   The smoke test dates to before the 2026-08-16 rebrand.

## Net estimate

About 450 lines deleted from the product and its tests, about 80 lines
added to the research package and the test assembly as relocated evidence
and fixtures. One dependency change: none.
