using System.Collections.Generic;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Tests.Editor.Shared;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The D8 consent layer: range-admitted versions beyond the last
    /// re-attested maximum, and majors at or beyond the declared bound,
    /// require an explicit click-through on every build. Range refusals
    /// never prompt, and consent never overrides a refusal.
    /// </summary>
    public sealed class VersionConsentTests
    {
        private static HostLifecycleFacts SupportedFacts(
            string unityVersion = "2022.3.22f1",
            string ndmfVersion = "1.14.8",
            string vrchatSdkBaseVersion = "3.10.5",
            string vrchatSdkAvatarsVersion = "3.10.5")
        {
            return new HostLifecycleFacts(
                unityVersion,
                ndmfVersion,
                vrchatSdkBaseVersion,
                vrchatSdkAvatarsVersion,
                "nadena.dev.ndmf.vrchat.avatar3",
                AmuseBuildPath.NonPlayNdmfBuild,
                true,
                true,
                true,
                true);
        }

        [Test]
        public void UnityAboveAttestedPatchRequiresConsent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(unityVersion: "2022.3.23f1"));

            Assert.That(result.MayUsePositiveMutation, Is.True);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
            Assert.That(result.ConsentRequired, Is.True);
            Assert.That(result.ConsentSubjects.Count, Is.EqualTo(1));
            Assert.That(
                result.ConsentSubjects[0],
                Does.Contain("2022.3.23f1"));
        }

        [Test]
        public void UnityAtAttestedPatchIsSilent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(unityVersion: "2022.3.22f1"));

            Assert.That(result.ConsentRequired, Is.False);
            Assert.That(result.ConsentSubjects, Is.Empty);
        }

        [Test]
        public void NdmfAboveAttestedMaxRequiresConsent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.14.9"));

            Assert.That(result.ConsentRequired, Is.True);
            Assert.That(result.ConsentSubjects.Count, Is.EqualTo(1));
            Assert.That(result.ConsentSubjects[0], Does.Contain("1.14.9"));
        }

        [Test]
        public void NdmfAtAttestedMaxIsSilent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.14.8"));

            Assert.That(result.ConsentRequired, Is.False);
        }

        /// <summary>
        /// Every in-range version at or below the attested maximum sits in
        /// the re-attested interval, so 1.14.5 needs no consent.
        /// </summary>
        [Test]
        public void NdmfInRangeBelowAttestedMaxIsSilent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.14.5"));

            Assert.That(result.ConsentRequired, Is.False);
        }

        [Test]
        public void NdmfMajorAtDeclaredBoundRequiresConsentNotRefusal()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "2.0.0"));

            Assert.That(result.MayUsePositiveMutation, Is.True,
                "the declared major bound is a consent subject, not a " +
                "refusal (decision V8)");
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
            Assert.That(result.ConsentRequired, Is.True);
            Assert.That(result.ConsentSubjects[0], Does.Contain("2.0.0"));
        }

        [Test]
        public void NdmfMajorBeyondDeclaredBoundRequiresConsent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "2.1.0"));

            Assert.That(result.ConsentRequired, Is.True);
            Assert.That(result.ConsentSubjects[0], Does.Contain("2.1.0"));
        }

        [Test]
        public void SdkAboveAttestedMaxRequiresConsent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(
                    vrchatSdkBaseVersion: "3.10.6",
                    vrchatSdkAvatarsVersion: "3.10.6"));

            Assert.That(result.ConsentRequired, Is.True);
            Assert.That(result.ConsentSubjects.Count, Is.EqualTo(2));
        }

        [Test]
        public void SdkMajorAtDeclaredBoundRequiresConsentNotRefusal()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(
                    vrchatSdkBaseVersion: "4.0.0",
                    vrchatSdkAvatarsVersion: "4.0.0"));

            Assert.That(result.MayUsePositiveMutation, Is.True);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
            Assert.That(result.ConsentRequired, Is.True);
        }

        [Test]
        public void BelowFloorVersionStillRefusesWithoutConsent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.14.3"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedNdmfVersion));
            Assert.That(result.ConsentRequired, Is.False,
                "a range refusal never prompts: consent never overrides " +
                "a refusal");
        }

        [Test]
        public void PrereleaseVersionStillRefusesWithoutConsent()
        {
            var result = HostLifecycleCapability.Evaluate(
                SupportedFacts(ndmfVersion: "1.15.0-beta.1"));

            Assert.That(result.MayUsePositiveMutation, Is.False);
            Assert.That(result.ConsentRequired, Is.False);
        }

        [Test]
        public void EmptySubjectsProceedWithoutAsking()
        {
            var asked = false;
            var result = VersionConsentDialog.ShouldProceed(
                new string[0], false,
                subjects =>
                {
                    asked = true;
                    return true;
                });

            Assert.That(result, Is.True);
            Assert.That(asked, Is.False,
                "nothing to ask about must never open a dialog");
        }

        [Test]
        public void BatchModeRefusesWithoutAsking()
        {
            var asked = false;
            var result = VersionConsentDialog.ShouldProceed(
                new[] { "NDMF 1.14.9 is newer than the last verified 1.14.8." },
                true,
                subjects =>
                {
                    asked = true;
                    return true;
                });

            Assert.That(result, Is.False,
                "batch mode has no user to ask, so it refuses");
            Assert.That(asked, Is.False);
        }

        [Test]
        public void PresenterDecisionPassesThrough()
        {
            Assert.That(
                VersionConsentDialog.ShouldProceed(
                    new[] { "subject" }, false, subjects => true),
                Is.True);
            Assert.That(
                VersionConsentDialog.ShouldProceed(
                    new[] { "subject" }, false, subjects => false),
                Is.False);
        }

        /// <summary>
        /// A material assigned nowhere, referenced only by an
        /// object-reference curve on a stored-graph clip, must still get
        /// its consent subject: the D8 pre-scan walks assigned materials
        /// and the swap curves of the stored committed graph. The subject
        /// names the swap-only shader, and a granted build carries its
        /// name.
        /// </summary>
        [Test]
        public void SwapOnlyMaterialProducesSubjectAndGrantedName()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var shaderScope = new TestTransientScope(
                ConsentSwapShaderFolder);
            shaderScope.EnsureTempFolder();
            var shader = TestShaderWriter.WriteTestShader(
                ConsentSwapShaderFolder + "/swap-consent-cutout.shader",
                StandInShaderText(
                    LilToonSourceAttestation.CutoutShaderName));
            var swapMaterial = shaderScope.Track(new Material(shader));
            var assignedMaterial = new Material(Shader.Find("Unlit/Color"));
            var mesh = TriangleMesh();
            var root = new GameObject("AMUSE swap consent fixture");
            var clip = new AnimationClip { name = "swap consent clip" };
            AnimationUtility.SetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(
                    "",
                    typeof(SkinnedMeshRenderer),
                    "m_Materials.Array.data[0]"),
                new[]
                {
                    new ObjectReferenceKeyframe
                    {
                        time = 0f,
                        value = swapMaterial,
                    },
                });
            var presentations = new List<IReadOnlyList<string>>();

            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<
                    Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                FixtureProofScope.PinAllSizes(root);
                var renderer = root.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.sharedMaterials = new[] { assignedMaterial };

                var context = AvatarProcessor.ProcessAvatar(
                    root, ConsentTestPlatform.Instance);
                var state = context.GetState<AmusePlatformFinishState>();
                state.AnimatorBindings =
                    GenericPlatformAnimatorBindings.Instance;
                state.StructuralGraph = StoredGraphWithClip(clip);

                AmusePlatformFinishPass.Execute(
                    context,
                    SupportedFacts(),
                    subjects =>
                    {
                        presentations.Add(subjects);
                        return true;
                    });

                var after = context.GetState<AmusePlatformFinishState>();
                Assert.That(
                    presentations,
                    Has.Count.EqualTo(1),
                    "the swap-only material's shader must be offered as " +
                    "one consent subject");
                Assert.That(
                    presentations[0],
                    Has.Count.EqualTo(1),
                    "the unsupported assigned material produces no " +
                    "subject, so the swap-only shader is the only one");
                Assert.That(
                    presentations[0][0],
                    Does.Contain(
                        LilToonSourceAttestation.CutoutShaderName),
                    "the subject must name the swap-only shader");
                Assert.That(
                    after.ConsentDeclined,
                    Is.False,
                    "the granted build must carry the swap-only name");
                Assert.That(
                    after.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None));
            }
            finally
            {
                shaderScope.TearDown();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(assignedMaterial);
                Object.DestroyImmediate(clip);
            }
        }

        /// <summary>
        /// The consent dialog runs after the committed-graph gate: an
        /// avatar that refuses avatar-wide on the stored graph must never
        /// be asked, and the refusal itself is unchanged.
        /// </summary>
        [Test]
        public void GraphRefusedAvatarNeverOpensTheConsentDialog()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var shaderScope = new TestTransientScope(
                ConsentSwapShaderFolder);
            shaderScope.EnsureTempFolder();
            var shader = TestShaderWriter.WriteTestShader(
                ConsentSwapShaderFolder + "/graph-refused-cutout.shader",
                StandInShaderText(
                    LilToonSourceAttestation.CutoutShaderName));
            var assignedMaterial = shaderScope.Track(new Material(shader));
            var mesh = TriangleMesh();
            var root = new GameObject("AMUSE graph refused consent fixture");
            var presentations = new List<IReadOnlyList<string>>();

            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<
                    Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                FixtureProofScope.PinAllSizes(root);
                var renderer = root.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.sharedMaterials = new[] { assignedMaterial };

                var context = AvatarProcessor.ProcessAvatar(
                    root, ConsentTestPlatform.Instance);
                var state = context.GetState<AmusePlatformFinishState>();
                state.AnimatorBindings =
                    GenericPlatformAnimatorBindings.Instance;
                state.StructuralGraph = new CommittedControllerGraphResult(
                    AvatarAnimationRefusal.UnsupportedAnimatorControllerForm,
                    System.Array.Empty<CommittedLayer>());

                AmusePlatformFinishPass.Execute(
                    context,
                    SupportedFacts(),
                    subjects =>
                    {
                        presentations.Add(subjects);
                        return true;
                    });

                var after = context.GetState<AmusePlatformFinishState>();
                Assert.That(
                    presentations,
                    Is.Empty,
                    "a graph-refused avatar must never see the consent " +
                    "dialog");
                Assert.That(
                    after.AvatarRefusal,
                    Is.EqualTo(
                        AvatarAnimationRefusal
                            .UnsupportedAnimatorControllerForm),
                    "the avatar refusal is unchanged by the moved block");
                Assert.That(after.ConsentDeclined, Is.False);
                Assert.That(after.AnalyzedRendererCount, Is.Zero);
            }
            finally
            {
                shaderScope.TearDown();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }

        private const string ConsentSwapShaderFolder =
            "Assets/AmuseTests_ConsentSwap";

        /// <summary>
        /// A stand-in shader asset carrying a supported family's exact
        /// vendor name, so the D8 pre-scan selects the family while its
        /// unpinned source keeps the consent subject alive.
        /// </summary>
        private static string StandInShaderText(string shaderName)
        {
            return "Shader \"" + shaderName + "\"\n" +
                "{\n    Properties\n    {\n" +
                "        _MainTex (\"Main\", 2D) = \"white\" {}\n" +
                "        _Color (\"Color\", Color) = (1,1,1,1)\n" +
                "        _Cutoff (\"Cutoff\", Range(0, 1)) = 0.5\n" +
                "    }\n    SubShader { Pass {} }\n}\n";
        }

        private static Mesh TriangleMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up,
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            return mesh;
        }

        private static CommittedControllerGraphResult StoredGraphWithClip(
            AnimationClip clip)
        {
            return new CommittedControllerGraphResult(
                AvatarAnimationRefusal.None,
                new[]
                {
                    new CommittedLayer(
                        "swap-consent",
                        0,
                        AnimatorLayerBlendingMode.Override,
                        new[] { clip },
                        System.Array.Empty<StateMachineBehaviour>(),
                        false),
                });
        }

        private sealed class ConsentTestPlatform : INDMFPlatformProvider
        {
            internal static readonly ConsentTestPlatform Instance =
                new ConsentTestPlatform();

            public string QualifiedName => "nadena.dev.ndmf.generic";
            public string DisplayName => "AMUSE consent test generic";
        }
    }
}