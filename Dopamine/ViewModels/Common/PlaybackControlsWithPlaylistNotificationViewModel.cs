using Digimezzo.Foundation.Core.Settings;
using Digimezzo.Foundation.Core.Utils;
using Dopamine.Core.Api.Lyrics;
using Dopamine.Core.Enums;
using Dopamine.Core.Prism;
using Dopamine.Services.Blacklist;
using Dopamine.Services.Collection;
using Dopamine.Services.Entities;
using Dopamine.Services.Lyrics;
using Dopamine.Services.Playback;
using Dopamine.Services.Playlist;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Timers;

namespace Dopamine.ViewModels.Common
{
    public class PlaybackControlsWithPlaylistNotificationViewModel : BindableBase
    {
        private readonly IPlaybackService playbackService;
        private readonly IPlaylistService playlistService;
        private readonly IBlacklistService blacklistService;
        private readonly ILyricsService lyricsService;
        private readonly IEventAggregator eventAggregator;

        private string addedTracksToPlaylistText;
        private bool showAddedTracksToPlaylistText;
        private bool isShowingLyric;

        private readonly Timer showAddedTracksToPlaylistTextTimer;
        private readonly int showAddedTracksToPlaylistTextSeconds = 2;

        // When the mouse has left the controls, lyrics resume after this delay.
        private readonly Timer lyricsResumeTimer;
        private readonly int lyricsResumeSeconds = 5;

        // Polls the current lyric line while playing.
        private readonly Timer lyricLineTimer;
        private readonly int lyricLineTimerIntervalMilliseconds = 250;

        private bool suppressLyrics;
        private bool isMessageActive;
        private string currentLyricLine;
        private string displayedLyricLine;
        private string lyricsTrackPath;
        private IList<LyricsLineViewModel> lyricsLines;

        private bool showLyricsInPlaybackControls;
        private bool isNowPlayingPageActive;
        private bool isNowPlayingLyricsSubPageActive;
        private bool isLyricsPageActive;

        public DelegateCommand PlaylistNotificationMouseEnterCommand { get; set; }
        public DelegateCommand PlaylistNotificationMouseLeaveCommand { get; set; }

        public string AddedTracksToPlaylistText
        {
            get { return this.addedTracksToPlaylistText; }
            set { SetProperty<string>(ref this.addedTracksToPlaylistText, value); }
        }

        public bool ShowAddedTracksToPlaylistText
        {
            get { return this.showAddedTracksToPlaylistText; }
            set { SetProperty<bool>(ref this.showAddedTracksToPlaylistText, value); }
        }

        public bool IsShowingLyric
        {
            get { return this.isShowingLyric; }
            set { SetProperty<bool>(ref this.isShowingLyric, value); }
        }

        public PlaybackControlsWithPlaylistNotificationViewModel(IPlaybackService playbackService, IPlaylistService playlistService, IBlacklistService blacklistService, ILyricsService lyricsService, IEventAggregator eventAggregator)
        {
            this.playbackService = playbackService;
            this.playlistService = playlistService;
            this.blacklistService = blacklistService;
            this.lyricsService = lyricsService;
            this.eventAggregator = eventAggregator;

            this.showLyricsInPlaybackControls = SettingsClient.Get<bool>("Lyrics", "ShowInPlaybackControls");
            this.isNowPlayingPageActive = SettingsClient.Get<bool>("FullPlayer", "IsNowPlayingSelected");
            this.isNowPlayingLyricsSubPageActive = ((NowPlayingSubPage)SettingsClient.Get<int>("FullPlayer", "SelectedNowPlayingSubPage")) == NowPlayingSubPage.Lyrics;
            this.isLyricsPageActive = this.isNowPlayingPageActive && this.isNowPlayingLyricsSubPageActive;

            SettingsClient.SettingChanged += (_, e) =>
            {
                if (SettingsClient.IsSettingChanged(e, "Lyrics", "ShowInPlaybackControls"))
                {
                    this.showLyricsInPlaybackControls = (bool)e.Entry.Value;
                    this.UpdateLyricsAvailability();
                }
            };

            // Hide the control bar lyrics while the lyrics page itself is shown.
            this.eventAggregator.GetEvent<IsNowPlayingPageActiveChanged>().Subscribe(active =>
            {
                this.isNowPlayingPageActive = active;
                this.RefreshLyricsPageActive();
            });

            this.eventAggregator.GetEvent<IsNowPlayingSubPageChanged>().Subscribe(tuple =>
            {
                this.isNowPlayingLyricsSubPageActive = tuple.Item2 == NowPlayingSubPage.Lyrics;
                this.RefreshLyricsPageActive();
            });

            this.PlaylistNotificationMouseEnterCommand = new DelegateCommand(() => this.OnMouseEnter());
            this.PlaylistNotificationMouseLeaveCommand = new DelegateCommand(() => this.OnMouseLeave());

            this.playlistService.TracksAdded += (numberTracksAdded, playlist) =>
            {
                string text = ResourceUtils.GetString("Language_Added_Track_To_Playlist");

                if (numberTracksAdded > 1)
                {
                    text = ResourceUtils.GetString("Language_Added_Tracks_To_Playlist");
                }

                this.ShowMessage(text.Replace("{numberoftracks}", numberTracksAdded.ToString()).Replace("{playlistname}", playlist));
            };

            this.playbackService.AddedTracksToQueue += iNumberOfTracks =>
            {
                string text = ResourceUtils.GetString("Language_Added_Track_To_Now_Playing");

                if (iNumberOfTracks > 1)
                {
                    text = ResourceUtils.GetString("Language_Added_Tracks_To_Now_Playing");
                }

                this.ShowMessage(text.Replace("{numberoftracks}", iNumberOfTracks.ToString()));
            };

            this.blacklistService.AddedTracksToBacklist += numberOfTracks =>
            {
                string text = ResourceUtils.GetString("Language_Added_Track_To_Blacklist");

                if (numberOfTracks > 1)
                {
                    text = ResourceUtils.GetString("Language_Added_Tracks_To_Blacklist");
                }

                this.ShowMessage(text.Replace("{numberoftracks}", numberOfTracks.ToString()));
            };

            this.showAddedTracksToPlaylistTextTimer = new Timer
            {
                Interval = TimeSpan.FromSeconds(this.showAddedTracksToPlaylistTextSeconds).TotalMilliseconds
            };
            this.showAddedTracksToPlaylistTextTimer.Elapsed += (_, __) => this.OnMessageExpired();

            this.lyricsResumeTimer = new Timer
            {
                Interval = TimeSpan.FromSeconds(this.lyricsResumeSeconds).TotalMilliseconds
            };
            this.lyricsResumeTimer.Elapsed += (_, __) => this.OnLyricsResume();

            // Keeps track of the current lyric line and shows it when allowed.
            this.lyricLineTimer = new Timer { Interval = this.lyricLineTimerIntervalMilliseconds };
            this.lyricLineTimer.Elapsed += (_, __) => this.OnLyricLineTimerElapsed();
            this.lyricLineTimer.Start();

            this.playbackService.PlaybackSuccess += (_, __) => this.LoadLyricsForCurrentTrack();

            if (this.playbackService.CurrentTrack != null)
            {
                this.LoadLyricsForCurrentTrack();
            }
        }

        // A normal, short lived notification ("added X tracks to now playing", ...).
        private void ShowMessage(string text)
        {
            this.AddedTracksToPlaylistText = text;
            this.IsShowingLyric = false;
            this.displayedLyricLine = null;
            this.isMessageActive = true;
            this.ShowAddedTracksToPlaylistText = true;

            this.showAddedTracksToPlaylistTextTimer.Stop();
            this.showAddedTracksToPlaylistTextTimer.Start();
        }

        private void OnMessageExpired()
        {
            this.showAddedTracksToPlaylistTextTimer.Stop();
            this.isMessageActive = false;
            this.UpdateLyricsAvailability();
        }

        private void OnMouseEnter()
        {
            // Immediately show the playback controls again.
            this.showAddedTracksToPlaylistTextTimer.Stop();
            this.isMessageActive = false;
            this.lyricsResumeTimer.Stop();
            this.suppressLyrics = true;
            this.HideOverlay();
        }

        private void OnMouseLeave()
        {
            this.lyricsResumeTimer.Stop();
            this.lyricsResumeTimer.Start();
        }

        private void OnLyricsResume()
        {
            this.lyricsResumeTimer.Stop();
            this.suppressLyrics = false;
            this.UpdateLyricsAvailability();
        }

        private void RefreshLyricsPageActive()
        {
            this.isLyricsPageActive = this.isNowPlayingPageActive && this.isNowPlayingLyricsSubPageActive;
            this.UpdateLyricsAvailability();
        }

        private bool IsLyricsDisplaySuppressed => this.suppressLyrics || this.isLyricsPageActive || !this.showLyricsInPlaybackControls;

        // Hides the lyric overlay when it is no longer allowed, or shows the current line when
        // it becomes allowed again.
        private void UpdateLyricsAvailability()
        {
            if (this.isMessageActive)
            {
                // Never disturb a normal notification.
                return;
            }

            if (this.IsLyricsDisplaySuppressed)
            {
                if (this.IsShowingLyric)
                {
                    this.HideOverlay();
                }
            }
            else if (!string.IsNullOrEmpty(this.currentLyricLine))
            {
                this.ShowLyric(this.currentLyricLine);
            }
        }

        private void OnLyricLineTimerElapsed()
        {
            // Always track the current line, even while hidden, so it can be shown as soon as
            // the display is allowed again.
            this.UpdateCurrentLyricLine();

            if (this.IsLyricsDisplaySuppressed || this.isMessageActive)
            {
                return;
            }

            if (this.currentLyricLine == this.displayedLyricLine)
            {
                return;
            }

            if (!string.IsNullOrEmpty(this.currentLyricLine))
            {
                this.ShowLyric(this.currentLyricLine);
            }
            else
            {
                this.HideOverlay();
            }
        }

        private void ShowLyric(string text)
        {
            this.AddedTracksToPlaylistText = text;
            this.IsShowingLyric = true;
            this.displayedLyricLine = text;
            this.ShowAddedTracksToPlaylistText = true;
        }

        private void HideOverlay()
        {
            // Keep IsShowingLyric untouched while the overlay fades out, so a disappearing
            // lyric keeps its accent text (and no icon) during the exit animation. It is
            // reset by the next ShowMessage()/ShowLyric() call.
            this.displayedLyricLine = null;
            this.ShowAddedTracksToPlaylistText = false;
        }

        private void UpdateCurrentLyricLine()
        {
            TrackViewModel track = this.playbackService.CurrentTrack;

            if (track == null || track.Path != this.lyricsTrackPath || this.lyricsLines == null)
            {
                this.currentLyricLine = null;
                return;
            }

            this.currentLyricLine = this.GetLineAt(this.playbackService.GetCurrentTime);
        }

        private string GetLineAt(TimeSpan position)
        {
            string result = null;

            foreach (LyricsLineViewModel line in this.lyricsLines)
            {
                if (!line.IsTimed)
                {
                    continue;
                }

                if (line.Time <= position)
                {
                    result = line.Text;
                }
                else
                {
                    // Timed lines are ordered by time.
                    break;
                }
            }

            return result;
        }

        private async void LoadLyricsForCurrentTrack()
        {
            TrackViewModel track = this.playbackService.CurrentTrack;

            if (track == null)
            {
                return;
            }

            string path = track.Path;
            this.lyricsTrackPath = path;
            this.lyricsLines = null;
            this.currentLyricLine = null;
            this.displayedLyricLine = null;

            try
            {
                Lyrics lyrics = await this.lyricsService.GetLyricsAsync(track);

                if (this.playbackService.CurrentTrack == null || this.playbackService.CurrentTrack.Path != path)
                {
                    return;
                }

                this.lyricsLines = this.lyricsService.ParseLyrics(lyrics);
            }
            catch
            {
            }
        }
    }
}
