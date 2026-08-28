# Runtime Texture Evidence — Design

**Status: design only. No production code, no tests, and no implementation plan
accompany this document.**

Branch: `feat/runtime-texture-evidence`
Base commit: `cff5025` (`origin/main`, PR #25 merged)
Date: 2026-08-27

## 1. What this milestone buys

AMUSE can prove a triangle opaque from texture alpha only for textures that are
**readable, uncompressed, and single-mip** — `UnityAlphaFieldEvidence.TryCapture`
refuses `!isReadable`, refuses `mipmapCount != 1`, and admits four uncompressed
formats. Real avatar textures are none of those things. Every texture-backed
opacity proof therefore fails on real content today, which is why the
alpha-separation vertical slice records this as a blocking prerequisite
(`2026-08-27-alpha-separation-vertical-slice.md` §11 item 3).

This milestone replaces the acquisition route with the one characterized in
`2026-08-27-runtime-texture-evidence.md`: a GPU texel-fetch predicate rendered
into an `R8_UNorm` target, capturing **every declared mip**, and changes the
evidence the resolver carries from one grid to a validated mip chain.

It ships no transformation. It unblocks one.

## 2. Decision summary

| Decision | Outcome |
| --- | --- |
| New evidence type | **One:** `AlphaMipChain`, internal, in its own file (§4) |
| Change to `AlphaTextureData` | **None** |
| Change to `TriangleAlphaClassifier` | **None** |
| Change to `MaterialSemantics` | **None** |
| Change to NDMF phase / plugin configuration | **None** (§11) |
| Provider seam | `AlphaFieldProvider` retained; returned value becomes `AlphaMipChain` (§5) |
| Second provider / adapter / registry | **None** |
| Acquisition route | Single GPU predicate route for every admitted format (§7) |
| `GetPixels32` production path | **Removed.** Direct readback survives only as a research test oracle |
| Production shader | **One**, moved into the product package with its green diagnostic channel intact (§6) |
| Acquisition core | **One private static method** beneath the identity/policy gates, shared by ordinary capture and the host-capability check (§7.1) |
| Host-capability check | Accepted as gate 12; lazy, once per Editor AppDomain; a process-local latch, not evidence (§8.2) |
| Texture-evidence cache | **None.** No source-scoped or build-scoped store of captured texture evidence; existing batch deduplication is the whole answer (§10) |
| New folder | **One:** `Editor/Host/Shaders/` |
| Channels | `TextureChannel.Alpha` only |
| Build target | `BuildTarget.StandaloneWindows64` only |

## 3. Current-code evidence

Every claim below was read from the checked-out tree at `cff5025`.

| Fact | Location |
| --- | --- |
| Provider contract and its finite-and-`[0,1]` attestation | `Editor/Analysis/AlphaSemanticsResolver.cs:23-37` |
| `AlphaResolution` stores one field, validates resolved/failure in its ctor | `:44-76` |
| `TryGetUniformOutcome` is deliberately the whole uniform surface | `:111-147` |
| `Classify` delegates one grid to the classifier | `:157-165` |
| `ResolveScaledSample` reads no bytes for `k < 1`; it needs only the attestation | `:217-252` |
| `AlphaTextureData` validates width, height, and length, and copies its input | `Editor/Analysis/TriangleAlphaClassifier.cs:103-160` |
| `MaxSupportRegions = 65536` declared at `:173`, applied per grid at four sites | `:260, :310, :377, :465` |
| `IsFullyOpaque` / `IsFullyNonOpaque` short-circuit before any geometry work | `:201-206` |
| `TryCapture` refuses `!isReadable` and `mipmapCount != 1` | `Editor/Host/UnityAlphaFieldEvidence.cs:104-115` |
| `TryGetSampling` refuses `mipmapCount > 1` | `Editor/Semantics/UnityTextureEvidence.cs:83-88` |
| One `CapturedTextureEvidence` per `TextureSourceId` per batch | `Editor/Host/UnityMaterialEvidenceCapture.cs:674-721` |
| The single production call into `TryCapture` | `:989-993` |
| `GatherAlphaFields` and the inline provider lambda | `Editor/Host/UnityRendererAlphaAnalysis.cs:503-510, :575-597` |
| Build handoff local function `AlphaFields` | `Editor/Build/AmusePlatformFinishPlugin.cs:466-476` |
| Classified resolutions never merge | `Editor/Analysis/AdmittedMaterialStates.cs:154-195` |
| Capture runs inside a synchronous `BuildPhase.PlatformFinish` pass | `Editor/Build/AmusePlatformFinishPlugin.cs:91-101` |
| Release zips the product package excluding only `Tests/` | `.github/workflows/release.yml` |
| Research tests already reference `Alrauna.Amuse.Editor` | `Packages/com.alrauna.amuse.research/Tests/Editor/*.asmdef` |
| Product grants `InternalsVisibleTo` to the research test assembly | `Editor/AssemblyInfo.cs:20` |

Two observations that change how this design is scoped:

**The instance half of `UnityAlphaFieldEvidence` has no production consumer.**
`new UnityAlphaFieldEvidence(...)` and `TryGetAlphaField` are referenced only from
`Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs` and
`Tests/Editor/Host/AlphaEvidenceClassifierIntegrationTests.cs`. The two production
providers are the lambdas at `UnityRendererAlphaAnalysis.cs:503` and
`AmusePlatformFinishPlugin.cs:468`, both built over `GatherAlphaFields`, and the
only production entry into the class is the **static** `TryCapture`. Seam 3's
instance half is therefore a test-only surface today. This design changes its
types so it stays compilable and honest, and does **not** delete it: removing a
documented `AlphaFieldProvider` implementation is a separate decision, recorded in
§15.

**Batch deduplication is batch-scoped, not build-scoped** (§10).

## 4. `AlphaMipChain` — the evidence representation

### 4.1 Placement

**Its own file: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaMipChain.cs`.**

The alternative — beside `AlphaTextureData` in `TriangleAlphaClassifier.cs` — was
evaluated and rejected. That file is the **single-grid classifier vocabulary**:
`TriangleAlphaOutcome`, `AlphaFilterMode`, `AlphaWrapMode`,
`AlphaSamplingSettings`, `TriangleAlphaInput`, `AlphaTextureData`, and the
classifier itself, 610 lines. The classifier never sees a chain, and the pinned
boundary keeps both it and `AlphaTextureData` unchanged; adding the chain there
would place a type inside the one file this milestone is supposed to leave alone
and would imply the classifier consumes it.

`AlphaSemanticsResolver.cs` was also evaluated and rejected: the chain is carried
by `Editor/Host/` types (`CapturedTextureEvidence`, `GatherAlphaFields`) that have
nothing to do with resolution, so it is not resolver vocabulary either.

A separate file for a type with exactly one responsibility matches
`AdmittedMaterialStates.cs` and `MeshSeparationPlanner.cs`. It is the smaller
change in the sense that matters: it touches no file whose contents are pinned.

### 4.2 Surface

```
internal sealed class AlphaMipChain
{
    internal AlphaMipChain(IReadOnlyList<AlphaTextureData> levelsFromMipZero);
    internal int Count { get; }
    internal AlphaTextureData this[int index] { get; }
}
```

That is the whole surface. Deliberately absent:

- no `Base` / `MipZero` convenience property — every consumer either iterates or
  carries the value opaquely, and a "the important one" accessor invites exactly
  the mip-0-only reasoning §9 of the investigation refutes;
- no exposed collection property — it would leak the backing array;
- no equality, matching `AlphaResolution`, which has none either, and matching the
  `AdmittedMaterialStates` rule that classified resolutions never merge;
- no `IsFullyOpaque` aggregate — see §5.4;
- no `Width` / `Height` — those belong to a level, and a chain-level dimension
  would be mip 0's under another name.

### 4.3 Constructor invariants

Enforced, in this order:

| # | Condition | Failure |
| --- | --- | --- |
| 1 | list is not null | `ArgumentNullException` |
| 2 | list is non-empty | `ArgumentException` |
| 3 | no element is null | `ArgumentNullException`, naming the index |
| 4 | for every `i >= 1`: `w[i] == max(1, w[i-1] >> 1)` and `h[i] == max(1, h[i-1] >> 1)` | `ArgumentException`, naming the index |

The constructor copies the list into a private array. The elements are already
immutable and already defensively copied by `AlphaTextureData`, so a shallow copy
is a genuine deep-immutability guarantee, and it matches what
`AlphaTextureData` does with its own input.

**Invariant 2 is the load-bearing one.** An empty chain would make "every mip is
`ProvenOpaque`" vacuously true and turn §5.3's conjunction into an unconditional
`ProvenOpaque` — the exact false-positive direction AMUSE's correctness policy
forbids. Making the empty chain unrepresentable is why this is a type and not an
`IReadOnlyList<AlphaTextureData>`.

Invariant 4 is stated per axis because §6.3 of the investigation measured
`16x4 -> 8x2 -> 4x1 -> 2x1 -> 1x1`: one axis clamps at one while the other keeps
halving. A single "halve both" rule would reject a legitimate non-square chain,
and a looser "each dimension is no larger than the previous" rule would accept a
malformed one.

### 4.4 A deliberate non-invariant

**The chain is not required to terminate at 1x1.** Unity permits a `Texture2D`
whose `mipmapCount` is less than a full chain, and the type cannot see
`mipmapCount` to check the claim anyway. Requiring termination would be an
invariant beyond the pinned list that the type has no evidence for.

Chain **completeness** — that the levels are every level the sampler may select —
is therefore not a type invariant. It is part of the **provider contract** (§5.1)
and is enforced by the producer, which refuses the whole texture unless every
declared mip captures (§8). The division is deliberate and must stay visible in
both doc comments: *the type guarantees shape; the provider attests completeness.*

### 4.5 Why this is not a general IR

It holds `AlphaTextureData`, an existing type, in an order, and answers two
questions: how many levels, and which grid is level `i`. It has no notion of
format, channel, colour space, sampler, texture kind, source, or transformation;
no way to represent a colour, a magnitude, a non-alpha channel, a 3D or array
texture, or a level that is anything other than the existing grid. It cannot be
extended into a sampling framework without adding every one of those. It exists
because §6.1 of the investigation measured a texture whose mip 0 proves opaque
while mip 1 does not, and the current single-grid value cannot express the
counterexample.

## 5. The six seams

Each seam carries exactly one `AlphaTextureData` today. The change is a change of
type on one value, propagated. No adapter, no second provider, no compatibility
shim, and no parallel evidence graph is introduced to avoid it.

### 5.1 Seam 1 — `AlphaFieldProvider`

`Editor/Analysis/AlphaSemanticsResolver.cs:34-37`

```
internal delegate bool AlphaFieldProvider(
    TextureSourceId source,
    TextureChannel channel,
    out AlphaMipChain chain);
```

The XML contract needs two substantive edits, not just a type swap:

1. The predicate attestation — finite and within `[0, 1]`, byte 255 marks exactly
   the texels whose value is exactly 1, every other byte marks a value strictly
   below 1, bottom-to-top row-major — now binds **at every level**, not over "the
   relevant base-level texel domain".
2. A new clause: the chain is the source's **complete declared mip chain in
   order**, mip 0 first. Without this the conjunction in §5.3 is unsound — an
   unexamined level the sampler could select could be non-opaque, and the
   resolution would report `ProvenOpaque`.

The sentence "Under Point or Bilinear sampling those facts give the classifier its
predicate" stays true and gains a clause: the level is chosen by hardware and is
not knowable, so the conjunction over levels is what closes the gap. Trilinear
remains outside the vocabulary (§9).

### 5.2 Seam 2 — `AlphaResolution`

`Editor/Analysis/AlphaSemanticsResolver.cs:48, :56, :101`

- field `_field` becomes `_chain`, typed `AlphaMipChain`;
- the private constructor's parameter changes type; its null guard at `:67-70` is
  unchanged in meaning — a classified resolution still always has its evidence;
- `Classified(AlphaMipChain chain, AlphaSamplingSettings sampling)`;
- `Classify` aggregates (§5.3).

`IsResolved`, `Failure`, `Refused`, `Uniform`, and `TryGetUniformOutcome` are
untouched.

### 5.3 Outcome aggregation and precedence

In `AlphaResolution.Classify`, for a classified resolution:

```
sawUnknown = false
for i in 0 .. chain.Count-1:
    outcome = TriangleAlphaClassifier.Classify(triangle, chain[i], sampling)
    if outcome == MustRemainTransparent: return MustRemainTransparent
    if outcome == Unknown: sawUnknown = true
return sawUnknown ? Unknown : ProvenOpaque
```

Precedence, matching the pinned rule:

| Condition | Outcome |
| --- | --- |
| any level `MustRemainTransparent` | `MustRemainTransparent` |
| otherwise, any level `Unknown` | `Unknown` |
| otherwise (every level `ProvenOpaque`) | `ProvenOpaque` |

**Semantics.** A mip chain is *alternative evidence about one configuration*, not
a set of admitted configurations. The hardware may select any level and AMUSE
cannot know which, so a single non-opaque level is a counterexample. This is
deliberately unlike consensus aggregation over `AdmittedMaterialStates`, where
AMUSE enumerates states a material may legitimately occupy.

**Early exit on `MustRemainTransparent` only.** It is the absorbing element, so
returning immediately cannot change the result. `Unknown` must **not** early-exit:
a later level may be `MustRemainTransparent`, which outranks it. An implementation
that returns on the first `Unknown` is sound-but-degraded in one direction and
loses a refusal in the other; §13 pins it with a test.

`ProvenOpaque` is reachable only after the loop body has executed at least once,
which invariant 2 of §4.3 guarantees.

### 5.4 `TryGetUniformOutcome` stays exactly as narrow

No new uniform path is introduced. In particular the tempting shortcut — "every
level reports `IsFullyOpaque`, so return `Uniform(ProvenOpaque)`" — is
**rejected**. `AlphaResolution.Uniform` means *independent of geometry*, and it
feeds `AdmittedMaterialStates.DistinctResolutions`, where two uniform resolutions
carrying the same outcome **merge**. Converting classified evidence into a uniform
resolution therefore changes merging behaviour, and it re-describes a classified
chain as uniform on the strength of a property of the evidence — the same door the
comment at `AlphaSemanticsResolver.cs:125-134` closes for sampled agreement. The
existing `IsFullyOpaque` short-circuit inside the classifier already delivers the
performance benefit without touching the resolution's kind (§14).

### 5.5 Scaled-sample range attestation stays valid

`ResolveScaledSample` (`:217-252`) changes by one type only:

```
if (!fieldProvider(sample.Source, channel, out var chain) || chain == null)
    return Refused(MissingTextureEvidence);
return Uniform(MustRemainTransparent);
```

The lemma is unchanged and, if anything, strengthened. For `k < 1` the resolver
needs the provider's attestation that the sampled value lies in `[0, 1]` and not
one byte of its contents. Under a chain that attestation holds at **every** level,
and bilinear filtering within a level is a convex combination, so whichever level
the hardware selects, `alpha = s * k <= k < 1` at every reachable sample. The
multiplier lemma's premise is now established over a larger set of sampling
outcomes than before, not a smaller one.

This is also why the `Uniform` returned here is legitimate and does not violate
§5.4: it comes from the multiplier, which is genuinely constant across the
surface, not from observing that some classifications happened to agree.

### 5.6 Seams 3-6

| # | Seam | Change |
| --- | --- | --- |
| 3 | `UnityAlphaFieldEvidence` | `Dictionary<TextureSourceId, AlphaMipChain>`; `TryCapture(Texture, out TextureSourceId, out AlphaMipChain)`; `TryGetAlphaField(..., out AlphaMipChain)`. `TryCapture`'s body is replaced entirely (§7-8). The channel gate, the identity gate, the `ArgumentException` on an uninitialized source, and the `ArgumentOutOfRangeException` on an undefined channel are unchanged. |
| 4 | `CapturedTextureEvidence.AlphaChannel` | Property type at `:263`, constructor parameter at `:274`, and the capture site at `:989-993`. **The property keeps its name.** It is still the alpha-channel evidence for the texture; renaming it would churn the constructor and every test for no gain in meaning. Its doc gains one sentence saying the value is the complete declared mip chain. |
| 5 | `UnityRendererAlphaAnalysis` | `GatherAlphaFields` returns `IReadOnlyDictionary<TextureSourceId, AlphaMipChain>` (`:575-597`); the inline provider lambda's `out` parameter changes type (`:503-510`). Its `channel == TextureChannel.Alpha` guard is unchanged. |
| 6 | `AmusePlatformFinishPlugin` | The local function `AlphaFields` (`:466-476`) changes its `out` parameter type. Nothing else in the pass changes. |

### 5.7 `AdmittedMaterialStates` — comment only

`Editor/Analysis/AdmittedMaterialStates.cs:175` names `AlphaTextureData` in the
rule that classified resolutions never merge. The rule is **unaffected in
substance**: two reference-distinct chains are no more cheaply provable equivalent
than two reference-distinct grids, and the categorical refusal to merge classified
resolutions — including with themselves — still holds for the same reason. The
comment updates to name `AlphaMipChain`, and gains nothing else. This is the only
change to that file.

## 6. Shader ownership

### 6.1 Where the production shader lives

`Packages/com.alrauna.amuse/Editor/Host/Shaders/AmuseAlphaExactOne.shader`

Shader name string: `Hidden/Alrauna/Amuse/AlphaExactOne`.

Packaging was inspected before choosing this. `.github/workflows/release.yml`
zips the entire package directory with `-x "Tests/*" "Tests.meta"` and then
**verifies** that no `Tests/` content re-entered the artifact, so any path outside
`Tests/` ships and any path inside it cannot. `Editor/Host/Shaders/` therefore
ships with the product. Placing it under `Editor/` additionally means Unity
excludes the asset from player builds, so the shader never reaches a built avatar —
which is correct, because it is only ever used by an Editor-time build pass.

The research package is excluded from the VPM listing and from every release
artifact (`build-listing.yml` builds only `vars.PACKAGE_NAME`), so a production
shader may not live there: it would not ship.

### 6.2 Lookup, and what happens when it fails

```
AssetDatabase.LoadAssetAtPath<Shader>(
    "Packages/com.alrauna.amuse/Editor/Host/Shaders/AmuseAlphaExactOne.shader")
```

held as an `internal const string` next to the capture code. UPM addresses every
package as `Packages/<package-name>/...` regardless of where it physically lives —
embedded, local, git, or VPM-installed — so the path is stable across every
install shape, and it is the mechanism the merged research probe has already been
exercising.

`Shader.Find` was considered and rejected: it resolves by shader **name**, which
is not owned by this repository and can collide with another package's
`Hidden/` shader, and it would silently bind to whichever asset won.
GUID lookup via `AssetDatabase.GUIDToAssetPath` was also considered and rejected:
it survives a file move, which is a benefit this design does not need, at the cost
of a hex literal that no reader can check against the tree.

**Failure is a refusal, never an exception and never a log.** A null asset or
`!shader.isSupported` fails gate 11 (§8), `TryCapture` returns `false`, and the
texture simply has no alpha evidence — the same outcome as an unsupported format.
Because gate 11 runs before any GPU call, a missing shader cannot produce a Unity
console error, and because it runs on every capture, a shader that becomes
unsupported mid-session refuses rather than producing a wrong answer.

### 6.3 How research reuses it, without a copy

Sharing is practical, and duplication is therefore rejected. Two facts make it
work with no new plumbing:

- `Packages/com.alrauna.amuse.research/Tests/Editor/Alrauna.Amuse.Research.Tests.Editor.asmdef`
  already references `Alrauna.Amuse.Editor`;
- `Packages/com.alrauna.amuse/Editor/AssemblyInfo.cs:20` already grants
  `InternalsVisibleTo("Alrauna.Amuse.Research.Tests.Editor")`.

So the production capture type exposes its shader path as an
`internal const string`, and `AlphaEvidenceProbe` uses that constant instead of
its own literal. The characterization then loads **the production asset**, and the
path cannot drift because there is only one string.

The dependency direction is the permitted one: research depends on product.
Nothing in `Packages/com.alrauna.amuse/` gains a reference to the research
package, and the product `package.json` is unchanged.

### 6.4 The move, and GUIDs

`Tests/Editor/Calibration/AlphaExactOneProbe.shader` moves to the production path.
The `.shader` and its `.meta` move **together**, preserving GUID
`85ccb222632d847b6b653f0e05b1ee97`. Nothing references the shader by GUID today —
lookup is by path — so preservation is insurance rather than a requirement, but
moving the `.meta` is mandatory in either case: a missing `.meta` gives the asset a
new GUID on import, which `CLAUDE.md` treats as a compatibility change rather than
cosmetic churn.

**The shader body is unchanged by the move.** It keeps
`float4(alpha == 1.0 ? 1.0 : 0.0, alpha, 0.0, 1.0)`, green included.

Production renders it into an `R8_UNorm` target, which stores **only the red
component**. The green raw-alpha component is discarded by the destination before
any production code sees a result, so it carries no production evidence meaning,
has no production reader, and adds no production code path — nothing in production
samples, validates, or branches on it. The research characterization renders the
same shader into its float diagnostic target and reads green there, which is why
the channel survives the move rather than being deleted and re-created.

Only the header comment changes: from "research-only, never referenced by the
product package" to a statement of the shader's production role, its `R8_UNorm`
target contract, and the fact that green is a research-only diagnostic which the
production target discards.

### 6.5 Why there is exactly one shader

**One** shader asset exists after this change. An earlier draft of this design
proposed a second, research-only magnitude shader so that the production shader
could emit red alone; **that is withdrawn.** It would have created a second asset
to keep aligned for no gain, because §6.4 shows the green component costs
production nothing — the `R8_UNorm` target discards it.

`AlphaEvidenceProbe` therefore keeps both of its routes against the single moved
asset: the predicate route through the `R8_UNorm` target, and
`TryCaptureRawAlphaDiagnostic` through its float diagnostic target. The one case
that genuinely needs magnitudes —
`AFloatFieldDefeatsTheExactlyOnePredicateAsAnAttestation`, which shows that `2.0`,
`-1.0`, `NaN` and `+Inf` are stored while the one-bit predicate reports the same
`0` for all of them — needs no re-basing onto a different evidence source, and its
"diagnostic only, no other case may use this" doc stands unchanged.

## 7. The acquisition route

One route for every admitted format. `GetPixels32` is removed from production;
direct readback survives only as `AlphaEvidenceProbe.TryDirectReadback`, a research
test oracle.

Per level: set `_Mip`; allocate a `RenderTexture` sized `max(1, w >> mip)` by
`max(1, h >> mip)` with `GraphicsFormat.R8_UNorm`, `sRGB = false`,
`useMipMap = false`, `autoGenerateMips = false`; `Graphics.Blit(texture, target,
material)`; `AsyncGPUReadback.Request(target, 0, R8_UNorm)`; `WaitForCompletion()`;
validate; copy bytes into a managed array; construct one `AlphaTextureData`.

After every declared mip has produced a level, and only then, construct the
`AlphaMipChain`.

### 7.1 Layering: one private acquisition core

`UnityAlphaFieldEvidence.TryCapture` stays the entry point and keeps every identity
and policy gate (§8). Beneath those gates sits **one private acquisition core**:

```
private static bool TryAcquireLevel(
    Texture2D texture,
    int mip,
    Material material,
    out AlphaTextureData level)
```

The core performs exactly the Blit / `R8_UNorm` / readback / validation sequence of
§7.2 for one level, and nothing else. It contains **no** identity, build-target,
format-allowlist, mip-limit, streaming, or capability gate — those belong to its
caller, and repeating them here would create a second place for the policy to
drift.

It has exactly two callers, both inside `UnityAlphaFieldEvidence`:

1. **ordinary chain capture**, which calls it once per declared mip after
   `TryCapture`'s gates have passed;
2. **the host-capability check of §8.2**, which calls it once against a small
   asymmetric in-memory texture.

This layering is forced, not stylistic. The self-check's fixture is built in memory
and therefore has **no asset identity**, so `UnityTextureEvidence.TryGetSourceId`
fails for it and it can never pass gate 2. A self-check written against the public
entry point is impossible; a self-check written against a *copy* of the acquisition
logic would prove nothing about the path production actually runs. The core is the
smallest construct that lets both callers execute the identical sequence.

It is a private static method. It is not a service, registry, interface, factory,
injectable backend, or public abstraction, and nothing outside
`UnityAlphaFieldEvidence` names it.

**Visibility, stated exactly.** `TryAcquireLevel` stays **private** — no test calls
it. The narrow validators it calls (§7.2) and the gate predicates of §8 are
**`internal`**, which makes them reachable from `Alrauna.Amuse.Tests.Editor` through
the `InternalsVisibleTo` grant that already exists at
`Editor/AssemblyInfo.cs:3`, with no new seam. That is the whole visibility change.
Each helper remains one narrow named check that production calls; no general
validation API, validator registry, or exported "GPU capture" surface is created,
and a helper production stopped calling would be a test-only seam and would have to
go.

### 7.2 Validation performed on every level

| Check | Meaning if it fails |
| --- | --- |
| `target.graphicsFormat == R8_UNorm` exactly | Unity substituted a format; the readback no longer means what the predicate means. Refusal, not tolerance. |
| `target.width/height` equal the requested size | Output integrity |
| `!request.hasError` | The level did not capture |
| `request.width/height` equal the requested size | Output integrity |
| `data.Length == width * height` | Output integrity |
| every byte is exactly `0` or `255` | The value was filtered, rescaled, or transfer-converted on the way out |

Each of these is a **narrow pure predicate** that the core calls — an exact
`GraphicsFormat` comparison, a dimension comparison, a length comparison, and a
binary-byte scan. They are unit-testable in isolation precisely because production
is their caller (§13.5).

These are **output-integrity** checks on a destination this code allocated. None of
them establishes that the requested *source* level was resident; that is what the
declared-state gates in §8 are for, and no comment or doc may claim otherwise.

### 7.3 Resource ownership and cleanup

| Resource | Created | Released |
| --- | --- | --- |
| `Material` | once **per texture**, from the loaded shader | `Object.DestroyImmediate` in the texture-scoped `finally` |
| `RenderTexture` | `RenderTexture.GetTemporary` once **per level** | `RenderTexture.ReleaseTemporary` in the level-scoped `finally` |
| active render target | read before `Blit` | restored in a `finally` immediately around the `Blit` |
| `AsyncGPUReadbackRequest` | per level; a struct, nothing to dispose | its `NativeArray<byte>` is owned by the request and is **copied into a managed array inside the request's scope**, never returned or retained |

Notes that matter for a correct implementation:

- The material is per texture rather than per level because `Graphics.Blit` sets
  `_MainTex` on it and `_Mip` is the only thing that varies between levels. One
  allocation per texture, not per mip.
- `AlphaTextureData`'s constructor takes `IReadOnlyList<byte>`, which
  `NativeArray<byte>` does not implement, so the managed copy is forced by the
  existing type rather than chosen. That is the desired outcome regardless: the
  native buffer must not outlive the request.
- Restoring `RenderTexture.active` is not cosmetic. Leaving it pointing at a
  released temporary is a defect that surfaces far from here.
- Exactly one target is live at a time. Peak temporary cost is one byte per texel
  of mip 0.

### 7.4 Exception boundary

`MissingReferenceException` from any Unity-object read is caught and converted to a
refusal — the existing pattern at `UnityAlphaFieldEvidence.cs:159-167`, retained
for the same measured reason.

Nothing else is caught. There is **no** blanket `catch (Exception)`: an
`ArgumentException`, `NullReferenceException`, or `InvalidOperationException`
raised from this code is a programming defect and must propagate, per `CLAUDE.md`.
The `catch (ArgumentException)` that currently wraps `GetPixels32` disappears with
`GetPixels32`.

Console noise is avoided structurally rather than by catching: the format
allowlist is evaluated **before** any GPU call, so the compressed-source direct-
readback path that Unity logs a hard error for (§3.2 of the investigation) is never
entered.

## 8. Gates, ordering, and refusal

**Every policy and capability gate below runs before `GetTemporary`, `Blit`, or any
readback.** That is the property the design depends on, and it is the only ordering
claim made here. The table is *not* a strict cost ranking — gate 2's asset-database
identity lookup is more expensive than gate 3's enum comparison, and deliberately
precedes it, because identity is the precondition for the whole evidence contract
and a texture without it is refused regardless of target. The `Cost` column
describes each gate, it does not order them.

| # | Gate | Cost |
| --- | --- | --- |
| 1 | `texture as Texture2D != null` (Unity equality — true for a destroyed object) | reference |
| 2 | `UnityTextureEvidence.TryGetSourceId` succeeds | asset-db lookup |
| 3 | `EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64` | enum compare; eliminates every non-Windows project at once |
| 4 | `texture.format` is in the allowlist | enum compare |
| 5 | `texture.activeMipmapLimit == 0` | property read |
| 6 | `!texture.streamingMipmaps` | property read |
| 7 | `width > 0 && height > 0 && mipmapCount > 0` | property reads |
| 8 | `SystemInfo.supportsAsyncGPUReadback` | device query, host-constant |
| 9 | `SystemInfo.IsFormatSupported(R8_UNorm, Render)` **and** `SystemInfo.IsFormatSupported(R8_UNorm, ReadPixels)` | device query, host-constant |
| 10 | `SystemInfo.IsFormatSupported(texture.graphicsFormat, FormatUsage.Sample)` | device query, per texture |
| 11 | shader loads at the production path and `shader.isSupported` | asset-db load |
| 12 | the host-capability check of §8.2 has passed | one lazy evaluation per Editor AppDomain |

Gate 3 before gate 4 is deliberate: the format allowlist is only meaningful for the
target whose import was characterized, and on a non-Windows target the loaded
import is a different asset variant entirely.

Gates 5 and 6 are gates on **declared state**. `activeMipmapLimit` is the
per-texture effective limit and already folds in the global limit and any mipmap-
limit group, so it is one check rather than a survey. Streaming is **refused, not
handled**: supporting it would mean designing against behaviour nobody has observed.

**Gate 8** is the whole route's precondition: without `supportsAsyncGPUReadback`
there is no way to get bytes back at all. §2 of the investigation recorded it
`True` on the measured host, but it is a device capability and must be gated rather
than assumed.

**Gate 10 is not a duplicate of gate 4.** Gate 4 asks a policy question — is this
`TextureFormat` one AMUSE has characterized — over the enum Unity reports for the
imported asset. Gate 10 asks a device question about the **actual imported source
representation**: can this GPU sample `texture.graphicsFormat`. The shader reads the
source through `Load` on exactly that representation, so a source the device cannot
sample must refuse *before* anything is allocated, rather than producing a blit
whose result has no defined meaning.

Gates 8, 9 and 10 together are a **pure predicate over four capability facts** —
`(supportsAsyncGPUReadback, r8Renderable, r8Readable, sourceSampleable)` — and are
written that way. That is acceptable, and is the preferred shape, because
production is the predicate's caller: the predicate *is* the gate, not a
restatement of it (§13.4).

Gate 12 is last because it depends on gates 9 and 11 having already passed: the
check itself renders through the production shader into an `R8_UNorm` target.

Channel is gated separately, in `TryGetAlphaField`, where it already is:
`channel != TextureChannel.Alpha` refuses.

**Allowlist**, exactly: `RGBA32`, `ARGB32`, `Alpha8`, `RGB24`, `DXT5`, `BC7`.

**Refused**, exactly and for stated reasons: every float format (the predicate
cannot attest finite-and-`[0,1]`, and the format supplies no structural
guarantee); `DXT5Crunched` (behaves as `DXT5` in an earlier measurement but is not
durably exercised through the R8 path); `ARGB4444` (**not** because 4-bit alpha
quantizes several authoring values to exactly one — that is not by itself unsafe,
since the imported GPU-decoded representation is what playback samples, so a source
`254` that imports to exactly one genuinely *is* opaque as rendered; it is refused
because it has no durable approved production-shaped characterization in this
milestone); all ASTC (tolerance-based decode, and it is the Quest format); ETC/EAC,
PVRTC, BC1/BC4/BC5, and everything unlisted (not characterized); every non-
`StandaloneWindows64` target; streaming textures; nonzero mip limits; non-alpha
channels; a host missing async GPU readback, exact `R8_UNorm` render/readback, or
the ability to sample the source's `graphicsFormat`; a host whose §8.2
capability check did not pass; and any partial or malformed GPU result.

The allowlist is not broadened by this design.

### 8.1 Partial per-mip failure

If any level fails any check in §7.2, the **texture** refuses: `TryCapture` returns
`false`, `source` and the chain out-parameter are reset, and no chain is
constructed.

Two structural properties make this leak-free and partial-evidence-free:

1. Each level's `RenderTexture` is released in its own `finally` and the material
   in the texture-scoped `finally`, so a mid-chain failure releases everything
   already allocated and allocates nothing further.
2. The `AlphaMipChain` is constructed **only after** every declared mip has
   succeeded. There is no code path on which a partially populated chain exists,
   so none can escape. Levels captured before the failure are ordinary managed
   objects and are collected.

A failed alpha capture does not widen refusal for the texture's other facts: see
§10.

### 8.2 The host-capability check

**Accepted.** It is gate 12 — a gate, never an evidence source.

#### Why it exists

Row order is soundness-critical: `AlphaTextureData` is indexed `y * Width + x`
bottom-to-top, and the classifier maps UV to texel coordinates with `v` increasing
upward. A vertical flip would attribute alpha to the wrong triangles and could
yield a false `ProvenOpaque` — the forbidden direction.

The measured orientation agreement (§3.4, §6.3 of the investigation) is **Metal
only**. The shader derives its source texel from `SV_POSITION`, whose origin
convention is graphics-API dependent, and reads it back from a render target whose
readback row order is also a Unity convention. On D3D-like APIs the two conventions
compose to the measured bottom-to-top result; on OpenGL-like APIs both flip
together and are expected to cancel — but that is a reading, not a measurement.
**Gate 3 does not constrain this:** `activeBuildTarget == StandaloneWindows64` says
nothing about the *editor's* graphics API, and capture runs in whatever Editor the
user builds in.

#### Mechanics

- **Fixture.** A small in-memory `Texture2D` in an admitted uncompressed format
  carrying a deliberately asymmetric alpha pattern: asymmetric on both axes, not
  symmetric under transpose, and **non-square**, so that a vertical flip, a
  horizontal mirror, a transpose, and a width/height swap each produce a different
  result from the expected one. It is created, used, and destroyed inside the
  check. No asset is written and nothing persists.
- **Path.** The check calls the **private acquisition core of §7.1** — the exact
  Blit / `R8_UNorm` / readback / validation sequence ordinary capture runs. It does
  not go through `TryCapture`, and it *cannot*: an in-memory texture has no asset
  identity, so `UnityTextureEvidence.TryGetSourceId` fails and gate 2 would refuse
  it before anything happened (§7.1).
- **When.** Lazily, **once per Editor AppDomain**, on the first capture that
  reaches gate 12 — that is, after gate 11 has loaded the production shader and
  gates 8-10 have confirmed the device capabilities the check itself depends on. A
  project in which no texture ever reaches gate 12 never runs it.
- **Outcome.** Pass, or **every texture-alpha capture refuses** for the remainder
  of the AppDomain. A core that returns no level and a core that returns a level
  whose pattern is anything other than the expected one are the same outcome:
  refusal. There is no partial credit and no retry.
- **Storage.** A `static bool?` on `UnityAlphaFieldEvidence`, cleared naturally by
  domain reload. It is a **process-local host-capability latch**: it records one
  fact about this Editor process's graphics stack, is keyed by nothing, and holds
  no texel, no texture, and no source identity. It is explicitly **not** the
  source- or build-scoped texture-evidence cache that §10 rules out, and it must
  never be grown into one.
- **What it is not.** Not a registry, service, public abstraction, NDMF state
  object, `BuildContext` extension, or general GPU extraction framework. It is one
  nullable static field and one private method, both inside
  `UnityAlphaFieldEvidence`.

#### What it proves, stated narrowly

It proves that **on the current host, the production route preserves the expected
bottom-to-top row-major orientation and the expected binary `R8_UNorm` encoding**,
end to end, through the same acquisition core ordinary capture uses.

It proves nothing beyond that. In particular it does **not** independently attest
the decode or swizzle behaviour of every admitted compressed format: the fixture is
a single uncompressed texture, and `DXT5` and `BC7` decode correctness continues to
rest on the specification authorities and the durable characterization recorded in
the investigation. It is not a substitute for the per-format coverage in §13.3, and
no doc may describe it as one.

## 9. `UnityTextureEvidence.TryGetSampling`

`Editor/Semantics/UnityTextureEvidence.cs:83-88` currently reads:

```
if (texture.mipmapCount > 1 ||
    texture.mipMapBias != 0f ||
    texture.anisoLevel > 1)
```

**The only change is deleting `texture.mipmapCount > 1 ||`.** The doc comment drops
the word "mipmapped" from its list of refusals. Nothing else in the method moves.

Preserved refusals, and why each stays:

- **`mipMapBias != 0f`** — bias only shifts which level the hardware selects, and
  the §5.3 conjunction already covers every level, so this refusal is now
  conservative beyond what soundness requires. It stays because relaxing it is a
  coverage decision with its own evidence burden, not part of this milestone.
  Recorded in §15 as **conservative deferred coverage**.
- **Trilinear**, refused via `TryMapFilterMode` — refused for **scope, not
  soundness**. An earlier draft of this design claimed that trilinear blends
  between levels and so no per-level conjunction can cover it. **That claim is
  withdrawn and was wrong:** trilinear interpolates between two selected levels,
  and if every contributing sample from both levels is exactly one, their
  interpolation is exactly one as well, so a conjunction over all levels would in
  fact cover it. Trilinear stays refused because the current `TextureSampling` and
  `AlphaFilterMode` vocabularies do not express it and the classifier admits no
  such mode; widening that vocabulary is outside this milestone. Recorded in §15 as
  **conservative deferred coverage**, alongside mip bias.
- **`anisoLevel > 1`** — the one genuinely required refusal of the three.
  Anisotropic sampling averages texels across an elongated footprint the classifier
  does not model at all, so no conjunction over levels covers it.
- **unsupported or unequal wrap modes** — unchanged.

What makes admitting mipmaps sound under `Bilinear` is that Unity's `Bilinear`
filters within the selected level and selects a level without blending, so "some
level, bilinear within it" is exactly the model the chain conjunction covers.

**This removal and the §5.3 conjunction land together or not at all.** Until the
conjunction exists, the `mipmapCount > 1` refusal is the only thing keeping the
classifier sound for mipmapped textures (§6.1 of the investigation demonstrates a
texture whose mip 0 alone proves an opacity that mip 1 refutes).

## 10. Capture reuse

**No texture-evidence cache is introduced** — no source-scoped, build-scoped, or
static store of captured alpha evidence, and no registry or service. (The §8.2
host-capability latch is not one: it holds a single boolean about this Editor
process's graphics stack, is keyed by nothing, and stores no texel, texture, or
source identity.) The existing deduplication in
`UnityMaterialEvidenceCapture.Capture` (`:674-721`) is the whole mechanism:

1. every assignment's texture is resolved to a `TextureSourceId` and registered in
   a batch-local `Dictionary<TextureSourceId, SharedTextureBuilder>`;
2. requested evidence kinds are unioned per source;
3. `CaptureTexture` runs **once per distinct source for the whole batch**;
4. the single resulting `CapturedTextureEvidence` is handed to every assignment of
   that source.

`TryCapture` — and therefore all GPU work — is reached only from `CaptureTexture`
(`:989-993`), so GPU capture runs exactly once per distinct `TextureSourceId` per
batch.

**The un-deduplicated fallback cannot cause repeated GPU work.** An assignment
whose texture has no resolvable source id takes the `texture.Shared == null` path
at `:749-755` and calls `CaptureTexture` per assignment — but `TryCapture` refuses
at gate 2 (`TryGetSourceId` fails) before allocating anything. An unidentified
texture is refused cheaply, every time.

**Honest limit.** Deduplication is **batch-scoped**, and batch breadth is the
caller's. `UnityMaterialSemantics.CaptureAlphaMaterials` batches all of a
renderer's materials into one `Capture` call, so a texture shared across a
renderer's slots is captured once; `TryAttestAlphaMaterial` uses a single-material
batch. A texture used by two renderers is therefore captured twice. That is
accepted for this milestone: a build-scoped texture-evidence cache is explicitly
out of scope, and
its correct key is still not knowable — `TextureSourceId` alone does not change
when the importer, the active build target, or a platform override changes. The
repeat cost is instead the evidence a future cache decision would need (§14).

### 10.1 A failed chain coexists with the texture's other facts

`CaptureTexture` (`:965-1005`) computes each fact independently: `hasSampling`,
`hasColorInterpretation`, `sampledAlphaIsOne`, `canonicalNormal`, and
`hasAlphaChannel` are separate booleans, each gated by its own
`TextureEvidenceKinds` flag and its own predicate. A `false` from `TryCapture`
sets `hasAlphaChannel = false` and `alphaChannel = null` and touches nothing else.

Downstream, `GatherAlphaFields` (`:575-597`) admits a source only when
`texture.HasSourceIdentity && texture.HasAlphaChannel`, so a texture with no chain
simply never enters the provider's dictionary; the provider returns `false` for it,
and `AlphaSemanticsResolver` refuses that one material with
`MissingTextureEvidence`. Materials whose alpha does not depend on that texture are
unaffected, and the texture's sampling and colour facts remain available to any
consumer that asked for them.

This is exactly the "unknown information invalidates only conclusions that depend
on it" rule, and it needs no new code — only the guarantee that the failure path
returns `false` rather than throwing.

## 11. NDMF lifecycle

**No NDMF phase or plugin change is required, and none is proposed.**

`AmusePlatformFinishPlugin.Configure` (`:91-101`) registers two passes in
`BuildPhase.PlatformFinish`, the second being `sequence.Run(BarrierPassName,
AmusePlatformFinishPass.Execute)`. NDMF passes are synchronous
`Action<BuildContext>` delegates invoked on the Unity main thread in the Editor;
there is no coroutine, task, or async contract to violate. Material evidence
capture already runs inside that pass, through
`UnityMaterialSemantics.CaptureAlphaMaterials` →
`UnityMaterialEvidenceCapture.Capture` → `CaptureTexture` → `TryCapture`. The GPU
work lands at the point that already exists.

`Graphics.Blit`, `RenderTexture.GetTemporary`, and
`AsyncGPUReadbackRequest.WaitForCompletion` are all main-thread Editor-legal, and
`WaitForCompletion` is what makes the capture synchronously complete before the
immutable evidence is handed onward — which the existing architecture requires,
since `CapturedTextureEvidence` is consumed after `Capture` returns and must hold
no pending work.

Two lifecycle facts worth stating rather than assuming:

- The pass reads the **build avatar's** materials. NDMF clones materials but not
  textures, so `TryGetSourceId` still resolves to the source texture asset. That is
  what the existing identity contract already relies on; nothing changes.
- The capture blocks the Editor main thread for the duration of the readback.
  §5.4 of the investigation observed 17-56 ms per 2K/4K chain, single-run and
  illustrative. This is build-time cost, not a correctness concern, and §14 says
  how it will be observed.

No mesh, submesh, material, importer, quality setting, streaming state, scene, or
prefab is written. The route reads GPU state through a temporary render target it
owns and releases.

## 12. Files expected to change

### Production — `Packages/com.alrauna.amuse/`

| File | Change |
| --- | --- |
| `Editor/Analysis/AlphaMipChain.cs` | **new** (+ `.meta`) |
| `Editor/Host/Shaders/AmuseAlphaExactOne.shader` | **new folder + moved asset** (+ `.meta`, GUID preserved) |
| `Editor/Analysis/AlphaSemanticsResolver.cs` | delegate type and contract; `AlphaResolution` field/ctor/`Classified`; `Classify` conjunction; `ResolveScaledSample` type |
| `Editor/Host/UnityAlphaFieldEvidence.cs` | dictionary and both signatures; `TryCapture` body replaced by the gated GPU route; the private acquisition core (§7.1); the host-capability latch and its check (§8.2); class doc |
| `Editor/Host/UnityMaterialEvidenceCapture.cs` | `CapturedTextureEvidence.AlphaChannel` type (`:263`), ctor parameter (`:274`), capture local (`:989`); shader-path constant if hosted here |
| `Editor/Host/UnityRendererAlphaAnalysis.cs` | provider lambda (`:503-510`), `GatherAlphaFields` (`:575-597`) |
| `Editor/Build/AmusePlatformFinishPlugin.cs` | `AlphaFields` local function (`:466-476`) |
| `Editor/Semantics/UnityTextureEvidence.cs` | `TryGetSampling` mip refusal removal + doc |
| `Editor/Analysis/AdmittedMaterialStates.cs` | comment only (`:175`) |

Unchanged and expected to stay unchanged: `TriangleAlphaClassifier.cs`,
`MaterialSemantics`, `package.json`, every `.asmdef`, and the plugin's phase
configuration.

### Research — `Packages/com.alrauna.amuse.research/`

| File | Change |
| --- | --- |
| `Tests/Editor/Calibration/AlphaExactOneProbe.shader` | **moved out** (+ `.meta`), §6.4 |
| `Tests/Editor/Calibration/AlphaEvidenceProbe.cs` | its shader-path literal is replaced by the production constant; **both** the predicate route and `TryCaptureRawAlphaDiagnostic` then load the moved production asset |
| `Tests/Editor/Calibration/AlphaEvidenceCharacterizationTests.cs` | reachability description only, if the probe's support text changes |

**Shader and `.meta` accounting.** Exactly **one** shader asset exists after this
change: `Editor/Host/Shaders/AmuseAlphaExactOne.shader`, moved from the research
package. No shader is created and none is deleted. Expected `.meta` changes are
**three new** — `Editor/Analysis/AlphaMipChain.cs.meta`, `Editor/Host/Shaders.meta`
for the new folder, and `Tests/Editor/Analysis/AlphaMipChainTests.cs.meta` — plus
**one moved**, the shader's own `.meta`, carrying GUID
`85ccb222632d847b6b653f0e05b1ee97` with it.

### Tests — `Packages/com.alrauna.amuse/Tests/Editor/`

| File | Change |
| --- | --- |
| `Analysis/AlphaMipChainTests.cs` | **new** |
| `Analysis/AlphaSemanticsResolverTests.cs` | every provider lambda (`:53-66, :213, :282, :459, :575`); new aggregation cases |
| `Analysis/AdmittedMaterialStatesTests.cs` | provider lambda (`:475`), fixture (`:1189`) |
| `Host/UnityAlphaFieldEvidenceTests.cs` | largely rewritten for the GPU route and its gates |
| `Host/AlphaEvidenceClassifierIntegrationTests.cs` | chain-shaped evidence; new lower-mip case |
| `Host/UnityMaterialEvidenceCaptureTests.cs` | `AlphaChannel` assertions (`:356-395`); shared-capture case |
| `Host/UnityRendererAlphaAnalysisTests.cs`, `Host/RendererAlphaAnalysisIntegrationTests.cs` | provider and gather types; lower-mip integration case |
| `Semantics/UnityTextureEvidenceTests.cs` | `TryGetSampling_MipmappedTexture_IsRefused` inverts to an admission case; bias/aniso/trilinear/wrap cases unchanged |
| `Build/AmusePlatformFinishPluginTests.cs` | only if it constructs provider-shaped values |
| `Analysis/TriangleAlphaClassifierTests.cs` | **unchanged** — its remaining green state is the evidence that the classifier did not change |

## 13. Test strategy

RED first for every behaviour that is **actually constructible**. Where a branch
cannot be reached without synthesizing a Unity failure, this section says so
explicitly rather than promising coverage it cannot deliver.

Two rules bound what may be added:

- **Narrow pure helpers are allowed**, and are the preferred shape for validation
  logic: target and request dimension checks, the exact target-format comparison,
  the byte count, the binary-byte scan, the capability facts behind gates 8-10, the
  declared-state facts behind gates 5-6, and the §8.2 orientation pattern
  comparison. Each is acceptable **only because production calls that same
  helper** — the helper *is* the production check, not a parallel restatement of
  it. A helper that production stopped calling would be a test-only seam and would
  have to go.
- **No injectable GPU backend, interface, factory, seam, or test-only production
  framework** may be introduced in order to synthesize Unity failures. A branch
  reachable only by faking Unity is left unautomated and reported (§13.5, §13.6).

Real integration coverage — meaning tests that drive actual Unity textures through
the actual production path — is expected for: successful `R8_UNorm` acquisition;
every admitted format; non-readable compressed multi-mip evidence; orientation;
full-chain capture; the publicly reachable refusal gates; and restoration of
observable state.

### 13.1 `AlphaMipChain` invariants

| Case | Falsifies |
| --- | --- |
| null list throws | missing guard |
| **empty list throws** | the vacuous-`ProvenOpaque` defect — the single most dangerous value |
| null element throws, index named | a hole that would `NullReferenceException` deep in `Classify` |
| `8x8 -> 4x4 -> 2x2 -> 1x1` accepted | over-strict shape rule |
| `16x4 -> 8x2 -> 4x1 -> 2x1 -> 1x1` accepted | per-axis halving implemented as a single shared shift |
| `8x8 -> 4x4 -> 4x4` rejected | "no larger than previous" written instead of exact halving |
| `8x8 -> 2x2` rejected | a skipped level |
| `4x4 -> 8x8` rejected | reversed order |
| single-element chain accepted | an implementation that demands more than one level |
| truncated chain (`8x8 -> 4x4`) accepted | an invented "must reach 1x1" invariant (§4.4) |
| mutating the caller's list after construction does not change the chain | missing defensive copy |

### 13.2 Aggregation

Through `AlphaResolution.Classified(chain, sampling).Classify(triangle)`:

| Case | Falsifies |
| --- | --- |
| single-mip chain matches today's single-grid outcome exactly | a regression in the ordinary case |
| every level opaque → `ProvenOpaque` | inverted conjunction |
| mip 0 opaque, mip 1 transparent → `MustRemainTransparent` | mip-0-only proof — the defect this milestone exists to fix |
| mip 0 transparent, mip 1 opaque → `MustRemainTransparent` | "last level wins" |
| one level `Unknown`, one `MustRemainTransparent`, transparent **last** → `MustRemainTransparent` | **early exit on `Unknown`** (§5.3) |
| one level `Unknown`, rest opaque → `Unknown` | `Unknown` swallowed by an all-opaque test |
| every level `Unknown` → `Unknown` | defaulting to the zero enum value `ProvenOpaque` |
| a chain whose levels disagree is **not** reported by `TryGetUniformOutcome` | re-describing classified evidence as uniform (§5.4) |
| a chain whose levels all agree is **still not** reported as uniform | the same, on the tempting-but-rejected optimization |
| `k < 1` scaled sample with a valid chain → `Uniform(MustRemainTransparent)`, contents unread | the attestation being dropped, or bytes being read |
| `k < 1` scaled sample with provider refusing → `MissingTextureEvidence` | the attestation requirement being dropped entirely |

### 13.3 Acquisition, through the real production R8 path

| Case | Falsifies |
| --- | --- |
| each admitted format — `RGBA32`, `ARGB32`, `Alpha8`, `RGB24`, `DXT5`, `BC7` — separates maximum from submaximum alpha | a format admitted without evidence |
| **non-readable `DXT5`** whose mip 0 is exactly one where a lower mip is not | the whole milestone's premise, on the format that dominates real avatars |
| non-square chain preserves bottom-to-top row-major placement of a zero column and an isolated texel | a flip or transpose |
| `chain.Count == texture.mipmapCount` on a real multi-mip texture | a chain silently truncated — the ordinary-truncation risk §4.4 leaves to the capture loop rather than the type (§13.5) |
| the real production shader asset loads at its production path and reports `isSupported` | a broken path constant, a botched move, or a shader that does not compile on this host |
| the §8.2 host-capability check **passes on this host** — its asymmetric non-square fixture round-trips through the acquisition core with exactly the expected pattern | a vertical flip, horizontal mirror, transpose, or width/height swap in the production route here |

Resource observations belong to §13.6 and are not repeated here.

### 13.4 Refusals

**Constructible through the real `TryCapture` entry point.** Each asserts `false`
with no chain. Every fixture is a plain in-memory allocation requiring no importer,
no project mutation, and producing no Unity console error:

| Case | Fixture |
| --- | --- |
| unresolvable source id | any in-memory `Texture2D` — it has no asset identity |
| destroyed texture | an allocated texture passed after `DestroyImmediate` |
| null texture | `null` |
| not a `Texture2D` | a `RenderTexture` or `Cubemap` |
| representative refused format | `ARGB4444`, and one float format — both allocatable directly with `new Texture2D(...)` |

**Representative is deliberate.** The complete allowlist/refusal policy is covered
exhaustively below, through the production-called `TextureFormat` predicate, so
nothing is gained by manufacturing an ASTC or `DXT5Crunched` fixture merely to
watch an enum comparison reject it before any GPU work happens. An earlier draft of
this design claimed the merged research fixtures already exercise those two
formats; **that claim is withdrawn and was wrong.** The investigation records
`DXT5Crunched` as measured in an earlier turn but explicitly *not* durably
exercised through the R8 path, and ASTC as never characterized at all.

**Covered by production-called pure predicates**, because the deciding facts cannot
safely be induced on a conforming host or without mutating project state. Each is
tested at every relevant combination, with a companion case asserting that the
production overload reads exactly those facts, so the predicate cannot drift from
the rule it states:

| Branch | Predicate over | Why the Unity state is not induced |
| --- | --- | --- |
| gate 3 | the active `BuildTarget` value | switching the active build target is a project-state mutation this design does not sanction in a test |
| gate 4 | the **complete** `TextureFormat` policy — every admitted value admitted, and every refused value named in §8 refused, including all float formats, `DXT5Crunched`, `ARGB4444`, ASTC, ETC/EAC, PVRTC and BC1/BC4/BC5 | exhaustive coverage of an enum policy needs no texture at all, and risky compressed fixtures buy nothing |
| gates 5-6 | `(activeMipmapLimit, streamingMipmaps)` | a runtime texture cannot be given a nonzero limit or streaming state without mutating project or importer state, which production must never do |
| gate 7 | `(width, height, mipmapCount)` positivity | a zero-sized or zero-mip `Texture2D` is **not** claimed to be constructible; the gate predicate is tested directly instead |
| gates 8-10 | `(supportsAsyncGPUReadback, r8Renderable, r8Readable, sourceSampleable)` | host device facts that cannot be forced false on a conforming host |
| gate 11 | `(shaderAssetLoaded, shaderIsSupported)` | the shader is **not** moved, renamed, deleted, or replaced to manufacture failure; §13.3 separately asserts that the real production asset loads and reports supported |
| gate 12's decision | the expected orientation pattern against an actual byte buffer — matching, vertically flipped, horizontally mirrored, transposed, and dimension-swapped | a host that genuinely fails orientation cannot be summoned; this validator is what decides the gate, and §13.3 asserts the real check passes here |

These predicates are **not** test-only seams: production is their caller, and each
predicate *is* the gate rather than a copy of it. This follows the convention the
merged research probe already established.

**Structural, not induced.** A real gate-12 *failure*, and the sticky-`false`
behaviour by which it refuses every subsequent capture for the rest of the
AppDomain, both require a host whose graphics stack actually fails the check. **No
test-only latch setter, reflection mutation, injectable GPU backend, or override
hook is added to simulate one.** The guarantee is control flow: the latch is
assigned exactly once from the check's result, and gate 12 reads it before every
capture. Review confirms both; no test induces either.

The channel refusal is asserted where it lives, on `TryGetAlphaField`:
`channel != TextureChannel.Alpha` returns `false` with no chain, while an
uninitialized source still throws `ArgumentException` and an undefined channel
still throws `ArgumentOutOfRangeException` — a malformed argument is a caller
defect, not a refusal, and that distinction must not be lost in the rewrite.

### 13.5 Malformed GPU results — what is automated and what is not

None of these conditions can be provoked on a conforming host without faking Unity,
and this design forbids the seam that would allow it. They are covered at the level
that is honest — the pure validation helpers of §7.2, each of which production
calls:

| Condition | Coverage |
| --- | --- |
| target format substituted | the exact `GraphicsFormat` comparison is unit-tested over matching and mismatching values. The Unity-side substitution itself is **not automated**. |
| readback buffer of the wrong length | pure helper over `(expectedWidth, expectedHeight, actualLength)`, tested at the boundary. The Unity-side condition is **not automated**. |
| readback dimensions disagreeing with the request | pure helper over the four integers. **Not automated** at the Unity level. |
| a byte that is neither `0` nor `255` | pure helper over a byte buffer, tested with `0`, `255`, `1`, `128`, and `254`. The Unity-side condition is **not automated**. |
| one level fails, so no chain is produced | **structural control flow, not synthesized** — and *not* a constructor guarantee. See below. |

A defect in any of these comparisons is caught even though the Unity condition that
would trigger it cannot be staged. That is the whole value of the helper shape, and
it is the reason these are helpers rather than inline expressions.

**Where the no-partial-chain guarantee actually lives.** An earlier draft claimed
`AlphaMipChain`'s constructor "rejects every shape a partial chain could take."
**That is false and contradicted §4.4 and §13.1**, which intentionally *accept* a
correctly shaped prefix such as `8x8 -> 4x4`. The corrected account:

- `AlphaMipChain` guarantees **non-empty ordered shape only** — non-empty, mip 0
  first, no nulls, per-axis halving. It cannot prove completeness, has no access to
  `mipmapCount`, and deliberately accepts a correctly shaped prefix (§4.4).
- **Completeness belongs exclusively to the provider contract (§5.1) and the
  capture loop.** Production constructs the chain only after exactly
  `texture.mipmapCount` successful captures; any level failure returns before the
  constructor is ever reached.
- **Ordinary truncation** — the realistic defect, a loop that stops early or only
  ever captures mip 0 — is caught by the §13.3 integration assertion that
  `chain.Count == texture.mipmapCount` on a real multi-mip texture.
- **Mid-loop failure producing no partial result** remains a **structural
  control-flow guarantee**, confirmed by review, unless a safe real failure can be
  constructed. No test fakes a failed level, and the constructor must never be
  described as providing this.

### 13.6 Resources — executed assertions versus structural guarantees

**Executed assertions**, limited to what is genuinely observable:

- `RenderTexture.active` equals its prior value after a **successful ordinary
  capture**;
- `RenderTexture.active` equals its prior value after a **successful
  host-capability check** (§8.2), which allocates and releases through the same
  acquisition core.

That list is short on purpose. An earlier draft also promised "no temporary render
texture remains held by this code" after a capture. **That is withdrawn:**
`RenderTexture.GetTemporary` draws from a Unity-managed pool, and this repository
has established no reliable observation of that pool's retained contents or of
ownership within it. An assertion about pool residency would test an unverified
model of Unity rather than AMUSE.

**No resource-leak test is written for a gated refusal.** Gates 1-11 all precede
the caller's first `GetTemporary`, so there is nothing allocated to leak; the
protection is gate ordering, and §13.4 already covers that those gates refuse.

**Structural guarantees — reviewed, not dynamically induced:**

- release of the temporary render texture and destruction of the `Material` after a
  **successful** capture;
- the same after a **readback failure**;
- the same after an **exception thrown between allocation and return**.

No deterministic, safe, real trigger was identified for the latter two. A readback
error requires a device or resource state this design gates out before the call; a
post-allocation exception requires either faking Unity or destroying the texture
mid-capture, which is neither deterministic nor safe. Automating either would
require the injectable GPU backend this design forbids, and that seam costs more
than the assertions are worth.

For all three the guarantee is `finally` placement:
`RenderTexture.ReleaseTemporary`, `Object.DestroyImmediate`, and the
`RenderTexture.active` restore sit in `finally` blocks that no return path and no
throw path can bypass (§7.3, §7.4). **Review must confirm that placement, because
no test will.**

These are recorded as unautomated branches rather than papered over.

### 13.7 Seam propagation and integration

| Case | Falsifies |
| --- | --- |
| `TryGetSampling` admits a mipmapped texture | the blanket refusal surviving |
| `TryGetSampling` still refuses nonzero bias, `anisoLevel > 1`, Trilinear, unequal wrap | **over-deletion** — the most likely mistake in §9 |
| a texture shared by several material slots is captured **once** across one `Capture` batch | dedup broken by the heavier capture |
| a texture whose alpha capture fails still reports its sampling and colour facts | refusal widening (§10.1) |
| renderer analysis: a lower mip prevents an otherwise mip-0-only `ProvenOpaque` | the conjunction not reaching the renderer path |
| build handoff: the same, through `AmusePlatformFinishPlugin` | the conjunction not reaching the build path |

### 13.8 Regression surface

The existing single-mip, runtime-state, Poiyomi, lilToon, classifier, and full
EditMode suites must remain green, and `TriangleAlphaClassifierTests.cs` must pass
**unmodified** — that is the evidence that the classifier and `AlphaTextureData`
genuinely did not change.

## 14. Observing cumulative multi-mip cost

**No new budget is proposed, and no new metric, counter, telemetry type, or
reporting surface is introduced.** The investigation withdrew its `4/3` cost
estimate as unjustified, and nothing has replaced it with evidence.

`TriangleAlphaClassifier.MaxSupportRegions` continues to apply **per grid**,
unchanged and unmoved. Whether the existing per-grid budget suffices in aggregate
is an open question that implementation must answer with observation, not with a
guess encoded as a constant.

Two mitigations already exist in the code and should be relied on before anything
new is contemplated:

- `TriangleAlphaClassifier.cs:201-206` short-circuits on `IsFullyOpaque` /
  `IsFullyNonOpaque` before any geometry work. Lower mips of a uniform texture —
  the common case, since averaging drives levels toward uniformity — cost O(1)
  each.
- The §5.3 early exit on `MustRemainTransparent` truncates the loop at the first
  counterexample.

How it will be observed during implementation, without new infrastructure:

1. record wall-clock for the existing renderer and build integration EditMode
   suites before and after the change, from the same runner;
2. if that shows pressure, add a **throwaway** probe that counts classifier
   invocations per triangle across a representative chain, record question,
   preconditions, result, what it proves and what it does not, and **remove the
   probe** once the conclusion is captured — the characterization discipline
   `CLAUDE.md` already requires;
3. report the observation with the branch. If it shows a real problem, that is a
   finding for controller review, not a licence to invent a budget inside this
   milestone.

## 15. Scope exclusions

Not in this design: mesh or submesh mutation; generated opaque materials; any
render-state conversion change (that capability is merged and is not reopened);
vertical-slice orchestration; NDMF phase or plugin changes; lilToon- or
Poiyomi-specific texture rules; assigned Poiyomi `_AlphaMask` semantic support;
any texture importer mutation; Android/Quest; `DXT5Crunched`; a universal texture,
material, or shader IR; a generic GPU extraction framework; any source- or
build-scoped texture-evidence cache; a new planner; RGB or any non-alpha channel;
trilinear, anisotropic, derivative, LOD, or streaming sampling semantics; and any
Census Lab work.

Also deliberately not done, though adjacent:

- **`ResolveScaledSample` is not given a chain-aware fast path.** It reads no
  contents today and will read none after.
- **The instance half of `UnityAlphaFieldEvidence` is not deleted**, despite
  having no production consumer (§3). Removing a documented `AlphaFieldProvider`
  implementation is a separate decision with its own test consequences.
- **`mipMapBias != 0f` is not relaxed**, though §9 shows the conjunction now
  covers it. Conservative deferred coverage — a coverage defect worth reporting,
  not fixing here.
- **Trilinear is not admitted**, though §9 withdraws the claim that a per-level
  conjunction cannot cover it. Admitting it requires widening the
  `TextureSampling` / `AlphaFilterMode` vocabulary and the classifier's admitted
  modes, which is a separate milestone. Conservative deferred coverage, recorded
  alongside mip bias.
- **The §8.2 latch is not generalized.** It stays one nullable static and one
  private method, and is never turned into a capability service, a registry, or an
  NDMF build-state object.

## 16. What this supersedes in the 2026-08-19 design

`docs/superpowers/specs/2026-08-19-texture-alpha-evidence-design.md` is historical
evidence. It is **not edited retroactively**. The following of its conclusions no
longer describe AMUSE:

| 2026-08-19 conclusion | Superseded by |
| --- | --- |
| Read route is `Texture2D.GetPixels32()` on the imported texture | GPU texel-fetch predicate into an `R8_UNorm` target (§7). `GetPixels32` fabricates opacity on compressed input. |
| "New immutable evidence type: **None**" | `AlphaMipChain` (§4) |
| "New seam / intermediate: **None.** The delegate is the seam." | The delegate remains the seam, but its **returned value changes type**, propagating through six seams (§5) |
| "Change to `AlphaSemanticsResolver`: **None**" | `AlphaResolution` carries a chain and performs the conjunction (§5.2-5.3) |
| "Change to `UnityTextureEvidence`: **None.** No sixth method." | `TryGetSampling` loses its `mipmapCount > 1` refusal (§9). Still no sixth method. |
| "New production file: **One**" | One new production source file, plus a moved shader asset and one new folder (§12) |
| Admitted formats `RGBA32`, `ARGB32`, `Alpha8`, `RGB24` | Those four plus `DXT5` and `BC7` (§8) |
| Non-readable textures deferred, pending a route that decodes the source asset | No such route is needed. `isReadable` governs the CPU copy; GPU readback reads the GPU resource. |
| Compressed formats "measured to be a false-opaque source; likely permanently refused for exact proof" | That was a property of Unity's **CPU decoder**, not of the formats. Through the GPU, `DXT5` and `BC7` decode a uniform source `254` to `254/255`. |
| Single-mip evidence sufficient | Unsound for any mipmapped texture (§5.3, and §6.1 of the investigation) |
| "16-bit / float alpha: requires `GetPixels()`/`GetPixelData` and an exact `== 1f` test" | Float formats are refused for a **different and stronger** reason: one predicate bit cannot distinguish a legitimate below-one value from `2.0`, `-1.0`, `NaN`, or `+Inf`, so the format cannot supply the finite-and-`[0,1]` attestation. A UNorm format supplies it structurally. |

Its no-cache decision, its refusal to open a `TextureImporter` in the producer, its
alpha-only channel scope, its malformed-versus-unsupported convention, and its
`Editor/Host/` placement all **stand**.

## 17. Unresolved risks after this milestone

1. **The analysis GPU is not the playback GPU.** Every measurement is Apple M2 /
   Metal. `BC7` decompression is specified bit-accurate; `BC3` alpha is an exact
   integer scheme by specification, but that is a reading rather than a
   cross-vendor measurement. Unresolved for any non-Metal GPU.
2. **Row order is measured on one graphics API**, which is why the §8.2
   host-capability check is part of this design. The build-target gate does not
   constrain the editor's graphics API, so the check reduces this from an
   unverified assumption to a checked precondition **on the host that runs the
   build**, whose failure mode is refusal. What remains is narrower and must not be
   overstated: the check attests orientation and binary `R8_UNorm` encoding on that
   host, not the decode or swizzle behaviour of each admitted compressed format
   (risk 1).
3. **Mipmap streaming is refused, never handled.** What a `Load` of a non-resident
   level returns was never provoked, and no read-only signal is known to
   distinguish a resident level from an evicted one.
4. **Nonzero `activeMipmapLimit` is refused** and its branch could not be
   constructed in memory without mutating project state, so it is covered only as
   a pure predicate.
5. **Only the active build target's import is observable.** A proof obtained under
   `StandaloneWindows64` says nothing about the Android variant of the same asset.
6. **Cumulative multi-mip classification cost is unmeasured** (§14).
7. **Deduplication is batch-scoped** (§10), so a texture shared across renderers is
   captured more than once per build.
8. **Texel-fetch transfer and swizzle semantics vary across graphics APIs**, which
   is why RGB channels stay out of scope and why alpha-only support rests on
   "alpha is the only channel characterized", not on texel fetch being known
   transfer-free.
9. **The §8.2 latch is evaluated once per AppDomain.** A graphics-stack change
   within a single Editor process would not re-trigger it. Unity does not change
   graphics API without a restart, which clears the AppDomain, so this is judged
   safe — but it is a judgement, not a measurement.
10. **Two failure branches are structurally guaranteed rather than tested** —
    cleanup after a readback error and after a post-allocation exception (§13.6).
    Their protection is `finally` placement confirmed by review.
