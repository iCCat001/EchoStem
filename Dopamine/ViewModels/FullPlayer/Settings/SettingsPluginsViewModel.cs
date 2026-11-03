using Digimezzo.Foundation.Core.Utils;
using Dopamine.Plugins;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Dopamine.ViewModels.FullPlayer.Settings
{
    /// <summary>
    /// Lists the plugin packages bundled with EchoStem and the ones placed in
    /// the per-user plugin folder. The page is intentionally read-only: plugin
    /// installation is owned by each integration (for example the DSH Plugin
    /// Manager), while this page is the catalog and folder launcher.
    /// </summary>
    public class SettingsPluginsViewModel : BindableBase
    {
        private readonly IPluginCatalogService pluginCatalogService;

        private ObservableCollection<PluginPackageViewModel> plugins;
        private bool hasPlugins;

        public SettingsPluginsViewModel(IPluginCatalogService pluginCatalogService)
        {
            this.pluginCatalogService = pluginCatalogService;

            this.RefreshCommand = new DelegateCommand(this.Refresh);
            this.OpenPluginsFolderCommand = new DelegateCommand(this.OpenPluginsFolder);

            this.Refresh();
        }

        public DelegateCommand RefreshCommand { get; private set; }

        public DelegateCommand OpenPluginsFolderCommand { get; private set; }

        public ObservableCollection<PluginPackageViewModel> Plugins
        {
            get { return this.plugins; }
            private set
            {
                if (this.SetProperty<ObservableCollection<PluginPackageViewModel>>(ref this.plugins, value))
                {
                    this.HasPlugins = value != null && value.Count > 0;
                }
            }
        }

        public bool HasPlugins
        {
            get { return this.hasPlugins; }
            private set { this.SetProperty<bool>(ref this.hasPlugins, value); }
        }

        private void Refresh()
        {
            this.Plugins = new ObservableCollection<PluginPackageViewModel>(
                this.pluginCatalogService.GetPlugins().Select(package => new PluginPackageViewModel(package)));
        }

        private void OpenPluginsFolder()
        {
            try
            {
                string root = this.pluginCatalogService.BundledPluginsRoot;

                if (Directory.Exists(root))
                {
                    Process.Start("explorer.exe", "\"" + root + "\"");
                }
            }
            catch
            {
                // Opening a folder is a convenience; never fail the settings page.
            }
        }
    }
}
