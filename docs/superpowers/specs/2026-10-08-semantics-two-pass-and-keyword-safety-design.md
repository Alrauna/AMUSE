# Semantics Two-Pass, Texture Dimension, and Keyword Safety Design Specification

Date: 2026-10-08. Branch: `fix/latent-bugs-and-impurities`. Base: `main` at `9eed463`.

Privacy note: this record is sanitized. It names no private avatar, renderer, material, texture, scene, asset folder, instance name, or test-job identifier. Aggregate counts appear as ranges. Machine references name roles only, for example the dev editor instance.

---

## 1. Overview

This design addresses three defects in the semantics subsystem:
1. Two-pass Poiyomi shaders lose second-pass alpha semantics in `AnalyzeBaseMaterial`.
2. Non-2D textures produce invalid two-dimensional texture samplers in `UnityTextureEvidence.TryGetSampling`.
3. LilToon regular opaque conversions inherit mode-specific cutout and dither keywords from source materials.

All changes enforce strict fail-closed invariants and preserve existing material features.

---

## 2. Requirements and Invariants

1. `PoiyomiMaterialSemantics.AnalyzeBaseMaterial` must recognize the Two-Pass Poiyomi shader.
2. Two-pass analysis must capture both full schema properties and second-pass alpha properties.
3. Two-pass analysis must pass the two-pass flag to predicate creation and material interpretation.
4. `UnityTextureEvidence.TryGetSampling` must require `texture.dimension == TextureDimension.Tex2D`.
5. Non-2D textures must fail sampler extraction closed and return false.
6. `LilToonOpaqueTarget.PrepareCanonicalOpaqueClone` must disable cutout, clip, dither, and overlay mask keywords on regular conversions.
7. Feature keywords such as normal mapping and emission must remain intact on converted materials.

---

## 3. Detailed Architecture and Implementation

### 3.1 Two-Pass Evidence Capture in AnalyzeBaseMaterial

In `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`:

Define the full two-pass evidence request:

```csharp
private static MaterialEvidenceRequest FullTwoPassMaterialEvidenceRequest { get; } =
    CreateTwoPassAlphaEvidenceRequest(FullMaterialEvidenceRequest);
```

Update `AnalyzeBaseMaterial`:

```csharp
internal static PoiyomiSemanticResult AnalyzeBaseMaterial(Material material)
{
    RequireAnalyzableMaterial(material);

    var isTwoPass = material.shader != null &&
        string.Equals(
            material.shader.name,
            PoiyomiTwoPassShaderName,
            StringComparison.Ordinal);

    var request = isTwoPass
        ? FullTwoPassMaterialEvidenceRequest
        : FullMaterialEvidenceRequest;

    var captured = UnityMaterialEvidenceCapture.Capture(new[]
    {
        new MaterialEvidenceCaptureInput(
            material,
            request,
            AlphaPredicateRequestFor(material, isTwoPass)),
    })[0];

    var evidence = GatherSourceEvidence(
        material.shader, captured, RequiredSchemaProperties);
    if (!TryVerifyPoiyomiIdentity(evidence, out var diagnostic))
    {
        return Unsupported(diagnostic);
    }

    return InterpretVerifiedMaterial(
        material,
        QualitySettings.activeColorSpace,
        captured,
        isTwoPass);
}
```

### 3.2 Two-Dimensional Texture Guard in Sampler Extraction

In `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`:
In `UnityTextureEvidence.TryGetSampling`:

```csharp
internal static bool TryGetSampling(
    Texture texture,
    out TextureSampling sampling)
{
    sampling = default;
    if (texture == null)
    {
        return false;
    }

    if (texture.dimension != UnityEngine.Rendering.TextureDimension.Tex2D)
    {
        return false;
    }

    if (!TryMapFilterMode(texture.filterMode, out var filter))
    {
        return false;
    }

    if (!TryMapWrapMode(texture.wrapModeU, out var wrapU) ||
        !TryMapWrapMode(texture.wrapModeV, out var wrapV) ||
        wrapU != wrapV)
    {
        return false;
    }

    var aniso = texture.anisoLevel > 1
        ? TextureAnisoMode.Anisotropic
        : TextureAnisoMode.None;

    sampling = new TextureSampling(filter, wrapU, aniso);
    return true;
}
```

### 3.3 Keyword Normalization on LilToon Regular Opaque Conversions

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs`:
In `LilToonOpaqueTarget.PrepareCanonicalOpaqueClone`:

When `attestedTarget != source.shader`, the conversion moves a cutout or transparent material to opaque.
The method must strip mode-specific cutout, clip, dither, and alpha mask keywords:

```csharp
if (attestedTarget == source.shader)
{
    clone.SetFloat("_TransparentMode", 0f);
    WriteMultiModeZeroKeywordSet(clone);
}
else
{
    clone.DisableKeyword("UNITY_UI_ALPHACLIP");
    clone.DisableKeyword("UNITY_UI_CLIP_RECT");
    clone.DisableKeyword("ETC1_EXTERNAL_ALPHA");
    clone.DisableKeyword("_COLOROVERLAY_ON");
}
```

Feature keywords such as `_NORMALMAP`, `_EMISSION`, and `_REQUIRE_UV2` remain untouched.
This removes inherited cutout and dither shader variant triggers.

---

## 4. Verification Plan

1. In `PoiyomiMaterialSemanticsTests.cs`:
   - `AnalyzeBaseMaterial_RecognizesTwoPassShaderAndCapturesSecondPassAlpha`
2. In `UnityTextureEvidenceTests.cs`:
   - `TryGetSampling_Non2DTexture_ReturnsFalse`
3. In `LilToonOpaqueTargetTests.cs`:
   - `PrepareCanonicalOpaqueClone_RegularCutoutConversion_DisablesAlphaClipKeywords`
   - `PrepareCanonicalOpaqueClone_PreservesFeatureKeywords`
