using System;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.Media.Blackmagic
{
    class ThreadedMemcpy : IDisposable
    {
        unsafe class CopyThread : IDisposable
        {
            Thread m_Thread;
            CancellationToken m_CancellationToken;
            SemaphoreSlim m_StartSemaphore;
            SemaphoreSlim m_StopSemaphore;
            void* m_Dst;
            void* m_Src;
            long m_Count;

            public CopyThread(CancellationToken cancellationToken, string threadName)
            {
                m_CancellationToken = cancellationToken;
                m_StartSemaphore = new SemaphoreSlim(0, 1);
                m_StopSemaphore = new SemaphoreSlim(0, 1);

                m_Thread = new Thread(CopyLoop)
                {
                    Name = threadName,
                    IsBackground = true,
                };
                m_Thread.Start();
            }

            public void Dispose()
            {
                if (m_Thread != null)
                {
                    m_Thread.Join();
                    m_Thread = null;
                }
                if (m_StartSemaphore != null)
                {
                    m_StartSemaphore.Dispose();
                    m_StartSemaphore = null;
                }
                if (m_StopSemaphore != null)
                {
                    m_StopSemaphore.Dispose();
                    m_StopSemaphore = null;
                }
            }

            public void BeginCopy(void* dst, void* src, long count)
            {
                m_Dst = dst;
                m_Src = src;
                m_Count = count;

                m_StartSemaphore.Release();
            }

            public void EndCopy()
            {
                try
                {
                    m_StopSemaphore.Wait(m_CancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
            }

            void CopyLoop()
            {
                Profiler.BeginThreadProfiling("Blackmagic Memcpy Threads", m_Thread.Name);

                try
                {
                    while (!m_CancellationToken.IsCancellationRequested)
                    {
                        m_StartSemaphore.Wait(m_CancellationToken);

                        if (!m_CancellationToken.IsCancellationRequested)
                        {
                            Profiler.BeginSample("MemCpy");

                            UnsafeUtility.MemCpy(m_Dst, m_Src, m_Count);

                            Profiler.EndSample();

                            m_StopSemaphore.Release();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    Profiler.EndThreadProfiling();
                }
            }
        }

        CancellationTokenSource m_CancellationTokenSource;
        CopyThread[] m_Threads;

        public ThreadedMemcpy(string theadName) : this(theadName, Environment.ProcessorCount)
        {
        }

        public ThreadedMemcpy(string theadName, int threadCount)
        {
            m_CancellationTokenSource = new CancellationTokenSource();
            m_Threads = new CopyThread[threadCount];

            for (var i = 0; i < m_Threads.Length; i++)
            {
                m_Threads[i] = new CopyThread(m_CancellationTokenSource.Token, $"{theadName} {i}");
            }
        }

        public void Dispose()
        {
            if (m_CancellationTokenSource != null)
            {
                m_CancellationTokenSource.Cancel();
                m_CancellationTokenSource = null;
            }
            if (m_Threads != null)
            {
                for (var i = 0; i < m_Threads.Length; i++)
                {
                    m_Threads[i].Dispose();
                }
                m_Threads = null;
            }
        }

        public unsafe void MemCpy(void* dst, void* src, long count)
        {
            if (dst == null)
                throw new ArgumentNullException(nameof(dst));
            if (src == null)
                throw new ArgumentNullException(nameof(src));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Cannot be negative.");
            if (m_Threads == null)
                throw new ObjectDisposedException(nameof(ThreadedMemcpy));

            try
            {
                Profiler.BeginSample("MemCpy");

                // use a single thread for small copies, using threads takes longer due to scheduling latency
                if (count <= 1024 * 1024)
                {
                    UnsafeUtility.MemCpy(dst, src, count);
                    return;
                }

                // Cieled integer division is used so the blocks are slightly larger than needed when
                // the number of bytes to copy is not divisible by the number of threads.
                var blockSize = (count + m_Threads.Length - 1) / m_Threads.Length;

                for (var i = 0; i < m_Threads.Length; i++)
                {
                    var blockStart = i * blockSize;
                    var blockEnd = Math.Min(blockStart + blockSize, count);

                    m_Threads[i].BeginCopy(
                        (byte*)dst + blockStart,
                        (byte*)src + blockStart,
                        blockEnd - blockStart
                    );
                }

                // wait for all the threads to complete the copy operation
                for (var i = 0; i < m_Threads.Length; i++)
                {
                    m_Threads[i].EndCopy();
                }
            }
            finally
            {
                Profiler.EndSample();
            }
        }
    }
}
