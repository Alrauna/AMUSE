// Executable specification of the lilToon property contract AMUSE consumes.
// It is a purpose-built stand-in for deterministic tests, not a pretend lilToon
// distribution, and contains no upstream lilToon source.
Shader "Hidden/Alrauna/AmuseTests/LilToonSemanticTest"
{
    Properties
    {
        [HideInInspector] _lilToonVersion ("Version", Int) = 45

        _Invisible ("Invisible", Int) = 0
        _ShiftBackfaceUV ("ShiftBackfaceUV", Int) = 0
        _UDIMDiscardCompile ("UDIMDiscard", Int) = 0
        _BackfaceColor ("BackfaceColor", Color) = (0,0,0,0)

        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _MainTex_ScrollRotate ("ScrollRotate", Vector) = (0,0,0,0)
        _MainTexHSVG ("HSVG", Vector) = (0,1,1,1)
        _MainGradationStrength ("GradationStrength", Range(0,1)) = 0
        _MainColorAdjustMask ("AdjustMask", 2D) = "white" {}

        _UseMain2ndTex ("UseMain2nd", Int) = 0
        _UseMain3rdTex ("UseMain3rd", Int) = 0
        _UseParallax ("UseParallax", Int) = 0
        _UsePOM ("UsePOM", Int) = 0
        _UseAudioLink ("UseAudioLink", Int) = 0
        _UseAnisotropy ("UseAnisotropy", Int) = 0

        _UseBumpMap ("UseBumpMap", Int) = 0
        [Normal] _BumpMap ("BumpMap", 2D) = "bump" {}
        _BumpScale ("BumpScale", Range(-10,10)) = 1
        _UseBump2ndMap ("UseBump2nd", Int) = 0

        _UseEmission ("UseEmission", Int) = 0
        [HDR] _EmissionColor ("EmissionColor", Color) = (1,1,1,1)
        _EmissionMap ("EmissionMap", 2D) = "white" {}
        _EmissionMap_ScrollRotate ("ScrollRotate", Vector) = (0,0,0,0)
        _EmissionMap_UVMode ("UVMode", Int) = 0
        _EmissionMainStrength ("MainStrength", Range(0,1)) = 0
        _EmissionBlend ("Blend", Range(0,1)) = 1
        _EmissionBlendMask ("BlendMask", 2D) = "white" {}
        _EmissionBlendMode ("BlendMode", Int) = 1
        _EmissionBlink ("Blink", Vector) = (0,0,3.141593,0)
        _EmissionUseGrad ("UseGrad", Int) = 0
        _EmissionParallaxDepth ("ParallaxDepth", Float) = 0
        _EmissionFluorescence ("Fluorescence", Range(0,1)) = 0
        _AudioLink2Emission ("AudioLink2Emission", Int) = 0

        _UseEmission2nd ("UseEmission2nd", Int) = 0
        _UseReflection ("UseReflection", Int) = 0
        _UseMatCap ("UseMatCap", Int) = 0
        _UseMatCap2nd ("UseMatCap2nd", Int) = 0
        _UseRim ("UseRim", Int) = 0
        _UseRimShade ("UseRimShade", Int) = 0
        _UseGlitter ("UseGlitter", Int) = 0
        _UseBacklight ("UseBacklight", Int) = 0
        _DissolveParams ("DissolveParams", Vector) = (0,0,0.5,0.1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert(float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            fixed4 frag() : SV_Target
            {
                return fixed4(1, 1, 1, 1);
            }
            ENDCG
        }
    }
}
