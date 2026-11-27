using Digimezzo.Foundation.Core.Utils;
using EchoStem.Services.Update;
using Prism.Mvvm;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace EchoStem.ViewModels.Common
{
    /// <summary>
    /// Content of the dialog which shows an available update: the new version, the release notes,
    /// the install mode (quick portable update or full installer), the download progress, and starts
    /// the update when the user confirms it.
    /// </summary>
    public class UpdateDialogViewModel : BindableBase
    {
        private readonly IUpdateService updateService;
        private readonly UpdateCheckResult checkResult;
        private double progress;
        private bool isDownloading;
        private bool isFullInstallSelected;
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

        /// <summary>True when the quick (portable) update can be installed.</summary>
        public bool CanInstallQuick => this.checkResult.PortableAsset != null;

        /// <summary>True when the full installer is available for this release.</summary>
        public bool CanInstallFull => this.checkResult.InstallerAsset != null;

        /// <summary>
        /// Quick install: downloads the portable package and copies it over the application folder.
        /// This is the default. Selecting it clears the full installer option.
        /// </summary>
        public bool IsQuickInstallSelected
        {
            get { return !this.isFullInstallSelected; }
            set
            {
                if (value)
                {
                    this.SetFullInstallSelected(false);
                }
            }
        }

        /// <summary>
        /// Full install: downloads the setup executable, starts it and closes the application so the
        /// installer can update it. Selecting it clears the quick install option.
        /// </summary>
        public bool IsFullInstallSelected
        {
            get { return this.isFullInstallSelected; }
            set { this.SetFullInstallSelected(value); }
        }

        /// <summary>True when the update was prepared/started and the application is about to close.</summary>
        public bool UpdateStarted { get; private set; }

        public UpdateDialogViewModel(IUpdateService updateService, UpdateCheckResult checkResult)
        {
            this.updateService = updateService;
            this.checkResult = checkResult;

            // Quick install is the default. It can only be default when the release actually has a
            // portable package; otherwise fall back to the full installer.
            this.isFullInstallSelected = !this.CanInstallQuick && this.CanInstallFull;

            this.VersionText = $"{ResourceUtils.GetString("Language_Version")}: {checkResult.Release?.TagName ?? checkResult.OnlineVersion?.ToString()}";
            this.ReleaseNotes = ReleaseNotesHelper.GetText(checkResult.Release?.Body);
        }

        /// <summary>
        /// Called by the "Update" button. Depending on the selected install mode this either applies
        /// the portable package or starts the full installer. Returns whether the dialog can close.
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

            bool started = this.IsFullInstallSelected
                ? await this.InstallFullUpdateAsync(progress)
                : await this.InstallQuickUpdateAsync(progress);

            if (started)
            {
                this.UpdateStarted = true;
            }

            return started;
        }

        private async Task<bool> InstallQuickUpdateAsync(IProgress<double> progress)
        {
            if (this.checkResult.PortableAsset == null)
            {
                this.IsDownloading = false;
                this.StatusText = ResourceUtils.GetString("Language_Update_Download_Failed");
                return false;
            }

            string updateFolder = await this.updateService.DownloadAndPrepareUpdateAsync(
                this.checkResult.PortableAsset, progress);

            this.IsDownloading = false;

            if (string.IsNullOrEmpty(updateFolder) || !this.updateService.StartUpdateAndRestart(updateFolder))
            {
                this.StatusText = ResourceUtils.GetString("Language_Update_Download_Failed");
                return false;
            }

            return true;
        }

        private async Task<bool> InstallFullUpdateAsync(IProgress<double> progress)
        {
            if (this.checkResult.InstallerAsset == null)
            {
                this.IsDownloading = false;
                this.StatusText = ResourceUtils.GetString("Language_Update_Download_Failed");
                return false;
            }

            string installerPath = await this.updateService.DownloadInstallerAsync(
                this.checkResult.InstallerAsset, progress);

            this.IsDownloading = false;

            if (string.IsNullOrEmpty(installerPath) || !this.updateService.StartInstaller(installerPath))
            {
                this.StatusText = ResourceUtils.GetString("Language_Update_Download_Failed");
                return false;
            }

            return true;
        }

        private void SetFullInstallSelected(bool value)
        {
            if (this.isFullInstallSelected == value)
            {
                return;
            }

            this.isFullInstallSelected = value;
            RaisePropertyChanged(nameof(this.IsFullInstallSelected));
            RaisePropertyChanged(nameof(this.IsQuickInstallSelected));
        }
    }
}
