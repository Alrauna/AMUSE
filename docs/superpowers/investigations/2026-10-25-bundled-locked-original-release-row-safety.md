# Release row safety record for the bundled locked original admission

Status as of 2026-10-27. This is the stage 3 record of the bundled locked original admission design (Y8, stage 3). It carries the safety argument for the two admitted 9.3.64 release rows. The record pins no product change. Tasks 1 to 7 of the plan landed the aligner rules. This task added the era twins and this record.

Privacy note: this record covers AMUSE source, AMUSE tests, AMUSE records, and the synthetic shader texts of the test suite. Every fixture name in the tests is synthetic. No private avatar, renderer, material, or asset name appears. No machine name, user name, port, instance name, or digest value appears. All paths are repository relative.

Evidence labels:

- `[SOURCE]` A fact read in a file, with the path.
- `[MEASURED]` A fact observed in a run for this record on 2026-10-27, with the source of the count.
- `[INFERENCE]` A conclusion from cited facts that no run checked.

## 1. The Y8 argument

The aligner runs for locked materials whose original passes the identity gate. As of 2026-10-25 those are the two 9.3.64 release rows. Stage 7 of this plan adds the two bundled 9.3 rows beside them after its gates hold.

`[SOURCE]` The aligner knows three vendor eras: the ThryEditor bundled with 9.3.64, thryeditor 2.74.0 to 2.74.2, and thryeditor 2.74.3 (A2.2 of the alignment design). The code names them `Bundled9364`, `Thry274To2742`, and `Thry2743` (`Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiLockEra.cs`). The release rows carry the bundled era.

`[SOURCE]` The 9.3.64 release shader is not installed in the dev project. The readiness record of 2026-10-25 states this in its section 8. No release lock can run in this repository.

Owner decision A3b, 2026-10-25: release package testing is dropped. The bundled 9.3 shader that ships with Poiyomi 10 is the primary support path. The product keeps the two 9.3.64 release rows. The owner removed release package testing, not release row support. No release package test exists anywhere in this design.

So the safety of the two release rows rests permanently on the synthetic era tests of the A11 pattern. The basis is Y8 item 2 of the design: the new rules key on consumption and text, not on era. A synthetic pair that drives a rule under all three eras exercises that rule in the release era too. The release era is `Bundled9364`, and each era twin names it first in its test case list.

## 2. The rule deltas and why none can turn an aligned pair into a false positive

The new rules change behavior in exactly the ways their decisions state. `[SOURCE]` The decisions are Y2, Y3, Y5, Y6, and Y7 of the design. Each delta and its safety direction:

| Decision | Delta | Why the delta cannot admit a wrong pair |
|---|---|---|
| Y2 catch-up probe | Fires only where the old code refused undecided. The old tie loop decided a rival outcome only on a candidate death, which is a line mismatch, and refused when both candidates died. `Unobservable` stands untouched. | M1 records only what a grounding walk forced. The probe death is a line mismatch, which is text evidence. Catch-up and end of file refuse undecided. |
| Y3 zero-and-zero | Fires only in the tie path where both trials consumed zero generated lines and the old code refused undecided. `Unobservable` stands untouched. | M2 records a body that produced no generated text. Its strict check refuses unless a vendor relock of the clone drops the same body. A false clause refuses. |
| Y5 digit-led runs | Widens acceptance for digit-led runs. | Y5 only drops runs the original holds as whole tokens or that start with a digit. Every renamed form starts with a letter or an underscore, so the drop cannot hide a rename artifact. Every other run stays an artifact and refuses when it carries no declared name. |
| Y6 earliest boundary | Narrows the split set. One artifact yields candidates only from the earliest position where a declared name ends and an underscore follows. | Wrong suffixes fail at the walk, never at acceptance. The renamed Properties line is the load-bearing pin. A wrong suffix mismatches there before any program line can be misattributed. |
| Y7 Properties window | Widens the Properties window past braces inside strings and comments. It also narrows the window for a commented-out Properties block, because those lines no longer count. Both directions fail closed. | Y7 can only add artifacts from true Properties lines. A line that stops counting can only lose a candidate, and a lost candidate refuses. A window that closes late adds artifacts, and the walk still compares every line. |

`[SOURCE]` The Y7 delta sentence above restates the two behavior deltas that the shared helper of Task 7 introduced, with their pins: a characterization pin for a gamma mark inside a quoted display string, and accepted-shape pins for an escaped quote and an unclosed string.

`[MEASURED]` The aligner refusal sentence set did not change. The aligner holds 15 refusal sentences on 2026-10-27, and the version before the first task of this plan held the same 15. The plan added no sentence. The new rules refuse through the existing sentences: the tie rules through the undecided sentence, the strict condition check through the existing condition sentence, and the split rules through the ambiguous suffix sentence.

## 3. The falsifiers ran under all three eras

Six parameterized tests in the alignment test class rebuild the falsifier pairs of Tasks 1 to 7 with no era specific form, that is no sampler collateral and no negative wrap. Each test carries one case per era and drives `AlignForEra` (`Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs`).

| Test | Falsifiers | Assertion | Cases | Observed on 2026-10-27 |
|---|---|---|---|---|
| TheDuplicatedBodyTieRefusesInEveryEra | 1 and 9 | The undecided refusal, with a null source | 3 | All passed |
| TheZeroAndZeroWrapperResolvesInEveryEra | 2, 3, and 11 record halves | The wrapper `Removed` with `ZeroAndZero == true` and the guard `NotEvaluated` | 3 | All passed |
| TheDigitLedLiteralsDoNotBlockTheSuffixInEveryEra | 6 and 12 | Success with the suffix `_S` | 3 | All passed |
| TheEarliestHeadBoundarySuffixDerivesInEveryEra | 4 and 5 | The earliest boundary suffix `_Cutoff_X` | 3 | All passed |
| TheBracedDisplayStringWindowYieldsTheArtifactInEveryEra | 7 | Success with the renamed artifact `_W_S` | 3 | All passed |
| TheCommentedDirectiveNeverOpensABlockInEveryEra | 8 | Success with the right nesting, two blocks | 3 | All passed |

`[MEASURED]` Source of the observed result: the filtered `run_tests` job of 2026-10-27, EditMode, class filter `Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiLockedSourceAlignmentTests`. The job report summary gave 89 total, 89 passed, 0 failed, 0 skipped, result state `Passed`. The class held 71 leaves after Task 7, so the 89 leaves are the 71 prior leaves plus the 18 new era cases. The job progress counter is not a leaf count, so this record takes the counts from the job report summary only.

The 18 era cases passed on their first run. There is no production change in this task, so they are characterization twins, not RED first tests. They pin the all-era behavior that Tasks 1 to 7 built, and each comment names the no-op guard in the all-era tests that proves the fixture reaches the rule.

## 4. Refusal sentence inventory (A11.1)

`[SOURCE]` The aligner holds 15 refusal sentences as private constants in `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs`: LineMismatch, UnconsumedLine, TwoLiterals, MixedUses, SuffixAmbiguous, InlinedMarker, Include, OutsideSet, MissingPass, EnumWithoutBake, EraDisagreement, BlockAlignment, BlockTie, BlockUnclosed, and OptimizerMissing.

`[MEASURED]` Checked on 2026-10-27 against the test tree. Nine sentences keep at least one refusal test that asserts the exact sentence in the alignment test class:

| Sentence | Refusal tests that assert it |
|---|---|
| LineMismatch | 5 tests |
| UnconsumedLine | 1 test |
| TwoLiterals | 2 tests |
| MixedUses | 1 test |
| SuffixAmbiguous | 3 tests |
| InlinedMarker | 1 test |
| Include | 1 test |
| BlockTie | 4 all-era tests plus the 3 cases of the duplicated body era twin |
| OptimizerMissing | 1 test |

`[MEASURED]` The other six sentences (OutsideSet, MissingPass, EnumWithoutBake, EraDisagreement, BlockAlignment, BlockUnclosed) carry no exact-sentence refusal assertion anywhere in the test tree. That state predates this plan. The plan changed no sentence and removed no test, so every retained sentence keeps the coverage it had. This is a recorded gap of the older suite, not a delta of this plan.

`[MEASURED]` The new rules add no sentence. See section 2.

## 5. Suite runs of 2026-10-27

All runs ran in the dev editor instance. The instance check ran first: `Application.dataPath` matched the Assets folder of this repository before every write and run. No other agent drove Unity during the runs. Counts come from the recorder method of the plan (the temporary recorder script), never from the job progress counter.

| Run | Counts | Source |
|---|---|---|
| Class filter, alignment test class | 89 total, 89 passed, 0 failed, 0 skipped | Filtered `run_tests` job report summary |
| Full product assembly, `Alrauna.Amuse.Tests.Editor` | 3123 total, 3118 passed, 0 failed, 5 skipped named vendor gate skips | Recorder footer counts of the product output file, leaf rows deduplicated |
| Full research assembly, `Alrauna.Amuse.Research.Tests.Editor` | 151 total, 151 passed, 0 failed, 0 skipped | Recorder footer counts of the research output file, leaf rows deduplicated |

The previous recorded full run of this plan, taken on 2026-10-27 after the second task, reported product 3085 total, 3080 passed, 0 failed, and 5 skipped named vendor gate skips, and research 151 of 151. Tasks 3 to 7 added tests since that run. The product assembly holds 38 more leaves on 2026-10-27, which is the difference between 3085 and 3123. The research assembly count is unchanged.

The 5 product skips are the named opt-in gate skips of the vendor test designs. Their full names in the product output file are the two merged soundness gates, the real lock characterization gate, and the two vendor production order gates. They are skips, never failures.

## 6. Limits

- The release shader is not installed in the dev project. Nothing in this record runs against release bytes. The synthetic era coverage is the whole argument, and it is permanent by design.
- The six structural sentences without exact-sentence refusal tests predate this plan. A later suite change can close that gap.
- The A10.4 curve rewrite stays a recorded follow-up of the design. This plan does not touch it.
- The temporary recorder script and its output files are transient. They were removed before the commit of this record.
