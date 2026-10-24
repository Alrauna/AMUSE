using System;
using System.Globalization;
using Alrauna.Amuse.Editor.Build;

namespace Alrauna.Amuse.Editor.DryRun
{
    /// <summary>
    /// One member per banner row the spec's Inspector section lists,
    /// in the order the dry run can emit them. A new banner row needs
    /// a spec sentence first, then a member here, so the display
    /// cannot drift from the spec text.
    /// </summary>
    internal enum BannerKind
    {
        AmuseOff,
        SeparatorOff,
        RootRequired,
        NonWindowsTarget,
        NoSamplingPolicy,
    }

    /// <summary>
    /// One member per conditional note row the spec's Inspector
    /// section lists. The dry run accumulates the note facts across
    /// renderer passes and emits each note at most once per avatar.
    /// The 2026-10-20 admission design adds ApprovedUnverifiedSource
    /// (spec B4).
    /// </summary>
    internal enum NoteKind
    {
        StreamingMipmaps,
        LockedMaterials,
        // The locked mirror note (A8.5): a locked material the build
        // refuses is not shown as deferred.
        LockedMaterialNotReproducible,
        ApprovedUnverifiedSource,
        EditorOnly,
        RefreshOnly,
    }

    /// <summary>
    /// One immutable display record. It is the cache's stored unit and
    /// the inspector's draw input, and it knows nothing about IMGUI,
    /// so tests pin every sentence without a window. Unstated fields
    /// hold null or -1, so a drawer cannot read a stale count by
    /// accident.
    /// </summary>
    internal sealed class MaterialTransformationRow
    {
        private MaterialTransformationRow(
            MaterialTransformationRowKind kind,
            string rendererPath,
            string rendererTypeName,
            int slotIndex,
            int movedTriangles,
            int provenTriangles,
            int totalTriangles,
            string sentence,
            string sourceMaterialPath = null)
        {
            // An empty sentence is a test failure, never a displayable
            // row. The guard turns a broken builder call into a throw
            // at row build time instead of a blank inspector line.
            if (string.IsNullOrEmpty(sentence))
            {
                throw new ArgumentException(
                    "A display row needs a sentence.", nameof(sentence));
            }

            Kind = kind;
            RendererPath = rendererPath;
            RendererTypeName = rendererTypeName;
            SlotIndex = slotIndex;
            MovedTriangles = movedTriangles;
            ProvenTriangles = provenTriangles;
            TotalTriangles = totalTriangles;
            Sentence = sentence;
            SourceMaterialPath = sourceMaterialPath;
        }

        internal MaterialTransformationRowKind Kind { get; }
        internal string RendererPath { get; }
        internal string RendererTypeName { get; }
        public string SourceMaterialPath { get; }
        internal int SlotIndex { get; }
        internal int MovedTriangles { get; }
        internal int ProvenTriangles { get; }
        internal int TotalTriangles { get; }
        internal string Sentence { get; }

        internal static MaterialTransformationRow Banner(string sentence)
        {
            return new MaterialTransformationRow(
                MaterialTransformationRowKind.Banner,
                null, null, -1, -1, -1, -1,
                sentence);
        }

        internal static MaterialTransformationRow Note(string sentence)
        {
            return new MaterialTransformationRow(
                MaterialTransformationRowKind.Note,
                null, null, -1, -1, -1, -1,
                sentence);
        }

        internal static MaterialTransformationRow Errored(
            string rendererPath, string rendererTypeName, string sentence)
        {
            return new MaterialTransformationRow(
                MaterialTransformationRowKind.Errored,
                rendererPath, rendererTypeName, -1, -1, -1, -1,
                sentence);
        }

        internal static MaterialTransformationRow Renderer(
            string path, string typeName, int moved, int proven, int total,
            string sentence)
        {
            return new MaterialTransformationRow(
                MaterialTransformationRowKind.Renderer,
                path, typeName, -1,
                moved, proven, total,
                sentence);
        }

        internal static MaterialTransformationRow Slot(
            string path, string typeName, int slotIndex,
            int proven, int total, string sentence,
            string sourceMaterialPath = null)
        {
            return new MaterialTransformationRow(
                MaterialTransformationRowKind.Slot,
                path, typeName, slotIndex,
                -1, proven, total,
                sentence,
                sourceMaterialPath);
        }

        internal static MaterialTransformationRow FromSerialized(
            SerializedTransformationRow row)
        {
            return new MaterialTransformationRow(
                row.kind,
                row.rendererPath,
                row.rendererTypeName,
                row.slotIndex,
                row.movedTriangles,
                row.provenTriangles,
                row.totalTriangles,
                row.sentence,
                row.sourceMaterialPath);
        }
    }

    /// <summary>
    /// The spec's Inspector sentences, verbatim, with counts
    /// interpolated. Every builder-local sentence lives here, so the
    /// wording has one home. Refusal sentences come from the shared
    /// report string table through the key builders, because the dry
    /// run must never add table keys.
    /// </summary>
    internal static class RowText
    {
        internal static string RefusedSlotSentence()
        {
            return "Refused: AMUSE currently does not support this material";
        }

        internal static string DeferredSlotSentence(int total)
        {
            return "Deferred: 0 out of " +
                total.ToString(CultureInfo.InvariantCulture) +
                " polygons may be moved (evaluates at build time)";
        }

        internal static string UntouchableSlotSentence(int total)
        {
            return "Untouchable: 0 out of " + total + " polygons may be moved";
        }

        internal static string SplittableSlotSentence(int proven, int total)
        {
            return "Splittable: " + proven + " out of " + total + " polygons may be moved";
        }

        internal static string ConvertableSlotSentence(int total)
        {
            return "Convertable: " + total + " out of " + total + " polygons may be moved";
        }

        internal static string SeparatorOffSlotSentence()
        {
            return "Skipped: the matching separator switch is off.";
        }

        internal static string RendererRefusedSentence(
            string path, string typeName, string refusalKey, params string[] args)
        {
            return RendererPrefix(path, typeName) + "refused. "
                + TableSentence(refusalKey, args);
        }

        internal static string StructuralRowSentence(string prefix, string sentence)
        {
            if (string.IsNullOrEmpty(sentence))
            {
                throw new ArgumentException(
                    "A structural row needs the refusal sentence.",
                    nameof(sentence));
            }

            // The prefix carries its own separator when one renderer
            // caused the refusal. An avatar wide refusal renders the
            // bare sentence.
            return string.IsNullOrEmpty(prefix) ? sentence : prefix + sentence;
        }

        internal static string BannerSentence(BannerKind kind)
        {
            switch (kind)
            {
                case BannerKind.AmuseOff:
                    return "AMUSE is off. Turn it on to see results.";
                case BannerKind.SeparatorOff:
                    return "The Alpha Separator is off. Nothing will move. "
                        + "Turn it on to see results.";
                case BannerKind.RootRequired:
                    return "This component must sit on the avatar root object.";
                case BannerKind.NonWindowsTarget:
                    return "The active build target is not Windows. "
                        + "Texture proof needs the Windows import. This view "
                        + "can prove fewer triangles than a Windows build.";
                case BannerKind.NoSamplingPolicy:
                    return "The active build target supports no sampling "
                        + "policy. A build on this target applies nothing. "
                        + "This view shows no results for it.";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(kind), kind, "Unknown banner kind.");
            }
        }

        internal static string NoteSentence(NoteKind kind)
        {
            switch (kind)
            {
                case NoteKind.StreamingMipmaps:
                    return "Some textures use streaming mipmaps. This view "
                        + "does not read them, because reading would change "
                        + "project files. A build can prove more for those "
                        + "textures.";
                case NoteKind.LockedMaterials:
                    return "This avatar holds locked or transfer protected "
                        + "materials. A build with granted consent may "
                        + "convert further than this view shows.";
                case NoteKind.ApprovedUnverifiedSource:
                    return "A shader version inside the supported range "
                        + "did not verify against its measured source. Its "
                        + "triangles stay unproven.";
                case NoteKind.EditorOnly:
                    return "Some objects are marked EditorOnly. The build "
                        + "removes them before AMUSE runs. This view shows "
                        + "what those renderers would get if they stayed.";
                case NoteKind.RefreshOnly:
                    return "Some textures change every frame. This view "
                        + "computes them on Refresh only.";
                case NoteKind.LockedMaterialNotReproducible:
                    return "Some locked materials cannot be rebuilt from "
                        + "their recorded source. A build will not convert "
                        + "those materials. This view does not promise a "
                        + "conversion for them.";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(kind), kind, "Unknown note kind.");
            }
        }

        internal static string DisclosureSentence(int index)
        {
            // The spec lists exactly two disclosure rows under the
            // nested foldout title. An index outside them is a
            // programming defect, so the builder throws instead of
            // drawing a blank row.
            switch (index)
            {
                case 0:
                    return "This view runs the same proof as a build on the avatar in "
                        + "the scene. Other tools can change the avatar during build "
                        + "time. Run a Play mode build and exit to see accurate "
                        + "information.";
                case 1:
                    return "The first open measures textures on the GPU. "
                        + "It can take a moment.";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(index), index, "Unknown disclosure row.");
            }
        }

        internal static string ErroredSentence(string path, string typeName)
        {
            return RendererPrefix(path, typeName)
                + "could not be computed. Press Refresh to try again.";
        }

        internal static string NoRenderersSentence()
        {
            return "No renderer on this avatar.";
        }

        /// <summary>
        /// The renderer name shape every renderer-level row shares, so
        /// the path and type render the same way across summaries,
        /// refusals, and errors.
        /// </summary>
        private static string RendererPrefix(string path, string typeName)
        {
            return path + " (" + typeName + "): ";
        }

        /// <summary>
        /// The one route a table-sourced sentence may take. The builder
        /// guards with Has first, because an unknown key is a
        /// programming defect and must throw, and a table sentence that
        /// formats to nothing would draw a blank row.
        /// </summary>
        private static string TableSentence(string key, string[] args)
        {
            if (!AmuseReportStrings.Has(key))
            {
                throw new InvalidOperationException(
                    "The report string table has no sentence for key '"
                    + key + "'.");
            }

            var sentence = AmuseReportStrings.Format(key, args);
            if (string.IsNullOrEmpty(sentence))
            {
                throw new InvalidOperationException(
                    "The report string table holds an empty sentence for key '"
                    + key + "'.");
            }

            return sentence;
        }
    }
}
