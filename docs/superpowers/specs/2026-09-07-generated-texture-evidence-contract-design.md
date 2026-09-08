# Generated-Texture Evidence Contract Design

Date: 2026-09-07
Status: draft (producer characterization filled from source analysis; design
questions open)
Scope: the source-identity gate on build-transient textures, the capture of
alpha mip chains from generated texture objects, and the refusal policy for
uncharacterized producers.

## 1. Problem

AMUSE is a build-time optimizer. It proves a triangle visually opaque and
moves it to an opaque material copy. Every proof needs the alpha mip chain of
the deciding texture.

The evidence layer refuses a texture that is not the main asset at its asset
path. An importer reproduces only the main asset. A sub-asset texture has no
reproducible source. The gate fails closed.

The test avatar runs Anatawa12 Avatar Optimizer `TraceAndOptimize` with
`optimizeTexture` enabled. That pass runs before AMUSE inside the same NDMF
build. After it runs, every material texture is a generated sub-asset. The
source-identity gate refuses it. The alpha chain is never captured. The
resolution refuses. Every triangle answers Unknown. The full pipeline moves 0
triangles.

The `d4rkAvatarOptimizer` component also sits on the avatar. It does not run
inside NDMF. It runs later, at upload time, through a VRChat SDK callback.
Its output never reaches AMUSE. Source analysis confirms the ordering.

## 2. Measured evidence

All facts below are measured and verified.

- The gate is `UnityTextureEvidence.TryGetSourceId`. It refuses a texture
  that is not the main asset at its asset path. An importer reproduces only
  the main asset. A sub-asset texture has no reproducible source. The gate
  fails closed.
- The avatar runs Anatawa12 Avatar Optimizer `TraceAndOptimize` with
  `optimizeTexture` enabled. That pass runs before AMUSE inside the same NDMF
  build. The avatar also carries `d4rkAvatarOptimizer`. That one runs after
  NDMF, at upload time, through a VRChat SDK callback. Its output never
  reaches AMUSE.
- After `optimizeTexture` runs, every material texture is a generated
  sub-asset. Its asset path is non-empty. It is not the main asset at that
  path. The source-identity gate refuses it. The alpha chain is never
  captured. The resolution refuses. Every triangle answers Unknown.
- A control run with `optimizeTexture` turned off keeps every texture a
  pristine main asset. The full pipeline still analyzed 31 renderers and
  moved 0 triangles. So the generated-texture refusal is real, and it is not
  the only blocker on this avatar.
- The control run shows the remaining blocker stack. Most renderers use
  shader families that AMUSE does not ship yet: `Hidden/lilToonOutline`,
  `Hidden/lilToonGem`, and `Hidden/lilToonTransparentOutline`. Four
  renderers refuse at the animation layer. The only material swaps on disk
  target a camera gadget inside those renderers. The clothing renderers on
  shipped families resolve their materials but answer Unknown for every
  triangle. Those slot refusals are silent by design. Isolating them needs a
  separate investigation of the baked animation states.
- Replay of the production pipeline internals on the pristine asset material
  works end to end. The family is `LilToonTransparent`. The lilToon 2.3.4
  attestation passes. The production request asks
  `ScaleOffset|SourceIdentity|Sampling|AlphaChannel` for `_MainTex`. The
  closed capture returns an 11-level alpha chain. The refusal is caused by
  the generated textures. It is not caused by the shaders, the attestation,
  or the capture routes.
- The dress, sleeve, witch hat, and roses main textures are BC7. Streaming
  mipmaps are enabled on them. `_MainTex_ST` is identity. The face-effect
  material and its mask are DXT1. DXT1 is outside the alpha-evidence format
  allowlist by design.
- The user mip policy is Preserve Transparency Maximum Mipmap. The default
  cap is Mip 4. It shipped green this session. 1,997 of 1,997 EditMode tests
  pass. The policy truncates the consulted mip chain prefix at the cap. On
  this avatar it never received real content to decide. The texture evidence
  refused upstream.

## 3. Scope and non-goals

In scope:

- The five design questions in section 4.
- The evidence contract for build-transient generated textures.
- The refusal policy for uncharacterized producers.

Non-goals:

- Changes to the shader frontends, the attestation, or the capture routes.
  The replay proves that they work end to end on the pristine material.
- New shader families. The outline and gem families stay unshipped.
- Admission of DXT1 into the alpha-evidence format allowlist. DXT1 stays
  outside by design.
- Changes to the exact-one opacity bar.
- Changes to the mip cap. The cap stays a user policy.

## 4. Design questions

1. Can AMUSE capture an alpha chain from a build-transient texture object
   directly, with no importer reproduction?
2. What identity can pin such a texture, given that the generated asset GUID
   exists but the texture is a sub-asset?
3. What does the characterized producer guarantee about the generated
   textures: format, mip chain shape, streaming flags, determinism across
   builds? Section 6 answers this from source analysis.
4. Does the GPU blit route stay sound on generated textures, or is a CPU
   readback of the generated object required?
5. Which refusal stays when a producer is not characterized?

## 5. Constraints

- Fail closed. Uncharacterized producers keep refusing.
- Never mutate source assets. Generated build assets and the NDMF build copy
  are the only mutation targets.
- Exact-one alpha remains the opacity bar. The mip cap stays a user policy.
- No absolute paths, no host names, no private identifiers in this spec.
- Simplified Technical English for every human-read sentence.

## 6. Producer characterization (source analysis, Anatawa12 Avatar Optimizer
1.9.17)

All facts below come from reading the installed producer source.

### 6.1 Asset shape

- `OptimizeTexture` creates plain in-memory `Texture2D` objects and rewires
  materials with `SetTexture`. It never calls `AssetDatabase` itself.
- NDMF serializes each referenced in-memory texture into its own container
  asset. The container is a `SubAssetContainer` scriptable object. The
  texture is added with `AddObjectToAsset`. The generated texture is
  importer-free. No importer can reproduce it.
- The container and its sub-assets exist only for the build. NDMF deletes
  them after an upload build.

### 6.2 Format, mip chain, and streaming

- The generated texture keeps the source format when the block-copy path
  applies. Otherwise it is rendered, read back, and recompressed to the
  source format with the editor compressor.
- The mip chain is regenerated. `Apply(true)` writes every level. The chain
  length is the smaller of the atlas size limit and the source mip count.
- The streaming flags are copied from the source texture through serialized
  fields. A streaming source stays a streaming generated texture. This is
  decisive: the GPU route refuses streaming textures, and the clone route
  requires an importer. Both existing routes refuse a generated texture even
  after the source-identity gate learns to admit it. A third route must read
  the live object.
- The CPU readability follows the source texture. A non-readable source
  gives a non-readable generated texture.

### 6.3 Determinism

- The atlas island order comes from a hash set and an unstable sort. Equal
  islands can swap placement between builds.
- The blit path renders on the GPU and recompresses with the editor
  compressor. Both vary across graphics back ends and driver versions.
- Conclusion: generated bytes are not deterministic. The contract cannot
  cache by content hash or assume reproduction. Identity must pin the build
  instance, not the bytes.

### 6.4 Geometry

- `OptimizeTexture` is a true atlas pass. It rewrites mesh UVs to the atlas
  domain and duplicates shared vertices. AMUSE captures the effective build
  geometry, so its UV evidence already matches the atlas domain. No extra
  transform is needed.

### 6.5 d4rkAvatarOptimizer

- It merges same-dimension textures into `Texture2DArray` sub-assets at
  upload time, after NDMF. Its output never reaches AMUSE. A
  `Texture2DArray` is not a `Texture2D` and is already outside the capture
  surface. No contract work is needed for it.
