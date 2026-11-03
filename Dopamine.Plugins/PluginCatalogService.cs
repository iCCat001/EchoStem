using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dopamine.Plugins
{
    /// <summary>
    /// Scans the application and user plugin folders for <c>plugin.json</c>
    /// manifests. Broken packages are skipped instead of failing the catalog, so
    /// one malformed future plugin cannot take the settings page down.
    /// </summary>
    public sealed class PluginCatalogService : IPluginCatalogService
    {
        private const string ManifestFileName = "plugin.json";

        public string BundledPluginsRoot
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins"); }
        }

        public string UserPluginsRoot
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dopamine", "Plugins"); }
        }

        public IReadOnlyList<PluginPackage> GetPlugins()
        {
            var plugins = new List<PluginPackage>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            this.ScanRoot(this.BundledPluginsRoot, plugins, seenIds);
            this.ScanRoot(this.UserPluginsRoot, plugins, seenIds);

            return plugins
                .OrderBy(package => package.Manifest.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(package => package.Directory, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void ScanRoot(string root, IList<PluginPackage> plugins, ISet<string> seenIds)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return;
            }

            IEnumerable<string> manifestPaths;

            try
            {
                manifestPaths = Directory.EnumerateFiles(root, ManifestFileName, SearchOption.AllDirectories).ToList();
            }
            catch (Exception)
            {
                return;
            }

            foreach (string manifestPath in manifestPaths)
            {
                PluginPackage package = this.ReadPackage(manifestPath);

                if (package == null)
                {
                    continue;
                }

                string id = package.Manifest.Id;

                if (string.IsNullOrEmpty(id) || !seenIds.Add(id))
                {
                    continue;
                }

                plugins.Add(package);
            }
        }

        private PluginPackage ReadPackage(string manifestPath)
        {
            try
            {
                string json = File.ReadAllText(manifestPath);
                PluginManifest manifest = JsonConvert.DeserializeObject<PluginManifest>(json);

                if (manifest == null)
                {
                    return null;
                }

                if (string.IsNullOrWhiteSpace(manifest.Id) && string.IsNullOrWhiteSpace(manifest.Name))
                {
                    return null;
                }

                if (string.IsNullOrWhiteSpace(manifest.Id))
                {
                    manifest.Id = manifest.Name;
                }

                if (string.IsNullOrWhiteSpace(manifest.Name))
                {
                    manifest.Name = manifest.Id;
                }

                if (string.IsNullOrWhiteSpace(manifest.Version))
                {
                    manifest.Version = "0.0.0";
                }

                if (string.IsNullOrWhiteSpace(manifest.Kind))
                {
                    manifest.Kind = "unknown";
                }

                if (manifest.Description == null)
                {
                    manifest.Description = string.Empty;
                }

                string directory = Path.GetDirectoryName(manifestPath);

                if (string.IsNullOrEmpty(directory))
                {
                    return null;
                }

                return new PluginPackage(manifest, directory, manifestPath);
            }
            catch (Exception)
            {
                // A malformed package is ignored; the rest of the catalog stays usable.
                return null;
            }
        }
    }
}
