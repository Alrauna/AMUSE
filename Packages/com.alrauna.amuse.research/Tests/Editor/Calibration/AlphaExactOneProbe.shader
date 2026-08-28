// Characterization support for AlphaEvidenceProbe. Editor-only, research-only:
// this shader is never referenced by the AMUSE product package and is not part of
// any release artifact.
//
// It loads ONE explicit mip level by integer texel index and emits the binary
// result of "alpha is exactly one". Load is a texel fetch: no filtering, no mip
// selection, no wrap. The raw alpha rides along in green so a characterization
// case can compare magnitudes; a production extraction would render only the
// red channel into an R8 target.
Shader "Hidden/Alrauna/Amuse/Research/AlphaExactOneProbe"
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
