using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace CodexCat
{
    internal sealed class OutlinedText : FrameworkElement
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text", typeof(string), typeof(OutlinedText), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public Brush Fill { get; set; }
        public Brush Stroke { get; set; }
        public double StrokeThickness { get; set; }

        public OutlinedText()
        {
            Fill = new SolidColorBrush(Color.FromRgb(190, 92, 219));
            Stroke = new SolidColorBrush(Color.FromRgb(91, 42, 129));
            StrokeThickness = 1.7;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (string.IsNullOrEmpty(Text) || ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            double fontSize = System.Math.Max(18.0, System.Math.Min(35.0, ActualWidth * 0.102));
            Typeface typeface = new Typeface(
                new FontFamily("YouYuan, 幼圆, Microsoft YaHei UI"),
                FontStyles.Normal,
                FontWeights.Normal,
                FontStretches.Normal);
            FormattedText formatted = new FormattedText(Text, CultureInfo.GetCultureInfo("zh-CN"),
                FlowDirection.LeftToRight, typeface, fontSize, Fill, 1.0);
            Geometry geometry = formatted.BuildGeometry(new Point((ActualWidth - formatted.Width) / 2.0, (ActualHeight - formatted.Height) / 2.0));
            Pen outline = new Pen(Stroke, StrokeThickness)
            {
                LineJoin = PenLineJoin.Round,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            drawingContext.PushTransform(new TranslateTransform(1.4, 2.2));
            drawingContext.DrawGeometry(new SolidColorBrush(Color.FromArgb(70, 53, 27, 74)), null, geometry);
            drawingContext.Pop();
            drawingContext.DrawGeometry(Fill, outline, geometry);
        }
    }
}
