using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Semantics.LilToon;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[assembly: ExportsPlugin(typeof(
    Alrauna.Amuse.Tests.Editor.Build
        .AlphaSeparationApplyTests.AlphaSeparationApplyTestPlugin))]

[assembly: ExportsPlugin(typeof(
    Alrauna.Amuse.Tests.Editor.Build
        .AlphaSeparationApplyTests.AlphaSeparationSeamPlugin))]

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The third pass's validation, finalization, sweep and apply behaviour,
    /// driven through real NDMF builds.
    /// <para>
    /// Most falsifiers run the production three-pass lifecycle on a
    /// test-local plugin whose barrier substitutes only the verified
    /// conversion seam. Falsifiers 4, 15 and 17 cannot call
    /// <c>PrepareSurvivingSet</c> outside a pass — it reads the reactivated
    /// <c>AnimationIndex</c> — so they drive it through a second test-local
    /// plugin whose third pass calls the production method exactly once and
    /// never applies, the same lifecycle route
    /// <c>AnimatorServicesReactivationCharacterizationTests</c> establishes.
    /// </para>
    /// </summary>
    public sealed class AlphaSeparationApplyTests
    {
        private readonly List<UnityEngine.Object> tracked =
            new List<UnityEngine.Object>();

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null)
            {
                tracked.Add(obj);
            }

            return obj;
        }

        [TearDown]
        public void TearDown()
        {
            DestroyTracked();
        }

        private void DestroyTracked()
        {
            for (var index = tracked.Count - 1; index >= 0; index--)
            {
                if (tracked[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(tracked[index]);
                }
            }

            tracked.Clear();
        }

        // --- Test-local plugins, platforms and the seam probe --------------

        internal const string ApplyTestPlatformName =
            "com.alrauna.amuse.tests.alpha-separation-apply";

        internal const string SeamTestPlatformName =
            "com.alrauna.amuse.tests.alpha-separation-seam";

        internal sealed class ApplyTestPlatform : INDMFPlatformProvider
        {
            internal static readonly ApplyTestPlatform Instance =
                new ApplyTestPlatform();

            public string QualifiedName => ApplyTestPlatformName;
            public string DisplayName => "AMUSE alpha separation apply";
        }

        internal sealed class SeamTestPlatform : INDMFPlatformProvider
        {
            internal static readonly SeamTestPlatform Instance =
                new SeamTestPlatform();

            public string QualifiedName => SeamTestPlatformName;
            public string DisplayName => "AMUSE alpha separation seam";
        }

        /// <summary>
        /// The production-like apply lifecycle: bindings capture under the
        /// active extension, the extension-free barrier, an inert probe pass
        /// between barrier and third pass, then the real
        /// <see cref="AlphaSeparationApply.Execute"/> under a reactivated
        /// extension. The production plugin runs on the VRChat platform only,
        /// so on this dedicated platform it never runs and this plugin is the
        /// whole lifecycle.
        /// </summary>
        [RunsOnPlatforms(ApplyTestPlatformName)]
        public sealed class AlphaSeparationApplyTestPlugin :
            Plugin<AlphaSeparationApplyTestPlugin>
        {
            public override string QualifiedName =>
                "com.alrauna.amuse.tests.alpha-separation-apply-plugin";

            protected override void Configure()
            {
                var sequence = InPhase(BuildPhase.PlatformFinish);

                sequence.WithRequiredExtension(
                    typeof(AnimatorServicesContext),
                    inner => inner.Run(
                        "AMUSE test bindings capture", CapturePass));

                sequence.Run("AMUSE test barrier", BarrierPass);

                sequence.Run("AMUSE test armed probe", ProbePass);

                sequence.WithRequiredExtension(
                    typeof(AnimatorServicesContext),
                    inner => inner.Run(
                        AlphaSeparationApply.PassName,
                        ctx => AlphaSeparationApply.Execute(ctx)));
            }
        }

        /// <summary>
        /// The seam lifecycle for the falsifiers that must observe prepared
        /// state across <c>PrepareSurvivingSet</c>: the third pass calls the
        /// production method exactly once under the reactivated extension and
        /// returns without ever calling <c>ApplyFinalization</c>, so nothing
        /// writes to the build avatar after it.
        /// </summary>
        [RunsOnPlatforms(SeamTestPlatformName)]
        public sealed class AlphaSeparationSeamPlugin :
            Plugin<AlphaSeparationSeamPlugin>
        {
            public override string QualifiedName =>
                "com.alrauna.amuse.tests.alpha-separation-seam-plugin";

            protected override void Configure()
            {
                var sequence = InPhase(BuildPhase.PlatformFinish);

                sequence.WithRequiredExtension(
                    typeof(AnimatorServicesContext),
                    inner => inner.Run(
                        "AMUSE test bindings capture", CapturePass));

                sequence.Run("AMUSE test barrier", BarrierPass);

                sequence.Run("AMUSE test armed probe", ProbePass);

                sequence.WithRequiredExtension(
                    typeof(AnimatorServicesContext),
                    inner => inner.Run(
                        "AMUSE test preparation seam", SeamPass));
            }

            private static void SeamPass(BuildContext context)
            {
                var state = context.GetState<AmusePlatformFinishState>();
                var probe = context.GetState<AlphaSeparationSeamProbe>();
                probe.State = state;

                probe.RendererDigestBefore = DigestRenderers(
                    context.AvatarRootObject);
                probe.CurveDigestBefore = DigestCurves(context, state);

                // Recorded before prepare: the sweep that prepare runs destroys
                // unreferenced transients, so the references must be taken
                // while they are still alive.
                if (state.Separation != null)
                {
                    probe.RecordedClones.AddRange(state.Separation.CreatedClones);
                    probe.RecordedMeshClones.AddRange(
                        state.Separation.Renderers.Select(
                            prepared => prepared.MeshClone));
                }

                probe.Decision = AlphaSeparationApply.PrepareSurvivingSet(
                    context, state, out var finalization);
                probe.Finalization = finalization;

                probe.RendererDigestAfter = DigestRenderers(
                    context.AvatarRootObject);
                probe.CurveDigestAfter = DigestCurves(context, state);
            }
        }

        public sealed class AlphaSeparationSeamProbe
        {
            internal AmusePlatformFinishState State { get; set; }
            internal AmusePreparationDecision Decision { get; set; }
            internal AlphaSeparationFinalization Finalization { get; set; }

            public readonly List<Material> RecordedClones =
                new List<Material>();

            public readonly List<Mesh> RecordedMeshClones =
                new List<Mesh>();

            public string RendererDigestBefore { get; set; }
            public string RendererDigestAfter { get; set; }
            public string CurveDigestBefore { get; set; }
            public string CurveDigestAfter { get; set; }

            internal int SlotRefusals(AlphaSeparationSlotRefusal reason)
            {
                return State.SlotRefusalCount(reason);
            }
        }

        // --- Test-local pass bodies and scopes ------------------------------

        /// <summary>
        /// The real bindings capture pass, followed by the marker-stub
        /// override: when a test arms special clip names, the barrier is
        /// handed a stub host bindings that reports those clips as special
        /// motions. The public project installs no VRChat SDK, so no real
        /// proxy clip can exist and the live marker route is unreachable;
        /// this stub exercises the barrier-side marker refusal.
        /// </summary>
        private static void CapturePass(BuildContext context)
        {
            AmuseAnimatorBindingsCapture.Execute(context);
            if (SpecialClipNames.Count > 0)
            {
                context.GetState<AmusePlatformFinishState>().AnimatorBindings =
                    new MarkerStubBindings();
            }
        }

        /// <summary>
        /// The real extension-free barrier through the public-fixture seams.
        /// The conversion delegate defaults to the family-routing verified
        /// seam; a test that must substitute a different conversion
        /// boundary — e.g. an identity mapping — arms
        /// <see cref="ConversionOverride"/> for the duration of its build,
        /// the same way <see cref="SpecialClipNames"/> works.
        /// </summary>
        private static void BarrierPass(BuildContext context)
        {
            AmusePlatformFinishPass.Execute(
                context,
                SupportedFacts(),
                VerifiedLilToonTestSeams.SelectVerifiedFixtureRequest,
                VerifiedLilToonTestSeams.CaptureVerifiedFixtureMaterials,
                VerifiedLilToonTestSeams.VerifiedAlphaOnly,
                ConversionOverride ??
                    VerifiedPoiyomiTestSeams.VerifiedConversion,
                VerifiedFamilyConversion);
        }

        /// <summary>
        /// Routes the family-agnostic lilToon conversion seam parameter to
        /// the family-specific verified seam function by fixture shader
        /// reference: the transparent stand-in to
        /// <see cref="VerifiedLilToonTestSeams.VerifiedTransparentConversionStep"/>,
        /// everything else — the cutout stand-in first of all — to
        /// <see cref="VerifiedLilToonTestSeams.VerifiedConversion"/>, whose
        /// behavior for those materials is exactly what every pre-existing
        /// expectation here was written against.
        /// </summary>
        private static bool VerifiedFamilyConversion(
            Material live,
            CapturedMaterialEvidence derived,
            Material preparedOpaque,
            out Material opaque,
            out LilToonOpaqueConversionRefusal refusal)
        {
            if (live != null && live.shader == Shader.Find(
                    LilToonConversionShaderNames.Transparent))
            {
                return VerifiedLilToonTestSeams
                    .VerifiedTransparentConversionStep(
                        live, derived, preparedOpaque,
                        out opaque, out refusal);
            }

            return VerifiedLilToonTestSeams.VerifiedConversion(
                live, derived, preparedOpaque, out opaque, out refusal);
        }

        private static VerifiedPoiyomiConversion ConversionOverride
        {
            get;
            set;
        }

        private sealed class ConversionOverrideScope : IDisposable
        {
            private readonly VerifiedPoiyomiConversion previous;

            internal ConversionOverrideScope(
                VerifiedPoiyomiConversion conversion)
            {
                previous = ConversionOverride;
                ConversionOverride = conversion;
            }

            public void Dispose()
            {
                ConversionOverride = previous;
            }
        }

        /// <summary>
        /// The armed probe pass between the barrier and the third pass. The
        /// action runs extension-free, so it can only touch renderer
        /// components and committed animator assets — exactly the
        /// foreign-pass interference the late-validation design must survive.
        /// </summary>
        private static void ProbePass(BuildContext context)
        {
            ProbeAction?.Invoke(context);
        }

        private static HostLifecycleFacts SupportedFacts()
        {
            return new HostLifecycleFacts(
                "2022.3.22f1",
                "1.14.4",
                "3.10.4",
                "3.10.4",
                WellKnownPlatforms.VRChatAvatar30,
                AmuseBuildPath.NonPlayNdmfBuild,
                hasAssetSaver: true,
                hasAssetContainer: true,
                hasObjectRegistry: true,
                hasErrorReport: true);
        }

        private static IReadOnlyCollection<string> SpecialClipNames
        {
            get;
            set;
        } = Array.Empty<string>();

        private sealed class MarkerBindingsScope : IDisposable
        {
            private readonly IReadOnlyCollection<string> previous;

            internal MarkerBindingsScope(params string[] clipNames)
            {
                previous = SpecialClipNames;
                SpecialClipNames = clipNames;
            }

            public void Dispose()
            {
                SpecialClipNames = previous;
            }
        }

        private sealed class MarkerStubBindings : IPlatformAnimatorBindings
        {
            public bool IsSpecialMotion(Motion motion)
            {
                return motion is AnimationClip clip &&
                       SpecialClipNames.Contains(clip.name);
            }

            public IEnumerable<(object, RuntimeAnimatorController, bool)>
                GetInnateControllers(GameObject root)
            {
                return GenericPlatformAnimatorBindings.Instance
                    .GetInnateControllers(root);
            }

            public void CommitControllers(
                GameObject root,
                IDictionary<object, RuntimeAnimatorController> controllers)
            {
                GenericPlatformAnimatorBindings.Instance.CommitControllers(
                    root, controllers);
            }
        }

        private static Action<BuildContext> ProbeAction { get; set; }

        private sealed class ProbeScope : IDisposable
        {
            private readonly Action<BuildContext> previous;

            internal ProbeScope(Action<BuildContext> action)
            {
                previous = ProbeAction;
                ProbeAction = action;
            }

            public void Dispose()
            {
                ProbeAction = previous;
            }
        }

        // --- Digests -------------------------------------------------------

        /// <summary>
        /// A structural digest of every renderer on the avatar: animation
        /// path, shared mesh identity, and the full shared-materials array by
        /// name and instance id. It is stable under no mutation and sensitive
        /// to any mesh or material-slot change.
        /// </summary>
        private static string DigestRenderers(GameObject avatarRoot)
        {
            var parts = new List<string>();
            foreach (var renderer in avatarRoot
                         .GetComponentsInChildren<Renderer>(true))
            {
                var path = AnimationUtility.CalculateTransformPath(
                    renderer.transform, avatarRoot.transform);
                var mesh = renderer is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh
                    : null;
                var materials = renderer.sharedMaterials
                    ?? Array.Empty<Material>();
                parts.Add(
                    path + "|mesh=" + DescribeObject(mesh) + "|materials=" +
                    string.Join(",", materials.Select(DescribeObject)));
            }

            return string.Join(";;", parts);
        }

        /// <summary>
        /// A digest of every live object-reference curve the reactivated
        /// index associates with the prepared renderers' paths.
        /// </summary>
        private static string DigestCurves(
            BuildContext context,
            AmusePlatformFinishState state)
        {
            if (state.Separation == null)
            {
                return string.Empty;
            }

            var index = context.Extension<AnimatorServicesContext>()
                .AnimationIndex;
            var parts = new List<string>();
            foreach (var prepared in state.Separation.Renderers)
            {
                foreach (var clip in index
                             .GetClipsForObjectPath(prepared.RendererPath)
                             .ToList())
                {
                    foreach (var binding in clip.GetObjectCurveBindings())
                    {
                        parts.Add(
                            prepared.RendererPath + "|" +
                            clip.Name + "|" +
                            binding.path + "|" +
                            binding.type.FullName + "|" +
                            binding.propertyName + "|" +
                            DescribeCurve(clip.GetObjectCurve(binding)));
                    }
                }
            }

            return string.Join(";;", parts);
        }

        private static string DescribeCurve(ObjectReferenceKeyframe[] curve)
        {
            if (curve == null)
            {
                return "<null>";
            }

            return string.Join("|", curve.Select(key =>
                key.time.ToString("R") + "=>" + DescribeObject(
                    key.value as UnityEngine.Object)));
        }

        private static string DescribeObject(UnityEngine.Object obj)
        {
            return obj == null
                ? "<null>"
                : obj.name + "#" + obj.GetInstanceID();
        }

        // --- Fixture helpers -----------------------------------------------

        private static SkinnedMeshRenderer AddRenderer(
            GameObject root,
            string name,
            Mesh mesh,
            params Material[] materials)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = materials;
            return renderer;
        }

        private static AnimationClip NewSwapClip(
            string name,
            string rendererPath,
            int slotIndex,
            params (float time, Material value)[] keys)
        {
            var clip = new AnimationClip { name = name };
            var keyframes = new ObjectReferenceKeyframe[keys.Length];
            for (var index = 0; index < keys.Length; index++)
            {
                keyframes[index] = new ObjectReferenceKeyframe
                {
                    time = keys[index].time,
                    value = keys[index].value,
                };
            }

            AnimationUtility.SetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(
                    rendererPath,
                    typeof(SkinnedMeshRenderer),
                    "m_Materials.Array.data[" + slotIndex + "]"),
                keyframes);
            return clip;
        }

        private static AnimatorController NewController(
            GameObject root,
            string name,
            params AnimationClip[] clips)
        {
            var controller = new AnimatorController { name = name };
            controller.AddLayer("L0");
            for (var index = 0; index < clips.Length; index++)
            {
                controller.layers[0].stateMachine
                    .AddState("S" + index).motion = clips[index];
            }

            root.AddComponent<Animator>().runtimeAnimatorController =
                controller;
            return controller;
        }

        private static AnimationClip CommittedClipWithObjectBinding(
            GameObject root,
            string rendererPath,
            string propertyName)
        {
            foreach (var animator in root
                         .GetComponentsInChildren<Animator>(true))
            {
                if (!(animator.runtimeAnimatorController is
                        AnimatorController controller))
                {
                    continue;
                }

                foreach (var layer in controller.layers)
                {
                    foreach (var child in layer.stateMachine.states)
                    {
                        if (!(child.state.motion is AnimationClip clip))
                        {
                            continue;
                        }

                        foreach (var binding in AnimationUtility
                                     .GetObjectReferenceCurveBindings(clip))
                        {
                            if (binding.path == rendererPath &&
                                binding.propertyName == propertyName)
                            {
                                return clip;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static string DescribeAuthoredCurve(
            AnimationClip clip,
            string rendererPath,
            string propertyName)
        {
            var curve = AnimationUtility.GetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(
                    rendererPath, typeof(SkinnedMeshRenderer),
                    propertyName));
            if (curve == null)
            {
                return "<null>";
            }

            return string.Join("|", curve.Select(key =>
                key.time.ToString("R") + "=>" +
                (key.value == null ? "null" : key.value.name)));
        }

        private static string CommittedCurve(
            GameObject root,
            string rendererPath,
            string propertyName)
        {
            var committed = CommittedClipWithObjectBinding(
                root, rendererPath, propertyName);
            Assert.That(committed, Is.Not.Null,
                "no committed clip carried the binding under test");
            return DescribeAuthoredCurve(committed, rendererPath,
                propertyName);
        }

        private static void DestroyGenerated(AmusePlatformFinishState state)
        {
            if (state?.Separation == null)
            {
                return;
            }

            foreach (var clone in state.Separation.CreatedClones)
            {
                if (clone != null)
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                }
            }

            foreach (var prepared in state.Separation.Renderers)
            {
                if (prepared.MeshClone != null)
                {
                    UnityEngine.Object.DestroyImmediate(prepared.MeshClone);
                }
            }
        }

        private static void DestroyCommittedClone(
            GameObject root,
            AnimatorController original)
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                return;
            }

            var committed = animator.runtimeAnimatorController;
            if (committed == null || ReferenceEquals(committed, original))
            {
                return;
            }

            animator.runtimeAnimatorController = null;
            if (committed is AnimatorController controller)
            {
                foreach (var layer in controller.layers)
                {
                    UnityEngine.Object.DestroyImmediate(layer.stateMachine);
                }
            }

            UnityEngine.Object.DestroyImmediate(committed);
        }

        private static void AssertNoFeatureRefusals(
            AmusePlatformFinishState state)
        {
            foreach (AlphaSeparationSlotRefusal reason in Enum.GetValues(
                         typeof(AlphaSeparationSlotRefusal)))
            {
                if (reason == AlphaSeparationSlotRefusal.None)
                {
                    continue;
                }

                Assert.That(
                    state.SlotRefusalCount(reason), Is.Zero,
                    "unexpected feature refusal bucket " + reason);
            }
        }

        private static Material VerifiedOpaqueMaterial()
        {
            var material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
            material.SetFloat("_AlphaForceOpaque", 1f);
            return material;
        }

        private static Material VerifiedTransparentMaterial()
        {
            var material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
            return material;
        }

        private static Mesh SingleTriangleMesh()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero, Vector3.right, Vector3.up,
                },
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            return mesh;
        }

        private static Mesh TwoTriangleMesh()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero, Vector3.right, Vector3.up,
                    new Vector3(2f, 0f, 0f),
                    new Vector3(3f, 0f, 0f),
                    new Vector3(2f, 1f, 0f),
                },
                subMeshCount = 2,
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 3, 4, 5 }, 1);
            return mesh;
        }

        // --- Falsifier 1: wholly opaque slot -------------------------------

        [Test]
        public void WhollyOpaqueSlotReplacesOnlyItsMaterial()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE wholly opaque apply");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AmusePlatformFinishState state = null;
            try
            {
                var material = Track(VerifiedOpaqueMaterial());
                var mesh = Track(SingleTriangleMesh());
                var renderer = AddRenderer(root, "body", mesh, material);

                var context = AvatarProcessor.ProcessAvatar(
                    root, ApplyTestPlatform.Instance);
                state = context.GetState<AmusePlatformFinishState>();

                Assert.That(state.SemanticallyRefusedRendererCount, Is.Zero,
                    "fixture precondition: the renderer must analyze");
                Assert.That(state.Separation, Is.Not.Null,
                    "fixture precondition: the slot must be prepared");
                Assert.That(
                    state.Separation.CreatedClones, Has.Count.EqualTo(1),
                    "fixture precondition: the material must convert, or " +
                    "the applied result proves nothing");

                var clone = state.Separation.OpaqueBySource[material];
                Assert.That(renderer.sharedMaterials[0], Is.SameAs(clone),
                    "the wholly opaque slot must carry the opaque result");
                Assert.That(renderer.sharedMesh, Is.SameAs(mesh),
                    "a wholly opaque slot must not touch the mesh");
                Assert.That(mesh.subMeshCount, Is.EqualTo(1),
                    "a wholly opaque slot must not change the submesh count");
                Assert.That(state.AppliedRendererCount, Is.EqualTo(1));
                Assert.That(state.AppliedOpaqueTriangleCount, Is.EqualTo(1));
                AssertNoFeatureRefusals(state);
            }
            finally
            {
                DestroyGenerated(state);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // --- Falsifier 6: every swap value maps at identical times ---------

        [Test]
        public void EveryMaterialSwapValueMapsAtIdenticalKeyframeTimes()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE swap mapping");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AmusePlatformFinishState state = null;
            AnimatorController controller = null;
            try
            {
                var first = Track(VerifiedOpaqueMaterial());
                var second = Track(VerifiedOpaqueMaterial());
                var mesh = Track(SingleTriangleMesh());
                var renderer = AddRenderer(root, "body", mesh, first);

                var clip = new AnimationClip { name = "AMUSE swap" };
                AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    EditorCurveBinding.PPtrCurve(
                        "body",
                        typeof(SkinnedMeshRenderer),
                        "m_Materials.Array.data[0]"),
                    new[]
                    {
                        new ObjectReferenceKeyframe
                        {
                            time = 0f, value = first,
                        },
                        new ObjectReferenceKeyframe
                        {
                            time = 0.25f, value = second,
                        },
                        new ObjectReferenceKeyframe
                        {
                            time = 1.5f, value = first,
                        },
                    });
                controller = NewController(root, "AMUSE swap graph", clip);

                var context = AvatarProcessor.ProcessAvatar(
                    root, ApplyTestPlatform.Instance);
                state = context.GetState<AmusePlatformFinishState>();

                var slot = state.Separation.Renderers[0].CandidateSlots[0];
                Assert.That(
                    slot.OpaqueOfAdmitted, Has.Count.EqualTo(2),
                    "fixture precondition: both swap values must map, or " +
                    "the rewritten curve proves nothing");

                var firstOpaque = slot.OpaqueOfAdmitted[first];
                var secondOpaque = slot.OpaqueOfAdmitted[second];
                Assert.That(firstOpaque, Is.Not.SameAs(secondOpaque),
                    "fixture precondition: the two opaque results must be " +
                    "distinct clones");

                var committedCurve = AnimationUtility.GetObjectReferenceCurve(
                    CommittedClipWithObjectBinding(
                        root, "body", "m_Materials.Array.data[0]"),
                    EditorCurveBinding.PPtrCurve(
                        "body",
                        typeof(SkinnedMeshRenderer),
                        "m_Materials.Array.data[0]"));
                Assert.That(committedCurve, Has.Length.EqualTo(3));
                Assert.That(
                    new[]
                    {
                        committedCurve[0].time.ToString("R"),
                        committedCurve[1].time.ToString("R"),
                        committedCurve[2].time.ToString("R"),
                    },
                    Is.EqualTo(new[]
                    {
                        0f.ToString("R"),
                        0.25f.ToString("R"),
                        1.5f.ToString("R"),
                    }),
                    "the keyframe times must be preserved exactly");
                Assert.That(committedCurve[0].value, Is.SameAs(firstOpaque));
                Assert.That(committedCurve[1].value, Is.SameAs(secondOpaque));
                Assert.That(committedCurve[2].value, Is.SameAs(firstOpaque),
                    "the same source material must always map to the same " +
                    "opaque result");
                Assert.That(renderer.sharedMaterials[0],
                    Is.SameAs(firstOpaque),
                    "the current assignment maps through the same opaque " +
                    "result");
                AssertNoFeatureRefusals(state);
            }
            finally
            {
                DestroyCommittedClone(root, controller);
                DestroyGenerated(state);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // --- Falsifier 7: an unmapped live value invalidates only its slot --

        [Test]
        public void UnmappedLiveValueInvalidatesOnlyItsSlot()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE unmapped value");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AmusePlatformFinishState state = null;
            AnimatorController controller = null;
            try
            {
                var first = Track(VerifiedOpaqueMaterial());
                var second = Track(VerifiedOpaqueMaterial());
                var swapTarget = Track(VerifiedOpaqueMaterial());
                var mesh = Track(TwoTriangleMesh());
                var renderer = AddRenderer(root, "body", mesh, first, second);

                var clip = Track(new AnimationClip { name = "AMUSE swaps" });
                AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    EditorCurveBinding.PPtrCurve(
                        "body", typeof(SkinnedMeshRenderer),
                        "m_Materials.Array.data[0]"),
                    new[]
                    {
                        new ObjectReferenceKeyframe
                        {
                            time = 0f, value = swapTarget,
                        },
                    });
                controller = NewController(root, "AMUSE swaps graph", clip);

                var foreign = Track(VerifiedOpaqueMaterial());
                using (new ProbeScope(_ =>
                {
                    renderer.sharedMaterials = new[] { first, foreign };
                }))
                {
                    var context = AvatarProcessor.ProcessAvatar(
                        root, ApplyTestPlatform.Instance);
                    state = context.GetState<AmusePlatformFinishState>();
                }

                Assert.That(
                    state.SlotRefusalCount(
                        AlphaSeparationSlotRefusal
                            .RuntimeMaterialValueNotMapped),
                    Is.EqualTo(1),
                    "exactly the replaced slot may refuse");
                foreach (AlphaSeparationSlotRefusal reason in Enum.GetValues(
                             typeof(AlphaSeparationSlotRefusal)))
                {
                    if (reason == AlphaSeparationSlotRefusal.None ||
                        reason == AlphaSeparationSlotRefusal
                            .RuntimeMaterialValueNotMapped)
                    {
                        continue;
                    }

                    Assert.That(
                        state.SlotRefusalCount(reason), Is.Zero,
                        "the sibling must survive beside the refused slot: " +
                        reason);
                }

                var slotZero = state.Separation.Renderers[0]
                    .CandidateSlots.Single(slot =>
                        slot.Plan.SourceMaterialBindingIndex == 0);
                Assert.That(
                    renderer.sharedMaterials[0],
                    Is.SameAs(slotZero.OpaqueOfAdmitted[first]),
                    "the surviving slot must still apply its own current " +
                    "assignment");
                Assert.That(
                    renderer.sharedMaterials[1], Is.SameAs(foreign),
                    "the refused slot must keep exactly the foreign " +
                    "assignment");
                Assert.That(renderer.sharedMesh, Is.SameAs(mesh),
                    "the renderer must keep its source mesh");
                Assert.That(state.AppliedRendererCount, Is.EqualTo(1));

                var committedCurve = CommittedCurve(
                    root, "body", "m_Materials.Array.data[0]");
                Assert.That(committedCurve, Is.EqualTo(
                        "0=>" + slotZero.OpaqueOfAdmitted[swapTarget].name),
                    "the surviving slot's curve must be rewritten");
                Assert.That(
                    CommittedClipWithObjectBinding(
                        root, "body", "m_Materials.Array.data[1]"),
                    Is.Null,
                    "no curve edit may appear for the refused slot");
            }
            finally
            {
                DestroyCommittedClone(root, controller);
                DestroyGenerated(state);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // --- Falsifier 8: marker clip refusal -------------------------------

        [Test]
        public void MarkerClipRefusalIsSlotLocalAndWritesNothingForIt()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            using var marker = new MarkerBindingsScope("AMUSE marked swap");
            var root = new GameObject("AMUSE marker clip");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AmusePlatformFinishState state = null;
            AnimatorController controller = null;
            try
            {
                var markedCurrent = Track(VerifiedOpaqueMaterial());
                var markedSwap = Track(VerifiedOpaqueMaterial());
                var ordinaryCurrent = Track(VerifiedOpaqueMaterial());
                var ordinarySwap = Track(VerifiedOpaqueMaterial());
                var mesh = Track(TwoTriangleMesh());
                var renderer = AddRenderer(
                    root, "marked", mesh, markedCurrent, ordinaryCurrent);

                var markedClip = Track(NewSwapClip(
                    "AMUSE marked swap", "marked", 0,
                    (0f, markedSwap)));
                var ordinaryClip = Track(NewSwapClip(
                    "AMUSE ordinary swap", "marked", 1,
                    (0f, ordinarySwap)));
                controller = NewController(
                    root, "AMUSE marker graph", markedClip, ordinaryClip);

                var authoredMarked = DescribeAuthoredCurve(
                    markedClip, "marked", "m_Materials.Array.data[0]");

                var context = AvatarProcessor.ProcessAvatar(
                    root, ApplyTestPlatform.Instance);
                state = context.GetState<AmusePlatformFinishState>();

                Assert.That(
                    state.SlotRefusalCount(
                        AlphaSeparationSlotRefusal
                            .MarkerClipCarriesSlotBinding),
                    Is.EqualTo(1),
                    "the slot whose binding the special motion carries must " +
                    "be refused");
                foreach (AlphaSeparationSlotRefusal reason in Enum.GetValues(
                             typeof(AlphaSeparationSlotRefusal)))
                {
                    if (reason == AlphaSeparationSlotRefusal.None ||
                        reason == AlphaSeparationSlotRefusal
                            .MarkerClipCarriesSlotBinding)
                    {
                        continue;
                    }

                    Assert.That(
                        state.SlotRefusalCount(reason), Is.Zero,
                        "no other slot may be invalidated: " + reason);
                }

                var survivingSlot = state.Separation.Renderers[0]
                    .CandidateSlots.Single();
                Assert.That(
                    survivingSlot.Plan.SourceMaterialBindingIndex,
                    Is.EqualTo(1),
                    "only the unaffected slot may survive");

                Assert.That(
                    renderer.sharedMaterials[0], Is.SameAs(markedCurrent),
                    "the refused slot's material must be untouched");
                Assert.That(
                    renderer.sharedMaterials[1],
                    Is.SameAs(survivingSlot.OpaqueOfAdmitted[ordinaryCurrent]),
                    "the unaffected slot must still apply its own current " +
                    "assignment");

                Assert.That(
                    CommittedCurve(root, "marked",
                        "m_Materials.Array.data[0]"),
                    Is.EqualTo(authoredMarked),
                    "the marker clip's own committed curve must be unchanged");
                Assert.That(
                    DescribeAuthoredCurve(
                        markedClip, "marked", "m_Materials.Array.data[0]"),
                    Is.EqualTo(authoredMarked),
                    "the source clip asset must be unchanged");
            }
            finally
            {
                DestroyCommittedClone(root, controller);
                DestroyGenerated(state);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // --- Falsifier 20: live current material, not barrier state ---------

        [Test]
        public void UnmappedReplacementBetweenPassesIsPreservedEntirely()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE foreign replacement");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AmusePlatformFinishState state = null;
            try
            {
                var first = Track(VerifiedOpaqueMaterial());
                var second = Track(VerifiedOpaqueMaterial());
                var mesh = Track(TwoTriangleMesh());
                var renderer = AddRenderer(root, "body", mesh, first, second);

                var foreign = Track(VerifiedOpaqueMaterial());
                using (new ProbeScope(_ =>
                {
                    renderer.sharedMaterials = new[] { first, foreign };
                }))
                {
                    var context = AvatarProcessor.ProcessAvatar(
                        root, ApplyTestPlatform.Instance);
                    state = context.GetState<AmusePlatformFinishState>();
                }

                Assert.That(
                    state.SlotRefusalCount(
                        AlphaSeparationSlotRefusal
                            .RuntimeMaterialValueNotMapped),
                    Is.EqualTo(1));

                var slotZero = state.Separation.Renderers[0]
                    .CandidateSlots.Single(slot =>
                        slot.Plan.SourceMaterialBindingIndex == 0);
                Assert.That(
                    renderer.sharedMaterials[0],
                    Is.SameAs(slotZero.OpaqueOfAdmitted[first]),
                    "the independent sibling must still apply");
                Assert.That(
                    renderer.sharedMaterials[1], Is.SameAs(foreign),
                    "a foreign pass's same-length replacement must be " +
                    "carried through untouched, not overwritten with a " +
                    "stale barrier-time opaque result");
                Assert.That(renderer.sharedMesh, Is.SameAs(mesh));
            }
            finally
            {
                DestroyGenerated(state);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MappedReplacementBetweenPassesAppliesThatValue()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE mapped replacement");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AmusePlatformFinishState state = null;
            AnimatorController controller = null;
            try
            {
                var first = Track(VerifiedOpaqueMaterial());
                var second = Track(VerifiedOpaqueMaterial());
                var other = Track(VerifiedOpaqueMaterial());
                var mesh = Track(TwoTriangleMesh());
                var renderer = AddRenderer(root, "body", mesh, first, second);

                var clip = Track(new AnimationClip { name = "AMUSE swap" });
                AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    EditorCurveBinding.PPtrCurve(
                        "body", typeof(SkinnedMeshRenderer),
                        "m_Materials.Array.data[1]"),
                    new[]
                    {
                        new ObjectReferenceKeyframe
                        {
                            time = 0f, value = second,
                        },
                        new ObjectReferenceKeyframe
                        {
                            time = 1f, value = other,
                        },
                    });
                controller = NewController(root, "AMUSE swap graph", clip);

                using (new ProbeScope(_ =>
                {
                    renderer.sharedMaterials = new[] { first, other };
                }))
                {
                    var context = AvatarProcessor.ProcessAvatar(
                        root, ApplyTestPlatform.Instance);
                    state = context.GetState<AmusePlatformFinishState>();
                }

                AssertNoFeatureRefusals(state);
                var slotZero = state.Separation.Renderers[0]
                    .CandidateSlots.Single(slot =>
                        slot.Plan.SourceMaterialBindingIndex == 0);
                var slotOne = state.Separation.Renderers[0]
                    .CandidateSlots.Single(slot =>
                        slot.Plan.SourceMaterialBindingIndex == 1);
                Assert.That(
                    slotZero.OpaqueOfAdmitted.TryGetValue(first,
                        out var firstOpaque),
                    Is.True,
                    "fixture precondition: the surviving slot's prepared " +
                    "state must map its current material, or the applied " +
                    "result proves nothing");
                Assert.That(
                    slotOne.OpaqueOfAdmitted.TryGetValue(other,
                        out var otherOpaque),
                    Is.True,
                    "fixture precondition: the replaced current material " +
                    "must be one of the slot's admitted states, or the " +
                    "mapped-replacement claim proves nothing");
                Assert.That(
                    renderer.sharedMaterials[0],
                    Is.SameAs(firstOpaque),
                    "the independent sibling must still apply");
                Assert.That(
                    renderer.sharedMaterials[1],
                    Is.SameAs(otherOpaque),
                    "the replaced current material is one of the states the " +
                    "slot was proven against, so the slot must apply with " +
                    "that material's opaque result, named by reference");
                Assert.That(state.AppliedRendererCount, Is.EqualTo(1));
            }
            finally
            {
                DestroyCommittedClone(root, controller);
                DestroyGenerated(state);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // --- Falsifier 4: planned split invalidated late --------------------

        [Test]
        public void PlannedSplitInvalidatedLateSweepsItsCloneWithoutAnyWrite()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE late invalidated split");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AlphaSeparationSeamProbe probe = null;
            try
            {
                AlphaSeparationSplitTests.EnsureSplitFolder();
                try
                {
                    var texture = Track(
                        AlphaSeparationSplitTests.ImportSplitAlphaTexture(
                            "late_invalidated"));
                    var split = Track(
                        AlphaSeparationSplitTests.SplitAlphaMaterial(texture));
                    var transparent = Track(VerifiedTransparentMaterial());
                    var mesh = Track(
                        AlphaSeparationSplitTests.CreateSplitSourceMesh());
                    var renderer = AddRenderer(
                        root, "split", mesh, split, transparent);
                    Assert.That(
                        mesh.GetSubMesh(0).baseVertex, Is.EqualTo(4),
                        "fixture precondition: the split submesh must carry " +
                        "a nonzero base vertex");

                    var foreign = Track(VerifiedOpaqueMaterial());
                    using (new ProbeScope(_ =>
                    {
                        renderer.sharedMaterials = new[] { foreign,
                            transparent };
                    }))
                    {
                        var context = AvatarProcessor.ProcessAvatar(
                            root, SeamTestPlatform.Instance);
                        probe = context.GetState<AlphaSeparationSeamProbe>();
                    }

                    Assert.That(probe.Decision.IsPrepared, Is.True);
                    Assert.That(probe.Decision.HasMutation, Is.False,
                        "nothing may be applied when every slot was " +
                        "invalidated");

                    Assert.That(probe.RecordedMeshClones, Has.Count.EqualTo(1));
                    Assert.That(probe.RecordedMeshClones[0], Is.Not.Null,
                        "the split slot survived preparation, so the clone " +
                        "existed after the barrier");
                    Assert.That(probe.RecordedClones, Has.Count.EqualTo(1));
                    Assert.That(probe.RecordedClones[0], Is.Not.Null);

                    Assert.That(probe.RendererDigestBefore,
                        Is.EqualTo(probe.RendererDigestAfter),
                        "prepare must not mutate the build avatar");
                    Assert.That(probe.CurveDigestBefore,
                        Is.EqualTo(probe.CurveDigestAfter));

                    Assert.That(
                        probe.RecordedMeshClones[0] == null, Is.True,
                        "the clone no surviving split references must be " +
                        "destroyed");
                    Assert.That(
                        probe.RecordedClones[0] == null, Is.True,
                        "the orphaned material clone must be destroyed");

                    Assert.That(
                        probe.SlotRefusals(
                            AlphaSeparationSlotRefusal
                                .RuntimeMaterialValueNotMapped),
                        Is.EqualTo(1));
                }
                finally
                {
                    AlphaSeparationSplitTests.DeleteSplitFolder();
                }
            }
            finally
            {
                DestroyGenerated(probe?.State);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // --- Falsifier 15: the sweep destroys exactly the unreferenced ------

        [Test]
        public void SweepDestroysExactlyTheUnreferencedClones()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE selective sweep");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AlphaSeparationSeamProbe probe = null;
            try
            {
                AlphaSeparationSplitTests.EnsureSplitFolder();
                try
                {
                    var texture = Track(
                        AlphaSeparationSplitTests.ImportSplitAlphaTexture(
                            "selective_sweep"));
                    var shared = Track(
                        AlphaSeparationSplitTests.SplitAlphaMaterial(texture));
                    var alone = Track(
                        AlphaSeparationSplitTests.SplitAlphaMaterial(texture));
                    var transparent = Track(VerifiedTransparentMaterial());
                    shared.name = "amuse sweep shared";
                    alone.name = "amuse sweep alone";

                    var firstMesh = Track(
                        AlphaSeparationSplitTests.CreateSplitSourceMesh());
                    var secondMesh = Track(
                        AlphaSeparationSplitTests.CreateSplitSourceMesh());
                    var survivingRenderer = AddRenderer(
                        root, "surviving", firstMesh, shared, transparent);
                    var refusedRenderer = AddRenderer(
                        root, "refused", secondMesh, alone, transparent);

                    var foreign = Track(VerifiedOpaqueMaterial());
                    using (new ProbeScope(_ =>
                    {
                        refusedRenderer.sharedMaterials =
                            new[] { foreign, transparent };
                    }))
                    {
                        var context = AvatarProcessor.ProcessAvatar(
                            root, SeamTestPlatform.Instance);
                        probe = context.GetState<AlphaSeparationSeamProbe>();
                    }

                    Assert.That(probe.Decision.IsPrepared, Is.True);
                    Assert.That(probe.Decision.HasMutation, Is.True,
                        "fixture precondition: the surviving renderer must " +
                        "still produce a write, or the sweep proves nothing");
                    Assert.That(
                        probe.SlotRefusals(
                            AlphaSeparationSlotRefusal
                                .RuntimeMaterialValueNotMapped),
                        Is.EqualTo(1),
                        "exactly the replaced slot may refuse");

                    Assert.That(probe.RecordedClones, Has.Count.EqualTo(2),
                        "two distinct sources produce exactly two clones, " +
                        "registered in the barrier's renderer order");
                    Assert.That(probe.RecordedMeshClones, Has.Count.EqualTo(2));

                    Assert.That(probe.RendererDigestBefore,
                        Is.EqualTo(probe.RendererDigestAfter),
                        "prepare must not mutate the build avatar");
                    Assert.That(probe.CurveDigestBefore,
                        Is.EqualTo(probe.CurveDigestAfter));

                    // Registration order follows the barrier's renderer loop:
                    // the surviving renderer's shared source first, the
                    // refused renderer's alone source second. Only the clone
                    // no surviving slot references reports destroyed.
                    Assert.That(
                        probe.RecordedClones[0] == null, Is.False,
                        "the clone the surviving slot references must " +
                        "survive the sweep");
                    Assert.That(
                        probe.RecordedClones[0].name, Does.StartWith(
                            "amuse sweep shared"),
                        "the surviving clone must be the one the surviving " +
                        "slot references");
                    Assert.That(
                        probe.RecordedClones[1] == null, Is.True,
                        "the abandoned clone must be destroyed");

                    Assert.That(
                        probe.RecordedMeshClones[0] == null, Is.False,
                        "the surviving split slot's mesh clone must survive " +
                        "the sweep");
                    Assert.That(
                        probe.RecordedMeshClones[1] == null, Is.True,
                        "the invalidated renderer's mesh clone must be " +
                        "destroyed");
                }
                finally
                {
                    AlphaSeparationSplitTests.DeleteSplitFolder();
                }
            }
            finally
            {
                DestroyGenerated(probe?.State);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // --- Falsifier 17: every slot validated before any mutation ---------

        [Test]
        public void
            EverySlotIsValidatedBeforePrepareReturnsAndEveryDistinctReasonIsRecorded()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE validation coverage");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AlphaSeparationSeamProbe probe = null;
            AnimatorController controller = null;
            try
            {
                var first = Track(VerifiedOpaqueMaterial());
                var second = Track(VerifiedOpaqueMaterial());
                var swap = Track(VerifiedOpaqueMaterial());
                var mesh = Track(TwoTriangleMesh());
                var renderer = AddRenderer(root, "body", mesh, first, second);

                var clip = Track(new AnimationClip { name = "AMUSE slot swap" });
                AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    EditorCurveBinding.PPtrCurve(
                        "body", typeof(SkinnedMeshRenderer),
                        "m_Materials.Array.data[1]"),
                    new[]
                    {
                        new ObjectReferenceKeyframe
                        {
                            time = 0f, value = swap,
                        },
                    });
                controller = NewController(root, "AMUSE coverage graph", clip);

                var foreign = Track(VerifiedOpaqueMaterial());
                using (new ProbeScope(context =>
                {
                    // Slot 0: an unmapped current material, refusing with
                    // RuntimeMaterialValueNotMapped.
                    renderer.sharedMaterials = new[] { foreign, second };

                    // Slot 1: a binding of a different type at the same path
                    // and slot, absent from the captured evidence, refusing
                    // with SlotBindingAbsentFromEvidence.
                    var committed = CommittedClipWithObjectBinding(
                        root, "body", "m_Materials.Array.data[1]");
                    Assert.That(committed, Is.Not.Null,
                        "fixture precondition: a committed clip must carry " +
                        "the slot's binding");
                    AnimationUtility.SetObjectReferenceCurve(
                        committed,
                        EditorCurveBinding.PPtrCurve(
                            "body", typeof(Renderer),
                            "m_Materials.Array.data[1]"),
                        new[]
                        {
                            new ObjectReferenceKeyframe
                            {
                                time = 0f, value = swap,
                            },
                        });
                }))
                {
                    var context = AvatarProcessor.ProcessAvatar(
                        root, SeamTestPlatform.Instance);
                    probe = context.GetState<AlphaSeparationSeamProbe>();
                }

                Assert.That(probe.Decision.IsPrepared, Is.True);
                Assert.That(probe.Decision.HasMutation, Is.False,
                    "no surviving slot may produce a write");

                Assert.That(probe.RendererDigestBefore,
                    Is.EqualTo(probe.RendererDigestAfter),
                    "prepare must not mutate any renderer");
                Assert.That(probe.CurveDigestBefore,
                    Is.EqualTo(probe.CurveDigestAfter),
                    "prepare must not mutate any clip curve");

                Assert.That(
                    probe.SlotRefusals(
                        AlphaSeparationSlotRefusal
                            .RuntimeMaterialValueNotMapped),
                    Is.EqualTo(1),
                    "the slot with the unmapped current material must be " +
                    "validated and recorded");
                Assert.That(
                    probe.SlotRefusals(
                        AlphaSeparationSlotRefusal
                            .SlotBindingAbsentFromEvidence),
                    Is.EqualTo(1),
                    "the slot with the unrecorded binding must also be " +
                    "validated and recorded; a validator that stops at the " +
                    "first refusal cannot produce both");
                foreach (AlphaSeparationSlotRefusal reason in Enum.GetValues(
                             typeof(AlphaSeparationSlotRefusal)))
                {
                    if (reason == AlphaSeparationSlotRefusal.None ||
                        reason == AlphaSeparationSlotRefusal
                            .RuntimeMaterialValueNotMapped ||
                        reason == AlphaSeparationSlotRefusal
                            .SlotBindingAbsentFromEvidence)
                    {
                        continue;
                    }

                    Assert.That(
                        probe.SlotRefusals(reason), Is.Zero,
                        "no other reason may be recorded: " + reason);
                }
            }
            finally
            {
                DestroyCommittedClone(root, controller);
                DestroyGenerated(probe?.State);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // --- Defect C regression: identity-mapped Split appended curve ------

        [Test]
        public void IdentityMappedSplitStillWritesItsAppendedCurve()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE identity split");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AlphaSeparationSeamProbe probe = null;
            AnimatorController controller = null;
            try
            {
                AlphaSeparationSplitTests.EnsureSplitFolder();
                try
                {
                    var texture = Track(
                        AlphaSeparationSplitTests.ImportSplitAlphaTexture(
                            "identity_split"));
                    var split = Track(
                        AlphaSeparationSplitTests.SplitAlphaMaterial(texture));
                    var other = Track(
                        AlphaSeparationSplitTests.SplitAlphaMaterial(texture));
                    var transparent = Track(VerifiedTransparentMaterial());
                    var mesh = Track(
                        AlphaSeparationSplitTests.CreateSplitSourceMesh());
                    AddRenderer(
                        root, "split", mesh, split, transparent);

                    var clip = Track(new AnimationClip
                    {
                        name = "AMUSE identity swap",
                    });
                    AnimationUtility.SetObjectReferenceCurve(
                        clip,
                        EditorCurveBinding.PPtrCurve(
                            "split", typeof(SkinnedMeshRenderer),
                            "m_Materials.Array.data[0]"),
                        new[]
                        {
                            new ObjectReferenceKeyframe
                            {
                                time = 0f, value = split,
                            },
                            new ObjectReferenceKeyframe
                            {
                                time = 1f, value = other,
                            },
                        });
                    controller = NewController(
                        root, "AMUSE identity graph", clip);

                    using (new ConversionOverrideScope(
                        (Material live, CapturedMaterialEvidence derived,
                         Material preparedOpaque,
                         out Material opaque,
                         out PoiyomiOpaqueConversionRefusal refusal) =>
                        {
                            opaque = live;
                            refusal = PoiyomiOpaqueConversionRefusal.None;
                            return true;
                        }))
                    {
                        var context = AvatarProcessor.ProcessAvatar(
                            root, SeamTestPlatform.Instance);
                        probe = context.GetState<AlphaSeparationSeamProbe>();
                    }

                    Assert.That(probe.Decision.IsPrepared, Is.True);
                    Assert.That(probe.Decision.HasMutation, Is.True,
                        "fixture precondition: the appended slot must " +
                        "produce a write, or the appended curve proves " +
                        "nothing");

                    Assert.That(probe.Finalization.Writes,
                        Has.Count.EqualTo(1));
                    var write = probe.Finalization.Writes[0];
                    Assert.That(write.CurveEdits, Has.Count.EqualTo(1),
                        "only the appended binding may be written; the " +
                        "Split slot's own curve stays authored");
                    var appended = write.CurveEdits[0];
                    Assert.That(
                        appended.Binding.propertyName,
                        Is.EqualTo("m_Materials.Array.data[2]"),
                        "n = 2, so the first surviving Split slot's opaque " +
                        "result lands on appended slot 2");
                    Assert.That(appended.Curve, Has.Length.EqualTo(2));
                    Assert.That(
                        appended.Curve[0].time.ToString("R"),
                        Is.EqualTo(0f.ToString("R")));
                    Assert.That(
                        appended.Curve[1].time.ToString("R"),
                        Is.EqualTo(1f.ToString("R")),
                        "the appended curve must carry the authored " +
                        "keyframe times exactly");
                    Assert.That(
                        appended.Curve[0].value, Is.SameAs(split),
                        "an identity mapping must write the source material " +
                        "itself");
                    Assert.That(appended.Curve[1].value, Is.SameAs(other));

                    Assert.That(
                        probe.RendererDigestBefore,
                        Is.EqualTo(probe.RendererDigestAfter),
                        "prepare must not mutate the build avatar");
                    Assert.That(
                        probe.CurveDigestBefore,
                        Is.EqualTo(probe.CurveDigestAfter),
                        "prepare must not mutate any clip curve");
                }
                finally
                {
                    AlphaSeparationSplitTests.DeleteSplitFolder();
                }
            }
            finally
            {
                DestroyCommittedClone(root, controller);
                DestroyGenerated(probe?.State);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // --- Task 5 integration coverage: the cutout family end to end ------

        /// <summary>
        /// Full-artifact end to end (coverage 21): one real NDMF build over
        /// a renderer carrying a wholly-opaque cutout slot over a fully
        /// opaque mipmap chain and a splitting cutout slot over a mip-free
        /// split-alpha texture. The build result must carry the converted
        /// clones with the canonical recipe read back and retention naming,
        /// the split mesh with the appended submesh assigned the clone, the
        /// proven triangle moved, the remaining triangle on the source
        /// material, unchanged sources, persistent generated objects, and
        /// no orphaned clones.
        /// <para>
        /// Falsifies: an apply boundary that prepares but never writes the
        /// cutout family, a clone recipe that skips read-back or retention
        /// naming, an appended submesh assigned the source material, a sweep
        /// that destroys referenced clones or spares orphans, generated
        /// objects that never persist through serialization, and a
        /// conversion that mutates its source. The clone's pre-naming
        /// emptiness is the conversion unit tests' assertion; the retention
        /// name is the observable here.
        /// </para>
        /// </summary>
        [Test]
        public void CutoutBuildCarriesTheFullArtifactEndToEnd()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                ApplyCutoutPersistenceFolder);
            var root = new GameObject("AMUSE cutout full artifact");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AmusePlatformFinishState state = null;
            var fixtures = new LilToonCutoutConversionFixtures();
            try
            {
                fixtures.BaseSetUp();
                AlphaSeparationSplitTests.EnsureSplitFolder();
                try
                {
                    var mipTexture = fixtures.ImportFullyOpaqueMipmap(
                        "cutout_full_artifact");
                    var splitTexture =
                        AlphaSeparationSplitTests.ImportSplitAlphaTexture(
                            "cutout_full_artifact_split");
                    var wholeMaterial = Track(
                        NewCutoutConversionMaterial(mipTexture));
                    var splitMaterial = Track(
                        NewCutoutSplitMaterial(splitTexture));
                    var sourceMesh = Track(
                        AlphaSeparationSplitTests
                            .CreateOpaqueAndSplitSourceMesh());
                    var renderer = AddRenderer(
                        root, "body", sourceMesh,
                        wholeMaterial, splitMaterial);

                    var wholeDigestBefore = DigestMaterial(wholeMaterial);
                    var splitDigestBefore = DigestMaterial(splitMaterial);
                    var meshDigestBefore = DigestMesh(sourceMesh);

                    var context = AvatarProcessor.ProcessAvatar(
                        root, ApplyTestPlatform.Instance);
                    state = context.GetState<AmusePlatformFinishState>();

                    Assert.That(context.Successful, Is.True,
                        "fixture precondition: the build must complete");
                    Assert.That(state.AnalyzedRendererCount, Is.EqualTo(1),
                        "fixture precondition: the renderer must analyze");
                    Assert.That(state.OpaqueCandidateTriangleCount,
                        Is.EqualTo(2),
                        "fixture precondition: one wholly opaque triangle " +
                        "and one split opaque triangle must be candidates");
                    AssertNoFeatureRefusals(state);
                    Assert.That(state.Separation, Is.Not.Null);

                    var prepared = state.Separation.Renderers.Single();
                    var wholeSlot = prepared.CandidateSlots.Single(
                        slot => slot.Plan.SourceMaterialBindingIndex == 0);
                    var splitSlot = prepared.CandidateSlots.Single(
                        slot => slot.Plan.SourceMaterialBindingIndex == 1);
                    Assert.That(wholeSlot.Plan.Disposition,
                        Is.EqualTo(SubmeshSeparationDisposition
                            .WhollyOpaqueCandidate),
                        "fixture precondition: the fully opaque slot must " +
                        "convert in place");
                    Assert.That(splitSlot.Plan.Disposition,
                        Is.EqualTo(SubmeshSeparationDisposition.Split),
                        "fixture precondition: the mixed slot must split, " +
                        "or the appended submesh proves nothing");

                    // The generated clones: canonical recipe read back and
                    // retention naming.
                    Assert.That(state.Separation.CreatedClones,
                        Has.Count.EqualTo(2));
                    var wholeClone =
                        state.Separation.OpaqueBySource[wholeMaterial];
                    var splitClone =
                        state.Separation.OpaqueBySource[splitMaterial];
                    Assert.That(wholeClone, Is.Not.SameAs(splitClone),
                        "two sources convert to two distinct clones");
                    AssertCanonicalOpaqueRecipe(wholeClone);
                    AssertCanonicalOpaqueRecipe(splitClone);
                    Assert.That(wholeClone.name,
                        Is.EqualTo(wholeMaterial.name + " (AMUSE Opaque 0)"),
                        "the first registered clone must carry the " +
                        "retention name");
                    Assert.That(splitClone.name,
                        Is.EqualTo(splitMaterial.name + " (AMUSE Opaque 1)"));

                    // The split mesh: proven triangle moved to the appended
                    // submesh, remaining triangle kept with the source.
                    var builtMesh = renderer.sharedMesh;
                    Assert.That(builtMesh, Is.EqualTo(prepared.MeshClone),
                        "fixture precondition: the renderer must carry the " +
                        "finalized mesh clone");
                    Assert.That(builtMesh, Is.Not.EqualTo(sourceMesh),
                        "fixture precondition: the assigned mesh must be " +
                        "generated, not the source");
                    Assert.That(builtMesh.name,
                        Is.EqualTo(sourceMesh.name + " (AMUSE Separated 0)"));
                    Assert.That(builtMesh.subMeshCount, Is.EqualTo(3));
                    CollectionAssert.AreEqual(
                        new[] { 0, 1, 2 }, builtMesh.GetIndices(0),
                        "the wholly opaque slot's submesh must stay " +
                        "untouched");
                    CollectionAssert.AreEqual(
                        AlphaSeparationSplitTests
                            .SplitTransparentEffectiveIndicesFor(4),
                        builtMesh.GetIndices(1),
                        "the split submesh must retain exactly the " +
                        "unproven triangle");
                    CollectionAssert.AreEqual(
                        AlphaSeparationSplitTests
                            .SplitOpaqueEffectiveIndicesFor(4),
                        builtMesh.GetIndices(2),
                        "the appended submesh must carry exactly the " +
                        "proven triangle");

                    // The material arrays.
                    Assert.That(renderer.sharedMaterials,
                        Has.Length.EqualTo(3));
                    Assert.That(renderer.sharedMaterials[0],
                        Is.EqualTo(wholeClone),
                        "the wholly opaque slot carries its opaque result " +
                        "in place");
                    Assert.That(renderer.sharedMaterials[1],
                        Is.EqualTo(splitMaterial),
                        "the split submesh's remaining triangle must stay " +
                        "on the source material");
                    Assert.That(renderer.sharedMaterials[2],
                        Is.EqualTo(splitClone),
                        "the appended submesh must be assigned the clone");

                    Assert.That(state.AppliedRendererCount, Is.EqualTo(1));
                    Assert.That(state.AppliedOpaqueTriangleCount,
                        Is.EqualTo(2),
                        "the wholly opaque slot's triangle and the moved " +
                        "split triangle must both count as applied opaque");

                    // The sources are evidence, never mutation targets.
                    Assert.That(DigestMaterial(wholeMaterial),
                        Is.EqualTo(wholeDigestBefore),
                        "the source material must be unchanged");
                    Assert.That(DigestMaterial(splitMaterial),
                        Is.EqualTo(splitDigestBefore),
                        "the source material must be unchanged");
                    Assert.That(DigestMesh(sourceMesh),
                        Is.EqualTo(meshDigestBefore),
                        "the source mesh must be unchanged");

                    // Persistence through serialization.
                    foreach (var generated in new UnityEngine.Object[]
                             {
                                 wholeClone,
                                 splitClone,
                                 builtMesh,
                             })
                    {
                        Assert.That(EditorUtility.IsPersistent(generated),
                            Is.True,
                            generated.name +
                            " must persist through serialization");
                        Assert.That(AssetDatabase.Contains(generated),
                            Is.True,
                            generated.name + " must live in the asset " +
                            "database after the build");
                        var path = AssetDatabase
                            .GetAssetPath(generated)
                            .Replace('\\', '/');
                        Assert.That(path,
                            Does.StartWith(ApplyCutoutPersistenceFolder +
                                           "/"),
                            generated.name + " must be saved inside the " +
                            "test-owned persistence directory, not " + path);
                        Assert.That(path, Does.EndWith(".asset"),
                            generated.name + " must be a serialized asset");
                    }

                    // The sweep leaves no orphans: every created clone is
                    // alive and referenced by the build copy.
                    foreach (var clone in state.Separation.CreatedClones)
                    {
                        Assert.That(clone, Is.Not.Null,
                            "the sweep must leave no orphan clones");
                    }

                    CollectionAssert.Contains(
                        renderer.sharedMaterials, wholeClone,
                        "the surviving clone must stay referenced");
                    CollectionAssert.Contains(
                        renderer.sharedMaterials, splitClone,
                        "the surviving clone must stay referenced");
                }
                finally
                {
                    AlphaSeparationSplitTests.DeleteSplitFolder();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(ApplyCutoutPersistenceFolder);
                Assert.That(
                    AssetDatabase.IsValidFolder(ApplyCutoutPersistenceFolder),
                    Is.False,
                    "the test-owned persistence directory must be deleted " +
                    "even when an assertion fails");
                DestroyGenerated(state);
                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// Curve rewrite and appended-slot indexing through a real build
        /// (coverage 21): a material-swap clip alternating the cutout source
        /// with a second admitted value over a Split slot. The slot's own
        /// curve stays authored; the appended binding carries identical
        /// times with every keyframe mapped, the clone for the cutout value
        /// and the value itself where conversion maps to itself.
        /// <para>
        /// Falsifies: an appended-curve writer that maps only the current
        /// value (every admitted keyframe must map), an appended index
        /// computed from the slot count instead of the live array length,
        /// and a rewrite that touches the Split slot's own authored curve.
        /// </para>
        /// </summary>
        [Test]
        public void CutoutSplitSlotRewritesCurvesOntoTheAppendedSlot()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE cutout appended slot");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AmusePlatformFinishState state = null;
            AnimatorController controller = null;
            var fixtures = new LilToonCutoutConversionFixtures();
            try
            {
                fixtures.BaseSetUp();
                AlphaSeparationSplitTests.EnsureSplitFolder();
                try
                {
                    var splitTexture =
                        AlphaSeparationSplitTests.ImportSplitAlphaTexture(
                            "cutout_appended");
                    var cutout = Track(
                        NewCutoutSplitMaterial(splitTexture));
                    var swap = Track(
                        LilToonFixtureTestBase.CreateVerifiedMaterial());
                    var transparent = Track(VerifiedTransparentMaterial());
                    var sourceMesh = Track(
                        AlphaSeparationSplitTests.CreateSplitSourceMesh());
                    var renderer = AddRenderer(
                        root, "split", sourceMesh, cutout, transparent);

                    var clip = Track(NewSwapClip(
                        "AMUSE cutout appended swap", "split", 0,
                        (0f, cutout), (1f, swap)));
                    controller = NewController(
                        root, "AMUSE cutout appended graph", clip);
                    var authoredOwn = DescribeAuthoredCurve(
                        clip, "split", "m_Materials.Array.data[0]");

                    var context = AvatarProcessor.ProcessAvatar(
                        root, ApplyTestPlatform.Instance);
                    state = context.GetState<AmusePlatformFinishState>();

                    Assert.That(state.AnalyzedRendererCount, Is.EqualTo(1),
                        "fixture precondition: the renderer must analyze");
                    Assert.That(state.OpaqueCandidateTriangleCount,
                        Is.EqualTo(1),
                        "fixture precondition: exactly one opaque triangle " +
                        "must be a candidate");
                    AssertNoFeatureRefusals(state);
                    Assert.That(state.Separation, Is.Not.Null);

                    var slot = state.Separation.Renderers[0]
                        .CandidateSlots.Single();
                    Assert.That(slot.Plan.Disposition,
                        Is.EqualTo(SubmeshSeparationDisposition.Split),
                        "fixture precondition: the slot must split, or " +
                        "the appended binding proves nothing");
                    Assert.That(slot.OpaqueOfAdmitted, Has.Count.EqualTo(2),
                        "fixture precondition: both admitted values must " +
                        "map, or the rewritten curve proves nothing");
                    Assert.That(state.Separation.CreatedClones,
                        Has.Count.EqualTo(1),
                        "only the cutout source may clone; the attested " +
                        "opaque swap value maps to itself");

                    var clone = state.Separation.OpaqueBySource[cutout];

                    // The Split slot's own binding stays authored.
                    var committedOwn = CommittedClipWithObjectBinding(
                        root, "split", "m_Materials.Array.data[0]");
                    Assert.That(committedOwn, Is.Not.Null,
                        "fixture precondition: the committed clip must " +
                        "carry the slot's own binding");
                    Assert.That(
                        DescribeAuthoredCurve(
                            committedOwn, "split",
                            "m_Materials.Array.data[0]"),
                        Is.EqualTo(authoredOwn),
                        "a Split slot's own curve stays authored; only " +
                        "the appended opaque slot receives the mapped " +
                        "curve");

                    // The appended binding: identical times, every value
                    // mapped.
                    var appendedClip = CommittedClipWithObjectBinding(
                        root, "split", "m_Materials.Array.data[2]");
                    Assert.That(appendedClip, Is.Not.Null,
                        "two live slots means the appended opaque slot is " +
                        "index 2");
                    var appendedCurve = AnimationUtility
                        .GetObjectReferenceCurve(
                            appendedClip,
                            EditorCurveBinding.PPtrCurve(
                                "split", typeof(SkinnedMeshRenderer),
                                "m_Materials.Array.data[2]"));
                    Assert.That(appendedCurve, Has.Length.EqualTo(2));
                    Assert.That(
                        new[]
                        {
                            appendedCurve[0].time.ToString("R"),
                            appendedCurve[1].time.ToString("R"),
                        },
                        Is.EqualTo(new[]
                        {
                            0f.ToString("R"),
                            1f.ToString("R"),
                        }),
                        "the appended curve must carry the authored " +
                        "keyframe times exactly");
                    Assert.That(appendedCurve[0].value, Is.SameAs(clone),
                        "the cutout value maps to the generated clone");
                    Assert.That(appendedCurve[1].value, Is.SameAs(swap),
                        "the attested opaque value maps to itself");

                    // The source clip asset is unchanged.
                    Assert.That(
                        DescribeAuthoredCurve(
                            clip, "split", "m_Materials.Array.data[0]"),
                        Is.EqualTo(authoredOwn),
                        "the source clip must be unchanged");

                    // The build copy: mesh split, appended slot assigned
                    // the clone.
                    Assert.That(renderer.sharedMesh,
                        Is.Not.EqualTo(sourceMesh));
                    Assert.That(renderer.sharedMesh.subMeshCount,
                        Is.EqualTo(3));
                    CollectionAssert.AreEqual(
                        AlphaSeparationSplitTests
                            .SplitTransparentEffectiveIndicesFor(4),
                        renderer.sharedMesh.GetIndices(0),
                        "the split submesh must retain exactly the " +
                        "unproven triangle");
                    CollectionAssert.AreEqual(
                        AlphaSeparationSplitTests
                            .SplitOpaqueEffectiveIndicesFor(4),
                        renderer.sharedMesh.GetIndices(2),
                        "the appended submesh must carry exactly the " +
                        "proven triangle");
                    Assert.That(renderer.sharedMaterials,
                        Has.Length.EqualTo(3));
                    Assert.That(renderer.sharedMaterials[0],
                        Is.SameAs(cutout),
                        "the split slot's source material stays on its " +
                        "submesh");
                    Assert.That(renderer.sharedMaterials[1],
                        Is.SameAs(transparent));
                    Assert.That(renderer.sharedMaterials[2],
                        Is.SameAs(clone),
                        "the appended slot must carry the clone");

                    Assert.That(state.AppliedRendererCount, Is.EqualTo(1));
                    Assert.That(state.AppliedOpaqueTriangleCount,
                        Is.EqualTo(1));
                }
                finally
                {
                    AlphaSeparationSplitTests.DeleteSplitFolder();
                }
            }
            finally
            {
                DestroyCommittedClone(root, controller);
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// Attested-opaque map-to-self at pipeline level (coverage 2): a
        /// cutout slot whose swap set reaches the opaque lilToon stand-in
        /// maps that value to itself with no clone, through a real build
        /// with the apply pass.
        /// <para>
        /// Falsifies a pipeline-level routing that clones the attested
        /// opaque admitted value instead of mapping it to itself, or that
        /// drops the self-mapped value from the rewritten curve.
        /// </para>
        /// </summary>
        [Test]
        public void CutoutSwapToAttestedOpaqueMapsToItselfWithoutAClone()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE cutout map to self");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AmusePlatformFinishState state = null;
            AnimatorController controller = null;
            var fixtures = new LilToonCutoutConversionFixtures();
            try
            {
                fixtures.BaseSetUp();
                var mipTexture = fixtures.ImportFullyOpaqueMipmap(
                    "cutout_map_to_self");
                var cutout = Track(NewCutoutConversionMaterial(mipTexture));
                var swap = Track(
                    LilToonFixtureTestBase.CreateVerifiedMaterial());
                var mesh = Track(SingleTriangleMesh());
                mesh.uv = new[]
                {
                    new Vector2(0.25f, 0.25f),
                    new Vector2(0.75f, 0.25f),
                    new Vector2(0.25f, 0.75f),
                };
                var renderer = AddRenderer(root, "body", mesh, cutout);

                var clip = Track(NewSwapClip(
                    "AMUSE cutout map to self swap", "body", 0,
                    (0f, cutout), (1f, swap)));
                controller = NewController(
                    root, "AMUSE cutout map to self graph", clip);

                var context = AvatarProcessor.ProcessAvatar(
                    root, ApplyTestPlatform.Instance);
                state = context.GetState<AmusePlatformFinishState>();

                Assert.That(state.AnalyzedRendererCount, Is.EqualTo(1),
                    "fixture precondition: the renderer must analyze");
                Assert.That(state.OpaqueCandidateTriangleCount,
                    Is.EqualTo(1),
                    "fixture precondition: both admitted values prove the " +
                    "triangle opaque");
                AssertNoFeatureRefusals(state);
                Assert.That(state.Separation, Is.Not.Null);

                var slot = state.Separation.Renderers[0]
                    .CandidateSlots.Single();
                Assert.That(slot.Plan.Disposition,
                    Is.EqualTo(SubmeshSeparationDisposition
                        .WhollyOpaqueCandidate));
                Assert.That(slot.OpaqueOfAdmitted, Has.Count.EqualTo(2),
                    "fixture precondition: both swap values must map");
                Assert.That(state.Separation.CreatedClones,
                    Has.Count.EqualTo(1),
                    "the attested opaque admitted value must map to " +
                    "itself with no clone; only the cutout source clones");

                var clone = state.Separation.OpaqueBySource[cutout];
                Assert.That(state.Separation.OpaqueBySource[swap],
                    Is.SameAs(swap),
                    "the avatar-wide mapping must carry the identity for " +
                    "the attested opaque value");

                var committed = CommittedClipWithObjectBinding(
                    root, "body", "m_Materials.Array.data[0]");
                Assert.That(committed, Is.Not.Null,
                    "fixture precondition: the committed clip must carry " +
                    "the rewritten binding");
                var committedCurve = AnimationUtility
                    .GetObjectReferenceCurve(
                        committed,
                        EditorCurveBinding.PPtrCurve(
                            "body", typeof(SkinnedMeshRenderer),
                            "m_Materials.Array.data[0]"));
                Assert.That(committedCurve, Has.Length.EqualTo(2));
                Assert.That(committedCurve[0].value, Is.SameAs(clone),
                    "the cutout value maps to the generated clone");
                Assert.That(committedCurve[1].value, Is.SameAs(swap),
                    "the self-mapped value must survive the rewrite " +
                    "unchanged");
                Assert.That(renderer.sharedMaterials[0],
                    Is.SameAs(clone),
                    "the current assignment maps through the same opaque " +
                    "result");
                Assert.That(state.AppliedRendererCount, Is.EqualTo(1));
                Assert.That(state.AppliedOpaqueTriangleCount,
                    Is.EqualTo(1));
            }
            finally
            {
                DestroyCommittedClone(root, controller);
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// Row 21 — regression parity with a transparent sibling. Two real
        /// builds through the full lifecycle: a control avatar carrying
        /// exactly the pre-existing cutout-split and Poiyomi fixtures, and
        /// a sibling avatar carrying the same two fixtures plus one
        /// transparent conversion renderer. Every observable of the
        /// cutout and Poiyomi paths must be byte-identical across the two
        /// builds — clone facts (shader, queue, RenderType, every
        /// shader-declared float), retention names, the rewritten
        /// appended curve, the mesh indices, the applied counts — and the
        /// transparent sibling must convert onto the same canonical
        /// recipe beside them.
        /// <para>
        /// Falsifies a shared or widened request object: the transparent
        /// family's capture and conversion requests join the renderer
        /// unions in the sibling build, so any leakage into the cutout or
        /// Poiyomi evidence, gates, or results shows up as a digest
        /// difference here. Cross-build identity is asserted through
        /// observable facts only, never object references.
        /// </para>
        /// </summary>
        [Test]
        public void TransparentSiblingKeepsPoiyomiAndCutoutResultsByteIdentical()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonCutoutConversionFixtures();
            try
            {
                fixtures.BaseSetUp();
                AlphaSeparationSplitTests.EnsureSplitFolder();
                try
                {
                    var control = new TransparentParityResult();
                    control.Build(
                        fixtures, withTransparentSibling: false);
                    TransparentParityResult sibling = null;
                    try
                    {
                        sibling = new TransparentParityResult();
                        sibling.Build(
                            fixtures, withTransparentSibling: true);

                        // The cutout path: byte-identical clone facts,
                        // retention name, curve rewrite, mesh split and
                        // applied accounting with the transparent sibling
                        // present.
                        Assert.That(sibling.CutoutCloneFacts,
                            Is.EqualTo(control.CutoutCloneFacts),
                            "the cutout clone must be byte-identical to " +
                            "its pre-existing expectation when a " +
                            "transparent sibling converts beside it");
                        Assert.That(sibling.CutoutCloneName,
                            Is.EqualTo(control.CutoutCloneName),
                            "the cutout clone's retention name must not " +
                            "shift when the transparent renderer registers " +
                            "after it");
                        Assert.That(sibling.CutoutAppendedCurve,
                            Is.EqualTo(control.CutoutAppendedCurve),
                            "the appended curve must carry identical " +
                            "keyframe times and mapping in both builds");
                        Assert.That(sibling.CutoutMeshIndices,
                            Is.EqualTo(control.CutoutMeshIndices),
                            "the split submesh and appended submesh must " +
                            "carry identical index buffers in both builds");
                        Assert.That(sibling.CutoutOwnCurve,
                            Is.EqualTo(control.CutoutOwnCurve),
                            "the split slot's authored curve must stay " +
                            "untouched in both builds");

                        // The Poiyomi path: byte-identical clone facts and
                        // in-place replacement.
                        Assert.That(sibling.PoiyomiCloneFacts,
                            Is.EqualTo(control.PoiyomiCloneFacts),
                            "the Poiyomi clone must be byte-identical to " +
                            "its pre-existing expectation when a " +
                            "transparent sibling converts beside it");
                        Assert.That(sibling.PoiyomiName,
                            Is.EqualTo(control.PoiyomiName));
                        Assert.That(sibling.AppliedRendererCount,
                            Is.EqualTo(control.AppliedRendererCount + 1),
                            "only the transparent renderer may add to the " +
                            "applied accounting");
                        Assert.That(sibling.AppliedOpaqueTriangleCount,
                            Is.EqualTo(control.AppliedOpaqueTriangleCount + 1),
                            "only the transparent triangle may add to the " +
                            "applied opaque accounting");

                        // The transparent sibling itself: converted onto
                        // the canonical opaque recipe beside the siblings.
                        Assert.That(
                            sibling.State.Separation.TryGetOpaque(
                                sibling.TransparentMaterial,
                                out var transparentClone),
                            Is.True,
                            "fixture precondition: the transparent " +
                            "sibling must convert, or the parity proof " +
                            "runs beside nothing");
                        AssertCanonicalOpaqueRecipe(transparentClone);
                        Assert.That(
                            sibling.State.Separation.CreatedClones,
                            Has.Count.EqualTo(3),
                            "the cutout source, the Poiyomi source and " +
                            "the transparent source must each clone once");
                    }
                    finally
                    {
                        if (sibling != null)
                        {
                            sibling.Dispose();
                        }
                    }

                    control.Dispose();
                }
                finally
                {
                    AlphaSeparationSplitTests.DeleteSplitFolder();
                }
            }
            finally
            {
                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// One full-build parity fixture: a cutout split renderer with a
        /// swap clip (the appended-slot fixture), a Poiyomi verified-opaque
        /// renderer, and — in the sibling variant — a transparent
        /// conversion renderer last, so registration order keeps the
        /// control artifacts' retention names stable.
        /// </summary>
        private sealed class TransparentParityResult : IDisposable
        {
            private readonly List<UnityEngine.Object> owned =
                new List<UnityEngine.Object>();

            private GameObject root;
            private AnimationClip clip;
            private AnimatorController controller;
            private AmusePlatformFinishState state;

            internal AmusePlatformFinishState State => state;
            internal Material TransparentMaterial { get; private set; }
            internal string CutoutCloneFacts { get; private set; }
            internal string CutoutCloneName { get; private set; }
            internal string CutoutAppendedCurve { get; private set; }
            internal string CutoutMeshIndices { get; private set; }
            internal string CutoutOwnCurve { get; private set; }
            internal string PoiyomiCloneFacts { get; private set; }
            internal string PoiyomiName { get; private set; }
            internal int AppliedRendererCount { get; private set; }
            internal int AppliedOpaqueTriangleCount { get; private set; }

            private T Own<T>(T obj) where T : UnityEngine.Object
            {
                if (obj != null)
                {
                    owned.Add(obj);
                }

                return obj;
            }

            internal void Build(
                LilToonCutoutConversionFixtures fixtures,
                bool withTransparentSibling)
            {
                root = new GameObject(
                    "AMUSE transparent parity " +
                    (withTransparentSibling ? "sibling" : "control"));
                root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                try
                {
                    // The cutout + Poiyomi fixture: exactly the proven
                    // full-artifact shape — one renderer whose slot 0 is a
                    // Poiyomi verified-opaque whole slot and whose slot 1
                    // is a cutout split slot with a swap clip — so the
                    // control expectations are the pre-existing ones.
                    var splitTexture =
                        AlphaSeparationSplitTests.ImportSplitAlphaTexture(
                            "parity_split_" +
                            (withTransparentSibling ? "sibling" : "control"));
                    var cutout = Own(
                        NewCutoutSplitMaterial(splitTexture));
                    var swap = Own(
                        LilToonFixtureTestBase.CreateVerifiedMaterial());
                    var poiyomi = Own(VerifiedOpaqueMaterial());

                    var sourceMesh = Own(
                        AlphaSeparationSplitTests
                            .CreateOpaqueAndSplitSourceMesh());
                    var cutoutRenderer = AddRenderer(
                        root, "cutoutBody", sourceMesh, poiyomi, cutout);

                    clip = Own(NewSwapClip(
                        "AMUSE parity swap", "cutoutBody", 1,
                        (0f, cutout), (1f, swap)));
                    controller = Own(NewController(
                        root, "AMUSE parity graph", clip));

                    if (withTransparentSibling)
                    {
                        var transparent = Own(
                            NewTransparentConversionMaterial(
                                fixtures.ImportFullyOpaqueMipmap(
                                    "parity_transparent")));
                        var transparentMesh = Own(SingleTriangleMesh());
                        transparentMesh.uv = new[]
                        {
                            new Vector2(0.25f, 0.25f),
                            new Vector2(0.75f, 0.25f),
                            new Vector2(0.25f, 0.75f),
                        };

                        // Added last, so registration order keeps the
                        // control artifacts' retention names stable.
                        AddRenderer(
                            root, "transparentBody",
                            transparentMesh, transparent);
                        TransparentMaterial = transparent;
                    }

                    var context = AvatarProcessor.ProcessAvatar(
                        root, ApplyTestPlatform.Instance);
                    state = context.GetState<AmusePlatformFinishState>();

                    Assert.That(context.Successful, Is.True,
                        "fixture precondition: the build must complete");
                    Assert.That(state.Separation, Is.Not.Null);
                    AssertNoFeatureRefusals(state);

                    var cutoutClone =
                        state.Separation.OpaqueBySource[cutout];
                    var poiClone =
                        state.Separation.OpaqueBySource[poiyomi];
                    CutoutCloneFacts =
                        DigestMaterialWithoutTextures(cutoutClone);
                    CutoutCloneName = cutoutClone.name;
                    PoiyomiCloneFacts =
                        DigestMaterialWithoutTextures(poiClone);
                    PoiyomiName = poiClone.name;
                    CutoutOwnCurve = DescribeAuthoredCurve(
                        clip, "cutoutBody",
                        "m_Materials.Array.data[0]");
                    CutoutMeshIndices = DescribeSubmeshIndices(
                        cutoutRenderer.sharedMesh);
                    AppliedRendererCount =
                        state.AppliedRendererCount;
                    AppliedOpaqueTriangleCount =
                        state.AppliedOpaqueTriangleCount;

                    var appendedClip = CommittedClipWithObjectBinding(
                        root, "cutoutBody",
                        "m_Materials.Array.data[2]");
                    CutoutOwnCurve = DescribeAuthoredCurve(
                        clip, "cutoutBody",
                        "m_Materials.Array.data[1]");
                    CutoutAppendedCurve = DescribeAuthoredCurve(
                        appendedClip, "cutoutBody",
                        "m_Materials.Array.data[2]");
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                DestroyCommittedClone(root, controller);
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
                for (var index = owned.Count - 1; index >= 0; index--)
                {
                    if (owned[index] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(owned[index]);
                    }
                }

                owned.Clear();
            }
        }

        /// <summary>
        /// A convertible transparent fixture material over a fully-opaque
        /// mip chain. The caller owns destruction through the tracker.
        /// </summary>
        private static Material NewTransparentConversionMaterial(
            Texture2D mipTexture)
        {
            var material =
                LilToonFixtureTestBase.CreateTransparentConversionMaterial();
            material.SetTexture("_MainTex", mipTexture);
            return material;
        }

        /// <summary>
        /// The characterized clone facts a cross-build digest may compare:
        /// shader identity by name, queue, RenderType, keywords and every
        /// shader-declared non-texture property value. Texture references
        /// are deliberately excluded — each parity build imports its own
        /// texture assets — so the digest compares conversion results, not
        /// fixture identities.
        /// </summary>
        private static string DigestMaterialWithoutTextures(Material material)
        {
            var shader = material.shader;
            var parts = new List<string>
            {
                material.name,
                shader.name,
                material.renderQueue.ToString(CultureInfo.InvariantCulture),
                material.GetTag("RenderType", false),
                "keywords=" + string.Join(
                    ",", material.shaderKeywords
                        .OrderBy(keyword => keyword, StringComparer.Ordinal)),
            };

            var count = shader.GetPropertyCount();
            for (var index = 0; index < count; index++)
            {
                var propertyName = shader.GetPropertyName(index);
                var type = shader.GetPropertyType(index);
                if (type ==
                    UnityEngine.Rendering.ShaderPropertyType.Texture)
                {
                    continue;
                }

                parts.Add(
                    propertyName + ":" + type + "=" +
                    DescribeNonTexturePropertyValue(
                        material, propertyName, type));
            }

            return string.Join("\n", parts);
        }

        /// <summary>
        /// Describes a shader property value of any non-texture type, in a
        /// culture-invariant round-trip form the cross-build digest can
        /// compare byte for byte.
        /// </summary>
        private static string DescribeNonTexturePropertyValue(
            Material material,
            string propertyName,
            UnityEngine.Rendering.ShaderPropertyType type)
        {
            switch (type)
            {
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                    var color = material.GetColor(propertyName);
                    return "(" + color.r.ToString("R", CultureInfo.InvariantCulture) +
                           "," + color.g.ToString("R", CultureInfo.InvariantCulture) +
                           "," + color.b.ToString("R", CultureInfo.InvariantCulture) +
                           "," + color.a.ToString("R", CultureInfo.InvariantCulture) +
                           ")";
                case UnityEngine.Rendering.ShaderPropertyType.Vector:
                    var vector = material.GetVector(propertyName);
                    return "(" + vector.x.ToString("R", CultureInfo.InvariantCulture) +
                           "," + vector.y.ToString("R", CultureInfo.InvariantCulture) +
                           "," + vector.z.ToString("R", CultureInfo.InvariantCulture) +
                           "," + vector.w.ToString("R", CultureInfo.InvariantCulture) +
                           ")";
                case UnityEngine.Rendering.ShaderPropertyType.Float:
                case UnityEngine.Rendering.ShaderPropertyType.Range:
                    return material.GetFloat(propertyName)
                        .ToString("R", CultureInfo.InvariantCulture);
                case UnityEngine.Rendering.ShaderPropertyType.Int:
                    return material.GetInteger(propertyName)
                        .ToString(CultureInfo.InvariantCulture);
                default:
                    return "<unhandled " + type + ">";
            }
        }

        private static string DescribeSubmeshIndices(Mesh mesh)
        {
            var parts = new List<string>
            {
                mesh.subMeshCount.ToString(CultureInfo.InvariantCulture),
            };
            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                parts.Add(string.Join(
                    ",",
                    mesh.GetIndices(submesh).Select(index =>
                        index.ToString(CultureInfo.InvariantCulture))));
            }

            return string.Join("|", parts);
        }

        // --- Cutout conversion fixture helpers -------------------------------

        /// <summary>
        /// The one test-owned directory NDMF's persistence scope is pointed
        /// at for the full-artifact scenario; deleted unconditionally in
        /// that test's finally.
        /// </summary>
        private const string ApplyCutoutPersistenceFolder =
            "Assets/AmuseTests_AlphaApplyCutout";

        /// <summary>
        /// Imports the fully-opaque mipmap texture the cutout conversion
        /// fixture assigns to <c>_MainTex</c>. The base's SetUp/TearDown are
        /// driven manually: NUnit never instantiates this helper.
        /// </summary>
        private sealed class LilToonCutoutConversionFixtures
            : LilToonFixtureTestBase
        {
            internal Texture2D ImportFullyOpaqueMipmap(string name)
            {
                var pixels = new Color32[4 * 4];
                for (var index = 0; index < pixels.Length; index++)
                {
                    pixels[index] = new Color32(255, 255, 255, 255);
                }

                return ImportMipmapTexture(name, 4, 4, pixels);
            }
        }

        private sealed class LilToonConversionShaderNames
            : LilToonFixtureTestBase
        {
            /// <summary>The tuple-carrying attested opaque stand-in.</summary>
            internal const string OpaqueTarget = OpaqueConversionShaderName;

            /// <summary>The schema-complete transparent source stand-in.</summary>
            internal const string Transparent = TransparentConversionShaderName;
        }

        /// <summary>
        /// The convertible cutout fixture material over a fully-opaque
        /// mipmap chain: cutout schema plus the full canonical tuple at
        /// canonical defaults.
        /// </summary>
        private static Material NewCutoutConversionMaterial(
            Texture2D mipTexture)
        {
            var material =
                LilToonFixtureTestBase.CreateCutoutConversionMaterial();
            material.SetTexture("_MainTex", mipTexture);
            return material;
        }

        /// <summary>
        /// The convertible cutout fixture material over the mip-free split
        /// alpha texture, so one submesh can carry one proven-opaque and one
        /// unproven triangle — a Split.
        /// </summary>
        private static Material NewCutoutSplitMaterial(
            Texture2D splitTexture)
        {
            var material =
                LilToonFixtureTestBase.CreateCutoutConversionMaterial();
            material.SetTexture("_MainTex", splitTexture);
            return material;
        }

        /// <summary>
        /// Reads back every canonical opaque fact on a generated cutout
        /// clone: the eighteen recipe scalars, the render queue, the
        /// RenderType tag, the whole-fact comparison, and the attested
        /// opaque stand-in target.
        /// </summary>
        private static void AssertCanonicalOpaqueRecipe(Material clone)
        {
            foreach (var (property, value) in
                         LilToonOpaqueTarget.CanonicalOpaqueProperties)
            {
                Assert.That(clone.GetFloat(property), Is.EqualTo(value),
                    "canonical recipe '" + property + "'");
            }

            Assert.That(clone.renderQueue,
                Is.EqualTo(
                    LilToonOpaqueTarget.CanonicalOpaqueRenderQueue));
            Assert.That(
                clone.GetTag(
                    LilToonOpaqueTarget.RenderTypeTagName, false),
                Is.EqualTo(
                    LilToonOpaqueTarget.CanonicalOpaqueRenderType));
            Assert.That(
                LilToonOpaqueTarget.TryFindNonCanonicalFact(
                    clone, out _),
                Is.False,
                "every canonical fact must read back on the clone");
            Assert.That(clone.shader,
                Is.SameAs(Shader.Find(
                    LilToonConversionShaderNames.OpaqueTarget)),
                "the clone must carry the attested opaque stand-in " +
                "target");
        }

        /// <summary>
        /// The source-material facts a plausible conversion could falsify:
        /// name, shader, queue, RenderType tag, main texture, tint, cutoff
        /// and every canonical recipe scalar.
        /// </summary>
        private static string DigestMaterial(Material material)
        {
            var parts = new List<string>
            {
                material.name,
                material.shader.name,
                material.renderQueue
                    .ToString(CultureInfo.InvariantCulture),
                material.GetTag("RenderType", false),
                material.mainTexture == null
                    ? "<none>"
                    : material.mainTexture.GetInstanceID()
                        .ToString(CultureInfo.InvariantCulture),
                string.Join(
                    ",",
                    material.GetColor("_Color").r
                        .ToString("R", CultureInfo.InvariantCulture),
                    material.GetColor("_Color").g
                        .ToString("R", CultureInfo.InvariantCulture),
                    material.GetColor("_Color").b
                        .ToString("R", CultureInfo.InvariantCulture),
                    material.GetColor("_Color").a
                        .ToString("R", CultureInfo.InvariantCulture)),
                material.GetFloat("_Cutoff")
                    .ToString("R", CultureInfo.InvariantCulture),
            };
            foreach (var (property, _) in
                         LilToonOpaqueTarget.CanonicalOpaqueProperties)
            {
                parts.Add(
                    material.GetFloat(property)
                        .ToString("R", CultureInfo.InvariantCulture));
            }

            return string.Join("|", parts);
        }

        /// <summary>
        /// The source-mesh facts a plausible finalization could falsify:
        /// name, vertex count, submesh count, bounds and every submesh's
        /// index buffer.
        /// </summary>
        private static string DigestMesh(Mesh mesh)
        {
            var parts = new List<string>
            {
                mesh.name,
                mesh.vertexCount.ToString(CultureInfo.InvariantCulture),
                mesh.subMeshCount.ToString(CultureInfo.InvariantCulture),
                string.Join(
                    ",",
                    mesh.bounds.center.x
                        .ToString("R", CultureInfo.InvariantCulture),
                    mesh.bounds.center.y
                        .ToString("R", CultureInfo.InvariantCulture),
                    mesh.bounds.center.z
                        .ToString("R", CultureInfo.InvariantCulture)),
                string.Join(
                    ",",
                    mesh.bounds.extents.x
                        .ToString("R", CultureInfo.InvariantCulture),
                    mesh.bounds.extents.y
                        .ToString("R", CultureInfo.InvariantCulture),
                    mesh.bounds.extents.z
                        .ToString("R", CultureInfo.InvariantCulture)),
            };
            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                parts.Add(string.Join(",", mesh.GetIndices(submesh)));
            }

            return string.Join("|", parts);
        }
    }
}
