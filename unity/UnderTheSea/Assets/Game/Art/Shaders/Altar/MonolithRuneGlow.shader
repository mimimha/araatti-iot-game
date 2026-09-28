// 돌기둥 문양을 아래에서 위로 점등하는 발광 전용 레이어.
//
// ⚠ 이 셰이더는 돌을 그리지 않는다. 돌은 계속 Synty/Generic_Basic 이 그린다.
//    Monolith 의 MeshRenderer 두 번째 Material 슬롯에 얹혀서 **빛만 더한다**(가산 합성).
//    그래서 발광이 0 이면 더하는 값도 0 이고, 평소 외형은 손대기 전과 완전히 같다.
//    Synty 원본 셰이더 · Material · 아틀라스는 하나도 고치지 않는다. (요청 §4 · §19)
//
// 발광 계산은 설계가 정한 그대로다. (요청 §3)
//
//     FinalEmission = RuneEmissionMask x VerticalReveal x GlowColor
//
// 세로 방향은 UV 가 아니라 **Object Space Y** 로 판단한다. 문양이 별도 지오메트리이고,
// 이 메시의 문양은 UV 가 단색 swatch 한 점으로 모여 있어 UV 로는 위아래를 알 수 없다. (요청 §5)
//
// ⚠ ShadowCaster 패스가 없다. 그림자는 계속 Synty Material 만 만든다.
Shader "Altar/MonolithRuneGlow"
{
    Properties
    {
        _RuneMask ("Rune Emission Mask", 2D) = "black" {}

        [HDR] _RuneGlowColor ("Glow Color (HDR)", Color) = (0,0,0,1)

        _RuneGlowProgress ("Glow Progress", Range(0,1)) = 0

        _RuneGlowMinY ("Rune Min Y (object space)", Float) = 2.320778
        _RuneGlowMaxY ("Rune Max Y (object space)", Float) = 7.591467

        _RuneGlowFeather ("Reveal Feather", Range(0.001,0.5)) = 0.07

        // 점등 경계의 얇은 밝은 선. 1 이면 꺼진 것과 같다. (요청 §7)
        _RuneFrontBoost ("Front Highlight Boost", Range(1,3)) = 1.35
        _RuneFrontWidth ("Front Highlight Width", Range(0.001,0.3)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "RuneGlow"
            Tags { "LightMode" = "UniversalForward" }

            // 빛을 더하기만 한다. 깊이를 쓰지 않아 뒤 오브젝트 정렬을 건드리지 않고,
            // 같은 면에 겹쳐 그리므로 살짝 앞으로 당겨 ZTest 를 확실히 통과시킨다.
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  objectY    : TEXCOORD1;
            };

            TEXTURE2D(_RuneMask);
            SAMPLER(sampler_RuneMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _RuneMask_ST;
                float4 _RuneGlowColor;
                float  _RuneGlowProgress;
                float  _RuneGlowMinY;
                float  _RuneGlowMaxY;
                float  _RuneGlowFeather;
                float  _RuneFrontBoost;
                float  _RuneFrontWidth;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _RuneMask);

                // ⚠ 스케일 · 회전 · 부모를 타기 전의 메시 좌표다. 그래서 프리팹에서
                //    Monolith 를 키우거나 돌려도 Min/Max Y 를 다시 맞출 필요가 없다.
                OUT.objectY = IN.positionOS.y;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // 문양만 1, 돌과 이끼는 0. 기존 마스크를 그대로 쓴다.
                half mask = SAMPLE_TEXTURE2D(_RuneMask, sampler_RuneMask, IN.uv).r;

                // 문양의 세로 범위를 0~1 로 편다.
                float denom = max(_RuneGlowMaxY - _RuneGlowMinY, 1e-4);
                float ny = saturate((IN.objectY - _RuneGlowMinY) / denom);

                // 아래(ny 가 작은 쪽)가 먼저 1 이 된다. 지나간 아래쪽은 계속 1 로 남는다 —
                // 띠가 지나가고 뒤가 꺼지는 방식이 아니라 아래에서부터 차오르는 방식이다. (요청 §2 · §6)
                float reveal = 1.0 - smoothstep(_RuneGlowProgress - _RuneGlowFeather,
                                                _RuneGlowProgress,
                                                ny);

                // 지금 점등되는 경계만 살짝 더 밝게. reveal 을 곱하므로 아직 안 켜진 위쪽으로 새지 않는다.
                // progress 가 1 에 가까워지면(=전체 점등 유지 구간) 자연히 사라진다.
                float front = 1.0 - smoothstep(0.0, _RuneFrontWidth, abs(ny - _RuneGlowProgress));
                float frontFade = 1.0 - smoothstep(0.85, 1.0, _RuneGlowProgress);
                float boost = lerp(1.0, _RuneFrontBoost, front * frontFade);

                half3 rgb = _RuneGlowColor.rgb * (mask * reveal * boost);

                // 가산 합성이라 alpha 는 쓰이지 않는다.
                return half4(rgb, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
