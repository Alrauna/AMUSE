# Dead-code cleanup design

Date: 2026-09-24. Derived from the audit record
`docs/superpowers/investigations/2026-09-24-ponytail-dead-code-audit.md`.
This spec fixes the disposition of every finding F1 through F9 and the
test strategy for each. F9 was added by the far-future review on
2026-09-24. Nothing here changes observable build behavior.

## D1. The retired refusal name

Decision: delete the product member and its three report strings. Keep the
census mirror member as a retired name, under the 2026-09-14
one-directional mirror contract.

Options weighed:

1. Retired-names set in the census mirror. The mirror already holds
   `MaterialPropertyOverridesPresent` under this exact rule, with a test
   that pins it. The snapshot stays a superset of the product vocabulary.
   Chosen. It costs one retained enum member and one pinned name in the
   research package, keeps archived census data replayable, and keeps the
   fail-closed mapping.
2. A parser tolerance rule. Rejected. No parser exists in the repository.
   A tolerance rule would have to live in record construction and would
   weaken `CensusGuard.Defined` for every future name, not just this one.
   The census design treats a silent category fold as a miscount.
3. Research-local vocabulary, meaning a second enum for retired names.
   Rejected. It duplicates the mirror with a new concept, and the mirror
   is already the documented snapshot surface.

Concretely, this design:

- Deletes `RendererAnalysisRefusal.LockedPoiyomiThryUnattested`, its doc
  block, and the retention comments in
  `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`.
- Deletes the three `amuse.renderer.LockedPoiyomiThryUnattested` string
  entries and their retention comment in
  `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`.
- Deletes the one switch arm in `CensusVocabulary.ToCensus`. The mapping
  stays total over the product enum and keeps its throwing default.
- Keeps `Census.RendererRefusal.LockedPoiyomiThryUnattested` in the
  research mirror, in place and in order, with a doc line marking it
  retired on 2026-09-24, in the style of the
  `MaterialPropertyOverridesPresent` retention. In-place retention keeps
  every mirror name and numeric value stable, so archived census records
  replay identically. The product enum changes, and no archive ever sees
  the product enum: the census record type holds the mirror value, and
  the vocabulary converts at collection time.
- Rewords the product enum doc. The declaration-order mirroring claim is
  false since 2026-09-14. The truthful sentence: the census mirror holds a
  snapshot of product names and may retain retired ones.
- Records the retirement rule in the mirror doc: a retired name leaves
  the mirror only together with the archives that can contain it. No
  code-side decision alone removes one. This bounds accumulation: every
  retained name has a stated exit condition, even when that condition is
  outside this repository's control.

Test strategy: strengthen `CensusVocabularyTests.RendererRefusalMirrorsAmuse`
with one assertion that the census snapshot still contains
`LockedPoiyomiThryUnattested`, beside the existing retired-name assertion.
On the tree of 2026-09-24 this assertion passes, so it is recorded as
characterization, not RED. The plausible wrong implementation is deleting
the mirror member with the product member. The strengthened pin fails
against that implementation. After the deletion, the proof is the full
green suite: `AmuseReportStringsTests` enumerates the live enum, so it
stays green when member and strings leave together.

## D2. The digest record

Decision: relocate the two pinned digests into the research package as a
data-only record. Delete the machinery from the product.

Options weighed:

1. Status quo. Rejected. Production code would keep an AssetDatabase
   locator, a SHA-256 hasher, and a cache that no build path runs.
2. Data-only record in the research package. Chosen. The values stay in
   code, which the 2026-09-21 record names as their home. The research
   package never ships. The ban test forbids `AssetDatabase.FindAssets`
   in research production source, so the record is two string constants
   and a provenance doc, nothing else.
3. A docs-only record. Rejected as the primary home. The 2026-09-21
   privacy note keeps the values out of documents on purpose. A document
   is also outside every compile-time check.
4. Deletion. Rejected. The values are measured evidence that cannot be
   re-derived without the same verified vendor archives.

Concretely, this design:

- Adds one internal static class in the research package, for example
  `ThryLockToolDigestRecord` under the census collection folder, holding
  the two lowercase hex constants in era order. Its doc states what each
  attests, the vendor package versions, the recording date 2026-09-21,
  and the cross-check against the 2026-09-19 characterization. It also
  states that the record has no consumer by design and that deleting it
  is a reviewed evidence decision, never a cleanup. No methods.
- Deletes from `TransientUnlockAvailability`: `PinnedSourceDigests`,
  `ThrySourceAttested`, `ComputeSourceAttested`, `SourceDigestOfFile`,
  `ClearCache`, `sourceAttestedCache`, and `OptimizerScriptName`.
- Rewrites the `TransientUnlockAvailability` class doc. The truthful text
  covers the two live members only: the in-memory production restore and
  the window eligibility gate.
- Deletes the two digest tests from
  `TransientUnlockAvailabilityTests`. One pins constants against
  themselves after the move. The other re-verifies SHA-256, which is
  standard library behavior.

Test strategy: no new test. A test that asserts a constant equals itself
proves nothing. The research record needs no consumer to justify its
existence, because it is recorded evidence, not code. Proof is the full
green suite and the research assembly compiling.

## D3. The vendor stand-in

Decision: shrink the stand-in to the tripwire it performs.

- Keep: the declaring full name `Thry.ThryEditor.ShaderOptimizer`, the
  vendor-shaped `LockMaterials(IEnumerable<Material>)` and
  `UnlockMaterials(IEnumerable<Material>)` signatures, `LockCallCount`,
  `UnlockCallCount`, and `Reset`.
- The two method bodies become counter increments with a true return.
- Delete: the `Mode` enum, `CurrentMode`, every mode branch, the
  `lockedShaderByMaterial` dictionary, `ShaderForRecordedOriginal`,
  `LockedFormShader`, `SetFlag`, and the `LockMaterials(string)` decoy.
- Rewrite the class doc. The truthful text: the type exists so a
  reintroduced vendor call resolves and counts. The counters are the
  tripwire. No behavior is scripted. The doc notes that the removed
  scripting is recoverable from git history before the 2026-09-24
  handover, so a future test that needs a scripted vendor outcome
  restores it from there rather than reinventing it.

The signatures stay on purpose. Deleting them would let a reintroduced
vendor call throw at resolution time while every zero-count assertion
passes vacuously. The kept shape keeps the tripwire armed. This is a
keep-with-truthful-reason disposition for the signatures, and delete for
everything else in the file's scripting.

Test strategy: no RED exists, because no test drives the deleted surface.
The existing zero-count assertions are the guard for what stays. Proof is
the full green suite.

## D4. Truthful words and names on the restore path

Decision: rewrite the three false doc blocks. Rename the two types that
carry the false vendor claim.

- `TransientUnlockVendorOutcome` becomes `TransientUnlockRestoreOutcome`.
- `TransientUnlockDelegate` becomes `TransientUnlockRestoreDelegate`.
- Ten references update across the product assembly and its tests.
- The outcome doc states: the outcome of one restore call, where a
  completed reconstruction is `Succeeded` and every named refusal is
  `Failed`. A `Failed` outcome is an expected named failure, never a
  defect.
- The delegate doc states: restores one locked clone to its unlocked form
  in memory through `LockedMaterialReconstruction`. Implementations must
  never mutate the locked original.
- The `InvertReferences` doc drops the cross-pair sentence. The truthful
  text: the order inside the method is slot arrays before curves, and the
  caller destroys this pair's clone only after this method and the
  reassertion return.

The rename is included because a type name is the strongest doc a reader
meets. The cost is mechanical and compile-checked. The cheaper option,
doc text only, leaves the false claim in every call site. If the owner
overrules the rename at review, the doc rewrites stand alone.

Test strategy: no RED. Names and docs are not behavior. Proof is the
compile and the full green suite.

## D5. The census semantics seam comment

Decision: keep the seam, reword the comment.

The comment on `BaseMaterialSemanticsProvider` states the live consumer:
the research `RendererObservationBuilder` overload and the census tests.
It states the direction: new product paths use
`CapturedAlphaMaterialSemanticsResolver`. It drops the word temporarily,
because no expiry exists. The migration itself stays parked.

Test strategy: none. Comment only.

## D6. The alpha field evidence instance surface

Decision: relocate the instance surface into the product test assembly as
a fixture adapter. The static capture engine stays in the host.

- The new fixture, for example `CapturedAlphaFieldBag` under
  `Tests/Editor/Host/`, takes an enumerable of textures or texture and
  channel pairs. Its constructor loop, dictionary, and `TryGetAlphaField`
  move verbatim from the production file, so the null-entry skip, the
  duplicate-identity first-wins rule, and the malformed-argument throws
  stay byte for byte. The loop calls the short static
  `UnityAlphaFieldEvidence.TryCapture` overload, the one that already
  applies the inert bounds, and it exposes `TryGetAlphaField` shaped like
  `AlphaFieldProvider`.
- The two constructors and `TryGetAlphaField` leave the production file.
  The production doc claim that the class is the one Unity
  implementation of `AlphaFieldProvider` is reworded to describe the
  static capture engine.
- `UnityAlphaFieldEvidenceTests` and
  `AlphaEvidenceClassifierIntegrationTests` construct the fixture instead.
  The immutability characterization transfers unchanged: destroyed
  texture, mutated texture, absent source, deterministic repeat.
- The three short `TryCapture` overloads with inert defaults stay in the
  production file, per audit finding F9, each with one doc sentence
  naming the friend test assemblies as consumer and the full form with
  explicit policy as the production route. Without that sentence the
  relocation recreates the F6 pattern one layer down, one merge later.

Test strategy: the relocated fixture is proven by the transferred tests
running green. No new assertions. The wrong implementation to fear is a
fixture that re-reads the texture on query; the existing destroyed-texture
and mutated-texture characterization catches exactly that.

## D7. The test-named capture entry point

Decision: rename `CaptureGraphForTests` to `CaptureGraphThroughSeams` and
rewrite its doc. The doc states the truth: the seam-parameterized graph
capture, used by the barrier's fixture route when a selector seam is
injected and no animation index is active, and by the friend test
assembly. A fold into the private `CaptureGraph` was considered and
rejected: the two differ in default injection, and the fold would touch
the seam defaults that the barrier branches rely on.

Test strategy: compile and green suite. Renames are compile-checked.

## D8. The smoke test

Decision: delete `TestInfrastructureSmokeTests` and its `.meta` file. The
repository rule that a filtered run reporting zero tests is a failure
covers the discoverability claim. A test that cannot fail inflates counts.

Test strategy: the full suite count drops by one. That is the expected
observation, recorded in the plan.

## Retention discipline

Three retained surfaces survive this cleanup: the retired census mirror
names, the digest record, and the inert-defaults capture overloads. Each
carries a doc sentence naming its consumer or its exit condition. That is
the anti-recurrence mechanism for the failure class this audit found: the
2026-09-24 handover merge left four retained surfaces, each dated and
reasoned, none with a removal condition, and every one became a finding
here. The rule for future work: a surface kept past its last consumer
states who still reads it, or what must happen before it may go. A
retention comment with neither is a defect, not documentation.

## Stop conditions

Stop and return with evidence when:

1. Any report key, refusal name, or census category changes its runtime
   value or count. That is behavior, not cleanup.
2. The research assembly fails to compile for any reason outside the one
   deleted vocabulary arm.
3. A consumer appears that the audit missed. Re-rank the finding before
   touching it.
4. Any census snapshot or vocabulary test fails after the designed pins
   are in place.
5. The Lab project or any Unity instance outside the dev editor instance
   appears necessary. No slice here needs it.

## Boundary

No commits, no staging, no pushes. One branch,
`chore/0.1.0-pre.3-prerelease-audit`. The
research package must not enter the product or the VPM listing. Every
deleted file deletes its `.meta` file with it.
