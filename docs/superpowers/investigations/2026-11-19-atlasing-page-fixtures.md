# Page format, border, and page size fixtures

Privacy note: This record contains measurements on synthetic in-memory textures only. It contains no private avatar data, no vendor source, and no machine identifiers. The GPU is named by its graphics API only.

Date: 2026-11-19.
Status on 2026-11-19: This record closes Gate 0 item 8 of `docs/superpowers/specs/2026-11-19-minimal-atlasing-design.md`. The user chose the page size rule, the protected mip default, and the maximum page size on 2026-11-19. No product code changed. No asset was written. Every probe ran as in-memory editor code on the dev editor instance and destroyed its objects.

## 1. Questions

Gate 0 item 8 asks for five facts:

1. The format table.
2. The Color quality bound.
3. The border function.
4. The page ladder and its UV rounding bound.
5. The protected mip default.

## 2. Format and quality

### Method

One synthetic 256 by 256 image has four regions: random noise, a smooth gradient, a hard checker, and a mixed pattern of sine waves and bit patterns. Alpha varies in all regions. The probe encoded the image with the editor CPU encoder. It then encoded the decoded result again and compared the two decoded results. It also copied the raw compressed blocks of an encoded texture into a larger compressed page at a block-aligned origin and compared decoded texels.

### Observations

Maximum per-channel difference, 0 to 255 scale, for a second encode of decoded data at the same block grid:

| Format | Noise | Gradient | Checker | Mixed |
| --- | --- | --- | --- | --- |
| BC7 | 138 | 4 | 9 | 24 |

For BC1 and BC3, the second encode at the same block grid differed by at most 4 in color and 0 in alpha over the whole image. A shift of 2 texels made the BC1 difference about 165.

The raw block copy of a BC7 texture into a page gave a difference of 0.

### Consequences

- The page format equals the source graphics format. The graphics format joins the group key. Supported formats are RGBA32, RGB24, BC1, BC3, and BC7, each in its sRGB or linear form. Other formats refuse the use.
- Protected levels are built by copying raw blocks from the source chain. Copied texels equal the source texels exactly.
- Borders inside the source and Repeat borders at a source edge are copies of source blocks. They are also exact.
- A Clamp border at a source edge repeats the edge texels. For BC1 and BC3, the border block keeps the endpoints of the edge block and repeats the indices of the edge column or row. That is exact, because the palette does not change. Uncompressed formats copy the texels. BC7 partitions make index repeats inexact, so a BC7 use that needs a Clamp edge border refuses with a name.
- The Color quality bound is 0. The output check requires exact equality of decoded texels at every protected level.

## 3. Border function

### Method

A probe shader sampled a texture with explicit gradients. The texture was black, except for one white texel on one mip level. The probe swept the gradient length, the anisotropic ratio up to 64, and six angles. It recorded the largest distance from the white texel at which any output was nonzero. The distance is in texels of the mip level that holds the white texel. The sample spacing was one eighth of a texel. The anisotropic filtering quality setting was ForceEnable. The probe did not change any setting. The graphics API was Direct3D 11 on one desktop GPU.

### Observations

| Texture anisotropy level | Largest reach in texels |
| --- | --- |
| 0 | 0.94 |
| 1 to 8 | 5.8, because ForceEnable raises these levels |
| 9 | 6.8 |
| 16 | 12.2 |

Level 0 was not raised by ForceEnable. This matches the desktop exception in `AlphaSamplingPolicy.ConservativeDesktop`.

### Consequences

- The border in texels of each protected level, `r`, is 1 for anisotropy level 0 and 16 for any other level. The 16 is the named maximum ratio of design section 8. It covers the measured reach of 12.2 with a margin.
- The border at level 0 is `max(r, b) * 2^(P-1)`. Here `b` is the block size, 4 for compressed formats and 1 for uncompressed formats. `P` is the protected level count.
- Placement origins and sizes align to `b * 2^(P-1)` texels at level 0.
- The texture importer default anisotropy level is 1. So most sources get `r = 16`.
- The evidence comes from one GPU. The release notes state this limit.

## 4. Page sizes and UV rounding

### Observations

- A page of 2176 by 1088 texels, which is not a power of two, encoded with a full mip chain in BC1, BC3, and BC7. `GraphicsFormatUtility.ComputeMipChainSize` equaled the raw data length in each format.
- With power-of-two pages, one whole 1024 source and its border need more than 1024 texels. Two such placements do not fit in 2048, so the page grows to 4096 and its memory grows four times.
- A remap in double precision, stored as Float32, moved a UV by at most `S * 2^-25` page texels, where `S` is the page size. 200000 random cases per page size from 512 to 8192 reached that bound and did not pass it. At 8192 the bound is 1/4096 texel. Direct3D 11 filters with 8 bits of subtexel precision, so the rounding is below one sixteenth of a filter step.

### Consequences

- The user chose tight pages on 2026-11-19. The page width and height are the packing bounds, rounded up to the alignment grid. Neither may exceed the maximum page size. The power-of-two ladder is removed.
- The UV rounding bound is one formula, `S * 2^-25` page texels, with `S` the larger page dimension.

## 5. Protected mip default and maximum page size

Area cost of the border for one whole 1024 source with `r = 16`:

| P | Level 0 border | Area increase |
| --- | --- | --- |
| 1 | 16 | 6 percent |
| 2 | 32 | 13 percent |
| 3 | 64 | 27 percent |
| 4 | 128 | 56 percent |

The user chose these defaults on 2026-11-19:

- Protected mip level default: 1. Level 0 is exact. Coarser levels come from Unity mip generation of the page. They can show neighbor texels at oblique and distant views.
- Maximum page size default: 4096 at first. After the spike below, the user chose 8192 on 2026-11-19. 8192 is also the most that the setting accepts.

### Maximum page size spike, 2026-11-19

The user asked for a spike before any change to the maximum page size default. The probes ran in memory on the dev editor instance, Unity 2022.3.22f1, Direct3D 11. Every probe destroyed its textures.

VRChat SDK limit, from source review:

- The SDK constant `MAX_SDK_TEXTURE_SIZE` is 8192. The avatar builder reports an error when a texture importer has `maxTextureSize` above 8192. So 8192 is allowed, and it is the ceiling.
- The check reads texture importers only. A texture without an asset importer is skipped. A generated page has no importer, so the SDK check cannot catch an oversize page.
- The probe device reported `SystemInfo.maxTextureSize` 16384. Unity created a texture 8196 texels wide without error. So neither Unity nor the device stops a page above 8192. AMUSE must enforce the ceiling itself.

Page probes. Each page was filled with random bytes, given a Unity mip chain, and compressed at Normal quality:

| Page | Format | Mip levels | Raw length equals `ComputeMipChainSize` | Compress time (ms) |
| --- | --- | --- | --- | --- |
| 4160 x 2080 | DXT1 | 13 | yes | 12 |
| 4160 x 2080 | DXT5 | 13 | yes | 9 |
| 4160 x 2080 | BC7 | 13 | yes | 546 |
| 8192 x 4096 | DXT1 | 14 | yes | 42 |
| 8192 x 4096 | DXT5 | 14 | yes | 47 |
| 8192 x 4096 | BC7 | 14 | yes | 2004 |
| 8192 x 8192 | DXT5 | 14 | yes | 86 |
| 8192 x 8192 | BC7 | 14 | yes | 4125 |

- Each page kept its size, its format, and a full mip chain.
- Filling an 8192 x 8192 page and generating its mips took between 4 and 5 seconds in the probe. The probe filled bytes in a managed loop, so this time is an upper bound for the fill step only.
- `Profiler.GetRuntimeMemorySizeLong` gave unstable values for the same page format. The spike does not use it.

Decode memory, measured as the managed heap change on one 8192 x 8192 level:

- `GetPixels` returned `Color[]` at 16 bytes per texel, so 1 GiB for the level.
- `GetPixels32` returned `Color32[]` at 4 bytes per texel, so 256 MiB for the level.
- The uncompressed RGBA32 working page with its mip chain adds about 4/3 of 256 MiB in native memory. That value is arithmetic and was not measured.

Packing arithmetic, from the border function of design section 7 with `P = 1`:

- A whole 2048 source with `r = 16` is a 2080 placement. With `r = 1` it is a 2056 placement.
- Two such placements need 4160 or 4112 texels on one side. Both are above 4096. So a 4096 maximum never puts two whole 2048 sources on one page.
- Under an 8192 maximum, up to three whole 2048 sources fit on each side. A tight page for two of them is 4160 x 2080 texels.
- A whole 4096 source is a 4128 placement. It refuses under a 4096 maximum. Under an 8192 maximum it fits, but only one fits on each side.

Streaming mip maps:

- The avatar builder reports an error for an importer texture with mip maps but without streaming mip maps.
- A generated texture starts with `streamingMipmaps` false. Setting the serialized property `m_StreamingMipmaps` through `SerializedObject` made the property read true.
- The spike did not build or upload an avatar. So it does not show that a generated page streams in a built bundle, or that VRChat enforces the 8192 ceiling at upload or at load. The ceiling comes from SDK source and the user statement.

## 6. Limits

- The format and quality probes used the editor CPU encoder. The build uses the same encoder path as fixture item 4 of the Gate 0 fixtures record.
- The reach probe ran on one GPU and one graphics API.
- The probes used synthetic images. Real textures can encode differently, but block copy does not depend on content.
