# Unassigned MainTex Declared-Default Proof Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove an unassigned `_MainTex` in the two lilToon alpha frontends as the declared white default (alpha collapses to `_Color.a`), and print the capture reason family and source-identity flag in texture-capture refusal reports.

**Architecture:** Each frontend replaces its `!IsAssigned` Unknown guard with a declared-default arm that collapses the main sample to `ScalarSemanticValue.Constant(colorAlpha)`, mirroring the existing `SampledAlphaIsProvenOne` collapse. A sampled alpha mask over an unassigned main refuses with `UnsupportedSampling` naming `_MainTex`, because a mask borrows the main sampler. The capture-refusal report gains two format arguments. No capture, request, resolver, or classifier change.

**Tech Stack:** Unity 2022.3.22f1 editor assembly (`Alrauna.Amuse.Editor`), NUnit via Unity Test Framework, EditMode only. Tests run in the dev editor instance through the Test Runner; there is no CLI test runner.

**Spec:** `docs/superpowers/specs/2026-09-26-maintex-unassigned-default-design.md`

## Global Constraints

- Tests never run in the Census Lab project. Every test run happens in the dev editor instance against this repository.
- Never stage or commit without explicit authorization at execution time. Commit steps below carry exact messages for when authorization is given.
- One closed refusal vocabulary per scope: reuse `LilToonSemanticDiagnosticCode.UnsupportedSampling`; add no new enum value.
- User-visible report strings follow the house string style: short sentences, active voice, one idea per sentence, no contractions.
- XML doc comments on load-bearing rules state why, not what.
- A filtered test run that reports 0 tests is a failure. Record observed counts.
- After the last code task, run `git diff --check` and sweep changed files for identifiers (at-sign joined to a hex hash, drive-letter paths, home-directory paths, four-digit ports, private asset names).

---

### Task 1: Cutout declared-default arm

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs` (the `InterpretCutoutAlpha` texture arm, around lines 397-530)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs`

**Interfaces:**
- Consumes: existing `LilToonAlphaMaskTermKind.Sample`, `LilToonSemanticDiagnosticCode.UnsupportedSampling`, `ScalarSemanticValue.Constant`, `AlphaFieldProvider` delegate type, `AlphaSemanticsResolver.Resolve(alpha, provider, cutoff)`.
- Produces: for an unassigned `_MainTex` with a non-sampled mask, `InterpretVerifiedCutoutAlpha` completes `Constant(colorAlpha)`; with a sampled mask, it records `UnsupportedSampling` naming `_MainTex`. Task 2 mirrors this shape; nothing else consumes these internals.

- [ ] **Step 1: Write the failing tests**

Add to `LilToonCutoutAlphaTests.cs`, in a new region after the last existing test region, before the private helpers section:

```csharp
        // --- declared-default arm: the unassigned Main Texture -------------

        [Test]
        public void UnassignedMainTex_AtUnitTint_ProvesTheCornerTriangleOpaque()
        {
            var material = NewCutoutFixtureMaterial();

            var resolution = ResolveThroughUnassignedCutout(material);

            // Falsifies: consulting any texel for an unassigned _MainTex.
            Assert.That(resolution.IsResolved, Is.True);
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void UnassignedMainTex_BelowCutoff_IsUniformMustRemainTransparent()
        {
            var material = NewCutoutFixtureMaterial();
            material.SetColor(ColorProperty, new Color(1f, 1f, 1f, 0.25f));

            var resolution = ResolveThroughUnassignedCutout(material);

            // Falsifies: treating the declared-default constant as opaque
            // regardless of the cutout comparison.
            Assert.That(resolution.IsResolved, Is.True);
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void UnassignedMainTex_WithSampledAlphaMask_RefusesSampling()
        {
            var material = NewCutoutFixtureMaterial();
            material.SetFloat("_AlphaMaskMode", 2f);
            material.SetTexture("_AlphaMask", ImportTexture("mask_unassigned"));

            // Falsifier 1: a sampled mask borrows _MainTex's captured
            // sampler, so the declared-default arm must refuse it with the
            // sampling code, not prove through borrowed facts. Before the
            // arm exists this fails with UnsupportedFeature instead.
            var result =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutMaterial(
                    material, ColorSpace.Linear, AllFeatures);

            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Alpha)
                    .Any(d =>
                        d.Code == LilToonSemanticDiagnosticCode
                            .UnsupportedSampling &&
                        d.Detail.Contains("_MainTex")),
                Is.True,
                "expected UnsupportedSampling naming _MainTex");
        }

        private AlphaResolution ResolveThroughUnassignedCutout(
            Material material)
        {
            var captured = CaptureCutoutEvidence(material);
            var alpha =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutAlpha(
                    captured);
            return AlphaSemanticsResolver.Resolve(
                alpha, UnusedProvider(), 0);
        }

        private static AlphaFieldProvider UnusedProvider()
        {
            return (TextureSourceId source, TextureChannel channel,
                out AlphaMipChain result) =>
            {
                result = null;
                throw new InvalidOperationException(
                    "the declared-default collapse must not consult a texel");
            };
        }
```

- [ ] **Step 2: Run the tests and watch them fail**

Run in the dev editor instance: refresh assets, then the EditMode filter `Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonCutoutAlphaTests`.
Expected: 3 run, 3 failed. The first two fail on `IsResolved` (False, because the guard returns Unknown). The falsifier fails on the diagnostic-code assertion (UnsupportedFeature instead of UnsupportedSampling). Record the observed counts.

- [ ] **Step 3: Implement the arm**

In `LilToonCutoutMaterialSemantics.cs`, replace the whole region from the mask-constant early return through the final `return SemanticOutput<ScalarSemanticValue>.Complete(alphaChain);` with:

```csharp
            // Replace mode with a constant term samples nothing at all:
            // the alpha is that constant, so no texture gate applies.
            if (maskTerm.Kind == LilToonAlphaMaskTermKind.Constant)
            {
                return SemanticOutput<ScalarSemanticValue>.Complete(
                    ScalarSemanticValue.Constant(maskTerm.Constant));
            }

            // Texture-backed arm (B2 basis). An unassigned _MainTex takes
            // the declared-default arm first; an assigned texture keeps
            // every captured-fact gate below, in the same order as before.
            var hasMainSampler = false;
            ScalarSemanticValue alphaChain;
            if (!evidence.TryGetTexture(
                    MainTextureProperty, out var assignment) ||
                !assignment.IsAssigned)
            {
                // Declared-default arm (design 2026-09-26): every attested
                // lilToon source declares _MainTex = "white" {}, and the
                // canonical digests pin that Properties block. Playback
                // binds the declared default when a material assigns no
                // texture, so the sample is exactly one at every texel and
                // coordinate-independent: the main sample collapses to its
                // constant exactly as the importer theorem does for the
                // assigned case. The digest is the enforcement: a vendor
                // change to the default breaks attestation before this arm
                // can run.
                alphaChain = ScalarSemanticValue.Constant(colorAlpha);
            }
            else
            {
                if (!assignment.Texture.HasSampling)
                {
                    return RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        LilToonSemanticOutput.Alpha,
                        LilToonSemanticDiagnosticCode.UnsupportedSampling,
                        MainTextureProperty);
                }

                if (!assignment.HasScaleOffset)
                {
                    return RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        LilToonSemanticOutput.Alpha,
                        LilToonSemanticDiagnosticCode.UnsupportedFeature,
                        MainTextureProperty);
                }

                // Identity is tested exactly, per binary32 component. Unity's
                // Vector2 ==/!= is deliberately not used here: it is epsilon-based
                // (equal when the difference magnitude is under 1e-5), so it would
                // let near-identity ST past this C4 boundary and into the
                // family-blind affine resolver, whose own identity test is exact.
                // -0.0f stays admitted: -0.0f != 0f is false, and +-0 are
                // equivalent for this coordinate model.
                if (assignment.Scale.x != 1f ||
                    assignment.Scale.y != 1f ||
                    assignment.Offset.x != 0f ||
                    assignment.Offset.y != 0f)
                {
                    return RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        LilToonSemanticOutput.Alpha,
                        LilToonSemanticDiagnosticCode.UnsupportedUv,
                        MainTexStProperty);
                }

                hasMainSampler = true;

                // The shader composes, in order: main alpha, second layer,
                // third layer, alpha mask, dissolve, clip. The base below is
                // the main alpha; the layers compose onto it; the mask term
                // composes last. The cutout clip by _Cutoff applies after
                // everything and is the classifier's declared cutoff, so it
                // composes nowhere here.
                if (assignment.Texture.SampledAlphaIsProvenOne)
                {
                    // The importer theorem: a source without an alpha channel
                    // and an import that writes none samples alpha exactly one
                    // at every texel of every level, so the main sample
                    // collapses to its constant and no main field is read.
                    alphaChain = ScalarSemanticValue.Constant(colorAlpha);
                }
                else
                {
                    if (!assignment.Texture.HasSourceIdentity)
                    {
                        return RecordUnknown<ScalarSemanticValue>(
                            diagnostics,
                            LilToonSemanticOutput.Alpha,
                            LilToonSemanticDiagnosticCode
                                .UnstableTextureIdentity,
                            MainTextureProperty);
                    }

                    // uvMain is UV0 under the identity gates above.
                    var mainSample = new TextureSample(
                        assignment.Texture.SourceIdentity,
                        new UvMapping(0, assignment.Scale, assignment.Offset),
                        assignment.Texture.Sampling);
                    alphaChain = colorAlpha == 1f
                        ? ScalarSemanticValue.Texture(
                            mainSample, TextureChannel.Alpha)
                        : ScalarSemanticValue.TextureTimesConstant(
                            mainSample, TextureChannel.Alpha, colorAlpha);
                }
            }

            TextureSample maskSample = null;
            if (maskTerm.Kind == LilToonAlphaMaskTermKind.Sample)
            {
                // The mask borrows _MainTex's captured sampler facts. An
                // unassigned main has none, so a sampled mask refuses by
                // name instead of proving through borrowed facts. A later
                // design can attest the default sampler; this one refuses.
                if (!hasMainSampler)
                {
                    return RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        LilToonSemanticOutput.Alpha,
                        LilToonSemanticDiagnosticCode.UnsupportedSampling,
                        MainTextureProperty);
                }

                maskSample = new TextureSample(
                    maskTerm.Source,
                    maskTerm.Mapping,
                    assignment.Texture.Sampling);

                // A replace mask runs after the layers, so the mask term is
                // the whole alpha: neither _MainTex's texels nor _Color.a nor
                // any layer reaches the value.
                if (maskTerm.ReplacesMainAlpha)
                {
                    return SemanticOutput<ScalarSemanticValue>.Complete(
                        ScalarSemanticValue.Texture(
                            maskSample, TextureChannel.Red));
                }
            }

            // The layers, in shader order. Their writers sit before the mask.
            alphaChain = ComposeLayer(
                alphaChain, evidence, third: false, diagnostics);
            if (alphaChain == null)
            {
                return SemanticOutput<ScalarSemanticValue>.Unknown();
            }

            alphaChain = ComposeLayer(
                alphaChain, evidence, third: true, diagnostics);
            if (alphaChain == null)
            {
                return SemanticOutput<ScalarSemanticValue>.Unknown();
            }

            if (maskSample != null)
            {
                // Multiply mode: the term composes over the layered value.
                // The multiply cannot fold into a saturating sum or
                // difference below it, so those shapes refuse.
                var multiplied = Multiply(
                    alphaChain,
                    ScalarSemanticValue.TextureTimesConstant(
                        maskSample, TextureChannel.Red, 1f),
                    AlphaMaskModeProperty,
                    diagnostics);
                if (multiplied == null)
                {
                    return SemanticOutput<ScalarSemanticValue>.Unknown();
                }

                alphaChain = multiplied;
            }

            return SemanticOutput<ScalarSemanticValue>.Complete(alphaChain);
```

The assigned-path behavior is byte-identical to before: the same gates run in the same order for an assigned texture, and the mask-sample construction reads the same values. Only the previously-failing unassigned guard changes.

- [ ] **Step 4: Run the tests and watch them pass**

Run the same EditMode filter as Step 2.
Expected: 3 run, 3 passed. Record the observed counts.

- [ ] **Step 5: Run the whole cutout class**

Run the EditMode filter `Alrauna.Amuse.Tests.Editor.Semantics.LilToon` (all lilToon test classes).
Expected: 0 failures. Any failure in an existing assigned-path test is a regression in the restructure; fix before continuing. Record the observed counts.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs
git commit -m "feat: prove an unassigned main texture as the declared white default in the cutout family"
```

---

### Task 2: Transparent declared-default arm

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs` (the `InterpretTransparentAlpha` texture arm, around lines 469-620)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs`

**Interfaces:**
- Consumes: the same shape Task 1 produced; this task mirrors it independently, per the repository rule that each family answers its own source.
- Produces: for an unassigned `_MainTex` with a non-sampled mask, `InterpretVerifiedTransparentAlpha` completes `Constant(colorAlpha)`; with a sampled mask, `UnsupportedSampling` naming `_MainTex`.

- [ ] **Step 1: Write the failing tests**

Add to `LilToonTransparentAlphaTests.cs`, after the last test region, before the private helpers section:

```csharp
        // --- declared-default arm: the unassigned Main Texture -------------

        [Test]
        public void UnassignedMainTex_AtUnitTint_ProvesTheCornerTriangleOpaque()
        {
            var material = NewTransparentFixtureMaterial();

            var resolution = ResolveThroughUnassignedTransparent(material);

            // Falsifies: consulting any texel for an unassigned _MainTex.
            // The transparent theorem has no clip, so exactly one proves
            // opaque outright.
            Assert.That(resolution.IsResolved, Is.True);
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void UnassignedMainTex_SubUnitTint_IsUniformMustRemainTransparent()
        {
            var material = NewTransparentFixtureMaterial();
            material.SetColor(ColorProperty, new Color(1f, 1f, 1f, 0.8f));

            var resolution = ResolveThroughUnassignedTransparent(material);

            // Falsifies: ignoring _Color.a, as the tint-multiplier row does
            // for the assigned arm.
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void UnassignedMainTex_WithSampledAlphaMask_RefusesSampling()
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_AlphaMaskMode", 2f);
            material.SetTexture("_AlphaMask", ImportTexture("t_mask_unassigned"));

            // Falsifier 2: the transparent mask borrows the main sampler
            // exactly as the cutout mask does.
            var result = InterpretTransparent(material);

            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Alpha)
                    .Any(d =>
                        d.Code == LilToonSemanticDiagnosticCode
                            .UnsupportedSampling &&
                        d.Detail.Contains("_MainTex")),
                Is.True,
                "expected UnsupportedSampling naming _MainTex");
        }

        private AlphaResolution ResolveThroughUnassignedTransparent(
            Material material)
        {
            var captured = CaptureTransparentEvidence(material);
            var alpha = LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentAlpha(captured);
            return AlphaSemanticsResolver.Resolve(
                alpha, UnusedProvider(), 0);
        }

        private static AlphaFieldProvider UnusedProvider()
        {
            return (TextureSourceId source, TextureChannel channel,
                out AlphaMipChain result) =>
            {
                result = null;
                throw new InvalidOperationException(
                    "the declared-default collapse must not consult a texel");
            };
        }
```

- [ ] **Step 2: Run the tests and watch them fail**

Run the EditMode filter `Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonTransparentAlphaTests`.
Expected: 3 run, 3 failed, for the same reasons as Task 1 Step 2. Record the observed counts.

- [ ] **Step 3: Implement the arm**

The transparent method has the same shape as the cutout method. Apply the same restructure, with these exact edits:

The unassigned branch that replaces the failing guard is:

```csharp
            var hasMainSampler = false;
            ScalarSemanticValue alphaChain;
            if (!evidence.TryGetTexture(
                    MainTextureProperty, out var assignment) ||
                !assignment.IsAssigned)
            {
                // Declared-default arm (design 2026-09-26): every attested
                // lilToon source declares _MainTex = "white" {}, and the
                // canonical digests pin that Properties block. Playback
                // binds the declared default when a material assigns no
                // texture, so the sample is exactly one at every texel and
                // coordinate-independent: the main sample collapses to its
                // constant exactly as the importer theorem does for the
                // assigned case. The digest is the enforcement: a vendor
                // change to the default breaks attestation before this arm
                // can run.
                alphaChain = ScalarSemanticValue.Constant(colorAlpha);
            }
            else
```

The mask-sample guard that replaces the unconditional construction is:

```csharp
            TextureSample maskSample = null;
            if (maskTerm.Kind == LilToonAlphaMaskTermKind.Sample)
            {
                // The mask borrows _MainTex's captured sampler facts. An
                // unassigned main has none, so a sampled mask refuses by
                // name instead of proving through borrowed facts. A later
                // design can attest the default sampler; this one refuses.
                if (!hasMainSampler)
                {
                    return RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        LilToonSemanticOutput.Alpha,
                        LilToonSemanticDiagnosticCode.UnsupportedSampling,
                        MainTextureProperty);
                }

                maskSample = new TextureSample(
                    maskTerm.Source,
                    maskTerm.Mapping,
                    assignment.Texture.Sampling);
```

Mechanical rules for the rest of the method, which moves but changes no behavior:

- Keep the mask-constant early return, the dissolve, scroll-rotate, cutoff, and tint-alpha gates, and their comments exactly where they are, before the texture arm.
- Move the transparent method's existing assigned-branch gates (`HasSampling`, `HasScaleOffset`, the exact per-component `_MainTex_ST` identity test with `UnsupportedUv` naming `MainTexStProperty`) inside the `else` block, preserving each existing comment verbatim, and set `hasMainSampler = true` after the ST test.
- Keep the `SampledAlphaIsProvenOne` collapse and the source-identity check inside the assigned branch, preserving the `T1 §9.1 clause 5` comment and the runtime-rotation-path comment verbatim. Build the main sample with the existing `UvMapping(0, assignment.Scale, assignment.Offset)` there.
- Keep the existing mask tail (`ReplacesMainAlpha`, the two `ComposeLayer` calls, the multiply, and the final `Complete`) unchanged, except that the mask sample now reads `assignment.Texture.Sampling` because the old `sharedSampling` local moved inside the assigned branch.

The transparent method's assigned-path behavior is byte-identical to before.

- [ ] **Step 4: Run the tests and watch them pass**

Run the EditMode filter from Step 2.
Expected: 3 run, 3 passed. Record the observed counts.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs
git commit -m "feat: prove an unassigned main texture as the declared white default in the transparent family"
```

---

### Task 3: Capture-refusal report detail

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs` (`TextureCaptureRefusal`, around lines 237-254)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs` (the `--- Texture capture refusals ---` block, around lines 297-333)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs`

**Interfaces:**
- Consumes: `TextureCaptureRefusal.Reason` and `TextureCaptureRefusal.HasSourceIdentity`, both existing.
- Produces: every texture-capture refusal message names the reason family and states the identity fact. The report-completeness tests keep passing unchanged.

- [ ] **Step 1: Write the failing tests**

Add to `AmuseReportsSlotTests.cs`, directly after `TextureCaptureRefusalReportsSlotPropertyAndChannel`:

```csharp
        [Test]
        public void TextureCaptureRefusal_StatesReasonAndMissingIdentity()
        {
            var refusal = new TextureCaptureRefusal(
                "_MainTex",
                false,
                default,
                TextureChannel.Alpha,
                TextureCaptureRefusalReason.UnavailableCapture);

            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.TextureCaptureRefusal(_renderer, 3, refusal));

            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("UnavailableCapture"),
                "the report must name the exact refusal reason family");
            Assert.That(message, Does.Contain("has no source identity"),
                "the report must state the identity fact");
        }

        [Test]
        public void TextureCaptureRefusal_StatesReasonAndPresentIdentity()
        {
            var refusal = new TextureCaptureRefusal(
                "_MainTex",
                true,
                default,
                TextureChannel.Alpha,
                TextureCaptureRefusalReason.NonResidentMips);

            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.TextureCaptureRefusal(_renderer, 1, refusal));

            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("NonResidentMips"),
                "the report must name the exact refusal reason family");
            Assert.That(message, Does.Contain("has a source identity"),
                "the report must state the identity fact");
        }
```

- [ ] **Step 2: Run the tests and watch them fail**

Run the EditMode filter `Alrauna.Amuse.Tests.Editor.Build.AmuseReportsSlotTests`.
Expected: 2 new tests fail (the message lacks the reason name and the identity phrase). Existing tests in the class keep passing. Record the observed counts.

- [ ] **Step 3: Implement**

In `AmuseReports.TextureCaptureRefusal`, pass two more format arguments:

```csharp
            using (ErrorReport.WithContextObject(renderer))
            {
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
                        : "has no source identity");
            }
```

In `AmuseReportStrings.cs`, update all three texture-capture refusal families. Append one sentence to each `:description` and one sentence to each `:hint`, using `{3}` and `{4}`. The sentences are identical across the three families:

Description sentence:

```
The refusal reason is {3}, and the texture {4}.
```

Hint sentence, appended after each family's existing hint text:

```
When the report says the texture has no source identity, the build saw a texture that no project asset backs. When it says the texture has a source identity, the capture route refused a real imported asset.
```

So, for example, `amuse.texture.UnavailableCapture:description` becomes:

```csharp
            ["amuse.texture.UnavailableCapture:description"] =
                "The capture of texture property {1} (channel {2}) on " +
                "material slot {0} refused, so AMUSE has no proof for " +
                "the triangles that sample it. Those triangles stay on " +
                "the original material. The refusal reason is {3}, and " +
                "the texture {4}.",
```

Apply the same appended sentences to `amuse.texture.NonResidentMips` and `amuse.texture.UnsupportedFormat` descriptions and hints. Leave every other key untouched.

- [ ] **Step 4: Run the tests and watch them pass**

Run the EditMode filter from Step 2.
Expected: all tests in the class pass, including the two report-completeness tests. Record the observed counts.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs
git commit -m "feat: name the capture reason and identity fact in texture capture refusals"
```

---

### Task 4: Full validation

**Files:**
- None. This task runs and records.

- [ ] **Step 1: Run the full product assembly**

Run the full `Alrauna.Amuse.Tests.Editor` assembly, EditMode, in the dev editor instance.
Expected: 0 failures. Record the observed pass, fail, and skip counts. A run that reports 0 tests is a failure.

- [ ] **Step 2: Run the research assembly**

Run the full `Alrauna.Amuse.Research.Tests.Editor` assembly, EditMode, in the dev editor instance.
Expected: 0 failures. Record the observed counts.

- [ ] **Step 3: Whitespace and identifier sweep**

Run `git diff --check` over the working tree. Then sweep every changed file for an at-sign joined to a hexadecimal hash, drive-letter paths, home-directory paths, four-digit ports, and every private asset name known to the session. Fix every hit, then state that the sweep ran.

- [ ] **Step 4: Census Lab characterization handoff**

This step is manual and happens after the next Lab build with the updated package. The enriched capture-refusal report on the streamed cutout texture separates the investigation's open second cause: `has no source identity` points at a build-copy clone; `has a source identity` points at the capture route or the build context. Record the outcome in a dated follow-up to `docs/superpowers/investigations/2026-09-26-maintex-refusal-investigation.md`.
