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
    /// UnattestedApprovedFlags mirrors UnattestedOrdinals one to one when
    /// non-empty. It names which failed members carried an approved package
    /// version with an identity failure. Empty means none. It is a mirror,
    /// not a partition member.
    /// </summary>
    internal sealed class ClosedAlphaCaptureOutcome
    {
        internal ClosedAlphaCaptureOutcome(
            IReadOnlyList<CapturedAlphaMaterial> captured,
            IReadOnlyList<int> unattestedOrdinals,
            IReadOnlyList<bool> unattestedApprovedFlags = null)
        {
            Captured = new ReadOnlyCollection<CapturedAlphaMaterial>(
                new List<CapturedAlphaMaterial>(captured));
            UnattestedOrdinals =
                new ReadOnlyCollection<int>(
                    new List<int>(unattestedOrdinals));
            UnattestedApprovedFlags =
                new ReadOnlyCollection<bool>(
                    unattestedApprovedFlags == null
                        ? new List<bool>()
                        : new List<bool>(unattestedApprovedFlags));
        }

        internal IReadOnlyList<CapturedAlphaMaterial> Captured { get; }
        internal IReadOnlyList<int> UnattestedOrdinals { get; }
        internal IReadOnlyList<bool> UnattestedApprovedFlags { get; }
    }
}
