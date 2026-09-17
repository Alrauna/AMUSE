# Authored depth test on transparent sources: web evidence and treatment paths

Privacy note: This record cites public vendor documentation, public graphics
API references, public repositories, and one committed same-day investigation.
It names no private avatar, renderer, material, or texture. The connected
private material appears here only as the committed note that characterizes
it.

Date: 2026-09-17. Status: stage 0 decision-support record. No production code
changed.

Method: four parallel read-only web research passes against primary sources
(Unity 2022.3 documentation, D3D11, Vulkan, and OpenGL references,
LearnOpenGL, NVIDIA GPU Gems and the NVIDIA depth precision article, lilToon
2.3.4 upstream, and the AAO and d4rkAvatarOptimizer repositories), plus the
committed source-read facts of
`2026-09-17-liltoon-twopass-transparent-analysis.md`. Every external claim
carries its URL. Scout transcripts stay in session history.

## 1. Evidence base

### 1.1 Comparison function semantics

- Unity 2022.3 documents every ShaderLab `ZTest` value. `LEqual` is "the
  default value". `Less` reads "Do not draw geometry that is at the same
  distance as or behind existing geometry", so equality fails. The same page
  documents `ZTest Equal` as a legitimate technique for drawing "exactly
  where already rendered geometry is".
  https://docs.unity3d.com/2022.3/Documentation/Manual/SL-ZTest.html
- The GPU APIs enumerate the equality case exactly: D3D11 `LESS` passes only
  when the source is smaller, `LESS_EQUAL` also passes on equality
  (https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_comparison_func).
  Vulkan `VkCompareOp` states the same
  (https://registry.khronos.org/vulkan/specs/latest/man/html/VkCompareOp.html).
  OpenGL `glDepthFunc` states the same
  (https://registry.khronos.org/OpenGL-Refpages/gl4/html/glDepthFunc.xhtml).

So `Less` and `LEqual` differ in exactly one pixel class: exact equality
against stored depth.

### 1.2 Equality collisions are a population property

- NVIDIA's depth precision article simulates ten thousand nearby distinct
  depths and counts the pairs that map to the same stored depth value: 45 to
  77 percent collide under standard projection in the measured range.
  https://developer.nvidia.com/content/depth-precision-visualized
- LearnOpenGL demonstrates z-fighting on coplanar geometry where "the depth
  test has no way of figuring out which is the right one".
  https://learnopengl.com/Advanced-OpenGL/Depth-testing
- The documented industry fixes for coplanar geometry are depth bias and
  polygon offset, not a test change: Microsoft's depth bias guide
  (https://learn.microsoft.com/en-us/windows/win32/direct3d11/d3d10-graphics-programming-guide-output-merger-stage-depth-bias)
  and Unity's `Offset` command page
  (https://docs.unity3d.com/2022.3/Documentation/Manual/SL-Offset.html).
- GPU Gems chapter 9 shows the sanctioned equality techniques: multipass
  lighting uses `LEQUAL` between passes to avoid z-fighting, and an
  illumination pass uses `EQUAL` to shade exactly matching depth.
  https://developer.nvidia.com/gpugems/gpugems/part-ii-lighting-and-shadows/chapter-9-efficient-shadow-volume-rendering

Consequence: a per-pixel proof that "no partner ever quantizes equal" is not
realistic. Equality collisions arise from depth buffer quantization itself,
not only from duplicated geometry.

### 1.3 Unity platform behavior

- 2022.3 uses reversed-Z on DirectX 11, DirectX 12, and Metal, and states
  "Unity automatically deals with depth (Z) bias".
  https://docs.unity3d.com/2022.3/Documentation/Manual/SL-PlatformDifferences.html
- No Unity page documents a `ZTest` remap under reversed-Z. A well-regarded
  community rendering expert states Unity flips the test so authored meaning
  is preserved, with script-supplied projection matrices as the break case.
  (secondhand, verified through a reader proxy)
  https://discussions.unity.com/t/ztest-and-z-buffer-direction/659927
- Consequence: AMUSE can reason in authored-intent terms. The stated
  compatibility assumption is "Unity preserves the authored comparison
  meaning on 2022.3 with standard projection".

### 1.4 lilToon upstream facts

- At tag 2.3.4 the common-tail reset writes `_ZTest = 4` inside
  `if(transparentMode != TransparentMode.TwoPass)`
  (`Editor/lilMaterialUtils.cs`). The guard appeared in commit
  `746f10b` during v1.3.0 development with no recorded rationale. The parent
  commit reset unconditionally. No issue or pull request explains it.
  https://github.com/lilxyzw/lilToon/commit/746f10bd5cab093184bb8322259822cebab4ee6b
- The official documentation frames the ZTest control as part of the
  normally-untouched rendering settings for fixing render failures and
  special expressions. The TwoPass transparent sub-mode is absent from the
  official rendering-mode list.
  https://lilxyzw.github.io/lilToon/ja_JP/advanced/rendering.html
- The maintainer deliberately ships `Less` elsewhere: `_OutlineZTest`
  defaults to 2 (Less) since v1.1.6 fixed an outline-over-backface artifact
  (https://github.com/lilxyzw/lilToon/issues/6). The inspector's rendering
  reset stamps `_ZTest = 4`.
- The pass library shows the authored value governs only the forward pass:
  `FORWARD` reads `[_ZTest]`, while `FORWARD_ADD` hardcodes
  `ZTest LEqual` (`Shader/ltspass_transparent.shader`).
  https://github.com/lilxyzw/lilToon/blob/2.3.4/Assets/lilToon/Shader/ltspass_transparent.shader
- Community products depend on non-default TwoPass depth behavior. A 2025
  BOOTH product ships lilToon presets that render two-pass transparent
  pantyhose over an inner garment layer, and ships a repair preset for users
  who break the state (https://booth.pm/ja/items/7473105). An MToon to
  lilToon converter copies `_ZTest`, `_ZWrite`, and `_ColorMask` "to avoid
  visual regressions after conversion"
  (https://github.com/yoridrill/ndmf-mtoon10-to-liltoon/pull/9).

### 1.5 Ecosystem facts

- AAO's ShaderInformation API models texture, UV, and vertex index usage
  only. It has no render-state concept. Merge safety comes from same-shader
  checks plus reference-material cloning, never from state comparison.
  https://raw.githubusercontent.com/anatawa12/AvatarOptimizer/master/API-Editor/ShaderInformation.cs
- d4rkAvatarOptimizer compares render state indirectly: its merge gate
  demands equal render queues and equal values for every ShaderLab
  bracket-bound property, which catches bracket-driven `ZTest`, `ZWrite`, and
  `Blend`.
  https://github.com/d4rkc0d3r/d4rkAvatarOptimizer
- NDMF publishes no render-state guidance.
  https://ndmf.nadena.dev/

Consequence: a depth-test-aware admission rule has no ecosystem precedent.
AMUSE would define the policy, not follow one.

### 1.6 Repository facts

The same-day note establishes: the `_ZTest != LEqual` refusal is identical
cross-frontend policy in lilToon cutout, lilToon transparent, and Poiyomi.
All three recipes write `LEqual`. The refusal currently blocks a Census Lab
garment material with a large provable triangle fraction.

## 2. What the evidence decides

1. The refusal premise is sound. The test value is authored visibility
   intent: Unity documents it as authored per-pass state, the lilToon
   maintainer uses `Less` deliberately outside TwoPass, and community
   products depend on non-default depth behavior.
2. The refusal scope is over-broad for a bounded sub-case. Against the
   canonical target, the divergence is one comparison on one pass, active
   only at exact stored-depth equality.
3. A pixel-exact equivalence proof is not realistically reachable, because
   quantized equality is a depth buffer population property. Any admission is
   a stated approximation or a consented divergence, never a proven identity.
   The vision anticipates this: exact proof is not required when
   version-specific observations and explicit compatibility assumptions
   support the contract, and an experimental policy can permit stated
   behavior changes through an explicit user choice.

## 3. Paths

### Path A: formalized refusal (status quo, documented)

Mechanism: keep gate 5 unchanged. Document the cross-frontend render-state
policy. Sharpen the refusal diagnostic to name the value and the family.

Strengths:
- Zero correctness risk. The evidence says non-default values are authored
  special expression, which is exactly what the gate protects.
- Near-zero cost. One documentation and reporting change.

Drawbacks:
- Permanent coverage defect for the class. The vision names unnecessary
  refusal a coverage defect, and the tension stays unresolved.
- The connected corpus material never optimizes.

Future risks:
- Relevance erosion on common layered clothing as other tools handle it.
- Upstream lilToon can remove the guard at any version. The attestation
  re-pin would surface it, so the risk is a coverage change, not a silent
  correctness change.

### Path B: bounded insensitivity admission (proof-heavy)

Mechanism: admit `_ZTest == Less` only for materials where a per-material
characterization proves moved-triangle insensitivity. Preconditions would
include the FORWARD_BACK pre-pass proof, `_Cull == Back` on the forward pass
to remove the structural self-collision against pre-pass depth, and mesh
analysis that excludes exact-duplicate partner geometry.

Strengths:
- Keeps the exact-proof architecture. No policy relaxation, no consent.
- Falsifiable in the existing test style.

Drawbacks:
- The quantization evidence undercuts any universal claim. Excluding
  foreign-partner equality requires reasoning about every co-drawn primitive,
  which is not a bounded proof.
- New machinery (duplicate-position analysis, partner reasoning) is
  disproportionate to a narrow slice with small coverage.

Future risks:
- The theorem must be maintained per frontend and per platform depth
  format. A quiet assumption of collision-freedom would be false confidence,
  which the correctness policy forbids.

### Path C: consent-backed normalization (experimental policy)

Mechanism: under an experimental policy value, admit `_ZTest == Less`
sources and disclose one declared divergence: moved triangles compare with
`LEqual` instead of `Less`. The consent dialog and the build report state it.
Batch and automated builds refuse, as the existing consent machinery already
requires. First-slice restriction: wholly opaque slots only, because a mixed
split would place `Less` triangles and `LEqual` triangles of the same surface
in one slot and a coplanar pair could flip its draw winner. The divergence is
confined to the forward pass, because the source's additive pass already
hardcodes `LEqual`.

Strengths:
- Unlocks the class now with an honest, stated contract. The vision
  explicitly supports stated changes through explicit user choice, and the
  per-build consent path exists.
- Small implementation: consent wiring, report text, the slot restriction,
  and falsifiers. No new proof machinery.

Drawbacks:
- The user carries a real visual risk: coplanar regions that the author
  stabilized with `Less` can z-fight under `LEqual`.
- Automated builds cannot consent, so the wall moves rather than vanishes.
- Consent fatigue is a product risk.

Future risks:
- Consent creep. The policy must stay limited to enumerated, characterized
  divergences, or every render-state refusal becomes a consent dialog.
- Animated `_ZTest` already refuses through the captured evidence, so the
  animation risk is closed today.
- Interop stays clean: the generated material carries a self-consistent
  `LEqual` state that downstream tools see as ordinary.

### Path D: evidence-gated revisit

Mechanism: keep Path A now. Define a revisit trigger: a privacy-reviewed
Census aggregate measuring how many corpus materials carry a non-default
forward depth test and would otherwise prove opaque. Commission Path B or C
only above a stated threshold.

Strengths:
- Matches the vision rule that measured benefit selects the next
  optimization.
- Avoids speculative work while the FORWARD_BACK proof, which is required
  regardless, dominates the cost.

Drawbacks:
- The concrete corpus case is already in hand. Waiting re-asks a known
  question.
- The aggregation needs privacy review infrastructure before it produces a
  number.

Future risks:
- Deferral can normalize the wall. The metric work can also grow into a
  prerequisite-shaped detour.

## 4. Recommendation

1. The FORWARD_BACK pre-pass proof is unconditional correctness work. Any
   TwoPass material that passes gate 5 today with non-degenerate pre-pass
   state can move triangles the pre-pass drew differently. Schedule it
   independently of this decision.
2. For the depth test value: take Path A immediately, then Path C as the
   first coverage slice under an experimental policy with the wholly
   opaque slot restriction. Carry Path D's metric as the reassessment
   trigger. Open Path B only if Census data reveals a provable narrow
   slice.
3. Whatever the choice, state it as a cross-frontend render-state policy.
   The identical gate exists in three frontends, so a lilToon-TwoPass-only
   patch would be a second convention beside an existing one.

`[DECISION NEEDED]` on the policy choice and on commissioning the FORWARD_BACK
proof slice.

Branch: `investigation/liltoon-two-pass-transparent`. This note is
uncommitted. Git authorization stays with the product owner.
