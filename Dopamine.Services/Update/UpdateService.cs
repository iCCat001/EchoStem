using Digimezzo.Foundation.Core.IO;
using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Settings;
using Dopamine.Core.Api.GitHub;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Dopamine.Services.Update
{
    public class UpdateService : IUpdateService
    {
        // The repository which publishes the releases and the portable packages.
        private const string RepositoryOwner = "iCCat001";
        private const string RepositoryName = "EchoStem";
        private const string PortableAssetPrefix = "Dopamine-Portable-";
        private const string PortableAssetExtension = ".zip";

        private const string InstalledVersionSetting = "InstalledVersion";
        private const string InstalledVersionAssemblySetting = "InstalledVersionAssembly";

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

                result.PortableAsset = release.Assets?
                    .FirstOrDefault(a => a.Name != null &&
                                         a.Name.StartsWith(PortableAssetPrefix, StringComparison.OrdinalIgnoreCase) &&
                                         a.Name.EndsWith(PortableAssetExtension, StringComparison.OrdinalIgnoreCase));

                if (result.PortableAsset == null)
                {
                    return this.CreateError(result, "The release does not contain a portable package.");
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

        public async Task<string> DownloadAndPrepareUpdateAsync(GitHubReleaseAsset portableAsset, string versionText,
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

                using (var client = new HttpClient())
                using (var response = await client.GetAsync(portableAsset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    long totalSize = response.Content.Headers.ContentLength ?? portableAsset.Size;
                    long receivedSize = 0;

                    using (var input = await response.Content.ReadAsStreamAsync())
                    using (var output = System.IO.File.Create(packagePath))
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

                string extractedFolder = Path.Combine(updateFolder, "Extracted");
                ZipFile.ExtractToDirectory(packagePath, extractedFolder);

                // The portable package contains the application in a "Dopamine" folder.
                string applicationFolder = Path.Combine(extractedFolder, "Dopamine");

                if (!Directory.Exists(applicationFolder))
                {
                    applicationFolder = extractedFolder;
                }

                // Remember which release is being installed, and the build it was applied to, so
                // the next check compares against it.
                SettingsClient.Set<string>("Updates", InstalledVersionSetting, versionText);
                SettingsClient.Set<string>("Updates", InstalledVersionAssemblySetting, GetAssemblyVersionOfFile(applicationFolder));

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
                script.AppendLine($"start \"\" \"{applicationPath}\"");
                script.AppendLine("del \"%~f0\"");

                // The command processor reads the script with the system code page.
                System.IO.File.WriteAllText(scriptPath, script.ToString(), Encoding.Default);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"\"{scriptPath}\"\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
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

        // Reads the assembly version of the executable of a prepared update, so the version which
        // is recorded after installing it can be tied to that exact build.
        private static string GetAssemblyVersionOfFile(string applicationFolder)
        {
            try
            {
                string executableName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
                string executablePath = Path.Combine(applicationFolder, executableName);

                if (System.IO.File.Exists(executablePath))
                {
                    return AssemblyName.GetAssemblyName(executablePath).Version?.ToString() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                LogClient.Error("Update: could not read the assembly version of the update. Exception: {0}", ex.Message);
            }

            return string.Empty;
        }
    }
}
