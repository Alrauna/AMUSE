# The lilToon consent false refusal in the Census Lab

Date: 2026-09-07. Base: `main` at `a88e9c0`.
Labels: `[SOURCE]` is a fact read in this tree or in pinned upstream source.
`[MEASURED]` is a fact produced by a run in this investigation.
`[INFERENCE]` is a conclusion. `[DECISION NEEDED]` is a choice the user must make.

## 1. The symptom

The user ran an opted-in corpus avatar in the Census Lab project. The AMUSE
consent dialog appeared. It named six lilToon shaders as unverified versions:
`lilToon`, `Hidden/lilToonOutline`, `Hidden/lilToonCutout`,
`Hidden/lilToonCutoutOutline`, `Hidden/lilToonTransparent`, and
`Hidden/lilToonTransparentOutline`. `[SOURCE]`

The dialog says AMUSE would treat these shaders with the verified version's
rules, which may be wrong. The user concluded that lilToon transparency support
is likely incorrect. This note corrects that reading. `[SOURCE]`

## 2. What the run actually did

The editor log shows the reported verdicts come from
`AmuseReports.ConsentDeclined`. The consent question returned false. The pass
returned before any renderer analysis. The later apply pass only revalidated
and swept. Nothing on the avatar was mutated. `[SOURCE]`

So the transparency code paths never ran in that build. The dialog lists
transparent shaders because the whole lilToon family failed identity, not
because of a transparency defect. No false positive on transparent triangles is
observed. `[INFERENCE]`

## 3. The reproduction

The attestation pins lilToon 2.3.4. The Lab project has exactly
`jp.lilxyzw.liltoon` 2.3.4 installed. The version pins match. `[MEASURED]`

A throwaway harness reproduced the production digest outside Unity. It reads
each file as UTF-8, strips a byte order mark, normalizes line endings, and
computes SHA-256, exactly as `ComputeNormalizedSourceHash` does. It forms the
include-tree digest exactly as `ComputeIncludeTreeDigest` does.

- The Lab include tree digests to `6e2dce6c...c48fd46`. This equals the pinned
  `IncludeTreeDigest`. The include directory is not the failing input.
  `[MEASURED]`
- The six shader assets and their pass files differ from the official 2.3.4
  package artifact. The canonical digests therefore cannot equal the pins.
  `[MEASURED]`

The official artifact was the VPM release zip for 2.3.4. Its SHA-256 is
`34d172761c51aa9469a904704109086aafa6125a4fa0e058766e2ddc73d3b303`, which
matches the registry record. `[MEASURED]`

## 4. The root cause

The Lab has the LTCGI package installed. lilToon's generator rewrote its shader
assets at import with LTCGI integration enabled. The rewrite is vendor behavior,
not a hand edit. Three variation kinds appear, the same in every family:

1. Each `.shader` asset gains one appended tag: `"LTCGI"="ALWAYS"` inside the
   existing `Tags` line. The canonicalizer retains tags by design, so the
   appended tag breaks the digest. `[SOURCE]` `[MEASURED]`
2. Each pass file gains `#define LIL_FEATURE_AUDIOLINK_PACKAGE` before the
   first `skip_variants` line, because the Lab also has AudioLink. `[MEASURED]`
3. Each pass file gains `#define LIL_FEATURE_LTCGI` at three body positions.
   The canonicalizer removes valueless setting defines only inside the
   `HLSLINCLUDE` region. These positions sit in pass bodies, so the lines are
   retained and break the digest. `[SOURCE]` `[MEASURED]`

The pass files also carry shortened `skip_variants` lists. The generator drops
the lightmap, probe volume, and decal entries when LTCGI is on. The
canonicalizer verifies the substitution slot against the default-settings
shape, so the shorter lines fail that proof. `[SOURCE]` `[MEASURED]`

The canonicalization covers exactly the variations proven from a
default-settings install. LTCGI and AudioLink generator output sits outside
that proof. Every lilToon material therefore refuses. The refusal is a false
positive on identity: unnecessary refusal is a coverage defect by policy.
`[INFERENCE]`

## 5. The transparency verdict

No transparency-specific defect is found. All six variants fail the same three
shared variations. The transparent profiles would attest once the shared cause
is fixed. The alpha-relevant regions of the generated passes must still be
re-proven under the LTCGI shape before admission, per the existing discipline
that a digest variation must touch nothing the alpha proofs cite. `[INFERENCE]`

## 6. A provenance discrepancy to correct

The attestation comment pins the official tag 2.3.4 zip at SHA-256
`e81579d3...91bdc66322`. The VPM release zip hashes to `34d17276...d73d3b303`
and the registry declares the same value. Either the pinned digest names a
different artifact, or the record is wrong. The measurement provenance must be
re-checked and the comment corrected when the next attestation pass runs.
`[MEASURED]` `[DECISION NEEDED]`

## 7. The corrective plan

The declared intent of the canonicalization is to admit legitimate per-project
generator output and refuse everything else. The fix extends the canonicalizer
with newly proven generator variations. Pin tables per settings combination are
rejected: the combinations are open-ended. Clicking through on every Census run
is rejected: batch mode refuses, and the dialog is not a fix.

Proposed slice, pending design approval:

1. Characterize the generator's full variation surface for the settings the
   corpus uses: default, LTCGI, AudioLink, and both. Produce one generated
   install per shape and diff all shader and pass files. `[SOURCE]`
2. Extend the canonicalization with a proof per variation kind: the appended
   tag token inside an otherwise official `Tags` line, the valueless feature
   defines at their generator slots, and the alternate `skip_variants` shapes
   at the existing substitution slot. Retain the fail-closed direction: a hand
   edit anywhere else still changes the digest. `[SOURCE]`
3. Prove the alpha citations: diff the regions the alpha proofs read between
   the default and LTCGI shapes, and record that they are untouched.
   `[SOURCE]`
4. Re-measure every profile digest from a regenerated install. Require that the
   canonicalized bytes of every settings shape agree on one digest per profile.
   Disagreement means the canonicalization is incomplete. `[MEASURED]`
5. Add falsifiers: a hand-added tag token refuses, a moved or edited feature
   define refuses, a wrong version refuses, and each settings shape attests.
   `[SOURCE]`
6. Correct the zip provenance comment in the same change. `[SOURCE]`

Stop gate: this changes the attestation method and its correctness contract.
It waits for the user's approval of this plan. It blocks Census validation runs
that include lilToon avatars, because batch runs refuse at the consent layer.

## 8. Residuals

- The generator's variation space is larger than two observed settings. Each
  future third-party integration can add kinds. The canonicalization proof
  list must grow with evidence, one kind at a time.
- The `e81579d3` zip provenance question in section 6 is resolved: it is the
  tag source zip, reproduced by download. `[MEASURED]`
- The one-pass and two-pass wrapper digests were measured on tag bytes with
  canonicalization as the identity. That assumption is false under LTCGI;
  the 2026-09-07 re-measurement re-derived them under the new
  canonicalization and confirmed the digest values unchanged. `[MEASURED]`

## 9. Validation outcome (2026-09-07, after the fix)

A first validation round exposed a second refusal layer: the activator
provenance check refused the generator's own integration defines. The
slot-scoped rule (design G5, shipped in PR #74) removed that layer.

The full corpus validation then ran in the Census Lab project, driven
through the editor automation bridge:

- Consent pre-screen: every prefab assigned-material set was checked
  against the transfer-consent collector. Zero subjects flagged.
  `[MEASURED]`
- Full nondestructive builds with the trigger component ensured: 64 of
  64 corpus prefabs completed without exception. `[MEASURED]`
- The semantic barrier and capture passes executed in every build.
  `[MEASURED]`
- The editor log for the session contains zero unverified-version
  reports. `[MEASURED]`

The lilToon consent false refusal is closed for the corpus. The S12
census validation is unblocked.
