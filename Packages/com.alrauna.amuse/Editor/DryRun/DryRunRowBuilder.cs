using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using UnityEngine;

namespace Alrauna.Amuse.Editor.DryRun
{
    internal static class DryRunRowBuilder
    {
        internal static IReadOnlyList<MaterialTransformationRow> BuildResolvedRefusalRows(
            Renderer renderer,
            string rendererPath,
            string rendererTypeName,
            RuntimeStateResolution.ResolvedRuntimeStates resolved,
            IReadOnlyList<CapturedMaterialSlotEvidence> slots)
        {
            var rows = new List<MaterialTransformationRow>();
            var allDeferred = true;
            var hasUnresolved = false;

            for (var slotIndex = 0;
                 slotIndex < resolved.SlotResults.Length;
                 slotIndex++)
            {
                if (resolved.SlotResults[slotIndex].IsResolved)
                {
                    continue;
                }

                hasUnresolved = true;
                var slotEvidence = slotIndex < slots.Count ? slots[slotIndex] : null;
                if (!AlphaSeparationDryRun.IsSlotDeferred(renderer, slotIndex, resolved.SlotResults[slotIndex], slotEvidence))
                {
                    allDeferred = false;
                }

                rows.Add(RefusedSlotRow(
                    renderer,
                    rendererPath,
                    rendererTypeName,
                    resolved.SlotResults[slotIndex],
                    slotEvidence,
                    slotIndex,
                    renderer.gameObject.name));
            }

            if (!hasUnresolved || !allDeferred)
            {
                rows.Insert(0, RendererRefusedRow(
                    rendererPath, rendererTypeName, resolved.Refusal,
                    -1, -1, renderer.gameObject.name));
            }

            return rows;
        }

        internal static IReadOnlyList<MaterialTransformationRow> BuildRows(
            Renderer renderer,
            string rendererPath,
            string rendererTypeName,
            IReadOnlyList<SlotTransformVerdict> verdicts,
            SlotResolutionResult[] slotResults,
            IReadOnlyList<CapturedMaterialSlotEvidence> slots,
            string rendererName)
        {
            var rows = new List<MaterialTransformationRow>();
            for (var slotIndex = 0; slotIndex < slotResults.Length; slotIndex++)
            {
                if (!slotResults[slotIndex].IsResolved)
                {
                    rows.Add(RefusedSlotRow(
                        renderer,
                        rendererPath,
                        rendererTypeName,
                        slotResults[slotIndex],
                        slotIndex < slots.Count ? slots[slotIndex] : null,
                        slotIndex,
                        rendererName));
                    continue;
                }

                if (slotIndex >= verdicts.Count)
                {
                    continue;
                }

                var verdict = verdicts[slotIndex];
                var sentence = SlotSentence(verdict);
                if (sentence == null)
                {
                    continue;
                }

                rows.Add(MaterialTransformationRow.Slot(
                    rendererPath,
                    rendererTypeName,
                    verdict.SlotIndex,
                    verdict.ProvenTriangles,
                    verdict.TotalTriangles,
                    sentence));
            }

            return rows;
        }

        private static MaterialTransformationRow RefusedSlotRow(
            Renderer renderer,
            string rendererPath,
            string rendererTypeName,
            SlotResolutionResult result,
            CapturedMaterialSlotEvidence slot,
            int slotIndex,
            string rendererName)
        {
            var totalTriangles = AlphaSeparationDryRun.SubmeshPolygonCount(renderer, slotIndex);
            if (AlphaSeparationDryRun.IsSlotDeferred(renderer, slotIndex, result, slot))
            {
                return MaterialTransformationRow.Slot(
                    rendererPath,
                    rendererTypeName,
                    slotIndex,
                    0,
                    totalTriangles,
                    RowText.DeferredSlotSentence(totalTriangles));
            }

            return MaterialTransformationRow.Slot(
                rendererPath,
                rendererTypeName,
                slotIndex,
                -1,
                totalTriangles,
                RowText.RefusedSlotSentence());
        }

        private static string SlotSentence(SlotTransformVerdict verdict)
        {
            switch (verdict.Outcome)
            {
                case SlotTransformOutcome.Split:
                    if (verdict.TotalTriangles > 0 && verdict.ProvenTriangles == verdict.TotalTriangles)
                    {
                        return RowText.ConvertableSlotSentence(verdict.TotalTriangles);
                    }
                    if (verdict.ProvenTriangles > 0)
                    {
                        return RowText.SplittableSlotSentence(
                            verdict.ProvenTriangles, verdict.TotalTriangles);
                    }
                    return RowText.UntouchableSlotSentence(verdict.TotalTriangles);
                case SlotTransformOutcome.Fold:
                    return RowText.ConvertableSlotSentence(verdict.TotalTriangles);
                case SlotTransformOutcome.AlreadyOpaque:
                case SlotTransformOutcome.CoverageBelowMinimum:
                    return RowText.UntouchableSlotSentence(verdict.TotalTriangles);
                case SlotTransformOutcome.AnisotropyDifferences:
                    return RowText.RefusedSlotSentence();
                default:
                    return RowText.UntouchableSlotSentence(verdict.TotalTriangles);
            }
        }

        internal static MaterialTransformationRow RendererRefusedRow(
            string rendererPath,
            string rendererTypeName,
            RendererAnalysisRefusal refusal,
            int meshSubMeshCount,
            int materialSlotCount,
            string rendererName)
        {
            return MaterialTransformationRow.Renderer(
                rendererPath,
                rendererTypeName,
                -1, -1, -1,
                RowText.RendererRefusedSentence(
                    rendererPath,
                    rendererTypeName,
                    AmuseReportStrings.RendererKey(refusal),
                    SlotCountPhrase(meshSubMeshCount),
                    SlotCountPhrase(materialSlotCount),
                    rendererName,
                    ""));
        }

        private static string SlotCountPhrase(int count)
        {
            if (count < 0)
            {
                return "an unknown number of material slots";
            }

            return count == 1
                ? "1 material slot"
                : count + " material slots";
        }
    }
}
