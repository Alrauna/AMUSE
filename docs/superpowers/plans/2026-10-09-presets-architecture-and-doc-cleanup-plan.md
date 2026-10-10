# Presets, Architecture, and Documentation Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove five duplications across Analysis, Build, Semantics, and tests. Apply the layering ruling. Fix two documentation drifts. Make one report switch and one parser path fail closed. Stop the silent rewrites in the inspector and the preset file store.

**Architecture:** One editor assembly, `Alrauna.Amuse.Editor`. Refactors keep one definition per shared rule and preserve behavior. Behavior fixes land test first in the Unity Test Runner, EditMode.

**Tech Stack:** C# (Unity 2022.3), Unity Test Framework (EditMode), NUnit constraint model.

**Spec:** `docs/superpowers/specs/2026-10-09-presets-architecture-and-doc-cleanup-design.md`

## Global Constraints

- Use simple English. Short active sentences. One idea per sentence.
- No semicolons in prose. No contractions. No em-dashes in prose.
- Never record absolute or machine-specific paths. Use repository-relative paths only.
- Never record host names, user account names, home-directory paths, ports, or Unity MCP instance names.
- Date every status claim. As of 2026-10-09.
- Never stage, commit, push, or modify Git history without explicit user authorization.
- The dev editor instance with `Application.dataPath == <repo-root>/Assets` is the only authorized test instance.
- Vendor shaders are never installed. Tests use the `Hidden/Alrauna/AmuseTests/*` stand-ins and the verified seams in `Tests/Editor/Build/`.
- A filtered run that reports 0 tests is a failure. A successful compile is never validation. Record observed counts.
- An assertion that passes on first run is characterization. The plan marks those steps.
- Pair 4 lands before this pair on `TransientUnlockWindowClose.cs` and `TransientUnlockWindowState.cs`. Pair 5 lands before this pair on `PoiyomiOpaqueConversion.cs`. Re-read touched regions before editing.

---

### Task 1: ClosureFailureSentence Fails Closed

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs`

**Interfaces:**
- Consumes: `AmuseReports.ClosureFailureSentence(MaterialDependencyClosureFailure)`
- Produces: `InvalidOperationException` on an unhandled closure failure value, matching the `FeatureSentence` discipline

- [ ] **Step 1: Write the failing test**

In `AmuseReportStringsTests.cs`, add:

```csharp
[Test]
public void UnhandledClosureFailureThrows()
{
    Assert.Throws<InvalidOperationException>(
        () => AmuseReports.ClosureFailureSentence(
            (MaterialDependencyClosureFailure)999));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the new test via Unity Test Runner, EditMode. Verify it fails because the current default arm returns `""` and no exception is thrown.

- [ ] **Step 3: Implement the throw**

In `AmuseReports.cs`, replace the `default` arm of `ClosureFailureSentence`:

```csharp
default:
    throw new InvalidOperationException(
        "MaterialDependencyClosureFailure has an unhandled value. " +
        "Every value needs its own report sentence.");
```

- [ ] **Step 4: Run tests to verify they pass**

Run `AmuseReportStringsTests` via Unity Test Runner, EditMode. Verify all new and existing tests pass and the filtered run reports a nonzero test count.

---

### Task 2: Preset Parser Range-Checks Integer Tokens

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetParserTests.cs`

**Interfaces:**
- Consumes: `PresetParser.TryParse`, `PresetLoadRefusal.ValueOutOfRange`
- Produces: `ValueOutOfRange` refusal instead of an unnamed `OverflowException` for integers outside the `int` range

- [ ] **Step 1: Write the failing test**

The suite already carries the `ValidJson` body, the `WithSettings(find, replace)` helper, and the `Refuses(json, expected)` helper. In `PresetParserTests.cs`, add:

```csharp
[Test]
public void IntegerBeyondIntRangeRefusesValueOutOfRange()
{
    Refuses(WithSettings(
            "\"schemaVersion\": 1",
            "\"schemaVersion\": 3000000000"),
        PresetLoadRefusal.ValueOutOfRange);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the new test via Unity Test Runner, EditMode. Verify it fails with an `OverflowException` escaping `TryParse`, not with a refusal.

- [ ] **Step 3: Implement the range check**

In `PresetParser.cs` in `TryRead<T>`, branch before `token.Value<T>()`:

```csharp
if (typeof(T) == typeof(int))
{
    var raw = token.Value<long>();
    if (raw < int.MinValue || raw > int.MaxValue)
    {
        refusal = PresetLoadRefusal.ValueOutOfRange;
        value = default;
        return false;
    }
    value = (T)(object)(int)raw;
    return true;
}
value = token.Value<T>();
```

The `bool` and `string` instantiations keep the generic path. A token beyond `long` range already refuses as `MalformedJson` at `JToken.Parse`.

- [ ] **Step 4: Run tests to verify they pass**

Run `PresetParserTests` via Unity Test Runner, EditMode. Verify all new and existing tests pass.

---

### Task 3: Clamp Pair Invariant at Parse Time

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Presets/PresetLoadRefusal.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetParserTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Consumes: `AlphaPolicyBounds.ClampNoise`, `PresetLoadRefusal`
- Produces: `PresetLoadRefusal.ClampAboveOpaquePercent` for a stored clamp at or above the effective opaque percent. `PolygonClampPercentFrom` loses the `ClampNoise` re-normalization and keeps the 0 to 100 banding and the inert mapping of 100 to 0.

- [ ] **Step 1: Write the failing tests**

The suite helpers are `ValidJson`, `WithSettings(find, replace)`, and `Refuses(json, expected)`. The shipped body carries `minimumOpaqueAlphaPercent` 100 and `polygonAlphaUpperClampPercent` 100. In `PresetParserTests.cs`, add:

```csharp
[Test]
public void ClampAtOrAboveOpaquePercentRefuses()
{
    Refuses(WithSettings(
                "\"minimumOpaqueAlphaPercent\": 100",
                "\"minimumOpaqueAlphaPercent\": 60")
            .Replace("\"polygonAlphaUpperClampPercent\": 100",
                "\"polygonAlphaUpperClampPercent\": 60"),
        PresetLoadRefusal.ClampAboveOpaquePercent);
}

[Test]
public void ClampBelowOpaquePercentParses()
{
    var body = WithSettings(
            "\"minimumOpaqueAlphaPercent\": 100",
            "\"minimumOpaqueAlphaPercent\": 60")
        .Replace("\"polygonAlphaUpperClampPercent\": 100",
            "\"polygonAlphaUpperClampPercent\": 59");
    Assert.That(PresetParser.TryParse(
        body, out _, out var refusal), Is.True, refusal.ToString());
}

[Test]
public void InertClampWithAnyOpaqueParses()
{
    // The shipped clamp 100 stays the inert sentinel over an opaque
    // percent of 0, which reads as effective 100.
    var body = WithSettings(
        "\"minimumOpaqueAlphaPercent\": 100",
        "\"minimumOpaqueAlphaPercent\": 0");
    Assert.That(PresetParser.TryParse(
        body, out _, out var refusal), Is.True, refusal.ToString());
}
```

In `AmusePlatformFinishPluginTests.cs`, add a characterization test that a valid in-band component pair maps to the stored clamp unchanged after the compensation deletion. This assertion is expected to pass before and after. Mark it as characterization in the test comment.

- [ ] **Step 2: Run tests to verify they fail**

Run the new parser tests via Unity Test Runner, EditMode. Verify the refusal test fails because the parser accepts the violating pair before this fix and `ClampAboveOpaquePercent` does not exist yet. The build mapper test passes on first run. Record that as characterization.

- [ ] **Step 3: Implement the invariant**

In `PresetLoadRefusal.cs`, add `ClampAboveOpaquePercent,` after `ValueOutOfRange,`.
In `PresetParser.cs`, after the four `TryReadPercent` reads succeed in `TryParse`, add:

```csharp
if (polygonClamp != 100)
{
    var effectiveOpaque = alphaClamp <= 0 ? 100 : alphaClamp;
    if (polygonClamp >= effectiveOpaque)
    {
        refusal = PresetLoadRefusal.ClampAboveOpaquePercent;
        return false;
    }
}
```

In `AmusePlatformFinishPlugin.cs` in `PolygonClampPercentFrom`, replace `return AlphaPolicyBounds.ClampNoise(opaque, stored);` with `return stored;` and restate the method comment: the parser enforces the pair invariant for preset files, the inspector enforces it for live edits, and this banding exists only against tampered serialized components.

- [ ] **Step 4: Run tests to verify they pass**

Run `PresetParserTests` and `AmusePlatformFinishPluginTests` via Unity Test Runner, EditMode. Verify all new and existing tests pass.

---

### Task 4: Preset File Store Handles the File-Reference Layout

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Presets/PresetFileStore.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetFileStoreTests.cs`

**Interfaces:**
- Consumes: `UnityEditor.PackageManager.PackageInfo.FindForAssembly`, `AssetDatabase.LoadAssetAtPath`, `File.ReadAllText`, `PresetParser.TryParse`
- Produces: a store that loads through a project-relative path or a direct file read, and never returns a partially filled list on failure

- [ ] **Step 1: Run the runtime probe and record the observed layout**

In the dev editor instance, execute editor code that returns:

```csharp
var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
    typeof(Alrauna.Amuse.Editor.Presets.PresetFileStore).Assembly);
return new
{
    AssetPath = info?.assetPath,
    IsRooted = info != null &&
        System.IO.Path.IsPathRooted(info.assetPath),
    SafeLoadable = AssetDatabase.LoadAssetAtPath<TextAsset>(
        info?.assetPath + "/Presets/safe.json") != null,
};
```

Record the observed values in the task notes. The result decides which branch the file-reference tests cover. A rooted path that loads is already relative-izable. A rooted path that does not load exercises the direct-read branch.

- [ ] **Step 2: Write the failing test for the partial list**

In `PresetFileStoreTests.cs`, add a test that points the store at a temporary folder with one valid and one broken preset file, in the existing temp-scope style of the suite:

```csharp
[Test]
public void FailedLoadReturnsNoPartialPresets()
{
    // Two files: one valid, one with malformed JSON.
    var loaded = PresetFileStore.TryLoadAllFor(
        folder, out var presets, out var failedFile, out var refusal);
    Assert.That(loaded, Is.False);
    Assert.That(presets, Is.Empty);
    Assert.That(refusal, Is.EqualTo(PresetLoadRefusal.MalformedJson));
}
```

`TryLoadAllFor` is a new internal overload taking the folder, extracted from `TryLoadAll` so tests inject the folder. The production `TryLoadAll` keeps its signature and calls the overload with `PresetsFolder()`.

- [ ] **Step 3: Run the test to verify it fails**

Run the new test via Unity Test Runner, EditMode. Verify it fails because `presets` holds the file parsed before the broken one.

- [ ] **Step 4: Implement the store changes**

In `PresetFileStore.cs`:

- Extract `TryLoadAllFor(string folder, ...)` as above. Before every `return false` after the loop began, call `presets.Clear()`.
- In `PresetsFolder`, when `Path.IsPathRooted(info.assetPath)` is true, relativize against the project root:

```csharp
var projectRoot = System.IO.Directory
    .GetParent(Application.dataPath).FullName;
var relative = System.IO.Path.GetRelativePath(
    projectRoot, info.assetPath);
```

Use the relative path when it does not start with `..`. Keep the rooted path for the direct-read branch when it does.
- In `TryLoadAllFor`, when the relativized route yields no `TextAsset` and the rooted folder exists on disk, load the text with `File.ReadAllText(folder + "/" + name + ".json")` and parse with `PresetParser.TryParse`. Keep the `FileMissing` refusal when neither route finds the file.

- [ ] **Step 5: Add the branch tests the probe confirmed**

If the probe observed a rooted non-loading path, add `RootedOutsideProjectPathReadsFromDisk` using the injected-folder overload and a temporary directory outside the project. If the probe observed a rooted loading path, add `RootedInsideProjectPathRelativizes`. Both follow the temp-scope style. Write only the branches the probe confirmed.

- [ ] **Step 6: Run tests to verify they pass**

Run `PresetFileStoreTests` via Unity Test Runner, EditMode. Verify all new and existing tests pass.

---

### Task 5: Inspector Stops the Silent Rewrite

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`

**Interfaces:**
- Consumes: `EditorGUI.BeginChangeCheck`, `EditorGUI.EndChangeCheck`, `SerializedProperty.intValue`
- Produces: inspector controls that write only on a user change and warn on out-of-band stored values. No control clamps a stored value back into range by itself.

- [ ] **Step 1: Run the runtime confirmation probe and record the observed dirty behavior**

In the dev editor instance, select a temporary GameObject with an `AmuseAvatarOptimizer` component. Record `EditorSceneManager.GetActiveScene().isDirty`, force one inspector repaint with no user input, and record the flag again. Record the observed pair of values in the task notes. The fix removes the unconditional write regardless of the observed outcome, so this probe confirms the defect and does not gate the fix shape.

- [ ] **Step 2: Write the failing tests**

In `AmuseAvatarOptimizerEditorTests.cs`, follow the existing editor-test style of `PolygonClampNormalizationKeepsTheInertSentinel`. Create a component, set out-of-band stored values through the serialized object, force a repaint of the inspector, and assert the stored values survive:

- `PlainRepaintDoesNotRewriteStoredMipCap`: stored `_preserveTransparencyMaxMipLevel` of 15 survives a repaint unchanged.
- `OutOfBandMipCapStaysAndWarns`: the repaint renders a `HelpBox` naming the stored value. Assert through the existing GUI-capture helper the suite uses, or through the extracted warning method below.
- `OutOfBandTextureSizeStaysAndWarns`: stored `_preserveTransparencyMinTextureSize` of 100 survives and warns.
- `OutOfBandPercentStaysAndWarns`: stored `_minimumOpaqueAlphaPercent` of 150 survives and warns.

Extract the warning rendering into one internal static method so the tests assert it directly:

```csharp
internal static bool StoredValueIsOutOfBand(
    int stored, int lowestLegal, int highestLegal)
```

and one internal static string builder for the warning text. The tests then assert the predicate and the message without GUI capture for the warning half and keep the repaint assertions for the write half.

- [ ] **Step 3: Run tests to verify they fail**

Run the new tests via Unity Test Runner, EditMode. Verify the write tests fail because a repaint rewrites 15 to 10, 100 to 128, and 150 to 150. The mip cap rewrites down to the band edge. The texture size rounds up to the next representable power of two, so 100 becomes 128 and 150 becomes 256. The percent slider clamps into range.

- [ ] **Step 4: Implement the change-checked writes**

In `AmuseAvatarOptimizerEditor.cs`:

- Wrap the body of `DrawMipCapPopup`, `DrawMinTextureSizePopup`, and `PercentSlider` in `EditorGUI.BeginChangeCheck()` and `EditorGUI.EndChangeCheck()`. Assign the property only when the check reports a change.
- `DrawMipCapPopup` keeps the current index mapping for display. When `stored` is outside -1 to 10, render the warning `HelpBox` above the popup.
- `DrawMinTextureSizePopup` keeps the display mapping. When `stored` is not -1 and not a power of two in 2 to 8192, render the warning.
- `PercentSlider` displays `Mathf.Clamp(stored, 0, 100)` and writes only on change. When `stored` is outside 0 to 100, render the warning. The clamp normalization in `DrawAlphaPolicyControls` is deliberate policy, not an out-of-band rewrite. It moves inside the same change check. It writes the normalized sentinel only when the user moved a slider.
- Warning text follows the same pattern for all three: name the stored value, state the nearest legal value, and state that AMUSE writes only when the user changes the control.

- [ ] **Step 5: Run tests to verify they pass**

Run `AmuseAvatarOptimizerEditorTests` via Unity Test Runner, EditMode. Verify all new and existing tests pass, including `PolygonClampNormalizationKeepsTheInertSentinel`.

---

### Task 6: Pass Registry Documentation

**Files:**
- Modify: `AGENTS.md`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`

**Interfaces:**
- Consumes: none. Documentation and comment text only.
- Produces: documentation ordinals that match the five-pass registration order.

- [ ] **Step 1: Update the class summaries**

Re-read the two summary regions first, because pair 4 edited `TransientUnlockWindowClose.cs` before this task.
In `AlphaSeparationApply.cs`, change the summary opening from "The third PlatformFinish pass" to "The fourth PlatformFinish pass".
In `TransientUnlockWindowClose.cs`, change "the fourth and last" to "the fifth and last". Keep the rest of the sentence.
In `AmusePlatformFinishPlugin.cs` in the `Configure` comment, change "The window close is the fourth and last pass" to "the fifth and last pass".

- [ ] **Step 2: Update the repository documentation**

In `AGENTS.md`, replace "registers three PlatformFinish passes" with "registers five PlatformFinish passes". Extend the numbered list to five items in registration order, one plain-English sentence each, drawn from the registration comments: the structural graph check, the animator bindings capture, the semantic barrier, the alpha separation apply, and the transient unlock window close. Update the Important Files entry from "the three passes" to "the five passes".

- [ ] **Step 3: Verify by reading**

Read the final `AGENTS.md` architecture section and the three comments. Verify every ordinal matches `Configure`. No test run applies to this task. Run one full compile through the Unity Test Runner with a broad existing filter such as `AmusePlatformFinishPluginTests` to confirm the comment edits compile clean, and record the observed count.

---

### Task 7: Layering Ruling Sentence and Evidence Gates Member Move

**Files:**
- Modify: `AGENTS.md`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/EvidenceGates.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonUnknownRecord.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`

**Interfaces:**
- Consumes: `LilToonSemanticDiagnostic`, `LilToonSemanticOutput`, `LilToonSemanticDiagnosticCode`, `SemanticOutput<T>`
- Produces: `LilToonUnknownRecord.RecordUnknown<T>` in the lilToon namespace. `EvidenceGates` holds no frontend-specific member.

- [ ] **Step 1: Amend the repository layering sentence**

In `AGENTS.md`, keep the Host ownership sentence as Host owning live-object reads and capture. Add one sentence after it: the Semantics conversion recipes are sanctioned exceptions that mutate only AMUSE-owned transients. This applies the shared ruling from the spec. The repository owner can overrule it.

- [ ] **Step 2: Move the recorder**

Create `LilToonUnknownRecord.cs` in namespace `Alrauna.Amuse.Editor.Semantics.LilToon`:

```csharp
internal static class LilToonUnknownRecord
{
    /// <summary>
    /// Records one lilToon diagnostic and answers the Unknown output of
    /// the requested kind. The Poiyomi frontend keeps its own analog,
    /// because it routes through the Poiyomi diagnostic vocabulary.
    /// </summary>
    internal static SemanticOutput<T> RecordUnknown<T>(
        List<LilToonSemanticDiagnostic> diagnostics,
        LilToonSemanticOutput output,
        LilToonSemanticDiagnosticCode code,
        string detail)
        where T : class
    {
        diagnostics.Add(new LilToonSemanticDiagnostic(output, code, detail));
        return SemanticOutput<T>.Unknown();
    }
}
```

Delete `RecordUnknown<T>` and the `using Alrauna.Amuse.Editor.Semantics.LilToon;` from `EvidenceGates.cs`. Remove the phrase "the lilToon unknown-recorder" from the class comment. The remaining comment claim then matches the members.
In the two caller files, add `using static Alrauna.Amuse.Editor.Semantics.LilToon.LilToonUnknownRecord;` beside the existing `using static Alrauna.Amuse.Editor.Semantics.EvidenceGates;`. Both files use other gate members, so both usings stay. The 48 call sites stay untouched because the method name is unchanged.

- [ ] **Step 3: Run the lilToon suites to verify behavior is preserved**

Run `LilToonAlphaTests`, `LilToonBaseColorTests`, `LilToonEmissionTests`, and `LilToonNormalTests` via Unity Test Runner, EditMode. Verify all pass. This is a move, so the suites are the safety net and no new RED is possible. Record the observed counts.

---

### Task 8: One Per-Material Resolution Rule

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`

**Interfaces:**
- Consumes: `UnityMaterialSemantics.AlphaRequestForFamily`, `AlphaFieldSet.TryGetFor`, `AlphaSemanticsResolver.Resolve`
- Produces: `AdmittedMaterialStates.ResolveAdmittedMaterialSemantics(CapturedAlphaMaterial, CapturedAlphaSemantics, AlphaFieldSet, int)` as the one per-material resolution rule. Both entry points call it.

- [ ] **Step 1: Extract the shared rule**

In `AdmittedMaterialStates.cs`, add:

```csharp
/// <summary>
/// One per-material resolution rule: the family's own alpha request scopes
/// the field lookup to the material's own evidence, and the resolver runs
/// at the caller's noise bound. The admitted-slot loop and the Host probe
/// both call this, so field scoping cannot drift between the proof path
/// and the census calibration path.
/// </summary>
internal static AlphaResolution ResolveAdmittedMaterialSemantics(
    CapturedAlphaMaterial material,
    CapturedAlphaSemantics capturedSemantics,
    AlphaFieldSet alphaFields,
    int maxNoiseTexelPercent)
{
    var predicateRequest =
        UnityMaterialSemantics.AlphaRequestForFamily(
            material.Family);
    AlphaFieldProvider materialFields =
        (TextureSourceId source,
            TextureChannel channel,
            out AlphaMipChain chain) =>
        {
            chain = null;
            return alphaFields.TryGetFor(
                material.Evidence,
                predicateRequest,
                source,
                channel,
                out chain);
        };
    return AlphaSemanticsResolver.Resolve(
        capturedSemantics.Semantics.Alpha,
        materialFields,
        maxNoiseTexelPercent);
}
```

In `ResolveSlot`, replace the inline predicate request, provider closure, and `Resolve` call with one call to the new method. Keep the derived-evidence admission, the refusal ladder, and the missing memoization exactly as they are.
In `UnityRendererAlphaAnalysis.ResolveFor`, keep the null-material branch and the memoization. Replace the predicate request, provider closure, and `Resolve` call with the same call.

- [ ] **Step 2: Run both suites to verify behavior is preserved**

Run `AdmittedMaterialStatesTests` and `UnityRendererAlphaAnalysisTests` via Unity Test Runner, EditMode. Verify all pass. This is a behavior-preserving refactor. No new RED is possible. Record the observed counts.

---

### Task 9: One Conversion Outcome Mapping per Family

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/VerifiedPoiyomiTestSeams.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/VerifiedLilToonTestSeams.cs`

**Interfaces:**
- Consumes: `LilToonOpaqueConversionEligibility`, `PoiyomiOpaqueConversionEligibility`, `PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone`
- Produces: `AlphaSeparationPreparation.TryMapLilToonOutcome` and `AlphaSeparationPreparation.TryMapPoiyomiOutcome`. Production arms and both verified seams call them.

- [ ] **Step 1: Add the two mapping functions**

In `AlphaSeparationPreparation.cs`, add:

```csharp
internal static bool TryMapLilToonOutcome(
    LilToonOpaqueConversionEligibility eligibility,
    Func<Material> createClone,
    out Material opaque,
    out LilToonOpaqueConversionRefusal refusal,
    out bool depthTestDivergence)
{
    if (eligibility.Outcome !=
        LilToonOpaqueConversionOutcome.Convertible)
    {
        opaque = null;
        refusal = eligibility.Refusal;
        depthTestDivergence = false;
        return false;
    }

    // An already-prepared artifact for this source is reused here;
    // only a first conversion creates the canonical clone. The caller
    // owns the clone recipe, because the production route attests
    // through the evidence overload while the verified seam pins an
    // explicit stand-in shader.
    opaque = createClone();
    refusal = LilToonOpaqueConversionRefusal.None;
    depthTestDivergence = eligibility.DepthTestDivergence;
    return true;
}
```

and:

```csharp
internal static bool TryMapPoiyomiOutcome(
    PoiyomiOpaqueConversionEligibility eligibility,
    Material live,
    Material preparedOpaque,
    out Material opaque,
    out PoiyomiOpaqueConversionRefusal refusal,
    out bool depthTestDivergence,
    out bool premultiplyNormalization)
{
    switch (eligibility.Outcome)
    {
        case PoiyomiOpaqueConversionOutcome.AlreadyOpaque:
            opaque = live;
            refusal = PoiyomiOpaqueConversionRefusal.None;
            depthTestDivergence = false;
            premultiplyNormalization = false;
            return true;
        case PoiyomiOpaqueConversionOutcome.Convertible:
            // An already-prepared artifact for this source is reused
            // here; only a first conversion creates the canonical clone.
            opaque = preparedOpaque ??
                PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone(
                    live);
            refusal = PoiyomiOpaqueConversionRefusal.None;
            depthTestDivergence = eligibility.DepthTestDivergence;
            premultiplyNormalization =
                eligibility.PremultiplyNormalization;
            return true;
        default:
            opaque = null;
            refusal = eligibility.Refusal;
            depthTestDivergence = false;
            premultiplyNormalization = false;
            return false;
    }
}
```

- [ ] **Step 2: Redirect the production arms**

Rewrite the Multi arm, the regular lilToon arm, and the Poiyomi inline arm to call the mappings. The lilToon callers pass `() => preparedOpaque ?? LilToonOpaqueTarget.PrepareCanonicalOpaqueClone(live, derived)`. The Poiyomi caller keeps `refusedDetail = refusal.ToString()` on the false return.

- [ ] **Step 3: Redirect the verified seams**

`VerifiedPoiyomiTestSeams.VerifiedConversion` calls `TryMapPoiyomiOutcome` and passes its arguments through.
`VerifiedLilToonTestSeams.VerifiedOpaqueConversion` calls `TryMapLilToonOutcome` with `() => preparedOpaque ?? LilToonOpaqueTarget.PrepareCanonicalOpaqueClone(live, Shader.Find(LilToonFixtureTestBase.OpaqueConversionShaderName))`.
The seam documentation keeps its warning against a second copy. The warning is now true by construction.

- [ ] **Step 4: Run the suites to verify behavior is preserved**

Run `AlphaSeparationPreparationTests`, `AlphaSeparationSplitTests`, `LilToonMultiSourceEligibilityTests`, and the fixture suites that drive the seams, via Unity Test Runner, EditMode. Verify all pass. Behavior-preserving refactor. Record the observed counts.

---

### Task 10: One Normalize and Digest Rule

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/NormalizedSourceHash.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonSourceAttestationTests.cs`

**Interfaces:**
- Consumes: `NormalizedSourceHash.Compute`
- Produces: `NormalizedSourceHash.NormalizeText` as the public normalize half. `LilToonSourceAttestation` holds no private normalize or digest rule.

- [ ] **Step 1: Add the characterization test**

In `LilToonSourceAttestationTests.cs`, add a characterization test pinning the normalize rule through the public seam:

```csharp
[Test]
public void NormalizeTextMatchesTheAttestationRule()
{
    Assert.That(
        NormalizedSourceHash.NormalizeText("﻿a\r\nb\rc"),
        Is.EqualTo("a\nb\nc"));
    Assert.That(
        NormalizedSourceHash.Compute("a\r\nb"),
        Is.EqualTo(
            NormalizedSourceHash.Compute("a\nb")));
}
```

This assertion is expected to pass before and after the refactor. Record it as characterization in the test comment.

- [ ] **Step 2: Run the test to verify it passes on first run**

Run the new test via Unity Test Runner, EditMode. Record the observed pass as characterization.

- [ ] **Step 3: Extract and delete**

In `NormalizedSourceHash.cs`, add `NormalizeText` holding the BOM drop and the newline fold. Refactor `Compute` to call `NormalizeText` and then digest.
In `LilToonSourceAttestation.cs`, delete the private `Normalize` and `Sha256`. Replace the three text-split call sites at the strip rules with `NormalizedSourceHash.NormalizeText`. Replace the four digest call sites with `NormalizedSourceHash.Compute`, which is value-identical on the already-normalized canonical text they hash. Keep `ComputeNormalizedSourceHash` delegating to `Compute`.

- [ ] **Step 4: Run the suites to verify behavior is preserved**

Run `LilToonSourceAttestationTests` and `LilToonAttestationTests` via Unity Test Runner, EditMode. Verify all pass. Record the observed counts.

---

### Task 11: One Unity-Level Canonical Verification Skeleton

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/CanonicalOpaqueVerification.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`

**Interfaces:**
- Consumes: `EffectiveRenderState.ReadEffectiveRenderState`
- Produces: one shared constants set, one shared Unity-level fact scan, and one shared clone, write, verify, destroy skeleton. The family float tuples stay per family.

- [ ] **Step 1: Create the shared seam**

Create `CanonicalOpaqueVerification.cs` in namespace `Alrauna.Amuse.Editor.Semantics`:

```csharp
internal static class CanonicalOpaqueVerification
{
    internal const int CanonicalOpaqueRenderQueue = 2000;
    internal const string RenderTypeTagName = "RenderType";
    internal const string CanonicalOpaqueRenderType = "Opaque";

    internal static bool TryFindNonCanonicalFact(
        Material candidate,
        IReadOnlyList<(string Property, float Value)> tuple,
        out string factName)

    internal static Material PrepareCanonicalClone(
        Material source,
        Shader attestedTarget,
        IReadOnlyList<(string Property, float Value)> tuple,
        Action<Material> familyWrites,
        Func<Material, string> familyScan,
        string shaderIdentityFailureText)
}
```

`TryFindNonCanonicalFact` scans the tuple, then the effective queue, then the RenderType tag.
`PrepareCanonicalClone` clones with `new Material(source)`, swaps the shader when `attestedTarget` is non-null, writes the tuple, sets the queue and the tag, calls `familyWrites`, runs the shared scan plus `familyScan`, verifies the shader identity against `attestedTarget ?? source.shader`, destroys the clone, and throws on failure. The read-back failure text moves here verbatim. The shader-identity text stays per family through `shaderIdentityFailureText`, because lilToon says the clone did not take the attested opaque target shader and Poiyomi says it did not preserve the source shader. lilToon's Multi keyword branch keeps its own destroy path inside the lilToon class. The skeleton clears the clone name through `familyWrites`, so the lilToon name-clearing order relative to the queue and tag writes may shift. That reorder is behaviorally equivalent.

- [ ] **Step 2: Redirect the families**

`LilToonOpaqueTarget` and `PoiyomiOpaqueConversion` delete their local queue, tag name, and tag value constants and reference the shared ones.
`LilToonOpaqueTarget.TryFindNonCanonicalFact` calls the shared scan and then applies the `_TransparentMode` conjunct. `PoiyomiOpaqueConversion.TryFindNonCanonicalFact` calls the shared scan alone.
`LilToonOpaqueTarget.PrepareCanonicalOpaqueClone(Material, Shader)` keeps its shader-level property precheck, then calls the shared skeleton with `attestedTarget`, a `familyWrites` closure that clears the clone name and applies the Multi keyword branch or the regular keyword disables, a `familyScan` returning the `_TransparentMode` fact name or null, and its own shader-identity text.
`PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone` calls the shared skeleton with `attestedTarget: null`, an empty `familyWrites`, a null-returning `familyScan`, and its own shader-identity text.
Re-read the file first, because pair 5 changed the NaN sweep order in `PoiyomiOpaqueConversion.cs` before this task.

- [ ] **Step 3: Run the suites to verify behavior is preserved**

Run `LilToonOpaqueTargetTests`, `PoiyomiOpaqueConversionTests`, `AlphaSeparationPreparationTests`, and `AlphaSeparationSplitTests` via Unity Test Runner, EditMode. Verify all pass. The stand-in shaders drive both skeletons through the redirected families. Record the observed counts.

---

### Task 12: The Smaller Impurities

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/LiveAnimationObservation.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/EffectiveMaterialMaterialization.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowState.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockSwapIn.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/LiveAnimationObservationVirtualClipTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`
- Modify: `Packages/.gitignore`

**Interfaces:**
- Consumes: `ReferenceEqualityComparer<T>.Instance`, `UnityRendererAlphaSnapshot.ExtraUvSets`
- Produces: seven one-line-scale fixes. Sub-items 2 and 4 are behavior changes with their own tests. The rest are renames, comments, deletions, and one whitelist line.

- [ ] **Step 1: Write the failing tests for sub-items 2 and 4**

In `LiveAnimationObservationVirtualClipTests.cs`, add two tests against the real observation API:

```csharp
[Test]
public void SingleNonFiniteKeyIsNotFiniteExact()
{
    var clip = new AnimationClip { name = "SingleNonFiniteKey" };
    var binding = EditorCurveBinding.FloatCurve(
        "Body", typeof(SkinnedMeshRenderer), "m_BlendShape.Weight");
    AnimationUtility.SetEditorCurve(
        clip, binding, new AnimationCurve(new Keyframe(0f, float.NaN)));

    var observed = LiveAnimationObservation.ObserveClip(clip, false);

    Assert.That(observed.Floats[0].IsFiniteExact, Is.False);
}

[Test]
public void AllEqualInfiniteRunIsNotFiniteExact()
{
    var clip = new AnimationClip { name = "AllEqualInfiniteRun" };
    var binding = EditorCurveBinding.FloatCurve(
        "Body", typeof(SkinnedMeshRenderer), "m_BlendShape.Weight");
    var curve = new AnimationCurve(
        new Keyframe(0f, float.PositiveInfinity),
        new Keyframe(1f, float.PositiveInfinity));
    AnimationUtility.SetEditorCurve(clip, binding, curve);

    var observed = LiveAnimationObservation.ObserveClip(clip, false);

    Assert.That(observed.Floats[0].IsFiniteExact, Is.False);
}
```

The second test pins the drift the finding names. Before the fix, the all-equal infinite run with flat tangents answers true, so the consumer names the sources-disagree refusal. The executor records the observed first-run verdict for both tests.

In `UnityRendererAlphaAnalysisTests.cs`, add a test beside the direct `Classify` test around line 1215, which already builds `extra` UV sets explicitly:

```csharp
[Test]
public void ProbePathClassifiesThroughExtraUvSets()
{
    // Build the snapshot with the suite's snapshot helper. Give the
    // material a uv1 layer whose value decides one triangle. Run the
    // probe Analyze. Assert the triangle outcome the uv1 layer decides,
    // which the probe answers Unknown before the fix.
}
```

Build the snapshot with the suite's existing snapshot helper and the same extra-set shape the direct `Classify` test uses. Assert the triangle outcome the uv1 layer decides.

- [ ] **Step 2: Run the tests to verify they fail**

Run the two new tests via Unity Test Runner, EditMode. Verify `SingleNonFiniteKeyIsNotFiniteExact` and `AllEqualInfiniteRunIsNotFiniteExact` fail on the `IsFiniteExact` verdict and `ProbePathClassifiesThroughExtraUvSets` fails because the probe resolves the layer unknown. Record the observed first-run verdicts.

- [ ] **Step 3: Implement the seven fixes**

1. Rename `BaseMaterialSemanticsProvider` to `ResearchMaterialSemanticsProvider` in `UnityRendererAlphaAnalysis.cs`. Update the declaration, the `Capture` parameter, and every reference. Restate the comment so the name says research-only.
2. In `LiveAnimationObservation.IsFiniteExact`, add the early non-finite arm before the adjacency loop: when any key value is non-finite, return false. A single-key NaN curve and an all-equal infinite run with flat tangents then answer false, so the consumer names the non-finite refusal. Every finite-curve verdict stays identical.
3. In `EffectiveMaterialMaterialization.cs`, restate the skip comment: the guard is a null-reference guard for a material or shader without a schema, and `SchemaFor` synthesizes a schema for any shader, so the blind spot is a comment-level note, not a schema gap. No behavior change.
4. In the probe `Analyze` loop in `UnityRendererAlphaAnalysis.cs`, pass `snapshot.ExtraUvSets` as the fifth `Classify` argument. Remove the `= null` default from the `extraUvSets` parameter of `Classify`. The product call at `IntersectResolvedOutcomes` and the test caller already pass the argument.
5. Delete the write-only binding records: the `bindings` field, the `Bindings` property, the `AddBinding` method, and the `RewrittenBinding` struct in `TransientUnlockWindowState.cs`. Delete the `pair.AddBinding(binding, slotIndex);` call in `TransientUnlockSwapIn.cs`. Re-read both files first, because pair 4 edited the close pass before this task. Delete the test scaffolding local and call at `TransientUnlockWindowCloseTests.cs` around line 880 and keep that test's assertions unchanged.
6. In `UnityMaterialEvidenceCaptureTests.cs`, replace `ReferenceEqualityComparer.Instance` at the `Walk` call with `ReferenceEqualityComparer<object>.Instance` and delete the private comparer class.
7. Delete the last line of `Packages/.gitignore`, which duplicates the whitelist pattern of line 2.

- [ ] **Step 4: Run tests to verify they pass**

Run `LiveAnimationObservationVirtualClipTests`, `UnityRendererAlphaAnalysisTests`, `UnityMaterialEvidenceCaptureTests`, `TransientUnlockWindowCloseTests`, `TransientUnlockSwapInTests`, and `TransientUnlockWindowStateTests` via Unity Test Runner, EditMode. Verify all new and existing tests pass. Record the observed counts.

---

### Task 13: List Indirect Dependencies Offline

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/PackageVersionReader.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/PackageVersionReaderTests.cs`

**Interfaces:**
- Consumes: `UnityEditor.PackageManager.Client.List(bool, bool)`
- Produces: `internal static Func<bool, bool, RequestList> ListPackages`, the production test seam

- [ ] **Step 1: Write the failing test**

Create `Packages/com.alrauna.amuse/Tests/Editor/Semantics/PackageVersionReaderTests.cs` if it does not exist. Add `ListPackagesReceivesIncludeIndirectTrue`. The test substitutes the `ListPackages` seam with a recording fake, calls the reader's version path, and asserts the seam received `offline: true` and `includeIndirect: true`. Restore the seam in a finally.

```csharp
[Test]
public void ListPackagesReceivesIncludeIndirectTrue()
{
    bool? seenOffline = null;
    bool? seenIndirect = null;
    var original = PackageVersionReader.ListPackages;
    PackageVersionReader.ListPackages = (offline, includeIndirect) =>
    {
        seenOffline = offline;
        seenIndirect = includeIndirect;
        return null;
    };
    try
    {
        PackageVersionReader.ReadProducerVersion("any.package.name");
    }
    finally
    {
        PackageVersionReader.ListPackages = original;
    }

    Assert.That(seenOffline, Is.True);
    Assert.That(seenIndirect, Is.True);
}
```

Adapt the reader call name to the real member if it differs. The assertion on the seam arguments is the RED: today the reader calls the one-argument `Client.List(true)`, so `includeIndirect` never arrives.

- [ ] **Step 2: Run the test to verify it fails**

Run `ListPackagesReceivesIncludeIndirectTrue` via Unity Test Runner, EditMode. Verify it fails because the seam does not exist and the reader never passes the flag.

- [ ] **Step 3: Implement the seam and the flag**

In `PackageVersionReader.cs`, add `using System;` if the file lacks it. The `Func` type lives in the `System` namespace. Then add the seam field next to the read method:

```csharp
internal static Func<bool, bool, RequestList> ListPackages =
    (offline, includeIndirect) => Client.List(offline, includeIndirect);
```

Change the read call to `ListPackages(true, true)` and enumerate the result exactly as before. Every admission path heals, because the version lookup now sees indirectly installed producers.

- [ ] **Step 4: Run the test to verify it passes**

Run the new test via Unity Test Runner, EditMode. Verify it passes. Then run one manual check and record the observed result: with this repository's installed packages, the compressor and AAO producer versions still admit. A filtered run that reports 0 tests is a failure.

- [ ] **Step 5: Record the deferred findings**

Findings 35 and 36 of the investigation stay deferred. The spec's section 2.15 records the rationale. This task adds no code for them.

---

### Task 14: Full Suite Sweep

**Files:**
- No production changes. Verification only.

**Interfaces:**
- Consumes: every suite named in Tasks 1 to 13.
- Produces: one observed green sweep for the pair.

- [ ] **Step 1: Run the affected suites together**

Run all suites named in Tasks 1 to 13 in one EditMode session via Unity Test Runner. Verify zero failures and a nonzero total. A filtered run that reports 0 tests is a failure.

- [ ] **Step 2: Sweep the changed files for identifiers**

Check every changed file for an at sign joined to a hexadecimal hash, drive-letter paths, home-directory paths, four-digit ports, and private asset names learned in the session. Every hit is a defect. Fix the hits, then state that the sweep ran.
