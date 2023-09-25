Shader "Hidden/BlackmagicVideo/Shader/SimpleBlit"
{
    Properties
    {
        _MainTex("", 2D) = "gray" {}
    }

    HLSLINCLUDE

#include "UnityCG.cginc"
    sampler2D _MainTex;

    void Vertex(
        uint vid : SV_VertexID,
        out float4 position : SV_Position,
        out float2 texcoord : TEXCOORD
    )
    {
        float x = (vid == 1) ? 1 : 0;
        float y = (vid == 2) ? 1 : 0;
        position = float4(x * 4 - 1, y * 4 - 1, 1, 1);
        texcoord = float2(x * 2, y * 2);

        if (_ProjectionParams.x < 0.0)
        {
            texcoord.y = 1.0 - texcoord.y;
        }
    }

    half4 Fragment(
        float4 position : SV_Position,
        float2 texcoord : TEXCOORD
    ) : SV_Target
    {
        return tex2D(_MainTex, texcoord);
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
