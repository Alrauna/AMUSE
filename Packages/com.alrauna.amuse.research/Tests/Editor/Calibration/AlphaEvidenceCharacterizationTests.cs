using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Research.Tests.Editor.Calibration
{
    /// <summary>
    /// In-memory fixtures. Nothing is imported, so no importer setting is read
    /// or written and no scratch asset can survive a failed teardown.
    /// </summary>
    internal static class AlphaProbeFixtures
    {
        internal const byte Maximum = 255;

        /// <summary>
        /// The largest alpha strictly below maximum that an 8-bit UNorm channel
        /// can represent. It is the value Unity's CPU decode was measured to
        /// round up to 255 on compressed input, which is the whole reason the
        /// GPU route exists.
        /// </summary>
        internal const byte Submaximum = 254;

        /// <summary>
        /// Four quadrants with four distinct alphas, so a row flip, a column
        /// flip and a transpose are each detectable: bottom-left 255,
        /// bottom-right 254, top-left 0, top-right 128. Each quadrant is a
        /// whole 4x4 block at size 8, so a block compressor encodes it exactly
        /// and the fixture measures decode rather than encoder error. RGB is a
        /// uniform non-white value so a channel confusion is visible.
        /// </summary>
        internal static Texture2D Quadrants(
            TextureFormat format, int size, bool mipChain)
        {
            var texture = new Texture2D(size, size, format, mipChain, true);
            var pixels = new Color32[size * size];
            var half = size / 2;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    byte alpha = y < half
                        ? (x < half ? Maximum : Submaximum)
                        : (x < half ? (byte)0 : (byte)128);
                    pixels[y * size + x] = new Color32(64, 32, 16, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(mipChain, true);
            return texture;
        }

        /// <summary>
        /// The same layout, block-compressed. Built uncompressed, compressed in
        /// place, then stripped of its CPU copy, so the compressed cases are
        /// non-readable exactly as an ordinary imported avatar texture is.
        /// </summary>
        internal static Texture2D CompressedQuadrants(
            TextureFormat format, int size, bool mipChain)
        {
            var texture = new Texture2D(
                size, size, TextureFormat.RGBA32, mipChain, true);
            var pixels = new Color32[size * size];
            var half = size / 2;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    byte alpha = y < half
                        ? (x < half ? Maximum : Submaximum)
                        : (x < half ? (byte)0 : (byte)128);
                    pixels[y * size + x] = new Color32(64, 32, 16, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(mipChain, false);
            EditorUtility.CompressTexture(
                texture, format, TextureCompressionQuality.Best);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>
        /// The soundness fixture. Alpha is maximum for x &lt; 5 and 200
        /// otherwise, on an 8-wide texture, so the boundary is deliberately
        /// odd-aligned and does not survive halving: column 4 is exactly one at
        /// mip 0, and the mip-1 texel covering it is not.
        /// <para>
        /// The chain is always generated uncompressed and then compressed as a
        /// whole when <paramref name="format"/> asks for it, so the compressed
        /// case measures decode of a real mip chain rather than compression of
        /// a single level. The result is non-readable either way.
        /// </para>
        /// </summary>
        internal static Texture2D OddAlignedBoundary(TextureFormat format)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, true, true);
            var pixels = new Color32[64];
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    pixels[y * 8 + x] =
                        new Color32(64, 32, 16, x < 5 ? Maximum : (byte)200);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            if (format != TextureFormat.RGBA32)
            {
                EditorUtility.CompressTexture(
                    texture, format, TextureCompressionQuality.Best);
            }
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>
        /// Non-square, with a zero column at x = 0 and a single submaximum at
        /// (11, 1). A transpose moves the zero column onto a row; a flip moves
        /// the lone submaximum. Its chain also exercises independent per-axis
        /// clamping.
        /// </summary>
        internal static Texture2D NonSquare()
        {
            var texture = new Texture2D(16, 4, TextureFormat.RGBA32, true, true);
            var pixels = new Color32[64];
            for (var y = 0; y < 4; y++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var alpha = Maximum;
                    if (x == 0) alpha = 0;
                    if (x == 11 && y == 1) alpha = Submaximum;
                    pixels[y * 16 + x] = new Color32(64, 32, 16, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>
        /// A float field carrying values the AlphaFieldProvider contract
        /// forbids alongside ones it permits.
        /// </summary>
        internal static Texture2D FloatAlphas(float[] alphas)
        {
            var texture = new Texture2D(
                alphas.Length, 1, TextureFormat.RGBAHalf, false, true);
            var pixels = new Color[alphas.Length];
            for (var index = 0; index < alphas.Length; index++)
            {
                pixels[index] = new Color(0.25f, 0.125f, 0.0625f, alphas[index]);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }

    /// <summary>
    /// Characterization of the alpha evidence predicate on this machine,
    /// through the production-shaped route: one shader, one explicit mip, an
    /// R8_UNorm predicate target, one byte per texel.
    /// <para>
    /// These are hardware-dependent observations, deliberately expressed as
    /// tests so a machine or Unity version that stops reproducing them fails
    /// loudly. They characterize the platform, not AMUSE production code:
    /// nothing in <c>com.alrauna.amuse</c> is exercised, and no result here is
    /// authority for a transformation.
    /// </para>
    /// <para>
    /// Support gates are asserted, never skipped. Following
    /// <see cref="VendorReachabilityTests"/>, an unreachable characterization
    /// must fail rather than report as a pass.
    /// </para>
    /// </summary>
    public sealed class AlphaEvidenceCharacterizationTests
    {
        private Texture2D _texture;

        [TearDown]
        public void TearDown()
        {
            if (_texture != null)
            {
                UnityEngine.Object.DestroyImmediate(_texture);
                _texture = null;
            }
        }

        private AlphaProbeLevel Capture(Texture2D texture, int mip)
        {
            var level = AlphaEvidenceProbe.TryCaptureLevel(texture, mip);
            Assert.That(
                level, Is.Not.Null,
                "Mip " + mip + " could not be established through the R8 "
                + "predicate path on this machine ("
                + AlphaEvidenceProbe.ProbeSupport().Describe() + ").");
            return level;
        }

        [Test]
        public void TheProductionShapedPathIsReachableOnThisMachine()
        {
            Assert.That(
                EditorUserBuildSettings.activeBuildTarget,
                Is.EqualTo(BuildTarget.StandaloneWindows64),
                "This characterization is scoped to exactly StandaloneWindows64, "
                + "the one target whose imports were observed. Under any other "
                + "target the textures under test are not the ones the "
                + "investigation reasoned about, and its conclusions do not "
                + "carry.");

            var support = AlphaEvidenceProbe.ProbeSupport();
            Assert.That(
                support.IsUsable, Is.True,
                "The R8 predicate path is unreachable here, so every case below "
                + "would report a vacuous pass. " + support.Describe());
        }

        // ---- Format allowlist, every admitted format through the R8 path ----

        /// <summary>
        /// Every alpha-bearing format on the proposed initial allowlist. The
        /// claim that separates a GPU decode from Unity's CPU decode:
        /// GetPixels32 reports a compressed 254 as 255, fabricating opacity.
        /// Maximum alpha must satisfy the predicate and a representable
        /// submaximum must not.
        /// </summary>
        [TestCase(TextureFormat.RGBA32, false)]
        [TestCase(TextureFormat.ARGB32, false)]
        [TestCase(TextureFormat.Alpha8, false)]
        [TestCase(TextureFormat.DXT5, true)]
        [TestCase(TextureFormat.BC7, true)]
        public void AnAlphaBearingFormatSeparatesMaximumFromSubmaximum(
            TextureFormat format, bool compressed)
        {
            _texture = compressed
                ? AlphaProbeFixtures.CompressedQuadrants(format, 8, false)
                : AlphaProbeFixtures.Quadrants(format, 8, false);
            Assert.That(
                _texture.format, Is.EqualTo(format),
                "The fixture did not end up in the format under test.");
            Assert.That(
                _texture.isReadable, Is.False,
                "The whole point of this route is that it needs no CPU copy. A "
                + "readable fixture would silently turn this investigation back "
                + "into readable-texture characterization.");

            var level = Capture(_texture, 0);

            Assert.That(
                level.ExactlyOneAt(1, 1), Is.True,
                "Maximum alpha must satisfy the predicate.");
            Assert.That(
                level.ExactlyOneAt(5, 1), Is.False,
                "A representable submaximum alpha must not be reported as "
                + "exactly one. This is the case Unity's CPU decode fails.");
            Assert.That(level.ExactlyOneAt(1, 5), Is.False, "alpha 0");
            Assert.That(level.ExactlyOneAt(5, 5), Is.False, "alpha 128");
        }

        /// <summary>
        /// The one RGB-only format on the allowlist. It carries no alpha
        /// channel at all, so the sampler returns exactly one everywhere and
        /// the whole field is opaque regardless of what the fixture asked for.
        /// </summary>
        [Test]
        public void AnRgbOnlyFormatSamplesAlphaExactlyOne()
        {
            _texture = AlphaProbeFixtures.Quadrants(TextureFormat.RGB24, 8, false);
            Assert.That(_texture.format, Is.EqualTo(TextureFormat.RGB24));
            Assert.That(
                _texture.isReadable, Is.False,
                "RGB24 is characterized on the same non-readable footing as "
                + "every other admitted format.");

            var level = Capture(_texture, 0);

            Assert.That(
                level.AllExactlyOne(), Is.True,
                "RGB24 has no alpha channel, so every texel samples exactly "
                + "one even where the source asked for 0, 128 and 254.");
        }

        // ---- Soundness ----

        /// <summary>
        /// Mip levels disagree, so a mip-0 proof is not a proof. Column 4 is
        /// exactly one at mip 0; the mip-1 texel covering it is not.
        /// <para>
        /// Run against both an uncompressed and a block-compressed chain. The
        /// compressed case is the one the feature actually needs: real avatar
        /// textures are non-readable DXT5, and lower-mip selection must be
        /// observable there and not only on an uncompressed fixture.
        /// </para>
        /// </summary>
        [TestCase(TextureFormat.RGBA32)]
        [TestCase(TextureFormat.DXT5)]
        public void MipZeroAndMipOneDisagreeAboutOpacity(TextureFormat format)
        {
            _texture = AlphaProbeFixtures.OddAlignedBoundary(format);
            Assert.That(
                _texture.format, Is.EqualTo(format),
                "The fixture did not end up in the format under test.");
            Assert.That(
                _texture.isReadable, Is.False,
                "The mip-disagreement claim must be made about a non-readable "
                + "texture; a readable fixture would characterize the wrong "
                + "thing.");
            Assert.That(_texture.mipmapCount, Is.GreaterThan(1));

            var mip0 = Capture(_texture, 0);
            var mip1 = Capture(_texture, 1);

            Assert.That(
                mip0.ExactlyOneAt(4, 0), Is.True,
                "Source column 4 is exactly one at mip 0.");
            Assert.That(
                mip1.ExactlyOneAt(2, 0), Is.False,
                "The mip-1 texel covering source column 4 is below one, so "
                + "opacity proven from mip 0 alone would be unsound.");
        }

        [Test]
        public void OrientationSurvivesAnAsymmetricNonSquareChain()
        {
            _texture = AlphaProbeFixtures.NonSquare();
            var level = Capture(_texture, 0);

            Assert.That(level.Width, Is.EqualTo(16));
            Assert.That(level.Height, Is.EqualTo(4));

            // A transpose would move the zero column onto a row.
            for (var y = 0; y < 4; y++)
            {
                Assert.That(
                    level.ExactlyOneAt(0, y), Is.False,
                    "Column 0 is zero at every row.");
            }
            for (var x = 1; x < 16; x++)
            {
                if (x == 11) continue;
                Assert.That(
                    level.ExactlyOneAt(x, 0), Is.True,
                    "Row 0 is opaque away from column 0.");
            }

            // A row or column flip would move the lone submaximum.
            Assert.That(
                level.ExactlyOneAt(11, 1), Is.False,
                "The isolated submaximum sits at exactly (11, 1), "
                + "bottom-to-top.");
            Assert.That(level.ExactlyOneAt(11, 0), Is.True);
        }

        // ---- Mip residency gate, and output integrity ----

        /// <summary>
        /// The residency rule, as a pure predicate over the two facts that
        /// decide it. Every combination is covered here because the refusal
        /// branches cannot be constructed in memory: a runtime texture cannot
        /// be given a nonzero activeMipmapLimit or streaming state without
        /// mutating project or importer state, which production must never do.
        /// </summary>
        [TestCase(0, false, true)]
        [TestCase(1, false, false)]
        [TestCase(2, false, false)]
        [TestCase(0, true, false)]
        [TestCase(1, true, false)]
        public void TheMipResidencyGateAdmitsOnlyAnUnlimitedNonStreamingTexture(
            int activeMipmapLimit, bool streamingMipmaps, bool expected)
        {
            Assert.That(
                AlphaEvidenceProbe.MipResidencyGatesPass(
                    activeMipmapLimit, streamingMipmaps),
                Is.EqualTo(expected));
        }

        /// <summary>
        /// The texture overload must read the same two facts, so the pure
        /// predicate above is a faithful statement of the rule rather than a
        /// parallel one that could drift.
        /// </summary>
        [Test]
        public void TheGateOverloadAgreesWithThePredicateForARealTexture()
        {
            _texture = AlphaProbeFixtures.NonSquare();

            Assert.That(
                AlphaEvidenceProbe.MipResidencyGatesPass(_texture),
                Is.EqualTo(AlphaEvidenceProbe.MipResidencyGatesPass(
                    _texture.activeMipmapLimit, _texture.streamingMipmaps)));
            Assert.That(_texture.activeMipmapLimit, Is.EqualTo(0));
            Assert.That(_texture.streamingMipmaps, Is.False);
        }

        /// <summary>
        /// Output integrity only. This proves that each capture returned a
        /// buffer describing the destination the probe itself allocated, with
        /// the row layout that destination implies - nothing more.
        /// <para>
        /// It is <strong>not</strong> a residency test. The destination
        /// dimensions were chosen by this code, so they cannot establish that
        /// the requested source mip was resident, nor that
        /// <c>Texture2D.Load</c> did not substitute or return default data.
        /// Source residency is gated separately, on declared state.
        /// </para>
        /// </summary>
        [Test]
        public void EachCaptureMatchesTheDestinationSizeAndRowLayoutRequested()
        {
            _texture = AlphaProbeFixtures.NonSquare();

            for (var mip = 0; mip < _texture.mipmapCount; mip++)
            {
                AlphaEvidenceProbe.ExpectedSize(
                    _texture, mip, out var width, out var height);
                var level = Capture(_texture, mip);
                Assert.That(level.Width, Is.EqualTo(width), "mip " + mip);
                Assert.That(level.Height, Is.EqualTo(height), "mip " + mip);
                Assert.That(
                    level.ExactlyOne.Length, Is.EqualTo(width * height),
                    "mip " + mip);
            }
        }

        /// <summary>
        /// A level outside the chain is refused rather than answered. Measured:
        /// the shader's own Load returns zero for an out-of-range level without
        /// raising anything, so the bound must be checked explicitly.
        /// </summary>
        [Test]
        public void ALevelOutsideTheChainIsRefused()
        {
            _texture = AlphaProbeFixtures.NonSquare();

            Assert.That(
                AlphaEvidenceProbe.TryCaptureLevel(
                    _texture, _texture.mipmapCount), Is.Null);
            Assert.That(
                AlphaEvidenceProbe.TryCaptureLevel(_texture, -1), Is.Null);
        }

        // ---- Independent oracle ----

        /// <summary>
        /// Where the platform supports a direct readback - uncompressed only -
        /// the R8 predicate must agree with it at every texel and every mip. A
        /// plausible incorrect extraction (wrong mip, flipped rows, filtered
        /// sample) would fail here.
        /// <para>
        /// Only the predicate is compared. The two routes are measured to
        /// differ by one ULP on magnitudes strictly below one, because the
        /// UNorm decode is not required to round identically; exactly-one is
        /// unaffected, which is itself why only the predicate may be relied on.
        /// </para>
        /// </summary>
        [Test]
        public void TheR8PredicateAgreesWithDirectReadbackWhereSupported()
        {
            _texture = AlphaProbeFixtures.Quadrants(
                TextureFormat.RGBA32, 8, true);

            for (var mip = 0; mip < _texture.mipmapCount; mip++)
            {
                var direct = AlphaEvidenceProbe.TryDirectReadback(_texture, mip);
                Assert.That(
                    direct, Is.Not.Null,
                    "An uncompressed texture must support direct readback; "
                    + "without it this case proves nothing. mip " + mip);

                var level = Capture(_texture, mip);
                Assert.That(level.ExactlyOne.Length, Is.EqualTo(direct.Length));
                for (var index = 0; index < direct.Length; index++)
                {
                    Assert.That(
                        level.ExactlyOne[index],
                        Is.EqualTo(direct[index] == 1f),
                        "predicate disagreement at mip " + mip
                        + " texel " + index);
                }
            }
        }

        // ---- Why float formats stay refused ----

        /// <summary>
        /// The one case that legitimately needs raw magnitudes, and the only
        /// user of the diagnostic path. The exactly-one bit is correct for
        /// 0.999, but it reports the same false for 2.0, -1.0, NaN and
        /// infinity, so it cannot distinguish a legitimate below-one texel from
        /// one that violates AlphaFieldProvider's finite-and-within-[0,1]
        /// attestation. A UNorm format supplies that guarantee structurally; a
        /// float format does not, and one bit cannot recover it.
        /// </summary>
        [Test]
        public void AFloatFieldDefeatsTheExactlyOnePredicateAsAnAttestation()
        {
            var alphas = new[]
            {
                1f, 0.999f, 2f, -1f, float.NaN, float.PositiveInfinity, 0.5f, 0f
            };
            _texture = AlphaProbeFixtures.FloatAlphas(alphas);
            Assert.That(_texture.format, Is.EqualTo(TextureFormat.RGBAHalf));

            var support = AlphaEvidenceProbe.ProbeSupport();
            Assert.That(
                support.DiagnosticTargetAvailable, Is.True,
                "This case needs raw magnitudes and cannot report them here.");

            var level = Capture(_texture, 0);
            var raw = AlphaEvidenceProbe.TryCaptureRawAlphaDiagnostic(_texture, 0);
            Assert.That(raw, Is.Not.Null);

            Assert.That(level.ExactlyOneAt(0, 0), Is.True, "1.0 is exactly one");
            Assert.That(
                level.ExactlyOneAt(1, 0), Is.False,
                "0.999 must not round up to exactly one.");
            Assert.That(
                raw[1], Is.LessThan(1f),
                "0.999 must survive below one.");

            for (var index = 2; index <= 5; index++)
            {
                var alpha = raw[index];
                var valid = !float.IsNaN(alpha) && !float.IsInfinity(alpha)
                    && alpha >= 0f && alpha <= 1f;
                Assert.That(
                    valid, Is.False,
                    "Fixture texel " + index + " is meant to violate [0,1].");
                Assert.That(
                    level.ExactlyOneAt(index, 0), Is.False,
                    "A contract-violating texel reports exactly the same bit as "
                    + "an ordinary below-one texel, which is why the predicate "
                    + "cannot attest validity for float formats.");
            }
        }
    }
}
