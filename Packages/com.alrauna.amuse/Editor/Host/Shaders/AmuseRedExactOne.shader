// The AMUSE red-channel evidence predicate. Editor-only: it lives under
// Editor/ so it is excluded from player builds and never reaches a built
// avatar.
//
// It loads ONE explicit mip level by integer texel index and emits the binary
// result of "red is exactly one" in RED. Load is a texel fetch: no filtering,
// no mip selection, no wrap. Whether or not the fetch applies the sRGB
// transfer, the decode is monotone and maps exactly 1.0 -> 1.0, so byte 255
// is exactly the stored texel whose sampled value is one and the predicate is
// decode-proof.
//
// GREEN carries the raw red and is a RESEARCH DIAGNOSTIC ONLY, mirroring the
// alpha predicate. Production renders this shader into a
// GraphicsFormat.R8_UNorm target, which stores only the red component, so
// green is discarded before any production code sees a result.
Shader "Hidden/Alrauna/Amuse/RedExactOne"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
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
                return float4(mask == 1.0 ? 1.0 : 0.0, mask, 0.0, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
