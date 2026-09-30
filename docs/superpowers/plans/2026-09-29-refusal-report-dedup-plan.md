# Refusal Report Dedup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One NDMF console entry per refusal fact: fold texture capture facts into the slot refusal entry, and report consent declines and avatar animation refusals exactly once.

**Architecture:** Three reporting defects, three local changes. The finish pass renderer loop folds each refused slot's texture capture refusals into that slot's entry and gates the standalone texture entry on slot resolution. The declined-consent branch loses its inline duplicate call. The avatar refusal gate keeps the state record and the stop, and drops its duplicate report call. No classification, capture, or mutation behavior changes.

**Tech Stack:** Unity 2022.3.22f1, C#, NDMF 1.14.4 error reporting, NUnit through the Unity Test Framework, EditMode only.

**Spec:** `docs/superpowers/specs/2026-09-29-refusal-report-dedup-design.md`
Investigation: `docs/superpowers/investigations/2026-09-29-ndmf-console-duplicate-refusal-reporting.md`

## Global Constraints

- Branch `fix/duplicate-refusal-reports`, base `main` at `bff34a0`.
- Unity 2022.3.22f1, NDMF 1.14.4, VRChat SDK Base and Avatars `[3.10.4, 4.0.0)`.
- Tests run in the dev editor instance through the Unity Test Runner, EditMode mode, assembly `Alrauna.Amuse.Tests.Editor`. Tests never run in the Census Lab project. An agent drives runs through the editor MCP `run_tests` tool with the test filter, then polls `get_test_job`.
- RED before GREEN. Record the observed test count and the failing message for every RED state. A filtered run that reports 0 tests is a failure.
- Every user-visible string uses simple english: short active sentences, one idea per sentence, no semicolons, no contractions.
- Evidence records stay free of live Unity objects. `TextureCaptureRefusal` is a closed record of strings and enums.
- No production behavior change outside report text and report call sites. Refusals, classifications, and the mutation path stay byte-identical.
- Commit steps run only after the user authorizes commits for this branch in the execution session. Stage only the files the task names. Until then, leave changes unstaged.
- Stop conditions: the `slots` and `SlotResults` bases can diverge, a resolved slot with capture refusals appears in a real pipeline, a caller outside the finish pass needs the new parameter, or any RED state cannot be reached. Stop and report evidence.

---

### Task 1: Consent decline reports exactly once

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (declined-consent branch, the `ConsentDeclined` block near line 450)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` (add beside `DeclinedConsentStopsThePipelineWithoutAnalysis`)

**Interfaces:**
- Consumes: existing test helpers `FixtureAvatarIdentity.AttachVrcDescriptor`, `FixtureProofScope.PinAllSizes`, `SupportedFacts`, `TestGenericPlatform.Instance`, `OverrideTemporaryDirectoryScope`.
- Produces: nothing new. The helper `AmuseReports.ConsentDeclined` becomes the only reporter for the consent vocabulary.

- [ ] **Step 1: Write the failing test**

Add to `AmusePlatformFinishPluginTests`, directly after `DeclinedConsentStopsThePipelineWithoutAnalysis`:

```csharp
        [Test]
        public void DeclinedConsentReportsTheDeclinedEntryExactlyOnce()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE consent declined report fixture");
            FixtureAvatarIdentity.AttachVrcDescriptor(root);
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            FixtureProofScope.PinAllSizes(root);
            root.AddComponent<LineRenderer>();

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);

                var reports = ErrorReport.CaptureErrors(
                    () => AmusePlatformFinishPass.Execute(
                        context,
                        SupportedFacts(unityVersion: "2022.3.23f1"),
                        subjects => false));

                var declinedEntries = 0;
                foreach (var entry in reports)
                {
                    if (entry.TheError is SimpleError simple &&
                        simple.TitleKey == "amuse.consent.Declined")
                    {
                        declinedEntries++;
                    }
                }

                Assert.That(declinedEntries, Is.EqualTo(1),
                    "one consent decline is one console entry, never two");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
```

- [ ] **Step 2: Run the test and observe RED**

Run in the dev editor instance: Test Runner, EditMode, filter `DeclinedConsentReportsTheDeclinedEntryExactlyOnce`.

Expected: 1 test, Failed, with `Expected: 1 But was: 2` on the declined-entry count. Record the observed count and message.

- [ ] **Step 3: Delete the inline duplicate report**

In `AmusePlatformFinishPlugin.cs`, the declined branch currently reads:

```csharp
                state.ConsentDeclined = true;
                ErrorReport.ReportError(
                    AmuseReports.Localizer,
                    ErrorSeverity.Information,
                    "amuse.consent.Declined");
                AmuseReports.ConsentDeclined(subjects);
                return;
```

Replace it with:

```csharp
                state.ConsentDeclined = true;
                AmuseReports.ConsentDeclined(subjects);
                return;
```

`AmuseReports.ConsentDeclined` already reports `amuse.consent.Declined` and then one entry per subject.

- [ ] **Step 4: Run the test and observe GREEN**

Same filter. Expected: 1 test, Passed.

- [ ] **Step 5: Run the neighboring consent tests**

Filter `Consent`. Expected: `DeclinedConsentStopsThePipelineWithoutAnalysis`, `GrantedConsentLetsThePipelineRun`, and the new test all Pass. Record observed counts.

- [ ] **Step 6: Commit (authorization-gated)**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "fix: report a consent decline once"
```

---

### Task 2: Avatar animation refusal reports exactly once

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (the avatar refusal gate near line 492)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` (add beside `AvatarAnimationRefusalReportsPlainEnglishEntry`)

**Interfaces:**
- Consumes: existing test helpers `AttachProbeBehaviour`, `AddAnalyzableRenderer`, `SeedRetainedHostBindings`, `DestroyCommittedClone`, `DestroyControllerGraph`, `DisposeAnalyzableRenderer`, `SupportedFacts`, `TestGenericPlatform.Instance`.
- Produces: nothing new. `AmuseStructuralGraphCheck.Execute` stays the only reporter for the avatar animation refusal, on the pass path and on the inline fallback path.

- [ ] **Step 1: Write the failing test**

Add to `AmusePlatformFinishPluginTests`, directly after `AvatarAnimationRefusalReportsPlainEnglishEntry`:

```csharp
        [Test]
        public void AvatarAnimationRefusalReportsExactlyOneEntry()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE avatar refusal count fixture");
            FixtureAvatarIdentity.AttachVrcDescriptor(root);
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            FixtureProofScope.PinAllSizes(root);
            var controller = new AnimatorController { name = "unallowlisted" };
            var fixture = default(AnalyzableRendererFixture);

            try
            {
                controller.AddLayer("L0");
                var state = controller.layers[0].stateMachine.AddState("S0");
                var behaviour = AttachProbeBehaviour(state);
                Assert.That(behaviour, Is.Not.Null,
                    "fixture precondition: Unity did not attach the probe behaviour");

                root.AddComponent<Animator>().runtimeAnimatorController =
                    controller;
                fixture = AddAnalyzableRenderer(root);

                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);
                SeedRetainedHostBindings(context);

                var reports = ErrorReport.CaptureErrors(
                    () => AmusePlatformFinishPass.Execute(
                        context, SupportedFacts()));

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(amuse.AvatarRefusal, Is.EqualTo(
                        AvatarAnimationRefusal.UnrecognizedStateMachineBehaviour),
                    "fixture precondition: the unallowlisted behaviour must " +
                    "refuse the avatar");

                var avatarEntries = 0;
                foreach (var entry in reports)
                {
                    if (entry.TheError is SimpleError simple &&
                        simple.TitleKey.StartsWith("amuse.avatar."))
                    {
                        avatarEntries++;
                    }
                }

                Assert.That(avatarEntries, Is.EqualTo(1),
                    "one avatar refusal is one console entry, never two");
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                DestroyCommittedClone(root, controller);
                Object.DestroyImmediate(root);
                DestroyControllerGraph(controller);
            }
        }
```

- [ ] **Step 2: Run the test and observe RED**

Filter `AvatarAnimationRefusalReportsExactlyOneEntry`.

Expected: 1 test, Failed, with `Expected: 1 But was: 2` on the avatar-entry count. The inline fallback path runs `AmuseStructuralGraphCheck.Execute` inside the barrier, which reports, and then the gate reports again. Record the observed count and message.

- [ ] **Step 3: Drop the duplicate report from the gate**

In `AmusePlatformFinishPlugin.cs`, the gate currently reads:

```csharp
            if (graph.Refusal != AvatarAnimationRefusal.None)
            {
                // Avatar scope: the exact named cause is preserved and the whole
                // avatar stops. No renderer is analyzed, so no partial result and
                // no per-renderer accounting can survive. V7: the stop is never
                // silent - one plain English entry names the cause.
                state.AvatarRefusal = graph.Refusal;
                AmuseReports.AvatarRefusal(
                    context.AvatarRootObject, graph.Refusal);
                return;
            }
```

Replace it with:

```csharp
            if (graph.Refusal != AvatarAnimationRefusal.None)
            {
                // Avatar scope: the exact named cause is preserved and the whole
                // avatar stops. No renderer is analyzed, so no partial result and
                // no per-renderer accounting can survive. The structural graph
                // check pass named the cause, on the pass path and on the inline
                // fallback path. This gate records the state and stops.
                state.AvatarRefusal = graph.Refusal;
                return;
            }
```

- [ ] **Step 4: Run the test and observe GREEN**

Same filter. Expected: 1 test, Passed.

- [ ] **Step 5: Run the neighboring avatar refusal tests**

Filter `AvatarRefusal` and filter `UnallowlistedBehaviour`. Expected: `UnallowlistedBehaviourRefusesTheWholeAvatarWithoutAnalysis`, `AvatarAnimationRefusalReportsPlainEnglishEntry`, and the new test all Pass. `AvatarAnimationRefusalReportsPlainEnglishEntry` asserts `Has.Some`, so one entry satisfies it. Record observed counts.

- [ ] **Step 6: Commit (authorization-gated)**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "fix: report an avatar animation refusal once"
```

---

### Task 3: Fold texture capture facts into the refused slot entry

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs` (`SlotAnalysisRefusal` plus one private helper)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs` (one new entry beside the `amuse.slotAnalysis` block)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (the renderer loop: slot report near line 675, texture loop near line 713)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs` (add beside `RefusedCaptureSurfacesOnItsSlotAndTheSiblingSlotStaysConvertible`)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs` (two reporter-level tests)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs` (one `Has` assertion)

**Interfaces:**
- Consumes: `TextureCaptureRefusal` (closed record: `PropertyName`, `HasSourceIdentity`, `SourceIdentity`, `Channel`, `Reason`), `CapturedMaterialSlotEvidence.CaptureRefusals`, `resolved.SlotResults[i].IsResolved`.
- Produces: `AmuseReports.SlotAnalysisRefusal(Renderer renderer, int slotIndex, RendererAnalysisRefusal cause, string rendererName = null, CapturedAlphaMaterial offender = null, AlphaUnknownReason unknownReason = null, IReadOnlyList<TextureCaptureRefusal> captureRefusals = null)`. The new parameter is trailing and optional, so every existing call site compiles unchanged. The string key `amuse.slotAnalysis.CaptureRefused` with substitutions `{0}` property, `{1}` channel, `{2}` reason.

- [ ] **Step 1: Write the failing integration test**

Add to `AlphaSeparationPreparationTests`, directly after `RefusedCaptureSurfacesOnItsSlotAndTheSiblingSlotStaysConvertible`. The fixture copies that test's setup with one deliberate change: slot 0's `_MainTex` is a runtime in-memory `Texture2D`, so the identity gate refuses with `UnavailableCapture` and the slot's proof lands `SemanticsUnknown`. The assertions read the report stream:

```csharp
        /// <summary>
        /// One refused slot is one console entry. The entry folds the
        /// slot's texture capture facts, and no standalone texture entry
        /// repeats them. The sibling slot's conversion is untouched.
        /// <para>
        /// Falsifies: a fold that keeps the standalone texture entry, a
        /// fold that drops the capture facts from the slot entry, and a
        /// suppression that loses the renderer name or the material.
        /// </para>
        /// </summary>
        [Test]
        public void RefusedCaptureSlotReportsOneFoldedEntry()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE refused capture fold fixture");
            FixtureAvatarIdentity.AttachVrcDescriptor(root);
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            FixtureProofScope.PinAllSizes(root);
            Material refusedMaterial = null;
            Material convertedMaterial = null;
            Mesh mesh = null;
            AmusePlatformFinishState amuse = null;
            Texture2D refusedTexture = null;
            var fixtures = new PoiyomiTextureBackedFixtures();

            try
            {
                fixtures.BaseSetUp();
                refusedTexture = new Texture2D(
                    8, 8, TextureFormat.RGBA32, false);
                refusedMaterial = TextureBackedNonIdentityStMaterial(
                    refusedTexture, Vector2.one, Vector2.zero);
                convertedMaterial = TextureBackedNonIdentityStMaterial(
                    fixtures.ImportFullyOpaqueMipmap("fold_convertible_slot"),
                    Vector2.one, Vector2.zero);
                AddTwoTriangleRenderer(
                    root, refusedMaterial, convertedMaterial, out mesh);
                mesh.uv = new[]
                {
                    new Vector2(0.25f, 0.25f),
                    new Vector2(0.25f, 0.25f),
                    new Vector2(0.25f, 0.25f),
                    new Vector2(0.25f, 0.25f),
                    new Vector2(0.25f, 0.25f),
                    new Vector2(0.25f, 0.25f),
                };

                var reports = ErrorReport.CaptureErrors(
                    () => amuse = RunBarrier(root));

                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(amuse.AnalyzedRendererCount, Is.EqualTo(1),
                    "fixture precondition: the renderer must analyze");

                var slotEntries = new List<string>();
                var textureEntries = 0;
                foreach (var entry in reports)
                {
                    if (entry.TheError is SimpleError simple)
                    {
                        if (simple.TitleKey == "amuse.slotAnalysis.Refusal")
                        {
                            slotEntries.Add(entry.TheError.ToMessage());
                        }

                        if (simple.TitleKey.StartsWith("amuse.texture."))
                        {
                            textureEntries++;
                        }
                    }
                }

                Assert.That(slotEntries, Has.Count.EqualTo(1),
                    "the refused slot reports exactly one entry");
                Assert.That(slotEntries[0], Does.Contain("_MainTex"),
                    "the folded entry names the refused texture property");
                Assert.That(slotEntries[0], Does.Contain("Alpha"),
                    "the folded entry names the refused channel");
                Assert.That(slotEntries[0], Does.Contain("UnavailableCapture"),
                    "the folded entry names the capture reason");
                Assert.That(textureEntries, Is.Zero,
                    "a refused slot's texture facts ride the slot entry, " +
                    "so no standalone texture entry repeats them");
            }
            finally
            {
                DestroyGenerated(amuse);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (refusedMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(refusedMaterial);
                }
                if (convertedMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(convertedMaterial);
                }
                if (refusedTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(refusedTexture);
                }
            }
        }

        /// <summary>
        /// Characterization for the resolved-slot diagnostic branch. An
        /// imported texture whose format the capture route refuses leaves
        /// the slot RESOLVED with Unknown triangle outcomes, so the slot
        /// loop prints no slot entry and the standalone texture entry is
        /// the only report. This holds before and after the fold, and it
        /// keeps the resolved-slot gate covered end to end.
        /// </summary>
        [Test]
        public void RefusedFormatCaptureKeepsTheStandaloneDiagnosticEntry()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE refused format diagnostic fixture");
            FixtureAvatarIdentity.AttachVrcDescriptor(root);
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            FixtureProofScope.PinAllSizes(root);
            Material refusedMaterial = null;
            Material convertedMaterial = null;
            Mesh mesh = null;
            AmusePlatformFinishState amuse = null;
            var fixtures = new PoiyomiTextureBackedFixtures();

            try
            {
                fixtures.BaseSetUp();
                refusedMaterial = TextureBackedNonIdentityStMaterial(
                    fixtures.ImportRefusedFormatMipmap("diagnostic_refused_slot"),
                    Vector2.one, Vector2.zero);
                convertedMaterial = TextureBackedNonIdentityStMaterial(
                    fixtures.ImportFullyOpaqueMipmap("diagnostic_convertible_slot"),
                    Vector2.one, Vector2.zero);
                AddTwoTriangleRenderer(
                    root, refusedMaterial, convertedMaterial, out mesh);
                mesh.uv = new[]
                {
                    new Vector2(0.25f, 0.25f),
                    new Vector2(0.25f, 0.25f),
                    new Vector2(0.25f, 0.25f),
                    new Vector2(0.25f, 0.25f),
                    new Vector2(0.25f, 0.25f),
                    new Vector2(0.25f, 0.25f),
                };

                var reports = ErrorReport.CaptureErrors(
                    () => amuse = RunBarrier(root));

                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(amuse.AnalyzedRendererCount, Is.EqualTo(1),
                    "fixture precondition: the renderer must analyze");

                var slotEntries = 0;
                var textureEntries = 0;
                foreach (var entry in reports)
                {
                    if (entry.TheError is SimpleError simple)
                    {
                        if (simple.TitleKey == "amuse.slotAnalysis.Refusal")
                        {
                            slotEntries++;
                        }

                        if (simple.TitleKey.StartsWith("amuse.texture."))
                        {
                            textureEntries++;
                        }
                    }
                }

                Assert.That(slotEntries, Is.Zero,
                    "the unsupported-format refusal resolves its slot with " +
                    "unknown outcomes, so no slot entry exists");
                Assert.That(textureEntries, Is.EqualTo(1),
                    "the resolved slot keeps the standalone texture entry " +
                    "as its only report");
            }
            finally
            {
                DestroyGenerated(amuse);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (refusedMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(refusedMaterial);
                }
                if (convertedMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(convertedMaterial);
                }
            }
        }
```

- [ ] **Step 2: Run the test and observe RED**

Run both new tests with filter group_names regex `RefusedCaptureSlotReportsOneFoldedEntry|RefusedFormatCaptureKeepsTheStandaloneDiagnosticEntry`. Name the exact regex in the report.

Expected: 2 tests, 1 Failed, 1 Passed. The characterization test (`RefusedFormatCaptureKeepsTheStandaloneDiagnosticEntry`) passes on current code and is recorded as characterization, never dressed up as RED. The fold test fails because on current code the slot entry exists but carries no texture facts: the message lacks `_MainTex`, `Alpha`, and `UnavailableCapture`, and the texture-entry count reads `Expected: 0 But was: 1`. The in-memory texture drives the slot's proof to `SemanticsUnknown`, so the slot refuses today; the count assertion `Has.Count.EqualTo(1)` passes today and becomes load-bearing only at GREEN. Record every observed count and message.

- [ ] **Step 3: Add the string-table entry**

In `AmuseReportStrings.cs`, beside the `amuse.slotAnalysis.Refusal` block:

```csharp
            ["amuse.slotAnalysis.CaptureRefused"] =
                "Texture property {0} (channel {1}) could not be captured. " +
                "Reason: {2}. The proof for this slot has no texture data.",
```

- [ ] **Step 4: Extend the slot reporter**

In `AmuseReports.cs`, change the signature:

```csharp
        internal static void SlotAnalysisRefusal(
            Renderer renderer,
            int slotIndex,
            RendererAnalysisRefusal cause,
            string rendererName = null,
            CapturedAlphaMaterial offender = null,
            AlphaUnknownReason unknownReason = null,
            IReadOnlyList<TextureCaptureRefusal> captureRefusals = null)
```

Replace the last report argument `FeatureSentence(unknownReason)` with a composed sentence, and add the private helper:

```csharp
            var evidenceSentence = FeatureSentence(unknownReason);
            var captureSentence = CaptureSentence(captureRefusals);
            if (evidenceSentence.Length == 0)
            {
                evidenceSentence = captureSentence;
            }
            else if (captureSentence.Length > 0)
            {
                evidenceSentence = evidenceSentence + " " + captureSentence;
            }
```

```csharp
        /// <summary>
        /// One sentence per texture capture refusal of this slot, so the
        /// slot entry names the property, the channel, and the reason the
        /// proof lost its texture data. Empty when the slot carries no
        /// capture refusal.
        /// </summary>
        private static string CaptureSentence(
            IReadOnlyList<TextureCaptureRefusal> captureRefusals)
        {
            if (captureRefusals == null || captureRefusals.Count == 0)
            {
                return "";
            }

            var sentences = new List<string>(captureRefusals.Count);
            foreach (var refusal in captureRefusals)
            {
                sentences.Add(string.Format(
                    AmuseReportStrings.Get(
                        "amuse.slotAnalysis.CaptureRefused"),
                    refusal.PropertyName,
                    refusal.Channel.ToString(),
                    refusal.Reason.ToString()));
            }

            return string.Join(" ", sentences);
        }
```

The XML doc comment on `SlotAnalysisRefusal` gains: one refused slot is one entry, and the caller passes the slot's capture refusals so the entry carries their facts.

- [ ] **Step 5: Rewire the finish pass renderer loop**

In `AmusePlatformFinishPlugin.cs`, hoist the slot construction and pass the refusals:

```csharp
                refusal = resolved.Refusal;
                var slots = MaterialSlotsFor(
                    evidence, rendererPath, rendererTypeName);
                // Every refused slot reports its own exact reason, even
                // when the renderer as a whole continues or the first
                // refusal names the renderer: one line per slot, no
                // aggregation, so a manual test can read the full list.
                // The slot entry folds the slot's texture capture facts,
                // so one refused slot is one console entry. The slots and
                // the slot results come from one MaterialSlotsFor call on
                // one evidence record, so slot i of one is slot i of the
                // other, and a divergence is an invariant defect.
                for (var slotIndex = 0;
                     slotIndex < resolved.SlotResults.Length;
                     slotIndex++)
                {
                    if (resolved.SlotResults[slotIndex].IsResolved)
                    {
                        continue;
                    }

                    AmuseReports.SlotAnalysisRefusal(
                        renderer,
                        slotIndex,
                        resolved.SlotResults[slotIndex].Refusal,
                        renderer.gameObject.name,
                        resolved.SlotResults[slotIndex].Offender,
                        resolved.SlotResults[slotIndex].UnknownReason,
                        slots[slotIndex].CaptureRefusals);
                }
```

Inside the extraction branch, delete the inner declaration `var slots = MaterialSlotsFor(evidence, rendererPath, rendererTypeName);`, because the hoisted list is in scope, and gate the capture-refusal loop on slot resolution:

```csharp
                        for (var slotIndex = 0;
                             slotIndex < slots.Count;
                             slotIndex++)
                        {
                            // A refused slot's own entry carries the
                            // capture facts. A standalone texture entry
                            // for that slot would state one fact twice,
                            // so it reports only for resolved slots. The
                            // no-alpha-channel diagnostic refuses nothing
                            // and names a different fact, so it stays
                            // unconditional.
                            if (resolved.SlotResults[slotIndex].IsResolved)
                            {
                                foreach (var textureRefusal in
                                         slots[slotIndex].CaptureRefusals)
                                {
                                    AmuseReports.TextureCaptureRefusal(
                                        renderer,
                                        slotIndex,
                                        textureRefusal);
                                }
                            }

                            foreach (var propertyName in
                                     slots[slotIndex].NoAlphaChannelProperties)
                            {
                                AmuseReports.TextureAlphaMissing(
                                    renderer,
                                    slotIndex,
                                    propertyName);
                            }
                        }
```

- [ ] **Step 6: Run the integration test and observe GREEN**

Filter `RefusedCaptureSlotReportsOneFoldedEntry`. Expected: 1 test, Passed.

- [ ] **Step 7: Add the reporter-level contract tests**

Add to `AmuseReportsSlotTests`:

```csharp
        [Test]
        public void SlotAnalysisRefusalEmbedsCaptureRefusalFacts()
        {
            var refusal = new TextureCaptureRefusal(
                "_MainTex",
                false,
                default,
                TextureChannel.Alpha,
                TextureCaptureRefusalReason.UnavailableCapture);

            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotAnalysisRefusal(
                    _renderer,
                    3,
                    RendererAnalysisRefusal.AnimatedMaterialPropertyNotSingleton,
                    captureRefusals: new[] { refusal }));

            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("_MainTex"));
            Assert.That(message, Does.Contain("Alpha"));
            Assert.That(message, Does.Contain("UnavailableCapture"));
        }

        [Test]
        public void SlotAnalysisRefusalEmbedsEveryCaptureRefusal()
        {
            var alpha = new TextureCaptureRefusal(
                "_MainTex",
                false,
                default,
                TextureChannel.Alpha,
                TextureCaptureRefusalReason.UnavailableCapture);
            var red = new TextureCaptureRefusal(
                "_MaskTex",
                true,
                default,
                TextureChannel.Red,
                TextureCaptureRefusalReason.NonResidentMips);

            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotAnalysisRefusal(
                    _renderer,
                    1,
                    RendererAnalysisRefusal.AnimatedMaterialPropertyNotSingleton,
                    captureRefusals: new[] { alpha, red }));

            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("_MainTex"));
            Assert.That(message, Does.Contain("UnavailableCapture"));
            Assert.That(message, Does.Contain("_MaskTex"),
                "a fold that keeps only the first refusal loses facts");
            Assert.That(message, Does.Contain("NonResidentMips"));
        }
```

The second test is the falsifier for a first-refusal-only fold.

- [ ] **Step 8: Add the string-table assertion**

In `AmuseReportStringsTests`, inside the test that asserts the consent keys, add:

```csharp
            Assert.That(
                AmuseReportStrings.Has("amuse.slotAnalysis.CaptureRefused"),
                Is.True);
```

- [ ] **Step 9: Run the focused filters**

Filters `SlotAnalysisRefusal`, `TextureCaptureRefusal`, and `RefusedCapture` (the latter covers both new tests). Expected: the new tests and the existing `AmuseReportsSlotTests` texture tests all Pass. The standalone `TextureCaptureRefusal` reporter keeps its behavior, so its tests stay green. Record observed counts.

- [ ] **Step 10: Commit (authorization-gated)**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs
git commit -m "fix: fold texture capture facts into the refused slot entry"
```

---

### Task 4: Full validation, documentation, and sweep

**Files:**
- Modify: `README.md` (the "Reading the console reports" section)

**Interfaces:**
- Consumes: all three tasks complete.

- [ ] **Step 1: Run the full product assembly**

Run the whole `Alrauna.Amuse.Tests.Editor` assembly, EditMode, in the dev editor instance. Expected: every test Passes. Record the observed pass and fail counts. A filtered or full run that reports 0 tests is a failure.

- [ ] **Step 2: Document the fold in the README**

In the "Reading the console reports" section, add:

```markdown
When a slot's refusal comes from a texture the route could not read, the slot's single line names the texture property, the channel, and the reason. A texture line on its own means the slot kept its proof and the line is a diagnostic only.
```

- [ ] **Step 3: Whitespace and identifier sweep**

Run `git diff --check`. Expected: no output. Then sweep every changed file for an at sign joined to a hexadecimal hash, drive-letter paths, home-directory paths, four-digit ports, and every private asset name known to the session. Every hit is a defect. Fix the hits before reporting.

- [ ] **Step 4: Commit (authorization-gated)**

```bash
git add README.md
git commit -m "docs: describe the folded texture facts in the console report section"
```

---

## Self-review notes

- Spec coverage: family one is Task 3, family two is Task 1, family three is Task 2, and the documentation sentence is Task 4. The non-goal items appear in no task, by design.
- Existing tests: none pin the duplicate counts. `AvatarAnimationRefusalReportsPlainEnglishEntry` asserts `Has.Some`, so it survives the dedup. `DeclinedConsentStopsThePipelineWithoutAnalysis` asserts state only. The `AmuseReportsSlotTests` texture reporter tests cover the reporter that stays.
- Type consistency: the new parameter name is `captureRefusals` at the declaration and at both test call sites. The string key is `amuse.slotAnalysis.CaptureRefused` at the table, in `CaptureSentence`, and in the strings test.
