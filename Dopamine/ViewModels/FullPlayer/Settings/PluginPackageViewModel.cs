using Digimezzo.Foundation.Core.Utils;
using Dopamine.Plugins;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Diagnostics;

namespace Dopamine.ViewModels.FullPlayer.Settings
{
    /// <summary>
    /// One row in the Plugins settings page. The row owns its "open folder"
    /// action so the list stays a plain ItemsControl.
    /// </summary>
    public sealed class PluginPackageViewModel : BindableBase
    {
        private readonly PluginPackage package;

        public PluginPackageViewModel(PluginPackage package)
        {
            this.package = package;

            this.OpenFolderCommand = new DelegateCommand(() =>
            {
                try
                {
                    if (System.IO.Directory.Exists(this.Directory))
                    {
                        Process.Start("explorer.exe", "\"" + this.Directory + "\"");
                    }
                }
                catch
                {
                    // Opening a folder is a convenience; never fail the settings page.
                }
            });
        }

        public string Name
        {
            get { return this.package.Manifest.Name; }
        }

        public string Version
        {
            get { return this.package.Manifest.Version; }
        }

        public string Kind
        {
            get { return this.package.Manifest.Kind; }
        }

        public string KindDisplay
        {
            get
            {
                if (string.Equals(this.Kind, "dsh-web", StringComparison.OrdinalIgnoreCase))
                {
                    return ResourceUtils.GetString("Language_Plugin_Kind_Dsh_Web");
                }

                return this.Kind;
            }
        }

        public string Description
        {
            get { return this.package.Manifest.Description; }
        }

        public string Directory
        {
            get { return this.package.Directory; }
        }

        public DelegateCommand OpenFolderCommand { get; private set; }
    }
}
