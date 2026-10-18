namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// The lilToon property names that more than one module reads, each
    /// spelled once. The spellings are the attested lilToon 2.3.4 shader
    /// property table; a property that only one module reads keeps its
    /// local spelling. Renaming here retargets every reader, so a capture
    /// schema and a gate can never disagree about one property's name.
    /// </summary>
    internal static class LilToonProperties
    {
        internal const string Invisible = "_Invisible";
        internal const string UdimDiscardCompile = "_UDIMDiscardCompile";
        internal const string UdimDiscardMode = "_UDIMDiscardMode";
        internal const string ShiftBackfaceUV = "_ShiftBackfaceUV";
        internal const string UseParallax = "_UseParallax";
        internal const string UseMain2ndTex = "_UseMain2ndTex";
        internal const string UseMain3rdTex = "_UseMain3rdTex";
        internal const string AlphaMaskMode = "_AlphaMaskMode";
        internal const string AlphaMaskScale = "_AlphaMaskScale";
        internal const string AlphaMaskValue = "_AlphaMaskValue";
        internal const string UseDither = "_UseDither";
        internal const string IdMask1 = "_IDMask1";
        internal const string IdMask2 = "_IDMask2";
        internal const string IdMask3 = "_IDMask3";
        internal const string IdMask4 = "_IDMask4";
        internal const string IdMask5 = "_IDMask5";
        internal const string IdMask6 = "_IDMask6";
        internal const string IdMask7 = "_IDMask7";
        internal const string IdMask8 = "_IDMask8";
        internal const string IdMaskControlsDissolve =
            "_IDMaskControlsDissolve";
        internal const string Cutoff = "_Cutoff";
        internal const string Color = "_Color";
        internal const string MainTex = "_MainTex";
        internal const string DissolveParams = "_DissolveParams";
        internal const string MainTexScrollRotate = "_MainTex_ScrollRotate";
        internal const string AlphaBoostFA = "_AlphaBoostFA";
        internal const string SubpassCutoff = "_SubpassCutoff";
    }
}
