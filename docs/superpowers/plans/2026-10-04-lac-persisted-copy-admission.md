# Persisted Replacement Copy Admission Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task inline. Steps use checkbox (`- [ ]`) syntax for tracking. Subagent dispatch is not authorized by default in this repository. Work inline unless the user grants authorization.

**Goal:** Admit NDMF-persisted LAC replacement copies (`_compressed`) and bake outputs (`_baked`) into the alpha evidence capture, so a baked output avatar is analyzable.

**Architecture:** One new admission branch in `GeneratedTextureAttestation.TryIdentifyProducer`, gated on the exact NDMF container type, a pinned output suffix, and the existing LAC version seam. Every consumer then serves the persisted shape: the identity gate resolves the ordinary asset form, and `TryIdentifyRouteTexture` sends the copy to the generated route before the streaming route.

**Tech Stack:** Unity 2022.3.22f1, C# editor assembly `Alrauna.Amuse.Editor`, NUnit through the Unity Test Framework, EditMode only.

**Spec:** `docs/superpowers/specs/2026-10-04-lac-persisted-copy-admission-design.md`

## Global Constraints

- Branch: `fix/replacement-copy-reanalysis-admission`, base `main`.
- Tests run in the dev editor instance only. Before the first Unity call of a session, verify `Application.dataPath` ends at this repository's `Assets` folder, exactly. A mismatch stops the work.
- A filtered test run that reports 0 tests is a failure.
- RED before GREEN. Record the failing test names and failure messages before any production edit. An admission test that passes on first run is a characterization, not a RED.
- Tests never install LAC. The version provider is substituted through `ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull`.
- One production assembly. Test classes mirror production types. Constraint model assertions only: `Assert.That(actual, Is.EqualTo(expected))`.
- No private identifiers, no machine paths, no instance names in any file.
- Do not weaken or delete any existing test.
- Compilation happens inside Unity. Refresh assets after new files, then run.

---

### Task 1: RED batch — tests for falsifiers 8 through 14

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/GeneratedTextureAttestationTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs`

**Interfaces:**
- Consumes: `GeneratedTextureAttestation.TryIdentifyProducer(Texture, out GeneratedTextureProducer)`, `ReplacementTextureAttestation.TryIdentifyReplacement(Texture, out Texture2D)`, `ReplacementTextureAttestation.ResetForTests()`, `ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull`, `UnityTextureEvidence.TryGetSourceId(Texture, out TextureSourceId)`, `UnityTextureEvidence.TryGetColorInterpretation(Texture, out TextureColorInterpretation)`, `UnityAlphaFieldEvidence.TryCapture(Texture, out TextureSourceId, out AlphaMipChain)`.
- Produces: test method names below. Task 2 makes exactly the RED set pass and keeps every guard passing.

- [ ] **Step 1: Add the version helper and TearDown reset to `GeneratedTextureAttestationTests`**

Add to the fixture body:

```csharp
private static void InstallAdmittedCompressorVersion()
{
    ReplacementTextureAttestation.ResetForTests();
    ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
        _ => "0.9.0";
}
```

Extend the existing `TearDown` with one first line:

```csharp
ReplacementTextureAttestation.ResetForTests();
```

- [ ] **Step 2: Add the producer falsifier tests to `GeneratedTextureAttestationTests`**

Falsifier 8, refusal arms (guards, must pass before and after):

```csharp
[Test]
public void PersistedCopyWithUnadmittedProducerVersion_IsRefused()
{
    var copy = new Texture2D(8, 8, TextureFormat.RGBA32, true);
    copy.name = "MainTex_compressed";
    AssetDatabase.AddObjectToAsset(copy, TestContainerPath);
    AssetDatabase.SaveAssets();
    ReplacementTextureAttestation.ResetForTests();
    ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
        _ => "0.9.0";
    ReplacementTextureAttestation.SetAdmittedVersionsForTests();

    var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
        copy, out var producer);

    Assert.That(isCharacterized, Is.False);
    Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.None));
}

[Test]
public void PersistedCopyWithUninstalledProducer_IsRefused()
{
    var copy = new Texture2D(8, 8, TextureFormat.RGBA32, true);
    copy.name = "MainTex_compressed";
    AssetDatabase.AddObjectToAsset(copy, TestContainerPath);
    AssetDatabase.SaveAssets();
    ReplacementTextureAttestation.ResetForTests();

    var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
        copy, out var producer);

    Assert.That(isCharacterized, Is.False);
    Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.None));
}
```

Falsifier 8 and 10, admission arms (RED today):

```csharp
[Test]
public void PersistedCompressedCopyInNdmfContainer_IsIdentifiedAsCharacterizedProducer()
{
    InstallAdmittedCompressorVersion();
    var copy = new Texture2D(8, 8, TextureFormat.RGBA32, true);
    copy.name = "MainTex_compressed";
    AssetDatabase.AddObjectToAsset(copy, TestContainerPath);
    AssetDatabase.SaveAssets();

    var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
        copy, out var producer);

    Assert.That(isCharacterized, Is.True);
    Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.LimitexTextureCompressor));
}

[Test]
public void PersistedBakedCopyInNdmfContainer_IsIdentifiedAsCharacterizedProducer()
{
    InstallAdmittedCompressorVersion();
    var baked = new Texture2D(8, 8, TextureFormat.RGBA32, true);
    baked.name = "MainTex_baked";
    AssetDatabase.AddObjectToAsset(baked, TestContainerPath);
    AssetDatabase.SaveAssets();

    var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
        baked, out var producer);

    Assert.That(isCharacterized, Is.True);
    Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.LimitexTextureCompressor));
}
```

Falsifier 9 (guard):

```csharp
[Test]
public void PersistedCompressedCopyInDerivedContainer_IsRefused()
{
    InstallAdmittedCompressorVersion();
    var derived = ScriptableObject.CreateInstance<ForeignNamespace.DerivedSubAssetContainer>();
    AssetDatabase.CreateAsset(derived, ArbitraryContainerPath);
    var copy = new Texture2D(8, 8, TextureFormat.RGBA32, true);
    copy.name = "MainTex_compressed";
    AssetDatabase.AddObjectToAsset(copy, ArbitraryContainerPath);
    AssetDatabase.SaveAssets();

    var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
        copy, out var producer);

    Assert.That(isCharacterized, Is.False);
    Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.None));
}
```

Falsifier 11 (guard):

```csharp
[Test]
public void InBuildBakedOutputWithoutRegistration_StaysRefused()
{
    InstallAdmittedCompressorVersion();
    var baked = new Texture2D(8, 8, TextureFormat.RGBA32, false);
    baked.name = "MainTex_baked";

    var isReplacement = ReplacementTextureAttestation.TryIdentifyReplacement(
        baked, out var source);

    Assert.That(isReplacement, Is.False);
    Assert.That(source, Is.Null);
}
```

- [ ] **Step 3: Add the identity and color tests to `UnityTextureEvidenceTests`**

Falsifier 12 (RED today). Add a fixture constant `private const string TestContainerPath = "Assets/AmuseTests_PersistedIdentity.asset";`, a container field, creation in `SetUp` exactly like `GeneratedTextureAttestationTests`, and `AssetDatabase.DeleteAsset(TestContainerPath)` in `TearDown`:

```csharp
[Test]
public void PersistedCompressedCopyResolvesOrdinaryAssetIdentityForm()
{
    ReplacementTextureAttestation.ResetForTests();
    ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
        _ => "0.9.0";
    var copy = new Texture2D(8, 8, TextureFormat.RGBA32, true);
    copy.name = "MainTex_compressed";
    AssetDatabase.AddObjectToAsset(copy, TestContainerPath);
    AssetDatabase.SaveAssets();

    var resolved = UnityTextureEvidence.TryGetSourceId(copy, out var sourceId);

    Assert.That(resolved, Is.True);
    Assert.That(sourceId.Value, Does.StartWith("unity-asset:"));
    Assert.That(sourceId.Value.Contains("unity-replacement"), Is.False);
}
```

Color proof (RED today):

```csharp
[Test]
public void PersistedCompressedCopyColorInterpretation_ReadsTheGraphicsFormat()
{
    ReplacementTextureAttestation.ResetForTests();
    ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
        _ => "0.9.0";
    var copy = new Texture2D(8, 8, TextureFormat.RGBA32, true, true);
    copy.name = "MainTex_compressed";
    EditorUtility.CompressTexture(copy, TextureFormat.DXT5);
    AssetDatabase.AddObjectToAsset(copy, TestContainerPath);
    AssetDatabase.SaveAssets();

    var proven = UnityTextureEvidence.TryGetColorInterpretation(
        copy, out var interpretation);

    Assert.That(proven, Is.True);
    Assert.That(interpretation, Is.EqualTo(TextureColorInterpretation.Linear));
}
```

- [ ] **Step 4: Add the route capture test to `UnityAlphaFieldEvidenceTests`**

Falsifier 14 (RED today). Add a fixture constant `private const string CaptureContainerPath = "Assets/AmuseTests_PersistedCapture.asset";` and `AssetDatabase.DeleteAsset(CaptureContainerPath)` in `TearDown` guarded by a `LoadAssetAtPath` null check, then this test:

```csharp
[Test]
public void PersistedStreamingCompressedCopy_CapturesThroughGeneratedRoute()
{
    ReplacementTextureAttestation.ResetForTests();
    ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
        _ => "0.9.0";
    var container = ScriptableObject.CreateInstance<nadena.dev.ndmf.runtime.SubAssetContainer>();
    AssetDatabase.CreateAsset(container, CaptureContainerPath);
    var copy = new Texture2D(64, 64, TextureFormat.RGBA32, true, true);
    var colors = new Color32[64 * 64];
    for (var i = 0; i < colors.Length; i++)
    {
        colors[i] = new Color32(255, 255, 255, 255);
    }
    copy.SetPixels32(colors);
    copy.Apply(false, false);
    EditorUtility.CompressTexture(copy, TextureFormat.DXT5);
    copy.name = "BodyTex_compressed";
    AssetDatabase.AddObjectToAsset(copy, CaptureContainerPath);
    AssetDatabase.SaveAssets();
    var serialized = new SerializedObject(copy);
    serialized.FindProperty("m_StreamingMipmaps").boolValue = true;
    serialized.ApplyModifiedPropertiesWithoutUndo();
    copy.requestedMipmapLevel = 0;

    var captured = UnityAlphaFieldEvidence.TryCapture(
        copy, out _, out var chain);
    var levelZeroHasEvidence = captured && chain != null &&
        !chain.IsLevelWithoutEvidence(0);
    var levelCount = chain?.Count ?? 0;
    AssetDatabase.DeleteAsset(CaptureContainerPath);

    Assert.That(captured, Is.True);
    Assert.That(levelZeroHasEvidence, Is.True);
    Assert.That(levelCount, Is.EqualTo(copy.mipmapCount));
}
```

- [ ] **Step 5: Observe the RED run**

Refresh assets in the dev editor instance after the identity check. Run these EditMode classes:

- `Alrauna.Amuse.Tests.Editor.Semantics.GeneratedTextureAttestationTests`
- `Alrauna.Amuse.Tests.Editor.Semantics.UnityTextureEvidenceTests`
- `Alrauna.Amuse.Tests.Editor.Host.UnityAlphaFieldEvidenceTests`

Expected, and record the observed counts:

- FAIL: `PersistedCompressedCopyInNdmfContainer_IsIdentifiedAsCharacterizedProducer`, `PersistedBakedCopyInNdmfContainer_IsIdentifiedAsCharacterizedProducer`, `PersistedCompressedCopyResolvesOrdinaryAssetIdentityForm`, `PersistedCompressedCopyColorInterpretation_ReadsTheGraphicsFormat`, `PersistedStreamingCompressedCopy_CapturesThroughGeneratedRoute`.
- PASS: `PersistedCopyWithUnadmittedProducerVersion_IsRefused`, `PersistedCopyWithUninstalledProducer_IsRefused`, `PersistedCompressedCopyInDerivedContainer_IsRefused`, `InBuildBakedOutputWithoutRegistration_StaysRefused`.
- Every other test in the three classes keeps its current outcome.

If an admission-arm test fails with `NonResidentMips` or an unexpected message, stop. Report the message and the counts. Do not change the test to make it pass.

- [ ] **Step 6: Commit nothing yet**

The RED batch stays uncommitted. Task 2 commits the pair together, so no commit in the branch history has a red suite.

### Task 2: GREEN — the producer admission branch

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/GeneratedTextureAttestation.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/ReplacementTextureAttestation.cs`

**Interfaces:**
- Consumes: `ReplacementTextureAttestation.ReplacementNameSuffix`, `TryReadInstalledProducerVersion(out string)`, `IsVersionAdmitted(string)`.
- Produces: `ReplacementTextureAttestation.BakedOutputSuffix` (`"_baked"`); `TryIdentifyProducer` returns `LimitexTextureCompressor` for the persisted shape. No signature changes.

- [ ] **Step 1: Add the bake suffix constant**

In `ReplacementTextureAttestation`, next to `ReplacementNameSuffix`:

```csharp
internal const string BakedOutputSuffix = "_baked";
```

- [ ] **Step 2: Add the persisted branch to `TryIdentifyProducer`**

Replace the `SubAssetContainer` branch body so the LAC branch follows the AAO branch, and the AAO behavior is untouched:

```csharp
if (mainAsset.GetType() == typeof(nadena.dev.ndmf.runtime.SubAssetContainer))
{
    var textureName = texture.name ?? string.Empty;
    var isAaoTexture = textureName.EndsWith(" (AAO UV Packed)") ||
                       textureName.StartsWith("AAO Monotone ");
    if (isAaoTexture)
    {
        producer = GeneratedTextureProducer.Anatawa12AvatarOptimizer;
        return true;
    }

    // Persisted replacement copies (spec 2026-10-04, section 6). NDMF
    // persists every in-memory copy of the attested compressor as a
    // sub-asset of one of these containers after every plugin phase.
    // The copy keeps the source name plus a pinned output suffix, so
    // the suffix plus the admitted producer version characterizes the
    // shape. Bake outputs named "_baked" persist the same way.
    var isPersistedReplacement =
        textureName.EndsWith(
            ReplacementTextureAttestation.ReplacementNameSuffix,
            StringComparison.Ordinal) ||
        textureName.EndsWith(
            ReplacementTextureAttestation.BakedOutputSuffix,
            StringComparison.Ordinal);
    if (isPersistedReplacement &&
        ReplacementTextureAttestation.TryReadInstalledProducerVersion(
            out var version) &&
        ReplacementTextureAttestation.IsVersionAdmitted(version))
    {
        producer = GeneratedTextureProducer.LimitexTextureCompressor;
        return true;
    }

    return false;
}
```

Add `using System;` if the file does not already import it for `StringComparison`.

- [ ] **Step 3: Correct the false upstream premise in the `ReplacementTextureAttestation` comment**

Replace the class-level comment that starts `// Upstream path (spec 2026-10-03, section 12).` with:

```csharp
// Upstream path (spec 2026-10-03, section 12; premise corrected
// 2026-10-04). Persistence of each copy as a container sub-asset would
// retire the unity-replacement identity form and the duplicate guard.
// It would not retire producer admission: the container basis checks a
// producer name marker, and a "_compressed" copy carries no admitted
// marker. A name marker plus a version admission remains either way
// (spec 2026-10-04). The same release registering bake pairs would let
// AMUSE admit the in-build "_baked" output and retire its refusal.
```

Keep the rest of the comment's paragraph intact where it states the version and registry contracts.

- [ ] **Step 4: Observe the GREEN run**

Refresh, then run the same three EditMode classes. Expected, and record the observed counts:

- Every Task 1 test passes, including the five RED arms.
- Every guard still passes.
- No other test in the three classes changed outcome.

- [ ] **Step 5: Run both test assemblies**

Run the full EditMode suites of `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor`. Record the observed totals and failures. Zero failures expected. A new failure in an unrelated class is a stop condition: report it, do not fix it inside this change.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/GeneratedTextureAttestation.cs \
        Packages/com.alrauna.amuse/Editor/Semantics/ReplacementTextureAttestation.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Semantics/GeneratedTextureAttestationTests.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs
git commit -m "feat: admit persisted LAC replacement copies into the capture"
```

### Task 3: Characterization gate, hint, and closure

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs` (one sentence)

**Interfaces:**
- Consumes: the Task 2 branch. Produces: the recorded characterization counts and the updated report hint.

- [ ] **Step 1: Run the characterization gate on the dev editor instance**

Spec section 8 makes this gate binding. Build a persisted shape by hand in a scratch folder under `Assets/` of the dev editor instance: a DXT5 mip-mapped texture named with the `_compressed` suffix, added as a sub-asset of a real `nadena.dev.ndmf.runtime.SubAssetContainer` asset, streaming flag set, then released and reloaded from disk before capture. Capture with `UnityAlphaFieldEvidence.TryCapture` under the inert bounds. Record: requested level reads loaded or not, levels with evidence, refusal reason if any.

Gate outcome:
- Pass: the requested level reads loaded and at least that level carries evidence. The admission ships.
- Fail: the capture refuses or no level carries evidence. The admission does not ship. Stop, report the counts, and revert Task 2's branch edit on this branch, keeping the tests as documented refusals. Do not weaken any test.

Delete the scratch folder afterwards. The repo test suite must not depend on the scratch.

- [ ] **Step 2: Extend the report hint by one sentence**

In `AmuseReportStrings.cs`, find the `UnavailableCapture` hint near the text naming the texture-atlas setting and the LAC Texture Compressor. Append one sentence after the existing sentence about the same shape:

```csharp
"The build saver persists these copies, and a persisted copy reads " +
"only under the same version rule. "
```

- [ ] **Step 3: Re-run the affected suites and the whitespace check**

Run `GeneratedTextureAttestationTests`, `UnityTextureEvidenceTests`, `UnityAlphaFieldEvidenceTests`, and any test class that pins report strings, if one exists. Record the counts. Then:

```bash
git diff --check
```

- [ ] **Step 4: Commit and close**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs
git commit -m "feat: name the persisted copy rule in the capture refusal hint"
```

## Stop conditions

- The characterization gate fails: revert the admission branch per Task 3 Step 1, keep the refusal tests, report.
- The Task 1 RED run shows an admission-arm failure other than refusal at the producer, identity, or route: stop and report before any production edit.
- A full-suite failure outside the three touched classes: stop and report.
- Any scope discovery that changes the admitted basis: stop and report. The basis is a correctness-contract decision owned by the spec.
