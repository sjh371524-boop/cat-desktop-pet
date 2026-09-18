using System;
using System.Windows;

namespace CodexCat
{
    internal static class PetScalePolicy
    {
        public const double BaseWidth = 320.0;
        public const double BaseHeight = 345.0;

        public static ScaleRange GetRange(Rect workArea)
        {
            double desktopRatio = Math.Min(workArea.Width / 1920.0, workArea.Height / 1080.0);
            double minimum = Clamp(0.55 * desktopRatio, 0.45, 0.70);
            double maximumByWidth = workArea.Width * 0.38 / BaseWidth;
            double maximumByHeight = workArea.Height * 0.62 / BaseHeight;
            double maximum = Clamp(Math.Min(maximumByWidth, maximumByHeight), 0.80, 2.20);

            if (maximum < minimum)
            {
                maximum = minimum;
            }

            return new ScaleRange(minimum, maximum);
        }

        public static double Step(double currentScale, bool enlarge, Rect workArea)
        {
            ScaleRange range = GetRange(workArea);
            double requested = currentScale * (enlarge ? 1.20 : 0.80);
            return Clamp(requested, range.Minimum, range.Maximum);
        }

        public static double ClampToDesktop(double scale, Rect workArea)
        {
            ScaleRange range = GetRange(workArea);
            return Clamp(scale, range.Minimum, range.Maximum);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    internal struct ScaleRange
    {
        private readonly double minimum;
        private readonly double maximum;

        public ScaleRange(double minimum, double maximum)
        {
            this.minimum = minimum;
            this.maximum = maximum;
        }

        public double Minimum { get { return minimum; } }
        public double Maximum { get { return maximum; } }
    }
}
