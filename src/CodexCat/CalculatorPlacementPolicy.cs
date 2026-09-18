using System;
using System.Windows;

namespace CodexCat
{
    internal static class CalculatorPlacementPolicy
    {
        public static Rect GetBounds(Rect bubbleBounds, double scale, Rect workArea)
        {
            double safeScale = Math.Max(0.45, Math.Min(2.20, scale));
            double gap = Math.Max(6.0, 8.0 * safeScale);
            double margin = 4.0;
            double width = Math.Min(bubbleBounds.Width, Math.Max(1.0, workArea.Width - margin * 2.0));
            double height = Math.Min(bubbleBounds.Height, Math.Max(1.0, workArea.Height - margin * 2.0));

            double roomBelow = workArea.Bottom - bubbleBounds.Bottom - gap - margin;
            double roomAbove = bubbleBounds.Top - workArea.Top - gap - margin;
            bool placeBelow = roomBelow >= height || (roomAbove < height && roomBelow >= roomAbove);

            double left = bubbleBounds.Left + (bubbleBounds.Width - width) / 2.0;
            double top = placeBelow
                ? bubbleBounds.Bottom + gap
                : bubbleBounds.Top - gap - height;

            left = Clamp(left, workArea.Left + margin, workArea.Right - width - margin);
            top = Clamp(top, workArea.Top + margin, workArea.Bottom - height - margin);
            return new Rect(left, top, width, height);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (maximum < minimum) return minimum;
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    internal static class CalculatorPlacementVerifier
    {
        public static void Run()
        {
            Rect workArea = new Rect(0, 0, 1920, 1080);
            Rect middleBubble = new Rect(120, 180, 400, 330);
            Rect below = CalculatorPlacementPolicy.GetBounds(middleBubble, 1.0, workArea);
            if (below.Top <= middleBubble.Bottom)
            {
                throw new InvalidOperationException("Calculator should prefer the space below the bubble.");
            }

            Rect bottomLeftBubble = new Rect(4, 746, 400, 330);
            Rect aboveLeft = CalculatorPlacementPolicy.GetBounds(bottomLeftBubble, 1.0, workArea);
            if (aboveLeft.Bottom >= bottomLeftBubble.Top || aboveLeft.Left < workArea.Left)
            {
                throw new InvalidOperationException("Bottom-left placement should move the calculator above the bubble.");
            }

            Rect bottomRightBubble = new Rect(1516, 746, 400, 330);
            Rect aboveRight = CalculatorPlacementPolicy.GetBounds(bottomRightBubble, 1.0, workArea);
            if (aboveRight.Bottom >= bottomRightBubble.Top || aboveRight.Right > workArea.Right)
            {
                throw new InvalidOperationException("Bottom-right placement should move the calculator above the bubble.");
            }

            Rect topRightBubble = new Rect(1516, 4, 400, 330);
            Rect belowRight = CalculatorPlacementPolicy.GetBounds(topRightBubble, 1.0, workArea);
            if (belowRight.Top <= topRightBubble.Bottom || belowRight.Right > workArea.Right)
            {
                throw new InvalidOperationException("Top-right placement should keep the calculator below and on-screen.");
            }
        }
    }
}
