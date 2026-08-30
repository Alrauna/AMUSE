using System;
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
        public void MixedPoiyomiAndLilToonSlotIsRefusedAsUnsupportedFamily()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE mixed family slot");
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
                    "produce an opaque candidate — closure and alpha proof " +
                    "succeeded — or the family refusal proves nothing");
                Assert.That(
                    amuse.SlotRefusalCount(
                        AlphaSeparationSlotRefusal
                            .OpaqueConversionUnsupportedFamily),
                    Is.EqualTo(1),
                    "a slot mixing an attested lilToon material with a " +
                    "Poiyomi one must be refused as an unsupported " +
                    "conversion family");
                Assert.That(amuse.Separation, Is.Null,
                    "the only candidate slot was refused, so nothing is " +
                    "retained");
                foreach (AlphaSeparationSlotRefusal reason in Enum.GetValues(
                             typeof(AlphaSeparationSlotRefusal)))
                {
                    if (reason == AlphaSeparationSlotRefusal.None ||
                        reason == AlphaSeparationSlotRefusal
                            .OpaqueConversionUnsupportedFamily)
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
                DestroyControllerGraph(root, controller);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
                if (poiyomi != null) UnityEngine.Object.DestroyImmediate(poiyomi);
                if (lilToon != null) UnityEngine.Object.DestroyImmediate(lilToon);
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        // --- Falsifier 10: Poiyomi slot survives beside a refused lilToon ----

        [Test]
        public void
            PoiyomiSlotPreparesBesideARefusedLilToonSlotOnTheSameRenderer()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE lilToon sibling");
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
                    "lilToon material passes family selection and closure, " +
                    "so only conversion can refuse it");
                Assert.That(amuse.OpaqueCandidateTriangleCount, Is.EqualTo(2),
                    "fixture precondition: both slots must prove their " +
                    "triangle opaque, or the same-renderer escalation " +
                    "proves nothing");
                Assert.That(
                    amuse.SlotRefusalCount(
                        AlphaSeparationSlotRefusal
                            .OpaqueConversionUnsupportedFamily),
                    Is.EqualTo(1),
                    "the lilToon slot must be refused as an unsupported " +
                    "conversion family");

                Assert.That(amuse.Separation, Is.Not.Null);
                Assert.That(amuse.Separation.Renderers, Has.Count.EqualTo(1));
                var candidates = amuse.Separation.Renderers[0].CandidateSlots;
                Assert.That(candidates, Has.Count.EqualTo(1),
                    "the Poiyomi slot must survive beside the refused " +
                    "lilToon slot on the same renderer");
                Assert.That(
                    candidates[0].Plan.SourceMaterialBindingIndex, Is.Zero);
                Assert.That(candidates[0].OpaqueOfAdmitted, Is.Not.Empty,
                    "the Poiyomi slot's mapping must be prepared");
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

                VerifiedOpaqueConversion conversion =
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

                amuse = RunBarrier(root, conversion: conversion);

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
            CapturedAlphaMaterialSemanticsResolver resolveSemantics = null,
            VerifiedOpaqueConversion conversion = null)
        {
            var context = AvatarProcessor.ProcessAvatar(
                root, PreparationTestPlatform.Instance);
            context.GetState<AmusePlatformFinishState>().AnimatorBindings =
                GenericPlatformAnimatorBindings.Instance;

            AmusePlatformFinishPass.Execute(
                context,
                SupportedFacts(),
                selectRequest ?? VerifiedPoiyomiTestSeams
                    .SelectVerifiedFixtureRequest,
                VerifiedPoiyomiTestSeams.CaptureVerifiedFixtureMaterials,
                resolveSemantics ?? VerifiedPoiyomiTestSeams.VerifiedAlphaOnly,
                conversion ?? VerifiedPoiyomiTestSeams.VerifiedConversion);

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