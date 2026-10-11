# NDMF Console Info Reporting for Anisotropy Refusals Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Report material slot refusals caused by texture anisotropy differences in the NDMF console with Information severity and a dedicated refusal cause.

**Architecture:** Add `AlphaSeparationSlotRefusal.AnisotropyDifferences` to the closed slot refusal enum. Expose `HasAnisotropicSampling` on `AlphaResolution` and `SlotResolutionResult`. Pass slot results into `AlphaSeparationPreparation.Prepare` to detect unproven slots under conservative proof policies and emit NDMF Information reports through `AmuseReports`.

**Tech Stack:** C# against Unity 2022.3 APIs, NDMF 1.14.4 `ErrorReport`, NUnit EditMode test framework.

**Spec:** `docs/superpowers/specs/2026-10-11-ndmf-anisotropy-refusal-reporting-design.md`

## Global Constraints

- Unity 2022.3.22f1 or newer admitted f-releases.
- NDMF `[1.14.4, 2.0.0)`.
- VRChat SDK Base and Avatars `[3.10.4, 4.0.0)`.
- Pure analysis code in `Analysis/` remains immutable, allocation-free, and host-independent.
- Fail closed on unhandled enum values and unknown inputs.
- No mock tests. Tests use public synthetic fixtures and verified seams.
- Plain English ASD-STE100: short active sentences, one idea per sentence, no semicolons, and no contractions.
- Never record absolute paths, machine names, ports, or hex hashes.

---

### Task 1: Dedicated Refusal Enum Member and Report Strings

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs:80-95`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:512-525`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs:220-240,390-415`

**Interfaces:**
- Produces: `AlphaSeparationSlotRefusal.AnisotropyDifferences`
- Produces: `amuse.slotSeparation.AnisotropyDifferences` keys in `AmuseReportStrings`

- [ ] **Step 1: Write the failing test for the new enum member strings**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs`, add a typed unit test for `AnisotropyDifferences`:

```csharp
        [Test]
        public void AnisotropyDifferencesRefusalReportsInformationEntry()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotSeparationRefusal(
                    _renderer,
                    0,
                    AlphaSeparationSlotRefusal.AnisotropyDifferences,
                    "TestRenderer",
                    _material));

            Assert.That(errors, Has.Count.EqualTo(1));
            var simple = errors[0].TheError as SimpleError;
            Assert.That(simple, Is.Not.Null);
            Assert.That(
                simple.TitleKey,
                Is.EqualTo("amuse.slotSeparation.AnisotropyDifferences"));
            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("TestRenderer"));
            Assert.That(message, Does.Contain("0"));
            Assert.That(message, Does.Contain("anisotropic sampling"));
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run test in Unity Test Runner or inspect compilation.
Expected: FAIL because `AlphaSeparationSlotRefusal.AnisotropyDifferences` does not exist yet.

- [ ] **Step 3: Add the enum member and report strings**

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs`, add `AnisotropyDifferences` after `OpaqueCoverageBelowMinimum`:

```csharp
        /// <summary>The slot was left unchanged because its texture uses
        /// anisotropic sampling under the active conservative proof policy.
        /// The proof could not bound the elongated anisotropic footprint
        /// on mixed alpha texels. Enabling desktop or mobile anisotropy
        /// acceptance allows AMUSE to prove opacity without anisotropic
        /// footprints.</summary>
        AnisotropyDifferences,
```

In `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`, add the three table entries after `OpaqueCoverageBelowMinimum:hint`:

```csharp
            ["amuse.slotSeparation.AnisotropyDifferences"] =
                "AMUSE left material slot {0} of renderer '{2}' unchanged.",
            ["amuse.slotSeparation.AnisotropyDifferences:description"] =
                "AMUSE cannot prove opacity for slot {0} of renderer '{2}'. " +
                "Its texture uses anisotropic sampling under the active conservative proof policy. " +
                "Reason: {1}. The slot keeps its original material.",
            ["amuse.slotSeparation.AnisotropyDifferences:hint"] =
                "Enable 'Accept Desktop Anisotropy Differences' (or mobile) on the AMUSE component " +
                "if you want AMUSE to prove opacity without anisotropic footprints.",
```

- [ ] **Step 4: Run tests to verify they pass**

Run `AmuseReportsSlotTests`.
Expected: `AnisotropyDifferencesRefusalReportsInformationEntry` and `EverySlotSeparationCauseHasPlainEnglishStrings` PASS.

---

### Task 2: Pure Analysis Anisotropic Sampling Queries

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:115-130`
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs:45-55`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`

**Interfaces:**
- Produces: `AlphaResolution.HasAnisotropicSampling`
- Produces: `SlotResolutionResult.HasAnisotropicSampling`

- [ ] **Step 1: Write the failing tests in AlphaSemanticsResolverTests**

In `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`, add:

```csharp
        [Test]
        public void ClassifiedResolution_ExposesAnisotropicSamplingStatus()
        {
            var chain = SingleLevelChain(8, 8, 255);
            var anisoSampling = new TextureSampling(
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp,
                TextureAnisoMode.Anisotropic);
            var nonAnisoSampling = new TextureSampling(
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp,
                TextureAnisoMode.None);

            var anisoResolution = AlphaResolution.Classified(
                chain, anisoSampling, UvMapping.Identity, 0);
            var nonAnisoResolution = AlphaResolution.Classified(
                chain, nonAnisoSampling, UvMapping.Identity, 0);
            var uniformResolution = AlphaResolution.Uniform(
                TriangleAlphaOutcome.ProvenOpaque);
            var refusedResolution = AlphaResolution.Refused(
                AlphaResolutionFailure.SemanticsUnknown);

            Assert.That(anisoResolution.HasAnisotropicSampling, Is.True);
            Assert.That(nonAnisoResolution.HasAnisotropicSampling, Is.False);
            Assert.That(uniformResolution.HasAnisotropicSampling, Is.False);
            Assert.That(refusedResolution.HasAnisotropicSampling, Is.False);
        }

        [Test]
        public void CompositeResolutions_PropagateAnisotropicSampling()
        {
            var chain = SingleLevelChain(8, 8, 255);
            var anisoSampling = new TextureSampling(
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp,
                TextureAnisoMode.Anisotropic);
            var nonAnisoSampling = new TextureSampling(
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp,
                TextureAnisoMode.None);

            var aniso = AlphaResolution.Classified(
                chain, anisoSampling, UvMapping.Identity, 0);
            var nonAniso = AlphaResolution.Classified(
                chain, nonAnisoSampling, UvMapping.Identity, 0);

            var product = AlphaResolution.Product(aniso, nonAniso);
            var disjunction = AlphaResolution.Or(aniso, nonAniso);
            var pureNonAnisoProduct = AlphaResolution.Product(nonAniso, nonAniso);

            Assert.That(product.HasAnisotropicSampling, Is.True);
            Assert.That(disjunction.HasAnisotropicSampling, Is.True);
            Assert.That(pureNonAnisoProduct.HasAnisotropicSampling, Is.False);
        }
```

- [ ] **Step 2: Run test to verify it fails**

Expected: FAIL because `HasAnisotropicSampling` does not exist on `AlphaResolution`.

- [ ] **Step 3: Implement HasAnisotropicSampling on AlphaResolution and SlotResolutionResult**

In `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs` in `AlphaResolution`:

```csharp
        internal bool HasAnisotropicSampling
        {
            get
            {
                if (!IsResolved || _isUniform)
                {
                    return false;
                }

                if (_isProduct || _isDisjunction)
                {
                    return (_firstFactor != null && _firstFactor.HasAnisotropicSampling) ||
                           (_secondFactor != null && _secondFactor.HasAnisotropicSampling);
                }

                return _sampling.Aniso == TextureAnisoMode.Anisotropic;
            }
        }
```

In `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs` in `SlotResolutionResult`:

```csharp
        internal bool HasAnisotropicSampling
        {
            get
            {
                if (Resolutions == null || Resolutions.Count == 0)
                {
                    return false;
                }

                for (var i = 0; i < Resolutions.Count; i++)
                {
                    if (Resolutions[i].HasAnisotropicSampling)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run `AlphaSemanticsResolverTests` and `AdmittedMaterialStatesTests`.
Expected: PASS.

---

### Task 3: Preparation Pass Anisotropy Refusal Detection and Emission

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:95-125,250-280`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:858-872,1540-1568`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

**Interfaces:**
- Consumes: `SlotResolutionResult.HasAnisotropicSampling`
- Produces: `state.RecordSlotRefusal(AlphaSeparationSlotRefusal.AnisotropyDifferences)`
- Produces: `AmuseReports.SlotSeparationRefusal(..., AlphaSeparationSlotRefusal.AnisotropyDifferences)`

- [ ] **Step 1: Write the failing test in AlphaSeparationPreparationTests**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`, add a test verifying that an unproven anisotropic slot records `AnisotropyDifferences`:

```csharp
        [Test]
        public void UnchangedSlotWithAnisotropicSampling_RecordsAnisotropyDifferencesRefusal()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE anisotropy refusal candidate");
            Mesh mesh = null;
            Material material = null;
            try
            {
                FixtureAvatarIdentity.RequireVrcDescriptorSupport();
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                FixtureProofScope.PinAllSizes(root);

                // Create a mixed half texture with authored level one (anisotropic under forced quality)
                AlphaSeparationSplitTests.EnsureSplitFolder();
                var mixed = ImportMixedHalfTexture(
                    "aniso_refusal_tex", MixedHalfAlphaPixels());
                mesh = MixedHalfMesh();
                material = SampledAlphaMaterial(mixed);

                var renderer = root.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.sharedMaterials = new[] { material };

                // Force anisotropic filtering so ConservativeDesktop projects anisotropic sampling
                UnityEngine.QualitySettings.anisotropicFiltering =
                    UnityEngine.AnisotropicFiltering.ForceEnable;

                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);
                SeedRetainedHostBindings(context);
                AmusePlatformFinishPass.Execute(
                    context,
                    SupportedFacts(),
                    SelectVerifiedFixtureRequest,
                    CaptureVerifiedFixtureMaterials,
                    VerifiedAlphaOnly);

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(
                    amuse.SlotRefusalCount(
                        AlphaSeparationSlotRefusal.AnisotropyDifferences),
                    Is.EqualTo(1),
                    "an unproven slot under conservative anisotropic sampling " +
                    "must record the AnisotropyDifferences refusal");
            }
            finally
            {
                DestroyCommittedClone(root, null);
                Object.DestroyImmediate(root);
                if (mesh != null) Object.DestroyImmediate(mesh);
                if (material != null) Object.DestroyImmediate(material);
            }
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run the new test.
Expected: FAIL because refusal count for `AnisotropyDifferences` is currently zero.

- [ ] **Step 3: Update preparation pass to evaluate anisotropy refusals**

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`:

1. Update `Prepare` method signature to accept optional `IReadOnlyList<SlotResolutionResult> slotResults = null`.
2. Add helper `HasAnyUnknownOutcome(MeshSeparationPlan plan, int submeshIndex)`:

```csharp
        private static bool HasAnyUnknownOutcome(
            MeshSeparationPlan plan,
            int submeshIndex)
        {
            if (plan.Source?.Submeshes == null ||
                submeshIndex < 0 ||
                submeshIndex >= plan.Source.Submeshes.Count)
            {
                return false;
            }

            var outcomes = plan.Source.Submeshes[submeshIndex].Outcomes;
            if (outcomes == null)
            {
                return false;
            }

            for (var i = 0; i < outcomes.Count; i++)
            {
                if (outcomes[i] == TriangleAlphaOutcome.Unknown)
                {
                    return true;
                }
            }

            return false;
        }
```

3. Before returning early on `!plan.HasAnyOpaqueCandidates`, evaluate unproven slots:

```csharp
            var reportedAnisotropySlots = new HashSet<int>();
            if (slotResults != null)
            {
                foreach (var submesh in plan.Submeshes)
                {
                    if (submesh.Disposition == SubmeshSeparationDisposition.Unchanged)
                    {
                        var slotIndex = submesh.SourceMaterialBindingIndex;
                        if (slotIndex >= 0 && slotIndex < slotResults.Count &&
                            reportedAnisotropySlots.Add(slotIndex))
                        {
                            var slotResult = slotResults[slotIndex];
                            if (slotResult.IsResolved &&
                                slotResult.HasAnisotropicSampling &&
                                HasAnyUnknownOutcome(plan, submesh.SourceSubmeshIndex))
                            {
                                state.RecordSlotRefusal(
                                    AlphaSeparationSlotRefusal.AnisotropyDifferences);
                                AmuseReports.SlotSeparationRefusal(
                                    target.Renderer,
                                    slotIndex,
                                    AlphaSeparationSlotRefusal.AnisotropyDifferences);
                            }
                        }
                    }
                }
            }

            if (!plan.HasAnyOpaqueCandidates)
            {
                return null;
            }
```

4. In the candidate loop over `plan.Submeshes`:
   - When `submesh.Disposition == SubmeshSeparationDisposition.Unchanged`, check `reportedAnisotropySlots` and report if not already reported.
   - When `submesh.Disposition == SubmeshSeparationDisposition.Split` and `OpaqueCoverageBelow(...)`:
     If `slotResults != null` and slot has `HasAnisotropicSampling` and `HasAnyUnknownOutcome`, record and report `AnisotropyDifferences`. Otherwise record and report `OpaqueCoverageBelowMinimum`.

5. In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`:
   Update `RetainPreparedSeparation` call on line 858 to pass `resolved.SlotResults`.
   Update `RetainPreparedSeparation` definition on line 1540 to accept and pass `resolved.SlotResults`.

- [ ] **Step 4: Run tests to verify they pass**

Run `AlphaSeparationPreparationTests`.
Expected: PASS.

---

### Task 4: End-to-End Integration Test and Final Sweep

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Verifies: Full build lifecycle with NDMF Console capturing `amuse.slotSeparation.AnisotropyDifferences` report.

- [ ] **Step 1: Write the end-to-end integration test in AmusePlatformFinishPluginTests**

Add `AnisotropyDifferencesRefusalReportsToNdmfConsole` to `AmusePlatformFinishPluginTests.cs`:

```csharp
        [Test]
        public void AnisotropyDifferencesRefusalReportsToNdmfConsole()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var savedFiltering = UnityEngine.QualitySettings.anisotropicFiltering;
            var root = new GameObject("AMUSE anisotropy reporting test");
            Mesh mesh = null;
            Material material = null;

            try
            {
                Assert.That(
                    EditorUserBuildSettings.activeBuildTarget,
                    Is.EqualTo(BuildTarget.StandaloneWindows64));

                var optimizer = root.AddComponent<
                    Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                FixtureAvatarIdentity.RequireVrcDescriptorSupport();
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                FixtureProofScope.PinAllSizes(root);

                // Acceptance stays false (the default):
                var serialized = new SerializedObject(optimizer);
                serialized.FindProperty("_acceptDesktopAnisotropyDifferences")
                    .boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                EnsureSamplingPolicyFolder();
                var mixed = ImportMixedHalfTexture(
                    "aniso_console_report", MixedHalfAlphaPixels());
                mesh = MixedHalfMesh();
                material = SampledAlphaMaterial(mixed);

                var renderer = root.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.sharedMaterials = new[] { material };

                UnityEngine.QualitySettings.anisotropicFiltering =
                    UnityEngine.AnisotropicFiltering.ForceEnable;

                var errors = ErrorReport.CaptureErrors(() =>
                {
                    var context = AvatarProcessor.ProcessAvatar(
                        root, TestGenericPlatform.Instance);
                    SeedRetainedHostBindings(context);
                    AmusePlatformFinishPass.Execute(
                        context,
                        SupportedFacts(),
                        SelectVerifiedFixtureRequest,
                        CaptureVerifiedFixtureMaterials,
                        VerifiedAlphaOnly);
                });

                var anisoReports = new List<ErrorReport.ReportedError>();
                foreach (var entry in errors)
                {
                    if (entry.TheError is SimpleError simple &&
                        simple.TitleKey == "amuse.slotSeparation.AnisotropyDifferences")
                    {
                        anisoReports.Add(entry);
                    }
                }

                Assert.That(
                    anisoReports, Has.Count.EqualTo(1),
                    "NDMF console must receive exactly one AnisotropyDifferences report");
                var message = anisoReports[0].TheError.ToMessage();
                Assert.That(message, Does.Contain(renderer.gameObject.name));
                Assert.That(message, Does.Contain("slot 0"));
                Assert.That(message, Does.Contain("Accept Desktop Anisotropy Differences"));
            }
            finally
            {
                UnityEngine.QualitySettings.anisotropicFiltering = savedFiltering;
                DestroyCommittedClone(root, null);
                Object.DestroyImmediate(root);
                if (mesh != null) Object.DestroyImmediate(mesh);
                if (material != null) Object.DestroyImmediate(material);
                if (AssetDatabase.IsValidFolder(SamplingPolicyTempFolder))
                {
                    AssetDatabase.DeleteAsset(SamplingPolicyTempFolder);
                }
            }
        }
```

- [ ] **Step 2: Run the end-to-end integration test**

Run test in Unity Test Runner.
Expected: PASS.

- [ ] **Step 3: Run the full test suite**

Run `Alrauna.Amuse.Tests.Editor`.
Expected: All tests pass with zero regressions.

- [ ] **Step 4: Perform privacy and compliance sweep**

Verify that changed files contain:
- Zero absolute or user paths
- Zero hex hashes or four-digit ports
- Zero private asset identifiers
- No semicolons outside code blocks
- No contractions in human-facing text
