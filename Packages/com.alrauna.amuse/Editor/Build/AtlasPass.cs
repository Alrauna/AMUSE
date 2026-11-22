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
    /// Behind the gates it runs the atlas path of Milestone 2: it
    /// captures one use per renderer slot, builds one page per
    /// admitted group, and writes the outputs it changed.
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

        /// <summary>
        /// The acquisition step of each distinct source. Production
        /// runs <see cref="AcquireThroughStreamingEvidence"/>. The
        /// pass calls this seam, so a test can inject a failure and
        /// restore the default in its teardown.
        /// </summary>
        internal static AtlasSourceAcquisition AcquireSource =
            AcquireThroughStreamingEvidence;

        internal delegate bool AtlasSourceAcquisition(
            Texture2D source,
            int protectedMipLevelCount,
            out AtlasSourceImage image);

        internal static bool AcquireThroughStreamingEvidence(
            Texture2D source,
            int protectedMipLevelCount,
            out AtlasSourceImage image)
        {
            return UnityStreamingTextureEvidence.TryAcquireAtlasSource(
                source, protectedMipLevelCount, out image, out _);
        }

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

            var gate = AvatarGate(
                optimizer, AmusePlatformFinishPlugin.ActiveBuildTarget());
            if (gate != AtlasUseRefusal.None)
            {
                Refuse(context, state, gate);
                return;
            }

            state.ReachedAtlasAnalysis = true;
            Run(context, state, optimizer);
            Report(context, state);
        }

        /// <summary>
        /// The avatar-scope settings and target gates, in contract
        /// order: the maximum page size, the protected mip level count,
        /// then the build target. It returns the first cause, or
        /// <see cref="AtlasUseRefusal.None"/>. The inspector view calls
        /// the same gate, so both name the same cause.
        /// </summary>
        internal static AtlasUseRefusal AvatarGate(
            Alrauna.Amuse.Runtime.AmuseAvatarOptimizer optimizer,
            BuildTarget target)
        {
            if (optimizer.MaximumAtlasPageSize < 1 ||
                optimizer.MaximumAtlasPageSize >
                    Alrauna.Amuse.Runtime.AmuseAvatarOptimizer
                        .MaximumAtlasPageSizeLimit)
            {
                return AtlasUseRefusal.MaximumPageSizeOutOfRange;
            }

            if (optimizer.ProtectedMipLevelCount < 1 ||
                optimizer.ProtectedMipLevelCount >
                    Alrauna.Amuse.Runtime.AmuseAvatarOptimizer
                        .MaximumProtectedMipLevelCount)
            {
                return AtlasUseRefusal.ProtectedMipLevelCountOutOfRange;
            }

            if (target != PlatformAlphaSamplingPolicy.DesktopTarget)
            {
                return AtlasUseRefusal.UnsupportedBuildTarget;
            }

            return AtlasUseRefusal.None;
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

            var analysis = AtlasAnalysis.Analyze(
                avatarRoot,
                graph,
                bindings,
                optimizer.MaximumAtlasPageSize,
                optimizer.ProtectedMipLevelCount,
                AtlasBudgetLimits.Provider());
            state.AtlasSlotCount = analysis.Outcomes.Count;
            foreach (var refusal in analysis.Refusals)
            {
                state.RecordAtlasUseRefusal(refusal);
            }

            if (analysis.Uses.Count == 0)
            {
                return;
            }

            var uses = analysis.Uses;
            var admittedOutcomes = analysis.AdmittedOutcomes;
            var plans = analysis.Plans;
            var verdicts = analysis.Verdicts;

            // The first live outcome of each distinct source resolves
            // the source texture at acquisition time: the member
            // property for a main source, the route property for a
            // companion source.
            var mainSourceById =
                new Dictionary<int, (AtlasUseOutcome Outcome, string Property)>();
            var companionSourceById =
                new Dictionary<int, (AtlasUseOutcome Outcome, string Property)>();
            for (var index = 0; index < admittedOutcomes.Count; index++)
            {
                var id = uses[index].SourceTextureInstanceId;
                if (!mainSourceById.ContainsKey(id))
                {
                    mainSourceById.Add(
                        id, (admittedOutcomes[index], MemberProperty));
                }

                foreach (var companion in uses[index].Companions)
                {
                    if (!companionSourceById.ContainsKey(
                            companion.SourceTextureInstanceId))
                    {
                        companionSourceById.Add(
                            companion.SourceTextureInstanceId,
                            (admittedOutcomes[index], companion.Property));
                    }
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
                if (verdicts[group].Refusal != AtlasUseRefusal.None)
                {
                    continue;
                }

                BuildGroup(
                    state,
                    analysis,
                    mainSourceById,
                    companionSourceById,
                    group,
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
        /// The member property of both admitted rows. The page material
        /// binds the main page here.
        /// </summary>
        private const string MemberProperty = "_MainTex";

        /// <summary>
        /// Builds every page of one admitted group: each main page,
        /// then the companion pages that follow it, then its page
        /// material. A group that loses one source acquisition
        /// withdraws whole: every use of the group refuses with
        /// SourceAcquisitionFailed and every page of the group goes
        /// away. A build or check failure destroys the pages of the
        /// group and rethrows.
        /// </summary>
        private static void BuildGroup(
            AmusePlatformFinishState state,
            AtlasAnalysisResult analysis,
            Dictionary<int, (AtlasUseOutcome Outcome, string Property)>
                mainSourceById,
            Dictionary<int, (AtlasUseOutcome Outcome, string Property)>
                companionSourceById,
            int groupIndex,
            int protectedMipLevelCount,
            Dictionary<int, Material> pageMaterialByUse,
            Dictionary<int, AtlasPlacement> placementByUse,
            Dictionary<int, AtlasPagePlanEntry> pageByUse)
        {
            var plan = analysis.Plans[groupIndex];
            var uses = analysis.Uses;
            var admittedOutcomes = analysis.AdmittedOutcomes;

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
                for (var pageIndex = 0;
                    pageIndex < plan.Pages.Count;
                    pageIndex++)
                {
                    var page = plan.Pages[pageIndex];

                    // The names follow the plan order, so two runs of
                    // the same avatar name the same page.
                    var pageName =
                        "AMUSE atlas page " + groupIndex + "-" + pageIndex;
                    if (!TryBuildPage(
                            state,
                            page,
                            plan.Key.Sampler,
                            mainSourceById,
                            uses,
                            protectedMipLevelCount,
                            pageName,
                            built,
                            out var texture,
                            out var failedTexture,
                            out var changed))
                    {
                        WithdrawGroup(
                            state,
                            plan,
                            admittedOutcomes,
                            built,
                            pagesBuilt,
                            pageMaterialByUse,
                            placementByUse,
                            pageByUse,
                            changed
                                ? AtlasRefusalDetail.SourceChanged()
                                : AtlasRefusalDetail.SourceReadFailed(
                                    failedTexture),
                            failedTexture);
                        return;
                    }

                    var routePages = new List<(string Property, Texture2D Page)>();
                    foreach (var companionPage in plan.CompanionPages)
                    {
                        if (companionPage.MainPageIndex != pageIndex)
                        {
                            continue;
                        }

                        if (!TryBuildPage(
                                state,
                                companionPage.Entry,
                                companionPage.Sampler,
                                companionSourceById,
                                uses,
                                protectedMipLevelCount,
                                pageName + " " + companionPage.Property,
                                built,
                                out var routeTexture,
                                out failedTexture,
                                out changed))
                        {
                            WithdrawGroup(
                                state,
                                plan,
                                admittedOutcomes,
                                built,
                                pagesBuilt,
                                pageMaterialByUse,
                                placementByUse,
                                pageByUse,
                                changed
                                    ? AtlasRefusalDetail.SourceChanged()
                                    : AtlasRefusalDetail.SourceReadFailed(
                                        failedTexture),
                                failedTexture);
                            return;
                        }

                        routePages.Add((companionPage.Property, routeTexture));
                    }

                    var pageMaterial = NewPageMaterial(
                        firstMaterial,
                        texture,
                        uses[firstIndex].PageAliasProperties,
                        routePages);
                    pageMaterial.name =
                        "AMUSE atlas material " + groupIndex + "-" +
                        pageIndex;
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

                state.AtlasPageSets.Add(
                    AtlasAnalysis.PageSetOf(analysis, groupIndex));
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
        /// Acquires the sources of one page entry, builds the page,
        /// names it, and checks it. The texture joins the built list
        /// at once, so a check or ledger failure cannot leak it. It
        /// returns false on an acquisition failure, with the same
        /// out values as <see cref="TryAcquirePageSources"/>.
        /// </summary>
        private static bool TryBuildPage(
            AmusePlatformFinishState state,
            AtlasPagePlanEntry page,
            Semantics.AtlasSamplerFacts sampler,
            Dictionary<int, (AtlasUseOutcome Outcome, string Property)>
                sourceById,
            IReadOnlyList<CapturedAtlasUse> uses,
            int protectedMipLevelCount,
            string name,
            List<UnityEngine.Object> built,
            out Texture2D texture,
            out Texture2D failedTexture,
            out bool changed)
        {
            texture = null;
            var ledger = new AtlasByteLedger(
                AtlasBudget.PredictPagePeakBytes(
                    page, uses, protectedMipLevelCount));
            var sources = new Dictionary<int, AtlasSourceImage>();
            try
            {
                if (!TryAcquirePageSources(
                        page,
                        sourceById,
                        protectedMipLevelCount,
                        sources,
                        out failedTexture,
                        out changed))
                {
                    return false;
                }

                texture = AtlasPageBuilder.Build(
                    page,
                    sampler,
                    sources,
                    protectedMipLevelCount,
                    ledger);
                texture.name = name;
                built.Add(texture);
                state.AmuseOwnedObjects.Add(texture);
                PageCheck(
                    texture, page, sources,
                    protectedMipLevelCount, ledger);
                texture.Apply(false, true);
                return true;
            }
            finally
            {
                sources.Clear();
            }
        }

        /// <summary>
        /// One page material per main page: a clone of the first use
        /// material with the main page on the member texture and on
        /// every alias property, and each companion page on its route
        /// property. Unity's material copy constructor re-syncs drawer
        /// governed keywords to their controlling property values, so
        /// the clone can carry keywords the capture never checked. The
        /// source keyword array is written back, and a remaining
        /// difference is an invariant failure.
        /// </summary>
        private static Material NewPageMaterial(
            Material firstMaterial,
            Texture2D texture,
            IReadOnlyList<string> aliasProperties,
            IReadOnlyList<(string Property, Texture2D Page)> routePages)
        {
            var pageMaterial = new Material(firstMaterial);
            pageMaterial.shaderKeywords = firstMaterial.shaderKeywords;
            pageMaterial.SetTexture(MemberProperty, texture);
            foreach (var alias in aliasProperties)
            {
                pageMaterial.SetTexture(alias, texture);
            }

            foreach (var routePage in routePages)
            {
                pageMaterial.SetTexture(routePage.Property, routePage.Page);
            }

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
            Dictionary<int, AtlasPagePlanEntry> pageByUse,
            string detail,
            Texture failedTexture)
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
                var outcome = admittedOutcomes[useIndex];
                state.RecordAtlasUseRefusal(new AtlasUseRefusalRecord(
                    outcome.Renderer,
                    outcome.SlotIndex,
                    AtlasUseRefusal.SourceAcquisitionFailed,
                    outcome.Material,
                    failedTexture,
                    detail));
            }
        }

        /// <summary>
        /// Acquires every distinct source of one page. The live
        /// material of the first use of each source resolves the
        /// texture through the property the source sits on, and a
        /// missing or changed texture is an acquisition failure,
        /// because the capture saw it live. On a failure,
        /// <paramref name="changed"/> is true when the texture went
        /// missing or was replaced, and <paramref name="failedTexture"/>
        /// holds the texture that could not be read otherwise.
        /// </summary>
        private static bool TryAcquirePageSources(
            AtlasPagePlanEntry page,
            Dictionary<int, (AtlasUseOutcome Outcome, string Property)>
                sourceById,
            int protectedMipLevelCount,
            Dictionary<int, AtlasSourceImage> sources,
            out Texture2D failedTexture,
            out bool changed)
        {
            failedTexture = null;
            changed = false;
            foreach (var placement in page.Placements)
            {
                var id = placement.SourceTextureInstanceId;
                if (sources.ContainsKey(id))
                {
                    continue;
                }

                var (outcome, property) = sourceById[id];
                var material =
                    outcome.Renderer.sharedMaterials[outcome.SlotIndex];
                var texture = material == null
                    ? null
                    : material.GetTexture(property) as Texture2D;
                if (texture == null || texture.GetInstanceID() != id)
                {
                    changed = true;
                    return false;
                }

                if (!AcquireSource(
                        texture, protectedMipLevelCount, out var image))
                {
                    failedTexture = texture;
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
        /// R4: one summary line for the avatar, then one line per
        /// recorded use refusal in record order, then one line per
        /// page set in plan order. The report runs after the write, so
        /// a thrown invariant failure reports nothing.
        /// </summary>
        private static void Report(
            BuildContext context,
            AmusePlatformFinishState state)
        {
            AmuseReports.AtlasSummaryLine(
                context.AvatarRootObject,
                state.AtlasSlotCount,
                state.AtlasAdmittedUseCount,
                state.AtlasPageSets,
                state.AtlasUseRefusals);

            foreach (var refusal in state.AtlasUseRefusals)
            {
                AmuseReports.AtlasUseLine(refusal);
            }

            foreach (var pageSet in state.AtlasPageSets)
            {
                AmuseReports.AtlasPageSetLine(
                    context.AvatarRootObject, pageSet);
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
