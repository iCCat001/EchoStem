using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Settings;
using Digimezzo.Foundation.Core.Utils;
using Dopamine.Core.Prism;
using Dopamine.Services.Appearance;
using Dopamine.Services.Cache;
using Dopamine.Services.Metadata;
using Dopamine.Services.Playback;
using Dopamine.Services.Utils;
using Prism.Events;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Dopamine.ViewModels.Common
{
    public class BackgroundCoverArtControlViewModel : CoverArtControlViewModel
    {
        private IAppearanceService appearanceService;
        private ICacheService cacheService;
        private IMetadataService metadataService;
        private IEventAggregator eventAggregator;

        public BackgroundCoverArtControlViewModel(IPlaybackService playbackService,
            ICacheService cacheService, IAppearanceService appearanceService, 
            IMetadataService metadataService, IEventAggregator eventAggregator) : base(playbackService, cacheService, metadataService)
        {
            this.playbackService = playbackService;
            this.appearanceService = appearanceService;
            this.cacheService = cacheService;
            this.metadataService = metadataService;
            this.eventAggregator = eventAggregator;

            // The dominant color depends on the artwork, which is set by the base class.
            this.PropertyChanged += this.BackgroundCoverArtControlViewModel_PropertyChanged;

            // Recompute when the gradient gets enabled or disabled.
            SettingsClient.SettingChanged += (_, e) =>
            {
                if (SettingsClient.IsSettingChanged(e, "Appearance", "NowPlayingGradient"))
                {
                    this.UpdateDominantColor();
                }
            };
        }

        /// <summary>
        /// The background is blurred and heavily masked, so it can be updated later than the
        /// foreground cover art: this keeps its (expensive) render work from overlapping with the
        /// foreground cover and text transitions.
        /// </summary>
        protected override int CoverArtLoadDelayMilliseconds => 800;

        private void BackgroundCoverArtControlViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(this.CoverArtViewModel))
            {
                this.UpdateDominantColor();
            }
        }

        private void UpdateDominantColor()
        {
            byte[] artwork = this.CoverArtViewModel?.CoverArt;

            if (artwork == null || artwork.Length == 0)
            {
                this.PublishDominantColor(Colors.Transparent);
                return;
            }

            // The color is only used by the gradient: don't decode the artwork when it is off.
            if (!SettingsClient.Get<bool>("Appearance", "NowPlayingGradient"))
            {
                this.PublishDominantColor(Colors.Transparent);
                return;
            }

            Task.Run(() =>
            {
                Color color = Colors.Transparent;

                try
                {
                    color = DominantColorCache.Get(artwork);
                }
                catch (Exception ex)
                {
                    LogClient.Error("Could not get the dominant color of the background cover art. Exception: {0}", ex.Message);
                }

                Application.Current?.Dispatcher.Invoke(() => this.PublishDominantColor(color));
            });
        }

        private void PublishDominantColor(Color color)
        {
            this.eventAggregator.GetEvent<NowPlayingCoverColorChanged>().Publish(color);
        }
    }
}
