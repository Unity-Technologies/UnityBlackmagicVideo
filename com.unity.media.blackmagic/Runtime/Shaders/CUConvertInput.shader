Shader "Hidden/BlackmagicVideo/CUConvertInput"
{
    Properties
    {
        _MainTex("", 2D) = "" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Fragment
            #pragma multi_compile _ LINEAR_COLOR_CONVERSION
            #pragma multi_compile _ WORKING_SPACE_CONVERSION
            #pragma multi_compile BT601 BT709 BT2020
            #pragma multi_compile NO_PACKING CLIP_ONLY LINGAMMA_ONLY PASSTHROUGH YUV8Bit YUV10Bit ARGB8Bit BGRA8Bit RGB10Bit RGBXLE10Bit RGBX10Bit RGB12Bit RGBLE12Bit
            #include "CUConvertInput.cginc"
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Fragment
            #pragma multi_compile _ LINEAR_COLOR_CONVERSION
            #pragma multi_compile _ WORKING_SPACE_CONVERSION
            #pragma multi_compile BT601 BT709 BT2020
            #pragma multi_compile NO_PACKING CLIP_ONLY LINGAMMA_ONLY PASSTHROUGH YUV8Bit YUV10Bit ARGB8Bit BGRA8Bit RGB10Bit RGBXLE10Bit RGBX10Bit RGB12Bit RGBLE12Bit
            #define BLACKMAGIC_DEINTERLACE_ODD
            #include "CUConvertInput.cginc"
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Fragment
            #pragma multi_compile _ LINEAR_COLOR_CONVERSION
            #pragma multi_compile _ WORKING_SPACE_CONVERSION
            #pragma multi_compile BT601 BT709 BT2020
            #pragma multi_compile NO_PACKING CLIP_ONLY LINGAMMA_ONLY PASSTHROUGH YUV8Bit YUV10Bit ARGB8Bit BGRA8Bit RGB10Bit RGBXLE10Bit RGBX10Bit RGB12Bit RGBLE12Bit
            #define BLACKMAGIC_DEINTERLACE_EVEN
            #include "CUConvertInput.cginc"
            ENDCG
        }
    }
}
