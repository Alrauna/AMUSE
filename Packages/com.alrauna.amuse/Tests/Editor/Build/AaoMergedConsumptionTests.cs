using System;
using System.Collections.Generic;
using System.IO;
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
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The real-optimizer reproduction: a VRChat avatar with the AMUSE
    /// component and Avatar Optimizer's Trace and Optimize at its defaults
    /// (Merge Skinned Mesh enabled), through the full production pipeline
    /// with the attested vendor lilToon transparent shader.
    /// <para>
    /// The soundness contract under test: after the build, every triangle
    /// that ended up on a generated opaque material samples only fully
    /// opaque texels of its source texture. A triangle over a fully
    /// transparent or partially transparent texel must stay on its original
    /// material. This is the assertion the merged-renderer investigation
    /// needed: it fails on any implementation that proves polygons from
    /// evidence that does not exist.
    /// </para>
    /// <para>
    /// Both optimizer packages are discovered at runtime and the test
    /// ignores loudly when either is absent, so the public suite stays
    /// green on a fresh clone.
    /// </para>
    /// </summary>
    public sealed class AaoMergedConsumptionTests
    {
        private const string TempFolder = "Assets/AmuseTests_AaoMerge";
        private const string LilToonTransparentShaderName =
            "Hidden/lilToonTransparent";
        private const string TraceAndOptimizeTypeName =
            "Anatawa12.AvatarOptimizer.TraceAndOptimize,"
            + "com.anatawa12.avatar-optimizer.runtime";

        private readonly List<UnityEngine.Object> tracked =
            new List<UnityEngine.Object>();

        private Texture2D quadrantTexture;
        private Material dressMaterialA;
        private Material dressMaterialB;

        private T Track<T>(T asset) where T : UnityEngine.Object
        {
            tracked.Add(asset);
            return asset;
        }

        [TearDown]
        public void TearDown()
        {
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
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        [Test]
        public void MergedRendererKeepsNonOpaquePolygonsOnTheirOriginalMaterials()
        {
            var traceAndOptimizeType = Type.GetType(TraceAndOptimizeTypeName);
            var transparentShader = Shader.Find(LilToonTransparentShaderName);
            if (traceAndOptimizeType == null || transparentShader == null)
            {
                Assert.Ignore(
                    "The reproduction needs com.anatawa12.avatar-optimizer"
                    + " and jp.lilxyzw.liltoon installed; one of them is"
                    + " missing, so the merged-renderer consumption cannot"
                    + " be exercised in this project.");
            }

            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");
            quadrantTexture = Track(ImportQuadrantAlphaTexture());
            dressMaterialA = Track(NewTransparentMaterial(transparentShader));
            dressMaterialB = Track(NewTransparentMaterial(transparentShader));

            var root = new GameObject("AMUSE AAO merged consumption");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                root.AddComponent(traceAndOptimizeType);
                var controller = Track(
                    UnityEditor.Animations.AnimatorController
                        .CreateAnimatorControllerAtPath(
                            TempFolder + "/reproduction.controller"));
                root.AddComponent<Animator>().runtimeAnimatorController =
                    controller;

                // Two renderers, two distinct transparent materials: the
                // configuration the investigation observed, where the merge
                // combines renderers and AMUSE must keep its per-material
                // split behavior unchanged.
                CreateTexturedRenderer(root, dressMaterialA);
                CreateTexturedRenderer(root, dressMaterialB);

                var context = AvatarProcessor.ProcessAvatar(
                    root, AmbientPlatform.DefaultPlatform);
                ObserveAndAssertBuiltAvatar(context, root);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

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
            var shader = Shader.Find("Hidden/lilToonTransparent");
            Assume.That(shader, Is.Not.Null, "lilToon not installed");
            var material = Track(NewTransparentMaterial(shader));

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
            var shader = Shader.Find("Hidden/lilToonTransparent");
            Assume.That(shader, Is.Not.Null, "lilToon not installed");

            var root = new GameObject("AMUSE evidence probe root");
            Track(root);
            FixtureAvatarIdentity.AttachVrcDescriptor(root);
            var controller = Track(
                UnityEditor.Animations.AnimatorController
                    .CreateAnimatorControllerAtPath(
                        TempFolder + "/evidence-probe.controller"));
            root.AddComponent<Animator>().runtimeAnimatorController =
                controller;
            var material = Track(NewTransparentMaterial(shader));
            CreateTexturedRenderer(root, material);
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

        // --- fixture construction -------------------------------------------

        private void CreateTexturedRenderer(
            GameObject root, Material material)
        {
            // Production avatars carry one renderer per child object, and
            // the installed optimizers patch single-object component
            // stacking, so the fixture follows the production shape.
            var holder = new GameObject("AMUSE AAO fixture renderer");
            holder.transform.SetParent(root.transform, false);
            var renderer = holder.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh { name = "AMUSE AAO fixture mesh" };
            Track(mesh);

            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(2f, 0f, 0f),
                new Vector3(3f, 0f, 0f),
                new Vector3(2f, 1f, 0f),
                new Vector3(4f, 0f, 0f),
                new Vector3(5f, 0f, 0f),
                new Vector3(4f, 1f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0.10f, 0.25f),
                new Vector2(0.30f, 0.25f),
                new Vector2(0.20f, 0.45f),
                new Vector2(0.60f, 0.25f),
                new Vector2(0.90f, 0.25f),
                new Vector2(0.75f, 0.45f),
                new Vector2(0.40f, 0.70f),
                new Vector2(0.55f, 0.70f),
                new Vector2(0.47f, 0.90f),
            };
            mesh.SetTriangles(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, 0);
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = new[] { material };
        }

        private Material NewTransparentMaterial(Shader shader)
        {
            var material = new Material(shader);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            // The transparent frontend proves the plain main-alpha clip
            // only at or below its provable cutoff boundary; the vendor
            // shader's 0.5 default sits above it and the alpha would stay
            // unknown. The lab avatar's materials were authored at zero.
            material.SetFloat("_Cutoff", 0f);
            // Mask mode 2 (multiply) with scale 1 and value 1: the pinned
            // vendor source computes saturate(mask.r * 1 + 1), which is
            // exactly one for every texel, so the mask term is inert and
            // the alpha is the plain main texture sample.
            material.SetFloat("_AlphaMaskMode", 2f);
            material.SetFloat("_AlphaMaskScale", 1f);
            material.SetFloat("_AlphaMaskValue", 1f);
            material.SetTexture("_AlphaMask", quadrantTexture);
            material.SetTexture("_MainTex", quadrantTexture);
            return material;
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
            // would silently classify every triangle all-unknown. The
            // quadrant edge stays on the texel grid at every mip level,
            // including the default mip cap of four.
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

            var cpu = new Texture2D(size, size, TextureFormat.RGBA32, false);
            cpu.SetPixels32(pixels);
            cpu.Apply(false, false);
            var encoded = cpu.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(cpu);

            var path = TempFolder + "/quadrant_alpha.png";
            File.WriteAllBytes(path, encoded);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            importer.mipmapEnabled = true;
            importer.isReadable = true;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture, Is.Not.Null, "fixture precondition");
            return texture;
        }

        // --- observation and the soundness contract -------------------------

        /// <summary>
        /// Reads the built avatar and asserts the soundness contract: every
        /// triangle on a generated opaque material samples only fully
        /// opaque texels. Generated materials come from the separation
        /// state's own created-clone record, never from name heuristics.
        /// <para>
        /// Avatar Optimizer's own Initial Step reports an
        /// ArgumentNullException on minimal fixtures (empty animator
        /// controller), which marks the build unsuccessful even though
        /// every pass, including the AMUSE platform-finish passes, still
        /// runs. The build's success flag therefore cannot gate this
        /// reproduction; the soundness assertions below are the gate.
        /// </para>
        /// </summary>
        private void ObserveAndAssertBuiltAvatar(
            BuildContext context, GameObject root)
        {
            Assert.That(context, Is.Not.Null, "the build must produce context");

            var state = context.GetState<AmusePlatformFinishState>();
            Assert.That(state, Is.Not.Null);
            Assert.That(
                state.AnalyzedRendererCount,
                Is.GreaterThanOrEqualTo(1),
                "AMUSE must analyze the avatar after the optimizer merge; a"
                + " zero means the passes did not run on this platform or"
                + " an avatar-scope gate refused: reachedAnalysis="
                + state.ReachedRendererAnalysis
                + " avatarRefusal=" + state.AvatarRefusal
                + " lifecycle=" + (state.Lifecycle != null
                    ? state.Lifecycle.ToString()
                    : "<null>")
                + " consentDeclined=" + state.ConsentDeclined
                + " alphaPolicyActive=" + state.AlphaPolicyActive
                + " refusedRenderers=" + state.SemanticallyRefusedRendererCount
                + DescribeRendererRefusals(state)
                + " builtRenderers=" + DescribeBuiltRenderers(root));

            var generated = new HashSet<Material>(
                state.Separation?.CreatedClones
                ?? (IEnumerable<Material>)Array.Empty<Material>());
            Assert.That(
                generated,
                Is.Not.Empty,
                "fixture expectation: the fixture's opaque triangle is"
                + " proven and converted; zero clones means the pipeline"
                + " refused the renderer instead. Refusal buckets: "
                + DescribeSlotRefusals(state)
                + " analyzed=" + state.AnalyzedRendererCount
                + " refusedRenderers=" + state.SemanticallyRefusedRendererCount
                + " opaqueCandidates=" + state.OpaqueCandidateTriangleCount
                + " separation=" + DescribeSeparation(state)
                + " builtRenderers=" + DescribeBuiltRenderers(root));


            var renderers =
                root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var movedSubmeshTriangles =
                new List<(Vector2 a, Vector2 b, Vector2 c)>();
            var keptSubmeshTriangleCount = 0;
            foreach (var renderer in renderers)
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null) continue;

                var materials = renderer.sharedMaterials;
                var uvs = mesh.uv;
                if (materials == null || uvs == null
                    || materials.Length == 0) continue;

                // The optimizers may rewrite the mesh and the slot list;
                // the loop reads only the slots that survive with a
                // material, never past either array.
                var subMeshCount = Math.Min(
                    mesh.subMeshCount, materials.Length);
                for (var subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    var material = materials[subMesh];
                    var isGenerated = material != null
                        && generated.Contains(material);
                    var triangles = mesh.GetTriangles(subMesh);
                    for (var t = 0; t < triangles.Length; t += 3)
                    {
                        var a = uvs[triangles[t]];
                        var b = uvs[triangles[t + 1]];
                        var c = uvs[triangles[t + 2]];
                        if (isGenerated)
                        {
                            movedSubmeshTriangles.Add((a, b, c));
                        }
                        else
                        {
                            keptSubmeshTriangleCount++;
                        }
                    }
                }
            }

            foreach (var (a, b, c) in movedSubmeshTriangles)
            {
                Assert.That(
                    SamplesOnlyOpaqueTexels(a)
                    && SamplesOnlyOpaqueTexels(b)
                    && SamplesOnlyOpaqueTexels(c),
                    Is.True,
                    "a triangle over non-opaque texels moved to a generated"
                    + " opaque material: uv corner " + a + " violates the"
                    + " quadrant contract");
            }

            // The fixture authors at least one hole triangle and one
            // boundary triangle; if every kept triangle vanished from the
            // accounting, the assertion above would prove nothing.
            Assert.That(
                keptSubmeshTriangleCount + movedSubmeshTriangles.Count,
                Is.GreaterThanOrEqualTo(3),
                "fixture precondition: the built avatar must still carry"
                + " the fixture triangles");
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
        private static string DescribeSeparation(AmusePlatformFinishState state)
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

        /// <summary>
        /// The quadrant rule: uv x below one half samples the fully opaque
        /// half of the fixture texture. A triangle corner exactly on the
        /// boundary samples both halves under bilinear filtering, so it
        /// counts as non-opaque evidence.
        /// </summary>
        private static bool SamplesOnlyOpaqueTexels(Vector2 uv)
        {
            return uv.x < 0.5f;
        }
    }
}
