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
        /// The two properties that stand between a fresh Poiyomi material and
        /// a non-forced alpha term. Both were measured in the Lab, not assumed:
        /// on an untouched material <c>_AlphaForceOpaque</c> is 1, which
        /// short-circuits InterpretAlpha to a constant 1 before anything else
        /// is read, and <c>_MainAlphaMaskMode</c> is 2 - the only one of
        /// AMUSE's 28 alpha gates that is non-zero by default.
        /// </summary>
        private Material NewUnforcedPoiyomiMaterial(Shader shader, string name)
        {
            var material = _scene.NewMaterial(shader, name);
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            return material;
        }

        /// <summary>
        /// Gate case 2, the decisive one. If the production path cannot reach
        /// ProvenOpaque, no census result means anything, because the success
        /// path is unreachable in the environment being measured.
        /// <para>
        /// Observed in the Lab: an untouched Poiyomi material proves opaque
        /// because _AlphaForceOpaque defaults to 1. That is not a degenerate
        /// pass - a forced-opaque material genuinely is opaque - but it does
        /// mean the gate would pass without the alpha equation ever running,
        /// so the second case drives the real equation with the force flag off.
        /// </para>
        /// </summary>
        [Test]
        public void PoiyomiReachesProvenOpaqueThroughTheProductionPath()
        {
            var presence = CensusVendorProbe.Probe(CensusVendorFamily.Poiyomi);
            if (!presence.IsInstalled)
            {
                Assert.That(presence.Shader, Is.Null);
                return;
            }

            var forced = _scene.NewMaterial(presence.Shader, "GateForcedOpaque");
            var forcedSubmesh = ObserveSingleSubmesh(forced);

            Assert.That(
                forcedSubmesh.AlphaFailure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(
                forcedSubmesh.ProvenOpaqueTriangleCount,
                Is.EqualTo(forcedSubmesh.TriangleCount),
                "A forced-opaque Poiyomi material did not prove opaque.");

            var computed = NewUnforcedPoiyomiMaterial(
                presence.Shader, "GateComputedOpaque");
            computed.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            var computedSubmesh = ObserveSingleSubmesh(computed);

            Assert.That(
                computedSubmesh.AlphaFailure,
                Is.EqualTo(AlphaResolutionFailure.None),
                "The Poiyomi alpha equation refused a fully opaque material "
                + "with every alpha gate off.");
            Assert.That(
                computedSubmesh.ProvenOpaqueTriangleCount,
                Is.EqualTo(computedSubmesh.TriangleCount),
                "Poiyomi did not prove a unit-alpha material opaque.");
        }

        /// <summary>
        /// Gate case 4. AlphaSemanticsResolver classifies a constant alpha
        /// below one as MustRemainTransparent outright, with no texture
        /// sampled and no importer consulted.
        /// <para>
        /// Colour alpha alone is not enough to get there: it is only consulted
        /// once _AlphaForceOpaque is off, which is why an avatar's opaque-mode
        /// materials prove opaque regardless of what their colour alpha says.
        /// </para>
        /// </summary>
        [Test]
        public void PoiyomiReachesMustRemainTransparentThroughTheProductionPath()
        {
            var presence = CensusVendorProbe.Probe(CensusVendorFamily.Poiyomi);
            if (!presence.IsInstalled)
            {
                Assert.That(presence.Shader, Is.Null);
                return;
            }

            var material = NewUnforcedPoiyomiMaterial(
                presence.Shader, "GateTransparent");
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));

            var submesh = ObserveSingleSubmesh(material);

            Assert.That(
                submesh.AlphaFailure,
                Is.EqualTo(AlphaResolutionFailure.None),
                "Poiyomi refused a constant sub-unit alpha.");
            Assert.That(
                submesh.MustRemainTransparentTriangleCount,
                Is.EqualTo(submesh.TriangleCount),
                "Poiyomi did not preserve a half-alpha material as "
                + "transparent.");
            Assert.That(
                submesh.Disposition,
                Is.EqualTo(SeparationDisposition.Unchanged));
        }

        /// <summary>
        /// Gate case 3, recorded as a CHARACTERIZATION of a negative result.
        /// <para>
        /// MissingTextureEvidence was predicted here and is NOT what happens.
        /// A runtime Texture2D is not a project asset, so it has no
        /// TextureImporter, and Poiyomi's own InterpretAlpha requires that
        /// import evidence to build a texture sample at all. It therefore
        /// returns Unknown at the SEMANTICS layer, and the census records
        /// SemanticsUnknown - the resolver never gets to report
        /// MissingTextureEvidence.
        /// </para>
        /// <para>
        /// Consequence, and it is a real one: production reachability of
        /// MissingTextureEvidence is UNPROVEN. CollectorSeamCountingTests
        /// proves the census counts it correctly when it occurs, but nothing
        /// yet proves AMUSE produces it from a real material. Reaching it
        /// plausibly needs a texture that IS a project asset - so the adapter
        /// can read its filter, wrap, and importer - whose alpha field the
        /// resolver still cannot supply. Recorded as a gap, not coded around.
        /// </para>
        /// </summary>
        [Test]
        public void ANonAssetTextureIsRefusedBySemanticsBeforeTheResolverSeesIt()
        {
            var presence = CensusVendorProbe.Probe(CensusVendorFamily.Poiyomi);
            if (!presence.IsInstalled)
            {
                Assert.That(presence.Shader, Is.Null);
                return;
            }

            var texture = new Texture2D(4, 4) { name = "GateRuntimeTexture" };
            try
            {
                var material = NewUnforcedPoiyomiMaterial(
                    presence.Shader, "GateNonAssetTexture");
                material.SetFloat("_MainIgnoreTexAlpha", 0f);
                material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
                material.SetTexture("_MainTex", texture);

                var submesh = ObserveSingleSubmesh(material);

                Assert.That(
                    submesh.AlphaFailure,
                    Is.EqualTo(AlphaResolutionFailure.SemanticsUnknown),
                    "Observed behaviour changed. If this now reports "
                    + "MissingTextureEvidence, production reachability of that "
                    + "failure is newly proven and the gap noted above closes.");

                // The shader is still attested; only the alpha term is unknown.
                // That distinction is what lets a census tell this apart from
                // an unrecognized shader, and it is asserted so it stays true.
                Assert.That(
                    submesh.ShaderFamilyAttestation,
                    Is.EqualTo(ShaderFamilyAttestation.Poiyomi));
            }
            finally
            {
                Object.DestroyImmediate(texture);
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
        /// investigation in the design's section 6, and the attestation
        /// assertion is the load-bearing half: a locked material lands in the
        /// SAME unattested bucket as a shader AMUSE has never heard of, which
        /// is precisely why an unattested share is an upper bound on
        /// unsupported shader families rather than a measurement of them.
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
            material.SetFloat("_ShaderOptimizerEnabled", 1f);

            var submesh = ObserveSingleSubmesh(material);

            Assert.That(
                submesh.AlphaFailure,
                Is.EqualTo(AlphaResolutionFailure.SemanticsUnknown),
                "A locked Poiyomi material was expected to be unattested.");
            Assert.That(
                submesh.ShaderFamilyAttestation,
                Is.EqualTo(ShaderFamilyAttestation.None),
                "A locked Poiyomi material must be indistinguishable from an "
                + "unknown family in the census record - that is the finding "
                + "the deferred investigation exists to preserve.");
        }

        /// <summary>
        /// Gate case 2b, a NEGATIVE reachability result and the most
        /// consequential observation of the Lab run.
        /// <para>
        /// lilToon 2.3.4 is installed, and 2.3.4 is exactly the version
        /// LilToonSourceAttestation pins - yet no lilToon material is attested
        /// in this environment, in any configuration tried. Every one reports
        /// SemanticsUnknown with no attestation. lilToon regenerates its shader
        /// assets from per-project settings and attestation digests those
        /// generated assets, so a real install can legitimately differ from the
        /// pinned digests.
        /// </para>
        /// <para>
        /// This is asserted as observed so that the day it changes, the change
        /// is noticed. It implements nothing and alters no attestation: a
        /// census run in this environment must be read as measuring zero
        /// lilToon coverage, and that is a statement about AMUSE, not about
        /// any avatar.
        /// </para>
        /// </summary>
        [Test]
        public void LilToonIsNotAttestedInThisEnvironmentDespiteMatchingItsPin()
        {
            var presence = CensusVendorProbe.Probe(CensusVendorFamily.LilToon);
            if (!presence.IsInstalled)
            {
                Assert.That(presence.Shader, Is.Null);
                return;
            }

            Assert.That(
                presence.InstalledPackageVersion,
                Is.EqualTo(presence.ExpectedPackageVersion),
                "Precondition: the installed lilToon must be the pinned "
                + "version for this observation to mean what it says.");

            var material = _scene.NewMaterial(presence.Shader, "GateLilToon");
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));

            var submesh = ObserveSingleSubmesh(material);

            Assert.That(
                submesh.ShaderFamilyAttestation,
                Is.EqualTo(ShaderFamilyAttestation.None),
                "lilToon is now attested. That is good news and this test "
                + "should be replaced by a positive reachability assertion.");
            Assert.That(
                submesh.AlphaFailure,
                Is.EqualTo(AlphaResolutionFailure.SemanticsUnknown));
        }

        /// <summary>
        /// Gate case 6. In a project with no vendor package, the vendor cases
        /// above assert nothing - correct, and dangerous if unnoticed. This
        /// records which families were actually exercised, so a vacuous run is
        /// visible as vacuous rather than reading as a pass.
        /// <para>
        /// The name is deliberately a warning rather than a description: this
        /// test's row is the only place in a green CI list where the difference
        /// between "the gate passed" and "the gate proved something" is
        /// visible, and a reader scanning names must not be able to miss it.
        /// </para>
        /// <para>
        /// Assert.Ignore is deliberately not used anywhere in this file: an
        /// ignored test in the Lab, where a vendor package might genuinely have
        /// gone missing, reports a pass-shaped result for a condition that must
        /// abort a census.
        /// </para>
        /// </summary>
        [Test]
        public void AGreenRunProvesNothingUnlessThisNamesAnInstalledVendorFamily()
        {
            var installed = CensusVendorProbe.ProbeAll()
                .Where(p => p.IsInstalled)
                .Select(p => p.ExpectedPackageName + " " + p.InstalledPackageVersion)
                .ToList();

            if (installed.Count == 0)
            {
                Assert.Pass(
                    "VENDOR REACHABILITY NOT PROVEN - no attested vendor family "
                    + "is installed. This is the EXPECTED state in the public "
                    + "development project, which installs no vendor shader. "
                    + "Every vendor case in this file asserted nothing. A green "
                    + "run here says only that the gate compiles; it does not "
                    + "establish that AMUSE reaches ProvenOpaque. A census run "
                    + "in this environment must abort rather than report.");
            }

            Assert.Pass(
                "VENDOR REACHABILITY EXERCISED for: "
                + string.Join(", ", installed));
        }
    }
}
