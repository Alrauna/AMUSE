using UnityEngine;

namespace Alrauna.Amuse.Runtime
{
    /// <summary>
    /// Marks a bool field so the inspector draws the checkbox left of
    /// its label, in the style of the optimizers users already know.
    /// The drawer lives in the editor assembly, because the runtime
    /// assembly must stay free of UnityEditor types.
    /// </summary>
    internal sealed class ToggleLeftAttribute : PropertyAttribute
    {
    }
}
