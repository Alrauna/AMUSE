using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Alrauna.Amuse.Runtime;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The production-order vendor fixtures of the stage 5 plan. One
    /// dispatcher call drives the real registered SDK callbacks in the
    /// production order: NDMF part one, VRCFury, NDMF part two, then
    /// d4rkAvatarOptimizer when installed. The tier A fixtures carry the
    /// vendor components and build through that dispatcher, so the
    /// vendor VRChat passes of Modular Avatar and VRCFury run against
    /// the fixture exactly as a real avatar upload drives them. The
    /// tier B twins inject the equivalent animation input by hand and
    /// build on the dedicated test platform, so the AMUSE behavior
    /// assertions hold without the VRChat SDK.
    /// <para>
    /// Every fixture asserts the shared triangle soundness oracle and a
    /// survival assertion on the committed graph. The survival
    /// assertion names the vendor output or the hand injected input, so
    /// a silent vendor skip or a silent input loss cannot turn green.
    /// </para>
    /// <para>
    /// The test assembly references no Modular Avatar or VRCFury
    /// assembly, and the VRCFury model types are internal in the vendor
    /// assembly, so every vendor type resolves by assembly qualified
    /// reflection. A null resolution is a named skip that names the
    /// absent package and its locked version.
    /// </para>
    /// </summary>
    public sealed class VendorProductionOrderTests
        : MergedConsumptionFixture
    {
        private const string TempFolderName = "AmuseTests_ProductionOrder";

        protected override string TempFolder =>
            "Assets/" + TempFolderName;

        private const string ModularAvatarPackageId =
            "nadena.dev.modular-avatar";
        private const string ModularAvatarLockedVersion = "1.18.7";
        private const string VrcFuryPackageId = "com.vrcfury.vrcfury";
        private const string VrcFuryLockedVersion = "1.1442.0";

        private const string MergeAnimatorTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator,"
            + " nadena.dev.modular-avatar.core";
        private const string MaObjectToggleTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarObjectToggle,"
            + " nadena.dev.modular-avatar.core";
        private const string MaMenuItemTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMenuItem,"
            + " nadena.dev.modular-avatar.core";
        private const string MaToggledObjectTypeName =
            "nadena.dev.modular_avatar.core.ToggledObject,"
            + " nadena.dev.modular-avatar.core";
        private const string MaObjectReferenceTypeName =
            "nadena.dev.modular_avatar.core.AvatarObjectReference,"
            + " nadena.dev.modular-avatar.core";
        private const string MaToggleProbeName =
            "AMUSE production order MA toggle probe";
        private const string MaToggleHolderName =
            "AMUSE production order MA toggle holder";
        private const string MaToggleParameterName =
            "AMUSE production order MA toggle";
        private const string VrcFuryTypeName = "VF.Model.VRCFury, VRCFury";
        private const string FuryToggleTypeName =
            "VF.Model.Feature.Toggle, VRCFury";
        private const string FuryObjectToggleActionTypeName =
            "VF.Model.StateAction.ObjectToggleAction, VRCFury";

        private const string MaSwapClipName =
            "AMUSE production order MA swap";
        private const string MaToggleTwinClipName =
            "AMUSE production order MA toggle twin";
        private const string FuryToggleName =
            "AMUSE production order fury toggle";
        private const string FuryTwinSwapClipName =
            "AMUSE production order fury twin swap";

        private const string MaHolderName =
            "AMUSE production order MA holder";
        private const string FuryHolderName =
            "AMUSE production order fury holder";

        private const string VrcFuryBuildsFolder =
            "Packages/com.vrcfury.temp/Builds";
        private const string NdmfGeneratedAssetsRoot =
            "Packages/nadena.dev.ndmf/__Generated";

        // True from a dispatched build until its teardown ran. The
        // residue cleanup and the tier A texture arm both read it, so
        // tier B twins, skipped runs, and every other suite keep the
        // strict behavior and delete nothing.
        private static bool _dispatchRanThisTest;

        // The avatar name of the dispatched build. The teardown deletes
        // the vendor generated assets folder of that name, so the next
        // dispatched build of the same avatar never rebuilds over a
        // stale deleted folder. The vendor AssetSaver deletes and
        // recreates that folder per build, and a stale deletion state
        // turned its asset path empty on 2026-11-10.
        private static string _dispatchedAvatarName;

        // The Builds folder entry names before the dispatch of the
        // running test. The teardown deletes only the new entries, so
        // vendor content that predates the test survives.
        private static readonly HashSet<string> BuildsFolderSnapshot =
            new HashSet<string>(StringComparer.Ordinal);

        // --- tier B twins ---------------------------------------------------

        /// <summary>
        /// The tier B twin of the Modular Avatar merge animator fixture.
        /// The swap controller input is injected by hand on the root
        /// Animator, and the build runs on the dedicated platform
        /// through the mirrored five pass topology. No vendor component
        /// and no VRChat SDK take part.
        /// </summary>
        [Test]
        public void ModularAvatarMergeAnimatorKeepsTrianglesSound()
        {
            RequireLilToonEnvironment(
                out var transparentShader, out var cutoutShader);
            AssetDatabase.CreateFolder("Assets", TempFolderName);

            var root = new GameObject("AMUSE production order MA twin");
            try
            {
                root.AddComponent<AmuseAvatarOptimizer>();
                FixtureProofScope.AcceptDesktopAnisotropyDifferences(root);

                AttachBandedRenderer(root, MaHolderName,
                    "AMUSE production order MA twin mesh",
                    "banded_production_ma", transparentShader,
                    cutoutShader, out var transparent, out var cutout);

                var swapClip = Track(
                    AlphaSeparationTestLifecycle.NewSwapClip(
                        MaSwapClipName, MaHolderName, 0,
                        (0f, transparent), (1f, cutout)));
                AlphaSeparationTestLifecycle.NewController(
                    root, "AMUSE production order MA twin graph",
                    swapClip);

                BuildOnTierBPlatform(root);

                AssertCommittedRootGraphCarriesMotion(
                    root, MaSwapClipName);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The tier B twin of the VRCFury toggle fixture. VRCFury runs
        /// only through SDK callbacks, so the twin injects the
        /// equivalent animation input by hand: one swap clip on the
        /// root Animator carries the animation the vendor layer would
        /// drive. The build runs on the dedicated platform.
        /// </summary>
        [Test]
        public void VRCFuryToggleKeepsTrianglesSound()
        {
            RequireLilToonEnvironment(
                out var transparentShader, out var cutoutShader);
            AssetDatabase.CreateFolder("Assets", TempFolderName);

            var root = new GameObject("AMUSE production order fury twin");
            try
            {
                root.AddComponent<AmuseAvatarOptimizer>();
                FixtureProofScope.AcceptDesktopAnisotropyDifferences(root);

                AttachBandedRenderer(root, FuryHolderName,
                    "AMUSE production order fury twin mesh",
                    "banded_production_fury", transparentShader,
                    cutoutShader, out var transparent, out var cutout);

                var swapClip = Track(
                    AlphaSeparationTestLifecycle.NewSwapClip(
                        FuryTwinSwapClipName, FuryHolderName, 0,
                        (0f, transparent), (1f, cutout)));
                AlphaSeparationTestLifecycle.NewController(
                    root, "AMUSE production order fury twin graph",
                    swapClip);

                BuildOnTierBPlatform(root);

                AssertCommittedRootGraphCarriesMotion(
                    root, FuryTwinSwapClipName);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The tier B twin of the Modular Avatar object toggle fixture.
        /// The vendor reactive object system animates the renderer
        /// holder active state on the VRChat platform, so the twin
        /// injects the equivalent animation input by hand: one holder
        /// active state clip on the root Animator. The build runs on
        /// the dedicated platform through the mirrored five pass
        /// topology. No vendor component and no VRChat SDK take part.
        /// </summary>
        [Test]
        public void ModularAvatarObjectToggleKeepsTrianglesSound()
        {
            RequireLilToonEnvironment(
                out var transparentShader, out var cutoutShader);
            AssetDatabase.CreateFolder("Assets", TempFolderName);

            var root = new GameObject("AMUSE production order MA toggle twin");
            try
            {
                root.AddComponent<AmuseAvatarOptimizer>();
                FixtureProofScope.AcceptDesktopAnisotropyDifferences(root);

                AttachBandedRenderer(root, MaToggleHolderName,
                    "AMUSE production order MA toggle twin mesh",
                    "banded_production_ma", transparentShader,
                    cutoutShader, out _, out _);

                var toggleClip = Track(NewHolderToggleClip(
                    MaToggleTwinClipName, MaToggleHolderName));
                AlphaSeparationTestLifecycle.NewController(
                    root, "AMUSE production order MA toggle twin graph",
                    toggleClip);

                BuildOnTierBPlatform(root);

                AssertCommittedRootGraphCarriesMotion(
                    root, MaToggleTwinClipName);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// One hand built clip that holds the fixture renderer holder
        /// on. The vendor reactive object system animates the same
        /// property through the menu toggle on the VRChat platform, so
        /// the twin injects the equivalent animation input by hand.
        /// </summary>
        private static AnimationClip NewHolderToggleClip(
            string name, string holderPath)
        {
            var clip = new AnimationClip { name = name };
            clip.SetCurve(
                holderPath, typeof(GameObject), "m_IsActive",
                AnimationCurve.Constant(0f, 1f, 1f));
            return clip;
        }

        private void BuildOnTierBPlatform(GameObject root)
        {
            // The production capture route is the tier B lifecycle the
            // real material twins of the alpha separation suite use:
            // the production barrier entry over real installed
            // materials, with the admitted lifecycle facts. The
            // production barrier refuses every platform whose name is
            // not the VRChat name, so the route drives the barrier
            // through those facts on this dedicated platform.
            BuildContext context;
            using (new AlphaSeparationApplyTests.ProductionCaptureScope())
            {
                context = AvatarProcessor.ProcessAvatar(
                    root,
                    AlphaSeparationApplyTests.ApplyTestPlatform.Instance);
            }

            AssertBuiltTrianglesMatchAuthoring(context, root);
        }

        // --- tier A fixtures ------------------------------------------------

        /// <summary>
        /// The tier A fixture of the Modular Avatar merge animator. The
        /// standard fixture avatar carries one reflected
        /// ModularAvatarMergeAnimator component that merges one swap
        /// controller into the FX layer. The build runs through the
        /// installed SDK callback dispatcher, so the whole production
        /// order executes. The oracle holds on the built avatar and the
        /// committed FX graph carries the merged swap layer, so a
        /// silent vendor skip cannot turn green.
        /// </summary>
        [Test]
        [Category(VendorVrChatTierGate.TierACategory)]
        public void ModularAvatarMergeAnimatorKeepsTrianglesSoundVendorVRChat()
        {
            VendorVrChatTierGate.RequireInstalledVrcSdkPackages();
            // The dispatcher runs VRCFury on every avatar, and VRCFury
            // saves its generated controllers into its own project
            // folder during the dispatch. That write is project level
            // state, so the fixture runs only behind its named opt-in
            // gate (V15).
            VendorOptInGate.Require(
                VendorOptInGate.ProductionOrderDispatch,
                "Set " + VendorOptInGate.ProductionOrderDispatch + "=1"
                + " in a disposable project to run this fixture. The"
                + " SDK callback dispatcher runs VRCFury, and VRCFury"
                + " writes generated controllers into Packages/"
                + "com.vrcfury.temp during the build.");
            var mergeAnimatorType = Type.GetType(MergeAnimatorTypeName);
            if (mergeAnimatorType == null)
            {
                Assert.Ignore(
                    "The " + ModularAvatarPackageId + " package "
                    + ModularAvatarLockedVersion + " is not installed in"
                    + " this project. Install it to run this fixture.");
            }
            RequireLilToonEnvironment(
                out var transparentShader, out var cutoutShader);
            AssetDatabase.CreateFolder("Assets", TempFolderName);

            var root = new GameObject("AMUSE production order MA fixture");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                FixtureProofScope.AcceptDesktopAnisotropyDifferences(root);
                AttachAnimationFixture(root, "production-order-ma");
                PinProbeWriteDefaultsOff(root);

                AttachBandedRenderer(root, MaHolderName,
                    "AMUSE production order MA mesh",
                    "banded_production_ma", transparentShader,
                    cutoutShader, out var transparent, out var cutout);

                var swapClip = Track(
                    AlphaSeparationTestLifecycle.NewSwapClip(
                        MaSwapClipName, MaHolderName, 0,
                        (0f, transparent), (1f, cutout)));
                var swapController = NewMergeController(
                    "AMUSE production order MA swap controller",
                    swapClip);
                var component = root.AddComponent(mergeAnimatorType);
                mergeAnimatorType.GetField("animator")
                    .SetValue(component, swapController);

                BuildThroughSdkDispatcher(root);

                AssertBuiltTrianglesMatchAuthoring(
                    VendorProductionOrderCapture.Context, root);
                AssertCommittedDescriptorFxCarriesMotion(
                    root, MaSwapClipName);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The tier A fixture of the Modular Avatar object toggle. The
        /// standard fixture avatar carries one reflected
        /// ModularAvatarObjectToggle component on a probe object. The
        /// toggle holds the on state for the fixture renderer holder, so
        /// the vendor reactive object system builds a real toggle layer
        /// into the FX controller. The build runs through the installed
        /// SDK callback dispatcher, so the whole production order
        /// executes. The oracle holds on the built avatar and the
        /// committed FX graph carries the holder active state animation,
        /// so a silent vendor skip cannot turn green.
        /// </summary>
        [Test]
        [Category(VendorVrChatTierGate.TierACategory)]
        public void ModularAvatarObjectToggleKeepsTrianglesSoundVendorVRChat()
        {
            VendorVrChatTierGate.RequireInstalledVrcSdkPackages();
            // The same opt-in gate as the other tier A fixtures: the
            // dispatch runs VRCFury, and VRCFury writes project level
            // state during the build. No new dispatch gate is added.
            VendorOptInGate.Require(
                VendorOptInGate.ProductionOrderDispatch,
                "Set " + VendorOptInGate.ProductionOrderDispatch + "=1"
                + " in a disposable project to run this fixture. The"
                + " SDK callback dispatcher runs VRCFury, and VRCFury"
                + " writes generated controllers into Packages/"
                + "com.vrcfury.temp during the build.");
            var toggleType = Type.GetType(MaObjectToggleTypeName);
            if (toggleType == null)
            {
                Assert.Ignore(
                    "The " + ModularAvatarPackageId + " package "
                    + ModularAvatarLockedVersion + " is not installed in"
                    + " this project. Install it to run this fixture.");
            }
            RequireLilToonEnvironment(
                out var transparentShader, out var cutoutShader);
            AssetDatabase.CreateFolder("Assets", TempFolderName);

            var root = new GameObject("AMUSE production order MA toggle");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                FixtureProofScope.AcceptDesktopAnisotropyDifferences(root);
                AttachAnimationFixture(root, "production-order-ma-toggle");
                PinProbeWriteDefaultsOff(root);

                var renderer = AttachBandedRenderer(root, MaToggleHolderName,
                    "AMUSE production order MA toggle mesh",
                    "banded_production_ma", transparentShader,
                    cutoutShader, out _, out _);

                var toggledObjectType = Type.GetType(MaToggledObjectTypeName);
                var objectReferenceType =
                    Type.GetType(MaObjectReferenceTypeName);
                var menuItemType = Type.GetType(MaMenuItemTypeName);
                Assert.That(
                    toggledObjectType, Is.Not.Null,
                    "the installed " + ModularAvatarPackageId + " must"
                    + " carry the ToggledObject model (version pin)");
                Assert.That(
                    objectReferenceType, Is.Not.Null,
                    "the installed " + ModularAvatarPackageId + " must"
                    + " carry the AvatarObjectReference model (version"
                    + " pin)");
                Assert.That(
                    menuItemType, Is.Not.Null,
                    "the installed " + ModularAvatarPackageId + " must"
                    + " carry the ModularAvatarMenuItem model (version"
                    + " pin)");
#if AMUSE_VRCSDK3_AVATARS
                AttachMaObjectToggle(
                    toggleType, toggledObjectType, objectReferenceType,
                    menuItemType, root, renderer.gameObject);
#endif

                BuildThroughSdkDispatcher(root);

                AssertBuiltTrianglesMatchAuthoring(
                    VendorProductionOrderCapture.Context, root);
                AssertCommittedDescriptorFxTogglesHolder(
                    root, MaToggleHolderName);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The tier A fixture of the VRCFury toggle. The standard
        /// fixture avatar carries one reflected VRCFury component whose
        /// content holds one reflected Toggle feature. The toggle turns
        /// the fixture renderer holder on, so the vendor builds a real
        /// toggle layer into the FX controller. The build runs through
        /// the installed SDK callback dispatcher. The oracle holds on
        /// the built avatar and the committed FX graph carries the
        /// vendor generated layer, so a silent vendor skip cannot turn
        /// green.
        /// </summary>
        [Test]
        [Category(VendorVrChatTierGate.TierACategory)]
        public void VRCFuryToggleKeepsTrianglesSoundVendorVRChat()
        {
            VendorVrChatTierGate.RequireInstalledVrcSdkPackages();
            // The opt-in gate of V15: the dispatch runs VRCFury, and
            // VRCFury writes its generated controllers under its own
            // project folder during the build.
            VendorOptInGate.Require(
                VendorOptInGate.ProductionOrderDispatch,
                "Set " + VendorOptInGate.ProductionOrderDispatch + "=1"
                + " in a disposable project to run this fixture. The"
                + " SDK callback dispatcher runs VRCFury, and VRCFury"
                + " writes generated controllers into Packages/"
                + "com.vrcfury.temp during the build.");
            var furyType = Type.GetType(VrcFuryTypeName);
            if (furyType == null)
            {
                Assert.Ignore(
                    "The " + VrcFuryPackageId + " package "
                    + VrcFuryLockedVersion + " is not installed in this"
                    + " project. Install it to run this fixture.");
            }
            var toggleType = Type.GetType(FuryToggleTypeName);
            var toggleActionType =
                Type.GetType(FuryObjectToggleActionTypeName);
            Assert.That(
                toggleType, Is.Not.Null,
                "the installed " + VrcFuryPackageId + " must carry the"
                + " Toggle feature model (version pin)");
            Assert.That(
                toggleActionType, Is.Not.Null,
                "the installed " + VrcFuryPackageId + " must carry the"
                + " ObjectToggleAction model (version pin)");
            RequireLilToonEnvironment(
                out var transparentShader, out var cutoutShader);
            AssetDatabase.CreateFolder("Assets", TempFolderName);

            var root = new GameObject("AMUSE production order fury fixture");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                FixtureProofScope.AcceptDesktopAnisotropyDifferences(root);
                AttachAnimationFixture(root, "production-order-fury");
                PinProbeWriteDefaultsOff(root);

                var renderer = AttachBandedRenderer(root, FuryHolderName,
                    "AMUSE production order fury mesh",
                    "banded_production_fury", transparentShader,
                    cutoutShader, out _, out _);

                var fury = root.AddComponent(furyType);
                AttachFuryToggleFeature(
                    furyType, toggleType, toggleActionType,
                    fury, renderer.gameObject);

                BuildThroughSdkDispatcher(root);

                AssertBuiltTrianglesMatchAuthoring(
                    VendorProductionOrderCapture.Context, root);
                AssertCommittedDescriptorFxTogglesHolder(
                    root, FuryHolderName);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Pins every probe state of the fixture animation to write
        /// defaults off. Unity creates new states with write defaults
        /// on, while VRCFury builds its own layers with write defaults
        /// off. The mixed avatar base raises the vendor modal fix
        /// dialog in the middle of a dispatched build, which blocks a
        /// runner with no way to answer it. A uniform off avatar base
        /// reads as sound, so no dialog can appear.
        /// </summary>
        private static void PinProbeWriteDefaultsOff(GameObject root)
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null) return;
            if (!(animator.runtimeAnimatorController
                    is AnimatorController controller))
            {
                return;
            }

            foreach (var layer in controller.layers)
            {
                if (layer.stateMachine == null) continue;
                foreach (var child in layer.stateMachine.states)
                {
                    child.state.writeDefaultValues = false;
                }
            }

            EditorUtility.SetDirty(controller);
        }

        [TearDown]
        public void TearDownVrcFuryBuildsOutput()
        {
            // The cleanup is run scoped. It deletes only when this
            // test dispatched a build, and only the entries the test
            // added under the vendor builds folder. Vendor content
            // that predates the test survives, and a tier B twin or a
            // skipped run deletes nothing.
            DeleteDispatchedGeneratedAssets();
            if (!_dispatchRanThisTest)
            {
                return;
            }

            _dispatchRanThisTest = false;
            if (!AssetDatabase.IsValidFolder(VrcFuryBuildsFolder))
            {
                return;
            }

            foreach (var entry in Directory.GetFileSystemEntries(
                         VrcFuryBuildsFolder))
            {
                var name = Path.GetFileName(entry);
                if (name.EndsWith(".meta", StringComparison.Ordinal)
                    || BuildsFolderSnapshot.Contains(name))
                {
                    continue;
                }

                AssetDatabase.DeleteAsset(entry);
            }
        }

        /// <summary>
        /// Deletes the vendor generated assets folder of the dispatched
        /// avatar. The vendor AssetSaver deletes and recreates that
        /// folder on every build. A folder left behind by an earlier
        /// dispatched build put the next same-named build into a stale
        /// deletion state, the vendor asset path resolved empty, and
        /// the SDK callback failure raised a blocking dialog on
        /// 2026-11-10. The deletion removes that state after every
        /// dispatched build. The folder holds only this build's
        /// generated transients, so vendor content that predates the
        /// test survives.
        /// </summary>
        private static void DeleteDispatchedGeneratedAssets()
        {
            if (!_dispatchRanThisTest
                || string.IsNullOrEmpty(_dispatchedAvatarName))
            {
                return;
            }

            var generatedFolder = NdmfGeneratedAssetsRoot + "/"
                + _dispatchedAvatarName;
            if (AssetDatabase.IsValidFolder(generatedFolder))
            {
                AssetDatabase.DeleteAsset(generatedFolder);
            }
            if (System.IO.Directory.Exists(generatedFolder))
            {
                System.IO.Directory.Delete(generatedFolder, true);
            }
            var generatedMeta = generatedFolder + ".meta";
            if (System.IO.File.Exists(generatedMeta))
            {
                System.IO.File.Delete(generatedMeta);
            }
            _dispatchedAvatarName = null;
        }

        /// <summary>
        /// The tier A texture arm. A dispatched build hands the AMUSE
        /// capture a slot material the vendor save session re-homed,
        /// and the canonical clone then samples the vendor saved copy
        /// of the fixture texture instead of the fixture instance. The
        /// arm is tier A only: the dispatch flag holds between a
        /// dispatched build and its teardown, so the tier B twins and
        /// every other suite keep the strict instance identity. The
        /// copy is accepted only when its mip count and its decoded
        /// pixels of every mip level equal the fixture texture
        /// exactly. Any difference refuses with both identities
        /// named.
        /// </summary>
        protected override bool TryAcceptVendorSavedTextureCopy(
            Texture2D sourceTexture,
            Texture2D sampledTexture,
            out string refusal)
        {
            refusal = "";
            if (!_dispatchRanThisTest)
            {
                return false;
            }

            if (sourceTexture == null || sampledTexture == null)
            {
                refusal = " a sampled texture is missing";
                return false;
            }

            if (sourceTexture.width != sampledTexture.width
                || sourceTexture.height != sampledTexture.height)
            {
                refusal = " the sampled texture dimensions differ:"
                    + " source " + sourceTexture.name + " tex="
                    + sourceTexture.GetInstanceID() + " "
                    + sourceTexture.width + "x" + sourceTexture.height
                    + ", sampled " + sampledTexture.name + " tex="
                    + sampledTexture.GetInstanceID() + " "
                    + sampledTexture.width + "x" + sampledTexture.height;
                return false;
            }

            if (sourceTexture.mipmapCount != sampledTexture.mipmapCount)
            {
                refusal = " the sampled texture mip counts differ:"
                    + " source " + sourceTexture.name + " tex="
                    + sourceTexture.GetInstanceID() + " mips="
                    + sourceTexture.mipmapCount + ", sampled "
                    + sampledTexture.name + " tex="
                    + sampledTexture.GetInstanceID() + " mips="
                    + sampledTexture.mipmapCount;
                return false;
            }

            for (var mip = 0; mip < sourceTexture.mipmapCount; mip++)
            {
                var sourcePixels = sourceTexture.GetPixels32(mip);
                var sampledPixels = sampledTexture.GetPixels32(mip);
                if (sourcePixels.Length != sampledPixels.Length)
                {
                    refusal = " the sampled texture mip " + mip
                        + " size differs: source " + sourceTexture.name
                        + " tex=" + sourceTexture.GetInstanceID()
                        + " texels=" + sourcePixels.Length
                        + ", sampled " + sampledTexture.name + " tex="
                        + sampledTexture.GetInstanceID() + " texels="
                        + sampledPixels.Length;
                    return false;
                }

                for (var index = 0; index < sourcePixels.Length; index++)
                {
                    var sourcePixel = sourcePixels[index];
                    var sampledPixel = sampledPixels[index];
                    if (sourcePixel.r != sampledPixel.r
                        || sourcePixel.g != sampledPixel.g
                        || sourcePixel.b != sampledPixel.b
                        || sourcePixel.a != sampledPixel.a)
                    {
                        refusal = " the sampled texture pixels differ"
                            + " at mip " + mip + " texel " + index
                            + ": source " + sourceTexture.name + " tex="
                            + sourceTexture.GetInstanceID() + " rgb("
                            + sourcePixel.r + "," + sourcePixel.g + ","
                            + sourcePixel.b + ") a" + sourcePixel.a
                            + ", sampled " + sampledTexture.name
                            + " tex=" + sampledTexture.GetInstanceID()
                            + " rgb(" + sampledPixel.r + ","
                            + sampledPixel.g + "," + sampledPixel.b
                            + ") a" + sampledPixel.a;
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Builds the reflected Modular Avatar object toggle probe. One
        /// probe object carries one reflected ModularAvatarMenuItem with
        /// a Toggle control and one reflected
        /// ModularAvatarObjectToggle. The menu item gives the toggle a
        /// real parameter condition, and the toggle targets the fixture
        /// renderer holder and holds the on state, so the vendor
        /// reactive object system generates a parameter driven toggle
        /// layer into the FX controller. A bare object toggle carries no
        /// controlling condition, and the vendor prunes that shape
        /// instead of animating it. The fields are set by name or by
        /// direct SDK construction per the installed model shape, so a
        /// missing field is a version-specific fixture failure, never a
        /// silent default.
        /// </summary>
        private static void AttachMaObjectToggle(
            Type toggleType,
            Type toggledObjectType,
            Type objectReferenceType,
            Type menuItemType,
            GameObject root,
            GameObject holder)
        {
            var probe = new GameObject(MaToggleProbeName);
            probe.transform.SetParent(root.transform, false);

#if AMUSE_VRCSDK3_AVATARS
            var menuItem = probe.AddComponent(menuItemType);
            menuItemType.GetField("Control").SetValue(menuItem,
                new VRC.SDK3.Avatars.ScriptableObjects
                    .VRCExpressionsMenu.Control
                    {
                        type = VRC.SDK3.Avatars.ScriptableObjects
                            .VRCExpressionsMenu.Control.ControlType.Toggle,
                        parameter = new VRC.SDK3.Avatars.ScriptableObjects
                            .VRCExpressionsMenu.Control.Parameter
                            {
                                name = MaToggleParameterName,
                            },
                        value = 1f,
                    });
#endif

            var component = probe.AddComponent(toggleType);
            // The explicit argument array binds the GameObject
            // constructor. The parameterized reflection form left the
            // reference path unset, and the vendor then silently
            // skipped the whole reaction.
            var reference = Activator.CreateInstance(
                objectReferenceType, new object[] { holder });
            var entry = Activator.CreateInstance(toggledObjectType);
            toggledObjectType.GetField("Object")
                .SetValue(entry, reference);
            toggledObjectType.GetField("Active")
                .SetValue(entry, true);

            var listType = typeof(List<>).MakeGenericType(toggledObjectType);
            var list = (IList)Activator.CreateInstance(listType);
            list.Add(entry);
            toggleType.GetProperty("Objects")
                .SetValue(component, list);
        }

        /// <summary>
        /// Builds the reflected Toggle feature on the reflected VRCFury
        /// component. The feature fields are set by name through
        /// reflection per the installed model shape. One feature sits
        /// in the SerializeReference content field of the component.
        /// The obsolete config field stays untouched.
        /// </summary>
        private static void AttachFuryToggleFeature(
            Type furyType,
            Type toggleType,
            Type toggleActionType,
            Component fury,
            GameObject holder)
        {
            var toggle = Activator.CreateInstance(
                toggleType, nonPublic: true);
            toggleType.GetField("name").SetValue(toggle, FuryToggleName);
            toggleType.GetField("defaultOn").SetValue(toggle, true);

            var stateField = toggleType.GetField("state");
            var state = Activator.CreateInstance(
                stateField.FieldType, nonPublic: true);
            var actionsField = stateField.FieldType.GetField("actions");
            var actions = (IList)actionsField.GetValue(state);
            var action = Activator.CreateInstance(
                toggleActionType, nonPublic: true);
            toggleActionType.GetField("obj").SetValue(action, holder);
            var modeField = toggleActionType.GetField("mode");
            modeField.SetValue(action, Enum.ToObject(
                modeField.FieldType, 0));
            actions.Add(action);

            stateField.SetValue(toggle, state);
            furyType.GetField("content").SetValue(fury, toggle);
        }

        /// <summary>
        /// One dispatcher call runs the real registered callbacks in
        /// the production order. The armed capture hook reads the NDMF
        /// build context between the VRCFury callback and the NDMF
        /// optimizing hook, so the soundness oracle can read the final
        /// build state after the dispatch returns.
        /// </summary>
        private static void BuildThroughSdkDispatcher(GameObject root)
        {
#if AMUSE_VRCSDK3_AVATARS
            // The residue cleanup and the tier A texture arm scope
            // themselves to this dispatch: the snapshot lists the
            // vendor folder before the build, and the flag marks the
            // test that dispatched one.
            _dispatchRanThisTest = false;
            _dispatchedAvatarName = root.name;
            BuildsFolderSnapshot.Clear();
            BuildsFolderSnapshot.UnionWith(SnapshotBuildsEntries());

            using (VendorProductionOrderCapture.Arm())
            using (PinValidNdmfTemporaryAssetRoot())
            {
                try
                {
                    var accepted = VRC.SDKBase.Editor.BuildPipeline
                        .VRCBuildPipelineCallbacks
                        .OnPreprocessAvatar(root);
                    Assert.That(
                        accepted,
                        Is.True,
                        "the SDK preprocess chain must accept the"
                        + " fixture avatar");
                }
                finally
                {
                    // The flag must hold even when the dispatch
                    // rejects or throws. The vendor callbacks may have
                    // written their builds folder before the failure,
                    // and a skipped cleanup would protect that residue
                    // in every later snapshot forever.
                    _dispatchRanThisTest = true;
                }
            }

            Assert.That(
                VendorProductionOrderCapture.Context,
                Is.Not.Null,
                "the capture hook did not observe the NDMF build"
                + " context of the dispatched build");
#else
            Assert.Ignore(
                "The VRChat SDK is not installed in this project, so"
                + " the installed callback dispatcher cannot be"
                + " exercised.");
#endif
        }

        /// <summary>
        /// Pins the NDMF temporary asset root for one dispatched build.
        /// The root is a process wide vendor static. The observed
        /// blocking dialog of 2026-11-10 came from a build whose root
        /// read as invalid, so the first vendor asset save failed
        /// inside the SDK callback and the SDK turned that failure into
        /// a blocking dialog. The pin keeps the dispatch on a valid
        /// root, and restores the previous value after the build, so a
        /// leaked invalid root cannot block the editor main thread.
        /// </summary>
        private static IDisposable PinValidNdmfTemporaryAssetRoot()
        {
            var field = typeof(nadena.dev.ndmf.AvatarProcessor)
                .GetField(
                    "TemporaryAssetRoot",
                    BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(
                field, Is.Not.Null,
                "the installed NDMF must carry the temporary asset"
                + " root (NDMF version pin)");
            var current = field.GetValue(null) as string;
            var root = string.IsNullOrEmpty(current)
                ? NdmfGeneratedAssetsRoot
                : current;
            return new nadena.dev.ndmf.OverrideTemporaryDirectoryScope(
                root);
        }

        /// <summary>
        /// The entry names under the vendor builds folder right now.
        /// Both a generated folder and its meta companion list, so the
        /// snapshot sees the same shape the teardown reads.
        /// </summary>
        private static HashSet<string> SnapshotBuildsEntries()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (!AssetDatabase.IsValidFolder(VrcFuryBuildsFolder))
            {
                return names;
            }

            foreach (var entry in Directory.GetFileSystemEntries(
                         VrcFuryBuildsFolder))
            {
                names.Add(Path.GetFileName(entry));
            }

            return names;
        }

        // --- shared fixture helpers -----------------------------------------

        /// <summary>
        /// Attaches the standard banded renderer: one real lilToon
        /// transparent material with one triangle per alpha band, and a
        /// real lilToon cutout material as the swap target. The banded
        /// shape is the merged-consumption control shape, so the opaque
        /// band conversion proves a generated material exists for the
        /// oracle.
        /// </summary>
        private SkinnedMeshRenderer AttachBandedRenderer(
            GameObject root,
            string holderName,
            string meshName,
            string textureName,
            Shader transparentShader,
            Shader cutoutShader,
            out Material transparent,
            out Material cutout)
        {
            var texture = Track(ImportBandedAlphaTexture(textureName));
            transparent = Track(NewTransparentMaterial(
                transparentShader, texture));
            cutout = Track(NewCutoutMaterial(cutoutShader, texture,
                0.25f));
            return CreateSkinnedRenderer(
                root, holderName, meshName,
                new[] { transparent },
                new[] { Band.Transparent, Band.Partial, Band.Opaque },
                new[] { 0, 0, 0 },
                firstTriangleIndex: 0);
        }

        /// <summary>
        /// One standalone in-memory controller for the Modular Avatar
        /// merge input. The controller never attaches to the avatar, so
        /// only the vendor merge can carry its motion into the
        /// committed graph.
        /// </summary>
        private static AnimatorController NewMergeController(
            string name, AnimationClip clip)
        {
            var controller = new AnimatorController { name = name };
            controller.AddLayer("L0");
            controller.layers[0].stateMachine.AddState("Swap")
                .motion = clip;
            return controller;
        }

        // --- the survival assertions -----------------------------------------

        /// <summary>
        /// The tier A survival read. The committed FX controller of the
        /// avatar descriptor must carry a state whose motion holds the
        /// named clip, so a vendor that silently skipped its work
        /// cannot turn the fixture green.
        /// </summary>
        private static void AssertCommittedDescriptorFxCarriesMotion(
            GameObject root, string motionName)
        {
            var controller = DescriptorFxController(root);
            Assert.That(
                ControllerCarriesMotionNamed(controller, motionName),
                Is.True,
                "the committed FX graph must carry the merged motion "
                + motionName
                + " so a silent vendor skip cannot turn green."
                + DescribeCommittedLayers(controller));
        }

        /// <summary>
        /// The tier A survival read of the VRCFury fixture. The vendor
        /// compiles a simple object toggle into its proxy animation
        /// system, so the committed FX graph does not carry a layer
        /// named after the feature. The vendor generated output this
        /// fixture can hold onto is the active state curve on the
        /// fixture renderer holder path. A silent vendor skip leaves
        /// no such curve, so the assertion cannot turn green.
        /// </summary>
        private static void AssertCommittedDescriptorFxTogglesHolder(
            GameObject root, string holderPath)
        {
            var controller = DescriptorFxController(root);
            Assert.That(
                ControllerCarriesActiveToggleFor(controller, holderPath),
                Is.True,
                "the committed FX graph must carry the vendor generated"
                + " holder active state animation for " + holderPath
                + " so a silent vendor skip cannot turn green."
                + DescribeCommittedLayers(controller));
        }

        private static bool ControllerCarriesActiveToggleFor(
            AnimatorController controller, string holderPath)
        {
            if (controller == null) return false;
            foreach (var layer in controller.layers)
            {
                if (MachineCarriesActiveToggleFor(
                        layer.stateMachine, holderPath))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MachineCarriesActiveToggleFor(
            AnimatorStateMachine machine, string holderPath)
        {
            if (machine == null) return false;
            foreach (var child in machine.states)
            {
                if (MotionCarriesActiveToggleFor(
                        child.state.motion, holderPath))
                {
                    return true;
                }
            }

            foreach (var child in machine.stateMachines)
            {
                if (MachineCarriesActiveToggleFor(
                        child.stateMachine, holderPath))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MotionCarriesActiveToggleFor(
            Motion motion, string holderPath)
        {
            if (motion is AnimationClip clip)
            {
                foreach (var binding in AnimationUtility
                             .GetCurveBindings(clip))
                {
                    if (binding.type == typeof(GameObject)
                        && binding.path == holderPath
                        && binding.propertyName == "m_IsActive")
                    {
                        return true;
                    }
                }

                return false;
            }

            if (motion is BlendTree tree)
            {
                foreach (var child in tree.children)
                {
                    if (MotionCarriesActiveToggleFor(
                            child.motion, holderPath))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The tier B survival read. The committed controller on the
        /// root Animator must carry a state whose motion holds the
        /// named hand injected clip, so a silent input loss cannot turn
        /// the twin green.
        /// </summary>
        private static void AssertCommittedRootGraphCarriesMotion(
            GameObject root, string motionName)
        {
            var animator = root.GetComponent<Animator>();
            Assert.That(
                animator, Is.Not.Null,
                "the tier B fixture must carry the root Animator");
            var controller = animator.runtimeAnimatorController
                as AnimatorController;
            Assert.That(
                controller, Is.Not.Null,
                "the root Animator must hold an AnimatorController"
                + " after the build");
            Assert.That(
                ControllerCarriesMotionNamed(controller, motionName),
                Is.True,
                "the committed graph must carry the hand injected"
                + " motion " + motionName
                + " so a silent input loss cannot turn green."
                + DescribeCommittedLayers(controller));
        }

#if AMUSE_VRCSDK3_AVATARS
        private static AnimatorController DescriptorFxController(
            GameObject root)
        {
            var descriptor = root.GetComponent<
                VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            Assert.That(
                descriptor, Is.Not.Null,
                "the tier A fixture must carry the avatar descriptor");
            var fxType = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor
                .AnimLayerType.FX;
            // The committed layer array shape is the commit path's own
            // choice: after a dispatched build it does not always list
            // all five entries in the authored order. The read finds
            // the FX entry by its type and names the committed shape
            // when the entry is gone.
            foreach (var layer in descriptor.baseAnimationLayers)
            {
                if (layer.type == fxType)
                {
                    return layer.animatorController as AnimatorController;
                }
            }

            var names = new StringBuilder();
            foreach (var layer in descriptor.baseAnimationLayers)
            {
                names.Append(" ").Append(layer.type);
            }

            Assert.Fail(
                "the committed descriptor carries no FX base layer"
                + " entry; committed entries:" + names);
            return null;
        }
#else
        /// <summary>
        /// The SDK-less shape of the committed descriptor read. The
        /// tier A fixtures skip before they reach it, so this arm only
        /// names the missing package, the same loud ignore the
        /// descriptor fixture helper reports.
        /// </summary>
        private static AnimatorController DescriptorFxController(
            GameObject root)
        {
            Assert.Ignore(
                "The com.vrchat.avatars package is not installed in"
                + " this project. Fixture avatars cannot carry a"
                + " VRCAvatarDescriptor, so the tier A survival read"
                + " has no committed descriptor. Install it to run"
                + " them.");
            return null;
        }
#endif

        private static bool ControllerCarriesMotionNamed(
            AnimatorController controller, string motionName)
        {
            if (controller == null) return false;
            foreach (var layer in controller.layers)
            {
                if (StateMachineCarriesMotionNamed(
                        layer.stateMachine, motionName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool StateMachineCarriesMotionNamed(
            AnimatorStateMachine machine, string motionName)
        {
            if (machine == null) return false;
            foreach (var child in machine.states)
            {
                if (child.state.motion is AnimationClip clip
                    && clip.name == motionName)
                {
                    return true;
                }
            }

            foreach (var child in machine.stateMachines)
            {
                if (StateMachineCarriesMotionNamed(
                        child.stateMachine, motionName))
                {
                    return true;
                }
            }

            return false;
        }

        private static string DescribeCommittedLayers(
            AnimatorController controller)
        {
            if (controller == null)
            {
                return " The committed controller is missing.";
            }

            var builder = new StringBuilder();
            builder.Append(" Committed layers:");
            foreach (var layer in controller.layers)
            {
                builder.Append(" [").Append(layer.name).Append("]");
                AppendMachineMotions(builder, layer.stateMachine);
            }

            return builder.ToString();
        }

        private static void AppendMachineMotions(
            StringBuilder builder, AnimatorStateMachine machine)
        {
            if (machine == null) return;
            foreach (var child in machine.states)
            {
                builder.Append(" ")
                    .Append(child.state.name)
                    .Append("=")
                    .Append(child.state.motion != null
                        ? child.state.motion.name
                        : "<null>");
            }

            foreach (var child in machine.stateMachines)
            {
                AppendMachineMotions(builder, child.stateMachine);
            }
        }
    }

    /// <summary>
    /// The arm and read seam of the tier A context capture. The fixture
    /// arms the capture around one dispatcher call and reads the
    /// context after the dispatch returns. Inert unless armed, so no
    /// other test or build observes a change.
    /// </summary>
    internal static class VendorProductionOrderCapture
    {
        private static bool _armed;

        internal static nadena.dev.ndmf.BuildContext Context
        {
            get;
            private set;
        }

        internal static bool IsArmed => _armed;

        internal static IDisposable Arm()
        {
            Context = null;
            _armed = true;
            return new Disarm();
        }

        internal static void Observe(nadena.dev.ndmf.BuildContext context)
        {
            if (_armed)
            {
                Context = context;
            }
        }

        private sealed class Disarm : IDisposable
        {
            public void Dispose()
            {
                _armed = false;
            }
        }
    }

#if AMUSE_VRCSDK3_AVATARS
    /// <summary>
    /// The test owned SDK callback that sits between the VRCFury
    /// callback and the NDMF optimizing hook. NDMF part one stores its
    /// build context on an internal holder component and part two
    /// destroys that holder when the build ends, so this hook reads the
    /// context while it still exists and hands it to the armed capture
    /// seam. The hook returns true immediately when it is not armed, so
    /// every other dispatcher call in the suite stays untouched.
    /// </summary>
    public sealed class VendorProductionOrderCaptureHook
        : VRC.SDKBase.Editor.BuildPipeline.IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => -1026;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            if (!VendorProductionOrderCapture.IsArmed) return true;

            var holderType = Type.GetType(
                "nadena.dev.ndmf.VRChat.ContextHolder,"
                + " nadena.dev.ndmf.vrchat");
            if (holderType == null) return true;
            var holder = avatarGameObject.GetComponent(holderType);
            if (holder == null) return true;
            var field = holderType.GetField(
                "context",
                BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance);
            if (field == null) return true;

            VendorProductionOrderCapture.Observe(
                field.GetValue(holder) as nadena.dev.ndmf.BuildContext);
            return true;
        }
    }
#endif
}
