# Report Naming Slimming Design

Date: 2026-10-06. Base: `main` at `96b5705`, branch
`fix/poiyomi-refusal-reporting`, review point: the uncommitted working
tree on that date.

Investigation:
`docs/superpowers/investigations/2026-10-06-report-naming-slimming-investigation.md`.

## 1. Overview

The branch loses about 40 lines. Five cuts apply. All five keep
behavior. The report text that a build emits is byte-identical before
and after. Three cuts rewrite test expression forms and keep every
assertion and every literal.

The cuts touch the report emitters, one Host capture helper, and four
test files. The classification path, the apply path, and the shader
frontends are untouched. The one Host edit derives the same
`formatName` value in a shorter form.

This design stacks on the branch's uncommitted work. The branch's own
dated pair,
`specs/2026-10-06-poiyomi-refusal-reporting-design.md` and
`plans/2026-10-06-poiyomi-refusal-reporting-plan.md`, is complete. If
the branch lands first, these cuts apply on the branch head.

## 2. Texture capture: the ternary form

`UnityMaterialEvidenceCapture.cs` lines 1441 to 1445 become:

```csharp
var formatName = texture is Texture2D texture2D
    ? texture2D.format.ToString()
    : null;
```

The `textureName` line above the block stays. The derived value is
identical for `Texture2D` and for any other `Texture` subclass. The
file already uses this exact ternary shape at four other places.

## 3. Renderer refusal: no renderer name parameter

### 3.1 The emitter

`AmuseReports.RendererRefusal` loses the `rendererName` parameter,
the four-line comment, and the fallback reassignment:

```csharp
internal static void RendererRefusal(
    Renderer renderer,
    RendererAnalysisRefusal cause,
    int meshSubMeshCount = -1,
    int materialSlotCount = -1,
    string detail = null)
{
    using (ErrorReport.WithContextObject(renderer))
    {
        ErrorReport.ReportError(
            Localizer,
            ErrorSeverity.Information,
            AmuseReportStrings.RendererKey(cause),
            SlotCountPhrase(meshSubMeshCount),
            SlotCountPhrase(materialSlotCount),
            renderer != null ? renderer.gameObject.name : null,
            string.IsNullOrEmpty(detail) ? "" : detail);
    }
}
```

The `{2}` substitution reads the live renderer's name exactly as the
deleted fallback did. A null renderer yields null, exactly as before.
The emitted text is byte-identical for both shapes. The
`string.IsNullOrEmpty(detail)` coercion stays, because a null
substitution renders as the literal `<missing>` in the report text.

### 3.2 The caller

`AmusePlatformFinishPlugin.cs` line 789 loses the name argument. The
detail expression shifts into the `detail` position:

```csharp
AmuseReports.RendererRefusal(
    renderer,
    refusal,
    extractionMeshSubMeshCount,
    extractionMaterialSlotCount,
    refusal ==
        RendererAnalysisRefusal
            .MaterialDependencyClosureFailed
        ? AmuseReports.ClosureFailureSentence(
            evidence.ClosureFailure)
        : null);
```

The caller at line 576 passes no optional arguments and needs no edit.

### 3.3 The guard test

`AmuseReportsRendererTests.ClosureRefusalNamesRendererAndFailureWay`
at line 100 drops the explicit name. The fixture root name
`"AMUSE renderer report"` flows through the emitter's own guard, so
the test then covers the exact path production uses:

```csharp
var errors = ErrorReport.CaptureErrors(() =>
    AmuseReports.RendererRefusal(
        _renderer,
        RendererAnalysisRefusal
            .MaterialDependencyClosureFailed,
        -1,
        -1,
        AmuseReports.ClosureFailureSentence(
            MaterialDependencyClosureFailure
                .InvalidSwapValue)));

Assert.That(errors, Has.Count.EqualTo(1));
var message = errors[0].TheError.ToMessage();

// After play mode the context reference is dead, so the
// text is the only durable identity carrier.
Assert.That(message, Does.Contain("'AMUSE renderer report'"));
Assert.That(message, Does.Contain("not a material"));
```

Both asserts keep their strength. The test still requires the entry
to name the renderer and the way the closure failed.

## 4. Slot tests: one refusal per reason

`AmuseReportsSlotTests.TextureEntriesNeverClaimWhereTrianglesLanded`
replaces its array of refusals with an array of reasons and builds
one refusal inside the existing loop:

```csharp
[Test]
public void TextureEntriesNeverClaimWhereTrianglesLanded()
{
    var reasons = new[]
    {
        TextureCaptureRefusalReason.UnavailableCapture,
        TextureCaptureRefusalReason.NonResidentMips,
        TextureCaptureRefusalReason.UnsupportedFormat,
    };

    foreach (var reason in reasons)
    {
        var refusal = new TextureCaptureRefusal(
            "_MainTex",
            true,
            default,
            TextureChannel.Alpha,
            reason,
            formatName: "DXT1Crunched",
            textureName: "synthetic atlas copy");

        var errors = ErrorReport.CaptureErrors(() =>
            AmuseReports.TextureCaptureRefusal(
                _renderer, 5, refusal));

        var message = errors[0].TheError.ToMessage();

        // A resolved slot's proof may not need the refused chain,
        // so the entry states the lost evidence, never the slot's
        // outcome. All three changed texture entries carry the
        // invariant.
        Assert.That(
            message, Does.Not.Contain("stay on the original material"));
        Assert.That(
            message, Does.Not.Contain("kept the affected triangles"));
        Assert.That(message, Does.Contain("cannot use this capture"));
    }
}
```

The loop runs three times, once per reason, exactly as the array of
refusals did. Every property argument, literal, comment, and assert
stays.

## 5. String tests: one key array

`AmuseReportStringsTests.RefusalNamingFixKeysExist` keeps the
rationale comment and every literal key. The seven keys move into one
array. The key is the assert message, so a failure still names the
key:

```csharp
[Test]
public void RefusalNamingFixKeysExist()
{
    // The per-cause loops cover the keys the key builders
    // produce. These literal checks pin the exact keys the
    // report naming fixes changed, so a table edit or a key
    // builder change cannot drop one silently.
    var keys = new[]
    {
        "amuse.slotSeparation.OpaqueConversionRefused:description",
        "amuse.renderer.LockedPoiyomiOriginalShaderUnattested",
        "amuse.renderer.LockedPoiyomiOriginalShaderUnattested:description",
        "amuse.renderer.MaterialDependencyClosureFailed",
        "amuse.renderer.MaterialDependencyClosureFailed:description",
        "amuse.texture.UnsupportedFormat:description",
        "amuse.texture.UnsupportedFormat:hint",
    };
    foreach (var key in keys)
    {
        Assert.That(AmuseReportStrings.Has(key), Is.True, key);
    }
}
```

The loop runs seven times, once per literal. The pin coverage is
identical.

## 6. Alpha mask tests: three rows

`PoiyomiAlphaMaskTests.AlphaMaskPropertiesCarryFeatureLabels` becomes
one parameterized body:

```csharp
[TestCase("_AlphaMaskValue")]
[TestCase("_AlphaMaskBlendStrength")]
[TestCase("_AlphaMaskInvert")]
public void AlphaMaskPropertiesCarryFeatureLabels(string propertyName)
{
    // The refusal sentence must name the feature, not "a shader
    // feature": the alpha mask is supported for other values, and
    // lilToon already labels the same concept "Alpha mask".
    Assert.That(
        PoiyomiMaterialSemantics.FeatureLabelFor(propertyName),
        Is.EqualTo("Alpha mask"));
}
```

Each row reports on its own, so the case count rises from 1 to 3. The
covered behavior is identical. The comment stays on the body.

## 7. Acceptance risks

- Report text: the contract is byte-identical output. The risk sits
  in the `{2}` substitution. Section 3.1 keeps the null shape and the
  non-null shape identical to the deleted fallback. The pre-existing
  tests `MappingRefusalReportCarriesBothSlotCounts` and
  `MappingRefusalWithUnknownCountStatesUnknownNotMinusOne` call the
  emitter without a name, so the guard path stays exercised.
- Case counts: section 6 raises the suite total by 2. The implementer
  records observed counts before and after, and treats the +2 as the
  expected value.
- No assertion may weaken. The three test cuts keep every literal and
  every assert message. The closure test swap in section 3.3 keeps
  both asserts at equal strength.
- A filtered run that reports 0 tests is a failure.

## 8. Relation to other records

The branch's own design and plan are complete and unapplied cuts do
not touch their contracts. The two earlier complexity review records
cover other files. No cut in this design overlaps a cut in any
earlier record. Each record speaks for its own date.
