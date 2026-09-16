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
        _Color2nd ("Color2nd", Color) = (1,1,1,1)
        _Color3rd ("Color3rd", Color) = (1,1,1,1)
        _Main2ndTex ("Main2ndTex", 2D) = "white" {}
        _Main3rdTex ("Main3rdTex", 2D) = "white" {}
        _Main2ndTex_ST ("Main2ndTexST", Vector) = (1,1,0,0)
        _Main3rdTex_ST ("Main3rdTexST", Vector) = (1,1,0,0)
        _Main2ndTex_ScrollRotate ("Main2ndScrollRotate", Vector) = (0,0,0,0)
        _Main3rdTex_ScrollRotate ("Main3rdScrollRotate", Vector) = (0,0,0,0)
        _Main2ndTexAngle ("Main2ndAngle", Float) = 0
        _Main3rdTexAngle ("Main3rdAngle", Float) = 0
        _Main2ndTex_UVMode ("Main2ndUVMode", Int) = 0
        _Main3rdTex_UVMode ("Main3rdUVMode", Int) = 0
        _Main2ndTexAlphaMode ("Main2ndAlphaMode", Int) = 0
        _Main3rdTexAlphaMode ("Main3rdAlphaMode", Int) = 0
        _Main2ndTex_Cull ("Main2ndCull", Int) = 0
        _Main3rdTex_Cull ("Main3rdCull", Int) = 0
        _Main2ndTexIsDecal ("Main2ndIsDecal", Int) = 0
        _Main3rdTexIsDecal ("Main3rdIsDecal", Int) = 0
        _Main2ndTexIsLeftOnly ("Main2ndIsLeftOnly", Int) = 0
        _Main3rdTexIsLeftOnly ("Main3rdIsLeftOnly", Int) = 0
        _Main2ndTexIsRightOnly ("Main2ndIsRightOnly", Int) = 0
        _Main3rdTexIsRightOnly ("Main3rdIsRightOnly", Int) = 0
        _Main2ndTexShouldCopy ("Main2ndShouldCopy", Int) = 0
        _Main3rdTexShouldCopy ("Main3rdShouldCopy", Int) = 0
        _Main2ndTexShouldFlipMirror ("Main2ndShouldFlipMirror", Int) = 0
        _Main3rdTexShouldFlipMirror ("Main3rdShouldFlipMirror", Int) = 0
        _Main2ndTexShouldFlipCopy ("Main2ndShouldFlipCopy", Int) = 0
        _Main3rdTexShouldFlipCopy ("Main3rdShouldFlipCopy", Int) = 0
        _Main2ndTexIsMSDF ("Main2ndIsMSDF", Int) = 0
        _Main3rdTexIsMSDF ("Main3rdIsMSDF", Int) = 0
        _Main2ndBlendMask ("Main2ndBlendMask", 2D) = "white" {}
        _Main3rdBlendMask ("Main3rdBlendMask", 2D) = "white" {}
        _Main2ndDistanceFade ("Main2ndDistanceFade", Vector) = (0.1,0.01,0,0)
        _Main3rdDistanceFade ("Main3rdDistanceFade", Vector) = (0.1,0.01,0,0)
        _Main2ndDissolveParams ("Main2ndDissolveParams", Vector) = (0,0,0.5,0.1)
        _Main3rdDissolveParams ("Main3rdDissolveParams", Vector) = (0,0,0.5,0.1)
        _AudioLink2Main2nd ("AudioLink2Main2nd", Int) = 0
        _AudioLink2Main3rd ("AudioLink2Main3rd", Int) = 0
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
