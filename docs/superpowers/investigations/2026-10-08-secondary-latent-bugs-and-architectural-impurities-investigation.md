# Secondary Latent Bugs and Architectural Impurities Investigation

Date: 2026-10-08. Branch: `fix/latent-bugs-and-impurities`. Base: `main` at `9eed463`.

Privacy note: this record is sanitized. It names no private avatar, renderer, material, texture, scene, asset folder, instance name, or test-job identifier. Aggregate counts appear as ranges. Machine references name roles only, for example the dev editor instance.

---

## 1. Executive Summary

A second audit of the codebase was conducted on 2026-10-08.
The audit inspected the Build, Host, Analysis, and Semantics subsystems.
This report documents six confirmed latent defects and architectural impurities.
Each defect includes exact file citations, failure mechanisms, risk levels, and verified remediation.
An adversarial review verified that all six findings represent genuine codebase defects.
Remediation details were refined to preserve full property schemas and feature keywords.
All proposed changes must maintain strict fail-closed invariants.
Each phase must include failing tests before implementation.

---

## 2. Findings

### Finding 1: Unhandled ShaderPropertyType.Int in Locked Material Reconstruction

- Path: `Packages/com.alrauna.amuse/Editor/Build/LockedMaterialReconstruction.cs`
- Symbol: `LockedMaterialReconstruction.MoveSuffixedSavedValues`
- Failure condition:
  A locked material uses an original shader that declares integer properties.
  These properties include enum selectors, stencil comparison values, and culling modes.
  In `MoveSuffixedSavedValues`, the code checks only for `ShaderPropertyType.Float` and `ShaderPropertyType.Range`.
  When the declared property type is `ShaderPropertyType.Int`, the check returns false.
  The loop skips the entry.
  The clone never restores the plain integer value.
  The restored material keeps default integer values.
- Risk level: High.
  Restored materials can render with incorrect culling modes, blend modes, or stencil configurations.
- Remediation:
  Add `type == ShaderPropertyType.Int` to the numeric restoration check.
  Call `clone.SetFloat(plainName, entry.value)` for integer properties.

### Finding 2: Missing Blend Tree Recursion and State Machine Guards in Window Close

- Path: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`
- Symbols: `TransientUnlockWindowClose.ClipsInMotion` and `TransientUnlockWindowClose.ClipsInStateMachine`
- Failure condition:
  `ClipsInMotion` traverses child motions of `BlendTree` objects.
  The method tracks only `AnimationClip` instances in a seen set.
  It does not track visited `Motion` or `BlendTree` instances.
  Complex avatar controllers frequently reuse blend trees across multiple states.
  Reused blend trees cause exponential re-traversal.
  If an asset contains a cyclic blend tree reference, `ClipsInMotion` triggers a `StackOverflowException`.
  `ClipsInStateMachine` also traverses child state machines without tracking visited state machines.
- Risk level: Medium.
  A cyclic or deeply shared blend tree causes stack overflows or build stalls.
- Remediation:
  Add a `HashSet<Motion>` seen set to `ClipsInMotion`.
  Add a `HashSet<AnimatorStateMachine>` seen set to `ClipsInStateMachine`.
  Exit traversal immediately when an object was already visited.

### Finding 3: Uncaught ArgumentException on Non-Finite Secondary UV Channels

- Paths:
  - `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`
  - `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`
  - `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`
- Symbols:
  - `UnityRendererAlphaAnalysis.Classify`
  - `TriangleAlphaInput.TryGetUvSet`
  - `TriangleAlphaClassifier.ValidateFinite`
- Failure condition:
  `UnityRendererAlphaAnalysis.Classify` validates that vertex positions and primary UV0 coordinates are finite.
  It passes extra UV sets (UV1 through UV3) directly without checking `IsFinite`.
  When a shader layer samples a texture mapped to channels 1 through 3, `AlphaSemanticsResolver` reads these coordinates via `TryGetUvSet`.
  It places them into the input as UV0.
  When `TriangleAlphaClassifier.Classify` runs, `ValidateFinite` throws an `ArgumentException`.
  The build halts with an unhandled exception.
  The system fails to catch the error or degrade the triangle outcome to `Unknown`.
- Risk level: High.
  An unhandled exception crashes the avatar build process on malformed secondary UV data.
- Remediation:
  In `TriangleAlphaInput.TryGetUvSet`, verify that extracted coordinates are finite before returning true.
  Return false when any coordinate is non-finite.
  This allows `AlphaSemanticsResolver` to degrade the triangle to `Unknown` fail-closed.

### Finding 4: Two-Pass Poiyomi Shader Misinterpretation in AnalyzeBaseMaterial

- Path: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`
- Symbol: `PoiyomiMaterialSemantics.AnalyzeBaseMaterial`
- Failure condition:
  A material uses the Two Pass shader (`.poiyomi/Poiyomi Toon Two Pass`).
  `AnalyzeBaseMaterial` verifies source identity because `TryVerifyPoiyomiIdentity` admits the Two Pass shader.
  However, `AnalyzeBaseMaterial` captures evidence with `AlphaPredicateRequestFor(material, false)`.
  It then calls `InterpretVerifiedMaterial` with `interpretSecondAlphaFamily: false`.
  This ignores the second pass alpha chain (`_ModeTwoPass`, `_TwoPassColor`, `_AlphaForceOpaque2`).
  The returned semantics treat the two-pass material as a single-pass material.
- Risk level: High.
  Two-pass materials lose second pass alpha semantics during base material analysis.
- Remediation:
  Detect whether the material shader is the Two Pass shader in `AnalyzeBaseMaterial`.
  Use an evidence request combining full material properties with two-pass alpha properties.
  This preserves required schema validation while capturing second-pass alpha inputs.
  Pass `true` for `isTwoPass` into `AlphaPredicateRequestFor` and `InterpretVerifiedMaterial`.

### Finding 5: Missing Texture Dimension Guard in Sampler Extraction

- Path: `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`
- Symbol: `UnityTextureEvidence.TryGetSampling`
- Failure condition:
  A material assigns a `Cubemap`, `Texture3D`, `Texture2DArray`, or `RenderTexture` to a texture slot.
  `TryGetSampling` checks filter modes and wrap modes on the base `Texture` class.
  It never verifies that `texture.dimension` equals `TextureDimension.Tex2D`.
  The method succeeds and returns a `TextureSampling` struct.
  Downstream interpreters construct a two-dimensional `UvMapping` against a non-2D texture asset.
- Risk level: Medium.
  Non-2D textures produce invalid 2D sampling structures.
- Remediation:
  In `TryGetSampling`, verify that `texture.dimension == UnityEngine.Rendering.TextureDimension.Tex2D`.
  Return false when the texture dimension is not two-dimensional.

### Finding 6: Leaky Keyword Inheritance in LilToon Opaque Conversion

- Path: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs`
- Symbol: `LilToonOpaqueTarget.PrepareCanonicalOpaqueClone`
- Failure condition:
  A material converts from regular cutout or transparent lilToon to the opaque shader.
  `PrepareCanonicalOpaqueClone` creates a clone with `new Material(source)`.
  It swaps the clone shader to `attestedTarget`.
  It writes the canonical opaque properties.
  However, it does not normalize `clone.shaderKeywords`.
  Any keywords present on the cutout or transparent source material leak onto the opaque clone.
  In contrast, the Multi container conversion resets keywords via `WriteMultiModeZeroKeywordSet`.
- Risk level: Medium.
  Inherited transparency or cutout keywords can trigger invalid shader variants on the canonical opaque material.
- Remediation:
  Strip transparency and cutout mode keywords from `clone.shaderKeywords` on regular conversions.
  Preserve material feature keywords like normal mapping, emission, and matcaps.
