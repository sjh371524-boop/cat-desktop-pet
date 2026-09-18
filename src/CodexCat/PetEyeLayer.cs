using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexCat
{
    // Runtime texture deformation, not a second pupil painted over the old one.
    // Sample only the original eye interiors; the eyelids, fur, nose, alpha and
    // body are immutable. No new art is written back to the character assets.
    internal sealed class PetEyeLayer
    {
        private readonly BitmapSource original;
        private readonly byte[] source;
        private readonly byte[] pixels;
        private readonly WriteableBitmap output;
        private readonly int width;
        private readonly int height;
        private readonly int stride;
        private Vector previous = new Vector(double.NaN, double.NaN);

        public PetEyeLayer(BitmapSource frame)
        {
            original = frame;
            BitmapSource rgba = frame.Format == PixelFormats.Pbgra32 ? frame :
                new FormatConvertedBitmap(frame, PixelFormats.Pbgra32, null, 0);
            width = rgba.PixelWidth;
            height = rgba.PixelHeight;
            stride = width * 4;
            source = new byte[stride * height];
            rgba.CopyPixels(source, stride, 0);
            pixels = (byte[])source.Clone();
            output = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
        }

        internal BitmapSource Frame(Vector gaze)
        {
            gaze = PetGazeProfile.Limit(gaze);
            // Preserve the original image exactly for neutral endpoints and
            // existing action-to-action continuity checks.
            if (gaze.LengthSquared < 0.00000001) return original;
            if ((gaze - previous).LengthSquared < 0.00000001) return output;
            Buffer.BlockCopy(source, 0, pixels, 0, source.Length);
            WarpEye(133.6, 76.8, 8.1, 8.6, gaze);
            WarpEye(172.9, 76.8, 7.5, 8.4, gaze);
            output.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
            previous = gaze;
            return output;
        }

        private void WarpEye(double centerX, double centerY, double radiusX, double radiusY, Vector gaze)
        {
            double scaleX = width / 256.0, scaleY = height / 256.0;
            int left = Math.Max(0, (int)Math.Floor((centerX - radiusX) * scaleX));
            int right = Math.Min(width - 1, (int)Math.Ceiling((centerX + radiusX) * scaleX));
            int top = Math.Max(0, (int)Math.Floor((centerY - radiusY) * scaleY));
            int bottom = Math.Min(height - 1, (int)Math.Ceiling((centerY + radiusY) * scaleY));
            for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++)
            {
                double nx = (x / scaleX - centerX) / radiusX;
                double ny = (y / scaleY - centerY) / radiusY;
                double radial = nx * nx + ny * ny;
                if (radial >= 1) continue;
                // Pin the eye socket while leaving enough iris travel to make
                // vertical glances readable, not just moving the catchlights.
                // Travel stays below half the radius to prevent texture folds.
                double influence = 1 - radial;
                double sx = x - gaze.X * PetGazeProfile.HorizontalTravel * influence * scaleX;
                double sy = y - gaze.Y * PetGazeProfile.VerticalTravel * influence * scaleY;
                Sample(sx, sy, (y * width + x) * 4);
            }
        }

        private void Sample(double x, double y, int destination)
        {
            x = Math.Max(0, Math.Min(width - 1.001, x));
            y = Math.Max(0, Math.Min(height - 1.001, y));
            int ix = (int)x, iy = (int)y;
            double fx = x - ix, fy = y - iy;
            int a = iy * stride + ix * 4, b = a + 4, c = a + stride, d = c + 4;
            // Subpixel sampling gives a smooth gaze without replacing the
            // heterochromic pigment or introducing an outline around the eyes.
            for (int channel = 0; channel < 3; channel++)
            {
                double upper = source[a + channel] * (1 - fx) + source[b + channel] * fx;
                double lower = source[c + channel] * (1 - fx) + source[d + channel] * fx;
                pixels[destination + channel] = (byte)Math.Round(upper * (1 - fy) + lower * fy);
            }
            // Alpha remains the exact original value, including near the lids.
        }
    }
}
