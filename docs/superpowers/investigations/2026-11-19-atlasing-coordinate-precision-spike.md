# Atlas coordinate precision and anisotropic support spike

Privacy note: This record contains public synthetic fixture results only. It contains no private asset observations, machine identifiers, or vendor source excerpts.

Date: 2026-11-19.
Status on 2026-11-19: The authorized disposable spike completed. It gives a candidate rounding bound for sampling without anisotropic filtering. It found that anisotropic filtering can break that bound. It also gives a candidate anisotropic support rule. G0.2 remains open. No product code changed.

## 1. Purpose and scope

On 2026-11-18 the user accepted a separately bounded UV-rounding error. The bound must come from measured coordinate precision on a bounded route. This spike measured that precision and tested a bound against real sampling.

The dev editor used Unity 2022.3.22f1 and Direct3D11 on one NVIDIA GPU. Each execution checked the exact repository asset directory before it created objects. No operation targeted the private editor. The spike compiled diagnostic shaders and C# in memory. It created only transient shaders, materials, meshes, textures, and render targets. No asset, scene, project setting, or product source changed. Each batch reported zero surviving owned objects and restoration of the previous render target.

No vendor shader ran in this spike. The UV route was a direct sample of UV0 with an optional scale and offset applied in the vertex shader. A real vendor route still needs its own check.

## 2. Specification facts

The Direct3D 11.3 functional specification, section 7.18.16.1, requires at least 8 fractional bits of subtexel precision and at least 8 fractional bits of LOD precision. Section 7.18.11 gives the LOD and the ratio of anisotropy. It does not define where anisotropic taps go. Section 15.16.2 says that attribute interpolation follows the floating-point rules. It does not give a fixed error in ULPs.

Bilinear interpolation along one axis changes by at most D times the position change. D is the largest difference between adjacent texels in that channel and level. Quantization with step q changes a position difference by less than q. For two routes whose continuous positions differ by delta texels, this gives a candidate bound for each channel:

```text
E <= (delta_u + q) * D_u + (delta_v + q) * D_v + (delta_lod + q) * D_lod + one output step
```

Here q is 2^-8. D_lod is the largest range between neighboring texels of two adjacent levels. The bound holds only if both routes use the same filter taps. Anisotropic filtering does not guarantee that.

## 3. Batch A: hardware quantization

A 2-texel half-float texture with values zero and one was sampled at 4096 positions across one texel. The result had 257 distinct values with a step of 2^-8. The largest deviation from the exact weight was 0.00183, about half a step. This is round-to-nearest.

A 2-level texture with level values zero and one was sampled at 4096 explicit LOD values between zero and one. The result had 256 distinct values with a step of 2^-8. The largest deviation was 0.00378, about one step. This is truncation.

This GPU therefore uses exactly the specification minimum for both quantities.

## 4. Batch B: coordinate error on rasterized meshes

A grid of 1152 triangles covered a 512-pixel square target. Each vertex had a different clip-space w, so perspective-correct interpolation ran. The source route and the page route used the same positions, so each fragment had the same barycentrics in both routes. The page UV was computed in double precision from the stored source UV, then stored as a mesh attribute. Each fragment wrote its interpolated UV to a 32-bit float target. The CPU compared the two texel positions in double precision.

The sweep used source sizes 512 and 2048 and page sizes up to 16384. It used two translations per page, integer source tiling offsets 0, 3, and 64, and material offsets 0 and 0.37. This gave 96 configurations for each storage format, with 262144 fragments each.

### Float32 UV storage

The largest error fits this model in every configuration:

```text
delta <= c * 2^-24 * (P * max(1, |u_page|) + S * max(1, |u_source|))
```

The largest observed coefficient c was 7.92. The smallest was 1.8. P and S are the page and source sizes in texels.

| Case | Largest observed delta in texels |
| --- | --- |
| Tiling offset 0, page 4096 | 0.00194 |
| Tiling offset 3, page 4096 | 0.00317 |
| Tiling offset 0, page 16384 | 0.00714 |
| Tiling offset 64, source 2048 | 0.0648 |

Delta is not always below 2^-9 texels, even at page size 4096. The bound must therefore keep the delta term. A precondition of delta below one quantization step is not enough.

A large source tiling magnitude dominates the error. The cause is the precision of the source route itself, not the page.

### Float16 UV storage

Half-precision page UVs gave errors of 0.25 texels at page 1024, 1.0 texel at page 4096, and up to 4.06 texels at page 16384. Remapped UV channels must therefore be written as Float32.

Unity 2022.3 applies vertex compression only to meshes that are not skinned and not readable. The dev project has the default compression mask, and that mask includes UV0. The output mesh format and its readability are therefore route facts that must be checked.

Finding: `Mesh.SetUVs` changed a Float16 UV0 attribute back to Float32 without a warning. A later `SetVertexBufferParams` call converted the data to half precision again. The first Float16 sweep was therefore really Float32. It was discarded and run again with this correction.

## 5. Batch C: sampled error against the candidate bound

The source was a 256-pixel high-contrast fixture with mip levels from Unity. The page held 3 by 3 copies of the source and its mip levels, so no foreign texel could enter any footprint. The page sizes were 1024 and 4096. Each configuration drew a 32-pixel quad with a chosen screen-to-texel Jacobian. The CPU compared each channel with the candidate bound, using the measured delta for each pixel. Delta_lod was set to 2^-10. This value is an assumption, and no measurement supports it.

| Run | Configuration-page pairs | Pixels | Pixels over the bound | Largest error to bound ratio |
| --- | --- | --- | --- | --- |
| Fixed set, sampler anisotropy 1, 4, and 16 | 54 | 55296 | 423 | 3.048 |
| Same two failing settings, anisotropy level 0 | 4 | 4096 | 0 | 0.257 |
| Random set, high contrast, anisotropy 0 and 16 | 480 | 491520 | 0 | 0.603 at level 0, 0.812 at level 16 |
| Random set, smooth fixture, anisotropy 0 and 16 | 480 | 491520 | 0 | 0.448 |
| Integer and half-integer anisotropy ratios | 264 | 270336 | 25 | 1.194 |

In the fixed set, two configurations exceeded the bound: isotropic 2.7 texels per pixel at 17 degrees, and an 8-to-1 footprint at 30 degrees. Their largest error was 0.0469 in a channel with a difference of one between adjacent texels. Sampler anisotropy levels 1, 4, and 16 gave nearly the same results. The project forces anisotropic filtering, so this is expected.

A diagnostic isolated the cause. With anisotropy level 0, the same two configurations stayed within the bound under trilinear and bilinear filtering. With anisotropy level 16, the hardware LOD differed between the routes by up to two 2^-8 steps. The derivative lengths differed in the sixth significant digit, for example 2.69999 against 2.70001 texels per pixel. Screen-space derivatives subtract nearby UV values, so this small difference cannot be avoided. Anisotropic tap selection depends on those derivatives, and the specification does not define it.

The integer-ratio hypothesis was not confirmed. Only one of 264 configuration-page pairs exceeded the bound. It had a ratio of about 11.

In the worst fixed case, the error was about 2^-6 times the sum of the D values. This is a measurement from one GPU. It is not a bound.

A quarter-texel translation acted as a sensitivity control. It produced errors up to 0.25. The candidate bound grew with delta and covered the magnified case. The bound is therefore a Lipschitz limit, not an equivalence test. A route policy must also limit delta itself.

## 6. Batch D: anisotropic support radius

The page held only the used source region plus a border b, with the border applied at each mip level. Everything outside was poison. Each page was rendered twice with two different poison colors. Any output difference proved that a poison texel had nonzero weight. This detection is exact.

The sweep used anisotropy levels 0 and 16. The major axis was 1, 2, 4, 8, 16, or 24 texels per pixel. The axis ratio was 1, 4, 16, or 32. The border b took powers of two from 0 to 128. In all 48 configurations, the first clean border was also clean at every larger tested border.

Every configuration was clean at a border no larger than the major-axis length plus one texel of the coarser trilinear level, measured in base pixels. The largest ratio of the needed border to the major-axis length was 1.0. A half-major-axis model failed in 7 configurations. The power-of-two border grid cannot separate the two models, so the half-axis rule remains unproven.

## 7. Findings

1. Without anisotropic filtering, the candidate bound held in every measured pixel, with a largest ratio of 0.603. The bound uses the 2^-8 subtexel and LOD steps from the specification, the measured delta, and local texel differences.
2. Delta follows a Float32 model with a measured coefficient below 8. The coefficient comes from one GPU. A route must use Float32 UV storage for remapped channels.
3. With anisotropic filtering, small derivative differences changed the LOD and the tap selection on rare pixels. Errors reached about three times the candidate bound. The specification gives no bound for this case.
4. A candidate anisotropic support rule held in all measured cases: a border of the major-axis length plus one texel of the coarser trilinear level. It is a measurement from one GPU.

## 8. Decision and limits

Status on 2026-11-19: The anisotropic case needed a user decision. A strict reading of the 2026-11-18 rule would reject almost all atlas routes, because the project forces anisotropic filtering and client settings can force it too. On 2026-11-19 the user chose to treat anisotropic tap-selection differences like the accepted compression risk. Recipe fixtures and per-output quality checks qualify them. No separate anisotropic allowance exists. Contamination from neighboring regions stays forbidden. Section 15.2 of the controlling design records the decision.

This record does not approve a numerical limit, a border constant, or a coefficient. It does not prove behavior on other GPU vendors, in other graphics APIs, or in vendor shader routes. No Unity Test Runner suite, vendor render, compression, or avatar build ran.

The Console query on 2026-11-19 returned the same five entries that the 2026-11-17 record reported: two async readback errors for a compressed format, and three transport-timeout warnings. This session used no compressed textures and no async readback. The Console was not cleared.

The controlling design is `docs/superpowers/specs/2026-11-16-merge-ready-atlasing-design.md`. Earlier sampling evidence is in `docs/superpowers/investigations/2026-11-17-atlasing-sampling-followup-spike.md`.
