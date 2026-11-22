using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The atlas analysis of one avatar: the capture outcomes, the
    /// admitted uses, the group plans, the budget verdicts, and every
    /// refusal in record order. The build pass and the inspector view
    /// read the same result, so the view cannot drift from the build.
    /// </summary>
    internal sealed class AtlasAnalysisResult
    {
        internal AtlasAnalysisResult(
            IReadOnlyList<AtlasUseOutcome> outcomes,
            IReadOnlyList<AtlasUseOutcome> admittedOutcomes,
            IReadOnlyList<CapturedAtlasUse> uses,
            IReadOnlyList<AtlasGroupPlan> plans,
            IReadOnlyList<AtlasBudgetVerdict> verdicts,
            IReadOnlyList<AtlasUseRefusalRecord> refusals)
        {
            Outcomes = ReadOnly(outcomes);
            AdmittedOutcomes = ReadOnly(admittedOutcomes);
            Uses = ReadOnly(uses);
            Plans = ReadOnly(plans);
            Verdicts = ReadOnly(verdicts);
            Refusals = ReadOnly(refusals);
        }

        /// <summary>One outcome per renderer slot, in capture order.</summary>
        internal IReadOnlyList<AtlasUseOutcome> Outcomes { get; }

        /// <summary>
        /// The admitted outcomes. Index i matches <see cref="Uses"/>[i]
        /// and the use indices of every plan.
        /// </summary>
        internal IReadOnlyList<AtlasUseOutcome> AdmittedOutcomes { get; }

        internal IReadOnlyList<CapturedAtlasUse> Uses { get; }

        /// <summary>Empty when no use was admitted.</summary>
        internal IReadOnlyList<AtlasGroupPlan> Plans { get; }

        /// <summary>One verdict per plan, in plan order.</summary>
        internal IReadOnlyList<AtlasBudgetVerdict> Verdicts { get; }

        /// <summary>
        /// Capture refusals in outcome order, then placement refusals
        /// in plan order, then budget refusals in plan order.
        /// </summary>
        internal IReadOnlyList<AtlasUseRefusalRecord> Refusals { get; }

        private static IReadOnlyList<T> ReadOnly<T>(IReadOnlyList<T> items)
        {
            return new ReadOnlyCollection<T>(new List<T>(items));
        }
    }
}
