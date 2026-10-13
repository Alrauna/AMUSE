using System;
using System.Collections.Generic;

namespace Alrauna.Amuse.Editor.Analysis
{
    /// <summary>
    /// One material slot's outcome under the shared verdict ladder. The
    /// names mirror the slot refusals the barrier reports, so a verdict
    /// maps onto a refusal or a plan action without a translation table.
    /// </summary>
    internal enum SlotTransformOutcome
    {
        None,
        Split,
        Fold,
        CoverageBelowMinimum,
        AnisotropyDifferences,
        AlreadyOpaque,
    }

    /// <summary>
    /// The counts behind one slot's outcome. The dry run shows them beside
    /// the verdict, and a fold carries the unproven triangle count the
    /// build's forced report event needs.
    /// </summary>
    internal readonly struct SlotTransformVerdict
    {
        internal int SlotIndex { get; }
        internal SlotTransformOutcome Outcome { get; }
        internal int ProvenTriangles { get; }
        internal int TotalTriangles { get; }
        internal int FoldedUnprovenTriangles { get; }

        internal SlotTransformVerdict(
            int slotIndex,
            SlotTransformOutcome outcome,
            int provenTriangles,
            int totalTriangles,
            int foldedUnprovenTriangles)
        {
            SlotIndex = slotIndex;
            Outcome = outcome;
            ProvenTriangles = provenTriangles;
            TotalTriangles = totalTriangles;
            FoldedUnprovenTriangles = foldedUnprovenTriangles;
        }
    }

    /// <summary>
    /// The per-slot verdict ladder shared by the PlatformFinish barrier and
    /// the edit-time dry run. Both read one function, so the dropdown a
    /// user sees can never drift from the plan the build prepares.
    /// </summary>
    internal static class SlotTransformDecision
    {
        // One decision per material slot, in ascending slot index order.
        // Decision ladder, fixed: the preserve fold gate first. Then, for a
        // slot whose every submesh stayed Unchanged, AnisotropyDifferences
        // when the slot result carries HasAnisotropicSampling. Then, on a
        // slot with Split submeshes, CoverageBelowMinimum when the convert
        // coverage predicate refuses. Then Split. AlreadyOpaque comes from
        // the caller-supplied per-slot flag and wins over everything. A
        // slot with no plan entry and no flag gets None.
        internal static IReadOnlyList<SlotTransformVerdict> Decide(
            MeshSeparationPlan plan,
            IReadOnlyList<SlotResolutionResult> slotResults,
            IReadOnlyList<bool> alreadyOpaqueBySlot,
            int minimumOpaqueCoveragePercent,
            int minimumOpaqueCoverageToPreservePercent)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var entriesByBindingIndex =
                new Dictionary<int, List<SubmeshSeparationPlan>>();
            var slotCount = 0;
            foreach (var entry in plan.Submeshes)
            {
                if (!entriesByBindingIndex.TryGetValue(
                        entry.SourceMaterialBindingIndex,
                        out var slotEntries))
                {
                    slotEntries = new List<SubmeshSeparationPlan>();
                    entriesByBindingIndex.Add(
                        entry.SourceMaterialBindingIndex, slotEntries);
                }

                slotEntries.Add(entry);
                if (entry.SourceMaterialBindingIndex + 1 > slotCount)
                {
                    slotCount = entry.SourceMaterialBindingIndex + 1;
                }
            }

            if (alreadyOpaqueBySlot != null &&
                alreadyOpaqueBySlot.Count > slotCount)
            {
                slotCount = alreadyOpaqueBySlot.Count;
            }

            if (slotResults != null && slotResults.Count > slotCount)
            {
                slotCount = slotResults.Count;
            }

            var verdicts = new List<SlotTransformVerdict>(slotCount);
            for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                verdicts.Add(DecideSlot(
                    entriesByBindingIndex, slotIndex, slotResults,
                    alreadyOpaqueBySlot, minimumOpaqueCoveragePercent,
                    minimumOpaqueCoverageToPreservePercent));
            }

            return verdicts;
        }

        /// <summary>
        /// Whether every submesh of the slot holds a strictly smaller
        /// unproven share than the stored preserve value. Moved verbatim
        /// from the barrier's fold gate, so preparation and the dry run
        /// cannot qualify different slots. A preserve value of 0 qualifies
        /// nothing.
        /// </summary>
        internal static bool PreserveSlotQualifies(
            IReadOnlyList<SubmeshSeparationPlan> slotEntries,
            int minimumOpaqueCoverageToPreservePercent)
        {
            var slotQualifies = true;
            foreach (var entry in slotEntries)
            {
                var totalTriangles =
                    entry.OpaqueTriangleOrdinals.Count +
                    entry.TransparentTriangleOrdinals.Count;
                if (entry.Disposition !=
                        SubmeshSeparationDisposition.Split ||
                    entry.TransparentTriangleOrdinals.Count * 100 >=
                    minimumOpaqueCoverageToPreservePercent *
                    totalTriangles)
                {
                    slotQualifies = false;
                    break;
                }
            }

            return slotQualifies;
        }

        /// <summary>
        /// Whether the split's proven-opaque share is strictly below
        /// the user's minimum. Exact integer arithmetic, and a share
        /// exactly at the minimum proceeds. Unknown and transparent
        /// triangles both count in the denominator, because neither is
        /// proven opaque.
        /// </summary>
        internal static bool OpaqueCoverageBelow(
            int minimumOpaqueCoveragePercent,
            SubmeshSeparationPlan submesh)
        {
            var totalTriangles =
                submesh.OpaqueTriangleOrdinals.Count +
                submesh.TransparentTriangleOrdinals.Count;
            return submesh.OpaqueTriangleOrdinals.Count * 100 <
                   minimumOpaqueCoveragePercent * totalTriangles;
        }

        private static SlotTransformVerdict DecideSlot(
            Dictionary<int, List<SubmeshSeparationPlan>> entriesByBindingIndex,
            int slotIndex,
            IReadOnlyList<SlotResolutionResult> slotResults,
            IReadOnlyList<bool> alreadyOpaqueBySlot,
            int minimumOpaqueCoveragePercent,
            int minimumOpaqueCoverageToPreservePercent)
        {
            entriesByBindingIndex.TryGetValue(
                slotIndex, out var slotEntries);
            var hasEntries = slotEntries != null;
            var proven = 0;
            var unproven = 0;
            var everySubmeshUnchanged = hasEntries;
            var splitSubmeshBelowCoverage = false;
            if (hasEntries)
            {
                foreach (var entry in slotEntries)
                {
                    proven += entry.OpaqueTriangleOrdinals.Count;
                    unproven += entry.TransparentTriangleOrdinals.Count;
                    if (entry.Disposition !=
                        SubmeshSeparationDisposition.Unchanged)
                    {
                        everySubmeshUnchanged = false;
                    }

                    if (entry.Disposition ==
                            SubmeshSeparationDisposition.Split &&
                        OpaqueCoverageBelow(
                            minimumOpaqueCoveragePercent, entry))
                    {
                        splitSubmeshBelowCoverage = true;
                    }
                }
            }

            var outcome = SlotTransformOutcome.None;
            if (alreadyOpaqueBySlot != null &&
                slotIndex < alreadyOpaqueBySlot.Count &&
                alreadyOpaqueBySlot[slotIndex])
            {
                outcome = SlotTransformOutcome.AlreadyOpaque;
            }
            else if (hasEntries &&
                     PreserveSlotQualifies(
                         slotEntries,
                         minimumOpaqueCoverageToPreservePercent))
            {
                outcome = SlotTransformOutcome.Fold;
            }
            else if (hasEntries &&
                     everySubmeshUnchanged &&
                     slotResults != null &&
                     slotIndex < slotResults.Count &&
                     slotResults[slotIndex].HasAnisotropicSampling)
            {
                outcome = SlotTransformOutcome.AnisotropyDifferences;
            }
            else if (splitSubmeshBelowCoverage)
            {
                outcome = SlotTransformOutcome.CoverageBelowMinimum;
            }
            else if (hasEntries)
            {
                outcome = SlotTransformOutcome.Split;
            }

            // The fold's unproven count is what MeshSeparationPlanner.Folded
            // would drop from the entry, read without building the folded
            // plan: the verdict never mutates the plan it judges.
            return new SlotTransformVerdict(
                slotIndex,
                outcome,
                proven,
                proven + unproven,
                outcome == SlotTransformOutcome.Fold ? unproven : 0);
        }
    }
}
