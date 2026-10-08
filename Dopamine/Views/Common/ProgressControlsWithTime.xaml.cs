using Digimezzo.Foundation.Core.Settings;
using Dopamine.Utils;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Dopamine.Views.Common
{
    public partial class ProgressControlsWithTime : UserControl
    {
        public static readonly DependencyProperty SliderLengthProperty = DependencyProperty.Register("SliderLength", typeof(double), typeof(ProgressControlsWithTime), new PropertyMetadata(100.0));

        private bool isSubscribed;

        public new object DataContext
        {
            get { return base.DataContext; }
            set { base.DataContext = value; }
        }

        public double SliderLength
        {
            get { return Convert.ToDouble(GetValue(SliderLengthProperty)); }

            set { SetValue(SliderLengthProperty, value); }
        }

        public ProgressControlsWithTime()
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
