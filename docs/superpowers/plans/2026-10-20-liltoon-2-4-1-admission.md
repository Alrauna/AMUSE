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
- Every sentence a human reads (comments, report strings, changelog) follows simple english: short active sentences, one idea per sentence, no semicolons, no contractions. Semicolons inside C# code are syntax and fine.
- Never weaken or delete an existing valid test. Update pins, keep assertions.
- Before any Unity MCP call: read `mcpforunity://instances`, and verify the target instance's `Path.GetDirectoryName(Application.dataPath)` equals this repository root. Stop on mismatch. Tests never run in any other project.
- After any file change: `xd://mcp__unitymcp_refresh_unity` and wait for compilation before running tests.
- Run tests with `xd://mcp__unitymcp_run_tests` (EditMode), poll `xd://mcp__unitymcp_get_test_job` to completion. A filtered run reporting 0 tests is a failure. Record observed counts.
- One commit per task. `git diff --check` before every commit. Commit messages: `feat: ...` for behavior, `docs: ...` per content. Never stage unrelated files. Never push, never open PRs.
- Decision IDs in code comments come from the spec's B namespace (B1-B6) only.

## Preflight rulings from the plan review (2026-10-20)

1. Ruling: the const-pinned Multi pairing cases (`MultiContainer_CorrectPerVersionPairing_Verifies`, `Tests/Editor/Semantics/LilToonSourceAttestationTests.cs:518-560`) auto-move and stay green across the flip. Verified in source: `TryMultiColumnDigest` D2-pairs an approved non-row version to the nearest Multi row (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:2810-2834`), and `TryVerifyMultiContainer` steps 3-5 accept a packaged 2.4.1 Multi presenting the 2.4.1 tree with the 2.4.0 container bytes (`:2516-2560`). One reviewer claimed these cases break. The claim is rejected on this evidence.
2. Ruling: the new routing branch sits after the locked-identity check in `RecordConsentFacts`, not before it. A locked and approved material keeps its true LockedMaterials note, which names its real gate. The spec's "first branch" wording narrows to "the first branch that is not the locked-identity check". The spec carries this amendment.
3. Ruling: the identity-failure conjunct is extracted as an internal helper so its negative arm is testable without a real Multi capture. The capture middle calls the helper. Unit tests feed it hand-built records.

---

### Task 1: Flip admitted rows, constants, and witnesses to 2.4.1

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToonSourceAttestationTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/VerifiedLilToonTestSeams.cs`

**Interfaces:**
- Consumes: existing `LilToonSourceAttestation` constants and `AdmittedRows()` / `AdmittedMultiRows()` accessors. No signature changes.
- Produces: `PackageVersion == "2.4.1"`, `IncludeTreeDigest == 063f…`, seven admitted rows ending at 2.4.1. Task 2 relies on these values.

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

c. `Verify_AcceptsAdmittedPatchVersionWithItsOwnTree` (around line 2737). Add a TestCase, and update two comment sites: the `// --- S11: admitted versions 2.3.0 through 2.4.0` banner becomes "through 2.4.1", and the doc comment's first sentence "byte-identical across 2.3.0-2.4.0" becomes "2.3.0-2.4.1". Append to the provenance sentence: "the 2.4.1 digest was computed from the official tag tree (38 files) under the anchor license on 2026-10-20".

```csharp
        [TestCase("2.4.1",
            "063f08cdc225ffc488d874c81860de376d5a9f9ad1c906ca84aa59f6193f0625")]
```

d. Pairing witness flips. In `ApprovedRangePatchWithTheNearestRowTreeVerifies` (around line 1838): `[TestCase("2.4.1")]` becomes `[TestCase("2.4.2")]`, and the comment (lines 1841-1844) becomes "2.4.2 and 2.4.9 both pair the 2.4.1 row, so the evidence presents that row's tree and the whole conjunction verifies honestly."

e. In `ApprovedRangeVersionWithAForeignTreeReportsModifiedSource` (around line 1853): `packageVersion: "2.4.1"` becomes `packageVersion: "2.4.2"`, and the comment becomes "The pairing still binds version to tree: 2.4.2 pairs the 2.4.1 row, so the 2.3.4 tree is a foreign tree and the answer is modified source, never UnsupportedVersion."

f. In the doc comment of `Verify_AcceptsApprovedNonRowVersionWithTheNearestRowTree`: "(2.4.0)" becomes "(2.4.1)".

g. New exact-row drift test, placed after `ApprovedRangeVersionWithAForeignTreeReportsModifiedSource`, matching that sibling's access patterns:

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

In `Tests/Editor/Semantics/LilToonSourceAttestationTests.cs`:

h. Add a mirror const with a provenance comment beside `Multi240OutlineDigest` (around line 211):

```csharp
        // The 2.4.0 include-tree pin, computed from the official 2.4.0 tag
        // tree and reproduced on installed 2.4.0 shapes on 2026-10-19. It
        // anchors the 2.4.1 row's tag-tree license (spec B1).
        private const string Multi240IncludeTree =
            "549c3b552f8f001f2a2f6059acf4c8f0df6e9475f80b8d155784dabb68e7b670";
```

i. In `MultiContainer_LooseInstall_TreeIdentifiesTheRow` (around line 588): the two cases that pass `LilToonSourceAttestation.IncludeTreeDigest` become literal-based on `Multi240IncludeTree`, and two new cases pin that the 2.4.1 tree identifies no Multi row:

```csharp
        [TestCase(Multi234IncludeTree, Multi234BaseDigest, ExpectedResult = true)]
        [TestCase(Multi240IncludeTree, Multi234BaseDigest, ExpectedResult = true)]
        [TestCase(Multi234IncludeTree, Multi240OutlineDigest, ExpectedResult = false)]
        [TestCase(Multi240IncludeTree, Multi234OutlineDigest, ExpectedResult = false)]
        [TestCase(
            LilToonSourceAttestation.IncludeTreeDigest, Multi234BaseDigest,
            ExpectedResult = false)]
        [TestCase(
            LilToonSourceAttestation.IncludeTreeDigest, Multi234OutlineDigest,
            ExpectedResult = false)]
```

j. New packaged-Multi pairing test beside the pairing tests, using the file's existing `VerifyPairing` helper (around line 497):

```csharp
        [Test]
        public void MultiContainer_PackagedPairing_FlowsThroughThePairedRow()
        {
            Assert.That(
                VerifyPairing(
                    BaseContainerName, BaseContainerGuid,
                    "2.4.1",
                    "063f08cdc225ffc488d874c81860de376d5a9f9ad1c906ca84aa59f6193f0625",
                    Multi234BaseDigest, Multi234BaseDigest),
                Is.True, "the packaged 2.4.1 Multi install pairs the 2.4.0 row");
            Assert.That(
                VerifyPairing(
                    BaseContainerName, BaseContainerGuid,
                    "2.4.1", Multi240IncludeTree,
                    Multi234BaseDigest, Multi234BaseDigest),
                Is.False, "a 2.4.1 label over a foreign tree refuses");
        }
```

Match the real helper's parameter names at implementation time. The existing const-arg pairing cases (`:518-560`) move automatically and stay green (preflight ruling 1). Name them in the run record.

k. In `Tests/Editor/Build/VerifiedLilToonTestSeams.cs`, flip the witness (spec B3 item 1). Around line 476:

```csharp
        internal const string ApprovedRangeVendorVersion = "2.4.2";
```

Update its doc comment: "The default declared package version: an approved, non-row version. It pairs the 2.4.1 row for the attestation-side pins and rides the version-keyed routing arms in the pipeline fixtures. The scope's constructor takes another version, so a boundary test can install the same package out of range. When upstream ships a real 2.4.2, or when a later admission makes 2.4.2 a row, re-sweep this witness to a version outside the rows. Runtime derivation from the row list is the escape hatch." The const's consumers read the constant and need no edit.

- [ ] **Step 2: Pre-flip sweep (spec B5 item 6)**

Grep the package for `"2.4.1"` literals and for consumers of `PackageVersion` / `IncludeTreeDigest`. Expected remaining `"2.4.1"` sites, all keep-sites: `UnityMaterialSemanticsTests.cs:965, :992, :1071, :1089, :1112` (range-keyed approval witnesses) and the parse-parity list at `LilToonAttestationTests.cs:1976`. Expected const consumers that auto-move: the Multi pairing const cases (`LilToonSourceAttestationTests.cs:518-560`), the D3 fixture consumers. Record the sweep result. Any site outside these lists: stop and rule before the flip.

- [ ] **Step 3: Observe RED**

Refresh Unity. Run `LilToonAttestationTests` and `LilToonSourceAttestationTests` filtered. Expected failures: the rows list, the digest audit, the new own-tree TestCase (`2.4.1` + `063f…` is foreign pre-flip), the drift test (1g), the new packaged-Multi pairing test (1j), and the `(IncludeTreeDigest, Multi234BaseDigest)` loose-install case. Expected green despite the edits: the flipped pairing witnesses (the `Evidence` helper coalesces to the `IncludeTreeDigest` const, so they pair-and-match on both sides) and the seams consumers. Record observed counts.

- [ ] **Step 4: Flip production (GREEN)**

In `Editor/Semantics/LilToon/LilToonSourceAttestation.cs`:

a. Line ~345 header comment: "admitted lilToon versions, 2.3.0 through 2.4.1".

b. Line ~362:

```csharp
        internal const string PackageVersion = "2.4.1"; // newest admitted. The set is AdmittedPackageVersions (S11)
```

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

d. Row-block comment (~396-412). Add after the sentence naming the 2.4.0 tree difference: "The 2.4.1 row carries the one upstream patch past it. The 2.4.0 to 2.4.1 diff changed two changelogs, the package version fields, and Shader/Includes/openlit_core.hlsl. The 2.4.1 row is computed from the official 2.4.1 tag tree by the recipe below. The anchor reproduced the 2.4.0 digest from the 2.4.0 tag tree on 2026-10-20. The installed-shape follow-up re-measures, from two installed 2.4.1 shapes: the include tree, the shader, pass, and extended family digests, the Multi container digests, the GUIDs, the `_lilToonVersion` stamp, and the opaque-target tuple per the mandate at `Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:26-30`." Replace the closing ban sentence with: "Never re-derive the generated shader and pass digests from the lilToon repository, whose committed generated shaders are stale relative to their own tag's generator. Include-tree rows may cite the official tag tree when the anchor reproduces the previous row's digest."

e. Rows array (~413-427): the final row becomes explicit 2.4.0 plus the moved tuple pair:

```csharp
                ("2.4.0",
                    "549c3b552f8f001f2a2f6059acf4c8f0df6e9475f80b8d155784dabb68e7b670"),
                (PackageVersion, IncludeTreeDigest),
```

- [ ] **Step 5: Observe GREEN**

Run the same two filtered classes. Expected: all pass, 0 failed. Record counts.

- [ ] **Step 6: Full product suite**

Run the full EditMode product suite. Expected: 0 failures. The installed 2.4.0 stays attested, so no consent path arms. Any failure this plan does not anticipate: STOP for the owner (spec halt point 3).

- [ ] **Step 7: Commit**

`git diff --check` must be clean.

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToonSourceAttestationTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/VerifiedLilToonTestSeams.cs
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
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`

**Interfaces:**
- Consumes: `PackageVersion`/rows from Task 1; the D3 suppression reads `CapturedVersionIsApproved` (`Editor/Semantics/UnityMaterialSemantics.cs:413-425`).
- Produces: `ClosedAlphaCaptureOutcome.UnattestedApprovedFlags` (`IReadOnlyList<bool>`, empty default, mirrors `UnattestedOrdinals`), `UnityMaterialSemantics.IsApprovedIdentityFailure` (internal, one record in, bool out), `CapturedAlphaMaterial.CapturedVersionApproved` (bool, default false), `NoteKind.ApprovedUnverifiedSource`. Task 3's changelog names the note.

- [ ] **Step 1: Land the vocabulary (compile-clean base)**

Add the `NoteKind.ApprovedUnverifiedSource` member and the `NoteSentence` case in `Editor/DryRun/MaterialTransformationRow.cs`, and one sentence to the enum's doc comment: "The 2026-10-20 admission design adds ApprovedUnverifiedSource (spec B4)."

```csharp
                case NoteKind.ApprovedUnverifiedSource:
                    return "A shader version inside the supported range "
                        + "did not verify against its measured source. Its "
                        + "triangles stay unproven.";
```

- [ ] **Step 2: Write the failing tests (RED)**

a. In `Tests/Editor/DryRun/MaterialTransformationRowTests.cs`, extend `NoteSentencesMatchTheSpec` (around line 94):

```csharp
            Assert.That(
                RowText.NoteSentence(NoteKind.ApprovedUnverifiedSource),
                Is.EqualTo(
                    "A shader version inside the supported range did not " +
                    "verify against its measured source. Its triangles " +
                    "stay unproven."));
```

b. In `Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`, beside `InRangeMultiStateRefusalStillProducesItsSubject` (around line 995), pin the conjunct through the new helper:

```csharp
        [Test]
        public void ApprovedIdentityFailureAloneEarnsTheCauseFlag()
        {
            // An approved Multi record refused on state carries no cause
            // flag. The false "did not verify" sentence must never reach
            // the preview for a verified source (spec B4).
            Assert.That(
                UnityMaterialSemantics.IsApprovedIdentityFailure(
                    MultiCaptured("2.4.0",
                        LilToonMultiResolutionRefusal.ClippingCanceller)),
                Is.False);
            Assert.That(
                UnityMaterialSemantics.IsApprovedIdentityFailure(
                    MultiCaptured("2.4.0",
                        LilToonMultiResolutionRefusal.AttestationFailed)),
                Is.True);
        }
```

`MultiCaptured` names the hand-built refused-Multi record helper the sibling test uses (real name and refusal members at `:996-1018`; the clipping-canceller refusal member is whichever value that fixture uses for the state refusal). Match both at implementation time.

c. In `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`, extend `ApprovedVersionStaysUnknownInDryRun` (around line 1337). After the existing renderer-row assertions, add avatar-row assertions:

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

d. New sequential-scope routing test beside it. The two scope versions share one embedded package folder (`VerifiedLilToonTestSeams.cs:478-485`) and the manifest write early-returns when present, so side-by-side scopes cannot work. Install, build, assert, tear down, then install the next version. Extract the avatar-build body of `ApprovedVersionStaysUnknownInDryRun` into a private helper `RunApprovedAvatar(VendorScope scope)` returning `dryRun.AvatarRows`, refactor the original test onto it, then:

```csharp
        [UnityTest]
        public IEnumerator ApprovedAndOutOfRangeAvatarsRouteTheirNotes()
        {
            var vendor = new VerifiedLilToonTestSeams
                .ApprovedRangeVendorScope();
            vendor.InstallCutoutShader();
            vendor.RequestRegistration();
            var guard = 0;
            while (!vendor.IsRegistered() && guard++ < 600)
            {
                yield return null;
            }
            Assert.That(vendor.IsRegistered(), Is.True);

            var approvedRows = RunApprovedAvatar(vendor);
            Assert.That(
                approvedRows.Any(row =>
                    row.Sentence == RowText.NoteSentence(
                        NoteKind.ApprovedUnverifiedSource)),
                Is.True, "the approved avatar earns the named cause");
            Assert.That(
                approvedRows.Any(row =>
                    row.Sentence == RowText.NoteSentence(
                        NoteKind.LockedMaterials)),
                Is.False, "the approved avatar keeps no consent note");
            vendor.TearDown();

            var outOfRange = new VerifiedLilToonTestSeams
                .ApprovedRangeVendorScope("2.5.0");
            outOfRange.InstallCutoutShader();
            outOfRange.RequestRegistration();
            guard = 0;
            while (!outOfRange.IsRegistered() && guard++ < 600)
            {
                yield return null;
            }
            Assert.That(outOfRange.IsRegistered(), Is.True);

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
            outOfRange.TearDown();
        }
```

Match the scope constructor's real parameter shape (`VerifiedLilToonTestSeams.cs:508-512`) and the teardown pattern the sibling test uses in its `finally`.

e. Cache-hit test: run the same avatar twice through the cache pattern an existing cache-hit test in `AlphaSeparationDryRunTests.cs` uses. Assert the note appears exactly once across both runs.

f. Attested avatars: notes are absent by construction. `RecordConsentFacts` emits only from the locked-refusal branch and the Unsupported-sentinel branch, which attested records never match. Record this argument as a comment on test (d) instead of a new harness.

- [ ] **Step 3: Observe RED**

Run `MaterialTransformationRowTests`, `UnityMaterialSemanticsTests` (filtered to the new test), and `AlphaSeparationDryRunTests`. Expected: the sentence pin passes (vocabulary landed in Step 1); the conjunct test fails to compile on `IsApprovedIdentityFailure`; the routing assertions fail (no flag plumbing). Record counts.

- [ ] **Step 4: Implement (GREEN)**

a. `Editor/Semantics/UnityMaterialSemantics.cs`: add the internal helper beside `CapturedVersionIsApproved`:

```csharp
        /// <summary>
        /// The D3 suppression conjunct over one failed capture record
        /// (spec B4). Approval alone is not enough: an approved Multi
        /// record refused on state, for example the clipping canceller,
        /// names no unverified source. For the regular lilToon families
        /// the capture failure is itself the identity verify, so approval
        /// plus failure suffices.
        /// </summary>
        internal static bool IsApprovedIdentityFailure(
            CapturedAlphaMaterial record)
        {
            var identityRefused = record.Family !=
                CapturedAlphaMaterialFamily.LilToonMulti ||
                (record.MultiResolution != null &&
                    record.MultiResolution.Refusal ==
                        LilToonMultiResolutionRefusal.AttestationFailed);
            return CapturedVersionIsApproved(record) && identityRefused;
        }
```

b. Shared capture middle: before the loop, `var approvedFlags = new List<bool>();`. At the single `failed.Add(index)` site (around line 401):

```csharp
                approvedFlags.Add(
                    IsApprovedIdentityFailure(result[index]));
                failed.Add(index);
```

Pass `approvedFlags` as the third constructor argument at the outcome construction (around line 403).

c. `Editor/Semantics/ClosedAlphaCaptureOutcome.cs`: add the mirror list and parameter:

```csharp
        internal ClosedAlphaCaptureOutcome(
            IReadOnlyList<CapturedAlphaMaterial> captured,
            IReadOnlyList<int> unattestedOrdinals,
            IReadOnlyList<bool> unattestedApprovedFlags = null)
```

with `UnattestedApprovedFlags` wrapping a null-safe copy, and one added doc sentence: "UnattestedApprovedFlags mirrors UnattestedOrdinals one to one when non-empty. It names which failed members carried an approved package version with an identity failure. Empty means none. It is a mirror, not a partition member."

d. `CapturedAlphaMaterial`: add the property and constructor parameter:

```csharp
        /// <summary>
        /// Whether the failed capture that produced this sentinel carried
        /// an approved package version with an identity failure. Only the
        /// sentinel builder sets it true. Live captures leave it false.
        /// The preview's cause note routes on it (spec B4).
        /// </summary>
        internal bool CapturedVersionApproved { get; }
```

Constructor: `bool capturedVersionApproved = false` as the last parameter, assigned in the body, matching the existing style.

e. `UnattestedMaterial` (`:1289-1322`): add `bool capturedVersionApproved = false`, pass through, and add one `<para>` to its doc: "True in capturedVersionApproved marks the capture-failure sentinel for an approved version with an identity failure. The preview's cause note routes on it."

f. `Editor/Host/UnityAnimationEvidenceCapture.cs`, substitution loop (around line 711): track the failed index and pass the flag:

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

g. `Editor/DryRun/AlphaSeparationDryRun.cs`, `RecordConsentFacts` (around line 1222): insert the routing branch after the `LockedIdentityRefusal` check, before the existing `Unsupported` fallback:

```csharp
                // An approved version whose identity failed earns its own
                // named cause. The consent-subject note below is false for
                // it: nothing is locked, and the D3 approval arm converts
                // without asking (spec B4). A locked record keeps its true
                // locked note: the check above stays first.
                if (captured.Family == CapturedAlphaMaterialFamily.Unsupported
                    && captured.CapturedVersionApproved)
                {
                    AddNoteOnce(NoteKind.ApprovedUnverifiedSource);
                    continue;
                }
```

- [ ] **Step 5: Observe GREEN**

Run `MaterialTransformationRowTests`, `UnityMaterialSemanticsTests`, `AlphaSeparationDryRunTests`, and `UnityAnimationEvidenceCaptureTests` filtered. Expected: all pass. Then the full EditMode product suite. Expected: 0 failures. Any failure this plan does not anticipate: STOP for the owner (spec halt point 3). Record counts.

- [ ] **Step 6: Commit**

`git diff --check` must be clean.

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/ClosedAlphaCaptureOutcome.cs \
  Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs \
  Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs \
  Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs \
  Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs \
  Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs
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

Add an entry dated 2026-10-20 in the changelog's current style: support for lilToon 2.4.1 as a measured version, and the new preview note naming the cause for approved-but-unverified sources. Simple english.

- [ ] **Step 2: Repository guide**

In `AGENTS.md` (repository root), Project Overview: change "exact rows through 2.4.0" to "exact rows through 2.4.1". In the DryRun sentence of the architecture section, add that the preview names the cause for approved-but-unverified sources.

- [ ] **Step 3: Verify and commit**

No product behavior changes. Run `git diff --check`. Sweep both files for machine paths, host names, ports, private names, semicolons, and contractions in prose. Commit:

```bash
git add Packages/com.alrauna.amuse/CHANGELOG.md AGENTS.md
git commit -m "docs: record liltoon 2.4.1 support and the preview cause note"
```

---

## Review integration record

Five reviewers attacked plan v1 on 2026-10-20 (spec conformance, code accuracy, safety and security, architecture and ponytail, latent bugs). Integrated: the Task 1 commit now stages the seams file (four reviewers, CRITICAL), the two-scope test is sequential by construction (three reviewers), the state-refused Multi conjunct has a dedicated unit test through an extracted internal helper (three reviewers), the packaged-Multi pairing and foreign-tree cases exist as a new test with the real `VerifyPairing` helper, the pre-flip sweep is a named step with its expected-clean result, the row-block comment carries the four-file diff and the full follow-up work list, the MCP verification uses `Path.GetDirectoryName(Application.dataPath)`, the `PackageVersion` comment loses its semicolon, the RED expectations name exactly the tests that fail and the ones that stay green, the NoteKind doc comment and the new const carry provenance, `git diff --check` runs at every commit, the routing branch's precedence over the locked note is stated as a ruling, and halt-point 3 rides both full-suite steps.

Rejected with evidence: one reviewer's claim that the const-pinned Multi pairing cases break at the flip. `TryMultiColumnDigest` D2-pairs an approved non-row version to the nearest Multi row (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:2810-2834`), and `TryVerifyMultiContainer` steps 3-5 accept the packaged 2.4.1 shape (`:2516-2560`). The cases auto-move and stay green. Preflight ruling 1 records this.

## Self-review record

- Spec coverage: B1 → Task 1 Steps 1a-c, 4; B2 → Task 1 Steps 1h-j, 4e; B3 → Task 1 Steps 1d-f, 1k; B4 → Task 2; B5 → Task 1; B6 → Task 3.
- No placeholders: every code step carries its code. Named helpers point at concrete existing code.
- Type consistency: `UnattestedApprovedFlags`, `IsApprovedIdentityFailure`, `CapturedVersionApproved`, `ApprovedUnverifiedSource` spelled identically everywhere.
- RED discipline: Task 1 lands its RED pins and the production flip in one commit so the suite never sits broken. Task 2 splits RED: vocabulary first (compile-clean), then plumbing.
