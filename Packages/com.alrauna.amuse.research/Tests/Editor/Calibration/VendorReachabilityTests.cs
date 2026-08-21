using System.Linq;
using Alrauna.Amuse.Research.Census;
using Alrauna.Amuse.Research.Collection;
using Alrauna.Amuse.Research.Tests.Editor.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Research.Tests.Editor.Calibration
{
    /// <summary>
    /// Reachability, not counting. Every case here drives the production
    /// overload of RendererObservationBuilder - no semantics provider - so a
    /// pass means AMUSE reaches the outcome in a real project, not that the
    /// collector counts a substituted result correctly.
    /// CollectorSeamCountingTests makes the counting claim; conflating the two
    /// would let a census report near-total SemanticsUnknown and call it a pass.
    /// </summary>
    public sealed class VendorReachabilityTests
    {
        private CollectorTestScene _scene;

        [SetUp]
        public void SetUp() => _scene = new CollectorTestScene();

        [TearDown]
        public void TearDown() => _scene.Destroy();

        [Test]
        public void ProbeReportsBothAttestedFamilies()
        {
            var probed = CensusVendorProbe.ProbeAll();

            Assert.That(probed.Count, Is.EqualTo(2));
            Assert.That(
                probed.Select(p => p.ExpectedPackageName),
                Is.EquivalentTo(
                    new[] { "com.poiyomi.toon", "jp.lilxyzw.liltoon" }));
        }

        [Test]
        public void AnAbsentFamilyReportsAbsenceRatherThanThrowing()
        {
            foreach (var presence in CensusVendorProbe.ProbeAll())
            {
                if (!presence.IsInstalled)
                {
                    Assert.That(presence.Shader, Is.Null);
                    Assert.That(presence.InstalledPackageVersion, Is.Null);
                }
                else
                {
                    Assert.That(presence.Shader, Is.Not.Null);
                }
            }
        }

        /// <summary>
        /// Gate case 1. Attestation is exact-version: PoiyomiMaterialSemantics
        /// pins 9.3.64 and LilToonSourceAttestation pins 2.3.4, and a mismatch
        /// makes every material of that family unattested. An installed family
        /// at the wrong version is therefore a gate failure, not a census
        /// result.
        /// </summary>
        [Test]
        public void AnInstalledFamilyMatchesTheVersionAmuseAttests()
        {
            foreach (var presence in CensusVendorProbe.ProbeAll())
            {
                if (!presence.IsInstalled)
                {
                    continue;
                }

                Assert.That(
                    presence.InstalledPackageVersion,
                    Is.EqualTo(presence.ExpectedPackageVersion),
                    presence.ExpectedPackageName
                    + " is installed at a version AMUSE does not attest. "
                    + "Every material of this family will be unattested, and a "
                    + "census run would measure the mismatch rather than AMUSE.");
            }
        }

        [Test]
        public void TheSceneHelperTracksAndDestroysAnArbitraryShaderMaterial()
        {
            var material = _scene.NewMaterial(
                Shader.Find("Standard"), "CensusGateProbe");

            Assert.That(material, Is.Not.Null);
            Assert.That(material.name, Is.EqualTo("CensusGateProbe"));

            _scene.Destroy();

            // Unity's overloaded equality reports a destroyed object as null.
            Assert.That(material == null, Is.True);
        }

        /// <summary>
        /// One renderer, one submesh, one material, through the PRODUCTION
        /// overload. No semantics provider: that is the entire point of this
        /// file.
        /// </summary>
        private ObservedSubmesh ObserveSingleSubmesh(Material material)
        {
            var root = _scene.NewRoot("GateAvatar");
            var go = _scene.NewMeshRenderer(
                root, "GateMesh", _scene.NewTriangleMesh(1), material);

            var observed = RendererObservationBuilder.Build(
                go.GetComponent<MeshRenderer>(),
                "GateMesh",
                new CensusShaderFamily());

            Assert.That(
                observed.Refusal,
                Is.EqualTo(RendererRefusal.None),
                "The gate's own renderer was refused; the case cannot speak to "
                + "vendor reachability at all.");
            Assert.That(observed.Submeshes.Count, Is.EqualTo(1));
            return observed.Submeshes[0];
        }

        /// <summary>
        /// Gate case 2. A default vendor material with opaque colour alpha and
        /// no alpha texture is the simplest thing that should prove opaque. If
        /// this cannot pass, no census result is meaningful, because the
        /// success path is unreachable in the environment being measured.
        /// </summary>
        [Test]
        public void AnOpaqueVendorMaterialReachesProvenOpaque()
        {
            foreach (var presence in CensusVendorProbe.ProbeAll())
            {
                if (!presence.IsInstalled)
                {
                    continue;
                }

                var material = _scene.NewMaterial(
                    presence.Shader, "GateOpaque_" + presence.Family);
                material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
                material.SetTexture("_MainTex", null);

                var submesh = ObserveSingleSubmesh(material);

                Assert.That(
                    submesh.AlphaFailure,
                    Is.EqualTo(AlphaResolutionFailure.None),
                    presence.Family + " failed alpha resolution on a default "
                    + "opaque material.");
                Assert.That(
                    submesh.ProvenOpaqueTriangleCount,
                    Is.EqualTo(submesh.TriangleCount),
                    presence.Family + " did not prove a fully opaque material "
                    + "opaque.");
            }
        }

        /// <summary>
        /// Gate case 4. AlphaSemanticsResolver classifies a CONSTANT alpha
        /// below one as MustRemainTransparent outright - no texture is sampled
        /// and no importer is consulted - so colour alpha alone reaches the
        /// transparent path.
        /// </summary>
        [Test]
        public void AVendorMaterialWithSubUnitAlphaMustRemainTransparent()
        {
            foreach (var presence in CensusVendorProbe.ProbeAll())
            {
                if (!presence.IsInstalled)
                {
                    continue;
                }

                var material = _scene.NewMaterial(
                    presence.Shader, "GateTransparent_" + presence.Family);
                material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
                material.SetTexture("_MainTex", null);

                var submesh = ObserveSingleSubmesh(material);

                Assert.That(
                    submesh.AlphaFailure,
                    Is.EqualTo(AlphaResolutionFailure.None),
                    presence.Family + " failed alpha resolution on a constant "
                    + "sub-unit alpha.");
                Assert.That(
                    submesh.MustRemainTransparentTriangleCount,
                    Is.EqualTo(submesh.TriangleCount),
                    presence.Family + " did not preserve a half-alpha material "
                    + "as transparent.");
            }
        }

        /// <summary>
        /// Gate case 3. A runtime Texture2D is not a project asset, so
        /// AssetDatabase.GetAssetPath returns empty, no TextureImporter exists,
        /// and UnityTextureEvidence can prove nothing about it. This must
        /// surface as MissingTextureEvidence and NOT as SemanticsUnknown:
        /// "understood shader, unseen texture" and "unknown shader" imply
        /// completely different next steps for AMUSE.
        /// </summary>
        [Test]
        public void AVendorMaterialSamplingANonAssetTextureReportsMissingEvidence()
        {
            foreach (var presence in CensusVendorProbe.ProbeAll())
            {
                if (!presence.IsInstalled)
                {
                    continue;
                }

                var texture = new Texture2D(4, 4) { name = "GateRuntimeTexture" };
                try
                {
                    var material = _scene.NewMaterial(
                        presence.Shader, "GateMissing_" + presence.Family);
                    material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
                    material.SetTexture("_MainTex", texture);

                    var submesh = ObserveSingleSubmesh(material);

                    Assert.That(
                        submesh.AlphaFailure,
                        Is.EqualTo(
                            AlphaResolutionFailure.MissingTextureEvidence),
                        presence.Family + " did not distinguish an unseeable "
                        + "texture from an unknown shader.");
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        /// <summary>
        /// Gate case 5. Poiyomi rejects locked materials before any source
        /// check, and the lock is read from the material property rather than
        /// from the shader, so setting the float reproduces the rejection
        /// without generating a shader or writing anything. THE LOCKER IS NEVER
        /// RUN.
        /// <para>
        /// This characterizes existing behaviour and implements no support for
        /// locked materials. It is the evidence behind the deferred
        /// investigation in the design's section 6: the census cannot currently
        /// distinguish an unknown shader family from a supported-but-locked
        /// vendor material, and a future reader of a census must know that.
        /// </para>
        /// </summary>
        [Test]
        public void ALockedPoiyomiMaterialIsUnattestedAndReportsSemanticsUnknown()
        {
            var presence = CensusVendorProbe.Probe(CensusVendorFamily.Poiyomi);
            if (!presence.IsInstalled)
            {
                Assert.That(presence.Shader, Is.Null);
                return;
            }

            var material = _scene.NewMaterial(presence.Shader, "GateLocked");
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            material.SetTexture("_MainTex", null);
            material.SetFloat("_ShaderOptimizerEnabled", 1f);

            var submesh = ObserveSingleSubmesh(material);

            Assert.That(
                submesh.AlphaFailure,
                Is.EqualTo(AlphaResolutionFailure.SemanticsUnknown),
                "A locked Poiyomi material was expected to be unattested. If "
                + "this fails, the deferred investigation in the design's "
                + "section 6 needs revisiting, not this test.");
        }
    }
}
