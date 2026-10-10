# LTCGI-integrated lilToon consent regression investigation

Date: 2026-10-10. Status on this date: investigation complete. No
production code changed in this session.

Privacy note: this record names no private avatar, renderer, material,
animation, or asset of the Census Lab corpus. The Census Lab project
appears by role name only. The shader files, line numbers, and digest
values below belong to the public vendor package `jp.lilxyzw.liltoon`
and to AMUSE's own pins.

Labels: `[SOURCE]` is a fact read in this tree, in pinned vendor source,
or in official vendor documentation. `[MEASURED]` is a value produced by
a run on 2026-10-10. `[INFERENCE]` is a conclusion drawn from those
facts. `[LIMIT]` is a known boundary of the evidence.

## The question

The session asked for a safe, simple, and architecturally sound way to
let the LTCGI-patched lilToon install through AMUSE without a consent
prompt. Three candidate directions were named: (a) pin a second,
patched-shader digest, (b) a bounded token strip at pass level, and (c)
a declared-irrelevant metadata allowlist. Widening the existing token
strip was ruled out in the session context.

## Answer in brief

The patched install fails identity because of a defect in the
2026-10-09 Finding 41 fix, not because the patch needs a new admission
rule. `[MEASURED]`

The LTCGI token sits on the SubShader `Tags` line. The R6 strip already
proves and admits exactly that token at that position. The strip never
fires, because the 2026-10-09 scope walk recognizes only the inline
form `SubShader {`. The shipped lilToon 2.3.4 files place the brace on
its own line. On real files the walk pushes the wrong scope kind, the
Tags line stays ineligible, the token survives into the canonical
digest, and the identity verify refuses. The consent dialog follows.

The recommendation is to repair the scope walk so it implements its own
documented predicate for both brace styles. This restores the behavior
that the 2026-09-07 shape-agreement measurement validated on two real
installs. It widens nothing. Directions (a), (b), and (c) each add
mechanism where a correct implementation of the proven mechanism
suffices, and direction (b) is in addition excluded by the session
boundary.

## Correction to the session premise

The session context described the one-line delta as a pass-level Tags
entry at line 640. The line number is right. The scope is not. Line 640
is the SubShader `Tags` line. `[MEASURED]` The August matrix recorded
the same fact as "the official SubShader tag at zero-based line 639".
`[SOURCE]`
`docs/superpowers/specs/2026-08-21-liltoon-official-integration-matrix-design.md`,
section "Activation and generated-source matrix". Direction (b), a
strip at pass level, therefore addresses a location the patch does not
occupy.

## Measured facts

All measurements are from 2026-10-10.

1. The official lilToon 2.3.4 source zip downloads from
   `https://github.com/lilxyzw/lilToon/archive/refs/tags/2.3.4.zip`. Its
   SHA-256 is
   `e81579d355878ed73880d99a68ab30a8552d55051be603c450f56491bdc66322`,
   equal to the provenance value recorded in the attestation source
   comments. `[SOURCE]` `[MEASURED]`
2. The vendor zip's `lts.shader` hashes, raw and canonical, to
   `5206bec25e82db5f8009b27fcc5ba94d7c41113031d4b6b0a2c25ca324a9c704`.
   That is the pinned `ShaderCanonicalDigest`
   (`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:374`).
   Canonicalization is byte-preserving on the vendor file. `[MEASURED]`
3. The Census Lab project installs lilToon 2.3.4 with LTCGI 1.7.3. Its
   `lts.shader` is byte-identical to the vendor file plus one change:
   line 640 reads
   `Tags {"RenderType" = "Opaque" "Queue" = "Geometry" "LTCGI"="ALWAYS"}`
   instead of the vendor line without the last token. The reconstructed
   vendor-plus-token file hashes to
   `1a2ffc7fa6b3d54d5765de3c98ab1ff2e8ce7da4fd773e507c8c32568c369f56`,
   equal to the Lab file's raw hash and to the August matrix's
   LTCGI-state base digest. `[MEASURED]`
4. The token-removed Lab text, produced with the exact R6 removal
   semantics, hashes to the pin `5206bec2...`. The token is the only
   delta between the Lab file and the pinned identity. `[MEASURED]`
5. The Lab file uses Allman braces at the SubShader block: line 638 is
   `SubShader`, line 639 is `{`, line 640 is the `Tags` line. A trace
   of `ComputeLtcgiTagStripScope` over these lines shows the brace line
   pushes scope kind `other`, so line 640 is not eligible for the
   strip. `[MEASURED]`
6. The production canonicalizer, run inside the Census Lab editor
   instance over the installed package files, returns the canonical
   digest `1a2ffc7f...` for the Lab base shader. It equals the raw
   hash, so the canonicalizer changed nothing: no rule fired, least of
   all R6. `[MEASURED]`
7. The same run returns the pass digest
   `aee1ea0c1fd0ae26f561fbade4c23309bb62ed8aff0c9221d1cba7c74a31d9d1`
   for `ltspass_opaque.shader`, equal to the pinned
   `PassCanonicalDigest`, with the pinned pass GUID. The pass half of
   the identity attests today. The include tree matches the pin per the
   2026-10-10 session facts. `[MEASURED]` `[SOURCE]`
8. R6 removes the token with `line.Remove(tokenIndex, token.Length - 1)`
   and keeps the Tags closing brace
   (`LilToonSourceAttestation.cs:1246-1261`). The token form requires
   the line to start with `Tags {` and to end with
   ` "LTCGI"="ALWAYS"}`. The Lab line satisfies both. `[SOURCE]`
9. Official LTCGI documentation defines the tag as the marker that makes
   the LTCGI controller recognize a shader as compatible. The value is
   `ALWAYS` or a shader property name. The tag is selection metadata
   for the controller. `[SOURCE]` `https://ltcgi.dev/Advanced/Shader_Authors`
10. The August record characterizes the same tag as "integration
    metadata that the LTCGI controller uses to select renderers" and
    locates the trust problem in the activation defines, not in the
    tag. `[SOURCE]`
    `docs/superpowers/specs/2026-08-21-liltoon-attestation-investigation-design.md`,
    section "Why LTCGI cannot simply be canonicalized away".

## Root cause

`ComputeLtcgiTagStripScope`
(`LilToonSourceAttestation.cs:1271-1305`) tracks brace scope per line.
It pushes `SubShader` only when the first token of a brace-carrying
line is `SubShader`, that is, the inline form `SubShader {`. The
shipped 2.3.4 files write

```
SubShader
{
    Tags {...}
```

The brace line alone carries `{`, so the walk pushes `other`. The
SubShader `Tags` line never becomes eligible. `[MEASURED]`

Finding 41 (2026-10-09) found that the pre-fix R6 matched any `Tags`
line while its proof covered only the SubShader line. The fix anchored
the match to the SubShader block through this walk. The walk
misimplements the anchor for the Allman style that every real 2.3.4
container uses. `[SOURCE]`
`docs/superpowers/investigations/2026-10-09-third-latent-bugs-and-architectural-impurities-investigation.md`,
Finding 41.

## Why the September validation passed and October fails

The 2026-09-07 generator-shape decision G3 measured the pins through
the production canonicalizer from two real shapes and required one
digest per profile across the shapes. `[SOURCE]`
`docs/superpowers/specs/2026-09-07-liltoon-generator-shape-attestation-design.md`.
At that date R6 matched any `Tags` line, so the real Allman file
stripped its token and the shapes agreed. `[SOURCE]` Finding 41 text.

The 2026-10-09 Task 7 fix added the scope walk. From that date the
strip under-fires on every real install, the Lab shape fails identity,
and the consent pre-scan
(`Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:485-489`
feeding `CollectTransferConsent`,
`Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:471-530`)
emits the subject for shader `lilToon`. `[INFERENCE]` from facts 5, 6,
and the code. The Lab consent dialog on 2026-10-10 is the reported
symptom. `[SOURCE]` session facts.

## Direction analysis

### (a) Pin the patched digest as a second admitted row

Safety: the admitted set stays exact bytes. Nothing beyond the measured
patch passes. `[INFERENCE]`

Cost: the value `1a2ffc7f...` is the output of a scope-detection
defect. A later walk repair makes the row unreachable dead pin weight.
The profile structure holds one `ShaderCanonicalDigest` per family
profile today, so admission needs a second row per family and a
membership check instead of the equality checks at
`LilToonSourceAttestation.cs:2093-2102`. Any other patch formatting, or
a patch on another family file, falls outside the row and prompts
again. `[INFERENCE]`

Architectural fit: pinned data, measured from a real install, never
re-derived at runtime. The rule is honored. The consent consequence for
uncovered patch shapes is the existing dialog, which is the correct
fail-closed behavior. `[INFERENCE]`

### (b) Token strip at pass level

Excluded twice. The session boundary rules out widening the strip. The
measurement removes the motive: the token is on the SubShader line, not
in a pass. `[MEASURED]`

### (c) Declared-irrelevant metadata allowlist keyed into the identity rule

Safety: the admitted delta class is the exact pair occurrence inside
`Tags` lines, with all other bytes hashed. The pair is inert for the
alpha proofs, which read pass state, material properties, texture
evidence, and canonical digests, never pass tags. `[INFERENCE]`

Cost: a second exception mechanism beside R6, a new scope proof, new
falsifiers, and supersession of two Finding 41 era tests
(`Canonicalize_PassTagsLineWithLtcgiToken_IsRetained` and
`Canonicalize_AppendedLtcgiTagTokenOutsideSubShaderScope_IsRetained`
would need re-anchoring under a wide reading). All of this duplicates
what R6 already proves for the observed patch location. The
consent-dialog consequence matches the repair when the pair is the only
delta. `[INFERENCE]`

Architectural fit: the allowlist is pinned data and honors the pin
rule. The cost is real but buys admission for a patch location that
does not exist in the measured population. `[INFERENCE]`

### (d) Repair of the scope walk (recommended)

The walk must implement its documented predicate, "a Tags line whose
innermost open brace scope is a SubShader block", for the Allman form
as well as the inline form. The admitted delta class returns to exactly
the class the 2026-09-07 G3 measurement validated: the pinned bytes
plus the exact token at the final position of a SubShader `Tags` line.
No pin, profile, or consent code changes. Under-strip stays the failure
direction: a line that keeps its token changes the digest and refuses.
`[INFERENCE]`

## Recommendation

Repair `ComputeLtcgiTagStripScope` to hold the block kind pending
between a `SubShader` keyword line and its brace line. This is not a
widening of the strip. The strip admits the same token at the same
scope as its R6 proof and as the pre-2026-10-09 behavior. The repair
removes a false refusal, which the repository policy names a coverage
defect. `[DECISION]` pending user approval of the design.

Fallback if the strip is off limits for any change: direction (a), with
the digest `1a2ffc7f...` pinned as a second opaque-family row, measured
2026-10-10 from the Census Lab install and reproduced from the official
zip plus the one-line patch, provenance recorded in the attestation
comments. `[DECISION]` fallback only.

## Stop conditions checked

1. "The LTCGI token can affect an alpha-relevant compilation path in
   some configuration." Not triggered. The strip changes only which
   bytes enter the identity digest, never what Unity compiles. The tag
   is controller selection metadata per the vendor documentation and
   the August record. `[SOURCE]` `[INFERENCE]`
2. "The patched digest varies across patched installs." Not triggered
   for the measured population. The Lab install is byte-identical to
   vendor plus the one-line token, and the admitted class is
   formatting-exact, so any install outside it keeps the consent
   dialog. Cross-install sampling is one install. `[LIMIT]`
3. "A direction requires re-deriving digests from vendor sources at
   runtime." Not triggered. The repair uses the existing pins. The zip
   download was a one-time characterization measurement, not runtime
   behavior. `[SOURCE]`

## Measured versus inferred

Measured on 2026-10-10: the zip digest, the vendor file digest, the
Lab file equality with vendor plus token, the token-removed digest, the
scope walk trace, the production canonical digest of the Lab base
shader, and the Lab pass digest against the pin.

Inferred: the regression timeline, the equivalence of the repaired walk
with the G3-validated behavior, and the inertness of the token for the
alpha proofs, which rests on the vendor documentation and the August
characterization.

Not directly measured by this session: the include-tree digest inside
the Lab. Session facts establish it. The first measurement pass ended
before that check. Also not directly measured: a full identity verify
of the Lab install after the repair, which the implementation plan
covers as a validation step.
