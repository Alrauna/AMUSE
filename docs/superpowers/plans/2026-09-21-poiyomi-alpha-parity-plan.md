# Poiyomi alpha parity - implementation plan

## Privacy note

This plan names no private avatar, renderer, material, texture, mesh, or
hierarchy. It describes private fixtures by role. It records no machine
paths, no host names, no ports, and no asset identifiers. Test fixtures
are public synthetic stand-ins under the repository's own test shader
family. Property names and vendor line anchors are public facts of the
Poiyomi Toon 9.3.64 shader source.

## 1. Authority and shape

Date: 2026-09-21. The authority is the committed note
`docs/superpowers/investigations/2026-09-21-poiyomi-liltoon-alpha-parity.md`
and the design
`docs/superpowers/specs/2026-09-21-poiyomi-alpha-parity-design.md` on the
same date. The roadmap order of note section 8 is the task order below.
The owner decision of 2026-09-21 clears slice 7 with the disclosure.

One task is one implementation prompt. Each prompt states its base
branch and commit, the exact scope and allowed mutations, the required
RED and GREEN evidence, the validation steps, the expected report, the
stop conditions, and the Git authorization boundary. No prompt hides an
unresolved decision. Tasks 5, 6, and 7 carry a named pre-task decision
from the design. The prompt restates it as settled before code moves.

## 2. Session rules for every task

- RED before GREEN. Observe the new test fail first, against the named
  plausible wrong implementation. A first run that passes is
  characterization, and the prompt must say so.
- Falsifiers are numbered adversarial cases, marked
  `--- Falsifier N: ... ---`, and carry no-op guards so later changes
  cannot silence them.
- A filtered run that reports 0 tests is a failure. Run a focused
  filter first, then the full `Alrauna.Amuse.Tests.Editor` assembly.
  Record observed counts and Console output.
- Refresh the Unity asset database after new files, before the run.
- No vendor shader installs. Stand-ins run through the verified seams
  the test assemblies already provide.
- Never weaken a valid test. Never claim a run nobody observed.
- Docs and comments follow simple english. Sweep changed files for
  identifiers before reporting done.
- No staging, committing, pushing, or PR without explicit authorization
  in the task prompt.

## 3. Task 1: BeatSaber gate

### Base facts

`_BSSEnabled` appears nowhere under the editor sources as of
2026-09-21. The vendor toggle turns on a post-clip alpha writer,
`alpha = alpha * emission.z`, and rewrites the alpha blend pair (note
4.7). The coverage gate run precedes the forced-opaque short-circuit on
the alpha path, so a coverage entry protects both. The alpha evidence
request unions `AlphaCoverageGates` into its scalar set.

### RED

Join the existing class that hosts
`AlphaCoverageGateEnabled_IsUnsupportedFeature`. New behavior sentences:

- `BeatSaberModuleEnabled_KeepsAlphaUnknownNamingTheProperty`. Wrong
  implementation: the frontend ignores `_BSSEnabled` and proves the
  base chain. Assert the alpha output is unknown with
  `UnsupportedFeature` naming `_BSSEnabled`.
- `BeatSaberModuleEnabled_WithForcedOpaque_StillRefuses`. Wrong
  implementation: the gate covers only the non-forced path. Assert the
  forced-opaque short-circuit also refuses with the same name.

### Falsifiers

- `--- Falsifier 1: enabled module over an exactly-one domain ---` The
  domain of the main texture alpha is exactly one, and the module is
  on. No triangle proves.
- `--- Falsifier 2: enabled module under force-opaque ---`
  `_AlphaForceOpaque` is 1 and the module is on. The forced path
  refuses.

### GREEN

Add `_BSSEnabled` to `AlphaCoverageGates` in `PoiyomiMaterialSemantics.cs`
with a proof comment pinned to the note 4.7 vendor facts. No other file
changes.

### Expected observed report

Two new tests fail before the fix and pass after. The focused filter
and then the full assembly report their observed counts. No census,
report string, or conversion change appears. The Console shows no new
errors.

## 4. Task 2: premultiply alpha admission

### Base facts

`_AlphaPremultiply` sits in `AlphaFeatureGates` and in
`BaseColorFeatureGates` as of 2026-09-21. The vendor premultiply scales
the base color by `saturate(alpha)` in three passes and never touches
the alpha value (note 4.3). The alpha value is exactly 1 on every
triangle the proof moves, so the feature is an identity there.

### RED

Tests in the Poiyomi alpha seam classes:

- `Premultiply_On_WithProvenExactlyOneDomain_CompletesAlpha`. Wrong
  implementation: the committed alpha gate refuses by name. Assert the
  alpha output completes and carries no premultiply diagnostic.
- `Premultiply_On_SubOneDomain_NeverProvesATriangle`. Wrong
  implementation: a change that lets a sub-one region prove, for
  example by editing the alpha value instead of only the gate list.
  Assert the sub-one region stays unknown at the classification seam.
- `Premultiply_StillGatesBaseColor`. Wrong implementation: the entry
  disappears from both arrays. Assert the base-color output still
  refuses a premultiply material.
- `Premultiply_ConversionStillRefusesUntilSlice7`. Wrong
  implementation: conversion gate 5 opens in the same task. Assert
  `EvaluateVerifiedEligibility` still refuses with
  `PremultipliedAlphaEnabled`.

### Falsifiers

- `--- Falsifier 1: sub-one domain under premultiply ---` A material
  with premultiply on and a sub-one region proves no triangle.
- `--- Falsifier 2: additive blend coverage ---` The conversion blend
  gates still cover the additive factors. The existing conversion tests
  pass unchanged.

### GREEN

Remove `_AlphaPremultiply` from `AlphaFeatureGates` only. Add a proof
comment pinned to the three vendor sites the note cites. `ConversionSchema`
and `BaseColorFeatureGates` stay untouched.

### Expected observed report

Four new tests observed in the RED then GREEN order. The alpha output
of a premultiply stand-in with an exact-one domain completes. The
base-color output and conversion gate 5 keep refusing, observed by test.
Full assembly counts recorded.

## 5. Task 3: mask replace with the admitted pair and invert

### Base facts

Mode 1 with an assigned mask refuses `UnsupportedFeature: _AlphaMask` as
of 2026-09-21. The request asks `TextureEvidenceKinds.None` for
`_AlphaMask`. The vendor mask block samples red through the main
sampler, at `uv[_AlphaMaskUV]` under `_AlphaMask_ST`, panned by
`_AlphaMaskPan.xy` (note 4.2). The lilToon machinery proves the same
algebra with the red field route, which already exists. The design
section 6 fixes the contract: UV gate exact integer 0 to 3, pan exact
zero, pair (1, 0) admitted, saturated pair (1, value >= 1) admitted for
both invert states, unassigned default unchanged, every other pair
refusing.

### RED

Tests in `PoiyomiAlphaMaskTests`:

- `ReplaceBoundMask_AdmittedPair_ProvesThroughTheRedField`. Wrong
  implementation: the frontend keeps refusing an assigned mask, or
  proves the term from the base chain instead of the red sample.
- `ReplaceBoundMask_WithHole_ClassifiesTheHoleUnknown`. Wrong
  implementation: the hole widens transparency or proves.
- `ReplaceBoundMask_NonIdentityMaskSt_ProvesTriangle`. Wrong
  implementation: the mask affine demands identity, unlike the lilToon
  rule.
- `ReplaceBoundMask_UvNotExactInteger_RefusesNamingUv`.
- `ReplaceBoundMask_NonZeroPan_RefusesNamingPan`.
- `ReplaceBoundMask_InvertOnValueZero_ProvesTheInvertedField`. Wrong
  implementation: invert flips after saturation in the wrong order.
- `ReplaceBoundMask_SaturatedPair_ProvesConstantOne` for invert off and
  invert on. Wrong implementation: the saturated pair needs a texel
  threshold.
- `ReplaceBoundMask_OtherStrengthValuePairs_RefuseNamingTheProperty`.

### Falsifiers

- `--- Falsifier 1: mask hole ---` A bound mask with a hole keeps the
  hole triangles unknown or transparent. None prove.
- `--- Falsifier 2: wrap seam ---` A domain adjacent to the wrap seam
  stays unproven under the wrap-blend rule.
- `--- Falsifier 3: non-identity mask affine ---` The triangle proves
  with a non-identity mask ST, mirroring
  `AlphaMaskMode2_WithNonIdentityMaskSt_ProvesTriangle` on the lilToon
  side.

### GREEN

Upgrade the `_AlphaMask` request to `ScaleOffset`, `SourceIdentity`, and
`RedChannel`. Add the UV and pan gates, the pair admissions, and the
invert value shape through the saturating difference. State the binary32
rounding argument for each admitted shape in the proof comments, as the
lilToon term does. Capture, texture identity, and the classifier change
nothing.

### Expected observed report

Eight new tests in the RED then GREEN order. With task 2 landed, the
premultiply and replace stand-in of the locked denim garment fixture
role completes its alpha end to end on the fixture seam. Full assembly
counts recorded. No new diagnostic code, no census change, no report
change.

## 6. Task 4: mask multiply with the declared default

### Base facts

Mode 2 refuses naming `_MainAlphaMaskMode` as of 2026-09-21. The
declared vendor default of `_MainAlphaMaskMode` is 2, so a material that
never touched the mask section carries mode 2 (note 7.3). The lilToon
term names the unbound (1, 0) outcome `MainUnchanged` and folds a bound
admitted pair through the exact product machinery.

### RED

Tests in `PoiyomiAlphaMaskTests`:

- `MultiplyUnboundMask_DefaultPair_LeavesChainUnchanged`. Wrong
  implementation: mode 2 replaces the chain instead of multiplying, or
  refuses.
- `DefaultShapedMaterial_NeverTouchedMaskSection_CompletesAlpha`.
  Wrong implementation: the declared default 2 keeps refusing.
- `MultiplyBoundMask_WithHole_ClassifiesTheHoleUnknown`. Wrong
  implementation: the hole widens transparency.
- `MultiplyModes3And4_RefuseNamingMode`. Wrong implementation: add and
  subtract modes admit without their threshold envelope.

### Falsifiers

- `--- Falsifier 1: bound hole in multiply mode ---` The hole
  classifies unknown. No triangle widens.
- `--- Falsifier 2: fresh default material ---` The material completes
  its alpha when the rest proves.
- `--- Falsifier 3: saturating modes ---` Modes 3 and 4 refuse naming
  `_MainAlphaMaskMode`.

### GREEN

Admit mode 2 for the unbound (1, 0) case as a chain-unchanged term and
for a bound admitted pair through the exact product fold. Modes 3 and 4
keep refusing. The request from task 3 already covers multiply.

### Expected observed report

Four new tests in the RED then GREEN order. A default-shaped stand-in
completes alpha. Full assembly counts recorded. No census or report
change.

## 7. Task 5: Two Pass routing and second-family gates

### Pre-task decisions

From design section 8, settled in the prompt before code moves:

1. Routing shape. Recommendation: a new `CapturedAlphaMaterialFamily`
   member for Two Pass with its own request mapping. Conversion then
   refuses through the existing `OpaqueConversionUnsupportedFamily`
   default, with no new refusal member.
2. Second-family tint. The Two Pass second base reads `_TwoPassColor.a`
   (note 4.1). The Two Pass request extends the plain request with the
   second tint color and `_AlphaForceOpaque2`. The plain request stays
   without them.

### Base facts

`ClassifyShaderName` routes only the plain Toon name as of 2026-09-21.
`TryVerifyPoiyomiIdentity` attests both names, each with its own pinned
identity facts. The unlock precondition reuses that conjunction, so a
locked Two Pass original opens the window as of 2026-09-21. The
unlocked clone then refuses as `AdmittedMaterialSemanticsUnknown`. `_ModeTwoPass` belongs to
task 6.

### RED

- `TwoPassUnlockedName_RoutesToPoiyomiFamily`. Wrong implementation:
  the committed routing answers unsupported.
- `TwoPassForceOpaqueFirstOnSecondOff_NeverCompletesAlpha`. Wrong
  implementation: only `_AlphaForceOpaque` is read, so the second
  family writer slips into a proven claim.
- `PlainToon_StillCapturesWithoutSecondFamilyScalars`. Wrong
  implementation: one shared request demands second-family scalars from
  every Poiyomi material.
- `TwoPass_ConversionRefusesByName`. Wrong implementation: conversion
  runs the plain recipe on a Two Pass material.

### Falsifiers

- `--- Falsifier 1: family divergence ---` Force-opaque on the first
  family and off on the second. Alpha never completes.
- `--- Falsifier 2: plain capture unchanged ---` A plain Toon material
  captures without the second-family scalars, byte for byte the
  committed request shape.

### GREEN

Add the family member or the chosen refusal shape, route the Two Pass
name, extend the request, gate `_AlphaForceOpaque2` beside
`_AlphaForceOpaque`, and answer every family switch the compiler
enumerates. Record in the task report whether the research census stores
the family value, and document the one-value delta if it does.

### Expected observed report

Four new tests in the RED then GREEN order. A Two Pass stand-in with
both families provable completes alpha. A Two Pass conversion attempt
refuses with the chosen named path. Full assembly counts recorded.

## 8. Task 6: cutout coverage split by cutoff

### Pre-task decision

From design section 9: one request captures `_Mode` and `_Cutoff`
unconditionally, and the interpretation branches on the preset. The
cutoff rides the main texture request as a captured theorem scalar, the
lilToon cutout pattern. The plan lands the recommendation unless the
prompt records a different settled choice.

### Base facts

The Poiyomi alpha request has no `_Mode` and no `_Cutoff` as of
2026-09-21. The vendor clips with `clip(alpha - _Cutoff)` in every pass
and forces alpha to 1 in cutout mode (note 4.5). The classification
split route exists. Conversion gate 10 already bounds the cutoff at 1.

### RED

- `CutoutPreset_ChainAtOrAboveCutoff_ProvesTriangle`. Wrong
  implementation: the committed capture never declares the cutoff, so
  the triangle stays unproven.
- `CutoutPreset_ChainBelowCutoff_NeverMoves`. Wrong implementation: a
  fully below-cutoff triangle proves and moves although the shader
  discards it.
- `BlendingPreset_SameChain_StaysSubjectToExactOneRule`. Wrong
  implementation: the split route leaks into a blending preset.
- `CutoutPreset_CutoffMissingOrNonFinite_RefusesNamingCutoff`. Wrong
  implementation: a missing or non-finite cutoff silently proves.

### Falsifiers

- `--- Falsifier 1: at or above the cutoff ---` The chain at the
  cutoff and at 1 proves in cutout mode.
- `--- Falsifier 2: same chain, blending preset ---` The same chain
  under a blending preset stays subject to the exact-one rule.

### GREEN

Add `_Mode` and `_Cutoff` to the Poiyomi alpha request. Switch the alpha
field to the split route when the preset is cutout. The classifier
changes nothing. Conversion changes nothing.

### Expected observed report

Four new tests in the RED then GREEN order. A cutout stand-in proves a
triangle between the cutoff and 1. The same chain under a blending
preset stays exact-one. Full assembly counts recorded.

## 9. Task 7: premultiply conversion admission

### Pre-task decisions

From design section 10, settled in the prompt before code moves:

1. Disclosure string. Recommendation: a distinct report key with its own
  true sentence, fired through the existing per-slot divergence
  mechanism. The depth sentence would be false for a premultiply-only
  case.
2. Unreachable refusal member. Recommendation: remove
  `PremultipliedAlphaEnabled` in the clean cutover. Historical census
  rows keep their recorded names.

### Base facts

Conversion gate 5 refuses `PremultipliedAlphaEnabled` as of 2026-09-21.
The owner accepted the premise revision on 2026-09-21 (note 6.2 and
9.1). The vendor factor is `saturate(alpha)`, exactly 1 on every
triangle the proof moves (note 4.3). The per-slot divergence flag and
report already exist.

### RED

- `Premultiply_ConversionConvertible_WhenOtherwiseEligible`. Wrong
  implementation: the committed gate 5 refuses.
- `PremultiplyConversion_WithUnprovenTriangle_StaysRefused`. Wrong
  implementation: an unscoped admission that lets a material with an
  unproven region carry a premultiply conversion claim. Assert at the
  preparation seam that the unproven triangles stay on the original
  material and the slot refuses.
- `PremultiplyDisclosure_FiresOncePerPreparedSlot`. Wrong
  implementation: silent admission, or a disclosure per triangle. Assert
  exactly one disclosure record per prepared slot.

### Falsifiers

- `--- Falsifier 1: unproven region ---` A premultiply material with
  any unproven triangle never completes a premultiply conversion claim
  for that region.
- `--- Falsifier 2: disclosure count ---` The disclosure fires once per
  prepared slot, and never on a slot without a premultiply conversion.

### GREEN

Admit premultiply in gate 5 with the rewritten premise comment. Add the
disclosure path per the settled decision. Remove or keep the refusal
member per the settled decision, and sweep every switch the compiler
enumerates. Update the report string table if the new key was chosen.

### Expected observed report

Three new tests in the RED then GREEN order. A premultiply stand-in
converts end to end on the preparation seam. The disclosure sentence
appears once per prepared slot. The removed member, if removed, appears
nowhere in the assembly. Full assembly counts recorded.

## 10. Order and dependency notes

The roadmap order is the task order. Task 1 stands alone. Task 2 stands
alone. Task 3 needs task 2 for the end-to-end fixture claim but lands on
its own tests otherwise. Task 4 needs the task 3 request. Task 5 stands
alone. Task 6 needs no earlier task. Task 7 lands last, after task 2, so
the alpha path and the conversion path admit in the declared order.
Each task starts from a base that carries the merged earlier tasks.
