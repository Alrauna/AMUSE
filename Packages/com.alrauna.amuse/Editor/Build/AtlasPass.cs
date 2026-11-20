using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The sixth AMUSE pass. It reads the shared gate verdict the
    /// barrier recorded and applies every avatar-scope atlas gate,
    /// naming each refusal. The pass never reads
    /// <see cref="AmusePlatformFinishState.Separation"/>, because an
    /// atlas page reads materials, not the alpha proof plan.
    /// Milestone 1 stops at the analysis point. Milestone 2 adds the
    /// first atlas path there.
    /// <para>
    /// The check order below is part of the contract. The settings
    /// gates run before the target gate, so a wrong setting names
    /// itself on every machine, and the once-only guard runs first so
    /// a double registration can never report twice.
    /// </para>
    /// </summary>
    internal static class AtlasPass
    {
        internal const string PassName = "AMUSE atlas";

        /// <summary>
        /// The output check of each built page. Production runs
        /// <see cref="AtlasPageCheck.Verify"/>. The pass calls this
        /// seam between the page build and the read only close, so a
        /// test can inject a failure and restore the default in its
        /// teardown.
        /// </summary>
        internal static Action<Texture2D, AtlasPagePlanEntry,
            IReadOnlyDictionary<int, AtlasSourceImage>, int,
            AtlasByteLedger> PageCheck = AtlasPageCheck.Verify;

        internal static void Execute(BuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var state = context.GetState<AmusePlatformFinishState>();
            if (state.HasAtlasExecuted)
            {
                throw new InvalidOperationException(
                    "AMUSE atlas pass executed more than once.");
            }

            state.HasAtlasExecuted = true;

            // The barrier owns every shared report. This pass runs
            // only behind it, so a missing lifecycle verdict is a
            // caller defect, not a refusal.
            if (state.Lifecycle == null)
            {
                throw new InvalidOperationException(
                    "AMUSE atlas pass ran before the barrier pass.");
            }

            var optimizer = AmusePlatformFinishPass.ActivatedOptimizer(
                context);
            if (optimizer == null)
            {
                return;
            }

            if (!optimizer.AtlasingEnabled)
            {
                return;
            }

            if (!state.SharedGatesPassed)
            {
                // The barrier already reported the cause.
                return;
            }

            if (optimizer.MaximumAtlasPageSize < 1 ||
                optimizer.MaximumAtlasPageSize >
                    Alrauna.Amuse.Runtime.AmuseAvatarOptimizer
                        .MaximumAtlasPageSizeLimit)
            {
                Refuse(
                    context, state,
                    AtlasUseRefusal.MaximumPageSizeOutOfRange);
                return;
            }

            if (optimizer.ProtectedMipLevelCount < 1 ||
                optimizer.ProtectedMipLevelCount >
                    Alrauna.Amuse.Runtime.AmuseAvatarOptimizer
                        .MaximumProtectedMipLevelCount)
            {
                Refuse(
                    context, state,
                    AtlasUseRefusal.ProtectedMipLevelCountOutOfRange);
                return;
            }

            if (AmusePlatformFinishPlugin.ActiveBuildTarget() !=
                PlatformAlphaSamplingPolicy.DesktopTarget)
            {
                Refuse(
                    context, state,
                    AtlasUseRefusal.UnsupportedBuildTarget);
                return;
            }

            state.ReachedAtlasAnalysis = true;
            Run(context, state, optimizer);
        }

        /// <summary>
        /// The atlas path after the analysis point: capture one use
        /// per renderer slot, record every refusal, plan the groups,
        /// admit them against the budget, build one page at a time,
        /// assemble one mesh per touched renderer, then write.
        /// </summary>
        private static void Run(
            BuildContext context,
            AmusePlatformFinishState state,
            Alrauna.Amuse.Runtime.AmuseAvatarOptimizer optimizer)
        {
            var avatarRoot = context.AvatarRootObject;

            // E1: the graph is enumerated again after the animator
            // scope closed, exactly as the window close does, because
            // the stored structural graph predates the curves that
            // alpha apply added.
            var bindings = state.AnimatorBindings;
            var graph = bindings == null
                ? null
                : CommittedControllerGraph.Enumerate(avatarRoot, bindings);

            var outcomes = AtlasUseCapture.Capture(
                avatarRoot,
                graph,
                bindings,
                optimizer.ProtectedMipLevelCount);

            foreach (var outcome in outcomes)
            {
                if (outcome.Refusal != AtlasUseRefusal.None)
                {
                    state.RecordAtlasUseRefusal(
                        outcome.Renderer,
                        outcome.SlotIndex,
                        outcome.Refusal);
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
                return;
            }

            var plans = AtlasPlacementPlanner.Plan(
                uses,
                optimizer.MaximumAtlasPageSize,
                optimizer.ProtectedMipLevelCount);
            foreach (var plan in plans)
            {
                foreach (var refused in plan.RefusedUses)
                {
                    state.RecordAtlasUseRefusal(
                        admittedOutcomes[refused.UseIndex].Renderer,
                        admittedOutcomes[refused.UseIndex].SlotIndex,
                        MapPlacementRefusal(refused.Refusal));
                }
            }

            var admissions = AtlasBudgetAdmission.Admit(
                plans,
                uses,
                optimizer.ProtectedMipLevelCount,
                AtlasBudgetLimits.Provider());
            for (var group = 0; group < plans.Count; group++)
            {
                if (admissions[group] == AtlasUseRefusal.None)
                {
                    continue;
                }

                foreach (var useIndex in plans[group].AdmittedUseIndices)
                {
                    state.RecordAtlasUseRefusal(
                        admittedOutcomes[useIndex].Renderer,
                        admittedOutcomes[useIndex].SlotIndex,
                        admissions[group]);
                }
            }

            // The first live outcome of each distinct source resolves
            // the source texture at acquisition time.
            var outcomeBySource =
                new Dictionary<int, AtlasUseOutcome>();
            for (var index = 0; index < admittedOutcomes.Count; index++)
            {
                var id = uses[index].SourceTextureInstanceId;
                if (!outcomeBySource.ContainsKey(id))
                {
                    outcomeBySource.Add(id, admittedOutcomes[index]);
                }
            }

            // The outputs are captured before any page is built.
            var outputs = new List<AtlasRendererOutput>();
            var outputsByRenderer =
                new Dictionary<Renderer, AtlasRendererOutput>();
            for (var index = 0; index < admittedOutcomes.Count; index++)
            {
                var renderer = admittedOutcomes[index].Renderer;
                if (!outputsByRenderer.TryGetValue(
                        renderer, out var output))
                {
                    output = new AtlasRendererOutput(avatarRoot, renderer);
                    outputs.Add(output);
                    outputsByRenderer.Add(renderer, output);
                }

                output.UseIndices.Add(index);
            }

            var pageMaterialByUse = new Dictionary<int, Material>();
            var placementByUse =
                new Dictionary<int, AtlasPlacement>();
            var pageByUse =
                new Dictionary<int, AtlasPagePlanEntry>();
            for (var group = 0; group < plans.Count; group++)
            {
                if (admissions[group] != AtlasUseRefusal.None)
                {
                    continue;
                }

                BuildGroup(
                    state,
                    uses,
                    admittedOutcomes,
                    outcomeBySource,
                    plans[group],
                    optimizer.ProtectedMipLevelCount,
                    pageMaterialByUse,
                    placementByUse,
                    pageByUse);
            }

            foreach (var output in outputs)
            {
                AssembleOutput(
                    state,
                    uses,
                    output,
                    pageMaterialByUse,
                    placementByUse,
                    pageByUse);
            }

            state.AtlasAdmittedUseCount = pageMaterialByUse.Count;
            AtlasWrite.Apply(
                state, outputs, CollectCurveReferences(graph));
        }

        /// <summary>
        /// Every object the committed object reference curves name.
        /// The cleanup keeps each owned member of this set, because a
        /// curve keyframe references objects no renderer holds.
        /// </summary>
        private static HashSet<UnityEngine.Object>
            CollectCurveReferences(CommittedControllerGraphResult graph)
        {
            var referenced = new HashSet<UnityEngine.Object>();
            if (graph == null)
            {
                return referenced;
            }

            foreach (var layer in graph.Layers)
            {
                foreach (var clip in layer.Clips)
                {
                    foreach (var binding in AnimationUtility
                        .GetObjectReferenceCurveBindings(clip))
                    {
                        foreach (var frame in AnimationUtility
                            .GetObjectReferenceCurve(clip, binding))
                        {
                            if (frame.value != null)
                            {
                                referenced.Add(frame.value);
                            }
                        }
                    }
                }
            }

            return referenced;
        }

        /// <summary>
        /// Builds every page of one admitted group. A group that
        /// loses one source acquisition withdraws whole: every use of
        /// the group refuses with SourceAcquisitionFailed and every
        /// page of the group goes away. A build or check failure
        /// destroys the pages of the group and rethrows.
        /// </summary>
        private static void BuildGroup(
            AmusePlatformFinishState state,
            IReadOnlyList<CapturedAtlasUse> uses,
            IReadOnlyList<AtlasUseOutcome> admittedOutcomes,
            Dictionary<int, AtlasUseOutcome> outcomeBySource,
            AtlasGroupPlan plan,
            int protectedMipLevelCount,
            Dictionary<int, Material> pageMaterialByUse,
            Dictionary<int, AtlasPlacement> placementByUse,
            Dictionary<int, AtlasPagePlanEntry> pageByUse)
        {
            // A group whose every placement refused has no page and
            // no admitted use. Its refusals are already recorded, so
            // the build simply moves on.
            if (plan.Pages.Count == 0 ||
                plan.AdmittedUseIndices.Count == 0)
            {
                return;
            }

            var built = new List<UnityEngine.Object>();
            var pagesBuilt = 0;
            try
            {
                var firstIndex = plan.AdmittedUseIndices[0];
                var firstOutcome = admittedOutcomes[firstIndex];
                var firstMaterial =
                    firstOutcome.Renderer.sharedMaterials[
                        firstOutcome.SlotIndex];
                foreach (var page in plan.Pages)
                {
                    var ledger = new AtlasByteLedger(
                        AtlasBudget.PredictPagePeakBytes(
                            page, uses, protectedMipLevelCount));
                    var sources = new Dictionary<int, AtlasSourceImage>();
                    try
                    {
                        if (!TryAcquirePageSources(
                                page,
                                outcomeBySource,
                                protectedMipLevelCount,
                                sources))
                        {
                            WithdrawGroup(
                                state,
                                plan,
                                admittedOutcomes,
                                built,
                                pagesBuilt,
                                pageMaterialByUse,
                                placementByUse,
                                pageByUse);
                            return;
                        }

                        var texture = AtlasPageBuilder.Build(
                            page,
                            plan.Key.Sampler,
                            sources,
                            protectedMipLevelCount,
                            ledger);
                        // The texture joins the built list at once, so
                        // a check or ledger failure below cannot leak
                        // it.
                        built.Add(texture);
                        state.AmuseOwnedObjects.Add(texture);
                        PageCheck(
                            texture, page, sources,
                            protectedMipLevelCount, ledger);
                        texture.Apply(false, true);

                        var pageMaterial =
                            NewPageMaterial(firstMaterial, texture);
                        built.Add(pageMaterial);
                        state.AmuseOwnedObjects.Add(pageMaterial);
                        state.AtlasPageCount++;
                        pagesBuilt++;
                        foreach (var placement in page.Placements)
                        {
                            foreach (var useIndex in placement.UseIndices)
                            {
                                pageMaterialByUse[useIndex] = pageMaterial;
                                placementByUse[useIndex] = placement;
                                pageByUse[useIndex] = page;
                            }
                        }
                    }
                    finally
                    {
                        sources.Clear();
                    }
                }
            }
            catch
            {
                foreach (var owned in built)
                {
                    state.AmuseOwnedObjects.Remove(owned);
                    UnityEngine.Object.DestroyImmediate(owned);
                }

                state.AtlasPageCount -= pagesBuilt;
                throw;
            }
        }

        /// <summary>
        /// One page material per page: a clone of the first use
        /// material with the page on the member texture. Unity's
        /// material copy constructor re-syncs drawer governed
        /// keywords to their controlling property values, so the
        /// clone can carry keywords the capture never checked. The
        /// source keyword array is written back, and a remaining
        /// difference is an invariant failure.
        /// </summary>
        private static Material NewPageMaterial(
            Material firstMaterial,
            Texture2D texture)
        {
            var pageMaterial = new Material(firstMaterial);
            pageMaterial.shaderKeywords = firstMaterial.shaderKeywords;
            pageMaterial.SetTexture("_MainTex", texture);
            var sourceKeywords = new HashSet<string>(
                firstMaterial.shaderKeywords,
                StringComparer.Ordinal);
            var pageKeywords = new HashSet<string>(
                pageMaterial.shaderKeywords,
                StringComparer.Ordinal);
            if (!sourceKeywords.SetEquals(pageKeywords))
            {
                throw new InvalidOperationException(
                    "The page material of an atlas page carries a" +
                    " different enabled keyword set than its source" +
                    " material. The copy and the capture disagree.");
            }

            return pageMaterial;
        }

        /// <summary>
        /// E12: one failed acquisition withdraws the whole group.
        /// Every use of the group refuses, every page of the group is
        /// destroyed, and the group is never planned again.
        /// </summary>
        private static void WithdrawGroup(
            AmusePlatformFinishState state,
            AtlasGroupPlan plan,
            IReadOnlyList<AtlasUseOutcome> admittedOutcomes,
            List<UnityEngine.Object> built,
            int pagesBuilt,
            Dictionary<int, Material> pageMaterialByUse,
            Dictionary<int, AtlasPlacement> placementByUse,
            Dictionary<int, AtlasPagePlanEntry> pageByUse)
        {
            foreach (var owned in built)
            {
                state.AmuseOwnedObjects.Remove(owned);
                UnityEngine.Object.DestroyImmediate(owned);
            }

            state.AtlasPageCount -= pagesBuilt;
            foreach (var useIndex in plan.AdmittedUseIndices)
            {
                pageMaterialByUse.Remove(useIndex);
                placementByUse.Remove(useIndex);
                pageByUse.Remove(useIndex);
                state.RecordAtlasUseRefusal(
                    admittedOutcomes[useIndex].Renderer,
                    admittedOutcomes[useIndex].SlotIndex,
                    AtlasUseRefusal.SourceAcquisitionFailed);
            }
        }

        /// <summary>
        /// Acquires every distinct source of one page. The live
        /// material of the first use of each source resolves the
        /// texture, and a missing or changed texture is an
        /// acquisition failure, because the capture saw it live.
        /// </summary>
        private static bool TryAcquirePageSources(
            AtlasPagePlanEntry page,
            Dictionary<int, AtlasUseOutcome> outcomeBySource,
            int protectedMipLevelCount,
            Dictionary<int, AtlasSourceImage> sources)
        {
            foreach (var placement in page.Placements)
            {
                var id = placement.SourceTextureInstanceId;
                if (sources.ContainsKey(id))
                {
                    continue;
                }

                var outcome = outcomeBySource[id];
                var material =
                    outcome.Renderer.sharedMaterials[outcome.SlotIndex];
                var texture = material == null
                    ? null
                    : material.GetTexture("_MainTex") as Texture2D;
                if (texture == null || texture.GetInstanceID() != id)
                {
                    return false;
                }

                if (!UnityStreamingTextureEvidence.TryAcquireAtlasSource(
                        texture,
                        protectedMipLevelCount,
                        out var image,
                        out _))
                {
                    return false;
                }

                sources.Add(id, image);
            }

            return true;
        }

        /// <summary>
        /// Fills the new slot array and the new mesh of one output.
        /// A slot the pass did not admit keeps its captured material,
        /// and a renderer with no admitted use keeps its whole
        /// output unwritten.
        /// </summary>
        private static void AssembleOutput(
            AmusePlatformFinishState state,
            IReadOnlyList<CapturedAtlasUse> uses,
            AtlasRendererOutput output,
            Dictionary<int, Material> pageMaterialByUse,
            Dictionary<int, AtlasPlacement> placementByUse,
            Dictionary<int, AtlasPagePlanEntry> pageByUse)
        {
            var newSlots = (Material[])output.Slots.Clone();
            var remaps = new List<AtlasSubmeshRemap>();
            foreach (var useIndex in output.UseIndices)
            {
                if (!pageMaterialByUse.TryGetValue(
                        useIndex, out var pageMaterial))
                {
                    continue;
                }

                var use = uses[useIndex];
                var placement = placementByUse[useIndex];
                var page = pageByUse[useIndex];
                newSlots[use.SlotIndex] = pageMaterial;
                remaps.Add(new AtlasSubmeshRemap(
                    use.SubmeshIndex,
                    use.SourceWidth,
                    use.SourceHeight,
                    placement.SourceX,
                    placement.SourceY,
                    placement.PageX,
                    placement.PageY,
                    page.Width,
                    page.Height));
            }

            if (remaps.Count == 0)
            {
                return;
            }

            output.NewSlots = newSlots;
            output.NewMesh = AtlasMeshBuilder.Build(output.Mesh, remaps);
            state.AmuseOwnedObjects.Add(output.NewMesh);
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

        /// <summary>
        /// Records one avatar-scope refusal and reports it once. The
        /// avatar keeps every original texture after a refusal.
        /// </summary>
        private static void Refuse(
            BuildContext context,
            AmusePlatformFinishState state,
            AtlasUseRefusal cause)
        {
            state.AtlasRefusal = cause;
            AmuseReports.AtlasRefusal(context.AvatarRootObject, cause);
        }
    }
}
