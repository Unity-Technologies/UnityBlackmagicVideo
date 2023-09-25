
float getIndexed10BitValue( float4 block, int i, bool isSigned)
{
    // Little-endian (inversed), so we treat the bytes as ABGR32
    //     A       B        G        R
    // XXYYYYYY YYYYUUUU UUUUUUVV VVVVVVVV
    //       i = 2     i = 1       i = 0  (10 bits values)
    int ret = 0.0;
    if (i == 0) ret = (fmod(int(block.g * 255.0 + .5), 4.0) * 256.0) + (block.r * 255.0 + .5);
    if (i == 1) ret = (fmod(int(block.b * 255.0 + .5), 16.0) * 64.0) + (int(block.g * 255.0 + .5) / 4.0);
    if (i == 2) ret = (fmod(int(block.a * 255.0 + .5), 64.0) * 16.0) + (int(block.b * 255.0 + .5) / 16.0);

    // normalizing
    float val = ret / 1023.0;

    // UV handling and signal clipping
    if (isSigned)
    {
        val = (val - 16.0/255.0) / (224.0/255.0);
        return clamp(val, 0.0, 1.0) - 128.0/255.0;
    }
    val = (val - 16.0/255.0) / ((235.0-16.0)/255.0);
    return clamp(val, 0.0, 1.0);
}

float4 packRGBinR210(float3 rgb)
{
    rgb = clamp(rgb, 0, 1);
    // Big-endian, so we treat the bytes as RGBA32
    //     R       G        B        A
    // XXRRRRRR RRRRGGGG GGGGGGBB BBBBBBBB

    int4 packed;
    packed.r = fmod(int(rgb.r * 1023.0 + .5) / 16.0, 64.0);
    packed.g = (fmod(int(rgb.r * 1023.0 + .5), 16.0) * 16.0) + (int(rgb.g * 1023.0 + .5) / 64.0);
    packed.b = (fmod(int(rgb.g * 1023.0 + .5), 64.0) * 4.0) + (int(rgb.b * 1023.0 + .5) / 256.0);
    packed.a = fmod(int(rgb.b * 1023.0 + .5), 256.0);

    // Normalize
    return clamp(packed / 255.0, 0.0, 1.0);
}

float4 packRGBinR10B(float3 rgb)
{
    rgb = clamp(rgb, 0, 1);
    // Big-endian, so we treat the bytes as RGBA32
    //     R       G        B        A
    // RRRRRRRR RRGGGGGG GGGGBBBB BBBBBBXX

    int4 packed;
    packed.r = int(rgb.r * 1023.0 + .5) / 4.0;
    packed.g = (fmod(int(rgb.r * 1023.0 + .5), 4.0) * 64.0) + (int(rgb.g * 1023.0 + .5) / 16.0);
    packed.b = (fmod(int(rgb.g * 1023.0 + .5), 16.0) * 16.0) + (int(rgb.b * 1023.0 + .5) / 64.0);
    packed.a = fmod(int(rgb.b * 1023.0 + .5), 64.0) * 4.0;

    // Normalize
    return clamp(packed / 255.0, 0.0, 1.0);
}

float4 packRGBinR10l(float3 rgb)
{
    rgb = clamp(rgb, 0, 1);
    // Little-endian (inversed), so we treat the bytes as ABGR32
    //     A       B        G        R
    // RRRRRRRR RRGGGGGG GGGGBBBB BBBBBBXX

    int4 packed;
    packed.a = int(rgb.r * 1023.0 + .5) / 4.0;
    packed.b = (fmod(int(rgb.r * 1023.0 + .5), 4.0) * 64.0) + (int(rgb.g * 1023.0 + .5) / 16.0);
    packed.g = (fmod(int(rgb.g * 1023.0 + .5), 16.0) * 16.0) + (int(rgb.b * 1023.0 + .5) / 64.0);
    packed.r = fmod(int(rgb.b * 1023.0 + .5), 64.0) * 4.0;

    // Normalize
    return clamp(packed / 255.0, 0.0, 1.0);
}

float4 packYUVinV210(float v, float u, float y)
{
    v = clamp(v, 0, 1);
    u = clamp(u, 0, 1);
    y = clamp(y, 0, 1);

    // Endianness is inversed, so we treat the bytes as ABGR32
    //     A       B        G        R
    // XXYYYYYY YYYYUUUU UUUUUUVV VVVVVVVV
    //       i = 2     i = 1       i = 0  (10 bits values)
    int4 ret;
    ret.r = fmod(int(v * 1023.f + .5f), 256);
    ret.g = fmod(int(u * 1023.f + .5f), 64) * 4 + int(v * 1023.f + .5) / 256.f;
    ret.b = fmod(int(y * 1023.f + .5f), 16) * 16 + int(u * 1023.f + .5) / 64.f;
    ret.a = int(y * 1023.f + .5) / 16.0;

    // Normalize
    return ret / 255.f;
}

float4 packRGBinR12B(float3 rgb0, float3 rgb1, int index)
{
    // Big-endian, so we treat the bytes as RGBA32
    //     R       G        B        A
    // RRRRRRRR RRRRGGGG GGGGGGBB BBBBBBBB

    int3 pixel0 = floatToInt12(rgb0);
    int3 pixel1 = floatToInt12(rgb1);

    // --- Unrolled version ---
    // int4 word0;
    // COMPONENT_R(word0) = packFloatClamp(pixel0.r, TWO_TO_THE_8);
    // COMPONENT_G(word0) = packFloatRightShift(pixel0.r, TWO_TO_THE_8) + packFloatLeftShift(pixel0.g, TWO_TO_THE_4, TWO_TO_THE_4);
    // COMPONENT_B(word0) = packFloatRightShift(pixel0.g, TWO_TO_THE_4);
    // COMPONENT_A(word0) = packFloatClamp(pixel0.b, TWO_TO_THE_8);

    // int4 word1;
    // COMPONENT_R(word1) = packFloatRightShift(pixel0.b, TWO_TO_THE_8) + packFloatLeftShift(pixel1.r, TWO_TO_THE_4, TWO_TO_THE_4);
    // COMPONENT_G(word1) = packFloatRightShift(pixel1.r, TWO_TO_THE_4);
    // COMPONENT_B(word1) = packFloatClamp(pixel1.g, TWO_TO_THE_8);
    // COMPONENT_A(word1) = packFloatRightShift(pixel1.g, TWO_TO_THE_8) + packFloatLeftShift(pixel1.b, TWO_TO_THE_4, TWO_TO_THE_4);

    // int4 word2;
    // COMPONENT_R(word2) = packFloatRightShift(pixel0.b, TWO_TO_THE_4);
    // COMPONENT_G(word2) = packFloatClamp(pixel1.r, TWO_TO_THE_8);
    // COMPONENT_B(word2) = packFloatRightShift(pixel1.r, TWO_TO_THE_8) + packFloatLeftShift(pixel1.g, TWO_TO_THE_4, TWO_TO_THE_4);
    // COMPONENT_A(word2) = packFloatRightShift(pixel1.g, TWO_TO_THE_4);

    // int4 word3;
    // COMPONENT_R(word3) = packFloatClamp(pixel0.b, TWO_TO_THE_8);
    // COMPONENT_G(word3) = packFloatRightShift(pixel0.b, TWO_TO_THE_8) + packFloatLeftShift(pixel1.r, TWO_TO_THE_4, TWO_TO_THE_4);
    // COMPONENT_B(word3) = packFloatRightShift(pixel1.r, TWO_TO_THE_4);
    // COMPONENT_A(word3) = packFloatClamp(pixel1.g, TWO_TO_THE_8);

    // int4 word4;
    // COMPONENT_R(word4) = packFloatRightShift(pixel0.g, TWO_TO_THE_8) + packFloatLeftShift(pixel0.b, TWO_TO_THE_4, TWO_TO_THE_4);
    // COMPONENT_G(word4) = packFloatRightShift(pixel0.b, TWO_TO_THE_4);
    // COMPONENT_B(word4) = packFloatClamp(pixel1.r, TWO_TO_THE_8);
    // COMPONENT_A(word4) = packFloatRightShift(pixel1.r, TWO_TO_THE_8) + packFloatLeftShift(pixel1.g, TWO_TO_THE_4, TWO_TO_THE_4);

    // int4 word5;
    // COMPONENT_R(word5) = packFloatRightShift(pixel0.g, TWO_TO_THE_4);
    // COMPONENT_G(word5) = packFloatClamp(pixel0.b, TWO_TO_THE_8);
    // COMPONENT_B(word5) = packFloatRightShift(pixel0.b, TWO_TO_THE_8) + packFloatLeftShift(pixel1.r, TWO_TO_THE_4, TWO_TO_THE_4);
    // COMPONENT_A(word5) = packFloatRightShift(pixel1.r, TWO_TO_THE_4);

    // int4 word6;
    // COMPONENT_R(word6) = packFloatClamp(pixel0.g, TWO_TO_THE_8);
    // COMPONENT_G(word6) = packFloatRightShift(pixel0.g, TWO_TO_THE_8) + packFloatLeftShift(pixel0.b, TWO_TO_THE_4, TWO_TO_THE_4);
    // COMPONENT_B(word6) = packFloatRightShift(pixel0.b, TWO_TO_THE_4);
    // COMPONENT_A(word6) = packFloatClamp(pixel1.r, TWO_TO_THE_8);

    // int4 word7;
    // COMPONENT_R(word7) = packFloatRightShift(pixel0.r, TWO_TO_THE_8) + packFloatLeftShift(pixel0.g, TWO_TO_THE_4, TWO_TO_THE_4);
    // COMPONENT_G(word7) = packFloatRightShift(pixel0.g, TWO_TO_THE_4);
    // COMPONENT_B(word7) = packFloatClamp(pixel0.b, TWO_TO_THE_8);
    // COMPONENT_A(word7) = packFloatRightShift(pixel0.b, TWO_TO_THE_8) + packFloatLeftShift(pixel1.r, TWO_TO_THE_4, TWO_TO_THE_4);

    // int4 word8;
    // COMPONENT_R(word8) = packFloatRightShift(pixel0.r, TWO_TO_THE_4);
    // COMPONENT_G(word8) = packFloatClamp(pixel0.g, TWO_TO_THE_8);
    // COMPONENT_B(word8) = packFloatRightShift(pixel0.g, TWO_TO_THE_8) + packFloatLeftShift(pixel0.b, TWO_TO_THE_4, TWO_TO_THE_4);
    // COMPONENT_A(word8) = packFloatRightShift(pixel0.b, TWO_TO_THE_4);

    // const int4 words[9] =
    // {
    //     word0,
    //     word1,
    //     word2,
    //     word3,
    //     word4,
    //     word5,
    //     word6,
    //     word7,
    //     word8
    // };

    // int4 packed = words[index];
    // --------------------------

    // --- Vectorized version ---
    const float unpacked[6] =
    {
        pixel0.r,
        pixel0.g,
        pixel0.b,
        pixel1.r,
        pixel1.g,
        pixel1.b,
    };

    const int componentIndex[9][6] =
    {
        { 0, 0, 1, 1, 2, 0 },
        { 2, 3, 3, 4, 4, 5 },
        { 2, 3, 3, 4, 4, 0 },
        { 2, 2, 3, 3, 4, 0 },
        { 1, 2, 2, 3, 3, 4 },
        { 1, 2, 2, 3, 3, 0 },
        { 1, 1, 2, 2, 3, 0 },
        { 0, 1, 1, 2, 2, 3 },
        { 0, 1, 1, 2, 2, 0 },
    };

    const float c0 = unpacked[componentIndex[index][0]];
    const float c1 = unpacked[componentIndex[index][1]];
    const float c2 = unpacked[componentIndex[index][2]];
    const float c3 = unpacked[componentIndex[index][3]];
    const float c4 = unpacked[componentIndex[index][4]];
    const float c5 = unpacked[componentIndex[index][5]];

    float4 word0;
    COMPONENT_R(word0) = packFloatClamp(c0, TWO_TO_THE_8);
    COMPONENT_G(word0) = packFloatRightShift(c1, TWO_TO_THE_8) + packFloatLeftShift(c2, TWO_TO_THE_4, TWO_TO_THE_4);
    COMPONENT_B(word0) = packFloatRightShift(c3, TWO_TO_THE_4);
    COMPONENT_A(word0) = packFloatClamp(c4, TWO_TO_THE_8);

    float4 word1;
    COMPONENT_R(word1) = packFloatRightShift(c0, TWO_TO_THE_8) + packFloatLeftShift(c1, TWO_TO_THE_4, TWO_TO_THE_4);
    COMPONENT_G(word1) = packFloatRightShift(c2, TWO_TO_THE_4);
    COMPONENT_B(word1) = packFloatClamp(c3, TWO_TO_THE_8);
    COMPONENT_A(word1) = packFloatRightShift(c4, TWO_TO_THE_8) + packFloatLeftShift(c5, TWO_TO_THE_4, TWO_TO_THE_4);

    float4 word2;
    COMPONENT_R(word2) = packFloatRightShift(c0, TWO_TO_THE_4);
    COMPONENT_G(word2) = packFloatClamp(c1, TWO_TO_THE_8);
    COMPONENT_B(word2) = packFloatRightShift(c2, TWO_TO_THE_8) + packFloatLeftShift(c3, TWO_TO_THE_4, TWO_TO_THE_4);
    COMPONENT_A(word2) = packFloatRightShift(c4, TWO_TO_THE_4);

    const float4 possibilities[3] =
    {
        word0,
        word1,
        word2
    };

    const int indexMod3 = int(fmod(index, 3.0) + 0.5);
    const float4 packed = possibilities[indexMod3];
    // --------------------------

    return int8ToNormalizedFloat(packed);
}
