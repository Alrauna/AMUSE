# Poiyomi alpha parity - design

## Privacy note

This record describes private Census Lab fixtures by role only. It names
no avatar, renderer, material, texture, mesh, or hierarchy. It records no
machine paths, no host names, no ports, and no instance names or hashes.
It records no asset identifiers (a GUID is an asset identifier inside
Unity). The designated locked denim garment fixture appears by role, and
its alpha facts appear as feature values, never as a per-slot table.
Every status claim carries its date. Property names, mode numbers, and
vendor line anchors are public facts of the Poiyomi Toon 9.3.64 shader
source. They are not private data.

## 1. Scope and authority

Date: 2026-09-21. This is the design stage for the Poiyomi alpha parity
work. It changes no production code and adds no test.

The authority is the committed note
`docs/superpowers/investigations/2026-09-21-poiyomi-liltoon-alpha-parity.md`.
Its roadmap (note section 8) fixes the slice order and the scope. The
owner decision of 2026-09-21 stands: the conversion may admit premultiply
under the policy, with the disclosure (note sections 6.2 and 9.1).

Before writing, this design re-checked every code claim of the note
against the committed branch on 2026-09-21. Section 2 records the result.
No claim was wrong. Section 2.1 records facts the note does not state
that shape the slices.

## 2. Verification record, 2026-09-21

Each point below was read in the committed code on 2026-09-21. File names
and type names are repository facts.

1. `_AlphaPremultiply` sits in `AlphaFeatureGates` in
   `PoiyomiMaterialSemantics.cs`. Note 6.1.
2. Conversion gate 5 refuses `PremultipliedAlphaEnabled` in
   `PoiyomiOpaqueConversion.cs`. Note 6.1 and 6.2.
3. `TryInterpretAlphaMask` reads mode 0 and mode 1 only. Mode 1 refuses
   an assigned mask with `UnsupportedFeature: _AlphaMask`. Every other
   mode refuses naming `_MainAlphaMaskMode`. Note rows 3, 4, and 6.
4. The alpha evidence request asks `TextureEvidenceKinds.None` for
   `_AlphaMask`. Note row 3.
5. The frontend reads neither `_AlphaMaskUV` nor `_AlphaMaskPan`. Note
   row 7 and gap E.
6. `_BSSEnabled` appears nowhere under the editor sources. Note 7.2.
7. `_ModeTwoPass` and `_AlphaForceOpaque2` appear nowhere under the
   editor sources. Note 7.1.
8. `ClassifyShaderName` routes only the plain Toon name
   `.poiyomi/Poiyomi Toon` to the Poiyomi family
   (`UnityMaterialSemantics.cs`). Note 7.1.
9. `TryVerifyPoiyomiIdentity` attests the Two Pass name against its own
   pinned identity facts. `LockedMaterialIdentity`
   .`PassesPinnedPoiyomiIdentity` reuses that same conjunction for the
   unlock precondition. Note 7.1. So a locked Two Pass original opens
   the unlock window as of 2026-09-21, and the unlocked clone then
   refuses as `AdmittedMaterialSemanticsUnknown`.
10. `_Cutoff` is absent from the Poiyomi alpha evidence request.
    Conversion gate 10 refuses a cutoff above 1. Note 6.6.
11. The depth gate admits `Less` beside `LEqual` under the opt-in
    policy, carries `DepthTestDivergence` on the eligibility result, and
    reports `amuse.slotSeparation.DepthTestDivergence`. Note rows 13
    and 14.
12. The lilToon mask machinery exists as the note describes:
    `LilToonAlphaMaskTermKind.MainUnchanged`, the admitted pairs
    (1, 0) and (1, value >= 1), the unassigned default, and the plain
    affine mask coordinate (`LilToonAlphaMaskSemantics.cs`). The named
    lilToon and Poiyomi tests exist under `Tests/Editor/Semantics/`.
13. `TextureEvidenceKinds` already declares `RedChannel`, and the red
    field route (`AlphaFieldKey.ForRed`) already serves the lilToon
    mask. Note 6.3.

### 2.1 Additions the note does not state

No note claim is wrong. Five facts shape the slices, so this design
records them.

1. `_AlphaPremultiply` also sits in `BaseColorFeatureGates`. Slice 2
   removes it from the alpha gate list only. The base-color gate stays
   closed on purpose. The vendor premultiply scales the color per pixel,
   so the base-color equation genuinely changes. Only the alpha value is
   untouched. The fixture is unaffected either way, because its slot
   consumes the alpha output only (note sections 5 and 9.5).
2. `CreateAlphaEvidenceRequest` unions the gate arrays into the scalar
   set. One array edit covers the gate and the evidence request for
   slices 1 and 2.
3. The coverage gates run before the forced-opaque short-circuit on the
   alpha path. A new coverage gate therefore protects the forced-opaque
   claim too. Slice 1 relies on this order.
4. `ConversionSchema` holds no `_BSSEnabled`. No conversion change is
   needed for slice 1. Conversion is consulted only for a material whose
   triangles already proved, and a material with an enabled module never
   proves a triangle.
5. The alpha capture arm already carries a declared shader cutoff
   concept on the texture request, and the lilToon cutout request
   declares `_Cutoff` as a captured theorem scalar riding that request.
   Slice 6 mirrors that pattern for Poiyomi.

## 3. Contract vocabulary

Each slice section states its contract surface with the same six words
the repository already uses.

- Evidence request: the material facts one capture reads
  (`MaterialEvidenceRequest`). A request change must state why each new
  fact is consumed, because unused evidence widens animation relevance.
- Gates: the closed zero-gate arrays and the explicit checks the
  frontend runs before any claim.
- Diagnostics: the closed `PoiyomiSemanticDiagnosticCode` vocabulary.
  Unsupported still means `UnsupportedFeature` naming one property.
- Conversion: the pinned recipe and `EvaluateVerifiedEligibility` in
  `PoiyomiOpaqueConversion.cs`.
- Report strings: the user-facing sentences in `AmuseReportStrings.cs`.
- Census: the research record of build outcomes. A slice that adds no
  refusal member and no family value needs no census change.

Closed refusal enums stay closed. A slice that needs a new named refusal
says so in its section. A slice that makes an existing member
unreachable says that too, and the plan decides the member's fate.

## 4. Slice 1: BeatSaber gate

The vendor toggle `_BSSEnabled` turns on a post-clip alpha writer,
`alpha = alpha * emission.z`, and rewrites the alpha blend pair (note
4.7). The frontend never reads the property as of 2026-09-21. An enabled
module can therefore sit inside a claimed exactly-one alpha. Every other
finding in the note errs on the safe side. This one does not. This slice
is a correctness fix, not a parity extension.

### Contract surface

- Evidence request: `_BSSEnabled` joins `AlphaCoverageGates`. The
  request union picks it up, so one edit covers both.
- Gates: the existing coverage zero-gate run on every alpha path. The
  forced-opaque short-circuit stays protected, because the coverage run
  precedes it.
- Diagnostics: `UnsupportedFeature` naming `_BSSEnabled`. No new
  diagnostic code. No new refusal member.
- Conversion: no change. `ConversionSchema` stays as it is.
- Report strings: no change.
- Census: no change.

### Proof obligation

A material with the module enabled never proves a triangle on any alpha
path, forced opaque included. The diagnostic names `_BSSEnabled`.

### Open design questions

1. Should conversion carry `_BSSEnabled` as defense in depth?
   Recommendation: no. The alpha gate refuses before any plan exists, so
   conversion never sees such a material. One load-bearing gate matches
   the note's scope.

## 5. Slice 2: premultiply alpha admission

The vendor premultiply scales the base color by `saturate(alpha)` in
three passes. The alpha value itself never changes (note 4.3). On a
triangle whose alpha chain is proven exactly one, the factor is exactly
one. The feature is an identity on exactly the triangle set that may
move. Today the alpha equation refuses the feature by name, so a
premultiply material never proves anything.

### Contract surface

- Evidence request: unchanged. `_AlphaPremultiply` already rides the
  scalar set.
- Gates: remove `_AlphaPremultiply` from `AlphaFeatureGates` only. Keep
  the `BaseColorFeatureGates` entry (section 2.1 point 1). Add a proof
  comment pinned to the three vendor sites the note cites.
- Diagnostics: the premultiply refusal disappears from the alpha
  output. No new member appears. Every other gate keeps refusing.
- Conversion: unchanged in this slice. Gate 5 keeps refusing until
  slice 7.
- Report strings: no change.
- Census: no change.

### Proof obligation

After the slice, a material with premultiply on proves its alpha exactly
when the same material with premultiply off proves it. No triangle moves
unless its alpha domain is exactly one. The per-triangle premise guards
every moved triangle, because the classification layer keeps that rule.

### Open design questions

1. None. The base-color scope is settled in section 2.1: the base-color
   gate stays closed, and no base-color parity work belongs to this
   roadmap.

## 6. Slice 3: mask replace with the admitted pair and invert

The vendor mask block samples `_AlphaMask.red` through the main sampler
at `uv[_AlphaMaskUV]` transformed by `_AlphaMask_ST`, panned by
`_AlphaMaskPan.xy`. The term is `saturate(r * strength + (invert ?
-value : value))`, followed by `1 - m` when invert holds (note 4.2). The
mask has no separate scale-offset property. Unity derives `_AlphaMask_ST`
from the texture assignment, so the existing `ScaleOffset` evidence kind
covers it. The lilToon mask machinery proves the same algebra and is the
parity target.

### Contract surface

- Evidence request: upgrade the `_AlphaMask` texture request from
  `TextureEvidenceKinds.None` to `ScaleOffset`, `SourceIdentity`, and
  `RedChannel`. These are exactly the three facts the interpretation
  consumes, mirroring the lilToon mask request.
- Gates: mode 1 with an assigned mask becomes interpreted. `_AlphaMaskUV`
  gates to an exact integer 0 to 3. `_AlphaMaskPan` gates to exact zero.
  The strength-value pair (1, 0) admits. The provably saturated pair
  (1, value >= 1) admits through the same proof shape lilToon uses, for
  invert off and invert on. The unassigned default stays as it is.
  Every other pair refuses and names its property, under the deferred
  threshold-envelope contract (note 9.2).
- Value shapes: invert off makes the replace term the red sample.
  Invert on makes it `saturate(1 - r)`, expressed through the existing
  saturating-difference shape.
- Diagnostics: unchanged vocabulary. Refusals name `_AlphaMaskUV`,
  `_AlphaMaskPan`, `_AlphaMaskBlendStrength`, `_AlphaMaskValue`, or
  `_AlphaMaskInvert`, in the declared diagnostic order.
- Capture, texture identity, classifier: no change. The red field route
  exists.
- Report strings: no change.
- Census: no change.

### Proof obligation

A bound replace mask proves exactly what the lilToon machinery proves
for the same inputs. A mask hole keeps its triangles unknown. A
wrap-seam-adjacent domain stays unproven under the wrap-blend rule. A
non-identity mask affine still proves.

### Open design questions

1. Landing order inside the slice. The smallest honest slice in note 6.3
   admits the pair (1, 0). The contract surface admits the saturated
   pair too, because its proof needs no threshold envelope. The plan may
   land both in one slice or land (1, 0) first. Recommendation: land
   both, because lilToon already proves the saturated pair and parity
   means same inputs, same proof.
2. The exact binary32 rounding argument for invert on at value 0. The
   term is `saturate(1 - r)` for red `r` in the unit range. The task
   must state the rounding argument in the proof comment, as the lilToon
   term does for its own constant cases.

## 7. Slice 4: mask multiply with the declared default

The declared vendor default of `_MainAlphaMaskMode` is 2 (multiply), not
off (note 7.3). A material that never touched the mask section carries
mode 2 and refused on 2026-09-21. This fact gives the slice the highest
reach after slice 3.

### Contract surface

- Gates: mode 2 admits for an unbound mask with the pair (1, 0). The
  chain stands unchanged, the role the lilToon term names
  `MainUnchanged`. Mode 2 admits for a bound mask with an admitted pair
  from slice 3. The red sample folds into the running chain through the
  exact product machinery. Modes 3 and 4 keep refusing and name
  `_MainAlphaMaskMode`.
- Evidence request: the slice 3 request already covers multiply.
- Diagnostics: unchanged vocabulary.
- Conversion, report strings, census: no change.

### Proof obligation

A fresh default-shaped material completes its alpha when the rest of the
material proves. A bound hole in multiply mode classifies the hole
unknown and never widens transparency. The product fold matches the
lilToon exact product term for the same inputs.

### Open design questions

1. None beyond slice 3. The product machinery and the admitted pairs are
   shared.

## 8. Slice 5: Two Pass routing and second-family gates

The Two Pass identity is attested but unrouted as of 2026-09-21
(note 7.1, verification point 9). Routing the unlocked name is a
coverage extension. The shipped refusal is safe as of 2026-09-21, so
nothing in this slice repairs an over-claim.

### Contract surface

- Routing: `ClassifyShaderName` learns the Two Pass name. The family
  value and the request shape are the first design decision below.
- Evidence request: the plain alpha request plus the second-family
  scalars. `_AlphaForceOpaque2` is the one the note names (note 4.4).
  The plain Toon request must not demand second-family scalars, so the
  plain material keeps capturing without them (note 7.1 falsifier).
- Gates: `_AlphaForceOpaque2` is interpreted beside `_AlphaForceOpaque`.
  A claim survives only when every family the material draws proves.
  The `_ModeTwoPass` cutout split of the second family belongs to
  slice 6.
- Conversion: the plain recipe was derived from the plain shader's own
  preset metadata (note 4.6). Applying it to a Two Pass material would
  rest on a premise nobody derived. This slice therefore keeps
  conversion refusing for Two Pass through an existing named refusal
  path. The second design decision below picks the shape.
- Report strings: no new strings under the recommended shape.
- Census: the plan task records whether the research census stores the
  family value. If it does, the new value is one documented census
  delta. Otherwise no census change.

### Proof obligation

A Two Pass material proves alpha only when both families prove. A
material with force-opaque on the first family and off on the second
never completes alpha. A plain Toon material still captures without the
second-family scalars.

### Open design questions

1. Routing shape. Option A: a new `CapturedAlphaMaterialFamily` member
   for Two Pass, with its own request mapping. The compiler then forces
   every family switch to answer, and conversion refuses through the
   existing `OpaqueConversionUnsupportedFamily` default with no new
   conversion vocabulary. Option B: reuse the Poiyomi family member and
   add a named conversion refusal for Two Pass. Recommendation: option
   A. It keeps the request shape per family, needs no new refusal
   member, and makes the unsupported conversion visible by name.
2. The second-family tint. The Two Pass second base reads
   `_TwoPassColor.a` where the plain base reads `_Color.a` (note 4.1).
   The request asks one color property as of 2026-09-21. The task must
   extend the Two Pass request with the second tint and make the
   second-family chain read it. The exact request shape is the task's
   first step.

## 9. Slice 6: cutout coverage split by cutoff

The vendor clips with `clip(alpha - _Cutoff)` in every pass and then
forces alpha to 1 in cutout mode (note 4.5). The alpha equation never
declares the cutoff as of 2026-09-21, so a cutout triangle with a chain
between the cutoff and 1 stays unproven. That is safe, and it is a
coverage gap against the lilToon cutout behavior. The classification
split route already exists (note 6.6).

### Contract surface

- Evidence request: `_Mode` and `_Cutoff` join the Poiyomi alpha
  capture. `_Cutoff` rides the main texture request as a captured
  theorem scalar, the pattern the lilToon cutout request uses. See the
  open question on request shape.
- Gates: the cutout preset switches the alpha field to the split route.
  Every other preset keeps the plain-clip rules and the exact-one rule
  the transparent family uses. The classification layer needs no
  change, because the split route exists.
- Conversion: no change. Gate 10 already bounds the cutoff at 1 for the
  canonical clone.
- Diagnostics: a cutoff that blocks the proof names `_Cutoff`, in the
  family's existing vocabulary.
- Report strings: no change.
- Census: no change.

### Proof obligation

A cutout triangle whose chain sits at or above the cutoff proves. The
same triangle under a blending preset stays subject to the exact-one
rule. A triangle whose whole domain sits below the cutoff never moves,
because the shader discards it and no claim survives.

### Open design questions

1. Request shape. Option A: one request captures `_Mode` and `_Cutoff`
   unconditionally, and the interpretation branches on the preset.
   Option B: per-preset requests, as the note's "per mode preset"
   phrasing hints. Recommendation: option A. Two extra scalars cost
   little, one request keeps the capture seam single, and the mode value
   itself gates nothing until the split route reads it. The plan task
   records the choice and its reason.

## 10. Slice 7: premultiply conversion admission

Conversion gate 5 refuses premultiply on the premise that
premultiplication changes how the color is produced, so the blend
predicate cannot excuse it. The vendor source shows the exact production
step. It is `saturate(alpha)`, and that value is exactly 1 on every
triangle the proof moves. The owner accepted the premise revision on
2026-09-21 (note sections 6.2 and 9.1), so this slice is cleared, with
the disclosure. Until it lands, the shipped behavior stays safe.

### Contract surface

- Conversion: gate 5 admits premultiply. The gate comment states the
  revised premise: the factor is `saturate(alpha)` and alpha is exactly
  1 on every moved triangle, so the canonical clone reproduces the
  source color exactly there.
- Premise scoping: the admission holds only on the proof's premise. A
  material with any unproven triangle never carries a premultiply
  conversion claim for those triangles, and they stay on the original
  material. The pure eligibility function cannot see triangles, so the
  scoping lives where it always lived: conversion runs only for a
  material with a proven plan, and the canonical clone serves only the
  proven submesh.
- Disclosure: the per-slot divergence report mechanism carries the
  disclosure, fired once per prepared slot. See the first open question
  on the string.
- Closed enum: `PremultipliedAlphaEnabled` becomes unreachable. See the
  second open question.
- Alpha analysis, evidence request: no change. Slice 2 already admitted
  premultiply there.
- Census: no new member appears. If the closed-enum question ends in
  removal, the historical census rows keep their recorded names, and
  records speak for their date.

### Proof obligation

For every moved triangle, the canonical clone's color equals the source
color exactly. Any unproven triangle stays on the original material. The
disclosure appears once per prepared slot, and only for a conversion
that used the revised premise.

### Open design questions

1. The disclosure string. The existing
   `amuse.slotSeparation.DepthTestDivergence` sentence says moved
   triangles changed their depth rule. That sentence would be false for
   a premultiply-only case, because the premultiply transformation is
   exact on the proof's premise. Option A: reuse the existing key and
   text. Option B: add a distinct report key with its own true
   sentence, fired through the same per-slot mechanism. Recommendation:
   option B. The owner asked for the disclosure, and a disclosure must
   say what actually happened.
2. The unreachable refusal member. Option A: remove
   `PremultipliedAlphaEnabled` in the clean cutover. Option B: keep it
   as documented dead vocabulary. Recommendation: option A. The name has
   no external contract, closed enums stay honest, and historical
   census rows keep their recorded names either way. The plan task
   states the choice before the code lands.

## 11. Stop-condition review

The session stop conditions are the standard four, plus a slice that
needs a shared frontend abstraction, plus a vendor fact that contradicts
the note or the pinned sources.

1. Correctness contract. Slice 7 revises a declared conversion premise.
   The owner accepted that revision on 2026-09-21, so the condition is
   resolved in favor of the slice (note 9.1). No other slice touches
   the proof rule or the policy.
2. Scope expansion. Slice 2 leaves the base-color gate closed. Section
   2.1 records that boundary so no task widens it silently. The detail
   feature and the other blocked outputs stay outside the parity
   boundary (note 9.5).
3. Shared frontend abstraction. No slice needs one. Every Poiyomi
   equation keeps its own channel selector, pan, invert, and default
   mode. The lilToon machinery stays the proof shape to mirror, not a
   shared seam. lilToon and Poiyomi keep applying pressure on the value
   algebra that already exists.
4. Contradicting vendor fact. The 2026-09-21 verification found none.
   Every checked anchor matched the note.
5. Standard conditions. No contradiction inside the approved plan
   survived verification. The wording difference in note 6.3 between
   the full admission set and the smallest slice is a scoping gradient,
   and section 6 records both.

## 12. Validation of this stage

No repository test ran. This is a docs-only stage on a branch without
production changes. The verification record of section 2 is the observed
evidence, gathered by reading the committed sources on 2026-09-21. The
live probe evidence stays in the note of the same date and is not
repeated here.
