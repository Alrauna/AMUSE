using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
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
                        AlphaSeparationApply.Execute));
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
        /// The conversion delegate defaults to the verified seam; a test that
        /// must substitute a different conversion boundary — e.g. an identity
        /// mapping — arms <see cref="ConversionOverride"/> for the duration
        /// of its build, the same way <see cref="SpecialClipNames"/> works.
        /// </summary>
        private static void BarrierPass(BuildContext context)
        {
            AmusePlatformFinishPass.Execute(
                context,
                SupportedFacts(),
                VerifiedPoiyomiTestSeams.SelectVerifiedFixtureRequest,
                VerifiedPoiyomiTestSeams.CaptureVerifiedFixtureMaterials,
                VerifiedPoiyomiTestSeams.VerifiedAlphaOnly,
                ConversionOverride ??
                    VerifiedPoiyomiTestSeams.VerifiedConversion);
        }

        private static VerifiedOpaqueConversion ConversionOverride
        {
            get;
            set;
        }

        private sealed class ConversionOverrideScope : IDisposable
        {
            private readonly VerifiedOpaqueConversion previous;

            internal ConversionOverrideScope(
                VerifiedOpaqueConversion conversion)
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
    }
}
