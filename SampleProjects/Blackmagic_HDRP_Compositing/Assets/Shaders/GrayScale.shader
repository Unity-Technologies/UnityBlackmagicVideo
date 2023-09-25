Shader "Hidden/BlackmagicVideo/Shader/GrayScale"
{
    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord   : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    TEXTURE2D_X(_InputTexture);
    float _Intensity;

    float4 CustomPostProcess(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        // Get ScreenSpace position and texture color.
        uint2 positionSS = input.texcoord * _ScreenSize.xy;
        float4 outColor = LOAD_TEXTURE2D_X(_InputTexture, positionSS);
        float4 finalColor = outColor;

        // We don't want to apply the GrayScale outside the Plane.
        if (outColor.w > 0.0)
        {
            // Apply the Grayscale effect based on the texture Luminance and the Intensity defined on the script.
            finalColor = float4(lerp(outColor, Luminance(outColor.xyz).xxx, _Intensity), outColor.z);
        }

        return finalColor;
    }

    ENDHLSL
    SubShader
    {
        Pass
        {
            Name "GrayScale"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off
            HLSLPROGRAM
                #pragma fragment CustomPostProcess
                #pragma vertex Vert
            ENDHLSL
        }
    }

    Fallback Off
}
