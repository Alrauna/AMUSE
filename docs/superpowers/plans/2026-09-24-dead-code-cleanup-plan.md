# Dead-code cleanup implementation plan

Date: 2026-09-24. Executes the design
`docs/superpowers/specs/2026-09-24-dead-code-cleanup-design.md` from the
audit `docs/superpowers/investigations/2026-09-24-ponytail-dead-code-audit.md`.

Branch: `chore/0.1.0-pre.3-prerelease-audit`, base `main` at `cf7bef1`. One
branch carries every task. No commits, no staging, no push. The controller
runs Unity in the dev editor instance only. No slice needs the Census Lab
project.

Unity MCP discipline: before any tool use against an editor instance,
enumerate reachable instances read-only, inspect `Application.dataPath`,
and require the exact match `<repo-root>/Assets`. Stop on any mismatch.

Every deleted `.cs` file deletes its `.meta` file in the same task. Run
`git diff --check` before reporting.

## Task 0: baseline

1. Refresh Unity. Confirm the console holds no compile errors.
2. Run the full EditMode population of both assemblies,
   `Alrauna.Amuse.Tests.Editor` and
   `Alrauna.Amuse.Research.Tests.Editor`. Record the counts. Attribute
   every failure against the known environment failures documented in the
   2026-09-24 handover plan record. Any new failure stops the run.

No production file changes in this task.

## Task 1: census retired-name pin, then refusal removal

Write first, observe, then delete:

1. RED-class guard, observed as characterization: extend
   `CensusVocabularyTests.RendererRefusalMirrorsAmuse` with one assertion
   that the census snapshot contains `LockedPoiyomiThryUnattested`, in the
   style of the existing `MaterialPropertyOverridesPresent` assertion.
   Run the focused filter. It passes on the tree of 2026-09-24. Record it as
   characterization with its date. A plausible wrong implementation that
   also deletes the mirror member fails this assertion.
2. Delete the product member and its doc block from
   `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`.
   Reword the enum header doc: the census mirror holds a name snapshot and
   may retain retired names. Drop the declaration-order claim.
3. Delete the three string entries and the retention comment from
   `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`.
4. Delete the one switch arm from `CensusVocabulary.ToCensus` in
   `Packages/com.alrauna.amuse.research/Editor/Collection/CensusVocabulary.cs`.
   Keep the throwing default arm unchanged.
5. Mark the retained mirror member in
   `Packages/com.alrauna.amuse.research/Editor/Census/CensusCategories.cs`
   with a doc line: retired from the product on 2026-09-24, retained so
   recorded census data keeps its category.
6. Refresh Unity. Confirm both assemblies compile.
7. GREEN: run the focused filters `CensusCategorySnapshotTests`,
   `CensusVocabularyTests`, `AmuseReportStringsTests`. Record the counts.
   Zero-test filters are failures.

Proof beyond the filters: the removal cannot fail a test by design beyond
these pins, because no build path produced the member since 2026-09-24.

## Task 2: digest relocation and availability cleanup

1. Create the data-only record in the research package:
   `Packages/com.alrauna.amuse.research/Editor/Collection/ThryLockToolDigestRecord.cs`
   with its `.meta` file. Two internal constants in era order, with the
   provenance doc from design D2, including the no-consumer statement and
   the deletion rule. No methods, no AssetDatabase use. Copy
   the two values from the product table byte for byte.
2. Delete from `TransientUnlockAvailability.cs`: `PinnedSourceDigests`,
   `ThrySourceAttested`, `ComputeSourceAttested`, `SourceDigestOfFile`,
   `ClearCache`, `sourceAttestedCache`, `OptimizerScriptName`. Drop the
   now unused usings, including `System.Security.Cryptography`.
3. Rewrite the class doc per design D2.
4. Delete the two tests `ThePinnedDigestTableCarriesTheTwoRecordedToolDigests`
   and `SourceDigestIsTheSha256OfTheFileBytes` from
   `TransientUnlockAvailabilityTests.cs`. Remove the `ClearCache` calls
   from its `SetUp` and `TearDown`. Keep
   `TheProductionRestoreReconstructsTheUnlockedForm`.
5. Refresh Unity. Confirm the ban filter
   `ResearchSourceApiBanTests` passes, since the new record is data only.
6. GREEN: run `TransientUnlockAvailabilityTests` and
   `ResearchSourceApiBanTests`. Record the counts.

Plain green run: the deleted block has no production consumer, so no test
can fail by design.

## Task 3: stand-in shrink

1. Rewrite
   `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockVendorStandIn.cs`
   per design D3: keep the type name, the two vendor-shaped signatures,
   the counters, and `Reset`. Bodies become counter increments returning
   true. Delete the `Mode` enum, `CurrentMode`, the dictionary, the three
   shader helpers, and the string decoy. Rewrite the class doc.
2. Refresh Unity. Confirm compilation. The `Reset` callers in
   `TransientUnlockAvailabilityTests` and any lifecycle `SetUp` stay valid.
3. GREEN: run the filters `TransientUnlockTransformationTests`,
   `TransientUnlockWindowCloseTests`, `TransientUnlockSwapInTests`, and
   the `AmusePlatformFinishPluginTests` play path cases that assert the
   counters. Record the counts.

Plain green run: no test drives the deleted modes, so no test can fail by
design. The zero-count assertions prove the tripwire still arms.

## Task 4: truthful words and names on the restore path

1. Rename `TransientUnlockVendorOutcome` to
   `TransientUnlockRestoreOutcome` and `TransientUnlockDelegate` to
   `TransientUnlockRestoreDelegate` across
   `TransientUnlockAvailability.cs`, `TransientUnlockSwapIn.cs`, and
   `TransientUnlockAvailabilityTests.cs`. Ten references.
2. Rewrite the three doc blocks per design D4: the outcome doc, the
   delegate doc, and the `InvertReferences` doc in
   `TransientUnlockWindowClose.cs`, dropping the false cross-pair
   sentence.
3. Reword the `BaseMaterialSemanticsProvider` comment in
   `UnityRendererAlphaAnalysis.cs` per design D5. Drop the word
   temporarily. Name the live research consumer.
4. Rename `CaptureGraphForTests` to `CaptureGraphThroughSeams` in
   `UnityAnimationEvidenceCapture.cs` and its three call sites:
   `AmusePlatformFinishPlugin.cs`, `AmusePlatformFinishPluginTests.cs`,
   and `UnityAnimationEvidenceCaptureTests.cs`.
   Rewrite its doc per design D7.
5. Refresh Unity. Confirm compilation.
6. GREEN: run `TransientUnlockAvailabilityTests`,
   `UnityAnimationEvidenceCaptureTests`, and
   `CollectorSeamCountingTests`. Record the counts.

## Task 5: alpha field evidence instance relocation

1. Create the fixture adapter under
   `Packages/com.alrauna.amuse/Tests/Editor/Host/` with its `.meta` file,
   per design D6. Move the constructor loop, the dictionary, and
   `TryGetAlphaField` verbatim from the production file, so the
   null-entry skip, the duplicate-identity first-wins rule, and the
   malformed-argument throws stay byte for byte. The loop calls the
   short static `UnityAlphaFieldEvidence.TryCapture` overload.
2. Move the instance surface out of
   `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs`:
   both constructors, `TryGetAlphaField`, and the instance dictionary.
   Reword the class doc to describe the static capture engine. Add the
   finding F9 doc sentence to each of the three short `TryCapture`
   overloads: the friend test assemblies are the consumer, and the
   production route is the full form with explicit policy.
3. Update `UnityAlphaFieldEvidenceTests` and
   `AlphaEvidenceClassifierIntegrationTests` to construct the fixture.
   Keep every existing assertion, including the destroyed-texture and
   mutated-texture characterization. These guard the relocation: a fixture
   that re-reads a texture on query fails them.
4. Refresh Unity. Confirm compilation.
5. GREEN: run `UnityAlphaFieldEvidenceTests` and
   `AlphaEvidenceClassifierIntegrationTests`. Record the counts.

The transferred characterization is the wrong-implementation guard. No new
assertions.

## Task 6: smoke test deletion

1. Delete `Packages/com.alrauna.amuse/Tests/Editor/TestInfrastructureSmokeTests.cs`
   and its `.meta` file.
2. GREEN: full suite in Task 7 shows the product assembly count lower by
   exactly one.

## Task 7: full validation and records

1. Run the full EditMode population of both assemblies. Expected: the
   Task 0 counts, minus one product test from Task 6, plus one
   characterization assertion inside an existing research test, plus the
   relocated fixture tests under their new class name. Every failure
   attributed against the known environment failures. Any other failure
   stops the work.
2. `git diff --check`.
3. Inspect the full diff. Confirm: no research file ships into the
   product package, no `.meta` orphan remains for any deleted file, and
   no asset or manifest changed.
4. Identifier sweep over every changed file: an at sign joined to a
   hexadecimal hash, drive-letter paths, home-directory paths, four-digit
   ports, and every private asset name from the session. Zero hits.
5. Record the observed counts and the sweep result in this plan.

## Stop conditions

The design's stop conditions apply. In addition, stop when the full suite
shows any failure not on the known environment list, or when the diff
contains any file outside the plan's target list.

## Expected report

The changed files per task, the observed focused and full counts with
dates, the characterization record from Task 1, the diff check result, the
identifier sweep result, and the remaining limits. State that no upload
ran, no Unity instance outside the dev editor instance was touched, and
nothing was staged or committed.

## Results, 2026-09-24

Executed on branch `chore/0.1.0-pre.3-prerelease-audit`, working tree
only, in the dev editor instance after data path verification. Every task
ran with a fresh implementer subagent and a task review. All reviews
returned spec pass and quality approved. Two minor findings were recorded
and resolved: a misstated line count in a scratch report, and a subsumed
doc sentence on one capture overload.

Observed counts:

- Task 0 baseline: 2372 of 2379 completed, exactly the 5 known
  environment failures.
- Task 1 characterization: CensusVocabularyTests 10 of 10 before the
  deletion. Task 1 GREEN: 20 of 20 across the three named filters.
- Task 2 GREEN: 3 of 3. Suite total 2379 to 2377, the two deleted digest
  tests.
- Task 3 GREEN: 97 of 97 across the four named filters.
- Task 4 GREEN: 86 of 86 across the three named filters. Observed rename
  count: 13 restore-path references and 4 capture entry point sites. The
  plan text said ten references; 13 is the observed truth.
- Tasks 5 and 6 GREEN: 119 of 119 across the two named filters, including
  the transferred destroyed-texture and mutated-texture characterization.
  Suite total 2377 to 2376, the deleted smoke test.
- Task 7 full population: 2369 of 2376 completed, exactly the same 5
  known environment failures, no new failure.

Diff check: clean. Nineteen tracked files changed, 112 insertions and 618
deletions, plus two new source files with editor generated .meta files and
the smoke test pair deleted together. No manifest, asset, or shader
changed. The research package gained the data only digest record and
nothing else.

Identifier sweep over every addition: zero hits for the banned patterns.
The only four digit token outside known constants was a diff hunk header.

Limits: no upload ran. No Unity instance outside the dev editor instance
was touched. Nothing was staged or committed. The five environment
failures are pre existing and unrelated to this cleanup.
