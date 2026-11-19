# Atlas sampling follow-up spike

Privacy note: This record contains public synthetic fixture results only. It contains no private asset observations, machine identifiers, or vendor source excerpts.

Date: 2026-11-17.
Status on 2026-11-17: The authorized disposable sampling follow-up completed. It established additional construction constraints and one narrow strict-mode candidate. It did not close the general sampling or quality gate. No product code changed.

## 1. Scope and method

The dev editor used Unity 2022.3.22f1 and Direct3D11. The spike verified the exact repository asset directory before execution. Each execution also checked that directory before creating objects. No operation targeted the private editor.

The spike compiled C# and a sampling shader in memory. It created only transient textures, shaders, materials, and render targets. It did not create an asset folder, save a scene, or change project settings. Texture filtering was Trilinear. Texture anisotropy was four. The project reported ForceEnable anisotropic filtering.

The source images used linear RGBA32 storage. Unity generated their full mip chains before cropping. Protected page levels copied corresponding source mip neighborhoods. They did not derive from the packed base page. Unprotected levels used a deterministic four-sample box reduction. Unfilled page pixels were magenta with zero alpha.

A floating-point target and CPU readback measured absolute sample differences. Each comparison used a grid of 129 by 129 samples. The ordinary source image was 128 pixels square. Its page was 256 pixels square. Pixel-preserving placement added a source-pixel translation before division by page size. Source and page derivatives represented the same source-pixel footprint.

The high-frequency fixture used a horizontal 13-pixel red ramp, a vertical green ramp, and alternating blue bands. Blue changed with three-pixel horizontal bands and five-pixel vertical bands. Alpha was approximately one quarter on the left and one on the right. A separate smooth fixture used continuous color and alpha gradients.

Rectangle probes covered source pixels 32 through 96 on both axes. Thin-triangle probes used vertices at pixels (32, 32), (96, 95), and (96, 96). The shader generated barycentric sample positions inside that triangle. No triangle was subdivided.

Explicit probes used levels zero, one half, one, one and one half, and two. Gradient probes included pixel vectors (1, 0) and (0, 1), (4, 0) and (0, 1), and (16, 0) and (0, 1). A diagonal case used (2, 2) and (-0.5, 0.5). Strict probes also reached coarser levels and larger footprints. These are controlled probes, not a proof of every vendor sampler or hardware anisotropic footprint.

## 2. Filtering neighborhoods and compression blocks

The first sweep varied the border while keeping the page translation at 64 pixels on both axes. Protected levels were zero through two.

| Border in base pixels | Largest rectangle error before compression | Largest thin-triangle error before compression |
| --- | --- | --- |
| 0 | 0.703120 | 0.683589 |
| 4 | 0.234379 | 0.250004 |
| 8 | 0 | 0.005859 |
| 16 | 0 | 0.005859 |
| 32 | 0 | 0.005859 |

These maxima include the gradient probes. An eight-pixel border was sufficient for the measured uncompressed rectangle cases. It was not sufficient for block-independent BC3 encoding.

A second sweep compressed both the full source and each page to BC3 at quality 100. The source and destination grids had the same block phase. It tested explicit levels zero, one, one and one half, and two, plus the wide gradient probe.

| Border in base pixels | Largest BC3 sample difference | Comparisons with a difference |
| --- | --- | --- |
| 4 | 0.274601 | 4 of 5 |
| 8 | 0.125124 | 2 of 5 |
| 16 | 0 | 0 of 5 |

This is a construction test against an independently compressed source. Zero difference does not establish quality against the uncompressed prepared image.

### A bounded rule for the simple bilinear case

For an exact pixel-preserving translation, let x be a source coordinate in base-pixel units. At level m, bilinear sampling uses the integer texels around x divided by two to the power m, minus one half.

For a closed coordinate interval, calculate the first lower texel index at its minimum. Calculate the last upper texel index at its maximum. Include every texel between them. For trilinear sampling, take the union for both sampled levels. For BC3, extend each copied range to complete four-texel blocks at that level. Preserve the translation phase for both the mip grid and the compression grid.

For the measured interval from 32 through 96, this gives:

| Level | Inclusive bilinear texel indices | Block-aligned base-pixel interval |
| --- | --- | --- |
| 0 | 31 through 96 | 28 through 100, end excluded |
| 1 | 15 through 48 | 24 through 104, end excluded |
| 2 | 7 through 24 | 16 through 112, end excluded |

The union requires the measured 16-pixel border for this compressed case. This rule copies an established full-source pyramid. It does not derive Unity's mip-generation filter from a cropped base image.

This rule is not a general anisotropic bound. It also assumes exact coordinate arithmetic. A supported anisotropic footprint and coordinate-rounding allowance remain necessary. Do not use this fixture's border as a universal constant.

## 3. Coordinate precision is separate from padding

The thin-triangle error did not decrease when the border increased from eight to 32 pixels. A diagnostic removed only the translation. Its explicit-level comparisons became exact. Restoring the 64-pixel translation restored the small differences.

A separate diagnostic rounded probe positions to an exactly representable pixel grid. Its translated explicit-level comparisons also became exact. This diagnostic changed only the probe positions. It is not a proposed shader change or a way to discard the original failure.

These observations isolate translation-sensitive numerical sampling from missing-neighborhood contamination. They support a coordinate-precision explanation. They do not establish a universal hardware rounding bound. The largest observed error was 0.005859 in a high-frequency diagonal probe. A base-level thin probe also changed alpha by 0.002930.

Proposal on 2026-11-17: Define a separate coordinate-rounding allowance within the output quality contract. Keep it distinct from compression error and foreign-region contamination. If a route cannot establish its bound, preserve the dependent source use. Do not increase padding to hide this error. The user acceptance of compression differences does not itself approve this additional allowance.

## 4. Wrap modes, integer moves, and unequal maps

A Repeat-boundary crop sampled pixels 112 through 144 horizontally. Its copied neighborhood wrapped through the full source period at every protected level. Both uncompressed and BC3 comparisons were exact across all eight probes per format.

The unequal-map case used a 128-pixel color image and a 64-pixel map. Their pages were 256 and 128 pixels square. Their translations were 64 and 32 pixels. Both therefore used the same normalized placement. The smaller map copied its complete image to satisfy its block neighborhoods. Both maps had zero difference across all eight probes before and after BC3 compression. Relative map resolution remained unchanged.

Whole-island offsets of positive two and negative two source periods also matched across five explicit-level probes each. Two negative controls rejected a general vertex-wrapping rule. Clamp changed the sampled content after an integer move. A one-island move under a texture scale of one and one half also changed the content. Their largest errors were 0.750973 and one. Integer moves remain conditional on every dependent mapping and wrap mode.

Clamp needed a distinct border construction. Copying wrapped neighbors for a Clamp source produced large errors. Copying repeated edge pixels removed that construction error, but did not make independent compression commute with clamping.

| Clamp construction | Largest uncompressed sample difference | Largest BC3 sample difference |
| --- | --- | --- |
| Repeated source-edge pixels | 0.005875 | 0.143969 |
| Incorrect wrapped neighbors | 0.385290 | 0.371588 |

A decoded-border diagnostic recovered a base-level result through tool history. The translated interior edge had zero error. A newly encoded outside-border pixel differed by 0.160784 from the decoded source edge. A source sampler clamps an already decoded edge value. An atlas encoder instead compresses newly extended input pixels. Those operations need not produce the same value.

Treat this compression difference through the role quality check. Do not classify all Clamp differences as foreign-region contamination. Do not approve Clamp unconditionally from the uncompressed construction.

## 5. A useful but narrow strict-mode candidate

A single used crop can qualify without packing independent images together. The source was 128 pixels square with an exactly repeated 64-pixel pattern. The used region covered pixels 32 through 96. A 64-pixel page copied that region and its corresponding mip content. The coordinate translation was negative 32 pixels. No shader sampling restriction was used.

This was lossless cropping in sampling space, not density reduction. The page kept one stored pixel per source pixel. The source and page repeated the same pattern. Their coarse levels became equal constants after the page reached its final level.

The first periodic fixture had independent red and green gradients plus a checker in blue. The second had an eight-pixel black-and-white checker. Both remain in the evidence. The simpler fixture did not replace the earlier colorful result.

The strict matrix used 16 explicit levels, including half levels and probes beyond the page's final level. Seven gradient probes included large footprints. The uncompressed pairs matched in all 23 comparisons. Independently compressed source and page pairs also matched in all 23 comparisons per format.

Quality against the uncompressed prepared source remained a separate result:

| Periodic fixture and output | Largest RGBA error against prepared source |
| --- | --- |
| Colorful pattern, BC3 | 0.447471 |
| Colorful pattern, BC7 | 0.146075 |
| Grayscale checker, BC7 | 0.003922 |

The colorful patterns therefore do not establish a small-error recipe. The grayscale difference came from alpha at selected levels. It reached one step in an eight-bit channel. BC7 must not receive unconditional opaque permission from this result.

For an opaque consumer that uses RGB and does not read alpha, the grayscale case is a narrower positive candidate. An exhaustive mip-center probe covered every source texel center at every source mip. It used the full source domain, not only the cropped region. All eight RGB comparisons against the prepared source were exact. Another 23 comparisons covered the thin triangle, half levels, and large footprints. They also had zero measured RGB error.

A separate threshold probe compared the grayscale red channel with a threshold of one half. It found no threshold-membership difference in its five tested levels. That result does not generalize to other masks or thresholds. Alpha still changed.

The raw encoded mip payload fell from about 21.36 KiB to 5.36 KiB for both tested block formats. The difference was exactly 16 KiB. This is about a 75 percent reduction in encoded image data. It is not a driver-allocation or peak-memory measurement. A real build saves the source allocation only when no retained use still needs it.

This demonstrates a useful sampling candidate, not a qualified vendor recipe or complete strict-mode implementation. Admission still needs an exact recipe, required-channel evidence across admitted states, and live revalidation. Do not add a general periodicity search to find such cases. Do not infer safe storage sharing from pixel resemblance alone.

A separate red-source and blue-source page demonstrated the opposite limit. Its final one-texel level could not retain both constants. Both comparisons differed by about one half at that level. Strict mode still cannot pack arbitrary independent content through a shared full mip chain. Larger borders cannot resolve that conflict.

## 6. Verification, cleanup, and tool limits

Observation on 2026-11-17: Thirteen fully recovered execution batches produced 494 GPU comparisons. Interpreter assertions checked the positive construction cases, negative controls, role separation, and result counts. Every recovered batch reported zero surviving owned objects and restoration of the previous active render target.

One initial CodeDom compilation failed because a local variable shadowed a lambda parameter. It executed no fixture. The corrected batch retained the same experiment.

One decoded-border diagnostic request returned a transport timeout. Tool history then showed two successful entries with the same result preview. Only the first-level measurement was recoverable. The full cleanup counts for those entries were not recovered. The diagnostic code had a finally block. Do not include those entries in the 13 fully recovered batches or the comparison count. The duplicate-execution or duplicate-history risk was reported through the tool issue route.

The Console query returned two asynchronous-readback errors and three warnings. This spike used render-target readback. The query did not establish the origin of the warnings. The Console was not cleared. No clean-Console claim follows.

No Unity Test Runner suite ran. No vendor render, AMUSE alpha classifier, complete packer, avatar build, or memory benchmark ran. No source asset or product code changed.

## 7. Gate disposition

As of 2026-11-17, G0.2 remains open. The bilinear block-neighborhood rule has analytical support and focused GPU evidence. Repeat, integer moves, aligned unequal maps, thin regions, and the full-chain conflict have additional evidence. A narrow strict candidate can reduce encoded data without a shader clamp.

The remaining blockers are a verified anisotropic support bound, an explicit coordinate-precision policy, actual frontend routes, and quality limits for required channels. Clamp compression also requires its own quality qualification. The evidence does not approve a global border, format menu, memory guard, or shipped strict control.

Next engineering step: Resolve coordinate precision and the anisotropic support contract. Then apply the accepted recipe-level qualification and per-output quality checks. Keep the colorful compression failures and alpha-loss cases as falsifiers. Resource measurements follow on the resulting bounded construction.

The controlling design is `docs/superpowers/specs/2026-11-16-merge-ready-atlasing-design.md`. The delivery gates are in `docs/superpowers/plans/2026-11-16-0-2-0-atlasing-implementation-plan.md`. Earlier sampling evidence is in `docs/superpowers/investigations/2026-11-16-atlasing-sampling-spike.md`.
