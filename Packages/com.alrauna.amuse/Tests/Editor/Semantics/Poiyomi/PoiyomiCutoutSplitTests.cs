using System;
using System.IO;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// Cutout coverage split by cutoff (plan task 6). The vendor clips with
    /// <c>clip(alpha - _Cutoff)</c> in every pass without condition and then
    /// forces alpha to 1 in cutout mode (note 4.5), so a cutout triangle
    /// whose chain sits at or above the cutoff renders exactly one. The
    /// tests drive the real capture, the interpretation, and the classifier
    /// over the captured alpha field, mirroring the lilToon cutout seam: a
    /// uniform stand-in texture whose every mip level binarizes the same
    /// way, one corner triangle over it, and the captured chain as the
    /// resolver's field. A blending preset under the same chain keeps the
    /// exact-one rule, and a cutout preset without a usable cutoff refuses
    /// naming <c>_Cutoff</c>.
    /// </summary>
    public sealed class PoiyomiCutoutSplitTests : PoiyomiFixtureTestBase
    {
        private const string MainTextureProperty = "_MainTex";
        private const string CutoffProperty = "_Cutoff";
        private const string ModeProperty = "_Mode";

        // Texel values with exact cutoff relations. The capture decodes a
        // byte to byte/255f and compares it against the clamped cutoff with
        // a plain >=, so the byte at the cutoff survives the clip test.
        private const byte ByteBelowCutoff = 127;
        private const byte ByteAtCutoff = 128;
        private const byte ByteBetweenCutoffAndOne = 192;
        private const byte ByteAtOne = 255;

        // The at-the-cutoff cutoff value and the between-the-cutoff-and-one
        // chain value of the falsifier rows.
        private const float AtCutoff =
            (float)ByteAtCutoff / byte.MaxValue;

        // --- Request shape --------------------------------------------------

        /// <summary>
        /// The split route's declaration. The alpha request declares the
        /// cutoff on the main texture entry, so a cutout material's own
        /// capture predicate binarizes the alpha field by the cutoff value.
        /// The plain-clip variant carries the same schema, including the
        /// preset selector and the cutoff scalar, with no declaration: it is
        /// the predicate of every non-cutout preset, and the two must differ
        /// in the declaration alone. The Two Pass request adds the second
        /// family's own selector and follows the same split.
        /// </summary>
        [Test]
        public void AlphaEvidenceRequest_DeclaresTheCutoutSplitRoute()
        {
            var declaring = PoiyomiMaterialSemantics.AlphaEvidenceRequest;
            var plain = PoiyomiMaterialSemantics.PlainAlphaEvidenceRequest;

            Assert.That(
                declaring.ScalarProperties.Contains(ModeProperty), Is.True,
                "the preset selector is a captured fact of the split");
            Assert.That(
                plain.ScalarProperties.Contains(ModeProperty), Is.True,
                "the plain-clip predicate carries the same schema");
            Assert.That(
                declaring.ScalarProperties.Contains(CutoffProperty), Is.True,
                "the cutoff rides the capture as a theorem scalar");

            var declaringMain = declaring.TextureProperties.Single(
                texture => texture.PropertyName == MainTextureProperty);
            Assert.That(
                declaringMain.CutoffScalarProperty,
                Is.EqualTo(CutoffProperty),
                "the cutout request declares the cutoff on the main " +
                "texture entry");
            var plainMain = plain.TextureProperties.Single(
                texture => texture.PropertyName == MainTextureProperty);
            Assert.That(
                plainMain.CutoffScalarProperty, Is.Null,
                "the plain-clip predicate declares no cutoff, so the " +
                "capture keeps the exact-255 field");
            Assert.That(
                plainMain.Evidence, Is.EqualTo(declaringMain.Evidence),
                "the two variants differ in the declaration alone");

            var twoPass = PoiyomiMaterialSemantics.TwoPassAlphaEvidenceRequest;
            var twoPassPlain =
                PoiyomiMaterialSemantics.PlainTwoPassAlphaEvidenceRequest;
            Assert.That(
                twoPass.ScalarProperties.Contains("_ModeTwoPass"), Is.True,
                "the second family's own selector joins the Two Pass " +
                "request");
            Assert.That(
                twoPassPlain.ScalarProperties.Contains("_ModeTwoPass"),
                Is.True,
                "the Two Pass plain-clip predicate carries the same " +
                "schema");
            Assert.That(
                twoPass.TextureProperties.Single(
                    texture => texture.PropertyName == MainTextureProperty)
                    .CutoffScalarProperty,
                Is.EqualTo(CutoffProperty));
            Assert.That(
                twoPassPlain.TextureProperties.Single(
                    texture => texture.PropertyName == MainTextureProperty)
                    .CutoffScalarProperty,
                Is.Null);

            // The plain Toon request stays without the second family's
            // scalars, so a plain material keeps capturing without them.
            Assert.That(
                declaring.ScalarProperties.Contains("_AlphaForceOpaque2"),
                Is.False);
            Assert.That(
                declaring.ScalarProperties.Contains("_ModeTwoPass"), Is.False);
        }

        // --- Falsifier 1: at or above the cutoff ----------------------------

        /// <summary>
        /// The chain at the cutoff and at 1 proves in cutout mode. The
        /// vendor keeps a fragment whose alpha - cutoff is not negative and
        /// then writes alpha = 1 (note 4.5), so the whole domain of a
        /// uniform at-the-cutoff texture renders exactly one. A capture
        /// that never declares the cutoff keeps the exact-255 field, where
        /// the at-the-cutoff texels read non-opaque, so the triangle stays
        /// unproven and the at-the-cutoff row fails.
        /// </summary>
        [Test]
        public void CutoutPreset_ChainAtOrAboveCutoff_ProvesTriangle()
        {
            // No-op guard: the at-one chain proves under the exact field
            // too, so the at-the-cutoff proof below comes from the split
            // route and not from the harness.
            AssertProven(
                CutoutMaterial(UniformTexture("cutout_at_one", ByteAtOne)));

            AssertProven(
                CutoutMaterial(
                    UniformTexture("cutout_at_cutoff", ByteAtCutoff)));
        }

        // --- Below-cutoff domain ---------------------------------------------

        /// <summary>
        /// A triangle whose whole domain sits below the cutoff never moves:
        /// the shader discards it (note 4.5), so it can never be called
        /// opaque and the split route marks it must-stay-transparent. A
        /// wrong implementation that reads the cutout preset as a blanket
        /// alpha forcing proves the discarded triangle here.
        /// </summary>
        [Test]
        public void CutoutPreset_ChainBelowCutoff_NeverMoves()
        {
            AssertTransparent(
                CutoutMaterial(
                    UniformTexture("cutout_below_cutoff", ByteBelowCutoff)));

            // No-op guard: an at-one chain under the same preset still
            // proves, so the transparent verdict above comes from the
            // cutoff relation and not from the preset alone.
            AssertProven(
                CutoutMaterial(
                    UniformTexture("cutout_guard_at_one", ByteAtOne)));
        }

        // --- Falsifier 2: same chain, blending preset -----------------------

        /// <summary>
        /// The same chain under a blending preset stays subject to the
        /// exact-one rule. A fade preset renders the chain value itself, so
        /// a chain between the cutoff and one must never prove, even though
        /// the binarized field of a leaked split route would call it
        /// opaque. The row drives both sides: with the production
        /// plain-clip predicate the exact-one rule holds on the exact
        /// field, and with the declaring predicate wrongly applied to the
        /// blending material the interpretation refuses naming
        /// <c>_Cutoff</c> instead of proving over binarized bytes.
        /// </summary>
        [Test]
        public void BlendingPreset_SameChain_StaysSubjectToExactOneRule()
        {
            var material = FadeMaterial(
                UniformTexture(
                    "blend_between_cutoff_and_one",
                    ByteBetweenCutoffAndOne));

            // Production predicate: the plain-clip variant keeps the exact
            // field, so the sub-one chain never proves.
            var evidence = Capture(material, productionPredicate: true);
            var alpha = PoiyomiMaterialSemantics.InterpretVerifiedAlpha(
                evidence);
            Assert.That(
                alpha.IsComplete, Is.True,
                "the fade material's chain equation stays provable");
            var resolution = AlphaSemanticsResolver.Resolve(
                alpha, ProviderFor(evidence), 0);
            Assert.That(resolution.IsResolved, Is.True);
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.Not.EqualTo(TriangleAlphaOutcome.ProvenOpaque),
                "a chain between the cutoff and one never proves under a " +
                "blending preset");

            // The leak falsifier: the declaring predicate applied to a
            // blending material binarizes the field, and the interpretation
            // must refuse instead of reading those bytes as exact ones.
            var leaked = Capture(material, productionPredicate: false);
            var leakedResult =
                PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                    material, ColorSpace.Linear, leaked);
            Assert.That(
                leakedResult.Semantics.Alpha.IsComplete, Is.False,
                "a cutoff-binarized field answers no exact-one claim");
            var match = leakedResult.Diagnostics.FirstOrDefault(
                d => d.Output == PoiyomiSemanticOutput.Alpha &&
                    d.Code == PoiyomiSemanticDiagnosticCode
                        .UnsupportedFeature &&
                    d.Detail == CutoffProperty);
            Assert.That(
                match, Is.Not.Null,
                "the leak refusal names _Cutoff. Diagnostics: " +
                Describe(leakedResult));
        }

        // --- Fail-closed cutoff ----------------------------------------------

        /// <summary>
        /// A missing or non-finite cutoff on a cutout preset refuses naming
        /// <c>_Cutoff</c>. The split route's premise is the declared
        /// cutoff: without a usable value the capture cannot binarize by
        /// it and the proof cannot know which domain survives the clip, so
        /// a silent proof would be a guess. The guard completes a cutout
        /// material with a present, valid cutoff, so the refusals below
        /// come from the cutoff alone.
        /// </summary>
        [Test]
        public void CutoutPreset_CutoffMissingOrNonFinite_RefusesNamingCutoff()
        {
            // No-op guard: a present, valid cutoff admits the cutout
            // material's claim.
            Assert.That(
                PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                    CutoutMaterial(
                        UniformTexture("cutout_valid_cutoff", ByteAtOne),
                        0.5f),
                    ColorSpace.Linear)
                    .Semantics.Alpha.IsComplete,
                Is.True,
                "a cutout material with a valid cutoff proves");

            var nonFinite = CutoutMaterial(
                UniformTexture("cutout_nan_cutoff", ByteAtOne), 0.5f);
            nonFinite.SetFloat(CutoffProperty, float.NaN);
            AssertCutoffRefusal(nonFinite);

            AssertCutoffRefusal(CutoffMissingMaterial());
        }

        // --- Helpers ----------------------------------------------------------

        private Material CutoutMaterial(Texture2D texture)
        {
            var material = NewFixtureMaterial();
            material.SetTexture(MainTextureProperty, texture);
            material.SetFloat(ModeProperty, 1f);
            material.SetFloat(CutoffProperty, AtCutoff);
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            return material;
        }

        private Material CutoutMaterial(Texture2D texture, float cutoff)
        {
            var material = CutoutMaterial(texture);
            material.SetFloat(CutoffProperty, cutoff);
            return material;
        }

        private Material FadeMaterial(Texture2D texture)
        {
            var material = NewFixtureMaterial();
            material.SetTexture(MainTextureProperty, texture);
            material.SetFloat(ModeProperty, 2f);
            material.SetFloat(CutoffProperty, AtCutoff);
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            material.SetFloat("_SrcBlend", 5f);
            material.SetFloat("_DstBlend", 10f);
            return material;
        }

        /// <summary>
        /// A material whose stand-in declares the preset selector but no
        /// cutoff property at all, so the captured evidence carries no
        /// cutoff value. The fixture shader source is copied with the
        /// cutoff declaration removed and a test-only shader name, and the
        /// copy lives in the shared temp folder the base tears down.
        /// </summary>
        private Material CutoffMissingMaterial()
        {
            var source = AssetDatabase.GetAssetPath(
                Shader.Find(FixtureShaderName));
            Assert.That(
                source, Is.Not.Empty,
                "fixture precondition: the stand-in shader asset resolves");
            var text = File.ReadAllText(source);
            var renamed = text.Replace(
                "Hidden/Alrauna/AmuseTests/PoiyomiSemanticTest",
                "Hidden/Alrauna/AmuseTests/PoiyomiCutoutNoCutoff");
            var cutoffLine = text
                .Split('\n')
                .First(line => line.TrimStart().StartsWith(
                    "_Cutoff (", StringComparison.Ordinal));
            var withoutCutoff = renamed.Replace(cutoffLine, string.Empty);

            var path = TempFolder + "/PoiyomiCutoutNoCutoff.shader";
            File.WriteAllText(path, withoutCutoff);
            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(
                shader, Is.Not.Null,
                "the cutoff-less stand-in variant must import");

            // The shader is an imported asset, so it stays out of the
            // transient destroy list: the base teardown's folder deletion
            // removes it with the rest of the temp folder.
            var material = Track(new Material(shader));
            material.SetTexture(
                MainTextureProperty,
                UniformTexture("cutout_missing_cutoff", ByteAtOne));
            material.SetFloat(ModeProperty, 1f);
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            return material;
        }

        /// <summary>
        /// An 8x8 mipmapped uniform-alpha stand-in texture. Uniformity
        /// keeps every mip level on the same side of the cutoff, so the
        /// resolver's whole-chain conjunction sees one verdict. The import
        /// enables streaming mipmaps, so the capture takes the direct
        /// source-image route: that route binarizes each texel by the
        /// declared cutoff, which is exactly the split route this suite
        /// observes.
        /// </summary>
        private Texture2D UniformTexture(string name, byte alpha)
        {
            var path = TempFolder + "/" + name + ".png";
            var staging = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            var pixels = new Color32[64];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(255, 255, 255, alpha);
            }

            staging.SetPixels32(pixels);
            staging.Apply();
            File.WriteAllBytes(path, staging.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(staging);

            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(
                loaded, Is.Not.Null,
                $"Imported texture '{path}' must load.");
            return loaded;
        }

        private static CapturedMaterialEvidence Capture(
            Material material,
            bool productionPredicate)
        {
            var predicate = productionPredicate
                ? PoiyomiMaterialSemantics.AlphaPredicateRequestFor(
                    material, false)
                : PoiyomiMaterialSemantics.AlphaEvidenceRequest;
            return UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material,
                    PoiyomiMaterialSemantics.AlphaEvidenceRequest,
                    predicate),
            })[0];
        }

        /// <summary>
        /// A field provider handing back the captured alpha chain for its
        /// own source identity, so the classifier sees the bytes the
        /// capture actually stored.
        /// </summary>
        private static AlphaFieldProvider ProviderFor(
            CapturedMaterialEvidence evidence)
        {
            Assert.That(
                evidence.TryGetTexture(
                    MainTextureProperty, out var assignment),
                Is.True,
                "the cutout split request captures the main texture");
            Assert.That(
                assignment.IsAssigned &&
                    assignment.Texture != null &&
                    assignment.Texture.HasSourceIdentity &&
                    assignment.Texture.HasAlphaChannel,
                Is.True,
                "resolver-seam tests key the captured chain on its " +
                "source identity");
            var expected = assignment.Texture.SourceIdentity;
            var chain = assignment.Texture.AlphaChannel;
            return (TextureSourceId source, TextureChannel channel,
                out AlphaMipChain result) =>
            {
                if (channel == TextureChannel.Alpha &&
                    source.Equals(expected))
                {
                    result = chain;
                    return true;
                }

                result = null;
                return false;
            };
        }

        /// <summary>Nondegenerate lower-left corner triangle.</summary>
        private static TriangleAlphaInput CornerTriangle()
        {
            return TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.05f, 0.05f),
                new Vector2(0.45f, 0.05f),
                new Vector2(0.05f, 0.45f));
        }

        private static void AssertProven(Material material)
        {
            Assert.That(
                ClassifyCorner(material),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque),
                "the at-or-above-cutoff domain proves in cutout mode");
        }

        private static void AssertTransparent(Material material)
        {
            Assert.That(
                ClassifyCorner(material),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent),
                "a domain below the cutoff never moves");
        }

        private static TriangleAlphaOutcome ClassifyCorner(
            Material material)
        {
            var evidence = Capture(material, productionPredicate: true);
            var alpha = PoiyomiMaterialSemantics.InterpretVerifiedAlpha(
                evidence);
            Assert.That(
                alpha.IsComplete, Is.True,
                "the cutout equation completes. Diagnostics: " +
                Describe(PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                    material, ColorSpace.Linear, evidence)));
            var resolution = AlphaSemanticsResolver.Resolve(
                alpha, ProviderFor(evidence), 0);
            Assert.That(resolution.IsResolved, Is.True);
            return resolution.Classify(CornerTriangle());
        }

        private static void AssertCutoffRefusal(Material material)
        {
            var evidence = Capture(material, productionPredicate: true);
            var result =
                PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                    material, ColorSpace.Linear, evidence);
            Assert.That(
                result.Semantics.Alpha.IsComplete, Is.False,
                "a cutout material without a usable cutoff stays unknown");
            var match = result.Diagnostics.FirstOrDefault(
                d => d.Output == PoiyomiSemanticOutput.Alpha &&
                    d.Code == PoiyomiSemanticDiagnosticCode
                        .UnsupportedFeature &&
                    d.Detail == CutoffProperty);
            Assert.That(
                match, Is.Not.Null,
                "the refusal names _Cutoff. Diagnostics: " +
                Describe(result));
        }

        private static string Describe(PoiyomiSemanticResult result)
        {
            if (result.Diagnostics.Count == 0)
            {
                return "(none)";
            }

            return string.Join(
                " ",
                result.Diagnostics.Select(
                    d => $"[{d.Output}/{d.Code}:{d.Detail}]"));
        }
    }
}
