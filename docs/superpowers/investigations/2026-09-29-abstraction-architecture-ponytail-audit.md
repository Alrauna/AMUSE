# Abstraction Architecture Investigation and Ponytail Audit

Date: 2026-09-29. Inspected commit: `0af25ff`, clean working tree.

Read-only investigation. No code changed. This record answers one question: is
AMUSE's shader-generic core the right direction now that a third shader will be
added, and what maintainability risks does the current architecture carry? It
also contains the standing whole-repo ponytail audit.

## Method

Four read-only scouts mapped the modules in parallel. One parent pass read the
seam contract, the vision document, and the architecture comparison document
directly. Every published audit finding was re-verified by a direct caller
search in the product package, not taken from a scout report. Line counts come
from a direct count of the inspected commit.

Evidence base:

- All production code under `Packages/com.alrauna.amuse/Editor/`.
- The product test suite under `Packages/com.alrauna.amuse/Tests/Editor/`.
- `docs/architecture/vision.md` and
  `docs/architecture/shader-frontend-comparison.md`.
- `docs/superpowers/investigations/2026-09-24-ponytail-dead-code-audit.md`.

## Question restated

Almost no production file outside `Editor/Semantics/LilToon/` and
`Editor/Semantics/Poiyomi/` names a shader. Is that generality sound, or is the
core a two-shader common denominator that will fight the third frontend?

## Answer in brief

The generality is real, measured, and mostly correct. Three different things
hide under the word "generic", and they deserve different verdicts:

1. Shader-independent exact math and evidence. This is the majority of the
   core. It needs zero edits for a third shader. Keep it.
2. Shader-parametric orchestration. The build pipeline, capture engine, and
   apply path consume shader identity only as opaque data. They need zero to
   two small edits for a third shader. Keep it.
3. The dispatch hub. One file, `UnityMaterialSemantics.cs`, plus one record,
   `CapturedAlphaMaterial`, hard-code the current frontend roster as a closed
   enum, six-plus switch sites, and typed per-frontend evidence fields. Every
   new shader edits this file wide but shallow. This is a deliberate,
   documented, fail-closed design. Its cost is mechanical, not conceptual. The
   risk is drift, and drift has already happened twice.

The direction is sound. The risks are specific, named below, and two findings
are dead code that violates the repository's own stated policy.

## Line census (inspected commit)

| Module | Files | Lines | Share |
| --- | --- | --- | --- |
| `Editor/Build/` + editor root | 22 | ~17,200 | 24% |
| `Editor/Host/` | 19 | ~14,800 | 21% |
| `Editor/Analysis/` | 8 | ~10,600 | 15% |
| `Editor/Semantics/` root | 9 | ~6,100 | 9% |
| `Editor/Semantics/LilToon/` | 10 | ~14,700 | 21% |
| `Editor/Semantics/Poiyomi/` | 2 | ~7,300 | 10% |
| Total production | 70 | ~70,700 | 100% |

Shader-specific frontends are about 31 percent of production code. The
observation behind this investigation is numerically correct: about 69 percent
of production code lives outside the frontend folders. The census below sorts
that 69 percent by what its genericity is worth.

## What the governing documents already decided

This question was adjudicated before. Two documents state the intended
architecture, and this audit measures code against them.

`docs/architecture/vision.md` says: shader-specific knowledge stays with the
frontend unless independent consumers establish a shared concept. A separate
shared domain is justified only by real consumers. The next stage must not turn
every new case into a new framework.

`docs/architecture/shader-frontend-comparison.md` is the operational record.
It classifies all shared concepts into categories A through G. It rejects an
`IShaderAdapter` interface and a frontend registry because nothing in the
repository dispatches polymorphically over frontends. It sets the extraction
rule: do not extract on two producers, extract when adapter number three
confirms byte-identity across three. It names coverage-versus-value as the
strongest unresolved gap in the semantic core. It warns that the four-output
vocabulary rests on two toon-shader producers and may be a toon-shader
coincidence.

This audit finds the code in broad compliance with those documents, with the
violations listed below.

## Where shader knowledge actually lives

Measured sites outside the two frontend folders:

- `Editor/Semantics/UnityMaterialSemantics.cs` (1,012 lines). The hub. It owns
  the `CapturedAlphaMaterialFamily` enum (six members), a ten-branch
  exact-name classifier, six-plus family switches (alpha interpretation,
  transferred twin, attestation, alpha request, capture request, batch build),
  and the `CapturedAlphaMaterial` record with typed `PoiyomiEvidence` and
  `LilToonEvidence` fields.
- `Editor/Analysis/AdmittedMaterialStates.cs`. One leak: the slot resolver
  rebuilds `CapturedAlphaMaterial` passing both evidence fields through and
  asks the hub for the per-family predicate request. It interprets nothing
  itself. A third shader touches one expression here.
- `Editor/Build/AlphaSeparationPreparation.cs`. The conversion dispatch:
  one switch in `ConvertAdmittedMaterial` plus the
  `ConversionRequestForFamily` and `CanonicalPropertiesForFamily` maps.
- `Editor/Build/AmusePlatformFinishPlugin.cs`. Two per-family conversion
  delegate parameters (`VerifiedPoiyomiConversion`, `VerifiedLilToonConversion`)
  threaded through five nested signatures.
- `Editor/Build/LockedMaterialIdentity.cs`. Thry lock recognition is
  ecosystem-generic, but original-shader attestation is pinned to Poiyomi
  identity only.
- `Editor/Host/UnityRendererAlphaAnalysis.cs`. One enum member:
  `RendererAnalysisRefusal.LockedPoiyomiOriginalShaderUnattested`, a
  vendor-named refusal in the Host closed vocabulary, plus three report
  strings in `AmuseReportStrings.cs`.
- `Editor/Semantics/ToonMaterialCutoutSemantics.cs` and
  `Editor/Semantics/MaterialCutoutSpecification.cs`. Per-shader property
  logic in the shared root. Dead: zero production callers. See the audit.
- `Editor/Host/UnityAlphaFieldEvidence.cs` and
  `Editor/Host/UnityStreamingTextureEvidence.cs`. Comment-level leaks only;
  the red predicate shader's doc rationale names lilToon mask sampling. The
  mechanism is request-driven and neutral.
- `Editor/Semantics/GeneratedTextureAttestation.cs`. Vendor pins, but
  third-party-tool identity, not shader identity.

Everything else measured clean. `Editor/Build/AlphaSeparationApply.cs`, the
single mutation boundary, contains zero shader, family, or property tokens.
Host capture gathers exactly what each frontend's `MaterialEvidenceRequest`
declares and nothing else.

## Third-shader change surface, measured

Case A: a third shader with alpha analysis and no opaque conversion.

- New frontend folder with its own attestation, requests, and interpretation.
- One enum member, one classifier branch, and arms in the hub switches, all in
  `UnityMaterialSemantics.cs`.
- One expression in `AdmittedMaterialStates.cs`.
- Zero edits in `Build/`, `Host/`, and the rest of `Analysis/`.
- Algebra edits only if the new alpha equation needs an operator the seven
  current `ScalarSemanticValueKind` forms cannot express. An unexpressible form
  fails closed as `SemanticsUnknown`. It never produces an unsound claim.

Case B: the third shader also converts to canonical opaque materials.

- Case A plus: one case in each of the two conversion maps, one branch in
  `ConvertAdmittedMaterial`, one new delegate type, and a new parameter
  threaded through five signatures in the plugin and preparation path.

Case C: the third shader also locks through the Thry optimizer.

- Case B plus: `LockedMaterialIdentity` attestation must dispatch per family.
  Today a Thry-locked non-Poiyomi original fails attestation and is reported
  under the Poiyomi-named refusal member.

Test surface for any case: one or more stand-in shaders, a third fixture base
or an extraction of the two near-duplicate bases, a third seam class, a third
twin class in each of the four characterization property files, and a third
leg in the shared-evidence agreement tests.

The shape is wide but shallow. The edits are loud, compiler-visible at the
switch sites, and fail closed at the `default` arms. Nothing about the design
requires guessing where a new frontend plugs in. The cost is paid in edit
count, not in risk of silent misbehavior.

## Maintainability risks, ranked

R1. Hub drift. `UnityMaterialSemantics.cs` mixes the neutral contract with
frontend routing. Each shader adds arms to six-plus switches. The file already
carries one proven drift surface: `AnalyzeAlphaMaterialTransferred` re-uses
the family dispatch of `AnalyzeAlphaMaterial` minus the verification gate, as a
hand-copied twin. A copied dispatch is a divergence waiting for an editor.

R2. The evidence record bakes in the roster. `CapturedAlphaMaterial` holds one
typed nullable field per frontend. Every new frontend grows the constructor,
and every construction site, in production and in tests, follows. This is the
single hardest two-shader assumption in the codebase.

R3. Algebra coverage is empirical, not universal. The value algebra is neutral
in form, but its operator set is the accreted union of two toon shaders'
alpha equations. Documented genealogy: saturating sum and difference come from
lilToon layer modes, product chains and the invert idiom from Poiyomi. A
shader outside the toon family may need new kinds and new resolver lemmas. The
fail-closed default bounds the blast radius to refusal, never to unsoundness.
The comparison document already prescribes the right test: pick frontend three
to falsify an axis, not to confirm a third toon shader.

R4. Vendor-named lock machinery. The Thry lock classifier is generic. Its
attestation gate is Poiyomi-only, and the refusal member and report strings
name Poiyomi. A future lock-capable non-Poiyomi shader would be refused and
misreported under a wrong vendor label. The rename has a cost: the research
census mirror pins refusal names one-directionally, so the change must
coordinate with the census vocabulary.

R5. The four-output vocabulary is production-idle beyond alpha. The full
`AnalyzeBaseMaterial` path has no production caller. Production runs the
captured alpha path and leaves base color, emission, and normal Unknown. The
algebra's breadth beyond alpha is validated by tests, not by a consumer. That
is the comparison document's own warning made concrete: generality is being
carried on one live output.

R6. Conversion seam signatures. The per-family delegate parameters are the
deliberate alternative to a registry. Their cost is now measured: five
signatures per conversion-capable shader, plus two maps and one switch case.
Tolerable at three shaders. Re-price at four.

R7. Test architecture grows per shader. The four characterization properties
are expressed as per-shader twin classes, not parameterized cases. The two
fixture bases duplicate about 90 lifecycle lines. Two generic-core suites,
`AdmittedMaterialStatesTests` and `RendererAlphaAnalysisIntegrationTests`,
exercise the generic core through exactly one frontend, Poiyomi. The core's
multi-family behavior at those layers is untested by construction today.

R8. Family-internal duplication in lilToon. The cutout and transparent
slices duplicate about 20 property constants, 45-entry request tables, and
gate arrays. The transparent table is a documented delta of the cutout table.
`FirstFailedZeroGate` now exists as six byte-identical copies across the two
frontends, and `TryReadBinary` as two. The comparison document's extraction
trigger counts producers, not copies. The copy count has outrun the spirit of
the rule while the letter of the rule says wait for frontend three.

Drift evidence that R1 and R8 are live risks, not theoretical ones: the dead
cutout pair below was born with a speculative doc comment and survived two
audits, and the superseded 2026-08-17 adapter spec stated a reason for
`TryReadBinary` duplication that the merged code falsified.

## Ponytail audit findings

Scope: production code and product tests of the inspected commit. Findings
ranked biggest cut first. Each finding was verified by a direct caller search.
Correctness, security, and performance are out of scope. Nothing is applied.

- `delete:` `ToonMaterialCutoutSemantics` and `MaterialCutoutSpecification`,
  plus `Tests/Editor/Semantics/ToonMaterialCutoutSemanticsTests.cs`. Zero
  production callers; only their own tests consume them. Born speculative in
  commit `426e04e` with a "future extensions" doc comment and no consumer.
  Cutoff clamping already lives in the frontends' eligibility and request
  layers. Replacement: nothing. Flags a third time, after the 2026-09-16
  cleanup audit and the 2026-09-24 audit's cleared list. Path:
  `Editor/Semantics/ToonMaterialCutoutSemantics.cs`,
  `Editor/Semantics/MaterialCutoutSpecification.cs`.
- `shrink:` `AnalyzeAlphaMaterialTransferred` re-implements the
  `AnalyzeAlphaMaterial` family dispatch minus verification, about 70 lines of
  twin switch. Replacement: one dispatch with a verified-skip parameter.
  Path: `Editor/Semantics/UnityMaterialSemantics.cs`.
- `shrink:` three capture entry points repeat the same shaders, inputs,
  capture, build loop. Replacement: one core with an optional attestation
  filter. Path: `Editor/Semantics/UnityMaterialSemantics.cs`.
- `shrink:` exact-float decoder and encoder unification. RETRACTED on
  2026-09-29 by direct body read during the same-day design. `DecodeFloat`
  normalizes the dyadic and throws on non-finite input, while `Decompose`
  keeps the raw n times 2 to the d pair that `AffineAlphaMap` equality is
  defined on, so normalization would change observable equality.
  `EncodeToNearestFloat` takes a rational and returns an exact error term,
  while `Round` takes a dyadic pair and returns a float. No merge. The true
  remainder is the three `BitLength` helpers. Disposition:
  `docs/superpowers/specs/2026-09-29-generic-core-hygiene-design.md`.
  Paths: `Editor/Analysis/ExactUvGeometry.cs`,
  `Editor/Semantics/AffineAlphaMap.cs`.
- `shrink:` `AlphaSamplingSettings` in the classifier duplicates
  `TextureSampling` field for field, copied at two resolver sites.
  Replacement: pass `TextureSampling` through. `Analysis` already references
  the `Semantics` namespace, so no layering change. Paths:
  `Editor/Analysis/TriangleAlphaClassifier.cs`, `Editor/Analysis/AlphaSemanticsResolver.cs`.
- `shrink:` `ReadEffectiveRenderState` exists verbatim in both frontends'
  conversion paths, reading queue and render type. A Unity host fact, not
  shader knowledge. Replacement: one internal helper in the shared root.
  Paths: `Editor/Semantics/LilToon/LilToonOpaqueTarget.cs`,
  `Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`.
- `stdlib:` three hand-rolled bit-length helpers, one byte-array variant and
  two identical shift loops. `BigInteger.GetBitLength()` ships in the .NET
  Standard 2.1 profile. Verify the assembly's API compatibility level before
  cutting. Replacement: the framework method. Paths:
  `Editor/Semantics/AffineAlphaMap.cs`, `Editor/Analysis/ExactUvGeometry.cs`,
  `Editor/Analysis/AffineUvTransform.cs`.
  Outcome, dated 2026-09-29: the swap was attempted and falsified. Unity
  2022.3.22f1 lacks `BigInteger.GetBitLength` at `apiCompatibilityLevel` 6.
  The compile reported nine errors, one per swapped call site. The three
  helpers are kept, each with a provenance comment that names the date.
  Disposition: the ruled fallback of the same-day hygiene plan, Task 5.
- `stdlib:` Unity blend and compare enum values hand-copied as raw float
  literals in two conversion factor tables. Replacement: cast the
  `UnityEngine.Rendering` enums at the declaration. Paths:
  `Editor/Semantics/LilToon/LilToonOpaqueConversionResult.cs`,
  `Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`.
  Outcome, dated 2026-09-29: the replacement landed in the same-day hygiene
  plan, Task 6. The blend casts name `UnityEngine.Rendering.BlendMode`
  because `BlendFactor` is absent in this Unity version. The depth write
  flag stays a literal with a provenance comment because the profile has no
  depth write enum.
- `yagni:` `GeneratedTextureProducer` enum and its out parameter. All three
  external callers discard the value with `out _`. Replacement: a bool
  `TryIdentifyProducer(Texture)`. Path:
  `Editor/Semantics/GeneratedTextureAttestation.cs`.
- `delete:` `PreparedSlotSeparation.FamilyOfAdmitted`. Zero readers in the
  package. Its doc comment claims the window close reads it. No reader exists.
  Replacement: nothing, plus a truthful doc on the family data that is used.
  Path: `Editor/Build/AlphaSeparationRecords.cs`.
- `shrink:` the `ReferenceEquals` or Unity-equality double check appears three
  times across the transient unlock files. Replacement: one equality helper
  or the Unity == alone where wrapper handling is required. Paths:
  `Editor/Build/TransientUnlockWindowState.cs`,
  `Editor/Build/TransientUnlockSwapIn.cs`,
  `Editor/Build/TransientUnlockWindowClose.cs`.
- `shrink:` minor: `TextureSample` re-validates a source id its own type
  already validates. The UV channel zero-to-three bound is enforced twice.
  The `isProduct` and `isDisjunction` bool pair is a three-state enum in
  disguise. Paths: `Editor/Semantics/MaterialSemantics.cs`,
  `Editor/Analysis/AlphaSemanticsResolver.cs`.

Deferred by the repository's own documented rule, listed for the record, not
counted as cuts: the byte-identical helper cluster (`FirstFailedZeroGate` six
copies, `TryReadBinary` two, `RequireAnalyzableMaterial`, `AllUnknown`),
extract when frontend three confirms byte-identity; the per-family conversion
delegate map, extract at the same trigger; the lilToon cutout and transparent
table twins, a family-internal delta with no second producer yet. The
comparison document governs all three.

Note: `net` counts production lines only, at estimate precision. The API
compatibility note for the stdlib item was corrected on 2026-09-29. The
compile in the editor falsified the availability claim. Unity 2022.3.22f1
does not expose `BigInteger.GetBitLength()` on this profile.

net: about -295 production lines possible, plus about 100 test lines from the
first finding, 0 dependencies possible. Reduced on 2026-09-29 from the first
publication by the retracted exact-float finding above. Reduced again on
2026-09-29 because the falsified BitLength swap left the estimate, so its
share no longer counts.

## Corrections to prior records

- The 2026-09-24 dead-code audit's cleared list states the shared cutout seam
  traces to live consumers. Direct caller search shows the only consumers are
  tests. Tests are not production consumers. The cleared-list claim was wrong
  for this one item. This record corrects it.
- `AlphaSeparationRecords.cs` line 348 documents that the window close reads
  `FamilyOfAdmitted`. Direct search shows no reader. The doc comment is stale.
- The 2026-09-16 cleanup audit flagged the cutout pair. The flag was recorded
  and no cut followed. This audit re-flags with fuller evidence.
- Self-correction 2026-09-29: the exact-float unification finding above was
  scout-sourced and escaped the direct-verification pass. A direct body read
  during the same-day design work retracted it. Every surviving finding in
  this record rests on a direct caller search or direct body read.

## Recommendations

Do now, all mechanical and consumer-safe:

1. Delete the dead cutout pair and its tests.
2. Delete `FamilyOfAdmitted` and fix or remove the stale doc comment.
3. Collapse `AnalyzeAlphaMaterialTransferred` into one dispatch.
4. Collapse the three capture entry loops.
5. Retracted on 2026-09-29 by direct body read. Do not unify the exact-float
   decode and encode helpers. Normalization and non-finite handling differ
   on the two sides. `AffineAlphaMap` equality depends on the raw pair. See
   the retracted finding in the audit section above for the disposition.
6. Apply the two stdlib replacements after the compatibility check.

Decide before frontend three, not after:

7. Rename `LockedPoiyomiOriginalShaderUnattested` to a neutral member and
   coordinate the one-directional census mirror in the same change.
8. Plan the `LockedMaterialIdentity` attestation callback per family, so the
   second lock-capable shader does not inherit a Poiyomi-only gate.
9. Parameterize the characterization twin classes over fixture families
   before writing a third twin of each, or accept the three-fold growth
   explicitly.

Keep the documented deferrals:

10. No `IShaderAdapter`, no registry, no polymorphic frontend dispatch. The
    exclusive-trial selection and closed enum remain correct while no
    polymorphic call site exists.
11. No extraction of gate lists, equations, or attestation models. These are
    categories C, D, and E and stay per shader.
12. Helper-cluster extraction waits for frontend three, per the comparison
    document, unless the owner amends the rule for knowledge-free helpers.
    The six-copy count is the argument for amending it. This record takes no
    side.

## Verification appendix

- Line counts: direct `wc -l` over each module at commit `0af25ff`.
- Dead-code claims: caller search for
  `ToonMaterialCutoutSemantics|EvaluateCutoutSpecification|MaterialCutoutSpecification`
  over both packages; history search showing the introducing commit is the
  only commit touching the evaluator symbol; same-method search for
  `FamilyOfAdmitted` and `AnalyzeBaseMaterial`.
- Vendor-pin claim: direct search for `GeneratedTextureProducer` consumers,
  all three discard with `out _`.
- Prior-audit status: all nine 2026-09-24 findings re-checked in current code
  by the test scout. All addressed as prescribed. The cleared list carried the
  one error corrected above.
- Module classifications: four scout reports cross-checked against the
  parent's direct reads of `UnityMaterialSemantics.cs`,
  `MaterialSemantics.cs`, `AlphaSemanticsResolver.cs`, `AdmittedMaterialStates.cs`,
  `ToonMaterialCutoutSemantics.cs`, `CapturedAlphaSemantics.cs`, and both
  architecture documents. Disagreements were resolved by direct read.
- Scout-only claims marked as such where not re-verified by direct search:
  the six-copy count of `FirstFailedZeroGate`, the verbatim duplication of
  `ReadEffectiveRenderState`, and the file-level Build and Host
  classifications.
