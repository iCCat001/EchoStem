using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.WPF.Controls;
using Dopamine.Core.Prism;
using Dopamine.Services.Entities;
using Dopamine.Services.Utils;
using Dopamine.ViewModels.FullPlayer.Collection;
using Dopamine.Views.Common.Base;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Dopamine.Views.FullPlayer.Collection
{
    public partial class CollectionArtists : TracksViewBase
    {
        // The hidden tracks pane takes 35% of the page width. The artists and albums columns (3:7)
        // share the rest and are squeezed together when the tracks pane opens.
        private const double TracksPaneWidthRatio = 0.35;

        private static readonly Duration SlideDuration = new Duration(TimeSpan.FromMilliseconds(380));
        private const double DimmedOpacity = 0.8;
        private const double FloatingCoverClosedScale = 0.9;

        private bool isTracksPaneOpen;
        private double tracksPaneWidth;

        // True while the album wall is handling a right click: the selection it causes must not open
        // the track panel (the context menu needs the wall to stay visible).
        private bool suppressSelectionExpansion;

        // Used to ignore the 2nd click of a double click when playing on single click.
        private string lastPlayedTrackPath;
        private DateTime lastPlayedTrackTime = DateTime.MinValue;

        // Used to tell a tap from a drag (mouse or touch), so scrolling never starts playback.
        private Point tracksPressPosition;
        private bool isTracksPressed;

        // Keeps the last shown cover so it can be shown while the tracks pane slides in/out.
        public static readonly DependencyProperty FloatingCoverArtworkPathProperty =
            DependencyProperty.Register(nameof(FloatingCoverArtworkPath), typeof(string), typeof(CollectionArtists), new PropertyMetadata(null));

        public string FloatingCoverArtworkPath
        {
            get { return (string)GetValue(FloatingCoverArtworkPathProperty); }
            set { SetValue(FloatingCoverArtworkPathProperty, value); }
        }

        public CollectionArtists() : base()
        {
            InitializeComponent();

            // Commands
            this.ViewInExplorerCommand = new DelegateCommand(() => this.ViewInExplorer(this.ListBoxTracks));
            this.JumpToPlayingTrackCommand = new DelegateCommand(async () => await this.ScrollToPlayingTrackAsync(this.ListBoxTracks));

            // PubSub Events
            this.eventAggregator.GetEvent<ScrollToPlayingTrack>().Subscribe(async (_) => await this.ScrollToPlayingTrackAsync(this.ListBoxTracks));

            // A right click must not open the track panel.
            this.ListBoxAlbums.PreviewMouseRightButtonDown += (sender, e) => this.suppressSelectionExpansion = true;
            this.ListBoxAlbums.PreviewMouseRightButtonUp += (sender, e) => this.suppressSelectionExpansion = false;

            // Clicking an album which is already selected does not raise SelectionChanged (this
            // happens after a right click selected it), so the pane has to be opened explicitly.
            this.ListBoxAlbums.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent,
                new MouseButtonEventHandler(this.ListBoxAlbums_PreviewMouseLeftButtonUp), true);

            // A right click selects the album for its context menu but does not open the pane. Such a
            // selection must not survive a page change: when the page is shown again, the selection
            // would look like a normal selection and open the pane by itself.
            this.ListBoxAlbums.Unloaded += (sender, e) =>
            {
                if (!this.isTracksPaneOpen)
                {
                    this.ListBoxAlbums.UnselectAll();
                }
            };

            this.eventAggregator.GetEvent<PerformSemanticJump>().Subscribe(async (data) =>
            {
                try
                {
                    if (data.Item1.Equals("Artists"))
                    {
                        await SemanticZoomUtils.SemanticScrollAsync(this.ListBoxArtists, data.Item2);
                    }
                }
                catch (Exception ex)
                {
                    LogClient.Error("Could not perform semantic zoom on Artists. Exception: {0}", ex.Message);
                }
            });
        }

        private void ListBoxAlbums_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (this.isTracksPaneOpen || this.ListBoxAlbums.SelectedItems.Count != 1)
            {
                return;
            }

            var item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);

            if (item == null || !object.ReferenceEquals(item.DataContext, this.ListBoxAlbums.SelectedItem))
            {
                return;
            }

            // The clicked album was already the selected one: nothing changed in the selection, so
            // open its tracks pane here.
            this.SetTracksPaneOpen(true, true);
        }

        private static T FindAncestor<T>(DependencyObject element) where T : DependencyObject
        {
            DependencyObject current = element;

            while (current != null)
            {
                var typed = current as T;

                if (typed != null)
                {
                    return typed;
                }

                current = current is Visual || current is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current)
                    : LogicalTreeHelper.GetParent(current);
            }

            return null;
        }

        private async void ListBoxArtists_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            await this.ActionHandler(sender, e.OriginalSource as DependencyObject, true);
        }

        private async void ListBoxArtists_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await this.ActionHandler(sender, e.OriginalSource as DependencyObject, true);
            }
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
            // Enable touch panning on the internal scroll viewer so a touch drag scrolls the list
            // (and is not promoted to a click that would start playback).
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
            // multi-select, ignore clicks on controls inside the row (rating, love, scroll bar, ...)
            // and ignore drags (used to scroll the list).
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

        private async void ListBoxTracks_KeyUp(object sender, KeyEventArgs e)
        {
            await this.KeyUpHandlerAsync(sender, e);
        }

        private async void ListBoxTracks_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await this.ActionHandler(sender, e.OriginalSource as DependencyObject, true);
            }
        }

        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            this.UpdatePaneWidths();
            this.SetTracksPaneOpen(false, false);

            // A reload of the artist list discards the selection. Restore it once (jump to the
            // last selected artist and select it again) when the page is shown again, or right
            // after a reload which happened while the page was visible.
            if (this.DataContext is CollectionArtistsViewModel artistsViewModel)
            {
                artistsViewModel.ListsFilled -= this.ArtistsViewModel_ListsFilled;
                artistsViewModel.ListsFilled += this.ArtistsViewModel_ListsFilled;

                if (artistsViewModel.ConsumeListsRestoreRequest())
                {
                    this.RestoreArtistSelectionDeferred();
                }
            }
        }

        private void RootGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is CollectionArtistsViewModel artistsViewModel)
            {
                artistsViewModel.ListsFilled -= this.ArtistsViewModel_ListsFilled;
            }
        }

        private void ArtistsViewModel_ListsFilled(object sender, EventArgs e)
        {
            this.RestoreArtistSelectionDeferred();
        }

        // Waits until the new artist items are bound and laid out, then restores the selection.
        private void RestoreArtistSelectionDeferred()
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                this.ListBoxArtists.UpdateLayout();
                this.RestoreArtistSelection();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // Selects the artist(s) which were selected before the artist list got reloaded, and
        // scrolls to the first one. Selecting triggers the normal selection handler, so the
        // albums pane is filled again as if the artist had been clicked.
        private void RestoreArtistSelection()
        {
            if (!(this.DataContext is CollectionArtistsViewModel artistsViewModel))
            {
                return;
            }

            if (artistsViewModel.SelectedArtists == null || artistsViewModel.SelectedArtists.Count == 0)
            {
                return;
            }

            var artistsToSelect = new List<ArtistViewModel>();

            foreach (object item in this.ListBoxArtists.Items)
            {
                if (item is ArtistViewModel artist && artistsViewModel.SelectedArtists.Contains(artist.ArtistName))
                {
                    artistsToSelect.Add(artist);
                }
            }

            if (artistsToSelect.Count == 0)
            {
                return;
            }

            this.ListBoxArtists.UnselectAll();

            foreach (ArtistViewModel artist in artistsToSelect)
            {
                this.ListBoxArtists.SelectedItems.Add(artist);
            }

            this.ListBoxArtists.UpdateLayout();

            ScrollUtils.ScrollToListBoxItemCenteredVertically(this.ListBoxArtists, artistsToSelect[0]);
        }

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Only reposition when the pane widths actually changed, so a layout pass cannot cut a
            // running animation short.
            if (!this.UpdatePaneWidths())
            {
                return;
            }

            this.SetTracksPaneOpen(this.isTracksPaneOpen, false);
        }

        private void ArtistsButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.ListBoxArtists.SelectedItem == null)
            {
                this.eventAggregator.GetEvent<ToggleArtistOrderCommand>().Publish(null);
            }
            else
            {
                this.ListBoxArtists.SelectedItem = null;
            }
        }

        private void AlbumsButton_Click(object sender, RoutedEventArgs e)
        {
            this.ListBoxAlbums.SelectedItem = null;
        }

        private void ListBoxAlbums_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool open = this.ListBoxAlbums.SelectedItems.Count > 0;

            if (open)
            {
                // Capture the cover now, so it is shown while the tracks pane slides in.
                this.FloatingCoverArtworkPath = (this.ListBoxAlbums.SelectedItem as AlbumViewModel)?.ArtworkPath;
            }

            // A right click selects the album so its context menu acts on it, but it must not open
            // the track panel (the panel and its dim overlay would cover the context menu).
            this.SetTracksPaneOpen(open && !this.suppressSelectionExpansion, true);
        }

        private void AlbumsDimOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Clicking the dimmed album wall closes the tracks pane.
            this.ListBoxAlbums.UnselectAll();
            e.Handled = true;
        }

        private bool UpdatePaneWidths()
        {
            double width = this.RootGrid.ActualWidth;

            if (width <= 0)
            {
                return false;
            }

            double tracks = width * TracksPaneWidthRatio;

            if (Math.Abs(tracks - this.tracksPaneWidth) < 0.5)
            {
                return false;
            }

            this.tracksPaneWidth = tracks;

            // The content panel always spans the full page width; it is only translated.
            this.ContentPane.Width = width;

            return true;
        }

        private void SetTracksPaneOpen(bool open, bool animate)
        {
            this.isTracksPaneOpen = open;

            double width = this.RootGrid.ActualWidth;

            if (width <= 0)
            {
                return;
            }

            double contentTo = open ? -this.tracksPaneWidth : 0;
            double tracksTo = open ? 0 : this.tracksPaneWidth;
            double dimTo = open ? DimmedOpacity : 0;
            double coverScaleTo = open ? 1 : FloatingCoverClosedScale;
            double coverOpacityTo = open ? 1 : 0;

            this.TracksPane.Width = this.tracksPaneWidth;

            // The overlay would swallow clicks meant for the floating cover while it is fading out,
            // so only make it hit-testable while the pane is open.
            this.AlbumsDimOverlay.IsHitTestVisible = open;

            if (animate)
            {
                AnimateDouble(this.ContentPaneTranslate, TranslateTransform.XProperty, contentTo);
                AnimateDouble(this.TracksPaneTranslate, TranslateTransform.XProperty, tracksTo);
                AnimateDouble(this.AlbumsDimOverlay, UIElement.OpacityProperty, dimTo);
                AnimateDouble(this.FloatingCover, UIElement.OpacityProperty, coverOpacityTo);
                AnimateDouble(this.FloatingCoverScale, ScaleTransform.ScaleXProperty, coverScaleTo);
                AnimateDouble(this.FloatingCoverScale, ScaleTransform.ScaleYProperty, coverScaleTo);
            }
            else
            {
                this.ContentPaneTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                this.TracksPaneTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                this.AlbumsDimOverlay.BeginAnimation(UIElement.OpacityProperty, null);
                this.FloatingCover.BeginAnimation(UIElement.OpacityProperty, null);
                this.FloatingCoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                this.FloatingCoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

                this.ContentPaneTranslate.X = contentTo;
                this.TracksPaneTranslate.X = tracksTo;
                this.AlbumsDimOverlay.Opacity = dimTo;
                this.FloatingCover.Opacity = coverOpacityTo;
                this.FloatingCoverScale.ScaleX = coverScaleTo;
                this.FloatingCoverScale.ScaleY = coverScaleTo;
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
    }
}

