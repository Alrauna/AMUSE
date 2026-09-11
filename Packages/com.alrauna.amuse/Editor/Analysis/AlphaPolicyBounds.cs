namespace Alrauna.Amuse.Editor.Analysis
{
    /// <summary>
    /// The user's alpha policy as exact byte bounds. The opaque bound is
    /// the smallest byte that counts as opaque evidence. The noise bound
    /// is the smallest byte at or which a texel stops being noise, so a
    /// texel is noise exactly when its byte is below the noise bound.
    /// Both bounds map by ceiling so a percent never claims a byte its
    /// value cannot support, and the inert bounds reproduce the base
    /// exact-255 contract.
    /// </summary>
    internal readonly struct AlphaPolicyBounds
    {
        internal byte OpaqueBound { get; }
        internal byte NoiseBound { get; }

        internal static AlphaPolicyBounds Inert => new(byte.MaxValue, 0);

        private AlphaPolicyBounds(byte opaqueBound, byte noiseBound)
        {
            OpaqueBound = opaqueBound;
            NoiseBound = noiseBound;
        }

        internal static AlphaPolicyBounds From(
            int opaquePercent,
            int noisePercent)
        {
            return new(OpaqueCeilingByte(opaquePercent), OpaqueCeilingByte(noisePercent));
        }

        /// <summary>
        /// The smallest byte b with b * 100 at or above percent * 255,
        /// in exact integer arithmetic. Percent 100 maps to 255 and
        /// percent 0 maps to 0.
        /// </summary>
        internal static byte OpaqueCeilingByte(int percent)
        {
            if (percent <= 0)
            {
                return 0;
            }
            if (percent >= 100)
            {
                return byte.MaxValue;
            }
            return (byte)((percent * 255 + 99) / 100);
        }

        /// <summary>
        /// The inspector clamp: the noise percent stays strictly below
        /// the opaque percent, and an opaque percent of 0 forces the
        /// noise percent to 0. This keeps the erased, witness, and
        /// opaque bands all expressible.
        /// </summary>
        internal static int ClampNoise(int opaquePercent, int noisePercent)
        {
            if (opaquePercent <= 0)
            {
                return 0;
            }
            return noisePercent < opaquePercent ? noisePercent : opaquePercent - 1;
        }
    }
}
