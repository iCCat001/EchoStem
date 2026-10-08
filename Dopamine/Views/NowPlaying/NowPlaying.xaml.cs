using Digimezzo.Foundation.Core.Settings;
using System;
using System.Timers;
using System.Windows;
using System.Windows.Controls;

namespace Dopamine.Views.NowPlaying
{
    public partial class NowPlaying : UserControl
    {
        private Timer hideControlsTimer = new Timer();
        private bool touchOptimization;
        private bool isSubscribed;

        public bool CanShowControls
        {
            get { return Convert.ToBoolean(GetValue(CanShowControlsProperty)); }
            set { SetValue(CanShowControlsProperty, value); }
        }

        public static readonly DependencyProperty CanShowControlsProperty =
            DependencyProperty.Register(nameof(CanShowControls), typeof(bool), typeof(NowPlaying), new PropertyMetadata(null));

        public NowPlaying()
        {
            InitializeComponent();

            this.hideControlsTimer.Interval = 2000;
            this.hideControlsTimer.Elapsed += new ElapsedEventHandler(this.CleanupNowPlayingHandler);

            // Subscribe when loaded and release the subscriptions when unloaded, so repeated
            // navigations don't leak this view.
            this.Loaded += this.LoadedHandler;
            this.Unloaded += this.UnloadedHandler;

            this.UpdateTouchOptimization();
            this.ShowControls();
        }

        private void LoadedHandler(object sender, RoutedEventArgs e)
        {
            if (this.isSubscribed)
            {
                return;
            }

            this.isSubscribed = true;

            SettingsClient.SettingChanged += this.SettingChangedHandler;

            this.UpdateTouchOptimization();
            this.ShowControls();
        }

        private void UnloadedHandler(object sender, RoutedEventArgs e)
        {
            if (!this.isSubscribed)
            {
                return;
            }

            this.isSubscribed = false;

            SettingsClient.SettingChanged -= this.SettingChangedHandler;
            this.hideControlsTimer.Stop();
        }

        private void SettingChangedHandler(object sender, SettingChangedEventArgs e)
        {
            if (SettingsClient.IsSettingChanged(e, "Features", "TouchOptimization"))
            {
                this.UpdateTouchOptimization();
                this.ShowControls();
            }
        }

        private void UpdateTouchOptimization()
        {
            this.touchOptimization = Utils.TouchOptimization.IsEnabled;
        }

        private void ShowControls()
        {
            this.hideControlsTimer.Stop();
            this.CanShowControls = true;

            // With touch optimization, the controls must stay visible: on a touch screen they can't
            // be revealed by moving a mouse pointer into the control area.
            if (!this.touchOptimization)
            {
                this.hideControlsTimer.Start();
            }
        }

        public void CleanupNowPlayingHandler(object sender, ElapsedEventArgs e)
        {
            if (this.touchOptimization)
            {
                return;
            }

            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!this.BackButton.IsMouseOver)
                {
                    this.CanShowControls = false;
                }
            }));
        }

        private void NowPlaying_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            this.ShowControls();
        }

        private void SpectrumAnalyzer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.AlignSpectrumAnalyzer();
        }

        private void NowPlaying_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.AlignSpectrumAnalyzer();
            this.AlignBackgroundCoverArt();
        }

        private void AlignSpectrumAnalyzer()
        {
            // This makes sure the spectrum analyzer is centered on the screen, based on the left pixel.
            // When we align center, alignment is sometimes (depending on the width of the screen) done
            // on a half pixel. This causes a blurry spectrum analyzer.
            try
            {
                this.SpectrumAnalyzer.Margin = new Thickness(Convert.ToInt32(this.ActualWidth / 2) - Convert.ToInt32(this.SpectrumAnalyzer.ActualWidth / 2), 0, 0, 0);
            }
            catch (Exception)
            {
                // Swallow this exception
            }
        }

        private void AlignBackgroundCoverArt()
        {
            try
            {
                this.BackgroundCoverArtControl.Margin = new Thickness(0, -Convert.ToInt32(this.ActualHeight / 2), 0, 0);
            }
            catch (Exception)
            {
                // Swallow this exception
            }
        }
    }
}
