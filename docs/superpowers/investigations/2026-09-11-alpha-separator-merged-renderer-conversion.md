# Alpha separator over-conversion on merged skinned meshes

Date: 2026-09-11. Status: diagnosis complete, fix pending.

Privacy note: this record is sanitized. The avatar, its materials, and
its textures are named by role only. Exact triangle counts are recorded
as ranges or fractions. The private lab project is named by role as the
lab editor instance. Publicly available tools are named.

## Symptom

A lab avatar carries the AMUSE component with every slider at its inert
default. A play mode build moved the overwhelming majority of the dress
garment polygons to opaque materials. The summary line reported two
analyzed renderers, no alpha policy marker, and more than forty thousand
moved triangles. After the build, the dress and sleeves rendered far
more solid than they render in edit mode. Polygons whose texels are
fully transparent stayed on the original materials. Polygons whose
texels are only partially transparent moved and turned solid.

The user reports that this did not happen before the alpha policy
feature branch. The trigger is AAO, anatawa12's Avatar Optimizer. Its
TraceAndOptimize component has a Merge Skinned Mesh option. With that
option on, the build shows the problem. With it off, the build does
not. A second public optimizer, d4rkAvatarOptimizer, is present on the
avatar and was ruled out as the cause.

## What the merged build state looks like

AAO TraceAndOptimize runs before NDMF's PlatformFinish passes, so AMUSE
analyzes the merged renderer that AAO produces. The merge combines
thirty five skinned mesh renderers into two. The large merged renderer
holds eighteen material slots and roughly one hundred eighty thousand
triangles. After the bake, AMUSE appended three opaque submeshes to
that renderer: one for the dress, one for the sleeves, and one for a
floral decoration material. The appended submeshes hold roughly nine
tenths of the dress polygons and roughly nine tenths of the sleeve
polygons.

## Where the failure occurs

The failure occurs inside AMUSE, not inside AAO. AAO TraceAndOptimize
only changes the renderer state that AMUSE analyzes. On the merged
renderer, the material evidence capture for the dress material binds an
empty first texture slot, and the alpha resolution turns that unbound
main texture sample into a constant of full opacity. Both stages run in
the AmusePlatformFinishPass.

## Material and texture facts

The dress and sleeve slots use the vendor transparent lilToon variant
with an assigned alpha mask. The material settings are mask mode 2,
mask scale 1, and mask value 1. The color alpha is 1.

The pinned lilToon source in the lab project computes the mask as
`saturate(mask.r * _AlphaMaskScale + _AlphaMaskValue)` and applies mode
2 as a multiply. With scale 1 and value 1 this is
`saturate(mask.r + 1)`, which is 1 for every texel. The mask is
therefore inert at runtime for this configuration. The effective
fragment alpha is the diffuse texture's own alpha channel.

The diffuse texture's source image alpha histogram:

- about 36 percent of texels at 255
- about 52 percent of texels at 0
- the remaining texels partial

The lace look is fine grained: binary holes averaged by mip filtering
and bilinear sampling read as smooth translucency on screen.

## Measured facts about the moved set

Sampling the diffuse source image under exactly the triangles AMUSE
moved, at their baked UVs:

- the moved dress triangles sample about 82 percent fully opaque texels
  and about 18 percent partial texels
- the moved sleeve triangles sample about 63 percent fully opaque
  texels and about 37 percent partial texels

Under the exact contract, a polygon may move only when every consulted
texel of every consulted mip level is fully opaque. A polygon with a
single partial texel must stay. The moved set violates that contract.

## Measured facts about the captured evidence

The production capture for the dress material produced evidence with
exactly one populated alpha chain. That chain is binary and holds about
13 percent of texels at 255 and 87 percent at 0, which matches the
correct exact-255 composition of the diffuse alpha and the mask for
this configuration. The first texture slot of the evidence is empty.

Two facts cannot both be true: a chain with 13 percent opaque texels,
and a classification that proved nine tenths of the polygons. The
classification therefore consulted something other than that chain for
the moved polygons.

## Analysis

The failure point is the material evidence capture and the alpha
resolution that consumes it, both of which run in the
AmusePlatformFinishPass on the AAO merged renderer. The working
hypothesis: when the texture evidence for a required alpha sample fails
to bind (the first texture slot is empty), the alpha resolution falls
back to a constant instead of failing closed. A constant of full
opacity proves every polygon of the material, which matches the
observed move fraction. A capture refusal of this shape is expected on
this machine: the lab editor's texture quality settings leave mip
levels non-resident, and a probe capture of the same texture through
the production-shaped predicate path returned null for that reason
during this investigation.

Two smaller defects were found and fixed on this branch during the same
round and are recorded for completeness:

- the build mapper read a stored opaque clamp of zero as an opaque
  bound of zero, which admits every texel as opaque evidence
- the inspector normalization pushed a drawn per-polygon clamp of 100
  down to 99, silently leaving the inert state

## Conclusion

The separator must fail closed when a texture evidence chain is missing
or a capture fails. The affected material's alpha resolution must
become unknown, so its polygons stay on the original materials. The fix
is specified in the companion design and plan documents dated today.
