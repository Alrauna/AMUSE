# Material Slot Mapping Report Counts Design

Date: 2026-09-28.
Branch: `report/material-slot-counts`.
Base: `main` at `102e524`.
Investigation: `docs/superpowers/investigations/2026-09-28-slot-mapping-report-counts.md`.
Status on 2026-09-28: design and plan written. No production code has changed on this branch.

Privacy note: this design covers product code only. The triggering report was observed in the
Census Lab console, and this document names no avatar, scene, renderer, material, texture, or
asset path from that project, and no instance names, hashes, ports, or machine paths.

## Problem

The build report says "This renderer's material slots do not match its mesh." It states that a
mismatch exists, but no numbers. The reader cannot tell whether the mesh has more parts than the
renderer has slots, or fewer, or by how much. The reader also cannot tell which side to change.

The mechanism, from the investigation record:

1. The comparison is `materialSlotCount != mesh.subMeshCount` in `MaterialSlotMappingRefusalFor`
   (`Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:571-578`). The mesh
   side, `subMeshCount`, is the number of material slots the mesh supports. The renderer side is
   the length of the material slot array.
2. The refusal travels as a bare enum. The extraction record for a refused capture carries no
   counts (`Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaSnapshot.cs:114-152`).
3. The emitter `AmuseReports.RendererRefusal(renderer, cause)` passes only the key, no arguments
   (`Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs:308-318`).
4. The string table holds a title, a description, and a hint with no placeholders
   (`Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:59-67`).

The refusal can cover a `MeshRenderer` as well as a `Skinned Mesh Renderer`, so the new wording
must stay renderer-generic.

## Goal

The mapping refusal report states how many material slots the mesh supports and how many
material slots the renderer carries, using the exact counts the refusal was computed from.

## Non-goals

- No change to refusal enum values, report keys, or emission sites as a set. The report family
  keeps one entry per refused renderer.
- No change to the title or the hint string. The description carries the numbers.
- No live re-read of renderer state at emission time. The counts ride the refusal.
- No change to classification, capture order, the Census schema, or the mutation path.
- No change to the other refusal causes' strings. Their templates gain no placeholders.

## Decision

The change has four parts: the string table, the emitter, the evidence record, and the wiring.

### 1. The string table

The description for `amuse.renderer.UnprovenMaterialSlotMapping` becomes:

```
The mesh supports {0}. The renderer has {1}. AMUSE only works when the numbers match.
```

The title and the hint keep their current text. The hint keeps no placeholders on purpose: a
count directive cannot stay truthful when a count is unknown. "Make the renderer carry an
unknown number of material slots" directs nobody. The description right above carries both
numbers, so the hint would add no information.

### 2. The emitter

`AmuseReports.RendererRefusal` gains two optional integers,
`meshSubMeshCount = -1` and `materialSlotCount = -1`, and always passes two pre-rendered
phrases from a private `SlotCountPhrase(int)`:

- A count below zero renders as "an unknown number of material slots".
- The count one renders as "1 material slot".
- Any other count renders as "N material slots".

The helper owns pluralization and the sentinel, so the template stays a plain sentence and
"1 material slots" can never render. Two arguments always flow, so the table never formats with
missing arguments. The other refusal causes' templates have no placeholders and ignore the extra
arguments; the slot reports already pass five arguments to short titles, so that path is proven
in this repository.

### 3. The evidence record

`UnityRendererAlphaExtraction.Refused` gains two optional integers with the same defaults, and
the record exposes `MeshSubMeshCount` and `MaterialSlotCount`. The mapping check inside
`Capture` (`Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:358-361`)
passes `mesh.subMeshCount` and the slot count it just used. Every other refusal site keeps the
defaults, because those reports name no counts.

### 4. The wiring

- `HostStructuralRefusalFor` gains an overload with `out int meshSubMeshCount` and
  `out int materialSlotCount`. The one-argument form delegates to it. The counts stay minus one
  unless the mapping check itself ran, so a report never shows a count no decision used.
- Barrier loop site 1 (`Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:557-566`)
  calls the overload and passes both counts to the report.
- Barrier loop site 2 declares two locals before the capture block, reads them from the
  extraction when `CaptureGeometry` refuses
  (`Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:680-685`), and the
  shared refusal block (`:746-750`) passes them. The runtime-state refusal path leaves the
  locals at minus one, and the resolution causes' templates name no counts.

## Properties of the chosen shape

- The counts are the ones the refusal was computed from. At site 2 the honest slot count is the
  captured material slot count, not a fresh read of the live renderer. A report that re-read
  live state could disagree with the refusal it explains.
- The staged rollout degrades gracefully. After the emitter change but before the wiring, the
  report renders "The mesh supports an unknown number of material slots." That is honest, never
  a throw, and never a printed minus one. Every intermediate commit builds and tells the truth.
- The grammar holds for zero, one, many, and unknown, because the helper renders the noun
  phrase and the template holds only the verb.
- No existing test pins the current description text, so the wording change breaks nothing.

## Alternatives considered

- **Live re-read at emission.** Rejected. At site 2 the refusal's slot basis is the captured
  slot list, and a live re-read could disagree with the decision. This duplicates exactly what
  the evidence discipline already rejects.
- **Placeholders in the hint.** Rejected. The hint cannot phrase a count directive that stays
  true when a count is unknown, and the description above it already carries the numbers.
- **A second count-only report entry.** Rejected. The reader would see two entries for one
  cause, and the plumbing cost is the same.
- **Pluralization inside the template.** Rejected. Conditional text in the table would make the
  string table a program. The helper keeps the table plain English.

## Testing

Product tests run in the dev editor instance through the Test Runner, EditMode mode. Tests
never run in the Census Lab. A filtered run that reports zero tests is a failure, and observed
counts are recorded for every run. One new test file appears: `AmuseReportsRendererTests`,
which mirrors the fixture of `AmuseReportsSlotTests`.

Four layers, narrowest first:

1. **String table** (`AmuseReportStringsTests`): format the description with the two phrases and
   assert both sides appear. RED is a clean assert failure: the current text states no numbers.
2. **Emitter** (`AmuseReportsRendererTests`, through `ErrorReport.CaptureErrors`): the rendered
   message must carry both phrases on the correct sides, and the sentinel must render as the
   unknown phrase, never as "-1". The order falsifier uses the distinct counts three and one.
   The RED run for these tests is a compile failure, because the four-argument overload does not
   exist yet; the recorded compiler message is the RED evidence.
3. **Evidence** (`UnityRendererAlphaAnalysisTests`): a capture that refuses the mapping carries
   both counts in both directions, the host overload reports them, and a non-mapping refusal
   leaves them unrecorded. The RED run is a compile failure for the missing properties; the
   recorded compiler message is the RED evidence.
4. **Barrier path** (`AmusePlatformFinishPluginTests`): a fixture with a two-submesh mesh and
   one material runs the real barrier pass, captures the reports, and asserts the rendered
   message names two mesh slots and one renderer slot. This test compiles against existing APIs,
   so its RED is a clean assert failure against the interim "unknown" wording.

The completeness test (`EveryRendererRefusalCauseHasPlainEnglishStrings`) keeps passing, because
no key moves. No test pins the old wording. After the focused runs, the full
`Alrauna.Amuse.Tests.Editor` assembly runs, and the `Alrauna.Amuse.Research.Tests.Editor`
assembly runs because it consumes the same internals.

## Expected effect

Every mapping refusal report names both sides of the mismatch. The two common cases read:

- Fewer slots than parts: "The mesh supports 2 material slots. The renderer has 1 material
  slot. AMUSE only works when the numbers match."
- Surplus slots: "The mesh supports 1 material slot. The renderer has 2 material slots. AMUSE
  only works when the numbers match."

The author can see which side to change without opening the mesh.

## Risks

The behavior change is report text only, and only for one report id. No refusal decision, count,
severity, or mutation moves. The plausible wrong implementations are covered by falsifiers: a
description without placeholders, swapped arguments at the emitter, a raw sentinel in the text,
an emission site that drops the counts, and counts that name a state no decision used. The one
semantic subtlety, that site 2's slot basis is the captured list, is pinned by the evidence
tests reading the extraction record directly.
