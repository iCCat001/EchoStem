using Digimezzo.Foundation.Core.Utils;
using Dopamine.Services.Update;
using Prism.Mvvm;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace Dopamine.ViewModels.Common
{
    /// <summary>
    /// Content of the dialog which shows an available update: the new version, the release notes,
    /// the download progress, and starts the update when the user confirms it.
    /// </summary>
    public class UpdateDialogViewModel : BindableBase
    {
        private readonly IUpdateService updateService;
        private readonly UpdateCheckResult checkResult;
        private double progress;
        private bool isDownloading;
        private string statusText;

        public string VersionText { get; }

        public string ReleaseNotes { get; }

        public double Progress
        {
            get { return this.progress; }
            private set { SetProperty<double>(ref this.progress, value); }
        }

        public bool IsDownloading
        {
            get { return this.isDownloading; }
            private set
            {
                SetProperty<bool>(ref this.isDownloading, value);
                RaisePropertyChanged(nameof(this.ProgressVisibility));
            }
        }

        public Visibility ProgressVisibility => this.IsDownloading ? Visibility.Visible : Visibility.Collapsed;

        public string StatusText
        {
            get { return this.statusText; }
            private set { SetProperty<string>(ref this.statusText, value); }
        }

        /// <summary>True when the update was prepared and the application is about to restart.</summary>
        public bool UpdateStarted { get; private set; }

        public UpdateDialogViewModel(IUpdateService updateService, UpdateCheckResult checkResult)
        {
            this.updateService = updateService;
            this.checkResult = checkResult;

            this.VersionText = $"{ResourceUtils.GetString("Language_Version")}: {checkResult.Release?.TagName ?? checkResult.OnlineVersion?.ToString()}";
            this.ReleaseNotes = ReleaseNotesHelper.GetText(checkResult.Release?.Body);
        }

        /// <summary>
        /// Called by the "Update" button. Downloads the portable package, prepares it and starts
        /// the update script. Returns whether the dialog can be closed.
        /// </summary>
        public async Task<bool> UpdateNowAsync()
        {
            if (this.UpdateStarted || this.IsDownloading)
            {
                return false;
            }

            this.IsDownloading = true;
            this.Progress = 0;
            this.StatusText = ResourceUtils.GetString("Language_Downloading_Update");

            var progress = new Progress<double>(value => this.Progress = value);

            string updateFolder = await this.updateService.DownloadAndPrepareUpdateAsync(
                this.checkResult.PortableAsset, progress);

            this.IsDownloading = false;

            if (string.IsNullOrEmpty(updateFolder))
            {
                this.StatusText = ResourceUtils.GetString("Language_Update_Download_Failed");
                return false;
            }

            if (!this.updateService.StartUpdateAndRestart(updateFolder))
            {
                this.StatusText = ResourceUtils.GetString("Language_Update_Download_Failed");
                return false;
            }

            this.UpdateStarted = true;

            return true;
        }
    }
}
