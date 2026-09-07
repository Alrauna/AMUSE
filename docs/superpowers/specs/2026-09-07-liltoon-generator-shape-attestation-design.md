# lilToon generator shape attestation - design

Date: 2026-09-07. Basis: the user's approval of the corrective plan in
`docs/superpowers/investigations/2026-09-07-liltoon-ltcgi-consent-false-refusal.md`.
Companion plan: `docs/superpowers/plans/2026-09-07-liltoon-generator-shape-attestation-plan.md`.
Labels: `[SOURCE]` is a fact read in pinned upstream source or in this tree.
`[MEASURED]` is a fact produced by a run in this investigation. `[DECISION]` is
a choice the user made. `[LIMIT]` is a known instrument or evidence limit.

## 1. The defect

AMUSE refuses every lilToon material in a project whose lilToon import ran with
LTCGI integration. The refusal is a false positive on source identity. The
Consent Lab run showed the consent dialog for all six lilToon variants and
declined. Nothing was mutated. `[MEASURED]`

## 2. The vendor facts

The attested version is lilToon 2.3.4. Its generator rewrites the shader and
pass files at import from container templates. The container unpacker runs a
dedup pass over `skip_variants` lines: a token can survive in only one line per
file, so slot content is order dependent. The unpacker source is
`Editor/lilShaderContainerImporter.cs` in the installed package. `[SOURCE]`

Three variation kinds are proven:

1. The `*LIL_SHADER_SETTING_MULTI*` slot emits a valueless define per active
   integration from the closed `BuildShaderSettingStringMulti` domain. The
   AudioLink define appears there when AudioLink is installed. `[SOURCE]`
2. The `lil_multi_compile_forward` expansion inserts
   `#define LIL_FEATURE_LTCGI` directly before `#define LIL_PASS_FORWARD` when
   LTCGI is installed. `[SOURCE]`
3. The SubShader `Tags` line gains one appended token: `"LTCGI"="ALWAYS"`.
   `[SOURCE]`

The skip-variant slot contents differ between the shipped bytes and a
regenerated install because the dedup pass redistributes tokens. All observed
tokens come from the generator's fixed literals. `[SOURCE]` `[MEASURED]`

## 3. The decisions

`[DECISION]` G1 - The canonicalizer removes three more content-verified kinds.
R5: a `skip_variants` line whose tokens all belong to the pinned generator
vocabulary is removed anywhere in the file. R4: a valueless
`#define LIL_FEATURE_LTCGI` directly before the existing
`#define LIL_PASS_FORWARD` anchor is removed. R6: the exact appended token
` "LTCGI"="ALWAYS"` is removed from a `Tags` line when it is the last token.
Everything else stays retained, so any other hand edit still changes the
digest.

Rationale for file-wide R5: a `skip_variants` line only prunes compile
variants. It cannot change the alpha semantics of any compiled variant, so it
cannot change an AMUSE decision. A hostile line using an unknown token stays
hashed. `[DECISION]`

Correction found during implementation: the official record verifier needs no
change. Its last three checks couple each skip record to its feature flag by
inversion, and the regenerated shape satisfies them already. The shape G2
described was based on a misreading; no verifier edit ships.


`[DECISION]` G3 - The profile pins are re-measured through the production
canonicalizer from two real shapes: the shipped 2.3.4 bytes and a regenerated
LTCGI plus AudioLink install. Every profile must produce one digest across the
shapes. Disagreement means the canonicalization is incomplete and blocks the
`[DECISION]` G4 - The zip provenance question is resolved. The comment value
`e81579d3...` names the tag source-code zip; the download reproduces it. The
VPM release zip hashes to `34d17276...d73d3b303`, which the registry declares.
Both artifact identities are now named explicitly in the attestation comments.

`[DECISION]` G5 - found during the first Lab validation: the activator check
refused every occurrence of the three external-activation defines, including
the generator's own. The rule is now slot-scoped. An occurrence is admitted
when it sits inside the verified setting run, or - for the LTCGI define -
directly before the LIL_PASS_FORWARD anchor. An occurrence anywhere else
still refuses as an unsupported variant. The defines gate lighting variants,
not alpha semantics, so slot-proven occurrences cannot flip an AMUSE
decision.

## 4. Falsifiers

A plausible wrong implementation must fail at least one:

1. F1: a hand-added tag token other than the exact LTCGI token stays hashed.
2. F2: a `skip_variants` line with an unknown token stays hashed.
3. F3: a valued `#define LIL_FEATURE_LTCGI 1` stays hashed.
4. F4: an LTCGI define away from the `LIL_PASS_FORWARD` anchor stays hashed.
5. F5: a settings-shape change that alters nothing else produces the same
   canonical digest (the shape-agreement property).
6. F6: a different package version still refuses at the version gate.

## 5. Residuals

- The URP and HDRP multi-compile branches may place the LTCGI define
  differently. Only the built-in RP shape is observed and attested. A URP
  avatar project needs its own characterization before admission.
- A future lilToon version or a new external activator define needs a new
  vocabulary and pin measurement under the D8 discipline.
- The generator's full settings space is larger than two observed shapes. The
  shape-agreement measurement covers the two real shapes; the vocabulary
  closure argument covers the rest within 2.3.4.
