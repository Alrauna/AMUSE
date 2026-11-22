using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alrauna.Amuse.Editor.Build;

namespace Alrauna.Amuse.Editor.DryRun
{
    /// <summary>
    /// The result of one inspector atlas view computation. It holds a
    /// banner, an error flag, or the computed page sets and refusals.
    /// TRANSIENT HOST DATA: the page sets and the refusals keep live
    /// references, so the view can draw object fields.
    /// </summary>
    internal sealed class AtlasPreviewResult
    {
        private AtlasPreviewResult(
            string bannerSentence,
            bool errored,
            int slotCount,
            int atlasedSlotCount,
            IReadOnlyList<AtlasPageSet> pageSets,
            IReadOnlyList<AtlasUseRefusalRecord> refusals)
        {
            BannerSentence = bannerSentence;
            Errored = errored;
            SlotCount = slotCount;
            AtlasedSlotCount = atlasedSlotCount;
            PageSets = new ReadOnlyCollection<AtlasPageSet>(
                new List<AtlasPageSet>(pageSets));
            Refusals = new ReadOnlyCollection<AtlasUseRefusalRecord>(
                new List<AtlasUseRefusalRecord>(refusals));
        }

        /// <summary>Null when the analysis ran.</summary>
        internal string BannerSentence { get; }

        internal bool Errored { get; }

        internal int SlotCount { get; }

        internal int AtlasedSlotCount { get; }

        internal IReadOnlyList<AtlasPageSet> PageSets { get; }

        internal IReadOnlyList<AtlasUseRefusalRecord> Refusals { get; }

        internal static AtlasPreviewResult Banner(string sentence)
        {
            if (string.IsNullOrEmpty(sentence))
            {
                throw new ArgumentException(
                    "A banner needs its sentence.", nameof(sentence));
            }

            return new AtlasPreviewResult(
                sentence,
                false,
                0,
                0,
                Array.Empty<AtlasPageSet>(),
                Array.Empty<AtlasUseRefusalRecord>());
        }

        internal static AtlasPreviewResult Error()
        {
            return new AtlasPreviewResult(
                null,
                true,
                0,
                0,
                Array.Empty<AtlasPageSet>(),
                Array.Empty<AtlasUseRefusalRecord>());
        }

        internal static AtlasPreviewResult Computed(
            int slotCount,
            int atlasedSlotCount,
            IReadOnlyList<AtlasPageSet> pageSets,
            IReadOnlyList<AtlasUseRefusalRecord> refusals)
        {
            return new AtlasPreviewResult(
                null,
                false,
                slotCount,
                atlasedSlotCount,
                pageSets,
                refusals);
        }
    }
}
