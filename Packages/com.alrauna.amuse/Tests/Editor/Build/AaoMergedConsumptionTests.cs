using System;
using System.Collections.Generic;
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
        : MergedConsumptionFixture
    {
        protected override string TempFolder =>
            "Assets/AmuseTests_AaoMerge";

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
                    out var capturedList,
                    RegisteredSourceIdentity.Resolve);
            Assert.That(
                attested,
                Is.True,
                "attested closed capture refused the fixture material;"
                + " family=" + family);

            var semantics =
                UnityMaterialSemantics.AnalyzeAlphaMaterial(capturedList[0]);
            Assert.That(
                semantics.Semantics.Alpha.IsComplete,
                Is.True,
                "alpha analysis did not resolve for the fixture material;"
                + " family=" + family
                + " alphaKind=" + semantics.Semantics.Alpha.GetCompleteValue().Kind);
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
            var probeMaterial = evidence.AdmittedMaterials[0];
            AlphaFieldProvider provider = (
                TextureSourceId source,
                TextureChannel channel,
                out AlphaMipChain chain) =>
                fields.TryGetFor(
                    probeMaterial.Evidence,
                    UnityMaterialSemantics.AlphaRequestForFamily(
                        probeMaterial.Family),
                    source,
                    channel,
                    out chain);
            var resolution = AlphaSemanticsResolver.Resolve(
                semantics.Semantics.Alpha, provider, 0);
            Assert.That(
                resolution.Failure,
                Is.EqualTo(AlphaResolutionFailure.None),
                "the in-build resolution pipeline failed: fields="
                + fields.Count
                + " alphaComplete=" + semantics.Semantics.Alpha.IsComplete
                + " alphaKind=" + (semantics.Semantics.Alpha.IsComplete
                    ? semantics.Semantics.Alpha.GetCompleteValue().Kind.ToString()
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
        /// The generated-atlas reproduction of the real-avatar defect:
        /// after Avatar Optimizer merges the group, a lilToon cutout slot
        /// and a lilToon transparent slot share one generated atlas
        /// texture, the shape Avatar Optimizer's texture packing produces
        /// when a cutout material and a transparent material land in one
        /// atlas. The cutout's own field is captured binarized by its
        /// declared cutoff, and the transparent slot's field is captured
        /// exact. A gather that collapses the two predicates into one
        /// shared field lets the transparent slot's partial-band triangle
        /// prove opaque under the cutout's cutoff and move to a generated
        /// material, which this test fails on. The cutout slot's own
        /// partial-band triangle legitimately converts under its own
        /// predicate in both worlds.
        /// </summary>
        [Test]
        public void MergedCutoutSlotSharingAGeneratedAtlasKeepsTheTransparentPredicate()
        {
            RequireIntegrationEnvironment(
                out var traceAndOptimizeType,
                out var transparentShader,
                out var cutoutShader);
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

            var root = new GameObject("AMUSE atlas cutout leak");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                var traceAndOptimize =
                    root.AddComponent(traceAndOptimizeType);
                ConfigureTraceAndOptimize(
                    traceAndOptimize, mergeSkinnedMesh: true,
                    optimizeTexture: false);
                AttachAnimationFixture(root, "atlas-leak");
                var atlas = CreateGeneratedAtlas("atlas_cutout_leak");
                var cutout = Track(NewCutoutMaterial(
                    cutoutShader, atlas, 0.25f));
                var transparent = Track(NewTransparentMaterial(
                    transparentShader, atlas));

                // The cutout renderer leads the merge, so its binarized
                // field is gathered first for the shared atlas source.
                CreateSkinnedRenderer(
                    root, "AMUSE atlas cutout renderer",
                    "AMUSE atlas cutout mesh",
                    new[] { cutout },
                    new[] { Band.Partial, Band.Opaque },
                    new[] { 0, 0 },
                    firstTriangleIndex: 0,
                    expectationOverrides: new TriangleExpectation?[]
                    {
                        // The cutout's own 0.25 cutoff admits the
                        // partial band: it converts under its own
                        // predicate whether or not fields are shared.
                        TriangleExpectation.ConvertsToOpaque,
                        null,
                    });
                CreateSkinnedRenderer(
                    root, "AMUSE atlas transparent renderer",
                    "AMUSE atlas transparent mesh",
                    new[] { transparent },
                    new[] { Band.Partial, Band.Opaque },
                    new[] { 0, 0 },
                    firstTriangleIndex: 2);

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

        /// <summary>
        /// The cutout-family trigger, isolated: one merged group where a
        /// lilToon transparent slot and a lilToon cutout slot share the
        /// same banded texture. The cutout slot's own predicate (cutoff
        /// 0.25) legitimately proves its partial-band triangle; the
        /// transparent slot's partial-band triangle must stay regardless.
        /// If the cutout slot's binarized field leaks into the transparent
        /// slot's classification, the transparent partial triangle
        /// converts and this test fails on its soundness expectation.
        /// </summary>
        [Test]
        public void CutoutFamilyPresenceKeepsTheTransparentSlotOutcome()
        {
            RequireLilToonEnvironment(out var transparentShader, out var cutoutShader);
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

            var root = new GameObject("AMUSE cutout presence");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                AttachAnimationFixture(root, "cutout-presence");
                var texture = Track(ImportBandedAlphaTexture("banded_cutout_presence"));
                var transparent = Track(NewTransparentMaterial(
                    transparentShader, texture));
                var cutout = Track(NewCutoutMaterial(cutoutShader, texture, 0.25f));

                CreateSkinnedRenderer(
                    root, "AMUSE cutout presence renderer",
                    "AMUSE cutout presence mesh",
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
        /// The many-slot reproduction: a merged renderer carrying many
        /// material slots - mixed-alpha materials on fully-opaque
        /// neighbors, in shuffled slot order - must still classify every
        /// triangle by its own slot's texture. Any cross-slot evidence
        /// leak converts whole mixed slots, which this test fails on.
        /// </summary>
        [Test]
        [TestCase(false)]
        [TestCase(true)]
        public void ManySlotMergedRendererKeepsMixedAlphaTrianglesOnTheirSlots(
            bool includeDao)
        {
            RequireIntegrationEnvironment(
                out var traceAndOptimizeType,
                out var transparentShader,
                out var cutoutShader);
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

            var root = new GameObject("AMUSE many slot merged"
                + (includeDao ? " with DAO" : ""));
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                var traceAndOptimize =
                    root.AddComponent(traceAndOptimizeType);
                ConfigureTraceAndOptimize(
                    traceAndOptimize, mergeSkinnedMesh: true,
                    optimizeTexture: false,
                    allowShuffleMaterialSlots: true,
                    includeDao: includeDao);
                AttachAnimationFixture(root, "many-slots");

                var banded =
                    Track(ImportBandedAlphaTexture("banded_many"));
                var opaque =
                    Track(ImportSolidAlphaTexture("solid_opaque"));

                const int rendererCount = 10;
                var firstTriangleIndex = 0;
                for (var index = 0; index < rendererCount; index++)
                {
                    // The real avatar's dress family are per-renderer
                    // material clones: distinct instances with identical
                    // content, one set per source renderer. The fixture
                    // mirrors that instead of sharing one instance.
                    var mixed = Track(NewTransparentMaterial(
                        transparentShader, banded));
                    var fullyOpaque = Track(NewTransparentMaterial(
                        transparentShader, opaque));
                    // Even renderers lead with the mixed slot; odd ones
                    // lead with the opaque slot, so both orders exist for
                    // the shuffler.
                    var mixedFirst = index % 2 == 0;
                    var materials = mixedFirst
                        ? new[] { mixed, fullyOpaque }
                        : new[] { fullyOpaque, mixed };
                    var mixedSubmesh = mixedFirst ? 0 : 1;
                    var opaqueSubmesh = mixedFirst ? 1 : 0;
                    CreateSkinnedRenderer(
                        root, "AMUSE many slot renderer " + index,
                        "AMUSE many slot mesh " + index,
                        materials,
                        new[] { Band.Partial, Band.Opaque, Band.Opaque },
                        new[]
                        {
                            mixedSubmesh, mixedSubmesh, opaqueSubmesh,
                        },
                        firstTriangleIndex);
                    firstTriangleIndex += 3;
                }

                var context = AvatarProcessor.ProcessAvatar(
                    root, AmbientPlatform.DefaultPlatform);
                AssertBuiltTrianglesMatchAuthoring(context, root);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
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

        /// <summary>
        /// Creates one AAO-shaped generated atlas: a banded RGBA32
        /// mipmapped texture as a sub-asset of an NDMF SubAssetContainer,
        /// named the way Avatar Optimizer's texture packing names its
        /// outputs, so the shipped capture admits it through the
        /// generated route with banded alpha.
        /// </summary>
        private Texture2D CreateGeneratedAtlas(string name)
        {
            const int size = 128;
            var containerPath = TempFolder + "/" + name + "_container.asset";
            var container = ScriptableObject.CreateInstance<
                nadena.dev.ndmf.runtime.SubAssetContainer>();
            AssetDatabase.CreateAsset(container, containerPath);
            var atlas = new Texture2D(size, size, TextureFormat.RGBA32, true);
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

            atlas.SetPixels32(pixels);
            atlas.Apply(false, false);
            atlas.name = name + " (AAO UV Packed)";
            AssetDatabase.AddObjectToAsset(atlas, containerPath);
            AssetDatabase.SaveAssets();
            return Track(atlas);
        }

        // --- observation asserts (oracle in MergedConsumptionFixture) -------

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
    }
}
