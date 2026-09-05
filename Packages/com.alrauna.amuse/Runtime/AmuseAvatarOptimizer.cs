using UnityEngine;
using nadena.dev.ndmf;

namespace Alrauna.Amuse.Runtime
{
    /// <summary>
    /// Opts an avatar into AMUSE build-time optimization. Presence on the
    /// avatar root turns the pipeline on. Absence keeps the build untouched.
    /// NDMF removes IEditorOnly components from the uploaded avatar, so this
    /// component never ships.
    /// </summary>
    [AddComponentMenu("AMUSE/Avatar Optimizer")]
    [DisallowMultipleComponent]
    [HelpURL("https://github.com/Alrauna/AMUSE")]
    public sealed class AmuseAvatarOptimizer : MonoBehaviour, INDMFEditorOnly
    {
    }
}