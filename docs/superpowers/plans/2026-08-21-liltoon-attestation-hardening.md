# lilToon Attestation Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing standalone lilToon 2.3.4 attestation accept a canonical digest only when every R1-erased source record belongs to the closed official generator-emittable language, every external activator satisfies the standalone trust constraints, and all existing source/digest evidence still matches.

**Architecture:** Refactor the existing R1 walk into one analysis that returns both unchanged canonical text and immutable raw provenance evidence. Carry the base/pass analyses in `LilToonSourceEvidence`, then apply one fixed standalone validator before the existing digest and semantic checks. This proves membership and bounded activation, not a historical live generator invocation or a reconstruction of every compile-symbol/environment input. No profile framework, dependency registry, or HLSL parser is added.

**Tech Stack:** Unity 2022.3, C#, NUnit EditMode tests, existing `Alrauna.Amuse.Editor` and `Alrauna.Amuse.Tests.Editor` assemblies. No new dependency, assembly, asset, `.meta`, or package metadata change.

**Spec:** `docs/superpowers/specs/2026-08-21-liltoon-attestation-hardening-design.md`

## Global Constraints

- Supported profile remains canonical upstream `jp.lilxyzw.liltoon` `2.3.4`, BRP base opaque, standalone only.
- Existing shader name/GUID, pass name/GUID, exact `_lilToonVersion == 45f`, package evidence, `LIL_RENDER == 0`, and output-local semantic checks remain unchanged.
- Existing pins remain byte-identical:
  - base: `5206bec25e82db5f8009b27fcc5ba94d7c41113031d4b6b0a2c25ca324a9c704`
  - pass: `6b6c30c1cbe546fe753bcdc77f547441e3f9114ee80e9591bde2b8e6e7e5eb14`
  - include tree: `6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46`
- R1/R2/R3 canonical output must remain unchanged. Provenance validation is an additional conjunct, not a new canonical form.
- “Provenance” means membership in the closed official lilToon 2.3.4 generator-emittable language plus the selected profile's trust constraints. Do not claim historical invocation or complete environment reconstruction.
- The exact official R1 define domain is the 109-identifier list in the spec. Do not replace it with prefix matching or reflection against an installed lilToon assembly.
- Standalone forbids `LIL_FEATURE_VRCLIGHTVOLUMES`, `LIL_FEATURE_AUDIOLINK_PACKAGE`, and `LIL_FEATURE_LTCGI` in either raw source.
- `LIL_FEATURE_VRCLIGHTVOLUMES_WITHOUTPACKAGE` remains a valid bundled-source variation.
- Unknown or structurally invalid evidence fails closed before semantic interpretation.
- Do not add LTCGI, AudioLink package, or external VRC Light Volumes positive support.
- Do not modify `LilToonMaterialSemantics.cs`, `UnityMaterialSemantics.cs`, semantic value types, census code, NDMF integration, or package metadata.
- Do not commit, push, or open a PR without separate authorization.
- Before every reported Unity test result, discover instances read-only and select only the instance whose normalized, case-exact `Application.dataPath` equals the normalized `<repo-root>/Assets`.
- Never use or modify the Census Lab for this plan.
- Inspect the complete `Packages/manifest.json` and `Packages/packages-lock.json` diff before restoring any Unity-generated host toolchain/sysroot churn. Restore only when the entire relevant diff is exactly the prohibited machine-generated state described by `AGENTS.md`.

## File map

| File | Change | Responsibility |
| --- | --- | --- |
| `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs` | Modify | R1 analysis result, raw provenance extraction, exact generator grammar, standalone policy, evidence wiring, existing canonicalization and pins. |
| `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs` | Modify | Extraction, unchanged canonicalization, exact default/stripped records, malicious digest-equivalent witnesses, refusal diagnostics, unchanged pins. |

No new file or Unity asset is planned.

## Test execution

Focused run:

```text
mode: EditMode
test_names: Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonAttestationTests
include_failed_tests: true
```

All lilToon semantic tests:

```text
mode: EditMode
test_names:
  Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonAttestationTests
  Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonBaseColorTests
  Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonAlphaTests
  Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonEmissionTests
  Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonNormalTests
  Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonAdversarialTests
include_failed_tests: true
```

Final run: the complete public EditMode suite with no filter.

---

### Task 1: Extract lossless R1 provenance without changing canonicalization

**Files:**

- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`

**Interfaces:**

- Consumes: existing `Normalize`, `SettingDefine`, `SkipVariants`, `IsShadowSlotExpansion`, and `NormalizeIncludeLine` behavior.
- Produces:

```csharp
internal enum LilToonRemovedRecordKind
{
    Define,
    SkipVariants,
}

internal readonly struct LilToonRemovedRecord
{
    internal int LineIndex { get; }
    internal int OffsetInRegion { get; }
    internal LilToonRemovedRecordKind Kind { get; }
    internal string Text { get; }
}

internal sealed class LilToonRemovedRegion
{
    internal int HlslIncludeOrdinal { get; }
    internal int HlslIncludeLineIndex { get; }
    internal IReadOnlyList<LilToonRemovedRecord> Records { get; }
}

internal readonly struct LilToonActivatorOccurrence
{
    internal int LineIndex { get; }
    internal string Identifier { get; }
    internal string Text { get; }
}

internal sealed class LilToonCanonicalizationAnalysis
{
    internal string CanonicalSource { get; }
    internal IReadOnlyList<LilToonRemovedRegion> RemovedRegions { get; }
    internal IReadOnlyList<LilToonActivatorOccurrence> Activators { get; }
}

internal static LilToonCanonicalizationAnalysis AnalyzeCanonicalization(
    string rawShaderSource,
    string shaderDirectory,
    string projectRoot,
    LilToonIncludeTree includeTree);
```

- Preserves: the existing four-parameter `Canonicalize` signature and exact returned string.

- [ ] **Step 1: Add failing extraction tests**

Add a helper beside `Canon`:

```csharp
using System.Linq;

private static LilToonCanonicalizationAnalysis Analyze(string source)
{
    return LilToonSourceAttestation.AnalyzeCanonicalization(
        source, ShaderDir, ProjectRoot, Tree());
}
```

Add focused tests with these exact behaviors:

```csharp
[Test]
public void AnalyzeCanonicalization_RecordsEveryHlslIncludeRegionInOrder()
{
    const string source =
        "HLSLINCLUDE\n" +
        "    #define LIL_RENDER 0\n" +
        "ENDHLSL\n" +
        "HLSLINCLUDE\n" +
        "    #define LIL_FEATURE_MAIN2ND\n" +
        "    #pragma skip_variants LIGHTPROBE_SH\n" +
        "    #pragma target 3.5\n" +
        "ENDHLSL\n";

    var analysis = Analyze(source);

    Assert.That(analysis.RemovedRegions, Has.Count.EqualTo(2));
    Assert.That(analysis.RemovedRegions[0].HlslIncludeOrdinal, Is.EqualTo(0));
    Assert.That(analysis.RemovedRegions[0].HlslIncludeLineIndex, Is.EqualTo(0));
    Assert.That(analysis.RemovedRegions[0].Records, Is.Empty);
    Assert.That(analysis.RemovedRegions[1].HlslIncludeOrdinal, Is.EqualTo(1));
    Assert.That(analysis.RemovedRegions[1].HlslIncludeLineIndex, Is.EqualTo(3));
    Assert.That(analysis.RemovedRegions[1].Records, Has.Count.EqualTo(2));
    Assert.That(analysis.RemovedRegions[1].Records[0].LineIndex, Is.EqualTo(4));
    Assert.That(analysis.RemovedRegions[1].Records[0].OffsetInRegion, Is.EqualTo(0));
    Assert.That(
        analysis.RemovedRegions[1].Records[0].Kind,
        Is.EqualTo(LilToonRemovedRecordKind.Define));
    Assert.That(
        analysis.RemovedRegions[1].Records[0].Text,
        Is.EqualTo("#define LIL_FEATURE_MAIN2ND"));
    Assert.That(
        analysis.RemovedRegions[1].Records[1].Kind,
        Is.EqualTo(LilToonRemovedRecordKind.SkipVariants));
}
```

```csharp
[Test]
public void AnalyzeCanonicalization_RecordsKnownActivatorsAnywhere()
{
    const string source =
        "#define LIL_FEATURE_LTCGI\n" +
        "HLSLINCLUDE\n" +
        "  #define  LIL_FEATURE_AUDIOLINK_PACKAGE\n" +
        "  #pragma target 3.5\n" +
        "#define LIL_FEATURE_VRCLIGHTVOLUMES 1\n";

    var analysis = Analyze(source);

    Assert.That(
        analysis.Activators.Select(value => value.Identifier),
        Is.EqualTo(new[]
        {
            "LIL_FEATURE_LTCGI",
            "LIL_FEATURE_AUDIOLINK_PACKAGE",
            "LIL_FEATURE_VRCLIGHTVOLUMES",
        }));
    Assert.That(analysis.Activators.Select(value => value.LineIndex),
        Is.EqualTo(new[] { 0, 2, 4 }));
}
```

Add a canonical-output equivalence test proving the analyzer retains the old R1 behavior:

```csharp
[Test]
public void AnalyzeCanonicalization_HiddenUnknownRecordKeepsOldCanonicalOutput()
{
    const string clean =
        "HLSLINCLUDE\n" +
        "    #define LIL_FEATURE_MAIN2ND\n" +
        "    #pragma target 3.5\n";
    const string mutated =
        "HLSLINCLUDE\n" +
        "    #define LIL_FEATURE_MAIN2ND\n" +
        "    #define LIL_FEATURE_AMUSE_UNKNOWN\n" +
        "    #pragma target 3.5\n";

    Assert.That(
        Analyze(mutated).CanonicalSource,
        Is.EqualTo(Analyze(clean).CanonicalSource));
    Assert.That(
        Analyze(mutated).RemovedRegions[0].Records.Select(value => value.Text),
        Does.Contain("#define LIL_FEATURE_AMUSE_UNKNOWN"));
}
```

The activator scanner must match define directives with arbitrary whitespace and optional trailing tokens, but not comments or ordinary mentions. Add negative scanner assertions for:

```text
// #define LIL_FEATURE_LTCGI
const char* name = "LIL_FEATURE_LTCGI";
#undef LIL_FEATURE_LTCGI
```

- [ ] **Step 2: Run the focused test and verify RED**

Refresh Unity, then run `LilToonAttestationTests`.

Expected: compile failure because `AnalyzeCanonicalization` and the provenance types do not exist. Fix test syntax only until the failure is solely the missing production API.

- [ ] **Step 3: Implement the immutable evidence values**

Add the types beside `LilToonSourceEvidence`. Defensively copy each incoming list, then wrap the private copy in `ReadOnlyCollection<T>` (or an equivalently non-castable immutable collection) before exposing it as `IReadOnlyList<T>`. Reject null lists in constructors. Do not expose a caller-castable backing array, retain a caller-owned `List<T>`, or return a mutable collection through an interface.

Add a focused immutability test that mutates the constructor's input list after construction and proves the evidence is unchanged, then asserts the exposed collection cannot be cast to either `T[]` or `IList<T>` with a working mutator.

Use zero-based normalized line indices. Store trimmed raw record text; do not normalize internal whitespace.

- [ ] **Step 4: Extract analysis in the existing R1 walk**

Refactor the existing `Canonicalize` body into `AnalyzeCanonicalization`:

```csharp
internal static string Canonicalize(
    string rawShaderSource,
    string shaderDirectory,
    string projectRoot,
    LilToonIncludeTree includeTree)
{
    return AnalyzeCanonicalization(
        rawShaderSource, shaderDirectory, projectRoot, includeTree)
        .CanonicalSource;
}
```

Inside `AnalyzeCanonicalization`:

1. keep the existing null guards and `Normalize(rawShaderSource).Split('\n')`;
2. keep the existing `inSettingRegion` array;
3. when an exact trimmed `HLSLINCLUDE` is found, create one region with the next ordinal even when its run is empty;
4. mark the same maximal contiguous `SettingDefine`/`SkipVariants` run as today;
5. record each marked line's normalized index, zero-based offset, kind, and trimmed text;
6. scan every normalized raw line for a define of one of the three activator identifiers, including valued/tail forms;
7. emit canonical text with the existing R1 removal, R2 slot check, newline behavior, and R3 normalization unchanged.

The activator regex is closed:

```csharp
private static readonly Regex ExternalActivatorDefine = new Regex(
    @"^#define\s+(?<identifier>" +
    @"LIL_FEATURE_VRCLIGHTVOLUMES|" +
    @"LIL_FEATURE_AUDIOLINK_PACKAGE|" +
    @"LIL_FEATURE_LTCGI)(?:\s.*)?$",
    RegexOptions.Compiled);
```

Do not add substring matching or scan comments.

- [ ] **Step 5: Run the focused test and verify GREEN**

Run `LilToonAttestationTests`.

Expected: all existing canonicalization tests and the new extraction tests pass. Record the exact pass/fail count.

- [ ] **Step 6: Inspect the task diff**

```bash
git diff --check
git diff -- Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs
git status --short
```

Confirm no evidence field, verifier behavior, pin, or package file changed yet. Do not commit.

---

### Task 2: Enforce the exact standalone generator/provenance predicate

**Files:**

- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`

**Interfaces:**

- Consumes: `LilToonCanonicalizationAnalysis` from Task 1 and the exact 109-identifier/three-pragma grammar from the spec.
- Extends `LilToonSourceEvidence` with:

```csharp
internal LilToonCanonicalizationAnalysis ShaderCanonicalization { get; }
internal LilToonCanonicalizationAnalysis PassCanonicalization { get; }
```

- Produces one private fixed-profile validator:

```csharp
private static bool TryVerifyStandaloneCanonicalizationProvenance(
    LilToonCanonicalizationAnalysis shader,
    LilToonCanonicalizationAnalysis pass,
    out LilToonSemanticDiagnostic diagnostic);
```

No public/internal profile enum, registry, interface, or provider is produced.

- [ ] **Step 1: Make the test evidence helper carry real analysis**

Add source builders:

```csharp
private static LilToonCanonicalizationAnalysis EmptyShaderAnalysis()
{
    return Analyze("Shader \"lilToon\"\n{\n}\n");
}

private static LilToonCanonicalizationAnalysis PassAnalysis(
    IEnumerable<string> settingRecords,
    string beforeSecondEnd = null)
{
    return Analyze(
        "HLSLINCLUDE\n" +
        "    #define LIL_RENDER 0\n" +
        "ENDHLSL\n" +
        "HLSLINCLUDE\n" +
        string.Join("\n", settingRecords.Select(line => "    " + line)) +
        "\n    #pragma target 3.5\n" +
        (beforeSecondEnd ?? string.Empty) +
        "ENDHLSL\n");
}
```

Extend `Evidence` with analysis values plus explicit presence flags:

```csharp
bool hasShaderCanonicalization = true,
LilToonCanonicalizationAnalysis shaderCanonicalization = null,
bool hasPassCanonicalization = true,
LilToonCanonicalizationAnalysis passCanonicalization = null
```

When the corresponding flag is true, coalesce a null value to `EmptyShaderAnalysis()` or `PassAnalysis(DefaultStandaloneRecords())`. When it is false, pass null into `LilToonSourceEvidence`. This preserves concise defaults while allowing missing provenance to be tested without another sentinel type.

`DefaultStandaloneRecords()` is a test-owned literal sequence, not a call into the production allow-list. Copy the exact 103-record sequence characterized in the spec and independently confirmed against genuine official 2.3.4 output:

- all 100 feature-setting identifiers in spec order except `LIL_FEATURE_CLIPPING_CANCELLER`;
- `LIL_OPTIMIZE_APPLY_SHADOW_FA`;
- `LIL_OPTIMIZE_USE_FORWARDADD`;
- `LIL_OPTIMIZE_USE_VERTEXLIGHT`;
- `#pragma skip_variants _MIXED_LIGHTING_SUBTRACTIVE`.

Assert the helper's count is 103 so accidental omissions fail visibly.

`StrippedStandaloneRecords()` filters that literal sequence by removing these twelve default records:

```text
#define LIL_FEATURE_RECEIVE_SHADOW
#define LIL_FEATURE_EMISSION_1ST
#define LIL_FEATURE_EMISSION_2ND
#define LIL_FEATURE_ANIMATE_EMISSION_UV
#define LIL_FEATURE_ANIMATE_EMISSION_MASK_UV
#define LIL_FEATURE_EMISSION_GRADATION
#define LIL_FEATURE_NORMAL_1ST
#define LIL_FEATURE_NORMAL_2ND
#define LIL_FEATURE_BACKLIGHT
#define LIL_FEATURE_OUTLINE_RECEIVE_SHADOW
#define LIL_FEATURE_BumpMap
#define LIL_FEATURE_EmissionMap
```

Assert the stripped helper's count is 91.

- [ ] **Step 2: Add failing positive characterization tests**

```csharp
[Test]
public void Verify_DefaultStandaloneGeneratorRecord_Succeeds()
{
    var analysis = PassAnalysis(DefaultStandaloneRecords());
    Assert.That(analysis.RemovedRegions[1].Records, Has.Count.EqualTo(103));

    Assert.That(
        LilToonSourceAttestation.TryVerifyLilToonIdentity(
            Evidence(passCanonicalization: analysis), out var diagnostic),
        Is.True);
    Assert.That(diagnostic, Is.Null);
}

[Test]
public void Verify_StrippedStandaloneGeneratorRecord_Succeeds()
{
    var analysis = PassAnalysis(StrippedStandaloneRecords());
    Assert.That(analysis.RemovedRegions[1].Records, Has.Count.EqualTo(91));

    Assert.That(
        LilToonSourceAttestation.TryVerifyLilToonIdentity(
            Evidence(passCanonicalization: analysis), out var diagnostic),
        Is.True);
    Assert.That(diagnostic, Is.Null);
}
```

Add positive standalone grammar witnesses, each in exact generator order, for:

- `LIL_FEATURE_PARALLAX` together with dependent `LIL_FEATURE_POM`;
- `LIL_FEATURE_CLIPPING_CANCELLER`;
- `LIL_OPTIMIZE_USE_FORWARDADD_SHADOW`;
- `LIL_OPTIMIZE_USE_LIGHTMAP` with `#pragma skip_variants _MIXED_LIGHTING_SUBTRACTIVE` absent, plus the inverse legal state with the optimize define absent and the pragma present;
- `LIL_FEATURE_VRCLIGHTVOLUMES_WITHOUTPACKAGE` in its exact post-optimizer slot; and
- `LIL_INPUT_OPTIMIZED` last.

These are positive closed-domain witnesses intended to catch accidental omissions and ordering mistakes without weakening any validation rule. They may share a compact parameterized helper where that keeps each expected sequence obvious.

- [ ] **Step 3: Add failing digest-equivalent attack witnesses**

Use one parameterized test for the three known activators:

```csharp
[TestCase("LIL_FEATURE_VRCLIGHTVOLUMES")]
[TestCase("LIL_FEATURE_AUDIOLINK_PACKAGE")]
[TestCase("LIL_FEATURE_LTCGI")]
public void Verify_HiddenExternalActivatorWithOldCanonicalOutput_IsRefused(
    string identifier)
{
    var clean = PassAnalysis(DefaultStandaloneRecords());
    var mutatedRecords = DefaultStandaloneRecords().ToList();
    mutatedRecords.Insert(1, "#define " + identifier);
    var mutated = PassAnalysis(mutatedRecords);

    Assert.That(mutated.CanonicalSource, Is.EqualTo(clean.CanonicalSource));
    Assert.That(
        LilToonSourceAttestation.TryVerifyLilToonIdentity(
            Evidence(passCanonicalization: mutated), out var diagnostic),
        Is.False);
    Assert.That(
        diagnostic.Code,
        Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShaderVariant));
    Assert.That(diagnostic.Detail, Does.Contain(identifier));
}
```

Add the same canonical-equality/refusal shape for:

```text
#define LIL_FEATURE_AMUSE_UNKNOWN
#define LIL_OPTIMIZE_AMUSE_UNKNOWN
#pragma skip_variants AMUSE_UNKNOWN
#pragma skip_variants LIGHTPROBE_SH AMUSE_UNKNOWN
```

These use `ModifiedShaderSource`, not `UnsupportedShaderVariant`.

- [ ] **Step 4: Add failing structural and global-absence tests**

Add one focused test per behavior:

- duplicate `LIL_FEATURE_MAIN2ND` refuses;
- swap two adjacent known internal records and refuse;
- remove each of the three mandatory noise records using `[TestCase]` and refuse;
- add a child without its required parent for each dependency group using `[TestCaseSource]` and refuse;
- mismatch each inverse skip rule using `[TestCaseSource]` and refuse;
- place `LIL_INPUT_OPTIMIZED` before a pragma and refuse;
- include both Light Volumes identifiers and refuse;
- put a valid internal setting record in region 0 before `LIL_RENDER` and refuse;
- give a known token a double internal space (`#define  LIL_FEATURE_MAIN2ND`) and refuse;
- pass a null shader or pass analysis and receive `MissingSourceEvidence`;
- place each known activator outside R1 in either source and receive `UnsupportedShaderVariant`;
- duplicate and relocate a known activator within region 1 and refuse while canonical output remains equal to clean.

The dependency cases are exactly:

```csharp
private static readonly (string Child, string[] Parents)[] GeneratorDependencies =
{
    ("LIL_FEATURE_DECAL", new[] { "LIL_FEATURE_MAIN2ND", "LIL_FEATURE_MAIN3RD" }),
    ("LIL_FEATURE_ANIMATE_DECAL", new[] { "LIL_FEATURE_MAIN2ND", "LIL_FEATURE_MAIN3RD" }),
    ("LIL_FEATURE_LAYER_DISSOLVE", new[] { "LIL_FEATURE_MAIN2ND", "LIL_FEATURE_MAIN3RD" }),
    ("LIL_FEATURE_RECEIVE_SHADOW", new[] { "LIL_FEATURE_SHADOW" }),
    ("LIL_FEATURE_SHADOW_3RD", new[] { "LIL_FEATURE_SHADOW" }),
    ("LIL_FEATURE_SHADOW_LUT", new[] { "LIL_FEATURE_SHADOW" }),
    ("LIL_FEATURE_ANIMATE_EMISSION_UV", new[] { "LIL_FEATURE_EMISSION_1ST", "LIL_FEATURE_EMISSION_2ND" }),
    ("LIL_FEATURE_ANIMATE_EMISSION_MASK_UV", new[] { "LIL_FEATURE_EMISSION_1ST", "LIL_FEATURE_EMISSION_2ND" }),
    ("LIL_FEATURE_EMISSION_GRADATION", new[] { "LIL_FEATURE_EMISSION_1ST", "LIL_FEATURE_EMISSION_2ND" }),
    ("LIL_FEATURE_RIMLIGHT_DIRECTION", new[] { "LIL_FEATURE_RIMLIGHT" }),
    ("LIL_FEATURE_POM", new[] { "LIL_FEATURE_PARALLAX" }),
    ("LIL_FEATURE_AUDIOLINK_VERTEX", new[] { "LIL_FEATURE_AUDIOLINK" }),
    ("LIL_FEATURE_AUDIOLINK_LOCAL", new[] { "LIL_FEATURE_AUDIOLINK" }),
};
```

- [ ] **Step 5: Run the focused test and verify RED**

Run `LilToonAttestationTests`.

Expected: compile failure because `LilToonSourceEvidence` does not yet accept the two analysis fields, or—after only test syntax is corrected—verification failures because the existing verifier ignores provenance. Do not weaken tests to make them compile against the old constructor.

- [ ] **Step 6: Add analysis fields to source evidence and live extraction**

Extend the constructor and immutable properties. In `GatherSourceEvidence`, analyze each readable source once and take both values from the result:

```csharp
var shaderAnalysis = shaderText == null
    ? null
    : AnalyzeCanonicalization(
        shaderText, shaderDirectory, projectRoot, includeTree);
var shaderDigest = shaderAnalysis == null
    ? null
    : Sha256(shaderAnalysis.CanonicalSource);
```

Do the same for `passText`, using the pass directory. Pass both analyses into `LilToonSourceEvidence`. Do not call `Canonicalize` a second time.

- [ ] **Step 7: Implement the fixed generator grammar**

In `LilToonSourceAttestation`, add one exact ordered array containing the 109 identifiers from the spec. The first item is `LIL_FEATURE_ANIMATE_MAIN_UV`; the last four positions are the two alternative Light Volumes identifiers, `LIL_FEATURE_AUDIOLINK_PACKAGE`, and `LIL_INPUT_OPTIMIZED` after the five optimizer identifiers. Copy every identifier verbatim and assert the array has 109 unique ordinal entries in a focused test.

Add the exact pragma array:

```csharp
private static readonly string[] OfficialSkipVariantRecords =
{
    "#pragma skip_variants _REFLECTION_PROBE_BOX_PROJECTION",
    "#pragma skip_variants LIGHTPROBE_SH",
    "#pragma skip_variants _MIXED_LIGHTING_SUBTRACTIVE",
};
```

Validate the setting record in one linear pass:

1. require exact `#define <identifier>` or one of the three exact pragma lines;
2. require strictly increasing generator order, treating the two Light Volumes alternatives as one mutually exclusive slot;
3. reject duplicates and unknown identifiers;
4. require the three unconditional noise identifiers;
5. enforce the dependency table from the spec;
6. enforce pragma order and the exact inverse relationships;
7. require `LIL_INPUT_OPTIMIZED`, when present, to be last.

Use `HashSet<string>(StringComparer.Ordinal)` only for membership/duplicate checks. Do not reduce the source record itself to a set; order and location remain authoritative.

- [ ] **Step 8: Implement standalone source positions and activation policy**

`TryVerifyStandaloneCanonicalizationProvenance` applies checks in this order:

```text
missing analysis
known activator occurrence in either source
base has zero HLSLINCLUDE regions
pass has exactly two regions
pass region 0 is empty
pass region 1 satisfies the exact official generator grammar
```

Known activator failure:

```csharp
diagnostic = MaterialDiagnostic(
    LilToonSemanticDiagnosticCode.UnsupportedShaderVariant,
    occurrence.Identifier);
```

Malformed/untrusted structure failure:

```csharp
diagnostic = MaterialDiagnostic(
    LilToonSemanticDiagnosticCode.ModifiedShaderSource,
    PassShaderName + " canonicalization provenance");
```

Missing analysis uses `MissingSourceEvidence` and the same source detail.

Call the validator in `TryVerifyLilToonIdentity` after the existing pass-GUID check and before the existing include/base/pass digest conjunction. Do not change earlier first-failure behavior or any later check.

- [ ] **Step 9: Run the focused test and verify GREEN**

Run `LilToonAttestationTests`.

Expected: every existing and new test passes. Record the exact pass/fail count and inspect every warning/error, not only the final count.

- [ ] **Step 10: Re-run attack tests with a deliberate policy bypass to prove RED/GREEN**

Temporarily make `TryVerifyStandaloneCanonicalizationProvenance` return true after its null guard, run only the hidden known/unknown and structural tests, and observe the digest-equivalent witnesses fail because the verifier accepts them. Restore the validator and rerun the same tests to green.

Do not retain the bypass. This is the regression-test red/green proof required by `verification-before-completion` for a security fix.

- [ ] **Step 11: Inspect the task diff**

```bash
git diff --check
git diff --stat
git diff -- Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs
git status --short
```

Confirm:

- only the planned source/test files and the approved design/plan documents changed;
- all three pin strings are untouched;
- no semantic interpreter, package metadata, or Unity-generated state changed;
- no new abstraction or integration support entered the diff.

Do not commit.

---

### Task 3: Full regression and repository safety gate

**Files:** no planned code changes. Any correction returns to the relevant red/green task.

**Interfaces:** verifies the complete public behavior and repository scope.

- [ ] **Step 1: Re-read requirements against the implementation**

Check each design invariant explicitly:

```text
canonical output unchanged
three pins unchanged
complete R1 evidence retained
109-identifier closed domain
three exact pragma forms
positions/counts/order/duplicates validated
three external activators absent globally
default 103-record state accepted
stripped 91-record state accepted
old digest-equivalent witnesses refused
existing output-local semantics unchanged
future record shape does not encode standalone absence
no positive integration support or framework
```

If any item cannot be tied to code and a test, return to Task 2. Do not add a prose-only exception.

- [ ] **Step 2: Run all lilToon tests**

Use the six-class lilToon EditMode run listed under **Test execution**.

Expected: zero failures and zero skipped tests. Record the exact counts.

- [ ] **Step 3: Run the complete public EditMode suite**

Run all EditMode tests with no filter in the positively identified public project.

Expected: zero failures. Record passed, failed, skipped, and duration exactly as reported.

- [ ] **Step 4: Inspect Unity package churn before any restore**

```bash
git diff -- Packages/manifest.json Packages/packages-lock.json
```

If the diff is empty, continue. If it contains only the prohibited host-specific `com.unity.toolchain.macos-arm64-linux-x86_64` and transitive `com.unity.sysroot*` state, record that finding and restore only those two whole files from `HEAD` as repository policy permits. If either file contains any additional change, stop and request direction; do not restore blindly.

- [ ] **Step 5: Run final static and Git verification**

```bash
git diff --check
git status --short --branch
git diff --stat
git diff --name-status
git diff --cached --stat
git diff -- Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs docs/superpowers/specs/2026-08-21-liltoon-attestation-hardening-design.md docs/superpowers/plans/2026-08-21-liltoon-attestation-hardening.md
```

Expected unstaged paths:

```text
Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs
Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs
docs/superpowers/specs/2026-08-21-liltoon-attestation-hardening-design.md
docs/superpowers/plans/2026-08-21-liltoon-attestation-hardening.md
```

Expected staged diff: empty. Do not stage or commit without separate authorization.

- [ ] **Step 6: Report the implementation review gate**

Report:

- branch/base state;
- exact files changed;
- the Layer-2 representation and standalone invariant implemented;
- the red/green digest-equivalent witnesses observed;
- focused, lilToon-wide, and full-suite test counts;
- skipped validation and remaining risks;
- whether Unity generated prohibited package churn and how it was handled;
- that the Census Lab was not used or modified;
- final `git status`;
- a request for review before any commit, push, or integration work.

Stop at that gate.
