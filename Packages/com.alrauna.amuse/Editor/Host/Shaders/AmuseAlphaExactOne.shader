// The AMUSE alpha evidence predicate. Editor-only: it lives under Editor/ so it
// is excluded from player builds and never reaches a built avatar.
//
// It loads ONE explicit mip level by integer texel index and emits the binary
// result of "alpha is exactly one" in RED. Load is a texel fetch: no filtering,
// no mip selection, no wrap.
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
                float alpha = _MainTex.Load(coordinate).a;
                return float4(alpha == 1.0 ? 1.0 : 0.0, alpha, 0.0, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
