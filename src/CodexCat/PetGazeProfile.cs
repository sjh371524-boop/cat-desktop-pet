using System;
using System.Windows;

namespace CodexCat
{
    internal static class PetGazeProfile
    {
        public const double ResponseSeconds = 0.09;
        public const double HorizontalTravel = 3.6; // source sprite pixels
        public const double VerticalTravel = 3.2;

        public static bool CanTrack(PetState state, bool screenOn)
        {
            return screenOn && state == PetState.Stand;
        }

        public static Point EyeCenter(double phase)
        {
            double breathing = Math.Sin(phase * Math.PI * 2.0) * 1.4;
            return new Point(8 + 153.25 * 284 / 256.0,
                2 - breathing + 76.8 * (284 + breathing) / 256.0);
        }

        // PointFromScreen supplies local DIPs. Undo the exact letterboxing used
        // by CatVisual.OnRender so eye contact survives scaling and monitor DPI.
        public static Vector Target(Point localCursor, Size visualSize, double phase)
        {
            if (!Finite(localCursor.X) || !Finite(localCursor.Y)
                || !Finite(visualSize.Width) || !Finite(visualSize.Height)
                || visualSize.Width <= 0 || visualSize.Height <= 0) return new Vector();
            double scale = Math.Min(visualSize.Width / 300.0, visualSize.Height / 300.0);
            Point logical = new Point(
                (localCursor.X - (visualSize.Width - 300 * scale) / 2) / scale,
                (localCursor.Y - (visualSize.Height - 300 * scale) / 2) / scale);
            Vector direction = logical - EyeCenter(phase);
            double distance = direction.Length;
            if (!Finite(distance) || distance <= 5) return new Vector();
            double reach = distance - 5;
            double strength = reach / Math.Sqrt(reach * reach + 180 * 180);
            return direction * (strength / distance);
        }

        public static Vector Smooth(Vector current, Vector target, double deltaSeconds)
        {
            current = Limit(current);
            target = Limit(target);
            if (!Finite(deltaSeconds) || deltaSeconds <= 0) return current;
            Vector result = current + (target - current) * (1 - Math.Exp(-deltaSeconds / ResponseSeconds));
            return (result - target).LengthSquared < 0.0000000001 ? target : result;
        }

        public static Vector Limit(Vector direction)
        {
            if (!Finite(direction.X) || !Finite(direction.Y)) return new Vector();
            double length = direction.Length;
            if (!Finite(length)) return new Vector();
            return length > 1 ? direction / length : direction;
        }

        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
}
