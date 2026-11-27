using Digimezzo.Foundation.Core.Settings;
using EchoStem.Utils;
using System.Windows;
using System.Windows.Controls;

namespace EchoStem.Views.Common
{
    public partial class ProgressControlsThin : UserControl
    {
        private bool isSubscribed;

        public ProgressControlsThin()
        {
            InitializeComponent();

            this.Loaded += this.LoadedHandler;
            this.Unloaded += this.UnloadedHandler;
        }

        private void LoadedHandler(object sender, RoutedEventArgs e)
        {
            if (!this.isSubscribed)
            {
                this.isSubscribed = true;
                SettingsClient.SettingChanged += this.SettingChangedHandler;
            }

            TouchOptimization.UpdateProgressThumb(this.ProgressSliderThin);
        }

        private void UnloadedHandler(object sender, RoutedEventArgs e)
        {
            if (this.isSubscribed)
            {
                this.isSubscribed = false;
                SettingsClient.SettingChanged -= this.SettingChangedHandler;
            }
        }

        private void SettingChangedHandler(object sender, SettingChangedEventArgs e)
        {
            if (SettingsClient.IsSettingChanged(e, "Features", "TouchOptimization"))
            {
                TouchOptimization.UpdateProgressThumb(this.ProgressSliderThin);
            }
        }
    }
}
