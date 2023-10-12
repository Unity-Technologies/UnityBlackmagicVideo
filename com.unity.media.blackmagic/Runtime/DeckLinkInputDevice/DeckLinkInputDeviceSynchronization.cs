#if LIVE_CAPTURE_4_0_0_OR_NEWER
using System;
using Unity.LiveCapture;
using UnityEngine;

namespace Unity.Media.Blackmagic
{
    using Object = UnityEngine.Object;
    using LCTimecode = Unity.LiveCapture.Timecode;
    using LCFrameRate = Unity.LiveCapture.FrameRate;

    partial class DeckLinkInputDevice : ITimecodeSource, ITimedDataSource
    {
        [SerializeField, HideInInspector]
        FrameTime m_PresentationOffset;
        [SerializeField, HideInInspector]
        bool m_IsSynchronized;

        TimecodeSourceState m_TimecodeSourceState;

        /// <inheritdoc />
        string IRegistrable.Id => $"Blackmagic Input {name}";

        /// <inheritdoc />
        string IRegistrable.FriendlyName => $"Blackmagic Input {name}";

        /// <inheritdoc />
        FrameTimeWithRate? ITimecodeSource.CurrentTime => m_TimecodeSourceState?.CurrentTime;

        /// <inheritdoc />
        LCFrameRate ITimecodeSource.FrameRate => GetFrameRate();

        /// <inheritdoc />
        LCFrameRate ITimedDataSource.FrameRate => GetFrameRate();

        /// <inheritdoc/>
        ISynchronizer ITimedDataSource.Synchronizer { get; set; }

        /// <inheritdoc/>
        Object ITimedDataSource.UndoTarget => this;

        /// <inheritdoc />
        int ITimedDataSource.BufferSize
        {
            get => QueueLength;
            set => QueueLength = value;
        }

        /// <inheritdoc />
        int? ITimedDataSource.MinBufferSize => k_MinQueueLength;

        /// <inheritdoc />
        int? ITimedDataSource.MaxBufferSize => k_MaxQueueLength;

        /// <inheritdoc />
        FrameTime ITimedDataSource.Offset
        {
            get => m_PresentationOffset;
            set => m_PresentationOffset = value;
        }

        /// <inheritdoc />
        bool ITimedDataSource.IsSynchronized
        {
            get => m_IsSynchronized;
            set => m_IsSynchronized = value;
        }

        /// <inheritdoc />
        bool ITimedDataSource.TryGetBufferRange(out FrameTime oldestSample, out FrameTime newestSample)
        {
            using (_ = m_Plugin.LockQueue())
            {
                if (IsActive && m_Queue != null)
                {
                    var frameRate = GetFrameRate();
                    oldestSample = BlackmagicUtilities.FlicksToFrameTime(m_Queue.Front().timecode, frameRate).Time + m_PresentationOffset;
                    newestSample = BlackmagicUtilities.FlicksToFrameTime(m_Queue.Back().timecode, frameRate).Time + m_PresentationOffset;
                    return true;
                }
            }

            oldestSample = default;
            newestSample = default;
            return false;
        }

        /// <inheritdoc />
        TimedSampleStatus ITimedDataSource.PresentAt(FrameTimeWithRate presentTime)
        {
            if (!IsActive || !m_IsSynchronized)
                return TimedSampleStatus.DataMissing;

            var frameRate = GetFrameRate();
            var requestedFrameTime = presentTime.Remap(frameRate);
            var presentationTime = requestedFrameTime - new FrameTimeWithRate(frameRate, m_PresentationOffset);
            var presentationFlicks = BlackmagicUtilities.FrameTimeToFlicks(presentationTime);

            TimedSampleStatus status;

            using (_ = m_Plugin.LockQueue())
            {
                if (m_Queue == null)
                    return TimedSampleStatus.DataMissing;

                status = TryGetSample(
                    m_Queue,
                    presentationFlicks,
                    m_Format.Value.frameDuration,
                    m_Format.Value.fieldDominance,
                    out var sample,
                    out var timeInFrame
                );

                if (status != TimedSampleStatus.DataMissing)
                {
                    PresentFrame(sample, timeInFrame);
                }
            }

            if (status != TimedSampleStatus.DataMissing)
            {
                UnpackTexture();
            }

            return status;
        }

        FrameTimeWithRate? PollTimecode()
        {
            var frameRate = GetFrameRate();

            if (!frameRate.IsValid)
            {
                return null;
            }

            var timecode = default(Timecode ? );

            using (_ = m_Plugin.LockQueue())
            {
                if (m_Queue != null && m_Queue.Count > 0)
                {
                    timecode = m_Queue.Back().timecode;
                }
            }

            if (timecode == null)
            {
                return null;
            }

            return BlackmagicUtilities.FlicksToFrameTime(timecode.Value, frameRate);
        }

        internal LCFrameRate GetFrameRate()
        {
            if (!IsActive || m_Format == null)
            {
                return default;
            }

            var numerator = m_Format.Value.frameRateNumerator;
            var denominator = m_Format.Value.frameRateDenominator;

            // when interlaced, use the field rate instead of the frame rate
            switch (m_Format.Value.fieldDominance)
            {
                case BMDFieldDominance.LowerFieldFirst :
                case BMDFieldDominance.UpperFieldFirst:
                    numerator *= 2;
                    break;
            }

            return new LCFrameRate(numerator, denominator, m_IsDropFrame);
        }

        static TimedSampleStatus TryGetSample(
            FrameQueue queue,
            Timecode time,
            long frameDuration,
            BMDFieldDominance fieldDominance,
            out BufferedFrame sample,
            out long timeInFrame
        )
        {
            sample = default;
            timeInFrame = default;

            if (queue.Count <= 0)
            {
                return TimedSampleStatus.DataMissing;
            }

            // We want to round to the nearest frame to the specified time. This can by done
            // by offsetting the time by half a frame. However, interlaced frames contain two fields
            // and we need to change the midpoint of the rounding to the middle of the two fields, which
            // is at the first quarter of the frame.
            long offset;
            switch (fieldDominance)
            {
                case BMDFieldDominance.LowerFieldFirst:
                case BMDFieldDominance.UpperFieldFirst:
                    offset = frameDuration / 4;
                    break;
                default:
                    offset = frameDuration / 2;
                    break;
            }

            var timeWithOffset = time.Flicks + offset;

            // Find the frame that overlaps with the specified time.
            for (var i = 0; i < queue.Count; i++)
            {
                sample = queue[i];
                timeInFrame = timeWithOffset - sample.timecode.Flicks;

                if (timeInFrame < 0)
                {
                    timeInFrame = 0;
                    return TimedSampleStatus.Ahead;
                }
                if (timeInFrame < sample.frameDuration)
                {
                    return TimedSampleStatus.Ok;
                }
            }

            timeInFrame = frameDuration - 1;
            return TimedSampleStatus.Behind;
        }
    }
}
#endif
