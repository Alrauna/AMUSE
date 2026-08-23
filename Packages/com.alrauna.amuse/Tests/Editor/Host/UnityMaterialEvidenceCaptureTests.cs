using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    public sealed class UnityMaterialEvidenceCaptureTests
    {
        private const string TempFolder = "Assets/AmuseTests_MaterialEvidence";
        private const string PoiyomiFixtureShader =
            "Hidden/Alrauna/AmuseTests/PoiyomiSemanticTest";
        private const string LilToonFixtureShader =
            "Hidden/Alrauna/AmuseTests/LilToonSemanticTest";

        private readonly List<Material> _materials = new List<Material>();

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AmuseTests_MaterialEvidence");
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var material in _materials)
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }

            _materials.Clear();
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        [Test]
        public void RequestCapturesOnlyNamedEvidence()
        {
            var request = Request(
                colors: new[] { "_Color" });
            var evidence = Capture(NewMaterial(PoiyomiFixtureShader), request);

            Assert.That(evidence.TryGetColor("_Color", out _), Is.True);
            Assert.Throws<ArgumentException>(
                () => evidence.TryGetScalar("_Cutoff", out _));
        }

        [Test]
        public void SecondRequestAddsPropertyWithoutChangingCaptureMechanism()
        {
            var request = Request(
                scalars: new[] { "_Cutoff" });
            var evidence = Capture(NewMaterial(PoiyomiFixtureShader), request);

            Assert.That(
                evidence.TryGetScalar("_Cutoff", out var value),
                Is.True);
            Assert.That(value, Is.EqualTo(0.5f));
        }

        [Test]
        public void RequestedPropertyWithWrongUnityTypeReturnsFalse()
        {
            var evidence = Capture(
                NewMaterial(PoiyomiFixtureShader),
                Request(colors: new[] { "_Cutoff" }));

            Assert.That(evidence.TryGetColor("_Cutoff", out _), Is.False);
        }

        [Test]
        public void RequestedUnassignedTextureReturnsAnUnassignedAssignment()
        {
            var material = NewMaterial(PoiyomiFixtureShader);
            material.SetTexture("_MainTex", null);
            var request = Request(
                textures: new[]
                {
                    new TexturePropertyEvidenceRequest(
                        "_MainTex", TextureEvidenceKinds.ScaleOffset),
                });

            var evidence = Capture(material, request);

            Assert.That(
                evidence.TryGetTexture("_MainTex", out var assignment),
                Is.True);
            Assert.That(assignment.IsAssigned, Is.False);
            Assert.That(assignment.HasScaleOffset, Is.True);
            Assert.That(assignment.Texture, Is.Null);
        }

        [Test]
        public void FamilyAlphaRequestsExcludeUnconsumedAndOtherFamilyProperties()
        {
            var poiyomi = PoiyomiMaterialSemantics.AlphaEvidenceRequest;
            foreach (var property in new[]
            {
                "_Cutoff",
                "_Mode",
                "_SrcBlend",
                "_DstBlend",
                "_EmissionColor",
                "_EmissionStrength",
                "_BumpScale",
                "_BumpMapUV",
                "_BumpMap",
                "_EmissionMap",
                "_EnableEmission",
                "_EnableEmission1",
                "_EnableEmission2",
                "_EnableEmission3",
            })
            {
                Assert.That(Requests(poiyomi, property), Is.False, property);
            }

            var lilToon = LilToonMaterialSemantics.AlphaEvidenceRequest;
            foreach (var property in new[]
            {
                "_AlphaForceOpaque",
                "_MainIgnoreTexAlpha",
                "_AlphaToCoverage",
            })
            {
                Assert.That(Requests(lilToon, property), Is.False, property);
            }
        }

        [Test]
        public void CombineUsesOrdinalUnionAndUnionsTextureEvidenceKinds()
        {
            var first = Request(
                shaderName: true,
                presence: new[] { "z", "a" },
                scalars: new[] { "_Cutoff" },
                textures: new[]
                {
                    new TexturePropertyEvidenceRequest(
                        "_MainTex", TextureEvidenceKinds.SourceIdentity),
                });
            var second = Request(
                activeColorSpace: true,
                presence: new[] { "m", "a" },
                colors: new[] { "_Color" },
                textures: new[]
                {
                    new TexturePropertyEvidenceRequest(
                        "_MainTex", TextureEvidenceKinds.Sampling),
                });

            var combined = MaterialEvidenceRequest.Combine(first, second);

            Assert.That(combined.ShaderName, Is.True);
            Assert.That(combined.ActiveColorSpace, Is.True);
            CollectionAssert.AreEqual(
                new[] { "a", "m", "z" }, combined.PresenceProperties);
            Assert.That(combined.TextureProperties.Count, Is.EqualTo(1));
            Assert.That(
                combined.TextureProperties[0].Evidence,
                Is.EqualTo(
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.Sampling));
        }

        [Test]
        public void RequestDefensivelyCopiesCallerCollections()
        {
            var scalars = new List<string> { "_Cutoff" };
            var request = Request(scalars: scalars);

            scalars[0] = "_AlphaForceOpaque";

            CollectionAssert.AreEqual(
                new[] { "_Cutoff" }, request.ScalarProperties);
            Assert.That(
                request.ScalarProperties,
                Is.InstanceOf<ReadOnlyCollection<string>>());
        }

        [Test]
        public void RequestValidationRejectsMalformedOrAmbiguousInputs()
        {
            Assert.Throws<ArgumentException>(
                () => new TexturePropertyEvidenceRequest(
                    " ", TextureEvidenceKinds.None));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TexturePropertyEvidenceRequest(
                    "_MainTex", (TextureEvidenceKinds)(1 << 20)));
            Assert.Throws<ArgumentException>(
                () => Request(scalars: new[] { "_Cutoff", "_Cutoff" }));
            Assert.Throws<ArgumentException>(
                () => Request(
                    scalars: new[] { "_Color" },
                    colors: new[] { "_Color" }));
            Assert.Throws<ArgumentException>(
                () => Request(
                    scalars: new[] { "_MainTex" },
                    textures: new[]
                    {
                        new TexturePropertyEvidenceRequest(
                            "_MainTex", TextureEvidenceKinds.None),
                    }));
        }

        [Test]
        public void CaptureRejectsNullInputListAndNullRequest()
        {
            Assert.Throws<ArgumentNullException>(
                () => UnityMaterialEvidenceCapture.Capture(null));
            Assert.Throws<ArgumentNullException>(
                () => UnityMaterialEvidenceCapture.Capture(
                    new[] { default(MaterialEvidenceCaptureInput) }));
        }

        [Test]
        public void DestroyedMaterialProducesEmptyEvidenceForItsRequest()
        {
            var material = NewMaterial(PoiyomiFixtureShader);
            UnityEngine.Object.DestroyImmediate(material);
            var request = Request(
                shaderName: true,
                scalars: new[] { "_Cutoff" });

            var evidence = Capture(material, request);

            Assert.That(evidence.HasShaderName, Is.False);
            Assert.That(evidence.TryGetScalar("_Cutoff", out _), Is.False);
        }

        [Test]
        public void PerMaterialRequestsShareStableTextureEvidenceAndRemainImmutable()
        {
            var texturePath = TempFolder + "/shared.png";
            var texture = ImportAsymmetric(texturePath);
            var poiyomiMaterial = NewMaterial(PoiyomiFixtureShader);
            var lilToonMaterial = NewMaterial(LilToonFixtureShader);
            poiyomiMaterial.SetFloat("_Cutoff", 0.25f);
            poiyomiMaterial.SetTexture("_MainTex", texture);
            poiyomiMaterial.SetTextureScale("_MainTex", new Vector2(2f, 3f));
            poiyomiMaterial.SetTextureOffset("_MainTex", new Vector2(0.1f, 0.2f));
            lilToonMaterial.SetFloat("_UseEmission", 1f);
            lilToonMaterial.SetTexture("_EmissionMap", texture);

            var textureFacts =
                TextureEvidenceKinds.SourceIdentity |
                TextureEvidenceKinds.Sampling |
                TextureEvidenceKinds.ColorInterpretation |
                TextureEvidenceKinds.ScaleOffset |
                TextureEvidenceKinds.AlphaChannel;
            var poiyomiRequest = Request(
                scalars: new[] { "_Cutoff" },
                textures: new[]
                {
                    new TexturePropertyEvidenceRequest("_MainTex", textureFacts),
                });
            var lilToonRequest = Request(
                scalars: new[] { "_UseEmission" },
                textures: new[]
                {
                    new TexturePropertyEvidenceRequest("_EmissionMap", textureFacts),
                });

            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(poiyomiMaterial, poiyomiRequest),
                new MaterialEvidenceCaptureInput(lilToonMaterial, lilToonRequest),
            });

            Assert.That(captured[0].TryGetScalar("_Cutoff", out var cutoff), Is.True);
            Assert.That(cutoff, Is.EqualTo(0.25f));
            Assert.Throws<ArgumentException>(
                () => captured[0].TryGetScalar("_UseEmission", out _));
            Assert.That(captured[1].TryGetScalar("_UseEmission", out var emissionOn), Is.True);
            Assert.That(emissionOn, Is.EqualTo(1f));
            Assert.Throws<ArgumentException>(
                () => captured[1].TryGetScalar("_Cutoff", out _));

            Assert.That(captured[0].TryGetTexture("_MainTex", out var main), Is.True);
            Assert.Throws<ArgumentException>(
                () => captured[0].TryGetTexture("_EmissionMap", out _));
            Assert.That(captured[1].TryGetTexture("_EmissionMap", out var emission), Is.True);
            Assert.Throws<ArgumentException>(
                () => captured[1].TryGetTexture("_MainTex", out _));
            Assert.That(ReferenceEquals(main.Texture, emission.Texture), Is.True);
            Assert.That(main.Texture.HasAlphaChannel, Is.True);
            Assert.That(main.Texture.AlphaChannel.GetAlpha(0, 0), Is.EqualTo(128));
            Assert.That(main.Texture.AlphaChannel.GetAlpha(3, 3), Is.EqualTo(254));
            Assert.That(main.HasScaleOffset, Is.True);
            Assert.That(main.Scale, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(main.Offset, Is.EqualTo(new Vector2(0.1f, 0.2f)));
            Assert.That(captured[0].Textures.Count, Is.EqualTo(1));
            Assert.That(captured[1].Textures.Count, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(
                () => ((IList)captured[0].Textures).RemoveAt(0));

            var source = main.Texture.SourceIdentity;
            var sampling = main.Texture.Sampling;
            var interpretation = main.Texture.ColorInterpretation;
            var alpha = main.Texture.AlphaChannel;

            texture.SetPixels32(UniformPixels(17));
            texture.Apply();
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
            poiyomiMaterial.SetTexture("_MainTex", null);
            poiyomiMaterial.SetTextureScale("_MainTex", Vector2.one);
            poiyomiMaterial.SetTextureOffset("_MainTex", Vector2.zero);
            lilToonMaterial.SetTexture("_EmissionMap", null);
            UnityEngine.Object.DestroyImmediate(poiyomiMaterial);
            UnityEngine.Object.DestroyImmediate(lilToonMaterial);
            AssetDatabase.DeleteAsset(texturePath);

            Assert.That(main.Texture.HasSourceIdentity, Is.True);
            Assert.That(main.Texture.SourceIdentity, Is.EqualTo(source));
            Assert.That(main.Texture.HasSampling, Is.True);
            Assert.That(main.Texture.Sampling, Is.EqualTo(sampling));
            Assert.That(main.Texture.HasColorInterpretation, Is.True);
            Assert.That(main.Texture.ColorInterpretation, Is.EqualTo(interpretation));
            Assert.That(main.Scale, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(main.Offset, Is.EqualTo(new Vector2(0.1f, 0.2f)));
            Assert.That(main.Texture.AlphaChannel, Is.SameAs(alpha));
            Assert.That(alpha.GetAlpha(0, 0), Is.EqualTo(128));
            Assert.That(alpha.GetAlpha(3, 3), Is.EqualTo(254));
            AssertNoLiveObjectsOrDelegates(captured);
        }

        private Material NewMaterial(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null, shaderName);
            var material = new Material(shader);
            _materials.Add(material);
            return material;
        }

        private static CapturedMaterialEvidence Capture(
            Material material,
            MaterialEvidenceRequest request)
        {
            return UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(material, request),
            })[0];
        }

        private static MaterialEvidenceRequest Request(
            bool shaderName = false,
            bool activeColorSpace = false,
            IEnumerable<string> presence = null,
            IEnumerable<string> scalars = null,
            IEnumerable<string> colors = null,
            IEnumerable<string> vectors = null,
            IEnumerable<TexturePropertyEvidenceRequest> textures = null)
        {
            return new MaterialEvidenceRequest(
                shaderName,
                activeColorSpace,
                presence ?? Array.Empty<string>(),
                scalars ?? Array.Empty<string>(),
                colors ?? Array.Empty<string>(),
                vectors ?? Array.Empty<string>(),
                textures ?? Array.Empty<TexturePropertyEvidenceRequest>());
        }

        private static bool Requests(
            MaterialEvidenceRequest request,
            string property)
        {
            if (Contains(request.PresenceProperties, property) ||
                Contains(request.ScalarProperties, property) ||
                Contains(request.ColorProperties, property) ||
                Contains(request.VectorProperties, property))
            {
                return true;
            }

            foreach (var texture in request.TextureProperties)
            {
                if (string.Equals(
                        texture.PropertyName, property, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains(
            IEnumerable<string> values,
            string expected)
        {
            foreach (var value in values)
            {
                if (string.Equals(value, expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static Texture2D ImportAsymmetric(string path)
        {
            var staging = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = UniformPixels(255);
            pixels[0] = new Color32(64, 32, 16, 128);
            pixels[pixels.Length - 1] = new Color32(64, 32, 16, 254);
            staging.SetPixels32(pixels);
            staging.Apply();
            File.WriteAllBytes(path, staging.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(staging);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Color32[] UniformPixels(byte alpha)
        {
            var pixels = new Color32[16];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(64, 32, 16, alpha);
            }

            return pixels;
        }

        private static void AssertNoLiveObjectsOrDelegates(object root)
        {
            Walk(root, new HashSet<object>(ReferenceEqualityComparer.Instance));
        }

        private static void Walk(object value, HashSet<object> visited)
        {
            if (value == null)
            {
                return;
            }

            var type = value.GetType();
            Assert.That(
                typeof(UnityEngine.Object).IsAssignableFrom(type),
                Is.False,
                "Captured graph retained " + type.FullName);
            Assert.That(
                typeof(Delegate).IsAssignableFrom(type),
                Is.False,
                "Captured graph retained delegate " + type.FullName);

            if (type.IsPrimitive || type.IsEnum || value is string ||
                value is decimal)
            {
                return;
            }

            if (!type.IsValueType && !visited.Add(value))
            {
                return;
            }

            if (value is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    Walk(item, visited);
                }
            }

            for (var current = type; current != null; current = current.BaseType)
            {
                foreach (var field in current.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly))
                {
                    Walk(field.GetValue(value), visited);
                }
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance =
                new ReferenceEqualityComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
