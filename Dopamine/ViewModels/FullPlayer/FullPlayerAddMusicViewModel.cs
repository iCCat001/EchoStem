using Digimezzo.Foundation.Core.IO;
using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Settings;
using Digimezzo.Foundation.Core.Utils;
using Dopamine.Core.Audio;
using Dopamine.Data;
using Dopamine.Data.Entities;
using Dopamine.Data.Metadata;
using Dopamine.Data.Repositories;
using Dopamine.Services.Indexing;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dopamine.ViewModels.FullPlayer
{
    public class FullPlayerAddMusicViewModel : BindableBase
    {
        private bool checkBoxRefreshCollectionAutomaticallyChecked;
        private bool checkBoxIgnoreRemovedFilesChecked;
        private bool checkBoxDownloadMissingAlbumCoversChecked;
        private bool isRefreshNowBusy;
        private bool isReloadAllCoversBusy;
        private bool isReloadMissingCoversBusy;
        private bool isConvertAllNcmBusy;
        private string convertAllNcmStatusText;
        private ITrackRepository trackRepository;
        private IIndexingService indexingService;

        public FullPlayerAddMusicViewModel(ITrackRepository trackRepository, IIndexingService indexingService)
        {
            this.trackRepository = trackRepository;
            this.indexingService = indexingService;

            // The commands disable their button while the task is running, until it completes.
            this.RefreshNowCommand = new DelegateCommand(async () => await this.RefreshNowAsync(), () => !this.isRefreshNowBusy);
            this.ReloadAllCoversCommand = new DelegateCommand(async () => await this.ReloadCoversAsync(false), () => !this.isReloadAllCoversBusy);
            this.ReloadMissingCoversCommand = new DelegateCommand(async () => await this.ReloadCoversAsync(true), () => !this.isReloadMissingCoversBusy);
            this.ConvertAllNcmCommand = new DelegateCommand(async () => await this.ConvertAllNcmAsync(), () => !this.isConvertAllNcmBusy);

            this.IsConvertAllNcmVisible = SettingsClient.Get<bool>("Features", "NonPublicExperimentalServices");

            this.GetCheckBoxesAsync();
        }

        public DelegateCommand RefreshNowCommand { get; set; }

        public DelegateCommand ReloadAllCoversCommand { get; set; }

        public DelegateCommand ReloadMissingCoversCommand { get; set; }

        public DelegateCommand ConvertAllNcmCommand { get; set; }

        public bool IsConvertAllNcmVisible { get; private set; }

        public string ConvertAllNcmStatusText
        {
            get { return this.convertAllNcmStatusText; }
            private set { SetProperty<string>(ref this.convertAllNcmStatusText, value); }
        }

        public bool CheckBoxRefreshCollectionAutomaticallyChecked
        {
            get { return this.checkBoxRefreshCollectionAutomaticallyChecked; }
            set
            {
                SettingsClient.Set<bool>("Indexing", "RefreshCollectionAutomatically", value, true);
                SetProperty<bool>(ref this.checkBoxRefreshCollectionAutomaticallyChecked, value);
            }
        }

        public bool CheckBoxIgnoreRemovedFilesChecked
        {
            get { return this.checkBoxIgnoreRemovedFilesChecked; }
            set
            {
                SettingsClient.Set<bool>("Indexing", "IgnoreRemovedFiles", value);
                SetProperty<bool>(ref this.checkBoxIgnoreRemovedFilesChecked, value);

                if (!value)
                {
                    // Fire and forget
                    this.trackRepository.ClearRemovedTrackAsync();
                }
            }
        }

        public bool CheckBoxDownloadMissingAlbumCoversChecked
        {
            get { return this.checkBoxDownloadMissingAlbumCoversChecked; }
            set
            {
                SettingsClient.Set<bool>("Covers", "DownloadMissingAlbumCovers", value);
                SetProperty<bool>(ref this.checkBoxDownloadMissingAlbumCoversChecked, value);

                if (value)
                {
                    _ = this.indexingService.ReScanAlbumArtworkAsync(true);
                }
            }
        }

        private async Task RefreshNowAsync()
        {
            this.isRefreshNowBusy = true;
            this.RefreshNowCommand.RaiseCanExecuteChanged();

            try
            {
                await this.indexingService.RefreshCollectionImmediatelyAsync();
            }
            finally
            {
                this.isRefreshNowBusy = false;
                this.RefreshNowCommand.RaiseCanExecuteChanged();
            }
        }

        private async Task ReloadCoversAsync(bool onlyMissingCovers)
        {
            this.isReloadAllCoversBusy = true;
            this.isReloadMissingCoversBusy = true;
            this.ReloadAllCoversCommand.RaiseCanExecuteChanged();
            this.ReloadMissingCoversCommand.RaiseCanExecuteChanged();

            try
            {
                await this.indexingService.ReScanAlbumArtworkAsync(onlyMissingCovers);
            }
            finally
            {
                this.isReloadAllCoversBusy = false;
                this.isReloadMissingCoversBusy = false;
                this.ReloadAllCoversCommand.RaiseCanExecuteChanged();
                this.ReloadMissingCoversCommand.RaiseCanExecuteChanged();
            }
        }

        // Converts all NCM files of the collection to a public format, and removes the original
        // NCM files. Existing files with the same name are overwritten.
        private async Task ConvertAllNcmAsync()
        {
            this.isConvertAllNcmBusy = true;
            this.ConvertAllNcmCommand.RaiseCanExecuteChanged();

            int converted = 0;
            int total = 0;

            try
            {
                IList<Track> allTracks = await this.trackRepository.GetTracksAsync();
                List<string> ncmPaths = allTracks
                    .Select(t => t.Path)
                    .Where(p => NcmFile.IsNcmFile(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                total = ncmPaths.Count;
                this.ConvertAllNcmStatusText = this.FormatNcmProgress(converted, total);

                foreach (string ncmPath in ncmPaths)
                {
                    try
                    {
                        string targetPath = await Task.Run(() => NcmConverter.Convert(ncmPath));

                        // The converted file takes over the row of the NCM file, keeping its album,
                        // cached artwork and play statistics.
                        await this.ReplaceNcmTrackAsync(ncmPath, targetPath);

                        try
                        {
                            FileUtils.SendToRecycleBinSilent(ncmPath);
                        }
                        catch (Exception ex)
                        {
                            LogClient.Error("Could not remove the NCM file '{0}'. Exception: {1}", ncmPath, ex.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogClient.Error("Could not convert the NCM file '{0}'. Exception: {1}", ncmPath, ex.Message);
                    }

                    converted++;
                    this.ConvertAllNcmStatusText = this.FormatNcmProgress(converted, total);
                }

                // Make the collection reflect the converted files.
                await this.indexingService.RefreshCollectionImmediatelyAsync();
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not convert all NCM files. Exception: {0}", ex.Message);
            }
            finally
            {
                this.isConvertAllNcmBusy = false;
                this.ConvertAllNcmCommand.RaiseCanExecuteChanged();
            }
        }

        private async Task ReplaceNcmTrackAsync(string ncmPath, string targetPath)
        {
            Track oldTrack = await this.trackRepository.GetTrackAsync(ncmPath);

            if (oldTrack == null)
            {
                return;
            }

            Track newTrack = await MetadataUtils.Path2TrackAsync(targetPath);

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

        private string FormatNcmProgress(int converted, int total)
        {
            return ResourceUtils.GetString("Language_Convert_Ncm_Progress")
                .Replace("{done}", converted.ToString())
                .Replace("{total}", total.ToString());
        }

        private async void GetCheckBoxesAsync()
        {
            await Task.Run(() =>
            {
                this.checkBoxRefreshCollectionAutomaticallyChecked = SettingsClient.Get<bool>("Indexing", "RefreshCollectionAutomatically");
                this.checkBoxIgnoreRemovedFilesChecked = SettingsClient.Get<bool>("Indexing", "IgnoreRemovedFiles");
                this.checkBoxDownloadMissingAlbumCoversChecked = SettingsClient.Get<bool>("Covers", "DownloadMissingAlbumCovers");
            });
        }
    }
}
