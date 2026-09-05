using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The AMUSE localizer and the typed report helpers. Every message is
    /// Information severity: a refusal is a normal AMUSE outcome, never a
    /// build failure. Each renderer report carries the renderer as its
    /// context object, so the NDMF console links straight to the object.
    /// </summary>
    internal static class AmuseReports
    {
        internal static readonly Localizer Localizer =
            new Localizer(
                "en-us",
                () => new List<(string, Func<string, string>)>
                {
                    ("en-us", AmuseReportStrings.Get),
                });

        internal static void RendererRefusal(
            Renderer renderer, RendererAnalysisRefusal cause)
        {
            using (ErrorReport.WithContextObject(renderer))
            {
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    AmuseReportStrings.RendererKey(cause));
            }
        }

        internal static void ConsentDeclined(
            IReadOnlyList<string> subjects)
        {
            ErrorReport.ReportError(
                Localizer,
                ErrorSeverity.Information,
                "amuse.consent.Declined");
            foreach (var subject in subjects)
            {
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    "amuse.consent.Subject",
                    subject);
            }
        }

        internal static void AvatarSummary(
            GameObject avatarRoot,
            int analyzedRenderers,
            int movedTriangles,
            int untouchedRenderers)
        {
            var summary = string.Format(
                AmuseReportStrings.Get(
                    "amuse.summary.Title:description"),
                analyzedRenderers,
                movedTriangles,
                untouchedRenderers);

            AmuseBuildStatusStore.Record(
                avatarRoot.GetInstanceID(),
                "Last upload: " + summary);

            using (ErrorReport.WithContextObject(avatarRoot))
            {
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    "amuse.summary.Title",
                    analyzedRenderers,
                    movedTriangles,
                    untouchedRenderers);
            }
        }
    }

    /// <summary>
    /// Session-scoped last-build status for the component inspector. The
    /// store lives for the editor session only and carries nothing across
    /// restarts; the avatar root's instance ID is the key.
    /// </summary>
    internal static class AmuseBuildStatusStore
    {
        private static readonly Dictionary<int, string> Status =
            new Dictionary<int, string>();

        internal static void Record(int avatarInstanceId, string summary)
        {
            Status[avatarInstanceId] = summary;
        }

        internal static bool TryGet(int avatarInstanceId, out string summary)
        {
            return Status.TryGetValue(avatarInstanceId, out summary);
        }

        /// <summary>Removes one avatar's status. Tests use this so a fixed
        /// instance ID cannot leak state between runs.</summary>
        internal static void Forget(int avatarInstanceId)
        {
            Status.Remove(avatarInstanceId);
        }
    }
}