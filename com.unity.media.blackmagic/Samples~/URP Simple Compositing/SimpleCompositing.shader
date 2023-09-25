Shader "Hidden/BlackmagicVideo/Shader/SimpleCompositing"
{
    Properties
    {
        _SceneTex("", 2D) = "gray" {}
        _VideoTex("", 2D) = "gray" {}
    }
    HLSLINCLUDE

#include "UnityCG.cginc"

    sampler2D _SceneTex;
    sampler2D _VideoTex;

    struct v2f
    {
        float2 uv : TEXCOORD0;
        float4 pos : SV_POSITION;
    };

    v2f Vertex(float4 vertex : POSITION,
               float2 uv : TEXCOORD0)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(vertex);
        o.uv = uv;

#if UNITY_UV_STARTS_AT_TOP
        o.uv.y = 1.0 - o.uv.y;
#endif

        if (_ProjectionParams.x < 0.0)
        {
            o.uv.y = 1.0 - o.uv.y;
        }

        return o;
    }

    half4 Fragment(v2f i) : SV_Target
    {
        float2 uv = i.uv;
        half4 sceneTex = tex2D(_SceneTex, uv);
        half4 composTex = tex2D(_VideoTex, uv);

        float4 finalColor = (sceneTex.a) * sceneTex + (1.f - sceneTex.a) * composTex;
        finalColor.a = 0;

        return finalColor;
    }

        ENDHLSL

        SubShader
    {
        Cull Off ZWrite Off ZTest Always
            Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            ENDHLSL
        }
    }
}
