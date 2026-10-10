# lilToon LTCGI strip SubShader scope repair design

Date: 2026-10-10. Basis: the user request to let the LTCGI-patched
lilToon install through AMUSE without a consent prompt, and the
investigation record
`docs/superpowers/investigations/2026-10-10-ltcgi-liltoon-consent-regression-investigation.md`.
Companion plan:
`docs/superpowers/plans/2026-10-10-ltcgi-strip-subshader-scope-repair-plan.md`.

Labels: `[SOURCE]` is a fact read in this tree or in official vendor
source or documentation. `[MEASURED]` is a value produced by a run on
2026-10-10. `[DECISION]` is a choice this design makes. `[LIMIT]` is a
known boundary.

## The defect

Every lilToon 2.3.4 install with LTCGI integration fails base-shader
attestation since the 2026-10-09 Finding 41 fix. The consent pre-scan
then offers a shader transfer consent for shader `lilToon` on every
such avatar. `[SOURCE]` investigation record, "Root cause" and
"Why the September validation passed and October fails".

The chain:

1. lilToon's LTCGI integration appends the exact token
   ` "LTCGI"="ALWAYS"}` to the SubShader `Tags` line of the base
   container. In the shipped 2.3.4 files the `SubShader` keyword and
   its brace sit on separate lines. `[SOURCE]` `[MEASURED]`
2. R6, `RemoveAppendedLtcgiTagToken`
   (`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:1246-1261`),
   removes exactly this token from an eligible line and keeps the
   closing brace. `[SOURCE]`
3. `ComputeLtcgiTagStripScope` (`:1271-1305`) decides eligibility. It
   pushes the scope kind `SubShader` only for a brace-carrying line
   whose first token is `SubShader`. For the Allman form the brace line
   carries only `{`, so the walk pushes `other`, the Tags line stays
   ineligible, and the token survives into the canonical digest.
   `[SOURCE]` `[MEASURED]`
4. The Lab base shader then digests to
   `1a2ffc7f...` instead of the pin `5206bec2...`, the identity
   conjunction refuses, and `CollectTransferConsent` emits a subject.
   `[MEASURED]` `[SOURCE]`
   `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:471-530`.

The measured delta between the Lab install and the pinned identity is
the token alone: removing the token with exact R6 semantics restores
the pinned bytes and the pinned digest. `[MEASURED]`

## Facts the design rests on

- The pinned `ShaderCanonicalDigest`
  `5206bec25e82db5f8009b27fcc5ba94d7c41113031d4b6b0a2c25ca324a9c704`
  is the raw vendor `lts.shader` digest. The official 2.3.4 source zip
  reproduces it. `[MEASURED]`
- The token line in the Lab install is line 640, the SubShader `Tags`
  line, between `SubShader` (line 638) and `{` (line 639). `[MEASURED]`
- The pass file and the include tree attest against their pins in the
  same install. `[MEASURED]` `[SOURCE]`
- The token is controller selection metadata. Official LTCGI
  documentation defines the `LTCGI` tag with value `ALWAYS` as the
  compatibility marker the controller reads. `[SOURCE]`
  `https://ltcgi.dev/Advanced/Shader_Authors`

## Decisions

`[DECISION]` D1 - Repair `ComputeLtcgiTagStripScope` so the documented
predicate, "a Tags line whose innermost open brace scope is a
SubShader block", also holds for the Allman form. A `SubShader`
keyword line with no brace holds its kind pending. The next
brace-carrying line opens the `SubShader` scope if it is the pending
brace line. Blank lines and comment lines between keyword and brace
keep the pair. Any other content line between them breaks the pair and
the strip under-fires.

`[DECISION]` D2 - This is a repair, not a widening. The strip admits
the same token, in the same final position, at the same SubShader
scope, as its R6 proof and as the behavior the 2026-09-07 G3
shape-agreement measurement validated on two real installs. The
admitted delta class stays exactly: pinned bytes plus the token's
presence at the final position of a SubShader `Tags` line. The session
boundary against widening the strip is respected. Any doubt about this
classification goes back to the requesting session before
implementation.

`[DECISION]` D3 - No pin, profile, version row, consent, or diagnostic
code changes. The pinned digests stay untouched. The consent dialog
stays for genuinely unknown modifications.

`[DECISION]` D4 - Alternatives rejected:

- Direction (a), a second pinned digest row for
  `1a2ffc7f...`: pins the output of this defect, dies as dead weight
  after the repair, needs profile structure change, and leaves every
  other patch formatting at the consent prompt. Fallback only, with
  the measured provenance in the investigation record.
- Direction (b), a strip at pass level: excluded by the session
  boundary and moot. The token is measured on the SubShader line.
- Direction (c), a declared-irrelevant metadata allowlist: sound, but
  it builds a second exception mechanism with a new scope proof for a
  location the existing proven mechanism already covers once the walk
  works.

## The exact change

One function and its doc comment in
`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`.
Nothing else in production changes.

Current body, lines 1271-1305:

```csharp
private static bool[] ComputeLtcgiTagStripScope(string[] lines)
{
    var eligible = new bool[lines.Length];
    var scopeKinds = new List<string>();
    for (var i = 0; i < lines.Length; i++)
    {
        var trimmed = lines[i].Trim();
        var isComment = trimmed.StartsWith("//", StringComparison.Ordinal);
        var firstToken = trimmed.Length == 0 || isComment
            ? string.Empty
            : trimmed.Split(' ')[0];

        eligible[i] = !isComment &&
            trimmed.StartsWith("Tags {", StringComparison.Ordinal) &&
            scopeKinds.Count > 0 &&
            scopeKinds[scopeKinds.Count - 1] == "SubShader";

        if (isComment)
        {
            continue;
        }

        var opens = CountCharacter(trimmed, '{');
        var closes = CountCharacter(trimmed, '}');
        for (var open = 0; open < opens; open++)
        {
            scopeKinds.Add(
                firstToken == "SubShader" && open == 0
                    ? "SubShader"
                    : "other");
        }
        for (var close = 0; close < closes; close++)
        {
            if (scopeKinds.Count > 0)
            {
                scopeKinds.RemoveAt(scopeKinds.Count - 1);
            }
        }
    }

    return eligible;
}
```

Repaired body:

```csharp
private static bool[] ComputeLtcgiTagStripScope(string[] lines)
{
    var eligible = new bool[lines.Length];
    var scopeKinds = new List<string>();
    var pendingSubShader = false;
    for (var i = 0; i < lines.Length; i++)
    {
        var trimmed = lines[i].Trim();
        var isComment = trimmed.StartsWith("//", StringComparison.Ordinal);
        var firstToken = trimmed.Length == 0 || isComment
            ? string.Empty
            : trimmed.Split(' ')[0];

        eligible[i] = !isComment &&
            trimmed.StartsWith("Tags {", StringComparison.Ordinal) &&
            scopeKinds.Count > 0 &&
            scopeKinds[scopeKinds.Count - 1] == "SubShader";

        if (isComment)
        {
            continue;
        }

        var opens = CountCharacter(trimmed, '{');
        var closes = CountCharacter(trimmed, '}');
        for (var open = 0; open < opens; open++)
        {
            var opensSubShader = open == 0 &&
                (firstToken == "SubShader" ||
                    (pendingSubShader &&
                        trimmed.StartsWith(
                            "{", StringComparison.Ordinal)));
            scopeKinds.Add(opensSubShader ? "SubShader" : "other");
        }

        if (opens > 0)
        {
            pendingSubShader = false;
        }
        else if (firstToken == "SubShader")
        {
            pendingSubShader = true;
        }
        else if (pendingSubShader && trimmed.Length > 0)
        {
            // A content line between the keyword and its brace breaks
            // the Allman pair. The strip under-fires and the material
            // refuses, which is the safe direction.
            pendingSubShader = false;
        }

        for (var close = 0; close < closes; close++)
        {
            if (scopeKinds.Count > 0)
            {
                scopeKinds.RemoveAt(scopeKinds.Count - 1);
            }
        }
    }

    return eligible;
}
```

Replacement doc comment:

```
/// <summary>
/// Marks the lines where the R6 LTCGI tag strip may run: a Tags line
/// whose innermost open brace scope is a SubShader block. One forward
/// walk tracks brace depth. A SubShader keyword whose brace opens on a
/// later line, the shipped Allman shape, holds its kind pending until
/// the brace line. Blank lines and comments between keyword and brace
/// keep the pair. Any other content line between them breaks the pair,
/// so the strip under-fires. A comment-only line carries no braces.
/// The anchor may under-strip: a Tags line that keeps its token
/// contributes its original text to the digest and the material
/// refuses. It must never widen the strip beyond SubShader scope.
/// </summary>
```

Behavior of the repair on the measured shapes:

- Inline `SubShader {`: unchanged. First token classification as
  today. All existing green tests stay green.
- Allman `SubShader` then `{`: the brace line pushes `SubShader`. Line
  640 of the Lab shape becomes eligible. R6 removes the token. The
  canonical digest collapses to the pin `5206bec2...`. `[MEASURED]`
  for the byte arithmetic. The executing session observes the test
  result.
- `Properties` and `Pass` blocks in Allman form: unaffected. Their
  brace lines push `other` exactly as today.

## Safety argument

The strip masks one delta class: presence or absence of the exact
token ` "LTCGI"="ALWAYS"}` at the final position of a `Tags` line
whose innermost open scope is `SubShader`. Nothing else on the line or
in the file is masked. Any other edit changes the canonical digest and
refuses. `[SOURCE]` R6 code and falsifier F1.

The masked class is inert for AMUSE decisions:

- The strip acts on the identity digest input only. It never changes
  what Unity compiles. `[SOURCE]` the canonicalizer feeds
  `NormalizedSourceHash.Compute`, not the shader compiler.
- The token is metadata that the LTCGI controller reads to select
  compatible shaders. `[SOURCE]` vendor documentation and the August
  characterization.
- The alpha proofs read pass state, material properties, texture
  evidence, and canonical digests. They read no pass tags. The
  declared render mode comes from the pass source scan, not from
  tags. `[SOURCE]` `TryScanRenderMode`,
  `LilToonSourceAttestation.cs:1465-1502`.

Fail-closed direction: an unrecognized shape keeps the token, changes
the digest, refuses at identity, and reaches the consent dialog.
Under-strip is the designed failure direction. `[SOURCE]` R6 doc
comment.

Finding 41 compliance: the pre-fix R6 matched any `Tags` line. The fix
narrowed eligibility to SubShader scope. The repair keeps that
narrowing and corrects the scope detection for the Allman form. The
admitted set after the repair is a strict subset of the admitted set
before the 2026-10-09 fix. `[INFERENCE]`

## Consent consequence, what the user sees

Before the repair, on every LTCGI-integrated lilToon install, the
build shows the shader transfer consent entry: "Shader 'lilToon' is
not a verified version. AMUSE would treat it with the verified
version's rules, which may be wrong." `[SOURCE]`
`UnityMaterialSemantics.cs:498-502`.

After the repair, that entry is gone for the measured patch shape. The
canonical digest matches the pin, the identity conjunction holds, and
`CollectTransferConsent` emits no subject for the material. The dialog
does not appear for that avatar unless another material carries a
genuinely unknown modification. Those materials keep the same wording.
`[INFERENCE]` from the measured delta and the code path. The
implementation plan validates it.

## Falsifiers

A plausible wrong implementation must fail at least one. Numbers
continue the R-series falsifier style.

- F7: an Allman `Pass` block with the token on its `Tags` line keeps
  the token. Kills "push SubShader for any keyword or brace line".
- F8: an anonymous brace block before a `Tags` line keeps the token.
  Kills "treat any bare brace line as SubShader".
- F9: a quoted `"SubShader"` text line before a brace line keeps the
  token on the following `Tags` line. Kills "match SubShader by
  containment".
- F10: a content line such as `LOD 200` between the `SubShader`
  keyword and its brace keeps the token. Kills "pending kind survives
  any intervening line".
- F11: blank lines and comment lines between the keyword and the brace
  still strip the token. Kills implementations that clear the pending
  kind on blank or comment lines, and keeps the repair from being
  narrower than the shipped files require.
- F12: the exact token with a different value or key stays hashed in
  Allman form. Kills "strip any trailing quoted token". Existing F1
  covers the inline form.
- F13: a `SubShader` keyword line followed by another block's inline
  brace line, for example `Pass {`, grants no SubShader scope to that
  block. Kills a repair whose pending kind attaches to any
  brace-carrying line instead of the pending brace line.

## Tests

New tests in
`Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`,
one class, same `Canon` seam as the existing R6 tests:

- `Canonicalize_AppendedLtcgiTagTokenInsideAllmanSubShader_IsRemoved`
  (RED first: it fails on the current code for the measured reason)
- `Canonicalize_AllmanSubShaderScopeTokenWithOtherLineEdits_IsRetained`
- `Canonicalize_AllmanPassScopeLtcgiToken_IsRetained`
- `Canonicalize_AnonymousBraceBlockTagsLineLtcgiToken_IsRetained`
- `Canonicalize_QuotedSubShaderTextDoesNotOpenSubShaderScope`
- `Canonicalize_ContentBetweenSubShaderKeywordAndBrace_BreaksEligibility`
- `Canonicalize_AllmanSubShaderBlankAndCommentLinesBeforeBrace_StillStrips`
- `Canonicalize_SubShaderKeywordBeforeInlinePassBlock_DoesNotGrantScope`

Existing tests that must stay green unchanged:
`Canonicalize_AppendedLtcgiTagTokenInsideSubShader_IsRemoved`,
`Canonicalize_AppendedLtcgiTagTokenOutsideSubShaderScope_IsRetained`,
`Canonicalize_PassTagsLineWithLtcgiToken_IsRetained`,
`Canonicalize_OtherTagTokenEdit_IsRetained`,
`Canonicalize_GeneratorShapes_AgreeOnOneForm`. No test is deleted or
weakened. `[DECISION]`

## Residuals and limits

- `[LIMIT]` Cross-install sampling is one real install plus the vendor
  zip. The admitted class is formatting-exact, so unmeasured patch
  formats keep the consent dialog by design.
- `[LIMIT]` Only the built-in render pipeline shape is characterized.
  The 2026-09-07 residuals on URP and HDRP stand.
- `[LIMIT]` A malformed file that carries the bare word `SubShader`
  inside a pass extends eligibility to the next brace line. The masked
  class is still the inert token only. The falsifier F9 covers the
  quoted form. The malformed keyword form stays inside the masked
  class that the R6 proof covers.
- A later lilToon version can move or reformat the token. That is a
  new pin measurement under the existing discipline, not an extension
  of this repair.
