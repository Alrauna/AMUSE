// The AMUSE red-channel evidence predicate. Editor-only: it lives under
// Editor/ so it is excluded from player builds and never reaches a built
// avatar.
//
// It loads ONE explicit mip level by integer texel index and emits a
// three-state verdict in RED over the sampled red value, under the same
// bounds contract as the alpha predicate: byte 255 at or above the opaque
// bound, AlphaTextureData.ErasedFlag (byte 1) strictly below the noise
// bound, byte 0 otherwise. The bounds arrive as normalized floats, a bound
// byte divided by 255, with the inert defaults 1.0 and 0.0. Load is a texel
// fetch: no filtering, no mip selection, no wrap.
//
// Whether the fetch applies the sRGB transfer is decided by the texture's
// import, not by this shader. The transfer is strictly monotone and fixes
// exactly 0.0 and 1.0, and that is what makes the comparisons sound:
//
// - Inert bounds reduce the verdict to the former binary test, for every
//   input. The opaque arm also requires the sample to be at most 1.0, so
//   raw >= 1.0 and raw <= 1.0 hold together exactly when raw == 1.0,
//   which under a strictly monotone transfer that fixes 1.0 is exactly
//   the stored texel whose byte is 255. The erased arm requires an
//   active noise bound above 0, so nothing erases. The output is byte
//   for byte the former output, including for the out-of-range float
//   sources the research characterization feeds this shader.
// - Under any monotone fetch the three verdict bands stay ordered in
//   stored-byte order: a lower byte can never outrank a higher one.
// - When the fetch applies no transfer, the decode is UNorm b/255 and the
//   normalized bounds compare exactly as the stored bytes compare, which is
//   the alpha channel's case at every texel.
// - When the transfer applies, the bounds compare in the transfer's value
//   space: a bound byte B met by raw means the stored byte is the one the
//   transfer maps across B/255. The inert contract above is what production
//   captures are pinned on, and it is exact.
//
// The constants 1.0 and 0.0 store exactly in the R8_UNorm target. The
// erased constant 1.0/255.0 stores 1 on a write that rounds to nearest,
// and the inert bounds never reach it: the capability gate re-measures
// the erased encoding once per AppDomain under active bounds. The claim
// is narrow: the output validator refuses every byte outside the three
// states, so a deviation that leaves the flag grid fails the capture
// instead of corrupting evidence.
//
// GREEN carries the raw red and is a RESEARCH DIAGNOSTIC ONLY, mirroring the
// alpha predicate. Production renders this shader into a
// GraphicsFormat.R8_UNorm target, which stores only the red component, so
// green is discarded before any production code sees a result.
Shader "Hidden/Alrauna/Amuse/RedExactOne"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OpaqueBound ("Opaque bound", Float) = 1.0
        _NoiseBound ("Noise bound", Float) = 0.0
    }
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            Texture2D<float4> _MainTex;
            int _Mip;
            float _OpaqueBound;
            float _NoiseBound;

            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                int3 coordinate = int3((int)i.pos.x, (int)i.pos.y, _Mip);
                float mask = _MainTex.Load(coordinate).r;

                // The erased test reads the red fetch, the same value the
                // opaque test reads. The opaque test reads first, as
                // AlphaPolicyBounds' published verdict does; the bands
                // cannot overlap under the inspector clamp, so the order
                // only pins the behavior of bounds no production caller
                // supplies. The 1.0 ceiling keeps a value outside [0, 1]
                // out of the opaque band, and the active-gate check keeps
                // the inert noise bound 0 from erasing a negative float;
                // the research characterization renders float sources
                // through this shader too.
                float verdict = mask >= _OpaqueBound && mask <= 1.0
                    ? 1.0
                    : (mask < _NoiseBound && _NoiseBound > 0.0
                        ? 1.0 / 255.0
                        : 0.0);
                return float4(verdict, mask, 0.0, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
