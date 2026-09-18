param(
    [string]$InputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'assets\character\calculator_greeting\calculator-greeting-sheet-v1.png'),
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'assets\character\calculator_greeting\left')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $InputPath)) {
    throw "Calculator greeting sheet was not found: $InputPath"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$processorSource = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class CalculatorGreetingSheetProcessor
{
    private static bool IsCheckerBackground(byte blue, byte green, byte red)
    {
        int minimum = Math.Min(red, Math.Min(green, blue));
        int maximum = Math.Max(red, Math.Max(green, blue));
        return minimum >= 222 && maximum - minimum <= 12;
    }

    private static void RemoveConnectedCheckerboard(Bitmap bitmap)
    {
        Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int stride = Math.Abs(data.Stride);
        byte[] pixels = new byte[stride * bitmap.Height];
        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

        int width = bitmap.Width;
        int height = bitmap.Height;
        bool[] background = new bool[width * height];
        Queue<int> queue = new Queue<int>();

        Action<int, int> enqueue = (x, y) =>
        {
            int index = y * width + x;
            if (background[index]) return;
            int pixel = y * stride + x * 4;
            if (!IsCheckerBackground(pixels[pixel], pixels[pixel + 1], pixels[pixel + 2])) return;
            background[index] = true;
            queue.Enqueue(index);
        };

        for (int x = 0; x < width; x++)
        {
            enqueue(x, 0);
            enqueue(x, height - 1);
        }
        for (int y = 1; y < height - 1; y++)
        {
            enqueue(0, y);
            enqueue(width - 1, y);
        }

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % width;
            int y = index / width;
            if (x > 0) enqueue(x - 1, y);
            if (x + 1 < width) enqueue(x + 1, y);
            if (y > 0) enqueue(x, y - 1);
            if (y + 1 < height) enqueue(x, y + 1);
        }

        for (int index = 0; index < background.Length; index++)
        {
            if (!background[index]) continue;
            int x = index % width;
            int y = index / width;
            pixels[y * stride + x * 4 + 3] = 0;
        }

        // Remove a single connected checkerboard fringe left by antialiasing.
        bool[] fringe = new bool[background.Length];
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int index = y * width + x;
                if (background[index]) continue;
                int pixel = y * stride + x * 4;
                if (!IsCheckerBackground(pixels[pixel], pixels[pixel + 1], pixels[pixel + 2])) continue;
                if (background[index - 1] || background[index + 1] || background[index - width] || background[index + width])
                {
                    fringe[index] = true;
                }
            }
        }
        for (int index = 0; index < fringe.Length; index++)
        {
            if (!fringe[index]) continue;
            int x = index % width;
            int y = index / width;
            pixels[y * stride + x * 4 + 3] = 0;
        }

        Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        bitmap.UnlockBits(data);
    }

    public static void Process(string inputPath, string outputDirectory)
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
                        RemoveConnectedCheckerboard(cell);

                        using (Bitmap frame = new Bitmap(256, 256, PixelFormat.Format32bppArgb))
                        using (Graphics graphics = Graphics.FromImage(frame))
                        {
                            graphics.Clear(Color.Transparent);
                            graphics.CompositingMode = CompositingMode.SourceCopy;
                            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                            graphics.PixelOffsetMode = PixelOffsetMode.Half;
                            graphics.DrawImage(cell, new Rectangle(0, 0, 256, 256));
                            int frameIndex = row * 4 + column;
                            frame.Save(Path.Combine(outputDirectory, frameIndex.ToString("000") + ".png"), ImageFormat.Png);
                        }
                    }
                }
            }
        }
    }
}
'@

Add-Type -TypeDefinition $processorSource -ReferencedAssemblies 'System.Drawing'
[CalculatorGreetingSheetProcessor]::Process(
    [System.IO.Path]::GetFullPath($InputPath),
    [System.IO.Path]::GetFullPath($OutputDirectory))

Write-Host "Prepared transparent sprite frames: $OutputDirectory" -ForegroundColor Green
