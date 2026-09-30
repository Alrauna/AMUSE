# Refusal report deduplication for the NDMF console

Privacy note: This design comes from a Census Lab investigation on 2026-09-29.
The investigation names no avatar, renderer, material, or texture from the
private corpus, and this document repeats none of them. It names renderers by
role and code by repository-relative paths.

Date: 2026-09-29.
Branch: `fix/duplicate-refusal-reports`.
Base: `main` at `bff34a0`.
Investigation: `docs/superpowers/investigations/2026-09-29-ndmf-console-duplicate-refusal-reporting.md`.
Status on 2026-09-29: design awaiting implementation. Production code had not
changed.

## Problem

The NDMF console shows one refusal as several entries. A reader takes the
extra entries for duplicates. The investigation found three families.

Family one, observed in the Lab. A texture capture refuses. The proof then has
no alpha field, the slot lands unresolved, and the finish pass reports the same
root fact twice in one renderer loop: one slot entry that says the slot got no
proof, and one texture entry that says the capture refused. Both name the same
renderer and the same slot. The retained build report showed exactly this pair
for one slot of the reported mesh.

Family two, code proven. The declined-consent branch reports
`amuse.consent.Declined` inline and then calls `AmuseReports.ConsentDeclined`,
whose first act is reporting the same key again. One decline always produces
two identical entries.

Family three, code proven. `AmuseStructuralGraphCheck.Execute` reports the
avatar animation refusal when the enumerated graph refuses. The finish pass
then reads the stored graph at its own gate and reports the same refusal again.
This holds on the pass path and on the inline fallback path. One avatar
animation refusal always produces two entries.

Every NDMF entry also logs one Unity console warning, so each duplicate pair
appears on both surfaces.

## Approach decisions

### Decision one: one entry per refused slot, with the texture facts folded in

Approach A, chosen: the slot entry carries the whole explanation. The slot
entry already names the renderer, the slot, and the material. The capture
facts, the texture property, the channel, and the refusal reason, join its
evidence sentence. The standalone texture entry then reports only for slots
that resolved.

Approach B, rejected: keep both entries and reword the slot line to point at
the texture line. The reader still sees two entries for one fact.

Approach C, rejected: suppress the slot entry and enrich the texture entry with
the material name. After play mode ends, the build-copy renderer is destroyed,
the console object link dies, and the report text is the only place that still
names the renderer. The texture entry names only a slot index. The slot entry
is the one that must survive, because it keeps the renderer name and the
material.

### Decision two: the folded sentence rides the existing evidence slot

The slot description template already reserves one substitution for an evidence
sentence. Today it carries the shader feature sentence. The capture sentence
composes into the same substitution, so the template stays unchanged. One new
string-table entry carries the sentence shape, and the refusal reason rides as
text inside it, so no per-reason template family is needed.

Approach rejected: a new `AlphaUnknownKind` value for capture refusals. Capture
refusals are host facts about a texture read, not shader feature facts. The
value algebra stays clean.

### Decision three: consent reports through the helper only

`AmuseReports.ConsentDeclined` owns the consent vocabulary. It reports the
declined entry and the per-subject entries. The inline duplicate call in the
declined branch goes away. Approach rejected: delete the helper's first report
and keep the inline call. The helper belongs to the report vocabulary, and the
per-subject entries follow its declined entry.

### Decision four: the structural graph check stays the single avatar reporter

The check pass runs first, extension free, and reports the named refusal when
the graph refuses. The finish pass keeps recording
`state.AvatarRefusal = graph.Refusal` and keeps stopping. It stops reporting.
Both the pass path and the inline fallback then produce exactly one entry.

Approach rejected: report from the finish pass gate and silence the check pass.
The check pass is the earliest truthful reporter, and its comment already
promises the report.

### Decision five: the standalone texture entry survives for resolved slots

A resolved slot with capture refusals is unreachable in today's pipelines, but
the reporting rule stays general. When a slot resolved, its capture refusals
are a pure diagnostic, and the standalone entry is the only place they appear.
When a slot refused, the folded entry carries the facts, and the standalone
entry would repeat one fact twice. The no-alpha-channel diagnostic stays
unconditional in both cases, because it refuses nothing and names a likely
material misconfiguration, which is a different fact.

## Design

### The folded slot entry

`AmuseReports.SlotAnalysisRefusal` gains one trailing optional parameter:

```csharp
internal static void SlotAnalysisRefusal(
    Renderer renderer,
    int slotIndex,
    RendererAnalysisRefusal cause,
    string rendererName = null,
    CapturedAlphaMaterial offender = null,
    AlphaUnknownReason unknownReason = null,
    IReadOnlyList<TextureCaptureRefusal> captureRefusals = null)
```

The method composes the evidence sentence from the two sources and passes the
result as the fifth substitution. The description template
`amuse.slotAnalysis.Refusal:description` is unchanged:

```csharp
private static string CaptureSentence(
    IReadOnlyList<TextureCaptureRefusal> captureRefusals)
{
    if (captureRefusals == null || captureRefusals.Count == 0)
    {
        return "";
    }

    var sentences = new List<string>(captureRefusals.Count);
    foreach (var refusal in captureRefusals)
    {
        sentences.Add(string.Format(
            AmuseReportStrings.Get("amuse.slotAnalysis.CaptureRefused"),
            refusal.PropertyName,
            refusal.Channel.ToString(),
            refusal.Reason.ToString()));
    }

    return string.Join(" ", sentences);
}
```

The composition rule: either sentence alone wins, and both present join with
one space. An empty result keeps today's text exactly.

The new string-table entry:

```csharp
["amuse.slotAnalysis.CaptureRefused"] =
    "Texture property {0} (channel {1}) could not be captured. " +
    "Reason: {2}. The proof for this slot has no texture data.",
```

`TextureCaptureRefusal` is a closed record of strings and enums, so the folded
entry keeps the evidence discipline. No live Unity object crosses into the
report arguments.

### The finish pass renderer loop

The loop hoists the slot record construction above the slot report loop, passes
each refused slot's capture refusals into its entry, and gates the standalone
texture entry on the slot resolution:

```csharp
refusal = resolved.Refusal;
var slots = MaterialSlotsFor(evidence, rendererPath, rendererTypeName);
for (var slotIndex = 0;
     slotIndex < resolved.SlotResults.Length;
     slotIndex++)
{
    if (resolved.SlotResults[slotIndex].IsResolved)
    {
        continue;
    }

    AmuseReports.SlotAnalysisRefusal(
        renderer,
        slotIndex,
        resolved.SlotResults[slotIndex].Refusal,
        renderer.gameObject.name,
        resolved.SlotResults[slotIndex].Offender,
        resolved.SlotResults[slotIndex].UnknownReason,
        slots[slotIndex].CaptureRefusals);
}
```

The index alignment is structural. `ResolveRuntimeStates` builds
`SlotResults` from the same `MaterialSlotsFor` call on the same evidence, so
slot `i` of one is slot `i` of the other. A divergence is an invariant defect
and throws, which blocks the build as the correctness policy demands. The
hoisted call is the same read-only construction the loop already ran later in
the success path.

Inside the extraction branch, the texture loop keeps its shape and gains one
gate:

```csharp
for (var slotIndex = 0;
     slotIndex < slots.Count;
     slotIndex++)
{
    // A refused slot's own entry carries the capture facts. A
    // standalone texture entry for that slot would state one fact
    // twice, so it reports only for resolved slots.
    if (resolved.SlotResults[slotIndex].IsResolved)
    {
        foreach (var textureRefusal in slots[slotIndex].CaptureRefusals)
        {
            AmuseReports.TextureCaptureRefusal(
                renderer, slotIndex, textureRefusal);
        }
    }

    foreach (var propertyName in slots[slotIndex].NoAlphaChannelProperties)
    {
        AmuseReports.TextureAlphaMissing(renderer, slotIndex, propertyName);
    }
}
```

The inner `slots` declaration moves away. `resolved` is in scope at this
point in the same loop body.

### The consent branch

The declined branch shrinks to:

```csharp
state.ConsentDeclined = true;
AmuseReports.ConsentDeclined(subjects);
return;
```

### The avatar refusal gate

The gate keeps the state record and the stop, and drops the report call. Its
comment states the reporting contract: the structural graph check pass named
the cause, and this gate records the state and stops.

### Documentation

The README section "Reading the console reports" gains one sentence: when a
slot's refusal comes from a texture the route could not read, the slot's single
line names the texture property, the channel, and the reason. The XML doc
comments on `SlotAnalysisRefusal` and the texture loop state the fold and the
gate.

## Testing

Product tests run in the dev editor instance through the Test Runner, EditMode
mode. Tests never run in the Census Lab.

- Family one gets a RED integration test beside the existing capture-refusal
  fixture test. The fixture refuses one slot's `_MainTex` alpha capture and
  keeps a convertible sibling slot. The test asserts exactly one
  `amuse.slotAnalysis.Refusal` entry for the refused slot, that its message
  names the property, the channel, and the reason, and that no
  `amuse.texture.` entry exists. On the current code the entry lacks the facts
  and the texture entry exists, so both assertions fail. The falsifier is a
  fold that keeps only the first refusal, covered at reporter level with two
  refusal records.
- The reporter gains two contract tests through `ErrorReport.CaptureErrors`:
  one folded refusal names its facts, and two folded refusals both appear.
- Family two gets a RED count test beside the existing declined-consent test:
  the report holds exactly one `amuse.consent.Declined` entry. The current
  code produces two.
- Family three gets a RED count test beside the existing avatar refusal tests:
  the report holds exactly one `amuse.avatar.` entry, and
  `state.AvatarRefusal` stays set. The current code produces two.
- The string-table test asserts the new `amuse.slotAnalysis.CaptureRefused`
  key exists.
- Existing tests need no edits. The avatar refusal report test asserts
  `Has.Some`, so it stays green with one entry. The declined-consent test
  asserts state only. No existing test pins the duplicate counts.

After the focused runs, the full `Alrauna.Amuse.Tests.Editor` assembly runs,
and observed counts are recorded. A filtered run that reports zero tests is a
failure.

## Non-goals

- The renderer-level entry beside per-slot entries. Aggregate and per-slot
  facts are different scopes, and the investigation records this as intended.
- The per-slot repetition of one material on several slots. Documented as
  intended in the README.
- NDMF's own `[NDMF] Error Reported` warning mechanism. One warning per entry
  is NDMF behavior. The pair's second warning disappears because the pair
  becomes one entry.
- Any change to classification, capture, or the mutation path. This design
  changes only what the console says.
- An end-to-end fixture for a resolved slot with capture refusals. No pipeline
  produces one today. The gate branch stays defensive and is covered at
  reporter level.

## Correction during execution, 2026-09-29

Task 3's first RED run falsified one premise of this design. There are two
refusal classes, and they behave differently at slot scope.

Class one: the texture is an in-memory copy with no source identity, the known
upstream atlas replacement. The identity gate refuses with
`UnavailableCapture`, the proof loses its alpha field entirely, the slot lands
`SemanticsUnknown`, and the slot refuses. This class produces the observed
duplicate pair. The Task 3 fold test now uses this class: slot 0's `_MainTex`
is a runtime `Texture2D`, which the capture-level tests already prove refuses
with `UnavailableCapture`.

Class two: the texture is an imported asset whose format the capture route
refuses (`UnsupportedFormat`). The captured evidence keeps the texture
assignment, the resolver refuses with `MissingTextureEvidence`, and the slot
RESOLVES with Unknown triangle outcomes. The slot loop prints no slot entry
for this class, so no duplicate pair exists here. The original Task 3 fixture
used this class, which is why the RED state could not be reached.

Consequences, superseding the earlier text where they conflict:

- The Testing section's fold fixture is the class-one fixture above, and its
  assertions name `UnavailableCapture`.
- Decision five's resolved-slot branch is not unreachable. It is the live
  behavior of class two, and a new characterization test
  (`RefusedFormatCaptureKeepsTheStandaloneDiagnosticEntry`) covers it end to
  end. The Non-goals item "an end-to-end fixture for a resolved slot with
  capture refusals" is withdrawn.
- The Risks paragraph's "no pipeline produces that state today" is corrected:
  class two produces it, the diagnostic entry is the intended report for it,
  and the characterization test pins it.

Addition from the final whole-branch review, 2026-09-29: Decision one's folded
sentence carries FOUR facts, not three. The suppressed standalone entry named
the identity fact, and the class-one case is exactly the in-memory replacement
the identity fact signals. The capture sentence therefore also states whether
the texture has a source identity, and the reporter-level and fold tests
assert it. The standalone entry keeps its identity fact and its hint for
resolved slots, unchanged.

## Risks and limits

The fold indexes `slots` by `resolved.SlotResults` positions. The two arrays
come from one `MaterialSlotsFor` call on one evidence record, so a divergence
means a programming defect. Such a defect throws and blocks the build, which is
the intended failure mode under the correctness policy. If a future change
splits the slot bases, this design must revisit the fold. The resolved-slot
diagnostic branch has end-to-end coverage through the class-two characterization
test, and the reporter keeps its own tests. The fold's fixture relies on the
in-memory texture refusing at the identity gate, the behavior the capture-level
tests already pin. If a future change lets an in-memory texture capture, the
class-one fixture stops reaching the RED state, and the fold test must follow
the refusal to whatever class then produces the slot refusal. The folded
sentence names the refusal reason by its enum text, the same text the
standalone entry prints today, so no vocabulary is lost.
