using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEngine;

// Required: UnityEngine also declares TextureWrapMode, so the semantic enum
// must be named explicitly in this test file.
using TextureWrapMode = Alrauna.Amuse.Editor.Semantics.TextureWrapMode;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    /// <summary>
    /// Pins the per-slot verdict ladder that the PlatformFinish barrier and
    /// the edit-time dry run share. The fixtures use hand-built plans, so
    /// the pins hold the ladder itself and not the planner that feeds it.
    /// </summary>
    public sealed class SlotTransformDecisionTests
    {
        // --- Falsifier 1: gate-skipping split -------------------------------
        // A Decide that skips the convert coverage gate labels the
        // below-threshold fixture Split. This test catches it, and its last
        // assertion is the guard: a plan entry never yields None.
        [Test]
        public void BelowConvertCoverageSlotRefusesWithCoverageBelowMinimum()
        {
            var plan = Plan(SplitSubmesh(0, 0, 1, 2));

            var verdicts = SlotTransformDecision.Decide(
                plan, null, null, 50, 0);

            Assert.That(verdicts, Has.Count.EqualTo(1));
            Assert.That(verdicts[0].SlotIndex, Is.Zero);
            Assert.That(verdicts[0].Outcome, Is.EqualTo(
                SlotTransformOutcome.CoverageBelowMinimum));
            Assert.That(verdicts[0].ProvenTriangles, Is.EqualTo(1));
            Assert.That(verdicts[0].TotalTriangles, Is.EqualTo(3));
            AssertPlanEntriesNeverYieldNone(plan, verdicts);
        }

        [Test]
        public void AtConvertCoverageSlotSplits()
        {
            var plan = Plan(SplitSubmesh(0, 0, 1, 1));

            var verdicts = SlotTransformDecision.Decide(
                plan, null, null, 50, 0);

            // The predicate proceeds at the boundary, so a share exactly at
            // the minimum stays a split.
            Assert.That(verdicts[0].Outcome, Is.EqualTo(
                SlotTransformOutcome.Split));
            AssertPlanEntriesNeverYieldNone(plan, verdicts);
        }

        // --- Falsifier 2: gate-skipping fold --------------------------------
        // A Decide that skips the preserve gate leaves the qualifying
        // fixture on Split. This test catches it, and its last assertion is
        // the guard: a plan entry never yields None.
        [Test]
        public void PreserveQualifyingSlotFolds()
        {
            var plan = Plan(
                SplitSubmesh(0, 0, 99, 1),
                SplitSubmesh(1, 0, 49, 1));

            var verdicts = SlotTransformDecision.Decide(
                plan, null, null, 50, 5);

            Assert.That(verdicts, Has.Count.EqualTo(1));
            Assert.That(verdicts[0].Outcome, Is.EqualTo(
                SlotTransformOutcome.Fold));
            Assert.That(verdicts[0].FoldedUnprovenTriangles, Is.EqualTo(2));
            Assert.That(verdicts[0].ProvenTriangles, Is.EqualTo(148));
            Assert.That(verdicts[0].TotalTriangles, Is.EqualTo(150));
            AssertPlanEntriesNeverYieldNone(plan, verdicts);
        }

        [Test]
        public void PreserveValueOfZeroQualifiesNothing()
        {
            var plan = Plan(
                SplitSubmesh(0, 0, 99, 1),
                SplitSubmesh(1, 0, 49, 1));

            var verdicts = SlotTransformDecision.Decide(
                plan, null, null, 50, 0);

            // A preserve value of 0 qualifies nothing, so the slot stays a
            // split and never folds.
            Assert.That(verdicts[0].Outcome, Is.EqualTo(
                SlotTransformOutcome.Split));
            Assert.That(verdicts[0].FoldedUnprovenTriangles, Is.Zero);
            AssertPlanEntriesNeverYieldNone(plan, verdicts);
        }

        [Test]
        public void AnisotropyRefusesOnlyWhenEverySubmeshUnchanged()
        {
            var allUnchanged = Plan(
                UnchangedSubmesh(0, 0),
                UnchangedSubmesh(1, 0));

            var refuses = SlotTransformDecision.Decide(
                allUnchanged,
                new[] { AnisotropicSlot() },
                null, 50, 0);
            Assert.That(refuses[0].Outcome, Is.EqualTo(
                SlotTransformOutcome.AnisotropyDifferences));

            var splitSibling = Plan(
                UnchangedSubmesh(0, 0),
                SplitSubmesh(1, 0, 2, 0));
            var keepsSplit = SlotTransformDecision.Decide(
                splitSibling,
                new[] { AnisotropicSlot() },
                null, 50, 0);
            Assert.That(keepsSplit[0].Outcome, Is.EqualTo(
                SlotTransformOutcome.Split),
                "a slot with a split submesh never refuses for anisotropy");

            var nonAnisotropic = SlotTransformDecision.Decide(
                allUnchanged,
                new[] { NonAnisotropicSlot() },
                null, 50, 0);
            Assert.That(nonAnisotropic[0].Outcome, Is.EqualTo(
                SlotTransformOutcome.Split),
                "a resolved slot without anisotropic sampling never refuses");
        }

        [Test]
        public void AlreadyOpaqueWinsOverSplit()
        {
            var plan = Plan(
                SplitSubmesh(0, 0, 2, 0),
                UnchangedSubmesh(1, 1));

            var verdicts = SlotTransformDecision.Decide(
                plan, null, new[] { true, false }, 50, 0);

            Assert.That(verdicts, Has.Count.EqualTo(2));
            Assert.That(verdicts[0].SlotIndex, Is.Zero);
            Assert.That(verdicts[0].Outcome, Is.EqualTo(
                SlotTransformOutcome.AlreadyOpaque));
            Assert.That(verdicts[1].SlotIndex, Is.EqualTo(1));
            Assert.That(verdicts[1].Outcome, Is.EqualTo(
                SlotTransformOutcome.Split));

            var extended = SlotTransformDecision.Decide(
                plan, null, new[] { true, true, false }, 50, 0);

            // The flag wins over a plan entry, and a slot with no plan
            // entry and no flag gets None.
            Assert.That(extended, Has.Count.EqualTo(3));
            Assert.That(extended[1].Outcome, Is.EqualTo(
                SlotTransformOutcome.AlreadyOpaque));
            Assert.That(extended[2].Outcome, Is.EqualTo(
                SlotTransformOutcome.None));
            AssertPlanEntriesNeverYieldNone(plan, extended);
        }

        private static void AssertPlanEntriesNeverYieldNone(
            MeshSeparationPlan plan,
            IReadOnlyList<SlotTransformVerdict> verdicts)
        {
            foreach (var submesh in plan.Submeshes)
            {
                Assert.That(
                    verdicts[submesh.SourceMaterialBindingIndex].Outcome,
                    Is.Not.EqualTo(SlotTransformOutcome.None),
                    "a plan entry never yields None");
            }
        }

        private static MeshSeparationPlan Plan(
            params SubmeshSeparationPlan[] submeshes)
        {
            return new MeshSeparationPlan(null, submeshes, false, false, 0, 0);
        }

        private static SubmeshSeparationPlan SplitSubmesh(
            int submeshIndex, int slotIndex, int opaque, int transparent)
        {
            return new SubmeshSeparationPlan(
                submeshIndex, slotIndex,
                new int[opaque], new int[transparent],
                SubmeshSeparationDisposition.Split);
        }

        private static SubmeshSeparationPlan UnchangedSubmesh(
            int submeshIndex, int slotIndex)
        {
            return new SubmeshSeparationPlan(
                submeshIndex, slotIndex,
                Array.Empty<int>(), Array.Empty<int>(),
                SubmeshSeparationDisposition.Unchanged);
        }

        private static SlotResolutionResult AnisotropicSlot()
        {
            return SlotResolutionResult.Resolved(
                new[] { ClassifiedResolution(TextureAnisoMode.Anisotropic) });
        }

        private static SlotResolutionResult NonAnisotropicSlot()
        {
            return SlotResolutionResult.Resolved(
                new[] { ClassifiedResolution(TextureAnisoMode.None) });
        }

        private static AlphaResolution ClassifiedResolution(
            TextureAnisoMode anisoMode)
        {
            return AlphaResolution.Classified(
                new AlphaMipChain(
                    new[] { new AlphaTextureData(1, 1, new[] { (byte)255 }) }),
                new TextureSampling(
                    TextureFilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    anisoMode),
                new UvMapping(0, Vector2.one, Vector2.zero), 0);
        }
    }
}
