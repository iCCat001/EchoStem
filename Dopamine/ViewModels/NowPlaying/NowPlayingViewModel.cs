using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Settings;
using Digimezzo.Foundation.WPF.Controls;
using Dopamine.ViewModels.Common.Base;
using Dopamine.Core.Enums;
using Dopamine.Core.Prism;
using Dopamine.Services.Playback;
using Dopamine.Utils;
using Dopamine.Views.NowPlaying;
using Prism.Events;
using Prism.Regions;
using System;
using System.Windows;
using System.Windows.Media;

namespace Dopamine.ViewModels.NowPlaying
{
    public class NowPlayingViewModel : NowPlayingViewModelBase
    {
        // Alpha of the dominant color gradient which is drawn on top of the Now Playing background.
        private const byte GradientColorAlpha = 110;

        private SlideDirection direction;
        private IRegionManager regionManager;
        private double backgroundMaskOpacity;
        private Brush backgroundGradientBrush;
        private Color currentCoverColor = Colors.Transparent;
        private bool useGradient = true;
        private bool isMaterialSyncActive;
        private double materialBlurRadius;

        public SlideDirection Direction
        {
            get { return this.direction; }
            set
            {
                SetProperty<SlideDirection>(ref this.direction, value);
            }
        }

        public double BackgroundMaskOpacity
        {
            get { return this.backgroundMaskOpacity; }
            private set { SetProperty<double>(ref this.backgroundMaskOpacity, value); }
        }

        public Brush BackgroundGradientBrush
        {
            get { return this.backgroundGradientBrush; }
            private set { SetProperty<Brush>(ref this.backgroundGradientBrush, value); }
        }

        /// <summary>
        /// True when the background mask should approximate the window material instead of
        /// using the mask opacity slider.
        /// </summary>
        public bool IsMaterialSyncActive
        {
            get { return this.isMaterialSyncActive; }
            private set { SetProperty<bool>(ref this.isMaterialSyncActive, value); }
        }

        /// <summary>
        /// Blur radius (device independent pixels) of the album background when the material
        /// is approximated. 0 when not active.
        /// </summary>
        public double MaterialBlurRadius
        {
            get { return this.materialBlurRadius; }
            private set { SetProperty<double>(ref this.materialBlurRadius, value); }
        }

        public NowPlayingViewModel(IPlaybackService playbackService, IRegionManager regionManager, IEventAggregator eventAggregator) : base(playbackService)
        {
            this.regionManager = regionManager;

            this.BackgroundGradientBrush = Brushes.Transparent;

            this.ReadBackgroundSettings();

            SettingsClient.SettingChanged += (_, e) =>
            {
                if (SettingsClient.IsSettingChanged(e, "Appearance", "NowPlayingBackgroundOpacity") ||
                    SettingsClient.IsSettingChanged(e, "Appearance", "NowPlayingGradient") ||
                    SettingsClient.IsSettingChanged(e, "Appearance", "NowPlayingMaterialSync") ||
                    SettingsClient.IsSettingChanged(e, "Appearance", "WindowMaterial"))
                {
                    this.ReadBackgroundSettings();
                }
            };

            eventAggregator.GetEvent<IsNowPlayingSubPageChanged>().Subscribe(tuple =>
            {
                this.NagivateToSelectedPage(tuple.Item1, tuple.Item2);
            });

            eventAggregator.GetEvent<NowPlayingCoverColorChanged>().Subscribe(this.OnCoverColorChanged, ThreadOption.UIThread);
        }

        private void OnCoverColorChanged(Color color)
        {
            this.currentCoverColor = color;
            this.UpdateGradientBrush();
        }

        private void UpdateGradientBrush()
        {
            this.BackgroundGradientBrush = this.useGradient
                ? CreateGradientBrush(this.currentCoverColor)
                : Brushes.Transparent;
        }

        private static Brush CreateGradientBrush(Color color)
        {
            if (color.A == 0)
            {
                return Brushes.Transparent;
            }

            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1)
            };

            brush.GradientStops.Add(new GradientStop(Color.FromArgb(GradientColorAlpha, color.R, color.G, color.B), 0.0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 0.85));
            brush.Freeze();

            return brush;
        }

        private void ReadBackgroundSettings()
        {
            int maskOpacity = 88;
            bool useGradient = true;
            bool materialSync = false;

            try
            {
                maskOpacity = SettingsClient.Get<int>("Appearance", "NowPlayingBackgroundOpacity");
                useGradient = SettingsClient.Get<bool>("Appearance", "NowPlayingGradient");
                materialSync = SettingsClient.Get<bool>("Appearance", "NowPlayingMaterialSync");
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not read the Now Playing background settings. Exception: {0}", ex.Message);
            }

            // The mask opacity is limited to 80-96% to keep the text readable.
            maskOpacity = Math.Max(80, Math.Min(96, maskOpacity));

            WindowMaterial material = WindowMaterialHelper.GetSavedMaterial();

            this.IsMaterialSyncActive = materialSync && material != WindowMaterial.Traditional;

            if (this.IsMaterialSyncActive)
            {
                // Approximate Mica/Acrylic: blur the album background and tint it.
                if (material == WindowMaterial.Mica)
                {
                    this.MaterialBlurRadius = 96;
                    this.BackgroundMaskOpacity = 0.85;
                }
                else
                {
                    this.MaterialBlurRadius = 32;
                    this.BackgroundMaskOpacity = 0.70;
                }
            }
            else
            {
                this.MaterialBlurRadius = 0;
                this.BackgroundMaskOpacity = maskOpacity / 100.0;
            }

            this.useGradient = useGradient;

            this.UpdateGradientBrush();
        }

        private void NagivateToSelectedPage(SlideDirection direction, NowPlayingSubPage page)
        {
            this.Direction = direction;
           
            switch (page)
            {
                case NowPlayingSubPage.ArtistInformation:
                    this.regionManager.RequestNavigate(RegionNames.NowPlayingSubPageRegion, typeof(NowPlayingArtistInformation).FullName);
                    break;
                case NowPlayingSubPage.Lyrics:
                    this.regionManager.RequestNavigate(RegionNames.NowPlayingSubPageRegion, typeof(NowPlayingLyrics).FullName);
                    break;
                case NowPlayingSubPage.Playlist:
                    this.regionManager.RequestNavigate(RegionNames.NowPlayingSubPageRegion, typeof(NowPlayingPlaylist).FullName);
                    break;
                case NowPlayingSubPage.ShowCase:
                    this.regionManager.RequestNavigate(RegionNames.NowPlayingSubPageRegion, typeof(NowPlayingShowcase).FullName);
                    break;
                default:
                    break;
            }
        }
    }
}
