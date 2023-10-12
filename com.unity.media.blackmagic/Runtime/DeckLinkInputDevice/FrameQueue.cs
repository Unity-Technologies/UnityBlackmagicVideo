using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// A class that represents a circular buffer of video frames.
    /// </summary>
    class FrameQueue : IDisposable, IEnumerable<BufferedFrame>
    {
        readonly Func<BufferedFrame> m_AllocateFrame;
        BufferedFrame[] m_Buffer;
        int m_Start;
        int m_End;
        int m_Count;

        /// <summary>
        /// The capacity of the queue.
        /// </summary>
        public int Capacity => m_Buffer.Length;

        /// <summary>
        /// The number of frames in the queue.
        /// </summary>
        public int Count => m_Count;

        /// <summary>
        /// Creates a new <see cref="FrameQueue"/> instance.
        /// </summary>
        /// <param name="capacity">The initial capacity of the queue.</param>
        /// <param name="allocateFrame">The function invoked to allocate a frame.</param>
        public FrameQueue(int capacity, Func<BufferedFrame> allocateFrame)
        {
            m_AllocateFrame = allocateFrame ?? throw new ArgumentNullException(nameof(allocateFrame));

            m_Count = 0;
            m_Start = 0;
            m_End = 0;

            SetCapacity(capacity);
        }

        /// <summary>
        /// Disposes the queue and any frames it allocated.
        /// </summary>
        public void Dispose()
        {
            for (var i = 0; i < Capacity; i++)
            {
                m_Buffer[i].Dispose();
            }
        }

        /// <summary>
        /// Gets a frame from the queue.
        /// </summary>
        /// <param name="index">The index of the frame to get.</param>
        public BufferedFrame this[int index]
        {
            get
            {
                ThrowIfEmpty();
                ThrowIfOutOfRange(index);
                return m_Buffer[InternalIndex(index)];
            }
        }

        /// <summary>
        /// Sets the <see cref="Capacity"/> of the queue.
        /// </summary>
        /// <remarks>If the new size is smaller than the current <see cref="Count"/>, elements will be truncated from the front.</remarks>
        /// <param name="capacity">The desired capacity of the queue.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the capacity is not greater than zero.</exception>
        public void SetCapacity(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Must be greater than zero.");
            }

            if (m_Buffer != null && capacity == Capacity)
            {
                return;
            }

            var newBuffer = new BufferedFrame[capacity];

            // Copy existing frames to the new array, and dispose excess frames if the capacity is less.
            if (m_Buffer != null)
            {
                for (var i = 0; i < m_Buffer.Length; i++)
                {
                    // copy starting from the back to preserve the newest values
                    var item = m_Buffer[InternalIndex((m_Buffer.Length - 1) - i)];

                    if (i < newBuffer.Length)
                    {
                        newBuffer[i] = item;
                    }
                    else
                    {
                        item.Dispose();
                    }
                }
            }

            // Allocate new frames if the new capacity is larger
            for (var i = m_Buffer?.Length ?? 0; i < newBuffer.Length; i++)
            {
                newBuffer[i] = m_AllocateFrame();
            }

            m_Buffer = newBuffer;
            m_Count = Math.Min(m_Count, capacity);
            m_Start = 0;
            m_End = m_Count % capacity;
        }

        /// <summary>
        /// Removes all frames from the queue.
        /// </summary>
        public void Clear()
        {
            m_Start = 0;
            m_End = 0;
            m_Count = 0;
        }

        /// <summary>
        /// Gets the frame at the front of the queue.
        /// </summary>
        /// <returns>The frame at the front of the queue.</returns>
        public BufferedFrame Front()
        {
            ThrowIfEmpty();
            return m_Buffer[m_Start];
        }

        /// <summary>
        /// Gets the frame at the back of the queue.
        /// </summary>
        /// <returns>The frame at the back of the queue.</returns>
        public BufferedFrame Back()
        {
            ThrowIfEmpty();
            return m_Buffer[Decrement(m_End)];
        }

        /// <summary>
        /// Adds a new frame to the queue.
        /// </summary>
        /// <param name="item">Returns the frame to populate.</param>
        /// <returns><see langword="true"/> if the frame is overwriting a previously existing frame.</returns>
        public bool Enqueue(out BufferedFrame item)
        {
            item = m_Buffer[m_End];
            m_End = Increment(m_End);

            if (m_Count < Capacity)
            {
                m_Count++;
                return false;
            }

            m_Start = m_End;
            return true;
        }

        public IEnumerator<BufferedFrame> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return m_Buffer[InternalIndex(i)];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        void ThrowIfEmpty()
        {
            if (Count == 0)
            {
                throw new InvalidOperationException("Cannot access an empty buffer.");
            }
        }

        void ThrowIfOutOfRange(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new IndexOutOfRangeException();
            }
        }

        int Increment(int index)
        {
            return (index + 1) % Capacity;
        }

        int Decrement(int index)
        {
            return (Capacity + index - 1) % Capacity;
        }

        int InternalIndex(int index)
        {
            return (m_Start + index) % Capacity;
        }
    }
}
