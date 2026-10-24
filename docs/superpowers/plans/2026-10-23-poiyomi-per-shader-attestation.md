# Poiyomi per-shader attestation implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

Status: 2026-10-23, second revision. It integrates two adversarial plan reviews (compile order and spec coverage). Their findings moved the locked-original rule before the identity cutover, limited the float texture check, fixed two falsifiers, and named the right test classes.

**Goal:** Admit unlocked materials on the 9.3 shader that Poiyomi 10.x packages bundle, attest Poiyomi by shader file instead of by package, and close the rename-suffixed binding false positive.

**Architecture:** A fixed row table in `PoiyomiSourceAttestation` holds every admitted shader file as name, GUID, digest, Two Pass flag, and locked-original flag. One matcher and one name predicate replace the scalar pins and the package gates. A shader-independent rule in `AnimationBindingResolution` refuses bindings that extend a requested property name with `_`.

**Tech Stack:** C# for Unity 2022.3.22f1, NDMF 1.14.4, NUnit EditMode tests through the Unity Test Runner.

**Spec:** `docs/superpowers/specs/2026-10-23-poiyomi-per-shader-attestation-design.md` (the design). Executors read the design sections named in each task.

## Global Constraints

- Product paths are relative to `Packages/com.alrauna.amuse/`. `research:` means `Packages/com.alrauna.amuse.research/`.
- Row values, verbatim, in this order:
  1. 9.3.64 main: name `.poiyomi/Poiyomi Toon`, Two Pass false, GUID `CanonicalShaderGuid`, digest `31f2ff15615c5e2ac9b05fea08b6310731394d1b5a928b16048e7bde8f8b1755`, locked original admitted true.
  2. 9.3.64 Two Pass: name `.poiyomi/Poiyomi Toon Two Pass`, Two Pass true, GUID `TwoPassCanonicalShaderGuid`, digest `b1d9ecd3072d21db97001dd23f88d089996b809e4039891a05f64d2ffcd4df67`, locked original admitted true.
  3. Bundled main: name `.poiyomi/Old Versions/9.3/Poiyomi Toon`, Two Pass false, GUID `CanonicalShaderGuid`, digest `c812d4d39ce3352b6198fdfcb5de87eb07678739a4bce7e4afd18cea28be19f4`, locked original admitted false.
  4. Bundled Two Pass: name `.poiyomi/Old Versions/9.3/Poiyomi Toon Two Pass`, Two Pass true, GUID `TwoPassCanonicalShaderGuid`, digest `4fd1d8c1e35bd4e81a21c1dd6545ceb022542b67aa9996cb8341da7f3b64b65f`, locked original admitted false.
- All string comparisons are ordinal.
- Code comments that implement a decision cite its ID (K1 to K9) and the design path. No other decision letter is reused.
- Every English text a human reads uses simple English: short active sentences, one idea per sentence, no semicolons, no contractions. This binds comments, test messages, docs, and commit messages.
- No absolute path, machine name, user name, port, or instance name in any doc, comment, commit message, or test. No GUID value in a doc, comment, commit message, or test message. Existing test code that compares a GUID literal may stay.
- Never edit, stage, or commit `Packages/manifest.json`, `Packages/packages-lock.json`, `Packages/vpm-manifest.json`, `ProjectSettings/EditorSettings.asset`, `ProjectSettings/SceneTemplateSettings.json`, or `Assets/default.unity*`. They hold user-owned changes. Stage explicit paths only.
- A new `.cs` file needs its `.meta` file. Refresh Unity, then commit both.
- Never weaken a valid test. A test deleted with its behavior is named in this plan.
- RED means a test observed failing for the named reason before the fix. A compile failure on a new member counts as RED for new API. A test that passes on its first run is recorded as characterization.
- Tests run only in the dev editor instance, whose `Application.dataPath` is `<repo-root>/Assets`. Verify it before the first run in a session. Never run tests in the Lab instance.
- A filtered run that reports 0 tests is a failure. Record observed counts.
- Every commit leaves both test assemblies green.

## Review Focus

1. A locked material whose `OriginalShader` tag names the 9.3.64 shader while its GUID resolves to the bundled shader. Expected: the precheck refuses as `LockedPoiyomiOriginalShaderUnattested`, and the dry run does not defer it. Owned by Task 4.
2. A suffixed binding on a renamed texture's scale-offset, such as `material._MainTex_Body_ST.x`. Expected: `UnrecognizedMaterialBinding` when `_MainTex` is requested with scale-offset evidence. Owned by Task 1.
3. A stale digest memo after an in-place package update pairs a bundled name with the 9.3.64 digest. Expected: `ModifiedShaderSource`. Owned by Task 5.
4. A 10.x project where `.poiyomi/Poiyomi Toon` resolves to the 10.0 shader and the bundled shader attests. Expected: the research probe reports the bundled shader as matching. Owned by Task 3.
5. A future row that shares a name with a different Two Pass flag. Expected: the row-set invariant test fails. Owned by Task 2.

---

### Task 1: Rename-suffixed bindings refuse (K9)

Design: K9, section "Over-refusal surface", verified fact 14, test duty 10.

**Files:**
- Modify: `Editor/Host/AnimationBindingResolution.cs:117-130, 133-185, 280-310`
- Test: `Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`
- Possibly modify, after Step 5: `Tests/Editor/Build/TransientUnlockTransformationTests.cs:504-566`, `Tests/Editor/Build/TransientUnlockWindowCloseTests.cs:559-616`, `Tests/Editor/Build/TransientUnlockTestLifecycle.cs:749`

**Interfaces:**
- Produces: no signature change. `ResolveProofRelevant` and `IsUnrecognizedObjectMaterialBinding` change behavior only.

- [ ] **Step 1: Write the tests** in `UnityAnimationEvidenceCaptureTests`. Add one helper beside `Relevance()` (`:2110-2120`):

```csharp
// Relevance() plus "_MainTex" requested with scale-offset and source identity,
// and "_MainTex_ScrollRotate" as an extra requested vector.
private static MaterialEvidenceRequest RelevanceWithTexture() { ... }
```

Use `new TexturePropertyEvidenceRequest("_MainTex", TextureEvidenceKinds.ScaleOffset | TextureEvidenceKinds.SourceIdentity)` or the equivalent constructor the type offers.

```csharp
// --- Falsifier 1: a classifier that treats a rename-suffixed binding as Irrelevant ---
[TestCase("material._Cutoff_Body")]
[TestCase("material._Color_Body.a")]
[TestCase("material._MainTexPan_Body.x")]
[TestCase("material[0]._Cutoff_Body")]
public void RenameSuffixedRequestedPropertyFailsClosed(string property)
{
    Assert.That(
        UnityAnimationEvidenceCapture.ResolveProofRelevant(
            Bound(property), "Body", Relevance(), out _),
        Is.EqualTo(ProofRelevantBindingResolution.UnrecognizedMaterialBinding));
}

// --- Falsifier 2: a float check that leaves out scale-offset texture names ---
[Test]
public void RenameSuffixedTextureScaleOffsetFailsClosed()
{
    // "material._MainTex_Body_ST.x" with RelevanceWithTexture() -> UnrecognizedMaterialBinding.
}

// --- Falsifier 3: an object-binding check that ignores rename suffixes ---
[Test]
public void RenameSuffixedTextureObjectBindingIsUnrecognized()
{
    // Object binding "material._MainTex_Body" on path "Body",
    // type typeof(SkinnedMeshRenderer).FullName, with RelevanceWithTexture():
    // IsUnrecognizedObjectMaterialBinding returns true.
}

// Characterization: generated resolution runs first.
[Test]
public void TextureScaleOffsetComponentStillResolves()
{
    // "material._MainTex_ST.x" -> RendererWide, kind TextureScaleOffsetComponent.
}

// Characterization: a requested name that extends another requested name keeps its resolution.
[Test]
public void RequestedVectorThatExtendsATextureNameStillResolves()
{
    // "material._MainTex_ScrollRotate.x" -> RendererWide, kind VectorComponent.
}
```

Build the `CapturedObjectBinding` with the constructor shape used at `Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs:773` (type at `Editor/Host/CapturedAnimationEvidence.cs:64`). Leave `UnrequestedPropertiesRemainIrrelevant` and `ScaleOffsetIsNotDerivedWhenTheEvidenceKindIsNotRequested` (`:482-511`) unchanged. Both must still pass.

- [ ] **Step 2: Run and confirm RED**

Run: `run_tests` EditMode, `group_names: ["Alrauna.Amuse.Tests.Editor.Host.UnityAnimationEvidenceCaptureTests"]`.
Expected: the three falsifier tests fail with `Irrelevant` or `False`. The two characterization tests pass. Record both.

- [ ] **Step 3: Implement the rule in `AnimationBindingResolution`**

1. `CouldAddressAny` also returns true when `text.Length > property.Length`, `text` starts with `property`, and `text[property.Length] == '_'`.
2. `CouldAddressRelevantProperty` also checks the stems of the derived scale-offset properties, that is, the requested textures whose request includes scale-offset evidence. A texture requested without scale-offset evidence stays out of the float check.
3. `IsUnrecognizedObjectMaterialBinding` keeps its existing call over all requested texture names.
4. Generated-property resolution stays first.
5. The method comment cites K9 and gives the reason in short sentences: Thry writes rename-marked keyframes under a suffixed name, and the proof must never treat such a curve as irrelevant.

- [ ] **Step 4: Run the class and confirm GREEN**

Same command. Expected: every test in the class passes, including the two unchanged tests named in Step 1.

- [ ] **Step 5: Evaluate the suffixed-name tests in `Tests/Editor/Build/`**

Run: `run_tests` EditMode, `assembly_names: ["Alrauna.Amuse.Tests.Editor"]`.
The known candidates are the swap-cycle tests that bind `material._MainTex_ParityCape` and `material._MainTex_Cape`. Their purpose is the survival of a suffixed binding through the unlock window, not the alpha proof. Ruling: rename the bound property to a suffixed name of a property that no family requests, `material._EmissionMap_Cape` and `material._EmissionMap_ParityCape`. `_EmissionMap` is a presence-only property (`Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:60`). Put a one-line comment at each binding that states the reason: a suffix of a requested property now refuses the renderer (K9). Any other failure stops the task and goes in the report as a concern.

- [ ] **Step 6: Run both assemblies**

Run: `assembly_names: ["Alrauna.Amuse.Tests.Editor", "Alrauna.Amuse.Research.Tests.Editor"]`.
Expected: all pass. Record the counts.

- [ ] **Step 7: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/AnimationBindingResolution.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs <each test file changed in Step 5>
git commit -m "fix(host): refuse rename-suffixed material bindings"
```

---

### Task 2: Admitted source rows (K1, K3.1)

Design: K1, K3.1, K4.4, test duties 5 and 6.

**Files:**
- Modify: `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs`
- Test: `Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs`, class `PoiyomiSourceAttestationTests` (starts at `:178`). This is the test class for `PoiyomiSourceAttestation`.

**Interfaces:**
- Produces:
  - `internal readonly struct PoiyomiAdmittedSource` with `string ShaderName`, `bool IsTwoPass`, `string AssetGuid`, `string NormalizedSourceHash`, `bool LockedOriginalAdmitted`. It lives in `PoiyomiSourceAttestation.cs`, beside `PoiyomiSourceEvidence`.
  - `internal static IReadOnlyList<PoiyomiAdmittedSource> PoiyomiSourceAttestation.AdmittedSources()`
  - `internal static bool PoiyomiSourceAttestation.TryMatchAdmittedSource(string shaderName, string assetGuid, string normalizedSourceHash, out PoiyomiAdmittedSource row)`
  - `internal static bool PoiyomiSourceAttestation.TryClassifyShaderName(string shaderName, out bool isTwoPass)`

- [ ] **Step 1: Write the tests** in `PoiyomiSourceAttestationTests`.

```csharp
[Test]
public void AdmittedSourcesAreTheFourMeasuredRows()
{
    // Assert the exact four rows, in Global Constraints order, every field.
    // GUIDs compare to CanonicalShaderGuid and TwoPassCanonicalShaderGuid.
    // Digests compare to the literal values.
    // Message: "A new row needs the K5 admission measurement first."
}

[Test]
public void RowsSharingANameShareTheTwoPassFlag() { ... }

[Test]
public void EveryRowHasAHexGuidAndAHexDigest()
{
    // 32 lowercase hex for AssetGuid, 64 lowercase hex for NormalizedSourceHash.
}

[TestCase(".poiyomi/Old Versions/9.3/Poiyomi Toon", false)]
[TestCase(".poiyomi/Old Versions/9.3/Poiyomi Toon Two Pass", true)]
[TestCase(".poiyomi/Poiyomi Toon", false)]
[TestCase(".poiyomi/Poiyomi Toon Two Pass", true)]
public void AdmittedNamesClassifyWithTheirTwoPassFlag(string name, bool expected)
{
    Assert.That(PoiyomiSourceAttestation.TryClassifyShaderName(name, out var isTwoPass), Is.True);
    Assert.That(isTwoPass, Is.EqualTo(expected));
}

[TestCase(".poiyomi/Old Versions/9.0/Poiyomi Toon")]
[TestCase(".poiyomi/Old Versions/9.3/Poiyomi Toon World")]
[TestCase(".poiyomi/Poiyomi Toon Two Pas")]
[TestCase(null)]
public void OtherNamesDoNotClassify(string name) { ... Is.False ... }

// --- Falsifier 4: a matcher that admits any known digest regardless of the name ---
[Test]
public void MatchRequiresNameGuidAndDigestOfOneRow()
{
    // Bundled main name + main GUID + 9.3.64 main digest: false.
    // 9.3.64 main name + main GUID + bundled main digest: false.
    // Bundled main name + Two Pass GUID + bundled main digest: false.
    // Bundled main name + main GUID + bundled main digest: true, and row.LockedOriginalAdmitted is false.
}
```

- [ ] **Step 2: Run and confirm RED**

Run: `run_tests` EditMode, `group_names: ["Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiSourceAttestationTests"]`.
Expected: compile failure on the missing members.

- [ ] **Step 3: Implement the row type, `AdmittedSources()`, `TryMatchAdmittedSource`, and `TryClassifyShaderName`**

The table is a `static readonly` array in row order. The two new digests are private literals beside the table. The 9.3.64 rows reuse the existing digest constants in this task. Task 5 removes those constants. The row block comment records the evidence per row (source release, 2026-10-23 measurement, and the bundled rows' reuse of the 9.3.64 proof model) and states the K5 admission rule in short sentences.

- [ ] **Step 4: Run and confirm GREEN**

Same filter, then both assemblies. Expected: all pass. Record the counts.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs
git commit -m "feat(poiyomi): add admitted shader file rows"
```

---

### Task 3: Research probe follows the rows (K6)

Design: K6, test duty 9.

**Files:**
- Modify: `research:Tests/Editor/Calibration/CensusVendorProbe.cs`
- Modify: `research:Tests/Editor/Calibration/VendorReachabilityTests.cs:41-129, 520-530`
- Create: `research:Tests/Editor/Calibration/CensusVendorProbeTests.cs` and its `.meta`

**Interfaces:**
- Consumes: `PoiyomiSourceAttestation.AdmittedSources()`, `TryMatchAdmittedSource` (Task 2), and the existing `TryReadNormalizedSourceHashCached`.
- Produces:
  - `internal readonly struct CensusPoiyomiCandidate` with `string ShaderName`, `Shader Shader`, `string AssetGuid`, `string NormalizedSourceHash`.
  - `internal static IReadOnlyList<PoiyomiAdmittedSource> CensusVendorProbe.PoiyomiProbeRows()`. The rows with `IsTwoPass` false, in row order.
  - `internal static CensusPoiyomiCandidate? CensusVendorProbe.SelectPoiyomi(IReadOnlyList<CensusPoiyomiCandidate> resolvedInRowOrder)`. It returns the first candidate that matches a row through `TryMatchAdmittedSource`. Otherwise it returns the last candidate. It returns null for an empty list.
  - `bool CensusVendorPresence.MatchesAdmittedSource`.

- [ ] **Step 1: Write the tests** in `CensusVendorProbeTests`. Candidates use the real row strings with `Shader` null. No vendor file is needed.

```csharp
[Test]
public void ProbeRowsAreTheMainRowsInRowOrder()
{
    // Exactly two rows: ".poiyomi/Poiyomi Toon", then ".poiyomi/Old Versions/9.3/Poiyomi Toon".
    // No row has IsTwoPass true.
}

[Test]
public void SelectionPrefersTheCandidateThatMatchesARow()
{
    // First: name ".poiyomi/Poiyomi Toon", GUID of 32 zeros, any digest (a 10.0-like shader).
    // Second: the bundled main row's name, GUID, and digest.
    // Expect: the second candidate.
}

[Test]
public void SelectionReportsTheLastResolvedCandidateWhenNoneMatches() { ... }

[Test]
public void SelectionOfNoCandidatesReportsAbsence() { ... Is.Null ... }
```

- [ ] **Step 2: Run and confirm RED** (compile failure on the missing members).

Run: `run_tests` EditMode, `group_names: ["Alrauna.Amuse.Research.Tests.Editor.Calibration.CensusVendorProbeTests"]`.

- [ ] **Step 3: Implement**

1. `Probe(CensusVendorFamily.Poiyomi)` walks `PoiyomiProbeRows()`. For each name it calls `Shader.Find`. For a found shader it reads the GUID through `AssetDatabase.TryGetGUIDAndLocalFileIdentifier` (lowercase) and the digest through `PoiyomiSourceAttestation.TryReadNormalizedSourceHashCached`. Copy the guards from `PoiyomiSourceAttestation.GatherSourceEvidence` (`Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:217-247`): an empty asset path, `File.Exists`, and catches for `IOException` and `UnauthorizedAccessException`. An unreadable file gives a candidate with a null digest. The probe never throws for a missing or unreadable file.
2. Feed the candidates to `SelectPoiyomi`.
3. The Poiyomi presence carries `ExpectedPackageName = "com.poiyomi.toon"` from a research-owned constant, `ExpectedPackageVersion = null`, `InstalledPackageVersion = null`, and `MatchesAdmittedSource`. lilToon keeps its current fields. Its `MatchesAdmittedSource` is false and unused.
4. The class comment says the Poiyomi label is a family label, not a pin, and that Poiyomi attests by shader file (K6). The comment that says pins are never retyped as literals stays true for every pin.
5. `AnInstalledFamilyMatchesTheVersionAmuseAttests`: the Poiyomi branch asserts `presence.MatchesAdmittedSource` is true. The message names the reported shader and says that no admitted Poiyomi shader file is installed. The gate-case comment at `:58-66` drops "pins 9.3.64 exactly" and says that Poiyomi attests by shader file.
6. The installed-family line at `:520-530` names the reported Poiyomi shader instead of a null version.
7. The probe calls only `Shader.Find`, `AssetDatabase.GetAssetPath`, `AssetDatabase.TryGetGUIDAndLocalFileIdentifier`, `File.Exists`, and the product hash reader.

- [ ] **Step 4: Refresh Unity so the new file gets its `.meta`, then run and confirm GREEN**

Run: the class above, then both assemblies.
Expected: all pass. Gated tests that need an installed vendor package report Ignored in the dev project. Record the counts.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/CensusVendorProbe.cs Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/CensusVendorProbeTests.cs Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/CensusVendorProbeTests.cs.meta
git commit -m "test(research): probe Poiyomi by admitted shader file"
```

---

### Task 4: Locked originals stay on the 9.3.64 rows (K8)

Design: K8, test duties 7 and 8, Review Focus 1. This task lands before the identity cutover, so no commit ever admits a locked bundled original.

**Files:**
- Modify: `Editor/Build/LockedMaterialIdentity.cs:132-307`
- Modify: `Editor/DryRun/AlphaSeparationDryRun.cs:1223-1232`
- Modify: `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs` (one name query)
- Test: `Tests/Editor/Build/LockedMaterialIdentityTests.cs`
- Test: `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`

**Interfaces:**
- Consumes: Task 2 members. `PoiyomiSourceEvidence` keeps its nine-argument constructor in this task.
- Produces:
  - `internal static bool LockedMaterialIdentity.AttestsLockedOriginal(string originalShaderTag, in PoiyomiSourceEvidence resolved)`
  - `internal static bool PoiyomiSourceAttestation.IsLockedOriginalAdmittedName(string shaderName)`. True when a row with this name has `LockedOriginalAdmitted` true.

- [ ] **Step 1: Write the tests** in `LockedMaterialIdentityTests`. Build evidence for a row with `isLocked: false`, `hasReadableSource: true`, `hasRequiredSchema: true`, the row's name, GUID, and digest, and the package arguments that pass the gate as of this task (`hasPackage: false`).

```csharp
[Test]
public void ReleaseMainRowWithMatchingTagAttestsTheLockedOriginal() { ... Is.True ... }

// --- Falsifier 7: a precondition that reuses the identity gate alone ---
// Characterization in this task, because the identity gate still refuses the
// bundled name. It guards Task 5, where the bundled rows start to attest.
[Test]
public void BundledRowRefusesAsALockedOriginal() { ... Is.False ... }

// --- Falsifier 8: a precondition that ignores the recorded original name ---
[Test]
public void ReleaseMainEvidenceWithAnotherAdmittedTagRefuses()
{
    // Tag ".poiyomi/Poiyomi Toon Two Pass", resolved evidence = 9.3.64 main row -> false.
}

[TestCase(null)]
[TestCase("")]
public void MissingOriginalShaderTagRefuses(string tag)
{
    // Resolved evidence = 9.3.64 main row -> false.
}
```

In `AlphaSeparationDryRunTests`, follow the locked name-tag pattern at `:1293-1325` and `:1716-1740`. Its helper hard-codes the 9.3.64 tag. Give it a tag parameter whose default is the old value.

```csharp
// Characterization: the dry run must not promise a recovery the build refuses.
[Test]
public void LockedMaterialTaggedWithTheBundledShaderIsNotDeferred() { ... Is.False ... }
```

- [ ] **Step 2: Run and confirm RED** (compile failure on `AttestsLockedOriginal`).

Run: `group_names` for `Alrauna.Amuse.Tests.Editor.Build.LockedMaterialIdentityTests` and the dry-run test class.

- [ ] **Step 3: Implement**

1. `AttestsLockedOriginal` returns true only when the tag is non-empty and equals `resolved.ShaderName`, `TryVerifyPoiyomiIdentity` passes, and `TryMatchAdmittedSource` finds a row with `LockedOriginalAdmitted` true.
2. `PassesPinnedPoiyomiIdentity` keeps gathering the resolved shader's evidence and calls `AttestsLockedOriginal`. `ResolvesToAttestedPoiyomi(string originalGuid, string originalShaderTag)` passes the tag through. `PreCheckRefusal` and `OriginalShaderAttested` pass `identity.OriginalShader`. The orphan case at `:146-152`, where both tags are absent, keeps returning `None`.
3. `IsAttestedOriginalShader` in the dry run calls `PoiyomiSourceAttestation.IsLockedOriginalAdmittedName(tag)`.
4. Comments cite K8 and give the reason in short sentences: thryeditor 2.74 locks write a suffix tag that reconstruction does not read, and the swap-in requires the recorded name to equal the restored shader name.

- [ ] **Step 4: Run and confirm GREEN**

Run: the two classes, then both assemblies. Expected: all pass. Record the counts.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/LockedMaterialIdentity.cs Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs Packages/com.alrauna.amuse/Tests/Editor/Build/LockedMaterialIdentityTests.cs Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs
git commit -m "fix(build): keep locked originals on the 9.3.64 rows"
```

---

### Task 5: Identity cutover (K2, K3.2, K4)

Design: K2, K3.2, K4, verified facts 11 and 12, test duties 1 to 5.

**Files:**
- Modify: `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:11-61, 96-263`
- Modify: `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:25-34, 121-125, 612-620`
- Modify: `Editor/Semantics/UnityMaterialSemantics.cs:825-843, 1121-1123`
- Modify: `Editor/Build/LockedMaterialIdentity.cs` (package lookup and comment)
- Modify: `Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:122-148, 583-586`
- Test: `Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs`, class `PoiyomiSourceAttestationTests` (`:178-461`)
- Test: `Tests/Editor/Semantics/Poiyomi/PoiyomiMultipassRuleTests.cs:130-168`
- Test: `Tests/Editor/Semantics/ShaderSemanticsArchitectureTests.cs:103-111`
- Test: `Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs:1437-1445`
- Test: `Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs` (routing tests)
- Test: `Tests/Editor/Build/LockedMaterialIdentityTests.cs` (evidence construction)

**Interfaces:**
- Consumes: Task 2 and Task 4 members.
- Produces: `PoiyomiSourceEvidence(string shaderName, bool isLocked, bool hasReadableSource, string assetGuid, string normalizedSourceHash, bool hasRequiredSchema)`. The three package members are gone. `PoiyomiSemanticDiagnosticCode.UnsupportedVersion` is gone.

- [ ] **Step 1: Write the tests**

In `PoiyomiSourceAttestationTests`, beside the existing `Identity_*` tests:

```csharp
[TestCase(0)] // 9.3.64 main (characterization)
[TestCase(1)] // 9.3.64 Two Pass (characterization)
[TestCase(2)] // bundled main (RED)
[TestCase(3)] // bundled Two Pass (RED)
public void Identity_EveryAdmittedRowIsSupported(int rowIndex)
{
    var row = PoiyomiSourceAttestation.AdmittedSources()[rowIndex];
    // Evidence with row.ShaderName, row.AssetGuid, row.NormalizedSourceHash: supported, diagnostic null.
}

// --- Falsifier 5: a digest gate that accepts any admitted digest for an admitted name ---
[Test]
public void Identity_BundledNameWithReleaseDigest_IsModifiedShaderSource()
{
    // Bundled main name, main GUID, 9.3.64 main digest -> ModifiedShaderSource.
}

// Characterization: the reverse pairing refuses the same way.
[Test]
public void Identity_ReleaseNameWithBundledDigest_IsModifiedShaderSource() { ... }

// --- Falsifier 6: a GUID gate that accepts any admitted GUID ---
[Test]
public void Identity_BundledMainNameWithTwoPassGuid_IsMissingSourceEvidence() { ... }
```

In `UnityMaterialSemanticsTests`, beside `Legacy90ShaderName_SelectsUnsupportedFamily`, follow its pattern through `TrySelectAlphaMaterialRequests`:

```csharp
[Test]
public void BundledShaderName_SelectsPoiyomiFamily() { ... CapturedAlphaMaterialFamily.Poiyomi ... }

[Test]
public void BundledTwoPassShaderName_SelectsPoiyomiTwoPassFamily() { ... CapturedAlphaMaterialFamily.PoiyomiTwoPass ... }

// Characterization.
[Test]
public void BundledWorldShaderName_SelectsUnsupportedFamily() { ... }
```

- [ ] **Step 2: Run and confirm RED**

Run: `run_tests` EditMode, `group_names: ["Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiSourceAttestationTests", "Alrauna.Amuse.Tests.Editor.Semantics.UnityMaterialSemanticsTests"]`.
Expected: rows 2 and 3, Falsifier 5, Falsifier 6, and the two bundled routing tests fail. The bundled identity tests fail with `UnsupportedShader`. Record the characterization results.

- [ ] **Step 3: Implement the cutover**

1. `TryVerifyPoiyomiIdentity` follows the gate table in design K2.1. Name gate: `TryClassifyShaderName`. GUID gate: some row with this name has this GUID. Digest gate: `TryMatchAdmittedSource`. The package block is removed.
2. `PoiyomiSourceEvidence` drops `HasPackage`, `PackageName`, and `PackageVersion`. `GatherSourceEvidence` and `LockedMaterialIdentity.PassesPinnedPoiyomiIdentity` stop calling `PackageInfo.FindForAssetPath`. Remove `using` lines that become unused.
3. Remove `PoiyomiPackageName`, `PoiyomiPackageVersion`, `CanonicalNormalizedSourceHash`, and `TwoPassCanonicalNormalizedSourceHash`. The 9.3.64 rows take private digest literals. Remove the aliases at `PoiyomiMaterialSemantics.cs:29`, `:30`, and `:32`. Rewrite the header comment at `:25-27` to name the row set (K1) and the design path. Remove `PoiyomiSemanticDiagnosticCode.UnsupportedVersion`.
4. `ClassifyShaderName` Poiyomi branches call `TryClassifyShaderName`. `AnalyzeBaseMaterial` takes its Two Pass choice from `TryClassifyShaderName`.
5. Rewrite the comments named in design K4.3 so they name the row set and the K5 rule, not the scalar pins or the package gate. The `LockedMaterialIdentity` comment drops "package identity".
6. Tests:
   - The `Evidence` helper drops the package parameters. Its digest parameter defaults to a private `UsePin` sentinel string, the same pattern as `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:250`. The sentinel resolves to the 9.3.64 main row digest. An explicit `null` stays `null`, so `Identity_UnreadableSource_IsMissingSourceEvidence` keeps its input.
   - Delete `Identity_LegacyInstallWithoutPackage_IsSupported`, `Identity_WrongPackageName_IsMissingSourceEvidence`, and `Identity_WrongPackageVersion_IsUnsupportedVersion`. Their behavior is removed (design K2.5).
   - `TwoPassIdentityVerifiesAgainstItsOwnPins` drops the package arguments and takes the Two Pass digest from its row.
   - In `TwoPassPins_AreTheMeasuredValues` (`PoiyomiMultipassRuleTests.cs:157-168`), keep the name and GUID assertions. Delete only the hash assertion, because the Task 2 row-set test pins every digest.
   - Delete the two hash-format assertions at `ShaderSemanticsArchitectureTests.cs:108-111`. Keep the GUID assertions at `:104-107`.
   - The conversion tripwire at `PoiyomiOpaqueConversionTests.cs:1437-1445` keeps its main GUID assertion. Its digest assertion asserts that `AdmittedSources()` holds exactly the four Global Constraints digests. It keeps the message "Re-derive the canonical Opaque tuple before changing this pin." and adds "A new row needs the K5 admission measurement first."
   - `LockedMaterialIdentityTests` evidence construction from Task 4 drops the package arguments.

- [ ] **Step 4: Run and confirm GREEN**

Run: the two classes from Step 2, then both assemblies.
Expected: all pass, including Falsifier 7 from Task 4, which now guards a real bundled attestation. Record the counts.

- [ ] **Step 5: Commit**

```bash
git add <every file changed in this task, by explicit path>
git commit -m "feat(poiyomi): attest Poiyomi by shader file and admit the bundled 9.3 shader"
```

---

### Task 6: Documentation (K7)

Design: K7.

**Files:**
- Modify: `README.md:42`, `Packages/com.alrauna.amuse/README.md:42`
- Modify: `docs/architecture/vision.md:55`
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md` (`[Unreleased]` section)
- Modify: `AGENTS.md` (Project Overview, supported shader sources line)

- [ ] **Step 1: Edit the support statements**

Both READMEs, the vision line, and `AGENTS.md` say: Poiyomi Toon 9.3.64, and the 9.3 shader that Poiyomi 10.x packages bundle, each including Two Pass. A locked material on the bundled shader keeps its original slot. Keep the lilToon text unchanged.

- [ ] **Step 2: Add changelog entries under `[Unreleased]`**

- `### Added`: support for the 9.3 main and Two Pass shaders that Poiyomi 10.x packages bundle, unlocked.
- `### Changed`: Poiyomi attests by shader file (name, GUID, digest), not by package. An old 9.x file with the pinned GUID and other bytes reports a modified source in the conversion detail. The research probe finds the admitted shader file.
- `### Fixed`: a rename-suffixed material animation binding of a proof-relevant property now refuses its renderer. A locked material refuses unless its recorded original shader name equals the resolved shader name. A locked material whose original is the bundled shader refuses.

Simple English. Keep the `[Unreleased]` header as is.

- [ ] **Step 3: Sweep the changed docs**

Search the changed files for an at sign joined to hexadecimal, drive-letter paths, home-directory paths, four-digit ports, and private names. Expected: no hits.

- [ ] **Step 4: Commit**

```bash
git add README.md Packages/com.alrauna.amuse/README.md docs/architecture/vision.md Packages/com.alrauna.amuse/CHANGELOG.md AGENTS.md
git commit -m "docs: record bundled Poiyomi 9.3 support"
```
