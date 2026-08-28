using System;
using System.Collections.Generic;
using System.IO;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    /// <summary>
    /// Coverage of the one Unity implementation of <c>AlphaFieldProvider</c>. Every
    /// negative case is a refusal predicate: unprovable texture state must return
    /// false with a null field, never a field that fabricates opacity.
    /// </summary>
    public sealed class UnityAlphaFieldEvidenceTests
    {
        private const string TempFolder = "Assets/AmuseTests_AlphaField";
        private const int Size = 4;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AmuseTests_AlphaField");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        /// <summary>
        /// Bottom-left texel 128, top-right texel 254, every other texel opaque. The
        /// asymmetry is deliberate: a uniform field cannot detect a row flip or an
        /// axis swap, and <see cref="AlphaTextureData"/> short-circuits on a fully
        /// opaque field before any geometry is examined.
        /// </summary>
        private static Color32[] AsymmetricPixels()
        {
            var pixels = new Color32[Size * Size];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(64, 32, 16, 255);
            }

            pixels[0] = new Color32(64, 32, 16, 128);
            pixels[pixels.Length - 1] = new Color32(64, 32, 16, 254);
            return pixels;
        }

        private static Color32[] UniformPixels(int width, int height, byte alpha)
        {
            var pixels = new Color32[width * height];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(64, 32, 16, alpha);
            }

            return pixels;
        }

        private static Texture2D Import(
            string name,
            Color32[] pixels,
            int width,
            int height,
            Action<TextureImporter> configure = null)
        {
            var path = TempFolder + "/" + name + ".png";
            var staging = new Texture2D(width, height, TextureFormat.RGBA32, false);
            staging.SetPixels32(pixels);
            staging.Apply();
            File.WriteAllBytes(path, staging.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(staging);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            configure?.Invoke(importer);
            importer.SaveAndReimport();

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(loaded, Is.Not.Null, $"Imported texture '{path}' must load.");
            return loaded;
        }

        private static Texture2D ImportAsymmetric(
            string name,
            Action<TextureImporter> configure = null)
        {
            return Import(name, AsymmetricPixels(), Size, Size, configure);
        }

        /// <summary>
        /// Builds a texture asset directly in a chosen format, for formats the
        /// importer will not produce cleanly. The result is a real project asset with
        /// a resolvable identity, which is all the producer's contract requires.
        /// </summary>
        private static Texture2D CreateTextureAsset(string name, TextureFormat format)
        {
            var texture = new Texture2D(Size, Size, format, false);
            texture.SetPixels32(AsymmetricPixels());
            texture.Apply();
            AssetDatabase.CreateAsset(texture, TempFolder + "/" + name + ".asset");

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(TempFolder + "/" + name + ".asset");
            Assert.That(loaded, Is.Not.Null, $"Texture asset '{name}' must load.");
            return loaded;
        }

        /// <summary>
        /// Requests a specific <see cref="TextureFormat"/> through a real platform
        /// override. The "DefaultTexturePlatform" pseudo-entry rejects several valid
        /// formats with a console error, so the active standalone platform is used.
        /// </summary>
        private static Action<TextureImporter> Format(TextureImporterFormat format)
        {
            return importer =>
            {
                var settings = importer.GetPlatformTextureSettings("Standalone");
                settings.overridden = true;
                settings.format = format;
                importer.SetPlatformTextureSettings(settings);
            };
        }

        /// <summary>
        /// Level 0 of the captured chain, for the cases that are only about mip 0.
        /// Those cases deliberately unwrap here; full-chain cases use
        /// <see cref="TryChain(Texture, out AlphaMipChain)"/> instead.
        /// <para>
        /// Unwrapping preserves each case's <em>subject</em>, not its literal
        /// expectations: the GPU route stores the predicate rather than the alpha
        /// magnitude, so assertions that formerly read 128 or 254 were correctly
        /// changed to assert 0, against a 255 anchor.
        /// </para>
        /// </summary>
        private static bool TryField(Texture texture, out AlphaTextureData field)
        {
            return TryField(texture, TextureChannel.Alpha, out field);
        }

        private static bool TryField(
            Texture texture,
            TextureChannel channel,
            out AlphaTextureData field)
        {
            field = null;
            if (!TryChain(texture, channel, out var chain))
            {
                return false;
            }

            field = chain[0];
            return true;
        }

        private static bool TryChain(Texture texture, out AlphaMipChain chain)
        {
            return TryChain(texture, TextureChannel.Alpha, out chain);
        }

        private static bool TryChain(
            Texture texture,
            TextureChannel channel,
            out AlphaMipChain chain)
        {
            var evidence = new UnityAlphaFieldEvidence(new[] { texture });
            Assert.That(
                UnityTextureEvidence.TryGetSourceId(texture, out var sourceId),
                Is.True,
                "The fixture texture must have a resolvable source identity.");
            return evidence.TryGetAlphaField(sourceId, channel, out chain);
        }

        // --- Positive evidence -------------------------------------------------

        [Test]
        public void SupportedImport_ReportsImportedDimensions()
        {
            var texture = ImportAsymmetric("dimensions");

            Assert.That(TryField(texture, out var field), Is.True);
            Assert.That(field.Width, Is.EqualTo(texture.width));
            Assert.That(field.Height, Is.EqualTo(texture.height));
            Assert.That(field.Width, Is.EqualTo(Size));
            Assert.That(field.Height, Is.EqualTo(Size));
        }

        /// <summary>
        /// The highest-risk defect in this producer. The R8 readback is row-major
        /// bottom-to-top and so is <see cref="AlphaTextureData"/>, so a correct
        /// implementation copies straight across. A flip or transpose would still
        /// pass a uniform fixture.
        /// <para>
        /// The stored bytes are the <em>predicate</em>, not the alpha magnitude:
        /// sampled alpha exactly one stores 255, and every finite value below one
        /// stores 0. The fixture's 128 and 254 texels are both strictly below one,
        /// so both read 0 while their opaque neighbours read 255 - which is what
        /// makes the asymmetry detectable at all.
        /// </para>
        /// </summary>
        [Test]
        public void SupportedImport_PreservesBottomToTopRowOrder()
        {
            var texture = ImportAsymmetric("roworder");

            Assert.That(TryField(texture, out var field), Is.True);
            Assert.That(
                field.GetAlpha(0, 0),
                Is.EqualTo(0),
                "Texel (0,0) is the bottom-left texel; its alpha 128 is below one.");
            Assert.That(
                field.GetAlpha(Size - 1, Size - 1),
                Is.EqualTo(0),
                "Texel (3,3) is the top-right texel; its alpha 254 is below one.");
            Assert.That(
                field.GetAlpha(1, 1),
                Is.EqualTo(255),
                "An interior opaque texel must be exactly 255, so the two zeroes "
                + "above are a real asymmetry and not a blank field.");
        }

        [Test]
        public void SupportedImport_MarksEveryOtherTexelExactlyOpaque()
        {
            var texture = ImportAsymmetric("exactbytes");

            Assert.That(TryField(texture, out var field), Is.True);
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    if ((x == 0 && y == 0) || (x == Size - 1 && y == Size - 1))
                    {
                        continue;
                    }

                    Assert.That(
                        field.GetAlpha(x, y),
                        Is.EqualTo(255),
                        $"Texel ({x},{y}) is opaque in the fixture.");
                }
            }

            Assert.That(field.IsFullyOpaque, Is.False);
            Assert.That(field.IsFullyNonOpaque, Is.False);
        }

        // --- Format policy -----------------------------------------------------
        //
        // The formats still refused below are refused by the closed allowlist,
        // which is checked before any GPU call. The formats now admitted were
        // refused historically because Unity's CPU decoder reported 255 for a texel
        // whose source alpha was strictly below one; the GPU route does not.

        /// <summary>
        /// The ordinary avatar import: non-readable and block-compressed. Both were
        /// refusals before this milestone.
        /// <para>
        /// The fixture is 8x8 rather than 4x4 because a 4x4 block-compressed texture
        /// is a single compression block, in which the encoder legitimately snaps
        /// 254 to 255 - the imported field really is opaque there, and the predicate
        /// correctly reports it.
        /// </para>
        /// </summary>
        [Test]
        public void DefaultImport_IsNotReadableAndCompressed_AndIsStillCaptured()
        {
            var texture = Import("default", QuadrantPixels(), 8, 8, importer =>
            {
                importer.isReadable = false;
                importer.textureCompression = TextureImporterCompression.Compressed;
            });

            Assert.That(texture.isReadable, Is.False, "The Unity default is non-readable.");
            Assert.That(
                TryField(texture, out var field), Is.True,
                "isReadable governs the CPU copy; GPU readback reads the GPU "
                + "resource, so a non-readable texture is now captured.");
            Assert.That(field.GetAlpha(1, 1), Is.EqualTo(255), "alpha 255");
            Assert.That(field.GetAlpha(5, 1), Is.EqualTo(0), "alpha 254");
        }

        /// <summary>
        /// 8x8 quadrants: alpha 255 for x &lt; 4, 254 otherwise. Block-compressed
        /// formats encode 4x4 blocks, so a 4x4 fixture is a single block in which
        /// the encoder legitimately snaps 254 to 255 - the imported field really is
        /// opaque there. Separating maximum from submaximum therefore needs the
        /// submaximum in a <em>different</em> block, which is the arrangement the
        /// merged research characterization uses.
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
        /// Asserts a block-compressed format is admitted and still separates a
        /// source alpha of 254 from 255. Through the GPU the decode is exact; it was
        /// Unity's CPU decoder, not the format, that fabricated opacity.
        /// </summary>
        private static void AssertAdmittedCompressedFormat(
            Texture2D texture, TextureFormat expected)
        {
            Assert.That(
                texture.format, Is.EqualTo(expected),
                "The fixture must import in the format under test.");

            Assert.That(TryField(texture, out var field), Is.True);
            Assert.That(
                field.GetAlpha(1, 1), Is.EqualTo(255),
                "Maximum alpha must satisfy the predicate exactly.");
            Assert.That(
                field.GetAlpha(5, 1), Is.EqualTo(0),
                "A representable submaximum in a different compression block must "
                + "read exactly 0. This is the case Unity's CPU decode fails.");
        }

        /// <summary>
        /// Asserts the refusal comes from the format specifically: the fixture really
        /// imported in the format under test, and it is readable, so neither a failed
        /// override nor a readability gate can make the test pass for another reason.
        /// </summary>
        private static void AssertRefusedForFormat(Texture2D texture, TextureFormat expected)
        {
            Assert.That(
                texture.format,
                Is.EqualTo(expected),
                "The fixture must import in the format under test.");
            Assert.That(
                texture.isReadable,
                Is.True,
                "The refusal must come from the format, not from readability.");
            Assert.That(texture.mipmapCount, Is.EqualTo(1));

            Assert.That(TryField(texture, out var field), Is.False);
            Assert.That(field, Is.Null);
        }

        // Through the GPU, DXT5 decodes a source alpha of 254 exactly.
        [Test]
        public void Dxt5_IsAdmittedAndSeparatesMaximumFromSubmaximum()
        {
            var texture = Import(
                "dxt5", QuadrantPixels(), 8, 8, Format(TextureImporterFormat.DXT5));

            AssertAdmittedCompressedFormat(texture, TextureFormat.DXT5);
        }

        // BC7 decompression is specified bit-accurate.
        [Test]
        public void Bc7_IsAdmittedAndSeparatesMaximumFromSubmaximum()
        {
            var texture = Import(
                "bc7", QuadrantPixels(), 8, 8, Format(TextureImporterFormat.BC7));

            AssertAdmittedCompressedFormat(texture, TextureFormat.BC7);
        }

        // Measured: DXT5Crunched turns a source alpha of 254 into 255.
        [Test]
        public void ReadableCrunchedDxt5_Refuses()
        {
            var texture = ImportAsymmetric("crunch", Format(TextureImporterFormat.DXT5Crunched));

            AssertRefusedForFormat(texture, TextureFormat.DXT5Crunched);
        }

        // Measured: GetPixels32 rounds a half-precision alpha of 0.999 up to 255.
        [Test]
        public void ReadableRgbaHalf_Refuses()
        {
            var texture = ImportAsymmetric("half", Format(TextureImporterFormat.RGBAHalf));

            AssertRefusedForFormat(texture, TextureFormat.RGBAHalf);
        }

        // Four-bit alpha expands to 255 from 15; the predicate probably survives, but
        // the expansion rule was never verified, so it is not admitted.
        [Test]
        public void ReadableArgb4444_Refuses()
        {
            var texture = ImportAsymmetric("argb16", Format(TextureImporterFormat.ARGB16));

            AssertRefusedForFormat(texture, TextureFormat.ARGB4444);
        }

        /// <summary>
        /// The two remaining admitted formats, so the allow-list is covered by test
        /// and not only by the positive RGBA32 path. Maximum alpha must be exactly
        /// 255 and a representable submaximum exactly 0.
        /// </summary>
        [Test]
        public void ReadableAlpha8_IsAdmittedAndSeparatesMaximumFromSubmaximum()
        {
            var texture = ImportAsymmetric("alpha8", Format(TextureImporterFormat.Alpha8));

            Assert.That(texture.format, Is.EqualTo(TextureFormat.Alpha8));
            Assert.That(TryField(texture, out var field), Is.True);
            Assert.That(field.GetAlpha(1, 1), Is.EqualTo(255), "alpha 255");
            Assert.That(field.GetAlpha(0, 0), Is.EqualTo(0), "alpha 128");
            Assert.That(field.GetAlpha(Size - 1, Size - 1), Is.EqualTo(0), "alpha 254");
        }

        /// <summary>
        /// ARGB32 is created as a texture asset rather than requested from the
        /// importer, which rejects it for the Default texture type with a console
        /// error and then produces it anyway. The producer opens no importer, so how
        /// the asset came to be in this format is outside its contract: what it must
        /// handle is an ARGB32 <see cref="Texture2D"/> with a project identity, which
        /// is exactly what this builds. It also confirms that
        /// the shader samples alpha from ARGB32's channel order correctly, since
        /// ARGB32's memory order differs from RGBA32's.
        /// </summary>
        [Test]
        public void ReadableArgb32_IsAdmittedAndSeparatesMaximumFromSubmaximum()
        {
            var texture = CreateTextureAsset("argb32", TextureFormat.ARGB32);

            Assert.That(texture.format, Is.EqualTo(TextureFormat.ARGB32));
            Assert.That(texture.mipmapCount, Is.EqualTo(1));
            Assert.That(TryField(texture, out var field), Is.True);
            Assert.That(field.GetAlpha(1, 1), Is.EqualTo(255), "alpha 255");
            Assert.That(field.GetAlpha(0, 0), Is.EqualTo(0), "alpha 128");
            Assert.That(field.GetAlpha(Size - 1, Size - 1), Is.EqualTo(0), "alpha 254");
        }

        [Test]
        public void ReadableRgb24_IsAdmittedAndFullyOpaque()
        {
            var texture = ImportAsymmetric("rgb24", Format(TextureImporterFormat.RGB24));

            Assert.That(texture.format, Is.EqualTo(TextureFormat.RGB24));
            Assert.That(TryField(texture, out var field), Is.True);
            Assert.That(
                field.IsFullyOpaque,
                Is.True,
                "With no alpha channel the sampler returns exactly one everywhere.");
        }

        // --- Structural and identity refusals ----------------------------------

        [Test]
        public void MipmappedTexture_IsCapturedAsAFullChain()
        {
            var texture = ImportAsymmetric("mips", importer => importer.mipmapEnabled = true);

            Assert.That(texture.mipmapCount, Is.GreaterThan(1));
            Assert.That(TryChain(texture, out var chain), Is.True);
            Assert.That(
                chain.Count, Is.EqualTo(texture.mipmapCount),
                "Every declared mip must be captured, or none.");
            Assert.That(chain[0].GetAlpha(0, 0), Is.EqualTo(0), "alpha 128");
            Assert.That(chain[0].GetAlpha(1, 1), Is.EqualTo(255), "alpha 255");
        }

        [Test]
        public void RenderTextureAsset_Refuses()
        {
            var renderTexture = new RenderTexture(Size, Size, 0);
            AssetDatabase.CreateAsset(renderTexture, TempFolder + "/rendertexture.renderTexture");

            Assert.That(
                UnityTextureEvidence.TryGetSourceId(renderTexture, out var source),
                Is.True,
                "A RenderTexture asset still has a project identity; the refusal must "
                + "come from it not being a Texture2D.");

            var evidence = new UnityAlphaFieldEvidence(new Texture[] { renderTexture });

            Assert.That(
                evidence.TryGetAlphaField(source, TextureChannel.Alpha, out var field),
                Is.False);
            Assert.That(field, Is.Null);
        }

        [Test]
        public void SourceNotSuppliedToTheProducer_Refuses()
        {
            var supplied = ImportAsymmetric("supplied");
            var absent = ImportAsymmetric("absent");

            Assert.That(UnityTextureEvidence.TryGetSourceId(absent, out var absentSource), Is.True);
            var evidence = new UnityAlphaFieldEvidence(new Texture[] { supplied });

            Assert.That(
                evidence.TryGetAlphaField(absentSource, TextureChannel.Alpha, out var field),
                Is.False,
                "Identity is never guessed; an unsupplied texture is not resolved.");
            Assert.That(field, Is.Null);
        }

        [Test]
        public void ColourChannels_Refuse()
        {
            var texture = ImportAsymmetric("channels");
            var channels = new[]
            {
                TextureChannel.Red,
                TextureChannel.Green,
                TextureChannel.Blue,
            };

            foreach (var channel in channels)
            {
                Assert.That(TryField(texture, channel, out var field), Is.False, channel.ToString());
                Assert.That(field, Is.Null, channel.ToString());
            }
        }

        [Test]
        public void NullElementInTheSuppliedSet_IsSkippedNotThrown()
        {
            var texture = ImportAsymmetric("withnull");
            Assert.That(UnityTextureEvidence.TryGetSourceId(texture, out var source), Is.True);

            var evidence = new UnityAlphaFieldEvidence(new Texture[] { null, texture, null });

            Assert.That(
                evidence.TryGetAlphaField(source, TextureChannel.Alpha, out var field),
                Is.True,
                "An unassigned material slot is an ordinary input, not a caller error.");
            Assert.That(field, Is.Not.Null);
        }

        [Test]
        public void SameTextureSuppliedTwice_ResolvesWithoutThrowing()
        {
            var texture = ImportAsymmetric("duplicate");
            Assert.That(UnityTextureEvidence.TryGetSourceId(texture, out var source), Is.True);

            var evidence = new UnityAlphaFieldEvidence(new Texture[] { texture, texture });

            Assert.That(
                evidence.TryGetAlphaField(source, TextureChannel.Alpha, out var field),
                Is.True);
            Assert.That(field, Is.Not.Null);
        }

        [Test]
        public void TextureDestroyedAfterConstruction_DoesNotChangeCapturedField()
        {
            var texture = ImportAsymmetric("destroyed-after-capture");
            Assert.That(UnityTextureEvidence.TryGetSourceId(texture, out var source), Is.True);
            var evidence = new UnityAlphaFieldEvidence(new Texture[] { texture });
            Assert.That(evidence.TryGetAlphaField(source, TextureChannel.Alpha, out var before), Is.True);

            AssetDatabase.DeleteAsset(TempFolder + "/destroyed-after-capture.png");

            Assert.That(texture == null, Is.True, "Unity's overloaded equality sees the destruction.");
            Assert.That(
                ReferenceEquals(texture, null),
                Is.False,
                "ReferenceEquals does not, which is why the producer must not use it.");

            Assert.That(
                evidence.TryGetAlphaField(source, TextureChannel.Alpha, out var after),
                Is.True);
            AssertSameField(before[0], after[0], "Captured alpha must not re-read Texture2D.");
        }

        [Test]
        public void TexturePixelsMutatedAfterConstruction_DoNotChangeCapturedField()
        {
            var texture = ImportAsymmetric("mutated-after-capture");
            Assert.That(UnityTextureEvidence.TryGetSourceId(texture, out var source), Is.True);
            var evidence = new UnityAlphaFieldEvidence(new Texture[] { texture });
            Assert.That(evidence.TryGetAlphaField(source, TextureChannel.Alpha, out var before), Is.True);

            texture.SetPixels32(UniformPixels(texture.width, texture.height, 17));
            texture.Apply();

            Assert.That(
                evidence.TryGetAlphaField(source, TextureChannel.Alpha, out var after),
                Is.True);
            AssertSameField(before[0], after[0], "Captured alpha must not re-read mutated Texture2D.");
        }

        // --- Malformed input ---------------------------------------------------

        [Test]
        public void NullTextureCollection_Throws()
        {
            Assert.That(
                () => new UnityAlphaFieldEvidence(null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void UninitializedSourceIdentity_Throws()
        {
            var evidence = new UnityAlphaFieldEvidence(Array.Empty<Texture>());

            Assert.That(
                () => evidence.TryGetAlphaField(default, TextureChannel.Alpha, out _),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void UndefinedChannel_Throws()
        {
            var texture = ImportAsymmetric("badchannel");
            Assert.That(UnityTextureEvidence.TryGetSourceId(texture, out var source), Is.True);
            var evidence = new UnityAlphaFieldEvidence(new Texture[] { texture });

            Assert.That(
                () => evidence.TryGetAlphaField(source, (TextureChannel)99, out _),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        // --- Importer-setting characterization ---------------------------------
        //
        // Class 2 settings are predicate-invariant. Class 1 settings change the
        // imported field, and the producer must report the changed field without
        // inspecting the setting.

        [Test]
        public void AlphaIsTransparency_LeavesTheFieldUnchanged()
        {
            var baseline = ImportAsymmetric("ait_off");
            Assert.That(TryField(baseline, out var expected), Is.True);

            var texture = ImportAsymmetric("ait_on", importer => importer.alphaIsTransparency = true);
            Assert.That(TryField(texture, out var actual), Is.True);

            AssertSameField(expected, actual, "alphaIsTransparency only dilates RGB");
        }

        [Test]
        public void SrgbFlag_LeavesTheFieldUnchanged()
        {
            var baseline = ImportAsymmetric("srgb_on");
            Assert.That(TryField(baseline, out var expected), Is.True);

            var texture = ImportAsymmetric("srgb_off", importer => importer.sRGBTexture = false);
            Assert.That(TryField(texture, out var actual), Is.True);

            AssertSameField(expected, actual, "alpha is never sRGB-encoded");
        }

        [Test]
        public void AlphaSourceFromInput_ReportsTheInputAlpha()
        {
            var texture = ImportAsymmetric("as_input", importer =>
                importer.alphaSource = TextureImporterAlphaSource.FromInput);

            Assert.That(TryField(texture, out var field), Is.True);
            Assert.That(field.GetAlpha(1, 1), Is.EqualTo(255), "alpha 255");
            Assert.That(field.GetAlpha(0, 0), Is.EqualTo(0), "alpha 128 is below one");
            Assert.That(
                field.GetAlpha(Size - 1, Size - 1), Is.EqualTo(0),
                "alpha 254 is below one");
        }

        /// <summary>
        /// alphaSource is a setting that <em>changes</em> the imported field, so the
        /// obligation is that the producer follows the import rather than the source
        /// file. The fixture's RGB is uniform, so a generated alpha is uniform too —
        /// which the input-alpha result provably is not.
        /// </summary>
        [Test]
        public void AlphaSourceFromGrayScale_ReportsTheGeneratedAlphaNotTheInputAlpha()
        {
            var texture = ImportAsymmetric("as_gray", importer =>
                importer.alphaSource = TextureImporterAlphaSource.FromGrayScale);

            Assert.That(TryField(texture, out var field), Is.True);
            Assert.That(
                field.GetAlpha(0, 0),
                Is.EqualTo(field.GetAlpha(Size - 1, Size - 1)),
                "Uniform RGB yields a uniform generated alpha, so the corner that "
                + "carried source alpha 128 and the corner that carried 254 now "
                + "agree - which the input-alpha field provably does not.");
        }

        [Test]
        public void AlphaSourceNone_ReportsAFullyOpaqueField()
        {
            var texture = ImportAsymmetric("as_none", importer =>
                importer.alphaSource = TextureImporterAlphaSource.None);

            Assert.That(TryField(texture, out var field), Is.True);
            Assert.That(
                field.IsFullyOpaque,
                Is.True,
                "With no imported alpha the sampler returns exactly one everywhere.");
        }

        [Test]
        public void MaxTextureSize_ReportsTheResizedDimensions()
        {
            var texture = ImportAsymmetric("resize", importer => importer.maxTextureSize = 2);

            Assert.That(TryField(texture, out var field), Is.True);
            Assert.That(field.Width, Is.EqualTo(2));
            Assert.That(field.Height, Is.EqualTo(2));
            Assert.That(field.Width, Is.EqualTo(texture.width));
        }

        [Test]
        public void NonPowerOfTwoWithoutScaling_PreservesOddDimensions()
        {
            var texture = Import(
                "npot",
                UniformPixels(3, 3, 200),
                3,
                3,
                importer => importer.npotScale = TextureImporterNPOTScale.None);

            Assert.That(TryField(texture, out var field), Is.True);
            Assert.That(field.Width, Is.EqualTo(3));
            Assert.That(field.Height, Is.EqualTo(3));
        }

        // --- Determinism and immutability --------------------------------------

        [Test]
        public void RepeatedCalls_ReturnEqualContents()
        {
            var texture = ImportAsymmetric("determinism");
            Assert.That(UnityTextureEvidence.TryGetSourceId(texture, out var source), Is.True);
            var evidence = new UnityAlphaFieldEvidence(new Texture[] { texture });

            Assert.That(evidence.TryGetAlphaField(source, TextureChannel.Alpha, out var first), Is.True);
            Assert.That(evidence.TryGetAlphaField(source, TextureChannel.Alpha, out var second), Is.True);

            AssertSameField(first[0], second[0], "the producer is deterministic");
        }

        [Test]
        public void SeparateCalls_ReturnTheCapturedImmutableField()
        {
            var texture = ImportAsymmetric("independence");
            Assert.That(UnityTextureEvidence.TryGetSourceId(texture, out var source), Is.True);
            var evidence = new UnityAlphaFieldEvidence(new Texture[] { texture });

            Assert.That(evidence.TryGetAlphaField(source, TextureChannel.Alpha, out var first), Is.True);
            Assert.That(evidence.TryGetAlphaField(source, TextureChannel.Alpha, out var second), Is.True);

            Assert.That(
                ReferenceEquals(first, second),
                Is.True,
                "The immutable captured field is safe to share between lookups.");
            AssertSameField(first[0], second[0], "captured fields still agree");
        }


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

        /// <summary>
        /// The source-sampling gate. Every admitted alpha-bearing format must have
        /// exact reported-format Sample support; RGB24 alone is exempt.
        /// <para>
        /// Measured on this host: SystemInfo reports Sample support False for
        /// R8G8B8_UNorm, which is RGB24's reported graphicsFormat, yet the
        /// production shader route samples RGB24 with alpha exactly one at 4x4 and
        /// 8x8, single-mip and mipmapped. Unity 2022.3 converts RGB24 to RGBA32 at
        /// texture load because native RGB24 support is rare, so the reported
        /// storage format is not the sampled one.
        /// </para>
        /// <para>
        /// The exemption is deliberately format-specific rather than a general
        /// GetCompatibleFormat fallback: a compatible format promises a supported
        /// similar format, not the exact alpha preservation AMUSE needs from an
        /// uncharacterized alpha-bearing substitution.
        /// </para>
        /// </summary>
        [TestCase(TextureFormat.RGBA32, true, true)]
        [TestCase(TextureFormat.RGBA32, false, false)]
        [TestCase(TextureFormat.ARGB32, true, true)]
        [TestCase(TextureFormat.ARGB32, false, false)]
        [TestCase(TextureFormat.Alpha8, true, true)]
        [TestCase(TextureFormat.Alpha8, false, false)]
        [TestCase(TextureFormat.DXT5, true, true)]
        [TestCase(TextureFormat.DXT5, false, false)]
        [TestCase(TextureFormat.BC7, true, true)]
        [TestCase(TextureFormat.BC7, false, false)]
        [TestCase(TextureFormat.RGB24, true, true)]
        [TestCase(TextureFormat.RGB24, false, true)]
        public void OnlyRgb24IsExemptFromExactSourceFormatSampleSupport(
            TextureFormat format, bool exactSampleable, bool passes)
        {
            Assert.That(
                UnityAlphaFieldEvidence.SourceSamplingGatePasses(
                    format, exactSampleable),
                Is.EqualTo(passes));
        }

        /// <summary>
        /// The exemption must not become an admission route. A refused format that
        /// happens to be exactly sampleable still fails the closed allowlist, which
        /// is an independent gate evaluated before this one.
        /// </summary>
        [Test]
        public void TheSourceSamplingGateNeverAdmitsARefusedFormat()
        {
            foreach (var refused in new[]
            {
                TextureFormat.DXT5Crunched,
                TextureFormat.ARGB4444,
                TextureFormat.RGBAHalf,
                TextureFormat.BC6H
            })
            {
                Assert.That(
                    UnityAlphaFieldEvidence.IsAdmittedFormat(refused), Is.False,
                    refused + " must stay outside the allowlist.");
            }

            // Even where the sampling gate answers true, the allowlist has already
            // refused these formats, so no combination admits them.
            Assert.That(
                UnityAlphaFieldEvidence.SourceSamplingGatePasses(
                    TextureFormat.ARGB4444, true),
                Is.True,
                "The sampling gate answers only the sampling question.");
            Assert.That(
                UnityAlphaFieldEvidence.IsAdmittedFormat(TextureFormat.ARGB4444),
                Is.False,
                "The closed allowlist is what refuses it, independently.");
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
        /// reachable in production. The product is computed in long so the
        /// comparison stays correct for the largest textures Unity imports.
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


        // --- GPU acquisition: real Unity integration --------------------------

        /// <summary>
        /// Imports a mipmapped, non-readable fixture in a requested format. This is
        /// the shape of a real avatar texture, and every one of these properties was
        /// a refusal before this milestone. A newly created synthetic asset in this
        /// suite's own TempFolder, which TearDown deletes regardless of outcome.
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
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
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
                    pixels[y * 8 + x] =
                        new Color32(64, 32, 16, x < 5 ? (byte)255 : (byte)200);
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
        /// Every alpha-bearing admitted format that a Standalone importer override
        /// can produce, mipmapped and non-readable, through the real R8 predicate
        /// route. The 8x8 quadrant fixture puts the submaximum in a different
        /// compression block so the block-compressed cases are meaningful.
        /// <para>
        /// ARGB32 is absent deliberately: the importer rejects it for the Default
        /// texture type on Standalone with a console error. It is covered by
        /// <see cref="ReadableArgb32_IsAdmittedAndSeparatesMaximumFromSubmaximum"/>,
        /// which builds the asset directly, and by the exhaustive allowlist
        /// predicate.
        /// </para>
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
        /// exemption is sound in practice and not merely permitted by a predicate:
        /// every returned byte must be exactly 255 at every level.
        /// </summary>
        [Test]
        public void AnRgbOnlyFormatSamplesAlphaExactlyOneAtEveryLevel()
        {
            var texture = ImportMipmapped(
                "rgb24_mipped", QuadrantPixels(), 8, 8,
                TextureImporterFormat.RGB24);

            Assert.That(texture.format, Is.EqualTo(TextureFormat.RGB24));
            Assert.That(
                SystemInfo.IsFormatSupported(
                    texture.graphicsFormat, FormatUsage.Sample),
                Is.False,
                "This host reports no exact Sample support for RGB24's graphics "
                + "format; the exemption is what admits it.");

            Assert.That(TryChain(texture, out var chain), Is.True);
            Assert.That(chain.Count, Is.EqualTo(texture.mipmapCount));
            for (var level = 0; level < chain.Count; level++)
            {
                var grid = chain[level];
                Assert.That(grid.IsFullyOpaque, Is.True, "level " + level);
                for (var y = 0; y < grid.Height; y++)
                {
                    for (var x = 0; x < grid.Width; x++)
                    {
                        Assert.That(
                            grid.GetAlpha(x, y), Is.EqualTo(255),
                            $"level {level} texel ({x},{y}) must be exactly 255.");
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

        /// <summary>
        /// Gate 12. A cached true can only have been written by a real passing run
        /// earlier in this AppDomain, so this claim is sound whether or not this
        /// call is the one that executed the check.
        /// </summary>
        [Test]
        public void TheHostCapabilityCheckPassesOnThisHost()
        {
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

        // --- Architecture boundary ---------------------------------------------



        private static string PackageDirectory(string relative)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, "Packages", "com.alrauna.amuse", relative);
        }

        /// <summary>
        /// Matches the <c>UnityEditor</c> identifier rather than a using directive,
        /// so a fully-qualified reference or a namespace alias is caught too.
        /// </summary>
        private static int CountFilesDependingOnUnityEditor(string directory, out int fileCount)
        {
            var files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
            fileCount = files.Length;

            var pattern = new System.Text.RegularExpressions.Regex(@"\bUnityEditor\b");
            var hits = 0;
            foreach (var file in files)
            {
                if (pattern.IsMatch(File.ReadAllText(file)))
                {
                    hits++;
                }
            }

            return hits;
        }

        /// <summary>
        /// The proof core must stay host-neutral. This is the placement boundary that
        /// forced the producer into its own namespace, and a boundary nobody can
        /// verify is a boundary that erodes.
        /// </summary>
        [Test]
        public void AnalysisNamespace_HasNoDependencyOnTheUnityEditorNamespace()
        {
            var directory = PackageDirectory(Path.Combine("Editor", "Analysis"));
            Assert.That(Directory.Exists(directory), Is.True, directory);

            var hits = CountFilesDependingOnUnityEditor(directory, out var fileCount);

            Assert.That(fileCount, Is.GreaterThan(0), "The guard must not pass vacuously.");
            Assert.That(hits, Is.Zero, "Editor/Analysis must not depend on UnityEditor.");
        }

        /// <summary>
        /// Permanent negative control: the same detector, pointed at a namespace that
        /// genuinely uses <c>UnityEditor</c>, must report it. Without this the guard
        /// above could pass because the detector is broken.
        /// </summary>
        [Test]
        public void UnityEditorDetector_ReportsADirectoryThatDoesDependOnIt()
        {
            var directory = PackageDirectory(Path.Combine("Editor", "Semantics"));
            Assert.That(Directory.Exists(directory), Is.True, directory);

            var hits = CountFilesDependingOnUnityEditor(directory, out var fileCount);

            Assert.That(fileCount, Is.GreaterThan(0));
            Assert.That(hits, Is.GreaterThan(0), "Editor/Semantics is known to use UnityEditor.");
        }

        private static void AssertSameField(
            AlphaTextureData expected,
            AlphaTextureData actual,
            string because)
        {
            Assert.That(actual.Width, Is.EqualTo(expected.Width), because);
            Assert.That(actual.Height, Is.EqualTo(expected.Height), because);
            for (var y = 0; y < expected.Height; y++)
            {
                for (var x = 0; x < expected.Width; x++)
                {
                    Assert.That(
                        actual.GetAlpha(x, y),
                        Is.EqualTo(expected.GetAlpha(x, y)),
                        $"{because} — texel ({x},{y})");
                }
            }
        }
    }
}
