using Digimezzo.Foundation.Core.Settings;
using Dopamine.Core.Base;
using Dopamine.Services.Shell;
using Dopamine.Utils;
using Dopamine.Views.Common.Base;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Dopamine.Views.Common
{
    /// <summary>
    /// Touch optimization: the button in the mini player control bar which expands/collapses the panel
    /// with the window buttons (switch player / minimize / close). It is only shown when touch
    /// optimization is enabled: in mouse mode those buttons are revealed by hovering the mini player.
    /// </summary>
    public partial class MiniPlayerTouchWindowControlsButton : UserControl
    {
        private bool isSubscribed;

        /// <summary>
        /// Size of the button. Each player sets this to the size of the buttons next to it, so the
        /// control row keeps a uniform look.
        /// </summary>
        public static readonly DependencyProperty ButtonSizeProperty =
            DependencyProperty.Register(nameof(ButtonSize), typeof(double), typeof(MiniPlayerTouchWindowControlsButton), new PropertyMetadata(30.0));

        /// <summary>Font size of the chevron icon.</summary>
        public static readonly DependencyProperty IconFontSizeProperty =
            DependencyProperty.Register(nameof(IconFontSize), typeof(double), typeof(MiniPlayerTouchWindowControlsButton), new PropertyMetadata(16.0));

        public double ButtonSize
        {
            get { return (double)GetValue(ButtonSizeProperty); }
            set { SetValue(ButtonSizeProperty, value); }
        }

        public double IconFontSize
        {
            get { return (double)GetValue(IconFontSizeProperty); }
            set { SetValue(IconFontSizeProperty, value); }
        }

        public MiniPlayerTouchWindowControlsButton()
        {
            InitializeComponent();

            // Set the initial state right away, so the control bar is laid out correctly (the button
            // takes no space in mouse mode).
            this.UpdateVisibility();

            this.Loaded += this.MiniPlayerTouchWindowControlsButton_Loaded;
            this.Unloaded += this.MiniPlayerTouchWindowControlsButton_Unloaded;
        }

        private void MiniPlayerTouchWindowControlsButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.isSubscribed)
            {
                return;
            }

            this.isSubscribed = true;

            MiniPlayerTouchPanelManager.ExpansionChanged += this.MiniPlayerTouchPanelManager_ExpansionChanged;
            SettingsClient.SettingChanged += this.SettingsClient_SettingChanged;

            this.UpdateVisibility();
            this.UpdateCheckedState();
        }

        private void MiniPlayerTouchWindowControlsButton_Unloaded(object sender, RoutedEventArgs e)
        {
            if (!this.isSubscribed)
            {
                return;
            }

            this.isSubscribed = false;

            MiniPlayerTouchPanelManager.ExpansionChanged -= this.MiniPlayerTouchPanelManager_ExpansionChanged;
            SettingsClient.SettingChanged -= this.SettingsClient_SettingChanged;
        }

        private void MiniPlayerTouchPanelManager_ExpansionChanged(object sender, EventArgs e)
        {
            this.UpdateCheckedState();
        }

        private void SettingsClient_SettingChanged(object sender, SettingChangedEventArgs e)
        {
            if (!SettingsClient.IsSettingChanged(e, "Features", "TouchOptimization"))
            {
                return;
            }

            if (!TouchOptimization.IsEnabled)
            {
                // Collapse first, so the window returns to its original geometry.
                MiniPlayerTouchPanelManager.SetExpanded(false, 0);
            }

            this.UpdateVisibility();
            this.UpdateCheckedState();
        }

        private void UpdateVisibility()
        {
            this.Visibility = TouchOptimization.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateCheckedState()
        {
            this.Button.IsChecked = MiniPlayerTouchPanelManager.IsExpanded;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // SetExpanded reports whether the requested state could be applied, which is not the same
            // as the resulting state: collapsing returns true, and using that value re-checked the
            // button, so it could never expand again. Always re-read the manager state.
            MiniPlayerTouchPanelManager.SetExpanded(this.Button.IsChecked == true, this.GetPanelWidth());
            this.Button.IsChecked = MiniPlayerTouchPanelManager.IsExpanded;
        }

        /// <summary>
        /// Width added to the window by the panel of the mini player this button belongs to (the Nano
        /// Player uses a wider, horizontal panel).
        /// </summary>
        private double GetPanelWidth()
        {
            DependencyObject current = this;

            while (current != null)
            {
                var player = current as MiniPlayerViewBase;

                if (player != null)
                {
                    return player.TouchWindowControlsPanelWidth;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return Constants.MiniPlayerTouchPanelWidth;
        }
    }
}
