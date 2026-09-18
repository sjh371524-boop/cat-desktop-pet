using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexCat
{
    // Split the existing authored pixels once. Animate only the tail behind the
    // untouched body, so the head, torso, paws and body proportions never warp.
    // The tail uses a pinned-root raster bend rather than a rigid rotation: the
    // socket at the hip stays still, the middle eases into the motion, and only
    // the distal tail receives the full angle. This prevents a transparent seam
    // opening between the tail root and the cat's rump at either swing extreme.
    internal sealed class PetTailLayer
    {
        private const double RootAnchorLength = 14.0;
        private const double RootBlendLength = 46.0;
        private const int AngleStepsPerDegree = 2;
        private const int MaximumCachedWarps = 24;

        private readonly BitmapSource original;
        private readonly BitmapSource body;
        private readonly BitmapSource tail;
        private readonly byte[] tailPixels;
        private readonly int pixelWidth;
        private readonly int pixelHeight;
        private readonly int stride;
        private readonly Point root;
        private readonly bool onRight;
        private readonly Dictionary<int, BitmapSource> warpedTailCache = new Dictionary<int, BitmapSource>();
        private readonly Queue<int> warpedTailOrder = new Queue<int>();

        public PetTailLayer(BitmapSource source, bool tailOnRight)
        {
            original = source;
            onRight = tailOnRight;
            root = tailOnRight ? new Point(165, 188) : new Point(95, 188);
            BitmapSource rgba = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
            pixelWidth = rgba.PixelWidth;
            pixelHeight = rgba.PixelHeight;
            stride = pixelWidth * 4;
            byte[] bodyPixels = new byte[stride * pixelHeight];
            tailPixels = new byte[bodyPixels.Length];
            rgba.CopyPixels(bodyPixels, stride, 0);
            for (int y = 0; y < pixelHeight; y++)
            {
                double sy = y * 256.0 / pixelHeight;
                double edge = tailOnRight ? 175.0 - Math.Max(0, sy - 150) * 0.25
                    : 84.0 + Math.Max(0, sy - 150) * 0.32;
                // Follow the actual transparent gap in this pose, not a fixed
                // vertical cut that can leave the inside half of a tail behind.
                int low = (int)((tailOnRight ? 148 : 68) * pixelWidth / 256.0);
                int high = (int)((tailOnRight ? 190 : 112) * pixelWidth / 256.0);
                int bestWidth = 0;
                for (int x = low; x < high; x++)
                {
                    if (bodyPixels[y * stride + x * 4 + 3] > 12) continue;
                    int start = x;
                    while (x < high && bodyPixels[y * stride + x * 4 + 3] <= 12) x++;
                    int end = x;
                    if (start == low || end == high || end - start <= bestWidth) continue;
                    bestWidth = end - start;
                    edge = (start + end) * 128.0 / pixelWidth;
                }
                for (int x = 0; x < pixelWidth; x++)
                {
                    double sx = x * 256.0 / pixelWidth;
                    bool inTail = sy >= (tailOnRight ? 90 : 78) && sy < 191 && (tailOnRight ? sx > edge : sx < edge);
                    if (!inTail) continue;
                    int index = y * stride + x * 4;
                    for (int c = 0; c < 4; c++) tailPixels[index + c] = bodyPixels[index + c];

                    // Keep a short copy of the original tail socket in the body
                    // layer. It is drawn last and acts as a stable overlap collar
                    // between the rump and the deforming tail, without changing
                    // the visible neutral pose or leaving a duplicate tail tip.
                    if (root.Y - sy > RootAnchorLength + 2.0)
                        for (int c = 0; c < 4; c++) bodyPixels[index + c] = 0;
                }
            }
            body = BitmapSource.Create(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32, null, bodyPixels, stride);
            tail = BitmapSource.Create(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32, null, tailPixels, stride);
            body.Freeze(); tail.Freeze();
        }

        public void Draw(DrawingContext dc, Rect destination, double angle)
        {
            if (Math.Abs(angle) < 0.001) { dc.DrawImage(original, destination); return; }
            dc.DrawImage(GetWarpedTail(angle), destination);
            dc.DrawImage(body, destination);
        }

        internal static double BendWeight(double canonicalY)
        {
            double distanceFromRoot = 188.0 - canonicalY;
            double amount = (distanceFromRoot - RootAnchorLength) / RootBlendLength;
            if (amount <= 0.0) return 0.0;
            if (amount >= 1.0) return 1.0;
            return amount * amount * (3.0 - 2.0 * amount);
        }

        private BitmapSource GetWarpedTail(double angle)
        {
            int key = (int)Math.Round(angle * AngleStepsPerDegree);
            if (key == 0) return tail;

            BitmapSource cached;
            if (warpedTailCache.TryGetValue(key, out cached)) return cached;

            cached = CreateWarpedTail(key / (double)AngleStepsPerDegree);
            if (warpedTailCache.Count >= MaximumCachedWarps)
            {
                int oldest = warpedTailOrder.Dequeue();
                warpedTailCache.Remove(oldest);
            }
            warpedTailCache[key] = cached;
            warpedTailOrder.Enqueue(key);
            return cached;
        }

        private BitmapSource CreateWarpedTail(double angle)
        {
            byte[] output = new byte[tailPixels.Length];
            double rootX = root.X * pixelWidth / 256.0;
            double rootY = root.Y * pixelHeight / 256.0;
            double direction = onRight ? -1.0 : 1.0;

            // Inverse-map one destination row at a time. The bend weight is zero
            // throughout the socket, so those pixels are sampled from precisely
            // their authored positions. Farther up the tail it grows smoothly to
            // one, avoiding both a hinge-like kink and a gap at the hip.
            for (int y = 0; y < pixelHeight; y++)
            {
                double canonicalY = (y + 0.5) * 256.0 / pixelHeight;
                double radians = direction * angle * BendWeight(canonicalY) * Math.PI / 180.0;
                double cosine = Math.Cos(radians);
                double sine = Math.Sin(radians);
                double destinationY = y + 0.5;
                for (int x = 0; x < pixelWidth; x++)
                {
                    double destinationX = x + 0.5;
                    double dx = destinationX - rootX;
                    double dy = destinationY - rootY;
                    double sourceX = rootX + cosine * dx + sine * dy - 0.5;
                    double sourceY = rootY - sine * dx + cosine * dy - 0.5;
                    int outputIndex = y * stride + x * 4;
                    SampleBilinear(sourceX, sourceY, output, outputIndex);
                }
            }

            BitmapSource bitmap = BitmapSource.Create(pixelWidth, pixelHeight, 96, 96,
                PixelFormats.Pbgra32, null, output, stride);
            bitmap.Freeze();
            return bitmap;
        }

        private void SampleBilinear(double sourceX, double sourceY, byte[] output, int outputIndex)
        {
            int left = (int)Math.Floor(sourceX);
            int top = (int)Math.Floor(sourceY);
            double horizontal = sourceX - left;
            double vertical = sourceY - top;
            double topLeftWeight = (1.0 - horizontal) * (1.0 - vertical);
            double topRightWeight = horizontal * (1.0 - vertical);
            double bottomLeftWeight = (1.0 - horizontal) * vertical;
            double bottomRightWeight = horizontal * vertical;

            for (int channel = 0; channel < 4; channel++)
            {
                double value = ReadChannel(left, top, channel) * topLeftWeight
                    + ReadChannel(left + 1, top, channel) * topRightWeight
                    + ReadChannel(left, top + 1, channel) * bottomLeftWeight
                    + ReadChannel(left + 1, top + 1, channel) * bottomRightWeight;
                output[outputIndex + channel] = (byte)Math.Max(0, Math.Min(255, Math.Round(value)));
            }
        }

        private byte ReadChannel(int x, int y, int channel)
        {
            if (x < 0 || y < 0 || x >= pixelWidth || y >= pixelHeight) return 0;
            return tailPixels[y * stride + x * 4 + channel];
        }
    }
}
