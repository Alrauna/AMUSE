# lilToon Opaque Target Attestation and Evidence Alignment Design

Date: 2026-09-08
Status: implemented
Scope: lilToon opaque target preparation, source attestation evidence gathering, profile lookup, and conversion evidence requests.

## 1. Problem

During avatar build, AMUSE proves triangles opaque on a lilToon transparent or cutout material. AMUSE then creates a canonical opaque material copy through `LilToonOpaqueTarget.PrepareCanonicalOpaqueClone`.

In the Census Lab, `PrepareCanonicalOpaqueClone` failed with an exception:
`System.InvalidOperationException: The attested lilToon opaque target failed source attestation: shader asset GUID`.

Triangles had been proven opaque on the avatar. The material conversion threw before creating the opaque material copy.

## 2. Root Cause Analysis

Investigation identified three defects in `LilToonSourceAttestation.cs` and one schema omission in conversion evidence requests.

### 2.1 Target Evidence Shader Name Fallback

In `LilToonOpaqueTarget.PrepareCanonicalOpaqueClone`:
1. AMUSE resolves the canonical target shader name for the source material (`"lilToon"`).
2. AMUSE finds the shader object with `Shader.Find("lilToon")`.
3. AMUSE gathers target evidence with `LilToonSourceAttestation.GatherSourceEvidenceForShaderName(target, evidence)`.
4. `evidence` is the `CapturedMaterialEvidence` of the source material (for example, `Hidden/lilToonTransparent`).

In `LilToonSourceAttestation.GatherSourceEvidenceForShaderName`:
```csharp
internal static LilToonSourceEvidence GatherSourceEvidenceForShaderName(
    Shader shader,
    CapturedMaterialEvidence evidence)
{
    if (shader == null) throw new ArgumentNullException(nameof(shader));
    return Gather(
        shader, evidence, ProfileForShaderName(shader.name));
}
```

The method did not pass `shader.name` to the `shaderNameOverride` argument of `Gather`.
In `Gather`:
```csharp
return new LilToonSourceEvidence(
    shaderNameOverride ??
        (evidence.HasShaderName ? evidence.ShaderName : null),
    assetGuid?.ToLowerInvariant(),
    ...);
```

When `shaderNameOverride` is null, `Gather` falls back to `evidence.ShaderName`.
Because `evidence` comes from the source material, `LilToonSourceEvidence.ShaderName` received `"Hidden/lilToonTransparent"`.
However, `assetGuid` was read from `target` (`Shader.Find("lilToon")`), which is `df12117ecd77c31469c224178886498e`.

The resulting `LilToonSourceEvidence` was inconsistent:
- `ShaderName`: `"Hidden/lilToonTransparent"`
- `AssetGuid`: `df12117ecd77c31469c224178886498e` (the GUID of `lilToon`)

`LilToonSourceAttestation.TryVerifyIdentityForShaderName` inspected `ShaderName`. It selected `TransparentFamilyProfiles`. It then compared the actual asset GUID against the pinned GUID of `Hidden/lilToonTransparent` (`165365ab7100a044ca85fc8c33548a62`). The comparison failed and returned `UnsupportedShader: "shader asset GUID"`.

`GatherOpaqueTargetSourceEvidence` already passed `shader.name` as `shaderNameOverride`. `GatherSourceEvidenceForShaderName` was added to support outline wrappers as well, but omitted the parameter.

### 2.2 Missing Transparent Profile in Profile Lookup

`LilToonSourceAttestation.ProfileForShaderName(string shaderName)` maps live shader names to pinned `LilToonSourceProfile` records.
The method handled outline variants and cutout, but omitted `TransparentShaderName` (`"Hidden/lilToonTransparent"`).
Any call to `ProfileForShaderName("Hidden/lilToonTransparent")` fell through to `OpaqueProfile`.

### 2.3 Canonical Opaque Target Shader Name for Outline Wrappers

`LilToonSourceAttestation.ResolveCanonicalTargetShaderName(string sourceShaderName)` resolves the canonical opaque target shader for a source material.
The method previously returned `sourceShaderName` for outline wrappers:
```csharp
internal static string ResolveCanonicalTargetShaderName(
    string sourceShaderName)
{
    return IsOutlineWrapperShaderName(sourceShaderName)
        ? sourceShaderName
        : SupportedShaderName;
}
```

If a source material uses `Hidden/lilToonCutoutOutline` or `Hidden/lilToonTransparentOutline`, `sourceShaderName` is a cutout or transparent shader. The canonical opaque target must be `Hidden/lilToonOutline` (`OutlineShaderName`), not the transparent or cutout wrapper.

### 2.4 Missing Version Property in Conversion Evidence Requests

`LilToonTransparentSourceEligibility.SourceEvidenceRequest` and `LilToonCutoutSourceEligibility.SourceEvidenceRequest` omitted `_lilToonVersion` (`ShaderFormatVersionProperty`).
When `AdmittedMaterialStates.TryAdmitDerivedEvidence` projects evidence for animated conversion bindings, `derived` contains only properties present in `ConversionEvidenceRequest`.
Subsequent calls to `Gather` invoke `evidence.TryGetScalar("_lilToonVersion", out _)`.
If `_lilToonVersion` is not in the request, `CapturedMaterialEvidence.TryGetScalar` throws an `ArgumentException`.

## 3. Proposed Changes

### 3.1 Pass Target Shader Name in GatherSourceEvidenceForShaderName

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`:
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

`shader.name` ensures `LilToonSourceEvidence.ShaderName` matches `shader`, regardless of the source material evidence.

### 3.2 Add Transparent Shader to Profile Lookup

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`:
```csharp
if (string.Equals(
        shaderName,
        TransparentShaderName,
        StringComparison.Ordinal))
{
    return TransparentProfile;
}
```

### 3.3 Resolve Outline Wrappers to Opaque Outline Shader

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`:
```csharp
internal static string ResolveCanonicalTargetShaderName(
    string sourceShaderName)
{
    return IsOutlineWrapperShaderName(sourceShaderName)
        ? OutlineShaderName
        : SupportedShaderName;
}
```

### 3.4 Include Version Property in Conversion Requests

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentSourceEligibility.cs`:
Add `LilToonSourceAttestation.ShaderFormatVersionProperty` to `SourceSchema`.

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs`:
Add `LilToonSourceAttestation.ShaderFormatVersionProperty` to `SourceSchema`.

## 4. Test Strategy

1. **Unit Tests for Evidence Gathering**:
   - Verify `GatherSourceEvidenceForShaderName` assigns `target.name` to `LilToonSourceEvidence.ShaderName`.
   - Verify `targetEvidence.ShaderName` does not match `sourceEvidence.ShaderName` when the source is transparent or cutout.
   - Verify `TryVerifyIdentityForShaderName` verifies the gathered target evidence without error.

2. **Unit Tests for Profile and Name Resolution**:
   - Verify `ProfileForShaderName("Hidden/lilToonTransparent")` returns `TransparentProfile`.
   - Verify `ResolveCanonicalTargetShaderName("Hidden/lilToonTransparent")` returns `"lilToon"`.
   - Verify `ResolveCanonicalTargetShaderName("Hidden/lilToonTransparentOutline")` returns `"Hidden/lilToonOutline"`.
   - Verify `ResolveCanonicalTargetShaderName("Hidden/lilToonCutoutOutline")` returns `"Hidden/lilToonOutline"`.
   - Verify `ResolveCanonicalTargetShaderName("Hidden/lilToonOutline")` returns `"Hidden/lilToonOutline"`.

3. **Conversion Evidence Schema Tests**:
   - Verify `ConversionEvidenceRequest` for transparent and cutout contains `_lilToonVersion`.
   - Verify `TryAdmitDerivedEvidence` retains `_lilToonVersion` on projected derived evidence.

4. **Integration Tests for PrepareCanonicalOpaqueClone**:
   - Verify `PrepareCanonicalOpaqueClone` succeeds on a lilToon transparent source material when stand-in target shaders have matching attested GUIDs.
   - Verify full EditMode test suite passes.
