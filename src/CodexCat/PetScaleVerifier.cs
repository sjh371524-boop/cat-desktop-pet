using System;
using System.Windows;

namespace CodexCat
{
    internal static class PetScaleVerifier
    {
        public static void Run()
        {
            Rect fullHd = new Rect(0, 0, 1920, 1040);
            ScaleRange range = PetScalePolicy.GetRange(fullHd);
            AssertClose(PetScalePolicy.Step(1.0, true, fullHd), 1.20, "放大应增加当前大小的 20%");
            AssertClose(PetScalePolicy.Step(1.0, false, fullHd), 0.80, "缩小应减少当前大小的 20%");

            double atMaximum = PetScalePolicy.Step(range.Maximum, true, fullHd);
            double atMinimum = PetScalePolicy.Step(range.Minimum, false, fullHd);
            AssertClose(atMaximum, range.Maximum, "放大不能越过桌面动态上限");
            AssertClose(atMinimum, range.Minimum, "缩小不能越过桌面动态下限");

            Rect compact = new Rect(0, 0, 1024, 600);
            ScaleRange compactRange = PetScalePolicy.GetRange(compact);
            if (compactRange.Maximum >= range.Maximum)
            {
                throw new InvalidOperationException("较小桌面的缩放上限必须更小。");
            }
            if (compactRange.Minimum < 0.45 || compactRange.Maximum > 2.20)
            {
                throw new InvalidOperationException("动态缩放范围超出安全边界。");
            }
        }

        private static void AssertClose(double actual, double expected, string message)
        {
            if (Math.Abs(actual - expected) > 0.0001)
            {
                throw new InvalidOperationException(message + " 实际值=" + actual + "，期望值=" + expected);
            }
        }
    }
}
