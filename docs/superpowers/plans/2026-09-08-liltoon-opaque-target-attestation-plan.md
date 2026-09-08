# lilToon Opaque Target Attestation and Evidence Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix lilToon opaque target source attestation by passing the target shader name to the evidence gatherer, registering transparent profile lookup, routing outline wrappers to the canonical opaque outline shader, and retaining `_lilToonVersion` in conversion evidence requests.

**Architecture:** `LilToonSourceAttestation.GatherSourceEvidenceForShaderName` provides `shader.name` as `shaderNameOverride` so gathered target evidence aligns the target shader name with its asset GUID. `ProfileForShaderName` and `ResolveCanonicalTargetShaderName` complete family routing for transparent and outline wrappers. `LilToonTransparentSourceEligibility` and `LilToonCutoutSourceEligibility` include `_lilToonVersion` in their source schema so projected derived evidence preserves the format version required for source attestation.

**Tech Stack:** C# against Unity 2022.3, NUnit (Unity Test Framework EditMode), NDMF.

**Spec:** `docs/superpowers/specs/2026-09-08-liltoon-opaque-target-attestation-design.md`

## Global Constraints

- Fail closed: uncharacterized or unattested shaders refuse.
- Never mutate source assets; generated build assets and the NDMF build copy are the only mutation targets.
- Do not introduce public API changes; keep production types internal to `Alrauna.Amuse.Editor`.
- No absolute paths, host names, ports, or private avatar identifiers in any file or commit message.
- Every English text that a human reads uses ASD-STE100 Simplified Technical English.

---

## File Structure

- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
  - Pass `shader.name` as `shaderNameOverride` in `GatherSourceEvidenceForShaderName`.
  - Add `TransparentShaderName` to `ProfileForShaderName`.
  - Return `OutlineShaderName` for `IsOutlineWrapperShaderName` in `ResolveCanonicalTargetShaderName`.
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentSourceEligibility.cs`
  - Add `LilToonSourceAttestation.ShaderFormatVersionProperty` to `SourceSchema`.
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs`
  - Add `LilToonSourceAttestation.ShaderFormatVersionProperty` to `SourceSchema`.
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`
  - Add unit tests for `ProfileForShaderName` and `ResolveCanonicalTargetShaderName`.
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`
  - Add unit tests for `GatherSourceEvidenceForShaderName` target name alignment and `PrepareCanonicalOpaqueClone`.
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentSourceEligibilityTests.cs`
  - Add unit test verifying `ConversionEvidenceRequest` contains `_lilToonVersion`.
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutSourceEligibilityTests.cs`
  - Add unit test verifying `ConversionEvidenceRequest` contains `_lilToonVersion`.

---

### Task 1: Target Evidence Name Override and Profile/Name Resolution

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:1670-1815`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`

**Interfaces:**
- Consumes: `LilToonSourceAttestation.GatherSourceEvidenceForShaderName`, `LilToonSourceAttestation.ProfileForShaderName`, `LilToonSourceAttestation.ResolveCanonicalTargetShaderName`.
- Produces: Corrected evidence gathering that sets `LilToonSourceEvidence.ShaderName` to the target shader name, maps `TransparentShaderName` to `TransparentProfile`, and maps outline wrappers to `OutlineShaderName`.

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`:
Add tests:
```csharp
[Test]
public void ProfileForShaderName_TransparentShader_ReturnsTransparentProfile()
{
    var m = typeof(LilToonSourceAttestation).GetMethod(
        "ProfileForShaderName",
        BindingFlags.NonPublic | BindingFlags.Static);
    Assert.That(m, Is.Not.Null);

    var profile = m.Invoke(null, new object[] { LilToonSourceAttestation.TransparentShaderName });
    Assert.That(profile, Is.Not.Null);

    var nameProp = profile.GetType().GetProperty("ShaderName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.That(nameProp.GetValue(profile), Is.EqualTo(LilToonSourceAttestation.TransparentShaderName));
}

[Test]
public void ResolveCanonicalTargetShaderName_OutlineWrappers_ReturnsOutlineOpaqueShader()
{
    Assert.That(
        LilToonSourceAttestation.ResolveCanonicalTargetShaderName(LilToonSourceAttestation.OutlineCutoutShaderName),
        Is.EqualTo(LilToonSourceAttestation.OutlineShaderName));
    Assert.That(
        LilToonSourceAttestation.ResolveCanonicalTargetShaderName(LilToonSourceAttestation.OutlineTransparentShaderName),
        Is.EqualTo(LilToonSourceAttestation.OutlineShaderName));
}
```

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`:
Add test:
```csharp
[Test]
public void GatherSourceEvidenceForShaderName_UsesTargetShaderName_NotSourceEvidenceShaderName()
{
    var source = Track(ConversionEligibleStandIn());
    var captured = UnityMaterialEvidenceCapture.Capture(new[]
    {
        new MaterialEvidenceCaptureInput(
            source,
            LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
    })[0];
    var target = Shader.Find(OpaqueConversionShaderName);
    Assert.That(target, Is.Not.Null);

    var targetEvidence =
        LilToonSourceAttestation.GatherSourceEvidenceForShaderName(
            target, captured);

    Assert.That(targetEvidence.ShaderName, Is.EqualTo(target.name));
    Assert.That(targetEvidence.ShaderName, Is.Not.EqualTo(captured.ShaderName));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run tests via Unity MCP test runner or reflection execution.
Expected: `ResolveCanonicalTargetShaderName_OutlineWrappers_ReturnsOutlineOpaqueShader` fails (returns cutout outline instead of opaque outline), and `GatherSourceEvidenceForShaderName_UsesTargetShaderName_NotSourceEvidenceShaderName` fails (receives source shader name).

- [ ] **Step 3: Implement minimal fix in LilToonSourceAttestation.cs**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`:

1. Update `ProfileForShaderName`:
```csharp
            if (string.Equals(
                    shaderName,
                    TransparentShaderName,
                    StringComparison.Ordinal))
            {
                return TransparentProfile;
            }
```

2. Update `GatherSourceEvidenceForShaderName`:
```csharp
        internal static LilToonSourceEvidence GatherSourceEvidenceForShaderName(
            Shader shader,
            CapturedMaterialEvidence evidence)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            return Gather(
                shader, evidence, ProfileForShaderName(shader.name), shader.name);
        }
```

3. Update `ResolveCanonicalTargetShaderName`:
```csharp
        internal static string ResolveCanonicalTargetShaderName(
            string sourceShaderName)
        {
            return IsOutlineWrapperShaderName(sourceShaderName)
                ? OutlineShaderName
                : SupportedShaderName;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run tests in `LilToonAttestationTests` and `LilToonOpaqueTargetTests`.
Expected: All pass.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs
git commit -m "fix: align target shader evidence name and profile routing in lilToon attestation"
```

---

### Task 2: Version Scalar Retention in Conversion Evidence Requests

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentSourceEligibility.cs:94-100`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs:85-95`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentSourceEligibilityTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutSourceEligibilityTests.cs`

**Interfaces:**
- Consumes: `LilToonTransparentSourceEligibility.SourceEvidenceRequest`, `LilToonCutoutSourceEligibility.SourceEvidenceRequest`.
- Produces: `ConversionEvidenceRequest` schema that includes `_lilToonVersion`.

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentSourceEligibilityTests.cs`:
Add test:
```csharp
[Test]
public void ConversionEvidenceRequest_IncludesShaderFormatVersionProperty()
{
    var request = LilToonTransparentSourceEligibility.ConversionEvidenceRequest;
    Assert.That(
        request.ScalarProperties.Contains(LilToonSourceAttestation.ShaderFormatVersionProperty),
        Is.True,
        "Conversion request must retain _lilToonVersion for source attestation.");
}
```

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutSourceEligibilityTests.cs`:
Add test:
```csharp
[Test]
public void ConversionEvidenceRequest_IncludesShaderFormatVersionProperty()
{
    var request = LilToonCutoutSourceEligibility.ConversionEvidenceRequest;
    Assert.That(
        request.ScalarProperties.Contains(LilToonSourceAttestation.ShaderFormatVersionProperty),
        Is.True,
        "Conversion request must retain _lilToonVersion for source attestation.");
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run tests via Unity Test Runner.
Expected: Both tests fail because `_lilToonVersion` is absent from `SourceSchema`.

- [ ] **Step 3: Implement minimal fix**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentSourceEligibility.cs`:
Add `LilToonSourceAttestation.ShaderFormatVersionProperty` to `SourceSchema`:
```csharp
        private static readonly string[] SourceSchema =
        {
            LilToonSourceAttestation.ShaderFormatVersionProperty,
            CutoffProperty, AlphaBoostFaProperty, SubpassCutoffProperty,
        };
```

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs`:
Add `LilToonSourceAttestation.ShaderFormatVersionProperty` to `SourceSchema`:
```csharp
        private static readonly string[] SourceSchema =
        {
            LilToonSourceAttestation.ShaderFormatVersionProperty,
            CutoffProperty, AlphaBoostFaProperty, SubpassCutoffProperty,
        };
```

- [ ] **Step 4: Run tests to verify they pass**

Run `LilToonTransparentSourceEligibilityTests` and `LilToonCutoutSourceEligibilityTests`.
Expected: All pass.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentSourceEligibility.cs \
        Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentSourceEligibilityTests.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutSourceEligibilityTests.cs
git commit -m "fix: retain lilToon version in conversion evidence requests"
```

---

### Task 3: Integration Coverage for PrepareCanonicalOpaqueClone

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`

**Interfaces:**
- Consumes: `LilToonOpaqueTarget.PrepareCanonicalOpaqueClone(Material source, CapturedMaterialEvidence evidence)`.
- Produces: Verified end-to-end preparation test where source attestation succeeds against attested stand-in target shaders.

- [ ] **Step 1: Write integration tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`:
Add tests:
```csharp
[Test]
public void PrepareCanonicalOpaqueClone_AttestedTarget_SucceedsWithoutException()
{
    // Import stand-in lilToon shader with the exact attested GUID
    ImportTempShader(
        "lilToon",
        "lilToon.shader",
        ValidStandInLilToonSource(LilToonSourceAttestation.SupportedShaderGuid));
    try
    {
        var source = Track(ConversionEligibleStandIn());
        var captured = UnityMaterialEvidenceCapture.Capture(new[]
        {
            new MaterialEvidenceCaptureInput(
                source,
                LilToonTransparentSourceEligibility.ConversionEvidenceRequest),
        })[0];

        Material clone = null;
        Assert.DoesNotThrow(() =>
        {
            clone = Track(LilToonOpaqueTarget.PrepareCanonicalOpaqueClone(source, captured));
        });

        Assert.That(clone, Is.Not.Null);
        Assert.That(clone.shader.name, Is.EqualTo("lilToon"));
    }
    finally
    {
        DeleteConversionTempFolder();
    }
}
```

- [ ] **Step 2: Run test to verify it passes with current fix**

Run `LilToonOpaqueTargetTests.PrepareCanonicalOpaqueClone_AttestedTarget_SucceedsWithoutException`.
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs
git commit -m "test: add integration test for lilToon opaque target preparation"
```

---

### Task 4: Full Suite Validation and Clean Handoff

**Files:**
- Modify: `docs/superpowers/specs/2026-09-08-liltoon-opaque-target-attestation-design.md`
- Validation only.

- [ ] **Step 1: Run full EditMode test suite**

Run all EditMode tests via Unity Test Runner.
Confirm 2,025+ tests pass with 0 failures.

- [ ] **Step 2: Discard manifest churn and run git diff --check**

```bash
git checkout -- Packages/manifest.json Packages/packages-lock.json 2>/dev/null
git diff --check
```

- [ ] **Step 3: Update documentation status**

Update `docs/superpowers/specs/2026-09-08-liltoon-opaque-target-attestation-design.md` status from `draft` to `implemented`.

- [ ] **Step 4: Commit documentation update**

```bash
git add docs/superpowers/specs/2026-09-08-liltoon-opaque-target-attestation-design.md
git commit -m "docs: mark lilToon opaque target attestation design implemented"
```
