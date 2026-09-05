# Runtime Texture Evidence — Investigation

**Status: characterization only. This investigation wrote and changed no production AMUSE code.**

Branch: `investigate/runtime-texture-evidence`
Base commit: `fa63573` (`origin/main`, PR #24 merged)
Date: 2026-08-27 — revised after controller review

This note answers one question: what is the smallest sound way for AMUSE to get
immutable, predicate-equivalent texture evidence from ordinary imported Unity `Texture2D`
assets that are **non-readable, compressed, and mipmapped**, without changing source
importer settings.

Labels mark each claim throughout:

- **[M]** measured in the public AMUSE Unity project on this host.
- **[S]** sourced from pinned upstream source or an authoritative format specification.
- **[U]** unresolved — neither measured nor settled by an authority.

A first revision of this note over-claimed in six places. The corrections are not cosmetic.
Two of them (§4 on float formats, §6 on mip residency) changed the recommended
production boundary. §14 lists what changed and why.

## 1. The proof predicate under test

`AlphaFieldProvider` in
[`AlphaSemanticsResolver.cs`](../../../Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs)
states the contract, and it is **wider than an exactly-one test**. The provider returns
false unless it can prove

> that every effective per-texel scalar value is **finite and within `[0, 1]`**, that byte
> 255 marks exactly the texels whose value is exactly 1, and that every other byte marks a
> value strictly below 1.

Both halves bind. A GPU predicate of the form `alpha == 1` establishes only the second.
§4 shows why that gap decides the initial format policy.

"Effective decoded texel" means the value a shader receives from the **imported** texture,
not the value in the source file.

## 2. Environment

| Fact | Value |
| --- | --- |
| Unity version | `2022.3.22f1` |
| `Application.dataPath` | `<repo-root>/Assets` — exact normalized identity match to this repository, confirmed before every Unity operation |
| Graphics API / device type | `Metal`, `Apple M2`, vendor `Apple`, shader level `50` |
| Active build target | `StandaloneWindows64` (host is macOS) |
| `supportsAsyncGPUReadback` | `True` |
| `QualitySettings.globalTextureMipmapLimit` / `masterTextureLimit` | `0` |
| `QualitySettings.streamingMipmapsActive` | `False` |

**This is one GPU, one API, one build target.** Every measurement below carries that
scope, and §11 keeps it in view rather than generalizing from it.

## 3. Candidate 1 — direct `AsyncGPUReadback` of a `Texture2D` mip

### 3.1 `isReadable` is not a barrier **[M]**

| `format` | `isReadable` | readback |
| --- | --- | --- |
| `RGBA32` | `False` | **succeeds**, exact |
| `RGBA32` | `True` | succeeds, identical result |
| `DXT5` | `False` | fails |
| `DXT5` | `True` | fails |

The `2026-08-19` design named non-readable textures "the dominant real case" and deferred
them. The deferral assumed that decoding the source asset would be required. **The design
needs no such route.** `isReadable` governs the CPU copy. GPU readback reads the GPU resource.

### 3.2 Compressed formats refuse, loudly **[M]**

`AsyncGPUReadback` returns `hasError == true` for every compressed source at every
destination format tried (`(native)`, `R8G8B8A8_UNorm`, `R8G8B8A8_SRGB`,
`R32G32B32A32_SFloat`, `R16G16B16A16_SFloat`, `R32_SFloat`, `R8_UNorm`). Capability query
agrees: `IsFormatSupported(RGBA_DXT5_UNorm, ReadPixels)` and the `BC7` equivalent are both
`False` on this device.

**The refusal is not silent.** Unity logs a hard error per attempt:

```
'RGBA_DXT5_UNorm' doesn't support ReadPixels usage on this platform. Async GPU readback failed.
AsyncGPUReadback - The source format RGBA Compressed DXT5|BC3 UNorm (101)
                   is a compressed format which is not supported by async read back
```

This surfaced as a test failure before anyone understood it. It is a production
constraint, not a curiosity. **A production route must not probe direct readback and catch
its failure.** Otherwise it fills the Console with errors for every compressed texture on an
avatar. The route must come from the format, ahead of any attempt.

### 3.3 Mip addressing and end-of-chain **[M]**

8x8 mipmapped, `mipmapCount == 4`: mips 0–3 return `8x8, 4x4, 2x2, 1x1`. Mip 4 returns
`hasError == true` rather than fabricated data, and a 64x16 chain returns
`64x16, 32x8, 16x4, 8x2, 4x1, 2x1, 1x1`. Each axis halves independently and clamps at one,
and every reported size matched `max(1, size >> mip)` at all seven levels.

### 3.4 Coordinate agreement **[M]**

Index `0` is bottom-left, rows run bottom-to-top, row-major — the layout `AlphaTextureData`
declares (`_alpha8[y * Width + x]`). Verified against an asymmetric four-quadrant fixture
and, separately, a non-square 16x4 fixture with a zero column and one isolated texel. No
flip, no transpose.

### 3.5 Determinism **[M]**

Five repeated captures of mip 0 and mip 1 were byte-identical.

## 4. Floating-point formats stay refused

The first revision of this note recommended admitting `RGBAHalf` because a 32-bit readback
preserves `0.999023438` and so `== 1f` is a real test. **That reasoning was incomplete, and
the first revision withdrew the recommendation.**

Measured, on an in-memory `RGBAHalf` field **[M]**:

| stored | readback | `a == 1` bit | finite and in `[0,1]`? |
| --- | --- | --- | --- |
| `1` | `1` | **1** | OK |
| `0.999` | `0.999023438` | 0 | OK |
| `2` | `2` | 0 | **VIOLATES** |
| `-1` | `-1` | 0 | **VIOLATES** |
| `NaN` | `NaN` | 0 | **VIOLATES** |
| `+Inf` | `+Inf` | 0 | **VIOLATES** |
| `0.5` | `0.5` | 0 | OK |
| `0` | `0` | 0 | OK |

The exactly-one bit is *correct* for `0.999`. But it reports the **same `0`** for `2.0`,
`-1.0`, `NaN` and `+Inf` as for an ordinary below-one texel. **One bit cannot distinguish a
legitimate below-one value from one that violates the finite-and-within-`[0,1]` attestation
this contract makes.**

The resolution does **not** require a second validity channel, and this note proposes none:

- A **UNorm** format supplies the attestation *structurally*. Decode is `n / max` over an
  unsigned integer, so the result is always finite and always within `[0, 1]`. The format
  is the proof. The format needs no per-texel evidence **[S]**.
- A **float** format supplies no such guarantee, and recovering it would mean carrying a
  second per-texel fact that no current consumer asks for.

Nothing in the current code shows a need for a validity channel. The correct first
scope is therefore the narrower one: **the format allowlist carries the attestation, and
refuses float formats.** Should a consumer later require float coverage, that is a separate
milestone with its own design.

## 5. Candidate 2 — bounded GPU predicate extraction

A single fragment shader loading **one explicit mip** by integer texel index and emitting
the binary result of `alpha == 1`:

```hlsl
Texture2D<float4> _MainTex;
int _Mip;
float4 frag(v2f i) : SV_Target {
    int3 coordinate = int3((int)i.pos.x, (int)i.pos.y, _Mip);
    float alpha = _MainTex.Load(coordinate).a;
    return float4(alpha == 1.0 ? 1.0 : 0.0, alpha, 0.0, 1.0);
}
```

The **production-shaped path** renders that into a `GraphicsFormat.R8_UNorm` target sized
to the mip and reads it back as bytes — one byte per texel, `0` or `255`, and the predicate
is `byte == 255`. The green channel exists only for one diagnostic (§4), and an R8 target
discards it.

Required, and all verified on this host **[M]**:

| Requirement | Observed |
| --- | --- |
| `IsFormatSupported(R8_UNorm, Render)` | `True` |
| `IsFormatSupported(R8_UNorm, ReadPixels)` | `True` |
| allocated target's actual `graphicsFormat` is exactly `R8_UNorm` | `True` — no substitution |
| every returned byte is exactly `0` or `255` | `True` |

An inexact target format is a **refusal**, not something to tolerate, because a substituted format
would silently change what the readback means. A byte between `0` and `255` is likewise a
refusal: it would mean something filtered, rescaled, or transfer-converted the value on
the way out, and the predicate would then no longer be the predicate.

### 5.1 Compressed decode is exact **[M]**

The `2026-08-19` design recorded that `DXT5`, `BC7` and `DXT5Crunched` turn a source alpha
of `254` into `255`, and concluded they were "likely permanently refused for exact proof."
**That is a property of the Unity CPU decoder in `GetPixels32`, not of the formats.**

Measured through the GPU, the same imported assets decode a uniform `254` block to
`254/255`, not `1`:

| Format | a(255) | a(254) | a(0) | a(128) |
| --- | --- | --- | --- | --- |
| `RGBA32` | `1` | `0.996078432` | `0` | `0.5019608` |
| `DXT5` | `1` | `0.996078432` | `0` | `0.5019608` |
| `BC7` | `1` | `0.996078432` | `0` | `0.5019608` |
| `DXT5Crunched` | `1` | `0.996078432` | `0` | `0.5019608` |
| `Alpha8` | `1` | `0.996078432` | `0` | `0.5019608` |
| `ARGB4444` | `1` | **`1`** | `0` | `0.533333361` |

`ARGB4444` reporting `1` for a source `254` is not a defect: 4-bit alpha quantizes
`254/255` to `15/15`, and the shader genuinely samples exactly `1`. The imported field is
opaque there.

Where compression *does* destroy the distinction it does so inside the imported data, and
the shader then really does sample `1`. Measured: the 4x4 mip 1 of the quadrant fixture is
one DXT5 block. The encoder snapped `254` to `255`, and both `Load` and a filtered sample
return exactly `1` — under §1 the correct answer, because it is what renders.

### 5.2 `Load` versus a filtered sample **[M]**, and the sRGB caveat **[S] [U]**

The probe sampled each fixture by `Load(int3(px, mip))` and by
`SampleLevel(sampler, (px + 0.5) / mipSize, mip)`, and compared the exactly-one predicate.
**14 of 14 configurations agreed** — `RGBA32`, `DXT5`, `BC7`, `DXT5Crunched`, `Alpha8`,
`ARGB4444`, `RGBAHalf`, each with `sRGBTexture` on and off. `Alpha8` carries no swizzle
hazard on this platform: `Load(...).r == 0` and `Load(...).a` carries the value.

The first revision explained this by asserting that **`Load` bypasses sRGB decoding**.
**This note withdraws that claim.** Texel-fetch semantics differ across graphics APIs: on
OpenGL ES a texel fetch applies the sRGB transfer function and can apply component
swizzling, so portability fails — the probe measured agreement **on Metal**, for the
**alpha** channel only.

Alpha-only support therefore rests on a narrower and more honest basis. **Alpha is the only
channel this investigation characterized, and the only channel any current consumer
requires.** This basis does not assert that texel fetch is transfer-free.

**RGB channels remain out of scope.** A separate characterization must first cover the
transfer function and component swizzling on every graphics API that AMUSE must support.
This matches the existing refusal of colour channels in
`UnityAlphaFieldEvidence.TryGetAlphaField`, which fails closed for the same reason.

### 5.3 The two routes agree on the predicate, not on magnitudes **[M]**

Where direct readback works, the R8 predicate agrees with it **exactly** at every texel and
every mip. The two do **not** agree bit-exactly on magnitudes. Measured through the
diagnostic float target: `0.996078491` against `0.996078432` direct — a one-ULP
difference, because the UNorm decode is not required to round identically on the two paths.

The predicate does not change, because a UNorm maximum decodes to exactly `1.0`. This
confirms the design rather than a problem. **Rely on the predicate alone, never on a
magnitude** — which is exactly what `AlphaTextureData` stores and what
`TriangleAlphaClassifier` reads, and why the production route reads bytes from an R8
target and never a float magnitude at all.

### 5.4 Cost **[M], illustrative only**

Single-run, single-machine observations, recorded for order of magnitude and **not** as a
performance claim. No repetition, no warm-up control, no statistical treatment. No one
should cite them as a benchmark, and this note deliberately does **not** retain the 2K/4K
workload as a test.

| Fixture | Direct readback | Predicate extraction |
| --- | --- | --- |
| 2K `RGBA32`, 12 mips | 56 ms | 39 ms |
| 2K `DXT5`, 12 mips | unusable (12 errors) | 17 ms |
| 4K `RGBA32`, 13 mips | 191 ms | 56 ms |
| 4K `DXT5`, 13 mips | unusable (13 errors) | 55 ms |

Peak temporary allocation for mip 0 is 16x smaller for the predicate target: 4 MB against
64 MB at 2K, and 16 MB against 256 MB at 4K. The predicate needs one byte per texel
rather than sixteen — an arithmetic property of the output format, not a timing
measurement.

### 5.5 Why candidate 2, stated without appeal to timing

The architectural case does not rest on §5.4. The first revision was wrong to say the
route "strictly dominates" on that basis. It rests on four properties:

1. **Exact predicate preservation.** It reads the decoded texel the shader samples. It
   neither fabricates opacity (as `GetPixels32` does on compressed input) nor loses it.
2. **Compressed-format reach.** It is the only characterized route that answers at all for
   `DXT5` and `BC7`, which dominate real avatars. Direct readback cannot (§3.2).
3. **Bounded output size.** One byte per texel, independent of source format, so the
   temporary cost is predictable from dimensions alone.
4. **One production path.** A single route for every admitted format means one predicate to
   keep sound, not two. Two routes would need an equivalence proof. §5.3 shows the two
   routes are *not* bit-identical, so maintaining both would mean owning that divergence.

Direct readback keeps one role: **a test oracle** for uncompressed formats, where the two
must agree on the predicate (§9).

### 5.6 Failure taxonomy **[M]**

| Provoked condition | Behaviour |
| --- | --- |
| `Load` with `_Mip` past the end of the chain | **Returns `0` silently — no error.** |
| Direct readback with mip past the end | `hasError == true` |
| Destroyed `Texture2D` | `MissingReferenceException` |
| `null` texture | `NullReferenceException` |
| Compressed source, direct readback | `hasError == true` **plus logged Unity errors** (§3.2) |

The silent out-of-range `Load` fails *closed* for the opaque predicate, because alpha `0`
is not exactly one. That is an accident of the default value, not a design property.
**Production must validate `mip < mipmapCount` explicitly.**

## 6. Mip behaviour

### 6.1 Multi-mip capture is required for soundness **[M]**

8x8, alpha `255` for `x < 5` and `200` otherwise — the boundary is deliberately
**odd-aligned** so it does not survive halving:

```
mip0 row y=0:  1  1  1  1  1  0.784  0.784  0.784
mip1 row y=0:  1  1  0.894  0.784
```

Source texel `x = 4` is **exactly 1 at mip 0**. The mip-1 texel covering it is **0.894**. A
triangle whose UV support lies inside source texel `x = 4` would be `ProvenOpaque` from mip
0 alone. A fragment for which the sampler selects mip 1 receives a value below one.

**Mip-0-only proof is unsound for any mipmapped texture.** It reproduces on `DXT5` (mip 0
column `x = 4` is `1`, while mip 1 `x = 2` is `0.91`), so the requirement is format-independent.
§10 durably covers both the uncompressed case and the non-readable `DXT5` case.

The disagreement runs both ways. An 8x8 field of `255` with a single `254` gives mip 0
63/64 exact-ones. It gives mips 1–3 **fully opaque**, because `254.75` rounds to `255`.

This is the concrete justification for capturing the chain, and it means the current
`mipmapCount > 1` refusal in `UnityTextureEvidence.TryGetSampling` is **sound but
over-refusing**.

### 6.2 Mip residency — corrected twice

The first revision claimed AMUSE "can inspect the full chain" and treated the refusals as
safe. The first revision withdrew that claim. The second revision then proposed a
**dimension check** as a positive proof of residency. **That proposal was also wrong.
This note withdraws it too.**

The dimension check reads the **destination render texture whose size this code itself
chose**. It cannot establish that the requested *source* mip was resident. It also cannot
establish that `Texture2D.Load` did not silently substitute a different level or return
default data. A `Load` of a non-resident level could return zeros at full destination size
and pass every dimension check. **No claim that destination or readback dimensions
establish source residency survives in this note.**

What Unity 2022.3.22f1 exposes **[M]**, confirmed present by reflection and observed on a
resident texture:

| Surface | Type | Observed |
| --- | --- | --- |
| `Texture2D.activeMipmapLimit` | `int` | `0` |
| `Texture2D.ignoreMipmapLimit` | `bool` | `False` |
| `Texture2D.mipmapLimitGroup` | `string` | `""` |
| `Texture2D.streamingMipmaps` | `bool` | `False` |
| `Texture2D.loadedMipmapLevel` | `int` | `0` |
| `Texture2D.desiredMipmapLevel` / `requestedMipmapLevel` / `minimumMipmapLevel` | `int` | `-1` |
| `QualitySettings.globalTextureMipmapLimit` | `int` | `0` |
| `QualitySettings.masterTextureLimit` | `int` | `0` (legacy alias) |

#### The minimal safe initial policy

> **Gate on declared state, before capturing anything:**
>
> 1. require `texture.activeMipmapLimit == 0`;
> 2. refuse `texture.streamingMipmaps == true` outright for the initial implementation;
> 3. only then capture **every declared mip**, `0 .. mipmapCount - 1`;
> 4. retain the destination dimension and buffer-length checks as **output-integrity**
>    checks on the render target this code allocated — never as source-residency proof.

`activeMipmapLimit` is the per-texture *effective* limit, so it already folds in the global
limit and any mipmap-limit group. Gating on it being zero is therefore a single check rather
than a survey of the settings that feed it.

**This design refuses streaming and does not handle it.** Designing streaming support now
would mean designing against behaviour this investigation never observed. §11 records it
as **future coverage requiring separate characterization**.

**No production mechanism may mutate importer settings, global quality settings, or
streaming state** to make a level resident. `ignoreMipmapLimit`, `globalTextureMipmapLimit`
and streaming state are project and asset state owned by the user. Writing them to satisfy
an analysis would violate the evidence/mutation boundary in `AGENTS.md`
§NDMF and mutation boundary exactly as flipping `isReadable` would. Code may
**read** them as gates and diagnostics.

(An earlier turn of this investigation set and restored `masterTextureLimit` for
measurement. That caused Unity to rewrite `ProjectSettings/QualitySettings.asset` as a
`serializedVersion: 2 -> 3` migration with values preserved. This investigation restored
it. That episode is itself why this is not an acceptable production mechanism.)

**[U]** This investigation never provoked non-residency. The policy above does not depend
on the unmeasured case. It refuses a texture whose declared state shows any limit or
streaming, before any capture attempt.

### 6.3 Non-square chains **[M]**

16x4, `mipmapCount == 5`, zero column at `x = 0`, one `254` at `(11, 1)`: levels are
`16x4, 8x2, 4x1, 2x1, 1x1`. The zero column stays in **column** 0 (a transpose would move
it to a row). The isolated `254` lands at its exact coordinate.

## 7. Candidate 3 — generic `Graphics.Blit` + `ReadPixels`

Measured on the non-readable `DXT5` quadrant fixture into an `ARGB32` render texture, it
**happens to be correct**. It shows the right orientation and a preserved `254` under both
sRGB and linear. It remains the wrong choice for reasons independent of that fixture:

1. **It is an 8-bit path**, reinstating the rounding hazard that disqualified
   `GetPixels32`.
2. **It cannot address a specific mip.** Selecting one requires a custom shader — at which
   point it is candidate 2 with a worse destination format.
3. **It depends on 1:1 sampling alignment** holding silently.

Agreement on one fixture is not proof, and the alternative costs nothing more.

## 8. Recommendation

**Adopt candidate 2 — bounded GPU predicate extraction — as the single acquisition route**,
on the grounds in §5.5.

### 8.1 Smallest production boundary

Two things do not move. `AlphaFieldProvider` remains the **conceptual lookup seam**. The
resolver still asks one question: "what is the proven alpha evidence for this source and
channel". It still never opens an asset. `Editor/Host/` remains the **Unity boundary**,
the one place allowed to touch both Unity objects and `Analysis` types.

What does move is the **returned value of the provider**. It changes from a single
`AlphaTextureData` grid to the narrow mip-chain type of §9.3. That is a change of type on a
value which **six existing seams currently carry as one grid**. The change therefore
propagates through every one of them. §9.2 enumerates them with file and line, and the
production change must update each. This is not a single-producer edit.

The shader and its caller are the smallest *new* code, but they are not the whole change.
§9.2, rather than this section, is the authority on scope.

**Not proposed:** a universal texture IR, a shader IR, a sampling framework, a generic GPU
extraction system. Also not proposed: a compute path, a validity channel, or a cache.
None has a consumer.

### 8.2 Initial format allowlist

A single Metal/Apple M2 observation does not authorize arbitrary compressed formats. The
initial allowlist admits only formats justified by **both** durable characterization
through the R8 predicate path (§10) **and** authoritative format semantics.

| Format | Durably exercised | Authoritative basis |
| --- | --- | --- |
| `RGBA32` | **[M]** R8 path | UNorm decode, below |
| `ARGB32` | **[M]** R8 path | UNorm decode, below |
| `Alpha8` | **[M]** R8 path | UNorm decode, below |
| `RGB24` | **[M]** R8 path | no alpha channel; sampled alpha is exactly one |
| `DXT5` (BC3) | **[M]** R8 path | BC3 alpha block, below |
| `BC7` | **[M]** R8 path | BC7 bit-accurate decode, below |

**Authorities [S]:**

- **UNorm decode.** Vulkan 1.3 specification, §"Fixed-Point Data Conversions" — an
  unsigned normalized integer of *b* bits converts to floating point as `n / (2^b - 1)`,
  giving exactly `0.0` at `n = 0` and exactly `1.0` at `n = 2^b - 1`.
  <https://registry.khronos.org/vulkan/specs/1.3-extensions/html/chap3.html#fundamentals-fixedconv>
  This is what makes the attestation in §4 structural: the result is always finite and
  always within `[0, 1]`, for every UNorm format, with no per-texel evidence required.
- **BC3 (DXT5) alpha.** Vulkan 1.3 specification, appendix "Compressed Image Formats",
  BC3 — the BC4 unsigned rule decodes the alpha block, an exact integer endpoint and
  weighted-interpolation scheme, and endpoint `alpha_0 = 255` decodes to `255`.
  <https://registry.khronos.org/vulkan/specs/1.3-extensions/html/chap44.html#appendix-compressedtex-bc>
  The Microsoft BC3 reference gives the same integer rule:
  <https://learn.microsoft.com/en-us/windows/win32/direct3d11/bc3-format>
  Note the scope limit: the tolerance historically permitted for BC1–BC3 applies to the
  *colour* endpoints, not to this alpha rule.
- **BC7.** The Microsoft BC7 documentation states that **decompression hardware must be
  bit-accurate**, i.e. it must return results identical to the reference decoder.
  <https://learn.microsoft.com/en-us/windows/win32/direct3d11/bc7-format>
  Decoding details: <https://learn.microsoft.com/en-us/windows/win32/direct3d11/bc7-format-mode-reference>
  This is the strongest cross-vendor guarantee of the four and the reason this note admits
  BC7 on the same footing as an uncompressed format.
- **ASTC — the counter-authority, and why it stays refused.** The Khronos ASTC
  specification defines decoding with permitted implementation variation rather than a
  single bit-exact result, with conformance expressed as an error bound against the reference
  decoder, and `decode_mode` affects the returned precision.
  <https://registry.khronos.org/OpenGL/extensions/KHR/KHR_texture_compression_astc_hdr.txt>
  An exact `== 1` predicate is not obviously safe under a tolerance-based decode, so ASTC
  requires its own characterization before any admission.

**Refused in the initial scope**, each for a stated reason:

| Refused | Reason |
| --- | --- |
| All float formats (`RGBAHalf`, `RGBAFloat`, `BC6H`, `RGB9e5Float`) | §4 — the predicate cannot attest finite-and-`[0,1]`, and the format supplies no structural guarantee |
| `DXT5Crunched` | Measured **[M]** in an earlier turn to behave as `DXT5`, but **not** durably exercised through the R8 path, so it is not admitted. Adding coverage is the cheapest next step; it is common on real avatars. |
| `ARGB4444` | Measured **[M]** exact, but 4-bit alpha makes many source values decode to exactly one; admitting it is a coverage decision deserving its own review |
| All ASTC | Tolerance-based decode, above; and it is the Quest format, which §8.3 excludes anyway |
| ETC/EAC, PVRTC, BC1/BC4/BC5, everything unlisted | Not characterized |

### 8.3 Initial target boundary

**Exactly `StandaloneWindows64`.** This is the one target whose imports this investigation
observed (§2). It is deliberately **not** generalized to "Standalone": `StandaloneOSX`,
`StandaloneLinux64` and the other members of that group have their own default format
tables, and this investigation never characterized them, so admitting them would be an unmeasured
generalization — exactly the kind this note has already had to withdraw twice.

With `activeBuildTarget == StandaloneWindows64`, this design does not load the Android/Quest
import and cannot inspect it at all, so a proof obtained here says nothing about the
Android variant of the same asset, which may be ASTC at a different `maxTextureSize`.
**Android/Quest stays unsupported until a separate investigation covers it.**

### 8.4 Exact refusal conditions

Refuse — return `false`, produce no field — when any holds:

| # | Condition |
| --- | --- |
| 1 | `texture as Texture2D == null` (Unity equality) — destroyed, null, or not 2D |
| 2 | `UnityTextureEvidence.TryGetSourceId` fails |
| 3 | `channel != TextureChannel.Alpha` (§5.2) |
| 4 | `format` is not in the §8.2 allowlist — **checked before any readback attempt**, so no Unity error is logged (§3.2) |
| 5 | `EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64` (§8.3) |
| 6 | `texture.activeMipmapLimit != 0` (§6.2) |
| 7 | `texture.streamingMipmaps` (§6.2) |
| 8 | `width <= 0 \|\| height <= 0`, or `mipmapCount <= 0` |
| 9 | the extraction shader is missing or `!isSupported` |
| 10 | `IsFormatSupported(R8_UNorm, Render)` or `IsFormatSupported(R8_UNorm, ReadPixels)` is false |
| 11 | the allocated target's `graphicsFormat` is not exactly `R8_UNorm` (§5) |
| 12 | any mip in `0 .. mipmapCount-1` fails to capture, reports a destination size other than `max(1, size >> mip)`, or returns a buffer of the wrong length — **output integrity, not residency proof** (§6.2) |
| 13 | any returned byte is neither `0` nor `255` (§5) |
| 14 | `MissingReferenceException` from any Unity-object read |

Conditions 9–14 must stay *scoped* refusals, never a blanket `catch` that converts a
programming error into "unsupported", per `.omp/RULES.md` rule 6.

### 8.5 What stops being refused

`isReadable == false`, `DXT5`, `BC7`, and `mipmapCount > 1`. Removing the
`mipmapCount > 1` refusal in `TryGetSampling` is **conditional on** the multi-mip
conjunction in §9 existing, and until then that refusal is the only thing keeping the
classifier sound (§6.1). The two changes land together or not at all.

## 9. Mip aggregation semantics, the affected seams, and the classifier

### 9.1 Semantics

A mip chain is **alternative sampling support**: the hardware may select any level for a
given fragment, and AMUSE cannot know which. Therefore **any mip level demonstrated
non-opaque is a counterexample to opaque conversion.**

This is deliberately **different from consensus aggregation across admitted runtime
states**. There, AMUSE enumerates the states a material may legitimately occupy and forms a
conclusion over that admitted set. Here there is no set to admit: the levels are not
alternative *configurations* but alternative *evidence about one configuration*, and a
single counterexample refutes the proof.

Proposed outcomes, pinned:

| Condition | Outcome |
| --- | --- |
| **every** mip classifies `ProvenOpaque` | `ProvenOpaque` |
| **any** mip classifies `MustRemainTransparent` | `MustRemainTransparent` |
| otherwise | `Unknown` |

### 9.2 The seams this changes — read from current code

The multi-mip requirement is **not** confined to the producer. Today **every** seam below
carries exactly one `AlphaTextureData`, so changing what the provider returns propagates
through all of them. A future production change must update each:

| # | Seam | Location | Current shape |
| --- | --- | --- | --- |
| 1 | `AlphaFieldProvider` delegate | `Editor/Analysis/AlphaSemanticsResolver.cs:34` | `out AlphaTextureData field` |
| 2 | `AlphaResolution` | `Editor/Analysis/AlphaSemanticsResolver.cs:48,56,101` | `_field`, ctor parameter, `Classified(AlphaTextureData, AlphaSamplingSettings)` |
| 3 | `UnityAlphaFieldEvidence` | `Editor/Host/UnityAlphaFieldEvidence.cs` | `Dictionary<TextureSourceId, AlphaTextureData>`, `TryCapture(out … field)`, `TryGetAlphaField(out … field)` |
| 4 | `CapturedTextureEvidence.AlphaChannel` | `Editor/Host/UnityMaterialEvidenceCapture.cs:263` | property, ctor parameter `:274`, capture call site `:990-1004` |
| 5 | `UnityRendererAlphaAnalysis.GatherAlphaFields` | `Editor/Host/UnityRendererAlphaAnalysis.cs:576-598` | returns `Dictionary<TextureSourceId, AlphaTextureData>`; inline provider lambda at `:503-510` |
| 6 | build / runtime-state handoff | `Editor/Build/AmusePlatformFinishPlugin.cs:468-476` | local `AlphaFields` function, `out AlphaTextureData field` |

`AdmittedMaterialStates` also *mentions* `AlphaTextureData` (`:175`) in the rule that
classified resolutions never merge. In substance, that rule does not change — two chains are
no more cheaply provable equivalent than two grids — but the comment should be re-read when
the type changes.

### 9.3 The representation: a tiny invariant-bearing type, not `IReadOnlyList`

Recommended: **a narrow internal type wrapping an ordered chain of the existing
`AlphaTextureData`**, mip 0 first. Not a texture IR, not a sampling framework, not public.

The invariants that must hold are:

- the chain is **non-empty**.
- element `0` is **mip 0**.
- no element is null.
- dimensions **halve independently per axis with a floor of one**:
  `w[i+1] == max(1, w[i] >> 1)` and likewise for height.

`IReadOnlyList<AlphaTextureData>` can express none of them. It would make every one of the
six seams above a place where the invariants are either re-checked or silently assumed, and
§6.3 shows the halving rule is exactly the kind of thing that is easy to get wrong for a
non-square chain. The list type would also admit an empty chain, which is the single most
dangerous value here: an empty chain would make "every mip is opaque" **vacuously true**
and turn the §9.1 conjunction into an unconditional `ProvenOpaque`.

A constructor-validating type costs roughly thirty lines and matches what the surrounding
code already does — `AlphaTextureData` validates its own width, height and length, and
`AlphaResolution` validates its resolved/failure combination in its constructor. The
consistent choice is the small type.

### 9.4 The classifier

**`TriangleAlphaClassifier` itself needs no change**, and neither does `AlphaTextureData`.
The hypothesis holds: a mip *is* one texel grid — not a different kind of object, only a
different `Width`/`Height` — and the classifier already models one grid correctly. The
conjunction belongs in `AlphaResolution`, which already carries the field and the sampling
settings and already refuses to expose them.

#### Cost — no bound is claimed

The classifier applies its `MaxSupportRegions` budget **independently to each grid**, so:

- **each individual classification retains the existing safety budget**, unchanged and
  applied per mip exactly as it is today.
- **the implementation must measure the cumulative cost of classifying one
  triangle against a whole chain**.
- **this investigation proposes no new budget**, because it established no evidence for
  one.

An earlier revision claimed the conjunction costs roughly `4/3` of the mip-0 cost and
therefore needs no new budget. **This note withdraws that claim.** It was a texel-count ratio, and
it did not measure classifier work. The cost tracks the candidate
region covered by the UV support of one triangle, not grid area. It is particularly
unjustified for non-square chains, where an axis clamps at one while the other keeps
halving (§6.3) and the per-level candidate counts do not follow the `4/3` series at all.
Whether the existing per-grid budget is sufficient in aggregate is an open question for the
implementation milestone.

## 10. Durable characterization added

The **research** package preserves the soundness-critical observations, under the
existing `Tests/Editor/Calibration/` convention established by `CensusVendorProbe` and
`VendorReachabilityTests`. That convention already answers the questions a
hardware-dependent characterization raises: a probe reports capability **as a value**, and
absence is a **reported state, never `Assert.Ignore`** — because a skipped case reports as
a pass, and a silently unreachable characterization is worse than none.

`ResearchSourceApiBanTests` bans importer and mutation APIs in the **production** source of
the research package and explicitly exempts `Tests/`, so the fixtures live in tests.

Files added — two source files and one shader, no framework:

| File | Role |
| --- | --- |
| `Tests/Editor/Calibration/AlphaExactOneProbe.shader` | The single shader. Loads one explicit mip; emits the exactly-one bit. |
| `Tests/Editor/Calibration/AlphaEvidenceProbe.cs` | Support-as-a-value, the mip-residency gate as a pure predicate, R8 capture, and the direct-readback oracle. |
| `Tests/Editor/Calibration/AlphaEvidenceCharacterizationTests.cs` | In-memory fixtures and the twenty cases. |

**The soundness cases run the production-shaped route.** Capture renders into a real
`R8_UNorm` target, verifies the `graphicsFormat` of the allocated target is exactly `R8_UNorm`,
reads the target back **as bytes**, rejects any byte that is neither `0` nor `255`, and
derives the predicate from those bytes. The route reads no magnitude.

One raw-magnitude diagnostic remains, used by exactly one case: the float-attestation test
in §4 needs to show that the texture genuinely stores `2.0`, `-1.0`, `NaN` and `+Inf`, while
the predicate bit cannot distinguish them. The suite documents it as a diagnostic, and no
other case may use it.

**This investigation builds every fixture in memory** with `Texture2D` APIs and `EditorUtility.CompressTexture`
— the process imports nothing, never reads an importer, never writes one, and no scratch asset
can outlive a failed teardown. The suite does **not** retain the 2K/4K timing workload.

**The probe also exercises lower-mip selection on a non-readable `DXT5` chain, not only an
uncompressed one.** The odd-aligned boundary fixture generates its chain uncompressed and
then compresses the whole chain, so the compressed case measures decode of a real mip chain
rather than compression of a single level. Mip 0 column 4 is exactly one through the R8
path while the mip-1 texel covering it is not, on both `RGBA32` and `DXT5`. That is the
combination the feature actually needs, because real avatar textures are non-readable and
block-compressed.

**Every allowlist case and both mip-disagreement cases assert `isReadable == false`**,
including `RGB24`. This pins the central coverage claim: a fixture change cannot silently
turn the investigation back into readable-texture characterization.
The reachability gate also asserts `EditorUserBuildSettings.activeBuildTarget` is exactly
`BuildTarget.StandaloneWindows64` (§8.3), so running the characterization under another
target fails with a clear reason rather than quietly measuring different imports.

This revision **removed** `DirectReadbackIsUnavailableForCompressedFormats` from the durable
suite. The chosen architecture never directly reads a compressed source, so a case that
deliberately provoked Unity errors on every full test run protected no production behaviour
and only added console noise, and §3.2 keeps the measurement it recorded. The
direct-readback **oracle** for supported uncompressed textures remains.

Coverage:

| Requirement | Case |
| --- | --- |
| Alpha-bearing formats: maximum true, submaximum false — through R8 | `AnAlphaBearingFormatSeparatesMaximumFromSubmaximum` for `RGBA32`, `ARGB32`, `Alpha8`, `DXT5`, `BC7` |
| RGB-only format: sampled alpha exactly one | `AnRgbOnlyFormatSamplesAlphaExactlyOne` (`RGB24`) |
| float `0.999…` not exactly one, and why float stays refused | `AFloatFieldDefeatsTheExactlyOnePredicateAsAnAttestation` |
| mip 0 / lower mip disagreement, on **non-readable `RGBA32` and non-readable `DXT5`** | `MipZeroAndMipOneDisagreeAboutOpacity(RGBA32)`, `MipZeroAndMipOneDisagreeAboutOpacity(DXT5)` |
| asymmetric / non-square orientation | `OrientationSurvivesAnAsymmetricNonSquareChain` |
| agreement with direct readback where supported | `TheR8PredicateAgreesWithDirectReadbackWhereSupported` |
| mip-residency gate, every combination | `TheMipResidencyGateAdmitsOnlyAnUnlimitedNonStreamingTexture`, `TheGateOverloadAgreesWithThePredicateForARealTexture` |
| destination size and row layout — output integrity only | `EachCaptureMatchesTheDestinationSizeAndRowLayoutRequested` |
| out-of-range level refused | `ALevelOutsideTheChainIsRefused` |
| reachability gate | `TheProductionShapedPathIsReachableOnThisMachine` |

The suite tests the residency gate as a **pure predicate** over `(activeMipmapLimit,
streamingMipmaps)` at all five relevant combinations, because no in-memory path can
construct the refusal branches: giving a runtime texture a nonzero `activeMipmapLimit` or
streaming state would mutate project or importer state, which production must never do.
A second case asserts the texture overload reads those same two facts, so the pure
predicate cannot drift from the rule it states.

## 10a. Implementation addendum — measured 2026-08-28

Recorded during the production implementation, on the same host and Unity version
as §2. It is an addendum, not a revision: no earlier measurement changed.

### The reported graphics format is not always the sampled format **[M]**

| Query | Result |
| --- | --- |
| `IsFormatSupported(R8G8B8_UNorm, Sample)` | **`False`** |
| `IsFormatSupported(R8G8B8A8_UNorm, Sample)` | `True` |
| `IsFormatSupported(R8_UNorm, Sample)` — `Alpha8` | `True` |
| `IsFormatSupported(RGBA_DXT5_UNorm, Sample)` | `True` |
| `IsFormatSupported(RGBA_BC7_UNorm, Sample)` | `True` |
| `GetCompatibleFormat(R8G8B8_UNorm, Sample)` | `R8G8B8A8_UNorm` |

`R8G8B8_UNorm` is what a `RGB24` import reports as its `graphicsFormat`. So a
preflight gate on exact reported-format `Sample` support **refuses `RGB24`**, which
§8.2 admits — the two clauses cannot both hold on this host.

The refusal is a false negative. Measured through the production shader route,
`RGB24` samples alpha exactly one everywhere: every returned byte was `255` at 4x4
and 8x8, single-mip and mipmapped. Unity 2022.3 converts `RGB24` to `RGBA32` at
texture load because native `RGB24` support is rare, so the reported storage format
is not the format actually sampled.

**Resolution, narrow:** alpha-bearing admitted formats keep the exact
reported-format requirement. `RGB24` alone is exempt. `RGB24` is safe precisely
because it carries no alpha channel, so the substitution cannot lose alpha
information — the sampler returns exactly one either way. This investigation rejected
`GetCompatibleFormat` as a general gate: it promises a supported *similar* format, not the exact
alpha preservation this contract needs from an uncharacterized alpha-bearing
substitution.

The substitution affects only `RGB24`. This investigation measured every other admitted format exactly
sampleable, so the exemption applies to exactly one format.

### Block size decides whether a compressed submaximum survives **[M]**

A 4x4 block-compressed fixture is a *single* compression block, and the encoder snaps a
source alpha of `254` to `255` there, which §5.1 already recorded and the production tests
reproduced. Separating maximum from submaximum therefore requires the submaximum in a
**different** block, which is why the durable characterization and the production tests both
use 8x8 quadrant fixtures.

## 11. Remaining uncertainty and future coverage

The weakest part of the evidence, and it should gate how aggressively the design scopes
the capability.

1. **The analysis GPU is not the playback GPU.** Every measurement is Apple M2 / Metal.
   For **BC7** the guarantee is strong: Microsoft specifies that decompression hardware
   must be bit-accurate **[S]** (§8.2). For **BC3 alpha** the decode rule is an exact
   integer scheme **[S]**, and the tolerance historically allowed for BC1–BC3 applies to
   the colour endpoints rather than that rule — but that is a reading of the specification,
   **not a cross-vendor measurement**. **[U]** for any non-Metal GPU.
2. **§5.3 shows two routes on one GPU already differ by one ULP** on sub-one magnitudes.
   Harmless for the predicate, but direct evidence that decode paths are not bit-uniform,
   which should temper confidence in cross-vendor identity.
3. **ASTC** **[U]** — tolerance-based decode (§8.2), and it is the Quest format.
4. **Only the import of the active build target is observable** (§8.3).
5. **Mipmap streaming** **[U]** — this investigation never provoked non-residency, and this
   design *refuses* streaming rather than handling it (§6.2). **Future coverage requiring
   separate characterization**: what a `Load` of a non-resident level returns, whether
   `AsyncGPUReadback` reports it, and whether any read-only signal distinguishes a resident
   level from an evicted one. Until then this design refuses a streaming texture.
6. **Non-zero `activeMipmapLimit`** **[U]** — this design refuses it, and no in-memory path
   could construct the refusal branch without mutating project state (§10).
7. **Texel-fetch transfer/swizzle semantics vary across graphics APIs** (§5.2) **[S] [U]** —
   measured on Metal only, and the reason RGB stays out of scope.

## 12. Ecosystem comparison — verified against upstream

Re-verified directly against fresh upstream clones. Repository URLs and commit SHAs, not
local paths:

| Project | Repository | Commit |
| --- | --- | --- |
| d4rkAvatarOptimizer | `github.com/d4rkc0d3r/d4rkAvatarOptimizer` | `4b6629f894545e744a5a5f35d6007262b4ac6f44` |
| Avatar Optimizer | `github.com/anatawa12/AvatarOptimizer` | `767c9bdb3e0bd89df03814d0194976a4830227b6` |
| NDMF | `github.com/bdunderscore/ndmf` | `a4d66cbf086f554010aa8019ebe3d55dc6f5822e` |
| Modular Avatar | `github.com/bdunderscore/modular-avatar` | `ed2a14bdb8a55ec6464c9db32715794a210c9cae` |
| VRCFury | `github.com/VRCFury/VRCFury` | `ede27e016901ce58bc22ed9de4cb3592876db3a2` |

**None computes an exact-equals-one predicate and none captures the mip chain**, so there
is no implementation to adopt as proof.

| Project | Route **[S]** | Relevance |
| --- | --- | --- |
| **d4rkAvatarOptimizer** | `Editor/TextureCompressionAnalyzer.cs:74` sets `textureImporter.isReadable = true`; 1x1 `ReadPixels` probes at `:276`, `:308` | **Mutates the source importer** — what `AGENTS.md` §NDMF and mutation boundary forbids. AMUSE cannot follow this. |
| **Avatar Optimizer** | `Internal/Utils/Utils.TextureGraphicsFormat.cs:44` gates on `SystemInfo.GetCompatibleFormat(..., FormatUsage.ReadPixels)` and documents avoiding precision loss; `Editor/Processors/TraceAndOptimize/OptimizeTexture.cs:1061` blits then `ReadPixels`; `Editor/Inspector/RemoveMeshByMaskEditor.cs:92,241` set `importer.isReadable = true` | **Independently validates the precision concern** and uses the same `ReadPixels` capability gate measured in §3.2. Its goal is a transformation-preserving copy for atlasing/resizing, and it re-encodes into a `TextureFormat`, reintroducing quantization. Notes at `:854` that crunched textures return an empty `GetRawTextureData`. Not a predicate. |
| **Modular Avatar** | `Editor/ReactiveObjects/MeshFiltering/VertexFilterByMask.cs:126,141` — on `!isReadable`, blit into an `ARGB32` linear RT, `ReadPixels`, then `GetPixels32` | **The closest structural analogue**, and it is candidate 3. Sound *for its own predicate*, a black/white **threshold** tolerant of 8-bit quantization. Reads **mip 0 only**. Neither property transfers to an exact-`1` proof. |
| **VRCFury** | `Editor-Common/Utils/Texture2DExtensions.cs:62` blit + `ReadPixels` for rescaling; toggles `isReadable` | Same importer mutation as d4rk, for transformation rather than proof. |
| **NDMF** | **No texture read route at all** — zero matches for `AsyncGPUReadback`, `ReadPixels`, `GetPixels32`, `GetRawTextureData` across the upstream tree | Confirms NDMF owns lifecycle, not pixels. Nothing to reconcile. |

Every one of these is a **transformation-oriented texture copy**, free to lose a
least-significant bit. AMUSE is not.

## 13. Provenance

- This investigation took all measurements in the **public** AMUSE Unity project, with
  `dataPath` confirmed as `<repo-root>/Assets` by exact normalized identity match before
  every Unity operation.
- **The Census Lab project was not used, opened, read, or listed.** No path beneath it was
  accessed in this revision. The ecosystem comparison comes entirely from the upstream
  clones in §12.
- Upstream clones live outside the repository, in the session scratchpad, and are not part
  of the deliverable.

## 14. Revision history

### Second revision — corrections to the first

| # | First revision claimed | Corrected to |
| --- | --- | --- |
| 1 | `RGBAHalf` becomes admissible | **Float formats refused** (§4). The predicate cannot attest finite-and-`[0,1]`; the UNorm format supplies it structurally, so no validity channel is needed. |
| 2 | AMUSE "can inspect the full chain"; refusals safe | **Withdrawn** (§6.2). Non-residency was never provoked. |
| 3 | `Load` bypasses sRGB decode | **Withdrawn** (§5.2). Not portable — OpenGL ES texel fetch applies sRGB conversion and swizzling. Alpha-only retained because alpha is the only channel characterized and required. |
| 4 | Ecosystem read from Lab-vendored packages | **Re-verified against upstream** with SHAs (§12); Lab exception removed (§13). |
| 5 | Absolute host path recorded | `<repo-root>/Assets` (§2). |
| 6 | Extraction "strictly dominates" on timing | **Withdrawn** (§5.4, §5.5). |
| 7 | Mip conjunction stated without semantics | **Pinned** (§9.1). |
| 8 | Compressed formats broadly admissible | **Narrowed** to an explicit allowlist (§8.2). |

### Third revision — corrections to the second

| # | Second revision claimed | Corrected to |
| --- | --- | --- |
| 9 | The durable probe was "production-shaped" | It captured into an **RGBA float** target and derived the predicate from a float channel. **The probe now renders into a real `R8_UNorm` target, verifies the allocated format is exactly `R8_UNorm`, reads back bytes, and derives the predicate from those bytes** (§5, §10). Every soundness case runs that path. The raw-magnitude route survives only as a documented diagnostic for the one case in §4 that needs magnitudes. |
| 10 | Destination/readback dimensions **positively establish** source mip residency | **Withdrawn** (§6.2). The dimensions are those of a destination this code allocated; they cannot show the source level was resident or that `Load` did not substitute or return defaults. Replaced by a gate on declared state — `activeMipmapLimit == 0` and `!streamingMipmaps` — with the dimension and length checks demoted to **output integrity**. The test was renamed `EachCaptureMatchesTheDestinationSizeAndRowLayoutRequested` and is no longer listed as a residency test. |
| 11 | The multi-mip change was described as landing in `AlphaResolution` | **Six existing seams** each carry a single `AlphaTextureData` today and all must change; they are enumerated with file and line (§9.2). A tiny invariant-bearing internal type is recommended over `IReadOnlyList`, with the reasoning given — notably that an empty chain would make the conjunction vacuously true (§9.3). |
| 12 | Allowlist admitted formats not all durably exercised | **Every admitted format now runs through the R8 path** — `RGBA32`, `ARGB32`, `Alpha8`, `RGB24`, `DXT5`, `BC7` — distinguishing alpha-bearing from RGB-only behaviour. Authoritative URLs and sections added for UNorm, BC3, BC7 and ASTC (§8.2). `DXT5Crunched` is refused because it is not durably exercised. |
| 13 | Boundary was "Standalone" | **Exactly `StandaloneWindows64`** (§8.3). |
| 14 | `DirectReadbackIsUnavailableForCompressedFormats` in the durable suite | **Removed** (§10). It generated Unity errors on every full run and protected no production behaviour, since the architecture never directly reads a compressed source. |

### Fourth revision — documentation cleanup

No probe, shader, test, allowlist, or production change. Documentation only.

| # | Third revision claimed | Corrected to |
| --- | --- | --- |
| 15 | §8.1: production is "one extended producer plus the extraction shader and its single caller" | **Withdrawn** — it contradicted §9.2. §8.1 now preserves `AlphaFieldProvider` as the conceptual lookup seam and `Editor/Host/` as the Unity boundary, and states that the provider's *returned value* changes from one grid to the narrow mip-chain type and propagates through the six seams §9.2 enumerates. |
| 16 | §9.4: the conjunction "costs roughly `4/3` of the mip-0 cost. It needs no new budget." | **Withdrawn** (§9.4). It was a texel-count ratio, not a measurement of classifier work, and `MaxSupportRegions` is applied independently per grid. Each classification keeps the existing budget; cumulative multi-mip cost must be measured during implementation; no new budget is proposed without evidence. A duplicated clause in the same paragraph was removed. |
