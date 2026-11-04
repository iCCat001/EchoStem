using Dopamine.Utils;
using Dopamine.Views.Common;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Dopamine.Views.NowPlaying
{
    public partial class NowPlayingShowcase : UserControl
    {
        // Side-by-side ("wide") layout: the cover art is derived from the available height.
        private const double CoverArtToHeightFactor = 1.5;
        private const int MinimumCoverArtSize = 250;
        private const int MaximumCoverArtSize = 600;

        // Stacked ("portrait") layout: cover art on top, text below.
        private const double StackedCoverArtWidthRatio = 0.86;
        private const double StackedCoverArtToHeightFactor = 1.62;
        private const double StackedVerticalReserve = 90;
        private const double MaximumStackedCoverArtSize = 680;
        private const double MinimumStackedCoverArtSize = 180;
        private const double StackedHorizontalPadding = 24;
        private const double StackedTopPadding = 18;
        private const double AnimationDurationSeconds = 0.08;

        private bool? isStacked;

        public NowPlayingShowcase()
        {
            InitializeComponent();
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.UpdateLayoutMode();
        }

        /// <summary>
        /// Picks between the side-by-side and the stacked arrangement, the same way a responsive web
        /// page does: the stacked arrangement kicks in in portrait, or as soon as the artwork (which
        /// is sized from the height) plus a readable text column no longer fit next to each other.
        /// </summary>
        private void UpdateLayoutMode()
        {
            double width = this.ActualWidth;
            double height = this.ActualHeight;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            bool stack = ResponsiveLayout.ShouldStackVertically(width, height)
                || ResponsiveLayout.DoesNotFitSideBySide(width, this.GetSideBySideCoverArtSize(height));

            if (this.isStacked != stack)
            {
                this.isStacked = stack;
                this.ApplyArrangement(stack);
            }

            if (stack)
            {
                this.ApplyStackedLayout(width, height);
            }
            else
            {
                this.ResizePlaybackInfo();
            }
        }

        private double GetSideBySideCoverArtSize(double height)
        {
            return ResponsiveLayout.Clamp(height / CoverArtToHeightFactor, MinimumCoverArtSize, MaximumCoverArtSize);
        }

        /// <summary>
        /// Applies the properties that cannot be animated: docking, alignment and the fixed height
        /// used by the side-by-side storyboards.
        /// </summary>
        private void ApplyArrangement(bool stack)
        {
            DockPanel.SetDock(this.CoverArt, stack ? Dock.Top : Dock.Left);
            this.PlaybackInfoPanel.VerticalAlignment = stack ? VerticalAlignment.Center : VerticalAlignment.Bottom;

            this.PlaybackInfo.VerticalAlignment = stack ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            this.PlaybackInfo.HorizontalAlignment = HorizontalAlignment.Stretch;
            this.PlaybackInfo.TextAlignment = stack ? TextAlignment.Center : TextAlignment.Left;
            this.PlaybackInfo.LoveAndRatingAlignment = stack ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            this.PlaybackInfo.Margin = stack
                ? new Thickness(StackedHorizontalPadding, StackedTopPadding, StackedHorizontalPadding, 0)
                : new Thickness(30, 0, 0, 20);

            if (stack)
            {
                // "Bottom aligned" pins the text to the bottom of an explicitly sized control (its
                // internal layout binds to ActualHeight). Below the cover art the text block has to
                // size itself to its content instead, which is exactly what the other mode does.
                this.PlaybackInfo.IsBottomAligned = false;
                this.PlaybackInfo.BeginAnimation(HeightProperty, null);
                this.PlaybackInfo.Height = double.NaN;
            }
            else
            {
                this.PlaybackInfo.IsBottomAligned = true;

                // The side-by-side storyboards animate the height from its current value, and a
                // DoubleAnimation cannot start from NaN (which is what the stacked layout leaves
                // behind). Restore a finite height first, so the animation has a valid origin.
                if (double.IsNaN(this.PlaybackInfo.Height))
                {
                    this.PlaybackInfo.Height = Math.Max(0, this.GetSideBySideCoverArtSize(this.ActualHeight) - 20);
                }
            }
        }

        private void ApplyStackedLayout(double width, double height)
        {
            double coverArtSize = ResponsiveLayout.Clamp(
                Math.Min(width * StackedCoverArtWidthRatio,
                         (height - StackedVerticalReserve) / StackedCoverArtToHeightFactor),
                MinimumStackedCoverArtSize,
                MaximumStackedCoverArtSize);

            this.Animate(CoverArt, WidthProperty, coverArtSize);

            // Below the cover art there is no room for the very large side-by-side typography.
            this.Animate(this.PlaybackInfo, PlaybackInfoControl.TitleFontSizeProperty, ResponsiveLayout.Clamp(coverArtSize * 0.13, 26, 84));
            this.Animate(this.PlaybackInfo, PlaybackInfoControl.ArtistFontSizeProperty, coverArtSize * 0.062);
            this.Animate(this.PlaybackInfo, PlaybackInfoControl.AlbumFontSizeProperty, coverArtSize * 0.062);
            this.Animate(this.PlaybackInfo, PlaybackInfoControl.QualityFontSizeProperty, coverArtSize * 0.032);
            this.Animate(this.PlaybackInfo, PlaybackInfoControl.RatingFontSizeProperty, coverArtSize * 0.05);
            this.Animate(this.PlaybackInfo, PlaybackInfoControl.LoveFontSizeProperty, coverArtSize * 0.053);
        }

        private void Animate(IAnimatable target, DependencyProperty property, double value)
        {
            var animation = new DoubleAnimation(value, new Duration(TimeSpan.FromSeconds(AnimationDurationSeconds)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            target.BeginAnimation(property, animation);
        }

        private void ResizePlaybackInfo()
        {
            Storyboard resizeCoverArtStoryboard = null;

            if (this.ActualHeight < CoverArtToHeightFactor * MinimumCoverArtSize)
            {
                resizeCoverArtStoryboard = this.PlaybackInfoPanel.Resources["ResizeCoverArt250"] as Storyboard;
            }
            else
            {
                for (int index = MinimumCoverArtSize; index <= MaximumCoverArtSize; index += 50)
                {
                    if (this.ActualHeight >= CoverArtToHeightFactor * index)
                    {
                        resizeCoverArtStoryboard = this.PlaybackInfoPanel.Resources[string.Format("ResizeCoverArt{0}", index)] as Storyboard;
                    }
                }
            }

            if (resizeCoverArtStoryboard != null)
            {
                resizeCoverArtStoryboard.Begin();
            }
        }
    }
}
