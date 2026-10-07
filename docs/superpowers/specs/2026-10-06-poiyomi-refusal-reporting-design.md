# Poiyomi refusal reporting fixes from the Census Lab audit

Privacy note: this design comes from a Census Lab investigation on
2026-10-06. The investigation names no avatar, renderer, material, or texture
from the private corpus, and this document repeats none. It names code by
repository-relative paths and public shader names only.

Date: 2026-10-06.
Branch: `fix/poiyomi-refusal-reporting`.
Base: `main` at `96b5705`.
Investigation: `docs/superpowers/investigations/2026-10-06-poiyomi-avatar-refusal-audit-investigation.md`.
Status on 2026-10-06: design awaiting implementation. Production code has
not changed.

## Problem

The audit built one Poiyomi-heavy private avatar several times and verified
every refusal against the live materials. Every refusal was fail-closed
correct, but the console text carried five defects:

1. Four `amuse.slotSeparation.OpaqueConversionRefused` entries rendered no
   offending material. The preparation emitter passes three arguments only
   (`AlphaSeparationPreparation.cs:428-431`), so `offendingMaterial` defaults
   to null, the registry resolve short-circuits
   (`RegisteredSourceIdentity.cs:20-22`), and no slot-separation template
   consumes the offender anyway (`AmuseReportStrings.cs:429-436`). The reader
   cannot tell which material refused.
2. The renderer-level locked-material title says AMUSE "cannot trace" the
   lock (`AmuseReportStrings.cs:172-173`). In the audited case the trace
   succeeded and the blocker was attestation of the traced original. The
   description's second disjunct and the hint are correct; the title names
   the wrong gate.
3. Five `amuse.renderer.MaterialDependencyClosureFailed` entries said only
   "AMUSE could not read this renderer's animations." The entry text carries
   no renderer name, and the closure fails in four ways
   (`CapturedAnimationEvidence.cs:8-14`) of which only one is fairly
   described by that sentence. After a play-mode run the context reference is
   dead, so text is the only durable identity.
4. Four `amuse.texture.UnsupportedFormat` entries named neither the format
   nor the texture, and the description claims the sampling triangles "stay
   on the original material" even though one audited slot with that entry
   converted through a route that needed no refused chain.
5. The Poiyomi `_AlphaMaskValue` refusal sentence says "a shader feature"
   because the property has no entry in the Poiyomi feature-label map
   (`PoiyomiMaterialSemantics.cs:976-991`). The lilToon side labels the same
   property "Alpha mask" (`LilToonMaterialSemantics.cs:638-640`).

## Approach decisions

### Decision one: pass the refused material and make the template consume it

Chosen: track the refused slot's live material through the conversion loop
and pass it as `offendingMaterial`. Extend the
`OpaqueConversionRefused` description to name substitution `{3}`. The emitter
already resolves registered build copies to authoring assets
(`AmuseReports.cs:236-243`), so transient-unlock clones name their source.

Rejected: deriving the offender from `renderer.sharedMaterials[slotIndex]`
at the emitter. Preparation works on admitted live materials, and the
refusing material is already in scope. Guessing by index can name the wrong
material in a multi-material slot.

Rejected: only passing the material without touching the template. The
substitution list would carry it, but no text and no template would show it.

### Decision two: the locked title names verification, not tracing

Chosen: reword the title to "whose original shader AMUSE has not verified",
and reorder the description to lead with the version case. The closed
refusal vocabulary stays one cause: both description cases share one remedy
path, and the hint already covers it.

Rejected: splitting `LockedPoiyomiOriginalShaderUnattested` into two refusal
values. That changes the refusal taxonomy, every switch over it, and the
locked-material identity flow, for a wording problem.

### Decision three: the closure entry names the renderer and the failure way

Chosen: `AmuseReports.RendererRefusal` gains two trailing optional
parameters, `rendererName` and `detail`, with the same null-fallback
discipline the slot-separation emitter uses. The finish pass passes the live
renderer name and, for closure refusals, one sentence naming the closure
failure way. A small mapper in `AmuseReports` turns each
`MaterialDependencyClosureFailure` into a plain sentence. The closure
description drops its false claim that an animation always swaps a material:
the unassigned-slot way has no animation at all.

Rejected: new `RendererAnalysisRefusal` values per closure way. The ways are
facts under one refusal, not new refusals, and the enum is a closed
compatibility surface.

Rejected: name-only. The way is what makes the title truthful; without it the
reader still cannot tell a read failure from an unassigned slot.

### Decision four: the texture entry names its format and texture, and stops claiming outcomes

Chosen: `TextureCaptureRefusal` gains two display strings, `FormatName` and
`TextureName`, filled at `BuildCaptureRefusals`
(`UnityMaterialEvidenceCapture.cs:1430-1466`) from the texture already in
scope. The `UnsupportedFormat` description names both. All three texture
descriptions replace the outcome claim with a proof-availability statement:
the capture is not usable as proof, and no sentence asserts where the
triangles landed, because a resolved slot's proof may not need the refused
chain. The re-import hint adds the admitted `DXT1`, which the investigation
found missing.

Rejected: plumbing the format through `CapturedTextureEvidence`. The record
needs display strings, not another evidence field. Rejected: deleting the
availability sentence entirely. The reader still needs to learn what was
lost.

### Decision five: Poiyomi labels the alpha mask properties

Chosen: add `_AlphaMaskValue`, `_AlphaMaskBlendStrength`, and
`_AlphaMaskInvert` to the Poiyomi `FeatureLabels` map with the label
"Alpha mask", matching the lilToon labeling of the same concepts.

Rejected: special-casing the sentence in `AmuseReports`. The label map is the
established mechanism and already serves both frontends.

## Design

### The conversion-refused slot names its material

In `AlphaSeparationPreparation`, the per-slot conversion loop already tracks
`lastConversionRefusal` (`:335`, `:392`). One parallel variable tracks the
material:

```csharp
var lastConversionRefusal = AlphaSeparationSlotRefusal.None;
Material lastRefusedMaterial = null;
```

Inside the `conversionRefusal != None` branch, beside the existing
assignment:

```csharp
lastConversionRefusal = conversionRefusal;
lastRefusedMaterial = live;
```

The single-material path breaks immediately after, so the failing material is
the last recorded one. The report call gains the offender:

```csharp
AmuseReports.SlotSeparationRefusal(
    target.Renderer,
    slotIndex,
    slotRefusal,
    offendingMaterial: lastRefusedMaterial);
```

The string table entry consumes it:

```csharp
["amuse.slotSeparation.OpaqueConversionRefused:description"] =
    "The opaque conversion refused one material ({3}) on slot {0} of " +
    "renderer '{2}'. Reason: {1}. The slot keeps its original material.",
```

Substitution `{3}` renders the material's name through the NDMF object
reference, after the emitter's registry resolve names the authoring asset for
registered clones.

### The locked-material title

```csharp
["amuse.renderer.LockedPoiyomiOriginalShaderUnattested"] =
    "This renderer holds a locked material whose original shader " +
    "AMUSE has not verified.",
["amuse.renderer.LockedPoiyomiOriginalShaderUnattested:description"] =
    "The material is locked. The shader version the lock recorded is " +
    "not a version AMUSE knows, or the lock records no original " +
    "shader. AMUSE cannot read a locked material, so it changed " +
    "nothing on this renderer.",
```

The hint stays as written: it already names the remedy.

### The closure entry

`RendererRefusal` grows two trailing optional parameters:

```csharp
internal static void RendererRefusal(
    Renderer renderer,
    RendererAnalysisRefusal cause,
    int meshSubMeshCount = -1,
    int materialSlotCount = -1,
    string rendererName = null,
    string detail = null)
```

The name falls back to the live renderer exactly as
`SlotSeparationRefusal` does (`AmuseReports.cs:226-235`). A new mapper owns
the way sentences:

```csharp
internal static string ClosureFailureSentence(
    MaterialDependencyClosureFailure failure)
{
    switch (failure)
    {
        case MaterialDependencyClosureFailure.MissingCurrentMaterial:
            return "The renderer has a material slot with no material " +
                   "assigned. ";
        case MaterialDependencyClosureFailure.SlotOutOfRange:
            return "An animation binds a material slot the renderer does " +
                   "not have. ";
        case MaterialDependencyClosureFailure.InvalidSwapValue:
            return "An animation swaps in something that is not a " +
                   "material. ";
        case MaterialDependencyClosureFailure.UnattestedMaterial:
            return "A material in the animation could not be captured. ";
        default:
            return "";
    }
}
```

The finish pass report site (`AmusePlatformFinishPlugin.cs:783-789`) passes
both, from the `evidence` local that is already in scope at `:637` or `:654`:

```csharp
AmuseReports.RendererRefusal(
    renderer,
    refusal,
    extractionMeshSubMeshCount,
    extractionMaterialSlotCount,
    renderer.gameObject.name,
    refusal == RendererAnalysisRefusal.MaterialDependencyClosureFailed
        ? AmuseReports.ClosureFailureSentence(evidence.ClosureFailure)
        : null);
```

The templates:

```csharp
["amuse.renderer.MaterialDependencyClosureFailed"] =
    "AMUSE could not prove the material animations of renderer '{2}'.",
["amuse.renderer.MaterialDependencyClosureFailed:description"] =
    "AMUSE cannot prove what this renderer shows when an animation " +
    "changes its materials. {3}The renderer keeps its original " +
    "materials.",
```

### The texture entry

The record gains two display strings, both optional so every existing
construction compiles:

```csharp
internal string FormatName { get; }
internal string TextureName { get; }
```

`BuildCaptureRefusals` fills them from the in-scope texture:

```csharp
var textureName = texture.name;
string formatName = null;
if (texture is Texture2D texture2D)
{
    formatName = texture2D.format.ToString();
}
```

The emitter passes both after the identity clause, and the
`UnsupportedFormat` description names them:

```csharp
["amuse.texture.UnsupportedFormat:description"] =
    "Texture property {1} (channel {2}) on material slot {0} uses the " +
    "storage format {5}, which is outside the formats AMUSE can prove. " +
    "The texture is '{6}'. AMUSE cannot use this capture as proof for " +
    "the triangles that sample it. The refusal reason is {3}, and the " +
    "texture {4}.",
```

The other two texture descriptions replace the outcome claim with the same
availability sentence, and the re-import hint adds `DXT1`:

```csharp
"... AMUSE cannot use this capture as proof for the triangles that " +
"sample it. ..."
```

The record keeps its discipline: the new fields are strings, so no live
Unity object crosses into report arguments.

### The Poiyomi feature labels

```csharp
["_AlphaMaskValue"] = "Alpha mask",
["_AlphaMaskBlendStrength"] = "Alpha mask",
["_AlphaMaskInvert"] = "Alpha mask",
```

The refusal sentence then reads "AMUSE has no proven rule for the alpha mask
(property _AlphaMaskValue), so it cannot prove alpha."

## Testing

Product tests run in the dev editor instance through the Test Runner,
EditMode mode. Tests never run in the Census Lab.

- Decision one gets a RED preparation test on the existing depth-policy
  arm fixture with depth change disallowed, which produces exactly one
  `OpaqueConversionRefused`. The test asserts the captured message names the
  arm's material. Today it does not, so the assertion fails.
- Decision two gets a RED reporter test: `RendererRefusal` with
  `LockedPoiyomiOriginalShaderUnattested` must render the verification
  wording and must not contain "cannot trace".
- Decision three gets RED reporter tests: the closure entry names the
  renderer and each failure-way sentence; the old wording is absent. A
  falsifier drives `ClosureFailureSentence` over all four ways.
- Decision four gets RED reporter tests: an `UnsupportedFormat` entry with
  the new record fields names the format and the texture, and no texture
  entry contains "stay on the original material". A construction test pins
  the new record fields.
- Decision five gets a RED unit test: `FeatureLabelFor("_AlphaMaskValue")`
  returns "Alpha mask", and the strength and invert properties likewise.
- The string-table test gains `Has` assertions for every changed key.
- Existing tests need no edits: no test pins the changed copy (verified by
  search on 2026-10-06).

After the focused runs, the full `Alrauna.Amuse.Tests.Editor` assembly runs
and observed counts are recorded. A filtered run that reports zero tests is a
failure.

## Non-goals

- The Poiyomi threshold-envelope parity for alpha-mask strength-value pairs.
  That is a classification coverage contract with its own dated design work.
- Admission of crunched BC1 formats, red channel or otherwise.
- Slot-scoping the three slot-local closure failure ways. That changes the
  refusal's blast radius, not its text.
- The summary wording ("analyzed" and "kept everything original"). The audit
  judged the summary honest by construction.
- NDMF's own console behavior, including one Unity console warning per entry.
- Any change to classification, capture, or the mutation path. This design
  changes only what the console says.
