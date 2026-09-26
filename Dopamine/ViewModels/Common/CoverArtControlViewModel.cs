using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.WPF.Controls;
using Dopamine.Data.Entities;
using Dopamine.ViewModels;
using Dopamine.Services.Cache;
using Dopamine.Services.Metadata;
using Dopamine.Services.Playback;
using Prism.Mvvm;
using System;
using System.Threading.Tasks;
using System.Timers;
using Dopamine.Services.Entities;

namespace Dopamine.ViewModels.Common
{
    public class CoverArtControlViewModel : BindableBase
    {
        protected CoverArtViewModel coverArtViewModel;
        protected IPlaybackService playbackService;
        private ICacheService cacheService;
        private IMetadataService metadataService;
        private SlideDirection slideDirection;
        private long? previousArtworkLength;
        private byte[] artwork;

        public CoverArtViewModel CoverArtViewModel
        {
            get { return this.coverArtViewModel; }
            set { SetProperty<CoverArtViewModel>(ref this.coverArtViewModel, value); }
        }

        public SlideDirection SlideDirection
        {
            get { return this.slideDirection; }
            set { SetProperty<SlideDirection>(ref this.slideDirection, value); }
        }

        private void ClearArtwork()
        {
            this.CoverArtViewModel = new CoverArtViewModel { CoverArt = null };
            this.artwork = null;
        }

        public CoverArtControlViewModel(IPlaybackService playbackService, ICacheService cacheService, IMetadataService metadataService)
        {
            this.playbackService = playbackService;
            this.cacheService = cacheService;
            this.metadataService = metadataService;

            this.playbackService.PlaybackSuccess += (_, e) =>
            {
                this.SlideDirection = e.IsPlayingPreviousTrack ? SlideDirection.UpToDown : SlideDirection.DownToUp;
                this.RefreshCoverArtAsync(this.playbackService.CurrentTrack);
            };

            this.playbackService.PlayingTrackChanged += (_, __) => this.RefreshCoverArtAsync(this.playbackService.CurrentTrack);

            // Defaults
            this.SlideDirection = SlideDirection.DownToUp;
            this.RefreshCoverArtAsync(this.playbackService.CurrentTrack);
        }

        private void RefreshTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.RefreshCoverArtAsync(this.playbackService.CurrentTrack);
        }

        protected async virtual void RefreshCoverArtAsync(TrackViewModel track)
        {
            await Task.Delay(250);

            await Task.Run(async () =>
            {
                // Only the length of the previous artwork is remembered (instead of keeping the
                // whole byte array), so a single cover image is held in memory at a time.
                this.previousArtworkLength = this.artwork != null ? this.artwork.LongLength : (long?)null;

                // No track selected: clear cover art.
                if (track == null)
                {
                    this.ClearArtwork();
                    return;
                }

                // Try to find artwork
                byte[] artwork = null;

                try
                {
                    artwork = await this.metadataService.GetArtworkAsync(track.Path);
                }
                catch (Exception ex)
                {
                    LogClient.Error("Could not get artwork for Track {0}. Exception: {1}", track.Path, ex.Message);
                }

                long? artworkLength = artwork != null ? artwork.LongLength : (long?)null;

                // Verify if the artwork changed (by length, as before).
                if (this.previousArtworkLength != null & artworkLength != null && this.previousArtworkLength == artworkLength)
                {
                    return;
                }
                else if (this.previousArtworkLength == null & artworkLength == null & this.CoverArtViewModel != null)
                {
                    return;
                }

                this.artwork = artwork;

                if (artwork != null)
                {
                    try
                    {
                        this.CoverArtViewModel = new CoverArtViewModel { CoverArt = artwork };
                    }
                    catch (Exception ex)
                    {
                        LogClient.Error("Could not show file artwork for Track {0}. Exception: {1}", track.Path, ex.Message);
                        this.ClearArtwork();
                    }

                    return;
                }
                else
                {
                    this.ClearArtwork();
                    return;
                }
            });
        }
    }
}