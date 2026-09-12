// The AMUSE alpha evidence predicate. Editor-only: it lives under Editor/ so it
// is excluded from player builds and never reaches a built avatar.
//
// It loads ONE explicit mip level by integer texel index and emits a three-state
// alpha verdict in RED: byte 255 when the sampled alpha is at or above the
// policy's opaque bound, AlphaTextureData.ErasedFlag (byte 1) when it is
// strictly below the noise bound, and byte 0 otherwise. The bounds arrive as
// normalized floats, a bound byte divided by 255, with the inert defaults 1.0
// and 0.0.
//
// The comparisons are exact in stored-byte order for every admitted format. An
// alpha channel never crosses the sRGB transfer, and every admitted alpha
// decode is UNorm, so a stored byte b samples exactly b/255: the float
// comparisons then order texels exactly as their stored bytes order, and a
// bound of B/255 is met exactly when the stored byte is at or above B. The
// opaque arm also requires the sample to be at most 1.0, so a value
// outside [0, 1] can never read opaque under any bounds, and erasure
// requires an active noise bound above 0. With the inert bounds the
// verdict is therefore the former binary test for every input, including
// the out-of-range float values the research characterization feeds it:
// alpha >= 1.0 and alpha <= 1.0 hold together exactly when alpha == 1.0,
// and nothing erases. The output is byte for byte the former output for
// every input.
//
// The constants 1.0 and 0.0 store exactly in the R8_UNorm target. The
// erased constant 1.0/255.0 stores 1 on a write that rounds to nearest,
// and the inert bounds never reach it: the capability gate re-measures
// the erased encoding once per AppDomain under active bounds. The claim
// is narrow: the output validator refuses every byte outside the three
// states, so a deviation that leaves the flag grid fails the capture
// instead of corrupting evidence.
//
// GREEN carries the raw alpha and is a RESEARCH DIAGNOSTIC ONLY. Production
// renders this shader into a GraphicsFormat.R8_UNorm target, which stores only
// the red component, so green is discarded before any production code sees a
// result: it has no production evidence meaning, no production reader, and no
// production code path. The research characterization renders the same shader
// into a float target to read it, which is why the channel is retained here
// rather than deleted and re-created as a second asset.
Shader "Hidden/Alrauna/Amuse/AlphaExactOne"
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
                float alpha = _MainTex.Load(coordinate).a;

                // The opaque test reads first, as AlphaPolicyBounds'
                // published verdict does. The bands cannot overlap under
                // the inspector clamp, so the order only pins the
                // behavior of bounds no production caller supplies.
                // The 1.0 ceiling keeps a value outside [0, 1] out of the
                // opaque band: the research characterization renders float
                // sources through this shader, and one bit must report
                // them exactly as it reported them before the bounds
                // arrived. The active-gate check keeps the inert noise
                // bound 0 from erasing a negative float.
                float verdict = alpha >= _OpaqueBound && alpha <= 1.0
                    ? 1.0
                    : (alpha < _NoiseBound && _NoiseBound > 0.0
                        ? 1.0 / 255.0
                        : 0.0);
                return float4(verdict, alpha, 0.0, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
