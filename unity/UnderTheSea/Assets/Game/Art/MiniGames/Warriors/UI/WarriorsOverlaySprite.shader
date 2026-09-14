// The ROUND 2 weakness marker has to be readable at all times, and it lives right among the
// kraken's arms - depth offsets and raised anchors both lost to geometry that curves around it.
// Drawing it with the depth test off is the only thing that guarantees the whole ring is there.
Shader "Warriors/OverlaySprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        // Font atlases keep the glyph in the alpha channel and leave RGB black, so multiplying
        // the sampled colour straight through paints the arrow black. Set this to 1 for text.
        [Toggle] _AlphaOnly ("Alpha Only (font atlas)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _AlphaOnly;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 sampled = lerp(tex, fixed4(1, 1, 1, tex.a), _AlphaOnly);
                return sampled * i.color;
            }
            ENDCG
        }
    }
}
