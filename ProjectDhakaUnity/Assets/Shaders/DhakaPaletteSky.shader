Shader "ProjectDhaka/PaletteGradientSky"
{
    Properties
    {
        _ZenithColor ("Zenith Color", Color) = (0.114, 0.180, 0.290, 1)
        _HorizonColor ("Horizon Color", Color) = (0.294, 0.349, 0.447, 1)
        _GroundColor ("Ground Color", Color) = (0.082, 0.098, 0.141, 1)
        _HorizonExponent ("Horizon Exponent", Range(0.5, 8.0)) = 2.6
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 dirWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half _HorizonExponent;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dirWS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float y = normalize(IN.dirWS).y;
                half upT = pow(saturate(y), _HorizonExponent);
                half downT = pow(saturate(-y), _HorizonExponent);
                half3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, upT);
                col = lerp(col, _GroundColor.rgb, downT);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
