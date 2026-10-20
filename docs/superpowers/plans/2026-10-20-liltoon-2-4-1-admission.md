# lilToon 2.4.1 admission implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admit lilToon 2.4.1 as the newest measured row and give the EditMode preview a named cause note for approved-but-unverified sources.

**Architecture:** One attestation flip (two constants, one new row, comments) plus one preview feature (an approval flag computed once in the shared capture middle, mirrored through the capture outcome onto the sentinel record, routed to one new `NoteKind`). No consent, D3, or Multi row surface changes.

**Tech Stack:** C# / Unity 2022.3, NUnit via Unity Test Runner (EditMode), no new dependencies.

**Spec:** `docs/superpowers/specs/2026-10-20-liltoon-2-4-1-admission-design.md` — decisions B1-B6. The spec is the binding authority. The investigation `docs/superpowers/investigations/2026-10-20-liltoon-2-4-1-admission-break-investigation.md` is its fact source.

## Global Constraints

- Pinned digest literals, verbatim: new 2.4.1 tree `063f08cdc225ffc488d874c81860de376d5a9f9ad1c906ca84aa59f6193f0625`; 2.4.0 tree `549c3b552f8f001f2a2f6059acf4c8f0df6e9475f80b8d155784dabb68e7b670`; Multi base `346528b53eaa04b495b87e3faf583a79d6e0cbb1d35d7f3881c442b0a39ddf8f`; Multi 2.3.4 outline `225af2c6eba7a017593ca8a0f38114a98580ed67844a4d8465fed2c96f7cb75f`; Multi 2.4.0 outline `f33e846c45eca0c62d343526fc9c3ee72d0e8585cc42b372c0fde9460ef18c85`.
- Witness version is the string `"2.4.2"`, never derived at runtime.
- New note sentence, verbatim: "A shader version inside the supported range did not verify against its measured source. Its triangles stay unproven."
- Every sentence a human reads (comments, report strings, changelog) follows simple english: short active sentences, one idea per sentence, no semicolons, no contractions.
- Never weaken or delete an existing valid test. Update pins, keep assertions.
- Before any Unity MCP call: read `mcpforunity://instances`, and verify the target instance's `Application.dataPath` equals this repository root. Stop on mismatch. Tests never run in any other project.
- After any file change: `xd://mcp__unitymcp_refresh_unity` and wait for compilation before running tests.
- Run tests with `xd://mcp__unitymcp_run_tests` (EditMode), poll `xd://mcp__unitymcp_get_test_job` to completion. A filtered run reporting 0 tests is a failure. Record observed counts.
- One commit per task. Commit messages: `feat: ...` for behavior, `test: ...` / `docs: ...` per content. Never stage unrelated files. Never push, never open PRs.
- Decision IDs in code comments come from the spec's B namespace (B1-B6) only.

---

### Task 1: Flip admitted rows, constants, and witnesses to 2.4.1

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToonSourceAttestationTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/VerifiedLilToonTestSeams.cs`

**Interfaces:**
- Consumes: existing `LilToonSourceAttestation` constants and `AdmittedRows()` / `AdmittedMultiRows()` accessors. No signature changes.
- Produces: `PackageVersion == "2.4.1"`, `IncludeTreeDigest == 063f…`, six admitted rows ending at 2.4.1. Task 2 and Task 3 rely on these values.

- [ ] **Step 1: Update the pinning tests (RED)**

In `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`:

a. `AdmittedVersionRows_AreTheMeasuredSet` (around line 1760). Version list gains `"2.4.1"`, plus a seventh-tree uniqueness assertion:

```csharp
            Assert.That(rows.Select(row => row.Version).ToList(), Is.EqualTo(
                new[] { "2.3.0", "2.3.1", "2.3.2", "2.3.3", "2.3.4", "2.4.0",
                    "2.4.1" }));
```

and after the existing `rows.Take(5)` / `rows[5]` assertion add:

```csharp
            Assert.That(
                rows.Take(6).Select(row => row.IncludeDigest),
                Has.None.EqualTo(rows[6].IncludeDigest),
                "the 2.4.1 tree matches no earlier row");
```

The Multi rows assertion stays exactly as is (two rows).

b. `PinnedDigests_AreTheTask0Measurements` (around line 2231). Replace the include-tree block:

```csharp
            // The flip moved the include-tree pin to the newest admitted
            // tree (2.4.1). The digest was computed from the official 2.4.1
            // tag tree on 2026-10-20 by the production recipe, licensed by
            // reproducing the 2.4.0 pin from the 2.4.0 tag tree (spec B1).
            // The 2.4.0 anchor lives in the AdmittedPackageVersions row
            // literal and in the S11 tests. Installed-shape confirmation is
            // outstanding.
            Assert.That(
                LilToonSourceAttestation.IncludeTreeDigest,
                Is.EqualTo(
                    "063f08cdc225ffc488d874c81860de376d5a9f9ad1c906ca84aa59f6193f0625"));
```

c. `Verify_AcceptsAdmittedPatchVersionWithItsOwnTree` (around line 2737). Add a TestCase and update the doc comment header line to "2.3.0 through 2.4.1", plus append to the provenance sentence: "the 2.4.1 digest was computed from the official tag tree (38 files) under the anchor license on 2026-10-20".

```csharp
        [TestCase("2.4.1",
            "063f08cdc225ffc488d874c81860de376d5a9f9ad1c906ca84aa59f6193f0625")]
```

d. Pairing witness flips. In `ApprovedRangePatchWithTheNearestRowTreeVerifies` (around line 1838): `[TestCase("2.4.1")]` becomes `[TestCase("2.4.2")]`, and the comment (lines 1841-1844) becomes "2.4.2 and 2.4.9 both pair the 2.4.1 row, so the evidence presents that row's tree and the whole conjunction verifies honestly."

e. In `ApprovedRangeVersionWithAForeignTreeReportsModifiedSource` (around line 1853): `packageVersion: "2.4.1"` becomes `packageVersion: "2.4.2"`, and the comment becomes "The pairing still binds version to tree: 2.4.2 pairs the 2.4.1 row, so the 2.3.4 tree is a foreign tree and the answer is modified source, never UnsupportedVersion."

f. In the doc comment of `Verify_AcceptsApprovedNonRowVersionWithTheNearestRowTree` (the "2.4.3 pairs … (2.4.0)" comment): "(2.4.0)" becomes "(2.4.1)".

g. New exact-row drift test, placed after `ApprovedRangeVersionWithAForeignTreeReportsModifiedSource`:

```csharp
        [Test]
        public void AdmittedPatchVersionWithThePriorTreeReportsModifiedSource()
        {
            // The 2.4.1 row binds version to its own tree. A 2.4.1 label
            // over the 2.4.0 tree is a foreign tree, and the answer is
            // modified source, never UnsupportedVersion (spec B5).
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(
                        packageVersion: "2.4.1",
                        includeDigest:
                            "549c3b552f8f001f2a2f6059acf4c8f0df6e9475f80b8d155784dabb68e7b670"),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
        }
```

If the `Evidence` helper or `diagnostic.Code` shape differs at implementation time, match the sibling test at `:1853-1870` exactly for access patterns, keeping the assertions above.

In `Tests/Editor/Semantics/LilToonSourceAttestationTests.cs`:

h. Add a mirror const beside `Multi240OutlineDigest` (around line 211):

```csharp
        private const string Multi240IncludeTree =
            "549c3b552f8f001f2a2f6059acf4c8f0df6e9475f80b8d155784dabb68e7b670";
```

i. In `MultiContainer_LooseInstall_TreeIdentifiesTheRow` (around line 588): the two cases that pass `LilToonSourceAttestation.IncludeTreeDigest` become literal-based, and two new cases pin that the 2.4.1 tree identifies no Multi row:

```csharp
        [TestCase(Multi234IncludeTree, Multi234BaseDigest, ExpectedResult = true)]
        [TestCase(
            Multi240IncludeTree, Multi234BaseDigest,
            ExpectedResult = true)]
        [TestCase(Multi234IncludeTree, Multi240OutlineDigest, ExpectedResult = false)]
        [TestCase(
            Multi240IncludeTree, Multi234OutlineDigest,
            ExpectedResult = false)]
        [TestCase(
            LilToonSourceAttestation.IncludeTreeDigest, Multi234BaseDigest,
            ExpectedResult = false)]
        [TestCase(
            LilToonSourceAttestation.IncludeTreeDigest, Multi234OutlineDigest,
            ExpectedResult = false)]
```

(The last two cases assert the post-flip const, the 2.4.1 tree, identifies no Multi row loosely, per spec B2 item 3.)

j. In `Tests/Editor/Build/VerifiedLilToonTestSeams.cs`, flip the witness (spec B3 item 1). Around line 476:

```csharp
        internal const string ApprovedRangeVendorVersion = "2.4.2";
```

Update its doc comment (around 472-475): "The default declared package version: an approved, non-row version. It pairs the 2.4.1 row for the attestation-side pins and rides the version-keyed routing arms in the pipeline fixtures. The scope's constructor takes another version, so a boundary test can install the same package out of range. When upstream ships a real 2.4.2, or when a later admission makes 2.4.2 a row, re-sweep this witness to a version outside the rows. Runtime derivation from the row list is the escape hatch." The const's consumers (`Tests/Editor/Build/AlphaSeparationPreparationTests.cs:3332-3333`, `:3673-3674`, and the DryRun tests) read the constant and need no edit.

- [ ] **Step 2: Observe RED**

Refresh Unity, then run the two test classes filtered: `LilToonAttestationTests` and `LilToonSourceAttestationTests`. Expected failures: the rows list, the digest audit, the loose-install const cases, the new drift test (missing diagnostic shape), and the flipped pairing witnesses. Record the observed failure count.

- [ ] **Step 3: Flip production (GREEN)**

In `Editor/Semantics/LilToon/LilToonSourceAttestation.cs`:

a. Line ~345 header comment: "admitted lilToon versions, 2.3.0 through 2.4.1".

b. Line ~362: `internal const string PackageVersion = "2.4.1";` (comment unchanged).

c. Lines ~391-394, comment and constant:

```csharp
        // The newest admitted tree digest (2.4.1). Computed from the
        // official 2.4.1 tag tree on 2026-10-20 by the recipe below,
        // licensed by reproducing the 2.4.0 pin from the 2.4.0 tag tree
        // (spec B1). The 2.4.0 digest lives in the AdmittedPackageVersions
        // row literal. Installed-shape confirmation is outstanding.
        internal const string IncludeTreeDigest =
            "063f08cdc225ffc488d874c81860de376d5a9f9ad1c906ca84aa59f6193f0625";
```

d. Row-block comment (~396-412): after the sentence naming the 2.4.0 tree difference, add: "The 2.4.1 row is computed from the official 2.4.1 tag tree by the recipe below. The anchor reproduced the 2.4.0 digest from the 2.4.0 tag tree on 2026-10-20. Installed-shape confirmation is outstanding (spec B1)." Replace the closing ban sentence with: "Never re-derive the generated shader and pass digests from the lilToon repository, whose committed generated shaders are stale relative to their own tag's generator. Include-tree rows may cite the official tag tree when the anchor reproduces the previous row's digest."

e. Rows array (~413-427): the final row becomes explicit 2.4.0 plus the moved tuple pair:

```csharp
                ("2.4.0",
                    "549c3b552f8f001f2a2f6059acf4c8f0df6e9475f80b8d155784dabb68e7b670"),
                (PackageVersion, IncludeTreeDigest),
```

- [ ] **Step 4: Observe GREEN**

Run the same two filtered classes. Expected: all pass, 0 failed, 0 skipped-in-error. Record counts.

- [ ] **Step 5: Full product suite**

Run the full EditMode product suite (`Alrauna.Amuse.Tests.Editor` plus research assembly if the runner exposes it; otherwise the whole EditMode run). Expected: 0 failures. The installed 2.4.0 stays attested, so no consent path arms. If a test fails that this plan does not anticipate, STOP for the owner (spec halt point 3).

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToonSourceAttestationTests.cs
git commit -m "feat: admit liltoon 2.4.1 as the newest measured row"
```

---

### Task 2: Preview note for approved-but-unverified sources

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/ClosedAlphaCaptureOutcome.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`

**Interfaces:**
- Consumes: `PackageVersion`/rows from Task 1; the D3 suppression reads `CapturedVersionIsApproved` (`Editor/Semantics/UnityMaterialSemantics.cs:413-425`).
- Produces: `ClosedAlphaCaptureOutcome.UnattestedApprovedFlags` (`IReadOnlyList<bool>`, empty default, mirrors `UnattestedOrdinals`), `CapturedAlphaMaterial.CapturedVersionApproved` (bool, default false), `NoteKind.ApprovedUnverifiedSource`. Task 3's changelog names the note.

- [ ] **Step 1: Write the failing tests (RED)**

a. In `Tests/Editor/DryRun/MaterialTransformationRowTests.cs`, extend `NoteSentencesMatchTheSpec` (around line 94):

```csharp
            Assert.That(
                RowText.NoteSentence(NoteKind.ApprovedUnverifiedSource),
                Is.EqualTo(
                    "A shader version inside the supported range did not " +
                    "verify against its measured source. Its triangles " +
                    "stay unproven."));
```

b. In `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`, extend `ApprovedVersionStaysUnknownInDryRun` (around line 1337). After the existing renderer-row assertions, add avatar-row assertions:

```csharp
                Assert.That(
                    dryRun.AvatarRows.Count(row =>
                        row.Sentence == RowText.NoteSentence(
                            NoteKind.ApprovedUnverifiedSource)),
                    Is.EqualTo(1),
                    "the approved unverified source names its cause once");
                Assert.That(
                    dryRun.AvatarRows.Any(row =>
                        row.Sentence == RowText.NoteSentence(
                            NoteKind.LockedMaterials)),
                    Is.False,
                    "the false locked-materials note must not fire for an " +
                    "approved unverified source");
```

Check `AvatarRows` is the exposed accessor name on the dry run (`Editor/DryRun/AlphaSeparationDryRun.cs:135-141`); use the real one.

c. New test beside it, same harness shape, pinning the conjunct and the out-of-range fallback. Model it on `ApprovedVersionStaysUnknownInDryRun`'s vendor-scope scaffolding:

```csharp
        [UnityTest]
        public IEnumerator ApprovedUnverifiedNoteRoutingPins()
        {
            // The default vendor scope carries the approved witness version
            // (2.4.2 after Task 1). Its capture fails on identity, so the
            // flag path fires. The explicit "2.5.0" boundary scope refuses
            // out of range, so the consent-subject note stays.
            var vendor = new VerifiedLilToonTestSeams
                .ApprovedRangeVendorScope();
            var outOfRange = new VerifiedLilToonTestSeams
                .ApprovedRangeVendorScope("2.5.0");
            var installed = 0;
            try
            {
                vendor.InstallCutoutShader();
                outOfRange.InstallCutoutShader();
                installed = 2;
                vendor.RequestRegistration();
                outOfRange.RequestRegistration();
                var guard = 0;
                while ((!vendor.IsRegistered() || !outOfRange.IsRegistered()) &&
                    guard++ < 600)
                {
                    yield return null;
                }

                var approvedRows = RunApprovedAvatar(vendor);
                Assert.That(
                    approvedRows.Any(row =>
                        row.Sentence == RowText.NoteSentence(
                            NoteKind.ApprovedUnverifiedSource)),
                    Is.True, "the approved avatar earns the named cause");
                var outOfRangeRows = RunApprovedAvatar(outOfRange);
                Assert.That(
                    outOfRangeRows.Any(row =>
                        row.Sentence == RowText.NoteSentence(
                            NoteKind.LockedMaterials)),
                    Is.True, "the out-of-range avatar keeps the consent note");
                Assert.That(
                    outOfRangeRows.Any(row =>
                        row.Sentence == RowText.NoteSentence(
                            NoteKind.ApprovedUnverifiedSource)),
                    Is.False, "the out-of-range avatar earns no named cause");
            }
            finally
            {
                if (installed >= 1)
                {
                    vendor.TearDown();
                }
                if (installed >= 2)
                {
                    outOfRange.TearDown();
                }
            }
        }
```

`RunApprovedAvatar` is a small private helper extracting the avatar-build body (avatar root, optimizer, texture, material, renderer, `NewDryRun`, `Run`) from `ApprovedVersionStaysUnknownInDryRun`; refactor that body into the helper and reuse it in both tests. The scope constructor taking an explicit version exists (`Tests/Editor/Build/VerifiedLilToonTestSeams.cs:508-512`); match its real parameter shape.

If two embedded packages cannot register side by side (same package name `com.alrauna.amuse.tests.vendor`), run the two avatars sequentially: build and assert the approved avatar, tear down, install the out-of-range scope, build and assert. Keep the assertions identical.

d. Cache-hit test: run the same avatar twice through the same dry run instance pattern the suite already uses for cache coverage (find the existing cache-hit test in `AlphaSeparationDryRunTests.cs` and mirror its second-run call). Assert the note appears exactly once across both runs.

- [ ] **Step 2: Observe RED**

Run `MaterialTransformationRowTests` and `AlphaSeparationDryRunTests` filtered. Expected: compile error on `NoteKind.ApprovedUnverifiedSource` (missing member), then routing failures. Record observations.

- [ ] **Step 3: Implement (GREEN)**

a. `Editor/Semantics/ClosedAlphaCaptureOutcome.cs`: add the mirror list and parameter:

```csharp
        internal ClosedAlphaCaptureOutcome(
            IReadOnlyList<CapturedAlphaMaterial> captured,
            IReadOnlyList<int> unattestedOrdinals,
            IReadOnlyList<bool> unattestedApprovedFlags = null)
        {
            Captured = new ReadOnlyCollection<CapturedAlphaMaterial>(
                new List<CapturedAlphaMaterial>(captured));
            UnattestedOrdinals =
                new ReadOnlyCollection<int>(
                    new List<int>(unattestedOrdinals));
            UnattestedApprovedFlags =
                new ReadOnlyCollection<bool>(
                    unattestedApprovedFlags == null
                        ? new List<bool>()
                        : new List<bool>(unattestedApprovedFlags));
        }

        internal IReadOnlyList<bool> UnattestedApprovedFlags { get; }
```

Extend the class doc: "UnattestedApprovedFlags mirrors UnattestedOrdinals one to one when non-empty. It names which failed members carried an approved package version with an identity failure. Empty means none. It is a mirror, not a partition member."

b. `Editor/Semantics/UnityMaterialSemantics.cs`, shared capture middle: before the loop, `var approvedFlags = new List<bool>();`. At the single `failed.Add(index)` site (around line 401), compute and append:

```csharp
                // The D3 suppression conjunct over the failed record:
                // approval alone is not enough. A Multi record refused on
                // state, for example the clipping canceller, must not earn
                // the preview's unverified-source note (spec B4).
                var failedRecord = result[index];
                var identityRefused = failedRecord.Family !=
                    CapturedAlphaMaterialFamily.LilToonMulti ||
                    (failedRecord.MultiResolution != null &&
                        failedRecord.MultiResolution.Refusal ==
                            LilToonMultiResolutionRefusal.AttestationFailed);
                approvedFlags.Add(
                    CapturedVersionIsApproved(failedRecord) && identityRefused);
                failed.Add(index);
```

Pass `approvedFlags` as the third constructor argument at the outcome construction (around line 403).

c. `CapturedAlphaMaterial`: add property and constructor parameter:

```csharp
        /// <summary>
        /// Whether the failed capture that produced this sentinel carried
        /// an approved package version with an identity failure. Only the
        /// sentinel builder sets it true. Live captures leave it false.
        /// The preview's cause note routes on it (spec B4).
        /// </summary>
        internal bool CapturedVersionApproved { get; }
```

Constructor: `bool capturedVersionApproved = false` as the last parameter, assigned in the body. Match the existing constructor's assignment style.

d. `UnattestedMaterial` (`:1289-1322`): add `bool capturedVersionApproved = false` parameter, pass through to the constructor.

e. `Editor/Host/UnityAnimationEvidenceCapture.cs`, substitution loop (around line 711): track the failed index and pass the flag:

```csharp
                var failedOrdinal = 0;
                for (var index = 0; index < attestedIndices.Count; index++)
                {
                    var admittedIndex = attestedIndices[index];
                    if (failedOrdinals.Contains(index))
                    {
                        var approved = batchOutcome.UnattestedApprovedFlags;
                        var approvedFlag = failedOrdinal < approved.Count &&
                            approved[failedOrdinal];
                        failedOrdinal++;
                        var lockedRefusal = lockedRefusalCheck?.Invoke(
                            admitted[admittedIndex]) ?? RendererAnalysisRefusal.None;
                        capturedByIndex[admittedIndex] =
                            UnityMaterialSemantics.UnattestedMaterial(
                                lockedRefusal,
                                admitted[admittedIndex],
                                resolveRegisteredSource,
                                approvedFlag);
                    }
```

The count invariant at `:703-710` stays unchanged.

f. `Editor/DryRun/MaterialTransformationRow.cs`: add `ApprovedUnverifiedSource` to `NoteKind`, add the sentence case:

```csharp
                case NoteKind.ApprovedUnverifiedSource:
                    return "A shader version inside the supported range "
                        + "did not verify against its measured source. Its "
                        + "triangles stay unproven.";
```

g. `Editor/DryRun/AlphaSeparationDryRun.cs`, `RecordConsentFacts` (around line 1222): insert the routing branch after the `LockedIdentityRefusal` check, before the existing `Unsupported` fallback:

```csharp
                // An approved version whose identity failed earns its own
                // named cause. The consent-subject note below is false for
                // it: nothing is locked, and the D3 approval arm converts
                // without asking (spec B4).
                if (captured.Family == CapturedAlphaMaterialFamily.Unsupported
                    && captured.CapturedVersionApproved)
                {
                    AddNoteOnce(NoteKind.ApprovedUnverifiedSource);
                    continue;
                }
```

- [ ] **Step 4: Observe GREEN**

Run the two DryRun test classes and `UnityAnimationEvidenceCaptureTests` (Host substitution) plus `UnityMaterialSemanticsTests` filtered. Expected: all pass. Then the full EditMode product suite. Record counts.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/ClosedAlphaCaptureOutcome.cs \
  Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs \
  Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs \
  Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs \
  Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs \
  Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs
git commit -m "feat: name the unverified-source cause in the dry run"
```

---

### Task 3: Documentation

**Files:**
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md`
- Modify: `AGENTS.md`

**Interfaces:**
- Consumes: shipped behavior from Tasks 1-2.
- Produces: accurate user-facing version and preview statements.

- [ ] **Step 1: Package changelog**

Add an entry at the top of the changelog's unreleased/current section, matching its existing entry style: support for lilToon 2.4.1 as a measured version, and the new preview note naming the cause for approved-but-unverified sources. Simple english. Date the entry 2026-10-20.

- [ ] **Step 2: Repository guide**

In `AGENTS.md` (repository root), Project Overview: change "exact rows through 2.4.0" to "exact rows through 2.4.1". In the DryRun sentence of the architecture section, add that the preview names the cause for approved-but-unverified sources.

- [ ] **Step 3: Verify and commit**

No product behavior changes. Run `git diff --check`. Skim both files for the identifier classes (machine paths, host names, ports, private names) and for semicolons or contractions in prose. Commit:

```bash
git add Packages/com.alrauna.amuse/CHANGELOG.md AGENTS.md
git commit -m "docs: record liltoon 2.4.1 support and the preview cause note"
```

---

## Self-review record

- Spec coverage: B1 → Task 1 Steps 1a-c, 3; B2 → Task 1 Steps 1h-i, 3e; B3 → Task 1 Steps 1d-f, 1j; B4 → Task 2; B5 → Task 1; B6 → Task 3.
- No placeholders: every code step carries its code. Two named refactor helpers (`RunApprovedAvatar`, the `Evidence` access pattern) point at concrete existing code to copy.
- Type consistency: `UnattestedApprovedFlags`, `CapturedVersionApproved`, `ApprovedUnverifiedSource` spelled identically everywhere.
- RED discipline: Task 1's witness flips and row pins fail against pre-flip production in one observed RED run, then land with the production flip in one commit so the suite never sits broken. Task 2's new API is RED by compile error, then routed.
