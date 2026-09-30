# Investigation: duplicate refusal reports in the NDMF console

Date: 2026-09-29. Status: characterization complete. No code change yet.

Privacy note: this record is sanitized. It names no avatar, renderer, material,
texture, path, instance, port, or hash from the private corpus. It names
renderers by role and reports by slot index. The NDMF report keys, refusal
cause names, and entry counts are product data, so this record quotes them.

## Symptom

The NDMF console shows what reads as duplicate reports for a mesh that threw a
refusal. One entry says the slot got no proof. A second entry says a texture on
the same slot could not be read. Both name the same renderer and the same slot.
A build with refused meshes shows the pair. A clean build does not.

Every NDMF report also logs one Unity console warning, so the pair appears on
both surfaces.

## Observed evidence

Source: the retained NDMF report of the most recent play build of the reference
avatar, read from the Census Lab editor instance on 2026-09-29. The report held
10 entries, 6 of them from AMUSE.

The duplicated pair for the reported mesh, slot 1:

- `amuse.slotAnalysis.Refusal`, cause `AdmittedMaterialSemanticsUnknown`.
  Title: "AMUSE proved nothing for material slot 1 of renderer '...'".
- `amuse.texture.UnavailableCapture`, channel Alpha, reason `UnavailableCapture`.
  Title: "AMUSE could not read a material texture on slot 1."

Two other renderers refused too. The first held slot 0 and also produced the
renderer-level entry `amuse.renderer.AdmittedMaterialSemanticsUnknown`. The
third held slot 2 and produced only its slot entry. No entry appeared twice
with an identical key and identical arguments. The duplication is one fact
reported as two entries, not a replay of the same call.

## Why one refusal produces two entries

### Family 1: slot refusal plus texture refusal, same slot (observed)

One texture capture refusal is the root fact. `UnityAlphaFieldEvidence.TryCapture`
refuses the alpha read and records the reason in
`CapturedTextureEvidence.AlphaCaptureRefusal`. The resolver then has no alpha
field, the material resolution refuses, and the slot lands unresolved. The
finish pass reports both halves of this one fact in the same renderer loop:

- `AmusePlatformFinishPlugin.cs`, slot loop: one `AmuseReports.SlotAnalysisRefusal`
  per unresolved slot.
- `AmusePlatformFinishPlugin.cs`, texture loop, a few lines later: one
  `AmuseReports.TextureCaptureRefusal` per capture refusal of the same slot.

The slot entry names the renderer and the material. The texture entry names the
property, the channel, and the refusal reason. Neither entry alone carries the
full explanation, and together they read as a duplicate.

Caveat: the retained report does not store the resolver failure detail, so the
link between this slot's `AdmittedMaterialSemanticsUnknown` and this capture
refusal rests on the code path above, not on the report alone.

### Family 2: consent declined reported twice (code proven, not observed here)

In `AmusePlatformFinishPlugin.cs`, the declined-consent branch calls
`ErrorReport.ReportError` with `amuse.consent.Declined` and then calls
`AmuseReports.ConsentDeclined`, which reports `amuse.consent.Declined` again
before the per-subject entries. One decline always produces two identical
entries.

### Family 3: avatar animation refusal reported twice (code proven)

`AmuseStructuralGraphCheck.Execute` runs as its own pass and reports
`AmuseReports.AvatarRefusal` when the enumerated graph refused. The finish pass
then reads the stored graph and reports the same refusal again at its own gate.
This holds for both the pass path and the inline fallback. One avatar animation
refusal always produces two entries.

## Related observation, judged not a duplicate

When every slot of a renderer refuses, the build reports each slot refusal and
one renderer-level entry. Aggregate and per-slot facts are different scopes, so
this stays. It is recorded here because a reader can also see it as redundant.

## Recommendation

One entry per refused slot, carrying the whole explanation:

- When a slot refused and its capture refusals explain the refusal, fold the
  texture facts (property, channel, reason) into the slot entry, in the style
  of the existing feature sentence. Report no standalone texture entry for
  that slot.
- When a slot resolved but its evidence carries capture refusals, keep the
  standalone texture entry. It is then a pure diagnostic.
- Delete the duplicate `amuse.consent.Declined` report in the declined branch.
- Report the avatar animation refusal at exactly one site.

## Test plan

The product test assembly can capture NDMF reports with
`ErrorReport.CaptureErrors`. RED: a test that asserts one entry per refused
slot with texture facts folded in fails on the current code, which produces
two entries. GREEN: the folded reporter. Family 2 and Family 3 each get a
count assertion that fails before and passes after. Tests run in the product
repo, never in the private Lab project.
