# Census Lab refusal audit: one Poiyomi-heavy avatar, NDMF console accuracy

Date: 2026-10-06. Branch: `investigation/poiyomi-refusal-audit`. Base: `main` at `96b5705`.

Privacy note: this record is sanitized. It names no private avatar, renderer,
material, texture, scene, asset folder, lock hash, or instance identity. The
avatar is "the Census Lab avatar under test". Materials are named by role.
Exact triangle totals are replaced by ranges. Aggregate report-entry counts
are stated as observed counts. Shader names are public vendor identifiers and
are kept exact.

Labels: `[SOURCE]` is a fact read at cited lines in this tree.
`[MEASURED]` is a fact observed in the live Census Lab editor instance during
this investigation. `[INFERENCE]` is a conclusion. `[DECISION NEEDED]` is a
choice for the user.

## 1. Verdict

**Every refusal on this avatar was fail-closed correct. The main Poiyomi body
converted. The console text is mostly accurate, with four text defects, two
under-informative families, and one scope concern.** No false positive
(conversion of a non-opaque triangle) was found or is plausibly reachable from
the observed refusal set. `[MEASURED]`

Top line of the build: of the renderers present at PlatformFinish (under 40,
after upstream auto-merge collapsed about 30 authored renderers), 7 finished
classification with writes, roughly 120-125 thousand triangles moved to
AMUSE-generated opaque materials, and 31 renderers kept everything original.
Each build emitted 63 AMUSE entries, all Information severity. `[MEASURED]`

## 2. Method and instruments

The user's original run was an NDMF Apply-on-Play build. Exiting play mode
and the domain reload wiped the in-memory NDMF error reports, so the run was
reproduced: the avatar prefab (placed by the user in the Census Lab corpus
prefab folder) was built through NDMF manual bake, which crosses the same
admitted lifecycle gate (`HostLifecycleCapability.cs:15-20`, `:242-244`).
Seven full builds exist in today's editor log: the user's earlier runs and
the reproductions. The last six in-memory NDMF reports are byte-identical,
so the reproduction is deterministic. `[MEASURED]`

Evidence channels: NDMF `ErrorReport.Reports` read by reflection (keys,
substitution arguments, context references), NDMF's own warning-log line per
report (`nadena.dev.ndmf/Editor/ErrorReporting/ErrorReport.cs:164`), and
read-only editor reads of the refusing materials, textures, and the baked
clone. The avatar's opt-in component was present with the alpha separator
enabled; no consent dialog fired in any run. `[MEASURED]`

Instrument notes, both `[MEASURED]`:

- Two Unity editor instances were reachable this session. One early probe
  routed to the wrong project instance before pinning. Every later call
  carried an explicit `Application.dataPath` guard. No write ran against the
  wrong instance; the misrouted calls were read-only probes.
- A manual bake that exceeds the editor-bridge reply timeout is re-delivered
  by the bridge, so one request produced two full builds. Harmless here (the
  second run doubled as a determinism check) but worth remembering for Lab
  automation.

## 3. The refusal inventory (per build, aggregate)

| Family | Entries | Cause values | Ground truth |
|---|---|---|---|
| Slot analysis refusals | 22 | `AdmittedMaterialSemanticsUnknown` x21, `LockedPoiyomiOriginalShaderUnattested` x1 | unsupported shader families; one Poiyomi alpha-mask gap; one locked old-Poiyomi material |
| Renderer-level refusals | 22 | mirrors the slot causes | one entry per refused renderer |
| Animation-closure refusals | 5 | `MaterialDependencyClosureFailed` | the body-skinned mesh plus four camera-gadget compute quads |
| Slot separation refusals | 4 | `OpaqueConversionRefused` | four slots on one upstream auto-merged mesh |
| Texture capture refusals | 4 | `UnsupportedFormat` | crunched BC1 textures on two slots of that mesh |
| Summary | 1 | `amuse.summary.Title` | see section 4.6 |

Unsupported shader families named by the reports: a gem variant outside the
attested lilToon family; four camera-gadget system shaders; Unity `Standard`;
`VRChat/Mobile/Standard Lite`; `.poiyomi/Old Versions/9.0/Poiyomi Toon`
(locked and unlocked forms). `[MEASURED]`

## 4. Accuracy findings, by family

### 4.1 The Poiyomi `_AlphaMaskValue` refusal: correct, and the named culprit is real

Four renderers share one gradation-mask material: current Poiyomi, mask
texture assigned, `_AlphaMaskValue = 0.4`, mask strength 1 (strength verified
live this session, `[MEASURED]`), transparent mode. The pair (strength 1,
value 0.4) on a bound mask falls into the final branch of
`TryInterpretAlphaMask`, which admits only (1, 0), (1, value >= 1), and the
unbound-mask constants (`PoiyomiMaterialSemantics.cs:1508-1835`, final
refusal `:1822-1834`). The refusal is fail-closed correct: proving
`saturate(red + 0.4) = 1` needs a per-texel predicate the deferred envelope
contract has not admitted. `[SOURCE]`

This is not the 2026-10-04 property-name trap: every read before the refusal
succeeded, each failure mode names its own property, and the value outside
`{0} ∪ [1,∞)` is the genuine blocking fact. lilToon admits every assigned
mask pair through the mapped-sample envelope (`LilToonAlphaMaskSemantics.cs:328-332`,
`LilToonAlphaInterpreter.cs:334-358`); Poiyomi parity is the documented
deferral in-line (`PoiyomiMaterialSemantics.cs:1822-1826`) and in the
2026-09-27 record. Red-channel evidence is already captured for these
materials, so the machinery gap is the admission rule alone. Classification:
coverage defect by policy (unnecessary refusal), deliberate and dated.
`[SOURCE]`

Text: UNDER-INFORMATIVE. The sentence is truthful, but `_AlphaMaskValue`
has no entry in the Poiyomi feature-label map, so the text says "a shader
feature" instead of "the alpha mask" (`PoiyomiMaterialSemantics.cs:983-996`,
`AmuseReports.cs:150-160`); the lilToon side labels the same property.
`[SOURCE]`

### 4.2 Locked and unlocked old-Poiyomi: refusals correct; one misleading title

The locked material's lock tag records the original shader as old 9.0 Poiyomi
`[MEASURED]`. Lock classification needs both the `Hidden/Locked/` name and
the optimizer flag; the tag is read, the original is resolved, and the
pinned-identity conjunction refuses it (exact name, GUID, package version,
normalized hash: `LockedMaterialIdentity.cs:92-155`, `:236-252`;
`PoiyomiMaterialSemantics.cs:2191-2271`, admitted identities only current
Poiyomi and its two-pass variant). Fail-closed correct. The unlocked sibling
using old 9.0 directly refuses as a plain unsupported shader, also correct
(`UnityMaterialSemantics.cs:685-798`). Neither produces a transfer-consent
subject, because unsupported families have no verified version to transfer
from (`UnityMaterialSemantics.cs:473-503`) — matching the zero consent
dialogs observed. `[SOURCE]`

Text defects, both `[SOURCE]`:

- The renderer-level title "This renderer holds a locked material AMUSE
  cannot trace." (`AmuseReportStrings.cs:172-173`) is MISLEADING for this
  cause: the trace succeeded (the tag names the original), and the blocker
  is attestation of the traced original. The description's second disjunct
  and the hint state the true cause (`:174-181`). A title reader would try
  to re-lock the material instead of installing the recorded shader version.
- The slot-level sentence is UNDER-INFORMATIVE: it prints the locked
  generated shader name and the camelCase cause token, never the
  lock/original/attestation mechanism.

Counterfactual, `[INFERENCE]` with cited gates: the unlocked old-9.0 material
is opaque-preset, queue 2000, no mask; identical values on attested current
Poiyomi resolve `AlreadyOpaque` with alpha exactly 1 and emit no entry
(`PoiyomiOpaqueConversion.cs:175-176`, `:296-300`, `:400-410`). The refusal
is purely the version gate — a coverage artifact, not a semantic wall.

### 4.3 The `<missing>` offending material on conversion refusals: live defect

Four slots on one upstream auto-merged mesh refused `OpaqueConversionRefused`
and printed `<missing>` for the offending material. Root cause is
code-closed: the preparation emitter passes three arguments only —
`SlotSeparationRefusal(renderer, slotIndex, slotRefusal)`
(`AlphaSeparationPreparation.cs:428-431`, verified verbatim this session) —
so `offendingMaterial` defaults to null (`AmuseReports.cs:215`), the
registry resolve short-circuits on null
(`RegisteredSourceIdentity.cs:20-22`), and NDMF renders a managed null as
`<missing>` (`InlineError.cs:30-32`). The refused material was in scope at
the call site, and the transient-unlock clone was registered with the object
registry (`TransientUnlockSwapIn.cs:252-254`), so the existing resolver
(`AmuseReports.cs:236-243`) would have named the authoring asset had it been
passed. The 2026-09-28 `<missing>` fix covered the renderer-name argument
only (`AmuseReports.cs:225-234`); no material fallback exists. DEFECT —
emitter-local fix: pass the refused slot's live material. `[SOURCE]`

The detail text is also UNDER-INFORMATIVE: seven conversion-refusal arms
collapse into the single string `OpaqueConversionRefused`
(`AlphaSeparationRecords.cs:40-44`; arms at
`AlphaSeparationPreparation.cs:655-857`), and the internally available
per-gate refusal is discarded at the emitter. Which arm these four slots hit
is unresolved `[INFERENCE]`; the most plausible is per-material Poiyomi
eligibility, since sibling slots sharing the same attested shader converted.

### 4.4 "Could not read this renderer's animations": misleading title, no name, broad scope

Five entries, `MaterialDependencyClosureFailed`, including the body-skinned
mesh — the single largest coverage loss on this avatar. Closure fails in
exactly four ways (`CapturedAnimationEvidence.cs:8-14`; gate at
`AmusePlatformFinishPlugin.cs:1116-1121`): an unassigned current slot, an
out-of-range swap binding (tolerance off), an invalid swap keyframe value,
or the all-or-nothing material capture refusing. Of these, only the invalid
swap value is accurately described by "could not read this renderer's
animations"; two ways are not animation-read failures at all, and the
description asserts a material swap exists even for the unassigned-slot way
(`AmuseReportStrings.cs:33-36`). MISLEADING. For the body mesh the plausible
way is invalid swap keyframes from a full-controller integration whose swap
clips reference transient assets `[INFERENCE]`; for the camera-gadget quads,
invalid swap values or unassigned slots `[INFERENCE]`. `[SOURCE]`

Presentation: UNDER-INFORMATIVE. The entry text carries no renderer name
(the templates have no name placeholder; both count phrases render as
"an unknown number of material slots" because geometry extraction never
ran, `AmusePlatformFinishPlugin.cs:715-721`). Identity rides only on the
context reference, which dies with the build copy after a play-mode run —
the exact durability argument the 2026-09-28 record established for names
in text. `[SOURCE]`

Scope: the outcome is safe (renderer keeps originals), but one slot-local
fact forfeits the whole renderer's per-slot analysis — including slots whose
current materials are attested Poiyomi that would otherwise classify
(`AmusePlatformFinishPlugin.cs:694-707`, `:776-787`, empty slot results at
`:1288-1295`). That is broader than the codebase's own blast-radius principle
("a fact about a slot is never about the renderer",
`UnityAnimationEvidenceCapture.cs:607-611`); only the capturer-failure way
is inherently renderer-scoped. `[SOURCE]`

### 4.5 Texture-format refusals: correct gate, empty text, one false outcome sentence

All four refusals are crunched BC1 (`DXT1Crunched`) textures on the two
bodysuit-UV slots of the auto-merged mesh — main textures read via Alpha,
mask copies via Red. The closed allowlist admits exactly RGBA32, ARGB32,
Alpha8, RGB24, DXT1, DXT5, BC7 (`UnityAlphaFieldEvidence.cs:835-849`);
crunched is excluded pending durable characterization (`:827-828`). Both
Alpha-channel refusals are sound-by-contract; the Red-channel refusals are
conservative rather than necessary (BC1 red is technically recoverable) and
are the clearest future-admission candidate. No plain-DXT1 fact refused; a
plain-DXT1 mask on the other merged mesh captured cleanly, and BC7 mains on
converted slots passed. `[SOURCE]` `[MEASURED]`

Text: UNDER-INFORMATIVE — neither the format nor the texture name appears
anywhere in the entry, and the reason argument repeats the title. This is
the never-implemented option-four residual from the 2026-10-04 record; the
hint enumerates re-import targets but omits admitted DXT1
(`AmuseReportStrings.cs:351-367`). `[SOURCE]`

One outcome sentence is MISLEADING: standalone texture entries fire only for
slots that resolved (`AmusePlatformFinishPlugin.cs:728-747`), and the
description claims the sampling triangles "stay on the original material"
(`AmuseReportStrings.cs:354-360`) — yet one of the two slots demonstrably
converted to an AMUSE opaque material this build, its proof completing
through a route that needed no refused chain `[MEASURED]`, consistent with
the sampled-alpha-is-one route (`UnityTextureEvidence.cs:196-217`)
`[INFERENCE]`. `[SOURCE]`

### 4.6 Summary honesty and run-to-run variance

The summary's numbers are honest by construction: untouched = analyzed +
semantically refused − applied, so refused renderers cannot hide
(`AmusePlatformFinishPlugin.cs:48-60`), and every renderer terminates in an
analyzed or refused outcome with an entry. ACCURATE, with under-informative
wording: "analyzed" means finished classification, "kept everything
original" conflates proven-fine with refused, and the summary does not say
its population is the upstream-merged build-copy renderer set, so 7 + 31 =
under 40 looks like a hole against the about-70 authored census.
`[SOURCE]` `[MEASURED]`

The user's first run differed: five ear/tail renderers existed separately
and reported slot refusals, and one offender path pointed into a transient
build asset folder of a full-controller integration. Later runs merged those
renderers into the auto-merged meshes and re-reported the same facts under
merged identities. The upstream auto-merge is composition-sensitive
(eligibility and categorization read live material and animation facts,
`AutoMergeSkinnedMesh.cs:74-186`), so per-run composition explains the shift
`[INFERENCE]`. AMUSE reported exactly what each run's PlatformFinish saw;
the last six builds are byte-identical. The transient-path offender is
technically true of the build copy, absent from authoring — the documented
hardening finding-3 limitation; the registry remedy cannot fire because that
producer registers nothing. `[SOURCE]` `[MEASURED]`

## 5. Confirmed-correct behaviors worth keeping

- Fail-closed direction on every observed refusal; nothing unproven moved.
- The `_AlphaMaskValue` attribution is the real blocking fact, not the
  property-name trap.
- Zero consent subjects was correct for this avatar's shader set.
- Per-slot entries fold their capture refusals; no duplicate-entry families
  from the 2026-09-29 record reappeared.
- The 2026-09-27 summary fix and the 2026-09-28 renderer-name fallback both
  held under a live private avatar.

## 6. Corrective options (report-only, small; production changes need their own approved slice)

1. Pass the refused slot's live material at
   `AlphaSeparationPreparation.cs:428-431`; the existing resolver then names
   the authoring asset. Closes the `<missing>` defect.
2. Reword `amuse.renderer.LockedPoiyomiOriginalShaderUnattested` to name
   attestation, not tracing (`AmuseReportStrings.cs:172-181`).
3. Carry the closure-failure way and the renderer name in the
   `MaterialDependencyClosureFailed` entry text.
4. Name the format and texture in `amuse.texture.UnsupportedFormat`, and
   soften the outcome sentence to not claim the slot stayed original.
5. Add a Poiyomi feature label for the alpha mask properties so the sentence
   stops saying "a shader feature".

Coverage items (larger, separate decisions): the Poiyomi threshold-envelope
parity; crunched-BC1 red-channel admission; slot-scoping the three
slot-local closure ways. `[DECISION NEEDED]`

## 7. Limits

- The wrong-instance probe and the doubled build are recorded in section 2;
  neither contaminated the evidence.
- The evidence bundle with unsanitized names lives only in session-private
  scratch outside the repository and must never be committed.
- The four conversion-refused slots' exact arm, and the exact closure-failure
  way per renderer, remain `[INFERENCE]`; the current report text cannot
  answer them, which is itself finding 4.3/4.4.
