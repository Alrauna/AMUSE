# Alpha Separator Settings: Current-State Characterization

Date: 2026-09-10. Base: `main` at `93ced28`.

Labels: `[SOURCE]` is a fact read in this repository. `[INFERENCE]` is a deduction. `[RECOMMENDATION]` is a proposed next step.

## 1. Purpose

Three user requests need a design:

1. A minimum texture size for the transparency proof, valid for all mipmap sizes up to 8192.
2. A minimum coverage percentage, so a low fraction of proven-opaque faces does not pay CPU cost for a small GPU gain.
3. A new inspector foldout named "Alpha Separator" that holds these two settings and the existing "Preserve Transparency Maximum Mipmap" setting.

This note records the current state that the design must touch. It changes nothing.

## 2. The mip cap today

`[SOURCE]` The runtime component `AmuseAvatarOptimizer` stores `_preserveTransparencyMaxMipLevel` as an `int`. The value `-1` means every level. The default is `4`. The public property is `PreserveTransparencyMaxMipLevel`.

`[SOURCE]` The inspector draws the control inside `DrawAdvancedSettings()`, under the "Advanced Settings" foldout. The foldout uses `EditorStyles.foldoutHeader` and a private `_advancedOpen` state field. The dropdown options are "All Mips" and "Mip 0" through "Mip 10". Index 0 maps to stored `-1`; index `i` maps to level `i-1`. The same method draws the "Ignore Out-of-Range Material Slots" toggle.

`[SOURCE]` The build maps the stored value once, in `ProofMipCapFrom` in the platform finish plugin. Stored `-1` becomes `int.MaxValue`, which means no cap. A missing component also maps to no cap.

`[SOURCE]` `UnityRendererAlphaAnalysis.GatherAlphaFields` applies the cap per texture channel. Each alpha or red channel chain is truncated with `AlphaMipChain.LimitedTo(maxMipLevel)`.

`[SOURCE]` `AlphaMipChain.LimitedTo` returns a prefix of levels `0` through the cap. A cap at or above the last level returns the same chain. Chains always run down to a `1x1` level, because the builder halves sizes with a floor of one.

`[INFERENCE]` The cap is a global level index. It is blind to texture size. A cap of `4` consults down to `128x128` on a `2048` texture, but only down to `2x2` on a `32` texture. A minimum texture size is a different policy: it caps each texture at the level whose dimensions fall below the given size.

## 3. The plan and the split cost today

`[SOURCE]` `MeshSeparationPlanner.Create` sorts each submesh's triangles into `OpaqueTriangleOrdinals` and `TransparentTriangleOrdinals`. A submesh with zero opaque triangles stays `Unchanged`. A submesh with zero transparent triangles becomes `WhollyOpaqueCandidate`. A mixed submesh becomes `Split`. The renderer-wide plan carries `OpaqueTriangleCount` and `TransparentTriangleCount`.

`[SOURCE]` A `Split` plan appends one submesh to the build mesh. One appended submesh is one extra draw call per renderer at runtime. A `WhollyOpaqueCandidate` replaces the material on the existing submesh, so the draw call count does not change.

`[INFERENCE]` The CPU cost the user wants to bound is the extra draw call from a `Split`. A coverage gate that only gates `Split` dispositions bounds exactly that cost. Gating `WhollyOpaqueCandidate` would block conversions with no draw call cost.

`[SOURCE]` `AlphaSeparationPreparation.Prepare` walks the plan's submeshes. It skips `Unchanged` dispositions. It records slot-scoped refusals with `state.RecordSlotRefusal(...)` and `continue`, before any clone is prepared. The marker-clip check at the top of the loop is the existing pattern.

`[SOURCE]` `AlphaSeparationSlotRefusal` is the closed refusal enum for slot-scoped transformation refusals. New slot-scoped policy refusals belong there.

`[INFERENCE]` The natural seam for a coverage gate is the top of the `Prepare` loop, next to the marker-clip check. The gate reads the plan's own ordinal counts, refuses the slot with a new enum member, and prepares nothing for it. Classification stays exact; the gate is a policy decision after proof, like the mip cap.

## 4. Constraints the design must keep

`[SOURCE]` The mip cap is user-owned policy, not proof. The stored evidence keeps full chains. Truncation happens at `GatherAlphaFields` only.

`[SOURCE]` Defaults must not silently change existing builds. Today's default is mip cap `4`, no size rule, no coverage rule.

`[SOURCE]` Every setting needs an inspector label and tooltip in plain technical English. Closed refusal enums need a named member per refusal reason.

`[RECOMMENDATION]` Derive the size cap per texture: consult level `k` only while both level dimensions stay at or above the minimum size. Combine the two caps per texture with the more restrictive one. Gate only `Split` dispositions with the coverage percentage, per submesh, counted as proven-opaque triangles over all triangles of that submesh. Put both new settings and the moved mip setting under a new "Alpha Separator" foldout.
