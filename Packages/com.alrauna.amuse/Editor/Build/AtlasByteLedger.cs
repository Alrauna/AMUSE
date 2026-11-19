using System;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The running byte counter of one page build. The pass adds each
    /// allocation it makes and releases each release. A value above
    /// the prediction is an invariant failure and throws, because the
    /// prediction is a pure function of the plan. Allocations inside
    /// Unity, such as importer and encoder internals, stay outside
    /// the ledger.
    /// </summary>
    internal sealed class AtlasByteLedger
    {
        private readonly long prediction;

        internal long Current { get; private set; }

        internal AtlasByteLedger(long prediction)
        {
            this.prediction = prediction;
        }

        internal void Add(long bytes)
        {
            if (bytes < 0)
            {
                throw new InvalidOperationException(
                    "A ledger allocation must be zero or positive.");
            }

            checked
            {
                long next = Current + bytes;
                if (next > prediction)
                {
                    throw new InvalidOperationException(
                        "The atlas pass holds "
                        + next
                        + " bytes, above its prediction of "
                        + prediction
                        + " bytes. The allocation code and the"
                        + " prediction disagree.");
                }

                Current = next;
            }
        }

        internal void Release(long bytes)
        {
            if (bytes < 0)
            {
                throw new InvalidOperationException(
                    "A ledger release must be zero or positive.");
            }

            checked
            {
                if (Current - bytes < 0)
                {
                    throw new InvalidOperationException(
                        "A ledger release would take the held bytes"
                        + " below zero. A release without its"
                        + " allocation is a leak in the pass.");
                }

                Current -= bytes;
            }
        }
    }
}
