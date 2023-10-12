using System;
using UnityEngine;

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// A simple circular software queue class.
    /// </summary>
    public class SimpleRingBuffer
    {
        /// <summary>
        /// The capacity of the buffer.
        /// </summary>
        public int Capacity = 0;

        /// <summary>
        /// The number of elements currently written to the buffer.
        /// </summary>
        public int FillCount => (int)(m_WriteCount - m_ReadCount);

        /// <summary>
        /// The available space in the buffer.
        /// </summary>
        public int FreeCount => Capacity - FillCount;

        /// <summary>
        /// Determines where to start reading the data.
        /// </summary>
        public int ReadOffset => (int)(m_ReadCount % (ulong)Capacity);

        /// <summary>
        /// Determines where to start writing the data.
        /// </summary>
        public int WriteOffset => (int)(m_WriteCount % (ulong)Capacity);

        /// <summary>
        /// Determines if the buffer is ready to be read.
        /// </summary>
        public bool IsReady = false;

        ulong m_ReadCount = 0;
        ulong m_WriteCount = 0;

        float[] m_Buffer;

        /// <summary>
        /// A simple constructor that allocates memory to the buffer.
        /// </summary>
        /// <param name="capacity"></param>
        public SimpleRingBuffer(ulong capacity)
        {
            Capacity = (int)capacity;
            m_Buffer = new float[capacity];
        }

        /// <summary>
        /// Writes data to the circular buffer.
        /// </summary>
        /// <param name="source">The data to write in the buffer.</param>
        /// <param name="count">The number of samples to write in the buffer.</param>
        /// <returns>The number of samples written in the buffer.</returns>
        public int Write(float[] source, int count)
        {
            if (count > FreeCount)
            {
                Debug.LogWarning("Buffer overflow!");
                return 0;
            }

            if (WriteOffset + count >= Capacity)
            {
                int offset = Capacity - WriteOffset;

                Array.Copy(source, 0, m_Buffer, WriteOffset, offset);
                Array.Copy(source, offset, m_Buffer, 0, count - offset);
            }
            else
            {
                Array.Copy(source, 0, m_Buffer, WriteOffset, count);
            }

            m_WriteCount += (ulong)count;

            if (FillCount > 0)
                IsReady = true;

            return count;
        }

        /// <summary>
        /// Reads data from the circular buffer.
        /// </summary>
        /// <param name="sink">An array of floats comprising the audio data.</param>
        /// <param name="count">The length of the array of floats.</param>
        /// <returns>The number of elements that have been read from the buffer.</returns>
        public int Read(ref float[] sink, int count)
        {
            if (count > FillCount)
            {
                Debug.LogWarning("Buffer underflow!");
                return 0;
            }

            if (ReadOffset + count >= Capacity)
            {
                int offset = Capacity - ReadOffset;

                Array.Copy(m_Buffer, ReadOffset, sink, 0, offset);
                Array.Copy(m_Buffer, 0, sink, offset, count - offset);
            }
            else
            {
                Array.Copy(m_Buffer, ReadOffset, sink, 0, count);
            }

            m_ReadCount += (ulong)count;

            if (m_ReadCount > m_WriteCount)
            {
                Debug.LogError("Read index incremented past write index!");
            }

            return count;
        }

        /// <summary>
        /// Reads data from the circular buffer without performing any copy.
        /// </summary>
        /// <param name="count">The length of the data to mark as read.</param>
        /// <returns>The number of elements that have been read from the buffer.</returns>
        public int FreeSpace(int count)
        {
            m_ReadCount += (ulong)count;
            return count;
        }
    }
}
