using Dopamine.Core.Base;
using Dopamine.Utils;
using Dopamine.Views.Common.Base;
using System;
using System.Windows;
using System.Windows.Input;


namespace Dopamine.Views.MiniPlayer
{
    public partial class MicroPlayer : MiniPlayerViewBase
    {
        public MicroPlayer()
        {
            InitializeComponent();

            // Touch optimization: the cover art does not drag the window (it keeps Windows' own
            // press and hold / right click for its menu), the rest of the player drags the window and
            // ignores right clicks.
            this.EnableTouchDragArea(this.PlayerPanel, this.CoverGrid);
            this.LimitContextMenuToTouchTarget(this.CoverGrid);
        }

        private void CoverGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // On a touch screen the cover art responds to a long press, not to a drag.
            if (TouchOptimization.IsEnabled)
            {
                return;
            }

            this.MouseLeftButtonDownHandler(sender, e);
        }

        private void AlignSpectrumAnalyzer()
        {
            // This makes sure the spectrum analyzer is centered on the screen, based on the left pixel.
            // When we align center, alignment is sometimes (depending on the width of the screen) done
            // on a half pixel. This causes a blurry spectrum analyzer.
            try
            {
                // When the spectrum container is collapsed (e.g. right after loading), its width is 0.
                // Centering with a 0 width would push it to the right, so skip until it has been measured
                // (the SizeChanged handler re-aligns it).
                if (this.SpectrumAnalyzer.ActualWidth <= 0 || this.ControlsPanel.ActualWidth <= 0)
                {
                    return;
                }

                this.SpectrumAnalyzer.Margin = new Thickness(Convert.ToInt32(this.ControlsPanel.ActualWidth / 2) - Convert.ToInt32(this.SpectrumAnalyzer.ActualWidth / 2), 0, 0, 0);
            }
            catch (Exception)
            {
                // Swallow this exception
            }
        }

        private void SpectrumAnalyzer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.AlignSpectrumAnalyzer();
        }

        private void CommonMiniPlayerView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            this.AlignSpectrumAnalyzer();
        }

        protected override void UpdateTouchWindowControlsVisibility()
        {
            // Touch optimization: the window buttons which are revealed by hovering the cover art are
            // useless on a touch screen. Hide them completely, so they can't be tapped by accident.
            bool touchOptimization = TouchOptimization.IsEnabled;

            this.CoverControls.Visibility = touchOptimization ? Visibility.Collapsed : Visibility.Visible;
            this.CoverControls.IsHitTestVisible = !touchOptimization;
        }

        protected override void UpdateTouchWindowControlsPanelLayout(bool isExpanded)
        {
            this.TouchWindowControlsPanelColumn.Width = new GridLength(isExpanded ? Constants.MiniPlayerTouchPanelWidth : 0);
            this.TouchWindowControlsPanel.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
