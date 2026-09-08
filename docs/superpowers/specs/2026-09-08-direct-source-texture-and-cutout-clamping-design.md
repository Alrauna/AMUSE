# Direct Source Image Mip Pipeline & Toon Cutout Clamping Design

Date: 2026-09-08
Status: approved design
Scope: direct uncompressed authoring texture decode from disk, toon shader
cutout parameterization for lilToon and Poiyomi, and pure area-averaged box
mipmap generation scoped by the user mip cap.

## 1. Problem and motivation

AMUSE proves triangle opacity by evaluating alpha mip chains against exact
continuous UV domains. In practice on real avatars, two separate issues
prevented valid triangles from proving opaque:

1. **Unity import downscaling and lossy BC7/DXT5 compression**:
   Unity's texture import pipeline downscales high-resolution textures (for
   example, a 4096x4096 authoring PNG downscaled to 1024x1024) and applies
   block compression (BC7 or DXT5). BC7 quantizes edge texels down from 255 to
   254 or 250. When Unity generates mipmaps from compressed or downscaled data,
   alpha blurs into opaque regions. The classifier's exact-one bar then marks
   these triangles as `MustRemainTransparent` or `Unknown`, even when the
   authoring source texture has millions of solid opaque pixels.

2. **Cutout shaders evaluated with an exact-one bar**:
   Cutout shaders discard fragments only when alpha is strictly below a cutoff
   threshold (`clip(alpha - _Cutoff)`). Fragments with `alpha >= _Cutoff` render
   completely opaque. Testing cutout textures against `alpha == 1.0` (255)
   incorrectly discards texels between `_Cutoff` and `1.0`, even though they
   render with 100% opacity on screen.

## 2. Architecture

```
                 Texture on Material
                         │
                         ▼
        Does an on-disk image file exist? (.png / .tga)
            │                           │
            │ Yes                       │ No (procedural / generated sub-asset)
            ▼                           ▼
  [SourceImageAlphaReader]      [Unity TextureImporter Fallback]
  - Read bytes from disk        - Uses current Unity import capture
  - Decode raw 8-bit RGBA       - Handles procedural or generated
  - Pure box mip pyramid          sub-asset textures
            │                           │
            └───────────┬───────────────┘
                        │
                        ▼
       [ToonMaterialCutoutSemantics]
       - Extract cutoff threshold per material:
           * lilToon Cutout: _Cutoff (bounded by 0.9999)
           * Poiyomi Cutout: _Cutoff (bounded by 1.0)
           * Transparent / Opaque: 1.0 (exact-one)
                        │
                        ▼
       [Thresholded Binary Mip Construction]
       - flag[i] = (sample >= threshold) ? 255 : 0
       - Stored in AlphaMipChain
                        │
                        ▼
       [User Mip-Cap Truncation]
       - chain.LimitedTo(maxMipLevel) (e.g. Mip 4)
                        │
                        ▼
       [Triangle Classification & Separation]
```

## 3. Direct source image reading (`SourceImageAlphaReader`)

`SourceImageAlphaReader` inspects a `Texture2D`:
1. It queries `AssetDatabase.GetAssetPath(texture)`.
2. If the file exists on disk and has an image extension (`.png`, `.tga`):
   - It reads the file bytes directly using `System.IO.File.ReadAllBytes`.
   - It decodes the uncompressed image at native authoring resolution.
   - It generates box-filtered mip levels from uncompressed data down to 1x1.
3. If the asset path is empty or refers to an in-memory/generated asset, it
   falls back to the existing Unity texture capture route.

## 4. Unified cutout parameterization (`ToonMaterialCutoutSemantics`)

Different toon shaders implement cutout alpha clipping with different property
names and bounds:

- **lilToon Cutout** (`Hidden/lilToonCutout`, `Hidden/lilToonCutoutOutline`):
  Reads `_Cutoff`. Admitted if finite and `<= 0.9999f`.
- **Poiyomi Cutout** (`.poiyomi/Poiyomi Toon`, `.poiyomi/Poiyomi Toon Two Pass`):
  Reads `_Cutoff` when effective rendering mode is cutout. Admitted if finite
  and `<= 1.0f`.
- **Transparent / Opaque modes**:
  Threshold defaults to `1.0f` (exact-one alpha).
- **Extensibility**:
  Designed so future toon shaders (such as UnityChanToonShader / UTS3, Sunao,
  etc.) register their cutoff extraction rules cleanly without modifying the
  core classification or capture engine.

## 5. Mip-cap scoping

Tail mip levels (such as 4x4 or 1x1) where the entire texture averages below
the cutoff threshold do not invalidate triangle opacity. The user-configured
mip cap (`PreserveTransparencyMaxMipLevel`, default Mip 4) truncates the
consulted mip chain prefix before classification occurs.

## 6. Upstream non-destructive tools

When an upstream tool (for example, Anatawa12 Avatar Optimizer's
`OptimizeTexture`) runs before AMUSE, it generates in-memory textures and places
them into container sub-assets.

In that situation:
1. The texture has no independent source file on disk.
2. The texture has no asset importer that can reproduce it.
3. It falls back to the Unity runtime texture capture route.
