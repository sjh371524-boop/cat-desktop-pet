using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CodexCat
{
    internal sealed class CalculatorWindow : Window
    {
        internal const int DesignWidth = 400;
        internal const int DesignHeight = 330;

        private readonly CalculatorEngine engine;
        private readonly TextBlock displayText;
        private readonly TextBlock statusText;
        private readonly Viewbox viewbox;

        public CalculatorWindow()
        {
            engine = new CalculatorEngine();
            Width = DesignWidth;
            Height = DesignHeight;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Title = "猫猫算数机";

            Grid designRoot = new Grid { Width = DesignWidth, Height = DesignHeight, Margin = new Thickness(5) };
            Border shell = new Border
            {
                CornerRadius = new CornerRadius(26),
                BorderBrush = new SolidColorBrush(Color.FromRgb(83, 109, 135)),
                BorderThickness = new Thickness(2.5),
                Background = new LinearGradientBrush(
                    Color.FromRgb(34, 55, 79),
                    Color.FromRgb(15, 34, 55),
                    new Point(0, 0),
                    new Point(1, 1)),
                Padding = new Thickness(13),
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(20, 35, 52),
                    BlurRadius = 14,
                    ShadowDepth = 4,
                    Opacity = 0.42
                }
            };
            designRoot.Children.Add(shell);

            Grid keypad = new Grid();
            keypad.ColumnDefinitions.Add(new ColumnDefinition());
            keypad.ColumnDefinitions.Add(new ColumnDefinition());
            keypad.ColumnDefinitions.Add(new ColumnDefinition());
            keypad.ColumnDefinitions.Add(new ColumnDefinition());
            keypad.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
            for (int row = 0; row < 6; row++) keypad.RowDefinitions.Add(new RowDefinition());
            shell.Child = keypad;

            Border displayPanel = new Border
            {
                Margin = new Thickness(2, 1, 2, 5),
                CornerRadius = new CornerRadius(13),
                BorderBrush = new SolidColorBrush(Color.FromRgb(8, 23, 36)),
                BorderThickness = new Thickness(2),
                Background = new LinearGradientBrush(
                    Color.FromRgb(238, 244, 239),
                    Color.FromRgb(210, 224, 215),
                    new Point(0, 0),
                    new Point(0, 1)),
                Padding = new Thickness(10, 4, 36, 4)
            };
            Grid displayGrid = new Grid();
            displayGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            displayGrid.RowDefinitions.Add(new RowDefinition());
            statusText = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(74, 92, 86)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            displayText = new TextBlock
            {
                Text = "0",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 29,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(17, 37, 52)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetRow(statusText, 0);
            Grid.SetRow(displayText, 1);
            displayGrid.Children.Add(statusText);
            displayGrid.Children.Add(displayText);
            displayPanel.Child = displayGrid;
            Grid.SetRow(displayPanel, 0);
            Grid.SetColumnSpan(displayPanel, 4);
            keypad.Children.Add(displayPanel);

            AddRow(keypad, 1, new[] { "MC", "MR", "M+", "M−" });
            AddRow(keypad, 2, new[] { "AC", "±", "%", "÷" });
            AddRow(keypad, 3, new[] { "7", "8", "9", "×" });
            AddRow(keypad, 4, new[] { "4", "5", "6", "−" });
            AddRow(keypad, 5, new[] { "1", "2", "3", "+" });
            AddKey(keypad, "0", 6, 0, 2);
            AddKey(keypad, ".", 6, 2, 1);
            AddKey(keypad, "=", 6, 3, 1);

            Button closeButton = new Button
            {
                Content = "×",
                Width = 22,
                Height = 22,
                Margin = new Thickness(0, 15, 17, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(105, 49, 49)),
                Background = new SolidColorBrush(Color.FromRgb(250, 178, 174)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 116, 110)),
                BorderThickness = new Thickness(1.5),
                Cursor = Cursors.Hand,
                ToolTip = "关闭算数机",
                Template = CreateRoundedTemplate(13)
            };
            closeButton.Click += delegate { Close(); };
            designRoot.Children.Add(closeButton);

            viewbox = new Viewbox
            {
                Stretch = Stretch.Fill,
                StretchDirection = StretchDirection.Both,
                Child = designRoot
            };
            Content = viewbox;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        public void ShowAt(Rect bounds)
        {
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
            if (!IsVisible) Show();
            Activate();
            Focus();
        }

        internal FrameworkElement PreparePreview()
        {
            return viewbox;
        }

        internal void PressForPreview(params string[] keys)
        {
            foreach (string key in keys) engine.Press(key);
            RefreshDisplay();
        }

        private void AddRow(Grid keypad, int row, string[] keys)
        {
            for (int column = 0; column < keys.Length; column++) AddKey(keypad, keys[column], row, column, 1);
        }

        private void AddKey(Grid keypad, string key, int row, int column, int columnSpan)
        {
            bool operation = key == "+" || key == "−" || key == "×" || key == "÷" || key == "=";
            bool memoryKey = key.StartsWith("M", StringComparison.Ordinal);
            bool functionKey = key == "AC" || key == "±" || key == "%";
            Button button = new Button
            {
                Content = key,
                Tag = key,
                Margin = new Thickness(3),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = memoryKey ? 13.5 : 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = operation ? Brushes.White : new SolidColorBrush(Color.FromRgb(19, 39, 58)),
                Background = operation
                    ? (Brush)new LinearGradientBrush(Color.FromRgb(255, 158, 46), Color.FromRgb(242, 112, 16), new Point(0, 0), new Point(0, 1))
                    : (memoryKey
                        ? (Brush)new LinearGradientBrush(Color.FromRgb(90, 110, 136), Color.FromRgb(58, 76, 101), new Point(0, 0), new Point(0, 1))
                        : (functionKey
                            ? (Brush)new LinearGradientBrush(Color.FromRgb(215, 222, 230), Color.FromRgb(168, 182, 197), new Point(0, 0), new Point(0, 1))
                            : (Brush)new LinearGradientBrush(Color.FromRgb(255, 255, 255), Color.FromRgb(224, 228, 233), new Point(0, 0), new Point(0, 1)))),
                BorderBrush = operation
                    ? new SolidColorBrush(Color.FromRgb(255, 183, 74))
                    : new SolidColorBrush(Color.FromRgb(95, 113, 132)),
                BorderThickness = new Thickness(1.4),
                Cursor = Cursors.Hand,
                Template = CreateRoundedTemplate(10),
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(7, 18, 29),
                    BlurRadius = 4,
                    ShadowDepth = 2,
                    Opacity = 0.35
                }
            };
            if (memoryKey) button.Foreground = Brushes.White;
            button.Click += OnCalculatorButtonClick;
            Grid.SetRow(button, row);
            Grid.SetColumn(button, column);
            Grid.SetColumnSpan(button, columnSpan);
            keypad.Children.Add(button);
        }

        private void OnCalculatorButtonClick(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;
            engine.Press(button.Tag as string);
            RefreshDisplay();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            string key = null;
            if (e.Key >= Key.D0 && e.Key <= Key.D9) key = ((int)e.Key - (int)Key.D0).ToString();
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) key = ((int)e.Key - (int)Key.NumPad0).ToString();
            else if (e.Key == Key.Decimal || e.Key == Key.OemPeriod) key = ".";
            else if (e.Key == Key.Add || (e.Key == Key.OemPlus && Keyboard.Modifiers == ModifierKeys.Shift)) key = "+";
            else if (e.Key == Key.Subtract || e.Key == Key.OemMinus) key = "−";
            else if (e.Key == Key.Multiply) key = "×";
            else if (e.Key == Key.Divide || e.Key == Key.OemQuestion) key = "÷";
            else if (e.Key == Key.Enter || e.Key == Key.Return) key = "=";
            else if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
            else if (e.Key == Key.Delete) key = "AC";

            if (key == null) return;
            engine.Press(key);
            RefreshDisplay();
            e.Handled = true;
        }

        private void RefreshDisplay()
        {
            displayText.Text = engine.Display;
            statusText.Text = engine.Status;
        }

        private static ControlTemplate CreateRoundedTemplate(double radius)
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(Border.PaddingProperty, new Thickness(3, 1, 3, 1));

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.AppendChild(presenter);
            return new ControlTemplate(typeof(Button)) { VisualTree = border };
        }
    }
}
