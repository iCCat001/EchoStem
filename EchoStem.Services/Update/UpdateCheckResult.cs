using EchoStem.Core.Api.GitHub;

namespace EchoStem.Services.Update
{
    public enum UpdateCheckStatus
    {
        /// <summary>A newer version was found.</summary>
        UpdateAvailable,

        /// <summary>The installed version is the latest.</summary>
        UpToDate,

        /// <summary>The version could not be checked (network error, missing release, ...).</summary>
        Error
    }

    public sealed class UpdateCheckResult
    {
        public UpdateCheckStatus Status { get; set; }

        public AppVersion InstalledVersion { get; set; }

        public AppVersion OnlineVersion { get; set; }

        public GitHubRelease Release { get; set; }

        public GitHubReleaseAsset PortableAsset { get; set; }

        public GitHubReleaseAsset InstallerAsset { get; set; }

        /// <summary>True when the release exists but has no downloadable update package.</summary>
        public bool IsPackageMissing { get; set; }

        public string ErrorMessage { get; set; }
    }
}
