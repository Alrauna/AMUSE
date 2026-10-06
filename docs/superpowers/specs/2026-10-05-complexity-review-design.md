# Complexity Review Design

Date: 2026-10-05. Base: `main` at `f319186`, branch
`feature/optimizer-ui-rework`, review point `0891286`.

Investigation:
`docs/superpowers/investigations/2026-10-05-complexity-review-investigation.md`.

## 1. Overview

The branch keeps its behavior and loses about 95 lines. The design
applies the eight cuts from the investigation. Every cut is
behavior-preserving. No component state changes and no build behavior
changes. The deferred behavior observation in investigation section 6
stays out of scope.

## 2. PresetParser: one generic reader

`TryParse` loses the dead `refusal = PresetLoadRefusal.None` before its
success return. The top-of-method assignment stays.

`TryReadObject`, `TryReadInt`, and `TryReadBool` are deleted.
`TryRead<T>` replaces them:

```csharp
private static bool TryRead<T>(
    JObject obj,
    string key,
    JTokenType expected,
    ref PresetLoadRefusal refusal,
    out T value)
```

The body keeps the shared shape: assign `MissingField` when the key is
absent, assign `WrongValueType` when the token type differs, and
convert with `token.Value<T>()`. `TryReadNonEmptyString` calls
`TryRead<string>` with `JTokenType.String` and keeps its own empty
check, because the empty check is the only part that differs. Call
sites read `TryRead<JObject>`, `TryRead<int>`, and `TryRead<bool>`.

The refusal value list is unchanged. The JSON schema is unchanged.
Every branch that a parser test exercises today keeps its refusal
value.

## 3. PresetFileStore: visibility only

`FileNames` becomes `private`. No signature, value, or caller changes.

## 4. OptimizerEditor: header and guard cuts

`DrawHeader` holds the subtext literal in place:

```csharp
// A proper product name, so the plain-English sentence
// rules do not rewrite it.
EditorGUILayout.LabelField(
    "Alrauna's Material Understanding and Simplification Engine",
    subtextStyle);
```

This supersedes section 3 of the title-subtext design from this date.
The literal is the same text. The one-line comment keeps the plain
-English rationale the doc comment carried. A wording change stays a
one-line edit.

`DrawPresetRow` guards with the refusal comparison only:

```csharp
if (_presetProblem != PresetLoadRefusal.None)
```

The lazy load above the guard is unchanged.

## 5. PresetParserTests: one refusal helper

A private static helper absorbs the repeated pair:

```csharp
private static void Refuses(string json, PresetLoadRefusal expected)
{
    Assert.That(PresetParser.TryParse(
        json, out _, out var refusal), Is.False);
    Assert.That(refusal, Is.EqualTo(expected));
}
```

The fifteen refusal tests call `Refuses` with their JSON and their
expected value. `ValidSchemaParsesAllNineValues`,
`AcceptedEdgeValuesParseSuccessfully`, and `PercentAboveHundredRefuses`
keep their bodies. `PercentAboveHundredRefuses` joins the helper
because its assert pair is identical. The five range tests reuse
`WithMipCap` and `WithMinTextureSize` instead of hand-rolled `Replace`
calls. Test names and refusal expectations are unchanged, so a failure
still names the exact refused case.

## 6. AaoMergedConsumptionTests: two direct asserts

The switched-off test asserts the clone list directly:

```csharp
Assert.That(
    state.Separation?.CreatedClones,
    Is.Null.Or.Empty,
    "a switched-off run must not create generated materials");
```

The `HashSet<Material>` wrapper goes away. The test then captures the
fixture return value:

```csharp
var renderer = CreateSkinnedRenderer(root);
```

and asserts `renderer.sharedMaterials` on that one renderer. The
`GetComponentsInChildren` sweep and the null-mesh guard go away. The
assert message text is kept verbatim, because it states the contract.

## 7. Test strategy

No new tests. The cut is behavior-preserving, so the guard is the
existing suite. The parser tests are the load-bearing net for section
2: they exercise every refusal branch. The plan records the EditMode
test count for each touched fixture before and after its cut. A count
change or a failure stops the work.

The inspector header has no EditMode coverage. The plan adds a smoke
check on the dev editor instance for section 4, the same pattern the
title-subtext and quality-preset plans used.

## 8. Documentation

None. The README does not describe the parser internals, the editor
header, or these tests. The three dated records for this review are
the documentation.
