using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace CodexCat
{
    internal static class DesktopCursor
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int X; public int Y; }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out NativePoint point);

        public static bool TryGetPosition(out Point screenPixels)
        {
            NativePoint point;
            bool available = GetCursorPos(out point);
            screenPixels = available ? new Point(point.X, point.Y) : new Point();
            return available;
        }
    }
}
