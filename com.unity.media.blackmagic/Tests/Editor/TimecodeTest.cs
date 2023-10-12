using NUnit.Framework;

namespace Unity.Media.Blackmagic.Tests
{
    static class TimecodeTest
    {
        [TestCase(1, 60)] // 60 Hz
        [TestCase(1001, 60000)] // 59.94 Hz
        public static void BcdConversion(int mul, int div)
        {
            var frameDuration = BlackmagicUtilities.k_FlicksPerSecond * mul / div;
            for (long i = 0; i < 2 * 60 * 60 * 60; i += 13)
            {
                var t1 = i * frameDuration;
                var bcd = new Timecode(frameDuration, t1).ToBCD();
                var t2 = Timecode.FromBCD(frameDuration, bcd).Value.Flicks;
                Assert.AreEqual(t1, t2, "Frame = {0}, BCD = {1:X}", i, bcd);
            }
        }
    }
}
