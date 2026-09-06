# S7 disposition - A8 lilToon affine UV is infeasible as scoped

Date: 2026-09-06. Scope: roadmap slice S7
(`docs/superpowers/plans/2026-09-05-0.1.0-implementation-roadmap.md`).
No production code changed. This note records why the slice stops and what
it would take to reopen it.

## 1. The roadmap premise and the contradiction

The roadmap scopes S7 as: "admitted `_MainTex_ST` tiling and handled
`_MainTex_ScrollRotate` under the same exactness rules Poiyomi uses; identity
case unchanged", implemented in
`LilToonCutoutMaterialSemantics.cs` and
`LilToonTransparentMaterialSemantics.cs`.

The affine MainTex ST design
(`docs/superpowers/specs/2026-08-31-affine-maintex-st-support-design.md`)
already decided the opposite for lilToon, from pinned source:

- §5-G (selected, `[DECISION]`): grant non-identity ST exactly where the
  attested source proves the sampler coordinate is the affine result with no
  further unbounded fragment arithmetic. Poiyomi 9.3.64 qualifies
  (`poiUV(uv, st)`, extra term exactly zero). lilToon 2.3.4 cutout does not:
  "lilToon 2.3.4 cutout keeps its shipped identity-only coverage and refuses
  non-identity ST in its own frontend ... which is where the shader-specific
  fact lives."
- §3.1 (`[SOURCE]`, official lilToon tag 2.3.4, commit `252fd8cf...`):
  `lilCalcUV` applies `lilRotateUV(outuv, uv_sr.z + uv_sr.w * LIL_TIME)`
  unconditionally after the affine step, and `lilRotateUV` has **no
  zero-angle early-out** (`lil_common_functions.hlsl:424-437`). Even at the
  standing gate `_MainTex_ScrollRotate == (0,0,0,0)` the executed expression
  is a sincos round trip, not the identity. §3.3-A4: no absolute bound for
  that displacement is citable on every runtime target the AMUSE contract
  covers. Admitting new coverage on top of it would adopt an unmodeled
  uncertainty, which the design bans (§5-B, C7: "by proof rather than by
  adopted constant").

## 2. The transparent family shares the wall

The transparent separation investigation
(`docs/superpowers/investigations/2026-09-01-liltoon-transparent-normal-alpha-separation.md`)
confirms the same facts independently: the transparent pass composes
`lilCalcUV(uvMain, _MainTex_ST, _MainTex_ScrollRotate)` through the same
`OVERRIDE_ANIMATE_MAIN_UV` arm, executes the same early-out-free
`lilRotateUV(uv, 0)`, and its refusal matrix lists non-identity `_MainTex_ST`
as a family-gate `UnsupportedUv` refusal that "the family boundary PR #42
preserved".

So both lilToon alpha families are blocked by the same pinned-source fact.
Porting the Poiyomi affine path to lilToon cannot be made sound without
either modeling the sincos round trip per runtime target or adopting an
uncertainty bound the design's own standard forbids.

## 3. The displacement is real but small; that is not enough

At angle `+0` the round trip reduces to `fl(fl(t-0.5)+0.5)` per axis plus
cross terms multiplied by exact `+0`/`1`. For `|t| >= 0.5` the two
roundings are exact (the coordinates stay multiples of the result ulp), so
the round trip is the identity there. For `|t| < 0.5` it is not: the
subtraction can round, so a static displacement of up to one ulp at the
coordinate magnitude perturbs the sample. If `sincos(+0)` is not exact on a
target, the `si` cross term scales with the coordinate magnitude and the
displacement is unbounded in the tile coordinate. Tiled UV layouts - the
exact case A8 wants - span both regions. A sound widening would need a
per-target `sincos` accuracy contract to cite. The re-attestation
investigation (2026-09-04, section on method limits) already found no
citable trig accuracy contract for the covered runtimes.

## 4. Options

1. **Descope A8 (recommended).** Non-identity ST and nonzero ScrollRotate
   keep refusing with the named `UnsupportedUv` diagnostic, which S4 makes
   visible and actionable. Tiled-lilToon users lose nothing they had:
   triangles stay on their original material with zero visual risk. The
   refusal already tells them why.
2. **Adopted-bound widening.** Inflate the affine envelope by a declared
   `sincos`/rounding constant. Sound only relative to a constant the design
   standard forbids adopting without evidence; would need a design
   amendment, a citable per-runtime trig contract, and gate-2-style
   approval. Reopen only if upstream lilToon adds a zero-angle early-out or
   a target contract appears.
3. **Exact-tier widening.** Admit ST combinations whose whole realizable
   domain survives the round trip exactly. Dead as stated: any tiled domain
   crosses `|t| < 0.5` per axis, where the round trip is not exact.

## 5. Decision requested

Option 1: drop S7 from 0.1.0 and record A8 as a standing, verified refusal.
The 0.1.0 shader-coverage goal (V3) is otherwise delivered by S6 plus the
existing Poiyomi affine path.
