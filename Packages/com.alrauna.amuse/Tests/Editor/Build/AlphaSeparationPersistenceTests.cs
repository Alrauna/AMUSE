using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Tests.Editor.Semantics.LilToon;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Task 3 falsifiers 13 and 16 plus the deterministic
    /// <c>SaveAsset</c> structural guard.
    /// <para>
    /// Falsifier 16 proves exactly that assigned generated objects become
    /// persistent through <c>BuildContext.Serialize()</c> and that
    /// serialization traverses object references that only a rewritten curve
    /// carries. It does <b>not</b> prove production never called
    /// <c>SaveAsset</c>: an eager <c>SaveAsset</c> implementation could also
    /// leave these objects persistent. The only assertion establishing the
    /// no-eager-save invariant is the structural guard, which audits the
    /// alpha-separation production sources for the <c>SaveAsset</c> token.
    /// </para>
    /// </summary>
    public sealed class AlphaSeparationPersistenceTests
    {
        /// <summary>
        /// The one test-owned directory NDMF's persistence scope is pointed
        /// at. A null scope disables saving entirely, so a real directory is
        /// required for the persistence proof; it is deleted unconditionally
        /// in <c>finally</c>, including when an assertion fails.
        /// </summary>
        internal const string PersistenceTempFolder =
            "Assets/AmuseTests_AlphaPersistence";

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

        // --- Falsifier 16: assigned generated objects persist ----------------

        /// <summary>
        /// Drives the real three-pass alpha-separation lifecycle with NDMF's
        /// persistence scope pointed at a real test-owned directory. The
        /// fixture produces an assigned generated mesh, assigned generated
        /// materials, and one generated material reachable only through the
        /// rewritten object-reference curve, then asserts all of them are
        /// persistent after the build.
        /// <para>
        /// This proves that serialization traverses curve-only object
        /// references; it does not prove production avoided
        /// <c>SaveAsset</c> — the structural guard in this class is the only
        /// assertion for that invariant.
        /// </para>
        /// <para>
        /// Identity is asserted through Unity's native object identity
        /// (<c>Is.EqualTo</c> on <c>UnityEngine.Object</c>): the persistence
        /// import can hand back a second managed wrapper for the same native
        /// object, so managed reference equality across that boundary is not
        /// a valid identity test.
        /// </para>
        /// </summary>
        [Test]
        public void AssignedGeneratedObjectsPersistThroughNdmfSerialization()
        {
            using var assets =
                new OverrideTemporaryDirectoryScope(PersistenceTempFolder);
            var root = new GameObject("AMUSE persistence");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AmusePlatformFinishState state = null;
            try
            {
                AlphaSeparationSplitTests.EnsureSplitFolder();
                try
                {
                    var texture = Track(
                        AlphaSeparationSplitTests.ImportSplitAlphaTexture(
                            "persistence"));
                    var split = Track(
                        AlphaSeparationSplitTests.SplitAlphaMaterial(texture));
                    var first = Track(VerifiedOpaqueMaterial());
                    var second = Track(VerifiedOpaqueMaterial());
                    var sourceMesh = Track(
                        AlphaSeparationSplitTests
                            .CreateOpaqueAndSplitSourceMesh());
                    var renderer = AddRenderer(
                        root, "body", sourceMesh, first, split);

                    var clip = Track(new AnimationClip
                    {
                        name = "AMUSE persistence swap",
                    });
                    SetSwapCurve(
                        clip, "body", 0,
                        (0f, first), (0.25f, second), (1.5f, first));
                    Track(NewController(root, "AMUSE persistence graph", clip));

                    var context = AvatarProcessor.ProcessAvatar(
                        root,
                        AlphaSeparationApplyTests.ApplyTestPlatform.Instance);
                    state = context.GetState<AmusePlatformFinishState>();

                    // --- Fixture preconditions: the feature ran, converted
                    // every slot, and actually applied a write.
                    Assert.That(context.Successful, Is.True,
                        "fixture precondition: the build must complete");
                    Assert.That(state.AnalyzedRendererCount, Is.EqualTo(1),
                        "fixture precondition: the renderer must analyze");
                    Assert.That(state.OpaqueCandidateTriangleCount,
                        Is.EqualTo(2),
                        "fixture precondition: one wholly opaque triangle " +
                        "and one split opaque triangle must be candidates");
                    Assert.That(state.AppliedRendererCount, Is.EqualTo(1),
                        "fixture precondition: a write must be applied, or " +
                        "nothing was generated to persist");
                    Assert.That(state.Separation, Is.Not.Null);
                    Assert.That(state.Separation.CreatedClones,
                        Has.Count.EqualTo(3),
                        "fixture precondition: both swap values and the " +
                        "split material must convert");

                    var wholeSlot = state.Separation.Renderers[0]
                        .CandidateSlots.Single(slot =>
                            slot.Plan.SourceMaterialBindingIndex == 0);
                    var splitSlot = state.Separation.Renderers[0]
                        .CandidateSlots.Single(slot =>
                            slot.Plan.SourceMaterialBindingIndex == 1);
                    Assert.That(
                        wholeSlot.Plan.Disposition,
                        Is.EqualTo(SubmeshSeparationDisposition
                            .WhollyOpaqueCandidate),
                        "fixture precondition: slot 0 must be wholly " +
                        "opaque, or no curve rewrite exists");
                    Assert.That(
                        splitSlot.Plan.Disposition,
                        Is.EqualTo(SubmeshSeparationDisposition.Split),
                        "fixture precondition: slot 1 must be a Split, or " +
                        "no mesh clone is assigned");
                    Assert.That(wholeSlot.OpaqueOfAdmitted,
                        Has.Count.EqualTo(2),
                        "fixture precondition: both swap values must map");

                    var assignedClone = wholeSlot.OpaqueOfAdmitted[first];
                    var curveOnlyClone = wholeSlot.OpaqueOfAdmitted[second];
                    var appendedClone = splitSlot.OpaqueOfAdmitted[split];
                    var meshClone = renderer.sharedMesh;

                    // --- Assigned generated mesh and materials. Identity
                    // here is Unity's native object identity
                    // (Is.EqualTo on UnityEngine.Object), not managed
                    // reference equality: after the persistence import the
                    // same native object can come back through a second
                    // managed wrapper, so ReferenceEquals across that
                    // boundary is not a valid identity test.
                    Assert.That(meshClone,
                        Is.EqualTo(state.Separation.Renderers[0].MeshClone),
                        "fixture precondition: the renderer must carry the " +
                        "finalized mesh clone");
                    Assert.That(meshClone, Is.Not.EqualTo(sourceMesh),
                        "fixture precondition: the assigned mesh must be " +
                        "generated, not the source mesh");
                    Assert.That(meshClone.subMeshCount, Is.EqualTo(3));
                    Assert.That(renderer.sharedMaterials,
                        Has.Length.EqualTo(3));
                    Assert.That(renderer.sharedMaterials[0],
                        Is.EqualTo(assignedClone));
                    Assert.That(renderer.sharedMaterials[1],
                        Is.EqualTo(split));
                    Assert.That(renderer.sharedMaterials[2],
                        Is.EqualTo(appendedClone));

                    // --- The curve-only proof comes first: if the renderer
                    // referenced the clone, persistence would prove nothing
                    // about curve traversal.
                    var committedClip = CommittedClipCarrying(
                        root, "body", "m_Materials.Array.data[0]");
                    Assert.That(committedClip, Is.Not.Null,
                        "fixture precondition: the committed clip must " +
                        "carry the rewritten curve");
                    var committedCurve = AnimationUtility
                        .GetObjectReferenceCurve(
                            committedClip,
                            EditorCurveBinding.PPtrCurve(
                                "body",
                                typeof(SkinnedMeshRenderer),
                                "m_Materials.Array.data[0]"));
                    Assert.That(committedCurve, Has.Length.EqualTo(3));
                    Assert.That(committedCurve[0].value,
                        Is.EqualTo(assignedClone));
                    Assert.That(committedCurve[1].value,
                        Is.EqualTo(curveOnlyClone),
                        "fixture precondition: the rewritten curve must " +
                        "carry the second swap value's opaque result");
                    Assert.That(committedCurve[2].value,
                        Is.EqualTo(assignedClone));
                    Assert.That(curveOnlyClone,
                        Is.Not.EqualTo(assignedClone));
                    Assert.That(curveOnlyClone, Is.Not.EqualTo(appendedClone));
                    CollectionAssert.DoesNotContain(
                        renderer.sharedMaterials,
                        curveOnlyClone,
                        "the clone must be reachable only through the " +
                        "rewritten curve, or the persistence proof below " +
                        "is vacuous");

                    foreach (var generated in new UnityEngine.Object[]
                             {
                                 meshClone,
                                 assignedClone,
                                 curveOnlyClone,
                                 appendedClone,
                             })
                    {
                        Assert.That(EditorUtility.IsPersistent(generated),
                            Is.True,
                            generated.name + " must be persistent after " +
                            "the build");
                        Assert.That(AssetDatabase.Contains(generated),
                            Is.True,
                            generated.name + " must live in the asset " +
                            "database after the build");
                        var path = AssetDatabase.GetAssetPath(generated)
                            .Replace('\\', '/');
                        Assert.That(path,
                            Does.StartWith(PersistenceTempFolder + "/"),
                            generated.name + " must be saved inside the " +
                            "test-owned persistence directory, not " + path);
                        Assert.That(path, Does.EndWith(".asset"),
                            generated.name + " must be a serialized asset");
                    }
                }
                finally
                {
                    AlphaSeparationSplitTests.DeleteSplitFolder();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(PersistenceTempFolder);
                Assert.That(
                    AssetDatabase.IsValidFolder(PersistenceTempFolder),
                    Is.False,
                    "the test-owned persistence directory must be deleted " +
                    "even when an assertion fails");
                DestroyGenerated(state);
                DestroyTracked();
            }
        }

        /// <summary>
        /// S5 (decision V9): every surviving generated clone must resolve
        /// back to its source through NDMF's object registry, so later
        /// passes and error reports can map AMUSE outputs. The assertion
        /// re-enters the build's own registry scope, because the static
        /// registry is only active during a build.
        /// </summary>
        [Test]
        public void GeneratedClonesResolveToTheirSourcesThroughTheObjectRegistry()
        {
            using var assets =
                new OverrideTemporaryDirectoryScope(PersistenceTempFolder);
            var root = new GameObject("AMUSE registry resolution");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AmusePlatformFinishState state = null;
            try
            {
                AlphaSeparationSplitTests.EnsureSplitFolder();
                try
                {
                    var texture = Track(
                        AlphaSeparationSplitTests.ImportSplitAlphaTexture(
                            "registry"));
                    var split = Track(
                        AlphaSeparationSplitTests.SplitAlphaMaterial(texture));
                    var first = Track(VerifiedOpaqueMaterial());
                    var second = Track(VerifiedOpaqueMaterial());
                    var sourceMesh = Track(
                        AlphaSeparationSplitTests
                            .CreateOpaqueAndSplitSourceMesh());
                    var renderer = AddRenderer(
                        root, "body", sourceMesh, first, split);

                    var clip = Track(new AnimationClip
                    {
                        name = "AMUSE registry swap",
                    });
                    SetSwapCurve(
                        clip, "body", 0,
                        (0f, first), (0.25f, second), (1.5f, first));
                    Track(NewController(root, "AMUSE registry graph", clip));

                    var context = AvatarProcessor.ProcessAvatar(
                        root,
                        AlphaSeparationApplyTests.ApplyTestPlatform.Instance);
                    state = context.GetState<AmusePlatformFinishState>();
                    Assert.That(context.Successful, Is.True,
                        "fixture precondition: the build must complete");
                    Assert.That(state.AppliedRendererCount, Is.EqualTo(1),
                        "fixture precondition: a write must be applied");

                    var wholeSlot = state.Separation.Renderers[0]
                        .CandidateSlots.Single(slot =>
                            slot.Plan.SourceMaterialBindingIndex == 0);
                    var splitSlot = state.Separation.Renderers[0]
                        .CandidateSlots.Single(slot =>
                            slot.Plan.SourceMaterialBindingIndex == 1);

                    using (new ObjectRegistryScope(
                        (IObjectRegistry)context.ObjectRegistry))
                    {
                        Assert.That(
                            ObjectRegistry.GetReference(
                                wholeSlot.OpaqueOfAdmitted[first]).Object,
                            Is.EqualTo(first),
                            "the whole-slot clone must resolve back to its " +
                            "source material");
                        Assert.That(
                            ObjectRegistry.GetReference(
                                wholeSlot.OpaqueOfAdmitted[second]).Object,
                            Is.EqualTo(second),
                            "the curve-only clone must resolve back to its " +
                            "source material");
                        Assert.That(
                            ObjectRegistry.GetReference(
                                splitSlot.OpaqueOfAdmitted[split]).Object,
                            Is.EqualTo(split),
                            "the appended split clone must resolve back to " +
                            "its source material");
                        Assert.That(
                            ObjectRegistry.GetReference(
                                renderer.sharedMesh).Object,
                            Is.EqualTo(sourceMesh),
                            "the mesh clone must resolve back to the source " +
                            "mesh");
                    }
                }
                finally
                {
                    AlphaSeparationSplitTests.DeleteSplitFolder();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(PersistenceTempFolder);
                DestroyGenerated(state);
                DestroyTracked();
            }
        }

        /// <summary>
        /// Captures structural digests of the source mesh, materials, clip
        /// and controller before a successful real feature build, then
        /// asserts they are unchanged afterward while the build avatar
        /// demonstrably carries NDMF's own copies: a committed controller
        /// and clip that are not the source objects, the finalized mesh
        /// clone, and the opaque material clones.
        /// <para>
        /// The digests are the characterized structural pattern —
        /// vertex/index layout, bounds and the named fidelity fields for the
        /// mesh, complete shader property state for the materials — not a
        /// universal object serializer. They capture only the state a
        /// plausible alpha-separation mutation could falsify.
        /// </para>
        /// </summary>
        [Test]
        public void SourceAssetsRemainUnchangedAfterSuccessfulFeatureBuild()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE source preservation");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AmusePlatformFinishState state = null;
            AnimatorController controller = null;
            var lilFixtures = new LilToonTransparentConversionFixtures();
            IReadOnlyList<UnityEngine.Object> teardownSources = null;
            try
            {
                AlphaSeparationSplitTests.EnsureSplitFolder();
                lilFixtures.BaseSetUp();
                try
                {
                    var texture = Track(
                        AlphaSeparationSplitTests.ImportSplitAlphaTexture(
                            "preservation"));
                    var split = Track(
                        AlphaSeparationSplitTests.SplitAlphaMaterial(texture));
                    var first = Track(VerifiedOpaqueMaterial());
                    var second = Track(VerifiedOpaqueMaterial());
                    var sourceMesh = Track(
                        AlphaSeparationSplitTests
                            .CreateOpaqueAndSplitSourceMesh());
                    var renderer = AddRenderer(
                        root, "body", sourceMesh, first, split);

                    // The transparent extension: a second renderer whose
                    // one slot is a transparent conversion source over a
                    // fully-opaque mip chain, so the feature converts it
                    // and the audit covers the transparent source family.
                    var transparentTexture =
                        lilFixtures.ImportFullyOpaqueMipmap(
                            "preservation_transparent");
                    var transparentMaterial = Track(
                        LilToonFixtureTestBase
                            .CreateTransparentConversionMaterial());
                    transparentMaterial.SetTexture(
                        "_MainTex", transparentTexture);
                    var transparentMesh = Track(CreateSingleTriangleMesh());
                    var transparentRenderer = AddRenderer(
                        root, "alphaTransparent", transparentMesh,
                        transparentMaterial);

                    var sourceClip = Track(new AnimationClip
                    {
                        name = "AMUSE preservation swap",
                    });
                    SetSwapCurve(
                        sourceClip, "body", 0,
                        (0f, first), (0.25f, second), (1.5f, first));
                    controller = Track(NewController(
                        root, "AMUSE preservation graph", sourceClip));

                    teardownSources = new UnityEngine.Object[]
                    {
                        transparentMaterial,
                        transparentMesh,
                        split,
                        first,
                        second,
                        sourceMesh,
                        sourceClip,
                        controller,
                    };

                    // --- Source state captured before the build. These are
                    // the original objects; the build copy does not exist
                    // yet.
                    var meshDigest = DescribeMesh(sourceMesh);
                    var splitDigest = DescribeMaterial(split);
                    var firstDigest = DescribeMaterial(first);
                    var secondDigest = DescribeMaterial(second);
                    var clipDigest = DescribeClip(sourceClip);
                    var controllerDigest = DescribeController(controller);
                    var transparentMaterialDigest =
                        DescribeMaterial(transparentMaterial);
                    var transparentMeshDigest = DescribeMesh(transparentMesh);
                    var transparentTexturePath =
                        AssetDatabase.GetAssetPath(transparentTexture);
                    var transparentTextureHash =
                        AssetDatabase.GetAssetDependencyHash(
                            transparentTexturePath);

                    var context = AvatarProcessor.ProcessAvatar(
                        root,
                        AlphaSeparationApplyTests.ApplyTestPlatform.Instance);
                    state = context.GetState<AmusePlatformFinishState>();

                    // --- Fixture precondition: a real, successful, applied
                    // feature build, so preservation is proved under the
                    // mutation path and not a no-op path.
                    Assert.That(context.Successful, Is.True,
                        "fixture precondition: the build must complete");
                    Assert.That(state.AnalyzedRendererCount, Is.EqualTo(2),
                        "fixture precondition: both renderers must " +
                        "analyze — the cutout/Poiyomi fixture renderer " +
                        "and the transparent extension renderer");
                    Assert.That(state.AppliedRendererCount, Is.EqualTo(2),
                        "fixture precondition: the feature must actually " +
                        "mutate both build renderers");
                    Assert.That(state.Separation, Is.Not.Null);
                    Assert.That(state.Separation.CreatedClones,
                        Is.Not.Empty,
                        "fixture precondition: the feature must generate " +
                        "opaque results");

                    // --- The build copy is explicitly distinct from the
                    // sources. Every digest below is therefore read from the
                    // original object, never from NDMF's committed clone.
                    var committedController = root
                        .GetComponent<Animator>().runtimeAnimatorController;
                    Assert.That(committedController, Is.Not.Null,
                        "fixture precondition: the built avatar must " +
                        "carry a committed controller");
                    Assert.That(committedController,
                        Is.Not.SameAs(controller),
                        "the built avatar must carry NDMF's committed " +
                        "controller clone, not the source controller");
                    var committedClip = CommittedClipCarrying(
                        root, "body", "m_Materials.Array.data[0]");
                    Assert.That(committedClip, Is.Not.Null,
                        "fixture precondition: the committed clip must " +
                        "carry the rewritten curve");
                    Assert.That(committedClip, Is.Not.SameAs(sourceClip),
                        "the committed clip must be the build copy, not " +
                        "the source clip");
                    Assert.That(renderer.sharedMesh,
                        Is.Not.SameAs(sourceMesh),
                        "the built renderer must carry the mesh clone, " +
                        "not the source mesh");
                    Assert.That(renderer.sharedMaterials[0],
                        Is.Not.SameAs(first),
                        "the built renderer must carry the opaque clone, " +
                        "not the source material");
                    Assert.That(renderer.sharedMaterials[1], Is.SameAs(split),
                        "the split slot keeps the source material itself, " +
                        "so its digest below is read on the exact object " +
                        "the build renderer references");

                    // --- The transparent slot converted onto its clone,
                    // so the audit below reads the exact object the build
                    // renderer references — and the slot kept the source
                    // mesh itself, because a wholly opaque slot splits
                    // nothing.
                    Assert.That(
                        state.Separation.TryGetOpaque(
                            transparentMaterial, out var transparentClone),
                        Is.True,
                        "fixture precondition: the transparent source " +
                        "must convert, or the transparent extension " +
                        "audits nothing");
                    Assert.That(
                        transparentRenderer.sharedMaterials[0],
                        Is.EqualTo(transparentClone),
                        "the built transparent renderer must carry the " +
                        "opaque clone, not the source material");
                    Assert.That(
                        transparentRenderer.sharedMesh,
                        Is.SameAs(transparentMesh),
                        "a wholly opaque slot splits nothing, so the " +
                        "renderer must keep the source mesh itself");

                    // --- The sources themselves are unchanged.
                    Assert.That(DescribeMesh(sourceMesh),
                        Is.EqualTo(meshDigest),
                        "the source mesh must be unchanged: layout, " +
                        "bounds and the characterized fidelity fields");
                    Assert.That(DescribeMaterial(split),
                        Is.EqualTo(splitDigest),
                        "the source split material must be unchanged");
                    Assert.That(DescribeMaterial(first),
                        Is.EqualTo(firstDigest),
                        "the first source swap material must be unchanged");
                    Assert.That(DescribeMaterial(second),
                        Is.EqualTo(secondDigest),
                        "the second source swap material must be unchanged");
                    Assert.That(DescribeClip(sourceClip),
                        Is.EqualTo(clipDigest),
                        "the source clip must be unchanged");
                    Assert.That(DescribeController(controller),
                        Is.EqualTo(controllerDigest),
                        "the source controller must be unchanged");

                    // --- The transparent source family is unchanged too:
                    // the material's full property state, the mesh layout,
                    // and the texture asset together with its import
                    // settings (the dependency hash covers both).
                    Assert.That(DescribeMaterial(transparentMaterial),
                        Is.EqualTo(transparentMaterialDigest),
                        "the source transparent material must be " +
                        "unchanged");
                    Assert.That(DescribeMesh(transparentMesh),
                        Is.EqualTo(transparentMeshDigest),
                        "the source transparent mesh must be unchanged");
                    Assert.That(
                        AssetDatabase.GetAssetDependencyHash(
                            transparentTexturePath),
                        Is.EqualTo(transparentTextureHash),
                        "the source texture and its import settings must " +
                        "be unchanged");
                }
                finally
                {
                    AlphaSeparationSplitTests.DeleteSplitFolder();
                    lilFixtures.BaseTearDown();
                }
            }
            finally
            {
                DestroyCommittedClone(root, controller);
                DestroyGenerated(state);

                // The avatar root leaves the scene before the audit: a
                // failing audit below must not leak a root whose
                // renderers reference objects later teardown destroys.
                UnityEngine.Object.DestroyImmediate(root);

                if (state?.Separation != null && teardownSources != null)
                {
                    // --- Teardown evidence (row 19): destroying the
                    // feature's output destroys exactly the created clones
                    // and mesh clones and nothing else — every source
                    // object is still alive right here, after
                    // DestroyGenerated ran and before the test's own
                    // tracker runs.
                    foreach (var clone in state.Separation.CreatedClones)
                    {
                        Assert.That(clone == null, Is.True,
                            "teardown must destroy every created clone");
                    }

                    foreach (var prepared in state.Separation.Renderers)
                    {
                        Assert.That(prepared.MeshClone == null, Is.True,
                            "teardown must destroy every generated mesh");
                    }

                    for (var index = 0;
                         index < teardownSources.Count;
                         index++)
                    {
                        // Unity-null semantics: a destroyed
                        // UnityEngine.Object is not .NET null, and the
                        // message must not touch a possibly-dead
                        // reference, so no .name here. The two imported
                        // textures are absent from this list on purpose:
                        // their preservation is proved by the dependency
                        // hash while the asset exists, and their deletion
                        // afterwards is the fixture's own cleanup, not
                        // the feature's teardown.
                        Assert.That(teardownSources[index] == null,
                            Is.False,
                            "teardown must not destroy source asset #" +
                            index);
                    }
                }

                DestroyTracked();
            }
        }

        // --- The deterministic SaveAsset structural guard --------------------

        private static readonly (string File, string Anchor)[]
            AuditedProductionFiles =
            {
                ("Build/AlphaSeparationRecords.cs",
                    "enum AlphaSeparationSlotRefusal"),
                ("Build/AlphaSeparationPreparation.cs",
                    "class AlphaSeparationPreparation"),
                ("Build/AlphaSeparationApply.cs",
                    "class AlphaSeparationApply"),
                ("Build/AmusePlatformFinishPlugin.cs",
                    "class AmusePlatformFinishPlugin"),
                ("Semantics/LilToon/LilToonCutoutMaterialSemantics.cs",
                    "class LilToonCutoutMaterialSemantics"),
                ("Semantics/LilToon/LilToonOpaqueConversionResult.cs",
                    "class LilToonOpaqueConversionFactors"),
                ("Semantics/LilToon/LilToonOpaqueTarget.cs",
                    "class LilToonOpaqueTarget"),
                ("Semantics/LilToon/LilToonCutoutSourceEligibility.cs",
                    "class LilToonCutoutSourceEligibility"),
                ("Semantics/LilToon/LilToonTransparentSourceEligibility.cs",
                    "class LilToonTransparentSourceEligibility"),
                ("Semantics/LilToon/LilToonTransparentMaterialSemantics.cs",
                    "class LilToonTransparentMaterialSemantics"),
            };

        /// <summary>
        /// REVERSED by decision V9 (2026-09-05): the original guard forbade
        /// the <c>SaveAsset</c> token anywhere in the alpha-separation
        /// production sources. S5 registers surviving generated assets
        /// through the build's asset saver and NDMF's object registry, so
        /// the audit now asserts the narrower contract: saves exist only in
        /// <c>AlphaSeparationApply.cs</c>, only through the passed
        /// <c>saver</c> parameter, and every save is paired with an object
        /// registry registration. Every other audited file still bans the
        /// token outright.
        /// </summary>
        [Test]
        public void AlphaSeparationProductionSavesOnlyThroughThePassedSaver()
        {
            var offences = new List<string>();
            foreach (var (file, anchor) in AuditedProductionFiles)
            {
                // Package-relative, portable: no machine-specific path may
                // enter this audit.
                var path = Path.GetFullPath(
                    "Packages/com.alrauna.amuse/Editor/" + file);
                Assert.That(File.Exists(path), Is.True,
                    "audited production source not found: " + path);
                var text = File.ReadAllText(path);
                Assert.That(
                    text.Contains(anchor), Is.True,
                    file + " no longer names " + anchor + "; the audit is " +
                    "not reading the production file it exists for");

                var isRegistrationFile = file ==
                    "Build/AlphaSeparationApply.cs";
                if (isRegistrationFile)
                {
                    Assert.That(
                        text.Contains("ObjectRegistry.RegisterReplacedObject"),
                        Is.True,
                        file + " must register generated replacements with " +
                        "NDMF's object registry");
                    continue;
                }

                if (text.Contains("SaveAsset"))
                {
                    offences.Add(file + ": SaveAsset");
                }
            }

            CollectionAssert.IsEmpty(offences);
        }

        // --- Fixture helpers -------------------------------------------------

        private static Material VerifiedOpaqueMaterial()
        {
            var material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
            material.SetFloat("_AlphaForceOpaque", 1f);
            return material;
        }

        /// <summary>
        /// A single UV'd triangle for the transparent extension renderer:
        /// the transparent proof is texture-backed, so the candidate
        /// triangle needs a UV0 domain over the fully-opaque chain.
        /// </summary>
        private static Mesh CreateSingleTriangleMesh()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                },
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.uv = new[]
            {
                new Vector2(0.25f, 0.25f),
                new Vector2(0.75f, 0.25f),
                new Vector2(0.25f, 0.75f),
            };
            return mesh;
        }

        /// <summary>
        /// Texture importer for the transparent extension. The importers
        /// are family-agnostic — schema, format and sampler vocabulary
        /// only — so this reuses the cutout fixture class under the
        /// transparent name. The base's SetUp/TearDown are driven manually
        /// by the test that owns the instance.
        /// </summary>
        private sealed class LilToonTransparentConversionFixtures
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

        private static void SetSwapCurve(
            AnimationClip clip,
            string rendererPath,
            int slotIndex,
            params (float time, Material value)[] keys)
        {
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

        private static AnimationClip CommittedClipCarrying(
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

        // --- Structural digests ----------------------------------------------

        /// <summary>
        /// The characterized mesh digest: vertex/index layout, both bounds
        /// levels, and the named fidelity fields
        /// <see cref="MeshCloneFinalizationCharacterizationTests"/> measures.
        /// A match means only that these fields agree; it is not a byte
        /// comparison.
        /// </summary>
        private static string DescribeMesh(Mesh mesh)
        {
            var parts = new List<string>
            {
                "vertexCount=" + mesh.vertexCount,
                "indexFormat=" + mesh.indexFormat,
                "subMeshCount=" + mesh.subMeshCount,
                "bounds=" + Format(mesh.bounds),
                "positions=" + Join(mesh.vertices, Format),
                "normals=" + Join(mesh.normals, Format),
                "tangents=" + Join(mesh.tangents, Format),
                "colors32=" + Join(mesh.colors32, Format),
                "uv0=" + Join(ReadUvChannel(mesh, 0), Format),
                "uv3=" + Join(ReadUvChannel(mesh, 3), Format),
                "uv7=" + Join(ReadUvChannel(mesh, 7), Format),
                "boneWeights=" + Join(mesh.boneWeights, weight =>
                    weight.boneIndex0 + ":" + F(weight.weight0) + "," +
                    weight.boneIndex1 + ":" + F(weight.weight1)),
                "bindposes=" + Join(mesh.bindposes, Format),
            };

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var descriptor = mesh.GetSubMesh(submesh);
                parts.Add(
                    "submesh" + submesh + "=" +
                    "topology=" + descriptor.topology +
                    " indexStart=" + descriptor.indexStart +
                    " indexCount=" + descriptor.indexCount +
                    " baseVertex=" + descriptor.baseVertex +
                    " firstVertex=" + descriptor.firstVertex +
                    " vertexCount=" + descriptor.vertexCount +
                    " bounds=" + Format(descriptor.bounds) +
                    " effective=[" +
                    Join(mesh.GetIndices(submesh), DescribeIndex) + "]" +
                    " stored=[" +
                    Join(mesh.GetIndices(submesh, false), DescribeIndex) +
                    "]");
            }

            parts.Add("blendShapes=" + DescribeBlendShapes(mesh));
            return string.Join("\n", parts);
        }

        private static string DescribeBlendShapes(Mesh mesh)
        {
            var described = new List<string>();
            for (var shape = 0; shape < mesh.blendShapeCount; shape++)
            {
                var frames = mesh.GetBlendShapeFrameCount(shape);
                for (var frame = 0; frame < frames; frame++)
                {
                    var deltaVertices = new Vector3[mesh.vertexCount];
                    var deltaNormals = new Vector3[mesh.vertexCount];
                    var deltaTangents = new Vector3[mesh.vertexCount];
                    mesh.GetBlendShapeFrameVertices(
                        shape, frame, deltaVertices, deltaNormals,
                        deltaTangents);

                    described.Add(
                        mesh.GetBlendShapeName(shape) + "#" + frame + "@" +
                        F(mesh.GetBlendShapeFrameWeight(shape, frame)) + ":" +
                        Join(deltaVertices, Format) + "|" +
                        Join(deltaNormals, Format) + "|" +
                        Join(deltaTangents, Format));
                }
            }

            return string.Join(";", described);
        }

        /// <summary>
        /// The material's complete relevant property state: shader identity,
        /// render queue, keywords, and every property the shader declares,
        /// whatever its type.
        /// </summary>
        private static string DescribeMaterial(Material material)
        {
            var shader = material.shader;
            var parts = new List<string>
            {
                "name=" + material.name,
                "shader=" + shader.name + "#" + shader.GetInstanceID(),
                "renderQueue=" + material.renderQueue,
                "keywords=" + string.Join(
                    ",", material.shaderKeywords
                        .OrderBy(keyword => keyword, StringComparer.Ordinal)),
            };

            var count = shader.GetPropertyCount();
            for (var index = 0; index < count; index++)
            {
                var propertyName = shader.GetPropertyName(index);
                parts.Add(
                    propertyName + ":" + shader.GetPropertyType(index) + "=" +
                    DescribePropertyValue(material, propertyName,
                        shader.GetPropertyType(index)));
            }

            return string.Join("\n", parts);
        }

        private static string DescribePropertyValue(
            Material material,
            string propertyName,
            ShaderPropertyType type)
        {
            switch (type)
            {
                case ShaderPropertyType.Color:
                    var color = material.GetColor(propertyName);
                    return "(" + F(color.r) + "," + F(color.g) + "," +
                           F(color.b) + "," + F(color.a) + ")";
                case ShaderPropertyType.Vector:
                    return Format(material.GetVector(propertyName));
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    return F(material.GetFloat(propertyName));
                case ShaderPropertyType.Int:
                    return material.GetInteger(propertyName)
                        .ToString(CultureInfo.InvariantCulture);
                case ShaderPropertyType.Texture:
                    var texture = material.GetTexture(propertyName);
                    var scale = material.GetTextureScale(propertyName);
                    var offset = material.GetTextureOffset(propertyName);
                    return (texture == null
                                ? "null"
                                : texture.name + "#" +
                                  texture.GetInstanceID()) +
                           " scale=" + Format(scale) +
                           " offset=" + Format(offset);
                default:
                    return "<unhandled " + type + ">";
            }
        }

        private static string DescribeClip(AnimationClip clip)
        {
            var parts = new List<string> { "name=" + clip.name };
            foreach (var binding in AnimationUtility
                         .GetObjectReferenceCurveBindings(clip))
            {
                parts.Add(
                    "object|" + binding.path + "|" +
                    binding.type.FullName + "|" + binding.propertyName +
                    "=" + DescribeCurve(AnimationUtility
                        .GetObjectReferenceCurve(clip, binding)));
            }

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                parts.Add(
                    "curve|" + binding.path + "|" +
                    binding.type.FullName + "|" + binding.propertyName);
            }

            return string.Join("\n", parts);
        }

        private static string DescribeCurve(ObjectReferenceKeyframe[] curve)
        {
            return curve == null
                ? "<null>"
                : string.Join("|", curve.Select(key =>
                    key.time.ToString("R", CultureInfo.InvariantCulture) +
                    "=>" + (key.value == null
                        ? "null"
                        : key.value.name + "#" +
                          key.value.GetInstanceID())));
        }

        private static string DescribeController(AnimatorController controller)
        {
            var parts = new List<string>
            {
                "name=" + controller.name,
                "layers=" + controller.layers.Length,
            };

            foreach (var layer in controller.layers)
            {
                var states = new List<string>();
                foreach (var child in layer.stateMachine.states)
                {
                    var motion = child.state.motion;
                    states.Add(
                        child.state.name + "=>" + (motion == null
                            ? "null"
                            : motion.name + "#" + motion.GetInstanceID()));
                }

                parts.Add(
                    "layer|" + layer.name + "=" + string.Join(";", states));
            }

            return string.Join("\n", parts);
        }

        private static List<Vector2> ReadUvChannel(Mesh mesh, int channel)
        {
            var uvs = new List<Vector2>();
            mesh.GetUVs(channel, uvs);
            return uvs;
        }

        private static string F(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string DescribeIndex(int index)
        {
            return index.ToString(CultureInfo.InvariantCulture);
        }

        private static string Format(Vector2 value)
        {
            return "(" + F(value.x) + "," + F(value.y) + ")";
        }

        private static string Format(Vector3 value)
        {
            return "(" + F(value.x) + "," + F(value.y) + "," + F(value.z) +
                   ")";
        }

        private static string Format(Vector4 value)
        {
            return "(" + F(value.x) + "," + F(value.y) + "," + F(value.z) +
                   "," + F(value.w) + ")";
        }

        private static string Format(Matrix4x4 value)
        {
            return Format(value.GetColumn(0)) + Format(value.GetColumn(1)) +
                   Format(value.GetColumn(2)) + Format(value.GetColumn(3));
        }

        private static string Format(Color32 value)
        {
            return "(" + value.r + "," + value.g + "," + value.b + "," +
                   value.a + ")";
        }

        private static string Format(Bounds bounds)
        {
            return Format(bounds.center) + "/" + Format(bounds.extents);
        }

        private static string Join<T>(
            IEnumerable<T> values,
            Func<T, string> format)
        {
            return string.Join(";", values.Select(format));
        }
    }
}
