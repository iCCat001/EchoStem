using Dopamine.ViewModels.Common;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Dopamine.Views.Common
{
    public partial class BackgroundCoverArtControl : UserControl
    {
        // Resolution of the baked background bitmaps. The blurred one can be small (it is
        // stretched over the window but is blurred anyway), the sharp one needs more detail.
        private const int BlurredSnapshotSize = 640;
        private const int SharpSnapshotSize = 1024;

        // Extra margin around the cover art square. The baked bitmaps have no faded edges, so a
        // small value is enough; it only prevents rounding artifacts at the window border.
        private const double SnapshotBleed = 16;

        // When true, the baked background has no faded edges (mirrored margin + crop), which
        // allows a small bleed. When false, the previous behaviour (faded edges + large bleed)
        // is used, which zooms the cover art in more.
        private static readonly bool RemoveSnapshotEdgeFade = true;

        // On-screen size of the cover art square for which the blur radius is defined. Used to
        // convert the radius to the snapshot resolution.
        private const double SnapshotReferenceSize = 1800;

        private static readonly Duration SnapshotFadeDuration = new Duration(TimeSpan.FromSeconds(0.4));

        private BackgroundCoverArtControlViewModel viewModel;
        private double materialBlurRadius;
        private bool materialBlurInitialized;
        private int snapshotVersion;
        private readonly DispatcherTimer sizeChangeTimer;

        public BackgroundCoverArtControl()
        {
            InitializeComponent();

            // While the window is being resized, SizeChanged fires continuously, and every size
            // change re-renders the background. Coalesce them into a single update.
            this.sizeChangeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            this.sizeChangeTimer.Tick += (_, __) =>
            {
                this.sizeChangeTimer.Stop();
                this.UpdateCoverImageSize();
            };

            this.DataContextChanged += this.BackgroundCoverArtControl_DataContextChanged;

            // Prism can set the DataContext while InitializeComponent() runs, in which case
            // DataContextChanged has already fired before we subscribed. Attach explicitly.
            this.AttachViewModel(this.DataContext as BackgroundCoverArtControlViewModel);
        }

        /// <summary>
        /// Applies (or removes) the material blur on the cover art background. The cover art is
        /// enlarged by a bleed margin, so the baked blur never fades out inside the window (which
        /// would show up as a white halo).
        /// </summary>
        public void SetMaterialBlur(double radius)
        {
            // Re-applying the same blur would needlessly re-render the background bitmap.
            if (this.materialBlurInitialized && Math.Abs(radius - this.materialBlurRadius) < 0.01)
            {
                return;
            }

            this.materialBlurInitialized = true;
            this.materialBlurRadius = radius;

            this.UpdateCoverImageSize();
            this.UpdateBackgroundSnapshot();
        }

        private void BackgroundCoverArtControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            this.AttachViewModel(e.NewValue as BackgroundCoverArtControlViewModel);
        }

        private void AttachViewModel(BackgroundCoverArtControlViewModel viewModel)
        {
            if (this.viewModel != null)
            {
                this.viewModel.PropertyChanged -= this.ViewModel_PropertyChanged;
            }

            this.viewModel = viewModel;

            if (this.viewModel != null)
            {
                this.viewModel.PropertyChanged += this.ViewModel_PropertyChanged;
            }

            // Apply the initial state (the ViewModel was created before we subscribed).
            this.UpdateBackgroundSnapshot();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BackgroundCoverArtControlViewModel.CoverArtViewModel))
            {
                this.UpdateBackgroundSnapshot();
            }
        }

        private void UserControl_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            this.sizeChangeTimer.Stop();
            this.sizeChangeTimer.Start();
        }

        private void UpdateCoverImageSize()
        {
            if (this.ActualWidth == 0 || this.ActualHeight == 0)
            {
                return;
            }

            // Compensating the faded snapshot edges allows a much smaller bleed, which zooms the
            // cover art in less. Toggle this to compare both looks.
            if (!RemoveSnapshotEdgeFade && this.materialBlurRadius > 0)
            {
                // The baked blur fades out at the edges of the bitmap, so the cover art must
                // overflow the window on all four sides.
                double largeBleed = this.materialBlurRadius * 2;
                double largeSize = Math.Max(this.ActualWidth, this.ActualHeight * 1.5) + (largeBleed * 2);

                this.CoverImage.Width = largeSize;
                this.CoverImage.Height = largeSize;

                return;
            }

            // The baked bitmap has no faded edges (see RenderBlurred), so only a tiny bleed is
            // needed to avoid rounding artifacts at the window border. The control is offset
            // upwards by AlignBackgroundCoverArt (in NowPlaying), so the height is accounted for.
            double bleed = this.materialBlurRadius > 0 ? SnapshotBleed : 0;
            double size = Math.Max(this.ActualWidth, this.ActualHeight * 1.5) + (bleed * 2);

            this.CoverImage.Width = size;
            this.CoverImage.Height = size;
        }

        private async void UpdateBackgroundSnapshot()
        {
            // The cover art is set from a background thread.
            if (!this.Dispatcher.CheckAccess())
            {
                this.Dispatcher.BeginInvoke(new Action(this.UpdateBackgroundSnapshot));
                return;
            }

            int version = ++this.snapshotVersion;

            byte[] artwork = this.viewModel?.CoverArtViewModel?.CoverArt;

            if (artwork == null || artwork.Length == 0)
            {
                this.ShowSnapshot(null);
                return;
            }

            bool blurred = this.materialBlurRadius > 0;
            int snapshotSize = blurred ? BlurredSnapshotSize : SharpSnapshotSize;
            double snapshotBlurRadius = this.materialBlurRadius * snapshotSize / SnapshotReferenceSize;

            BitmapSource decoded = await Task.Run(() => DecodeArtwork(artwork, snapshotSize));

            if (decoded == null || version != this.snapshotVersion)
            {
                return;
            }

            // Rendering the blur requires the UI thread.
            BitmapSource snapshot = blurred
                ? RenderBlurred(decoded, snapshotSize, snapshotBlurRadius)
                : decoded;

            if (snapshot != null && version == this.snapshotVersion)
            {
                this.ShowSnapshot(snapshot);
            }
        }

        private void ShowSnapshot(BitmapSource snapshot)
        {
            // Keep the previous snapshot underneath and fade the new one in on top of it: a
            // compositor-only transition, no re-rendering of the background.
            this.PreviousBackground.Source = this.CurrentBackground.Source;

            if (this.PreviousBackground.Source != null)
            {
                this.PreviousBackground.BeginAnimation(UIElement.OpacityProperty, null);
                this.PreviousBackground.Opacity = 1.0;
            }

            this.CurrentBackground.Source = snapshot;

            if (snapshot == null)
            {
                return;
            }

            this.CurrentBackground.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, SnapshotFadeDuration));
        }

        private static BitmapSource DecodeArtwork(byte[] artwork, int decodeWidth)
        {
            try
            {
                using (var stream = new MemoryStream(artwork))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = decodeWidth;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    return bitmap;
                }
            }
            catch
            {
                return null;
            }
        }

        // Renders the (downscaled) artwork with a Gaussian blur into a bitmap, once.
        private static BitmapSource RenderBlurred(BitmapSource source, int size, double radius)
        {
            try
            {
                if (!RemoveSnapshotEdgeFade)
                {
                    return RenderBlurredSimple(source, size, radius);
                }

                // The artwork is surrounded by mirrored copies, and only its central part is kept:
                // the blur therefore never fades to transparency inside the bitmap, so the cover
                // art doesn't need a large bleed (which used to zoom it in).
                int margin = (int)Math.Ceiling(radius * 3);
                int canvasSize = size + (margin * 2);

                var artworkRect = new Rect(margin, margin, size, size);

                var artworkBrush = new DrawingBrush(new ImageDrawing(source, artworkRect))
                {
                    TileMode = TileMode.FlipXY,
                    Viewport = artworkRect,
                    ViewportUnits = BrushMappingMode.Absolute,
                    Stretch = Stretch.None
                };

                var tiledVisual = new DrawingVisual();

                using (DrawingContext drawingContext = tiledVisual.RenderOpen())
                {
                    drawingContext.DrawRectangle(artworkBrush, null, new Rect(0, 0, canvasSize, canvasSize));
                }

                var tiledBitmap = new RenderTargetBitmap(canvasSize, canvasSize, 96, 96, PixelFormats.Pbgra32);
                tiledBitmap.Render(tiledVisual);

                var image = new Image
                {
                    Source = tiledBitmap,
                    Width = canvasSize,
                    Height = canvasSize,
                    Stretch = Stretch.Fill,
                    Effect = new BlurEffect
                    {
                        Radius = radius,
                        KernelType = KernelType.Gaussian,
                        RenderingBias = RenderingBias.Performance
                    }
                };

                image.Measure(new Size(canvasSize, canvasSize));
                image.Arrange(new Rect(0, 0, canvasSize, canvasSize));
                image.UpdateLayout();

                var blurredBitmap = new RenderTargetBitmap(canvasSize, canvasSize, 96, 96, PixelFormats.Pbgra32);
                blurredBitmap.Render(image);
                blurredBitmap.Freeze();

                var croppedBitmap = new CroppedBitmap(blurredBitmap, new Int32Rect(margin, margin, size, size));
                croppedBitmap.Freeze();

                return croppedBitmap;
            }
            catch
            {
                return null;
            }
        }

        // Original behaviour: the artwork is blurred as-is, so the blur fades to transparency at
        // the edges of the bitmap.
        private static BitmapSource RenderBlurredSimple(BitmapSource source, int size, double radius)
        {
            var image = new Image
            {
                Source = source,
                Width = size,
                Height = size,
                Stretch = Stretch.UniformToFill,
                Effect = new BlurEffect
                {
                    Radius = radius,
                    KernelType = KernelType.Gaussian,
                    RenderingBias = RenderingBias.Performance
                }
            };

            image.Measure(new Size(size, size));
            image.Arrange(new Rect(0, 0, size, size));
            image.UpdateLayout();

            var target = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            target.Render(image);
            target.Freeze();

            return target;
        }
    }
}
