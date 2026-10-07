using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
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
        /// The report names the offending material by its project path
        /// when the capture recorded one, and the exact shader fact that
        /// stopped the alpha proof when the frontend recorded one.
        /// One refused slot is one entry: the caller passes the slot's
        /// texture capture refusals, so the entry carries their facts
        /// instead of a standalone texture entry repeating them.
        /// A <see cref="RendererAnalysisRefusal.None"/> is not a refusal,
        /// so it throws.
        /// </summary>
        internal static void SlotAnalysisRefusal(
            Renderer renderer,
            int slotIndex,
            RendererAnalysisRefusal cause,
            string rendererName = null,
            CapturedAlphaMaterial offender = null,
            AlphaUnknownReason unknownReason = null,
            IReadOnlyList<TextureCaptureRefusal> captureRefusals = null,
            string closureWaySentence = null)
        {
            if (cause == RendererAnalysisRefusal.None)
            {
                throw new InvalidOperationException(
                    "RendererAnalysisRefusal.None is not a refusal.");
            }

            var evidenceSentence = FeatureSentence(unknownReason);
            var captureSentence = CaptureSentence(captureRefusals);
            if (evidenceSentence.Length == 0)
            {
                evidenceSentence = captureSentence;
            }
            else if (captureSentence.Length > 0)
            {
                evidenceSentence = evidenceSentence + " " + captureSentence;
            }

            // A closure-failure slot carries its own way sentence, so the
            // entry names the unassigned slot, the out-of-range binding, or
            // the non-material swap instead of the bare refusal name.
            if (!string.IsNullOrEmpty(closureWaySentence))
            {
                evidenceSentence = evidenceSentence.Length == 0
                    ? closureWaySentence
                    : evidenceSentence + " " + closureWaySentence;
            }

            using (ErrorReport.WithContextObject(renderer))
            {
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    AmuseReportStrings.SlotAnalysisKey(cause),
                    slotIndex,
                    cause.ToString(),
                    rendererName,
                    MaterialDescription(offender),
                    evidenceSentence);
            }
        }

        /// <summary>
        /// One sentence per texture capture refusal of this slot, so the
        /// slot entry names the property, the channel, and the reason the
        /// proof lost its texture data. Empty when the slot carries no
        /// capture refusal.
        /// </summary>
        private static string CaptureSentence(
            IReadOnlyList<TextureCaptureRefusal> captureRefusals)
        {
            if (captureRefusals == null || captureRefusals.Count == 0)
            {
                return "";
            }

            var sentences = new List<string>(captureRefusals.Count);
            foreach (var refusal in captureRefusals)
            {
                sentences.Add(string.Format(
                    AmuseReportStrings.Get(
                        "amuse.slotAnalysis.CaptureRefused"),
                    refusal.PropertyName,
                    refusal.Channel.ToString(),
                    refusal.Reason.ToString(),
                    refusal.HasSourceIdentity
                        ? "has a source identity"
                        : "has no source identity"));
            }

            return string.Join(" ", sentences);
        }

        /// <summary>
        /// The refused slot's material, named path-first: the project asset
        /// path when the capture recorded one, else the material name, else
        /// a plain word. A path names exactly one asset, so the author can
        /// find the material without searching by display name.
        /// </summary>
        private static string MaterialDescription(
            CapturedAlphaMaterial offender)
        {
            if (offender == null)
            {
                return "an unknown material";
            }

            if (!string.IsNullOrEmpty(offender.MaterialPath))
            {
                return "'" + offender.MaterialPath + "'";
            }

            if (!string.IsNullOrEmpty(offender.MaterialName))
            {
                return "'" + offender.MaterialName + "'";
            }

            return "an unnamed material";
        }

        /// <summary>
        /// One short sentence naming the exact shader fact that stopped the
        /// alpha proof, or an empty string when no reason was recorded. The
        /// slot-refusal report embeds it, so the reader sees the cause, not
        /// only the refused rule's name.
        /// </summary>
        internal static string FeatureSentence(AlphaUnknownReason reason)
        {
            if (reason == null)
            {
                return "";
            }

            switch (reason.Kind)
            {
                case AlphaUnknownKind.UnsupportedFeature:
                    var feature = reason.Feature != null
                        ? "the " + reason.Feature + " feature"
                        : "a shader feature";
                    var property = reason.Property != null
                        ? " (property " + reason.Property + ")"
                        : "";
                    return "AMUSE has no proven rule for " + feature +
                        property + ", so it cannot prove alpha.";
                case AlphaUnknownKind.FeatureRetention:
                    var retainedFeature = reason.Feature != null
                        ? "the " + reason.Feature + " feature"
                        : "a shader feature";
                    var retainedProperty = reason.Property != null
                        ? " (property " + reason.Property + ")"
                        : "";
                    return "AMUSE retains this material because " +
                        retainedFeature + " is active" + retainedProperty +
                        ". A moved triangle would lose its distance fade, " +
                        "so no triangle moves.";
                case AlphaUnknownKind.UnsupportedMultiState:
                    var multiState = reason.Feature != null
                        ? reason.Feature
                        : "an unnamed Multi state";
                    return string.Format(
                        AmuseReportStrings.Get(
                            "amuse.slotAnalysis.UnsupportedMultiState"),
                        multiState);
                case AlphaUnknownKind.UnsupportedShader:
                    return reason.ShaderName != null
                        ? "The shader '" + reason.ShaderName +
                          "' is not one AMUSE supports."
                        : "The shader is not one AMUSE supports.";
                case AlphaUnknownKind.UnattestedShader:
                    return reason.ShaderName != null
                        ? "The shader '" + reason.ShaderName +
                          "' names a supported family, but AMUSE cannot " +
                          "verify its source."
                        : "The material names a supported shader family, " +
                          "but AMUSE cannot verify its source.";
                default:
                    throw new InvalidOperationException(
                        "AlphaUnknownKind has an unhandled value. " +
                        "Every value needs its own report sentence.");
            }
        }

        /// <summary>
        /// One Information entry per slot whose prepared separation was
        /// dropped, with the slot index, the exact separation refusal, the
        /// renderer name, and — when the refusal names one — the offending
        /// material, plus the proven mapping's keys when provided, so a
        /// report distinguishes an admission gap from an identity mismatch.
        /// Optional detail replaces the cause text and names a copy by
        /// role only.
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
                provenMapping = null,
            string detail = null)
        {
            if (cause == AlphaSeparationSlotRefusal.None)
            {
                throw new InvalidOperationException(
                    "AlphaSeparationSlotRefusal.None is not a refusal.");
            }

            // The separation refusals must stay readable after play mode
            // ends: the build-copy renderer is destroyed then, the
            // console's object link is dead, and the report text is the
            // only place that still names the renderer. NDMF renders a
            // null substitution as the literal "<missing>", so an
            // omitted name falls back to the live renderer's own name.
            // An explicit name keeps winning, and a null renderer keeps
            // the honest placeholder.
            rendererName = rendererName
                ?? (renderer != null ? renderer.gameObject.name : null);

            // The offending material arrives as the live build copy; the
            // report names the authoring asset when a producer registered
            // the copy's replacement. One resolve here covers every apply
            // and preparation call site that passes a live material.
            offendingMaterial = (Material)(
                RegisteredSourceIdentity.Resolve(offendingMaterial)
                ?? offendingMaterial);

            var names = new List<string>();
            if (provenMapping != null)
            {
                foreach (var key in provenMapping.Keys)
                {
                    // The mapping's keys are live build copies; the
                    // diagnostic names the authoring asset when a producer
                    // registered the copy's replacement, matching the
                    // offender naming above.
                    var namedKey =
                        RegisteredSourceIdentity.Resolve(key) ?? key;
                    names.Add(namedKey != null ? namedKey.name : "<null>");
                }
            }

            using (ErrorReport.WithContextObject(renderer))
            {
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    AmuseReportStrings.SlotSeparationKey(cause),
                    slotIndex,
                    string.IsNullOrEmpty(detail)
                        ? cause.ToString()
                        : detail,
                    rendererName,
                    offendingMaterial,
                    names);
            }
        }

        /// <summary>
        /// One Information entry per prepared slot whose conversion
        /// admitted a depth-test divergence: the slot converted, and its
        /// moved triangles now draw under the normal depth rule. The
        /// report carries the fixed sentence that names the change. A
        /// slot without the divergence reports nothing here, exactly as
        /// before.
        /// </summary>
        internal static void SlotSeparationDivergence(
            Renderer renderer,
            int slotIndex,
            string rendererName = null)
        {
            using (ErrorReport.WithContextObject(renderer))
            {
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    AmuseReportStrings.SlotSeparationDivergenceKey,
                    slotIndex,
                    rendererName);
            }
        }

        /// <summary>
        /// One Information entry per prepared slot whose conversion used
        /// the revised premultiply premise: the source scaled its color
        /// by alpha, the proof moves only triangles whose alpha is
        /// exactly one, and the opaque material reproduces those colors
        /// exactly. A slot without a premultiply conversion reports
        /// nothing here, exactly as before.
        /// </summary>
        internal static void SlotSeparationPremultiplyNormalization(
            Renderer renderer,
            int slotIndex,
            string rendererName = null)
        {
            using (ErrorReport.WithContextObject(renderer))
            {
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    AmuseReportStrings
                        .SlotSeparationPremultiplyNormalizationKey,
                    slotIndex,
                    rendererName);
            }
        }
        /// <summary>
        /// One Information entry per texture whose capture refused, with
        /// the slot index, the texture property, the sampled channel, the
        /// storage format, and the texture name: the entry states the lost
        /// evidence and never claims the slot's outcome.
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
                    refusal.Channel.ToString(),
                    refusal.Reason.ToString(),
                    refusal.HasSourceIdentity
                        ? "has a source identity"
                        : "has no source identity",
                    refusal.FormatName,
                    refusal.TextureName);
            }
        }
        /// <summary>
        /// One Information entry per texture whose source has no alpha
        /// channel. The classification proved those triangles from the
        /// format theorem; this report only names the likely material
        /// misconfiguration, so the author can fix the shader type. It
        /// refuses nothing and changes no classification outcome.
        /// </summary>
        internal static void TextureAlphaMissing(
            Renderer renderer,
            int slotIndex,
            string propertyName)
        {
            using (ErrorReport.WithContextObject(renderer))
            {
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    AmuseReportStrings.TextureAlphaMissingKey,
                    slotIndex,
                    propertyName);
            }
        }

        /// <summary>
        /// One Information entry per refused renderer. The mapping refusal
        /// carries the two slot counts the refusal was computed from, so
        /// the reader can tell which side to change. The counts ride the
        /// refusal because the captured slot basis, not a live re-read,
        /// is the truth the decision used. Causes whose templates name no
        /// counts ignore the two arguments.
        /// </summary>
        internal static void RendererRefusal(
            Renderer renderer,
            RendererAnalysisRefusal cause,
            int meshSubMeshCount = -1,
            int materialSlotCount = -1,
            string detail = null)
        {
            using (ErrorReport.WithContextObject(renderer))
            {
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    AmuseReportStrings.RendererKey(cause),
                    SlotCountPhrase(meshSubMeshCount),
                    SlotCountPhrase(materialSlotCount),
                    renderer != null ? renderer.gameObject.name : null,
                    string.IsNullOrEmpty(detail) ? "" : detail);
            }
        }

        /// <summary>
        /// One plain sentence naming the way the material dependency
        /// closure failed. The renderer entry prints it so the reader
        /// can tell a read failure from an unassigned slot. The empty
        /// string keeps the entry readable when no way is known.
        /// </summary>
        internal static string ClosureFailureSentence(
            MaterialDependencyClosureFailure failure)
        {
            switch (failure)
            {
                case MaterialDependencyClosureFailure
                        .MissingCurrentMaterial:
                    return "The renderer has a material slot with no " +
                           "material assigned. ";
                case MaterialDependencyClosureFailure.SlotOutOfRange:
                    return "An animation binds a material slot the " +
                           "renderer does not have. ";
                case MaterialDependencyClosureFailure.InvalidSwapValue:
                    return "An animation swaps in something that is not " +
                           "a material. ";
                case MaterialDependencyClosureFailure.UnattestedMaterial:
                    return "A material in the animation could not be " +
                           "captured. ";
                default:
                    return "";
            }
        }

        /// <summary>
        /// A slot count renders as a noun phrase, so the sentence stays
        /// grammatical for zero, one, and many, and an unreadable count
        /// never prints as a number.
        /// </summary>
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
    /// Session-scoped last-build status for the component inspector.
    /// The slot is intentionally not keyed by avatar or build path:
    /// the processed build copy never survives to be inspected, so an
    /// object-identity key could never be read. The store describes
    /// the editor session's most recent AMUSE build, and the recorded
    /// text names the build path.
    /// </summary>
    internal static class AmuseBuildStatusStore
    {
        private static string Status;

        internal static void Record(string summary)
        {
            Status = summary;
        }

        internal static bool TryGet(out string summary)
        {
            summary = Status;
            return !string.IsNullOrEmpty(summary);
        }

        /// <summary>Clears the slot. Tests use this so one run cannot
        /// leak its status into the next.</summary>
        internal static void Forget()
        {
            Status = null;
        }
    }
}
