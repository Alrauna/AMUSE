# Poiyomi Refusal Reporting Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every AMUSE refusal entry on the audited failure modes name its subject truthfully: the refused material, the real locked-material gate, the closure renderer and failure way, the refused texture's format and name, and the Poiyomi alpha mask feature.

**Architecture:** Five report-only defects, five local fixes. One emitter passes a material it already holds. One string triple is reworded. One emitter gains two optional arguments and a failure-way sentence mapper. One evidence record gains two display strings that the capture site fills from the texture it already holds, and three templates stop asserting slot outcomes. One label map gains three entries. No classification, capture, or mutation behavior changes.

**Tech Stack:** Unity 2022.3.22f1, C#, NDMF 1.14.4 error reporting, NUnit through the Unity Test Framework, EditMode only.

**Spec:** `docs/superpowers/specs/2026-10-06-poiyomi-refusal-reporting-design.md`
Investigation: `docs/superpowers/investigations/2026-10-06-poiyomi-avatar-refusal-audit-investigation.md`

## Global Constraints

- Branch `fix/poiyomi-refusal-reporting`, base `main` at `96b5705`.
- Unity 2022.3.22f1, NDMF 1.14.4, VRChat SDK Base and Avatars `[3.10.4, 4.0.0)`.
- Tests run in the dev editor instance through the Unity Test Runner, EditMode mode, assembly `Alrauna.Amuse.Tests.Editor`. Tests never run in the Census Lab project. An agent drives runs through the editor MCP `run_tests` tool with the test filter, then polls `get_test_job`. Before any Unity MCP write, verify `Application.dataPath` equals the dev project's `Assets` folder and pin the dev editor instance.
- RED before GREEN. Record the observed test count and the failing message for every RED state. A filtered run that reports 0 tests is a failure.
- Every user-visible string uses simple english: short active sentences, one idea per sentence, no semicolons, no contractions.
- Evidence records stay free of live Unity objects. `TextureCaptureRefusal` gains display strings only.
- No production behavior change outside report text, report arguments, and report call sites. Refusals, classifications, and the mutation path stay byte-identical.
- Commit steps run only after the user authorizes commits for this branch in the execution session. Stage only the files the task names. Until then, leave changes unstaged.
- Stop conditions: the conversion loop's live material does not name the refusing material at the report site, `evidence` is not in scope at the renderer refusal site, an existing test pins any changed copy, or any RED state cannot be reached. Stop and report evidence.
- The user's unrelated working-tree changes (`.github/workflows/pr.yml`, `ProjectSettings/ProjectSettings.asset`) are never staged by these tasks.

---

### Task 1: The conversion-refused slot names its material

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:331-431` (conversion loop plus report call)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:431-433` (the `OpaqueConversionRefused` description)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs` (add directly after `RefusedTransparentSlotLeavesItsAdmittedSiblingsConverted`, which ends at `:5356`)

**Interfaces:**
- Consumes: `AmuseReports.SlotSeparationRefusal(Renderer, int, AlphaSeparationSlotRefusal, string rendererName = null, Material offendingMaterial = null, ...)` — the existing signature at `AmuseReports.cs:210-218`; its registry resolve at `:236-243` already names the authoring asset for registered clones.
- Produces: nothing new. Substitution `{3}` of `amuse.slotSeparation.OpaqueConversionRefused` now renders the refused material's name.

- [ ] **Step 1: Write the failing test**

Add to `AlphaSeparationPreparationTests`, after `:5356`:

```csharp
        /// <summary>
        /// A conversion-refused slot names the material that refused.
        /// The locality arm's transparent slot refuses conversion through
        /// its depth comparison, exactly one entry reports that refusal,
        /// and the entry names the refused material.
        /// <para>
        /// Falsifies: an emitter that passes no offending material, and a
        /// template that never consumes it.
        /// </para>
        /// </summary>
        [Test]
        public void ConversionRefusedSlotNamesItsMaterial()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonTransparentConversionFixtures();
            try
            {
                fixtures.BaseSetUp();
                var opaqueTexture = fixtures.ImportFullyOpaqueMipmap(
                    "conversion_refusal_names_material");

                using (var arm = LocalityArmFixture.Create(
                           fixtures, opaqueTexture,
                           transparent => transparent.SetFloat("_ZTest", 8f)))
                {
                    AmusePlatformFinishState amuse = null;
                    var reports = ErrorReport.CaptureErrors(
                        () => amuse = arm.Run());

                    Assert.That(
                        amuse.SlotRefusalCount(
                            AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused),
                        Is.EqualTo(1),
                        "fixture precondition: the transparent slot's " +
                        "depth comparison must refuse exactly its own " +
                        "slot");

                    Assert.That(
                        reports.Select(r => r.TheError.ToMessage()),
                        Has.Exactly(1).Contains(
                            arm.TransparentMaterial.name),
                        "the refused slot's entry must name the material " +
                        "that refused");
                }
            }
            finally
            {
                fixtures.BaseTearDown();
            }
        }
```

One arm run produces both the state and the report stream, matching the capture pattern of the depth-policy tests in the same file.

- [ ] **Step 2: Run the test and observe RED**

Run in the dev editor instance: Test Runner, EditMode, filter `ConversionRefusedSlotNamesItsMaterial`.

Expected: 1 test, Failed — the message assertion finds no entry naming `arm.TransparentMaterial.name`, because the emitter passes no material and no template consumes one. Record the observed count and message.

- [ ] **Step 3: Track and pass the refused material**

In `AlphaSeparationPreparation.cs`, the per-slot conversion loop declares its trackers at `:333-335`. Add one parallel tracker:

```csharp
                var slotRefusal = AlphaSeparationSlotRefusal.None;
                var unconvertedCount = 0;
                var lastConversionRefusal = AlphaSeparationSlotRefusal.None;
                Material lastRefusedMaterial = null;
```

Inside the refusal branch at `:390-392`, beside the existing assignment:

```csharp
                    if (conversionRefusal != AlphaSeparationSlotRefusal.None)
                    {
                        lastConversionRefusal = conversionRefusal;
                        lastRefusedMaterial = live;
```

The single-material path breaks immediately after this assignment, so the failing material is the recorded one. The report call at `:427-431` gains the offender:

```csharp
                    state.RecordSlotRefusal(slotRefusal);
                    AmuseReports.SlotSeparationRefusal(
                        target.Renderer,
                        slotIndex,
                        slotRefusal,
                        offendingMaterial: lastRefusedMaterial);
```

- [ ] **Step 4: Make the template consume the offender**

In `AmuseReportStrings.cs:431-433`, replace the description:

```csharp
            ["amuse.slotSeparation.OpaqueConversionRefused:description"] =
                "The opaque conversion refused one material ({3}) on slot " +
                "{0} of renderer '{2}'. Reason: {1}. The slot keeps its " +
                "original material.",
```

- [ ] **Step 5: Run the test and observe GREEN**

Same filter. Expected: 1 test, Passed.

- [ ] **Step 6: Run the neighboring preparation tests**

Filters `RefusedTransparentSlot` and `OpaqueConversionRefused`. Expected: the locality tests and the new test all Pass. Record observed counts.

- [ ] **Step 7: Commit (authorization-gated)**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs
git commit -m "fix: name the refused material in conversion refusals"
```

---

### Task 2: The locked-material title names verification

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:172-178` (title and description)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsRendererTests.cs` (add directly after `MappingRefusalWithUnknownCountStatesUnknownNotMinusOne`, which ends at `:78`)

**Interfaces:**
- Consumes: `AmuseReports.RendererRefusal(Renderer, RendererAnalysisRefusal, int meshSubMeshCount = -1, int materialSlotCount = -1)` at `AmuseReports.cs:381-396`, unchanged in this task.
- Produces: nothing new.

- [ ] **Step 1: Write the failing test**

Add to `AmuseReportsRendererTests`, before the closing brace:

```csharp
        [Test]
        public void LockedMaterialTitleNamesVerificationNotTracing()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.RendererRefusal(
                    _renderer,
                    RendererAnalysisRefusal
                        .LockedPoiyomiOriginalShaderUnattested));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();

            // The trace succeeds for a recorded lock; the blocker is
            // attestation of the traced original. The title names the
            // gate that actually refused.
            Assert.That(message, Does.Contain("has not verified"));
            Assert.That(message, Does.Not.Contain("cannot trace"));
        }
```

- [ ] **Step 2: Run the test and observe RED**

Filter `LockedMaterialTitleNamesVerificationNotTracing`.

Expected: 1 test, Failed — the current title is "This renderer holds a locked material AMUSE cannot trace." (`AmuseReportStrings.cs:172-173`), so the "has not verified" assertion fails. Record the observed count and message.

- [ ] **Step 3: Reword the title and description**

In `AmuseReportStrings.cs:172-178`, replace:

```csharp
            ["amuse.renderer.LockedPoiyomiOriginalShaderUnattested"] =
                "This renderer holds a locked material whose original " +
                "shader AMUSE has not verified.",
            ["amuse.renderer.LockedPoiyomiOriginalShaderUnattested:description"] =
                "The material is locked. The shader version the lock " +
                "recorded is not a version AMUSE knows, or the lock " +
                "records no original shader. AMUSE cannot read a locked " +
                "material, so it changed nothing on this renderer.",
```

The hint at `:179-181` stays as written.

- [ ] **Step 4: Run the test and observe GREEN**

Same filter. Expected: 1 test, Passed.

- [ ] **Step 5: Commit (authorization-gated)**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsRendererTests.cs
git commit -m "fix: name verification in the locked material title"
```

---

### Task 3: The closure entry names its renderer and failure way

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs:373-396` (`RendererRefusal` signature and substitutions, plus one new mapper method)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:783-789` (the renderer refusal call site)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:31-39` (the closure title and description)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsRendererTests.cs` (add beside Task 2's test)

**Interfaces:**
- Consumes: `MaterialDependencyClosureFailure` (`CapturedAnimationEvidence.cs:8-15`), the `evidence` local in scope at the call site (captured at `AmusePlatformFinishPlugin.cs:637` or `:654`).
- Produces: `AmuseReports.RendererRefusal(Renderer renderer, RendererAnalysisRefusal cause, int meshSubMeshCount = -1, int materialSlotCount = -1, string rendererName = null, string detail = null)` and `AmuseReports.ClosureFailureSentence(MaterialDependencyClosureFailure failure)` returning a trailing-space-terminated sentence or the empty string. Both new parameters are trailing and optional, so every existing call site compiles unchanged.

- [ ] **Step 1: Write the failing tests**

Add to `AmuseReportsRendererTests`:

```csharp
        [Test]
        public void ClosureRefusalNamesRendererAndFailureWay()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.RendererRefusal(
                    _renderer,
                    RendererAnalysisRefusal
                        .MaterialDependencyClosureFailed,
                    -1,
                    -1,
                    "Body",
                    AmuseReports.ClosureFailureSentence(
                        MaterialDependencyClosureFailure
                            .InvalidSwapValue)));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();

            // After play mode the context reference is dead, so the
            // text is the only durable identity carrier.
            Assert.That(message, Does.Contain("'Body'"));
            Assert.That(message, Does.Contain("not a material"));
        }

        [Test]
        public void ClosureFailureSentenceCoversEveryWay()
        {
            Assert.That(
                AmuseReports.ClosureFailureSentence(
                    MaterialDependencyClosureFailure
                        .MissingCurrentMaterial),
                Does.Contain("no material assigned"));
            Assert.That(
                AmuseReports.ClosureFailureSentence(
                    MaterialDependencyClosureFailure.SlotOutOfRange),
                Does.Contain("slot the renderer does not have"));
            Assert.That(
                AmuseReports.ClosureFailureSentence(
                    MaterialDependencyClosureFailure.InvalidSwapValue),
                Does.Contain("not a material"));
            Assert.That(
                AmuseReports.ClosureFailureSentence(
                    MaterialDependencyClosureFailure.UnattestedMaterial),
                Does.Contain("could not be captured"));
            Assert.That(
                AmuseReports.ClosureFailureSentence(
                    MaterialDependencyClosureFailure.None),
                Is.EqualTo(""));
        }
```

Both tests compile only after the mapper and the new parameters exist; the compile failure is this task's RED state.

- [ ] **Step 2: Observe RED**

Run filter `ClosureRefusalNamesRendererAndFailureWay`.

Expected: the test run cannot execute because `RendererRefusal` takes no `rendererName`/`detail` and `ClosureFailureSentence` does not exist. Record the compile failure as the RED state for this API change.

- [ ] **Step 3: Extend the emitter and add the mapper**

In `AmuseReports.cs:381-396`, extend `RendererRefusal`:

```csharp
        internal static void RendererRefusal(
            Renderer renderer,
            RendererAnalysisRefusal cause,
            int meshSubMeshCount = -1,
            int materialSlotCount = -1,
            string rendererName = null,
            string detail = null)
        {
            // Same discipline as the slot-separation emitter: the text
            // must stay readable after the build copy is destroyed, so
            // an omitted name falls back to the live renderer's own
            // name, and a null renderer keeps the honest placeholder.
            rendererName = rendererName
                ?? (renderer != null ? renderer.gameObject.name : null);

            using (ErrorReport.WithContextObject(renderer))
            {
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    AmuseReportStrings.RendererKey(cause),
                    SlotCountPhrase(meshSubMeshCount),
                    SlotCountPhrase(materialSlotCount),
                    rendererName,
                    string.IsNullOrEmpty(detail) ? "" : detail);
            }
        }
```

Add the mapper beside it:

```csharp
        /// <summary>
        /// One plain sentence naming the way the material dependency
        /// closure failed. The renderer entry prints it so the reader
        /// can tell a read failure from an unassigned slot. The empty
        /// string keeps the entry readable when no way is known.
        /// </summary>
        internal static string ClosureFailureSentence(
            MaterialDependencyClosureFailure failure)
        {
            switch (failure)
            {
                case MaterialDependencyClosureFailure
                        .MissingCurrentMaterial:
                    return "The renderer has a material slot with no " +
                           "material assigned. ";
                case MaterialDependencyClosureFailure.SlotOutOfRange:
                    return "An animation binds a material slot the " +
                           "renderer does not have. ";
                case MaterialDependencyClosureFailure.InvalidSwapValue:
                    return "An animation swaps in something that is not " +
                           "a material. ";
                case MaterialDependencyClosureFailure.UnattestedMaterial:
                    return "A material in the animation could not be " +
                           "captured. ";
                default:
                    return "";
            }
        }
```

`AmuseReports.cs` already imports `Alrauna.Amuse.Editor.Host` for `TextureCaptureRefusal`.

- [ ] **Step 4: Reword the closure templates**

In `AmuseReportStrings.cs:31-39`, replace:

```csharp
            ["amuse.renderer.MaterialDependencyClosureFailed"] =
                "AMUSE could not prove the material animations of " +
                "renderer '{2}'.",
            ["amuse.renderer.MaterialDependencyClosureFailed:description"] =
                "AMUSE cannot prove what this renderer shows when an " +
                "animation changes its materials. {3}The renderer keeps " +
                "its original materials.",
```

Other renderer templates consume `{0}`/`{1}` only; the two new substitutions are inert for them, exactly as the slot-count arguments already are for the closure template.

- [ ] **Step 5: Pass name and way at the call site**

In `AmusePlatformFinishPlugin.cs:783-789`, replace the report call:

```csharp
                    state.RecordRendererRefusal(refusal);
                    AmuseReports.RendererRefusal(
                        renderer,
                        refusal,
                        extractionMeshSubMeshCount,
                        extractionMaterialSlotCount,
                        renderer.gameObject.name,
                        refusal ==
                            RendererAnalysisRefusal
                                .MaterialDependencyClosureFailed
                            ? AmuseReports.ClosureFailureSentence(
                                evidence.ClosureFailure)
                            : null);
                    continue;
```

`evidence` is the local captured at `:637` or `:654` in the same loop iteration.

- [ ] **Step 6: Run the tests and observe GREEN**

Filters `ClosureRefusalNamesRendererAndFailureWay` and `ClosureFailureSentenceCoversEveryWay`. Expected: 2 tests, Passed.

- [ ] **Step 7: Run the neighboring renderer-report tests**

Filter `MappingRefusal`. Expected: `MappingRefusalReportCarriesBothSlotCounts` and `MappingRefusalWithUnknownCountStatesUnknownNotMinusOne` still Pass (their templates ignore the new substitutions). Record observed counts.

- [ ] **Step 8: Commit (authorization-gated)**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsRendererTests.cs
git commit -m "fix: name the renderer and failure way in closure refusals"
```

---

### Task 4: The texture entry names its format and texture, and stops claiming outcomes

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/TextureCaptureRefusal.cs:19-54` (two display properties and constructor parameters)
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs:1430-1466` (`BuildCaptureRefusals` fills them)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs:330-349` (`TextureCaptureRefusal` emitter passes them)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:307-367` (three descriptions and the re-import hint)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs` (add after `SlotAnalysisRefusalEmbedsEveryCaptureRefusal`)

**Interfaces:**
- Consumes: the `texture` parameter already in scope in `BuildCaptureRefusals` (`UnityMaterialEvidenceCapture.cs:1432`).
- Produces: `TextureCaptureRefusal` constructor gains `string formatName = null, string textureName = null` as trailing optional parameters, with `FormatName` and `TextureName` string properties. Every existing five-argument construction compiles unchanged. The emitter passes five-then-two substitutions: `{5}` is `FormatName`, `{6}` is `TextureName`.

- [ ] **Step 1: Write the failing tests**

Add to `AmuseReportsSlotTests`:

```csharp
        [Test]
        public void UnsupportedFormatEntryNamesFormatAndTexture()
        {
            var refusal = new TextureCaptureRefusal(
                "_MainTex",
                true,
                default,
                TextureChannel.Alpha,
                TextureCaptureRefusalReason.UnsupportedFormat,
                formatName: "DXT1Crunched",
                textureName: "synthetic atlas copy");

            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.TextureCaptureRefusal(
                    _renderer, 5, refusal));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("DXT1Crunched"));
            Assert.That(message, Does.Contain("synthetic atlas copy"));
        }

        [Test]
        public void TextureEntriesNeverClaimWhereTrianglesLanded()
        {
            var refusal = new TextureCaptureRefusal(
                "_MainTex",
                true,
                default,
                TextureChannel.Alpha,
                TextureCaptureRefusalReason.UnsupportedFormat,
                formatName: "DXT1Crunched",
                textureName: "synthetic atlas copy");

            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.TextureCaptureRefusal(
                    _renderer, 5, refusal));

            var message = errors[0].TheError.ToMessage();

            // A resolved slot's proof may not need the refused chain,
            // so the entry states the lost evidence, never the slot's
            // outcome.
            Assert.That(
                message, Does.Not.Contain("stay on the original material"));
            Assert.That(message, Does.Contain("cannot use this capture"));
        }
```

- [ ] **Step 2: Observe RED**

Filter `UnsupportedFormatEntryNamesFormatAndTexture` and `TextureEntriesNeverClaimWhereTrianglesLanded`.

Expected: the tests cannot compile (the constructor takes no such parameters), and today's description contains "Those triangles stay on the original material". Record the compile failure and the pinned old sentence as the RED state.

- [ ] **Step 3: Extend the record**

In `TextureCaptureRefusal.cs`, add the properties and constructor parameters:

```csharp
        internal string PropertyName { get; }
        internal bool HasSourceIdentity { get; }
        internal TextureSourceId SourceIdentity { get; }
        internal TextureChannel Channel { get; }
        internal TextureCaptureRefusalReason Reason { get; }
        internal string FormatName { get; }
        internal string TextureName { get; }

        internal TextureCaptureRefusal(
            string propertyName,
            bool hasSourceIdentity,
            TextureSourceId sourceIdentity,
            TextureChannel channel,
            TextureCaptureRefusalReason reason,
            string formatName = null,
            string textureName = null)
```

Assign both in the body after the existing assignments. Extend the class doc comment: the record may carry the texture's display name and storage format for the report, and it still holds no live Unity object.

- [ ] **Step 4: Fill them at the capture site**

In `UnityMaterialEvidenceCapture.cs`, inside `BuildCaptureRefusals` after the null guard at `:1435-1438`:

```csharp
            var textureName = texture.name;
            string formatName = null;
            if (texture is Texture2D texture2D)
            {
                formatName = texture2D.format.ToString();
            }
```

Pass both into the two `new TextureCaptureRefusal(...)` constructions at `:1444-1449` and `:1455-1460`, as the trailing `formatName:` and `textureName:` arguments.

- [ ] **Step 5: Pass them at the emitter**

In `AmuseReports.cs:330-349`, extend the `ReportError` argument list:

```csharp
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    AmuseReportStrings.TextureCaptureKey(refusal.Reason),
                    slotIndex,
                    refusal.PropertyName,
                    refusal.Channel.ToString(),
                    refusal.Reason.ToString(),
                    refusal.HasSourceIdentity
                        ? "has a source identity"
                        : "has no source identity",
                    refusal.FormatName,
                    refusal.TextureName);
```

- [ ] **Step 6: Reword the templates**

In `AmuseReportStrings.cs`:

`UnavailableCapture:description` (`:309-314`) and `NonResidentMips:description` (`:337-343`): in each, replace the sentence pair "so AMUSE has no proof for the triangles that sample it. Those triangles stay on the original material." with "AMUSE cannot use this capture as proof for the triangles that sample it."

`UnsupportedFormat:description` (`:354-360`) becomes:

```csharp
            ["amuse.texture.UnsupportedFormat:description"] =
                "Texture property {1} (channel {2}) on material slot {0} " +
                "uses the storage format {5}, which is outside the " +
                "formats AMUSE can prove. The texture is '{6}'. AMUSE " +
                "cannot use this capture as proof for the triangles " +
                "that sample it. The refusal reason is {3}, and the " +
                "texture {4}.",
```

`UnsupportedFormat:hint` (`:361-367`) adds the admitted format:

```csharp
            ["amuse.texture.UnsupportedFormat:hint"] =
                "Re-import the texture as RGBA32, ARGB32, Alpha8, RGB24, " +
                "DXT1, DXT5, or BC7. When the report says the texture " +
                "has no source identity, the build saw a texture that " +
                "no project asset backs. When it says the texture has a " +
                "source identity, the capture route refused a real " +
                "imported asset.",
```

- [ ] **Step 7: Run the tests and observe GREEN**

Same two filters. Expected: 2 tests, Passed.

- [ ] **Step 8: Run the neighboring texture and capture tests**

Filters `TextureCapture`, `RefusedFormatCapture`, `RefusedCaptureSlot`. Expected: all Pass; `RefusedFormatCaptureKeepsTheStandaloneDiagnosticEntry` asserts entry presence and reason, not the softened sentence. Record observed counts. If any test pins the old sentence, stop per the global stop conditions.

- [ ] **Step 9: Commit (authorization-gated)**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/TextureCaptureRefusal.cs Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs
git commit -m "fix: name the refused format and texture and stop claiming outcomes"
```

---

### Task 5: Poiyomi labels the alpha mask properties

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:976-991` (the `FeatureLabels` map)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiAlphaMaskTests.cs` (add one test inside the existing class)

**Interfaces:**
- Consumes: `PoiyomiMaterialSemantics.FeatureLabelFor(string property)` (`:964-974`), already `internal`.
- Produces: nothing new. `AmuseReports.FeatureSentence` (`AmuseReports.cs:143-160`) renders the label instead of the "a shader feature" fallback.

- [ ] **Step 1: Write the failing test**

Add to `PoiyomiAlphaMaskTests`:

```csharp
        [Test]
        public void AlphaMaskPropertiesCarryFeatureLabels()
        {
            // The refusal sentence must name the feature, not "a shader
            // feature": the alpha mask is supported for other values, and
            // lilToon already labels the same concept "Alpha mask".
            Assert.That(
                PoiyomiMaterialSemantics.FeatureLabelFor("_AlphaMaskValue"),
                Is.EqualTo("Alpha mask"));
            Assert.That(
                PoiyomiMaterialSemantics.FeatureLabelFor(
                    "_AlphaMaskBlendStrength"),
                Is.EqualTo("Alpha mask"));
            Assert.That(
                PoiyomiMaterialSemantics.FeatureLabelFor("_AlphaMaskInvert"),
                Is.EqualTo("Alpha mask"));
        }
```

- [ ] **Step 2: Run the test and observe RED**

Filter `AlphaMaskPropertiesCarryFeatureLabels`.

Expected: 1 test, Failed — `FeatureLabelFor` returns null for all three properties today. Record the observed count and message.

- [ ] **Step 3: Add the labels**

In `PoiyomiMaterialSemantics.cs`, inside the `FeatureLabels` initializer (`:976-991`), add:

```csharp
                ["_AlphaMaskValue"] = "Alpha mask",
                ["_AlphaMaskBlendStrength"] = "Alpha mask",
                ["_AlphaMaskInvert"] = "Alpha mask",
```

- [ ] **Step 4: Run the test and observe GREEN**

Same filter. Expected: 1 test, Passed.

- [ ] **Step 5: Run the neighboring alpha mask tests**

Filter `AlphaMask`. Expected: the existing Poiyomi alpha mask tests all Pass; none pins the "a shader feature" fallback (verified by search on 2026-10-06). Record observed counts.

- [ ] **Step 6: Commit (authorization-gated)**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiAlphaMaskTests.cs
git commit -m "fix: label the poiyomi alpha mask feature in refusals"
```

---

### Task 6: Documentation and full assembly run

**Files:**
- Modify: `README.md` (the console-reading section named "Reading the console reports")
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs` (key-presence assertions)

**Interfaces:**
- Consumes: everything Tasks 1-5 produced.
- Produces: nothing.

- [ ] **Step 1: Add string-table assertions**

In `AmuseReportStringsTests`, extend the existing key-coverage assertions with `Has` checks for the changed keys: `amuse.slotSeparation.OpaqueConversionRefused:description`, `amuse.renderer.LockedPoiyomiOriginalShaderUnattested`, `amuse.renderer.LockedPoiyomiOriginalShaderUnattested:description`, `amuse.renderer.MaterialDependencyClosureFailed`, `amuse.renderer.MaterialDependencyClosureFailed:description`, `amuse.texture.UnsupportedFormat:description`, `amuse.texture.UnsupportedFormat:hint`. Match the file's existing assertion style.

- [ ] **Step 2: Update the README section**

In "Reading the console reports", add one short paragraph: a conversion refusal names the material that refused; a locked-material refusal names the unverified original shader; a closure refusal names the renderer and the failure way; a format refusal names the storage format and the texture; a Poiyomi alpha mask refusal names the alpha mask. Simple english, one idea per sentence.

- [ ] **Step 3: Run the string tests**

Filter `AmuseReportStrings`. Expected: all Pass. Record observed counts.

- [ ] **Step 4: Run the full product assembly**

Run the full `Alrauna.Amuse.Tests.Editor` assembly, EditMode. Expected: every test Passes. Record the observed total. A run that reports 0 tests is a failure.

- [ ] **Step 5: Commit (authorization-gated)**

```bash
git add README.md Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs
git commit -m "docs: describe the refusal report naming fixes"
```
