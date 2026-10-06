using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Utils;
using Dopamine.Core.Api.Netease;
using Dopamine.Core.Audio;
using Dopamine.Core.Base;
using Dopamine.Core.Enums;
using Dopamine.Core.Extensions;
using Dopamine.Data.Entities;
using Dopamine.Data.Metadata;
using Dopamine.Data.Repositories;
using Dopamine.Services.Cache;
using Dopamine.Services.Collection;
using Dopamine.Services.Dialog;
using Dopamine.Services.Indexing;
using Dopamine.Services.InfoDownload;
using Dopamine.Services.Lyrics;
using Dopamine.Services.Metadata;
using Dopamine.Services.Playback;
using Dopamine.Services.Entities;
using Dopamine.Utils;
using Dopamine.ViewModels.Common.Base;
using Dopamine.Views.Common;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Dopamine.ViewModels.Common
{
    public class EditTrackViewModel : EditMetadataBase
    {
        private IList<string> paths;
        private IMetadataService metadataService;
        private IDialogService dialogService;
        private IInfoDownloadService infoDownloadService;
        private ILyricsService lyricsService;
        private IIndexingService indexingService;
        private IPlaybackService playbackService;
        private ITrackRepository trackRepository;

        private string multipleValuesText;
        private bool hasMultipleArtwork;

        private bool updateAlbumArtwork;
        private bool isNcmConvertMode;
        private MetadataValue artists;
        private MetadataValue title;
        private MetadataValue album;
        private MetadataValue albumArtists;
        private MetadataValue year;
        private MetadataValue trackNumber;
        private MetadataValue trackCount;
        private MetadataValue discNumber;
        private MetadataValue discCount;
        private MetadataValue genres;
        private MetadataValue grouping;
        private MetadataValue comment;
        private MetadataValue lyrics;

        private int slideInFrom;
        private UserControl editTrackContent;

        private EditTrackPage previousSelectedEditTrackPage;
        private EditTrackPage selectedEditTrackPage;

        public DelegateCommand LoadedCommand { get; set; }
        public DelegateCommand ChangeArtworkCommand { get; set; }
        public DelegateCommand RemoveArtworkCommand { get; set; }
        public DelegateCommand GetFromNeteaseCommand { get; set; }
        public DelegateCommand GetLyricsFromOnlineCommand { get; set; }

        /// <summary>
        /// True when the editor was opened to convert an NCM file. In that mode the tags can't be
        /// written back to the .ncm itself: saving converts the file to a public format instead.
        /// </summary>
        public bool IsNcmConvertMode
        {
            get { return this.isNcmConvertMode; }
            set { SetProperty<bool>(ref this.isNcmConvertMode, value); }
        }

        public string DialogTitle
        {
            get
            {
                string dialogTitle = this.paths.Count > 1 ? ResourceUtils.GetString("Language_Edit_Multiple_Songs") : ResourceUtils.GetString("Language_Edit_Song");
                return dialogTitle.ToLower();
            }
        }

        public string MultipleTracksWarningText
        {
            get { return ResourceUtils.GetString("Language_Multiple_Songs_Selected").Replace("{trackcount}", this.paths.Count.ToString()); }
        }

        public bool ShowMultipleTracksWarning
        {
            get { return this.paths.Count > 1; }
        }

        public int SlideInFrom
        {
            get { return this.slideInFrom; }
            set { SetProperty<int>(ref this.slideInFrom, value); }
        }

        public EditTrackPage SelectedEditTrackPage
        {
            get { return selectedEditTrackPage; }
            set
            {
                SetProperty<EditTrackPage>(ref this.selectedEditTrackPage, value);
                this.NagivateToSelectedPage();
            }
        }

        public UserControl EditTrackContent
        {
            get { return this.editTrackContent; }
            set { SetProperty<UserControl>(ref this.editTrackContent, value); }
        }

        public bool HasMultipleArtwork
        {
            get { return this.hasMultipleArtwork; }
            set { SetProperty<bool>(ref this.hasMultipleArtwork, value); }
        }

        public MetadataValue Artists
        {
            get { return this.artists; }
            set
            {
                SetProperty<MetadataValue>(ref this.artists, value);
                this.DownloadArtworkCommand.RaiseCanExecuteChanged();
            }
        }

        public MetadataValue Title
        {
            get { return this.title; }
            set { SetProperty<MetadataValue>(ref this.title, value); }
        }

        public MetadataValue Album
        {
            get { return this.album; }
            set
            {
                SetProperty<MetadataValue>(ref this.album, value);
                this.DownloadArtworkCommand.RaiseCanExecuteChanged();
            }
        }

        public MetadataValue AlbumArtists
        {
            get { return this.albumArtists; }
            set
            {
                SetProperty<MetadataValue>(ref this.albumArtists, value);
                this.DownloadArtworkCommand.RaiseCanExecuteChanged();
            }
        }

        public MetadataValue Year
        {
            get { return this.year; }
            set { SetProperty<MetadataValue>(ref this.year, value); }
        }

        public MetadataValue TrackNumber
        {
            get { return this.trackNumber; }
            set { SetProperty<MetadataValue>(ref this.trackNumber, value); }
        }

        public MetadataValue TrackCount
        {
            get { return this.trackCount; }
            set { SetProperty<MetadataValue>(ref this.trackCount, value); }
        }

        public MetadataValue DiscNumber
        {
            get { return this.discNumber; }
            set { SetProperty<MetadataValue>(ref this.discNumber, value); }
        }

        public MetadataValue DiscCount
        {
            get { return this.discCount; }
            set { SetProperty<MetadataValue>(ref this.discCount, value); }
        }

        public MetadataValue Genres
        {
            get { return this.genres; }
            set { SetProperty<MetadataValue>(ref this.genres, value); }
        }

        public MetadataValue Grouping
        {
            get { return this.grouping; }
            set { SetProperty<MetadataValue>(ref this.grouping, value); }
        }

        public MetadataValue Comment
        {
            get { return this.comment; }
            set { SetProperty<MetadataValue>(ref this.comment, value); }
        }

        public MetadataValue Lyrics
        {
            get { return this.lyrics; }
            set { SetProperty<MetadataValue>(ref this.lyrics, value); }
        }

        public bool UpdateAlbumArtwork
        {
            get { return this.updateAlbumArtwork; }
            set { SetProperty<bool>(ref this.updateAlbumArtwork, value); }
        }

        public EditTrackViewModel(IList<string> paths, IMetadataService metadataService,
            IDialogService dialogService, ICacheService cacheService, IInfoDownloadService infoDownloadService,
            ILyricsService lyricsService, IIndexingService indexingService, IPlaybackService playbackService,
            ITrackRepository trackRepository) : base(cacheService, infoDownloadService)
        {
            this.multipleValuesText = "<" + ResourceUtils.GetString("Language_Multiple_Values") + ">";

            this.metadataService = metadataService;
            this.dialogService = dialogService;
            this.infoDownloadService = infoDownloadService;
            this.lyricsService = lyricsService;
            this.indexingService = indexingService;
            this.playbackService = playbackService;
            this.trackRepository = trackRepository;

            this.paths = paths;

            this.HasMultipleArtwork = false;
            this.UpdateAlbumArtwork = false;

            this.LoadedCommand = new DelegateCommand(async () =>
            {
                this.NagivateToSelectedPage();
                await this.GetFilesMetadataAsync();
            });

            this.ChangeArtworkCommand = new DelegateCommand(async () =>
            {
                if (!await OpenFileUtils.OpenImageFileAsync(new Action<byte[]>(this.UpdateArtwork)))
                {
                    this.dialogService.ShowNotification(
                        0xe711,
                        16,
                        ResourceUtils.GetString("Language_Error"),
                        ResourceUtils.GetString("Language_Error_Changing_Image"),
                        ResourceUtils.GetString("Language_Ok"),
                        true,
                        ResourceUtils.GetString("Language_Log_File"));
                }
            });

            this.RemoveArtworkCommand = new DelegateCommand(() => this.UpdateArtwork(null));
            this.DownloadArtworkCommand = new DelegateCommand(async () => await this.DownloadArtworkAsync(), () => this.CanDownloadArtwork());
            this.GetFromNeteaseCommand = new DelegateCommand(async () => await this.GetFromNeteaseAsync());
            this.GetLyricsFromOnlineCommand = new DelegateCommand(async () => await this.GetLyricsFromOnlineAsync());
        }

        private async Task DownloadArtworkAsync()
        {
            try
            {
                await base.DownloadArtworkAsync(
                   this.album.Value,
                   new List<string>() { this.albumArtists.Values.FirstOrDefault() },
                   this.Title.Value,
                   new List<string>() { this.artists.Values.FirstOrDefault() });
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not download artwork. Exception: {0}", ex.Message);
            }
        }

        // Searches NetEase Cloud Music using the current artist and title and fills the tag fields
        // and the cover art with the best match.
        private async Task GetFromNeteaseAsync()
        {
            if (this.IsBusy)
            {
                return;
            }

            string artist = this.artists != null && this.artists.Values != null && this.artists.Values.Length > 0 ? this.artists.Values[0] : string.Empty;
            string title = this.title != null && this.title.Value != null ? this.title.Value : string.Empty;

            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            this.IsBusy = true;

            try
            {
                NeteaseTrackMetadata metadata = await this.infoDownloadService.GetNeteaseTrackMetadataAsync(artist, title);

                if (metadata == null)
                {
                    this.dialogService.ShowNotification(
                        0xe711,
                        16,
                        ResourceUtils.GetString("Language_Error"),
                        ResourceUtils.GetString("Language_Netease_No_Result"),
                        ResourceUtils.GetString("Language_Ok"),
                        false,
                        string.Empty);

                    return;
                }

                this.Title = ChangedValue(metadata.Title);
                this.Artists = ChangedValue(string.Join(";", metadata.Artists));
                this.Album = ChangedValue(metadata.AlbumTitle);
                this.AlbumArtists = ChangedValue(string.Join(";", metadata.AlbumArtists));
                this.Year = ChangedValue(metadata.Year > 0 ? metadata.Year.ToString() : string.Empty);
                this.TrackNumber = ChangedValue(metadata.TrackNumber > 0 ? metadata.TrackNumber.ToString() : string.Empty);
                this.DiscNumber = ChangedValue(metadata.DiscNumber > 0 ? metadata.DiscNumber.ToString() : string.Empty);

                await this.UpdateArtworkFromUrlAsync(metadata.CoverUrl);
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not get metadata from NetEase Cloud Music for '{0} - {1}'. Exception: {2}", artist, title, ex.Message);
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        // Fetches the lyrics from the online sources and puts them in the lyrics field (as plain
        // text, without LRC timestamps) so they can be written to the file on save.
        private async Task GetLyricsFromOnlineAsync()
        {
            if (this.IsBusy)
            {
                return;
            }

            string artist = this.artists != null && this.artists.Values != null && this.artists.Values.Length > 0 ? this.artists.Values[0] : string.Empty;
            string title = this.title != null && this.title.Value != null ? this.title.Value : string.Empty;

            if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            this.IsBusy = true;

            try
            {
                string lyrics = await this.lyricsService.GetPlainOnlineLyricsAsync(artist, title);

                if (!string.IsNullOrWhiteSpace(lyrics))
                {
                    this.Lyrics = ChangedValue(lyrics);
                }
                else
                {
                    this.dialogService.ShowNotification(
                        0xe711,
                        16,
                        ResourceUtils.GetString("Language_Error"),
                        ResourceUtils.GetString("Language_No_Lyrics_Found"),
                        ResourceUtils.GetString("Language_Ok"),
                        false,
                        string.Empty);
                }
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not get online lyrics for '{0} - {1}'. Exception: {2}", artist, title, ex.Message);
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        // Creates a MetadataValue which is flagged as changed, so that the file metadata writers
        // will actually persist it.
        private static MetadataValue ChangedValue(string value)
        {
            var metadataValue = new MetadataValue();
            metadataValue.Value = value ?? string.Empty;
            return metadataValue;
        }

        /// <summary>
        /// Saves an NCM file by converting it to a public format and writing the tags which are
        /// currently in the editor into the converted file. When <paramref name="replaceOriginalFile"/>
        /// is true, the original .ncm file is removed (replaced by the converted file).
        /// Invoked by the "save" and "save and replace NCM file" buttons.
        /// </summary>
        public async Task<bool> SaveNcmAsync(bool replaceOriginalFile)
        {
            if (!this.AllEntriesValid())
            {
                return false;
            }

            string ncmPath = this.paths.FirstOrDefault(path => NcmFile.IsNcmFile(path));

            if (string.IsNullOrEmpty(ncmPath))
            {
                return false;
            }

            this.IsBusy = true;

            try
            {
                // When the file being converted is the one currently playing, the player holds a
                // handle on it: playback must be stopped before the file can be removed.
                if (replaceOriginalFile
                    && this.playbackService.CurrentTrack != null
                    && string.Equals(this.playbackService.CurrentTrack.Path, ncmPath, StringComparison.OrdinalIgnoreCase))
                {
                    this.playbackService.Stop();
                    await Task.Delay(200);
                }

                string targetPath = await Task.Run(() => NcmConverter.Convert(ncmPath));

                // Write the (possibly edited) tags into the converted file.
                await Task.Run(() =>
                {
                    var fileMetadata = new FileMetadata(targetPath);

                    fileMetadata.Title = this.title;
                    fileMetadata.Artists = this.artists;
                    fileMetadata.Album = this.album;
                    fileMetadata.AlbumArtists = this.albumArtists;
                    fileMetadata.Year = this.year;
                    fileMetadata.TrackNumber = this.trackNumber;
                    fileMetadata.TrackCount = this.trackCount;
                    fileMetadata.DiscNumber = this.discNumber;
                    fileMetadata.DiscCount = this.discCount;
                    fileMetadata.Genres = this.genres;
                    fileMetadata.Grouping = this.grouping;
                    fileMetadata.Comment = this.comment;
                    fileMetadata.Lyrics = this.lyrics;
                    fileMetadata.ArtworkData = this.Artwork;
                    fileMetadata.Save();
                });

                if (replaceOriginalFile)
                {
                    DeleteFileWithRetry(ncmPath);
                    await this.ReplaceNcmInLibraryAndQueueAsync(ncmPath, targetPath);
                }
                else
                {
                    // Make the converted file appear in the collection.
                    await this.indexingService.RefreshCollectionImmediatelyAsync();
                }

                this.dialogService.ShowNotification(
                    0xe73e,
                    16,
                    ResourceUtils.GetString("Language_Convert_Ncm_To_Public_Format"),
                    ResourceUtils.GetString("Language_Ncm_Converted_To").Replace("{path}", targetPath),
                    ResourceUtils.GetString("Language_Ok"),
                    false,
                    string.Empty);

                return true;
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not convert the NCM file '{0}'. Exception: {1}", ncmPath, ex.Message);

                this.dialogService.ShowNotification(
                    0xe711,
                    16,
                    ResourceUtils.GetString("Language_Error"),
                    ResourceUtils.GetString("Language_Error_Converting_Ncm"),
                    ResourceUtils.GetString("Language_Ok"),
                    false,
                    string.Empty);

                return false;
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        // Converts the NCM file and replaces it (removes the original .ncm). Invoked by the
        // "save and replace NCM file" button of the custom dialog.
        public async Task<bool> SaveAndReplaceNcmAsync()
        {
            if (!this.isNcmConvertMode)
            {
                return false;
            }

            return await this.SaveNcmAsync(true);
        }

        private static void DeleteFileWithRetry(string path)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    FileUtils.SendToRecycleBinSilent(path);
                    return;
                }
                catch (Exception) when (attempt < 4)
                {
                    // The handle may not have been released yet: wait a bit and retry.
                    System.Threading.Thread.Sleep(200);
                }
            }
        }

        // The player keeps an open handle on the file while it is playing, which makes it impossible
        // to write the tags. Stop playback when one of the files being saved is the playing one.
        private async Task ReleasePlayingFileLocksAsync()
        {
            TrackViewModel currentTrack = this.playbackService.CurrentTrack;

            if (currentTrack == null || string.IsNullOrEmpty(currentTrack.Path))
            {
                return;
            }

            if (this.paths.Any(path => string.Equals(path, currentTrack.Path, StringComparison.OrdinalIgnoreCase)))
            {
                this.playbackService.Stop();
                await Task.Delay(200); // Give the player a moment to close the file.
            }
        }

        // After an NCM file has been replaced by its converted file: makes the converted file take
        // over the old NCM entry in the collection (keeping the album and its artwork) and replaces
        // the queued entry.
        private async Task ReplaceNcmInLibraryAndQueueAsync(string ncmPath, string targetPath)
        {
            string ncmSafePath = ncmPath.ToSafePath();

            // Take over the existing row (and thus its album and cached artwork) instead of removing
            // and re-adding it: removing the only track of an album would delete that album.
            Track oldTrack = await this.trackRepository.GetTrackAsync(ncmPath);
            Track newTrack = await Dopamine.Data.MetadataUtils.Path2TrackAsync(targetPath);

            if (oldTrack != null)
            {
                newTrack.TrackID = oldTrack.TrackID;
                newTrack.DateAdded = oldTrack.DateAdded;
                newTrack.Rating = oldTrack.Rating;
                newTrack.Love = oldTrack.Love;
                newTrack.PlayCount = oldTrack.PlayCount;
                newTrack.SkipCount = oldTrack.SkipCount;
                newTrack.DateLastPlayed = oldTrack.DateLastPlayed;

                // Force the indexer to pick this row up, so the collection lists are refreshed.
                newTrack.NeedsIndexing = 1;

                await this.trackRepository.UpdateTrackAsync(newTrack);
            }
            else
            {
                // The old row is gone: add the converted file directly. Ask for the artwork to be
                // indexed as well, in case the album cover had been cleaned up.
                newTrack.NeedsIndexing = 1;
                newTrack.NeedsAlbumArtworkIndexing = 1;

                await this.trackRepository.AddTrackAsync(newTrack);
            }

            // Make the indexer refresh the collection.
            await this.indexingService.RefreshCollectionImmediatelyAsync();

            // Replace the queued NCM entry with the converted file, keeping its position.
            var queue = this.playbackService.Queue.ToList();
            TrackViewModel queuedNcmTrack = queue.FirstOrDefault(t => string.Equals(t.SafePath, ncmSafePath, StringComparison.OrdinalIgnoreCase));

            if (queuedNcmTrack == null)
            {
                return;
            }

            Track refreshedTrack = await this.trackRepository.GetTrackAsync(targetPath);

            if (refreshedTrack == null)
            {
                refreshedTrack = await Dopamine.Data.MetadataUtils.Path2TrackAsync(targetPath);
            }

            queuedNcmTrack.UpdateTrack(refreshedTrack);
            await this.playbackService.UpdateQueueOrderAsync(queue);
            await this.playbackService.SaveQueuedTracksAsync();
        }

        private void NagivateToSelectedPage()
        {
            this.SlideInFrom = this.selectedEditTrackPage <= this.previousSelectedEditTrackPage ? -Constants.SlideDistance : Constants.SlideDistance;
            this.previousSelectedEditTrackPage = this.selectedEditTrackPage;

            switch (this.selectedEditTrackPage)
            {
                case EditTrackPage.Tags:
                    var tagsContent = new EditTrackTagsControl();
                    tagsContent.DataContext = this;
                    this.EditTrackContent = tagsContent;
                    break;
                case EditTrackPage.Lyrics:
                    var lyricsContent = new EditTrackLyricsControl();
                    lyricsContent.DataContext = this;
                    this.EditTrackContent = lyricsContent;
                    break;
                default:
                    break;
            }
        }

        private bool CanDownloadArtwork()
        {
            if (this.albumArtists == null || this.albumArtists.Value == null ||
                this.Artists == null || this.Artists.Value == null ||
                this.Album == null || this.Album.Value == null)
            {
                return false;
            }

            return (!string.IsNullOrEmpty(this.albumArtists.Value) || !string.IsNullOrEmpty(this.Artists.Value) &&
                !string.IsNullOrEmpty(this.Album.Value));
        }

        private async Task GetFilesMetadataAsync()
        {
            var fileMetadatas = new List<FileMetadata>();

            try
            {
                foreach (string path in this.paths)
                {
                    fileMetadatas.Add(await this.metadataService.GetFileMetadataAsync(path));
                }
            }
            catch (Exception ex)
            {
                LogClient.Error("An error occurred while getting the metadata from the files. Exception: {0}", ex.Message);
            }

            if (fileMetadatas.Count == 0) return;

            await Task.Run(() =>
            {
                try
                {
                    // Artists
                    List<string> distinctArtists = fileMetadatas.Select((f) => f.Artists.Value).Distinct().ToList();
                    this.Artists = new MetadataValue(distinctArtists.Count == 1 ? distinctArtists.First() : this.multipleValuesText);

                    // Title
                    List<string> distinctTitles = fileMetadatas.Select((f) => f.Title.Value).Distinct().ToList();
                    this.Title = new MetadataValue(distinctTitles.Count == 1 ? distinctTitles.First() : this.multipleValuesText);

                    // Album
                    List<string> distinctAlbums = fileMetadatas.Select((f) => f.Album.Value).Distinct().ToList();
                    this.Album = new MetadataValue(distinctAlbums.Count == 1 ? distinctAlbums.First() : this.multipleValuesText);

                    // AlbumArtists
                    List<string> distinctAlbumArtists = fileMetadatas.Select((f) => f.AlbumArtists.Value).Distinct().ToList();
                    this.AlbumArtists = new MetadataValue(distinctAlbumArtists.Count == 1 ? distinctAlbumArtists.First() : this.multipleValuesText);

                    // Year
                    List<string> distinctYears = fileMetadatas.Select((f) => f.Year.Value).Distinct().ToList();
                    this.Year = new MetadataValue(distinctYears.Count == 1 ? distinctYears.First().ToString() : this.multipleValuesText);

                    // TrackNumber
                    List<string> distinctTrackNumbers = fileMetadatas.Select((f) => f.TrackNumber.Value).Distinct().ToList();
                    this.TrackNumber = new MetadataValue(distinctTrackNumbers.Count == 1 ? distinctTrackNumbers.First().ToString() : this.multipleValuesText);

                    // TrackCount
                    List<string> distinctTrackCounts = fileMetadatas.Select((f) => f.TrackCount.Value).Distinct().ToList();
                    this.TrackCount = new MetadataValue(distinctTrackCounts.Count == 1 ? distinctTrackCounts.First().ToString() : this.multipleValuesText);

                    // DiscNumber
                    List<string> distinctDiscNumbers = fileMetadatas.Select((f) => f.DiscNumber.Value).Distinct().ToList();
                    this.DiscNumber = new MetadataValue(distinctDiscNumbers.Count == 1 ? distinctDiscNumbers.First().ToString() : this.multipleValuesText);

                    // DiscCount
                    List<string> distinctDiscCounts = fileMetadatas.Select((f) => f.DiscCount.Value).Distinct().ToList();
                    this.DiscCount = new MetadataValue(distinctDiscCounts.Count == 1 ? distinctDiscCounts.First().ToString() : this.multipleValuesText);

                    // Genres
                    List<string> distinctGenres = fileMetadatas.Select((f) => f.Genres.Value).Distinct().ToList();
                    this.Genres = new MetadataValue(distinctGenres.Count == 1 ? distinctGenres.First() : this.multipleValuesText);

                    // Grouping
                    List<string> distinctGroupings = fileMetadatas.Select((f) => f.Grouping.Value).Distinct().ToList();
                    this.Grouping = new MetadataValue(distinctGroupings.Count == 1 ? distinctGroupings.First() : this.multipleValuesText);

                    // Comment
                    List<string> distinctComments = fileMetadatas.Select((f) => f.Comment.Value).Distinct().ToList();
                    this.Comment = new MetadataValue(distinctComments.Count == 1 ? distinctComments.First() : this.multipleValuesText);

                    // Lyrics
                    List<string> distinctLyrics = fileMetadatas.Select((f) => f.Lyrics.Value).Distinct().ToList();
                    this.lyrics = new MetadataValue(distinctLyrics.Count == 1 ? distinctLyrics.First() : this.multipleValuesText);

                    // Artwork 
                    this.GetArtwork(fileMetadatas);
                }
                catch (Exception ex)
                {
                    LogClient.Error("An error occurred while parsing the metadata. Exception: {0}", ex.Message);
                }
            });
        }

        private void GetArtwork(List<FileMetadata> fileMetadatas)
        {
            byte[] foundArtwork = null;

            List<byte[]> artworks = fileMetadatas.Select((f) => f.ArtworkData.Value).ToList();
            List<int> artworksSizes = new List<int>();

            foreach (byte[] eaw in artworks)
            {
                if (eaw != null)
                {
                    artworksSizes.Add(eaw.Length);
                    foundArtwork = eaw;
                }
                else
                {
                    artworksSizes.Add(0);
                }
            }

            int distinctArtworkCount = artworksSizes.Select((l) => l).Distinct().Count();

            if (distinctArtworkCount > 1)
            {
                foundArtwork = null;
                this.HasMultipleArtwork = true;
            }
            else
            {
                this.HasMultipleArtwork = false;
            }

            this.ShowArtwork(foundArtwork);
        }

        private bool AllEntriesValid()
        {
            return this.Year.IsNumeric &
                   this.TrackNumber.IsNumeric &
                   this.TrackCount.IsNumeric &
                   this.DiscNumber.IsNumeric &
                   this.DiscCount.IsNumeric;
        }

        private void VisualizeArtwork(byte[] imageData)
        {
            this.ArtworkThumbnail = ImageUtils.ByteToBitmapImage(imageData, 0, 0, Convert.ToInt32(Constants.CoverLargeSize));

            // Size of the artwork
            if (imageData != null)
            {
                // Use PixelWidth and PixelHeight instead of Width and Height:
                // Width and Height take DPI into account. We don't want that here.
                this.ArtworkSize = this.ArtworkThumbnail.PixelWidth + "x" + this.ArtworkThumbnail.PixelHeight;
            }
            else
            {
                this.ArtworkSize = string.Empty;
            }

            RaisePropertyChanged(nameof(this.HasArtwork));
        }

        protected override void UpdateArtwork(byte[] imageData)
        {
            base.UpdateArtwork(imageData);

            // Artwork is updated. Multiple artwork is now impossible.
            this.HasMultipleArtwork = false;
        }

        public async Task<bool> SaveTracksAsync()
        {
            // In NCM convert mode, saving converts the file to a public format (keeping the original
            // .ncm) instead of writing tags back to the source file.
            if (this.isNcmConvertMode)
            {
                return await this.SaveNcmAsync(false);
            }

            // NCM files are encrypted containers: their tags can't be written back to disk.
            if (this.paths.Any(path => NcmFile.IsNcmFile(path)))
            {
                this.dialogService.ShowNotification(
                    0xe711,
                    16,
                    ResourceUtils.GetString("Language_Error"),
                    ResourceUtils.GetString("Language_Ncm_Write_Not_Supported"),
                    ResourceUtils.GetString("Language_Ok"),
                    false,
                    string.Empty);

                return false;
            }

            if (!this.AllEntriesValid()) return false;

            var fmdList = new List<FileMetadata>();

            this.IsBusy = true;

            // The tags can't be written while the file is being played: release it first.
            await this.ReleasePlayingFileLocksAsync();

            await Task.Run(() =>
            {
                try
                {
                    foreach (string path in this.paths)
                    {
                        FileMetadata fmd = this.metadataService.GetFileMetadata(path);

                        fmd.Artists = this.artists;
                        fmd.Title = this.title;
                        fmd.Album = this.album;
                        fmd.AlbumArtists = this.albumArtists;
                        fmd.Year = this.year;
                        fmd.TrackNumber = this.trackNumber;
                        fmd.TrackCount = this.trackCount;
                        fmd.DiscNumber = this.discNumber;
                        fmd.DiscCount = this.discCount;
                        fmd.Genres = this.genres;
                        fmd.Grouping = this.grouping;
                        fmd.Comment = this.comment;
                        fmd.Lyrics = this.lyrics;
                        fmd.ArtworkData = this.Artwork;

                        fmdList.Add(fmd);
                    }
                }
                catch (Exception ex)
                {
                    LogClient.Error("An error occurred while setting the metadata. Exception: {0}", ex.Message);
                }
            });

            if (fmdList.Count > 0)
            {
                await this.metadataService.UpdateTracksAsync(fmdList, this.UpdateAlbumArtwork);
            }

            this.IsBusy = false;

            return true;
        }
    }
}