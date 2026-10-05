# Ponytail audit cuts design

Date: 2026-10-04. Derived from the audit record
`docs/superpowers/investigations/2026-10-04-ponytail-repo-wide-audit.md`,
findings F1 through F49. Branch `chore/ponytail-audit-cuts`, base `main` at
`4a4ba3f`. This spec fixes the disposition of every finding, the cut order,
the shared homes and names, and the test strategy for each slice. Nothing
here changes observable build behavior, with the two stated exceptions in
D11 and D12, both of which are behavior-preserving at the refusal surface.

## D1. Scope and parked items

In scope: F1 through F49 with the exceptions below. The anchor pass of
2026-10-04 verified every symbol and line range against the working tree
of `4a4ba3f`.

Parked, outside this implementation:

1. F4, the task-6 SDK spike record. Delete or relocate is an owner
   preference about history, not a code decision. It waits for the owner.
2. F26, deletion of the placeholder PR workflow. That deletion requires a
   branch-protection change outside this repository. The safe half of F26
   is in scope: reword the stale expiry comment in
   `.github/workflows/pr.yml` so the file no longer promises an arrival
   that happened. The workflow itself stays until the required check is
   removed by the owner.

One live correction to the record: F18 names three constant-alias classes.
The anchor pass found four on the tree of 2026-10-04, listed in the plan.
All four go.

Dispositions by number, so no finding rides a category unnamed. Named
decisions: F1 in D5, F2 and F3 in D13, F5 in D6, F6 in D7, F9 in D8,
F12 in D10, F13 in D4, F14 in D14, F17 in D9, F18 in D15, F21 in D16,
F24 in D11, F35 in D12, F26 in D17 together with the park above.
Executed inside D13 without their own decision: F7, F8, F10, F11, F16,
F20, F36. Helper extractions that ride the record replacements verbatim,
each one seam, factory, delegation, or parameterization: F15, F22, F23,
F25, F27, F28, F29, F31, F33, F37, F42. Mechanical per the record: F19,
F30, F32, F34, F38, F39, F40, F41, F43, F44, F45, F46, F47, F48, F49.
F40 is mechanical with one caveat: it widens a gate to internal and
rewrites four test calls.

## D2. Execution model

One branch, `chore/ponytail-audit-cuts`. The plan runs as ordered tasks.
Each task ends with a focused green run recorded with its date and counts.
No staging, no commits, no push during execution. The controller runs Unity
in the dev editor instance only, after the data-path check in the plan
header. No task needs the Census Lab project.

## D3. Cut order and why

1. Tasks 1 to 3, mechanical and cleanup: the deletes F19, F21, F30, F32,
   F40, F43, F44, F47, F48, the swaps F34, F38, F39, F41, F45, F46,
   F49, the channel F35 per D12, the comment F26 per D17, and the helper
   F42. Small diffs, every one pinned by existing suites, so the cheap
   wins land before any shared type exists.
2. Tasks 4 and 5, Host and Build dedup: F22, F23, F25, F27, F31, F13
   per D4, F33, and F14 per D14. Internal seams, no pinned attestation
   behavior, rich direct test coverage.
3. Tasks 6 and 7, Semantics helpers then the twin merge: F5, F6, F9,
   F17, F28, F37 with their decisions, then F1 per D5, which runs after
   its helpers exist and after a falsifier baseline is recorded.
4. Tasks 8 and 9, Semantics surface cleanups: F24 per D11 and F12 per
   D10. Neither needs a shared type. They follow their neighbors for
   review locality.
5. Tasks 10 to 13, test infrastructure: F2 and F3 per D13, F7 also per
   D13, and F8, F10, F11, F16, F18 per D15, F20, F36. The biggest churn,
   saved for last so it never destabilizes the verification of earlier
   slices.
6. Task 14, research: F15 and F29. Independent, never ships, no risk to
   the product.

## D4. F13, the GPU readback core

Decision: one internal acquisition core, parameterized. The asymmetries
survive on purpose.

- The core lives as a sibling of `TryAcquireLevel` on
  `UnityAlphaFieldEvidence`. It takes the target format, a decode
  delegate over the readback, and a validation delegate.
- The generated route keeps its inert-bounds logic, residency gate,
  placeholder fallback, session cache, and its
  `MissingReferenceException` catch in its own method. Only the
  descriptor, allocate, gate, request, length-check, and release shape
  merge.
- The green-arm gap stays. The core receives validation as a delegate.
  `TryAcquireLevel` validates unconditionally, the generated route
  validates only in the red arm, exactly as today. Closing the gap is a
  behavior change and stays out. The core doc states this with the reason
  the gap exists: green-arm flags are synthesized values, so validation
  there would be vacuous.
- The render-target divergence stays, deliberately. The core takes a
  save-restore flag: `TryAcquireLevel` saves and restores
  `RenderTexture.active` around its blit, the generated route does not,
  exactly as today. The adoption alternative was rejected in the review
  of 2026-10-04: no existing test can observe restoration on the
  generated route, so the adoption fallback could never fire. The core
  doc states the divergence and its reason.

Test strategy: plain green. Filters `UnityAlphaFieldEvidenceTests` and
`UnityGeneratedTextureEvidenceTests` pin both arms end to end. No test
file changes.

## D5. F1, the lilToon twin merge

Decision: one parameterized alpha interpreter plus one shared request
builder. The pinned constants and bounds stay per family.

- The interpreter takes a family argument with exactly the three deltas
  the record measured: the cutoff bound, the two transparent-only gates
  with their bounds, and the coverage-gate array, which carries the
  dither-membership difference.
- Per-family constants keep their homes: `MaxProvableCutoff` stays
  declared per file with its own value and doc. The interpreter receives
  the bound as an input, it never derives it.
- The request builder takes a per-family delta table over the scalar
  property list: the shared 46-entry base, the transparent additions, the
  dither removal, the reorder, and the per-family `CutoffProperty`
  declaration. The builder serves the two duplicated families, cutout
  and transparent, which stay distinct values. The main lilToon request
  in `LilToonMaterialSemantics` is outside finding F1 and keeps its own
  construction unchanged, because it is not part of the duplicated twin.
- The class declarations of both files stay, because
  `AlphaSeparationPersistenceTests.AuditedProductionFiles` pins them.

Test strategy: characterization then green. Before the cut, run and record
`LilToonCutoutAlphaTests` and `LilToonTransparentAlphaTests`, plus the
neutral-claim, invariance, blast-radius, monotonicity, and shared-agreement
suites named in the plan. After the cut, the same filters must return the
same counts. The falsifier rows are the wrong-implementation guards: a
merge that copies the cutout bound into transparent fails the transparent
cutoff falsifiers, and a merge that unifies the gate arrays fails the
dither rows.

## D6. F5, the shared evidence gates

Decision: one internal static helper type in `Editor/Semantics`.

- `EvidenceGates` holds: both `FirstFailedZeroGate` overloads, both
  `TryReadBinary` overloads, the `IsFinite` trio,
  `RequireAnalyzableMaterial`, `RecordUnknown` for the three lilToon
  files, and `AllUnknown` for its three sites.
- The Poyiomi `RecordUnknown` analog stays local. It routes through the
  Poyiomi diagnostic type and its local `AddDiagnostic`, so it is
  shape-equivalent, not a copy. Forcing it in would change diagnostic
  typing for no line saving.
- `BuildEligibilitySchema` joins in one place, parameterized over the
  recipe and source arrays the three eligibility files already pass.
- The `IsFinite` copies outside the four frontend files stay. The record
  scoped F5 to the frontends. Sweeping them is scope drift.

Test strategy: plain green over the per-frontend and eligibility filters.
No test file changes.

## D7. F6, the exact product fold

Decision: one static fold beside `ScalarSemanticValue`, parameterized over
map support and diagnostic emission.

- The fold signature takes the two operands, the refusal property name,
  and the diagnostic constructor as a delegate, because the lilToon and
  Poyiomi diagnostic types differ.
- Map support is a flag: the lilToon copies thread the affine map list
  and gate the single-sample shape on `HasAnyMap`. The Poyiomi copy has
  no maps. The flag carries the difference.
- `ComposeLayer` stays in the frontends. It is the layer-mode caller, not
  part of the fold.

Test strategy: plain green over the mask-product falsifier filters named
in the plan. No test file changes.

## D8. F9, the producer version seam

Decision: one internal seam type in `Editor/Semantics`, plus same-named
internal forwards.

- The seam type is `AdmittedProducerVersions` in
  `Editor/Semantics/AdmittedProducerVersions.cs`. It holds the admitted
  set, the delegate field, the cache, the reset, the session clear, and
  the membership check. Each attestation class constructs it with its
  package-name pin and its version pins.
- Both attestation classes keep same-named internal forwards for every
  member the seven test files address: `ResetForTests`,
  `SetAdmittedVersionsForTests`, `ReadInstalledPackageVersionOrNull`,
  `AdmittedVersions`, `ClearVersionCacheForSession`,
  `IsVersionAdmitted`, and `TryReadInstalledProducerVersion`. Each
  forward carries a doc sentence naming the test files that address it,
  per the retention discipline.
- The pins and their provenance comments stay in the attestation classes.
  Pins are excluded evidence.

Test strategy: plain green over all seven files that address the
forwards: `ReplacementTextureAttestationTests`,
`AaoAtlasTextureAttestationTests`, `GeneratedTextureAttestationTests`,
`UnityTextureEvidenceTests`, `UnityAlphaFieldEvidenceTests`,
`UnityGeneratedTextureEvidenceTests`, and
`UnityMaterialEvidenceCaptureTests`. No test file changes.

## D9. F17, the opaque conversion factors

Decision: one shared union type in the `Editor/Semantics` root.

- The union type carries the ten constants and two predicates of the
  lilToon type plus nothing new, because the Poyiomi block is a subset:
  it lacks `BlendOpMax`, `ColorMaskAll`, and `DepthWriteOn`.
- `LilToonOpaqueConversionResult.cs` keeps its result type. The factors
  class moves out. The persistence pin is edited twice in the same task:
  the existing entry for `LilToonOpaqueConversionResult.cs` re-anchors
  to the surviving result type declaration, and a new entry pairs the
  new factors file with the moved class. No live audited file drops out
  of the list.
- The four consumers, two lilToon eligibility files, the Poyiomi
  conversion, and the Poyiomi eligibility gates, reference the shared
  type.

Test strategy: plain green over the eligibility and conversion filters.
One test file changes: the persistence pin table.

## D10. F12, TextureSampling pass-through

Decision: delete `AlphaSamplingSettings`. `TextureSampling` flows through.

- The resolver stores `TextureSampling` and stops copying fields at its
  two construction sites.
- The classifier reads `.Filter`, `.Wrap`, `.Aniso` instead of the
  duplicated property names.
- The construction sites in six test files re-type mechanically. The
  anchor pass lists every line.

Test strategy: plain green over the classifier and resolver filters after
the mechanical re-typing. No assertion changes.

## D11. F24, the producer enum

Decision: delete `GeneratedTextureProducer`. Both methods return `bool`.

- The production discard sites drop their `out _`. Five production sites
  and one test site hold them, listed in the plan.
- The fourteen assertions in `GeneratedTextureAttestationTests` that
  read the member rewrite to bool assertions. The two route-texture
  setups with their admitted-version gates stay, and their two producer
  reads rewrite with the fourteen. After the rewrite the negative arms
  still discriminate: a refusal still refuses, and the admitted-version
  gates still gate. Positive-arm producer identity is intentionally
  lost. The record sanctions the narrowing because the distinction is
  behaviorally dead in production.

Test strategy: characterization before the rewrite: record the fourteen
assertions green. Then rewrite and re-run. Same counts.

## D12. F35, the dead prerelease channel

Decision: remove the channel. The refusal surface is identical.

- The parser's `hasPrereleaseSuffix` output, its branch, the admission
  and bound conjunctions, and the consent disjunct go. A prerelease
  version then parses as unparseable input and refuses with the same
  named cause as before.
- The four tests that pin the prerelease refusals keep passing unchanged.
  They assert the refusal name, which does not move.
- The parser doc sentence and the two summary sentences that advertise
  the distinction rewrite to the truth: a prerelease refuses as
  unparseable input.

Test strategy: plain green over `HostLifecycleCapabilityTests` and
`VersionConsentTests`. No test file changes.

## D13. F2 and F3, the shared test homes

Decision: one new folder, `Tests/Editor/Shared/`, with seven types. File
name equals type name.

- `TestTextureImport.WritePng(name, width, height, pixels,
  configureImporter)` replaces the 21 PNG staging blocks across the 12
  product suites and the two fixture-base importers, which carry the
  eight Semantics-side copies between them. The `Action<TextureImporter>`
  callback shape already exists at three sites. The one EXR staging site
  stays local: different encoder, different importer path, one
  occurrence. The native-asset importer joins the same helper as one
  shared `ImportNative`: the record and the anchor pass disagree on
  whether both bases carry a copy, so the executor verifies which copies
  exist and moves the survivors.
- `TestShaderWriter.WriteTestShader(name, shaderText)` replaces the ten
  shader-writer copies. The two sites that hand-write `.meta` files with
  pinned GUIDs keep those writes local. The helper covers write, forced
  import, resolve, and the null assert.
- `TestTransientScope` holds the tracked-object list, the temp-folder
  create and destroy, and `Track<T>`. Both fixture bases and the two
  multi suites consume it. `PoiyomiTextureEvidenceTests` derives from
  `PoiyomiFixtureTestBase` and deletes its private scaffolding.
- `ObjectRegistryGuard`, a disposable save-swap-restore, replaces the
  registry boilerplate at every class the census found, about eleven.
  The four classes the scouts named plus `UnityAlphaFieldEvidenceTests`
  at five sites, `UnityMaterialEvidenceCaptureTests`, and
  `AmusePlatformFinishPluginTests` are the known ones. The executor
  searches the assembly for the save-and-swap pattern and adopts the
  guard at every hit, so the census either fully executes or the miss is
  visible in diff review.
- `AttestationEnvironment` holds `ProjectRoot`, `ShaderDir`, `Tree`, and
  `Canon` for the two attestation suites.
- `CurveDescription` holds the object-curve describer and the
  authored-curve overload. The byte-identical committed twin in
  `AlphaSeparationSplitTests` deletes and its five callers re-point.
- `FloatUlp` holds the plus-one step and the N-step form.
- `MergedConsumptionFixture` lives beside its two consumers in
  `Tests/Editor/Build/`, not in Shared, because only they consume it. It
  absorbs the band model, the authored-triangle registry, the banded
  texture import, the stand-in material builders, the reflected optimizer
  configuration, and the triangle-match oracle. The DAO env-var gate, the
  SDK dispatcher call, and the d4rk cleanup stay in the DAO file.

Test strategy: plain green per suite after each migration. Each migration
is its own plan task with its own filters, so a failure localizes to one
suite.

## D14. F14, the slot-binding filter

Decision: a primitives-shaped helper plus one shared walk.

- The helper takes five strings: the binding path, the binding type full
  name, the property name, the renderer path, and the renderer type full
  name, plus the slot out. The first three identify the binding, the
  last two gate the renderer. Site six in the window close checks the
  path through dictionary membership and loops candidate types, so it
  calls the helper per candidate type with the dictionary-resolved path.
  A primitives shape avoids a generic binding abstraction the repo does
  not need.
- The helper lives beside the existing parse and gate members it wraps,
  on `UnityAnimationEvidenceCapture`, and is internal.
- The swap-in closure pass and `RemapClosureCurves` share one private
  renderer-and-clips walk inside `TransientUnlockSwapIn`.

Test strategy: plain green over the five site-covering suites. No test
file changes.

## D15. F18, the alias classes

Decision: cut all four. Widen the shader-name constants on the two
fixture bases from protected to internal. Delete the four const-only
subclasses. The behavioral fixture subclasses stay untouched.

Test strategy: plain green over the consumers. No test file changes
beyond the deletions.

## D16. F21, the locked-material identity facts

Decision: delete the two facts, their serialization members, the
constructor plumbing, and the dead per-call reads. Rewrite the false doc
sentence.

- `LockedMaterialIdentityTests` changes: every `Serialization`
  construction site drops the two named arguments, and the two dead-fact
  assertions delete with their premise comments. The remaining roundtrip
  assertions stay.
- The tag-name constant stays. Tests read tags through it.

Test strategy: characterization then green. Record the file green before
the edit, then after, with the test count lower by exactly the deleted
assertions' tests if any whole test deletes.

## D17. F26, the workflow comment

Decision: reword the header comment of `.github/workflows/pr.yml` to
state the truth on the day of the cut: the release-candidate version
series has arrived, the gate remains as a deliberate branch-protection
placeholder, and removal needs the required-check change first. No
workflow behavior changes.

Test strategy: none. Comment only.

## Retention discipline

Every surface kept past its last consumer carries a doc sentence naming
the consumer or the exit condition, per the 2026-09-24 rule. Three new
retentions arise here: the attestation forwards of D8 name the seven test
files, the EXR staging site of D13 names its one occurrence, and the
workflow gate of D17 names its exit condition.

## Stop conditions

Stop and return with evidence when:

1. Any report key, refusal name, or refusal count changes its runtime
   value. That is behavior, not cleanup.
2. A consumer appears that the record missed. Re-verify the finding
   before touching it.
3. Any focused filter returns zero tests, or any suite count moves
   without a task-designed explanation.
4. The falsifier or characterization baseline of D5 or D11 does not
   reproduce after its cut.
5. The research assembly fails to compile outside its own tasks.
6. The Lab project or any Unity instance outside the dev editor instance
   appears necessary.
7. The lilToon twin merge cannot preserve a pinned bound or gate
   membership without a new abstraction layer. Return the attempt as
   evidence instead of forcing it.

## Boundary

No staging, no commits, no push during execution. One branch,
`chore/ponytail-audit-cuts`. The research package must not enter the
product or the VPM listing. Every deleted `.cs` file deletes its `.meta`
file in the same task. New `.cs` files get their `.meta` from the Unity
refresh and both land together. Run `git diff --check` before reporting.
