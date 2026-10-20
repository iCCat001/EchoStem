using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Dopamine.Controls
{
    public class CrossFadeContentControl : ContentControl
    {
        private ContentPresenter PART_MainContent;
        private Shape PART_PaintArea;

        public double Duration
        {
            get { return Convert.ToDouble(GetValue(DurationProperty)); }
            set { SetValue(DurationProperty, value); }
        }

        public static readonly DependencyProperty DurationProperty = DependencyProperty.Register("Duration", typeof(double), typeof(CrossFadeContentControl), new PropertyMetadata(0.5));

        /// <summary>
        /// Scale at which the old content is captured. Below 1, the captured bitmap is smaller
        /// than the control: the fade still looks the same, but the capture is much cheaper.
        /// </summary>
        public double RenderScale
        {
            get { return Convert.ToDouble(GetValue(RenderScaleProperty)); }
            set { SetValue(RenderScaleProperty, value); }
        }

        public static readonly DependencyProperty RenderScaleProperty = DependencyProperty.Register("RenderScale", typeof(double), typeof(CrossFadeContentControl), new PropertyMetadata(1.0));

        static CrossFadeContentControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CrossFadeContentControl), new FrameworkPropertyMetadata(typeof(CrossFadeContentControl)));
        }

        public override void OnApplyTemplate()
        {
            this.PART_MainContent = (ContentPresenter)GetTemplateChild("PART_MainContent");
            this.PART_PaintArea = (Shape)GetTemplateChild("PART_PaintArea");

            base.OnApplyTemplate();
        }
        
        protected override void OnContentChanged(object oldContent, object newContent)
        {
            try
            {
                if (PART_PaintArea != null && PART_MainContent != null)
                {
                    PART_PaintArea.Fill = this.CreateBrushFromVisual(PART_MainContent);
                    this.BeginAnimateContentReplacement();
                }

                base.OnContentChanged(oldContent, newContent);
            }
            catch (Exception)
            {
            }
        }

        private Brush CreateBrushFromVisual(Visual iVisual)
        {
            if (iVisual == null)
            {
                throw new ArgumentNullException("iVisual");
            }

            double scale = this.RenderScale;

            if (double.IsNaN(scale) || scale <= 0 || scale > 1)
            {
                scale = 1.0;
            }

            int pixelWidth = Convert.ToInt32(this.ActualWidth * scale);
            int pixelHeight = Convert.ToInt32(this.ActualHeight * scale);

            if (pixelWidth <= 0 || pixelHeight <= 0)
            {
                throw new ArgumentOutOfRangeException("iVisual");
            }

            var drawingVisual = new DrawingVisual();

            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                if (scale < 1.0)
                {
                    drawingContext.PushTransform(new ScaleTransform(scale, scale));
                }

                drawingContext.DrawRectangle(new VisualBrush(iVisual), null, new Rect(0, 0, this.ActualWidth, this.ActualHeight));

                if (scale < 1.0)
                {
                    drawingContext.Pop();
                }
            }

            var target = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
            target.Render(drawingVisual);

            var brush = new ImageBrush(target);
            brush.Freeze();

            return brush;
        }

        private void BeginAnimateContentReplacement()
        {
            PART_PaintArea.Opacity = 1.0;

            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(this.Duration)),
                AutoReverse = false
            };

            var fadeInAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(this.Duration)),
                AutoReverse = false
            };

            PART_PaintArea.BeginAnimation(OpacityProperty, fadeOutAnimation);
            PART_MainContent.BeginAnimation(OpacityProperty, fadeInAnimation);
        }
    }
}
