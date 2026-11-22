using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf.animator;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The read-only part of the atlas path: capture one use per
    /// renderer slot, plan the groups, and admit them against the
    /// budget. The build pass and the inspector view both call it, so
    /// the view shows the same grouping and the same refusals as the
    /// build. It creates no texture, no material, and no mesh.
    /// </summary>
    internal static class AtlasAnalysis
    {
        internal static AtlasAnalysisResult Analyze(
            GameObject avatarRoot,
            CommittedControllerGraphResult graph,
            IPlatformAnimatorBindings bindings,
            int maximumPageSize,
            int protectedMipLevelCount,
            AtlasBudgetLimits limits)
        {
            var outcomes = AtlasUseCapture.Capture(
                avatarRoot,
                graph,
                bindings,
                protectedMipLevelCount);

            var refusals = new List<AtlasUseRefusalRecord>();
            foreach (var outcome in outcomes)
            {
                if (outcome.Refusal != AtlasUseRefusal.None)
                {
                    refusals.Add(new AtlasUseRefusalRecord(
                        outcome.Renderer,
                        outcome.SlotIndex,
                        outcome.Refusal,
                        outcome.Material,
                        outcome.MemberTexture,
                        outcome.Detail));
                }
            }

            var admittedOutcomes = new List<AtlasUseOutcome>();
            foreach (var outcome in outcomes)
            {
                if (outcome.Refusal == AtlasUseRefusal.None)
                {
                    admittedOutcomes.Add(outcome);
                }
            }

            var uses = new List<CapturedAtlasUse>(admittedOutcomes.Count);
            foreach (var outcome in admittedOutcomes)
            {
                uses.Add(outcome.Use);
            }

            if (uses.Count == 0)
            {
                return new AtlasAnalysisResult(
                    outcomes,
                    admittedOutcomes,
                    uses,
                    Array.Empty<AtlasGroupPlan>(),
                    Array.Empty<AtlasBudgetVerdict>(),
                    refusals);
            }

            var plans = AtlasPlacementPlanner.Plan(
                uses,
                maximumPageSize,
                protectedMipLevelCount);
            foreach (var plan in plans)
            {
                foreach (var refused in plan.RefusedUses)
                {
                    var outcome = admittedOutcomes[refused.UseIndex];
                    refusals.Add(new AtlasUseRefusalRecord(
                        outcome.Renderer,
                        outcome.SlotIndex,
                        MapPlacementRefusal(refused.Refusal),
                        outcome.Material,
                        outcome.MemberTexture,
                        PlacementDetail(
                            refused, outcome, maximumPageSize)));
                }
            }

            var verdicts = AtlasBudgetAdmission.Admit(
                plans,
                uses,
                protectedMipLevelCount,
                limits);
            for (var group = 0; group < plans.Count; group++)
            {
                var verdict = verdicts[group];
                if (verdict.Refusal == AtlasUseRefusal.None)
                {
                    continue;
                }

                foreach (var useIndex in plans[group].AdmittedUseIndices)
                {
                    var outcome = admittedOutcomes[useIndex];
                    refusals.Add(new AtlasUseRefusalRecord(
                        outcome.Renderer,
                        outcome.SlotIndex,
                        verdict.Refusal,
                        outcome.Material,
                        outcome.MemberTexture,
                        string.Empty,
                        verdict.ValueBytes,
                        verdict.LimitBytes));
                }
            }

            return new AtlasAnalysisResult(
                outcomes,
                admittedOutcomes,
                uses,
                plans,
                verdicts,
                refusals);
        }

        /// <summary>
        /// The report facts of one group whose pages all build. The
        /// source count and bytes cover the distinct sources of the
        /// admitted uses, as the budget counts them. The members follow
        /// the admitted use order of the plan.
        /// </summary>
        internal static AtlasPageSet PageSetOf(
            AtlasAnalysisResult analysis,
            int groupIndex)
        {
            var plan = analysis.Plans[groupIndex];
            var sources = new HashSet<int>();
            foreach (var useIndex in plan.AdmittedUseIndices)
            {
                sources.Add(analysis.Uses[useIndex].SourceTextureInstanceId);
            }

            var pages = new List<AtlasPageSetPage>(plan.Pages.Count);
            var pageIndexByUse = new Dictionary<int, int>();
            for (var pageIndex = 0; pageIndex < plan.Pages.Count; pageIndex++)
            {
                var page = plan.Pages[pageIndex];
                pages.Add(new AtlasPageSetPage(
                    page.Width,
                    page.Height,
                    page.Format,
                    page.MipCount,
                    page.Placements.Count));
                foreach (var placement in page.Placements)
                {
                    foreach (var useIndex in placement.UseIndices)
                    {
                        pageIndexByUse[useIndex] = pageIndex;
                    }
                }
            }

            var members = new List<AtlasPageSetMember>(
                plan.AdmittedUseIndices.Count);
            foreach (var useIndex in plan.AdmittedUseIndices)
            {
                var outcome = analysis.AdmittedOutcomes[useIndex];
                var use = analysis.Uses[useIndex];
                members.Add(new AtlasPageSetMember(
                    outcome.Renderer,
                    outcome.SlotIndex,
                    outcome.Material,
                    outcome.MemberTexture,
                    use.SourceWidth,
                    use.SourceHeight,
                    pageIndexByUse[useIndex]));
            }

            return new AtlasPageSet(
                groupIndex,
                plan.Key.ShaderRowName,
                plan.Pages.Count,
                plan.AdmittedUseIndices.Count,
                AtlasBudget.GroupRetainedBytes(plan),
                sources.Count,
                AtlasBudget.SourceBytes(plan, analysis.Uses),
                pages,
                members);
        }

        private static string PlacementDetail(
            AtlasRefusedUse refused,
            AtlasUseOutcome outcome,
            int maximumPageSize)
        {
            switch (refused.Refusal)
            {
                case AtlasPlacementRefusal.PlacementTooLarge:
                    return AtlasRefusalDetail.PlacementTooLarge(
                        refused.RegionWidth,
                        refused.RegionHeight,
                        maximumPageSize);
                case AtlasPlacementRefusal.ClampEdgeOnBc7:
                    return AtlasRefusalDetail.ClampEdgeOnBc7(
                        outcome.MemberTexture);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(refused));
            }
        }

        /// <summary>
        /// Maps a placement refusal of the Analysis layer onto the
        /// use refusal of the Build layer.
        /// </summary>
        private static AtlasUseRefusal MapPlacementRefusal(
            AtlasPlacementRefusal refusal)
        {
            switch (refusal)
            {
                case AtlasPlacementRefusal.PlacementTooLarge:
                    return AtlasUseRefusal.PlacementTooLarge;
                case AtlasPlacementRefusal.ClampEdgeOnBc7:
                    return AtlasUseRefusal.ClampEdgeOnBc7;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(refusal));
            }
        }
    }
}
