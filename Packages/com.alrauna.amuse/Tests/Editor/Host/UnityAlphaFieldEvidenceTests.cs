using System;
using System.IO;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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

        private static bool TryField(Texture texture, out AlphaTextureData field)
        {
            return TryField(texture, TextureChannel.Alpha, out field);
        }

        private static bool TryField(
            Texture texture,
            TextureChannel channel,
            out AlphaTextureData field)
        {
            var evidence = new UnityAlphaFieldEvidence(new[] { texture });
            Assert.That(
                UnityTextureEvidence.TryGetSourceId(texture, out var sourceId),
                Is.True,
                "The fixture texture must have a resolvable source identity.");
            return evidence.TryGetAlphaField(sourceId, channel, out field);
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
        /// The highest-risk defect in this producer. Unity's
        /// <c>GetPixels32</c> is row-major bottom-to-top and so is
        /// <see cref="AlphaTextureData"/>, so a correct implementation copies
        /// straight across. A flip or transpose would still pass a uniform fixture.
        /// </summary>
        [Test]
        public void SupportedImport_PreservesBottomToTopRowOrder()
        {
            var texture = ImportAsymmetric("roworder");

            Assert.That(TryField(texture, out var field), Is.True);
            Assert.That(
                field.GetAlpha(0, 0),
                Is.EqualTo(128),
                "Texel (0,0) is the bottom-left texel and carries alpha 128.");
            Assert.That(
                field.GetAlpha(Size - 1, Size - 1),
                Is.EqualTo(254),
                "Texel (3,3) is the top-right texel and carries alpha 254.");
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

        // --- Format refusals ---------------------------------------------------
        //
        // Every case below is a measured false opaque: the CPU view reports 255 for
        // a texel whose source alpha was strictly below one. Admitting any of them
        // would let the classifier prove opacity that the shader does not produce.

        [Test]
        public void DefaultImport_IsNotReadable_AndRefuses()
        {
            var texture = ImportAsymmetric("default", importer =>
            {
                importer.isReadable = false;
                importer.textureCompression = TextureImporterCompression.Compressed;
            });

            Assert.That(texture.isReadable, Is.False, "The Unity default is non-readable.");
            Assert.That(TryField(texture, out var field), Is.False);
            Assert.That(field, Is.Null);
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

        // Measured: DXT5 turns a source alpha of 254 into 255.
        [Test]
        public void ReadableDxt5_Refuses()
        {
            var texture = ImportAsymmetric("dxt5", Format(TextureImporterFormat.DXT5));

            AssertRefusedForFormat(texture, TextureFormat.DXT5);
        }

        // Measured: BC7 turns a source alpha of 254 into 255.
        [Test]
        public void ReadableBc7_Refuses()
        {
            var texture = ImportAsymmetric("bc7", Format(TextureImporterFormat.BC7));

            AssertRefusedForFormat(texture, TextureFormat.BC7);
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
        /// and not only by the positive RGBA32 path.
        /// </summary>
        [Test]
        public void ReadableAlpha8_IsAdmittedAndExact()
        {
            var texture = ImportAsymmetric("alpha8", Format(TextureImporterFormat.Alpha8));

            Assert.That(texture.format, Is.EqualTo(TextureFormat.Alpha8));
            Assert.That(TryField(texture, out var field), Is.True);
            Assert.That(field.GetAlpha(0, 0), Is.EqualTo(128));
            Assert.That(field.GetAlpha(Size - 1, Size - 1), Is.EqualTo(254));
        }

        /// <summary>
        /// ARGB32 is created as a texture asset rather than requested from the
        /// importer, which rejects it for the Default texture type with a console
        /// error and then produces it anyway. The producer opens no importer, so how
        /// the asset came to be in this format is outside its contract: what it must
        /// handle is an ARGB32 <see cref="Texture2D"/> with a project identity, which
        /// is exactly what this builds. It also confirms that
        /// <see cref="Color32.a"/> is a normalized accessor, since ARGB32's memory
        /// order differs from RGBA32's.
        /// </summary>
        [Test]
        public void ReadableArgb32_IsAdmittedAndExact()
        {
            var texture = CreateTextureAsset("argb32", TextureFormat.ARGB32);

            Assert.That(texture.format, Is.EqualTo(TextureFormat.ARGB32));
            Assert.That(texture.isReadable, Is.True);
            Assert.That(texture.mipmapCount, Is.EqualTo(1));
            Assert.That(TryField(texture, out var field), Is.True);
            Assert.That(field.GetAlpha(0, 0), Is.EqualTo(128));
            Assert.That(field.GetAlpha(Size - 1, Size - 1), Is.EqualTo(254));
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
        public void MipmappedTexture_Refuses()
        {
            var texture = ImportAsymmetric("mips", importer => importer.mipmapEnabled = true);

            Assert.That(texture.mipmapCount, Is.GreaterThan(1));
            Assert.That(TryField(texture, out var field), Is.False);
            Assert.That(field, Is.Null);
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

        /// <summary>
        /// The primary destroyed-object guard is Unity's overloaded <c>== null</c>,
        /// which is true for a destroyed object where <c>ReferenceEquals</c> is
        /// false. Deleting the asset destroys the loaded object, which is the
        /// deterministic way to reach that state through the real API.
        /// </summary>
        [Test]
        public void TextureDestroyedAfterConstruction_RefusesWithoutThrowing()
        {
            var texture = ImportAsymmetric("destroyed");
            Assert.That(UnityTextureEvidence.TryGetSourceId(texture, out var source), Is.True);
            var evidence = new UnityAlphaFieldEvidence(new Texture[] { texture });

            AssetDatabase.DeleteAsset(TempFolder + "/destroyed.png");

            Assert.That(texture == null, Is.True, "Unity's overloaded equality sees the destruction.");
            Assert.That(
                ReferenceEquals(texture, null),
                Is.False,
                "ReferenceEquals does not, which is why the producer must not use it.");

            AlphaTextureData field = null;
            Assert.That(
                () => evidence.TryGetAlphaField(source, TextureChannel.Alpha, out field),
                Throws.Nothing);
            Assert.That(field, Is.Null);
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
            Assert.That(field.GetAlpha(0, 0), Is.EqualTo(128));
            Assert.That(field.GetAlpha(Size - 1, Size - 1), Is.EqualTo(254));
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
                Is.Not.EqualTo(128),
                "The producer must report the generated alpha, not the source alpha.");
            Assert.That(
                field.GetAlpha(0, 0),
                Is.EqualTo(field.GetAlpha(Size - 1, Size - 1)),
                "Uniform RGB yields a uniform generated alpha.");
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

            AssertSameField(first, second, "the producer is deterministic");
        }

        [Test]
        public void SeparateCalls_ReturnIndependentFields()
        {
            var texture = ImportAsymmetric("independence");
            Assert.That(UnityTextureEvidence.TryGetSourceId(texture, out var source), Is.True);
            var evidence = new UnityAlphaFieldEvidence(new Texture[] { texture });

            Assert.That(evidence.TryGetAlphaField(source, TextureChannel.Alpha, out var first), Is.True);
            Assert.That(evidence.TryGetAlphaField(source, TextureChannel.Alpha, out var second), Is.True);

            Assert.That(
                ReferenceEquals(first, second),
                Is.False,
                "No buffer is shared between results, and none is cached.");
            AssertSameField(first, second, "independent fields still agree");
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
