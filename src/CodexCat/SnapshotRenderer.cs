using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CodexCat
{
    internal static class SnapshotRenderer
    {
        private sealed class SnapshotCase
        {
            public string Name;
            public PetState State;
            public double Phase;
            public double Progress;
            public Vector Movement;
            public bool? ForceDragBlink;
        }

        public static void RenderAll(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            List<SnapshotCase> cases = new List<SnapshotCase>
            {
                new SnapshotCase { Name = "01-stand", State = PetState.Stand, Phase = 0.18, Progress = 0, Movement = new Vector() },
                new SnapshotCase { Name = "02-drag", State = PetState.Dragging, Phase = 0.28, Progress = 1, Movement = new Vector(18, -3) },
                new SnapshotCase { Name = "02b-drag-left-blink", State = PetState.Dragging, Phase = 0.72, Progress = 1, Movement = new Vector(-18, 2), ForceDragBlink = true },
                new SnapshotCase { Name = "02ba-drag-left-blue-eye", State = PetState.Dragging, Phase = 0.28, Progress = 1, Movement = new Vector(-18, 0), ForceDragBlink = false },
                new SnapshotCase { Name = "02bb-drag-right-original-eye", State = PetState.Dragging, Phase = 0.28, Progress = 1, Movement = new Vector(18, 0), ForceDragBlink = false },
                new SnapshotCase { Name = "02c-idle1-paw-right", State = PetState.IdleAnimation1, Phase = 0.46, Progress = 0.58, Movement = new Vector(1, 0) },
                new SnapshotCase { Name = "02d-idle1-paw-left", State = PetState.IdleAnimation1, Phase = 0.46, Progress = 0.58, Movement = new Vector(-1, 0) },
                new SnapshotCase { Name = "02e-turn-right", State = PetState.Dragging, Phase = 0.12, Progress = 0.52, Movement = new Vector(18, 0) },
                new SnapshotCase { Name = "02f-turn-left", State = PetState.Dragging, Phase = 0.12, Progress = 0.52, Movement = new Vector(-18, 0) },
                new SnapshotCase { Name = "02g-settle-right", State = PetState.SettlingFromDrag, Phase = 0.12, Progress = 0.52, Movement = new Vector(18, 0) },
                new SnapshotCase { Name = "03-lick", State = PetState.Licking, Phase = 0.62, Progress = 0.64, Movement = new Vector() },
                new SnapshotCase { Name = "04-lowering", State = PetState.LoweringToSleep, Phase = 0.46, Progress = 0.58, Movement = new Vector() },
                new SnapshotCase { Name = "05-sleep", State = PetState.Sleeping, Phase = 0.33, Progress = 1, Movement = new Vector() },
                new SnapshotCase { Name = "06-wake", State = PetState.Waking, Phase = 0.52, Progress = 0.55, Movement = new Vector() },
                new SnapshotCase { Name = "07a-petting-right", State = PetState.Petting, Phase = 0.32, Progress = 0.50, Movement = new Vector(1, 0) },
                new SnapshotCase { Name = "07aa-petting-left", State = PetState.Petting, Phase = 0.32, Progress = 0.50, Movement = new Vector(-1, 0) },
                new SnapshotCase { Name = "07ab-petted-enter", State = PetState.Petted, Phase = 0.18, Progress = 0.20, Movement = new Vector(1, 0) },
                new SnapshotCase { Name = "07-petted", State = PetState.Petted, Phase = 0.92, Progress = 0.42, Movement = new Vector(1, 0) },
                new SnapshotCase { Name = "07b-petted-left", State = PetState.Petted, Phase = 0.48, Progress = 0.42, Movement = new Vector(-1, 0) },
                new SnapshotCase { Name = "07ba-petted-exit", State = PetState.Petted, Phase = 0.78, Progress = 0.91, Movement = new Vector(1, 0) },
                new SnapshotCase { Name = "07e-soothe-first-stroke", State = PetState.SoothingStroke, Phase = 0.18, Progress = 0.32, Movement = new Vector(1, 0) },
                new SnapshotCase { Name = "07ea-soothe-ear-start-right", State = PetState.SoothingStroke, Phase = 0.18, Progress = 0.09, Movement = new Vector(1, 0) },
                new SnapshotCase { Name = "07eb-soothe-ear-start-left", State = PetState.SoothingStroke, Phase = 0.18, Progress = 0.09, Movement = new Vector(-1, 0) },
                new SnapshotCase { Name = "07f-soothe-second-stroke", State = PetState.SoothingStroke, Phase = 0.38, Progress = 0.76, Movement = new Vector(1, 0) },
                new SnapshotCase { Name = "07g-soothe-stroke-end", State = PetState.SoothingStroke, Phase = 0.42, Progress = 1, Movement = new Vector(1, 0) },
                new SnapshotCase { Name = "07ga-soothed-lowering-start", State = PetState.SoothedLoweringToSleep, Phase = 0.42, Progress = 0, Movement = new Vector(1, 0) },
                new SnapshotCase { Name = "07gb-soothed-lowering-early", State = PetState.SoothedLoweringToSleep, Phase = 0.42, Progress = 0.38, Movement = new Vector(1, 0) },
                new SnapshotCase { Name = "07h-soothed-lowering", State = PetState.SoothedLoweringToSleep, Phase = 0.42, Progress = 0.58, Movement = new Vector(1, 0) },
                new SnapshotCase { Name = "07ha-soothed-lowering-end", State = PetState.SoothedLoweringToSleep, Phase = 0.42, Progress = 1, Movement = new Vector(1, 0) }
            };

            foreach (int direction in new int[] { -1, 1 })
            for (int step = 0; step <= 12; step++)
                cases.Add(new SnapshotCase { Name = "13-tail-" + (direction < 0 ? "left-" : "right-") + step.ToString("00"),
                    State = PetState.Petted, Phase = 0, Progress = step / 12.0, Movement = new Vector(direction, 0) });
            foreach (double p in new double[] { 0, .125, .25, .375, .48, .50, .53, .58, .64, .75, .875, 1 })
                cases.Add(new SnapshotCase { Name = "14-idle-tail-" + ((int)(p * 1000)).ToString("0000"),
                    State = PetState.TailIdle, Phase = .25, Progress = p, Movement = new Vector() });
            foreach (SnapshotCase item in cases)
            {
                CatVisual visual = new CatVisual { Width = 300, Height = 300 };
                visual.SuppressStateTransitions = true;
                visual.DragBlinkOverride = item.ForceDragBlink;
                visual.Update(item.State, item.Phase, item.Progress, item.Movement);
                visual.Measure(new Size(300, 300));
                visual.Arrange(new Rect(0, 0, 300, 300));
                visual.UpdateLayout();

                RenderTargetBitmap bitmap = new RenderTargetBitmap(300, 300, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                string path = Path.Combine(outputDirectory, item.Name + ".png");
                using (FileStream stream = File.Create(path))
                {
                    encoder.Save(stream);
                }
            }

            RenderEarIdle(outputDirectory);
            VerifyTailRootAnchoring();
            RenderBubble(Path.Combine(outputDirectory, "08-bubble.png"), false);
            RenderBubble(Path.Combine(outputDirectory, "09-bubble-sleep.png"), true);
            RenderSleepingLabel(Path.Combine(outputDirectory, "10-sleeping-label.png"));
            RenderCalculator(Path.Combine(outputDirectory, "11-calculator.png"));
            RenderBubbleAndCalculator(Path.Combine(outputDirectory, "12-bubble-calculator-below.png"));
            VerifySoothingJoins();
            VerifyWeatherIconAnimations(outputDirectory);
            foreach (double phase in new double[] { 0, .25, .73 })
            {
                AssertSamePose(RenderPosePixels(PetState.Stand, 0, phase, 0), RenderPosePixels(PetState.TailIdle, 0, phase, 0), "Idle tail entry must match neutral stand.");
                AssertSamePose(RenderPosePixels(PetState.Stand, 0, phase, 0), RenderPosePixels(PetState.TailIdle, 1, phase, 0), "Idle tail exit must match neutral stand.");
                AssertSamePose(RenderPosePixels(PetState.Stand, 0, phase, 0), RenderPosePixels(PetState.EarIdle, 0, phase, 0), "Ear idle entry must match neutral stand.");
                AssertSamePose(RenderPosePixels(PetState.Stand, 0, phase, 0), RenderPosePixels(PetState.EarIdle, 1, phase, 0), "Ear idle exit must match neutral stand.");
            }
        }

        private static void RenderEarIdle(string outputDirectory)
        {
            CatVisual visual = new CatVisual { Width = 300, Height = 300, SuppressStateTransitions = true };
            byte[] baseline = RenderPosePixels(PetState.Stand, 0, .25, 0);
            byte[] midpointBlink = RenderPosePixels(PetState.TailIdle, .5, .25, 0);
            for (int step = 0; step <= 50; step++)
            {
                double earProgress = step / 50.0;
                visual.Update(PetState.EarIdle, .25, earProgress, new Vector());
                RenderTargetBitmap bitmap = RenderElement(visual, 300, 300);
                byte[] pixels = new byte[baseline.Length];
                bitmap.CopyPixels(pixels, 1200, 0);
                // Outside the shared midpoint blink, everything below the ear
                // region remains identical at a fixed breathing phase.
                if (PetMotionProfile.IdleEarBlink(earProgress) <= .001)
                    for (int index = 80 * 1200; index < pixels.Length; index++)
                        if (pixels[index] != baseline[index])
                            throw new InvalidOperationException("Ear idle changed the face or body outside its blink.");
                if (step == 25)
                    AssertSamePose(pixels, midpointBlink,
                        "The ear idle midpoint must use the same blink pose as the tail idle.");
                if (step == 6 || step == 19 || step == 31 || step == 44)
                {
                    int changed = 0;
                    for (int index = 0; index < 80 * 1200; index++)
                        if (pixels[index] != baseline[index]) changed++;
                    if (changed < 100) throw new InvalidOperationException("Ear motion is missing from the rendered pose.");
                }
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (FileStream stream = File.Create(Path.Combine(outputDirectory, "19-ear-idle-" + step.ToString("00") + ".png")))
                    encoder.Save(stream);
            }
        }

        private static void VerifyTailRootAnchoring()
        {
            byte[] neutral = RenderPosePixels(PetState.Stand, 0, .25, 0);
            // Sample close to the positive and negative peaks of both cycles,
            // never at a neutral crossing such as .25 or .75.
            foreach (double tailProgress in new double[] { .16, .34, .66, .84 })
            {
                byte[] wagging = RenderPosePixels(PetState.TailIdle, tailProgress, .25, 0);
                AssertSameRegion(neutral, wagging, 96, 199, 28, 22,
                    "The wagging tail root separated from the original hip socket.");
                if (CountRegionDifferences(neutral, wagging, 42, 82, 75, 116) < 100)
                    throw new InvalidOperationException("The tail-root check did not include a visible tail swing.");
            }
        }

        private static void VerifySoothingJoins()
        {
            foreach (double direction in new double[] { -1, 1 })
            {
                foreach (double phase in new double[] { 0, 0.25, 0.73 })
                {
                    AssertSamePose(
                        RenderPosePixels(PetState.SoothingStroke, 1, phase, direction),
                        RenderPosePixels(PetState.SoothedLoweringToSleep, 0, phase, direction),
                        "The end of stroking must exactly match the start of lowering.");
                    AssertSamePose(
                        RenderPosePixels(PetState.SoothedLoweringToSleep, 1, phase, direction),
                        RenderPosePixels(PetState.Sleeping, 1, phase, direction),
                        "The end of lowering must exactly match sleep, including breathing and Zzz.");
                }
            }
        }

        private static void VerifyWeatherIconAnimations(string outputDirectory)
        {
            int width = InteractionBubbleWindow.DesignWidth;
            int height = InteractionBubbleWindow.RegularDesignHeight;
            InteractionBubbleWindow bubble = new InteractionBubbleWindow
            { Opacity = 0, ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
            try
            {
                bubble.HoldOpenForCalculator();
                foreach (WeatherIconKind kind in Enum.GetValues(typeof(WeatherIconKind)))
                {
                    FrameworkElement visual = bubble.PreparePreview(new WeatherDisplayInfo
                    { LocationWeatherLine = "动态图标测试", DateLine = "测试预览", IconKind = kind }, false);
                    bubble.Show();
                    PumpAnimation(.12);
                    if (bubble.ActiveWeatherAnimations == 0) throw new InvalidOperationException("Visible weather icon has no animation: " + kind + ", visible=" + bubble.IsVisible);
                    RenderTargetBitmap first = RenderElement(visual, width, height);
                    PumpAnimation(.41);
                    RenderTargetBitmap second = RenderElement(visual, width, height);
                    byte[] a = new byte[width * height * 4], b = new byte[a.Length];
                    first.CopyPixels(a, width * 4, 0); second.CopyPixels(b, width * 4, 0);
                    bool changed = false;
                    for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) { changed = true; break; }
                    if (!changed) throw new InvalidOperationException("Weather icon did not move between samples: " + kind);
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(second));
                    using (FileStream stream = File.Create(Path.Combine(outputDirectory, "15-weather-icon-test-" + kind + ".png"))) encoder.Save(stream);
                    bubble.Hide();
                    if (bubble.ActiveWeatherAnimations != 0) throw new InvalidOperationException("Hidden bubble still has weather animation clocks.");
                }
                WeatherService live = new WeatherService(AppSettings.Load());
                if (live.HasWeather)
                {
                    FrameworkElement visual = bubble.PreparePreview(live.CurrentDisplayInfo, false);
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(RenderElement(visual, width, height)));
                    using (FileStream stream = File.Create(Path.Combine(outputDirectory, "16-current-weather.png"))) encoder.Save(stream);
                }
            }
            finally { bubble.Close(); }
        }

        private static void PumpAnimation(double seconds)
        {
            DispatcherFrame frame = new DispatcherFrame();
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
            timer.Tick += delegate { timer.Stop(); frame.Continue = false; };
            timer.Start(); Dispatcher.PushFrame(frame);
        }

        private static byte[] RenderPosePixels(PetState state, double progress, double phase, double direction)
        {
            CatVisual visual = new CatVisual { Width = 300, Height = 300, SuppressStateTransitions = true };
            if (!visual.UsesReferenceSprites)
                throw new InvalidOperationException("Reference sprites are required for the soothing continuity checks.");
            visual.Update(state, phase, progress, new Vector(direction, 0));
            RenderTargetBitmap bitmap = RenderElement(visual, 300, 300);
            byte[] pixels = new byte[300 * 300 * 4];
            bitmap.CopyPixels(pixels, 300 * 4, 0);
            return pixels;
        }

        private static void AssertSamePose(byte[] first, byte[] second, string message)
        {
            for (int index = 0; index < first.Length; index++)
                if (first[index] != second[index]) throw new InvalidOperationException(message);
        }

        private static void AssertSameRegion(byte[] first, byte[] second, int x, int y, int width, int height, string message)
        {
            if (first.Length != second.Length) throw new InvalidOperationException(message);
            for (int row = y; row < y + height; row++)
            for (int column = x; column < x + width; column++)
            {
                int index = (row * 300 + column) * 4;
                for (int channel = 0; channel < 4; channel++)
                    if (first[index + channel] != second[index + channel])
                        throw new InvalidOperationException(message);
            }
        }

        private static int CountRegionDifferences(byte[] first, byte[] second, int x, int y, int width, int height)
        {
            int changedPixels = 0;
            for (int row = y; row < y + height; row++)
            for (int column = x; column < x + width; column++)
            {
                int index = (row * 300 + column) * 4;
                if (first[index] != second[index] || first[index + 1] != second[index + 1]
                    || first[index + 2] != second[index + 2] || first[index + 3] != second[index + 3])
                    changedPixels++;
            }
            return changedPixels;
        }

        private static void RenderBubbleAndCalculator(string outputPath)
        {
            int width = InteractionBubbleWindow.DesignWidth;
            int height = InteractionBubbleWindow.RegularDesignHeight;
            int combinedWidth = width + 8;
            int combinedHeight = height * 2 + 16;
            InteractionBubbleWindow bubble = new InteractionBubbleWindow();
            CalculatorWindow calculator = new CalculatorWindow();
            try
            {
                WeatherDisplayInfo weather = new WeatherDisplayInfo
                {
                    LocationWeatherLine = "中国－上海市－晴",
                    DateLine = DateTime.Now.ToString("yyyy年M月d日 dddd"),
                    IconKind = WeatherIconKind.Sunny
                };
                FrameworkElement bubbleVisual = bubble.PreparePreview(weather, false);
                FrameworkElement calculatorVisual = calculator.PreparePreview();
                RenderTargetBitmap bubbleBitmap = RenderElement(bubbleVisual, width, height);
                RenderTargetBitmap calculatorBitmap = RenderElement(calculatorVisual, width, height);

                Rect bubbleBounds = new Rect(4, 4, width, height);
                Rect workArea = new Rect(0, 0, combinedWidth, combinedHeight);
                Rect calculatorBounds = CalculatorPlacementPolicy.GetBounds(bubbleBounds, 1.0, workArea);
                DrawingVisual combined = new DrawingVisual();
                using (DrawingContext dc = combined.RenderOpen())
                {
                    dc.DrawImage(bubbleBitmap, bubbleBounds);
                    dc.DrawImage(calculatorBitmap, calculatorBounds);
                }

                RenderTargetBitmap bitmap = new RenderTargetBitmap(combinedWidth, combinedHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(combined);
                SaveBitmap(bitmap, outputPath);
            }
            finally
            {
                calculator.Close();
                bubble.Close();
            }
        }

        private static RenderTargetBitmap RenderElement(FrameworkElement visual, int width, int height)
        {
            visual.Measure(new Size(width, height));
            visual.Arrange(new Rect(0, 0, width, height));
            visual.UpdateLayout();
            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            return bitmap;
        }

        private static void SaveBitmap(BitmapSource bitmap, string outputPath)
        {
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(outputPath))
            {
                encoder.Save(stream);
            }
        }

        private static void RenderCalculator(string outputPath)
        {
            CalculatorWindow calculator = new CalculatorWindow();
            try
            {
                calculator.PressForPreview("1", "2", "3", "4", "5", ".", "6", "7");
                FrameworkElement visual = calculator.PreparePreview();
                visual.Measure(new Size(CalculatorWindow.DesignWidth, CalculatorWindow.DesignHeight));
                visual.Arrange(new Rect(0, 0, CalculatorWindow.DesignWidth, CalculatorWindow.DesignHeight));
                visual.UpdateLayout();

                RenderTargetBitmap bitmap = new RenderTargetBitmap(
                    CalculatorWindow.DesignWidth, CalculatorWindow.DesignHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (FileStream stream = File.Create(outputPath))
                {
                    encoder.Save(stream);
                }
            }
            finally
            {
                calculator.Close();
            }
        }

        private static void RenderSleepingLabel(string outputPath)
        {
            OutlinedText label = new OutlinedText
            {
                Text = "睡着了",
                Width = 320,
                Height = 44,
                StrokeThickness = 1.7
            };
            label.Measure(new Size(320, 44));
            label.Arrange(new Rect(0, 0, 320, 44));
            label.UpdateLayout();

            RenderTargetBitmap bitmap = new RenderTargetBitmap(320, 44, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(label);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(outputPath))
            {
                encoder.Save(stream);
            }
        }

        private static void RenderBubble(string outputPath, bool sleeping)
        {
            InteractionBubbleWindow bubble = new InteractionBubbleWindow();
            try
            {
                WeatherDisplayInfo weather = new WeatherDisplayInfo
                {
                    LocationWeatherLine = "中国－上海市－晴",
                    DateLine = DateTime.Now.ToString("yyyy年M月d日 dddd"),
                    IconKind = sleeping ? WeatherIconKind.PartlyCloudy : WeatherIconKind.Sunny
                };
                FrameworkElement visual = bubble.PreparePreview(weather, sleeping);
                int width = InteractionBubbleWindow.DesignWidth;
                int height = sleeping ? InteractionBubbleWindow.SleepingDesignHeight
                    : InteractionBubbleWindow.RegularDesignHeight;
                visual.Measure(new Size(width, height));
                visual.Arrange(new Rect(0, 0, width, height));
                visual.UpdateLayout();

                if (!bubble.HasKeepAwakeAction) throw new InvalidOperationException("The interaction bubble has no keep-awake action.");
                if (bubble.IsKeepAwakeActionVisible == sleeping)
                    throw new InvalidOperationException("Keep-awake must be visible while awake and hidden while sleeping.");
                Rect[] interiorBounds = bubble.VisibleInteriorActionBounds;
                Rect[] sideBounds = bubble.VisibleSideControlBounds;
                if (interiorBounds.Length != 4)
                    throw new InvalidOperationException("Each bubble state must show exactly four interior cards.");
                if (sideBounds.Length != (sleeping ? 2 : 3))
                    throw new InvalidOperationException("The bubble has the wrong number of visible side controls.");
                for (int index = 0; index < interiorBounds.Length; index++)
                {
                    Rect bounds = interiorBounds[index];
                    if (bounds.Left < 0 || bounds.Top < 0 || bounds.Right > width || bounds.Bottom > height)
                        throw new InvalidOperationException("An interior action extends outside the bubble canvas.");
                    if (Math.Abs(bounds.Width - 200) > .1)
                        throw new InvalidOperationException("Interior action widths are no longer aligned.");
                    if (index > 0 && bounds.Top - interiorBounds[index - 1].Bottom < 5.9)
                        throw new InvalidOperationException("Interior actions no longer have a consistent safe gap.");
                }
                if (interiorBounds[0].Top < 51.9 || interiorBounds[interiorBounds.Length - 1].Bottom > 266.1)
                    throw new InvalidOperationException("Interior actions left the cloud's safe central region.");
                foreach (Rect interior in interiorBounds)
                    foreach (Rect sideControl in sideBounds)
                        if (interior.IntersectsWith(sideControl))
                            throw new InvalidOperationException("An interior card overlaps a side control.");
                AssertBubbleControlsBackedByCloud(interiorBounds, sideBounds, width, height);
                if (!sleeping)
                {
                    Rect keepAwakeBounds = bubble.KeepAwakeActionBounds;
                    if (keepAwakeBounds.Left < 0 || keepAwakeBounds.Top < 0 || keepAwakeBounds.Right > width || keepAwakeBounds.Bottom > height)
                        throw new InvalidOperationException("Keep-awake button extends outside the cloud design canvas.");
                    if (Math.Abs(keepAwakeBounds.Width - 52) > 0.1 || Math.Abs(keepAwakeBounds.Height - 52) > 0.1 ||
                        keepAwakeBounds.Left < 289 || keepAwakeBounds.Top < 199)
                        throw new InvalidOperationException("Keep-awake must be a round side control beneath the exit control.");
                    if (bubble.KeepAwakeActionText != "保持清醒" || bubble.KeepAwakeVisualText != "保持\n清醒")
                        throw new InvalidOperationException("Keep-awake button has the wrong initial label.");
                }

                RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (FileStream stream = File.Create(outputPath))
                {
                    encoder.Save(stream);
                }
                if (!sleeping)
                {
                    bool keepAwakeRaised = false;
                    bool requestedKeepAwake = false;
                    bubble.KeepAwakeChanged += delegate(object changedSender, KeepAwakeChangedEventArgs args)
                    {
                        keepAwakeRaised = true;
                        requestedKeepAwake = args.KeepAwake;
                    };
                    bubble.ClickKeepAwakeForPreview();
                    if (!keepAwakeRaised || !requestedKeepAwake || bubble.KeepAwakeActionText != "取消保持清醒" ||
                        bubble.KeepAwakeVisualText != "取消保持\n清醒")
                        throw new InvalidOperationException("Keep-awake toggle did not enter the enabled state.");
                    bubble.ClickKeepAwakeForPreview();
                    if (requestedKeepAwake || bubble.KeepAwakeActionText != "保持清醒" || bubble.KeepAwakeVisualText != "保持\n清醒")
                        throw new InvalidOperationException("Keep-awake toggle did not return to the disabled state.");

                }
            }
            finally
            {
                bubble.Close();
            }
        }

        private static void AssertBubbleControlsBackedByCloud(Rect[] interiorBounds,
            Rect[] sideBounds, int designWidth, int designHeight)
        {
            string path = Path.Combine(AppPaths.UiDirectory, "cloud-bubble-v2.png");
            BitmapImage source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.UriSource = new Uri(path, UriKind.Absolute);
            source.EndInit();
            FormatConvertedBitmap bitmap = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int stride = bitmap.PixelWidth * 4;
            byte[] pixels = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, stride, 0);

            foreach (bool mirrored in new bool[] { false, true })
            {
                foreach (Rect bounds in interiorBounds)
                {
                    const int radius = 16;
                    for (int x = (int)Math.Ceiling(bounds.Left + radius);
                        x <= (int)Math.Floor(bounds.Right - radius); x++)
                    {
                        RequireCloudAlpha(pixels, stride, bitmap.PixelWidth, bitmap.PixelHeight,
                            x, bounds.Top, mirrored, designWidth, designHeight);
                        RequireCloudAlpha(pixels, stride, bitmap.PixelWidth, bitmap.PixelHeight,
                            x, bounds.Bottom, mirrored, designWidth, designHeight);
                    }
                    for (int y = (int)Math.Ceiling(bounds.Top + radius);
                        y <= (int)Math.Floor(bounds.Bottom - radius); y++)
                    {
                        RequireCloudAlpha(pixels, stride, bitmap.PixelWidth, bitmap.PixelHeight,
                            bounds.Left, y, mirrored, designWidth, designHeight);
                        RequireCloudAlpha(pixels, stride, bitmap.PixelWidth, bitmap.PixelHeight,
                            bounds.Right, y, mirrored, designWidth, designHeight);
                    }
                }
                foreach (Rect bounds in sideBounds)
                {
                    for (int sample = 0; sample < 72; sample++)
                    {
                        double angle = sample * Math.PI / 36.0;
                        RequireCloudAlpha(pixels, stride, bitmap.PixelWidth, bitmap.PixelHeight,
                            bounds.Left + bounds.Width / 2.0 + Math.Cos(angle) * bounds.Width / 2.0,
                            bounds.Top + bounds.Height / 2.0 + Math.Sin(angle) * bounds.Height / 2.0,
                            mirrored, designWidth, designHeight);
                    }
                }
            }
        }

        private static void RequireCloudAlpha(byte[] pixels, int stride, int pixelWidth, int pixelHeight,
            double x, double y, bool mirrored, int designWidth, int designHeight)
        {
            double cloudX = mirrored ? designWidth - x : x;
            int sourceX = Math.Max(0, Math.Min(pixelWidth - 1,
                (int)Math.Round(cloudX / designWidth * (pixelWidth - 1))));
            int sourceY = Math.Max(0, Math.Min(pixelHeight - 1,
                (int)Math.Round(y / designHeight * (pixelHeight - 1))));
            byte alpha = pixels[sourceY * stride + sourceX * 4 + 3];
            if (alpha < 230)
                throw new InvalidOperationException("A bubble control crosses the cloud edge at "
                    + x.ToString("0.0") + "," + y.ToString("0.0")
                    + (mirrored ? " (mirrored)." : "."));
        }
    }
}
