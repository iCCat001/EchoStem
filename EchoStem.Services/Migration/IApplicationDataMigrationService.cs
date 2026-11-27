using System;
using System.Threading.Tasks;

namespace EchoStem.Services.Migration
{
    /// <summary>
    /// Handles the one-time migration of the user data that was created by the old Dopamine
    /// installation (settings, music library database, providers and user folders) into the EchoStem
    /// data folder.
    /// </summary>
    public interface IApplicationDataMigrationService
    {
        /// <summary>True when old data was found at startup and has not been handled yet.</summary>
        bool IsMigrationAvailable { get; }

        /// <summary>The detected old data folder (for diagnostics/UI).</summary>
        string SourceFolder { get; }

        /// <summary>Copies the old data into the EchoStem data folder. The old data is not modified.</summary>
        Task MigrateAsync(IProgress<string> progress);

        /// <summary>Remembers that the user made a decision, so the wizard is not shown again.</summary>
        void MarkAsHandled();
    }
}
