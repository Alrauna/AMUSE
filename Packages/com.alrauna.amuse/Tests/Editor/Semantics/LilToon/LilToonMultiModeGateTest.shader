// Purpose-built stand-in for the Multi mode-consistency gate fixtures. It
// declares exactly the two scalar facts the gate reads, so a fixture that
// sets the scalars genuinely carries them through the production capture.
// It is not a pretend lilToon distribution and contains no upstream
// lilToon source.
Shader "Hidden/Alrauna/AmuseTests/LilToonMultiModeGateTest"
{
    Properties
    {
        _UseClippingCanceller ("UseClippingCanceller", Float) = 0
        _AsOverlay ("AsOverlay", Float) = 0
        // The vendor keyword-writer condition inputs. Declaring them lets a
        // fixture set real captured values instead of relying on absent
        // property fallbacks.
        _UseShadow ("UseShadow", Float) = 0
        _UseRimShade ("UseRimShade", Float) = 0
        _UseEmission ("UseEmission", Float) = 0
        _UseEmission2nd ("UseEmission2nd", Float) = 0
        _EmissionBlendMask ("EmissionBlendMask", 2D) = "white" {}
        _Emission2ndBlendMask ("Emission2ndBlendMask", 2D) = "white" {}
        _UseBumpMap ("UseBumpMap", Float) = 0
        _UseBump2ndMap ("UseBump2ndMap", Float) = 0
        _UseAnisotropy ("UseAnisotropy", Float) = 0
        _UseMatCap ("UseMatCap", Float) = 0
        _UseMatCap2nd ("UseMatCap2nd", Float) = 0
        _MatCapCustomNormal ("MatCapCustomNormal", Float) = 0
        _MatCap2ndCustomNormal ("MatCap2ndCustomNormal", Float) = 0
        _UseRim ("UseRim", Float) = 0
        _RimDirStrength ("RimDirStrength", Float) = 0
        _UseGlitter ("UseGlitter", Float) = 0
        _UseAudioLink ("UseAudioLink", Float) = 0
        _AudioLinkAsLocal ("AudioLinkAsLocal", Float) = 0
        _UseBacklight ("UseBacklight", Float) = 0
        _UseParallax ("UseParallax", Float) = 0
        _UsePOM ("UsePOM", Float) = 0
        _UseReflection ("UseReflection", Float) = 0
        _MainGradationStrength ("MainGradationStrength", Float) = 0
        _MainTexHSVG ("MainTexHSVG", Vector) = (0, 1, 1, 1)
        _UseMain2ndTex ("UseMain2ndTex", Float) = 0
        _UseMain3rdTex ("UseMain3rdTex", Float) = 0
        _Main2ndTexDecalAnimation ("Main2ndTexDecalAnimation", Vector) = (1, 1, 1, 30)
        _Main3rdTexDecalAnimation ("Main3rdTexDecalAnimation", Vector) = (1, 1, 1, 30)
        _Main2ndDissolveParams ("Main2ndDissolveParams", Vector) = (0, 0, 0, 0)
        _Main3rdDissolveParams ("Main3rdDissolveParams", Vector) = (0, 0, 0, 0)
        _UseDither ("UseDither", Float) = 0
        _AlphaMaskMode ("AlphaMaskMode", Float) = 0
        _DissolveParams ("DissolveParams", Vector) = (0, 0, 0.5, 0.1)
        _DistanceFade ("DistanceFade", Vector) = (0.1, 0.01, 0, 0)
        _OutlineTexHSVG ("OutlineTexHSVG", Vector) = (0, 1, 1, 1)
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
