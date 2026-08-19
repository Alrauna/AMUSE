# Texture Alpha Evidence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` by default. Use `superpowers:subagent-driven-development` only if the user separately authorizes subagents. Track execution with the checkboxes below.

**Goal:** Implement the single missing production implementation of the existing
`AlphaFieldProvider` delegate, converting supported Unity texture state into the immutable
`AlphaTextureData` the exact triangle-alpha proof core already consumes.

**Architecture:** One new production file, `Editor/Host/UnityAlphaFieldEvidence.cs`. No new
immutable type, no change to `TriangleAlphaClassifier`, `AlphaTextureData`,
`MaterialSemantics`, `AlphaSemanticsResolver`, `UnityTextureEvidence`, or either shader
frontend. The producer reads the **imported** `Texture2D` via `GetPixels32()`, refuses
everything it cannot prove, and constructs `AlphaTextureData` directly.

**Tech Stack:** Unity 2022.3.22f1, C#, NUnit EditMode tests, existing
`Alrauna.Amuse.Editor` and `Alrauna.Amuse.Tests.Editor` assemblies. No new dependency,
assembly, asmdef, or package metadata change.

## Global constraints

- The approved specification is `docs/superpowers/specs/2026-08-19-texture-alpha-evidence-design.md`. Read it before Task 1, including the **seven** measured Unity experiments and the refusal matrix.
- Execute only after explicit design/plan approval, on `feat/texture-alpha-evidence` based on `016f3d2`.
- **The v1 format allow-list is closed and measured: `RGBA32`, `ARGB32`, `Alpha8`, `RGB24`.** Do not add a format without measuring it against a real shader sample the way Experiment 5 did. `BGRA32` is out; `TextureImporterFormat` cannot request it in 2022.3.

### TDD rule

Red/green is **mandatory for any behaviour that requires implementation or a change**: the
positive extraction path, the field contents, dimensions, and row order. For those, observe
the focused red **for the intended reason** before writing production code, then observe
that same scope green.

**Fail-closed read handling is not in that set.** The narrow exception catches are
defensive hardening justified by Experiment 7 and confirmed at Task 10 code review. Include
them under an observed RED only if a deterministic real Unity fixture is found that
genuinely reaches the guarded read and throws *after every positive precondition has
passed*. Do not manufacture a race, and do not add an injectable texture or read
abstraction merely to exercise a catch.

Several test classes will legitimately pass on first run, because a **positive** allow-list
already implements the refusal for everything outside it:

- refusal-boundary tests (unsupported formats, non-readable, mipmapped, non-`Texture2D`, unknown id, non-Alpha channel);
- characterization tests (the Class 1 / Class 2 importer-setting behaviour);
- architecture-guard tests (Task 9);
- regression tests for the measured false-opaque formats.

**Record which tests passed immediately and why. Do not manufacture RED by deliberately
breaking production code**, removing an allow-list entry, or inverting a condition. A test
that passes because the design was right is evidence, not a gap. The obligation is that the
test would *fail if the behaviour regressed* — verify that by reasoning about the assertion,
or, where cheap and non-destructive, by a temporary negative control that is reverted
immediately (Task 9 uses one).
- **Never widen a claim under uncertainty.** Every ambiguity resolves to `return false`. A test that asserts a refusal must fail if the producer ever starts accepting that case.
- **Do not create:** `IShaderAdapter`, an adapter registry or factory, a generalized texture-evidence framework, `TextureEvidence<T>`, a shader schema, an expression DAG, a feature graph, an HLSL parser, coverage semantics, a richer `ColorSemanticValue`, shader adapter #3, atlasing, material combining, animation or state tracing, optimization-planner changes, NDMF integration, an avatar-root component, inspector UI, a build callback, a Play Mode path, or GitHub Actions CI.
- **Do not modify:** `TriangleAlphaClassifier.cs`, `ExactUvGeometry.cs`, `MeshSeparationPlanner.cs`, `AlphaSemanticsResolver.cs`, `MaterialSemantics.cs`, `UnityTextureEvidence.cs`, `PoiyomiMaterialSemantics.cs`, `LilToonMaterialSemantics.cs`, `LilToonSourceAttestation.cs`, the reference fixtures, either stand-in fixture shader, asmdefs, `AssemblyInfo.cs`, package metadata, manifests or locks, workflows, or project settings.
- **Do not weaken, delete, skip, rename, or rewrite any pre-existing test case or expectation.** Every test that exists at `016f3d2` must still exist, unaltered, and pass at the end of the plan. `SharedClass_ExposesExactlyFiveSemanticFacts` in particular must pass **unmodified** — no sixth method on `UnityTextureEvidence`.
- **No asset mutation in production code.** The producer reads only. It must never call `SaveAndReimport`, set `isReadable`, write a texture, or touch an importer. Tests may create and delete temporary assets under `Assets/` and must clean up in `[TearDown]`.
- Treat each Unity asset and its `.meta` file as one unit. New `.cs` files and the new folder must get their `.meta` from Unity import; inspect every new GUID; do not hand-write, copy, or delete `.meta` files.
- Do not commit, push, open a PR, tag, publish, or change repository settings. Those require separate authorization; the plan ends at a review handoff.
- **Testbed policy.** Every test in this plan creates and deletes temporary assets under `Assets/`. Run them **only** in the public development project. Confirm `Application.dataPath == "E:/AI/Git/AMUSE/Assets"` before any test run. If only the private avatar testbed is reachable, **stop and report** rather than running there.

---

## Planned files

**Create:**

- `Packages/com.alrauna.amuse/Editor/Host.meta`
- `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs` (+ `.meta`)
- `Packages/com.alrauna.amuse/Tests/Editor/Host.meta`
- `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs` (+ `.meta`)
- `Packages/com.alrauna.amuse/Tests/Editor/Host/AlphaEvidenceClassifierIntegrationTests.cs` (+ `.meta`)

**Modify:**

- `docs/superpowers/specs/2026-08-19-texture-alpha-evidence-design.md` (Task 3 measurement result; Task 10 stop-condition outcomes)
- `docs/superpowers/plans/2026-08-19-texture-alpha-evidence.md` (execution checkboxes and observed-result notes)

**Expected final working-tree scope.** At the review handoff the working tree should
contain exactly the five created paths plus their `.meta` files and the two modified
documents. Anything else that changed is a finding to report, not to silently restore.

### Test-fixture convention

Both test files follow `UnityTextureEvidenceTests` exactly: a `TempFolder` constant under
`Assets/`, `[SetUp]` creating it, `[TearDown]` calling `AssetDatabase.DeleteAsset`, and a
private `Import(...)` helper that writes a PNG with `EncodeToPNG`, imports it with
`ImportAssetOptions.ForceSynchronousImport`, configures the importer through a delegate,
then `SaveAndReimport`s and loads the result. **Do not introduce a shared test base** with
the existing evidence tests; the duplication is the standing convention in this repository.

Use distinct temp folder names per test class so the two classes cannot collide.

---

## Task 1 — Confirm the seam and the base state

**No code. Verification only. Gates every later task.**

**Architectural question:** *Is `AlphaFieldProvider` genuinely unimplemented in production,
and is the classifier's input contract reachable without adaptation?*

- [ ] Confirm the branch is `feat/texture-alpha-evidence` at base `016f3d2` and the worktree is clean.
- [ ] Confirm `git grep -n "AlphaFieldProvider" Packages/com.alrauna.amuse/Editor` returns only the declaration in `AlphaSemanticsResolver.cs` — no production implementation exists.
- [ ] Confirm `git grep -n "using UnityEditor" Packages/com.alrauna.amuse/Editor/Analysis` returns nothing. This is the invariant Task 9 will lock down.
- [ ] Confirm `AlphaTextureData`'s constructor still copies into a private array and still derives `IsFullyOpaque` / `IsFullyNonOpaque`, and that the four classify paths still test only `== byte.MaxValue`.
- [ ] Run the full EditMode suite once, unchanged, and record the pass count as the baseline.

**Stop if:** any of the first four checks disagrees with the design document, or the
baseline suite is not green. A red baseline is a finding, not something to work around.

---

## Task 2 — Red: the exactness and row-order proof

**Architectural question:** *Does an imported uncompressed readable texture yield exactly
the bytes the classifier's grid expects, in the orientation it expects?*

- [ ] Create `Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs` with the fixture convention above.
- [ ] Write the import helper so the alpha pattern is **asymmetric**: a 4x4 RGBA32 PNG whose texel at `(0, 0)` has alpha `128`, texel `(3, 3)` has alpha `254`, and every other texel is `255`.
- [ ] Write three failing tests:
  - readable + uncompressed returns `true`, and `field.Width == 4 && field.Height == 4`;
  - `field.GetAlpha(0, 0) == 128` and `field.GetAlpha(3, 3) == 254` — **the row-order and axis proof**;
  - every other coordinate is `255`, and `field.IsFullyOpaque` is `false`.
- [ ] Observe red: the type does not exist yet (compile error is the expected first red).

**Note:** the asymmetric pattern is deliberate. A uniform texture would pass a
transposed or row-flipped implementation.

---

## Task 3 — Green the core extraction path

**Architectural question:** *Can the measured allow-list be implemented without any
importer inspection?*

The format measurement gate is **already complete** — Experiments 5 and 6 in the design
document admitted `RGBA32`, `ARGB32`, `Alpha8`, and `RGB24` against a real shader sample,
and rejected `BGRA32` as unreachable. Do not re-run it and do not re-open the list.

- [ ] Create `Editor/Host/UnityAlphaFieldEvidence.cs` implementing the constructor and `TryGetAlphaField` for the supported path only: identity map via `UnityTextureEvidence.TryGetSourceId`, `texture == null` check (Unity's overloaded operator, **not** `ReferenceEquals`), `Texture2D` cast, `isReadable`, `mipmapCount == 1`, the closed format allow-list, positive dimensions, the guarded `GetPixels32(0)` read, length check, then `byte[]` → `new AlphaTextureData(width, height, bytes)`.
- [ ] Copy the alpha bytes straight across: `Color32[]` from `GetPixels32` is already row-major bottom-to-top (Experiment 4). **No flip, no transpose, no index arithmetic.**
- [ ] Let Unity import the new folder and file; confirm fresh `.meta` files appeared and no existing GUID changed.
- [ ] Observe Task 2's three tests green.

**Do not** add a size cap, a cache, a `TextureImporter` lookup, an `alphaSource` branch, a
swizzle branch, or any condition the design's allow-list does not list.

---

## Task 4 — Red/green: the measured false-opaque refusals

**Architectural question:** *Does the producer refuse exactly the states that were measured
to fabricate opacity?*

- [ ] Write tests asserting `TryGetAlphaField` returns `false` for: default (non-readable) import; readable `DXT5`; readable `BC7`; readable `DXT5Crunched`; readable `RGBAHalf`; readable `ARGB16`/`ARGB4444`.
- [ ] Each test must additionally assert `field == null`, so a refusal never hands back a partially-built field.
- [ ] Add a comment on the `DXT5`, `BC7`, `DXT5Crunched`, and `RGBAHalf` tests naming the measured false-opaque value (`254 → 255`, and `0.999f → 255`). These are regression tests for a real correctness hazard, not style checks.

**These are expected to pass immediately**, because Task 3's positive allow-list already
refuses every format outside it. That is the correct outcome — per the TDD rule above, do
**not** break the allow-list to manufacture red. Record which passed on first run.

For each, confirm by reading the assertion that it would fail if the format were added to
the allow-list; that is what makes it a boundary lock rather than a tautology.

---

## Task 5 — Red/green: structural and identity refusals

**Architectural question:** *Can the producer be induced to fabricate identity or to read a
texture whose field is not one grid?*

- [ ] Write tests asserting `false` for: a mipmapped readable texture; a `RenderTexture` supplied as a `Texture`; a source id that was never supplied to the constructor; `TextureChannel.Red`, `Green`, and `Blue` on an otherwise-supported texture.
- [ ] Write a test proving a `null` element inside the supplied collection is **skipped**, not thrown: construct with `{ null, validTexture }` and assert the valid texture still resolves.
- [ ] Write a test proving two textures resolving to the same id do not throw.
- [ ] Most of these will pass immediately; the `RenderTexture` and mipmap cases may too. Record which required implementation.

### Task 5b — Red/green: fail-closed reads

**Architectural question:** *Does an expected texture-read failure refuse, rather than
escape as an exception?*

Experiment 7 measured the taxonomy: `ArgumentException` for not-readable / corrupted /
absent data and for an invalid mip level; `MissingReferenceException` for a destroyed
object. `MissingReferenceException`'s base is `SystemException`, **not** `UnityException`.

- [ ] Implement the guard exactly as the design's *Fail-closed reads* section specifies. `MissingReferenceException` must cover **every Unity-object evidence read** the producer performs after the null and `Texture2D` checks — `isReadable`, `format`, `mipmapCount`, `width`, `height`, and `GetPixels32(0)` — because Experiment 7 showed `.isReadable` itself throws on a destroyed object. `ArgumentException` stays **narrowly** on the `GetPixels32(0)` evidence read.
- [ ] **Do not write `catch (Exception)`, a bare `catch { }`, or `catch (UnityException)` as a substitute.** No unexpected exception class may be silently swallowed.
- [ ] Write the deterministic destroyed-object fixture: import a supported texture, construct the producer, `Object.DestroyImmediate` the texture, then call `TryGetAlphaField`. Assert `false`, `field == null`, and that **nothing throws**.
- [ ] **This fixture is expected to PASS immediately.** Task 3 already implements the Unity-overloaded `texture == null` guard, which is what refuses a destroyed object; the lookup returns the destroyed Unity object, `== null` is true, and the producer refuses. The test verifies that primary guard, **not** the `MissingReferenceException` catch. Do not observe RED for it and do not remove the null check to manufacture one.
- [ ] Assert the producer does not rely on `ReferenceEquals(texture, null)`, which is `false` for a destroyed object.

**Do not** add a texture-size cap. No evidence requires one.

---

## Task 6 — Red/green: malformed input throws

**Architectural question:** *Is the malformed/unsupported split the same one the rest of the
repository uses?*

- [ ] Write failing tests asserting: `null` collection → `ArgumentNullException`; `default(TextureSourceId)` → `ArgumentException`; an undefined `TextureChannel` cast → `ArgumentOutOfRangeException`.
- [ ] Observe red then green for those three.
- [ ] **Zero dimensions and `GetPixels32` length mismatch are conditional.** Keep the production guards (`width > 0`, `height > 0`, `pixels.Length == width * height`), but write a test **only if** Unity 2022.3 can produce the state through a deterministic real fixture under the approved architecture. Do **not** create a test seam, mock `Texture2D`, use reflection or deliberate corruption, or change production architecture to manufacture an impossible Unity state. If no natural fixture exists, verify the guards by code review at Task 10 and record that no natural fixture exists.

---

## Task 7 — Characterization: importer settings, determinism, immutability

**Architectural question:** *Does the producer follow the imported field without inspecting
the importer?*

The design distinguishes two classes and this task must not conflate them. **Class 2**
settings are predicate-invariant and are tested for invariance. **Class 1** settings change
the field, and are tested by asserting the producer reports the changed field.

- [ ] **Class 2 — assert unchanged:** the returned field is identical with `alphaIsTransparency = true` and with `sRGBTexture = false`.
- [ ] **Class 1 — `alphaSource`, assert the producer follows the import**, three cases against three *different* expected fields:
  - `FromInput` → bytes equal the source alpha;
  - `FromGrayScale` → bytes equal the luminance-derived alpha, **not** the source alpha;
  - `None` → an all-255 field.
- [ ] **Class 1 — dimensions follow the import:** `maxTextureSize = 2` yields a `2x2` field; a 3x3 NPOT source with `npotScale = None` yields a `3x3` field. No power-of-two assumption.
- [ ] Determinism: two consecutive calls for the same id return fields with identical dimensions and identical bytes at every coordinate.
- [ ] Immutability: obtain a field, obtain a second field for the same id, and confirm they are independent — the classifier's constructor copy semantics hold and no shared buffer is observable.
- [ ] Record which of these passed on first run. Per the TDD rule, do not manufacture red.

**Do not** assert that `alphaSource` leaves the field unchanged. It does not, it was
measured not to, and asserting it would encode a false invariant.

**If a Class 2 setting turns out to change the field**, stop and report. That would falsify
a measured claim and is an architecture finding, not a test to adjust.

---

## Task 8 — Red/green: the integration test with the real classifier

**Architectural question:** *Can host evidence be consumed by the existing exact classifier
without an impedance mismatch?*

- [ ] Create `Tests/Editor/Host/AlphaEvidenceClassifierIntegrationTests.cs` with its own temp folder.
- [ ] Build the chain exactly as the design specifies: imported texture → `TryGetSourceId` → `TryGetSampling` → `TextureSample` → `ScalarSemanticValue.Texture(sample, TextureChannel.Alpha)` → `SemanticOutput<ScalarSemanticValue>.Complete(...)` → `AlphaSemanticsResolver.Resolve(..., evidence.TryGetAlphaField)`.
- [ ] Use the asymmetric 4x4 texture: only texel `(0, 0)` is non-opaque.
- [ ] Assert three outcomes:
  - a triangle whose UV hull lies wholly inside the opaque region → `ProvenOpaque`;
  - a triangle whose UV hull covers texel `(0, 0)` → `MustRemainTransparent`;
  - a fully opaque texture → `ProvenOpaque`.
- [ ] Assert the resolution is `IsResolved` in all three cases, so a refusal cannot masquerade as a proof.
- [ ] Observe red then green.

**Do not** introduce `Material`, either shader frontend, or `MeshSeparationPlanner` into
this test. If the chain cannot be built without them, that is a design finding — stop and
report it.

Case 1 versus case 2 is the whole point: it is the only test that simultaneously exercises
dimensions, bottom-to-top row order, x/y orientation, and byte semantics against real
geometry.

---

## Task 9 — Red/green: lock the Analysis boundary

**Architectural question:** *Is "the proof core has no `UnityEditor` dependency" an
enforceable invariant rather than a convention?*

- [ ] Write a source-text test that reads every `.cs` file under `Packages/com.alrauna.amuse/Editor/Analysis/` and asserts none has **any dependency on the `UnityEditor` namespace** — not merely the literal text `using UnityEditor`.
- [ ] Match the identifier, not the using-directive. A word-boundary regex on `UnityEditor` catches all three evasions the narrow check would miss:
  - `using UnityEditor;` and `using UnityEditor.Something;`
  - fully-qualified use with no using at all: `UnityEditor.AssetDatabase.GetAssetPath(...)`
  - an alias: `using AD = UnityEditor.AssetDatabase;`
- [ ] Assert the file list is non-empty before asserting the contents, so a wrong path cannot make the test vacuously green.
- [ ] Negative control: temporarily point the test at `Editor/Semantics/` (which does use `UnityEditor`) and confirm it fails. Revert immediately.
- [ ] Place it in `UnityAlphaFieldEvidenceTests.cs` — it belongs with the boundary it defends, and it does not warrant its own file.

This test is expected to pass on first run; that is the point. It locks an invariant that
already holds. This is the same defensive move as `UnityTextureEvidence`'s five-member
reflection guard.

---

## Task 10 — Architecture and scope checkpoint

**No new code. Verification and reporting only.**

- [ ] Re-read the design's Non-goals and Stop-conditions sections against the actual diff.
- [ ] Confirm nothing on the "Do not modify" list changed: `git diff --stat` must show only the five created paths and the two documents.
- [ ] Confirm `UnityTextureEvidence` still exposes exactly five members and that its guard test passes unmodified.
- [ ] Confirm `Editor/Host/` contains exactly one file.
- [ ] Confirm the production file contains no cache, no static mutable state, no `SaveAndReimport`, no `isReadable` assignment, no texture-size cap, and **no `TextureImporter` use at all** — grep the file for `TextureImporter`, `alphaSource`, `swizzle`, and `AssetImporter` and confirm zero hits. This is the structural test that the producer follows the imported field rather than branching on importer settings.
- [ ] Confirm the production file contains no `catch (Exception)` and no bare `catch`.
- [ ] Confirm the admitted format set is exactly `{RGBA32, ARGB32, Alpha8, RGB24}`.
- [ ] Confirm every refusal path sets `field = null` and that no code path returns `true` with a `null` field.
- [ ] List the tests that passed on first run and confirm each is a refusal-boundary, characterization, architecture-guard, or regression test — not a behaviour that should have needed implementation.
- [ ] Record any stop condition that fired, with its evidence, in the design document.

---

## Task 11 — Full validation

- [ ] Confirm the connected Unity Editor is the **public** project (`Application.dataPath`).
- [ ] Run the **complete** EditMode suite. Observe and record the exact pass/fail counts.
- [ ] Compare against Task 1's baseline: the count must have grown by exactly the number of added tests, with zero pre-existing failures.
- [ ] Confirm no temporary asset folder survives the run (`Assets/AmuseTests_*` and any scratch folder must be gone).
- [ ] Inspect unstaged and staged diffs separately; confirm only intended files changed and no unexpected `.meta` or GUID churn appears.
- [ ] Confirm `Packages/manifest.json`, `Packages/packages-lock.json`, and `Packages/vpm-manifest.json` are unchanged.

**Never claim a test passed unless it was run and its result observed.**

---

## Execution record — 2026-08-19

**Status: Tasks 1–11 complete. Suite 629 → 666, all green.**

| Task | Outcome |
| --- | --- |
| 1 | Base `016f3d2`, clean tree. `AlphaFieldProvider` confirmed declaration-only (4 hits, all in `AlphaSemanticsResolver.cs`). `Editor/Analysis` confirmed free of any `UnityEditor` reference. **Baseline 629/629 green.** |
| 2 | **Genuine RED**: `CS0234: namespace 'Host' does not exist`. |
| 3 | **GREEN 3/3.** Alpha bytes copy straight across; the row-order test passed on the first implementation, confirming Experiment 4's bottom-to-top finding. |
| 4 | Format refusals — **all passed on first run**, as predicted: a positive allow-list already refuses everything outside it. Strengthened afterwards to assert the fixture really imported in the format under test and is readable, so no test can pass for the wrong reason. |
| 5 | Structural/identity refusals — **all passed on first run.** |
| 5b | Destroyed-object refusal — **passed on first run**, as the review anticipated: the Unity-overloaded `texture == null` guard from Task 3 is what refuses. No RED manufactured. |
| 6 | Malformed-input throws — **passed on first run** (the guards were written in Task 3). |
| 7 | Class 1 / Class 2 characterization — **all passed on first run.** |
| 8 | **GREEN 4/4** integration tests against the real classifier. |
| 9 | Architecture guard — **passed on first run**, together with its permanent negative control. |
| 10 | Scope audit clean; see below. |
| 11 | **Full EditMode 666/666, 0 failed, 31.9 s.** |

### Genuine red/green versus pass-immediately

Only **Task 2/3** produced an observed RED (the type did not exist) followed by GREEN. Every
other test class passed on first run and is recorded as such: refusal-boundary (Tasks 4, 5),
characterization (Task 7), architecture-guard (Task 9), malformed-contract (Task 6), and the
destroyed-object guard (Task 5b). Per the approved TDD rule none was converted into a
manufactured RED by breaking production code.

Task 8's integration tests are counted as genuine green-after-implementation: they exercise
a chain that did not previously exist end to end.

### Two fixture corrections during execution

Neither changed production behaviour:

1. `ColourChannel_Refuses(TextureChannel)` failed to compile — a public NUnit method cannot
   take the internal `TextureChannel`. Replaced with one parameterless test looping the
   three channels.
2. Requesting a format through `GetDefaultPlatformTextureSettings()` makes Unity log
   *"not valid with the current texture type 'Default'"* for DXT5, BC7, DXT5Crunched, and
   ARGB16, which NUnit fails on as an unhandled log message. Switched to the real
   `"Standalone"` platform override, which fixed all four.

### ARGB32 log handling — corrected at the review gate

`ARGB32` still drew the same importer complaint on Standalone while producing the format
anyway. The first fix set `LogAssert.ignoreFailingMessages` for that test; the review gate
required an exact `LogAssert.Expect` where the message is stable, or a narrowly scoped and
reliably restored suppression.

**Neither was needed.** The complaint is emitted by the *importer*, and the producer opens
no importer — so how an asset came to be in a given format is outside its contract. The test
now builds the ARGB32 texture with `AssetDatabase.CreateAsset`, which yields a real project
asset that is ARGB32, readable, single-mip, with a resolvable identity and exact alpha
(measured: 128 and 254). No `LogAssert` call and no `UnityEngine.TestTools` import remains
anywhere in the suite, and the fixture is a more faithful test of the producer's actual
contract than the importer route was.

Re-validated after the correction: focused **37/37**, full suite **666/666**, and the Unity
console reports **zero errors and zero warnings**.

### Task 10 audit results

- Production file: **zero** code references to `TextureImporter`, `AssetImporter`,
  `AssetDatabase`, `alphaSource`, `swizzle`, `SaveAndReimport`, or an `isReadable`
  assignment. The only matches are two XML doc comments.
- Exactly two catch clauses: `ArgumentException` (scoped to the `GetPixels32(0)` read) and
  `MissingReferenceException` (wrapping every Unity-object read). No `catch (Exception)`,
  no bare `catch`, no `catch (UnityException)`.
- Allow-list is exactly `RGBA32`, `ARGB32`, `Alpha8`, `RGB24`.
- `Editor/Host/` contains exactly one file. No cache: the instance dictionary holds resolved
  identities only, and `SeparateCalls_ReturnIndependentFields` proves a fresh
  `AlphaTextureData` per call.
- Both `return true` sites are reachable only with a non-null field; `field = null` is set at
  method entry and again in the catch.
- **No natural fixture exists** for zero dimensions or a `GetPixels32` length mismatch:
  Unity 2022.3 does not produce either state through the approved architecture once the
  positive preconditions pass. Per Correction 3 the guards are retained and verified here by
  code review; no test seam, mock, reflection, or corruption was introduced.
- No temporary asset folder survives; `Assets/` has no subfolders. Console shows only the
  two deliberately-ignored ARGB32 importer complaints.

### Stop conditions

**None fired.**

---

## Review handoff

The plan ends here. Do not commit, push, open a PR, or change repository settings.

Report: what changed; the observed test counts before and after; which tests passed on
first run and why each is legitimately a pass-immediately category; which behaviour
genuinely required red/green; whether the Class 2 invariance claims held; the three
`alphaSource` field results; the fail-closed read behaviour and whether the destroyed-texture
fixture was deterministic; any stop condition that fired; the exact working-tree status;
and confirmation that the private testbed was neither used nor modified.
