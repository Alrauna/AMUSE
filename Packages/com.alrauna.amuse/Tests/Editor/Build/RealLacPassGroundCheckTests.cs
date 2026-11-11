using System;
using nadena.dev.ndmf;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Tests.Editor.Shared;

[assembly: ExportsPlugin(typeof(
    Alrauna.Amuse.Tests.Editor.Build
        .RealLacPassGroundCheckTests.RealLacPassGroundCheckPlugin))]

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The gated ground check of the real Limitex Avatar Compressor
    /// pass. The class holds three tests over one shared rig. Each
    /// test builds one fixture avatar that carries an asset-backed
    /// source texture larger than the vendor minimum source size,
    /// runs the avatar through NDMF on a dedicated test platform so
    /// the real vendor pass runs in the Optimizing phase, and asserts
    /// real-output proofs while the build objects are alive. The
    /// ground check asserts every conjunct of the copy shape by name:
    /// the name suffix, the path-less state, the registry pair, and
    /// the desktop compression format. The two output route proofs
    /// assert the unity-replacement identity mint and the color
    /// interpretation of the real copy.
    /// <para>
    /// The tests sit behind the AMUSE_REAL_LAC_PASS opt-in gate,
    /// because the real pass writes generated build assets and touches
    /// the LAC editor preferences. A vendor-absent environment skips
    /// with the fixed vendor-absent prefix of the harness. The probe
    /// pass of the test plugin captures the copy state while the build
    /// object registry is active, so the assertions read the registry
    /// pair exactly as production admission reads it.
    /// </para>
    /// </summary>
    public sealed class RealLacPassGroundCheckTests
    {
        private const string TempFolderName = "AmuseTests_RealLacPass";

        private const string SourceTextureName =
            "LacGroundCheckSource";

        private const string SourceTextureAssetPath =
            "Assets/" + TempFolderName + "/" + SourceTextureName + ".png";

        private const string ShaderName =
            "Hidden/Alrauna/AmuseTests/LacGroundCheck";

        private const string ShaderAssetPath =
            "Assets/" + TempFolderName
            + "/LacGroundCheckShader.shader";

        private const string MainTexProperty = "_MainTex";

        // One source pixel edge above the vendor minimum source size
        // of 256, so the pass always processes the fixture texture.
        private const int SourceTextureSize = 512;

        private const int DesktopTargetPlatformValue = 1;

        // The capture the probe pass fills during the build. NUnit runs
        // test fixtures of one assembly sequentially in the editor
        // domain, so one static slot is safe here.
        private static RealLacCopyCapture capture;

        [OneTimeSetUp]
        public void ResetVersionCacheOnceBeforeTheClass()
        {
            // The real pass class reads the installed producer version
            // through production admission. One reset per class keeps
            // the once-per-session read out of every test and leaves no
            // injected provider behind, per the reset rule of design
            // section 4.2.
            ReplacementTextureAttestation.Versions.ResetForTests();
        }

        [OneTimeTearDown]
        public void ResetVersionCacheOnceAfterTheClass()
        {
            ReplacementTextureAttestation.Versions.ResetForTests();
        }

        [SetUp]
        public void ClearTheCapture()
        {
            capture = null;
        }

        [Test]
        public void
            RealLacPassProducesACopyThatAttestsUnderProductionAdmission()
        {
            Texture2D sourceTexture = null;
            RunTheGatedRealPassBuild(
                out sourceTexture,
                () => AssertTheCopyShape(sourceTexture));
        }

        [Test]
        public void RealLacPassCopyMintsTheReplacementIdentityOfItsSource()
        {
            Texture2D sourceTexture = null;
            RunTheGatedRealPassBuild(out sourceTexture, () =>
            {
                Assert.That(capture, Is.Not.Null,
                    "fixture precondition: the probe pass of the test"
                    + " platform must capture the build state");
                Assert.That(
                    capture.IdentityMinted, Is.True,
                    "the real pass copy must mint a source identity"
                    + " through the production identity route");
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    sourceTexture, out var guid, out long localId);
                var expected = "unity-replacement:"
                    + guid.ToLowerInvariant() + ":" + localId.ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
                Assert.That(
                    capture.IdentityValue, Is.EqualTo(expected),
                    "the real pass copy identity must name the fixture"
                    + " source asset, not a stand-in form");
            });
        }

        [Test]
        public void RealLacPassCopyReadsColorInterpretationFromItsGraphicsFormat()
        {
            Texture2D sourceTexture = null;
            RunTheGatedRealPassBuild(out sourceTexture, () =>
            {
                Assert.That(capture, Is.Not.Null,
                    "fixture precondition: the probe pass of the test"
                    + " platform must capture the build state");
                Assert.That(
                    capture.ColorInterpretationProven, Is.True,
                    "the real pass copy color interpretation must be"
                    + " provable through the production route");
                Assert.That(
                    capture.CopyColorInterpretation,
                    Is.EqualTo(TextureColorInterpretation.Srgb),
                    "the real pass re-encodes with the source color"
                    + " space, so the sRGB fixture source must read"
                    + " sRGB on the copy");
            });
        }

        /// <summary>
        /// The shared gated rig of the three real-pass output tests. It
        /// gates on AMUSE_REAL_LAC_PASS, proves the vendor binding,
        /// builds the fixture avatar, runs the real pass through the
        /// test plugin sequence, and invokes the caller assertions
        /// while the build objects and the build object registry are
        /// still alive. The fixture folder dies in the scope guard, so
        /// an assertion that needs the fixture asset must run inside
        /// the callback.
        /// </summary>
        private static void RunTheGatedRealPassBuild(
            out Texture2D sourceTexture,
            System.Action assertsWhileBuildObjectsAreAlive)
        {
            sourceTexture = null;
            VendorOptInGate.Require(
                VendorOptInGate.RealLacPass,
                "Set " + VendorOptInGate.RealLacPass + "=1 in a"
                + " disposable project to run the real compression"
                + " pass. The pass writes generated build assets and"
                + " touches the LAC editor preferences.");

            var componentType = RealLacPass.ResolveComponentTypeOrNull();
            var passType = RealLacPass.ResolvePassTypeOrNull();
            if (componentType == null || passType == null)
            {
                Assert.Ignore(
                    RealLacPass.VendorAbsentSkipPrefix
                    + "the real pass output proofs run the real"
                    + " compression pass, so they need the installed"
                    + " component and pass types.");
            }

            var keys = RealLacPass.AssertBinding(componentType);
            RealLacPass.AssertPassBinding(passType);
            AssetDatabase.CreateFolder("Assets", TempFolderName);
            var root = default(GameObject);
            var material = default(Material);
            try
            {
                sourceTexture = ImportSourceTexture();
                var shader = ImportStandInShader();
                material = new Material(shader);
                material.SetTexture(MainTexProperty, sourceTexture);

                root = BuildFixtureAvatar(material, componentType);

                using (RealLacPass.FreezeCpuBackends(
                    keys.AnalysisBackendKey,
                    keys.ResizeBackendKey))
                {
                    var context = AvatarProcessor.ProcessAvatar(
                        root, GroundCheckPlatform.Instance);
                    capture =
                        context.GetState<RealLacCopyCapture>();
                }

                assertsWhileBuildObjectsAreAlive();
            }
            finally
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }

                AssetDatabase.DeleteAsset("Assets/" + TempFolderName);
            }
        }

        // --- Falsifier for the harness: a ground check whose build
        // never runs the vendor pass must fail the copy shape, because
        // no copy exists to attest. The RED observation of 2026-11-10
        // skipped the component attach and watched this test fail on
        // the missing copy. ---
        private static void AssertTheCopyShape(
            Texture2D sourceTexture)
        {
            Assert.That(
                capture, Is.Not.Null,
                "fixture precondition: the probe pass of the test"
                + " platform must capture the build state");
            Assert.That(
                capture.RendererFound, Is.True,
                "fixture precondition: the built avatar must carry the"
                + " fixture renderer");
            Assert.That(
                capture.TextureFoundOnMaterial, Is.True,
                "fixture precondition: the built renderer material"
                + " must carry a texture to check");

            Assert.That(
                capture.CopyProperty, Is.EqualTo(MainTexProperty),
                "the copy must ride the main texture slot the fixture"
                + " filled");
            Assert.That(
                capture.CopyName,
                Is.EqualTo(SourceTextureName + "_compressed"),
                "the copy must carry the source name plus the pinned"
                + " replacement suffix");
            Assert.That(
                capture.CopyAssetPath, Is.Empty,
                "the copy must be a live build object with no asset"
                + " path");
            Assert.That(
                capture.RegisteredSource,
                Is.EqualTo(sourceTexture),
                "the object registry must pair the copy with the"
                + " asset-backed fixture source");

            Assert.That(
                capture.ProductionAdmitted, Is.True,
                "production admission must identify the real pass"
                + " copy as an admitted replacement");
            Assert.That(
                capture.ProductionResolvedSource,
                Is.EqualTo(sourceTexture),
                "production admission must resolve the copy back to"
                + " the fixture source");
            Assert.That(
                new[]
                {
                    TextureFormat.DXT1,
                    TextureFormat.DXT5,
                    TextureFormat.BC7,
                    TextureFormat.BC5,
                },
                Does.Contain(capture.CopyFormat),
                "the copy must carry a desktop compression format on"
                + " this build target");
        }

        private static Texture2D ImportSourceTexture()
        {
            var pixelCount = SourceTextureSize * SourceTextureSize;
            var pixels = new Color32[pixelCount];
            for (var i = 0; i < pixelCount; i++)
            {
                pixels[i] = new Color32(196, 128, 74, 255);
            }

            return TestTextureImport.WritePng(
                SourceTextureAssetPath,
                SourceTextureSize,
                SourceTextureSize,
                pixels);
        }

        private static Shader ImportStandInShader()
        {
            return TestShaderWriter.WriteStandInShader(
                ShaderAssetPath,
                ShaderName,
                "\n        " + MainTexProperty
                + " (\"Texture\", 2D) = \"white\" {}");
        }

        private static GameObject BuildFixtureAvatar(
            Material material,
            Type componentType)
        {
            var root = new GameObject("LacGroundCheckAvatar");
            root.AddComponent<Animator>();

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "LacGroundCheckSurface";
            quad.transform.SetParent(root.transform, false);
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;

            // --- Real LAC pass attach. Commenting this one line skips
            // the pass call for the RED observation of the harness
            // falsifier. ---
            ConfigureLacComponent(root.AddComponent(componentType));

            return root;
        }

        private static void ConfigureLacComponent(Component component)
        {
            var serialized = new SerializedObject(component);
            var targetPlatform =
                serialized.FindProperty("TargetPlatform");
            Assert.That(
                targetPlatform, Is.Not.Null,
                "fixture precondition: TargetPlatform field (version"
                + " pin)");
            Assert.That(
                targetPlatform.enumNames[DesktopTargetPlatformValue],
                Is.EqualTo("Desktop"),
                "the member behind the Desktop ordinal of the vendor"
                + " CompressionPlatform enum must be named Desktop"
                + " (version pin)");
            targetPlatform.intValue = DesktopTargetPlatformValue;
            var detectUnused = serialized.FindProperty(
                "DetectUnusedTextures");
            Assert.That(
                detectUnused, Is.Not.Null,
                "fixture precondition: DetectUnusedTextures field"
                + " (version pin)");
            detectUnused.boolValue = false;
            var bakeLilToon = serialized.FindProperty(
                "BakeLilToonTextures");
            Assert.That(
                bakeLilToon, Is.Not.Null,
                "fixture precondition: BakeLilToonTextures field"
                + " (version pin)");
            bakeLilToon.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal const string GroundCheckPlatformName =
            "com.alrauna.amuse.tests.real-lac-pass";

        internal sealed class GroundCheckPlatform : INDMFPlatformProvider
        {
            internal static readonly GroundCheckPlatform Instance =
                new GroundCheckPlatform();

            public string QualifiedName => GroundCheckPlatformName;

            public string DisplayName =>
                "AMUSE real LAC pass ground check";
        }

        /// <summary>
        /// The test plugin of the ground check platform. The Optimizing
        /// sequence drives the resolved vendor pass instance with the
        /// animator services extension active, which mirrors the vendor
        /// plugin registration. The direct drive is required because
        /// the vendor plugin declares the VRChat platform only, so the
        /// solver stubs its pass as incompatible on this test platform
        /// (observation of 2026-11-10). The PlatformFinish sequence
        /// captures the built copy after the vendor pass ran. The
        /// production AMUSE plugin is platform filtered to the VRChat
        /// platform, so it never runs here.
        /// </summary>
        [RunsOnPlatforms(GroundCheckPlatformName)]
        public sealed class RealLacPassGroundCheckPlugin :
            Plugin<RealLacPassGroundCheckPlugin>
        {
            public override string QualifiedName =>
                "com.alrauna.amuse.tests.real-lac-pass-plugin";

            public override string DisplayName =>
                "AMUSE real LAC pass ground check";

            protected override void Configure()
            {
                var optimizing = InPhase(BuildPhase.Optimizing);
                optimizing.WithRequiredExtension(
                    typeof(nadena.dev.ndmf.animator.AnimatorServicesContext),
                    sequence => sequence.Run(
                        "AMUSE real LAC pass drive", DriveVendorPass));

                InPhase(BuildPhase.PlatformFinish).Run(
                    "AMUSE real LAC copy capture", CaptureBuiltCopy);
            }

            private static void DriveVendorPass(BuildContext context)
            {
                var passType = RealLacPass.ResolvePassTypeOrNull();
                Assert.That(
                    passType, Is.Not.Null,
                    "the real LAC pass drive needs the vendor pass"
                    + " type. The gate was set and the component"
                    + " resolved, so this is a binding break");
                RealLacPass.ExecutePass(passType, context);
            }
        }

        /// <summary>
        /// The build state record the probe pass fills while the build
        /// object registry is active. The live fields hold build
        /// objects the test reads before its teardown destroys them.
        /// </summary>
        internal sealed class RealLacCopyCapture
        {
            internal bool RendererFound;

            internal bool TextureFoundOnMaterial;

            internal string CopyProperty;

            internal string CopyName;

            internal string CopyAssetPath;

            internal TextureFormat CopyFormat;

            internal UnityEngine.Object RegisteredSource;

            internal bool ProductionAdmitted;

            internal UnityEngine.Object ProductionResolvedSource;

            internal bool IdentityMinted;

            internal string IdentityValue;

            internal bool ColorInterpretationProven;

            internal TextureColorInterpretation CopyColorInterpretation;
        }

        private static void CaptureBuiltCopy(BuildContext context)
        {
            var state = context.GetState<RealLacCopyCapture>();
            var renderer = context.AvatarRootObject
                .GetComponentInChildren<MeshRenderer>(true);
            if (renderer == null)
            {
                return;
            }

            state.RendererFound = true;
            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0
                || materials[0] == null)
            {
                return;
            }

            var material = materials[0];
            foreach (var propertyName in material.GetTexturePropertyNames())
            {
                if (!(material.GetTexture(propertyName) is Texture2D copy))
                {
                    continue;
                }

                state.TextureFoundOnMaterial = true;
                state.CopyProperty = propertyName;
                state.CopyName = copy.name;
                state.CopyAssetPath =
                    AssetDatabase.GetAssetPath(copy);
                state.CopyFormat = copy.format;
                state.RegisteredSource =
                    RegisteredSourceIdentity.Resolve(copy);
                state.ProductionAdmitted = ReplacementTextureAttestation
                    .TryIdentifyReplacement(
                        copy, out var resolvedSource);
                state.ProductionResolvedSource = resolvedSource;
                state.IdentityMinted = UnityTextureEvidence.TryGetSourceId(
                    copy, out var minted);
                state.IdentityValue = minted.Value;
                state.ColorInterpretationProven =
                    UnityTextureEvidence.TryGetColorInterpretation(
                        copy, out var interpretation);
                state.CopyColorInterpretation = interpretation;
                return;
            }
        }
    }
}
