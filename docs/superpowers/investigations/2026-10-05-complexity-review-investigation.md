# Branch complexity review: current-state characterization

Date: 2026-10-05. Branch: `feature/optimizer-ui-rework`, base `main` at
`f319186`, review point `0891286`.

Labels: `[SOURCE]` is a fact read in this repository. `[INFERENCE]` is
a deduction. `[RECOMMENDATION]` is a proposed next step.

## 1. Purpose

The branch owner asked for a parallelized complexity review of all code
changes on the branch. The review used the ponytail-review rules. That
method looks for over-engineering only. Correctness, security, and
performance are out of scope. This note records what the review found.
It changes nothing.

A second review outcome is a behavior observation. Section 6 records
it. The observation is out of scope for this review and for the design
that follows.

## 2. How the review ran

`[SOURCE]` The branch diff against `main` at `f319186` touches 46 files
with about 4.4 thousand inserted lines. About 2.8 thousand of those
lines are dated records under `docs/superpowers/`. The review covered
the remaining C# and JSON changes in five read-only slices: the
inspector and runtime component, the preset core types, the preset
parser, the build wiring, and the tests. Each slice was reviewed by one
independent reviewer. The reviewer results were then checked against
the working tree before this note recorded them.

`[SOURCE]` The review point for every line number in this note is
`0891286`.

## 3. Findings

Each finding names one cut and its replacement. The eight findings
together allow about 95 fewer lines with no behavior change.

### 3.1 PresetParser: dead refusal reset

`[SOURCE]` `TryParse` sets `refusal` to `PresetLoadRefusal.None` at the
top. Every failure path assigns a named cause and returns false. The
success path then assigns `None` again right before it returns true.
The second assignment can never change the value. One line goes away.
Nothing replaces it.

### 3.2 PresetParser: four typed readers repeat one shape

`[SOURCE]` `TryReadObject`, `TryReadInt`, `TryReadNonEmptyString`, and
`TryReadBool` each carry the same two-step refusal block. Each checks
field presence and then token type, and assigns `MissingField` or
`WrongValueType` on the way out. About 80 lines repeat one shape.

`[RECOMMENDATION]` One generic reader replaces three of the four:

```csharp
private static bool TryRead<T>(
    JObject obj,
    string key,
    JTokenType expected,
    ref PresetLoadRefusal refusal,
    out T value)
```

`TryReadNonEmptyString` becomes a `TryRead<string>` call plus its
existing empty check. All parser branches stay exercised by the
existing tests.

### 3.3 PresetFileStore: unused visibility

`[SOURCE]` `FileNames` is `internal`. A repository search finds its
only reader inside `PresetFileStore` itself. The parser tests name the
three files as literals. `internal` is flexibility nobody uses. The
field becomes `private`. No lines change.

### 3.4 OptimizerEditor: single-caller constant

`[SOURCE]` `ProductSubtext` is an `internal const` with a doc comment
block. One call site in `DrawHeader` reads it. A repository search
finds no other reader and no test reader. The title-subtext design from
the same date chose a named constant so a wording change stays one
edit. That reason is speculative. The literal inside `DrawHeader` is
also a one-edit wording change. The constant block and its doc comment
go away, and the call site holds the literal with a one-line comment
about the plain-English rule exemption.

### 3.5 OptimizerEditor: dead null disjunct

`[SOURCE]` `DrawPresetRow` lazy-loads the presets and then guards with
`_presets == null || _presetProblem != PresetLoadRefusal.None`.
`TryLoadAll` assigns a fresh list to the out parameter in its first
statement, so after the lazy-load the list is never null. The first
disjunct can never decide the branch. The guard keeps only the refusal
comparison. One line goes away.

### 3.6 PresetParserTests: fifteen copies of one assert pair

`[SOURCE]` Fifteen refusal tests repeat the same two assertions: the
parse returns false and the refusal equals one expected value. The file
also holds `WithMipCap` and `WithMinTextureSize` helpers that wrap the
same `Replace` calls that three texture-size tests hand-roll.

`[RECOMMENDATION]` One helper absorbs the pair:

```csharp
private static void Refuses(string json, PresetLoadRefusal expected)
```

Every refusal test body becomes one line. The five range tests reuse
the two existing helpers. About 70 lines go away.

### 3.7 AaoMergedConsumptionTests: set wrapper around an empty check

`[SOURCE]` The switched-off test builds a `HashSet<Material>` from
`state.Separation?.CreatedClones` only to assert that the set is empty.
Deduplication proves nothing here. A direct
`Is.Null.Or.Empty` assert on the list is shorter. One line goes away.

### 3.8 AaoMergedConsumptionTests: sweep rediscovers one renderer

`[SOURCE]` The same test sweeps the whole hierarchy with
`GetComponentsInChildren` and guards a null mesh, to reach the one
renderer that the fixture method `CreateSkinnedRenderer` already
returns. The test already asserts `AnalyzedRendererCount` is zero.
Capturing the fixture return value and asserting its
`sharedMaterials` directly says the same thing in fewer lines. About
five lines go away.

## 4. Slices with no findings

`[SOURCE]` The build-wiring slice holds `AmusePlatformFinishPlugin`,
the assembly definition, `package.json`, the lockfile, `README.md`, and
the metadata files. No finding. The lockfile change matches an
intentional dependency edit. The uncommitted `pr.yml` change is a
comment-only edit. The dated records under `docs/superpowers/` were
outside review scope by the method's terms.

## 5. Conflict with the title-subtext design

`[SOURCE]` The title-subtext design from this date names the constant
as a deliberate choice. Finding 3.4 cuts it anyway. The two records
disagree on purpose: the design wanted a named anchor for a future
edit. The review treats that future as speculation with one reader.
The design that follows this note resolves the conflict in favor of
the cut.

## 6. Deferred behavior observation

`[SOURCE]` `DrawPresetRow` computes its pressed state from a one-shot
lazy load. The load also caches failure. After a broken preset file is
fixed, the row stays hidden until the inspector reloads. The help box
text implies that fixing the file is enough. This is a behavior
question, not a complexity question. The review records it and does
not address it. A later behavior pass owns it.

## 7. Verification record

`[SOURCE]` On 2026-10-05 the five reviewer reports were checked against
the working tree at `0891286`. The checks read the named files, ran a
repository search for the two visibility findings, read the parser
refusal flow end to end, and counted the parser test pattern: 15
refusal assertions across 17 tests in
`PresetParserTests`. Every line citation in section 3 matched the
working tree. No claim needed a correction.

`[RECOMMENDATION]` The design at
`docs/superpowers/specs/2026-10-05-complexity-review-design.md` turns
the eight findings into cuts. The plan at
`docs/superpowers/plans/2026-10-05-complexity-review-plan.md` orders
them.
