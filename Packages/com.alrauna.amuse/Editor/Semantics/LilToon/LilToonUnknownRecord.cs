using System.Collections.Generic;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    internal static class LilToonUnknownRecord
    {
        /// <summary>
        /// Records one lilToon diagnostic and answers the Unknown output of
        /// the requested kind. The Poiyomi frontend keeps its own analog,
        /// because it routes through the Poiyomi diagnostic vocabulary.
        /// </summary>
        internal static SemanticOutput<T> RecordUnknown<T>(
            List<LilToonSemanticDiagnostic> diagnostics,
            LilToonSemanticOutput output,
            LilToonSemanticDiagnosticCode code,
            string detail)
            where T : class
        {
            diagnostics.Add(new LilToonSemanticDiagnostic(output, code, detail));
            return SemanticOutput<T>.Unknown();
        }
    }
}
