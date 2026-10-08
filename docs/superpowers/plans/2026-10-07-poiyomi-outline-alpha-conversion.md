# Poiyomi Outline Alpha Conversion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Poiyomi material with outlines enabled proves its outline shell
in the alpha semantics, so the classified-proven set is already
base-and-outline proven, conversion carries no outline machinery, and a
polygon moves only when both its base surface and its outline patch are
opaque.

**Architecture:** The outline alpha composes into the existing Poiyomi alpha
semantics as conditional terms through the exact product machinery
(`ScalarProductFold.Fold`) gated on `_EnableOutlines`. The outline factor
multiplies into the base alpha chain: it never replaces the base chain,
preserving the controller contract that both base surface and outline shell
must be opaque. The alpha evidence request gathers the outline facts
(scalars including `_OutlineOverrideAlpha`, the pan vector, the line color,
and one outline texture entry). The conversion layer's outline gate, refusal
member, schema entry, and presence machinery are deleted in a clean cutover,
because the classified set is already the intersection the contract asks for.
**Tech Stack:** C# against Unity 2022.3.22f1, editor-only assembly
`Alrauna.Amuse.Editor`, NUnit via Unity Test Framework (EditMode, run through
the Unity Test Runner in the dev editor instance; no CLI).

**Spec:** `docs/superpowers/specs/2026-10-07-poiyomi-outline-alpha-conversion-design.md`
(decisions D1-D6 taken on 2026-10-07; background in
`docs/superpowers/investigations/2026-10-07-poiyomi-outline-support-investigation.md`).

## Global Constraints

- No CLI test run exists. Tests run through the Unity Test Runner, EditMode.
  A filtered run reporting 0 tests is a failure.
- RED before GREEN: each behavior change observes a failing test first, for
  a named plausible wrong implementation. A compile error on a removed
  contract counts as RED for that cutover. An assertion that passes on
  first run is recorded as characterization, never dressed up as RED.
- One public type per file; file name equals type name; namespaces mirror
  folders; production types `internal`; `Editor/AssemblyInfo.cs` grants
  `InternalsVisibleTo`.
- Closed refusal and diagnostic vocabularies per scope. Unsupported means a
  named value: conversion refusals for conversion policy, analysis
  diagnostics for analysis facts. Programming defects throw and block the
  build. Fail closed; `MustRemainTransparent` is absorbing.
- Tests run on schema-only stand-in shaders under `Hidden/Alrauna/AmuseTests/*`.
  Vendor shaders are never installed.
- Assertions use the NUnit constraint model:
  `Assert.That(actual, Is.EqualTo(expected))`.
- No private identifiers, machine paths, ports, or instance names in any
  file, commit message, or report. Record observed counts.
- Every commit in this plan executes only on the session's explicit
  authorization. Stage explicit paths only.
- After each task: refresh the editor (compile request, wait for ready),
  read the console, and require zero CS errors before running tests.
- The chain pins live in spec section 3 (installed attested source). Do not
  re-derive vendor facts from vendor repositories. The pass state block
  stays unpinned and no task reads pass state.

---

### Task 1: Re-home the outline evidence to the alpha request

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`
  (property constants near `MainTextureProperty`; `CreateAlphaEvidenceRequest`
  `:2499-2570`)
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiSemanticTest.shader`
  (property block near `:185-189`)
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiTwoPassSemanticTest.shader`
  (property block near `:198-202`)
- Test: the existing alpha-request test (re-locate by symbol
  `AlphaEvidenceRequest` in `PoiyomiCutoutSplitTests.cs`)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`
  (the Poiyomi two-questions separation row at `:384`)
**Interfaces:**
- Consumes: the existing `MaterialEvidenceRequest` constructor and
  `TexturePropertyEvidenceRequest(string propertyName, TextureEvidenceKinds
  evidence, string cutoffScalarProperty = null)`.
- Produces: `AlphaEvidenceRequest` carries the scalars `_EnableOutlines`,
  `_OutlineOverrideAlpha`, `_OutlineAlphaDistanceFade`,
  `_OutlineALColorEnabled`, `_OutlineTextureUV`; the vector
  `_OutlineTexturePan`; the color `_LineColor`; and one texture entry
  `_OutlineTexture` with `TextureEvidenceKinds.ScaleOffset |
  TextureEvidenceKinds.SourceIdentity | TextureEvidenceKinds.SampledAlphaIsOne |
  TextureEvidenceKinds.AlphaChannel`.
  New property-name constants in `PoiyomiMaterialSemantics`:
  `OutlineEnabledProperty`, `OutlineOverrideAlphaProperty`,
  `OutlineAlphaDistanceFadeProperty`, `OutlineAudioLinkColorProperty`,
  `OutlineTextureUvProperty`, `OutlineTexturePanProperty`,
  `LineColorProperty`, `OutlineTextureProperty`. Task 2 consumes the
  captured entries; the conversion request is untouched in this task.

- [ ] **Step 1: Write the failing alpha-request assertions**

Extend the existing alpha-request test with the outline entries:

```csharp
Assert.That(
    request.ScalarProperties,
    Does.Contain(PoiyomiMaterialSemantics.OutlineEnabledProperty));
Assert.That(
    request.ScalarProperties,
    Does.Contain(PoiyomiMaterialSemantics.OutlineOverrideAlphaProperty));
Assert.That(
    request.ScalarProperties,
    Does.Contain(PoiyomiMaterialSemantics.OutlineTextureUvProperty));
Assert.That(
    request.VectorProperties,
    Does.Contain(PoiyomiMaterialSemantics.OutlineTexturePanProperty));
Assert.That(
    request.ColorProperties,
    Does.Contain(PoiyomiMaterialSemantics.LineColorProperty));
var outline = request.TextureProperties.Single(t =>
    t.PropertyName == PoiyomiMaterialSemantics.OutlineTextureProperty);
Assert.That(
    outline.Evidence,
    Is.EqualTo(TextureEvidenceKinds.ScaleOffset |
        TextureEvidenceKinds.SourceIdentity |
        TextureEvidenceKinds.SampledAlphaIsOne |
        TextureEvidenceKinds.AlphaChannel));
```

The internal constants need `InternalsVisibleTo` access the test assembly
already has. Update the Poiyomi two-questions separation row in
`UnityMaterialSemanticsTests.cs`: `_EnableOutlines` is alpha-request state
from this task on, so the Poiyomi conversion-only array at `:384` shrinks
to `_ZWrite` (the lilToon row at `:453` remains unchanged):

```csharp
// _EnableOutlines moved to the alpha request; only _ZWrite stays
// conversion-only.
foreach (var conversionOnly in new[] { "_ZWrite" })
```
- [ ] **Step 2: Run the semantics groups, verify RED**

Run group
`Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi` (the request tests) and
group `Alrauna.Amuse.Tests.Editor.Semantics.UnityMaterialSemanticsTests`.
Expected: the new assertions fail on the missing constants and request
entries (compile error counts as RED for the request contract); the
separation row that still lists `_EnableOutlines` as conversion-only fails.
Record the failing counts.

- [ ] **Step 3: Add the outline properties to both stand-in shaders**

In `PoiyomiSemanticTest.shader`, immediately after the `_OutlineDstBlendAlpha`
declaration (the outline block that ends at `:188`), add:

```
        _LineColor ("Outline Color", Color) = (1,1,1,1)
        [ToggleUI] _OutlineOverrideAlpha ("Override Base Alpha", Float) = 0
        [ToggleUI] _OutlineALColorEnabled ("Audio Link Outline Color", Float) = 0
        _OutlineAlphaDistanceFade ("Outline Distance Alpha Fade", Float) = 0
        _OutlineTextureUV ("Outline UV", Int) = 0
        _OutlineTexturePan ("Outline Texture Pan", Vector) = (0, 0, 0, 0)
        _OutlineTexture ("Outline Texture", 2D) = "white" {}
```
Add the same seven lines to `PoiyomiTwoPassSemanticTest.shader` after its
`_OutlineDstBlendAlpha` declaration (at `:201`). The defaults mirror the
vendor property block (`Poiyomi Toon.shader:2132-2135` installed): white line
features off, UV0, zero pan, white outline texture. The outline texture
declaration keeps its scale-offset fields - the vendor affine `poiUV` reads
`_OutlineTexture_ST`, and Unity derives that float4 from the texture
property's tiling and offset - so the declaration carries no
`[NoScaleOffset]`.

- [ ] **Step 4: Implement the request additions**

In `PoiyomiMaterialSemantics.cs`, add the property constants beside
`MainTextureProperty`:

```csharp
internal const string OutlineEnabledProperty = "_EnableOutlines";
internal const string OutlineOverrideAlphaProperty = "_OutlineOverrideAlpha";
internal const string LineColorProperty = "_LineColor";
internal const string OutlineTextureProperty = "_OutlineTexture";
internal const string OutlineAlphaDistanceFadeProperty =
    "_OutlineAlphaDistanceFade";
internal const string OutlineAudioLinkColorProperty =
    "_OutlineALColorEnabled";
internal const string OutlineTextureUvProperty = "_OutlineTextureUV";
internal const string OutlineTexturePanProperty = "_OutlineTexturePan";

In `CreateAlphaEvidenceRequest` (`:2499-2570`), add to the scalar set:

```csharp
OutlineEnabledProperty,
OutlineOverrideAlphaProperty,
OutlineAlphaDistanceFadeProperty,
OutlineAudioLinkColorProperty,
OutlineTextureUvProperty,
extend the vector and color lists:

```csharp
vectorProperties: new[]
{
    MainTexPanProperty,
    AlphaMaskPanProperty,
    OutlineTexturePanProperty,
},
colorProperties: new[] { ColorProperty, LineColorProperty },
```

and add the outline texture entry after the mask entry, mirroring its
comment style:

```csharp
// The outline texture proves per triangle through its alpha channel when
// the outline is enabled: its own scale and offset for the plain affine,
// the stable project identity, the whole-texture fast-path fact, and the
// alpha field itself. Sampling is never asked of it: the vendor outline
// block samples through the main sampler, whose state the main-texture
// request above already carries.
new TexturePropertyEvidenceRequest(
    OutlineTextureProperty,
    TextureEvidenceKinds.ScaleOffset |
    TextureEvidenceKinds.SourceIdentity |
    TextureEvidenceKinds.SampledAlphaIsOne |
    TextureEvidenceKinds.AlphaChannel),
```

- [ ] **Step 5: Run the semantics groups, verify GREEN**

Same groups as Step 2. Expected: all green, and every pre-existing request
test still passes. Run group
`Alrauna.Amuse.Tests.Editor.Host.UnityAnimationEvidenceCaptureTests`: the
capture schema rows that contain `_EnableOutlines` must stay green, because
the combined capture request still gathers it - now through the alpha
request. Record the counts.

- [ ] **Step 6: Commit (only on explicit session authorization)**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiSemanticTest.shader \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiTwoPassSemanticTest.shader \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs
git commit -m "feat(poiyomi): gather the outline alpha evidence in the alpha request"
```

---

### Task 2: The outline terms in the alpha semantics

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`
  (UV channel ordering in `Classify` around line `:400`)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`
  (channel mapping with affine transform test)
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`
  (the alpha output construction and the chain folds after it;
  the feature-label map `:983-996`)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOutlineAlphaSemanticsTests.cs`
  (new file)

**Interfaces:**
- Consumes: Task 1's captured outline entries; the mask composition shapes
  (`TryInterpretAlphaMask` producing replacement and multiplier terms,
  `PoiyomiMaterialSemantics.cs:1898-1906`); `ScalarSemanticValue.Texture`
  (`MaterialSemantics.cs:439`); `TextureSample` (`MaterialSemantics.cs:198`);
  `TextureChannel.Alpha` (`MaterialSemantics.cs:223`); the supported
  mapping form (`TryGetSupportedUvMapping`, `:2600-2650`); the diagnostic
  pattern (`RecordUnknown` with `UnsupportedFeature` and the offending
  property, `:1246-1252`).
- Produces: the alpha output for outline-enabled materials composes the
  outline factor into the base alpha through `ScalarProductFold.Fold` (it
  never replaces the base alpha, preserving the controller contract), with
  mask-style named diagnostics for every unprovable factor. Task 3's cutover
  deletes the conversion gate, after which the classified set is the moved set.

- [ ] **Step 0: Fix UV channel ordering in AlphaSemanticsResolver**

In `AlphaSemanticsResolver.cs:404-421`, channel selection must occur before
`AffineUvTransform.TryTransform`:

```csharp
if (_mapping.Channel != 0)
{
    if (!triangle.TryGetUvSet(_mapping.Channel,
            out var channelA, out var channelB,
            out var channelC))
    {
        return TriangleAlphaOutcome.Unknown;
    }

    transformed = TriangleAlphaInput.WithUv0(
        triangle.Position0, triangle.Position1,
        triangle.Position2, channelA, channelB, channelC);
}

if (_mapping.Scale.x != 1f ||
    _mapping.Scale.y != 1f ||
    _mapping.Offset.x != 0f ||
    _mapping.Offset.y != 0f)
{
    if (!AffineUvTransform.TryTransform(
            _mapping, transformed, out transformed, out envelope))
    {
        return TriangleAlphaOutcome.Unknown;
    }
}
```

Add a test in `AlphaSemanticsResolverTests.cs` verifying that a mapping on
channel 1 with scale (2, 2) and offset (0.5, 0.25) transforms channel 1
coordinates and calculates the envelope from channel 1.

- [ ] **Step 1: Write the failing semantic tests**

Create `PoiyomiOutlineAlphaSemanticsTests.cs` on the fixture base, using
the `Interpret` seam. `OutlineEnabled` MUST set `_AlphaForceOpaque` to 0f,
because the stand-in shader defaults `_AlphaForceOpaque` to 1f:

```csharp
public sealed class PoiyomiOutlineAlphaSemanticsTests : PoiyomiFixtureTestBase
{
    private static Material OutlineEnabled()
    {
        var material = NewFixtureMaterial();
        material.SetFloat("_AlphaForceOpaque", 0f);
        material.SetFloat("_EnableOutlines", 1f);
        return material;
    }

    [Test]
    public void OutlinesDisabled_HostileState_ChangesNothing()
    {
        var material = NewFixtureMaterial();
        material.SetFloat("_OutlineAlphaDistanceFade", 1f);
        material.SetFloat("_OutlineALColorEnabled", 1f);
        material.SetColor("_LineColor", new Color(1f, 1f, 1f, 0.25f));

        var result = Interpret(material);

        AssertUnsupportedOutputIsAbsent(result);
    }

    [Test]
    public void OutlinesEnabled_ProvenInputs_AlphaStaysComplete()
    {
        var material = OutlineEnabled();

        var result = Interpret(material);

        AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
    }

    [Test]
    public void OutlinesEnabled_LineColorAlphaBelowOne_IsUnknownWithDiagnostic()
    {
        var material = OutlineEnabled();
        material.SetColor("_LineColor", new Color(1f, 1f, 1f, 0.5f));

        AssertUnsupportedOutput(
            Interpret(material),
            PoiyomiSemanticOutput.Alpha,
            PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
            "_LineColor");
    }

    [Test]
    public void OutlinesEnabled_DistanceFadeEnabled_IsUnknownWithDiagnostic()
    {
        var material = OutlineEnabled();
        material.SetFloat("_OutlineAlphaDistanceFade", 1f);

        AssertUnsupportedOutput(
            Interpret(material),
            PoiyomiSemanticOutput.Alpha,
            PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
            "_OutlineAlphaDistanceFade");
    }

    [Test]
    public void OutlinesEnabled_AudioLinkColorEnabled_IsUnknownWithDiagnostic()
    {
        var material = OutlineEnabled();
        material.SetFloat("_OutlineALColorEnabled", 1f);

        AssertUnsupportedOutput(
            Interpret(material),
            PoiyomiSemanticOutput.Alpha,
            PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
            "_OutlineALColorEnabled");
    }

    [Test]
    public void OutlinesEnabled_LineColorAlphaNaN_IsUnknownWithDiagnostic()
    {
        // --- Falsifier: a non-finite line color must refuse, not pass
        // through as a proven factor. ---
        var material = OutlineEnabled();
        material.SetColor("_LineColor", new Color(1f, 1f, 1f, float.NaN));

        AssertUnsupportedOutput(
            Interpret(material),
            PoiyomiSemanticOutput.Alpha,
            PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
            "_LineColor");
    }

    [Test]
    public void OutlinesEnabled_UnprovenTexture_NonzeroPan_IsUnknownWithDiagnostic()
    {
        var material = OutlineEnabled();
        material.SetVector("_OutlineTexturePan", new Vector4(0f, 0.5f, 0f, 0f));
        material.SetTexture("_OutlineTexture", ImportTexture("outline"));

        AssertUnsupportedOutput(
            Interpret(material),
            PoiyomiSemanticOutput.Alpha,
            PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
            "_OutlineTexturePan");
    }

    [Test]
    public void OutlinesEnabled_UnprovenTexture_PanOnOneAxisOnly_IsUnknownWithDiagnostic()
    {
        // --- Falsifier: the supported-form gate tests both pan
        // components. ---
        var material = OutlineEnabled();
        material.SetVector("_OutlineTexturePan", new Vector4(0.5f, 0f, 0f, 0f));
        material.SetTexture("_OutlineTexture", ImportTexture("outline"));

        AssertUnsupportedOutput(
            Interpret(material),
            PoiyomiSemanticOutput.Alpha,
            PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
            "_OutlineTexturePan");
    }

    [Test]
    public void OutlinesEnabled_WholeTextureProven_AlphaStaysComplete()
    {
        var material = OutlineEnabled();
        material.SetTexture("_OutlineTexture", ImportTexture(
            "outlineSolid",
            importer => importer.alphaSource =
                TextureImporterAlphaSource.None,
            sourceHasAlpha: false));

        AssertOutputComplete(Interpret(material),
            PoiyomiSemanticOutput.Alpha);
    }

    [Test]
    public void OutlinesEnabled_UnprovenTexture_ChainAdmitted_AlphaIsTheMappedTerm()
    {
        var material = OutlineEnabled();
        material.SetTexture("_OutlineTexture", ImportTexture("outline"));

        var result = Interpret(material);

        // The mapped term is complete as a term: the per-triangle verdict
        // happens in the classifier over this value.
        AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
    }

    [Test]
    public void OutlinesEnabled_OverrideAlphaEnabled_StillRequiresBaseAlpha()
    {
        // --- Falsifier: _OutlineOverrideAlpha cannot replace the base chain.
        // Even when override alpha is 1, a transparent base color keeps the
        // material unproven/transparent. ---
        var material = OutlineEnabled();
        material.SetFloat("_OutlineOverrideAlpha", 1f);
        material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));

        var result = Interpret(material);
        AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
        var value = result.Semantics.Alpha.GetCompleteValue();
        Assert.That(value.Kind, Is.Not.EqualTo(ScalarSemanticValueKind.Constant));
    }

    [Test]
    public void OutlinesEnabled_OverrideAlphaNaN_IsUnknownWithDiagnostic()
    {
        var material = OutlineEnabled();
        material.SetFloat("_OutlineOverrideAlpha", float.NaN);

        AssertUnsupportedOutput(
            Interpret(material),
            PoiyomiSemanticOutput.Alpha,
            PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
            "_OutlineOverrideAlpha");
    }

    [Test]
    public void OutlinesEnabled_WithReplaceMask_EvaluatesOutlineFactor()
    {
        // --- Falsifier: Replace alpha mask mode must not bypass outline
        // evaluation. ---
        var material = OutlineEnabled();
        material.SetFloat("_MainAlphaMaskMode", 1f); // Replace mode
        material.SetColor("_LineColor", new Color(1f, 1f, 1f, 0.5f));

        AssertUnsupportedOutput(
            Interpret(material),
            PoiyomiSemanticOutput.Alpha,
            PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
            "_LineColor");
    }
}
```

`AssertUnsupportedOutputIsAbsent` is the file-local guard asserting the
Alpha output carries no diagnostic; write it as the negation of the base's
`AssertUnsupportedOutput` match. The unproven-texture rows use the alpha-
200 fixture texture, which is not whole-texture proven; the chain defaults
(UV0, zero pan, finite affine) are admitted.

- [ ] **Step 2: Run the new file, verify RED**

Run group
`Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiOutlineAlphaSemanticsTests`.
Expected: every outline-enabled row fails - today's semantics ignore the
outline inputs, so the diagnostic rows find no diagnostic and the hostile
rows show no difference. `OutlinesDisabled_HostileState_ChangesNothing`
passes on first run and is recorded as characterization of the guard.
- [ ] **Step 3: Implement the outline terms**

In the alpha output construction of `PoiyomiMaterialSemantics.cs`, evaluate
the base alpha across all branches first:
- If `maskReplacement != null`, `baseAlpha = maskReplacement`.
- Else, `baseAlpha = (maskMultiplier != null ? multiplied : baseChain)`.

For non-forced materials (`_AlphaForceOpaque == 0`), outline evaluation
runs across all paths before the analyzer returns. If
`!evidence.TryGetScalar(OutlineEnabledProperty, out var enableOutlines) || !IsFinite(enableOutlines)`:
return `RecordUnknown<ScalarSemanticValue>(diagnostics, PoiyomiSemanticOutput.Alpha, PoiyomiSemanticDiagnosticCode.UnsupportedFeature, OutlineEnabledProperty);`.

If `enableOutlines <= 0f`: outlines are clipped and disabled, so return
`SemanticOutput<ScalarSemanticValue>.Complete(baseAlpha)`.

If `enableOutlines > 0f`: evaluate the outline factor:
1. Override alpha: `evidence.TryGetScalar(OutlineOverrideAlphaProperty, out var overrideAlpha) && IsFinite(overrideAlpha)`, else `RecordUnknown(..., UnsupportedFeature, OutlineOverrideAlphaProperty)`.
2. Line color: `evidence.TryGetColor(LineColorProperty, out var color) && IsFinite(color.a) && color.a == 1f`, else `RecordUnknown(..., UnsupportedFeature, LineColorProperty)`.
3. Fade: `evidence.TryGetScalar(OutlineAlphaDistanceFadeProperty, out var fade) && IsFinite(fade) && fade == 0f`, else `RecordUnknown(..., UnsupportedFeature, OutlineAlphaDistanceFadeProperty)`.
4. AudioLink: `evidence.TryGetScalar(OutlineAudioLinkColorProperty, out var alColor) && IsFinite(alColor) && alColor == 0f`, else `RecordUnknown(..., UnsupportedFeature, OutlineAudioLinkColorProperty)`.
5. Texture: `evidence.TryGetTexture(OutlineTextureProperty, out var outlineTexture)`. Unassigned: factor is `ScalarSemanticValue.Constant(1f)`. Assigned: `outlineTexture.Texture.SampledAlphaIsProvenOne` is the stage-1 fast path (constant one). Otherwise the chain must fit the supported form: outline UV scalar an exact integer 0 to 3, captured pan vector exactly zero on both axes, finite scale and offset, captured alpha channel (`HasAlphaChannel` with `AlphaCaptureRefusal == None`), and main texture sampling available (`evidence.TryGetTexture(MainTextureProperty, out var main) && main.Texture != null && main.Texture.HasSampling`). The factor is `ScalarSemanticValue.Texture(new TextureSample(outlineTexture.Texture.SourceIdentity, mapping, main.Texture.Sampling), TextureChannel.Alpha)`. Each failed form records its named diagnostic on the offending property (`_OutlineTextureUV`, `_OutlineTexturePan`, or `_OutlineTexture`).
6. Compose: fold `baseAlpha` and the outline factor through `ScalarProductFold.Fold(baseAlpha, outlineFactor, OutlineEnabledProperty, property => AddDiagnostic(diagnostics, PoiyomiSemanticOutput.Alpha, PoiyomiSemanticDiagnosticCode.UnsupportedFeature, property), threadMaps: true)`. If fold returns null, return `SemanticOutput<ScalarSemanticValue>.Unknown()`. Otherwise return `SemanticOutput<ScalarSemanticValue>.Complete(folded)`.

Add feature-label map entries for `_OutlineOverrideAlpha`, `_LineColor`,
`_OutlineTexture`, `_OutlineAlphaDistanceFade`, `_OutlineALColorEnabled`, and
`_OutlineTextureUV` beside the existing map (`:983-996`), so the
diagnostic text names the feature instead of "a shader feature".

- [ ] **Step 4: Run the semantics groups, verify GREEN**

Run the new group, group
`Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiAlphaMaskTests`, and
the full `Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi` group. Expected:
all green - the mask rows prove the composition did not disturb the
existing terms, and every outline-disabled row is untouched. Record the
counts.

- [ ] **Step 5: Commit (only on explicit session authorization)**

```bash
  Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs \
  Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOutlineAlphaSemanticsTests.cs
git commit -m "feat(poiyomi): compose the outline alpha factor in the alpha semantics"
```

---

### Task 3: The conversion cutover

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`
  (enum `:40-53`, `EnableOutlinesProperty` `:211-219`, step 4 `:316-323`,
  `BuildConversionSchema` `:592-608`, `ConversionRequiredSchemaProperties`
  `:231-233`, `GatherConversionSourceEvidence` `:577-592`,
  `ConversionEvidenceRequest` `:252-261`)
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`
  (literal schema array `:25-40`, outline refusal rows `:347-360`,
  `:479-514`)
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationSplitTests.cs`
  (`WhollyOpaqueSurvivesWhileItsSplitSiblingRefuses` `:636-710`, new
  sibling tests)

**Interfaces:**
- Consumes: Task 2's semantics, which already prove or refuse the outline
  per triangle.
- Produces: `ConversionSchema` of 23 scalars; `OutlinesEnabled`,
  `EnableOutlinesProperty`, and the step-4 gate deleted with no
  replacement; the conversion request texture- and color-free again. No
  later task consumes any outline symbol from the conversion layer - the
  Task 4 falsifier pins that.

- [ ] **Step 1: Write the failing cutover tests**

Update the literal schema array to the 23 recipe properties (drop the
`"_EnableOutlines"` entry and the two Task 1 scalars never added to it -
the array returns to the committed 24 minus `_EnableOutlines`) and the
count assertion to 23. Add the grep-level falsifier beside the request
tests, so the duplication cannot silently return:

```csharp
[Test]
public void ConversionEvidenceRequest_CarriesNoOutlineSymbol()
{
    var request = PoiyomiOpaqueConversion.ConversionEvidenceRequest;

    Assert.That(
        request.ScalarProperties,
        Has.No.Member("_EnableOutlines"));
    Assert.That(
        request.ScalarProperties,
        Has.No.Member("_OutlineTextureUV"));
    Assert.That(
        request.ScalarProperties,
        Has.No.Member("_OutlineAlphaDistanceFade"));
    Assert.That(
        request.ScalarProperties,
        Has.No.Member("_OutlineALColorEnabled"));
    Assert.That(request.ColorProperties, Is.Empty);
    Assert.That(request.TextureProperties, Is.Empty);
}
```

Add the end-to-end rows in
`AlphaSeparationSplitTests.cs`, mirroring
`WhollyOpaqueSurvivesWhileItsSplitSiblingRefuses` line for line with the
differences shown:

```csharp
private Texture2D ImportSplitHollowTexture(string name)
{
    var pixels = new Color32[16];
    for (var i = 0; i < pixels.Length; i++)
    {
        pixels[i] = new Color32(0, 0, 0, 0);
    }

    return TestTextureImport.WritePng(
        SplitFolder + "/" + name + ".png",
        4,
        4,
        pixels,
        importer =>
        {
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
        },
        alpha: true);
}

[Test]
public void OutlinesOnSplitConvertsWhenTheOutlineIsProven()
{
    using var assets = new OverrideTemporaryDirectoryScope(null);
    var root = new GameObject("AMUSE split outlines converts");
    root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
    FixtureProofScope.PinAllSizes(root);
    AmusePlatformFinishState state = null;
    try
    {
        EnsureSplitFolder();
        try
        {
            var texture = Track(ImportSplitAlphaTexture("splitOutlineProven"));
            var opaque = Track(VerifiedOpaqueMaterial());
            var splitOutlines = Track(SplitAlphaMaterial(texture));
            splitOutlines.SetFloat("_EnableOutlines", 1f);
            var sourceMesh = Track(CreateOpaqueAndSplitSourceMesh());
            var renderer = AddRenderer(
                root, "body", sourceMesh, opaque, splitOutlines);

            var context = AvatarProcessor.ProcessAvatar(
                root, AlphaSeparationApplyTests.ApplyTestPlatform.Instance);
            state = context.GetState<AmusePlatformFinishState>();

            Assert.That(state.AnalyzedRendererCount, Is.EqualTo(1));
            Assert.That(
                state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal.OpaqueConversionRefused),
                Is.Zero, "a proven outline no longer refuses the split slot");
            Assert.That(renderer.sharedMesh.subMeshCount, Is.EqualTo(3));
            Assert.That(state.AppliedOpaqueTriangleCount, Is.EqualTo(2));
        }
        finally
        {
            DeleteSplitFolder();
        }
    }
    finally
    {
        state?.Dispose();
        UnityEngine.Object.DestroyImmediate(root);
    }
}

[Test]
public void OutlinesHoleyTextureMovesNothingButRefusesNothing()
{
    using var assets = new OverrideTemporaryDirectoryScope(null);
    var root = new GameObject("AMUSE split outlines holey");
    root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
    FixtureProofScope.PinAllSizes(root);
    AmusePlatformFinishState state = null;
    try
    {
        EnsureSplitFolder();
        try
        {
            var texture = Track(ImportSplitAlphaTexture("splitBase"));
            var outlineTexture = Track(ImportSplitHollowTexture("splitOutlineHoley"));
            var opaque = Track(VerifiedOpaqueMaterial());
            var splitOutlines = Track(SplitAlphaMaterial(texture));
            splitOutlines.SetFloat("_EnableOutlines", 1f);
            splitOutlines.SetTexture("_OutlineTexture", outlineTexture);
            var sourceMesh = Track(CreateOpaqueAndSplitSourceMesh());
            var renderer = AddRenderer(
                root, "body", sourceMesh, opaque, splitOutlines);

            var context = AvatarProcessor.ProcessAvatar(
                root, AlphaSeparationApplyTests.ApplyTestPlatform.Instance);
            state = context.GetState<AmusePlatformFinishState>();

            Assert.That(
                state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal.OpaqueConversionRefused),
                Is.Zero, "an empty outline intersection is the contract, not a refusal");
            Assert.That(state.AppliedOpaqueTriangleCount, Is.EqualTo(1),
                "only the wholly opaque sibling moves");
            Assert.That(renderer.sharedMesh.subMeshCount, Is.EqualTo(2),
                "the outlines slot keeps its original submesh");
            Assert.That(
                renderer.sharedMaterials[1],
                Is.SameAs(splitOutlines),
                "the outlines slot's material must be untouched");
        }
        finally
        {
            DeleteSplitFolder();
        }
    }
    finally
    {
        state?.Dispose();
        UnityEngine.Object.DestroyImmediate(root);
    }
}
```

Swap the sibling test's refusal trigger from the outlines arm to a
conversion arm that still exists:

```csharp
var splitRefused = Track(SplitAlphaMaterial(texture));
splitRefused.SetFloat("_AlphaToCoverage", 1f);
```

with its message updated to name the coverage arm.

- [ ] **Step 2: Run the split group, verify RED**

Run group
`Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationSplitTests`. Expected:
both new tests fail while the gate still refuses (`OpaqueConversionRefused`
count 1 and applied count 1), and the swapped trigger still refuses, which
keeps the sibling test green. Record the counts.

- [ ] **Step 3: Implement the cutover**

Delete, with no replacement: the step-4 outline gate and its comment
(`:316-323`), the `EnableOutlinesProperty` constant and its doc
(`:211-219`), and the `OutlinesEnabled` enum member (`:44`). Revert
`BuildConversionSchema` to the recipe only:

```csharp
private static string[] BuildConversionSchema()
{
    var schema = new string[CanonicalOpaqueTuple.Length];
    for (var index = 0; index < CanonicalOpaqueTuple.Length; index++)
    {
        schema[index] = CanonicalOpaqueTuple[index].Property;
    }

    return schema;
}
```

Point `ConversionRequiredSchemaProperties` and
`GatherConversionSourceEvidence` at that 23-name array, and remove the
color and texture entries from `ConversionEvidenceRequest` so it carries
scalars only. Delete the outline refusal rows in
`PoiyomiOpaqueConversionTests.cs` - `RefusedEvaluation_LeavesTheSourceMaterialUntouched`
keeps its unchanged-source guarantee with the coverage trigger, and
`EnabledOutlines_RefuseConversion` and
`EnabledOutlines_RefuseEvenWhenBaseAlphaIsExactlyOne` are deleted: their
semantic counterparts live in the Task 2 file. Keep
`AlreadyOpaque_EvenWithOutlinesEnabled` and
`AlreadyOpaque_EvenWithNonFiniteEnableOutlines` - the short-circuit
precedes every outline question, and the canonical read-back no longer
reads `_EnableOutlines` at all, so both stay green.

- [ ] **Step 4: Run the cutover groups, verify GREEN**

Run groups
`Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationSplitTests`,
`Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiOpaqueConversionTests`,
and `Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationPreparationTests`.
Expected: all green. Record the counts.

- [ ] **Step 5: Commit (only on explicit session authorization)**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationSplitTests.cs
git commit -m "refactor(poiyomi): cut the outline machinery from conversion"
```

---

### Task 4: The neighbor sweep

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`
  (parameterized binding rows `:186-198`)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`
  (capture-schema rows `:1242-1244`, relationship rows `:2249-2253`)

**Interfaces:**
- Consumes: Tasks 1-3's request shapes. No production change in this task.

- [ ] **Step 1: Fix the binding and capture rows that name outlines**

`_EnableOutlines` is alpha-request state now, so the conversion-relevance
rows change meaning. In
`ConversionOnlyState_IsIrrelevantToAlphaButRelevantToConversion`, remove
the `material._EnableOutlines` TestCase (it is no longer conversion-only)
and keep `material._ZWrite`. In the capture rows at `:1242-1244` and the
derived relationship rows at `:2249-2253`, `_EnableOutlines` rides the
combined capture request through the alpha side, so the containment rows
stay green; the conversion-only derivation now yields one fewer name.
Run the groups and fix any literally stated list to the request-derived
set, recording each edit.

- [ ] **Step 2: Run the sweep groups**

Run groups `Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi`,
`Alrauna.Amuse.Tests.Editor.Host.UnityAnimationEvidenceCaptureTests`,
`Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationPreparationTests`,
`Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationApplyTests`, and
`Alrauna.Amuse.Tests.Editor.Semantics.UnityMaterialSemanticsTests`.
Expected: all green. The conversion-request texture emptiness pin in
`UnityMaterialSemanticsTests.cs:412-416` returns to its committed form and
must pass untouched - the outline texture now lives in the alpha request,
which is the shape the pin originally guarded. The report-token rows in
the preparation group prove the remaining conversion refusals render
unchanged. A failure names a literal list; update it to the request-
derived set and record the edit.

- [ ] **Step 3: Commit (only on explicit session authorization)**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs
git commit -m "test(poiyomi): re-point the neighbor pins after the outline re-homing"
```

---

### Task 5: Full assemblies and final sweeps

**Files:**
- No production or test changes expected. Fixes belong to the task whose
  row failed.

- [ ] **Step 1: Run the full product assembly**

Run the full `Alrauna.Amuse.Tests.Editor` assembly through the Unity Test
Runner. Expected: every test passes; record the observed count. A filtered
run reporting 0 tests is a failure.

- [ ] **Step 2: Run the research assembly**

Run `Alrauna.Amuse.Research.Tests.Editor`. It does not consume the Poiyomi
alpha request, but the run is cheap and pins the claim. Expected: all
pass; record the observed count.

- [ ] **Step 3: Whitespace and tree checks**

```bash
git diff --check
git status --porcelain=v1
```

Expected: no whitespace errors; only the files this plan touched are
modified.

- [ ] **Step 4: Report**

Report: the branch and base commit, the RED observations per task with
counts, the GREEN observations per task with counts, the characterization
rows recorded as characterization, and the deferred arms (distance-fade
endpoint modeling, AudioLink color, derived outline UV modes) as dated in
spec section 6. The next authorized Census Lab build observes the corpus
projection end to end: all 9 observed outline materials convert when their
textures prove; polygons whose outline patch is unproven keep their
original material.
