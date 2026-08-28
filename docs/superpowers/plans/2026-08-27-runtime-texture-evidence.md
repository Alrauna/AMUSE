# Runtime Texture Evidence Implementation Plan

> **Execution:** This plan is executed **serially in the current Claude Code chat**, task by task, in the order below. No subagent, parallel dispatch, or worktree isolation is authorized; do not delegate any task. Steps use checkbox (`- [ ]`) syntax for tracking. **Implementation output stays unstaged and uncommitted** for controller review — no `git add`, no commit, no push, no PR at any point.

**Goal:** Let AMUSE obtain alpha evidence from ordinary imported Unity textures that are non-readable, block-compressed, and mipmapped, by replacing the `GetPixels32` acquisition route with a gated GPU texel-fetch predicate that captures every declared mip, and by carrying that chain through the six seams that today carry one grid.

**Architecture:** One new internal type `AlphaMipChain` wraps an ordered chain of existing `AlphaTextureData`. `AlphaFieldProvider` keeps its role and changes its returned value; `AlphaResolution` classifies every level and combines outcomes. `UnityAlphaFieldEvidence` gains a private `TryAcquireLevel` GPU core beneath its identity and policy gates, plus internal pure gate/validation predicates and one AppDomain-local host-capability latch. The existing predicate shader moves from the research package into the product with its `.meta` and GUID intact.

**Tech Stack:** Unity 2022.3.22f1, C#, NUnit EditMode tests, existing `Alrauna.Amuse.Editor`, `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` assemblies. No new dependency, assembly, package metadata, or NDMF configuration change.

**Spec:** `docs/superpowers/specs/2026-08-27-runtime-texture-evidence-design.md` — authoritative, committed at `7f1c8a7`.

**Plan status:** awaiting controller approval. Do not begin Task 1 until approved.

---

## Global Constraints

### Correctness invariants

- `AlphaMipChain` guarantees **shape only**: non-empty, mip 0 first, no null element, and `w[i] == max(1, w[i-1] >> 1)` and `h[i] == max(1, h[i-1] >> 1)` per axis independently. It does **not** require termination at 1x1 and deliberately accepts a correctly shaped prefix such as `8x8 -> 4x4` (spec §4.4).
- **Completeness** — that the chain is every level the sampler may select — belongs to the `AlphaFieldProvider` contract and the capture loop, never to the constructor. Production constructs the chain only after exactly `texture.mipmapCount` successful level captures.
- Outcome precedence over a chain: any `MustRemainTransparent` wins; otherwise any `Unknown` wins; otherwise `ProvenOpaque`. Early exit on `MustRemainTransparent` only — **never** on `Unknown`.
- The empty chain is unrepresentable, so `ProvenOpaque` can never be reached vacuously.
- `TriangleAlphaClassifier` and `AlphaTextureData` do not change.
- `TryGetUniformOutcome` gains no new uniform path. Do **not** add "every level is `IsFullyOpaque`, therefore `Uniform(ProvenOpaque)`".
- `ResolveScaledSample` for `k < 1` still reads **zero bytes**: it calls the provider, checks the chain is non-null, and returns `Uniform(MustRemainTransparent)`.
- Admitted formats, exactly: `RGBA32`, `ARGB32`, `Alpha8`, `RGB24`, `DXT5`, `BC7`. Do not add `DXT5Crunched`, `ARGB4444`, ASTC, any float format, or anything else.
- Active build target gate, exactly: `BuildTarget.StandaloneWindows64`.
- Channel gate, exactly: `TextureChannel.Alpha`.
- Every byte read back must be exactly `0` or `255`; the allocated target's `graphicsFormat` must be exactly `GraphicsFormat.R8_UNorm`.
- `Editor/Analysis/` must contain no `UnityEditor` identifier. `UnityAlphaFieldEvidenceTests.AnalysisNamespace_HasNoDependencyOnTheUnityEditorNamespace` enforces this and will fail if `AlphaMipChain.cs` names `UnityEditor`.

### Scope boundaries — do not implement

- No compatibility adapter, second `AlphaFieldProvider`, generic GPU backend, injectable capture interface, factory, registry, service, texture-evidence cache, or temporary abstraction to ease sequencing.
- No mesh, submesh, material-conversion, or vertical-slice work.
- No NDMF phase, plugin, or `Configure()` change.
- No change to `TriangleAlphaClassifier.cs`, `MaterialSemantics`, the Poiyomi or lilToon production frontends, package metadata, or any `.asmdef`.
- No importer, quality-setting, streaming-state, scene, or prefab mutation **in production**. Tests follow the synthetic-fixture boundary below.
- No RGB or non-alpha channel support; no trilinear, anisotropic, derivative, LOD, or streaming sampling semantics.
- No Android/Quest support; no second shader; no new performance framework, budget, counter, or telemetry.
- No Census Lab use of any kind.

### Synthetic-fixture importer policy

This is the single authority on importer configuration. It is deliberately a
boundary, not a blanket ban, because Tasks 5 and 6 must import textures whose
mipmapped, non-readable, compressed state is exactly the thing under test.

**Permitted:**

- Configuring `TextureImporter` settings on a **newly created synthetic asset**
  that the test itself just wrote into its **own dedicated temporary test folder**
  under `Assets/` — for example `Assets/AmuseTests_AlphaField`,
  `Assets/AmuseTests_BuildLowerMip`, or the folder an existing suite already owns.
  That folder and every asset in it must be deleted in `[TearDown]` or in the
  test's `finally`, whether or not assertions passed.

**Never permitted, in production or in tests:**

- Production mutating any importer setting, for any reason.
- Mutating an **existing, user-owned, source-avatar, Census, or repository fixture**
  asset's importer — or its pixels — to obtain evidence or to induce a refusal.
- Changing project or global settings: `QualitySettings`, the active build target,
  global streaming state, or any global or per-texture mipmap limit.

The distinction is authorship, not API. Writing a fresh PNG into a folder the test
owns and choosing how it imports is fixture construction. Reaching into an asset the
test did not create is evidence tampering.

### Forbidden test techniques

None of the following may appear in any test this plan produces:

- changing `EditorUserBuildSettings.activeBuildTarget`;
- changing `QualitySettings`, global streaming state, or any mipmap limit;
- mutating an existing, user-owned, source-avatar, Census, or repository fixture
  asset's importer or pixels, whether to gain evidence or to induce a refusal;
- moving, renaming, deleting, or replacing the production shader to make it missing;
- forcing or faking `SystemInfo` device capabilities;
- manufacturing an `AsyncGPUReadback` error;
- setting the host-capability latch through a test-only setter, reflection, injection, or an override hook;
- any fake or injected GPU backend.

Configuring a newly created synthetic asset's importer inside a test-owned temporary
folder is **not** on this list; it is governed by the policy above.

For those paths the plan uses production-called pure predicates or documented structural review, exactly as spec §13.4-§13.6 approves.

### Process

- Do not stage, commit, push, or open a PR. Leave every implementation change in the working tree.
- Do not touch any branch other than `feat/runtime-texture-evidence`.
- Before reporting any Unity test result, enumerate Unity instances read-only and select only the instance whose normalized `Application.dataPath` equals the normalized `<repo-root>/Assets`. A case-only match is not identity.
- Inspect the complete `Packages/manifest.json` and `Packages/packages-lock.json` diff before restoring host toolchain churn; restore only when the entire diff is exactly that machine-generated state and nothing intentional shares those files.
- Each new `.cs` file is one logical unit with its Unity-generated `.meta`. A new folder also generates a `.meta`.

---

## File map

### Production — `Packages/com.alrauna.amuse/`

| File | Change | Responsibility |
| --- | --- | --- |
| `Editor/Analysis/AlphaMipChain.cs` | **Add** | The invariant-bearing ordered mip chain. Host-neutral; names no `UnityEditor` type. |
| `Editor/Host/Shaders/AmuseAlphaExactOne.shader` | **Move in** | The exactly-one predicate shader, moved from the research package with its `.meta` and GUID `85ccb222632d847b6b653f0e05b1ee97`. Body unchanged, including the green diagnostic channel. |
| `Editor/Analysis/AlphaSemanticsResolver.cs` | Modify | `AlphaFieldProvider` returns `AlphaMipChain`; `AlphaResolution` stores a chain and aggregates across levels; `ResolveScaledSample` and `ResolveSampled` parameter types. |
| `Editor/Host/UnityAlphaFieldEvidence.cs` | Modify | Dictionary and both signatures; internal gate/validation predicates; private `TryAcquireLevel` GPU core; the AppDomain latch; `GetPixels32` removed. |
| `Editor/Host/UnityMaterialEvidenceCapture.cs` | Modify | `CapturedTextureEvidence.AlphaChannel` property type (`:263`), constructor parameter (`:274`), capture local (`:989`). |
| `Editor/Host/UnityRendererAlphaAnalysis.cs` | Modify | Provider lambda (`:503-510`) and `GatherAlphaFields` (`:575-597`) types. |
| `Editor/Build/AmusePlatformFinishPlugin.cs` | Modify | `AlphaFields` local function parameter type (`:466-476`). |
| `Editor/Semantics/UnityTextureEvidence.cs` | Modify | Delete `texture.mipmapCount > 1 ||` from `TryGetSampling` and update its doc. |
| `Editor/Analysis/AdmittedMaterialStates.cs` | Modify | Comment only (`:175`): name `AlphaMipChain`. |

### Research — `Packages/com.alrauna.amuse.research/`

| File | Change | Responsibility |
| --- | --- | --- |
| `Tests/Editor/Calibration/AlphaExactOneProbe.shader` | **Move out** | Deleted from here by the move; the `.meta` travels with it. |
| `Tests/Editor/Calibration/AlphaEvidenceProbe.cs` | Modify | `ShaderPath` literal replaced by `UnityAlphaFieldEvidence.ShaderAssetPath`; both the predicate route and `TryCaptureRawAlphaDiagnostic` then load the product asset. |

### Tests — `Packages/com.alrauna.amuse/Tests/Editor/`

| File | Change | Responsibility |
| --- | --- | --- |
| `Analysis/AlphaMipChainTests.cs` | **Add** | Constructor invariants and defensive copy. |
| `Analysis/AlphaSemanticsResolverTests.cs` | Modify | Provider lambdas; chain-shaped fixtures; the full aggregation/precedence matrix. |
| `Analysis/AdmittedMaterialStatesTests.cs` | Modify | `NoAlphaFields` signature (`:472-479`); `ClassifiedResolution` fixture (`:1186-1192`). |
| `Host/UnityAlphaFieldEvidenceTests.cs` | Modify | Chain-shaped assertions; new GPU-route cases; gate-predicate and validator suites; latch integration assertion. |
| `Host/AlphaEvidenceClassifierIntegrationTests.cs` | Modify | Chain-shaped evidence; a lower-mip counterexample case. |
| `Host/UnityMaterialEvidenceCaptureTests.cs` | Modify | `AlphaChannel` assertions at `:357-358` and `:395-397` become `AlphaChannel[0]`. |
| `Host/RendererAlphaAnalysisIntegrationTests.cs` | Modify | Repurpose `ANonReadableAlphaTextureRefusesOnlyItsOwnSubmesh`; add the renderer-level lower-mip case and its dedicated mesh and texture fixtures. |
| `Build/AmusePlatformFinishPluginTests.cs` | Modify | One build-handoff lower-mip regression plus its five test-local helpers (`OddBoundaryAlphaPixels`, `UniformOpaqueAlphaPixels`, `ImportLowerMipTexture`, `SampledAlphaMaterial`, `BuildLowerMipBuildMesh`) and a test-local temp asset folder. No class-wide `SetUp`/`TearDown` is added. |
| `Semantics/UnityTextureEvidenceTests.cs` | Modify | `TryGetSampling_MipmappedTexture_IsRefused` inverts to an admission case; bias, Trilinear, and wrap cases unchanged. |
| `Analysis/TriangleAlphaClassifierTests.cs` | **Unchanged** | Its remaining green state is the evidence that the classifier did not change. |

### Counts

- **2 added production files** (`AlphaMipChain.cs`, and the moved `AmuseAlphaExactOne.shader`), **7 modified production files**, **1 modified research file**, **1 moved-out research shader**.
- **1 added test file** (`AlphaMipChainTests.cs`), **8 modified test files**,
  including `Build/AmusePlatformFinishPluginTests.cs`.
- **`.meta` accounting: 3 new** — `Editor/Analysis/AlphaMipChain.cs.meta`, `Editor/Host/Shaders.meta` (new folder), `Tests/Editor/Analysis/AlphaMipChainTests.cs.meta` — and **1 moved**, the shader's own `.meta`.
- Exactly **one** shader asset exists at completion. None is created and none is deleted.

---

## How compilation is preserved during the six-seam migration

`AlphaFieldProvider` is a delegate. Changing its `out` parameter type breaks every implementation and every call site in the same compile. There is no way to stage that across several compilable tasks **without** a compatibility adapter or a second provider, and both are forbidden by the specification and by this plan's scope boundaries.

Therefore **Task 2 migrates all six seams in one task**, and keeps behaviour identical by having the producer wrap its existing single grid in a one-element chain:

```csharp
field = new AlphaMipChain(new[] { new AlphaTextureData(width, height, alpha) });
```

With a one-element chain the Task 2 aggregation loop returns exactly what today's single `Classify` call returns, so every existing behavioural test stays green through mechanical type edits only. Multi-mip acquisition arrives later, in Task 5, against seams that already carry the right type. This is the smallest sequencing that never introduces an adapter and never leaves the repository uncompilable.

Every other task compiles independently:

- Task 1 adds a type nothing consumes yet.
- Task 3 moves an asset and re-points one research literal.
- Task 4 adds internal predicates consumed by Task 5 **within this plan**; leaving any of them without a production caller at plan end is a defect, not an acceptable outcome.
- Tasks 5-7 change behaviour behind seams whose types already settled in Task 2.

---

## Test execution

Run focused suites during tasks and the full suite at the end. Discover and pin the Unity instance by exact normalized `dataPath` before every run.

| Stage | Scope |
| --- | --- |
| Per task | The named NUnit test class or explicit test names for that task only. |
| After Task 2 | `Alrauna.Amuse.Tests.Editor` Analysis + Host suites, proving single-mip behaviour is unchanged. |
| After Task 3 | `Alrauna.Amuse.Research.Tests.Editor` calibration suite, proving the moved shader still drives every characterization case. |
| After Task 6 | Semantics, both Host integration suites, and the Build suite. |
| Task 7 | Full EditMode run of both assemblies, plus Console inspection. |

---

## Task 1: `AlphaMipChain` and its constructor invariants

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaMipChain.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaMipChainTests.cs`

**Interfaces:**
- Consumes: `AlphaTextureData` from `Editor/Analysis/TriangleAlphaClassifier.cs` (`Width`, `Height`).
- Produces: `internal sealed class AlphaMipChain` with `internal AlphaMipChain(IReadOnlyList<AlphaTextureData> levelsFromMipZero)`, `internal int Count { get; }`, and `internal AlphaTextureData this[int index] { get; }`. Tasks 2-7 rely on exactly these three members.

- [ ] **Step 0: Record the timing baseline, before any edit**

The working tree is still unmodified at this point, so this is the only moment a
true baseline can be taken. Run `RendererAlphaAnalysisIntegrationTests` and
`AmusePlatformFinishPluginTests` on the pinned Unity instance and record each
suite's wall-clock duration and test count. Task 7, Step 3 compares against these
numbers; without them, no before/after claim may be made.

Record them in the session transcript only. **Do not** create a results file, a
benchmark harness, or any committed artefact.

- [ ] **Step 1: Write the failing tests**

Create `AlphaMipChainTests.cs`:

```csharp
using System;
using Alrauna.Amuse.Editor.Analysis;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    /// <summary>
    /// The chain guarantees shape and nothing else. An empty chain is the single
    /// most dangerous value it could admit: it would make "every mip is opaque"
    /// vacuously true and turn the conjunction into an unconditional ProvenOpaque.
    /// </summary>
    public sealed class AlphaMipChainTests
    {
        private static AlphaTextureData Level(int width, int height, byte value)
        {
            var bytes = new byte[width * height];
            for (var index = 0; index < bytes.Length; index++)
            {
                bytes[index] = value;
            }

            return new AlphaTextureData(width, height, bytes);
        }

        [Test]
        public void NullListThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new AlphaMipChain(null));
        }

        [Test]
        public void EmptyListThrows()
        {
            Assert.Throws<ArgumentException>(
                () => new AlphaMipChain(Array.Empty<AlphaTextureData>()));
        }

        /// <summary>
        /// The index must appear in the message: a chain of a dozen levels gives a
        /// bare ArgumentNullException nothing to say about which one is missing.
        /// </summary>
        [Test]
        public void NullElementThrowsAndIdentifiesTheOffendingIndex()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new AlphaMipChain(
                    new[] { Level(4, 4, 255), Level(2, 2, 255), null }));

            Assert.That(exception.Message, Does.Contain("2"),
                "The message must name the index of the null level.");
        }

        [Test]
        public void SingleLevelChainIsAccepted()
        {
            var chain = new AlphaMipChain(new[] { Level(4, 4, 255) });

            Assert.That(chain.Count, Is.EqualTo(1));
            Assert.That(chain[0].Width, Is.EqualTo(4));
        }

        [Test]
        public void SquareChainHalvingToOneIsAccepted()
        {
            var chain = new AlphaMipChain(new[]
            {
                Level(8, 8, 255), Level(4, 4, 255),
                Level(2, 2, 255), Level(1, 1, 255)
            });

            Assert.That(chain.Count, Is.EqualTo(4));
            Assert.That(chain[3].Width, Is.EqualTo(1));
            Assert.That(chain[3].Height, Is.EqualTo(1));
        }

        /// <summary>
        /// Each axis halves independently and clamps at one. A single shared shift
        /// would reject this legitimate non-square chain.
        /// </summary>
        [Test]
        public void NonSquareChainClampingOneAxisIsAccepted()
        {
            var chain = new AlphaMipChain(new[]
            {
                Level(16, 4, 255), Level(8, 2, 255),
                Level(4, 1, 255), Level(2, 1, 255), Level(1, 1, 255)
            });

            Assert.That(chain.Count, Is.EqualTo(5));
            Assert.That(chain[2].Width, Is.EqualTo(4));
            Assert.That(chain[2].Height, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedDimensionsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new AlphaMipChain(new[]
            {
                Level(8, 8, 255), Level(4, 4, 255), Level(4, 4, 255)
            }));
        }

        [Test]
        public void SkippedLevelIsRejected()
        {
            Assert.Throws<ArgumentException>(
                () => new AlphaMipChain(new[] { Level(8, 8, 255), Level(2, 2, 255) }));
        }

        [Test]
        public void ReversedOrderIsRejected()
        {
            Assert.Throws<ArgumentException>(
                () => new AlphaMipChain(new[] { Level(4, 4, 255), Level(8, 8, 255) }));
        }

        /// <summary>
        /// Deliberately valid. The type cannot see mipmapCount and so cannot prove
        /// completeness; a correctly shaped prefix is in-domain, and completeness is
        /// the provider contract's obligation.
        /// </summary>
        [Test]
        public void CorrectlyShapedPrefixIsAccepted()
        {
            var chain = new AlphaMipChain(new[] { Level(8, 8, 255), Level(4, 4, 255) });

            Assert.That(chain.Count, Is.EqualTo(2));
        }

        [Test]
        public void MutatingTheSuppliedListDoesNotChangeTheChain()
        {
            var levels = new[] { Level(2, 2, 255), Level(1, 1, 255) };
            var chain = new AlphaMipChain(levels);

            levels[0] = Level(2, 2, 0);

            Assert.That(chain[0].IsFullyOpaque, Is.True);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the `AlphaMipChainTests` class in `Alrauna.Amuse.Tests.Editor`.
Expected: compile failure — `AlphaMipChain` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `AlphaMipChain.cs`. Do not name `UnityEditor` anywhere in this file.

```csharp
using System;
using System.Collections.Generic;

namespace Alrauna.Amuse.Editor.Analysis
{
    /// <summary>
    /// One texture's ordered alpha mip chain: the existing per-level grids the
    /// classifier already consumes, mip 0 first.
    /// <para>
    /// It guarantees <em>shape</em> and nothing else: non-empty, ordered, no null
    /// element, and each level's width and height independently equal
    /// <c>max(1, previous &gt;&gt; 1)</c>. Non-emptiness is the load-bearing
    /// invariant: an empty chain would make "every level is ProvenOpaque"
    /// vacuously true and turn the conjunction in <see cref="AlphaResolution"/>
    /// into an unconditional proof of opacity.
    /// </para>
    /// <para>
    /// It deliberately does <strong>not</strong> require the chain to reach 1x1. A
    /// correctly shaped prefix is in-domain, because this type cannot see
    /// <c>mipmapCount</c> and so cannot prove completeness. That the chain is every
    /// level the sampler may select is the <see cref="AlphaFieldProvider"/>
    /// contract's obligation and the capture loop's, never this constructor's.
    /// </para>
    /// <para>
    /// It is not a texture IR: it carries no format, channel, colour space,
    /// sampler, source identity, or transformation, and cannot represent a
    /// magnitude or a non-alpha channel.
    /// </para>
    /// </summary>
    internal sealed class AlphaMipChain
    {
        private readonly AlphaTextureData[] _levels;

        internal AlphaMipChain(IReadOnlyList<AlphaTextureData> levelsFromMipZero)
        {
            if (levelsFromMipZero == null)
            {
                throw new ArgumentNullException(nameof(levelsFromMipZero));
            }
            if (levelsFromMipZero.Count == 0)
            {
                throw new ArgumentException(
                    "A mip chain must contain at least mip 0.",
                    nameof(levelsFromMipZero));
            }

            var levels = new AlphaTextureData[levelsFromMipZero.Count];
            for (var index = 0; index < levelsFromMipZero.Count; index++)
            {
                var level = levelsFromMipZero[index];
                if (level == null)
                {
                    throw new ArgumentNullException(
                        nameof(levelsFromMipZero),
                        "Mip level " + index + " is null.");
                }

                if (index > 0)
                {
                    var previous = levels[index - 1];
                    if (level.Width != Halved(previous.Width) ||
                        level.Height != Halved(previous.Height))
                    {
                        throw new ArgumentException(
                            "Mip level " + index + " must be " +
                            Halved(previous.Width) + "x" + Halved(previous.Height) +
                            "; each axis halves independently with a floor of one.",
                            nameof(levelsFromMipZero));
                    }
                }

                levels[index] = level;
            }

            _levels = levels;
        }

        internal int Count => _levels.Length;

        internal AlphaTextureData this[int index] => _levels[index];

        private static int Halved(int size)
        {
            var halved = size >> 1;
            return halved < 1 ? 1 : halved;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run `AlphaMipChainTests`. Expected: all pass.

- [ ] **Step 5: Run the namespace guard**

Run `UnityAlphaFieldEvidenceTests.AnalysisNamespace_HasNoDependencyOnTheUnityEditorNamespace`.
Expected: PASS. It scans `Editor/Analysis` for the `UnityEditor` identifier, and the new file must not trip it.

- [ ] **Step 6: Confirm the `.meta`**

Confirm Unity generated `AlphaMipChain.cs.meta` and `AlphaMipChainTests.cs.meta`. Do not stage or commit.

---

## Task 2: Migrate the six seams, preserving single-mip behaviour

Every seam moves in one task because `AlphaFieldProvider` is a delegate and no compatibility adapter is permitted. The producer keeps `GetPixels32` and every current refusal; it wraps its single grid in a one-element chain. Behaviour must be **identical** at the end of this task.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs` (`:34-37`, `:48`, `:56`, `:100-108`, `:157-165`, `:230-252`, `:254-280`)
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs` (`:34`, `:56`, `:80-84`, `:154-157`, `:176-181`)
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs` (`:263`, `:274`, `:989`)
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs` (`:503-510`, `:575-597`)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (`:466-476`)
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs` (`:175`, comment only)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs` (`:472-479`, `:1186-1192`)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs` (`:137-156` and every assertion reading a field)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs` (`:357-358`, `:395-397`)

**Interfaces:**
- Consumes: `AlphaMipChain` from Task 1.
- Produces: `internal delegate bool AlphaFieldProvider(TextureSourceId source, TextureChannel channel, out AlphaMipChain chain)`; `AlphaResolution.Classified(AlphaMipChain chain, AlphaSamplingSettings sampling)`; `UnityAlphaFieldEvidence.TryCapture(Texture texture, out TextureSourceId source, out AlphaMipChain chain)`; `UnityAlphaFieldEvidence.TryGetAlphaField(TextureSourceId source, TextureChannel channel, out AlphaMipChain chain)`; `CapturedTextureEvidence.AlphaChannel` of type `AlphaMipChain`; `UnityRendererAlphaAnalysis.GatherAlphaFields(IReadOnlyList<CapturedAlphaMaterial>)` returning `IReadOnlyDictionary<TextureSourceId, AlphaMipChain>`. Tasks 5-7 rely on exactly these.

- [ ] **Step 1: Write the failing aggregation tests**

Add to `AlphaSemanticsResolverTests.cs`. These need no GPU: chains are built directly.

```csharp
        private static AlphaMipChain Chain(params AlphaTextureData[] levels)
        {
            return new AlphaMipChain(levels);
        }

        /// <summary>
        /// 2x2 fully opaque over 1x1 fully opaque.
        /// </summary>
        private static AlphaMipChain AllOpaqueChain()
        {
            return Chain(Field(2, 2, 255), Field(1, 1, 255));
        }

        /// <summary>
        /// Mip 0 fully opaque, mip 1 fully non-opaque. Mip-0-only reasoning would
        /// call this ProvenOpaque; the conjunction must not.
        /// </summary>
        private static AlphaMipChain OpaqueThenTransparentChain()
        {
            return Chain(Field(2, 2, 255), Field(1, 1, 0));
        }

        [Test]
        public void EveryLevelOpaqueIsProvenOpaque()
        {
            var resolution = AlphaResolution.Classified(
                AllOpaqueChain(), new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp));

            Assert.That(
                resolution.Classify(OpaqueCornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void ALowerLevelTransparencyDefeatsAMipZeroOpaqueProof()
        {
            var resolution = AlphaResolution.Classified(
                OpaqueThenTransparentChain(),
                new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp));

            Assert.That(
                resolution.Classify(OpaqueCornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void MipZeroTransparencyIsNotOverriddenByALowerOpaqueLevel()
        {
            var resolution = AlphaResolution.Classified(
                Chain(Field(2, 2, 0), Field(1, 1, 255)),
                new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp));

            Assert.That(
                resolution.Classify(OpaqueCornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        /// <summary>
        /// The transparent level is deliberately LAST. An implementation that
        /// returns on the first Unknown reports Unknown and loses the refusal.
        /// </summary>
        [Test]
        public void TransparencyOutranksUnknownEvenWhenItComesLast()
        {
            var resolution = AlphaResolution.Classified(
                Chain(MixedField(), Field(1, 1, 0)),
                new AlphaSamplingSettings(AlphaFilterMode.Bilinear, AlphaWrapMode.Clamp));

            Assert.That(
                resolution.Classify(SpanningTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void OneUnknownLevelWithNoTransparencyIsUnknown()
        {
            var resolution = AlphaResolution.Classified(
                Chain(MixedField(), Field(1, 1, 255)),
                new AlphaSamplingSettings(AlphaFilterMode.Bilinear, AlphaWrapMode.Clamp));

            Assert.That(
                resolution.Classify(SpanningTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.Unknown));
        }

        /// <summary>
        /// A chain whose levels agree is still a classified resolution. Reporting it
        /// as uniform would let the deduplication consumer merge it.
        /// </summary>
        /// <summary>
        /// Every level Unknown. A 4x4 half-opaque grid over a 2x2 half-opaque grid:
        /// the spanning triangle covers both opaque and non-opaque texels at each
        /// level, so neither level decides. ProvenOpaque is the zero value of
        /// TriangleAlphaOutcome, so an implementation that defaults instead of
        /// tracking `sawUnknown` would wrongly report it.
        /// </summary>
        [Test]
        public void EveryLevelUnknownIsUnknown()
        {
            var mip0 = new AlphaTextureData(4, 4, new byte[]
            {
                255, 255, 255, 255,
                255, 255, 255, 255,
                0,   0,   0,   0,
                0,   0,   0,   0
            });
            var resolution = AlphaResolution.Classified(
                Chain(mip0, MixedField()),
                new AlphaSamplingSettings(
                    AlphaFilterMode.Bilinear, AlphaWrapMode.Clamp));

            Assert.That(
                resolution.Classify(SpanningTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.Unknown));
        }

        /// <summary>
        /// Disagreement across levels must not be re-described as uniform either.
        /// </summary>
        [Test]
        public void ADisagreeingChainIsNotAUniformResolution()
        {
            var resolution = AlphaResolution.Classified(
                OpaqueThenTransparentChain(),
                new AlphaSamplingSettings(
                    AlphaFilterMode.Point, AlphaWrapMode.Clamp));

            Assert.That(resolution.TryGetUniformOutcome(out var outcome), Is.False);
            Assert.That(outcome, Is.EqualTo(TriangleAlphaOutcome.Unknown));
        }

        [Test]
        public void AnAgreeingChainIsStillNotAUniformResolution()
        {
            var resolution = AlphaResolution.Classified(
                AllOpaqueChain(), new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp));

            Assert.That(resolution.TryGetUniformOutcome(out var outcome), Is.False);
            Assert.That(outcome, Is.EqualTo(TriangleAlphaOutcome.Unknown));
        }
```

`OpaqueCornerTriangle()` and `TransparentCornerTriangle()` already exist at `:76` and `:90`; there is no `Sampling(...)` helper, so `AlphaSamplingSettings` is constructed directly. Add one new triangle helper beside the existing two — `MixedField()` is 2x2 with the bottom row 255 and the top row 0, so a triangle spanning both rows classifies `Unknown` there while `OpaqueCornerTriangle()` stays wholly inside the opaque row:

```csharp
        /// <summary>
        /// Spans all of UV space, so a half-opaque field cannot decide it.
        /// </summary>
        private static TriangleAlphaInput SpanningTriangle()
        {
            return TriangleAlphaInput.WithUv0(
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector2(0.05f, 0.05f),
                new Vector2(0.95f, 0.05f),
                new Vector2(0.05f, 0.95f));
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run `AlphaSemanticsResolverTests`.
Expected: compile failure — `AlphaResolution.Classified` takes `AlphaTextureData`, not `AlphaMipChain`.

- [ ] **Step 3: Change `AlphaSemanticsResolver.cs`**

Delegate at `:34-37`:

```csharp
    internal delegate bool AlphaFieldProvider(
        TextureSourceId source,
        TextureChannel channel,
        out AlphaMipChain chain);
```

Update the delegate's XML doc so both obligations are explicit. Replace "over the relevant base-level texel domain in bottom-to-top order" with a statement binding the predicate to **every level**, and add the completeness clause:

> ... that for **every level of the returned chain**, in bottom-to-top row-major order, every effective per-texel scalar value is finite and within [0, 1], that byte 255 marks exactly the texels whose value is exactly 1, and that every other byte marks a value strictly below 1. It further attests that the chain is the source's **complete declared mip chain**, mip 0 first: the sampler may select any level and the resolver cannot know which, so an incomplete chain would let an unexamined level escape the proof. `AlphaMipChain` validates shape only and cannot check this; the provider owns it.

In `AlphaResolution`: rename `_field` to `_chain` and type it `AlphaMipChain`; change the private constructor parameter and the `Classified` factory parameter to `AlphaMipChain chain`. The existing null guard keeps its meaning.

Replace the classified arm of `Classify` (`:161-164`):

```csharp
            if (_isUniform)
            {
                return _uniformOutcome;
            }

            // A mip chain is alternative evidence about one configuration, not a
            // set of admitted configurations: the hardware may select any level and
            // AMUSE cannot know which, so one non-opaque level refutes the proof.
            // MustRemainTransparent is absorbing, so returning on it cannot change
            // the result. Unknown must NOT exit early - a later level may be
            // MustRemainTransparent, which outranks it.
            var sawUnknown = false;
            for (var index = 0; index < _chain.Count; index++)
            {
                var outcome = TriangleAlphaClassifier.Classify(
                    triangle, _chain[index], _sampling);
                if (outcome == TriangleAlphaOutcome.MustRemainTransparent)
                {
                    return TriangleAlphaOutcome.MustRemainTransparent;
                }
                if (outcome == TriangleAlphaOutcome.Unknown)
                {
                    sawUnknown = true;
                }
            }

            // Never vacuous: AlphaMipChain forbids an empty chain, so the loop body
            // ran at least once.
            return sawUnknown
                ? TriangleAlphaOutcome.Unknown
                : TriangleAlphaOutcome.ProvenOpaque;
```

In `ResolveSampled` and `ResolveScaledSample`, rename the `out var field` locals to `out var chain` and the null checks to `chain == null`. **Do not change either method's logic.** `ResolveScaledSample` still returns `AlphaResolution.Uniform(TriangleAlphaOutcome.MustRemainTransparent)` for `k < 1` without reading a byte. Extend its XML doc with one sentence:

> The evidence contract now bounds the sampled value to [0, 1] at **every** level, so the bound holds whichever level the hardware selects; the lemma is strengthened, not weakened, and still needs no byte of the contents.

- [ ] **Step 4: Change the remaining five seams and the comment**

`UnityAlphaFieldEvidence.cs`: type the dictionary `Dictionary<TextureSourceId, AlphaMipChain>`; change `TryCapture`'s and `TryGetAlphaField`'s `out` parameters to `out AlphaMipChain chain`; at the end of the successful `TryCapture` path, replace the single-field construction with the one-element wrap:

```csharp
                // Single-mip acquisition is unchanged in this task; the chain is
                // one level long and every existing behavioural test must stay
                // green. Multi-mip acquisition arrives with the GPU route.
                chain = new AlphaMipChain(new[]
                {
                    new AlphaTextureData(width, height, alpha)
                });
                return true;
```

`UnityMaterialEvidenceCapture.cs`: `AlphaChannel` property type (`:263`), constructor parameter (`:274`), and the capture local (`:989`) become `AlphaMipChain`. Add to `AlphaChannel`'s doc that the value is the complete declared mip chain.

`UnityRendererAlphaAnalysis.cs`: the lambda's `out AlphaTextureData field` (`:505`) becomes `out AlphaMipChain chain`, and `GatherAlphaFields` returns `IReadOnlyDictionary<TextureSourceId, AlphaMipChain>` with its local dictionary retyped. The `channel == TextureChannel.Alpha` guard is unchanged.

`AmusePlatformFinishPlugin.cs`: `AlphaFields`'s `out AlphaTextureData field` (`:471`) becomes `out AlphaMipChain chain`. Nothing else in the pass changes.

`AdmittedMaterialStates.cs:175`: change `<see cref="AlphaTextureData"/>` to `<see cref="AlphaMipChain"/>` in the sentence about reference-distinct evidence. The rule is unchanged in substance — two chains are no more cheaply provable equivalent than two grids.

- [ ] **Step 5: Update the existing tests mechanically**

`AlphaSemanticsResolverTests.cs`: change `Providing`/`ProvidingNothing` and the four inline lambdas at `:213`, `:282`, `:459`, `:575` to `out AlphaMipChain result`. `Providing` takes an `AlphaMipChain`; add a `Providing(AlphaTextureData)` overload that wraps a single level so existing call sites read unchanged:

```csharp
        private static AlphaFieldProvider Providing(AlphaTextureData field)
        {
            return Providing(new AlphaMipChain(new[] { field }));
        }

        private static AlphaFieldProvider Providing(AlphaMipChain chain)
        {
            return (TextureSourceId source, TextureChannel channel,
                out AlphaMipChain result) =>
            {
                result = chain;
                return true;
            };
        }
```

`AdmittedMaterialStatesTests.cs`: `NoAlphaFields` (`:472-479`) takes `out AlphaMipChain field`; `ClassifiedResolution` (`:1186-1192`) wraps its 1x1 grid in a chain.

`UnityAlphaFieldEvidenceTests.cs`: `TryField` (`:137-156`) returns `out AlphaMipChain`; every assertion that read `field.GetAlpha(...)`, `field.Width`, `field.Height`, `field.IsFullyOpaque` reads `chain[0]. ...`. `AssertSameField` compares `chain[0]` against `chain[0]`. Do not change any assertion's expected value.

`UnityMaterialEvidenceCaptureTests.cs`: `:357-358` becomes `main.Texture.AlphaChannel[0].GetAlpha(0, 0)` and `[0].GetAlpha(3, 3)`; the retained-reference assertions at `:395-397` compare the chain with `Is.SameAs` and read `alpha[0].GetAlpha(...)`.

- [ ] **Step 6: Run the tests to verify they pass**

Run `AlphaMipChainTests`, `AlphaSemanticsResolverTests`, `AdmittedMaterialStatesTests`, `TriangleAlphaClassifierTests`, `UnityAlphaFieldEvidenceTests`, `UnityMaterialEvidenceCaptureTests`, `AlphaEvidenceClassifierIntegrationTests`, `UnityRendererAlphaAnalysisTests`, `RendererAlphaAnalysisIntegrationTests`, `AmusePlatformFinishPluginTests`.
Expected: all pass. `TriangleAlphaClassifierTests` must pass **unmodified** — that is the evidence the classifier did not change.

- [ ] **Step 7: Confirm behaviour is unchanged**

No test's expected value may have been edited in Step 5 — only types and accessor
shape. If any expected value needed changing, stop and report it: it means the
migration altered behaviour, which this task forbids.

**These named existing tests establish one-mip behavioural equivalence** and must be
green with their expectations untouched:

| Test | What it holds fixed |
| --- | --- |
| `UnityAlphaFieldEvidenceTests.SupportedImport_ReportsImportedDimensions` (`:158`) | Dimensions survive the wrap. |
| `UnityAlphaFieldEvidenceTests.SupportedImport_PreservesBottomToTopRowOrder` (`:176`) | Row order is untouched by the chain. |
| `UnityAlphaFieldEvidenceTests.SupportedImport_MarksEveryOtherTexelExactlyOpaque` (`:192`) | Per-texel predicate values are identical. |
| `UnityAlphaFieldEvidenceTests.ReadableAlpha8_IsAdmittedAndExact` (`:309`) | An admitted format's exactness is unchanged. |
| `UnityAlphaFieldEvidenceTests.ReadableArgb32_IsAdmittedAndExact` (`:330`) | Likewise. |
| `UnityAlphaFieldEvidenceTests.ReadableRgb24_IsAdmittedAndFullyOpaque` (`:343`) | Likewise. |
| `UnityAlphaFieldEvidenceTests.RepeatedCalls_ReturnEqualContents` (`:627`) | Determinism is unchanged. |
| `UnityAlphaFieldEvidenceTests.SeparateCalls_ReturnTheCapturedImmutableField` (`:640`) | Immutability of captured evidence is unchanged. |
| `AlphaEvidenceClassifierIntegrationTests.MixedTexture_TriangleInsideTheOpaqueRegion_IsProvenOpaque` (`:144`) | A one-level chain classifies exactly as one grid did. |
| `AlphaEvidenceClassifierIntegrationTests.MixedTexture_TriangleCoveringTheNonOpaqueTexel_MustRemainTransparent` (`:160`) | Likewise, in the refusing direction. |
| `AlphaEvidenceClassifierIntegrationTests.FullyOpaqueTexture_IsProvenOpaqueForAnyTriangle` (`:176`) | Likewise, for the short-circuit path. |
| `RendererAlphaAnalysisIntegrationTests.RendererToPlanSeparatesProvenOpaqueGeometryFromPreservedGeometry` (`:261`) | The whole renderer path is unchanged. |
| `TriangleAlphaClassifierTests` (entire class) | The classifier and `AlphaTextureData` did not change. |

---

## Task 3: Move the predicate shader into the product package

**Files:**
- Move: `Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/AlphaExactOneProbe.shader` (+ `.meta`) → `Packages/com.alrauna.amuse/Editor/Host/Shaders/AmuseAlphaExactOne.shader` (+ `.meta`)
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs` (add the path constant)
- Modify: `Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/AlphaEvidenceProbe.cs` (`:103-105`)

**Interfaces:**
- Produces: `internal const string UnityAlphaFieldEvidence.ShaderAssetPath`. Task 5 and the research probe both use it.

- [ ] **Step 1: Move the asset and its `.meta` together**

Use a plain filesystem move — **never `git mv`**, which would stage the rename. The
`.meta` moves with the asset so the GUID survives; Unity then reconciles the move on
its next refresh without reimporting the asset under a new identity.

```bash
mkdir -p Packages/com.alrauna.amuse/Editor/Host/Shaders
mv Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/AlphaExactOneProbe.shader \
   Packages/com.alrauna.amuse/Editor/Host/Shaders/AmuseAlphaExactOne.shader
mv Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/AlphaExactOneProbe.shader.meta \
   Packages/com.alrauna.amuse/Editor/Host/Shaders/AmuseAlphaExactOne.shader.meta
```

Git records this as an unstaged delete plus an untracked add. That is correct and
expected: **nothing in this plan is ever staged.** Confirm immediately:

```bash
git diff --cached --name-only
```

Expected: empty output, here and after every subsequent step.

- [ ] **Step 2: Verify the GUID survived**

```bash
grep guid Packages/com.alrauna.amuse/Editor/Host/Shaders/AmuseAlphaExactOne.shader.meta
```

Expected: `guid: 85ccb222632d847b6b653f0e05b1ee97`. If it differs, stop — the `.meta` did not travel and the asset identity changed.

- [ ] **Step 3: Edit only the shader's header comment and its name string**

Change the `Shader` declaration to `Shader "Hidden/Alrauna/Amuse/AlphaExactOne"`.

Replace the header comment with a statement of its production role. **Leave the fragment body exactly as it is**, green channel included:

```hlsl
// The AMUSE alpha evidence predicate. Editor-only: it lives under Editor/ so it
// is excluded from player builds and never reaches a built avatar.
//
// It loads ONE explicit mip level by integer texel index and emits the binary
// result of "alpha is exactly one" in RED. Load is a texel fetch: no filtering,
// no mip selection, no wrap.
//
// GREEN carries the raw alpha and is a RESEARCH DIAGNOSTIC ONLY. Production
// renders this shader into a GraphicsFormat.R8_UNorm target, which stores only
// the red component, so green is discarded before any production code sees a
// result: it has no production evidence meaning, no production reader, and no
// production code path. The research characterization renders the same shader
// into a float target to read it, which is why the channel is retained here
// rather than deleted and re-created as a second asset.
```

- [ ] **Step 4: Add the production path constant**

In `UnityAlphaFieldEvidence`:

```csharp
        /// <summary>
        /// The predicate shader's project path. UPM addresses every package as
        /// <c>Packages/&lt;name&gt;/...</c> regardless of where it physically lives,
        /// so this is stable for embedded, local, git and VPM installs alike.
        /// <para>
        /// Shader.Find is deliberately not used: it resolves by shader name, which
        /// this repository does not own, and would silently bind to whichever
        /// asset won a name collision.
        /// </para>
        /// </summary>
        internal const string ShaderAssetPath =
            "Packages/com.alrauna.amuse/Editor/Host/Shaders/AmuseAlphaExactOne.shader";
```

- [ ] **Step 5: Re-point the research probe**

In `AlphaEvidenceProbe.cs`, delete the private `ShaderPath` literal at `:103-105` and replace every one of its four uses (`:116`, `:179`, `:263`, and the `ProbeSupport` load) with `UnityAlphaFieldEvidence.ShaderAssetPath`. Add `using Alrauna.Amuse.Editor.Host;`. The assembly reference and `InternalsVisibleTo` grant already exist, so no `.asmdef` or `AssemblyInfo` change is needed.

Update the class doc: it currently says "nothing in `com.alrauna.amuse` references it". Replace with: the probe now loads the **product** shader, so the characterization and production exercise one asset and the predicate cannot drift.

- [ ] **Step 6: Run the research calibration suite**

Run `AlphaEvidenceCharacterizationTests` in `Alrauna.Amuse.Research.Tests.Editor`.
Expected: all pass, including `TheProductionShapedPathIsReachableOnThisMachine` and `AFloatFieldDefeatsTheExactlyOnePredicateAsAnAttestation` — the latter proves the green diagnostic still works through the moved asset.

- [ ] **Step 7: Confirm the folder `.meta`**

Confirm Unity generated `Editor/Host/Shaders.meta`. Confirm no second shader exists anywhere:

```bash
find Packages -name "*.shader" -not -path "*/Tests/Editor/Semantics/*"
```

Expected: exactly one path, the moved production shader.

---

## Task 4: Internal gate predicates and output validators

Pure, allocation-free, `internal` so `Alrauna.Amuse.Tests.Editor` reaches them through the existing grant at `Editor/AssemblyInfo.cs:3`. **Task 5 wires every one of them.** Any predicate still without a production caller when the plan ends is a defect.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs`

**Interfaces:**
- Produces, all `internal static` on `UnityAlphaFieldEvidence`:
  - `bool IsAdmittedFormat(TextureFormat format)`
  - `bool IsAdmittedBuildTarget(BuildTarget target)`
  - `bool MipResidencyGatesPass(int activeMipmapLimit, bool streamingMipmaps)`
  - `bool AreDimensionsUsable(int width, int height, int mipmapCount)`
  - `bool HostCapabilitiesPass(bool asyncReadback, bool r8Renderable, bool r8Readable, bool sourceSampleable)`
  - `bool IsShaderUsable(bool assetLoaded, bool isSupported)`
  - `bool IsExpectedLevelSize(int width, int height, int expectedWidth, int expectedHeight)`
  - `bool IsExpectedTargetFormat(GraphicsFormat actual, GraphicsFormat expected)`
  - `bool IsExpectedBufferLength(long actualLength, int width, int height)`
  - `bool IsBinaryPredicateBuffer(byte[] bytes)`
  - `bool MatchesExpectedPattern(byte[] actual, byte[] expected)`

All **twelve** are invoked by `TryCapture`, `TryAcquireLevel`, or
`RunHostCapabilityCheck` in Task 5 — seven policy/capability gates
(`IsAdmittedBuildTarget`, `IsAdmittedFormat`, `MipResidencyGatesPass`,
`AreDimensionsUsable`, `HostCapabilitiesPass`, `SourceSamplingGatePasses`,
`IsShaderUsable`) and five output validators (`IsExpectedTargetFormat`,
`IsExpectedLevelSize`, `IsExpectedBufferLength`, `IsBinaryPredicateBuffer`,
`MatchesExpectedPattern`).

`SourceSamplingGatePasses(textureFormat, exactGraphicsFormatSampleable)` was added
during implementation, after a measured contradiction between two pinned spec
clauses: `IsFormatSupported(R8G8B8_UNorm, Sample)` is `False` on this host, and that
is `RGB24`'s reported `graphicsFormat`, so an exact-format requirement refused an
admitted format. Its whole policy is *exact format sampleable -> true; otherwise
RGB24 -> true; otherwise false*. It makes no `SystemInfo` call of its own. See
spec §8 and investigation §10a. Three responsibilities stay separate on purpose:
`IsExpectedLevelSize` compares the two dimensions, `IsExpectedBufferLength` compares
the **actual returned length** against `width * height`, and
`IsBinaryPredicateBuffer` is responsible for exactly one thing — that every returned
byte is `0` or `255`. Folding length into the byte scan is what made the earlier
draft's mismatch branch unreachable, because production allocated the array and then
passed its own `Length` as the expected value. Production must pass the length Unity
returned, before any array of its own exists.

- [ ] **Step 1: Write the failing tests**

Add to `UnityAlphaFieldEvidenceTests.cs`:

```csharp
        // --- Gate predicates: production calls each of these ------------------

        /// <summary>
        /// Genuinely exhaustive: every value the TextureFormat enum currently
        /// declares is compared against the exact admitted set, so a format added
        /// to the enum by a Unity upgrade, or silently added to the allowlist,
        /// fails here rather than passing a hand-picked sample.
        /// </summary>
        [Test]
        public void TheFormatAllowlistIsExactlyTheSixAdmittedFormats()
        {
            var admitted = new HashSet<TextureFormat>
            {
                TextureFormat.RGBA32,
                TextureFormat.ARGB32,
                TextureFormat.Alpha8,
                TextureFormat.RGB24,
                TextureFormat.DXT5,
                TextureFormat.BC7
            };

            var unexpectedlyAdmitted = new List<TextureFormat>();
            var unexpectedlyRefused = new List<TextureFormat>();
            foreach (TextureFormat format in Enum.GetValues(typeof(TextureFormat)))
            {
                var actual = UnityAlphaFieldEvidence.IsAdmittedFormat(format);
                if (actual && !admitted.Contains(format))
                {
                    unexpectedlyAdmitted.Add(format);
                }
                if (!actual && admitted.Contains(format))
                {
                    unexpectedlyRefused.Add(format);
                }
            }

            Assert.That(
                unexpectedlyAdmitted, Is.Empty,
                "Formats admitted without characterization.");
            Assert.That(
                unexpectedlyRefused, Is.Empty,
                "Admitted formats the predicate refuses.");
            Assert.That(admitted.Count, Is.EqualTo(6));
        }

        [TestCase(BuildTarget.StandaloneWindows64, true)]
        [TestCase(BuildTarget.StandaloneWindows, false)]
        [TestCase(BuildTarget.StandaloneOSX, false)]
        [TestCase(BuildTarget.StandaloneLinux64, false)]
        [TestCase(BuildTarget.Android, false)]
        [TestCase(BuildTarget.iOS, false)]
        public void OnlyStandaloneWindows64IsAdmitted(BuildTarget target, bool admitted)
        {
            Assert.That(
                UnityAlphaFieldEvidence.IsAdmittedBuildTarget(target), Is.EqualTo(admitted));
        }

        [TestCase(0, false, true)]
        [TestCase(0, true, false)]
        [TestCase(1, false, false)]
        [TestCase(1, true, false)]
        [TestCase(2, false, false)]
        public void TheMipResidencyGateAdmitsOnlyAnUnlimitedNonStreamingTexture(
            int activeMipmapLimit, bool streaming, bool admitted)
        {
            Assert.That(
                UnityAlphaFieldEvidence.MipResidencyGatesPass(activeMipmapLimit, streaming),
                Is.EqualTo(admitted));
        }

        [TestCase(4, 4, 3, true)]
        [TestCase(1, 1, 1, true)]
        [TestCase(0, 4, 3, false)]
        [TestCase(4, 0, 3, false)]
        [TestCase(4, 4, 0, false)]
        [TestCase(-1, 4, 3, false)]
        public void DimensionsMustBePositive(int w, int h, int mips, bool usable)
        {
            Assert.That(
                UnityAlphaFieldEvidence.AreDimensionsUsable(w, h, mips), Is.EqualTo(usable));
        }

        [TestCase(true, true, true, true, true)]
        [TestCase(false, true, true, true, false)]
        [TestCase(true, false, true, true, false)]
        [TestCase(true, true, false, true, false)]
        [TestCase(true, true, true, false, false)]
        public void EveryHostCapabilityIsRequired(
            bool async, bool render, bool read, bool sample, bool pass)
        {
            Assert.That(
                UnityAlphaFieldEvidence.HostCapabilitiesPass(async, render, read, sample),
                Is.EqualTo(pass));
        }

        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, false)]
        public void TheShaderMustBeBothLoadedAndSupported(
            bool loaded, bool supported, bool usable)
        {
            Assert.That(
                UnityAlphaFieldEvidence.IsShaderUsable(loaded, supported), Is.EqualTo(usable));
        }

        // --- Output validators: production calls each of these ----------------

        [TestCase(8, 4, 8, 4, true)]
        [TestCase(8, 4, 4, 8, false)]
        [TestCase(8, 4, 8, 2, false)]
        [TestCase(8, 4, 16, 4, false)]
        public void ALevelMustMatchTheRequestedSize(
            int w, int h, int expectedW, int expectedH, bool ok)
        {
            Assert.That(
                UnityAlphaFieldEvidence.IsExpectedLevelSize(w, h, expectedW, expectedH),
                Is.EqualTo(ok));
        }

        /// <summary>
        /// Unity may substitute a format it prefers for a temporary target. A
        /// substituted target silently changes what the readback means, so an
        /// inexact match is a refusal.
        /// </summary>
        [Test]
        public void TheTargetFormatMustMatchExactly()
        {
            Assert.That(
                UnityAlphaFieldEvidence.IsExpectedTargetFormat(
                    GraphicsFormat.R8_UNorm, GraphicsFormat.R8_UNorm),
                Is.True);
            Assert.That(
                UnityAlphaFieldEvidence.IsExpectedTargetFormat(
                    GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormat.R8_UNorm),
                Is.False);
            Assert.That(
                UnityAlphaFieldEvidence.IsExpectedTargetFormat(
                    GraphicsFormat.R8_SRGB, GraphicsFormat.R8_UNorm),
                Is.False);
            Assert.That(
                UnityAlphaFieldEvidence.IsExpectedTargetFormat(
                    GraphicsFormat.None, GraphicsFormat.R8_UNorm),
                Is.False);
        }

        /// <summary>
        /// Takes the length Unity actually returned, so the mismatch branch is
        /// reachable in production. The multiplication is done in long to stay
        /// correct for a 16384-square texture, where width * height overflows a
        /// signed 32-bit int only after further scaling but is close enough that
        /// int arithmetic is not worth relying on.
        /// </summary>
        [Test]
        public void TheReturnedBufferLengthMustEqualWidthTimesHeight()
        {
            Assert.That(
                UnityAlphaFieldEvidence.IsExpectedBufferLength(32L, 8, 4), Is.True);
            Assert.That(
                UnityAlphaFieldEvidence.IsExpectedBufferLength(31L, 8, 4), Is.False);
            Assert.That(
                UnityAlphaFieldEvidence.IsExpectedBufferLength(33L, 8, 4), Is.False);
            Assert.That(
                UnityAlphaFieldEvidence.IsExpectedBufferLength(0L, 8, 4), Is.False);
            Assert.That(
                UnityAlphaFieldEvidence.IsExpectedBufferLength(1L, 1, 1), Is.True);
            Assert.That(
                UnityAlphaFieldEvidence.IsExpectedBufferLength(
                    268435456L, 16384, 16384),
                Is.True,
                "Overflow-safe: 16384 * 16384 must be computed in long.");
        }

        /// <summary>
        /// One responsibility only: every byte is 0 or 255. Length is
        /// IsExpectedBufferLength's job, checked earlier and against the length
        /// Unity returned.
        /// </summary>
        [Test]
        public void OnlyZeroAnd255AreAcceptedFromThePredicateTarget()
        {
            Assert.That(
                UnityAlphaFieldEvidence.IsBinaryPredicateBuffer(
                    new byte[] { 0, 255, 255, 0 }), Is.True);
            Assert.That(
                UnityAlphaFieldEvidence.IsBinaryPredicateBuffer(
                    new byte[] { 0, 1, 255, 0 }), Is.False);
            Assert.That(
                UnityAlphaFieldEvidence.IsBinaryPredicateBuffer(
                    new byte[] { 0, 254, 255, 0 }), Is.False);
            Assert.That(
                UnityAlphaFieldEvidence.IsBinaryPredicateBuffer(
                    new byte[] { 0, 128, 255, 0 }), Is.False);
            Assert.That(
                UnityAlphaFieldEvidence.IsBinaryPredicateBuffer(null), Is.False);
        }

        /// <summary>
        /// The orientation validator that decides gate 12. The expected pattern is
        /// a 4x2 grid, bottom-to-top row-major, asymmetric on both axes and not
        /// symmetric under transpose, so a vertical flip, a horizontal mirror and a
        /// transpose each produce a different eight-byte buffer.
        /// <para>
        /// Every case here is a real eight-byte rearrangement. A width/height swap
        /// is not tested here: it is a dimension fault, and IsExpectedLevelSize
        /// owns it.
        /// </para>
        /// </summary>
        [Test]
        public void TheOrientationValidatorRejectsEveryReorientation()
        {
            // grid, x fastest, y = 0 is the bottom row:
            //   y=1:  255   0   0   0
            //   y=0:  255 255   0   0
            var expected = new byte[] { 255, 255, 0, 0, 255, 0, 0, 0 };

            // Rows exchanged.
            var verticalFlip = new byte[] { 255, 0, 0, 0, 255, 255, 0, 0 };

            // Each row reversed.
            var horizontalMirror = new byte[] { 0, 0, 255, 255, 0, 0, 0, 255 };

            // True transpose to a 2-wide, 4-tall arrangement: t(x, y) = e(y, x).
            //   t = [ e(0,0) e(0,1) | e(1,0) e(1,1) | e(2,0) e(2,1) | e(3,0) e(3,1) ]
            //     = [ 255    255    | 255    0      | 0      0      | 0      0      ]
            var transposed = new byte[] { 255, 255, 255, 0, 0, 0, 0, 0 };

            Assert.That(
                UnityAlphaFieldEvidence.MatchesExpectedPattern(
                    (byte[])expected.Clone(), expected), Is.True);
            Assert.That(
                UnityAlphaFieldEvidence.MatchesExpectedPattern(verticalFlip, expected),
                Is.False, "A vertical flip must be rejected.");
            Assert.That(
                UnityAlphaFieldEvidence.MatchesExpectedPattern(horizontalMirror, expected),
                Is.False, "A horizontal mirror must be rejected.");
            Assert.That(
                UnityAlphaFieldEvidence.MatchesExpectedPattern(transposed, expected),
                Is.False, "A transpose must be rejected.");
            Assert.That(
                UnityAlphaFieldEvidence.MatchesExpectedPattern(null, expected), Is.False);
        }
```

Add `using System.Collections.Generic;` and `using UnityEngine.Experimental.Rendering;`
to the test file for `HashSet`, `List`, and `GraphicsFormat`.

- [ ] **Step 2: Run the tests to verify they fail**

Run the named tests in `UnityAlphaFieldEvidenceTests`.
Expected: compile failure — none of the predicates exists.

- [ ] **Step 3: Write the minimal implementations**

Add to `UnityAlphaFieldEvidence` (`using UnityEditor;` is already present for `BuildTarget`):

```csharp
        /// <summary>
        /// The closed format allowlist. Each member is admitted on two grounds:
        /// durable characterization through the R8 predicate path, and an
        /// authoritative decode rule. UNorm decode is n/(2^b - 1), so the result is
        /// structurally finite and within [0, 1]; BC3's alpha block is an exact
        /// integer scheme; BC7 decompression is specified bit-accurate; RGB24 has no
        /// alpha channel, so the sampler returns exactly one.
        /// <para>
        /// Everything else is refused. Float formats cannot supply the
        /// finite-and-[0,1] attestation, because one predicate bit reports the same
        /// 0 for a legitimate below-one value as for 2.0, -1.0, NaN or +Inf.
        /// DXT5Crunched behaves as DXT5 in one earlier measurement but is not
        /// durably exercised. ARGB4444 is exact - its 4-bit quantization of many
        /// authoring values to exactly one is not itself unsafe, because the
        /// imported GPU-decoded representation is what playback samples - but it has
        /// no durable production-shaped characterization. ASTC decodes under a
        /// tolerance rather than bit-exactly.
        /// </para>
        /// </summary>
        internal static bool IsAdmittedFormat(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                case TextureFormat.Alpha8:
                case TextureFormat.RGB24:
                case TextureFormat.DXT5:
                case TextureFormat.BC7:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Deliberately not generalized to "Standalone": the other members of that
        /// group have their own default format tables and were never characterized.
        /// With another target active, the Windows import is not loaded and cannot
        /// be inspected at all.
        /// </summary>
        internal static bool IsAdmittedBuildTarget(BuildTarget target)
        {
            return target == BuildTarget.StandaloneWindows64;
        }

        /// <summary>
        /// A gate on declared state. activeMipmapLimit is the per-texture effective
        /// limit and already folds in the global limit and any mipmap-limit group.
        /// Streaming is refused outright rather than handled: what a Load of a
        /// non-resident level returns has never been observed.
        /// <para>
        /// This is a pure predicate because its false branches cannot be constructed
        /// without mutating project or importer state, which production must never
        /// do.
        /// </para>
        /// </summary>
        internal static bool MipResidencyGatesPass(int activeMipmapLimit, bool streamingMipmaps)
        {
            return activeMipmapLimit == 0 && !streamingMipmaps;
        }

        internal static bool AreDimensionsUsable(int width, int height, int mipmapCount)
        {
            return width > 0 && height > 0 && mipmapCount > 0;
        }

        /// <summary>
        /// Async readback is the whole route's precondition. The exact R8_UNorm
        /// render and readback capabilities are what the destination requires. The
        /// source-sample capability is a different question from the format
        /// allowlist: the allowlist is AMUSE policy over TextureFormat, this asks
        /// the device whether it can sample the actual imported representation the
        /// shader will Load.
        /// </summary>
        internal static bool HostCapabilitiesPass(
            bool asyncReadback, bool r8Renderable, bool r8Readable, bool sourceSampleable)
        {
            return asyncReadback && r8Renderable && r8Readable && sourceSampleable;
        }

        internal static bool IsShaderUsable(bool assetLoaded, bool isSupported)
        {
            return assetLoaded && isSupported;
        }

        internal static bool IsExpectedLevelSize(
            int width, int height, int expectedWidth, int expectedHeight)
        {
            return width == expectedWidth && height == expectedHeight;
        }

        /// <summary>
        /// Unity may substitute a format it prefers for a temporary target. A
        /// substituted target would silently change what the readback means, so an
        /// inexact match is a refusal rather than something to tolerate.
        /// </summary>
        internal static bool IsExpectedTargetFormat(
            GraphicsFormat actual, GraphicsFormat expected)
        {
            return actual == expected;
        }

        /// <summary>
        /// Compares the length Unity actually returned against the destination this
        /// code requested. It must be called with the readback's own length, before
        /// any managed array is allocated: allocating an array of the expected size
        /// and then passing its own Length would make this branch unreachable.
        /// <para>
        /// The product is computed in long so the comparison stays correct for the
        /// largest textures Unity imports.
        /// </para>
        /// </summary>
        internal static bool IsExpectedBufferLength(
            long actualLength, int width, int height)
        {
            return actualLength == (long)width * height;
        }

        /// <summary>
        /// One responsibility: the shader emits only 0 or 1, which an R8_UNorm
        /// target stores as 0 or 255. Anything between means the value was
        /// filtered, rescaled, or transfer-converted on the way out, and the
        /// predicate would no longer be the predicate. Length is
        /// <see cref="IsExpectedBufferLength"/>'s job.
        /// </summary>
        internal static bool IsBinaryPredicateBuffer(byte[] bytes)
        {
            if (bytes == null)
            {
                return false;
            }

            foreach (var value in bytes)
            {
                if (value != 0 && value != byte.MaxValue)
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool MatchesExpectedPattern(byte[] actual, byte[] expected)
        {
            if (actual == null || expected == null || actual.Length != expected.Length)
            {
                return false;
            }

            for (var index = 0; index < expected.Length; index++)
            {
                if (actual[index] != expected[index])
                {
                    return false;
                }
            }

            return true;
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run `UnityAlphaFieldEvidenceTests`. Expected: all pass, including every pre-existing case — no production behaviour changed in this task.

---

## Task 5: The GPU acquisition core, its gates, and the host-capability latch

**One task, deliberately.** Splitting acquisition from gate 12 would leave the
repository in a state where the new GPU route returns evidence that no orientation
check has validated — GPU-derived alpha admitted without the precondition that makes
it trustworthy. Gate 12 is therefore written in the **same GREEN step** as the route,
and `TryCapture` never exists in a form that can return a chain without it.

Behaviour changes here: non-readable, block-compressed and mipmapped textures begin
producing evidence, and `GetPixels32` leaves production.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs` (`TryCapture` replaced; the core, the check and the latch added)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs`

**Interfaces:**
- Consumes: Task 3's `ShaderAssetPath`; all twelve Task 4 predicates.
- Produces:
  - `private static bool TryAcquireLevel(Texture2D texture, int mip, Material material, out AlphaTextureData level)` — private; called only by the chain loop and the capability check.
  - `internal static bool HostCapabilityCheckPasses()` — gate 12's reader. **No setter, no reset, no reflection hook.**

### A note on the latch and test ordering

`HostCapabilityCheckPasses()` memoizes into a `static bool?` that only a domain
reload clears. NUnit gives no ordering guarantee within an AppDomain, so a test
calling it after any other capture may observe a **cached** `true` and execute no GPU
work at all.

This plan therefore never claims a test exercised the real check on a fresh latch:

- `TheHostCapabilityCheckPassesOnThisHost` asserts the value is `true`. That claim is
  sound either way — a cached `true` can only have come from a real passing run
  earlier in this AppDomain, since nothing else can write the latch.
- `RenderTexture.active` restoration is asserted around **ordinary capture**, which
  allocates on every call and so is never short-circuited.
- Cleanup inside the capability check itself is a **reviewed structural guarantee**,
  not an executed assertion, precisely because it may not run.

Do not add a reset, a setter, or a reflection hook to make the check re-runnable.

- [ ] **Step 1: Write the failing tests**

Add to `UnityAlphaFieldEvidenceTests.cs`. Every fixture is a real project asset in `TempFolder`, which `[TearDown]` deletes whether or not assertions passed.

```csharp
        // --- GPU acquisition: real Unity integration --------------------------

        /// <summary>
        /// Imports a mipmapped, non-readable texture in a requested format. This is
        /// the shape of a real avatar texture, and every one of these properties was
        /// a refusal before this milestone.
        /// </summary>
        private static Texture2D ImportMipmapped(
            string name,
            Color32[] pixels,
            int width,
            int height,
            TextureImporterFormat format)
        {
            return Import(name, pixels, width, height, importer =>
            {
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                Format(format)(importer);
            });
        }

        /// <summary>
        /// 8x8, alpha 255 for x &lt; 5 and 200 otherwise. The boundary is
        /// deliberately odd-aligned so it does not survive halving: source texel
        /// x = 4 is exactly one at mip 0, while the mip-1 texel covering it is not.
        /// </summary>
        private static Color32[] OddBoundaryPixels()
        {
            var pixels = new Color32[64];
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    pixels[y * 8 + x] = new Color32(64, 32, 16, x < 5 ? (byte)255 : (byte)200);
                }
            }

            return pixels;
        }

        [Test]
        public void ANonReadableMipmappedTextureIsCaptured()
        {
            var texture = ImportMipmapped(
                "nonreadable_mipped", AsymmetricPixels(), Size, Size,
                TextureImporterFormat.RGBA32);

            Assert.That(texture.isReadable, Is.False, "The fixture must be non-readable.");
            Assert.That(texture.mipmapCount, Is.GreaterThan(1));
            Assert.That(TryChain(texture, out var chain), Is.True);
            Assert.That(chain.Count, Is.EqualTo(texture.mipmapCount));
            Assert.That(chain[0].Width, Is.EqualTo(Size));
        }

        /// <summary>
        /// 8x8 quadrants: alpha 255 for x &lt; 4, 254 otherwise. Block-compressed
        /// formats encode 4x4 blocks, so a 4x4 fixture is a single block in which
        /// the encoder legitimately snaps 254 to 255 - the imported field really is
        /// opaque there. Separating maximum from submaximum needs the submaximum in
        /// a <em>different</em> block.
        /// </summary>
        private static Color32[] QuadrantPixels()
        {
            var pixels = new Color32[64];
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    pixels[y * 8 + x] =
                        new Color32(64, 32, 16, x < 4 ? (byte)255 : (byte)254);
                }
            }

            return pixels;
        }

        /// <summary>
        /// ARGB32 is absent deliberately: the importer rejects it for the Default
        /// texture type on Standalone with a console error. It is covered instead by
        /// a direct texture-asset case, and by the exhaustive allowlist predicate.
        /// </summary>
        [TestCase(TextureImporterFormat.RGBA32)]
        [TestCase(TextureImporterFormat.Alpha8)]
        [TestCase(TextureImporterFormat.DXT5)]
        [TestCase(TextureImporterFormat.BC7)]
        public void EachAdmittedAlphaFormatSeparatesMaximumFromSubmaximum(
            TextureImporterFormat format)
        {
            var texture = ImportMipmapped(
                "fmt_" + format, QuadrantPixels(), 8, 8, format);

            Assert.That(TryChain(texture, out var chain), Is.True);
            Assert.That(chain[0].GetAlpha(1, 1), Is.EqualTo(255),
                "Maximum alpha must satisfy the predicate exactly.");
            Assert.That(chain[0].GetAlpha(5, 1), Is.EqualTo(0),
                "A representable submaximum must read exactly 0.");
        }

        /// <summary>
        /// RGB24 through the real production shader route. It is the one admitted
        /// format exempt from exact source-format Sample support, so this proves the
        /// exemption is sound in practice: every returned byte must be exactly 255.
        /// </summary>
        [Test]
        public void AnRgbOnlyFormatSamplesAlphaExactlyOneAtEveryLevel()
        {
            var texture = ImportMipmapped(
                "rgb24_mipped", QuadrantPixels(), 8, 8, TextureImporterFormat.RGB24);

            Assert.That(texture.format, Is.EqualTo(TextureFormat.RGB24));
            Assert.That(
                SystemInfo.IsFormatSupported(
                    texture.graphicsFormat, FormatUsage.Sample),
                Is.False,
                "This host reports no exact Sample support for RGB24's graphics "
                + "format; the exemption is what admits it.");

            Assert.That(TryChain(texture, out var chain), Is.True);
            for (var level = 0; level < chain.Count; level++)
            {
                var grid = chain[level];
                Assert.That(grid.IsFullyOpaque, Is.True, "level " + level);
                for (var y = 0; y < grid.Height; y++)
                {
                    for (var x = 0; x < grid.Width; x++)
                    {
                        Assert.That(grid.GetAlpha(x, y), Is.EqualTo(255));
                    }
                }
            }
        }

        /// <summary>
        /// The premise of the whole milestone, on the format that dominates real
        /// avatars: mip 0 proves an opacity that mip 1 refutes.
        /// </summary>
        [TestCase(TextureImporterFormat.RGBA32)]
        [TestCase(TextureImporterFormat.DXT5)]
        public void ALowerMipContradictsAMipZeroOpaqueTexel(TextureImporterFormat format)
        {
            var texture = ImportMipmapped(
                "oddboundary_" + format, OddBoundaryPixels(), 8, 8, format);

            Assert.That(TryChain(texture, out var chain), Is.True);
            Assert.That(chain.Count, Is.GreaterThan(1));
            Assert.That(chain[0].GetAlpha(4, 0), Is.EqualTo(255),
                "Source texel x=4 is exactly one at mip 0.");
            Assert.That(chain[1].GetAlpha(2, 0), Is.Not.EqualTo(255),
                "The mip-1 texel covering it is not.");
        }

        [Test]
        public void ANonSquareChainPreservesBottomToTopRowOrder()
        {
            var pixels = new Color32[16 * 4];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(64, 32, 16, 255);
            }
            for (var y = 0; y < 4; y++)
            {
                pixels[y * 16] = new Color32(64, 32, 16, 0);
            }

            var texture = ImportMipmapped(
                "nonsquare", pixels, 16, 4, TextureImporterFormat.RGBA32);

            Assert.That(TryChain(texture, out var chain), Is.True);
            Assert.That(chain[0].Width, Is.EqualTo(16));
            Assert.That(chain[0].Height, Is.EqualTo(4));
            for (var y = 0; y < 4; y++)
            {
                Assert.That(chain[0].GetAlpha(0, y), Is.EqualTo(0),
                    "The zero column must stay a column; a transpose would move it.");
            }
            Assert.That(chain[2].Width, Is.EqualTo(4));
            Assert.That(chain[2].Height, Is.EqualTo(1),
                "Each axis halves independently and clamps at one.");
        }

        [Test]
        public void TheProductionShaderAssetLoadsAndIsSupported()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                UnityAlphaFieldEvidence.ShaderAssetPath);

            Assert.That(shader, Is.Not.Null, UnityAlphaFieldEvidence.ShaderAssetPath);
            Assert.That(shader.isSupported, Is.True);
        }

        [Test]
        public void TheHostCapabilityCheckPassesOnThisHost()
        {
            // Gate 12. A cached true can only have been written by a real passing
            // run earlier in this AppDomain, so this claim is sound whether or not
            // this call is the one that executed the check.
            Assert.That(UnityAlphaFieldEvidence.HostCapabilityCheckPasses(), Is.True);
        }

        [Test]
        public void TheActiveRenderTargetIsRestoredAcrossACapture()
        {
            var texture = ImportMipmapped(
                "restore", AsymmetricPixels(), Size, Size, TextureImporterFormat.RGBA32);
            var previous = RenderTexture.active;

            Assert.That(TryChain(texture, out _), Is.True);

            Assert.That(RenderTexture.active, Is.SameAs(previous));
        }

        // --- Refusals reachable through the real entry point ------------------

        [Test]
        public void ARefusedFormatIsRefusedBeforeAnyGpuWork()
        {
            // ARGB4444 and a float format are allocatable directly and produce no
            // Unity console error. Representative is deliberate: the complete
            // policy is covered exhaustively by TheFormatAllowlistIsExact.
            AssertRefusedForFormat(
                CreateTextureAsset("argb4444", TextureFormat.ARGB4444),
                TextureFormat.ARGB4444);
            AssertRefusedForFormat(
                CreateTextureAsset("rgbahalf", TextureFormat.RGBAHalf),
                TextureFormat.RGBAHalf);
        }

        [Test]
        public void AnInMemoryTextureWithoutAssetIdentityIsRefused()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(AsymmetricPixels());
                texture.Apply();

                Assert.That(
                    UnityTextureEvidence.TryGetSourceId(texture, out _), Is.False);
                Assert.That(
                    UnityAlphaFieldEvidence.TryCapture(texture, out _, out var chain),
                    Is.False);
                Assert.That(chain, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
```

- [ ] **Step 2: Invert the existing refusal tests, in this RED step**

Existing cases in this file pin refusals this task removes, or assert alpha
**magnitudes** the GPU route does not preserve. Both are **expected-value changes and
must be made now, before any production edit**, so the RED run states the intended
new behaviour rather than being retrofitted after GREEN.

**The evidence is predicate bytes, not magnitudes.** Sampled alpha exactly one stores
`255`; every finite value below one stores `0`. Assertions that read `128` or `254`
under the old `GetPixels32` route must assert `0`, against a `255` anchor so the
result is a real asymmetry rather than a blank field.

Refusals that become admissions:

| Existing case | Becomes |
| --- | --- |
| `DefaultImport_IsNotReadable_AndRefuses` | `DefaultImport_IsNotReadableAndCompressed_AndIsStillCaptured` — **8x8** fixture, because a 4x4 block-compressed texture is a single block in which the encoder legitimately snaps 254 to 255. |
| `ReadableDxt5_Refuses` | `Dxt5_IsAdmittedAndSeparatesMaximumFromSubmaximum` — 8x8 quadrant fixture, maximum at (1,1) exactly `255` and submaximum at (5,1) exactly `0`. |
| `ReadableBc7_Refuses` | `Bc7_IsAdmittedAndSeparatesMaximumFromSubmaximum` — likewise. |
| `MipmappedTexture_Refuses` | `MipmappedTexture_IsCapturedAsAFullChain` — asserting `chain.Count == texture.mipmapCount` via `TryChain`. |

Magnitude assertions that become binary-predicate assertions — **seven**, across two
files:

| Case | File |
| --- | --- |
| `SupportedImport_PreservesBottomToTopRowOrder` | `UnityAlphaFieldEvidenceTests` |
| `ReadableAlpha8_IsAdmittedAndSeparatesMaximumFromSubmaximum` (renamed) | same |
| `ReadableArgb32_IsAdmittedAndSeparatesMaximumFromSubmaximum` (renamed) | same |
| `AlphaSourceFromInput_ReportsTheInputAlpha` | same |
| `AlphaSourceFromGrayScale_ReportsTheGeneratedAlphaNotTheInputAlpha` — drops its `128` reference and compares the two corners instead | same |
| the inverted `DefaultImport_…` and `MipmappedTexture_…` bodies | same |
| `PerMaterialRequestsShareStableTextureEvidenceAndRemainImmutable` — both the live and the retained-reference assertions | `UnityMaterialEvidenceCaptureTests` |

Leave these three **unchanged**, still asserting refusal:
`ReadableCrunchedDxt5_Refuses`, `ReadableRgbaHalf_Refuses`,
`ReadableArgb4444_Refuses`. They pin formats that stay outside the allowlist.

`ReadableArgb32_…` keeps its **direct texture-asset** construction
(`CreateTextureAsset`): the Standalone importer override rejects `ARGB32` for the
Default texture type with a console error, so it is not available as an override
case.

**Fixture policy.** These fixtures are newly created synthetic assets written into
this file's own `TempFolder`, which `[TearDown]` deletes whether or not assertions
passed, so configuring their importers through the existing `Import` /
`CreateTextureAsset` / `Format` helpers is permitted by the synthetic-fixture
importer policy in Global Constraints. Nothing here touches an asset the test did not
create, and no project or global setting is changed.

- [ ] **Step 3: Run the tests to verify they fail**

Run `UnityAlphaFieldEvidenceTests`.
Expected: FAIL — the four inverted cases and every new GPU case fail, because the
current producer refuses `!isReadable` and `mipmapCount != 1` and
`HostCapabilityCheckPasses` does not exist.
`TheProductionShaderAssetLoadsAndIsSupported` already passes, which is fine.

- [ ] **Step 4: Replace `TryCapture` with the gated GPU route, gate 12 included**

Add `using UnityEngine.Experimental.Rendering;` and `using UnityEngine.Rendering;`.

```csharp
        /// <summary>The predicate target: one byte per texel.</summary>
        private const GraphicsFormat PredicateTarget = GraphicsFormat.R8_UNorm;

        internal static bool TryCapture(
            Texture texture,
            out TextureSourceId source,
            out AlphaMipChain chain)
        {
            source = default;
            chain = null;

            // Unity's overloaded equality is required: it is true for a destroyed
            // object, where ReferenceEquals would be false.
            var texture2D = texture as Texture2D;
            if (texture2D == null)
            {
                return false;
            }

            if (!UnityTextureEvidence.TryGetSourceId(texture2D, out source))
            {
                return false;
            }

            try
            {
                // Every policy and capability gate precedes the first allocation.
                // The format allowlist in particular is checked before any GPU call,
                // so a compressed source never reaches a route that would log a
                // Unity error.
                if (!IsAdmittedBuildTarget(EditorUserBuildSettings.activeBuildTarget) ||
                    !IsAdmittedFormat(texture2D.format) ||
                    !MipResidencyGatesPass(
                        texture2D.activeMipmapLimit, texture2D.streamingMipmaps) ||
                    !AreDimensionsUsable(
                        texture2D.width, texture2D.height, texture2D.mipmapCount) ||
                    !HostCapabilitiesPass(
                        SystemInfo.supportsAsyncGPUReadback,
                        SystemInfo.IsFormatSupported(PredicateTarget, FormatUsage.Render),
                        SystemInfo.IsFormatSupported(PredicateTarget, FormatUsage.ReadPixels),
                        SystemInfo.IsFormatSupported(
                            texture2D.graphicsFormat, FormatUsage.Sample)))
                {
                    source = default;
                    return false;
                }

                var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
                if (!IsShaderUsable(shader != null, shader != null && shader.isSupported))
                {
                    source = default;
                    return false;
                }

                // Gate 12, last because it depends on the shader and on the device
                // capabilities above. It is written here in the same step as the
                // route it guards: there is no version of TryCapture that returns a
                // chain without it.
                if (!HostCapabilityCheckPasses())
                {
                    source = default;
                    return false;
                }

                if (!TryCaptureChain(texture2D, shader, out chain))
                {
                    source = default;
                    chain = null;
                    return false;
                }

                return true;
            }
            catch (MissingReferenceException)
            {
                // Measured: raised by any member access on a destroyed object, and
                // its base type is SystemException rather than UnityException.
                source = default;
                chain = null;
                return false;
            }
        }

        /// <summary>
        /// Captures every declared mip and constructs the chain only after exactly
        /// mipmapCount successes. A single failed level refuses the whole texture:
        /// there is no code path on which a partially populated chain exists, so
        /// none can escape.
        /// </summary>
        private static bool TryCaptureChain(
            Texture2D texture, Shader shader, out AlphaMipChain chain)
        {
            chain = null;
            var levels = new AlphaTextureData[texture.mipmapCount];

            // One material per texture, not per level: Graphics.Blit sets _MainTex
            // on it and only _Mip varies between levels.
            var material = new Material(shader);
            try
            {
                for (var mip = 0; mip < levels.Length; mip++)
                {
                    if (!TryAcquireLevel(texture, mip, material, out var level))
                    {
                        return false;
                    }

                    levels[mip] = level;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }

            chain = new AlphaMipChain(levels);
            return true;
        }

        /// <summary>
        /// The one GPU acquisition core: Blit through the predicate shader into an
        /// exact R8_UNorm target, read the bytes back synchronously, validate, and
        /// build one grid.
        /// <para>
        /// It holds no identity, build-target, format-allowlist, mip-limit,
        /// streaming, or capability gate. Those belong to its callers, and repeating
        /// them here would create a second place for the policy to drift.
        /// </para>
        /// <para>
        /// Its validations are output-integrity checks on a destination this code
        /// allocated. None of them establishes that the requested source level was
        /// resident; that is what the declared-state gates are for.
        /// </para>
        /// </summary>
        private static bool TryAcquireLevel(
            Texture2D texture, int mip, Material material, out AlphaTextureData level)
        {
            level = null;

            var width = Mathf.Max(1, texture.width >> mip);
            var height = Mathf.Max(1, texture.height >> mip);

            material.SetInt("_Mip", mip);

            var descriptor = new RenderTextureDescriptor(width, height, PredicateTarget, 0)
            {
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };

            var target = RenderTexture.GetTemporary(descriptor);
            try
            {
                // Unity may substitute a format it prefers. A substituted target
                // would silently change what the readback means, so an inexact match
                // is a refusal rather than something to tolerate.
                if (!IsExpectedTargetFormat(target.graphicsFormat, PredicateTarget) ||
                    !IsExpectedLevelSize(target.width, target.height, width, height))
                {
                    return false;
                }

                var previous = RenderTexture.active;
                try
                {
                    Graphics.Blit(texture, target, material);
                }
                finally
                {
                    RenderTexture.active = previous;
                }

                var request = AsyncGPUReadback.Request(target, 0, PredicateTarget);
                request.WaitForCompletion();
                if (request.hasError ||
                    !IsExpectedLevelSize(request.width, request.height, width, height))
                {
                    return false;
                }

                // The NativeArray is owned by the request and must not outlive it,
                // so the bytes are copied inside this scope. AlphaTextureData takes
                // IReadOnlyList<byte>, which NativeArray does not implement, so the
                // managed copy is forced by the existing type as well.
                var data = request.GetData<byte>();

                // Checked against the length Unity returned, BEFORE allocating,
                // so the mismatch branch is genuinely reachable.
                if (!IsExpectedBufferLength(data.Length, width, height))
                {
                    return false;
                }

                var bytes = new byte[width * height];
                data.CopyTo(bytes);

                if (!IsBinaryPredicateBuffer(bytes))
                {
                    return false;
                }

                // The readback is bottom-to-top row-major and so is
                // AlphaTextureData, so the bytes cross with no flip or transpose.
                level = new AlphaTextureData(width, height, bytes);
                return true;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(target);
            }
        }
```

Add the latch and the check in this same step, so gate 12 above resolves:

```csharp
        /// <summary>
        /// Process-local host-capability latch. It records one fact about this
        /// Editor process's graphics stack; it is keyed by nothing and holds no
        /// texel, texture, or source identity. It is explicitly NOT a texture
        /// evidence cache and must never be grown into one. Domain reload clears it.
        /// </summary>
        private static bool? _hostCapabilityPassed;

        /// <summary>
        /// 4x2, asymmetric on both axes and not symmetric under transpose, so a
        /// vertical flip, a horizontal mirror, a transpose and a width/height swap
        /// each produce a different buffer from the expected one. Bottom-to-top
        /// row-major, matching AlphaTextureData.
        /// </summary>
        private static readonly byte[] ExpectedOrientationPattern =
        {
            255, 255, 0, 0,
            255, 0, 0, 0
        };

        /// <summary>
        /// Gate 12. Row order is soundness-critical - a vertical flip would
        /// attribute alpha to the wrong triangles and could yield a false
        /// ProvenOpaque - and the orientation agreement was measured on one graphics
        /// API only. The active build target says nothing about the Editor's
        /// graphics API, so this converts an unverified cross-API assumption into a
        /// checked precondition on the host that actually runs the build.
        /// <para>
        /// Evaluated lazily, once per Editor AppDomain, after the shader and the
        /// device capabilities it depends on have been confirmed. On failure every
        /// texture-alpha capture refuses for the remainder of the AppDomain: there
        /// is no partial credit and no retry.
        /// </para>
        /// <para>
        /// It proves that this host's production route preserves the expected
        /// orientation and binary R8 encoding. It does NOT independently attest the
        /// decode or swizzle behaviour of any compressed format; the fixture is one
        /// uncompressed texture.
        /// </para>
        /// <para>
        /// The fixture is built in memory and so has no asset identity, which is why
        /// it calls the acquisition core directly: TryGetSourceId would refuse it at
        /// the identity gate.
        /// </para>
        /// </summary>
        internal static bool HostCapabilityCheckPasses()
        {
            if (_hostCapabilityPassed.HasValue)
            {
                return _hostCapabilityPassed.Value;
            }

            _hostCapabilityPassed = RunHostCapabilityCheck();
            return _hostCapabilityPassed.Value;
        }

        private static bool RunHostCapabilityCheck()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
            if (!IsShaderUsable(shader != null, shader != null && shader.isSupported))
            {
                return false;
            }

            var texture = new Texture2D(4, 2, TextureFormat.RGBA32, false);
            Material material = null;
            try
            {
                var pixels = new Color32[8];
                for (var index = 0; index < pixels.Length; index++)
                {
                    pixels[index] = new Color32(
                        64, 32, 16,
                        ExpectedOrientationPattern[index] == byte.MaxValue
                            ? (byte)255
                            : (byte)128);
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, false);

                material = new Material(shader);
                if (!TryAcquireLevel(texture, 0, material, out var level))
                {
                    return false;
                }

                var actual = new byte[ExpectedOrientationPattern.Length];
                for (var y = 0; y < 2; y++)
                {
                    for (var x = 0; x < 4; x++)
                    {
                        actual[y * 4 + x] = level.GetAlpha(x, y);
                    }
                }

                return MatchesExpectedPattern(actual, ExpectedOrientationPattern);
            }
            finally
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
```

Gate 12's call site is already written into the gate sequence above, in this same
step. There is no intermediate state in which the GPU route returns a chain that the
orientation check has not validated.

Delete `IsSupportedFormat`, the `isReadable` refusal, the `mipmapCount != 1`
refusal, the `GetPixels32` call, its `catch (ArgumentException)`, and the manual
alpha copy loop. Update the class doc: the evidence is predicate-equivalent to
effective shader alpha at **every declared mip**, obtained through the GPU rather
than a CPU copy.

- [ ] **Step 5: Run the tests to verify they pass**

Run `UnityAlphaFieldEvidenceTests`. Expected: **all** pass, including the four
inverted cases from Step 2, the three refusal cases left untouched, and every
pre-existing case in the file. No expected value may be edited at this point — Step 2
was the only place expected values were allowed to change.

- [ ] **Step 6: Confirm the Console carries no readback errors**

Inspect the Unity Console for the whole run. Expected: **no** `doesn't support ReadPixels usage` or `Async GPU readback failed` errors. Their absence is the evidence that the format allowlist is evaluated before any GPU call.

---
## Task 6: Admit mipmapped sampling and prove end-to-end propagation

One task, because the ordering matters. Every test below either fails for the one
reason this task fixes, or verifies behaviour Task 5 already landed. Writing them all
before the production edit is what makes the deletion of the blanket sampling refusal
a real RED-to-GREEN step rather than a retrofit.

This task contains **exactly one production edit**: deleting the `mipmapCount > 1`
clause from `UnityTextureEvidence.TryGetSampling`. Nothing else in production changes.

It lands only now because until the Task 2 conjunction and the Task 5 multi-mip
acquisition both exist, that refusal is the only thing keeping the classifier sound
for mipmapped textures.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs` (`:83-88`) — the only production change
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs` (`:129-137`)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/RendererAlphaAnalysisIntegrationTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/AlphaEvidenceClassifierIntegrationTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

### The lower-mip fixture, derived explicitly

An 8x8 texture, alpha `255` where `x < 5` and `200` otherwise. The boundary is
odd-aligned so it does not survive halving.

| Level | Size | Texel covering source `x = 4` | Alpha there |
| --- | --- | --- | --- |
| 0 | 8x8 | `x = 4` | `255` — exactly one |
| 1 | 4x4 | `x = 2`, covering source `x` 4 and 5 | mean of `255` and `200` — below one |

A triangle whose UV support lies wholly inside source texel `(4, 0)` is therefore
`ProvenOpaque` from mip 0 alone and `MustRemainTransparent` once mip 1 is consulted.
Texel `(4, 0)` spans `u ∈ [0.5, 0.625)` and `v ∈ [0, 0.125)`, so UVs
`(0.51, 0.01)`, `(0.61, 0.01)`, `(0.51, 0.11)` lie wholly inside it. Under `Point`
filtering and `Clamp` wrap — the sampling this fixture's importer already sets — that
support maps to exactly one texel at each level: `(4, 0)` at mip 0 and `(2, 0)` at
mip 1.

**Why the whole fixture proves nothing, and why that is correct.** Mip 3 is 1x1 and
covers every source texel, so it is not exactly one anywhere. Every triangle
sampling this texture is therefore `MustRemainTransparent` under the full
conjunction, which is the sound answer: the sampler may select mip 3 for any
fragment. A contrast triangle inside the same texture is impossible, so the contrast
comes from a **second slot** carrying a uniformly opaque texture, which is exactly
one at every level and still proves. That control is what shows the refusal is
evidence-scoped rather than a blanket failure of the new route.

**`BuildFixtureMesh` is not reused.** Its UVs (`:161-171`) are
`0.55-0.9` and `0.01-0.2` against a 4x4 texture; neither region lies inside source
texel `(4, 0)` of an 8x8 texture, so it cannot express the precondition. A dedicated
mesh is built instead.

### Route, read from the current file

`AnalyzeVerifiedRuntimeStates(root, renderer, out evidence)` (`:2315-2326`) is the
build-handoff path: it calls `CaptureVerifiedRuntimeStateEvidence` (`:2347-2364`),
then `CaptureRuntimeStateGeometry`, then
`AmusePlatformFinishPass.AnalyzeRuntimeStatesForTests`, returning
`(RendererAnalysisRefusal Refusal, int OpaqueCandidateTriangleCount)`.

Two constraints it imposes, both already satisfied by the existing pattern:

- `CaptureVerifiedRuntimeStateEvidence` asserts the committed graph has exactly
  **one layer and one clip** (`:2356-2358`). `AddAnimatedObjectReference(root,
  "m_ProbeAnchor", root.transform, out clip)` (`:2276-2299`) supplies exactly that
  with an **unrelated** object binding — the same device
  `RuntimeStateIntegration_UnrelatedObjectCurveRemainsAnalyzable` (`:733`) already
  uses.
- `TryAttestVerifiedFixture` (`:2366-2374`) attests any non-null material as Poiyomi
  with `PoiyomiMaterialSemantics.AlphaEvidenceRequest`, and `VerifiedAlphaOnly`
  (`:2404-2413`) drives `InterpretVerifiedAlpha`. Nothing new is needed there.

`AddAnalyzableRenderer` is **not** reused: its mesh never assigns `mesh.uv` and it
carries one material and one submesh (`:2065-2086`). `DisposeAnalyzableRenderer`
(`:2415-2426`) is likewise not reused, because `AnalyzableRendererFixture` holds a
single `Material`. This test builds and cleans up its own objects.

### Material configuration, read from the fixture shader

`PoiyomiFixtureTestBase.CreateVerifiedMaterial()` (`:70`) instantiates
`Hidden/Alrauna/AmuseTests/PoiyomiSemanticTest` at its declared defaults. In that
shader `_Color` already defaults to `(1,1,1,1)` (`:17`) and every other alpha gate
is already zero, so exactly two properties must change to put alpha on `_MainTex`:
`_AlphaForceOpaque`, declared `1` (`:34`), and `_MainAlphaMaskMode`, declared `2`
(`:36`). No force-opaque, no mask, no parallax, no other semantic shortcut.

- [ ] **Step 1: Add every fixture helper**

All fixture construction happens before any test is written, so Step 2's tests
compile as a set.

#### 1a. Renderer and classifier fixture helpers

```csharp
        /// <summary>
        /// 8x8, alpha 255 where x &lt; 5 and 200 otherwise. Odd-aligned so the
        /// boundary does not survive halving: source texel x = 4 is exactly one at
        /// mip 0, and the mip-1 texel covering it is not.
        /// </summary>
        private static Color32[] OddBoundaryPixels()
        {
            var pixels = new Color32[64];
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    pixels[y * 8 + x] =
                        new Color32(64, 32, 16, x < 5 ? (byte)255 : (byte)200);
                }
            }

            return pixels;
        }

        private static Color32[] UniformOpaquePixels(int width, int height)
        {
            var pixels = new Color32[width * height];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(64, 32, 16, 255);
            }

            return pixels;
        }

        /// <summary>
        /// Imports a mipmapped, non-readable fixture with the Point/Clamp sampling
        /// the classifier's exact domain requires. A newly created synthetic asset
        /// under TempFolder, which TearDown deletes whether or not assertions pass.
        /// </summary>
        private static Texture2D ImportMippedFixture(
            string name, Color32[] pixels, int width, int height)
        {
            var path = TempFolder + "/" + name + ".png";
            var staging = new Texture2D(width, height, TextureFormat.RGBA32, false);
            staging.SetPixels32(pixels);
            staging.Apply();
            File.WriteAllBytes(path, staging.EncodeToPNG());
            Object.DestroyImmediate(staging);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(loaded, Is.Not.Null, $"'{path}' must import.");
            return loaded;
        }

        /// <summary>
        /// Submesh 0: one triangle whose UV support lies wholly inside source texel
        /// (4, 0) of an 8x8 texture. Submesh 1: one triangle anywhere, bound to the
        /// uniformly opaque control slot.
        /// </summary>
        private Mesh BuildLowerMipFixtureMesh()
        {
            var mesh = Track(new Mesh());
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(2f, 0f, 0f),
                new Vector3(3f, 0f, 0f),
                new Vector3(2f, 1f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0.51f, 0.01f),   // inside texel (4, 0) at mip 0
                new Vector2(0.61f, 0.01f),
                new Vector2(0.51f, 0.11f),
                new Vector2(0.2f, 0.2f),     // control slot, uniformly opaque
                new Vector2(0.4f, 0.2f),
                new Vector2(0.2f, 0.4f)
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 3, 4, 5 }, 1);
            return mesh;
        }
```

#### 1b. Build-handoff fixture helpers, test-local

Deliberately duplicated in this file rather than shared: a cross-suite fixture
abstraction to save twenty lines would be the wrong trade.

```csharp
        private const string LowerMipTempFolder = "Assets/AmuseTests_BuildLowerMip";

        /// <summary>
        /// 8x8, alpha 255 where x &lt; 5 and 200 otherwise. Odd-aligned so the
        /// boundary does not survive halving: source texel x = 4 is exactly one at
        /// mip 0, and the mip-1 texel covering it is not.
        /// </summary>
        private static Color32[] OddBoundaryAlphaPixels()
        {
            var pixels = new Color32[64];
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    pixels[y * 8 + x] =
                        new Color32(64, 32, 16, x < 5 ? (byte)255 : (byte)200);
                }
            }

            return pixels;
        }

        private static Color32[] UniformOpaqueAlphaPixels()
        {
            var pixels = new Color32[64];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(64, 32, 16, 255);
            }

            return pixels;
        }

        /// <summary>
        /// Imports an 8x8 mipmapped, non-readable RGBA32 asset with Point/Clamp
        /// sampling, zero mip bias, anisotropy 1, and no streaming — the exact
        /// state every acquisition gate admits. The folder is created by the
        /// caller and deleted in its finally.
        /// </summary>
        private static Texture2D ImportLowerMipTexture(string name, Color32[] pixels)
        {
            var path = LowerMipTempFolder + "/" + name + ".png";
            var staging = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            try
            {
                staging.SetPixels32(pixels);
                staging.Apply();
                File.WriteAllBytes(path, staging.EncodeToPNG());
            }
            finally
            {
                // Encoding or writing can throw; the in-memory staging texture must
                // not survive that.
                Object.DestroyImmediate(staging);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.mipMapBias = 0f;
            importer.anisoLevel = 1;
            importer.streamingMipmaps = false;
            importer.SaveAndReimport();

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(loaded, Is.Not.Null, $"'{path}' must import.");
            return loaded;
        }

        /// <summary>
        /// A verified Poiyomi fixture material whose alpha comes from _MainTex.
        /// Only the two gates the stand-in shader declares non-zero are changed;
        /// _Color already defaults to opaque white.
        /// </summary>
        private static Material SampledAlphaMaterial(Texture2D mainTex)
        {
            var material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            material.SetTexture("_MainTex", mainTex);
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);
            return material;
        }

        /// <summary>
        /// Submesh 0: one triangle whose UV support lies wholly inside source texel
        /// (4, 0) of an 8x8 texture — that texel spans u in [0.5, 0.625) and v in
        /// [0, 0.125). Submesh 1: one triangle on the uniformly opaque control.
        /// </summary>
        private static Mesh BuildLowerMipBuildMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(2f, 0f, 0f),
                new Vector3(3f, 0f, 0f),
                new Vector3(2f, 1f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0.51f, 0.01f),
                new Vector2(0.61f, 0.01f),
                new Vector2(0.51f, 0.11f),
                new Vector2(0.2f, 0.2f),
                new Vector2(0.4f, 0.2f),
                new Vector2(0.2f, 0.4f)
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 3, 4, 5 }, 1);
            return mesh;
        }
```

Add `using System.IO;` to the file if it is not already present.

- [ ] **Step 2: Write every test, before the production edit**

#### 2a. The renderer lower-mip regression

```csharp
        /// <summary>
        /// The conjunction reaching renderer analysis. Submesh 0's triangle is
        /// exactly one at mip 0 and below one at mip 1, so a mip-0-only
        /// implementation would report it as an opaque candidate. Submesh 1 is the
        /// control: uniformly opaque at every level, so it still proves.
        /// </summary>
        [Test]
        public void ALowerMipPreventsAnOtherwiseMipZeroOnlyOpaqueProof()
        {
            var oddBoundary = ImportMippedFixture(
                "odd_boundary", OddBoundaryPixels(), 8, 8);
            var uniform = ImportMippedFixture(
                "uniform_opaque", UniformOpaquePixels(8, 8), 8, 8);

            // Preconditions, pinned so a passing assertion below cannot be right
            // for the wrong reason.
            Assert.That(
                UnityAlphaFieldEvidence.TryCapture(oddBoundary, out _, out var chain),
                Is.True);
            Assert.That(chain.Count, Is.EqualTo(oddBoundary.mipmapCount));
            Assert.That(
                chain[0].GetAlpha(4, 0), Is.EqualTo(255),
                "Precondition: mip 0 alone would prove this texel opaque.");
            Assert.That(
                chain[1].GetAlpha(2, 0), Is.Not.EqualTo(255),
                "Precondition: the mip-1 texel covering it is not opaque.");

            var blocked = NewSampledAlphaMaterial(oddBoundary);
            var control = NewSampledAlphaMaterial(uniform);
            var renderer = NewRenderer(BuildLowerMipFixtureMesh(), blocked, control);

            var result = AnalyzeVerified(renderer, blocked, control);

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(
                result.Submeshes[0].Failure, Is.EqualTo(AlphaResolutionFailure.None),
                "Evidence was available; the triangle is simply not proven.");
            Assert.That(
                result.Plan.Submeshes[0].OpaqueTriangleOrdinals, Is.Empty,
                "A lower mip contradicts the mip-0 opaque texel.");
            Assert.That(
                result.Plan.Submeshes[1].OpaqueTriangleOrdinals,
                Is.EqualTo(new[] { 0 }),
                "The uniformly opaque control must still prove, so the refusal " +
                "above is evidence-scoped and not a blanket failure.");
        }
```

#### 2b. Repurpose the non-readable refusal case, and add its positive twin

`ANonReadableAlphaTextureRefusesOnlyItsOwnSubmesh` (`:393`) asserts a refusal this
milestone removes. Its real value is that a texture-scoped refusal stays
submesh-scoped, so keep that proof and change only the cause to a format that is
still refused:

```csharp
        /// <summary>
        /// Builds a texture asset in a refused format. ARGB4444 is allocatable
        /// directly and produces no console error; the producer refuses it at the
        /// format gate, before any GPU work.
        /// </summary>
        private static Texture2D CreateRefusedFormatTexture(string name)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.ARGB4444, false);
            texture.Apply();
            var path = TempFolder + "/" + name + ".asset";
            AssetDatabase.CreateAsset(texture, path);
            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(loaded, Is.Not.Null, path);
            return loaded;
        }
```

Rename the case `ARefusedFormatAlphaTextureRefusesOnlyItsOwnSubmesh`, replace
`ImportTexture("non_readable", readable: false)` with
`CreateRefusedFormatTexture("refused_format")`, and leave every assertion in the body
exactly as it stands: `RendererAnalysisRefusal.None` for the renderer,
`MissingTextureEvidence` for submesh 0, `Unchanged` disposition, submesh 1 still
proving `{ 0, 2 }`, `OpaqueTriangleCount` 2.

Then add the positive case proving the old refusal is really gone:

```csharp
        /// <summary>
        /// The milestone's headline change at renderer level: a non-readable
        /// texture, which refused before, now proves geometry.
        /// </summary>
        [Test]
        public void ANonReadableAlphaTextureNowProvesItsOwnSubmesh()
        {
            var nonReadable = ImportTexture("non_readable", readable: false);
            var material = NewSampledAlphaMaterial(nonReadable);
            var renderer = NewRenderer(BuildFixtureMesh(), material, material);

            var result = AnalyzeVerified(renderer, material, material);

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(
                result.Submeshes[0].Failure, Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(result.Plan.HasAnyOpaqueCandidates, Is.True);
        }
```

`ImportTexture` (`:87`) already produces a single-mip 4x4 fixture whose only
non-opaque texel is `(0, 0)`, and `BuildFixtureMesh`'s submesh-0 UVs
(`0.55-0.9`) lie wholly inside the opaque region, so this case needs no fixture
change at all.

#### 2c. The classifier lower-mip regression

`AlphaEvidenceClassifierIntegrationTests.Resolve` (`:95-112`) already builds a real
`TextureSample` over an imported texture and returns an `AlphaResolution`. It asserts
`TryGetSampling` succeeds, which now admits mipmapped textures.

Add `ImportMippedFixture` and `OddBoundaryPixels` to that file with the same bodies
as Step 1, then:

```csharp
        /// <summary>The exact UV support of source texel (4, 0) in an 8x8 texture.</summary>
        private static TriangleAlphaInput MipZeroOpaqueTexelTriangle()
        {
            return Triangle(
                new Vector2(0.51f, 0.01f),
                new Vector2(0.61f, 0.01f),
                new Vector2(0.51f, 0.11f));
        }

        /// <summary>
        /// A triangle wholly inside a texel that is exactly one at mip 0 must still
        /// not be proven opaque, because a lower level covering it is not.
        /// </summary>
        [Test]
        public void ATriangleInsideAMipZeroOpaqueTexelIsRefusedByALowerMip()
        {
            var texture = ImportMippedFixture(
                "classifier_odd_boundary", OddBoundaryPixels(), 8, 8);

            Assert.That(
                UnityAlphaFieldEvidence.TryCapture(texture, out _, out var chain),
                Is.True);
            Assert.That(
                chain[0].GetAlpha(4, 0), Is.EqualTo(255),
                "Precondition: mip 0 alone would prove this triangle opaque.");
            Assert.That(
                chain[1].GetAlpha(2, 0), Is.Not.EqualTo(255),
                "Precondition: the mip-1 texel covering it is not opaque.");

            var resolution = Resolve(texture);

            Assert.That(resolution.IsResolved, Is.True);
            Assert.That(
                resolution.Classify(MipZeroOpaqueTexelTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }
```

`OpaqueRegionTriangle()` (`:125`) is **not** reused: its UVs are `0.5-0.9` against a
4x4 texture and span four texels of an 8x8 one, so it cannot express the
single-texel precondition.

#### 2d. The build-handoff lower-mip regression

```csharp
        /// <summary>
        /// The mip conjunction reaching the production build handoff. Slot 0's
        /// single triangle lies wholly inside a texel that is exactly one at mip 0
        /// and below one at mip 1, so a mip-0-only build implementation would
        /// report TWO opaque candidates. Slot 1 is uniformly opaque at every level
        /// and must still prove, so the expected answer is exactly ONE — which
        /// distinguishes the conjunction from both a mip-0-only implementation and
        /// a blanket failure of texture-backed build analysis.
        /// </summary>
        [Test]
        public void RuntimeStateIntegration_ALowerMipPreventsAMipZeroOnlyOpaqueProof()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE build lower mip");
            Mesh mesh = null;
            Material blockedMaterial = null;
            Material controlMaterial = null;
            AnimatorController controller = null;
            AnimationClip clip = null;

            try
            {
                // Inside the try: if folder creation or the first import throws,
                // the root GameObject and any partially created folder are still
                // released by the finally below.
                if (!AssetDatabase.IsValidFolder(LowerMipTempFolder))
                {
                    AssetDatabase.CreateFolder("Assets", "AmuseTests_BuildLowerMip");
                }

                var blocked = ImportLowerMipTexture(
                    "odd_boundary", OddBoundaryAlphaPixels());
                var control = ImportLowerMipTexture(
                    "uniform_opaque", UniformOpaqueAlphaPixels());

                // Causal preconditions. Without these the final count could be
                // right for the wrong reason.
                Assert.That(
                    UnityTextureEvidence.TryGetSourceId(blocked, out _), Is.True,
                    "The blocked texture must have a resolvable source identity.");
                Assert.That(
                    UnityTextureEvidence.TryGetSourceId(control, out _), Is.True,
                    "The control texture must have a resolvable source identity.");

                Assert.That(
                    UnityAlphaFieldEvidence.TryCapture(
                        blocked, out _, out var blockedChain),
                    Is.True);
                Assert.That(
                    blockedChain.Count, Is.EqualTo(blocked.mipmapCount),
                    "Every declared mip must be captured.");
                Assert.That(
                    blockedChain[0].GetAlpha(4, 0), Is.EqualTo(255),
                    "Precondition: mip 0 alone would prove slot 0's triangle opaque.");
                Assert.That(
                    blockedChain[1].GetAlpha(2, 0), Is.Not.EqualTo(255),
                    "Precondition: the mip-1 texel covering it is not opaque.");

                Assert.That(
                    UnityAlphaFieldEvidence.TryCapture(
                        control, out _, out var controlChain),
                    Is.True);
                for (var level = 0; level < controlChain.Count; level++)
                {
                    Assert.That(
                        controlChain[level].IsFullyOpaque, Is.True,
                        "Control level " + level + " must be opaque at every level.");
                }

                mesh = BuildLowerMipBuildMesh();
                blockedMaterial = SampledAlphaMaterial(blocked);
                controlMaterial = SampledAlphaMaterial(control);

                var renderer = root.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.sharedMaterials = new[] { blockedMaterial, controlMaterial };

                // One layer, one clip, animating something unrelated to alpha, as
                // CaptureVerifiedRuntimeStateEvidence requires.
                controller = AddAnimatedObjectReference(
                    root, "m_ProbeAnchor", root.transform, out clip);

                var result = AnalyzeVerifiedRuntimeStates(
                    root, renderer, out var evidence);

                Assert.That(
                    evidence.AdmittedMaterials, Has.Count.EqualTo(2),
                    "Evidence closure must have admitted both slots.");
                Assert.That(
                    result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None),
                    "The analysis must not refuse; the proof must fail on evidence.");

                Assert.That(
                    result.OpaqueCandidateTriangleCount, Is.EqualTo(1),
                    "Exactly one: the uniformly opaque control proves, and the " +
                    "odd-boundary triangle does not. A mip-0-only implementation " +
                    "would report two.");
            }
            finally
            {
                DestroyCommittedClone(root, controller);
                Object.DestroyImmediate(root);
                if (mesh != null) Object.DestroyImmediate(mesh);
                if (blockedMaterial != null) Object.DestroyImmediate(blockedMaterial);
                if (controlMaterial != null) Object.DestroyImmediate(controlMaterial);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
                if (AssetDatabase.IsValidFolder(LowerMipTempFolder))
                {
                    AssetDatabase.DeleteAsset(LowerMipTempFolder);
                }
            }
        }
```

The `try` opens **before** the temporary folder is created, so a failure in
`CreateFolder` or in the first import still releases the root GameObject and deletes
whatever part of the folder exists. `ImportLowerMipTexture` wraps its in-memory
staging `Texture2D` in its own `try`/`finally`, so a throw from `EncodeToPNG` or
`File.WriteAllBytes` cannot leak it.

The outer `finally` order mirrors the existing build tests (`:715-721`): committed
clone first, then the root, then the objects this test created, then the original
controller graph, and last the asset folder. Every created asset, GameObject,
material, mesh, clip, controller and committed clone is released whether or not an
assertion failed. No shared fixture abstraction and no class-wide `SetUp`/`TearDown`
is introduced — the other build tests are untouched.

#### 2e. The shared-evidence identity assertion

`UnityMaterialEvidenceCaptureTests` already asserts at `:355` that one texture
assigned to two materials in a single `Capture` batch yields one shared
`CapturedTextureEvidence`. Extend that same test by one line:

```csharp
            Assert.That(
                ReferenceEquals(main.Texture.AlphaChannel, emission.Texture.AlphaChannel),
                Is.True,
                "Both assignments share one evidence object, so both share one " +
                "chain instance.");
```

**State the claim exactly.** `ReferenceEquals` proves the two assignments **share
one evidence object and one chain instance**. It does **not** prove the GPU capture
executed exactly once — a second capture that happened to be discarded would leave
this assertion green. Once-per-batch acquisition is a **code-structure property**
confirmed by review of `UnityMaterialEvidenceCapture.Capture` (`:674-721`), where
`CaptureTexture` is called once per distinct `TextureSourceId` from the `identified`
dictionary. Do not add a counter, a hook, or any other instrumentation to observe the
call count; no comment in this test may claim more than shared identity.

#### 2f. The sampling admission and over-deletion tests

Replace `TryGetSampling_MipmappedTexture_IsRefused` in `UnityTextureEvidenceTests`:

```csharp
        /// <summary>
        /// The blanket mipmap refusal is gone: AlphaResolution now classifies every
        /// level of the captured chain, so "some level, bilinear within it" is
        /// exactly the model the conjunction covers. Unity's Bilinear filters within
        /// the selected level and selects a level without blending.
        /// </summary>
        [Test]
        public void TryGetSampling_MipmappedTexture_IsAdmitted()
        {
            var texture = Import(
                "mipped",
                sourceHasAlpha: true,
                importer => importer.mipmapEnabled = true);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = UnityEngine.TextureWrapMode.Repeat;

            Assert.That(texture.mipmapCount, Is.GreaterThan(1));
            Assert.That(
                UnityTextureEvidence.TryGetSampling(texture, out var sampling), Is.True);
            Assert.That(
                sampling,
                Is.EqualTo(new TextureSampling(
                    TextureFilterMode.Bilinear,
                    Alrauna.Amuse.Editor.Semantics.TextureWrapMode.Repeat)));
        }

        /// <summary>
        /// Over-deletion guard. Only the mipmapCount clause goes; a mipmapped
        /// texture that also carries a nonzero bias or anisotropy must still refuse.
        /// </summary>
        [Test]
        public void TryGetSampling_MipmappedWithBiasOrAnisotropy_StillRefuses()
        {
            var biased = Import(
                "mipped_biased", sourceHasAlpha: true,
                importer => importer.mipmapEnabled = true);
            biased.mipMapBias = -1f;
            Assert.That(UnityTextureEvidence.TryGetSampling(biased, out _), Is.False);

            var aniso = Import(
                "mipped_aniso", sourceHasAlpha: true,
                importer => importer.mipmapEnabled = true);
            aniso.anisoLevel = 4;
            Assert.That(UnityTextureEvidence.TryGetSampling(aniso, out _), Is.False);
        }
```

- [ ] **Step 3: Run the focused tests and record honestly what fails**

Run `UnityTextureEvidenceTests`, `RendererAlphaAnalysisIntegrationTests`,
`AlphaEvidenceClassifierIntegrationTests`, `UnityMaterialEvidenceCaptureTests`, and
`AmusePlatformFinishPluginTests`.

**Must FAIL — the blanket sampling refusal still exists, so `TryGetSampling` returns
false for every mipmapped fixture and no resolution is ever classified:**

| Case | Where it fails |
| --- | --- |
| `TryGetSampling_MipmappedTexture_IsAdmitted` | returns `false` |
| `ALowerMipPreventsAnOtherwiseMipZeroOnlyOpaqueProof` (renderer) | slot 0 refuses `UnsupportedSampling`, and the control slot proves nothing either, so submesh 1's expected `{ 0 }` is empty |
| `ATriangleInsideAMipZeroOpaqueTexelIsRefusedByALowerMip` (classifier) | `Resolve`'s `TryGetSampling` assertion fails outright |
| `RuntimeStateIntegration_ALowerMipPreventsAMipZeroOnlyOpaqueProof` (build) | `OpaqueCandidateTriangleCount` is `0`, not `1` |

**Must already PASS — these verify Task 5's acquisition, not this task's edit, and
must not be described as RED:**

| Case | Why it already passes |
| --- | --- |
| Every `TryCapture` precondition inside the four cases above — resolvable source ids, `chain.Count == mipmapCount`, mip 0 texel `(4,0)` is `255`, mip 1 texel `(2,0)` is not, control opaque at every level | Task 5 admits mipmapped, non-readable textures at the **acquisition** gate. Acquisition and sampling admission are independent gates; only the latter is still closed. |
| `ANonReadableAlphaTextureNowProvesItsOwnSubmesh` | Its fixture is single-mip, so the blanket refusal never applied to it. Task 5 made it pass. |
| `ARefusedFormatAlphaTextureRefusesOnlyItsOwnSubmesh` | Same single-mip fixture shape; the refusal it pins is the format gate, untouched here. |
| The shared-evidence identity assertion | Single-mip fixture; shared identity is a batching property Task 2 preserved. |
| `TryGetSampling_MipmappedWithBiasOrAnisotropy_StillRefuses` | Bias and anisotropy already refuse today, for their own clauses. It is an over-deletion guard, green before and after. |

Record which set each observed failure belongs to. A case in the second table failing
means something earlier is wrong; stop and diagnose rather than proceeding.

- [ ] **Step 4: Delete exactly one clause**

```csharp
            if (texture.mipMapBias != 0f ||
                texture.anisoLevel > 1)
            {
                return false;
            }
```

In the method's XML doc, delete only the word "mipmapped" from the refusal list. Add:

> Mipmapped sampling is admitted because the resolver classifies every level of the captured chain. Nonzero mip bias stays refused as conservative deferred coverage - the conjunction would in fact cover it, since bias only shifts which level is selected. Trilinear likewise stays refused for scope rather than soundness: interpolating between two levels whose contributing samples are all exactly one is itself exactly one, but the sampling vocabulary does not express trilinear and widening it is a separate milestone. Anisotropy stays refused because it averages texels across a footprint the classifier does not model at all.

- [ ] **Step 5: Run every affected suite GREEN**

Run all five suites named in Step 3. Expected: everything passes, including
`TryGetSampling_TrilinearFilter_IsRefused` and
`TryGetSampling_MismatchedWrap_IsRefused` unchanged, every pre-existing build test
unaffected by the test-local additions, and both tables from Step 3 now green.

---

## Task 7: Completion sweep

- [ ] **Step 1: Full EditMode run, both assemblies**

Run every test in `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor`. Expected: all green. Record the totals.

- [ ] **Step 2: Console classification**

The full suite deliberately provokes synthetic failures and may carry existing
harness or MCP noise, so an empty Console is not the expectation and must not be
required.

Clear the Console, run the full suite, then **classify every remaining entry** into
exactly one of:

1. **Expected synthetic** — an exception or error a test deliberately provokes, each
   traceable to the test that provokes it;
2. **Pre-existing harness or MCP noise** — present on a baseline run of the same
   suites before this milestone's changes;
3. **New and unexplained** — anything else.

The gate: **category 3 must be empty**, and specifically there must be **no**
`AsyncGPUReadback` failure and no compressed direct-readback error
(`doesn't support ReadPixels usage`, `is a compressed format which is not supported
by async read back`). Their absence is the evidence that the format allowlist is
evaluated before any GPU call. Report the counts in each category.

- [ ] **Step 3: Timing observation**

A before/after comparison is only honest if a baseline was actually recorded, so the
baseline is taken in **Task 1, Step 0**, before any production edit, from the same
runner and the same two suites. Re-run those two suites now and compare against the
recorded numbers.

If for any reason the Task 1 baseline was not captured, **report only the final
observed durations and make no before/after claim.** Do not reconstruct a baseline
from a dirty tree, and do not estimate one.

Either way: **do not** add a benchmark, budget, counter, or performance framework,
and do not tune anything on the strength of one run. If the numbers show real
pressure, report it as a finding for controller review.

- [ ] **Step 4: Source-asset integrity**

Confirm no fixture, avatar, importer setting, `QualitySettings`, scene, or prefab was modified:

```bash
git status --porcelain
git diff --stat -- ProjectSettings Assets
```

Expected: no `ProjectSettings` or `Assets` entry at all.

- [ ] **Step 5: Diff, whitespace and `.meta` audit**

```bash
git status --porcelain --untracked-files=all
git diff
git diff --check
git diff --cached --stat
```

Confirm: every new `.cs` has its `.meta`; `Editor/Host/Shaders.meta` exists; the
shader `.meta` still carries GUID `85ccb222632d847b6b653f0e05b1ee97`; exactly one
`.shader` outside `Tests/Editor/Semantics`; no trailing whitespace.

**`git diff --cached` must be empty.** The shader move appears as an unstaged
delete of the research path plus an untracked add at the product path — or, if Git
pairs them heuristically in `git status`, as an unstaged rename. Either shape is
correct. A *staged* rename record is not, and means `git mv` was used against this
plan's instructions.

- [ ] **Step 6: Manifest churn check**

Inspect the complete `Packages/manifest.json` and `Packages/packages-lock.json` diff. If and only if it is exactly the known host toolchain/sysroot churn and no intentional change shares those files:

```bash
git checkout HEAD -- Packages/manifest.json Packages/packages-lock.json
```

- [ ] **Step 7: Scope and unchanged-file audit**

Confirm unchanged: `TriangleAlphaClassifier.cs`, every `MaterialSemantics` file, the
Poiyomi and lilToon production frontends, every `.asmdef`, both `package.json` files,
and `AmusePlatformFinishPlugin.Configure()` — Task 6 adds build **tests** only and touches no production build code. Confirm `Assets/AmuseTests_BuildLowerMip` no longer
exists. Confirm no cache, registry, service, second provider, adapter, or injectable backend was introduced, and that every Task 4 predicate has a production caller.

- [ ] **Step 8: Census confirmation**

Confirm the Census Lab was not opened, read, listed, or modified at any point.

- [ ] **Step 9: Report and stop**

Report changed files, `.meta` accounting, test totals, the timing observation, remaining unsupported cases, the structural guarantees that review must confirm (`finally` placement in `TryAcquireLevel`, `TryCaptureChain`, and `RunHostCapabilityCheck`), and any point where the repository contradicted the specification. **Leave everything uncommitted.** Do not push, do not open a PR, and do not begin any follow-up work.

---

## Validation categories

The three honest categories from spec §13, as this plan realizes them.

| Category | Where |
| --- | --- |
| **Real Unity integration** | Non-readable mipmapped capture; each admitted format; `RGB24` fully opaque; lower-mip contradiction on `RGBA32` and `DXT5`; non-square row order; `chain.Count == mipmapCount`; production shader loads and is supported; host-capability check reports `true`; `RenderTexture.active` restored across ordinary capture; refusals for unresolvable identity, destroyed, null, non-`Texture2D`, and representative refused formats; mipmapped sampling admitted; renderer, classifier **and build-handoff** lower-mip propagation, each with a uniformly opaque control; shared evidence identity across a capture batch. |
| **Production-called pure predicates** | Twelve, all `internal`, all invoked by `TryCapture`, `TryAcquireLevel`, or `RunHostCapabilityCheck`: `IsAdmittedBuildTarget`, `IsAdmittedFormat` (tested exhaustively over the whole `TextureFormat` enum), `MipResidencyGatesPass`, `AreDimensionsUsable`, `HostCapabilitiesPass`, `SourceSamplingGatePasses` (every admitted format against both sampleability outcomes), `IsShaderUsable`, `IsExpectedTargetFormat`, `IsExpectedLevelSize`, `IsExpectedBufferLength`, `IsBinaryPredicateBuffer`, `MatchesExpectedPattern`. |
| **Structural, reviewed not induced** | Mid-loop level failure yielding no partial chain; a real gate-12 failure and its sticky-`false` behaviour; cleanup inside the host-capability check, which a memoized latch may skip entirely; temporary render-texture release and `Material` destruction after success, after readback failure, and after a post-allocation exception; once-per-batch GPU acquisition, which `ReferenceEquals` cannot prove. Guaranteed by `finally` placement, by constructing the chain only after the loop, and by `UnityMaterialEvidenceCapture.Capture`'s single `CaptureTexture` call per distinct source. Review confirms; no test induces and no instrumentation is added. |

Nothing in this plan asserts pool residency for `RenderTexture.GetTemporary`, because the repository has established no reliable observation of it.
