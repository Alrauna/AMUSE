using System;
using System.Collections.Generic;
using System.Globalization;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Runtime;
using nadena.dev.ndmf.animator;
using UnityEditor;

namespace Alrauna.Amuse.Editor.DryRun
{
    /// <summary>
    /// The inspector atlas view. It runs the shared
    /// <see cref="AtlasAnalysis"/> on the avatar in the scene, so the
    /// view shows the same grouping and the same refusals as a build of
    /// the same avatar. It reads only. It creates no texture, no
    /// material, and no mesh. A build can still differ: other tools
    /// change the build copy first, and a build withdraws a group when
    /// a source read fails.
    /// </summary>
    internal static class AtlasPreview
    {
        /// <summary>
        /// The inputs that change the result without a scene change.
        /// The view computes again when the key changes.
        /// </summary>
        internal static string InputKey(
            AmuseAvatarOptimizer optimizer,
            BuildTarget target)
        {
            return string.Join(
                "\u001f",
                optimizer.AmuseDisabled.ToString(),
                optimizer.AtlasingEnabled.ToString(),
                optimizer.MaximumAtlasPageSize.ToString(
                    CultureInfo.InvariantCulture),
                optimizer.ProtectedMipLevelCount.ToString(
                    CultureInfo.InvariantCulture),
                target.ToString(),
                (optimizer.transform.parent == null).ToString());
        }

        internal static AtlasPreviewResult Compute(
            AmuseAvatarOptimizer optimizer,
            BuildTarget target,
            AtlasBudgetLimits limits)
        {
            if (optimizer == null)
            {
                throw new ArgumentNullException(nameof(optimizer));
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return AtlasPreviewResult.Banner(
                    RowText.AtlasPlayModeSentence());
            }

            if (optimizer.transform.parent != null)
            {
                return AtlasPreviewResult.Banner(
                    RowText.BannerSentence(BannerKind.RootRequired));
            }

            if (optimizer.AmuseDisabled)
            {
                return AtlasPreviewResult.Banner(
                    RowText.BannerSentence(BannerKind.AmuseOff));
            }

            if (!optimizer.AtlasingEnabled)
            {
                return AtlasPreviewResult.Banner(RowText.AtlasOffSentence());
            }

            var gate = AtlasPass.AvatarGate(optimizer, target);
            if (gate != AtlasUseRefusal.None)
            {
                var key = AmuseReportStrings.AtlasKey(gate);
                return AtlasPreviewResult.Banner(
                    AmuseReportStrings.Get(key) + " " +
                    AmuseReportStrings.Get(key + ":description"));
            }

            var root = optimizer.gameObject;
            var graph = CommittedControllerGraph.Enumerate(
                root, VRChatPlatformAnimatorBindings.Instance);
            var analysis = AtlasAnalysis.Analyze(
                root,
                graph,
                VRChatPlatformAnimatorBindings.Instance,
                optimizer.MaximumAtlasPageSize,
                optimizer.ProtectedMipLevelCount,
                limits);

            var pageSets = new List<AtlasPageSet>();
            var atlased = 0;
            for (var group = 0; group < analysis.Plans.Count; group++)
            {
                var plan = analysis.Plans[group];
                if (analysis.Verdicts[group].Refusal != AtlasUseRefusal.None ||
                    plan.Pages.Count == 0 ||
                    plan.AdmittedUseIndices.Count == 0)
                {
                    continue;
                }

                var pageSet = AtlasAnalysis.PageSetOf(analysis, group);
                pageSets.Add(pageSet);
                atlased += pageSet.Members.Count;
            }

            return AtlasPreviewResult.Computed(
                analysis.Outcomes.Count,
                atlased,
                pageSets,
                analysis.Refusals);
        }
    }
}
