# Texture Alpha Evidence — Design

Milestone: `feat/texture-alpha-evidence`
Base commit: `016f3d2` (`origin/main`)
Date: 2026-08-19

## Executive decision summary

**The consumer-facing boundary this milestone was asked to design already exists.**
`AlphaSemanticsResolver` (merged at `33df8ea`) declares:

```csharp
internal delegate bool AlphaFieldProvider(
    TextureSourceId source,
    TextureChannel channel,
    out AlphaTextureData field);
```

with a fully specified predicate contract in its XML doc, and it has **one production
implementation: none.** Every existing test supplies a hand-built lambda. This milestone
writes the missing Unity implementation of that delegate and nothing else.

Consequently:

| Decision | Outcome |
| --- | --- |
| New immutable evidence type | **None.** `AlphaTextureData` is already the host-neutral immutable type, already defensively copies, already validates. The producer constructs it directly. |
| New seam / intermediate | **None.** The delegate is the seam. |
| Change to `TriangleAlphaClassifier` | **None.** |
| Change to `AlphaTextureData` | **None.** |
| Change to `MaterialSemantics` | **None.** |
| Change to `AlphaSemanticsResolver` | **None.** |
| Change to `UnityTextureEvidence` | **None.** The five-method boundary is untouched; no sixth method. |
| New production file | **One:** `Editor/Host/UnityAlphaFieldEvidence.cs` |
| New folder | **One:** `Editor/Host/` — forced, see [Placement](#placement--the-only-forced-structural-change) |
| Read route | `Texture2D.GetPixels32()` on the **imported** texture |
| Cache | **None in v1** |
| Channels supported | `TextureChannel.Alpha` only |
| Admitted v1 formats | `RGBA32`, `ARGB32`, `Alpha8`, `RGB24` — each **measured** predicate-equivalent to shader `.a` |
| Importer inspection | **None.** No `TextureImporter` is opened by the producer. |

**The central architectural insight**, established by measurement: reading the *imported*
`Texture2D` rather than the source file collapses almost the entire list of import
concerns this milestone was asked to investigate. Resize, `maxTextureSize`, NPOT scaling,
`alphaSource`, swizzling, `alphaIsTransparency`, sRGB, and platform overrides are all
**already applied to the texture the GPU samples**, so the producer needs to inspect none
of them and opens no `TextureImporter` at all.

Note the precise form of that claim — the two halves are not the same statement:

```
importer history does not need to be inspected
        !=
importer setting does not affect the imported field
```

The first is what the collapse establishes. The second is **false** for `alphaSource`,
resize, NPOT scaling, and swizzling, all of which demonstrably change the field. The
producer simply reports whichever field resulted. What survives as an actual condition is a
small set of format and state predicates, each measured against a real shader sample.

## Verified base state

| Check | Result |
| --- | --- |
| Previous milestone merged | Yes. PR #11 (`test/semantic-adapter-characterization`) merged as `016f3d2` on `origin/main`. Remote topic branch deleted. |
| `main` synchronized | Fast-forwarded to `016f3d2`; identical to `origin/main`. |
| Worktree at branch creation | Clean. |
| Branch created | `feat/texture-alpha-evidence` from `016f3d2`. |
| Stacked on old feature branch | No. |
| Unity project | `E:/AI/Git/AMUSE/Assets`, Unity **2022.3.22f1**, active target `StandaloneWindows64`, color space Gamma. |
| Unity instance used | The **public** development project, confirmed by `Application.dataPath` before any operation. The private avatar testbed was **not** used. |

## Current contract: what `TriangleAlphaClassifier` actually requires

Read from `Editor/Analysis/TriangleAlphaClassifier.cs` at `016f3d2`. Nothing below is
inferred.

### `AlphaTextureData` — the alpha input type

```csharp
internal AlphaTextureData(int width, int height, IReadOnlyList<byte> alpha8BottomToTop)
```

| Property | Contract as implemented |
| --- | --- |
| Dimensions | `width > 0`, `height > 0`, else `ArgumentOutOfRangeException`. No upper bound. |
| Data length | `(long)width * height == alpha8BottomToTop.Count`, else `ArgumentException`. |
| Null data | `ArgumentNullException`. |
| Representation | **`byte`**, not a normalized float. |
| Row order | **Bottom-to-top, row-major**: `GetAlpha(x, y) => _alpha8[y * Width + x]`. |
| Ownership | **Copies into a private array in the constructor.** The producer need not defend its own buffer, and a caller cannot mutate the field afterwards. |
| Derived facts | `IsFullyOpaque` / `IsFullyNonOpaque` computed once at construction. |
| Equality | None. Reference equality only. Not required by any consumer. |
| Unity dependency | **None.** |

### The only predicate the classifier evaluates on alpha

Every one of the four sampling paths tests exactly one thing:

```csharp
if (texture.GetAlpha(x, y) == byte.MaxValue) { continue; }   // 255 == opaque
```

**The magnitude of a non-255 byte is never read.** The classifier's alpha input is
effectively a *per-texel boolean* — "is this texel exactly opaque?" — transported in a
byte. This is the single most important fact for the producer's exactness argument
(see [Exactness argument](#exactness-argument)).

### Sampling and geometry inputs

| Input | Contract |
| --- | --- |
| `AlphaSamplingSettings` | `{Point, Bilinear} x {Clamp, Repeat}`. Any other value throws. **No mip level, no mip bias, no anisotropy, no border colour.** |
| `TriangleAlphaInput` | Three `Vector3` positions plus either UV0 for all three vertices or an explicit `MissingUv0`. |
| Mip assumption | Implicit and total: the classifier models **one** texel grid of `Width x Height`. There is no mip concept in the type system. |
| Workload limit | `MaxSupportRegions = 65536` candidate texel regions per triangle. This bounds *geometry*, not texture size. |
| Malformed vs Unknown | Malformed **throws** (non-finite positions/UVs, invalid sampling enum, null texture). Un-analysable but well-formed input returns **`Unknown`** (degenerate geometry, missing UV0, workload exceeded). |

### What is therefore *not* the producer's job

Filter mode, wrap mode, UV mapping, and geometry all arrive through
`TextureSample.Sampling` / `TriangleAlphaInput` and are validated by
`UnityTextureEvidence.TryGetSampling` and `AlphaSemanticsResolver`. **The producer must
not re-derive them**; it answers one question only: *what is the alpha field?*

## Current contract: the `AlphaSemanticsResolver` output seam

`AlphaSemanticsResolver.Resolve(SemanticOutput<ScalarSemanticValue>, AlphaFieldProvider)`
returns an `AlphaResolution` that is one of:

- **`Uniform(outcome)`** — geometry-independent. Reached for `Constant(1f)` → `ProvenOpaque`,
  `Constant(<1f)` → `MustRemainTransparent`, and `sample x k` with `k < 1` →
  `MustRemainTransparent`.
- **`Classified(field, sampling)`** — delegates per-triangle to `TriangleAlphaClassifier`.
- **`Refused(failure)`** — one of `SemanticsUnknown`, `UnsupportedMultiplier`,
  `UnsupportedUvMapping`, `UnsupportedSampling`, `MissingTextureEvidence`.

The producer is reached **only** through `MissingTextureEvidence`'s guard, i.e. after
the resolver has already proven:

1. the semantic Alpha value is `Complete`;
2. the UV mapping is identity on channel 0 (`IsSupportedMapping`);
3. the sampling maps onto `{Point, Bilinear} x {Clamp, Repeat}` (`TryMapSampling`);
4. any multiplier is exactly `1f` (a `k < 1` path never needs field *contents*).

**The seam is sufficient. No new contract is needed and `MaterialSemantics` requires no
change.** The producer receives `(TextureSourceId, TextureChannel)` — deliberately not a
`Texture` — and returns `bool` + `AlphaTextureData`, which is exactly a refusal predicate
in the established `UnityTextureEvidence` style.

### The provider contract, quoted from the merged source

> It returns false unless the provider can prove, for the named source and channel over
> the relevant base-level texel domain in bottom-to-top order, that every effective
> per-texel scalar value is finite and within [0, 1], that byte 255 marks exactly the
> texels whose value is exactly 1, and that every other byte marks a value strictly below
> 1. […] The source need not itself be an uncompressed 8-bit b/255 field.

This is a **predicate-equivalence** contract, not a value-exactness contract. The design
below satisfies it and does not widen it.

## Boundary diagram

```
Material (Unity)                                     [Semantics — Unity-bound]
   |  Poiyomi / lilToon frontends
   v
MaterialSemantics.Alpha : SemanticOutput<ScalarSemanticValue>
   |        \- TextureSample { TextureSourceId, UvMapping, TextureSampling }
   v
AlphaSemanticsResolver.Resolve(alpha, provider)      [Analysis — NO UnityEditor]
   |                                    ^
   |                                    |  AlphaFieldProvider delegate
   |            UnityAlphaFieldEvidence.TryGetAlphaField     <- NEW  [Host — Unity-bound]
   |                                    ^
   |                                    |  Texture2D.GetPixels32()
   |                          imported Texture2D (asset)
   v
AlphaResolution.Classify(TriangleAlphaInput)
   v
TriangleAlphaClassifier.Classify(triangle, AlphaTextureData, AlphaSamplingSettings)
   v
TriangleAlphaOutcome { ProvenOpaque | MustRemainTransparent | Unknown }
```

The arrow into the resolver is a *delegate*, so `Analysis` never names the Host type and
never gains a `UnityEditor` reference.

## Definition of effective alpha for the supported domain

> **Effective alpha** at texel `(x, y)` is the scalar value a fragment shader receives in
> the `.a` component of `tex2D(sampler, uv)` when the sample lands exactly on that texel's
> centre at mip level 0, for the **active build target at analysis time**.

### The claim AMUSE makes, stated precisely

`GetPixels32()` does **not** return raw GPU memory. It returns a converted CPU `Color32`
*view* of the imported texture. AMUSE therefore does **not** claim that the imported CPU
data is universally identical to what the GPU samples. The claim is narrower and is the
only one the classifier needs:

> For the positively supported format and state domain, the imported CPU view is
> **predicate-equivalent** to effective mip-0 shader alpha:
>
> ```
> GetPixels32().a == 255   <=>   tex2D(...).a is exactly 1
> GetPixels32().a <  255   <=>   tex2D(...).a is strictly below 1
> ```

Predicate equivalence is weaker than value identity and stronger than a bound. It is
exactly what `AlphaFieldProvider` asks for and exactly what `TriangleAlphaClassifier`
consumes, because the classifier reads no non-255 magnitude. Every format admitted to the
allow-list was **measured** against a real shader sample (Experiment 5); a format that
cannot be positively shown predicate-equivalent is not admitted.

Four candidate layers were distinguished, per the milestone brief:

| Layer | Relationship to effective alpha |
| --- | --- |
| Source asset pixels (the PNG on disk) | **Not** effective alpha. Separated from it by resize, NPOT scaling, `alphaSource`, swizzling, and compression. |
| Imported texture data | The layer the GPU uploads and samples. AMUSE reasons about this layer, not the source file. |
| The CPU `Color32` view of it (`GetPixels32`) | A *converted view* of the imported data — **not** assumed identical to it. Proven **predicate-equivalent** to shader `.a` for the admitted formats, and measured to diverge for the refused ones. |
| Values the classifier models | A `Width x Height` grid of "exactly opaque / not exactly opaque". Reachable from the CPU view by the predicate above. |

**Consequence — the collapse.** AMUSE reads the imported texture, so every transformation
between the source file and the imported data is already applied. The producer therefore
needs **no importer inspection**. Two distinct claims are involved and they must not be
conflated:

```
importer history does not need to be inspected
        !=
importer setting does not affect the imported field
```

The first is what the collapse establishes. The second is **false** for several settings,
and the design does not assert it.

**Class 1 — settings that change the imported alpha field.** The producer does not inspect
them, and simply reports whatever field resulted:

| Setting | Measured effect on the field |
| --- | --- |
| `alphaSource` | **Changes it.** `FromInput` keeps input alpha; `FromGrayScale` generates alpha from luminance (measured: alpha became `20`); `None` yields a no-alpha import. |
| `maxTextureSize` / resize | **Changes it** — dimensions and values (measured: 4x4 → 2x2, alpha `128` → `223`). |
| NPOT scaling | **Changes it** under `ToNearest` (3x3 → 4x4, resampled); `None` preserves 3x3. |
| Swizzling (`swizzleA`) | **Changes it** — baked into the imported data (Experiment 6). |

**Class 2 — settings measured not to affect the alpha predicate at all:**

| Setting | Measured effect |
| --- | --- |
| `alphaIsTransparency` | None. It dilates RGB for filtering; the alpha channel is untouched. |
| `sRGBTexture` | None. Alpha is never sRGB-encoded; only RGB is. |

**Class 3 — no reconciliation needed:** platform overrides (the loaded texture *is* the
active target's import) and texture streaming (can only affect mip levels > 0, which the
domain excludes).

None of the three classes appears as a condition in the supported domain. Gating on them
"because it sounds safe" would be a false condition, as the brief warned against — but
Class 1 settings are still *observable in the result*, and the tests assert exactly that
(see [Test strategy](#test-strategy)).

## Unity 2022.3 evidence research

All measurements were taken in the **public** development project (Unity 2022.3.22f1,
`StandaloneWindows64`) via `execute_code`, using 4x4 RGBA32 PNGs written with a known
alpha pattern (bottom-left texel `128`, top-right texel `254`, all others `255`), imported
with `mipmapEnabled = false`. Scratch assets were created under
`Assets/AmuseScratch_TexProbe` and **deleted**; the worktree was verified clean afterwards.

### Experiment 1 — import state

| Case | `format` | dims | `isReadable` | `mipmapCount` | `GetPixels32` alpha result |
| --- | --- | --- | --- | --- | --- |
| Default import | `DXT5` | 4x4 | **False** | 1 | **throws `ArgumentException`** |
| Readable + Uncompressed | `RGBA32` | 4x4 | True | 1 | `128 … 254`, 14 texels at 255 — **exact** |
| Readable + Compressed | `DXT5` | 4x4 | True | 1 | `128 … ` **`255`**, 15 at 255 — **false opaque** |
| `alphaIsTransparency = true` | `RGBA32` | 4x4 | True | 1 | `128 … 254` — **unchanged** |
| `maxTextureSize = 2` | `RGBA32` | **2x2** | True | 1 | `223 …` — resampled, self-consistent |
| `sRGBTexture = false` | `RGBA32` | 4x4 | True | 1 | `128 … 254` — **unchanged** |
| `mipmapEnabled = true` | `RGBA32` | 4x4 | True | **3** | base level unchanged |

### Experiment 2 — format allow-list probing

| Forced platform format | Resulting `format` | `GetPixels32` alpha | Verdict |
| --- | --- | --- | --- |
| `RGB24` | `RGB24` | all `255` (16/16) | **exact** — alpha structurally absent ⇒ 1 |
| `Alpha8` | `Alpha8` | `128 … 254` | **exact** |
| `RGBAHalf` | `RGBAHalf` | `128 … 254` | *appears* exact — but see Experiment 3 |
| `ARGB16` | `ARGB4444` | `136 … 255` | lossy at import; predicate survives, but unverified — **refuse in v1** |
| `BC7` | `BC7` | `128 … ` **`255`** | **false opaque** |
| `DXT5Crunched` | `DXT5Crunched` | `128 … ` **`255`** | **false opaque** |
| NPOT 3x3, `ToNearest` | `RGBA32` | 3x3 → **4x4**, resampled | baked in |
| NPOT 3x3, `None` | `RGBA32` | **3x3** preserved | baked in |
| `alphaSource = FromGrayScale` | `RGBA32` | alpha `20` everywhere | baked in |

### Experiment 3 — the float-format hazard (decisive)

```
RGBAHalf, alpha set to 0.999f
  -> GetPixels32().a == 255          <- FALSE OPAQUE
  -> GetPixels().a   == 0.999023438  <- the true value, strictly below 1
RGBA32, alpha 254 / 255 -> GetPixels32().a == 254 / 255   (exact)
```

`GetPixels32` quantizes a float channel to a byte by **rounding**, so any value in roughly
`[0.998, 1.0)` becomes 255. Experiment 2's apparently-clean `RGBAHalf` row was a
coincidence of round-tripping `b/255` values. **This is why the allow-list is defined by
native alpha bit depth, not by "is it lossy".**

### Experiment 4 — row order and type discrimination

```
Texture2D 2x2, SetPixels32 { 1, 2, 3, 4 }
  GetPixels32()[0].a == 1   GetPixel(0,0).a == 1   <- index 0 IS bottom-left
  GetPixel(1,0).a    == 2                          <- index 1 is +x
  GetPixel(0,1).a    == 3                          <- index 2 is +y
RenderTexture:  is Texture == True,  as Texture2D == null
```

`Color32[]` from `GetPixels32` is **row-major, bottom-to-top** — byte-for-byte the layout
`AlphaTextureData` declares (`_alpha8[y * Width + x]`). **No transposition, no row flip,
no index arithmetic is required.** `Texture2D.GetPixels32(int miplevel = 0)` reads mip 0
by default (Unity 2022.3 ScriptReference).

A `RenderTexture` is a `Texture` but not a `Texture2D`, so the cast refuses it for free.

### Experiment 5 — CPU view versus shader view (the admission gate)

The gate that decides the allow-list. A scratch ShaderLab file
(`Hidden/AmuseScratch/AlphaPredicate`) sampled each imported texture and emitted a
*predicate*, not a value, so the readback's own 8-bit quantization cannot corrupt the
answer:

```hlsl
float4 t = tex2D(_MainTex, i.uv);
return float4((t.a >= 1.0) ? 1 : 0,        // eqOne  — the predicate under test
              (t.a >= 0.5) ? 1 : 0,        // ge05   — separates 0 from 128
              (t.a >= 0.9) ? 1 : 0,        // ge09   — separates 128 from 254
              (t.r >= 1.0) ? 1 : 0);       // rOne   — channel-confusion check
```

Each case used a **uniform** 4x4 texture (every texel identical), blitted 1:1 into a
same-sized `ARGB32` `RenderTexture` and read back. Uniformity makes the result independent
of blit orientation, so no row-flip assumption enters the measurement. Point filter, Clamp
wrap, no mips, `isReadable`, uncompressed. Source `R = 64` throughout, so a channel mix-up
would show as `rOne = 1`.

Source alpha values tested: **0, 128, 254, 255**.

| Requested format | Imported as | srcA=0 | srcA=128 | srcA=254 | srcA=255 | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| `RGBA32` | `RGBA32` | cpu 0 / gpu 0 | cpu 128 / gpu 0 | cpu 254 / gpu 0 | cpu 255 / gpu 1 | **AGREE 4/4** |
| `ARGB32` | `ARGB32` | cpu 0 / gpu 0 | cpu 128 / gpu 0 | cpu 254 / gpu 0 | cpu 255 / gpu 1 | **AGREE 4/4** |
| `Alpha8` | `Alpha8` | cpu 0 / gpu 0 | cpu 128 / gpu 0 | cpu 254 / gpu 0 | cpu 255 / gpu 1 | **AGREE 4/4** |
| `RGB24` | `RGB24` | cpu 255 / gpu 1 | cpu 255 / gpu 1 | cpu 255 / gpu 1 | cpu 255 / gpu 1 | **AGREE 4/4** |
| `BGRA32` | — | \- | \- | \- | \- | **UNREACHABLE** |

("cpu N" is `GetPixels32().a`; "gpu 1" means the shader observed `.a >= 1.0`. The
predicate compared is `cpu == 255` against `gpu == 1`.)

Three findings:

1. **All 16 comparisons agree.** `RGBA32`, `ARGB32`, `Alpha8`, and `RGB24` are positively
   proven predicate-equivalent at every tested value.
2. `rOne` was `0` in every alpha case, confirming the shader read the alpha channel and
   not red. `Alpha8` in particular returns its value through `.a` in a shader, not `.r`;
   had it uploaded as a red-only format the row would have diverged.
3. **`TextureImporterFormat` has no `BGRA32` member in 2022.3** (the enum offers
   `Alpha8, ARGB16, RGB24, RGBA32, ARGB32, RG32`), so Unity cannot be asked to produce it
   through the importer and it **cannot be positively proven**. It is therefore **removed
   from the v1 allow-list**, per the review instruction to admit only what is proven.

### Experiment 6 — `TextureImporter` alpha swizzling (the stop-condition check)

`TextureImporter.swizzleR/G/B/A` **exist** in 2022.3.22 with values
`R, G, B, A, OneMinusR, OneMinusG, OneMinusB, OneMinusA, Zero, One`. If swizzling were
applied at *sample* time rather than baked at import, `GetPixels32().a` would not reflect
the channel the shader observes and the no-`TextureImporter` producer would be unsound.

Two uniform `RGBA32` sources with deliberately distinct red and alpha, crossed with four
`swizzleA` modes:

| src R | src A | `swizzleA` | `GetPixels32().a` | shader `.a >= 1` | Predicate |
| --- | --- | --- | --- | --- | --- |
| 255 | 0 | `A` | 0 | 0 | **AGREE** |
| 255 | 0 | `R` | **255** | **1** | **AGREE** |
| 255 | 0 | `One` | 255 | 1 | **AGREE** |
| 255 | 0 | `OneMinusA` | **255** | **1** | **AGREE** |
| 0 | 255 | `A` | 255 | 1 | **AGREE** |
| 0 | 255 | `R` | **0** | **0** | **AGREE** |
| 0 | 255 | `One` | 255 | 1 | **AGREE** |
| 0 | 255 | `OneMinusA` | **0** | **0** | **AGREE** |

**Result: 8/8 agree, and the swizzle demonstrably moved both views together.** The
`A → R` and `A → OneMinusA` rows are the decisive ones: the CPU byte changed away from the
source alpha in exactly the cases where the shader value changed, and by the same
predicate. Swizzling is applied **at import**, so `GetPixels32().a` already reflects the
alpha channel shader sampling observes.

**The stop condition did not fire.** The no-`TextureImporter` producer remains sound, and
no swizzle inspection is required.

### Experiment 7 — `GetPixels32(0)` failure taxonomy

| Provoked condition | Exception |
| --- | --- |
| Texture made non-readable (`Apply(false, true)`) | `System.ArgumentException` — *"texture data is either not readable, corrupted or does not exist"* |
| Non-readable imported texture | `System.ArgumentException` (Experiment 1) |
| `GetPixels32(5)` on a texture with one mip | `System.ArgumentException` — *"invalid mipmap level"* |
| Destroyed `Texture2D` | `UnityEngine.MissingReferenceException` |
| Destroyed `Texture2D`, reading `.isReadable` | `UnityEngine.MissingReferenceException` |

Two facts that shape the implementation:

- **`MissingReferenceException`'s base type is `System.SystemException`, not
  `UnityException`.** Catching `UnityException` would *not* catch it. This is the kind of
  assumption that has to be measured rather than remembered.
- A destroyed `Texture2D` satisfies Unity's overloaded `== null`, so an explicit null check
  rejects it **before** any read. `ReferenceEquals(texture, null)` is `false` for a
  destroyed object and must not be used.

## Positive allow-list — the supported first domain

`TryGetAlphaField` returns `true` only when **all** of the following hold:

| # | Condition | Why it is required |
| --- | --- | --- |
| 1 | `channel == TextureChannel.Alpha` | The only channel any frontend produces for Alpha (Poiyomi: `TextureChannel.Alpha`; lilToon: constant, never sampled). R/G/B would additionally need an sRGB-transfer argument. |
| 2 | The `TextureSourceId` resolves to a `Texture` supplied to this producer | Identity is never fabricated; see [Identity resolution](#identity-resolution). |
| 3 | `texture is Texture2D` | `GetPixels32` exists only there. Excludes `RenderTexture`, `Cubemap`, `Texture2DArray`, `Texture3D`, `CustomRenderTexture`. |
| 4 | `texture2D.isReadable` | Without a CPU copy there is no non-GPU route to the data. Measured: `GetPixels32` throws otherwise. |
| 5 | `texture2D.mipmapCount == 1` | The classifier models exactly one grid. Redundant with `TryGetSampling` upstream, kept as an independent field-side obligation so the producer is sound when called directly. |
| 6 | `texture2D.format` in `{RGBA32, ARGB32, Alpha8, RGB24}` | **Every member measured predicate-equivalent against a real shader sample** at alpha 0/128/254/255 (Experiment 5). Nothing provisional remains. |
| 7 | `texture2D.width > 0 && texture2D.height > 0` | `AlphaTextureData` throws otherwise; refuse rather than throw. |
| 8 | `GetPixels32(0)` returns without an expected read failure | See [Fail-closed reads](#fail-closed-reads). |
| 9 | The returned array holds exactly `width * height` entries | Defensive; a mismatch means an assumption broke, and `AlphaTextureData` would throw. |

`BGRA32` was in the draft allow-list and is **removed**: `TextureImporterFormat` has no
`BGRA32` member in 2022.3, so Unity cannot be asked to produce one and predicate
equivalence cannot be positively demonstrated. Admitting it would be widening under
uncertainty.

Conditions deliberately **not** included, because measurement showed them unnecessary:
`sRGBTexture`, `alphaIsTransparency`, `alphaSource`, `maxTextureSize`, `npotScale`,
`textureType`, `textureShape`, platform-override presence, streaming settings, wrap mode,
filter mode, anisotropy, and the existence of a `TextureImporter` at all.

> **The producer needs no `TextureImporter`.** Every fact it checks is on the `Texture2D`
> itself. This is a stronger position than `UnityTextureEvidence`'s importer-based
> predicates and means a well-formed generated-but-registered texture is not refused for
> the wrong reason.

### Format allow-list rationale

Two distinct arguments, one per group:

**Group A — native 8-bit UNorm alpha** (`RGBA32`, `ARGB32`, `Alpha8`): `Color32.a` is a
normalized accessor, so channel order in memory is irrelevant — measured for `ARGB32`,
whose memory order differs from `RGBA32` yet whose CPU and shader predicates agree. The
GPU maps UNorm8 `b` to `b/255`, so
`b == 255` iff the value is exactly `1.0`, and `b < 255` iff the value is strictly below 1.
Exact.

**Group B — alpha structurally absent** (`RGB24`): the sampler returns `1.0` for a missing
alpha component per D3D/Vulkan/GL rules; `GetPixels32` reports `255` (measured, 16/16).
The predicate holds trivially and no compression or quantization can affect a channel that
does not exist. Confirmed against a real shader sample for all four source alpha values.

Both arguments are **corroborated, not replaced**, by Experiment 5: the reasoning above
says why the predicate should hold, and the measurement says that it does.

### Fail-closed reads

Experiment 7 showed that a destroyed `Texture2D` throws `MissingReferenceException` from
**`.isReadable` itself**, not only from `GetPixels32`. The guard therefore covers every
Unity-object evidence read the producer performs after the null and `Texture2D` checks —
`isReadable`, `format`, `mipmapCount`, `width`, `height`, and the pixel read — while
`ArgumentException` stays narrowly associated with the `GetPixels32(0)` evidence read,
which is the only operation measured to raise it:

```csharp
try
{
    if (!texture2D.isReadable) { return false; }
    if (texture2D.mipmapCount != 1) { return false; }
    if (!IsSupportedFormat(texture2D.format)) { return false; }

    var width = texture2D.width;
    var height = texture2D.height;
    if (width <= 0 || height <= 0) { return false; }

    Color32[] pixels;
    try
    {
        pixels = texture2D.GetPixels32(0);
    }
    catch (ArgumentException)          // not readable, corrupted, absent, or invalid mip
    {
        return false;                  // field stays null
    }

    if (pixels.Length != (long)width * height) { return false; }
    // … build the byte[] and the AlphaTextureData …
}
catch (MissingReferenceException)      // destroyed between the null check and any read
{
    return false;
}
```

Constraints, all deliberate:

- **No `catch (Exception)`, no bare `catch { }`, and no `catch (UnityException)` as a
  substitute.** Only the two exception types Experiment 7 actually produced are caught. An
  unexpected exception type is a defect and must surface, not be swallowed into a silent
  refusal.
- `MissingReferenceException` is caught **explicitly**, because its base type is
  `SystemException` — catching `UnityException` would miss it entirely.
- The **primary** defence against a destroyed texture is the `texture == null` check at the
  head of the method, using Unity's overloaded operator, which is `true` for a destroyed
  object. `ReferenceEquals(texture, null)` is `false` for one and must not be used. The
  `MissingReferenceException` handler is defensive depth for a destruction that races the
  reads; it is not the path a destroyed texture normally takes.
- **No texture-size cap.** No evidence requires one; `isReadable` already bounds the
  realistic input set. Adding a cap would be a speculative gate.

**Testability note.** The `width`/`height`/length checks and the two catches guard states
Unity 2022.3 is not known to produce through the approved architecture once the positive
preconditions pass. They are retained as defensive hardening and verified by code review;
no test seam, mock `Texture2D`, reflection, or deliberate corruption is introduced to
manufacture them.

## Refusal / Unknown matrix

| Input state | Result | Evidence |
| --- | --- | --- |
| Non-readable texture (**the Unity default**) | `false` | Measured: `GetPixels32` throws |
| `DXT5` | `false` | Measured false opaque (254 → 255) |
| `BC7` | `false` | Measured false opaque |
| `DXT5Crunched` | `false` | Measured false opaque |
| `DXT1`, `ETC`, `ASTC`, `PVRTC`, any other compressed | `false` | Not measured; same class, refuse |
| `RGBAHalf`, `RGBAFloat`, `RGBA64`, `RHalf`, `RFloat` | `false` | Measured false opaque via `GetPixels32` rounding |
| `ARGB4444`, `RGBA4444` | `false` | Predicate probably survives (x17 expansion) but unverified; no consumer |
| `BGRA32` | `false` | **Unreachable through `TextureImporterFormat` in 2022.3; predicate equivalence could not be positively proven** |
| Any format not on the allow-list | `false` | The list is positive; absence is refusal |
| `GetPixels32` throws `ArgumentException` / `MissingReferenceException` | `false` | Expected evidence-read failure, caught explicitly |
| Mipmapped texture | `false` | Classifier models one grid |
| `RenderTexture` / `Cubemap` / array / 3D | `false` | Not a `Texture2D` |
| Scene-only or generated texture with no `TextureSourceId` | `false` | Never reaches the producer — `TryGetSourceId` refused upstream, so no `TextureSample` exists |
| Source id not among the supplied textures | `false` | Identity is never guessed |
| `channel != Alpha` | `false` | Unsupported in v1 |
| Zero-dimension texture | `false` | Refuse rather than throw |
| `null` texture in the supplied set | *skipped at construction* | See [Malformed versus unsupported](#malformed-versus-unsupported) |

Every `false` propagates to `AlphaResolutionFailure.MissingTextureEvidence`, which is a
material-scoped refusal — **not** a per-triangle `Unknown`. This is the existing
distinction and the producer does not blur it.

## Exactness argument

No epsilon, no tolerance, no sampling, no histogram, no heuristic appears anywhere.

The claim is **predicate equivalence**, not value identity — see
[The claim AMUSE makes](#the-claim-amuse-makes-stated-precisely).

1. The classifier's only alpha predicate is `byte == 255`. Non-255 magnitudes are never read.
2. For Group A formats, UNorm8 decode is `b/255`, so `b == 255` iff the sampled value is
   exactly `1.0`, and every other byte gives a value strictly below 1 and at least 0. Both
   bounds of the delegate's `[0, 1]` attestation hold exactly. **Measured against a real
   shader sample** at alpha 0/128/254/255 for `RGBA32`, `ARGB32`, and `Alpha8`
   (Experiment 5), including under every `swizzleA` mode tested (Experiment 6).
3. For Group B, alpha is absent and the sampler returns exactly `1.0`; `GetPixels32`
   reports `255` uniformly. **Measured**, 4/4.
4. Under Point filtering the sampled value is one texel's value, so the predicate transfers
   directly. Under Bilinear the sampled value is a convex combination of up to four texel
   values, all in `[0, 1]`; such a combination equals 1 **iff** every positive-weight
   contributor equals 1. That is precisely what the classifier tests by scanning the
   contributing texel neighbourhood, and it is why the delegate's contract is phrased as a
   predicate rather than as value equality.
5. Every refused case above is refused because step 2 or 3 **cannot be asserted**, not
   because the deviation was measured to be small. Compressed and float formats are
   refused even though most of their texels round-trip correctly — one measured
   `254 → 255` is a correctness bug, and its rarity is irrelevant.

## Proposed producer API

```csharp
// Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs
namespace Alrauna.Amuse.Editor.Host
{
    internal sealed class UnityAlphaFieldEvidence
    {
        internal UnityAlphaFieldEvidence(IEnumerable<Texture> textures);

        // Signature-compatible with AlphaFieldProvider; pass as a method group.
        internal bool TryGetAlphaField(
            TextureSourceId source,
            TextureChannel channel,
            out AlphaTextureData field);
    }
}
```

Call site:

```csharp
var evidence = new UnityAlphaFieldEvidence(new[] { material.GetTexture("_MainTex") });
var resolution = AlphaSemanticsResolver.Resolve(semantics.Alpha, evidence.TryGetAlphaField);
```

### Identity resolution

The constructor builds `Dictionary<TextureSourceId, Texture2D>` by calling the **existing**
`UnityTextureEvidence.TryGetSourceId` on each supplied texture. This is deliberate:

- The producer **never parses** the `TextureSourceId` string. Its
  `unity-asset:<guid>:<localId>` format stays an implementation detail of one method in
  one class, exactly as the characterization milestone left it.
- No `AssetDatabase.GUIDToAssetPath` scan, no sub-asset enumeration, no path handling, no
  ordering question — therefore deterministic by construction.
- Identity is produced by the same function the frontends used to produce the
  `TextureSample`, so the two can never disagree.
- Instance scope gives the evidence a natural, obvious lifetime.

Ambiguity rule: if two supplied textures resolve to the same `TextureSourceId` they are the
same asset, so first-wins is sound; a texture whose id cannot be resolved is skipped, and a
later lookup for it simply refuses.

### Placement — the only forced structural change

Measured at `016f3d2`:

- `Editor/Analysis` has **no dependency on the `UnityEditor` namespace** — it is the
  host-neutral proof core, and `AlphaSemanticsResolver` documents that it "never opens an
  asset".
- `Editor/Semantics/*.cs` is host-bound but is **depended upon by** `Analysis`, not the
  reverse (`AlphaSemanticsResolver` has `using Alrauna.Amuse.Editor.Semantics`).

The producer needs `AlphaTextureData` (Analysis) **and** `Texture2D` (Unity). Putting it in
`Analysis` would introduce the proof core's first `UnityEditor`/asset dependency — the
milestone's stated prohibition. Putting it in `Semantics` would invert the existing
dependency direction. Therefore a third location is required, not preferred:
`Editor/Host/`, same assembly, no asmdef change.

This is proposed **because two existing constraints leave no alternative**, not to scaffold
a host layer. It gets exactly one file.

A companion source-text test makes the boundary enforceable, in the spirit of
`UnityTextureEvidence`'s five-member reflection guard — *a boundary nobody can verify is a
boundary that erodes*. The invariant it asserts is **"`Editor/Analysis` has no dependency
on the `UnityEditor` namespace"**, matched as a word-boundary identifier so it also catches
fully-qualified references (`UnityEditor.AssetDatabase.…`) and aliases
(`using AD = UnityEditor.AssetDatabase;`), not merely a `using UnityEditor;` directive.

## Relationship to `UnityTextureEvidence`

**Untouched.** No sixth method, no signature change, no widened predicate. The
characterization milestone's `SharedClass_ExposesExactlyFiveSemanticFacts` guard must
still pass unmodified.

The producer *consumes* one of the five (`TryGetSourceId`) and is a different kind of
thing:

| | `UnityTextureEvidence` | `UnityAlphaFieldEvidence` |
| --- | --- | --- |
| Shape | static, stateless | instance, holds a resolved id→texture map |
| Input | a `Texture` | a `TextureSourceId` |
| Output | a small scalar/enum fact | a `Width x Height` immutable field |
| Consumers | two shader frontends | one resolver delegate |
| Cost | trivial | proportional to texture area |

Adding this to `UnityTextureEvidence` would break every row. The hypothesis in the
milestone brief — that the responsibility "may deserve a separate internal producer" — is
**confirmed**.

## Relationship to `TriangleAlphaClassifier`

No change, and none is needed. The classifier's input contract proved **sufficient**: the
producer can construct `AlphaTextureData` directly with zero adaptation, zero
transposition, and zero new type. No stop condition fired here.

One property is worth recording because it shapes the tests: `AlphaTextureData`
short-circuits on `IsFullyOpaque` / `IsFullyNonOpaque` *before* any geometry is examined.
A uniformly-opaque texture therefore returns `ProvenOpaque` for **any** triangle, which
means a uniform texture cannot detect a row-order or axis error. Integration tests must
use an asymmetric field.

## Relationship to `AlphaSemanticsResolver`

No change. The `AlphaFieldProvider` delegate is the correct seam and its documented
contract is exactly what the Unity producer can prove. The resolver keeps: no asset access,
no arithmetic on evidence contents, no shader knowledge.

The producer likewise knows nothing about Poiyomi, lilToon, shader property names, coverage
gates, or material-global semantics — it receives an opaque id and a channel.

## Caching decision

**No cache in v1.** Justification:

- There is no consumer that iterates. The milestone ships evidence, not a pass.
- The correct key is not yet knowable. `TextureSourceId` alone is wrong: it does not change
  when the importer, the active build target, or a platform override changes, all of which
  change the effective field.
- A per-instance map already exists and is naturally scoped to one analysis, so the obvious
  future home is memoizing inside the existing dictionary — not a static.

**Never a global static cache**, because its invalidation semantics against re-import,
`SaveAndReimport`, and build-target switching are unproven.

Cost note for later: `GetPixels32` on a 2048-square texture allocates roughly 16 MB of
`Color32[]` plus a 4 MB byte array. `Texture2D.GetPixelData<byte>` avoids the managed copy
but requires per-format byte-layout knowledge. Recorded as the optimization path; **not**
implemented, since no measured pressure exists.

## Malformed versus unsupported

Follows the established convention exactly:

| Class | Behaviour | Cases |
| --- | --- | --- |
| **Programming error → throw** | `ArgumentNullException` | `textures` collection is `null` |
| **Uninitialized value → throw** | `ArgumentException` | `source.Value` is null/empty (a `default(TextureSourceId)`) |
| **Undefined enum → throw** | `ArgumentOutOfRangeException` | `channel` is not a defined `TextureChannel` |
| **Unsupported / unprovable → refuse** | `return false`, `field = null` | everything in the refusal matrix |
| **Skipped at construction** | no throw | a `null` or destroyed element inside `textures`, or one whose id cannot be resolved |

Rationale for the split: a caller handing a `null` collection or an undefined enum has a
bug that silence would hide; a caller handing a mixed array of real textures, some of which
happen to be unassigned slots, is doing something normal. `Material.GetTexture` returns
`null` for an unassigned slot, so tolerating `null` elements is the ordinary case, not an
error.

`AlphaTextureData`'s own constructor validation is not duplicated — conditions 7 and 8
refuse before it can throw.

## Test strategy

EditMode, in the **public** development project, following the existing
`UnityTextureEvidenceTests` pattern: a temp folder under `Assets/`, `[SetUp]` create,
`[TearDown]` `DeleteAsset`, PNGs written with `EncodeToPNG` and imported with
`ForceSynchronousImport`.

Every test maps to a proof obligation or a refusal boundary; none exists to raise a count.

### Positive evidence

| Test | Obligation |
| --- | --- |
| Uncompressed readable RGBA32 returns exact per-texel bytes | Group A exactness |
| Dimensions equal `texture.width`/`height`, including after `maxTextureSize` resize | Effective-alpha definition |
| **Row order: a single non-opaque texel at a known corner lands at the matching `GetAlpha(x, y)`** | The highest-risk defect; guards against a row flip or axis swap |
| NPOT `None` (3x3) preserves odd dimensions | No power-of-two assumption leaked in |
| `RGB24` yields an all-255 field | Group B |
| `Alpha8` and `ARGB32` yield exact bytes | Group A, measured members |
| `alphaIsTransparency = true` and `sRGBTexture = false` leave the field unchanged | Class 2 — genuinely predicate-invariant |
| **`alphaSource` — the producer follows the resulting imported field** (see below) | Class 1 — the setting *does* change the field |
| Two calls return equal contents | Determinism |
| Mutating the returned data cannot affect a later call | Immutability / defensive copy |

#### The `alphaSource` test contract

`alphaSource` is a **Class 1** setting: it changes the imported alpha field. Asserting
invariance under it would be asserting something false. The correct obligation is that the
producer *follows* the import result without inspecting the setting:

| `alphaSource` | Source used | Expected field |
| --- | --- | --- |
| `FromInput` | RGBA source with known alpha | the input-alpha result — bytes equal the source alpha |
| `FromGrayScale` | same source | the generated-alpha result — bytes equal the luminance-derived alpha, **not** the source alpha (measured: alpha became `20`) |
| `None` | same source | the no-alpha result — an all-255 field |

Each case asserts the *field that was actually imported*, and each is a distinct expected
value, so a producer that secretly branched on `alphaSource` and a producer that reads the
imported texture are distinguished by the `FromGrayScale` row. The producer must contain
no reference to `alphaSource`, and the test for that is structural: the production file
contains no `TextureImporter` use at all.

### Refusal boundaries

| Test | Obligation |
| --- | --- |
| Default (non-readable) import refuses | The single most common real-world state |
| `DXT5` readable refuses | **Regression for a measured false opaque** |
| `BC7` readable refuses | Measured false opaque |
| `DXT5Crunched` readable refuses | Measured false opaque |
| `RGBAHalf` readable refuses | Measured `GetPixels32` rounding false opaque |
| `ARGB4444` refuses | Unverified expansion |
| Mipmapped texture refuses | One-grid model |
| `RenderTexture` refuses | Not a `Texture2D` |
| A destroyed `Texture2D` refuses instead of throwing | Fail-closed read; a deterministic fixture (destroy the texture after constructing the producer) |
| Unknown / unsupplied source id refuses | No fabricated identity |
| `TextureChannel.Red/Green/Blue` refuses | v1 scope |
| `default(TextureSourceId)` throws; undefined channel throws; `null` collection throws | Malformed contract |
| `null` element inside the collection is skipped, not thrown | Unassigned-slot tolerance |
| `Editor/Analysis` has **no dependency on the `UnityEditor` namespace** | The placement boundary |

**Conditional, not mandatory.** Zero dimensions and a `GetPixels32` length mismatch are
tested **only if** Unity 2022.3 can produce them through a deterministic real fixture under
the approved architecture. No test seam, mock `Texture2D`, reflection, or deliberate
corruption is introduced to manufacture them. If no natural fixture exists, the guards are
verified by code review at Task 10 and that absence is recorded.

### Integration-level classifier test

The end-of-milestone test, deliberately **without** `Material`, shader adapters, or
`MeshSeparationPlanner`:

```
imported Texture2D (4x4, readable, RGBA32; texel (0,0) alpha = 128, all others 255)
    -> UnityTextureEvidence.TryGetSourceId        (existing)
    -> UnityTextureEvidence.TryGetSampling        (existing)
    -> TextureSample(sourceId, UvMapping(0, one, zero), sampling)
    -> ScalarSemanticValue.Texture(sample, TextureChannel.Alpha)
    -> AlphaSemanticsResolver.Resolve(..., evidence.TryGetAlphaField)
    -> AlphaResolution.Classify(triangle)
```

Three cases:

1. **Triangle whose UV hull lies wholly inside the opaque region** → `ProvenOpaque`.
2. **Triangle whose UV hull covers the non-opaque texel** → `MustRemainTransparent`
   (the classifier must **not** prove opaque).
3. **A fully opaque texture** → `ProvenOpaque` for any triangle.

Case 1 versus case 2 is what proves there is no impedance mismatch: it exercises
dimensions, bottom-to-top row order, x/y orientation, and byte semantics simultaneously.
A symmetric or uniform texture would pass all three by accident.

## Non-goals

No coverage semantics, dissolve modelling, cutout semantics, discard graph, or
alpha-to-coverage. No `sample.rgb x sample.a` work. No `IShaderAdapter`, registry,
factory, `TextureEvidence<T>`, generalized texture framework, shader schema, expression DAG,
feature graph, or HLSL parser. No third shader adapter. No atlasing, material combining,
animation or state tracing, optimization-planner change, NDMF pass, avatar component,
inspector UI, Play Mode work, or CI change. No asset mutation of any kind: the producer
reads and never writes, and never toggles `isReadable`. No refactor of unrelated helpers.
No modification to either shader frontend.

## Stop conditions

None fired during design research. Explicitly evaluated:

| Condition | Status |
| --- | --- |
| Classifier must gain Unity dependencies | **No** — delegate seam; `Analysis` stays `UnityEditor`-free |
| `MaterialSemantics` requires modification | **No** |
| `AlphaSemanticsResolver` must become shader-specific | **No** |
| Exact effective alpha unobtainable for any useful domain | **No** — Group A + Group B obtained |
| A common case requires approximation | **No** — common cases are *refused*, not approximated |
| Classifier's alpha representation not expressive enough | **No** — it is a per-texel opacity predicate and that is exactly what is provable |
| Platform behaviour makes "exact" ambiguous | **No** — resolved by reading the imported texture for the active target |
| **Swizzling affects shader `.a` without being reflected by `GetPixels32().a`** | **No — measured, 8/8 agree (Experiment 6).** Swizzling is baked at import, so the no-`TextureImporter` producer is sound. |
| GPU rendering/readback required | **No** — `GetPixels32` is CPU-side. A shader sample was used **once, in a scratch experiment**, to *validate* the allow-list; no rendering harness exists in production or in the test suite. |
| Generalized texture framework becoming necessary | **No** |
| Expansion into mesh/material mutation, NDMF, UI, atlasing, CI | **No** |

One condition warrants a note rather than a stop: the supported domain **requires
`isReadable`, which is off by default**, so v1 will refuse most real avatar textures. This
is a coverage limitation, not a correctness or architecture problem, and it is the honest
consequence of not approximating. See [Deferred work](#deferred-work).

## Risks

| Risk | Severity | Mitigation |
| --- | --- | --- |
| Row-order or axis error silently inverts the field | **High** — would produce false `ProvenOpaque` | Asymmetric-corner test in both the unit and integration suites; measured convention documented |
| A format's CPU view diverges from shader `.a` | **Resolved** | Experiment 5 measured all four admitted formats against a real shader sample; `BGRA32` dropped because it could not be proven |
| `TextureImporter` swizzling bypasses the CPU view | **Resolved** | Experiment 6: swizzling is baked at import; 8/8 agree, including `A→R` and `A→OneMinusA` |
| An unexpected `GetPixels32` failure is swallowed | Low | Only the two measured exception types are caught; no `catch (Exception)` |
| Memory on very large readable textures | Low | Bounded by `isReadable`, which is rare and usually small; no cap added speculatively |
| A future caller passes a texture whose importer changed mid-analysis | Low | No cache means every call re-reads current state |
| `Editor/Host/` grows into a speculative host layer | Medium | One file; the source-text boundary test and this document constrain it |
| Gamma color space in the public project | Low | Irrelevant to alpha (measured); affects frontend tests only, which already handle it |

## Deferred work

| Item | Blocked on |
| --- | --- |
| **Non-readable textures** — the dominant real case | A route that does not need `isReadable`: decode the source asset and *prove* the import chain preserves the predicate. Requires proving no resize, no lossy format, no `alphaSource` change. Worth its own milestone. |
| Compressed formats (DXT5/BC7/ASTC) | Would require proving a decoder equals the GPU's. Measured to be a false-opaque source; likely permanently refused for exact proof. |
| 4-bit alpha (`ARGB4444`) | Verify Unity's x17 expansion is guaranteed, then the predicate is exact |
| `BGRA32` | A route that produces one, so predicate equivalence can be measured. `TextureImporterFormat` cannot request it in 2022.3. |
| 16-bit / float alpha | Requires `GetPixels()`/`GetPixelData` and an exact `== 1f` test, never `GetPixels32` |
| R/G/B channels | Needs the sRGB transfer-function argument (`sRGB(1) == 1`, monotonic) written down |
| Caching | A consumer that iterates, plus a key covering importer + build target |
| `GetPixelData<byte>` fast path | Measured allocation pressure |

## Implications for `feat/end-to-end-alpha-analysis`

1. **The chain is now closed.** After this milestone every link exists:
   `Material → MaterialSemantics.Alpha → AlphaSemanticsResolver → AlphaTextureData →
   TriangleAlphaClassifier → TriangleAlphaOutcome`. The next milestone connects
   `Renderer → Mesh → triangles` and joins it to `MeshSeparationPlanner`; it should not
   need to invent any new seam.
2. **The next milestone will immediately feel the `isReadable` wall.** A real avatar will
   produce `MissingTextureEvidence` almost everywhere. That is the correct fail-closed
   behaviour and it is the evidence that should drive prioritizing the non-readable route.
3. **Diagnostics become the visible product surface.** `AlphaResolutionFailure` currently
   has one refusal for every texture-evidence problem. Distinguishing "non-readable" from
   "compressed" from "mipmapped" is what makes the refusal actionable — but that is a
   change to `AlphaSemanticsResolver`'s enum and belongs to the milestone that has a
   consumer for it, not to this one.
4. **`Editor/Host/` is now the named home** for anything that must touch both Unity objects
   and Analysis types. Mesh extraction (`Renderer`/`Mesh` → `TriangleAlphaInput`) is the
   next such thing and belongs there for the same reason.
5. **Coverage semantics remain the strongest known gap** in the semantic core, unchanged by
   this milestone. It stays a two-producer / zero-consumer pressure.
