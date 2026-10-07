# Report naming slimming: branch characterization

Date: 2026-10-06. Branch: `fix/poiyomi-refusal-reporting`, base `main` at
`96b5705`. The branch holds no commits beyond `main`. The review target
was the uncommitted working tree at review time on 2026-10-06. The tree
held 14 changed files with 374 inserted lines and 44 deleted lines.

Labels: `[SOURCE]` is a fact read in this repository. `[INFERENCE]` is
a deduction. `[RECOMMENDATION]` is a proposed next step.

Privacy note. This record names only repository files and symbols. It
holds no avatar, material, or renderer names, no machine paths, and no
instance identifiers.

## 1. Purpose

The branch owner asked for a parallelized ponytail review of the
branch. The ponytail-review method looks for over-engineering only.
Correctness, security, and performance are out of scope. This note
records what the review found. It changes nothing.

## 2. How the review ran

`[SOURCE]` The diff covers one feature surface: the refusal report
naming work. Three independent reviewers ran three read-only slices:
the production reporting files, the Host and Semantics production
files, and the test files. One reviewer ran each slice in parallel.

`[SOURCE]` After the reviewers reported, the integration pass checked
each finding against the working tree on 2026-10-06:

- Read the full diff of all 14 files.
- Located every production caller of the two changed emitters with a
  repository search.
- Compared the new `rendererName` parameter against the sibling
  emitters `SlotAnalysisRefusal` and `SlotSeparationRefusal`.
- Checked the repository for switch expressions and for `[TestCase]`
  use, to judge idiom claims.

Every finding below survived those checks. One extra cut candidate
did not survive: the integration pass dropped a proposal to rewrite
the `ClosureFailureSentence` switch statement as a switch expression.
The repository uses no switch expressions anywhere. A conversion
would introduce a new idiom, and the saved lines were small. Section
7 records the checks in full.

## 3. Findings

Each finding names one cut and its replacement. The five findings
together allow about 40 fewer lines with no behavior change. The
line counts are conservative. Each count states its arithmetic.

### 3.1 Texture capture: a five-line declare-then-if block

`[SOURCE]` `UnityMaterialEvidenceCapture.cs` lines 1441 to 1445
derive `formatName` with a declaration, an `if`, and an assignment.
The same file derives the same null-or-value shape as a ternary at
four other places, among them lines 735, 764, 1288, and 1309.

`[RECOMMENDATION]` Replace the block with the file's own idiom:

```csharp
var formatName = texture is Texture2D texture2D
    ? texture2D.format.ToString()
    : null;
```

The derived value is identical. 2 lines go away: 5 deleted, 3 added.

### 3.2 Renderer refusal: a renderer name parameter nobody sets

`[SOURCE]` `AmuseReports.RendererRefusal` gains a
`string rendererName = null` parameter at line 388, a four-line
comment at lines 391 to 394, and a fallback reassignment at lines
395 to 396. The fallback computes
`renderer.gameObject.name` when the caller passes no name.

`[SOURCE]` The production callers are
`AmusePlatformFinishPlugin.cs` line 576, which passes no name, and
line 789, which passes `renderer.gameObject.name`. The renderer is
non-null at line 789. The explicit pass is byte-for-byte the value
the fallback computes when the parameter is absent. One test, the method at `AmuseReportsRendererTests.cs` line 100,
passes the literal `"Body"` at line 109.

`[RECOMMENDATION]` Delete the parameter, the comment block, the
reassignment, and the caller argument at line 789. Pass the guard
inline where the report call names its `{2}` slot:

```csharp
renderer != null ? renderer.gameObject.name : null,
```

The emitted text is unchanged. The test at line 100 drops the
explicit name argument and asserts the fixture renderer's own name.
The sibling emitters keep their pre-existing parameters. Those are
outside this diff, so this record does not propose cuts for them.
8 production lines go away: 8 deleted, 0 added. The test edit is
neutral.

### 3.3 Slot tests: one refusal construction pasted three times

`[SOURCE]` `AmuseReportsSlotTests.cs`
`TextureEntriesNeverClaimWhereTrianglesLanded` at line 136 builds
three 8-line `TextureCaptureRefusal` constructions inside an array
literal at lines 139 to 164. The three differ only in the `Reason`
argument. The test body already runs one `foreach` over the array.

`[RECOMMENDATION]` Replace the array of refusals with an array of
the three reasons. Build one refusal inside the loop, with the
reason as the fifth argument and the same `formatName` and
`textureName` arguments:

```csharp
var reasons = new[]
{
    TextureCaptureRefusalReason.UnavailableCapture,
    TextureCaptureRefusalReason.NonResidentMips,
    TextureCaptureRefusalReason.UnsupportedFormat,
};
foreach (var reason in reasons)
{
    var refusal = new TextureCaptureRefusal(
        "_MainTex", true, default,
        TextureChannel.Alpha, reason,
        formatName: "DXT1Crunched",
        textureName: "synthetic atlas copy");
    // ... asserts unchanged ...
}
```

The case count stays at three. Every assert keeps its text. About
13 lines go away.

### 3.4 String tests: seven assert blocks with one shape

`[SOURCE]` `AmuseReportStringsTests.cs`
`RefusalNamingFixKeysExist` at line 182 carries seven 4-line
`Assert.That(AmuseReportStrings.Has(key), Is.True)` blocks at lines
189 to 218. The blocks differ only in the literal key. The same file
drives its per-cause coverage with loops at lines 19 to 40.

`[RECOMMENDATION]` Keep every literal and the rationale comment.
Move the seven keys into one array and assert in a loop, with the
key as the assert message so a failure still names the key:

```csharp
var keys = new[] { /* the seven literal keys, one per line */ };
foreach (var key in keys)
{
    Assert.That(AmuseReportStrings.Has(key), Is.True, key);
}
```

About 11 lines go away: 28 deleted, 17 added.

### 3.5 Alpha mask tests: one assert pasted three times

`[SOURCE]` `PoiyomiAlphaMaskTests.cs`
`AlphaMaskPropertiesCarryFeatureLabels` at line 941 repeats one
4-line assert three times for three property names. The repository
uses `[TestCase]` rows in more than 34 test files for exactly this
shape.

`[RECOMMENDATION]` One parameterized body with three rows:

```csharp
[TestCase("_AlphaMaskValue")]
[TestCase("_AlphaMaskBlendStrength")]
[TestCase("_AlphaMaskInvert")]
public void AlphaMaskPropertiesCarryFeatureLabels(string propertyName)
{
    Assert.That(
        PoiyomiMaterialSemantics.FeatureLabelFor(propertyName),
        Is.EqualTo("Alpha mask"));
}
```

The case count rises from 1 to 3, because each row now reports on
its own. The covered behavior is identical. About 8 lines go away.

## 4. Slices with no findings

`[SOURCE]` The reviewers checked these and recorded no finding:

- `.github/workflows/pr.yml` and `README.md`. Comment and prose
  updates only. Both document real behavior and the branch-protection
  gate.
- `AmuseReportStrings.cs`. In-place string rewrites in the mandated
  central table. No orphaned keys. The new `{5}` and `{6}` template
  positions collide with no pre-existing template.
- `AlphaSeparationPreparation.cs`. Three minimal lines that track the
  offending material for the report.
- `TextureCaptureRefusal.cs`. The new `FormatName` and `TextureName`
  fields have a production consumer in `AmuseReports.cs` lines 344
  to 349. The optional constructor defaults are the minimal form.
- `PoiyomiMaterialSemantics.cs`. The three new label rows are one
  dictionary line per key, which is the minimal form.
- The `string.IsNullOrEmpty(detail)` coercion in `AmuseReports.cs`.
  It is load-bearing. A null substitution renders as the literal
  `<missing>` in the report text, per the pre-existing emitter
  comment.
- `ClosureFailureSentence`. A closed-enum-to-sentence map. One form
  per refusal way. Its falsifier-numbered test coverage is mandated
  policy.
- The falsifier guards and fixture boilerplate in
  `ConversionRefusedSlotNamesItsMaterial`. Mandated policy. The test
  reuses the file's existing fixture members.

## 5. Out-of-scope observations

No correctness, security, or performance question surfaced in this
pass. The method excludes them, and none was recorded.

## 6. Relation to other records

`[SOURCE]` The reviewed diff implements the branch's own dated pair:
`specs/2026-10-06-poiyomi-refusal-reporting-design.md` and
`plans/2026-10-06-poiyomi-refusal-reporting-plan.md`. This review is
a follow-on pass on that work's output. It proposes no change to any
contract that pair records.

`[SOURCE]` The repository holds two earlier complexity passes:
`2026-10-05-complexity-review-*` and
`2026-10-06-alpha-separator-complexity-review-*`. No cut in this
record overlaps a cut in either earlier record. Each record speaks
for its own date.

## 7. Verification record

`[SOURCE]` On 2026-10-06 the three reviewer reports were checked
against the working tree. The checks did the following:

- Located both production callers of `RendererRefusal` and read the
  call arguments at `AmusePlatformFinishPlugin.cs` lines 576 and 789.
- Compared the new parameter against the sibling emitters at
  `AmuseReports.cs` lines 43, 214, 287, and 311.
- Searched the package for switch expressions and for `[TestCase]`
  use to weigh the idiom claims in findings 3.4 and 3.5.
- Located all five named test methods and counted their repeated
  blocks.

`[SOURCE]` The checks changed the report in three ways:

- The reviewer estimate for finding 3.2 was about 6 lines. The
  arithmetic in section 3.2 makes it 8. This note states the
  arithmetic.
- The integration pass added the dropped switch-expression proposal
  to section 2, with the reason for the drop.
- The sibling-emitter note in section 3.2 records that the
  pre-existing parameters of `SlotAnalysisRefusal` and
  `SlotSeparationRefusal` stay outside this diff's scope.

`[SOURCE]` On 2026-10-06 an adversarial pass re-checked this note,
the design note, and the plan against the working tree. It found and
fixed these defects:

- The emitter comment block is four lines at 391 to 394, not five.
  The finding 3.2 arithmetic moved from 9 to 8 lines.
- The plan's caller-search step said the renderer test file holds
  three calls. It holds four.
- The plan's full-suite gate compared the assembly total against the
  sum of the five filter baselines. The filters do not cover the
  whole assembly. The plan now records a plan-level assembly
  baseline in its first task.
- Four cited ranges in the plan named wrong end lines. The ranges
  now match the tree.
- The plan claimed three test tasks rely on its first task. The
  tasks are independent. The plan now states that.

`[RECOMMENDATION]` The findings stay unapplied. A design note and a
plan note can turn them into cuts, in the same shape as the
2026-10-06 alpha-separator complexity review pair under `specs/` and
`plans/`.
