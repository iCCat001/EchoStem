using Digimezzo.Foundation.Core.IO;
using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Settings;
using EchoStem.Core.Api.GitHub;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EchoStem.Services.Update
{
    public class UpdateService : IUpdateService
    {
        // The repository which publishes the releases and the portable packages.
        private const string RepositoryOwner = "iCCat001";
        private const string RepositoryName = "EchoStem";
        private const string PortableAssetExtension = ".zip";

        // The assets of the current releases are named EchoStem-Portable-*.zip and
        // EchoStem-Setup-*.exe. Releases from before the rename still carry the old Dopamine names,
        // so those are accepted as a fallback while such a release is the latest one.
        private static readonly string[] PortableAssetPrefixes = { "EchoStem-Portable-", "Dopamine-Portable-" };
        private static readonly string[] InstallerAssetPrefixes = { "EchoStem-Setup-", "Dopamine-Setup-" };
        private const string InstallerAssetExtension = ".exe";
        private const string PortableFolderName = "EchoStem";
        private const string LegacyPortableFolderName = "Dopamine";

        private const string InstalledVersionSetting = "InstalledVersion";

        public async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            var result = new UpdateCheckResult();

            try
            {
                result.InstalledVersion = this.GetInstalledVersion();

                LogClient.Info("Update check: installed version = {0}", result.InstalledVersion);

                GitHubRelease release = await GitHubApi.GetLatestReleaseAsync(RepositoryOwner, RepositoryName);

                if (release == null || string.IsNullOrEmpty(release.TagName))
                {
                    return this.CreateError(result, "No release was found.");
                }

                result.Release = release;
                result.OnlineVersion = AppVersion.Parse(release.TagName);

                if (result.OnlineVersion == null)
                {
                    return this.CreateError(result, $"Could not parse the release tag '{release.TagName}'.");
                }

                LogClient.Info("Update check: online version = {0} (tag {1})", result.OnlineVersion, release.TagName);

                result.PortableAsset = FindPortableAsset(release);
                result.InstallerAsset = FindInstallerAsset(release);

                LogClient.Info("Update check: portable asset = {0}, installer asset = {1}",
                    result.PortableAsset?.Name ?? "(none)", result.InstallerAsset?.Name ?? "(none)");

                if (result.PortableAsset == null && result.InstallerAsset == null)
                {
                    return this.CreateError(result, "The release does not contain an update package.");
                }

                result.Status = result.OnlineVersion.CompareTo(result.InstalledVersion) > 0
                    ? UpdateCheckStatus.UpdateAvailable
                    : UpdateCheckStatus.UpToDate;
            }
            catch (Exception ex)
            {
                LogClient.Error("Update check: could not retrieve the online version information. Exception: {0}", ex.Message);
                this.CreateError(result, ex.Message);
            }

            return result;
        }

        public async Task<string> DownloadAndPrepareUpdateAsync(GitHubReleaseAsset portableAsset,
            IProgress<double> progress)
        {
            if (portableAsset == null || string.IsNullOrEmpty(portableAsset.DownloadUrl))
            {
                return null;
            }

            string updateFolder = Path.Combine(Path.GetTempPath(), "EchoStemUpdate");

            try
            {
                if (Directory.Exists(updateFolder))
                {
                    Directory.Delete(updateFolder, true);
                }

                Directory.CreateDirectory(updateFolder);

                string packagePath = Path.Combine(updateFolder, portableAsset.Name);

                await DownloadAssetAsync(portableAsset, packagePath, progress);

                string extractedFolder = Path.Combine(updateFolder, "Extracted");
                ZipFile.ExtractToDirectory(packagePath, extractedFolder);

                string applicationFolder = GetApplicationFolderFromExtractedPackage(extractedFolder);

                return applicationFolder;
            }
            catch (Exception ex)
            {
                LogClient.Error("Update: could not download or extract the update. Exception: {0}", ex.Message);
                return null;
            }
        }

        public bool StartUpdateAndRestart(string updateSourceDirectory)
        {
            try
            {
                string applicationFolder = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                string applicationPath = Process.GetCurrentProcess().MainModule.FileName;
                string scriptPath = Path.Combine(Path.GetTempPath(), "EchoStemUpdate", "apply-update.cmd");
                int processId = Process.GetCurrentProcess().Id;

                // When the application is installed in a protected folder (e.g. Program Files), the
                // copy requires administrator rights: the script is then started elevated.
                bool needsElevation = !IsFolderWritable(applicationFolder);

                var script = new StringBuilder();
                script.AppendLine("@echo off");
                script.AppendLine("setlocal");
                script.AppendLine(":wait");
                script.AppendLine($"tasklist /FI \"PID eq {processId}\" 2>nul | find \"{processId}\" >nul");
                script.AppendLine("if not errorlevel 1 (");
                script.AppendLine("  ping -n 2 127.0.0.1 >nul");
                script.AppendLine("  goto wait");
                script.AppendLine(")");
                script.AppendLine($"robocopy \"{updateSourceDirectory}\" \"{applicationFolder}\" /E /R:5 /W:2 /NFL /NDL /NJH /NJS >nul");

                if (needsElevation)
                {
                    // The script runs elevated: restart the application as the current, non
                    // elevated user (explorer runs in the user's context).
                    script.AppendLine($"explorer.exe \"{applicationPath}\"");
                }
                else
                {
                    script.AppendLine($"start \"\" \"{applicationPath}\"");
                }

                script.AppendLine("del \"%~f0\"");

                // The command processor reads the script with the system code page.
                System.IO.File.WriteAllText(scriptPath, script.ToString(), Encoding.Default);

                // Forget the stored version (which may have been set for testing), so the next
                // check compares against the version of the newly installed build instead.
                SettingsClient.Set<string>("Updates", InstalledVersionSetting, string.Empty);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"\"{scriptPath}\"\"",
                    CreateNoWindow = !needsElevation,
                    UseShellExecute = needsElevation,
                    Verb = needsElevation ? "runas" : string.Empty,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                LogClient.Info("Update: the update script was started.");

                return true;
            }
            catch (Exception ex)
            {
                LogClient.Error("Update: could not start the update script. Exception: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Downloads the full installer of the release and returns its path, or null when it failed.
        /// </summary>
        public async Task<string> DownloadInstallerAsync(GitHubReleaseAsset installerAsset, IProgress<double> progress)
        {
            if (installerAsset == null || string.IsNullOrEmpty(installerAsset.DownloadUrl))
            {
                return null;
            }

            string installerFolder = Path.Combine(Path.GetTempPath(), "EchoStemUpdateInstaller");

            try
            {
                Directory.CreateDirectory(installerFolder);

                string installerPath = Path.Combine(installerFolder, installerAsset.Name);

                if (System.IO.File.Exists(installerPath))
                {
                    System.IO.File.Delete(installerPath);
                }

                await DownloadAssetAsync(installerAsset, installerPath, progress);

                return installerPath;
            }
            catch (Exception ex)
            {
                LogClient.Error("Update: could not download the installer. Exception: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Starts the downloaded installer. The caller shuts the application down so the installer
        /// can replace the installed files.
        /// </summary>
        public bool StartInstaller(string installerPath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                });

                LogClient.Info("Update: the installer was started.");

                return true;
            }
            catch (Exception ex)
            {
                LogClient.Error("Update: could not start the installer. Exception: {0}", ex.Message);
                return false;
            }
        }

        internal static GitHubReleaseAsset FindPortableAsset(GitHubRelease release)
        {
            return FindAsset(release, PortableAssetPrefixes, PortableAssetExtension);
        }

        internal static GitHubReleaseAsset FindInstallerAsset(GitHubRelease release)
        {
            return FindAsset(release, InstallerAssetPrefixes, InstallerAssetExtension);
        }

        /// <summary>
        /// The portable zip contains the application in an "EchoStem" folder (older packages used
        /// "Dopamine"). Falls back to the extracted folder itself.
        /// </summary>
        internal static string GetApplicationFolderFromExtractedPackage(string extractedFolder)
        {
            string applicationFolder = Path.Combine(extractedFolder, PortableFolderName);

            if (!Directory.Exists(applicationFolder))
            {
                applicationFolder = Path.Combine(extractedFolder, LegacyPortableFolderName);
            }

            if (!Directory.Exists(applicationFolder))
            {
                applicationFolder = extractedFolder;
            }

            return applicationFolder;
        }

        private static GitHubReleaseAsset FindAsset(GitHubRelease release, string[] prefixes, string extension)
        {
            if (release?.Assets == null)
            {
                return null;
            }

            foreach (string prefix in prefixes)
            {
                foreach (GitHubReleaseAsset asset in release.Assets)
                {
                    if (asset.Name != null &&
                        asset.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        asset.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    {
                        return asset;
                    }
                }
            }

            return null;
        }

        private static async Task DownloadAssetAsync(GitHubReleaseAsset asset, string destinationPath, IProgress<double> progress)
        {
            using (var client = new HttpClient())
            using (var response = await client.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                long totalSize = response.Content.Headers.ContentLength ?? asset.Size;
                long receivedSize = 0;

                using (var input = await response.Content.ReadAsStreamAsync())
                using (var output = System.IO.File.Create(destinationPath))
                {
                    var buffer = new byte[81920];
                    int read;

                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await output.WriteAsync(buffer, 0, read);
                        receivedSize += read;

                        if (totalSize > 0)
                        {
                            progress?.Report((double)receivedSize / totalSize);
                        }
                    }
                }
            }
        }
        private static bool IsFolderWritable(string folder)
        {
            try
            {
                string testFile = Path.Combine(folder, "." + Guid.NewGuid().ToString("N") + ".tmp");
                System.IO.File.WriteAllText(testFile, string.Empty);
                System.IO.File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private UpdateCheckResult CreateError(UpdateCheckResult result, string message)
        {
            result.Status = UpdateCheckStatus.Error;
            result.ErrorMessage = message;

            return result;
        }

        private AppVersion GetInstalledVersion()
        {
            // The stored version (the release tag which was installed) has priority, so it can be
            // changed for testing. When it is empty, the version is taken from the assembly.
            try
            {
                string storedVersion = SettingsClient.Get<string>("Updates", InstalledVersionSetting);
                AppVersion parsedVersion = AppVersion.Parse(storedVersion);

                if (parsedVersion != null)
                {
                    return parsedVersion;
                }
            }
            catch (Exception ex)
            {
                LogClient.Error("Update check: could not read the installed version. Exception: {0}", ex.Message);
            }

            return AppVersion.FromAssemblyVersion(ProcessExecutable.AssemblyVersion());
        }
    }
}
