using Digimezzo.Foundation.Core.Settings;
using Dopamine.Utils;
using Dopamine.ViewModels.NowPlaying;
using System;
using System.ComponentModel;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Threading.Tasks;

namespace Dopamine.Views.NowPlaying
{
    public partial class NowPlaying : UserControl
    {
        private Timer hideControlsTimer = new Timer();
        private bool touchOptimization;
        private bool isSubscribed;
        private NowPlayingViewModel viewModel;

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
            this.DataContextChanged += this.NowPlaying_DataContextChanged;

            // Prism can set the DataContext while InitializeComponent() runs, in which case
            // DataContextChanged has already fired before we subscribed. Attach explicitly.
            this.AttachViewModel(this.DataContext as NowPlayingViewModel);

            this.UpdateTouchOptimization();
            this.ShowControls();
        }

        private void NowPlaying_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            this.AttachViewModel(e.NewValue as NowPlayingViewModel);
        }

        private void AttachViewModel(NowPlayingViewModel viewModel)
        {
            if (this.viewModel != null)
            {
                this.viewModel.PropertyChanged -= this.ViewModel_PropertyChanged;
            }

            this.viewModel = viewModel;

            if (this.viewModel != null)
            {
                this.viewModel.PropertyChanged += this.ViewModel_PropertyChanged;
            }

            // Apply the initial state (the ViewModel was created before we subscribed).
            this.ScheduleBackgroundMaterialUpdate();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NowPlayingViewModel.BackgroundGradientBrush))
            {
                // Fade the new dominant color in instead of switching instantly.
                this.BackgroundGradientOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.6)));
            }
            else if (e.PropertyName == nameof(NowPlayingViewModel.IsMaterialSyncActive) ||
                     e.PropertyName == nameof(NowPlayingViewModel.MaterialBlurRadius))
            {
                this.UpdateBackgroundMaterial();
            }
        }

        private void UpdateBackgroundMaterial()
        {
            if (this.viewModel == null)
            {
                return;
            }

            this.BackgroundCoverArtControl.SetMaterialBlur(this.viewModel.IsMaterialSyncActive ? this.viewModel.MaterialBlurRadius : 0);
        }

        // The blur is expensive to render. When this page is shown, let its entrance animation
        // finish first, so the two don't compete for the same frames.
        private async void ScheduleBackgroundMaterialUpdate()
        {
            await Task.Delay(350);

            if (!this.IsLoaded)
            {
                return;
            }

            this.UpdateBackgroundMaterial();
        }

        private void LoadedHandler(object sender, RoutedEventArgs e)
        {
            // This page draws its own (opaque) album art background, which fully covers the
            // window: the window backdrop (Mica/Acrylic) is invisible, so don't render it.
            WindowMaterialHelper.SetBackdropSuppressed(true);

            this.ScheduleBackgroundMaterialUpdate();

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
            WindowMaterialHelper.SetBackdropSuppressed(false);

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
                if (this.SpectrumAnalyzer.ActualWidth <= 0 || this.ActualWidth <= 0)
                {
                    return;
                }

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
