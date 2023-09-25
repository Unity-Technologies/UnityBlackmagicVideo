using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.Media.Blackmagic
{
    sealed class PooledBufferAsyncGPUReadback : IDisposable
    {
        List<(long, AsyncGPUReadbackRequest, NativeArray<byte>)> asyncBuffers = new List<(long, AsyncGPUReadbackRequest, NativeArray<byte>)>();
        private Dictionary<long, bool> BMDIsUsingIt = new Dictionary<long, bool>();

        public AsyncGPUReadbackRequest RequestGPUReadBack(long frameCount, RenderTexture tex, GraphicsFormat format, Action<AsyncGPUReadbackRequest> cb)
        {
            var buff = new NativeArray<byte>();
            GetAsyncBuffer(tex.width, tex.height, format, ref buff); // Gets a buffer from the bufferpool
            var req = AsyncGPUReadback.RequestIntoNativeArray(ref buff, tex, 0, format, cb);
            RegisterAsyncBuffer(frameCount, req,
                ref buff); // Associates the buffer with an asyncRequest to make sure it is used only when free.

            BMDLock(frameCount);
            return req;
        }

        public AsyncGPUReadbackRequest RequestGPUReadBack(long frameCount, RenderTexture tex, Action<AsyncGPUReadbackRequest> cb = null)
        {
            return RequestGPUReadBack(frameCount, tex, tex.graphicsFormat, cb);
        }

        void BMDLock(long frameCount)
        {
            BMDIsUsingIt[frameCount] = true;
        }

        public void BMDRelease(long frameCount)
        {
            if (BMDIsUsingIt.ContainsKey(frameCount))
            {
                BMDIsUsingIt.Remove(frameCount);
            }
        }

        void GetAsyncBuffer(int width, int height, GraphicsFormat format, ref NativeArray<byte> buff)
        {
            NativeArray<byte> ret = default;
            var sz = (int)UnityEngine.Experimental.Rendering.GraphicsFormatUtility.ComputeMipmapSize(width,
                height,
                format); // Might not be able to use it.

            int idx;
            var found = false;
            for (idx = 0; idx < asyncBuffers.Count; ++idx)
            {
                long frameCount = asyncBuffers[idx].Item1;
                if (asyncBuffers[idx].Item2.done && asyncBuffers[idx].Item3.Length == sz &&
                    (!BMDIsUsingIt.ContainsKey(frameCount) || !BMDIsUsingIt[frameCount]))
                {
                    ret = asyncBuffers[idx].Item3;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                ret = new NativeArray<byte>(sz, Allocator.Persistent);
                asyncBuffers.Add((0, default,
                    ret));     // Register the buffer with a dummy request
            }

            buff = ret;
        }

        void RegisterAsyncBuffer(long frameCount, AsyncGPUReadbackRequest r, ref NativeArray<byte> buff)
        {
            for (var idx = 0; idx < asyncBuffers.Count; ++idx)
            {
                if (asyncBuffers[idx].Item3 == buff)
                {
                    asyncBuffers[idx] = (frameCount, r, buff);
                    return;
                }
            }

            throw new InvalidOperationException("The buffer is not registered to the Recorder buffer pool");
        }

        public void Dispose()
        {
            foreach (var buffer in asyncBuffers)
            {
                buffer.Item2.WaitForCompletion();
                buffer.Item3.Dispose();
            }
            asyncBuffers.Clear();
            BMDIsUsingIt.Clear();
        }
    }
}
