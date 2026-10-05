namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Exact render-state predicates and the Unity enum constants they read.
    /// This is a constants-and-predicates file and must stay one: a mode
    /// parameter, a gate table, a dispatch, or a shared Evaluate* body here is
    /// out of scope and returns to the controller (design §11). Four
    /// consumers read this union: the lilToon cutout and transparent
    /// source-eligibility modules, the Poiyomi opaque-conversion module, and
    /// the audited-file pin in AlphaSeparationPersistenceTests.
    /// </summary>
    internal static class OpaqueConversionFactors
    {
        internal const float BlendOpAdd =
            (float)UnityEngine.Rendering.BlendOp.Add;
        internal const float BlendOpMax =
            (float)UnityEngine.Rendering.BlendOp.Max;
        internal const float BlendFactorZero =
            (float)UnityEngine.Rendering.BlendMode.Zero;
        internal const float BlendFactorOne =
            (float)UnityEngine.Rendering.BlendMode.One;
        internal const float BlendFactorSrcAlpha =
            (float)UnityEngine.Rendering.BlendMode.SrcAlpha;
        internal const float BlendFactorOneMinusSrcAlpha =
            (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;

        internal const float LEqualDepthComparison =
            (float)UnityEngine.Rendering.CompareFunction.LessEqual;

        internal const float LessDepthComparison =
            (float)UnityEngine.Rendering.CompareFunction.Less;

        internal const float ColorMaskAll =
            (float)UnityEngine.Rendering.ColorWriteMask.All;

        // The Unity 2022.3 profile lacks UnityEngine.Rendering.DepthWrite (checked 2026-09-29). Revisit this literal on a Unity upgrade.
        internal const float DepthWriteOn = 1f;

        /// <summary>One and SrcAlpha both evaluate to 1 at alpha 1.</summary>
        internal static bool IsUnitSourceFactorAtAlphaOne(float factor)
        {
            return factor == BlendFactorOne || factor == BlendFactorSrcAlpha;
        }

        /// <summary>Zero and OneMinusSrcAlpha both evaluate to 0 at alpha 1.</summary>
        internal static bool IsZeroDestinationFactorAtAlphaOne(float factor)
        {
            return factor == BlendFactorZero ||
                   factor == BlendFactorOneMinusSrcAlpha;
        }
    }
}
