using Digimezzo.Foundation.Core.Settings;
using Dopamine.Utils;
using System.Windows;
using System.Windows.Controls;

namespace Dopamine.Views.Common
{
    public partial class ProgressControls : UserControl
    {
        private bool isSubscribed;

        public ProgressControls()
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

            TouchOptimization.UpdateProgressThumb(this.ProgressSlider);
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
                TouchOptimization.UpdateProgressThumb(this.ProgressSlider);
            }
        }
    }
}
