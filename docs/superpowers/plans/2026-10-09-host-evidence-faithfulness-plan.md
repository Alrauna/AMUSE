# Host Evidence Faithfulness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refuse the disk decode route for imports whose alpha Unity regenerates, pin the red channel capture to inert bounds with a field contract assertion, align the masked streaming chain's coarse levels with effective content, fingerprint the generated texture session cache, and narrow the disk reader's blanket catch.

**Architecture:** All changes stay inside `Editor/Host/` of `Packages/com.alrauna.amuse`. No change touches `Editor/Analysis/`. Every refused capture keeps the clone route as its fallback.

**Tech Stack:** C# (Unity 2022.3), Unity Test Framework (EditMode).

**Spec:** `docs/superpowers/specs/2026-10-09-host-evidence-faithfulness-design.md`

## Global Constraints

- Use simple English. Short active sentences. One idea per sentence.
- No semicolons in prose.
- No contractions.
- Never record absolute or machine-specific paths. Use repository-relative paths only.
- Never record host names, user account names, home-directory paths, ports, or Unity MCP instance names.
- Date every status claim. As of 2026-10-09.
- Never stage, commit, push, or modify Git history without explicit user authorization.
- The dev editor instance with `Application.dataPath == <repo-root>/Assets` is the only authorized test instance.
- Run every test step in Unity Test Runner, EditMode. A filtered run that reports 0 tests is a failure.
- This pair runs third in the cross pair order. It shares no production file with another pair.

---

### Task 1: Refuse the Disk Route for a Generated Alpha Channel

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/SourceImageAlphaReaderTests.cs`

**Interfaces:**
- Consumes: `SourceImageAlphaReader.TryReadSourceAlphaChain(Texture2D, TextureChannel, float, AlphaPolicyBounds, out AlphaMipChain)`
- Produces: `false` for an alpha channel read of a `TextureImporterAlphaSource.FromGrayScale` import, so `UnityStreamingTextureEvidence.TryCapture` falls through to the clone route

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Host/SourceImageAlphaReaderTests.cs`, add two tests.

```csharp
[Test]
public void TryReadSourceAlphaChain_RefusesGrayScaleGeneratedAlphaForTheAlphaChannel()
{
    // Mid gray RGB with opaque alpha: the import generates an alpha
    // near 0.5 from the luminance, so the file's opaque alpha bytes
    // are not the imported representation.
    var pixels = new Color32[16];
    for (var i = 0; i < pixels.Length; i++)
    {
        pixels[i] = new Color32(128, 128, 128, 255);
    }
    var imported = TestTextureImport.WritePng(
        Path.Combine(TestFolder, "gray_scale_alpha.png"), 4, 4, pixels,
        importer => importer.alphaSource =
            TextureImporterAlphaSource.FromGrayScale);

    Assert.That(
        SourceImageAlphaReader.TryReadSourceAlphaChain(
            imported, TextureChannel.Alpha, 1.0f,
            AlphaPolicyBounds.Inert, out var chain),
        Is.False,
        "The route cannot reproduce a generated alpha channel.");
    Assert.That(chain, Is.Null);
}

[Test]
public void TryReadSourceAlphaChain_KeepsServingTheRedChannelOfGrayScaleImports()
{
    // Characterization, not RED: grayscale generation rewrites only
    // the alpha channel, so the red read stays faithful and admitted.
    var pixels = new Color32[16];
    for (var i = 0; i < pixels.Length; i++)
    {
        pixels[i] = new Color32(128, 128, 128, 255);
    }
    var imported = TestTextureImport.WritePng(
        Path.Combine(TestFolder, "gray_scale_red.png"), 4, 4, pixels,
        importer => importer.alphaSource =
            TextureImporterAlphaSource.FromGrayScale);

    Assert.That(
        SourceImageAlphaReader.TryReadSourceAlphaChain(
            imported, TextureChannel.Red, 1.0f,
            AlphaPolicyBounds.Inert, out var chain),
        Is.True);
    var mip0 = chain[0];
    for (var x = 0; x < 4; x++)
    {
        Assert.That(mip0.GetAlpha(x, 0), Is.EqualTo(0),
            "Red 128 is below exactly one at inert bounds.");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the fixture `Alrauna.Amuse.Tests.Editor.Host.SourceImageAlphaReaderTests` in Unity Test Runner, EditMode.
Expect `TryReadSourceAlphaChain_RefusesGrayScaleGeneratedAlphaForTheAlphaChannel` to fail, because the route returns true and serves the file's opaque alpha today.
Expect `TryReadSourceAlphaChain_KeepsServingTheRedChannelOfGrayScaleImports` to pass. It is a characterization guard for the channel scope.
The pinned test `AlphaSourceFromGrayScale_ReportsTheGeneratedAlphaNotTheInputAlpha` at `Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs:843-849` proves the field route reports the generated alpha. This task makes the disk route agree by refusing. The pinned test stays unchanged.

- [ ] **Step 3: Implement the refusal**

In `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs`:

Read the importer once, directly after the extension gate. The extension gate sits at lines 48 to 52. Insert the read before the `try` block that starts at line 70, and delete the duplicate importer read at line 71 inside the try.

```csharp
var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
if (importer != null &&
    channel == TextureChannel.Alpha &&
    importer.alphaSource == TextureImporterAlphaSource.FromGrayScale)
{
    // Unity generates the alpha channel from RGB grayscale at
    // import. The raw file bytes carry the authoring alpha, so this
    // route cannot reproduce the imported representation. The clone
    // route reproduces the import and stays the fallback.
    return false;
}
```

Delete the now duplicate line `var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;` inside the `try` at line 71. The `isAlphaNone` line at line 72 keeps using the outer local.

Update the class doc comment at lines 12-19 to add one sentence: an alpha channel read of a FromGrayScale import refuses, because the import generates that channel from RGB grayscale.

- [ ] **Step 4: Run tests to verify they pass**

Run the fixture again in Unity Test Runner, EditMode. Verify both new tests and every existing test in the fixture pass.

---

### Task 2: Pin the Red Capture to Inert Bounds and Assert the Field Contract

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/AlphaFieldSet.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/AlphaFieldSetTests.cs`

**Interfaces:**
- Consumes: `UnityAlphaFieldEvidence.TryCapture(Texture, TextureChannel, float, AlphaPolicyBounds, out TextureSourceId, out AlphaMipChain, out TextureCaptureRefusal, Material)`
- Produces: `CaptureTexture` captures red under `AlphaPolicyBounds.Inert`, asserts `AssertRedFieldContract` on every captured red chain, and `AlphaFieldKey.ForRed` keys the inert bounds

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`, add a local shader constant beside the existing ones and three tests.

```csharp
private const string LilToonCutoutConversionShader =
    "Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest";

[Test]
public void RedFieldUnderAnActivePolicyMatchesTheInertRedField()
{
    var pixels = new Color32[16];
    for (var i = 0; i < pixels.Length; i++)
    {
        pixels[i] = new Color32(217, 0, 0, 255);
    }
    var mask = TestTextureImport.WritePng(
        Path.Combine(TempFolder, "red_seam_mask.png"), 4, 4, pixels);
    var material = NewMaterial(LilToonCutoutConversionShader);
    _materials.Add(material);
    material.SetTexture("_AlphaMask", mask);

    var active = UnityMaterialEvidenceCapture.Capture(
        new[]
        {
            new MaterialEvidenceCaptureInput(
                material, LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
        },
        AlphaPolicyBounds.From(80, 2))[0];
    var inert = UnityMaterialEvidenceCapture.Capture(
        new[]
        {
            new MaterialEvidenceCaptureInput(
                material, LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
        },
        AlphaPolicyBounds.Inert)[0];

    Assert.That(active.TryGetTexture("_AlphaMask", out var activeBound),
        Is.True);
    Assert.That(inert.TryGetTexture("_AlphaMask", out var inertBound),
        Is.True);
    var activeRed = activeBound.Texture.RedChannel;
    var inertRed = inertBound.Texture.RedChannel;
    Assert.That(activeRed, Is.Not.Null);
    Assert.That(inertRed, Is.Not.Null);
    for (var mip = 0; mip < activeRed.Count; mip++)
    {
        Assert.That(activeRed.IsLevelWithoutEvidence(mip),
            Is.EqualTo(inertRed.IsLevelWithoutEvidence(mip)));
        if (activeRed.IsLevelWithoutEvidence(mip))
        {
            continue;
        }
        for (var y = 0; y < activeRed[mip].Height; y++)
        {
            for (var x = 0; x < activeRed[mip].Width; x++)
            {
                Assert.That(activeRed[mip].GetAlpha(x, y),
                    Is.EqualTo(inertRed[mip].GetAlpha(x, y)),
                    $"mip {mip} texel ({x}, {y})");
            }
        }
    }
    Assert.That(activeRed[0].GetAlpha(0, 0), Is.EqualTo(0),
        "Red 217 is below exactly one under the inert contract.");
}

[Test]
public void RedFieldContractAssertion_RefusesTheErasedFlagByte()
{
    var level = new AlphaTextureData(
        1, 1, new byte[] { AlphaTextureData.ErasedFlag });
    var chain = new AlphaMipChain(new[] { level });

    Assert.Throws<InvalidOperationException>(
        () => UnityMaterialEvidenceCapture.AssertRedFieldContract(chain));
}

[Test]
public void RedFieldContractAssertion_AcceptsBinaryVerdicts()
{
    // Characterization, not RED: binary verdicts satisfy the contract.
    var level = new AlphaTextureData(2, 1, new byte[] { 255, 0 });
    var chain = new AlphaMipChain(new[] { level });

    Assert.DoesNotThrow(
        () => UnityMaterialEvidenceCapture.AssertRedFieldContract(chain));
}
```

In `Packages/com.alrauna.amuse/Tests/Editor/Host/AlphaFieldSetTests.cs`, add one test.

```csharp
[Test]
public void ForRed_KeysTheInertBounds()
{
    var evidence = new CapturedTextureEvidence(
        true, new TextureSourceId(), 1f, AlphaPolicyBounds.From(80, 2),
        false, default, false, default, false, false,
        false, null, TextureCaptureRefusalReason.None,
        true, null, TextureCaptureRefusalReason.None);

    Assert.That(
        AlphaFieldKey.ForRed(evidence).Bounds,
        Is.EqualTo(AlphaPolicyBounds.Inert));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the fixtures `Alrauna.Amuse.Tests.Editor.Host.UnityMaterialEvidenceCaptureTests` and `Alrauna.Amuse.Tests.Editor.Host.AlphaFieldSetTests` in Unity Test Runner, EditMode.
Expect `RedFieldUnderAnActivePolicyMatchesTheInertRedField` to fail, because the active capture stores 255 for red 217 under the bound 204 while the inert capture stores 0.
Expect `RedFieldContractAssertion_RefusesTheErasedFlagByte` to fail, because `AssertRedFieldContract` does not exist yet.
Expect `ForRed_KeysTheInertBounds` to fail, because `ForRed` carries `texture.CaptureBounds` today.
Expect the two characterization tests to pass only after Step 3 adds the helper.

- [ ] **Step 3: Implement the pin, the assertion, and the key**

In `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs`:

Replace the red capture call at lines 1386-1398 and its comment above it. Keep the alpha call unchanged.

```csharp
// The alpha channel reads under the active policy. The red channel
// reads under the inert bounds: the Analysis mapped arm evaluates a
// red field sample at exactly 1, so byte 255 must mark exactly the
// texels whose value is exactly 1. An active policy verdict on red
// would store 255 for a sub-one texel and break the mapped contract.
```

```csharp
var hasRedChannel =
    (evidence & TextureEvidenceKinds.RedChannel) != 0 &&
    UnityAlphaFieldEvidence.TryCapture(
        texture,
        TextureChannel.Red,
        1.0f,
        AlphaPolicyBounds.Inert,
        out _,
        out redChannel,
        out redRefusal,
        originMaterial: originMaterial);
if (hasRedChannel)
{
    AssertRedFieldContract(redChannel);
}
```

Add the internal static helper beside `CaptureTexture`.

```csharp
/// <summary>
/// Asserts the Analysis field contract on a red chain at the capture
/// seam: byte 255 marks exactly the texels whose value is exactly 1
/// and every other byte marks a value strictly below 1, so an inert
/// red chain carries only 255 and 0. The inert bounds never erase,
/// so the erased flag byte proves a capture seam defect. A violation
/// throws instead of serving evidence the proof core would misread.
/// </summary>
internal static void AssertRedFieldContract(AlphaMipChain chain)
{
    if (chain == null)
    {
        throw new ArgumentNullException(nameof(chain));
    }
    for (var mip = 0; mip < chain.Count; mip++)
    {
        if (chain.IsLevelWithoutEvidence(mip))
        {
            continue;
        }
        var level = chain[mip];
        for (var y = 0; y < level.Height; y++)
        {
            for (var x = 0; x < level.Width; x++)
            {
                var verdict = level.GetAlpha(x, y);
                if (verdict == byte.MaxValue || verdict == 0)
                {
                    continue;
                }
                throw new InvalidOperationException(
                    "Red evidence breaks the exact-255 field contract: " +
                    "level " + mip + " texel (" + x + ", " + y + ") stores " +
                    verdict + ", which is neither exactly one nor below one.");
            }
        }
    }
}
```

Update the `CapturedTextureEvidence.CaptureBounds` doc comment at lines 348-357. Replace the sentence about both arms keying the raw bounds with: the alpha arm keys these bounds, and the red arm keys the inert bounds because the capture pins red to them.

In `Packages/com.alrauna.amuse/Editor/Host/AlphaFieldSet.cs`:

Replace the `ForRed` body at lines 66-74.

```csharp
/// <summary>
/// The red arm of one captured texture assignment. Masks are always
/// captured exact - a texel between a noise gate and a shader
/// cutoff never applies to the mask product - so the arm's
/// threshold is fixed at one, and the capture pins the arm to the
/// inert bounds, which the key carries.
/// </summary>
internal static AlphaFieldKey ForRed(
    CapturedTextureEvidence texture)
{
    return new AlphaFieldKey(
        texture.SourceIdentity,
        TextureChannel.Red,
        1f,
        AlphaPolicyBounds.Inert);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run both fixtures in Unity Test Runner, EditMode. Verify the four new tests and every existing test in both fixtures pass.

---

### Task 3: Align the Masked Chain's Coarse Levels With Effective Content

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/SourceImageMaskedChain.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs` (comments only)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/SourceImageMaskedChainTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/SourceImageAlphaReaderTests.cs`

**Interfaces:**
- Consumes: `SourceImageMaskedChain.Build(IReadOnlyList<byte>, int, int, int, AlphaPolicyBounds)`
- Produces: coarse levels average every texel, exactly as the imported mip chain does, and publish the policy verdict of that plain average

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Analysis/SourceImageMaskedChainTests.cs`, replace the test `SingleStraySubstitutesAtEveryLevel` at lines 46-74. The old test bakes the optimistic substitution this task removes. Updating its expectation is mandated, not a test weakening.

```csharp
[Test]
public void SingleStrayDragsEveryCoarseLevelBelowTheOpaqueBound()
{
    // 8x8, all 255 except one texel at 3. n = 6 erases the stray at
    // level 0, and the plain average drags every block that contains
    // it below the opaque bound. This matches the imported mip chain
    // the clone route reads.
    var bytes = Uniform(64, 255);
    bytes[0] = 3;
    var bounds = AlphaPolicyBounds.From(100, 2);
    var levels = SourceImageMaskedChain.Build(
        bytes, 8, 8, 4, bounds);
    Assert.That(levels.Length, Is.EqualTo(4));
    Assert.That(levels[0].GetAlpha(0, 0),
        Is.EqualTo(AlphaTextureData.ErasedFlag));
    Assert.That(levels[0].GetAlpha(1, 0), Is.EqualTo(255));
    for (var level = 1; level < levels.Length; level++)
    {
        Assert.That(levels[level].IsFullyOpaque, Is.False,
            $"level {level}");
        Assert.That(levels[level].GetAlpha(0, 0), Is.EqualTo(0),
            $"level {level}");
    }
}
```

In `Packages/com.alrauna.amuse/Tests/Editor/Host/SourceImageAlphaReaderTests.cs`, add the route level test.

```csharp
[Test]
public void MaskedRouteCoarseLevelsAverageEveryTexelUnderActivePolicy()
{
    // 4x4 with mipmaps, one zero texel. Under an active policy with
    // no shader cutoff the reader takes the masked route. Level 1
    // must publish the plain average of the imported chain, so the
    // block over the stray reads 0, not the masked 255.
    var pixels = new Color32[16];
    for (var i = 0; i < pixels.Length; i++)
    {
        pixels[i] = new Color32(255, 255, 255, 255);
    }
    pixels[0] = new Color32(255, 255, 255, 0);
    var pngPath = Path.Combine(TestFolder, "masked_coarse.png");
    var imported = TestTextureImport.WritePng(pngPath, 4, 4, pixels,
        importer => importer.mipmapEnabled = true);

    Assert.That(
        SourceImageAlphaReader.TryReadSourceAlphaChain(
            imported, TextureChannel.Alpha, 1.0f,
            AlphaPolicyBounds.From(100, 2), out var chain),
        Is.True);

    var level0 = chain[0];
    Assert.That(level0.GetAlpha(0, 0),
        Is.EqualTo(AlphaTextureData.ErasedFlag));
    var level1 = chain[1];
    Assert.That(level1.Width, Is.EqualTo(2));
    Assert.That(level1.GetAlpha(0, 0), Is.EqualTo(0),
        "The block over the stray averages all four texels.");
    Assert.That(level1.GetAlpha(1, 1), Is.EqualTo(255),
        "The block without the stray stays opaque.");
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the fixtures `Alrauna.Amuse.Tests.Editor.Analysis.SourceImageMaskedChainTests` and `Alrauna.Amuse.Tests.Editor.Host.SourceImageAlphaReaderTests` in Unity Test Runner, EditMode.
Expect `SingleStrayDragsEveryCoarseLevelBelowTheOpaqueBound` to fail, because the masked route substitutes and keeps every coarse level fully opaque today.
Expect `MaskedRouteCoarseLevelsAverageEveryTexelUnderActivePolicy` to fail, because the masked route reports 255 for the level 1 block over the stray today.

- [ ] **Step 3: Implement the aligned averaging**

In `Packages/com.alrauna.amuse/Editor/Host/SourceImageMaskedChain.cs`:

Rewrite the class comment at lines 7-14.

```csharp
/// <summary>
/// Builds the source-image route's alpha chain under an alpha policy.
/// The builder averages every texel of a block. That plain average is
/// what the imported mip chain stores, so both capture routes resolve
/// each level against the same effective content. The builder
/// publishes the policy verdict of the average: a block at or above
/// the opaque bound stores 255, a block below the noise bound carries
/// the erased flag, and the rest store 0. All comparisons are exact
/// integers. No float crosses a verdict.
/// </summary>
```

Replace the body of `BuildBlock` at lines 79-104.

```csharp
private static byte BuildBlock(
    IReadOnlyList<byte> source,
    int sourceWidth,
    int startX,
    int endX,
    int startY,
    int endY,
    AlphaPolicyBounds bounds)
{
    long sum = 0;
    for (var y = startY; y < endY; y++)
    {
        for (var x = startX; x < endX; x++)
        {
            sum += source[y * sourceWidth + x];
        }
    }
    var count = (long)(endX - startX) * (endY - startY);
    if (bounds.NoiseBound > 0 && sum < (long)bounds.NoiseBound * count)
    {
        return AlphaTextureData.ErasedFlag;
    }
    // Exact integer verdict: the plain average meets the opaque
    // bound exactly when sum >= bound * count.
    return sum >= (long)bounds.OpaqueBound * count
        ? byte.MaxValue
        : (byte)0;
}
```

In `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs`:

Update the class comment at lines 11-19 and the masked route comment inside `TryReadSourceAlphaChain` at lines 117-119. State that the masked builder derives every level from the plain box averages, so the coarse levels describe the same effective content the imported chain stores.

- [ ] **Step 4: Run tests to verify they pass**

Run both fixtures in Unity Test Runner, EditMode. Verify the two new or updated tests pass.
Verify the pinned tests that survive unchanged still pass, because level 0 blocks are one texel and the no-noise blocks compute identical sums: `InertBoundsReproduceThresholdAt255`, `DenseNoiseKeepsWitnessing`, `AllNoiseBlockCarriesTheErasedFlag`, `MixedBlockBelowTheOpaqueBoundStaysWitness`, `OddWidthPartitionPlacesEachTexelInItsBlock`, `WitnessBandByteStaysWitness`.

---

### Task 4: Fingerprint the Generated Texture Session Cache

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityGeneratedTextureEvidenceTests.cs`

**Interfaces:**
- Consumes: `Texture2D.updateCount`, `Texture2D.imageContentsHash`
- Produces: a session key of `(instanceId, channel, cutoff, bounds, activeMipmapLimit, updateCount, contentsHash)`, so an in place mutation or a recreated instance re-keys

- [ ] **Step 1: Write the failing test**

In `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityGeneratedTextureEvidenceTests.cs`, add one test.

```csharp
[Test]
public void SessionCache_DoesNotServeStaleEvidenceAfterInPlaceMutation()
{
    UnityGeneratedTextureEvidence.ClearCache();
    var texture = new Texture2D(4, 4, TextureFormat.RGBA32, true);
    var pixels = new Color32[16];
    for (var i = 0; i < pixels.Length; i++)
    {
        pixels[i] = new Color32(255, 255, 255, 0);
    }
    for (var m = 0; m < texture.mipmapCount; m++)
    {
        texture.SetPixels32(pixels, m);
    }
    texture.Apply(false, false);
    texture.name = "MutatedCacheKey (AAO UV Packed)";
    AssetDatabase.AddObjectToAsset(texture, _container);
    AssetDatabase.SaveAssets();

    Assert.That(
        UnityGeneratedTextureEvidence.TryCapture(
            texture, TextureChannel.Alpha, 1.0f,
            AlphaPolicyBounds.Inert, out var first),
        Is.True);
    Assert.That(first[0].GetAlpha(1, 0), Is.EqualTo(0));

    for (var i = 0; i < pixels.Length; i++)
    {
        pixels[i] = new Color32(255, 255, 255, 255);
    }
    texture.SetPixels32(pixels);
    texture.Apply(false, false);

    Assert.That(
        UnityGeneratedTextureEvidence.TryCapture(
            texture, TextureChannel.Alpha, 1.0f,
            AlphaPolicyBounds.Inert, out var second),
        Is.True);
    Assert.That(second[0].GetAlpha(1, 0), Is.EqualTo(255),
        "A mutated texture must re-capture instead of serving stale evidence.");
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the fixture `Alrauna.Amuse.Tests.Editor.Host.UnityGeneratedTextureEvidenceTests` in Unity Test Runner, EditMode.
Expect `SessionCache_DoesNotServeStaleEvidenceAfterInPlaceMutation` to fail, because the second lookup hits the session key built from the unchanged instance id and serves the first chain today.

- [ ] **Step 3: Implement the fingerprint key**

In `Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs`:

Replace the session cache field at lines 23-24.

```csharp
// The content fingerprint rides in the key: Unity bumps the update
// counter and the contents hash when capture visible pixels change,
// so an in place mutation or a recreated instance re-keys instead of
// serving stale evidence.
private static readonly Dictionary<
    (int instanceId, TextureChannel channel, float cutoff,
     AlphaPolicyBounds bounds, int activeMipmapLimit,
     uint updateCount, Hash128 contentsHash),
    AlphaMipChain> SessionCache = new();
```

Replace the key construction at lines 100-102.

```csharp
var threshold = Mathf.Clamp01(cutoffThreshold);
var key = (texture.GetInstanceID(), channel, threshold, bounds,
    activeMipmapLimit, texture.updateCount, texture.imageContentsHash);
```

The measured probe on 2026-10-09 in the dev editor instance confirmed both counters move on `SetPixels32` plus `Apply`. `Hash128` implements `Equals(Hash128)`, so the tuple key compares by value. If the second capture still serves stale evidence on the device under test, stop and record the observation, because the fingerprint premise failed.

- [ ] **Step 4: Run tests to verify they pass**

Run the fixture in Unity Test Runner, EditMode. Verify the new test and every existing test in the fixture pass, including `SessionCache_ReusesPreviouslyCapturedChain` at line 409, `DifferentLimitsDoNotShareTheGeneratedSessionCache` at line 320, and `ClearCache_EvictsPreviouslyCapturedChains` at line 562.

---

### Task 5: Narrow the Reader's Catch to Decode Read Failures

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/SourceImageAlphaReaderTests.cs`

**Interfaces:**
- Consumes: `SourceImageAlphaReader.TryReadSourceAlphaChain`
- Produces: `IsDecodeReadFailure(Exception)` as the catch filter. `IOException` and `UnauthorizedAccessException` keep the route refusal. Every other exception propagates.

- [ ] **Step 1: Write the tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Host/SourceImageAlphaReaderTests.cs`, add three tests.

```csharp
[Test]
public void IsDecodeReadFailure_AcceptsIoAndAccessFailures()
{
    Assert.That(
        SourceImageAlphaReader.IsDecodeReadFailure(
            new IOException(" vanished")),
        Is.True);
    Assert.That(
        SourceImageAlphaReader.IsDecodeReadFailure(
            new FileNotFoundException("missing.png")),
        Is.True);
    Assert.That(
        SourceImageAlphaReader.IsDecodeReadFailure(
            new UnauthorizedAccessException("denied")),
        Is.True);
}

[Test]
public void IsDecodeReadFailure_RejectsDefectExceptions()
{
    Assert.That(
        SourceImageAlphaReader.IsDecodeReadFailure(
            new NullReferenceException()),
        Is.False);
    Assert.That(
        SourceImageAlphaReader.IsDecodeReadFailure(
            new IndexOutOfRangeException()),
        Is.False);
    Assert.That(
        SourceImageAlphaReader.IsDecodeReadFailure(
            new InvalidOperationException("defect")),
        Is.False);
}

[Test]
public void TryReadSourceAlphaChain_TruncatedPngRefusesWithoutThrowing()
{
    // Characterization, not RED: malformed data refuses through the
    // LoadImage false path, with no exception involved, before and
    // after this task. It guards the refusal path against a later
    // regression to a throwing route.
    var pixels = new Color32[16];
    for (var i = 0; i < pixels.Length; i++)
    {
        pixels[i] = new Color32(255, 255, 255, 255);
    }
    var pngPath = Path.Combine(TestFolder, "truncated.png");
    var imported = TestTextureImport.WritePng(pngPath, 4, 4, pixels);
    File.WriteAllBytes(pngPath, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

    Assert.That(
        SourceImageAlphaReader.TryReadSourceAlphaChain(
            imported, TextureChannel.Alpha, 1.0f,
            AlphaPolicyBounds.Inert, out var chain),
        Is.False);
    Assert.That(chain, Is.Null);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the fixture `Alrauna.Amuse.Tests.Editor.Host.SourceImageAlphaReaderTests` in Unity Test Runner, EditMode.
Expect the two `IsDecodeReadFailure` tests to fail to compile, because the filter does not exist yet. That compile failure is the RED state for a new API.
Expect `TryReadSourceAlphaChain_TruncatedPngRefusesWithoutThrowing` to pass. It is a characterization guard.
No input driven test can force a defect exception inside the private catch without an injection seam, so the propagation guarantee rests on the narrowed filter and the filter tests.

- [ ] **Step 3: Implement the narrowed catch**

In `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs`:

Add the filter beside `TryReadSourceAlphaChain`.

```csharp
/// <summary>
/// True for the failures that mean this route cannot read the
/// authoring image today: the file vanished, shrank, or refuses
/// access. Every other exception is a defect. A defect propagates
/// instead of masquerading as unsupported input.
/// </summary>
internal static bool IsDecodeReadFailure(Exception e)
{
    return e is IOException || e is UnauthorizedAccessException;
}
```

Replace the bare catch at lines 149-153.

```csharp
catch (Exception e) when (IsDecodeReadFailure(e))
{
    chain = null;
    return false;
}
```

`FileNotFoundException` and `DirectoryNotFoundException` derive from `IOException`, so they keep the refusal. `ImageConversion.LoadImage` reports malformed data by returning false, which the body already handles near lines 65 to 68. A `NullReferenceException`, `IndexOutOfRangeException`, or `InvalidOperationException` from a defect now propagates to the caller.

If `SourceImageAlphaReaderTests.cs` lacks `using System;` at the top of the file, add it. The three defect types live in the `System` namespace. The file already imports `System.IO`.

- [ ] **Step 4: Run tests to verify they pass**

Run the fixture in Unity Test Runner, EditMode. Verify the three tests and every existing test in the fixture pass.
