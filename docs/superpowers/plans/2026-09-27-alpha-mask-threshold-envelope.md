# lilToon Alpha-Mask Threshold-Envelope Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove the general lilToon alpha-mask pair `saturate(r * _AlphaMaskScale + _AlphaMaskValue)` per triangle through an exact binary32 rounding-envelope contract, so assigned masks with any finite scale and value stop refusing and classify like the other mask shapes.

**Architecture:** A small exact map type in Semantics carries the affine-saturate map; the classifier gains mapped witness walks that answer one question (does the domain contain a sub-255 texel) and decide from the map evaluated at red bounds 0 and 1; the resolver gains a mapped classified resolution, two uniform arms, and product folds; the lilToon term and both frontends emit mapped samples where they refused before. Raw proof paths stay untouched.

**Tech Stack:** Unity 2022.3.22f1 editor assembly (`Alrauna.Amuse.Editor`, C# 9, `System.Numerics.BigInteger` available), NUnit via Unity Test Framework, EditMode only. Tests run in the dev editor instance through the Test Runner; there is no CLI test runner.

**Spec:** `docs/superpowers/specs/2026-09-27-alpha-mask-threshold-envelope-design.md`

## Global Constraints

- Tests never run in the Census Lab project. Every test run happens in the dev editor instance against this repository.
- Never stage or commit without explicit authorization at execution time. Commit steps below carry exact messages for when authorization is given.
- Raw classify paths keep their bodies and early exits; mapped logic is additive.
- The identity pair `(1, 0)` and `scale == 0` never construct a map; the factory throws and the term routes them to the existing arms.
- Modes 3 and 4 keep refusing. No evidence-request change: the mask request already carries `ScaleOffset | SourceIdentity | RedChannel` and the two scalars.
- New production files need a matching `.meta`; refresh Unity after adding files before running tests.
- A filtered test run that reports 0 tests is a failure. Record observed counts.
- After the last code task, run `git diff --check` and sweep changed files for identifiers (at-sign joined to a hex hash, drive-letter paths, home-directory paths, four-digit ports, private asset names).

---

### Task 1: Exact map type `AffineAlphaMap`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/AffineAlphaMap.cs` (plus `.meta` via Unity refresh)
- Test: Create `Packages/com.alrauna.amuse/Tests/Editor/Semantics/AffineAlphaMapTests.cs` (plus `.meta`)

**Interfaces:**
- Consumes: nothing.
- Produces: `AffineAlphaMap.FromBinary32(float scale, float value)`, `(float Lower, float Upper) Evaluate(float r)`, `IEquatable<AffineAlphaMap>`. Every later task consumes exactly these two members.

- [ ] **Step 1: Write the failing tests**

The tests do not compile until the type exists; that compile failure is the RED.

```csharp
using System;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    public class AffineAlphaMapTests
    {
        private static float Saturate(float value)
        {
            return value <= 0f ? 0f : value >= 1f ? 1f : value;
        }

        private static float RandomFinite(Random rng)
        {
            float value;
            do
            {
                value = (float)(rng.NextDouble() * 4.0 - 2.0);
            }
            while (float.IsNaN(value));
            return value;
        }

        [Test]
        public void Evaluate_ContainsBothHardwareOrders_ForPseudoRandomInputs()
        {
            var rng = new Random(20260927);
            var checkedPairs = 0;
            for (var index = 0; index < 4000; index++)
            {
                var scale = RandomFinite(rng);
                var value = RandomFinite(rng);
                if (scale == 0f || (scale == 1f && value == 0f))
                {
                    continue;
                }

                var map = AffineAlphaMap.FromBinary32(scale, value);
                var r = Saturate((float)(rng.NextDouble() * 1.5 - 0.25));
                var (lower, upper) = map.Evaluate(r);
                var fused = Saturate(MathF.FusedMultiplyAdd(r, scale, value));
                var unfused = Saturate(r * scale + value);

                Assert.That(lower, Is.LessThanOrEqualTo(fused),
                    $"lower exceeds fused for ({r}, {scale}, {value})");
                Assert.That(lower, Is.LessThanOrEqualTo(unfused),
                    $"lower exceeds unfused for ({r}, {scale}, {value})");
                Assert.That(upper, Is.GreaterThanOrEqualTo(fused),
                    $"upper drops below fused for ({r}, {scale}, {value})");
                Assert.That(upper, Is.GreaterThanOrEqualTo(unfused),
                    $"upper drops below unfused for ({r}, {scale}, {value})");
                if (fused == unfused)
                {
                    Assert.That(lower == fused && upper == fused,
                        $"envelope not tight at the agreeing point ({r}, {scale}, {value})");
                }
                checkedPairs++;
            }
            Assert.That(checkedPairs, Is.GreaterThan(3000));
        }

        [Test]
        public void Evaluate_AtOne_ProvesTheSaturatedPairExactlyOne()
        {
            var map = AffineAlphaMap.FromBinary32(1f, 0.5f);
            var (lower, upper) = map.Evaluate(1f);
            Assert.That(lower == 1f && upper == 1f, Is.True);
        }

        [Test]
        public void Evaluate_AtZero_IsTheClampedValue_Alone()
        {
            var map = AffineAlphaMap.FromBinary32(1f, 0.25f);
            var (lower, upper) = map.Evaluate(0f);
            Assert.That(lower == 0.25f && upper == 0.25f, Is.True);
        }

        [Test]
        public void Evaluate_WithNegativeScale_SwapsTheEnvelopeEndpoints()
        {
            var map = AffineAlphaMap.FromBinary32(-1f, 1f);
            var (lower, upper) = map.Evaluate(0.25f);
            // saturate(0.25 * -1 + 1) == 0.75 exactly in binary32.
            Assert.That(lower == 0.75f && upper == 0.75f, Is.True);
        }

        [Test]
        public void Evaluate_WithOverflowingSum_ClampsToExactlyOne()
        {
            var map = AffineAlphaMap.FromBinary32(1e30f, 1e30f);
            var (lower, upper) = map.Evaluate(1f);
            Assert.That(lower == 1f && upper == 1f, Is.True);
        }

        [Test]
        public void Factory_RejectsIdentityZeroScaleAndNonFinite()
        {
            Assert.Throws<ArgumentException>(
                () => AffineAlphaMap.FromBinary32(1f, 0f));
            Assert.Throws<ArgumentException>(
                () => AffineAlphaMap.FromBinary32(0f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => AffineAlphaMap.FromBinary32(float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => AffineAlphaMap.FromBinary32(1f, float.PositiveInfinity));
        }

        [Test]
        public void Equality_ComparesTheExactRationalComponents()
        {
            var first = AffineAlphaMap.FromBinary32(2f, 0.5f);
            var second = AffineAlphaMap.FromBinary32(2f, 0.5f);
            var other = AffineAlphaMap.FromBinary32(2f, 0.25f);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.Not.EqualTo(other));
        }
    }
}
```

- [ ] **Step 2: Run the tests and watch them fail**

Run in the dev editor instance: refresh assets, then the EditMode filter `Alrauna.Amuse.Tests.Editor.Semantics.AffineAlphaMapTests`.
Expected: the assembly fails to compile because `AffineAlphaMap` does not exist. That failure is the RED. Record it.

- [ ] **Step 3: Implement the type**

Create `Packages/com.alrauna.amuse/Editor/Semantics/AffineAlphaMap.cs`:

```csharp
using System;
using System.Numerics;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// The exact binary32 map <c>saturate(r * scale + value)</c>. Both
    /// hardware evaluation orders are monotone nondecreasing in r, because
    /// round-to-nearest-half-even and saturate are monotone, so the term
    /// over any red interval is bounded by the evaluations at that
    /// interval's endpoints. Every evaluation is carried out on exact
    /// BigInteger rationals and rounded once per hardware step to the
    /// binary32 grid at half-even; there are no ulp margins. This is the
    /// rounding argument the 2026-09-07 mask composition design deferred.
    /// </summary>
    internal readonly struct AffineAlphaMap : IEquatable<AffineAlphaMap>
    {
        private readonly BigInteger _scaleNumerator;
        private readonly int _scaleExponent;
        private readonly BigInteger _valueNumerator;
        private readonly int _valueExponent;

        private AffineAlphaMap(
            BigInteger scaleNumerator,
            int scaleExponent,
            BigInteger valueNumerator,
            int valueExponent)
        {
            _scaleNumerator = scaleNumerator;
            _scaleExponent = scaleExponent;
            _valueNumerator = valueNumerator;
            _valueExponent = valueExponent;
        }

        internal static AffineAlphaMap FromBinary32(float scale, float value)
        {
            if (!IsFinite(scale))
            {
                throw new ArgumentOutOfRangeException(nameof(scale));
            }
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            if (scale == 1f && value == 0f)
            {
                throw new ArgumentException(
                    "The identity pair routes to the plain sample arm and " +
                    "never constructs a map.",
                    nameof(scale));
            }
            if (scale == 0f)
            {
                throw new ArgumentException(
                    "A zero scale routes to the constant arm and never " +
                    "constructs a map.",
                    nameof(scale));
            }

            Decompose(scale, out var scaleNumerator, out var scaleExponent);
            Decompose(value, out var valueNumerator, out var valueExponent);
            return new AffineAlphaMap(
                scaleNumerator, scaleExponent, valueNumerator, valueExponent);
        }

        /// <summary>
        /// Returns the lower and upper binary32 bounds of the mapped term at
        /// the exact input r, covering the fused and the multiply-then-add
        /// order. When the orders agree the envelope collapses to a point.
        /// </summary>
        internal (float Lower, float Upper) Evaluate(float r)
        {
            if (!IsFinite(r))
            {
                throw new ArgumentOutOfRangeException(nameof(r));
            }

            Decompose(r, out var rn, out var rd);
            var productNumerator = rn * _scaleNumerator;
            var productExponent = rd + _scaleExponent;

            var fused = Saturate(Round(Sum(
                productNumerator, productExponent,
                _valueNumerator, _valueExponent)));

            var roundedProduct = Round(productNumerator, productExponent);
            Decompose(roundedProduct, out var pn, out var pd);
            var unfused = Saturate(Round(Sum(
                pn, pd, _valueNumerator, _valueExponent)));

            return fused <= unfused ? (fused, unfused) : (unfused, fused);
        }

        public bool Equals(AffineAlphaMap other)
        {
            return _scaleNumerator.Equals(other._scaleNumerator) &&
                   _scaleExponent.Equals(other._scaleExponent) &&
                   _valueNumerator.Equals(other._valueNumerator) &&
                   _valueExponent.Equals(other._valueExponent);
        }

        public override bool Equals(object obj)
        {
            return obj is AffineAlphaMap other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = _scaleNumerator.GetHashCode();
                hash = hash * 31 + _scaleExponent.GetHashCode();
                hash = hash * 31 + _valueNumerator.GetHashCode();
                hash = hash * 31 + _valueExponent.GetHashCode();
                return hash;
            }
        }

        /// <summary>Decomposes a finite float into n * 2^d with integer n.</summary>
        private static void Decompose(float value, out BigInteger n, out int d)
        {
            var bits = BitConverter.SingleToInt32Bits(value);
            var rawExponent = (int)((bits >> 23) & 0xFF);
            if (rawExponent == 0)
            {
                n = bits & 0x007FFFFF;
                d = -149;
            }
            else
            {
                n = (bits & 0x007FFFFF) | 0x00800000;
                d = rawExponent - 150;
            }

            if (bits < 0)
            {
                n = -n;
            }
        }

        /// <summary>Exact sum (n1 * 2^d1) + (n2 * 2^d2) as n * 2^d.</summary>
        private static (BigInteger n, int d) Sum(
            BigInteger n1, int d1, BigInteger n2, int d2)
        {
            var d = Math.Min(d1, d2);
            return (n1 << (d1 - d) + (n2 << (d2 - d)), d);
        }

        /// <summary>
        /// Rounds n * 2^d to the nearest binary32 at half-even, infinities
        /// included, and never produces NaN.
        /// </summary>
        private static float Round(BigInteger n, int d)
        {
            if (n.IsZero)
            {
                return 0f;
            }

            var negative = n.Sign < 0;
            var abs = BigInteger.Abs(n);
            var exponent = (int)abs.GetBitLength() - 1 + d;

            if (exponent > 127)
            {
                return negative
                    ? float.NegativeInfinity
                    : float.PositiveInfinity;
            }

            BigInteger mantissa;
            if (exponent >= -126)
            {
                // Normal: 24 significant bits, implicit leading one.
                var shift = (int)abs.GetBitLength() - 24;
                if (shift > 0)
                {
                    mantissa = abs >> shift;
                    var remainder = abs - (mantissa << shift);
                    var half = BigInteger.One << (shift - 1);
                    if (remainder > half ||
                        (remainder == half && mantissa.IsEven))
                    {
                        mantissa += 1;
                    }
                }
                else
                {
                    mantissa = abs << -shift;
                    shift = -shift;
                }

                if (mantissa == (BigInteger.One << 24))
                {
                    mantissa >>= 1;
                    exponent += 1;
                    if (exponent > 127)
                    {
                        return negative
                            ? float.NegativeInfinity
                            : float.PositiveInfinity;
                    }
                }

                var bits = (int)((exponent + 127) << 23) |
                           (int)(mantissa & 0x007FFFFF);
                return Int32BitsToSingle(bits, negative);
            }

            // Subnormal: grid spacing 2^-149.
            var subShift = 149 + d;
            if (subShift >= 0)
            {
                mantissa = abs << subShift;
            }
            else
            {
                mantissa = abs >> -subShift;
                var remainder = abs - (mantissa << -subShift);
                var half = BigInteger.One << (-subShift - 1);
                if (remainder > half ||
                    (remainder == half && mantissa.IsEven))
                {
                    mantissa += 1;
                }
            }

            if (mantissa == 0)
            {
                return 0f;
            }

            if (mantissa >= (BigInteger.One << 23))
            {
                // Rounded up into the smallest normal.
                var bits = (int)(1 << 23) | (int)((mantissa - (BigInteger.One << 23)) & 0x007FFFFF);
                return Int32BitsToSingle(bits, negative);
            }

            return Int32BitsToSingle((int)mantissa, negative);
        }

        private static float Int32BitsToSingle(int bits, bool negative)
        {
            var result = BitConverter.Int32BitsToSingle(bits);
            return negative ? -result : result;
        }

        private static float Saturate(float value)
        {
            return value <= 0f ? 0f : value >= 1f ? 1f : value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
```

Implementation notes for the executor: `Sum` shifts by `d1 - d` only when nonnegative, so order the arguments so the larger exponent shifts left. `Int32BitsToSingle` of the sign-cleared bits followed by negation is exact because binary32 negation is exact, including zero and infinity. If any property test exposes a rounding bug, fix `Round`, never the test.

- [ ] **Step 4: Run the tests to green**

Same filter as Step 2. Expected: 7 run, 7 passed. Record the observed counts. If a property case fails, reduce the seed case to a triple and verify against `MathF.FusedMultiplyAdd` by hand before changing `Round`.

- [ ] **Step 5: Commit (when authorized)**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/AffineAlphaMap.cs Packages/com.alrauna.amuse/Editor/Semantics/AffineAlphaMap.cs.meta Packages/com.alrauna.amuse/Tests/Editor/Semantics/AffineAlphaMapTests.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/AffineAlphaMapTests.cs.meta
git commit -m "feat: add the exact alpha mask affine map"
```

---

### Task 2: Algebra carries mapped samples and chain maps

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs` (the kind enum around line 373, the private constructor and `ProductChain` around lines 398-590, the chain accessors around lines 645-668)
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs` (`Multiply` and `CollectFactors`, lines 714-760)
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs` (`Multiply` and `CollectFactors`, lines 634-673)
- Test: Modify `Packages/com.alrauna.amuse/Tests/Editor/Semantics/MaterialSemanticsTests.cs`

**Interfaces:**
- Consumes: `AffineAlphaMap` from Task 1.
- Produces: `ScalarSemanticValueKind.MappedTextureSample`, `ScalarSemanticValue.MappedTexture(TextureSample, TextureChannel, AffineAlphaMap)`, `GetMap()`, `ProductChain(samples, channels, multiplier, maps)` with per-factor maps (null means identity), `GetChainMap(int)` returning null for identity factors, and `Multiply`/`CollectFactors` gathering maps. The resolver (Task 4) and frontends (Tasks 5-6) consume these names.

- [ ] **Step 1: Write the failing tests**

Add to `MaterialSemanticsTests.cs`:

```csharp
        [Test]
        public void MappedTexture_CarriesSampleChannelAndMap()
        {
            var sample = NewSample("mapped_carry");
            var map = AffineAlphaMap.FromBinary32(0.5f, 0.5f);
            var value = ScalarSemanticValue.MappedTexture(
                sample, TextureChannel.Red, map);
            Assert.That(value.Kind, Is.EqualTo(
                ScalarSemanticValueKind.MappedTextureSample));
            Assert.That(value.GetTextureSample(), Is.EqualTo(sample));
            Assert.That(value.GetChannel(), Is.EqualTo(TextureChannel.Red));
            Assert.That(value.GetMap(), Is.EqualTo(map));
        }

        [Test]
        public void ProductChain_KeepsPerFactorMaps_AndDefaultsNull()
        {
            var first = NewSample("chain_map_first");
            var second = NewSample("chain_map_second");
            var map = AffineAlphaMap.FromBinary32(2f, 1f);
            var mapped = ScalarSemanticValue.ProductChain(
                new[] { first, second },
                new[] { TextureChannel.Alpha, TextureChannel.Red },
                1f,
                new[] { null, map });
            Assert.That(mapped.GetChainMap(0), Is.Null);
            Assert.That(mapped.GetChainMap(1), Is.EqualTo(map));

            var plain = ScalarSemanticValue.ProductChain(
                new[] { first, second },
                new[] { TextureChannel.Alpha, TextureChannel.Red },
                1f);
            Assert.That(plain.GetChainMap(0), Is.Null);
            Assert.That(plain.GetChainMap(1), Is.Null);
        }

        [Test]
        public void ProductChain_AllowsASingleFactorOnlyWhenItCarriesAMap()
        {
            var sample = NewSample("chain_map_single");
            var map = AffineAlphaMap.FromBinary32(2f, 1f);
            Assert.Throws<ArgumentException>(
                () => ScalarSemanticValue.ProductChain(
                    new[] { sample },
                    new[] { TextureChannel.Red },
                    1f));
            var single = ScalarSemanticValue.ProductChain(
                new[] { sample },
                new[] { TextureChannel.Red },
                0.5f,
                new[] { map });
            Assert.That(single.GetChainFactorCount(), Is.EqualTo(1));
            Assert.That(single.GetChainMap(0), Is.EqualTo(map));
        }

        [Test]
        public void MappedTexture_RejectsIdentityMap()
        {
            var sample = NewSample("mapped_identity");
            Assert.Throws<ArgumentException>(
                () => ScalarSemanticValue.MappedTexture(
                    sample, TextureChannel.Red,
                    AffineAlphaMap.FromBinary32(1f, 0f)));
        }
```

Use the file's existing sample construction helper for `NewSample`; if none exists, construct `new TextureSample(source, mapping, sampling)` beside the file's existing fixtures. Match the file's namespace and using style.

- [ ] **Step 2: Run and watch them fail**

Filter `Alrauna.Amuse.Tests.Editor.Semantics.MaterialSemanticsTests`. Expected: compile failure, because `MappedTextureSample`, `MappedTexture`, `GetMap`, and the map overload do not exist. RED recorded.

- [ ] **Step 3: Implement the algebra changes**

In `MaterialSemantics.cs`:

1. Add `MappedTextureSample` to `ScalarSemanticValueKind`.
2. Add the field `private readonly AffineAlphaMap _map;` and a `private readonly AffineAlphaMap[] _chainMaps;`. Thread both through the private nine-argument constructor as a tenth and eleventh parameter, and update the two existing convenience constructor calls to pass `null, null`.
3. Add the factory and accessor:

```csharp
        /// <summary>
        /// One normalized scalar built as saturate(sample * scale + value)
        /// at the sampled coordinate, with the exact map carrying the
        /// rounding argument. The identity pair never reaches this kind:
        /// those materials are the existing plain sample.
        /// </summary>
        internal static ScalarSemanticValue MappedTexture(
            TextureSample sample,
            TextureChannel channel,
            AffineAlphaMap map)
        {
            ValidateTextureArguments(sample, channel);
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
            return new ScalarSemanticValue(
                ScalarSemanticValueKind.MappedTextureSample,
                default,
                sample,
                channel,
                default,
                null,
                null,
                null,
                null,
                null,
                map);
        }

        internal AffineAlphaMap GetMap()
        {
            RequireKind(ScalarSemanticValueKind.MappedTextureSample);
            return _map;
        }
```

4. Extend `ProductChain` with the map overload and relax the arity rule exactly one notch: a single-factor chain is valid only when it carries a non-null map, because the frontends produce that shape for a collapsed main constant times one mapped mask factor.

```csharp
        internal static ScalarSemanticValue ProductChain(
            IReadOnlyList<TextureSample> samples,
            IReadOnlyList<TextureChannel> channels,
            float multiplier,
            IReadOnlyList<AffineAlphaMap> maps)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }
            if (channels == null)
            {
                throw new ArgumentNullException(nameof(channels));
            }
            if (maps != null && maps.Count != samples.Count)
            {
                throw new ArgumentException(
                    "Every chain factor needs exactly one map entry.",
                    nameof(maps));
            }
            if (samples.Count == 0)
            {
                throw new ArgumentException(
                    "A chain carries at least one sampled factor.",
                    nameof(samples));
            }
            if (samples.Count == 1 && !HasMappedFactor(maps))
            {
                throw new ArgumentException(
                    "A single sample has its own kind unless it carries a " +
                    "map.",
                    nameof(samples));
            }
            for (var index = 0; index < samples.Count; index++)
            {
                ValidateTextureArguments(samples[index], channels[index]);
            }
            ValidateFinite(multiplier, nameof(multiplier));

            var value = new ScalarSemanticValue(
                ScalarSemanticValueKind.ProductChainOfTextureSamples,
                default,
                null,
                default,
                multiplier,
                null,
                null,
                null,
                null,
                CopyMaps(maps, samples.Count),
                null);
            var sampleCopy = new TextureSample[samples.Count];
            var channelCopy = new TextureChannel[channels.Count];
            for (var index = 0; index < samples.Count; index++)
            {
                sampleCopy[index] = samples[index];
                channelCopy[index] = channels[index];
            }

            return value.WithChain(sampleCopy, channelCopy);
        }

        private static bool HasMappedFactor(IReadOnlyList<AffineAlphaMap> maps)
        {
            if (maps == null)
            {
                return false;
            }
            for (var index = 0; index < maps.Count; index++)
            {
                if (maps[index] != null)
                {
                    return true;
                }
            }
            return false;
        }

        private static AffineAlphaMap[] CopyMaps(
            IReadOnlyList<AffineAlphaMap> maps,
            int count)
        {
            if (maps == null)
            {
                return null;
            }
            var copy = new AffineAlphaMap[count];
            for (var index = 0; index < count; index++)
            {
                copy[index] = maps[index];
            }
            return copy;
        }
```

Keep the existing three-argument `ProductChain` as a delegating overload: `ProductChain(samples, channels, multiplier, null)`. Move the existing body's arity guard so the two-argument minimum applies only when `HasMappedFactor` is false. Add:

```csharp
        internal AffineAlphaMap GetChainMap(int index)
        {
            RequireChain();
            return _chainMaps == null ? null : _chainMaps[index];
        }
```

5. Extend `WithChain` to carry `_chainMaps` through the private constructor.

6. Allow `GetTextureSample` and `GetChannel` for the mapped kind: remove `MappedTextureSample` handling from their throw lists only insofar as mapped instances hold a real sample and channel; add the mapped kind to the throw lists of `GetMultiplier`, `GetProductMultiplier`, `GetConstantValue` where the kind cannot answer.

In both lilToon frontends, extend `CollectFactors` and `Multiply`:

```csharp
        private static float CollectFactors(
            ScalarSemanticValue value,
            List<TextureSample> samples,
            List<TextureChannel> channels,
            List<AffineAlphaMap> maps,
            float multiplier)
        {
            switch (value.Kind)
            {
                // ... existing cases unchanged, except every recursive or
                // chain case now passes `maps` through and appends null per
                // plain factor ...
                case ScalarSemanticValueKind.MappedTextureSample:
                    samples.Add(value.GetTextureSample());
                    channels.Add(value.GetChannel());
                    maps.Add(value.GetMap());
                    return multiplier;
                // ...
            }
        }
```

In `Multiply`, create `var maps = default(List<AffineAlphaMap>);` — or a list grown alongside the samples, null entries appended for every plain factor — pass it to both `CollectFactors` calls, and pick the output shape:

```csharp
            if (samples.Count == 0)
            {
                return ScalarSemanticValue.Constant(multiplier);
            }

            if (samples.Count == 1 && !HasAnyMap(maps))
            {
                return ScalarSemanticValue.TextureTimesConstant(
                    samples[0], channels[0], multiplier);
            }

            return ScalarSemanticValue.ProductChain(
                samples, channels, multiplier, maps);
```

with a local `HasAnyMap` mirroring the algebra helper. The single-mapped-factor shape here is exactly the collapsed-main-constant times one mapped mask factor; the map list keeps the rounding chain honest. Keep the existing saturating-shape refusal untouched.

- [ ] **Step 4: Run to green**

Filters: `MaterialSemanticsTests`, then the two lilToon alpha test classes. Expected: the new tests pass and every existing frontend test keeps its current outcome (the map list is null on every existing path, so nothing moves). Record observed counts.

- [ ] **Step 5: Commit (when authorized)**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/MaterialSemanticsTests.cs
git commit -m "feat: carry mapped samples and chain maps in the value algebra"
```

---

### Task 3: Classifier mapped walks

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs` (add `ClassifyMapped` beside `Classify` at line 299, mapped walks beside each raw walk, and the shared decision tail)
- Test: Modify `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`

**Interfaces:**
- Consumes: `AffineAlphaMap.Evaluate` from Task 1.
- Produces: `TriangleAlphaClassifier.ClassifyMapped(TriangleAlphaInput, AlphaTextureData, AlphaSamplingSettings, AlphaUvEnvelope, AffineAlphaMap)` returning `TriangleAlphaOutcome`. Task 4 consumes it per mip level.

- [ ] **Step 1: Write the failing tests**

Add to `TriangleAlphaClassifierTests.cs`, matching the file's existing texture fixture helpers:

```csharp
        [Test]
        public void ClassifyMapped_AllOpaqueDomain_ProvesThroughTheMap()
        {
            var texture = NewUniformTexture(4, 4, byte.MaxValue);
            var map = AffineAlphaMap.FromBinary32(1f, 0.5f);
            var outcome = TriangleAlphaClassifier.ClassifyMapped(
                CornerTriangle(), texture, PointClampSettings(),
                AlphaUvEnvelope.Zero, map);
            // All-255 domain: red is exactly one, term is exactly one.
            Assert.That(outcome, Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void ClassifyMapped_WitnessDomain_ProvesTransparentOnlyBelowOne()
        {
            var texture = NewUniformTexture(4, 4, byte.MaxValue);
            texture.SetTexel(0, 0, 0);
            var map = AffineAlphaMap.FromBinary32(1f, 0.5f);
            var outcome = TriangleAlphaClassifier.ClassifyMapped(
                TriangleCoveringTexel(0, 0), texture, PointClampSettings(),
                AlphaUvEnvelope.Zero, map);
            // Witness red in [0, 1): term in [0.5, 1): unknown, not opaque.
            Assert.That(outcome, Is.EqualTo(TriangleAlphaOutcome.Unknown));

            var tight = AffineAlphaMap.FromBinary32(0.5f, 0f);
            var below = TriangleAlphaClassifier.ClassifyMapped(
                TriangleCoveringTexel(0, 0), texture, PointClampSettings(),
                AlphaUvEnvelope.Zero, tight);
            // Term in [0, 0.5): provably below one.
            Assert.That(below, Is.EqualTo(
                TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void ClassifyMapped_ErasedTexel_ActsAsWitness()
        {
            var texture = NewUniformTexture(4, 4, byte.MaxValue);
            texture.SetTexel(0, 0, AlphaTextureData.ErasedFlag);
            var map = AffineAlphaMap.FromBinary32(1f, 0.5f);
            var outcome = TriangleAlphaClassifier.ClassifyMapped(
                TriangleCoveringTexel(0, 0), texture, PointClampSettings(),
                AlphaUvEnvelope.Zero, map);
            // The mapped path never substitutes erasure: the red behind an
            // erased texel is unknown in [0, 1).
            Assert.That(outcome, Is.EqualTo(TriangleAlphaOutcome.Unknown));
        }

        [Test]
        public void ClassifyMapped_AnisotropicSampling_StaysUnknown()
        {
            var texture = NewUniformTexture(4, 4, 128);
            var map = AffineAlphaMap.FromBinary32(1f, 0.5f);
            var outcome = TriangleAlphaClassifier.ClassifyMapped(
                CornerTriangle(), texture, AnisotropicSettings(),
                AlphaUvEnvelope.Zero, map);
            Assert.That(outcome, Is.EqualTo(TriangleAlphaOutcome.Unknown));
        }
```

Use the file's existing helpers for texture construction, triangle inputs, and sampling settings wherever they exist; add the smallest local helpers where they do not. `SetTexel` names the file's existing texel mutation helper if present.

- [ ] **Step 2: Run and watch them fail**

Filter `Alrauna.Amuse.Tests.Editor.Analysis.TriangleAlphaClassifierTests`. Expected: compile failure, because `ClassifyMapped` does not exist. RED recorded.

- [ ] **Step 3: Implement the mapped paths**

In `TriangleAlphaClassifier.cs`, add beside `Classify`:

```csharp
        /// <summary>
        /// Classifies one triangle's mapped mask term saturate(r * s + v)
        /// under the same filter dispatch as the raw path. The walk answers
        /// one question: does the triangle's contributing domain contain a
        /// texel below byte 255? The field contract then bounds the filtered
        /// red to [0, 1) on a witness and exactly 1 without one, so the map
        /// evaluated at the red bounds 0 and 1 decides the level. Erased
        /// texels always count as witnesses: their red is unknown in [0, 1),
        /// and the raw erasure-substitution policy is a statement about the
        /// raw witness verdict, not about a red value this proof reads.
        /// </summary>
        internal static TriangleAlphaOutcome ClassifyMapped(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            AlphaSamplingSettings sampling,
            AlphaUvEnvelope envelope,
            AffineAlphaMap map)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
            ValidateDensityPolicy(0);
            ValidateFinite(triangle.Position0, nameof(triangle.Position0));
            ValidateFinite(triangle.Position1, nameof(triangle.Position1));
            ValidateFinite(triangle.Position2, nameof(triangle.Position2));

            if (ExactUvGeometry.IsDegenerateGeometry(triangle))
            {
                return TriangleAlphaOutcome.Unknown;
            }
            if (!triangle.HasUv0)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            ValidateFinite(triangle.Uv0, nameof(triangle.Uv0));
            ValidateFinite(triangle.Uv1, nameof(triangle.Uv1));
            ValidateFinite(triangle.Uv2, nameof(triangle.Uv2));

            if (texture.IsFullyOpaque)
            {
                return DecideMapped(map, witness: false);
            }
            if (texture.IsFullyNonOpaque)
            {
                return DecideMapped(map, witness: true);
            }

            if (sampling.AnisoMode == TextureAnisoMode.Anisotropic)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            if (sampling.FilterMode == TextureFilterMode.Point &&
                sampling.WrapMode == TextureWrapMode.Clamp)
            {
                return DecideMapped(
                    map,
                    HasMappedWitnessPointClamp(triangle, texture, envelope));
            }
            if (sampling.FilterMode == TextureFilterMode.Point &&
                sampling.WrapMode == TextureWrapMode.Repeat)
            {
                return DecideMapped(
                    map,
                    HasMappedWitnessPointRepeat(triangle, texture, envelope));
            }
            if (sampling.FilterMode == TextureFilterMode.Bilinear ||
                sampling.FilterMode == TextureFilterMode.Trilinear)
            {
                var witness = sampling.WrapMode == TextureWrapMode.Clamp
                    ? HasMappedWitnessBilinearClamp(
                        triangle, texture, envelope)
                    : HasMappedWitnessBilinearRepeat(
                        triangle, texture, envelope);
                return DecideMapped(map, witness);
            }

            return TriangleAlphaOutcome.Unknown;
        }

        /// <summary>
        /// The mapped three-way lattice on the map's endpoint envelope: a
        /// lower bound at exactly one proves the term one everywhere; an
        /// upper bound below one proves the term below one everywhere; the
        /// rest is unknown.
        /// </summary>
        private static TriangleAlphaOutcome DecideMapped(
            AffineAlphaMap map,
            bool witness)
        {
            var lower = witness
                ? map.Evaluate(0f).Lower
                : map.Evaluate(1f).Lower;
            var upper = map.Evaluate(1f).Upper;
            if (lower == 1f)
            {
                return TriangleAlphaOutcome.ProvenOpaque;
            }
            if (upper < 1f)
            {
                return TriangleAlphaOutcome.MustRemainTransparent;
            }
            return TriangleAlphaOutcome.Unknown;
        }
```

The four mapped walks mirror their raw siblings with one transformation: keep the raw walk's domain construction, window clamps, candidate-count guard, pre-filter, and exact intersection test; replace the witness verdict with an early `return true` on the first contributing texel whose byte differs from `MaxValue`, counting erased texels as witnesses (no substitution branch and no policy read); `return false` after the loops. Concretely:

- `HasMappedWitnessPointClamp` mirrors `ClassifyPointClamp` (`TriangleAlphaClassifier.cs:388-432`), minus `substituteErased`.
- `HasMappedWitnessPointRepeat` mirrors `ClassifyPointRepeat` the same way.
- `HasMappedWitnessBilinearClamp` mirrors `ClassifyBilinearClamp` (`TriangleAlphaClassifier.cs:666-763`), keeping `canPreFilter`, the boundary exception, and `ConservativeBilinearSupportOverlapsTriangle`, minus `substituteErased`.
- `HasMappedWitnessBilinearRepeat` mirrors `ClassifyBilinearRepeat` (`TriangleAlphaClassifier.cs:517-598`), keeping `NormalizeRepeat`, the cell-index guards, `FloorMod` unwrapping, and the pre-filter, minus `substituteErased`.

Example, the point clamp walk:

```csharp
        private static bool HasMappedWitnessPointClamp(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            AlphaUvEnvelope envelope)
        {
            var domain = ExactUvGeometry.CreateTextureScaledDomain(
                triangle, texture.Width, texture.Height, envelope);
            var minimumX = PointClampIndex(
                ExactUvGeometry.Minimum(domain, true),
                texture.Width,
                domain.TexelScale);
            var maximumX = PointClampIndex(
                ExactUvGeometry.Maximum(domain, true),
                texture.Width,
                domain.TexelScale);
            var minimumY = PointClampIndex(
                ExactUvGeometry.Minimum(domain, false),
                texture.Height,
                domain.TexelScale);
            var maximumY = PointClampIndex(
                ExactUvGeometry.Maximum(domain, false),
                texture.Height,
                domain.TexelScale);
            var candidateCount = (long)(maximumX - minimumX + 1) *
                                 (maximumY - minimumY + 1);
            if (candidateCount > MaxSupportRegions)
            {
                // An unbounded window cannot claim a bounded witness set;
                // unknown is the sound answer either way.
                return true;
            }

            for (var y = minimumY; y <= maximumY; y++)
            {
                for (var x = minimumX; x <= maximumX; x++)
                {
                    if (texture.GetAlpha(x, y) == byte.MaxValue)
                    {
                        continue;
                    }
                    if (ExactUvGeometry.Intersects(
                        domain,
                        PointClampInterval(x, texture.Width, domain.TexelScale),
                        PointClampInterval(y, texture.Height, domain.TexelScale)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
```

Note the one deliberate divergence: an over-large candidate window returns `true` (witness), not the raw `Unknown`, because the mapped decision tail has no third input; a witness bound with the red range [0, 1) yields the honest unknown through the map. Do the same in the repeat walks' overflow guards.

- [ ] **Step 4: Run to green**

Filter `TriangleAlphaClassifierTests` (whole class). Expected: new tests pass, every raw-path test unchanged. Record observed counts.

- [ ] **Step 5: Commit (when authorized)**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs
git commit -m "feat: classify mapped mask terms with exact witness walks"
```

---

### Task 4: Resolver mapped resolution and product folds

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs` (`Classified` factory around line 139, the per-level loop around lines 380-438, `Resolve` dispatch around lines 466-500, `ResolveProductChain` around lines 563-610)
- Test: Modify `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`

**Interfaces:**
- Consumes: `ClassifyMapped` (Task 3), `ScalarSemanticValueKind.MappedTextureSample` and `GetChainMap` (Task 2).
- Produces: `AlphaResolution.Classified(chain, sampling, mapping, maxNoiseTexelPercent, map = null)`; uniform folds inside the product chain. Task 5 consumes the mapped resolution through the frontends without new names.

- [ ] **Step 1: Write the failing tests**

Add to `AlphaSemanticsResolverTests.cs`, reusing the file's provider and chain fixtures:

```csharp
        [Test]
        public void MappedSample_UniformlyBelowOne_NeedsNoTexel()
        {
            var map = AffineAlphaMap.FromBinary32(0.5f, 0f);
            var value = ScalarSemanticValue.MappedTexture(
                NewSample("mapped_uniform"), TextureChannel.Red, map);
            var resolution = ResolveComplete(value, ThrowingProvider());
            Assert.That(resolution.TryGetUniformOutcome(out var outcome), Is.True);
            Assert.That(outcome, Is.EqualTo(
                TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void MappedSample_MissingField_RefusesByName()
        {
            var map = AffineAlphaMap.FromBinary32(1f, 0.5f);
            var value = ScalarSemanticValue.MappedTexture(
                NewSample("mapped_missing"), TextureChannel.Red, map);
            var resolution = ResolveComplete(value, NullProvider());
            Assert.That(resolution.IsResolved, Is.False);
            Assert.That(resolution.Failure, Is.EqualTo(
                AlphaResolutionFailure.MissingTextureEvidence));
        }

        [Test]
        public void MappedSample_ClassifiesThroughTheChain()
        {
            var map = AffineAlphaMap.FromBinary32(1f, 0.5f);
            var value = ScalarSemanticValue.MappedTexture(
                NewSample("mapped_classified"), TextureChannel.Red, map);
            var resolution = ResolveComplete(value, WhiteProvider());
            Assert.That(resolution.TryGetUniformOutcome(out _), Is.False);
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void ProductChain_UniformTransparentFactor_AbsorbsTheProduct()
        {
            var main = NewSample("fold_main");
            var mask = NewSample("fold_mask");
            var map = AffineAlphaMap.FromBinary32(0.5f, 0f);
            var value = ScalarSemanticValue.ProductChain(
                new[] { main, mask },
                new[] { TextureChannel.Alpha, TextureChannel.Red },
                1f,
                new[] { null, map });
            var resolution = ResolveComplete(value, WhiteProvider());
            Assert.That(resolution.TryGetUniformOutcome(out var outcome), Is.True);
            Assert.That(outcome, Is.EqualTo(
                TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void ProductChain_UniformOpaqueFactor_DropsOut()
        {
            var main = NewSample("drop_main");
            var mask = NewSample("drop_mask");
            var map = AffineAlphaMap.FromBinary32(1f, 1f);
            var value = ScalarSemanticValue.ProductChain(
                new[] { main, mask },
                new[] { TextureChannel.Alpha, TextureChannel.Red },
                1f,
                new[] { null, map });
            var resolution = ResolveComplete(value, WhiteProvider());
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }
```

`ThrowingProvider` throws on any lookup, falsifying an implementation that consults texels for the pure uniform arms; `NullProvider` returns false; `WhiteProvider` serves an all-255 chain. Match the file's existing provider fixtures where they exist.

- [ ] **Step 2: Run and watch them fail**

Filter `AlphaSemanticsResolverTests`. Expected: the new tests fail on compile (missing map parameter and dispatch case). RED recorded.

- [ ] **Step 3: Implement**

1. `AlphaResolution.Classified` gains an optional trailing parameter `AffineAlphaMap map = null`, stored in a new field; the constructor invariant "a classified value always has its field" stays.
2. In `AlphaResolution.Classify`'s per-level loop, when the stored map is non-null, call `TriangleAlphaClassifier.ClassifyMapped(transformed, _chain[index], _sampling, envelope, _map)` in place of `Classify`. The absorbing per-level fold, the without-evidence check, and the no-early-Unknown rule stay byte for byte.
3. Add the dispatch case in `Resolve`:

```csharp
                case ScalarSemanticValueKind.MappedTextureSample:
                    return ResolveMappedSample(
                        value.GetTextureSample(),
                        value.GetChannel(),
                        value.GetMap(),
                        fieldProvider,
                        maxNoiseTexelPercent);
```

4. Add `ResolveMappedSample`, mirroring `ResolveScaledSample`'s structure: the uniform arms still pay the provider lookup first, because the lookup attests the [0, 1] range and the complete chain that the arms' bounding argument relies on.

```csharp
        /// <summary>
        /// alpha = saturate(s * scale + value) over a sampled field bounded
        /// in [0, 1] by the contract. The map's endpoint evaluations decide
        /// two uniform arms with no texel: a lower bound of one at both red
        /// bounds proves the term one at every reachable sample, and an
        /// upper bound below one at both proves the opposite. Everything
        /// else classifies the chain under the map.
        /// </summary>
        private static AlphaResolution ResolveMappedSample(
            TextureSample sample,
            TextureChannel channel,
            AffineAlphaMap map,
            AlphaFieldProvider fieldProvider,
            int maxNoiseTexelPercent)
        {
            if (!fieldProvider(sample.Source, channel, out var chain) ||
                chain == null)
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.MissingTextureEvidence);
            }

            var atZero = map.Evaluate(0f);
            var atOne = map.Evaluate(1f);
            if (atZero.Lower == 1f && atOne.Lower == 1f)
            {
                return AlphaResolution.Uniform(
                    TriangleAlphaOutcome.ProvenOpaque);
            }
            if (atZero.Upper < 1f && atOne.Upper < 1f)
            {
                return AlphaResolution.Uniform(
                    TriangleAlphaOutcome.MustRemainTransparent);
            }

            return AlphaResolution.Classified(
                chain,
                sample.Sampling,
                sample.Mapping,
                maxNoiseTexelPercent,
                map);
        }
```

5. In `ResolveProductChain`, resolve each factor through its map and fold uniform factors:

```csharp
            AlphaResolution conjoined = null;
            for (var index = 0; index < value.GetChainFactorCount(); index++)
            {
                var map = value.GetChainMap(index);
                var factor = map == null
                    ? ResolveSampled(
                        value.GetChainSample(index),
                        value.GetChainChannel(index),
                        fieldProvider,
                        maxNoiseTexelPercent)
                    : ResolveMappedSample(
                        value.GetChainSample(index),
                        value.GetChainChannel(index),
                        map,
                        fieldProvider,
                        maxNoiseTexelPercent);
                if (!factor.IsResolved)
                {
                    return factor;
                }

                if (factor.TryGetUniformOutcome(out var uniform))
                {
                    if (uniform == TriangleAlphaOutcome.MustRemainTransparent)
                    {
                        return AlphaResolution.Uniform(
                            TriangleAlphaOutcome.MustRemainTransparent);
                    }

                    // A uniformly opaque factor multiplies by exactly one
                    // and drops out of the conjunction.
                    continue;
                }

                conjoined = conjoined == null
                    ? factor
                    : AlphaResolution.Product(conjoined, factor);
            }

            return conjoined ?? AlphaResolution.Uniform(
                TriangleAlphaOutcome.ProvenOpaque);
```

The `TryGetUniformOutcome` arm is reachable only for mapped factors: `ResolveSampled` never returns uniform, which the existing `AlphaResolution.Product` guard already assumed.

- [ ] **Step 4: Run to green**

Filter `AlphaSemanticsResolverTests`, then the whole `Analysis` folder. Expected: new tests pass, existing tests unchanged. Record observed counts.

- [ ] **Step 5: Commit (when authorized)**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs
git commit -m "feat: resolve mapped mask samples through the exact envelope"
```

---

### Task 5: Term arms and the transparent frontend

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs` (term kind enum at lines 12-20, doc block at lines 40-70, `Interpret` arms at lines 150-276)
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs` (the mask consumption site at lines 582-619)
- Test: Modify `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1-4.
- Produces: `LilToonAlphaMaskTermKind.MappedSample` with `Scale` and `Value` float accessors; transparent materials with any finite mask pair resolve. Task 6 mirrors for cutout.

- [ ] **Step 1: Flip the pinned refusals and write the new tests (RED)**

In `LilToonTransparentAlphaTests.cs`, change the two pinned refusal tests to their new provable outcomes. Today at lines 1069-1075 (`t_mask_val_low`, scale 1, value 0.5, all-white mask) the test asserts `AssertAlphaGateUnknown(InterpretTransparent(material), "_AlphaMaskValue")`; replace the assertion body with:

```csharp
            // The uniform white mask's red is exactly one, so the term is
            // exactly one: the mask composes nothing and the value proves.
            // The resolution is classified, not uniform, so the assertion
            // goes through the triangle classifier, using the same triangle
            // fixture the file's other resolved tests classify.
            var resolution = ResolveThroughTransparentFrontend(
                material, AllOpaqueChain());
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
```

Apply the same change to the scale-shifted row at lines 1082-1088 (`t_mask_scale_bad`), keeping the material setup. Leave the non-finite row at lines 1091-1097 exactly as it is: it must keep refusing.

Add new tests beside them:

```csharp
        [Test]
        public void MappedMask_GradientHole_TrianglesOverHolesStayTransparent()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_mapped_hole");
            material.SetFloat("_AlphaMaskMode", 2f);
            material.SetFloat("_AlphaMaskScale", 1f);
            material.SetFloat("_AlphaMaskValue", 0.5f);
            AssignPartBlackMask(material, "t_mapped_hole_mask");

            var resolution = ResolveThroughTransparentFrontend(
                material, AllOpaqueChain());
            // A mask texel at red zero bounds the term to [0.5, 1) on any
            // triangle whose footprint touches it: unknown, never opaque.
            Assert.That(
                resolution.Classify(TriangleOverBlackTexel()),
                Is.EqualTo(TriangleAlphaOutcome.Unknown));
            Assert.That(
                resolution.Classify(CornerTriangleOverWhite()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void MappedMask_Sub255Texel_NeverProvesOpaque()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_mapped_254");
            material.SetFloat("_AlphaMaskMode", 2f);
            material.SetFloat("_AlphaMaskScale", 1f);
            material.SetFloat("_AlphaMaskValue", 0.25f);
            AssignRed254Mask(material, "t_mapped_254_mask");

            var resolution = ResolveThroughTransparentFrontend(
                material, AllOpaqueChain());
            // A byte-254 red lies in [0, 1), so the term envelope is
            // [0.25, 1]: mixed, and never provably opaque, whichever real
            // red value the import carries. Falsifies treating sub-255
            // bytes as 254/255 or proving from the 255 texels alone.
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.Unknown));
        }

        [Test]
        public void MappedMask_NegativeValueOverSub255_ProvesTransparent()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_mapped_neg");
            material.SetFloat("_AlphaMaskMode", 2f);
            material.SetFloat("_AlphaMaskScale", 1f);
            material.SetFloat("_AlphaMaskValue", -0.25f);
            AssignRed254Mask(material, "t_mapped_neg_mask");

            var resolution = ResolveThroughTransparentFrontend(
                material, AllOpaqueChain());
            // The same byte-254 red with a negative value: the envelope is
            // [0, 0.75], provably below one over the whole footprint.
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void MappedMask_ZeroScaleReplace_IsTheClampedValue()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_mapped_zs");
            material.SetFloat("_AlphaMaskMode", 1f);
            material.SetFloat("_AlphaMaskScale", 0f);
            material.SetFloat("_AlphaMaskValue", 0.75f);
            AssignAllWhiteMask(material, "t_mapped_zs_mask");

            // Replace mode with a zero-scale mask is the constant
            // saturate(value): alpha 0.75 everywhere, provably below the
            // opaque bar.
            var resolution = ResolveThroughTransparentFrontend(
                material, AllOpaqueChain());
            Assert.That(resolution.TryGetUniformOutcome(out var outcome), Is.True);
            Assert.That(outcome, Is.EqualTo(
                TriangleAlphaOutcome.MustRemainTransparent));
        }
```

Write the mask fixtures with the file's existing grid helpers (`MaskGrid`, `AssignAllWhiteMask` and siblings): a part-black mask is the existing grid with at least one fully black cell whose texel footprint a triangle covers, and the red-254 mask sets one cell to the byte 254. Reuse existing triangle fixtures for the covering triangles; add the smallest local fixture where the file has none. The test names are behavior sentences per the house test style.

- [ ] **Step 2: Run and watch them fail**

Filter `LilToonTransparentAlphaTests`. Expected: the two flipped rows fail (they still answer Unknown at `_AlphaMaskValue`), the three new tests fail the same way, and the non-finite row passes. Record the observed counts. This is the RED for the feature.

- [ ] **Step 3: Implement the term arms**

In `LilToonAlphaMaskSemantics.cs`:

1. Add `MappedSample` to `LilToonAlphaMaskTermKind` with doc text "Modes 1 and 2 over a general finite pair: the term is the exact affine map of the sampled red."
2. Add `Scale` and `Value` float properties and a `MappedSampleOf(TextureSourceId source, UvMapping mapping, float scale, float value, bool replacesMainAlpha)` factory mirroring `SampleOf`.
3. In `Interpret`, after the `(1, 0)` sample arm and before the final refusal, add the zero-scale arm and replace the fall-through:

```csharp
            if (scale == 0f)
            {
                // 0 * s is exactly zero and zero + v is exactly v in
                // binary32, so the term is saturate(v) under both orders
                // with the same operands the shader adds.
                var constant = Mathf.Clamp01(value);
                if (mode == 1f)
                {
                    return ConstantTerm(constant);
                }

                return constant >= 1f
                    ? MainUnchanged()
                    : Refuse(
                        diagnostics,
                        LilToonSemanticDiagnosticCode.UnsupportedFeature,
                        ScaleProperty);
            }

            return MappedSampleOf(
                assignment.Texture.SourceIdentity,
                new UvMapping(0, assignment.Scale, assignment.Offset),
                scale,
                value,
                mode == 1f);
```

The mapped arm requires source identity: keep the existing `UnstableTextureIdentity` refusal naming `_AlphaMask` by moving the identity check above the new return (it currently sits inside the `(1, 0)` arm; hoist it so both sample arms share it). The scale-offset requirement moves with it: both sample shapes need `assignment.HasScaleOffset`. Delete the fall-through refusal at lines 269-275. Update the type doc block to the spec's admitted-case table and the rounding argument.

- [ ] **Step 4: Implement the transparent emission**

In `LilToonTransparentMaterialSemantics.cs`, extend the mask consumption site at lines 582-619:

```csharp
            TextureSample maskSample = null;
            AffineAlphaMap maskMap = null;
            if (maskTerm.Kind == LilToonAlphaMaskTermKind.Sample ||
                maskTerm.Kind == LilToonAlphaMaskTermKind.MappedSample)
            {
                // The mask borrows _MainTex's captured sampler facts. An
                // unassigned main has none, so a sampled or mapped mask
                // refuses by name instead of proving through borrowed facts.
                if (!hasMainSampler)
                {
                    return RecordUnknown<ScalarSemanticValue>(
                        diagnostics,
                        LilToonSemanticOutput.Alpha,
                        LilToonSemanticDiagnosticCode.UnsupportedSampling,
                        MainTextureProperty);
                }

                maskSample = new TextureSample(
                    maskTerm.Source,
                    maskTerm.Mapping,
                    assignment.Texture.Sampling);
                if (maskTerm.Kind == LilToonAlphaMaskTermKind.MappedSample)
                {
                    maskMap = AffineAlphaMap.FromBinary32(
                        maskTerm.Scale, maskTerm.Value);
                }

                if (maskTerm.ReplacesMainAlpha)
                {
                    return SemanticOutput<ScalarSemanticValue>.Complete(
                        maskMap == null
                            ? ScalarSemanticValue.Texture(
                                maskSample, TextureChannel.Red)
                            : ScalarSemanticValue.MappedTexture(
                                maskSample, TextureChannel.Red, maskMap));
                }
            }
```

and at the multiply site:

```csharp
            if (maskSample != null)
            {
                var maskFactor = maskMap == null
                    ? ScalarSemanticValue.TextureTimesConstant(
                        maskSample, TextureChannel.Red, 1f)
                    : ScalarSemanticValue.MappedTexture(
                        maskSample, TextureChannel.Red, maskMap);
                var multiplied = Multiply(
                    alphaChain, maskFactor, AlphaMaskModeProperty, diagnostics);
                if (multiplied == null)
                {
                    return SemanticOutput<ScalarSemanticValue>.Unknown();
                }

                alphaChain = multiplied;
            }
```

The Multiply/CollectFactors changes from Task 2 carry the map through.

- [ ] **Step 5: Run to green**

Filters: `LilToonTransparentAlphaTests`, then `LilToonAlphaTests`, then the Characterization folder (`NeutralClaimGatingTests`, `IrrelevantChangeInvarianceTests`, `UncertaintyMonotonicityTests`). Expected: the flipped and new tests pass; the non-finite row still refuses; everything else keeps its outcome. Record observed counts.

- [ ] **Step 6: Commit (when authorized)**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs
git commit -m "feat: prove general lilToon mask pairs in the transparent family"
```

---

### Task 6: The cutout frontend

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs` (the mask consumption site at lines 502-545)
- Test: Modify `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs`

**Interfaces:**
- Consumes: `MappedSample` (Task 5), the mapped resolution (Task 4).
- Produces: cutout materials with any finite mask pair resolve through the cutout coverage transform. Nothing else consumes these internals.

- [ ] **Step 1: Write the new tests (RED)**

The cutout family has no pinned refusal rows for this shape, so all its coverage is new. Add to `LilToonCutoutAlphaTests.cs`:

```csharp
        [Test]
        public void MappedMask_ValueAboveCutoff_ProvesCoveredTrianglesOpaque()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("c_mapped_ok");
            material.SetFloat("_AlphaMaskMode", 2f);
            material.SetFloat("_AlphaMaskScale", 1f);
            material.SetFloat("_AlphaMaskValue", 0.5f);
            material.SetFloat("_Cutoff", 0.5f);
            AssignAllWhiteMask(material, "c_mapped_ok_mask");

            var resolution = ResolveThroughCutoutFrontend(material);
            // Classified resolution: the assertion classifies a triangle.
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void MappedMask_HoleTriangles_StaysUnknownNotOpaque()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("c_mapped_hole");
            material.SetFloat("_AlphaMaskMode", 2f);
            material.SetFloat("_AlphaMaskScale", 1f);
            material.SetFloat("_AlphaMaskValue", 0.5f);
            material.SetFloat("_Cutoff", 0.5f);
            AssignPartBlackMask(material, "c_mapped_hole_mask");

            var resolution = ResolveThroughCutoutFrontend(material);
            Assert.That(
                resolution.Classify(TriangleOverBlackTexel()),
                Is.EqualTo(TriangleAlphaOutcome.Unknown));
        }

        [Test]
        public void MappedMask_NonFiniteValue_KeepsRefusing()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("c_mapped_nan");
            material.SetFloat("_AlphaMaskMode", 2f);
            material.SetFloat("_AlphaMaskScale", 1f);
            material.SetFloat("_AlphaMaskValue", float.NaN);
            AssignAllWhiteMask(material, "c_mapped_nan_mask");

            AssertAlphaGateUnknown(InterpretCutout(material), "_AlphaMaskValue");
        }
```

Use the file's existing helpers for mask grids, triangles, and the resolution path (`ResolveThroughCutoutFrontend` exists; mirror the transparent fixtures for the part-black and triangle helpers where the file lacks them).

- [ ] **Step 2: Run and watch them fail**

Filter `LilToonCutoutAlphaTests`. Expected: the first two fail (Unknown at `_AlphaMaskValue` today), the non-finite row passes. RED recorded.

- [ ] **Step 3: Implement**

Apply the identical consumption-site change as Task 5 Step 4 to `LilToonCutoutMaterialSemantics.cs` at lines 502-545: the combined `Sample`/`MappedSample` branch, the shared `hasMainSampler` refusal, `maskMap`, the replace-mode completion, and the multiply site. The cutoff comparison is the classifier's declared cutoff and stays untouched: the mask composes before the cutout transform in the source, and the classified value expresses exactly that, unchanged from the 2026-09-07 design.

- [ ] **Step 4: Run to green**

Filter `LilToonCutoutAlphaTests`, then the whole `Semantics/LilToon` folder. Expected: new tests pass, all existing cutout tests unchanged. Record observed counts.

- [ ] **Step 5: Commit (when authorized)**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs
git commit -m "feat: prove general lilToon mask pairs in the cutout family"
```

---

### Task 7: Full validation and cleanup

**Files:**
- No production changes expected. Fixtures and helpers added by earlier tasks are already committed.

- [ ] **Step 1: Run both assemblies**

Run the full `Alrauna.Amuse.Tests.Editor` assembly, then `Alrauna.Amuse.Research.Tests.Editor`, in the dev editor instance. Expected: zero failures and zero skipped-by-error. Record the observed counts; a run that reports zero tests is a failure.

- [ ] **Step 2: Schema and pin checks**

Confirm the evidence-request schema tests (`LilToonTransparentAlphaTests`, `LilToonCutoutAlphaTests` request-list pins) stayed green without edits: this design adds no capture kinds. Confirm `LilToonAttestationTests` is untouched.

- [ ] **Step 3: Whitespace and identifier sweep**

Run `git diff --check` on the branch. Sweep every changed file for: an at sign joined to a hexadecimal hash, drive-letter paths, home-directory paths, four-digit ports, and every private asset name known to the session. Fix every hit, then state that the sweep ran.

- [ ] **Step 4: Census Lab follow-up (manual, after the next Lab sync)**

After this branch's code syncs into the Lab's embedded package copy, the next play-mode build answers the two investigated slots. Record the observed outcome as a dated follow-up on `docs/superpowers/investigations/2026-09-27-alpha-mask-value-refusal-investigation.md`: the two in-scope reports should disappear, with the slots classifying per triangle. Tests never run in the Census Lab.

- [ ] **Step 5: Final commit (when authorized)**

```bash
git add -A
git commit -m "docs: record the alpha mask threshold envelope validation"
```
