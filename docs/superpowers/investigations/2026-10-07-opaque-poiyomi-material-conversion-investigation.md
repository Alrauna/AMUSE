# Investigation: Opaque Poiyomi Material Conversion in AMUSE

Date: 2026-10-07. Status: complete.

Privacy note: this record is sanitized. It names no private avatar, renderer, material, texture, scene, asset folder, lock hash, or instance identity. The avatar is "the avatar under test". Materials and renderers are described by role. Exact counts are replaced by ranges. Aggregate report counts are stated as observed ranges. Public vendor shader names and public property names remain exact.

Labels: `[SOURCE]` marks a fact read at cited lines in this tree. `[MEASURED]` marks a fact observed in the Census Lab editor instance during this investigation on 2026-10-07. `[INFERENCE]` marks a logical conclusion.

---

## 1. Executive Summary

During play mode builds on the avatar under test, AMUSE converts many opaque Poiyomi materials into new canonical opaque clones. It reports these materials as modified. It also increments opaque candidate triangle counts.

This investigation determined why this conversion occurs.

1. AMUSE does not check whether a source material is already opaque before running alpha analysis. `[SOURCE]`
2. The alpha classifier proves that all triangles on an opaque submesh are opaque. `[SOURCE]`
3. The mesh separation planner marks the submesh as a wholly opaque candidate. `[SOURCE]`
4. Unlike lilToon, Poiyomi uses one single shader asset for all rendering presets. AMUSE cannot identify opaque Poiyomi materials by shader asset name. `[SOURCE]`
5. AMUSE tests every candidate Poiyomi material against 25 canonical opaque facts. `[SOURCE]`
6. Authored materials with rendering mode 0 (Opaque) almost never match all 25 canonical facts. Default shader properties in Unity leave properties such as `_BlendOpAlpha`, `_DstBlendAlpha`, and `_Cutoff` at non-canonical values. `[MEASURED]`
7. Because the 25 facts do not match, the material fails the canonical check. It does not resolve to the no-op path. `[SOURCE]`
8. The material passes all transformation gates. AMUSE marks the material as convertible. `[SOURCE]`
9. AMUSE generates a canonical opaque clone. It then replaces the original material on the renderer with this clone. `[SOURCE]`

This behavior contradicts the optimization scope defined in repository policy. Repository policy restricts alpha optimization to original AlphaTest and AlphaBlend materials. `[SOURCE]`

---

## 2. Census Lab Observations

On 2026-10-07, the avatar under test in the Census Lab editor instance was inspected.

1. The avatar contains between 60 and 70 renderers. `[MEASURED]`
2. Between 50 and 55 renderers use Poiyomi shaders. `[MEASURED]`
3. Exactly 31 renderers contain material slots with `_Mode = 0` (Opaque). `[MEASURED]`
4. Across the avatar, 67 material slots use `_Mode = 0` Poiyomi materials. These slots cover between 310,000 and 320,000 triangles. `[MEASURED]`
5. There are 17 unique `_Mode = 0` materials on the avatar. `[MEASURED]`
6. Exactly zero of the 17 materials match the 25 canonical facts required by `PoiyomiOpaqueConversion`. `[MEASURED]`
7. Observed differences on the 17 unique materials:
   - All 17 materials have `_BlendOpAlpha = 0` (Add). The canonical recipe requires 4 (Max). `[MEASURED]`
   - All 17 materials have `_DstBlendAlpha = 10` (OneMinusSrcAlpha). The canonical recipe requires 1 (One). `[MEASURED]`
   - 10 materials have `_Cutoff = 0.5`. The canonical recipe requires 0.0. `[MEASURED]`
   - 13 materials have `_OutlineDstBlendAlpha = 10`. The canonical recipe requires 0. `[MEASURED]`
   - 13 materials have `_OutlineBlendOpAlpha = 0`. The canonical recipe requires 4. `[MEASURED]`
   - Between 5 and 10 materials have custom render queues between 2200 and 3000. The canonical recipe requires 2000. `[MEASURED]`
8. Out of the 17 unique materials, 16 have outlines disabled (`_EnableOutlines = 0`). Only one material has outlines enabled. `[MEASURED]`
9. The 16 materials with outlines disabled pass every transformation gate. All 16 evaluate to `Convertible`. `[MEASURED]`
10. These 16 convertible materials occupy 63 material slots across 27 renderers. They account for between 290,000 and 300,000 triangles. `[MEASURED]`
11. When an AMUSE build runs, AMUSE converts all 63 slots. It creates canonical opaque material clones and overwrites `sharedMaterials` on 27 renderers. `[MEASURED]`

### 2.1 Post-Implementation Characterization

On 2026-10-08, the avatar under test was re-inspected in the Census Lab editor instance under the functional opacity qualification. `[MEASURED]`

1. Exactly 8 unique materials evaluate to `AlreadyOpaque`. `[MEASURED]`
   - These 8 materials cover 45 material slots across 21 renderers.
   - These 45 slots account for between 150,000 and 155,000 triangles.
   - Zero material clones are generated for these 45 slots.
   - The materials map directly to themselves by reference.
2. Exactly 6 unique materials evaluate to `Convertible`. `[MEASURED]`
   - These 6 materials cover 16 material slots across 9 renderers.
   - These 16 slots account for between 115,000 and 120,000 triangles.
   - These materials evaluate to `Convertible` because they use custom render queues of 2200 and 3000.
   - Because `IsFunctionallyOpaque` requires `effectiveRenderQueue == 2000`, they diverge on queue.
3. Exactly 3 unique materials evaluate to `Refused` (`ConversionPropertyAbsent`). `[MEASURED]`
   - These 3 materials cover 6 material slots across 6 renderers.
   - These materials use locked shaders with stripped properties in raw scene state.

---

## 3. The Code Execution Path

The conversion follows a deterministic path through five pipeline stages.

### Stage 1: AmusePlatformFinishPass
File: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`

Lines 581 to 850 iterate through every renderer on the avatar.
Lines 630 to 685 call `UnityAnimationEvidenceCapture.CaptureWithAnimationIndex`. This captures all assigned and swap-reachable materials. It does not check whether a material uses an opaque blend mode.
Lines 693 to 696 call `ResolveRuntimeStates`.
Lines 763 to 765 capture renderer geometry.
Lines 770 to 772 call `ClassifyRuntimeStates`.

### Stage 2: Triangle Classification
Files:
- `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`
- `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`

In `PoiyomiMaterialSemantics.cs` (lines 1224 to 1290), `InterpretSingleFamilyAlpha` reads material alpha properties.
If `_AlphaForceOpaque == 1`, alpha returns a constant 1.0.
If `_AlphaForceOpaque == 0`, alpha binds to the main texture alpha channel.
Because the main textures are opaque, `TriangleAlphaClassifier.Classify` classifies every triangle on the submesh as `TriangleAlphaOutcome.ProvenOpaque`.

### Stage 3: Submesh Separation Planning
File: `Packages/com.alrauna.amuse/Editor/Analysis/MeshSeparationPlanner.cs`

In lines 180 to 205:
When all triangles of a submesh are proven opaque, `transparentOrdinals.Count` is zero.
Line 198 assigns `SubmeshSeparationDisposition.WhollyOpaqueCandidate`.
Line 214 sets `HasAnyOpaqueCandidates` to true.

### Stage 4: Separation Preparation and Eligibility Evaluation
Files:
- `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`
- `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`

In `AlphaSeparationPreparation.cs` (lines 255 to 427):
The prepare pass iterates over candidate submeshes.
`WhollyOpaqueCandidate` submeshes are not skipped.
Line 384 calls `ConvertAdmittedMaterial`.
Lines 818 to 920 handle `CapturedAlphaMaterialFamily.Poiyomi`.
Line 893 calls `PoiyomiOpaqueConversion.EvaluateVerifiedEligibility`.

In `PoiyomiOpaqueConversion.cs`:
Line 285 calls `IsCanonicalOpaque`.
Lines 420 to 438 compare material values against `CanonicalOpaqueTuple`.
`CanonicalOpaqueTuple` (lines 172 to 197) contains 23 properties.
Because property values differ on real materials, `IsCanonicalOpaque` returns false.
The evaluator moves to step 3 (lines 290 to 392).
The material passes all transformation gates.
Line 390 returns `PoiyomiOpaqueConversionOutcome.Convertible`.

In `AlphaSeparationPreparation.cs` (lines 902 to 914):
AMUSE creates a canonical clone by calling `PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone(live)`.
It writes the 23 canonical properties, render queue 2000, and `RenderType = "Opaque"` onto the clone.

### Stage 5: Apply Pass Mutation
File: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`

In lines 331 to 344:
AMUSE inspects each surviving wholly opaque candidate slot.
It retrieves the mapped opaque material.
Because the mapped material is a newly instantiated clone, `ReferenceEquals(opaque, materials[slotIndex])` returns false.
Line 341 sets `materialChanged = true`.
Line 343 replaces the material in the array: `materials[slotIndex] = opaque`.
In lines 525 to 535, `write.Renderer.sharedMaterials = write.Materials` writes the mutated array to the avatar renderer.
Lines 541 to 542 increment the applied renderer count and applied opaque triangle count.

---

## 4. Architectural Comparison: Poiyomi vs lilToon

The contrast between the two shader frontends explains why this issue affects Poiyomi but not lilToon.

| Metric | lilToon Frontend | Poiyomi Frontend |
|---|---|---|
| Shader asset layout | Separate shader assets per mode (`lilToon`, `lilToonCutout`, `lilToonTransparent`) | Single shader asset for all modes (`.poiyomi/Poiyomi Toon`) |
| Capture family | Split into `LilToon`, `LilToonCutout`, `LilToonTransparent` | Unified into single `CapturedAlphaMaterialFamily.Poiyomi` |
| Opaque material handling | Mapped directly to live material in `AlphaSeparationPreparation.cs:649` (`opaque = live`) | Evaluated at runtime against 25 canonical facts |
| Clone generation for opaque | Never generated (bypasses conversion entirely) | Generated whenever any of 25 facts diverges from recipe |
| AlreadyOpaque outcome in enum | None (unnecessary by construction) | Exists, but almost never matched by authored assets |

LilToon recognizes opaque materials at capture time. Opaque lilToon materials bypass conversion and never create clones.
Poiyomi forces all materials through runtime eligibility checks.

---

## 5. Root Cause Analysis and Specification Divergence

### 5.1 Root Cause 1: Presets Versus Unity Shader Defaults
In Poiyomi, rendering mode presets are defined using ThryEditor metadata (`on_value_actions`).
When a user changes `_Mode` in the custom inspector, the custom GUI executes preset actions.
However, when a material is created in Unity, imported from an FBX, or switched via code, Unity does not execute GUI preset actions.
Unity initializes properties using the defaults in the shader Properties block.
The shader default for `_Cutoff` is 0.5.
The shader default for `_BlendOpAlpha` is 0.
The shader default for `_DstBlendAlpha` is 10.
As a result, authored opaque materials do not match the recipe.

### 5.2 Root Cause 2: Overly Strict Definition of AlreadyOpaque
In `PoiyomiOpaqueConversion.cs`, `IsCanonicalOpaque` requires exact equality across 23 property values plus queue 2000 plus `RenderType = "Opaque"`.
It does not distinguish between essential render state and harmless author settings.
For example, in opaque mode, alpha blending is not active. `_DstBlendAlpha` and `_BlendOpAlpha` do not affect opaque RGB rendering.
Similarly, `_Cutoff` is not consumed by the opaque pass.
Requiring exact equality on unused properties forces functional opaque materials to fail the check.

### 5.3 Root Cause 3: WhollyOpaqueCandidate Adopts Authored Opaque Submeshes
`MeshSeparationPlanner.cs` marks any submesh with 100% opaque triangles as `WhollyOpaqueCandidate`.
The planner does not know whether the source material was originally transparent or opaque.
It assumes every wholly opaque submesh is an optimization opportunity.
When an authored material is already opaque, replacing it with an AMUSE clone provides zero draw call benefit and zero overdraw benefit.
It only produces material churn.

### 5.4 Divergence from Policy in AGENTS.md
`AGENTS.md` states:
"For an original AlphaTest/AlphaBlend material, triangles not proven safe remain on the original material. Triangles proven visually opaque may move to an appended submesh that uses an AMUSE-generated canonical opaque material."

AMUSE alpha optimization is intended for AlphaTest and AlphaBlend materials.
Converting authored opaque materials exceeds this policy.

---

## 6. Recommendations and Solutions

Three potential solutions exist to resolve this issue.

### Option A: Filter Authored Opaque Submeshes in Separation Planning
In `MeshSeparationPlanner.cs` or `AmusePlatformFinishPlugin.cs`:
Inspect whether the source material is already opaque.
If a submesh uses an authored opaque material and requires no split, mark its disposition as `Unchanged`.
This prevents authored opaque materials from entering preparation.

### Option B: Map Authored Opaque Poiyomi Materials in Preparation
In `AlphaSeparationPreparation.cs`:
Mirror the lilToon pattern.
Detect if a Poiyomi material has `_Mode == 0` with opaque base blending (`_SrcBlend == 1`, `_DstBlend == 0`, `_ZWrite == 1`, `_ZTest == 4`).
Map the material directly to itself (`opaque = live`).
Do not generate a clone.

### Option C: Relax IsCanonicalOpaque
In `PoiyomiOpaqueConversion.cs`:
Revise `IsCanonicalOpaque` to check only properties that affect opaque rendering.
Ignore inactive properties such as `_Cutoff` and alpha blending factors when base RGB blending is opaque.
This allows real authored opaque materials to resolve to `AlreadyOpaque`.
`AlreadyOpaque` maps `source -> source` without creating clones.
