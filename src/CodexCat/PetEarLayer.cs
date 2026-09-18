using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexCat
{
    // Bend only the original ear pixels. Displacement fades to zero along each
    // slanted ear root, so the ear remains attached and the face never stretches.
    internal sealed class PetEarLayer
    {
        private readonly BitmapSource original;
        private readonly byte[] pixels;
        private readonly int width, height, stride;
        private readonly Dictionary<int, BitmapSource> frames = new Dictionary<int, BitmapSource>();

        public PetEarLayer(BitmapSource source)
        {
            original = source;
            BitmapSource rgba = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
            width = rgba.PixelWidth; height = rgba.PixelHeight; stride = width * 4;
            pixels = new byte[stride * height];
            rgba.CopyPixels(pixels, stride, 0);
            frames[0] = source;
        }

        public void Draw(DrawingContext dc, Rect destination, double angle)
        {
            int key = (int)Math.Round(Math.Max(-PetMotionProfile.EarIdleMaxAngle,
                Math.Min(PetMotionProfile.EarIdleMaxAngle, angle)) * 8);
            BitmapSource frame;
            if (!frames.TryGetValue(key, out frame))
            {
                frame = BuildFrame(key / 8.0);
                frames[key] = frame;
            }
            dc.DrawImage(frame, destination);
        }

        private static Vector Displacement(double x, double y, double angle)
        {
            bool left = x < 151;
            double rootY = left ? 63 - .70 * (x - 102) : 36 + .82 * (x - 169);
            double depth = Math.Max(0, rootY - y);
            double sideWeight = left ? AnimationTransitionProfile.Ease((140 - x) / 11.0)
                : AnimationTransitionProfile.Ease((x - 170) / 11.0);
            double weight = AnimationTransitionProfile.Ease(depth / 35.0) * sideWeight;
            double radians = angle * Math.PI / 180.0;
            double direction = left ? -1 : 1;
            double rootX = left ? 117 : 191;
            // Most of a real cat's casual ear flick is visible at the tip. Keep
            // vertical travel especially small so the triangular ear does not
            // look rubbery or pull away from the skull.
            return new Vector(direction * radians * depth * weight,
                direction * radians * (x - rootX) * weight * .28);
        }

        private BitmapSource BuildFrame(double angle)
        {
            byte[] result = (byte[])pixels.Clone();
            // Include transparent padding around the tips. Only this upper-head
            // region is resampled; eyes, muzzle, body, tail and paws are identical.
            int x0 = (int)(88 * width / 256.0), x1 = (int)(219 * width / 256.0);
            int y1 = (int)(67 * height / 256.0);
            for (int y = 0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                double tx = x * 256.0 / width, ty = y * 256.0 / height;
                double sx = tx, sy = ty;
                // Invert the smooth bend; three small corrections suffice for
                // the deliberately modest ear angles and avoid any vacant seam.
                for (int step = 0; step < 3; step++)
                {
                    Vector offset = Displacement(sx, sy, angle);
                    sx = tx - offset.X; sy = ty - offset.Y;
                }
                double sampleX = sx * width / 256.0, sampleY = sy * height / 256.0;
                int ix = (int)Math.Floor(sampleX), iy = (int)Math.Floor(sampleY);
                double fx = sampleX - ix, fy = sampleY - iy;
                int dest = y * stride + x * 4;
                for (int c = 0; c < 4; c++)
                {
                    double value = Sample(ix, iy, c) * (1 - fx) * (1 - fy)
                        + Sample(ix + 1, iy, c) * fx * (1 - fy)
                        + Sample(ix, iy + 1, c) * (1 - fx) * fy
                        + Sample(ix + 1, iy + 1, c) * fx * fy;
                    result[dest + c] = (byte)Math.Max(0, Math.Min(255, Math.Round(value)));
                }
            }
            BitmapSource frame = BitmapSource.Create(width, height, 96, 96,
                PixelFormats.Pbgra32, null, result, stride);
            frame.Freeze();
            return frame;
        }

        private byte Sample(int x, int y, int channel)
        {
            return x < 0 || y < 0 || x >= width || y >= height ? (byte)0 : pixels[y * stride + x * 4 + channel];
        }
    }
}
