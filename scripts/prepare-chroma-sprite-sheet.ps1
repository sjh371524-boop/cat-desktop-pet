param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [switch]$KeepLargestComponent
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing.Common

if (-not (Test-Path -LiteralPath $InputPath)) {
    throw "Chroma sprite sheet was not found: $InputPath"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$processorSource = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class ChromaSpriteSheetProcessor
{
    private static bool IsChromaGreen(byte blue, byte green, byte red)
    {
        return green >= 150 && green >= red * 1.32 && green >= blue * 1.32;
    }

    private static void RemoveGreen(Bitmap bitmap)
    {
        Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int stride = Math.Abs(data.Stride);
        byte[] pixels = new byte[stride * bitmap.Height];
        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                int pixel = y * stride + x * 4;
                byte blue = pixels[pixel];
                byte green = pixels[pixel + 1];
                byte red = pixels[pixel + 2];
                if (IsChromaGreen(blue, green, red))
                {
                    pixels[pixel] = 0;
                    pixels[pixel + 1] = 0;
                    pixels[pixel + 2] = 0;
                    pixels[pixel + 3] = 0;
                }
            }
        }

        Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        bitmap.UnlockBits(data);
    }

    private static void RemoveGreenFringe(Bitmap bitmap)
    {
        Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int stride = Math.Abs(data.Stride);
        byte[] pixels = new byte[stride * bitmap.Height];
        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

        for (int pass = 0; pass < 3; pass++)
        {
            bool[] remove = new bool[bitmap.Width * bitmap.Height];
            for (int y = 1; y < bitmap.Height - 1; y++)
            {
                for (int x = 1; x < bitmap.Width - 1; x++)
                {
                    int pixel = y * stride + x * 4;
                    if (pixels[pixel + 3] == 0) continue;
                    byte blue = pixels[pixel];
                    byte green = pixels[pixel + 1];
                    byte red = pixels[pixel + 2];
                    if (green < 92 || green < red * 1.10 || green < blue * 1.10) continue;
                    bool touchesTransparent =
                        pixels[pixel - 4 + 3] == 0 || pixels[pixel + 4 + 3] == 0 ||
                        pixels[pixel - stride + 3] == 0 || pixels[pixel + stride + 3] == 0;
                    if (touchesTransparent) remove[y * bitmap.Width + x] = true;
                }
            }
            for (int index = 0; index < remove.Length; index++)
            {
                if (!remove[index]) continue;
                int x = index % bitmap.Width;
                int y = index / bitmap.Width;
                pixels[y * stride + x * 4 + 3] = 0;
            }
        }

        Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        bitmap.UnlockBits(data);
    }

    private static void KeepLargestOpaqueComponent(Bitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        bool[] visited = new bool[width * height];
        int[] queue = new int[width * height];
        bool[] keep = new bool[width * height];
        int largestCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int start = y * width + x;
                if (visited[start] || bitmap.GetPixel(x, y).A == 0) continue;
                int head = 0;
                int tail = 0;
                queue[tail++] = start;
                visited[start] = true;
                while (head < tail)
                {
                    int current = queue[head++];
                    int currentX = current % width;
                    int currentY = current / width;
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        for (int offsetX = -1; offsetX <= 1; offsetX++)
                        {
                            if (offsetX == 0 && offsetY == 0) continue;
                            int nextX = currentX + offsetX;
                            int nextY = currentY + offsetY;
                            if (nextX < 0 || nextX >= width || nextY < 0 || nextY >= height) continue;
                            int next = nextY * width + nextX;
                            if (visited[next] || bitmap.GetPixel(nextX, nextY).A == 0) continue;
                            visited[next] = true;
                            queue[tail++] = next;
                        }
                    }
                }
                if (tail > largestCount)
                {
                    Array.Clear(keep, 0, keep.Length);
                    for (int index = 0; index < tail; index++) keep[queue[index]] = true;
                    largestCount = tail;
                }
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!keep[y * width + x]) bitmap.SetPixel(x, y, Color.Transparent);
            }
        }
    }

    private static int CountUnsafeBorderPixels(Bitmap bitmap)
    {
        int unsafePixels = 0;
        for (int x = 0; x < bitmap.Width; x++)
        {
            if (bitmap.GetPixel(x, 0).A > 0) unsafePixels++;
            if (bitmap.GetPixel(x, bitmap.Height - 1).A > 0) unsafePixels++;
        }
        for (int y = 1; y < bitmap.Height - 1; y++)
        {
            if (bitmap.GetPixel(0, y).A > 0) unsafePixels++;
            if (bitmap.GetPixel(bitmap.Width - 1, y).A > 0) unsafePixels++;
        }
        return unsafePixels;
    }

    public static void Process(string inputPath, string outputDirectory, bool keepLargestComponent)
    {
        Directory.CreateDirectory(outputDirectory);
        using (Bitmap source = new Bitmap(inputPath))
        {
            for (int row = 0; row < 2; row++)
            {
                int top = (int)Math.Round(row * source.Height / 2.0);
                int bottom = (int)Math.Round((row + 1) * source.Height / 2.0);
                for (int column = 0; column < 4; column++)
                {
                    int left = (int)Math.Round(column * source.Width / 4.0);
                    int right = (int)Math.Round((column + 1) * source.Width / 4.0);
                    using (Bitmap cell = new Bitmap(right - left, bottom - top, PixelFormat.Format32bppArgb))
                    {
                        using (Graphics graphics = Graphics.FromImage(cell))
                        {
                            graphics.Clear(Color.Transparent);
                            graphics.DrawImage(source,
                                new Rectangle(0, 0, cell.Width, cell.Height),
                                new Rectangle(left, top, cell.Width, cell.Height),
                                GraphicsUnit.Pixel);
                        }
                        RemoveGreen(cell);
                        RemoveGreenFringe(cell);
                        if (keepLargestComponent) KeepLargestOpaqueComponent(cell);

                        using (Bitmap frame = new Bitmap(256, 256, PixelFormat.Format32bppArgb))
                        using (Graphics graphics = Graphics.FromImage(frame))
                        {
                            graphics.Clear(Color.Transparent);
                            graphics.CompositingMode = CompositingMode.SourceCopy;
                            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                            graphics.PixelOffsetMode = PixelOffsetMode.Half;
                            // Keep a deterministic six-pixel transparent safety
                            // gutter around every frame. This prevents even a
                            // one-pixel wrist, tail or antialias fringe from being
                            // mistaken for content from the neighbouring cell.
                            graphics.DrawImage(cell, new Rectangle(6, 6, 244, 244));
                            int frameIndex = row * 4 + column;
                            int unsafePixels = CountUnsafeBorderPixels(frame);
                            if (unsafePixels > 0)
                            {
                                throw new InvalidDataException(
                                    "Frame " + frameIndex + " touches its cell border (" + unsafePixels + " pixels). " +
                                    "This would risk adjacent-frame bleed.");
                            }
                            frame.Save(Path.Combine(outputDirectory, frameIndex.ToString("000") + ".png"), ImageFormat.Png);
                        }
                    }
                }
            }
        }
    }
}
'@

$drawingAssemblies = @(
    [System.Drawing.Bitmap].Assembly.Location,
    [System.Drawing.Rectangle].Assembly.Location,
    (Join-Path (Split-Path ([System.Drawing.Bitmap].Assembly.Location) -Parent) 'System.Private.Windows.GdiPlus.dll'),
    (Join-Path (Split-Path ([System.Drawing.Bitmap].Assembly.Location) -Parent) 'System.Private.Windows.Core.dll')
)
Add-Type -TypeDefinition $processorSource -ReferencedAssemblies $drawingAssemblies
[ChromaSpriteSheetProcessor]::Process(
    [System.IO.Path]::GetFullPath($InputPath),
    [System.IO.Path]::GetFullPath($OutputDirectory),
    [bool]$KeepLargestComponent)

Write-Host "Prepared transparent chroma-key sprite frames: $OutputDirectory" -ForegroundColor Green
