# Bundled locked original admission design

Status: 2026-10-25, third revision. The first revision had two adversarial
reviews: one for soundness, reporting 1 CRITICAL, 2 MAJOR, and 5 MINOR
findings, and one for evidence and buildability, reporting 0 CRITICAL,
4 MAJOR, and 8 MINOR findings. Both returned "accept with fixes". The second
revision integrated every finding. A scoped soundness re-review of the
second revision on the same date returned "accept" with 1 MINOR finding,
which that revision also integrates (decision Y5). This third revision
answers the owner's second round on 2026-10-25. It closes the private scene
suffix split question with a Census Lab console record (A3a). It drops
release package testing and keeps the two release rows (A3b). It treats
thryeditor 2.74.x patch releases as compatible (A3c). A third-round review
pair on 2026-10-25 returned "accept with fixes". The soundness review
reported 0 CRITICAL, 3 MAJOR, and 2 MINOR findings. The evidence review
reported 0 CRITICAL, 1 MAJOR, and 1 MINOR findings. This revision
integrates every finding. Basis: the readiness
investigation
`docs/superpowers/investigations/2026-10-25-bundled-locked-original-readiness-investigation.md`
(this text calls it "the record"). The record decides nothing. This design
decides. The owner answered five open questions on 2026-10-25. The owner
answered three more questions in a second round on the same date. Every
owner answer is recorded as a short neutral statement. This design amends
the locked source
alignment design
`docs/superpowers/specs/2026-10-23-locked-source-alignment-design.md`
(decisions A1 to A12) and the per-shader attestation design
`docs/superpowers/specs/2026-10-23-poiyomi-per-shader-attestation-design.md`
(decision K8).

Privacy note: this record covers AMUSE source, earlier AMUSE records, and the
public vendor packages installed in the dev project. It contains no private
data. Vendor roots use the placeholders `<poiyomi-toon>` for the installed
Poiyomi Toon 10.0.27 package and `<thryeditor>` for the installed thryeditor
2.74.3 package. AMUSE paths are relative to `Packages/com.alrauna.amuse/`
unless they start with `docs/` or `Packages/`.

Decision-ID note: earlier designs burn the letters A, B, C, D, E, F, G, H, K,
L, M, P, R, S, T, V, and X in code or in specs. The same-day sibling design
`docs/superpowers/specs/2026-10-25-vendor-integrated-tests-design.md` labels
its wrong implementations W1 to W10 in its test strategy. To keep code
comments unambiguous, this document uses the Y namespace for its decisions.
No Y decision exists in code or docs as of 2026-10-25.

## Intent

AMUSE refuses every locked Poiyomi material whose original shader is one of
the two bundled 9.3 rows. The rows carry `LockedOriginalAdmitted = false`
(`Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:122-133`, decision K8.1
of the per-shader attestation design). The record shows that the flag is not
the only blocker. Two aligner defects block every measured real lock
(record section 4.1):

1. The block tie. All six measured locks refused with "A block outcome stays
   undecided." The tie has two shapes, M1 and M2, and both are general
   (record section 4.2).
2. The suffix refusal. Two triggers, J and N, can empty or split the suffix
   intersection (record section 4.3). The Properties window brace count has a
   third defect that hides renamed Properties lines after source line 55 of
   the bundled Toon file.

This design fixes the two defects as classes, gates the flag change on
measured real locks, and then admits the two bundled rows. It refuses
whatever it cannot prove. A false positive stays impossible by design. A
false negative is a coverage loss.

## Owner decisions, 2026-10-25

The owner answered the five questions of record section 9. Each answer below
is a short neutral statement, never a quotation.

1. Block tie shape (record question 1). Owner decision Q1, 2026-10-25: use
   the shape that is simplest to maintain over time and that settles every
   case.
2. Suffix shape (record question 2). Owner decision Q2, 2026-10-25: J1 is
   the pick, because it is the safest.
3. Time budget (record question 3). Owner decision Q3, 2026-10-25: no time
   limit applies. Slow code stays unacceptable.
4. Real locks in tests (record question 4). Owner decision Q4, 2026-10-25:
   real locks may run in tests.
5. Flag timing (record question 5). Owner decision Q5, 2026-10-25: this
   design picks the timing, and the pick must be safe.

The owner answered a second round of three questions on 2026-10-25. A3a
asks which suffix trigger produced the private scene refusals. Owner item 1
records that decision. A3b asks when to run a lock test against the 9.3.64
release shader. Owner item 2 records that decision. A3c asks about
thryeditor versions newer than 2.74.3. Owner item 3 records that decision.

The owner set the direction for questions 1, 2, 3, and 4. The concrete rules
below are design calls of this document. The owner delegated question 5 to
this design. The second round answers shape Owner items 1 to 3, Y8, and Y9.

## Goals

1. Admit locked materials whose original is one of the two bundled 9.3 rows,
   under the same reconstruction contract as the two 9.3.64 release rows.
2. Fix the block tie for M1 and M2 as classes, never per site.
3. Fix suffix trigger J and trigger N, and the Properties window defect.
4. Keep every refusal of the current aligner that the fixes do not address.
5. Gate the flag change on a measured real full swap-in of each bundled row.

## Non-goals

- True Poiyomi 10.0 shader support. No admitted row exists for a 10.0 name,
  so those materials stay refused (K8 and the attestation design non-goals).
- The other five bundled 9.3 variants (World, Grab Pass, Outline Early,
  Lil Fur, Lil Fur Two Pass). No row exists for them.
- A curve rewrite for animations that a locked material reads under another
  name. A10.4 stays a recorded follow-up.
- Restoring stripped textures. The 2026-10-23 fidelity record accepts the
  gap.
- A time based refusal anywhere in the aligner.

## Anti-goals

- A decision a tie rule makes because a budget ran out. Every cap in this
  design is an evidence bound.
- Trusting a material tag, a material name, or a defaults table for a fact
  the generated text records.
- Weakening an existing valid test to make the flag change pass.

## Constraints

- The aligner input stays exactly the original text, the generated text, and
  the original property table
  (`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:185-188, 271-300`).
  The live values of the locked material never enter the aligner. This is
  A3.1 and it does not change.
- The aligner stays pure C# with no Unity object access.
- Fail closed. Unknown refuses. A refusal is a coverage loss, never a false
  positive.
- The restore checks of A6 and the seventh fact of A8.3 stay the soundness
  net for every record the aligner writes.
- Simple English in every human-read text. No machine paths, no GUID or
  digest values, no instance names.

## The block tie invariant

Record section 7 states the invariant. This design uses it as the decision
rule:

A block resolution may record an outcome only when one of these holds.

1. The generated text forces the outcome. The rival hypothesis is inconsistent
   with the generated text, proven by a walk that grounds it.
2. The two hypotheses produce the same generated text, so the live alpha lines
   are identical, and the recorded outcome is the one whose A6 checks state
   what the locked material renders. When those checks cannot hold on the
   reconstructed clone, the build refuses.

No outcome may rest on a budget, a heuristic about lock-time values, or a
vendor rule the text does not show.

## Approaches

### Block tie

| Shape | Idea | Trade-off |
|---|---|---|
| T1 with its repeat check | When nothing survives between the block end and the enclosing end, decide kept if the kept trial consumed text, unless that text could also match at the removed position | Decides M1 only. M2 still refuses. The repeat check needs a precise shape or it under-refuses. |
| T2, lazy hypotheses past the enclosing end | Keep both candidates alive past the boundary and decide at the first contradiction | Decides M1 only. The two M2 candidates sit at the same positions and never contradict, so T2 refuses them at its cap. It walks past the boundary, which breaks the GrowthBoundary ownership of the enclosing block, and it multiplies lookahead cost on every tie. |
| T3, vendor unit rule | Treat a wrapper whose body is one guard as one unit with two outcomes | The unit assumption is not a vendor rule. A kept wrapper with a removed guard matches the unit removed outcome, so a wrong record passes and A6 forces wrong values. Rejected on soundness. |
| T4, status quo | Keep refusing | No gain. Every measured lock refuses. |
| Chosen: consumption classes | Classify a tie by the generated lines each trial consumed. One side at zero gets a catch-up probe. Both sides at zero adopt the removed trial without a lookahead. Both sides nonzero keeps refusing | Decides M1 and M2 as classes. Adds no lookahead on the M2 path. The rules key on consumption, not on vendor era, keyword, or site. Design call by this document under owner decision Q1, 2026-10-25. |

### Suffix

| Shape | Fixes | Trade-off |
|---|---|---|
| J1, compare the full maximal run | Trigger J | Owner decision Q2, 2026-10-25. The adopted form drops artifacts that the original holds as whole tokens, plus digit-led runs, see Y5. |
| A, split only at the earliest closed vendor head shape | Trigger N, tie and straddle shapes | One split point per artifact. The rename grammar is closed, so the head anchor is a vendor fact, not a guess. Design call. |
| B, split only at a token boundary | Trigger N, tie shape only | Leaves the straddle shape refused. |
| C, use the suffix tag as a selector | Trigger N when the tag exists | Needs an A1.1 amendment, a new aligner input channel, and a change to the pinned test that ignores a disagreeing tag
  (`Tests/Editor/Build/LockedMaterialReconstructionTests.cs:219-223`). More
  moving parts for no measured gain. Rejected as harder to maintain. |

### Flag timing

| Option | Trade-off |
|---|---|
| Flip the flag with the aligner fixes | No measured real swap-in. The value side of the restore checks has never run on a real aligned bundled pair (record section 4.4). |
| Flip after the record section 6 order | Chosen. The flag flips only after the aligner fixes, the Y8 release row safety argument, the measured real swap-in, and the window and report tests. Design call under owner decision Q5, 2026-10-25. |

## Decisions

### Y1. The record section 6 order is the stage order

The stages of this design follow record section 6 one for one. Documentation
cutover, which the record folds into its step 7, stays inside stage 7. A
stage starts only when the previous stage's gate holds.
The flag change is the last code change.

### Y2. Block tie, M1 rule: the catch-up probe

At a tie, that is, both hypotheses alive and the body holding at least one
surviving line
(`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:982-985, 1000-1057`),
the aligner classifies the tie by the generated lines each trial consumed.
The consumed count is the generated index delta across the trial.

1. Exactly one trial consumed zero generated lines. In the measured shapes
   the removed trial consumed zero because it skipped the kept body, while
   the kept trial consumed the block text. The zero trial must catch up:
   walk it forward on its own clone until one of three ends. It dies at a
   line it cannot match. It catches up, that is, its generated index reaches
   the rival's generated index. It reaches the end of the original file.
   Death and catch-up happen inside the generated region the rival already
   consumed, so the probe never needs to decide anything past that region.
   The end of the file is the formal cap that proves no later text could
   ground the lagging world. The probe differs from T2, which this design
   rejects. The probe is a consistency check on one candidate with a known
   target. T2 would walk two undecided candidates past the boundary and
   hand the enclosing block's post-block survival to an inner decision.
   The probe bypasses the lookahead window counter and caps at the end of
   the original file (Y10).
2. Death before catch-up means the rival world is the only consistent one.
   The generated text forces the outcome. Record the rival outcome. For the
   measured M1 shape that outcome is `Kept`.
3. Catch-up or end of file means both worlds stay consistent. The text does
   not force the outcome. Refuse with the existing sentence "A block outcome
   stays undecided." at the block start
   (`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:84-85, 1053-1057`).

The catch-up cap is the end of the original file. It is an evidence bound:
past the end of the file no text can ground either world. It is never a time
or step budget (Y10).

Rationale. The kept trial consumed the block text at exactly the block
position. The removed trial skipped that region without consuming it, so its
generated index lags. Its lag is reproducible only if the original lines after
the block produce the same text, which is the duplicated body case of record
section 7. The probe tests exactly that. When the rival dies, the text forced
the decision, which satisfies invariant arm 1. When it catches up, the
duplicated body makes both worlds real, and the design refuses, because the
two worlds can map the same generated lines to different original lines and
so to different alpha content.

Case-e nesting. Under this rule the M2 wrapper no longer runs a lookahead at
all (Y3), so the measured case-e shape, an M1 tie reported through an M2
wrapper lookahead, dissolves. A nested tie can still fire inside a catch-up
walk. That nested refusal propagates as undecided and the material refuses.
This design accepts that refusal. It is the unforced corner of invariant
arm 2.

### Y3. Block tie, M2 rule: zero-and-zero adopts the removed trial under a strict check

Scope. This rule fires only in the tie path. The tie path is the branch the
code reaches when both candidates are alive and the body holds at least one
surviving line
(`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:982-985`). When
no body line survives, the existing `Unobservable` outcome stands unchanged,
with no tie and no check
(`:1061-1064`). A test pins a comment-only wrapper as `Unobservable` with no
A6.2 demand, so no revision of this rule can silently reclassify that branch.

The shape. Both trials consumed zero generated lines in the tie path, so the
generated text is identical under both outcomes. The block markers of an
`//ifex` wrapper are comments that never consume generated text, so this
shape needs a wrapper whose observable body content is gone. A kept
`#ifdef` or `#ifndef` trial always consumes its directive line, so the
zero-and-zero shape implies an `//ifex` wrapper. A test asserts that
implication.

The rule: adopt the removed trial. Record the wrapper `Removed` and every
nested block `NotEvaluated`, which is exactly what the removed trial already
holds through `RecordSkippedBlocks`
(`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:1090-1127`). The
lookahead does not run. No candidate walks past the wrapper. The record
carries a marker that names it a zero-and-zero record.

The strict check. A zero-and-zero record may not use any check whose
justification reads "the removal shows that the vendor saw it true". Here
the removal is the claim the record makes, not evidence for it. A6.2 as
written
(`docs/superpowers/specs/2026-10-23-locked-source-alignment-design.md`)
counts every `isNotAnimated` clause true, and the shipped evaluator does the
same without reading the operand
(`Editor/Semantics/Poiyomi/LockedSourceReproduction.cs:435-441`,
`Editor/Semantics/Poiyomi/PoiyomiLockCondition.cs:146-150`). For a
zero-and-zero record that amendment of A6.2 does not apply. The evaluator
must run in a strict mode: comparisons evaluate against the values of the
clone `U`, and an `isNotAnimated(P)` clause reads the real `Animated` tag
of `P` on the material. Any clause that is false under the vendor grammar
refuses the material.

Why the strict check is the soundness proof. The `Removed` record claims
that the wrapper body does not render on `L`. The generated text proves
that for `L`. The check must prove the same for the admitted output. Fact 9
of the alignment design says the vendor drops the whole body of an excluded
wrapper and never evaluates a block inside a removed block. So when the
wrapper condition is true on `U` under the full grammar, including the real
`Animated` tags, a vendor relock of `U` drops the same body, nested blocks
included. `U` renders the same live lines as `L`. When any clause is false
on `U`, the relock keeps the body and the build refuses. The seventh fact
re-runs A6 from the memoized facts, so `CheckConsistency` must select the
strict mode from the record's zero-and-zero marker
(`Editor/Build/TransientUnlockSwapIn.cs:382-404`).

Why the nested records stay `NotEvaluated`. The alternative record, wrapper
`Removed` with the kept trial's nested records adopted, fires A6.1 on the
measured case e. It would demand `VRC_LIGHT_VOLUMES_INCLUDED` in the
keywords of `U`, a keyword that never sits in a `#pragma shader_feature`
line, and so re-create the refusal this decision exists to remove. The
strict wrapper check makes nested records moot: a true wrapper condition
drops the entire body at relock, whatever the nested blocks are.

Remaining corner. When the lock kept the wrapper and an inner removal emptied
the body, the strict check refuses whenever the condition is not true on
`U` under the real tags. That is fail closed. The refusal is a coverage
loss in a corner this design cannot distinguish from the text.

Invariant arm 2 covers this decision. Both hypotheses consume nothing, so
the alpha proof reads the same generated text either way. The `Removed`
record passes the restore checks only when a vendor relock of `U` provably
drops the same body, which is the second arm's "renders the same live
lines" condition.

Why not evaluate the wrapper condition at the declared defaults inside the
aligner. The defaults are not lock-time values. A defaults gate would refuse
pairs whose real lock values made the wrapper removed while the defaults
point the other way, and A6.2 already evaluates the clone's true values. The
gate adds an aligner input use, a second evaluator, and a worse refusal
sentence for no soundness gain. Simpler is safer here.

### Y4. Both-nonzero ties keep refusing, and the extent scan learns block comments

A tie where both trials consumed at least one generated line keeps the
refusal this code makes as of 2026-10-25. No measured lock produced this
shape. Deciding it has no forcing
evidence in general, because both worlds consumed real text at different
roles. Refusal is unchanged and sound.

The block extent finder counts directive lines with no block comment tracking,
while the walk and the skipped-body recorder drop block comments first
(`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:797-845` against
`:1086-1143`). The bundled Toon file holds 20 directive shaped lines inside
block comments and the Two Pass file holds 32 (record section 4.2). No
measured refusal shows a wrong extent as of 2026-10-25. This design still
fixes the finder: it tracks `/*` to `*/` spans the same way the walk does.
A commented directive shaped line never opens a block in the generated text,
because the vendor deletes block comments wholesale. The fix is behavior
preserving wherever the old count was right.

### Y5. Suffix trigger J: collect the full maximal run, drop digit-led runs

`CollectIdentifiers` starts a run only at a letter or an underscore
(`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:2365-2388`), so a
literal such as `0x7ffffff` yields the artifact `x7ffffff`, the whole-token
filter
(`:1453-1468`) never finds that form in the original, and the empty candidate
set empties the intersection
(`:371-397`). The owner chose J1, owner decision Q2, 2026-10-25: collect the full maximal run,
so the collected form is `0x7ffffff`.

Collection alone does not finish the fix. The generated text holds the baked
literal of every non-animated property, and a non-default value such as
`0.975` can carry a digit run that the original holds nowhere as a whole
token. A whole-token drop rule would refuse every material with such a value,
which regresses the admitted release rows. The rule is therefore:

1. A run starts at any word character, letter, digit, or underscore.
2. A collected run drops when either check holds. The original holds it as a
   whole token. Or the run starts with a digit. This covers every numeric
   form that the vendor prints (fact 5 of the alignment design), with or
   without the ` /*<name>*/` developer prefix and the 2.74 negative wrap. It
   also covers exponent fragments such as the run `1E` of a baked `1E-06`.
3. Every other collected run stays an artifact. An artifact with no declared
   name inside still yields an empty candidate set and refuses, fail closed.

A digit-led run can never carry a rename. Every renamed form starts with
the property name, an underscore, or the `sampler` collateral, and each of
these starts with a letter or an underscore. So the digit-led drop cannot
hide a rename artifact. Design call, after the re-review of 2026-10-25: the
second revision dropped only runs that parse as vendor numeric literals. That
left exponent fragments refusing. The digit-led rule is simpler and closes
the class with the same safety argument. Properties names and bracket names
keep their parsers. They do not go through the run scanner.

### Y6. Suffix trigger N: split only at the earliest vendor head boundary

Trigger N exists because `SuffixCandidates` splits an artifact at every
occurrence of every longest declared name
(`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:483-519`). When the
cleaned material name holds a declared property name, the extra split points
tie or straddle the intersection (record section 4.3). The vendor rename
grammar is closed (fact 7 of the alignment design and
`<thryeditor>/Editor/ShaderOptimizer.cs:1380-1391`): every renamed form is
the renamed head, an underscore, and the cleaned name, with the head drawn
from the property, its `sampler` collateral of the 2.74.3 era (a measured
fact of the 2026-10-25 record), and the derived `_ST` and `_TexelSize`
tails. A non-empty prefix before the head never comes from the vendor rename
of that property.

The rule: one artifact yields candidates only from the earliest position where
a declared original name ends and an underscore follows. Later positions give
nothing. The `_ST` and `_TexelSize` tail strip stays
(`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:529-551`).

Measured table outcomes under this rule (record section 4.3):

| Material name | Renamed set | Old result | New result |
|---|---|---|---|
| `Body` | `_Color` | `_Body` | `_Body`, unchanged |
| `Body_Color_1` | `_Color` | tie refusal | `_Body_Color_1` |
| `Body_Matcap_1` | `_Cutoff` | tie refusal | `_Body_Matcap_1` |
| `Cut_Cutoff_On` | `_Color`, `_EmissionColor` | straddle refusal | `_Cut_Cutoff_On` |
| `Cutoff_X` | `_Color` | wrong `_X`, walk refuses | `_Cutoff_X` |
| `Body` | `_MainTex` | `_Body` | `_Body`, unchanged |

The underscore-after requirement kills prefix extensions such as `_Color`
inside `_ColorLong`. An artifact whose earliest boundary is still not the
vendor head, for example a collateral identifier that carries an earlier
declared name inside its own prefix, gives a wrong suffix. A wrong single
suffix fails later at the walk and cannot give a false positive. The
load-bearing pin is the renamed Properties line. Every renamed property
keeps a renamed Properties line in the generated text (fact 7), and the
Properties matcher compares that line under the derived suffix, so a wrong
suffix mismatches there before any program line can be misattributed (record
section 4.3). Sound, and simpler than the longest-name scan it replaces.

### Y7. Properties window: count braces outside strings and comments

`PropertiesLineIndices` counts `{` and `}` over whole lines
(`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:2333-2363`).
Unbalanced braces in display strings drive the count negative at source line
55 of the bundled Toon file, so only lines 5 to 54 count as Properties lines,
and a renamed Properties line after line 54 gives no artifact (record section
6, step 2). The fix: the counter skips the contents of double-quoted strings,
with backslash escapes, of block comments, and of `//` line comments. The
line-comment skip mirrors the walk, which drops every `//` line wholesale
(`:614-618`). The review measured that the bundled Toon Properties window
holds no true line comment with a brace as of 2026-10-25, and its seven
`//` hits are web address text inside display strings, which the string
skip already covers. The helper still closes the window at the line that
returns the depth to zero.

One shared helper serves `PropertiesLineIndices` and the inline copy in
`GammaPropertyNames`
(`:152-183`), and the duplicate loop goes. Two behavior deltas come with the
shared helper, and both need pins:

1. A `[Gamma]` mark inside a quoted display string stops counting. That
   matches Unity parsing. Skipping string contents can only remove gamma
   marks, and the vendor bakes with the real Unity flags, so the direction
   is safe. A characterization test pins it.
2. An escaped quote and an unclosed string are accepted shapes with defined
   behavior, not refusals. Tests pin both so a later change cannot flip them
   silently.

### Y8. Safety for the two admitted release rows

The two 9.3.64 release rows run the aligner, and their era-synthetic pairs
align in the suite, as of 2026-10-25. The release shader is not installed
in the dev project (record
section 8), so the safety argument rests on tests, per the A11 pattern. The
owner removed release package testing (A3b, owner decision, 2026-10-25). This
safety argument rests on the synthetic era tests permanently. No release
package test exists anywhere in this design. Design call: the product keeps
the two 9.3.64 release rows. The owner removed release package testing, not
release row support. The bundled 9.3 shader that ships with Poiyomi 10 is
the primary support path.

1. Every existing aligner test passes unchanged. The new rules change
   behavior in exactly three ways, each stated in its decision. Y2 and Y3
   fire only where the old code refused undecided, with `Unobservable`
   untouched. Y5 widens acceptance for collected runs that start with a
   digit, a set the old code refused on. Y6 and Y7
   narrow the split set and widen the Properties window, in the ways their
   decisions state.
2. The M1, M2, J, N, and window falsifier pairs of this design run under each
   era of A2.2, release era included, because the rules key on consumption
   and text, not on era.
3. The rules never turn an aligned pair into a false positive. M1 records
   only what a grounding walk forced. M2 records a body that produced no
   generated text, and its strict check refuses unless a vendor relock of
   `U` drops the same body. Y5 only drops collected runs that the original
   holds or that start with a digit. Y6 changes which suffix
   is derived, and a wrong suffix fails at the walk, never at acceptance.
   Y7 widens the Properties window, which can only add artifacts.
4. A release lock whose pair refused undecided before the fix may now align.
   That is a coverage gain on the same contract. Its A6 and seventh-fact
   checks are unchanged.

### Y9. Gated real-lock acceptance tests in the dev project

The owner approved real Thry locks in the dev project (owner decision Q4, 2026-10-25). The
record section 4.6 proves they run and clean up.

1. Gate. One environment variable guards the suite. The gate is an explicit
   check followed by `Assert.Ignore` with a named reason, as the sibling
   design V1 requires
   (`docs/superpowers/specs/2026-10-25-vendor-integrated-tests-design.md`).
   The check reads the variable and compares its value. An unmet gate
   reports as a named skip, never as inconclusive. The sibling design V9.3
   converted the older `Assume.That` gate pattern of
   `Tests/Editor/Build/DaoMergedConsumptionTests.cs:104-117` for exactly
   this reason, after a 2026-10-24 run reported gated tests as
   inconclusive. The gate message names what the tests do and where they
   may run.
2. Vendor presence. The suite resolves the real vendor lock entry by
   reflection over the loaded assemblies, selecting by assembly so the test
   stand-in
   `Tests/Editor/Build/TransientUnlockVendorStandIn.cs:15-40` cannot answer.
   The sibling design's stage 5 deletes that stand-in, and reflection by
   assembly keeps this suite working after that deletion. When the
   thryeditor package is absent, or Poiyomi is absent, the suite skips with a
   named reason, in the pattern of the installed-package skips
   (`Tests/Editor/Build/DaoMergedConsumptionTests.cs:119-127`). The sibling
   vendor test design makes non-NDMF vendors optional with named skips, and
   this suite follows it.
3. Shader load. The suite resolves each bundled shader asset by its pinned
   row GUID through the AssetDatabase, never by name lookup, because a 10.x
   install answers a release name with the 10.0 shader (record section 4.1).
   A missing bundled asset skips with a named reason.
4. Cases. The suite locks and drives the production swap-in path for the
   record section 4.1 shapes on both rows: all defaults, one renamed
   property, two renamed properties, a material name with spaces and symbols,
   light volumes on and off, Two Pass defaults, and Two Pass renamed. It adds
   one case per row with AudioLink enabled, so the generated text keeps
   digit-led literals and exercises Y5 on real text. The default-texture
   limits of record section 8 apply and are stated in the test comments.
5. Assertions. Each case asserts the identity verdict, the aligner verdict,
   the clone, the restore, the seventh fact, and the consistency check
   outcome, and asserts that no slot refusal other than a named expected one
   fired. The suite writes per-phase elapsed times to the test output, so a
   dated record can cite observed numbers.
6. Cleanup order. In a finally block: delete the materials the suite created
   first, then delete the cache entries under `Assets/_LockedShaderCache/`
   that appeared during the test. The suite snapshots that folder before the
   first lock and asserts the folder set after cleanup equals the snapshot.
   It never deletes an entry it did not create, because the vendor documents
   that a deleted entry breaks other locked materials
   (`<thryeditor>/Editor/LockedShaderCache.cs:23-38`).
7. Network disclosure. The suite file header and the gate message disclose
   that the vendor lock calls `Helper.RegisterEditorUse`
   (`<thryeditor>/Editor/ShaderOptimizer.cs:910`), which sends one user count
   and one project count to vendor web endpoints once per editor session,
   unless the editor preferences already record the counts
   (`<thryeditor>/Editor/Helpers/Helper.cs:50-70`, endpoints in
   `<thryeditor>/Editor/DataStructs/Constants.cs:21-22`).
8. These tests never run in the Census Lab project, per the repository test
   rules. The gate message states it.
9. Vendor version. The suite has no version gate. It runs against whatever
   thryeditor is installed. The owner treats 2.74.x patch releases as
   compatible (A3c, owner decision, 2026-10-25). Era facts that this design
   cites stay measured facts of the installed 2.74.3 on 2026-10-25. A 2.75
   or later bump is outside that assumption. Design call: the suite still
   runs on a 2.75 or later bump, and it never skips for a version reason.
   A3c says tests must catch a breaking change. A run either aligns
   or fails, so a run is what catches the break. A version skip would hide
   the break. Reconciliation with the sibling design: in the canonical dev
   project, a 2.75 or later thryeditor bump fails the vendor suite-start
   check of sibling V1 obligation 4 first. That named failure is the
   intended loud signal. Y9 item 9 keeps this suite free of version skips.
   Both rules fail loudly. Neither passes silently.

### Y10. Performance obligations

The owner set no time limit (owner decision Q3, 2026-10-25). The obligations:

1. No step of the aligner or the swap-in refuses a material on elapsed time
   or on a step count. The only caps are evidence bounds. Y2 states the one
   new cap, the end of the original file.
2. Full walks are measured. Stage 4 records, per distinct generated shader,
   the align time of a full successful walk, the clone and restore time, and
   the dry-run align time. The numbers land in a dated investigation record
   that the row comment cites, in the K5.6 pattern.
3. Avoidable cost stays out. The M2 rule removes a lookahead walk. The M1
   catch-up runs only on ties. Neither adds per-line work. The memo keeps its
   A8.4 keying and lifetime
   (`Editor/Build/LockedSourceAlignmentMemo.cs:12-23, 41-43`), and the build
   reset keeps clearing it, because stale facts are a correctness risk. The
   design accepts that each build and each dry run pay the align cost again
   and records that cost in stage 4 instead of widening the cache.
4. The measured per-lock costs of record section 4.5, 26 to 31 seconds for
   fresh Toon locks and 52 to 53 seconds for fresh Two Pass locks as early
   exits on 2026-10-25, are the baseline. Full successful walks cost more,
   and stage 4 bounds them by measurement, not by assumption.

### Y11. Report wording for a locked material that attests and then fails to align

Such a material passes `LockedMaterialIdentity.AttestsLockedOriginal`, opens
a pair, and refuses in the restore path with the aligner's fixed detail
sentence. The slot refusal stays `TransientUnlockRestoreMismatch`. Its
strings
(`Editor/Build/AmuseReportStrings.cs:720-729`) gain one description sentence
and a hint clause, in simple English: the material is locked and its original
shader is verified. The locked shader text did not match what AMUSE can
reconstruct. The slot keeps its original locked material. The reason field
carries the aligner sentence, which A8.2 already prefers over the generic
sentence. No new refusal enum value is added. The existing dry-run mirror
note behavior of A8.5 stands.

### Y12. Window eligibility and consent behavior for bundled locked avatars

`WindowEligibleForConsent` answers true when any locked material attests
(`Editor/Build/TransientUnlockAvailability.cs:52-78`), and an eligible build
opens the window without a per-build consent subject
(`Editor/Build/AmusePlatformFinishPass.cs:328-338`). The attested check ends
in `row.LockedOriginalAdmitted`
(`Editor/Build/LockedMaterialIdentity.cs:296-300`), so a bundled-row
material is not window eligible until the flag flips. After the flag
change, a bundled locked avatar becomes eligible. Tests pin, before the
flag change:

1. A locked material whose tags name a bundled row, built on the real row
   evidence with no asset write, as record section 4.6 describes, does not
   make `WindowEligibleForConsent` true while the flag is off. This pin
   records the pre-flip negative as characterization.
2. The same avatar keeps its named refusal per slot while the flag is off,
   and the swap-in consent gate stays coupled to the eligibility gate, so no
   second dialog path can appear.

The positive pin moves into the stage 7 change set of Y13, in the
Falsifier 10 pattern. In the same change set as the flag, a bundled-row
locked material makes `WindowEligibleForConsent` true.

### Y13. The flag change, last, with the full surface cutover

The flag on the two bundled rows flips only in stage 7, after stages 1 to 6
hold. The exact gate, all on dated evidence:

1. Stages 1 and 2 GREEN. Every new falsifier observed RED and then GREEN, and
   the full alignment suite passes.
2. Stage 3 holds. The Y8 safety argument is written. Its falsifiers ran
   under all eras and the suite is green.
3. Stage 4 holds. At least one real lock per bundled row aligned, cloned,
   restored, passed the seventh fact and the consistency check, and every
   observed refusal sentence is one this design's rules produce. The times
   are recorded in a dated investigation record.
4. Stage 6 holds. The pre-flip Y12 pins and the Y11 strings landed and
   passed. The positive eligibility pin lands with the flag in this change
   set.

The change set that flips the flag also lands every surface of record
section 5. Tests that pin the old refusal are rewritten to pin the new
decision, never weakened:

- `Tests/Editor/Build/LockedMaterialIdentityTests.cs:197-220` asserts that a
  bundled row refuses as a locked original. It becomes the admission test:
  the bundled row with a matching tag attests. Falsifier 8, the tag
  mismatch refusal, stays as is
  (`:221-239`). The missing-tag tests stay
  (`:240-255`). The K8.3 comment
  (`Editor/Build/LockedMaterialIdentity.cs:269-301`) names this design.
- The eligibility pin flips with the flag in this change set: a bundled-row
  locked material makes `WindowEligibleForConsent` true. Y12 records the
  pre-flip negative as characterization.
- `Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs` pins the
  row set with `LockedOriginalAdmitted` false on the bundled rows at
  `:528, 541, 644-651`. The pin flips to true in the same change set, and the
  K8.4 accepted set of
  `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:200-221` grows
  through the rows with no code change beyond the flag.
- `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs:1770-1793` pins that a
  bundled tag is not deferred. It becomes: a bundled tag defers through the
  name-tag arm while the pair is not yet mirrored, and the locked mirror
  still refuses a pair that cannot align. The K8.4 seam
  (`Editor/DryRun/AlphaSeparationDryRun.cs:1663-1678`) needs no code change.
- The report strings drop the bundled-specific clause in
  `Editor/Build/AmuseReportStrings.cs:174-191`, because the bundled shader is
  no longer a reason a verified original refuses. The refusal stays for the
  other named causes.
- The K8.4 gate comment
  (`Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:200-205`) says the
  accepted set stays the two 9.3.64 release names as of 2026-10-23. Stage 7
  rewrites that sentence to name this design and its date. The rewrite also
  states the primary support path: the bundled 9.3 shader in Poiyomi 10. It
  names no release package test, because none exists (A3b).
- The row evidence comment that stage 7 writes or updates next to the row
  array records this design, its date, and the stage 4 record.
- K8.1 of the per-shader attestation design is amended by this decision.
  After stage 7 all four admitted rows carry `LockedOriginalAdmitted` true.
  The K8.1 refusal reason stands for every locked original whose name no
  admitted row admits, which covers every Poiyomi 10.0 name and every one of
  the five bundled variants. No row exists for those names. K8.2 stays and
  keeps protecting a release lock whose GUID now resolves to the bundled
  file: the tag gate refuses it
  (`Editor/Build/LockedMaterialIdentity.cs:281-288`). A3.7 and A4.2 of the
  alignment design are amended by Y2, Y3, Y5, and Y6. A6.2 is amended for
  zero-and-zero records by Y3. A8.3, and A11 stand unchanged and carry the
  new load.

Post-flip observation, not a gate. After stage 7, the owner runs one
play-mode build of the private avatar in the Census Lab editor instance, as
A3a describes. AMUSE tooling only reads the console of the Census Lab
editor instance after that build, read-only. The console read records the
remaining locked-material refusal sentences as ranges. No AMUSE tooling
enters play mode there, builds there, writes there, or runs tests there.
The observation shows which suffix refusals survive the fixes, if any.
Design call of the third revision on 2026-10-25, added with the A3a
closure.

## Test strategy

Every stage lands RED first. A test that passes on its first run is recorded
as characterization, never dressed up as RED. Falsifiers carry the
`--- Falsifier N: ... ---` marker and a no-op guard, in the house style.

Aligner tests drive the pure `Align` entry on synthetic texts
(`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:185`), and the
per-era falsifier runs drive `AlignForEra`
(`:236`), which `Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs:649-695`
already models. Real
text slices for the measured sites stay in the stage 4 gated suite, never as
committed vendor text.

1. Stage 1 aligner tests, synthetic:
   - M1 pair, kept guard, no duplicate: aligns, record `Kept`.
   - M2 pair, wrapper over one removed guard: aligns, wrapper `Removed`,
     guard `NotEvaluated`.
   - Comment-only wrapper pin: the pair resolves `Unobservable` with no A6.2
     demand, unchanged from the code as of 2026-10-25.
   - Case-e pair, M2 wrapper followed by a kept guard later in the file:
     aligns, both records right.
   - M2 implication test: every zero-and-zero resolution in the suite has an
     `//ifex` wrapper.
   - The commented-directive extent pair of Y4.
   - Per A11.1, one refusal test per retained refusal sentence.
2. Stage 2 aligner tests, synthetic: the Y6 table rows, the J1 literal pair,
   the window pair with its escaped-quote and unclosed-string pins, the
   gamma-mark characterization pin of Y7, and the tail strip interplay for a
   renamed texture.
3. Stage 3: the full alignment suite unchanged, plus each new falsifier run
   under all three eras.
4. Stage 4: the Y9 gated suite.
5. Stage 6: the Y12 and Y11 tests.

Falsifiers, each naming the plausible wrong implementation it kills:

- --- Falsifier 1: a tie rule that decides kept whenever the kept trial
  consumed text, with no catch-up probe. --- Pair: an M1-shaped block whose
  body is duplicated verbatim after the block. The wrong implementation
  records `Kept` while the removed world reproduces the text from the
  duplicate. The correct behavior refuses undecided. Assert the refusal.
- --- Falsifier 2: an M2 resolution that runs the old lookahead or decides
  kept. --- Pair: wrapper over one removed guard, nothing surviving between
  wrapper end and enclosing end. The wrong implementation refuses. Assert
  alignment success with the Y3 record.
- --- Falsifier 3: an M2 record that marks the inner guard `Removed`. ---
  Pair: the light-volume shape, keyword absent from every pragma line. The
  wrong implementation makes A6.1 demand a keyword `U` can never hold, and
  the consistency check refuses. Assert `CheckConsistency` passes on the
  reconstructed values.
- --- Falsifier 4: a suffix split that tries every occurrence of a longest
  declared name, the status quo. --- Case `Body_Color_1` over `_Color` must
  derive `_Body_Color_1`. The wrong implementation refuses as ambiguous.
- --- Falsifier 5: a head rule that anchors anywhere but the earliest
  boundary. --- Case `Cutoff_X` over `_Color` must derive `_Cutoff_X`. The
  wrong implementation derives `_X` and the walk refuses.
- --- Falsifier 6: a run scanner that starts runs only at letters and
  underscores, the status quo. --- A generated text holding `0x7ffffff`
  inside a feature region must align. The wrong implementation empties the
  intersection and refuses.
- --- Falsifier 7: a Properties window that counts braces inside display
  strings, the status quo. --- A Properties block whose display string holds
  an unbalanced `{` before a renamed property line must still yield that
  artifact. The wrong implementation refuses or derives a wrong suffix.
- --- Falsifier 8: an extent finder that counts directive shaped lines
  inside block comments, the status quo. --- A block whose body carries a
  commented `//ifex` shaped line must align with the right nesting. The
  wrong implementation mis-nests and refuses.
- --- Falsifier 9: a catch-up probe capped by a constant step or line count
  instead of the end of the file. --- A duplicate body placed beyond any
  small constant makes the wrong implementation decide kept without
  evidence. The end-of-file cap refuses. This kills any future time bound
  smuggled into the probe.
- --- Falsifier 10: a flag change that skips the stage gates. --- The row
  pin test asserts `LockedOriginalAdmitted` false on both bundled rows until
  stage 7, with a message that names this design and its stage list. Y13
  rewrites it in the same change set as the flag.
- --- Falsifier 11: a zero-and-zero record that reuses the A6.2
  `isNotAnimated` bypass, the plausible kept-wrapper implementation. ---
  Pair: wrapper `//ifex P==0 && isNotAnimated(P)` over one inner
  `//ifex Q==1` whose body holds an alpha-relevant line. `P` and `Q` carry
  `Animated` tag 1. At lock `P` is 0 and `Q` is 1, so the vendor keeps the
  wrapper and removes the inner block. Both wrapper trials consume zero
  lines. The wrong implementation records the wrapper `Removed`, counts
  `isNotAnimated(P)` true, sees `P==0` on `U`, passes the restore, and the
  admitted output renders the inner body after a relock, which `L` never
  rendered. The strict check of Y3 reads the real tag of `P`, finds the
  condition false on `U`, and refuses. Assert the refusal at the condition
  check, on the alignment result and on `CheckConsistency`.
- --- Falsifier 12: a run drop rule that relies on whole-token presence
  only. --- A generated text that bakes a non-default value such as `0.975`,
  whose digit run the original holds nowhere as a whole token, must align.
  A second text that bakes `1E-06` must align too. The wrong implementation
  collects `975` or `1E` as an artifact with an empty candidate set and
  refuses before the walk. The digit-led drop of Y5 removes both runs.
  Assert alignment success for both texts.

## Stage order

1. Block tie fix. Y2, Y3, Y4. The alignment pipeline: the aligner, the
   condition evaluator strict mode of Y3, and the `CheckConsistency`
   selection of Y3. Falsifiers 3 and 11 land here. RED then GREEN. Gate:
   stage 1 tests and the full alignment suite pass.
2. Suffix fix. Y5, Y6, Y7. Aligner only. RED then GREEN. Gate: stage 2 tests
   and the full alignment suite pass.
3. Release row safety. Y8. No production change. Gate: the argument is
   written, the falsifiers ran under all eras, and the suite is green.
4. Real full swap-in measurement. Y9 in the dev project. Gate: one real lock
   per bundled row through clone, restore, seventh fact, and consistency,
   with times and refusal sentences recorded in a dated investigation.
5. Time budget statement. Y10. No refusal cap. Gate: the dated record of
   stage 4 states the measured full walk costs and the dry-run cost.
6. Window and report tests. Y11, Y12. Strings and tests only. The Y12 pins
   record the pre-flip negative. Gate: those pins and the Y11 tests are
   GREEN as characterization.
7. Flag change and cutover. Y13. Gate: the Y13 checklist holds item for
   item, and the full `Alrauna.Amuse.Tests.Editor` and
   `Alrauna.Amuse.Research.Tests.Editor` assemblies pass in the dev editor
   instance with recorded counts.

## Docs to update

In the stage 7 change set: both READMEs, the bundled locked sentence at line
42 of each, the `AGENTS.md` overview line 7, `docs/architecture/vision.md`
line 55, and `Packages/com.alrauna.amuse/CHANGELOG.md` under `[Unreleased]`.
The K8.3 comment, the K8.4 gate comment at
`Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:200-205`, and the row
evidence comment name this design. The A3.7 and A4.2 amendment notes and the
A6.2 zero-and-zero amendment note go into the alignment design as dated
amendments.

## Owner items

Items 1 to 3 are closed. The owner answered them in the second round on
2026-10-25. Item 4 stays open, and only the owner can answer it. No item
blocks stages 1 to 6.

1. Closed on 2026-10-25. The question was the split of the 2026-10-24
   suffix refusals between triggers J and N. The split needs the private
   scene (record section 8). Owner decision A3a, 2026-10-25: the owner ran a
   play mode build in the Census Lab. AMUSE may read the console of that
   build. The design revision must halt when it needs more information. The
   main session read the console
   of the Census Lab editor instance on 2026-10-25, read-only. The console
   holds one play-mode NDMF build of the private avatar. Every
   locked-material refusal in it is the renderer refusal
   `LockedPoiyomiOriginalShaderUnattested`, with the sentence "This renderer
   holds a locked material whose original shader AMUSE has not verified."
   (`Packages/com.alrauna.amuse/Editor/Build/LockedMaterialIdentity.cs:134-161`,
   `Editor/Build/AmuseReportStrings.cs:174-176`). Between 10 and 20
   renderers report it. The refusal fires when the original shader does not
   resolve to a pinned row that admits locked originals, so it fires before
   the aligner runs. The console holds no aligner or suffix output. It
   cannot split the refusals between trigger J and trigger N. Design call:
   the split is not a design input. Y5 and Y6 fix both triggers, so the
   revision did not halt. Remaining suffix refusals become visible in two
   places: the stage 4 real-lock measurement, and a Census Lab build after
   the flag flip. The owner runs that build, as A3a describes. AMUSE
   tooling only reads the console of the Census Lab editor instance after
   the build, read-only. It never enters play mode there, builds there,
   writes there, or runs tests there. Y13 records the observation, never a
   gate.
2. Closed on 2026-10-25. The question was when to run a lock test against
   9.3.64 release shader, which is not installed. Owner decision A3b,
   2026-10-25: release package testing is dropped. The bundled 9.3 shader in
   Poiyomi 10 is the primary support path. No release
   package test exists in this design. The former release gate item of Y8
   is removed. The safety of the release rows rests permanently on the
   synthetic era tests of the A11 pattern. The bundled 9.3 shader in
   Poiyomi 10 is the primary support path. Design call: the product keeps
   the two 9.3.64 release rows. The owner removed release package testing,
   not release row support.
3. Closed on 2026-10-25. The question was thryeditor versions newer than
   2.74.3 (record section 8). Owner decision A3c, 2026-10-25: future 2.74.x
   releases are treated as free of breaking changes for now. No precedent
   beyond that is assumed. Tests must catch a breaking change in an AMUSE
   dependency. Design call: thryeditor
   2.74.x patch releases are treated as compatible. No item pins the exact
   2.74.3 patch as a gate or a skip. Era facts measured on 2.74.3 stay
   measured facts with their date. The gated real-lock suite of Y9 runs
   against the installed thryeditor, and it is what catches a breaking
   change. A bump to 2.75 or later is outside the assumption. Y9 item 9
   decides that case.
4. The A10.4 curve rewrite stays a recorded follow-up of the alignment
   design.

## Risks

1. The tie fix may uncover later refusals in the walk, because every measured
   walk refused in the first tenth of its file (record section 4.5). Stage 4
   surfaces them. Each new refusal is a coverage loss, never a false
   positive, and each must be a sentence this design's rules produce before
   stage 7.
2. The M2 rule refuses the wrapper-kept corner with recorded values that
   contradict the wrapper condition. That is fail closed. The measured locks
   do not sit in that corner.
3. The Y6 head rule can derive a wrong suffix for a collateral identifier
   whose prefix carries an earlier declared name. The walk refuses. No false
   positive is possible.
4. Stage 4 depends on the vendor packages staying pinned in the dev project.
   A version bump invalidates the measurement and reopens stage 4.

## Validation

1. Focused EditMode runs for each new test, observed RED and then GREEN.
2. The full `Alrauna.Amuse.Tests.Editor` and
   `Alrauna.Amuse.Research.Tests.Editor` assemblies in the dev editor
   instance, with recorded counts.
3. The gated Y9 suite only with the opt-in variable set, and never in the
   Census Lab project.
4. No test run asserts anything about a build. No NDMF build runs in the dev
   project for this design. Stage 4 drives the swap-in service directly. The
   Y13 post-flip Census Lab observation is an owner-run build plus a
   read-only console read, never a test run of this design.

Code comments that implement these decisions reference Y1 through Y13 and
point to this document.
