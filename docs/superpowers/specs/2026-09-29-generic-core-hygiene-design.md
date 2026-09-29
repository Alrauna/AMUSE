# Generic core hygiene design

Date: 2026-09-29. Derived from the investigation record
`docs/superpowers/investigations/2026-09-29-abstraction-architecture-ponytail-audit.md`
at inspected commit `0af25ff`. This spec fixes the disposition of the six
do-now findings and the test strategy for each. Every change is a deletion,
a mechanical collapse, or a standard library replacement. Nothing here
changes observable build behavior: no report string, no refusal name, no
build count, and no material output changes.

## Scope boundary

The investigation's items 7 through 9 are decisions with triggers, not
changes to make now. The refusal rename couples to the one-directional
census mirror. The lock-attestation dispatch waits for a second
lock-capable shader. The characterization twin parameterization waits for
the third twin or an explicit acceptance of the growth. This spec touches
none of them. The documented deferrals (the byte-identical helper cluster,
the conversion delegate map, the lilToon table twins) stay deferred under
`docs/architecture/shader-frontend-comparison.md` and its adapter-three
trigger.

## D1. The dead cutout pair

Decision: delete `ToonMaterialCutoutSemantics`,
`MaterialCutoutSpecification`, and their test class, each with its `.meta`
file.

Options weighed:

1. Delete. Chosen. Zero production callers since the introducing commit
   `426e04e`. The cutoff clamping the pair once carried now travels in the
   texture requests and the per-family eligibility bounds. This is the
   third flag, after the 2026-09-16 cleanup audit and the 2026-09-24
   audit's cleared list.
2. Relocate to the research package. Rejected. The pair is not recorded
   evidence. It is code without a consumer, and the research package must
   not become a code attic.
3. Keep with a truthful doc. Rejected. The repository retention rule says
   a surface kept past its last consumer states who still reads it or what
   must happen before it may go. No reader exists, so the rule admits no
   retention.

Concretely:

- Delete `Packages/com.alrauna.amuse/Editor/Semantics/ToonMaterialCutoutSemantics.cs`
  and its `.meta`.
- Delete `Packages/com.alrauna.amuse/Editor/Semantics/MaterialCutoutSpecification.cs`
  and its `.meta`.
- Delete `Packages/com.alrauna.amuse/Tests/Editor/Semantics/ToonMaterialCutoutSemanticsTests.cs`
  and its `.meta`.

Test strategy: no behavior test can fail by design, because no build path
reaches the pair. Proof is the compile, the full green suite, and the
product test count lower by the observed count of the deleted class.

## D2. The unread family record

Decision: delete `PreparedSlotSeparation.FamilyOfAdmitted` and its whole
plumbing chain.

Options weighed:

1. Delete. Chosen. Zero readers in the package. Its doc comment claims the
   window close reads it. No reader exists.
2. Wire a real reader. Rejected. No consumer needs the per-slot family
   map, so a reader would be speculative surface.
3. Keep with a truthful doc. Rejected for the same retention reason as D1.

Concretely, five sites:

- `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs`:
  the constructor parameter, its null-guard assignment, and the property
  with its stale doc block.
- `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`:
  the dictionary declaration, the `Add` call with its two-map comment
  block, and the constructor argument.

The two-map comment block at the `Add` site rewords to describe the output
map alone. The record constructor keeps its remaining parameters and
guards unchanged.

Test strategy: compile plus the preparation and apply filters. The
construction site count is one, so a missed site fails the compile.

## D3. The twin dispatch

Decision: collapse `AnalyzeAlphaMaterialTransferred` into a private core
shared with `AnalyzeAlphaMaterial`, with one `bool verifyIdentity`
parameter.

The two bodies hold the same family switch and the same interpretation
calls. They differ in exactly one gate per family arm:

- Poiyomi arms: the trusted path verifies `TryVerifyPoiyomiIdentity` and
  answers `UnattestedShader` on failure. The transferred path skips the
  verify call because the struct evidence always exists and the consent
  covers the identity risk.
- lilToon arms: both paths answer `UnattestedShader` on null evidence.
  Only the trusted path additionally runs the `TryVerifyLilToon*Identity`
  call.
- The `Unsupported` arm and the `MaterialSemantics` construction are
  identical.

Concretely:

```csharp
private static CapturedAlphaSemantics AnalyzeAlphaMaterialCore(
    CapturedAlphaMaterial captured,
    bool verifyIdentity)
```

Each arm's gate becomes:

```csharp
if (verifyIdentity && !PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(
        captured.PoiyomiEvidence, out _))
{
    return UnknownWithShaderReason(
        AlphaUnknownKind.UnattestedShader, captured.ShaderName);
}
```

and for the lilToon arms:

```csharp
if (captured.LilToonEvidence == null ||
    (verifyIdentity && !LilToonSourceAttestation.TryVerifyLilToonIdentity(
        captured.LilToonEvidence, out _)))
{
    return UnknownWithShaderReason(
        AlphaUnknownKind.UnattestedShader, captured.ShaderName);
}
```

Both public methods become one-line wrappers passing `true` and `false`.
Both doc blocks survive. The transferred doc gains one sentence: the
verification gate is skipped by the parameter, not by a second switch.

Options weighed:

1. Bool-parameter core. Chosen. The parameter mirrors the one documented
   behavioral difference between the two paths. It adds no policy of its
   own.
2. A verification delegate parameter. Rejected. A delegate implies the
   policy could vary beyond verified and transferred. No third policy
   exists or is planned.
3. Keep the twin. Rejected. The twin is a copied family switch. Every new
   family edits two dispatches. That is the drift surface the
   investigation names as risk R1.

Guards, by named wrong implementation:

- A merge that drops the gate from the trusted path fails the existing
  `UnityMaterialSemanticsTests` cases that assert `UnattestedShader` for
  unattested sources.
- A merge that adds the gate to the transferred path fails the existing
  transferred-path cases in the same class, because a consented
  unverified source must resolve rather than answer `UnattestedShader`.

## D4. The capture batch core

Decision: one private batch core behind the three capture entries.

The three entries repeat the same middle: build the `shaders` array, build
the `inputs` array with `AlphaPredicateRequestFor`, call
`UnityMaterialEvidenceCapture.Capture`, call `BuildCapturedAlphaMaterials`.
They differ in request policy, bounds, and attestation policy:

- `CaptureAlphaMaterials` classifies per material and uses that family's
  alpha request, with `EmptyEvidenceRequest` as the fallback. No bounds.
  No attestation filter.
- `TryCaptureClosedAlphaMaterials` takes one shared request and bounds.
  Every batch member must attest.
- `TryCaptureClosedAlphaMaterialsTransferred` is the same, with a granted
  shader-name set that skips attestation for exactly those names.

Concretely:

```csharp
private static IReadOnlyList<CapturedAlphaMaterial> CaptureBatch(
    IReadOnlyList<Material> materials,
    IReadOnlyList<CapturedAlphaMaterialFamily> families,
    IReadOnlyList<MaterialEvidenceRequest> requests,
    AlphaPolicyBounds bounds,
    RegisteredSourceLookup resolveRegisteredSource)
```

The core owns the array building, the `Capture` call, and the build call.
A null `bounds` selects the no-bounds `Capture` overload, exactly as the
current first entry does. The three public entries keep their own argument
guards, their own request and family construction, and their own
attestation loops.

Options weighed:

1. One core with distinct public entries. Chosen. The three policies stay
   explicit and readable at their entry points.
2. One entry with optional parameters replacing all three. Rejected. The
   entries' signatures are the test seams and the call sites' contract.
   Folding them changes the surface to save lines.
3. Keep the triplication. Rejected. The middle is two drift surfaces
   already. The capture predicate wiring is exactly the wiring a new
   family must not fork.

Test strategy: the three entry points keep their existing tests. A wrong
core that drops the predicate request wiring fails the cutout split cases
in the Semantics filters, because a sibling field would binarize.

## D5. The exact-binary32 dedup, narrowed

The investigation published a shrink finding that
`ExactUvGeometry.DecodeFloat` equals `AffineAlphaMap.Decompose` and that
`EncodeToNearestFloat` matches `AffineAlphaMap.Round`. Direct body reads
during this design falsify both claims. The record is retracted.

- `DecodeFloat` normalizes the dyadic (it shifts out trailing zero bits)
  and throws on non-finite input. `Decompose` keeps the raw
  n times 2 to the d pair and accepts the bit pattern of non-finite
  values. `AffineAlphaMap` equality and its hash are defined on the raw
  stored pair, so normalizing would change observable equality of an
  existing type.
- `EncodeToNearestFloat` takes an `ExactRational` and returns the rounded
  float with an exact `ExactRational` error term. `Round` takes a dyadic
  pair and returns a float. They share only the standard half-even
  mantissa step.

Decision: no decoder or encoder merge. The true remaining duplication in
this area is the three `BitLength` helpers, which move to D6. The
investigation record carries the retraction note dated 2026-09-29.

## D6. Standard library replacements

### D6a. Bit length

Decision: replace the three private `BitLength` implementations with
`BigInteger.GetBitLength()`.

- `Packages/com.alrauna.amuse/Editor/Semantics/AffineAlphaMap.cs:144`,
  the byte-array variant.
- `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs:387`,
  the shift loop.
- `Packages/com.alrauna.amuse/Editor/Analysis/AffineUvTransform.cs:275`,
  the identical shift loop.

Sign-domain argument, checked per call site: `GetBitLength` returns the
bit length of the magnitude, while the loops return zero for negative
inputs. Every call site passes an absolute value or a positive
denominator: `IsExactBinary32` and `HalfUlp` absolute first,
`CompareDyadicMagnitude` receives magnitudes, `FloorLog2` receives the
absolute numerator from `EncodeToNearestFloat` and a positive
denominator, `AffineUvTransform` passes `magnitude`, and `AffineAlphaMap`
passes `abs`. On non-negative inputs the three implementations and the
framework method agree, including zero.

Availability: `ProjectSettings/ProjectSettings.asset` sets
`apiCompatibilityLevel: 6`, the .NET Standard 2.1 profile in Unity
2022.3, which ships `GetBitLength`. The compile after the edit is the
gate. If the method is unavailable, stop and record. The fallback is
keeping the loops with a one-line provenance comment, and that fallback is
an owner decision, not an executor decision.

### D6b. Blend and compare constants

Decision: replace the hand-copied numeric literals with casts of the
`UnityEngine.Rendering` enums, in place, in both frontends.

- `LilToonOpaqueConversionFactors` in
  `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueConversionResult.cs`:
  the four blend factors, two blend operations, two depth comparisons, the
  color mask, and the depth write flag.
- The private mirror in
  `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`:
  its five blend and two comparison constants.

Enum-to-float casts are C# constant expressions, so every constant stays
a compile-time constant and no call site changes. The comment lines that
name the enum values become redundant and go.

Decision on placement: no new shared cross-frontend constants class. The
lilToon factors file's own doc pins its scope to the two lilToon
source-eligibility modules, and the comparison document defers
cross-frontend helper extraction to the adapter-three trigger. The two
predicate methods that read the constants stay where they are.

Test strategy: the constants keep their values, so the conversion and
eligibility filters must stay green unchanged.

## Validation strategy

The plan carries the exact steps. In summary:

1. Task 0 records the full EditMode population of both assemblies before
   any edit, and the observed test count of the deleted cutout class.
2. Each task refreshes Unity, requires a clean compile, and runs its named
   focused filters. A zero-test filter is a failure, except in Task 1
   where the zero result for the deleted class name is the expected
   deletion proof, checked together with a package-wide name search.
3. The final task runs the full population again. The expected delta is
   the observed cutout class count, and nothing else.
4. `git diff --check`, an orphaned `.meta` check, and the identifier
   sweep close the run.

## Stop conditions

Stop and return with evidence when:

1. Any observable behavior changes: a report string, a refusal name, a
   build count, a materializer output, or an `AffineAlphaMap` equality
   result.
2. `BigInteger.GetBitLength()` fails to compile in either assembly.
3. A consumer appears for any surface this spec deletes. Re-rank the
   finding before touching it.
4. The full suite shows any failure not on the known environment list
   recorded in the Task 0 baseline.
5. The diff contains any file outside this spec's target list.
6. The work needs any Unity instance other than the dev editor instance,
   or the Census Lab project.

## Boundary

Branch `chore/generic-core-hygiene` from `main` at `0af25ff`, working tree
only. No staging, no commits, no push, and no branch creation without
explicit user authorization. Every deleted file deletes its `.meta` in the
same task. The research package changes nothing. The only documentation
change is the retraction note already applied to the investigation record
on 2026-09-29.
