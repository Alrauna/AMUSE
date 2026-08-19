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
    /// Proves the Unity host evidence can be consumed by the existing exact triangle
    /// alpha classifier without an impedance mismatch, through the real
    /// <c>AlphaSemanticsResolver</c> seam.
    /// <para>
    /// Deliberately involves no <see cref="Material"/>, no shader frontend, and no
    /// separation planner: this is the evidence-to-proof link only. The full
    /// renderer-to-plan slice belongs to a later milestone.
    /// </para>
    /// <para>
    /// The fixture is asymmetric on purpose. A uniform texture short-circuits on
    /// <c>AlphaTextureData.IsFullyOpaque</c> before any geometry is examined, so it
    /// would pass every case here even if dimensions, row order, or axis orientation
    /// were wrong.
    /// </para>
    /// </summary>
    public sealed class AlphaEvidenceClassifierIntegrationTests
    {
        private const string TempFolder = "Assets/AmuseTests_AlphaIntegration";
        private const int Size = 4;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AmuseTests_AlphaIntegration");
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
        /// Only the bottom-left texel (0,0) is non-opaque. With a 4x4 texture under
        /// Point/Clamp sampling that texel owns UV [0, 0.25) x [0, 0.25).
        /// </summary>
        private static Texture2D ImportFixture(string name, bool fullyOpaque)
        {
            var path = TempFolder + "/" + name + ".png";
            var staging = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(64, 32, 16, 255);
            }

            if (!fullyOpaque)
            {
                pixels[0] = new Color32(64, 32, 16, 128);
            }

            staging.SetPixels32(pixels);
            staging.Apply();
            File.WriteAllBytes(path, staging.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(staging);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(loaded, Is.Not.Null, $"Imported texture '{path}' must load.");
            return loaded;
        }

        /// <summary>
        /// Builds the chain the milestone exists to close: Unity texture state to
        /// normalized semantics to the resolver to the exact classifier.
        /// </summary>
        private static AlphaResolution Resolve(Texture2D texture)
        {
            Assert.That(
                UnityTextureEvidence.TryGetSourceId(texture, out var source),
                Is.True,
                "The fixture must have a resolvable project identity.");
            Assert.That(
                UnityTextureEvidence.TryGetSampling(texture, out var sampling),
                Is.True,
                "Point filtering with equal Clamp wrap and no mips must be supported.");

            var sample = new TextureSample(
                source,
                new UvMapping(0, Vector2.one, Vector2.zero),
                sampling);
            var alpha = SemanticOutput<ScalarSemanticValue>.Complete(
                ScalarSemanticValue.Texture(sample, TextureChannel.Alpha));

            var evidence = new UnityAlphaFieldEvidence(new Texture[] { texture });
            return AlphaSemanticsResolver.Resolve(alpha, evidence.TryGetAlphaField);
        }

        private static TriangleAlphaInput Triangle(Vector2 uv0, Vector2 uv1, Vector2 uv2)
        {
            return TriangleAlphaInput.WithUv0(
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                uv0,
                uv1,
                uv2);
        }

        /// <summary>Wholly inside the opaque region, far from texel (0,0).</summary>
        private static TriangleAlphaInput OpaqueRegionTriangle()
        {
            return Triangle(
                new Vector2(0.5f, 0.5f),
                new Vector2(0.9f, 0.5f),
                new Vector2(0.5f, 0.9f));
        }

        /// <summary>Wholly inside texel (0,0), the one non-opaque texel.</summary>
        private static TriangleAlphaInput NonOpaqueTexelTriangle()
        {
            return Triangle(
                new Vector2(0.01f, 0.01f),
                new Vector2(0.2f, 0.01f),
                new Vector2(0.01f, 0.2f));
        }

        [Test]
        public void MixedTexture_TriangleInsideTheOpaqueRegion_IsProvenOpaque()
        {
            var resolution = Resolve(ImportFixture("mixed_opaque", fullyOpaque: false));

            Assert.That(resolution.IsResolved, Is.True, resolution.Failure.ToString());
            Assert.That(
                resolution.Classify(OpaqueRegionTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        /// <summary>
        /// The case that would break if the field were flipped, transposed, or scaled
        /// wrongly: the classifier must see the non-opaque texel under this triangle
        /// and refuse to prove opacity.
        /// </summary>
        [Test]
        public void MixedTexture_TriangleCoveringTheNonOpaqueTexel_MustRemainTransparent()
        {
            var resolution = Resolve(ImportFixture("mixed_transparent", fullyOpaque: false));

            Assert.That(resolution.IsResolved, Is.True, resolution.Failure.ToString());

            var outcome = resolution.Classify(NonOpaqueTexelTriangle());

            Assert.That(
                outcome,
                Is.Not.EqualTo(TriangleAlphaOutcome.ProvenOpaque),
                "Proving opacity over a non-opaque texel would be a false positive.");
            Assert.That(outcome, Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void FullyOpaqueTexture_IsProvenOpaqueForAnyTriangle()
        {
            var resolution = Resolve(ImportFixture("opaque", fullyOpaque: true));

            Assert.That(resolution.IsResolved, Is.True, resolution.Failure.ToString());
            Assert.That(
                resolution.Classify(OpaqueRegionTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(
                resolution.Classify(NonOpaqueTexelTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        /// <summary>
        /// A texture the producer refuses must surface as a material-scoped refusal
        /// carrying <c>MissingTextureEvidence</c>, never as a triangle outcome.
        /// </summary>
        [Test]
        public void UnsupportedTexture_RefusesWithMissingTextureEvidence()
        {
            var path = TempFolder + "/unsupported.png";
            var staging = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            staging.SetPixels32(new Color32[Size * Size]);
            staging.Apply();
            File.WriteAllBytes(path, staging.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(staging);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            var resolution = Resolve(texture);

            Assert.That(resolution.IsResolved, Is.False);
            Assert.That(
                resolution.Failure,
                Is.EqualTo(AlphaResolutionFailure.MissingTextureEvidence));
        }
    }
}
