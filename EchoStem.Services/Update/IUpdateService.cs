using EchoStem.Core.Api.GitHub;
using System;
using System.Threading.Tasks;

namespace EchoStem.Services.Update
{
    public interface IUpdateService
    {
        /// <summary>
        /// Checks the GitHub releases of the project for a newer version.
        /// </summary>
        Task<UpdateCheckResult> CheckForUpdatesAsync();

        /// <summary>
        /// Downloads the portable package of the given release, extracts it and returns the folder
        /// which has to be copied over the application folder. Returns null when it failed.
        /// </summary>
        Task<string> DownloadAndPrepareUpdateAsync(GitHubReleaseAsset portableAsset, IProgress<double> progress);

        /// <summary>
        /// Starts an external script which waits for this application to exit, copies the prepared
        /// update over the application folder and restarts the application.
        /// </summary>
        bool StartUpdateAndRestart(string updateSourceDirectory);

        /// <summary>
        /// Downloads the full installer of the given release and returns its path. Returns null when
        /// it failed.
        /// </summary>
        Task<string> DownloadInstallerAsync(GitHubReleaseAsset installerAsset, IProgress<double> progress);

        /// <summary>
        /// Starts the downloaded installer. The caller shuts the application down afterwards.
        /// </summary>
        bool StartInstaller(string installerPath);
    }
}
