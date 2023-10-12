using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Media.Blackmagic
{
    [BurstCompile]
    static class AudioUtilities
    {
        public static unsafe void ConvertToFloats(NativeSlice<float> dst, NativeSlice<short> src, int count)
        {
            Convert((float*)dst.GetUnsafePtr(), (short*)src.GetUnsafeReadOnlyPtr(), count);
        }

        public static unsafe void ConvertToFloats(NativeSlice<float> dst, NativeSlice<int> src, int count)
        {
            Convert((float*)dst.GetUnsafePtr(), (int*)src.GetUnsafeReadOnlyPtr(), count);
        }

        [BurstCompile]
        static unsafe void Convert([NoAlias] float* dst, [NoAlias] short* src, int count)
        {
            for (var i = 0; i < count; i++)
            {
                dst[i] = (float)src[i] / short.MaxValue;
            }
        }

        [BurstCompile]
        static unsafe void Convert([NoAlias] float* dst, [NoAlias] int* src, int count)
        {
            for (var i = 0; i < count; i++)
            {
                dst[i] = (float)src[i] / int.MaxValue;
            }
        }
    }
}
