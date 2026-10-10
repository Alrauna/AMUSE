# Host Evidence Faithfulness: Design and Specification

Date: 2026-10-09.
Branch plan: fix/latent-bugs-and-impurities. Base: fae7e7e.
No source assets are modified.
All changes stay inside the package Editor capture layer and its tests.

Privacy note: this document contains no private avatar, renderer, material, texture, scene, or machine identity. Public vendor shader identifiers stay exact. Probe results name the executing editor by role only. All paths are repository-relative.

Labels: `[SOURCE]` is a fact read from cited code lines in this repository, or measured by a probe on 2026-10-09.
`[INFERENCE]` is a conclusion from evidence.

---

## 1. Summary

This design fixes five host evidence faithfulness defects from the third latent bug audit.

The design refuses the disk decode route for imports whose alpha channel Unity regenerates at import.
The design pins the red channel capture to the inert policy bounds at the capture seam and asserts the Analysis field contract there.
The design aligns the masked streaming chain's coarse levels with the effective content, instead of refusing the masked route.
The design adds a content fingerprint to the generated texture session cache key.
The design narrows the disk reader's blanket catch to decode read failures.

The changes keep every capture route fail closed.
The changes keep the clone route as the fallback for every refused capture.

---

## 2. Background and Motivation

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs:29, 62-81, 103-108`.
`TryReadSourceAlphaChain` decodes the raw .png or .tga bytes with `ImageConversion.LoadImage`.
The reader consults one importer fact.
`alphaSource == TextureImporterAlphaSource.None` turns the alpha samples into 1.0, which matches playback.
Unity also replaces the alpha channel with RGB grayscale when the import uses `TextureImporterAlphaSource.FromGrayScale`.
The reader returns the file's real alpha bytes for that mode.
Playback samples the grayscale luminance as alpha.
A fully opaque file over mid gray RGB then captures an all 255 chain while playback samples about 0.5.

`[SOURCE]` `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs:843-849`.
The test `AlphaSourceFromGrayScale_ReportsTheGeneratedAlphaNotTheInputAlpha` pins the field route behavior.
The field route reports the generated alpha for a FromGrayScale import.
The disk route disagrees with that pinned behavior today.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs:1370-1395`.
`CaptureTexture` calls the red channel capture with threshold 1.0 and the caller's active bounds.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:22-40`.
The `AlphaFieldProvider` contract says byte 255 marks exactly the texels whose value is exactly 1.
Every other byte marks a value strictly below 1.
The mapped arm in `TriangleAlphaClassifier.cs:432-446, 500-528` evaluates the affine map at exactly 1 for a no-witness region.
Under an active policy a red byte 255 can mean at or above the opaque bound.
A texel at 0.85 under an 0.80 bound stores 255.
The mapped arm then proves opaque for a texel whose true mapped value is 0.7.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs:329-331` and `Packages/com.alrauna.amuse/Editor/Host/AlphaFieldSet.cs:49-61`.
The capture core forces inert bounds inside the primary route only when the cutoff threshold is below one.
The alpha field key applies the same force.
The red field key carries `texture.CaptureBounds` with no force.
So the active bounds reach the red field whenever the user moves the opaque percent below 100.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/AlphaFieldSet.cs:62-74`.
The `ForRed` key builder already documents that masks are always captured exact.
The capture does not pin the bounds that make the claim true.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/Shaders/AmuseRedExactOne.shader:13-27`.
The red predicate comment says the sRGB transfer is decided by the import.
A `Texture2D.Load` fetch applies only the Unorm expansion.
lilToon samples the mask through a standard sampler, so a default sRGB mask import linearizes at playback.
The capture compares raw stored bytes against a policy bound.
The two spaces disagree below 1.0.
Under inert bounds the transfer is monotone and fixes exactly 1.0, so the exact 255 contract holds in both spaces.
The shader comment states exactly this.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/SourceImageMaskedChain.cs:15-24, 79-104`.
The masked builder masks before averaging.
A texel below the noise bound takes no part in its block average.
A block whose every source texel is noise carries the erased flag.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs:164-174`.
The clone route comment states the opposite contract.
The clone's coarser levels arrive pre-averaged by the importer.
Each texel resolves at its own level against the plain average of all texels.
The masked route excludes the noise texels and raises the coarse averages.
The two routes produce different evidence for the same texture.
Which route serves depends on the file format.
The reader's class comment at `SourceImageAlphaReader.cs:10-17` promises predicate equivalence with the effective representation at every captured mip.
At most one route can honor that promise.
The masked route is the optimistic one.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs:23-24, 100-105`.
The session cache keys a chain by instance id, channel, cutoff, bounds, and mip limit.
A hit returns the stored chain before any content check.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs:149-153`.
One bare catch wraps the whole decode and returns route unavailable.
A defect inside the reader falls through to the clone route with no console signal.
The repository invariant says programming failures must throw and never masquerade as unsupported input.

`[SOURCE]` measured probe on 2026-10-09 in the dev editor instance for this repository, Unity 2022.3.
`Texture2D.updateCount` read 0, then 1, then 2 across two `SetPixels32` plus `Apply` mutations of one texture.
`Texture2D.imageContentsHash` changed on every applied content change.
Both members exist on `UnityEngine.Texture`.
The reset behavior for a recreated texture was not probed.
`[INFERENCE]` a fresh native object starts both members fresh, so the instance-reuse vector needs the recreated texture to reproduce the old content hash.

---

## 3. Detailed Design

### 3.1 The Disk Route Refuses a Generated Alpha Channel

In `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs`:

Move the importer read before the decode.
Read it once, directly after the extension gate.

Refuse the route when all three facts hold.
The importer exists.
The channel is `TextureChannel.Alpha`.
The importer mode is `TextureImporterAlphaSource.FromGrayScale`.

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

The later `isAlphaNone` line reuses the same `importer` local.
No second `AssetImporter.GetAtPath` call stays.

The refusal is channel scoped.
Grayscale generation rewrites only the alpha channel.
The red bytes the mask evidence reads survive the import unchanged.
So a red channel read of a FromGrayScale import stays faithful and stays admitted.

`alphaSource == TextureImporterAlphaSource.None` stays handled as today.
The reader forces the alpha samples to 1.0 and playback samples 1.0.
That mode stays faithful.

A missing importer keeps today's behavior.
A texture without a `TextureImporter` has no import transform, so the file bytes are the imported content.

The clone route fallback needs no change.
`UnityStreamingTextureEvidence.TryCapture` already tries the disk route first and falls through to the clone on a false return at `UnityStreamingTextureEvidence.cs:95-98`.

The pinned test `AlphaSourceFromGrayScale_ReportsTheGeneratedAlphaNotTheInputAlpha` stays unchanged.
It pins the field route, and the disk route now agrees with it by refusing.

Adjacent observation, recorded and out of scope: `alphaIsTransparency = true` dilates RGB at import.
A red channel read of such an import can diverge the same way.
The investigation scoped this finding to FromGrayScale, so this design does not act on the dilation case.

### 3.2 The Red Channel Captures Under Inert Bounds and Asserts the Field Contract

This is the cross pair decision. The repository owner can overrule it.

In `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs`:

Pass `AlphaPolicyBounds.Inert` for the red channel at the capture seam in `CaptureTexture`.

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

Rewrite the comment above the two channel calls.
The alpha channel reads under the active policy.
The red channel reads under the inert bounds.
The Analysis mapped arm evaluates a red field sample at exactly 1.
Byte 255 must mark exactly the texels whose value is exactly 1.
An active policy verdict on red would store 255 for a sub-one texel and break the mapped contract.

Add the seam assertion as an internal static helper on the same class.

```csharp
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

The inert bounds never erase because the noise bound is 0.
So an inert red chain carries only 255 and 0.
A third byte proves a capture seam defect.
The assertion throws instead of serving evidence the proof core would misread.
It skips levels flagged without evidence, because their placeholder grids prove nothing.

In `Packages/com.alrauna.amuse/Editor/Host/AlphaFieldSet.cs`:

Pin `ForRed` to the inert bounds.

```csharp
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

Update the doc comment.
The capture pins the red arm to the inert bounds, so the key carries the bounds the red bytes were captured under.

Update the `CapturedTextureEvidence.CaptureBounds` doc comment in `UnityMaterialEvidenceCapture.cs:348-357`.
The alpha arm keys these bounds.
The red arm keys the inert bounds because the capture pins red to them.

Effect on coverage.
Under an active policy a mask texel between the opaque bound and 1 no longer stores 255.
It stores its true sub-one verdict.
The mapped arm answers unknown instead of a false opaque proof.
That direction is conservative.

Effect on part B.
The pin neutralizes the red field exposure of the sRGB disagreement.
Under inert bounds the exact 255 contract holds in both value spaces, because the transfer is monotone and fixes 1.0.
The disagreement for active bound comparisons on other captures stays open.
The one texel probe in section 4 stays a documented precondition for any future bound aware capture work.

### 3.3 The Masked Chain Aligns Coarse Levels With Effective Content

Decision and justification.
This design aligns the masked route instead of refusing it under an active policy.
Three reasons.

The route exists to serve exact uncompressed evidence.
Refusing it under an active policy would push every policy active streaming texture to the clone route.
That removes the route for its main working case.

The aligned rule is the clone route's own per level verdict applied to the plain box average.
Both routes then share one verdict rule.
They can no longer disagree by construction.

The residual importer rounding and filtering divergence at coarse levels stays the route's documented caveat.
The finding records the same residual for the base route.
The aligned rule removes the charged failure direction, which is unbounded optimism from dropped texels.

In `Packages/com.alrauna.amuse/Editor/Host/SourceImageMaskedChain.cs`:

Rewrite the class comment.
The builder averages every texel of a block.
That plain average is what the imported mip chain stores.
The builder publishes the policy verdict of that average.
A block whose average sits below the noise bound carries the erased flag.
All comparisons are exact integers.
No float crosses a verdict.

Replace the body of `BuildBlock`.

```csharp
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
return sum >= (long)bounds.OpaqueBound * count
    ? byte.MaxValue
    : (byte)0;
```

Behavior at mip 0 stays identical.
A level 0 block is one texel.
The texel below the noise bound averages below the noise bound and carries the erased flag.
The texel at or above the opaque bound stores 255.
So every level 0 pinned expectation survives.

Behavior at coarse levels changes in exactly the charged direction.
A block with a noise texel now averages all its texels.
The average drops, so the block can become a witness instead of opaque.
That matches the imported mip chain, which the clone route reads.

Update the reader's class comment at `SourceImageAlphaReader.cs:10-17` and the masked route comment inside `TryReadSourceAlphaChain`.
The masked route derives every level from the plain box averages, so the coarse levels describe the same effective content the imported chain stores.

No clone route change is needed.
The clone route comment at `UnityStreamingTextureEvidence.cs:186-190` stays true.

### 3.4 The Session Cache Key Carries a Content Fingerprint

In `Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs`:

Extend the session cache key with `texture.updateCount` and `texture.imageContentsHash`.

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

Read both members before the lookup and mix them into the key.

```csharp
var threshold = Mathf.Clamp01(cutoffThreshold);
var key = (texture.GetInstanceID(), channel, threshold, bounds,
    activeMipmapLimit, texture.updateCount, texture.imageContentsHash);
```

Justification of the fingerprint choice.
The route's textures are in memory producer outputs.
They are not generally CPU readable, so a mip 0 byte hash cannot serve as a universal key part.
The Unity maintained pair is two property reads on every call, hit or miss.
`[SOURCE]` the measured probe in section 2 shows both move on an applied content change.
`[INFERENCE]` an instance id reuse after destroy and recreate starts both members fresh, so a stale entry can match only a recreated texture whose content hash equals the old content.
Equal content under an equal predicate produces an equal chain, so serving the cached chain is then correct.

`Hash128` implements `Equals(Hash128)`, verified by reflection in the dev editor instance on 2026-10-09.
The tuple key compares by value.

### 3.5 The Reader Catches Only Decode Read Failures

In `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs`:

Add an internal static filter on the same class.

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

Replace the bare catch.

```csharp
catch (Exception e) when (IsDecodeReadFailure(e))
{
    chain = null;
    return false;
}
```

`FileNotFoundException` and `DirectoryNotFoundException` derive from `IOException`, so they stay refusals.
`ImageConversion.LoadImage` reports malformed data by returning false, which the body already handles.
`NullReferenceException`, `IndexOutOfRangeException`, `DivideByZeroException`, and `InvalidOperationException` from a defect now propagate.
The route refusals keep the silent fallback to the clone route.
A defect surfaces as the exception it is.

---

## 4. Verification and Test Plan

1. Unit test `TryReadSourceAlphaChain_RefusesGrayScaleGeneratedAlphaForTheAlphaChannel` in `Packages/com.alrauna.amuse/Tests/Editor/Host/SourceImageAlphaReaderTests.cs`.
   Import an opaque alpha, mid gray RGB PNG with `alphaSource = TextureImporterAlphaSource.FromGrayScale`.
   Assert the disk route returns false and no chain.
2. Characterization test `TryReadSourceAlphaChain_KeepsServingTheRedChannelOfGrayScaleImports`.
   The same import through `TextureChannel.Red` stays admitted.
   This test passes before and after.
   It guards the channel scope of the refusal.
3. Seam test `RedFieldUnderAnActivePolicyMatchesTheInertRedField` in `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`.
   Capture one lilToon stand in material with an `_AlphaMask` texel at 217 under `AlphaPolicyBounds.From(80, 2)` and under `AlphaPolicyBounds.Inert`.
   Assert both red chains are byte equal.
   Before the fix the active capture stores 255 where the inert capture stores 0.
4. Unit tests `RedFieldContractAssertion_RefusesTheErasedFlagByte` and `RedFieldContractAssertion_AcceptsBinaryVerdicts` in the same file.
   The first feeds a synthetic chain with an erased flag byte and asserts `InvalidOperationException`.
   The second feeds a binary chain and passes.
5. Unit test `ForRed_KeysTheInertBounds` in `Packages/com.alrauna.amuse/Tests/Editor/Host/AlphaFieldSetTests.cs`.
   Build a `CapturedTextureEvidence` with active bounds.
   Assert `AlphaFieldKey.ForRed` carries `AlphaPolicyBounds.Inert`.
6. Pinned test update `SingleStrayDragsEveryCoarseLevelBelowTheOpaqueBound` in `Packages/com.alrauna.amuse/Tests/Editor/Analysis/SourceImageMaskedChainTests.cs`.
   This replaces `SingleStraySubstitutesAtEveryLevel` at line 46.
   The old test bakes the optimistic substitution this finding charges.
   The new test pins the aligned behavior.
   One stray at byte 3 in an 8 by 8 field of 255 under bounds from percents 100 and 2 drags every coarse level below the opaque bound.
7. Route test `MaskedRouteCoarseLevelsAverageEveryTexelUnderActivePolicy` in `Packages/com.alrauna.amuse/Tests/Editor/Host/SourceImageAlphaReaderTests.cs`.
   A 4 by 4 PNG with one zero texel under bounds from percents 100 and 2.
   Level 1 block over the stray reads 0.
   Before the fix it reads 255.
8. Unit test `SessionCache_DoesNotServeStaleEvidenceAfterInPlaceMutation` in `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityGeneratedTextureEvidenceTests.cs`.
   Capture, mutate in place with `SetPixels32` plus `Apply`, capture again.
   The second capture must reflect the mutation.
9. Unit tests `IsDecodeReadFailure_AcceptsIoAndAccessFailures` and `IsDecodeReadFailure_RejectsDefectExceptions` in `Packages/com.alrauna.amuse/Tests/Editor/Host/SourceImageAlphaReaderTests.cs`.
10. Characterization test `TryReadSourceAlphaChain_TruncatedPngRefusesWithoutThrowing`.
    Overwrite the asset file bytes with garbage after import.
    The route returns false without throwing.
    This test passes before and after.
    It guards the refusal path for malformed data.
11. Manual verification step, part B precondition.
    Before any future bound aware red or alpha capture work, run the one texel probe.
    Blit the red predicate with bound 0.8 over a stored byte 217 of an sRGB mask.
    Compare against a sampled fetch of the same texel.
    The sample linearizes to about 0.7.
    Record the observed pair of values in the investigation follow up.

### Rollout and touchpoints

Execution order across pairs is 1 multi capture, 2 analysis, 3 host, 4 build, 5 poiyomi, 6 semantics, 7 presets architecture.
This pair runs third.

This pair shares one production file with another pair. Pair 6 adds the `BoundedColorRange` captured fact and one `CapturedTextureEvidence` field to `Editor/Host/UnityMaterialEvidenceCapture.cs` at position 6 in the shared execution order. This pair lands first. Pair 6's constructor parameter step must also update the new positional construction this pair's Finding 6 test adds.
The file sharing map names `Editor/Semantics/UnityMaterialSemantics.cs` to pairs 1 and 6, and `Editor/Build/TransientUnlockWindowClose.cs` to pairs 4 and 7.
The Analysis contract texts cited in section 2 are read only touchpoints.
Pair 2 owns the `AlphaSemanticsResolver.cs` edits for its own findings.
The tasks inside this plan touch `SourceImageAlphaReader.cs` in tasks 1, 3, and 5.
Run them in plan order to avoid rebase friction.
