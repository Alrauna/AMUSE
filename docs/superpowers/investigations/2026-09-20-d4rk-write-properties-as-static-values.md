# d4rkAvatarOptimizer Write Properties as Static Values - characterization

## Status

Investigation note. Dated 2026-09-20. Read-only source characterization. It makes no production change and changes no design decision by itself. Question posed by the owner: how does this feature work, and does enabling it require design changes in AMUSE.

Privacy note. This note uses no private avatar data, no machine paths, and no instance identifiers. All facts come from the public upstream checkout described below and from repository records.

## Sources

| Source | Identity | Role |
|---|---|---|
| d4rkAvatarOptimizer | upstream `d4rkc0d3r/d4rkAvatarOptimizer`, commit `24c83f2`, package version 4.6.0 | Scratch checkout outside the repository, deleted after extraction. Same minor version as the pin in `2026-09-19-poiyomi-locked-materials-characterization.md`, so no version drift against the recorded characterization. |
| `2026-09-04-0.1.0-horizon-assessment.md` | sections 5.3 and 12 | Binding record for the SDK callback ordering re-confirmed below. |
| `2026-09-19-poiyomi-locked-materials-characterization.md` | section 8 | Binding record for the locked-material tag surface that this feature also reads. |

Anchor lines refer to the upstream commit above.

## 1. What the feature is

Write Properties as Static Values, WPSV below, rewrites shader source so that material property values become compile-time constants. The README states the mechanism directly: `uniform float4 _Color;` becomes `static float4 _Color = float4(1, 0, 1, 1);` (README, section Write Properties as Static Values). The generated code is emitted at `Editor/ShaderAnalyzer.cs:2684`. Constant folding then removes dead computation, so the shader runs faster. The README warns that the compiler may fold NaNs, which can change behavior in shaders not written for it.

This is the same mechanism class as Thry lock-in: bake property values into a per-material shader. The README says exactly that: WPSV takes over the job of locking in the shaders (README, section Merge Different Property Materials). One generated shader exists per optimized material configuration, renamed to `d4rkpl4y3r/Optimizer/<name>` (`Editor/ShaderAnalyzer.cs`, `OptimizedShader.SetName`).

WPSV does not create materials or meshes by itself. It is the shader-side half of DAO's material optimization. Mesh merging, texture arrays, and material merging are separate features that consume its output.

## 2. When it is active

The effective gate is `HasCustomShaderSupport && (settings.WritePropertiesAsStaticValues || MergeSkinnedMeshesWithShaderToggle || settings.MergeDifferentPropertyMaterials)` (`Editor/d4rkAvatarOptimizer.cs:244-245`). Two facts follow.

- WPSV exists only on the PC build target. `HasCustomShaderSupport` is true only for StandaloneWindows64 (`Editor/d4rkAvatarOptimizer.cs`, property definition).
- Two other features force it on: Shader Toggles and Merge Different Property Materials. A user who never touched the WPSV checkbox can still run it. The README documents both forcing relationships.

The user-facing default of the checkbox is false (`Editor/d4rkAvatarOptimizer.cs:41`).

## 3. Build path, step by step

DAO is not an NDMF plugin. It hooks the VRChat SDK at callback order -15 with Modular Avatar installed and -1025 otherwise (`Editor/AvatarBuildHook.cs:16-18`). This re-confirms the ordering record of `2026-09-04-0.1.0-horizon-assessment.md`: DAO runs after the whole NDMF pipeline, therefore after AMUSE.

1. At the start of `Optimize()`, when WPSV is active, DAO parses every used material's shader with its own HLSL parser and records whether each parse succeeded (`Editor/d4rkAvatarOptimizer.cs:182-188`).
2. DAO scans every used animation curve binding of form `material.<property>` on renderer paths and records the animated property names per renderer path. Property names must match an identifier pattern; component swizzles such as `.x` or `.r` are normalized away (`FindAllAnimatedMaterialProperties`, `Editor/d4rkAvatarOptimizer.cs:3839-3860`). A Thry rename-animated suffixed name such as a property ending in the cleaned material name is an ordinary identifier. The scan detects it.
3. Per material blob, `CreateOptimizedMaterials` builds a value table from the source material for every parsed property. Properties that the scan found animated are excluded from static baking and stay runtime-animatable through DAO's own per-mesh machinery. All other properties are emitted as `static` definitions in regenerated shader source (`Editor/d4rkAvatarOptimizer.cs:4541-4795`).
4. The regenerated files are written to disk under `Assets/d4rkAvatarOptimizer/TrashBin/` and imported (`Editor/d4rkAvatarOptimizer.cs:460`, `:4809-4830`). A fresh material named `m_<original>_<shader>` references the generated shader.
5. Materials whose shader failed to parse are skipped entirely. The original material stays in place; WPSV, material merging, and texture merging do not touch it (`Editor/d4rkAvatarOptimizer.cs:4809-4816`, README, Unparsable Materials).
6. Materials targeted by material-swap animations each get their own optimized material state, and swap-affected material slots are excluded from merging (`OptimizeMaterialSwapMaterials`, `Editor/d4rkAvatarOptimizer.cs:3463-3503`; README merge gate list).
7. Animation bindings of form `material.<property>` are preserved across the material replacement. Bindings address a renderer path plus a property name, not a material name, so a binding to a suffixed rename-animated property keeps working when the regenerated shader declares that property as animatable (`FixAllAnimationPaths` machinery, `Editor/d4rkAvatarOptimizer.cs:2070-2110`).

## 4. Locked material interplay

The build path contains no locked-material special casing. DAO parses a Thry generated locked shader like any HLSL file on disk and applies the same rules. Property values already baked by the lock fold further; properties that remain uniforms on the locked material are baked static unless the animation scan found them animated.

Lock detection exists, but only in the editor UI class. `IsLockedIn` reads the marker properties `_ShaderOptimizer`, `_ShaderOptimizerEnabled`, or `__Baked` equal to 1 (`Editor/d4rkAvatarOptimizerEditor.cs:1180-1204`). This matches the tag surface recorded in `2026-09-19-poiyomi-locked-materials-characterization.md` section 8. `HasPropertyMarkedAsRenameAnimated` reads the material tag `thry_rename_suffix`, falling back to the cleaned material name, which is the same suffix derivation Thry uses (`Editor/d4rkAvatarOptimizerEditor.cs:1206` and following). These detectors drive two user messages.

- Rename-animated properties without lock-in produce a warning: WPSV does not support this; lock the material with its native method (`Editor/d4rkAvatarOptimizerEditor.cs:753-760`).
- Locked materials without rename-animated properties produce an info text under Merge Different Property Materials: WPSV does effectively the same as locking in (`Editor/d4rkAvatarOptimizerEditor.cs:791-796`).

The README's rule for users follows: do not combine lock-in with WPSV, with one exception. Materials using rename-animated should stay locked in (README, sections Write Properties as Static Values and Merge Different Property Materials). So locked plus rename-animated is the documented supported combination. Upstream issue records 176 and 182 document remaining gaps for rename-animated cases.

## 5. Design impact on AMUSE

Verdict: enabling WPSV requires no correctness design change in AMUSE. The interaction is bounded by ordering, and the failure directions are benign. Two documentation-level additions to the transient-unlock spec are recommended.

### 5.1 Ordering bounds the interaction

DAO runs after every NDMF phase. AMUSE's capture, classification, planning, and apply never observe WPSV output. WPSV consumes AMUSE's shipped state as ordinary input. The reverse is also true: nothing AMUSE does at PlatformFinish can change what WPSV does, because AMUSE finishes before the SDK callback chain that runs DAO.

### 5.2 The existing alpha-separation contract is unchanged

- AMUSE's canonical opaque materials carry build-time constant values by construction. WPSV baking them static cannot change them.
- AMUSE's appended submeshes face the pre-existing documented hazard that DAO may merge them (product README and the 2026-09-05 scope decision). WPSV does not change that hazard. Mesh merging gates are the same machinery; Shader Toggles forcing WPSV on does not create a new class.
- On parse failure DAO skips a material completely, keeping the original. The skip direction for any AMUSE-related shader is degraded optimization, never changed rendering.

### 5.3 The transient-unlock spec holds, with two additions

The shipped re-locked material U sits in the same class as the user's own locked material, which is the design invariant. Upstream documents locked plus rename-animated as the supported combination for WPSV, and the build path keeps suffixed bindings animatable because the scan detects them. Without AMUSE, the user's own locked material undergoes the same WPSV treatment. Parity holds in both the re-lock success path and the fallback path, where the shipped material is the untouched original lock.

Two additions to `2026-09-20-poiyomi-transient-unlock-design.md` are recommended. Neither changes the design's structure.

1. Consent disclosure. State that post-build tools may re-process the re-locked output; with DAO WPSV enabled, the re-locked shader is re-parsed and re-optimized like any locked shader. The rename-animated surface stays animatable per upstream's documented supported combination.
2. Verification task V2 extension. AMUSE's window-close re-lock writes U's generated locked shader to disk inside the NDMF build container during the build. Post-NDMF tools read shaders from disk. V2 must observe that the re-locked files are complete and imported before the SDK callback chain runs. This is the same refresh hazard class that the characterization records for animation clips. The failure direction is benign, a DAO parse skip, but it must be observed and not assumed.

### 5.4 Detection is available and not needed

AMUSE could read the DAO component's settings on the build copy for diagnostics. This adds coupling to a non-NDMF tool's serialized component for no correctness benefit. Recorded as available, not recommended.

## 6. Verification statement

No Unity execution and no build run for this note. Every mechanism claim carries a source anchor into the upstream commit listed above. The ordering claim re-confirms two prior repository records. Claims about rename-animated gaps under WPSV rest on upstream issue records 176 and 182 and are not reproduced here; the spec addition in 5.3 routes the load-bearing case into verification task V2 observation.

## 7. Sources and links

- `https://github.com/d4rkc0d3r/d4rkAvatarOptimizer` at commit `24c83f2`, files `README.md`, `Editor/d4rkAvatarOptimizer.cs`, `Editor/ShaderAnalyzer.cs`, `Editor/d4rkAvatarOptimizerEditor.cs`, `Editor/AvatarBuildHook.cs`.
- `2026-09-04-0.1.0-horizon-assessment.md`, sections 5.3 and 12, for the callback ordering record.
- `2026-09-19-poiyomi-locked-materials-characterization.md`, section 8, for the lock marker and tag surface records.
- `docs/superpowers/specs/2026-09-20-poiyomi-transient-unlock-design.md`, sections 4, 9, and 16, for the consent and V2 anchors this note amends.
