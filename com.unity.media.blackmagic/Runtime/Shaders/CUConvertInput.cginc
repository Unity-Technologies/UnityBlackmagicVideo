#include "UnityCG.cginc"
#include "CUPacking.cginc"
#include "CUPackingFormatSpecific.cginc"
#include "CUColorConversions.cginc"
/*
 * BMD bmdFormat10BitRGB to Unity
 * The format is r210 (10 bit RGB 4:4:4)
 */
sampler2D _MainTex;
float4 _MainTex_TexelSize;

float4 Fragment(v2f_img input) : SV_Target
{
#ifdef RGB10Bit

    float2 uv = getSamplingUV(input.uv, _MainTex_TexelSize);
    uv.x = input.pos.x / _MainTex_TexelSize.z;
    float4 packed = tex2D(_MainTex, uv);

    // Big-endian, so we treat the bytes as RGBA32
    //     R       G        B        A
    // XXRRRRRR RRRRGGGG GGGGGGBB BBBBBBBB

    float3 unpacked;
    unpacked.r = (fmod(int(packed.r * 255.0 + .5), 64.0) * 16.0) + (int(packed.g * 255.0 + .5) / 16.0);
    unpacked.g = (fmod(int(packed.g * 255.0 + .5), 16.0) * 64.0) + (int(packed.b * 255.0 + .5) / 4.0);
    unpacked.b = (fmod(int(packed.b * 255.0 + .5), 4.0) * 256.0) + int(packed.a * 255.0 + .5);

    // normalizing
    unpacked /= 1023.0;

    return float4(RGB_INPUT(unpacked, false), 1.0);

#elif defined (RGBX10Bit)

    float2 uv = getSamplingUV(input.uv, _MainTex_TexelSize);
    uv.x = input.pos.x / _MainTex_TexelSize.z;
    float4 packed = tex2D(_MainTex, uv);

    // Big-endian, so we treat the bytes as RGBA32
    //     R       G        B        A
    // RRRRRRRR RRGGGGGG GGGGBBBB BBBBBBXX

    float3 unpacked;
    unpacked.r = (int(packed.r * 255.0 + .5) * 4.0) + (int(packed.g * 255.0 + .5) / 64.0);
    unpacked.g = (fmod(int(packed.g * 255.0 + .5), 64.0) * 16.0) + (int(packed.b * 255.0 + .5) / 16.0);
    unpacked.b = (fmod(int(packed.b * 255.0 + .5), 16.0) * 64.0) + (int(packed.a * 255.0 + .5) / 4.0);

    // normalizing
    unpacked /= 1023.0;

    return float4(RGB_INPUT(unpacked, false), 1.0);

#elif defined (RGBXLE10Bit)

    float2 uv = getSamplingUV(input.uv, _MainTex_TexelSize);
    uv.x = input.pos.x / _MainTex_TexelSize.z;
    float4 packed = tex2D(_MainTex, uv);

    // Little-endian (inversed), so we treat the bytes as ABGR32
    //     A       B        G        R
    // RRRRRRRR RRGGGGGG GGGGBBBB BBBBBBXX

    float3 unpacked;
    unpacked.r = (int(packed.a * 255.0 + .5) * 4.0) + (int(packed.b * 255.0 + .5) / 64.0);
    unpacked.g = (fmod(int(packed.b * 255.0 + .5), 64.0) * 16.0) + (int(packed.g * 255.0 + .5) / 16.0);
    unpacked.b = (fmod(int(packed.g * 255.0 + .5), 16.0) * 64.0) + (int(packed.r * 255.0 + .5) / 4.0);

    // normalizing
    unpacked /= 1023.0;

    return float4(RGB_INPUT(unpacked, false), 1.0);

#elif defined (YUV10Bit)

    const float2 uv = getSamplingUV(input.uv, _MainTex_TexelSize);
    const float dx = _MainTex_TexelSize.x;

    // Calculate packing indexes (ref: https://wiki.multimedia.cx/index.php/V210).
    const int xSampled = input.pos.x;
    const int CIndex = fmod(xSampled, 6) + .5; // Component index in the block [0-5]
    const int BIndex = int(xSampled / 6) * 4; // Block Index [RGBA first index]


    // These seems like magic numbers, but they are the specs fundamental const values
    // to unpack branching (perf pass).
    const int wordIndex[6][3] = {{1,0,2},{0,0,2},{2,1,0},{1,1,0},{0,2,1},{2,2,1}};
    const int blockIndex[6][3] = {{0,0,0},{1,0,0},{1,1,2},{2,1,2},{3,2,3},{3,2,3}};
    const float4 sourceXSample= {
        BIndex*dx + 0*dx + .5*dx,
        BIndex*dx + 1*dx + .5*dx,
        BIndex*dx + 2*dx + .5*dx,
        BIndex*dx + 3*dx + .5*dx
    };

    const float4 block1 = tex2D(_MainTex, float2(sourceXSample[blockIndex[CIndex][0]], uv.y));
    const float y = getIndexed10BitValue(block1, wordIndex[CIndex][0], false);
    const float4 block2 = tex2D(_MainTex, float2(sourceXSample[blockIndex[CIndex][1]], uv.y));
    const float u = getIndexed10BitValue(block2, wordIndex[CIndex][1], true);
    const float4 block3 = tex2D(_MainTex, float2(sourceXSample[blockIndex[CIndex][2]], uv.y));
    const float v = getIndexed10BitValue(block3, wordIndex[CIndex][2], true);

    /***
     * Unrolled algorithm of the unpacking part. Any investigation will require this version to be used
     * for proper analysis.
     *
    float y=0,u=0,v=0;
    if(CIndex == 0)
    {
        const float4 block = tex2D(_MainTex, float2(BIndex*dx + 0*dx + .275*dx, yPos));
        y = getIndexed10BitValue(block, 1, false);
        u = getIndexed10BitValue(block, 0, true);
        v = getIndexed10BitValue(block, 2, true);
    }
    if(CIndex == 1)
    {
        const float4 block = tex2D(_MainTex, float2(BIndex*dx + 1*dx + .275*dx, yPos));
        const float4 pBlock = tex2D(_MainTex, float2(BIndex*dx + 0*dx + .275*dx, yPos));
        y = getIndexed10BitValue(block, 0, false);
        u = getIndexed10BitValue(pBlock, 0, true);
        v = getIndexed10BitValue(pBlock, 2, true);
    }
    if(CIndex == 2)
    {
        const float4 block = tex2D(_MainTex, float2(BIndex*dx + 1*dx + .275*dx, yPos));
        const float4 nBlock = tex2D(_MainTex, float2(BIndex*dx + 2*dx + .275*dx, yPos));
        y = getIndexed10BitValue(block, 2, false);
        u = getIndexed10BitValue(block, 1, true);
        v = getIndexed10BitValue(nBlock, 0, true);
    }
    if(CIndex == 3)
    {
        const float4 block = tex2D(_MainTex, float2(BIndex*dx + 2*dx + .275*dx, yPos));
        const float4 pBlock = tex2D(_MainTex, float2(BIndex*dx + 1*dx + .275*dx, yPos));
        y = getIndexed10BitValue(block, 1, false);
        u = getIndexed10BitValue(pBlock, 1, true);
        v = getIndexed10BitValue(block, 0, true);
    }
    if(CIndex == 4)
    {
        const float4 block = tex2D(_MainTex, float2(BIndex*dx + 3*dx + .275*dx, yPos));
        const float4 pBlock = tex2D(_MainTex, float2(BIndex*dx + 2*dx + .275*dx, yPos));
        y = getIndexed10BitValue(block, 0, false);
        u = getIndexed10BitValue(pBlock, 2, true);
        v = getIndexed10BitValue(block, 1, true);
    }
    if(CIndex == 5)
    {
        const float4 block = tex2D(_MainTex, float2(BIndex*dx + 3*dx + .275*dx, yPos));
        const float4 pBlock = tex2D(_MainTex, float2(BIndex*dx + 2*dx + .275*dx, yPos));
        y = getIndexed10BitValue(block, 2, false);
        u = getIndexed10BitValue(pBlock, 2, true);
        v = getIndexed10BitValue(block, 1, true);
    }
    */
    return float4((YUV2RGB(float3(y,u,v))), 1.0);

#elif defined (RGB12Bit) || defined (RGBLE12Bit)
    const float xSampled = input.pos.x;
    const unsigned int blockIndex = int(xSampled / 8);
    const unsigned int patternIndex = xSampled - blockIndex * 8;
    const unsigned int pixelPosX0 = blockIndex * 9 + patternIndex;
    const unsigned int pixelPosX1 = pixelPosX0 + 1;
    const float dx = _MainTex_TexelSize.x;

    const float2 uv0 = getSamplingUV(float2(pixelPosX0*dx, input.uv.y), _MainTex_TexelSize);
    const float2 uv1 = getSamplingUV(float2(pixelPosX1*dx, input.uv.y), _MainTex_TexelSize);
    float4 packed0 = tex2D(_MainTex, uv0);
    float4 packed1 = tex2D(_MainTex, uv1);

    // Big-endian, so we treat the bytes as RGBA32
    //     R       G        B        A
    // RRRRRRRR RRRRGGGG GGGGGGBB BBBBBBBB

    packed0 = floatToInt8(packed0);
    packed1 = floatToInt8(packed1);

    // --- Unrolled version ---
    // float3 pixel0;
    // pixel0.r = COMPONENT_R(packed0) + packFloatLeftShift(COMPONENT_G(packed0), TWO_TO_THE_4, TWO_TO_THE_8);
    // pixel0.g = packFloatRightShift(COMPONENT_G(packed0), TWO_TO_THE_4) + packFloatLeftShift(COMPONENT_B(packed0), TWO_TO_THE_4);
    // pixel0.b = COMPONENT_A(packed0) + packFloatLeftShift(COMPONENT_R(packed1), TWO_TO_THE_4, TWO_TO_THE_8);

    // float3 pixel1;
    // pixel1.r = packFloatRightShift(COMPONENT_R(packed0), TWO_TO_THE_4) + packFloatLeftShift(COMPONENT_G(packed0), TWO_TO_THE_4);
    // pixel1.g = COMPONENT_B(packed0) + packFloatLeftShift(COMPONENT_A(packed0), TWO_TO_THE_4, TWO_TO_THE_8);
    // pixel1.b = packFloatRightShift(COMPONENT_A(packed0), TWO_TO_THE_4) + packFloatLeftShift(COMPONENT_R(packed1), TWO_TO_THE_4);

    // float3 pixel2;
    // pixel2.r = COMPONENT_G(packed0) + packFloatLeftShift(COMPONENT_B(packed0), TWO_TO_THE_4, TWO_TO_THE_8);
    // pixel2.g = packFloatRightShift(COMPONENT_B(packed0), TWO_TO_THE_4) + packFloatLeftShift(COMPONENT_A(packed0), TWO_TO_THE_4);
    // pixel2.b = COMPONENT_R(packed1) + packFloatLeftShift(COMPONENT_G(packed1), TWO_TO_THE_4, TWO_TO_THE_8);

    // float3 pixel3;
    // pixel3.r = packFloatRightShift(COMPONENT_G(packed0), TWO_TO_THE_4) + packFloatLeftShift(COMPONENT_B(packed0), TWO_TO_THE_4);
    // pixel3.g = COMPONENT_A(packed0) + packFloatLeftShift(COMPONENT_R(packed1), TWO_TO_THE_4, TWO_TO_THE_8);
    // pixel3.b = packFloatRightShift(COMPONENT_R(packed1), TWO_TO_THE_4) + packFloatLeftShift(COMPONENT_G(packed1), TWO_TO_THE_4);

    // float3 pixel4;
    // pixel4.r = COMPONENT_B(packed0) + packFloatLeftShift(COMPONENT_A(packed0), TWO_TO_THE_4, TWO_TO_THE_8);
    // pixel4.g = packFloatRightShift(COMPONENT_A(packed0), TWO_TO_THE_4) + packFloatLeftShift(COMPONENT_R(packed1), TWO_TO_THE_4);
    // pixel4.b = COMPONENT_G(packed1) + packFloatLeftShift(COMPONENT_B(packed1), TWO_TO_THE_4, TWO_TO_THE_8);

    // float3 pixel5;
    // pixel5.r = packFloatRightShift(COMPONENT_B(packed0), TWO_TO_THE_4) + packFloatLeftShift(COMPONENT_A(packed0), TWO_TO_THE_4);
    // pixel5.g = COMPONENT_R(packed1) + packFloatLeftShift(COMPONENT_G(packed1), TWO_TO_THE_4, TWO_TO_THE_8);
    // pixel5.b = packFloatRightShift(COMPONENT_G(packed1), TWO_TO_THE_4) + packFloatLeftShift(COMPONENT_B(packed1), TWO_TO_THE_4);

    // float3 pixel6;
    // pixel6.r = COMPONENT_A(packed0) + packFloatLeftShift(COMPONENT_R(packed1), TWO_TO_THE_4, TWO_TO_THE_8);
    // pixel6.g = packFloatRightShift(COMPONENT_R(packed1), TWO_TO_THE_4) + packFloatLeftShift(COMPONENT_G(packed1), TWO_TO_THE_4);
    // pixel6.b = COMPONENT_B(packed1) + packFloatLeftShift(COMPONENT_A(packed1), TWO_TO_THE_4, TWO_TO_THE_8);

    // float3 pixel7;
    // pixel7.r = packFloatRightShift(COMPONENT_A(packed0), TWO_TO_THE_4) + packFloatLeftShift(COMPONENT_R(packed1), TWO_TO_THE_4);
    // pixel7.g = COMPONENT_G(packed1) + packFloatLeftShift(COMPONENT_B(packed1), TWO_TO_THE_4, TWO_TO_THE_8);
    // pixel7.b = packFloatRightShift(COMPONENT_B(packed1), TWO_TO_THE_4) + packFloatLeftShift(COMPONENT_A(packed1), TWO_TO_THE_4);

    // const float3 pixels[8] =
    // {
    //     pixel0,
    //     pixel1,
    //     pixel2,
    //     pixel3,
    //     pixel4,
    //     pixel5,
    //     pixel6,
    //     pixel7
    // };

    // float3 unpacked = pixels[patternIndex];
    // --------------------------

    // --- Vectorized version ---
    const float packed[8] =
    {
        COMPONENT_R(packed0),
        COMPONENT_G(packed0),
        COMPONENT_B(packed0),
        COMPONENT_A(packed0),
        COMPONENT_R(packed1),
        COMPONENT_G(packed1),
        COMPONENT_B(packed1),
        COMPONENT_A(packed1)
    };

    const int componentIndex[8][6] =
    {
        { 0, 1, 1, 2, 3, 4 },
        { 0, 1, 2, 3, 3, 4 },
        { 1, 2, 2, 3, 4, 5 },
        { 1, 2, 3, 4, 4, 5 },
        { 2, 3, 3, 4, 5, 6 },
        { 2, 3, 4, 5, 5, 6 },
        { 3, 4, 4, 5, 6, 7 },
        { 3, 4, 5, 6, 6, 7 },
    };

    const float isOdd = int(fmod(patternIndex, 2.0) + 0.5);

    const float c0 = packed[componentIndex[patternIndex][0]];
    const float c1 = packed[componentIndex[patternIndex][1]];
    const float c2 = packed[componentIndex[patternIndex][2]];
    const float c3 = packed[componentIndex[patternIndex][3]];
    const float c4 = packed[componentIndex[patternIndex][4]];
    const float c5 = packed[componentIndex[patternIndex][5]];

    float3 unpacked;
    unpacked.r = packFloatRightShift(c0,  1.0 + 15.0 * isOdd) + packFloatLeftShift(c1,  16.0 + 240.0 * isOdd, 256.0 - 240.0 * isOdd);
    unpacked.g = packFloatRightShift(c2, 16.0 - 15.0 * isOdd) + packFloatLeftShift(c3, 256.0 - 240.0 * isOdd,  16.0 + 240.0 * isOdd);
    unpacked.b = packFloatRightShift(c4,  1.0 + 15.0 * isOdd) + packFloatLeftShift(c5,  16.0 + 240.0 * isOdd, 256.0 - 240.0 * isOdd);
    // --------------------------

    unpacked = int12ToNormalizedFloat(unpacked);

    return float4(RGB_INPUT(unpacked, true), 1.0);

#elif defined (ARGB8Bit)
    float2 uv = getSamplingUV(input.uv, _MainTex_TexelSize);
    float4 unpacked = tex2D(_MainTex, uv).gbar; // (ARGB in RGBA -> GBAR)
    return float4(RGB_INPUT(unpacked.rgb, false), unpacked.a);

#elif defined (BGRA8Bit)
    float2 uv = getSamplingUV(input.uv, _MainTex_TexelSize);
    float4 unpacked = tex2D(_MainTex, uv).bgra; // (BGRA in RGBA -> BGRA)
    return float4(RGB_INPUT(unpacked.rgb, false), unpacked.a);

#elif YUV8Bit

    // Deinterlacing
    //
    // * Sampling pattern for odd field
    //
    //     |   |
    // 5.0 +   +
    //     | x | 4.5 : Sample point for 4.0 - 6.0
    // 4.0 +---+
    //     |   |
    // 3.0 +   +
    //     | x | 2.5 : Sample point for 2.0 - 4.0
    // 2.0 +---+
    //     |   |
    // 1.0 +   +
    //     | x | 0.5 : Sample point for 0.0 - 2.0
    // 0.0 +---+
    //
    // * Sampling pattern for even field
    //
    //     | x | 5.5 : Sample point for 5.0 - 7.0
    // 5.0 +---+
    //     |   |
    // 4.0 +   +
    //     | x | 3.5 : Sample point for 3.0 - 5.0
    // 3.0 +---+
    //     |   |
    // 2.0 +   +
    //     | x | 1.5 : Sample point for 1.0 - 3.0
    // 1.0 +---+
    //     |   |
    // 0.0 +---+

    // Upsample from 4:2:2
    float2 uv = getSamplingUV(input.uv, _MainTex_TexelSize);
    uv.x -= .5*_MainTex_TexelSize.x;
    half4 uyvy = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv);
    bool sel = frac(uv.x * _MainTex_TexelSize.z) < 0.5;
    half3 yuv = sel ? uyvy.yxz : uyvy.wxz;

    return half4(YUV2RGB_8BITS(yuv), 1);

#elif defined (NO_PACKING)

    float2 uv = getSamplingUV(input.uv, _MainTex_TexelSize);
    float4 unpacked = tex2D(_MainTex, uv).rgba;
    return float4(RGB_INPUT(unpacked.rgb, false), unpacked.a);

#elif defined (CLIP_ONLY)

    float2 uv = getSamplingUV(input.uv, _MainTex_TexelSize);
    float4 unpacked = tex2D(_MainTex, uv).rgba;
    return float4(UnclipYUVSignal(unpacked.rgb), unpacked.a);

#elif defined (LINGAMMA_ONLY)

    float2 uv = getSamplingUV(input.uv, _MainTex_TexelSize);
    float4 unpacked = tex2D(_MainTex, uv).rgba;
    return float4(GammaToLinearSpaceBMD(unpacked.rgb), unpacked.a);

#elif defined (PASSTHROUGH)

    float2 uv = getSamplingUV(input.uv, _MainTex_TexelSize);
    return tex2D(_MainTex, uv).rgba;

#endif

    // No conversion format found in the keywords
    return float4(0,0,0,0);
}
