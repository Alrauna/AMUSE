using UnityEngine;
using nadena.dev.ndmf;

namespace Alrauna.Amuse.Runtime
{
    /// <summary>
    /// Opts an avatar into AMUSE build-time optimization. Presence on the
    /// avatar root turns the pipeline on; the Disable AMUSE toggle turns it
    /// back off without removing the component. Absence keeps the build
    /// untouched. NDMF removes IEditorOnly components from the uploaded
    /// avatar, so this component never ships.
    /// </summary>
    [AddComponentMenu("AMUSE/AMUSE Avatar Optimizer")]
    [DisallowMultipleComponent]
    [HelpURL("https://github.com/Alrauna/AMUSE")]
    public sealed class AmuseAvatarOptimizer : MonoBehaviour, INDMFEditorOnly
    {
        [SerializeField] private bool _amuseDisabled;

        /// <summary>
        /// True when the user disabled AMUSE from the inspector. The build
        /// treats a disabled component exactly like an absent one: nothing
        /// runs, nothing is reported.
        /// </summary>
        public bool AmuseDisabled => _amuseDisabled;
    }
}