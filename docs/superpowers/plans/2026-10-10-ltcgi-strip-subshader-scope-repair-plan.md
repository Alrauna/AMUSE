# lilToon LTCGI strip SubShader scope repair implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair `ComputeLtcgiTagStripScope` so the R6 LTCGI token
strip fires on the shipped Allman brace form, which removes the false
consent dialog for LTCGI-integrated lilToon 2.3.4 installs.

**Architecture:** One private function and its doc comment change in
`LilToonSourceAttestation.cs`. The walk holds the `SubShader` kind
pending between the keyword line and its brace line. Pins, profiles,
consent code, and diagnostics stay untouched. No test is deleted or
weakened.

**Tech Stack:** C# against Unity 2022.3 APIs, one editor assembly,
NUnit through Unity Test Framework, EditMode only. No new packages.

**Spec:** `docs/superpowers/specs/2026-10-10-ltcgi-strip-subshader-scope-repair-design.md`

## Global Constraints

- Base: `main` at `bca07f0` or the current integration head the
  requesting session names.
- Git boundary: no staging, committing, pushing, or PRs without
  explicit authorization in the executing session.
- Production changes are limited to
  `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`,
  function `ComputeLtcgiTagStripScope` and its doc comment.
- Test changes are limited to
  `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`.
- Pinned digests, GUIDs, version rows, and consent wording stay
  byte-identical.
- A filtered test run that reports 0 tests is a failure. Record
  observed counts for every run.
- Unity runs happen only in the dev editor instance after an exact
  case-sensitive check that `Application.dataPath` equals the
  repository `Assets` folder. Never run tests in the Census Lab
  project.
- No absolute paths, instance names, ports, or private asset names in
  any file, report, or commit message.

---

### Task 1: RED test for the Allman SubShader shape

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`

**Interfaces:**
- Consumes: the existing `Canon(string source)` helper in this test
  class, as the current R6 tests use it.
- Produces: the test
  `Canonicalize_AppendedLtcgiTagTokenInsideAllmanSubShader_IsRemoved`.

- [ ] **Step 1: Write the failing test**

Add next to `Canonicalize_AppendedLtcgiTagTokenInsideSubShader_IsRemoved`:

```csharp
[Test]
public void Canonicalize_AppendedLtcgiTagTokenInsideAllmanSubShader_IsRemoved()
{
    // The shipped 2.3.4 containers place the SubShader brace on its
    // own line. The R6 proof covers the token on that Tags line too.
    const string clean =
        "Shader \"s\" {\n" +
        "    SubShader\n" +
        "    {\n" +
        "        Tags {\"RenderType\" = \"Opaque\" \"Queue\" = \"Geometry\"}\n" +
        "    }\n" +
        "}\n";
    const string tagged =
        "Shader \"s\" {\n" +
        "    SubShader\n" +
        "    {\n" +
        "        Tags {\"RenderType\" = \"Opaque\" \"Queue\" = \"Geometry\" \"LTCGI\"=\"ALWAYS\"}\n" +
        "    }\n" +
        "}\n";

    Assert.That(Canon(tagged), Is.EqualTo(Canon(clean)));
}
```

- [ ] **Step 2: Compile and run only this test**

Refresh the dev editor instance, then run the EditMode filter for
`Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonAttestationTests.Canonicalize_AppendedLtcgiTagTokenInsideAllmanSubShader_IsRemoved`.

Expected: 1 test, FAILED. The failure is the regression proof: the
token stays hashed because the walk never marks the Allman Tags line
eligible. If the test passes on first run, stop and report. The
premise is then wrong.

- [ ] **Step 3: Record the observed failure**

Record the test name, the run count, and the assertion message.

---

### Task 2: GREEN repair of the scope walk

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:1263-1305`
  (doc comment at 1263-1270, function body to 1305)

**Interfaces:**
- Consumes: nothing new. Same inputs and return type
  `bool[]` over normalized lines.
- Produces: eligibility marks that cover the Allman SubShader form.

- [ ] **Step 1: Replace the doc comment**

Replace the comment above `ComputeLtcgiTagStripScope` with:

```csharp
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

- [ ] **Step 2: Replace the function body**

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

- [ ] **Step 3: Run the RED test again**

Same filter as Task 1.

Expected: 1 test, PASSED.

- [ ] **Step 4: Run the whole attestation test class**

Run the EditMode filter for
`Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonAttestationTests`.

Expected: the class runs with the same pass count as before Task 1
plus one, and 0 failures. These must stay green unchanged:
`Canonicalize_AppendedLtcgiTagTokenInsideSubShader_IsRemoved`,
`Canonicalize_AppendedLtcgiTagTokenOutsideSubShaderScope_IsRetained`,
`Canonicalize_PassTagsLineWithLtcgiToken_IsRetained`,
`Canonicalize_OtherTagTokenEdit_IsRetained`,
`Canonicalize_GeneratorShapes_AgreeOnOneForm`. A failure in any of
them means the repair widened the strip. Stop and fix the repair, not
the tests.

---

### Task 3: Falsifiers for the repaired walk

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`

**Interfaces:**
- Consumes: the repaired `ComputeLtcgiTagStripScope` from Task 2.
- Produces: falsifiers F7 to F13 as named tests. Mark each with the
  `--- Falsifier N: ... ---` comment form.

- [ ] **Step 1: Write the falsifier tests**

```csharp
// --- Falsifier F7: an Allman Pass block never grants SubShader
// scope. Kills a repair that pushes SubShader for any keyword or
// brace line. ---
[Test]
public void Canonicalize_AllmanPassScopeLtcgiToken_IsRetained()
{
    const string clean =
        "Shader \"s\" {\n" +
        "    SubShader\n" +
        "    {\n" +
        "        Pass\n" +
        "        {\n" +
        "            Tags {\"LightMode\" = \"ForwardBase\"}\n" +
        "        }\n" +
        "    }\n" +
        "}\n";
    const string tagged =
        "Shader \"s\" {\n" +
        "    SubShader\n" +
        "    {\n" +
        "        Pass\n" +
        "        {\n" +
        "            Tags {\"LightMode\" = \"ForwardBase\" \"LTCGI\"=\"ALWAYS\"}\n" +
        "        }\n" +
        "    }\n" +
        "}\n";

    Assert.That(Canon(tagged), Is.Not.EqualTo(Canon(clean)));
}

// --- Falsifier F8: an anonymous brace block grants no scope. Kills a
// repair that treats any bare brace line as SubShader. ---
[Test]
public void Canonicalize_AnonymousBraceBlockTagsLineLtcgiToken_IsRetained()
{
    const string tagged =
        "Shader \"s\" {\n" +
        "    {\n" +
        "        Tags {\"RenderType\" = \"Opaque\" \"LTCGI\"=\"ALWAYS\"}\n" +
        "    }\n" +
        "}\n";

    Assert.That(Canon(tagged), Does.Contain("\"LTCGI\"=\"ALWAYS\""));
}

// --- Falsifier F9: quoted SubShader text grants no scope. Kills a
// repair that matches the keyword by containment. ---
[Test]
public void Canonicalize_QuotedSubShaderTextDoesNotOpenSubShaderScope()
{
    const string tagged =
        "Shader \"s\" {\n" +
        "    // the text SubShader appears here\n" +
        "    \"SubShader\"\n" +
        "    {\n" +
        "        Tags {\"RenderType\" = \"Opaque\" \"LTCGI\"=\"ALWAYS\"}\n" +
        "    }\n" +
        "}\n";

    Assert.That(Canon(tagged), Does.Contain("\"LTCGI\"=\"ALWAYS\""));
}

// --- Falsifier F10: a content line between keyword and brace breaks
// the Allman pair. Kills a repair whose pending kind survives any
// intervening line. ---
[Test]
public void Canonicalize_ContentBetweenSubShaderKeywordAndBrace_BreaksEligibility()
{
    const string clean =
        "Shader \"s\" {\n" +
        "    SubShader\n" +
        "    {\n" +
        "        Tags {\"RenderType\" = \"Opaque\"}\n" +
        "    }\n" +
        "}\n";
    const string tagged =
        "Shader \"s\" {\n" +
        "    SubShader\n" +
        "    LOD 200\n" +
        "    {\n" +
        "        Tags {\"RenderType\" = \"Opaque\" \"LTCGI\"=\"ALWAYS\"}\n" +
        "    }\n" +
        "}\n";

    Assert.That(Canon(tagged), Is.Not.EqualTo(Canon(clean)));
}

// --- Falsifier F11: blank lines and comments between keyword and
// brace keep the pair. Kills a repair that clears the pending kind on
// blank or comment lines. ---
[Test]
public void Canonicalize_AllmanSubShaderBlankAndCommentLinesBeforeBrace_StillStrips()
{
    const string clean =
        "Shader \"s\" {\n" +
        "    SubShader\n" +
        "\n" +
        "    // a comment between keyword and brace\n" +
        "    {\n" +
        "        Tags {\"RenderType\" = \"Opaque\"}\n" +
        "    }\n" +
        "}\n";
    const string tagged =
        "Shader \"s\" {\n" +
        "    SubShader\n" +
        "\n" +
        "    // a comment between keyword and brace\n" +
        "    {\n" +
        "        Tags {\"RenderType\" = \"Opaque\" \"LTCGI\"=\"ALWAYS\"}\n" +
        "    }\n" +
        "}\n";

    Assert.That(Canon(tagged), Is.EqualTo(Canon(clean)));
}

// --- Falsifier F12: a different value or key stays hashed in Allman
// form. Kills a repair that strips any trailing quoted token. ---
[Test]
public void Canonicalize_AllmanSubShaderScopeTokenWithOtherLineEdits_IsRetained()
{
    const string clean =
        "Shader \"s\" {\n" +
        "    SubShader\n" +
        "    {\n" +
        "        Tags {\"RenderType\" = \"Opaque\"}\n" +
        "    }\n" +
        "}\n";
    const string otherValue =
        "Shader \"s\" {\n" +
        "    SubShader\n" +
        "    {\n" +
        "        Tags {\"RenderType\" = \"Opaque\" \"LTCGI\"=\"OFF\"}\n" +
        "    }\n" +
        "}\n";
    const string otherKey =
        "Shader \"s\" {\n" +
        "    SubShader\n" +
        "    {\n" +
        "        Tags {\"RenderType\" = \"Opaque\" \"LTCGIX\"=\"ALWAYS\"}\n" +
        "    }\n" +
        "}\n";

    Assert.That(Canon(otherValue), Is.Not.EqualTo(Canon(clean)));
    Assert.That(Canon(otherKey), Is.Not.EqualTo(Canon(clean)));
}
```

```csharp
// --- Falsifier F13: a SubShader keyword line followed by another
// block's inline brace line grants no SubShader scope to that block.
// Kills a repair whose pending kind attaches to any brace-carrying
// line. ---
[Test]
public void Canonicalize_SubShaderKeywordBeforeInlinePassBlock_DoesNotGrantScope()
{
    const string tagged =
        "Shader \"s\" {\n" +
        "    SubShader\n" +
        "    Pass {\n" +
        "        Tags {\"LightMode\" = \"ForwardBase\" \"LTCGI\"=\"ALWAYS\"}\n" +
        "    }\n" +
        "}\n";

    Assert.That(Canon(tagged), Does.Contain("\"LTCGI\"=\"ALWAYS\""));
}
```

- [ ] **Step 2: Run the seven falsifier tests**

Filter: `Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonAttestationTests`,
full class.

Expected: all falsifiers PASSED, the Task 1 test PASSED, and the
pre-existing tests PASSED. Record the observed counts.

- [ ] **Step 3: Wrong-implementation spot check**

Temporarily change the repair so the brace-line branch accepts any
pending kind (drop the `StartsWith("{")` guard). Run the class.
Expected: F13 fails. Restore the guard. Then remove the pending-clear
branch (the `else if` that clears `pendingSubShader` on a content
line). Run the class. Expected: F10 fails. Restore the repair. Run the
class again and confirm green. This proves the falsifiers detect
plausible wrong implementations.

---

### Task 4: Regression sweep and report

**Files:**
- No new changes. Validation only.

- [ ] **Step 1: Run the full product test assembly**

Run `Alrauna.Amuse.Tests.Editor` in EditMode. Expected: 0 failures.
Record the observed pass and fail counts.

- [ ] **Step 2: Check the consent-path tests specifically**

Confirm these stay green in the run from Step 1: the
`CollectTransferConsent` cases in
`Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`
and the consent pre-scan boundary cases in
`Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`.
No consent wording or subject count changes in this repair.

- [ ] **Step 3: Whitespace and diff check**

Run `git diff --check` and inspect the full diff of the two changed
files. Confirm the diff contains only the function, its comment, and
the new tests.

- [ ] **Step 4: Report**

Report: observed test counts per run, the Task 1 RED failure message,
the Task 2 GREEN result, and the diff summary. No commit without
explicit authorization.

---

### Task 5: Optional read-only Census Lab characterization

Run this task only when the requesting session asks for Lab-level
validation. Read-only. Never run tests there. Never write there.

- [ ] **Step 1: Identity gate**

Enumerate reachable editor instances read-only. Select the Census Lab
editor instance. In-editor, require an exact match of
`Application.dataPath` to the Census Lab project `Assets` folder
before any other call. A mismatch stops the task.

- [ ] **Step 2: Production canonicalizer check**

Through reflection, run the production `AnalyzeCanonicalization` and
`NormalizedSourceHash.Compute` over the installed
`jp.lilxyzw.liltoon` package files. Expected on the repaired code:
the base `lts.shader` canonical digest equals the pinned
`ShaderCanonicalDigest`
`5206bec25e82db5f8009b27fcc5ba94d7c41113031d4b6b0a2c25ca324a9c704`,
and the pass digest equals its pin, as measured on 2026-10-10.

- [ ] **Step 3: Consent pre-scan check**

Through reflection, call
`UnityMaterialSemantics.CollectTransferConsent` over the assigned
materials of one loaded avatar. Expected: no subject names shader
`lilToon`. Other subjects, if any, come from other materials and stay
unchanged. Record observed counts only. Sanitize the record: role
names only, no material, asset, or hierarchy names, no absolute
paths, no instance identifiers.
