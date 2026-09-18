using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CodexCat
{
    internal sealed class KeepAwakeChangedEventArgs : EventArgs
    {
        public bool KeepAwake { get; private set; }

        public KeepAwakeChangedEventArgs(bool keepAwake)
        {
            KeepAwake = keepAwake;
        }
    }

    internal sealed class InteractionBubbleWindow : Window
    {
        internal const int DesignWidth = 400;
        internal const int RegularDesignHeight = 330;
        internal const int SleepingDesignHeight = 330;

        private readonly Viewbox viewbox;
        private readonly Canvas designCanvas;
        private readonly Image cloudImage;
        private readonly StackPanel buttonPanel;
        private readonly Border weatherCard;
        private readonly TextBlock weatherLine;
        private readonly TextBlock dateLine;
        private readonly Canvas weatherIcon;
        private readonly Button wakeButton;
        private readonly Button sootheButton;
        private readonly List<FrameworkElement> interiorControls = new List<FrameworkElement>();
        private Grid scaleControl;
        private Grid exitControl;
        private Grid keepAwakeControl;
        private TextBlock keepAwakeLabel;
        private Button keepAwakeButton;
        private BubbleSide side;
        private double currentScale = 1.0;
        private bool sleeping;
        private bool keepVisibleForCalculator;
        private bool keepAwake;
        private WeatherIconKind currentWeatherKind;
        internal int ActiveWeatherAnimations { get; private set; }

        public event EventHandler PetRequested;
        public event EventHandler CalculatorRequested;
        public event EventHandler SootheRequested;
        public event EventHandler<KeepAwakeChangedEventArgs> KeepAwakeChanged;
        public event EventHandler WakeRequested;
        public event EventHandler ScaleUpRequested;
        public event EventHandler ScaleDownRequested;
        public event EventHandler ExitRequested;

        public BubbleSide CurrentSide { get { return side; } }
        public Rect CurrentBounds { get { return new Rect(Left, Top, Width, Height); } }

        public InteractionBubbleWindow()
        {
            Width = DesignWidth;
            Height = RegularDesignHeight;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;

            designCanvas = new Canvas { Width = DesignWidth, Height = RegularDesignHeight };
            viewbox = new Viewbox
            {
                Stretch = Stretch.Fill,
                StretchDirection = StretchDirection.Both,
                Child = designCanvas
            };
            Content = viewbox;

            cloudImage = CreateCloudImage();
            Canvas.SetLeft(cloudImage, 0);
            Canvas.SetTop(cloudImage, 0);
            designCanvas.Children.Add(cloudImage);

            // The content column stays inside the cloud's dense white centre.
            // The previous 226px cards reached into the tapered right lobe and
            // the bottom outline; this inset leaves a clear cloud margin at
            // every scale and in both regular/sleeping layouts.
            buttonPanel = new StackPanel { Width = 200 };
            Canvas.SetLeft(buttonPanel, 80);
            // Three regular actions plus weather remain inside the dense centre;
            // keep-awake is the third round side control below Exit.
            Canvas.SetTop(buttonPanel, 52);
            designCanvas.Children.Add(buttonPanel);

            Button petButton = CreateButton();
            StackPanel petLabel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            petLabel.Children.Add(new TextBlock
            {
                Text = "摸摸它  ",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontWeight = FontWeights.Bold,
                Foreground = DeepTextBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            petLabel.Children.Add(new TextBlock
            {
                Text = "❤",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 45, 65)),
                VerticalAlignment = VerticalAlignment.Center
            });
            petButton.Content = petLabel;
            petButton.Click += delegate
            {
                HideForInteraction();
                if (PetRequested != null) PetRequested(this, EventArgs.Empty);
            };
            buttonPanel.Children.Add(petButton);
            interiorControls.Add(petButton);

            Button calculatorButton = CreateButton();
            calculatorButton.Content = "算数";
            calculatorButton.Click += delegate
            {
                HoldOpenForCalculator();
                if (CalculatorRequested != null)
                {
                    CalculatorRequested(this, EventArgs.Empty);
                }
                else
                {
                    ReleaseCalculatorVisibilityLock();
                }
            };
            buttonPanel.Children.Add(calculatorButton);
            interiorControls.Add(calculatorButton);

            sootheButton = CreateButton();
            sootheButton.Content = "哄睡它";
            sootheButton.Foreground = new SolidColorBrush(Color.FromRgb(85, 67, 137));
            sootheButton.Click += delegate
            {
                HideForInteraction();
                if (SootheRequested != null) SootheRequested(this, EventArgs.Empty);
            };
            buttonPanel.Children.Add(sootheButton);
            interiorControls.Add(sootheButton);

            weatherCard = CreateCard(58);
            Grid weatherGrid = new Grid();
            weatherGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            weatherGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel weatherTopLine = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            weatherLine = new TextBlock
            {
                Text = "中国－上海市－天气加载中",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = DeepTextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 155,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            weatherIcon = new Canvas
            {
                Width = 24,
                Height = 22,
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            weatherTopLine.Children.Add(weatherLine);
            weatherTopLine.Children.Add(weatherIcon);
            Grid.SetRow(weatherTopLine, 0);
            weatherGrid.Children.Add(weatherTopLine);

            dateLine = new TextBlock
            {
                Text = DateTime.Now.ToString("yyyy年M月d日 dddd"),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = DeepTextBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            Grid.SetRow(dateLine, 1);
            weatherGrid.Children.Add(dateLine);
            weatherCard.Child = weatherGrid;
            buttonPanel.Children.Add(weatherCard);
            interiorControls.Add(weatherCard);

            wakeButton = CreateButton();
            wakeButton.Content = "叫醒它";
            wakeButton.Foreground = new SolidColorBrush(Color.FromRgb(91, 49, 134));
            wakeButton.Visibility = Visibility.Collapsed;
            wakeButton.Click += delegate
            {
                HideForInteraction();
                if (WakeRequested != null) WakeRequested(this, EventArgs.Empty);
            };
            buttonPanel.Children.Add(wakeButton);
            interiorControls.Add(wakeButton);

            CreateScaleControl();
            CreateExitControl();
            CreateKeepAwakeControl();
            SetKeepAwake(false);

            DrawWeatherIcon(WeatherIconKind.Loading);
            IsVisibleChanged += delegate
            {
                StopWeatherAnimations();
                if (IsVisible) AnimateWeatherIcon(currentWeatherKind);
            };
            Closed += delegate { StopWeatherAnimations(); };
            Deactivated += delegate
            {
                if (!keepVisibleForCalculator) Hide();
            };
        }

        public void ShowNear(Rect petBounds, WeatherDisplayInfo weather, bool isSleeping, double scale, Rect workArea)
        {
            sleeping = isSleeping;
            currentScale = Math.Max(0.45, Math.Min(2.20, scale));
            UpdateWeather(weather);
            wakeButton.Visibility = sleeping ? Visibility.Visible : Visibility.Collapsed;
            sootheButton.Visibility = sleeping ? Visibility.Collapsed : Visibility.Visible;
            keepAwakeControl.Visibility = sleeping ? Visibility.Collapsed : Visibility.Visible;

            double designHeight = sleeping ? SleepingDesignHeight : RegularDesignHeight;
            designCanvas.Height = designHeight;
            cloudImage.Height = designHeight;
            side = GetPreferredSide(petBounds, currentScale, workArea);
            Rect bounds = GetBoundsNear(petBounds, sleeping, currentScale, workArea);
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;

            cloudImage.RenderTransform = side == BubbleSide.Right
                ? (Transform)new ScaleTransform(-1, 1, DesignWidth / 2.0, 0)
                : Transform.Identity;
            Show();
            Activate();
        }

        internal static BubbleSide GetPreferredSide(Rect petBounds, double scale, Rect workArea)
        {
            double safeScale = Math.Max(0.45, Math.Min(2.20, scale));
            double bubbleWidth = DesignWidth * safeScale;
            double roomOnRight = workArea.Right - petBounds.Right;
            double roomOnLeft = petBounds.Left - workArea.Left;
            return roomOnRight >= bubbleWidth + 4 * safeScale || roomOnRight >= roomOnLeft
                ? BubbleSide.Right
                : BubbleSide.Left;
        }

        internal static Rect GetBoundsNear(Rect petBounds, bool isSleeping, double scale, Rect workArea)
        {
            double safeScale = Math.Max(0.45, Math.Min(2.20, scale));
            double width = DesignWidth * safeScale;
            double height = (isSleeping ? SleepingDesignHeight : RegularDesignHeight) * safeScale;
            BubbleSide preferredSide = GetPreferredSide(petBounds, safeScale, workArea);
            double gap = 4.0 * safeScale;
            double left = preferredSide == BubbleSide.Right ? petBounds.Right + gap : petBounds.Left - width - gap;
            double top = petBounds.Top + Math.Max(4 * safeScale, (petBounds.Height - height) / 2.0);
            left = Math.Max(workArea.Left + 4, Math.Min(workArea.Right - width - 4, left));
            top = Math.Max(workArea.Top + 4, Math.Min(workArea.Bottom - height - 4, top));
            return new Rect(left, top, width, height);
        }

        public void UpdateWeather(WeatherDisplayInfo weather)
        {
            if (weather == null) return;
            weatherLine.Text = weather.LocationWeatherLine;
            dateLine.Text = weather.DateLine;
            weatherCard.ToolTip = weather.Details;
            if (currentWeatherKind == weather.IconKind && weatherIcon.Children.Count > 0) return;
            StopWeatherAnimations();
            currentWeatherKind = weather.IconKind;
            DrawWeatherIcon(weather.IconKind);
            if (IsVisible) AnimateWeatherIcon(weather.IconKind);
        }

        internal FrameworkElement PreparePreview(WeatherDisplayInfo weather, bool isSleeping)
        {
            sleeping = isSleeping;
            UpdateWeather(weather);
            wakeButton.Visibility = sleeping ? Visibility.Visible : Visibility.Collapsed;
            sootheButton.Visibility = sleeping ? Visibility.Collapsed : Visibility.Visible;
            keepAwakeControl.Visibility = sleeping ? Visibility.Collapsed : Visibility.Visible;
            double designHeight = sleeping ? SleepingDesignHeight : RegularDesignHeight;
            designCanvas.Height = designHeight;
            cloudImage.Height = designHeight;
            Width = DesignWidth;
            Height = designHeight;
            return viewbox;
        }

        internal bool HasKeepAwakeAction { get { return keepAwakeControl != null && keepAwakeButton != null; } }
        internal bool IsKeepAwakeActionVisible { get { return keepAwakeControl.Visibility == Visibility.Visible; } }
        internal string KeepAwakeActionText { get { return keepAwake ? "取消保持清醒" : "保持清醒"; } }
        internal string KeepAwakeVisualText { get { return keepAwakeLabel.Text; } }
        internal Rect KeepAwakeActionBounds
        {
            get
            {
                Point origin = keepAwakeControl.TranslatePoint(new Point(), designCanvas);
                return new Rect(origin, new Size(keepAwakeControl.ActualWidth, keepAwakeControl.ActualHeight));
            }
        }

        internal Rect[] VisibleInteriorActionBounds
        {
            get
            {
                List<Rect> bounds = new List<Rect>();
                foreach (FrameworkElement control in interiorControls)
                {
                    if (control.Visibility != Visibility.Visible) continue;
                    Point origin = control.TranslatePoint(new Point(), designCanvas);
                    bounds.Add(new Rect(origin, new Size(control.ActualWidth, control.ActualHeight)));
                }
                return bounds.ToArray();
            }
        }

        internal Rect[] VisibleSideControlBounds
        {
            get
            {
                List<Rect> bounds = new List<Rect>();
                foreach (FrameworkElement control in new FrameworkElement[] { scaleControl, exitControl, keepAwakeControl })
                {
                    if (control == null || control.Visibility != Visibility.Visible) continue;
                    Point origin = control.TranslatePoint(new Point(), designCanvas);
                    bounds.Add(new Rect(origin, new Size(control.ActualWidth, control.ActualHeight)));
                }
                return bounds.ToArray();
            }
        }

        internal void ClickKeepAwakeForPreview()
        {
            keepAwakeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        public void SetKeepAwake(bool value)
        {
            keepAwake = value;
            keepAwakeLabel.Text = value ? "取消保持\n清醒" : "保持\n清醒";
            string toolTip = value
                ? "恢复按原有时间条件进入特殊睡眠"
                : "阻止定时待机自动入睡；手动哄睡仍然有效";
            keepAwakeControl.ToolTip = toolTip;
            keepAwakeButton.ToolTip = toolTip;
        }

        public void HoldOpenForCalculator()
        {
            keepVisibleForCalculator = true;
        }

        public void ReleaseCalculatorVisibilityLock()
        {
            keepVisibleForCalculator = false;
        }

        private void HideForInteraction()
        {
            keepVisibleForCalculator = false;
            Hide();
        }

        private void CreateScaleControl()
        {
            scaleControl = new Grid
            {
                Width = 54,
                Height = 54,
                Cursor = Cursors.Hand,
                ToolTip = "左半边放大 20%，右半边缩小 20%"
            };
            scaleControl.Children.Add(new Ellipse
            {
                Fill = new SolidColorBrush(Color.FromRgb(248, 174, 174)),
                Stroke = new SolidColorBrush(Color.FromRgb(230, 116, 116)),
                StrokeThickness = 3,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(245, 119, 119),
                    BlurRadius = 7,
                    ShadowDepth = 0,
                    Opacity = 0.28
                }
            });
            scaleControl.Children.Add(new TextBlock
            {
                Text = "放/缩",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(151, 52, 52)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            });

            Grid clickGrid = new Grid { Background = Brushes.Transparent };
            clickGrid.ColumnDefinitions.Add(new ColumnDefinition());
            clickGrid.ColumnDefinitions.Add(new ColumnDefinition());
            Button enlarge = CreateInvisibleButton("放大 20%");
            enlarge.Click += delegate
            {
                if (ScaleUpRequested != null) ScaleUpRequested(this, EventArgs.Empty);
            };
            Grid.SetColumn(enlarge, 0);
            clickGrid.Children.Add(enlarge);
            Button shrink = CreateInvisibleButton("缩小 20%");
            shrink.Click += delegate
            {
                if (ScaleDownRequested != null) ScaleDownRequested(this, EventArgs.Empty);
            };
            Grid.SetColumn(shrink, 1);
            clickGrid.Children.Add(shrink);
            scaleControl.Children.Add(clickGrid);

            Canvas.SetLeft(scaleControl, 288);
            Canvas.SetTop(scaleControl, 80);
            designCanvas.Children.Add(scaleControl);
        }

        private void CreateExitControl()
        {
            exitControl = new Grid
            {
                Width = 52,
                Height = 52,
                Cursor = Cursors.Hand,
                ToolTip = "退下并彻底结束桌宠进程"
            };
            exitControl.Children.Add(new Ellipse
            {
                Fill = new SolidColorBrush(Color.FromRgb(255, 213, 132)),
                Stroke = new SolidColorBrush(Color.FromRgb(238, 166, 65)),
                StrokeThickness = 3,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(242, 171, 61),
                    BlurRadius = 7,
                    ShadowDepth = 0,
                    Opacity = 0.28
                }
            });
            exitControl.Children.Add(new TextBlock
            {
                Text = "退下",
                FontFamily = new FontFamily("YouYuan, 幼圆, Microsoft YaHei UI"),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(147, 86, 24)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            });
            Button exitButton = CreateInvisibleButton("退下并结束桌宠");
            exitButton.Click += delegate
            {
                HideForInteraction();
                if (ExitRequested != null) ExitRequested(this, EventArgs.Empty);
            };
            exitControl.Children.Add(exitButton);

            Canvas.SetLeft(exitControl, 290);
            Canvas.SetTop(exitControl, 140);
            designCanvas.Children.Add(exitControl);
        }

        private void CreateKeepAwakeControl()
        {
            keepAwakeControl = new Grid
            {
                Width = 52,
                Height = 52,
                Cursor = Cursors.Hand
            };
            keepAwakeControl.Children.Add(new Ellipse
            {
                Fill = new SolidColorBrush(Color.FromRgb(198, 239, 209)),
                Stroke = new SolidColorBrush(Color.FromRgb(113, 196, 149)),
                StrokeThickness = 3,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(91, 211, 142),
                    BlurRadius = 7,
                    ShadowDepth = 0,
                    Opacity = 0.28
                }
            });
            keepAwakeLabel = new TextBlock
            {
                FontFamily = new FontFamily("YouYuan, 幼圆, Microsoft YaHei UI"),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 11.5,
                LineHeight = 14,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(48, 119, 78)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            keepAwakeControl.Children.Add(keepAwakeLabel);

            keepAwakeButton = CreateInvisibleButton("切换保持清醒");
            keepAwakeButton.Click += delegate
            {
                SetKeepAwake(!keepAwake);
                if (KeepAwakeChanged != null)
                    KeepAwakeChanged(this, new KeepAwakeChangedEventArgs(keepAwake));
            };
            keepAwakeControl.Children.Add(keepAwakeButton);

            Canvas.SetLeft(keepAwakeControl, 290);
            Canvas.SetTop(keepAwakeControl, 200);
            designCanvas.Children.Add(keepAwakeControl);
        }

        private void DrawWeatherIcon(WeatherIconKind kind)
        {
            weatherIcon.Children.Clear();
            if (kind == WeatherIconKind.Sunny)
            {
                AddSun(weatherIcon, 4, 3);
                return;
            }
            if (kind == WeatherIconKind.PartlyCloudy)
            {
                AddSun(weatherIcon, 1, 0);
                AddCloud(weatherIcon, 4, 8);
                return;
            }
            if (kind == WeatherIconKind.Thunder)
            {
                AddCloud(weatherIcon, 2, 3);
                Polygon bolt = new Polygon
                {
                    Points = new PointCollection { new Point(12, 11), new Point(8, 18), new Point(12, 17), new Point(10, 22), new Point(18, 14), new Point(14, 15) },
                    Fill = new SolidColorBrush(Color.FromRgb(255, 198, 37))
                };
                weatherIcon.Children.Add(bolt);
                return;
            }
            if (kind == WeatherIconKind.Rain)
            {
                AddCloud(weatherIcon, 2, 2);
                for (int i = 0; i < 3; i++)
                {
                    Line rain = new Line
                    {
                        X1 = 7 + i * 5,
                        Y1 = 14,
                        X2 = 5 + i * 5,
                        Y2 = 20,
                        Stroke = new SolidColorBrush(Color.FromRgb(46, 145, 232)),
                        StrokeThickness = 2,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    };
                    weatherIcon.Children.Add(rain);
                }
                return;
            }
            if (kind == WeatherIconKind.Snow)
            {
                AddCloud(weatherIcon, 2, 2);
                for (int i = 0; i < 3; i++)
                {
                    Ellipse snow = new Ellipse { Width = 3, Height = 3, Fill = new SolidColorBrush(Color.FromRgb(84, 178, 244)) };
                    Canvas.SetLeft(snow, 6 + i * 6);
                    Canvas.SetTop(snow, 17);
                    weatherIcon.Children.Add(snow);
                }
                return;
            }
            if (kind == WeatherIconKind.Fog)
            {
                AddCloud(weatherIcon, 2, 0);
                for (int i = 0; i < 2; i++)
                {
                    Line fog = new Line
                    {
                        X1 = 4,
                        X2 = 21,
                        Y1 = 16 + i * 4,
                        Y2 = 16 + i * 4,
                        Stroke = new SolidColorBrush(Color.FromRgb(96, 151, 180)),
                        StrokeThickness = 2,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    };
                    weatherIcon.Children.Add(fog);
                }
                return;
            }

            AddCloud(weatherIcon, 2, 5);
        }

        private static void AddSun(Canvas canvas, double left, double top)
        {
            Canvas sunGroup = new Canvas { Width = 20, Height = 20 };
            for (int ray = 0; ray < 8; ray++)
            {
                double angle = ray * Math.PI / 4;
                sunGroup.Children.Add(new Line
                {
                    X1 = 10 + Math.Cos(angle) * 8, Y1 = 10 + Math.Sin(angle) * 8,
                    X2 = 10 + Math.Cos(angle) * 10, Y2 = 10 + Math.Sin(angle) * 10,
                    Stroke = new SolidColorBrush(Color.FromRgb(255, 182, 37)), StrokeThickness = 1.5,
                    StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
                });
            }
            Ellipse sun = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(Color.FromRgb(255, 197, 45)),
                Stroke = new SolidColorBrush(Color.FromRgb(244, 151, 30)),
                StrokeThickness = 1.4
            };
            Canvas.SetLeft(sun, 3);
            Canvas.SetTop(sun, 3);
            sunGroup.Children.Add(sun);
            Canvas.SetLeft(sunGroup, left);
            Canvas.SetTop(sunGroup, top);
            canvas.Children.Add(sunGroup);
        }

        private static DoubleAnimation Loop(double from, double to, double seconds, bool reverse)
        {
            return new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds))
            { AutoReverse = reverse, RepeatBehavior = RepeatBehavior.Forever };
        }

        private void AnimateWeatherIcon(WeatherIconKind kind)
        {
            for (int i = 0; i < weatherIcon.Children.Count; i++)
            {
                UIElement element = weatherIcon.Children[i];
                if ((kind == WeatherIconKind.Sunny || kind == WeatherIconKind.PartlyCloudy) && i == 0)
                {
                    RotateTransform rotate = new RotateTransform(0, 10, 10);
                    element.RenderTransform = rotate;
                    rotate.BeginAnimation(RotateTransform.AngleProperty, Loop(0, 360, 16, false));
                }
                else
                {
                    TranslateTransform drift = new TranslateTransform();
                    element.RenderTransform = drift;
                    if ((kind == WeatherIconKind.Rain || kind == WeatherIconKind.Snow) && i > 0)
                    {
                        DoubleAnimation fall = Loop(0, 3.5, kind == WeatherIconKind.Rain ? .75 : 1.6, false);
                        fall.BeginTime = TimeSpan.FromSeconds(i * .12);
                        drift.BeginAnimation(TranslateTransform.YProperty, fall);
                        element.BeginAnimation(OpacityProperty, Loop(.95, .18, kind == WeatherIconKind.Rain ? .75 : 1.6, false));
                    }
                    else if (kind == WeatherIconKind.Thunder && i > 0)
                    {
                        DoubleAnimationUsingKeyFrames flash = new DoubleAnimationUsingKeyFrames
                        { Duration = TimeSpan.FromSeconds(2), RepeatBehavior = RepeatBehavior.Forever };
                        flash.KeyFrames.Add(new DiscreteDoubleKeyFrame(.3, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                        flash.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(.75))));
                        flash.KeyFrames.Add(new DiscreteDoubleKeyFrame(.3, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(.9))));
                        flash.KeyFrames.Add(new DiscreteDoubleKeyFrame(.95, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.0))));
                        flash.KeyFrames.Add(new DiscreteDoubleKeyFrame(.3, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.15))));
                        element.BeginAnimation(OpacityProperty, flash);
                    }
                    else
                    {
                        drift.BeginAnimation(TranslateTransform.XProperty, Loop(-1.2, 1.2, 1.8 + i * .15, true));
                        if (kind == WeatherIconKind.Loading || kind == WeatherIconKind.Fog)
                            element.BeginAnimation(OpacityProperty, Loop(.5, 1, 1.1, true));
                    }
                }
                ActiveWeatherAnimations++;
            }
        }

        private void StopWeatherAnimations()
        {
            foreach (UIElement element in weatherIcon.Children)
            {
                element.BeginAnimation(OpacityProperty, null);
                TranslateTransform drift = element.RenderTransform as TranslateTransform;
                if (drift != null)
                {
                    drift.BeginAnimation(TranslateTransform.XProperty, null);
                    drift.BeginAnimation(TranslateTransform.YProperty, null);
                }
                RotateTransform rotate = element.RenderTransform as RotateTransform;
                if (rotate != null) rotate.BeginAnimation(RotateTransform.AngleProperty, null);
                element.RenderTransform = Transform.Identity;
            }
            ActiveWeatherAnimations = 0;
        }

        private static void AddCloud(Canvas canvas, double left, double top)
        {
            System.Windows.Shapes.Path cloud = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M2,13 C2,9 5,7 8,8 C9,4 14,3 17,7 C21,7 23,9 23,12 C23,15 21,16 18,16 L7,16 C4,16 2,15 2,13 Z"),
                Fill = new SolidColorBrush(Color.FromRgb(139, 204, 241)),
                Stroke = new SolidColorBrush(Color.FromRgb(61, 142, 198)),
                StrokeThickness = 1.1,
                Stretch = Stretch.Uniform,
                Width = 21,
                Height = 15
            };
            Canvas.SetLeft(cloud, left);
            Canvas.SetTop(cloud, top);
            canvas.Children.Add(cloud);
        }

        private static Image CreateCloudImage()
        {
            string path = System.IO.Path.Combine(AppPaths.UiDirectory, "cloud-bubble-v2.png");
            Image image = new Image
            {
                Width = DesignWidth,
                Height = RegularDesignHeight,
                Stretch = Stretch.Fill,
                IsHitTestVisible = false,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(92, 211, 255),
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.42
                }
            };

            if (File.Exists(path))
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                image.Source = bitmap;
            }
            return image;
        }

        private static Button CreateButton()
        {
            Button button = new Button
            {
                Height = 46,
                Margin = new Thickness(0, 0, 0, 6),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                Foreground = DeepTextBrush,
                Background = PaleYellowBrush,
                BorderBrush = DeepBlueBorderBrush,
                BorderThickness = new Thickness(3),
                Cursor = Cursors.Hand,
                Template = CreateRoundedTemplate(16)
            };
            return button;
        }

        private static Border CreateCard(double height)
        {
            return new Border
            {
                Height = height,
                Margin = new Thickness(0, 0, 0, 6),
                CornerRadius = new CornerRadius(16),
                Background = PaleYellowBrush,
                BorderBrush = DeepBlueBorderBrush,
                BorderThickness = new Thickness(3),
                Padding = new Thickness(3, 3, 3, 3)
            };
        }

        private static Button CreateInvisibleButton(string toolTip)
        {
            return new Button
            {
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = toolTip,
                Opacity = 0.01
            };
        }

        private static ControlTemplate CreateRoundedTemplate(double radius)
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(Border.PaddingProperty, new Thickness(4, 1, 4, 1));

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.AppendChild(presenter);

            return new ControlTemplate(typeof(Button)) { VisualTree = border };
        }

        private static SolidColorBrush DeepBlueBorderBrush
        {
            get { return new SolidColorBrush(Color.FromArgb(191, 18, 72, 137)); }
        }

        private static SolidColorBrush PaleYellowBrush
        {
            get { return new SolidColorBrush(Color.FromRgb(255, 248, 205)); }
        }

        private static SolidColorBrush DeepTextBrush
        {
            get { return new SolidColorBrush(Color.FromRgb(27, 62, 101)); }
        }
    }
}
