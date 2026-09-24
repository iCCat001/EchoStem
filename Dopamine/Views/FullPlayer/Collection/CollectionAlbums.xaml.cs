using Dopamine.Views.Common.Base;
using Dopamine.Core.Prism;
using Dopamine.Services.Entities;
using Digimezzo.Foundation.WPF.Controls;
using Prism.Commands;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Dopamine.Views.FullPlayer.Collection
{
    public partial class CollectionAlbums : TracksViewBase
    {
        // The details pane takes this fraction of the page width. It is also the
        // distance the album wall is pushed to the left while the pane is open.
        private const double TracksPaneWidthRatio = 0.35;

        private static readonly Duration SlideDuration = new Duration(TimeSpan.FromMilliseconds(380));
        private const double DimmedOpacity = 0.8;
        private const double FloatingCoverClosedScale = 0.9;

        private bool isTracksPaneOpen;
        private double tracksPaneWidth;

        // Used to ignore the 2nd click of a double click when playing on single click.
        private string lastPlayedTrackPath;
        private DateTime lastPlayedTrackTime = DateTime.MinValue;

        // Used to tell a tap from a drag (mouse or touch), so scrolling never starts playback.
        private Point tracksPressPosition;
        private bool isTracksPressed;

        // Keeps the last shown cover so it fades out together with its border when the
        // pane closes (binding straight to SelectedItem would clear the image instantly).
        public static readonly DependencyProperty FloatingCoverArtworkPathProperty =
            DependencyProperty.Register(nameof(FloatingCoverArtworkPath), typeof(string), typeof(CollectionAlbums), new PropertyMetadata(null));

        public string FloatingCoverArtworkPath
        {
            get { return (string)GetValue(FloatingCoverArtworkPathProperty); }
            set { SetValue(FloatingCoverArtworkPathProperty, value); }
        }

        public CollectionAlbums() : base()
        {
            InitializeComponent();

            // Commands
            this.ViewInExplorerCommand = new DelegateCommand(() => this.ViewInExplorer(this.ListBoxTracks));
            this.JumpToPlayingTrackCommand = new DelegateCommand(async () => await this.ScrollToPlayingTrackAsync(this.ListBoxTracks));

            // PubSub Events
            this.eventAggregator.GetEvent<ScrollToPlayingTrack>().Subscribe(async (_) => await this.ScrollToPlayingTrackAsync(this.ListBoxTracks));
        }

        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            this.UpdateTracksPaneWidth();
            this.SetTracksPaneOpen(this.ListBoxAlbums.SelectedItems.Count > 0, false);
        }

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Only reposition when the pane width actually changed, so a layout pass
            // cannot cut a running animation short.
            if (!this.UpdateTracksPaneWidth())
            {
                return;
            }

            this.SetTracksPaneOpen(this.isTracksPaneOpen, false);
        }

        private void ListBoxAlbums_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool open = this.ListBoxAlbums.SelectedItems.Count > 0;

            if (open)
            {
                // Capture the cover now, so it is kept while the pane slides out.
                this.FloatingCoverArtworkPath = (this.ListBoxAlbums.SelectedItem as AlbumViewModel)?.ArtworkPath;
            }

            this.SetTracksPaneOpen(open, true);
        }

        private void AlbumsDimOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Clicking the dimmed album wall closes the details pane.
            this.ListBoxAlbums.UnselectAll();
            e.Handled = true;
        }

        private bool UpdateTracksPaneWidth()
        {
            double width = this.RootGrid.ActualWidth * TracksPaneWidthRatio;

            if (width <= 0 || Math.Abs(width - this.tracksPaneWidth) < 0.5)
            {
                return false;
            }

            this.tracksPaneWidth = width;
            this.TracksPane.Width = width;

            return true;
        }

        private void SetTracksPaneOpen(bool open, bool animate)
        {
            this.isTracksPaneOpen = open;

            double width = this.tracksPaneWidth > 0
                ? this.tracksPaneWidth
                : this.RootGrid.ActualWidth * TracksPaneWidthRatio;

            if (double.IsNaN(width) || width <= 0)
            {
                // The pane has not been laid out yet. The next layout pass will call this again.
                return;
            }

            double albumsTo = open ? -width : 0;
            double tracksTo = open ? 0 : width;
            double dimTo = open ? DimmedOpacity : 0;
            // The floating cover is centered in the visible part of the album wall
            // (the wall is shifted left by "width").
            double coverTo = open ? -width / 2 : 0;
            double coverScaleTo = open ? 1 : FloatingCoverClosedScale;
            double coverOpacityTo = open ? 1 : 0;

            // The cover would swallow clicks meant for the dimming overlay while it is
            // fading out, so only make the overlay hit-testable during the animation.
            this.AlbumsDimOverlay.IsHitTestVisible = open;

            if (animate)
            {
                AnimateDouble(this.AlbumsPaneTranslate, TranslateTransform.XProperty, albumsTo);
                AnimateDouble(this.TracksPaneTranslate, TranslateTransform.XProperty, tracksTo);
                AnimateDouble(this.AlbumsDimOverlay, UIElement.OpacityProperty, dimTo);
                AnimateDouble(this.FloatingCoverTranslate, TranslateTransform.XProperty, coverTo);
                AnimateDouble(this.FloatingCoverScale, ScaleTransform.ScaleXProperty, coverScaleTo);
                AnimateDouble(this.FloatingCoverScale, ScaleTransform.ScaleYProperty, coverScaleTo);
                AnimateDouble(this.FloatingCover, UIElement.OpacityProperty, coverOpacityTo);
            }
            else
            {
                this.AlbumsPaneTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                this.TracksPaneTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                this.AlbumsDimOverlay.BeginAnimation(UIElement.OpacityProperty, null);
                this.FloatingCoverTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                this.FloatingCoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                this.FloatingCoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                this.FloatingCover.BeginAnimation(UIElement.OpacityProperty, null);

                this.AlbumsPaneTranslate.X = albumsTo;
                this.TracksPaneTranslate.X = tracksTo;
                this.AlbumsDimOverlay.Opacity = dimTo;
                this.FloatingCoverTranslate.X = coverTo;
                this.FloatingCoverScale.ScaleX = coverScaleTo;
                this.FloatingCoverScale.ScaleY = coverScaleTo;
                this.FloatingCover.Opacity = coverOpacityTo;
            }
        }

        private static void AnimateDouble(IAnimatable target, DependencyProperty property, double to)
        {
            var animation = new DoubleAnimation
            {
                To = to,
                Duration = SlideDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            target.BeginAnimation(property, animation);
        }

        private async void ListBoxAlbums_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            await this.ActionHandler(sender, e.OriginalSource as DependencyObject, true);
        }

        private async void ListBoxAlbums_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await this.ActionHandler(sender, e.OriginalSource as DependencyObject, true);
            }
        }

        private void ListBoxTracks_Loaded(object sender, RoutedEventArgs e)
        {
            // Enable touch panning on the internal scroll viewer so a touch drag scrolls the
            // list (and is not promoted to a click that would start playback). PanningMode is
            // not an inherited property, so it has to be set on the ScrollViewer itself.
            ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>(this.ListBoxTracks);

            if (scrollViewer != null)
            {
                scrollViewer.PanningMode = PanningMode.VerticalOnly;
            }
        }

        private void ListBoxTracks_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.tracksPressPosition = e.GetPosition(this.ListBoxTracks);
            this.isTracksPressed = true;
        }

        private async void ListBoxTracks_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            bool wasPressed = this.isTracksPressed;
            this.isTracksPressed = false;

            // Only start playing on a plain left click / touch tap: keep Ctrl/Shift for
            // multi-select, ignore clicks on controls inside the row (rating, love, scroll
            // bar, ...) and ignore drags (used to scroll the list).
            if (!wasPressed || Keyboard.Modifiers != ModifierKeys.None || IsClickOnButton(e.OriginalSource as DependencyObject))
            {
                return;
            }

            Point releasePosition = e.GetPosition(this.ListBoxTracks);

            if (Math.Abs(releasePosition.X - this.tracksPressPosition.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(releasePosition.Y - this.tracksPressPosition.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                // The pointer moved: this is a drag (scrolling), not a tap.
                return;
            }

            TrackViewModel track = this.ListBoxTracks.SelectedItem as TrackViewModel;

            if (track == null)
            {
                return;
            }

            // Ignore the repeating click of a double click, which would enqueue twice.
            if (this.lastPlayedTrackPath == track.SafePath && (DateTime.Now - this.lastPlayedTrackTime).TotalMilliseconds < 500)
            {
                return;
            }

            this.lastPlayedTrackPath = track.SafePath;
            this.lastPlayedTrackTime = DateTime.Now;

            await this.ActionHandler(sender, e.OriginalSource as DependencyObject, true);
        }

        private static bool IsClickOnButton(DependencyObject source)
        {
            while (source != null && !(source is MultiSelectListBox.MultiSelectListBoxItem))
            {
                if (source is ButtonBase)
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            int count = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                {
                    return typedChild;
                }

                T result = FindVisualChild<T>(child);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private async void ListBoxTracks_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await this.ActionHandler(sender, e.OriginalSource as DependencyObject, true);
            }
        }

        private async void ListBoxTracks_KeyUp(object sender, KeyEventArgs e)
        {
            await this.KeyUpHandlerAsync(sender, e);
        }

        private void AlbumsButton_Click(object sender, RoutedEventArgs e)
        {
            this.ListBoxAlbums.UnselectAll();
        }
    }
}
