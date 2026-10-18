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
    public sealed class AmuseAvatarOptimizer : MonoBehaviour,
        INDMFEditorOnly, ISerializationCallbackReceiver
    {
        [ToggleLeft]
        [SerializeField] private bool _amuseDisabled;

        /// <summary>
        /// The alpha separator switch. The alpha separator is the
        /// first AMUSE feature with its own switch, and later features
        /// add sibling switches beside it. The master "Disable AMUSE"
        /// toggle stays above them and turns off the whole component.
        /// Off here stops the alpha separator only: nothing is
        /// analyzed, moved, or reported for it.
        /// </summary>
        [ToggleLeft]
        [SerializeField]
        private bool _alphaSeparatorEnabled = true;

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
        /// shows it as "Minimum Opaque Coverage to Convert (Per Material)".
        /// </summary>
        [SerializeField]
        [Range(0, 100)]
        private int _minimumOpaqueCoveragePercent = 25;

        /// <summary>
        /// The largest unproven share, in percent, a material slot's
        /// submeshes can hold and still skip the split. A slot whose every
        /// submesh sits strictly below this value flattens onto the opaque
        /// material without proof, which can lose transparency as a visible
        /// artifact. The default of 5 matches the Normal preset. The inspector
        /// shows it as "Minimum Opaque Coverage to Preserve (Per Material)".
        /// </summary>
        [SerializeField]
        [Range(0, 100)]
        private int _minimumOpaqueCoverageToPreservePercent = 5;

        /// <summary>
        /// The smallest alpha percentage that counts as opaque evidence
        /// for materials without a shader cutoff. Alpha at or above this
        /// value is opaque evidence. Alpha between this value and full
        /// opacity becomes fully opaque after a move, so a value below
        /// 100 consents to that flattening. The default of 100 is inert:
        /// only exact full opacity is opaque evidence, as before. The
        /// inspector shows it as "Alpha Upper Clamp to Convert (Per Texture)".
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
        /// "Alpha Upper Clamp to Convert (Per Polygon)".
        /// </summary>
        [SerializeField]
        [Range(0, 100)]
        private int _polygonAlphaUpperClampPercent = 100;

        /// <summary>
        /// The smallest share of a polygon's consulted texels, in
        /// percent, that must stay non-stray before AMUSE ignores the
        /// strays. Lowering it lets AMUSE ignore denser strays. The
        /// default of 100 is inert: no share of strays is ever
        /// ignored. The inspector shows it as
        /// "Minimum Opaque Coverage to Convert (Per Polygon)".
        /// </summary>
        [SerializeField]
        [Range(0, 100)]
        private int _polygonMinimumOpaqueCoveragePercent = 100;

        /// <summary>
        /// True after the upgrade backfill copies the transparent values
        /// into the cutout fields. A legacy payload predates the cutout
        /// fields, so it deserializes with this marker false. The marker
        /// is also true after Reset establishes the factory defaults on
        /// a fresh component. The marker is serialized so the copy runs
        /// once. Without it, the marker would reset on every load and
        /// the copy would overwrite the stored cutout values each
        /// session.
        /// </summary>
        [SerializeField] private bool _cutoutSettingsBackfilled;

        /// <summary>
        /// The largest mip level the opacity proof consults for cutout
        /// sources. The value -1 means every level. The default is 3.
        /// The transparent setting defaults to 4. The inspector shows
        /// it as "Smallest Tested Mipmap". This setting governs cutout
        /// sources only.
        /// </summary>
        [SerializeField]
        private int _cutoutPreserveTransparencyMaxMipLevel = 3;

        /// <summary>
        /// The smallest texture size the opacity proof consults for
        /// cutout sources, in texels. The value -1 means every size. A
        /// texture already smaller than this size has no consulted
        /// levels and never converts. The default of 128 matches the
        /// transparent setting. The inspector shows it as "Smallest
        /// Tested Texture". This setting governs cutout sources only.
        /// </summary>
        [SerializeField]
        private int _cutoutPreserveTransparencyMinTextureSize = 128;

        /// <summary>
        /// The smallest share of proven-opaque triangles a cutout source
        /// needs before AMUSE converts it. The value 0 always converts
        /// when at least one triangle is proven opaque. The default of
        /// 25 matches the transparent setting. The inspector shows it
        /// as "Minimum Opaque Coverage to Convert (Per Material)". This
        /// setting governs cutout sources only.
        /// </summary>
        [SerializeField]
        [Range(0, 100)]
        private int _cutoutMinimumOpaqueCoveragePercent = 25;

        /// <summary>
        /// The largest unproven share, in percent, a cutout source can
        /// hold and still skip the conversion. The default of 5 matches
        /// the transparent setting. The inspector shows it as "Minimum
        /// Opaque Coverage to Preserve (Per Material)". This setting
        /// governs cutout sources only.
        /// </summary>
        [SerializeField]
        [Range(0, 100)]
        private int _cutoutMinimumOpaqueCoverageToPreservePercent = 5;

        [ToggleLeft]
        [SerializeField]
        private bool _allowDepthTestChange = true;

        /// <summary>
        /// True when the user accepts possible desktop anisotropy
        /// filtering differences, so the opacity proof may drop
        /// anisotropic footprints on the Windows target. The default of
        /// true matches the Normal preset. This field never changes the
        /// mobile field.
        /// </summary>
        [SerializeField]
        private bool _acceptDesktopAnisotropyDifferences = true;

        /// <summary>
        /// True when the user accepts possible mobile anisotropy
        /// filtering differences, so the proof may drop anisotropic
        /// footprints on the Android and iOS targets. The default of
        /// true matches the Normal preset. The existing mobile
        /// texture-capture and shader refusals keep their force. This
        /// field never changes the desktop field.
        /// </summary>
        [SerializeField]
        private bool _acceptMobileAnisotropyDifferences = true;

        [ToggleLeft]
        [SerializeField]
        private bool _ignoreOutOfRangeMaterialSlots = true;

        /// <summary>
        /// True when the user opened the advanced options. This is
        /// inspector-only state: the build never reads it. It is
        /// serialized so Reset and Undo treat it like every other
        /// control.
        /// </summary>
        [ToggleLeft]
        [SerializeField]
        private bool _advancedSettingsRevealed;

        /// <summary>
        /// Restores every serialized field to its declared default, the same
        /// contract Unity's context-menu Reset offers when no Reset is
        /// defined. It also sets the backfill marker, so the first
        /// serialization round trip of a fresh component does not copy the
        /// transparent values over the cutout defaults. Unity calls Reset
        /// only in the editor, when the component is first added or the user
        /// presses Reset.
        /// </summary>
        private void Reset()
        {
            _amuseDisabled = false;
            _alphaSeparatorEnabled = true;
            _preserveTransparencyMaxMipLevel = 4;
            _preserveTransparencyMinTextureSize = 128;
            _minimumOpaqueCoveragePercent = 25;
            _minimumOpaqueCoverageToPreservePercent = 5;
            _minimumOpaqueAlphaPercent = 100;
            _polygonAlphaUpperClampPercent = 100;
            _polygonMinimumOpaqueCoveragePercent = 100;
            _cutoutSettingsBackfilled = true;
            _cutoutPreserveTransparencyMaxMipLevel = 3;
            _cutoutPreserveTransparencyMinTextureSize = 128;
            _cutoutMinimumOpaqueCoveragePercent = 25;
            _cutoutMinimumOpaqueCoverageToPreservePercent = 5;
            _allowDepthTestChange = true;
            _acceptDesktopAnisotropyDifferences = true;
            _acceptMobileAnisotropyDifferences = true;
            _ignoreOutOfRangeMaterialSlots = true;
            _advancedSettingsRevealed = false;
        }

        /// <summary>
        /// True when the user disabled AMUSE from the inspector. The build
        /// treats a disabled component exactly like an absent one: nothing
        /// runs, nothing is reported.
        /// </summary>
        public bool AmuseDisabled => _amuseDisabled;

        /// <summary>
        /// True when the alpha separator runs for this component. The
        /// default of true keeps every avatar saved before this field
        /// behaving as before, because Unity fills a missing serialized
        /// field from the initializer.
        /// </summary>
        public bool AlphaSeparatorEnabled => _alphaSeparatorEnabled;

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
        /// The largest unproven triangle share, in percent, a slot's
        /// submeshes can hold and still flatten onto the opaque material.
        /// The value 0 never flattens.
        /// </summary>
        public int MinimumOpaqueCoverageToPreservePercent =>
            _minimumOpaqueCoverageToPreservePercent;

        /// <summary>
        /// The cutout mip cap as stored: -1 for every level, else the
        /// largest consulted level index. The build maps negative values
        /// to "no cap". It governs cutout sources only.
        /// </summary>
        public int CutoutPreserveTransparencyMaxMipLevel =>
            _cutoutPreserveTransparencyMaxMipLevel;

        /// <summary>
        /// The cutout minimum texture size as stored: -1 for every size,
        /// else the smallest consulted size in texels. It governs cutout
        /// sources only.
        /// </summary>
        public int CutoutPreserveTransparencyMinTextureSize =>
            _cutoutPreserveTransparencyMinTextureSize;

        /// <summary>
        /// The smallest proven-opaque triangle share, in percent, a
        /// cutout source needs before AMUSE converts it. It governs
        /// cutout sources only.
        /// </summary>
        public int CutoutMinimumOpaqueCoveragePercent =>
            _cutoutMinimumOpaqueCoveragePercent;

        /// <summary>
        /// The largest unproven triangle share, in percent, a cutout
        /// source can hold and still skip the conversion. It governs
        /// cutout sources only.
        /// </summary>
        public int CutoutMinimumOpaqueCoverageToPreservePercent =>
            _cutoutMinimumOpaqueCoverageToPreservePercent;

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
        /// When true (default), AMUSE ignores animation bindings that target
        /// material slot indices outside the mesh material count. When false,
        /// an out-of-range slot animation causes the renderer to refuse
        /// optimization.
        /// </summary>
        public bool IgnoreOutOfRangeMaterialSlots =>
            _ignoreOutOfRangeMaterialSlots;

        /// <summary>
        /// When true (default), AMUSE may move proven-opaque triangles of a
        /// material whose depth test is Less onto the canonical opaque
        /// material, whose depth test is LessEqual. The moved triangles then
        /// draw at exactly equal depth too. When false, such materials keep
        /// every triangle and refuse with the named depth-comparison
        /// refusal.
        /// </summary>
        public bool AllowDepthTestChange => _allowDepthTestChange;

        /// <summary>
        /// True when the user accepts possible desktop anisotropy
        /// filtering differences. The build reads this only through the
        /// per-build policy selection, never as a live global.
        /// </summary>
        public bool AcceptDesktopAnisotropyDifferences =>
            _acceptDesktopAnisotropyDifferences;

        /// <summary>
        /// True when the user accepts possible mobile anisotropy
        /// filtering differences. The build reads this only through the
        /// per-build policy selection, never as a live global.
        /// </summary>
        public bool AcceptMobileAnisotropyDifferences =>
            _acceptMobileAnisotropyDifferences;

        /// <summary>
        /// An upgraded component must keep proofing every source with the
        /// values its owner set. A legacy payload predates the cutout
        /// fields, so Unity fills them from the initializers on load.
        /// This one-time copy moves the transparent values into the
        /// cutout fields. The copy keeps the upgrade verdict-identical:
        /// the component proofs cutout sources with the same thresholds
        /// it used before the upgrade. Unity raises these callbacks
        /// around every serialize and deserialize. The serialized marker
        /// makes the copy run once. Without it, the marker would reset
        /// on every load and the copy would overwrite stored cutout
        /// values each session.
        /// </summary>
        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (_cutoutSettingsBackfilled)
            {
                return;
            }
            _cutoutSettingsBackfilled = true;
            _cutoutPreserveTransparencyMaxMipLevel =
                _preserveTransparencyMaxMipLevel;
            _cutoutPreserveTransparencyMinTextureSize =
                _preserveTransparencyMinTextureSize;
            _cutoutMinimumOpaqueCoveragePercent =
                _minimumOpaqueCoveragePercent;
            _cutoutMinimumOpaqueCoverageToPreservePercent =
                _minimumOpaqueCoverageToPreservePercent;
        }
    }
}