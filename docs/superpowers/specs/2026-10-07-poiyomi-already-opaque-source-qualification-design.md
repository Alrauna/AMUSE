# Design: Poiyomi AlreadyOpaque Source Qualification

Date: 2026-10-07. Status: proposed.
Related investigation: `docs/superpowers/investigations/2026-10-07-opaque-poiyomi-material-conversion-investigation.md`.

Privacy note: this design is sanitized. It names no private avatar, renderer, material, texture, scene, asset folder, lock hash, or instance identity. The avatar is "the avatar under test". Materials and renderers are described by role. Exact counts are replaced by ranges. Public vendor shader names and public property names remain exact.

Labels: `[SOURCE]` marks a fact read at cited lines in this tree. `[MEASURED]` marks a fact observed in the Census Lab editor instance on 2026-10-07. `[INFERENCE]` marks a logical conclusion.

---

## 1. Summary and Contract

On 2026-10-07, AMUSE converts authored opaque Poiyomi materials into generated canonical opaque clones.
On the avatar under test, 27 renderers, 63 slots, and roughly 295,000 triangles of authored opaque materials convert during builds. `[MEASURED]`

This design separates source qualification from target clone validation in `PoiyomiOpaqueConversion.cs`.

### Contract
1. A source Poiyomi material that is already functionally opaque qualifies as `AlreadyOpaque`.
2. `AlreadyOpaque` maps the source material to itself by reference. It creates no material clone and does not overwrite `sharedMaterials`.
3. Generated canonical opaque material clones continue to enforce the complete 23-property recipe, render queue 2000, and `RenderType = "Opaque"`.
4. Converted Cutout and Fade materials continue to produce full canonical opaque clones.

---

## 2. Problem Statement `[SOURCE]`

In `PoiyomiOpaqueConversion.cs`, `EvaluateVerifiedEligibility` tests whether a source material is already opaque before running transformation gates (`:284-288`):

```csharp
if (IsCanonicalOpaque(values, effectiveRenderQueue, effectiveRenderType))
{
    return PoiyomiOpaqueConversionEligibility.AlreadyOpaque();
}
```

The helper `IsCanonicalOpaque` (`:420-438`) requires an exact match across all 23 properties in `CanonicalOpaqueTuple`:
- `_Mode == 0f`
- `_AlphaForceOpaque == 1f`
- `_BlendOp == 0f`
- `_BlendOpAlpha == 4f`
- `_Cutoff == 0f`
- `_SrcBlend == 1f`
- `_DstBlend == 0f`
- `_SrcBlendAlpha == 1f`
- `_DstBlendAlpha == 1f`
- `_AddSrcBlend == 1f`
- `_AddDstBlend == 1f`
- `_AddSrcBlendAlpha == 0f`
- `_AddDstBlendAlpha == 1f`
- `_AlphaToCoverage == 0f`
- `_ZWrite == 1f`
- `_ZTest == 4f`
- `_AlphaPremultiply == 0f`
- `_OutlineSrcBlend == 1f`
- `_OutlineDstBlend == 0f`
- `_OutlineSrcBlendAlpha == 1f`
- `_OutlineDstBlendAlpha == 0f`
- `_OutlineBlendOp == 0f`
- `_OutlineBlendOpAlpha == 4f`
- `effectiveRenderQueue == 2000`
- `effectiveRenderType == "Opaque"`

In Unity, newly created or imported Poiyomi materials initialize from shader defaults.
Shader defaults leave inactive properties at different values:
- `_Cutoff` defaults to `0.5`.
- `_BlendOpAlpha` defaults to `0` (Add).
- `_DstBlendAlpha` defaults to `10` (OneMinusSrcAlpha).
- `_OutlineDstBlendAlpha` defaults to `10`.
- `_OutlineBlendOpAlpha` defaults to `0`.

Because real authored materials diverge on these inactive properties, `IsCanonicalOpaque` returns false.
The material then passes all transformation gates and evaluates to `Convertible`.
AMUSE creates an unnecessary clone and replaces the authored material on the avatar renderer.
This violates repository policy in `AGENTS.md`. `AGENTS.md` restricts alpha optimization to original AlphaTest and AlphaBlend materials.

---

## 3. Technical Design

### 3.1 Separation of Source Qualification and Target Validation

On 2026-10-07, `PoiyomiOpaqueConversion.cs` uses `CanonicalOpaqueTuple` for both:
1. Validating newly generated clones in `TryFindNonCanonicalFact`.
2. Qualifying whether an authored source material is already opaque in `IsCanonicalOpaque`.

These two responsibilities are distinct.
- **Target clone validation** must remain strict. AMUSE must guarantee that any clone it synthesizes matches every vendor canonical fact.
- **Source qualification** must check functional opacity. It must test whether the material already renders as opaque geometry.

### 3.2 Functional Opacity Rules for Source Materials

A source Poiyomi material is functionally opaque when it satisfies seven requirements:

1. **Rendering preset or forced alpha**:
   The material must have `_Mode == 0f` (Opaque preset) or `_AlphaForceOpaque == 1f`.
   When `_Mode == 0f`, Poiyomi executes its opaque pass. It does not execute alpha clip instructions.
   When `_AlphaForceOpaque == 1f`, fragment alpha is forced to 1.0.

2. **Base RGB blending is replacement**:
   - `_BlendOp == 0f` (Add)
   - `_SrcBlend == 1f` (One)
   - `_DstBlend == 0f` (Zero)
   This ensures the fragment shader completely replaces the destination pixel. No background color bleeds through.

3. **Depth test and depth write are standard**:
   - `_ZWrite == 1f` (On)
   - `_ZTest == 4f` (LEqual)
   The material writes depth and uses standard depth testing.

4. **ForwardAdd RGB blending is standard additive**:
   - `_AddSrcBlend == 1f` (One)
   - `_AddDstBlend == 1f` (One)
   Additive light passes accumulate normally.

5. **Advanced alpha features are inactive**:
   - `_AlphaToCoverage == 0f` (Off)
   - `_AlphaPremultiply == 0f` (Off)
   Alpha-to-coverage and alpha premultiplication do not alter fragment values.

6. **RenderType tag is Opaque**:
   `effectiveRenderType` equals `"Opaque"`.

7. **Render queue is standard opaque queue**:
   `effectiveRenderQueue == CanonicalOpaqueRenderQueue` (2000).

### 3.3 Inactive Properties Ignored During Source Qualification

The following properties do not affect opaque RGB rendering. Source qualification ignores them:

- `_Cutoff`:
  In Poiyomi, `_Cutoff` is only used when `_Mode == 1f` (Cutout).
  When `_Mode == 0f` or `_AlphaForceOpaque == 1f`, Poiyomi does not clip by cutoff.
  Shader default `0.5` does not discard opaque fragments.

- `_BlendOpAlpha`, `_SrcBlendAlpha`, `_DstBlendAlpha`:
  When RGB blending is `One / Zero`, the color buffer receives full opacity.
  When alpha is 1.0, both the shader default equation (`1 * 1 + DstAlpha * 0 = 1`) and the canonical recipe equation (`Max(1, DstAlpha) = 1`) produce 1.0.

- `_OutlineDstBlendAlpha`, `_OutlineBlendOpAlpha`:
  The outline pass writes opaque RGB when `_OutlineSrcBlend == 1` and `_OutlineDstBlend == 0`.
  Alpha blend equations produce 1.0 on both paths.

### 3.4 Target Clone Synthesis and Validation Unchanged

`CanonicalOpaqueTuple` remains unchanged.
`CanonicalOpaqueProperties` remains unchanged.
`PrepareCanonicalOpaqueClone` remains unchanged.
`TryFindNonCanonicalFact` remains unchanged. It continues to enforce all 23 properties plus queue 2000 plus `RenderType = "Opaque"`.
Synthesized clones continue to receive the full vendor canonical opaque recipe.

---

## 4. Pipeline Impact and Invariants

### 4.1 Preparation Pass Impact
File: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`

In line 893, `AlphaSeparationPreparation.cs` calls `PoiyomiOpaqueConversion.EvaluateVerifiedEligibility`.
When an authored opaque Poiyomi material is evaluated:
1. `EvaluateVerifiedEligibility` finds that the material meets the functional opacity requirements.
2. It returns `PoiyomiOpaqueConversionEligibility.AlreadyOpaque()`.
3. In line 899, the switch statement executes:
   ```csharp
   case PoiyomiOpaqueConversionOutcome.AlreadyOpaque:
       opaque = live;
       break;
   ```
4. The slot mapping stores `mapping.Add(live, live)`.
5. Because `ReferenceEquals(opaque, live)` is true, line 422 skips adding `opaque` to `pendingClones`.
6. AMUSE creates no material clone.

### 4.2 Apply Pass Impact
File: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`

In line 340, `AlphaSeparationApply.cs` retrieves `opaque = slot.OpaqueOfAdmitted[live[slotIndex]]`.
Because `opaque` is `live[slotIndex]`:
1. `ReferenceEquals(opaque, materials[slotIndex])` is true.
2. `materialChanged` remains false.
3. If no other slot on the renderer modified materials or split submeshes, `materialChanged` is false.
4. Line 519 skips writing `write.Renderer.sharedMaterials = write.Materials`.
5. The renderer remains untouched.

### 4.3 Animation Swap Invariants
If an avatar slot has an animated material swap:
- Case 1: All swapped materials are authored opaque Poiyomi materials.
  Every swapped material evaluates to `AlreadyOpaque`.
  Every material maps to itself.
  No clone is created. Curves require no edits.
- Case 2: One swapped material is Cutout, and one is authored Opaque.
  The cutout material evaluates to `Convertible`. It creates a canonical opaque clone.
  The authored opaque material evaluates to `AlreadyOpaque`. It maps to itself.
  The slot prepares successfully. The animation curve rewrites the cutout key to the clone. It leaves the opaque key pointing to the authored opaque material.

---

## 5. Security and Testing Strategy

### 5.1 Unit Tests
File: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`

1. **Authored defaults test**:
   A Poiyomi material with shader defaults (`_Mode = 0`, `_Cutoff = 0.5`, `_BlendOpAlpha = 0`, `_DstBlendAlpha = 10`) evaluates to `AlreadyOpaque`.

2. **Load-bearing property perturbation tests**:
   Perturbing any of the essential opaque properties prevents `AlreadyOpaque`:
   - `_Mode = 1` (Cutout)
   - `_SrcBlend != 1`
   - `_DstBlend != 0`
   - `_BlendOp != 0`
   - `_ZWrite != 1`
   - `_ZTest != 4`
   - `_AlphaToCoverage != 0`
   - `_AlphaPremultiply != 0`
   - `_AddSrcBlend != 1`
   - `_AddDstBlend != 1`
   - `renderQueue != 2000`
   - `RenderType != "Opaque"`

3. **Inactive property perturbation tests**:
   Perturbing inactive properties (`_Cutoff = 0.8`, `_BlendOpAlpha = 0`, `_DstBlendAlpha = 10`) still yields `AlreadyOpaque`.

4. **Target clone validation unchanged**:
   `TryFindNonCanonicalFact` tests remain green and continue to enforce all 25 facts on generated clones.

### 5.2 Build Preparation Tests
File: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

1. An authored opaque Poiyomi material slot produces `AlreadyOpaque`.
2. `CreatedClones` remains empty.
3. `OpaqueOfAdmitted` maps the source material to itself by reference.
