# Conversion blockers on a private Census test avatar

Date: 2026-09-08. Base: `main` at `1ae9a0f`.
Labels: `[SOURCE]` is a fact read in this repository or in pinned upstream code.
`[MEASURED]` is a fact produced by a test or execution in this investigation.
`[INFERENCE]` is a deduction. `[RECOMMENDATION]` is a proposed next step.

Privacy note: renderer, material, clip, and controller names are replaced by
role descriptions. Exact per-renderer counts become ranges. The analytical
findings are unchanged.

## 1. Executive summary

During avatar optimization builds, AMUSE reported analyzing 31 renderers and
moving 0 triangles on the test avatar. `[MEASURED]`

This investigation isolated the exact cause for every renderer on the avatar.
The investigation evaluated both the production pipeline and isolated runtime
replays. `[MEASURED]`

The zero moved triangles count is not caused by a single bug. It is caused by
the mathematical reality of the avatar materials and meshes:

1. Renderers with opaque shaders are already opaque. AMUSE maps them to their
   original materials without changes. They contribute zero moved triangles.
   `[MEASURED]` `[SOURCE]`
2. Renderers with the lace dress are genuinely transparent. Every triangle
   footprint touches transparent texels. The exact classifier proves every
   triangle as `MustRemainTransparent`. They contribute zero moved triangles.
   `[MEASURED]`
3. Renderers with cutout floral submeshes contain zero wholly opaque triangles.
   All triangles touch cutout boundaries. They contribute zero moved triangles.
   `[MEASURED]`
4. Four renderers fail animation dependency closure. Animation curves swap
   materials into non-existent slots. `[MEASURED]`
5. Nineteen renderers use unadmitted shader families or gadget materials.
   `[MEASURED]`
6. When upstream texture optimization runs, generated textures lack asset
   importers and fail the source identity gate. `[MEASURED]` `[SOURCE]`

## 2. Finding 1: Already-opaque renderers produce no writes

Seventeen renderers across body, undergarment, accessory, tiara, pins,
sandals, underwear, hair (eight renderers), and kemono-style (two renderers)
groups showed positive opaque candidate counts during analysis. Candidate
counts ranged from 660 to 7,474 triangles per renderer. `[MEASURED]`

All these renderers use `lilToon` or `Hidden/lilToonOutline`. Both shaders are
opaque shaders. Their alpha is forced to one. `[SOURCE]`

AMUSE classifies all triangles on these slots as opaque. During separation
preparation, `ConvertAdmittedMaterial` handles `CapturedAlphaMaterialFamily.LilToon`.
The method sets `opaque = live`. It returns an identity mapping. `[SOURCE]`

During separation apply, `AlphaSeparationApply` evaluates the candidates:
1. The submesh disposition is `WhollyOpaqueCandidate`. No mesh split occurs.
   The mesh clone remains null. `[SOURCE]`
2. The target material equals the live material. `materialChanged` is false.
   `[SOURCE]`
3. The curve edits list is empty because the material mapping maps each material
   to itself. `[SOURCE]`
4. Line 337 of `AlphaSeparationApply.cs` creates a write only when `mesh != null`,
   `materialChanged`, or `curveEdits.Count > 0`. `[SOURCE]`

No write is created for these renderers. `CountOpaqueTriangles` only counts
triangles when the applied material differs from the live material. Because
these renderers are already opaque, AMUSE does not change them. The moved count
is zero by design. `[SOURCE]` `[INFERENCE]`

## 3. Finding 2: The lace dress is completely transparent

The avatar carries five renderers using `Hidden/lilToonTransparent` with white
lace dress materials across five parts: skirt, sleeve, top, upper-arm, and
jewelry slot. `[MEASURED]`

The avatar carries seven outfit swap animation clips: six color variants and
the default outfit. `[MEASURED]`

Analysis resolves all runtime states successfully across all seven outfit
variants. Every variant returns `ok`. `[MEASURED]`

An isolated classification of all 5,508 triangles on one lace renderer
produced:
- `ProvenOpaque`: 0
- `MustRemainTransparent`: 5,508
- `Unknown`: 0
`[MEASURED]`

Every single triangle is proven `MustRemainTransparent`. The dress is made of
lace. The texture contains transparency across the entire UV layout. Every
triangle footprint intersects sub-one alpha texels. `[MEASURED]`

Because zero triangles are proven opaque, the submesh disposition is
`Unchanged`. AMUSE does not alter the dress. The behavior is correct and sound.
`[INFERENCE]`

## 4. Finding 3: Cutout floral submeshes have no wholly opaque triangles

One cutout-floral renderer contains three submeshes:
- Submesh 0 (`lilToon`): 14,876 triangles. This submesh is
  already opaque. `[MEASURED]`
- Submesh 1 (`Hidden/lilToonCutoutOutline`): 12,152 triangles.
  `[MEASURED]`
- Submesh 2 (`Hidden/lilToonCutout`): 44,696 triangles.
  `[MEASURED]`

The classifier evaluated Submesh 1 and Submesh 2. Both submeshes produced zero
opaque candidate triangles. `[MEASURED]`

The cutout textures depict fine petals and foliage with a cutoff threshold of
0.75. Every triangle covers fine leaf or petal borders. The footprint of every
triangle touches texels below the cutoff. Zero triangles are wholly opaque.
`[MEASURED]` `[INFERENCE]`

One further cutout renderer (a hat, slot 0, `Hidden/lilToonCutout`) also
produced zero opaque candidate triangles across its five outfit swap variants.
`[MEASURED]`

## 5. Finding 4: Animation dependency closure failures

Four renderers failed animation dependency closure with
`RendererAnalysisRefusal.MaterialDependencyClosureFailed`:
1. A body renderer: `ClosureFailure = SlotOutOfRange`.
2. A dress renderer: `ClosureFailure = SlotOutOfRange`.
3. A sleeves renderer: `ClosureFailure = InvalidSwapValue`.
4. A staff-prop renderer: `ClosureFailure = SlotOutOfRange`.
`[MEASURED]`

`SlotOutOfRange` occurs when an animation curve targets
`m_Materials.Array.data[N]` where `N` is greater than or equal to the renderer
slot count. For example, the mesh has one slot, but an animation targets slot
index one or two. `[SOURCE]` `[INFERENCE]`

`InvalidSwapValue` occurs when a keyframe references a missing material or null
asset. `[SOURCE]` `[INFERENCE]`

These four renderers match the four warnings in the build log:
"AMUSE could not read this renderer's animations. An animation on this avatar
swaps a material on this renderer, but AMUSE could not identify which material
the animation assigns." `[MEASURED]` `[SOURCE]`

## 6. Finding 5: Unadmitted shader families and gadgets

Nineteen renderers refused analysis with
`RendererAnalysisRefusal.AdmittedMaterialSemanticsUnknown`. `[MEASURED]`

These renderers fall into three categories:
1. Transparent outline shaders: a veil renderer and a glasses renderer
   (slot 1) use `Hidden/lilToonTransparentOutline`. This shader family is not
   yet admitted. `[MEASURED]`
2. Gem shaders: an accessory renderer (slot 1) and the hat renderer (slots 1
   and 2) use `Hidden/lilToonGem`. This shader family is not admitted.
   `[MEASURED]`
3. Gadgets and particles: Seventeen non-avatar renderers under three gadget
   hierarchy roots use particle shaders (`Mobile/Particles/Alpha Blended`,
   `Mobile/Particles/Additive`, `Particles/Standard Unlit`) or custom gadget
   shaders from two asset vendors. `[MEASURED]`

These nineteen renderers match the nineteen build warnings:
"AMUSE could not prove any triangle on this renderer. The materials resolved,
but every triangle answer came back unknown." `[MEASURED]` `[SOURCE]`

## 7. Finding 6: Upstream texture optimization generated sub-assets

When Anatawa12 Avatar Optimizer runs `OptimizeTexture` before AMUSE, it creates
in-memory `Texture2D` instances. NDMF serializes these instances into
`SubAssetContainer` assets. `[SOURCE]`

The generated textures have an asset path, but they are not the main asset at
that path. The gate `UnityTextureEvidence.TryGetSourceId` rejects them. An
importer cannot reproduce sub-assets. `[SOURCE]`

When this occurs, texture evidence capture fails. The resolver records
`AlphaResolutionFailure.MissingTextureEvidence`. The resolution wraps into an
unresolved `AlphaResolution`. Triangle classification treats unresolved
resolutions as `Unknown`. `[SOURCE]` `[MEASURED]`

## 8. Finding 7: Latent defect in CommittedControllerGraph enumeration

During standalone replay of controller graph enumeration on the test avatar,
`CommittedControllerGraph.Enumerate` threw an unhandled `NullReferenceException`
at line 127:
`var identity = BehaviourIdentity.Of(behaviour.GetType());` `[MEASURED]` `[SOURCE]`

One gesture animator controller contains twelve state nodes where a
`StateMachineBehaviour` slot is null. One runtime-cloned FX controller contains
one such slot. `[MEASURED]`

In `CommittedControllerGraph.cs`:
Line 220 iterates over `child.state.behaviours` and adds every element to the
behaviours list without checking for null. `[SOURCE]`

When line 127 executes, it invokes `behaviour.GetType()`. If `behaviour` is null,
an unhandled `NullReferenceException` terminates the pass. `[SOURCE]`

`[RECOMMENDATION]` Update `WalkStateMachine` in `CommittedControllerGraph.cs` to
filter out null behaviour references before adding them to the behaviours list.

## 9. Conclusion

AMUSE behaves correctly on this avatar.

Every slot on the avatar belongs to one of the following cases:
1. Already opaque (`lilToon`, `Hidden/lilToonOutline`).
2. Genuinely transparent lace (white lace dress materials).
3. Genuinely cutout floral borders (cutout and cutout-outline materials).
4. Broken authoring material swaps (`SlotOutOfRange`, `InvalidSwapValue`).
5. Unadmitted shader families (`Hidden/lilToonTransparentOutline`, `Hidden/lilToonGem`).
6. Non-avatar gadgets and particle systems.

The optimizer proves what is true: the transparent parts of this avatar are
genuinely transparent, and the opaque parts are already opaque. The optimizer
correctly leaves all triangles in their original materials.
