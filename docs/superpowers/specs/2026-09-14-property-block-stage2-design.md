# Property blocks as effective material state (Stage 2)

Date: 2026-09-14. Status: design addendum for review. Implementation has not started.

## Privacy note

This document contains observations from synthetic fixtures and private avatars. It contains no private avatar, renderer, material, animation clip, or controller names. It contains no machine paths, network ports, or instance identifiers. Machines and instances are named by role only. All counts are aggregate numbers.

## Background and problem

Stage 1 enabled analysis for renderers that carry a Unity property block (`MaterialPropertyBlock`). Stage 1 materializes per-slot effective materials by creating transient `Material` clones. The materializer copies block values onto the clones for all declared properties. The GPU alpha readback and semantic analysis then read the clones.

Stage 1 solved the presence-only refusal defect. However, stage 1 leaves two problems:

1. High allocation overhead on many-slot renderers. Cloned `Material` instances must be allocated, populated through native calls, and destroyed during capture.
2. Whole-material clones cannot represent per-slot block disagreement for admitted materials shared across multiple slots.

Stage 2 replaces materialization for scalar, color, and vector properties with in-memory per-property domains. Stage 2 keeps materialization for texture properties.

## Measured facts and research findings

### Fact 1: ST-component curves write degenerate vectors

When an animation animates a single component curve like `material._MainTex_ST.x`, the Unity animation runtime writes a full `Vector4` to the property block [MEASURED: dev editor instance probe, 2026-09-14]. The written vector is `(scale.x, 0, 0, 0)`. The other three components are zeroed.

A rendered gradient probe proves that the GPU samples with that degenerate vector [MEASURED: dev editor instance probe, 2026-09-14]. The U coordinate scales, but the V coordinate collapses to zero.

The materialized clone reproduces this vector [MEASURED: dev editor instance test run, 2026-09-14]. The UV interpreter fails closed over the degenerate UV envelope with `AdmittedMaterialSemanticsUnknown`.

The Unity Scripting API documents `MaterialPropertyBlock.GetVector` and `Material.SetTextureScale` [DOC: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MaterialPropertyBlock.GetVector.html] [DOC: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Material.SetTextureScale.html]. Unity does not document component-wise block updates for material vectors [DOC: https://docs.unity3d.com/2022.3/Documentation/Manual/animeditor-AnimationCurves.html]. The engine behavior of zeroing un-animated vector components in property blocks is undocumented internal runtime behavior [MEASURED: dev editor instance probe, 2026-09-14].

Cross-type reads on `MaterialPropertyBlock` return default zero values without throwing exceptions [MEASURED: dev editor instance probe, 2026-09-14]. Calling `GetFloat("_MainTex_ST")` returns `0.0f` [MEASURED: dev editor instance probe, 2026-09-14]. Calling `GetVector("_Cutoff")` returns `(0, 0, 0, 0)` [MEASURED: dev editor instance probe, 2026-09-14]. The typed check `HasVector` returns true for vectors and colors, but false for floats [MEASURED: dev editor instance probe, 2026-09-14] [DOC: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MaterialPropertyBlock.HasVector.html].

### Fact 2: Commit block values can disagree with animation curves

The Unity animation runtime can write a block value that disagrees with the curve [MEASURED: Census Lab editor instance, private swap avatar, 2026-09-14].

On a test renderer with two slots, slot 0 had serialized `_AlphaForceOpaque = 0` [MEASURED: dev editor instance probe, 2026-09-14]. Slot 1 had serialized `_AlphaForceOpaque = 1` [MEASURED: dev editor instance probe, 2026-09-14]. The animation curve animated `material._AlphaForceOpaque = 1` [MEASURED: dev editor instance probe, 2026-09-14].

When the controller committed, Unity wrote `_AlphaForceOpaque = 0` onto the renderer-wide block [MEASURED: dev editor instance probe, 2026-09-14]. The renderer-wide block applies to all slots. Both slots received `_AlphaForceOpaque = 0` from the block. The animation curve demanded `1`. Both slots failed admission with `AnimatedMaterialPropertyNotSingleton` [MEASURED: dev editor instance test run, 2026-09-14].

This disagreement is deterministic per avatar [MEASURED: Census Lab editor instance, 2026-09-14]. Unity writes renderer-wide blocks from the default state of the controller graph during commit [INFERRED: observed animator commit behavior].

### Fact 3: Partial block write coverage

Not all animated properties receive a block entry when a controller is committed [MEASURED: Census Lab editor instance, private swap avatar, 2026-09-14].

Emission color and fluorescence properties were absent from property blocks, while other properties on other renderers were present [MEASURED: Census Lab editor instance, 2026-09-14]. The block write coverage from the animation runtime is partial and per-renderer.

### Fact 4: Multi-slot per-index block disagreement (Stage 1 Corner 4)

In stage 1, `EffectiveMaterialMaterialization.MaterializeAdmitted` produces one clone per admitted material.

In stage 1, one admitted material can occupy multiple slots. If those slots have different per-index property blocks, stage 1 cannot represent the difference on one clone [MEASURED: stage 1 code inspection, 2026-09-14]. Stage 1 falls back to the renderer-wide block alone for multi-slot materials [MEASURED: Packages/com.alrauna.amuse/Editor/Host/EffectiveMaterialMaterialization.cs:86-90].

Unity documents that per-index blocks take precedence over renderer-wide blocks [DOC: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Renderer.SetPropertyBlock.html]. Specifically: "If there is both a per-renderer and a per-material block, only the per-Material block is used." [DOC: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Renderer.SetPropertyBlock.html].

### Fact 5: Real-avatar characterization

On the private swap avatar in the Census Lab editor instance, 39 of 73 surviving renderers carried a transient property block [MEASURED: Census Lab editor instance, 2026-09-14].

Mesh separations and material conversions succeeded with blocks present [MEASURED: Census Lab editor instance, 2026-09-14]. Block contents were byte-stable across two consecutive runs [MEASURED: Census Lab editor instance, 2026-09-14].

## Decision E: Measured stage-1 benchmark overhead and the stage-2 gate

To justify stage 2, overhead was measured on the dev editor instance on 2026-09-14.

The benchmark compared block-free materialization, block-carrying materialization with scalar properties, and GPU alpha readback. The test varied the slot count from 1 to 32 slots.

| Slot count | Block-free latency | Block-carrying clone latency | Created clones |
| :--- | :--- | :--- | :--- |
| 1 slot | 29.03 microseconds | 1.23 milliseconds (1231.97 us) | 1 clone |
| 4 slots | 4.19 microseconds | 3.12 milliseconds (3122.59 us) | 4 clones |
| 8 slots | 3.39 microseconds | 9.16 milliseconds (9163.40 us) | 8 clones |
| 16 slots | 2.22 microseconds | 10.13 milliseconds (10125.51 us) | 16 clones |
| 32 slots | 4.64 microseconds | 19.91 milliseconds (19906.32 us) | 32 clones |

Measured observations:

1. Block-free materialization has near-zero overhead: 2.2 to 4.6 microseconds.
2. Block-carrying materialization scales linearly with slot count: approximately 0.6 to 1.2 milliseconds per slot. On a 32-slot renderer, materialization takes 19.91 milliseconds.
3. GPU texture readback (`UnityAlphaFieldEvidence`) takes 0.95 milliseconds for a 512x512 texture with full mipmaps [MEASURED: dev editor instance probe, 2026-09-14].

Cloning 32 material slots takes 21 times longer than a full 512x512 GPU texture readback. The cost comes entirely from allocating Unity C++ `Material` objects and destroying them immediately after capture.

Replacing scalar materialization with in-memory value domains eliminates this allocation overhead. The stage-2 performance gate is passed with measured evidence.

Stage 2 keeps materialization for texture properties. Avatars rarely animate or override textures in property blocks. A texture property requires GPU sampling, so materialization of textures remains necessary and proportionate.

## Decision A: Per-property domains for scalar, color, and vector entries

Two semantic models exist for the domain of an animated property:

Model 1 (Baseline):
The domain is the union of the serialized material value, the block value, and the animation closure set:
`Domain = Serialized union Block union Closure`.

Model 2 (Replacement):
The block entry replaces the serialized default as the base value:
`Base = Block.HasProperty(P) ? BlockValue : SerializedValue`.
`Domain = { Base } union Closure`.

Decision: Adopt Model 1 (Baseline).

Reasoning:

1. Model 1 preserves safety invariant Falsifier 6: "A block defines a base value for an animated property, and the animation disagrees with the serialized default. The existing relevance-layer refusal must fire." If an animation disagrees with the serialized default, the avatar can show the default when un-animated or unweighted. Model 1 protects against this condition.
2. In Fact 2, the block holds `0` while the animation curve holds `1`. Under both Model 1 and Model 2, the block value `0` disagrees with the curve value `1`. Therefore, both models produce a non-singleton domain `{0, 1}`.
3. If the commit block holds `0`, the renderer displays `0` at commit time. If AMUSE separates the mesh based on `1` (opaque), the renderer displays the wrong state when un-evaluated. Refusing with `AnimatedMaterialPropertyNotSingleton` is the correct fail-closed outcome.

Consequences for real-avatar shapes:

- Shape 2 (Block disagrees with animation curve): AMUSE refuses the renderer with `AnimatedMaterialPropertyNotSingleton`. This refusal is fail-closed and sound.
- Shape 3 (Animated property absent from block): The block provides no entry. The domain is `Serialized union Closure`. The property resolves normally without spurious refusal.

## Decision B: Texture companion entries

Texture companion entries include `_ST`, `_TexelSize`, and `_HDR`.

Decision:
Keep whole-vector materialization for texture companion entries, and fail closed on degenerate vectors. Do not introduce per-component synthetic domains for texture vectors.

Reasoning:

1. In Fact 1, the GPU actually executes the vertex shader with `(scale.x, 0, 0, 0)`. The V coordinate collapses to zero on the GPU.
2. If AMUSE invents or merges serialized components into the vector, AMUSE proves a UV space different from what the GPU renders. That violates soundness.
3. Unity accessors for vectors return a complete `Vector4` [DOC: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MaterialPropertyBlock.GetVector.html]. A cross-type `GetFloat` returns zero [MEASURED: dev editor instance probe, 2026-09-14].
4. The existing exact UV envelope logic in `Analysis` detects when an envelope is degenerate. The interpreter fails closed with `AdmittedMaterialSemanticsUnknown`.

The shown degenerate vector must stay the proof input. AMUSE correctly refuses the degenerate UV envelope.

## Decision C: Resolution of failing fixture tests

Two fixture tests failed under stage 1:

### Test 1: `CutoutAnimationAtTheSerializedValuePrepares` (`_MainTex_ST.x` arm)

Context:
This test loops over animated properties to assert positive preparation when properties are animated at their serialized values. All scalar and color properties prepare successfully. However, the `_MainTex_ST.x` arm fails because Unity writes `(scale.x, 0, 0, 0)` into the block. The collapsed V coordinate creates a degenerate UV envelope. The interpreter fails closed with `AdmittedMaterialSemanticsUnknown`.

Decision: Split the test into two focused tests:
1. `CutoutAnimationAtTheSerializedValuePrepares`: Keeps all scalar, color, and independent vector properties. Verifies that every supported property prepares and splits when animated at its serialized value.
2. `CutoutAnimationOfDegenerateStComponentVectorRefuses`: Specifically exercises `material._MainTex_ST.x`. Asserts that Unity's zeroed sibling components produce a degenerate UV envelope, and that AMUSE refuses admission with `AdmittedMaterialSemanticsUnknown`.

Justification:
This split does not weaken the test suite. It preserves positive testing for all valid scalar properties. It also establishes an explicit negative regression pin for Unity's degenerate vector behavior.

### Test 2: `RuntimeStateProductionEntry_PostClosureSlotRefusalKeepsTheValidSibling`

Context:
This test intends to verify that one slot's post-closure admission failure does not eliminate a valid sibling slot.
The test used a renderer-wide curve `material._AlphaForceOpaque = 1f`.
Slot 0 had serialized `0`. Slot 1 had serialized `1`.
When the controller was committed, Unity wrote `_AlphaForceOpaque = 0` to the renderer-wide property block from slot 0.
Because the block was renderer-wide, it also set slot 1's effective block state to `0`.
Slot 1's domain became `{0, 1}`, which caused slot 1 to fail admission with `AnimatedMaterialPropertyNotSingleton`.
Both slots failed, so the whole renderer was refused.

Decision: Update the test fixture so that slot 0's failure is slot-scoped rather than renderer-wide:
1. Slot 0 fails admission via a slot-scoped difference. Examples include an unattested shader on slot 0, or a per-slot property override on slot 0 (`renderer.SetPropertyBlock(block0, 0)`).
2. Slot 1 carries a valid opaque material and an agreeing singleton curve.
3. Assert that slot 0 refuses admission, while slot 1 resolves and produces an opaque candidate triangle.

Justification:
Using a renderer-wide animation curve on a property that differs across slots contradicts Unity's property block mechanics. A renderer-wide curve causes Unity to write a renderer-wide block that overwrites all slots. Isolating the failure to slot 0 tests the intended invariant without triggering unintended renderer-wide block contamination.

## Decision D: Resolution of multi-slot disagreement (Corner 4)

Corner 4 occurs when one admitted material is assigned to multiple slots, and those slots have different per-index property blocks.

In stage 1, `MaterializeAdmitted` operated on materials without slot context. Therefore, it applied renderer-wide blocks to multi-slot materials.

In stage 2, slot resolution in `Analysis` runs per slot index:
`AdmittedMaterialStates.ResolveSlot(slots[slotIndex], ...)`.

Decision:
1. For scalar, color, and vector properties:
   Capture effective property values per slot index. For each slot index `i`, read `perIndex[i]` first, then `wide`, then the serialized material. Corner 4 is dissolved for all scalar, color, and vector properties.
2. For texture properties:
   Keep per-slot materialization. `EffectiveMaterialMaterialization.Materialize` already receives slot materials and produces per-slot clones (`result[slotIndex] = clone`).
3. If an admitted material swap across multiple slots contains conflicting per-index texture overrides, refuse that slot with `AlphaSeparationSlotRefusal.RuntimeMaterialValueNotMapped`.

## Architecture of Stage 2

### Layer responsibilities

1. `Host` (`EffectiveMaterialMaterialization.cs`):
   - Probes `MaterialPropertyBlock` entries.
   - Extracts scalar, color, and vector entries into an immutable struct `CapturedSlotBlockState`.
   - Materializes only slots that contain declared texture property overrides. If a slot contains only scalar, color, or vector overrides, cloning is bypassed.
2. `Analysis` (`CapturedAlphaMaterial.cs`, `AdmittedMaterialStates.cs`):
   - Accepts `CapturedSlotBlockState` during slot resolution.
   - Computes effective property domain: `Domain(P) = Serialized(P) union Block(P) union Curves(P)`.
   - Refuses non-singletons with `AnimatedMaterialPropertyNotSingleton`.
   - Uses effective block values for unanimated properties.
3. `Build` (`AmusePlatformFinishPlugin.cs`, `AlphaSeparationApply.cs`):
   - Retains block snapshot in `PreparedAlphaSeparation`.
   - Revalidates block equality at Apply time through `BlockStateEquals`.

## Falsifiers

1. A renderer has 32 slots with scalar property overrides. Materialization must not allocate 32 `Material` clones. The proof must consume the block values directly.
2. A block overrides `_Cutoff` on an unanimated material from 0.5 to 0.25. The proof must use 0.25.
3. A block overrides `_AlphaForceOpaque` to 0, while an animation curve animates 1. The domain `{0, 1}` must refuse with `AnimatedMaterialPropertyNotSingleton`.
4. A material is assigned to slot 0 and slot 1. Slot 0 has a per-index block setting `_Cutoff = 0.25`. Slot 1 has a per-index block setting `_Cutoff = 0.75`. Slot 0 must resolve with 0.25 and slot 1 must resolve with 0.75.
5. An animation animates `material._MainTex_ST.x`. The block contains `(scale.x, 0, 0, 0)`. The proof must fail closed over the degenerate UV envelope with `AdmittedMaterialSemanticsUnknown`.

## Validation plan

1. Unity EditMode test suite execution:
   - Run `Alrauna.Amuse.Tests.Editor.Host.EffectiveMaterialMaterializationTests`.
   - Run `Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationPreparationTests`.
   - Run `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests`.
   - Run the full `Alrauna.Amuse.Tests.Editor` assembly.
   - Run the full `Alrauna.Amuse.Research.Tests.Editor` assembly.
2. Benchmark validation:
   - Verify that clone allocation on scalar-only blocks is zero.
3. Git and privacy checks:
   - Run `git diff --check` to confirm whitespace integrity.
   - Run identifier sweep for private names, machine paths, and ports.
