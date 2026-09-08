Shader "FishingMiniGame/StylizedOcean"
{
    Properties
    {
        _BaseColor("Shallow Color", Color) = (0.03, 0.47, 0.68, 1)
        _DeepColor("Deep Color", Color) = (0.015, 0.16, 0.32, 1)
        _FoamColor("Foam Color", Color) = (0.72, 0.96, 1, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.82
        _FoamStrength("Foam Strength", Range(0, 1)) = 0.34
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _DeepColor;
                half4 _FoamColor;
                half _Smoothness;
                half _FoamStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirection = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                Light mainLight = GetMainLight();
                half lightAmount = saturate(dot(normalWS, mainLight.direction)) * 0.34h + 0.66h;
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirection)), 3.0h);
                half depthBand = saturate((input.positionWS.z + 1.0h) / 58.0h);
                half ripple = sin(input.positionWS.x * 0.68h + _Time.y * 0.62h) *
                              sin(input.positionWS.z * 0.47h - _Time.y * 0.84h);
                half foam = smoothstep(0.56h, 0.92h, ripple) * _FoamStrength;

                half3 water = lerp(_BaseColor.rgb, _DeepColor.rgb, depthBand * 0.72h);
                water *= lightAmount * mainLight.color;
                water = lerp(water, _FoamColor.rgb, foam);
                water += fresnel * _FoamColor.rgb * (0.12h + _Smoothness * 0.16h);
                water = MixFog(water, input.fogFactor);
                return half4(water, 1.0h);
            }
            ENDHLSL
        }
    }
}
