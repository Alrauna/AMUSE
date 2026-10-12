# Production Test Coverage Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the named production coverage gaps from the 2026-10-11 coverage investigation, highest risk first, with the suite green throughout.

**Architecture:** Coverage additions organized in four phases: build-path risk, reader and grammar logic, guard batteries, semantics and research depth. Two named production seams (finish-plugin target probe, version-reader match extraction) are the only production edits besides the gated contract decision in Task 14.

**Tech Stack:** C# against Unity 2022.3 APIs, NUnit EditMode, NDMF `AvatarProcessor` test plugins, verified vendor-attestation seams.

**Spec:** `docs/superpowers/specs/2026-10-11-test-coverage-gaps-design.md`

## Global Constraints

- Base branch `fix/unit-test-soundness`. Run after the soundness fixes plan. No commits without explicit authorization.
- The suite stays green. Every task records observed counts.
- New tests are green on HEAD. Mutant verification happens with a temporary production edit, restored before moving on, with the observed failure recorded.
- No mocks. Public synthetic fixtures and verified seams only.
- Never record absolute paths, machine names, ports, or hex hashes.

---

### Task 1: Version reader match logic

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/PackageVersionReader.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/PackageVersionReaderTests.cs`

**Interfaces:**
- Consumes: `PackageVersionReader.ListPackages` seam (exists).
- Produces: `PackageVersionReader.VersionFor(System.Collections.Generic.IEnumerable<UnityEditor.PackageManager.PackageInfo> packages, string packageName)`.

- [ ] **Step 1: Write the failing tests**

```csharp
        [Test]
        public void NullRequestReadsAsFailedListing()
        {
            var original = PackageVersionReader.ListPackages;
            PackageVersionReader.ListPackages = (_, _) => null;
            try
            {
                Assert.That(
                    PackageVersionReader.ReadInstalledOrNull("any.package"),
                    Is.Null);
            }
            finally
            {
                PackageVersionReader.ListPackages = original;
            }
        }

        [Test]
        public void VersionFor_MatchesByNameAndReturnsItsVersion()
        {
            var packages = new[]
            {
                MakePackage("other.package", "1.0.0"),
                MakePackage("jp.lilxyzw.liltoon", "0.9.0"),
            };

            Assert.That(
                PackageVersionReader.VersionFor(packages, "jp.lilxyzw.liltoon"),
                Is.EqualTo("0.9.0"));
            Assert.That(
                PackageVersionReader.VersionFor(packages, "absent.package"),
                Is.Null);
        }

        private static UnityEditor.PackageManager.PackageInfo MakePackage(
            string name, string version)
        {
            var info = new UnityEditor.PackageManager.PackageInfo();
            typeof(UnityEditor.PackageManager.PackageInfo)
                .GetProperty(nameof(UnityEditor.PackageManager.PackageInfo.name))
                ?.SetValue(info, name);
            typeof(UnityEditor.PackageManager.PackageInfo)
                .GetProperty(nameof(UnityEditor.PackageManager.PackageInfo.version))
                ?.SetValue(info, version);
            return info;
        }
```

If `PackageInfo` properties reject reflection writes, use the serialization-free alternative: serialize two anonymous records through `JsonUtility` into real `PackageInfo` instances, or match on whatever settable surface exists. State which route was taken in the execution report.

- [ ] **Step 2: Run to verify the match test fails**

Expected: FAIL because `VersionFor` does not exist. The null-request test passes already; it promotes the existing implicit path to an assertion.

- [ ] **Step 3: Extract the match logic**

In `PackageVersionReader`, extract the loop into:

```csharp
        internal static string VersionFor(
            System.Collections.Generic.IEnumerable<
                UnityEditor.PackageManager.PackageInfo> packages,
            string packageName)
        {
            foreach (var package in packages)
            {
                if (package.name == packageName)
                {
                    return package.version;
                }
            }
            return null;
        }
```

`ReadInstalledOrNull` keeps the request handling and ends with `return VersionFor(request.Result, packageName);`.

- [ ] **Step 4: Run the fixture**

Run `PackageVersionReaderTests`. Expected: all green.

---

### Task 2: Finish-plugin build-target seam and unsupported-target disclosure

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (the plugin's build-target read site, around lines 613-627)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Consumes: the existing barrier fixture helpers in `AmusePlatformFinishPluginTests`.
- Produces: `AmusePlatformFinishPlugin.ActiveBuildTargetForTests` (`Func<BuildTarget>`, null default).

Note: `PlatformAlphaSamplingPolicy.TrySelect` takes the target as a parameter and never reads the setting, so the policy already has full truth-table coverage for unsupported targets. Do not touch it.

- [ ] **Step 1: Add the seam at the plugin's read site**

In `AmusePlatformFinishPlugin`, where the pass reads `EditorUserBuildSettings.activeBuildTarget` to feed the sampling policy, resolve through the seam:

```csharp
        // Test-only override. Null in production. The disclosure path is
        // the only consumer; the apply and editor read sites keep the
        // real setting.
        internal static Func<BuildTarget> ActiveBuildTargetForTests = null;

        private static BuildTarget ActiveBuildTarget()
        {
            return ActiveBuildTargetForTests != null
                ? ActiveBuildTargetForTests()
                : EditorUserBuildSettings.activeBuildTarget;
        }
```

- [ ] **Step 2: Write the disclosure test**

In `AmusePlatformFinishPluginTests`, add a barrier test that arms the seam to a non-admitted target, runs the existing barrier fixture, and asserts the disclosure:

```csharp
        [Test]
        public void UnsupportedBuildTarget_ReportsTheUnsupportedSamplingGate()
        {
            var original = AmusePlatformFinishPlugin.ActiveBuildTargetForTests;
            AmusePlatformFinishPlugin.ActiveBuildTargetForTests =
                () => BuildTarget.StandaloneLinux64;
            try
            {
                var errors = ErrorReport.CaptureErrors(() =>
                    RunBarrier(<existing fixture arguments>));

                Assert.That(errors, Has.Some.Matches<...>(
                    an entry whose title key is the
                    amuse.samplingPolicy unsupported-target key));
                // and: the run records no positive-mutation outcomes.
            }
            finally
            {
                AmusePlatformFinishPlugin.ActiveBuildTargetForTests = original;
            }
        }
```

Fill the fixture arguments and the report-key assertion from the neighboring report-capture tests, which already assert exact `amuse.*` keys against captured `SimpleError` entries.

- [ ] **Step 3: Verify the disclosure test bites**

Temporarily remove the plugin's unsupported-target gate (the `!HasSamplingPolicy` branch). Run the disclosure test.
Expected: FAIL, no report entry. Restore. Run again.
Expected: PASS.

- [ ] **Step 4: Run the fixture and restore**

Expected: green with the seam restored to null in a finally block.

---

### Task 3: Renderer-scoped conversion invalidation

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

**Interfaces:**
- Consumes: the existing `RunBarrier` helper and clip-building fixtures in the file.
- Produces: three tests asserting `RefuseEveryCandidateSlot` fires through its named arms.

- [ ] **Step 1: Write the unrecognized-binding arm test**

Build the smallest avatar whose clip animates a conversion-relevant property the union request cannot recognize, so `ConversionBindingUnrecognized` fires and every candidate slot is refused once:

```csharp
        [Test]
        public void UnrecognizedConversionBinding_RefusesEveryCandidateSlot()
        {
            // Fixture: one renderer, one conversion-capable material via the
            // verified lilToon cutout seam, one clip whose sole curve targets
            // a conversion-relevant property outside the union request.
            // Reuse the file's barrier helper; assert before the claim:
            Assert.That(candidateSlots, Is.Not.Empty, "precondition");

            var state = RunBarrier(root, clipWithUnknownRelevantBinding);

            Assert.That(
                state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal.ConversionBindingUnrecognized),
                Is.EqualTo(candidateSlots.Count),
                "a renderer-wide unrecognized conversion binding must " +
                "refuse every candidate slot exactly once");
        }
```

Fill the fixture expressions from the neighboring renderer-wide analysis refusal test, which already builds the additive-layer and blend-tree shapes the arms mirror. Add the sibling arms `ConversionStateUnderAdditiveLayer` and `ConversionStateUnderUnnormalizedDirectBlendTree` as two more tests with the same assertion shape, using that neighbor's layer and blend-tree fixtures.

- [ ] **Step 2: Run to verify they fail**

Expected: the new tests FAIL. The production arms never fired before; if they fire correctly the tests pass and the investigation's reachability claim was wrong. Record either way.

Note: this task may reveal the arms are dead because production never routes those states into the conversion scope. That is a stop-condition finding, not a failure.

---

### Task 4: Non-skinned mesh mutation

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`

- [ ] **Step 1: Confirm the analysis admits MeshRenderer**

Read the renderer-admission matrix in `UnityRendererAlphaAnalysisTests` and confirm `MeshRenderer` plus `MeshFilter` is an admitted renderer kind. If it is refused, stop per the spec.

- [ ] **Step 2: Write the apply test**

Clone the file's existing proven-opaque apply fixture, swapping the `SkinnedMeshRenderer` for a `MeshRenderer` plus `MeshFilter`:

```csharp
        [Test]
        public void MeshRendererCandidate_AppendsSubmeshAndSwapsFilterMesh()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            // Build the same single proven-opaque submesh fixture the
            // neighboring apply test uses, on a MeshRenderer + MeshFilter.
            // Run the real ApplyFinalization through the file's test plugin.

            Assert.That(
                filter.sharedMesh, Is.Not.SameAs(originalMesh),
                "the filter's mesh must be the split clone");
            Assert.That(
                filter.sharedMesh.subMeshCount,
                Is.EqualTo(originalMesh.subMeshCount + 1));
            Assert.That(
                filter.sharedMaterials[^1],
                Is.EqualTo(expectedCanonicalOpaqueMaterial));
            // A MeshRenderer carries no sharedMesh; the write target is
            // the MeshFilter only, which the first assertion proves.
        }
```

Fill the fixture expressions from the neighboring skinned apply test; only the renderer component and its assertions differ.

- [ ] **Step 3: Verify the test bites**

Temporarily make the apply's mesh write use `GetComponent<SkinnedMeshRenderer>()` only. Run the test.
Expected: FAIL, the filter mesh is never swapped. Restore. Run again.
Expected: PASS.

---

### Task 5: Malformed mesh data refusal arms

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`

- [ ] **Step 1: Write the five table rows**

Add a helper that builds a mesh with one broken property, then one `[TestCase]` per arm:

```csharp
        private static Mesh MeshWithBrokenData(int kind)
        {
            var mesh = new Mesh { vertices = Generate.ValidPositions(3) };
            switch (kind)
            {
                case 0: mesh.vertices = new Vector3[2]; break;          // short positions
                case 1: mesh.SetUVs(0, new List<Vector2> { new(0, 0) }); break; // partial uv0
                case 2: mesh.SetUVs(1, new System.Collections.Generic.List<Vector2>
                        { new Vector2(0f, 0f), new Vector2(1f, 1f) });
                    break;                                              // uv1 count != vertex count
                case 3: mesh.triangles = new[] { 0, 1 }; break;         // not a multiple of 3
                case 4: mesh.triangles = new[] { 0, 1, 99 }; break;     // out-of-range index
            }
            return mesh;
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void BrokenMeshData_RefusesAsMalformedMeshData(int kind) { ... }
```

Write each switch arm fully against the real `Mesh` API, mirroring the neighboring refusal tests' capture call and the exact `RendererAnalysisRefusal.MalformedMeshData` assertion.

- [ ] **Step 2: Add the non-finite-position classification row**

One more test with a `float.NaN` vertex position through `Classify`, asserting the non-finite-position refusal, next to the existing non-finite-UV row.

- [ ] **Step 3: Run the fixture**

Expected: green. Then temporarily invert one arm's condition to confirm the matching row fails. Restore.

---

### Task 6: Version grammar edges

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/HostLifecycleCapabilityTests.cs`

- [ ] **Step 1: Add the table rows**

Add `TestCase` rows beside the existing version table, constructing `HostLifecycleFacts` exactly as the neighboring rows do and varying only the version strings:

```csharp
        [TestCase("2022.3.22f1")]                       // no release-type suffix variant handled upstream
        [TestCase("2022.3.22f1 junk")]                  // trailing junk
        [TestCase("1.14.4-pre.2+build.meta")]           // build metadata
        [TestCase("2022.3")]                            // missing components
        [TestCase("1.14.4.1")]                          // fourth component
        [TestCase("1..4")]                              // empty component
        public void VersionGrammarEdge_ResolvesToANamedRefusal(string version) { ... }
```

For each row, first record the current production answer, then assert it as the expected refusal or acceptance with its exact `HostLifecycleRefusal` member. The point is pinning current grammar behavior; any answer change must fail loudly.

- [ ] **Step 2: Run the fixture**

Expected: green with the recorded answers asserted.

---

### Task 7: Behaviour allowlist completion

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/BehaviourIdentityTests.cs`

- [ ] **Step 1: Add the missing rows**

Copy the exact allowlist entry strings from the production table in `Editor/Host/BehaviourIdentity.cs` for `LocomotionControl`, `TemporaryPoseSpace`, `JumpAndGravity`, and `PlayAudio`. Add one `IsAllowed` assertion per entry following the existing allowlist test shape, plus one `com.vrchat.base@`-prefixed mimic case asserting the prefix arm admits a base-package behaviour.

- [ ] **Step 2: Verify one row bites**

Temporarily delete one of the four entries from the production table. Run the new rows.
Expected: the matching row FAILs. Restore.

---

### Task 8: Field-set separation and closure plumbing

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/AlphaFieldSetTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`

- [ ] **Step 1: Write the TryGetFor branch tests**

Three tests with hand-built captured evidence following the file's existing bag pattern:

```csharp
        [Test]
        public void PredicateDisagreement_FailsClosed()
        {
            // One source, two deciding assignments: one cutoff predicate,
            // one exact-one predicate. Lookup without a predicate request.
            // The real signature is TryGetFor(CapturedMaterialEvidence,
            // MaterialEvidenceRequest, TextureSourceId, out CapturedAlphaField).
            Assert.That(set.TryGetFor(evidence, request, sourceId, out _),
                Is.False);
        }

        [Test]
        public void ChannelNotAskedByTheProperty_SkipsTheEntry() { ... }

        [Test]
        public void MalformedLookups_Throw() { ... }
```

Write each body against the real `AlphaFieldSet.TryGetFor` signature and the file's existing evidence builders; the disagreement case needs one source captured under two predicates, which the capture side already produces in its own tests.

- [ ] **Step 2: Write the slot-propagation and closure rows**

In `AdmittedMaterialStatesTests`, assert `SlotResolutionResult.HasAnisotropicSampling` propagates from a containing resolution. `ClosureWay` is a property of type `MaterialDependencyClosureFailure` on the resolution: assert it records the captured way for every failure way, and that the closure-sentence report method fails closed (throws) for `None`, matching the neighboring sentence tests.

- [ ] **Step 3: Run both fixtures**

Expected: green; verify the disagreement test bites by short-circuiting the branch temporarily.

---

### Task 9: Alpha mask scalar gates

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAlphaMaskSemanticsTests.cs`

- [ ] **Step 1: Write the scalar rows**

Use the file's `CreateMaskEvidenceWithoutSourceIdentity(float mode, float maskScale, float maskValue)` helper:

```csharp
        [Test]
        public void AlphaMask_WithNonFiniteScalarScale_RefusesNamingTheScalar()
        {
            var diagnostics = new List<LilToonSemanticDiagnostic>();
            var term = LilToonAlphaMaskTerm.Interpret(
                CreateMaskEvidenceWithoutSourceIdentity(1f, float.NaN, 0f),
                diagnostics);

            Assert.That(term.Kind, Is.EqualTo(LilToonAlphaMaskTermKind.Refused));
            Assert.That(term.RefusalDetail, Is.EqualTo("_AlphaMaskScale"));
            Assert.That(diagnostics[0].Detail, Is.EqualTo("_AlphaMaskScale"));
        }

        [Test]
        public void AlphaMask_WithNonFiniteScalarValue_RefusesNamingTheValue()
        {
            // Same shape with maskValue: float.PositiveInfinity and detail
            // "_AlphaMaskValue".
        }
```

- [ ] **Step 2: Run the fixture**

Expected: green. This closes the zero-coverage `_AlphaMaskValue` gate and gives `_AlphaMaskScale` its second coverage point.

---

### Task 10: Poiyomi premultiply and gate arms

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`

- [ ] **Step 1: Write the premultiply rows**

One premultiply-on convertible row asserting `eligibility.PremultiplyNormalization` true plus `DepthTestDivergence` false; one refused row and one already-opaque row asserting the flag false.

- [ ] **Step 2: Write the gate arms**

One row with `_Cutoff` above 1 on the plain path refusing naming `_Cutoff`; the same on the forced-opaque path. For the non-binary first-family `_AlphaForceOpaque` value, no refusal member names it: the value only fails the already-opaque fast path in `IsFunctionallyOpaque` and the material falls through to the ordinary gates. Add a row asserting exactly that fall-through: a material whose only anomaly is a non-binary first-family force-opaque flag is not reported `AlreadyOpaque` and lands on the ordinary refusal the next gate produces. Name the observed refusal in the assertion.

- [ ] **Step 3: Write the outline hole-field row**

Extend `OutlinesEnabled_UnprovenTexture_ChainAdmitted_AlphaIsTheMappedTerm` in `PoiyomiOutlineAlphaSemanticsTests` (or add a sibling): give the unproven outline texture an asymmetric alpha field through the resolver seam so one texel is non-opaque, and assert the resulting alpha keeps a mapped outline contribution instead of collapsing to a plain constant-one factor. This closes the under-assertion the investigation recorded.

- [ ] **Step 4: Run the fixture**

Expected: green.

---

### Task 11: Preset store and parser edges

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetFileStoreTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetParserTests.cs`

- [ ] **Step 1: Store failure rows**

```csharp
        [Test]
        public void MissingShippedFile_RefusesWithFileMissing()
        {
            var result = PresetFileStore.TryLoadAllFor(
                <temp folder holding only safe.json>,
                out var presets, out var failedFile, out var refusal);

            Assert.That(result, Is.False);
            Assert.That(presets, Is.Empty);
            Assert.That(failedFile, Is.EqualTo("normal.json"));
            Assert.That(refusal, Is.EqualTo(PresetLoadRefusal.FileMissing));
        }

        [Test]
        public void NullFolder_RefusesWithFileMissing() { /* folder null, failedFile "Presets" */ }

        [Test]
        public void RootedFolderOutsideAssets_FallsBackToDirectDiskRead()
        {
            // Write the three preset files by hand into a temp folder
            // outside Assets, so no imported text asset can exist and the
            // store takes its direct-disk fallback branch.
            var result = PresetFileStore.TryLoadAllFor(
                <temp folder with safe/normal/aggressive json>,
                out var presets, out var failedFile, out var refusal);

            Assert.That(result, Is.True);
            Assert.That(presets, Has.Count.EqualTo(3));
            Assert.That(refusal, Is.EqualTo(PresetLoadRefusal.None));
        }
```

- [ ] **Step 2: Parser rows**

Add: `TryParse(null, …)` refuses `MalformedJson`; `minimumOpaqueCoveragePercent: -1` refuses the out-of-range arm; a `features` object missing and one wrongly typed refuse with `MissingField` and `WrongValueType`; the inert-clamp parse (`minimumOpaqueAlphaPercent 0`, `polygonAlphaUpperClampPercent 59`) succeeds.

- [ ] **Step 3: Run both fixtures**

Expected: green.

---

### Task 12: Research depth

**Files:**
- Test: `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CollectorSeamCountingTests.cs`
- Test: `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/AvatarCensusCollectorTests.cs`

- [ ] **Step 1: Transparent counting row**

Add a seam counting test whose substituted semantics answer constant sub-unit alpha, asserting `MustRemainTransparentTriangleCount` equals the triangle count and the disposition stays `Unchanged`.

- [ ] **Step 2: Asset identity row**

Add a collector test over a renderer whose material is an imported project asset; assert `MaterialAssetPath` and `MaterialAssetGuid` propagate non-null.

- [ ] **Step 3: Guards**

Add the `CensusCopy.CheckedCopy` null-element refusal row and one tier-2 numeric guard row per arm class (sum mismatch, negative count, undefined enum) in `AnonymizedRecordTests`.

- [ ] **Step 4: Run the research assembly**

Expected: green.

---

### Task 13: Full-suite checkpoint after phase 3

- [ ] **Step 1: Run the full EditMode suite**

Expected: green with every new test included. Record observed counts before the gated and depth tasks.

---

### Task 14: Poiyomi `UnattestedMaterial` decision (GATED)

**Gate:** do not start until the owner picks option A (delete the arm) or option B (add the gate) from design section 2.1.

**Files (option A):**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs` (enum member at line 37)
- Test: the Poiyomi conversion and reports fixtures.

**Files (option B):**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs` (`EvaluateVerifiedEligibility`)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`

- [ ] **Step 1 (option A): delete the arm and its keys**

Remove `UnattestedMaterial` from `PoiyomiOpaqueConversionRefusal`. Grep `AmuseReportStrings` for any key naming the member and remove those entries. Run the conversion, reports, and adversarial fixtures.
Expected: green. The closed-enum completeness loops adapt automatically.

- [ ] **Step 2 (option B): add the gate and one refusal row**

Add the attestation precondition to `EvaluateVerifiedEligibility` ahead of the schema gates, returning `Refused(UnattestedMaterial)` for an unverified source. Add one eligibility row with an unattested source asserting the named refusal.

- [ ] **Step 3: record the decision**

Record the chosen option and the observed counts in the execution report.

---

### Task 15: Locked-material attested path and reconstruction sub-gaps

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/LockedMaterialIdentityTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/LockedMaterialReconstructionTests.cs`

- [ ] **Step 1: Attempt the attested-true fixture**

The true path of `ResolvesToAttestedPoiyomi` needs an original material that passes vendor attestation. Attempt it through the verified seam: feed `LockedMaterialIdentity` an original whose recorded shader facts match the seam's verified source. If the seam cannot produce an attested original, record the gap as seam-limited in the execution report and continue with step 2.

- [ ] **Step 2: Reconstruction rows**

Add rows for the renamed color and vector property moves and the `_ST` scale/offset restore through `TryTextureBaseName`, following the file's existing float and texture move rows.

---

### Task 16: Streaming evidence reachable arms

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityStreamingTextureEvidenceTests.cs`

- [ ] **Step 1: Write the reachable-arm rows**

Add direct rows for the arms a fixture can reach: a texture with no asset path, and a texture whose path exists but is not a main asset. Follow the file's two existing happy-path tests for the capture call shape and assert the named fail-closed refusal each arm returns.

- [ ] **Step 2: Record the seam-limited arms**

The copy, importer, clone, dimension, and pixel failure arms need importer-state control this plan does not build. Record them as seam-limited in the execution report.

---

### Task 17: Include-tree identity resolution

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/` (new fixture file following the LilToon test naming)

- [ ] **Step 1: Write the temp-tree fixture tests**

Write two include files to a temp folder and drive `LilToonIncludeTree.Enumerate` through its injectable read delegates. Assert the digest equals the `ForTests`-built tree for the same content and that `Files` lists both. Then assert `TryGetRelativePath` resolves an inside path to its relative form and refuses an outside or redirected path. Add a one-line `Empty()` test: zero files, every `TryGetRelativePath` refused.

---

### Task 18: LilToon layer and fold depth

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/` existing fixtures

- [ ] **Step 1: Third-layer composition rows**

Compose `LilToonLayerAlphaTerm.Interpret` with `third: true` over the existing synthetic evidence builders. Add one row per refusal arm class the investigation named: decal flags, MSDF, audio link, cull, per-layer dissolve and distance fade, out-of-range alpha mode, non-finite layer alpha.

- [ ] **Step 2: Unstable-identity and fold rows**

Add alpha-path `UnstableTextureIdentity` rows for the main chain and the mask. Add the fold-interaction rows: `ScalarProductFold` refusal over a saturating chain, sampled mask with an unassigned main, non-finite `_Color` alpha, and a requested-but-absent mask entry.

- [ ] **Step 3: Run the LilToon fixtures**

Expected: green. Record observed counts.

---

### Task 19: Full-suite validation

- [ ] **Step 1: Run the full EditMode suite**

Expected: green with every new test included. Record observed counts.

- [ ] **Step 2: Execution report**

Record per task: tests added, mutant verifications with observed failures, seam-limited gaps recorded, the seam decisions taken where a signature drifted from this plan, the `UnattestedMaterial` decision outcome, and any stop-condition findings.

## Review record

Three adversarial reviewers audited this plan and its design against the investigation, the coverage-scout findings, and the repository on 2026-10-11. Corrections applied in this revision:

- The build-target seam moved from `PlatformAlphaSamplingPolicy` (which takes the target as a parameter and never reads the setting) to the finish plugin's real read site. The duplicate policy truth-table row was dropped.
- The gated `UnattestedMaterial` task, the locked-material path, the streaming-ladder arms, the include-tree fixture, the LilToon layer depth, and the Poiyomi outline row are now plan tasks instead of silent drops.
- `TryGetFor`, `ClosureWay`, the malformed-mesh switch arms, and the MeshRenderer assertion now match the real signatures and API surface.
- The version grammar table carries all six edges; the preset store carries the direct-disk fallback row; the reader task states its seam-limited arms instead of promising unreachable tests.
