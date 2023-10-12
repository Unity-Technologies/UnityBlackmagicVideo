#include "UnityCG.cginc"
#include "CUPacking.cginc"
#include "CUPackingFormatSpecific.cginc"
#include "CUColorConversions.cginc"

sampler2D _MainTex;
sampler2D _FieldTex;

float4 _MainTex_TexelSize;


float4 Fragment(v2f_img input) : SV_Target
{
    // Input Sampling
    float2 uv = input.uv;

    // Invert Y axis
    #if defined(VERTICAL_FLIP)
    uv.y = 1 - uv.y;
    #endif

    // Per Format packing invocation

    // *******************************************************************
#ifdef RGB10Bit
    const float3 ts = float3(_MainTex_TexelSize.xy, 0);
    uv.x = input.pos.x / _MainTex_TexelSize.z;

    // Sample
    float4 sample;
#ifdef SUBSAMPLER_INTERLACE
    float iy = input.uv.y * _MainTex_TexelSize.w;
    if (frac(iy / 2) < 0.51)
    {
        sample = tex2D(_FieldTex, uv);
    } else {
#endif
        sample = tex2D(_MainTex, uv);
#ifdef SUBSAMPLER_INTERLACE
    }
#endif
    sample.rgb = clamp(RGB_OUTPUT(sample.rgb, false), 0.0, 1.0);

    return packRGBinR210(sample.rgb);

    // *******************************************************************
#elif defined (RGBX10Bit)

    const float3 ts = float3(_MainTex_TexelSize.xy, 0);
    uv.x = input.pos.x / _MainTex_TexelSize.z;

    // Sample
    float4 sample;
#ifdef SUBSAMPLER_INTERLACE
    float iy = input.uv.y * _MainTex_TexelSize.w;
    if (frac(iy / 2) < 0.51)
    {
        sample = tex2D(_FieldTex, uv);
    } else {
#endif
        sample = tex2D(_MainTex, uv);
#ifdef SUBSAMPLER_INTERLACE
    }
#endif

    sample.rgb = RGB_OUTPUT(sample.rgb, false);

    return packRGBinR10B(sample.rgb);

    // *******************************************************************
#elif defined (RGBXLE10Bit)

    const float3 ts = float3(_MainTex_TexelSize.xy, 0);
    uv.x = input.pos.x / _MainTex_TexelSize.z;

    // Sample
    float4 sample;
#ifdef SUBSAMPLER_INTERLACE
    float iy = input.uv.y * _MainTex_TexelSize.w;
    if (frac(iy / 2) < 0.51)
    {
        sample = tex2D(_FieldTex, uv);
    } else {
#endif
        sample = tex2D(_MainTex, uv);
#ifdef SUBSAMPLER_INTERLACE
    }
#endif

    sample.rgb = RGB_OUTPUT(sample.rgb, false);

    return packRGBinR10l(sample.rgb);

    // *******************************************************************
#elif defined (YUV10Bit)

    // Calculate the out index (ref: https://wiki.multimedia.cx/index.php/V210).
    const unsigned int xSampled = input.pos.x;
    const unsigned int patternIndex = fmod(xSampled, 4); // The pattern index of the packed block to assemble [0-3]

    const float dx = _MainTex_TexelSize.x;
    const unsigned int blockIndex = int(xSampled / 4) * 6; // The pixel index of the start of the patterns

    // Interlacing Support
    float3 yuv0,yuv1,yuv2,yuv3,yuv4,yuv5;
#ifdef SUBSAMPLER_INTERLACE
    float iy = input.uv.y * _MainTex_TexelSize.w;
    if (frac(iy / 2) < 0.51)
    {
        // Sample and convert scene texture
        yuv0 = RGB2YUV(tex2D(_FieldTex, float2(blockIndex*dx + 0*dx + .275*dx, uv.y)));
        yuv1 = RGB2YUV(tex2D(_FieldTex, float2(blockIndex*dx + 1*dx + .275*dx, uv.y)));
        yuv2 = RGB2YUV(tex2D(_FieldTex, float2(blockIndex*dx + 2*dx + .275*dx, uv.y)));
        yuv3 = RGB2YUV(tex2D(_FieldTex, float2(blockIndex*dx + 3*dx + .275*dx, uv.y)));
        yuv4 = RGB2YUV(tex2D(_FieldTex, float2(blockIndex*dx + 4*dx + .275*dx, uv.y)));
        yuv5 = RGB2YUV(tex2D(_FieldTex, float2(blockIndex*dx + 5*dx + .275*dx, uv.y)));
    } else {
#endif
        // Sample and convert scene texture
        yuv0 = RGB2YUV(tex2D(_MainTex, float2(blockIndex*dx + 0*dx + .275*dx, uv.y)));
        yuv1 = RGB2YUV(tex2D(_MainTex, float2(blockIndex*dx + 1*dx + .275*dx, uv.y)));
        yuv2 = RGB2YUV(tex2D(_MainTex, float2(blockIndex*dx + 2*dx + .275*dx, uv.y)));
        yuv3 = RGB2YUV(tex2D(_MainTex, float2(blockIndex*dx + 3*dx + .275*dx, uv.y)));
        yuv4 = RGB2YUV(tex2D(_MainTex, float2(blockIndex*dx + 4*dx + .275*dx, uv.y)));
        yuv5 = RGB2YUV(tex2D(_MainTex, float2(blockIndex*dx + 5*dx + .275*dx, uv.y)));
#ifdef SUBSAMPLER_INTERLACE
    }
#endif

    // prepare 2:2 values upfront to avoid recalculation
    yuv0.g = avg(yuv0.g, yuv1.g);
    yuv0.b = avg(yuv0.b, yuv1.b);
    yuv2.g = avg(yuv2.g, yuv3.g);
    yuv2.b = avg(yuv2.b, yuv3.b);
    yuv4.g = avg(yuv4.g, yuv5.g);
    yuv4.b = avg(yuv4.b, yuv5.b);

    // yuv values contains yuv components on the rgb channels (y = r, u = g, v = b)
    const float patterns[4][3] =
    {
        // block 1, bits  0 -  9: U0+1
        // block 1, bits 10 - 19: Y0
        // block 1, bits 20 - 29: V0+1
        { yuv0.g, yuv0.r, yuv0.b },

        // block 2, bits  0 -  9: Y1
        // block 2, bits 10 - 19: U2+3
        // block 2, bits 20 - 29: Y2
        { yuv1.r, yuv2.g, yuv2.r },

        // block 3, bits  0 -  9: V2+3
        // block 3, bits 10 - 19: Y3
        // block 3, bits 20 - 29: U4+5
        { yuv2.b, yuv3.r, yuv4.g },

        // block 4, bits  0 -  9: Y4
        // block 4, bits 10 - 19: V4+5
        // block 4, bits 20 - 29: Y5
        { yuv4.r, yuv4.b, yuv5.r }
    };

    return packYUVinV210(
        patterns[patternIndex][0],
        patterns[patternIndex][1],
        patterns[patternIndex][2]
    );

    // *******************************************************************
#elif defined (RGB12Bit) || defined (RGBLE12Bit)
    const float xSampled = input.pos.x;
    const unsigned int blockIndex = int(xSampled / 9);
    const unsigned int patternIndex = xSampled - blockIndex * 9;
    unsigned int pixelPosX0 = int(float(patternIndex) * 8.0 / 9.0);
    unsigned int pixelPosX1 = min(pixelPosX0 + 1, 7);
    pixelPosX0 += blockIndex * 8;
    pixelPosX1 += blockIndex * 8;
    const float dx = _MainTex_TexelSize.x;

    // Interlace Sampling
    float4 rgb0, rgb1;
#ifdef SUBSAMPLER_INTERLACE
    float iy = input.uv.y * _MainTex_TexelSize.w;
    if (frac(iy / 2) < 0.51)
    {
        rgb0 = tex2D(_FieldTex, float2((pixelPosX0 + 0.275) * dx, uv.y));
        rgb1 = tex2D(_FieldTex, float2((pixelPosX1 + 0.275) * dx, uv.y));
    } else {
#endif
        rgb0 = tex2D(_MainTex, float2((pixelPosX0 + 0.275) * dx, uv.y));
        rgb1 = tex2D(_MainTex, float2((pixelPosX1 + 0.275) * dx, uv.y));
#ifdef SUBSAMPLER_INTERLACE
    }
#endif

    rgb0.rgb = RGB_OUTPUT(rgb0.rgb, true);
    rgb1.rgb = RGB_OUTPUT(rgb1.rgb, true);
    return packRGBinR12B(rgb0.rgb, rgb1.rgb, patternIndex);

    // *******************************************************************
#elif defined (ARGB8Bit)
    const float3 ts = float3(_MainTex_TexelSize.xy, 0);

    // Sample
    float4 sample;
#ifdef SUBSAMPLER_INTERLACE
    float iy = input.uv.y * _MainTex_TexelSize.w;
    if (frac(iy / 2) < 0.51)
    {
        sample = tex2D(_FieldTex, uv);
    } else {
#endif
        sample = tex2D(_MainTex, uv);
#ifdef SUBSAMPLER_INTERLACE
    }
#endif

    sample.rgb = RGB_OUTPUT(sample.rgb, true);
    return half4( sample[3], sample[0], sample[1], sample[2]);

    // *******************************************************************
#elif defined (BGRA8Bit)
    const float3 ts = float3(_MainTex_TexelSize.xy, 0);

    // Sample
    float4 sample;
#ifdef SUBSAMPLER_INTERLACE
    float iy = input.uv.y * _MainTex_TexelSize.w;
    if (frac(iy / 2) < 0.51)
    {
        sample = tex2D(_FieldTex, uv);
    } else {
#endif
        sample = tex2D(_MainTex, uv);
#ifdef SUBSAMPLER_INTERLACE
    }
#endif

    sample.rgb = RGB_OUTPUT(sample.rgb, true);
    return half4(sample[2], sample[1], sample[0], sample[3]);

    // *******************************************************************
#elif YUV8Bit

    const float3 ts = float3(_MainTex_TexelSize.xy, 0);

    // Sample and convert scene texture
    half3 yuv1 = (RGB2YUV_8BITS(tex2D(_MainTex, uv        )));
    half3 yuv2 = (RGB2YUV_8BITS(tex2D(_MainTex, uv + ts.xz)));

#ifdef SUBSAMPLER_INTERLACE
    half3 yuv3 = (RGB2YUV_8BITS(tex2D(_FieldTex, uv        )));
    half3 yuv4 = (RGB2YUV_8BITS(tex2D(_FieldTex, uv + ts.xz)));

    float iy = input.uv.y * _MainTex_TexelSize.w;
    if (frac(iy / 2) < 0.51)
    {
        yuv1 = yuv3;
        yuv2 = yuv4;
    }
#endif

    half u = (yuv1.y + yuv2.y) * 0.5;
    half v = (yuv1.z + yuv2.z) * 0.5;

    half4 result = half4(u, yuv1.x, v, yuv2.x);
    return half4(result.x, result.y, result.z, yuv2.x);

    // *******************************************************************
#elif defined (NO_PACKING)

    const float3 ts = float3(_MainTex_TexelSize.xy, 0);

    // Sample
    float4 sample;
#ifdef SUBSAMPLER_INTERLACE
    float iy = input.uv.y * _MainTex_TexelSize.w;
    if (frac(iy / 2) < 0.51)
    {
        sample = tex2D(_FieldTex, uv);
    }
    else {
#endif
        sample = tex2D(_MainTex, uv);
#ifdef SUBSAMPLER_INTERLACE
    }
#endif
    return float4(RGB_OUTPUT(sample.rgb,false), sample.a);

    // *******************************************************************
#elif defined (CLIP_ONLY)

    const float3 ts = float3(_MainTex_TexelSize.xy, 0);

    // Sample
    float4 sample;
#ifdef SUBSAMPLER_INTERLACE
    float iy = input.uv.y * _MainTex_TexelSize.w;
    if (frac(iy / 2) < 0.51)
    {
        sample = tex2D(_FieldTex, uv);
    }
    else {
#endif
        sample = tex2D(_MainTex, uv);
#ifdef SUBSAMPLER_INTERLACE
    }
#endif
    return float4(ClipYUVSignal(sample.rgb), sample.a);

    // *******************************************************************
#elif defined (LINGAMMA_ONLY)

    const float3 ts = float3(_MainTex_TexelSize.xy, 0);

    // Sample
    float4 sample;
#ifdef SUBSAMPLER_INTERLACE
    float iy = input.uv.y * _MainTex_TexelSize.w;
    if (frac(iy / 2) < 0.51)
    {
        sample = tex2D(_FieldTex, uv);
    }
    else {
#endif
        sample = tex2D(_MainTex, uv);
#ifdef SUBSAMPLER_INTERLACE
    }
#endif
    return float4(LinearToGammaSpaceBMD(sample.rgb), sample.a);

    // *******************************************************************
#elif defined (PASSTHROUGH)

    const float3 ts = float3(_MainTex_TexelSize.xy, 0);

    // Sample
    float4 sample;
#ifdef SUBSAMPLER_INTERLACE
    float iy = input.uv.y * _MainTex_TexelSize.w;
    if (frac(iy / 2) < 0.51)
    {
        sample = tex2D(_FieldTex, uv);
    }
    else {
#endif
        sample = tex2D(_MainTex, uv);
#ifdef SUBSAMPLER_INTERLACE
    }
#endif
    return float4(sample.rgb, sample.a);
#endif

    // *******************************************************************
    return float4(0,0,0,0); // You need to define a packing to interpret the data
}
