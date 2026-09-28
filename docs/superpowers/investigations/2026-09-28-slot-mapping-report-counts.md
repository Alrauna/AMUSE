# Investigation: slot counts in the material slot mapping report

Date: 2026-09-28. Method: read-only code investigation of the current tree at
`main` `102e524`. No Census Lab data used. The only repo changes are the topic
branch and this note. This note covers product code only. It names no avatar,
renderer, material, or scene from any private project.

## 1. Question

A build report says: "This renderer's material slots do not match its mesh."
The request: the report must also state how many material slots the mesh
supports, and how many material slots the renderer component carries. What
must change, and what data is already at hand?

## 2. Verdict

**Small and contained.** Both counts exist exactly where the refusal is
decided, but the refusal record drops them: it carries the bare enum value
only. The change is new argument plumbing from the two refusal sites to the
report emitter, plus two sentences in the string table that gain number
placeholders. No classification logic changes. No refusal vocabulary changes.

## 3. Current flow

All paths are repo-relative under `Packages/com.alrauna.amuse/`.

- The string table holds the title, description, and hint for the report id
  `amuse.renderer.UnprovenMaterialSlotMapping`
  (`Editor/Build/AmuseReportStrings.cs:59-67`). The actual title text reads
  "its mesh", not "it's mesh".
- The comparison is `materialSlotCount != mesh.subMeshCount` in
  `MaterialSlotMappingRefusalFor`
  (`Editor/Host/UnityRendererAlphaAnalysis.cs:571-578`). The mesh side,
  `subMeshCount`, is the number of material slots the mesh supports. The
  renderer side is the length of the material slot array.
- The check runs at two sites:
  1. `HostStructuralRefusalFor` reads `renderer.sharedMaterials` and calls
     the helper (`Editor/Host/UnityRendererAlphaAnalysis.cs:300-307`). The
     build pass calls this directly for every renderer
     (`Editor/Build/AmusePlatformFinishPlugin.cs:558-566`).
  2. `Capture` computes the slot count from the captured admitted material
     slots when present, else from `renderer.sharedMaterials`
     (`Editor/Host/UnityRendererAlphaAnalysis.cs:350-368`). The build pass
     reaches this through `CaptureGeometry`
     (`Editor/Build/AmusePlatformFinishPlugin.cs:683-685`).
- Both sites return the refusal as a bare enum. The extraction record for a
  refused capture carries no counts
  (`Editor/Host/UnityRendererAlphaAnalysis.cs`, `Refused` factory).
- The emitter `AmuseReports.RendererRefusal(renderer, cause)` passes only the
  key, no arguments (`Editor/Build/AmuseReports.cs:308-318`). The localizer
  registration is at `Editor/Build/AmuseReports.cs:20-25`.
- Numbered placeholders are proven in this table: the texture reports and the
  slot analysis reports use `{0}` to `{4}` with arguments passed through
  `ErrorReport.ReportError` (`Editor/Build/AmuseReportStrings.cs:298-305`,
  `:383-389`).

## 4. Facts that shape the change

- The refusal covers both supported renderer types, `MeshRenderer` and
  `Skinned Mesh Renderer`. The new wording must say "renderer", not only
  "Skinned Mesh Renderer".
- At site 2 the slot count that produced the refusal is the count of the
  captured material slots, not a fresh read of the live renderer. A report
  that re-reads live state at emission time could disagree with the refusal
  it explains. The counts must travel with the refusal.
- `materialSlotCount` uses a `-1` sentinel when the slot array is unreadable
  (`Editor/Host/UnityRendererAlphaAnalysis.cs:353-357`). The wording or the
  plumbing must survive that sentinel without printing a nonsense number.
- The completeness test only checks that title, description, and hint keys
  exist for every refusal cause
  (`Tests/Editor/Build/AmuseReportStringsTests.cs:18-38`). Placeholders do
  not affect it. No test pins the current wording.
- A RED test belongs in `AmuseReportStringsTests`: format the mapping
  refusal description and hint with two sample counts and assert both counts
  appear. An implementation that edits the sentences without placeholders,
  or passes the counts in the wrong order, fails it.
- The string table's own rule requires short active sentences
  (`Editor/Build/AmuseReportStrings.cs:7-13`). The new sentences must follow
  it.

## 5. Options

- **A. Emitter re-reads live state.** Rejected. At site 2 the honest count is
  the captured one, and re-reading live state duplicates the evidence
  discipline the codebase already rejects.
- **B. Plumb the counts.** `MaterialSlotMappingRefusalFor` yields the two
  numbers; they ride the refusal to the emitter; the emitter passes them as
  report arguments; description and hint gain `{0}` and `{1}`. Recommended.
  The exact shape (out parameters, a small record, or fields on the refused
  extraction) is a design choice for the plan.
- **C. Separate second report for the counts.** Rejected. More keys, more
  plumbing, and the reader sees two entries for one cause.

## 6. Recommendation

Option B. Draft wording direction, subject to the plan: the description
states both numbers, mesh parts first, then renderer slots; the hint states
the same two numbers so the author knows which side to change. Stop line:
this note authorizes no production edit. Implementation needs a written
prompt with RED/GREEN evidence per the working discipline rules.
