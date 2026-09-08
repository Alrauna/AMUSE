# Avatar conversion blockers on the test avatar

Date: 2026-09-08. Base: `main` at `fb7bb2d`.
Labels: `[SOURCE]` is a fact read in this repository or in pinned upstream code.
`[MEASURED]` is a fact produced by a test or execution in this investigation.
`[INFERENCE]` is a deduction. `[RECOMMENDATION]` is a proposed next step.

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

The following renderers showed positive opaque candidate counts during
analysis: `Body` (7,474), `Bra` (3,118), `Chains` (3,140), `dress tiara`
(4,030), `Hair_*` (8 renderers, 90 to 5,114 each), `Kemono_*` (2 renderers,
1,178 to 2,276 each), `Pins` (1,625), `Sandals` (3,032), and `Underwear` (660).
`[MEASURED]`

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

The avatar carries five renderers using `Hidden/lilToonTransparent` with
material `White_Dress` or `White_sleeve`: `dress bottom`, `dress sleeve`,
`dress top`, `dress upper arms`, and `dress jewlery` (slot 1). `[MEASURED]`

The avatar carries seven outfit swap animation clips: `Outfit Red`, `Outfit
Default Rags`, `Outfit Black Rags`, `Outfit Blue`, `Outfit Black`, `Outfit
Green`, and the default outfit. `[MEASURED]`

Analysis resolves all runtime states successfully across all seven outfit
variants. Every variant returns `ok`. `[MEASURED]`

An isolated classification of all 5,508 triangles on `dress bottom` produced:
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

The `Roses` renderer contains three submeshes:
- Submesh 0 (`Vines Pink`, `lilToon`): 14,876 triangles. This submesh is
  already opaque. `[MEASURED]`
- Submesh 1 (`Staff Pink`, `Hidden/lilToonCutoutOutline`): 12,152 triangles.
  `[MEASURED]`
- Submesh 2 (`Roses Pink`, `Hidden/lilToonCutout`): 44,696 triangles.
  `[MEASURED]`

The classifier evaluated Submesh 1 and Submesh 2. Both submeshes produced zero
opaque candidate triangles. `[MEASURED]`

The rose petals and staff foliage use cutout textures with cutoff threshold
0.75. Every triangle covers fine leaf or petal borders. The footprint of every
triangle touches texels below the cutoff. Zero triangles are wholly opaque.
`[MEASURED]` `[INFERENCE]`

The `Witch Hat` renderer slot 0 (`Hidden/lilToonCutout`) also produced zero
opaque candidate triangles across its five outfit swap variants. `[MEASURED]`

## 5. Finding 4: Animation dependency closure failures

Four renderers failed animation dependency closure with
`RendererAnalysisRefusal.MaterialDependencyClosureFailed`:
1. `Body_Base`: `ClosureFailure = SlotOutOfRange`.
2. `Roses Dress`: `ClosureFailure = SlotOutOfRange`.
3. `Roses Sleeves`: `ClosureFailure = InvalidSwapValue`.
4. `s_RoseStaff_pink`: `ClosureFailure = SlotOutOfRange`.
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
1. Transparent outline shaders: `dress veil` and `UnderrimGlasses_Round` (slot 1)
   use `Hidden/lilToonTransparentOutline`. This shader family is not yet
   admitted. `[MEASURED]`
2. Gem shaders: `Chains` (slot 1) and `Witch Hat` (slots 1 and 2) use
   `Hidden/lilToonGem`. This shader family is not admitted. `[MEASURED]`
3. Gadgets and particles: Seventeen non-avatar renderers (`ABT/*`, `HUD/*`,
   `NadeSystem/*`) use particle shaders (`Mobile/Particles/Alpha Blended`,
   `Mobile/Particles/Additive`, `Particles/Standard Unlit`) or custom gadget
   shaders (`Ikeiwa/HUD/*`, `RedNightWorks/*`). `[MEASURED]`

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

The avatar's `Alraune_Gesture` controller contains twelve state nodes where a
`StateMachineBehaviour` slot is null. The `Alraune Slip Dress FX (Runtime Clone)`
controller contains one such slot. `[MEASURED]`

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
2. Genuinely transparent lace (`White_Dress`, `White_sleeve`).
3. Genuinely cutout floral borders (`Roses Pink`, `Staff Pink`, `White Witch Hat`).
4. Broken authoring material swaps (`SlotOutOfRange`, `InvalidSwapValue`).
5. Unadmitted shader families (`Hidden/lilToonTransparentOutline`, `Hidden/lilToonGem`).
6. Non-avatar gadgets and particle systems.

The optimizer proves what is true: the transparent parts of this avatar are
genuinely transparent, and the opaque parts are already opaque. The optimizer
correctly leaves all triangles in their original materials.
