using System;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// What a resolver answers for one material: the usual
    /// <see cref="MaterialSemantics"/> plus, when its alpha is Unknown, the
    /// frontend's reason. The wrapper exists so the reason can travel with
    /// the semantics without putting a reporting concern on the shared
    /// algebra record. A null <see cref="AlphaUnknownReason"/> with an
    /// unknown alpha means the resolver was a seam substitute that records
    /// no reason; the report then falls back to naming the cause alone.
    /// </summary>
    internal sealed record CapturedAlphaSemantics
    {
        internal MaterialSemantics Semantics { get; }
        internal AlphaUnknownReason AlphaUnknownReason { get; }

        internal CapturedAlphaSemantics(
            MaterialSemantics semantics,
            AlphaUnknownReason alphaUnknownReason)
        {
            Semantics = semantics
                ?? throw new ArgumentNullException(nameof(semantics));
            AlphaUnknownReason = alphaUnknownReason;
        }

        internal static CapturedAlphaSemantics AllUnknown()
        {
            return new CapturedAlphaSemantics(
                EvidenceGates.AllUnknown(), null);
        }
    }
}
