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
        /// The largest mip level the opacity proof consults. The value -1
        /// means every level. Mip levels above the cap are policy-ignored:
        /// the user accepts that the GPU may sample them when the avatar
        /// renders very small. The default of 4 is a rule of thumb that
        /// matches normal viewing distances. The inspector shows it as
        /// "Preserve Transparency Maximum Mipmap".
        /// </summary>
        [SerializeField]
        private int _preserveTransparencyMaxMipLevel = 4;

        [SerializeField]
        private bool _ignoreOutOfRangeMaterialSlots;

        /// <summary>
        /// True when the user disabled AMUSE from the inspector. The build
        /// treats a disabled component exactly like an absent one: nothing
        /// runs, nothing is reported.
        /// </summary>
        public bool AmuseDisabled => _amuseDisabled;

        /// <summary>
        /// The opacity proof's mip cap as stored: -1 for every level, else
        /// the largest consulted level index. The build maps negative values
        /// to "no cap".
        /// </summary>
        public int PreserveTransparencyMaxMipLevel =>
            _preserveTransparencyMaxMipLevel;

        /// <summary>
        /// When true, AMUSE ignores animation bindings that target material
        /// slot indices outside the mesh material count. When false, an
        /// out-of-range slot animation causes the renderer to refuse
        /// optimization.
        /// </summary>
        public bool IgnoreOutOfRangeMaterialSlots =>
            _ignoreOutOfRangeMaterialSlots;
    }
}