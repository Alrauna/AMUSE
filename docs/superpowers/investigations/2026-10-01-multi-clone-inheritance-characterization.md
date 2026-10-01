# Investigation: what a fresh Multi clone inherits

Date: 2026-10-01. Status: characterization complete. This record is the
plan Task 3 deliverable
(`docs/superpowers/plans/2026-09-30-liltoon-multi-parity-plan.md`, Stage 0).
It was produced in a scratch Unity 2022.3.22f1 project created for the Task
2 digest measurement, outside this repository, with the pinned lilToon
2.3.4 tag source (zip sha256 `e81579d3…`, the value already recorded in
`LilToonSourceAttestation.cs`'s provenance comments) installed as the
package.

Privacy note: this record names public vendor facts and aggregate results
only. The scratch project is named by role (the Task 2 throwaway project);
no machine, path, instance, port, or private asset appears here.

## Method

One editor script, run in the scratch project through the Unity Test
Framework's host editor in batch mode (executeMethod). For each supported
container (`_lil/lilToonMulti`, `Hidden/lilToonMultiOutline`):

1. `new Material(shader)` and read back: `renderQueue`, the `RenderType`
   override tag, `shaderKeywords`, and the `ShadowCaster` pass-enable state.
2. Pass-enable copy probe: a source material with
   `SetShaderPassEnabled("ShadowCaster", false)`, then `new Material(source)`,
   and read the clone's ShadowCaster pass-enable state.
3. Keyword copy probe: a source material with `UNITY_UI_ALPHACLIP` enabled,
   then `new Material(source)`, and read the clone's keyword set.

No asset was saved; every produced object was destroyed.

## Measured facts, 2026-10-01

| Fact | `_lil/lilToonMulti` | `Hidden/lilToonMultiOutline` |
|---|---|---|
| Bare clone render queue | 2000 (shader default) | 2900 (shader default) |
| Bare clone RenderType override tag | Opaque | Opaque |
| Bare clone keyword set | empty | empty |
| Bare clone ShadowCaster pass enabled | true | true |
| Pass-enable copy probe | copied (clone ShadowCaster disabled) | copied |
| Keyword copy probe | copied (`UNITY_UI_ALPHACLIP` carried) | copied |

Both probes reproduced identically on both containers.

## What this means

1. The container-default trap is confirmed: a bare clone of the outline
   container lands at queue 2900. The shipped recipe's explicit
   RenderType Opaque and queue 2000 writes (Task 8) are what hold both
   containers to the canonical state, and the Task 8 fixtures already pin
   it. This measurement is the dated evidence behind that ruling.
2. `new Material` copies the keyword set. The recipe's unconditional
   mode-0 keyword write is therefore necessary (a copied source keyword
   would re-enter a non-opaque mode) and sufficient (the write replaces
   the whole set). The Task 8 fixtures pin both directions.
3. `new Material` copies the pass-enable list. The `_AsOverlay` follow-up
   branch's blocking question — whether the clone path preserves a
   disabled ShadowCaster — is answered yes, natively, with no extra
   mechanism. The follow-up branch may start directly from this fact.

## Scope limits

One Unity version (2022.3.22f1), one install method (tag source zip laid
into `Packages/`), two settings shapes exercised in the same project for
the Task 2 measurement. The pass-enable probe covers ShadowCaster only;
the other pass-enable slots the editor writes (`DepthOnly`, `DepthNormals`,
`DepthForwardOnly`, `MotionVectors`) were not probed individually — the
built-in pipeline target renders only ShadowCaster of those.
