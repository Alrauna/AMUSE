using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Runtime;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The real-optimizer reproduction: a VRChat avatar with the AMUSE
    /// component, with and without Avatar Optimizer's Trace and Optimize
    /// at controlled configurations, through the full production pipeline
    /// with the attested vendor lilToon transparent and cutout shaders.
    /// <para>
    /// The soundness contract under test: after the build, every triangle
    /// that ended up on a generated opaque material samples only fully
    /// opaque texels of its source texture. A triangle over a fully
    /// transparent or partially transparent texel must stay on its
    /// original material - or, on a cutout material whose own cutoff
    /// admits it, convert only under that material's own predicate. This
    /// fails on any implementation that proves polygons from evidence
    /// that does not exist, including a capture whose texture field was
    /// binarized under a sibling material's cutoff.
    /// </para>
    /// <para>
    /// Every soundness test reads the built avatar through one oracle:
    /// each authored triangle is registered as an order-independent
    /// avatar-root-space position triple with an expectation, and the
    /// built triangles must match the authored set exactly once each.
    /// UV coordinates never carry triangle identity.
    /// </para>
    /// <para>
    /// Both optimizer packages are discovered at runtime and the test
    /// ignores loudly when either is absent, so the public suite stays
    /// green on a fresh clone. The required integration profile reports
    /// those ignores as failures of that profile.
    /// </para>
    /// </summary>
    public sealed class AaoMergedConsumptionTests
    {
        private const string TempFolder = "Assets/AmuseTests_AaoMerge";
        private const string LilToonTransparentShaderName =
            "Hidden/lilToonTransparent";
        private const string LilToonCutoutShaderName =
            "Hidden/lilToonCutout";
        private const string TraceAndOptimizeTypeName =
            "Anatawa12.AvatarOptimizer.TraceAndOptimize,"
            + "com.anatawa12.avatar-optimizer.runtime";

        /// <summary>The alpha bands of the fixture texture, in UV x
        /// order. The band boundaries sit at exact quarter fractions so
        /// every resident mip level (the default cap is four) keeps every
        /// texel inside one band.</summary>
        private enum Band
        {
            /// <summary>Alpha 0: never provably opaque.</summary>
            Transparent,

            /// <summary>Alpha 128: the partial band. A transparent
            /// material must keep it; a cutout material binarizes it by
            /// its own declared cutoff.</summary>
            Partial,

            /// <summary>Alpha 255: the positive conversion control.</summary>
            Opaque,
        }

        private enum TriangleExpectation
        {
            StaysTransparent,
            ConvertsToOpaque,
        }

        private sealed class AuthoredTriangle
        {
            internal string Key;
            internal TriangleExpectation Expectation;
            // Count of still-unmatched output triangles for this key.
            // Identical position triples are legitimate - two renderers
            // may author the same shape - so matching is a multiset, and
            // the 32-bit-index fixture keeps the per-key lookup O(1).
            internal int Remaining;
        }

        private readonly List<UnityEngine.Object> tracked =
            new List<UnityEngine.Object>();
        private readonly Dictionary<string, AuthoredTriangle> authored =
            new Dictionary<string, AuthoredTriangle>();

        private Texture2D quadrantTexture;

        // Band-interior UV triplets with margins from every band boundary
        // large enough that the widest bilinear footprint at the smallest
        // resident mip (8x8) still samples one band only.
        private static readonly Vector2[] TransparentBandUv =
        {
            new Vector2(0.02f, 0.25f),
            new Vector2(0.12f, 0.25f),
            new Vector2(0.02f, 0.45f),
        };

        private static readonly Vector2[] PartialBandUv =
        {
            new Vector2(0.28f, 0.25f),
            new Vector2(0.44f, 0.25f),
            new Vector2(0.28f, 0.45f),
        };

        private static readonly Vector2[] OpaqueBandUv =
        {
            new Vector2(0.56f, 0.25f),
            new Vector2(0.94f, 0.25f),
            new Vector2(0.56f, 0.45f),
        };

        private T Track<T>(T asset) where T : UnityEngine.Object
        {
            tracked.Add(asset);
            return asset;
        }

        [TearDown]
        public void TearDown()
        {
            OptimizerMergeObservation.Reset();
            foreach (var asset in tracked)
            {
                if (asset == null) continue;
                if (AssetDatabase.Contains(asset))
                {
                    // Persistent assets go with the temp folder deletion
                    // below; DestroyImmediate refuses them without the
                    // asset flag and logs an error per object.
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(asset);
            }

            tracked.Clear();
            authored.Clear();
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        // --- capture probes ---------------------------------------------------

        /// <summary>
        /// The capture-level probe for the reproduction environment: the
        /// production capture must produce a chain for the fixture texture.
        /// A refusal here names exactly why the merged-renderer
        /// reproduction cannot prove polygons, before any pipeline machinery
        /// is involved.
        /// </summary>
        [Test]
        public void ProductionCaptureProducesAChainForTheFixtureTexture()
        {
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");
            quadrantTexture = Track(ImportQuadrantAlphaTexture());
            var captured = UnityAlphaFieldEvidence.TryCapture(
                quadrantTexture,
                TextureChannel.Alpha,
                1.0f,
                AlphaPolicyBounds.Inert,
                out var source,
                out var chain,
                out var refusal);

            Assert.That(
                captured,
                Is.True,
                "the production capture refused the fixture texture: "
                + refusal + " — the reproduction's soundness contract cannot"
                + " be exercised without a chain");
            Assert.That(chain, Is.Not.Null);
            Assert.That(chain.Count, Is.GreaterThanOrEqualTo(1));
        }

        /// <summary>
        /// Walks the production material path for the fixture's real
        /// lilToon material one stage at a time and names the first stage
        /// that refuses: family selection, attested closed capture, then
        /// alpha analysis. Each checkpoint prints its named outcome, so a
        /// zero-candidate build names its blocker instead of failing
        /// silently upstream of every refusal bucket.
        /// </summary>
        [Test]
        public void ProductionSemanticsResolveTheFixtureMaterial()
        {
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");
            quadrantTexture = Track(ImportQuadrantAlphaTexture());
            var shader = Shader.Find(LilToonTransparentShaderName);
            Assume.That(shader, Is.Not.Null, "lilToon not installed");
            var material = Track(NewTransparentMaterial(shader, quadrantTexture));

            var selected = UnityMaterialSemantics
                .TrySelectAlphaMaterialRequests(
                    material,
                    out var family,
                    out var relevanceRequest,
                    out var captureRequest);
            Assert.That(
                selected,
                Is.True,
                "family selection refused the fixture material; family="
                + family);

            var attested = UnityMaterialSemantics
                .TryCaptureClosedAlphaMaterials(
                    new[] { material },
                    new[] { family },
                    captureRequest,
                    AlphaPolicyBounds.Inert,
                    out var capturedList);
            Assert.That(
                attested,
                Is.True,
                "attested closed capture refused the fixture material;"
                + " family=" + family);

            var semantics =
                UnityMaterialSemantics.AnalyzeAlphaMaterial(capturedList[0]);
            Assert.That(
                semantics.Alpha.IsComplete,
                Is.True,
                "alpha analysis did not resolve for the fixture material;"
                + " family=" + family
                + " alphaKind=" + semantics.Alpha.GetCompleteValue().Kind);
        }

        /// <summary>
        /// Reproduces the barrier's evidence capture for the fixture
        /// renderer outside the NDMF build and names what the capture
        /// admitted. An empty admitted set explains a zero-candidate build
        /// with zero refusal buckets: every slot then resolves without
        /// texture evidence and classifies all-unknown silently.
        /// </summary>
        [Test]
        public void EvidenceCaptureAdmitsTheFixtureMaterial()
        {
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");
            quadrantTexture = Track(ImportQuadrantAlphaTexture());
            var shader = Shader.Find(LilToonTransparentShaderName);
            Assume.That(shader, Is.Not.Null, "lilToon not installed");

            var root = new GameObject("AMUSE evidence probe root");
            Track(root);
            FixtureAvatarIdentity.AttachVrcDescriptor(root);
            AttachAnimationFixture(root, "evidence-probe");
            var material = Track(
                NewTransparentMaterial(shader, quadrantTexture));
            CreateSkinnedRenderer(
                root, "AMUSE evidence probe renderer",
                "AMUSE evidence probe mesh",
                new[] { material },
                new[] { Band.Partial, Band.Opaque },
                new[] { 0, 0 },
                firstTriangleIndex: 0);
            var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
            var rendererPath = AnimationUtility.CalculateTransformPath(
                renderer.transform, root.transform);

            var bindings = VRChatPlatformAnimatorBindings.Instance;
            var graph = CommittedControllerGraph.Enumerate(root, bindings);
            var evidence = UnityAnimationEvidenceCapture.Capture(
                rendererPath,
                renderer.sharedMaterials,
                graph,
                bindings,
                AlphaPolicyBounds.Inert,
                out var admittedLiveMaterials);

            Assert.That(
                evidence.AdmittedMaterials,
                Is.Not.Empty,
                "the animation evidence capture admitted none of the"
                + " fixture's materials, so every slot resolves without"
                + " evidence and classifies all-unknown; graphRefusal="
                + graph.Refusal
                + " closureFailure=" + evidence.ClosureFailure
                + " isClosed=" + evidence.IsClosed
                + " admittedLive=" + admittedLiveMaterials.Count);

            var fields = UnityRendererAlphaAnalysis.GatherAlphaFields(
                evidence.AdmittedMaterials, 4, 128);
            var semantics =
                UnityMaterialSemantics.AnalyzeAlphaMaterial(
                    evidence.AdmittedMaterials[0]);
            AlphaFieldProvider provider = (
                TextureSourceId source,
                TextureChannel channel,
                out AlphaMipChain chain) =>
                fields.TryGetValue((source, channel), out chain);
            var resolution = AlphaSemanticsResolver.Resolve(
                semantics.Alpha, provider, 0);
            Assert.That(
                resolution.Failure,
                Is.EqualTo(AlphaResolutionFailure.None),
                "the in-build resolution pipeline failed: fields="
                + fields.Count
                + " alphaComplete=" + semantics.Alpha.IsComplete
                + " alphaKind=" + (semantics.Alpha.IsComplete
                    ? semantics.Alpha.GetCompleteValue().Kind.ToString()
                    : "<unknown>"));
        }

        // --- soundness: no optimizer component --------------------------------

        /// <summary>
        /// The harness control: the real production pipeline with no
        /// optimizer component must convert only the positive opaque
        /// control triangle and must keep both the transparent-band and
        /// partial-band triangles on their original materials. The build
        /// must report success. This is the green baseline the mixed
        /// predicate cases and the real merge cases are measured against.
        /// </summary>
        [Test]
        public void PipelineWithoutOptimizerKeepsNonOpaquePolygonsOnTheirOriginalMaterials()
        {
            RequireLilToonEnvironment(out var transparentShader, out _);
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

            var root = new GameObject("AMUSE soundness control");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                AttachAnimationFixture(root, "control");
                var texture = Track(ImportBandedAlphaTexture("banded_control"));
                var material = Track(
                    NewTransparentMaterial(transparentShader, texture));

                CreateSkinnedRenderer(
                    root, "AMUSE control renderer A", "AMUSE control mesh A",
                    new[] { material },
                    new[] { Band.Transparent, Band.Partial, Band.Opaque },
                    new[] { 0, 0, 0 },
                    firstTriangleIndex: 0);
                CreateSkinnedRenderer(
                    root, "AMUSE control renderer B", "AMUSE control mesh B",
                    new[] { material },
                    new[] { Band.Transparent, Band.Partial, Band.Opaque },
                    new[] { 0, 0, 0 },
                    firstTriangleIndex: 3);

                var context = AvatarProcessor.ProcessAvatar(
                    root, AmbientPlatform.DefaultPlatform);
                AssertBuiltTrianglesMatchAuthoring(context, root);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The request-union boundary: one renderer carrying a transparent
        /// slot and a cutout slot must keep every slot's classification
        /// under that slot's own predicate. The transparent slot's
        /// partial-band triangle must stay even though the cutout slot's
        /// own partial-band triangle legitimately converts under its
        /// cutoff. A capture that unions the slots' requests - or reuses a
        /// field binarized under the sibling's cutoff - proves the
        /// transparent partial triangle opaque and moves it, which this
        /// test fails on. Slot order must not matter.
        /// </summary>
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void MixedSlotRendererKeepsTransparentPredicateWhenCutoutSharesThePipeline(
            bool cutoutFirst, bool sharedTexture)
        {
            RequireLilToonEnvironment(out var transparentShader, out var cutoutShader);
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

            var root = new GameObject("AMUSE mixed slot "
                + (cutoutFirst ? "cutout-first" : "transparent-first")
                + (sharedTexture ? " shared" : " distinct"));
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                AttachAnimationFixture(root, "mixed-" + (sharedTexture ? "s" : "d"));

                var transparentTexture =
                    Track(ImportBandedAlphaTexture("banded_transparent"));
                var cutoutTexture = sharedTexture
                    ? transparentTexture
                    : Track(ImportBandedAlphaTexture("banded_cutout"));
                var transparent = Track(NewTransparentMaterial(
                    transparentShader, transparentTexture));
                var cutout = Track(NewCutoutMaterial(
                    cutoutShader, cutoutTexture, 0.25f));

                // The transparent slot carries the defect witness: a
                // partial-band triangle that must stay, plus the positive
                // opaque control that must convert. The cutout slot
                // carries its own partial-band triangle, which converts
                // under the cutout's own 0.25 cutoff.
                var transparentSubmesh = cutoutFirst ? 1 : 0;
                var cutoutSubmesh = cutoutFirst ? 0 : 1;
                var materials = cutoutFirst
                    ? new[] { cutout, transparent }
                    : new[] { transparent, cutout };

                CreateSkinnedRenderer(
                    root, "AMUSE mixed slot renderer", "AMUSE mixed slot mesh",
                    materials,
                    new Band[] { Band.Partial, Band.Opaque, Band.Partial },
                    new[]
                    {
                        transparentSubmesh, transparentSubmesh, cutoutSubmesh,
                    },
                    firstTriangleIndex: 0);

                var context = AvatarProcessor.ProcessAvatar(
                    root, AmbientPlatform.DefaultPlatform);
                AssertBuiltTrianglesMatchAuthoring(context, root);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The green boundary of the predicate space: a cutout alternative
        /// whose cutoff (0.75) rejects the partial band must keep it even
        /// when it shares its texture with a transparent slot. Every
        /// admitted material state of this fixture - either slot captured
        /// first, either field consulted - keeps the partial triangles,
        /// so a failure names a defect stronger than union drift.
        /// </summary>
        [Test]
        public void MaterialAlternativeWithHigherCutoffStaysWithinItsOwnPredicate()
        {
            RequireLilToonEnvironment(out var transparentShader, out var cutoutShader);
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

            var root = new GameObject("AMUSE cutout alternative");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                AttachAnimationFixture(root, "alternative");
                var texture = Track(ImportBandedAlphaTexture("banded_alternative"));
                var transparent = Track(NewTransparentMaterial(
                    transparentShader, texture));
                var cutout = Track(NewCutoutMaterial(cutoutShader, texture, 0.75f));

                CreateSkinnedRenderer(
                    root, "AMUSE alternative renderer", "AMUSE alternative mesh",
                    new[] { transparent, cutout },
                    new[] { Band.Partial, Band.Opaque, Band.Partial },
                    new[] { 0, 0, 1 },
                    firstTriangleIndex: 0);

                var context = AvatarProcessor.ProcessAvatar(
                    root, AmbientPlatform.DefaultPlatform);
                AssertBuiltTrianglesMatchAuthoring(context, root);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The shared-source boundary across renderers: two renderers
        /// referencing one texture instance - a transparent slot on one
        /// and a cutout slot on the other - must not let the cutout's
        /// binarized field leak into the transparent renderer's evidence,
        /// in either processing order.
        /// </summary>
        [Test]
        public void SharedTextureAcrossRenderersKeepsTransparentPredicate()
        {
            RequireLilToonEnvironment(out var transparentShader, out var cutoutShader);
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

            var root = new GameObject("AMUSE shared texture renderers");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                AttachAnimationFixture(root, "shared-renderers");
                var texture = Track(ImportBandedAlphaTexture("banded_shared"));
                var transparent = Track(NewTransparentMaterial(
                    transparentShader, texture));
                var cutout = Track(NewCutoutMaterial(cutoutShader, texture, 0.25f));

                CreateSkinnedRenderer(
                    root, "AMUSE shared transparent renderer",
                    "AMUSE shared transparent mesh",
                    new[] { transparent },
                    new[] { Band.Partial, Band.Opaque },
                    new[] { 0, 0 },
                    firstTriangleIndex: 0);
                CreateSkinnedRenderer(
                    root, "AMUSE shared cutout renderer",
                    "AMUSE shared cutout mesh",
                    new[] { cutout },
                    new[] { Band.Partial },
                    new[] { 0 },
                    firstTriangleIndex: 2);

                var context = AvatarProcessor.ProcessAvatar(
                    root, AmbientPlatform.DefaultPlatform);
                AssertBuiltTrianglesMatchAuthoring(context, root);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // --- soundness: real Avatar Optimizer merge ---------------------------

        /// <summary>
        /// The real merge case: Avatar Optimizer with Merge Skinned Mesh
        /// enabled must combine the source renderers into one renderer
        /// before AMUSE runs, that merged renderer must carry both source
        /// material modes, and the soundness oracle must still hold on
        /// the merged avatar. A capture that proves the transparent
        /// slot's partial triangle under the cutout slot's cutoff fails
        /// here at the integration level.
        /// </summary>
        [Test]
        public void AaoMergeCombinesRenderersAndPreservesSoundness()
        {
            RequireIntegrationEnvironment(
                out var traceAndOptimizeType,
                out var transparentShader,
                out var cutoutShader);
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

            var root = new GameObject("AMUSE AAO merged consumption");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                var traceAndOptimize =
                    root.AddComponent(traceAndOptimizeType);
                ConfigureTraceAndOptimize(
                    traceAndOptimize, mergeSkinnedMesh: true,
                    optimizeTexture: false);
                AttachAnimationFixture(root, "merge");
                var texture = Track(ImportBandedAlphaTexture("banded_merge"));
                var transparent = Track(NewTransparentMaterial(
                    transparentShader, texture));
                var cutout = Track(NewCutoutMaterial(cutoutShader, texture, 0.25f));

                CreateSkinnedRenderer(
                    root, "AMUSE merge transparent renderer",
                    "AMUSE merge transparent mesh",
                    new[] { transparent },
                    new[] { Band.Transparent, Band.Partial, Band.Opaque },
                    new[] { 0, 0, 0 },
                    firstTriangleIndex: 0);
                CreateSkinnedRenderer(
                    root, "AMUSE merge cutout renderer",
                    "AMUSE merge cutout mesh",
                    new[] { cutout },
                    new[] { Band.Partial },
                    new[] { 0 },
                    firstTriangleIndex: 3);

                OptimizerMergeObservation.Reset();
                OptimizerMergeObservation.Enabled = true;
                try
                {
                    var context = AvatarProcessor.ProcessAvatar(
                        root, AmbientPlatform.DefaultPlatform);

                    AssertMergedRendererCarriedBothSourcesPreAmuse();
                    AssertBuiltTrianglesMatchAuthoring(context, root);
                }
                finally
                {
                    OptimizerMergeObservation.Reset();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The real texture case: with Merge Skinned Mesh and Optimize
        /// Texture both enabled, Avatar Optimizer must actually pack the
        /// fixture textures - the output slots must reference a different
        /// generated texture at or above the capture's minimum size. On
        /// this vendor version the packed atlas is not classifiable by
        /// the shipped capture, so the pinned AMUSE-side contract is the
        /// conservative one: the renderer is refused by name, nothing is
        /// proven, and every triangle - the positive control included -
        /// stays on its original material. A run where the packing never
        /// happens fails here instead of silently exercising no
        /// transformation.
        /// </summary>
        [Test]
        public void AaoTexturePackingTakesTheGeneratedRouteAndRefusesConservatively()
        {
            RequireIntegrationEnvironment(
                out var traceAndOptimizeType,
                out var transparentShader,
                out _);
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

            var root = new GameObject("AMUSE AAO texture packing");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                var traceAndOptimize =
                    root.AddComponent(traceAndOptimizeType);
                ConfigureTraceAndOptimize(
                    traceAndOptimize, mergeSkinnedMesh: true,
                    optimizeTexture: true);
                AttachAnimationFixture(root, "packing");
                var textureA =
                    Track(ImportBandedAlphaTexture("banded_pack_a", 512));
                var textureB =
                    Track(ImportBandedAlphaTexture("banded_pack_b", 512));
                var materialA = Track(NewTransparentMaterial(
                    transparentShader, textureA));
                var materialB = Track(NewTransparentMaterial(
                    transparentShader, textureB));

                CreateSkinnedRenderer(
                    root, "AMUSE packing renderer A", "AMUSE packing mesh A",
                    new[] { materialA },
                    new[] { Band.Transparent, Band.Partial, Band.Opaque },
                    new[] { 0, 0, 0 },
                    firstTriangleIndex: 0);
                CreateSkinnedRenderer(
                    root, "AMUSE packing renderer B", "AMUSE packing mesh B",
                    new[] { materialB },
                    new[] { Band.Transparent, Band.Partial, Band.Opaque },
                    new[] { 0, 0, 0 },
                    firstTriangleIndex: 3);

                OptimizerMergeObservation.Reset();
                OptimizerMergeObservation.Enabled = true;
                try
                {
                    var context = AvatarProcessor.ProcessAvatar(
                        root, AmbientPlatform.DefaultPlatform);

                    AssertPackingReplacedTheFixtureTextures(
                        root, new[] { textureA, textureB });
                    AssertBuiltTrianglesMatchAuthoring(context, root,
                        conservativeRefusalAllowed: true);
                }
                finally
                {
                    OptimizerMergeObservation.Reset();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The component-information contract observed on the real
        /// boundary: after Avatar Optimizer's passes and again after
        /// AMUSE's own sequence, the AMUSE component must still be on
        /// the avatar root, because the platform-finish gate reads its
        /// serialized state there. The soundness oracle additionally
        /// proves the passes really consumed that state - conversions
        /// happened - so the survival is load-bearing, not incidental.
        /// </summary>
        [Test]
        public void AmuseComponentSurvivesUntilItsPlatformFinishPass()
        {
            RequireIntegrationEnvironment(
                out var traceAndOptimizeType,
                out var transparentShader,
                out var cutoutShader);
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

            var root = new GameObject("AMUSE component survival");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                var traceAndOptimize =
                    root.AddComponent(traceAndOptimizeType);
                ConfigureTraceAndOptimize(
                    traceAndOptimize, mergeSkinnedMesh: true,
                    optimizeTexture: false);
                AttachAnimationFixture(root, "survival");
                var texture = Track(ImportBandedAlphaTexture("banded_survival"));
                var transparent = Track(NewTransparentMaterial(
                    transparentShader, texture));
                var cutout = Track(NewCutoutMaterial(cutoutShader, texture, 0.25f));

                CreateSkinnedRenderer(
                    root, "AMUSE survival transparent renderer",
                    "AMUSE survival transparent mesh",
                    new[] { transparent },
                    new[] { Band.Transparent, Band.Partial, Band.Opaque },
                    new[] { 0, 0, 0 },
                    firstTriangleIndex: 0);
                CreateSkinnedRenderer(
                    root, "AMUSE survival cutout renderer",
                    "AMUSE survival cutout mesh",
                    new[] { cutout },
                    new[] { Band.Partial },
                    new[] { 0 },
                    firstTriangleIndex: 3);

                OptimizerMergeObservation.Reset();
                OptimizerMergeObservation.Enabled = true;
                try
                {
                    var context = AvatarProcessor.ProcessAvatar(
                        root, AmbientPlatform.DefaultPlatform);

                    Assert.That(
                        OptimizerMergeObservation.AfterOptimizer,
                        Is.Not.Null);
                    Assert.That(
                        OptimizerMergeObservation.AfterOptimizer
                            .RootHasAmuseComponent,
                        Is.True,
                        "the AMUSE component must survive Avatar"
                        + " Optimizer's passes until the AMUSE passes"
                        + " read it at platform finish");
                    Assert.That(
                        OptimizerMergeObservation.AfterAmuse,
                        Is.Not.Null);
                    Assert.That(
                        OptimizerMergeObservation.AfterAmuse
                            .RootHasAmuseComponent,
                        Is.True,
                        "the AMUSE component must still be on the root"
                        + " after the AMUSE passes ran");
                    AssertBuiltTrianglesMatchAuthoring(context, root);
                }
                finally
                {
                    OptimizerMergeObservation.Reset();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The serialized disable control: with the component's Disable
        /// AMUSE toggle set, the build completes but the pipeline must
        /// not execute - no renderer analyzed, no clones created - and
        /// every authored triangle stays on its original material. The
        /// toggle reads the serialized field, not the component's
        /// enabled state, so the fixture leaves the component enabled.
        /// </summary>
        [Test]
        public void SerializedDisableControlKeepsTheBuildUntouched()
        {
            RequireIntegrationEnvironment(
                out var traceAndOptimizeType,
                out var transparentShader,
                out _);
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

            var root = new GameObject("AMUSE disable control");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                var amuse = root.AddComponent<AmuseAvatarOptimizer>();
                var serialized = new SerializedObject(amuse);
                var toggle = serialized.FindProperty("_amuseDisabled");
                Assert.That(toggle, Is.Not.Null,
                    "AmuseAvatarOptimizer._amuseDisabled field pin");
                toggle.boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                AttachAnimationFixture(root, "disabled");
                var texture = Track(ImportBandedAlphaTexture("banded_disabled"));
                var transparent = Track(NewTransparentMaterial(
                    transparentShader, texture));
                CreateSkinnedRenderer(
                    root, "AMUSE disabled renderer", "AMUSE disabled mesh",
                    new[] { transparent },
                    new[] { Band.Transparent, Band.Partial, Band.Opaque },
                    new[] { 0, 0, 0 },
                    firstTriangleIndex: 0);

                var context = AvatarProcessor.ProcessAvatar(
                    root, AmbientPlatform.DefaultPlatform);
                Assert.That(context, Is.Not.Null);
                Assert.That(
                    context.Successful, Is.True,
                    "a disabled AMUSE component must not fail the build");

                var state = context.GetState<AmusePlatformFinishState>();
                Assert.That(state, Is.Not.Null);
                Assert.That(
                    state.HasExecuted, Is.True,
                    "HasExecuted records that the barrier pass ran; the"
                    + " disable toggle gates the pipeline after it, so"
                    + " the pass executes and refuses the avatar");
                Assert.That(
                    state.AnalyzedRendererCount, Is.EqualTo(0),
                    "a disabled run must not analyze renderers");

                var generated = new HashSet<Material>(
                    state.Separation?.CreatedClones
                    ?? (IEnumerable<Material>)Array.Empty<Material>());
                Assert.That(
                    generated, Is.Empty,
                    "a disabled run must not create generated materials");

                foreach (var renderer in root.GetComponentsInChildren<
                             SkinnedMeshRenderer>(true))
                {
                    var mesh = renderer.sharedMesh;
                    if (mesh == null) continue;
                    Assert.That(
                        renderer.sharedMaterials,
                        Has.All.Matches<Material>(material =>
                            material == transparent),
                        "a disabled run must leave the original material"
                        + " on every slot");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The swapped-state boundary: a committed material-swap curve
        /// flips one slot between a transparent material and a cutout
        /// material on one shared texture. The slot's admitted states
        /// then disagree - the partial-band triangle converts only under
        /// the cutout state - so the conservative per-slot resolution
        /// must keep it. A capture that lets the cutout state's
        /// binarized field answer for the transparent state proves the
        /// partial triangle opaque in both states and moves it.
        /// </summary>
        [Test]
        public void MaterialSwapKeepsTheConservativeStateOutcome()
        {
            RequireLilToonEnvironment(out var transparentShader, out var cutoutShader);
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

            var root = new GameObject("AMUSE material swap");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                AttachAnimationFixture(root, "swap");
                var texture = Track(ImportBandedAlphaTexture("banded_swap"));
                var transparent = Track(NewTransparentMaterial(
                    transparentShader, texture));
                var cutout = Track(NewCutoutMaterial(cutoutShader, texture, 0.25f));

                const string rendererPath = "AMUSE swap renderer";
                CreateSkinnedRenderer(
                    root, rendererPath, "AMUSE swap mesh",
                    new[] { transparent },
                    new[] { Band.Partial, Band.Opaque },
                    new[] { 0, 0 },
                    firstTriangleIndex: 0);

                // One committed swap curve on the slot: transparent at
                // rest, cutout while the clip plays.
                var swapClip = Track(new AnimationClip
                {
                    name = "swap-clip",
                });
                var binding = EditorCurveBinding.PPtrCurve(
                    rendererPath, typeof(SkinnedMeshRenderer),
                    "m_Materials.Array.data[0]");
                AnimationUtility.SetObjectReferenceCurve(swapClip, binding,
                    new[]
                    {
                        new ObjectReferenceKeyframe
                        {
                            time = 0f, value = transparent,
                        },
                        new ObjectReferenceKeyframe
                        {
                            time = 1f, value = cutout,
                        },
                    });

                var controller =
                    AnimatorController.CreateAnimatorControllerAtPath(
                        TempFolder + "/probe-swap-layer.controller");
                var swapState =
                    controller.layers[0].stateMachine.AddState("Swap");
                swapState.motion = swapClip;
                controller.layers[0].stateMachine.defaultState = swapState;
#if AMUSE_VRCSDK3_AVATARS
                var descriptor = root.GetComponent<
                    VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                if (descriptor != null)
                {
                    descriptor.customizeAnimationLayers = true;
                    descriptor.baseAnimationLayers = new[]
                    {
                        new VRC.SDK3.Avatars.Components.VRCAvatarDescriptor
                            .CustomAnimLayer
                        {
                            type = VRC.SDK3.Avatars.Components
                                .VRCAvatarDescriptor.AnimLayerType.Base,
                            animatorController = controller,
                            isDefault = false,
                            isEnabled = true,
                        },
                    };
                }
#endif

                var context = AvatarProcessor.ProcessAvatar(
                    root, AmbientPlatform.DefaultPlatform);
                AssertBuiltTrianglesMatchAuthoring(context, root);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The mask boundary: one inert-mask slot (multiply, scale 1,
        /// value 1 - the mask term saturates to exactly one) proves and
        /// converts its opaque-band control, while the sibling slot's
        /// darkening mask (scale 0.5, value 0) turns every main-alpha
        /// texel, the opaque band included, into a partial one that must
        /// stay. Same texture, same bands - only the mask predicate
        /// differs, and each slot must answer under its own.
        /// </summary>
        [Test]
        public void MaterialMaskDarkeningKeepsTheDarkenedTriangles()
        {
            RequireLilToonEnvironment(out var transparentShader, out _);
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

            var root = new GameObject("AMUSE mask darkening");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                AttachAnimationFixture(root, "mask");
                var texture = Track(ImportBandedAlphaTexture("banded_mask"));
                var inert = Track(NewMaskedTransparentMaterial(
                    transparentShader, texture, texture,
                    maskScale: 1f, maskValue: 1f));
                var darkened = Track(NewMaskedTransparentMaterial(
                    transparentShader, texture, texture,
                    maskScale: 0.5f, maskValue: 0f));

                CreateSkinnedRenderer(
                    root, "AMUSE mask renderer", "AMUSE mask mesh",
                    new[] { inert, darkened },
                    new[] { Band.Opaque, Band.Partial, Band.Opaque },
                    new[] { 0, 1, 1 },
                    firstTriangleIndex: 0,
                    expectationOverrides: new TriangleExpectation?[]
                    {
                        null,
                        null,

                        // The darkening mask halves every sampled alpha
                        // on this slot, so the opaque band arrives partial
                        // and must stay even though the same band converts
                        // on the inert slot.
                        TriangleExpectation.StaysTransparent,
                    });

                var context = AvatarProcessor.ProcessAvatar(
                    root, AmbientPlatform.DefaultPlatform);
                AssertBuiltTrianglesMatchAuthoring(context, root);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The streaming boundary: a material whose main texture imports
        /// with streaming mipmaps can never hand the capture a resident
        /// field, so the renderer must refuse conservatively - nothing
        /// proven, nothing moved, every authored triangle including the
        /// positive control on its original material.
        /// </summary>
        [Test]
        public void StreamingTextureRefusesItsRendererConservatively()
        {
            RequireLilToonEnvironment(out var transparentShader, out _);
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

            var root = new GameObject("AMUSE streaming texture");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                AttachAnimationFixture(root, "streaming");
                var texture = Track(
                    ImportBandedAlphaTexture("banded_streaming",
                        streaming: true));
                var material = Track(NewTransparentMaterial(
                    transparentShader, texture));

                CreateSkinnedRenderer(
                    root, "AMUSE streaming renderer", "AMUSE streaming mesh",
                    new[] { material },
                    new[] { Band.Partial, Band.Opaque },
                    new[] { 0, 0 },
                    firstTriangleIndex: 0);

                var context = AvatarProcessor.ProcessAvatar(
                    root, AmbientPlatform.DefaultPlatform);
                AssertBuiltTrianglesMatchAuthoring(context, root,
                    conservativeRefusalAllowed: true);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// A real two-bone rig: both renderers weight their triangles
        /// across a Hips/Chest hierarchy with per-bone bind poses, and
        /// Avatar Optimizer merges them under the same root bone. The
        /// bones sit at identity, so the oracle's root-space positions
        /// stay exact, and the merged avatar must keep every band's
        /// expectation.
        /// </summary>
        [Test]
        public void MultiBoneRigMergesAndPreservesSoundness()
        {
            RequireIntegrationEnvironment(
                out var traceAndOptimizeType,
                out var transparentShader,
                out var cutoutShader);
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

            var root = new GameObject("AMUSE multi bone rig");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                var traceAndOptimize =
                    root.AddComponent(traceAndOptimizeType);
                ConfigureTraceAndOptimize(
                    traceAndOptimize, mergeSkinnedMesh: true,
                    optimizeTexture: false);
                AttachAnimationFixture(root, "bones");

                var hips = new GameObject("Hips");
                hips.transform.SetParent(root.transform, false);
                var chest = new GameObject("Chest");
                chest.transform.SetParent(hips.transform, false);
                var bones = new[] { hips.transform, chest.transform };

                var texture = Track(ImportBandedAlphaTexture("banded_bones"));
                var transparent = Track(NewTransparentMaterial(
                    transparentShader, texture));
                var cutout = Track(NewCutoutMaterial(cutoutShader, texture, 0.25f));

                CreateSkinnedRenderer(
                    root, "AMUSE rig transparent renderer",
                    "AMUSE rig transparent mesh",
                    new[] { transparent },
                    new[] { Band.Transparent, Band.Partial, Band.Opaque },
                    new[] { 0, 0, 0 },
                    firstTriangleIndex: 0,
                    bones: bones);
                CreateSkinnedRenderer(
                    root, "AMUSE rig cutout renderer",
                    "AMUSE rig cutout mesh",
                    new[] { cutout },
                    new[] { Band.Partial },
                    new[] { 0 },
                    firstTriangleIndex: 3,
                    bones: bones);

                OptimizerMergeObservation.Reset();
                OptimizerMergeObservation.Enabled = true;
                try
                {
                    var context = AvatarProcessor.ProcessAvatar(
                        root, AmbientPlatform.DefaultPlatform);

                    var snapshot = OptimizerMergeObservation.AfterOptimizer;
                    Assert.That(snapshot, Is.Not.Null);
                    var merged = snapshot.Renderers.Where(renderer =>
                        renderer.TriangleKeys.Count == 4).ToList();
                    Assert.That(
                        merged.Count, Is.EqualTo(1),
                        "the two rigged renderers must merge under one"
                        + " root bone: " + DescribeSnapshot(snapshot));
                    AssertBuiltTrianglesMatchAuthoring(context, root);
                }
                finally
                {
                    OptimizerMergeObservation.Reset();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void RequireLilToonEnvironment(
            out Shader transparentShader, out Shader cutoutShader)
        {
            transparentShader = Shader.Find(LilToonTransparentShaderName);
            cutoutShader = Shader.Find(LilToonCutoutShaderName);
            if (transparentShader != null && cutoutShader != null) return;
            Assert.Ignore(
                "The jp.lilxyzw.liltoon package is not installed in this"
                + " project; the vendor shader fixtures cannot resolve."
                + " Install it to run them.");
        }

        private static void RequireIntegrationEnvironment(
            out Type traceAndOptimizeType,
            out Shader transparentShader,
            out Shader cutoutShader)
        {
            RequireLilToonEnvironment(
                out transparentShader, out cutoutShader);
            traceAndOptimizeType = Type.GetType(TraceAndOptimizeTypeName);
            if (traceAndOptimizeType != null) return;
            Assert.Ignore(
                "The com.anatawa12.avatar-optimizer package is not"
                + " installed in this project; the real merge cannot be"
                + " exercised. Install it to run them.");
        }

        private void ConfigureTraceAndOptimize(
            Component traceAndOptimize,
            bool mergeSkinnedMesh,
            bool optimizeTexture)
        {
            // The component type is public, but the vendor does not
            // promise a scripting configuration API, so the fixture goes
            // through serialized fields and pins them: a missing field is
            // a version-specific fixture failure, never a silent default.
            var serialized = new SerializedObject(traceAndOptimize);
            var merge = serialized.FindProperty("mergeSkinnedMesh");
            var texture = serialized.FindProperty("optimizeTexture");
            Assert.That(merge, Is.Not.Null,
                "TraceAndOptimize.mergeSkinnedMesh field (AAO version pin)");
            Assert.That(texture, Is.Not.Null,
                "TraceAndOptimize.optimizeTexture field (AAO version pin)");
            merge.boolValue = mergeSkinnedMesh;
            texture.boolValue = optimizeTexture;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Adds the committed animation fixture: a real synthetic clip on
        /// a real controller state, carried by the root Animator and, when
        /// the SDK is present, by the descriptor layer NDMF's bindings
        /// actually read. An empty controller or an untouched default SDK
        /// layer is exactly the fixture shape that made earlier runs
        /// diverge, so the fixture controls both.
        /// </summary>
        private void AttachAnimationFixture(
            GameObject root, string assetSuffix,
            Action<AnimationClip> customizeClip = null)
        {
            var anchor = new GameObject("ProbeAnchor");
            anchor.transform.SetParent(root.transform, false);
            var clip = Track(new AnimationClip
            {
                name = "probe-clip-" + assetSuffix,
            });
            customizeClip?.Invoke(clip);
            clip.SetCurve(
                "ProbeAnchor", typeof(Transform), "m_LocalPosition.z",
                AnimationCurve.Constant(0f, 1f, 1f));

            var controller = AnimatorController.CreateAnimatorControllerAtPath(
                TempFolder + "/probe-" + assetSuffix + ".controller");
            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.AddState("Probe");
            state.motion = clip;
            stateMachine.defaultState = state;

            root.AddComponent<Animator>().runtimeAnimatorController =
                controller;

#if AMUSE_VRCSDK3_AVATARS
            var descriptor = root.GetComponent<
                VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            if (descriptor == null) return;
            descriptor.customizeAnimationLayers = true;
            var baseLayer =
                new VRC.SDK3.Avatars.Components.VRCAvatarDescriptor
                    .CustomAnimLayer
            {
                type = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor
                    .AnimLayerType.Base,
                animatorController = controller,
                isDefault = false,
                isEnabled = true,
            };
            var layers = descriptor.baseAnimationLayers;
            if (layers == null || layers.Length == 0)
            {
                layers = new[] { baseLayer };
            }
            else
            {
                layers[0] = baseLayer;
            }

            descriptor.baseAnimationLayers = layers;
            // A fresh descriptor carries null layer arrays (verified on
            // this SDK build). NDMF's commit path tolerates a null array
            // when virtualizing but dereferences it when committing, so
            // the fixture materializes the special layers with their
            // fallback defaults instead of leaving the build a crash.
            if (descriptor.specialAnimationLayers == null)
            {
                descriptor.specialAnimationLayers = new[]
                {
                    FallbackLayer(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor
                        .AnimLayerType.Sitting),
                    FallbackLayer(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor
                        .AnimLayerType.TPose),
                    FallbackLayer(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor
                        .AnimLayerType.IKPose),
                };
            }
#endif
        }

        /// <summary>
        /// Creates one production-shaped skinned renderer: one holder
        /// object, real skinning against the avatar root bone, distinct
        /// world positions per triangle, one band per triangle, and the
        /// triangles partitioned across the material slots. Each triangle
        /// is registered for the oracle with its band's expectation.
        /// </summary>
        private SkinnedMeshRenderer CreateSkinnedRenderer(
            GameObject root,
            string holderName,
            string meshName,
            Material[] materials,
            Band[] bands,
            int[] triangleSubmesh,
            int firstTriangleIndex,
            Transform[] bones = null,
            TriangleExpectation?[] expectationOverrides = null)
        {
            Assert.That(bands.Length, Is.EqualTo(triangleSubmesh.Length));
            var holder = new GameObject(holderName);
            holder.transform.SetParent(root.transform, false);
            var renderer = holder.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh { name = meshName };
            Track(mesh);

            var vertices = new Vector3[bands.Length * 3];
            var uvs = new Vector2[bands.Length * 3];
            var submeshTriangles = new List<int>[materials.Length];
            for (var submesh = 0; submesh < materials.Length; submesh++)
            {
                submeshTriangles[submesh] = new List<int>();
            }

            for (var i = 0; i < bands.Length; i++)
            {
                var index = firstTriangleIndex + i;
                var v0 = i * 3;
                vertices[v0] = new Vector3(4 * index, 0f, 0f);
                vertices[v0 + 1] = new Vector3(4 * index + 1, 0f, 0f);
                vertices[v0 + 2] = new Vector3(4 * index, 1f, 0f);
                var triplet = BandUv(bands[i]);
                uvs[v0] = triplet[0];
                uvs[v0 + 1] = triplet[1];
                uvs[v0 + 2] = triplet[2];
                submeshTriangles[triangleSubmesh[i]].AddRange(
                    new[] { v0, v0 + 1, v0 + 2 });
                var authoredKey = OptimizerMergeObservation.TriangleKey(
                    vertices[v0], vertices[v0 + 1], vertices[v0 + 2],
                    root.transform);
                if (!authored.TryGetValue(authoredKey, out var entry))
                {
                    entry = new AuthoredTriangle
                    {
                        Key = authoredKey,
                        Expectation = expectationOverrides != null
                            && expectationOverrides[i].HasValue
                            ? expectationOverrides[i].Value
                            : bands[i] == Band.Opaque
                                ? TriangleExpectation.ConvertsToOpaque
                                : TriangleExpectation.StaysTransparent,
                    };
                    authored.Add(authoredKey, entry);
                }

                entry.Remaining++;
            }

            if (mesh.vertexCount > ushort.MaxValue)
            {
                // The 32-bit format must precede the indices: writing
                // indices past 65535 on a 16-bit buffer corrupts the mesh.
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.subMeshCount = materials.Length;
            for (var submesh = 0; submesh < materials.Length; submesh++)
            {
                mesh.SetTriangles(submeshTriangles[submesh], submesh);
            }

            // The optimizers merge only valid skinned meshes; an
            // unskinned fixture is refused before classification and
            // proves nothing. Identity bind poses, full per-vertex
            // weights; the rig defaults to the avatar root as its one
            // bone, and the multi-bone fixture passes its hierarchy.
            var boneList = bones ?? new[] { root.transform };
            mesh.bindposes = boneList.Select(_ => Matrix4x4.identity)
                .ToArray();
            var weights = new BoneWeight[mesh.vertexCount];
            var normals = new Vector3[mesh.vertexCount];
            for (var i = 0; i < mesh.vertexCount; i++)
            {
                weights[i] = new BoneWeight
                {
                    boneIndex0 = i % boneList.Length,
                    weight0 = 1f,
                };
                normals[i] = Vector3.forward;
            }

            mesh.boneWeights = weights;
            mesh.normals = normals;
            mesh.RecalculateBounds();

            if (mesh.vertexCount > ushort.MaxValue)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            // Avatar Optimizer only merges renderers whose every
            // categorized property matches, bounds included, so every
            // renderer in one fixture shares one explicit bound box
            // instead of the per-triangle positions' natural bounds.
            var sharedBounds = new Bounds(
                new Vector3(2f, 0.5f, 0f), new Vector3(64f, 8f, 8f));
            mesh.bounds = sharedBounds;

            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = materials;
            renderer.bones = boneList;
            renderer.rootBone = boneList[0];
            renderer.probeAnchor = root.transform;
            renderer.localBounds = sharedBounds;
            return renderer;
        }

        private static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor
            .CustomAnimLayer FallbackLayer(
            VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType type)
        {
            return new VRC.SDK3.Avatars.Components.VRCAvatarDescriptor
                .CustomAnimLayer
            {
                type = type,
                isDefault = true,
                isEnabled = true,
            };
        }

        private static Vector2[] BandUv(Band band)
        {
            switch (band)
            {
                case Band.Transparent: return TransparentBandUv;
                case Band.Partial: return PartialBandUv;
                case Band.Opaque: return OpaqueBandUv;
                default:
                    throw new ArgumentOutOfRangeException(nameof(band));
            }
        }

        private Material NewTransparentMaterial(
            Shader shader, Texture texture)
        {
            var material = new Material(shader);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            // A positive synthetic cutoff within the proven boundary: the
            // transparent frontend proves the plain main-alpha clip at or
            // below its provable cutoff boundary.
            material.SetFloat("_Cutoff", 0.01f);
            // Mask mode 2 (multiply) with scale 1 and value 1: the pinned
            // vendor source computes saturate(mask.r * 1 + 1), which is
            // exactly one for every texel, so the mask term is inert and
            // the alpha is the plain main texture sample.
            material.SetFloat("_AlphaMaskMode", 2f);
            material.SetFloat("_AlphaMaskScale", 1f);
            material.SetFloat("_AlphaMaskValue", 1f);
            material.SetTexture("_AlphaMask", texture);
            material.SetTexture("_MainTex", texture);
            return Track(material);
        }


        private Material NewMaskedTransparentMaterial(
            Shader shader,
            Texture mainTexture,
            Texture maskTexture,
            float maskScale,
            float maskValue)
        {
            var material = new Material(shader);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            material.SetFloat("_Cutoff", 0.01f);
            // Multiply mask: the pinned vendor source computes
            // saturate(mask.r * scale + value). Scale 1 with value 1
            // saturates to exactly one for every texel - inert. Scale
            // 0.5 with value 0 halves the sampled alpha.
            material.SetFloat("_AlphaMaskMode", 2f);
            material.SetFloat("_AlphaMaskScale", maskScale);
            material.SetFloat("_AlphaMaskValue", maskValue);
            material.SetTexture("_AlphaMask", maskTexture);
            material.SetTexture("_MainTex", mainTexture);
            return Track(material);
        }
        private Material NewCutoutMaterial(
            Shader shader, Texture texture, float cutoff)
        {
            var material = new Material(shader);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            material.SetFloat("_Cutoff", cutoff);
            material.SetFloat("_AlphaMaskMode", 2f);
            material.SetFloat("_AlphaMaskScale", 1f);
            material.SetFloat("_AlphaMaskValue", 1f);
            material.SetTexture("_AlphaMask", texture);
            material.SetTexture("_MainTex", texture);
            return Track(material);
        }

        /// <summary>
        /// A 128x128 (or 512x512) RGBA32 mipmapped texture with three
        /// vertical alpha bands: 0, 128, and 255, left to right. The band
        /// boundaries sit at exact quarter fractions of the width, so
        /// every texel of every resident mip level - down to the default
        /// mip cap of four - samples exactly one band.
        /// </summary>
        private Texture2D ImportBandedAlphaTexture(
            string assetName, int size = 128, bool streaming = false)
        {
            Assert.That(size % 8, Is.EqualTo(0),
                "the fixture needs the default four-level mip cap clean");
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var alpha = x < size / 4
                        ? (byte)0
                        : x < size / 2 ? (byte)128 : (byte)255;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            return ImportAlphaTexture(
                assetName, size, pixels, streaming);
        }

        /// <summary>
        /// A 128x128 RGBA32 mipmapped texture: the left half fully opaque,
        /// the right half fully transparent. The quadrant edge sits exactly
        /// on the texel grid so the strict-quadrant triangles sample texels
        /// of one verdict only.
        /// </summary>
        private Texture2D ImportQuadrantAlphaTexture()
        {
            // 128 pixels: the shipped component default refuses textures
            // smaller than 128 as capture evidence, so a smaller fixture
            // would silently classify every triangle all-unknown.
            const int size = 128;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    pixels[y * size + x] = x < size / 2
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            return ImportAlphaTexture("quadrant_alpha", size, pixels);
        }

        private Texture2D ImportAlphaTexture(
            string assetName, int size, Color32[] pixels,
            bool streaming = false)
        {
            var cpu = new Texture2D(size, size, TextureFormat.RGBA32, false);
            cpu.SetPixels32(pixels);
            cpu.Apply(false, false);
            var encoded = cpu.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(cpu);

            var path = TempFolder + "/" + assetName + ".png";
            File.WriteAllBytes(path, encoded);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            importer.mipmapEnabled = true;
            importer.isReadable = true;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.streamingMipmaps = streaming;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture, Is.Not.Null, "fixture precondition");
            return texture;
        }

        // --- observation and the soundness oracle -----------------------------

        private static void AssertMergedRendererCarriedBothSourcesPreAmuse()
        {
            var snapshot = OptimizerMergeObservation.AfterOptimizer;
            Assert.That(
                snapshot, Is.Not.Null,
                "the observation pass after the optimizer must have run;"
                + " postAmuse="
                + (OptimizerMergeObservation.AfterAmuse != null));
            var merged = snapshot.Renderers.Where(renderer =>
                renderer.TriangleKeys.Count == 4).ToList();
            Assert.That(
                merged.Count, Is.EqualTo(1),
                "Avatar Optimizer must merge the two source renderers"
                + " into one renderer before AMUSE runs; observed: "
                + DescribeSnapshot(snapshot));
            Assert.That(
                merged[0].SlotShaderNames,
                Has.Some.EqualTo(LilToonTransparentShaderName),
                "the merged renderer must keep the transparent mode:"
                + DescribeSnapshot(snapshot));
            Assert.That(
                merged[0].SlotShaderNames,
                Has.Some.EqualTo(LilToonCutoutShaderName),
                "the merged renderer must keep the cutout mode:"
                + DescribeSnapshot(snapshot));
        }

        private static void AssertPackingReplacedTheFixtureTextures(
            GameObject root, Texture2D[] sources)
        {
            var sourceNames = new HashSet<string>(
                sources.Select(texture => texture.name));
            var packedSeen = false;
            foreach (var renderer in root.GetComponentsInChildren<
                         SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null) continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    if (!material.HasProperty("_MainTex")) continue;
                    var texture = material.GetTexture("_MainTex");
                    if (texture == null) continue;
                    Assert.That(
                        sourceNames,
                        Does.Not.Contain(texture.name),
                        "Optimize Texture must replace the fixture"
                        + " texture; slot " + material.name + " still"
                        + " references " + texture.name);
                    Assert.That(
                        texture.width,
                        Is.GreaterThanOrEqualTo(128),
                        "the packed texture must stay at or above the"
                        + " capture's minimum size");
                    packedSeen = true;
                }
            }

            Assert.That(
                packedSeen, Is.True,
                "Optimize Texture must leave at least one packed slot to"
                + " exercise the real texture transformation; if packing"
                + " refused this fixture, shrink the islands - never"
                + " weaken this assertion");
        }

        /// <summary>
        /// The soundness oracle. Every authored triangle must appear in
        /// the built avatar exactly once, matched as an unordered
        /// avatar-root-space position triple on the fixed rounding grid,
        /// and its material assignment must follow its expectation: a
        /// positive control triangle sits on a generated opaque material,
        /// and every other triangle sits on a non-generated slot. With
        /// <c>conservativeRefusalAllowed</c>, a build the pipeline refused
        /// by the named all-unknown bucket takes the other arm: nothing
        /// may be generated anywhere, so nothing moved. Missing,
        /// repeated, and unexpected triangles all fail with the built
        /// renderer shape in the message.
        /// </summary>
        private void AssertBuiltTrianglesMatchAuthoring(
            BuildContext context, GameObject root,
            bool conservativeRefusalAllowed = false)
        {
            Assert.That(context, Is.Not.Null, "the build must produce context");
            Assert.That(
                context.Successful, Is.True,
                "the fixture must complete the real build; the NDMF"
                + " error report names the failing pass in the editor"
                + " console; builtRenderers="
                + DescribeBuiltRenderers(root));

            var state = context.GetState<AmusePlatformFinishState>();
            Assert.That(state, Is.Not.Null);
            var generated = new HashSet<Material>(
                state.Separation?.CreatedClones
                ?? (IEnumerable<Material>)Array.Empty<Material>());

            // The conservative arm of the soundness contract: when the
            // pipeline refuses every renderer by name - observed for the
            // AAO-packed atlas, which the shipped capture cannot admit -
            // nothing may be proven and nothing may move. The strict arm
            // demands real analysis and at least one conversion.
            var conservativeRefusal = conservativeRefusalAllowed
                && state.ReachedRendererAnalysis
                && state.AnalyzedRendererCount == 0
                && state.SemanticallyRefusedRendererCount > 0
                && state.RendererRefusalCount(
                    RendererAnalysisRefusal.AdmittedMaterialSemanticsUnknown)
                == state.SemanticallyRefusedRendererCount;
            if (conservativeRefusal)
            {
                Assert.That(
                    generated, Is.Empty,
                    "a conservatively refused renderer must not move any"
                    + " triangle to a generated material");
            }
            else
            {
                Assert.That(
                    state.AnalyzedRendererCount,
                    Is.GreaterThanOrEqualTo(1),
                    "AMUSE must analyze the avatar; a zero means the"
                    + " passes did not run on this platform or an"
                    + " avatar-scope gate refused: reachedAnalysis="
                    + state.ReachedRendererAnalysis
                    + " avatarRefusal=" + state.AvatarRefusal
                    + " lifecycle=" + (state.Lifecycle != null
                        ? state.Lifecycle.ToString()
                        : "<null>")
                    + " consentDeclined=" + state.ConsentDeclined
                    + " alphaPolicyActive=" + state.AlphaPolicyActive
                    + " refusedRenderers="
                    + state.SemanticallyRefusedRendererCount
                    + DescribeRendererRefusals(state)
                    + " builtRenderers=" + DescribeBuiltRenderers(root));
                Assert.That(
                    generated,
                    Is.Not.Empty,
                    "fixture expectation: the fixture's opaque control"
                    + " triangle is proven and converted; zero clones"
                    + " means the pipeline refused the renderer instead."
                    + " Refusal buckets: " + DescribeSlotRefusals(state)
                    + " analyzed=" + state.AnalyzedRendererCount
                    + " refusedRenderers="
                    + state.SemanticallyRefusedRendererCount
                    + " opaqueCandidates="
                    + state.OpaqueCandidateTriangleCount
                    + " separation=" + DescribeSeparation(state)
                    + " builtRenderers=" + DescribeBuiltRenderers(root));
            }

            foreach (var renderer in root.GetComponentsInChildren<
                         SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh;
                var materials = renderer.sharedMaterials;
                if (mesh == null || materials == null
                    || materials.Length == 0) continue;

                var vertices = mesh.vertices;
                var subMeshCount = Math.Min(mesh.subMeshCount,
                    materials.Length);
                for (var subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    var material = materials[subMesh];
                    var isGenerated = material != null
                        && generated.Contains(material);
                    var triangles = mesh.GetTriangles(subMesh);
                    for (var t = 0; t < triangles.Length; t += 3)
                    {
                        var key = OptimizerMergeObservation.TriangleKey(
                            renderer.transform.TransformPoint(
                                vertices[triangles[t]]),
                            renderer.transform.TransformPoint(
                                vertices[triangles[t + 1]]),
                            renderer.transform.TransformPoint(
                                vertices[triangles[t + 2]]),
                            root.transform);
                        if (!authored.TryGetValue(key, out var match)
                            || match.Remaining <= 0)
                        {
                            Assert.Fail(
                                "output triangle " + key + " must match"
                                + " exactly one unmatched authored triangle"
                                + " (renderer " + renderer.name + " slot "
                                + subMesh + "); builtRenderers="
                                + DescribeBuiltRenderers(root));
                        }

                        match.Remaining--;
                        var slotName = material != null
                            ? material.name : "<null>";
                        var expectGenerated = conservativeRefusal
                            ? false
                            : match.Expectation
                                == TriangleExpectation.ConvertsToOpaque;
                        Assert.That(
                            isGenerated,
                            Is.EqualTo(expectGenerated),
                            "triangle " + key + " violated its soundness"
                            + " expectation: slot material " + slotName
                            + (isGenerated ? " is" : " is not")
                            + " a generated opaque material"
                            + (conservativeRefusal
                                ? "; conservative refusal allows no"
                                    + " movement"
                                : "")
                            + "; builtRenderers="
                            + DescribeBuiltRenderers(root));
                    }
                }
            }

            var unmatched = authored.Values.Where(entry =>
                entry.Remaining > 0).ToList();
            Assert.That(
                unmatched, Is.Empty,
                "authored triangles missing from the built avatar: "
                + string.Join("; ", unmatched.Select(entry => entry.Key))
                + "; builtRenderers=" + DescribeBuiltRenderers(root));
        }
        /// <summary>
        /// Names exactly what the barrier analyzed: the post-optimizer
        /// renderer shapes, slot shader names, submesh counts, UV0
        /// presence, and property-block presence. A slot carrying an
        /// unexpected shader, a mesh whose UV0 was dropped or resized by
        /// optimizer mesh processing, or a property block AAO wrote on the
        /// renderer explains a silent refusal that the analyzed count
        /// alone does not.
        /// </summary>
        private static string DescribeBuiltRenderers(GameObject root)
        {
            var renderers =
                root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var parts = new List<string>();
            foreach (var renderer in renderers)
            {
                var mesh = renderer.sharedMesh;
                var uvs = mesh != null ? mesh.uv : null;
                var shaders = new List<string>();
                foreach (var slot in renderer.sharedMaterials)
                {
                    shaders.Add(slot != null && slot.shader != null
                        ? slot.shader.name
                        : "<null>");
                }

                parts.Add(renderer.name + " slots=["
                    + string.Join(", ", shaders) + "]"
                    + " submeshes=" + (mesh != null
                        ? mesh.subMeshCount.ToString()
                        : "<no mesh>")
                    + " vertices=" + (mesh != null
                        ? mesh.vertexCount.ToString()
                        : "<no mesh>")
                    + " uv0=" + (uvs != null
                        ? uvs.Length.ToString()
                        : "<missing>")
                    + " propertyBlock=" + renderer.HasPropertyBlock());
            }

            return parts.Count == 0
                ? "none"
                : string.Join("; ", parts);
        }

        private static string DescribeSnapshot(
            OptimizerMergeObservation.PhaseSnapshot snapshot)
        {
            if (snapshot == null) return "<none>";
            var parts = snapshot.Renderers.Select(renderer =>
                renderer.RendererName + " triangles="
                + renderer.TriangleKeys.Count + " slots=["
                + string.Join(", ", renderer.SlotShaderNames) + "]");
            return string.Join("; ", parts);
        }

        /// <summary>
        /// Prints every non-empty renderer-scoped refusal bucket, so a
        /// zero-analyzed build with a reached analysis names the exact
        /// refusal each renderer hit instead of vanishing between the
        /// structural check and the analyzed counter.
        /// </summary>
        private static string DescribeRendererRefusals(
            AmusePlatformFinishState state)
        {
            var parts = new List<string>();
            foreach (RendererAnalysisRefusal reason in Enum.GetValues(
                         typeof(RendererAnalysisRefusal)))
            {
                if (reason == RendererAnalysisRefusal.None)
                {
                    continue;
                }

                var count = state.RendererRefusalCount(reason);
                if (count != 0)
                {
                    parts.Add(" " + reason + "=" + count);
                }
            }

            return parts.Count == 0 ? "" : " buckets:" + string.Join(
                "", parts);
        }

        /// <summary>
        /// Names what the barrier retained, so a zero-clone failure splits
        /// into "no renderer ever had a candidate slot" (separation none or
        /// empty: admission or slot lookup failed silently) versus
        /// "prepared renderers exist with zero proven triangles"
        /// (resolution succeeded, classification received bad inputs).
        /// </summary>
        private static string DescribeSeparation(
            AmusePlatformFinishState state)
        {
            var separation = state.Separation;
            if (separation == null) return "none";
            var parts = new List<string>();
            foreach (var renderer in separation.Renderers)
            {
                parts.Add(renderer.RendererPath + " slots="
                    + renderer.CandidateSlots.Count);
            }

            return parts.Count == 0
                ? "empty"
                : string.Join("; ", parts);
        }

        private static string DescribeSlotRefusals(
            AmusePlatformFinishState state)
        {
            var parts = new List<string>();
            foreach (AlphaSeparationSlotRefusal reason in Enum.GetValues(
                         typeof(AlphaSeparationSlotRefusal)))
            {
                if (reason == AlphaSeparationSlotRefusal.None) continue;
                var count = state.SlotRefusalCount(reason);
                if (count != 0) parts.Add(reason + "=" + count);
            }

            return parts.Count == 0
                ? "none"
                : string.Join(", ", parts);
        }
    }
}
