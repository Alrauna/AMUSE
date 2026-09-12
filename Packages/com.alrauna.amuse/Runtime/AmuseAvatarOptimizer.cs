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
        /// "Smallest Tested Mipmap".
        /// </summary>
        [SerializeField]
        private int _preserveTransparencyMaxMipLevel = 4;

        /// <summary>
        /// The smallest texture size the opacity proof consults, in
        /// texels. The value -1 means every size. The proof stops
        /// consulting a texture's mip chain at the level whose width or
        /// height falls below this size. A texture already smaller than
        /// this size has no consulted levels and never converts. The
        /// default of 128 is a rule of thumb that matches normal
        /// viewing distances. The inspector shows it as "Smallest
        /// Tested Texture".
        /// </summary>
        [SerializeField]
        private int _preserveTransparencyMinTextureSize = 128;

        /// <summary>
        /// The smallest share of proven-opaque triangles a mixed
        /// submesh needs before AMUSE splits it onto a separate opaque
        /// material. Each split adds one runtime draw call, so a small
        /// share can cost more CPU than it saves GPU work. The value 0
        /// always splits when at least one triangle is proven opaque.
        /// The default of 25 percent is a rule of thumb. The inspector
        /// shows it as "Minimum Opaque Coverage (Per Material)".
        /// </summary>
        [SerializeField]
        [Range(0, 100)]
        private int _minimumOpaqueCoveragePercent = 25;

        /// <summary>
        /// The smallest alpha percentage that counts as opaque evidence
        /// for materials without a shader cutoff. Alpha at or above this
        /// value is opaque evidence. Alpha between this value and full
        /// opacity becomes fully opaque after a move, so a value below
        /// 100 consents to that flattening. The default of 100 is inert:
        /// only exact full opacity is opaque evidence, as before. The
        /// inspector shows it as "Alpha Upper Clamp (Per Texture)".
        /// </summary>
        [SerializeField]
        [Range(0, 100)]
        private int _minimumOpaqueAlphaPercent = 100;

        /// <summary>
        /// The alpha percentage that bounds the per-polygon stray band.
        /// Texels below this value are strays for materials without a
        /// shader cutoff. AMUSE ignores strays only where they are
        /// sparse, and the coverage slider sets how sparse. Ignored
        /// strays become fully opaque after a move, so a value below
        /// 100 consents to that flattening. The default of 100 is
        /// inert: nothing is a stray. The inspector shows it as
        /// "Alpha Upper Clamp (Per Polygon)".
        /// </summary>
        [SerializeField]
        [Range(0, 100)]
        private int _polygonAlphaUpperClampPercent = 100;

        /// <summary>
        /// The smallest share of a polygon's consulted texels, in
        /// percent, that must stay non-stray before AMUSE ignores the
        /// strays. Lowering it lets AMUSE ignore denser strays. The
        /// default of 100 is inert: no share of strays is ever
        /// ignored. The inspector shows it as "Minimum Opaque Coverage
        /// (Per Polygon)".
        /// </summary>
        [SerializeField]
        [Range(0, 100)]
        private int _polygonMinimumOpaqueCoveragePercent = 100;

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
        /// The minimum texture size as stored: -1 for every size, else
        /// the smallest consulted size in texels.
        /// </summary>
        public int PreserveTransparencyMinTextureSize =>
            _preserveTransparencyMinTextureSize;

        /// <summary>
        /// The smallest proven-opaque triangle share, in percent, a
        /// mixed submesh needs before AMUSE splits it. The value 0
        /// splits whenever at least one triangle is proven opaque.
        /// </summary>
        public int MinimumOpaqueCoveragePercent =>
            _minimumOpaqueCoveragePercent;

        /// <summary>
        /// The smallest alpha percentage that counts as opaque evidence
        /// for materials without a shader cutoff.
        /// </summary>
        public int MinimumOpaqueAlphaPercent =>
            _minimumOpaqueAlphaPercent;

        /// <summary>
        /// The alpha percentage that bounds the per-polygon stray band
        /// for materials without a shader cutoff.
        /// </summary>
        public int PolygonAlphaUpperClampPercent =>
            _polygonAlphaUpperClampPercent;

        /// <summary>
        /// The smallest non-stray share, in percent of a polygon's
        /// consulted texels, before AMUSE ignores the strays.
        /// </summary>
        public int PolygonMinimumOpaqueCoveragePercent =>
            _polygonMinimumOpaqueCoveragePercent;

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