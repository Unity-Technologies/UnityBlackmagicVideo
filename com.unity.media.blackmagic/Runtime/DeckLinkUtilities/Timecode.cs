using System;

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// A struct that represents a timecode.
    /// </summary>
    public readonly struct Timecode : IComparable, IComparable<Timecode>, IEquatable<Timecode>
    {
        /// <summary>
        /// The timecode in flicks.
        /// </summary>
        /// <remarks>
        /// A flick is defined as 1/705,600,000 of a second.
        /// </remarks>
        public long Flicks { get; }

        /// <summary>
        /// The number of elapsed hours.
        /// </summary>
        public int Hour { get; }

        /// <summary>
        /// The number of elapsed minutes in the current hour.
        /// </summary>
        public int Minute { get; }

        /// <summary>
        /// The number of elapsed seconds in the current minute.
        /// </summary>
        public int Second { get; }

        /// <summary>
        /// The number of elapsed frames in the current second.
        /// </summary>
        public int Frame { get; }

        /// <summary>
        /// Is the timecode a drop frame timecode.
        /// </summary>
        public bool IsDropFrame { get; }

        /// <summary>
        /// The duration of the frame in flicks.
        /// </summary>
        public long FrameDuration { get; }

        /// <summary>
        /// Creates a new <see cref="Timecode"/> instance.
        /// </summary>
        /// <param name="frameDuration">The duration of the frame in flicks.</param>
        /// <param name="hour">The number of elapsed hours.</param>
        /// <param name="minute">The number of elapsed minutes in the current hour.</param>
        /// <param name="second">The number of elapsed seconds in the current minute.</param>
        /// <param name="frame">The number of elapsed frames in the current second.</param>
        /// <param name="isDropFrame">Is the specified time a valid drop frame timecode.</param>
        public Timecode(long frameDuration, int hour, int minute, int second, int frame, bool isDropFrame)
        {
            // frames per second (ceiled)
            var fps = (BlackmagicUtilities.k_FlicksPerSecond + frameDuration - 1) / frameDuration;
            var fpm = fps * 60;
            var fph = fpm * 60;

            Flicks = (fph * hour + fpm * minute + fps * second + frame) * frameDuration;
            Hour = hour;
            Minute = minute;
            Second = second;
            Frame = frame;
            IsDropFrame = isDropFrame;
            FrameDuration = frameDuration;
        }

        /// <summary>
        /// Creates a new <see cref="Timecode"/> instance.
        /// </summary>
        /// <param name="frameDuration">The duration of the frame in flicks.</param>
        /// <param name="flicks">The time in flicks.</param>
        /// <param name="isDropFrame">Is the specified time a valid drop frame timecode.</param>
        public Timecode(long frameDuration, long flicks, bool isDropFrame = false)
        {
            // frames per second (ceiled)
            var fps = (BlackmagicUtilities.k_FlicksPerSecond + frameDuration - 1) / frameDuration;
            var fpm = fps * 60;
            var fph = fpm * 60;

            // total time in frames
            var frames = flicks / frameDuration;

            var hours = frames / fph;
            frames -= hours * fph;

            var minutes = frames / fpm;
            frames -= minutes * fpm;

            var seconds = frames / fps;
            frames -= seconds * fps;

            // 24 hours wrapping around
            hours %= 24;

            Flicks = flicks;
            Hour = (int)hours;
            Minute = (int)minutes;
            Second = (int)seconds;
            Frame = (int)frames;
            IsDropFrame = isDropFrame;
            FrameDuration = frameDuration;
        }

        internal static Timecode? FromBCD(long frameDuration, uint bcdTimecode)
        {
            if (BlackmagicUtilities.UnpackBcdTimecode(
                bcdTimecode,
                frameDuration,
                out var hour,
                out var minute,
                out var second,
                out var frame,
                out var isDropFrame
            ))
            {
                return new Timecode(frameDuration, hour, minute, second, frame, isDropFrame);
            }

            return null;
        }

        internal uint ToBCD()
        {
            return BlackmagicUtilities.PackBcdTimecode(FrameDuration, Hour, Minute, Second, Frame, IsDropFrame);
        }

        /// <summary>
        /// Compares this instance to a specified <see cref="Timecode"/> and returns an indication of their relative values.
        /// </summary>
        /// <param name="other">The value to compare with this instance.</param>
        /// <returns>A signed number indicating the relative values of this instance and <paramref name="other"/>.<br/>
        /// * Returns a negative value when this instance is less than <paramref name="other"/>.<br/>
        /// * Returns zero when this instance is the same as <paramref name="other"/>.<br/>
        /// * Returns a positive value when this instance is greater than <paramref name="other"/>.<br/>
        /// </returns>
        public int CompareTo(Timecode other)
        {
            return Flicks.CompareTo(other.Flicks);
        }

        /// <summary>
        /// Compares this instance to a specified object and returns an indication of their relative values.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns>A signed number indicating the relative values of this instance and <paramref name="obj"/>.<br/>
        /// * Returns a negative value when <paramref name="obj"/> is not a valid <see cref="Timecode"/> instance or this instance is less than <paramref name="obj"/>.<br/>
        /// * Returns zero when this instance is the same as <paramref name="obj"/>.<br/>
        /// * Returns a positive value when this instance is greater than <paramref name="obj"/>.<br/>
        /// </returns>
        public int CompareTo(object obj)
        {
            return obj is Timecode other ? CompareTo(other) : -1;
        }

        /// <summary>
        /// Returns a value indicating whether this instance is equal to a specified <see cref="Timecode"/>.
        /// </summary>
        /// <param name="other">A value to compare with this instance.</param>
        /// <returns><see langword="true"/> if <paramref name="other"/> has the same value as this instance; otherwise, <see langword="false"/>.</returns>
        public bool Equals(Timecode other)
        {
            return Flicks == other.Flicks && IsDropFrame == other.IsDropFrame;
        }

        /// <summary>
        /// Returns a value indicating whether this instance is equal to a specified object.
        /// </summary>
        /// <param name="obj">An object to compare with this instance.</param>
        /// <returns><see langword="true"/> if <paramref name="obj"/> is an instance of <see cref="Timecode"/> and equals
        /// the value of this instance; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object obj)
        {
            return obj is Timecode other && Equals(other);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>The hash code for this instance.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Flicks.GetHashCode();
                hashCode = (hashCode * 397) ^ IsDropFrame.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Returns the timecode represented as a string.
        /// </summary>
        /// <returns>The timecode represented as a string.</returns>
        public override string ToString()
        {
            return $"{Hour:D2}:{Minute:D2}:{Second:D2}{(IsDropFrame ? ";" : ":")}{Frame:D2}";
        }

        /// <summary>
        /// Determines whether two specified instances of <see cref="Timecode"/> are equal.
        /// </summary>
        /// <param name="a">The first instance to compare.</param>
        /// <param name="b">The second instance to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="a"/> and <paramref name="b"/> represent the same value; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(Timecode a, Timecode b) => a.Equals(b);

        /// <summary>
        /// Determines whether two specified instances of <see cref="Timecode"/> are not equal.
        /// </summary>
        /// <param name="a">The first instance to compare.</param>
        /// <param name="b">The second instance to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="a"/> and <paramref name="b"/> do not represent the same value; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(Timecode a, Timecode b) => !a.Equals(b);

        /// <summary>
        /// Determines whether one specified <see cref="Timecode"/> is later than or the same as another specified <see cref="Timecode"/>.
        /// </summary>
        /// <param name="a">The first instance to compare.</param>
        /// <param name="b">The second instance to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="a"/> is later than or the same as <paramref name="b"/>; otherwise, <see langword="false"/>.</returns>
        public static bool operator >=(Timecode a, Timecode b) => a.CompareTo(b) >= 0;

        /// <summary>
        /// Determines whether one specified <see cref="Timecode"/> is earlier than or the same as another specified <see cref="Timecode"/>.
        /// </summary>
        /// <param name="a">The first instance to compare.</param>
        /// <param name="b">The second instance to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="a"/> is earlier than or the same as <paramref name="b"/>; otherwise, <see langword="false"/>.</returns>
        public static bool operator <=(Timecode a, Timecode b) => a.CompareTo(b) <= 0;

        /// <summary>
        /// Determines whether one specified <see cref="Timecode"/> is later than another specified <see cref="Timecode"/>.
        /// </summary>
        /// <param name="a">The first instance to compare.</param>
        /// <param name="b">The second instance to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="a"/> is later than <paramref name="b"/>; otherwise, <see langword="false"/>.</returns>
        public static bool operator >(Timecode a, Timecode b) => a.CompareTo(b) > 0;

        /// <summary>
        /// Determines whether one specified <see cref="Timecode"/> is earlier than another specified <see cref="Timecode"/>.
        /// </summary>
        /// <param name="a">The first instance to compare.</param>
        /// <param name="b">The second instance to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="a"/> is earlier than <paramref name="b"/>; otherwise, <see langword="false"/>.</returns>
        public static bool operator <(Timecode a, Timecode b) => a.CompareTo(b) < 0;
    }
}
