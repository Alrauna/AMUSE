using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// One closed batch capture's per-material outcome. Captured carries
    /// every surviving member's result in batch order. UnattestedOrdinals
    /// names the batch positions whose member failed source attestation and
    /// was not admitted. Survivor ordinal k addresses the k-th batch
    /// position outside UnattestedOrdinals; the two lists partition the
    /// batch. The capturer's bool stays false only for a failure that
    /// names no material, which no production capturer produces.
    /// </summary>
    internal sealed class ClosedAlphaCaptureOutcome
    {
        internal ClosedAlphaCaptureOutcome(
            IReadOnlyList<CapturedAlphaMaterial> captured,
            IReadOnlyList<int> unattestedOrdinals)
        {
            Captured = new ReadOnlyCollection<CapturedAlphaMaterial>(
                new List<CapturedAlphaMaterial>(captured));
            UnattestedOrdinals =
                new ReadOnlyCollection<int>(
                    new List<int>(unattestedOrdinals));
        }

        internal IReadOnlyList<CapturedAlphaMaterial> Captured { get; }
        internal IReadOnlyList<int> UnattestedOrdinals { get; }
    }
}
