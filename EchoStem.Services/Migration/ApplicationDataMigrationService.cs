using Digimezzo.Foundation.Core.Logging;
using EchoStem.Core.Base;
using Digimezzo.Foundation.Core.Settings;
using System;
using System.IO;
using System.Threading.Tasks;

namespace EchoStem.Services.Migration
{
    /// <summary>
    /// Copies user data from the old Dopamine data folder (%AppData%\Dopamine) into the EchoStem
    /// data folder (%AppData%\EchoStem).
    ///
    /// The decision whether the migration should be offered is taken once, early in the application
    /// startup (before the settings and database of the new application are created), because after
    /// that both sides look like "user data" and cannot be told apart any more.
    /// </summary>
    public sealed class ApplicationDataMigrationService : IApplicationDataMigrationService
    {
        private const string OldFolderName = "Dopamine";
        private const string OldDatabaseName = "Dopamine.db";
        private const string NewDatabaseName = "EchoStem.db";
        private const string MarkerFileName = ".dopamine-migration-handled";
        private const string PendingSettingsFileName = "Settings.xml.import-pending";
        private const string ApplicationName = "EchoStem";

        private static bool migrationPending;
        private static string detectedSourceFolder;

        /// <summary>
        /// Must be called once at startup, before the new application creates its settings/database.
        /// </summary>
        private static string detectionSourceFolder;
        private static string detectionTargetFolder;
        private static bool detectionSourceHasData;
        private static bool detectionTargetHasData;
        private static bool detectionAlreadyHandled;
        private static string detectionError;

        /// <summary>
        /// Must be called before anything else in the application startup: every Digimezzo client
        /// (settings, logging) creates its default files on first use, which would make the new data
        /// folder look like it already contains user data. This method only touches the file system
        /// and does not log (logging itself would create the settings file).
        /// </summary>
        public static void DetectAtStartup()
        {
            migrationPending = false;
            detectedSourceFolder = null;
            detectionError = null;

            try
            {
                // The data folder of the new application is derived from the executable name; do not
                // use SettingsClient here, because creating it would create Settings.xml.
                detectionSourceFolder = GetOldDataFolder();
                detectionTargetFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ApplicationName);

                detectionSourceHasData =
                    System.IO.File.Exists(Path.Combine(detectionSourceFolder, "Settings.xml")) ||
                    System.IO.File.Exists(Path.Combine(detectionSourceFolder, OldDatabaseName));

                detectionAlreadyHandled = System.IO.File.Exists(Path.Combine(detectionTargetFolder, MarkerFileName));
                detectionTargetHasData =
                    System.IO.File.Exists(Path.Combine(detectionTargetFolder, "Settings.xml")) ||
                    System.IO.File.Exists(Path.Combine(detectionTargetFolder, NewDatabaseName));

                migrationPending = detectionSourceHasData && !detectionAlreadyHandled && !detectionTargetHasData;
                detectedSourceFolder = migrationPending ? detectionSourceFolder : null;
            }
            catch (Exception ex)
            {
                migrationPending = false;
                detectedSourceFolder = null;
                detectionError = ex.Message;
            }
        }

        /// <summary>
        /// Must be called at the very beginning of the next startup, before the settings client reads its
        /// file: promotes the migrated settings file to the live settings file. The running session
        /// writes its own settings when it shuts down, so the migrated file has to wait for the restart.
        /// </summary>
        public static void ApplyPendingSettingsImport()
        {
            try
            {
                string targetFolder = GetNewDataFolderForDetection();
                string pendingPath = Path.Combine(targetFolder, PendingSettingsFileName);

                if (!System.IO.File.Exists(pendingPath))
                {
                    return;
                }

                string settingsPath = Path.Combine(targetFolder, "Settings.xml");
                System.IO.File.Copy(pendingPath, settingsPath, true);
                System.IO.File.Delete(pendingPath);
            }
            catch
            {
                // A failed import must never block the startup; the defaults are used then.
            }
        }

        /// <summary>Logs the result of <see cref="DetectAtStartup"/> once logging is available.</summary>
        public static void LogDetectionResult()
        {
            if (detectionError != null)
            {
                LogClient.Error("Could not detect old Dopamine data. Exception: {0}", detectionError);
                return;
            }

            LogClient.Info(
                "Migration detection: source={0}, sourceHasData={1}, target={2}, targetHasData={3}, alreadyHandled={4}, pending={5}",
                detectionSourceFolder, detectionSourceHasData, detectionTargetFolder, detectionTargetHasData,
                detectionAlreadyHandled, migrationPending);
        }

        /// <summary>
        /// True when the user asked for the migration in the wizard. The actual copy runs after the
        /// wizard has closed, because closing it writes the (still default) settings of this session.
        /// </summary>
        public static bool IsMigrationRequested { get; private set; }

        public static void RequestMigration()
        {
            IsMigrationRequested = true;
        }

        public bool IsMigrationAvailable => migrationPending && !string.IsNullOrEmpty(detectedSourceFolder);

        public string SourceFolder => detectedSourceFolder;

        public async Task MigrateAsync(IProgress<string> progress)
        {
            string sourceFolder = GetOldDataFolder();
            string targetFolder = GetNewDataFolder();

            // ConfigureAwait(false): the caller runs on the UI thread and blocks on the result.
            await Task.Run(() =>
            {
                System.IO.Directory.CreateDirectory(targetFolder);

                LogClient.Info("Migration: copying settings");
                Report(progress, "settings");
                // Settings are imported on the next startup (before the settings client reads them):
                // the current session writes its own settings when it shuts down, which would
                // overwrite a file copied now.
                CopyFile(sourceFolder, targetFolder, "Settings.xml", PendingSettingsFileName);

                LogClient.Info("Migration: copying database");
                Report(progress, "database");
                CopyFile(sourceFolder, targetFolder, OldDatabaseName, NewDatabaseName);

                LogClient.Info("Migration: copying providers");
                Report(progress, "providers");
                CopyFile(sourceFolder, targetFolder, "Providers.xml", "Providers.xml");

                LogClient.Info("Migration: copying color schemes");
                Report(progress, "colorschemes");
                CopyFolder(sourceFolder, targetFolder, "ColorSchemes");

                LogClient.Info("Migration: copying equalizer");
                Report(progress, "equalizer");
                CopyFolder(sourceFolder, targetFolder, "Equalizer");

                LogClient.Info("Migration: copying languages");
                Report(progress, "languages");
                CopyFolder(sourceFolder, targetFolder, "Languages");

                // Album artwork cache: the database refers to these files by name, so without them
                // every album would show its placeholder until the artwork is downloaded again.
                LogClient.Info("Migration: copying artwork cache");
                Report(progress, "artwork");
                CopyFolder(sourceFolder, targetFolder, Path.Combine("Cache", "CoverArt"));
            }).ConfigureAwait(false);

            LogClient.Info("Migration copy finished.");
        }

        public void MarkAsHandled()
        {
            try
            {
                string targetFolder = GetNewDataFolder();
                System.IO.Directory.CreateDirectory(targetFolder);
                System.IO.File.WriteAllText(Path.Combine(targetFolder, MarkerFileName), DateTime.Now.ToString("s"));
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not write the migration marker. Exception: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Test seam: copies the known data entries from one folder to another. Public so the copy
        /// logic can be exercised without touching the real user profile.
        /// </summary>
        public static void MigrateFolders(string sourceFolder, string targetFolder)
        {
            System.IO.Directory.CreateDirectory(targetFolder);

            CopyFile(sourceFolder, targetFolder, "Settings.xml", PendingSettingsFileName);
            CopyFile(sourceFolder, targetFolder, OldDatabaseName, NewDatabaseName);
            CopyFile(sourceFolder, targetFolder, "Providers.xml", "Providers.xml");
            CopyFolder(sourceFolder, targetFolder, "ColorSchemes");
            CopyFolder(sourceFolder, targetFolder, "Equalizer");
            CopyFolder(sourceFolder, targetFolder, "Languages");
            CopyFolder(sourceFolder, targetFolder, Path.Combine("Cache", "CoverArt"));
        }

        private static void Report(IProgress<string> progress, string step)
        {
            if (progress != null)
            {
                progress.Report(step);
            }
        }

        private static void CopyFile(string sourceFolder, string targetFolder, string sourceName, string targetName)
        {
            string sourcePath = Path.Combine(sourceFolder, sourceName);

            if (!System.IO.File.Exists(sourcePath))
            {
                return;
            }

            string targetPath = Path.Combine(targetFolder, targetName);

            // Copy next to the target first, so a failed copy never leaves a half written file.
            string temporaryPath = targetPath + ".migrating";

            System.IO.File.Copy(sourcePath, temporaryPath, true);

            if (System.IO.File.Exists(targetPath))
            {
                System.IO.File.Delete(targetPath);
            }

            System.IO.File.Move(temporaryPath, targetPath);
        }

        private static void CopyFolder(string sourceFolder, string targetFolder, string folderName)
        {
            string sourcePath = Path.Combine(sourceFolder, folderName);

            if (!System.IO.Directory.Exists(sourcePath))
            {
                return;
            }

            string targetPath = Path.Combine(targetFolder, folderName);

            foreach (string directory in System.IO.Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                System.IO.Directory.CreateDirectory(Path.Combine(targetPath, GetRelativePath(sourcePath, directory)));
            }

            System.IO.Directory.CreateDirectory(targetPath);

            foreach (string file in System.IO.Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(targetPath, GetRelativePath(sourcePath, file));
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(destination));

                System.IO.File.Copy(file, destination, true);
            }
        }

        private static string GetRelativePath(string basePath, string path)
        {
            if (path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                return path.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar);
            }

            return Path.GetFileName(path);
        }

        private static string GetNewDataFolderForDetection()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductInformation.ApplicationName);
        }

        private static string GetOldDataFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), OldFolderName);
        }

        private static string GetNewDataFolder()
        {
            return SettingsClient.ApplicationFolder();
        }
    }
}
