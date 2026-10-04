# Generated-Route Alpha Policy Erasure Fix Implementation Plan

> **For agentic workers:** This plan executed on 2026-10-03. The checkboxes
> show the executed state, and each run step carries its observed outcome.
> A fresh execution should follow the same order and re-observe every run.

**Goal:** Make the generated capture route apply the Alpha Separator alpha
policy exactly like the other routes, so LAC replacement copies and other
generated textures stop proving triangles opaque over genuine partial
transparency.

**Architecture:** One change site, `UnityGeneratedTextureEvidence.TryCapture`.
The route writes the effective policy bounds to its blit material, stores
the shader's three-state verdict bytes verbatim in the exact arm, validates
the stored bytes, and keeps the cutoff arm's raw binarization.

**Tech Stack:** Unity 2022.3.22f1 editor assembly (`Alrauna.Amuse.Editor`),
NUnit EditMode tests (`Alrauna.Amuse.Tests.Editor`), the existing predicate
shaders.

**Spec:** `docs/superpowers/specs/2026-10-03-generated-route-alpha-policy-erasure-design.md`
- The plan argues from the spec. Executors read both. Spec section numbers
  below refer to that file.

**Investigation:** `docs/superpowers/investigations/2026-10-03-generated-route-alpha-policy-erasure.md`

## Global Constraints

- Fail closed. A readback that does not carry the three verdict bytes
  refuses the texture with the existing `UnavailableCapture` path. No new
  refusal kinds (spec section 8).
- Route parity. The stored byte for one texel must not depend on the route
  (spec section 7).
- A shader cutoff source stays policy-inert on this route (spec sections 6
  and 8).
- Production types stay `internal`. Comments state why, not what.
- No absolute paths, no host names, no ports, no private identifiers.
- Test mechanics: after creating or changing files, refresh Unity and wait
  for compile, then run through the dev editor instance after the
  `Application.dataPath` identity check. A run that reports 0 tests is a
  failure. A successful compile is never validation. Record observed counts.
- Git boundary: work on the topic branch
  `fix/generated-route-alpha-policy-erasure`. Never push, never open a PR,
  without explicit authorization.

---

### Task 1: RED tests that pin both defects

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityGeneratedTextureEvidenceTests.cs`

**Interfaces:**
- Consumes: `UnityGeneratedTextureEvidence.TryCapture(Texture2D,
  TextureChannel, float, AlphaPolicyBounds, out AlphaMipChain)`,
  `AlphaPolicyBounds.From(int, int)`, `AlphaTextureData.GetAlpha(int, int)`,
  `AlphaTextureData.ErasedFlag`, and the fixture's container sub-asset seam
  (`AssetDatabase.AddObjectToAsset`, name marker `AlphaMask (AAO UV Packed)`).
- Produces: two behavior tests that Tasks 2 and 3 turn green.

- [x] **Step 1: Write the two failing tests**

```csharp
[Test]
public void AnActiveNoisePolicyKeepsMidBandTexelsWitnessesOnTheGeneratedRoute()
{
    var midTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
    var pixels = new Color32[16];
    for (var i = 0; i < pixels.Length; i++)
    {
        // Alpha 128 sits above the noise byte 26 of a 10 percent
        // gate and below the opaque byte 255, so the policy calls
        // it a witness.
        pixels[i] = new Color32(255, 255, 255, 128);
    }
    midTex.SetPixels32(pixels);
    midTex.Apply(false, false);
    midTex.name = "AlphaMask (AAO UV Packed)";
    AssetDatabase.AddObjectToAsset(midTex, ContainerPath);
    AssetDatabase.SaveAssets();

    var ok = UnityGeneratedTextureEvidence.TryCapture(
        midTex, TextureChannel.Alpha, 1.0f,
        AlphaPolicyBounds.From(100, 10), out var chain);

    Assert.That(ok, Is.True);
    Assert.That(chain, Is.Not.Null);
    Assert.That(chain[0].GetAlpha(0, 0), Is.EqualTo(0));
}

[Test]
public void AnActiveOpaqueBoundLowersTheOpaqueBarOnTheGeneratedRoute()
{
    var nearTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
    var pixels = new Color32[16];
    for (var i = 0; i < pixels.Length; i++)
    {
        pixels[i] = new Color32(255, 255, 255, 240);
    }
    nearTex.SetPixels32(pixels);
    nearTex.Apply(false, false);
    nearTex.name = "AlphaMask (AAO UV Packed)";
    AssetDatabase.AddObjectToAsset(nearTex, ContainerPath);
    AssetDatabase.SaveAssets();

    var ok = UnityGeneratedTextureEvidence.TryCapture(
        nearTex, TextureChannel.Alpha, 1.0f,
        AlphaPolicyBounds.From(90, 10), out var chain);

    Assert.That(ok, Is.True);
    Assert.That(chain, Is.Not.Null);
    Assert.That(chain[0].GetAlpha(0, 0), Is.EqualTo(byte.MaxValue));
}
```

- [x] **Step 2: Refresh Unity, wait for compile, run the focused class
  filter against the unfixed production code**

Observed 2026-10-03: the full EditMode suite ran with the two tests in
place. 2539 completed, exactly 2 failed, both new tests, each
`Expected 0/255, But was 1` (the erased flag). No other test failed. RED
confirmed against a named plausible wrong implementation.

---

### Task 2: The route fix

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs`

**Interfaces:**
- Consumes: `AlphaPolicyBounds`, `AlphaPolicyBounds.Inert`,
  `UnityAlphaFieldEvidence.IsPredicateFlagBuffer(byte[])`, the predicate
  shaders' verdict packing (verdict in red, raw value in green).
- Produces: stored bytes that satisfy spec section 7's parity table for
  every producer of generated textures.

- [x] **Step 1: Write the effective bounds to the blit material**

After the material creation, before the level loop:

```csharp
// The shader owns the three-state verdict, so the active
// policy must reach it exactly as on the GPU route. A
// shader cutoff keeps the policy inert on this route,
// exactly as on the other three: the route re-derives the
// cutoff arm from the raw value, so bounds would not
// touch it, and the shader must not erase with them
// either.
var effectiveBounds = threshold < 1.0f
    ? AlphaPolicyBounds.Inert
    : bounds;
material.SetFloat(
    "_OpaqueBound", effectiveBounds.OpaqueBound / 255f);
material.SetFloat(
    "_NoiseBound", effectiveBounds.NoiseBound / 255f);
```

- [x] **Step 2: Store the verdict verbatim in the exact arm, keep the
  cutoff arm, validate the exact arm**

Replace the flag re-derivation in the level loop:

```csharp
var flags = new byte[data.Length];
if (threshold >= 1.0f)
{
    // The shader already emitted the exact
    // three-state verdict under the active
    // bounds, so the red byte stores verbatim,
    // byte for byte, as the GPU route stores
    // its readback. Re-deriving the flags from
    // the verdict byte would compare the mid
    // band's 0 against the noise bound and
    // turn every below-exact-one texel into
    // erasable noise.
    for (var i = 0; i < data.Length; i++)
    {
        flags[i] = data[i].r;
    }

    if (!UnityAlphaFieldEvidence
            .IsPredicateFlagBuffer(flags))
    {
        return false;
    }
}
else
{
    // A shader cutoff source stays gate-inert:
    // binarize the raw value the shader
    // returns in green, with no erasure and
    // no policy bounds.
    for (var i = 0; i < data.Length; i++)
    {
        flags[i] = (data[i].g / 255f) >= threshold
            ? byte.MaxValue
            : (byte)0;
    }
}
```

- [x] **Step 3: Refresh Unity, wait for compile, run the focused class
  filter**

Observed 2026-10-03: the full EditMode suite ran on the fixed code. 2537
passed, 0 failed, 0 skipped, in about 182 seconds. The two opt-in
integration guards report inconclusive by design and count with the passing
set. Both Task 1 tests pass. The pre-existing erasure test under
`AlphaPolicyBounds.From(100, 2)` passes, which guards against the
overcorrection of never erasing (spec section 9).

---

### Task 3: Closure checks

**Files:**
- No production or test files. Records only.

- [x] **Step 1: Whitespace check**

Run: `git diff --check`.
Observed 2026-10-03: clean.

- [x] **Step 2: Identifier sweep**

Sweep every changed file for an at sign joined to a hexadecimal hash,
drive-letter paths, home-directory paths, four-digit ports, and every
private identifier known to the session.
Observed 2026-10-03: no hits.

- [x] **Step 3: Record the work**

The investigation record and this plan carry the observed counts and dates.
The branch stays uncommitted until staging authorization arrives.
