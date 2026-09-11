# Alpha Separator Settings Design

Date: 2026-09-10. Base: `main` at `93ced28`.

Investigation: `docs/superpowers/investigations/2026-09-10-alpha-separator-settings.md`.

## 1. Overview

This design adds two user policy settings and one inspector change to the AMUSE avatar optimizer component:

1. **Preserve Transparency Minimum Texture Size.** A per-texture scope cap for the opacity proof. The proof stops consulting a texture's mip chain at the level whose dimensions fall below the chosen size.
2. **Minimum Opaque Coverage Percentage.** A split gate. A mixed submesh whose proven-opaque triangle share is below the percentage keeps all its triangles on the original material.
3. **An "Alpha Separator" foldout** in the inspector. It holds these two settings and the existing "Preserve Transparency Maximum Mipmap" setting. Advanced Settings keeps the "Ignore Out-of-Range Material Slots" toggle.

The settings are policy. They change the proof's scope and the transformation's value judgment. They never change classification. A triangle the proof consults is classified exactly as before.

## 2. Preserve Transparency Minimum Texture Size

### 2.1 Stored value

`AmuseAvatarOptimizer` stores `_preserveTransparencyMinTextureSize` as an `int`. The default is `128`. The value `-1` means every size. The public property is `PreserveTransparencyMinTextureSize`.

### 2.2 Scope rule

The proof consults a texture level only when both statements hold:

- the level index is at or below the mip cap, and
- the level's width and height are both at or above the minimum size.

The smaller dimension governs. A `2048x1024` texture with a minimum size of `512` consults levels down to `512x512` and no coarser level.

### 2.3 Texture below the minimum

A texture whose mip 0 is already smaller than the minimum on either side has no consulted levels. The proof sees no chain for that texture. Every triangle that samples it classifies Unknown, and its material does not convert. This is the user's informed choice: a texture this small is beneath the trust threshold the user set.

This rides the existing missing-evidence path. A texture absent from the gathered alpha fields resolves as `MissingTextureEvidence` and every triangle over it stays Unknown.

### 2.4 Combination with the mip cap

Both caps are active. Each texture is truncated to the more restrictive of the two caps. A stored minimum size below `1` is impossible from the inspector; the build treats a stored `-1` as "every size", which is a minimum size of `1`.

### 2.5 Pipeline placement

The size cap rides the same seam as the mip cap:

1. The plugin maps the stored value once, next to `ProofMipCapFrom`. Stored `-1` becomes `1`.
2. `UnityRendererAlphaAnalysis.GatherAlphaFields` takes the minimum size. Per texture it computes the largest level whose dimensions pass, then applies `LimitedTo` with the smaller of that level and the mip cap. When no level passes, the texture gets no field at all.
3. A pure helper on `AlphaMipChain` computes the largest qualifying level, so the rule is unit-tested without Unity textures.

The stored evidence keeps full chains. Only the handed-out proof scope shrinks, exactly as the mip cap behaves today.

## 3. Minimum Opaque Coverage Percentage

### 3.1 Stored value

`AmuseAvatarOptimizer` stores `_minimumOpaqueCoveragePercent` as an `int` in the range `0` through `100`. The default is `25`. The public property is `MinimumOpaqueCoveragePercent`.

### 3.2 Gate rule

The gate applies to `Split` submeshes only:

- coverage = proven-opaque triangles divided by all triangles of that submesh, as a percentage, and
- the slot is refused when coverage is strictly below the setting.

Triangles that classify Unknown count in the denominator and never count as opaque. The comparison uses exact integer arithmetic, so `opaqueCount * 100 < percent * totalCount` refuses and equality proceeds.

`WhollyOpaqueCandidate` submeshes always measure 100 percent and never refuse. `Unchanged` submeshes have nothing to gate. This bounds exactly the cost the user named: a `Split` appends one submesh, and one appended submesh is one extra runtime draw call.

### 3.3 Refusal vocabulary

`AlphaSeparationSlotRefusal` gains one slot-scoped member, `OpaqueCoverageBelowMinimum`. The refusal is recorded per slot in `AlphaSeparationPreparation.Prepare`, beside the existing marker-clip check, before any conversion work is prepared for the slot. A refused slot keeps its original submesh, material, and animation. Sibling slots continue.

### 3.4 Policy, not proof

Classification stays exact. The gate reads the plan's own ordinal counts after proof. Raising or lowering the setting never changes which triangles are proven. It only changes whether a proven split is worth a draw call.

## 4. Inspector

The inspector gains a private `_alphaSeparatorOpen` state and draws, in order:

1. **Alpha Separator** foldout:
   - "Preserve Transparency Maximum Mipmap" dropdown, moved unchanged from Advanced Settings.
   - "Preserve Transparency Minimum Texture Size" dropdown: "All Sizes" and the powers of two from `2` through `8192`. A stored value between listed sizes rounds up to the next listed size. "All Sizes" stores `-1`.
   - "Minimum Opaque Coverage Percentage" integer slider, `0` through `100`.
2. **Advanced Settings** foldout: the "Ignore Out-of-Range Material Slots" toggle only.

Every control keeps a plain technical English tooltip.

## 5. Default behavior changes

Both defaults are active. The user confirmed both consequences:

1. **Textures smaller than 128 texels on either side stop converting by default.** The proof consults no levels for them. A user who wants those materials converted lowers the size setting or selects All Sizes.
2. **Splits below 25 percent proven-opaque coverage stop by default.** Avatars that relied on small splits see those slots refuse with `OpaqueCoverageBelowMinimum` until the user lowers the slider.

## 6. Test strategy

- Unit tests for the level helper: exact boundary, non-square smaller-dimension rule, texture below the minimum, every level passing.
- End-to-end proof scope tests through the production barrier, mirroring the mip policy test: a fade inside the size-ignored levels must not stop the proof, a fade inside the consulted levels must stop it, and a texture below the minimum must not convert.
- Component tests for both defaults and both serialized round trips.
- Preparation tests: a split below the threshold records exactly `OpaqueCoverageBelowMinimum` and prepares nothing, a split at the threshold prepares, and the whole-opaque and unchanged dispositions never refuse for coverage.
- The full EditMode suite must pass with zero failures.
