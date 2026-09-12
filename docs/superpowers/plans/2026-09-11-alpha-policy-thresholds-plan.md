# Alpha Policy Thresholds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add three user policy settings — Minimum Opaque Alpha Percentage, Transparency Noise Gate Percentage, Maximum Noise Texel Percentage — that widen the opacity proof's evidence by disclosed user policy, with inert defaults, an exact byte-bound capture seam, a masked mip-chain builder, a per-polygon density gate in the classifier, and a report marker.

**Architecture:** Policy enters at evidence capture as exact byte bounds (opaque bound `O`, noise bound `n`): capture binarizes each texel to opaque-evidence (`255`), erased (`AlphaTextureData.ErasedFlag`), or witness (`0`). The source-image reader additionally rebuilds its mip chain with masked box averages so erased strays cannot dilute coarser levels. The classifier keeps its exact arithmetic and gains one density rule: erased texels inside a polygon's support count as opaque evidence only when `e * 100 < F * c` over that polygon's consulted texels. The density percent flows plugin → analysis as a plain int; the bounds flow plugin → capture and live in cache keys.

**Tech Stack:** Unity 2022.3.22f1, C#, NUnit via Unity Test Framework (EditMode only), NDMF PlatformFinish passes. Compilation happens inside Unity; there is no dotnet build and no CLI test runner.

**Spec:** `docs/superpowers/specs/2026-09-10-alpha-policy-thresholds-design.md` (with investigation `docs/superpowers/investigations/2026-09-10-minimum-preserved-transparency-value.md`)

## Global Constraints

- Branch: `feat/min-preserved-transparency`. Base: `main` at `64a71a0`.
- Defaults are inert: `100 / 0 / 2` must reproduce base behavior bit for bit. A characterization guard test pins this.
- The classifier's exact arithmetic gains no floats and no epsilons. Density compares exact integers: `e * 100 < F * c`. Equality refuses.
- Cutout sources (shader cutoff below `1.0`) keep the shader cutoff. All three settings are inert for them.
- Every new inspector label and tooltip is plain technical English, short sentences, no contractions.
- Tests run through the Unity Test Runner, EditMode mode. Refresh Unity after new files. A filtered run reporting 0 tests is a failure. Record observed counts.
- RED/GREEN: observe the failing run before the fix. An assertion that passes on first run is characterization, never dressed up as RED.
- Commits happen only with the user's explicit authorization in the executing session. The commit steps below mark the intended unit boundaries; stage exactly the listed paths.
- Never record absolute or machine-specific paths in code, comments, or reports.

## File Structure

- Create `Packages/com.alrauna.amuse/Editor/Analysis/AlphaPolicyBounds.cs` — the byte-bound mapping and clamp rules. Pure, unit-tested without Unity textures.
- Modify `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs` — `AlphaTextureData.ErasedFlag`, the `Classify` overload, and the four support scans' density rule.
- Modify `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs` — policy-aware chain build with masked averaging, delegating the math to a new pure builder.
- Create `Packages/com.alrauna.amuse/Editor/Host/SourceImageMaskedChain.cs` — the pure masked box-average builder over decoded mip-0 bytes.
- Modify `Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs` — ternary flags and cache key on published mips.
- Modify `Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs` — ternary flags and cache key on published mips.
- Modify `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs` — bounds parameters threaded to all three routes.
- Modify `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs` — bounds selection per texture (shader cutoff wins).
- Modify `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs` — density percent threaded to `AlphaResolution.Classified` and `Classify`.
- Modify `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs` — density percent parameter at the `Resolve` call site.
- Modify `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs` — density percent parameter at the `Resolve` call site and through the analysis entry.
- Modify `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs` — three serialized fields and properties.
- Modify `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs` — three controls in the Alpha Separator foldout with the clamp.
- Modify `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` — three mappers, policy threading into capture and analysis.
- Modify `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs` and `AmuseReportStrings.cs` — the policy-active summary figure.
- Test files: `Tests/Editor/Analysis/AlphaPolicyBoundsTests.cs` (new), `Tests/Editor/Analysis/SourceImageMaskedChainTests.cs` (new), `Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`, `Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`, `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`, plus a component round-trip test appended where the settings slice put its component tests (`Tests/Editor/Build/AlphaSeparationPreparationTests.cs` used `serialized.FindProperty` for the mip field; follow that pattern).

---

### Task 1: Byte bounds — `AlphaPolicyBounds` and `ErasedFlag`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaPolicyBounds.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaPolicyBoundsTests.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs` (inside `AlphaTextureData`, near line 139)

**Interfaces:**
- Produces: `AlphaPolicyBounds.Inert` (`OpaqueBound == 255`, `NoiseBound == 0`), `AlphaPolicyBounds.From(int opaquePercent, int noisePercent)`, `AlphaPolicyBounds.ClampNoise(int opaquePercent, int noisePercent)` returning the inspector-clamped noise percent, `AlphaPolicyBounds.OpaqueBound` / `NoiseBound` bytes, `AlphaTextureData.ErasedFlag` (`byte == 1`). Every later task consumes these exact names.

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;
using Alrauna.Amuse.Editor.Analysis;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    public sealed class AlphaPolicyBoundsTests
    {
        [TestCase(100, 255)]
        [TestCase(99, 253)]
        [TestCase(0, 0)]
        [TestCase(1, 3)]
        [TestCase(50, 128)]
        public void OpaqueBoundIsTheCeilingByte(int percent, int expected)
        {
            Assert.That(
                AlphaPolicyBounds.From(percent, 0).OpaqueBound,
                Is.EqualTo((byte)expected));
        }

        [TestCase(0, 0)]
        [TestCase(1, 3)]
        [TestCase(2, 6)]
        [TestCase(100, 255)]
        public void NoiseBoundIsTheCeilingByte(int percent, int expected)
        {
            Assert.That(
                AlphaPolicyBounds.From(100, percent).NoiseBound,
                Is.EqualTo((byte)expected));
        }

        [Test]
        public void InertMatchesTheBaseContract()
        {
            var bounds = AlphaPolicyBounds.Inert;
            Assert.That(bounds.OpaqueBound, Is.EqualTo(byte.MaxValue));
            Assert.That(bounds.NoiseBound, Is.EqualTo((byte)0));
        }

        [TestCase(100, 0, 0)]
        [TestCase(50, 50, 49)]
        [TestCase(50, 80, 49)]
        [TestCase(0, 0, 0)]
        [TestCase(0, 40, 0)]
        [TestCase(1, 100, 0)]
        public void ClampKeepsNoiseStrictlyBelowOpaque(
            int opaquePercent,
            int noisePercent,
            int expected)
        {
            Assert.That(
                AlphaPolicyBounds.ClampNoise(opaquePercent, noisePercent),
                Is.EqualTo(expected));
        }

        [Test]
        public void EveryPercentMapsToAByteBelowTheNextStep()
        {
            for (var percent = 0; percent <= 100; percent++)
            {
                var bound = (int)AlphaPolicyBounds.From(percent, 0).OpaqueBound;
                Assert.That(bound * 100, Is.GreaterThanOrEqualTo(percent * 255),
                    $"percent {percent}");
                Assert.That((bound - 1) * 100, Is.LessThan(percent * 255),
                    $"percent {percent}");
            }
        }
    }
}
```

- [ ] **Step 2: Run to verify RED**

Refresh Unity, then run the EditMode filter `Alrauna.Amuse.Tests.Editor.Analysis.AlphaPolicyBoundsTests`. Expected: compile failure — `AlphaPolicyBounds` does not exist. That is the RED.

- [ ] **Step 3: Implement**

```csharp
namespace Alrauna.Amuse.Editor.Analysis
{
    /// <summary>
    /// The user's alpha policy as exact byte bounds. The opaque bound is
    /// the smallest byte that counts as opaque evidence. The noise bound
    /// is the smallest byte at or which a texel stops being noise, so a
    /// texel is noise exactly when its byte is below the noise bound.
    /// Both bounds map by ceiling so a percent never claims a byte its
    /// value cannot support, and the inert bounds reproduce the base
    /// exact-255 contract.
    /// </summary>
    internal readonly struct AlphaPolicyBounds
    {
        internal byte OpaqueBound { get; }
        internal byte NoiseBound { get; }

        internal static AlphaPolicyBounds Inert => new(byte.MaxValue, 0);

        private AlphaPolicyBounds(byte opaqueBound, byte noiseBound)
        {
            OpaqueBound = opaqueBound;
            NoiseBound = noiseBound;
        }

        internal static AlphaPolicyBounds From(
            int opaquePercent,
            int noisePercent)
        {
            return new(OpaqueCeilingByte(opaquePercent), OpaqueCeilingByte(noisePercent));
        }

        /// <summary>
        /// The smallest byte b with b * 100 at or above percent * 255,
        /// in exact integer arithmetic. Percent 100 maps to 255 and
        /// percent 0 maps to 0.
        /// </summary>
        internal static byte OpaqueCeilingByte(int percent)
        {
            if (percent <= 0)
            {
                return 0;
            }
            if (percent >= 100)
            {
                return byte.MaxValue;
            }
            return (byte)((percent * 255 + 99) / 100);
        }

        /// <summary>
        /// The inspector clamp: the noise percent stays strictly below
        /// the opaque percent, and an opaque percent of 0 forces the
        /// noise percent to 0. This keeps the erased, witness, and
        /// opaque bands all expressible.
        /// </summary>
        internal static int ClampNoise(int opaquePercent, int noisePercent)
        {
            if (opaquePercent <= 0)
            {
                return 0;
            }
            return noisePercent < opaquePercent ? noisePercent : opaquePercent - 1;
        }
    }
}
```

In `TriangleAlphaClassifier.cs`, inside `AlphaTextureData`, add beside the existing fields:

```csharp
        /// <summary>
        /// The stored value for an erased texel: noise under the user's
        /// gate. Capture writes only 255, 0, and this flag, so a fixture
        /// that stores 1 by hand must not expect erasure unless the
        /// classifier runs with an active density policy.
        /// </summary>
        internal const byte ErasedFlag = 1;
```

- [ ] **Step 4: Run to verify GREEN**

Run the same filter. Expected: 17 tests passed, 0 failed.

- [ ] **Step 5: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AlphaPolicyBounds.cs \
  Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaPolicyBoundsTests.cs
git commit -m "feat: exact alpha policy byte bounds and erased flag"
```

---

### Task 2: Masked chain builder for the source-image route

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Host/SourceImageMaskedChain.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/SourceImageMaskedChainTests.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs:22-108`

**Interfaces:**
- Consumes: `AlphaPolicyBounds`, `AlphaTextureData.ErasedFlag` from Task 1.
- Produces: `SourceImageMaskedChain.Build(IReadOnlyList<byte> mipZeroBytes, int width, int height, int mipCount, AlphaPolicyBounds bounds)` returning `AlphaTextureData[]` with mip 0 first; `SourceImageMaskedChain.DecodeByte(float sample)` the exact byte reconstruction; `SourceImageAlphaReader.TryReadSourceAlphaChain(Texture2D texture, TextureChannel channel, float cutoffThreshold, AlphaPolicyBounds bounds, out AlphaMipChain chain)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    public sealed class SourceImageMaskedChainTests
    {
        private static IReadOnlyList<byte> Uniform(int count, byte value)
        {
            var bytes = new byte[count];
            for (var i = 0; i < count; i++)
            {
                bytes[i] = value;
            }
            return bytes;
        }

        [Test]
        public void DecodeByteRoundTripsEveryByte()
        {
            for (var k = 0; k <= 255; k++)
            {
                var sample = k / 255f;
                Assert.That(
                    SourceImageMaskedChain.DecodeByte(sample),
                    Is.EqualTo(k),
                    $"byte {k}");
            }
        }

        [Test]
        public void InertBoundsReproduceThresholdAt255()
        {
            var bytes = new byte[] { 255, 254, 0, 128 };
            var levels = SourceImageMaskedChain.Build(
                bytes, 2, 2, 1, AlphaPolicyBounds.Inert);
            Assert.That(levels[0].GetAlpha(0, 0), Is.EqualTo(255));
            Assert.That(levels[0].GetAlpha(1, 0), Is.EqualTo(0));
            Assert.That(levels[0].GetAlpha(0, 1), Is.EqualTo(0));
            Assert.That(levels[0].GetAlpha(1, 1), Is.EqualTo(0));
        }

        [Test]
        public void SingleStraySubstitutesAtEveryLevel()
        {
            // 8x8, all 255 except one texel at 3. n = 6 erases it, so
            // every masked level averages 255 and no level witnesses.
            var bytes = Uniform(64, 255);
            bytes[0] = 3;
            var bounds = AlphaPolicyBounds.From(100, 2);
            var levels = SourceImageMaskedChain.Build(
                bytes, 8, 8, 4, bounds);
            Assert.That(levels.Length, Is.EqualTo(4));
            for (var level = 0; level < levels.Length; level++)
            {
                Assert.That(levels[level].IsFullyOpaque, Is.True,
                    $"level {level}");
            }
        }

        [Test]
        public void DenseNoiseKeepsWitnessing()
        {
            // Half the texels at 0 with n = 6: blocks stay mixed or all
            // noise, erased blocks carry the flag, and the level is not
            // fully opaque.
            var bytes = Uniform(64, 255);
            for (var i = 0; i < 64; i += 2)
            {
                bytes[i] = 0;
            }
            var bounds = AlphaPolicyBounds.From(100, 2);
            var levels = SourceImageMaskedChain.Build(
                bytes, 8, 8, 2, bounds);
            Assert.That(levels[0].IsFullyOpaque, Is.False);
            Assert.That(levels[0].GetAlpha(0, 0), Is.EqualTo(
                AlphaTextureData.ErasedFlag));
            Assert.That(levels[0].GetAlpha(1, 0), Is.EqualTo(255));
        }

        [Test]
        public void AllNoiseBlockCarriesTheErasedFlag()
        {
            var bytes = Uniform(4, 2);
            var bounds = AlphaPolicyBounds.From(100, 2);
            var levels = SourceImageMaskedChain.Build(
                bytes, 2, 2, 1, bounds);
            Assert.That(levels[0].GetAlpha(0, 0),
                Is.EqualTo(AlphaTextureData.ErasedFlag));
        }

        [Test]
        public void MaskedAverageAboveTheBoundIsOpaque()
        {
            // One 4x4 block holds two 255s and two 200s with n = 6:
            // nothing is noise, masked average 227.5 stays below O = 255,
            // so the block is witness.
            var bytes = Uniform(16, 255);
            bytes[0] = 200;
            bytes[1] = 200;
            var bounds = AlphaPolicyBounds.From(100, 2);
            var levels = SourceImageMaskedChain.Build(
                bytes, 4, 4, 1, bounds);
            Assert.That(levels[0].GetAlpha(0, 0), Is.EqualTo(0));
        }

        [Test]
        public void WitnessBandByteStaysWitness()
        {
            // O = 253 (99 percent): a texel at 254 is opaque evidence,
            // a texel at 200 is witness, a texel at 3 is noise.
            var bytes = new byte[] { 254, 200, 3, 255 };
            var bounds = AlphaPolicyBounds.From(99, 2);
            var levels = SourceImageMaskedChain.Build(
                bytes, 2, 2, 1, bounds);
            Assert.That(levels[0].GetAlpha(0, 0), Is.EqualTo(255));
            Assert.That(levels[0].GetAlpha(1, 0), Is.EqualTo(0));
            Assert.That(levels[0].GetAlpha(0, 1),
                Is.EqualTo(AlphaTextureData.ErasedFlag));
            Assert.That(levels[0].GetAlpha(1, 1), Is.EqualTo(255));
        }
    }
}
```

- [ ] **Step 2: Run to verify RED**

Run the EditMode filter `Alrauna.Amuse.Tests.Editor.Analysis.SourceImageMaskedChainTests`. Expected: compile failure — the builder does not exist.

- [ ] **Step 3: Implement the pure builder**

```csharp
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// Builds the source-image route's alpha chain under an alpha policy.
    /// The builder masks before averaging: a noise texel takes no part in
    /// its block's average, because a stray the user called invisible
    /// must not drag a coarser level below the opaque bound. A block
    /// whose every source texel is noise carries the erased flag. All
    /// comparisons are exact integers; no float crosses a verdict.
    /// </summary>
    internal static class SourceImageMaskedChain
    {
        /// <summary>
        /// Reconstructs the decoded byte from an RGBA32 sample. The
        /// decode stored byte divided by 255 as the nearest float, so
        /// the rounded product returns every byte exactly; the test
        /// pins all 256 values.
        /// </summary>
        internal static byte DecodeByte(float sample)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(sample) * 255f);
        }

        internal static AlphaTextureData[] Build(
            IReadOnlyList<byte> mipZeroBytes,
            int width,
            int height,
            int mipCount,
            AlphaPolicyBounds bounds)
        {
            var levels = new AlphaTextureData[mipCount];
            var levelWidth = width;
            var levelHeight = height;
            for (var mip = 0; mip < mipCount; mip++)
            {
                var flags = new byte[levelWidth * levelHeight];
                for (var by = 0; by < levelHeight; by++)
                {
                    // Exact half-open source partition: block (bx, by)
                    // covers x in [bx * width / levelWidth,
                    // (bx + 1) * width / levelWidth). Integer bounds
                    // never drop a source texel and never double-count
                    // one, so odd sizes partition cleanly.
                    var sourceTop = by * height / levelHeight;
                    var sourceBottom = (by + 1) * height / levelHeight;
                    for (var bx = 0; bx < levelWidth; bx++)
                    {
                        var sourceLeft = bx * width / levelWidth;
                        var sourceRight = (bx + 1) * width / levelWidth;
                        flags[by * levelWidth + bx] = BuildBlock(
                            mipZeroBytes,
                            width,
                            sourceLeft,
                            sourceRight,
                            sourceTop,
                            sourceBottom,
                            bounds);
                    }
                }
                levels[mip] = new AlphaTextureData(levelWidth, levelHeight, flags);
                levelWidth = Mathf.Max(1, levelWidth >> 1);
                levelHeight = Mathf.Max(1, levelHeight >> 1);
            }
            return levels;
        }

        private static byte BuildBlock(
            IReadOnlyList<byte> source,
            int sourceWidth,
            int startX,
            int endX,
            int startY,
            int endY,
            AlphaPolicyBounds bounds)
        {
            long sum = 0;
            long consulted = 0;
            for (var y = startY; y < endY; y++)
            {
                for (var x = startX; x < endX; x++)
                {
                    var b = source[y * sourceWidth + x];
                    if (bounds.NoiseBound > 0 && b < bounds.NoiseBound)
                    {
                        continue;
                    }
                    sum += b;
                    consulted++;
                }
            }
            if (consulted == 0)
            {
                return AlphaTextureData.ErasedFlag;
            }
            // Exact integer verdict: the masked average meets the
            // opaque bound exactly when sum >= bound * count.
            return sum >= (long)bounds.OpaqueBound * consulted
                ? byte.MaxValue
                : (byte)0;
        }
    }
}
```

Implementation notes for the executor, not optional:

- `mipCount` deeper than the chain that reaches 1x1 repeats the 1x1 block; `AlphaMipChain`'s shape rule (`max(1, previous >> 1)`) is honored by the halving loop above.
- When `bounds` equals `AlphaPolicyBounds.Inert`, `Build` must produce exactly the base binarization: 255 at byte 255, else 0. The `InertBoundsReproduceThresholdAt255` test pins it.
- The partition bounds are load-bearing: `bx * width / levelWidth` is exact integer arithmetic, so a 6-wide texture partitions its texels across the 6, 3, 2, 1 levels without dropping or doubling one. If a level-dimension test fails, suspect the partition first.

- [ ] **Step 4: Wire the reader to the builder**

In `SourceImageAlphaReader.TryReadSourceAlphaChain`:

- Change the signature to `TryReadSourceAlphaChain(Texture2D texture, TextureChannel channel, float cutoffThreshold, AlphaPolicyBounds bounds, out AlphaMipChain chain)`.
- When `bounds.OpaqueBound == byte.MaxValue && bounds.NoiseBound == 0` (inert) **or** the material owns a shader cutoff (`cutoffThreshold < 1f`), keep the existing per-level float loop exactly as it is today.
- Otherwise: read mip 0 once with `uncompressed.GetPixels(0)`, decode each channel sample with `SourceImageMaskedChain.DecodeByte` (keeping the existing `isAlphaNone` rule that forces 1.0f), build the byte list, and call `SourceImageMaskedChain.Build(bytes, uncompressed.width, uncompressed.height, mipCount, bounds)`.
- Callers of `TryReadSourceAlphaChain` (only `UnityStreamingTextureEvidence.cs:99`) pass the bounds through; Task 4 updates that caller.

- [ ] **Step 5: Run to verify GREEN**

Run `Alrauna.Amuse.Tests.Editor.Analysis.SourceImageMaskedChainTests`. Expected: 8 passed, 0 failed (7 brief cases plus the odd-width partition fixture added in fix round 1). Then run `Alrauna.Amuse.Tests.Editor.Analysis.AlphaMipChainTests` unchanged. Expected: all pass, proving the chain shape contract still holds.

- [ ] **Step 6: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/SourceImageMaskedChain.cs \
  Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Analysis/SourceImageMaskedChainTests.cs
git commit -m "feat: masked mip chain builder for the alpha policy"
```

---

### Task 3: Published-chain routes — ternary flags and cache keys

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs:144-260` (`TryCapture` overloads)
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs:65-105,182-203`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs:42-90,160-169`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityGeneratedTextureEvidenceTests.cs` (bounds-keyed cache re-capture)

**Interfaces:**
- Consumes: `AlphaPolicyBounds` from Task 1, `SourceImageAlphaReader.TryReadSourceAlphaChain` new signature from Task 2.
- Produces: `UnityAlphaFieldEvidence.TryCapture(Texture texture, TextureChannel channel, float cutoffThreshold, AlphaPolicyBounds bounds, out TextureSourceId source, out AlphaMipChain chain)` (the alpha-channel overload; the red-channel overload mirrors it). Cache keys gain the bounds: the generated path's `(instanceID, channel, threshold)` session key becomes `(instanceID, channel, threshold, bounds)` and the streaming path's disk cache key gains the same. The published-chain flag rule, shared inline in both routes: `byte >= bounds.OpaqueBound` stores `255`; `bounds.NoiseBound > 0 && byte < bounds.NoiseBound` stores `AlphaTextureData.ErasedFlag`; else `0`.

- [ ] **Step 1: Write the failing predicate test** (append to `Tests/Editor/Analysis/AlphaPolicyBoundsTests.cs`)

```csharp
        [TestCase(255, 0, 254, 0)]
        [TestCase(255, 0, 255, 255)]
        [TestCase(253, 6, 252, 0)]
        [TestCase(253, 6, 3, 1)]
        [TestCase(253, 6, 253, 255)]
        public void PublishedFlagRuleResolvesTheThreeBands(
            int opaqueBound,
            int noiseBound,
            int sample,
            int expected)
        {
            var bounds = AlphaPolicyBounds.From(opaqueBound, noiseBound);
            var resolved = expected == 255
                ? byte.MaxValue
                : (byte)expected;
            Assert.That(
                SourceImageMaskedChainTestsHelpers.PublishedFlag(
                    (byte)sample, bounds),
                Is.EqualTo(resolved));
        }
```

With the helper, in the same test file:

```csharp
    internal static class SourceImageMaskedChainTestsHelpers
    {
        // Mirrors the inline rule both published-chain routes use. Kept
        // next to the tests so the table above pins the exact rule the
        // two routes must implement.
        internal static byte PublishedFlag(byte sample, AlphaPolicyBounds bounds)
        {
            if (sample >= bounds.OpaqueBound)
            {
                return byte.MaxValue;
            }
            return bounds.NoiseBound > 0 && sample < bounds.NoiseBound
                ? AlphaTextureData.ErasedFlag
                : (byte)0;
        }
    }
```

- [ ] **Step 2: Run to verify the rule table GREEN, then make RED by wiring**

The predicate test passes trivially once the helper exists — it is the rule pin, not the RED. The RED for this task is behavioral: the streaming/generated routes do not yet apply it. Record the RED as: run `Alrauna.Amuse.Tests.Editor.Analysis.AlphaPolicyBoundsTests` after adding the table, then implement the routes; the route wiring is proven by the Task 5/7 barrier tests because both routes need live textures. Note this honestly in the run log: Task 3's own evidence is the pinned rule plus a clean compile.

- [ ] **Step 3: Implement the routes**

- `UnityAlphaFieldEvidence.TryCapture` overloads at lines 144-151 and 153-260 gain an `AlphaPolicyBounds bounds` parameter and pass it to `UnityGeneratedTextureEvidence.TryCapture` (line 230) and `UnityStreamingTextureEvidence.TryCapture` (line 248).
- `UnityGeneratedTextureEvidence`: the session key at line 84 becomes `(texture.GetInstanceID(), channel, threshold, bounds)`. The flag loop at lines 163-166 becomes:

```csharp
                                // The generated route's proof channel is
                                // .r at the exact arm and .g under a
                                // shader cutoff; that packing is pinned
                                // by the existing blit shader. Erasure
                                // reads the same byte the opaque test
                                // reads, and only in the exact arm,
                                // because shader-cutoff sources are
                                // gate-inert.
                                var isErased = threshold >= 1.0f &&
                                    bounds.NoiseBound > 0 &&
                                    data[i].r < bounds.NoiseBound;
                                var isOpaque = threshold >= 1.0f
                                    ? data[i].r >= bounds.OpaqueBound
                                    : (data[i].g / 255f) >= threshold;
                                flags[i] = isErased
                                    ? AlphaTextureData.ErasedFlag
                                    : isOpaque ? byte.MaxValue : (byte)0;
```

- [ ] **Step 4: Compile check and suite slice**

Refresh Unity. Run `Alrauna.Amuse.Tests.Editor.Analysis.AlphaPolicyBoundsTests` and `Alrauna.Amuse.Tests.Editor.Analysis.AlphaMipChainTests`. Expected: all pass, zero compile errors. A filtered run reporting 0 tests is a failure.

Add one cache-key test to `Tests/Editor/Host/UnityGeneratedTextureEvidenceTests.cs`: capture the same readable generated texture twice through `TryCapture`, first with `AlphaPolicyBounds.Inert` and then with gate-on bounds, and assert the second call returns a chain whose content differs (the erased flags prove a re-capture rather than a cache hit). If the editor lacks AsyncGPUReadback support the test cannot run; record that honestly as remaining manual validation rather than deleting the coverage.

- [ ] **Step 5: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs \
  Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs \
  Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs \
  Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaPolicyBoundsTests.cs
git commit -m "feat: three-band published-chain flags and bounds-aware capture keys"
```

---

### Task 3b: Direct GPU route — bounds-aware predicate shaders

Added 2026-09-11 by controller ruling (ledger Ruling 5, plan defect): the capture layer has a FOURTH route — the direct GPU predicate-shader path for ordinary non-streaming, non-generated textures — which is the primary path for real avatar content and was missing from the spec's route inventory. The policy must reach it or the feature is nearly inert in practice. This task is sequenced after Task 3's review and before Task 7's end-to-end test.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/Shaders/AmuseAlphaExactOne.shader`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/Shaders/AmuseRedExactOne.shader`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs` (`TryCapture` direct route, `TryCaptureChain`, `TryAcquireLevel`, `IsBinaryPredicateBuffer`)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs`

**Interfaces:**
- Consumes: `AlphaPolicyBounds` from Task 1.
- Produces: `TryCapture`'s full overload passes `bounds` to `TryCaptureChain`; the shaders take `_OpaqueBound` and `_NoiseBound` float properties (normalized, defaults 1.0 and 0.0) and emit three states: 255 when the sampled byte is at or above the opaque bound, `AlphaTextureData.ErasedFlag` (1) when strictly below the noise bound, else 0. Inert bounds reproduce today's output byte for byte.

**Semantics and soundness notes for the implementer:**
- The shaders compare in linear sample space: `raw >= _OpaqueBound` and `raw < _NoiseBound`. The sRGB transfer is strictly monotone and fixes 1.0, so byte-order comparisons survive the conversion exactly (the file's existing red-channel argument generalizes).
- Inert equivalence is absolute: with `_OpaqueBound = 1.0` and `_NoiseBound = 0.0`, the shaders must produce exactly today's binary output. The existing decode-exactness pins cover this.
- `IsBinaryPredicateBuffer` becomes a three-state validator: every byte is 0, `AlphaTextureData.ErasedFlag`, or 255. Nothing else passes.
- No cache key change: verify whether this route caches; if it does not, no key work exists. If it does, the bounds join the key as on the other routes.
- Environment honesty: these tests need AsyncGPUReadback like their siblings; if the editor lacks it, record the skip honestly as remaining manual validation.

**Test cycle:** RED first (the three-state validator and the shader bounds do not exist). Then refresh, run `Alrauna.Amuse.Tests.Editor.Host` (full Host assembly), and expect: new bounds-aware capture tests pass, every existing Host test passes unchanged. Full-assembly run at Task 8.

**Commit (authorized sessions only):** message "feat: bounds-aware direct GPU capture route"; stage the two shaders, their `.shader.meta` files are already tracked (no new files), `UnityAlphaFieldEvidence.cs`, and the test file.

### Task 4: Classifier density rule — the core RED/GREEN

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:211-296` (`Classify`), `:298-345` (point clamp), `:347-427` (bilinear repeat), `:444-513` (bilinear clamp), `:657-713` (point repeat)
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`

**Interfaces:**
- Consumes: `AlphaTextureData.ErasedFlag` from Task 1.
- Produces: `TriangleAlphaClassifier.Classify(TriangleAlphaInput triangle, AlphaTextureData texture, AlphaSamplingSettings sampling, AlphaUvEnvelope envelope, int maxNoiseTexelPercent)`. The existing four-argument `Classify` stays and delegates with `0`, so every existing call site and test compiles unchanged. `maxNoiseTexelPercent` of `0` makes erasure inert: an erased texel behaves exactly like a witness.

- [ ] **Step 1: Write the failing tests** (append to `TriangleAlphaClassifierTests.cs`, following the file's direct-call conventions)

```csharp
        private static TriangleAlphaOutcome ClassifyFullCover(
            AlphaTextureData texture,
            AlphaFilterMode filter,
            int maxNoiseTexelPercent)
        {
            // A triangle whose UV0 footprint covers every texel of the
            // 2x2 texture: the corner (1, 1) sits on the exact boundary
            // x + y = 2, so the closed domain intersects all four texel
            // cells and the scan consults all four.
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero, Vector3.right, Vector3.up,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f));
            return TriangleAlphaClassifier.Classify(
                triangle,
                texture,
                new AlphaSamplingSettings(filter, AlphaWrapMode.Clamp),
                AlphaUvEnvelope.Zero,
                maxNoiseTexelPercent);
        }

        [Test]
        public void SparseErasedStrayProvesOpaqueUnderTheGate()
        {
            // Four consulted texels, one erased: 1 * 100 < 50 * 4 is
            // true, so the stray substitutes and the triangle proves.
            var bytes = new byte[]
            {
                255, 255,
                255, AlphaTextureData.ErasedFlag,
            };
            var texture = new AlphaTextureData(2, 2, bytes);
            Assert.That(
                ClassifyFullCover(
                    texture, AlphaFilterMode.Point, 50),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void DenseErasedNoiseRefusesUnderTheGate()
        {
            // Three erased among four: 3 * 100 < 50 * 4 is false, so
            // erasure does not fire and the triangle stays unproven.
            var bytes = new byte[]
            {
                255, AlphaTextureData.ErasedFlag,
                AlphaTextureData.ErasedFlag,
                AlphaTextureData.ErasedFlag,
            };
            var texture = new AlphaTextureData(2, 2, bytes);
            Assert.That(
                ClassifyFullCover(
                    texture, AlphaFilterMode.Point, 50),
                Is.Not.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void EqualityAtTheDensityBoundRefuses()
        {
            // Two erased among four consulted: 2 * 100 < 50 * 4 is
            // false, so exact equality refuses. Kills >= in place of >.
            var bytes = new byte[]
            {
                255, 255,
                AlphaTextureData.ErasedFlag,
                AlphaTextureData.ErasedFlag,
            };
            var texture = new AlphaTextureData(2, 2, bytes);
            Assert.That(
                ClassifyFullCover(
                    texture, AlphaFilterMode.Point, 50),
                Is.Not.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void ZeroDensityKeepsErasureInert()
        {
            var bytes = new byte[]
            {
                255, 255,
                255, AlphaTextureData.ErasedFlag,
            };
            var texture = new AlphaTextureData(2, 2, bytes);
            Assert.That(
                ClassifyFullCover(
                    texture, AlphaFilterMode.Point, 0),
                Is.Not.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void WitnessByteAlwaysBlocksRegardlessOfDensity()
        {
            var bytes = new byte[]
            {
                255, 255,
                255, 200,
            };
            var texture = new AlphaTextureData(2, 2, bytes);
            Assert.That(
                ClassifyFullCover(
                    texture, AlphaFilterMode.Point, 99),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void FourArgumentClassifyTreatsTheFlagAsWitness()
        {
            var bytes = new byte[]
            {
                255, 255,
                255, AlphaTextureData.ErasedFlag,
            };
            var texture = new AlphaTextureData(2, 2, bytes);
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero, Vector3.right, Vector3.up,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f));
            Assert.That(
                TriangleAlphaClassifier.Classify(
                    triangle,
                    texture,
                    new AlphaSamplingSettings(
                        AlphaFilterMode.Point, AlphaWrapMode.Clamp),
                    AlphaUvEnvelope.Zero),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void BilinearClampSharesTheDensityRule()
        {
            var bytes = new byte[]
            {
                255, 255,
                255, AlphaTextureData.ErasedFlag,
            };
            var texture = new AlphaTextureData(2, 2, bytes);
            Assert.That(
                ClassifyFullCover(
                    texture, AlphaFilterMode.Bilinear, 50),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(
                ClassifyFullCover(
                    texture, AlphaFilterMode.Bilinear, 0),
                Is.Not.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }
```

The bilinear repeat and point repeat scans apply the identical pre-pass and predicate; the battery proves the rule on one point path and one bilinear path, and the shared helper shape makes a per-scan divergence a review-visible choice.

- [ ] **Step 2: Run to verify RED**

Run `Alrauna.Amuse.Tests.Editor.Analysis.TriangleAlphaClassifierTests`. Expected: compile failure — no five-argument `Classify` overload exists.

- [ ] **Step 3: Implement**

In `TriangleAlphaClassifier`:

- Add the five-argument overload. The four-argument `Classify` delegates with `0`. Validation is shared.
- The rule per scan, stated once and applied to all four scans:

```
Pre-pass (only when maxNoiseTexelPercent > 0 and the candidate window
holds at least one ErasedFlag texel):
    walk the candidate window;
    c = texels whose support interval intersects the polygon domain
        (the same ExactUvGeometry.Intersects test the loop applies);
    e = erased texels among those c;
    substitute = e * 100 < maxNoiseTexelPercent * c;   // exact integers
Main loop (unchanged except the first test):
    alpha = texture.GetAlpha(x, y);
    if alpha == byte.MaxValue: continue;
    if alpha == AlphaTextureData.ErasedFlag && substitute: continue;
    if the support interval intersects the domain:
        return MustRemainTransparent;
return ProvenOpaque;
```

- When the pre-pass finds no erased texel in the window, or the policy is `0`, the scan runs the original single-pass loop with identical behavior — no Intersects calls for opaque texels, same early exits, same `MaxSupportRegions` guard. Do not reorder the existing checks.
- `AlphaTextureData` fast paths stay byte-255-based. An all-erased texture has `IsFullyNonOpaque` true (no byte equals 255), so it answers `MustRemainTransparent` before the scans — correct and conservative, because with no opaque evidence anywhere no density rule can prove a polygon.

- [ ] **Step 4: Run to verify GREEN**

Run `Alrauna.Amuse.Tests.Editor.Analysis.TriangleAlphaClassifierTests` in full — the new tests and every existing oracle case. Expected: all pass. The existing cases must pass without modification; any existing failure means the inert path drifted, and that is a defect in the change, not in the tests.

- [ ] **Step 5: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs
git commit -m "feat: per-polygon noise density rule in the alpha classifier"
```

---

### Task 5: Thread the density percent through resolution and analysis

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:258-344` (`Classify`), `:356-523` (`Resolve`, `ResolveSampled`, `Classified`)
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs:246-247`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:497-498,561-563,584-588`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`

**Interfaces:**
- Consumes: the five-argument `Classify` from Task 4.
- Produces: `AlphaSemanticsResolver.Resolve(SemanticOutput<ScalarSemanticValue> alpha, AlphaFieldProvider fieldProvider, int maxNoiseTexelPercent)`; `AlphaResolution.Classified(AlphaMipChain chain, AlphaSamplingSettings sampling, UvMapping mapping, int maxNoiseTexelPercent)`. Both gain the trailing int; existing callers are updated, not defaulted — the analysis layer passes the plugin's mapped value explicitly. The product and uniform resolution paths ignore the percent (a constant alpha is not texture noise).

- [ ] **Step 1: Write the failing test** (append to `AlphaSemanticsResolverTests.cs`, using the file's chain-fixture conventions)

```csharp
        [Test]
        public void DensityPercentReachesClassificationThroughResolve()
        {
            // A chain whose sole level holds one erased texel beside
            // three opaque ones. The sampled triangle covers exactly
            // that 2x2 region. Resolve with density 50 proves; Resolve
            // with density 0 does not. Same evidence, same provider.
            var flags = new byte[]
            {
                255, 255,
                255, AlphaTextureData.ErasedFlag,
            };
            var chain = new AlphaMipChain(new[]
            {
                new AlphaTextureData(2, 2, flags),
            });
            AlphaFieldProvider provider = (TextureSourceId _, TextureChannel _, out AlphaMipChain c) =>
            {
                c = chain;
                return true;
            };
            var alpha = SampledAlpha(); // the file's existing sampled-value fixture
            var proven = AlphaSemanticsResolver.Resolve(alpha, provider, 50);
            var refused = AlphaSemanticsResolver.Resolve(alpha, provider, 0);

            Assert.That(
                proven.Classify(FlatTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(
                refused.Classify(FlatTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }
```

`SampledAlpha()` and `FlatTriangle()` are the fixture helpers this test file already uses for a texture-sample semantic over a `TextureSourceId` default and a unit triangle with UV0 covering the chain; reuse the existing helpers where they exist and add only what is missing, with the same style.

- [ ] **Step 2: Run to verify RED**

Run `Alrauna.Amuse.Tests.Editor.Analysis.AlphaSemanticsResolverTests`. Expected: compile failure — no three-argument `Resolve`.

- [ ] **Step 3: Implement**

- `Resolve`, `ResolveSampled`, and `Classified` gain the trailing `int maxNoiseTexelPercent`; `AlphaResolution` stores it beside `_sampling`; `Classify` passes it to the five-argument `TriangleAlphaClassifier.Classify`. Uniform, product, and refused paths ignore it.
- `AdmittedMaterialStates.cs:246-247` and `UnityRendererAlphaAnalysis.cs:561-563` pass their method's new `maxNoiseTexelPercent` parameter. `UnityRendererAlphaAnalysis.Analyze` and `GatherAlphaFields`' caller chain gain the parameter from the plugin; the internal helper at line 497 that passes `int.MaxValue, 1` for the caps passes `0` for density (that path is the structural refusal probe, which must not convert anything).
- The plugin supplies the value in Task 7; until then the analysis entry points take the int so this task compiles standalone.

- [ ] **Step 4: Run to verify GREEN**

Run `Alrauna.Amuse.Tests.Editor.Analysis.AlphaSemanticsResolverTests` and `Alrauna.Amuse.Tests.Editor.Analysis.AdmittedMaterialStatesTests` in full. Expected: all pass; existing tests updated only where they call `Resolve` or `Classified` directly, passing `0` explicitly — expectation values never change.

- [ ] **Step 5: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs \
  Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs \
  Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs
git commit -m "feat: thread the noise density percent to alpha classification"
```

---

### Task 6: Component fields and inspector controls

**Files:**
- Modify: `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs` (fields after line 55, properties after line 88)
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:66-80` (`DrawAlphaSeparator`) and the control methods after line 194
- Modify: the component round-trip test location used by the settings slice (`Tests/Editor/Build/AlphaSeparationPreparationTests.cs:2177-2179` shows the `serialized.FindProperty` pattern)

**Interfaces:**
- Consumes: `AlphaPolicyBounds.ClampNoise` from Task 1.
- Produces: `AmuseAvatarOptimizer.MinimumOpaqueAlphaPercent` (int, `[Range(0, 100)]`, default 100), `TransparencyNoiseGatePercent` (int, `[Range(0, 100)]`, default 0), `MaximumNoiseTexelPercent` (int, `[Range(0, 100)]`, default 2). Serialized names: `_minimumOpaqueAlphaPercent`, `_transparencyNoiseGatePercent`, `_maximumNoiseTexelPercent`.

- [ ] **Step 1: Write the failing round-trip test** (follow the mip-field pattern at `AlphaSeparationPreparationTests.cs:2177`)

```csharp
        [Test]
        public void AlphaPolicyFieldsRoundTripAndDefaultInert()
        {
            var root = new GameObject("AlphaPolicyRoundTrip");
            try
            {
                var optimizer = root.AddComponent<
                    global::Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                var so = new UnityEditor.SerializedObject(optimizer);
                Assert.That(
                    so.FindProperty("_minimumOpaqueAlphaPercent").intValue,
                    Is.EqualTo(100));
                Assert.That(
                    so.FindProperty("_transparencyNoiseGatePercent").intValue,
                    Is.EqualTo(0));
                Assert.That(
                    so.FindProperty("_maximumNoiseTexelPercent").intValue,
                    Is.EqualTo(2));

                so.FindProperty("_minimumOpaqueAlphaPercent").intValue = 99;
                so.FindProperty("_transparencyNoiseGatePercent").intValue = 5;
                so.FindProperty("_maximumNoiseTexelPercent").intValue = 10;
                so.ApplyModifiedProperties();

                Assert.That(optimizer.MinimumOpaqueAlphaPercent, Is.EqualTo(99));
                Assert.That(optimizer.TransparencyNoiseGatePercent, Is.EqualTo(5));
                Assert.That(optimizer.MaximumNoiseTexelPercent, Is.EqualTo(10));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
```

- [ ] **Step 2: Run to verify RED**

Run the EditMode filter for the holding test class. Expected: compile failure — the properties do not exist.

- [ ] **Step 3: Implement fields and controls**

Component: three fields with doc comments in the style of the existing three, stating the flattening each consents to, then the three properties.

Editor: in `DrawAlphaSeparator`, after `DrawCoverageSlider()`, add `DrawAlphaPolicyControls();` implementing three `EditorGUILayout.IntSlider` controls in spec order, following the `DrawCoverageSlider` pattern, with tooltips in plain technical English:

1. "Minimum Opaque Alpha Percentage" — alpha at or above this counts as opaque evidence. The tooltip states: alpha between this value and full opacity becomes fully opaque after a move.
2. "Transparency Noise Gate Percentage" — alpha below this is noise. The tooltip states: applies to materials without a shader cutoff; a published-mip limitation applies to textures without a source file; states the flattening.
3. "Maximum Noise Texel Percentage" — the per-polygon sparsity bound; the tooltip states: AMUSE ignores a polygon's noise only when the noise texels are strictly under this share of the texels it checks.

The gate control clamps its drawn value with `AlphaPolicyBounds.ClampNoise(alphaPercent, gatePercent)` and writes the clamped value back, so the serialized field never holds `V_b >= V_a`.

- [ ] **Step 4: Run to verify GREEN**

Run the holding test class filter. Expected: the new test and the existing mip round-trip test pass.

- [ ] **Step 5: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs \
  Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs
git commit -m "feat: alpha policy sliders with the noise-gate clamp"
```

---

### Task 7: Plugin mappers, threading, and the report marker

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:426-433` (mapping reads), `:567-615` (mapper region), `:782-786` (`GatherAlphaFields` call), and the analysis/capture call arguments in the renderer loop
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs:98-128` (`AvatarSummary`)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs` (the string table)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:50-55` (summary call)
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:431-441` (summary call sites)

**Interfaces:**
- Consumes: everything from Tasks 1-6.
- Produces: private static mappers `OpaquePercentFrom(AmuseAvatarOptimizer)`, `NoisePercentFrom(AmuseAvatarOptimizer)`, `MaxNoiseTexelPercentFrom(AmuseAvatarOptimizer)` mirroring `MinimumCoverageFrom`'s defensive style; `AmuseReports.AvatarSummary(GameObject avatarRoot, int analyzedRenderers, int movedTriangles, int untouchedRenderers, AmuseBuildPath buildPath, bool alphaPolicyActive)`.

- [ ] **Step 1: Write the failing tests**

In `AmusePlatformFinishPluginTests`, beside the existing summary test at lines 431-441:

```csharp
        [Test]
        public void SummaryNamesPolicyOnlyWhileAlphaPolicyIsActive()
        {
            var root = new GameObject("SummaryPolicy");
            try
            {
                AmuseReports.AvatarSummary(
                    root, 1, 2, 3, AmuseBuildPath.NonPlayNdmfBuild, false);
                AmuseBuildStatusStore.TryGet(
                    root.GetInstanceID(), out var inertStatus);
                Assert.That(
                    inertStatus.Contains("alpha policy"),
                    Is.False);

                AmuseReports.AvatarSummary(
                    root, 1, 2, 3, AmuseBuildPath.NonPlayNdmfBuild, true);
                AmuseBuildStatusStore.TryGet(
                    root.GetInstanceID(), out var activeStatus);
                Assert.That(
                    activeStatus.Contains("alpha policy"),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
```

The mapper tests follow the existing mapper-test pattern in the same file (locate the `ProofMipCapFrom` coverage and mirror it): missing component → `AlphaPolicyBounds.Inert` behavior (opaque percent 100, noise percent 0, density 0), stored values below range clamp defensively.

- [ ] **Step 2: Run to verify RED**

Run the EditMode filter for `AmusePlatformFinishPluginTests`. Expected: compile failure — no six-argument `AvatarSummary`.

- [ ] **Step 3: Implement**

- Mappers: `OpaquePercentFrom` clamps below 0 to 0 and above 100 to 100; `NoisePercentFrom` applies `AlphaPolicyBounds.ClampNoise(opaque, stored)` after the same defensive clamps; `MaxNoiseTexelPercentFrom` clamps below 0 to 0. The renderer loop reads all three beside lines 428-430, builds `AlphaPolicyBounds.From(opaque, noise)`, and passes the bounds into the capture call chain and `maxNoiseTexelPercent` into the analysis call chain. The capture handoff (`UnityAnimationEvidenceCapture.Capture` variants at lines 471-488) gains the bounds parameter and forwards to `UnityMaterialEvidenceCapture`.
- Report: `AvatarSummary` gains the trailing `bool alphaPolicyActive`. When active, the composed status string and the report arguments gain one sentence from a new string-table entry `amuse.summary.PolicyActive:description`: "AMUSE moved triangles while the alpha policy was active, so some moved triangles rest on your alpha settings rather than on exact proof." The inert path composes exactly today's strings. Update both call sites (`AlphaSeparationApply.cs:51` passes whether the stored policy was active — thread one bool from the plugin through the separation state, beside `ReachedRendererAnalysis`) and the two test call sites at lines 432 and 439 (`false`).
- `AmuseReportStrings`: add the new key. The completeness test enumerates refusal vocabularies only, so no test change is needed for the table itself.

- [ ] **Step 4: End-to-end barrier test through the production path**

Mirror the existing mip-policy barrier fixture — the test that sets `_preserveTransparencyMaxMipLevel` through serialized properties at `Tests/Editor/Build/AlphaSeparationPreparationTests.cs:2177-2179` — with these differences:

- Set all three new component fields: `_minimumOpaqueAlphaPercent` 100, `_transparencyNoiseGatePercent` 2, `_maximumNoiseTexelPercent` 50 for the converting case; the gate percent 0 for the refusing case.
- The material is a fade-eligible fixture over a procedurally created readable 4x4 `Texture2D` whose alpha bytes are all 255 except one texel at 3. Build the texture with `SetPixels` and `Apply`; there is no asset file, so capture routes through the generated-texture path.
- Converting case: the barrier plans a split for the slot. Refusing case: the stray witnesses and the slot stays on its original material.
- Run both cases through the same harness the mip-policy test uses, and assert the plan disposition, not console output.

Expected: the converting case splits; the refusing case does not. Record observed outcomes.

- [ ] **Step 5: Run to verify GREEN**

Run `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests` in full. Expected: all pass.

- [ ] **Step 6: Commit (authorized sessions only)**
```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs \
  Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs \
  Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "feat: alpha policy mappers, capture threading, and report marker"
```

---

### Task 8: Characterization guard and full-suite validation

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs` (one guard test)

**Interfaces:**
- Consumes: the finished pipeline.

- [ ] **Step 1: Write the guard test**

```csharp
        [TestCase("fully-opaque-texture")]
        [TestCase("alpha-254-boundary")]
        [TestCase("fully-transparent-texture")]
        [TestCase("mixed-alpha-texture")]
        [TestCase("triangle-in-opaque-region")]
        [TestCase("triangle-in-transparent-region")]
        [TestCase("triangle-crosses-alpha-boundary")]
        public void InertPolicyMatchesOracle(string caseId)
        {
            // Runs the oracle cases through the five-argument Classify
            // with density 0 and the bounds-inert four-argument path,
            // asserting both agree with the reference fixture oracle.
            AssertCaseMatchesOracle(caseId);
        }
```

If `AssertCaseMatchesOracle` cannot route the density argument without refactoring, add the five-argument call inside the helper behind a `maxNoiseTexelPercent = 0` default so the same oracle data drives both paths. The assertion values must not change.

- [ ] **Step 2: Full validation**

1. Refresh Unity; confirm zero compile errors in the console.
2. Run the full `Alrauna.Amuse.Tests.Editor` assembly. Expected: every test passes, zero failures, zero inconclusive. Record the observed counts.
3. Run the full `Alrauna.Amuse.Research.Tests.Editor` assembly. Expected: every test passes. Record the observed counts.
4. `git diff --check` clean.
5. Identifier sweep over every changed file: an at sign joined to a hexadecimal hash, drive-letter paths, home-directory paths, four-digit ports, private asset names. Every hit is a defect; fix before reporting.

- [ ] **Step 3: Report**

Record in the execution summary: observed test counts for both assemblies, the RED evidence per task, any documented limitation hit (published-chain routes), and remaining risks. No commit, push, or PR without explicit authorization.

- [ ] **Step 4: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs
git commit -m "test: inert alpha policy characterization guard"
```

## Addendum: slider flip (2026-09-11, post-plan)

The user renamed all six inspector controls and inverted the
per-polygon pair after the plan's tasks completed. The classifier cap
became `100 - coverage` with the comparison flipped from a strict
share to a share at or under the cap. RED: EqualityAtTheCoverageBoundMoves,
observed failing before the flip. The clamp became the upper bound of
the tolerated stray band with a 100 sentinel for the inert noise
bound. Serialized fields were renamed with the user's acceptance that
saved values reset. See the spec's dated correction under section 6.
