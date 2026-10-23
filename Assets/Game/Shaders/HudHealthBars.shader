Shader "ColonyRTS/HudHealthBars"
{
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; };
            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                return output;
            }
            float4 frag(v2f input) : SV_Target { return input.color; }
            ENDCG
        }
    }
    Fallback Off
}
