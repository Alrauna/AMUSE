using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
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

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The barrier's alpha-separation preparation: conversion-relevance
    /// resolution, per-slot conversion admission, the single shader-family
    /// branch, the opaque mappings and both clone kinds.
    /// <para>
    /// Preparation mutates nothing but AMUSE-owned transient objects; the
    /// build avatar is written only by the later apply pass. Every falsifier
    /// asserts its closure and alpha-analysis preconditions so it cannot pass
    /// through an earlier refusal.
    /// </para>
    /// </summary>
    public sealed class AlphaSeparationPreparationTests
    {
        [Test]
        public void CandidateRendererProducesARetainedRecord()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE prepared candidate");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material material = null;
            Mesh mesh = null;
            AmusePlatformFinishState amuse = null;

            try
            {
                material = VerifiedOpaqueMaterial();
                var renderer = AddSingleTriangleRenderer(root, material, out mesh);

                amuse = RunBarrier(root);

                Assert.That(
                    amuse.SemanticallyRefusedRendererCount, Is.Zero,
                    "fixture precondition: the renderer must be analyzable");
                Assert.That(
                    amuse.OpaqueCandidateTriangleCount, Is.EqualTo(1),
                    "fixture precondition: the renderer must produce one opaque " +
                    "candidate triangle, or there is no candidate slot to retain");

                Assert.That(amuse.Separation, Is.Not.Null);
                Assert.That(amuse.Separation.Renderers, Has.Count.EqualTo(1));

                var prepared = amuse.Separation.Renderers[0];
                Assert.That(prepared.Target.Renderer, Is.SameAs(renderer));
                Assert.That(prepared.Target.ExpectedMesh, Is.SameAs(mesh));
                Assert.That(
                    prepared.Target.ExpectedMaterialSlotCount, Is.EqualTo(1));
                Assert.That(prepared.RendererPath, Is.Empty);
                Assert.That(prepared.Plan.OpaqueTriangleCount, Is.EqualTo(1));
                Assert.That(prepared.Evidence.IsClosed, Is.True);

                Assert.That(prepared.CandidateSlots, Has.Count.EqualTo(1));
                var slot = prepared.CandidateSlots[0];
                Assert.That(
                    slot.Plan.Disposition,
                    Is.EqualTo(SubmeshSeparationDisposition.WhollyOpaqueCandidate));
                Assert.That(slot.Plan.SourceMaterialBindingIndex, Is.Zero);
                Assert.That(slot.Plan.OpaqueTriangleOrdinals,
                    Is.EqualTo(new[] { 0 }));
                Assert.That(slot.Plan.TransparentTriangleOrdinals, Is.Empty);
            }
            finally
            {
                DestroyGenerated(amuse);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            }
        }

        /// <summary>
        /// Preparation creates transient clones and mappings but must never
        /// touch the build avatar: the renderer keeps its exact material
        /// array and its source mesh until the apply pass.
        /// </summary>
        [Test]
        public void PreparationDoesNotMutateTheBuildAvatar()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE prepared without mutation");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material material = null;
            Mesh mesh = null;
            AmusePlatformFinishState amuse = null;

            try
            {
                material = VerifiedOpaqueMaterial();
                var renderer = AddSingleTriangleRenderer(root, material, out mesh);
                var originalMaterials = renderer.sharedMaterials;

                amuse = RunBarrier(root);

                Assert.That(amuse.Separation, Is.Not.Null,
                    "fixture precondition: nothing was prepared, so the " +
                    "non-mutation would hold vacuously");
                Assert.That(
                    amuse.Separation.CreatedClones, Is.Not.Empty,
                    "fixture precondition: the material must convert, or " +
                    "the transient boundary proves nothing");

                Assert.That(
                    renderer.sharedMaterials, Is.EqualTo(originalMaterials),
                    "the barrier must not mutate the build avatar");
                Assert.That(renderer.sharedMesh, Is.SameAs(mesh));
                Assert.That(mesh.subMeshCount, Is.EqualTo(1));
                Assert.That(
                    amuse.SlotRefusalCount(
                        AlphaSeparationSlotRefusal.OpaqueConversionRefused),
                    Is.Zero);
            }
            finally
            {
                DestroyGenerated(amuse);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void RendererWithoutAnOpaqueCandidateRetainsNothing()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE no candidate");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material material = null;
            Mesh mesh = null;

            try
            {
                material = VerifiedTransparentMaterial();
                AddSingleTriangleRenderer(root, material, out mesh);

                var amuse = RunBarrier(root);

                Assert.That(
                    amuse.SemanticallyRefusedRendererCount, Is.Zero,
                    "fixture precondition: the renderer must be analyzed, not " +
                    "refused, or the absent record would prove nothing");
                Assert.That(
                    amuse.AnalyzedRendererCount, Is.EqualTo(1),
                    "fixture precondition: the renderer must reach analysis");
                Assert.That(
                    amuse.OpaqueCandidateTriangleCount, Is.Zero,
                    "fixture precondition: the material must prove no triangle " +
                    "opaque");

                Assert.That(
                    amuse.Separation, Is.Null,
                    "a renderer with no opaque candidate has nothing to prepare");
            }
            finally
            {
                DestroyGenerated(null);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            }
        }

        // --- Falsifier 12: AlreadyOpaque maps without a clone ----------------

        [Test]
        public void AlreadyOpaqueMapsToItselfWithoutAClone()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE already opaque");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material material = null;
            Mesh mesh = null;
            AmusePlatformFinishState amuse = null;

            try
            {
                material = CanonicalOpaqueMaterial();
                AddSingleTriangleRenderer(root, material, out mesh);

                var amuseState = RunBarrier(root);
                amuse = amuseState;

                Assert.That(amuseState.SemanticallyRefusedRendererCount,
                    Is.Zero,
                    "fixture precondition: the renderer must be analyzable");
                Assert.That(amuseState.OpaqueCandidateTriangleCount,
                    Is.EqualTo(1),
                    "fixture precondition: the canonical material must still " +
                    "prove its triangle opaque");
                Assert.That(amuseState.Separation, Is.Not.Null);

                var slot = amuseState.Separation.Renderers[0]
                    .CandidateSlots.Single();
                Assert.That(
                    amuseState.SlotRefusalCount(
                        AlphaSeparationSlotRefusal.OpaqueConversionRefused),
                    Is.Zero,
                    "the canonical material must classify AlreadyOpaque, not " +
                    "refuse");

                Assert.That(amuseState.Separation.CreatedClones, Is.Empty,
                    "an AlreadyOpaque source maps to itself and never " +
                    "enters CreatedClones");
                Assert.That(
                    amuseState.Separation.OpaqueBySource[material],
                    Is.SameAs(material),
                    "AlreadyOpaque must map the source material to itself " +
                    "by reference");
                Assert.That(
                    slot.OpaqueOfAdmitted[material], Is.SameAs(material),
                    "the slot's mapping must carry the identity");
                Assert.That(amuseState.Separation.Renderers[0].MeshClone,
                    Is.Null);
            }
            finally
            {
                DestroyGenerated(amuse);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            }
        }

        // --- Falsifier 3: no split anywhere, no mesh clone -------------------

        [Test]
        public void NoSplitAnywhereCreatesNoMeshClone()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE no split no clone");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material material = null;
            Mesh mesh = null;
            AmusePlatformFinishState amuse = null;

            try
            {
                material = VerifiedOpaqueMaterial();
                AddSingleTriangleRenderer(root, material, out mesh);

                amuse = RunBarrier(root);

                Assert.That(amuse.Separation, Is.Not.Null,
                    "fixture precondition: a candidate renderer must be " +
                    "retained, or the null clone proves nothing");
                foreach (var prepared in amuse.Separation.Renderers)
                {
                    Assert.That(prepared.MeshClone, Is.Null,
                        "a renderer whose plan requires no split must never " +
                        "clone its mesh");
                }
            }
            finally
            {
                DestroyGenerated(amuse);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            }
        }

        // --- Falsifier 11: conversion-only animation -------------------------

        [Test]
        public void
            ConversionOnlyAnimationAwayFromDefaultRefusesAndToDefaultPrepares()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);

            // Baseline: unanimated, the slot converts.
            var baselineRoot = new GameObject("AMUSE zwrite unanimated");
            baselineRoot.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material baselineMaterial = null;
            Mesh baselineMesh = null;
            AmusePlatformFinishState baseline = null;
            try
            {
                baselineMaterial = VerifiedOpaqueMaterial();
                AddSingleTriangleRenderer(
                    baselineRoot, baselineMaterial, out baselineMesh);
                baseline = RunBarrier(baselineRoot);
                Assert.That(baseline.OpaqueCandidateTriangleCount,
                    Is.EqualTo(1),
                    "fixture precondition: the unanimated build must " +
                    "produce exactly one opaque candidate triangle");
                Assert.That(baseline.Separation, Is.Not.Null);
                Assert.That(
                    baseline.SlotRefusalCount(
                        AlphaSeparationSlotRefusal.ConversionStateNotAdmitted),
                    Is.Zero);
            }
            finally
            {
                DestroyGenerated(baseline);
                if (baselineMesh != null)
                    UnityEngine.Object.DestroyImmediate(baselineMesh);
                UnityEngine.Object.DestroyImmediate(baselineRoot);
                if (baselineMaterial != null)
                    UnityEngine.Object.DestroyImmediate(baselineMaterial);
            }

            // (a) Animated away from the serialized default: conversion
            // admission refuses, but the alpha analysis is bit-for-bit
            // unaffected.
            var refusedRoot = new GameObject("AMUSE zwrite animated to 0");
            refusedRoot.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material refusedMaterial = null;
            Mesh refusedMesh = null;
            AnimationClip refusedClip = null;
            AnimatorController refusedController = null;
            AmusePlatformFinishState refused = null;
            try
            {
                refusedMaterial = VerifiedOpaqueMaterial();
                Assert.That(
                    refusedMaterial.GetFloat("_ZWrite"), Is.EqualTo(1f),
                    "fixture precondition: the material's serialized " +
                    "_ZWrite default must be 1");
                AddSingleTriangleRenderer(
                    refusedRoot, refusedMaterial, out refusedMesh);
                refusedClip = NewFloatClip(
                    "AMUSE zwrite to zero", string.Empty,
                    "material._ZWrite", 0f);
                refusedController = NewController(
                    refusedRoot, "AMUSE zwrite to zero graph", refusedClip);

                refused = RunBarrier(refusedRoot);

                Assert.That(refused.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(refused.OpaqueCandidateTriangleCount,
                    Is.EqualTo(baseline.OpaqueCandidateTriangleCount),
                    "conversion-only animation must not change the alpha " +
                    "proof's candidate accounting");
                Assert.That(refused.Separation, Is.Null,
                    "the only candidate slot was refused, so nothing is " +
                    "retained");
                Assert.That(
                    refused.SlotRefusalCount(
                        AlphaSeparationSlotRefusal.ConversionStateNotAdmitted),
                    Is.EqualTo(1),
                    "animating a conversion-read property away from the " +
                    "material's serialized default must refuse admission");
            }
            finally
            {
                DestroyGenerated(refused);
                DestroyControllerGraph(refusedRoot, refusedController);
                if (refusedMesh != null)
                    UnityEngine.Object.DestroyImmediate(refusedMesh);
                UnityEngine.Object.DestroyImmediate(refusedRoot);
                if (refusedMaterial != null)
                    UnityEngine.Object.DestroyImmediate(refusedMaterial);
                if (refusedClip != null) UnityEngine.Object.DestroyImmediate(refusedClip);
                if (refusedController != null)
                    DestroyControllerGraph(refusedController);
            }

            // (b) Animated to the serialized default: the slot prepares.
            var preparedRoot = new GameObject("AMUSE zwrite animated to 1");
            preparedRoot.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material preparedMaterial = null;
            Mesh preparedMesh = null;
            AnimationClip preparedClip = null;
            AnimatorController preparedController = null;
            AmusePlatformFinishState prepared = null;
            try
            {
                preparedMaterial = VerifiedOpaqueMaterial();
                AddSingleTriangleRenderer(
                    preparedRoot, preparedMaterial, out preparedMesh);
                preparedClip = NewFloatClip(
                    "AMUSE zwrite to one", string.Empty,
                    "material._ZWrite", 1f);
                preparedController = NewController(
                    preparedRoot, "AMUSE zwrite to one graph", preparedClip);

                prepared = RunBarrier(preparedRoot);

                Assert.That(prepared.OpaqueCandidateTriangleCount,
                    Is.EqualTo(baseline.OpaqueCandidateTriangleCount),
                    "conversion-only animation must not change the alpha " +
                    "proof's candidate accounting");
                Assert.That(prepared.Separation, Is.Not.Null,
                    "the animated value equals the serialized default, so " +
                    "the slot must prepare");
                Assert.That(prepared.Separation.CreatedClones,
                    Has.Count.EqualTo(1));
                Assert.That(
                    prepared.SlotRefusalCount(
                        AlphaSeparationSlotRefusal.ConversionStateNotAdmitted),
                    Is.Zero);
            }
            finally
            {
                DestroyGenerated(prepared);
                DestroyControllerGraph(preparedRoot, preparedController);
                if (preparedMesh != null)
                    UnityEngine.Object.DestroyImmediate(preparedMesh);
                UnityEngine.Object.DestroyImmediate(preparedRoot);
                if (preparedMaterial != null)
                    UnityEngine.Object.DestroyImmediate(preparedMaterial);
                if (preparedClip != null)
                    UnityEngine.Object.DestroyImmediate(preparedClip);
                if (preparedController != null)
                    DestroyControllerGraph(preparedController);
            }
        }

        // --- Falsifier 9: mixed Poiyomi + lilToon slot -----------------------

        [Test]
        public void MixedPoiyomiAndLilToonSlotMapsCompletely()
        {
            // Spec §10: a same-slot mixed admitted set of supported
            // families maps completely — each admitted value through its
            // own family. The pre-slice contract refused this slot
            // (OpaqueConversionUnsupportedFamily); the reviewed conversion
            // design supersedes that refusal. Falsifies: a per-family
            // routing that still refuses mixed sets, or one family's
            // mapping corrupting the other's.
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE mixed family slot");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material poiyomi = null;
            Material lilToon = null;
            Mesh mesh = null;
            AnimationClip clip = null;
            AnimatorController controller = null;

            try
            {
                poiyomi = VerifiedOpaqueMaterial();
                lilToon = LilToonFixtureTestBase.CreateVerifiedMaterial();
                AddSingleTriangleRenderer(root, poiyomi, out mesh);

                clip = NewSwapClip(
                    "AMUSE mixed swap", string.Empty, 0, (0f, lilToon));
                var controllerLocal = NewController(
                    root, "AMUSE mixed graph", clip);
                controller = controllerLocal;

                var amuse = RunBarrier(
                    root,
                    selectRequest: SelectMixedFamilyRequest,
                    resolveSemantics: ResolveMixedFamilySemantics);

                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.Zero);
                Assert.That(amuse.OpaqueCandidateTriangleCount, Is.EqualTo(1),
                    "fixture precondition: the mixed slot must resolve and " +
                    "produce an opaque candidate");
                foreach (AlphaSeparationSlotRefusal reason in Enum.GetValues(
                             typeof(AlphaSeparationSlotRefusal)))
                {
                    if (reason == AlphaSeparationSlotRefusal.None)
                    {
                        continue;
                    }

                    Assert.That(
                        amuse.SlotRefusalCount(reason), Is.Zero,
                        "a fully supported mixed admitted set must map: " +
                        reason);
                }

                Assert.That(amuse.Separation, Is.Not.Null,
                    "the mixed slot must prepare now that both families " +
                    "convert");
                Assert.That(amuse.Separation.Renderers, Has.Count.EqualTo(1));
                var candidates = amuse.Separation.Renderers[0].CandidateSlots;
                Assert.That(candidates, Has.Count.EqualTo(1));
                var mapping = candidates[0].OpaqueOfAdmitted;
                Assert.That(mapping, Has.Count.EqualTo(2),
                    "both admitted values must map");
                Assert.That(
                    mapping.ContainsKey(lilToon) && mapping[lilToon] == lilToon,
                    Is.True,
                    "the attested opaque lilToon admitted value must map " +
                    "to itself with no clone");
                Assert.That(mapping.ContainsKey(poiyomi), Is.True,
                    "the Poiyomi admitted value must map through its own " +
                    "conversion");
                Assert.That(mapping[poiyomi], Is.Not.SameAs(poiyomi),
                    "the Poiyomi value converts to a generated clone");
                Assert.That(amuse.Separation.CreatedClones,
                    Has.Count.EqualTo(1),
                    "only the Poiyomi conversion creates a clone");
            }
            finally
            {
                DestroyControllerGraph(root, controller);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (poiyomi != null) UnityEngine.Object.DestroyImmediate(poiyomi);
                if (lilToon != null) UnityEngine.Object.DestroyImmediate(lilToon);
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        // --- Falsifier 10: Poiyomi slot survives beside an opaque lilToon ---

        [Test]
        public void
            PoiyomiSlotPreparesBesideAnOpaqueLilToonSlotOnTheSameRenderer()
        {
            // Spec §10: the attested opaque lilToon frontend maps to itself,
            // so its slot prepares as a wholly-opaque candidate with an
            // identity mapping and no clone — the same outcome an attested
            // opaque Poiyomi slot already produces. The pre-slice contract
            // refused this slot; the reviewed conversion design supersedes
            // that refusal. Falsifies: sibling-slot corruption, refusal of
            // the map-to-self slot, or a clone created for it.
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE lilToon sibling");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material poiyomi = null;
            Material lilToon = null;
            Mesh mesh = null;
            AmusePlatformFinishState amuse = null;

            try
            {
                poiyomi = VerifiedOpaqueMaterial();
                lilToon = LilToonFixtureTestBase.CreateVerifiedMaterial();
                var renderer = AddTwoTriangleRenderer(
                    root, poiyomi, lilToon, out mesh);

                amuse = RunBarrier(
                    root,
                    selectRequest: SelectMixedFamilyRequest,
                    resolveSemantics: ResolveMixedFamilySemantics);

                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.Zero);
                Assert.That(amuse.AnalyzedRendererCount, Is.EqualTo(1),
                    "fixture precondition: the renderer must analyze — the " +
                    "lilToon material passes family selection and closure");
                Assert.That(amuse.OpaqueCandidateTriangleCount, Is.EqualTo(2),
                    "fixture precondition: both slots must prove their " +
                    "triangle opaque");
                foreach (AlphaSeparationSlotRefusal reason in Enum.GetValues(
                             typeof(AlphaSeparationSlotRefusal)))
                {
                    if (reason == AlphaSeparationSlotRefusal.None)
                    {
                        continue;
                    }

                    Assert.That(
                        amuse.SlotRefusalCount(reason), Is.Zero,
                        "both slots are supported families and must map: " +
                        reason);
                }

                Assert.That(amuse.Separation, Is.Not.Null);
                Assert.That(amuse.Separation.Renderers, Has.Count.EqualTo(1));
                var candidates = amuse.Separation.Renderers[0].CandidateSlots;
                Assert.That(candidates, Has.Count.EqualTo(2),
                    "both slots must survive on the same renderer");
                var poiyomiSlot = candidates.Single(
                    candidate =>
                        candidate.Plan.SourceMaterialBindingIndex == 0);
                var lilToonSlot = candidates.Single(
                    candidate =>
                        candidate.Plan.SourceMaterialBindingIndex == 1);
                Assert.That(poiyomiSlot.OpaqueOfAdmitted, Is.Not.Empty,
                    "the Poiyomi slot's mapping must be prepared");
                Assert.That(poiyomiSlot.OpaqueOfAdmitted[poiyomi],
                    Is.Not.SameAs(poiyomi),
                    "the Poiyomi slot converts to a generated clone");
                var lilToonMapping = lilToonSlot.OpaqueOfAdmitted;
                Assert.That(
                    lilToonMapping.ContainsKey(lilToon) &&
                    lilToonMapping[lilToon] == lilToon,
                    Is.True,
                    "the opaque lilToon slot must map its material to " +
                    "itself with no clone");
                Assert.That(amuse.Separation.CreatedClones,
                    Has.Count.EqualTo(1),
                    "only the Poiyomi conversion creates a clone");
            }
            finally
            {
                DestroyGenerated(amuse);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (poiyomi != null) UnityEngine.Object.DestroyImmediate(poiyomi);
                if (lilToon != null) UnityEngine.Object.DestroyImmediate(lilToon);
            }
        }


        // --- Defect A regression: avatar-wide deduplication ------------------

        [Test]
        public void SharedSourceMaterialReusesOneAvatarWideClone()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE shared source dedup");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material material = null;
            Mesh mesh = null;
            AmusePlatformFinishState amuse = null;

            try
            {
                material = VerifiedOpaqueMaterial();
                AddTwoTriangleRenderer(root, material, material, out mesh);

                amuse = RunBarrier(root);

                Assert.That(
                    amuse.SemanticallyRefusedRendererCount, Is.Zero,
                    "fixture precondition: the renderer must be analyzable");
                Assert.That(amuse.OpaqueCandidateTriangleCount,
                    Is.EqualTo(2),
                    "fixture precondition: both slots must produce opaque " +
                    "candidates, or the deduplication proves nothing");
                Assert.That(amuse.Separation, Is.Not.Null);
                var candidates = amuse.Separation.Renderers[0].CandidateSlots;
                Assert.That(candidates, Has.Count.EqualTo(2),
                    "fixture precondition: both slots must be prepared, or " +
                    "the shared mapping proves nothing");

                Assert.That(
                    amuse.Separation.CreatedClones, Has.Count.EqualTo(1),
                    "two slots proven against one source material must " +
                    "share one avatar-wide clone");
                var clone = amuse.Separation.CreatedClones[0];
                Assert.That(
                    amuse.Separation.OpaqueBySource[material],
                    Is.SameAs(clone),
                    "the avatar-wide mapping must hold the shared clone");
                Assert.That(
                    candidates[0].OpaqueOfAdmitted[material],
                    Is.SameAs(clone),
                    "the first slot's mapping must reference the shared " +
                    "clone, not a per-slot duplicate");
                Assert.That(
                    candidates[1].OpaqueOfAdmitted[material],
                    Is.SameAs(clone),
                    "the second slot's mapping must reference the shared " +
                    "clone; a locally converted duplicate the avatar map " +
                    "never registered would leak");
            }
            finally
            {
                DestroyGenerated(amuse);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            }
        }

        // --- Defect B regression: overwrite refusal precedes conversion -----

        [Test]
        public void OverwriteRuleRefusalNeverInvokesConversion()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE overwrite before conversion");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material material = null;
            Mesh mesh = null;
            AnimationClip clip = null;
            AnimatorController controller = null;
            Material rejectedClone = null;
            var conversionInvocations = 0;
            AmusePlatformFinishState amuse = null;

            try
            {
                material = VerifiedOpaqueMaterial();
                Assert.That(
                    material.GetFloat("_Cutoff"), Is.EqualTo(0.5f),
                    "fixture precondition: the material's serialized " +
                    "_Cutoff default must be 0.5, so animating it to 0.5 " +
                    "admits while violating the canonical 0");
                AddSingleTriangleRenderer(root, material, out mesh);

                // A canonical recipe property animated to its own serialized
                // default: admission succeeds, but the admitted value differs
                // from the canonical value the recipe would write.
                clip = NewFloatClip(
                    "AMUSE cutoff to default", string.Empty,
                    "material._Cutoff", 0.5f);
                controller = NewController(
                    root, "AMUSE cutoff graph", clip);

                VerifiedPoiyomiConversion conversion =
                    (Material live, CapturedMaterialEvidence derived,
                     Material preparedOpaque,
                     out Material opaque,
                     out PoiyomiOpaqueConversionRefusal refusal) =>
                    {
                        conversionInvocations++;
                        rejectedClone = new Material(live.shader);
                        opaque = rejectedClone;
                        refusal = PoiyomiOpaqueConversionRefusal.None;
                        return true;
                    };

                amuse = RunBarrier(root, poiyomiConversion: conversion);

                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(
                    amuse.SemanticallyRefusedRendererCount, Is.Zero,
                    "fixture precondition: the renderer must be analyzable");
                Assert.That(amuse.OpaqueCandidateTriangleCount,
                    Is.EqualTo(1),
                    "fixture precondition: the slot must be a candidate, " +
                    "or the overwrite refusal proves nothing");
                Assert.That(
                    amuse.SlotRefusalCount(
                        AlphaSeparationSlotRefusal
                            .ConversionPropertyOverwrittenAtRuntime),
                    Is.EqualTo(1),
                    "animating a canonical property to a non-canonical " +
                    "value must refuse the slot");
                Assert.That(
                    conversionInvocations, Is.Zero,
                    "the overwrite rule must be validated before the " +
                    "conversion step runs, so no material is created for a " +
                    "slot already known to violate it");
                Assert.That(amuse.Separation, Is.Null,
                    "the only candidate slot was refused, so nothing is " +
                    "retained");
                foreach (AlphaSeparationSlotRefusal reason in Enum.GetValues(
                             typeof(AlphaSeparationSlotRefusal)))
                {
                    if (reason == AlphaSeparationSlotRefusal.None ||
                        reason == AlphaSeparationSlotRefusal
                            .ConversionPropertyOverwrittenAtRuntime)
                    {
                        continue;
                    }

                    Assert.That(
                        amuse.SlotRefusalCount(reason), Is.Zero,
                        "no other reason may be recorded: " + reason);
                }
            }
            finally
            {
                DestroyGenerated(amuse);
                DestroyControllerGraph(root, controller);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
                if (rejectedClone != null)
                {
                    UnityEngine.Object.DestroyImmediate(rejectedClone);
                }
            }
        }

        // --- Cross-renderer contextual validation regression ----------------

        [Test]
        public void SharedSourceStillValidatesConversionPerRenderer()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE shared source per renderer");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material material = null;
            Mesh meshA = null;
            Mesh meshB = null;
            AnimationClip clip = null;
            AnimatorController controller = null;
            AmusePlatformFinishState amuse = null;

            try
            {
                // One source material shared by two renderers at distinct
                // animation paths. Renderer A animates nothing; renderer B
                // animates a canonical recipe property to its own serialized
                // default, which admits but violates the canonical value.
                material = VerifiedOpaqueMaterial();
                Assert.That(
                    material.GetFloat("_Cutoff"), Is.EqualTo(0.5f),
                    "fixture precondition: the source's serialized _Cutoff " +
                    "default must be 0.5, so renderer B's binding admits " +
                    "while violating the canonical 0");
                AddNamedChildRenderer(root, "bodyA", material, out meshA);
                AddNamedChildRenderer(root, "bodyB", material, out meshB);

                clip = NewFloatClip(
                    "AMUSE cutoff on body B", "bodyB",
                    "material._Cutoff", 0.5f);
                controller = NewController(
                    root, "AMUSE shared source graph", clip);

                amuse = RunBarrier(root);

                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(
                    amuse.SemanticallyRefusedRendererCount, Is.Zero,
                    "fixture precondition: both renderers must be " +
                    "analyzable");
                Assert.That(amuse.AnalyzedRendererCount, Is.EqualTo(2),
                    "fixture precondition: closure and alpha analysis must " +
                    "succeed for both renderers, or the contextual refusal " +
                    "proves nothing");
                Assert.That(amuse.OpaqueCandidateTriangleCount,
                    Is.EqualTo(2),
                    "fixture precondition: both renderers must produce " +
                    "opaque candidates");

                // Renderer A prepares and registers one avatar-wide clone.
                // Renderer B must still run its own conversion decision —
                // family, admission, and the runtime-overwrite rule — and is
                // refused; reusing renderer A's artifact without that
                // validation would silently prepare a renderer whose recipe
                // is provably overwritten at runtime.
                Assert.That(
                    amuse.Separation.CreatedClones, Has.Count.EqualTo(1),
                    "the shared source material must map to exactly one " +
                    "generated clone");
                Assert.That(amuse.Separation.Renderers,
                    Has.Count.EqualTo(1),
                    "only renderer A may be retained; renderer B's " +
                    "conversion validation must run even though renderer A " +
                    "already prepared the same source material");
                Assert.That(
                    amuse.Separation.Renderers[0].RendererPath,
                    Is.EqualTo("bodyA"),
                    "the retained renderer must be the one without the " +
                    "conflicting conversion animation");
                Assert.That(
                    amuse.SlotRefusalCount(
                        AlphaSeparationSlotRefusal
                            .ConversionPropertyOverwrittenAtRuntime),
                    Is.EqualTo(1),
                    "renderer B must be refused by its own overwrite " +
                    "validation, not bypassed by the avatar-wide artifact");
                foreach (AlphaSeparationSlotRefusal reason in Enum.GetValues(
                             typeof(AlphaSeparationSlotRefusal)))
                {
                    if (reason == AlphaSeparationSlotRefusal.None ||
                        reason == AlphaSeparationSlotRefusal
                            .ConversionPropertyOverwrittenAtRuntime)
                    {
                        continue;
                    }

                    Assert.That(
                        amuse.SlotRefusalCount(reason), Is.Zero,
                        "no other reason may be recorded: " + reason);
                }
            }
            finally
            {
                DestroyGenerated(amuse);
                DestroyControllerGraph(root, controller);
                if (meshA != null) UnityEngine.Object.DestroyImmediate(meshA);
                if (meshB != null) UnityEngine.Object.DestroyImmediate(meshB);
                UnityEngine.Object.DestroyImmediate(root);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        /// <summary>
        /// End-to-end cutout conversion: a convertible cutout fixture
        /// material at fresh defaults — its main texture a fully-opaque
        /// mipmap chain — driven through the barrier with the family-aware
        /// lilToon seams and the Poiyomi conversion seam default. The cutout
        /// family must convert the way Poiyomi does: the slot prepares and
        /// the prepared record carries the source-to-opaque mapping, instead
        /// of being refused as an unsupported family.
        /// </summary>
        [Test]
        public void CutoutFixtureSlotPreparesAndCarriesTheOpaqueMapping()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE cutout conversion candidate");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material material = null;
            Mesh mesh = null;
            AmusePlatformFinishState amuse = null;
            var fixtures = new LilToonCutoutConversionFixtures();

            try
            {
                fixtures.BaseSetUp();
                material = LilToonFixtureTestBase.CreateCutoutConversionMaterial();
                material.SetTexture(
                    "_MainTex",
                    fixtures.ImportFullyOpaqueMipmap("cutout_conversion"));
                var renderer =
                    AddSingleTriangleRenderer(root, material, out mesh);
                // The cutout proof is texture-backed, so the candidate
                // triangle needs a UV0 domain; the Poiyomi fixtures' constant
                // alpha never exercised one. Any in-bounds domain works over
                // a fully-opaque chain.
                mesh.uv = new[]
                {
                    new Vector2(0.25f, 0.25f),
                    new Vector2(0.75f, 0.25f),
                    new Vector2(0.25f, 0.75f),
                };

                amuse = RunBarrier(
                    root,
                    selectRequest: VerifiedLilToonTestSeams
                        .SelectVerifiedFixtureRequest,
                    capturer: VerifiedLilToonTestSeams
                        .CaptureVerifiedFixtureMaterials,
                    resolveSemantics: VerifiedLilToonTestSeams
                        .VerifiedAlphaOnly);

                Assert.That(
                    amuse.SemanticallyRefusedRendererCount, Is.Zero,
                    "fixture precondition: the renderer must be analyzable");
                Assert.That(
                    amuse.OpaqueCandidateTriangleCount, Is.EqualTo(1),
                    "fixture precondition: the cutout slot over a " +
                    "fully-opaque mip chain must prove one opaque " +
                    "candidate triangle, or there is no conversion " +
                    "candidate at all");

                Assert.That(
                    amuse.SlotRefusalCount(
                        AlphaSeparationSlotRefusal
                            .OpaqueConversionUnsupportedFamily),
                    Is.Zero,
                    "the cutout family must convert, not refuse as an " +
                    "unsupported family");
                Assert.That(
                    amuse.Separation,
                    Is.Not.Null,
                    "the cutout slot must prepare");
                Assert.That(
                    amuse.Separation.Renderers, Has.Count.EqualTo(1));
                Assert.That(
                    amuse.Separation.Renderers[0].Target.Renderer,
                    Is.SameAs(renderer));
                Assert.That(
                    amuse.Separation.TryGetOpaque(material, out var opaque),
                    Is.True,
                    "the prepared record must carry the cutout " +
                    "source-to-opaque mapping");
                Assert.That(opaque, Is.Not.Null);
            }
            finally
            {
                DestroyGenerated(amuse);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }

                fixtures.BaseTearDown();
            }
        }

        // --- Task 4: affine _MainTex_ST support (design 2026-08-31) --------

        /// <summary>
        /// Falsifier arm (a) (plan Task 4 item 1): a triangle whose raw
        /// (identity) UV samples a transparent texel is proven opaque once
        /// the material's real non-identity <c>_MainTex</c> ST is applied.
        /// Three proofs, each covering a distinct claim:
        /// <list type="bullet">
        /// <item><description><b>Classifier control.</b> The geometric
        /// transform contrast proven directly against
        /// <see cref="TriangleAlphaClassifier"/> (the classifier the
        /// resolver itself calls), isolated from capture/admission/planner
        /// plumbing.
        /// </description></item>
        /// <item><description><b>Content-decided barrier proof.</b> The
        /// same contrast driven through the real <c>RunBarrier</c> pipeline
        /// — capture, admission, the planner, the resolver, the classifier
        /// — against a real imported, single-level (non-mipmapped) RGBA32
        /// texture whose alpha genuinely varies with UV position. This is
        /// the claim the classifier control alone cannot make: an
        /// implementation that resolved the barrier path with an identity
        /// mapping instead of the captured ST — with capture and the
        /// classifier both individually correct — would still fail here,
        /// because the sampled content differs between the raw and the
        /// transformed domain. Confirmed by mutating
        /// <c>PoiyomiMaterialSemantics.TryGetSupportedUvMapping</c>'s
        /// <c>UvMapping</c> construction to identity and observing this arm
        /// fail; see the implementation report for the exact mutation.
        /// </description></item>
        /// <item><description><b>All-mip conjunction, migration proof.</b>
        /// A real imported texture cannot carry the identity-vs-transform
        /// content contrast <em>and</em> a genuine multi-level mip chain at
        /// once: Unity's generated mip chain box-filters any
        /// partial-opacity region down to the coarsest (1x1) level, whose
        /// single texel then covers the whole UV domain and would refute
        /// <c>ProvenOpaque</c> everywhere regardless of the sampled
        /// position — one non-opaque level refutes the proof for every
        /// triangle, not just the ones near it. This arm therefore uses
        /// uniformly opaque real mip-chain content, where sampled position
        /// is deliberately irrelevant to the content proof, to isolate the
        /// all-mip conjunction and prove the slot prepares and migrates
        /// through the Poiyomi conversion end to end.
        /// </description></item>
        /// </list>
        /// </summary>
        [Test]
        public void PoiyomiNonIdentityStSlotProvesOpaqueAndMigrates()
        {
            var scale = new Vector2(2f, 2f);
            var offset = new Vector2(0.5f, 0.25f);
            var mapping = new UvMapping(0, scale, offset);
            var triangle = TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.3f, 0.3f),
                new Vector2(0.4f, 0.3f),
                new Vector2(0.3f, 0.4f));
            var field = new AlphaTextureData(
                4,
                4,
                new byte[]
                {
                    0, 0, 0, 0,
                    0, 0, 0, 0,
                    0, 0, 0, 0,
                    0, 0, 0, 255,
                });
            var sampling = new AlphaSamplingSettings(
                AlphaFilterMode.Point, AlphaWrapMode.Clamp);

            Assert.That(
                TriangleAlphaClassifier.Classify(
                    triangle, field, sampling, AlphaUvEnvelope.Zero),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent),
                "fixture precondition: the raw (identity) UV domain must " +
                "sample the transparent interior, or the transform " +
                "contrast proves nothing");
            Assert.That(
                AffineUvTransform.TryTransform(
                    mapping, triangle, out var transformed, out var envelope),
                Is.True);
            Assert.That(
                TriangleAlphaClassifier.Classify(
                    transformed, field, sampling, envelope),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque),
                "the same triangle's ST-transformed domain must sample " +
                "the opaque corner");

            // --- Content-decided barrier proof: real single-level import --
            {
                using var assets = new OverrideTemporaryDirectoryScope(null);
                var root = new GameObject(
                    "AMUSE poiyomi nonidentity st content decided");
                root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                Material material = null;
                Mesh mesh = null;
                AmusePlatformFinishState amuse = null;
                var fixtures = new PoiyomiTextureBackedFixtures();

                try
                {
                    fixtures.BaseSetUp();
                    // Columns 0, 1 and 7 transparent, the rest opaque —
                    // the same 8x8 column mask
                    // PoiyomiAlphaTests.NonForcedMainTexNonIdentityStPreservesMappingAndClassifies
                    // uses. Raw UV in [0.05,0.1]x[0.15,0.2] sits in column 0
                    // (transparent); scale (2,2)/offset (0.5,0.25) moves it
                    // to [0.6,0.7]x[0.55,0.65], columns 4-5 (opaque).
                    var alpha = new byte[8 * 8];
                    for (var index = 0; index < alpha.Length; index++)
                    {
                        alpha[index] = 255;
                    }
                    for (var y = 0; y < 8; y++)
                    {
                        alpha[y * 8] = 0;
                        alpha[y * 8 + 1] = 0;
                        alpha[y * 8 + 7] = 0;
                    }
                    material = TextureBackedNonIdentityStMaterial(
                        fixtures.ImportSingleLevelAlphaField(
                            "poiyomi_nonidentity_st_content", 8, 8, alpha),
                        scale,
                        offset);
                    var renderer =
                        AddSingleTriangleRenderer(root, material, out mesh);
                    mesh.uv = new[]
                    {
                        new Vector2(0.05f, 0.15f),
                        new Vector2(0.1f, 0.15f),
                        new Vector2(0.05f, 0.2f),
                    };

                    amuse = RunBarrier(root);

                    Assert.That(
                        amuse.SemanticallyRefusedRendererCount, Is.Zero,
                        "fixture precondition: the renderer must be " +
                        "analyzable");
                    Assert.That(
                        amuse.OpaqueCandidateTriangleCount, Is.EqualTo(1),
                        "the raw UV samples the transparent column mask; " +
                        "only the captured ST transform can move the " +
                        "sampled domain into the opaque columns and prove " +
                        "the candidate through the full capture/admission" +
                        "/planner/resolver/classifier path");
                    Assert.That(
                        amuse.SlotRefusalCount(
                            AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused),
                        Is.Zero);
                    Assert.That(
                        amuse.Separation, Is.Not.Null,
                        "the slot must prepare and migrate");
                    Assert.That(
                        amuse.Separation.Renderers, Has.Count.EqualTo(1));
                    Assert.That(
                        amuse.Separation.Renderers[0].Target.Renderer,
                        Is.SameAs(renderer));
                    var slot =
                        amuse.Separation.Renderers[0].CandidateSlots.Single();
                    Assert.That(
                        slot.Plan.Disposition,
                        Is.EqualTo(SubmeshSeparationDisposition
                            .WhollyOpaqueCandidate));
                    Assert.That(
                        slot.OpaqueOfAdmitted[material],
                        Is.Not.SameAs(material),
                        "the Poiyomi value converts to a generated clone");
                    Assert.That(
                        amuse.Separation.CreatedClones,
                        Has.Count.EqualTo(1));
                }
                finally
                {
                    DestroyGenerated(amuse);
                    if (mesh != null)
                    {
                        UnityEngine.Object.DestroyImmediate(mesh);
                    }
                    UnityEngine.Object.DestroyImmediate(root);
                    if (material != null)
                    {
                        UnityEngine.Object.DestroyImmediate(material);
                    }

                    fixtures.BaseTearDown();
                }
            }

            // --- All-mip conjunction and migration: fully-opaque mip chain -
            {
                using var assets = new OverrideTemporaryDirectoryScope(null);
                var root = new GameObject(
                    "AMUSE poiyomi nonidentity st migrates");
                root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                Material material = null;
                Mesh mesh = null;
                AmusePlatformFinishState amuse = null;
                var fixtures = new PoiyomiTextureBackedFixtures();

                try
                {
                    fixtures.BaseSetUp();
                    material = TextureBackedNonIdentityStMaterial(
                        fixtures.ImportFullyOpaqueMipmap(
                            "poiyomi_nonidentity_st"),
                        scale,
                        offset);
                    var renderer =
                        AddSingleTriangleRenderer(root, material, out mesh);
                    mesh.uv = new[]
                    {
                        new Vector2(0.3f, 0.3f),
                        new Vector2(0.4f, 0.3f),
                        new Vector2(0.3f, 0.4f),
                    };

                    amuse = RunBarrier(root);

                    Assert.That(
                        amuse.SemanticallyRefusedRendererCount, Is.Zero,
                        "fixture precondition: the renderer must be " +
                        "analyzable");
                    Assert.That(
                        amuse.OpaqueCandidateTriangleCount, Is.EqualTo(1),
                        "the non-identity-ST slot over a fully-opaque mip " +
                        "chain must prove one opaque candidate triangle");
                    Assert.That(
                        amuse.SlotRefusalCount(
                            AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused),
                        Is.Zero);
                    Assert.That(
                        amuse.Separation, Is.Not.Null,
                        "the slot must prepare and migrate");
                    Assert.That(
                        amuse.Separation.Renderers, Has.Count.EqualTo(1));
                    Assert.That(
                        amuse.Separation.Renderers[0].Target.Renderer,
                        Is.SameAs(renderer));
                    var slot =
                        amuse.Separation.Renderers[0].CandidateSlots.Single();
                    Assert.That(
                        slot.Plan.Disposition,
                        Is.EqualTo(SubmeshSeparationDisposition
                            .WhollyOpaqueCandidate));
                    Assert.That(
                        slot.OpaqueOfAdmitted[material],
                        Is.Not.SameAs(material),
                        "the Poiyomi value converts to a generated clone");
                    Assert.That(
                        amuse.Separation.CreatedClones,
                        Has.Count.EqualTo(1));
                }
                finally
                {
                    DestroyGenerated(amuse);
                    if (mesh != null)
                    {
                        UnityEngine.Object.DestroyImmediate(mesh);
                    }
                    UnityEngine.Object.DestroyImmediate(root);
                    if (material != null)
                    {
                        UnityEngine.Object.DestroyImmediate(material);
                    }

                    fixtures.BaseTearDown();
                }
            }
        }

        /// <summary>
        /// Falsifier arm (b): a <c>_MainTex_ST</c> component animated to
        /// exactly its material's serialized default remains an admitted
        /// singleton, so the slot prepares — mirroring the existing
        /// conversion-only positive control
        /// (<see cref="ConversionOnlyAnimationAwayFromDefaultRefusesAndToDefaultPrepares"/>)
        /// for the alpha-relevant ST binding itself.
        /// </summary>
        [Test]
        public void PoiyomiStAnimatedAtSerializedDefaultPrepares()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE poiyomi st animated default");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material material = null;
            Mesh mesh = null;
            AnimationClip clip = null;
            AnimatorController controller = null;
            AmusePlatformFinishState amuse = null;
            var fixtures = new PoiyomiTextureBackedFixtures();

            try
            {
                fixtures.BaseSetUp();
                material = TextureBackedNonIdentityStMaterial(
                    fixtures.ImportFullyOpaqueMipmap(
                        "poiyomi_st_animated_default"),
                    new Vector2(2f, 2f),
                    new Vector2(0.5f, 0.25f));
                AddSingleTriangleRenderer(root, material, out mesh);
                mesh.uv = new[]
                {
                    new Vector2(0.3f, 0.3f),
                    new Vector2(0.4f, 0.3f),
                    new Vector2(0.3f, 0.4f),
                };
                clip = NewFloatClip(
                    "AMUSE poiyomi st default", string.Empty,
                    "material._MainTex_ST.x", 2f);
                controller = NewController(
                    root, "AMUSE poiyomi st default graph", clip);

                amuse = RunBarrier(root);

                Assert.That(
                    amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.Zero);
                Assert.That(
                    amuse.RendererRefusalCount(
                        RendererAnalysisRefusal
                            .AnimatedMaterialPropertyNotSingleton),
                    Is.Zero,
                    "animation at the serialized default must remain a " +
                    "singleton");
                Assert.That(amuse.OpaqueCandidateTriangleCount, Is.EqualTo(1));
                Assert.That(
                    amuse.Separation, Is.Not.Null,
                    "the animated value equals the serialized default, " +
                    "so the slot must prepare");
                Assert.That(
                    amuse.Separation.CreatedClones, Has.Count.EqualTo(1));
            }
            finally
            {
                DestroyGenerated(amuse);
                DestroyControllerGraph(root, controller);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);

                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// Falsifier arm (c): a <c>_MainTex_ST</c> component animated away
        /// from its material's serialized default refuses
        /// (<c>AnimatedMaterialPropertyNotSingleton</c>) and prepares
        /// nothing.
        /// </summary>
        [Test]
        public void PoiyomiNonSingletonStAnimationRefuses()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE poiyomi st non singleton");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material material = null;
            Mesh mesh = null;
            AnimationClip clip = null;
            AnimatorController controller = null;
            AmusePlatformFinishState amuse = null;
            var fixtures = new PoiyomiTextureBackedFixtures();

            try
            {
                fixtures.BaseSetUp();
                material = TextureBackedNonIdentityStMaterial(
                    fixtures.ImportFullyOpaqueMipmap(
                        "poiyomi_st_non_singleton"),
                    new Vector2(2f, 2f),
                    new Vector2(0.5f, 0.25f));
                AddSingleTriangleRenderer(root, material, out mesh);
                mesh.uv = new[]
                {
                    new Vector2(0.3f, 0.3f),
                    new Vector2(0.4f, 0.3f),
                    new Vector2(0.3f, 0.4f),
                };
                clip = NewFloatClip(
                    "AMUSE poiyomi st non singleton", string.Empty,
                    "material._MainTex_ST.x", 3f);
                controller = NewController(
                    root, "AMUSE poiyomi st non singleton graph", clip);

                amuse = RunBarrier(root);

                Assert.That(
                    amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(
                    amuse.RendererRefusalCount(
                        RendererAnalysisRefusal
                            .AnimatedMaterialPropertyNotSingleton),
                    Is.EqualTo(1),
                    "a _MainTex_ST component animated away from its " +
                    "serialized default must refuse at alpha admission");
                Assert.That(amuse.OpaqueCandidateTriangleCount, Is.Zero);
                Assert.That(
                    amuse.Separation, Is.Null,
                    "no non-singleton ST input may prepare");
            }
            finally
            {
                DestroyGenerated(amuse);
                DestroyControllerGraph(root, controller);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);

                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// Falsifier arm (d): a structural audit mirroring
        /// <c>AnalysisLeavesEverySourceObjectUnchanged</c>
        /// (<c>RendererAlphaAnalysisIntegrationTests</c>) at the preparation
        /// layer — the source material, its imported texture asset, and the
        /// source mesh are bit-for-bit unchanged after a non-identity-ST
        /// slot prepares and migrates.
        /// </summary>
        [Test]
        public void PoiyomiNonIdentityStPreparationLeavesSourceAssetsUnchanged()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE poiyomi st source preserved");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material material = null;
            Mesh mesh = null;
            AmusePlatformFinishState amuse = null;
            var fixtures = new PoiyomiTextureBackedFixtures();

            try
            {
                fixtures.BaseSetUp();
                var texture = fixtures.ImportFullyOpaqueMipmap(
                    "poiyomi_st_source_preserved");
                var texturePath = AssetDatabase.GetAssetPath(texture);
                material = TextureBackedNonIdentityStMaterial(
                    texture, new Vector2(2f, 2f), new Vector2(0.5f, 0.25f));
                var renderer =
                    AddSingleTriangleRenderer(root, material, out mesh);
                mesh.uv = new[]
                {
                    new Vector2(0.3f, 0.3f),
                    new Vector2(0.4f, 0.3f),
                    new Vector2(0.3f, 0.4f),
                };

                var beforeAssetHash =
                    AssetDatabase.GetAssetDependencyHash(texturePath);
                var beforeReadable = texture.isReadable;
                var beforePixels = texture.GetPixels32(0);
                var beforeVertices = mesh.vertices;
                var beforeUv = mesh.uv;
                var beforeIndices = mesh.GetIndices(0);
                var beforeMaterials = renderer.sharedMaterials;
                var beforeMainTex = material.GetTexture("_MainTex");
                var beforeScale = material.GetTextureScale("_MainTex");
                var beforeOffset = material.GetTextureOffset("_MainTex");

                amuse = RunBarrier(root);

                Assert.That(
                    amuse.Separation, Is.Not.Null,
                    "fixture precondition: the slot must prepare, or the " +
                    "non-mutation proves nothing");
                Assert.That(
                    amuse.Separation.CreatedClones, Is.Not.Empty,
                    "fixture precondition: the material must convert, or " +
                    "the transient boundary proves nothing");

                Assert.That(
                    AssetDatabase.GetAssetDependencyHash(texturePath),
                    Is.EqualTo(beforeAssetHash),
                    "preparation must not re-import or rewrite the " +
                    "texture asset");
                Assert.That(texture.isReadable, Is.EqualTo(beforeReadable));
                Assert.That(
                    texture.GetPixels32(0), Is.EqualTo(beforePixels));
                Assert.That(mesh.vertices, Is.EqualTo(beforeVertices));
                Assert.That(mesh.uv, Is.EqualTo(beforeUv));
                Assert.That(mesh.GetIndices(0), Is.EqualTo(beforeIndices));
                Assert.That(
                    renderer.sharedMaterials, Is.EqualTo(beforeMaterials));
                Assert.That(renderer.sharedMesh, Is.SameAs(mesh));
                Assert.That(
                    material.GetTexture("_MainTex"), Is.SameAs(beforeMainTex));
                Assert.That(
                    material.GetTextureScale("_MainTex"),
                    Is.EqualTo(beforeScale));
                Assert.That(
                    material.GetTextureOffset("_MainTex"),
                    Is.EqualTo(beforeOffset));
            }
            finally
            {
                DestroyGenerated(amuse);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }

                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// Boundary scenario (plan Task 4 item 1, second half): the same
        /// non-identity <c>_MainTex</c> ST shape on a lilToon cutout slot
        /// prepares zero opaque candidates — the C4 frontend gate refuses
        /// the alpha claim itself rather than letting the widened
        /// resolver's affine coverage reach it. Falsifies deleting the
        /// lilToon cutout non-identity-ST gate (F15): with it gone, this
        /// material's alpha would become complete and the slot would
        /// classify and migrate exactly like the Poiyomi scenario above.
        /// </summary>
        [Test]
        public void LilToonCutoutNonIdentityStSlotPreparesNoOpaqueCandidates()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonCutoutConversionFixtures();

            try
            {
                fixtures.BaseSetUp();
                var texture = fixtures.ImportFullyOpaqueMipmap(
                    "cutout_nonidentity_st_boundary");

                using var arm = CutoutArmFixture.Create(
                    texture,
                    "AMUSE cutout nonidentity st boundary",
                    boundaryMaterial =>
                    {
                        boundaryMaterial.SetTextureScale(
                            "_MainTex", new Vector2(2f, 2f));
                        boundaryMaterial.SetTextureOffset(
                            "_MainTex", new Vector2(0.5f, 0.25f));
                    },
                    null,
                    null);
                var amuse = arm.Run();

                Assert.That(
                    amuse.RendererRefusalCount(
                        RendererAnalysisRefusal
                            .AdmittedMaterialSemanticsUnknown),
                    Is.EqualTo(1),
                    "a non-identity _MainTex ST must refuse at the " +
                    "lilToon cutout frontend's own C4 gate, not reach the " +
                    "resolver");
                Assert.That(amuse.OpaqueCandidateTriangleCount, Is.Zero);
                Assert.That(
                    amuse.Separation, Is.Null,
                    "zero opaque candidates leaves nothing to prepare");
            }
            finally
            {
                fixtures.BaseTearDown();
            }
        }


        // --- Task 5 integration coverage: the cutout family end to end -----

        /// <summary>
        /// Cutoff slot outcomes through the full barrier path (spec §8.3).
        /// <para>
        /// Falsifies: an eligibility boundary read as a strict less-than
        /// (which would refuse the at-bound positive control), a cutoff gate
        /// read from the live material instead of the captured evidence, and
        /// a classification that treats an unresolvable admitted material as
        /// transparent instead of refusing the renderer with
        /// <c>AdmittedMaterialSemanticsUnknown</c>.
        /// </para>
        /// </summary>
        [Test]
        public void CutoutCutoffSlotOutcomesThroughTheFullPath()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonCutoutConversionFixtures();
            try
            {
                fixtures.BaseSetUp();
                var texture =
                    fixtures.ImportFullyOpaqueMipmap("cutoff_outcomes");

                // (a) A cutoff exactly at the twice-margin bound prepares.
                using (var arm = CutoutArmFixture.Create(
                           texture,
                           "AMUSE cutoff at bound",
                           material => material.SetFloat(
                               "_Cutoff",
                               LilToonCutoutSourceEligibility.MaxProvableCutoff),
                           null,
                           null))
                {
                    var amuse = arm.Run();

                    Assert.That(amuse.AvatarRefusal,
                        Is.EqualTo(AvatarAnimationRefusal.None));
                    Assert.That(amuse.SemanticallyRefusedRendererCount,
                        Is.Zero);
                    Assert.That(amuse.OpaqueCandidateTriangleCount,
                        Is.EqualTo(1),
                        "fixture precondition: a cutoff exactly at the " +
                        "twice-margin bound must still prove the triangle " +
                        "opaque, or the boundary has no positive control");
                    foreach (AlphaSeparationSlotRefusal reason in Enum
                                 .GetValues(
                                     typeof(AlphaSeparationSlotRefusal)))
                    {
                        if (reason == AlphaSeparationSlotRefusal.None)
                        {
                            continue;
                        }

                        Assert.That(
                            amuse.SlotRefusalCount(reason), Is.Zero,
                            "the at-bound slot must prepare: " + reason);
                    }

                    Assert.That(amuse.Separation, Is.Not.Null);
                    Assert.That(
                        amuse.Separation.TryGetOpaque(
                            arm.Material, out var opaque),
                        Is.True,
                        "the at-bound slot must convert and carry the " +
                        "opaque mapping");
                    Assert.That(opaque, Is.Not.Null);
                }

                // (b) Above the bound and non-finite refuse at alpha
                // resolution: no provable triangle, nothing retained.
                foreach (var cutoff in new[] { 1f, 1.001f, float.NaN })
                {
                    using (var arm = CutoutArmFixture.Create(
                               texture,
                               "AMUSE cutoff refused",
                               material => material.SetFloat(
                                   "_Cutoff", cutoff),
                               null,
                               null))
                    {
                        var amuse = arm.Run();

                        Assert.That(amuse.AvatarRefusal,
                            Is.EqualTo(AvatarAnimationRefusal.None));
                        Assert.That(
                            amuse.RendererRefusalCount(
                                RendererAnalysisRefusal
                                    .AdmittedMaterialSemanticsUnknown),
                            Is.EqualTo(1),
                            "a cutoff of " + cutoff + " leaves the cutout " +
                            "alpha equation unresolved, which must refuse " +
                            "the renderer at alpha resolution");
                        Assert.That(amuse.SemanticallyRefusedRendererCount,
                            Is.EqualTo(1),
                            "exactly the refusing renderer may be counted");
                        Assert.That(amuse.AnalyzedRendererCount, Is.Zero,
                            "a refused renderer is never analyzed");
                        Assert.That(amuse.OpaqueCandidateTriangleCount,
                            Is.Zero,
                            "no triangle may be proven over an unresolved " +
                            "alpha equation");
                        Assert.That(amuse.Separation, Is.Null,
                            "nothing may be retained for the refusing " +
                            "variant, so nothing can ever be applied from " +
                            "it");
                    }
                }
            }
            finally
            {
                fixtures.BaseTearDown();
            }
        }
        [Test]
        public void CutoutOptionalCoveragePathsRefuseEndToEnd()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonCutoutConversionFixtures();
            try
            {
                fixtures.BaseSetUp();
                var texture = fixtures.ImportFullyOpaqueMipmap(
                    "cutout_optional_paths");
                var cases = new (string Label, Action<Material> Configure)[]
                {
                    ("_UseDither",
                        material => material.SetFloat("_UseDither", 1f)),
                    ("_AlphaMaskMode=1",
                        material => material.SetFloat("_AlphaMaskMode", 1f)),
                    ("_AlphaMaskMode=2",
                        material => material.SetFloat("_AlphaMaskMode", 2f)),
                    ("_AlphaMaskMode=3",
                        material => material.SetFloat("_AlphaMaskMode", 3f)),
                    ("_AlphaMaskMode=4",
                        material => material.SetFloat("_AlphaMaskMode", 4f)),
                    ("_DissolveParams.x",
                        material => material.SetVector(
                            "_DissolveParams",
                            new Vector4(1f, 0f, 0f, 0f))),
                    ("_UseParallax",
                        material => material.SetFloat("_UseParallax", 1f)),
                    ("_ShiftBackfaceUV",
                        material => material.SetFloat(
                            "_ShiftBackfaceUV", 1f)),
                    ("_UDIMDiscardCompile",
                        material => material.SetFloat(
                            "_UDIMDiscardCompile", 1f)),
                    ("_UDIMDiscardMode",
                        material => material.SetFloat(
                            "_UDIMDiscardMode", 1f)),
                    ("_IDMask1",
                        material => material.SetFloat("_IDMask1", 1f)),
                    ("_IDMaskControlsDissolve",
                        material => material.SetFloat(
                            "_IDMaskControlsDissolve", 1f)),
                };

                foreach (var optionalPath in cases)
                {
                    using var arm = CutoutArmFixture.Create(
                        texture,
                        "AMUSE cutout optional " + optionalPath.Label,
                        optionalPath.Configure,
                        null,
                        null);
                    var amuse = arm.Run();

                    Assert.That(
                        amuse.RendererRefusalCount(
                            RendererAnalysisRefusal
                                .AdmittedMaterialSemanticsUnknown),
                        Is.EqualTo(1),
                        optionalPath.Label +
                        ": an active optional coverage path must refuse " +
                        "at alpha resolution");
                    Assert.That(amuse.OpaqueCandidateTriangleCount, Is.Zero,
                        optionalPath.Label);
                    Assert.That(amuse.Separation, Is.Null,
                        optionalPath.Label);
                }
            }
            finally
            {
                fixtures.BaseTearDown();
            }
        }

        [Test]
        public void UnsupportedCutoutTextureEvidenceRefusesEndToEnd()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonCutoutConversionFixtures();
            var renderTexture = new RenderTexture(4, 4, 0);
            try
            {
                fixtures.BaseSetUp();
                var cases = new (string Label, Texture Texture)[]
                {
                    ("trilinear",
                        fixtures.ImportTrilinearMipmap("cutout_trilinear")),
                    ("mismatched-wrap",
                        fixtures.ImportMismatchedWrapMipmap(
                            "cutout_mismatched_wrap")),
                    ("streaming-mipmap",
                        fixtures.ImportStreamingMipmap(
                            "cutout_streaming")),
                    ("non-Texture2D", renderTexture),
                };

                foreach (var unsupported in cases)
                {
                    using var arm = CutoutArmFixture.Create(
                        unsupported.Texture,
                        "AMUSE cutout texture " + unsupported.Label,
                        null,
                        null,
                        null);
                    var amuse = arm.Run();

                    var unknownRefusals = amuse.RendererRefusalCount(
                        RendererAnalysisRefusal
                            .AdmittedMaterialSemanticsUnknown);
                    Assert.That(unknownRefusals,
                        Is.EqualTo(0).Or.EqualTo(1),
                        unsupported.Label +
                        ": unsupported evidence may refuse resolution or " +
                        "analyze to no candidate, but never prove opaque");
                    if (unknownRefusals == 0)
                    {
                        Assert.That(amuse.AnalyzedRendererCount,
                            Is.EqualTo(1), unsupported.Label);
                    }
                    Assert.That(amuse.OpaqueCandidateTriangleCount, Is.Zero,
                        unsupported.Label);
                    Assert.That(amuse.Separation, Is.Null,
                        unsupported.Label);
                }
            }
            finally
            {
                fixtures.BaseTearDown();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        /// <summary>
        /// Non-singleton animation of every cutout alpha-request scalar,
        /// every addressable alpha vector/color/scale-offset component, and
        /// every conversion recipe scalar refuses at its own relevance layer
        /// (coverage 11).
        /// <para>
        /// Falsifies live-value admission and any relevance set that omits
        /// one requested scalar or addressable component.
        /// </para>
        /// </summary>
        [Test]
        public void
            CutoutNonSingletonAnimationRefusesAtItsRelevanceLayer()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonCutoutConversionFixtures();
            try
            {
                fixtures.BaseSetUp();
                var texture = fixtures.ImportFullyOpaqueMipmap(
                    "cutout_animation");
                using var probe = CutoutArmFixture.Create(
                    texture, "AMUSE cutout defaults probe", null, null, null);

                foreach (var animated in
                         CutoutAnimatedPropertyCases(probe.Material))
                {
                    using var arm = CutoutArmFixture.Create(
                        texture,
                        "AMUSE cutout non-singleton " + animated.Label,
                        null,
                        animated.Binding,
                        animated.Refused);
                    var amuse = arm.Run();

                    Assert.That(amuse.AvatarRefusal,
                        Is.EqualTo(AvatarAnimationRefusal.None),
                        animated.Label);
                    if (animated.AlphaRelevant)
                    {
                        Assert.That(
                            amuse.RendererRefusalCount(
                                RendererAnalysisRefusal
                                    .AnimatedMaterialPropertyNotSingleton),
                            Is.EqualTo(1),
                            animated.Label + ": every cutout-alpha input " +
                            "must refuse at alpha admission when its curve " +
                            "disagrees with the serialized value");
                    }
                    else
                    {
                        Assert.That(amuse.SemanticallyRefusedRendererCount,
                            Is.Zero, animated.Label);
                        Assert.That(
                            amuse.SlotRefusalCount(
                                AlphaSeparationSlotRefusal
                                    .ConversionStateNotAdmitted),
                            Is.EqualTo(1),
                            animated.Label + ": every conversion recipe " +
                            "input must refuse at conversion admission when " +
                            "its curve disagrees with the serialized value");
                    }

                    Assert.That(amuse.Separation, Is.Null,
                        animated.Label +
                        ": no non-singleton proof input may prepare");
                }
            }
            finally
            {
                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// Positive controls for every alpha-proof input: each binding
        /// animated exactly at its material's serialized value remains a
        /// singleton, so the slot prepares.
        /// <para>
        /// Falsifies an admission that cannot represent the
        /// singleton-at-serialized-value case — one that refuses every
        /// animated property outright, or one that substitutes the wrong
        /// component and breaks the alpha proof.
        /// </para>
        /// </summary>
        [Test]
        public void CutoutAnimationAtTheSerializedValuePrepares()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonCutoutConversionFixtures();
            try
            {
                fixtures.BaseSetUp();
                var texture = fixtures.ImportFullyOpaqueMipmap(
                    "cutout_animation_serialized");
                using var probe = CutoutArmFixture.Create(
                    texture, "AMUSE cutout singleton defaults", null, null,
                    null);

                foreach (var animated in
                         CutoutAnimatedPropertyCases(probe.Material))
                {
                    if (!animated.AlphaRelevant)
                    {
                        continue;
                    }

                    using var arm = CutoutArmFixture.Create(
                        texture,
                        "AMUSE cutout singleton " + animated.Label,
                        null,
                        animated.Binding,
                        animated.Serialized);
                    var amuse = arm.Run();

                    Assert.That(amuse.AvatarRefusal,
                        Is.EqualTo(AvatarAnimationRefusal.None),
                        animated.Label);
                    Assert.That(amuse.SemanticallyRefusedRendererCount,
                        Is.Zero, animated.Label);
                    foreach (AlphaSeparationSlotRefusal reason in Enum
                                 .GetValues(
                                     typeof(AlphaSeparationSlotRefusal)))
                    {
                        if (reason == AlphaSeparationSlotRefusal.None)
                        {
                            continue;
                        }

                        Assert.That(amuse.SlotRefusalCount(reason), Is.Zero,
                            animated.Label +
                            ": animation at the serialized value must " +
                            "remain an admitted singleton: " + reason);
                    }

                    Assert.That(amuse.OpaqueCandidateTriangleCount,
                        Is.EqualTo(1), animated.Label);
                    Assert.That(amuse.Separation, Is.Not.Null,
                        animated.Label);
                    Assert.That(amuse.Separation.CreatedClones,
                        Has.Count.EqualTo(1), animated.Label);
                }
            }
            finally
            {
                fixtures.BaseTearDown();
            }
        }

        // --- Task 6: the transparent family end to end -----------------------

        /// <summary>
        /// Routes the family-agnostic lilToon conversion seam parameter to
        /// the family-specific verified seam function by fixture shader
        /// reference — the transparent stand-in to
        /// <see cref="VerifiedLilToonTestSeams.VerifiedTransparentConversionStep"/>,
        /// everything else (the cutout stand-in first of all) to
        /// <see cref="VerifiedLilToonTestSeams.VerifiedConversion"/>, whose
        /// behavior for those materials is unchanged. Preparation dispatches
        /// on the closed family enum internally and passes one seam function
        /// through; the fixtures are distinguished by shader reference,
        /// never by name, matching the seams file's own rule.
        /// </summary>
        private static bool VerifiedFamilyConversion(
            Material live,
            CapturedMaterialEvidence derived,
            Material preparedOpaque,
            out Material opaque,
            out LilToonOpaqueConversionRefusal refusal)
        {
            if (live != null && live.shader == Shader.Find(
                    LilToonFixtureNames.Transparent))
            {
                return VerifiedLilToonTestSeams
                    .VerifiedTransparentConversionStep(
                        live, derived, preparedOpaque,
                        out opaque, out refusal);
            }

            return VerifiedLilToonTestSeams.VerifiedConversion(
                live, derived, preparedOpaque, out opaque, out refusal);
        }

        /// <summary>
        /// Row 18 — the prepared-clone contract for a transparent stand-in
        /// source, through the real capture and preparation path.
        /// <para>
        /// (a) A transparent fixture slot over a fully-opaque mip chain
        /// converts to one clone carrying the attested opaque stand-in
        /// target shader, all 18 canonical recipe values read back, queue
        /// 2000, <c>RenderType=Opaque</c>, no non-canonical fact, while the
        /// source material keeps every observed fact.
        /// </para>
        /// <para>
        /// (b) A target shader missing one recipe property throws
        /// <see cref="InvalidOperationException"/> — a compatibility
        /// failure, never a refusal — before any clone exists, and no
        /// material leaks.
        /// </para>
        /// <para>
        /// The third throw checkpoint (a read-back disagreement after
        /// <c>DestroyImmediate</c>) is a defensive invariant against Unity
        /// breaking its own material write contract: the material property
        /// bag is name-keyed and round-trips every <c>SetFloat</c>
        /// regardless of how the target shader declares the property
        /// (verified empirically against Float/2D/Vector declarations), and
        /// the queue and tag writes round-trip likewise, so no deterministic
        /// public synthetic shader can drive that checkpoint to fire. The
        /// checkpoint's detector is exercised positively by the read-backs
        /// below and its policy (throw, not refusal) by (b). Recorded as a
        /// plan defect for the implementation report.
        /// </para>
        /// </summary>
        [Test]
        public void TransparentFixtureSlotPreparesAndCarriesTheOpaqueCloneContract()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonTransparentConversionFixtures();
            try
            {
                fixtures.BaseSetUp();

                // (a) The end-to-end prepared-clone contract.
                var root = new GameObject(
                    "AMUSE transparent clone contract");
                root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                Material material = null;
                Mesh mesh = null;
                AmusePlatformFinishState amuse = null;
                try
                {
                    material =
                        LilToonFixtureTestBase
                            .CreateTransparentConversionMaterial();
                    material.SetTexture(
                        "_MainTex",
                        fixtures.ImportFullyOpaqueMipmap(
                            "transparent_clone_contract"));
                    var renderer = AddSingleTriangleRenderer(
                        root, material, out mesh);
                    mesh.uv = new[]
                    {
                        new Vector2(0.25f, 0.25f),
                        new Vector2(0.75f, 0.25f),
                        new Vector2(0.25f, 0.75f),
                    };

                    var sourceDigest =
                        TransparentSourceDigest(material);

                    amuse = RunBarrier(
                        root,
                        selectRequest: VerifiedLilToonTestSeams
                            .SelectVerifiedFixtureRequest,
                        capturer: VerifiedLilToonTestSeams
                            .CaptureVerifiedFixtureMaterials,
                        resolveSemantics: VerifiedLilToonTestSeams
                            .VerifiedAlphaOnly,
                        lilToonConversion: VerifiedFamilyConversion);

                    Assert.That(
                        amuse.AvatarRefusal,
                        Is.EqualTo(AvatarAnimationRefusal.None));
                    Assert.That(
                        amuse.SemanticallyRefusedRendererCount, Is.Zero,
                        "fixture precondition: the renderer must be " +
                        "analyzable");
                    Assert.That(
                        amuse.OpaqueCandidateTriangleCount,
                        Is.EqualTo(1),
                        "fixture precondition: the transparent slot over " +
                        "a fully-opaque mip chain must prove one opaque " +
                        "candidate triangle");
                    foreach (
                        AlphaSeparationSlotRefusal reason in Enum
                            .GetValues(typeof(AlphaSeparationSlotRefusal)))
                    {
                        if (reason == AlphaSeparationSlotRefusal.None)
                        {
                            continue;
                        }

                        Assert.That(
                            amuse.SlotRefusalCount(reason), Is.Zero,
                            "the transparent slot must convert: " + reason);
                    }

                    Assert.That(amuse.Separation, Is.Not.Null);
                    Assert.That(
                        amuse.Separation.CreatedClones, Has.Count.EqualTo(1),
                        "exactly one canonical opaque clone must be " +
                        "created for the transparent source");
                    Assert.That(
                        amuse.Separation.TryGetOpaque(
                            material, out var opaque),
                        Is.True,
                        "the prepared record must carry the transparent " +
                        "source-to-opaque mapping");
                    Assert.That(
                        opaque, Is.SameAs(amuse.Separation.CreatedClones[0]),
                        "the mapped opaque result must be the one created " +
                        "clone");
                    AssertPreparedCloneCarriesTheCanonicalOpaqueTarget(
                        opaque);
                    Assert.That(
                        TransparentSourceDigest(material),
                        Is.EqualTo(sourceDigest),
                        "the transparent source material must be " +
                        "unchanged by its own conversion");
                }
                finally
                {
                    DestroyGenerated(amuse);
                    if (mesh != null)
                    {
                        UnityEngine.Object.DestroyImmediate(mesh);
                    }

                    UnityEngine.Object.DestroyImmediate(root);
                    if (material != null)
                    {
                        UnityEngine.Object.DestroyImmediate(material);
                    }
                }

                // (b) A target shader missing one recipe property throws
                // before any clone exists.
                var cloneContractFolder =
                    TransparentCloneContractTempFolder;
                if (!AssetDatabase.IsValidFolder(cloneContractFolder))
                {
                    AssetDatabase.CreateFolder(
                        "Assets", "AmuseTests_AlphaCloneContract");
                }

                try
                {
                    var missingTarget = ImportShaderWithoutRecipeProperty();
                    var source =
                        LilToonFixtureTestBase
                            .CreateTransparentConversionMaterial();
                    try
                    {
                        var materialsBefore = LoadedMaterialCount();

                        Assert.Throws<InvalidOperationException>(
                            () => LilToonOpaqueTarget
                                .PrepareCanonicalOpaqueClone(
                                    source, missingTarget),
                            "a target that cannot declare the recipe is a " +
                            "compatibility failure, which must throw rather " +
                            "than refuse or half-convert");

                        Assert.That(
                            LoadedMaterialCount(),
                            Is.EqualTo(materialsBefore),
                            "the property check runs before any clone " +
                            "exists, so no material may leak");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(source);
                    }
                }
                finally
                {
                    AssetDatabase.DeleteAsset(cloneContractFolder);
                    Assert.That(
                        AssetDatabase.IsValidFolder(cloneContractFolder),
                        Is.False,
                        "the test-owned clone-contract directory must be " +
                        "deleted even when an assertion fails");
                }
            }
            finally
            {
                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// Row 15 — the animation closure over the transparent slot. For
        /// each proof-relevant binding the plan names — the color alpha,
        /// the theorem scalar, both transparent-only scalars, the distance
        /// fade, the dissolve parameters, the alpha mask gate, the scroll
        /// rotation, the main scale-offset, and one clause-2 recipe gate —
        /// a curve disagreeing with the serialized value refuses, and the
        /// refusal is asserted at its own named member: the alpha-request
        /// inputs at
        /// <see cref="RendererAnalysisRefusal.AnimatedMaterialPropertyNotSingleton"/>,
        /// the recipe input at
        /// <see cref="AlphaSeparationSlotRefusal.ConversionStateNotAdmitted"/>.
        /// <para>
        /// Falsifies live-value admission and any transparent request that
        /// omits a proof-relevant property: an omitted property makes its
        /// binding unrecognized or invisible, which fails the member
        /// assertion — the case is asserted to be recognized
        /// (<c>ConversionBindingUnrecognized</c> stays zero) and refused at
        /// its own layer, never merely "not converted".
        /// </para>
        /// </summary>
        [Test]
        public void TransparentNonSingletonAnimationRefusesAtItsRelevanceLayer()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonTransparentConversionFixtures();
            try
            {
                fixtures.BaseSetUp();
                var texture = fixtures.ImportFullyOpaqueMipmap(
                    "transparent_animation");
                using var probe = TransparentArmFixture.Create(
                    texture, "AMUSE transparent defaults probe",
                    null, null, null);

                foreach (var animated in
                         TransparentAnimatedPropertyCases(probe.Material))
                {
                    using var arm = TransparentArmFixture.Create(
                        texture,
                        "AMUSE transparent non-singleton " + animated.Label,
                        null,
                        animated.Binding,
                        animated.Refused);
                    var amuse = arm.Run();

                    Assert.That(amuse.AvatarRefusal,
                        Is.EqualTo(AvatarAnimationRefusal.None),
                        animated.Label);
                    Assert.That(
                        amuse.SlotRefusalCount(
                            AlphaSeparationSlotRefusal
                                .ConversionBindingUnrecognized),
                        Is.Zero,
                        animated.Label + ": the binding must be recognized " +
                        "by the transparent requests; unrecognized would be " +
                        "the signature of an omitted proof-relevant " +
                        "property");
                    if (animated.AlphaRelevant)
                    {
                        Assert.That(
                            amuse.RendererRefusalCount(
                                RendererAnalysisRefusal
                                    .AnimatedMaterialPropertyNotSingleton),
                            Is.EqualTo(1),
                            animated.Label + ": every transparent alpha " +
                            "input must refuse at alpha admission when its " +
                            "curve disagrees with the serialized value");
                        Assert.That(
                            amuse.SemanticallyRefusedRendererCount,
                            Is.EqualTo(1), animated.Label);
                        Assert.That(amuse.AnalyzedRendererCount, Is.Zero,
                            animated.Label);
                    }
                    else
                    {
                        Assert.That(amuse.SemanticallyRefusedRendererCount,
                            Is.Zero, animated.Label);
                        Assert.That(
                            amuse.SlotRefusalCount(
                                AlphaSeparationSlotRefusal
                                    .ConversionStateNotAdmitted),
                            Is.EqualTo(1),
                            animated.Label + ": every conversion recipe " +
                            "input must refuse at conversion admission when " +
                            "its curve disagrees with the serialized value");
                        Assert.That(amuse.AnalyzedRendererCount,
                            Is.EqualTo(1), animated.Label);
                        Assert.That(amuse.OpaqueCandidateTriangleCount,
                            Is.EqualTo(1), animated.Label);
                    }

                    Assert.That(amuse.Separation, Is.Null,
                        animated.Label +
                        ": no non-singleton proof input may prepare");
                }
            }
            finally
            {
                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// The transparent slot's animated property cases: exactly the row
        /// 15 list, with the vector inputs at component granularity like
        /// the cutout suite. <c>_UseDither</c> is deliberately absent from
        /// this table — it is not a transparent proof input, and its inert
        /// binding has its own test.
        /// </summary>
        private static IReadOnlyList<(
            string Label,
            string Binding,
            float Serialized,
            float Refused,
            bool AlphaRelevant)> TransparentAnimatedPropertyCases(
                Material material)
        {
            var cases = new List<(
                string Label,
                string Binding,
                float Serialized,
                float Refused,
                bool AlphaRelevant)>();

            foreach (var scalar in new[]
                     {
                         "_Cutoff",
                         "_AlphaBoostFA",
                         "_SubpassCutoff",
                         "_AlphaMaskMode",
                     })
            {
                var serialized = material.GetFloat(scalar);
                cases.Add((
                    scalar,
                    "material." + scalar,
                    serialized,
                    serialized + 1f,
                    true));
            }

            var colorAlpha = material.GetColor("_Color").a;
            cases.Add((
                "_Color.a",
                "material._Color.a",
                colorAlpha,
                colorAlpha + 1f,
                true));

            AddVectorCases(
                cases,
                "_DistanceFade",
                material.GetVector("_DistanceFade"));
            AddVectorCases(
                cases,
                "_DissolveParams",
                material.GetVector("_DissolveParams"));
            AddVectorCases(
                cases,
                "_MainTex_ScrollRotate",
                material.GetVector("_MainTex_ScrollRotate"));

            var scale = material.GetTextureScale("_MainTex");
            var offset = material.GetTextureOffset("_MainTex");
            AddComponentCase(cases, "_MainTex_ST", "x", scale.x);
            AddComponentCase(cases, "_MainTex_ST", "y", scale.y);
            AddComponentCase(cases, "_MainTex_ST", "z", offset.x);
            AddComponentCase(cases, "_MainTex_ST", "w", offset.y);

            // One representative clause-2 gate: a canonical recipe scalar
            // that conversion writes and the runtime could overwrite.
            var zWrite = material.GetFloat("_ZWrite");
            cases.Add((
                "_ZWrite",
                "material._ZWrite",
                zWrite,
                zWrite + 1f,
                false));

            return cases;
        }

        /// <summary>
        /// The settled animated-<c>_UseDither</c> contract (design §8, Task
        /// 3 Step 8). <c>LIL_RENDER 2</c> compiles the runtime dither path
        /// out entirely, so an animated <c>material._UseDither</c> binding
        /// on a transparent-only renderer is provably inert: it resolves
        /// <see cref="ProofRelevantBindingResolution.Irrelevant"/> against
        /// the family's own relevance requests, and the real capture and
        /// preparation path converts the renderer into a result observably
        /// equivalent to the same renderer without the binding.
        /// <para>
        /// The two scenarios are two independent preparation runs, so the
        /// equivalence table asserts observable facts — triangle outcome,
        /// the absence of every refusal member, clone count, the attested
        /// target shader, queue and <c>RenderType</c>, all 18 canonical
        /// values, and source preservation — never object identity across
        /// runs. <c>Is.SameAs</c> appears only within one run, between the
        /// mapping's clone and the run's own created clone.
        /// </para>
        /// <para>
        /// The same test drives a cutout source with the same disagreeing
        /// binding and asserts it still resolves through the cutout
        /// <c>_UseDither</c> request and still hits the cutout gate, so the
        /// transparent omission did not remove cutout relevance. A
        /// transparent-only resolution other than <c>Irrelevant</c> is
        /// stop condition 6, not an assertion to adjust.
        /// </para>
        /// </summary>
        [Test]
        public void AnimatedUseDither_IsIgnoredAsProvablyInert()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonTransparentConversionFixtures();
            try
            {
                fixtures.BaseSetUp();
                var texture = fixtures.ImportFullyOpaqueMipmap(
                    "transparent_use_dither");

                // The settled resolution, asserted directly against the
                // family's own relevance objects: a dither binding on the
                // analyzed renderer is irrelevant under both the alpha
                // request and the conversion request.
                var ditherBinding = new CapturedFloatBinding(
                    "body",
                    typeof(SkinnedMeshRenderer).FullName,
                    "material._UseDither",
                    true,
                    new[] { 1f });
                Assert.That(
                    UnityAnimationEvidenceCapture.ResolveProofRelevant(
                        ditherBinding,
                        "body",
                        LilToonTransparentMaterialSemantics
                            .AlphaEvidenceRequest,
                        out _),
                    Is.EqualTo(ProofRelevantBindingResolution.Irrelevant),
                    "the transparent alpha request must not recognize the " +
                    "dither binding: recognizing it would refuse an inert " +
                    "binding");
                Assert.That(
                    UnityAnimationEvidenceCapture.ResolveProofRelevant(
                        ditherBinding,
                        "body",
                        LilToonTransparentSourceEligibility
                            .ConversionEvidenceRequest,
                        out _),
                    Is.EqualTo(ProofRelevantBindingResolution.Irrelevant),
                    "the transparent conversion request must not recognize " +
                    "the dither binding: carrying _UseDither quietly would " +
                    "make the binding conversion-relevant");

                // The bound scenario: the real relevance pass and the real
                // preparation entry over a transparent-only renderer whose
                // one clip animates material._UseDither away from its
                // serialized default.
                var bound = TransparentDitherScenario.Run(
                    fixtures, texture, withBinding: true);
                var unbound = default(TransparentDitherScenario);
                try
                {
                    unbound = TransparentDitherScenario.Run(
                        fixtures, texture, withBinding: false);

                    Assert.That(bound.OpaqueCandidateTriangleCount,
                        Is.EqualTo(1),
                        "the bound scenario's triangle must prove opaque");
                    Assert.That(unbound.OpaqueCandidateTriangleCount,
                        Is.EqualTo(bound.OpaqueCandidateTriangleCount),
                        "the unbound scenario must prove the same triangle " +
                        "outcome");

                    Assert.That(bound.HasAnyRefusal, Is.False,
                        "the bound scenario must complete with no refusal " +
                        "member of any kind, including the unrecognized-" +
                        "binding refusal");
                    Assert.That(unbound.HasAnyRefusal,
                        Is.EqualTo(bound.HasAnyRefusal));

                    Assert.That(bound.CloneCount, Is.EqualTo(1),
                        "exactly one canonical opaque clone must exist in " +
                        "the bound scenario");
                    Assert.That(unbound.CloneCount,
                        Is.EqualTo(bound.CloneCount));

                    Assert.That(bound.CloneShaderName,
                        Is.EqualTo(LilToonFixtureNames.OpaqueTarget),
                        "the bound scenario's clone must carry the " +
                        "attested opaque target stand-in");
                    Assert.That(unbound.CloneShaderName,
                        Is.EqualTo(bound.CloneShaderName));

                    Assert.That(bound.CloneQueue,
                        Is.EqualTo(LilToonOpaqueTarget
                            .CanonicalOpaqueRenderQueue));
                    Assert.That(bound.CloneRenderType,
                        Is.EqualTo(LilToonOpaqueTarget
                            .CanonicalOpaqueRenderType));
                    Assert.That(unbound.CloneQueue,
                        Is.EqualTo(bound.CloneQueue));
                    Assert.That(unbound.CloneRenderType,
                        Is.EqualTo(bound.CloneRenderType));

                    Assert.That(bound.CloneRecipe,
                        Is.EqualTo(unbound.CloneRecipe),
                        "all 18 canonical values must be equal across the " +
                        "two scenarios, read back property by property");
                    Assert.That(
                        LilToonOpaqueTarget.TryFindNonCanonicalFact(
                            bound.Clone, out _),
                        Is.False,
                        "the bound scenario's clone must read back wholly " +
                        "canonical");

                    Assert.That(bound.SourceDigest,
                        Is.EqualTo(bound.SourceDigestBefore),
                        "the bound scenario's source material, mesh and " +
                        "clip must be unchanged");
                    Assert.That(unbound.SourceDigest,
                        Is.EqualTo(unbound.SourceDigestBefore),
                        "the unbound scenario's source material and mesh " +
                        "must be unchanged");
                    Assert.That(
                        bound.SourceDigest, Is.EqualTo(unbound.SourceDigest),
                        "both scenarios must start from the same source " +
                        "facts");
                }
                finally
                {
                    unbound.Dispose();
                }

                bound.Dispose();

                // The cutout control: the same disagreeing binding still
                // resolves through the cutout _UseDither request and still
                // hits the cutout gate at alpha admission.
                using (var arm = CutoutArmFixture.Create(
                           texture,
                           "AMUSE cutout animated dither gate",
                           null,
                           "material._UseDither",
                           1f))
                {
                    var amuse = arm.Run();

                    Assert.That(amuse.AvatarRefusal,
                        Is.EqualTo(AvatarAnimationRefusal.None));
                    Assert.That(
                        amuse.RendererRefusalCount(
                            RendererAnalysisRefusal
                                .AnimatedMaterialPropertyNotSingleton),
                        Is.EqualTo(1),
                        "the cutout family must still treat an animated " +
                        "_UseDither as a proof input and refuse the " +
                        "disagreeing curve at its own gate");
                    Assert.That(amuse.SemanticallyRefusedRendererCount,
                        Is.EqualTo(1));
                    Assert.That(amuse.OpaqueCandidateTriangleCount,
                        Is.Zero);
                    Assert.That(amuse.Separation, Is.Null);
                }
            }
            finally
            {
                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// One transparent-only dither scenario: a single-triangle renderer
        /// over the fully-opaque chain, optionally carrying one clip that
        /// animates <c>material._UseDither</c> to 1 against the serialized
        /// default 0, run through the real capture and preparation path
        /// with the family-routing conversion seam.
        /// </summary>
        private sealed class TransparentDitherScenario : IDisposable
        {
            private GameObject root;
            private Mesh mesh;
            private AnimationClip clip;
            private AnimatorController controller;
            private AmusePlatformFinishState amuse;

            internal Material Source { get; private set; }
            internal string SourceDigestBefore { get; private set; }
            internal string SourceDigest { get; private set; }
            internal int OpaqueCandidateTriangleCount { get; private set; }
            internal bool HasAnyRefusal { get; private set; }
            internal int CloneCount { get; private set; }
            internal Material Clone { get; private set; }
            internal string CloneShaderName { get; private set; }
            internal int CloneQueue { get; private set; }
            internal string CloneRenderType { get; private set; }
            internal float[] CloneRecipe { get; private set; }

            internal static TransparentDitherScenario Run(
                LilToonTransparentConversionFixtures fixtures,
                Texture texture,
                bool withBinding)
            {
                var scenario = new TransparentDitherScenario
                {
                    root = new GameObject(
                        "AMUSE transparent dither " +
                        (withBinding ? "bound" : "unbound")),
                    Source = LilToonFixtureTestBase
                        .CreateTransparentConversionMaterial(),
                };
                scenario.Source.SetTexture("_MainTex", texture);
                scenario.root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();

                scenario.mesh = new Mesh
                {
                    vertices = new[]
                    {
                        Vector3.zero,
                        Vector3.right,
                        Vector3.up,
                    },
                };
                scenario.mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
                FillInBoundsUvs(scenario.mesh);

                var renderer =
                    scenario.root.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = scenario.mesh;
                renderer.sharedMaterials = new[] { scenario.Source };

                if (withBinding)
                {
                    scenario.clip = NewFloatClip(
                        scenario.root.name + " clip", string.Empty,
                        "material._UseDither", 1f);
                    scenario.controller = NewController(
                        scenario.root, scenario.root.name + " graph",
                        scenario.clip);
                }

                scenario.SourceDigestBefore =
                    TransparentSourceDigest(scenario.Source);

                scenario.amuse = RunBarrier(
                    scenario.root,
                    selectRequest: VerifiedLilToonTestSeams
                        .SelectVerifiedFixtureRequest,
                    capturer: VerifiedLilToonTestSeams
                        .CaptureVerifiedFixtureMaterials,
                    resolveSemantics: VerifiedLilToonTestSeams
                        .VerifiedAlphaOnly,
                    lilToonConversion: VerifiedFamilyConversion);

                scenario.SourceDigest =
                    TransparentSourceDigest(scenario.Source);
                scenario.OpaqueCandidateTriangleCount =
                    scenario.amuse.OpaqueCandidateTriangleCount;
                scenario.HasAnyRefusal =
                    scenario.amuse.AvatarRefusal !=
                        AvatarAnimationRefusal.None ||
                    scenario.amuse.SemanticallyRefusedRendererCount != 0 ||
                    scenario.amuse.RendererRefusalCount(
                        RendererAnalysisRefusal
                            .UnrecognizedAnimatedMaterialBinding) != 0 ||
                    Enum.GetValues(typeof(AlphaSeparationSlotRefusal))
                        .Cast<AlphaSeparationSlotRefusal>()
                        .Where(reason =>
                            reason != AlphaSeparationSlotRefusal.None)
                        .Any(reason =>
                            scenario.amuse.SlotRefusalCount(reason) != 0);

                if (scenario.amuse.Separation != null)
                {
                    scenario.CloneCount =
                        scenario.amuse.Separation.CreatedClones.Count;
                    if (scenario.amuse.Separation.TryGetOpaque(
                            scenario.Source, out var clone))
                    {
                        scenario.Clone = clone;
                        scenario.CloneShaderName = clone.shader.name;
                        scenario.CloneQueue = clone.renderQueue;
                        scenario.CloneRenderType =
                            clone.GetTag(
                                LilToonOpaqueTarget.RenderTypeTagName,
                                false);
                        scenario.CloneRecipe = LilToonOpaqueTarget
                            .CanonicalOpaqueProperties
                            .Select(recipe => clone.GetFloat(recipe.Property))
                            .ToArray();
                    }
                }

                return scenario;
            }

            public void Dispose()
            {
                DestroyGenerated(amuse);
                if (root != null)
                {
                    DestroyControllerGraph(root, controller);
                }

                if (controller != null)
                {
                    DestroyControllerGraph(controller);
                }

                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                if (Source != null)
                {
                    UnityEngine.Object.DestroyImmediate(Source);
                }

                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Row 20 — locality. A renderer whose transparent slot refuses at
        /// conversion (an eligibility violation the alpha proof does not
        /// touch) or at alpha resolution (an interpretation refusal)
        /// keeps both admitted siblings — the Poiyomi slot and the
        /// lilToon-cutout slot — fully converted, and the refusal never
        /// spreads renderer-wide.
        /// <para>
        /// Falsifies family uncertainty spreading renderer-wide: a barrier
        /// that refuses or drops the whole renderer because one slot's
        /// transparent family could not convert.
        /// </para>
        /// </summary>
        [Test]
        public void RefusedTransparentSlotLeavesItsAdmittedSiblingsConverted()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonTransparentConversionFixtures();
            try
            {
                fixtures.BaseSetUp();
                var opaqueTexture = fixtures.ImportFullyOpaqueMipmap(
                    "transparent_locality");

                // (a) Conversion-stage refusal: the transparent slot's
                // triangle is proven, but eligibility refuses the depth
                // comparison, so only that slot is dropped.
                using (var arm = LocalityArmFixture.Create(
                           fixtures, opaqueTexture,
                           transparent => transparent.SetFloat("_ZTest", 8f)))
                {
                    var amuse = arm.Run();

                    Assert.That(amuse.AvatarRefusal,
                        Is.EqualTo(AvatarAnimationRefusal.None));
                    Assert.That(amuse.SemanticallyRefusedRendererCount,
                        Is.Zero,
                        "a conversion-stage slot refusal must never " +
                        "refuse the renderer");
                    Assert.That(amuse.OpaqueCandidateTriangleCount,
                        Is.EqualTo(3),
                        "fixture precondition: all three slots must " +
                        "prove their triangles before conversion");
                    Assert.That(
                        amuse.SlotRefusalCount(
                            AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused),
                        Is.EqualTo(1),
                        "the transparent slot's eligibility violation " +
                        "must refuse exactly its own slot");
                    Assert.That(amuse.Separation, Is.Not.Null,
                        "the two admitted siblings must still prepare");
                    Assert.That(
                        amuse.Separation.Renderers[0].CandidateSlots,
                        Has.Count.EqualTo(2),
                        "exactly the two admitted sibling slots must " +
                        "survive preparation");
                    Assert.That(
                        amuse.Separation.TryGetOpaque(
                            arm.PoiyomiMaterial, out _),
                        Is.True, "the Poiyomi sibling must convert");
                    Assert.That(
                        amuse.Separation.TryGetOpaque(
                            arm.CutoutMaterial, out _),
                        Is.True, "the lilToon-cutout sibling must convert");
                    Assert.That(
                        amuse.Separation.TryGetOpaque(
                            arm.TransparentMaterial, out _),
                        Is.False,
                        "the refused transparent slot must map nothing");
                    Assert.That(amuse.Separation.CreatedClones,
                        Has.Count.EqualTo(2),
                        "exactly the two admitted siblings may clone");
                }

                // (b) Alpha-resolution refusal: the transparent slot's
                // interpretation is Unknown, which unproves only its own
                // triangles while the siblings resolve, classify, convert
                // and prepare.
                using (var arm = LocalityArmFixture.Create(
                           fixtures, opaqueTexture,
                           transparent => transparent.SetFloat(
                               "_SubpassCutoff", 2f)))
                {
                    var amuse = arm.Run();

                    Assert.That(amuse.AvatarRefusal,
                        Is.EqualTo(AvatarAnimationRefusal.None));
                    Assert.That(amuse.SemanticallyRefusedRendererCount,
                        Is.Zero,
                        "a slot-local interpretation refusal must never " +
                        "refuse the renderer while a sibling slot " +
                        "resolves");
                    Assert.That(amuse.AnalyzedRendererCount,
                        Is.EqualTo(1),
                        "the renderer must still be analyzed over its " +
                        "resolving slots");
                    Assert.That(amuse.OpaqueCandidateTriangleCount,
                        Is.EqualTo(2),
                        "exactly the two sibling triangles may prove " +
                        "opaque; the Unknown transparent triangle must " +
                        "not become ProvenOpaque");
                    Assert.That(amuse.Separation, Is.Not.Null);
                    Assert.That(
                        amuse.Separation.Renderers[0].CandidateSlots,
                        Has.Count.EqualTo(2));
                    Assert.That(
                        amuse.Separation.TryGetOpaque(
                            arm.PoiyomiMaterial, out _),
                        Is.True);
                    Assert.That(
                        amuse.Separation.TryGetOpaque(
                            arm.CutoutMaterial, out _),
                        Is.True);
                    Assert.That(
                        amuse.Separation.TryGetOpaque(
                            arm.TransparentMaterial, out _),
                        Is.False,
                        "the all-Unknown transparent outcome must not " +
                        "prepare");
                }
            }
            finally
            {
                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// One three-slot locality arm: a Poiyomi slot, a lilToon-cutout
        /// slot and a configurable transparent slot over one mesh, run
        /// through the real barrier with the family-routing conversion
        /// seam.
        /// </summary>
        private sealed class LocalityArmFixture : IDisposable
        {
            private readonly LilToonTransparentConversionFixtures fixtures;
            private GameObject root;
            private Mesh mesh;
            private AmusePlatformFinishState amuse;

            internal Material PoiyomiMaterial { get; private set; }
            internal Material CutoutMaterial { get; private set; }
            internal Material TransparentMaterial { get; private set; }

            private LocalityArmFixture(
                LilToonTransparentConversionFixtures owner)
            {
                fixtures = owner;
            }

            internal static LocalityArmFixture Create(
                LilToonTransparentConversionFixtures fixtures,
                Texture opaqueTexture,
                Action<Material> configureTransparent)
            {
                var arm = new LocalityArmFixture(fixtures)
                {
                    root = new GameObject("AMUSE transparent locality"),
                    PoiyomiMaterial = VerifiedOpaqueMaterial(),
                    CutoutMaterial =
                        LilToonFixtureTestBase.CreateCutoutConversionMaterial(),
                    TransparentMaterial =
                        LilToonFixtureTestBase
                            .CreateTransparentConversionMaterial(),
                };
                arm.root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                arm.CutoutMaterial.SetTexture("_MainTex", opaqueTexture);
                arm.TransparentMaterial.SetTexture(
                    "_MainTex", opaqueTexture);
                configureTransparent(arm.TransparentMaterial);

                arm.mesh = new Mesh
                {
                    vertices = new[]
                    {
                        Vector3.zero,
                        Vector3.right,
                        Vector3.up,
                        new Vector3(2f, 0f, 0f),
                        new Vector3(3f, 0f, 0f),
                        new Vector3(2f, 1f, 0f),
                        new Vector3(4f, 0f, 0f),
                        new Vector3(5f, 0f, 0f),
                        new Vector3(4f, 1f, 0f),
                    },
                    subMeshCount = 3,
                };
                arm.mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
                arm.mesh.SetTriangles(new[] { 3, 4, 5 }, 1);
                arm.mesh.SetTriangles(new[] { 6, 7, 8 }, 2);
                FillInBoundsUvs(arm.mesh);

                var renderer = arm.root.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = arm.mesh;
                renderer.sharedMaterials = new[]
                {
                    arm.PoiyomiMaterial,
                    arm.CutoutMaterial,
                    arm.TransparentMaterial,
                };

                return arm;
            }

            internal AmusePlatformFinishState Run()
            {
                amuse = RunBarrier(
                    root,
                    selectRequest: VerifiedLilToonTestSeams
                        .SelectVerifiedFixtureRequest,
                    capturer: VerifiedLilToonTestSeams
                        .CaptureVerifiedFixtureMaterials,
                    resolveSemantics: VerifiedLilToonTestSeams
                        .VerifiedAlphaOnly,
                    lilToonConversion: VerifiedFamilyConversion);
                return amuse;
            }

            public void Dispose()
            {
                DestroyGenerated(amuse);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                if (PoiyomiMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(PoiyomiMaterial);
                }

                if (CutoutMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(CutoutMaterial);
                }

                if (TransparentMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(TransparentMaterial);
                }

                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Row 20 — an all-<c>Unknown</c> transparent outcome never becomes
        /// <c>ProvenOpaque</c>, for each named cause: an unsupported texture
        /// format, streamed mips, a texture whose alpha chain cannot be
        /// produced (missing readback), a degenerate triangle, a NaN UV,
        /// and a support-region overflow. Nothing is proven, so nothing is
        /// retained and nothing could ever be applied from these slots.
        /// </summary>
        [Test]
        public void TransparentAllUnknownOutcomesNeverBecomeProvenOpaque()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonTransparentConversionFixtures();
            var renderTexture = new RenderTexture(4, 4, 0);
            try
            {
                fixtures.BaseSetUp();

                var regionOverflowTexture =
                    fixtures.ImportMostlyOpaqueMipmapWithOneLesserTexel(
                        "transparent_region_overflow");
                var standardTexture = fixtures.ImportFullyOpaqueMipmap(
                    "transparent_unknown_causes");

                var cases = new (string Label, Texture Texture,
                    Action<Mesh> ConfigureMesh)[]
                {
                    ("unsupported-format",
                        fixtures.ImportUnsupportedFormatTexture(
                            "transparent_unsupported_format"),
                        null),
                    ("streamed-mips",
                        fixtures.ImportStreamingMipmap(
                            "transparent_streaming"),
                        null),
                    ("missing-readback", renderTexture, null),
                    ("degenerate-triangle", standardTexture,
                        mesh =>
                        {
                            mesh.vertices = new[]
                            {
                                Vector3.zero,
                                Vector3.right,
                                new Vector3(2f, 0f, 0f),
                            };
                        }),
                    ("nan-uv", standardTexture,
                        mesh =>
                        {
                            var uv = mesh.uv;
                            uv[0] = new Vector2(float.NaN, 0f);
                            mesh.uv = uv;
                        }),
                    ("region-overflow", regionOverflowTexture,
                        mesh =>
                        {
                            mesh.uv = new[]
                            {
                                new Vector2(0f, 0f),
                                new Vector2(31.9375f, 0f),
                                new Vector2(0f, 31.9375f),
                            };
                        }),
                };

                foreach (var unknownCase in cases)
                {
                    var root = new GameObject(
                        "AMUSE transparent unknown " + unknownCase.Label);
                    root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                    Material material = null;
                    Mesh mesh = null;
                    AmusePlatformFinishState amuse = null;
                    try
                    {
                        material =
                            LilToonFixtureTestBase
                                .CreateTransparentConversionMaterial();
                        material.SetTexture("_MainTex", unknownCase.Texture);
                        var renderer = AddSingleTriangleRenderer(
                            root, material, out mesh);
                        FillInBoundsUvs(mesh);
                        unknownCase.ConfigureMesh?.Invoke(mesh);

                        amuse = RunBarrier(
                            root,
                            selectRequest: VerifiedLilToonTestSeams
                                .SelectVerifiedFixtureRequest,
                            capturer: VerifiedLilToonTestSeams
                                .CaptureVerifiedFixtureMaterials,
                            resolveSemantics: VerifiedLilToonTestSeams
                                .VerifiedAlphaOnly,
                            lilToonConversion: VerifiedFamilyConversion);

                        Assert.That(
                            amuse.OpaqueCandidateTriangleCount, Is.Zero,
                            unknownCase.Label +
                            ": an all-Unknown transparent outcome must " +
                            "never become ProvenOpaque");
                        // Where the evidence-level refusals land (the
                        // capture's format/sampler gates or the slot's
                        // resolution) can vary across Unity versions, so
                        // the assertion mirrors the cutout twin: the
                        // uncertainty may refuse resolution or analyze to
                        // no candidate, but never prove opaque.
                        var unknownRefusals = amuse.RendererRefusalCount(
                            RendererAnalysisRefusal
                                .AdmittedMaterialSemanticsUnknown);
                        Assert.That(
                            unknownRefusals,
                            Is.EqualTo(0).Or.EqualTo(1),
                            unknownCase.Label +
                            ": unsupported evidence may refuse resolution " +
                            "or analyze to no candidate, but never prove " +
                            "opaque");
                        if (unknownRefusals == 0)
                        {
                            Assert.That(amuse.AnalyzedRendererCount,
                                Is.EqualTo(1), unknownCase.Label);
                        }
                        Assert.That(amuse.Separation, Is.Null,
                            unknownCase.Label +
                            ": nothing may be retained for an unproven " +
                            "slot, so nothing can ever be applied from it");
                    }
                    finally
                    {
                        DestroyGenerated(amuse);
                        if (mesh != null)
                        {
                            UnityEngine.Object.DestroyImmediate(mesh);
                        }

                        UnityEngine.Object.DestroyImmediate(root);
                        if (material != null)
                        {
                            UnityEngine.Object.DestroyImmediate(material);
                        }
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(renderTexture);
                fixtures.BaseTearDown();
            }
        }

        // --- Task 6 transparent helpers ---------------------------------------

        private const string TransparentCloneContractTempFolder =
            "Assets/AmuseTests_AlphaCloneContract";

        private const string TransparentCloneContractMissingShaderName =
            "Hidden/Alrauna/AmuseTests/TransparentCloneContractMissingBlendOpFA";

        /// <summary>
        /// Writes and imports the clone-contract target stand-in: the
        /// canonical opaque recipe's properties minus
        /// <c>_BlendOpFA</c>, so the shader-level property check fires
        /// before any clone exists.
        /// </summary>
        private static Shader ImportShaderWithoutRecipeProperty()
        {
            if (!AssetDatabase.IsValidFolder(
                    TransparentCloneContractTempFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", "AmuseTests_AlphaCloneContract");
            }

            var path = TransparentCloneContractTempFolder +
                       "/TransparentCloneContractMissingBlendOpFA.shader";
            File.WriteAllText(
                path,
                "Shader \"" +
                TransparentCloneContractMissingShaderName + "\"\n" +
                "{\n" +
                "    Properties\n" +
                "    {\n" +
                "        _Cutoff (\"Cutoff\", Range(0,1)) = 0.5\n" +
                "        _SrcBlend (\"SrcBlend\", Float) = 1\n" +
                "        _DstBlend (\"DstBlend\", Float) = 0\n" +
                "        _AlphaToMask (\"AlphaToMask\", Float) = 0\n" +
                "        _ZWrite (\"ZWrite\", Float) = 1\n" +
                "        _ZTest (\"ZTest\", Float) = 4\n" +
                "        _OffsetFactor (\"OffsetFactor\", Float) = 0\n" +
                "        _OffsetUnits (\"OffsetUnits\", Float) = 0\n" +
                "        _ColorMask (\"ColorMask\", Float) = 15\n" +
                "        _SrcBlendAlpha (\"SrcBlendAlpha\", Float) = 1\n" +
                "        _DstBlendAlpha (\"DstBlendAlpha\", Float) = 10\n" +
                "        _BlendOp (\"BlendOp\", Float) = 0\n" +
                "        _BlendOpAlpha (\"BlendOpAlpha\", Float) = 0\n" +
                "        _SrcBlendFA (\"SrcBlendFA\", Float) = 1\n" +
                "        _DstBlendFA (\"DstBlendFA\", Float) = 1\n" +
                "        _SrcBlendAlphaFA (\"SrcBlendAlphaFA\", Float) = 0\n" +
                "        _DstBlendAlphaFA (\"DstBlendAlphaFA\", Float) = 1\n" +
                "        // _BlendOpFA deliberately absent.\n" +
                "    }\n" +
                "\n" +
                "    SubShader\n" +
                "    {\n" +
                "        Tags { \"RenderType\" = \"Opaque\" }\n" +
                "        Pass\n" +
                "        {\n" +
                "            CGPROGRAM\n" +
                "            #pragma vertex vert\n" +
                "            #pragma fragment frag\n" +
                "            #include \"UnityCG.cginc\"\n" +
                "            float4 vert(float4 vertex : POSITION) : " +
                "SV_POSITION\n" +
                "            { return UnityObjectToClipPos(vertex); }\n" +
                "            fixed4 frag() : SV_Target\n" +
                "            { return fixed4(1, 1, 1, 1); }\n" +
                "            ENDCG\n" +
                "        }\n" +
                "    }\n" +
                "}\n");
            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);

            var shader = Shader.Find(
                TransparentCloneContractMissingShaderName);
            Assert.That(
                shader, Is.Not.Null,
                "The clone-contract target stand-in must import.");
            return shader;
        }

        /// <summary>
        /// Reads back the whole prepared-clone contract on a generated
        /// transparent-source clone: the attested opaque stand-in target,
        /// the eighteen canonical recipe scalars, queue 2000,
        /// <c>RenderType=Opaque</c>, and no non-canonical fact anywhere.
        /// </summary>
        private static void AssertPreparedCloneCarriesTheCanonicalOpaqueTarget(
            Material clone)
        {
            foreach (var (property, value) in
                         LilToonOpaqueTarget.CanonicalOpaqueProperties)
            {
                Assert.That(
                    clone.GetFloat(property), Is.EqualTo(value),
                    "canonical recipe '" + property + "'");
            }

            Assert.That(
                clone.renderQueue,
                Is.EqualTo(
                    LilToonOpaqueTarget.CanonicalOpaqueRenderQueue),
                "the clone must carry the canonical opaque queue");
            Assert.That(
                clone.GetTag(
                    LilToonOpaqueTarget.RenderTypeTagName, false),
                Is.EqualTo(
                    LilToonOpaqueTarget.CanonicalOpaqueRenderType),
                "the clone must carry the canonical opaque RenderType");
            Assert.That(
                LilToonOpaqueTarget.TryFindNonCanonicalFact(clone, out _),
                Is.False,
                "every canonical fact must read back on the clone");
            Assert.That(
                clone.shader,
                Is.SameAs(Shader.Find(LilToonFixtureNames.OpaqueTarget)),
                "the clone must carry the attested opaque stand-in " +
                "target");
        }

        /// <summary>
        /// The observed facts of a transparent source material — the state
        /// a plausible conversion could falsify: identity, shader, queue,
        /// RenderType, tint, the theorem and boost scalars, the distance
        /// fade, and the assigned main texture.
        /// </summary>
        private static string TransparentSourceDigest(Material material)
        {
            var culture = CultureInfo.InvariantCulture;
            var parts = new List<string>
            {
                material.name,
                material.shader.name,
                material.renderQueue.ToString(culture),
                material.GetTag("RenderType", false),
                material.mainTexture == null
                    ? "<none>"
                    : material.mainTexture.GetInstanceID().ToString(culture),
                string.Join(
                    ",",
                    material.GetColor("_Color").r.ToString("R", culture),
                    material.GetColor("_Color").g.ToString("R", culture),
                    material.GetColor("_Color").b.ToString("R", culture),
                    material.GetColor("_Color").a.ToString("R", culture)),
                material.GetFloat("_Cutoff").ToString("R", culture),
                material.GetFloat("_AlphaBoostFA").ToString("R", culture),
                material.GetFloat("_SubpassCutoff").ToString("R", culture),
                material.GetVector("_DistanceFade").x.ToString("R", culture),
                material.GetVector("_DistanceFade").y.ToString("R", culture),
                material.GetVector("_DistanceFade").z.ToString("R", culture),
                material.GetVector("_DistanceFade").w.ToString("R", culture),
            };
            return string.Join("|", parts);
        }

        /// <summary>
        /// One transparent conversion arm: a transient single-triangle
        /// renderer fixture over a caller-owned texture plus the barrier
        /// run over it, mirroring <see cref="CutoutArmFixture"/> with the
        /// transparent stand-in material and the family-routing conversion
        /// seam.
        /// </summary>
        private sealed class TransparentArmFixture : IDisposable
        {
            private GameObject root;
            private Mesh mesh;
            private AnimationClip clip;
            private AnimatorController controller;
            private AmusePlatformFinishState amuse;

            private TransparentArmFixture() { }

            internal Material Material { get; private set; }

            internal static TransparentArmFixture Create(
                Texture mainTex,
                string rootName,
                Action<Material> configure,
                string animatedBinding,
                float? animatedValue)
            {
                var fixture = new TransparentArmFixture
                {
                    root = new GameObject(rootName),
                    Material = LilToonFixtureTestBase
                        .CreateTransparentConversionMaterial(),
                };
                fixture.root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                fixture.Material.SetTexture("_MainTex", mainTex);
                configure?.Invoke(fixture.Material);

                fixture.mesh = new Mesh
                {
                    vertices = new[]
                    {
                        Vector3.zero,
                        Vector3.right,
                        Vector3.up,
                    },
                };
                fixture.mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
                FillInBoundsUvs(fixture.mesh);

                var renderer =
                    fixture.root.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = fixture.mesh;
                renderer.sharedMaterials = new[] { fixture.Material };

                if (animatedBinding != null)
                {
                    fixture.clip = NewFloatClip(
                        rootName + " clip", string.Empty,
                        animatedBinding, animatedValue ?? 0f);
                    fixture.controller = NewController(
                        fixture.root, rootName + " graph", fixture.clip);
                }

                return fixture;
            }

            internal AmusePlatformFinishState Run()
            {
                amuse = RunBarrier(
                    root,
                    selectRequest: VerifiedLilToonTestSeams
                        .SelectVerifiedFixtureRequest,
                    capturer: VerifiedLilToonTestSeams
                        .CaptureVerifiedFixtureMaterials,
                    resolveSemantics: VerifiedLilToonTestSeams
                        .VerifiedAlphaOnly,
                    lilToonConversion: VerifiedFamilyConversion);
                return amuse;
            }

            public void Dispose()
            {
                DestroyGenerated(amuse);
                if (root != null)
                {
                    DestroyControllerGraph(root, controller);
                }

                if (controller != null)
                {
                    DestroyControllerGraph(controller);
                }

                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                if (Material != null)
                {
                    UnityEngine.Object.DestroyImmediate(Material);
                }

                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Texture importer for the transparent conversion fixtures. The
        /// importers are family-agnostic — schema, format and sampler
        /// vocabulary only — so this reuses the cutout fixture class under
        /// the transparent name and adds the two transparent-only fixtures:
        /// the region-overflow chain and the unsupported-format asset.
        /// </summary>
        private sealed class LilToonTransparentConversionFixtures
            : LilToonCutoutConversionFixtures
        {
            internal Texture2D ImportMostlyOpaqueMipmapWithOneLesserTexel(
                string name)
            {
                const int size = 8;
                var pixels = new Color32[size * size];
                for (var index = 0; index < pixels.Length; index++)
                {
                    pixels[index] = new Color32(255, 255, 255, 255);
                }

                // One texel below full opacity: the chain is neither fully
                // opaque nor fully non-opaque, so classification reaches
                // the support-region budget instead of short-circuiting.
                pixels[0] = new Color32(255, 255, 255, 254);
                return ImportMipmapTexture(
                    name,
                    size,
                    size,
                    pixels,
                    FilterMode.Point,
                    UnityEngine.TextureWrapMode.Repeat);
            }

            internal Texture2D ImportUnsupportedFormatTexture(string name)
            {
                // Directly allocatable in a format outside the alpha
                // evidence's closed allowlist, producing no console error:
                // the producer refuses it at the format gate, before any
                // GPU work.
                var texture = new Texture2D(
                    8, 8, TextureFormat.ARGB4444, false);
                var path = TempFolder + "/" + name + ".asset";
                AssetDatabase.CreateAsset(texture, path);
                var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(
                    loaded, Is.Not.Null,
                    $"Imported texture '{path}' must load.");
                Assert.That(
                    loaded.format, Is.EqualTo(TextureFormat.ARGB4444),
                    "fixture precondition: the format must be outside " +
                    "the alpha evidence allowlist");
                return loaded;
            }
        }


        private static int LoadedMaterialCount()
        {
            return Resources.FindObjectsOfTypeAll<Material>().Length;
        }
        private static readonly string[] ExpectedLilToonCutoutAlphaScalars =
        {
            "_lilToonVersion",
            "_Invisible",
            "_UDIMDiscardCompile",
            "_UDIMDiscardMode",
            "_ShiftBackfaceUV",
            "_UseParallax",
            "_UseMain2ndTex",
            "_UseMain3rdTex",
            "_AlphaMaskMode",
            "_UseDither",
            "_IDMask1",
            "_IDMask2",
            "_IDMask3",
            "_IDMask4",
            "_IDMask5",
            "_IDMask6",
            "_IDMask7",
            "_IDMask8",
            "_IDMaskControlsDissolve",
            "_Cutoff",
        };

        private static readonly string[] ExpectedLilToonCanonicalOpaqueScalars =
        {
            "_SrcBlend",
            "_DstBlend",
            "_AlphaToMask",
            "_ZWrite",
            "_ZTest",
            "_OffsetFactor",
            "_OffsetUnits",
            "_ColorMask",
            "_SrcBlendAlpha",
            "_DstBlendAlpha",
            "_BlendOp",
            "_BlendOpAlpha",
            "_SrcBlendFA",
            "_DstBlendFA",
            "_SrcBlendAlphaFA",
            "_DstBlendAlphaFA",
            "_BlendOpFA",
            "_BlendOpAlphaFA",
        };

        private static IReadOnlyList<(
            string Label,
            string Binding,
            float Serialized,
            float Refused,
            bool AlphaRelevant)> CutoutAnimatedPropertyCases(
                Material material)
        {
            var cases = new List<(
                string Label,
                string Binding,
                float Serialized,
                float Refused,
                bool AlphaRelevant)>();

            foreach (var property in ExpectedLilToonCutoutAlphaScalars)
            {
                var serialized = material.GetFloat(property);
                cases.Add((
                    property,
                    "material." + property,
                    serialized,
                    serialized + 1f,
                    true));
            }

            var colorAlpha = material.GetColor("_Color").a;
            cases.Add((
                "_Color.a",
                "material._Color.a",
                colorAlpha,
                colorAlpha + 1f,
                true));

            AddVectorCases(
                cases,
                "_DissolveParams",
                material.GetVector("_DissolveParams"));
            AddVectorCases(
                cases,
                "_MainTex_ScrollRotate",
                material.GetVector("_MainTex_ScrollRotate"));

            var scale = material.GetTextureScale("_MainTex");
            var offset = material.GetTextureOffset("_MainTex");
            AddComponentCase(cases, "_MainTex_ST", "x", scale.x);
            AddComponentCase(cases, "_MainTex_ST", "y", scale.y);
            AddComponentCase(cases, "_MainTex_ST", "z", offset.x);
            AddComponentCase(cases, "_MainTex_ST", "w", offset.y);

            foreach (var property in ExpectedLilToonCanonicalOpaqueScalars)
            {
                var serialized = material.GetFloat(property);
                cases.Add((
                    property,
                    "material." + property,
                    serialized,
                    serialized + 1f,
                    false));
            }

            return cases;
        }

        private static void AddVectorCases(
            ICollection<(
                string Label,
                string Binding,
                float Serialized,
                float Refused,
                bool AlphaRelevant)> cases,
            string property,
            Vector4 value)
        {
            var suffixes = new[] { "x", "y", "z", "w" };
            for (var component = 0; component < suffixes.Length; component++)
            {
                AddComponentCase(
                    cases, property, suffixes[component], value[component]);
            }
        }

        private static void AddComponentCase(
            ICollection<(
                string Label,
                string Binding,
                float Serialized,
                float Refused,
                bool AlphaRelevant)> cases,
            string property,
            string suffix,
            float serialized)
        {
            cases.Add((
                property + "." + suffix,
                "material." + property + "." + suffix,
                serialized,
                serialized + 1f,
                true));
        }


        /// <summary>
        /// The cutout conversion-only recipe animation (controlling
        /// falsifier 3): the runtime-overwrite rule fires against the
        /// cutout family's own canonical recipe before the conversion step
        /// runs, and an animation agreeing with the canonical value admits.
        /// <para>
        /// Falsifies: a cutout routing that skips the overwrite rule (a
        /// clone would be prepared for a recipe provably overwritten at
        /// runtime), an ordering that runs the conversion before the rule
        /// (the counting seam must never be invoked on the refusing arm),
        /// and an admission that reads the live _ZWrite instead of the
        /// captured serialized value (which would misjudge every arm).
        /// </para>
        /// </summary>
        [Test]
        public void
            CutoutConversionRecipeAnimationRefusesWhenOverwrittenAndPreparesAtCanonical()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonCutoutConversionFixtures();
            try
            {
                fixtures.BaseSetUp();
                var texture = fixtures.ImportFullyOpaqueMipmap(
                    "cutout_zwrite");

                // (a) Serialized away from canonical and animated at that
                // serialized value: admission succeeds, but the recipe
                // AMUSE would write is provably overwritten at runtime.
                using (var arm = CutoutArmFixture.Create(
                           texture,
                           "AMUSE cutout zwrite overwritten",
                           material => material.SetFloat("_ZWrite", 0f),
                           "material._ZWrite",
                           0f))
                {
                    Assert.That(arm.Material.GetFloat("_ZWrite"),
                        Is.EqualTo(0f),
                        "fixture precondition: the serialized _ZWrite " +
                        "default must be 0, so animating it to 0 admits " +
                        "while violating the canonical 1");

                    var conversionInvocations = 0;
                    var amuse = arm.Run(
                        (Material live, CapturedMaterialEvidence derived,
                         Material preparedOpaque, out Material opaque,
                         out LilToonOpaqueConversionRefusal refusal) =>
                        {
                            conversionInvocations++;
                            opaque = null;
                            refusal =
                                LilToonOpaqueConversionRefusal.None;
                            return false;
                        });

                    Assert.That(amuse.AvatarRefusal,
                        Is.EqualTo(AvatarAnimationRefusal.None));
                    Assert.That(amuse.SemanticallyRefusedRendererCount,
                        Is.Zero);
                    Assert.That(amuse.OpaqueCandidateTriangleCount,
                        Is.EqualTo(1),
                        "fixture precondition: the slot must be a " +
                        "candidate, or the overwrite refusal proves " +
                        "nothing");
                    Assert.That(
                        amuse.SlotRefusalCount(
                            AlphaSeparationSlotRefusal
                                .ConversionPropertyOverwrittenAtRuntime),
                        Is.EqualTo(1),
                        "a recipe property animated at a non-canonical " +
                        "serialized value must refuse the slot");
                    Assert.That(conversionInvocations, Is.Zero,
                        "the overwrite rule must be validated before the " +
                        "conversion step runs, so no conversion boundary " +
                        "call may happen for a slot already known to " +
                        "violate the recipe");
                    Assert.That(amuse.Separation, Is.Null,
                        "the only candidate slot was refused, so nothing " +
                        "is retained");
                }

                // (b) Animated exactly at the canonical value: the slot
                // prepares and converts.
                using (var arm = CutoutArmFixture.Create(
                           texture,
                           "AMUSE cutout zwrite at canonical",
                           null,
                           "material._ZWrite",
                           1f))
                {
                    var amuse = arm.Run();

                    Assert.That(amuse.AvatarRefusal,
                        Is.EqualTo(AvatarAnimationRefusal.None));
                    Assert.That(amuse.SemanticallyRefusedRendererCount,
                        Is.Zero);
                    Assert.That(amuse.OpaqueCandidateTriangleCount,
                        Is.EqualTo(1));
                    foreach (AlphaSeparationSlotRefusal reason in Enum
                                 .GetValues(
                                     typeof(AlphaSeparationSlotRefusal)))
                    {
                        if (reason == AlphaSeparationSlotRefusal.None)
                        {
                            continue;
                        }

                        Assert.That(
                            amuse.SlotRefusalCount(reason), Is.Zero,
                            "animation at the canonical value must " +
                            "prepare: " + reason);
                    }

                    Assert.That(amuse.Separation, Is.Not.Null);
                    Assert.That(amuse.Separation.CreatedClones,
                        Has.Count.EqualTo(1));
                }

                // (c) Serialized at the canonical value and animated away
                // from it: the singleton admission itself refuses — the
                // animated set and the serialized default disagree — which
                // is the non-singleton arm, not the overwrite rule.
                using (var arm = CutoutArmFixture.Create(
                           texture,
                           "AMUSE cutout zwrite non-singleton",
                           null,
                           "material._ZWrite",
                           0f))
                {
                    var amuse = arm.Run();

                    Assert.That(amuse.Separation, Is.Null);
                    Assert.That(
                        amuse.SlotRefusalCount(
                            AlphaSeparationSlotRefusal
                                .ConversionStateNotAdmitted),
                        Is.EqualTo(1),
                        "an animated value disagreeing with the " +
                        "serialized default refuses at conversion " +
                        "admission, before any overwrite question");
                    Assert.That(
                        amuse.SlotRefusalCount(
                            AlphaSeparationSlotRefusal
                                .ConversionPropertyOverwrittenAtRuntime),
                        Is.Zero,
                        "the overwrite rule speaks only about admitted " +
                        "singletons");
                }
            }
            finally
            {
                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// Avatar-wide deduplication of one cutout source across two
        /// renderers, in both renderer orders: exactly one clone exists and
        /// every prepared slot maps to it by reference.
        /// <para>
        /// Falsifies a per-renderer (or per-slot) conversion cache that
        /// prepares one clone per renderer, and an ordering-dependent
        /// registration that forks the avatar-wide mapping when the
        /// hierarchy order flips.
        /// </para>
        /// </summary>
        [Test]
        public void CutoutSharedSourceReusesOneAvatarWideClone()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonCutoutConversionFixtures();

            // Each variant is a complete build over its own avatar, so the
            // renderer order the barrier sees is the child creation order.
            void RunVariant(string rootName, string firstChild,
                            string secondChild)
            {
                var root = new GameObject(rootName);
                root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                Material material = null;
                Mesh meshFirst = null;
                Mesh meshSecond = null;
                AmusePlatformFinishState amuse = null;
                try
                {
                    material = LilToonFixtureTestBase.CreateCutoutConversionMaterial();
                    material.SetTexture(
                        "_MainTex",
                        fixtures.ImportFullyOpaqueMipmap(
                            "cutout_dedup_" + firstChild));
                    AddNamedChildRenderer(
                        root, firstChild, material, out meshFirst);
                    AddNamedChildRenderer(
                        root, secondChild, material, out meshSecond);
                    FillInBoundsUvs(meshFirst);
                    FillInBoundsUvs(meshSecond);

                    amuse = RunBarrier(root);

                    Assert.That(amuse.AvatarRefusal,
                        Is.EqualTo(AvatarAnimationRefusal.None));
                    Assert.That(amuse.SemanticallyRefusedRendererCount,
                        Is.Zero,
                        "fixture precondition: both renderers must be " +
                        "analyzable");
                    Assert.That(amuse.OpaqueCandidateTriangleCount,
                        Is.EqualTo(2),
                        "fixture precondition: both slots must prove " +
                        "their triangle opaque, or the deduplication " +
                        "proves nothing");
                    Assert.That(amuse.Separation, Is.Not.Null);
                    Assert.That(amuse.Separation.Renderers,
                        Has.Count.EqualTo(2),
                        "fixture precondition: both renderers must be " +
                        "retained, or the shared mapping proves nothing");

                    Assert.That(amuse.Separation.CreatedClones,
                        Has.Count.EqualTo(1),
                        "two renderers proven against one cutout source " +
                        "material must share one avatar-wide clone");
                    var clone = amuse.Separation.CreatedClones[0];
                    Assert.That(
                        amuse.Separation.OpaqueBySource[material],
                        Is.SameAs(clone),
                        "the avatar-wide mapping must hold the shared " +
                        "clone");
                    foreach (var prepared in amuse.Separation.Renderers)
                    {
                        Assert.That(
                            prepared.CandidateSlots.Single()
                                .OpaqueOfAdmitted[material],
                            Is.SameAs(clone),
                            "every renderer's slot mapping must reference " +
                            "the shared clone, never a local duplicate");
                    }
                }
                finally
                {
                    DestroyGenerated(amuse);
                    if (meshFirst != null)
                        UnityEngine.Object.DestroyImmediate(meshFirst);
                    if (meshSecond != null)
                        UnityEngine.Object.DestroyImmediate(meshSecond);
                    UnityEngine.Object.DestroyImmediate(root);
                    if (material != null)
                        UnityEngine.Object.DestroyImmediate(material);
                }
            }

            try
            {
                fixtures.BaseSetUp();
                RunVariant(
                    "AMUSE cutout dedup forward", "first", "second");
                RunVariant(
                    "AMUSE cutout dedup reversed", "second", "first");
            }
            finally
            {
                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// A refused sibling renderer — a schema-complete cutout whose
        /// effective depth-write state is unsupported — must not corrupt the
        /// convertible renderer's preparation: its refusal is slot-scoped,
        /// its source never enters the avatar-wide mapping, and the
        /// convertible renderer still prepares and converts.
        /// <para>
        /// Falsifies: a dedup that registers an entry for the refused
        /// source, a refusal that poisons the whole avatar, and a cutout with
        /// unsupported effective render state that silently converts anyway.
        /// </para>
        /// </summary>
        [Test]
        public void
            CutoutRefusedSiblingLeavesTheConvertibleRendererUncorrupted()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonCutoutConversionFixtures();
            var root = new GameObject("AMUSE cutout refused sibling");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material convertible = null;
            Material bareCutout = null;
            Mesh convertibleMesh = null;
            Mesh bareMesh = null;
            AmusePlatformFinishState amuse = null;

            try
            {
                fixtures.BaseSetUp();
                var texture =
                    fixtures.ImportFullyOpaqueMipmap("cutout_refusal");

                convertible = LilToonFixtureTestBase.CreateCutoutConversionMaterial();
                convertible.SetTexture("_MainTex", texture);
                AddNamedChildRenderer(
                    root, "convertible", convertible, out convertibleMesh);
                FillInBoundsUvs(convertibleMesh);

                bareCutout = LilToonFixtureTestBase
                    .CreateCutoutConversionMaterial();
                bareCutout.SetTexture("_MainTex", texture);
                bareCutout.SetFloat("_ZWrite", 0f);
                AddNamedChildRenderer(
                    root, "bare", bareCutout, out bareMesh);
                FillInBoundsUvs(bareMesh);

                amuse = RunBarrier(root);

                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(amuse.SemanticallyRefusedRendererCount,
                    Is.Zero,
                    "fixture precondition: both renderers must analyze — " +
                    "the unsupported depth-write state is refused by " +
                    "conversion, not by alpha analysis");
                Assert.That(amuse.OpaqueCandidateTriangleCount,
                    Is.EqualTo(2),
                    "fixture precondition: both slots must produce " +
                    "opaque candidates, so the refusal is " +
                    "conversion-owned");
                Assert.That(
                    amuse.SlotRefusalCount(
                        AlphaSeparationSlotRefusal.OpaqueConversionRefused),
                    Is.EqualTo(1),
                    "exactly the unsupported cutout slot may refuse");
                foreach (AlphaSeparationSlotRefusal reason in Enum.GetValues(
                             typeof(AlphaSeparationSlotRefusal)))
                {
                    if (reason == AlphaSeparationSlotRefusal.None ||
                        reason == AlphaSeparationSlotRefusal
                            .OpaqueConversionRefused)
                    {
                        continue;
                    }

                    Assert.That(
                        amuse.SlotRefusalCount(reason), Is.Zero,
                        "no other reason may be recorded: " + reason);
                }

                Assert.That(amuse.Separation, Is.Not.Null,
                    "the convertible renderer must still prepare");
                Assert.That(amuse.Separation.Renderers,
                    Has.Count.EqualTo(1));
                Assert.That(
                    amuse.Separation.Renderers[0].RendererPath,
                    Is.EqualTo("convertible"),
                    "only the convertible renderer may be retained");
                Assert.That(amuse.Separation.CreatedClones,
                    Has.Count.EqualTo(1));
                Assert.That(
                    amuse.Separation.TryGetOpaque(
                        convertible, out var opaque),
                    Is.True,
                    "the convertible source must carry its mapping");
                Assert.That(opaque, Is.Not.Null);
                Assert.That(
                    amuse.Separation.TryGetOpaque(bareCutout, out _),
                    Is.False,
                    "a refused slot's source must never enter the " +
                    "avatar-wide mapping");
            }
            finally
            {
                DestroyGenerated(amuse);
                if (convertibleMesh != null)
                    UnityEngine.Object.DestroyImmediate(convertibleMesh);
                if (bareMesh != null)
                    UnityEngine.Object.DestroyImmediate(bareMesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (convertible != null)
                    UnityEngine.Object.DestroyImmediate(convertible);
                if (bareCutout != null)
                    UnityEngine.Object.DestroyImmediate(bareCutout);

                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// Sibling slots of two conversion families convert independently:
        /// the Poiyomi slot and the cutout slot each prepare, each map
        /// through their own conversion, and the clone registration order
        /// names them deterministically.
        /// <para>
        /// Falsifies: a single-family conversion branch that refuses or
        /// drops the sibling slot, clones that collide across families (one
        /// clone reused for both sources), and clone-order corruption in
        /// the " (AMUSE Opaque n)" numbering.
        /// </para>
        /// </summary>
        [Test]
        public void
            CutoutSlotPreparesBesideAPoiyomiSlotWithIndependentClones()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE cutout poiyomi siblings");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material poiyomi = null;
            Material cutout = null;
            Mesh mesh = null;
            AmusePlatformFinishState amuse = null;
            var fixtures = new LilToonCutoutConversionFixtures();

            try
            {
                fixtures.BaseSetUp();
                poiyomi = VerifiedOpaqueMaterial();
                cutout = LilToonFixtureTestBase.CreateCutoutConversionMaterial();
                cutout.SetTexture(
                    "_MainTex", fixtures.ImportFullyOpaqueMipmap(
                        "cutout_sibling"));
                AddTwoTriangleRenderer(root, poiyomi, cutout, out mesh);
                FillInBoundsUvs(mesh);

                amuse = RunBarrier(root);

                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(amuse.SemanticallyRefusedRendererCount,
                    Is.Zero);
                Assert.That(amuse.OpaqueCandidateTriangleCount,
                    Is.EqualTo(2),
                    "fixture precondition: both slots must prove their " +
                    "triangle opaque");
                foreach (AlphaSeparationSlotRefusal reason in Enum.GetValues(
                             typeof(AlphaSeparationSlotRefusal)))
                {
                    if (reason == AlphaSeparationSlotRefusal.None)
                    {
                        continue;
                    }

                    Assert.That(
                        amuse.SlotRefusalCount(reason), Is.Zero,
                        "both supported slots must map: " + reason);
                }

                Assert.That(amuse.Separation, Is.Not.Null);
                var candidates =
                    amuse.Separation.Renderers[0].CandidateSlots;
                Assert.That(candidates, Has.Count.EqualTo(2),
                    "both sibling slots must survive on the renderer");
                var poiyomiSlot = candidates.Single(
                    candidate =>
                        candidate.Plan.SourceMaterialBindingIndex == 0);
                var cutoutSlot = candidates.Single(
                    candidate =>
                        candidate.Plan.SourceMaterialBindingIndex == 1);

                Assert.That(amuse.Separation.CreatedClones,
                    Has.Count.EqualTo(2),
                    "each family converts its own source into its own " +
                    "clone");
                var poiyomiClone = amuse.Separation.CreatedClones[0];
                var cutoutClone = amuse.Separation.CreatedClones[1];
                Assert.That(poiyomiClone, Is.Not.SameAs(cutoutClone));
                Assert.That(
                    poiyomiSlot.OpaqueOfAdmitted[poiyomi],
                    Is.SameAs(poiyomiClone),
                    "the Poiyomi slot maps through its own conversion");
                Assert.That(
                    cutoutSlot.OpaqueOfAdmitted[cutout],
                    Is.SameAs(cutoutClone),
                    "the cutout slot maps through its own conversion");
                Assert.That(poiyomiClone.name,
                    Is.EqualTo(poiyomi.name + " (AMUSE Opaque 0)"),
                    "clone naming follows the barrier's slot order");
                Assert.That(cutoutClone.name,
                    Is.EqualTo(cutout.name + " (AMUSE Opaque 1)"));
            }
            finally
            {
                DestroyGenerated(amuse);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (poiyomi != null)
                    UnityEngine.Object.DestroyImmediate(poiyomi);
                if (cutout != null)
                    UnityEngine.Object.DestroyImmediate(cutout);

                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// A mixed supported admitted set — a material-swap clip
        /// alternating the Poiyomi stand-in with the convertible cutout —
        /// maps completely, each admitted value through its own family's
        /// conversion (spec §10).
        /// <para>
        /// Falsifies a mapping that resolves one family's admitted value
        /// through the other family's conversion, or one that refuses the
        /// mixed set now that both families convert.
        /// </para>
        /// </summary>
        [Test]
        public void
            MixedPoiyomiAndCutoutAdmittedSetMapsThroughEachOwnConversion()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE mixed poiyomi cutout slot");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material poiyomi = null;
            Material cutout = null;
            Mesh mesh = null;
            AnimationClip clip = null;
            AnimatorController controller = null;
            AmusePlatformFinishState amuse = null;
            var fixtures = new LilToonCutoutConversionFixtures();

            try
            {
                fixtures.BaseSetUp();
                poiyomi = VerifiedOpaqueMaterial();
                cutout = LilToonFixtureTestBase.CreateCutoutConversionMaterial();
                cutout.SetTexture(
                    "_MainTex", fixtures.ImportFullyOpaqueMipmap(
                        "cutout_mixed"));
                AddSingleTriangleRenderer(root, cutout, out mesh);
                FillInBoundsUvs(mesh);

                clip = NewSwapClip(
                    "AMUSE mixed poiyomi cutout swap", string.Empty, 0,
                    (0f, poiyomi), (1f, cutout));
                controller = NewController(
                    root, "AMUSE mixed poiyomi cutout graph", clip);

                amuse = RunBarrier(root);

                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(amuse.SemanticallyRefusedRendererCount,
                    Is.Zero);
                Assert.That(amuse.OpaqueCandidateTriangleCount,
                    Is.EqualTo(1),
                    "fixture precondition: the mixed slot must resolve " +
                    "and produce an opaque candidate");
                foreach (AlphaSeparationSlotRefusal reason in Enum.GetValues(
                             typeof(AlphaSeparationSlotRefusal)))
                {
                    if (reason == AlphaSeparationSlotRefusal.None)
                    {
                        continue;
                    }

                    Assert.That(
                        amuse.SlotRefusalCount(reason), Is.Zero,
                        "a fully supported mixed admitted set must map: " +
                        reason);
                }

                Assert.That(amuse.Separation, Is.Not.Null);
                var mapping = amuse.Separation.Renderers[0]
                    .CandidateSlots.Single().OpaqueOfAdmitted;
                Assert.That(mapping, Has.Count.EqualTo(2),
                    "both admitted values must map");
                Assert.That(mapping.ContainsKey(poiyomi), Is.True);
                Assert.That(mapping.ContainsKey(cutout), Is.True);
                Assert.That(mapping[poiyomi], Is.Not.SameAs(poiyomi),
                    "the Poiyomi value converts through its own family");
                Assert.That(mapping[cutout], Is.Not.SameAs(cutout),
                    "the cutout value converts through its own family");
                Assert.That(mapping[poiyomi], Is.Not.SameAs(mapping[cutout]),
                    "the two conversions must not share one clone");
                Assert.That(amuse.Separation.CreatedClones,
                    Has.Count.EqualTo(2));
            }
            finally
            {
                DestroyGenerated(amuse);
                DestroyControllerGraph(root, controller);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (poiyomi != null)
                    UnityEngine.Object.DestroyImmediate(poiyomi);
                if (cutout != null)
                    UnityEngine.Object.DestroyImmediate(cutout);
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                if (controller != null)
                    DestroyControllerGraph(controller);

                fixtures.BaseTearDown();
            }
        }

        /// <summary>
        /// A non-allowlisted lilToon identity (a stand-in cutout-outline
        /// shader) is unselectable at family selection and refuses its
        /// renderer-wide through material-dependency closure, while a
        /// later sibling renderer holding only supported materials still
        /// prepares with its mapping uncorrupted (spec §10).
        /// <para>
        /// Falsifies: a family selection that attests any lilToon-named
        /// shader, a closure that skips the unselectable material instead
        /// of failing the renderer's whole dependency set, and a renderer
        /// loop that stops at the refused renderer so the later supported
        /// sibling never prepares.
        /// </para>
        /// </summary>
        [Test]
        public void
            UnsupportedLilToonFamilyRefusesRendererWideThroughClosure()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE unsupported family closure");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            Material cutout = null;
            Material outline = null;
            Mesh blockedMesh = null;
            Mesh supportedMesh = null;
            AmusePlatformFinishState amuse = null;
            var fixtures = new LilToonCutoutConversionFixtures();

            try
            {
                fixtures.BaseSetUp();
                var texture =
                    fixtures.ImportFullyOpaqueMipmap("cutout_closure");
                var shader = ImportUnsupportedFamilyShader();
                outline = new Material(shader);

                cutout = LilToonFixtureTestBase.CreateCutoutConversionMaterial();
                cutout.SetTexture("_MainTex", texture);

                // The refused renderer comes first: the renderer loop must
                // continue past it. It holds one supported slot and one
                // unsupported slot, so closure spans the whole admitted set.
                var blockedChild = new GameObject("blocked");
                blockedChild.transform.SetParent(root.transform, false);
                blockedMesh = new Mesh
                {
                    vertices = new[]
                    {
                        Vector3.zero,
                        Vector3.right,
                        Vector3.up,
                        new Vector3(2f, 0f, 0f),
                        new Vector3(3f, 0f, 0f),
                        new Vector3(2f, 1f, 0f),
                    },
                    subMeshCount = 2,
                };
                blockedMesh.SetTriangles(new[] { 0, 1, 2 }, 0);
                blockedMesh.SetTriangles(new[] { 3, 4, 5 }, 1);
                FillInBoundsUvs(blockedMesh);
                var blockedRenderer =
                    blockedChild.AddComponent<SkinnedMeshRenderer>();
                blockedRenderer.sharedMesh = blockedMesh;
                blockedRenderer.sharedMaterials =
                    new[] { cutout, outline };

                // Fixture precondition: family selection itself cannot
                // attest the unsupported identity.
                Assert.That(
                    VerifiedLilToonTestSeams.SelectVerifiedFixtureRequest(
                        outline,
                        out _,
                        out _,
                        out _),
                    Is.False,
                    "fixture precondition: the outline identity must be " +
                    "unselectable at family selection");

                var supported = AddNamedChildRenderer(
                    root, "supported", cutout, out supportedMesh);
                FillInBoundsUvs(supportedMesh);

                amuse = RunBarrier(root);

                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(
                    amuse.RendererRefusalCount(
                        RendererAnalysisRefusal.MaterialDependencyClosureFailed),
                    Is.EqualTo(1),
                    "the unselectable member must fail the renderer's " +
                    "whole material dependency closure");
                Assert.That(amuse.SemanticallyRefusedRendererCount,
                    Is.EqualTo(1),
                    "exactly the renderer holding the unsupported member " +
                    "may refuse");
                Assert.That(amuse.AnalyzedRendererCount, Is.EqualTo(1),
                    "the supported sibling must still analyze");
                Assert.That(amuse.OpaqueCandidateTriangleCount,
                    Is.EqualTo(1),
                    "fixture precondition: the supported sibling must " +
                    "prove its triangle opaque");
                Assert.That(amuse.Separation, Is.Not.Null);
                Assert.That(amuse.Separation.Renderers,
                    Has.Count.EqualTo(1));
                Assert.That(
                    amuse.Separation.Renderers[0].Target.Renderer,
                    Is.SameAs(supported),
                    "the retained renderer must be the supported sibling");
                Assert.That(amuse.Separation.CreatedClones,
                    Has.Count.EqualTo(1));
                Assert.That(
                    amuse.Separation.TryGetOpaque(cutout, out var opaque),
                    Is.True,
                    "the supported sibling's mapping must be prepared and " +
                    "uncorrupted by the refused renderer");
                Assert.That(opaque, Is.Not.Null);
                Assert.That(
                    amuse.Separation.TryGetOpaque(outline, out _),
                    Is.False,
                    "the unsupported identity must never enter the " +
                    "avatar-wide mapping");
                foreach (AlphaSeparationSlotRefusal reason in Enum.GetValues(
                             typeof(AlphaSeparationSlotRefusal)))
                {
                    if (reason == AlphaSeparationSlotRefusal.None)
                    {
                        continue;
                    }

                    Assert.That(
                        amuse.SlotRefusalCount(reason), Is.Zero,
                        "the refusal is renderer-scoped closure, not a " +
                        "slot refusal: " + reason);
                }
            }
            finally
            {
                DestroyGenerated(amuse);
                if (blockedMesh != null)
                    UnityEngine.Object.DestroyImmediate(blockedMesh);
                if (supportedMesh != null)
                    UnityEngine.Object.DestroyImmediate(supportedMesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (cutout != null)
                    UnityEngine.Object.DestroyImmediate(cutout);
                if (outline != null)
                    UnityEngine.Object.DestroyImmediate(outline);
                AssetDatabase.DeleteAsset(UnsupportedFamilyTempFolder);

                fixtures.BaseTearDown();
            }
        }

        // --- Mixed-family fixture seams --------------------------------------

        /// <summary>
        /// Family selection for the mixed-family fixtures: the lilToon fixture
        /// shader selects the lilToon family and its own alpha request;
        /// everything else falls back to the verified Poiyomi seam, which is
        /// what production's stand-in fixtures encode. A lilToon fixture
        /// material renamed away from the fixture shader would fail visibly
        /// here as a Poiyomi schema mismatch, never silently.
        /// </summary>
        private static bool SelectMixedFamilyRequest(
            Material material,
            out CapturedAlphaMaterialFamily family,
            out MaterialEvidenceRequest alphaRelevance,
            out MaterialEvidenceRequest captureSchema)
        {
            if (material != null && material.shader != null &&
                string.Equals(
                    material.shader.name,
                    LilToonFixtureNames.ShaderName,
                    StringComparison.Ordinal))
            {
                family = CapturedAlphaMaterialFamily.LilToon;
                alphaRelevance = LilToonMaterialSemantics.AlphaEvidenceRequest;
                captureSchema = LilToonMaterialSemantics.AlphaEvidenceRequest;
                return true;
            }

            return VerifiedPoiyomiTestSeams.SelectVerifiedFixtureRequest(
                material, out family, out alphaRelevance, out captureSchema);
        }

        private static MaterialSemantics ResolveMixedFamilySemantics(
            CapturedAlphaMaterial captured)
        {
            switch (captured.Family)
            {
                case CapturedAlphaMaterialFamily.LilToon:
                    return new MaterialSemantics(
                        SemanticOutput<ColorSemanticValue>.Unknown(),
                        LilToonMaterialSemantics.InterpretVerifiedAlpha(
                            captured.Evidence),
                        SemanticOutput<ColorSemanticValue>.Unknown(),
                        SemanticOutput<NormalSemanticValue>.Unknown());
                default:
                    return VerifiedPoiyomiTestSeams.VerifiedAlphaOnly(captured);
            }
        }

        private sealed class LilToonFixtureNames : LilToonFixtureTestBase
        {
            internal const string ShaderName = FixtureShaderName;

            /// <summary>The schema-complete transparent source stand-in.</summary>
            internal const string Transparent = TransparentConversionShaderName;

            /// <summary>The distinct canonical opaque target stand-in.</summary>
            internal const string OpaqueTarget = OpaqueConversionShaderName;
        }

        /// <summary>
        /// Imports the fully-opaque mipmap texture the cutout conversion
        /// fixture assigns to <c>_MainTex</c>. The base's SetUp/TearDown are
        /// driven manually: NUnit never instantiates this helper.
        /// </summary>
        private class LilToonCutoutConversionFixtures
            : LilToonFixtureTestBase
        {
            internal Texture2D ImportFullyOpaqueMipmap(string name)
            {
                return ImportMipmapTexture(
                    name, 4, 4, FullyOpaquePixels());
            }

            internal Texture2D ImportTrilinearMipmap(string name)
            {
                return ImportMipmapTexture(
                    name,
                    4,
                    4,
                    FullyOpaquePixels(),
                    FilterMode.Trilinear);
            }

            internal Texture2D ImportMismatchedWrapMipmap(string name)
            {
                return ImportMipmapTexture(
                    name,
                    4,
                    4,
                    FullyOpaquePixels(),
                    configure: importer =>
                    {
                        importer.wrapModeU =
                            UnityEngine.TextureWrapMode.Repeat;
                        importer.wrapModeV =
                            UnityEngine.TextureWrapMode.Clamp;
                    });
            }

            internal Texture2D ImportStreamingMipmap(string name)
            {
                const int size = 64;
                var pixels = new Color32[size * size];
                for (var index = 0; index < pixels.Length; index++)
                {
                    pixels[index] = new Color32(255, 255, 255, 255);
                }

                var texture = ImportMipmapTexture(
                    name,
                    size,
                    size,
                    pixels,
                    configure: importer =>
                        importer.streamingMipmaps = true);
                Assert.That(texture.streamingMipmaps, Is.True,
                    "fixture precondition: the imported texture must " +
                    "retain streaming residency");
                return texture;
            }

            private static Color32[] FullyOpaquePixels()
            {
                var pixels = new Color32[4 * 4];
                for (var index = 0; index < pixels.Length; index++)
                {
                    pixels[index] = new Color32(255, 255, 255, 255);
                }

                return pixels;
            }
        }

        /// <summary>
        /// Imports a fully-opaque mipmap texture for the Poiyomi non-identity
        /// ST scenario, mirroring
        /// <see cref="LilToonCutoutConversionFixtures.ImportFullyOpaqueMipmap"/>
        /// in shape (mipmap-enabled, uncompressed RGBA32, a separate temp
        /// folder via <see cref="PoiyomiFixtureTestBase"/>) but not exactly:
        /// <see cref="ImportMipmapTexture"/> additionally sets
        /// <c>isReadable = true</c>, which the lilToon helper does not, so
        /// <c>PoiyomiNonIdentityStPreparationLeavesSourceAssetsUnchanged</c>
        /// can read the imported texture's pixels back directly via
        /// <c>GetPixels32</c> for its structural audit. This changes no
        /// production branch: <c>UnityAlphaFieldEvidence</c> captures alpha
        /// evidence exclusively via <c>Graphics.Blit</c> +
        /// <c>AsyncGPUReadback</c> (`:268,275`) and never reads
        /// <c>isReadable</c>. The base's SetUp/TearDown are driven manually:
        /// NUnit never instantiates this helper.
        /// </summary>
        private sealed class PoiyomiTextureBackedFixtures
            : PoiyomiFixtureTestBase
        {
            internal Texture2D ImportFullyOpaqueMipmap(string name)
            {
                return ImportMipmapTexture(name, 4, 4, FullyOpaquePixels());
            }

            /// <summary>
            /// Imports a real, single-level (<c>mipmapEnabled = false</c>),
            /// uncompressed RGBA32 texture whose alpha content is supplied
            /// verbatim (bottom-to-top, matching
            /// <see cref="AlphaTextureData"/>'s convention) — unlike
            /// <see cref="ImportFullyOpaqueMipmap"/>, this can carry a
            /// position-dependent alpha contrast end to end, because with no
            /// generated mip chain there is no coarser level for a
            /// partial-opacity region to box-filter into. Point/Clamp
            /// sampling, matching the classifier control's sampling settings
            /// so both proofs describe the same sampled domain.
            /// </summary>
            internal Texture2D ImportSingleLevelAlphaField(
                string name, int width, int height, byte[] alphaBottomToTop)
            {
                if (alphaBottomToTop.Length != width * height)
                {
                    throw new ArgumentException(
                        "Alpha grid length must equal width times height.",
                        nameof(alphaBottomToTop));
                }

                var path = TempFolder + "/" + name + ".png";
                var staging = new Texture2D(
                    width, height, TextureFormat.RGBA32, false);
                var pixels = new Color32[alphaBottomToTop.Length];
                for (var index = 0; index < pixels.Length; index++)
                {
                    pixels[index] = new Color32(
                        255, 255, 255, alphaBottomToTop[index]);
                }
                staging.SetPixels32(pixels);
                staging.Apply();
                File.WriteAllBytes(path, staging.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(staging);

                AssetDatabase.ImportAsset(
                    path, ImportAssetOptions.ForceSynchronousImport);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                importer.textureCompression =
                    TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();

                var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(
                    loaded, Is.Not.Null,
                    $"Imported texture '{path}' must load.");
                return loaded;
            }

            /// <summary>
            /// Writes an explicit RGBA32 pixel grid as mip 0 and imports it
            /// as a mipmap-enabled asset; the lower levels are the
            /// importer's own downsample of the supplied base level. Mirrors
            /// <see cref="LilToonFixtureTestBase.ImportMipmapTexture"/>.
            /// </summary>
            private Texture2D ImportMipmapTexture(
                string name,
                int width,
                int height,
                Color32[] baseLevelBottomToTop)
            {
                var path = TempFolder + "/" + name + ".png";
                var staging = new Texture2D(
                    width, height, TextureFormat.RGBA32, false);
                staging.SetPixels32(baseLevelBottomToTop);
                staging.Apply();
                File.WriteAllBytes(path, staging.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(staging);

                AssetDatabase.ImportAsset(
                    path, ImportAssetOptions.ForceSynchronousImport);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.mipmapEnabled = true;
                // Readable so the source-preservation audit
                // (PoiyomiNonIdentityStPreparationLeavesSourceAssetsUnchanged)
                // can read pixels back directly via GetPixels32; production's
                // own host provider does not require this.
                importer.isReadable = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = UnityEngine.TextureWrapMode.Repeat;
                importer.streamingMipmaps = false;
                // Uncompressed keeps the imported GPU format RGBA32: the
                // alpha-evidence format allowlist admits RGBA32 exactly,
                // while platform compression would collapse an all-opaque
                // source to DXT1, which has no alpha channel to prove and
                // refuses.
                importer.textureCompression =
                    TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();

                var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(
                    loaded, Is.Not.Null,
                    $"Imported texture '{path}' must load.");
                return loaded;
            }

            private static Color32[] FullyOpaquePixels()
            {
                var pixels = new Color32[4 * 4];
                for (var index = 0; index < pixels.Length; index++)
                {
                    pixels[index] = new Color32(255, 255, 255, 255);
                }

                return pixels;
            }
        }

        /// <summary>
        /// One cutout conversion arm: a transient single-triangle renderer
        /// fixture over a caller-owned texture plus the barrier run over it,
        /// with everything the arm created destroyed on disposal. An arm
        /// that animates a material binding carries one constant float clip;
        /// an arm that varies the serialized state configures the material
        /// before the build.
        /// </summary>
        private sealed class CutoutArmFixture : IDisposable
        {
            private GameObject root;
            private Mesh mesh;
            private AnimationClip clip;
            private AnimatorController controller;
            private AmusePlatformFinishState amuse;

            private CutoutArmFixture() { }

            internal Material Material { get; private set; }

            internal static CutoutArmFixture Create(
                Texture mainTex,
                string rootName,
                Action<Material> configure,
                string animatedBinding,
                float? animatedValue)
            {
                var fixture = new CutoutArmFixture
                {
                    root = new GameObject(rootName),
                    Material = LilToonFixtureTestBase.CreateCutoutConversionMaterial(),
                };
                fixture.root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                fixture.Material.SetTexture("_MainTex", mainTex);
                configure?.Invoke(fixture.Material);

                fixture.mesh = new Mesh
                {
                    vertices = new[]
                    {
                        Vector3.zero,
                        Vector3.right,
                        Vector3.up,
                    },
                };
                fixture.mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
                FillInBoundsUvs(fixture.mesh);

                var renderer = fixture.root.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = fixture.mesh;
                renderer.sharedMaterials = new[] { fixture.Material };

                if (animatedBinding != null)
                {
                    fixture.clip = NewFloatClip(
                        rootName + " clip", string.Empty,
                        animatedBinding, animatedValue ?? 0f);
                    fixture.controller = NewController(
                        fixture.root, rootName + " graph", fixture.clip);
                }

                return fixture;
            }

            internal AmusePlatformFinishState Run(
                VerifiedLilToonConversion lilToonConversion = null)
            {
                amuse = RunBarrier(
                    root, lilToonConversion: lilToonConversion);
                return amuse;
            }

            public void Dispose()
            {
                DestroyGenerated(amuse);
                if (root != null)
                {
                    DestroyControllerGraph(root, controller);
                }

                if (controller != null)
                {
                    DestroyControllerGraph(controller);
                }

                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                if (Material != null)
                {
                    UnityEngine.Object.DestroyImmediate(Material);
                }

                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Gives every vertex one in-bounds UV0 domain, so a texture-backed
        /// cutout proof has the domain it needs. Any in-bounds domain works
        /// over a fully-opaque chain.
        /// </summary>
        private static void FillInBoundsUvs(Mesh mesh)
        {
            var uvs = new Vector2[mesh.vertexCount];
            for (var index = 0; index < uvs.Length; index++)
            {
                uvs[index] = new Vector2(0.25f, 0.25f);
            }

            mesh.uv = uvs;
        }

        // --- Unsupported-family fixture ---------------------------------------

        /// <summary>
        /// A stand-in shader carrying a non-allowlisted lilToon identity (a
        /// cutout-outline shader). Written and imported under one
        /// test-owned folder; the caller deletes the folder in its finally.
        /// </summary>
        private const string UnsupportedFamilyTempFolder =
            "Assets/AmuseTests_AlphaUnsupportedFamily";

        private const string UnsupportedFamilyShaderName =
            "Hidden/lilToonCutoutOutline";

        /// <summary>
        /// Writes, imports, and returns the unsupported-family temp shader
        /// without authoring a .meta (Unity generates it on import).
        /// </summary>
        private static Shader ImportUnsupportedFamilyShader()
        {
            if (!AssetDatabase.IsValidFolder(UnsupportedFamilyTempFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", "AmuseTests_AlphaUnsupportedFamily");
            }

            var path = UnsupportedFamilyTempFolder +
                       "/LilToonCutoutOutline.shader";
            File.WriteAllText(
                path,
                "Shader \"" + UnsupportedFamilyShaderName + "\"\n" +
                "{\n" +
                "    SubShader\n" +
                "    {\n" +
                "        Pass\n" +
                "        {\n" +
                "            CGPROGRAM\n" +
                "            #pragma vertex vert\n" +
                "            #pragma fragment frag\n" +
                "            #include \"UnityCG.cginc\"\n" +
                "            float4 vert(float4 vertex : POSITION) : " +
                "SV_POSITION\n" +
                "            { return UnityObjectToClipPos(vertex); }\n" +
                "            fixed4 frag() : SV_Target\n" +
                "            { return fixed4(1, 1, 1, 1); }\n" +
                "            ENDCG\n" +
                "        }\n" +
                "    }\n" +
                "}\n");
            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);

            var shader = Shader.Find(UnsupportedFamilyShaderName);
            Assert.That(
                shader, Is.Not.Null,
                "The unsupported-family temp shader must import.");
            return shader;
        }

        // --- Fixture helpers ---------------------------------------------------

        /// <summary>
        /// Drives the real bindings-capture and barrier passes through the
        /// production entry, substituting the public-fixture seams for
        /// unavailable vendor source attestation and, by default, the fourth
        /// verified seam for the shader-family opaque-conversion step.
        /// </summary>
        private static AmusePlatformFinishState RunBarrier(
            GameObject root,
            AlphaMaterialRequestSelector selectRequest = null,
            ClosedAlphaMaterialCapturer capturer = null,
            CapturedAlphaMaterialSemanticsResolver resolveSemantics = null,
            VerifiedPoiyomiConversion poiyomiConversion = null,
            VerifiedLilToonConversion lilToonConversion = null)
        {
            var context = AvatarProcessor.ProcessAvatar(
                root, PreparationTestPlatform.Instance);
            context.GetState<AmusePlatformFinishState>().AnimatorBindings =
                GenericPlatformAnimatorBindings.Instance;

            AmusePlatformFinishPass.Execute(
                context,
                SupportedFacts(),
                selectRequest ?? VerifiedLilToonTestSeams
                    .SelectVerifiedFixtureRequest,
                capturer ?? VerifiedLilToonTestSeams
                    .CaptureVerifiedFixtureMaterials,
                resolveSemantics ?? VerifiedLilToonTestSeams.VerifiedAlphaOnly,
                poiyomiConversion ?? VerifiedPoiyomiTestSeams
                    .VerifiedConversion,
                lilToonConversion ?? VerifiedLilToonTestSeams
                    .VerifiedConversion);

            return context.GetState<AmusePlatformFinishState>();
        }

        private static void DestroyGenerated(AmusePlatformFinishState amuse)
        {
            if (amuse?.Separation == null)
            {
                return;
            }

            foreach (var clone in amuse.Separation.CreatedClones)
            {
                if (clone != null)
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                }
            }

            foreach (var prepared in amuse.Separation.Renderers)
            {
                if (prepared.MeshClone != null)
                {
                    UnityEngine.Object.DestroyImmediate(prepared.MeshClone);
                }
            }
        }

        private static void DestroyControllerGraph(
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

        private static void DestroyControllerGraph(AnimatorController source)
        {
            if (source == null)
            {
                return;
            }

            foreach (var layer in source.layers)
            {
                UnityEngine.Object.DestroyImmediate(layer.stateMachine);
            }

            UnityEngine.Object.DestroyImmediate(source);
        }

        private static SkinnedMeshRenderer AddSingleTriangleRenderer(
            GameObject root,
            Material material,
            out Mesh mesh)
        {
            mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                },
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);

            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = new[] { material };
            return renderer;
        }

        private static SkinnedMeshRenderer AddTwoTriangleRenderer(
            GameObject root,
            Material first,
            Material second,
            out Mesh mesh)
        {
            mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                    new Vector3(2f, 0f, 0f),
                    new Vector3(3f, 0f, 0f),
                    new Vector3(2f, 1f, 0f),
                },
                subMeshCount = 2,
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 3, 4, 5 }, 1);

            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = new[] { first, second };
            return renderer;
        }

        /// <summary>
        /// A single-triangle renderer on a named child, so two renderers can
        /// hold distinct animation paths in one fixture.
        /// </summary>
        private static SkinnedMeshRenderer AddNamedChildRenderer(
            GameObject root,
            string name,
            Material material,
            out Mesh mesh)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                },
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);

            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = new[] { material };
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

        private static AnimationClip NewFloatClip(
            string name,
            string rendererPath,
            string propertyName,
            float value)
        {
            var clip = new AnimationClip { name = name };
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(
                    rendererPath,
                    typeof(SkinnedMeshRenderer),
                    propertyName),
                AnimationCurve.Constant(0f, 1f, value));
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

        /// <summary>
        /// A non-forced Poiyomi material whose alpha proof is genuinely
        /// texture-backed with a non-identity <c>_MainTex</c> ST: every gate
        /// the attested source requires proven zero (design §3.2) is set
        /// explicitly, mirroring
        /// <c>PoiyomiAlphaTests.NonForcedMainTexNonIdentityStPreservesMappingAndClassifies</c>.
        /// </summary>
        private static Material TextureBackedNonIdentityStMaterial(
            Texture texture, Vector2 scale, Vector2 offset)
        {
            var material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            material.SetColor("_Color", Color.white);
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", scale);
            material.SetTextureOffset("_MainTex", offset);
            material.SetVector("_MainTexPan", Vector4.zero);
            material.SetFloat("_MainTexUV", 0f);
            material.SetFloat("_MainPixelMode", 0f);
            material.SetFloat("_MainTexStochastic", 0f);
            material.SetFloat("_PoiParallax", 0f);
            material.SetFloat("_PoiInternalParallax", 0f);
            return material;
        }

        /// <summary>
        /// A material carrying every one of the canonical Opaque recipe's
        /// facts, so conversion classifies it AlreadyOpaque: a successful
        /// no-op that maps the source to itself and creates no clone.
        /// </summary>
        private static Material CanonicalOpaqueMaterial()
        {
            var material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
            foreach (var (property, value) in PoiyomiOpaqueConversion
                         .CanonicalOpaqueProperties)
            {
                material.SetFloat(property, value);
            }

            material.renderQueue =
                PoiyomiOpaqueConversion.CanonicalOpaqueRenderQueue;
            material.SetOverrideTag(
                PoiyomiOpaqueConversion.RenderTypeTagName,
                PoiyomiOpaqueConversion.CanonicalOpaqueRenderType);
            return material;
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

        private sealed class PreparationTestPlatform : INDMFPlatformProvider
        {
            internal static readonly PreparationTestPlatform Instance =
                new PreparationTestPlatform();

            public string QualifiedName => "nadena.dev.ndmf.generic";
            public string DisplayName => "AMUSE alpha separation preparation";
        }
    }
}