# Ponytail repo-wide audit

Date: 2026-10-04. Branch: `audit/ponytail-2026-10-04`, base `main` at `8bb8565`.
Clean working tree. This audit applied nothing. The branch exists as the home
for follow-up cuts.

This revision incorporates the corrections of an adversarial review that ran
the same day against the raw scout payloads and the live source. The review
appendix lists what the review found and what changed.

## Method

Six read-only scouts scanned the tree in parallel on 2026-10-04. One scout per
area: Build plus Editor root plus Runtime, Host, Analysis plus Semantics,
product tests outside Semantics, Semantics tests plus reference fixtures, and
the research package plus the root surface. One parent pass aggregated, ranked,
and re-verified the claims that carry risk.

The parent re-verified the following against live source on 2026-10-04, by
repository-wide search, not from scout notes alone: F19, F24, F26, F30, F32,
F39, F40, F44, and F47. The parent also read the two prior audit records
cited under Corrections, and the product package manifest.

Line estimates come from the scouts. Counts marked as live recounts come from
the adversarial review of 2026-10-04. No Unity instance ran. No test ran.

## Scope

Everything the repository owns: both packages, production and test code, the
runtime assembly, workflows, tooling, and standing documents. The audit honors
the exclusion list. Attestation pins, reachable refusal members, absorbing
refusal states, falsifier guards, evidence-capture seams, and vendor-attestation
test seams are not findings. The census collector seam is likewise excluded,
per the 2026-09-24 record.

Excluded as not owned or not code: `Packages/com.vrchat.*`,
`Packages/nadena.dev.ndmf`, `ProjectSettings/`, `Library/`, `.meta` files,
`Packages/packages-lock.json`, and the dated records under
`docs/superpowers/`, which are history by convention. The parent scoped the
scouts to a count-only pass there.

## Ranked findings

Biggest cut first. Each finding names its tag, the cut, the replacement, and
the scout line estimate.

### F1. shrink: the lilToon transparent frontend duplicates its cutout twin

`LilToonTransparentMaterialSemantics` repeats the whole
`InterpretTransparentAlpha` body of the cutout path: coverage gates, the mask
arm, the main-texture declared-default and assigned arms, layer composition,
and the mask multiply. Only three deltas differ. They are the cutoff bound,
the two transparent-only gates, and the dither gate membership. The file also
re-copies byte-identical helpers and an evidence request whose scalar table
holds 46 entries in the cutout file and 47 in the transparent file, reordered,
plus two entries, minus one. The 2026-09-29 abstraction audit flagged the
table and gate copies as risk R8. The duplication has since grown to whole
twin methods.

Replacement: one parameterized lilToon alpha interpreter with per-family delta
inputs, plus one shared request builder over a delta table. The pinned
constants and bounds stay per family. The file keeps only its pins.
Estimate: 550 lines.

### F2. shrink: 21 hand-copied PNG texture-import fixture blocks

The staging-to-import block for PNG fixture textures is hand-copied across the
product test suites: `AlphaSeparationPreparationTests`, and
`AlphaSeparationApplyTests`, and `AlphaSeparationSplitTests`, and
`AmusePlatformFinishPluginTests`, and `AaoMergedConsumptionTests`, and
`DaoMergedConsumptionTests`, and `AlphaEvidenceClassifierIntegrationTests`,
and `RendererAlphaAnalysisIntegrationTests`, and `SourceImageAlphaReaderTests`,
and `UnityAlphaFieldEvidenceTests`, and `UnityMaterialEvidenceCaptureTests`,
and `UnityStreamingTextureEvidenceTests`. Pixel-row builders ride alongside.
A live recount on 2026-10-04 found 21 blocks in the product suites. Eight more
copies live under the Semantics tests and are counted in F3.

Replacement: one shared internal builder, for example
`TestTextureImport.WritePng(name, width, height, pixels, configureImporter)`.
It returns the loaded texture. Importer variation is the existing
`Action<TextureImporter>` callback shape. Each call site collapses to one to
three lines. Estimate: 300 lines.

### F3. shrink: test transient lifecycles and fixture plumbing hand-rolled many times

Five scout findings merge here.

`LilToonFixtureTestBase` and `PoiyomiFixtureTestBase` duplicate each other's
shader-agnostic helpers: the 32-line PNG importer, the native-asset helper,
and the whole transient lifecycle. `PoiyomiTextureEvidenceTests` re-implements
the Poyiomi base wholesale instead of deriving from it. The two lilToon multi
suites roll the same lifecycle again, and `LilToonMultiModeGateTests` carries
only the teardown half. Two attestation suites duplicate the project-root and
include-tree environment. The NDMF `ObjectRegistry` save-and-swap boilerplate
is re-rolled in four classes the scouts named, and a live census on 2026-10-04
found it in about seven more, among them `UnityAlphaFieldEvidenceTests` at
five sites and `AmusePlatformFinishPluginTests`.

Replacement: one shared transient scope, one shared texture-import utility for
the two fixture bases, one disposable registry guard, and one attestation
environment helper. Derive `PoiyomiTextureEvidenceTests` from
`PoiyomiFixtureTestBase`. Keep the shader-specific wrappers where they are.
Estimate: 258 lines, the sum of the five merged findings.

### F4. delete: the task-6 SDK source-audit spike record

`docs/task-6-vrchat-sdk-3.10.4-source-audit.md` is a one-off spike record. The
design it fed is implemented. The record pins VRChat SDK 3.10.4 while
`Packages/vpm-manifest.json` has resolved 3.10.5. It lives outside the
sanctioned home for dated records.

Replacement: nothing. Move it under `docs/superpowers/investigations/` if the
hash pins and call-graph citations must survive as history.
Estimate: 205 lines.

### F5. shrink: gate and read helpers copied across the frontends and the eligibility files

`FirstFailedZeroGate`, both overloads, plus `TryReadBinary`, `IsFinite`, and
`RequireAnalyzableMaterial` are byte-identical private copies across
`LilToonMaterialSemantics`, `LilToonCutoutMaterialSemantics`,
`LilToonTransparentMaterialSemantics`, and `PoiyomiMaterialSemantics`.
`RecordUnknown` is copied in three of them. `AllUnknown` is copied in
`UnityMaterialSemantics`, `LilToonMaterialSemantics`, and
`PoiyomiMaterialSemantics`, so not only in the four frontend files.
`BuildEligibilitySchema` is copied in the three lilToon source-eligibility
files, which are not frontend files.

Replacement: one internal static helper type in `Editor/Semantics`, for example
`EvidenceGates`. The schema builder joins recipe and source arrays in one
place. Estimate: 180 lines.

### F6. shrink: the exact product fold is written three times

`Multiply` plus `CollectFactors` plus `HasAnyMap` in the lilToon cutout file
and again in the transparent file. `MultiplyAlphaValues` plus
`CollectProductFactors` in `PoiyomiMaterialSemantics`. All three refuse
saturating shapes, fold constants, and emit the same three result shapes.

Replacement: one static fold beside `ScalarSemanticValue`. Each frontend keeps
only its diagnostic wiring. Estimate: 170 lines.

### F7. shrink: the DAO suite re-declares the AAO fixture and oracle scaffolding

`DaoMergedConsumptionTests` repeats the band model, the authored-triangle
registry, the banded texture import, the stand-in material builders, the
reflected optimizer configuration, and the triangle-match oracle of
`AaoMergedConsumptionTests`.

Replacement: one shared merged-consumption fixture and oracle builder. The DAO
env-var guard, the SDK dispatcher call, and the d4rk cleanup stay in the DAO
file. Estimate: 150 lines.

### F8. shrink: twelve linear Interpret wrappers plus one two-pass variant

Twelve test classes each declare a private `Interpret` that only forwards to
`PoiyomiMaterialSemantics.InterpretVerifiedMaterial` with the linear color
space. One further site forwards to the two-pass form. Two of the wrappers add
an optional color-space parameter. The LilToon base already ships this seam.

Replacement: one protected `Interpret` seam plus one `InterpretTwoPass` on
`PoiyomiFixtureTestBase`. Delete the 13 local copies.
Estimate: 65 lines.

### F9. shrink: the producer version seam exists twice

`ReplacementTextureAttestation` and `AaoAtlasTextureAttestation` carry the same
60-line version machinery: the admitted list, the delegate seam, the cache
fields, and the reset methods. Only the package-name pin and the version pins
differ.

Replacement: one pinned-version seam type in `Editor/Semantics`. Each
attestation class keeps its pins and its shape predicates.
Estimate: 55 lines.

### F10. shrink: ten hand-rolled test shader writers in nine files

Test suites write a shader source string, import it with forced synchronous
import, and resolve it by find or load, with a null assert. The scouts named
four copies in three files. A live recount on 2026-10-04 found 10 copies
across 9 test files, among them `UnityMaterialSemanticsTests`,
`PoiyomiCutoutSplitTests`, `LilToonAttestationTests`, and
`LilToonOpaqueTargetTests`.

Replacement: one shared `WriteTestShader(name, shaderText)` helper in the
shared test fixture home. Estimate: 50 lines.

### F11. shrink: the scene-hygiene mirror

`RunEndSceneHygiene.LeaveNoModifiedSceneOpen` re-implements
`TransientUnlockTestLifecycle.AssertNoSavedSceneDirty`. The capped restart
loop and the dirty-scene tripwire are verbatim, comments included. Both live
in the same test assembly.

Replacement: call the existing static. Keep both entry points.
Estimate: 45 lines.

### F12. shrink: AlphaSamplingSettings duplicates TextureSampling

`AlphaSamplingSettings` in the classifier duplicates the `TextureSampling`
fields and the three enum validations. The resolver copies it at exactly two
construction sites. Analysis already references the Semantics namespace.

Replacement: pass `TextureSampling` through. No layering change.
Estimate: 45 lines.

### F13. shrink: the GPU readback core exists twice

`UnityAlphaFieldEvidence.TryAcquireLevel` and the per-mip loop in
`UnityGeneratedTextureEvidence.TryCapture` share the descriptor shape, the
temporary target, the format and size and length checks, the request error
guard, and the flag-buffer validation. The two routes diverge in more than the
target format and the decode arm. The generated route adds inert-bounds logic,
a residency gate, a placeholder fallback, and a session cache. It validates
the flag buffer only in the red arm, never in the green arm. Only
`TryAcquireLevel` saves and restores the active render target.

Replacement: one internal acquisition core on `UnityAlphaFieldEvidence`,
parameterized by target format plus a flag decoder. The merge must state
whether the shared core closes the green-arm validation gap or keeps it, and
must adopt or keep the render-target divergence deliberately, with a doc
sentence. Estimate: 35 lines.

### F14. shrink: the material-slot binding filter re-implemented at six sites

The parse of a material-slot binding, plus the transform-path ordinal check,
plus the renderer-type gate, is written inline six times across the plugin,
the preparation, the apply, the swap-in, twice, and the window close. A live
recount on 2026-10-04 confirmed exactly six sites.

Replacement: one internal Host parse helper, plus one shared renderer and clip
walk for the two swap-in passes. Estimate: 35 lines.

### F15. shrink: six census checked-copy loops

The record constructors in `CensusObservation.cs` and `AnonymizedCensus.cs`
hand-roll the null-check-and-copy loop six times.

Replacement: one internal `CheckedCopy<T>` helper. The per-type invariant
checks stay. Estimate: 35 lines.

### F16. shrink: curve-describe helpers copied four times

`AlphaSeparationSplitTests` declares `DescribeCommittedCurve` and
`DescribeAuthoredCurve` as byte-identical bodies. The committed variant can go
and its callers can point at the authored one. The same shape exists a third
time in `AlphaSeparationApplyTests` and a fourth, as a path-only overload, in
`AnimatorServicesReactivationCharacterizationTests`. The shared
`DescribeObjectCurve` under them is itself duplicated in two of those files.

Replacement: one shared authored-curve describer in the shared fixture home.
Delete the twin in `AlphaSeparationSplitTests` and the per-file copies.
Estimate: 35 lines.

### F17. shrink: the opaque conversion constants partially duplicated

`LilToonOpaqueConversionFactors` and a private block in `PoiyomiOpaqueConversion`
share seven constants and two helpers byte-equivalent. The Poiyomi block lacks
`BlendOpMax`, `ColorMaskAll`, and `DepthWriteOn`. The blocks overlap, they do
not match. lilToon eligibility classes consume the shared one. Poiyomi
conversion consumes only its own.

Replacement: one shared internal factors type in `Editor/Semantics` carrying
the union. Both frontends and both eligibility evaluators reference it.
Estimate: 30 lines.

### F18. yagni: constant-alias subclasses

Three test classes exist only to reach protected fixture-base shader-name
constants. They add no behavior.

Replacement: widen the constants to internal on the two fixture bases. Delete
the three alias classes. The behavioral fixture subclasses stay.
Estimate: 30 lines.

### F19. delete: merge-observation fields no test reads

`OptimizerMergeObservation.RendererSnapshot` writes `GameObjectName`,
`HasUv0`, and `SlotTextureNames` during observe. `MainTextureName` exists only
to feed `SlotTextureNames`. `ContainsAll` has zero callers. A repository-wide
search on 2026-10-04 finds no reader for any of the five. Only `RendererName`,
`SlotShaderNames`, `TriangleKeys`, and the phase snapshot flag are consumed.

Replacement: nothing. Estimate: 28 lines.

### F20. shrink: two 40-line seam methods differing by one token

`VerifiedLilToonTestSeams.VerifiedConversion` and
`VerifiedTransparentConversionStep` share the whole body except the eligibility
type in one call. The doc of the second admits the copy.

Replacement: one private helper parameterized over the eligibility call. Keep
the two public seam names as one-line forwards. Estimate: 25 lines.

### F21. delete: LockedMaterialIdentity facts with no production reader

`AllLockedGuids` and `HasGeneratedShaderAsset`, plus their serialization tags
and constructor plumbing, have zero production readers. The only readers are
roundtrip assertions that pin the copy against its own input. The class doc
claims later increments restore against these facts. That claim is false since
the restore reads the material tags directly. The serialization path also pays
a dead asset-path call and a dead tag read on every pre-check, only to feed
the two dead facts.

Replacement: nothing. Rewrite the false doc sentence. The tag-name constant
stays, because tests read tags through it. Estimate: 25 lines.

### F22. shrink: ReadEffectiveRenderState verbatim in two vendor folders

The same helper, with the same doc, sits in `LilToonOpaqueTarget` and
`PoiyomiOpaqueConversion`. Both are called from the preparation path. The
2026-09-29 audit flagged it. Still open.

Replacement: one internal helper in the shared `Editor/Semantics` root.
Estimate: 25 lines.

### F23. shrink: ObserveClip and ObserveVirtualClip twin bodies

The two observation methods in `LiveAnimationObservation` repeat the same
curve and object-curve enumeration. Only the accessor calls differ.

Replacement: one private core taking the binding set plus accessor delegates.
The two public methods become two-line adapters. The virtual-clip
characterization suite pins the equality of both forms.
Estimate: 22 lines.

### F24. yagni: the GeneratedTextureProducer enum

Every production caller of `TryIdentifyProducer` and `TryIdentifyRouteTexture`
discards the producer with `out _`, verified by search on 2026-10-04. Only
`GeneratedTextureAttestationTests` reads member values. The 2026-09-29 audit
flagged the enum. It has since grown to four members and a second method.

Replacement: `bool` returns. Delete the enum. Rewrite the tests to assert the
bool. Estimate: 20 lines.

### F25. shrink: the default-capturer lambda written three times

The same 8-line lambda closing over the registered-source lookup is written
out three times in `UnityAnimationEvidenceCapture`.

Replacement: one private static factory used by all three sites.
Estimate: 20 lines.

### F26. delete: the placeholder PR workflow

`.github/workflows/pr.yml` runs one echo step and nothing else. Its header
says real CI lands with the 0.1.0 release candidate. The package is at
`0.1.0-pre.4` on 2026-10-04, so the stated moment has arrived and the expiry
sentence is stale. The gate itself is a policy choice for branch protection.

Replacement: delete the workflow and drop the check from any required-checks
config. The lighter alternative is to keep the gate and reword the stale
comment. Estimate: 17 lines.

### F27. shrink: the IsExpected predicates byte-identical in two Host files

`IsExpectedTargetFormat`, `IsExpectedLevelSize`, and `IsExpectedBufferLength`
are defined twice with the same names and bodies, at
`UnityGeneratedTextureEvidence.cs:304-320` and
`UnityAlphaFieldEvidence.cs:920-951`. Both are internal to the same assembly
and namespace.

Replacement: delete the copies in `UnityGeneratedTextureEvidence`. Call the
`UnityAlphaFieldEvidence` originals. Re-point the duplicated test cases. Keep
the 3-argument `HostCapabilitiesPass`. It is a genuinely different gate from
the 4-argument GPU-route version. Estimate: 15 lines.

### F28. shrink: the normalize-and-hash pipeline duplicated

`ComputeNormalizedSourceHash` exists in `PoiyomiMaterialSemantics` and in
`LilToonSourceAttestation`. Same BOM strip, same newline fold, same SHA-256
hex. A comment in the lilToon copy holds the cross-frontend invariant by hand.

Replacement: one shared helper in `Editor/Semantics`. Both attestation classes
call it. Estimate: 15 lines.

### F29. shrink: four identical vendor gate preambles plus one variant

`VendorReachabilityTests` repeats the probe-and-early-return preamble four
times byte-identical, plus a lilToon variant, so five call sites.

Replacement: one local `ProbeInstalled` helper returning the presence or
asserting absence, called at each of the five sites. Estimate: 15 lines.

### F30. yagni: the empty BehaviourIdentity admission tables

`AllowedIdentityValues` is an empty array. `AllowedIdentities` wraps it. The
loop over it in `IsAllowed` never iterates. The doc says the table stays for
future non-VRChat admissions. One test asserts it is empty. Verified by search
on 2026-10-04.

Replacement: nothing. Delete both members and the empty loop. The pinning
assertion goes with them. Estimate: 14 lines.

### F31. shrink: the reference comparer nested twice

Two Host files carry byte-identical `ReferenceComparer<T>` classes. The
comparer is deliberate, because Unity equality would collapse destroyed
objects. It must exist once.

Replacement: one internal static comparer in the Host namespace.
Estimate: 14 lines.

### F32. delete: MipCount and ReadMipLevel wrappers

`LilToonFixtureTestBase.MipCount` at line 448 and `ReadMipLevel` at line 457
delegate to the texture API. A repository-wide search on 2026-10-04 finds only
the definitions.

Replacement: nothing. Estimate: 14 lines.

### F33. shrink: the duplicated conversion-admission prologue

`ConvertAdmittedMaterial` in the preparation repeats the derived-evidence
admission call and the runtime-overwrite loop verbatim in the lilToon arm and
the Poiyomi arm.

Replacement: one private helper returning the slot refusal, called once per
arm before the family dispatch. Estimate: 13 lines.

### F34. stdlib: the hand-rolled hierarchy path join

`AvatarCensusCollector.RelativePath` builds a path with a reverse StringBuilder
loop.

Replacement: `string.Join` over the reversed segments.
Estimate: 13 lines.

### F35. yagni: the dead prerelease-suffix channel

The package version parser carries a prerelease flag through admission,
beyond-bound, and consent-subject checks. A prerelease refuses during
admission and during the beyond-major bound with the same named cause as an
unparseable version. The consent subject check runs for versions that pass
either gate, so a prerelease never reaches it. The channel is behaviorally
dead.

Replacement: nothing. Update the parser doc sentence that advertises the
distinction. Estimate: 12 lines.

### F36. shrink: three ULP stepper copies

`NextFloatAbove` is byte-identical in two lilToon suites. `Ulp` in the
attestation suite is the same mechanism. The profile has no
`Math.BitIncrement`.

Replacement: one shared helper. Keep the bit trick, written once.
Estimate: 12 lines.

### F37. shrink: ExactRational shims private to one file

`AffineUvTransform` carries private `Add` and `Multiply` that only delegate to
`ExactRational`, plus `Abs` and `Maximum` that belong beside the existing
rational operations.

Replacement: call `ExactRational` directly. Move `Abs` and `Maximum` onto it.
Estimate: 12 lines.

### F38. stdlib: hand-rolled list equality

`EffectiveMaterialMaterialization.BlockStateEquals` guards, then walks an
index-for-index loop.

Replacement: `Enumerable.SequenceEqual` after the existing guards.
Estimate: 11 lines.

### F39. stdlib: hand-rolled ordinal containment

`UnityAnimationEvidenceCapture.ContainsOrdinal` iterates a sequence by hand.
Four call sites, verified by search on 2026-10-04.

Replacement: `Enumerable.Contains(source, value, StringComparer.Ordinal)`.
Estimate: 11 lines.

### F40. delete: the test-only AreExactlyZero on the production frontend

`PoiyomiMaterialSemantics.AreExactlyZero` has no production caller. Only
`PoiyomiTextureEvidenceTests` calls it, four times. Verified by search on
2026-10-04.

Replacement: nothing. The tests call `FirstFailedZeroGate` directly.
Estimate: 10 lines.

### F41. stdlib: hand-rolled byte-array equality

`UnityAlphaFieldEvidence.MatchesExpectedPattern` loops elementwise with null
tolerance.

Replacement: keep the null guard. Replace the loop with
`Enumerable.SequenceEqual`. Estimate: 8 lines.

### F42. shrink: the ReferenceEquals double check at three sites

Three Build files restate the same reference-or-Unity-equality pattern. Two of
the three pattern-match an already typed local, so the cast is noise. The
2026-09-29 audit flagged this. Still open.

Replacement: one shared `SameMaterial` helper, or plain Unity equality where
the value is already typed. Estimate: 7 lines.

### F43. delete: the write-only Family property

`CensusVendorPresence.Family` has no reader. Every consumer keys on the
package name, the installed flag, the shader, or the version fields.

Replacement: nothing. Estimate: 6 lines.

### F44. delete: the never-read RootFullPath

`LilToonIncludeTree.RootFullPath` is assigned in the constructor and never
read. A repository-wide search on 2026-10-04 finds only the declaration and
the assignment.

Replacement: nothing. Estimate: 5 lines.

### F45. stdlib: entry-by-entry dictionary copy

`CensusAggregateReport.Freeze` copies the source dictionary entry by entry
before wrapping it.

Replacement: the `Dictionary` copy constructor. Estimate: 5 lines.

### F46. stdlib: hand-rolled finiteness

`ReferenceFixtureData.IsFinite` expands `!IsNaN && !IsInfinity` by hand.

Replacement: `float.IsFinite`. It ships in the .NET Standard 2.1 profile of
Unity 2022.3. Estimate: 4 lines.

### F47. yagni: the parameterless capability overload

`UnityAlphaFieldEvidence.HostCapabilityCheckPasses()` redirects to the channel
overload. Its only caller is one test, verified by search on 2026-10-04. The
2026-09-24 record adjudicated the sibling `TryCapture` conveniences as keep,
with a naming consumer sentence. This overload has neither.

Replacement: nothing. The test passes the channel explicitly.
Estimate: 4 lines.

### F48. delete: a dead null assignment

`ClonePersistRestoreVerify` assigns `mismatchReason = null` inside a branch
already guarded by that condition.

Replacement: nothing. Estimate: 1 line.

### F49. stdlib: raw float blend literals on the alpha path

`PoiyomiMaterialSemantics.IsProvenOpaqueBlend` compares captured blend state
against raw float literals at lines 1437 to 1443. The conversion-table cleanup
cast the Unity enums elsewhere. This path was missed. No line saving. It is a
clarity defect in scope for this audit only because the fix is mechanical.

Replacement: cast `UnityEngine.Rendering.BlendMode` and `BlendOp` at the
comparison, as the conversion files already do. Estimate: no line saving.

## What the audit cleared

The following produced no findings on 2026-10-04:

- The Editor root files and the runtime assembly. One component, one editor,
  one information class. The adversarial review confirmed this enumeration
  against the live tree.
- The two Host predicate shaders.
- `Website/`, `Tools/`, the release and build-listing workflows, README, the
  package manifests, and `.omp/RULES.md`.
- `docs/superpowers/` as history. The parent scoped the scouts to a count-only
  pass there, per convention.
- The census collector seam and the vendor-attestation seams, per the
  exclusion list.

## Corrections to prior records

- The 2026-09-24 dead-code audit cleared `OptimizerMergeObservation` as a
  helper with documented purpose and consumers. Direct search on 2026-10-04
  shows five written members with no reader anywhere. F19 corrects that entry.
- The 2026-09-29 abstraction audit deferred five of the items that reappear
  here under its documented frontend-three rule, which waits for a third
  frontend to confirm what is byte-identical. The deferral was a recorded
  decision, not an oversight. The five items reappear here unchanged or grown:
  the gate and table copies, now F1 and F5, the render-state helper, now F22,
  the sampling duplicate, now F12, the producer enum, now F24, and the
  equality double check, now F42. The duplication has since grown beyond what
  that record measured.

## Risk notes

- F1 walks pinned frontend behavior. Run the falsifier and characterization
  suites before and after any merge of it. Keep the per-family constants and
  bounds in place.
- F2 and F3 re-plumb the fixture layer for most of the suite. Cut them in
  small slices, with a focused EditMode filter between slices. A filtered run
  that reports zero tests is a failure.
- F13 hides a validation asymmetry. The merged core must state whether it
  closes the green-arm gap. Closing it changes generated-route behavior.
- F26 changes branch protection. Remove the required check before the workflow
  goes, or keep the gate and fix the comment.
- F24 rewrites consumer tests from enum assertions to bool assertions. Keep
  the falsifier guards.

## Net estimate

About 2,676 lines across 49 findings. This is the sum of the scout estimates
printed in the findings. F2 and F3 overlap by about 60 lines in the
fixture-base import copies, so the true net is slightly lower. Dependencies
removable: none. Every stdlib swap sits inside the .NET Standard 2.1 profile
Unity 2022.3 compiles.

## Adversarial review appendix

Date: 2026-10-04, same day, after the first draft of this record.

Method. Two independent reviewers. One compared the record against the raw
scout payloads, finding by finding. One re-verified sampled claims against the
live source. Both worked read-only.

Verdicts. Faithful with defects, and incorrect until fixed. All defects below
were applied to this revision.

Defects found and fixed:

1. The first draft silently dropped the curve-describe payload finding. F16
   restores it. The count moved from 48 to 49 findings.
2. F13 claimed only the target format and the decode arm differ. Live source
   shows the generated route also adds inert-bounds logic, a residency gate, a
   placeholder fallback, and a session cache, and validates the flag buffer
   only in the red arm. The text now lists all divergences and the risk note
   names the green-arm gap.
3. The net estimate said 2,588 lines and no finding carried an estimate, so
   the number was not recomputable. Every finding now prints its scout
   estimate and the net is their sum.
4. The Method list mixed a stdlib finding into a sentence about delete and
   yagni claims. The list now names only what the parent re-verified.
5. Count corrections against live source: 21 PNG import blocks in the product
   suites, plus 8 Semantics-side copies, 10 shader-writer copies across 9
   files, about 11 classes carrying the registry boilerplate, 46 and 47 scalar
   table entries, four linear gate preambles plus one variant, twelve linear
   Interpret wrappers plus one two-pass variant.
6. F17 overstated the conversion-constants overlap. Seven constants and two
   helpers match. The Poyiomi block lacks three members. The text now says
   partially duplicated and the replacement carries the union.
7. F35 stated the consent check runs only on admitted versions. It runs on
   admitted and beyond-bound versions, and both gates exclude a prerelease.
   The conclusion stands. The rationale is now exact.
8. The Corrections section said the 2026-09-29 record recommended immediate
   cuts. It deferred them under a recorded rule. The text now says so.
9. Dropped caveats restored: the behavioral fixture subclasses stay in F18,
   the 3-argument capability gate stays in F27, and the dead per-call costs
   joined F21.
10. The cleared section named one predicate shader where there are two, and
    cited the census seam to an exclusion list that does not name it. Both
    fixed.

What the review confirmed. Sixteen of twenty assigned re-verifications held
against live source: F1, F4, F7, F12, F14, F19, F21, F24, F26, F27, F32,
F35, F40, F44, F47, and F49. The prior-record citations trace to real
records. The simple-english check passed with zero semicolons and zero
contractions. The first draft carried 52 of 53 payload findings with all tags
faithful.
