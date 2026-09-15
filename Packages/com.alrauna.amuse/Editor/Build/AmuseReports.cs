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

        /// <summary>
        /// One Information entry per refused material slot, so a build
        /// names the exact slot and the exact rule that stopped its proof.
        /// A <see cref="RendererAnalysisRefusal.None"/> is not a refusal,
        /// so it throws.
        /// </summary>
        internal static void SlotAnalysisRefusal(
            Renderer renderer,
            int slotIndex,
            RendererAnalysisRefusal cause,
            string rendererName = null)
        {
            if (cause == RendererAnalysisRefusal.None)
            {
                throw new InvalidOperationException(
                    "RendererAnalysisRefusal.None is not a refusal.");
            }

            using (ErrorReport.WithContextObject(renderer))
            {
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    AmuseReportStrings.SlotAnalysisKey(cause),
                    slotIndex,
                    cause.ToString(),
                    rendererName);
            }
        }

        /// <summary>
        /// One Information entry per slot whose prepared separation was
        /// dropped, with the slot index, the exact separation refusal, the
        /// renderer name, and — when the refusal names one — the offending
        /// material, plus the proven mapping's keys when provided, so a
        /// report distinguishes an admission gap from an identity mismatch.
        /// A <see cref="AlphaSeparationSlotRefusal.None"/> is not a
        /// refusal, so it throws.
        /// </summary>
        internal static void SlotSeparationRefusal(
            Renderer renderer,
            int slotIndex,
            AlphaSeparationSlotRefusal cause,
            string rendererName = null,
            Material offendingMaterial = null,
            System.Collections.Generic.IReadOnlyDictionary<Material, Material>
                provenMapping = null)
        {
            if (cause == AlphaSeparationSlotRefusal.None)
            {
                throw new InvalidOperationException(
                    "AlphaSeparationSlotRefusal.None is not a refusal.");
            }

            var names = new List<string>();
            if (provenMapping != null)
            {
                foreach (var key in provenMapping.Keys)
                {
                    names.Add(key != null ? key.name : "<null>");
                }
            }

            using (ErrorReport.WithContextObject(renderer))
            {
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    AmuseReportStrings.SlotSeparationKey(cause),
                    slotIndex,
                    cause.ToString(),
                    rendererName,
                    offendingMaterial,
                    names);
            }
        }

        /// <summary>
        /// One Information entry per texture whose capture refused, with
        /// the slot index, the texture property, and the sampled channel:
        /// the triangles that sample it have no proof and stay on the
        /// original material.
        /// </summary>
        internal static void TextureCaptureRefusal(
            Renderer renderer,
            int slotIndex,
            TextureCaptureRefusal refusal)
        {
            using (ErrorReport.WithContextObject(renderer))
            {
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    AmuseReportStrings.TextureCaptureKey(refusal.Reason),
                    slotIndex,
                    refusal.PropertyName,
                    refusal.Channel.ToString());
            }
        }
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

        /// <summary>
        /// One Information entry per host lifecycle refusal, with the
        /// avatar root as its clickable context. A
        /// <see cref="HostLifecycleRefusal.None"/> next to a denied
        /// mutation permission is an invariant break, not a refusal, so
        /// it throws.
        /// </summary>
        internal static void LifecycleRefusal(
            GameObject avatarRoot, HostLifecycleRefusal cause)
        {
            if (cause == HostLifecycleRefusal.None)
            {
                throw new InvalidOperationException(
                    "HostLifecycleRefusal.None is not a refusal.");
            }

            using (ErrorReport.WithContextObject(avatarRoot))
            {
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    AmuseReportStrings.HostKey(cause));
            }
        }

        internal static void AvatarRefusal(
            GameObject avatarRoot, AvatarAnimationRefusal cause)
        {
            if (cause == AvatarAnimationRefusal.None)
            {
                throw new InvalidOperationException(
                    "AvatarAnimationRefusal.None is not a refusal.");
            }

            using (ErrorReport.WithContextObject(avatarRoot))
            {
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    AmuseReportStrings.AvatarKey(cause));
            }
        }

        internal static void AvatarSummary(
            GameObject avatarRoot,
            int analyzedRenderers,
            int movedTriangles,
            int untouchedRenderers,
            AmuseBuildPath buildPath,
            bool alphaPolicyActive)
        {
            var summary = string.Format(
                AmuseReportStrings.Get(
                    "amuse.summary.Title:description"),
                analyzedRenderers,
                movedTriangles,
                untouchedRenderers);
            var policySentence = alphaPolicyActive
                ? " " + AmuseReportStrings.Get(
                    "amuse.summary.PolicyActive:description")
                : "";

            AmuseBuildStatusStore.Record(
                avatarRoot.GetInstanceID(),
                (buildPath == AmuseBuildPath.ApplyOnPlay
                    ? "Last play mode run: "
                    : "Last upload: ") + summary + policySentence);

            using (ErrorReport.WithContextObject(avatarRoot))
            {
                var reportArguments = new List<object>
                {
                    analyzedRenderers,
                    movedTriangles,
                    untouchedRenderers,
                };
                if (alphaPolicyActive)
                {
                    // The policy disclosure rides in the report arguments.
                    // The description format keeps its three slots, so the
                    // inert report composes exactly its previous text, and
                    // the status store line above is where the sentence
                    // surfaces.
                    reportArguments.Add(AmuseReportStrings.Get(
                        "amuse.summary.PolicyActive:description"));
                }

                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    "amuse.summary.Title",
                    reportArguments.ToArray());
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
