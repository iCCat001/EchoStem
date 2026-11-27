using Digimezzo.Foundation.Core.Settings;
using Digimezzo.Foundation.WPF.Controls;
using EchoStem.Services.Indexing;
using EchoStem.Services.Migration;
using System.ComponentModel;
using System.Windows;

namespace EchoStem.Views
{
    public partial class Oobe : Windows10BorderlessWindow
    {
        private IIndexingService indexingService;

        public Oobe(IIndexingService indexingService)
        {
            InitializeComponent();

            this.indexingService = indexingService;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Prevent the OOBE window from appearing the next time the application is started
            SettingsClient.Set<bool>("General", "ShowOobe", false);

            // When a data migration was requested, the application copies the old data right after this
            // window closed and restarts. Starting the indexer here would open the database that is
            // about to be replaced.
            if (ApplicationDataMigrationService.IsMigrationRequested)
            {
                return;
            }

            // We're closing the OOBE window, tell the IndexingService to start checking the collection.
            this.indexingService.RefreshCollectionImmediatelyAsync();
        }

        private void ButtonFinish_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
