using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexCat
{
    internal static class GazeVerifier
    {
        public static void Run(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            VerifyMappingAndSmoothing();
            byte[] neutralRender = Pixels(Render(NewVisual(PetState.Stand, 0, 0)));
            string[] names = { "up-left", "up", "up-right", "left", "center", "right", "down-left", "down", "down-right" };
            int index = 0;
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                CatVisual visual = NewVisual(PetState.Stand, 0, 0);
                visual.UpdateGaze(PetGazeProfile.EyeCenter(0) + new Vector(x * 1400, y * 1400), 10, true);
                RenderTargetBitmap rendered = Render(visual);
                VerifyRenderedPixelScope(neutralRender, Pixels(rendered));
                Save(rendered, Path.Combine(outputDirectory, "17-gaze-" + names[index++] + ".png"));
            }
            VerifyEyeTexture();
            VerifyStateExpressions();
            VerifyDesktopMapping();
        }

        private static void VerifyMappingAndSmoothing()
        {
            foreach (double phase in new double[] { 0, .25, .73 })
            foreach (Size size in new Size[] { new Size(300, 300), new Size(150, 150), new Size(620, 580), new Size(213, 371) })
            {
                double scale = Math.Min(size.Width / 300, size.Height / 300);
                Point center = PetGazeProfile.EyeCenter(phase);
                Point local = new Point((size.Width - 300 * scale) / 2 + center.X * scale,
                    (size.Height - 300 * scale) / 2 + center.Y * scale);
                Require(PetGazeProfile.Target(local, size, phase).Length == 0, "Eye contact must stay centered after scaling/letterboxing.");
                foreach (Vector direction in new Vector[] { new Vector(-600, 0), new Vector(600, 0), new Vector(0, -600), new Vector(0, 600), new Vector(-600, -600) })
                {
                    Vector baseline = PetGazeProfile.Target(center + direction, new Size(300, 300), phase);
                    Vector resized = PetGazeProfile.Target(local + direction * scale, size, phase);
                    Require((baseline - resized).Length < 0.000001, "Gaze direction must be independent of pet size and local DIP scale.");
                    Require(Vector.Multiply(baseline, direction) > 0 && baseline.Length <= 1, "Cursor direction/range invalid.");
                }
            }
            Require(PetGazeProfile.Target(new Point(double.NaN, 0), new Size(300, 300), 0).Length == 0, "Invalid cursor data must center the gaze.");
            Require(PetGazeProfile.Target(new Point(0, 0), new Size(0, 0), 0).Length == 0, "Unarranged visuals must not divide by zero.");
            foreach (PetState state in Enum.GetValues(typeof(PetState)))
            {
                Require(PetGazeProfile.CanTrack(state, true) == (state == PetState.Stand), "Special actions own their eye expressions: " + state);
                Require(!PetGazeProfile.CanTrack(state, false), "Screen off must disable gaze acquisition.");
            }
            Vector a = new Vector(), b = new Vector();
            for (int i = 0; i < 30; i++) a = PetGazeProfile.Smooth(a, new Vector(1, 0), 1.0 / 30);
            for (int i = 0; i < 120; i++) b = PetGazeProfile.Smooth(b, new Vector(1, 0), 1.0 / 120);
            Require((a - b).Length < 0.00001, "Smoothing must be independent of frame rate.");
            Vector first = PetGazeProfile.Smooth(new Vector(-1, 0), new Vector(1, 0), .016);
            Require(first.X > -1 && first.X < 0, "A mouse jump must not snap the pupils to the other side.");
            for (int i = 0; i < 100; i++)
            {
                Vector next = PetGazeProfile.Smooth(first, new Vector(1, 0), .016);
                Require(next.X >= first.X && next.X <= 1, "Smoothing must not overshoot or oscillate.");
                first = next;
            }
            Require(PetGazeProfile.Limit(new Vector(100, -200)).Length <= 1.000001, "Diagonal travel must be bounded.");
        }

        private static void VerifyRenderedPixelScope(byte[] neutral, byte[] tracked)
        {
            // Check the WPF result as well as the source texture: converting a
            // bitmap format must not subtly change the fur or transparent edges.
            Rect left = new Rect(145, 75, 23, 24), right = new Rect(189, 75, 23, 24);
            for (int y = 0; y < 300; y++)
            for (int x = 0; x < 300; x++)
            {
                int i = (y * 300 + x) * 4;
                Require(neutral[i + 3] == tracked[i + 3], "Rendered gaze must preserve silhouette alpha.");
                if (left.Contains(new Point(x, y)) || right.Contains(new Point(x, y))) continue;
                for (int channel = 0; channel < 3; channel++)
                    Require(neutral[i + channel] == tracked[i + channel], "Rendered gaze must not change any pixels outside the eyes.");
            }
        }

        private static void VerifyEyeTexture()
        {
            BitmapImage original = new BitmapImage();
            original.BeginInit();
            original.CacheOption = BitmapCacheOption.OnLoad;
            original.UriSource = new Uri(Path.Combine(AppPaths.CharacterDirectory, "stand", "000.png"));
            original.EndInit();
            original.Freeze();
            PetEyeLayer layer = new PetEyeLayer(original);
            byte[] neutral = Pixels(original);
            Require(object.ReferenceEquals(original, layer.Frame(new Vector())), "Centered gaze must reuse the unchanged original sprite.");
            foreach (Vector direction in new Vector[] { new Vector(-1, 0), new Vector(1, 0), new Vector(0, -1), new Vector(0, 1), new Vector(-1, -1), new Vector(1, 1) })
            {
                byte[] tracked = Pixels(layer.Frame(direction));
                int changes = 0;
                for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    int i = (y * 256 + x) * 4;
                    bool changed = neutral[i] != tracked[i] || neutral[i + 1] != tracked[i + 1] || neutral[i + 2] != tracked[i + 2];
                    Require(neutral[i + 3] == tracked[i + 3], "Gaze must never modify silhouette/alpha.");
                    bool inEye = InsideEye(x, y, 133.6, 76.8, 8.1, 8.6) || InsideEye(x, y, 172.9, 76.8, 7.5, 8.4);
                    Require(!changed || inEye, "Gaze may only change the insides of the two eyes.");
                    if (changed) changes++;
                }
                Require(changes > 80, "Gaze must visibly move both pupils.");
                foreach (bool right in new bool[] { false, true })
                {
                    Vector shift = EyeFeatureShift(neutral, tracked, right);
                    if (direction.X != 0) Require(shift.X * direction.X > .12, "Horizontal eye texture motion must follow the cursor.");
                    if (direction.Y != 0) Require(shift.Y * direction.Y > .12, "Vertical eye texture motion must follow the cursor: " + direction + "; right=" + right + "; shift=" + shift);
                }
            }
        }

        private static bool InsideEye(double x, double y, double cx, double cy, double rx, double ry)
        {
            return Math.Pow((x - cx) / rx, 2) + Math.Pow((y - cy) / ry, 2) < 1;
        }

        private static Vector EyeFeatureShift(byte[] original, byte[] moved, bool right)
        {
            // Match a textured pupil patch including its catchlight. A black-
            // pixel centroid is invalid here: the upper dark lid connects to the
            // pupil, and the large white reflection is not a hole in the pupil.
            double cx = right ? 169.5 : 131.0, cy = 73.5;
            double bestError = double.MaxValue;
            Vector best = new Vector();
            for (double sy = -4; sy <= 4; sy += .25)
            for (double sx = -4; sx <= 4; sx += .25)
            {
                double error = 0;
                for (int y = -2; y <= 2; y++)
                for (int x = -2; x <= 2; x++)
                for (int channel = 0; channel < 3; channel++)
                {
                    double difference = SampleChannel(original, cx + x, cy + y, channel)
                        - SampleChannel(moved, cx + x + sx, cy + y + sy, channel);
                    error += difference * difference;
                }
                if (error < bestError) { bestError = error; best = new Vector(sx, sy); }
            }
            return best;
        }

        private static double SampleChannel(byte[] pixels, double x, double y, int channel)
        {
            int ix = (int)x, iy = (int)y;
            double fx = x - ix, fy = y - iy;
            int i = (iy * 256 + ix) * 4 + channel;
            return (pixels[i] * (1 - fx) + pixels[i + 4] * fx) * (1 - fy)
                + (pixels[i + 1024] * (1 - fx) + pixels[i + 1028] * fx) * fy;
        }

        private static void VerifyStateExpressions()
        {
            foreach (PetState state in Enum.GetValues(typeof(PetState)))
            {
                if (state == PetState.Stand) continue;
                CatVisual visual = NewVisual(state, .25, .5);
                byte[] before = Pixels(Render(visual));
                visual.UpdateGaze(new Point(-1800, 1700), 10, true);
                Require(visual.Gaze.Length == 0, "A special pose must not acquire a gaze offset.");
                RequireSame(before, Pixels(Render(visual)), "Tracking must not overwrite the expression in " + state);
            }
            CatVisual blink = NewVisual(PetState.Stand, .9375, 0);
            byte[] closed = Pixels(Render(blink));
            blink.UpdateGaze(new Point(-1800, 1700), 10, true);
            RequireSame(closed, Pixels(Render(blink)), "Closed eyes must not display a pupil overlay.");

            CatVisual resume = NewVisual(PetState.Stand, 0, 0);
            resume.UpdateGaze(new Point(1800, 80), 10, true);
            Require(resume.Gaze.X > .9, "Standing gaze should acquire a distant cursor.");
            resume.Update(PetState.Petting, 0, .5, new Vector(1, 0));
            Require(resume.Gaze.Length == 0, "Entering a special action must reset active eye control.");
            resume.Update(PetState.Stand, 0, 0, new Vector());
            resume.UpdateGaze(new Point(1800, 80), .016, true);
            Require(resume.Gaze.X > 0 && resume.Gaze.X < .2, "Returning to stand must reacquire the mouse smoothly.");
        }

        private static CatVisual NewVisual(PetState state, double phase, double progress)
        {
            CatVisual visual = new CatVisual { Width = 300, Height = 300, SuppressStateTransitions = true };
            visual.Update(state, phase, progress, new Vector(1, 0));
            visual.Measure(new Size(300, 300));
            visual.Arrange(new Rect(0, 0, 300, 300));
            visual.UpdateLayout();
            return visual;
        }

        private static void VerifyDesktopMapping()
        {
            CatVisual visual = new CatVisual { SuppressStateTransitions = true };
            Window host = new Window { Width = 370, Height = 340, Left = -10000, Top = -10000,
                Opacity = 0, ShowActivated = false, ShowInTaskbar = false,
                IsHitTestVisible = false, Content = visual };
            try
            {
                host.Show();
                host.UpdateLayout();
                Point screen;
                Require(DesktopCursor.TryGetPosition(out screen), "The Windows desktop cursor could not be read.");
                Point local = visual.PointFromScreen(screen);
                Require((visual.PointToScreen(local) - screen).Length < 1, "Screen pixels and visual DIPs must round-trip with the real window transform.");
                Vector expected = PetGazeProfile.Target(local, new Size(visual.ActualWidth, visual.ActualHeight), 0);
                visual.UpdateGaze(local, 10, true);
                Require((visual.Gaze - expected).Length < .00001, "The live desktop cursor must reach the same gaze target as the coordinate policy.");
            }
            finally { host.Close(); }
        }

        private static RenderTargetBitmap Render(CatVisual visual)
        {
            visual.UpdateLayout();
            RenderTargetBitmap bitmap = new RenderTargetBitmap(300, 300, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            return bitmap;
        }

        private static byte[] Pixels(BitmapSource bitmap)
        {
            if (bitmap.Format != PixelFormats.Pbgra32) bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Pbgra32, null, 0);
            byte[] result = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(result, bitmap.PixelWidth * 4, 0);
            return result;
        }

        private static void RequireSame(byte[] first, byte[] second, string message)
        {
            Require(first.Length == second.Length, message);
            for (int i = 0; i < first.Length; i++) Require(first[i] == second[i], message);
        }

        private static void Save(BitmapSource bitmap, string path)
        {
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
