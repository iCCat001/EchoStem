using Dopamine.Core.Base;
using Dopamine.Utils;
using Dopamine.Views.Common.Base;
using System.Windows;

namespace Dopamine.Views.MiniPlayer
{
    public partial class NanoPlayer : MiniPlayerViewBase
    {
        public NanoPlayer()
        {
            InitializeComponent();
        }

        protected override void UpdateTouchWindowControlsVisibility()
        {
            // Touch optimization: the window buttons which are revealed by hovering the Nano Player are
            // useless on a touch screen. Hide them completely, so they can't be tapped by accident.
            bool touchOptimization = TouchOptimization.IsEnabled;

            this.PlaybackInfoControls.Visibility = touchOptimization ? Visibility.Collapsed : Visibility.Visible;
            this.PlaybackInfoControls.IsHitTestVisible = !touchOptimization;
        }

        protected override void UpdateTouchWindowControlsPanelLayout(bool isExpanded)
        {
            // The Nano Player is only 48 px high: its panel lays the buttons out horizontally, so the
            // window only grows to the left.
            this.TouchWindowControlsPanelColumn.Width = new GridLength(isExpanded ? Constants.MiniPlayerTouchNanoPanelWidth : 0);
            this.TouchWindowControlsPanel.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
        }

        public override double TouchWindowControlsPanelWidth
        {
            get { return Constants.MiniPlayerTouchNanoPanelWidth; }
        }
    }
}
