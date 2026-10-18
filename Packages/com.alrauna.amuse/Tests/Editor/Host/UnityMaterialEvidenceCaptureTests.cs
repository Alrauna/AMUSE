using Alrauna.Amuse.Tests.Editor.Build;
using Alrauna.Amuse.Tests.Editor.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using TextureWrapMode = Alrauna.Amuse.Editor.Semantics.TextureWrapMode;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using nadena.dev.ndmf;
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
        private const string LilToonCutoutConversionShader =
            "Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest";
        private const string LilToonTransparentConversionShader =
            "Hidden/Alrauna/AmuseTests/LilToonTransparentConversionTest";
        private const string PoiyomiModeProperty = "_Mode";
        private const string MultiCutoutContainerShaderPath =
            "Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/" +
            "LilToonMultiCutoutTest.shader";
        private const string MultiTransparentContainerShaderPath =
            "Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/" +
            "LilToonMultiTransparentTest.shader";

        private readonly TestTransientScope _scope =
            new TestTransientScope();
        private bool _ownsTempFolder;

        [SetUp]
        public void SetUp()
        {
            _ownsTempFolder = false;
            Assert.That(Directory.Exists(TempFolder), Is.False,
                "The test must not reuse an existing asset folder.");
            Assert.That(File.Exists(TempFolder + ".meta"), Is.False,
                "The test must not replace existing folder metadata.");
            var folderId = AssetDatabase.CreateFolder(
                "Assets", "AmuseTests_MaterialEvidence");
            _ownsTempFolder = !string.IsNullOrEmpty(folderId);
            Assert.That(_ownsTempFolder, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            _scope.TearDown();
            if (_ownsTempFolder && AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
            _ownsTempFolder = false;
        }

        // A shared texture must not transfer a cutout predicate to transparent alpha.
        [TestCase(false)]
        [TestCase(true)]
        public void SharedCutoutTextureDoesNotProveTransparentPartialAlpha(bool reverse)
        {
            var texture = CreateIsolationTexture();
            var transparent = NewMaterial(
                "Hidden/Alrauna/AmuseTests/LilToonTransparentConversionTest");
            var cutout = NewMaterial(
                "Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest");
            transparent.SetTexture("_MainTex", texture);
            transparent.SetFloat("_Cutoff", 0.01f);
            cutout.SetTexture("_MainTex", texture);
            cutout.SetFloat("_Cutoff", 0.25f);
            var transparentInput = new MaterialEvidenceCaptureInput(
                transparent, LilToonTransparentMaterialSemantics.AlphaEvidenceRequest);
            var cutoutInput = new MaterialEvidenceCaptureInput(
                cutout, LilToonCutoutMaterialSemantics.AlphaEvidenceRequest);

            var alone = UnityMaterialEvidenceCapture.Capture(
                new[] { transparentInput })[0];
            Assert.That(ClassifyIsolationRegion(alone, 0.4f),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
            Assert.That(ClassifyIsolationRegion(alone, 0.75f),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));

            var captured = UnityMaterialEvidenceCapture.Capture(reverse
                ? new[] { cutoutInput, transparentInput }
                : new[] { transparentInput, cutoutInput });
            var subject = captured[reverse ? 1 : 0];
            Assert.That(ClassifyIsolationRegion(subject, 0.05f),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
            Assert.That(ClassifyIsolationRegion(subject, 0.4f),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
            Assert.That(ClassifyIsolationRegion(subject, 0.75f),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        // Taking the lower threshold would incorrectly prove the stricter material.
        [TestCase(false)]
        [TestCase(true)]
        public void SharedCutoutTextureKeepsEachMaterialsThreshold(bool reverse)
        {
            var texture = CreateIsolationTexture();
            var lower = NewMaterial(
                "Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest");
            var higher = NewMaterial(
                "Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest");
            lower.SetTexture("_MainTex", texture);
            lower.SetFloat("_Cutoff", 0.25f);
            higher.SetTexture("_MainTex", texture);
            higher.SetFloat("_Cutoff", 0.75f);
            var lowerInput = new MaterialEvidenceCaptureInput(
                lower, LilToonCutoutMaterialSemantics.AlphaEvidenceRequest);
            var higherInput = new MaterialEvidenceCaptureInput(
                higher, LilToonCutoutMaterialSemantics.AlphaEvidenceRequest);

            var alone = UnityMaterialEvidenceCapture.Capture(new[] { higherInput })[0];
            Assert.That(ClassifyIsolationRegion(alone, 0.4f),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
            Assert.That(ClassifyIsolationRegion(alone, 0.75f),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));

            var captured = UnityMaterialEvidenceCapture.Capture(reverse
                ? new[] { higherInput, lowerInput }
                : new[] { lowerInput, higherInput });
            Assert.That(ClassifyIsolationRegion(captured[reverse ? 1 : 0], 0.4f),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(ClassifyIsolationRegion(captured[reverse ? 0 : 1], 0.4f),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        // --- Falsifier 2: A batch-uniform capture cap, stamped once per
        // batch as at base, gives both arms one cap. The mixed-batch
        // shared-texture fixture asserts each arm's chain level count and
        // fails it. ---
        [Test]
        public void MixedBatchCapturesEachArmAtItsOwnCap()
        {
            // The RED form of this test captured against the one-cap
            // signature. The batch request carried the exact cap 4 alone,
            // and the cutout arm read five levels instead of one: one
            // request carried one cap. The two-cap form pins each bucket
            // to its own arm, so the ideal per-arm outcome holds in code.
            var captured = CaptureMixedBatchAtArms(0, 4);
            Assert.That(
                captured[0].TryGetTexture("_MainTex", out var cutoutAssignment),
                Is.True);
            Assert.That(
                captured[1].TryGetTexture("_MainTex", out var exactAssignment),
                Is.True);
            Assert.That(cutoutAssignment.Texture.AlphaChannel.Count,
                Is.EqualTo(1),
                "the cutout arm's chain holds the cutout cap's one level");
            Assert.That(exactAssignment.Texture.AlphaChannel.Count,
                Is.EqualTo(5),
                "the exact arm's chain holds the exact cap's five levels");
            // The intersect pin: a request cap narrower than its arm cap
            // yields the narrower chain.
            var narrowed = CaptureMixedBatchAtArms(0, 4, requestRawMipCap: 2);
            Assert.That(
                narrowed[1].TryGetTexture(
                    "_MainTex", out var narrowedAssignment),
                Is.True);
            Assert.That(narrowedAssignment.Texture.AlphaChannel.Count,
                Is.EqualTo(3),
                "the request cap narrower than the exact arm's cap " +
                "narrows the chain");
        }

        // --- Falsifier 3: A bucket cap keyed on the predicate declaration
        // instead of the captured threshold lets a cutoff-1.0 cutout
        // material stamp the cutout cap into the 1.0 bucket. The exact
        // arm's chain level count assertion fails it. ---
        [Test]
        public void CutoutMaterialsAtCutoffOneCaptureAtTheTransparentCap()
        {
            var captured = CaptureMixedBatchAtArms(0, 4, cutoutCutoff: 1f);
            Assert.That(
                captured[0].TryGetTexture("_MainTex", out var cutoutAssignment),
                Is.True);
            Assert.That(
                captured[1].TryGetTexture("_MainTex", out var exactAssignment),
                Is.True);
            Assert.That(
                ReferenceEquals(
                    cutoutAssignment.Texture, exactAssignment.Texture),
                Is.True,
                "the cutoff-one bucket and the exact bucket are one " +
                "shared capture");
            Assert.That(cutoutAssignment.Texture.AlphaChannel.Count,
                Is.EqualTo(5),
                "the cutoff-one cutout material captures at the exact " +
                "arm's cap");
            Assert.That(exactAssignment.Texture.AlphaChannel.Count,
                Is.EqualTo(5),
                "the exact arm's chain holds the exact cap's five levels");
        }

        // --- Falsifier 4: A cross-family leak lets a cutout value move a
        // transparent verdict, or the reverse. The isolation assertions
        // fail it. ---
        [Test]
        public void ChangingTheCutoutCapLeavesTheExactArmsChainUnchanged()
        {
            var captured = CaptureMixedBatchAtArms(3, 4);
            Assert.That(
                captured[0].TryGetTexture("_MainTex", out var cutoutAssignment),
                Is.True);
            Assert.That(
                captured[1].TryGetTexture("_MainTex", out var exactAssignment),
                Is.True);
            Assert.That(cutoutAssignment.Texture.AlphaChannel.Count,
                Is.EqualTo(4),
                "the cutout arm's chain moved with the cutout cap 3");
            Assert.That(exactAssignment.Texture.AlphaChannel.Count,
                Is.EqualTo(5),
                "the exact arm's chain still holds the exact cap's five " +
                "levels");
            Assert.That(ClassifyIsolationRegion(captured[1], 0.4f),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent),
                "the transparent verdict stays under the changed cutout " +
                "cap");
            Assert.That(ClassifyIsolationRegion(captured[1], 0.75f),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque),
                "the transparent verdict stays under the changed cutout " +
                "cap");
        }

        /// <summary>
        /// Builds the closed mixed batch the production assembly builds:
        /// one union request for the whole batch, each material's own
        /// family request as its predicate, and the wrapper's wider-cap
        /// stamp on the request. The two arm caps pin each bucket's chain
        /// through the closed capture. The optional request cap overrides
        /// the wider stamp, so a test can supply a request narrower than
        /// an arm cap.
        /// </summary>
        private CapturedMaterialEvidence[] CaptureMixedBatchAtArms(
            int cutoutRawMipCap,
            int exactRawMipCap,
            float cutoutCutoff = 0.25f,
            int? requestRawMipCap = null)
        {
            var texture = CreateArmCapTexture();
            var transparent = NewMaterial(
                "Hidden/Alrauna/AmuseTests/LilToonTransparentConversionTest");
            var cutout = NewMaterial(
                "Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest");
            transparent.SetTexture("_MainTex", texture);
            cutout.SetTexture("_MainTex", texture);
            cutout.SetFloat("_Cutoff", cutoutCutoff);
            var request = MaterialEvidenceRequest.Combine(
                LilToonCutoutMaterialSemantics.AlphaEvidenceRequest,
                LilToonTransparentMaterialSemantics.AlphaEvidenceRequest)
                .WithRawMipCap(requestRawMipCap
                    ?? Math.Max(cutoutRawMipCap, exactRawMipCap));
            var inputs = new[]
            {
                new MaterialEvidenceCaptureInput(
                    cutout, request,
                    LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
                new MaterialEvidenceCaptureInput(
                    transparent, request,
                    LilToonTransparentMaterialSemantics.AlphaEvidenceRequest),
            };

            return UnityMaterialEvidenceCapture.Capture(
                inputs, AlphaPolicyBounds.Inert,
                cutoutRawMipCap, exactRawMipCap).ToArray();
        }

        [Test]
        public void RegisteredCloneOverAdmittedAtlas_ProvesOpaqueAtItsOwnCutoff()
        {
            var originSource = new Material(
                Shader.Find("Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest"));
            AssetDatabase.CreateAsset(
                originSource, TempFolder + "/atlas-batch-source.mat");
            var clone = new Material(originSource.shader)
            {
                name = originSource.name + " build copy",
            };
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            atlas.name = "MainTex (AAO UV Packed)";
            var colors = new Color32[16 * 16];
            for (var i = 0; i < colors.Length; i++)
            {
                colors[i] = new Color32(255, 255, 255, 255);
            }
            atlas.SetPixels32(colors);
            atlas.Apply(false);
            clone.SetTexture("_MainTex", atlas);
            clone.SetFloat("_Cutoff", 0.75f);
            var registryGuard = new ObjectRegistryGuard();
            try
            {
                ObjectRegistry.RegisterReplacedObject(originSource, clone);
                AaoAtlasTextureAttestation.Versions.ResetForTests();
                AaoAtlasTextureAttestation.Versions.ReadInstalledPackageVersionOrNull =
                    _ => "1.9.17";

                var captured = UnityMaterialEvidenceCapture.Capture(
                    new[]
                    {
                        new MaterialEvidenceCaptureInput(
                            clone,
                            LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
                    })[0];

                Assert.That(
                    ClassifyIsolationRegion(captured, 0.75f),
                    Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque),
                    "a registered clone over an admitted opaque atlas " +
                    "proves its region opaque");
            }
            finally
            {
                registryGuard.Dispose();
                AaoAtlasTextureAttestation.Versions.ResetForTests();
                UnityEngine.Object.DestroyImmediate(atlas);
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void UnregisteredCloneOverAdmittedAtlas_StaysUnknown()
        {
            var clone = NewMaterial(
                "Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest");
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            atlas.name = "MainTex (AAO UV Packed)";
            var colors = new Color32[16 * 16];
            for (var i = 0; i < colors.Length; i++)
            {
                colors[i] = new Color32(255, 255, 255, 255);
            }
            atlas.SetPixels32(colors);
            atlas.Apply(false);
            clone.SetTexture("_MainTex", atlas);
            clone.SetFloat("_Cutoff", 0.75f);
            try
            {
                AaoAtlasTextureAttestation.Versions.ResetForTests();
                AaoAtlasTextureAttestation.Versions.ReadInstalledPackageVersionOrNull =
                    _ => "1.9.17";

                var captured = UnityMaterialEvidenceCapture.Capture(
                    new[]
                    {
                        new MaterialEvidenceCaptureInput(
                            clone,
                            LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
                    })[0];

                Assert.That(
                    captured.TryGetTexture("_MainTex", out var assignment),
                    Is.True);
                Assert.That(
                    assignment.Texture.HasAlphaChannel, Is.False,
                    "without a registry pair the slot material corroborates " +
                    "nothing, so the region stays unproven");
                Assert.That(
                    assignment.Texture.AlphaCaptureRefusal,
                    Is.EqualTo(TextureCaptureRefusalReason.UnavailableCapture),
                    "the identity gate is the first gate that refuses, so " +
                    "the capture records the unavailable-capture family");
            }
            finally
            {
                AaoAtlasTextureAttestation.Versions.ResetForTests();
                UnityEngine.Object.DestroyImmediate(atlas);
            }
        }

        private static Texture2D CreateIsolationTexture()
        {
            var path = TempFolder + "/predicate-isolation.asset";
            var container = ScriptableObject.CreateInstance<
                nadena.dev.ndmf.runtime.SubAssetContainer>();
            AssetDatabase.CreateAsset(container, path);
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false, true)
            {
                name = "Synthetic (AAO UV Packed)",
                filterMode = FilterMode.Point,
                wrapMode = UnityEngine.TextureWrapMode.Clamp
            };
            AssetDatabase.AddObjectToAsset(texture, path);
            var pixels = new Color32[128 * 128];
            for (var y = 0; y < 128; y++)
            for (var x = 0; x < 128; x++)
            {
                var alpha = (byte)(x < 42 ? 0 : x < 84 ? 128 : 255);
                pixels[y * 128 + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            AssetDatabase.SaveAssets();
            Assert.That(
                GeneratedTextureAttestation.TryIdentifyProducer(texture),
                Is.True,
                "The fixture must enter the generated capture route.");
            return texture;
        }

        /// <summary>
        /// The arm-cap fixture: a real imported PNG of the isolation
        /// columns with the importer's mip chain. The asset declares
        /// eight levels, so the cap semantics are observable on the
        /// imported GPU route: cap 0 keeps one level, cap 3 keeps four,
        /// and cap 4 keeps five. The generated route reads the live
        /// object and never applies the raw mip cap, so a generated
        /// fixture cannot express these counts.
        /// </summary>
        private static Texture2D CreateArmCapTexture()
        {
            var path = TempFolder + "/arm-cap-isolation.png";
            var pixels = new Color32[128 * 128];
            for (var y = 0; y < 128; y++)
            for (var x = 0; x < 128; x++)
            {
                var alpha = (byte)(x < 42 ? 0 : x < 84 ? 128 : 255);
                pixels[y * 128 + x] = new Color32(255, 255, 255, alpha);
            }
            var texture = TestTextureImport.WritePng(
                path, 128, 128, pixels, importer =>
                {
                    importer.isReadable = true;
                    importer.textureCompression =
                        TextureImporterCompression.Uncompressed;
                    importer.filterMode = FilterMode.Point;
                    importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                });
            Assert.That(texture.mipmapCount, Is.EqualTo(8),
                "The arm-cap fixture must declare the full mip chain.");
            return texture;
        }

        private static TriangleAlphaOutcome ClassifyIsolationRegion(
            CapturedMaterialEvidence material, float left)
        {
            Assert.That(material.TryGetTexture("_MainTex", out var assignment), Is.True);
            Assert.That(assignment.Texture.HasAlphaChannel, Is.True,
                "Missing evidence is not this regression.");
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero, Vector3.right, Vector3.up,
                new Vector2(left, 0.1f),
                new Vector2(left + 0.1f, 0.1f),
                new Vector2(left, 0.2f));
            return TriangleAlphaClassifier.Classify(
                triangle,
                assignment.Texture.AlphaChannel[0],
                new TextureSampling(TextureFilterMode.Point, TextureWrapMode.Clamp),
                AlphaUvEnvelope.Zero, 0);
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
        public void AlphaMaskIsRequestedForTheRedFieldRoute()
        {
            // The bound Replace mask proves through its red channel, so the
            // request gathers exactly the three facts that route reads: the
            // mask's own scale and offset, its stable identity, and the red
            // field. The mask's own sampling is never asked for, because the
            // vendor mask block samples through the main sampler.
            var poiyomi = PoiyomiMaterialSemantics.AlphaEvidenceRequest;

            var mask = poiyomi.TextureProperties.FirstOrDefault(
                texture => texture.PropertyName == "_AlphaMask");
            Assert.That(
                mask.PropertyName,
                Is.EqualTo("_AlphaMask"),
                "the Poiyomi alpha request must include _AlphaMask");
            Assert.That(
                mask.Evidence,
                Is.EqualTo(
                    TextureEvidenceKinds.ScaleOffset |
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.RedChannel));
        }

        [Test]
        public void AlphaMaskAssignmentIsCapturedWithoutUnusedTextureFacts()
        {
            var texture = ImportAsymmetric(TempFolder + "/mask.png");
            var assignedMaterial = NewMaterial(PoiyomiFixtureShader);
            assignedMaterial.SetTexture("_AlphaMask", texture);
            var unassignedMaterial = NewMaterial(PoiyomiFixtureShader);
            unassignedMaterial.SetTexture("_AlphaMask", null);
            var request = Request(
                textures: new[]
                {
                    new TexturePropertyEvidenceRequest(
                        "_AlphaMask", TextureEvidenceKinds.None),
                });

            var assigned = Capture(assignedMaterial, request);
            var unassigned = Capture(unassignedMaterial, request);

            Assert.That(
                assigned.TryGetTexture("_AlphaMask", out var bound), Is.True);
            Assert.That(bound.IsAssigned, Is.True);
            Assert.That(
                bound.RequestedEvidence,
                Is.EqualTo(TextureEvidenceKinds.None));
            Assert.That(bound.HasScaleOffset, Is.False);
            Assert.That(bound.Texture.HasSourceIdentity, Is.False);
            Assert.That(bound.Texture.HasSampling, Is.False);

            Assert.That(
                unassigned.TryGetTexture("_AlphaMask", out var empty), Is.True);
            Assert.That(empty.IsAssigned, Is.False);
        }

        // --- Named capture refusals -----------------------------------------

        [Test]
        public void UncapturableTextureRecordsANamedRefusalWithoutAChain()
        {
            // Falsifies: the silent refusal of the pre-refusal capture, which
            // recorded HasAlphaChannel == false and nothing about why no
            // chain exists; and a refusal record that fires for a field that
            // did capture or for an unassigned slot.
            var material = NewMaterial(PoiyomiFixtureShader);
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            try
            {
                material.SetTexture("_MainTex", texture);
                var request = Request(textures: new[]
                {
                    new TexturePropertyEvidenceRequest(
                        "_MainTex",
                        TextureEvidenceKinds.SourceIdentity |
                        TextureEvidenceKinds.AlphaChannel),
                });

                var evidence = Capture(material, request);

                Assert.That(
                    evidence.TryGetTexture("_MainTex", out var assignment),
                    Is.True,
                    "fixture precondition: the assignment itself exists");
                Assert.That(assignment.IsAssigned, Is.True);
                Assert.That(
                    assignment.Texture.HasAlphaChannel, Is.False,
                    "the refused capture must produce no chain");
                Assert.That(assignment.Texture.AlphaChannel, Is.Null);
                Assert.That(
                    assignment.CaptureRefusals, Has.Count.EqualTo(1),
                    "exactly the refused alpha field may carry a refusal");
                var refusal = assignment.CaptureRefusals[0];
                Assert.That(refusal.PropertyName, Is.EqualTo("_MainTex"));
                Assert.That(
                    refusal.Channel, Is.EqualTo(TextureChannel.Alpha));
                Assert.That(
                    refusal.Reason,
                    Is.EqualTo(TextureCaptureRefusalReason
                        .UnavailableCapture),
                    "an in-memory texture has no source identity, so the " +
                    "identity gate is the first gate that refuses");
                Assert.That(refusal.HasSourceIdentity, Is.False);
                Assert.That(
                    evidence.CaptureRefusals, Has.Count.EqualTo(1),
                    "the material evidence must aggregate exactly its " +
                    "assignment's refusal");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void UnsupportedFormatRefusalNamesTheAssetTexture()
        {
            // Falsifies: a capture refusal that cannot tell the reader which
            // gate refused, or one that loses the asset identity even though
            // the identity gate passes before the format gate.
            var material = NewMaterial(PoiyomiFixtureShader);
            var texture = ImportRefusedFormat(
                TempFolder + "/refused-format.png");
            material.SetTexture("_MainTex", texture);
            var request = Request(textures: new[]
            {
                new TexturePropertyEvidenceRequest(
                    "_MainTex",
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.AlphaChannel),
            });

            var evidence = Capture(material, request);

            Assert.That(
                evidence.TryGetTexture("_MainTex", out var assignment),
                Is.True);
            Assert.That(
                assignment.Texture.HasAlphaChannel, Is.False,
                "the refused format must produce no chain");
            Assert.That(assignment.CaptureRefusals, Has.Count.EqualTo(1));
            var refusal = assignment.CaptureRefusals[0];
            Assert.That(refusal.PropertyName, Is.EqualTo("_MainTex"));
            Assert.That(
                refusal.Reason,
                Is.EqualTo(TextureCaptureRefusalReason.UnsupportedFormat));
            Assert.That(
                refusal.HasSourceIdentity, Is.True,
                "an imported asset resolves its identity before the " +
                "format gate refuses");
        }

        [Test]
        public void RefusedTextureLeavesTheSiblingMaterialEvidenceUntouched()
        {
            // Falsifies: a batch-wide poisoning where one refused capture
            // clears every material's chains, and a refusal record that
            // names the sibling's texture or lands on the sibling's
            // assignment.
            var refusedMaterial = NewMaterial(PoiyomiFixtureShader);
            var refusedTexture =
                new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var capturedMaterial = NewMaterial(PoiyomiFixtureShader);
            var capturedTexture = ImportAsymmetric(
                TempFolder + "/refusal-sibling.png");
            try
            {
                refusedMaterial.SetTexture("_MainTex", refusedTexture);
                capturedMaterial.SetTexture("_MainTex", capturedTexture);
                var request = Request(textures: new[]
                {
                    new TexturePropertyEvidenceRequest(
                        "_MainTex",
                        TextureEvidenceKinds.SourceIdentity |
                        TextureEvidenceKinds.AlphaChannel),
                });

                var evidence = UnityMaterialEvidenceCapture.Capture(new[]
                {
                    new MaterialEvidenceCaptureInput(
                        refusedMaterial, request),
                    new MaterialEvidenceCaptureInput(
                        capturedMaterial, request),
                });

                Assert.That(
                    evidence[0].TryGetTexture("_MainTex", out var refused),
                    Is.True);
                Assert.That(refused.Texture.HasAlphaChannel, Is.False);
                Assert.That(refused.CaptureRefusals, Has.Count.EqualTo(1));
                Assert.That(
                    refused.CaptureRefusals[0].PropertyName,
                    Is.EqualTo("_MainTex"));

                Assert.That(
                    evidence[1].TryGetTexture("_MainTex", out var sibling),
                    Is.True);
                Assert.That(
                    sibling.Texture.HasAlphaChannel, Is.True,
                    "the sibling's texture must capture its chain in the " +
                    "same batch");
                Assert.That(sibling.Texture.AlphaChannel, Is.Not.Null);
                Assert.That(
                    sibling.CaptureRefusals, Is.Empty,
                    "the unaffected sibling must carry no refusal record");
                Assert.That(evidence[1].CaptureRefusals, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(refusedTexture);
            }
        }

        [Test]
        public void FamilyAlphaRequestsExcludeUnconsumedAndOtherFamilyProperties()
        {
            var poiyomi = PoiyomiMaterialSemantics.AlphaEvidenceRequest;

            // _Mode and _Cutoff joined the alpha request with the cutout
            // coverage split: the interpretation branches on the captured
            // preset and the main texture request declares the cutoff, so
            // both scalars are captured facts now. They therefore sit on the
            // consumed side of this list, not the excluded side.
            foreach (var property in new[]
            {
                "_SrcBlendFA",
                "_DstBlendFA",
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

        // --- Falsifier: a capture that ignores keyword state leaves the Multi mode gate blind. ---
        [Test]
        public void CaptureExposesMaterialKeywordsOnlyWhenTheRequestAsksForThem()
        {
            // Multi containers carry render mode as keyword state on one
            // shader asset: UNITY_UI_CLIP_RECT reads as transparent mode,
            // _COLOROVERLAY_ON as the alpha-mask feature. A regular-family
            // request reused unchanged captures none of that, so the Multi
            // mode gate would be blind. The keyword set enters the evidence
            // only under an explicit request flag, keeping the
            // closed-request rule: nothing outside the request is captured.
            //
            // Keyword-state isolation: the string keyword set lives on the
            // material, not on the shared shader asset, and this test's
            // material is created here and destroyed in TearDown, so no
            // sibling test observes these keywords.
            var material = NewMaterial(LilToonFixtureShader);
            material.shaderKeywords = new[]
            {
                "_COLOROVERLAY_ON",
                "UNITY_UI_CLIP_RECT",
            };

            var requested = Capture(material, Request(captureKeywords: true));
            var unrequested = Capture(material, Request());

            Assert.That(
                requested.Keywords,
                Is.EqualTo(new[] { "UNITY_UI_CLIP_RECT", "_COLOROVERLAY_ON" }),
                "the enabled keyword set must be captured complete and sorted");
            Assert.That(
                unrequested.Keywords,
                Is.Empty,
                "the request is closed: no flag, no keyword facts");
        }

        // --- Falsifier 8: A coverage selection keyed on the stored family enum routes a Poiyomi cutout preset and a plain Poiyomi sibling to the same pair. The Poiyomi sibling rows of this table assert different memberships and fail it. ---
        [Test]
        public void MenuMembershipRecordsPerMaterial()
        {
            // One closed batch covers the six menu-membership sources. Each
            // recorded flag answers one question: does the material's own
            // predicate request declare a cutoff property. The stored
            // family alone cannot answer it. A Poiyomi cutout preset and a
            // plain preset share one family and must not share one
            // membership. A Multi container's own predicate request is the
            // union schema, which carries every resolved mode's
            // declaration, so the resolved mode's own family predicate
            // decides at the one resolution point.
            var cutoutMaterial = NewMaterial(LilToonCutoutConversionShader);
            var transparentMaterial = NewMaterial(
                LilToonTransparentConversionShader);
            var multiCutoutMaterial = MultiContainerMaterial(
                MultiCutoutContainerShaderPath, 1f, "UNITY_UI_ALPHACLIP");
            var multiTransparentMaterial = MultiContainerMaterial(
                MultiTransparentContainerShaderPath, 2f,
                "UNITY_UI_CLIP_RECT");
            var poiyomiCutoutMaterial = NewMaterial(PoiyomiFixtureShader);
            poiyomiCutoutMaterial.SetFloat(PoiyomiModeProperty, 1f);
            var poiyomiPlainMaterial = NewMaterial(PoiyomiFixtureShader);

            var multiCutoutSelected =
                UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                    multiCutoutMaterial,
                    out var multiCutoutFamily,
                    out _,
                    out var multiSchema);
            Assert.That(multiCutoutSelected, Is.True);
            Assert.That(
                multiCutoutFamily,
                Is.EqualTo(CapturedAlphaMaterialFamily.LilToonMulti));
            var multiTransparentSelected =
                UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                    multiTransparentMaterial,
                    out var multiTransparentFamily,
                    out _,
                    out _);
            Assert.That(multiTransparentSelected, Is.True);
            Assert.That(
                multiTransparentFamily,
                Is.EqualTo(CapturedAlphaMaterialFamily.LilToonMulti));

            // One union request for the whole batch: each family's own
            // capture schema, exactly as the production assembly unions
            // the batch's selected schemas.
            var request = MaterialEvidenceRequest.Combine(
                MaterialEvidenceRequest.Combine(
                    LilToonCutoutMaterialSemantics.AlphaEvidenceRequest,
                    LilToonCutoutSourceEligibility
                        .ConversionEvidenceRequest),
                MaterialEvidenceRequest.Combine(
                    LilToonTransparentMaterialSemantics
                        .AlphaEvidenceRequest,
                    LilToonTransparentSourceEligibility
                        .ConversionEvidenceRequest),
                multiSchema,
                MaterialEvidenceRequest.Combine(
                    PoiyomiMaterialSemantics.AlphaEvidenceRequest,
                    PoiyomiOpaqueConversion.ConversionEvidenceRequest));

            VerifiedLilToonTestSeams.CaptureVerifiedFixtureMaterials(
                new[]
                {
                    cutoutMaterial,
                    transparentMaterial,
                    multiCutoutMaterial,
                    multiTransparentMaterial,
                    poiyomiCutoutMaterial,
                    poiyomiPlainMaterial,
                },
                new[]
                {
                    CapturedAlphaMaterialFamily.LilToonCutout,
                    CapturedAlphaMaterialFamily.LilToonTransparent,
                    multiCutoutFamily,
                    multiTransparentFamily,
                    CapturedAlphaMaterialFamily.Poiyomi,
                    CapturedAlphaMaterialFamily.Poiyomi,
                },
                request,
                AlphaPolicyBounds.Inert,
                int.MaxValue,
                int.MaxValue,
                out var captured);
            Assert.That(captured.Captured, Has.Count.EqualTo(6));

            Assert.That(
                captured.Captured[0].Family,
                Is.EqualTo(CapturedAlphaMaterialFamily.LilToonCutout));
            Assert.That(
                captured.Captured[0].PredicatesDeclareCutoff,
                Is.True,
                "the lilToon cutout family's own request declares the " +
                "cutoff");
            Assert.That(
                captured.Captured[1].Family,
                Is.EqualTo(CapturedAlphaMaterialFamily.LilToonTransparent));
            Assert.That(
                captured.Captured[1].PredicatesDeclareCutoff,
                Is.False,
                "the lilToon transparent family's own request declares " +
                "no cutoff");
            Assert.That(
                captured.Captured[2].MultiResolution.IsResolved,
                Is.True,
                "the mode-1 container must resolve to prove the row is " +
                "a resolved Multi record");
            Assert.That(
                captured.Captured[2].Family,
                Is.EqualTo(CapturedAlphaMaterialFamily.LilToonCutout),
                "the mode-1 container resolves to the cutout family");
            Assert.That(
                captured.Captured[2].PredicatesDeclareCutoff,
                Is.True,
                "the resolved mode-1 membership rides the cutout " +
                "family's own declaration");
            Assert.That(
                captured.Captured[3].MultiResolution.IsResolved,
                Is.True,
                "the mode-2 container must resolve to prove the row is " +
                "a resolved Multi record");
            Assert.That(
                captured.Captured[3].Family,
                Is.EqualTo(CapturedAlphaMaterialFamily.LilToonTransparent),
                "the mode-2 container resolves to the transparent " +
                "family");
            Assert.That(
                captured.Captured[3].PredicatesDeclareCutoff,
                Is.False,
                "the resolved mode-2 membership rides the transparent " +
                "family's own declaration");
            Assert.That(
                captured.Captured[4].Family,
                Is.EqualTo(CapturedAlphaMaterialFamily.Poiyomi));
            Assert.That(
                captured.Captured[4].PredicatesDeclareCutoff,
                Is.True,
                "the cutout preset's own predicate declares the cutoff " +
                "under the same stored family as row six");
            Assert.That(
                captured.Captured[5].Family,
                Is.EqualTo(CapturedAlphaMaterialFamily.Poiyomi));
            Assert.That(
                captured.Captured[5].PredicatesDeclareCutoff,
                Is.False,
                "the plain preset's own predicate declares no cutoff " +
                "under the same stored family as row five");
        }

        /// <summary>
        /// Creates one committed Multi container stand-in material at one
        /// mode's vendor state. The stand-in loads by asset path, because
        /// the fixture containers share the vendor-mimic shader name. The
        /// mode and both gate scalars are set explicitly, so the fixture
        /// never depends on stand-in defaults.
        /// </summary>
        private Material MultiContainerMaterial(
            string shaderAssetPath,
            float transparentMode,
            string modeKeyword)
        {
            var shader =
                AssetDatabase.LoadAssetAtPath<Shader>(shaderAssetPath);
            Assert.That(shader, Is.Not.Null, shaderAssetPath);
            var material = new Material(shader);
            _scope.Track(material);
            material.SetFloat("_TransparentMode", transparentMode);
            material.SetFloat("_UseClippingCanceller", 0f);
            material.SetFloat("_AsOverlay", 0f);
            material.shaderKeywords = new[] { modeKeyword };
            return material;
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
            // Both assignments share one evidence object, so both share one chain
            // instance. This proves shared evidence identity across the batch; it
            // does NOT prove the GPU capture executed exactly once - a discarded
            // second capture would leave this green. Once-per-batch acquisition is
            // a code-structure property of UnityMaterialEvidenceCapture.Capture,
            // confirmed by review rather than by instrumentation.
            Assert.That(
                ReferenceEquals(
                    main.Texture.AlphaChannel, emission.Texture.AlphaChannel),
                Is.True);
            Assert.That(main.Texture.HasAlphaChannel, Is.True);
            // Predicate bytes, not magnitudes: sampled alpha exactly one stores
            // 255, and every finite value below one stores 0.
            Assert.That(main.Texture.AlphaChannel[0].GetAlpha(1, 1), Is.EqualTo(255));
            Assert.That(main.Texture.AlphaChannel[0].GetAlpha(0, 0), Is.EqualTo(0));
            Assert.That(main.Texture.AlphaChannel[0].GetAlpha(3, 3), Is.EqualTo(0));
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
            Assert.That(alpha[0].GetAlpha(1, 1), Is.EqualTo(255));
            Assert.That(alpha[0].GetAlpha(0, 0), Is.EqualTo(0));
            Assert.That(alpha[0].GetAlpha(3, 3), Is.EqualTo(0));
            AssertNoLiveObjectsOrDelegates(captured);
        }

        // ------------------------------------------------------------------
        // Task 18: presence-preserving immutable evidence substitution.
        //
        // WithScalar/WithColor/WithVector are PRIMITIVES, not an authorization
        // path. They write whatever value an authorized caller supplies into a
        // present entry, which is why these tests hand them arbitrary values
        // that admission (Tasks 12-13) would reject; an absent entry keeps its
        // captured default and the supplied value is discarded. Whether a value may be substituted at all is
        // decided upstream; the primitive only derives evidence.
        // ------------------------------------------------------------------

        [Test]
        public void ScalarSubstitutionDerivesANewValueWithoutMutatingTheSource()
        {
            var captured = Substitutable();
            Assert.That(captured.TryGetScalar("_Cutoff", out var before), Is.True);

            var substituted = captured.WithScalar("_Cutoff", 0.25f);

            Assert.That(substituted, Is.Not.SameAs(captured));
            Assert.That(substituted.TryGetScalar("_Cutoff", out var after), Is.True);
            Assert.That(after, Is.EqualTo(0.25f));
            Assert.That(captured.TryGetScalar("_Cutoff", out var unchanged), Is.True);
            Assert.That(unchanged, Is.EqualTo(before),
                "substitution mutated the source evidence");
        }

        [Test]
        public void ColorSubstitutionDerivesANewValueWithoutMutatingTheSource()
        {
            var captured = Substitutable();
            Assert.That(captured.TryGetColor("_Color", out var before), Is.True);

            var value = new Color(0.1f, 0.2f, 0.3f, 0.5f);
            var substituted = captured.WithColor("_Color", value);

            Assert.That(substituted, Is.Not.SameAs(captured));
            Assert.That(substituted.TryGetColor("_Color", out var after), Is.True);
            Assert.That(after, Is.EqualTo(value));
            Assert.That(captured.TryGetColor("_Color", out var unchanged), Is.True);
            Assert.That(unchanged, Is.EqualTo(before),
                "substitution mutated the source evidence");
        }

        [Test]
        public void VectorSubstitutionDerivesANewValueWithoutMutatingTheSource()
        {
            var captured = Substitutable();
            Assert.That(captured.TryGetVector("_MainTexPan", out var before), Is.True);

            var value = new Vector4(2f, -3f, 0.5f, 7f);
            var substituted = captured.WithVector("_MainTexPan", value);

            Assert.That(substituted, Is.Not.SameAs(captured));
            Assert.That(substituted.TryGetVector("_MainTexPan", out var after), Is.True);
            Assert.That(after, Is.EqualTo(value));
            Assert.That(captured.TryGetVector("_MainTexPan", out var unchanged), Is.True);
            Assert.That(unchanged, Is.EqualTo(before),
                "substitution mutated the source evidence");
        }

        [Test]
        public void SubstitutingAnUnrequestedScalarThrows()
        {
            var captured = Substitutable();

            Assert.Throws<ArgumentException>(
                () => captured.WithScalar("_NotRequested", 1f));
        }

        [Test]
        public void SubstitutingAnUnrequestedColorThrows()
        {
            var captured = Substitutable();

            Assert.Throws<ArgumentException>(
                () => captured.WithColor("_NotRequested", Color.red));
        }

        [Test]
        public void SubstitutingAnUnrequestedVectorThrows()
        {
            var captured = Substitutable();

            Assert.Throws<ArgumentException>(
                () => captured.WithVector("_NotRequested", Vector4.one));
        }

        /// <summary>
        /// Requestedness is per category. A name requested as a vector is not a
        /// requested scalar, and substituting it as one would invent an evidence
        /// dimension rather than replace a captured fact.
        /// </summary>
        [Test]
        public void SubstitutingAcrossCategoriesIsADefectAndChangesNothing()
        {
            var captured = Substitutable();

            Assert.Throws<ArgumentException>(
                () => captured.WithScalar("_MainTexPan", 1f));
            Assert.Throws<ArgumentException>(
                () => captured.WithColor("_Cutoff", Color.red));
            Assert.Throws<ArgumentException>(
                () => captured.WithVector("_Color", Vector4.one));

            Assert.That(captured.TryGetVector("_MainTexPan", out var pan), Is.True);
            Assert.That(pan, Is.EqualTo(Vector4.zero));
            Assert.That(captured.TryGetScalar("_Cutoff", out var cutoff), Is.True);
            Assert.That(cutoff, Is.EqualTo(0.5f));
            Assert.That(captured.TryGetColor("_Color", out var color), Is.True);
            Assert.That(color, Is.EqualTo(Color.white));
        }

        /// <summary>
        /// THIS DOES NOT AUTHORIZE IGNORING AN ANIMATED ABSENT PROPERTY. It
        /// proves only that the substitution primitive preserves the captured
        /// fact that the material does not have the property; it never flips
        /// <c>HasValue</c> from false to true.
        /// <para>
        /// Task 5 observed that a bare <c>material._Cutoff</c> curve on a
        /// renderer whose material is <c>Unlit/Color</c> is genuinely sampled
        /// into a non-empty renderer-wide <c>MaterialPropertyBlock</c>, while
        /// the material itself still does not have <c>_Cutoff</c>. That does not
        /// establish whether the undeclared write reaches rendered pixels, so
        /// the design took the fail-closed branch: Task 19 MUST return
        /// <c>RendererAnalysisRefusal.AnimatedPropertyAbsentFromAdmittedMaterial</c>
        /// for a proof-relevant animated property that is absent from the
        /// admitted material, BEFORE any substitution is treated as authorized.
        /// Preserved absence here is not permission to ignore that binding.
        /// </para>
        /// </summary>
        [Test]
        public void ScalarSubstitutionDoesNotInventPresenceOnAMaterialWithoutTheProperty()
        {
            var captured = Capture(
                NewMaterial("Unlit/Color"),
                Request(scalars: new[] { "_Cutoff" }));
            Assert.That(captured.TryGetScalar("_Cutoff", out _), Is.False,
                "fixture precondition: the property must be absent");

            var substituted = captured.WithScalar("_Cutoff", 0.25f);

            Assert.That(substituted, Is.Not.SameAs(captured));
            Assert.That(substituted.TryGetScalar("_Cutoff", out var ignored), Is.False,
                "substitution invented a property the material does not have");
            Assert.That(ignored, Is.EqualTo(0f),
                "an absent entry retained the unauthorized substituted value");
            AssertNoLiveObjectsOrDelegates(substituted);
        }

        /// <summary>
        /// The same presence theorem for the other two categories. See
        /// <see cref="ScalarSubstitutionDoesNotInventPresenceOnAMaterialWithoutTheProperty"/>
        /// for why preserved absence is not authorization.
        /// </summary>
        [Test]
        public void ColorAndVectorSubstitutionDoNotInventPresence()
        {
            var captured = Capture(
                NewMaterial("Unlit/Color"),
                Request(
                    colors: new[] { "_EmissionColor" },
                    vectors: new[] { "_MainTexPan" }));
            Assert.That(captured.TryGetColor("_EmissionColor", out _), Is.False,
                "fixture precondition: the colour property must be absent");
            Assert.That(captured.TryGetVector("_MainTexPan", out _), Is.False,
                "fixture precondition: the vector property must be absent");

            var color = captured.WithColor("_EmissionColor", Color.red);
            Assert.That(color, Is.Not.SameAs(captured));
            Assert.That(color.TryGetColor("_EmissionColor", out var ignoredColor),
                Is.False,
                "substitution invented a colour the material does not have");
            Assert.That(ignoredColor, Is.EqualTo(default(Color)),
                "an absent entry retained the unauthorized substituted value");

            var vector = captured.WithVector("_MainTexPan", Vector4.one);
            Assert.That(vector, Is.Not.SameAs(captured));
            Assert.That(vector.TryGetVector("_MainTexPan", out var ignoredVector),
                Is.False,
                "substitution invented a vector the material does not have");
            Assert.That(ignoredVector, Is.EqualTo(default(Vector4)),
                "an absent entry retained the unauthorized substituted value");
        }

        [Test]
        public void SubstitutionPreservesEveryUnrelatedCapturedFact()
        {
            var captured = Substitutable();

            var substituted = captured.WithScalar("_Cutoff", 0.25f);

            Assert.That(substituted.HasShaderName, Is.EqualTo(captured.HasShaderName));
            Assert.That(substituted.ShaderName, Is.EqualTo(captured.ShaderName));
            Assert.That(
                substituted.HasActiveColorSpace,
                Is.EqualTo(captured.HasActiveColorSpace));
            Assert.That(
                substituted.ActiveColorSpace,
                Is.EqualTo(captured.ActiveColorSpace));
            Assert.That(substituted.HasProperty("_MainTex"), Is.True);
            Assert.That(substituted.TryGetScalar("_AlphaMod", out var sibling), Is.True);
            Assert.That(sibling, Is.EqualTo(0f));
            Assert.That(substituted.TryGetColor("_Color", out var color), Is.True);
            Assert.That(color, Is.EqualTo(Color.white));
            Assert.That(substituted.TryGetVector("_MainTexPan", out var pan), Is.True);
            Assert.That(pan, Is.EqualTo(Vector4.zero));
            Assert.That(substituted.TryGetTexture("_MainTex", out var texture), Is.True);
            Assert.That(captured.TryGetTexture("_MainTex", out var source), Is.True);
            Assert.That(texture.IsAssigned, Is.EqualTo(source.IsAssigned));
            Assert.That(texture.HasScaleOffset, Is.EqualTo(source.HasScaleOffset));
            Assert.That(texture.RequestedEvidence, Is.EqualTo(source.RequestedEvidence));
            Assert.That(texture.Scale, Is.EqualTo(source.Scale));
            Assert.That(texture.Offset, Is.EqualTo(source.Offset));
            Assert.That(texture.Texture, Is.SameAs(source.Texture));
            Assert.That(substituted.Textures, Is.SameAs(captured.Textures));
        }

        [Test]
        public void ChainedSubstitutionsAccumulateWithoutLosingEarlierOnes()
        {
            var captured = Substitutable();

            var first = captured.WithScalar("_Cutoff", 0.25f);
            var second = first.WithScalar("_AlphaMod", 0.75f);
            var third = second.WithColor("_Color", Color.black);

            Assert.That(third.TryGetScalar("_Cutoff", out var cutoff), Is.True);
            Assert.That(cutoff, Is.EqualTo(0.25f));
            Assert.That(third.TryGetScalar("_AlphaMod", out var mod), Is.True);
            Assert.That(mod, Is.EqualTo(0.75f));
            Assert.That(third.TryGetColor("_Color", out var color), Is.True);
            Assert.That(color, Is.EqualTo(Color.black));

            Assert.That(first.TryGetScalar("_AlphaMod", out var firstMod), Is.True);
            Assert.That(firstMod, Is.EqualTo(0f),
                "the first derivation was mutated by a later one");
            Assert.That(captured.TryGetScalar("_Cutoff", out var original), Is.True);
            Assert.That(original, Is.EqualTo(0.5f));
        }

        [Test]
        public void SubstitutingAnEqualValueStillDerivesANewEvidenceObject()
        {
            var captured = Substitutable();
            Assert.That(captured.TryGetScalar("_Cutoff", out var value), Is.True);

            var substituted = captured.WithScalar("_Cutoff", value);

            Assert.That(substituted, Is.Not.SameAs(captured));
            Assert.That(substituted.TryGetScalar("_Cutoff", out var after), Is.True);
            Assert.That(after, Is.EqualTo(value));
        }


        /// <summary>
        /// The primitive carries no admission policy. Finiteness, agreement
        /// between animated sources, and equality with the admitted material's
        /// serialized default are all decided upstream, so a non-finite value is
        /// written verbatim here rather than rejected.
        /// </summary>
        [Test]
        public void SubstitutionAppliesNoAdmissionPolicyToTheValue()
        {
            var captured = Substitutable();

            var substituted = captured.WithScalar("_Cutoff", float.NaN);

            Assert.That(substituted.TryGetScalar("_Cutoff", out var after), Is.True);
            Assert.That(float.IsNaN(after), Is.True);
        }

        [Test]
        public void RedFieldUnderAnActivePolicyMatchesTheInertRedField()
        {
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(217, 0, 0, 255);
            }
            var mask = TestTextureImport.WritePng(
                Path.Combine(TempFolder, "red_seam_mask.png"), 4, 4, pixels);
            var material = NewMaterial(LilToonCutoutConversionShader);
            _scope.Track(material);
            material.SetTexture("_AlphaMask", mask);

            var active = UnityMaterialEvidenceCapture.Capture(
                new[]
                {
                    new MaterialEvidenceCaptureInput(
                        material, LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
                },
                AlphaPolicyBounds.From(80, 2))[0];
            var inert = UnityMaterialEvidenceCapture.Capture(
                new[]
                {
                    new MaterialEvidenceCaptureInput(
                        material, LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
                },
                AlphaPolicyBounds.Inert)[0];

            Assert.That(active.TryGetTexture("_AlphaMask", out var activeBound),
                Is.True);
            Assert.That(inert.TryGetTexture("_AlphaMask", out var inertBound),
                Is.True);
            var activeRed = activeBound.Texture.RedChannel;
            var inertRed = inertBound.Texture.RedChannel;
            Assert.That(activeRed, Is.Not.Null);
            Assert.That(inertRed, Is.Not.Null);
            for (var mip = 0; mip < activeRed.Count; mip++)
            {
                Assert.That(activeRed.IsLevelWithoutEvidence(mip),
                    Is.EqualTo(inertRed.IsLevelWithoutEvidence(mip)));
                if (activeRed.IsLevelWithoutEvidence(mip))
                {
                    continue;
                }
                for (var y = 0; y < activeRed[mip].Height; y++)
                {
                    for (var x = 0; x < activeRed[mip].Width; x++)
                    {
                        Assert.That(activeRed[mip].GetAlpha(x, y),
                            Is.EqualTo(inertRed[mip].GetAlpha(x, y)),
                            $"mip {mip} texel ({x}, {y})");
                    }
                }
            }
            Assert.That(activeRed[0].GetAlpha(0, 0), Is.EqualTo(0),
                "Red 217 is below exactly one under the inert contract.");
        }

        [Test]
        public void RedFieldUnderAnActivePolicyStoresZeroBelowExactOne()
        {
            // Red 250 sRGB-decodes to about 0.96. That is above the active
            // opaque bound 0.8 but below exactly one. The capture pin must
            // store 0 here, while the old active-bounds capture stored 255.
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(250, 0, 0, 255);
            }
            var mask = TestTextureImport.WritePng(
                Path.Combine(TempFolder, "red_near_white_mask.png"), 4, 4,
                pixels);
            var material = NewMaterial(LilToonCutoutConversionShader);
            _scope.Track(material);
            material.SetTexture("_AlphaMask", mask);

            var active = UnityMaterialEvidenceCapture.Capture(
                new[]
                {
                    new MaterialEvidenceCaptureInput(
                        material, LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
                },
                AlphaPolicyBounds.From(80, 2))[0];

            Assert.That(active.TryGetTexture("_AlphaMask", out var activeBound),
                Is.True);
            var activeRed = activeBound.Texture.RedChannel;
            Assert.That(activeRed, Is.Not.Null);
            for (var y = 0; y < activeRed[0].Height; y++)
            {
                for (var x = 0; x < activeRed[0].Width; x++)
                {
                    Assert.That(activeRed[0].GetAlpha(x, y), Is.EqualTo(0),
                        $"mip 0 texel ({x}, {y})");
                }
            }
        }

        [Test]
        public void RedFieldContractAssertion_RefusesTheErasedFlagByte()
        {
            var level = new AlphaTextureData(
                1, 1, new byte[] { AlphaTextureData.ErasedFlag });
            var chain = new AlphaMipChain(new[] { level });

            Assert.Throws<InvalidOperationException>(
                () => UnityMaterialEvidenceCapture.AssertRedFieldContract(chain));
        }

        [Test]
        public void RedFieldContractAssertion_AcceptsBinaryVerdicts()
        {
            // Characterization, not RED: binary verdicts satisfy the contract.
            var level = new AlphaTextureData(2, 1, new byte[] { 255, 0 });
            var chain = new AlphaMipChain(new[] { level });

            Assert.DoesNotThrow(
                () => UnityMaterialEvidenceCapture.AssertRedFieldContract(chain));
        }

        [Test]
        public void AViolatingRedChainStillThrows()
        {
            // Characterization, not RED: the throw-on-violation contract must
            // survive the assertion's move to the route cache fill.
            Assert.Throws<InvalidOperationException>(
                () => CaptureViolatingRedChain());
        }

        /// <summary>
        /// Stands in for the route cache fill: builds the one red chain the
        /// real pipelines cannot produce (the inert bounds never erase, so no
        /// captured red level carries the erased flag) and hands it to the
        /// contract the fill enforces.
        /// </summary>
        private static void CaptureViolatingRedChain()
        {
            var level = new AlphaTextureData(
                1, 1, new byte[] { AlphaTextureData.ErasedFlag });
            var chain = new AlphaMipChain(new[] { level });
            UnityMaterialEvidenceCapture.AssertRedFieldContract(chain);
        }

        /// <summary>
        /// One schema-complete capture with every evidence category populated,
        /// so a substitution in one category can be shown not to disturb the
        /// others.
        /// </summary>
        private CapturedMaterialEvidence Substitutable()
        {
            return Capture(
                NewMaterial(PoiyomiFixtureShader),
                Request(
                    shaderName: true,
                    activeColorSpace: true,
                    presence: new[] { "_MainTex" },
                    scalars: new[] { "_Cutoff", "_AlphaMod" },
                    colors: new[] { "_Color" },
                    vectors: new[] { "_MainTexPan" },
                    textures: new[]
                    {
                        new TexturePropertyEvidenceRequest(
                            "_MainTex", TextureEvidenceKinds.ScaleOffset),
                    }));
        }

        private Material NewMaterial(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null, shaderName);
            var material = new Material(shader);
            _scope.Track(material);
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
            IEnumerable<TexturePropertyEvidenceRequest> textures = null,
            bool captureKeywords = false)
        {
            return new MaterialEvidenceRequest(
                shaderName,
                activeColorSpace,
                presence ?? Array.Empty<string>(),
                scalars ?? Array.Empty<string>(),
                colors ?? Array.Empty<string>(),
                vectors ?? Array.Empty<string>(),
                textures ?? Array.Empty<TexturePropertyEvidenceRequest>(),
                captureKeywords: captureKeywords);
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

        /// <summary>
        /// Imports a real asset texture whose storage format is outside the
        /// capture route's closed allowlist, through the same Standalone
        /// platform override the alpha-field tests use. The result has a
        /// resolvable identity: only the format gate refuses it.
        /// </summary>
        private static Texture2D ImportRefusedFormat(string path)
        {
            return TestTextureImport.WritePng(
                path, 4, 4, UniformPixels(255), importer =>
                {
                    importer.mipmapEnabled = false;
                    importer.isReadable = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    var settings = importer.GetPlatformTextureSettings("Standalone");
                    settings.overridden = true;
                    settings.format = TextureImporterFormat.RGBAHalf;
                    importer.SetPlatformTextureSettings(settings);
                });
        }

        private static Texture2D ImportAsymmetric(string path)
        {
            var pixels = UniformPixels(255);
            pixels[0] = new Color32(64, 32, 16, 128);
            pixels[pixels.Length - 1] = new Color32(64, 32, 16, 254);
            return TestTextureImport.WritePng(
                path, 4, 4, pixels, importer =>
                {
                    importer.mipmapEnabled = false;
                    importer.isReadable = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.wrapMode = UnityEngine.TextureWrapMode.Repeat;
                    importer.sRGBTexture = true;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                });
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
            Walk(root, new HashSet<object>(
                ReferenceEqualityComparer<object>.Instance));
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
    }
}
