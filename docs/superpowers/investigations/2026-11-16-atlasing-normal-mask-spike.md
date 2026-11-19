# Disposable normal and mask compression spike

Privacy note: This record contains public synthetic fixture results only. It contains no private asset observations or vendor source excerpts.

Date: 2026-11-16.
Status on 2026-11-16: The authorized characterization completed. Product code did not change. The design and production implementation remain unapproved.

## 1. Question and scope

How much do candidate compression recipes change decoded normals and threshold-sensitive masks after an aligned atlas placement?

The dev editor asset directory matched the repository asset directory exactly before execution. The spike used the same linear project and Direct3D11 backend as the preceding sampling spike. No operation ran in the private editor. Code and a shader compiled in memory. All textures, the shader, the material, and the render target were transient. No assets, scene objects, source files, or project settings changed.

This is characterization, not a Unity Test Runner run or an actual vendor shader test. It does not approve a format, a normal reduction recipe, or a quality tolerance.

## 2. Comparison method

The source was a linear RGBA32 image, 128 pixels square. The page was 256 pixels square. The sample region covered source coordinates from 32 to 96 on each axis. The copied neighborhood covered 16 to 112. Its destination began at 64. The resulting 48-pixel translation preserved compression-block phase through mip two.

Each protected page level copied the corresponding source mip neighborhood. Unused page pixels had a fixed contrasting value for masks and a flat normal for normal maps. Levels beyond two were not sampled. Page and source used trilinear filtering and Repeat addressing.

The GPU compared uncompressed prepared-source sampling against page sampling at levels zero, one half, one, one and one half, and two. Each render sampled 129 by 129 positions. Readback used a linear floating-point render target. The shader used explicit mip selection. It did not test anisotropic sampling, implicit derivatives, or vendor-specific sampling routes.

Each fixture and recipe ran once with an uncompressed page, then once with that page compressed. The editor encoder quality was 100. The harness checked that the resulting texture format matched the requested format. The source stayed uncompressed in both stages.

## 3. Normal fixtures and decoding

The smooth fixture used horizontal normal component `0.65 * sin(0.075 * x)` and vertical component `0.65 * cos(0.08 * y)`. The positive third component completed a unit vector.

The hard fixture used angle `((7 * x + 11 * y) modulo 31) * 2 * pi / 31`. Its radius alternated between 0.98 and 0.35 by three-pixel horizontal and five-pixel vertical bands. The positive third component again completed a unit vector. This creates rapidly changing directions and some near-grazing normals.

For each source mip, the harness averaged four preceding unit vectors and normalized their sum. It then encoded the first two components into the zero-to-one range. This defines the prepared reference for this spike. It is not proof that this reduction preserves the original shading distribution or normal variance.

Two representations were compared independently:

- BC5 stored the first two components in red and green.
- BC3 stored them in alpha and green. Red and blue were one.

The shader decoded the selected pair, reconstructed the positive third component, and normalized the result. It measured the angle between the decoded reference and output vectors. These explicit decoders do not establish that a particular lilToon or Poiyomi route accepts either representation.

Observation on 2026-11-16: BC5 had lower measured angular error than BC3 for these fixtures. Neither recipe preserved all local directions closely on the hard fixture.

| Fixture | Recipe | Largest local angle across sampled levels | Largest root mean square angle across sampled levels |
| --- | --- | --- | --- |
| Smooth | BC5, red and green | 2.145 degrees | 0.741 degrees |
| Smooth | BC3, alpha and green | 5.369 degrees | 1.586 degrees |
| Hard | BC5, red and green | 24.810 degrees | 3.746 degrees |
| Hard | BC3, alpha and green | 35.377 degrees | 6.495 degrees |

The two maxima in a row can come from different levels. The uncompressed normal controls differed by less than 0.00001 degrees. That small shader arithmetic residual is not a proposed quality tolerance. The prepared reference already includes eight-bit channel quantization. The table does not measure that quantization loss against the original vectors.

A separate CPU control compared the unit-vector-average reference with averaging only the first two components and then reconstructing the third. For the hard fixture, their largest angular difference was 39.598 degrees at mip one and 4.321 degrees at mip two. Both calculations started from the same preceding reference vectors at each level. This falsifies treating the two reduction recipes as equivalent. It does not select a complete shading-preservation policy.

## 4. Mask fixtures and threshold results

The ramp fixture set red and alpha to `0.5 + ((x modulo 17) - 8) / 255`. Green used `((x + y) modulo 13) / 12`. Blue was one quarter. The second fixture alternated red between 0.49 and 0.51 using the normal fixture's band pattern. Its alpha was one. Green and blue kept the same definitions. Unity generated the mask source mips.

The shader measured absolute red and alpha errors. It also compared the reference and output result of `value >= 0.5` separately for each channel. The threshold is a synthetic consumer operation, not a claim about a particular material's active threshold.

Observation on 2026-11-16: Both BC3 and BC7 changed threshold decisions. Small average error did not imply stable threshold behavior.

| Ramp output | Level | Maximum red error | Maximum alpha error | Red threshold changes | Alpha threshold changes |
| --- | --- | --- | --- | --- | --- |
| BC3 | 0 | 0.051118 | 0.003479 | 10.17% | 0% |
| BC3 | 2 | 0.043076 | 0.003815 | 46.45% | 3.88% |
| BC7 | 0 | 0.048447 | 0.048447 | 5.46% | 5.46% |
| BC7 | 2 | 0.026291 | 0.026291 | 12.39% | 12.39% |

Percentages describe sampled positions, not changed image texels or surface area. Source mips and filtering participate in each comparison. Uncompressed controls had no channel error or threshold changes at any tested level.

The second fixture supplied an opacity control. BC3 preserved its sampled alpha exactly. BC7 reduced some sampled alpha values from one by up to 0.003922. Neither format crossed the 0.5 alpha threshold in that fixture. Thus a stable threshold test does not prove exact opacity. The spike did not run AMUSE's final alpha classifier.

## 5. Design consequences

Recommendation on 2026-11-16: Keep sampling preservation, role quality, threshold behavior, and final opacity as distinct checks. A local angular bound and an aggregate angular measure serve different purposes. A global image average cannot hide large local normal errors.

Use BC5 as the first normal candidate only where the actual shader decoder supports its representation. Do not rewrite a shader route merely to fit that candidate. Do not assume BC3 is a quality-improving fallback for normals. It was worse in these fixtures.

Do not treat BC7 as a universal quality upgrade. It reduced some color errors in the preceding spike but changed alpha here, including fully opaque samples. Run final alpha proof on actual final output after any permitted replacement.

For masks, distinguish continuous-value use from an identified threshold operation. Do not apply a universal zero-threshold-change rule to every scalar map. A supported threshold-sensitive recipe needs evidence at the actual threshold and admitted states. These fixtures reject unconditional format acceptance. They do not set the allowed loss or authorize a new settings control.

Keep the existing bounded Automatic replacement limit. No format search, new format menu, or new abstraction follows from this spike. Preserve a dependent source use when neither permitted recipe meets its declared contract.

## 6. Verification and remaining gates

Observation on 2026-11-16: The batch produced 80 render comparisons. Forty were uncompressed controls. Interpreter assertions checked their residual bounds, exact mask equality, absence of control threshold changes, sample counts, and the threshold and opacity falsifiers. All assertions passed. These checks validate the measurements, not product behavior.

The finally block destroyed every owned Unity object and restored the previous active render target. The returned surviving-owned-object count was zero. No temporary asset folder was needed.

The final Console query returned the same two compressed asynchronous-readback errors recorded by the earlier acquisition spike. This batch used render-target readback. The Console was not cleared. There is no error-free Console claim.

Remaining on 2026-11-16: Verify actual vendor normal decoders, source import meaning, normal strength, reduction and variance behavior, color interpretation, active mask operations, admitted thresholds, and final alpha classification. Set quality bounds only after representative public fixtures and an explicit quality policy. Useful strict-mode cases and numeric memory or work limits also remain open. No benchmark or production test suite ran.

The controlling design is `docs/superpowers/specs/2026-11-16-merge-ready-atlasing-design.md`. The preceding placement and color evidence is `docs/superpowers/investigations/2026-11-16-atlasing-sampling-spike.md`.
